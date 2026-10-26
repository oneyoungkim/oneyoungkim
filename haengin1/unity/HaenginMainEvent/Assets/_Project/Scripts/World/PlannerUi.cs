// 행인1의 메인이벤트 — 주간 계획 화면(docs/13 3-4 '주간 계획(맡기기)'·8-2 일요일 밤): 2주 표(날짜 × 새벽·저녁·밤·주말) → [맡기기] / [직접 하기].
// ↑↓ 날짜·아래 단추, ← → 칸, × 칸 바꾸기(그 칸 보기를 돌림). 이벤트 날(날짜 박힌 핵심 카드)은 붉게 — 맡기기가 그날 아침에 멈춘다.
// 맡긴 뒤 몽타주: 하루 한 줄씩(원고 5화 봉투 몽타주처럼) → 결산 줄.
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Haengin
{
    [DisallowMultipleComponent]
    public sealed class PlannerUi : MonoBehaviour
    {
        public UiFonts Fonts;
        public InputActionAsset Actions;
        public const int Rows = 14;
        public bool Shown => root != null && root.activeSelf;
        public int Row { get; private set; }
        public int Col { get; private set; }
        public string MontageText => montage != null ? montage.text : "";
        /// 맡긴 결과 몽타주가 떠 있나(QA 봇)
        public bool InMontage => montage != null && montage.gameObject.activeSelf;

        GameObject root;
        TextMeshProUGUI head, foot, montage;
        TextMeshProUGUI[,] cells;
        Image[,] plates;
        Image[] buttons;
        TextMeshProUGUI[] buttonText;
        List<PlanDay> plan;
        Func<int, string> eventOf;
        InputAction nav, submit, cancel;
        bool pSub, pCan; int lastDir; int forced = -1;

        void Build()
        {
            if (root != null) return;
            UiKit.Canvas(gameObject, 63);
            var dim = UiKit.Panel("어둡게", transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.6f));
            UiKit.Stretch(dim.rectTransform);
            root = dim.gameObject;
            var card = UiKit.Panel("계획표", dim.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500f, 960f), UiKit.Paper);
            head = UiKit.Text("머리", card.transform, Fonts != null ? Fonts.Br : null, 44f, UiKit.Ink, TextAlignmentOptions.Left);
            UiKit.Place(head.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(50f, -20f), new Vector2(1400f, 60f));
            string[] colNames = { "날짜", "새벽", "저녁", "밤", "주말" };
            float[] colX = { 50f, 330f, 600f, 870f, 1140f };
            for (int c = 0; c < 5; c++)
            {
                var t = UiKit.Text("열 " + c, card.transform, Fonts != null ? Fonts.B : null, 22f, UiKit.Grey, TextAlignmentOptions.Left);
                UiKit.Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(colX[c], -88f), new Vector2(260f, 30f));
                t.text = colNames[c];
            }
            cells = new TextMeshProUGUI[Rows, 5];
            plates = new Image[Rows, 5];
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < 5; c++)
                {
                    var p = UiKit.Panel($"칸 {r},{c}", card.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(colX[c] - 8f, -124f - r * 52f), new Vector2(c == 0 ? 270f : 255f, 46f), new Color(0f, 0f, 0f, 0f));
                    plates[r, c] = p;
                    var t = UiKit.Text("글", p.transform, Fonts != null ? Fonts.B : null, 22f, UiKit.Ink, TextAlignmentOptions.Left);
                    UiKit.Place(t.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(c == 0 ? 260f : 245f, 44f));
                    cells[r, c] = t;
                }
            buttons = new Image[2]; buttonText = new TextMeshProUGUI[2];
            string[] bt = { "맡기기 — 이벤트 날까지 자동", "직접 하기(닫기)" };
            for (int i = 0; i < 2; i++)
            {
                buttons[i] = UiKit.Panel("단추 " + i, card.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-260f + i * 520f, 40f), new Vector2(480f, 60f), UiKit.Ink);
                buttonText[i] = UiKit.Text("글", buttons[i].transform, Fonts != null ? Fonts.B : null, 26f, UiKit.Paper, TextAlignmentOptions.Center);
                UiKit.Stretch(buttonText[i].rectTransform);
                buttonText[i].text = bt[i];
            }
            foot = UiKit.Text("발", card.transform, Fonts != null ? Fonts.B : null, 20f, UiKit.Grey, TextAlignmentOptions.Right);
            UiKit.Place(foot.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-50f, 108f), new Vector2(1400f, 30f));
            foot.text = "↑↓ 날짜   ← → 칸   × 바꾸기   맡기면: 돈 100% · 부 스탯 75% · 관계 이벤트 없음 · 시간·피로 그대로";
            montage = UiKit.Text("몽타주", dim.transform, Fonts != null ? Fonts.B : null, 28f, UiKit.Paper, TextAlignmentOptions.TopLeft, wrap: true);
            UiKit.Place(montage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400f, 900f));
            montage.gameObject.SetActive(false);
            root.SetActive(false);
        }

        void Awake() => Build();

        void Bind()
        {
            if (nav != null || Actions == null) return;
            nav = Actions.FindAction("Menu/Navigate", false); submit = Actions.FindAction("Menu/Submit", false); cancel = Actions.FindAction("Menu/Cancel", false);
            Actions.FindActionMap("Menu", false)?.Enable();
        }

        /// 테스트·봇: 단추를 고른다(0 맡기기 · 1 닫기)
        public void Force(int button) => forced = button;

        /// 계획을 보여 주고 고를 때까지. done(맡기기?)
        public IEnumerator Edit(List<PlanDay> p, Func<int, string> eventDay, string title, Action<bool> done, int auto = -1, float autoSecs = 0.3f)
        {
            Build(); Bind();
            plan = p; eventOf = eventDay;
            head.text = title;
            Row = Rows; Col = 0; forced = -1;
            float shown = 0f;
            root.SetActive(true);
            montage.gameObject.SetActive(false);
            transform.GetChild(0).GetChild(0).gameObject.SetActive(true);
            GameState.Modal = true;
            Refresh();
            pSub = submit != null && submit.IsPressed(); pCan = cancel != null && cancel.IsPressed(); lastDir = 0;
            yield return null;
            int pick = -1;
            while (pick < 0)
            {
                shown += UiKit.RealDt;
                if (forced >= 0) { pick = forced; break; }
                if (auto >= 0 && shown >= autoSecs) { pick = auto; break; }
                int d = Dir();
                if (d == 2) { Row = Mathf.Max(0, Row - 1); Refresh(); }
                else if (d == 4) { Row = Mathf.Min(Rows, Row + 1); Refresh(); }
                else if (d == 1) { Col = Mathf.Max(0, Col - 1); Refresh(); }
                else if (d == 3) { Col = Mathf.Min(Row == Rows ? 1 : 3, Col + 1); Refresh(); }
                bool s = submit != null && GameUi.Edge(submit, ref pSub);
                var kb = Keyboard.current; if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) s = true;
                bool c = cancel != null && GameUi.Edge(cancel, ref pCan);
                if (c) { pick = 1; break; }
                if (s)
                {
                    if (Row == Rows) { pick = Col == 0 ? 0 : 1; break; }
                    if (Row < plan.Count) Cycle(plan[Row], Col);
                    Refresh();
                }
                yield return null;
            }
            GameState.Modal = false;
            root.SetActive(false);
            done?.Invoke(pick == 0);
        }

        int Dir()
        {
            Vector2 v = nav != null ? nav.ReadValue<Vector2>() : Vector2.zero;
            int d = v.x < -0.5f ? 1 : v.y > 0.5f ? 2 : v.x > 0.5f ? 3 : v.y < -0.5f ? 4 : 0;
            int e = d != 0 && d != lastDir ? d : 0;
            lastDir = d;
            return e;
        }

        static void Cycle(PlanDay p, int col)
        {
            var opts = WeekPlanner.OptionsFor(p, col);
            if (opts.Length == 0) return;
            int i = Array.IndexOf(opts, WeekPlanner.Get(p, col));
            WeekPlanner.SetSlot(p, col, opts[(i + 1) % opts.Length]);
        }

        void Refresh()
        {
            for (int r = 0; r < Rows; r++)
            {
                bool has = plan != null && r < plan.Count;
                var p = has ? plan[r] : null;
                string ev = has && eventOf != null ? eventOf(p.Day) : null;
                for (int c = 0; c < 5; c++)
                {
                    var t = cells[r, c];
                    if (!has) { t.text = ""; plates[r, c].color = new Color(0, 0, 0, 0); continue; }
                    if (c == 0) t.text = $"{p.Day / 100}/{p.Day % 100}({GameClock.WeekdayOfKey(p.Day)}){(ev != null ? " ◆" : "")}";
                    else t.text = ev != null && c == 1 ? $"이벤트 — {ev}" : ev != null ? "" : WeekPlanner.Get(p, c - 1);
                    bool sel = Row == r && Col == c - 1 && c > 0;
                    plates[r, c].color = sel ? new Color(UiKit.Accent.r, UiKit.Accent.g, UiKit.Accent.b, 0.35f) : ev != null ? new Color(UiKit.Endure.r, UiKit.Endure.g, UiKit.Endure.b, 0.15f) : new Color(0, 0, 0, r % 2 == 0 ? 0.04f : 0f);
                    t.color = ev != null ? UiKit.Endure : UiKit.Ink;
                }
            }
            for (int i = 0; i < 2; i++) buttons[i].color = Row == Rows && Col == i ? UiKit.Accent : UiKit.Ink;
        }

        /// 맡긴 결과 몽타주(하루 한 줄 · 0.22초) → 결산 줄. 봇은 빠르게
        public IEnumerator Montage(IList<string> days, string summary, bool fast)
        {
            Build();
            root.SetActive(true);
            transform.GetChild(0).GetChild(0).gameObject.SetActive(false);
            montage.gameObject.SetActive(true);
            var sb = new System.Text.StringBuilder();
            int start = Mathf.Max(0, days.Count - 16);
            if (start > 0) sb.Append($"… 앞의 {start}일\n");
            for (int i = start; i < days.Count; i++)
            {
                sb.Append(days[i]).Append('\n');
                montage.text = sb.ToString();
                float t = 0f; while (t < (fast ? 0.03f : 0.22f)) { t += UiKit.RealDt; yield return null; }
            }
            sb.Append('\n').Append(summary);
            montage.text = sb.ToString();
            float w = 0f; while (w < (fast ? 0.3f : 2.2f)) { w += UiKit.RealDt; yield return null; }
            montage.gameObject.SetActive(false);
            transform.GetChild(0).GetChild(0).gameObject.SetActive(true);
            root.SetActive(false);
        }
    }
}
