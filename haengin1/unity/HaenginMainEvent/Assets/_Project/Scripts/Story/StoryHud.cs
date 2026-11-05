// 행인1의 메인이벤트 — 이야기 화면 글(docs/09_M3_버티컬슬라이스_설계.md 2-1·2-2)
// ① 날짜 판: 화면 왼쪽 위 「3월 3일(화) 05:31 새벽」 — 장면 시작 3초 보이고 접힘(조작 장면에서는 시각만 남은 작은 칩)
// ② 자리 장면 글: 아직 만들지 않은 장면(컷신·미니게임·전투 자리)의 id·장소·내용 요약 — 화면 아래 가운데 위쪽
// ③ 화면 글자: 가운데 큰 붓 글씨(1-7 훅 자막 "그 이유는 한 달에 백이십만 원쯤 한다." 등)
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Haengin
{
    [DisallowMultipleComponent]
    public sealed class StoryHud : MonoBehaviour
    {
        public UiFonts Fonts;
        public const float DateSecs = 3f;

        public bool DateShown => datePlate != null && datePlate.gameObject.activeSelf;
        public bool ChipShown => chip != null && chip.gameObject.activeSelf;
        public string DateText => dateText != null ? dateText.text : "";
        public bool StubShown => stub != null && stub.gameObject.activeSelf;
        public string StubText => stubText != null ? stubText.text : "";
        Image screenPlate;
        public bool ScreenShown => screen != null && screen.gameObject.activeSelf;
        public string ScreenText => screen != null ? screen.text : "";
        /// 조작 장면(시각 칩을 남김)
        public bool KeepChip;

        Image datePlate, chip, stub;
        TextMeshProUGUI dateText, chipText, stubText, screen;
        float dateLeft, screenLeft;
        string chipRaw = "", dateRaw = "";

        void Build()
        {
            if (datePlate != null) return;
            UiKit.Canvas(gameObject, 30);
            // D2(2026-11-05): B안 — 날짜 판(0.86 → 먹 판 0.93 · 왼쪽 주황 5px · Noto Black) · 시각 칩 Barlow · 자리 장면·화면 글자 판 먹 판(붓 글꼴 → Noto Black)
            datePlate = UiKit.Panel("날짜 판", transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(38f, -38f), new Vector2(420f, 64f), UiKit.InkPlate);
            UiKit.Fit(datePlate, 24, 26, 10, 12);
            dateText = UiKit.Text("글", datePlate.transform, UiKit.FBlack(Fonts), 29f, UiKit.Paper, TextAlignmentOptions.Left);
            var band = UiKit.Panel("주황 띠", datePlate.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(UiKit.Stripe, 0f), UiKit.Accent);
            band.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            band.rectTransform.anchorMax = new Vector2(0f, 1f);
            UiMotion.On(datePlate, new Vector2(-1f, 0f));
            datePlate.gameObject.SetActive(false);

            chip = UiKit.Panel("시각 칩", transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(38f, -38f), new Vector2(110f, 44f), UiKit.InkPlate);
            chipText = UiKit.Text("글", chip.transform, UiKit.FNum(Fonts), 30f, UiKit.Paper, TextAlignmentOptions.Center);
            UiKit.Stretch(chipText.rectTransform);
            chip.gameObject.SetActive(false);

            stub = UiKit.Panel("자리 장면", transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(1300f, 200f), UiKit.InkPlate);
            UiKit.Fit(stub, 40, 40, 20, 24);
            UiKit.EdgeLine(stub.transform, UiKit.Edge.Left, UiKit.Accent).gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            stubText = UiKit.Text("글", stub.transform, UiKit.FMed(Fonts), 28f, UiKit.Paper, TextAlignmentOptions.Center, wrap: true);
            stubText.GetComponent<RectTransform>().sizeDelta = new Vector2(1220f, 0f);
            var le = stubText.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 1220f;
            stub.gameObject.SetActive(false);

            screenPlate = UiKit.Panel("화면 글자 판", transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(800f, 160f), UiKit.InkPlate);     // 2026-10-31: 판 없이 배경 위에
            screenPlate.gameObject.SetActive(false);
            UiKit.EdgeLine(screenPlate.transform, UiKit.Edge.Top, UiKit.Accent);
            screen = UiKit.Text("화면 글자", screenPlate.transform, UiKit.FBlack(Fonts), 60f, UiKit.Paper, TextAlignmentOptions.Center, wrap: true);
            UiKit.Stretch(screen.rectTransform, 24f);
            screen.gameObject.SetActive(false); screenPlate.gameObject.SetActive(false);
        }

        void Awake() => Build();

        public void ShowDate()
        {
            Build();
            dateText.text = UiKit.NumRich(GameClock.Label, Fonts);      // 결정 ③: 자료 줄 숫자 Barlow
            dateLeft = DateSecs;
            UiMotion.Show(datePlate.gameObject);      // F1: 사라지는 중이면 취소
            chip.gameObject.SetActive(false);
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)datePlate.transform);
        }

        public void Stub(string text)
        {
            Build();
            bool on = !string.IsNullOrEmpty(text);
            stubText.text = text ?? "";
            stub.gameObject.SetActive(on);
            if (on) LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)stub.transform);
        }

        public void Screen(string text, float secs)
        {
            Build();
            screen.text = text ?? "";
            screenLeft = secs;
            bool screenOn = !string.IsNullOrEmpty(text); if (screenOn) { var pref = screen.GetPreferredValues(screen.text, 1560f, 0f); screenPlate.rectTransform.sizeDelta = new Vector2(Mathf.Min(1640f, pref.x + 96f), pref.y + 60f); } screenPlate.gameObject.SetActive(screenOn); screen.gameObject.SetActive(screenOn);
        }

        /// 날짜 판·시각 칩만 끔(오픈월드 HUD 가 다시 뜰 때 — 같은 왼쪽 위에 겹치던 것)
        public void HideDate()
        {
            if (datePlate == null) return;
            if (datePlate.gameObject.activeSelf) datePlate.gameObject.SetActive(false);
            if (chip.gameObject.activeSelf && !KeepChip) chip.gameObject.SetActive(false);
            dateLeft = 0f;
        }


        public void HideAll()
        {
            Build();
            datePlate.gameObject.SetActive(false); chip.gameObject.SetActive(false); stub.gameObject.SetActive(false); screen.gameObject.SetActive(false); screenPlate.gameObject.SetActive(false);
            dateLeft = screenLeft = 0f;
        }

        void LateUpdate()
        {
            if (datePlate == null) return;
            float rdt = UiKit.RealDt;
            if (datePlate.gameObject.activeSelf)
            {
                if (GameClock.Label != dateRaw) { dateRaw = GameClock.Label; dateText.text = UiKit.NumRich(dateRaw, Fonts); }
                dateLeft -= rdt;
                if (dateLeft <= 0f) { UiMotion.Hide(datePlate.gameObject); }      // D8
            }
            bool wantChip = KeepChip && !datePlate.gameObject.activeSelf;
            if (chip.gameObject.activeSelf != wantChip) chip.gameObject.SetActive(wantChip);
            if (wantChip && GameClock.TimeText != chipRaw) { chipRaw = GameClock.TimeText; chipText.text = UiKit.Tab(chipRaw); }
            if (screen.gameObject.activeSelf && screenLeft > 0f) { screenLeft -= rdt; if (screenLeft <= 0f) { screen.gameObject.SetActive(false); screenPlate.gameObject.SetActive(false); } }      // F2: 중괄호(시간 정한 화면 글이 첫 프레임에 판을 잃던 것)
        }
    }
}
