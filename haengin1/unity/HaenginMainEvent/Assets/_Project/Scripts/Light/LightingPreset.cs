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
    }
}
