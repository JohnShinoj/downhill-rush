using UnityEngine;

namespace SkiGame
{
    /// <summary>
    /// Marks something that crashes the skier on contact (swerve or jumpable obstacle).
    /// Lives on layer "Ignore Raycast" with trigger colliders; the player finds it with
    /// an overlap query. Jumpables are simply low enough to clear with a jump.
    /// </summary>
    public class Obstacle : MonoBehaviour
    {
        [SerializeField] private SpawnKind kind = SpawnKind.Swerve;
        [SerializeField] private string displayName = "obstacle";

        public SpawnKind Kind => kind;
        public string DisplayName => displayName;
        public bool HasBeenHit { get; private set; }

        public void Configure(SpawnKind obstacleKind, string obstacleName)
        {
            kind = obstacleKind;
            displayName = string.IsNullOrEmpty(obstacleName) ? "obstacle" : obstacleName.ToLowerInvariant();
        }

        /// <summary>Called by the player on impact: disable so it can't be hit twice, and knock it over a bit.</summary>
        public void OnHit()
        {
            HasBeenHit = true;
            foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
            transform.rotation *= Quaternion.Euler(Random.Range(-20f, 20f), 0f, Random.Range(-25f, 25f));
        }
    }
}
