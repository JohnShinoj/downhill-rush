using UnityEngine;

namespace SkiGame
{
    /// <summary>
    /// Builds every spawnable object from primitives (or from a prefab in a SpawnEntry).
    /// Objects are built at the origin, base at y = 0, facing +Z; the spawner places them.
    /// Colour code: Swerve = red, Jumpable = orange, Ramp = blue, Coins = gold.
    /// </summary>
    public static class ObstacleFactory
    {
        private static readonly Color MarkerRed = new Color(0.95f, 0.1f, 0.1f);
        private static readonly Color MarkerOrange = new Color(1f, 0.6f, 0.05f);
        private static readonly Color TrunkBrown = new Color(0.4f, 0.26f, 0.15f);

        /// <summary>
        /// Creates the object for a spawn entry.
        /// dims: Tree/Rock = size multiplier; Wall/Log = (width m, height mult, depth mult);
        ///       Gap = (width m, -, length mult); Ramp = (width m, height m, length m).
        /// </summary>
        public static GameObject Create(SpawnEntry e, Vector3 dims)
        {
            GameObject go;
            if (e.prefab != null)
            {
                go = Object.Instantiate(e.prefab);
                go.name = e.name;
            }
            else
            {
                switch (e.shape)
                {
                    case SpawnShape.Rock: go = BuildRock(dims, e.color, true, Random.Range(0, 6)); break;
                    case SpawnShape.Wall: go = BuildWall(dims.x, dims.y, e.color); break;
                    case SpawnShape.Log: go = BuildLog(dims.x, dims.y, e.color); break;
                    case SpawnShape.Gap: go = BuildGap(dims.x, dims.z, e.color); break;
                    case SpawnShape.Ramp: go = BuildRamp(dims.x, dims.y, dims.z, e.color); break;
                    default: go = BuildTree(dims, e.color, true, Random.Range(0, 4)); break;
                }
                go.name = e.name;
            }
            SetupGameplay(go, e, dims);
            return go;
        }

        /// <summary>Adds gameplay components, layers and collider flags.</summary>
        private static void SetupGameplay(GameObject go, SpawnEntry e, Vector3 dims)
        {
            if (e.kind == SpawnKind.Ramp)
            {
                if (!go.TryGetComponent(out Ramp ramp)) ramp = go.AddComponent<Ramp>();
                ramp.Configure(e.rampLaunchVelocity, dims.z, dims.y);
                PrimitiveBuilder.SetLayerRecursively(go, GameLayers.Ground);
                foreach (Collider c in go.GetComponentsInChildren<Collider>()) c.isTrigger = false;
                if (go.GetComponentInChildren<Collider>() == null) AddBoundsCollider(go, false);
            }
            else
            {
                if (!go.TryGetComponent(out Obstacle obstacle)) obstacle = go.AddComponent<Obstacle>();
                obstacle.Configure(e.kind, e.name);
                PrimitiveBuilder.SetLayerRecursively(go, GameLayers.Interactable);
                if (go.GetComponentInChildren<Collider>() == null) AddBoundsCollider(go, true);
                foreach (Collider c in go.GetComponentsInChildren<Collider>()) c.isTrigger = true;
            }
        }

        private static void AddBoundsCollider(GameObject go, bool trigger)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            var box = go.AddComponent<BoxCollider>();
            box.center = go.transform.InverseTransformPoint(b.center);
            box.size = b.size;
            box.isTrigger = trigger;
        }

        // ------------------------------------------------------------------ shapes
        /// <summary>Pine tree (trunk + two cone tiers + snow cap). interactive = adds a red marker ring + trigger.</summary>
        /// <summary>
        /// Snowy pine: trunk + three foliage tiers, each with a snow cap on its upper part.
        /// variant (0..3) slightly changes the shade so a forest doesn't look copy-pasted.
        /// interactive = adds a red marker ring + trigger collider (obstacle version).
        /// </summary>
        public static GameObject BuildTree(Vector3 s, Color foliage, bool interactive, int variant = 0)
        {
            var root = new GameObject("Tree");
            Transform t = root.transform;
            float h = 4.4f * s.y, r = 1.3f * s.x, baseY = 0.75f * s.y;
            float shade = 0.85f + 0.1f * (variant & 3);
            Color leaf = new Color(foliage.r * shade, foliage.g * shade, foliage.b * (shade + 0.05f));
            Material leafMat = MaterialFactory.Lit(leaf, 0.08f);
            Material leafLight = MaterialFactory.Lit(leaf * 1.12f, 0.08f);
            Material snow = MaterialFactory.Lit(new Color(0.96f, 0.98f, 1f), 0.35f);

            PrimitiveBuilder.Create(PrimitiveType.Cylinder, "Trunk", t, new Vector3(0f, 0.45f * s.y, 0f),
                new Vector3(0.3f * s.x, 0.65f * s.y, 0.3f * s.x), MaterialFactory.Lit(TrunkBrown, 0.1f));

            float[] tierBase = { 0f, 0.27f, 0.5f };
            float[] tierHeight = { 0.5f, 0.42f, 0.42f };
            float[] tierRadius = { 1f, 0.76f, 0.5f };
            for (int i = 0; i < 3; i++)
            {
                float y0 = baseY + h * tierBase[i];
                float th = h * tierHeight[i];
                float tr = r * tierRadius[i];
                PrimitiveBuilder.CreateMesh(MeshFactory.UnitCone, "Foliage" + i, t, new Vector3(0f, y0, 0f),
                    Quaternion.Euler(0f, i * 17f, 0f), new Vector3(tr, th, tr), (i & 1) == 0 ? leafMat : leafLight);
                // Snow on the upper 40% of each tier (slightly wider than the foliage there).
                PrimitiveBuilder.CreateMesh(MeshFactory.UnitCone, "Snow" + i, t, new Vector3(0f, y0 + th * 0.6f, 0f),
                    Quaternion.Euler(0f, i * 17f + 9f, 0f), new Vector3(tr * 0.43f, th * 0.42f, tr * 0.43f), snow);
            }

            if (interactive)
            {
                PrimitiveBuilder.Create(PrimitiveType.Cylinder, "RedMarker", t, new Vector3(0f, 0.35f, 0f),
                    new Vector3(0.42f * s.x, 0.08f, 0.42f * s.x), MaterialFactory.Emissive(MarkerRed, MarkerRed * 0.6f, 0.4f));
                var cap = root.AddComponent<CapsuleCollider>();
                cap.radius = 0.6f * s.x;
                cap.height = h;
                cap.center = new Vector3(0f, h * 0.5f, 0f);
                cap.isTrigger = true;
            }
            return root;
        }

        /// <summary>Low-poly boulder with a snow cap. interactive = obstacle (trigger collider).</summary>
        public static GameObject BuildRock(Vector3 s, Color color, bool interactive = true, int variant = 0)
        {
            var root = new GameObject("Rock");
            Transform t = root.transform;
            Material m = MaterialFactory.Lit(color, 0.15f);
            Material snow = MaterialFactory.Lit(new Color(0.96f, 0.98f, 1f), 0.35f);
            PrimitiveBuilder.CreateMesh(MeshFactory.Boulder(variant), "Main", t, new Vector3(0f, 0.3f * s.y, 0f),
                Quaternion.Euler(0f, variant * 47f, 0f), new Vector3(0.95f * s.x, 0.75f * s.y, 0.8f * s.z), m);
            PrimitiveBuilder.CreateMesh(MeshFactory.Boulder(variant + 1), "Side", t, new Vector3(0.6f * s.x, 0.15f * s.y, -0.35f * s.z),
                Quaternion.Euler(0f, variant * 31f + 60f, 0f), new Vector3(0.5f * s.x, 0.45f * s.y, 0.45f * s.z), m);
            PrimitiveBuilder.CreateMesh(MeshFactory.Boulder(variant + 2), "SnowCap", t, new Vector3(-0.05f * s.x, 0.78f * s.y, 0f),
                Quaternion.Euler(0f, variant * 23f, 0f), new Vector3(0.62f * s.x, 0.22f * s.y, 0.52f * s.z), snow);
            if (interactive)
            {
                var col = root.AddComponent<SphereCollider>();
                col.radius = 0.8f * s.x;
                col.center = new Vector3(0f, 0.5f * s.y, 0f);
                col.isTrigger = true;
            }
            return root;
        }

        private static GameObject BuildWall(float width, float heightMul, Color color)
        {
            var root = new GameObject("Wall");
            Transform t = root.transform;
            float h = 1.5f * heightMul;
            PrimitiveBuilder.Create(PrimitiveType.Cube, "Body", t, new Vector3(0f, h * 0.5f - 0.1f, 0f), new Vector3(width, h, 0.5f), MaterialFactory.Lit(color, 0.3f));
            Material white = MaterialFactory.Lit(Color.white, 0.3f);
            PrimitiveBuilder.Create(PrimitiveType.Cube, "Stripe1", t, new Vector3(0f, h * 0.3f, 0f), new Vector3(width + 0.01f, 0.15f, 0.52f), white);
            PrimitiveBuilder.Create(PrimitiveType.Cube, "Stripe2", t, new Vector3(0f, h * 0.68f, 0f), new Vector3(width + 0.01f, 0.15f, 0.52f), white);
            Material post = MaterialFactory.Lit(new Color(0.25f, 0.25f, 0.28f), 0.3f);
            PrimitiveBuilder.Create(PrimitiveType.Cube, "PostL", t, new Vector3(-width * 0.5f, h * 0.5f, 0f), new Vector3(0.2f, h + 0.2f, 0.6f), post);
            PrimitiveBuilder.Create(PrimitiveType.Cube, "PostR", t, new Vector3(width * 0.5f, h * 0.5f, 0f), new Vector3(0.2f, h + 0.2f, 0.6f), post);
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, h * 0.5f, 0f);
            box.size = new Vector3(width, h, 0.6f);
            box.isTrigger = true;
            return root;
        }

        private static GameObject BuildLog(float width, float heightMul, Color color)
        {
            var root = new GameObject("Log");
            Transform t = root.transform;
            float d = 0.55f * heightMul;
            Quaternion lying = Quaternion.Euler(0f, 0f, 90f); // cylinder axis along X
            PrimitiveBuilder.Create(PrimitiveType.Cylinder, "Trunk", t, new Vector3(0f, d * 0.5f, 0f), lying, new Vector3(d, width * 0.5f, d), MaterialFactory.Lit(color, 0.2f));
            Material ring = MaterialFactory.Lit(new Color(1f, 0.85f, 0.55f), 0.2f);
            PrimitiveBuilder.Create(PrimitiveType.Cylinder, "EndL", t, new Vector3(-width * 0.5f, d * 0.5f, 0f), lying, new Vector3(d * 0.9f, 0.02f, d * 0.9f), ring);
            PrimitiveBuilder.Create(PrimitiveType.Cylinder, "EndR", t, new Vector3(width * 0.5f, d * 0.5f, 0f), lying, new Vector3(d * 0.9f, 0.02f, d * 0.9f), ring);
            Material band = MaterialFactory.Lit(MarkerOrange * 1.1f, 0.4f);
            for (float x = -width * 0.35f; x <= width * 0.36f; x += Mathf.Max(0.8f, width * 0.35f))
                PrimitiveBuilder.Create(PrimitiveType.Cylinder, "Band", t, new Vector3(x, d * 0.5f, 0f), lying, new Vector3(d * 1.03f, 0.06f, d * 1.03f), band);
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, d * 0.5f, 0f);
            box.size = new Vector3(width, d, d);
            box.isTrigger = true;
            return root;
        }

        private static GameObject BuildGap(float width, float lengthMul, Color color)
        {
            var root = new GameObject("Gap");
            Transform t = root.transform;
            float len = 2.4f * lengthMul;
            PrimitiveBuilder.Create(PrimitiveType.Cube, "Hole", t, new Vector3(0f, 0.02f, 0f), new Vector3(width, 0.04f, len), MaterialFactory.Lit(color, 0.6f));
            PrimitiveBuilder.Create(PrimitiveType.Cube, "Depth", t, new Vector3(0f, 0.03f, 0f), new Vector3(width * 0.9f, 0.04f, len * 0.7f), MaterialFactory.Lit(color * 0.4f, 0.6f));
            Material edge = MaterialFactory.Lit(MarkerOrange, 0.4f);
            PrimitiveBuilder.Create(PrimitiveType.Cube, "EdgeFront", t, new Vector3(0f, 0.04f, -len * 0.5f), new Vector3(width, 0.07f, 0.18f), edge);
            PrimitiveBuilder.Create(PrimitiveType.Cube, "EdgeBack", t, new Vector3(0f, 0.04f, len * 0.5f), new Vector3(width, 0.07f, 0.18f), edge);
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0 ? -1f : 1f) * width * 0.5f;
                float z = (i < 2 ? -1f : 1f) * len * 0.5f;
                PrimitiveBuilder.Create(PrimitiveType.Cylinder, "Pole", t, new Vector3(x, 0.4f, z), new Vector3(0.06f, 0.4f, 0.06f), edge);
            }
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.17f, 0f);
            box.size = new Vector3(width, 0.35f, len * 0.8f);
            box.isTrigger = true;
            return root;
        }

        private static GameObject BuildRamp(float width, float height, float length, Color color)
        {
            var root = new GameObject("Ramp");
            Transform t = root.transform;
            GameObject body = PrimitiveBuilder.CreateMesh(MeshFactory.UnitWedge, "RampBody", t, Vector3.zero, Quaternion.identity,
                new Vector3(width, height, length), MaterialFactory.Lit(color, 0.5f));
            body.AddComponent<MeshCollider>().sharedMesh = MeshFactory.UnitWedge;
            // Yellow stripe at the lip + white side stripes so ramps read clearly from far away.
            float angle = Mathf.Atan2(height, length) * Mathf.Rad2Deg;
            float lipZ = length - 0.2f;
            PrimitiveBuilder.Create(PrimitiveType.Cube, "LipStripe", t, new Vector3(0f, height * lipZ / length + 0.02f, lipZ),
                Quaternion.Euler(-angle, 0f, 0f), new Vector3(width * 1.001f, 0.04f, 0.35f), MaterialFactory.Emissive(new Color(1f, 0.9f, 0.1f), new Color(1f, 0.8f, 0.1f) * 1.3f, 0.4f));
            Material white = MaterialFactory.Lit(Color.white, 0.4f);
            for (int side = -1; side <= 1; side += 2)
                PrimitiveBuilder.Create(PrimitiveType.Cube, "SideStripe", t, new Vector3(side * width * 0.42f, height * 0.5f + 0.02f, length * 0.5f),
                    Quaternion.Euler(-angle, 0f, 0f), new Vector3(0.2f, 0.04f, Mathf.Sqrt(length * length + height * height) * 0.95f), white);
            return root;
        }

        /// <summary>Gold spinning coin with a generous trigger.</summary>
        public static GameObject CreateCoin(int value, float pickupRadius)
        {
            var root = new GameObject("Coin");
            var col = root.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = pickupRadius;
            Transform spinner = PrimitiveBuilder.CreateEmpty("Spinner", root.transform, Vector3.zero);
            Quaternion upright = Quaternion.Euler(90f, 0f, 0f);
            PrimitiveBuilder.Create(PrimitiveType.Cylinder, "Disc", spinner, Vector3.zero, upright, new Vector3(0.6f, 0.04f, 0.6f),
                MaterialFactory.Emissive(new Color(1f, 0.78f, 0.15f), new Color(0.9f, 0.55f, 0.05f) * 1.4f, 0.85f, 0.8f));
            PrimitiveBuilder.Create(PrimitiveType.Cylinder, "Face", spinner, Vector3.zero, upright, new Vector3(0.42f, 0.05f, 0.42f),
                MaterialFactory.Emissive(new Color(1f, 0.92f, 0.4f), new Color(1f, 0.75f, 0.2f) * 1.8f, 0.85f, 0.8f));
            root.AddComponent<Coin>().Configure(value, spinner);
            PrimitiveBuilder.SetLayerRecursively(root, GameLayers.Interactable);
            return root;
        }

        /// <summary>Finish gate for the fixed-length course (visual only).</summary>
        public static GameObject CreateFinishGate(float width)
        {
            var root = new GameObject("FinishLine");
            Transform t = root.transform;
            Material dark = MaterialFactory.Lit(new Color(0.15f, 0.15f, 0.18f), 0.3f);
            Material black = MaterialFactory.Lit(Color.black, 0.3f);
            Material white = MaterialFactory.Lit(Color.white, 0.3f);
            PrimitiveBuilder.Create(PrimitiveType.Cylinder, "PostL", t, new Vector3(-width * 0.5f, 2.6f, 0f), new Vector3(0.3f, 2.6f, 0.3f), dark);
            PrimitiveBuilder.Create(PrimitiveType.Cylinder, "PostR", t, new Vector3(width * 0.5f, 2.6f, 0f), new Vector3(0.3f, 2.6f, 0.3f), dark);
            const int cols = 16;
            float cw = width / cols;
            for (int row = 0; row < 2; row++)
                for (int c = 0; c < cols; c++)
                    PrimitiveBuilder.Create(PrimitiveType.Cube, "Check", t, new Vector3(-width * 0.5f + cw * (c + 0.5f), 4.6f + row * 0.5f, 0f),
                        new Vector3(cw, 0.5f, 0.08f), ((c + row) & 1) == 0 ? black : white);
            PrimitiveBuilder.Create(PrimitiveType.Cube, "GroundLine", t, new Vector3(0f, 0.02f, 0f), new Vector3(width, 0.03f, 0.8f),
                MaterialFactory.Lit(new Color(0.9f, 0.1f, 0.1f), 0.3f));
            return root;
        }
    }
}
