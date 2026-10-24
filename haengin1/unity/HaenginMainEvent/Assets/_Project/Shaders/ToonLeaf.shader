// 행인1의 메인이벤트 — 식생 툰 셰이더(docs/10 2차 패스 2026-10-24): 1단 그림자(고유색 × 배경 곱셈) + 주변광 + 점광(Forward+) + 안개 + 바람 흔들림(정점, 높이에 비례).
// UTS 대신 쓰는 이유: 바람 정점 변형과 그림자 캐스터가 같이 흔들려야 하고, 잎 덩어리는 스펙큘러·림·헐 외곽선이 필요 없다. 외곽선은 화면 공간.
Shader "Haengin/ToonLeaf"
{
    Properties
    {
        _BaseMap ("Base", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _ShadeColor ("Shade multiply", Color) = (0.78, 0.76, 0.74, 1)
        _Step ("Shade step", Range(0, 1)) = 0.5
        _Feather ("Feather", Range(0.001, 0.3)) = 0.04
        _WindAmp ("Wind amplitude m", Float) = 0.05
        _WindFreq ("Wind frequency", Float) = 1.1
        _Height ("Sway height m", Float) = 3
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST; half4 _BaseColor, _ShadeColor; float _Step, _Feather, _WindAmp, _WindFreq, _Height;
        CBUFFER_END
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        float3 Sway(float3 posOS)
        {
            float3 wp = TransformObjectToWorld(posOS);
            float w = saturate(posOS.y / max(_Height, 0.01));
            float t = _Time.y * _WindFreq + wp.x * 0.35 + wp.z * 0.27;
            float2 off = float2(sin(t), cos(t * 0.7 + 1.3)) * _WindAmp * w * w;
            return float3(wp.x + off.x, wp.y, wp.z + off.y);
        }
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
            struct A { float4 pos : POSITION; float3 n : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 wp : TEXCOORD1; float3 n : TEXCOORD2; float fog : TEXCOORD3; UNITY_VERTEX_INPUT_INSTANCE_ID };
            V Vert(A a)
            {
                V o; UNITY_SETUP_INSTANCE_ID(a); UNITY_TRANSFER_INSTANCE_ID(a, o);
                o.wp = Sway(a.pos.xyz);
                o.pos = TransformWorldToHClip(o.wp);
                o.uv = TRANSFORM_TEX(a.uv, _BaseMap);
                o.n = TransformObjectToWorldNormal(a.n);
                o.fog = ComputeFogFactor(o.pos.z);
                return o;
            }
            half4 Frag(V i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseColor.rgb;
                float3 n = normalize(i.n);
                float4 sc = TransformWorldToShadowCoord(i.wp);
                Light ml = GetMainLight(sc);
                half nl = saturate(dot(n, ml.direction) * 0.5 + 0.5) * ml.shadowAttenuation;
                half lit = smoothstep(_Step - _Feather, _Step + _Feather, nl);
                half3 amb = SampleSH(n);
                half3 lightCol = max(ml.color, amb);            // UTS 와 같은 규칙: 해가 없으면 주변광이 밝기를 정함
                half3 col = albedo * lightCol * lerp(_ShadeColor.rgb, half3(1, 1, 1), lit);
                #if defined(_ADDITIONAL_LIGHTS)
                uint cnt = GetAdditionalLightsCount();
                InputData inputData = (InputData)0; inputData.positionWS = i.wp; inputData.normalWS = n; inputData.viewDirectionWS = normalize(GetCameraPositionWS() - i.wp);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.pos);
                LIGHT_LOOP_BEGIN(cnt)
                    Light l = GetAdditionalLight(lightIndex, i.wp);
                    half anl = saturate(dot(n, l.direction) * 0.5 + 0.5);
                    half a = smoothstep(0.35, 0.5, anl * l.distanceAttenuation);   // 툰 램프: 빛 웅덩이 경계 또렷
                    col += albedo * l.color * a * 0.9;
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
            ZWrite On ZTest LEqual ColorMask 0 Cull Back
            HLSLPROGRAM
            #pragma vertex VertS
            #pragma fragment FragS
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            struct A { float4 pos : POSITION; float3 n : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 pos : SV_POSITION; };
            V VertS(A a)
            {
                V o; UNITY_SETUP_INSTANCE_ID(a);
                float3 wp = Sway(a.pos.xyz);
                float3 n = TransformObjectToWorldNormal(a.n);
                float4 p = TransformWorldToHClip(ApplyShadowBias(wp, n, _LightDirection));
                #if UNITY_REVERSED_Z
                    p.z = min(p.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    p.z = max(p.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                o.pos = p; return o;
            }
            half4 FragS(V i) : SV_Target { return 0; }
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
            struct A { float4 pos : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 pos : SV_POSITION; };
            V VertD(A a) { V o; UNITY_SETUP_INSTANCE_ID(a); o.pos = TransformWorldToHClip(Sway(a.pos.xyz)); return o; }
            half4 FragD(V i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
