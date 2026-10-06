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
        public bool ScreenShown => screen != null && screen.gameObject.activeSelf;
        public string ScreenText => screen != null ? screen.text : "";
        /// 조작 장면(시각 칩을 남김)
        public bool KeepChip;

        Image datePlate, chip, stub;
        TextMeshProUGUI dateText, chipText, stubText, screen;
        float dateLeft, screenLeft;

        void Build()
        {
            if (datePlate != null) return;
            UiKit.Canvas(gameObject, 30);
            datePlate = UiKit.Panel("날짜 판", transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -30f), new Vector2(420f, 64f), new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.86f));
            UiKit.Fit(datePlate, 22, 26, 10, 12);
            dateText = UiKit.Text("글", datePlate.transform, Fonts != null ? Fonts.B : null, 30f, UiKit.Paper, TextAlignmentOptions.Left);
            var band = UiKit.Panel("주황 띠", datePlate.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(6f, 0f), UiKit.Accent);
            band.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            band.rectTransform.anchorMax = new Vector2(0f, 1f);
            datePlate.gameObject.SetActive(false);

            chip = UiKit.Panel("시각 칩", transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -30f), new Vector2(110f, 44f), new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.7f));
            chipText = UiKit.Text("글", chip.transform, Fonts != null ? Fonts.B : null, 24f, UiKit.Paper, TextAlignmentOptions.Center);
            UiKit.Stretch(chipText.rectTransform);
            chip.gameObject.SetActive(false);

            stub = UiKit.Panel("자리 장면", transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(1300f, 200f), new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.72f));
            UiKit.Fit(stub, 40, 40, 20, 24);
            stubText = UiKit.Text("글", stub.transform, Fonts != null ? Fonts.B : null, 28f, UiKit.Paper, TextAlignmentOptions.Center, wrap: true);
            stubText.GetComponent<RectTransform>().sizeDelta = new Vector2(1220f, 0f);
            var le = stubText.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 1220f;
            stub.gameObject.SetActive(false);

            screen = UiKit.Text("화면 글자", transform, Fonts != null ? Fonts.Br : null, 64f, UiKit.Paper, TextAlignmentOptions.Center, wrap: true);
            UiKit.Place(screen.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(1600f, 260f));
            var m = new Material(screen.fontSharedMaterial);
            m.EnableKeyword(ShaderUtilities.Keyword_Outline);
            m.SetColor(ShaderUtilities.ID_OutlineColor, UiKit.Ink);
            m.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.3f);
            screen.fontMaterial = m;
            screen.gameObject.SetActive(false);
        }

        void Awake() => Build();

        public void ShowDate()
        {
            Build();
            dateText.text = GameClock.Label;
            dateLeft = DateSecs;
            datePlate.gameObject.SetActive(true);
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
            screen.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        public void HideAll()
        {
            Build();
            datePlate.gameObject.SetActive(false); chip.gameObject.SetActive(false); stub.gameObject.SetActive(false); screen.gameObject.SetActive(false);
            dateLeft = screenLeft = 0f;
        }

        void LateUpdate()
        {
            if (datePlate == null) return;
            float rdt = UiKit.RealDt;
            if (datePlate.gameObject.activeSelf)
            {
                dateText.text = GameClock.Label;
                dateLeft -= rdt;
                if (dateLeft <= 0f) { datePlate.gameObject.SetActive(false); }
            }
            bool wantChip = KeepChip && !datePlate.gameObject.activeSelf;
            if (chip.gameObject.activeSelf != wantChip) chip.gameObject.SetActive(wantChip);
            if (wantChip) chipText.text = GameClock.TimeText;
            if (screen.gameObject.activeSelf && screenLeft > 0f) { screenLeft -= rdt; if (screenLeft <= 0f) screen.gameObject.SetActive(false); }
        }
    }
}
