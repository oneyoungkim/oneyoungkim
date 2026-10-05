// 행인1의 메인이벤트 — 전투 클립 측정표 (docs/08_M2_전투_설계.md 10-4 CombatClipProbe 결과)
// 에디터 ClipSetup 이 클립을 시우 아바타에 재생해 잰 값: 길이 · 타격 시각(치는 손·발·무릎이 정면으로 가장 멀리 나간 때) · 그때 뻗은 거리 ·
// 쓰러짐 끝 · 일어서기 구간 · 도발 1.4초 창. FighterAnim 이 재생 배율(= 클립 타격 시각 ÷ 표의 발생 시간)을 여기서 계산한다.
using System;
using UnityEngine;

namespace Haengin
{
    [CreateAssetMenu(menuName = "Haengin/ClipTable")]
    public sealed class ClipTable : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("애니메이터 상태 이름 = 클립 이름")] public string State;
            [Tooltip("동작 라이브러리 번호")] public int Id;
            public float Length;
            [Tooltip("타격 시각(초) — 공격 클립. 잡기 = 손이 닿는 때, 쓰러짐 = 바닥에 닿는 때")] public float Hit;
            [Tooltip("타격 때 치는 끝이 루트에서 정면으로 나간 거리(m)")] public float Reach;
        }

        public Entry[] Entries = new Entry[0];

        public bool TryGet(string state, out Entry e)
        {
            foreach (var x in Entries) if (x.State == state) { e = x; return true; }
            e = default;
            return false;
        }

        public bool TryGetId(int id, out Entry e)
        {
            foreach (var x in Entries) if (x.Id == id) { e = x; return true; }
            e = default;
            return false;
        }

        /// 기술(MoveDef.ClipId) → 애니메이터 상태 이름(같은 클립을 여러 기술이 씀: 크로스·크로스(끝)·카운터 = Cross)
        public static string StateFor(MoveDef m)
        {
            switch (m.ClipId)
            {
                case 191: return "Jab";
                case 192: return "Cross";
                case 193: return "Hook";
                case 194: return "Upper";
                case 195: return "BigHook";
                case 209: return "Kick";
                case 206: return "FKick";
                case 211: return "Knee";
                case 259: return m.State == "Push" ? "Push" : "Grab";
                case 260: return "PushFwd";
                case 512: return "Tackle";
                case 510: return "Cross";      // 달려들기: 돌진(Charge) 뒤 크로스
                default: return null;
            }
        }
    }
}
