// 행인1의 메인이벤트 — 얼룩·낙서·포스터·노면 판(2·3차 패스): 깊이 안 씀·깊이 오프셋·안개·주변광과 해 밝기만.
// 3차: 블렌드 선택(보통 / 곱하기 = 바닥·벽 얼룩), 바랜 톤(_Fade: 채도 내리고 종이색 쪽으로), 가장자리 거칠게(_EdgeRough: 해시 잡음으로 테두리 알파를 깎음) — 스티커 느낌 줄이기.
Shader "Haengin/DecalQuad"
{
    Properties
    {
        _MainTex ("Texture (atlas)", 2D) = "white" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Cull ("Cull", Float) = 2
        _SrcBlend ("Src blend", Float) = 5
        _DstBlend ("Dst blend", Float) = 10
        _Multiply ("Multiply mode", Float) = 0
        _Fade ("Fade (desaturate + paper)", Range(0, 1)) = 0
        _EdgeRough ("Edge roughness", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent-400" "IgnoreProjector" = "True" }
        ZWrite Off ZTest LEqual Blend [_SrcBlend] [_DstBlend] Offset -1, -1 Cull [_Cull]
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
            float4 _MainTex_ST; half4 _Tint; float _Cull, _SrcBlend, _DstBlend, _Multiply, _Fade, _EdgeRough;
            CBUFFER_END
            struct A { float4 pos : POSITION; float3 n : NORMAL; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float2 uv0 : TEXCOORD3; float3 n : TEXCOORD1; float fog : TEXCOORD2; };
            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            V Vert(A a)
            {
                V o; o.pos = TransformObjectToHClip(a.pos.xyz); o.uv = TRANSFORM_TEX(a.uv, _MainTex); o.uv0 = a.uv;
                o.n = TransformObjectToWorldNormal(a.n); o.fog = ComputeFogFactor(o.pos.z); return o;
            }
            half4 Frag(V i) : SV_Target
            {
                half4 t = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * _Tint;
                // 바랜 톤: 채도 ↓, 종이색(#EFE8DA) 쪽으로
                half lum = dot(t.rgb, half3(0.299, 0.587, 0.114));
                half3 faded = lerp(t.rgb, lerp(half3(lum, lum, lum), half3(0.94, 0.91, 0.85), 0.45), _Fade);
                t.rgb = faded;
                // 가장자리 거칠게: 판 테두리 8% 안에서 해시 잡음으로 알파를 깎음(찢긴 종이·번진 얼룩)
                float2 e = min(i.uv0, 1.0 - i.uv0);
                float edge = saturate(min(e.x, e.y) / 0.08);
                float noise = Hash(floor(i.uv0 * 48.0)) * 0.6 + Hash(floor(i.uv0 * 11.0)) * 0.4;
                t.a *= lerp(1.0, step(1.0 - edge, noise) * (0.7 + 0.3 * noise), _EdgeRough);
                Light ml = GetMainLight();
                half3 lightCol = max(ml.color, SampleSH(normalize(i.n)));
                if (_Multiply > 0.5)
                {
                    // 곱하기: 바탕색 × lerp(1, 얼룩색, 알파). 안개·조명은 바탕이 이미 받았으므로 안 곱함
                    half3 m = lerp(half3(1, 1, 1), t.rgb, t.a);
                    return half4(m, 1);
                }
                t.rgb *= lightCol;
                t.rgb = MixFog(t.rgb, i.fog);
                return t;
            }
            ENDHLSL
        }
    }
}
