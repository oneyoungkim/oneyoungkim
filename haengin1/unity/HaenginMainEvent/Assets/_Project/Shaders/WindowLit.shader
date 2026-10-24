// 행인1의 메인이벤트 — 켜진 창(5차 패스 2026-10-25): 키트 WinLit 판(창마다 0~1 UV)에 방마다 다른 실내 — 커튼 두 폭(주름 명암)·블라인드 줄·
//   창턱 화분·스탠드 실루엣, 빛 색도 조금씩(백열 주황 ~ 형광 흰빛). 판마다 다르게 하려고 화면 미분으로 판 가운데(월드)를 구해 해시(판 안에서 끊기지 않음).
//   조명 없음(스스로 빛남) + 안개. 켜고 끄기는 지금처럼 LightSpot(Window) 이 렌더러를 켬/끔.
Shader "Haengin/WindowLit"
{
    Properties
    {
        _Color ("Warm light", Color) = (1.15, 0.86, 0.55, 1)
        _Cool ("Cool light", Color) = (0.85, 0.95, 1.05, 1)
        _Strength ("Strength", Range(0, 2)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            Name "WindowLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Color, _Cool; float _Strength;
            CBUFFER_END
            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 wp : TEXCOORD1; float fog : TEXCOORD2; };
            float H(float3 p) { p = frac(p * 0.1031); p += dot(p, p.yzx + 33.33); return frac((p.x + p.y) * p.z); }
            V Vert(A a) { V o; UNITY_SETUP_INSTANCE_ID(a); o.wp = TransformObjectToWorld(a.pos.xyz); o.pos = TransformWorldToHClip(o.wp); o.uv = a.uv; o.fog = ComputeFogFactor(o.pos.z); return o; }
            half4 Frag(V i) : SV_Target
            {
                // 판 가운데(월드) = 지금 점 − dP/du·(u−½) − dP/dv·(v−½)
                float3 dpx = ddx(i.wp), dpy = ddy(i.wp); float2 dux = ddx(i.uv), duy = ddy(i.uv);
                float det = dux.x * duy.y - dux.y * duy.x;
                float3 dpdu = (dpx * duy.y - dpy * dux.y) / (abs(det) > 1e-9 ? det : 1e-9);
                float3 dpdv = (dpy * dux.x - dpx * duy.x) / (abs(det) > 1e-9 ? det : 1e-9);
                float3 c = i.wp - dpdu * (i.uv.x - 0.5) - dpdv * (i.uv.y - 0.5);
                float3 q = floor(c * 1.5);
                float h0 = H(q), h1 = H(q + 17.3), h2 = H(q + 41.7), h3 = H(q + 73.1);
                float2 uv = i.uv;
                half3 light = lerp(_Color.rgb, _Cool.rgb, step(0.72, h0)) * (0.85 + 0.3 * h1);
                // 안쪽 밝기: 천장 쪽이 조금 밝고 아래로 어두워짐
                half3 col = light * (0.75 + 0.35 * uv.y);
                half3 curtain = light * half3(0.55, 0.42, 0.35) * (0.6 + 0.4 * h2);
                float kind = h3;
                if (kind < 0.55)
                {
                    // 커튼 두 폭: 왼쪽 0~a, 오른쪽 1−b~1, 주름(세로 명암)
                    float a = 0.12 + 0.3 * h1, b = 0.1 + 0.3 * h2;
                    float inL = step(uv.x, a), inR = step(1 - b, uv.x);
                    float fold = 0.8 + 0.2 * sin(uv.x * 60.0 + h0 * 6.0);
                    col = lerp(col, curtain * fold, saturate(inL + inR));
                }
                else if (kind < 0.8)
                {
                    // 블라인드: 가로 줄, 위쪽 일부만 내림
                    float down = 0.35 + 0.6 * h1;
                    float slat = step(0.55, frac(uv.y * 14.0));
                    col = lerp(col, col * 0.45, slat * step(1 - down, uv.y));
                }
                // 실루엣: 창턱 화분(25%) · 스탠드(15%)
                if (h2 > 0.75)
                {
                    float px = 0.2 + 0.6 * h0;
                    float pot = step(abs(uv.x - px), 0.06) * step(uv.y, 0.12);
                    float leaves = step(length((uv - float2(px, 0.2)) * float2(1.0, 1.4)), 0.11);
                    col = lerp(col, half3(0.06, 0.05, 0.05), saturate(pot + leaves));
                }
                else if (h2 < 0.15)
                {
                    float px = 0.25 + 0.5 * h1;
                    float pole = step(abs(uv.x - px), 0.008) * step(uv.y, 0.55);
                    float shade = step(abs(uv.x - px), 0.07 - (uv.y - 0.55) * 0.25) * step(0.55, uv.y) * step(uv.y, 0.72);
                    col = lerp(col, half3(0.08, 0.06, 0.05), saturate(pole + shade * 0.85));
                }
                // 창틀 가장자리 어둡게
                float2 e = min(uv, 1 - uv);
                col *= 0.75 + 0.25 * smoothstep(0.0, 0.06, min(e.x, e.y));
                col *= _Strength;
                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
