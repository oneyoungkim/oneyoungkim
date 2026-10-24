// 행인1의 메인이벤트 — 드릴 고르기 메뉴 + 화이트보드(docs/12_체육관_설계.md 2장 '먹 원 마커' · 4-5 [시뮬] 토글 · 4-6 DrillBoard)
// 자리 마커 앞에서 × → 메뉴: [직접 하기] [시뮬 — 사유] [돌아가기]. 입력 = HInput Menu 맵(타이틀·일시정지 메뉴와 같은 규칙). 열려 있는 동안 GameState.Modal.
// 화이트보드(D14 자리) = 드릴 14 목록 · 최고 등급 · 시뮬 표시 · 오늘 남은 시뮬 · 검정까지 남은 횟수 · 몸 상태.
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Haengin
{
    [DisallowMultipleComponent]
    public sealed class DrillMenu : MonoBehaviour
    {
        public InputActionAsset Actions;
        public UiFonts Fonts;
        public const int MaxRows = 4;
        sealed class Item { public string Text; public Action Do; public bool Enabled = true; }
        readonly List<Item> items = new List<Item>();
        InputAction nav, submit, cancel;
        bool pSub, pCan, built, open, boardOpen;
        int sel, heldDir; float repeatAt, clock;
        GameObject root, board;
        TextMeshProUGUI head, note, boardText, boardHead;
        struct Row { public RectTransform Rt; public Image Plate, Bar; public TextMeshProUGUI Label; }
        Row[] rows;
        Action onClose;
        public bool Open => open;
        public bool BoardOpen => boardOpen;
        public int Selected => sel;
        public int Count => items.Count;
        public string Label(int i) => i >= 0 && i < items.Count ? items[i].Text : "";

        void Build()
        {
            if (built) return; built = true;
            UiKit.Canvas(gameObject, 13);
            var f = Fonts != null ? Fonts.B : null; var br = Fonts != null ? Fonts.Br : null;
            var c5 = new Vector2(0.5f, 0.5f);
            root = UiKit.Node("메뉴", transform).gameObject; UiKit.Stretch((RectTransform)root.transform);
            var plate = UiKit.Panel("판", root.transform, c5, c5, new Vector2(0f, -40f), new Vector2(760f, 420f), UiKit.Paper);
            head = UiKit.Text("제목", plate.transform, br, 40f, UiKit.Ink, TextAlignmentOptions.Center); UiKit.Place(head.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(720f, 60f));
            note = UiKit.Text("설명", plate.transform, f, 22f, UiKit.Grey, TextAlignmentOptions.Center, true); UiKit.Place(note.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(700f, 60f));
            rows = new Row[MaxRows];
            for (int i = 0; i < MaxRows; i++)
            {
                var p = UiKit.Panel("항목 " + (i + 1), plate.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -160f - i * 62f), new Vector2(640f, 54f), UiKit.Ink);
                var bar = UiKit.Panel("고른 표시", p.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(8f, 54f), UiKit.Accent);
                var l = UiKit.Text("글", p.transform, f, 26f, UiKit.Paper, TextAlignmentOptions.Center); UiKit.Stretch(l.rectTransform);
                rows[i] = new Row { Rt = p.rectTransform, Plate = p, Bar = bar, Label = l };
            }
            board = UiKit.Node("화이트보드", transform).gameObject; UiKit.Stretch((RectTransform)board.transform);
            var bp = UiKit.Panel("보드판", board.transform, c5, c5, new Vector2(0f, 0f), new Vector2(1400f, 820f), new Color(0.96f, 0.96f, 0.95f));
            var frame = UiKit.Panel("틀", bp.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(1400f, 70f), new Color(0.30f, 0.33f, 0.36f));
            boardHead = UiKit.Text("보드 제목", frame.transform, br, 34f, UiKit.Paper, TextAlignmentOptions.Left); UiKit.Place(boardHead.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(1300f, 60f));
            boardText = UiKit.Text("보드 글", bp.transform, Fonts != null ? Fonts.H : f, 27f, new Color(0.12f, 0.16f, 0.36f), TextAlignmentOptions.TopLeft, true); UiKit.Place(boardText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(1320f, 700f));
            root.SetActive(false); board.SetActive(false);
            if (Actions != null)
            {
                nav = Actions.FindAction("Menu/Navigate", false); submit = Actions.FindAction("Menu/Submit", false); cancel = Actions.FindAction("Menu/Cancel", false);
            }
        }

        /// 드릴 자리 메뉴
        public void Show(DrillDef def, GymSave save, Action direct, Action sim, Action close)
        {
            Build();
            items.Clear();
            int today = GymSave.Today;
            var ds = save.Of(def.Id, false);
            head.text = $"{def.Id} {def.NameKr}";
            string best = ds != null && ds.HasBest ? $"최고 {DrillGrader.Letter(ds.BestGrade)}" : "기록 없음";
            note.text = $"{def.RealName}\n{best} · {def.Rounds}R × {def.RoundSec:0}초 · 피로 {def.FatigueCost} · {GymSave.NameOf(def.MainStat)}{(def.SubStat != Stat.None ? "·" + GymSave.NameOf(def.SubStat) : "")}";
            string db = DrillSim.DirectBlocked(def, save, today), sb = DrillSim.Blocked(def, save, today);
            items.Add(new Item { Text = db.Length == 0 ? "직접 하기" : $"직접 하기 — {db}", Do = direct, Enabled = db.Length == 0 });
            items.Add(new Item { Text = sb.Length == 0 ? $"시뮬 (오늘 {save.SimLeft(today)}회 남음)" : $"시뮬 — {sb}", Do = sim, Enabled = sb.Length == 0 });
            items.Add(new Item { Text = "돌아가기", Do = close });
            onClose = close;
            sel = 0;
            OpenMenu();
        }

        void OpenMenu()
        {
            open = true; GameState.Modal = true;
            root.SetActive(true);
            EnableMenuMap(true);
            Refresh();
        }

        public void ShowBoard(DrillBook book, GymSave save, Action close)
        {
            Build();
            boardOpen = true; GameState.Modal = true; onClose = close;
            board.SetActive(true);
            EnableMenuMap(true);
            int today = GymSave.Today;
            boardHead.text = $"혜화 MMA — 오늘 훈련판   ({GameClock.Label})";
            var sb = new System.Text.StringBuilder();
            sb.Append($"피로 {save.Fatigue}/100   땀 {save.Sweat}   시뮬 오늘 {save.SimLeft(today)}/{DrillSim.SimPerDay}회   ");
            for (int i = 0; i < GymSave.StatNames.Length; i++) sb.Append($"{GymSave.StatNames[i]} {save.Stats[i]}  ");
            if (save.Injured(today)) sb.Append($"\n부상: {save.InjuryKind} — 머리 드릴 잠김");
            sb.Append("\n\n");
            if (book != null)
                foreach (var d in book.Drills)
                {
                    var ds = save.Of(d.Id, false);
                    string best = ds != null && ds.HasBest ? DrillGrader.Letter(ds.BestGrade) : "—";
                    string sim = !d.SimAllowed ? "시뮬 불가" : ds != null && ds.SimUnlocked ? (ds.CertDue ? "시뮬(검정 필요)" : $"시뮬 ○ (검정까지 {DrillSim.CertEvery - ds.SimRunsSinceCert})") : "시뮬 —";
                    sb.Append($"{d.Id}  {d.NameKr,-12}  최고 {best}   {sim}{(d.Playable ? "" : "   [자리만]")}\n");
                }
            boardText.text = sb.ToString();
        }

        void EnableMenuMap(bool on)
        {
            if (Actions == null) return;
            var m = Actions.FindActionMap("Menu", false);
            if (m == null) return;
            if (on) { m.Enable(); pSub = submit != null && submit.IsPressed(); pCan = cancel != null && cancel.IsPressed(); heldDir = 0; }
            else m.Disable();
        }

        public void Close()
        {
            if (!open && !boardOpen) return;
            open = boardOpen = false;
            root.SetActive(false); board.SetActive(false);
            GameState.Modal = false;
            EnableMenuMap(false);
            onClose?.Invoke(); onClose = null;
        }

        /// 봇·시험: 글머리로 고르기
        public bool Choose(string startsWith)
        {
            for (int i = 0; i < items.Count; i++) if (items[i].Text.StartsWith(startsWith)) { sel = i; Pick(); return true; }
            return false;
        }

        void Pick()
        {
            if (sel < 0 || sel >= items.Count) return;
            var it = items[sel];
            if (!it.Enabled) { return; }
            open = false; root.SetActive(false); GameState.Modal = false; EnableMenuMap(false);
            var act = it.Do; onClose = null;
            act?.Invoke();
        }

        void Update()
        {
            if (!built || (!open && !boardOpen)) return;
            clock += UiKit.RealDt;
            bool s = submit != null && GameUi.Edge(submit, ref pSub), c = cancel != null && GameUi.Edge(cancel, ref pCan);
            if (boardOpen) { if (s || c) Close(); return; }
            Vector2 v = nav != null ? nav.ReadValue<Vector2>() : Vector2.zero;
            int dir = v.y > 0.5f ? -1 : v.y < -0.5f ? 1 : 0;
            if (dir == 0) heldDir = 0;
            else if (dir != heldDir) { Move(dir); heldDir = dir; repeatAt = clock + 0.4f; }
            else if (clock >= repeatAt) { Move(dir); repeatAt = clock + 0.15f; }
            if (c) { Close(); return; }
            if (s) Pick();
        }

        void Move(int d) { if (items.Count == 0) return; sel = (sel + d + items.Count) % items.Count; Refresh(); }

        void Refresh()
        {
            for (int i = 0; i < MaxRows; i++)
            {
                bool on = i < items.Count;
                rows[i].Rt.gameObject.SetActive(on);
                if (!on) continue;
                bool isSel = i == sel;
                rows[i].Label.text = items[i].Text;
                rows[i].Plate.color = isSel ? UiKit.Accent : items[i].Enabled ? new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.85f) : new Color(UiKit.Grey.r, UiKit.Grey.g, UiKit.Grey.b, 0.6f);
                rows[i].Bar.enabled = isSel;
                rows[i].Label.color = UiKit.Paper;
            }
        }
    }
}
