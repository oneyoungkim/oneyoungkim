// 행인1의 메인이벤트 — 대사 자막(docs/09 2-2 · B안 「중계 화면」 docs/16): 화면 아래 먹 판 1416×156(본문 Noto Medium 35, 줄 간격 50) +
// 판 왼쪽 위 이름띠(주황 바탕 먹 글자 Noto Black 29, 204×48). 아래 가운데 고정(08 11-4 '자막이 겹치고 말한 사람이 화면 밖이면 안 보임'을 피함).
// 자막 크기 3단(설정 haengin.talk.size 0·1·2).
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Haengin
{
    [DisallowMultipleComponent]
    public sealed class SubtitleUi : MonoBehaviour
    {
        public UiFonts Fonts;
        public const string SizeKey = "haengin.talk.size";
        public static readonly float[] Scales = { 0.85f, 1f, 1.2f };
        /// 대사 판 폭·최소 높이(1080 기준) · 이름띠
        public const float Width = 1416f, PlateH = 156f, TagW = 204f, TagH = 48f, PadX = 22f, PadTop = 17f, PadBottom = 22f;

        public bool Visible => plate != null && plate.gameObject.activeSelf;
        public string Who => who != null ? who.text : "";
        public string Text => text != null ? text.text : "";
        /// 판 높이 + 이름띠(1080 기준) — 속마음이 겹치지 않게 비켜 갈 때
        public float Height => plate != null && Visible ? plate.rectTransform.rect.height + (tag.gameObject.activeSelf ? tag.rectTransform.rect.height : 0f) : 0f;
        public TextMeshProUGUI Label => text;

        Image plate, tag;
        TextMeshProUGUI who, text;

        public static float Scale => Scales[Mathf.Clamp(PlayerPrefs.GetInt(SizeKey, 1), 0, 2)];

        void Build()
        {
            if (plate != null) return;
            UiKit.Canvas(gameObject, 49);      // 가계부 표(46)·도감(47) 위
            var b = new Vector2(0.5f, 0f);
            plate = UiKit.Plate("자막 판", transform, b, b, Vector2.zero, new Vector2(Width, PlateH));
            who = UiKit.NameTag("이름띠", plate.transform, Fonts, new Vector2(0f, 1f), Vector2.zero, Vector2.zero, new Vector2(TagW, TagH), 29f, out tag);
            who.name = "화자";
            text = UiKit.TextAt("대사", plate.transform, UiKit.FMed(Fonts), 35f, UiKit.Paper, new Vector2(PadX, -PadTop), new Vector2(Width - PadX * 2f, PlateH - PadTop - PadBottom), TextAlignmentOptions.TopLeft, wrap: true);
            plate.gameObject.SetActive(false);
        }

        void Awake() => Build();

        public void Show(string speaker, string line)
        {
            Build();
            float k = Scale;
            // 좁은 화면(4:3)에선 화면 폭 안으로
            float cw = ((RectTransform)transform).rect.width;
            float w = cw > 10f ? Mathf.Min(Width, cw - 24f) : Width;
            who.fontSize = 29f * k; text.fontSize = 35f * k;
            tag.rectTransform.sizeDelta = new Vector2(TagW * k, TagH * k);
            UiKit.SetTag(tag, who, speaker, TagW * k);
            text.text = line ?? "";
            float tw = w - PadX * 2f;
            float th = Mathf.Max(PlateH * k - PadTop - PadBottom, text.GetPreferredValues(text.text, tw, 0f).y);
            text.rectTransform.sizeDelta = new Vector2(tw, th);
            plate.rectTransform.sizeDelta = new Vector2(w, th + PadTop + PadBottom);
            plate.gameObject.SetActive(true);
        }

        public void Hide() { Build(); plate.gameObject.SetActive(false); }
    }
}
