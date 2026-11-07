using System.Collections.Generic;
using UnityEngine;

namespace DTK.Game
{
    /// <summary>
    /// Runtime material factory. Base materials live in Resources/Materials (created by the editor setup) so their
    /// shaders are part of the build; we only copy them and change colours/textures, never shader keywords.
    /// </summary>
    public static class Mats
    {
        static readonly Dictionary<string, Material> baseCache = new Dictionary<string, Material>();
        static readonly Dictionary<string, Material> colorCache = new Dictionary<string, Material>();
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        static readonly int Smoothness = Shader.PropertyToID("_Smoothness");

        static Material Base(string name)
        {
            if (baseCache.TryGetValue(name, out var m) && m != null) return m;
            m = Resources.Load<Material>("Materials/" + name);
            if (m == null)
            {
                Debug.LogError("Eksik materyal: Resources/Materials/" + name + " (DTK > Kurulum menüsünü çalıştırın)");
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            }
            baseCache[name] = m;
            return m;
        }

        /// <summary>Shared lit material of a colour (cached, do not modify the result).</summary>
        public static Material Lit(Color c, float smoothness = 0.25f)
        {
            string key = "lit" + ColorUtility.ToHtmlStringRGBA(c) + smoothness.ToString("0.00");
            if (colorCache.TryGetValue(key, out var m)) return m;
            m = new Material(Base("M_Lit"));
            m.SetColor(BaseColor, c);
            m.SetFloat(Smoothness, smoothness);
            colorCache[key] = m;
            return m;
        }

        /// <summary>New lit material instance that the caller may modify.</summary>
        public static Material LitInstance(Color c, Texture tex = null, float smoothness = 0.2f)
        {
            var m = new Material(Base("M_Lit"));
            m.SetColor(BaseColor, c);
            m.SetFloat(Smoothness, smoothness);
            if (tex != null) m.SetTexture(BaseMap, tex);
            return m;
        }

        public static Material Emissive(Color baseColor, Color emission)
        {
            var m = new Material(Base("M_LitEmissive"));
            m.SetColor(BaseColor, baseColor);
            m.SetColor(EmissionColor, emission);
            return m;
        }

        public static Material Transparent(Color c, Texture tex = null)
        {
            var m = new Material(Base("M_LitTransparent"));
            m.SetColor(BaseColor, c);
            if (tex != null) m.SetTexture(BaseMap, tex);
            return m;
        }

        public static Material Unlit(Color c, Texture tex = null)
        {
            var m = new Material(Base("M_Unlit"));
            m.SetColor(BaseColor, c);
            if (tex != null) m.SetTexture(BaseMap, tex);
            return m;
        }

        public static Material Crowd() => new Material(Base("M_Crowd"));

        public static void SetColor(Material m, Color c) => m.SetColor(BaseColor, c);
        public static void SetEmission(Material m, Color c) => m.SetColor(EmissionColor, c);
        public static void SetTexture(Material m, Texture t) => m.SetTexture(BaseMap, t);
    }
}
