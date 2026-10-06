// 행인1의 메인이벤트 — 대사 한 줄(docs/09_M3_버티컬슬라이스_설계.md 2-2 '대사 데이터')
// TSV 열: id · 화자 · 표정 · 표시 · 대사 · 대기 · 카메라 · 메모. id = "장면.번호"(예 1-1.03 → 장면 1-1, 3번째 줄).
// 표시: 자막 · 속마음 · 말풍선 · 화면 글자 · 선택 · 이벤트
//   선택 = 대사 칸에 보기들을 " / " 로(1~3개), 메모 = "참기"(2-3 '참기' 선택 — 0번 [참는다]로 강제) 또는 "flag:a,b,c"(고른 번호의 플래그)
//   이벤트 = 대사 칸이 명령(StoryRunner.Apply 와 같은 말: "name:국밥" · "ledger:+8000:배달비" · "flag:x" · "endure")
// 대사 칸의 "\n" 두 글자 = 줄바꿈(한 줄 최대 32자, 두 줄까지 — 1-5).
using System;

namespace Haengin
{
    public enum DlgMode { Sub, Inner, Bubble, Screen, Choice, Event }

    [Serializable]
    public sealed class DlgLine
    {
        public string Id = "";
        public string Scene = "";
        public int No;
        public string Who = "";
        public string Face = "";
        public DlgMode Mode;
        public string Text = "";
        /// 이 줄 뒤 기다림(초, 0 = 입력 또는 자동 진행 규칙)
        public float Wait;
        public string Cam = "";
        public string Note = "";

        public string Display => Text.Replace("\\n", "\n");
        public string[] Options => Mode == DlgMode.Choice ? Text.Split(new[] { " / " }, StringSplitOptions.RemoveEmptyEntries) : new string[0];
        public bool IsEndure => Mode == DlgMode.Choice && Note.Trim() == "참기";

        /// 표시 최소 시간(1-5 · D06): 글자 수 ÷ 8 + 0.8초
        public float MinShow => Display.Replace("\n", "").Length / 8f + 0.8f;

        public static DlgMode ParseMode(string s) => s.Trim() switch
        {
            "속마음" => DlgMode.Inner,
            "말풍선" => DlgMode.Bubble,
            "화면 글자" => DlgMode.Screen,
            "화면글자" => DlgMode.Screen,
            "선택" => DlgMode.Choice,
            "이벤트" => DlgMode.Event,
            _ => DlgMode.Sub,
        };

        public static string ModeName(DlgMode m) => m switch
        {
            DlgMode.Inner => "속마음", DlgMode.Bubble => "말풍선", DlgMode.Screen => "화면 글자",
            DlgMode.Choice => "선택", DlgMode.Event => "이벤트", _ => "자막",
        };
    }
}
