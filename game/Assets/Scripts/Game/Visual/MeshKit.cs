using System.Collections.Generic;
using UnityEngine;

namespace DTK.Game
{
    /// <summary>Procedural meshes and GameObject helpers. Everything in the stadium is built from these.</summary>
    public static class MeshKit
    {
        static Mesh cube, sphere, capsule, cylinder, quad;

        static Mesh Primitive(PrimitiveType t, ref Mesh cache)
        {
            if (cache != null) return cache;
            var go = GameObject.CreatePrimitive(t);
            cache = go.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(go);
            return cache;
        }

        public static Mesh Cube => Primitive(PrimitiveType.Cube, ref cube);
        public static Mesh Sphere => Primitive(PrimitiveType.Sphere, ref sphere);
        public static Mesh Capsule => Primitive(PrimitiveType.Capsule, ref capsule);
        public static Mesh Cylinder => Primitive(PrimitiveType.Cylinder, ref cylinder);
        public static Mesh Quad => Primitive(PrimitiveType.Quad, ref quad);

        public static GameObject Obj(string name, Transform parent, Mesh mesh, Material mat, Vector3 pos, Vector3 scale, Quaternion? rot = null, bool shadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot ?? Quaternion.identity;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        public static GameObject Box(string name, Transform parent, Material mat, Vector3 center, Vector3 size, bool shadows = true)
            => Obj(name, parent, Cube, mat, center, size, null, shadows);

        /// <summary>A cylinder between two points (posts, crossbar, floodlight masts).</summary>
        public static GameObject Bar(string name, Transform parent, Material mat, Vector3 a, Vector3 b, float radius)
        {
            Vector3 mid = (a + b) * 0.5f;
            float len = Vector3.Distance(a, b);
            var rot = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
            return Obj(name, parent, Cylinder, mat, mid, new Vector3(radius * 2, len * 0.5f, radius * 2), rot);
        }

        /// <summary>Classic football: truncated icosahedron, 12 dark pentagons + 20 white hexagons, flat shaded.</summary>
        public static Mesh SoccerBall(float radius = 0.5f)
        {
            float phi = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var ico = new List<Vector3>
            {
                new Vector3(-1, phi, 0), new Vector3(1, phi, 0), new Vector3(-1, -phi, 0), new Vector3(1, -phi, 0),
                new Vector3(0, -1, phi), new Vector3(0, 1, phi), new Vector3(0, -1, -phi), new Vector3(0, 1, -phi),
                new Vector3(phi, 0, -1), new Vector3(phi, 0, 1), new Vector3(-phi, 0, -1), new Vector3(-phi, 0, 1),
            };
            int[] faces =
            {
                0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
            };
            // edge points at 1/3 and 2/3
            var edgePoint = new Dictionary<long, Vector3>();
            Vector3 P(int from, int to)
            {
                long key = from * 100L + to;
                if (!edgePoint.TryGetValue(key, out var p)) { p = Vector3.Lerp(ico[from], ico[to], 1f / 3f); edgePoint[key] = p; }
                return p;
            }
            var polys = new List<(List<Vector3> pts, bool dark)>();
            // pentagons around each icosahedron vertex
            for (int v = 0; v < ico.Count; v++)
            {
                var pts = new List<Vector3>();
                for (int f = 0; f < faces.Length; f += 3)
                    for (int k = 0; k < 3; k++)
                        if (faces[f + k] == v)
                        {
                            int a = faces[f + (k + 1) % 3];
                            var p = P(v, a);
                            if (!pts.Exists(q => (q - p).sqrMagnitude < 1e-6f)) pts.Add(p);
                        }
                polys.Add((pts, true));
            }
            // hexagons for each face
            for (int f = 0; f < faces.Length; f += 3)
            {
                int a = faces[f], b = faces[f + 1], c = faces[f + 2];
                polys.Add((new List<Vector3> { P(a, b), P(b, a), P(b, c), P(c, b), P(c, a), P(a, c) }, false));
            }
            var verts = new List<Vector3>();
            var cols = new List<Color>();
            var norms = new List<Vector3>();
            var tris = new List<int>();
            foreach (var (pts, dark) in polys)
            {
                Vector3 center = Vector3.zero;
                foreach (var p in pts) center += p;
                center /= pts.Count;
                Vector3 n = center.normalized;
                // order around the normal
                Vector3 u = (pts[0] - center).normalized, w = Vector3.Cross(n, u);
                pts.Sort((p1, p2) => Mathf.Atan2(Vector3.Dot(p1 - center, w), Vector3.Dot(p1 - center, u))
                    .CompareTo(Mathf.Atan2(Vector3.Dot(p2 - center, w), Vector3.Dot(p2 - center, u))));
                int baseIdx = verts.Count;
                Color col = dark ? new Color(0.08f, 0.08f, 0.1f) : new Color(0.97f, 0.97f, 0.97f);
                verts.Add(center.normalized * radius); norms.Add(n); cols.Add(col);
                foreach (var p in pts) { verts.Add(p.normalized * radius); norms.Add(n); cols.Add(col); }
                for (int i = 0; i < pts.Count; i++)
                {
                    tris.Add(baseIdx);
                    tris.Add(baseIdx + 1 + (i + 1) % pts.Count);
                    tris.Add(baseIdx + 1 + i);
                }
            }
            var mesh = new Mesh { name = "SoccerBall" };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);
            // make sure winding faces outwards
            var t = mesh.triangles;
            var vs = mesh.vertices;
            if (Vector3.Dot(Vector3.Cross(vs[t[1]] - vs[t[0]], vs[t[2]] - vs[t[0]]), vs[t[0]]) < 0)
            {
                for (int i = 0; i < t.Length; i += 3) { int tmp = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = tmp; }
                mesh.triangles = t;
            }
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Surface of revolution from a 2D profile (x = radius, y = height). Used for the trophy.</summary>
        public static Mesh Lathe(Vector2[] profile, int segments = 32)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int s = 0; s <= segments; s++)
            {
                float a = s / (float)segments * Mathf.PI * 2;
                float c = Mathf.Cos(a), sn = Mathf.Sin(a);
                foreach (var p in profile) verts.Add(new Vector3(p.x * c, p.y, p.x * sn));
            }
            int rows = profile.Length;
            for (int s = 0; s < segments; s++)
                for (int r = 0; r < rows - 1; r++)
                {
                    int i0 = s * rows + r, i1 = (s + 1) * rows + r;
                    tris.Add(i0); tris.Add(i0 + 1); tris.Add(i1);
                    tris.Add(i1); tris.Add(i0 + 1); tris.Add(i1 + 1);
                }
            var m = new Mesh { name = "Lathe" };
            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        public static Mesh Trophy()
        {
            return Lathe(new[]
            {
                new Vector2(0.0f, 0f), new Vector2(0.55f, 0f), new Vector2(0.55f, 0.18f), new Vector2(0.42f, 0.22f),
                new Vector2(0.38f, 0.40f), new Vector2(0.12f, 0.48f), new Vector2(0.09f, 0.85f), new Vector2(0.16f, 1.0f),
                new Vector2(0.42f, 1.15f), new Vector2(0.55f, 1.45f), new Vector2(0.60f, 1.80f), new Vector2(0.62f, 1.95f),
                new Vector2(0.56f, 1.97f), new Vector2(0.50f, 1.80f), new Vector2(0.0f, 1.70f),
            }, 40);
        }
    }
}
