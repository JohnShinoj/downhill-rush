using UnityEngine;

namespace SkiGame
{
    /// <summary>Builds ParticleSystems in code with sensible defaults (no prefabs needed).</summary>
    public static class ParticleFactory
    {
        public static ParticleSystem Create(string name, Transform parent, Material material, int maxParticles = 500,
            bool worldSpace = true)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.maxParticles = maxParticles;
            main.simulationSpace = worldSpace ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            return ps;
        }

        /// <summary>Colour over lifetime that fades alpha in quickly and out at the end.</summary>
        public static void FadeOut(ParticleSystem ps, float fadeInEnd = 0.08f, float fadeOutStart = 0.55f)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, fadeInEnd), new GradientAlphaKey(1f, fadeOutStart), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
        }

        public static void SizeOverLife(ParticleSystem ps, float start, float end)
        {
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, start, 1f, end));
        }

        /// <summary>Emits a burst at a world position (shape still adds direction/spread).</summary>
        public static void Burst(ParticleSystem ps, Vector3 position, int count)
        {
            if (ps == null || count <= 0) return;
            var ep = new ParticleSystem.EmitParams { position = position, applyShapeToPosition = true };
            ps.Emit(ep, count);
        }
    }
}
