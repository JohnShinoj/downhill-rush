using UnityEngine;

namespace SkiGame
{
    /// <summary>
    /// All tunable numbers for the skier's movement, jump, backflip, crash handling and
    /// the input-delay assists. Create via Assets > Create > Ski Game > Skier Settings.
    /// </summary>
    [CreateAssetMenu(menuName = "Ski Game/Skier Settings", fileName = "SkierSettings")]
    public class SkierSettings : ScriptableObject
    {
        [Header("Speed (m/s)")]
        public float startSpeed = 14f;
        public float minSpeed = 8f;
        public float maxSpeed = 34f;
        [Tooltip("Constant downhill acceleration (m/s²).")]
        public float downhillAcceleration = 1.6f;
        [Tooltip("Extra acceleration at SpeedControl = +1 (m/s²).")]
        public float boostAcceleration = 8f;
        [Tooltip("Deceleration at SpeedControl = -1 (m/s²).")]
        public float brakeDeceleration = 14f;
        [Tooltip("Speed lost while carving at full steer (m/s²).")]
        public float carveDrag = 1.5f;
        [Tooltip("Fraction of the downhill acceleration applied while airborne.")]
        [Range(0f, 1f)] public float airAccelerationFactor = 0.3f;

        [Header("Steering")]
        [Tooltip("Maximum heading away from straight downhill (degrees). Keeps the skier from going sideways/uphill.")]
        [Range(10f, 85f)] public float maxHeadingAngle = 60f;
        [Tooltip("How fast the skier turns while steering is held at full (degrees / second). Lower = less sensitive.")]
        public float steerTurnRate = 55f;
        [Tooltip("How fast the heading recovers after a crash (degrees / second).")]
        public float turnRate = 170f;
        [Tooltip("Steering authority while airborne (0..1).")]
        [Range(0f, 1f)] public float airSteerFactor = 0.5f;
        [Tooltip("How quickly the body aligns to the ground normal.")]
        public float alignSharpness = 12f;

        [Header("Boundary banks")]
        [Tooltip("Extra deceleration when fully up on a bank (m/s²).")]
        public float bankFriction = 8f;
        [Tooltip("How strongly a bank turns you back toward the path (degrees / second).")]
        public float bankPushTurnRate = 110f;
        [Tooltip("Hard limit as a fraction of the bank width (0..1).")]
        [Range(0.1f, 1f)] public float hardBoundaryFraction = 0.85f;

        [Header("Jump")]
        [Tooltip("Upward launch speed (m/s). Airtime = 2 * jumpVelocity / gravity.")]
        public float jumpVelocity = 10f;
        [Tooltip("Extra upward speed when jumping ON a ramp (or right after leaving one). Small = ramp jumps don't go sky-high.")]
        public float rampJumpBonus = 3f;
        [Tooltip("Gravity (m/s²). Higher than real gravity for a snappy, arcade feel.")]
        public float gravity = 25f;

        [Header("Backflip")]
        [Tooltip("Seconds for one full 360° backflip. Must be shorter than the jump airtime.")]
        public float flipDuration = 0.62f;
        [Tooltip("Landing is still clean if the rotation is within this many degrees of upright.")]
        [Range(0f, 120f)] public float landingAngleTolerance = 55f;
        [Tooltip("Allow more than one flip during a single (long, ramp) airtime.")]
        public bool allowMultipleFlips = true;

        [Header("Input-delay assists (for delayed sensor signals)")]
        [Tooltip("A jump/backflip trigger is remembered for this long (s) and executed as soon as it becomes possible.")]
        [Range(0f, 0.5f)] public float inputBufferTime = 0.13f;
        [Tooltip("You can still jump for this long (s) after leaving the ground.")]
        [Range(0f, 0.5f)] public float coyoteTime = 0.12f;
        [Tooltip("Landing is detected when the feet are within this height (m) of the ground (generous landing check).")]
        [Range(0f, 1f)] public float landingTolerance = 0.18f;
        [Tooltip("While grounded, the skier sticks to ground that drops away by less than this (m) per frame.")]
        public float groundSnapDistance = 0.35f;

        [Header("Crash")]
        [Tooltip("Speed is multiplied by this on a crash.")]
        [Range(0f, 1f)] public float crashSpeedFactor = 0.45f;
        [Tooltip("Seconds without control after a crash.")]
        public float stunDuration = 0.9f;
        [Tooltip("Seconds of invulnerability after a crash (skier blinks).")]
        public float invulnerabilityTime = 1.8f;

        [Header("Collision volume (capsule)")]
        public float collisionRadius = 0.35f;
        public float collisionHeight = 1.5f;

        [Header("Ground probe")]
        public float groundProbeUp = 2.5f;
        public float groundProbeDown = 6f;

        [Header("Visual hints")]
        [Tooltip("Above this normalised speed the skier automatically tucks.")]
        [Range(0f, 1f)] public float tuckSpeedThreshold01 = 0.7f;

        /// <summary>Airtime of a normal jump on flat ground (s).</summary>
        public float JumpAirtime => 2f * jumpVelocity / Mathf.Max(0.01f, gravity);
        /// <summary>Peak height of a normal jump (m).</summary>
        public float JumpPeakHeight => jumpVelocity * jumpVelocity / (2f * Mathf.Max(0.01f, gravity));

        private void OnValidate()
        {
            minSpeed = Mathf.Max(0f, minSpeed);
            maxSpeed = Mathf.Max(minSpeed + 0.1f, maxSpeed);
            startSpeed = Mathf.Clamp(startSpeed, minSpeed, maxSpeed);
            flipDuration = Mathf.Max(0.1f, flipDuration);
            if (flipDuration > JumpAirtime)
                Debug.LogWarning($"[SkiGame] flipDuration ({flipDuration:F2}s) is longer than the jump airtime ({JumpAirtime:F2}s): a flip cannot finish in a normal jump.", this);
        }
    }
}
