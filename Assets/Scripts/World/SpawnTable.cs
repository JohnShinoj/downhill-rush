using System.Collections.Generic;
using UnityEngine;

namespace SkiGame
{
    /// <summary>What a spawned thing does in gameplay. Colour coding: Swerve = red, Jumpable = orange, Ramp = blue.</summary>
    public enum SpawnKind
    {
        /// <summary>Must be steered around (trees, rocks, walls). Red.</summary>
        Swerve,
        /// <summary>Can be jumped over (low logs, crevasses). Orange.</summary>
        Jumpable,
        /// <summary>Launches the skier (longer airtime). Blue.</summary>
        Ramp
    }

    /// <summary>Built-in procedural shapes. Use CustomPrefab + a prefab for your own models.</summary>
    public enum SpawnShape { Tree, Rock, Wall, Log, Gap, Ramp, CustomPrefab }

    /// <summary>One row in the data-driven spawn table.</summary>
    [System.Serializable]
    public class SpawnEntry
    {
        public string name = "Obstacle";
        public SpawnKind kind = SpawnKind.Swerve;
        public SpawnShape shape = SpawnShape.Tree;
        [Tooltip("Optional prefab used instead of the procedural shape. Colliders are set up automatically.")]
        public GameObject prefab;
        [Tooltip("Relative chance to be picked.")]
        [Min(0f)] public float weight = 1f;
        [Tooltip("Only spawns once difficulty (0..1) reaches this value.")]
        [Range(0f, 1f)] public float minDifficulty = 0f;
        [Tooltip("Size multiplier (x = width, y = height, z = depth).")]
        public Vector3 size = Vector3.one;
        [Range(0f, 0.5f)] public float sizeJitter = 0.15f;
        [Tooltip("For walls, logs and gaps: width as a fraction of the path width.")]
        [Range(0.05f, 1f)] public float widthFraction = 0.5f;
        public Color color = Color.red;
        [Tooltip("Rotate with the slope (flat objects) instead of standing upright (trees, rocks).")]
        public bool alignToSlope = false;
        [Header("Coins")]
        [Tooltip("Spawn an arc of coins above this (jumpables) or after it (ramps).")]
        public bool coinArc = false;
        [Min(1)] public int coinArcCount = 5;
        [Header("Ramp only")]
        [Tooltip("Upward speed when leaving the ramp (m/s). Airtime grows with this.")]
        public float rampLaunchVelocity = 12f;
    }

    /// <summary>
    /// Data-driven description of everything that spawns on the slope: obstacles,
    /// ramps and coins. Add entries in the Inspector - no code changes needed.
    /// </summary>
    [CreateAssetMenu(menuName = "Ski Game/Spawn Table", fileName = "SpawnTable")]
    public class SpawnTable : ScriptableObject
    {
        public List<SpawnEntry> entries = CreateDefaultEntries();

        [Header("Rows")]
        [Tooltip("No obstacles before this distance (m).")]
        public float firstRowDistance = 70f;
        [Tooltip("Width of a lane used to place swerve obstacles side by side (m). One lane is always left free.")]
        public float laneWidth = 3f;
        public float rowZJitter = 1.5f;
        [Tooltip("Free distance after a jumpable obstacle (m).")]
        public float clearanceAfterJumpable = 18f;
        [Tooltip("Free distance after a ramp, so you never land on an obstacle (m).")]
        public float clearanceAfterRamp = 48f;

        [Header("Coins")]
        [Range(0f, 1f)] public float coinLineChance = 0.55f;
        [Min(1)] public int coinLineMaxCount = 7;
        public float coinLineSpacing = 2.8f;
        [Tooltip("Height of ground coins above the snow (m).")]
        public float coinHoverHeight = 0.9f;
        [Range(0f, 1f)] public float coinArcChance = 0.85f;
        [Tooltip("Assumed skier speed (m/s) used to lay out coin arcs over jumps and ramps.")]
        public float coinArcSpeedEstimate = 20f;
        [Tooltip("Pickup radius of a coin (m). Generous on purpose.")]
        public float coinPickupRadius = 0.8f;

        [Header("Ramp geometry (m)")]
        public float rampWidth = 4.5f;
        public float rampLength = 7f;
        public float rampHeight = 1.6f;

        public static List<SpawnEntry> CreateDefaultEntries()
        {
            return new List<SpawnEntry>
            {
                new SpawnEntry { name = "Pine Tree", kind = SpawnKind.Swerve, shape = SpawnShape.Tree, weight = 3f,
                    color = new Color(0.13f, 0.42f, 0.2f), alignToSlope = false },
                new SpawnEntry { name = "Rock", kind = SpawnKind.Swerve, shape = SpawnShape.Rock, weight = 2f,
                    color = new Color(0.55f, 0.22f, 0.2f), alignToSlope = false },
                new SpawnEntry { name = "Barrier Wall", kind = SpawnKind.Swerve, shape = SpawnShape.Wall, weight = 1f,
                    minDifficulty = 0.2f, widthFraction = 0.35f, color = new Color(0.88f, 0.12f, 0.1f), alignToSlope = true },
                new SpawnEntry { name = "Log", kind = SpawnKind.Jumpable, shape = SpawnShape.Log, weight = 1.6f,
                    widthFraction = 0.6f, color = new Color(0.95f, 0.55f, 0.12f), alignToSlope = true, coinArc = true },
                new SpawnEntry { name = "Long Log", kind = SpawnKind.Jumpable, shape = SpawnShape.Log, weight = 0.8f,
                    minDifficulty = 0.4f, widthFraction = 1f, color = new Color(0.95f, 0.5f, 0.1f), alignToSlope = true, coinArc = true },
                new SpawnEntry { name = "Crevasse", kind = SpawnKind.Jumpable, shape = SpawnShape.Gap, weight = 1f,
                    minDifficulty = 0.15f, widthFraction = 0.7f, color = new Color(0.06f, 0.08f, 0.18f), alignToSlope = true, coinArc = true },
                new SpawnEntry { name = "Kicker Ramp", kind = SpawnKind.Ramp, shape = SpawnShape.Ramp, weight = 1.2f,
                    sizeJitter = 0.1f, color = new Color(0.15f, 0.55f, 1f), alignToSlope = true, coinArc = true, coinArcCount = 7,
                    rampLaunchVelocity = 12f },
            };
        }
    }
}
