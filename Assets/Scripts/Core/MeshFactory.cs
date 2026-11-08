using System.Collections.Generic;
using UnityEngine;

namespace SkiGame
{
    /// <summary>
    /// Procedural meshes that Unity has no primitive for: a cone (tree foliage) and a
    /// wedge (ramps). Unit-sized and cached; scale them with the transform.
    /// </summary>
    public static class MeshFactory
    {
        private static Mesh cone;
        private static Mesh wedge;

        /// <summary>Cone: base radius 1 at y = 0, apex at y = 1.</summary>
        public static Mesh UnitCone
        {
            get
            {
                if (cone != null) return cone;
                var b = new Builder();
                const int segments = 10;
                Vector3 apex = Vector3.up;
                for (int i = 0; i < segments; i++)
                {
                    float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
                    var p0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                    var p1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                    Vector3 mid = (p0 + p1) * 0.5f;
                    b.Tri(p0, p1, apex, mid.normalized + Vector3.up * 0.6f);
                    b.Tri(Vector3.zero, p0, p1, Vector3.down);
                }
                cone = b.ToMesh("UnitCone");
                return cone;
            }
        }

        /// <summary>
        /// Wedge (ramp): x from -0.5..0.5, floor at y = 0 from z = 0..1, top surface rises
        /// from y = 0 at z = 0 to y = 1 at z = 1, vertical back face at z = 1.
        /// </summary>
        public static Mesh UnitWedge
        {
            get
            {
                if (wedge != null) return wedge;
                var b = new Builder();
                var bl0 = new Vector3(-0.5f, 0f, 0f); var br0 = new Vector3(0.5f, 0f, 0f);
                var bl1 = new Vector3(-0.5f, 0f, 1f); var br1 = new Vector3(0.5f, 0f, 1f);
                var tl1 = new Vector3(-0.5f, 1f, 1f); var tr1 = new Vector3(0.5f, 1f, 1f);
                b.Quad(bl0, br0, tr1, tl1, new Vector3(0f, 1f, -1f)); // sloped top
                b.Quad(bl1, br1, tr1, tl1, Vector3.forward);            // back
                b.Quad(bl0, br0, br1, bl1, Vector3.down);               // bottom
                b.Tri(bl0, bl1, tl1, Vector3.left);                     // left side
                b.Tri(br0, br1, tr1, Vector3.right);                    // right side
                wedge = b.ToMesh("UnitWedge");
                return wedge;
            }
        }

        /// <summary>
        /// Alpine low-poly mountain: base radius ~1 at y = 0, main summit at y = 1.
        /// Rings of vertices whose radius follows several angular "ridge" waves, with a
        /// main summit and secondary shoulders, flat shaded so the facets catch the light.
        /// </summary>
        public static Mesh CreateMountain(int seed)
        {
            var rng = new System.Random(seed);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            const int segments = 20;
            float[] ringHeight = { 0f, 0.18f, 0.38f, 0.58f, 0.76f, 0.9f };
            float[] ringRadius = { 1f, 0.8f, 0.58f, 0.38f, 0.21f, 0.09f };
            int rings = ringHeight.Length;
            float p1 = R(0f, 6.3f), p2 = R(0f, 6.3f), p3 = R(0f, 6.3f);
            int ridges = rng.Next(3, 6);
            var summitOffset = new Vector3(R(-0.12f, 0.12f), 0f, R(-0.12f, 0.12f));
            var v = new Vector3[rings, segments];
            for (int r = 0; r < rings; r++)
            {
                float t = r / (float)(rings - 1);
                for (int i = 0; i < segments; i++)
                {
                    float a = i * Mathf.PI * 2f / segments;
                    // Ridges: sharp radial spurs that reach further out than the valleys between them.
                    float ridge = Mathf.Pow(Mathf.Abs(Mathf.Sin(a * ridges * 0.5f + p1)), 3f);
                    float wobble = 0.16f * Mathf.Sin(a * 3f + p2) + 0.1f * Mathf.Sin(a * 7f + p3);
                    float radius = ringRadius[r] * (0.82f + 0.38f * ridge + wobble) * R(0.93f, 1.07f);
                    float y = ringHeight[r] + R(-0.03f, 0.03f) + ridge * 0.06f * (1f - t);
                    var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    v[r, i] = dir * Mathf.Max(0.02f, radius) + Vector3.up * y + summitOffset * t;
                }
            }
            var peak = new Vector3(summitOffset.x + R(-0.03f, 0.03f), 1f, summitOffset.z + R(-0.03f, 0.03f));
            var b = new Builder();
            for (int r = 0; r < rings - 1; r++)
                for (int i = 0; i < segments; i++)
                {
                    int j = (i + 1) % segments;
                    Vector3 c = (v[r, i] + v[r, j] + v[r + 1, i] + v[r + 1, j]) * 0.25f;
                    Vector3 outward = new Vector3(c.x, 0f, c.z).normalized + Vector3.up * 0.4f;
                    b.Tri(v[r, i], v[r, j], v[r + 1, j], outward);
                    b.Tri(v[r, i], v[r + 1, j], v[r + 1, i], outward);
                }
            for (int i = 0; i < segments; i++)
            {
                int j = (i + 1) % segments;
                Vector3 c = (v[rings - 1, i] + v[rings - 1, j]) * 0.5f;
                b.Tri(v[rings - 1, i], v[rings - 1, j], peak, new Vector3(c.x, 0f, c.z).normalized + Vector3.up);
            }
            return b.ToMesh("Mountain_" + seed);
        }

        private static readonly Dictionary<int, Mesh> Boulders = new Dictionary<int, Mesh>();

        /// <summary>
        /// Chunky low-poly boulder (radius ~1, flat bottom at y = -0.25), flat shaded.
        /// A handful of variants are cached by seed.
        /// </summary>
        public static Mesh Boulder(int seed)
        {
            seed = Mathf.Abs(seed) % 6;
            if (Boulders.TryGetValue(seed, out Mesh cached) && cached != null) return cached;
            var rng = new System.Random(500 + seed * 31);
            const int rings = 5, sides = 9;
            var v = new Vector3[rings + 1, sides];
            for (int r = 0; r <= rings; r++)
            {
                float lat = Mathf.Lerp(-0.25f, 1f, r / (float)rings) * Mathf.PI * 0.5f;
                for (int s = 0; s < sides; s++)
                {
                    float lon = s * Mathf.PI * 2f / sides + (float)rng.NextDouble() * 0.3f;
                    float rad = 0.78f + (float)rng.NextDouble() * 0.36f;
                    var p = new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon)) * rad;
                    if (r == rings) p = new Vector3(p.x * 0.25f, p.y, p.z * 0.25f);
                    v[r, s] = p;
                }
            }
            var b = new Builder();
            for (int r = 0; r < rings; r++)
                for (int s = 0; s < sides; s++)
                {
                    int s2 = (s + 1) % sides;
                    Vector3 c = (v[r, s] + v[r, s2] + v[r + 1, s] + v[r + 1, s2]) * 0.25f;
                    b.Tri(v[r, s], v[r, s2], v[r + 1, s2], c);
                    b.Tri(v[r, s], v[r + 1, s2], v[r + 1, s], c);
                }
            // Flat bottom cap.
            Vector3 bottom = Vector3.zero;
            for (int s = 0; s < sides; s++) bottom += v[0, s];
            bottom /= sides;
            for (int s = 0; s < sides; s++) b.Tri(bottom, v[0, s], v[0, (s + 1) % sides], Vector3.down);
            Mesh m = b.ToMesh("Boulder_" + seed);
            Boulders[seed] = m;
            return m;
        }

        /// <summary>Tiny flat-shaded mesh builder; orders each triangle so it faces "outward".</summary>
        private class Builder
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<int> triangles = new List<int>();

            public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
            {
                // Unity front faces: cross(b - a, c - a) points toward the viewer.
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f) { Vector3 t = b; b = c; c = t; }
                int i = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2);
            }

            /// <summary>Quad given as 4 corners in loop order.</summary>
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward)
            {
                Tri(a, b, c, outward);
                Tri(a, c, d, outward);
            }

            public Mesh ToMesh(string name)
            {
                var m = new Mesh { name = name };
                m.SetVertices(vertices);
                m.SetTriangles(triangles, 0);
                m.RecalculateNormals();
                m.RecalculateBounds();
                return m;
            }
        }
    }
}
