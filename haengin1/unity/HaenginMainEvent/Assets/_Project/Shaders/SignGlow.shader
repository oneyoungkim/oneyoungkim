// 행인1의 메인이벤트 — 간판 빛 번짐(5차 패스): 간판보다 조금 큰 판에 더하기(Additive)로 부드러운 사각 후광. 블룸 없이(09 3-7) 번짐만 흉내.
//   전역 _HaenginSignsOn(노을·밤) 일 때만. _Inner = 간판이 차지하는 안쪽 사각형 비율(uv), 바깥으로 갈수록 0.
//   _Mode 1 = 가로등 날벌레 점(밤만, 아주 약하게): 판 안에서 점 몇 개가 시간에 따라 맴돎.
Shader "Haengin/SignGlow"
{
    Properties
    {
        _Color ("Glow color", Color) = (1, 0.8, 0.5, 1)
        _Strength ("Strength", Range(0, 2)) = 0.35
        _Inner ("Inner rect (uv half size)", Vector) = (0.35, 0.3, 0, 0)
        _Mode ("0 glow / 1 bugs", Float) = 0
        _Seed ("Seed", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" }
        ZWrite Off ZTest LEqual Blend One One Cull Off
        Pass
        {
            Name "SignGlow"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Color; float _Strength, _Mode, _Seed; float4 _Inner;
            CBUFFER_END
            float _HaenginSignsOn, _HaenginNight;
            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float fogf : TEXCOORD1; };
            float Hash(float n) { return frac(sin(n * 12.9898 + _Seed * 78.233) * 43758.5453); }
            V Vert(A a) { V o; UNITY_SETUP_INSTANCE_ID(a); o.pos = TransformObjectToHClip(a.pos.xyz); o.uv = a.uv; o.fogf = ComputeFogFactor(o.pos.z); return o; }
            half4 Frag(V i) : SV_Target
            {
                float2 c = abs(i.uv - 0.5);
                if (_Mode < 0.5)
                {
                    if (_HaenginSignsOn < 0.01) return 0;
                    float2 d = max(c - _Inner.xy, 0) / max(0.5 - _Inner.xy, 1e-3);
                    float g = saturate(1 - length(d)); g = g * g * g;
                    float inside = step(c.x, _Inner.x) * step(c.y, _Inner.y);
                    half3 col = _Color.rgb * g * (1 - inside * 0.6) * _Strength * _HaenginSignsOn;
                    return half4(col * (1 - saturate(1 - i.fogf) * 0.7), 1);
                }
                // 날벌레: 점 6개가 각자 원을 그리며 맴돎(밤만)
                if (_HaenginNight < 0.01) return 0;
                float s = 0;
                for (int k = 0; k < 6; k++)
                {
                    float ph = _Time.y * (1.3 + Hash(k) * 1.7) + Hash(k + 10) * 6.28;
                    float2 p = 0.5 + float2(cos(ph), sin(ph * 1.3)) * (0.12 + Hash(k + 20) * 0.25) + float2(sin(ph * 2.7), cos(ph * 3.1)) * 0.03;
                    s += smoothstep(0.012, 0.0, length(i.uv - p));
                }
                return half4(_Color.rgb * s * _Strength * _HaenginNight, 1);
            }
            ENDHLSL
        }
    }
}
