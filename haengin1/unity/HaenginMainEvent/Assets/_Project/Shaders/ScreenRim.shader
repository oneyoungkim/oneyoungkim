// 행인1의 메인이벤트 — 7차 맵 아트 패스(2026-10-25): 하늘 역광 테두리. 하늘(깊이 없음)과 맞닿은 실루엣 바로 안쪽(먹선 다음 몇 화소)에 하늘빛 띠를 칠한다.
// 인물·나무 잎 덩어리(스텐실 1)에도 칠해야 해서 외곽선 패스와 따로(스텐실 없음) — URP Full Screen Pass 'Rim', 외곽선 다음.
// 색·세기 = 전역 _HaenginRimColor · _HaenginRim(DayLight 가 시간대마다). 가는 전선은 먹선 폭 안이라 거의 안 칠해짐.
// 룩데브 ④(docs/14 4-4, 2026-10-26): 깊이 안개(대비가 눌린 밝은 크림~회청 — 먼 곳 대비 압축)도 여기서(스텐실 없음 → 나무 잎 덩어리·먼 사람에도).
//   t = smoothstep(시작, 끝, 깊이) × 세기, 하늘(깊이 없음)은 뺌. 전역 _HaenginHaze(rgb 선형 + a 세기) · _HaenginHazeRange(시작, 끝) · _HaenginHazeOn.
Shader "Haengin/ScreenRim"
{
    Properties
    {
        _NearWidth ("Ink width at 4m px (1080p) — rim starts after this", Float) = 2.4
        _WidthPow ("Width falloff power", Float) = 0.55
        _RimPx ("Rim band px (1080p, at 4m)", Float) = 3.2
        _SkyDepth ("Sky = eye depth beyond m", Float) = 1500
        _MaxDepth ("Rim only nearer than m", Float) = 90
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        ZWrite Off ZTest Always Blend Off Cull Off
        Pass
        {
            Name "Rim"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            float _NearWidth, _WidthPow, _RimPx, _SkyDepth, _MaxDepth;
            float4 _HaenginRimColor; float _HaenginRim;
            float4 _HaenginHaze, _HaenginHazeRange; float _HaenginHazeOn;
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
            float SkyAt(float2 uv, float2 o)
            {
                // 8 방향 중 하늘인 곳의 비율(위쪽 하늘에 무게 — 역광은 주로 머리·수관 윗선)
                float s = 0.0;
                s += step(_SkyDepth, Eye(uv + float2(0, o.y))) * 1.6;
                s += step(_SkyDepth, Eye(uv + float2(o.x, o.y) * 0.707)) * 1.2;
                s += step(_SkyDepth, Eye(uv + float2(-o.x, o.y) * 0.707)) * 1.2;
                s += step(_SkyDepth, Eye(uv + float2(o.x, 0))) * 0.8;
                s += step(_SkyDepth, Eye(uv + float2(-o.x, 0))) * 0.8;
                s += step(_SkyDepth, Eye(uv + float2(o.x, -o.y) * 0.707)) * 0.3;
                s += step(_SkyDepth, Eye(uv + float2(-o.x, -o.y) * 0.707)) * 0.3;
                return saturate(s / 2.0);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 c = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                float d0 = Eye(uv);
                // ④ 깊이 안개(하늘 빼고) — 색 쪽으로 섞어 먼 곳 대비를 누름
                if (_HaenginHazeOn > 0.5 && _HaenginHaze.a > 0.001 && d0 < _SkyDepth)
                {
                    float t = smoothstep(_HaenginHazeRange.x, max(_HaenginHazeRange.y, _HaenginHazeRange.x + 1.0), d0) * _HaenginHaze.a;
                    c.rgb = lerp(c.rgb, _HaenginHaze.rgb, t);
                }
                if (_HaenginRim <= 0.001) return c;
                if (d0 > _MaxDepth) return c;
                float res = _ScreenParams.y / 1080.0;
                float k = pow(4.0 / max(d0, 4.0), _WidthPow);
                float inner = max(_NearWidth * k, 1.0) * res + 1.5 * res;     // 먹선(인물은 헐 외곽선)을 지나서
                float band = max(_RimPx * pow(4.0 / max(d0, 4.0), 0.35), 1.2) * res;
                float2 px = _ScreenParams.zw - 1.0;
                float outer = SkyAt(uv, px * (inner + band));
                float inn = SkyAt(uv, px * inner * 0.85);
                float rim = saturate(outer - inn * 0.85);
                rim *= 1.0 - smoothstep(_MaxDepth * 0.6, _MaxDepth, d0);
                // 하늘빛은 화면색을 밝히는 쪽으로(어두운 실루엣일수록 띠가 또렷)
                half3 lit = lerp(c.rgb, _HaenginRimColor.rgb, 0.75);
                c.rgb = lerp(c.rgb, max(c.rgb, lit), saturate(rim * _HaenginRim));
                return c;
            }
            ENDHLSL
        }
    }
}
