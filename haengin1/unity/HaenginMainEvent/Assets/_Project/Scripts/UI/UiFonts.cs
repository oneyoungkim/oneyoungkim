// 행인1의 메인이벤트 — 화면 글꼴(docs/09_M3_버티컬슬라이스_설계.md 2-2·6-2 D04 · docs/16_인터페이스_B.md 6·7)
// Body = KR_Bold_SDF(Noto Sans KR Bold, OFL) — 버튼·고른 줄 밖 목록·도감 글
// Medium = KR_Medium_SDF(Noto Sans KR Medium, OFL) — B안 본문(자막·안내·머리말)
// Black = KR_Black_SDF(Noto Sans KR Black, OFL) — B안 제목·이름띠·행동 이름
// Num = Num_Barlow_SDF(Barlow Condensed Bold, OFL) — B안 숫자(시각·돈·횟수·등급·타이머·라운드·키 이름). 한글은 대체 글꼴(Bold)로
// Brush = KR_Brush_SDF(Black Han Sans, OFL) — 의성어만(B안 7)
// Hand = KR_Hand_SDF(Gaegu, OFL — 이 PC 의 OYK 글꼴 후보 폴더에서 가져옴, 7장 결정 19) — 봉투·하늘 메모·서명·출석부(세계 안 손글씨)
// 에셋 Settings/UiFonts.asset(StorySetup 이 만듦). 모두 동적 아틀라스.
using TMPro;
using UnityEngine;

namespace Haengin
{
    [CreateAssetMenu(menuName = "Haengin/UiFonts")]
    public sealed class UiFonts : ScriptableObject
    {
        public TMP_FontAsset Body, Brush, Hand;
        public TMP_FontAsset Medium, Black, Num;

        public TMP_FontAsset B => Body != null ? Body : TMP_Settings.defaultFontAsset;
        public TMP_FontAsset Br => Brush != null ? Brush : B;
        public TMP_FontAsset H => Hand != null ? Hand : B;
        /// B안 본문(Noto Sans KR Medium)
        public TMP_FontAsset M => Medium != null ? Medium : B;
        /// B안 제목·이름(Noto Sans KR Black)
        public TMP_FontAsset K => Black != null ? Black : B;
        /// B안 숫자(Barlow Condensed Bold, 한글은 대체 글꼴)
        public TMP_FontAsset N => Num != null ? Num : B;

        /// 장면이 이 에셋을 불러오면(장면 참조) 잡아 둔다 — UiFonts 참조가 없는 화면(길잡이·조작 안내·상호작용 안내)이 B안 글꼴을 쓸 때
        public static UiFonts Current { get; private set; }
        void OnEnable() { if (Current == null || Body != null) Current = this; }

        public static TMP_FontAsset Or(UiFonts f, int which) =>
            f == null ? TMP_Settings.defaultFontAsset : which == 1 ? f.Br : which == 2 ? f.H : f.B;
    }
}
