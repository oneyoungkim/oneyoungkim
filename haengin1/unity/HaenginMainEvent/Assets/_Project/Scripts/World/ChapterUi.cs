// 행인1의 메인이벤트 — 챕터 카드(docs/13 4-3 · 12-2 #1·#10): 장 제목·기간·나이·목표 한 줄 + 조건 목록(■ 달성 / □ 아직),
// 하루 루프 안내 카드, 데모 끝 결산(목표 달성 판정), 다음 장 예고. 화면을 먹으로 덮고 누를 때까지(봇은 짧게).
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Haengin
{
    [DisallowMultipleComponent]
    public sealed class ChapterUi : MonoBehaviour
    {
        public UiFonts Fonts;
        public InputActionAsset Actions;
        public bool Shown => root != null && root.activeSelf;
        public string Title => title != null ? title.text : "";
        public string Body => body != null ? body.text : "";
        public string Kind { get; private set; } = "";
        public int Shows { get; private set; }

        GameObject root;
        TextMeshProUGUI small, title, sub, body, foot;
        InputAction submit;
        bool pSub; bool pressed;

        void Build()
        {
            if (root != null) return;
            UiKit.Canvas(gameObject, 66);
            var bg = UiKit.Panel("챕터 카드", transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, UiKit.Ink);
            UiKit.Stretch(bg.rectTransform);
            root = bg.gameObject;
            small = UiKit.Text("작은 글", bg.transform, Fonts != null ? Fonts.B : null, 28f, new Color(UiKit.Paper.r, UiKit.Paper.g, UiKit.Paper.b, 0.7f), TextAlignmentOptions.Center);
            UiKit.Place(small.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(1600f, 40f));
            title = UiKit.Text("제목", bg.transform, Fonts != null ? Fonts.Br : null, 96f, UiKit.Paper, TextAlignmentOptions.Center);
            UiKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(1700f, 130f));
            sub = UiKit.Text("부제", bg.transform, Fonts != null ? Fonts.B : null, 34f, UiKit.Warm, TextAlignmentOptions.Center, wrap: true);
            UiKit.Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -320f), new Vector2(1500f, 100f));
            body = UiKit.Text("본문", bg.transform, Fonts != null ? Fonts.B : null, 30f, UiKit.Paper, TextAlignmentOptions.TopLeft, wrap: true);
            UiKit.Place(body.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -440f), new Vector2(1300f, 520f));
            body.lineSpacing = 14f;
            foot = UiKit.Text("발", bg.transform, Fonts != null ? Fonts.B : null, 24f, UiKit.PaperDim, TextAlignmentOptions.Right);
            UiKit.Place(foot.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-80f, 50f), new Vector2(1200f, 36f));
            root.SetActive(false);
        }

        void Awake() => Build();

        public void Press() => pressed = true;

        public IEnumerator Show(string kind, string smallLine, string titleLine, string subLine, string bodyText, string footLine, float bot)
        {
            Build();
            if (submit == null && Actions != null) { submit = Actions.FindAction("Menu/Submit", false); Actions.FindActionMap("Menu", false)?.Enable(); }
            Kind = kind;
            small.text = smallLine; title.text = titleLine; sub.text = subLine; body.text = bodyText; foot.text = footLine;
            root.SetActive(true);
            Shows++;
            GameState.Modal = true;
            pressed = false;
            pSub = submit != null && submit.IsPressed();
            float t = 0f;
            yield return null;
            while (true)
            {
                t += UiKit.RealDt;
                if (bot > 0f) { if (t >= bot) break; }
                else if (t > 0.5f)
                {
                    bool s = submit != null && GameUi.Edge(submit, ref pSub);
                    var kb = Keyboard.current; if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame)) s = true;
                    var gp = Gamepad.current; if (gp != null && gp.buttonSouth.wasPressedThisFrame) s = true;
                    var m = Mouse.current; if (m != null && m.leftButton.wasPressedThisFrame) s = true;
                    if (s || pressed) break;
                }
                yield return null;
            }
            root.SetActive(false);
            GameState.Modal = false;
            Kind = "";
        }

        public void Hide() { Build(); root.SetActive(false); }
    }
}
