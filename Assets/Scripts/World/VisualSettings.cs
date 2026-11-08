using UnityEngine;

namespace SkiGame
{
    /// <summary>
    /// Look of the world: sun, fill light, procedural sky, ambient light, fog, shadows,
    /// distant mountains and snowfall. Assets > Create > Ski Game > Visual Settings.
    /// Default values give a cold, golden-hour alpine look.
    /// </summary>
    [CreateAssetMenu(menuName = "Ski Game/Visual Settings", fileName = "VisualSettings")]
    public class VisualSettings : ScriptableObject
    {
        [Header("Sun (key light)")]
        public Color sunColor = new Color(1f, 0.84f, 0.64f);
        [Range(0f, 3f)] public float sunIntensity = 1.15f;
        [Tooltip("Height of the sun above the horizon (degrees). Low = long dramatic shadows.")]
        [Range(5f, 90f)] public float sunElevation = 16f;
        [Tooltip("Compass direction the sunlight travels (degrees). ~170 = sun ahead, slightly backlit.")]
        public float sunHeading = 168f;
        [Range(0f, 1f)] public float shadowStrength = 0.82f;

        [Header("Fill light (cool, from the camera side)")]
        public Color fillColor = new Color(0.62f, 0.74f, 1f);
        [Range(0f, 2f)] public float fillIntensity = 0.28f;
        [Range(0f, 90f)] public float fillElevation = 32f;
        public float fillHeading = -12f;

        [Header("Sky")]
        public bool proceduralSky = true;
        [Tooltip("Use the art-directed SkiGame/Sky shader instead of Unity's procedural sky.")]
        public bool customSky = true;
        public Color skyZenith = new Color(0.07f, 0.2f, 0.52f);
        public Color skyMid = new Color(0.27f, 0.49f, 0.86f);
        public Color skyHorizon = new Color(0.74f, 0.82f, 0.94f);
        public Color skyHorizonSun = new Color(1f, 0.74f, 0.52f);
        public Color skySunColor = new Color(1f, 0.88f, 0.68f);
        [Range(0.005f, 0.1f)] public float skySunSize = 0.03f;
        [Range(0f, 3f)] public float skyHaloIntensity = 0.45f;
        [Range(0f, 1f)] public float skyCirrus = 0.4f;
        public Color skyTint = new Color(0.42f, 0.56f, 0.82f);
        public Color skyGroundColor = new Color(0.62f, 0.66f, 0.74f);
        [Range(0f, 5f)] public float atmosphereThickness = 0.8f;
        [Range(0f, 8f)] public float skyExposure = 0.95f;
        [Range(0f, 1f)] public float sunDiscSize = 0.035f;
        [Tooltip("Background colour when the procedural sky is off.")]
        public Color flatSkyColor = new Color(0.62f, 0.76f, 0.95f);

        [Header("Ambient light (shadows turn blue, like real snow)")]
        public Color ambientSky = new Color(0.42f, 0.52f, 0.76f);
        public Color ambientEquator = new Color(0.6f, 0.6f, 0.66f);
        public Color ambientGround = new Color(0.33f, 0.38f, 0.5f);

        [Header("Fog / atmosphere")]
        public bool fog = true;
        public Color fogColor = new Color(0.74f, 0.82f, 0.94f);
        public float fogStart = 75f;
        public float fogEnd = 270f;

        [Header("Shadows & quality")]
        public float shadowDistance = 90f;
        [Range(1, 4)] public int shadowCascades = 2;
        [Range(1, 8)] public int msaa = 2;

        [Header("Distant mountains")]
        public bool mountains = true;
        [Min(0)] public int mountainCount = 22;
        public Vector2 mountainRadius = new Vector2(750f, 1100f);
        public Vector2 mountainHeight = new Vector2(130f, 280f);
        [Tooltip("Mountain base sits this far below the camera.")]
        public float mountainDrop = 170f;
        public Color mountainRock = new Color(0.24f, 0.27f, 0.36f);
        public Color mountainRockHigh = new Color(0.34f, 0.36f, 0.46f);
        public Color mountainSnow = new Color(0.97f, 0.97f, 1f);
        [Range(0f, 1f)] public float mountainSnowLine = 0.34f;
        [Range(0f, 1f)] public float mountainHaze = 0.2f;
        [Range(0f, 1f)] public float nearHaze = 0.08f;
        [Range(0f, 1f)] public float farHaze = 0.5f;
        public Color mountainSunLit = new Color(1.15f, 0.95f, 0.78f);
        public Color mountainShadow = new Color(0.4f, 0.5f, 0.78f);
        public Color mountainAlpenglow = new Color(1f, 0.72f, 0.66f);

        [Header("Clouds")]
        public bool clouds = true;
        [Min(0)] public int cloudCount = 16;
        public float cloudRadius = 1050f;
        [Tooltip("Height of clouds above the camera (m).")]
        public Vector2 cloudHeight = new Vector2(110f, 300f);
        public Vector2 cloudWidth = new Vector2(320f, 620f);
        [Range(0f, 1f)] public float cloudOpacity = 0.85f;

        [Header("Weather")]
        [Tooltip("Ambient snowflakes per second around the camera. 0 = off.")]
        [Min(0f)] public float snowfallRate = 160f;
        [Tooltip("Sideways drift of falling snow (m/s).")]
        public Vector3 snowWind = new Vector3(1.2f, 0f, -0.6f);
    }
}
