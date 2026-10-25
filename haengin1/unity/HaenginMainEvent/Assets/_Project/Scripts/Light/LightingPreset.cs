// 행인1의 메인이벤트 — 시간대 조명 프리셋(docs/09_M3_버티컬슬라이스_설계.md 2-1 표 · 3-5): 새벽 · 낮 · 노을 · 밤
// 에셋 Settings/Light_<시간대>.asset — StorySetup 이 없을 때만 만든다(손으로 고친 값 보존. 기본값을 바꿨으면 지우고 다시).
using UnityEngine;

namespace Haengin
{
    [CreateAssetMenu(menuName = "Haengin/LightingPreset")]
    public sealed class LightingPreset : ScriptableObject
    {
        public DayPart Part;
        [Header("해(방향광) — 해가 없는 시간엔 달·하늘빛으로 약하게")]
        [Tooltip("고도(°)")] public float SunPitch = 48f;
        [Tooltip("방위(°, 0 = 북쪽에서 오는 빛, 180 = 남쪽)")] public float SunYaw = 160f;
        public Color SunColor = new Color(1f, 0.96f, 0.9f);
        public float SunIntensity = 1.2f;
        public float ShadowStrength = 0.7f;
        [Header("주변광(삼색)")]
        public Color AmbientSky = new Color(0.78f, 0.76f, 0.72f);
        public Color AmbientEquator = new Color(0.66f, 0.64f, 0.60f);
        public Color AmbientGround = new Color(0.46f, 0.43f, 0.40f);
        [Header("하늘·안개(하늘보다 안개를 L* 6 이상 어둡게 — 먼 실루엣이 하늘에 안 녹게)")]
        public Color Sky = new Color(0.933f, 0.890f, 0.820f);
        public Color Fog = new Color(0.831f, 0.808f, 0.765f);
        public float FogStart = 40f, FogEnd = 380f;
        [Header("필름 볼륨 스플릿 톤")]
        public Color SplitShadows = new Color(0.184f, 0.290f, 0.353f);
        public Color SplitHighlights = new Color(0.953f, 0.851f, 0.690f);
        [Range(-100f, 100f)] public float SplitBalance = 0f;
        [Header("인공 빛(점광, 그림자 없음)")]
        public bool StreetLamps, SecurityLamps, Vending, ConvAwning, GukbapWindow, ShopSigns;
        public Color Sodium = new Color(1f, 0.62f, 0.28f);
        public Color ConvTeal = new Color(0.55f, 0.95f, 0.88f);
        public float LampIntensity = 6f;
        [Tooltip("창문 불 켜짐 비율(키트 창 — 시드 고정)")] [Range(0f, 1f)] public float Windows;
        [Header("색 보정(필름 볼륨 — 스타일 프레임 팔레트, 2026-10-24 2차 패스)")]
        [Range(-100f, 100f)] public float Saturation = -15f;
        [Range(-100f, 100f)] public float Contrast = -6f;
        [Tooltip("Lift(어두운 쪽)·Gamma(중간)·Gain(밝은 쪽) — xyz 색, w 전체")] public Vector4 Lift = new Vector4(1f, 1f, 1f, 0f), Gamma = new Vector4(1f, 1f, 1f, 0f), Gain = new Vector4(1f, 1f, 1f, 0f);
        [Tooltip("하늘 천정 색(원경 카드 그라데이션 위쪽)")] public Color Zenith = new Color(0.86f, 0.82f, 0.74f);
        [Header("룩데브(docs/14 4장, 2026-10-26)")]
        [Tooltip("시간대 LUT(URP Color Lookup 띠 1024×32, sRGB 끔·읽기 켬) — 필름 볼륨 맨 끝(HDR 등급 모드: 내부 등급 뒤)")] public Texture2D Lut;
        [Range(0f, 1f)] public float LutContribution = 1f;
        [Tooltip("먹선 색(시간대에 물들지 않게 — 프레임 선 색 실측, sRGB)")] public Color Ink = new Color(0.259f, 0.239f, 0.200f);
        [Tooltip("깊이 안개 색(sRGB) · 시작–끝 m · 세기")] public Color HazeColor = new Color(0.851f, 0.824f, 0.761f);
        public float HazeStart = 40f, HazeEnd = 220f;
        [Range(0f, 1f)] public float HazeStrength = 0.3f;
        [Tooltip("땅 햇빛 얼룩(잎 그림자 데칼) 세기 — 낮 1, 새벽·노을 약하게, 밤 0")] [Range(0f, 1f)] public float Dapple = 1f;
        [Tooltip("밤 빛 웅덩이 세기 배율(LightPool)")] public float PoolBoost = 1f;
    }
}
