// 행인1의 메인이벤트 — 속마음(docs/09_M3_버티컬슬라이스_설계.md 2-2): 판 없이 화면 왼쪽 아래 1/3 자리, 종이색 글자 + 먹 테두리(시우 1인칭 내레이션).
// 대사 자막이 떠 있으면 그 위로 비켜 간다.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Haengin
{
    [DisallowMultipleComponent]
    public sealed class InnerVoiceUi : MonoBehaviour
    {
        public UiFonts Fonts;
        public SubtitleUi Sub;

        public bool Visible => plate != null && plate.gameObject.activeSelf;
        Image plate;
        public string Text => text != null ? text.text : "";
        public TextMeshProUGUI Label => text;

        TextMeshProUGUI text;
        RectTransform rt;

        void Build()
        {
            if (text != null) return;
            UiKit.Canvas(gameObject, 48);      // 가계부 표(46)·도감(47) 위
            // 2026-10-31 대표: 판 없이 바닥·벽 위에 나오던 흰 글 → 먹 판 위
            plate = UiKit.Panel("속마음 판", transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(150f, 230f), new Vector2(1000f, 100f), new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.93f));
            rt = plate.rectTransform;
            text = UiKit.Text("속마음", plate.transform, Fonts != null ? Fonts.B : null, 30f, UiKit.Paper, TextAlignmentOptions.TopLeft, wrap: true);
            UiKit.Stretch(text.rectTransform, 0f);
            text.rectTransform.offsetMin = new Vector2(24f, 12f); text.rectTransform.offsetMax = new Vector2(-24f, -12f);
            text.lineSpacing = 8f;
            text.fontStyle = FontStyles.Normal;
            plate.gameObject.SetActive(false);
        }

        void Awake() => Build();

        void LateUpdate()
        {
            if (!Visible) return;
            float lift = Sub != null && Sub.Visible ? Sub.Height + 70f : 0f;
            rt.anchoredPosition = new Vector2(150f, Mathf.Max(230f, 56f + lift));
        }

        public void Show(string line)
        {
            Build();
            text.fontSize = 30f * SubtitleUi.Scale;
            text.text = line ?? "";
            plate.gameObject.SetActive(true);
            // 판 크기 = 글 크기(줄 수에 맞춤)
            float w = rt.sizeDelta.x - 48f;
            var pref = text.GetPreferredValues(text.text, w, 0f);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, Mathf.Max(60f, pref.y + 26f));
            LateUpdate();
        }

        public void Hide() { Build(); plate.gameObject.SetActive(false); }
    }
}
