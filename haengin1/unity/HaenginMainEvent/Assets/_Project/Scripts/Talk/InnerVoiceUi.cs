// 행인1의 메인이벤트 — 속마음(docs/09_M3_버티컬슬라이스_설계.md 2-2): 판 없이 화면 왼쪽 아래 1/3 자리, 종이색 글자 + 먹 테두리(시우 1인칭 내레이션).
// 대사 자막이 떠 있으면 그 위로 비켜 간다.
using TMPro;
using UnityEngine;

namespace Haengin
{
    [DisallowMultipleComponent]
    public sealed class InnerVoiceUi : MonoBehaviour
    {
        public UiFonts Fonts;
        public SubtitleUi Sub;

        public bool Visible => text != null && text.gameObject.activeSelf;
        public string Text => text != null ? text.text : "";
        public TextMeshProUGUI Label => text;

        TextMeshProUGUI text;
        RectTransform rt;

        void Build()
        {
            if (text != null) return;
            UiKit.Canvas(gameObject, 48);      // 가계부 표(46)·도감(47) 위
            text = UiKit.Text("속마음", transform, Fonts != null ? Fonts.B : null, 30f, UiKit.Paper, TextAlignmentOptions.BottomLeft, wrap: true);
            rt = text.rectTransform;
            UiKit.Place(rt, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(150f, 230f), new Vector2(980f, 160f));
            text.lineSpacing = 8f;
            text.fontStyle = FontStyles.Normal;
            // 먹 테두리(재질 인스턴스 — 에셋은 그대로)
            var m = new Material(text.fontSharedMaterial);
            m.EnableKeyword(ShaderUtilities.Keyword_Outline);
            m.SetColor(ShaderUtilities.ID_OutlineColor, UiKit.Ink);
            m.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.22f);
            m.SetFloat(ShaderUtilities.ID_FaceDilate, 0.15f);
            text.fontMaterial = m;
            text.gameObject.SetActive(false);
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
            text.gameObject.SetActive(true);
            LateUpdate();
        }

        public void Hide() { Build(); text.gameObject.SetActive(false); }
    }
}
