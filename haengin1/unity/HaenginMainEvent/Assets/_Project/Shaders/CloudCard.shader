// 행인1의 메인이벤트 — 구름·달 판(docs/10 8장): 조명·안개 없음, 알파 텍스처 × 색.
Shader "Haengin/CloudCard"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Background+30" "IgnoreProjector" = "True" }
        ZWrite Off ZTest LEqual Cull Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Name "CloudCard"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            half4 _Tint;
            CBUFFER_END
            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            V Vert(A a) { V o; o.pos = TransformObjectToHClip(a.pos.xyz); o.uv = a.uv; return o; }
            half4 Frag(V i) : SV_Target
            {
                half4 t = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                return half4(t.rgb * _Tint.rgb, t.a * _Tint.a);
            }
            ENDHLSL
        }
    }
}
