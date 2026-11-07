using UnityEngine;
using UnityEngine.UI;

namespace DTK.Game
{
    /// <summary>Tiny uGUI toolkit: procedural sprites (rounded box, circle, flame, coin), fonts and element factories.</summary>
    public static class Ui
    {
        static Sprite rounded, circle, flame, coin, ball, soft;
        static Font fontBlack, fontBold, fontSemi;

        public static Font FontBlack => fontBlack != null ? fontBlack : (fontBlack = LoadFont("BarlowCondensed-ExtraBold"));
        public static Font FontBold => fontBold != null ? fontBold : (fontBold = LoadFont("BarlowCondensed-Bold"));
        public static Font FontSemi => fontSemi != null ? fontSemi : (fontSemi = LoadFont("BarlowCondensed-SemiBold"));

        static Font LoadFont(string name)
        {
            var f = Resources.Load<Font>("Fonts/" + name);
            if (f == null) f = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Arial" }, 32);
            return f;
        }

        public static Sprite Rounded => rounded != null ? rounded : (rounded = MakeRounded(64, 22));
        public static Sprite Circle => circle != null ? circle : (circle = MakeCircle(128));
        public static Sprite Soft => soft != null ? soft : (soft = MakeSoft(64));
        public static Sprite Flame => flame != null ? flame : (flame = MakeFlame(64));
        public static Sprite Coin => coin != null ? coin : (coin = MakeCoin(64));
        public static Sprite BallIcon => ball != null ? ball : (ball = MakeBall(64));

        static Sprite MakeRounded(int size, int radius)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(0, Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius)));
                    float dy = Mathf.Max(0, Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius)));
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(radius - d + 0.5f);
                    t.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }

        static Sprite MakeCircle(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            float r = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(r - d)));
                }
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        static Sprite MakeSoft(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float r = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                    t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(1 - d * d)));
                }
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        static Sprite MakeFlame(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2 - 1, v = (y + 0.5f) / size;
                    // teardrop: circle at the bottom, pointy top
                    float width = v < 0.45f ? Mathf.Sqrt(Mathf.Max(0, 0.2025f - (v - 0.45f) * (v - 0.45f))) / 0.45f : (1 - v) / 0.55f;
                    float inside = Mathf.Clamp01((width * 0.85f - Mathf.Abs(u)) * size * 0.5f);
                    var outer = new Color(1f, 0.45f, 0.08f);
                    var inner = new Color(1f, 0.88f, 0.25f);
                    float core = Mathf.Clamp01((width * 0.45f - Mathf.Abs(u)) * size * 0.5f) * (v < 0.6f ? 1 : 0);
                    var c = Color.Lerp(outer, inner, core);
                    c.a = inside;
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        static Sprite MakeCoin(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float r = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    float a = Mathf.Clamp01(r - d);
                    var c = d < r * 0.72f ? new Color(1f, 0.82f, 0.2f) : new Color(0.95f, 0.62f, 0.1f);
                    if (d < r * 0.5f && d > r * 0.36f) c = new Color(1f, 0.93f, 0.55f);
                    c.a = a;
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        static Sprite MakeBall(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float r = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2(x + 0.5f - r, y + 0.5f - r);
                    float d = p.magnitude;
                    float a = Mathf.Clamp01(r - d);
                    var c = Color.white;
                    if (d > r - 3) c = new Color(0.1f, 0.1f, 0.1f);
                    float ang = Mathf.Atan2(p.y, p.x);
                    if (d < r * 0.3f) c = new Color(0.1f, 0.1f, 0.12f);
                    else if (d > r * 0.62f && d < r * 0.9f && Mathf.Repeat(ang * 5f / (2 * Mathf.PI), 1f) < 0.35f) c = new Color(0.1f, 0.1f, 0.12f);
                    c.a = a;
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        // ------------------------------------------------------------------ factories

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static Image Image(Transform parent, string name, Sprite sprite, Color color)
        {
            var rt = Rect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            if (sprite != null && sprite.border != Vector4.zero) img.type = UnityEngine.UI.Image.Type.Sliced;
            return img;
        }

        public static RawImage Raw(Transform parent, string name, Texture tex)
        {
            var rt = Rect(parent, name);
            var img = rt.gameObject.AddComponent<RawImage>();
            img.texture = tex;
            img.raycastTarget = false;
            return img;
        }

        public static Text Text(Transform parent, string name, string value, int size, Color color, TextAnchor anchor, Font font = null, bool outline = true)
        {
            var rt = Rect(parent, name);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font ?? FontBold;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.text = value;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = false;
            if (outline)
            {
                var sh = rt.gameObject.AddComponent<Shadow>();
                sh.effectColor = new Color(0, 0, 0, 0.55f);
                sh.effectDistance = new Vector2(2, -3);
            }
            return t;
        }

        /// <summary>Circular photo (or initials when there is no photo). Returns the RawImage that receives the picture.</summary>
        public static RawImage Avatar(Transform parent, string name, float size, out Text initials)
        {
            var root = Rect(parent, name);
            root.sizeDelta = new Vector2(size, size);
            var ring = Image(root, "Ring", Circle, Color.white);
            Stretch(ring.rectTransform, -4);
            var mask = Image(root, "Mask", Circle, new Color(0.25f, 0.28f, 0.35f));
            Stretch(mask.rectTransform);
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            initials = Text(mask.transform, "Initials", "", Mathf.RoundToInt(size * 0.42f), Color.white, TextAnchor.MiddleCenter, FontBlack, false);
            Stretch(initials.rectTransform);
            var raw = Raw(mask.transform, "Photo", null);
            Stretch(raw.rectTransform);
            raw.enabled = false;
            return raw;
        }

        public static void Stretch(RectTransform rt, float pad = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(pad, pad);
            rt.offsetMax = new Vector2(-pad, -pad);
        }

        /// <summary>Place a rect by its centre in reference pixels (1080 wide), y measured from the top of the screen.</summary>
        public static void PlaceTop(RectTransform rt, float centerX, float topY, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(centerX, -topY);
            rt.sizeDelta = new Vector2(w, h);
        }

        public static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }

        public static string Initials(string name)
        {
            if (string.IsNullOrEmpty(name)) return "?";
            foreach (char c in name) if (char.IsLetterOrDigit(c)) return char.ToUpperInvariant(c).ToString();
            return name.Substring(0, 1);
        }

        public static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        /// <summary>Turkish-correct upper case (i -> İ, ı -> I).</summary>
        public static string UpperTr(string s) => string.IsNullOrEmpty(s) ? s : s.Replace('i', 'İ').Replace('ı', 'I').ToUpperInvariant();

        public static string Thousands(long v) => v.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture).Replace(',', '.');
    }
}
