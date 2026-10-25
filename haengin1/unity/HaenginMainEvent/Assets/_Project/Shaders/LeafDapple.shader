// 행인1의 메인이벤트 — 룩데브 ⑤(docs/14 4-5, 2026-10-26): 낮 땅의 햇빛 얼룩(나무 잎 그림자) — 나무 둘레 땅에 붙인 판, 곱하기.
// UTS 는 주광 쿠키를 안 읽어서(GetMainLight(shadowCoord) — 위치 없음) 쿠키 대신 판으로 칠한다. 깊이 안 씀 · 깊이 오프셋.
// 무늬 = 월드 xz 잡음 두 겹(잎 덩어리 그늘 + 그 사이 햇빛 구멍), 판 가장자리로 갈수록 옅게. 그린 듯하게 3단.
// 정점: uv = 판 안 좌표(-1~1, 원), 색 a = 세기(나무 크기), uv2.x = 씨앗. 세기 = 전역 _HaenginDapple(DayLight 프리셋 — 낮 1, 새벽·노을 약하게, 밤 0) × _HaenginLightOn.
Shader "Haengin/LeafDapple"
{
    Properties
    {
        _Shade ("Shade multiply (darkest)", Color) = (0.62, 0.60, 0.62, 1)
        _Scale ("Leaf blob size m", Float) = 0.9
        _Holes ("Sun holes share", Range(0, 1)) = 0.42
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent-395" "IgnoreProjector" = "True" }
        ZWrite Off ZTest LEqual Blend DstColor Zero Offset -2, -2 Cull Off
        Pass
        {
            Name "LeafDapple"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Shade; float _Scale, _Holes;
            CBUFFER_END
            float _HaenginDapple, _HaenginLightOn;
            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; float2 uv1 : TEXCOORD1; half4 col : COLOR; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float seed : TEXCOORD1; half a : TEXCOORD2; float3 wp : TEXCOORD3; };
            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise(float2 p) { float2 i = floor(p), f = frac(p); f = f * f * (3 - 2 * f); return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y); }
            V Vert(A a)
            {
                V o; o.wp = TransformObjectToWorld(a.pos.xyz); o.pos = TransformWorldToHClip(o.wp);
                o.uv = a.uv; o.seed = a.uv1.x; o.a = a.col.a; return o;
            }
            half4 Frag(V i) : SV_Target
            {
                float k = saturate(_HaenginDapple) * (_HaenginLightOn > 0.5 ? 1.0 : 0.0) * i.a;
                if (k <= 0.001) return half4(1, 1, 1, 1);
                float r = length(i.uv);
                float edge = 1 - smoothstep(0.55, 1.0, r);
                float2 p = i.wp.xz / _Scale + i.seed * 17.0;
                float n = Noise(p) * 0.55 + Noise(p * 2.3 + 3.7) * 0.3 + Noise(p * 5.1 + 9.1) * 0.15;
                // 그늘 덩어리(낮은 주파) 안에 햇빛 구멍(높은 값) — 가장자리에선 구멍이 많아짐
                float shade = smoothstep(_Holes - 0.08 + 0.25 * (1 - edge), _Holes + 0.06 + 0.25 * (1 - edge), 1 - n);
                shade = floor(shade * 3.0 + 0.5) / 3.0;
                float a = saturate(shade * edge * k);
                return half4(lerp(half3(1, 1, 1), _Shade.rgb, a), 1);
            }
            ENDHLSL
        }
    }
}
