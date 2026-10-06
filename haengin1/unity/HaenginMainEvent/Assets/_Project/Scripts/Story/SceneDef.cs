// 행인1의 메인이벤트 — 이야기 장면 하나(docs/09_M3_버티컬슬라이스_설계.md 2-1·1-4 표 한 줄)
// 데이터 원본 = Data/Story/m3_story.json → StorySetup(에디터)이 StoryDef 에셋에 넣는다(손으로 고치지 않음).
// ScriptableObject 가 아니라 StoryDef 안의 직렬화 클래스(장면 32개를 에셋 파일 32개로 나누지 않으려고 — 09 6-5).
using System;
using UnityEngine;

namespace Haengin
{
    /// 장면 형식(1-4 표): 회차 카드 · 컷신 · 조작(이동 목표) · 미니게임 · 전투 · 몽타주 · 화면 연출
    public enum SceneKind { Card, Cut, Move, Mini, Fight, Montage, Ui }

    /// 시간대 조명(2-1): 새벽 · 낮 · 노을 · 밤
    public enum DayPart { Dawn, Day, Dusk, Night }

    [Serializable]
    public sealed class TargetDef
    {
        public string Label;
        public Vector3 Pos;
        public float Radius = 2.5f;
    }

    [Serializable]
    public sealed class SceneDef
    {
        /// "1-2" · 회차 카드는 "1-0" · 시험 장면은 "T1"
        public string Id;
        public int Ep;
        public int Month, Day, Hour, Minute;
        /// "Zone1" 또는 무대 키(Gukbap · Home · Conv · School · Site · Flash) — 무대 장면 = Scenes/St_<키>.unity
        public string Place = "Zone1";
        /// 무대 안 스폰 자리 이름(빈칸 = "Spawn")
        public string Spawn = "";
        /// Zone1 스폰(발 높이는 실행 때 Ground 로 다시 잼)
        public bool HasPos;
        public Vector3 Pos;
        public float Yaw;
        public DayPart Light = DayPart.Day;
        public SceneKind Kind = SceneKind.Cut;
        /// 1-4 표의 장소 칸(화면 표시용) · 내용 요약(자리 장면에서 자막으로 보임)
        public string Title = "", Summary = "";
        /// 대사 묶음 이름(DlgBook — 보통 장면 id). 빈칸이면 자리 장면(요약 자막)
        public string Dlg = "";
        /// 1-4 표 '예상'(초) — 자리 장면은 이 시간만큼 기다린다(건너뛰기 가능)
        public float Len = 3f;
        public TargetDef[] Targets = new TargetDef[0];
        /// 게임 시계가 흐름(주행·걷기 장면 — 실제 1초 = 게임 3초)
        public bool Clock;
        /// 끝날 때 적용(건너뛰어도 같게 — D05): "ledger:+8000:배달비 → 엄마 통장" · "wallet:2600" · "envelope:+5000" ·
        /// "name:국밥" · "flag:이름" · "endure" · "ep:2"(회차 줄 확정)
        public string[] Effects = new string[0];
        /// 시작할 때 적용(화면만 — 퀘스트 메모 등. 상태를 바꾸는 명령은 Effects 에)
        public string[] Start = new string[0];

        public string DateLabel => $"{Month}월 {Day}일({GameClock.WeekdayOf(Month, Day)})";
        public string TimeLabel => $"{Hour:00}:{Minute:00}";
        public bool InZone1 => string.IsNullOrEmpty(Place) || Place == "Zone1";
        public bool Interactive => Kind == SceneKind.Move || Kind == SceneKind.Mini || Kind == SceneKind.Fight;

        public override string ToString() => $"{Id} {DateLabel} {TimeLabel} {Place} {Kind}";
    }
}
