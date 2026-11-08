using UnityEngine;

namespace SkiGame
{
    /// <summary>
    /// Populates every new slope chunk with obstacles, ramps and coins, driven entirely
    /// by the SpawnTable (what can spawn, how often, from which difficulty) and the
    /// DifficultySettings (how dense). Rows are laid out down the slope; each row is
    /// either a set of swerve obstacles (one lane always free), one jumpable obstacle
    /// or one ramp. Coins form lines between rows and arcs over jumps / after ramps.
    /// Everything is parented to its chunk, so it despawns with the chunk.
    /// </summary>
    public class ContentSpawner : MonoBehaviour
    {
        [SerializeField] private SlopeGenerator slope;
        [SerializeField] private SpawnTable spawnTable;
        [SerializeField] private DifficultySettings difficulty;
        [SerializeField] private SkierSettings skierSettings;
        [SerializeField] private GameRulesSettings rules;
        [Tooltip("0 = random every run.")]
        [SerializeField] private int seed = 0;

        private System.Random rng;
        private float nextRowZ;
        private bool finishSpawned;

        private void Awake()
        {
            if (slope == null) slope = GetComponent<SlopeGenerator>();
            if (slope == null) slope = FindAnyObjectByType<SlopeGenerator>();
            if (spawnTable == null) spawnTable = ScriptableObject.CreateInstance<SpawnTable>();
            if (difficulty == null) difficulty = ScriptableObject.CreateInstance<DifficultySettings>();
            if (skierSettings == null) skierSettings = ScriptableObject.CreateInstance<SkierSettings>();
            if (rules == null) rules = ScriptableObject.CreateInstance<GameRulesSettings>();
            rng = new System.Random(seed != 0 ? seed : System.Environment.TickCount ^ 0x5f3759df);
            nextRowZ = spawnTable.firstRowDistance;
        }

        private void OnEnable() { if (slope != null) slope.ChunkSpawned += HandleChunkSpawned; }
        private void OnDisable() { if (slope != null) slope.ChunkSpawned -= HandleChunkSpawned; }

        private void HandleChunkSpawned(SlopeChunk chunk)
        {
            bool course = slope.FixedCourseActive;
            float courseEnd = slope.CourseLength;
            Transform parent = chunk.transform;

            if (course && !finishSpawned && courseEnd >= chunk.StartZ && courseEnd < chunk.EndZ)
            {
                GameObject gate = ObstacleFactory.CreateFinishGate(2f * (slope.HalfWidth + 1f));
                Place(gate, 0f, courseEnd, true, parent, 0f);
                finishSpawned = true;
            }

            int guard = 0;
            while (nextRowZ < chunk.EndZ && guard++ < 64)
            {
                float z = nextRowZ;
                float d = difficulty.Evaluate(z);
                float spacing = Mathf.Lerp(difficulty.rowSpacingEasy, difficulty.rowSpacingHard, d) *
                                (1f + Range(-difficulty.rowSpacingJitter, difficulty.rowSpacingJitter));

                if (course && z > courseEnd - 25f) { nextRowZ += spacing; continue; }

                float clearance = SpawnRow(z, d, parent);
                float gap = Mathf.Max(spacing, clearance);
                if (clearance <= 0f) TrySpawnCoinLine(z, gap, parent);
                nextRowZ += gap;
            }
        }

        /// <summary>Spawns one row. Returns the free distance required after it (0 for swerve rows).</summary>
        private float SpawnRow(float z, float d, Transform parent)
        {
            SpawnEntry e = Pick(d, null);
            if (e == null) return 0f;
            float hw = slope.HalfWidth;

            switch (e.kind)
            {
                case SpawnKind.Swerve:
                    SpawnSwerveRow(z, d, e, parent);
                    return 0f;

                case SpawnKind.Jumpable:
                {
                    float width = Mathf.Clamp(e.widthFraction * 2f * hw, 1.5f, 2f * hw);
                    float lateral = width >= 2f * hw - 0.1f ? 0f : Range(-hw + width * 0.5f, hw - width * 0.5f);
                    float j = Jitter(e);
                    GameObject go = ObstacleFactory.Create(e, new Vector3(width, e.size.y * j, e.size.z * j));
                    Place(go, lateral, z, e.alignToSlope, parent, 0f);
                    if (e.coinArc && Chance(spawnTable.coinArcChance)) SpawnJumpArc(lateral, z, e.coinArcCount, parent);
                    return spawnTable.clearanceAfterJumpable;
                }

                case SpawnKind.Ramp:
                {
                    float j = Jitter(e);
                    float w = spawnTable.rampWidth * e.size.x * j;
                    float h = spawnTable.rampHeight * e.size.y * j;
                    float l = spawnTable.rampLength * e.size.z * j;
                    float lateral = Range(-hw + w * 0.5f + 0.5f, hw - w * 0.5f - 0.5f);
                    GameObject go = ObstacleFactory.Create(e, new Vector3(w, h, l));
                    Place(go, lateral, z, true, parent, 0f);
                    if (e.coinArc && Chance(spawnTable.coinArcChance))
                        SpawnRampArc(lateral, z + l, h, e.rampLaunchVelocity, e.coinArcCount, parent);
                    return spawnTable.clearanceAfterRamp;
                }
            }
            return 0f;
        }

        private void SpawnSwerveRow(float z, float d, SpawnEntry first, Transform parent)
        {
            float hw = slope.HalfWidth;
            int lanes = Mathf.Max(2, Mathf.FloorToInt(2f * hw / Mathf.Max(1f, spawnTable.laneWidth)));
            float laneW = 2f * hw / lanes;
            var occupied = new bool[lanes];
            int free = lanes;

            int maxCount = Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(difficulty.maxSwervePerRowEasy, difficulty.maxSwervePerRowHard, d)));
            int count = rng.Next(1, maxCount + 1);
            SpawnEntry entry = first;

            for (int k = 0; k < count; k++)
            {
                if (k > 0) entry = Pick(d, SpawnKind.Swerve);
                if (entry == null) break;

                bool isWall = entry.shape == SpawnShape.Wall;
                float width = isWall ? Mathf.Clamp(entry.widthFraction * 2f * hw, 2f, 2f * hw * 0.6f) : laneW * 0.8f;
                int span = Mathf.Clamp(Mathf.CeilToInt(width / laneW), 1, lanes - 1);
                if (free - span < 1) continue; // always keep at least one lane open

                int start = FindFreeRun(occupied, span);
                if (start < 0) continue;
                for (int i = start; i < start + span; i++) occupied[i] = true;
                free -= span;

                float lateral = -hw + start * laneW + span * laneW * 0.5f;
                if (span == 1) lateral += Range(-0.2f, 0.2f) * laneW;
                float zz = z + Range(-spawnTable.rowZJitter, spawnTable.rowZJitter);
                float j = Jitter(entry);
                Vector3 dims = isWall ? new Vector3(width, entry.size.y * j, entry.size.z * j) : entry.size * j;
                GameObject go = ObstacleFactory.Create(entry, dims);
                Place(go, lateral, zz, entry.alignToSlope, parent, entry.alignToSlope ? 0f : Range(0f, 360f));
            }
        }

        private int FindFreeRun(bool[] occupied, int span)
        {
            int maxStart = occupied.Length - span;
            for (int attempt = 0; attempt < 10; attempt++)
            {
                int s = rng.Next(0, maxStart + 1);
                bool ok = true;
                for (int i = s; i < s + span; i++) if (occupied[i]) { ok = false; break; }
                if (ok) return s;
            }
            return -1;
        }

        // ------------------------------------------------------------------ coins
        private void TrySpawnCoinLine(float rowZ, float gap, Transform parent)
        {
            if (!Chance(spawnTable.coinLineChance)) return;
            float start = rowZ + 6f;
            int count = Mathf.Min(spawnTable.coinLineMaxCount, Mathf.FloorToInt((gap - 12f) / spawnTable.coinLineSpacing));
            if (count < 2) return;
            float hw = slope.HalfWidth;
            float lateral = Range(-hw + 1.5f, hw - 1.5f);
            for (int i = 0; i < count; i++)
                SpawnCoin(slope.SurfacePoint(lateral, start + i * spawnTable.coinLineSpacing, spawnTable.coinHoverHeight), parent);
        }

        /// <summary>Arc that matches the jump trajectory, centred over a jumpable obstacle.</summary>
        private void SpawnJumpArc(float lateral, float z0, int n, Transform parent)
        {
            float airtime = skierSettings.JumpAirtime;
            float peak = skierSettings.JumpPeakHeight;
            float half = spawnTable.coinArcSpeedEstimate * airtime * 0.5f;
            for (int i = 0; i < n; i++)
            {
                float s = n == 1 ? 0f : Mathf.Lerp(-0.75f, 0.75f, i / (float)(n - 1));
                float h = spawnTable.coinHoverHeight + peak * (1f - s * s);
                SpawnCoin(slope.SurfacePoint(lateral, z0 + s * half, h), parent);
            }
        }

        /// <summary>Arc following the ballistic flight after a ramp lip.</summary>
        private void SpawnRampArc(float lateral, float zLip, float rampHeight, float launchVelocity, int n, Transform parent)
        {
            float g = Mathf.Max(0.1f, skierSettings.gravity);
            float v = launchVelocity;
            float airtime = (v + Mathf.Sqrt(v * v + 2f * g * rampHeight)) / g;
            for (int i = 1; i <= n; i++)
            {
                float t = airtime * i / (n + 1f);
                float z = zLip + spawnTable.coinArcSpeedEstimate * t;
                float h = rampHeight + v * t - 0.5f * g * t * t + spawnTable.coinHoverHeight * 0.6f;
                SpawnCoin(slope.SurfacePoint(lateral, z, Mathf.Max(h, spawnTable.coinHoverHeight)), parent);
            }
        }

        private void SpawnCoin(Vector3 position, Transform parent)
        {
            GameObject coin = ObstacleFactory.CreateCoin(rules.coinValue, spawnTable.coinPickupRadius);
            coin.transform.position = position;
            coin.transform.SetParent(parent, true);
        }

        // ------------------------------------------------------------------ helpers
        private void Place(GameObject go, float lateral, float z, bool alignToSlope, Transform parent, float extraYaw)
        {
            Vector3 p = slope.SurfacePoint(lateral, z);
            float yaw = slope.GetPathYaw(z);
            Quaternion rot = alignToSlope
                ? Quaternion.Euler(slope.SlopeAngle, yaw, 0f)
                : Quaternion.Euler(0f, yaw + extraYaw, 0f);
            go.transform.SetPositionAndRotation(p, rot);
            go.transform.SetParent(parent, true);
        }

        /// <summary>Weighted random pick among entries allowed at difficulty d (optionally of one kind).</summary>
        private SpawnEntry Pick(float d, SpawnKind? kind)
        {
            float total = 0f;
            foreach (SpawnEntry e in spawnTable.entries)
                if (IsAllowed(e, d, kind)) total += e.weight;
            if (total <= 0f) return null;

            double r = rng.NextDouble() * total;
            SpawnEntry last = null;
            foreach (SpawnEntry e in spawnTable.entries)
            {
                if (!IsAllowed(e, d, kind)) continue;
                last = e;
                r -= e.weight;
                if (r <= 0.0) return e;
            }
            return last;
        }

        private static bool IsAllowed(SpawnEntry e, float d, SpawnKind? kind)
        {
            return e != null && e.weight > 0f && d >= e.minDifficulty && (kind == null || e.kind == kind.Value);
        }

        private float Jitter(SpawnEntry e) => 1f + Range(-e.sizeJitter, e.sizeJitter);
        private float Range(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        private bool Chance(float p) => rng.NextDouble() < p;
    }
}
