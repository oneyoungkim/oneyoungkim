// 행인1의 메인이벤트 — 드릴 화면(docs/12_체육관_설계.md 4-6 CoachCaller + 라운드 타이머 + 박자 띠 + 알림)
// 위: 드릴 이름·라운드 / 가운데 위: 타이머(붓 글꼴) / 오른쪽 위: 코치 큐(누가 · 말, 음성 자리 = 짧은 합성 '틱') / 아래: 콤비 아이콘 띠(맞음 = 종이, 틀림 = 먹으로 지워짐) + 박자 띠 / 알림 토스트
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
        TextMeshProUGUI nameText, round, timer, cueWho, cueText, hint, toast, metrics;
        RectTransform topRt;
        CombatHud combatHud;
        public float PlateY => topRt != null ? topRt.anchoredPosition.y : 0f;
        Image timerPlate, hintPlate, metricsPlate;
        bool noticeOnly;
        public string HintText => hint != null && hint.gameObject.activeInHierarchy ? hint.text : "";
        public void ClearHint() { if (hint != null) hint.text = ""; }
        Image cuePlate, beatPlate, beatLine, toastPlate;
        readonly List<Image> ticks = new List<Image>();
        readonly List<TextMeshProUGUI> tickMarks = new List<TextMeshProUGUI>();
        readonly List<Image> icons = new List<Image>();
        readonly List<TextMeshProUGUI> iconTexts = new List<TextMeshProUGUI>();
        readonly List<(int, double)> visible = new List<(int, double)>();
        float cueLeft, toastLeft;
        AudioSource sfx;
        AudioClip tick;
        public string Cue => cueText != null ? cueText.text : "";
        public string Toast => toast != null && toast.gameObject.activeSelf ? toast.text : "";
        /// 알림판 자리(아래 가운데 기준) — 아이콘(아래 150) 위, 얼굴 높이 아래
        public static readonly Vector2 ToastPos = new Vector2(0f, 250f);
        public RectTransform ToastRect => toastPlate != null ? toastPlate.rectTransform : null;
        public bool Visible => root != null && root.activeSelf;
        const int MaxTicks = 16, MaxIcons = 6;
        public const float BeatSpan = 1.6f;

        void Build()
        {
            if (built) return; built = true;
            UiKit.Canvas(gameObject, 12);
            root = UiKit.Node("드릴", transform).gameObject;
            UiKit.Stretch((RectTransform)root.transform);
            var f = Fonts != null ? Fonts.B : null; var br = Fonts != null ? Fonts.Br : null;
            var c5 = new Vector2(0.5f, 0.5f);
            var top = UiKit.Panel("이름판", root.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -36f), new Vector2(520f, 64f), new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.95f));
            topRt = top.rectTransform;
            nameText = UiKit.Text("이름", top.transform, br, 30f, UiKit.Paper, TextAlignmentOptions.Left); UiKit.Place(nameText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(380f, 60f));
            round = UiKit.Text("라운드", top.transform, f, 24f, UiKit.Paper, TextAlignmentOptions.Right); UiKit.Place(round.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(140f, 60f));
            // 2026-10-31: 판 없는 큰 타이머가 천장·벽 위에 그대로 → 먹 판 위 종이색
            timerPlate = UiKit.Panel("타이머 판", root.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(190f, 80f), new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.95f));
            timer = UiKit.Text("타이머", timerPlate.transform, br, 60f, UiKit.Paper, TextAlignmentOptions.Center); UiKit.Stretch(timer.rectTransform);
            cuePlate = UiKit.Panel("큐판", root.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -36f), new Vector2(560f, 92f), UiKit.Paper);
            var bar = UiKit.Panel("큐 띠", cuePlate.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(10f, 92f), UiKit.Accent);
            cueWho = UiKit.Text("누가", cuePlate.transform, f, 22f, UiKit.AccentText, TextAlignmentOptions.Left); UiKit.Place(cueWho.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -6f), new Vector2(520f, 30f));
            cueText = UiKit.Text("말", cuePlate.transform, f, 30f, UiKit.Ink, TextAlignmentOptions.Left, true); UiKit.Place(cueText.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 6f), new Vector2(524f, 50f)); cueText.enableAutoSizing = true; cueText.fontSizeMin = 18f; cueText.fontSizeMax = 30f;
            // 2026-10-31: 왼쪽 아래 조작 안내와 겹치던 판 없는 드릴 안내 → 오른쪽 아래 판 위로
            hintPlate = UiKit.Panel("안내 판", root.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-16f, 14f), new Vector2(40f, 40f), new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.95f));
            UiKit.Fit(hintPlate, 14, 14, 6, 7);
            hint = UiKit.Text("안내", hintPlate.transform, f, 22f, UiKit.Paper, TextAlignmentOptions.Center);
            hintPlate.gameObject.SetActive(false);
            beatPlate = UiKit.Panel("박자 띠", root.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 56f), new Vector2(1200f, 88f), new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.95f));
            beatLine = UiKit.Panel("기준선", beatPlate.transform, c5, c5, Vector2.zero, new Vector2(6f, 88f), UiKit.Accent);
            for (int i = 0; i < MaxTicks; i++)
            {
                var t = UiKit.Panel("박자 " + i, beatPlate.transform, c5, c5, Vector2.zero, new Vector2(10f, 36f), UiKit.Paper); t.gameObject.SetActive(false); ticks.Add(t);
                var m = UiKit.Text("표시", t.transform, f, 22f, UiKit.Paper, TextAlignmentOptions.Center); UiKit.Place(m.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 2f), new Vector2(60f, 30f)); tickMarks.Add(m);
            }
            for (int i = 0; i < MaxIcons; i++)
            {
                var ic = UiKit.Panel("아이콘 " + i, root.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-250f + i * 100f, 150f), new Vector2(84f, 84f), UiKit.Paper); ic.gameObject.SetActive(false); icons.Add(ic);
                var tx = UiKit.Text("글", ic.transform, br, 40f, UiKit.Ink, TextAlignmentOptions.Center); UiKit.Stretch(tx.rectTransform); iconTexts.Add(tx);
            }
            // QA 2차(2026-10-28): 화면 가운데 위(+120)는 드릴 카메라에서 두 사람 얼굴 자리 — D04 미트 '팡!' 판이 이윤준·시우 얼굴을 가림 → 아래 버튼 아이콘 바로 위로
            toastPlate = UiKit.Panel("알림판", root.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), ToastPos, new Vector2(360f, 70f), UiKit.Ink);
            toast = UiKit.Text("알림", toastPlate.transform, br, 40f, UiKit.Paper, TextAlignmentOptions.Center); UiKit.Stretch(toast.rectTransform);
            toastPlate.gameObject.SetActive(false);
            metricsPlate = UiKit.Panel("지표 판", root.transform, c5, c5, new Vector2(0f, 40f), new Vector2(760f, 300f), new Color(UiKit.Paper.r, UiKit.Paper.g, UiKit.Paper.b, 0.96f));
            metrics = UiKit.Text("지표 미리", metricsPlate.transform, f, 26f, UiKit.Ink, TextAlignmentOptions.Center, true); UiKit.Stretch(metrics.rectTransform, 16f); metricsPlate.gameObject.SetActive(false);
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

        public void Show(bool on) { Build(); root.SetActive(on); GameUi.HintOverride = on ? "일시정지 Esc · Options" : null; if (!on) { cueLeft = 0f; toastLeft = 0f; } NoticeOnly(false); }

        /// QA Q34: 드릴을 못 시작할 때(문 닫음·회원만·막힘) 관장 말만 — 예전엔 Show(true) 로 지난 드릴의 이름판·타이머(0:40·R3/3)·안내까지 다시 떠 세계 HUD 와 겹쳤음
        public void Notice(string who, string line)
        {
            Build(); root.SetActive(true); NoticeOnly(true); Say(who, line);
        }
        public bool NoticeShown => root != null && root.activeSelf && topRt != null && !topRt.gameObject.activeSelf;

        void NoticeOnly(bool on)
        {
            if (topRt != null) topRt.gameObject.SetActive(!on);
            if (timerPlate != null) timerPlate.gameObject.SetActive(!on);
            noticeOnly = on;
            if (hintPlate != null && on) hintPlate.gameObject.SetActive(false);
            if (metricsPlate != null && on) metricsPlate.gameObject.SetActive(false);
            if (beatPlate != null && on) beatPlate.gameObject.SetActive(false);
        }

        /// 말칸(시험·QA)
        public bool CueShown => cuePlate != null && cuePlate.gameObject.activeSelf;
        public string CueName => cueWho != null ? cueWho.text : "";

        public void Say(string who, string line)
        {
            Build();
            if (string.IsNullOrEmpty(line)) return;
            cueWho.text = who; cueText.text = line; cueLeft = 2.6f + line.Length * 0.05f; cuePlate.gameObject.SetActive(true);
            if (sfx != null && tick != null && !Application.isBatchMode) sfx.PlayOneShot(tick, 0.5f);      // 음성 자리(합성음)
        }

        public void ShowToast(string s) { Build(); toast.text = s; toastLeft = 0.8f; toastPlate.gameObject.SetActive(true); }

        void Update()
        {
            if (!built || !root.activeSelf || Runner == null) return;
            float rdt = UiKit.RealDt;
            if (cueLeft > 0f) { cueLeft -= rdt; if (cueLeft <= 0f) cuePlate.gameObject.SetActive(false); }
            if (toastLeft > 0f) { toastLeft -= rdt; if (toastLeft <= 0f) toastPlate.gameObject.SetActive(false); }
            var d = Runner.Def;
            if (d == null) return;
            nameText.text = $"{d.Id} {d.NameKr}";
            // QA Q32: 스파링(D11)은 전투 HUD(왼쪽 위 '반시우' 체력·기세)가 같이 떠서 이름판과 겹쳐 'D시우기술 스파링'으로 읽혔음 → 그때는 이름판을 그 아래로
            if (combatHud == null) combatHud = FindAnyObjectByType<CombatHud>(FindObjectsInactive.Include);
            if (topRt != null) topRt.anchoredPosition = new Vector2(40f, combatHud != null && combatHud.Visible ? -176f : -36f);
            round.text = Runner.State == DrillRunner.Phase.Rest ? "휴식" : Runner.State == DrillRunner.Phase.Intro ? "준비" : $"R{Runner.Round} / {d.Rounds}";
            double left = Runner.State == DrillRunner.Phase.Round ? Runner.RoundLeft : Runner.State == DrillRunner.Phase.Rest ? d.RestSec : d.RoundSec;
            int li = Mathf.CeilToInt((float)left);
            timer.text = $"{li / 60}:{li % 60:00}";
            var st = Runner.Station;
            // QA Q35: 결과 카드의 '× (A) · Enter — 닫기' 와 드릴 안내('박자에 × 점프 — 발끝')가 화면 아래에서 겹쳤음 → 결과·대기 땐 안내를 비움
            var gmI = GymManager.Instance;
            bool cardUp = gmI != null && gmI.Card != null && gmI.Card.Visible;
            hint.text = st != null && !cardUp && Runner.State != DrillRunner.Phase.Result && Runner.State != DrillRunner.Phase.Idle ? st.Hint : "";
            bool hintOn = !noticeOnly && !string.IsNullOrEmpty(hint.text);
            if (hintPlate.gameObject.activeSelf != hintOn) hintPlate.gameObject.SetActive(hintOn);
            // 박자 띠
            var beat = st != null && Runner.State == DrillRunner.Phase.Round ? st.Beat : null;
            beatPlate.gameObject.SetActive(beat != null);
            if (beat != null)
            {
                beat.Visible(Runner.T, 0.25, BeatSpan, visible, d.RoundSec);
                for (int i = 0; i < MaxTicks; i++)
                {
                    bool on = i < visible.Count;
                    ticks[i].gameObject.SetActive(on);
                    if (!on) continue;
                    var (b, t) = visible[i];
                    float x = (float)((t - Runner.T) / BeatSpan) * 560f;      // 오른쏙에서 왼쪽 기준선으로
                    ticks[i].rectTransform.anchoredPosition = new Vector2(x, -18f);
                    int mark = beat.Mark(b);
                    bool taken = beat.Taken(b);
                    ticks[i].color = taken ? new Color(UiKit.Paper.r, UiKit.Paper.g, UiKit.Paper.b, 0.25f) : mark == 1 ? UiKit.Warm : UiKit.Paper;
                    tickMarks[i].text = d.Id == "D02" ? (mark == 1 ? "△" : "") : d.Id == "D06" ? (mark == 1 ? "△" : "□") : "";
                }
            }
            // 콤비 아이콘 띠
            var combo = st != null ? st.Combo : null;
            for (int i = 0; i < MaxIcons; i++)
            {
                bool on = combo != null && i < combo.Tokens.Count;
                icons[i].gameObject.SetActive(on);
                if (!on) continue;
                var tok = combo.Tokens[i];
                iconTexts[i].text = tok.Icon;
                bool judged = i < combo.Ok.Count;
                bool ok = judged && combo.Ok[i];
                icons[i].color = !judged ? (i == combo.Next ? UiKit.Warm : UiKit.Paper) : ok ? new Color(0.72f, 0.82f, 0.68f) : UiKit.Ink;
                iconTexts[i].color = judged && !ok ? UiKit.Accent : UiKit.Ink;
                if (judged && !ok) iconTexts[i].text = "×";      // QA Q26: 예전 글자(U+2715 굵은 곱셈표)는 붓 글꼴(KR_Brush_SDF)에 없어 네모로 — 붓 글꼴에 있는 U+00D7
            }
            float x0 = -(combo != null ? (combo.Tokens.Count - 1) * 50f : 0f);
            for (int i = 0; i < MaxIcons; i++) icons[i].rectTransform.anchoredPosition = new Vector2(x0 + i * 100f, 150f);
            // 휴식 중 지표 미리 보기
            bool rest = Runner.State == DrillRunner.Phase.Rest;
            metricsPlate.gameObject.SetActive(rest);
            if (rest)
            {
                var vals = DrillMetrics.Compute(d, Runner.Log);
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < d.MetricCount; i++) sb.Append(d.Metrics[i].LabelKr).Append("  ").Append(d.Metrics[i].Format(vals[i])).Append('\n');
                metrics.text = sb.ToString();
            }
        }
    }
}
