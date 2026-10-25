// 행인1의 메인이벤트 — 시간대 조명 적용(docs/09_M3_버티컬슬라이스_설계.md 2-1): 장면 시작 때 프리셋을 1.0초에 걸쳐 바꾼다(먹 닦기로 덮인 동안이면 바로).
// 바꾸는 것: 방향광(방향·색·세기·그림자) · 주변광(삼색) · 하늘(카메라 배경색) · 안개 · 필름 볼륨 스플릿 톤(볼륨의 '실행 중 복제본' — 에셋은 그대로) · 빛 자리(LightSpot).
// 에셋·장면 파일은 건드리지 않는다(프리셋을 바꿔도 Zone1 결정적 저장이 같음 — D07).
// 룩데브(docs/14, 2026-10-26): + 시간대 LUT(볼륨 Color Lookup — 연속 시계에서는 두 띠를 CPU 로 섞은 텍스처) · 먹선 색 · 깊이 안개 · 땅 햇빛 얼룩 · 밤 빛 웅덩이 배율(셰이더 전역 값).
//   실내(StageLights·GymLights)가 켜져 있으면 전역 값은 실내가 쓴다(IndoorDepth).
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
        /// 편집 모드 촬영(EnvSetup.Shots)에서도 필름 볼륨 스플릿 톤을 바꿀 때 켬(에셋은 그대로 — 복제본)
        public static bool EditModeFilm;
        static readonly int IdSignsOn = Shader.PropertyToID("_HaenginSignsOn"), IdNight = Shader.PropertyToID("_HaenginNight"), IdRim = Shader.PropertyToID("_HaenginRim"), IdRimColor = Shader.PropertyToID("_HaenginRimColor");
        public static readonly int IdInkColor = Shader.PropertyToID("_HaenginInkColor"), IdHaze = Shader.PropertyToID("_HaenginHaze"), IdHazeRange = Shader.PropertyToID("_HaenginHazeRange"),
            IdDapple = Shader.PropertyToID("_HaenginDapple"), IdPool = Shader.PropertyToID("_HaenginPoolBoost"), IdCrease = Shader.PropertyToID("_HaenginCreaseK"), IdFine = Shader.PropertyToID("_HaenginFineK");
        /// 켜져 있는 실내 조명 수(StageLights·GymLights) — 0 보다 크면 먹선·안개 전역 값은 실내가 쓴다
        public static int IndoorDepth;
        public LightingPreset Current { get; private set; }
        public DayPart Part => Current != null ? Current.Part : DayPart.Day;
        public int Applied { get; private set; }

        struct Snap
        {
            public Quaternion SunRot; public Color SunColor; public float SunI, Shadow;
            public Color ASky, AEq, AGround, Sky, Fog; public float FogStart, FogEnd;
            public Color SplitS, SplitH; public float SplitB;
            public float Sat, Con; public Vector4 Lift, Gamma, Gain;
            public Color Ink, HazeC; public float HazeS, HazeE, HazeK, Dapple, Pool; public Texture2D Lut; public float LutK;
        }

        Snap from, to;
        float t = 1f;
        SplitToning split;
        ColorAdjustments adjust;
        LiftGammaGain lgg;
        ColorLookup lookup;
        bool profileCloned;
        readonly LutBlend lutBlend = new LutBlend();
        float lastK = 1f;

        void OnEnable() { Instance = this; }
        void OnDisable() { if (Instance == this) Instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; IndoorDepth = 0; }

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
            if (Film != null && !profileCloned && (Application.isPlaying || EditModeFilm))
            {
                // volume.profile = 실행 중 복제본(sharedProfile 에셋은 그대로)
                var prof = Film.profile;
                profileCloned = true;
                if (prof != null)
                {
                    prof.TryGet(out split);
                    if (!prof.TryGet(out adjust)) adjust = prof.Add<ColorAdjustments>(true);
                    if (!prof.TryGet(out lgg)) lgg = prof.Add<LiftGammaGain>(true);
                    if (!prof.TryGet(out lookup)) lookup = prof.Add<ColorLookup>(true);
                }
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
            if (adjust != null) { s.Sat = adjust.saturation.value; s.Con = adjust.contrast.value; }
            if (lgg != null) { s.Lift = lgg.lift.value; s.Gamma = lgg.gamma.value; s.Gain = lgg.gain.value; }
            // 룩데브 값은 보간 도중이면 지금 적용된 쪽(to)에서 이어 감
            s.Ink = to.Ink; s.HazeC = to.HazeC; s.HazeS = to.HazeS; s.HazeE = to.HazeE; s.HazeK = to.HazeK; s.Dapple = to.Dapple; s.Pool = to.Pool; s.Lut = to.Lut; s.LutK = to.LutK;
            if (to.Lut == null && Current != null) { var c = Of(Current); s.Ink = c.Ink; s.HazeC = c.HazeC; s.HazeS = c.HazeS; s.HazeE = c.HazeE; s.HazeK = c.HazeK; s.Dapple = c.Dapple; s.Pool = c.Pool; s.Lut = c.Lut; s.LutK = c.LutK; }
            return s;
        }

        static Snap Of(LightingPreset p) => new Snap
        {
            SunRot = Quaternion.Euler(p.SunPitch, p.SunYaw + 180f, 0f),
            SunColor = p.SunColor, SunI = p.SunIntensity, Shadow = p.ShadowStrength,
            ASky = p.AmbientSky, AEq = p.AmbientEquator, AGround = p.AmbientGround,
            Sky = p.Sky, Fog = p.Fog, FogStart = p.FogStart, FogEnd = p.FogEnd,
            SplitS = p.SplitShadows, SplitH = p.SplitHighlights, SplitB = p.SplitBalance,
            Sat = p.Saturation, Con = p.Contrast, Lift = p.Lift, Gamma = p.Gamma, Gain = p.Gain,
            Ink = p.Ink, HazeC = p.HazeColor, HazeS = p.HazeStart, HazeE = p.HazeEnd, HazeK = p.HazeStrength, Dapple = p.Dapple, Pool = p.PoolBoost, Lut = p.Lut, LutK = p.LutContribution,
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
            // 원경 카드·구름·달(docs/10 8장) — 하늘색과 같은 팔레트
            var sky = SkyCards.Instance ?? FindFirstObjectByType<SkyCards>();
            if (sky != null) sky.Apply(part, p.Sky, p.Zenith);
            // 5차(2026-10-25): 간판 면 조명 상자·빛 번짐(노을·밤) · 밤 전용 효과(날벌레·창 커튼 그림자) — 셰이더 전역 값
            Shader.SetGlobalFloat(IdSignsOn, p.ShopSigns ? 1f : 0f);
            Shader.SetGlobalFloat(IdNight, part == DayPart.Night ? 1f : 0f);
            // 7차: 하늘 역광 테두리(Haengin/ScreenRim) — 시간대마다 하늘 지평선 쪽 빛 색·세기
            var (rimC, rimK) = part switch
            {
                DayPart.Dawn => (new Color(0.93f, 0.86f, 0.80f), 0.55f),
                DayPart.Dusk => (new Color(1.0f, 0.78f, 0.52f), 0.8f),
                DayPart.Night => (new Color(0.52f, 0.58f, 0.80f), 0.28f),
                _ => (new Color(1.0f, 0.96f, 0.86f), 0.5f),
            };
            Shader.SetGlobalColor(IdRimColor, rimC); Shader.SetGlobalFloat(IdRim, rimK);
            LookdevFlags.Push();
            Debug.Log($"[M3] 조명: {p.name} ({part}) · {(instant ? "바로" : BlendSecs + "초")} · 켠 빛 {CountLit()}");
        }

        /// 오픈월드(docs/13 12-4 1단계): 세계 시계를 따라 이웃한 두 프리셋을 연속 보간(3월 혜화동 — 해 뜸 06:40·해 짐 18:40 근처).
        /// 빛 자리·하늘 카드·셰이더 전역 값은 더 가까운 시간대가 바뀔 때만 다시 칠한다
        public void ApplyClock(double minutes)
        {
            var (a, b, k) = PartsAt(minutes);
            var pa = Of(a); var pb = Of(b);
            if (pa == null || pb == null) return;
            Find();
            var dom = k < 0.5f ? a : b;
            if (Current == null || Current.Part != dom || !clockMode) { Apply(dom, true); clockMode = true; }
            from = Of(pa); to = Of(pb); t = 1f;
            Write(Mathf.SmoothStep(0f, 1f, k));
            ClockK = k; ClockFrom = a; ClockTo = b;
        }
        bool clockMode;
        public float ClockK { get; private set; }
        public DayPart ClockFrom { get; private set; }
        public DayPart ClockTo { get; private set; }

        /// 시각 → (앞 시간대, 뒤 시간대, 0~1)
        public static (DayPart, DayPart, float) PartsAt(double minutes)
        {
            double m = minutes % 1440.0;
            (double at, DayPart p)[] keys =
            {
                (0, DayPart.Night), (5 * 60 + 20, DayPart.Night), (6 * 60 + 10, DayPart.Dawn), (6 * 60 + 50, DayPart.Dawn), (7 * 60 + 40, DayPart.Day),
                (16 * 60 + 50, DayPart.Day), (17 * 60 + 50, DayPart.Dusk), (18 * 60 + 30, DayPart.Dusk), (19 * 60 + 20, DayPart.Night), (1440, DayPart.Night),
            };
            for (int i = 0; i < keys.Length - 1; i++)
            {
                if (m < keys[i].at || m > keys[i + 1].at) continue;
                double span = keys[i + 1].at - keys[i].at;
                float k = span <= 0 ? 0f : (float)((m - keys[i].at) / span);
                return (keys[i].p, keys[i + 1].p, keys[i].p == keys[i + 1].p ? 0f : k);
            }
            return (DayPart.Night, DayPart.Night, 0f);
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
            lastK = k;
            WriteLook(k);
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
            if (adjust != null)
            {
                adjust.active = true; adjust.saturation.overrideState = adjust.contrast.overrideState = true;
                adjust.saturation.value = Mathf.Lerp(from.Sat, to.Sat, k); adjust.contrast.value = Mathf.Lerp(from.Con, to.Con, k);
            }
            if (lgg != null)
            {
                lgg.active = true; lgg.lift.overrideState = lgg.gamma.overrideState = lgg.gain.overrideState = true;
                lgg.lift.value = Vector4.Lerp(from.Lift, to.Lift, k); lgg.gamma.value = Vector4.Lerp(from.Gamma, to.Gamma, k); lgg.gain.value = Vector4.Lerp(from.Gain, to.Gain, k);
            }
        }

        /// 룩데브 전역 값·LUT(실내가 켜져 있으면 LUT 만 — 실내 볼륨이 덮음)
        void WriteLook(float k)
        {
            if (IndoorDepth <= 0)
            {
                Shader.SetGlobalColor(IdInkColor, Color.Lerp(from.Ink, to.Ink, k).linear);
                var hc = Color.Lerp(from.HazeC, to.HazeC, k).linear; hc.a = Mathf.Lerp(from.HazeK, to.HazeK, k);
                Shader.SetGlobalColor(IdHaze, hc);
                Shader.SetGlobalVector(IdHazeRange, new Vector4(Mathf.Lerp(from.HazeS, to.HazeS, k), Mathf.Lerp(from.HazeE, to.HazeE, k), 0f, 0f));
                Shader.SetGlobalFloat(IdDapple, Mathf.Lerp(from.Dapple, to.Dapple, k));
                Shader.SetGlobalFloat(IdPool, Mathf.Lerp(from.Pool, to.Pool, k));
                Shader.SetGlobalFloat(IdCrease, 1f);
                Shader.SetGlobalFloat(IdFine, 1f);
            }
            if (lookup != null)
            {
                var tex = lutBlend.Get(from.Lut, to.Lut, k);
                lookup.active = true;
                lookup.texture.overrideState = lookup.contribution.overrideState = true;
                lookup.texture.value = tex;
                lookup.contribution.value = tex != null && LookdevFlags.Lut ? Mathf.Lerp(from.Lut != null ? from.LutK : to.LutK, to.Lut != null ? to.LutK : from.LutK, k) : 0f;
            }
        }

        /// LookdevFlags 를 바꾼 뒤(비교 사진)
        public void RefreshLut() { Find(); WriteLook(lastK); }

        public static float LStar(Color c)
        {
            var lin = c.linear;
            float y = 0.2126f * lin.r + 0.7152f * lin.g + 0.0722f * lin.b;
            return y > 0.008856f ? 116f * Mathf.Pow(y, 1f / 3f) - 16f : 903.3f * y;
        }
    }

    /// 두 LUT 띠를 섞은 텍스처(볼륨 텍스처 값은 보간되지 않아 연속 시계에서 0.5 에 툭 바뀌던 것) — 읽기 켠 띠만, 0.02 단위로 다시 섞음
    public sealed class LutBlend
    {
        Texture2D tex, la, lb; Color32[] pa, pb, pc; float lastK = -1f;
        public Texture Get(Texture2D a, Texture2D b, float k)
        {
            if (a == null && b == null) return null;
            if (a == null) a = b; if (b == null) b = a;
            if (a == b || k <= 0.01f) return a;
            if (k >= 0.99f) return b;
            if (!a.isReadable || !b.isReadable || a.width != b.width || a.height != b.height) return k < 0.5f ? a : b;
            if (tex == null || tex.width != a.width)
            {
                tex = new Texture2D(a.width, a.height, TextureFormat.RGBA32, false, true) { name = "LutBlend(runtime)", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
                pc = new Color32[a.width * a.height]; lastK = -1f;
            }
            if (la != a) { pa = a.GetPixels32(); la = a; lastK = -1f; }
            if (lb != b) { pb = b.GetPixels32(); lb = b; lastK = -1f; }
            if (lastK >= 0f && Mathf.Abs(k - lastK) < 0.02f) return tex;
            int w = Mathf.RoundToInt(k * 256f);
            for (int i = 0; i < pc.Length; i++)
            {
                var x = pa[i]; var y = pb[i];
                pc[i] = new Color32((byte)((x.r * (256 - w) + y.r * w) >> 8), (byte)((x.g * (256 - w) + y.g * w) >> 8), (byte)((x.b * (256 - w) + y.b * w) >> 8), 255);
            }
            tex.SetPixels32(pc); tex.Apply(false);
            lastK = k;
            return tex;
        }
    }
}
