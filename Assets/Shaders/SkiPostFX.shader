// Camera post-processing for the built-in render pipeline (used by SkiPostEffects.cs):
//   pass 0: bloom prefilter (soft threshold) + 2x downsample
//   pass 1: downsample (box filter)
//   pass 2: upsample (additive, tent-ish box filter)
//   pass 3: composite: bloom + exposure + ACES filmic tonemap + split-tone grading
//           + saturation/contrast + vignette + subtle film grain + sun shafts
//   pass 4: sun-shaft mask (bright sky only, via the depth texture, around the sun)
//   pass 5: radial blur toward the sun (god rays)
Shader "Hidden/SkiGame/PostFX"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _BloomTex ("Bloom", 2D) = "black" {}
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;
    sampler2D _BloomTex;

    float _Threshold;
    float _Knee;
    float _BloomIntensity;
    float _Exposure;
    float _Contrast;
    float _Saturation;
    float _Vignette;
    float _Grain;
    float4 _ShadowTint;
    float4 _HighlightTint;
    float4 _BloomTint;

    sampler2D_float _CameraDepthTexture;
    sampler2D _ShaftTex;
    float4 _SunPos;          // xy = sun position in uv, z = visibility 0..1
    float4 _ShaftColor;
    float _ShaftIntensity;
    float _ShaftThreshold;
    float _ShaftLength;
    float _ShaftDecay;

    half3 Box4(float2 uv, float delta)
    {
        float4 o = _MainTex_TexelSize.xyxy * float4(-delta, -delta, delta, delta);
        half3 s = tex2D(_MainTex, uv + o.xy).rgb + tex2D(_MainTex, uv + o.zy).rgb +
                  tex2D(_MainTex, uv + o.xw).rgb + tex2D(_MainTex, uv + o.zw).rgb;
        return s * 0.25h;
    }

    half3 Prefilter(half3 c)
    {
        half brightness = max(c.r, max(c.g, c.b));
        half soft = brightness - _Threshold + _Knee;
        soft = clamp(soft, 0, 2 * _Knee);
        soft = soft * soft / (4 * _Knee + 1e-4);
        half contribution = max(soft, brightness - _Threshold) / max(brightness, 1e-4);
        return c * contribution;
    }

    half3 ACESFilm(half3 x)
    {
        return saturate((x * (2.51h * x + 0.03h)) / (x * (2.43h * x + 0.59h) + 0.14h));
    }

    half4 FragPrefilter(v2f_img i) : SV_Target { return half4(Prefilter(Box4(i.uv, 1)), 1); }
    half4 FragDown(v2f_img i) : SV_Target { return half4(Box4(i.uv, 1), 1); }
    half4 FragUp(v2f_img i) : SV_Target { return half4(Box4(i.uv, 0.5), 1); }

    half4 FragShaftMask(v2f_img i) : SV_Target
    {
        float2 uvDepth = i.uv;
        #if UNITY_UV_STARTS_AT_TOP
        if (_MainTex_TexelSize.y < 0) uvDepth.y = 1 - uvDepth.y;
        #endif
        float depth = Linear01Depth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uvDepth));
        half sky = step(0.995, depth);
        half3 c = max(tex2D(_MainTex, i.uv).rgb - _ShaftThreshold, 0);
        float2 d = (i.uv - _SunPos.xy) * float2(_ScreenParams.x / _ScreenParams.y, 1.0);
        half fall = saturate(1.0 - length(d) * 1.4);
        return half4(c * sky * fall * fall, 1);
    }

    half4 FragShaftBlur(v2f_img i) : SV_Target
    {
        const int SAMPLES = 20;
        float2 delta = (_SunPos.xy - i.uv) * _ShaftLength / SAMPLES;
        float2 uv = i.uv;
        half3 acc = 0;
        half w = 1;
        for (int k = 0; k < SAMPLES; k++)
        {
            acc += tex2D(_MainTex, uv).rgb * w;
            w *= _ShaftDecay;
            uv += delta;
        }
        return half4(acc / SAMPLES, 1);
    }

    half4 FragComposite(v2f_img i) : SV_Target
    {
        half3 c = tex2D(_MainTex, i.uv).rgb;
        c += tex2D(_BloomTex, i.uv).rgb * _BloomIntensity * _BloomTint.rgb;
        c += tex2D(_ShaftTex, i.uv).rgb * _ShaftColor.rgb * _ShaftIntensity * _SunPos.z;
        c *= _Exposure;
        c = ACESFilm(c);

        // Split toning: cool shadows, warm highlights.
        half lum = dot(c, half3(0.2126h, 0.7152h, 0.0722h));
        c *= lerp(_ShadowTint.rgb, _HighlightTint.rgb, smoothstep(0.05h, 0.85h, lum));

        // Saturation & contrast.
        lum = dot(c, half3(0.2126h, 0.7152h, 0.0722h));
        c = lerp(lum.xxx, c, _Saturation);
        c = saturate((c - 0.5h) * _Contrast + 0.5h);

        // Vignette.
        float2 d = (i.uv - 0.5) * float2(_ScreenParams.x / _ScreenParams.y, 1.0);
        half v = smoothstep(1.05h, 0.25h, length(d) * 1.25h);
        c *= lerp(1.0h, v, _Vignette);

        // Very subtle animated grain (keeps gradients from banding).
        float n = frac(sin(dot(i.uv * _ScreenParams.xy + _Time.y * 61.0, float2(12.9898, 78.233))) * 43758.5453);
        c += (n - 0.5) * _Grain;

        return half4(saturate(c), 1);
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass // 0
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragPrefilter
            ENDCG
        }
        Pass // 1
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragDown
            ENDCG
        }
        Pass // 2
        {
            Blend One One
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragUp
            ENDCG
        }
        Pass // 3
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragComposite
            ENDCG
        }
        Pass // 4
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragShaftMask
            ENDCG
        }
        Pass // 5
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragShaftBlur
            ENDCG
        }
    }
    Fallback Off
}
