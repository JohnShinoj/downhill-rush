using UnityEngine;

namespace SkiGame
{
    /// <summary>
    /// Lightweight post-processing for the built-in pipeline (no packages):
    /// HDR bloom, ACES filmic tonemapping, warm/cool split-tone grading, saturation,
    /// contrast, vignette (stronger at high speed) and fine film grain.
    /// Uses the "Hidden/SkiGame/PostFX" shader (Assets/Shaders/SkiPostFX.shader).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class SkiPostEffects : MonoBehaviour
    {
        [SerializeField] private Shader shader;
        [SerializeField] private PlayerController player;

        [Header("Bloom")]
        [SerializeField] private bool bloom = true;
        [SerializeField, Min(0f)] private float bloomThreshold = 1.6f;
        [SerializeField, Range(0f, 1f)] private float bloomSoftKnee = 0.5f;
        [SerializeField, Range(0f, 3f)] private float bloomIntensity = 0.32f;
        [SerializeField] private Color bloomTint = new Color(1f, 0.93f, 0.85f);
        [SerializeField, Range(1, 8)] private int bloomIterations = 6;

        [Header("Sun shafts (god rays)")]
        [SerializeField] private bool sunShafts = true;
        [SerializeField] private Color shaftColor = new Color(1f, 0.86f, 0.62f);
        [SerializeField, Range(0f, 3f)] private float shaftIntensity = 0.5f;
        [Tooltip("Only sky brighter than this produces rays.")]
        [SerializeField, Range(0f, 2f)] private float shaftThreshold = 0.55f;
        [SerializeField, Range(0.1f, 1f)] private float shaftLength = 0.5f;
        [SerializeField, Range(0.8f, 1f)] private float shaftDecay = 0.965f;

        [Header("Tone & colour")]
        [SerializeField, Range(0.2f, 3f)] private float exposure = 0.92f;
        [SerializeField, Range(0.5f, 1.6f)] private float contrast = 1.2f;
        [SerializeField, Range(0f, 2f)] private float saturation = 1.28f;
        [Tooltip("Multiplies dark tones (cool blue shadows).")]
        [SerializeField] private Color shadowTint = new Color(0.9f, 0.97f, 1.1f);
        [Tooltip("Multiplies bright tones (warm sunlight).")]
        [SerializeField] private Color highlightTint = new Color(1.06f, 1.01f, 0.94f);

        [Header("Vignette & grain")]
        [SerializeField, Range(0f, 1f)] private float vignette = 0.35f;
        [Tooltip("Extra vignette at top speed (tunnel feeling).")]
        [SerializeField, Range(0f, 1f)] private float speedVignette = 0.2f;
        [SerializeField, Range(0f, 0.1f)] private float grain = 0.018f;

        private Material material;
        private readonly RenderTexture[] levels = new RenderTexture[8];

        private void Awake()
        {
            if (player == null) player = FindAnyObjectByType<PlayerController>();
            cam = GetComponent<Camera>();
            cam.allowHDR = true;
            if (sunShafts) cam.depthTextureMode |= DepthTextureMode.Depth;
        }

        private Camera cam;

        /// <summary>Sun position on screen (uv) and how visible the rays should be.</summary>
        private Vector4 SunScreenPosition()
        {
            Light sun = RenderSettings.sun;
            if (sun == null || cam == null) return Vector4.zero;
            Vector3 toSun = -sun.transform.forward;
            Vector3 vp = cam.WorldToViewportPoint(cam.transform.position + toSun * 1000f);
            if (vp.z <= 0f) return Vector4.zero;
            float edge = Mathf.Max(Mathf.Abs(vp.x - 0.5f), Mathf.Abs(vp.y - 0.5f));
            float visibility = Mathf.Clamp01(1f - (edge - 0.5f) / 0.7f);
            return new Vector4(vp.x, vp.y, visibility, 0f);
        }

        private bool EnsureMaterial()
        {
            if (material != null) return true;
            if (shader == null) shader = Shader.Find("Hidden/SkiGame/PostFX");
            if (shader == null || !shader.isSupported) return false;
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            return true;
        }

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (!EnsureMaterial())
            {
                Graphics.Blit(source, destination);
                return;
            }

            float speed01 = player != null && player.ControlsEnabled ? player.Speed01 : 0f;
            material.SetFloat("_Threshold", bloomThreshold);
            material.SetFloat("_Knee", Mathf.Max(0.0001f, bloomThreshold * bloomSoftKnee));
            material.SetFloat("_BloomIntensity", bloom ? bloomIntensity : 0f);
            material.SetColor("_BloomTint", bloomTint);
            material.SetFloat("_Exposure", exposure);
            material.SetFloat("_Contrast", contrast);
            material.SetFloat("_Saturation", saturation);
            material.SetColor("_ShadowTint", shadowTint);
            material.SetColor("_HighlightTint", highlightTint);
            material.SetFloat("_Vignette", Mathf.Clamp01(vignette + speedVignette * speed01 * speed01));
            material.SetFloat("_Grain", grain);

            RenderTexture bloomResult = null;
            int count = 0;
            if (bloom && bloomIntensity > 0f)
            {
                RenderTextureFormat format = source.format;
                int w = source.width / 2, h = source.height / 2;
                RenderTexture current = levels[0] = RenderTexture.GetTemporary(w, h, 0, format);
                Graphics.Blit(source, current, material, 0);
                count = 1;
                for (int i = 1; i < bloomIterations; i++)
                {
                    w /= 2; h /= 2;
                    if (w < 4 || h < 4) break;
                    levels[i] = RenderTexture.GetTemporary(w, h, 0, format);
                    Graphics.Blit(current, levels[i], material, 1);
                    current = levels[i];
                    count++;
                }
                for (int i = count - 2; i >= 0; i--)
                {
                    Graphics.Blit(current, levels[i], material, 2); // additive upsample
                    current = levels[i];
                }
                bloomResult = current;
            }

            material.SetTexture("_BloomTex", bloomResult != null ? (Texture)bloomResult : Texture2D.blackTexture);

            // God rays: mask bright sky around the sun, then blur radially toward it (2 passes).
            RenderTexture shaftA = null, shaftB = null;
            Vector4 sunPos = SunScreenPosition();
            if (sunShafts && sunPos.z > 0.001f)
            {
                material.SetVector("_SunPos", sunPos);
                material.SetColor("_ShaftColor", shaftColor);
                material.SetFloat("_ShaftIntensity", shaftIntensity);
                material.SetFloat("_ShaftThreshold", shaftThreshold);
                material.SetFloat("_ShaftDecay", shaftDecay);
                int sw = source.width / 3, sh = source.height / 3;
                shaftA = RenderTexture.GetTemporary(sw, sh, 0, source.format);
                shaftB = RenderTexture.GetTemporary(sw, sh, 0, source.format);
                Graphics.Blit(source, shaftA, material, 4);
                material.SetFloat("_ShaftLength", shaftLength);
                Graphics.Blit(shaftA, shaftB, material, 5);
                material.SetFloat("_ShaftLength", shaftLength * 0.35f);
                Graphics.Blit(shaftB, shaftA, material, 5);
                material.SetTexture("_ShaftTex", shaftA);
            }
            else
            {
                material.SetVector("_SunPos", Vector4.zero);
                material.SetTexture("_ShaftTex", Texture2D.blackTexture);
            }

            Graphics.Blit(source, destination, material, 3);
            if (shaftA != null) RenderTexture.ReleaseTemporary(shaftA);
            if (shaftB != null) RenderTexture.ReleaseTemporary(shaftB);

            for (int i = 0; i < count; i++)
            {
                RenderTexture.ReleaseTemporary(levels[i]);
                levels[i] = null;
            }
        }
    }
}
