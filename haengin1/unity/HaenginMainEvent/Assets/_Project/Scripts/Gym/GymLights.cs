// 행인1의 메인이벤트 — 체육관 조명·시간대(docs/12_체육관_설계.md 3장·5-2): 형광등 3열 15 + 케이지 스포트 4 + 계단 1 + 사무실 1
// 아침 = 남쪽 2열(열 1·2) · 낮/저녁 = 전체 · 밤 = 가운데 열(z 6.0) + 케이지 스포트만(주광) · 새벽 = 닫힘(전부 끔)
// URP 는 실시간 Area 가 없어 형광등은 아래로 향한 넓은 스포트(120°)로 대신하고 발광 재질(M_Fluorescent)의 색을 함께 바꾼다(베이크 없음 — 12 문서 '구현 결과').
using System.Collections.Generic;
using UnityEngine;

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
        public float FluorIntensity = 3.2f, SpotIntensity = 4.0f;
        /// 실내 주변광(바탕 Zone1 의 시간대 주변광 대신 — 지하라 하늘빛 없음)
        public Color AmbientDay = new Color(0.60f, 0.58f, 0.55f), AmbientNight = new Color(0.20f, 0.19f, 0.22f);
        public GymPart Part { get; private set; } = GymPart.Day;
        public int LitCount { get; private set; }
        float flicker;

        public static GymPart PartOfHour(int hour) => hour >= 4 && hour < 7 ? GymPart.Dawn : hour >= 7 && hour < 11 ? GymPart.Morning : hour >= 11 && hour < 17 ? GymPart.Day : hour >= 17 && hour < 21 ? GymPart.Evening : GymPart.Night;
        public static string Name(GymPart p) => p switch { GymPart.Dawn => "새벽", GymPart.Morning => "아침", GymPart.Day => "낮", GymPart.Evening => "저녁", _ => "밤" };

        public void Apply(GymPart p)
        {
            Part = p;
            LitCount = 0;
            foreach (var f in Fluors)
            {
                bool on = p == GymPart.Day || p == GymPart.Evening || (p == GymPart.Morning && f.Row <= 1) || (p == GymPart.Night && f.Row == 1);
                if (f.Light != null) { f.Light.enabled = on; f.Light.intensity = p == GymPart.Night ? FluorIntensity * 1.3f : FluorIntensity; }
                if (f.Tube != null)
                {
                    var mats = f.Tube.materials;
                    foreach (var m in mats) if (m != null) { m.SetColor("_EmissionColor", on ? FluorColor * 1.6f : Color.black); m.SetColor("_Emissive_Color", on ? FluorColor * 1.4f : Color.black); if (on) m.EnableKeyword("_EMISSION"); else m.DisableKeyword("_EMISSION"); }
                }
                if (on) LitCount++;
            }
            bool spots = p == GymPart.Evening || p == GymPart.Night || p == GymPart.Day;
            for (int i = 0; i < CageSpots.Count; i++) if (CageSpots[i] != null) { CageSpots[i].enabled = spots; CageSpots[i].intensity = p == GymPart.Night ? SpotIntensity * 1.3f : SpotIntensity; CageSpots[i].shadows = i == 0 ? LightShadows.Soft : LightShadows.None; }
            if (Stair != null) Stair.enabled = p != GymPart.Dawn;
            if (Office != null) Office.enabled = p == GymPart.Day || p == GymPart.Morning || p == GymPart.Evening;
            if (spots) LitCount += CageSpots.Count;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = p == GymPart.Night || p == GymPart.Dawn ? AmbientNight : p == GymPart.Morning ? Color.Lerp(AmbientNight, AmbientDay, 0.7f) : AmbientDay;
            Debug.Log($"[Gym] 조명 {Name(p)}: 형광등 {LitCount - (spots ? CageSpots.Count : 0)}/{Fluors.Count} · 케이지 스포트 {(spots ? CageSpots.Count : 0)} · 사무실 {(Office != null && Office.enabled)}");
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
