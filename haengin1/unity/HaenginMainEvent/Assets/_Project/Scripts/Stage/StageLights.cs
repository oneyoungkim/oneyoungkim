// 행인1의 메인이벤트 — 실내 무대 조명(09 9단계, 2026-10-25): 키트 무대(국밥집·시우네·하루편의점·교실)가 켜져 있는 동안
// 바탕 Zone1 의 해·안개를 끄고(지붕 아래라 해가 바닥을 하얗게 날리던 것) 실내 주변광 + 무대 불(키트 소켓 Sock_Light_*) + 가벼운 실내 볼륨으로.
// 체육관 GymLights 와 같은 방식(시간대별 형광등은 아직 없음 — 새 오픈월드 설계 뒤).
// 룩데브(docs/14, 2026-10-26): 실내 볼륨이 바깥 시간대 색 보정을 모두 덮는다(스플릿 톤·Lift/Gamma/Gain 중립 — 그늘이 바깥 시간대 색으로 물들던 것) + 장소 LUT
//   + 먹선 색(실내 #2F2A28)·모서리 잔선 더(0.67)·실내 옅은 안개 — 셰이더 전역 값(DayLight.IndoorDepth 로 바깥 값을 막음).
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Haengin
{
    [DisallowMultipleComponent]
    public sealed class StageLights : MonoBehaviour
    {
        public Color Ambient = new Color(0.50f, 0.48f, 0.45f);
        public float Saturation = -12f, Contrast = 10f, Exposure = 0f;
        public Color Filter = Color.white;
        [Header("룩데브")]
        public Texture2D Lut;
        [Range(0f, 1f)] public float LutContribution = 1f;
        public Color Ink = new Color(0.184f, 0.165f, 0.157f);
        public Color HazeColor = new Color(0.85f, 0.81f, 0.74f);
        public float HazeStart = 3f, HazeEnd = 18f;
        [Range(0f, 1f)] public float HazeStrength = 0.08f;
        [Tooltip("그늘 들어 올림(Lift w)")] public float Lift = 0f;
        [Tooltip("밤(22~05시)·새벽(05~07시) 주변광 배율 — 집처럼 형광등이 아닌 곳은 밤에 스탠드만 남게")] public float NightAmbient = 1f, DawnAmbient = 1f;
        /// 해를 남김(창으로만 들어옴 — 키트 벽·천장이 그림자를 드리움). false 면 해를 끔(창 없는 곳)
        public bool KeepSun = true;
        Light sun; float sunWas = -1f; bool fogWas, saved;
        AmbientMode ambModeWas; Color ambWas;
        Volume vol; ColorLookup lookup; bool counted;
        Light[] ceiling; float[] ceilingI;      // 천장 등(키트 Sock_Light_Ceiling) — 밤 배율이 0.5 아래면 끔(스탠드·창빛만 — 프레임 s04)

        void OnEnable()
        {
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional && l.gameObject.scene != gameObject.scene) { sun = l; sunWas = l.intensity; break; }
            if (!saved) { fogWas = RenderSettings.fog; ambModeWas = RenderSettings.ambientMode; ambWas = RenderSettings.ambientLight; saved = true; }
            if (vol == null)
            {
                vol = gameObject.AddComponent<Volume>();
                vol.isGlobal = true; vol.priority = 50f; vol.weight = 1f;
                var prof = ScriptableObject.CreateInstance<VolumeProfile>(); prof.name = "Stage_Indoor(runtime)";
                var adj = prof.Add<ColorAdjustments>(true);
                adj.saturation.Override(Saturation); adj.contrast.Override(Contrast); adj.postExposure.Override(Exposure); adj.colorFilter.Override(Filter);
                var split = prof.Add<SplitToning>(true);
                split.shadows.Override(new Color(0.5f, 0.5f, 0.5f)); split.highlights.Override(new Color(0.5f, 0.5f, 0.5f)); split.balance.Override(0f);
                var lgg = prof.Add<LiftGammaGain>(true);
                lgg.lift.Override(new Vector4(1f, 1f, 1f, Lift)); lgg.gamma.Override(new Vector4(1f, 1f, 1f, 0f)); lgg.gain.Override(new Vector4(1f, 1f, 1f, 0f));
                lookup = prof.Add<ColorLookup>(true);
                vol.profile = prof;
                RefreshLut();
            }
            if (!counted) { DayLight.IndoorDepth++; counted = true; }
            Apply();
        }

        public void RefreshLut()
        {
            if (lookup == null) return;
            lookup.texture.Override(Lut);
            lookup.contribution.Override(Lut != null && LookdevFlags.Lut ? LutContribution : 0f);
        }

        void Apply()
        {
            if (!KeepSun && sun != null && sun.intensity > 0f) sun.intensity = 0f;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Flat;
            double m = GameClock.Minutes % 1440.0;
            float k = m >= 22 * 60 || m < 5 * 60 ? NightAmbient : m < 7 * 60 ? DawnAmbient : m >= 20 * 60 ? Mathf.Lerp(1f, NightAmbient, (float)((m - 20 * 60) / 120.0)) : 1f;
            RenderSettings.ambientLight = Ambient * k;
            if (NightAmbient < 0.99f)
            {
                if (ceiling == null)
                {
                    var ls = new System.Collections.Generic.List<Light>();
                    foreach (var l in GetComponentsInChildren<Light>(true)) if (l.name.Contains("Ceiling")) ls.Add(l);
                    ceiling = ls.ToArray(); ceilingI = new float[ceiling.Length];
                    for (int i = 0; i < ceiling.Length; i++) ceilingI[i] = ceiling[i].intensity;
                }
                for (int i = 0; i < ceiling.Length; i++) if (ceiling[i] != null) ceiling[i].intensity = k < 0.5f ? 0f : ceilingI[i] * k;
            }
            Shader.SetGlobalColor(DayLight.IdInkColor, Ink.linear);
            var hc = HazeColor.linear; hc.a = HazeStrength;
            Shader.SetGlobalColor(DayLight.IdHaze, hc);
            Shader.SetGlobalVector(DayLight.IdHazeRange, new Vector4(HazeStart, HazeEnd, 0f, 0f));
            Shader.SetGlobalFloat(DayLight.IdDapple, 0f);
            Shader.SetGlobalFloat(DayLight.IdCrease, 1f);      // 무대 키트는 이미 선이 많음(먹선 배율 1.1 — 체육관만 모서리 선을 더)
            Shader.SetGlobalFloat(DayLight.IdFine, 0.2f);       // 타일·벽지 줄이 다 잔선이 되어 줄임
        }

        void LateUpdate() => Apply();     // 이야기 시간대 조명(DayLight)이 무대를 연 뒤 다시 칠해도 실내로

        void OnDisable()
        {
            if (counted) { DayLight.IndoorDepth = Mathf.Max(0, DayLight.IndoorDepth - 1); counted = false; }
            if (DayLight.Instance != null) DayLight.Instance.RefreshLut();
            if (sun != null && sunWas >= 0f) sun.intensity = sunWas;
            if (saved) { RenderSettings.fog = fogWas; RenderSettings.ambientMode = ambModeWas; RenderSettings.ambientLight = ambWas; }
        }
    }
}
