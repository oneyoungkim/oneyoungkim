// 행인1의 메인이벤트 — 배경 화면 공간 외곽선(docs/09_M3_버티컬슬라이스_설계.md 3-5 결정 12, docs/10 10장)
// URP Full Screen Pass 'Outline'(후처리 앞, 깊이만 씀 — UTS 에는 DepthNormals 패스가 없어 법선 텍스처를 믿을 수 없다, 카메라 깊이·스텐실 바인딩).
// 선 = 실루엣(이웃 화소가 더 멀다) + 모서리(역깊이 2차 차분 — 평면은 1/z 가 화면에서 1차식이라 비스듬한 바닥에 잔선이 안 생기고,
//      창틀·방범창·문 안쪽처럼 작은 단차와 건물 모서리가 잡힌다). 색 #1A1417(선형).
// 4차(2026-10-24): 굵기를 거리별로 — 4m 안 2.4px → 12m 1.3 → 25m 0.9 → 45m 0.6(1px 아래는 굵기 대신 진하기로), 1080p 기준이라
//   해상도에 비례(슈퍼샘플 촬영·4K 에서도 같은 굵기로 보임) · 근경(8~22m 안) 면에 손그림 결: 종이 결 잡음 + 붓 얼룩(월드 좌표 3D 잡음 → 화면이 움직여도 면에 붙음).
// 캐릭터·잎 덩어리(스텐실 1 = UTS StencilMask · ToonLeafCard _StencilRef)에는 그리지 않음 — 헐 외곽선·잎 덩어리 먹선이 맡는다.
// 룩데브 ③(docs/14 4-3, 2026-10-26 — 전역 _HaenginInkOn = 1 일 때): 선 색 = 선 자리 칠 색 × 0.09 와 시간대 먹 색(_HaenginInkColor, 프레임 실측 따뜻한 짙은 갈색) 반반
//   — 필름 볼륨 전이라 예전 고정색 #1A1417 이 새벽 남색·노을 적갈로 물들던 것 · 굵기 2.4 → 2.0 × 붓 압력(월드 잡음 ±20%) · 색 경계 잔선(휘도 대비 0.16~0.38, 어두운 쪽 1px, 26m 까지) · 1px 언샤프(45m 까지 — 결·줄눈을 또렷하게)
//   2026-10-26: 이 패스는 후처리(색 보정·LUT) 뒤에 그린다(EnvSetup.OutlineFeature) — 앞이면 Lift 가 먹선을 띄우고 시간대 색이 물듦
//   · 실내는 모서리 문턱 × _HaenginCreaseK(0.67). 0 이면 예전 그대로(비교 사진).
Shader "Haengin/ScreenOutline"
{
    Properties
    {
        _Strength ("Strength", Range(0, 1)) = 1
        _NearWidth ("Width at 4m px (1080p)", Float) = 2.4
        _WidthPow ("Width falloff power", Float) = 0.55
        _FarWidth ("Min width px", Float) = 0.55
        _FadeStart ("Fade start m", Float) = 55
        _FadeEnd ("Fade end m", Float) = 80
        _DepthRel ("Silhouette (relative)", Float) = 0.022
        _DepthAbs ("Silhouette (m)", Float) = 0.03
        _CreaseInv ("Crease (inverse depth, relative)", Float) = 0.0045
        _Grain ("Paper grain", Range(0, 0.2)) = 0.035
        _Stain ("Brush stain", Range(0, 0.2)) = 0.05
        _HandNear ("Hand-drawn texture fade start m", Float) = 8
        _HandFar ("Hand-drawn texture fade end m", Float) = 22
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

            float _Strength, _NearWidth, _WidthPow, _FarWidth, _FadeStart, _FadeEnd, _DepthRel, _DepthAbs, _CreaseInv, _Grain, _Stain, _HandNear, _HandFar;
            float _HaenginInkOn, _HaenginCreaseK, _HaenginFineK; float4 _HaenginInkColor;   // _HaenginInk 는 A′ 잉크 모드(InkMode) 가 이미 float 로 씀
            float Lum(half3 c) { return dot(c, half3(0.2126, 0.7152, 0.0722)); }

            float Raw(float2 uv) { return SampleSceneDepth(uv); }
            float Eye(float raw)
            {
                #if UNITY_REVERSED_Z
                    if (raw <= 0.00001) return 100000.0;
                #else
                    if (raw >= 0.99999) return 100000.0;
                #endif
                return LinearEyeDepth(raw, _ZBufferParams);
            }
            float H3(float3 p) { p = frac(p * 0.3183099 + 0.1); p *= 17.0; return frac(p.x * p.y * p.z * (p.x + p.y + p.z)); }
            float N3(float3 x)
            {
                float3 i = floor(x), f = frac(x); f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(H3(i), H3(i + float3(1, 0, 0)), f.x), lerp(H3(i + float3(0, 1, 0)), H3(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(H3(i + float3(0, 0, 1)), H3(i + float3(1, 0, 1)), f.x), lerp(H3(i + float3(0, 1, 1)), H3(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 c = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                float raw0 = Raw(uv);
                float d0 = Eye(raw0);
                if (d0 > _FadeEnd || _Strength <= 0.001) return c;
                bool look = _HaenginInkOn > 0.5;
                float3 wp = ComputeWorldSpacePosition(uv, raw0, UNITY_MATRIX_I_VP);
                // ── 손그림 결(근경 면): 종이 결 + 붓 얼룩, 월드 좌표에 붙임
                float hand = 1.0 - smoothstep(_HandNear, _HandFar, d0);
                if (hand > 0.001)
                {
                    float g = N3(wp * 23.0) * 0.55 + N3(wp * 61.0) * 0.45;                        // 종이 결(4·1.6cm)
                    float st = N3(wp * 0.55 + 3.1) * 0.6 + N3(wp * 1.7 + 7.7) * 0.4;              // 붓 얼룩(1~2m)
                    float stain = smoothstep(0.56, 0.74, st);
                    float mul = 1.0 + (g - 0.5) * 2.0 * _Grain - stain * _Stain;
                    c.rgb *= lerp(1.0, mul, hand);
                }
                // ── 선: 굵기 = 거리별(1080p 기준 → 해상도 비례), 1px 아래는 진하기로
                float res = _ScreenParams.y / 1080.0;
                float near = look ? _NearWidth * (2.0 / 2.4) * (0.8 + 0.4 * N3(wp * 0.9 + 5.3)) : _NearWidth;     // ③ 굵기 2.0 · 붓 압력 ±20%
                float wpx = max(_FarWidth, near * pow(4.0 / max(d0, 4.0), _WidthPow));
                float alpha = saturate(wpx);                                   // 1px 미만이면 옅게
                float w = max(wpx, 1.0) * res;
                float fade = 1.0 - smoothstep(_FadeStart, _FadeEnd, d0);
                float2 px = w * (_ScreenParams.zw - 1.0);
                float dl = Eye(Raw(uv - float2(px.x, 0))), dr = Eye(Raw(uv + float2(px.x, 0)));
                float dd = Eye(Raw(uv - float2(0, px.y))), du = Eye(Raw(uv + float2(0, px.y)));
                float thr = _DepthRel * d0 + _DepthAbs;
                // 실루엣: 이웃이 더 멀면(앞 물체의 가장자리) — 뒤쪽 화소에는 안 그려 선이 두 번 생기지 않게
                float sil = max(max(step(thr, dl - d0), step(thr, dr - d0)), max(step(thr, dd - d0), step(thr, du - d0)));
                // 모서리: 역깊이 2차 차분(평면 = 0). 먼 쪽(배경) 이웃이 섞인 곳은 실루엣이 맡으므로 이웃이 너무 멀면 뺌
                float i0 = 1.0 / d0, il = 1.0 / dl, ir = 1.0 / dr, idn = 1.0 / dd, iu = 1.0 / du;
                float near4 = step(abs(dl - d0), thr * 4.0) * step(abs(dr - d0), thr * 4.0) * step(abs(dd - d0), thr * 4.0) * step(abs(du - d0), thr * 4.0);
                float ck = look && _HaenginCreaseK > 0.01 ? _HaenginCreaseK : 1.0;
                float cth = _CreaseInv * ck * i0;
                float crease = max(step(cth, abs(il + ir - 2.0 * i0)), step(cth, abs(idn + iu - 2.0 * i0))) * near4;
                float edge = saturate(max(sil, crease * 0.9)) * fade * alpha * _Strength;
                half3 ink = half3(0.0103, 0.0070, 0.0080);   // #1A1417 선형(예전)
                if (look)
                {
                    ink = lerp(c.rgb * 0.07, _HaenginInkColor.rgb * 0.8, 0.5);
                    // 색 경계 잔선: 이웃(1px 십자) 중 가장 밝은 것보다 이 화소가 뚜렷이 어두우면(창틀·벽돌 줄·간판 테두리처럼 깊이 차 없는 경계) — 어두운 쪽 한 줄만
                    float fineFade = 1.0 - smoothstep(14.0, 26.0, d0);
                    float sharpFade = 1.0 - smoothstep(20.0, 45.0, d0);
                    if (sharpFade > 0.001)
                    {
                        float2 p1 = res * (_ScreenParams.zw - 1.0);
                        half3 cr = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv + float2(p1.x, 0)).rgb, cl = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv - float2(p1.x, 0)).rgb;
                        half3 cu = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv + float2(0, p1.y)).rgb, cd = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv - float2(0, p1.y)).rgb;
                        // 또렷하게(언샤프 1px): 프레임은 결·줄눈·잎 가장자리가 또렷한 그림 — 평평하게 번진 결을 살림(디테일 밀도)
                        half3 avg = (cr + cl + cu + cd) * 0.25;
                        c.rgb = max(0, c.rgb + (c.rgb - avg) * (0.85 * sharpFade));
                        // 색 경계 잔선: 이웃(1px 십자) 중 가장 밝은 것보다 이 화소가 뚜렷이 어두우면(창틀·벽돌 줄·간판 테두리처럼 깊이 차 없는 경계) — 어두운 쪽 한 줄만
                        if (fineFade > 0.001 && edge < 0.5)
                        {
                            float l0 = Lum(c.rgb);
                            float lm = max(max(Lum(cr), Lum(cl)), max(Lum(cu), Lum(cd)));
                            float contrast = (lm - l0) / max(lm, 0.02);
                            float fk = _HaenginFineK > 0.01 ? _HaenginFineK : 1.0;      // 실내는 타일·벽지 줄이 다 잔선이 되어 줄임(StageLights 0.35)
                            float fine = smoothstep(0.13, 0.36, contrast) * fineFade * 0.75 * fk;
                            edge = max(edge, fine * _Strength);
                        }
                    }
                }
                c.rgb = lerp(c.rgb, ink, edge);
                return c;
            }
            ENDHLSL
        }
    }
}
