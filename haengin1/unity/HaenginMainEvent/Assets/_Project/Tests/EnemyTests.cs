// 행인1의 메인이벤트 — M2 적 테스트 C11 · C12 · C13 (docs/08_M2_전투_설계.md 10-5, 11장 7·8단계)
// 캡슐 적(유형 색) + 코드 시험장, 시간 1/60 고정, 난수는 CombatTuning.Seed 로 고정.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Haengin.Tests
{
    public sealed class EnemyTests
    {
        const float Dt = Lab.Dt;
        readonly List<HitEvent> hits = new List<HitEvent>();
        CombatTuning tune;
        PlayerCombat pc0;

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

        (RigFactory.Rig r, PlayerCombat pc) Siwoo(Vector3 feet, float yaw, bool combatCam = true)
        {
            var r = Lab.Rig(feet, yaw);
            var pc = CombatFactory.AddPlayer(r.Player, tune, MoveSet.CreateDefault());
            pc0 = pc;
            if (combatCam)
            {
                var cam = CombatFactory.BuildCombatCam(r, pc, r.CamRig.T, tune, null);
                CombatFactory.AddMode(r, pc, cam, false).Begin(0f);
            }
            else pc.Begin(0f);
            return (r, pc);
        }

        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        /// 입력을 게임 프레임으로(히트스톱 프레임은 세지 않음)
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

        static MoveDef Kill()
        {
            var m = MoveLib.Jab();
            m.Label = "시험 탈락";
            m.Damage = 9999;
            return m;
        }

        // ───────────────────────── C11 공격권
        IEnumerator DirectorRun(EnemyDef.Kind[] kinds, int max, string tag)
        {
            Lab.Floor(0f, 60f);
            var (r, pc) = Siwoo(Vector3.zero, 0f);
            pc.Me.MaxHp = pc.Me.Hp = 99999;
            var dir = CombatFactory.Director(pc.Me, tune);
            dir.MaxAttackers = max;
            var es = new List<EnemyBrain>();
            for (int i = 0; i < kinds.Length; i++)
            {
                var pos = HitResolver.YawDir(-40f + 40f * i) * 6f;
                es.Add(CombatFactory.Enemy(EnemyLib.Make(kinds[i]), pos, HitResolver.Yaw(-pos), pc.Me, dir, i, tune));
            }
            yield return Frames(5);
            foreach (var e in es) e.Activate();
            int maxAtk = 0, frames = Mathf.RoundToInt(60f / Dt), downs = 0, crushes = 0;
            double clock = 0;
            for (int i = 0; i < frames; i++)
            {
                // 막기만: 공격권 가진 적(없으면 가장 가까운 적) 쪽을 보고 막는다
                var face = es.FirstOrDefault(e => e.HasToken) ?? es.OrderBy(e => (e.Me.Position - pc.Me.Position).sqrMagnitude).First();
                if (pc.Me.State == Fighter.Phase.Free) pc.Me.Body.SetYaw(HitResolver.Yaw(HitResolver.Flat(face.Me.Position - pc.Me.Position)));
                pc.SetGuard(true);
                yield return null;
                clock += Time.deltaTime;
                maxAtk = Mathf.Max(maxAtk, dir.Attacking);
                Assert.That(dir.Attacking, Is.LessThanOrEqualTo(max), $"{tag}: 동시 공격 ≤ {max} (f{i})");
            }
            pc.SetGuard(false);
            downs = hits.Count(h => h.Victim == pc.Me && h.Move != null && h.Move.Down && h.Outcome == HitOutcome.Hit);
            crushes = hits.Count(h => h.Victim == pc.Me && h.Outcome == HitOutcome.Crushed);
            var starts = dir.Starts;
            // 서로 다른 적 공격 시작 간격(공격 끝 → 다음 시작 0.6초라 시작 → 시작은 더 김)
            double minGap = double.MaxValue;
            for (int k = 1; k < starts.Count; k++)
                if (starts[k].who != starts[k - 1].who) minGap = System.Math.Min(minGap, starts[k].at - starts[k - 1].at);
            var per = es.Select(e =>
            {
                var ts = starts.Where(s => s.who == e).Select(s => s.at).ToList();
                double worst = ts.Count == 0 ? 60 : ts[0];
                for (int k = 1; k < ts.Count; k++) worst = System.Math.Max(worst, ts[k] - ts[k - 1]);
                return (e, n: ts.Count, first: ts.Count > 0 ? ts[0] : -1, worst);
            }).ToList();
            Debug.Log($"[M2Test] C11 {tag}: 60초 · 동시 공격 최대 {maxAtk}(한도 {max}) · 공격 시작 {starts.Count}번 · 서로 다른 적 시작 간격 최소 {(minGap == double.MaxValue ? -1 : minGap):F2}초 · " +
                      string.Join(" / ", per.Select(p => $"{p.e.Def.Label} {p.n}번 첫 {p.first:F1}초 최대 기다림 {p.worst:F1}초")) + $" · 시우 크러시 {crushes} · 다운 {downs} · HP 잃음 {99999 - pc.Me.Hp}");
            Assert.That(maxAtk, Is.LessThanOrEqualTo(max), "동시 공격 한도");
            if (max == 1 && minGap != double.MaxValue) Assert.That(minGap, Is.GreaterThanOrEqualTo(tune.AttackGap - 1e-3), "서로 다른 적 공격 시작 간격 ≥ 0.6초");
            foreach (var p in per)
            {
                Assert.That(p.n, Is.GreaterThan(0), p.e.Def.Label + " 공격함");
                Assert.That(p.worst, Is.LessThanOrEqualTo(tune.StarveLimit), p.e.Def.Label + " 15초 넘게 못 받은 적 없음");
            }
        }

        [UnityTest] public IEnumerator C11_Director_Kkanjok3() => DirectorRun(new[] { EnemyDef.Kind.Kkanjok, EnemyDef.Kind.Kkanjok, EnemyDef.Kind.Kkanjok }, 1, "깐족이 3명");
        [UnityTest] public IEnumerator C11_Director_ThreeTypes() => DirectorRun(new[] { EnemyDef.Kind.Kkanjok, EnemyDef.Kind.Seokdal, EnemyDef.Kind.Naengjanggo }, 1, "3유형");
        [UnityTest] public IEnumerator C11_Director_Hard2() => DirectorRun(new[] { EnemyDef.Kind.Kkanjok, EnemyDef.Kind.Seokdal, EnemyDef.Kind.Naengjanggo }, 2, "3유형 어려움(2명)");

        // ───────────────────────── C13 다운·기상
        [UnityTest]
        public IEnumerator C13_Down_GetUp()
        {
            Lab.Floor(0f, 60f);
            var (r, pc) = Siwoo(Vector3.zero, 0f);
            var dir = CombatFactory.Director(pc.Me, tune);
            var e = CombatFactory.Enemy(EnemyLib.Kkanjok(), new Vector3(0f, 0f, 1.1f), 180f, pc.Me, dir, 0, tune);
            e.NoAttack = true; e.HoldPosition = true;
            yield return Frames(5);
            e.Activate();
            yield return Frames(20);
            // (1) □□□△ 큰 훅 → 적 다운 → 누움 1.0 → 기상 0.85 → 간보기, 그동안 무적
            hits.Clear();
            double fallAt = -1, lieAt = -1, upAt = -1, freeAt = -1, gt = 0;
            bool invulAll = true;
            int hitsDuringDown = 0;
            int hitsAtFall = -1;
            yield return RunGame(new[] { (0, Btn.Light), (8, Btn.Light), (20, Btn.Light), (35, Btn.Heavy), (90, Btn.Light), (115, Btn.Light), (140, Btn.Light) }, 260, g =>
            {
                gt = g * (double)Dt;
                var s = e.Me.State;
                if (fallAt < 0 && s == Fighter.Phase.Fall) { fallAt = gt; hitsAtFall = hits.Count(h => h.Victim == e.Me && h.Outcome != HitOutcome.Read); }
                if (lieAt < 0 && s == Fighter.Phase.Lie) lieAt = gt;
                if (upAt < 0 && s == Fighter.Phase.GetUp) upAt = gt;
                if (upAt > 0 && freeAt < 0 && s == Fighter.Phase.Free) { freeAt = gt; hitsDuringDown = hits.Count(h => h.Victim == e.Me && h.Outcome != HitOutcome.Read) - hitsAtFall; }
                if (e.Me.Down && !e.Me.Invulnerable) invulAll = false;
            });
            yield return Frames(10);
            string brainAfter = e.State.ToString();
            float lie = (float)(upAt - lieAt), getup = (float)(freeAt - upAt), fall = (float)(lieAt - fallAt);
            // (2) 시우 다운 + 연타 10번 → 누움 1.0초 · 다운된 시우에게 공격권 0
            var dir2 = dir;
            var others = new List<EnemyBrain>();
            for (int i = 0; i < 2; i++)
            {
                var o = CombatFactory.Enemy(EnemyLib.Kkanjok(), HitResolver.YawDir(120f * (i + 1)) * 4f, 0f, pc.Me, dir2, i + 1, tune);
                others.Add(o);
            }
            e.NoAttack = false; e.HoldPosition = false;
            yield return Frames(3);
            foreach (var o in others) o.Activate();
            yield return Frames(30);
            int startsBefore = dir.Starts.Count;
            pc.Me.StartDown(Vector3.back, 0f);
            double t0 = 0, lieStart = -1, lieEnd = -1;
            int mashes = 0, grantsWhileDown = 0, attackingWhileDown = 0;
            for (int i = 0; i < 300; i++)
            {
                var s = pc.Me.State;
                if (s == Fighter.Phase.Lie && mashes < 10 && i % 3 == 0) { pc.Press(Btn.Light); mashes++; }
                yield return null;
                t0 += Time.deltaTime;
                s = pc.Me.State;
                if (lieStart < 0 && s == Fighter.Phase.Lie) lieStart = t0;
                if (lieStart > 0 && lieEnd < 0 && s == Fighter.Phase.GetUp) lieEnd = t0;
                if (pc.Me.Down) { attackingWhileDown = System.Math.Max(attackingWhileDown, dir.Attacking); }
                if (s == Fighter.Phase.Free && lieEnd > 0) break;
            }
            grantsWhileDown = dir.Starts.Count - startsBefore;
            float siwooLie = (float)(lieEnd - lieStart);
            // (3) 연타 없이 = 2.0초
            yield return Frames(60);
            pc.Me.ResetFighter();
            yield return Frames(5);
            pc.Me.StartDown(Vector3.back, 0f);
            double t1 = 0, l0 = -1, l1 = -1;
            for (int i = 0; i < 300; i++)
            {
                yield return null;
                t1 += Time.deltaTime;
                if (l0 < 0 && pc.Me.State == Fighter.Phase.Lie) l0 = t1;
                if (l0 > 0 && l1 < 0 && pc.Me.State == Fighter.Phase.GetUp) { l1 = t1; break; }
            }
            float siwooLie0 = (float)(l1 - l0);
            Debug.Log($"[M2Test] C13 다운: 적 쓰러짐 {fall:F2}초 · 누움 {lie:F2}초 · 기상 {getup:F2}초 → {brainAfter} · 다운 중 무적 {invulAll} · 누운 동안 잽 맞음 {hitsDuringDown} / " +
                      $"시우 누움(연타 {mashes}번) {siwooLie:F2}초 · 연타 없이 {siwooLie0:F2}초 · 다운 중 공격권 새로 {grantsWhileDown} · 공격 중 적 최대 {attackingWhileDown}");
            Assert.That(fall, Is.EqualTo(tune.DownFall).Within(Dt + 1e-3f), "적 쓰러짐 0.42초");
            Assert.That(lie, Is.EqualTo(1.0f).Within(Dt + 1e-3f), "적 누움 1.0초");
            Assert.That(getup, Is.EqualTo(0.85f).Within(Dt + 1e-3f), "적 기상 0.85초");
            Assert.AreEqual("Strafe", brainAfter, "기상 뒤 간보기");
            Assert.IsTrue(invulAll, "다운·기상 중 무적");
            Assert.AreEqual(0, hitsDuringDown, "누운 적은 안 맞음");
            Assert.That(siwooLie, Is.EqualTo(1.0f).Within(Dt + 1e-3f), "연타 10번 = 누움 1.0초");
            Assert.That(siwooLie0, Is.EqualTo(2.0f).Within(Dt + 1e-3f), "연타 없이 2.0초");
            Assert.AreEqual(0, grantsWhileDown, "다운된 시우에게 공격권 0");
        }

        // ───────────────────────── C12 유형
        [UnityTest]
        public IEnumerator C12_Enemy_Types()
        {
            Lab.Floor(0f, 60f);
            var (r, pc) = Siwoo(Vector3.zero, 0f);
            var dir = CombatFactory.Director(pc.Me, tune);
            var log = new List<string>();

            // (1) 석 달 막기 확률(4-5): 3타째(훅) 발생 시작 때 막을 수 있는 상태에서 100번 굴림 → 80~98 / 실제 □□□ 20판(1·2타가 맞으면 경직이라 못 막음 — 기록만)
            var sd = CombatFactory.Enemy(EnemyLib.Seokdal(), new Vector3(0f, 0f, 1.25f), 180f, pc.Me, dir, 0, tune);
            sd.NoAttack = true; sd.NoCounter = true; sd.HoldPosition = true;
            yield return Frames(5);
            sd.Activate();
            var hookRun = new AttackRun { Move = MoveSet.CreateDefault().Hook, Combo = 3 };
            var jabRun = new AttackRun { Move = MoveSet.CreateDefault().Jab, Combo = 1 };
            var upRun = new AttackRun { Move = MoveSet.CreateDefault().Upper, Combo = 1 };
            int blocked3 = 0, blocked1 = 0, blockedUp = 0;
            for (int k = 0; k < 100; k++) { if (sd.RollBlock(hookRun)) blocked3++; if (sd.RollBlock(jabRun)) blocked1++; if (sd.RollBlock(upRun)) blockedUp++; }
            int third = 0, live3 = 0;
            for (int k = 0; k < 20; k++)
            {
                pc.Me.ResetFighter(); sd.Me.ResetFighter();
                ((PlayerBody)pc.Me.Body).Place(Vector3.zero, 0f);
                sd.Me.Body.Place(new Vector3(0f, 0f, 1.25f), 180f);
                yield return Frames(45);
                hits.Clear();
                yield return RunGame(new[] { (0, Btn.Light), (8, Btn.Light), (20, Btn.Light) }, 50);
                var mine = hits.Where(h => h.Attacker == pc.Me && h.Victim == sd.Me).ToList();
                if (mine.Count >= 3) { third++; var o = mine[2].Outcome; if (o == HitOutcome.Blocked || o == HitOutcome.Crushed) live3++; }
            }
            log.Add($"석 달 막기 굴림 100번: 3·4타 {blocked3} · 1·2타 {blocked1} · 어퍼 {blockedUp} / 실제 □□□ {third}판 중 3타째 막음 {live3}(1·2타가 맞으면 경직이라 못 막음)");
            Assert.That(blocked3, Is.InRange(80, 98), "3타째 이후 막음 90% 근처(100번 중 80~98)");
            Assert.That(blocked1, Is.InRange(35, 65), "1·2타 50% 근처");
            Assert.AreEqual(0, blockedUp, "어퍼는 못 막음");
            Assert.That(live3, Is.GreaterThan(0), "실제 콤보에서도 막음");

            // (2) 막는 석 달에게 □△ → 어퍼 가드 브레이크(경직 48f)
            pc.Me.ResetFighter(); sd.Me.ResetFighter();
            ((PlayerBody)pc.Me.Body).Place(Vector3.zero, 0f);
            sd.Me.Body.Place(new Vector3(0f, 0f, 1.25f), 180f);
            yield return Frames(45);
            sd.ForceBlock(3f);
            hits.Clear();
            float brk = 0f;
            yield return RunGame(new[] { (0, Btn.Light), (5, Btn.Heavy) }, 60, g => { if (brk == 0f && hits.Any(h => h.Outcome == HitOutcome.GuardBroken)) brk = sd.Me.StaggerLeft; });
            var up = hits.LastOrDefault(h => h.Attacker == pc.Me && h.Victim == sd.Me);
            log.Add($"막는 석 달 □△: 잽 {hits.FirstOrDefault(h => h.Attacker == pc.Me).Outcome} → {up.Move?.Label} {up.Outcome} · 경직 {brk * 60f:F0}f");
            Assert.AreEqual(HitOutcome.GuardBroken, up.Outcome, "어퍼 = 가드 브레이크");
            Assert.That(brk * 60f, Is.EqualTo(48f).Within(1.5f), "가드 브레이크 경직 48f");
            sd.gameObject.SetActive(false);

            // (3) 냉장고: □ 따로 3번 = 경직 없음(게이지 30 → 12) / 3초 뒤 가득 → □□△ = 게이지 0 → 60f 경직
            var nj = CombatFactory.Enemy(EnemyLib.Naengjanggo(), new Vector3(0f, 0f, 1.3f), 180f, pc.Me, dir, 1, tune);
            nj.NoAttack = true; nj.HoldPosition = true;
            pc.Me.ResetFighter();
            ((PlayerBody)pc.Me.Body).Place(Vector3.zero, 0f);
            yield return Frames(5);
            nj.Activate();
            yield return Frames(30);
            hits.Clear();
            bool staggered = false;
            for (int k = 0; k < 3; k++)
            {
                yield return RunGame(new[] { (0, Btn.Light) }, 40, g => { if (nj.Me.State == Fighter.Phase.Stagger) staggered = true; });
            }
            var jabs = hits.Where(h => h.Victim == nj.Me).Select(h => h.Outcome).ToList();
            float gauge3 = nj.Me.ArmorGauge;
            yield return Frames(Mathf.RoundToInt(3.3f / Dt));
            float refilled = nj.Me.ArmorGauge;
            hits.Clear();
            float brkStag = 0f;
            yield return RunGame(new[] { (0, Btn.Light), (8, Btn.Light), (20, Btn.Heavy) }, 60, g =>
            {
                if (brkStag == 0f && nj.Me.ArmorBroken && nj.Me.State == Fighter.Phase.Stagger) brkStag = nj.Me.StaggerLeft;
            });
            var combo = hits.Where(h => h.Victim == nj.Me).Select(h => $"{h.Move.Label} {h.Outcome}").ToList();
            log.Add($"냉장고 □ 따로 3번: {string.Join("/", jabs)} · 경직 {staggered} · 게이지 {gauge3:F0} → 3초 뒤 {refilled:F0} / □□△: {string.Join(", ", combo)} · 깨짐 경직 {brkStag * 60f:F0}f");
            Assert.That(jabs, Is.EqualTo(new[] { HitOutcome.Armored, HitOutcome.Armored, HitOutcome.Armored }), "냉장고 약타 3번 = 슈퍼아머");
            Assert.IsFalse(staggered, "냉장고 경직 없음");
            Assert.That(gauge3, Is.EqualTo(12f).Within(0.01f), "게이지 30 − 6×3 = 12");
            Assert.That(refilled, Is.EqualTo(30f).Within(0.01f), "3초 뒤 가득");
            Assert.That(brkStag * 60f, Is.EqualTo(60f).Within(1.5f), "게이지 0 = 60f 경직");
            nj.gameObject.SetActive(false);

            // (4) 깐족이 HP 25% + 동료 모두 탈락 → 도주
            var dir2 = CombatFactory.Director(pc.Me, tune, "Director2");
            var kk = CombatFactory.Enemy(EnemyLib.Kkanjok(), new Vector3(0f, 0f, 3f), 180f, pc.Me, dir2, 0, tune);
            var a1 = CombatFactory.Enemy(EnemyLib.Seokdal(), new Vector3(3f, 0f, 3f), 180f, pc.Me, dir2, 1, tune);
            var a2 = CombatFactory.Enemy(EnemyLib.Naengjanggo(), new Vector3(-3f, 0f, 3f), 180f, pc.Me, dir2, 2, tune);
            foreach (var b in new[] { kk, a1, a2 }) { b.NoAttack = true; }
            pc.Me.ResetFighter();
            yield return Frames(5);
            foreach (var b in new[] { kk, a1, a2 }) b.Activate();
            yield return Frames(30);
            kk.Me.Hp = Mathf.FloorToInt(kk.Me.MaxHp * 0.25f);
            yield return Frames(30);
            string before = kk.State.ToString();
            a1.Me.Receive(pc.Me, Kill()); a2.Me.Receive(pc.Me, Kill());
            bool fled = false;
            for (int i = 0; i < 120; i++) { yield return null; if (kk.State == EnemyBrain.S.Flee) fled = true; }
            log.Add($"깐족이 HP {kk.Me.Hp}/{kk.Me.MaxHp}: 동료 있을 때 {before} → 동료 탈락 뒤 도주 {fled} · 결과 집계 탈락 {kk.Eliminated}");
            Debug.Log("[M2Test] C12 유형: " + string.Join(" / ", log));
            Assert.AreNotEqual("Flee", before, "동료가 있으면 도주 안 함");
            Assert.IsTrue(fled, "HP 25% + 동료 탈락 = 도주");
            Assert.IsTrue(kk.Eliminated, "도주 = 탈락으로 집계");
        }
    }
}
