// Distant alpine mountains (not fogged - they carry their own atmospheric haze):
//  - snow vs rock decided by height, steepness and noise (snow streaks down gullies, cliffs stay rocky)
//  - warm golden sun-lit faces, cool blue shadow faces (low-poly facets read beautifully)
//  - pink/orange alpenglow on sunlit snow
//  - haze that grows toward the base and with the layer's distance (_Haze), tinted warmer toward the sun
Shader "SkiGame/Backdrop"
{
    Properties
    {
        _RockColor ("Rock", Color) = (0.24, 0.27, 0.36, 1)
        _RockTopColor ("Rock (high)", Color) = (0.34, 0.36, 0.46, 1)
        _SnowColor ("Snow", Color) = (0.96, 0.97, 1, 1)
        _SunLitColor ("Sunlit tint", Color) = (1.15, 0.95, 0.78, 1)
        _ShadowColor ("Shadow tint", Color) = (0.42, 0.52, 0.78, 1)
        _AlpenglowColor ("Alpenglow", Color) = (1, 0.7, 0.62, 1)
        _HazeColor ("Haze", Color) = (0.72, 0.8, 0.92, 1)
        _HazeSunColor ("Haze toward sun", Color) = (1, 0.8, 0.62, 1)
        _SnowLine ("Snow line (0..1)", Range(0, 1)) = 0.42
        _Haze ("Haze amount (distance layer)", Range(0, 1)) = 0.35
        _NoiseTex ("Noise", 2D) = "gray" {}
        _NoiseScale ("Noise scale", Float) = 0.004
    }
    SubShader
    {
        Tags { "Queue" = "Geometry+50" "RenderType" = "Opaque" }
        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _RockColor, _RockTopColor, _SnowColor, _SunLitColor, _ShadowColor, _AlpenglowColor, _HazeColor, _HazeSunColor;
            float _SnowLine, _Haze, _NoiseScale;
            sampler2D _NoiseTex;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 pos : SV_POSITION; float h : TEXCOORD0; float3 wn : TEXCOORD1; float3 wpos : TEXCOORD2; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.h = v.vertex.y;
                o.wn = UnityObjectToWorldNormal(v.normal);
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.wn);
                float h = saturate(i.h);
                float3 l = normalize(_WorldSpaceLightPos0.xyz);
                float3 v = normalize(_WorldSpaceCameraPos - i.wpos);
                float ndl = dot(n, l);

                // Snow / rock mask.
                float noise = tex2D(_NoiseTex, i.wpos.xz * _NoiseScale + i.wpos.y * _NoiseScale * 0.5).r;
                float steep = 1 - saturate(n.y);
                float snowLevel = h + (noise - 0.5) * 0.4 - steep * 0.28;
                float snow = smoothstep(_SnowLine - 0.03, _SnowLine + 0.05, snowLevel);
                snow = max(snow, smoothstep(0.9, 0.97, h) * 0.85);          // summits always snowy

                float3 rock = lerp(_RockColor.rgb, _RockTopColor.rgb, h) * (0.85 + noise * 0.3);
                float3 albedo = lerp(rock, _SnowColor.rgb, snow);

                // Warm sun / cool shadow lighting on the facets.
                float lit = smoothstep(-0.15, 0.6, ndl);
                float3 light = lerp(_ShadowColor.rgb, _SunLitColor.rgb, lit);
                float3 c = albedo * light;

                // Alpenglow on sunlit snow (stronger high up).
                c = lerp(c, c * _AlpenglowColor.rgb * 1.08, snow * lit * smoothstep(0.3, 1.0, h) * 0.55);

                // Atmospheric haze: more at the base and for distant layers, warmer toward the sun.
                float sunSide = pow(saturate(dot(-v, l) * 0.5 + 0.5), 4);
                float3 haze = lerp(_HazeColor.rgb, _HazeSunColor.rgb, sunSide * 0.55);
                float hazeAmount = saturate(_Haze + (1 - h) * (1 - h) * (0.35 + _Haze * 0.6));
                c = lerp(c, haze, hazeAmount);
                return fixed4(c, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
