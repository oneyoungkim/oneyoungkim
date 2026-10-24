// 행인1의 메인이벤트 — 천(빨래·현수막·차양 끝단) 툰 셰이더(5차 패스 2026-10-25): ToonLeaf 와 같은 셀 1단 그림자·주변광·점광·안개 +
//   바람 흔들림이 '위가 묶인' 천처럼: 흔들림 = (1 − uv.y)² (uv.y 1 = 줄에 매달린 위 끝, 0 = 아래). 양면, 알파 컷아웃(그림 바깥 투명).
//   _SwayAxis = 흔들리는 방향(세계 좌표, 천 면의 법선 쪽) — 판이 앞뒤로 펄럭임. 그림자 캐스터도 같이 흔들림.
Shader "Haengin/ToonCloth"
{
    Properties
    {
        _BaseMap ("Base", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _ShadeColor ("Shade multiply", Color) = (0.78, 0.76, 0.76, 1)
        _WindAmp ("Wind amplitude m", Float) = 0.06
        _WindFreq ("Wind frequency", Float) = 1.6
        _Cutoff ("Alpha cutoff", Range(0, 1)) = 0.3
        _Pin ("Pin: 0 top only / 1 top and bottom", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST; half4 _BaseColor, _ShadeColor; float _WindAmp, _WindFreq, _Cutoff, _Pin;
        CBUFFER_END
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        float3 Sway(float3 posOS, float3 nOS, float2 uv)
        {
            float3 wp = TransformObjectToWorld(posOS);
            float3 n = normalize(TransformObjectToWorldNormal(nOS));
            float w = _Pin > 0.5 ? sin(saturate(uv.y) * 3.14159) * 0.8 : saturate(1 - uv.y) * saturate(1 - uv.y);   // 현수막(위아래 묶임)은 가운데가 가장 펄럭임
            float t = _Time.y * _WindFreq + wp.x * 0.6 + wp.z * 0.45;
            float s = sin(t) * 0.7 + sin(t * 2.3 + 1.1) * 0.3;
            return wp + n * s * _WindAmp * w + float3(0, -abs(s) * _WindAmp * 0.15 * w, 0);
        }
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off ZWrite On
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
            struct A { float4 pos : POSITION; float3 n : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 wp : TEXCOORD1; float3 n : TEXCOORD2; float fog : TEXCOORD3; UNITY_VERTEX_INPUT_INSTANCE_ID };
            V Vert(A a)
            {
                V o; UNITY_SETUP_INSTANCE_ID(a); UNITY_TRANSFER_INSTANCE_ID(a, o);
                o.wp = Sway(a.pos.xyz, a.n, a.uv); o.pos = TransformWorldToHClip(o.wp);
                o.uv = TRANSFORM_TEX(a.uv, _BaseMap); o.n = TransformObjectToWorldNormal(a.n); o.fog = ComputeFogFactor(o.pos.z);
                return o;
            }
            half4 Frag(V i, bool front : SV_IsFrontFace) : SV_Target
            {
                half4 t = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                clip(t.a - _Cutoff);
                float3 n = normalize(i.n) * (front ? 1 : -1);
                float4 sc = TransformWorldToShadowCoord(i.wp);
                Light ml = GetMainLight(sc);
                half nl = saturate(dot(n, ml.direction) * 0.5 + 0.5) * ml.shadowAttenuation;
                half lit = smoothstep(0.45, 0.55, nl);
                half3 col = t.rgb * max(ml.color, SampleSH(n)) * lerp(_ShadeColor.rgb, half3(1, 1, 1), lit);
                #if defined(_ADDITIONAL_LIGHTS)
                uint cnt = GetAdditionalLightsCount();
                InputData inputData = (InputData)0; inputData.positionWS = i.wp; inputData.normalWS = n; inputData.viewDirectionWS = normalize(GetCameraPositionWS() - i.wp);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.pos);
                LIGHT_LOOP_BEGIN(cnt)
                    Light l = GetAdditionalLight(lightIndex, i.wp);
                    half anl = saturate(dot(n, l.direction) * 0.5 + 0.5);
                    col += t.rgb * l.color * smoothstep(0.35, 0.5, anl * l.distanceAttenuation) * 0.9;
                LIGHT_LOOP_END
                #endif
                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Off
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
                V o; UNITY_SETUP_INSTANCE_ID(a); o.uv = TRANSFORM_TEX(a.uv, _BaseMap);
                float3 wp = Sway(a.pos.xyz, a.n, a.uv); float3 n = TransformObjectToWorldNormal(a.n);
                float4 p = TransformWorldToHClip(ApplyShadowBias(wp, n, _LightDirection));
                #if UNITY_REVERSED_Z
                    p.z = min(p.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    p.z = max(p.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                o.pos = p; return o;
            }
            half4 FragS(V i) : SV_Target { clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a * _BaseColor.a - _Cutoff); return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R Cull Off
            HLSLPROGRAM
            #pragma vertex VertD
            #pragma fragment FragD
            #pragma multi_compile_instancing
            struct A { float4 pos : POSITION; float3 n : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            V VertD(A a) { V o; UNITY_SETUP_INSTANCE_ID(a); o.pos = TransformWorldToHClip(Sway(a.pos.xyz, a.n, a.uv)); o.uv = TRANSFORM_TEX(a.uv, _BaseMap); return o; }
            half4 FragD(V i) : SV_Target { clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a * _BaseColor.a - _Cutoff); return 0; }
            ENDHLSL
        }
    }
}
