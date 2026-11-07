using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DTK.Game
{
    /// <summary>
    /// Flag images from StreamingAssets/flags/{code}.png (public-domain flags, flagcdn) and team colours.
    /// Windows cannot draw flag emoji, so the screen always uses these images (DESIGN note).
    /// </summary>
    public static class Flags
    {
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, Color[]> colorCache = new Dictionary<string, Color[]>();
        static RootConfig cfg;

        static readonly Dictionary<string, string> ShortNames = new Dictionary<string, string>
        {
            {"TR","TÜR"},{"VN","VIE"},{"AZ","AZE"},{"ID","IDN"},{"BR","BRA"},{"AR","ARG"},{"MX","MEX"},{"MA","MAR"},
            {"PK","PAK"},{"PH","PHI"},{"DE","GER"},{"FR","FRA"},{"ES","ESP"},{"IT","ITA"},{"GB","GBR"},{"GB-ENG","ENG"},
            {"GB-SCT","SCO"},{"GB-WLS","WAL"},{"US","USA"},{"NL","NED"},{"PT","POR"},{"BE","BEL"},{"SA","KSA"},{"EG","EGY"},
            {"IQ","IRQ"},{"IR","IRN"},{"IN","IND"},{"BD","BAN"},{"NG","NGA"},{"CO","COL"},{"PE","PER"},{"CL","CHI"},
            {"UY","URU"},{"EC","ECU"},{"VE","VEN"},{"RU","RUS"},{"UA","UKR"},{"KZ","KAZ"},{"UZ","UZB"},{"GE","GEO"},
            {"AL","ALB"},{"BA","BIH"},{"RS","SRB"},{"BG","BUL"},{"RO","ROU"},{"GR","GRE"},{"SY","SYR"},{"DZ","ALG"},
            {"TN","TUN"},{"QA","QAT"},{"AE","UAE"},{"MY","MAS"},{"TH","THA"},{"KR","KOR"},{"JP","JPN"},{"CN","CHN"},
        };

        static readonly Dictionary<string, string> FullNamesTr = new Dictionary<string, string>
        {
            {"TR","TÜRKİYE"},{"VN","VİETNAM"},{"AZ","AZERBAYCAN"},{"ID","ENDONEZYA"},{"BR","BREZİLYA"},{"AR","ARJANTİN"},
            {"MX","MEKSİKA"},{"MA","FAS"},{"PK","PAKİSTAN"},{"PH","FİLİPİNLER"},{"DE","ALMANYA"},{"FR","FRANSA"},
            {"ES","İSPANYA"},{"IT","İTALYA"},{"GB-ENG","İNGİLTERE"},{"US","ABD"},{"NL","HOLLANDA"},{"PT","PORTEKİZ"},
            {"SA","S. ARABİSTAN"},{"EG","MISIR"},{"IQ","IRAK"},{"IR","İRAN"},{"IN","HİNDİSTAN"},{"RU","RUSYA"},
            {"UZ","ÖZBEKİSTAN"},{"KZ","KAZAKİSTAN"},{"GE","GÜRCİSTAN"},{"DZ","CEZAYİR"},{"TN","TUNUS"},{"MY","MALEZYA"},
            {"TH","TAYLAND"},{"KR","G. KORE"},{"JP","JAPONYA"},{"CO","KOLOMBİYA"},{"BD","BANGLADEŞ"},{"NG","NİJERYA"},
        };

        public static void Init(RootConfig config) { cfg = config; colorCache.Clear(); }

        public static string Short(string code)
        {
            if (string.IsNullOrEmpty(code)) return "?";
            if (cfg?.presentation.teamNames != null && cfg.presentation.teamNames.TryGetValue(code, out var n)) return n;
            return ShortNames.TryGetValue(code, out var s) ? s : code;
        }

        public static string FullName(string code)
        {
            if (string.IsNullOrEmpty(code)) return "";
            return FullNamesTr.TryGetValue(code, out var s) ? s : Short(code);
        }

        public static Texture2D Get(string code)
        {
            if (string.IsNullOrEmpty(code)) return null;
            if (cache.TryGetValue(code, out var t)) return t;
            string file = Path.Combine(Application.streamingAssetsPath, "flags", code.ToLowerInvariant() + ".png");
            t = null;
            if (File.Exists(file))
            {
                t = new Texture2D(2, 2, TextureFormat.RGBA32, true) { name = "flag_" + code, wrapMode = TextureWrapMode.Clamp, anisoLevel = 4 };
                t.LoadImage(File.ReadAllBytes(file), false);
                t.filterMode = FilterMode.Trilinear;
            }
            cache[code] = t;
            return t;
        }

        /// <summary>[0] = shirt, [1] = shorts/secondary. From config, else the two most common flag colours.</summary>
        public static Color[] TeamColors(string code)
        {
            if (string.IsNullOrEmpty(code)) return new[] { Color.gray, Color.white };
            if (colorCache.TryGetValue(code, out var c)) return c;
            if (cfg?.presentation.teamColors != null && cfg.presentation.teamColors.TryGetValue(code, out var hex) && hex != null && hex.Length > 0)
            {
                ColorUtility.TryParseHtmlString(hex[0], out var p);
                Color s = Color.white;
                if (hex.Length > 1) ColorUtility.TryParseHtmlString(hex[1], out s);
                c = new[] { p, s };
            }
            else c = Dominant(Get(code));
            colorCache[code] = c;
            return c;
        }

        static Color[] Dominant(Texture2D t)
        {
            if (t == null) return new[] { new Color(0.5f, 0.5f, 0.55f), Color.white };
            var px = t.GetPixels32();
            var buckets = new Dictionary<int, int>();
            var sums = new Dictionary<int, Vector3>();
            for (int i = 0; i < px.Length; i += 7)
            {
                var p = px[i];
                int key = (p.r >> 5) << 6 | (p.g >> 5) << 3 | (p.b >> 5);
                buckets.TryGetValue(key, out int n); buckets[key] = n + 1;
                sums.TryGetValue(key, out var s); sums[key] = s + new Vector3(p.r, p.g, p.b);
            }
            var keys = new List<int>(buckets.Keys);
            keys.Sort((a, b) => buckets[b].CompareTo(buckets[a]));
            Color C(int k) { var s = sums[k] / buckets[k] / 255f; return new Color(s.x, s.y, s.z); }
            Color first = C(keys[0]);
            Color second = Color.white;
            for (int i = 1; i < keys.Count; i++)
            {
                var c2 = C(keys[i]);
                if (Distance(c2, first) > 0.35f) { second = c2; break; }
            }
            // a white shirt reads badly from far away: prefer the coloured one as the shirt
            if (Lum(first) > 0.85f && Lum(second) < 0.85f) { var tmp = first; first = second; second = tmp; }
            return new[] { first, second };
        }

        public static float Lum(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
        public static float Distance(Color a, Color b) => Mathf.Sqrt((a.r - b.r) * (a.r - b.r) + (a.g - b.g) * (a.g - b.g) + (a.b - b.b) * (a.b - b.b));

        /// <summary>Kit colours for both teams, swapping the away kit when the shirts would look alike.</summary>
        public static void MatchKits(string home, string away, out Color[] homeKit, out Color[] awayKit)
        {
            homeKit = TeamColors(home);
            awayKit = (Color[])TeamColors(away).Clone();
            if (Distance(homeKit[0], awayKit[0]) < 0.4f)
            {
                var swapped = new[] { awayKit[1], awayKit[0] };
                awayKit = Distance(homeKit[0], swapped[0]) >= 0.4f ? swapped : new[] { new Color(0.12f, 0.12f, 0.16f), awayKit[0] };
            }
        }
    }
}
