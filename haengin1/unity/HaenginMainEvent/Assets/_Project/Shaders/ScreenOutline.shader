// 행인1의 메인이벤트 — 배경 화면 공간 외곽선(docs/09_M3_버티컬슬라이스_설계.md 3-5 결정 12, docs/10 10장)
// URP Full Screen Pass 'Outline'(후처리 앞, 깊이만 씀 — UTS 에는 DepthNormals 패스가 없어 법선 텍스처를 믿을 수 없다, 카메라 깊이·스텐실 바인딩).
// 선 = 실루엣(이웃 화소가 더 멀다) + 모서리(깊이 2차 차분 = 면의 기울기가 꺾임). 굵기: 근경 1.5px → 30m 0.75px → 55~80m 에서 0(원경은 카드).
// 색 #1A1417(선형). 캐릭터(스텐실 1 = UTS StencilMask)에는 그리지 않음 — 헐 외곽선이 맡는다.
Shader "Haengin/ScreenOutline"
{
    Properties
    {
        _Strength ("Strength", Range(0, 1)) = 1
        _NearWidth ("Near width px", Float) = 1.5
        _FarWidth ("Width at 30m px", Float) = 0.75
        _FadeStart ("Fade start m", Float) = 55
        _FadeEnd ("Fade end m", Float) = 80
        _DepthRel ("Silhouette (relative)", Float) = 0.035
        _DepthAbs ("Silhouette (m)", Float) = 0.04
        _CreaseRel ("Crease (relative)", Float) = 0.0008
        _CreaseAbs ("Crease (m)", Float) = 0.0006
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        ZWrite Off ZTest Always Blend Off Cull Off
        Pass
        {
            Name "Outline"
            Stencil { Ref 1 Comp NotEqual Pass Keep }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            float _Strength, _NearWidth, _FarWidth, _FadeStart, _FadeEnd, _DepthRel, _DepthAbs, _CreaseRel, _CreaseAbs;

            float Eye(float2 uv)
            {
                float raw = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    if (raw <= 0.00001) return 100000.0;
                #else
                    if (raw >= 0.99999) return 100000.0;
                #endif
                return LinearEyeDepth(raw, _ZBufferParams);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 c = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                float d0 = Eye(uv);
                if (d0 > _FadeEnd || _Strength <= 0.001) return c;
                float w = lerp(_NearWidth, _FarWidth, saturate(d0 / 30.0));
                float fade = 1.0 - smoothstep(_FadeStart, _FadeEnd, d0);
                float2 px = w * (_ScreenParams.zw - 1.0);
                float dl = Eye(uv - float2(px.x, 0)), dr = Eye(uv + float2(px.x, 0));
                float dd = Eye(uv - float2(0, px.y)), du = Eye(uv + float2(0, px.y));
                float thr = _DepthRel * d0 + _DepthAbs;
                // 실루엣: 이웃이 더 멀면(앞 물체의 가장자리) — 뒤쪽 화소에는 안 그려 선이 두 번 생기지 않게
                float sil = max(max(step(thr, dl - d0), step(thr, dr - d0)), max(step(thr, dd - d0), step(thr, du - d0)));
                // 모서리: 깊이가 이어지는데(불연속 아님) 기울기가 꺾이는 곳(2차 차분)
                float cont = step(abs(dl - d0), thr * 3.0) * step(abs(dr - d0), thr * 3.0) * step(abs(dd - d0), thr * 3.0) * step(abs(du - d0), thr * 3.0);
                float cth = _CreaseRel * d0 + _CreaseAbs;
                float crease = max(step(cth, abs(dl + dr - 2.0 * d0)), step(cth, abs(dd + du - 2.0 * d0))) * cont;
                float edge = saturate(max(sil, crease * 0.85)) * fade * _Strength;
                const half3 ink = half3(0.0103, 0.0070, 0.0080);   // #1A1417 선형
                c.rgb = lerp(c.rgb, ink, edge);
                return c;
            }
            ENDHLSL
        }
    }
}
