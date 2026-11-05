// 행인1의 메인이벤트 — 스토리보드 카드(2026-10-25 대표 피드백 "뭔 스토리인지도 모르겠고 막 지나감")
// ① 자리 장면(아직 만들지 않은 컷신·미니게임·전투): 회차·날짜·시각·장면 제목 + 원고 흐름 3~5줄 + 핵심 대사 2~3줄 + '제작 중 장면' 표시.
//    예전처럼 요약 한 줄 띄우고 len 초 뒤 넘어가지 않고, 버튼(× · Enter · Space · 마우스 왼쪽)을 눌러야 넘어간다(StoryRunner).
// ② 조작 장면(주행·걷기) 시작 전: 같은 카드로 '이번 장면' 소개 → 누르면 출발.
// ③ 새 게임 첫 화면: 1막 소개 카드(누가·어디서·무엇을).
// 데이터 = m3_story.json 의 "board"·"intro"(tools/storyboard.py — 원고·회차 구성에서 손으로 쓴 것, tools/storyboard_curated.py).
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Haengin
{
    [DisallowMultipleComponent]
    public sealed class StoryBoardUi : MonoBehaviour
    {
        public UiFonts Fonts;
        public bool Shown => dim != null && dim.gameObject.activeSelf;
        /// 지금 보이는 카드 종류("자리" · "조작" · "소개")와 제목(테스트)
        public string Mode { get; private set; } = "";
        public string Title => title != null ? title.text : "";
        public string Body => body != null ? body.text : "";
        public int Shows { get; private set; }
        /// '제작 중 장면 · 스토리보드' 로 뜬 횟수(QA 2차 B 시험 — 실제 장면으로 바꾼 카드는 늘지 않아야)
        public int StoryboardShows { get; private set; }

        Image dim, card, tagPlate;
        TextMeshProUGUI head, title, body, quotes, foot, tagText;

        void Build()
        {
            if (dim != null) return;
            UiKit.Canvas(gameObject, 65);
            dim = UiKit.Panel("어둡게", transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, UiKit.Seen(Color.black, 0.55f));
            UiKit.Stretch(dim.rectTransform);
            // D2(2026-11-05): B안 — 먹 판 + 위 주황 줄 · 제목 Noto Black · 흐름 Medium · 대사(D6: 손글씨가 아님) Medium Warm
            card = UiKit.Panel("카드", dim.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(1480f, 800f), UiKit.InkPlate);
            var band = UiKit.Panel("주황 줄", card.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(1480f, UiKit.Stripe), UiKit.Accent);
            band.rectTransform.anchorMax = new Vector2(1f, 1f);
            head = UiKit.Text("머리", card.transform, UiKit.FMed(Fonts), 24f, UiKit.PaperDim, TextAlignmentOptions.Left);
            UiKit.Place(head.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(70f, -40f), new Vector2(1000f, 40f));
            tagPlate = UiKit.Panel("제작 중", card.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-60f, -34f), new Vector2(330f, 50f), UiKit.Accent);
            tagText = UiKit.Text("글", tagPlate.transform, UiKit.FBlack(Fonts), 24f, UiKit.Ink, TextAlignmentOptions.Center);
            UiKit.Stretch(tagText.rectTransform);
            title = UiKit.Text("제목", card.transform, UiKit.FBlack(Fonts), 54f, UiKit.Paper, TextAlignmentOptions.Left, wrap: true);
            UiKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(70f, -92f), new Vector2(1340f, 80f));
            body = UiKit.Text("흐름", card.transform, UiKit.FMed(Fonts), 29f, UiKit.Paper, TextAlignmentOptions.TopLeft, wrap: true);
            UiKit.Place(body.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(70f, -200f), new Vector2(1340f, 330f));
            body.lineSpacing = 18f;
            quotes = UiKit.Text("대사", card.transform, UiKit.FMed(Fonts), 32f, UiKit.Warm, TextAlignmentOptions.TopLeft, wrap: true);
            UiKit.Place(quotes.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(90f, -540f), new Vector2(1320f, 180f));
            quotes.lineSpacing = 6f;
            foot = UiKit.Text("발", card.transform, UiKit.FMed(Fonts), 22f, UiKit.PaperDim, TextAlignmentOptions.Right);
            UiKit.Place(foot.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-60f, 30f), new Vector2(900f, 36f));
            dim.gameObject.SetActive(false);
        }

        void Awake() => Build();

        /// 자리 장면(make = false) 또는 조작 장면 소개(move = true)
        public void Show(SceneDef s, EpisodeDef ep, bool move)
        {
            Build();
            Layout(false);
            Mode = move ? "조작" : "자리";
            string epTxt = ep != null ? $"{ep.No}화 「{ep.Title}」" : $"{s.Ep}화";
            head.text = $"{epTxt}   ·   {s.DateLabel} {s.TimeLabel}";
            title.text = s.Title;
            tagPlate.gameObject.SetActive(true);
            tagText.text = move ? "이번 장면" : "제작 중 장면 · 스토리보드";
            if (!move) StoryboardShows++;
            tagPlate.color = move ? UiKit.Paper : UiKit.Accent;
            var lines = s.Board != null && s.Board.Length > 0 ? s.Board : new[] { s.Summary };
            body.text = "· " + string.Join("\n· ", lines);
            quotes.text = Quotes(s.Quotes);
            foot.text = move ? "× · Enter · 마우스 왼쪽   출발" : "× · Enter · 마우스 왼쪽   다음";
            Open();
        }

        /// 오픈월드 이벤트 카드(docs/13 5장 — 원고 없는 카드·사연·문자·요약): 머리 줄·제목·꼬리표·흐름·핵심 대사·발
        public void ShowCard(string headLine, string titleLine, string tagLine, bool accent, string[] lines, string[] quoteLines, string footLine, bool side = false)
        {
            Build();
            Mode = "카드";
            Layout(side);
            if (tagLine != null && tagLine.Contains("스토리보드")) StoryboardShows++;
            head.text = headLine ?? "";
            title.text = titleLine ?? "";
            tagPlate.gameObject.SetActive(!string.IsNullOrEmpty(tagLine));
            tagText.text = tagLine ?? "";
            tagPlate.color = accent ? UiKit.Accent : UiKit.Paper;
            body.text = lines != null && lines.Length > 0 ? "· " + string.Join("\n· ", lines) : "";
            quotes.text = Quotes(quoteLines);
            foot.text = footLine ?? "";
            Open();
        }

        /// 1막 소개(새 게임 첫 화면)
        public void ShowIntro(string t, string[] lines, string note)
        {
            Build();
            Layout(false);
            Mode = "소개";
            head.text = "행인1의 메인이벤트   ·   시우 루트 1부";
            title.text = t;
            tagPlate.gameObject.SetActive(false);
            body.text = string.Join("\n\n", lines ?? new string[0]);
            quotes.text = string.IsNullOrEmpty(note) ? "" : $"<size=26><color=#{ColorUtility.ToHtmlStringRGB(UiKit.PaperDim)}>{note}</color></size>";      // F3: 먹 판 위 흐린 글 = PaperDim(옛 종이 판용 #5E5957 은 2.6:1)
            foot.text = "× · Enter · 마우스 왼쪽   시작";
            Open();
        }

        /// 2026-10-29: 레슨 카드가 화면 가운데를 덮어 코치가 안 보이던 것 → 오른쪽 세로 판(어둡게 거의 없음). 그 밖 카드는 가운데 그대로
        public bool Side { get; private set; }
        public RectTransform CardRect => card != null ? card.rectTransform : null;
        public const float SideW = 640f, SideH = 860f;
        void Layout(bool side)
        {
            Side = side;
            var c = card.rectTransform;
            dim.color = UiKit.Seen(Color.black, side ? 0.08f : 0.55f);
            // D7: 가운데 카드 1480 은 4:3(1440)보다 넓음 → 화면 폭 − 76 안으로 줄임(글 상자는 카드 비율로)
            float cw = ((RectTransform)transform).rect.width;
            float k = cw > 10f ? Mathf.Min(1f, (cw - 76f) / 1480f) : 1f;
            card.rectTransform.localScale = new Vector3(k, k, 1f);
            if (side) { c.anchorMin = c.anchorMax = new Vector2(1f, 0.5f); c.pivot = new Vector2(1f, 0.5f); c.anchoredPosition = new Vector2(-36f, 20f); c.sizeDelta = new Vector2(SideW, SideH); }
            else { c.anchorMin = c.anchorMax = new Vector2(0.5f, 0.5f); c.pivot = new Vector2(0.5f, 0.5f); c.anchoredPosition = new Vector2(0f, 10f); c.sizeDelta = new Vector2(1480f, 800f); }
            float w = side ? SideW - 90f : 1340f;
            head.rectTransform.sizeDelta = new Vector2(side ? SideW - 260f : 1000f, 40f);      // 2026-10-31: 오른쪽 위 표(레슨)와 안 겹치게
            head.fontSize = side ? 22f : 26f;
            title.rectTransform.sizeDelta = new Vector2(w, side ? 120f : 80f);
            title.fontSize = side ? 42f : 58f;
            body.rectTransform.anchoredPosition = new Vector2(side ? 45f : 70f, side ? -230f : -200f);
            body.rectTransform.sizeDelta = new Vector2(w, side ? 330f : 330f);
            body.fontSize = side ? 25f : 29f;
            title.rectTransform.anchoredPosition = new Vector2(side ? 45f : 70f, -92f);
            head.rectTransform.anchoredPosition = new Vector2(side ? 45f : 70f, -40f);
            quotes.rectTransform.anchoredPosition = new Vector2(side ? 55f : 90f, side ? -580f : -540f);
            quotes.rectTransform.sizeDelta = new Vector2(side ? w - 10f : 1320f, side ? 220f : 180f);
            quotes.fontSize = side ? 30f : 36f;
            // 2026-10-31: 긴 대사(서진수 끝 카드 4줄)가 아래 안내와 겹침 → 칸 안으로 줄임
            quotes.enableAutoSizing = true; quotes.fontSizeMin = 18f; quotes.fontSizeMax = quotes.fontSize;
            foot.rectTransform.sizeDelta = new Vector2(side ? SideW - 80f : 900f, 36f);
            tagPlate.rectTransform.sizeDelta = new Vector2(side ? 200f : 330f, 50f);
        }

        static string Quotes(string[] q)
        {
            if (q == null || q.Length == 0) return "";
            var sb = new System.Text.StringBuilder();
            foreach (var e in q)
            {
                int i = e.IndexOf('|');
                string who = i >= 0 ? e.Substring(0, i) : "", line = i >= 0 ? e.Substring(i + 1) : e;
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(string.IsNullOrEmpty(who) ? $"“{line}”" : $"<size=26><color=#{ColorUtility.ToHtmlStringRGB(UiKit.PaperDim)}>{who}</color></size>   “{line}”");
            }
            return sb.ToString();
        }

        void Open()
        {
            dim.gameObject.SetActive(true);
            Shows++;
        }

        public void Hide()
        {
            Build();
            dim.gameObject.SetActive(false);
            Mode = "";
        }
    }
}
