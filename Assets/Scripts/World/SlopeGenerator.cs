using System.Collections.Generic;
using UnityEngine;

namespace SkiGame
{
    /// <summary>
    /// Endless winding slope.
    ///
    /// The slope is defined ANALYTICALLY (pure functions of x and z):
    ///   - base plane:  y = -z * tan(slopeAngle)          (downhill is +Z)
    ///   - path centre: x = sum of two sine waves of z    (the winding)
    ///   - banks:       smooth rise beyond the path half-width
    /// Mesh chunks are built from those functions ahead of the player and destroyed
    /// behind. Other systems (spawner, camera, player) query the same functions, so
    /// everything lines up without raycasts.
    ///
    /// Raises ChunkSpawned so the ContentSpawner can populate each new chunk.
    /// </summary>
    public class SlopeGenerator : MonoBehaviour
    {
        [SerializeField] private SlopeSettings settings;
        [Tooltip("Chunks are generated around this transform (the player).")]
        [SerializeField] private Transform player;

        /// <summary>Course/endless choice from the main menu; survives scene reloads. Null = use settings.</summary>
        public static bool? CourseModeOverride;

        /// <summary>Raised after a chunk has been built (content can be parented to chunk.transform).</summary>
        public event System.Action<SlopeChunk> ChunkSpawned;

        private readonly List<SlopeChunk> chunks = new List<SlopeChunk>();
        private int nextChunkIndex;
        private bool initialized;
        private float tanAngle, phase1, phase2;
        private Material snowMaterial;

        public SlopeSettings Settings { get { EnsureInit(); return settings; } }
        public bool FixedCourseActive => CourseModeOverride ?? Settings.useFixedCourse;
        public float CourseLength => Settings.courseLength;
        public float HalfWidth => Settings.pathHalfWidth;
        public float BankWidth => Settings.bankWidth;
        public float SlopeAngle => Settings.slopeAngle;
        public int DecorSeed { get; private set; }
        public Material SnowMaterial
        {
            get
            {
                if (snowMaterial == null)
                    snowMaterial = MaterialFactory.Snow(Settings.snowColor, Settings.snowSmoothness, Settings.snowNormalStrength);
                return snowMaterial;
            }
        }
        /// <summary>Normal of the slope base plane.</summary>
        public Vector3 SlopeNormal
        {
            get { float a = SlopeAngle * Mathf.Deg2Rad; return new Vector3(0f, Mathf.Cos(a), Mathf.Sin(a)); }
        }

        public void SetCourseMode(bool fixedCourse) => CourseModeOverride = fixedCourse;

        private void Awake()
        {
            EnsureInit();
            if (player == null)
            {
                var pc = FindAnyObjectByType<PlayerController>();
                if (pc != null) player = pc.transform;
            }
        }

        private void EnsureInit()
        {
            if (initialized) return;
            initialized = true;
            if (settings == null) settings = ScriptableObject.CreateInstance<SlopeSettings>();
            int seed = settings.seed != 0 ? settings.seed : System.Environment.TickCount;
            var rng = new System.Random(seed);
            phase1 = (float)(rng.NextDouble() * Mathf.PI * 2.0);
            phase2 = (float)(rng.NextDouble() * Mathf.PI * 2.0);
            DecorSeed = rng.Next();
            tanAngle = Mathf.Tan(settings.slopeAngle * Mathf.Deg2Rad);
            nextChunkIndex = Mathf.FloorToInt(-settings.despawnBehind / settings.chunkLength);
        }

        private void Start() => UpdateChunks();
        private void Update() => UpdateChunks();

        private void UpdateChunks()
        {
            float pz = player != null ? player.position.z : 0f;
            float limit = pz + settings.spawnAhead;
            if (FixedCourseActive) limit = Mathf.Min(limit, CourseLength + settings.runoutLength);

            int guard = 0;
            bool spawned = false;
            while (nextChunkIndex * settings.chunkLength < limit && guard++ < 64)
            {
                SpawnChunk(nextChunkIndex++);
                spawned = true;
            }
            // BUG FIX: spawned objects are built at the world origin and then moved. Without
            // this sync the physics engine still sees their colliders at the origin (where the
            // skier starts) until the next physics step -> "Hit a pine tree" right at the start.
            if (spawned) Physics.SyncTransforms();

            for (int i = chunks.Count - 1; i >= 0; i--)
            {
                if (chunks[i] == null) { chunks.RemoveAt(i); continue; }
                if (chunks[i].EndZ < pz - settings.despawnBehind)
                {
                    Destroy(chunks[i].gameObject);
                    chunks.RemoveAt(i);
                }
            }
        }

        private void SpawnChunk(int index)
        {
            var go = new GameObject("Chunk " + index);
            go.transform.SetParent(transform, false);
            go.transform.position = Vector3.zero;   // chunk vertices are in world space
            go.transform.rotation = Quaternion.identity;
            var chunk = go.AddComponent<SlopeChunk>();
            chunk.Build(this, index, index * settings.chunkLength, (index + 1) * settings.chunkLength);
            chunks.Add(chunk);
            ChunkSpawned?.Invoke(chunk);
        }

        // ------------------------------------------------------------- analytic slope
        /// <summary>Height of the base plane at z.</summary>
        public float GetBaseHeight(float z)
        {
            EnsureInit();
            return -z * tanAngle;
        }

        /// <summary>World x of the path centre at z.</summary>
        public float GetCenterX(float z)
        {
            EnsureInit();
            if (z <= 0f) return 0f;
            SlopeSettings s = settings;
            float straight = Smooth01(z / Mathf.Max(1f, s.straightStartLength));
            float ramp = Mathf.Clamp01(z / Mathf.Max(1f, s.windingRampDistance));
            float amp = Mathf.Lerp(s.windingAmplitudeStart, s.windingAmplitudeMax, ramp) * straight;
            float w1 = Mathf.Sin(z * Mathf.PI * 2f / s.windingWavelength1 + phase1);
            float w2 = Mathf.Sin(z * Mathf.PI * 2f / s.windingWavelength2 + phase2);
            return amp * (w1 + s.secondaryWindingRatio * w2);
        }

        /// <summary>Extra height of the soft bank at a lateral offset from the path centre.</summary>
        public float GetBankHeight(float lateralOffset)
        {
            EnsureInit();
            float d = Mathf.Abs(lateralOffset) - settings.pathHalfWidth;
            if (d <= 0f) return 0f;
            return settings.bankHeight * Smooth01(d / settings.bankWidth);
        }

        /// <summary>Snow surface height at world (x, z).</summary>
        public float GetSurfaceHeight(float x, float z) => GetBaseHeight(z) + GetBankHeight(x - GetCenterX(z));

        /// <summary>Path direction at z in degrees (0 = straight down +Z, positive = toward +X).</summary>
        public float GetPathYaw(float z)
        {
            float dx = GetCenterX(z + 1f) - GetCenterX(z - 1f);
            return Mathf.Atan2(dx, 2f) * Mathf.Rad2Deg;
        }

        /// <summary>Signed lateral offset of a world position from the path centre.</summary>
        public float LateralOffset(Vector3 worldPos) => worldPos.x - GetCenterX(worldPos.z);

        /// <summary>World point on the surface at a lateral offset and z, lifted by 'height'.</summary>
        public Vector3 SurfacePoint(float lateral, float z, float height = 0f)
        {
            float x = GetCenterX(z) + lateral;
            return new Vector3(x, GetSurfaceHeight(x, z) + height, z);
        }

        private static float Smooth01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
