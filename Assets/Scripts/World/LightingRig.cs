using UnityEngine;
using UnityEngine.Rendering;

namespace SkiGame
{
    /// <summary>
    /// Sets up the whole atmosphere from VisualSettings: warm low sun with soft shadows,
    /// a cool fill light, procedural sky with a sun disc, blue-ish trilight ambient,
    /// distance fog, shadow quality, and a ring of distant snow-capped mountains that
    /// follows the camera (always on the horizon, never reachable).
    /// Apply() also works in the editor (the scene builder calls it).
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class LightingRig : MonoBehaviour
    {
        [SerializeField] private VisualSettings settings;
        [SerializeField] private Light sun;
        [SerializeField] private Light fill;
        [SerializeField] private Camera targetCamera;
        [Tooltip("Optional skybox material asset (created by the scene builder so it's included in builds).")]
        [SerializeField] private Material skyboxMaterial;
        [Tooltip("Custom art-directed sky shader (SkiGame/Sky). Used when VisualSettings.customSky is on.")]
        [SerializeField] private Shader customSkyShader;
        private Material customSkyMaterial;

        private Transform backdrop;

        public VisualSettings Settings => settings;

        private void Awake()
        {
            if (settings == null) settings = ScriptableObject.CreateInstance<VisualSettings>();
            if (targetCamera == null) targetCamera = Camera.main;
            Apply();
            if (Application.isPlaying) BuildBackdrop();
        }

        /// <summary>Applies lights, sky, ambient, fog and quality settings.</summary>
        public void Apply()
        {
            VisualSettings s = settings;
            if (sun != null)
            {
                sun.type = LightType.Directional;
                sun.color = s.sunColor;
                sun.intensity = s.sunIntensity;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = s.shadowStrength;
                sun.shadowBias = 0.04f;
                sun.shadowNormalBias = 0.35f;
                sun.transform.rotation = Quaternion.Euler(s.sunElevation, s.sunHeading, 0f);
                RenderSettings.sun = sun;
            }
            if (fill != null)
            {
                fill.type = LightType.Directional;
                fill.color = s.fillColor;
                fill.intensity = s.fillIntensity;
                fill.shadows = LightShadows.None;
                fill.transform.rotation = Quaternion.Euler(s.fillElevation, s.fillHeading, 0f);
            }

            // Sky
            Shader customShader = customSkyShader != null ? customSkyShader : Shader.Find("SkiGame/Sky");
            if (s.proceduralSky && s.customSky && customShader != null)
            {
                if (customSkyMaterial == null) customSkyMaterial = new Material(customShader) { name = "SkiSky (custom)" };
                ConfigureCustomSky(customSkyMaterial, s);
                RenderSettings.skybox = customSkyMaterial;
            }
            else if (s.proceduralSky)
            {
                Material sky = skyboxMaterial;
                if (sky == null)
                {
                    Shader proc = Shader.Find("Skybox/Procedural");
                    if (proc != null) sky = new Material(proc) { name = "SkiSky (runtime)" };
                }
                if (sky != null)
                {
                    ConfigureSky(sky, s);
                    RenderSettings.skybox = sky;
                }
            }
            else
            {
                RenderSettings.skybox = null;
            }

            if (targetCamera != null)
            {
                bool hasSky = s.proceduralSky && RenderSettings.skybox != null;
                targetCamera.clearFlags = hasSky ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
                targetCamera.backgroundColor = s.fog ? s.fogColor : s.flatSkyColor;
                targetCamera.allowHDR = true;
                targetCamera.allowMSAA = true;
                targetCamera.farClipPlane = Mathf.Max(targetCamera.farClipPlane, Mathf.Max(s.mountainRadius.y * 1.6f, s.cloudRadius + 150f) + 500f);
            }

            // Ambient: trilight gives cool blue shadows and warm horizon bounce.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = s.ambientSky;
            RenderSettings.ambientEquatorColor = s.ambientEquator;
            RenderSettings.ambientGroundColor = s.ambientGround;

            // Fog
            RenderSettings.fog = s.fog;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = s.fogColor;
            RenderSettings.fogStartDistance = s.fogStart;
            RenderSettings.fogEndDistance = s.fogEnd;

            // Shadow / AA quality
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowDistance = s.shadowDistance;
            QualitySettings.shadowCascades = s.shadowCascades;
            QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
            QualitySettings.shadowProjection = ShadowProjection.StableFit;
            QualitySettings.antiAliasing = s.msaa >= 8 ? 8 : s.msaa >= 4 ? 4 : s.msaa >= 2 ? 2 : 0;
        }

        public static void ConfigureCustomSky(Material sky, VisualSettings s)
        {
            sky.SetColor("_ZenithColor", s.skyZenith);
            sky.SetColor("_MidColor", s.skyMid);
            sky.SetColor("_HorizonColor", s.skyHorizon);
            sky.SetColor("_HorizonSunColor", s.skyHorizonSun);
            sky.SetColor("_GroundColor", s.fogColor);
            sky.SetColor("_SunColor", s.skySunColor);
            sky.SetFloat("_SunSize", s.skySunSize);
            sky.SetFloat("_HaloIntensity", s.skyHaloIntensity);
            sky.SetFloat("_CirrusAmount", s.skyCirrus);
            sky.SetFloat("_Exposure", s.skyExposure);
        }

        public static void ConfigureSky(Material sky, VisualSettings s)
        {
            if (sky.HasProperty("_SunDisk")) sky.SetFloat("_SunDisk", 2f); // high quality
            if (sky.HasProperty("_SunSize")) sky.SetFloat("_SunSize", s.sunDiscSize);
            if (sky.HasProperty("_SunSizeConvergence")) sky.SetFloat("_SunSizeConvergence", 6f);
            if (sky.HasProperty("_AtmosphereThickness")) sky.SetFloat("_AtmosphereThickness", s.atmosphereThickness);
            if (sky.HasProperty("_SkyTint")) sky.SetColor("_SkyTint", s.skyTint);
            if (sky.HasProperty("_GroundColor")) sky.SetColor("_GroundColor", s.skyGroundColor);
            if (sky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", s.skyExposure);
        }

        // ------------------------------------------------------------------ mountains
        private void BuildBackdrop()
        {
            VisualSettings s = settings;
            if (!s.mountains || s.mountainCount <= 0) return;
            backdrop = new GameObject("Mountain Backdrop").transform;
            var meshes = new Mesh[8];
            for (int i = 0; i < meshes.Length; i++) meshes[i] = MeshFactory.CreateMountain(1000 + i * 17);
            var rng = new System.Random(4242);

            // Three layers for depth: far giants (very hazy), the main range, and a few near
            // darker ridges at the sides. Atmospheric perspective sells the scale.
            BuildMountainLayer("Far Range", rng, meshes, Mathf.RoundToInt(s.mountainCount * 0.8f),
                s.mountainRadius.y * 1.35f, s.mountainRadius.y * 1.6f, s.mountainHeight * 1.75f, s.farHaze, 360f, s.mountainSnowLine - 0.06f);
            BuildMountainLayer("Main Range", rng, meshes, s.mountainCount,
                s.mountainRadius.x, s.mountainRadius.y, s.mountainHeight, s.mountainHaze, 360f, s.mountainSnowLine);
            BuildMountainLayer("Near Ridges", rng, meshes, Mathf.RoundToInt(s.mountainCount * 0.45f),
                s.mountainRadius.x * 0.62f, s.mountainRadius.x * 0.78f, s.mountainHeight * 0.55f, s.nearHaze, 300f, s.mountainSnowLine + 0.08f, true);

            BuildClouds(rng);
            LateUpdate();
        }

        private void BuildMountainLayer(string name, System.Random rng, Mesh[] meshes, int count, float minRadius, float maxRadius,
            Vector2 height, float haze, float arc, float snowLine, bool avoidFront = false)
        {
            VisualSettings s = settings;
            Material mat = MaterialFactory.Backdrop(s.mountainRock, s.mountainRockHigh, s.mountainSnow, s.fogColor, snowLine, haze);
            if (mat == null || count <= 0) return;
            mat.name = "Ski_Mountains_" + name;
            if (mat.HasProperty("_SunLitColor")) mat.SetColor("_SunLitColor", s.mountainSunLit);
            if (mat.HasProperty("_ShadowColor")) mat.SetColor("_ShadowColor", s.mountainShadow);
            if (mat.HasProperty("_AlpenglowColor")) mat.SetColor("_AlpenglowColor", s.mountainAlpenglow);
            if (mat.HasProperty("_HazeSunColor")) mat.SetColor("_HazeSunColor", s.skyHorizonSun);
            if (mat.HasProperty("_NoiseTex")) mat.SetTexture("_NoiseTex", MaterialFactory.SnowTexture);

            var layer = new GameObject(name).transform;
            layer.SetParent(backdrop, false);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            for (int i = 0; i < count; i++)
            {
                float angle = (i + R(-0.4f, 0.4f)) / count * arc - arc * 0.5f;
                // Near ridges frame the view at the sides instead of blocking the horizon ahead.
                if (avoidFront && Mathf.Abs(Mathf.DeltaAngle(angle, 0f)) < 28f) angle += angle >= 0f ? 32f : -32f;
                float radius = R(minRadius, maxRadius);
                float h = R(height.x, height.y);
                float w = h * R(1.5f, 2.4f);
                var go = new GameObject("Mountain", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(layer, false);
                go.transform.localPosition = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;
                go.transform.localRotation = Quaternion.Euler(0f, R(0f, 360f), 0f);
                go.transform.localScale = new Vector3(w, h, w * R(0.8f, 1.2f));
                go.GetComponent<MeshFilter>().sharedMesh = meshes[rng.Next(meshes.Length)];
                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
        }

        private void BuildClouds(System.Random rng)
        {
            VisualSettings s = settings;
            if (!s.clouds || s.cloudCount <= 0) return;
            var textures = new Texture2D[3];
            var materials = new Material[3];
            for (int i = 0; i < textures.Length; i++)
            {
                textures[i] = MaterialFactory.CloudTexture(i + 1);
                materials[i] = MaterialFactory.Cloud(textures[i], s.cloudOpacity);
                if (materials[i] == null) return;
            }
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            for (int i = 0; i < s.cloudCount; i++)
            {
                // Mostly in the forward half of the sky, where the player looks.
                float angle = i < s.cloudCount * 0.7f ? R(-75f, 75f) : R(0f, 360f);
                float width = R(s.cloudWidth.x, s.cloudWidth.y);
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = "Cloud";
                Collider c = go.GetComponent<Collider>();
                if (c != null) { c.enabled = false; Destroy(c); }
                go.transform.SetParent(backdrop, false);
                go.transform.localPosition = dir * (s.cloudRadius + R(-120f, 120f)) +
                                             Vector3.up * (s.mountainDrop + R(s.cloudHeight.x, s.cloudHeight.y));
                go.transform.localRotation = Quaternion.LookRotation(dir, Vector3.up);
                go.transform.localScale = new Vector3(width, width * R(0.3f, 0.42f), 1f);
                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = materials[i % materials.Length];
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
        }

        private void LateUpdate()
        {
            if (backdrop == null || targetCamera == null) return;
            Vector3 c = targetCamera.transform.position;
            backdrop.position = new Vector3(c.x, c.y - settings.mountainDrop, c.z);
        }
    }
}
