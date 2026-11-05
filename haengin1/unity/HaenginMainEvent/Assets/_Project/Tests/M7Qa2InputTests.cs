// 행인1의 메인이벤트 — QA 2차 Q40(2026-10-26): 알바 미니게임·드릴 중 Esc = 일시정지(판 시간·라운드 시간 멈춤). 가짜 키보드(InputTestFixture).
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Haengin.Tests
{
    public sealed class M7Qa2InputTests : InputTestFixture
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var w = Object.FindAnyObjectByType<WorldRunner>();
            if (w != null) w.Stop();
            WorldRunner.BotPreset = null;
            GymManager.Gate = null; StageDoor.Gate = null;
            GameState.SetPaused(false);
            yield return M3Lab.TearDown();
        }

        [TearDown] public void Down() { GameClock.SetDate(GameClock.DefaultYear, 3, 3, 5 * 60 + 31); }

        IEnumerator Esc(Keyboard kb) { Press(kb.escapeKey); yield return null; yield return null; Release(kb.escapeKey); yield return null; }

        [UnityTest, Timeout(900000)]
        public IEnumerator Q40_Esc_Pauses_Job_And_Drill()
        {
            var kb = InputSystem.AddDevice<Keyboard>();
            kb.MakeCurrent();
            WorldRunner w = null;
            yield return M7Qa2Tests.Open(x => w = x);
            yield return M7Qa2Tests.Start(w, M7Qa2Tests.Mid(w.Def, 310, 16 * 60 + 40, "Gukbap", m => m.G.Fatigue = 0));
            // ① 알바 미니게임(사람 — 봇 실력 0): Esc → 일시정지, 문제 글·시간 막대가 멈춤 → 풀면 이어짐
            var job = w.Job;
            Assert.NotNull(job);
            bool jobDone = false;
            w.StartCoroutine(Run(job.Play("dish", 0f, 7, v => { }), () => jobDone = true));
            for (int i = 0; i < 60 && !job.Shown; i++) yield return null;
            yield return Lab.Frames(30);
            yield return Esc(kb);
            Assert.IsTrue(GameState.Paused, "미니게임 중 Esc 가 일시정지가 아님");
            Assert.AreEqual(1, job.Pauses);
            int p0 = job.Prompts; string t0 = job.PromptText;
            yield return Lab.Frames(240);          // 4초(설거지 막대 한 바퀴 1.15초 — 안 멈추면 문제가 서너 개 지나감)
            Assert.IsTrue(GameState.Paused);
            Assert.AreEqual(p0, job.Prompts, "일시정지 중에 판이 흘러감(문제 수)");
            Assert.IsTrue(job.Frozen);
            Assert.IsFalse(job.GetComponent<Canvas>().enabled, "일시정지 중 알바 판이 일시정지 메뉴와 겹침(2026-10-29 — 판은 안 그려야)");
            GameState.SetPaused(false);
            yield return Lab.Frames(120);
            Assert.Greater(job.Prompts, p0, "풀었는데 판이 안 이어짐");
            for (int i = 0; i < 60 * 40 && !jobDone; i++) yield return null;
            Assert.IsTrue(jobDone, "미니게임이 안 끝남");
            Debug.Log($"[Q] Q40 ① 미니게임: Esc 일시정지 {job.Pauses}번 · 멈춘 동안 문제 {p0} 그대로 · 풀고 이어짐 · 끝 {job.LastScore:F2}");
            // ② 드릴(사람): Esc → 일시정지, 라운드 시간 멈춤
            yield return M7Qa2Tests.Start(w, M7Qa2Tests.Mid(w.Def, 310, 16 * 60 + 45, SceneLoader.Gym, m => m.G.Fatigue = 0));
            GymManager gm = null;
            for (int i = 0; i < 300 && gm == null; i++) { gm = GymManager.Instance; yield return null; }
            Assert.NotNull(gm);
            // 시험 틀: InputTestFixture 가 입력 시스템을 갈아 끼우기 전에 켜진 맵(앞 시험들)은 새 키보드를 못 봄 → 다 끄고 드릴이 Combat 맵을 새로 켜게
            w.Actions.Disable();
            Assert.IsTrue(gm.StartDrill("D01"), "D01 시작");
            for (int i = 0; i < 600 && gm.Runner.State != DrillRunner.Phase.Round; i++) yield return null;
            yield return Lab.Frames(30);
            string pre = $"드릴 {gm.Runner.State} · Modal {GameState.Modal} · 잠금 {GameState.InputLocked} · 일시정지 {GameState.Paused} · 키보드 현재 {Keyboard.current == kb}";
            yield return Esc(kb);
            Debug.Log($"[Q] Q40 ② Esc 전: {pre} → 일시정지 {GameState.Paused}");
            Assert.IsTrue(GameState.Paused, "드릴 중 Esc 가 일시정지가 아님 — " + pre);
            double T0 = gm.Runner.T;
            yield return Lab.Frames(180);
            Assert.AreEqual(T0, gm.Runner.T, 1e-6, "일시정지 중에 라운드 시간이 흐름");
            GameState.SetPaused(false);
            yield return Lab.Frames(60);
            Assert.Greater(gm.Runner.T, T0, "풀었는데 라운드가 안 이어짐");
            gm.Runner.Abort();
            for (int i = 0; i < 300 && gm.Busy; i++) { gm.CloseCard(); yield return null; }
            Debug.Log($"[Q] Q40 ② 드릴 D01: Esc 일시정지 · 멈춘 동안 라운드 {T0:F2}초 그대로 · 풀고 이어짐");
        }

        // ───────────────────────── Q62b G1-b(2026-11-05): 시비 반복 싸움 중 Tab — 휴대폰이 안 열림(예전엔 Tab 을 키보드에서 바로 읽어 열렸고, 휴대폰은 내 입력만 막아 적은 계속 쳤음)
        [UnityTest, Timeout(600000)]
        public IEnumerator Q62b_Tab_Opens_No_Phone_In_Street_Fight()
        {
            var kb = InputSystem.AddDevice<Keyboard>();
            kb.MakeCurrent();
            WorldRunner w = null;
            yield return M7Qa2Tests.Open(x => w = x);
            var pk = w.Def.Balance.Point("parking");
            yield return M7Qa2Tests.Start(w, M7Qa2Tests.Mid(w.Def, 311, 17 * 60, SceneLoader.Zone1, m => { m.G.Fatigue = 0; m.Unlock("street_repeat"); }, pk.Pos + new Vector3(0f, 0.5f, 25f)));
            yield return Lab.Frames(30);
            // 시험 전제: 자유 이동에선 Tab = 휴대폰
            Press(kb.tabKey); yield return null; yield return null; Release(kb.tabKey); yield return Lab.Frames(5);
            bool freeOpen = w.Phone.Shown;
            w.Phone.Close();
            yield return Lab.Frames(10);
            Assert.IsTrue(freeOpen, "자유 이동 중 Tab 으로 휴대폰이 안 열림(시험 전제 — 가짜 키보드)");
            var enc = StoryRunner.ParkingEncounter();
            Assert.NotNull(enc, "주차장 시비 무대");
            var pc = Object.FindAnyObjectByType<PlayerCombat>();
            enc.Trigger();
            float t0 = Time.realtimeSinceStartup;
            while (!pc.Active && Time.realtimeSinceStartup - t0 < 5f) yield return null;
            Assert.IsTrue(pc.Active, "시비 전투가 안 켜짐(시험 전제)");
            Press(kb.tabKey); yield return null; yield return null; Release(kb.tabKey); yield return Lab.Frames(5);
            bool fightOpen = w.Phone.Shown;
            Debug.Log($"[Q] Q62b Tab: 자유 이동 휴대폰 {freeOpen} · 싸움 중 휴대폰 {fightOpen} · 시비 {enc.State} · Free {w.Free}");
            if (fightOpen) w.Phone.Close();
            Assert.IsFalse(fightOpen, "시비 싸움 중 Tab 으로 휴대폰이 열림");
        }

        static IEnumerator Run(IEnumerator e, System.Action done) { yield return e; done(); }
    }
}
