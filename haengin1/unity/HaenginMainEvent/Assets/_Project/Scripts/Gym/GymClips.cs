// 행인1의 메인이벤트 — 체육관 전용 동작 표(Settings/GymClips.asset, GymSetup 이 잼): 상태 이름 · 길이(초) · 한 클립 안 반복 수(줄넘기 점프 수)
// 2026-10-24 2차 패스: Jump_Rope 46 · kettlebell_swing 327 · Burpee_Exercise 396(스프롤 = 버피 앞 절반) — 시우 리그, 8cr/개
using UnityEngine;

namespace Haengin
{
    public sealed class GymClips : ScriptableObject
    {
        [System.Serializable] public struct Entry { public string State; public int Id; public float Length; public int Cycles; }
        public Entry[] Entries = new Entry[0];

        public bool TryGet(string state, out Entry e)
        {
            foreach (var x in Entries) if (x.State == state) { e = x; return true; }
            e = default; return false;
        }
        public bool Has(string state) => TryGet(state, out _);
        /// 반복 1회 길이(초) — 줄넘기 점프 1회
        public float CycleSec(string state) => TryGet(state, out var e) && e.Cycles > 0 ? e.Length / e.Cycles : 0f;
    }
}
