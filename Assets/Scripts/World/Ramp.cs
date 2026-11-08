using UnityEngine;

namespace SkiGame
{
    /// <summary>
    /// A ramp: a solid wedge on the ground layer. The player rides up it via the ground
    /// raycast; when it leaves the lip it is launched with at least LaunchVelocity.
    /// </summary>
    public class Ramp : MonoBehaviour
    {
        [SerializeField] private float launchVelocity = 12f;
        [SerializeField] private float length = 7f;
        [SerializeField] private float height = 1.6f;

        public float LaunchVelocity => launchVelocity;
        public float Length => length;
        public float Height => height;

        public void Configure(float launch, float rampLength, float rampHeight)
        {
            launchVelocity = launch;
            length = rampLength;
            height = rampHeight;
        }
    }
}
