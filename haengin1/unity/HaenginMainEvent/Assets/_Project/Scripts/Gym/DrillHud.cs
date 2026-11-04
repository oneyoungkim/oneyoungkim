// 행인1의 메인이벤트 — 드릴 화면(docs/12_체육관_설계.md 4-6 CoachCaller + 라운드 타이머 + 박자 띠 + 알림 · B안 「중계 화면」 docs/16)
// 위 가운데 머리글 판 576×101(위 주황 줄): 왼쪽 종이 칸 '라운드 1/3' · 가운데 타이머(Barlow 106) · 오른쪽 코드(Warm)·이름(Noto Black 26)
// 왼쪽 아래 코치 말(하단 자막처럼 이름띠 + 먹 판, Noto Black 60 — 인물을 가리지 않게) · 아래 가운데 입력 칸(지금 칠 칸 주황 125 · 다음 칸 먹 106)
// 오른쪽 아래 안내 판(키 이름 Warm) · 박자 띠 · 휴식 중 지표 표 · '팡!' 알림판(의성어만 붓 글꼴)
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Haengin
{
    [DisallowMultipleComponent]
    public sealed class DrillHud : MonoBehaviour
    {
        public UiFonts Fonts;
        public DrillRunner Runner;
        public TextMeshPro WallTimer;          // 벽 라운드 타이머(발광 숫자판) 동기
        bool built;
        GameObject root;
        TextMeshProUGUI codeText, nameText, round, timer, cueWho, cueText, hint, toast;
        RectTransform topRt;
        Image top, cueTag;
        CombatHud combatHud;
        public float PlateY => topRt != null ? topRt.anchoredPosition.y : 0f;
        /// 머리글 판(위 가운데) — 시험이 전투 HUD 판과 겹침을 잼
        public RectTransform PlateRect => topRt;
        Image hintPlate, metricsPlate;
        bool noticeOnly;
        string hintRaw = "";
        public string HintText => hint != null && hint.gameObject.activeInHierarchy ? hintRaw : "";
        public void ClearHint() { if (hint != null) { hint.text = ""; hintRaw = ""; } }
        Image cuePlate, beatPlate, beatLine, beatLine2, toastPlate;
        readonly List<Image> ticks = new List<Image>();
        readonly List<TextMeshProUGUI> tickMarks = new List<TextMeshProUGUI>();
        readonly List<Image> icons = new List<Image>();
        readonly List<TextMeshProUGUI> iconTexts = new List<TextMeshProUGUI>();
        readonly List<Image> holdBars = new List<Image>();
        readonly List<(int, double)> visible = new List<(int, double)>();
        readonly TextMeshProUGUI[] mLabels = new TextMeshProUGUI[5], mValues = new TextMeshProUGUI[5];
        float cueLeft, toastLeft, topW = TopW;
        // A4 아래 줄 배치
        const float Mg = 38f, Gap = 24f, HintY = 41f, RowY = 108f, CueMinW = 420f, HintMinW = 360f;
        float hintPW = 60f, hintLayW = -1f, hintLayH = 60f, cueWantW = CueW, cueTop;
        bool cueRaised;
        int lastRound = -1, lastSec = -1;
        string lastId;
        AudioSource sfx;
        AudioClip tick;
        public string Cue => cueText != null ? cueText.text : "";
        public string Toast => toast != null && toast.gameObject.activeSelf ? toast.text : "";
        /// 알림판 자리(아래 가운데 기준) — 입력 칸(아래 108~233) 위, 얼굴 높이 아래
        public static readonly Vector2 ToastPos = new Vector2(0f, 250f);
        public RectTransform ToastRect => toastPlate != null ? toastPlate.rectTransform : null;
        /// 드릴 알림판('팡!')이 떠 있나 — 그동안 전투 의성어(CombatFx)는 띄우지 않음(같은 순간 판 두 개)
        public static bool ToastUp { get; private set; }
        public bool Visible => root != null && root.activeSelf;
        const int MaxTicks = 16, MaxIcons = 6;
        public const float BeatSpan = 1.6f;
        public const float TopW = 576f, TopH = 101f, CueW = 528f, CueH = 106f, CueMaxW = 900f, BeatW = 760f, BeatH = 88f, BeatLineX = -250f, BeatScale = 600f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { ToastUp = false; CombatHud.TopReserve = 0f; }

        void Build()
        {
            if (built) return; built = true;
            UiKit.Canvas(gameObject, 12);
            root = UiKit.Node("드릴", transform).gameObject;
            UiKit.Stretch((RectTransform)root.transform);
            var f = Fonts;
            var c5 = new Vector2(0.5f, 0.5f);
            var tl = new Vector2(0f, 1f);
            // 머리글 판(위 가운데, 위 주황 줄)
            top = UiKit.Plate("이름판", root.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(TopW, TopH), UiKit.Edge.Top);
            topRt = top.rectTransform;
            var cell = UiKit.Panel("라운드 칸", top.transform, tl, tl, new Vector2(0f, -UiKit.Stripe), new Vector2(110f, TopH - UiKit.Stripe), UiKit.Paper);
            UiKit.TextAt("라운드 머리말", cell.transform, UiKit.FMed(f), 18f, UiKit.Grey, new Vector2(0f, -3f), new Vector2(110f, 26f), TextAlignmentOptions.Top).text = "라운드";
            round = UiKit.Text("라운드", cell.transform, UiKit.FNum(f), 53f, UiKit.Ink, TextAlignmentOptions.Baseline);
            UiKit.Place(round.rectTransform, tl, new Vector2(0.5f, 0.5f), new Vector2(55f, -84f), new Vector2(110f, 60f));
            timer = UiKit.Text("타이머", top.transform, UiKit.FNum(f), 106f, UiKit.Paper, TextAlignmentOptions.Baseline);
            UiKit.Place(timer.rectTransform, tl, new Vector2(0.5f, 0.5f), new Vector2(226f, -92f), new Vector2(220f, 120f));
            codeText = UiKit.TextAt("코드", top.transform, UiKit.FNum(f), 26f, UiKit.Warm, new Vector2(343f, -20f), new Vector2(220f, 46f), TextAlignmentOptions.BaselineLeft);      // 드릴-10: 코드·이름 묶음을 10px 내려 타이머 가운데와 맞춤
            nameText = UiKit.TextAt("이름", top.transform, UiKit.FBlack(f), 26f, UiKit.Paper, new Vector2(343f, -60f), new Vector2(220f, 46f), TextAlignmentOptions.BaselineLeft);
            // 코치 말(왼쪽 아래 — 이름띠 + 먹 판)
            cuePlate = UiKit.Plate("큐판", root.transform, Vector2.zero, Vector2.zero, new Vector2(38f, 108f), new Vector2(CueW, CueH));
            cueWho = UiKit.NameTag("이름띠", cuePlate.transform, f, tl, Vector2.zero, Vector2.zero, new Vector2(192f, 43f), 25f, out cueTag);
            cueWho.name = "누가";
            cueText = UiKit.Text("말", cuePlate.transform, UiKit.FBlack(f), 60f, UiKit.Paper, TextAlignmentOptions.MidlineLeft, true);
            UiKit.Stretch(cueText.rectTransform); cueText.rectTransform.offsetMin = new Vector2(19f, 4f); cueText.rectTransform.offsetMax = new Vector2(-19f, -4f);
            cueText.enableAutoSizing = true; cueText.fontSizeMin = 32f; cueText.fontSizeMax = 60f;
            cuePlate.gameObject.SetActive(false);
            // 안내(오른쪽 아래 — 키 이름 Warm)
            hintPlate = UiKit.Plate("안내 판", root.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-38f, 41f), new Vector2(802f, 60f));
            hint = UiKit.Text("안내", hintPlate.transform, UiKit.FMed(f), 24f, UiKit.Paper, TextAlignmentOptions.MidlineLeft, true);
            UiKit.Stretch(hint.rectTransform); hint.rectTransform.offsetMin = new Vector2(22f, 6f); hint.rectTransform.offsetMax = new Vector2(-22f, -6f);
            hintPlate.gameObject.SetActive(false);
            // 박자 띠(아래 가운데 — 입력 칸 자리, 기준선 왼쪽)
            beatPlate = UiKit.Plate("박자 띠", root.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 108f), new Vector2(BeatW, BeatH));
            // 기준선은 박자 막대 줄(−38~+2)에만 — 막대 위 □△ 표시가 지나갈 때 선이 글자에 걸치지 않게(글자 감사 Q57)
            // 드릴-7(2026-11-05): 기준선은 흰 눈금(10×36)과 색만 다르던 것 → 눈금이 들어가는 주황 문기둥 두 개(4×44, 사이 14)로 모양부터 다르게
            beatLine = UiKit.Panel("기준선", beatPlate.transform, c5, c5, new Vector2(BeatLineX - 9f, -18f), new Vector2(4f, 44f), UiKit.Accent);
            beatLine2 = UiKit.Panel("기준선 2", beatPlate.transform, c5, c5, new Vector2(BeatLineX + 9f, -18f), new Vector2(4f, 44f), UiKit.Accent);
            for (int i = 0; i < MaxTicks; i++)
            {
                var t = UiKit.Panel("박자 " + i, beatPlate.transform, c5, c5, Vector2.zero, new Vector2(10f, 36f), UiKit.Paper); t.gameObject.SetActive(false); ticks.Add(t);
                var m = UiKit.Text("표시", t.transform, UiKit.FNum(f), 24f, UiKit.Paper, TextAlignmentOptions.Center); UiKit.Place(m.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 2f), new Vector2(60f, 30f)); tickMarks.Add(m);
            }
            // 입력 칸(아래 가운데)
            for (int i = 0; i < MaxIcons; i++)
            {
                var ic = UiKit.Panel("아이콘 " + i, root.transform, new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero, new Vector2(106f, 106f), UiKit.InkPlate); ic.gameObject.SetActive(false); icons.Add(ic);
                var tx = UiKit.Text("글", ic.transform, UiKit.FNum(f), 58f, UiKit.Paper, TextAlignmentOptions.Center); UiKit.Stretch(tx.rectTransform, 10f); iconTexts.Add(tx);
                tx.enableAutoSizing = true; tx.fontSizeMin = 26f; tx.fontSizeMax = 58f;
                // A3(2026-11-05): '길게'는 글자('─')가 아니라 칸 아래 막대(□ 획 굵기)로 — '□-' 가 빼기표처럼 읽히던 것
                var hb = UiKit.Panel("길게", ic.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(60f, 5f), UiKit.Paper); hb.gameObject.SetActive(false); holdBars.Add(hb);      // '□─'(길게)·'↓△' 두 글자도 칸 안에
            }
            // QA 2차(2026-10-28): 화면 가운데 위(+120)는 드릴 카메라에서 두 사람 얼굴 자리 — '팡!' 판은 아래 입력 칸 바로 위로
            toastPlate = UiKit.Plate("알림판", root.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), ToastPos, new Vector2(360f, 70f), UiKit.Edge.Left);
            toast = UiKit.Text("알림", toastPlate.transform, UiKit.FBrush(f), 40f, UiKit.Paper, TextAlignmentOptions.Center); UiKit.Stretch(toast.rectTransform);
            toastPlate.gameObject.SetActive(false);
            // 휴식 중 지표 표(가운데)
            metricsPlate = UiKit.Plate("지표 판", root.transform, c5, c5, new Vector2(0f, 40f), new Vector2(680f, 330f), UiKit.Edge.Top);
            UiKit.Label("지표 머리말", metricsPlate.transform, f, new Vector2(28f, -18f), 600f).text = "휴식 · 지금까지";
            for (int i = 0; i < 5; i++)
            {
                float y = -52f - i * 54f;
                mLabels[i] = UiKit.TextAt("지표 " + i, metricsPlate.transform, UiKit.FMed(f), 26f, UiKit.Paper, new Vector2(28f, y), new Vector2(400f, 54f), TextAlignmentOptions.MidlineLeft);
                mValues[i] = UiKit.TextAt("값 " + i, metricsPlate.transform, UiKit.FNum(f), 36f, UiKit.Paper, new Vector2(432f, y), new Vector2(220f, 54f), TextAlignmentOptions.MidlineRight);
                if (i < 4) UiKit.Rule(metricsPlate.transform, new Vector2(28f, y - 54f), 624f);
            }
            metricsPlate.gameObject.SetActive(false);
            sfx = gameObject.AddComponent<AudioSource>(); sfx.playOnAwake = false; sfx.spatialBlend = 0f; sfx.volume = 0.35f;
            tick = MakeTick();
            root.SetActive(false);
        }

        static AudioClip MakeTick()
        {
            int sr = 22050, n = sr / 40;
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Sin(i * 2f * Mathf.PI * 1200f / sr) * (1f - i / (float)n) * 0.6f;
            var c = AudioClip.Create("drill_tick", n, 1, sr, false); c.SetData(data, 0); return c;
        }

        public void Show(bool on) { Build(); root.SetActive(on); GameUi.HintOverride = on ? "일시정지 Esc · Options" : null; if (!on) { cueLeft = 0f; toastLeft = 0f; SetToast(false); CombatHud.TopReserve = 0f; } NoticeOnly(false); if (on) PlaceTop(); }

        /// QA Q34: 드릴을 못 시작할 때(문 닫음·회원만·막힘) 관장 말만 — 예전엔 Show(true) 로 지난 드릴의 이름판·타이머(0:40·R3/3)·안내까지 다시 떠 세계 HUD 와 겹쳤음
        public void Notice(string who, string line)
        {
            Build(); root.SetActive(true); NoticeOnly(true); Say(who, line);
        }
        public bool NoticeShown => root != null && root.activeSelf && topRt != null && !topRt.gameObject.activeSelf && noticeOnly;

        void NoticeOnly(bool on)
        {
            if (topRt != null) topRt.gameObject.SetActive(!on);
            noticeOnly = on;
            if (hintPlate != null && on) hintPlate.gameObject.SetActive(false);
            if (metricsPlate != null && on) metricsPlate.gameObject.SetActive(false);
            if (beatPlate != null && on) beatPlate.gameObject.SetActive(false);
        }

        /// 말칸(시험·QA)
        public bool CueShown => cuePlate != null && cuePlate.gameObject.activeSelf;
        public string CueName => cueWho != null ? cueWho.text : "";

        /// A4(2026-11-05) 아래 줄 배치 규칙 하나: 왼쪽 코치 말 / 가운데 박자 띠·입력 칸 / 오른쪽 아래 드릴 안내.
        ///   ① 안내 판은 가운데 묶음 오른쪽 자리(≥ 360)에 들어가게 폭을 줄이고 줄을 바꿈 — 자리가 없으면(4:3) 802 폭 그대로 두고 가운데 묶음을 안내 판 위로 올림(계단, 틈 24)
        ///   ② 코치 말은 가운데 묶음 왼쪽 자리에 — 모자라면 폭을 줄여 두 줄로(최소 420, 글자 크기 자동 32~60), 그래도 모자라면 묶음 위로 올림
        /// 반환 = 가운데 묶음을 올린 양, stackTop = 가운데 묶음 위 끝(아래 기준)
        float LayoutBottom(bool hintOn, bool beatOn, float iconsW, out float stackTop)
        {
            float W = ((RectTransform)transform).rect.width;
            float cW = Mathf.Max(beatOn ? BeatW : 0f, iconsW);
            float cL = W * 0.5f - cW * 0.5f, cR = W * 0.5f + cW * 0.5f;
            float lift = 0f, hintL = W, hintTop = 0f;
            if (hintOn)
            {
                float full = Mathf.Min(802f, W - Mg * 2f);
                float hw = Mathf.Min(hintPW, full);
                if (cW > 0f)
                {
                    float room = W - Mg - (cR + Gap);
                    if (hw > room && room >= HintMinW) hw = room;
                }
                if (Mathf.Abs(hw - hintLayW) > 0.5f)
                {
                    hintLayW = hw;
                    hintLayH = hintPW > hw + 0.5f ? Mathf.Max(60f, hint.GetPreferredValues(hint.text, hw - 44f, 0f).y + 14f) : 60f;
                    hintPlate.rectTransform.sizeDelta = new Vector2(hw, hintLayH);
                }
                hintL = W - Mg - hw; hintTop = HintY + hintLayH;
                if (cW > 0f && hintL < cR + Gap) lift = Mathf.Max(0f, hintTop + Gap - RowY);
            }
            float bandY = RowY + lift;
            var br = beatPlate.rectTransform;
            if (Mathf.Abs(br.anchoredPosition.y - bandY) > 0.5f) br.anchoredPosition = new Vector2(0f, bandY);
            stackTop = iconsW > 0f ? (beatOn ? bandY + BeatH + 14f : bandY) + 125f : beatOn ? bandY + BeatH : 0f;
            cueRaised = false;
            if (cuePlate.gameObject.activeSelf)
            {
                float roomL = cW > 0f ? cL - Gap - Mg : W - Mg * 2f;
                float w = Mathf.Min(cueWantW, W - Mg * 2f), y = RowY;
                if (w > roomL)
                {
                    if (roomL >= CueMinW) w = roomL;
                    else { cueRaised = true; y = Mathf.Max(RowY, stackTop + 14f); }
                }
                if (hintOn && Mg + w > hintL - Gap && y < hintTop + 14f) y = hintTop + 14f;      // 줄 바꾼 안내 판이 코치 말 줄까지 올라옴
                var cr = cuePlate.rectTransform;
                if (Mathf.Abs(cr.sizeDelta.x - w) > 0.5f) cr.sizeDelta = new Vector2(w, CueH);
                if (Mathf.Abs(cr.anchoredPosition.y - y) > 0.5f || Mathf.Abs(cr.anchoredPosition.x - Mg) > 0.5f) cr.anchoredPosition = new Vector2(Mg, y);
                cueTop = y + CueH + 43f;      // 이름띠까지
                if (cueRaised && Mg + w < W * 0.5f - 180f) cueRaised = false;      // '팡!' 판(가운데 360)과 가로로 안 겹치면 그대로
            }
            return lift;
        }

        public void Say(string who, string line)
        {
            Build();
            if (string.IsNullOrEmpty(line)) return;
            UiKit.SetTag(cueTag, cueWho, who, 192f);
            cueText.text = line;
            // 짧은 호출('원-투')은 528 판에 60 · 긴 말은 판을 넓히고(최대 900) 그래도 넘치면 두 줄로 줄여 씀
            cueText.enableAutoSizing = false; cueText.fontSize = 60f;
            float w = cueText.GetPreferredValues(line).x + 38f + 4f;
            cueText.enableAutoSizing = true;
            cueWantW = Mathf.Clamp(w, CueW, CueMaxW);
            cuePlate.rectTransform.sizeDelta = new Vector2(cueWantW, CueH);
            cueLeft = 2.6f + line.Length * 0.05f; cuePlate.gameObject.SetActive(true);
            if (sfx != null && tick != null && !Application.isBatchMode) sfx.PlayOneShot(tick, 0.5f);      // 음성 자리(합성음)
        }

        static readonly HashSet<string> Onoma = new HashSet<string> { "팡!", "퍽!", "빡!", "탁!", "팍!", "쾅!", "쿵!", "짝!", "착!", "툭!" };
        static readonly char[] DigitChars = "0123456789".ToCharArray();
        /// 의성어인지(붓 글꼴로 그릴 글)
        public static bool IsWord(string s) => !string.IsNullOrEmpty(s) && Onoma.Contains(s.Trim());

        public void ShowToast(string s)
        {
            Build();
            // 의성어('팡!')만 붓 글꼴(B안 7) — B2: '!' 로 끝나는 말(페이크!·순서!·걸림!·가드!)은 의성어가 아님 → 목록으로 판별, 나머지는 Noto Black
            // B10: 숫자가 든 글('땀 12')은 Barlow(한글은 대체 글꼴)
            bool word = IsWord(s);
            bool num = !word && !string.IsNullOrEmpty(s) && s.IndexOfAny(DigitChars) >= 0;
            var want = word ? UiKit.FBrush(Fonts) : num ? UiKit.FNum(Fonts) : UiKit.FBlack(Fonts);
            if (want != null && toast.font != want) toast.font = want;
            toast.fontSize = word ? 40f : num ? 36f : 32f;
            toast.text = word ? s : UiKit.Tab(s);
            toastLeft = 0.8f; SetToast(true);
        }

        void SetToast(bool on)
        {
            if (toastPlate == null) return;
            toastPlate.gameObject.SetActive(on);
            ToastUp = on;
            if (on && CombatFx.Instance != null) CombatFx.Instance.HideWord();      // QA(2026-11-05): D05 '콰직!'·'팡!' 판 두 개가 같이 떠 있던 것
        }

        void OnDisable() { ToastUp = false; CombatHud.TopReserve = 0f; }

        /// 머리글 판 폭·자리 — 보일 때(Show)·Update·LateUpdate 마다 정함(드릴 시작·전투 HUD 켜짐이 어느 순서로 와도 지난 자리로 한 프레임 남지 않게. Q32)
        void LateUpdate() => PlaceTop();

        void PlaceTop()
        {
            if (!built || !root.activeSelf || Runner == null) return;
            var d = Runner.Def;
            if (d == null) return;
            var gmI = GymManager.Instance;
            bool cardUp = gmI != null && gmI.Card != null && gmI.Card.Visible;
            bool topOn = !noticeOnly && !cardUp;
            if (d.Id != lastId)
            {
                lastId = d.Id;
                codeText.text = d.Id;
                nameText.text = d.NameKr;
                // 긴 이름('기술 스파링(라이트)')은 판을 넓힘
                float nw = Mathf.Max(nameText.GetPreferredValues(d.NameKr).x, codeText.GetPreferredValues(d.Id).x);
                topW = Mathf.Max(TopW, 343f + nw + 24f);
                topRt.sizeDelta = new Vector2(topW, TopH);
                nameText.rectTransform.sizeDelta = codeText.rectTransform.sizeDelta = new Vector2(nw + 8f, 46f);
            }
            float tw = topW;
            // QA Q32: 스파링(D11)은 전투 HUD(왼쪽 위 '반시우' 체력·기세 판)가 같이 뜸 — 머리글 판이 그 판과 겹치면(좁은 화면) 그 아래로
            if (combatHud == null) combatHud = FindAnyObjectByType<CombatHud>(FindObjectsInactive.Include);
            // 겹치면 먼저 오른쪽으로 비켜 위에 그대로 두고(16:10 은 17px), 오른쪽 여백(38)이 모자라면 판 아래로(4:3)
            float hx = 0f, hy = -34f;
            // 스파링 드릴은 전투 HUD 가 뜨기 전 프레임부터 비켜 둠(전투 HUD 가 같은 프레임 뒤에 켜지면 한 프레임 겹치던 것 — Q32)
            if ((combatHud != null && combatHud.Visible) || Runner.Station is SparStation)
            {
                float half = ((RectTransform)transform).rect.width * 0.5f;
                float need = CombatHud.MeRight + 38f - (half - tw * 0.5f);      // 드릴-5(2026-11-05): 반시우 판과 틈 38 · 위 끝을 판(−38)에 맞춤 · 자리가 없으면 판 아래로
                hy = -(CombatHud.MeBottom - 108f);
                if (need > 0f) { if (half + tw * 0.5f + need <= half * 2f - 38f) hx = need; else hy = -(CombatHud.MeBottom + 24f); }
            }
            if (Mathf.Abs(topRt.anchoredPosition.y - hy) > 0.5f || Mathf.Abs(topRt.anchoredPosition.x - hx) > 0.5f) topRt.anchoredPosition = new Vector2(hx, hy);
            CombatHud.TopReserve = topOn ? -hy + TopH + 24f : 0f;      // 스파링: 머리 위 대상 바·의성어가 머리글 판을 지나가지 않게(드릴-6: 틈 24)
        }

        void Update()
        {
            if (!built || !root.activeSelf || Runner == null) return;
            PlaceTop();
            float rdt = UiKit.RealDt;
            if (cueLeft > 0f) { cueLeft -= rdt; if (cueLeft <= 0f) UiMotion.Hide(cuePlate.gameObject); }      // D8: 100ms 옅어진 뒤
            if (toastLeft > 0f) { toastLeft -= rdt; if (toastLeft <= 0f) SetToast(false); }
            var d = Runner.Def;
            if (d == null) return;
            var gmI = GymManager.Instance;
            bool cardUp = gmI != null && gmI.Card != null && gmI.Card.Visible;
            // QA(2026-11-05): 결과 카드 뒤로 드릴 머리글·타이머가 비치던 것 → 카드 동안 숨김
            bool topOn = !noticeOnly && !cardUp;
            if (topRt.gameObject.activeSelf != topOn) topRt.gameObject.SetActive(topOn);
            if (cardUp && cuePlate.gameObject.activeSelf) { cuePlate.gameObject.SetActive(false); cueLeft = 0f; }
            if (cardUp && toastPlate.gameObject.activeSelf) { SetToast(false); toastLeft = 0f; }      // B13: 결과 카드가 뜨면 '팡!' 알림판도 내림
            var rs = Runner.State;
            int rkey = (int)rs * 1000 + Runner.Round * 10 + d.Rounds;      // 바뀔 때만 글을 새로 씀(매 프레임 문자열·정규식 없게)
            if (rkey != lastRound)
            {
                lastRound = rkey;
                round.text = rs == DrillRunner.Phase.Rest ? "<size=40>휴식</size>" : rs == DrillRunner.Phase.Intro ? "<size=40>준비</size>" : $"{Runner.Round}<size=29><color=#{ColorUtility.ToHtmlStringRGB(UiKit.Grey)}>/{d.Rounds}</color></size>";
            }
            double left = rs == DrillRunner.Phase.Round ? Runner.RoundLeft : rs == DrillRunner.Phase.Rest ? d.RestSec : d.RoundSec;
            int li = Mathf.CeilToInt((float)left);
            if (li != lastSec) { lastSec = li; timer.text = UiKit.Tab($"{li / 60}:{li % 60:00}", 0.5f); }
            // D8: 라운드 마지막 10초 = 주황 + 1초마다 1.04배 맥박(0.25초에 돌아옴, 흔들림 줄이기면 맥박 없음)
            bool last10 = rs == DrillRunner.Phase.Round && li <= 10 && li > 0;
            var tc = last10 ? UiKit.Accent : UiKit.Paper;
            if (timer.color != tc) timer.color = tc;
            float pulse = last10 && !Accessibility.Reduced ? 1f + 0.04f * Mathf.Clamp01(1f - (float)(li - left) / 0.25f) : 1f;
            if (Mathf.Abs(timer.rectTransform.localScale.x - pulse) > 0.0005f) timer.rectTransform.localScale = new Vector3(pulse, pulse, 1f);
            var st = Runner.Station;
            // QA Q35: 결과 카드의 닫기 안내와 드릴 안내가 화면 아래에서 겹쳤음 → 결과·대기 땐 안내를 비움
            string h = st != null && !cardUp && rs != DrillRunner.Phase.Result && rs != DrillRunner.Phase.Idle ? st.Hint : "";
            if (h != hintRaw)
            {
                hintRaw = h ?? "";
                hint.text = UiKit.KeyMark(hintRaw);
                hintPW = hint.GetPreferredValues(hint.text).x + 44f;
                hintLayW = -1f;
            }
            bool hintOn = !noticeOnly && hintRaw.Length > 0;
            if (hintPlate.gameObject.activeSelf != hintOn) hintPlate.gameObject.SetActive(hintOn);
            // 박자 띠
            var beat = st != null && rs == DrillRunner.Phase.Round ? st.Beat : null;
            bool beatOn = beat != null && !noticeOnly && !cardUp;
            beatPlate.gameObject.SetActive(beatOn);
            if (beatOn)
            {
                beat.Visible(Runner.T, 0.25, BeatSpan, visible, d.RoundSec);
                // 표시(□△)가 있는 드릴만 눈금을 아래로 내려 위에 표시 자리 — 없으면 띠 세로 가운데
                bool marks = d.Id == "D02" || d.Id == "D06";
                float ly = marks ? -18f : 0f;
                if (Mathf.Abs(beatLine.rectTransform.anchoredPosition.y - ly) > 0.5f)
                {
                    beatLine.rectTransform.anchoredPosition = new Vector2(BeatLineX - 9f, ly);
                    beatLine2.rectTransform.anchoredPosition = new Vector2(BeatLineX + 9f, ly);
                }
                for (int i = 0; i < MaxTicks; i++)
                {
                    bool on = i < visible.Count;
                    ticks[i].gameObject.SetActive(on);
                    if (!on) continue;
                    var (b, t) = visible[i];
                    float x = BeatLineX + (float)((t - Runner.T) / BeatSpan) * BeatScale;      // 오른쪽에서 왼쪽 기준선으로
                    ticks[i].rectTransform.anchoredPosition = new Vector2(x, ly);
                    int mark = beat.Mark(b);
                    bool taken = beat.Taken(b);
                    ticks[i].color = taken ? UiKit.RuleColor : mark == 1 ? UiKit.Warm : UiKit.Paper;
                    tickMarks[i].text = d.Id == "D02" ? (mark == 1 ? "△" : "") : d.Id == "D06" ? (mark == 1 ? "△" : "□") : "";
                }
            }
            // 입력 칸(지금 칠 칸 = 주황 125 · 나머지 = 먹 106 · 맞음 = 종이 칸 · 틀림 = 먹 칸에 Warm ×)
            //   휴식 중에는 숨김(A4 — 쉬는 시간의 긴 말 900 폭과 같이 뜨던 것)
            var combo = st != null && !cardUp && rs != DrillRunner.Phase.Rest ? st.Combo : null;
            float total = 0f;
            int n = combo != null ? Mathf.Min(combo.Tokens.Count, MaxIcons) : 0;
            for (int i = 0; i < n; i++) total += (i == combo.Next ? 125f : 106f) + (i > 0 ? 4f : 0f);
            float lift = LayoutBottom(hintOn, beatOn, n > 0 ? total : 0f, out float stackTop);
            float baseY = (beatOn ? RowY + BeatH + 14f : RowY) + lift;
            float x0 = -total * 0.5f;
            for (int i = 0; i < MaxIcons; i++)
            {
                bool on = i < n;
                icons[i].gameObject.SetActive(on);
                if (!on) continue;
                var tok = combo.Tokens[i];
                bool judged = i < combo.Ok.Count;
                bool ok = judged && combo.Ok[i];
                bool cur = !judged && i == combo.Next;
                float s = cur ? 125f : 106f;
                var rt = icons[i].rectTransform;
                rt.sizeDelta = new Vector2(s, s);
                rt.anchoredPosition = new Vector2(x0, baseY);
                x0 += s + 4f;
                icons[i].color = cur ? UiKit.Accent : ok ? UiKit.Paper : UiKit.InkPlate;
                // 두 글자 표시('□─' 길게 · '↓△' · 'R2')는 작게 — '─'(U+2500)는 본문 글꼴에서 1em 넘게 그려져 칸을 넘음 → '–'
                bool miss = judged && !ok;
                bool hold = tok.Hold && !miss;
                string icon = miss ? "×" : (tok.Icon ?? "").Replace("─", "");      // QA Q26: × = U+00D7(숫자 글꼴에 없으면 본문 글꼴 대체)
                bool two = icon.Length >= 2;
                iconTexts[i].fontSizeMax = cur ? (two ? 52f : 70f) : (two ? 44f : 58f);
                iconTexts[i].color = cur || ok ? UiKit.Ink : judged ? UiKit.Warm : UiKit.Paper;
                iconTexts[i].text = icon;
                holdBars[i].gameObject.SetActive(hold);
                if (hold)
                {
                    holdBars[i].color = iconTexts[i].color;
                    holdBars[i].rectTransform.sizeDelta = new Vector2(Mathf.Round(s * 0.56f), cur ? 6f : 5f);
                    holdBars[i].rectTransform.anchoredPosition = new Vector2(0f, Mathf.Round(s * 0.12f));
                }
            }
            // '팡!' 판은 입력 칸·박자 띠 위로
            float ty = Mathf.Max(ToastPos.y, stackTop + 12f);
            if (cueRaised && cuePlate.gameObject.activeSelf) ty = Mathf.Max(ty, cueTop + 12f);
            if (Mathf.Abs(toastPlate.rectTransform.anchoredPosition.y - ty) > 0.5f) toastPlate.rectTransform.anchoredPosition = new Vector2(0f, ty);
            // 휴식 중 지표 표
            bool rest = rs == DrillRunner.Phase.Rest && !noticeOnly && !cardUp;
            metricsPlate.gameObject.SetActive(rest);
            if (rest)
            {
                var vals = DrillMetrics.Compute(d, Runner.Log);
                for (int i = 0; i < 5; i++)
                {
                    bool on = i < d.MetricCount;
                    mLabels[i].gameObject.SetActive(on); mValues[i].gameObject.SetActive(on);
                    if (!on) continue;
                    mLabels[i].text = d.Metrics[i].LabelKr;
                    mValues[i].text = d.Metrics[i].Format(vals[i]);
                }
            }
        }
    }
}
