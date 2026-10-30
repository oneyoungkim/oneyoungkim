// 행인1의 메인이벤트 — 체육관 조명·시간대(docs/12_체육관_설계.md 3장·5-2): 형광등 3열 15 + 케이지 스포트 4 + 계단 1 + 사무실 1
// 아침 = 남쪽 2열(열 1·2) · 낮/저녁 = 전체 · 밤 = 가운데 열(z 6.0) + 케이지 스포트만(주광) · 새벽 = 닫힘(전부 끔)
// URP 는 실시간 Area 가 없어 형광등은 아래로 향한 넓은 스포트(120°)로 대신하고 발광 재질(M_Fluorescent)의 색을 함께 바꾼다(베이크 없음 — 12 문서 '구현 결과').
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Haengin
{
    [DisallowMultipleComponent]
    public sealed class GymLights : MonoBehaviour
    {
        [System.Serializable] public sealed class Fluor { public Light Light; public Renderer Tube; public int Row; }
        public List<Fluor> Fluors = new List<Fluor>();
        public List<Light> CageSpots = new List<Light>();
        public Light Stair, Office;
        public Material FluorMat;
        public static readonly Color FluorColor = new Color(0.918f, 0.941f, 0.925f);
        public float FluorIntensity = 2.8f, SpotIntensity = 2.0f;
        /// 실내 주변광(바탕 Zone1 의 시간대 주변광 대신 — 지하라 하늘빛 없음). 2차 패스: 스타일 프레임 L* 34/46/40/28 · 채도 0.11~0.19 에 맞춤
        public Color AmbientMorning = new Color(0.56f, 0.56f, 0.55f), AmbientDay = new Color(0.74f, 0.73f, 0.70f), AmbientEvening = new Color(0.66f, 0.63f, 0.58f), AmbientNight = new Color(0.40f, 0.40f, 0.43f);
        [Header("QA 2차 A5(2026-10-26) — 밤 캐릭터 채움빛(가운데 열 형광등만 켜진 밤, 레슨 자리의 시우가 검은 실루엣이던 것)")]
        [Tooltip("밤 주변광(예전 0.40·0.40·0.43 푸른 회색 → 따뜻한 회갈로 조금 올림 — 14 문서 '그늘 회갈')")] public Color AmbientNightWarm = new Color(0.47f, 0.45f, 0.43f);
        [Tooltip("채움빛 색 = 따뜻한 회갈(그늘이 검정이 아니라 회갈로 읽히게)")] public Color NightFillColor = new Color(0.86f, 0.78f, 0.70f);
        public float NightFillIntensity = 2.4f, NightFillRange = 2.6f;
        Light fill; Transform fillPlayer;
        [Header("2026-10-30 캐릭터 채움(대표 — 얼굴이 어둡고 납작) · 체육관 색감은 그대로, 캐릭터만")]
        [Tooltip("UTS _Unlit_Intensity: 해가 꺼진 체육관에서 캐릭터 = 텍스처 × (주변광 × 이 값 + 형광등). 재질 값 0 이면 머리 위 형광등만 받아 앞면이 어두움. 텍스처 색을 곱하므로 회색으로 뜨지 않음(_GI_Intensity 와 다름)")]
        public float CharFill = 0.7f;
        readonly HashSet<Renderer> charFilled = new HashSet<Renderer>();
        MaterialPropertyBlock charMpb;
        float charScan;
        [Tooltip("캐릭터 그늘 방향(UTS 내장 빛 방향, 몸 기준): 앞 위 왼쪽 — 해 방향을 따르면 그늘 경계가 코 한가운데로 지나감(얼굴이 세로로 갈림)")]
        public Vector2 CharLightXY = new Vector2(-0.05f, 0.09f);
        static readonly int IdUnlit = Shader.PropertyToID("_Unlit_Intensity"), IdBld = Shader.PropertyToID("_Is_BLD"), IdBldX = Shader.PropertyToID("_Offset_X_Axis_BLD"), IdBldY = Shader.PropertyToID("_Offset_Y_Axis_BLD"), IdBldZ = Shader.PropertyToID("_Inverse_Z_Axis_BLD");
        /// 시험: 밤 채움빛(밤에만 켜짐)
        public Light Fill => fill;
        [Tooltip("실내 볼륨 채도(Zone1 필름 볼륨 위에 덮음)")] public float Saturation = -38f, Contrast = -4f;
        [Header("룩데브(docs/14, 2026-10-26) — 그늘 회갈·LUT·먹선·옅은 안개")]
        public Texture2D Lut;
        [Range(0f, 1f)] public float LutContribution = 1f;
        public Color Ink = new Color(0.184f, 0.165f, 0.157f);
        public Color HazeColor = new Color(0.80f, 0.78f, 0.74f);
        public float HazeStart = 4f, HazeEnd = 22f;
        [Range(0f, 1f)] public float HazeStrength = 0.06f;
        [Tooltip("그늘 들어 올림(Lift w)")] public float Lift = 0.005f;
        [Tooltip("그늘 따뜻한 회갈 스플릿 톤(0.5 회색 = 중립)")] public Color SplitShadow = new Color(0.53f, 0.50f, 0.48f);
        Volume vol; ColorAdjustments adjust; SplitToning split; LiftGammaGain lgg; ColorLookup lookup; bool counted;
        Light sun; float sunWas = -1f; bool fogWas; bool fogSaved;
        public GymPart Part { get; private set; } = GymPart.Day;
        public int LitCount { get; private set; }
        float flicker;

        public static GymPart PartOfHour(int hour) => hour >= 4 && hour < 7 ? GymPart.Dawn : hour >= 7 && hour < 11 ? GymPart.Morning : hour >= 11 && hour < 17 ? GymPart.Day : hour >= 17 && hour < 21 ? GymPart.Evening : GymPart.Night;
        public static string Name(GymPart p) => p switch { GymPart.Dawn => "새벽", GymPart.Morning => "아침", GymPart.Day => "낮", GymPart.Evening => "저녁", _ => "밤" };

        void EnsureVolume()
        {
            if (vol != null) return;
            vol = gameObject.AddComponent<Volume>();
            vol.isGlobal = true; vol.priority = 50f; vol.weight = 1f;
            var prof = ScriptableObject.CreateInstance<VolumeProfile>(); prof.name = "Gym_Film(runtime)";
            adjust = prof.Add<ColorAdjustments>(true); split = prof.Add<SplitToning>(true); lgg = prof.Add<LiftGammaGain>(true);
            split.shadows.Override(SplitShadow); split.highlights.Override(new Color(0.5f, 0.5f, 0.5f)); split.balance.Override(0f);
            lgg.lift.Override(new Vector4(1f, 1f, 1f, Lift)); lgg.gamma.Override(new Vector4(1f, 1f, 1f, 0f)); lgg.gain.Override(new Vector4(1f, 1f, 1f, 0f));
            lookup = prof.Add<ColorLookup>(true);
            RefreshLut();
            adjust.saturation.Override(Saturation); adjust.contrast.Override(Contrast); adjust.postExposure.Override(0f); adjust.colorFilter.Override(Color.white);
            vol.profile = prof;
        }

        void FindSun()
        {
            if (sun != null) return;
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional && l.gameObject.scene != gameObject.scene) { sun = l; sunWas = l.intensity; break; }
        }

        public void RefreshLut()
        {
            if (lookup == null) return;
            lookup.texture.Override(Lut);
            lookup.contribution.Override(Lut != null && LookdevFlags.Lut ? LutContribution : 0f);
        }

        void PushLook()
        {
            Shader.SetGlobalColor(DayLight.IdInkColor, Ink.linear);
            var hc = HazeColor.linear; hc.a = HazeStrength;
            Shader.SetGlobalColor(DayLight.IdHaze, hc);
            Shader.SetGlobalVector(DayLight.IdHazeRange, new Vector4(HazeStart, HazeEnd, 0f, 0f));
            Shader.SetGlobalFloat(DayLight.IdDapple, 0f);
            Shader.SetGlobalFloat(DayLight.IdCrease, 0.67f);
            Shader.SetGlobalFloat(DayLight.IdFine, 0.8f);
        }

        public void Apply(GymPart p)
        {
            Part = p;
            if (!counted) { DayLight.IndoorDepth++; counted = true; }
            PushLook();
            LitCount = 0;
            EnsureVolume();
            FindSun();
            if (!fogSaved) { fogWas = RenderSettings.fog; fogSaved = true; }
            RenderSettings.fog = false;                       // 지하 — 바탕 장면 안개·해는 들어오지 않는다
            if (sun != null) sun.intensity = 0f;
            adjust.saturation.Override(p == GymPart.Night ? Saturation + 6f : Saturation);
            adjust.postExposure.Override(p == GymPart.Night ? 0.45f : p == GymPart.Morning ? 0.3f : p == GymPart.Evening ? 0.35f : 0.4f);   // 2차 측정: 밤 L*24 → 28 목표
            adjust.colorFilter.Override(p == GymPart.Evening ? new Color(1f, 0.97f, 0.92f) : p == GymPart.Night ? new Color(0.95f, 0.96f, 1f) : Color.white);
            foreach (var f in Fluors)
            {
                bool on = p == GymPart.Day || p == GymPart.Evening || (p == GymPart.Morning && f.Row <= 1) || (p == GymPart.Night && f.Row == 1);
                if (f.Light != null) { f.Light.enabled = on; f.Light.intensity = p == GymPart.Night ? FluorIntensity * 1.5f : FluorIntensity; f.Light.color = FluorColor; }
                if (f.Tube != null)
                {
                    var mats = f.Tube.materials;
                    foreach (var m in mats) if (m != null) { m.SetColor("_EmissionColor", on ? FluorColor * 1.6f : Color.black); m.SetColor("_Emissive_Color", on ? FluorColor * 1.4f : Color.black); if (on) m.EnableKeyword("_EMISSION"); else m.DisableKeyword("_EMISSION"); }
                }
                if (on) LitCount++;
            }
            bool spots = p == GymPart.Evening || p == GymPart.Night || p == GymPart.Day;
            for (int i = 0; i < CageSpots.Count; i++) if (CageSpots[i] != null) { CageSpots[i].enabled = spots; CageSpots[i].intensity = p == GymPart.Night ? SpotIntensity * 1.4f : SpotIntensity; CageSpots[i].shadows = i == 0 ? LightShadows.Soft : LightShadows.None; }
            if (Stair != null) Stair.enabled = p != GymPart.Dawn;
            if (Office != null) Office.enabled = p == GymPart.Day || p == GymPart.Morning || p == GymPart.Evening;
            if (spots) LitCount += CageSpots.Count;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = p == GymPart.Night ? AmbientNightWarm : p == GymPart.Dawn ? AmbientNight : p == GymPart.Morning ? AmbientMorning : p == GymPart.Evening ? AmbientEvening : AmbientDay;
            EnsureFill();
            fill.enabled = p == GymPart.Night;
            fill.color = NightFillColor; fill.intensity = NightFillIntensity; fill.range = NightFillRange;
            PushCharFill();
            Debug.Log($"[Gym] 조명 {Name(p)}: 형광등 {LitCount - (spots ? CageSpots.Count : 0)}/{Fluors.Count} · 케이지 스포트 {(spots ? CageSpots.Count : 0)} · 사무실 {(Office != null && Office.enabled)}");
        }

        void EnsureFill()
        {
            if (fill != null) return;
            var go = new GameObject("밤 캐릭터 채움빛");
            go.transform.SetParent(transform, false);
            fill = go.AddComponent<Light>();
            fill.type = LightType.Point; fill.shadows = LightShadows.None; fill.renderMode = LightRenderMode.ForcePixel; fill.enabled = false;
        }

        /// 채움빛 자리: 시우 가슴에서 지금 카메라 쪽으로 0.9m · 위로 0.35m(카메라가 보는 앞면을 비춤 — 바닥은 반지름 2.6m 안에서만 옅게)
        void PlaceFill()
        {
            if (fill == null || !fill.enabled) return;
            if (fillPlayer == null) { var m = FindAnyObjectByType<PlayerMotor>(); if (m != null) fillPlayer = m.transform; }
            if (fillPlayer == null) return;
            var chest = fillPlayer.position + Vector3.up * 1.25f;
            var cam = Camera.main;
            var dir = cam != null ? cam.transform.position - chest : fillPlayer.forward;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) dir = fillPlayer.forward;
            fill.transform.position = chest + dir.normalized * 0.9f + Vector3.up * 0.35f;
        }

        void LateUpdate()
        {
            if (sun != null && sun.intensity > 0f) sun.intensity = 0f;
            if (RenderSettings.fog) RenderSettings.fog = false;
            if (counted) PushLook();
            PlaceFill();
            if (counted && Time.unscaledTime >= charScan) { charScan = Time.unscaledTime + 1f; PushCharFill(); }   // 새로 생긴 몸(스파링 고준 등)·맞을 때 지워진 블록 다시
        }

        /// 캐릭터 채움: 체육관 장면의 몸 + 시우 + 전투 몸(꺼진 것 포함 — 켜질 때 번쩍이지 않게)
        public void PushCharFill()
        {
            charMpb ??= new MaterialPropertyBlock();
            if (fillPlayer == null) { var m = FindAnyObjectByType<PlayerMotor>(); if (m != null) fillPlayer = m.transform; }
            foreach (var r in FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var mat = r.sharedMaterial;
                if (mat == null || !mat.HasProperty(IdUnlit)) continue;
                if (r.gameObject.scene != gameObject.scene && (fillPlayer == null || !r.transform.IsChildOf(fillPlayer)) && r.GetComponentInParent<Fighter>(true) == null) continue;
                r.GetPropertyBlock(charMpb);
                charMpb.SetFloat(IdUnlit, CharFill);
                bool on = CharFill > 0f;
                charMpb.SetFloat(IdBld, on ? 1f : mat.GetFloat(IdBld));
                charMpb.SetFloat(IdBldX, on ? CharLightXY.x : mat.GetFloat(IdBldX));
                charMpb.SetFloat(IdBldY, on ? CharLightXY.y : mat.GetFloat(IdBldY));
                charMpb.SetFloat(IdBldZ, on ? 0f : mat.GetFloat(IdBldZ));
                r.SetPropertyBlock(charMpb);
                charFilled.Add(r);
            }
        }

        /// 체육관을 나갈 때: 시우·전투 몸을 원래대로(표정 칸을 쓰는 얼굴은 블록을 지우면 안 됨)
        void ClearCharFill()
        {
            if (charMpb == null) return;
            foreach (var r in charFilled)
            {
                if (r == null) continue;
                var mat = r.sharedMaterial;
                var rig = r.GetComponentInParent<FaceRig>(true);
                if (rig != null && mat != null)
                {
                    r.GetPropertyBlock(charMpb);
                    charMpb.SetFloat(IdUnlit, mat.GetFloat(IdUnlit)); charMpb.SetFloat(IdBld, mat.GetFloat(IdBld));
                    charMpb.SetFloat(IdBldX, mat.GetFloat(IdBldX)); charMpb.SetFloat(IdBldY, mat.GetFloat(IdBldY)); charMpb.SetFloat(IdBldZ, mat.GetFloat(IdBldZ));
                    r.SetPropertyBlock(charMpb);
                }
                else r.SetPropertyBlock(null);
            }
            charFilled.Clear();
        }

        void OnDestroy()
        {
            if (counted) { DayLight.IndoorDepth = Mathf.Max(0, DayLight.IndoorDepth - 1); counted = false; }
            if (sun != null && sunWas >= 0f) sun.intensity = sunWas;
            if (fogSaved) RenderSettings.fog = fogWas;
            ClearCharFill();
        }

        void Update()
        {
            // 계단 맨 형광등 깜빡임(밤 0.5Hz)
            if (Stair == null || Part != GymPart.Night) return;
            flicker += UiKit.RealDt;
            float ph = Mathf.Repeat(flicker, 2f);
            Stair.intensity = ph < 0.08f || (ph > 0.3f && ph < 0.36f) ? 0.3f : 1.4f;
        }
    }
}
