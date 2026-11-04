// 행인1의 메인이벤트 — 속마음(docs/09 2-2 · B안 docs/16): 대사 자막과 같은 화면 아래 먹 판, 이름띠 없이 왼쪽 Warm 줄(시우 1인칭 내레이션).
// 대사 자막이 떠 있으면 그 위(이름띠 위)로 비켜 간다.
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
        public RectTransform PlateRect => plate != null ? plate.rectTransform : null;
        Image plate;
        public string Text => text != null ? text.text : "";
        public TextMeshProUGUI Label => text;

        TextMeshProUGUI text;
        RectTransform rt;
        const float PadL = 30f;

        void Build()
        {
            if (text != null) return;
            UiKit.Canvas(gameObject, 48);      // 가계부 표(46)·도감(47) 위
            var b = new Vector2(0.5f, 0f);
            plate = UiKit.Plate("속마음 판", transform, b, b, Vector2.zero, new Vector2(SubtitleUi.Width, SubtitleUi.PlateH), UiKit.Edge.Left, UiKit.Warm);
            rt = plate.rectTransform;
            text = UiKit.TextAt("속마음", plate.transform, UiKit.FMed(Fonts), 35f, UiKit.Paper, new Vector2(PadL, -SubtitleUi.PadTop), new Vector2(SubtitleUi.Width - PadL - SubtitleUi.PadX, 100f), TextAlignmentOptions.TopLeft, wrap: true);
            plate.gameObject.SetActive(false);
        }

        void Awake() => Build();

        void LateUpdate()
        {
            if (!Visible) return;
            float lift = Sub != null && Sub.Visible ? Sub.Height + 12f : 0f;
            rt.anchoredPosition = new Vector2(0f, lift);
        }

        public void Show(string line)
        {
            Build();
            float k = SubtitleUi.Scale;
            float cw = ((RectTransform)transform).rect.width;
            float w = cw > 10f ? Mathf.Min(SubtitleUi.Width, cw - 24f) : SubtitleUi.Width;
            text.fontSize = 35f * k;
            text.text = line ?? "";
            float tw = w - PadL - SubtitleUi.PadX;
            float th = Mathf.Max(SubtitleUi.PlateH * k - SubtitleUi.PadTop - SubtitleUi.PadBottom, text.GetPreferredValues(text.text, tw, 0f).y);
            text.rectTransform.sizeDelta = new Vector2(tw, th);
            rt.sizeDelta = new Vector2(w, th + SubtitleUi.PadTop + SubtitleUi.PadBottom);
            plate.gameObject.SetActive(true);
            LateUpdate();
        }

        public void Hide() { Build(); plate.gameObject.SetActive(false); }
    }
}
