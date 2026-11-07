using System.Collections.Generic;
using UnityEngine;

namespace DTK.Game
{
    /// <summary>Pitch dimensions in metres. Team 0 defends z = -HalfL (near the camera) and attacks +z.</summary>
    public static class Field
    {
        public const float HalfW = 13f, HalfL = 20f;
        public const float GoalHalfW = 2.6f, GoalH = 2.1f, GoalDepth = 1.6f;
        /// <summary>Sim units (-100..100) to metres along z.</summary>
        public const float UnitToZ = 18f / 100f;
        public static float Z(double units) => (float)units * UnitToZ;
        public static float AttackDir(int team) => team == 0 ? 1f : -1f;
    }

    /// <summary>
    /// Builds the whole low-poly stadium from code (DESIGN §13: LED boards and photo seats must show live data,
    /// so a ready-made stadium model would not work). Bright, clean, toy-like.
    /// </summary>
    public class Stadium : MonoBehaviour
    {
        public Light Sun { get; private set; }
        public Transform Trophy { get; private set; }
        public CrowdStand NearStand { get; private set; }
        public CrowdStand FarStand { get; private set; }
        public readonly List<LedBoard> Leds = new List<LedBoard>();
        readonly List<Light> floodSpots = new List<Light>();
        float sunBaseIntensity;
        float dimTarget = 1f, dim = 1f;

        public static Stadium Build(Transform parent)
        {
            var go = new GameObject("Stadium");
            go.transform.SetParent(parent, false);
            var s = go.AddComponent<Stadium>();
            s.BuildAll();
            return s;
        }

        void BuildAll()
        {
            var t = transform;

            // --- sun
            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(t, false);
            sunGo.transform.rotation = Quaternion.Euler(52f, -28f, 0f);
            Sun = sunGo.AddComponent<Light>();
            Sun.type = LightType.Directional;
            Sun.color = new Color(1f, 0.96f, 0.9f);
            Sun.intensity = 1.25f;
            Sun.shadows = LightShadows.Soft;
            Sun.shadowStrength = 0.75f;
            sunBaseIntensity = Sun.intensity;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.70f, 0.82f);
            RenderSettings.ambientEquatorColor = new Color(0.50f, 0.55f, 0.52f);
            RenderSettings.ambientGroundColor = new Color(0.25f, 0.28f, 0.22f);

            // --- ground and pitch
            MeshKit.Box("Ground", t, Mats.Lit(new Color(0.22f, 0.36f, 0.24f)), new Vector3(0, -0.06f, 0), new Vector3(160, 0.1f, 160), false);
            float runoff = 2.2f;
            var pitchTex = PitchTexture(Field.HalfW * 2 + runoff * 2, Field.HalfL * 2 + runoff * 2, runoff);
            var pitch = MeshKit.Obj("Pitch", t, MeshKit.Quad, Mats.LitInstance(Color.white, pitchTex, 0.05f), new Vector3(0, 0.005f, 0),
                new Vector3(Field.HalfW * 2 + runoff * 2, Field.HalfL * 2 + runoff * 2, 1), Quaternion.Euler(90, 0, 0), false);
            pitch.GetComponent<MeshRenderer>().receiveShadows = true;

            // --- goals
            BuildGoal(t, -1);
            BuildGoal(t, +1);

            // --- stands behind the goals (each team's fans sit behind its own goal, DESIGN §8)
            NearStand = CrowdStand.Build(t, "NearStand", new Vector3(0, 0, -Field.HalfL - runoff - 1.2f), 0f, 60f, 34, 1, roof: false);
            FarStand = CrowdStand.Build(t, "FarStand", new Vector3(0, 0, Field.HalfL + runoff + 1.2f), 180f, 50f, 12, 2);
            // low side stands so the edges are never empty
            CrowdStand.Build(t, "SideL", new Vector3(-Field.HalfW - runoff - 2.4f, 0, 0), 90f, 44f, 6, 3, roof: false, low: true);
            CrowdStand.Build(t, "SideR", new Vector3(Field.HalfW + runoff + 2.4f, 0, 0), -90f, 44f, 6, 4, roof: false, low: true);

            // --- LED boards along the touchlines (sponsors rotate here, DESIGN §9)
            Leds.Add(LedBoard.Build(t, new Vector3(-Field.HalfW - 1.3f, 0.45f, 0), 90f, Field.HalfL * 2 - 2f));
            Leds.Add(LedBoard.Build(t, new Vector3(Field.HalfW + 1.3f, 0.45f, 0), -90f, Field.HalfL * 2 - 2f));
            Leds.Add(LedBoard.Build(t, new Vector3(0, 0.45f, Field.HalfL + 1.4f), 180f, 18f));

            // --- floodlight masts
            foreach (var c in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
            {
                var basePos = new Vector3(c.x * (Field.HalfW + 9f), 0, c.y * (Field.HalfL + 9f));
                MeshKit.Bar("Mast", t, Mats.Lit(new Color(0.55f, 0.58f, 0.62f)), basePos, basePos + Vector3.up * 24f, 0.35f);
                var head = MeshKit.Box("Floodlight", t, Mats.Emissive(new Color(0.9f, 0.9f, 0.85f), new Color(2.2f, 2.2f, 2f)),
                    basePos + Vector3.up * 24.6f, new Vector3(4.5f, 2.2f, 0.4f), false);
                head.transform.LookAt(new Vector3(0, 0, 0));
                var spotGo = new GameObject("Spot");
                spotGo.transform.SetParent(t, false);
                spotGo.transform.position = basePos + Vector3.up * 24f;
                spotGo.transform.LookAt(Vector3.zero);
                var spot = spotGo.AddComponent<Light>();
                spot.type = LightType.Spot;
                spot.range = 80f;
                spot.spotAngle = 40f;
                spot.intensity = 0f;
                spot.color = new Color(1f, 0.97f, 0.9f);
                floodSpots.Add(spot);
            }

            // --- trophy (hidden until the ceremony)
            var trophy = MeshKit.Obj("Trophy", t, MeshKit.Trophy(), Mats.Lit(new Color(1f, 0.78f, 0.25f), 0.85f), new Vector3(0, 0, 0), Vector3.one * 1.1f);
            Trophy = trophy.transform;
            trophy.SetActive(false);
        }

        void BuildGoal(Transform parent, int side)
        {
            float z = side * Field.HalfL;
            var white = Mats.Lit(new Color(0.97f, 0.97f, 0.97f), 0.6f);
            float r = 0.09f;
            var lp = new Vector3(-Field.GoalHalfW, 0, z);
            var rp = new Vector3(Field.GoalHalfW, 0, z);
            MeshKit.Bar("PostL", parent, white, lp, lp + Vector3.up * Field.GoalH, r);
            MeshKit.Bar("PostR", parent, white, rp, rp + Vector3.up * Field.GoalH, r);
            MeshKit.Bar("Crossbar", parent, white, lp + Vector3.up * Field.GoalH, rp + Vector3.up * Field.GoalH, r);
            var netTex = NetTexture();
            var net = Mats.Transparent(new Color(1, 1, 1, 0.85f), netTex);
            float d = Field.GoalDepth * side;
            var back = MeshKit.Obj("NetBack", parent, MeshKit.Quad, net, new Vector3(0, Field.GoalH * 0.5f, z + d), new Vector3(Field.GoalHalfW * 2, Field.GoalH, 1), Quaternion.Euler(0, side > 0 ? 0 : 180, 0), false);
            MeshKit.Obj("NetTop", parent, MeshKit.Quad, net, new Vector3(0, Field.GoalH, z + d * 0.5f), new Vector3(Field.GoalHalfW * 2, Field.GoalDepth, 1), Quaternion.Euler(-90, 0, 0), false);
            MeshKit.Obj("NetL", parent, MeshKit.Quad, net, new Vector3(-Field.GoalHalfW, Field.GoalH * 0.5f, z + d * 0.5f), new Vector3(Field.GoalDepth, Field.GoalH, 1), Quaternion.Euler(0, -90, 0), false);
            MeshKit.Obj("NetR", parent, MeshKit.Quad, net, new Vector3(Field.GoalHalfW, Field.GoalH * 0.5f, z + d * 0.5f), new Vector3(Field.GoalDepth, Field.GoalH, 1), Quaternion.Euler(0, 90, 0), false);
            back.name = side > 0 ? "NetBackFar" : "NetBackNear";
        }

        /// <summary>Lights down + spot light for the K5 "Efsane Anı" (DESIGN §5.2).</summary>
        public void SetDramatic(bool on) { dimTarget = on ? 0.25f : 1f; }

        void Update()
        {
            dim = Mathf.MoveTowards(dim, dimTarget, Time.deltaTime * 1.5f);
            Sun.intensity = sunBaseIntensity * dim;
            float spot = (1f - dim) * 90f;
            foreach (var s in floodSpots) s.intensity = spot;
        }

        // ------------------------------------------------------------------ textures

        static Texture2D PitchTexture(float widthM, float lengthM, float runoff)
        {
            const float ppm = 28f; // pixels per metre
            int w = Mathf.RoundToInt(widthM * ppm), h = Mathf.RoundToInt(lengthM * ppm);
            var px = new Color32[w * h];
            var g1 = new Color32(92, 168, 74, 255);
            var g2 = new Color32(80, 152, 64, 255);
            var rnd = new System.Random(7);
            int stripes = 12;
            for (int y = 0; y < h; y++)
            {
                bool alt = ((int)(y / (float)h * stripes)) % 2 == 0;
                var bc = alt ? g1 : g2;
                for (int x = 0; x < w; x++)
                {
                    int n = rnd.Next(-6, 7);
                    px[y * w + x] = new Color32((byte)Mathf.Clamp(bc.r + n, 0, 255), (byte)Mathf.Clamp(bc.g + n, 0, 255), (byte)Mathf.Clamp(bc.b + n, 0, 255), 255);
                }
            }
            var line = new Color32(245, 248, 240, 255);
            float lw = 0.13f;
            float x0 = runoff, x1 = widthM - runoff, y0 = runoff, y1 = lengthM - runoff;
            float cx = widthM / 2, cy = lengthM / 2;
            void Rect(float ax, float ay, float bx, float by)
            {
                int ix0 = Mathf.RoundToInt(ax * ppm), ix1 = Mathf.RoundToInt(bx * ppm), iy0 = Mathf.RoundToInt(ay * ppm), iy1 = Mathf.RoundToInt(by * ppm);
                for (int y = Mathf.Max(0, iy0); y < Mathf.Min(h, iy1); y++)
                    for (int x = Mathf.Max(0, ix0); x < Mathf.Min(w, ix1); x++) px[y * w + x] = line;
            }
            void Outline(float ax, float ay, float bx, float by)
            {
                Rect(ax - lw / 2, ay - lw / 2, bx + lw / 2, ay + lw / 2);
                Rect(ax - lw / 2, by - lw / 2, bx + lw / 2, by + lw / 2);
                Rect(ax - lw / 2, ay - lw / 2, ax + lw / 2, by + lw / 2);
                Rect(bx - lw / 2, ay - lw / 2, bx + lw / 2, by + lw / 2);
            }
            void Ring(float rx, float ry, float rad, float from = 0, float to = 360)
            {
                int steps = Mathf.CeilToInt(rad * ppm * 7);
                for (int i = 0; i <= steps; i++)
                {
                    float a = Mathf.Lerp(from, to, i / (float)steps) * Mathf.Deg2Rad;
                    float px1 = rx + Mathf.Cos(a) * rad, py1 = ry + Mathf.Sin(a) * rad;
                    Rect(px1 - lw / 2, py1 - lw / 2, px1 + lw / 2, py1 + lw / 2);
                }
            }
            void Dot(float dx, float dy, float rad) => Rect(dx - rad, dy - rad, dx + rad, dy + rad);
            Outline(x0, y0, x1, y1);
            Rect(x0, cy - lw / 2, x1, cy + lw / 2);
            Ring(cx, cy, 4f);
            Dot(cx, cy, 0.18f);
            foreach (int s in new[] { -1, 1 })
            {
                float gy = s < 0 ? y0 : y1;
                float dir = -s;
                Outline(cx - 7f, Mathf.Min(gy, gy + dir * 7f), cx + 7f, Mathf.Max(gy, gy + dir * 7f));
                Outline(cx - 3.6f, Mathf.Min(gy, gy + dir * 2.4f), cx + 3.6f, Mathf.Max(gy, gy + dir * 2.4f));
                Dot(cx, gy + dir * 5.2f, 0.16f);
                Ring(cx, gy + dir * 5.2f, 3.6f, s < 0 ? 35 : 215, s < 0 ? 145 : 325);
                Ring(x0, gy, 0.6f, s < 0 ? 0 : 270, s < 0 ? 90 : 360);
                Ring(x1, gy, 0.6f, s < 0 ? 90 : 180, s < 0 ? 180 : 270);
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "PitchTex", anisoLevel = 8, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }

        static Texture2D NetTexture()
        {
            const int s = 64;
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    bool lineX = (x % 16) < 2, lineY = (y % 16) < 2;
                    px[y * s + x] = lineX || lineY ? new Color32(255, 255, 255, 230) : new Color32(255, 255, 255, 0);
                }
            var t = new Texture2D(s, s, TextureFormat.RGBA32, true) { name = "Net", wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
            t.SetPixels32(px);
            t.Apply(true, true);
            return t;
        }
    }

    /// <summary>
    /// Tiered stand with thousands of low-poly fans in ONE mesh (DESIGN §13 instancing idea, done as a single draw).
    /// The crowd shader paints them in team colours, makes them jump and can turn the whole stand into a giant flag (koreografi).
    /// </summary>
    public class CrowdStand : MonoBehaviour
    {
        Material mat;
        float jump, jumpTarget, choreo, choreoTarget;
        static readonly int ColorA = Shader.PropertyToID("_ColorA");
        static readonly int ColorB = Shader.PropertyToID("_ColorB");
        static readonly int FlagTex = Shader.PropertyToID("_FlagTex");
        static readonly int Jump = Shader.PropertyToID("_Jump");
        static readonly int Choreo = Shader.PropertyToID("_Choreo");
        public Vector3 FrontCenter { get; private set; }

        public static CrowdStand Build(Transform parent, string name, Vector3 frontCenter, float yaw, float width, int rows, int seed, bool roof = true, bool low = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = frontCenter;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var stand = go.AddComponent<CrowdStand>();
            stand.FrontCenter = frontCenter;
            float rowDepth = 1.05f, rowRise = low ? 0.55f : 0.62f;
            // concrete tiers (local -z = away from the pitch)
            var concrete = Mats.Lit(new Color(0.70f, 0.72f, 0.76f));
            var dark = Mats.Lit(new Color(0.30f, 0.32f, 0.38f));
            for (int r = 0; r < rows; r++)
            {
                MeshKit.Box("Tier" + r, go.transform, r % 2 == 0 ? concrete : dark,
                    new Vector3(0, (r + 1) * rowRise * 0.5f, -(r + 0.5f) * rowDepth),
                    new Vector3(width, (r + 1) * rowRise, rowDepth), false);
            }
            if (roof)
            {
                MeshKit.Box("BackWall", go.transform, dark, new Vector3(0, rows * rowRise * 0.5f + 1f, -rows * rowDepth - 0.3f), new Vector3(width + 2, rows * rowRise + 2f, 0.6f), true);
                MeshKit.Box("Roof", go.transform, Mats.Lit(new Color(0.13f, 0.17f, 0.30f)),
                    new Vector3(0, rows * rowRise + 3.4f, -rows * rowDepth * 0.55f), new Vector3(width + 2, 0.35f, rows * rowDepth * 0.9f), true);
            }

            var mesh = BuildCrowd(width, rows, rowDepth, rowRise, seed, low ? 0.55f : 0.85f);
            var crowdGo = new GameObject("Crowd");
            crowdGo.transform.SetParent(go.transform, false);
            crowdGo.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = crowdGo.AddComponent<MeshRenderer>();
            stand.mat = Mats.Crowd();
            mr.sharedMaterial = stand.mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            stand.SetColors(new Color(0.6f, 0.6f, 0.65f), Color.white, null);
            return stand;
        }

        static Mesh BuildCrowd(float width, int rows, float rowDepth, float rowRise, int seed, float density)
        {
            var rnd = new System.Random(seed);
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var cols = new List<Color>();
            var uv0 = new List<Vector2>();
            var uv1 = new List<Vector2>();
            var tris = new List<int>();
            float seat = 0.62f;
            int cols1 = Mathf.FloorToInt(width / seat);
            Color[] skins = { new Color(0.96f, 0.80f, 0.66f), new Color(0.85f, 0.64f, 0.47f), new Color(0.62f, 0.43f, 0.30f), new Color(0.42f, 0.29f, 0.20f) };
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols1; c++)
                {
                    if (rnd.NextDouble() > density) continue;
                    float x = -width / 2 + (c + 0.5f) * seat + (float)(rnd.NextDouble() - 0.5) * 0.12f;
                    float y = (r + 1) * rowRise;
                    float z = -(r + 0.55f) * rowDepth;
                    var gridUv = new Vector2((x + width / 2) / width, r / (float)Mathf.Max(1, rows - 1));
                    float slot = rnd.NextDouble() < 0.62 ? 0f : (rnd.NextDouble() < 0.7 ? 1f : 2f);
                    float phase = (float)rnd.NextDouble();
                    var neutral = Color.Lerp(new Color(0.15f, 0.15f, 0.2f), new Color(0.9f, 0.9f, 0.9f), (float)rnd.NextDouble());
                    AddBox(new Vector3(x, y + 0.42f, z), new Vector3(0.46f, 0.62f, 0.32f), neutral, gridUv, new Vector2(slot, phase));
                    AddBox(new Vector3(x, y + 0.92f, z), new Vector3(0.26f, 0.28f, 0.26f), skins[rnd.Next(skins.Length)], gridUv, new Vector2(3f, phase));
                }

            void AddBox(Vector3 c, Vector3 s, Color col, Vector2 a, Vector2 b)
            {
                Vector3 h = s * 0.5f;
                Vector3[] n = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right, Vector3.up };
                foreach (var nn in n)
                {
                    Vector3 u = Mathf.Abs(nn.y) > 0.5f ? Vector3.right : Vector3.up;
                    Vector3 v = Vector3.Cross(nn, u);
                    Vector3 fc = c + Vector3.Scale(nn, h);
                    Vector3 du = Vector3.Scale(u, h), dv = Vector3.Scale(v, h);
                    int i = verts.Count;
                    verts.Add(fc - du - dv); verts.Add(fc - du + dv); verts.Add(fc + du + dv); verts.Add(fc + du - dv);
                    for (int k = 0; k < 4; k++) { norms.Add(nn); cols.Add(col); uv0.Add(a); uv1.Add(b); }
                    tris.Add(i); tris.Add(i + 1); tris.Add(i + 2); tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
                }
            }

            var m = new Mesh { name = "Crowd", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.SetVertices(verts);
            m.SetNormals(norms);
            m.SetColors(cols);
            m.SetUVs(0, uv0);
            m.SetUVs(1, uv1);
            m.SetTriangles(tris, 0);
            // fix winding per face (outward normals)
            var t = m.triangles;
            var vs = m.vertices;
            var ns = m.normals;
            for (int i = 0; i < t.Length; i += 3)
            {
                var fn = Vector3.Cross(vs[t[i + 1]] - vs[t[i]], vs[t[i + 2]] - vs[t[i]]);
                if (Vector3.Dot(fn, ns[t[i]]) < 0) { int tmp = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = tmp; }
            }
            m.triangles = t;
            m.RecalculateBounds();
            var b2 = m.bounds; b2.Expand(1f); m.bounds = b2;
            return m;
        }

        public void SetColors(Color a, Color b, Texture flag)
        {
            mat.SetColor(ColorA, a);
            mat.SetColor(ColorB, b);
            if (flag != null) mat.SetTexture(FlagTex, flag);
        }

        /// <summary>0 = calm, 1 = going wild.</summary>
        public void SetExcitement(float v) { jumpTarget = Mathf.Clamp01(v); }
        public void Burst(float seconds) { burstUntil = Time.time + seconds; }
        public void ShowChoreo(float seconds) { choreoUntil = Time.time + seconds; }
        float burstUntil, choreoUntil;

        void Update()
        {
            float target = Time.time < burstUntil ? 1f : jumpTarget;
            jump = Mathf.MoveTowards(jump, target, Time.deltaTime * 2.5f);
            choreoTarget = Time.time < choreoUntil ? 1f : 0f;
            choreo = Mathf.MoveTowards(choreo, choreoTarget, Time.deltaTime * 2.5f);
            mat.SetFloat(Jump, jump);
            mat.SetFloat(Choreo, choreo);
        }
    }

    /// <summary>Pitch-side LED board: a world-space text strip (names of the top gifters rotate here).</summary>
    public class LedBoard : MonoBehaviour
    {
        UnityEngine.UI.Text text;
        UnityEngine.UI.Image bg;
        string[] lines = { "DÜNYA TARAFTAR KUPASI" };
        int idx;
        float next;
        public float RotateSec = 5f;

        public static LedBoard Build(Transform parent, Vector3 pos, float yaw, float length)
        {
            var go = new GameObject("LED");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(-35f, yaw, 0);
            MeshKit.Box("Frame", go.transform, Mats.Lit(new Color(0.06f, 0.08f, 0.16f)), new Vector3(0, 0, 0.12f), new Vector3(length, 1.3f, 0.18f), false);
            var led = go.AddComponent<LedBoard>();
            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(go.transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)canvasGo.transform;
            rt.sizeDelta = new Vector2(length * 100f, 120f);
            rt.localScale = Vector3.one * 0.01f;
            rt.localPosition = new Vector3(0, 0, 0.23f); // in front of the frame, on the pitch side
            rt.localRotation = Quaternion.Euler(0, 180, 0);
            led.bg = Ui.Image(rt, "Bg", Ui.Rounded, new Color(0.03f, 0.05f, 0.12f));
            Ui.Stretch(led.bg.rectTransform);
            led.text = Ui.Text(rt, "T", "", 96, new Color(1f, 0.85f, 0.2f), TextAnchor.MiddleCenter, Ui.FontBlack);
            Ui.Stretch(led.text.rectTransform);
            return led;
        }

        public void SetLines(IList<string> l)
        {
            if (l == null || l.Count == 0) return;
            lines = new string[l.Count];
            l.CopyTo(lines, 0);
            if (idx >= lines.Length) idx = 0;
        }

        void Update()
        {
            if (Time.time < next || lines.Length == 0) return;
            next = Time.time + RotateSec;
            idx = (idx + 1) % lines.Length;
            text.text = lines[idx];
        }
    }
}
