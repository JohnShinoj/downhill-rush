using UnityEngine;

namespace SkiGame
{
    /// <summary>
    /// Difficulty ramp. Difficulty is a 0..1 value that depends on DISTANCE down the slope,
    /// so world generation is deterministic (same distance = same difficulty).
    /// </summary>
    [CreateAssetMenu(menuName = "Ski Game/Difficulty Settings", fileName = "DifficultySettings")]
    public class DifficultySettings : ScriptableObject
    {
        [Tooltip("Distance (m) at which difficulty reaches its maximum.")]
        [Min(1f)] public float distanceForMaxDifficulty = 3000f;
        [Tooltip("Maps normalised distance (x 0..1) to difficulty (y 0..1).")]
        public AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Obstacle density")]
        [Tooltip("Metres between obstacle rows at difficulty 0.")]
        [Min(4f)] public float rowSpacingEasy = 34f;
        [Tooltip("Metres between obstacle rows at difficulty 1.")]
        [Min(4f)] public float rowSpacingHard = 15f;
        [Range(0f, 0.5f)] public float rowSpacingJitter = 0.25f;
        [Min(1)] public int maxSwervePerRowEasy = 1;
        [Min(1)] public int maxSwervePerRowHard = 3;

        [Header("Speed")]
        [Tooltip("Player max speed is multiplied by this at difficulty 1.")]
        [Min(1f)] public float maxSpeedMultiplierAtMax = 1.2f;

        /// <summary>Difficulty (0..1) at a given distance from the start.</summary>
        public float Evaluate(float distance)
        {
            float t = Mathf.Clamp01(distance / Mathf.Max(1f, distanceForMaxDifficulty));
            return Mathf.Clamp01(curve != null ? curve.Evaluate(t) : t);
        }
    }
}
