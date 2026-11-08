using UnityEngine;

namespace SkiGame
{
    /// <summary>
    /// Holds references to the custom shaders so Unity includes them in builds, and
    /// registers them with the MaterialFactory before anything else creates materials.
    /// (The scene builder fills these fields in automatically.)
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class ShaderLibrary : MonoBehaviour
    {
        [SerializeField] private Shader particleAlpha;
        [SerializeField] private Shader particleAdditive;
        [SerializeField] private Shader backdrop;
        [SerializeField] private Shader snow;
        [SerializeField] private Shader cloud;

        private void Awake()
        {
            if (particleAlpha != null) MaterialFactory.ParticleAlphaShader = particleAlpha;
            if (particleAdditive != null) MaterialFactory.ParticleAdditiveShader = particleAdditive;
            if (backdrop != null) MaterialFactory.BackdropShader = backdrop;
            if (snow != null) MaterialFactory.SnowShader = snow;
            if (cloud != null) MaterialFactory.CloudShader = cloud;
        }
    }
}
