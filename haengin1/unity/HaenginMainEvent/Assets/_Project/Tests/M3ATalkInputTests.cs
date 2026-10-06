// 행인1의 메인이벤트 — M3 1단계 테스트 D03 장치 입력 부분(docs/09_M3_버티컬슬라이스_설계.md 6-2)
// 가짜 패드·키보드·마우스(InputTestFixture) → 실제 HInput 의 Talk 맵 → TalkInput → 대사·선택지.
//   패드 × · 키보드 Enter·Space · 마우스 왼쪽 = 다음, 패드 × 길게 0.8초 = 건너뛰기, 십자키 아래 = 고르기, 마우스로 보기 고르기,
//   일시정지(Esc)를 열고 '계속'을 Enter 로 고른 누름이 대사로 새지 않음(그 프레임·다음 프레임 '다음' 없음, 대사 그대로).
// 이름이 M3A… 인 것은 다른 M3 테스트(실제 HInput 을 켬)보다 먼저 돌게(InputTests 처럼 가짜 장치를 쓰는 테스트는 앞에).
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.TestTools;

namespace Haengin.Tests
{
    public sealed class M3ATalkInputTests : InputTestFixture
    {
        const string InputPath = "Assets/_Project/Input/HInput.inputactions";

        [UnityTest, Timeout(300000)]
        public IEnumerator D03_Dialogue_Input_Devices()
        {
            Time.captureDeltaTime = Lab.Dt;
            TimeFx.Reset();
            GameState.SetPaused(false);
            yield return Lab.FreshScene();
            InputActionAsset asset = null;
            UiFonts fonts = null;
#if UNITY_EDITOR
            asset = UnityEditor.AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
            fonts = UnityEditor.AssetDatabase.LoadAssetAtPath<UiFonts>("Assets/_Project/Settings/UiFonts.asset");
#endif
            if (asset == null) { Assert.Ignore("입력 에셋을 에디터에서만 읽을 수 있음"); yield break; }
            asset.Disable();
            var gp = InputSystem.AddDevice<Gamepad>();
            var kb = InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.AddDevice<Mouse>();
            var log = new List<string>();
            try
            {
                var go = new GameObject("대화 입력 시험");
                var ti = go.AddComponent<TalkInput>(); ti.Actions = asset;
                var dr = go.AddComponent<DialogueRunner>();
                var sg = new GameObject("자막"); sg.transform.SetParent(go.transform); var sub = sg.AddComponent<SubtitleUi>(); sub.Fonts = fonts;
                var cg = new GameObject("선택지"); cg.transform.SetParent(go.transform); var ch = cg.AddComponent<ChoiceUi>(); ch.Fonts = fonts; ch.In = ti;
                dr.Sub = sub; dr.Choice = ch; dr.In = ti;
                var flags = new StoryFlags(); dr.Flags = () => flags;
                InputMaps.Use(asset, InputMaps.Talk);
                Assert.AreEqual("Talk", InputMaps.Enabled(asset), "Talk 맵만");
                yield return Lab.Frames(3);

                // ① '다음' 네 가지
                IEnumerator Tap(ButtonControl c) { Press(c); yield return null; bool n = ti.Next; Release(c); yield return null; log.Add($"{c.path} → 다음 {n}"); Assert.IsTrue(n, c.path + " = 다음"); }
                yield return Tap(gp.buttonSouth);
                yield return Tap(kb.enterKey);
                yield return Tap(kb.spaceKey);
                yield return Tap(mouse.leftButton);

                // ② 길게 0.8초 = 건너뛰기(패드 ×, Tab)
                Press(gp.buttonSouth);
                yield return Lab.Seconds(0.85f);
                float hold = ti.HoldTime;
                Release(gp.buttonSouth);
                yield return null;
                log.Add($"패드 × 0.85초 → 누름 {hold:F2}초");
                Assert.GreaterOrEqual(hold, DialogueRunner.HoldToSkip, "길게 누름 시간");
                Assert.AreEqual(0f, ti.HoldTime, "떼면 0");
                Press(kb.tabKey); yield return Lab.Seconds(0.85f); float tab = ti.HoldTime; Release(kb.tabKey); yield return null;
                Assert.GreaterOrEqual(tab, DialogueRunner.HoldToSkip, "Tab 길게");

                // ③ 실제 대사에서 길게 = 건너뛰기
                var lines = new List<DlgLine>
                {
                    new DlgLine { Id = "Y.1", Scene = "Y", Mode = DlgMode.Sub, Who = "엄마", Text = "추워. 바보." },
                    new DlgLine { Id = "Y.2", Scene = "Y", Mode = DlgMode.Choice, Text = "[참는다] / [노려본다]", Note = "참기" },
                    new DlgLine { Id = "Y.3", Scene = "Y", Mode = DlgMode.Sub, Who = "시우", Text = "…" },
                };
                bool running = true;
                dr.StartCoroutine(Run(dr.Play(lines), () => running = false));
                yield return Lab.Frames(10);
                Press(gp.buttonSouth);
                yield return Lab.Seconds(0.9f);
                Release(gp.buttonSouth);
                yield return Lab.Frames(2);
                Assert.IsFalse(running, "길게 눌러 건너뜀");
                Assert.IsTrue(dr.Skipped);
                Assert.AreEqual(1, flags.Endured, "건너뛴 '참기'");
                log.Add($"대사 길게 → 건너뜀 · 참기 {flags.Endured}");

                // ④ 선택지: 십자키 아래 → 1번, 마우스로 2번 보기 위 → 누름
                running = true;
                int picked = -1;
                dr.StartCoroutine(Run(ch.Ask(new[] { "[가]", "[나]", "[다]" }, v => picked = v), () => running = false));
                yield return Lab.Frames(3);
                Press(gp.dpad.down); yield return null; Release(gp.dpad.down); yield return Lab.Frames(2);
                Assert.AreEqual(1, ch.Selected, "십자키 아래 = 1번");
                Press(kb.downArrowKey); yield return null; Release(kb.downArrowKey); yield return Lab.Frames(2);
                Assert.AreEqual(2, ch.Selected, "↓ = 2번");
                var row = ch.Row(0);
                Vector2 sp = RectTransformUtility.WorldToScreenPoint(null, row.TransformPoint(row.rect.center));
                Set(mouse.position, sp + new Vector2(3f, 0f)); yield return null;
                Set(mouse.position, sp); yield return Lab.Frames(2);
                Assert.AreEqual(0, ch.Selected, "마우스를 올린 보기");
                Press(mouse.leftButton); yield return null; Release(mouse.leftButton); yield return Lab.Frames(2);
                Assert.IsFalse(running); Assert.AreEqual(0, picked, "마우스 누름 = 고름");
                log.Add($"선택지: 십자키·↓·마우스 → {picked}");

                // ⑤ 일시정지 '계속'의 Enter 가 대사로 새지 않음
                var ugo = new GameObject("UI 화면");
                var ui = ugo.AddComponent<GameUi>(); ui.Actions = asset; ui.Font = fonts != null ? fonts.Body : null;
                yield return Lab.Frames(3);
                var two = new List<DlgLine> { new DlgLine { Id = "Z.1", Scene = "Z", Mode = DlgMode.Sub, Text = "첫 줄" }, new DlgLine { Id = "Z.2", Scene = "Z", Mode = DlgMode.Sub, Text = "둘째 줄" } };
                running = true;
                dr.StartCoroutine(Run(dr.Play(two), () => running = false));
                yield return Lab.Frames(20);
                Assert.AreEqual("Z.1", dr.Current?.Id);
                Press(kb.escapeKey); yield return null; Release(kb.escapeKey); yield return Lab.Frames(2);
                Assert.IsTrue(GameState.Paused, "Esc(Talk/Pause) = 일시정지");
                Assert.IsTrue(ui.MenuVisible, "메뉴 보임");
                Press(kb.enterKey); yield return null;
                bool n0 = ti.Next, p0 = GameState.Paused;
                yield return null;
                bool n1 = ti.Next;
                Release(kb.enterKey);
                yield return Lab.Frames(3);
                log.Add($"메뉴 '계속' Enter: 그 프레임 다음 {n0} · 다음 프레임 {n1} · 일시정지 {p0}→{GameState.Paused} · 대사 {dr.Current?.Id}");
                Assert.IsFalse(GameState.Paused, "'계속' 으로 풀림");
                Assert.IsFalse(n0 || n1, "메뉴의 Enter 가 대사 '다음'으로 새지 않음");
                Assert.AreEqual("Z.1", dr.Current?.Id, "대사 그대로");
                // 그 뒤의 진짜 Enter 는 넘어감
                Press(kb.enterKey); yield return null; Release(kb.enterKey); yield return Lab.Frames(3);
                Assert.AreEqual("Z.2", dr.Current?.Id, "다음 Enter 는 넘어감");
                dr.RequestSkip();
                yield return Lab.Frames(3);
                Debug.Log("[M3Test] D03 장치 입력: " + string.Join(" | ", log));
            }
            finally
            {
                asset.Disable();
                GameState.SetPaused(false);
                TimeFx.Reset();
                Time.captureDeltaTime = 0f;
            }
        }

        static IEnumerator Run(IEnumerator e, System.Action done) { yield return e; done(); }
    }
}
