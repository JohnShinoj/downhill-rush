// Art-directed golden-hour skybox:
//  - deep blue zenith -> soft blue -> luminous horizon that turns warm peach toward the sun
//  - HDR sun disc + wide warm halo (bloom + god rays pick it up)
//  - faint high-altitude cirrus streaks, horizon glow band
//  - below-horizon colour that matches the fog, so the world melts into the sky
Shader "SkiGame/Sky"
{
    Properties
    {
        _ZenithColor ("Zenith", Color) = (0.1, 0.27, 0.6, 1)
        _MidColor ("Mid sky", Color) = (0.36, 0.56, 0.86, 1)
        _HorizonColor ("Horizon (away from sun)", Color) = (0.72, 0.8, 0.92, 1)
        _HorizonSunColor ("Horizon (toward sun)", Color) = (1, 0.76, 0.54, 1)
        _GroundColor ("Below horizon", Color) = (0.72, 0.8, 0.91, 1)
        _SunColor ("Sun", Color) = (1, 0.9, 0.72, 1)
        _SunSize ("Sun size", Range(0.005, 0.1)) = 0.028
        _SunIntensity ("Sun disc intensity (HDR)", Float) = 9
        _HaloIntensity ("Halo intensity", Float) = 0.75
        _HaloPower ("Halo tightness", Float) = 7
        _HorizonGlow ("Horizon glow", Range(0, 2)) = 0.6
        _CirrusAmount ("Cirrus", Range(0, 1)) = 0.35
        _Exposure ("Exposure", Float) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _ZenithColor, _MidColor, _HorizonColor, _HorizonSunColor, _GroundColor, _SunColor;
            float _SunSize, _SunIntensity, _HaloIntensity, _HaloPower, _HorizonGlow, _CirrusAmount, _Exposure;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3 - 2 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), u.x), lerp(hash(i + float2(0, 1)), hash(i + 1), u.x), u.y);
            }
            float fbm(float2 p)
            {
                float v = 0, a = 0.5;
                for (int k = 0; k < 5; k++) { v += vnoise(p) * a; p *= 2.03; a *= 0.5; }
                return v;
            }

            half4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float3 s = normalize(_WorldSpaceLightPos0.xyz);
                float y = d.y;
                float sunDot = dot(d, s);

                // Horizon colour bends warm toward the sun.
                float sunSide = pow(saturate(sunDot * 0.5 + 0.5), 4);
                float3 horizon = lerp(_HorizonColor.rgb, _HorizonSunColor.rgb, sunSide);

                float up = saturate(y);
                float3 sky = lerp(horizon, _MidColor.rgb, smoothstep(0.0, 0.32, up));
                sky = lerp(sky, _ZenithColor.rgb, smoothstep(0.25, 0.95, up));

                // Thin bright band right at the horizon.
                sky += horizon * _HorizonGlow * exp(-abs(y) * 22) * 0.6;

                // High cirrus streaks (stretched noise projected on the sky dome).
                if (y > 0)
                {
                    float2 cp = d.xz / (y + 0.15);
                    float c = fbm(cp * float2(0.9, 3.5) + 3.7);
                    c = smoothstep(0.55, 0.9, c) * smoothstep(0.02, 0.25, y) * (1 - smoothstep(0.6, 1.0, y));
                    float3 cirrusCol = lerp(float3(1, 1, 1), _HorizonSunColor.rgb * 1.2, sunSide);
                    sky = lerp(sky, cirrusCol, c * _CirrusAmount);
                }

                // Below the horizon: match the fog colour.
                sky = lerp(sky, _GroundColor.rgb, smoothstep(0.0, -0.08, y));

                // Sun halo + HDR disc.
                float halo = pow(saturate(sunDot), _HaloPower) * _HaloIntensity + pow(saturate(sunDot), 90) * 0.8;
                float disc = smoothstep(cos(_SunSize * 1.15), cos(_SunSize), sunDot);
                sky += _SunColor.rgb * halo + _SunColor.rgb * disc * _SunIntensity;

                return half4(sky * _Exposure, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
