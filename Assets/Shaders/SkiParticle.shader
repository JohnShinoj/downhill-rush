// Particle shaders that survive builds (referenced from the scene) and support fog.
//  - "SkiGame/Particle Alpha":    soft alpha-blended (snow, powder, ski tracks)
//  - "SkiGame/Particle Additive": additive with an HDR boost so bloom makes it glow (sparkles, trails)
Shader "SkiGame/Particle Alpha"
{
    Properties { _MainTex ("Texture", 2D) = "white" {} _Tint ("Tint", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            sampler2D _MainTex; float4 _MainTex_ST; fixed4 _Tint;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; UNITY_FOG_COORDS(1) };
            v2f vert(appdata v)
            {
                v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Tint; UNITY_TRANSFER_FOG(o, o.pos); return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv) * i.color;
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
}
