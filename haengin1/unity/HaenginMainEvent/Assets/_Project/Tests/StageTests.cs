// 행인1의 메인이벤트 — M2 13·14단계 테스트 C18 · C22 · C17② · C19 (docs/08_M2_전투_설계.md 8장·10-5)
//   C18 Encounter_Flow_Y4 : Zone1 주차장 — 걸어 들어가면 시비 → 전투(Combat 맵·CM_Combat 20) → 봇 승리 → 결과 카드 1번 → 탐색(CM_Combat 0) /
//                            일부러 패배 → 패배 화면 '다시' → 적 3명·HP 200·기세 20, 다시 걸어 들어가면 다시 시작
//   C22 Bot_MashOnly_Wins  : 같은 인카운터를 연타 봇(□ 만, 회피·막기·잡기 없음)으로 — 240초 안 승리, 남은 HP ≥ 20%
//   C17② Camera_Framing_Wall : 연습장 북쪽 벽을 등지고 락온 30초(적이 앞쪽 반원을 돎) — 매 프레임 카메라 벽 안 0 · 가림 0, 옆 바꾸기 ≤ 4번
//   C19 Yacha_WinLose      : Zone1 공터 — 심판 형 대화 → 입장 6초 → 스크럼 HP 0 = 항복 승 / 시우 다운 3번 = 심판 스톱 패 / 메뉴 항복 = 패 /
//                            시우를 바깥으로 밀어도 링 중심 거리 ≤ 6.8m(매 프레임)
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Haengin.Tests
{
    public sealed class StageTests
    {
        const float Dt = Lab.Dt;
        PlayerCombat pc;
        PlayerMotor motor;
        CombatMode mode;
        PInput input;
        Encounter enc;
        Yacha yacha;
        Unity.Cinemachine.CinemachineCamera cmCombat;

        IEnumerator OpenZone1()
        {
            Encounter.Suppress = false;
            Time.captureDeltaTime = Dt;
            TimeFx.Reset();
            GameState.SetPaused(false);
            GameState.Modal = GameState.InputLocked = false;
            yield return Lab.UnloadOurs(default);
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ZoneTests.ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            var zone = SceneManager.GetSceneByPath(ZoneTests.ScenePath);
            if (!zone.IsValid() || !zone.isLoaded) { Assert.Ignore("Zone1 장면 없음"); yield break; }
            SceneManager.SetActiveScene(zone);
            yield return null; yield return null;
            pc = Object.FindAnyObjectByType<PlayerCombat>();
            motor = Object.FindAnyObjectByType<PlayerMotor>();
            mode = Object.FindAnyObjectByType<CombatMode>();
            enc = Object.FindAnyObjectByType<Encounter>();
            yacha = Object.FindAnyObjectByType<Yacha>();
            input = Object.FindAnyObjectByType<PInput>();
            cmCombat = Object.FindObjectsByType<Unity.Cinemachine.CinemachineCamera>(FindObjectsSortMode.None).FirstOrDefault(c => c.name == CombatFactory.CombatCamName);
            Assert.NotNull(pc, "PlayerCombat"); Assert.NotNull(mode, "Zone1 CombatMode"); Assert.NotNull(enc, "인카운터 Y4"); Assert.NotNull(yacha, "야차 Y1"); Assert.NotNull(cmCombat, "CM_Combat");
            if (input != null) input.enabled = false;      // 사람 입력 끔(맵 전환은 CombatMode 가 그대로 함)
            foreach (var c in Object.FindObjectsByType<CamInput>(FindObjectsSortMode.None)) c.enabled = false;
            yield return Frames(20);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var b in Object.FindObjectsByType<FightBot>(FindObjectsSortMode.None)) Object.Destroy(b);
            if (input != null && input.Actions != null) input.Actions.Disable();      // 뒤 테스트(가짜 장치 입력)를 막지 않게
            GameState.Modal = GameState.InputLocked = false;
            GameUi.RetryHook = GameUi.SurrenderHook = null;
            TimeFx.Reset();
            GameState.SetPaused(false);
            yield return Lab.FreshScene();
            Time.captureDeltaTime = 0f;
        }

        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        /// 시우를 주차장 북쪽 진입로 바깥 인도에 세우고 가운데 쪽으로 걷게(트리거까지)
        IEnumerator WalkIn(int maxFrames = 600)
        {
            var d = enc.Def;
            for (int i = 0; i < maxFrames && enc.State == Encounter.Phase.Armed; i++)
            {
                var goal = d.ToWorld(new Vector2(1.5f, -1.0f));
                motor.SetMoveInput(HitResolver.Flat(goal - motor.Position).normalized, 1f, false);
                yield return null;
            }
            motor.ClearMoveInput();
        }

        // ───────────────────────── C18
        [UnityTest, Timeout(600000)]
        public IEnumerator C18_Encounter_Flow_Y4()
        {
            yield return OpenZone1();
            var log = new List<string>();
            var hud = StageHud.Instance;
            Assert.NotNull(hud, "StageHud");
            // 인도에서 시작
            var d = enc.Def;
            ((PlayerBody)pc.Me.Body).Place(d.ToWorld(d.RetryLocal) + Vector3.up * 0.1f, d.Yaw + 180f);
            yield return Frames(10);
            Assert.AreEqual(Encounter.Phase.Armed, enc.State, "처음엔 대기");
            Assert.IsFalse(mode.Active, "탐색");
            int prio0 = cmCombat.Priority.Value;
            yield return WalkIn();
            Assert.AreEqual(Encounter.Phase.Taunt, enc.State, "들어가면 시비");
            log.Add($"시비: 자막 \"{hud.LastSubtitle}\"");
            yield return Frames(70);
            Assert.AreEqual(Encounter.Phase.Fight, enc.State, "0.8초에 전투");
            Assert.IsTrue(mode.Active, "CombatMode 켜짐");
            bool combatMap = input == null || input.CombatInput;
            int prioFight = cmCombat.Priority.Value;
            log.Add($"전투: Combat 맵 {combatMap} · CM_Combat 우선순위 {prio0} → {prioFight} · 배너 \"{hud.LastBanner}\" · 경계 벽 켜짐");
            Assert.IsTrue(combatMap, "Explore → Combat 맵");
            Assert.AreEqual(20, prioFight, "CM_Combat 우선순위 20");
            Assert.AreEqual("시비 붙음!", hud.LastBanner);

            // 봇(막기·회피 쓰는 녹화용 봇) + 시우 무적 — 흐름 시험
            pc.Me.DebugInvuln = true;
            var bot = FightBot.On(pc, false);
            float t = 0f;
            int results0 = hud.ResultCount;
            while (enc.State == Encounter.Phase.Fight && t < 180f) { yield return null; t += Dt; }
            log.Add($"봇 승리: {t:F1}초 · 탈락 {enc.Eliminated}/3 · 누름 {bot.Presses} · 기세 액션 {bot.HeatActions}");
            Assert.AreEqual(Encounter.Phase.Finish, enc.State, "셋 다 탈락 → 마무리");
            Assert.AreEqual(3, enc.Eliminated, "탈락 3");
            Assert.AreEqual("정리.", hud.LastBigWord, "큰 붓 글자 「정리.」");
            while (enc.State != Encounter.Phase.Cleared && t < 200f) { yield return null; t += Dt; }
            yield return Frames(30);
            Object.Destroy(bot);
            pc.Me.DebugInvuln = false;
            log.Add($"결과: 카드 {hud.ResultCount - results0}번 \"{hud.LastResult}\" · 탐색 {(!mode.Active)} · Explore 맵 {(input == null || !input.CombatInput)} · CM_Combat {cmCombat.Priority.Value} · 전투 다시 메뉴 {GameUi.RetryHook != null}");
            Assert.AreEqual(1, hud.ResultCount - results0, "결과 카드 1번");
            Assert.IsFalse(mode.Active, "탐색 복귀");
            Assert.IsTrue(input == null || !input.CombatInput, "Combat → Explore 맵");
            Assert.AreEqual(0, cmCombat.Priority.Value, "CM_Combat 우선순위 0");
            Assert.IsNotNull(GameUi.RetryHook, "이긴 뒤 근처: 일시정지 메뉴 '전투 다시'");

            // 전투 다시(메뉴) → 일부러 패배 → 패배 화면 '다시'
            GameUi.RetryHook();
            yield return Frames(70);
            Assert.AreEqual(Encounter.Phase.Fight, enc.State, "전투 다시 → 다시 시작");
            Assert.AreEqual(3, enc.Brains.Count(b => b.isActiveAndEnabled && !b.Eliminated), "적 3명 다시");
            var killer = enc.Brains[0].Me;
            var kill = MoveLib.EnemyJab(); kill.Damage = 9999; kill.Label = "시험 탈락";
            pc.Me.Receive(killer, kill);
            float w = 0f;
            while (!hud.DefeatShown && w < 6f) { yield return null; w += Dt; }
            Assert.IsTrue(hud.DefeatShown, "패배 화면");
            Assert.IsTrue(GameState.Modal, "패배 화면 동안 일시정지 막음");
            log.Add($"패배: {w:F1}초 뒤 패배 화면");
            yield return Frames(80);
            hud.DefeatChoose(true);
            yield return Frames(10);
            int standing = enc.Brains.Count(b => b.isActiveAndEnabled && !b.Eliminated && b.State == EnemyBrain.S.Idle);
            log.Add($"다시: 적 {standing}명 대기 · 시우 HP {pc.Me.Hp} · 기세 {pc.Heat.Value:F0} · 상태 {enc.State} · 시우 자리 로컬 {d.ToLocal(pc.Me.Position)}");
            Assert.AreEqual(3, standing, "적 3명 처음 자리");
            Assert.AreEqual(200, pc.Me.Hp, "HP 200");
            Assert.That(pc.Heat.Value, Is.EqualTo(20f).Within(0.01f), "기세 20");
            Assert.AreEqual(Encounter.Phase.Armed, enc.State, "다시 대기");
            Assert.IsFalse(mode.Active, "탐색");
            yield return WalkIn();
            yield return Frames(70);
            Assert.AreEqual(Encounter.Phase.Fight, enc.State, "다시 걸어 들어가면 다시 시작");
            Assert.That(pc.Heat.Value, Is.EqualTo(20f).Within(0.5f), "다시 시작 기세 20");
            Debug.Log("[M2Test] C18 인카운터 Y4: " + string.Join(" | ", log));
        }

        // ───────────────────────── C22
        [UnityTest, Timeout(900000)]
        public IEnumerator C22_Bot_MashOnly_Wins()
        {
            yield return OpenZone1();
            var d = enc.Def;
            ((PlayerBody)pc.Me.Body).Place(d.ToWorld(d.RetryLocal) + Vector3.up * 0.1f, d.Yaw + 180f);
            yield return Frames(10);
            yield return WalkIn();
            yield return Frames(70);
            Assert.AreEqual(Encounter.Phase.Fight, enc.State, "전투 시작");
            var bot = FightBot.On(pc, true);
            float t = 0f;
            int hits = 0;
            System.Action<HitEvent> rec = e => { if (e.Victim == pc.Me && e.Landed) hits++; };
            Fighter.AnyHit += rec;
            var log = new List<string>();
            int sec = 0;
            while (enc.State == Encounter.Phase.Fight && t < 240f)
            {
                yield return null;
                t += Dt;
                if ((int)(t / 30f) > sec) { sec = (int)(t / 30f); log.Add($"{sec * 30}초: " + string.Join(", ", enc.Brains.Select(b => $"{b.Def.Label} {b.State} HP {b.Me.Hp}")) + $" · 시우 HP {pc.Me.Hp}"); }
            }
            Fighter.AnyHit -= rec;
            bool win = enc.State == Encounter.Phase.Finish || enc.State == Encounter.Phase.Result || enc.State == Encounter.Phase.Cleared;
            float hpLeft = pc.Me.Hp / (float)pc.Me.MaxHp;
            Debug.Log($"[M2Test] C22 연타 봇: 승리 {win} · {t:F1}초 · 남은 HP {pc.Me.Hp}/{pc.Me.MaxHp}({hpLeft * 100f:F0}%) · 누름 {bot.Presses} · 맞음 {hits} · 탈락 {enc.Eliminated}/3 | " + string.Join(" | ", log));
            Assert.IsTrue(win, "연타만으로 승리");
            Assert.That(t, Is.LessThanOrEqualTo(240f), "240초 안");
            Assert.That(hpLeft, Is.GreaterThanOrEqualTo(0.2f), "남은 HP ≥ 20%");
        }

        // ───────────────────────── C17②
        [UnityTest, Timeout(600000)]
        public IEnumerator C17b_Camera_Framing_Wall()
        {
            Time.captureDeltaTime = Dt;
            TimeFx.Reset();
            GameState.SetPaused(false);
            yield return Lab.UnloadOurs(default);
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ZCombatShots.ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            var scene = SceneManager.GetSceneByPath(ZCombatShots.ScenePath);
            if (!scene.IsValid() || !scene.isLoaded) { Assert.Ignore("CombatLab 없음"); yield break; }
            SceneManager.SetActiveScene(scene);
            yield return null;
            pc = Object.FindAnyObjectByType<PlayerCombat>();
            mode = Object.FindAnyObjectByType<CombatMode>();
            input = Object.FindAnyObjectByType<PInput>();
            if (input != null) input.enabled = false;
            foreach (var c in Object.FindObjectsByType<CamInput>(FindObjectsSortMode.None)) c.enabled = false;
            var dummy = Fighter.All.First(f => !f.IsPlayer);
            yield return Frames(60);
            Assert.IsTrue(mode.Active, "연습장 전투");
            // 북쪽 벽(z 8.0) 을 등지고(시우 z 7.25, 남쪽 보기), 허수아비가 앞쪽 반원(반경 2.2m)을 30초에 3번 왕복
            var me = pc.Me;
            ((PlayerBody)me.Body).Place(new Vector3(0f, 0f, 7.25f), 180f);
            dummy.Body.Place(new Vector3(0f, 0f, 5.0f), 0f);
            pc.Lock.Set(dummy);
            pc.CombatCam.Snap();
            yield return Frames(60);
            var cam = Camera.main;
            var rig = pc.CombatCam;
            int mask = Layers.Mask(Layers.Wall, Layers.Ground);
            int frames = Mathf.RoundToInt(30f / Dt), inside = 0, blocked = 0, swaps0 = rig.SideSwaps, widens0 = rig.SideWidens, stays0 = rig.SideStays;
            string first = null;
            float minDist = 99f;
            int near = 0, excursions = 0, excSign = 0, swapsInExc = 0, backAndForth = 0, cuts0 = rig.SideCuts, swapsSeen = rig.SideSwaps;
            float lastSide = rig.Side, lastExtra = rig.SideExtraNow;
            var evs = new List<string>();
            for (int i = 0; i < frames; i++)
            {
                float u = i * Dt / 10f;                         // 10초에 한 번 왕복
                float ang = Mathf.Sin(u * Mathf.PI * 2f) * 100f; // 남쪽 기준 ±100°
                var p = new Vector3(0f, 0f, 7.25f) + HitResolver.YawDir(180f + ang) * 2.2f;
                p.z = Mathf.Min(p.z, 7.3f);
                var dd = p - dummy.Position; dd.y = 0f;
                dummy.Body.Push(dd);
                dummy.Body.SetYaw(HitResolver.Yaw(HitResolver.Flat(me.Position - dummy.Position)));
                if (me.Position.z < 7.0f) ((PlayerBody)me.Body).Place(new Vector3(0f, 0f, 7.25f), me.Yaw);
                if (pc.Lock.Target != dummy) pc.Lock.Set(dummy);
                yield return null;
                var cp = cam.transform.position;
                var tp = me.Position + Vector3.up * 1.4f;
                bool inS = Physics.CheckSphere(cp, 0.05f, mask, QueryTriggerInteraction.Ignore);
                bool bl = Physics.Linecast(cp, tp, mask, QueryTriggerInteraction.Ignore);
                if (inS) inside++;
                if (bl) blocked++;
                minDist = Mathf.Min(minDist, Vector3.Distance(cp, tp));
                if (Vector3.Distance(cp, tp) < 1f) near++;
                // 대상이 한쪽으로 60° 넘게 건너간 횟수(같은 쪽에서 옆을 두 번 넘기면 '왕복')
                int sg = ang > 60f ? 1 : ang < -60f ? -1 : 0;
                if (sg != 0 && sg != excSign) { excSign = sg; excursions++; swapsInExc = 0; }
                if (rig.SideSwaps != swapsSeen) { swapsSeen = rig.SideSwaps; if (++swapsInExc > 1) backAndForth++; }
                if (rig.Side != lastSide || rig.SideExtraNow != lastExtra) { lastSide = rig.Side; lastExtra = rig.SideExtraNow; evs.Add($"{i * Dt:F1}초 대상 {ang:F0}° → 옆 {rig.Side:+0;-0} +{rig.SideExtraNow:F0}°"); }
                if ((inS || bl) && first == null) first = $"f{i} 카메라 ({cp.x:F2},{cp.y:F2},{cp.z:F2}) 안 {inS} 가림 {bl}";
            }
            int swaps = rig.SideSwaps - swaps0, widens = rig.SideWidens - widens0, stays = rig.SideStays - stays0, cuts = rig.SideCuts - cuts0;
            Debug.Log($"[M2Test] C17② 벽 등지고 락온 30초: 프레임 {frames} · 카메라 벽 안 {inside} · 가림 {blocked} · 대상 좌우 건너감 {excursions}번 · 옆 바꾸기 {swaps}번(컷 {cuts}) · 같은 쪽 왕복 {backAndForth} · 같은 옆 넓히기 {widens}번 · 그대로 {stays}번 · 시우 머리까지 최소 {minDist:F2}m · 1m 안 프레임 {near}({near * 100f / frames:F1}%){(first != null ? " · 처음 " + first : "")} | " + string.Join(", ", evs));
            Assert.AreEqual(0, inside, "카메라 벽 안 0");
            Assert.AreEqual(0, blocked, "가림 0");
            // 11-4: '옆 바꾸기 ≤ 4번' → 대상이 좌우로 건너간 횟수 이하 + 같은 쪽에서 왕복 0 + 머리 1m 안 ≤ 10%
            Assert.That(swaps, Is.LessThanOrEqualTo(excursions), "옆 바꾸기 ≤ 대상이 좌우로 건너간 횟수");
            Assert.AreEqual(0, backAndForth, "같은 쪽에서 옆 왕복 0");
            Assert.That(near, Is.LessThanOrEqualTo(frames / 10), "카메라가 시우 머리 1m 안 ≤ 10%");
        }

        // ───────────────────────── C19
        IEnumerator EnterYacha(bool viaDialog)
        {
            var me = pc.Me;
            ((PlayerBody)me.Body).Place(yacha.Def.Referee + new Vector3(0f, 0.1f, -1.2f), 0f);
            yield return Frames(10);
            if (viaDialog)
            {
                Assert.IsTrue(Interactable.TryUse(me.Position), "심판 형 상호작용(× / E)");
                yield return Frames(3);
                Assert.IsTrue(StageHud.Instance.DialogShown, "대화 판");
                StageHud.Instance.Choose(true);
            }
            else yacha.Enter();
            yield return Frames(3);
            Assert.AreEqual(Yacha.Phase.Entry, yacha.State, "입장");
            Assert.IsTrue(GameState.InputLocked, "입장 동안 조작 막힘");
            float t = 0f;
            while (yacha.State == Yacha.Phase.Entry && t < 10f) { yield return null; t += Dt; }
            Assert.AreEqual(Yacha.Phase.Fight, yacha.State, "6초 뒤 싸움");
            Assert.That(t, Is.InRange(5.8f, 6.3f), "입장 약 6초");
            Assert.IsFalse(GameState.InputLocked, "조작 풀림");
        }

        IEnumerator WaitEnd(float max)
        {
            float t = 0f;
            while (yacha.State == Yacha.Phase.Fight && t < max) { yield return null; t += Dt; }
            while (yacha.State == Yacha.Phase.End && t < max + 10f) { yield return null; t += Dt; }
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator C19_Yacha_WinLose()
        {
            yield return OpenZone1();
            var log = new List<string>();
            var hud = StageHud.Instance;
            var me = pc.Me;

            // ① 대화 → 입장 → 짧게 싸움(시우 무적, 봇) — 스크럼 기술 기록 → 스크럼 HP 0 = 항복 승
            yield return EnterYacha(true);
            Assert.IsTrue(hud.YachaBarShown, "야차 큰 바");
            Assert.IsNotNull(GameUi.SurrenderHook, "야차 중 메뉴 '항복'");
            var scrum = yacha.Foe;
            me.DebugInvuln = true;
            var bot = FightBot.On(pc, false);
            float t = 0f;
            var moves = new Dictionary<string, int>();
            AttackRun seen = null;
            while (t < 25f && yacha.State == Yacha.Phase.Fight)
            {
                yield return null; t += Dt;
                var r = scrum.Me.Run;
                if (r != null && r != seen) { seen = r; moves[r.Move.Label] = moves.TryGetValue(r.Move.Label, out var c) ? c + 1 : 1; }
            }
            bool ph2 = scrum.Phase2;
            log.Add($"스크럼 25초: 기술 {string.Join(", ", moves.Select(kv => kv.Key + " " + kv.Value))} · 태클 {scrum.Tackles}(헛방 {scrum.TackleWhiffs}) · HP {scrum.Me.Hp}/{scrum.Me.MaxHp} · 페이즈 2 {ph2} · 링 밀어냄 {yacha.Pushes} · 시우 링 최대 {yacha.MaxRingDist:F2}m");
            Object.Destroy(bot);
            yield return null;
            var kill = MoveLib.Jab(); kill.Damage = 9999; kill.Label = "시험 KO";
            scrum.Me.Receive(me, kill);
            yield return WaitEnd(20f);
            log.Add($"승: {yacha.Result} · 결과 \"{hud.LastResult}\" · 자막 \"{hud.LastSubtitle}\"");
            Assert.AreEqual(Yacha.Outcome.Win, yacha.Result, "스크럼 HP 0 → 항복 → 승");
            StringAssert.Contains("야차 승", hud.LastResult);
            Assert.AreEqual(Yacha.Phase.Idle, yacha.State, "끝 → 탐색");
            Assert.IsFalse(mode.Active, "탐색 복귀");
            me.DebugInvuln = false;
            yield return Frames(30);

            // ② 시우 다운 3번 → 심판 스톱
            yield return EnterYacha(false);
            scrum.NoAttack = true;
            for (int k = 0; k < 3; k++)
            {
                me.StartDown(Vector3.back, 0.3f);
                float w = 0f;
                while ((me.Down) && w < 6f && yacha.State == Yacha.Phase.Fight) { yield return null; w += Dt; }
                yield return Frames(20);
            }
            yield return WaitEnd(10f);
            log.Add($"다운 3번: {yacha.Result} · 다운 {yacha.Downs} · 결과 \"{hud.LastResult}\"");
            Assert.AreEqual(Yacha.Outcome.LoseDowns, yacha.Result, "다운 3번 → 심판 스톱");
            StringAssert.Contains("야차 패", hud.LastResult);
            scrum.NoAttack = false;
            yield return Frames(30);

            // ③ 메뉴 항복
            yield return EnterYacha(false);
            yield return Frames(30);
            Assert.IsNotNull(GameUi.SurrenderHook);
            GameUi.SurrenderHook();
            yield return WaitEnd(10f);
            log.Add($"항복: {yacha.Result}");
            Assert.AreEqual(Yacha.Outcome.LoseSurrender, yacha.Result, "메뉴 항복 → 패");
            Assert.IsNull(GameUi.SurrenderHook, "끝나면 '항복' 메뉴 없음");
            yield return Frames(30);

            // ④ 바깥으로 밀기: 시우가 바깥쪽으로 달리고 넉백까지 — 링 중심 거리 ≤ 6.8m(매 프레임)
            yield return EnterYacha(false);
            scrum.NoAttack = true;
            var c0 = yacha.Def.Center;
            float maxD = 0f;
            int pushes0 = yacha.Pushes;
            for (int i = 0; i < 300; i++)
            {
                var outDir = HitResolver.Flat(me.Position - c0); if (outDir.sqrMagnitude < 0.01f) outDir = Vector3.back;
                pc.SetStickWorld(outDir.normalized, 1f, true);
                if (i % 60 == 30) me.Knock(outDir.normalized, 1.5f);
                yield return null;
                maxD = Mathf.Max(maxD, HitResolver.Flat(me.Position - c0).magnitude);
            }
            pc.SetStickWorld(Vector3.zero, 0f);
            log.Add($"바깥으로 밀기 5초: 링 중심 최대 {maxD:F2}m · 구경꾼 떠밂 {yacha.Pushes - pushes0}번");
            Assert.That(maxD, Is.LessThanOrEqualTo(6.8f), "시우 거리 ≤ 6.8m");
            Assert.That(yacha.Pushes - pushes0, Is.GreaterThan(0), "가장자리에서 구경꾼이 떠밂");
            GameUi.SurrenderHook?.Invoke();
            yield return WaitEnd(10f);
            Debug.Log("[M2Test] C19 야차 Y1: " + string.Join(" | ", log));
        }
    }
}
