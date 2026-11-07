using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DTK.Sim
{
    /// <summary>
    /// Turns a comment into a country code (DESIGN §4): flag emoji first, then a country name, then a bare code.
    /// Codes are upper-case ISO-3166 alpha-2 ("TR"), plus "GB-ENG", "GB-SCT", "GB-WLS" for the tag-sequence flags.
    /// </summary>
    public static class CountryResolver
    {
        // code | aliases (normalised: lower case, no diacritics, Turkish letters folded)
        const string AliasTable =
            "TR|turkiye,turkey,turk,turkei,turquie,turquia,tc,turkler|" +
            "VN|vietnam,viet nam,vn,vietnamese|" +
            "AZ|azerbaycan,azerbaijan,azerbaican,azeri,azerbaycanli|" +
            "ID|indonesia,endonezya,indo|" +
            "BR|brasil,brazil,brezilya|" +
            "AR|argentina,arjantin|" +
            "MX|mexico,meksika,mejico|" +
            "MA|maroc,morocco,fas,maghrib,almaghrib|" +
            "PK|pakistan|" +
            "PH|philippines,pilipinas,filipinler,pinoy|" +
            "DE|germany,deutschland,almanya|" +
            "FR|france,fransa|" +
            "ES|spain,espana,ispanya|" +
            "IT|italy,italia,italya|" +
            "GB|united kingdom,uk,ingiltere,britain|" +
            "GB-ENG|england|" +
            "GB-SCT|scotland,iskocya|" +
            "US|usa,united states,america,abd,amerika|" +
            "NL|netherlands,holland,hollanda,nederland|" +
            "PT|portugal,portekiz|" +
            "BE|belgium,belcika,belgique|" +
            "SA|saudi,saudi arabia,suudi arabistan,ksa|" +
            "EG|egypt,misir,masr|" +
            "IQ|iraq,irak|" +
            "IR|iran|" +
            "IN|india,hindistan,bharat|" +
            "BD|bangladesh,banglades|" +
            "NG|nigeria,nijerya|" +
            "CO|colombia,kolombiya|" +
            "PE|peru|" +
            "CL|chile,sili|" +
            "UY|uruguay|" +
            "EC|ecuador,ekvador|" +
            "VE|venezuela|" +
            "RU|russia,rusya,rossiya|" +
            "UA|ukraine,ukrayna,ukraina|" +
            "KZ|kazakhstan,kazakistan,qazaqstan|" +
            "UZ|uzbekistan,ozbekiston|" +
            "TM|turkmenistan|" +
            "KG|kyrgyzstan,kirgizistan|" +
            "GE|georgia,gurcistan,sakartvelo|" +
            "AL|albania,arnavutluk,shqiperia|" +
            "BA|bosnia,bosna|" +
            "RS|serbia,sirbistan,srbija|" +
            "XK|kosovo|" +
            "MK|north macedonia,makedonya|" +
            "BG|bulgaria,bulgaristan|" +
            "RO|romania,romanya|" +
            "GR|greece,yunanistan,hellas|" +
            "CY|cyprus,kibris|" +
            "SY|syria,suriye|" +
            "LB|lebanon,lubnan|" +
            "JO|jordan,urdun|" +
            "PS|palestine,filistin|" +
            "DZ|algeria,cezayir,algerie|" +
            "TN|tunisia,tunus,tunisie|" +
            "LY|libya|" +
            "QA|qatar,katar|" +
            "AE|uae,emirates,bae|" +
            "KW|kuwait,kuveyt|" +
            "AF|afghanistan,afganistan|" +
            "MY|malaysia,malezya|" +
            "TH|thailand,tayland|" +
            "KH|cambodia,kamboc|" +
            "MM|myanmar|" +
            "KR|korea,south korea,kore,guney kore|" +
            "JP|japan,japonya,nippon|" +
            "CN|china,cin|" +
            "NP|nepal|" +
            "LK|sri lanka|" +
            "AU|australia,avustralya|" +
            "CA|canada,kanada|" +
            "PL|poland,polonya,polska|" +
            "CZ|czechia,cekya|" +
            "HU|hungary,macaristan,magyarorszag|" +
            "AT|austria,avusturya|" +
            "CH|switzerland,isvicre|" +
            "SE|sweden,isvec|" +
            "NO|norway,norvec|" +
            "DK|denmark,danimarka|" +
            "HR|croatia,hirvatistan,hrvatska|" +
            "SN|senegal|" +
            "CM|cameroon,kamerun|" +
            "GH|ghana,gana|" +
            "CI|ivory coast,fildisi|" +
            "ZA|south africa,guney afrika";

        static Dictionary<string, string> aliasToCode;
        static List<string> multiWordAliases;
        static HashSet<string> knownCodes;

        static void EnsureTable()
        {
            if (aliasToCode != null) return;
            aliasToCode = new Dictionary<string, string>();
            multiWordAliases = new List<string>();
            knownCodes = new HashSet<string>();
            var parts = AliasTable.Split('|');
            for (int i = 0; i + 1 < parts.Length; i += 2)
            {
                string code = parts[i].Trim();
                knownCodes.Add(code);
                foreach (var a in parts[i + 1].Split(','))
                {
                    string alias = a.Trim();
                    if (alias.Length == 0) continue;
                    aliasToCode[alias] = code;
                    if (alias.IndexOf(' ') >= 0) multiWordAliases.Add(alias);
                }
            }
            // longest first so "south korea" beats "korea"
            multiWordAliases.Sort((x, y) => y.Length.CompareTo(x.Length));
        }

        /// <summary>Returns a country code or null. featured = codes that may be written as bare upper-case tokens ("TR").</summary>
        public static string Resolve(string text, ICollection<string> featured = null)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            EnsureTable();

            string flag = FirstFlag(text);
            if (flag != null) return flag;

            string norm = Normalize(text);
            string padded = " " + norm + " ";
            foreach (var alias in multiWordAliases)
                if (padded.Contains(" " + alias + " ")) return aliasToCode[alias];

            var tokens = norm.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            // The whole comment is a bare code ("tr", "VN").
            if (tokens.Length == 1 && tokens[0].Length == 2)
            {
                string up = tokens[0].ToUpperInvariant();
                if (knownCodes.Contains(up) || (featured != null && featured.Contains(up))) return up;
            }
            foreach (var t in tokens)
            {
                if (t.Length < 3) continue; // short words are too ambiguous ("it", "no", "me")
                if (aliasToCode.TryGetValue(t, out var code)) return code;
            }
            // Upper-case featured code inside a longer comment: "hadi TR!"
            if (featured != null)
            {
                var raw = text.Split(new[] { ' ', ',', '.', '!', '?', ':', ';', '-', '_', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var t in raw)
                    if (t.Length == 2 && char.IsUpper(t[0]) && char.IsUpper(t[1]) && featured.Contains(t)) return t;
            }
            return null;
        }

        /// <summary>First flag emoji in the text: two regional indicators, or the black-flag tag sequence (England etc).</summary>
        public static string FirstFlag(string text)
        {
            int i = 0;
            while (i < text.Length)
            {
                int cp = CodePointAt(text, i, out int len);
                if (cp >= 0x1F1E6 && cp <= 0x1F1FF)
                {
                    int j = i + len;
                    if (j < text.Length)
                    {
                        int cp2 = CodePointAt(text, j, out int len2);
                        if (cp2 >= 0x1F1E6 && cp2 <= 0x1F1FF)
                            return new string(new[] { (char)('A' + (cp - 0x1F1E6)), (char)('A' + (cp2 - 0x1F1E6)) });
                    }
                }
                else if (cp == 0x1F3F4)
                {
                    var sb = new StringBuilder();
                    int j = i + len;
                    while (j < text.Length)
                    {
                        int t = CodePointAt(text, j, out int tl);
                        if (t >= 0xE0061 && t <= 0xE007A) { sb.Append((char)('a' + (t - 0xE0061))); j += tl; }
                        else break;
                    }
                    string tag = sb.ToString();
                    if (tag == "gbeng") return "GB-ENG";
                    if (tag == "gbsct") return "GB-SCT";
                    if (tag == "gbwls") return "GB-WLS";
                }
                i += len;
            }
            return null;
        }

        /// <summary>Unicode code point at index i; len = 1 or 2 UTF-16 units. Broken surrogates are returned as-is.</summary>
        static int CodePointAt(string s, int i, out int len)
        {
            char c = s[i];
            if (char.IsHighSurrogate(c) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
            {
                len = 2;
                return char.ConvertToUtf32(c, s[i + 1]);
            }
            len = 1;
            return c;
        }

        /// <summary>Lower case, diacritics removed, Turkish letters folded, punctuation turned into spaces.</summary>
        public static string Normalize(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char c0 in s)
            {
                char c = c0;
                switch (c)
                {
                    case 'İ': case 'I': case 'ı': c = 'i'; break;
                    case 'Ş': case 'ş': c = 's'; break;
                    case 'Ğ': case 'ğ': c = 'g'; break;
                    case 'Ç': case 'ç': c = 'c'; break;
                    case 'Ö': case 'ö': c = 'o'; break;
                    case 'Ü': case 'ü': c = 'u'; break;
                    case 'Đ': case 'đ': c = 'd'; break;
                    case 'Ə': case 'ə': c = 'e'; break;
                }
                sb.Append(c);
            }
            string decomposed = sb.ToString().Normalize(NormalizationForm.FormD);
            var outSb = new StringBuilder(decomposed.Length);
            bool lastSpace = true;
            foreach (char c in decomposed)
            {
                var cat = CharUnicodeInfo.GetUnicodeCategory(c);
                if (cat == UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetterOrDigit(c))
                {
                    outSb.Append(char.ToLowerInvariant(c));
                    lastSpace = false;
                }
                else if (!lastSpace)
                {
                    outSb.Append(' ');
                    lastSpace = true;
                }
            }
            return outSb.ToString().Trim();
        }
    }
}
