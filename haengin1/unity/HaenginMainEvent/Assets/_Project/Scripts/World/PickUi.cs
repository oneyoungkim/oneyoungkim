// 행인1의 메인이벤트 — 목록 고르기(docs/13 14-6): 가게 차림표 · 가방 · 레슨 예약(코치 → 시간 → 결제). 선택지(ChoiceUi)는 3개까지라 따로 둔다.
// ↑ ↓ (W·S · 스틱) 고르기 · Enter · Space · E · × 고름 · Esc · Backspace · ○ 그만(−1). 열려 있는 동안 GameState.Modal.
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Haengin
{
    [DisallowMultipleComponent]
    public sealed class PickUi : MonoBehaviour
    {
        public UiFonts Fonts;
        public InputActionAsset Actions;
        public const int Max = 9;
        public bool Shown => root != null && root.activeSelf;
        public int Selected { get; private set; }
        public int Count { get; private set; }
        public string Title => title != null ? title.text : "";
        public string Row(int i) => rows != null && i >= 0 && i < Count ? rows[i].text : "";
        public int Opens { get; private set; }

        GameObject root;
        TextMeshProUGUI title, foot;
        TextMeshProUGUI[] rows;
        Image[] plates;
        InputAction nav, cancel;
        bool pCan; int lastY, openFrame, forced = -2;

        void Build()
        {
            if (root != null) return;
            UiKit.Canvas(gameObject, 64);
            var frame = UiKit.Panel("고르기", transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 120f + Max * 58f), UiKit.Ink);
            root = frame.gameObject;
            title = UiKit.Text("제목", frame.transform, Fonts != null ? Fonts.Br : null, 32f, UiKit.Paper, TextAlignmentOptions.Left);
            UiKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -16f), new Vector2(770f, 44f));
            rows = new TextMeshProUGUI[Max]; plates = new Image[Max];
            for (int i = 0; i < Max; i++)
            {
                var p = UiKit.Panel("줄 " + i, frame.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -70f - i * 58f), new Vector2(780f, 52f), UiKit.Paper);
                plates[i] = p;
                rows[i] = UiKit.Text("글", p.transform, Fonts != null ? Fonts.B : null, 23f, UiKit.Ink, TextAlignmentOptions.Left);
                UiKit.Place(rows[i].rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(752f, 48f));
                rows[i].overflowMode = TextOverflowModes.Ellipsis;
            }
            foot = UiKit.Text("발", frame.transform, Fonts != null ? Fonts.B : null, 19f, UiKit.PaperDim, TextAlignmentOptions.Center);
            UiKit.Place(foot.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(780f, 30f));
            root.SetActive(false);
        }

        void Awake() => Build();

        /// 고르기(−1 = 그만). 시험은 Force(i)
        public IEnumerator Pick(string head, string[] options, string footLine, Action<int> done)
        {
            Build();
            if (Actions != null && nav == null) { nav = Actions.FindAction("Menu/Navigate", false); cancel = Actions.FindAction("Menu/Cancel", false); Actions.FindActionMap("Menu", false)?.Enable(); }
            Count = Mathf.Min(Max, options.Length);
            title.text = head;
            foot.text = string.IsNullOrEmpty(footLine) ? "↑ ↓ 고르기   Enter · E 고름   Esc · ○ 그만" : footLine;
            for (int i = 0; i < Max; i++) { plates[i].gameObject.SetActive(i < Count); if (i < Count) rows[i].text = options[i]; }
            var rt = (RectTransform)root.transform;
            rt.sizeDelta = new Vector2(820f, 120f + Count * 58f);
            Selected = 0; lastY = 0; forced = -2;
            root.SetActive(true);
            Opens++;
            bool modalWas = GameState.Modal;
            GameState.Modal = true;
            pCan = cancel != null && cancel.IsPressed();
            openFrame = Time.frameCount;
            Refresh();
            int pick = -2;
            while (pick == -2)
            {
                yield return null;
                if (forced != -2) { pick = forced; break; }
                if (Time.frameCount <= openFrame + 1) continue;
                Vector2 v = nav != null ? nav.ReadValue<Vector2>() : Vector2.zero;
                var kb = Keyboard.current; var gp = Gamepad.current;
                if (kb != null) { if (kb.upArrowKey.isPressed || kb.wKey.isPressed) v.y = 1; if (kb.downArrowKey.isPressed || kb.sKey.isPressed) v.y = -1; }
                int y = v.y > 0.5f ? 1 : v.y < -0.5f ? -1 : 0;
                if (y != 0 && y != lastY) { Selected = (Selected - y + Count) % Mathf.Max(1, Count); Refresh(); }
                lastY = y;
                bool ok = (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame)) || (gp != null && gp.buttonSouth.wasPressedThisFrame);
                bool no = (cancel != null && GameUi.Edge(cancel, ref pCan)) || (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame)) || (gp != null && gp.buttonEast.wasPressedThisFrame);
                if (ok && Count > 0) pick = Selected;
                else if (no) pick = -1;
            }
            root.SetActive(false);
            GameState.Modal = modalWas;
            done?.Invoke(pick);
        }

        public void Force(int i) => forced = i;
        public void Hide() { Build(); if (root.activeSelf) { root.SetActive(false); GameState.Modal = false; } }

        void Refresh()
        {
            for (int i = 0; i < Count; i++)
            {
                bool sel = i == Selected;
                plates[i].color = sel ? UiKit.Accent : UiKit.Paper;
                rows[i].color = UiKit.Ink;       // 주황 판 위 종이색은 대비 3.2
            }
        }
    }
}
