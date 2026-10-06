// 행인1의 메인이벤트 — 선택지(docs/09_M3_버티컬슬라이스_설계.md 2-2·2-3): 1~3개 세로 목록. 십자키·왼스틱·방향키(Talk/Navigate), × · Enter 고르기(Talk/Next),
// 마우스는 움직였을 때만 올린 항목을 고르고 누르면 고름(GameUi 메뉴와 같은 규칙). 실제 시간으로 돈다.
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Haengin
{
    [DisallowMultipleComponent]
    public sealed class ChoiceUi : MonoBehaviour
    {
        public UiFonts Fonts;
        public TalkInput In;
        public const int Max = 3;

        public bool Visible => root != null && root.activeSelf;
        public int Selected { get; private set; }
        public int Count { get; private set; }
        public string Label(int i) => rows != null && i >= 0 && i < Count ? rows[i].Label.text : "";
        /// 지금 고르는 중인 보기 위치(테스트·봇 — 화면 좌표)
        public RectTransform Row(int i) => rows != null && i >= 0 && i < Max ? rows[i].Rt : null;

        struct R { public RectTransform Rt; public Image Plate, Bar; public TextMeshProUGUI Label; }
        GameObject root;
        R[] rows;
        Vector2 lastMouse;
        int forced = -1;

        void Build()
        {
            if (root != null) return;
            UiKit.Canvas(gameObject, 50);
            var n = UiKit.Node("선택지", transform);
            UiKit.Stretch(n);
            root = n.gameObject;
            rows = new R[Max];
            for (int i = 0; i < Max; i++)
            {
                var c5 = new Vector2(0.5f, 0.5f);
                var plate = UiKit.Panel("보기 " + (i + 1), root.transform, c5, c5, Vector2.zero, new Vector2(620f, 58f), UiKit.Ink);
                var bar = UiKit.Panel("고른 표시", plate.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(8f, 58f), UiKit.Accent);
                var label = UiKit.Text("글", plate.transform, Fonts != null ? Fonts.B : null, 27f, UiKit.Paper, TextAlignmentOptions.Center);
                UiKit.Stretch(label.rectTransform);
                rows[i] = new R { Rt = (RectTransform)plate.transform, Plate = plate, Bar = bar, Label = label };
            }
            root.SetActive(false);
        }

        void Awake() => Build();

        /// 보기를 띄우고 고를 때까지 기다린다. done(번호)
        public IEnumerator Ask(string[] options, Action<int> done)
        {
            Build();
            Count = Mathf.Clamp(options.Length, 1, Max);
            Selected = 0;
            forced = -1;
            for (int i = 0; i < Max; i++)
            {
                bool on = i < Count;
                rows[i].Rt.gameObject.SetActive(on);
                if (!on) continue;
                rows[i].Label.text = options[i];
                rows[i].Rt.anchoredPosition = new Vector2(220f, 40f - i * 70f);
            }
            Refresh();
            root.SetActive(true);
            lastMouse = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
            yield return null;     // 띄운 프레임의 입력은 버린다
            int pick = -1;
            while (pick < 0)
            {
                if (forced >= 0) { pick = forced; break; }
                var t = In != null ? In : TalkInput.Instance;
                if (!GameState.Paused && (t == null || !t.Blocked))
                {
                    if (t != null && t.NavStep != 0) { Selected = (Selected + t.NavStep + Count) % Count; Refresh(); }
                    var m = Mouse.current;
                    if (m != null)
                    {
                        var mp = m.position.ReadValue();
                        int over = RowAt(mp);
                        if ((mp - lastMouse).sqrMagnitude > 4f && over >= 0 && over != Selected) { Selected = over; Refresh(); }
                        lastMouse = mp;
                        if (m.leftButton.wasPressedThisFrame && over >= 0) { Selected = over; pick = over; break; }
                    }
                    if (t != null && t.Next) { pick = Selected; break; }
                }
                yield return null;
            }
            root.SetActive(false);
            done?.Invoke(pick);
        }

        /// 테스트·봇: 지금 띄운 선택을 i 로 고른다
        public void Force(int i) => forced = Mathf.Clamp(i, 0, Mathf.Max(0, Count - 1));

        /// '참기' 강제: 선택 표시를 i 로 옮겨 보인다
        public void Highlight(int i) { Build(); Selected = Mathf.Clamp(i, 0, Mathf.Max(0, Count - 1)); Refresh(); }

        void Refresh()
        {
            for (int i = 0; i < Max; i++)
            {
                bool sel = i == Selected;
                rows[i].Plate.color = sel ? UiKit.Paper : new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.78f);
                rows[i].Bar.enabled = sel;
                rows[i].Label.color = sel ? UiKit.Ink : UiKit.Paper;
            }
        }

        int RowAt(Vector2 screen)
        {
            for (int i = 0; i < Count; i++)
                if (RectTransformUtility.RectangleContainsScreenPoint(rows[i].Rt, screen, null)) return i;
            return -1;
        }
    }
}
