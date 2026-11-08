using System.Collections.Generic;
using UnityEngine;

namespace SkiGame
{
    /// <summary>
    /// Creates and caches all runtime materials and procedural textures:
    /// lit (Standard) materials, emissive "glow" materials, snow with a procedural
    /// albedo + normal map, particle materials and the mountain backdrop material.
    /// No external assets needed. Custom shaders are registered by ShaderLibrary
    /// (so they are included in builds) and otherwise found by name.
    /// </summary>
    public static class MaterialFactory
    {
        private static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();
        private static Shader litShader;
        private static Texture2D snowAlbedo, snowNormal, softDot, streak;

        // Optional shader overrides registered by ShaderLibrary (scene references => included in builds).
        public static Shader ParticleAlphaShader, ParticleAdditiveShader, BackdropShader, SnowShader, CloudShader;

        private static Shader LitShader
        {
            get
            {
                if (litShader != null) return litShader;
                litShader = Shader.Find("Standard");
                if (litShader == null)
                {
                    GameObject tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    litShader = tmp.GetComponent<Renderer>().sharedMaterial.shader;
                    PrimitiveBuilder.SafeDestroy(tmp);
                }
                return litShader;
            }
        }

        private static Shader Resolve(Shader preferred, string name, string fallbackName)
        {
            if (preferred != null) return preferred;
            Shader s = Shader.Find(name);
            if (s == null && fallbackName != null) s = Shader.Find(fallbackName);
            return s;
        }

        // ------------------------------------------------------------------ lit
        /// <summary>Opaque lit material with the given colour (cached, shared).</summary>
        public static Material Lit(Color color, float smoothness = 0.15f, float metallic = 0f)
        {
            string key = "LIT_" + ColorUtility.ToHtmlStringRGBA(color) + "_" + smoothness.ToString("F2") + "_" + metallic.ToString("F2");
            if (Cache.TryGetValue(key, out Material m) && m != null) return m;
            m = new Material(LitShader) { name = "Ski_" + key, color = color, enableInstancing = true };
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            Cache[key] = m;
            return m;
        }

        /// <summary>Lit material that also emits light (glows under bloom).</summary>
        public static Material Emissive(Color color, Color emission, float smoothness = 0.5f, float metallic = 0f)
        {
            string key = "EMI_" + ColorUtility.ToHtmlStringRGBA(color) + "_" + emission.r.ToString("F2") + emission.g.ToString("F2") +
                         emission.b.ToString("F2") + "_" + smoothness.ToString("F2") + metallic.ToString("F2");
            if (Cache.TryGetValue(key, out Material m) && m != null) return m;
            m = new Material(LitShader) { name = "Ski_Emissive", color = color, enableInstancing = true };
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            Cache[key] = m;
            return m;
        }

        // ------------------------------------------------------------------ snow
        /// <summary>Snow: procedural albedo variation + ripple normal map + sun glints.</summary>
        public static Material Snow(Color tint, float smoothness = 0.42f, float normalStrength = 0.8f)
        {
            string key = "SNOW_" + ColorUtility.ToHtmlStringRGBA(tint) + smoothness.ToString("F2") + normalStrength.ToString("F2");
            if (Cache.TryGetValue(key, out Material m) && m != null) return m;
            BuildSnowTextures();
            Shader snowShader = Resolve(SnowShader, "SkiGame/Snow", null);
            m = new Material(snowShader != null ? snowShader : LitShader) { name = "Ski_Snow", color = tint, mainTexture = snowAlbedo };
            if (m.HasProperty("_SparkleTex")) m.SetTexture("_SparkleTex", SparkleNoise);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (normalStrength > 0f && m.HasProperty("_BumpMap"))
            {
                m.SetTexture("_BumpMap", snowNormal);
                m.SetFloat("_BumpScale", normalStrength);
                m.EnableKeyword("_NORMALMAP");
            }
            Cache[key] = m;
            return m;
        }

        private static Texture2D sparkleNoise;

        /// <summary>Random noise (independent R and G) used for snow glitter.</summary>
        public static Texture2D SparkleNoise
        {
            get
            {
                if (sparkleNoise != null) return sparkleNoise;
                const int size = 256;
                sparkleNoise = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
                    { name = "SparkleNoise", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
                var px = new Color32[size * size];
                var rng = new System.Random(99);
                for (int i = 0; i < px.Length; i++)
                    px[i] = new Color32((byte)rng.Next(256), (byte)rng.Next(256), 0, 255);
                sparkleNoise.SetPixels32(px);
                sparkleNoise.Apply(false);
                return sparkleNoise;
            }
        }

        public static Texture2D SnowTexture { get { BuildSnowTextures(); return snowAlbedo; } }

        private static void BuildSnowTextures()
        {
            if (snowAlbedo != null && snowNormal != null) return;
            const int size = 256;
            var height = new float[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;
                    // Wind ripples (stretched across the slope) + soft lumps.
                    float ripples = TileableNoise(u * 1f, v * 1f, 22f, 6f);
                    float lumps = TileableNoise(u, v, 5f, 5f);
                    height[y * size + x] = ripples * 0.55f + lumps * 0.45f;
                }

            var albedo = new Color32[size * size];
            var normal = new Color32[size * size];
            var rng = new System.Random(7);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int i = y * size + x;
                    float h = height[i];
                    float shade = 0.8f + 0.2f * h;
                    if (rng.NextDouble() < 0.0025) shade = 1f;
                    byte b = (byte)(Mathf.Clamp01(shade) * 255f);
                    albedo[i] = new Color32(b, b, b, 255);

                    float hl = height[y * size + (x - 1 + size) % size];
                    float hr = height[y * size + (x + 1) % size];
                    float hd = height[((y - 1 + size) % size) * size + x];
                    float hu = height[((y + 1) % size) * size + x];
                    var n = new Vector3((hl - hr) * 3f, (hd - hu) * 3f, 1f).normalized;
                    // Works for both RGB and DXT5nm (UnpackNormal multiplies x by alpha).
                    normal[i] = new Color32((byte)((n.x * 0.5f + 0.5f) * 255f), (byte)((n.y * 0.5f + 0.5f) * 255f),
                        (byte)((n.z * 0.5f + 0.5f) * 255f), 255);
                }

            snowAlbedo = new Texture2D(size, size, TextureFormat.RGBA32, true)
                { name = "SnowAlbedo", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
            snowAlbedo.SetPixels32(albedo);
            snowAlbedo.Apply(true);

            snowNormal = new Texture2D(size, size, TextureFormat.RGBA32, true, true)
                { name = "SnowNormal", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
            snowNormal.SetPixels32(normal);
            snowNormal.Apply(true);
        }

        /// <summary>Perlin noise that tiles over 0..1 (separate x/y frequencies).</summary>
        private static float TileableNoise(float u, float v, float fx, float fy)
        {
            float a = Mathf.PerlinNoise(u * fx, v * fy);
            float b = Mathf.PerlinNoise((u - 1f) * fx, v * fy);
            float c = Mathf.PerlinNoise(u * fx, (v - 1f) * fy);
            float d = Mathf.PerlinNoise((u - 1f) * fx, (v - 1f) * fy);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        // ------------------------------------------------------------------ particles
        /// <summary>Soft alpha-blended particle material (snow, powder).</summary>
        public static Material Particle() => ParticleAlpha(SoftDotTexture);

        public static Material ParticleAlpha(Texture2D texture)
        {
            string key = "PA_" + texture.name;
            if (Cache.TryGetValue(key, out Material m) && m != null) return m;
            Shader s = Resolve(ParticleAlphaShader, "SkiGame/Particle Alpha", "Sprites/Default");
            m = new Material(s) { name = "Ski_ParticleAlpha", mainTexture = texture };
            Cache[key] = m;
            return m;
        }

        /// <summary>Additive glowing particle material (sparkles, trails). Intensity > 1 feeds the bloom.</summary>
        public static Material ParticleAdditive(Texture2D texture, float intensity = 2.5f)
        {
            string key = "PADD_" + texture.name + intensity.ToString("F1");
            if (Cache.TryGetValue(key, out Material m) && m != null) return m;
            Shader s = Resolve(ParticleAdditiveShader, "SkiGame/Particle Additive", "Sprites/Default");
            m = new Material(s) { name = "Ski_ParticleAdditive", mainTexture = texture };
            if (m.HasProperty("_Intensity")) m.SetFloat("_Intensity", intensity);
            Cache[key] = m;
            return m;
        }

        /// <summary>Plain alpha material for trails / ribbons.</summary>
        public static Material Trail(bool additive, float intensity = 2f)
        {
            return additive ? ParticleAdditive(SoftStripTexture, intensity) : ParticleAlpha(SoftStripTexture);
        }

        public static Texture2D SoftDotTexture
        {
            get
            {
                if (softDot != null) return softDot;
                const int size = 64;
                softDot = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "SoftDot", wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x + 0.5f) / size - 0.5f, dy = (y + 0.5f) / size - 0.5f;
                        float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 2f);
                        px[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
                    }
                softDot.SetPixels32(px);
                softDot.Apply(true);
                return softDot;
            }
        }

        /// <summary>Horizontal strip that fades at the top/bottom edges (for trails & speed lines).</summary>
        public static Texture2D SoftStripTexture
        {
            get
            {
                if (streak != null) return streak;
                const int w = 8, h = 32;
                streak = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "SoftStrip", wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[w * h];
                for (int y = 0; y < h; y++)
                {
                    float t = Mathf.Abs((y + 0.5f) / h - 0.5f) * 2f;
                    byte a = (byte)(Mathf.Clamp01(1f - t * t) * 255f);
                    for (int x = 0; x < w; x++) px[y * w + x] = new Color32(255, 255, 255, a);
                }
                streak.SetPixels32(px);
                streak.Apply();
                return streak;
            }
        }

        // ------------------------------------------------------------------ clouds
        /// <summary>Soft procedural cloud texture (fbm noise inside a puffy mask). R = density, A = alpha.</summary>
        public static Texture2D CloudTexture(int seed)
        {
            const int w = 256, h = 128;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "Cloud" + seed, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            float ox = seed * 13.7f, oy = seed * 7.3f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)w, v = y / (float)h;
                    float n = 0f, amp = 0.5f, freq = 3f;
                    for (int o = 0; o < 5; o++)
                    {
                        n += Mathf.PerlinNoise(ox + u * freq * 2f, oy + v * freq) * amp;
                        amp *= 0.5f; freq *= 2.1f;
                    }
                    // Puffy mask: wide ellipse, flatter bottom, rounder top.
                    float dx = (u - 0.5f) * 2f;
                    float dy = v < 0.35f ? (0.35f - v) * 4f : (v - 0.35f) * 1.6f;
                    float mask = Mathf.Clamp01(1f - (dx * dx + dy * dy));
                    float a = Mathf.Clamp01((n * mask - 0.22f) * 3.2f);
                    px[y * w + x] = new Color32((byte)(Mathf.Clamp01(n) * 255f), 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }

        public static Material Cloud(Texture2D texture, float alpha)
        {
            Shader s = Resolve(CloudShader, "SkiGame/Cloud", null);
            if (s == null) return null;
            var m = new Material(s) { name = "Ski_Cloud", mainTexture = texture };
            m.SetColor("_Color", new Color(1f, 1f, 1f, alpha));
            return m;
        }

        // ------------------------------------------------------------------ backdrop
        /// <summary>Material for the distant mountains (unaffected by fog). Null if the shader is missing.</summary>
        public static Material Backdrop(Color rock, Color rockTop, Color snow, Color haze, float snowLine, float hazeAmount)
        {
            Shader s = Resolve(BackdropShader, "SkiGame/Backdrop", null);
            if (s == null) return null;
            var m = new Material(s) { name = "Ski_Backdrop" };
            m.SetColor("_RockColor", rock);
            m.SetColor("_RockTopColor", rockTop);
            m.SetColor("_SnowColor", snow);
            m.SetColor("_HazeColor", haze);
            m.SetFloat("_SnowLine", snowLine);
            m.SetFloat("_Haze", hazeAmount);
            return m;
        }
    }
}
