// 행인1의 메인이벤트 — 주행 화면(docs/09 2-6): 왼쪽 아래 국물(5-2 는 생크림) 게이지 · 정차하면 "× 건네기" · 9° 넘는 오르막에서 "□ ○ 번갈아" 박자 표시
// M2 HUD 바 모양(먹 테두리·종이색 판). 실제 시간(TimeFx.RealDt) — 고정 프레임 녹화에서도 같게.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Haengin
{
    [DisallowMultipleComponent]
    public sealed class BikeHud : MonoBehaviour
    {
        public UiFonts Fonts;
        public BikeMotor Motor;
        public SoupGauge Soup;
        public bool Shown => root != null && root.gameObject.activeSelf;
        public string Prompt => prompt != null && prompt.gameObject.activeSelf ? prompt.text : "";
        public bool PromptOn { get; set; }
        public string PromptText = "× · E  건네기";

        Image root, fill, beatL, beatR, beatPlate;
        TextMeshProUGUI label, value, prompt;
        float flash;

        void Build()
        {
            if (root != null) return;
            UiKit.Canvas(gameObject, 40);
            root = UiKit.Panel("국물 게이지", transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 120f), new Vector2(420f, 64f), new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.94f));
            label = UiKit.Text("이름", root.transform, Fonts != null ? Fonts.Br : null, 30f, UiKit.Paper, TextAlignmentOptions.Left);
            UiKit.Place(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(110f, 50f));
            var bg = UiKit.Panel("바 바탕", root.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(130f, 0f), new Vector2(210f, 22f), new Color(0.25f, 0.22f, 0.22f));
            fill = UiKit.Panel("바", bg.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(210f, 22f), UiKit.Warm);
            value = UiKit.Text("값", root.transform, Fonts != null ? Fonts.B : null, 24f, UiKit.Paper, TextAlignmentOptions.Right);
            UiKit.Place(value.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(70f, 40f));
            prompt = UiKit.Text("건네기", transform, Fonts != null ? Fonts.B : null, 34f, UiKit.Paper, TextAlignmentOptions.Center);
            UiKit.Place(prompt.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 250f), new Vector2(600f, 60f));
            var m = new Material(prompt.fontSharedMaterial); m.EnableKeyword(ShaderUtilities.Keyword_Outline); m.SetColor(ShaderUtilities.ID_OutlineColor, UiKit.Ink); m.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.25f);
            prompt.fontMaterial = m;
            beatPlate = UiKit.Panel("박자", transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 180f), new Vector2(230f, 60f), new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.6f));
            beatL = UiKit.Panel("□", beatPlate.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-50f, 0f), new Vector2(40f, 40f), UiKit.Paper);
            beatR = UiKit.Panel("○", beatPlate.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(50f, 0f), new Vector2(40f, 40f), UiKit.Paper);
            root.gameObject.SetActive(false); prompt.gameObject.SetActive(false); beatPlate.gameObject.SetActive(false);
        }

        void Awake() => Build();

        public void Show(bool on)
        {
            Build();
            root.gameObject.SetActive(on);
            if (!on) { prompt.gameObject.SetActive(false); beatPlate.gameObject.SetActive(false); PromptOn = false; }
        }

        void LateUpdate()
        {
            if (root == null || !root.gameObject.activeSelf || Soup == null) return;
            float rdt = UiKit.RealDt;
            label.text = Soup.Label;
            value.text = Mathf.RoundToInt(Soup.Value).ToString();
            var rt = fill.rectTransform;
            rt.sizeDelta = new Vector2(210f * Mathf.Clamp01(Soup.Value / SoupGauge.Max), 22f);
            fill.color = Soup.Value >= 70f ? UiKit.Warm : Soup.Value >= 40f ? new Color(0.85f, 0.65f, 0.35f) : UiKit.Endure;
            flash = Mathf.Max(0f, flash - rdt);
            prompt.gameObject.SetActive(PromptOn);
            prompt.text = PromptText;
            bool steep = Motor != null && Motor.Mounted && !Motor.NoStand && Motor.SlopeDeg > Motor.StandFrom;
            beatPlate.gameObject.SetActive(steep);
            if (steep)
            {
                bool nextRight = !Motor.LastBeatRight;
                beatL.color = nextRight ? new Color(1f, 1f, 1f, 0.35f) : UiKit.Accent;
                beatR.color = nextRight ? UiKit.Accent : new Color(1f, 1f, 1f, 0.35f);
            }
        }
    }
}
