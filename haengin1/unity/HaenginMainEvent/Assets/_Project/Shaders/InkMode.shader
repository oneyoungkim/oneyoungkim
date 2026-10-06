// 행인1의 메인이벤트 — A′ 흑백 노탄 + 해칭(docs/09_M3_버티컬슬라이스_설계.md 2-2 InkMode, FUJIMOTO 1-5).
// URP Full Screen Pass Renderer Feature 'InkMode' 가 후처리 뒤에 그린다(CutSetup 이 PC·Mobile 렌더러에 붙임). 전역 _HaenginInk 0 이면 화면 그대로.
// 밝기 4단: 먹(#1A1417) · 교차 해칭 · 한 방향 해칭 · 종이(#F4EFE6). 해칭 = 화면 45° 선, 간격 7px(1080p 기준으로 해상도에 맞춰 늘림).
Shader "Haengin/InkMode"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        ZWrite Off ZTest Always Blend Off Cull Off
        Pass
        {
            Name "InkMode"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _HaenginInk;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 c = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);
                if (_HaenginInk <= 0.001) return c;
                half l = dot(c.rgb, half3(0.299, 0.587, 0.114));
                float2 px = input.texcoord * _ScreenParams.xy * (1080.0 / max(_ScreenParams.y, 1.0));
                float gap = 7.0;
                half h1 = step(frac((px.x + px.y) / gap), 0.32);
                half h2 = step(frac((px.x - px.y) / gap), 0.32);
                const half3 ink = half3(0.102, 0.078, 0.090);
                const half3 paper = half3(0.957, 0.937, 0.902);
                half3 o;
                if (l < 0.20) o = ink;
                else if (l < 0.38) o = lerp(paper, ink, max(h1, h2));
                else if (l < 0.58) o = lerp(paper, ink, h1);
                else o = paper;
                c.rgb = lerp(c.rgb, o, saturate(_HaenginInk));
                return c;
            }
            ENDHLSL
        }
    }
}
