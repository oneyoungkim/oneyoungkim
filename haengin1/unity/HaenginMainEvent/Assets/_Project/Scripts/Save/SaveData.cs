// 행인1의 메인이벤트 — 저장 내용(docs/09_M3_버티컬슬라이스_설계.md 2-10)
// 저장 형식 버전 · 장면 id · 게임 시각 · 가계부 · 도감 · 이야기 플래그(참은 횟수·자발적 참기·관계도·땋기 시도 수 등).
// 설정(흔들림 줄이기·자동 정렬·자막 크기·자동 진행·진동)은 기존처럼 PlayerPrefs.
using System;
using UnityEngine;

namespace Haengin
{
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 1;

        public int Version = CurrentVersion;
        /// 이 장면의 '시작'에서 다시 한다(장면 시작마다 자동 저장)
        public string SceneId = "";
        /// 시험 장면 목록에서 저장했나
        public bool Test;
        public int Month = 3, Day = 3;
        public double Minutes;
        public Ledger Ledger = new Ledger();
        public NameBook Names = new NameBook();
        public StoryFlags Flags = new StoryFlags();
        /// 저장한 실제 시각(ISO 8601) · 장면 이름(이어하기 표시용)
        public string SavedAt = "";
        public string Label = "";

        public string ToJson() => JsonUtility.ToJson(this, true);

        public SaveData Clone() => JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(this));

        /// 두 저장이 같은가(SavedAt 은 뺌) — D11
        public bool SameAs(SaveData o)
        {
            if (o == null) return false;
            var a = Clone(); var b = o.Clone();
            a.SavedAt = b.SavedAt = "";
            return JsonUtility.ToJson(a) == JsonUtility.ToJson(b);
        }
    }
}
