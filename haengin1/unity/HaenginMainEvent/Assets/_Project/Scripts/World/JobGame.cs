// 행인1의 메인이벤트 — 알바 미니게임(docs/13 9-1 '숙련·맡기기' · 원고 3·4화 연출 노트): 화면 아래 판 하나로 네 가지.
//   serve(국밥집 서빙) : "N번 상 — 메뉴" → 방향(← 1 · ↑ 2 · → 3 · ↓ 4)으로 갖다 놓기, 빠를수록 점수
//   dish(설거지)       : 막대 위 움직이는 표시가 가운데 칸에 올 때 × — 그릇 10개
//   stock(야간 입고)   : 두 상품의 유통기한 → 빠른 쪽을 앞줄로(← / →), 끝에 폐기 찍기(×) — 선입선출(원고 3화)
//   counter(계산대)    : 값·받은 돈 → 거스름돈 셋 중 맞는 것(← ↑ →)
//   site(노가다)       : 유로폼 나르기 — 흔들리는 무게 중심을 ← → 로 가운데에(8초) + 못질 박자 × 8번(원고 4화 '하체')
// 점수 0~1 → 등급(WorldModel.GradeOf). 입력 = Menu 맵(Navigate·Submit — 십자키·왼스틱·방향키·WASD, × · Enter · Space). 봇 = skill(0~1, 결정적).
// QA 2차 A4(2026-10-26): Esc · Options = 일시정지(판 시간·문제 시간 멈춤, 일시정지 메뉴 그대로) — 예전엔 판이 떠 있는 동안 Esc 가 아무것도 안 했음(Modal).
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Haengin
{
    [DisallowMultipleComponent]
    public sealed class JobGame : MonoBehaviour
    {
        public UiFonts Fonts;
        public InputActionAsset Actions;
        public bool Shown => root != null && root.activeSelf;
        public string Mode { get; private set; } = "";
        public float LastScore { get; private set; }
        public int Prompts { get; private set; }
        public int Hits { get; private set; }
        public string PromptText => prompt != null ? prompt.text : "";

        GameObject root;
        Canvas cv;
        TextMeshProUGUI title, help, prompt, scoreText;
        TextMeshProUGUI[] slots;
        Image timer, barBack, barMark, barZone;
        InputAction nav, submit;
        bool pSub; int lastDir;
        float bot; int seed;

        void Build()
        {
            if (root != null) return;
            UiKit.Canvas(gameObject, 58);
            var bg = UiKit.Panel("알바 판", transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(1180f, 360f), new Color(UiKit.Paper.r, UiKit.Paper.g, UiKit.Paper.b, 0.96f));
            root = bg.gameObject;
            UiKit.Panel("먹 띠", bg.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(1180f, 8f), UiKit.Ink);
            title = UiKit.Text("제목", bg.transform, Fonts != null ? Fonts.Br : null, 40f, UiKit.Ink, TextAlignmentOptions.Left);
            UiKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -18f), new Vector2(700f, 56f));
            scoreText = UiKit.Text("점수", bg.transform, Fonts != null ? Fonts.B : null, 24f, UiKit.Grey, TextAlignmentOptions.Right);
            UiKit.Place(scoreText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -26f), new Vector2(420f, 40f));
            help = UiKit.Text("방법", bg.transform, Fonts != null ? Fonts.B : null, 22f, UiKit.Grey, TextAlignmentOptions.Left, wrap: true);
            UiKit.Place(help.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -74f), new Vector2(1100f, 34f));
            prompt = UiKit.Text("문제", bg.transform, Fonts != null ? Fonts.Br : null, 44f, UiKit.Ink, TextAlignmentOptions.Center);
            UiKit.Place(prompt.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(1100f, 56f));
            slots = new TextMeshProUGUI[4];
            for (int i = 0; i < 4; i++)
            {
                var sp = UiKit.Panel("칸 " + i, bg.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-405f + i * 270f, 70f), new Vector2(250f, 70f), new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.85f));
                slots[i] = UiKit.Text("글", sp.transform, Fonts != null ? Fonts.B : null, 26f, UiKit.Paper, TextAlignmentOptions.Center);
                UiKit.Stretch(slots[i].rectTransform);
            }
            barBack = UiKit.Panel("막대", bg.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 160f), new Vector2(900f, 26f), new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.25f));
            barZone = UiKit.Panel("칸", barBack.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(140f, 26f), new Color(0.35f, 0.62f, 0.38f, 0.85f));
            barMark = UiKit.Panel("표시", barBack.transform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(14f, 44f), UiKit.Ink);
            timer = UiKit.Panel("시간", bg.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 28f), new Vector2(1100f, 10f), UiKit.Accent);
            root.SetActive(false);
        }

        void Awake() => Build();

        void Bind()
        {
            if (Actions == null) return;
            if (nav == null)
            {
                nav = Actions.FindAction("Menu/Navigate", false);
                submit = Actions.FindAction("Menu/Submit", false);
            }
            Actions.FindActionMap("Menu", false)?.Enable();      // 일시정지 메뉴도 Menu 맵을 씀(두 번째 판부터 꺼져 있던 것)
        }

        /// 일시정지 횟수(시험)
        public int Pauses { get; private set; }
        bool wasPaused; int resumeGuard;

        static bool PausePressed()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) return true;
            var gp = Gamepad.current;
            return gp != null && gp.startButton.wasPressedThisFrame;
        }

        /// 이번 프레임 판 시간(QA 2차 A4): Esc·Options → 일시정지(시간 0). 풀린 뒤 두 프레임도 0 — 메뉴를 닫은 Enter·방향이 판 입력으로 새지 않게
        float Dt()
        {
            bool p = GameState.Paused;
            if (!p && !wasPaused && resumeGuard <= 0 && bot <= 0f && PausePressed()) { GameState.SetPaused(true); p = true; Pauses++; }
            if (wasPaused && !p) resumeGuard = 2;
            wasPaused = p;
            // 2026-10-29: 일시정지 메뉴가 알바 판 위에 겹쳐 글자가 섞이던 것 — 멈춘 동안 판(이 캔버스)만 안 그림(판 상태 Shown 은 그대로)
            if (cv == null) cv = GetComponent<Canvas>();
            if (cv != null && cv.enabled == p) cv.enabled = !p;
            if (p) return 0f;
            if (resumeGuard > 0)
            {
                resumeGuard--;
                pSub = submit != null && submit.IsPressed();
                lastDir = 99;
                return 0f;
            }
            return UiKit.RealDt;
        }

        /// 시험: 지금 판이 일시정지로 멈춰 있나
        public bool Frozen => Shown && GameState.Paused;

        /// 이번 프레임에 누른 방향(0 없음 · 1 ← · 2 ↑ · 3 → · 4 ↓)
        int DirPressed()
        {
            Vector2 v = nav != null ? nav.ReadValue<Vector2>() : Vector2.zero;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) v.x = -1; if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) v.x = 1;
                if (kb.upArrowKey.isPressed || kb.wKey.isPressed) v.y = 1; if (kb.downArrowKey.isPressed || kb.sKey.isPressed) v.y = -1;
            }
            int d = v.x < -0.5f ? 1 : v.y > 0.5f ? 2 : v.x > 0.5f ? 3 : v.y < -0.5f ? 4 : 0;
            int edge = d != 0 && d != lastDir ? d : 0;
            lastDir = d;
            return edge;
        }

        bool SubmitPressed()
        {
            bool s = submit != null && GameUi.Edge(submit, ref pSub);
            var kb = Keyboard.current;
            if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame)) s = true;
            var gp = Gamepad.current;
            if (gp != null && gp.buttonSouth.wasPressedThisFrame) s = true;
            return s;
        }

        float Roll(int i) => WorldModel.Hash01(seed, i, Mode);

        /// 미니게임 한 판. skill > 0 이면 봇(그 실력으로 결정적), done(점수 0~1)
        public IEnumerator Play(string mode, float skill, int seedKey, Action<float> done)
        {
            Build(); Bind();
            Mode = mode; bot = skill; seed = seedKey;
            Prompts = 0; Hits = 0;
            wasPaused = GameState.Paused; resumeGuard = 0;
            float total = 0f; int count = 0;
            // QA Q09: 판을 켜는 첫 프레임에 지난 판의 '끝 — 38점'·제목이 그대로 보이던 것 — 켜기 전에 비움
            title.text = ""; help.text = ""; prompt.text = ""; scoreText.text = "";
            foreach (var sl in slots) sl.text = "";
            if (timer != null) Timer(1f);
            root.SetActive(true);
            lastDir = 0; pSub = submit != null && submit.IsPressed();
            barBack.gameObject.SetActive(false);
            foreach (var s in slots) s.transform.parent.gameObject.SetActive(true);
            yield return null;
            switch (mode)
            {
                case "serve":
                    title.text = "국밥집 저녁 — 서빙";
                    help.text = "주문이 뜨면 그 상으로: ← 1번 상 · ↑ 2번 상 · → 3번 상 · ↓ 4번 상. 빨리 갖다 놓을수록 좋다.";
                    SetSlots("← 1번 상", "↑ 2번 상", "→ 3번 상", "↓ 4번 상");
                    string[] menu = { "순대국 둘", "따로국밥", "섞어 하나", "공깃밥 추가", "깍두기 리필", "순대국 특", "수육 小", "물 한 병" };
                    for (int i = 0; i < 10; i++)
                    {
                        int want = 1 + Mathf.FloorToInt(Roll(i) * 4f) % 4;
                        prompt.text = $"{want}번 상 — {menu[Mathf.FloorToInt(Roll(i + 50) * menu.Length) % menu.Length]}";
                        float sc = 0f;
                        yield return Choose(2.2f, i, want, v => sc = v);
                        total += sc; count++;
                        Score(total, count);
                    }
                    break;
                case "dish":
                    title.text = "국밥집 저녁 — 설거지";
                    help.text = "움직이는 표시가 초록 칸에 올 때 × · Enter · Space. 뚝배기 열 개.";
                    yield return Bar(10, 1.15f, (sc) => { total += sc; count++; Score(total, count); });
                    break;
                case "stock":
                    title.text = "편의점 야간 입고 — 선입선출";
                    help.text = "유통기한이 빠른 상품을 앞줄로: ← 왼쪽 상품 · → 오른쪽 상품. 끝에 폐기 상품을 × 로 찍는다.";
                    string[] items = { "삼각김밥 참치마요", "바나나우유", "샌드위치", "컵라면", "삼각김밥 전주비빔", "초코우유", "도시락", "두유" };
                    for (int i = 0; i < 9; i++)
                    {
                        int a = 20 + Mathf.FloorToInt(Roll(i) * 10f), b = a + 1 + Mathf.FloorToInt(Roll(i + 9) * 5f);
                        bool leftEarly = Roll(i + 30) < 0.5f;
                        string it = items[Mathf.FloorToInt(Roll(i + 70) * items.Length) % items.Length];
                        string l = $"{(leftEarly ? a : b) % 24:00}:30", r = $"{(leftEarly ? b : a) % 24:00}:30";
                        prompt.text = $"{it}   {l}  vs  {r}";
                        SetSlots($"← {l}", "", $"→ {r}", "");
                        float sc = 0f;
                        yield return Choose(2.6f, i, leftEarly ? 1 : 3, v => sc = v);
                        total += sc; count++;
                        Score(total, count);
                    }
                    SetSlots("", "", "", "");
                    prompt.text = "폐기 — 23:30 라벨을 찍는다(판매 불가)";
                    yield return Bar(1, 1.0f, (sc) => { total += sc; count++; Score(total, count); });
                    break;
                case "counter":
                    title.text = "편의점 계산대 — 거스름돈";
                    help.text = "값과 받은 돈을 보고 거스름돈을 고른다: ← · ↑ · →";
                    for (int i = 0; i < 8; i++)
                    {
                        int price = 1200 + 100 * Mathf.FloorToInt(Roll(i) * 80f);
                        int paid = price <= 5000 ? 5000 : price <= 10000 ? 10000 : 50000;
                        int right = paid - price;
                        int slot = 1 + Mathf.FloorToInt(Roll(i + 20) * 3f) % 3;
                        int[] opts = new int[3];
                        for (int k = 0; k < 3; k++)
                        {
                            int w = right + (k + 1 - slot) * 1000;
                            opts[k] = k + 1 == slot ? right : w > 0 ? w : right + 3000 + k * 500;
                        }
                        prompt.text = $"{Ledger.Won(price)}원 · 받은 돈 {Ledger.Won(paid)}원";
                        SetSlots($"← {Ledger.Won(opts[0])}", $"↑ {Ledger.Won(opts[1])}", $"→ {Ledger.Won(opts[2])}", "");
                        float sc = 0f;
                        yield return Choose(3.0f, i, slot == 1 ? 1 : slot == 2 ? 2 : 3, v => sc = v);
                        total += sc; count++;
                        Score(total, count);
                    }
                    break;
                case "site":
                    title.text = "노가다 — 유로폼 나르기 · 못질";
                    help.text = "흔들리는 무게 중심을 ← → 로 가운데 칸에(하체로 버틴다). 다음은 박자에 맞춰 × 로 못질.";
                    SetSlots("", "", "", "");
                    prompt.text = "유로폼 나르기";
                    float bal = 0f;
                    yield return Balance(8f, v => bal = v);
                    total += bal * 2f; count += 2;
                    Score(total, count);
                    prompt.text = "못질";
                    yield return Bar(8, 0.75f, (sc) => { total += sc; count++; Score(total, count); });
                    break;
                default:
                    title.text = "일"; help.text = ""; prompt.text = "…";
                    yield return new WaitForSecondsRealtime(0.5f);
                    total = 0.6f; count = 1;
                    break;
            }
            LastScore = count > 0 ? total / count : 0f;
            prompt.text = $"끝 — {Mathf.RoundToInt(LastScore * 100f)}점";
            float t = 0f;
            while (t < (bot > 0f ? 0.4f : 0.9f)) { t += Dt(); yield return null; }
            root.SetActive(false); if (cv != null) cv.enabled = true;
            done?.Invoke(LastScore);
        }

        void SetSlots(string a, string b, string c, string d)
        {
            var v = new[] { a, b, c, d };
            for (int i = 0; i < 4; i++) { slots[i].text = v[i]; slots[i].transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(v[i])); }
        }

        void Score(float total, int count) => scoreText.text = $"{count}개 · {Mathf.RoundToInt(100f * total / Mathf.Max(1, count))}점";

        void Timer(float frac) { timer.rectTransform.sizeDelta = new Vector2(1100f * Mathf.Clamp01(frac), 10f); }

        /// 방향 고르기 한 문제. 맞으면 1 − 0.5 × 걸린 시간 비율, 틀리거나 시간 넘김 0
        IEnumerator Choose(float window, int i, int want, Action<float> done)
        {
            Prompts++;
            float t = 0f; int got = 0;
            bool botRight = Roll(i + 200) < bot;
            float botAt = (1f - bot) * window * 0.55f + 0.25f;
            int botDir = botRight ? want : (want % 4) + 1;
            yield return null;
            while (t < window)
            {
                float dt = Dt();
                if (dt <= 0f) { yield return null; continue; }
                t += dt;
                Timer(1f - t / window);
                if (bot > 0f) { if (t >= botAt) { got = botDir; break; } }
                else { int d = DirPressed(); if (d != 0) { got = d; break; } }
                yield return null;
            }
            bool ok = got == want;
            if (ok) Hits++;
            prompt.text = ok ? "좋아." : got == 0 ? "늦었다." : "아니, 거기 아냐.";
            float w = 0f; while (w < 0.25f) { w += Dt(); yield return null; }
            done(ok ? 1f - 0.5f * Mathf.Clamp01(t / window) : 0f);
        }

        /// 막대 박자: 표시가 왼쪽→오른쪽(period 초), 가운데 칸(±0.08)에서 누르면 1, 가까우면 0.5
        IEnumerator Bar(int n, float period, Action<float> each)
        {
            barBack.gameObject.SetActive(true);
            for (int i = 0; i < n; i++)
            {
                Prompts++;
                float t = 0f; float hit = -1f;
                float botAt = period * (0.5f + (Roll(i + 400) - 0.5f) * (1f - bot) * 0.6f);
                yield return null;
                while (t < period)
                {
                    float dt = Dt();
                    if (dt <= 0f) { yield return null; continue; }
                    t += dt;
                    float x = t / period;
                    barMark.rectTransform.anchoredPosition = new Vector2(900f * x, 0f);
                    Timer(1f - x);
                    if (bot > 0f) { if (t >= botAt) { hit = x; break; } }
                    else if (SubmitPressed()) { hit = x; break; }
                    yield return null;
                }
                float off = hit < 0f ? 1f : Mathf.Abs(hit - 0.5f);
                float sc = off <= 0.08f ? 1f : off <= 0.16f ? 0.5f : 0f;
                if (sc >= 1f) Hits++;
                each(sc);
            }
            barBack.gameObject.SetActive(false);
        }

        /// 균형: 무게 중심(−1~1)이 흔들림, ←→ 로 밀어 가운데(±0.25)에 있던 시간 비율
        IEnumerator Balance(float secs, Action<float> done)
        {
            barBack.gameObject.SetActive(true);
            Prompts++;
            float t = 0f, x = 0f, vel = 0f, inside = 0f;
            yield return null;
            while (t < secs)
            {
                float dt = Dt();
                if (dt <= 0f) { yield return null; continue; }
                t += dt;
                float drift = Mathf.Sin(t * 1.7f + seed % 7) * 1.1f + Mathf.Sin(t * 3.1f) * 0.6f;
                float push = 0f;
                if (bot > 0f) push = Mathf.Clamp(-x * 4f - vel * 0.6f, -1f, 1f) * Mathf.Lerp(0.5f, 1.2f, bot);
                else
                {
                    Vector2 v = nav != null ? nav.ReadValue<Vector2>() : Vector2.zero;
                    var kb = Keyboard.current;
                    if (kb != null) { if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) v.x = -1; if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) v.x = 1; }
                    push = v.x;
                }
                vel += (drift * 0.9f + push * 2.4f) * dt;
                vel *= 1f - 1.8f * dt;
                x = Mathf.Clamp(x + vel * dt, -1f, 1f);
                if (Mathf.Abs(x) <= 0.25f) inside += dt;
                barMark.rectTransform.anchoredPosition = new Vector2(450f + 450f * x, 0f);
                Timer(1f - t / secs);
                yield return null;
            }
            barBack.gameObject.SetActive(false);
            float r = inside / secs;
            if (r >= 0.75f) Hits++;
            done(Mathf.Clamp01(r * 1.15f));
        }

        public void Hide() { Build(); root.SetActive(false); if (cv != null) cv.enabled = true; }
    }
}
