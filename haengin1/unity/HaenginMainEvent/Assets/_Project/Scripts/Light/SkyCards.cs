// 행인1의 메인이벤트 — 원경 카드·구름·달(docs/10_맵_아트패스.md 8장). Zone1/Sky 아래, EnvSetup 이 만든다.
// 카메라 자리를 따라다니는 반지름 600m 원통 위 카드 4장(북·동·남 + 서 = 북 카드 좌우 뒤집음) + 구름 판(낮·노을) + 초승달(밤).
// 색은 시간대마다 바꾼다(DayLight.Apply 가 부름 — sky_palette.json 값, LightingPreset 의 하늘색과 맞춤).
using UnityEngine;

namespace Haengin
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class SkyCards : MonoBehaviour
    {
        [System.Serializable]
        public struct Palette { public DayPart Part; public Color Sky, Far, Mid, Near; public float Lines, Dots; public Color Cloud; public bool Clouds, Moon; public Color Zenith; }

        public Renderer[] Arcs = new Renderer[0];      // 북·동·남·서(서 = 북 재질 + Flip)
        public Renderer[] Clouds = new Renderer[0];
        public Renderer Moon;
        public float Radius = 600f;
        public Palette[] Palettes = Default();

        public static SkyCards Instance { get; private set; }
        public DayPart Part { get; private set; } = DayPart.Day;

        static readonly int IdSky = Shader.PropertyToID("_SkyColor"), IdFar = Shader.PropertyToID("_FarColor"), IdMid = Shader.PropertyToID("_MidColor"),
                            IdNear = Shader.PropertyToID("_NearColor"), IdZenith = Shader.PropertyToID("_ZenithColor"), IdLines = Shader.PropertyToID("_Lines"), IdDots = Shader.PropertyToID("_Dots"), IdTint = Shader.PropertyToID("_Tint");
        MaterialPropertyBlock mpb;

        void OnEnable() { Instance = this; Apply(Part); }
        void OnDisable() { if (Instance == this) Instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }

        static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

        public static Palette[] Default() => new[]
        {
            new Palette { Part = DayPart.Dawn, Sky = Hex("#CBD1D6"), Zenith = Hex("#9FA9BB"), Far = Hex("#8E97A6"), Mid = Hex("#AAB0B9"), Near = Hex("#5B616C"), Lines = 0.6f, Dots = 0.35f, Cloud = new Color(0.85f, 0.87f, 0.92f, 0.6f), Clouds = false, Moon = false },
            new Palette { Part = DayPart.Day,  Sky = Hex("#E9E0CC"), Zenith = Hex("#D3C9B0"), Far = Hex("#8F877A"), Mid = Hex("#BBB2A2"), Near = Hex("#6E6A64"), Lines = 0.7f, Dots = 0f, Cloud = new Color(0.98f, 0.97f, 0.95f, 0.9f), Clouds = true, Moon = false },
            new Palette { Part = DayPart.Dusk, Sky = Hex("#EBC9A6"), Zenith = Hex("#B59A96"), Far = Hex("#9E7A72"), Mid = Hex("#BC957F"), Near = Hex("#5A4A4C"), Lines = 0.7f, Dots = 0.2f, Cloud = new Color(1f, 0.84f, 0.68f, 0.85f), Clouds = true, Moon = false },
            new Palette { Part = DayPart.Night, Sky = Hex("#2A3345"), Zenith = Hex("#151B2C"), Far = Hex("#3A4360"), Mid = Hex("#303956"), Near = Hex("#151A2B"), Lines = 0.5f, Dots = 1f, Cloud = new Color(0.3f, 0.34f, 0.5f, 0.5f), Clouds = false, Moon = true },
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
