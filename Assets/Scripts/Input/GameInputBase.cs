using UnityEngine;

namespace SkiGame
{
    /// <summary>
    /// Shared base for every input source. It turns RAW device values into the clean
    /// values the game expects:
    ///  - Steer / SpeedControl: clamped to -1..1, dead-zoned, then ramped smoothly toward
    ///    the target with an adjustable response time (seconds from 0 to full).
    ///  - Jump / Backflip: rising-edge detection + cooldown, so a held key or a long
    ///    muscle contraction still produces ONE single-frame trigger.
    ///
    /// A new input source only has to implement the four Read...() methods.
    /// Runs early (execution order -100) so the triggers are ready before gameplay reads them.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public abstract class GameInputBase : MonoBehaviour, IGameInput
    {
        [Header("Analog response (smoothing)")]
        [Tooltip("Seconds for Steer to move from 0 to full deflection. 0 = instant.")]
        [SerializeField, Min(0f)] private float steerResponseTime = 0.12f;
        [Tooltip("Seconds for Steer to return from full deflection to 0 when the input is released. Short = the skier stops sliding sideways quickly.")]
        [SerializeField, Min(0f)] private float steerReleaseTime = 0.04f;
        [Tooltip("Seconds for SpeedControl to move from 0 to full. 0 = instant.")]
        [SerializeField, Min(0f)] private float speedResponseTime = 0.25f;
        [Tooltip("Raw values smaller than this are treated as 0 (removes sensor noise / jitter).")]
        [SerializeField, Range(0f, 0.5f)] private float steerDeadZone = 0.05f;
        [SerializeField, Range(0f, 0.5f)] private float speedDeadZone = 0.05f;

        [Header("Discrete triggers")]
        [Tooltip("Minimum seconds between two jump triggers.")]
        [SerializeField, Min(0f)] private float jumpCooldown = 0.25f;
        [Tooltip("Minimum seconds between two backflip triggers.")]
        [SerializeField, Min(0f)] private float backflipCooldown = 0.30f;

        // ---- IGameInput ----
        public float Steer { get; private set; }
        public float SpeedControl { get; private set; }
        public bool JumpTriggered { get; private set; }
        public bool BackflipTriggered { get; private set; }

        // ---- Extra values for the debug HUD ----
        /// <summary>Target steer after clamp + dead zone, before smoothing.</summary>
        public float RawSteer { get; private set; }
        /// <summary>Target speed control after clamp + dead zone, before smoothing.</summary>
        public float RawSpeedControl { get; private set; }

        private bool previousJumpRequest;
        private bool previousBackflipRequest;
        private float nextJumpAllowedTime;
        private float nextBackflipAllowedTime;

        /// <summary>Raw steering from the device, ideally -1..1 (values are clamped).</summary>
        protected abstract float ReadSteer();
        /// <summary>Raw speed control from the device, ideally -1..1 (values are clamped).</summary>
        protected abstract float ReadSpeedControl();
        /// <summary>True while the device "requests" a jump (level or pulse - edges are detected here).</summary>
        protected abstract bool ReadJumpRequest();
        /// <summary>True while the device "requests" a backflip (level or pulse - edges are detected here).</summary>
        protected abstract bool ReadBackflipRequest();

        protected virtual void Update()
        {
            float dt = Time.deltaTime;

            // Analog channels: clamp -> dead zone -> smooth ramp.
            RawSteer = ApplyDeadZone(Mathf.Clamp(ReadSteer(), -1f, 1f), steerDeadZone);
            RawSpeedControl = ApplyDeadZone(Mathf.Clamp(ReadSpeedControl(), -1f, 1f), speedDeadZone);
            // Releasing (moving toward 0 or reversing) uses the shorter release time.
            bool releasing = Mathf.Abs(RawSteer) < Mathf.Abs(Steer) || RawSteer * Steer < 0f;
            Steer = Approach(Steer, RawSteer, releasing ? steerReleaseTime : steerResponseTime, dt);
            SpeedControl = Approach(SpeedControl, RawSpeedControl, speedResponseTime, dt);

            // Discrete channels: rising edge + cooldown => single-frame trigger.
            bool jumpRequest = ReadJumpRequest();
            JumpTriggered = jumpRequest && !previousJumpRequest && Time.time >= nextJumpAllowedTime;
            if (JumpTriggered) nextJumpAllowedTime = Time.time + jumpCooldown;
            previousJumpRequest = jumpRequest;

            bool flipRequest = ReadBackflipRequest();
            BackflipTriggered = flipRequest && !previousBackflipRequest && Time.time >= nextBackflipAllowedTime;
            if (BackflipTriggered) nextBackflipAllowedTime = Time.time + backflipCooldown;
            previousBackflipRequest = flipRequest;
        }

        /// <summary>Lets a subclass (e.g. the sensor input) set its own smoothing and dead zones.</summary>
        protected void ConfigureResponse(float steerResponse, float steerRelease, float speedResponse, float steerDz, float speedDz)
        {
            steerResponseTime = Mathf.Max(0f, steerResponse);
            steerReleaseTime = Mathf.Max(0f, steerRelease);
            speedResponseTime = Mathf.Max(0f, speedResponse);
            steerDeadZone = Mathf.Clamp(steerDz, 0f, 0.9f);
            speedDeadZone = Mathf.Clamp(speedDz, 0f, 0.9f);
        }

        protected virtual void OnDisable()
        {
            Steer = SpeedControl = RawSteer = RawSpeedControl = 0f;
            JumpTriggered = BackflipTriggered = false;
            previousJumpRequest = previousBackflipRequest = false;
        }

        private static float ApplyDeadZone(float value, float deadZone)
        {
            float a = Mathf.Abs(value);
            if (a <= deadZone) return 0f;
            return Mathf.Sign(value) * (a - deadZone) / (1f - deadZone);
        }

        /// <summary>Moves current toward target at a rate of 1 unit per responseTime seconds.</summary>
        private static float Approach(float current, float target, float responseTime, float dt)
        {
            if (responseTime <= 0f) return target;
            return Mathf.MoveTowards(current, target, dt / responseTime);
        }
    }
}
