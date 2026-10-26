// 행인1의 메인이벤트 — 양갈래 땋기 1화 최소판(docs/09 2-8, 12단계를 1화용으로 줄임 — 2026-10-25 대표 "1화만은 제대로")
// 세 가닥(왼·가운데·오른) 그림 + 박자 표시: □(J·마우스 왼쪽) = 왼쪽 가닥을 가운데로, ○(K·마우스 오른쪽) = 오른쪽 가닥을 가운데로 — 번갈아 8번.
// 박자 0.75초, 판정 창 ±0.22초. 결과는 원고 그대로 고정: 1회차 '양파'(9분) · 2회차 '다시'(7분) · 3회차 '합격' — 맞힌 박자 수는 점수로만 보인다(D22 1회차 양파·3회차 합격 고정).
// 대사의 이벤트 줄 "braidgame:N" 이 부른다(StoryRunner.ApplyCo). 봇·건너뛰기면 바로 끝.
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Haengin
{
    [DisallowMultipleComponent]
    public sealed class BraidGame : MonoBehaviour
    {
        public UiFonts Fonts;
        public const int Beats = 8;
        public const float Interval = 0.75f, Window = 0.22f;
        const float BarW = 520f;
        public static readonly string[] Results = { "양파", "다시", "합격" };
        public bool Shown => root != null && root.gameObject.activeSelf;
        public int LastHits { get; private set; }
        public string LastResult { get; private set; } = "";
        public int Plays { get; private set; }

        Image root, marker, strandL, strandC, strandR;
        TextMeshProUGUI title, prompt, score, stamp;

        void Build()
        {
            if (root != null) return;
            UiKit.Canvas(gameObject, 62);
            root = UiKit.Panel("땋기", transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-330f, 290f), new Vector2(600f, 470f), UiKit.Paper);   // 오른쪽 — 왼쪽엔 하늘(3D)이 보이게
            // QA Q14: 제목(44, 가운데)이 오른쪽 위 '박자 0/8' 과 붙어 '1번째박자 0/8' 로 읽히던 것 → 제목을 줄이고 왼쪽으로(점수 칸 비움)
            title = UiKit.Text("제목", root.transform, Fonts != null ? Fonts.Br : null, 36f, UiKit.Ink, TextAlignmentOptions.Left);
            UiKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -24f), new Vector2(360f, 60f));
            title.enableAutoSizing = true; title.fontSizeMin = 24f; title.fontSizeMax = 36f;
            strandL = UiKit.Panel("왼 가닥", root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-90f, 10f), new Vector2(34f, 240f), new Color(0.20f, 0.16f, 0.15f));
            strandC = UiKit.Panel("가운데 가닥", root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(34f, 240f), new Color(0.30f, 0.24f, 0.22f));
            strandR = UiKit.Panel("오른 가닥", root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(90f, 10f), new Vector2(34f, 240f), new Color(0.24f, 0.19f, 0.18f));
            var bar = UiKit.Panel("박자 줄", root.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 96f), new Vector2(BarW, 8f), new Color(0.75f, 0.71f, 0.66f));
            UiKit.Panel("판정 칸", root.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 84f), new Vector2(BarW * Window * 2f / Interval, 32f), new Color(UiKit.Accent.r, UiKit.Accent.g, UiKit.Accent.b, 0.35f));
            marker = UiKit.Panel("박자", root.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 84f), new Vector2(10f, 32f), UiKit.Ink);
            prompt = UiKit.Text("안내", root.transform, Fonts != null ? Fonts.B : null, 30f, UiKit.Ink, TextAlignmentOptions.Center);
            UiKit.Place(prompt.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(600f, 44f));
            score = UiKit.Text("점수", root.transform, Fonts != null ? Fonts.B : null, 26f, UiKit.Grey, TextAlignmentOptions.Right);
            UiKit.Place(score.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -30f), new Vector2(240f, 40f));
            stamp = UiKit.Text("결과", root.transform, Fonts != null ? Fonts.Br : null, 120f, UiKit.Accent, TextAlignmentOptions.Center);
            UiKit.Place(stamp.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(600f, 180f));
            root.gameObject.SetActive(false);
        }

        void Awake() => Build();

        static bool Pressed(bool right)
        {
            var k = Keyboard.current; var g = Gamepad.current; var m = Mouse.current;
            if (right) return (k != null && k.kKey.wasPressedThisFrame) || (g != null && g.buttonEast.wasPressedThisFrame) || (m != null && m.rightButton.wasPressedThisFrame);
            return (k != null && k.jKey.wasPressedThisFrame) || (g != null && g.buttonWest.wasPressedThisFrame) || (m != null && m.leftButton.wasPressedThisFrame);
        }

        /// 한 번 땋기(attempt 1~3). bot = 박자 가운데서 자동으로 누름, skip() = 바로 끝
        public IEnumerator Play(int attempt, bool bot, System.Func<bool> skip)
        {
            Build();
            Plays++;
            attempt = Mathf.Clamp(attempt, 1, 3);
            root.gameObject.SetActive(true);
            title.text = $"양갈래 땋기 — {attempt}번째";
            stamp.text = "";
            int hits = 0;
            float t = -0.6f;
            int beat = 0;
            bool pressedThisBeat = false;
            while (beat < Beats)
            {
                if (skip != null && skip()) break;
                float dt = UiKit.RealDt;
                if (!GameState.Paused) t += dt;
                bool right = beat % 2 == 1;
                prompt.text = right ? "○ · K · 마우스 오른쪽 — 오른쪽 가닥을 가운데로" : "□ · J · 마우스 왼쪽 — 왼쪽 가닥을 가운데로";
                float phase = t - beat * Interval;          // 0 = 박자 가운데
                ((RectTransform)marker.transform).anchoredPosition = new Vector2(Mathf.Clamp(phase / Interval * BarW, -BarW / 2f, BarW / 2f), 84f);
                bool inWin = Mathf.Abs(phase) <= Window;
                bool press = bot ? (Mathf.Abs(phase) < dt * 0.6f) : (Pressed(right) || Pressed(!right));
                if (press && !pressedThisBeat)
                {
                    pressedThisBeat = true;
                    bool good = inWin && (bot || Pressed(right));
                    if (good) { hits++; Swap(right); }
                }
                if (phase > Window || (pressedThisBeat && phase > 0f))
                {
                    if (phase > Window) { beat++; pressedThisBeat = false; }
                }
                score.text = $"박자 {hits}/{Beats}";
                yield return null;
            }
            LastHits = hits;
            LastResult = Results[attempt - 1];
            stamp.text = LastResult;
            float w = 0f;
            while (w < (bot ? 0.4f : 1.2f) && !(skip != null && skip())) { w += UiKit.RealDt; yield return null; }
            root.gameObject.SetActive(false);
            Debug.Log($"[M3] 양갈래 땋기 {attempt}번째: 박자 {hits}/{Beats} → {LastResult}");
        }

        void Swap(bool right)
        {
            // 바깥 가닥을 가운데로(그림만): 색을 바꿔 넘어간 느낌
            var a = right ? strandR : strandL;
            var c = a.color; a.color = strandC.color; strandC.color = c;
        }
    }
}
