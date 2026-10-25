// 행인1의 메인이벤트 — 실내 무대 2차(2026-10-25 — stages_compare 대비 휑한 곳): 시간대마다 켜고 끄는 무리(손님·동료·잠든 이불)·창빛 색·스탠드·김.
// 무대 루트에 붙고(WorldSetup.DressLife 가 편집기에서 채움) 게임 시계(GameClock)를 따라 0.5초마다 고친다. 연습장·이야기 장면에서도 시계대로.
using System;
using UnityEngine;

namespace Haengin
{
    [DisallowMultipleComponent]
    public sealed class StageLife : MonoBehaviour
    {
        [Serializable]
        public sealed class Slot
        {
            public GameObject Go;
            /// 분(From > To 면 자정을 넘는 창)
            public int From, To;
        }

        public Slot[] Slots = new Slot[0];
        /// 창빛(시간대 색·세기)
        public Light[] Windows = new Light[0];
        public Color DawnColor = new Color(0.62f, 0.72f, 0.95f), DayColor = new Color(1f, 0.94f, 0.82f), DuskColor = new Color(1f, 0.66f, 0.42f), NightColor = new Color(0.32f, 0.38f, 0.62f);
        public float DayIntensity = 1.6f, NightIntensity = 0.35f;
        /// 스탠드·형광등처럼 밤에 켜는 불(From~To 창)
        public Light[] Lamps = new Light[0];
        public int LampFrom = 19 * 60, LampTo = 60;
        /// 김(파티클) — 가게 여는 시간에만
        public ParticleSystem[] Steam = new ParticleSystem[0];
        public int SteamFrom = 5 * 60 + 30, SteamTo = 22 * 60;

        public int Applied { get; private set; }
        public int ActiveSlots { get; private set; }
        float left;

        public static bool InWindow(double m, int from, int to) => from <= to ? m >= from && m < to : m >= from || m < to;

        void OnEnable() { Apply(GameClock.Minutes); }

        void Update()
        {
            left -= Time.unscaledDeltaTime;
            if (left > 0f) return;
            left = 0.5f;
            Apply(GameClock.Minutes);
        }

        public void Apply(double minutes)
        {
            double m = minutes % 1440.0;
            int on = 0;
            foreach (var s in Slots)
            {
                if (s == null || s.Go == null) continue;
                bool a = InWindow(m, s.From, s.To);
                if (s.Go.activeSelf != a) s.Go.SetActive(a);
                if (a) on++;
            }
            ActiveSlots = on;
            var (pa, pb, k) = DayLight.PartsAt(m);
            Color c = Color.Lerp(Col(pa), Col(pb), k);
            float i = Mathf.Lerp(Int(pa), Int(pb), k);
            foreach (var w in Windows) if (w != null) { w.color = c; w.intensity = i; }
            bool lamp = InWindow(m, LampFrom, LampTo);
            foreach (var l in Lamps) if (l != null && l.enabled != lamp) l.enabled = lamp;
            bool steam = InWindow(m, SteamFrom, SteamTo);
            foreach (var p in Steam)
            {
                if (p == null) continue;
                if (steam && !p.isPlaying) p.Play();
                else if (!steam && p.isPlaying) p.Stop();
            }
            Applied++;
        }

        Color Col(DayPart p) => p switch { DayPart.Dawn => DawnColor, DayPart.Dusk => DuskColor, DayPart.Night => NightColor, _ => DayColor };
        float Int(DayPart p) => p switch { DayPart.Night => NightIntensity, DayPart.Dawn => DayIntensity * 0.55f, DayPart.Dusk => DayIntensity * 0.7f, _ => DayIntensity };
    }
}
