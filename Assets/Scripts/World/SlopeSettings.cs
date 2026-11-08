using UnityEngine;

namespace SkiGame
{
    /// <summary>Shape, generation and look of the slope (lighting/sky live in VisualSettings). Assets > Create > Ski Game > Slope Settings.</summary>
    [CreateAssetMenu(menuName = "Ski Game/Slope Settings", fileName = "SlopeSettings")]
    public class SlopeSettings : ScriptableObject
    {
        [Header("Random")]
        [Tooltip("0 = different slope every run.")]
        public int seed = 0;

        [Header("Shape")]
        [Tooltip("Downhill angle in degrees.")]
        [Range(0f, 35f)] public float slopeAngle = 14f;
        [Tooltip("Half of the skiable path width (m). Path width = 2x this.")]
        [Min(2f)] public float pathHalfWidth = 9f;
        [Tooltip("Width of the soft boundary bank on each side (m).")]
        [Min(0.5f)] public float bankWidth = 7f;
        [Tooltip("Height the bank rises to (m).")]
        [Min(0f)] public float bankHeight = 3.5f;
        [Tooltip("Flat snow beyond the bank, filled with decorative trees (m).")]
        [Min(0f)] public float outerWidth = 30f;

        [Header("Winding")]
        [Tooltip("The first metres are straight.")]
        [Min(1f)] public float straightStartLength = 80f;
        public float windingAmplitudeStart = 6f;
        public float windingAmplitudeMax = 15f;
        [Tooltip("Distance over which the winding grows from start to max amplitude.")]
        [Min(1f)] public float windingRampDistance = 1500f;
        [Min(10f)] public float windingWavelength1 = 220f;
        [Min(10f)] public float windingWavelength2 = 85f;
        [Range(0f, 1f)] public float secondaryWindingRatio = 0.25f;

        [Header("Course")]
        [Tooltip("Off = endless mode. On = fixed-length course with a finish line (can also be chosen in the main menu).")]
        public bool useFixedCourse = false;
        [Min(100f)] public float courseLength = 2500f;
        [Tooltip("Extra slope generated after the finish line.")]
        public float runoutLength = 80f;

        [Header("Chunks (endless generation)")]
        [Min(5f)] public float chunkLength = 40f;
        [Min(0.5f)] public float rowSpacing = 2f;
        [Min(1)] public int pathSegments = 6;
        [Min(1)] public int bankSegments = 7;
        [Tooltip("Generate this far ahead of the player (m).")]
        public float spawnAhead = 240f;
        [Tooltip("Destroy chunks this far behind the player (m).")]
        public float despawnBehind = 40f;

        [Header("Look")]
        public Color snowColor = new Color(0.95f, 0.97f, 1f);
        [Tooltip("Glossiness of the snow (sun glints).")]
        [Range(0f, 1f)] public float snowSmoothness = 0.42f;
        [Tooltip("Strength of the procedural snow ripples (normal map).")]
        [Range(0f, 2f)] public float snowNormalStrength = 1.4f;
        [Tooltip("Metres per repeat of the snow texture.")]
        public float snowTextureTile = 8f;
        public Color edgeMarkerColorA = new Color(1f, 0.45f, 0.05f);
        public Color edgeMarkerColorB = new Color(0.1f, 0.35f, 0.95f);
        public float edgeMarkerSpacing = 12f;
        public Color decorTreeColor = new Color(0.12f, 0.36f, 0.2f);
        [Min(0)] public int decorTreesPerChunkSide = 9;
        [Min(0)] public int decorRocksPerChunkSide = 2;

    }
}
