// 행인1의 메인이벤트 — 실내 무대 조명(09 9단계, 2026-10-25): 키트 무대(국밥집·시우네·하루편의점·교실)가 켜져 있는 동안
// 바탕 Zone1 의 해·안개를 끄고(지붕 아래라 해가 바닥을 하얗게 날리던 것) 실내 주변광 + 무대 불(키트 소켓 Sock_Light_*) + 가벼운 실내 볼륨으로.
// 체육관 GymLights 와 같은 방식(시간대별 형광등은 아직 없음 — 새 오픈월드 설계 뒤).
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
        /// 해를 남김(창으로만 들어옴 — 키트 벽·천장이 그림자를 드리움). false 면 해를 끔(창 없는 곳)
        public bool KeepSun = true;
        Light sun; float sunWas = -1f; bool fogWas, saved;
        AmbientMode ambModeWas; Color ambWas;
        Volume vol;

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
                vol.profile = prof;
            }
            Apply();
        }

        void Apply()
        {
            if (!KeepSun && sun != null && sun.intensity > 0f) sun.intensity = 0f;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Ambient;
        }

        void LateUpdate() => Apply();     // 이야기 시간대 조명(DayLight)이 무대를 연 뒤 다시 칠해도 실내로

        void OnDisable()
        {
            if (sun != null && sunWas >= 0f) sun.intensity = sunWas;
            if (saved) { RenderSettings.fog = fogWas; RenderSettings.ambientMode = ambModeWas; RenderSettings.ambientLight = ambWas; }
        }
    }
}
