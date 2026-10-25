// 행인1의 메인이벤트 — 6차 패스: 밤 빛이 길에 번지는 그린 듯한 빛 웅덩이(가게 앞·간판 아래·가로등 아래).
// 땅에 붙인 판(정점은 땅 높이에 맞춤) — 더하기 블렌드, 깊이 안 씀. 젖은 반사 없음(대표 지시: 젖은 듯한 반사는 쓰지 말 것).
// 정점: uv = 웅덩이 안 좌표(원형 -1~1 / 가게 앞 x -1~1 · y 0(문턱)~1(바깥)), uv2.x = 모양(0 원형 · 1 가게 앞), uv2.y = 켜짐 조건(0 밤 · 1 가게 간판 켜짐),
//       색 rgb = 빛 색, a = 세기. 하나의 재질로 합쳐 그리기 1회(구역 칸마다).
Shader "Haengin/LightPool"
{
    Properties
    {
        _Strength ("Strength", Float) = 1
        _Bands ("Painted bands (0 = smooth)", Range(0, 1)) = 0.25
        _Cover ("Cover base (alpha share)", Range(0, 1)) = 0.55
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent-390" "IgnoreProjector" = "True" }
        ZWrite Off ZTest LEqual Blend One OneMinusSrcAlpha Offset -2, -2 Cull Off   // 미리 곱한 알파: 바탕(밤 남색)을 조금 덮고 따뜻한 빛을 더함 — 더하기만 하면 분홍빛
        Pass
        {
            Name "LightPool"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _Strength, _Bands, _Cover;
            CBUFFER_END
            float _HaenginNight, _HaenginSignsOn, _HaenginLightOn, _HaenginPoolBoost;   // 룩데브 ⑤(docs/14 4-5): 밤 웅덩이 세기 × 프리셋 PoolBoost(밤 2)
            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; float2 uv1 : TEXCOORD1; half4 col : COLOR; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float2 k : TEXCOORD1; half4 col : TEXCOORD2; float fog : TEXCOORD3; float3 wp : TEXCOORD4; };
            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise(float2 p) { float2 i = floor(p), f = frac(p); f = f * f * (3 - 2 * f); return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y); }
            V Vert(A a)
            {
                V o; o.wp = TransformObjectToWorld(a.pos.xyz); o.pos = TransformWorldToHClip(o.wp);
                o.uv = a.uv; o.k = a.uv1; o.col = a.col; o.fog = ComputeFogFactor(o.pos.z); return o;
            }
            half4 Frag(V i) : SV_Target
            {
                float gate = saturate(_HaenginNight) * (i.k.y > 0.5 ? saturate(_HaenginSignsOn) : 1.0);
                if (gate <= 0.001) return 0;
                float f;
                if (i.k.x < 0.5)
                {
                    float d = length(i.uv);
                    f = saturate(1 - d); f = f * f * (3 - 2 * f);           // 가로등: 가운데 밝고 가장자리 부드럽게
                }
                else
                {
                    float sx = 1 - smoothstep(0.55, 1.0, abs(i.uv.x));      // 가게 앞: 폭 방향은 유리 폭만큼, 바깥으로 갈수록 옅게
                    float vy = _HaenginLightOn > 0.5 ? i.uv.y * 1.8 : i.uv.y;   // 룩데브 ⑤: 6m → 3.3m(인도까지만 — 차도가 통째로 밝던 것)
                    float sy = pow(saturate(1 - vy), 1.6) * smoothstep(0.0, 0.06, i.uv.y);
                    f = sx * sy;
                }
                // 붓 결: 땅 좌표 잡음으로 가장자리를 흔들고, 몇 단으로 나눠 그린 듯하게
                float n = Noise(i.wp.xz * 1.7) * 0.6 + Noise(i.wp.xz * 5.3) * 0.4;
                f = saturate(f * (0.82 + 0.36 * n) - 0.02);
                float stepped = floor(f * 4.0 + 0.5) / 4.0;
                f = lerp(f, stepped, _Bands * smoothstep(0.05, 0.25, f));
                // 룩데브 ⑤: 가로등 원 웅덩이만 세게, 가게 앞 사각은 줄임(넓게 깔려 길 전체가 밝은 베이지 — 2026-10-26 진단: 웅덩이를 끄면 길이 어두워짐)
                float boost = _HaenginLightOn > 0.5 ? (i.k.x < 0.5 ? (_HaenginPoolBoost > 0.01 ? _HaenginPoolBoost : 1.0) : 0.7) : 1.0;
                float k = saturate(i.col.a * f * gate * _Strength * boost);
                half3 c = i.col.rgb * k;
                half fogK = 1 - ComputeFogIntensity(i.fog);
                c = MixFogColor(c, half3(0, 0, 0), i.fog);                  // 안개 속에선 옅게
                return half4(c, k * _Cover * (1 - fogK));
            }
            ENDHLSL
        }
    }
}
