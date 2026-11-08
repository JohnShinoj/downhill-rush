using UnityEngine;

namespace SkiGame
{
    /// <summary>
    /// Snow spray from the ski tails. Builds its own ParticleSystem in code.
    /// The owning visual calls SetIntensity(0..1) every frame (0 while airborne).
    /// Reusable by any replacement character.
    /// </summary>
    public class SkiSnowSpray : MonoBehaviour
    {
        [SerializeField] private Color color = new Color(1f, 1f, 1f, 0.85f);
        [SerializeField, Min(0f)] private float maxRate = 230f;
        [SerializeField] private Vector2 particleSize = new Vector2(0.08f, 0.3f);
        [SerializeField] private Vector2 particleSpeed = new Vector2(1.8f, 5.5f);
        [SerializeField] private Vector2 particleLifetime = new Vector2(0.4f, 0.9f);

        private ParticleSystem system;

        private void Awake() => CreateSystem();

        /// <summary>Changes colour and maximum emission rate (used by the visual's Inspector colours).</summary>
        public void Configure(Color sprayColor, float maxEmissionRate)
        {
            color = sprayColor;
            maxRate = maxEmissionRate;
            if (system == null) return;
            var main = system.main;
            main.startColor = color;
        }

        /// <summary>0 = no spray, 1 = maximum spray.</summary>
        public void SetIntensity(float intensity)
        {
            if (system == null) return;
            var emission = system.emission;
            emission.rateOverTime = maxRate * Mathf.Clamp01(intensity);
        }

        private void CreateSystem()
        {
            system = gameObject.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = system.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(particleLifetime.x, particleLifetime.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(particleSpeed.x, particleSpeed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(particleSize.x, particleSize.y);
            main.startColor = color;
            main.gravityModifier = 0.8f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 800;

            var emission = system.emission;
            emission.rateOverTime = 0f;

            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 28f;
            shape.radius = 0.22f;
            // Emit backwards and slightly up from the ski tails.
            transform.localRotation = Quaternion.Euler(-22f, 180f, 0f);

            var fade = system.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            fade.color = new ParticleSystem.MinMaxGradient(gradient);

            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.5f));

            var psRenderer = GetComponent<ParticleSystemRenderer>();
            psRenderer.sharedMaterial = MaterialFactory.Particle();
            psRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            psRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            system.Play();
        }
    }
}
