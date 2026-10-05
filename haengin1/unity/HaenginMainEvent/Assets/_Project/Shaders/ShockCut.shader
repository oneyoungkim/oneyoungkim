// 행인1의 메인이벤트 — 쇼크 컷(08 5-1·5-5, FUJIMOTO 8장 9: 1~2프레임 흑백 반전).
// URP Full Screen Pass Renderer Feature 가 후처리 뒤에 이 재질로 화면 전체를 한 번 그린다(CombatSetup 이 PC·Mobile 렌더러에 붙임).
// 켜고 끄기는 전역 값 _HaenginShock(0~1) — 재질 에셋을 바꾸지 않아 실행 중 바꿔도 에셋이 더러워지지 않는다. 0 이면 화면 그대로.
Shader "Haengin/ShockCut"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        ZWrite Off ZTest Always Blend Off Cull Off
        Pass
        {
            Name "ShockCut"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _HaenginShock;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 c = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);
                // 밝기를 뒤집고 대비를 세게(먹 흑백 반전)
                half l = dot(c.rgb, half3(0.299, 0.587, 0.114));
                half inv = saturate((1.0 - l - 0.5) * 1.6 + 0.5);
                c.rgb = lerp(c.rgb, inv.xxx, saturate(_HaenginShock));
                return c;
            }
            ENDHLSL
        }
    }
}
