// 행인1의 메인이벤트 — 원경 카드·구름·달(docs/10_맵_아트패스.md 8장). Zone1/Sky 아래, EnvSetup 이 만든다.
// 카메라 자리를 따라다니는 반지름 600m 원통 위 카드 4장(북·동·남 + 서 = 북 카드 좌우 뒤집음) + 구름 판(낮·노을) + 초승달(밤).
// 색은 시간대마다 바꾼다(DayLight.Apply 가 부름 — sky_palette.json 값, LightingPreset 의 하늘색과 맞춤).
// 4차(2026-10-24): 구름은 카드 셰이더의 절차 붓 구름(층운·적운, 실루엣 뒤)으로 — 구름 판은 쓰지 않음(Clouds 비움). 지평선 띠색·구름 그늘색·구름 양 추가,
//   먼 층 색은 공기 원근(푸른 회색·옅게)으로, 그림 선(Lines)은 0.35 로 눌러 회색 줄무늬로 안 보이게.
using UnityEngine;

namespace Haengin
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class SkyCards : MonoBehaviour
    {
        [System.Serializable]
        public struct Palette { public DayPart Part; public Color Sky, Far, Mid, Near; public float Lines, Dots; public Color Cloud; public bool Clouds, Moon; public Color Zenith; public Color Horizon, CloudShade; public float Cover; }

        public Renderer[] Arcs = new Renderer[0];      // 북·동·남·서(서 = 북 재질 + Flip)
        public Renderer[] Clouds = new Renderer[0];
        public Renderer Moon;
        public float Radius = 600f;
        public Palette[] Palettes = Default();

        public static SkyCards Instance { get; private set; }
        public DayPart Part { get; private set; } = DayPart.Day;

        static readonly int IdSky = Shader.PropertyToID("_SkyColor"), IdFar = Shader.PropertyToID("_FarColor"), IdMid = Shader.PropertyToID("_MidColor"),
                            IdNear = Shader.PropertyToID("_NearColor"), IdZenith = Shader.PropertyToID("_ZenithColor"), IdLines = Shader.PropertyToID("_Lines"), IdDots = Shader.PropertyToID("_Dots"), IdTint = Shader.PropertyToID("_Tint"),
                            IdHorizon = Shader.PropertyToID("_HorizonColor"), IdCloudLit = Shader.PropertyToID("_CloudLit"), IdCloudShade = Shader.PropertyToID("_CloudShade"), IdCover = Shader.PropertyToID("_Cover");
        MaterialPropertyBlock mpb;

        void OnEnable() { Instance = this; Apply(Part); }
        void OnDisable() { if (Instance == this) Instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }

        static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

        public static Palette[] Default() => new[]
        {
            new Palette { Part = DayPart.Dawn, Sky = Hex("#CBD1D6"), Zenith = Hex("#A6B3C4"), Horizon = Hex("#E6D5BF"), Far = Hex("#97A0AE"), Mid = Hex("#ADB3BB"), Near = Hex("#9EA3AC"), Lines = 0.35f, Dots = 0.35f,
                          Cloud = new Color(0.93f, 0.90f, 0.88f, 0.9f), CloudShade = Hex("#B3B6C2"), Cover = 0.35f, Clouds = false, Moon = false },
            new Palette { Part = DayPart.Day,  Sky = Hex("#E9E0CC"), Zenith = Hex("#D9D4C3"), Horizon = Hex("#EFE6D3"), Far = Hex("#A8ADAE"), Mid = Hex("#C7C0B0"), Near = Hex("#B8B1A2"), Lines = 0.35f, Dots = 0f,
                          Cloud = new Color(0.985f, 0.975f, 0.95f, 1f), CloudShade = Hex("#D2CFC8"), Cover = 0.5f, Clouds = false, Moon = false },
            new Palette { Part = DayPart.Dusk, Sky = Hex("#EBC9A6"), Zenith = Hex("#A996A2"), Horizon = Hex("#F3BE86"), Far = Hex("#A3837E"), Mid = Hex("#BC9886"), Near = Hex("#A88A7E"), Lines = 0.4f, Dots = 0.2f,
                          Cloud = new Color(1f, 0.86f, 0.70f, 1f), CloudShade = Hex("#B98D8E"), Cover = 0.55f, Clouds = false, Moon = false },
            new Palette { Part = DayPart.Night, Sky = Hex("#2A3345"), Zenith = Hex("#151B2C"), Horizon = Hex("#3B4258"), Far = Hex("#323B57"), Mid = Hex("#2B3451"), Near = Hex("#222A42"), Lines = 0.35f, Dots = 1f,
                          Cloud = new Color(0.25f, 0.29f, 0.40f, 0.8f), CloudShade = Hex("#1E2436"), Cover = 0.3f, Clouds = false, Moon = true },
        };

        public Palette Of(DayPart p)
        {
            foreach (var x in Palettes) if (x.Part == p) return x;
            return Palettes.Length > 0 ? Palettes[0] : default;
        }

        /// 시간대 색 적용(DayLight.Apply 가 부른다). 재질 에셋은 건드리지 않음(MaterialPropertyBlock).
        public void Apply(DayPart part) => Apply(part, null, null);
        /// sky·zenith 를 주면 프리셋(LightingPreset) 값으로 덮어씀
        public void Apply(DayPart part, Color? sky, Color? zenith)
        {
            Part = part;
            var p = Of(part);
            if (sky.HasValue) p.Sky = sky.Value;
            if (zenith.HasValue && zenith.Value.a > 0f) p.Zenith = zenith.Value;
            mpb ??= new MaterialPropertyBlock();
            foreach (var r in Arcs)
            {
                if (r == null) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetColor(IdSky, p.Sky); mpb.SetColor(IdFar, p.Far); mpb.SetColor(IdMid, p.Mid); mpb.SetColor(IdNear, p.Near); mpb.SetColor(IdZenith, p.Zenith);
                mpb.SetFloat(IdLines, p.Lines); mpb.SetFloat(IdDots, p.Dots);
                mpb.SetColor(IdHorizon, p.Horizon.a > 0f ? p.Horizon : p.Sky); mpb.SetColor(IdCloudLit, p.Cloud); mpb.SetColor(IdCloudShade, p.CloudShade.a > 0f ? p.CloudShade : p.Sky * 0.9f); mpb.SetFloat(IdCover, p.Cover);
                r.SetPropertyBlock(mpb);
            }
            foreach (var r in Clouds)
            {
                if (r == null) continue;
                r.enabled = p.Clouds;
                r.GetPropertyBlock(mpb); mpb.SetColor(IdTint, p.Cloud); r.SetPropertyBlock(mpb);
            }
            if (Moon != null) Moon.enabled = p.Moon;
        }

        /// 카메라 자리(수평·눈높이)로 따라감 — 시차 없이 늘 같은 거리
        public void Follow(Camera cam)
        {
            if (cam == null) return;
            var p = cam.transform.position;
            transform.position = new Vector3(p.x, p.y, p.z);
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            Follow(cam);
        }
    }
}
