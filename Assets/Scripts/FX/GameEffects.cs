using UnityEngine;

namespace SkiGame
{
    /// <summary>
    /// World & event particle effects, all driven by GameEvents (no coupling to gameplay):
    ///  - ambient snowfall around the camera
    ///  - speed lines that appear at high speed
    ///  - powder puff on landing, big snow explosion on a crash
    ///  - golden sparkles when a coin is collected, a starburst on a clean backflip
    /// </summary>
    public class GameEffects : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private VisualSettings visualSettings;

        [Header("Speed lines")]
        [SerializeField] private bool speedLines = true;
        [Tooltip("Normalised speed at which speed lines start.")]
        [SerializeField, Range(0f, 1f)] private float speedLineThreshold = 0.5f;
        [SerializeField] private float speedLineMaxRate = 70f;

        [Header("Bursts")]
        [SerializeField] private Color powderColor = new Color(1f, 1f, 1f, 0.9f);
        [SerializeField] private Color sparkleColor = new Color(1f, 0.82f, 0.3f, 1f);
        [SerializeField] private Color trickColor = new Color(1f, 0.75f, 0.25f, 1f);
        [SerializeField] private int landingPuff = 28;
        [SerializeField] private int crashBurst = 120;
        [SerializeField] private int coinSparkles = 18;
        [SerializeField] private int backflipStars = 60;

        private ParticleSystem snowfall, speedStreaks, powder, sparkles, stars;

        private void Awake()
        {
            if (player == null) player = FindAnyObjectByType<PlayerController>();
            if (targetCamera == null) targetCamera = Camera.main;
            if (visualSettings == null) visualSettings = ScriptableObject.CreateInstance<VisualSettings>();
            BuildSystems();
        }

        private void OnEnable()
        {
            GameEvents.Landed += OnLanded;
            GameEvents.Crashed += OnCrashed;
            GameEvents.CoinPickedUpAt += OnCoin;
            GameEvents.BackflipLanded += OnBackflip;
        }

        private void OnDisable()
        {
            GameEvents.Landed -= OnLanded;
            GameEvents.Crashed -= OnCrashed;
            GameEvents.CoinPickedUpAt -= OnCoin;
            GameEvents.BackflipLanded -= OnBackflip;
        }

        private void BuildSystems()
        {
            Material soft = MaterialFactory.Particle();
            Material glow = MaterialFactory.ParticleAdditive(MaterialFactory.SoftDotTexture, 3f);
            Material streak = MaterialFactory.ParticleAdditive(MaterialFactory.SoftDotTexture, 1.2f);

            // --- Snowfall: emitter box floats above/ahead of the camera, flakes live in world space.
            if (visualSettings.snowfallRate > 0f)
            {
                snowfall = ParticleFactory.Create("Snowfall", transform, soft, 3000);
                var m = snowfall.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(6f, 8f);
                m.startSpeed = 0f;
                m.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
                m.startColor = new Color(1f, 1f, 1f, 0.9f);
                m.gravityModifier = 0.06f;
                var e = snowfall.emission;
                e.rateOverTime = visualSettings.snowfallRate;
                var sh = snowfall.shape;
                sh.shapeType = ParticleSystemShapeType.Box;
                sh.scale = new Vector3(70f, 1f, 110f);
                var vel = snowfall.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.World;
                vel.x = new ParticleSystem.MinMaxCurve(visualSettings.snowWind.x - 0.4f, visualSettings.snowWind.x + 0.4f);
                vel.y = new ParticleSystem.MinMaxCurve(-1.6f, -0.9f);
                vel.z = new ParticleSystem.MinMaxCurve(visualSettings.snowWind.z - 0.4f, visualSettings.snowWind.z + 0.4f);
                var noise = snowfall.noise;
                noise.enabled = true;
                noise.strength = 0.6f;
                noise.quality = ParticleSystemNoiseQuality.Low;
                noise.frequency = 0.25f;
                ParticleFactory.FadeOut(snowfall, 0.1f, 0.85f);
                snowfall.Play();
            }

            // --- Speed lines: local to the camera, rushing past.
            if (speedLines && targetCamera != null)
            {
                speedStreaks = ParticleFactory.Create("SpeedLines", targetCamera.transform, streak, 400, false);
                speedStreaks.transform.localPosition = new Vector3(0f, 0f, 30f);
                var m = speedStreaks.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.55f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(60f, 80f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
                m.startColor = new Color(1f, 1f, 1f, 0.35f);
                // Ring-shaped emitter: lines stream past the screen edges, the centre stays clear.
                var sh = speedStreaks.shape;
                sh.shapeType = ParticleSystemShapeType.Cone;
                sh.angle = 0f;
                sh.radius = 13f;
                sh.radiusThickness = 0.35f;
                speedStreaks.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // emit toward the camera
                var r = speedStreaks.GetComponent<ParticleSystemRenderer>();
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.velocityScale = 0.12f;
                r.lengthScale = 2f;
                ParticleFactory.FadeOut(speedStreaks, 0.2f, 0.6f);
                speedStreaks.Play();
            }

            // --- Powder: landing puffs and crash explosions.
            powder = ParticleFactory.Create("PowderBursts", transform, soft, 800);
            {
                var m = powder.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(2f, 7f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
                m.startColor = powderColor;
                m.gravityModifier = 0.35f;
                var sh = powder.shape;
                sh.shapeType = ParticleSystemShapeType.Hemisphere;
                sh.radius = 0.5f;
                powder.transform.rotation = Quaternion.Euler(-90f, 0f, 0f); // hemisphere opens upward
                var lim = powder.limitVelocityOverLifetime;
                lim.enabled = true;
                lim.drag = 2.5f;
                ParticleFactory.FadeOut(powder, 0.05f, 0.4f);
                ParticleFactory.SizeOverLife(powder, 0.6f, 1.8f);
                powder.Play();
            }

            // --- Sparkles: coins.
            sparkles = ParticleFactory.Create("CoinSparkles", transform, glow, 400);
            {
                var m = sparkles.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
                m.startColor = sparkleColor;
                m.gravityModifier = -0.1f;
                var sh = sparkles.shape;
                sh.shapeType = ParticleSystemShapeType.Sphere;
                sh.radius = 0.2f;
                ParticleFactory.FadeOut(sparkles, 0.02f, 0.3f);
                ParticleFactory.SizeOverLife(sparkles, 1f, 0.2f);
                sparkles.Play();
            }

            // --- Stars: clean backflip landing.
            stars = ParticleFactory.Create("TrickStars", transform, glow, 300);
            {
                var m = stars.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(4f, 10f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
                m.startColor = trickColor;
                m.gravityModifier = 0.4f;
                var sh = stars.shape;
                sh.shapeType = ParticleSystemShapeType.Sphere;
                sh.radius = 0.4f;
                ParticleFactory.FadeOut(stars, 0.02f, 0.4f);
                ParticleFactory.SizeOverLife(stars, 1f, 0.1f);
                stars.Play();
            }
        }

        private void LateUpdate()
        {
            if (targetCamera == null) return;
            Transform cam = targetCamera.transform;
            if (snowfall != null)
            {
                // Hover the emitter above and ahead of the camera so flakes are always around you.
                Vector3 fwd = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
                snowfall.transform.position = cam.position + Vector3.up * 16f + fwd * 40f;
            }
            if (speedStreaks != null && player != null)
            {
                float s = player.ControlsEnabled ? Mathf.InverseLerp(speedLineThreshold, 1f, player.Speed01) : 0f;
                var e = speedStreaks.emission;
                e.rateOverTime = speedLineMaxRate * s * s;
            }
        }

        private Vector3 PlayerFeet => player != null ? player.transform.position : Vector3.zero;

        private void OnLanded(bool cleanTrick) => ParticleFactory.Burst(powder, PlayerFeet, landingPuff);

        private void OnCrashed(string reason)
        {
            ParticleFactory.Burst(powder, PlayerFeet + Vector3.up * 0.3f, crashBurst);
        }

        private void OnCoin(Vector3 position) => ParticleFactory.Burst(sparkles, position, coinSparkles);

        private void OnBackflip(int flips, int multiplier, int points)
        {
            ParticleFactory.Burst(stars, PlayerFeet + Vector3.up * 1f, backflipStars * Mathf.Max(1, flips));
        }
    }
}
