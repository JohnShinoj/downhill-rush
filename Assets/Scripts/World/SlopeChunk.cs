using System.Collections.Generic;
using UnityEngine;

namespace SkiGame
{
    /// <summary>
    /// One segment of the slope: a mesh (with MeshCollider for the ground raycast),
    /// edge marker poles and decorative trees. Spawned content is parented to this
    /// object so it disappears when the chunk is despawned.
    /// </summary>
    public class SlopeChunk : MonoBehaviour
    {
        public int Index { get; private set; }
        public float StartZ { get; private set; }
        public float EndZ { get; private set; }

        private Mesh mesh;

        public void Build(SlopeGenerator gen, int index, float startZ, float endZ)
        {
            Index = index;
            StartZ = startZ;
            EndZ = endZ;
            gameObject.layer = GameLayers.Ground;
            SlopeSettings s = gen.Settings;

            BuildSurface(gen, s);

            // All decoration goes under one temporary parent, then gets merged into a few
            // big meshes (one per material). This cuts hundreds of draw calls per chunk to ~10.
            decorRoot = new GameObject("Decor").transform;
            decorRoot.SetParent(transform, false);
            BuildEdgeMarkers(gen, s);
            BuildDecorTrees(gen, s);
            BuildDecorRocks(gen, s);
            MergeDecor();
        }

        private Transform decorRoot;
        private readonly List<Mesh> mergedMeshes = new List<Mesh>();

        /// <summary>Combines every decor MeshFilter into one mesh per (material, shadow mode).</summary>
        private void MergeDecor()
        {
            var groups = new Dictionary<(Material, UnityEngine.Rendering.ShadowCastingMode), List<CombineInstance>>();
            foreach (MeshFilter mf in decorRoot.GetComponentsInChildren<MeshFilter>())
            {
                var mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || mf.sharedMesh == null) continue;
                var key = (mr.sharedMaterial, mr.shadowCastingMode);
                if (!groups.TryGetValue(key, out List<CombineInstance> list)) groups[key] = list = new List<CombineInstance>();
                // Chunk transform is identity at the world origin, so world matrix == chunk-local matrix.
                list.Add(new CombineInstance { mesh = mf.sharedMesh, transform = mf.transform.localToWorldMatrix });
            }
            foreach (var g in groups)
            {
                var mesh = new Mesh { name = "Decor " + Index, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.CombineMeshes(g.Value.ToArray(), true, true);
                mergedMeshes.Add(mesh);
                var go = new GameObject("Decor_" + (g.Key.Item1 != null ? g.Key.Item1.name : "mat"), typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(transform, false);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.GetComponent<MeshRenderer>();
                r.sharedMaterial = g.Key.Item1;
                r.shadowCastingMode = g.Key.Item2;
            }
            Destroy(decorRoot.gameObject);
            decorRoot = null;
        }

        private void BuildSurface(SlopeGenerator gen, SlopeSettings s)
        {
            List<float> offsets = BuildLateralOffsets(s);
            int cols = offsets.Count;
            int rows = Mathf.Max(2, Mathf.CeilToInt((EndZ - StartZ) / s.rowSpacing) + 1);

            var vertices = new Vector3[rows * cols];
            var uvs = new Vector2[rows * cols];
            float tile = Mathf.Max(0.5f, s.snowTextureTile);
            for (int r = 0; r < rows; r++)
            {
                float z = Mathf.Lerp(StartZ, EndZ, r / (float)(rows - 1));
                float cx = gen.GetCenterX(z);
                float baseY = gen.GetBaseHeight(z);
                for (int c = 0; c < cols; c++)
                {
                    float u = offsets[c];
                    float x = cx + u;
                    int i = r * cols + c;
                    vertices[i] = new Vector3(x, baseY + gen.GetBankHeight(u), z);
                    uvs[i] = new Vector2(x / tile, z / tile);
                }
            }

            var triangles = new int[(rows - 1) * (cols - 1) * 6];
            int t = 0;
            for (int r = 0; r < rows - 1; r++)
            {
                for (int c = 0; c < cols - 1; c++)
                {
                    int v0 = r * cols + c, v1 = v0 + 1, v2 = v0 + cols, v3 = v2 + 1;
                    // Clockwise seen from above = faces up.
                    triangles[t++] = v0; triangles[t++] = v2; triangles[t++] = v1;
                    triangles[t++] = v1; triangles[t++] = v2; triangles[t++] = v3;
                }
            }

            mesh = new Mesh { name = "SlopeChunk " + Index };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = gen.SnowMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            gameObject.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        /// <summary>Column positions across the slope, denser on the banks for a smooth curve.</summary>
        private static List<float> BuildLateralOffsets(SlopeSettings s)
        {
            float hw = s.pathHalfWidth, bw = s.bankWidth, ext = hw + bw + Mathf.Max(1f, s.outerWidth);
            var list = new List<float>();
            AddSegment(list, -ext, -(hw + bw), 3);
            AddSegment(list, -(hw + bw), -hw, s.bankSegments);
            AddSegment(list, -hw, hw, s.pathSegments);
            AddSegment(list, hw, hw + bw, s.bankSegments);
            AddSegment(list, hw + bw, ext, 3);
            list.Add(ext);
            return list;
        }

        private static void AddSegment(List<float> list, float a, float b, int steps)
        {
            steps = Mathf.Max(1, steps);
            for (int i = 0; i < steps; i++) list.Add(Mathf.Lerp(a, b, i / (float)steps));
        }

        private void BuildEdgeMarkers(SlopeGenerator gen, SlopeSettings s)
        {
            if (s.edgeMarkerSpacing <= 0f) return;
            Material a = MaterialFactory.Lit(s.edgeMarkerColorA, 0.4f);
            Material b = MaterialFactory.Lit(s.edgeMarkerColorB, 0.4f);
            float first = Mathf.Ceil(StartZ / s.edgeMarkerSpacing) * s.edgeMarkerSpacing;
            for (float z = first; z < EndZ; z += s.edgeMarkerSpacing)
            {
                int n = Mathf.RoundToInt(z / s.edgeMarkerSpacing);
                Material m = (n & 1) == 0 ? a : b;
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 p = gen.SurfacePoint(side * (s.pathHalfWidth + 0.4f), z);
                    PrimitiveBuilder.Create(PrimitiveType.Cylinder, "EdgeMarker", decorRoot,
                        p + Vector3.up * 0.6f, new Vector3(0.08f, 0.6f, 0.08f), m);
                    PrimitiveBuilder.Create(PrimitiveType.Cube, "Flag", decorRoot,
                        p + new Vector3(-side * 0.18f, 1.05f, 0f), new Vector3(0.34f, 0.22f, 0.02f), m);
                }
            }
        }

        private void BuildDecorTrees(SlopeGenerator gen, SlopeSettings s)
        {
            if (s.decorTreesPerChunkSide <= 0) return;
            var rng = new System.Random(gen.DecorSeed + Index * 7919);
            float inner = s.pathHalfWidth + s.bankWidth + 1f;
            float outer = s.pathHalfWidth + s.bankWidth + Mathf.Max(2f, s.outerWidth) - 1f;
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < s.decorTreesPerChunkSide; i++)
                {
                    float z = Mathf.Lerp(StartZ, EndZ, (float)rng.NextDouble());
                    // Bias toward the path so the forest edge is dense.
                    float t = (float)rng.NextDouble();
                    float lateral = side * Mathf.Lerp(inner, outer, t * t);
                    float scale = 0.8f + (float)rng.NextDouble() * 0.7f;
                    GameObject tree = ObstacleFactory.BuildTree(new Vector3(scale, scale * (0.9f + (float)rng.NextDouble() * 0.4f), scale),
                        s.decorTreeColor, false, rng.Next(4));
                    tree.transform.SetPositionAndRotation(gen.SurfacePoint(lateral, z, -0.15f),
                        Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));
                    tree.transform.SetParent(decorRoot, true);
                }
            }
        }

        private void BuildDecorRocks(SlopeGenerator gen, SlopeSettings s)
        {
            if (s.decorRocksPerChunkSide <= 0) return;
            var rng = new System.Random(gen.DecorSeed ^ (Index * 104729 + 7));
            float inner = s.pathHalfWidth + s.bankWidth * 0.95f;
            float outer = s.pathHalfWidth + s.bankWidth + Mathf.Max(2f, s.outerWidth) - 2f;
            Color rock = new Color(0.36f, 0.37f, 0.42f);
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < s.decorRocksPerChunkSide; i++)
                {
                    float z = Mathf.Lerp(StartZ, EndZ, (float)rng.NextDouble());
                    float lateral = side * Mathf.Lerp(inner, outer, (float)rng.NextDouble());
                    float sc = 0.5f + (float)rng.NextDouble() * 1.3f;
                    GameObject r = ObstacleFactory.BuildRock(new Vector3(sc, sc * 0.8f, sc), rock, false, rng.Next(6));
                    r.transform.SetPositionAndRotation(gen.SurfacePoint(lateral, z, -0.2f * sc),
                        Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));
                    r.transform.SetParent(decorRoot, true);
                }
        }

        private void OnDestroy()
        {
            foreach (Mesh m in mergedMeshes) if (m != null) Destroy(m);
            if (mesh != null) Destroy(mesh);
        }
    }
}
