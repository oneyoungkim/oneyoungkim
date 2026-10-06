// 행인1의 메인이벤트 — 회차 카드(docs/09_M3_버티컬슬라이스_설계.md 2-1·2-4): 「N화 제목」 + 날짜 + 가계부 한 줄, 3초.
// 화면을 먹으로 덮은 카드 — 다음 장면 전환의 먹 닦기가 카드 위를 덮은 뒤 카드를 거둔다(SceneLoader.Covered).
// 가계부 줄 = 지난 회차의 한 줄(부록 A-1 — 회차 끝 숫자라 그 회차 카드에 미리 보이면 6화 '잔액 ____' 같은 것이 앞질러 보여서. 09 6-5).
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Haengin
{
    [DisallowMultipleComponent]
    public sealed class EpisodeCard : MonoBehaviour
    {
        public UiFonts Fonts;
        public const float Secs = 3f;

        public bool Shown => root != null && root.gameObject.activeSelf;
        public string Title => title != null ? title.text : "";
        public string Line => line != null ? line.text : "";
        public int Count { get; private set; }

        Image root;
        TextMeshProUGUI no, title, dates, line;
        CanvasGroup cg;
        float fade;

        void Build()
        {
            if (root != null) return;
            UiKit.Canvas(gameObject, 55);
            root = UiKit.Panel("회차 카드", transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, UiKit.Ink);
            UiKit.Stretch((RectTransform)root.transform);
            cg = root.gameObject.AddComponent<CanvasGroup>();
            no = UiKit.Text("회차", root.transform, Fonts != null ? Fonts.Br : null, 120f, UiKit.Paper, TextAlignmentOptions.Center);
            UiKit.Place(no.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(1200f, 150f));
            title = UiKit.Text("제목", root.transform, Fonts != null ? Fonts.Br : null, 66f, UiKit.Paper, TextAlignmentOptions.Center);
            UiKit.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(1600f, 90f));
            dates = UiKit.Text("날짜", root.transform, Fonts != null ? Fonts.B : null, 28f, new Color(UiKit.Paper.r, UiKit.Paper.g, UiKit.Paper.b, 0.7f), TextAlignmentOptions.Center);
            UiKit.Place(dates.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(1200f, 40f));
            UiKit.Panel("줄", root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -110f), new Vector2(980f, 3f), new Color(UiKit.Paper.r, UiKit.Paper.g, UiKit.Paper.b, 0.35f));
            line = UiKit.Text("가계부 한 줄", root.transform, Fonts != null ? Fonts.B : null, 29f, UiKit.Paper, TextAlignmentOptions.Center, wrap: true);
            UiKit.Place(line.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, -130f), new Vector2(1300f, 100f));
            root.gameObject.SetActive(false);
        }

        void Awake() => Build();

        public void Show(EpisodeDef ep, string ledgerLine)
        {
            Build();
            no.text = $"{ep.No}화";
            title.text = ep.Title;
            dates.text = ep.Dates;
            line.text = ledgerLine ?? "";
            cg.alpha = 0f;
            fade = 0f;
            root.gameObject.SetActive(true);
            Count++;
        }

        public void Hide() { Build(); root.gameObject.SetActive(false); }

        void LateUpdate()
        {
            if (!Shown) return;
            fade += UiKit.RealDt;
            cg.alpha = Mathf.Clamp01(fade / 0.3f);
        }
    }
}
