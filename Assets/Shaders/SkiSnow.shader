// Snow surface: Standard (PBR) lighting + ripple normal map + view-dependent glitter.
// Glitter = two noise layers multiplied; one layer shifts with the view direction so
// sparkles twinkle as you move. They are strongest when looking toward the sun and are
// emitted in HDR, so the bloom turns them into tiny glints. A cool tint is added in
// grazing / shadowed areas to fake light scattering inside the snow.
Shader "SkiGame/Snow"
{
    Properties
    {
        _Color ("Tint", Color) = (0.95, 0.97, 1, 1)
        _MainTex ("Albedo", 2D) = "white" {}
        _BumpMap ("Normal", 2D) = "bump" {}
        _BumpScale ("Normal strength", Float) = 1.2
        _Glossiness ("Smoothness", Range(0, 1)) = 0.42
        _SparkleTex ("Sparkle noise", 2D) = "black" {}
        _SparkleScale ("Sparkle density", Float) = 0.18
        _SparkleThreshold ("Sparkle threshold", Range(0, 1)) = 0.95
        _SparkleIntensity ("Sparkle intensity (HDR)", Float) = 6
        _ScatterColor ("Subsurface tint", Color) = (0.55, 0.7, 1, 1)
        _ScatterStrength ("Subsurface strength", Range(0, 1)) = 0.18
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex, _BumpMap, _SparkleTex;
        fixed4 _Color, _ScatterColor;
        half _BumpScale, _Glossiness, _SparkleScale, _SparkleThreshold, _SparkleIntensity, _ScatterStrength;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
        };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Normal = UnpackScaleNormal(tex2D(_BumpMap, IN.uv_MainTex), _BumpScale);
            o.Smoothness = _Glossiness;
            o.Metallic = 0;

            float3 v = normalize(_WorldSpaceCameraPos - IN.worldPos);
            float3 l = normalize(_WorldSpaceLightPos0.xyz);

            // Glitter
            float2 suv = IN.worldPos.xz * _SparkleScale;
            float n1 = tex2D(_SparkleTex, suv).r;
            float n2 = tex2D(_SparkleTex, suv * 0.73 + v.xz * 0.35).g;
            float glitter = saturate((n1 * n2 - _SparkleThreshold * _SparkleThreshold) * 40.0);
            float towardSun = pow(saturate(dot(-v, l) * 0.5 + 0.5), 3.0);         // brighter looking into the sun
            float distFade = saturate(1.0 - length(_WorldSpaceCameraPos - IN.worldPos) / 60.0);
            o.Emission = glitter * _SparkleIntensity * (0.25 + towardSun) * distFade;

            // Fake subsurface scattering: cool glow on grazing angles.
            float rim = pow(1.0 - saturate(v.y), 2.0);
            o.Emission += _ScatterColor.rgb * _ScatterStrength * rim * 0.5;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
