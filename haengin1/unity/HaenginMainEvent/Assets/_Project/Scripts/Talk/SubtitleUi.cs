// 행인1의 메인이벤트 — 대사 자막(docs/09_M3_버티컬슬라이스_설계.md 2-2): 화면 아래 가운데 종이색 반투명 판, 화자 이름(작게, 먹색) + 대사 최대 2줄 × 32자.
// 아래 가운데 고정(08 11-4 '자막이 겹치고 말한 사람이 화면 밖이면 안 보임'을 피함). 글꼴 KR_Bold_SDF. 자막 크기 3단(설정 haengin.talk.size 0·1·2).
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

        public bool Visible => plate != null && plate.gameObject.activeSelf;
        public string Who => who != null ? who.text : "";
        public string Text => text != null ? text.text : "";
        /// 판 높이(1080 기준) — 속마음이 겹치지 않게 비켜 갈 때
        public float Height => plate != null && Visible ? ((RectTransform)plate.transform).rect.height : 0f;
        public TextMeshProUGUI Label => text;

        Image plate;
        TextMeshProUGUI who, text;

        public static float Scale => Scales[Mathf.Clamp(PlayerPrefs.GetInt(SizeKey, 1), 0, 2)];

        void Build()
        {
            if (plate != null) return;
            UiKit.Canvas(gameObject, 49);      // 가계부 표(46)·도감(47) 위
            plate = UiKit.Panel("자막 판", transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 56f), new Vector2(1000f, 120f), new Color(UiKit.Paper.r, UiKit.Paper.g, UiKit.Paper.b, 0.86f));
            UiKit.Fit(plate, 36, 36, 14, 18);
            var lay = plate.GetComponent<VerticalLayoutGroup>();
            lay.spacing = 4f;
            lay.childAlignment = TextAnchor.UpperCenter;
            who = UiKit.Text("화자", plate.transform, Fonts != null ? Fonts.B : null, 21f, new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.72f), TextAlignmentOptions.Center);
            text = UiKit.Text("대사", plate.transform, Fonts != null ? Fonts.B : null, 31f, UiKit.Ink, TextAlignmentOptions.Center);
            text.lineSpacing = 6f;
            plate.gameObject.SetActive(false);
        }

        void Awake() => Build();

        public void Show(string speaker, string line)
        {
            Build();
            float k = Scale;
            who.fontSize = 21f * k; text.fontSize = 31f * k;
            who.text = speaker ?? "";
            who.gameObject.SetActive(!string.IsNullOrEmpty(speaker));
            text.text = line ?? "";
            plate.gameObject.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)plate.transform);
        }

        public void Hide() { Build(); plate.gameObject.SetActive(false); }
    }
}
