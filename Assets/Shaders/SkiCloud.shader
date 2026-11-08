// Soft sky clouds (billboards on the horizon). Unlit, not fogged, lit warm when near the sun,
// with a darker cool underside.
Shader "SkiGame/Cloud"
{
    Properties
    {
        _MainTex ("Cloud (alpha)", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 0.85)
        _ShadowColor ("Underside", Color) = (0.62, 0.68, 0.82, 1)
        _SunColor ("Sun glow", Color) = (1, 0.82, 0.6, 1)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent-100" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            fixed4 _Color, _ShadowColor, _SunColor;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 wpos : TEXCOORD1; };
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 t = tex2D(_MainTex, i.uv);
                // Underside darker (uv.y = 0 at the bottom of the quad), top bright.
                fixed3 c = lerp(_ShadowColor.rgb, _Color.rgb, saturate(i.uv.y * 1.3 + (t.r - 0.5)));
                float3 view = normalize(i.wpos - _WorldSpaceCameraPos);
                float sun = pow(saturate(dot(view, normalize(_WorldSpaceLightPos0.xyz))), 6.0);
                c = lerp(c, _SunColor.rgb * 1.4, sun * 0.7);
                return fixed4(c, t.a * _Color.a);
            }
            ENDCG
        }
    }
}
