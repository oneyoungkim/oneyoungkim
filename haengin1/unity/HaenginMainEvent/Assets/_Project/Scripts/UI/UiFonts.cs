// 행인1의 메인이벤트 — 화면 글꼴 3종(docs/09_M3_버티컬슬라이스_설계.md 2-2·6-2 D04)
// Body = KR_Bold_SDF(Noto Sans KR Bold, OFL) — 자막·메뉴·도감 글
// Brush = KR_Brush_SDF(Black Han Sans, OFL) — 가계부 먹 붓 숫자·회차 카드 제목·화면 글자
// Hand = KR_Hand_SDF(Gaegu, OFL — 이 PC 의 OYK 글꼴 후보 폴더에서 가져옴, 7장 결정 19) — 봉투·하늘 메모·서명·출석부
// 에셋 Settings/UiFonts.asset(StorySetup 이 만듦). 셋 다 동적 아틀라스.
using TMPro;
using UnityEngine;

namespace Haengin
{
    [CreateAssetMenu(menuName = "Haengin/UiFonts")]
    public sealed class UiFonts : ScriptableObject
    {
        public TMP_FontAsset Body, Brush, Hand;

        public TMP_FontAsset B => Body != null ? Body : TMP_Settings.defaultFontAsset;
        public TMP_FontAsset Br => Brush != null ? Brush : B;
        public TMP_FontAsset H => Hand != null ? Hand : B;

        public static TMP_FontAsset Or(UiFonts f, int which) =>
            f == null ? TMP_Settings.defaultFontAsset : which == 1 ? f.Br : which == 2 ? f.H : f.B;
    }
}
