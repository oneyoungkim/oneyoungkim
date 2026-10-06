// 행인1의 메인이벤트 — 이야기 데이터 에셋(Settings/Story.asset, StorySetup 이 Data/Story/m3_story.json 에서 만든다)
// Scenes = 데모 장면 목록(회차 카드 포함, 1-4 표 순서), Test = 시험 장면 3개(0단계 전환 녹화·테스트용).
using System;
using UnityEngine;

namespace Haengin
{
    [CreateAssetMenu(menuName = "Haengin/Story")]
    public sealed class StoryDef : ScriptableObject
    {
        public int Version = 1;
        public EpisodeDef[] Episodes = new EpisodeDef[0];
        public SceneDef[] Scenes = new SceneDef[0];
        public SceneDef[] Test = new SceneDef[0];
        /// '불린 이름' 도감 칸(부록 A-2)
        public NameEntry[] Names = new NameEntry[0];
        /// 2-1 가계부 공개 표 줄(2화 원고 [수입]·[지출]·[잔액]·매달 나가는 돈)
        public string[] LedgerRows = new string[0];
        /// 퀘스트 「하늘이 생일」 메모(2화 원고 〈반하늘 11세 생일 요구사항〉 — 손글씨 글꼴)
        public string QuestHead = "";
        public string[] QuestMemo = new string[0];

        public SceneDef[] List(bool test) => test ? Test : Scenes;

        public int IndexOf(SceneDef[] list, string id) => Array.FindIndex(list, s => s.Id == id);

        public SceneDef Find(string id) => Array.Find(Scenes, s => s.Id == id) ?? Array.Find(Test, s => s.Id == id);

        public EpisodeDef Episode(int no) => Array.Find(Episodes, e => e.No == no);
    }
}
