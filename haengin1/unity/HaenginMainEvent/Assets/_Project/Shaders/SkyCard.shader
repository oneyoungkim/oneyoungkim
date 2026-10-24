// 행인1의 메인이벤트 — 원경 카드(docs/10_맵_아트패스.md 8장): 조명·안개 없음, 깊이 쓰기 없음.
// 색 = lerp(lerp(lerp(하늘, 먼, R), 가운데, G), 가까운, B) × lerp(1, detail, lines), 알파 = A. 밤엔 가운데 층에 창 불빛 점(시드 고정).
Shader "Haengin/SkyCard"
{
    Properties
    {
        _LayersTex ("Layers (R far, G mid, B near, A silhouette)", 2D) = "black" {}
        _DetailTex ("Detail (multiply)", 2D) = "white" {}
        _SkyColor ("Sky", Color) = (0.914, 0.878, 0.8, 1)
        _FarColor ("Far", Color) = (0.639, 0.612, 0.565, 1)
        _MidColor ("Mid", Color) = (0.788, 0.757, 0.698, 1)
        _NearColor ("Near", Color) = (0.431, 0.416, 0.392, 1)
        _Lines ("Lines", Range(0, 1)) = 0.7
        _Dots ("Night window dots", Range(0, 1)) = 0
        _Flip ("Flip U", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Background+20" "IgnoreProjector" = "True" }
        ZWrite Off ZTest LEqual Cull Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Name "SkyCard"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_LayersTex); SAMPLER(sampler_LayersTex);
            TEXTURE2D(_DetailTex); SAMPLER(sampler_DetailTex);
            CBUFFER_START(UnityPerMaterial)
            half4 _SkyColor, _FarColor, _MidColor, _NearColor;
            float _Lines, _Dots, _Flip;
            CBUFFER_END
            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            V Vert(A a) { V o; o.pos = TransformObjectToHClip(a.pos.xyz); o.uv = a.uv; return o; }
            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            half4 Frag(V i) : SV_Target
            {
                float2 uv = i.uv;
                uv.x = _Flip > 0.5 ? 1.0 - uv.x : uv.x;
                half4 L = SAMPLE_TEXTURE2D(_LayersTex, sampler_LayersTex, uv);
                half d = SAMPLE_TEXTURE2D(_DetailTex, sampler_DetailTex, uv).r;
                half3 c = lerp(lerp(lerp(_SkyColor.rgb, _FarColor.rgb, L.r), _MidColor.rgb, L.g), _NearColor.rgb, L.b);
                c *= lerp(1.0, d, _Lines);
                // 밤 창 불빛 점: 가운데 층(G)에만, 격자 셀당 하나, 시드 고정
                float2 cell = floor(uv * float2(640.0, 274.0));
                float h = Hash(cell);
                float dot1 = step(0.985, h) * L.g * (1.0 - L.b) * _Dots;
                c = lerp(c, half3(0.95, 0.80, 0.50), dot1 * 0.9);
                return half4(c, L.a);
            }
            ENDHLSL
        }
    }
}
