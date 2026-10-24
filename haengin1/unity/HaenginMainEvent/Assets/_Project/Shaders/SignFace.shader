// 행인1의 메인이벤트 — 간판 면(5차 패스 2026-10-25): 힉스필드 간판 그림 × 툰 1단 그림자 + 주변광 + 점광 + 안개.
//   낡음(_Age): 바램(채도↓·종이색 쪽) · 아래쪽 물때 얼룩 · 모서리 녹 — 면마다 _Seed 로 다르게.
//   조명 상자(_Emit): 전역 _HaenginSignsOn(DayLight — 노을·밤 1, 새벽·낮 0)일 때 면이 스스로 빛남. 형광등 줄이 고르지 않게(가로 위치마다 밝기 출렁).
//   LED(_Led 1): 점 격자 마스크 + 가로로 흐름(_ScrollU), 밤엔 밝게·낮엔 꺼진 점.
//   알파 컷아웃(_Cutoff) — 세로 간판 그림 바깥 투명. 깊이·그림자 패스 포함(화면 공간 외곽선이 면 테두리를 잡음).
Shader "Haengin/SignFace"
{
    Properties
    {
        _MainTex ("Face", 2D) = "white" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _ShadeColor ("Shade multiply", Color) = (0.80, 0.78, 0.78, 1)
        _Cutoff ("Alpha cutoff", Range(0, 1)) = 0.5
        _Age ("Age (fade, grime, rust)", Range(0, 1)) = 0.4
        _Seed ("Seed", Float) = 0
        _Emit ("Lightbox at night", Range(0, 2)) = 0
        _Led ("LED dots", Float) = 0
        _LedGrid ("LED grid (cells)", Vector) = (220, 14, 0, 0)
        _ScrollU ("Scroll U per second", Float) = 0
        _DayDim ("Brightness when signs off (shop interior through glass)", Range(0, 1)) = 1
        _Glass ("Glass reflection stripes", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
        float4 _MainTex_ST; half4 _Tint, _ShadeColor; float _Cutoff, _Age, _Seed, _Emit, _Led, _ScrollU, _DayDim, _Glass; float4 _LedGrid;
        CBUFFER_END
        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
        float _HaenginSignsOn;
        float2 FaceUV(float2 uv) { return TRANSFORM_TEX(uv, _MainTex) + float2(frac(_Time.y * _ScrollU), 0); }
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct A { float4 pos : POSITION; float3 n : NORMAL; float2 uv : TEXCOORD0; float2 uv2 : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float2 face : TEXCOORD4; float3 wp : TEXCOORD1; float3 n : TEXCOORD2; float fog : TEXCOORD3; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise(float2 p) { float2 i = floor(p), f = frac(p); f = f * f * (3 - 2 * f); return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y); }
            V Vert(A a)
            {
                V o; UNITY_SETUP_INSTANCE_ID(a); UNITY_TRANSFER_INSTANCE_ID(a, o);
                o.wp = TransformObjectToWorld(a.pos.xyz); o.pos = TransformWorldToHClip(o.wp);
                o.uv = a.uv; o.face = a.uv2;   // uv2 = 면 전체 기준 0~1(9칸 늘이기와 상관없이 낡음·조명 무늬용)
                o.n = TransformObjectToWorldNormal(a.n); o.fog = ComputeFogFactor(o.pos.z);
                return o;
            }
            half4 Frag(V i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half4 t = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, FaceUV(i.uv)) * _Tint;
                clip(t.a - _Cutoff);
                half3 albedo = t.rgb;
                float2 f = i.face;
                // 낡음: 바램 · 아래 물때 · 모서리 녹
                half lum = dot(albedo, half3(0.299, 0.587, 0.114));
                albedo = lerp(albedo, lerp(half3(lum, lum, lum), half3(0.93, 0.90, 0.84), 0.35), _Age * 0.35);
                float n1 = Noise(f * float2(9, 3) + _Seed * 7.1), n2 = Noise(f * float2(31, 9) + _Seed * 3.3);
                float grime = smoothstep(0.35, 0.0, f.y) * (0.5 + 0.5 * n1) * _Age;
                albedo *= 1.0 - grime * 0.35;
                float2 cd = min(f, 1 - f); float corner = smoothstep(0.08, 0.0, min(max(cd.x * 0.25, cd.y), max(cd.x, cd.y * 0.25)));
                albedo = lerp(albedo, half3(0.45, 0.28, 0.17), saturate(corner * n2 * 1.4) * _Age * 0.7);
                float3 n = normalize(i.n);
                float4 sc = TransformWorldToShadowCoord(i.wp);
                Light ml = GetMainLight(sc);
                half nl = saturate(dot(n, ml.direction) * 0.5 + 0.5) * ml.shadowAttenuation;
                half lit = smoothstep(0.45, 0.55, nl);
                half3 amb = SampleSH(n);
                half3 col = albedo * max(ml.color, amb) * lerp(_ShadeColor.rgb, half3(1, 1, 1), lit);
                #if defined(_ADDITIONAL_LIGHTS)
                uint cnt = GetAdditionalLightsCount();
                InputData inputData = (InputData)0; inputData.positionWS = i.wp; inputData.normalWS = n; inputData.viewDirectionWS = normalize(GetCameraPositionWS() - i.wp);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.pos);
                LIGHT_LOOP_BEGIN(cnt)
                    Light l = GetAdditionalLight(lightIndex, i.wp);
                    half anl = saturate(dot(n, l.direction) * 0.5 + 0.5);
                    col += albedo * l.color * smoothstep(0.35, 0.5, anl * l.distanceAttenuation) * 0.9;
                LIGHT_LOOP_END
                #endif
                float on = saturate(_HaenginSignsOn);
                // 조명 상자: 형광등 3~4줄이 비치는 고르지 않은 밝기 + 가장자리 어둡게
                float tubes = 0.78 + 0.22 * pow(abs(cos((f.x * 3.5 + _Seed) * 3.14159)), 2.0) - 0.12 * smoothstep(0.35, 0.5, abs(f.y - 0.5));
                col = lerp(col, albedo * 1.25 * tubes, saturate(_Emit * on));
                col *= lerp(_DayDim, 1.0, on);
                if (_Glass > 0.5)
                {
                    // 유리 반사 사선 띠(낮에 또렷, 밤엔 약하게) — 가게 안이 유리 너머로 보이게
                    float dg = frac((f.x * 2.2 + f.y * 1.1) + _Seed * 0.37);
                    float band = smoothstep(0.0, 0.03, dg) * (1 - smoothstep(0.10, 0.13, dg)) + smoothstep(0.30, 0.32, dg) * (1 - smoothstep(0.34, 0.36, dg));
                    half3 refl = max(ml.color, amb) * half3(0.55, 0.62, 0.70);
                    col = lerp(col, refl, band * lerp(0.45, 0.12, on));
                }
                if (_Led > 0.5)
                {
                    // LED: 점 격자는 면에 고정, 글자 그림만 흐름. 밤엔 켜진 점이 밝고 낮엔 어두운 판 + 희미한 점
                    float2 g = frac(i.uv * _LedGrid.xy) - 0.5;
                    float dotm = smoothstep(0.45, 0.30, length(g));
                    half bright = step(0.25, max(t.r, max(t.g, t.b)));
                    half3 env = max(ml.color, amb);
                    col = half3(0.04, 0.04, 0.045) * env + dotm * (t.rgb * lerp(0.2, 2.4, on) * bright + (1 - bright) * half3(0.06, 0.05, 0.05) * env);
                }
                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Back
            HLSLPROGRAM
            #pragma vertex VertS
            #pragma fragment FragS
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            struct A { float4 pos : POSITION; float3 n : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            V VertS(A a)
            {
                V o; UNITY_SETUP_INSTANCE_ID(a); o.uv = a.uv;
                float3 wp = TransformObjectToWorld(a.pos.xyz); float3 n = TransformObjectToWorldNormal(a.n);
                float4 p = TransformWorldToHClip(ApplyShadowBias(wp, n, _LightDirection));
                #if UNITY_REVERSED_Z
                    p.z = min(p.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    p.z = max(p.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                o.pos = p; return o;
            }
            half4 FragS(V i) : SV_Target { clip(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, FaceUV(i.uv)).a - _Cutoff); return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R Cull Back
            HLSLPROGRAM
            #pragma vertex VertD
            #pragma fragment FragD
            #pragma multi_compile_instancing
            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            V VertD(A a) { V o; UNITY_SETUP_INSTANCE_ID(a); o.pos = TransformObjectToHClip(a.pos.xyz); o.uv = a.uv; return o; }
            half4 FragD(V i) : SV_Target { clip(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, FaceUV(i.uv)).a - _Cutoff); return 0; }
            ENDHLSL
        }
    }
}
