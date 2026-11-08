using UnityEngine;

namespace SkiGame
{
    /// <summary>
    /// Smooth third-person follow camera.
    ///  - Follows forward motion exactly (no lag that grows with speed) and smooths
    ///    sideways / vertical motion, so jumps and turns feel soft.
    ///  - Partially follows the skier's heading.
    ///  - Field of view widens with speed. Short shake on crash.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class SkiCamera : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;
        [SerializeField] private PlayerController player;
        [SerializeField] private SlopeGenerator slope;

        [Header("Framing")]
        [SerializeField] private float distance = 7.5f;
        [SerializeField] private float height = 3.7f;
        [SerializeField] private float lookAhead = 9f;
        [SerializeField] private float lookHeight = 0.6f;
        [Tooltip("0 = camera always faces down the world axis, 1 = fully follows the skier heading.")]
        [SerializeField, Range(0f, 1f)] private float headingFollow = 0.45f;
        [SerializeField, Min(0f)] private float headingSharpness = 3f;

        [Header("Smoothing")]
        [SerializeField, Min(0f)] private float lateralSmoothTime = 0.12f;
        [SerializeField, Min(0f)] private float verticalSmoothTime = 0.18f;

        [Header("Field of view")]
        [SerializeField] private float baseFov = 60f;
        [SerializeField] private float maxSpeedFov = 74f;
        [SerializeField, Min(0f)] private float fovSharpness = 3f;

        [Header("Main menu orbit")]
        [SerializeField] private bool orbitInMenu = true;
        [SerializeField] private float orbitSpeed = 9f;
        [SerializeField] private float orbitDistance = 4.6f;
        [SerializeField] private float orbitHeight = 1.5f;
        [SerializeField] private float orbitStartAngle = 150f;
        [Tooltip("Seconds to blend from the menu orbit into the follow camera.")]
        [SerializeField] private float menuBlendTime = 1.4f;

        [Header("Crash shake")]
        [SerializeField] private float shakeAmplitude = 0.35f;
        [SerializeField] private float shakeDuration = 0.4f;

        private Camera cam;
        private Vector3 follow;
        private float relativeHeight;
        private float lateralVelocity, verticalVelocity;
        private float cameraYaw;
        private float shakeTimer;
        private float orbitAngle;
        private float menuBlend = 1f;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            if (player == null) player = FindAnyObjectByType<PlayerController>();
            if (target == null && player != null) target = player.transform;
            if (slope == null) slope = FindAnyObjectByType<SlopeGenerator>();
        }

        private void OnEnable() => GameEvents.Crashed += HandleCrash;
        private void OnDisable() => GameEvents.Crashed -= HandleCrash;
        private void HandleCrash(string reason) => shakeTimer = shakeDuration;

        private void Start()
        {
            orbitAngle = orbitStartAngle;
            menuBlend = InMenu ? 1f : 0f;
            Snap();
        }

        private bool InMenu => orbitInMenu && GameManager.Instance != null && GameManager.Instance.State == GameState.Menu;

        /// <summary>Jumps the camera to its target pose without smoothing.</summary>
        public void Snap()
        {
            if (target == null) return;
            follow = target.position;
            relativeHeight = target.position.y - BaseHeight(target.position.z);
            cameraYaw = player != null ? player.Heading * headingFollow : 0f;
            LateUpdate();
        }

        private float BaseHeight(float z) => slope != null ? slope.GetBaseHeight(z) : 0f;

        private void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.deltaTime;
            Vector3 t = target.position;

            // Forward: exact. Sideways: smoothed. Vertical: smoothed RELATIVE to the slope
            // plane, so going downhill quickly does not make the camera lag upwards.
            follow.z = t.z;
            follow.x = Mathf.SmoothDamp(follow.x, t.x, ref lateralVelocity, lateralSmoothTime, Mathf.Infinity, dt);
            float targetRelative = t.y - BaseHeight(t.z);
            relativeHeight = Mathf.SmoothDamp(relativeHeight, targetRelative, ref verticalVelocity, verticalSmoothTime, Mathf.Infinity, dt);
            follow.y = BaseHeight(t.z) + relativeHeight;

            float desiredYaw = player != null ? player.Heading * headingFollow : 0f;
            cameraYaw = Mathf.LerpAngle(cameraYaw, desiredYaw, 1f - Mathf.Exp(-headingSharpness * dt));
            Quaternion yaw = Quaternion.Euler(0f, cameraYaw, 0f);

            Vector3 position = follow + yaw * new Vector3(0f, height, -distance);
            Vector3 lookAt = follow + yaw * new Vector3(0f, lookHeight, lookAhead);

            if (shakeTimer > 0f)
            {
                shakeTimer -= dt;
                position += Random.insideUnitSphere * (shakeAmplitude * Mathf.Clamp01(shakeTimer / shakeDuration));
            }

            Quaternion rotation = Quaternion.LookRotation(lookAt - position, Vector3.up);

            // Menu: slow cinematic orbit around the skier, then a smooth blend into the follow view.
            bool menu = InMenu;
            menuBlend = menu ? 1f : Mathf.MoveTowards(menuBlend, 0f, dt / Mathf.Max(0.01f, menuBlendTime));
            if (menuBlend > 0f)
            {
                orbitAngle += orbitSpeed * Time.unscaledDeltaTime;
                Vector3 pivot = t + Vector3.up * 1.0f;
                Vector3 orbitPos = pivot + Quaternion.Euler(8f, orbitAngle, 0f) * new Vector3(0f, orbitHeight - 1f, -orbitDistance);
                Quaternion orbitRot = Quaternion.LookRotation(pivot - orbitPos, Vector3.up);
                float k = Mathf.SmoothStep(0f, 1f, menuBlend);
                position = Vector3.Lerp(position, orbitPos, k);
                rotation = Quaternion.Slerp(rotation, orbitRot, k);
            }

            transform.SetPositionAndRotation(position, rotation);

            if (cam != null && player != null)
            {
                float targetFov = Mathf.Lerp(baseFov, maxSpeedFov, player.Speed01);
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, 1f - Mathf.Exp(-fovSharpness * dt));
            }
        }
    }
}
