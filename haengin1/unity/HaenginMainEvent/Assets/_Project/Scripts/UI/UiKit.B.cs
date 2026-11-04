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
        /// PlateA = 시안(sRGB) 기준 판 불투명도 · 구분선 1px · 키 상자 테두리 2px(정수 px — 1.5 는 변마다 1·2px 로 그려짐)
        public const float PlateA = 0.93f, CoverA = 0.86f, Stripe = 5f, RuleW = 1f, KeyLineW = 2f;
        /// 먹 판(시안 0.93 — 선형 색공간 보정 Seen 을 거친 값)
        public static Color InkPlate => Seen(Ink, PlateA);
        /// 판 위 막대 바탕(트랙) — 미리 섞은 불투명 색(먹 판 위 종이색 18% 를 sRGB 로 섞은 시안 값). 채움(주황) 대 트랙 3.0:1
        public static readonly Color Track = new Color32(63, 58, 57, 255);
        /// 판 안 구분선·보상 칸 선 — 미리 섞은 불투명 색(먹 판 위 종이색 25% 의 시안 값), 1px
        public static readonly Color RuleColor = new Color32(75, 71, 72, 255);
        /// 화면 덮개 먹(시안 B_3 #0E0B0C) — Cover 가 Seen 으로 보정해 씀(흰 배경 → 약 48)
        public static readonly Color CoverInk = new Color32(14, 11, 12, 255);
        /// 위험 붉은색(트랙 위 막대 채움용). Endure(#7A1E1E)는 트랙과 1.1:1 이라 같은 붉은 계열을 밝힌 값(트랙 대비 3.2:1)
        public static readonly Color EndureLit = new Color32(255, 64, 64, 255);
        public enum Edge { None, Top, Left }

        // ───────────────────────── 마지막 입력 장치(B7 — 칩 H/R3 · 상호작용 E/× · 패배 안내 · 전투 안내가 같이 씀, 프레임마다 한 번 잼)
        static int padFrame = -1;
        static bool padLast;
        public static bool PadLast
        {
            get
            {
                if (padFrame == Time.frameCount) return padLast;
                padFrame = Time.frameCount;
                var gp = UnityEngine.InputSystem.Gamepad.current; var kb = UnityEngine.InputSystem.Keyboard.current; var ms = UnityEngine.InputSystem.Mouse.current;
                double tg = gp != null ? gp.lastUpdateTime : -1, tk = System.Math.Max(kb != null ? kb.lastUpdateTime : -1, ms != null ? ms.lastUpdateTime : -1);
                if (tg > 0 || tk > 0) padLast = tg > tk;
                return padLast;
            }
            set { padLast = value; padFrame = Time.frameCount; }
        }

        // ───────────────────────── 선형 색공간 보정(2026-11-05 A1)
        // 프로젝트가 Linear 색공간이라 UI 알파가 선형 빛 공간에서 섞인다 → 시안(브라우저 sRGB 섞기)의 0.93 이 화면에서는 밝은 배경 위 0.73~0.84 로 옅게 보였다.
        // 규칙: ① 어두운 반투명(먹 판·덮개)은 Seen(색, 시안 알파)로만 지정한다 — 가장 밝은 배경(흰색)에서 시안과 같게 보이는 섞기 알파로 바꿔 줌(어두운 배경에선 시안보다 조금 진함).
        //       ② 판 위에 놓이는 밝은 반투명(트랙·구분선·보상 선)은 반투명으로 두지 않고 미리 섞은 불투명 색(Track·RuleColor)을 쓴다.
        /// 시안(sRGB 섞기) 기준 알파 a → 화면에서 같게 보이는 섞기 알파(흰 배경 기준, 채널 중 가장 진한 값)
        public static float SeenA(Color c, float a)
        {
            if (a <= 0f || a >= 1f || QualitySettings.activeColorSpace != ColorSpace.Linear) return a;
            float r = a;
            for (int i = 0; i < 3; i++)
            {
                float cl = Mathf.GammaToLinearSpace(c[i]);
                if (cl > 0.98f) continue;
                r = Mathf.Max(r, (1f - Mathf.GammaToLinearSpace(a * c[i] + 1f - a)) / (1f - cl));
            }
            return Mathf.Min(Mathf.Ceil(r * 255f - 1e-3f) / 255f, 1f);      // 꼭짓점 색은 8비트 — 내림되면 시안보다 옅어지므로 올림
        }

        /// 색 c 를 시안 알파 a 로 — 보정된 알파를 단 색
        public static Color Seen(Color c, float a) => new Color(c.r, c.g, c.b, SeenA(c, a));

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
            UiMotion.On(p, SideOf(anchor));      // D8: 붙은 쪽에서 10px · 150ms 로 등장
            return p;
        }

        /// 판이 붙은 화면 쪽(앵커로) — 왼쪽·오른쪽 먼저, 그다음 위·아래, 가운데는 아래에서
        public static Vector2 SideOf(Vector2 anchor)
        {
            if (anchor.x <= 0.01f) return new Vector2(-1f, 0f);
            if (anchor.x >= 0.99f) return new Vector2(1f, 0f);
            return anchor.y >= 0.99f ? new Vector2(0f, 1f) : new Vector2(0f, -1f);
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

        /// 화면 전체 덮개(시안 먹 #0E0B0C, a = 시안 알파 → Seen 보정)
        public static Image Cover(string name, Transform parent, float a = CoverA)
        {
            var c = Panel(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, Seen(CoverInk, a));
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

        static readonly Regex Digits = new Regex("(<[^>]*>)|([0-9]+)");      // B14: 서식 태그(<size=29>·<color=#…>) 안 숫자는 건너뜀
        /// 바뀌는 숫자는 자리 폭 고정 — 숫자 덩어리만 &lt;mspace&gt;(쉼표·쌍점·빗금은 제 폭). Barlow 숫자 폭(옆 여백 포함) 0.28~0.48em, 잉크 폭은 0.45em 안 → 0.45em
        public static string Tab(string s, float em = 0.45f) =>
            string.IsNullOrEmpty(s) ? "" : Digits.Replace(s, m => m.Groups[1].Success ? m.Value : "<mspace=" + em.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "em>" + m.Value + "</mspace>");

        /// 본문 글꼴 글 속 숫자만 Barlow(+ 자리 폭 고정) — HUD 덧말처럼 한 글에 글꼴이 하나인 곳(B10). 대사·선택지 문장 속 숫자는 본문 글꼴 그대로(A6)
        public static string NumRich(string s, UiFonts f = null, float em = 0.45f)
        {
            var n = FNum(f);
            if (string.IsNullOrEmpty(s) || n == null) return s ?? "";
            MaterialReferenceManager.AddFontAsset(n);      // <font="이름"> 이 Resources 없이 찾게
            string open = "<font=\"" + n.name + "\"><mspace=" + em.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "em>";
            return Digits.Replace(s, m => m.Groups[1].Success ? m.Value : open + m.Value + "</mspace></font>");
        }

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
        public static TextMeshProUGUI NameTag(string name, Transform parent, UiFonts f, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, float fontSize, out Image plate, float padL = -1f)
        {
            plate = Panel(name, parent, anchor, pivot, pos, size, Accent);
            var t = Text("이름", plate.transform, FBlack(f), fontSize, Ink, TextAlignmentOptions.MidlineLeft);
            Stretch(t.rectTransform);
            t.rectTransform.offsetMin = new Vector2(padL >= 0f ? padL : fontSize * 0.6f, 0f);      // 대화-2: 대사 판은 본문 왼쪽 여백(PadX)과 같게
            t.rectTransform.offsetMax = new Vector2(-fontSize * 0.4f, 0f);
            return t;
        }

        /// 이름띠 글 + 폭(최소 minW, 글 + 양옆 여백). 빈 글이면 띠를 숨김
        public static void SetTag(Image plate, TextMeshProUGUI t, string s, float minW, float padL = -1f)
        {
            t.text = s ?? "";
            if (padL >= 0f) t.rectTransform.offsetMin = new Vector2(padL, 0f);
            float w = Mathf.Max(minW, t.GetPreferredValues(t.text).x + (padL >= 0f ? padL : t.fontSize * 0.6f) + t.fontSize * 0.4f + 2f);
            plate.rectTransform.sizeDelta = new Vector2(w, plate.rectTransform.sizeDelta.y);
            plate.gameObject.SetActive(!string.IsNullOrEmpty(s));
        }

        // ───────────────────────── 키 칩
        /// 키 칩: 종이색 1.5px 테두리 상자 + Barlow 키 이름 · interact = 주황 칸 + 먹 글자(E·× 같은 상호작용 키). 왼쪽 가운데 기준
        public static RectTransform KeyChip(string name, Transform parent, UiFonts f, string key, float h, bool interact, out TextMeshProUGUI label)
        {
            var box = Panel(name, parent, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(h, h), interact ? Accent : new Color(Ink.r, Ink.g, Ink.b, 0f));
            if (!interact) Border(box.transform, Paper, KeyLineW);
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
        /// 막대(바탕 = 트랙 불투명 색 + 채움, 판 왼쪽 위 기준). 채움은 SetBar(앵커 비율 — 스프라이트 없이)
        public static Image Bar(string name, Transform parent, Vector2 pos, Vector2 size, Color fill, out Image back)
        {
            back = Panel(name + " 바탕", parent, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, size, Track);
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
        public struct TableRow { public RectTransform Rt; public TextMeshProUGUI Label, Value; public Image Fill, Back, Rule, STick; }

        public static TableRow Table(string name, Transform parent, UiFonts f, Vector2 pos, float width, float height, float labelW, float valueW, float labelSize, float valueSize, float barH)
        {
            var row = Node(name, parent);
            Place(row, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, new Vector2(width, height));
            var label = Text("항목", row, FMed(f), labelSize, Paper, TextAlignmentOptions.MidlineLeft);
            Place(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(labelW, height));
            float gap = 24f, barW = width - labelW - valueW - gap * 2f;
            var back = Panel("막대 바탕", row, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(labelW + gap, 0f), new Vector2(barW, barH), Track);
            var fill = Panel("막대", back.transform, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, Accent);
            var fr = fill.rectTransform; fr.anchorMin = Vector2.zero; fr.anchorMax = new Vector2(0f, 1f); fr.pivot = new Vector2(0f, 0.5f); fr.offsetMin = fr.offsetMax = Vector2.zero;
            var value = Text("값", row, FNum(f), valueSize, Paper, TextAlignmentOptions.MidlineRight);
            Place(value.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(valueW, height));
            var rule = Panel("구분선", row, new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(width, RuleW), RuleColor);
            // A6(2026-11-05): 막대 = D→S 점수 → 오른쪽 끝에 S 눈금(막대가 어디까지 차면 S 인지)
            var tick = Panel("S 눈금", row, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(labelW + gap + barW, 0f), new Vector2(2f, barH + 14f), PaperDim);
            return new TableRow { Rt = row, Label = label, Value = value, Fill = fill, Back = back, Rule = rule, STick = tick };
        }

        // ───────────────────────── 안내 글의 키 이름 강조(작은 글 → Warm)
        static readonly Regex Symbols = new Regex("^[□△○×]+$");
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
            // 드릴-8(2026-11-05): □△○× 기호는 가는 외곽선이라 Warm 이 흐림(3.6~4.3:1) → 기호만 한 단계 크고 굵게
            return KeyWords.Replace(s, m => "<color=#" + hex + ">" + (Symbols.IsMatch(m.Value) ? "<size=118%><b>" + m.Value + "</b></size>" : m.Value) + "</color>");
        }
    }
}
