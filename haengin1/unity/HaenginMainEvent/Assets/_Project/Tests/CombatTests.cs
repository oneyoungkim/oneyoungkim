// 행인1의 메인이벤트 — M2 전투 테스트 C01~C10 · C17① · C20 · C04 (docs/08_M2_전투_설계.md 10-5, 11장 0~6단계)
// 07 9-1 방식: Time.captureDeltaTime = 1/60, 캡슐 리그 + 코드 시험장(CombatLab.Build), 입력은 PlayerCombat 에 직접.
// 프레임 표기: 'f0' = 첫 입력을 PlayerCombat 이 처리한 프레임(테스트 코루틴이 누른 다음 프레임).
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Haengin.Tests
{
    public sealed class CombatTests
    {
        const float Dt = Lab.Dt;
        readonly List<HitEvent> hits = new List<HitEvent>();
        CombatTuning tune;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.captureDeltaTime = Dt;
            TimeFx.Reset();
            hits.Clear();
            Fighter.AnyHit += Record;
            tune = ScriptableObject.CreateInstance<CombatTuning>();
            yield return Lab.FreshScene();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Fighter.AnyHit -= Record;
            TimeFx.Reset();
            GameState.SetPaused(false);
            Time.captureDeltaTime = 0f;
            yield return null;
        }

        void Record(HitEvent e) => hits.Add(e);

        // ───────────────────────── 도구
        /// 캡슐 시우 + 카메라(CM_Explore) + 전투 부품. combatCam = CM_Combat·CombatMode 까지
        (RigFactory.Rig r, PlayerCombat pc) Siwoo(Vector3 feet, float yaw, bool combatCam = false)
        {
            var r = Lab.Rig(feet, yaw);
            var pc = CombatFactory.AddPlayer(r.Player, tune, MoveSet.CreateDefault());
            pc0 = pc;
            if (combatCam)
            {
                var cam = CombatFactory.BuildCombatCam(r, pc, r.CamRig.T, tune, null);
                var mode = CombatFactory.AddMode(r, pc, cam, false);
                mode.Begin(0f);
            }
            else pc.Begin(0f);
            return (r, pc);
        }

        Fighter Dummy(Vector3 feet, float yaw, int hp = 999, string label = "허수아비") =>
            CombatFactory.Dummy(label, feet, yaw, hp, null, 1.80f, 0.30f, tune);

        static float YawTo(Vector3 from, Vector3 to) => HitResolver.Yaw(HitResolver.Flat(to - from));

        /// 적이 시우 쪽으로 기술(시험용 — AI 는 7단계)
        static void EnemyAttack(Fighter e, Fighter target, MoveDef m)
        {
            e.Body.SetYaw(YawTo(e.Position, target.Position));
            e.StartAttack(m, target);
        }

        /// 입력 순서를 '게임 프레임'으로 넣는다(히트스톱으로 멈춘 프레임은 세지 않음 — 08 표의 f 는 게임 시간).
        /// seq 의 f = PlayerCombat 이 그 입력을 처리하는 게임 프레임(0 = 첫 입력). each(게임 프레임 번호) = 매 프레임 끝
        IEnumerator RunGame((int f, Btn b)[] seq, int frames, System.Action<int> each = null)
        {
            int g = 0;
            var done = new HashSet<int>();
            for (int i = 0; i < frames * 3 && g <= frames; i++)
            {
                if (Time.timeScale > 0f)
                    for (int k = 0; k < seq.Length; k++)
                        if (seq[k].f == g && done.Add(k)) pc0.Press(seq[k].b);
                bool game = Time.timeScale > 0f;
                yield return null;
                if (game && Time.deltaTime > 0f) { each?.Invoke(g); g++; }
            }
        }
        PlayerCombat pc0;

        IEnumerator Settle(int frames = 20)
        {
            for (int i = 0; i < frames; i++) yield return null;
        }

        // ───────────────────────── C01 약 4타
        [UnityTest]
        public IEnumerator C01_Combo_Light4_Damage()
        {
            Lab.Floor(0f, 40f);
            var (r, pc) = Siwoo(Vector3.zero, 0f);
            var d = Dummy(new Vector3(0f, 0f, 1.0f), 180f);
            yield return Settle();
            hits.Clear();
            // 타격 시각 = 게임 시간(히트스톱 동안 멈춤 — 08 3-2 '히트스톱은 경직에 들어가지 않는다'). □ 는 게임 시간 5f 마다(히트스톱 중엔 누르지 않음)
            double t0 = -1;
            int pressed = 0, gameF = 0, lastPress = -1;
            for (int i = 0; i < 200; i++)
            {
                // 다음 프레임이 게임 프레임(배율 > 0)이고 그 번호가 5 의 배수면 누른다 → PlayerCombat 이 게임 프레임 0·5·10… 에 처리
                if (Time.timeScale > 0f && gameF % 5 == 0 && gameF <= 74 && lastPress != gameF)
                {
                    pc.Press(Btn.Light);
                    pressed++;
                    lastPress = gameF;
                    if (t0 < 0) t0 = Time.timeAsDouble + Dt;
                }
                yield return null;
                if (t0 >= 0 && Time.deltaTime > 0f) gameF++;
            }
            var mine = hits.Where(h => h.Attacker == pc.Me && h.Victim == d).ToList();
            string frames = string.Join(", ", mine.Select(h => $"{h.Move.Label} f{(h.GameTime - t0) * 60:F1} {h.Damage}"));
            Debug.Log($"[M2Test] C01 약 4타: □ {pressed}번 · 맞음 {mine.Count}번 · 피해 {mine.Sum(h => h.Damage)} · {frames}");
            Assert.AreEqual(4, mine.Count, "4번 맞음(4타 뒤 □ 무시)");
            Assert.AreEqual(29, mine.Sum(h => h.Damage), "피해 합 29");
            int[] want = { 7, 21, 39, 53 };
            for (int k = 0; k < 4; k++) Assert.That(System.Math.Abs((mine[k].GameTime - t0) * 60 - want[k]), Is.LessThanOrEqualTo(1.001), $"{k + 1}타 타격 시각 {want[k]}f ±1(게임 시간)");
        }

        // ───────────────────────── C02 마무리 4종
        [UnityTest]
        public IEnumerator C02_Combo_Finishers()
        {
            Lab.Floor(0f, 40f);
            var (r, pc) = Siwoo(Vector3.zero, 0f);
            var d = Dummy(new Vector3(0f, 0f, 1.0f), 180f);
            var cases = new (string name, (int f, Btn b)[] seq, string move, int dmg)[]
            {
                ("□△", new[] { (0, Btn.Light), (5, Btn.Heavy) }, "어퍼컷", 14),
                ("□□△", new[] { (0, Btn.Light), (8, Btn.Light), (20, Btn.Heavy) }, "스텝 무릎", 12),
                ("□□□△", new[] { (0, Btn.Light), (8, Btn.Light), (20, Btn.Light), (35, Btn.Heavy) }, "큰 훅", 18),
                ("□□□□△", new[] { (0, Btn.Light), (8, Btn.Light), (20, Btn.Light), (35, Btn.Light), (50, Btn.Heavy) }, "큰 훅", 18),
                ("△", new[] { (0, Btn.Heavy) }, "앞차기", 12),
            };
            var log = new List<string>();
            float kick = 0f;
            foreach (var c in cases)
            {
                pc.Me.ResetFighter(); d.ResetFighter();
                ((PlayerBody)pc.Me.Body).Place(Vector3.zero, 0f);
                d.Body.Place(new Vector3(0f, 0f, 1.0f), 180f);
                yield return Settle(150);
                hits.Clear();
                Vector3 dStart = d.Position;
                yield return RunGame(c.seq, 110);
                var last = hits.LastOrDefault(h => h.Attacker == pc.Me && h.Victim == d);
                log.Add($"{c.name} → {last.Move?.Label} {last.Damage}{(last.Move != null && last.Move.Down ? " 다운" : "")} (맞은 것 {string.Join(",", hits.Where(h => h.Attacker == pc.Me).Select(h => h.Move.Label))} · 거리 {HitResolver.Flat(d.Position - pc.Me.Position).magnitude:F2}m)");
                if (last.Move == null || last.Move.Label != c.move) Debug.Log("[M2Test] C02 실패 판: " + log[log.Count - 1]);
                Assert.IsNotNull(last.Move, c.name + " 맞음");
                Assert.AreEqual(c.move, last.Move.Label, c.name + " 기술");
                Assert.AreEqual(c.dmg, last.Damage, c.name + " 피해");
                if (c.move == "큰 훅") Assert.IsTrue(last.Move.Down, c.name + " 다운 기술");
                if (c.name == "△")
                {
                    yield return Settle(40);
                    kick = HitResolver.Flat(d.Position - dStart).magnitude;
                }
            }
            Debug.Log($"[M2Test] C02 마무리: {string.Join(" · ", log)} · 앞차기 넉백 {kick:F3}m");
            Assert.That(kick, Is.EqualTo(1.2f).Within(0.05f), "앞차기 넉백 1.2m ± 0.05");
        }

        // ───────────────────────── C03 연결 창·끊김·버퍼
        [UnityTest]
        public IEnumerator C03_Combo_Window_Reset()
        {
            Lab.Floor(0f, 40f);
            var (r, pc) = Siwoo(Vector3.zero, 0f);
            var d = Dummy(new Vector3(0f, 0f, 1.0f), 180f);
            yield return Settle();
            // (1) □ 뒤 0.5초 쉬고 □ → 다시 잽(순번 0 에서)
            pc.Press(Btn.Light);
            yield return Settle(20 + 30);
            pc.Press(Btn.Light);
            yield return null;
            string m1 = pc.Me.Run?.Move.Label;
            int c1 = pc.Combo;
            yield return Settle(40);
            // (2) 버퍼: 잽 발생 중(3f) □ → 13f 에 크로스 시작
            int crossAt = -1;
            yield return RunGame(new[] { (0, Btn.Light), (3, Btn.Light) }, 30, g =>
            {
                if (crossAt < 0 && pc.Me.Run != null && pc.Me.Run.Move.Label == "크로스") crossAt = g;
            });
            Debug.Log($"[M2Test] C03: 0.5초 쉬고 □ → {m1}(순번 {c1}) · 잽 3f 에 □ → 크로스 시작 {crossAt}f");
            Assert.AreEqual("잽", m1, "0.5초 뒤 □ = 다시 잽");
            Assert.AreEqual(1, c1, "순번 1");
            Assert.AreEqual(13, crossAt, "버퍼: 13f(게임 프레임 — 잽 히트스톱 4f 는 세지 않음) 에 크로스");
        }

        // ───────────────────────── C05 소프트 조준·자석
        [UnityTest]
        public IEnumerator C05_SoftAim_Magnet()
        {
            Lab.Floor(0f, 40f);
            var (r, pc) = Siwoo(Vector3.zero, 0f);
            var p = HitResolver.YawDir(30f) * 1.8f;
            var d = Dummy(p, 210f);
            yield return Settle();
            hits.Clear();
            var start = pc.Me.Position;
            pc.Press(Btn.Light);
            yield return null;                 // f0
            float yaw3 = 0f;
            for (int i = 1; i <= 3; i++) { yield return null; if (i == 2) yaw3 = pc.Me.Yaw; }
            float aimErr = Mathf.Abs(Mathf.DeltaAngle(yaw3, YawTo(pc.Me.Position, d.Position)));
            yield return Settle(20);
            float slide = HitResolver.Flat(pc.Me.Position - start).magnitude;
            bool hit = hits.Any(h => h.Attacker == pc.Me && h.Victim == d && h.Outcome == HitOutcome.Hit);
            Debug.Log($"[M2Test] C05 소프트 조준: 3f 안 대상 쪽 오차 {aimErr:F1}° · 미끄러짐 {slide:F3}m · 맞음 {hit}");
            Assert.That(aimErr, Is.LessThanOrEqualTo(5f), "3f 안에 대상 쪽 ±5°");
            Assert.That(slide, Is.LessThanOrEqualTo(0.8f + 1e-3f), "미끄러짐 ≤ 0.8m");
            Assert.That(slide, Is.GreaterThan(0.3f), "자석으로 다가감");
            Assert.IsTrue(hit, "맞음");
        }

        // ───────────────────────── C06 회피 무적
        [UnityTest]
        public IEnumerator C06_Dodge_IFrames()
        {
            Lab.Floor(0f, 40f);
            var (r, pc) = Siwoo(Vector3.zero, 0f);
            pc.DodgeMoveScale = 0f;
            var e = Dummy(new Vector3(0f, 0f, 1.0f), 180f, 999, "적");
            var jab = MoveLib.EnemyJab();
            var res = new List<string>();
            int mismatch = 0;
            for (int o = -20; o <= 5; o++)
            {
                pc.Me.ResetFighter(); e.ResetFighter();
                ((PlayerBody)pc.Me.Body).Place(Vector3.zero, 0f);
                e.Body.Place(new Vector3(0f, 0f, 1.0f), 180f);
                yield return Settle(60);
                hits.Clear();
                // 적 잽 = 코루틴 프레임 E → 시작 E+1, 판정 시작 E+1+9. 회피 누름 D → 시작 D+1. o = (D+1) − (E+1+9) → D = E + 9 + o
                int E = 25, D = E + 9 + o;
                for (int i = 0; i < 70; i++)
                {
                    if (i == E) EnemyAttack(e, pc.Me, jab);
                    if (i == D) pc.Press(Btn.Dodge);
                    yield return null;
                }
                bool got = hits.Any(h => h.Victim == pc.Me && h.Outcome == HitOutcome.Hit);
                bool expect = !(o >= -13 && o <= -2);     // 판정 첫 프레임(E+9)이 무적 2~13f 안 = 안 맞음
                if (got != expect) mismatch++;
                res.Add($"{o}:{(got ? "맞음" : "피함")}");
            }
            Debug.Log($"[M2Test] C06 회피 무적(회피 시작 − 적 판정 시작, f): {string.Join(" ", res)} · 어긋남 {mismatch}");
            Assert.AreEqual(0, mismatch, "안 맞은 경우 = 판정 시작이 무적 2~13f 안인 경우와 정확히 같음");
        }

        // ───────────────────────── C07 읽었다·반격
        [UnityTest]
        public IEnumerator C07_Dodge_Read_Counter()
        {
            Lab.Floor(0f, 40f);
            var (r, pc) = Siwoo(Vector3.zero, 0f);
            pc.DodgeMoveScale = 0f;
            var e = Dummy(new Vector3(0f, 0f, 1.0f), 180f, 999, "적");
            var jab = MoveLib.EnemyJab();
            yield return Settle(30);
            // (1) 판정 시작 6f 전에 회피 → 읽었다 → □ 카운터
            float heat0 = pc.Heat.Value;
            int reads0 = pc.ReadCount;
            int E = 5, D = E + 9 - 6;
            int slowFrames = 0, readFrame = -1;
            float heatAfter = 0f;
            for (int i = 0; i < 150; i++)
            {
                if (i == E) EnemyAttack(e, pc.Me, jab);
                if (i == D) pc.Press(Btn.Dodge);
                yield return null;
                if (Mathf.Approximately(Time.timeScale, 0.5f)) slowFrames++;
                if (readFrame < 0 && pc.ReadCount > reads0) { readFrame = i; heatAfter = pc.Heat.Value; pc.Press(Btn.Light); }
            }
            var counter = hits.FirstOrDefault(h => h.Attacker == pc.Me && h.Victim == e);
            int reads = pc.ReadCount - reads0;
            // (2) 판정 14f 전 회피 → 읽었다 없음
            pc.Me.ResetFighter(); e.ResetFighter();
            ((PlayerBody)pc.Me.Body).Place(Vector3.zero, 0f);
            e.Body.Place(new Vector3(0f, 0f, 1.0f), 180f);
            yield return Settle(60);
            int reads1 = pc.ReadCount;
            for (int i = 0; i < 50; i++)
            {
                if (i == E) EnemyAttack(e, pc.Me, jab);
                if (i == E + 9 - 14) pc.Press(Btn.Dodge);
                yield return null;
            }
            int late = pc.ReadCount - reads1;
            // (3) 실제 회피 이동(입력 없음 = 뒤 1.6m)으로 읽었다 → △ 더킹 어퍼가 닿는가(반격 자석 2.4m)
            pc.DodgeMoveScale = 1f;
            pc.Me.ResetFighter(); e.ResetFighter();
            ((PlayerBody)pc.Me.Body).Place(Vector3.zero, 0f);
            e.Body.Place(new Vector3(0f, 0f, 1.0f), 180f);
            yield return Settle(60);
            hits.Clear();
            int reads2 = pc.ReadCount;
            bool pressed = false;
            for (int i = 0; i < 150; i++)
            {
                if (i == E) EnemyAttack(e, pc.Me, jab);
                if (i == E + 9 - 6) pc.Press(Btn.Dodge);
                yield return null;
                if (!pressed && pc.ReadCount > reads2) { pressed = true; pc.Press(Btn.Heavy); }
            }
            var duck = hits.FirstOrDefault(h => h.Attacker == pc.Me && h.Victim == e);
            Debug.Log($"[M2Test] C07 읽었다: {reads}번 · 기세 {heat0:F0}→{heatAfter:F0}(+{heatAfter - heat0:F0}) · 슬로 50% {slowFrames}f({slowFrames * Dt:F3}초) · 카운터 {counter.Move?.Label} {counter.Damage} / 14f 전 회피: 읽었다 {late}번 / 뒤 회피(1.6m) 읽었다 {pc.ReadCount - reads2} → {duck.Move?.Label} {duck.Damage}");
            Assert.AreEqual(1, pc.ReadCount - reads2, "뒤로 회피해도 읽었다(회피 시작 자리 기준)");
            Assert.AreEqual("더킹 어퍼", duck.Move?.Label, "뒤 회피 뒤 △ 더킹 어퍼가 닿음");
            Assert.AreEqual(20, duck.Damage, "더킹 어퍼 피해 20");
            Assert.AreEqual(1, reads, "읽었다 1번");
            Assert.That(heatAfter - heat0, Is.EqualTo(15f).Within(0.01f), "기세 +15");
            Assert.That(slowFrames * Dt, Is.EqualTo(0.3f).Within(Dt + 1e-4f), "슬로 0.3초 50%(실제 시간 ±1f)");
            Assert.AreEqual("카운터 크로스", counter.Move?.Label, "□ = 카운터 크로스");
            Assert.AreEqual(14, counter.Damage, "카운터 피해 14");
            Assert.AreEqual(0, late, "14f 전 회피 = 읽었다 없음");
        }

        // ───────────────────────── C08 잡기 3갈래
        [UnityTest]
        public IEnumerator C08_Grab_Branches()
        {
            Lab.Floor(0f, 40f);
            // 벽: 허수아비(시우 앞 0.55m 에 붙음) 등 뒤 1.0m — 잡은 자리 z 0.55 → 벽 면 z 1.55
            var wall = Lab.Box("Wall", new Vector3(0f, 1.25f, 1.55f + 0.25f), new Vector3(6f, 2.5f, 0.5f), Quaternion.identity, Layers.Wall);
            wall.AddComponent<HeatSurface>();
            wall.SetActive(false);
            var (r, pc) = Siwoo(Vector3.zero, 0f);
            var d = Dummy(new Vector3(0f, 0f, 0.8f), 180f);
            yield return Settle();
            var log = new List<string>();

            // (1) 잡기 → □×3 → 자동 밀기
            hits.Clear();
            pc.Press(Btn.Grab);
            yield return Settle(16);
            Assert.AreEqual(d, pc.Held, "잡힘");
            for (int k = 0; k < 3; k++) { pc.Press(Btn.Light); yield return Settle(24); }
            yield return Settle(40);
            var knees = hits.Where(h => h.Attacker == pc.Me && h.Victim == d).Select(h => h.Move.Label + " " + h.Damage).ToList();
            log.Add("무릎×3: " + string.Join(", ", knees));
            var dmgs = hits.Where(h => h.Attacker == pc.Me && h.Victim == d).Select(h => h.Damage).ToArray();
            Assert.That(dmgs, Is.EqualTo(new[] { 6, 6, 8, 8 }), "무릎 6·6·8 후 자동 밀기 8");
            Assert.AreEqual("하체 밀기", hits.Last(h => h.Attacker == pc.Me && h.Victim == d).Move.Label, "자동 밀기");

            // (2) 잡기 → △, 등 뒤 1.0m 벽 → 벽꽝 8 + 6, 경직 60f
            wall.SetActive(true);
            pc.Me.ResetFighter(); d.ResetFighter();
            ((PlayerBody)pc.Me.Body).Place(Vector3.zero, 0f);
            d.Body.Place(new Vector3(0f, 0f, 0.8f), 180f);
            yield return Settle(40);
            hits.Clear();
            pc.Press(Btn.Grab);
            yield return Settle(16);
            Assert.AreEqual(d, pc.Held, "잡힘(벽)");
            pc.Press(Btn.Heavy);
            float stag = 0f;
            for (int i = 0; i < 60; i++)
            {
                yield return null;
                if (hits.Any(h => h.Move != null && h.Move.Label == "벽꽝") && stag == 0f) stag = d.StaggerLeft;
            }
            var wd = hits.Where(h => h.Victim == d).Select(h => $"{h.Move.Label} {h.Damage}").ToList();
            log.Add("밀기(벽): " + string.Join(", ", wd) + $" · 경직 {stag * 60f:F0}f");
            Assert.That(hits.Where(h => h.Victim == d).Select(h => h.Damage).ToArray(), Is.EqualTo(new[] { 8, 6 }), "벽꽝 피해 8 + 6");
            Assert.That(stag * 60f, Is.EqualTo(60f).Within(1.5f), "벽꽝 경직 60f");
            wall.SetActive(false);

            // (3) 방향 + ○ → 돌려세우기: 적의 등이 입력 방향(+X)
            pc.Me.ResetFighter(); d.ResetFighter();
            ((PlayerBody)pc.Me.Body).Place(Vector3.zero, 0f);
            d.Body.Place(new Vector3(0f, 0f, 0.8f), 180f);
            yield return Settle(40);
            pc.Press(Btn.Grab);
            yield return Settle(16);
            Assert.AreEqual(d, pc.Held, "잡힘(돌려세우기)");
            for (int i = 0; i < 30; i++)
            {
                pc.SetStickWorld(Vector3.right, 1f);
                if (i == 1) pc.Press(Btn.Grab);
                yield return null;
            }
            pc.SetStickWorld(Vector3.zero, 0f);
            var back = -d.Forward;
            float err = Vector3.Angle(back, Vector3.right);
            log.Add($"돌려세우기: 적 등 방향 오차 {err:F1}°");
            Assert.That(err, Is.LessThanOrEqualTo(10f), "적 등 = 입력 방향 ±10°");
            yield return Settle(150);

            // (4) 덩치형(슈퍼아머) 잡기 → 뿌리침: 시우 경직 18f
            pc.Me.ResetFighter(); d.ResetFighter();
            d.Armor = true;
            ((PlayerBody)pc.Me.Body).Place(Vector3.zero, 0f);
            d.Body.Place(new Vector3(0f, 0f, 0.8f), 180f);
            yield return Settle(40);
            pc.Press(Btn.Grab);
            float shake = 0f;
            for (int i = 0; i < 16; i++) { yield return null; if (shake == 0f && pc.Me.State == Fighter.Phase.Stagger) shake = pc.Me.StaggerLeft; }
            log.Add($"덩치 잡기: 잡힘 {pc.Held != null} · 시우 경직 {shake * 60f:F0}f");
            Debug.Log("[M2Test] C08 잡기: " + string.Join(" / ", log));
            Assert.IsNull(pc.Held, "덩치형은 못 잡음");
            Assert.That(shake * 60f, Is.EqualTo(18f).Within(1.5f), "뿌리침: 시우 경직 18f");
        }

        // ───────────────────────── C09 막기·가드 크러시
        [UnityTest]
        public IEnumerator C09_Guard_Crush()
        {
            Lab.Floor(0f, 40f);
            var (r, pc) = Siwoo(Vector3.zero, 0f);
            var e = Dummy(new Vector3(0f, 0f, 1.0f), 180f, 999, "적");
            var jab = MoveLib.EnemyJab();
            yield return Settle(30);
            hits.Clear();
            int hp0 = pc.Me.Hp;
            float heat0 = pc.Heat.Value;
            float crushStag = 0f;
            for (int i = 0; i < 7 * 30 + 40; i++)
            {
                pc.SetGuard(true);
                if (i % 30 == 0 && i < 7 * 30) EnemyAttack(e, pc.Me, jab);
                yield return null;
                if (crushStag == 0f && hits.Any(h => h.Outcome == HitOutcome.Crushed)) crushStag = pc.Me.StaggerLeft;
            }
            pc.SetGuard(false);
            var mine = hits.Where(h => h.Victim == pc.Me).ToList();
            string seq = string.Join(" ", mine.Select(h => h.Outcome == HitOutcome.Blocked ? "막음" : h.Outcome.ToString()));
            Debug.Log($"[M2Test] C09 가드: {seq} · 피해 {hp0 - pc.Me.Hp}(한 번 {string.Join("/", mine.Select(h => h.Damage))}) · 크러시 경직 {crushStag * 60f:F0}f · 기세 {heat0:F0}→{pc.Heat.Value:F0}");
            Assert.AreEqual(7, mine.Count, "7번 맞음");
            Assert.IsTrue(mine.Take(6).All(h => h.Outcome == HitOutcome.Blocked), "6번 막음");
            Assert.AreEqual(HitOutcome.Crushed, mine[6].Outcome, "7번째 크러시");
            Assert.IsTrue(mine.All(h => h.Damage == 1), "막은 피해 = 5 × 20% 올림 = 1");
            Assert.That(crushStag * 60f, Is.EqualTo(48f).Within(1.5f), "크러시 경직 48f");
            Assert.That(pc.Heat.Value, Is.EqualTo(heat0 + 6 * 2 - 5).Within(0.5f), "막기 +2 ×6, 크러시 −5");
        }

        // ───────────────────────── C09b 전진 버팀(08 3-5 — 표에 번호 없음, 5단계 확인용으로 더함)
        [UnityTest]
        public IEnumerator C09b_Brace_Forward()
        {
            Lab.Floor(0f, 40f);
            var (r, pc) = Siwoo(Vector3.zero, 0f);
            var e = Dummy(new Vector3(0f, 0f, 1.0f), 180f, 999, "적");
            var jab = MoveLib.EnemyJab();
            yield return Settle(30);
            hits.Clear();
            float heat0 = pc.Heat.Value;
            int hp0 = pc.Me.Hp;
            // 시우 □□ (크로스 = 게임 13f 시작, 발생 8f) · 적 잽을 크로스 발생 중에 맞게: 적 잽 판정 = 시작 + 9 → 크로스 시작 + 3 쯤
            string crossState = "";
            bool crossLanded = false;
            yield return RunGame(new[] { (0, Btn.Light), (8, Btn.Light) }, 60, g =>
            {
                if (g == 7) EnemyAttack(e, pc.Me, jab);           // 적 잽 시작 = 게임 8f → 판정 17f(크로스 13f 시작 + 4f, 발생 중)
                if (g == 18) crossState = pc.Me.State + " " + pc.Me.Run?.Move.Label;
            });
            crossLanded = hits.Any(h => h.Attacker == pc.Me && h.Move.Label == "크로스" && h.Outcome == HitOutcome.Hit);
            var braced = hits.FirstOrDefault(h => h.Victim == pc.Me);
            float heatAfter = pc.Heat.Value;
            // 2초 쿨다운 안의 두 번째 약타 = 그대로 경직
            yield return Settle(20);
            hits.Clear();
            yield return RunGame(new[] { (0, Btn.Light), (8, Btn.Light) }, 40, g => { if (g == 7) EnemyAttack(e, pc.Me, jab); });
            var second = hits.FirstOrDefault(h => h.Victim == pc.Me);
            Debug.Log($"[M2Test] C09b 전진 버팀: 크로스 발생 중 적 잽 → {braced.Outcome} 피해 {braced.Damage}(HP {hp0}→{pc.Me.Hp}) · 맞은 직후 {crossState} · 크로스 맞힘 {crossLanded} · 버팀 {pc.BraceCount}번 / 쿨다운 안 두 번째 → {second.Outcome}");
            Assert.AreEqual(HitOutcome.Braced, braced.Outcome, "크로스 발생 중 약타 = 버팀");
            Assert.AreEqual(5, braced.Damage, "피해는 받음");
            Assert.IsTrue(crossLanded, "공격 계속(크로스 맞힘)");
            Assert.AreEqual(HitOutcome.Hit, second.Outcome, "쿨다운 2초 안 = 그대로 경직");
        }

        // ───────────────────────── C10 타격감 숫자
        [UnityTest]
        public IEnumerator C10_Impact_Feel_Numbers()
        {
            Lab.Floor(0f, 40f);
            var (r, pc) = Siwoo(Vector3.zero, 0f);
            var d = Dummy(new Vector3(0f, 0f, 1.0f), 180f);
            var shaker = r.Cam.GetComponent<TraumaShake>();
            Assert.IsNotNull(shaker, "CM_Explore 에 흔들림 확장");
            float[] stopWant = { 0f, 0.060f, 0.095f, 0.160f, 0.200f };
            float[] knockWant = { 0f, 0.05f, 0.10f, 0.16f, 0f };
            float[] trauma = { 0f, 0.28f, 0.45f, 0.80f, 1.00f };
            var log = new List<string>();
            for (int p = 1; p <= 4; p++)
            {
                pc.Me.ResetFighter(); d.ResetFighter();
                ((PlayerBody)pc.Me.Body).Place(Vector3.zero, 0f);
                d.Body.Place(new Vector3(0f, 0f, 1.0f), 180f);
                yield return Settle(120);
                var m = MoveLib.Jab();
                m.Label = "시험 " + p;
                m.Power = (Power)p;
                m.Knock = knockWant[p];
                m.Magnet = 0f;
                hits.Clear();
                var d0 = d.Position;
                pc.Me.StartAttack(m, d);
                int stopFrames = 0, flashFrames = 0;
                float maxShake = 0f, maxExpect = 0f, shakeDur = 0f;
                bool hitSeen = false, scaleBack = false, camMoved = false;
                double tr = 0, clock0 = 0;
                for (int i = 0; i < 90; i++)
                {
                    yield return null;
                    if (!hitSeen && hits.Count > 0) { hitSeen = true; tr = trauma[p]; clock0 = Shake.Clock; }
                    if (!hitSeen) continue;
                    if (Time.timeScale == 0f) stopFrames++;
                    else if (stopFrames > 0) scaleBack = Time.timeScale == 1f || scaleBack;
                    if (d.React != null && d.React.FlashLeft > 0f) flashFrames++;
                    // 코루틴은 LateUpdate(카메라) 전에 돈다 → LastPos 는 지난 프레임 카메라 계산 값. 그 계산 때의 흔들림 시계(LastClock)로 시안 식과 맞춘다
                    double tc = shaker.LastClock - clock0;
                    if (tc < -1e-6) continue;
                    float sx = Mathf.Abs(shaker.LastPos.x);
                    if (sx > 1e-6f) shakeDur = (float)tc;
                    maxShake = Mathf.Max(maxShake, sx);
                    // 시안 식(05_fx.js CamRig): 트라우마 t₀ − 1.8·t(실제 시간), 세기 s = t², 가로 = 0.14·s·n(1)
                    double tt = System.Math.Max(0, tr - 1.8 * tc);
                    maxExpect = Mathf.Max(maxExpect, Mathf.Abs((float)(tt * tt * 0.14 * Shake.N(1f, shaker.LastClock))));
                    if (Vector3.Distance(Camera.main.transform.position, r.Cam.State.RawPosition) > 1e-4f) camMoved = true;
                }
                yield return Settle(30);
                float knock = HitResolver.Flat(d.Position - d0).magnitude;
                float stop = stopFrames * Dt, flash = flashFrames * Dt;
                float durWant = trauma[p] / 1.8f;
                log.Add($"p{p}: 히트스톱 {stop:F3}(목표 {stopWant[p]:F3}) · 흔들림 최대 {maxShake * 100f:F2}cm(시안 {maxExpect * 100f:F2}) {shakeDur:F2}초(시안 {durWant:F2}) · 번쩍 {flash:F3} · 넉백 {knock * 100f:F1}cm");
                Assert.IsTrue(hitSeen, $"p{p} 맞음");
                Assert.That(stop, Is.EqualTo(stopWant[p]).Within(Dt + 1e-4f), $"p{p} 히트스톱 ±1f");
                Assert.IsTrue(scaleBack, $"p{p} timeScale 1 로 돌아옴");
                Assert.That(maxShake, Is.EqualTo(maxExpect).Within(maxExpect * 0.2f + 1e-5f), $"p{p} 흔들림 최대 진폭 ±20%");
                Assert.That(shakeDur, Is.EqualTo(durWant).Within(0.05f + Dt), $"p{p} 흔들림 지속 ±0.05초");
                Assert.IsTrue(camMoved, $"p{p} 실제 카메라가 흔들림");
                Assert.That(flash, Is.EqualTo(0.1f).Within(Dt + 1e-4f), $"p{p} 번쩍 0.1초");
                if (p <= 3) Assert.That(knock, Is.EqualTo(knockWant[p]).Within(0.01f), $"p{p} 넉백 ±1cm");
            }
            Debug.Log("[M2Test] C10 타격감: " + string.Join(" / ", log));
        }

        // ───────────────────────── C04 락온
        [UnityTest]
        public IEnumerator C04_LockOn_Pick_Switch_Lose()
        {
            Lab.Floor(0f, 80f);
            var (r, pc) = Siwoo(Vector3.zero, 0f, true);
            var front = Dummy(new Vector3(0f, 0f, 3f), 180f, 50, "정면");
            var right = Dummy(HitResolver.YawDir(40f) * 3f, 220f, 50, "오른쪽");
            var leftBack = Dummy(HitResolver.YawDir(-140f) * 3.5f, 40f, 50, "왼쪽뒤");
            yield return Settle(60);
            var lk = pc.Lock;
            pc.ToggleLock();
            yield return Settle(5);
            string pick = lk.Target?.Label;
            // 오른쪽 튕기기: 중립 → 0.1초 안에 (1, 0)
            pc.LookStick(Vector2.zero); yield return null;
            pc.LookStick(new Vector2(0.5f, 0f)); yield return null;
            pc.LookStick(new Vector2(1f, 0f)); yield return null;
            pc.LookStick(Vector2.zero);
            yield return Settle(5);
            string flick = lk.Target?.Label;
            // 대상 탈락 → 0.4초 뒤 남은 가까운 적
            var victim = lk.Target;
            victim.Hp = 1;
            victim.Receive(pc.Me, MoveLib.Jab());
            int waited = 0;
            while (lk.Target == victim && waited < 60) { yield return null; waited++; }
            string next = lk.Target?.Label;
            float delay = waited * Dt;
            // 12m 넘게 멀어지면 풀림
            var cur = lk.Target;
            cur.Body.Place(new Vector3(0f, 0f, 13f), 180f);
            yield return Settle(3);
            bool lostFar = lk.Target == null;
            // 벽 뒤 1.0초 → 풀림
            cur.Body.Place(new Vector3(0f, 0f, 4f), 180f);
            lk.Set(cur);
            var wall = Lab.Box("Wall", new Vector3(0f, 1.25f, 2.5f), new Vector3(4f, 2.5f, 0.3f), Quaternion.identity, Layers.Wall);
            int hiddenFrames = 0;
            while (lk.Target != null && hiddenFrames < 120) { yield return null; hiddenFrames++; }
            Debug.Log($"[M2Test] C04 락온: 고름 {pick} · 오른쪽 튕김 → {flick} · 탈락 → {delay:F2}초 뒤 {next} · 13m → 풀림 {lostFar} · 벽 뒤 {hiddenFrames * Dt:F2}초에 풀림");
            Assert.AreEqual("정면", pick, "화면 가운데 = 정면");
            Assert.AreEqual("오른쪽", flick, "오른쪽 튕김");
            Assert.That(delay, Is.EqualTo(0.4f).Within(Dt * 2 + 1e-4f), "0.4초 뒤 다음");
            Assert.AreEqual("정면", next, "남은 가까운 적(정면 3m · 왼쪽뒤 3.5m)");
            Assert.IsTrue(lostFar, "12m 넘으면 풀림");
            Assert.That(hiddenFrames * Dt, Is.EqualTo(1.0f).Within(Dt * 2 + 1e-4f), "벽 뒤 1.0초 → 풀림");
            Object.Destroy(wall);
        }

        // ───────────────────────── C17① 락온 구도
        [UnityTest]
        public IEnumerator C17_Camera_Framing_Lock()
        {
            Lab.Floor(0f, 80f);
            var (r, pc) = Siwoo(Vector3.zero, 0f, true);
            var e = Dummy(new Vector3(0f, 0f, 3f), 180f, 50, "대상");
            yield return Settle(40);
            pc.ToggleLock();
            var log = new List<string>();
            foreach (float dist in new[] { 1.5f, 3f, 6f })
            {
                e.Body.Place(new Vector3(0f, 0f, dist), 180f);
                yield return Settle(150);
                var cam = Camera.main;
                bool ok = true;
                var pts = new List<string>();
                foreach (var f in new[] { pc.Me, e })
                    foreach (var h in new[] { 0.05f, f.Height })
                    {
                        var v = cam.WorldToViewportPoint(f.Position + Vector3.up * h);
                        pts.Add($"({v.x:F2},{v.y:F2})");
                        if (v.z <= 0f || v.x < 0.1f || v.x > 0.9f || v.y < 0.1f || v.y > 0.95f) ok = false;
                    }
                float cd = Vector3.Distance(cam.transform.position, pc.CombatCam.Pivot.position);
                log.Add($"{dist}m: 카메라 {cd:F2}m 옆 {pc.CombatCam.Side:+0;-0} 시우 발·머리 {pts[0]} {pts[1]} 대상 {pts[2]} {pts[3]}{(ok ? "" : " ✗")}");
            }
            Debug.Log("[M2Test] C17① 락온 구도: " + string.Join(" / ", log));
            Assert.IsFalse(log.Any(l => l.EndsWith("✗")), "시우·대상 둘 다 뷰포트 x 0.1~0.9, y 0.1~0.95");
        }

        // ───────────────────────── C20 프레임레이트 독립(C01·C06 짧은 판)
        [UnityTest]
        public IEnumerator C20_FrameRate_Independence()
        {
            var rates = new[] { 60f, 20f, 30f, 144f };
            var refTimes = new List<double>();
            int refHits = 0, refDmg = 0;
            var log = new List<string>();
            foreach (var fps in rates)
            {
                float dt = 1f / fps;
                Time.captureDeltaTime = dt;
                TimeFx.Reset();
                yield return Lab.FreshScene();
                Lab.Floor(0f, 40f);
                var (r, pc) = Siwoo(Vector3.zero, 0f);
                var d = Dummy(new Vector3(0f, 0f, 1.0f), 180f);
                for (int i = 0; i < Mathf.RoundToInt(0.4f / dt); i++) yield return null;
                hits.Clear();
                // C01 짧은 판: 5/60초마다 □(게임 시간), 1.2초까지
                double t0 = -1, next = 0, gt = 0;
                var stops = new List<int>();
                int zeroRun = 0;
                for (int i = 0; i < Mathf.RoundToInt(2.5f / dt); i++)
                {
                    if (gt >= next - 1e-6 && gt <= 1.24) { pc.Press(Btn.Light); if (t0 < 0) t0 = Time.timeAsDouble + dt; next += 5.0 / 60.0; }
                    yield return null;
                    gt += Time.deltaTime;
                    if (Time.timeScale == 0f) zeroRun++;
                    else if (zeroRun > 0) { stops.Add(zeroRun); zeroRun = 0; }
                }
                var mine = hits.Where(h => h.Attacker == pc.Me).ToList();
                var times = mine.Select(h => h.GameTime - t0).ToList();
                if (fps == 60f) { refTimes = times; refHits = mine.Count; refDmg = mine.Sum(h => h.Damage); }
                float firstStop = stops.Count > 0 ? stops[0] * dt : 0f;
                log.Add($"{fps}fps: {mine.Count}번 {mine.Sum(h => h.Damage)} · 시각 {string.Join("/", times.Select(x => (x * 60).ToString("F1")))}f · 첫 히트스톱 {firstStop:F3}초 · timeScale {Time.timeScale}");
                Assert.AreEqual(refHits, mine.Count, $"{fps}fps 맞은 횟수");
                Assert.AreEqual(refDmg, mine.Sum(h => h.Damage), $"{fps}fps 피해");
                for (int k = 0; k < times.Count && k < refTimes.Count; k++)
                    Assert.That(System.Math.Abs(times[k] - refTimes[k]), Is.LessThanOrEqualTo(dt + 1e-4), $"{fps}fps {k + 1}타 시각 차 ≤ 한 프레임");
                Assert.That(firstStop, Is.EqualTo(0.06f).Within(dt + 1e-4f), $"{fps}fps 히트스톱 ±1프레임");
                Assert.AreEqual(1f, Time.timeScale, $"{fps}fps 끝난 뒤 timeScale 1");

                // C06 짧은 판: 적 판정 시작 0.10초 전 회피 = 피함, 0.30초 전 = 맞음(무적 끝난 뒤), 0.05초 뒤 = 맞음(이미 맞음)
                pc.DodgeMoveScale = 0f;
                var e = Dummy(new Vector3(1.0f, 0f, 0f), -90f, 999, "적");
                d.gameObject.SetActive(false);
                var res = new List<bool>();
                foreach (double lead in new[] { 0.10, 0.30, -0.05 })
                {
                    pc.Me.ResetFighter(); e.ResetFighter();
                    for (int i = 0; i < Mathf.RoundToInt(0.6f / dt); i++) yield return null;
                    hits.Clear();
                    double start = 0, dodgeAt = 0.15 + 9.0 / 60.0 - lead, enemyAt = 0.15;
                    bool eDone = false, dDone = false;
                    for (int i = 0; i < Mathf.RoundToInt(1.0f / dt); i++)
                    {
                        if (!eDone && start >= enemyAt - 1e-6) { EnemyAttack(e, pc.Me, MoveLib.EnemyJab()); eDone = true; }
                        if (!dDone && start >= dodgeAt - 1e-6) { pc.Press(Btn.Dodge); dDone = true; }
                        yield return null;
                        start += Time.deltaTime;
                    }
                    res.Add(hits.Any(h => h.Victim == pc.Me && h.Outcome == HitOutcome.Hit));
                }
                log.Add($"  회피 0.10/0.30/−0.05초: {string.Join("/", res.Select(b => b ? "맞음" : "피함"))}");
                Assert.That(res, Is.EqualTo(new[] { false, true, true }), $"{fps}fps 회피 판정");
            }
            Time.captureDeltaTime = Dt;
            Debug.Log("[M2Test] C20 프레임레이트: " + string.Join(" / ", log));
        }
    }
}
