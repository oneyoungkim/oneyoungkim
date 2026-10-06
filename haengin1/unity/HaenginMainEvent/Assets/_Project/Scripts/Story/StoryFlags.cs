// 행인1의 메인이벤트 — 이야기 플래그(docs/09_M3_버티컬슬라이스_설계.md 2-1·2-3·2-10): 참은 횟수·자발적 참기·민재가 봉투를 봄·나리 스케치북·관계도 숫자 등
// 저장 파일에 그대로 들어간다(JsonUtility).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Haengin
{
    [Serializable]
    public sealed class StoryFlags
    {
        /// '참기' 횟수(강제된 것도 셈) · 진짜로 [참는다]를 먼저 고른 횟수(2-3)
        public int Endured, EnduredWillingly;
        /// 관계도(표시만 — 1-2 '자리만')
        public int Haneul, Nari;
        /// 양갈래 땋기 시도 수(2-8)
        public int BraidTries;
        /// 이름 붙은 플래그(민재가봉투를봄 · 나리스케치북 …)
        public List<string> Set = new List<string>();
        /// 고른 선택지 기록("장면.줄=번호")
        public List<string> Choices = new List<string>();

        public bool Has(string f) => Set.Contains(f);
        public void Raise(string f) { if (!string.IsNullOrEmpty(f) && !Set.Contains(f)) Set.Add(f); }

        /// 보스전 시작 기세(2-3): 20 + 5 × 참은 횟수, 최대 40
        public int StartHeat => Mathf.Min(40, 20 + 5 * Endured);

        public StoryFlags Clone() => JsonUtility.FromJson<StoryFlags>(JsonUtility.ToJson(this));

        public string Summary => $"참기 {Endured}(자발 {EnduredWillingly}) · 하늘 {Haneul} · 나리 {Nari} · 땋기 {BraidTries} · 플래그 [{string.Join(",", Set)}]";
    }
}
