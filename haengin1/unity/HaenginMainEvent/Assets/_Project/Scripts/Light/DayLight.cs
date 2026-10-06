// 행인1의 메인이벤트 — 시간대 조명 적용(docs/09_M3_버티컬슬라이스_설계.md 2-1): 장면 시작 때 프리셋을 1.0초에 걸쳐 바꾼다(먹 닦기로 덮인 동안이면 바로).
// 바꾸는 것: 방향광(방향·색·세기·그림자) · 주변광(삼색) · 하늘(카메라 배경색) · 안개 · 필름 볼륨 스플릿 톤(볼륨의 '실행 중 복제본' — 에셋은 그대로) · 빛 자리(LightSpot).
// 에셋·장면 파일은 건드리지 않는다(프리셋을 바꿔도 Zone1 결정적 저장이 같음 — D07).
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Haengin
{
    [DisallowMultipleComponent]
    public sealed class DayLight : MonoBehaviour
    {
        public LightingPreset Dawn, Day, Dusk, Night;
        public Light Sun;
        public Volume Film;
        public float BlendSecs = 1f;

        public static DayLight Instance { get; private set; }
        public LightingPreset Current { get; private set; }
        public DayPart Part => Current != null ? Current.Part : DayPart.Day;
        public int Applied { get; private set; }

        struct Snap
        {
            public Quaternion SunRot; public Color SunColor; public float SunI, Shadow;
            public Color ASky, AEq, AGround, Sky, Fog; public float FogStart, FogEnd;
            public Color SplitS, SplitH; public float SplitB;
        }

        Snap from, to;
        float t = 1f;
        SplitToning split;
        bool profileCloned;

        void OnEnable() { Instance = this; }
        void OnDisable() { if (Instance == this) Instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }

        public LightingPreset Of(DayPart p) => p switch { DayPart.Dawn => Dawn, DayPart.Dusk => Dusk, DayPart.Night => Night, _ => Day };

        void Find()
        {
            if (Sun == null)
            {
                foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l.type == LightType.Directional && l.gameObject.scene == gameObject.scene) { Sun = l; break; }
            }
            if (Film == null)
            {
                foreach (var v in FindObjectsByType<Volume>(FindObjectsSortMode.None))
                    if (v.isGlobal && v.gameObject.scene == gameObject.scene) { Film = v; break; }
            }
            if (Film != null && !profileCloned && Application.isPlaying)
            {
                // volume.profile = 실행 중 복제본(sharedProfile 에셋은 그대로)
                var prof = Film.profile;
                profileCloned = true;
                if (prof != null) prof.TryGet(out split);
            }
        }

        Snap Now()
        {
            var s = new Snap
            {
                SunRot = Sun != null ? Sun.transform.rotation : Quaternion.identity,
                SunColor = Sun != null ? Sun.color : Color.white,
                SunI = Sun != null ? Sun.intensity : 1f,
                Shadow = Sun != null ? Sun.shadowStrength : 0.7f,
                ASky = RenderSettings.ambientSkyColor, AEq = RenderSettings.ambientEquatorColor, AGround = RenderSettings.ambientGroundColor,
                Sky = Camera.main != null ? Camera.main.backgroundColor : RenderSettings.fogColor,
                Fog = RenderSettings.fogColor, FogStart = RenderSettings.fogStartDistance, FogEnd = RenderSettings.fogEndDistance,
            };
            if (split != null) { s.SplitS = split.shadows.value; s.SplitH = split.highlights.value; s.SplitB = split.balance.value; }
            return s;
        }

        static Snap Of(LightingPreset p) => new Snap
        {
            SunRot = Quaternion.Euler(p.SunPitch, p.SunYaw + 180f, 0f),
            SunColor = p.SunColor, SunI = p.SunIntensity, Shadow = p.ShadowStrength,
            ASky = p.AmbientSky, AEq = p.AmbientEquator, AGround = p.AmbientGround,
            Sky = p.Sky, Fog = p.Fog, FogStart = p.FogStart, FogEnd = p.FogEnd,
            SplitS = p.SplitShadows, SplitH = p.SplitHighlights, SplitB = p.SplitBalance,
        };

        /// 프리셋 적용. instant = 바로(먹 닦기로 덮인 동안·테스트)
        public void Apply(DayPart part, bool instant)
        {
            var p = Of(part);
            if (p == null) return;
            Find();
            Current = p;
            Applied++;
            from = Now();
            to = Of(p);
            t = instant ? 1f : 0f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            Write(instant ? 1f : 0f);
            // 빛 자리는 바로(켜짐은 보간하지 않음)
            foreach (var s in LightSpot.All)
            {
                if (s == null) continue;
                bool on; Color c = p.Sodium; float i = p.LampIntensity;
                switch (s.Kind)
                {
                    case LightKind.Street: on = p.StreetLamps; break;
                    case LightKind.Security: on = p.SecurityLamps; i *= 0.7f; break;
                    case LightKind.Vending: on = p.Vending; c = new Color(0.85f, 0.92f, 1f); i *= 0.6f; break;
                    case LightKind.ConvAwning: on = p.ConvAwning; c = p.ConvTeal; i *= 1.1f; break;
                    case LightKind.GukbapWindow: on = p.GukbapWindow; c = new Color(1f, 0.82f, 0.55f); i *= 0.8f; break;
                    case LightKind.ShopSign: on = p.ShopSigns; c = new Color(1f, 0.9f, 0.7f); i *= 0.5f; break;
                    default: on = s.Threshold < p.Windows; c = new Color(1f, 0.86f, 0.6f); i = 1f; break;
                }
                s.Set(on, c, i);
            }
            Debug.Log($"[M3] 조명: {p.name} ({part}) · {(instant ? "바로" : BlendSecs + "초")} · 켠 빛 {CountLit()}");
        }

        public int CountLit()
        {
            int n = 0;
            foreach (var s in LightSpot.All) if (s != null && s.Lit && s.Kind != LightKind.Window) n++;
            return n;
        }

        public int CountWindows()
        {
            int n = 0;
            foreach (var s in LightSpot.All) if (s != null && s.Lit && s.Kind == LightKind.Window) n++;
            return n;
        }

        void Update()
        {
            if (t >= 1f) return;
            t = Mathf.Min(1f, t + UiKit.RealDt / Mathf.Max(0.01f, BlendSecs));
            Write(Mathf.SmoothStep(0f, 1f, t));
        }

        void Write(float k)
        {
            if (Sun != null)
            {
                Sun.transform.rotation = Quaternion.Slerp(from.SunRot, to.SunRot, k);
                Sun.color = Color.Lerp(from.SunColor, to.SunColor, k);
                Sun.intensity = Mathf.Lerp(from.SunI, to.SunI, k);
                Sun.shadowStrength = Mathf.Lerp(from.Shadow, to.Shadow, k);
            }
            RenderSettings.ambientSkyColor = Color.Lerp(from.ASky, to.ASky, k);
            RenderSettings.ambientEquatorColor = Color.Lerp(from.AEq, to.AEq, k);
            RenderSettings.ambientGroundColor = Color.Lerp(from.AGround, to.AGround, k);
            var sky = Color.Lerp(from.Sky, to.Sky, k);
            foreach (var cam in Camera.allCameras) if (cam.clearFlags == CameraClearFlags.SolidColor) cam.backgroundColor = sky;
            if (Camera.main != null && Camera.main.clearFlags == CameraClearFlags.SolidColor) Camera.main.backgroundColor = sky;
            RenderSettings.fogColor = Color.Lerp(from.Fog, to.Fog, k);
            RenderSettings.fogStartDistance = Mathf.Lerp(from.FogStart, to.FogStart, k);
            RenderSettings.fogEndDistance = Mathf.Lerp(from.FogEnd, to.FogEnd, k);
            if (split != null)
            {
                split.active = true;
                split.shadows.overrideState = split.highlights.overrideState = split.balance.overrideState = true;
                split.shadows.value = Color.Lerp(from.SplitS, to.SplitS, k);
                split.highlights.value = Color.Lerp(from.SplitH, to.SplitH, k);
                split.balance.value = Mathf.Lerp(from.SplitB, to.SplitB, k);
            }
        }

        public static float LStar(Color c)
        {
            var lin = c.linear;
            float y = 0.2126f * lin.r + 0.7152f * lin.g + 0.0722f * lin.b;
            return y > 0.008856f ? 116f * Mathf.Pow(y, 1f / 3f) - 16f : 903.3f * y;
        }
    }
}
