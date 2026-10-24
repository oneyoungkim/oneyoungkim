// 행인1의 메인이벤트 — 원경 카드(docs/10_맵_아트패스.md 8장): 조명·안개 없음, 깊이 쓰기 없음.
// 색 = lerp(lerp(lerp(하늘, 먼, R), 가운데, G), 가까운, B) × lerp(1, detail, lines), 알파 = A. 밤엔 가운데 층에 창 불빛 점(시드 고정).
// 4차(2026-10-24): ① 층 텍스처 아랫줄(투명 테두리)을 피해 uv.y ≥ 0.012 로 집어 높은 곳에서 내려다봐도 지평선 아래가 '가까운 층'으로 이어짐
//   (3차의 회색 띠 + 크림색 빈 띠 원인) ② 하늘 = 지평선 띠색 → 하늘색 → 천정색 3단 그라데이션을 붓 띠(7단, 경계만 부드럽게)로
//   ③ 붓 터치 구름(절차): 낮은 층운(지평선 2~14°, 가로로 길게 끌린 붓질) + 적운(6~30°, 뭉게 덩어리, 아랫면 그늘 2톤) — 카드 실루엣 뒤(하늘)에만
//   ④ 종이 결(아주 약하게). 방향은 카메라 기준 월드 방향(카드 4장 이음매와 서쪽 뒤집기에 상관없이 이어짐).
Shader "Haengin/SkyCard"
{
    Properties
    {
        _LayersTex ("Layers (R far, G mid, B near, A silhouette)", 2D) = "black" {}
        _DetailTex ("Detail (multiply)", 2D) = "white" {}
        _SkyColor ("Sky", Color) = (0.914, 0.878, 0.8, 1)
        _HorizonColor ("Horizon band", Color) = (0.94, 0.89, 0.80, 1)
        _FarColor ("Far", Color) = (0.639, 0.612, 0.565, 1)
        _MidColor ("Mid", Color) = (0.788, 0.757, 0.698, 1)
        _NearColor ("Near", Color) = (0.431, 0.416, 0.392, 1)
        _ZenithColor ("Zenith", Color) = (0.83, 0.79, 0.69, 1)
        _CloudLit ("Cloud lit", Color) = (0.98, 0.97, 0.95, 1)
        _CloudShade ("Cloud shade", Color) = (0.85, 0.84, 0.86, 1)
        _Cover ("Cloud cover", Range(0, 1)) = 0.5
        _Lines ("Lines", Range(0, 1)) = 0.7
        _Dots ("Night window dots", Range(0, 1)) = 0
        _Flip ("Flip U", Float) = 0
    }
    SubShader
    {
        // 4차: 불투명 맨 끝(AlphaTest+49)에 그린다 — 건물·땅이 이미 깊이를 써서 가려진 화소는 깊이 검사로 빠짐(절차 구름 잡음이 보이는 하늘에만 돈다)
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "AlphaTest+49" "IgnoreProjector" = "True" }
        ZWrite Off ZTest LEqual Cull Off Blend Off
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
            half4 _SkyColor, _HorizonColor, _FarColor, _MidColor, _NearColor, _ZenithColor, _CloudLit, _CloudShade;
            float _Cover, _Lines, _Dots, _Flip;
            CBUFFER_END
            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 dir : TEXCOORD1; };
            V Vert(A a)
            {
                V o; float3 wp = TransformObjectToWorld(a.pos.xyz);
                o.pos = TransformWorldToHClip(wp); o.uv = a.uv;
                o.dir = wp - TransformObjectToWorld(float3(0, 0, 0));   // 카드 중심 = 카메라 자리(SkyCards.Follow)
                return o;
            }
            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p); float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }
            float Fbm(float2 p)
            {
                float s = 0, a = 0.5;
                for (int k = 0; k < 5; k++) { s += a * Noise(p); p = p * 2.03 + float2(17.1, 9.3); a *= 0.5; }
                return s;
            }
            // 둘레를 이어 붙이는 방위각 좌표(카드 이음매·360° 끝에서 끊기지 않게: 원 위 두 성분으로 잡음)
            float2 Circ(float az, float r) { return float2(cos(az), sin(az)) * r; }
            half4 Frag(V i) : SV_Target
            {
                float2 uv = i.uv;
                uv.x = _Flip > 0.5 ? 1.0 - uv.x : uv.x;
                float2 suv = float2(uv.x, max(uv.y, 0.012));                 // 아랫줄 투명 테두리 피함 → 지평선 아래는 가까운 층이 이어짐
                half4 L = SAMPLE_TEXTURE2D(_LayersTex, sampler_LayersTex, suv);
                half d = SAMPLE_TEXTURE2D(_DetailTex, sampler_DetailTex, suv).r;
                half3 c = lerp(lerp(lerp(_SkyColor.rgb, _FarColor.rgb, L.r), _MidColor.rgb, L.g), _NearColor.rgb, L.b);
                c *= lerp(1.0, d, _Lines * lerp(0.3, 1.0, saturate(L.g + L.b)));   // 먼 산의 빗금은 옅게(하늘에 비처럼 보이지 않게)
                float2 cell = floor(suv * float2(640.0, 274.0));
                float h = Hash(cell);
                float dot1 = step(0.985, h) * L.g * (1.0 - L.b) * _Dots;
                c = lerp(c, half3(0.95, 0.80, 0.50), dot1 * 0.9);

                float3 dir = normalize(i.dir);
                float el = asin(clamp(dir.y, -1.0, 1.0));                  // 고도(라디안)
                float az = atan2(dir.x, dir.z);
                float elD = degrees(el);
                // 하늘 그라데이션: 지평선 띠 → 하늘 → 천정, 붓 띠 7단
                float t = saturate(elD / 55.0);
                float tb = t * 7.0; float tq = (floor(tb) + smoothstep(0.30, 0.70, frac(tb))) / 7.0;
                tq = lerp(t, tq, 0.65);
                half3 sky = lerp(_HorizonColor.rgb, _SkyColor.rgb, smoothstep(0.0, 0.16, tq));
                sky = lerp(sky, _ZenithColor.rgb, smoothstep(0.16, 1.0, tq));
                // 종이 결
                float grain = Noise(float2(az * 900.0, elD * 16.0)) * 0.6 + Noise(float2(az * 2600.0, elD * 47.0)) * 0.4;
                sky *= 1.0 + (grain - 0.5) * 0.025;

                // ── 붓 터치 구름(하늘 쪽에만 — 카드 실루엣 뒤)
                float cover = _Cover;
                // 층운: 가로로 길게 끌린 붓질(2~14°)
                float bandS = smoothstep(1.5, 3.5, elD) * (1.0 - smoothstep(9.0, 15.0, elD));
                float2 ps = float2(0, elD * 0.55) + Circ(az, 3.2);
                float ns = Fbm(float2(ps.x * 1.0 + ps.y * 0.15, ps.y * 1.0) * float2(1.0, 1.0) + Circ(az, 1.1));
                float streak = Noise(float2(az * 140.0, elD * 3.2));                          // 마른 붓 결(가로 줄)
                float ms = smoothstep(0.62 - cover * 0.16, 0.66 - cover * 0.16, ns + (streak - 0.5) * 0.10) * bandS;
                // 적운: 뭉게 덩어리(6~30°), 아랫면은 평평하게 잘림
                float bandC = smoothstep(5.0, 9.0, elD) * (1.0 - smoothstep(24.0, 32.0, elD));
                float2 pc = Circ(az, 2.4) + float2(0, elD * 0.16);
                float nc = Fbm(pc * 1.6 + float2(4.2, 1.3));
                float base = smoothstep(0.0, 4.0, elD - 7.0 - Noise(Circ(az, 5.0)) * 6.0);   // 아랫면
                float mc = smoothstep(0.60 - cover * 0.14, 0.63 - cover * 0.14, nc * (0.55 + 0.45 * base) + 0.08 * base) * bandC;
                float m = saturate(max(ms, mc));
                // 2톤: 같은 잡음을 조금 아래(고도 −)에서 다시 재서 위가 더 진하면 = 아랫면 → 그늘
                float ncL = Fbm((Circ(az, 2.4) + float2(0, (elD - 1.6) * 0.16)) * 1.6 + float2(4.2, 1.3));
                float shadeC = step(ncL + 0.015, nc) * mc;
                float shadeS = ms * (1.0 - smoothstep(0.66 - cover * 0.16, 0.74 - cover * 0.16, ns)) * 0.7;   // 층운은 얇은 가장자리가 그늘색
                float shade = saturate(max(shadeC, shadeS));
                half3 cloud = lerp(_CloudLit.rgb, _CloudShade.rgb, shade);
                // 구름 가장자리 아주 얇은 붓 자국(조금 진하게)
                float rimC = (smoothstep(0.58 - cover * 0.14, 0.62 - cover * 0.14, nc * (0.55 + 0.45 * base) + 0.08 * base) - mc / max(bandC, 1e-3)) * bandC;
                cloud = lerp(cloud, _CloudShade.rgb * 0.92, saturate(rimC) * 0.5);
                sky = lerp(sky, cloud, m * _CloudLit.a);

                return half4(lerp(sky, c, L.a), 1);
            }
            ENDHLSL
        }
    }
}
