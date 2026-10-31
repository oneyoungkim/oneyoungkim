// 행인1의 메인이벤트 — 휴대폰(docs/13 3-1 '화면'·11장 PhoneUi): 문자 · 지도(오늘 뜰 수 있는 카드) · 일정 · 가계부 · 목표 · 몸 · 이름.
// 열기/닫기 = Tab · Select(View) 또는 일시정지 메뉴 '휴대폰'. ← → 탭, ○ · Esc · Backspace 닫기. 글은 WorldRunner 가 탭마다 만들어 넘긴다.
using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Haengin
{
    [DisallowMultipleComponent]
    public sealed class PhoneUi : MonoBehaviour
    {
        public UiFonts Fonts;
        public InputActionAsset Actions;
        public static readonly string[] Tabs = { "문자", "지도", "일정", "가계부", "목표", "몸", "이름", "레슨", "가방" };
        /// 탭에서 Enter · × — 그 탭의 일(레슨 예약 · 가방 먹기). WorldRunner 가 넣음. 돌려주는 것 = 그 탭에 할 일이 있나
        public Func<int, bool> Act;
        public Func<int, string> ActLabel;
        /// 탭 글(번호 → 글) — WorldRunner 가 넣음
        public Func<int, string> Content;
        public bool Shown => root != null && root.activeSelf;
        public int Tab { get; private set; }
        public string Body => body != null ? body.text : "";
        public int Opens { get; private set; }

        GameObject root;
        TextMeshProUGUI[] tabText;
        Image[] tabPlate;
        TextMeshProUGUI body, foot;
        InputAction nav, cancel;
        bool pCan; int lastX, openFrame;

        void Build()
        {
            if (root != null) return;
            UiKit.Canvas(gameObject, 62);
            var frame = UiKit.Panel("휴대폰", transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-60f, 0f), new Vector2(720f, 900f), UiKit.Ink);
            root = frame.gameObject;
            var screen = UiKit.Panel("화면", frame.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(680f, 840f), UiKit.Paper);
            tabText = new TextMeshProUGUI[Tabs.Length];
            tabPlate = new Image[Tabs.Length];
            for (int i = 0; i < Tabs.Length; i++)
            {
                var p = UiKit.Panel("탭 " + Tabs[i], screen.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f + i * (660f / Tabs.Length), -10f), new Vector2(660f / Tabs.Length - 4f, 50f), UiKit.Ink);
                tabPlate[i] = p;
                tabText[i] = UiKit.Text("글", p.transform, Fonts != null ? Fonts.B : null, 19f, UiKit.Paper, TextAlignmentOptions.Center);
                UiKit.Stretch(tabText[i].rectTransform);
                tabText[i].text = Tabs[i];
            }
            body = UiKit.Text("본문", screen.transform, Fonts != null ? Fonts.B : null, 24f, UiKit.Ink, TextAlignmentOptions.TopLeft, wrap: true);
            UiKit.Place(body.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -80f), new Vector2(630f, 700f));
            body.lineSpacing = 8f;
            body.overflowMode = TextOverflowModes.Ellipsis;
            foot = UiKit.Text("발", screen.transform, Fonts != null ? Fonts.B : null, 19f, UiKit.Grey, TextAlignmentOptions.Center);
            UiKit.Place(foot.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(640f, 30f));
            foot.text = "← → 탭   ○ · Esc · Tab 닫기";
            root.SetActive(false);
        }

        void Awake() => Build();

        public void Open(int tab = 0)
        {
            Build();
            if (Actions != null && nav == null) { nav = Actions.FindAction("Menu/Navigate", false); cancel = Actions.FindAction("Menu/Cancel", false); Actions.FindActionMap("Menu", false)?.Enable(); }
            Tab = Mathf.Clamp(tab, 0, Tabs.Length - 1);
            root.SetActive(true);
            Opens++;
            GameState.Modal = true;
            pCan = cancel != null && cancel.IsPressed();
            lastX = 0; openFrame = Time.frameCount;
            Refresh();
        }

        public void Close()
        {
            Build();
            if (!root.activeSelf) return;
            root.SetActive(false);
            GameState.Modal = false;
        }

        public void SetTab(int t) { Tab = (t + Tabs.Length) % Tabs.Length; Refresh(); }

        public void Refresh()
        {
            if (!Shown) return;
            for (int i = 0; i < Tabs.Length; i++)
            {
                bool sel = i == Tab;
                tabPlate[i].color = sel ? UiKit.Accent : UiKit.Ink;
                tabText[i].color = sel ? UiKit.Ink : UiKit.Paper;       // 주황 판 위 종이색은 대비 3.2
            }
            body.text = Content != null ? Content(Tab) ?? "" : "";
            string al = ActLabel != null ? ActLabel(Tab) : "";
            foot.text = string.IsNullOrEmpty(al) ? "← → 탭   ○ · Esc · Tab 닫기" : $"← → 탭   Enter · × {al}   ○ · Esc 닫기";
        }

        void Update()
        {
            if (!Shown || Time.frameCount == openFrame) return;
            Vector2 v = nav != null ? nav.ReadValue<Vector2>() : Vector2.zero;
            var kb = Keyboard.current;
            if (kb != null) { if (kb.leftArrowKey.isPressed) v.x = -1; if (kb.rightArrowKey.isPressed) v.x = 1; }
            int x = v.x > 0.5f ? 1 : v.x < -0.5f ? -1 : 0;
            if (x != 0 && x != lastX) SetTab(Tab + x);
            lastX = x;
            bool c = cancel != null && GameUi.Edge(cancel, ref pCan);
            if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.tabKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame)) c = true;
            var gp = Gamepad.current;
            if (gp != null && (gp.buttonEast.wasPressedThisFrame || gp.selectButton.wasPressedThisFrame)) c = true;
            if (c) { Close(); return; }
            bool ok = (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) || (gp != null && gp.buttonSouth.wasPressedThisFrame);
            if (ok && Act != null && Time.frameCount > openFrame + 1) Act(Tab);
        }
    }
}
