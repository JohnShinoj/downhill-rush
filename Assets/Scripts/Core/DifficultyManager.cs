using UnityEngine;

namespace SkiGame
{
    /// <summary>
    /// Tracks the current difficulty (from the player's distance) and pushes the
    /// speed bonus into the player. World spawning reads DifficultySettings directly.
    /// </summary>
    public class DifficultyManager : MonoBehaviour
    {
        [SerializeField] private DifficultySettings settings;
        [SerializeField] private PlayerController player;

        /// <summary>Current difficulty 0..1.</summary>
        public float Current { get; private set; }
        public DifficultySettings Settings => settings;

        private void Awake()
        {
            if (settings == null) settings = ScriptableObject.CreateInstance<DifficultySettings>();
            if (player == null) player = FindAnyObjectByType<PlayerController>();
        }

        private void Update()
        {
            if (player == null) return;
            Current = settings.Evaluate(player.DistanceTravelled);
            player.MaxSpeedMultiplier = Mathf.Lerp(1f, settings.maxSpeedMultiplierAtMax, Current);
        }
    }
}
