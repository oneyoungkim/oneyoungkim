// 행인1의 메인이벤트 — B안 「중계 화면」 공용 부품(docs/16_인터페이스_B.md, 2026-11-05 대표 선택). 1920×1080 기준.
// 판 = 먹 #1A1417 × 0.93 · 곧은 모서리 · 기울임·거친 테두리·그림자 없음. 강조 줄 5px(상태 판 = 위, 알림·안내 판 = 왼쪽).
// 이름띠 = 주황 바탕 먹 글자(Noto Black). 머리말 = 흐린 종이색 Noto Medium 18 · 판 안 구분선 = 종이색 25% 1.5px.
// 숫자 = Barlow Condensed Bold(바뀌는 숫자는 Tab 으로 자리 폭 고정). 주황 = 큰 글자·띠·선택 막대만, 작은 강조 글자 = Warm.
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Haengin
{
    public static partial class UiKit
    {
        public const float PlateA = 0.93f, Stripe = 5f, RuleW = 1.5f;
        /// 먹 판(0.93)
        public static Color InkPlate => new Color(Ink.r, Ink.g, Ink.b, PlateA);
        /// 판 안 구분선(종이색 25%) · 막대 바탕
        public static Color RuleColor => new Color(Paper.r, Paper.g, Paper.b, 0.25f);
        public enum Edge { None, Top, Left }

        // ───────────────────────── 글꼴(UiFonts 참조가 없으면 장면이 불러온 UiFonts.Current → 그것도 없으면 Fallback(한글 본문 글꼴))
        /// UiFonts 를 못 찾을 때 쓸 한글 글꼴(GameUi·StageHud·길잡이가 자기 글꼴을 넣음 — TMP 기본 글꼴엔 한글이 없음)
        public static TMP_FontAsset Fallback;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetFonts() { Fallback = null; }
        static UiFonts Fo(UiFonts f) => f != null ? f : UiFonts.Current;
        static TMP_FontAsset Fb => Fallback != null ? Fallback : TMP_Settings.defaultFontAsset;
        public static TMP_FontAsset FMed(UiFonts f = null) { var x = Fo(f); return x != null ? x.M : Fb; }
        public static TMP_FontAsset FBold(UiFonts f = null) { var x = Fo(f); return x != null ? x.B : Fb; }
        public static TMP_FontAsset FBlack(UiFonts f = null) { var x = Fo(f); return x != null ? x.K : Fb; }
        public static TMP_FontAsset FNum(UiFonts f = null) { var x = Fo(f); return x != null ? x.N : Fb; }
        public static TMP_FontAsset FBrush(UiFonts f = null) { var x = Fo(f); return x != null ? x.Br : Fb; }

        // ───────────────────────── 판·줄
        /// 먹 판 + 강조 줄(위 = 상태 판 · 왼쪽 = 알림·안내 판)
        public static Image Plate(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Edge edge = Edge.None, Color? edgeColor = null)
        {
            var p = Panel(name, parent, anchor, pivot, pos, size, InkPlate);
            if (edge != Edge.None) EdgeLine(p.transform, edge, edgeColor ?? Accent);
            return p;
        }

        /// 판 가장자리 강조 줄(판 크기를 따라감, 판의 첫 자식 — 글보다 뒤). 글이 줄에 걸치지 않게 판 안 여백을 줄 폭보다 크게
        public static Image EdgeLine(Transform plate, Edge edge, Color c, float w = Stripe)
        {
            var e = Panel("강조 줄", plate, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, c);
            var rt = e.rectTransform;
            if (edge == Edge.Top) { rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 1f); rt.sizeDelta = new Vector2(0f, w); }
            else { rt.anchorMin = Vector2.zero; rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 0.5f); rt.sizeDelta = new Vector2(w, 0f); }
            rt.anchoredPosition = Vector2.zero;
            e.transform.SetAsFirstSibling();
            return e;
        }

        /// 판 안 가로 구분선(판 왼쪽 위 기준 자리)
        public static Image Rule(Transform parent, Vector2 pos, float width) =>
            Panel("구분선", parent, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, new Vector2(width, RuleW), RuleColor);

        /// 화면 전체 덮개(먹)
        public static Image Cover(string name, Transform parent, float a)
        {
            var c = Panel(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(Ink.r, Ink.g, Ink.b, a));
            Stretch(c.rectTransform);
            return c;
        }

        // ───────────────────────── 글
        /// 판 왼쪽 위 기준 글
        public static TextMeshProUGUI TextAt(string name, Transform parent, TMP_FontAsset font, float size, Color c, Vector2 pos, Vector2 box, TextAlignmentOptions align = TextAlignmentOptions.TopLeft, bool wrap = false)
        {
            var t = Text(name, parent, font, size, c, align, wrap);
            Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, box);
            return t;
        }

        /// 작은 머리말(흐린 종이색 Noto Medium 18)
        public static TextMeshProUGUI Label(string name, Transform parent, UiFonts f, Vector2 pos, float width = 300f, float size = 18f, Color? c = null) =>
            TextAt(name, parent, FMed(f), size, c ?? PaperDim, pos, new Vector2(width, size * 1.45f));

        static readonly Regex Digits = new Regex("[0-9]+");
        /// 바뀌는 숫자는 자리 폭 고정 — 숫자 덩어리만 &lt;mspace&gt;(쉼표·쌍점·빗금은 제 폭). Barlow 숫자 폭(옆 여백 포함) 0.28~0.48em, 잉크 폭은 0.45em 안 → 0.45em
        public static string Tab(string s, float em = 0.45f) =>
            string.IsNullOrEmpty(s) ? "" : Digits.Replace(s, m => "<mspace=" + em.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "em>" + m.Value + "</mspace>");

        /// 글이 maxW 보다 넓으면 글자 크기를 줄임(min 까지) — 큰 돈 숫자가 옆 칸에 닿지 않게
        public static float FitWidth(TextMeshProUGUI t, float maxW, float size, float min)
        {
            t.fontSize = size;
            float w = t.GetPreferredValues(t.text).x;
            if (w > maxW && w > 0f) { t.fontSize = Mathf.Max(min, size * maxW / w); w = t.GetPreferredValues(t.text).x; }
            return w;
        }

        /// 원 단위 쉼표 숫자(기호 없이)
        public static string Won(long v) => v.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture);

        // ───────────────────────── 이름띠
        /// 이름띠(주황 바탕 · 먹 글자 Noto Black). 폭은 SetTag 가 글에 맞춤
        public static TextMeshProUGUI NameTag(string name, Transform parent, UiFonts f, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, float fontSize, out Image plate)
        {
            plate = Panel(name, parent, anchor, pivot, pos, size, Accent);
            var t = Text("이름", plate.transform, FBlack(f), fontSize, Ink, TextAlignmentOptions.MidlineLeft);
            Stretch(t.rectTransform);
            t.rectTransform.offsetMin = new Vector2(fontSize * 0.6f, 0f);
            t.rectTransform.offsetMax = new Vector2(-fontSize * 0.4f, 0f);
            return t;
        }

        /// 이름띠 글 + 폭(최소 minW, 글 + 양옆 여백). 빈 글이면 띠를 숨김
        public static void SetTag(Image plate, TextMeshProUGUI t, string s, float minW)
        {
            t.text = s ?? "";
            float w = Mathf.Max(minW, t.GetPreferredValues(t.text).x + t.fontSize * 1.0f + 2f);
            plate.rectTransform.sizeDelta = new Vector2(w, plate.rectTransform.sizeDelta.y);
            plate.gameObject.SetActive(!string.IsNullOrEmpty(s));
        }

        // ───────────────────────── 키 칩
        /// 키 칩: 종이색 1.5px 테두리 상자 + Barlow 키 이름 · interact = 주황 칸 + 먹 글자(E·× 같은 상호작용 키). 왼쪽 가운데 기준
        public static RectTransform KeyChip(string name, Transform parent, UiFonts f, string key, float h, bool interact, out TextMeshProUGUI label)
        {
            var box = Panel(name, parent, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(h, h), interact ? Accent : new Color(Ink.r, Ink.g, Ink.b, 0f));
            if (!interact) Border(box.transform, Paper, RuleW);
            label = Text("키", box.transform, FNum(f), h * 0.64f, interact ? Ink : Paper, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);
            SetKey(box.rectTransform, label, key, h);
            return box.rectTransform;
        }

        /// 키 칩 글 + 폭
        public static void SetKey(RectTransform box, TextMeshProUGUI label, string key, float h)
        {
            label.text = key ?? "";
            float w = Mathf.Max(h, label.GetPreferredValues(label.text).x + h * 0.42f);
            box.sizeDelta = new Vector2(w, h);
        }

        /// 상자 테두리(안쪽 네 변)
        public static void Border(Transform box, Color c, float w)
        {
            for (int i = 0; i < 4; i++)
            {
                var e = Panel("테두리", box, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, c);
                var rt = e.rectTransform;
                switch (i)
                {
                    case 0: rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 1f); rt.sizeDelta = new Vector2(0f, w); break;
                    case 1: rt.anchorMin = Vector2.zero; rt.anchorMax = new Vector2(1f, 0f); rt.pivot = new Vector2(0.5f, 0f); rt.sizeDelta = new Vector2(0f, w); break;
                    case 2: rt.anchorMin = Vector2.zero; rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 0.5f); rt.sizeDelta = new Vector2(w, 0f); break;
                    default: rt.anchorMin = new Vector2(1f, 0f); rt.anchorMax = Vector2.one; rt.pivot = new Vector2(1f, 0.5f); rt.sizeDelta = new Vector2(w, 0f); break;
                }
                rt.anchoredPosition = Vector2.zero;
            }
        }

        // ───────────────────────── 막대
        /// 막대(바탕 종이색 25% + 채움, 판 왼쪽 위 기준). 채움은 SetBar(앵커 비율 — 스프라이트 없이)
        public static Image Bar(string name, Transform parent, Vector2 pos, Vector2 size, Color fill, out Image back)
        {
            back = Panel(name + " 바탕", parent, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, size, RuleColor);
            var fi = Panel(name, back.transform, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, fill);
            var rt = fi.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 0.5f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return fi;
        }

        public static void SetBar(Image fill, float v)
        {
            var rt = fill.rectTransform;
            rt.anchorMax = new Vector2(Mathf.Clamp01(v), 1f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        // ───────────────────────── 목록 줄
        /// 목록 줄(선택지·메뉴): 고른 줄 = 종이 판 + 왼쪽 주황 6px + 먹 글자(Black) / 안 고른 줄 = 먹 판 + 종이색 글자(Bold)
        public struct Row { public RectTransform Rt; public Image Plate, Bar; public TextMeshProUGUI Label; }

        public static Row ListRow(string name, Transform parent, UiFonts f, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, float fontSize)
        {
            var plate = Panel(name, parent, anchor, pivot, pos, size, InkPlate);
            var bar = Panel("고른 표시", plate.transform, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, Accent);
            var br = bar.rectTransform; br.anchorMin = Vector2.zero; br.anchorMax = new Vector2(0f, 1f); br.pivot = new Vector2(0f, 0.5f); br.sizeDelta = new Vector2(6f, 0f); br.anchoredPosition = Vector2.zero;
            var label = Text("글", plate.transform, FBold(f), fontSize, Paper, TextAlignmentOptions.MidlineLeft);
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(Mathf.Round(fontSize * 0.83f), 0f);
            label.rectTransform.offsetMax = new Vector2(-Mathf.Round(fontSize * 0.5f), 0f);
            label.overflowMode = TextOverflowModes.Ellipsis;
            return new Row { Rt = plate.rectTransform, Plate = plate, Bar = bar, Label = label };
        }

        public static void SetRow(Row r, bool selected, UiFonts f)
        {
            r.Plate.color = selected ? Paper : InkPlate;
            r.Bar.enabled = selected;
            r.Label.color = selected ? Ink : Paper;
            var want = selected ? FBlack(f) : FBold(f);
            if (want != null && r.Label.font != want) r.Label.font = want;
        }

        // ───────────────────────── 표 줄
        /// 표 줄: 항목(Noto Medium) · 얇은 주황 막대 · 값(Barlow, 오른쪽 맞춤) · 아래 구분선. 판 왼쪽 위 기준
        public struct TableRow { public RectTransform Rt; public TextMeshProUGUI Label, Value; public Image Fill, Back, Rule; }

        public static TableRow Table(string name, Transform parent, UiFonts f, Vector2 pos, float width, float height, float labelW, float valueW, float labelSize, float valueSize, float barH)
        {
            var row = Node(name, parent);
            Place(row, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, new Vector2(width, height));
            var label = Text("항목", row, FMed(f), labelSize, Paper, TextAlignmentOptions.MidlineLeft);
            Place(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(labelW, height));
            float gap = 24f, barW = width - labelW - valueW - gap * 2f;
            var back = Panel("막대 바탕", row, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(labelW + gap, 0f), new Vector2(barW, barH), new Color(Paper.r, Paper.g, Paper.b, 0.18f));
            var fill = Panel("막대", back.transform, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, Accent);
            var fr = fill.rectTransform; fr.anchorMin = Vector2.zero; fr.anchorMax = new Vector2(0f, 1f); fr.pivot = new Vector2(0f, 0.5f); fr.offsetMin = fr.offsetMax = Vector2.zero;
            var value = Text("값", row, FNum(f), valueSize, Paper, TextAlignmentOptions.MidlineRight);
            Place(value.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(valueW, height));
            var rule = Panel("구분선", row, new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(width, RuleW), RuleColor);
            return new TableRow { Rt = row, Label = label, Value = value, Fill = fill, Back = back, Rule = rule };
        }

        // ───────────────────────── 안내 글의 키 이름 강조(작은 글 → Warm)
        static readonly Regex KeyWords = new Regex(@"(L1|L2|R1|R2|L3|R3|L↓|L↑|L 좌/우|Space|Shift|Ctrl|Enter|Esc|[□△○×]+)");
        /// '다음' 안내 판(오른쪽 위 538×106 · 왼쪽 주황 줄): 머리말 '다음 · 국밥집 · 73m'(Medium 18 흐림) · 제목(Noto Black 32) · 시각(Barlow 38 Warm).
        /// 세계 HUD(WorldHud)와 이야기 이동 장면의 길잡이(RouteHud)가 같은 판을 씀
        public sealed class NextPlate
        {
            public Image Plate;
            public TextMeshProUGUI Head, Title, Time;
            public const float W = 538f, H = 106f, X = 26f, Inner = W - X - 22f;

            public static NextPlate Make(Transform parent, UiFonts f, string name)
            {
                var n = new NextPlate();
                var tr = new Vector2(1f, 1f);
                n.Plate = UiKit.Plate(name, parent, tr, tr, new Vector2(-38f, -38f), new Vector2(W, H), Edge.Left);
                n.Head = Label("머리말", n.Plate.transform, f, new Vector2(X, -12f), Inner);
                n.Title = TextAt("제목", n.Plate.transform, FBlack(f), 32f, Paper, new Vector2(X, -46f), new Vector2(Inner, 60f), TextAlignmentOptions.BaselineLeft);
                n.Time = TextAt("시각", n.Plate.transform, FNum(f), 38f, Warm, new Vector2(X, -46f), new Vector2(120f, 60f), TextAlignmentOptions.BaselineLeft);
                return n;
            }

            public void Set(string head, string title, string time)
            {
                Head.text = Tab(head ?? "");
                Time.text = Tab(time ?? "");
                bool hasTime = !string.IsNullOrEmpty(time);
                Time.gameObject.SetActive(hasTime);
                float tw = hasTime ? Time.GetPreferredValues(Time.text).x + 14f : 0f;
                Title.text = title ?? "";
                float w = FitWidth(Title, Inner - tw, 32f, 22f);
                Title.overflowMode = TextOverflowModes.Ellipsis;
                Title.rectTransform.sizeDelta = new Vector2(Inner - tw, Title.rectTransform.sizeDelta.y);
                Time.rectTransform.anchoredPosition = new Vector2(X + Mathf.Min(w, Inner - tw) + 14f, Time.rectTransform.anchoredPosition.y);
            }
        }

        /// 안내 한 줄에서 키 이름(□△○× · R1 · Space …)만 Warm 으로
        public static string KeyMark(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            string hex = ColorUtility.ToHtmlStringRGB(Warm);
            return KeyWords.Replace(s, m => "<color=#" + hex + ">" + m.Value + "</color>");
        }
    }
}
