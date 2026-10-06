// 행인1의 메인이벤트 — 회차 하나(docs/09_M3_버티컬슬라이스_설계.md 2-1 회차 카드 · 부록 A-1 가계부 한 줄)
using System;

namespace Haengin
{
    [Serializable]
    public sealed class EpisodeDef
    {
        public int No;
        /// 「출석부에 없는 이름」
        public string Title = "";
        /// 카드에 쓰는 날짜("3월 3일" · "3월 4일 ~ 3월 6일")
        public string Dates = "";
        /// 회차 카드 아래 가계부 한 줄(부록 A-1) — 지난 회차 끝의 숫자
        public string Ledger = "";
        /// 이 회차의 첫 장면 id(회차 카드 "N-0")
        public string First = "";

        public string CardTitle => $"{No}화  {Title}";
    }
}
