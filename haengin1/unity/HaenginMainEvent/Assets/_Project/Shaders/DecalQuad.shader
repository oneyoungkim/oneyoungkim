// 행인1의 메인이벤트 — 얼룩·낙서·포스터·노면 표시 판(2차 패스): 알파 블렌드, 깊이 안 씀, 깊이 오프셋(바닥·벽 위 1cm 판이 깜빡이지 않게), 안개, 주변광·해 밝기만 받음(그림자 없음).
Shader "Haengin/DecalQuad"
{
    Properties
    {
        _MainTex ("Texture (atlas)", 2D) = "white" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent-400" "IgnoreProjector" = "True" }
        ZWrite Off ZTest LEqual Blend SrcAlpha OneMinusSrcAlpha Offset -1, -1 Cull [_Cull]
        Pass
        {
            Name "DecalQuad"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST; half4 _Tint; float _Cull;
            CBUFFER_END
            struct A { float4 pos : POSITION; float3 n : NORMAL; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 n : TEXCOORD1; float fog : TEXCOORD2; };
            V Vert(A a)
            {
                V o; o.pos = TransformObjectToHClip(a.pos.xyz); o.uv = TRANSFORM_TEX(a.uv, _MainTex);
                o.n = TransformObjectToWorldNormal(a.n); o.fog = ComputeFogFactor(o.pos.z); return o;
            }
            half4 Frag(V i) : SV_Target
            {
                half4 t = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * _Tint;
                Light ml = GetMainLight();
                half3 lightCol = max(ml.color, SampleSH(normalize(i.n)));
                t.rgb *= lightCol;
                t.rgb = MixFog(t.rgb, i.fog);
                return t;
            }
            ENDHLSL
        }
    }
}
