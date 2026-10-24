// 행인1의 메인이벤트 — 체육관 드릴 테스트 G01~G14 (docs/12_체육관_설계.md 4-7) + G15 체육관 입장(Zone1 → Gym Additive, 봇 S → 시뮬)
// 클래스 이름 M4… = 입력 테스트(InputTestFixture)보다 뒤에(실제 HInput 이 켜지면 가짜 장치 입력이 막힘 — M3Lab 과 같은 규칙).
// 산식·보상·시뮬은 순수 함수. 스테이션은 DrillRunner.Step(고정 dt) 로 결정론 구동(G07·G11·G12), 몸은 허수아비 Fighter(Actors.PlayerFighter).
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Haengin.Tests
{
    public sealed class M4GymTests
    {
        static DrillBook Book()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<DrillBook>("Assets/_Project/Settings/DrillBook.asset");
#else
            return null;
#endif
        }

        static DrillDef Def(string id) { var b = Book(); if (b == null) Assert.Ignore("DrillBook 없음 — GymSetup.Build 먼저"); var d = b.Find(id); Assert.NotNull(d, id); return d; }

        /// 4지표 시험용 정의(가중치 .4/.3/.2/.1, 2 higher · 2 lower)
        static DrillDef Four() => new DrillDef
        {
            Id = "T4", NameKr = "시험", Rounds = 1, RoundSec = 10, FatigueCost = 10, SweatBase = 20, MainStat = Stat.Power, SubStat = Stat.Speed,
            Metrics = new[]
            {
                new MetricDef { Id = "a", HigherIsBetter = true, D = 0, S = 100, Weight = 0.4f },
                new MetricDef { Id = "b", HigherIsBetter = false, D = 200, S = 50, Weight = 0.3f },
                new MetricDef { Id = "c", HigherIsBetter = true, D = 10, S = 20, Weight = 0.2f },
                new MetricDef { Id = "d", HigherIsBetter = false, D = 8, S = 0, Weight = 0.1f },
            },
        };

        static DrillLog LogOf(DrillDef d, params double[] m)
        {
            var l = new DrillLog { DrillId = d.Id, Rounds = d.Rounds, RoundSec = d.RoundSec };
            for (int i = 0; i < m.Length; i++) { var md = d.Metrics[i]; double v = md.HigherIsBetter ? md.D + (md.S - md.D) * m[i] : md.D - (md.D - md.S) * m[i]; l.Metric(md.Id, v); }
            return l;
        }

        [SetUp] public void Up() { GymState.Current = new GymSave(); }

        // ───────────────────────── G01 결정론
        [Test]
        public void G01_Grader_Deterministic()
        {
            var d = Def("D04");
            var log = new DrillLog { DrillId = "D04", Rounds = 3, RoundSec = 50 };
            var rnd = new System.Random(7);
            for (int r = 1; r <= 3; r++) for (int i = 0; i < 30; i++)
                {
                    log.Add("timing", i * 1.5, rnd.Next(0, 200), r); log.Add("combo", i * 1.5, rnd.Next(0, 2), r);
                    if (i % 3 == 0) log.Add("react", i * 1.5, 300 + rnd.Next(0, 600), r);
                }
            var first = DrillGrader.Evaluate(d, log);
            for (int k = 0; k < 100; k++)
            {
                var r = DrillGrader.Evaluate(d, log.Clone());
                Assert.AreEqual(first.Score, r.Score, 0.0); Assert.AreEqual(first.Grade, r.Grade);
                for (int i = 0; i < first.Values.Length; i++) Assert.AreEqual(first.Values[i], r.Values[i], 0.0);
            }
            Debug.Log($"[G01] 100회 같음: {first}");
        }

        // ───────────────────────── G02 지표 점수
        [Test]
        public void G02_Metric_Scores()
        {
            var hi = new MetricDef { HigherIsBetter = true, D = 60, S = 97 };
            var lo = new MetricDef { HigherIsBetter = false, D = 120, S = 25 };
            Assert.AreEqual(0.0, hi.Score(60), 1e-9); Assert.AreEqual(1.0, hi.Score(97), 1e-9); Assert.AreEqual(0.0, hi.Score(10), 1e-9); Assert.AreEqual(1.0, hi.Score(150), 1e-9);
            Assert.AreEqual(0.0, lo.Score(120), 1e-9); Assert.AreEqual(1.0, lo.Score(25), 1e-9); Assert.AreEqual(0.0, lo.Score(500), 1e-9); Assert.AreEqual(1.0, lo.Score(0), 1e-9);
            Assert.AreEqual(0.5, hi.Score(78.5), 1e-9); Assert.AreEqual(0.5, lo.Score(72.5), 1e-9);
        }

        // ───────────────────────── G03 S 조건
        [Test]
        public void G03_S_Requires_Min075()
        {
            var d = Four();
            // 89.9: a=1(40) b=1(30) c=0.995(19.9) d=0 → 89.9 → A
            var r1 = DrillGrader.Evaluate(d, DrillMetrics.Compute(d, LogOf(d, 1, 1, 0.995, 0)));
            Assert.AreEqual(89.9, r1.Score, 1e-6); Assert.AreEqual(Grade.A, r1.Grade);
            // 90.0 모두 ≥ .75: a=.9 b=.9 c=.9 d=.9 → 90 → S
            var r2 = DrillGrader.Evaluate(d, DrillMetrics.Compute(d, LogOf(d, 0.9, 0.9, 0.9, 0.9)));
            Assert.AreEqual(90.0, r2.Score, 1e-6); Assert.AreEqual(Grade.S, r2.Grade);
            // 90.0 인데 한 지표 .74: a=1(40) b=1(30) c=.63(12.6) d=.74(7.4) → 90.0 → A
            var r3 = DrillGrader.Evaluate(d, DrillMetrics.Compute(d, LogOf(d, 1, 1, 0.63, 0.74)));
            Assert.AreEqual(90.0, r3.Score, 1e-6); Assert.AreEqual(Grade.A, r3.Grade);
        }

        // ───────────────────────── G04 등급 경계
        [Test]
        public void G04_Grade_Boundaries()
        {
            Assert.AreEqual(Grade.D, DrillGrader.GradeOf(44.9, 1)); Assert.AreEqual(Grade.C, DrillGrader.GradeOf(45.0, 1));
            Assert.AreEqual(Grade.C, DrillGrader.GradeOf(59.9, 1)); Assert.AreEqual(Grade.B, DrillGrader.GradeOf(60.0, 1));
            Assert.AreEqual(Grade.B, DrillGrader.GradeOf(74.9, 1)); Assert.AreEqual(Grade.A, DrillGrader.GradeOf(75.0, 1));
            Assert.AreEqual(Grade.A, DrillGrader.GradeOf(89.9, 1)); Assert.AreEqual(Grade.S, DrillGrader.GradeOf(90.0, 0.75)); Assert.AreEqual(Grade.A, DrillGrader.GradeOf(95.0, 0.74));
        }

        // ───────────────────────── G05 보상표
        [Test]
        public void G05_Rewards_Table()
        {
            var d = Four(); var save = new GymSave();
            (float sweat, int main, int sub, int rel)[] table = { (0.4f, 0, 0, 0), (0.7f, 1, 0, 0), (1.0f, 2, 1, 0), (1.35f, 3, 1, 1), (1.8f, 4, 2, 2) };
            for (int g = 0; g < 5; g++)
            {
                var r = DrillRewards.Compute(d, (Grade)g, false, save, false);
                Assert.AreEqual(Mathf.RoundToInt(20 * table[g].sweat), r.Sweat, $"땀 {(Grade)g}"); Assert.AreEqual(table[g].main, r.MainDelta); Assert.AreEqual(table[g].sub, r.SubDelta); Assert.AreEqual(table[g].rel, r.Relation); Assert.AreEqual(10, r.Fatigue);
            }
            var sim = DrillRewards.Compute(d, Grade.S, true, save, false);
            Assert.AreEqual(27, sim.Sweat); Assert.AreEqual(3, sim.MainDelta); Assert.AreEqual(1, sim.SubDelta); Assert.AreEqual(0, sim.Relation);
            // 피로 70 → ×0.6
            save.Fatigue = 70;
            var tired = DrillRewards.Compute(d, Grade.S, false, save, false);
            Assert.AreEqual(Mathf.RoundToInt(36 * 0.6f), tired.Sweat); Assert.AreEqual(Mathf.RoundToInt(4 * 0.6f), tired.MainDelta); Assert.AreEqual(0.6f, tired.Efficiency, 1e-6);
            // 첫 S 보너스 1회만
            save = new GymSave();
            var res = DrillGrader.Evaluate(d, DrillMetrics.Compute(d, LogOf(d, 1, 1, 1, 1)));
            var a1 = DrillRewards.Apply(d, res, false, save); var a2 = DrillRewards.Apply(d, res, false, save);
            Assert.IsTrue(a1.FirstS); Assert.AreEqual(36 + 50, a1.Sweat); Assert.IsFalse(a2.FirstS); Assert.AreEqual(36, a2.Sweat);
            Assert.AreEqual(2, save.Of("T4").SCount); Assert.IsTrue(save.Of("T4").SimUnlocked);
            Assert.AreEqual(20 + 8, save.Stat(Stat.Power)); Assert.AreEqual(20 + 4, save.Stat(Stat.Speed)); Assert.AreEqual(20, save.Fatigue);
        }

        // ───────────────────────── G06 시뮬 규칙
        [Test]
        public void G06_Sim_Rules()
        {
            var d = Four(); var save = new GymSave(); int today = GymSave.DayKey(3, 10);
            StringAssert.Contains("S 를", DrillSim.Blocked(d, save, today));
            var sRes = DrillGrader.Evaluate(d, DrillMetrics.Compute(d, LogOf(d, 1, 1, 1, 1)));
            DrillRewards.Apply(d, sRes, false, save);
            Assert.IsTrue(DrillSim.CanSim(d, save, today));
            DrillSim.Run(d, save, today); DrillSim.Run(d, save, today);
            StringAssert.Contains("오늘 시뮬", DrillSim.Blocked(d, save, today));
            Assert.IsTrue(DrillSim.CanSim(d, save, DrillSim.NextDay(today)), "다음 날은 다시 2회");
            // 피로 60 거부
            var s2 = new GymSave(); DrillRewards.Apply(d, sRes, false, s2); s2.Fatigue = 60;
            StringAssert.Contains("피로", DrillSim.Blocked(d, s2, today));
            s2.Fatigue = 59; Assert.IsTrue(DrillSim.CanSim(d, s2, today));
            // 8회마다 검정: 하루 2회씩 4일 → 8회 → 검정 필요
            var s3 = new GymSave(); DrillRewards.Apply(d, sRes, false, s3);
            int day = today;
            for (int i = 0; i < 8; i++) { if (i % 2 == 0 && i > 0) { day = DrillSim.NextDay(day); s3.Fatigue = 0; } Assert.IsTrue(DrillSim.CanSim(d, s3, day), $"시뮬 {i + 1}회째 day {day}: {DrillSim.Blocked(d, s3, day)}"); DrillSim.Run(d, s3, day); }   // 밤에 집에서 회복(피로 0)
            Assert.IsTrue(s3.Of("T4").CertDue); StringAssert.Contains("검정", DrillSim.Blocked(d, s3, day));
            // 검정 B 이상 → 유지 / C → 잠김
            var bRes = DrillGrader.Evaluate(d, DrillMetrics.Compute(d, LogOf(d, 0.6, 0.6, 0.6, 0.6)));
            Assert.AreEqual(Grade.B, bRes.Grade);
            DrillRewards.Apply(d, bRes, false, s3);
            Assert.IsTrue(s3.Of("T4").SimUnlocked); Assert.AreEqual(0, s3.Of("T4").SimRunsSinceCert);
            for (int i = 0; i < 8; i++) { s3.Of("T4").SimRunsSinceCert++; }
            var cRes = DrillGrader.Evaluate(d, DrillMetrics.Compute(d, LogOf(d, 0.5, 0.5, 0.5, 0.5)));
            Assert.AreEqual(Grade.C, cRes.Grade);
            DrillRewards.Apply(d, cRes, false, s3);
            Assert.IsFalse(s3.Of("T4").SimUnlocked, "검정 B 미만 → 시뮬 잠김");
            // D11 은 시뮬 불가
            var d11 = Def("D11"); var s4 = new GymSave(); s4.Of("D11").SimUnlocked = true;
            StringAssert.Contains("스파링", DrillSim.Blocked(d11, s4, today));
        }

        // ───────────────────────── 스테이션 구동 도우미
        sealed class Harness
        {
            public GameObject Go; public DrillRunner Runner; public GymActors Actors; public Fighter Me, Foe;
            public Harness(bool withBody)
            {
                Go = new GameObject("DrillHarness");
                Runner = Go.AddComponent<DrillRunner>();
                Actors = new GymActors { Say = (w, l) => { }, Toast = s => { } };
                if (withBody)
                {
                    Me = CombatFactory.Dummy("시우(시험)", new Vector3(0f, 0f, 0f), 0f, 200, null, 1.74f, 0.3f, null, 0);
                    Foe = CombatFactory.Dummy("고준(시험)", new Vector3(0f, 0f, 1.2f), 180f, 200, null, 1.76f, 0.3f, null, 1);
                    Actors.PlayerFighter = Me;
                    Actors.Station = Go.transform;
                }
            }
            public void Dispose() { if (Me != null) UnityEngine.Object.Destroy(Me.gameObject); if (Foe != null) UnityEngine.Object.Destroy(Foe.gameObject); UnityEngine.Object.Destroy(Go); }
            /// 라운드 1 시각 t 까지 걸음(dt 고정)
            public void StepTo(double t, float dt)
            {
                int guard = 0;
                while ((Runner.State != DrillRunner.Phase.Round || Runner.T < t - 1e-9) && guard++ < 200000)
                {
                    if (Runner.State == DrillRunner.Phase.Idle || Runner.State == DrillRunner.Phase.Result) return;
                    Runner.Step(dt);
                }
            }
            public void ToRound(float dt) { int g = 0; while (Runner.State != DrillRunner.Phase.Round && g++ < 100000) Runner.Step(dt); }
            public void Finish(float dt) { int g = 0; while (Runner.State != DrillRunner.Phase.Result && g++ < 400000) Runner.Step(dt); }
        }

        /// 미트: 호출 뒤 첫 토큰을 (기대 시각 − 발생) + delayMs 에 넣으면 timing = delayMs
        static double MittRun(float dt, double delayMs, out DrillLog log)
        {
            var h = new Harness(false);
            try
            {
                var d = Def("D04");
                var st = new ShadowStation(true);
                h.Runner.Begin(d, st, h.Actors);
                h.ToRound(dt);
                int n = 0;
                while (n++ < 400000 && h.Runner.State == DrillRunner.Phase.Round && h.Runner.Round == 1)
                {
                    if (st.Combo != null && !st.Combo.Done && st.Combo.Next == 0)
                    {
                        var tok = st.Combo.Tokens[0];
                        double at = st.Combo.CalledAt + ShadowStation.MittLead - ComboLib.ActiveStart(tok) + delayMs / 1000.0;
                        h.StepTo(at, dt);
                        h.Runner.Inject(tok.Btn, at, tok.Hold, tok.StickDown);
                        // 나머지 토큰은 창 열릴 때 정확히
                        while (!st.Combo.Done && n++ < 400000)
                        {
                            st.Combo.Window(out double open, out _);
                            var want = st.Combo.Tokens[st.Combo.Next];
                            h.StepTo(open, dt);
                            h.Runner.Inject(want.Btn, open, want.Hold, want.StickDown);
                        }
                        break;
                    }
                    h.Runner.Step(dt);
                }
                log = h.Runner.Log.Clone();
                double first = log.Of("timing").First().Value;
                h.Runner.Abort();
                return first;
            }
            finally { h.Dispose(); }
        }

        // ───────────────────────── G07 미트 타이밍
        [Test]
        public void G07_Mitt_Timing()
        {
            var d = Def("D04");
            double t0 = MittRun(1f / 60f, 0, out var log0);
            Assert.AreEqual(0.0, t0, 0.5, "정확 타이밍 → 오차 0");
            var m0 = d.Metrics[d.IndexOf("timing")];
            Assert.AreEqual(1.0, m0.Score(t0), 1e-3);
            double t1 = MittRun(1f / 60f, 180, out _);
            Assert.AreEqual(180.0, t1, 0.5, "180ms 지연");
            Assert.AreEqual(0.0, m0.Score(t1), 1e-3);
            Debug.Log($"[G07] 미트 오차 0 → {t0:F2}ms(m={m0.Score(t0):F2}) · 180 지연 → {t1:F2}ms(m={m0.Score(t1):F2}) · 콤비 샘플 {log0.Count("combo")}");
        }

        // ───────────────────────── G08 콤비 정확도 = M2 연결 창
        [Test]
        public void G08_Combo_LinkWindow()
        {
            var lib = ComboLib.Tokens(null);
            var jab = lib['1'].Move;
            // 잽 → 크로스: 연결 창 안(LinkAt) → 맞음
            var ok = new ComboRun("12", lib, 0);
            ok.Feed(DrillBtn.Light, false, false, 0.2, out bool c1, out double s1); Assert.IsTrue(c1);
            ok.Window(out double open, out double close);
            Assert.AreEqual(s1 + jab.LinkAt, open, 1e-9); Assert.AreEqual(s1 + jab.Total + ComboLib.ComboReset, close, 1e-9);
            ok.Feed(DrillBtn.Heavy, false, false, open + 0.01, out bool c2, out _); Assert.IsTrue(c2, "연결 창 안 크로스");
            // 창 밖(늦게) → 틀림
            var late = new ComboRun("12", lib, 0);
            late.Feed(DrillBtn.Light, false, false, 0.2, out _, out _);
            late.Window(out _, out double close2);
            var tok = late.Feed(DrillBtn.Heavy, false, false, close2 + 0.05, out bool c3, out _);
            Assert.NotNull(tok); Assert.IsFalse(c3, "연결 창 지나서 넣은 크로스 = 틀림");
            // 버퍼(12f) 안 이른 입력은 창 열릴 때 나감, 그보다 이르면 무시
            var early = new ComboRun("12", lib, 0);
            early.Feed(DrillBtn.Light, false, false, 0.2, out _, out _);
            early.Window(out double open3, out _);
            Assert.IsNull(early.Feed(DrillBtn.Heavy, false, false, open3 - ComboLib.Buffer - 0.05, out _, out _), "버퍼보다 이른 입력은 무시");
            early.Feed(DrillBtn.Heavy, false, false, open3 - 0.1, out bool c4, out double s4); Assert.IsTrue(c4); Assert.AreEqual(open3, s4, 1e-9);
            // 잘못된 버튼 → 틀림, 빠뜨림(Expire) → 틀림
            var wrong = new ComboRun("12", lib, 0);
            wrong.Feed(DrillBtn.Heavy, false, false, 0.2, out bool c5, out _); Assert.IsFalse(c5);
            Assert.AreEqual(1, wrong.Expire(100.0));
            Assert.IsTrue(wrong.Done); Assert.AreEqual(0, wrong.Correct);
        }

        // ───────────────────────── G09 스파링 스톱·뇌진탕
        [Test]
        public void G09_Spar_Stop_And_Concussion()
        {
            var h = new Harness(true);
            try
            {
                var d = Def("D11");
                var st = new SparStation();
                h.Runner.Begin(d, st, h.Actors);
                Assert.IsTrue(st.ObserveOnly);
                h.ToRound(1f / 60f);
                Assert.AreEqual(1, h.Runner.Round);
                var upper = MoveLib.Upper();
                Assert.IsTrue(upper.Power >= Power.Mid && upper.Flinch != FlinchKind.Body, "어퍼 = 머리 강타(위력 중 이상)");
                st.Observe(new HitEvent { Attacker = h.Me, Victim = h.Foe, Move = upper, Outcome = HitOutcome.Hit, Power = upper.Power });
                Assert.AreEqual(1, st.Violations); Assert.IsTrue(st.EndRoundNow);
                h.Runner.Step(1f / 60f);
                Assert.AreEqual(DrillRunner.Phase.Rest, h.Runner.State, "스톱 → 라운드 즉시 종료");
                Assert.AreEqual(1, h.Runner.Log.Count("violation"));
                h.ToRound(1f / 60f);
                Assert.AreEqual(2, h.Runner.Round);
                // 시우 머리 피격 중 → 뇌진탕 의심: 드릴 종료 · 피로 +20 · D09·D11 잠김
                int fatigue = GymState.Current.Fatigue;
                var cross = MoveLib.Cross();
                st.Observe(new HitEvent { Attacker = h.Foe, Victim = h.Me, Move = cross, Outcome = HitOutcome.Hit, Power = cross.Power });
                Assert.IsTrue(st.EndDrillNow);
                h.Runner.Step(1f / 60f);
                Assert.AreEqual(DrillRunner.Phase.Result, h.Runner.State);
                Assert.IsTrue(h.Runner.Result.Concussion);
                Assert.AreEqual(fatigue + d.FatigueCost + 20, GymState.Current.Fatigue, "피로 +20(+드릴 비용)");
                int today = GymSave.Today;
                Assert.IsTrue(GymState.Current.Injured(today));
                StringAssert.Contains("부상", DrillSim.DirectBlocked(Def("D09"), GymState.Current, today));
                StringAssert.Contains("부상", DrillSim.DirectBlocked(d, GymState.Current, today));
                Assert.AreEqual("", DrillSim.DirectBlocked(Def("D01"), GymState.Current, today), "머리 아닌 드릴은 됨");
                Assert.AreEqual("", DrillSim.DirectBlocked(d, GymState.Current, DrillSim.NextDay(today)), "다음 날 풀림");
                Debug.Log($"[G09] {h.Runner.Result} · {GymState.Current.Summary}");
            }
            finally { h.Dispose(); }
        }

        // ───────────────────────── G10 샌드백: 돌아올 때 ×1.3 · 가드 복귀 1초
        [Test]
        public void G10_Bag_ReturnBonus_And_Guard()
        {
            Assert.AreEqual(2.0f, BagStation.PowerOf(Power.Mid, false, false, false), 1e-6);
            Assert.AreEqual(2.0f * 1.3f, BagStation.PowerOf(Power.Mid, false, false, true), 1e-6);
            Assert.AreEqual(3.0f * 1.3f, BagStation.PowerOf(Power.Mid, true, true, true), 1e-6);
            var h = new Harness(false);
            try
            {
                var d = Def("D05");
                var st = new BagStation();
                h.Runner.Begin(d, st, h.Actors);
                h.ToRound(1f / 60f);
                h.StepTo(2.0, 1f / 60f);
                st.RegisterHit(Power.Light, Vector3.forward, false);
                h.StepTo(2.5, 1f / 60f);
                h.Runner.InjectGuard(true); h.Runner.Step(1f / 60f); h.Runner.InjectGuard(false);
                Assert.AreEqual(1, h.Runner.Log.Count("guard")); Assert.AreEqual(1.0, h.Runner.Log.Of("guard").First().Value, 1e-9, "1초 안 가드 = 복귀");
                h.StepTo(5.0, 1f / 60f);
                st.RegisterHit(Power.Light, Vector3.forward, true);
                Assert.AreEqual(BagStation.PowerOf(Power.Light, false, true, true), (float)h.Runner.Log.Of("power").Last().Value, 1e-3, "돌아올 때 ×1.3(리듬 입력이 없으면 오차 0 = 리듬 안)");
                Assert.AreEqual(BagStation.PowerOf(Power.Light, false, true, false), (float)h.Runner.Log.Of("power").First().Value, 1e-3);
                h.StepTo(6.2, 1f / 60f);
                Assert.AreEqual(2, h.Runner.Log.Count("guard")); Assert.AreEqual(0.0, h.Runner.Log.Of("guard").Last().Value, 1e-9, "1초 넘김 = 복귀 못함");
                var vals = DrillMetrics.Compute(d, h.Runner.Log);
                Assert.AreEqual(50.0, vals[d.IndexOf("guard_rate")], 1e-6);
                h.Runner.Abort();
            }
            finally { h.Dispose(); }
            // BagSwing: 맞은 뒤 흔들리다 돌아오는 구간이 생긴다
            var go = new GameObject("bag"); var sw = go.AddComponent<BagSwing>(); sw.Length = 2.1f;
            sw.Hit(Vector3.forward, 2f);
            bool returned = false; float maxAng = 0f;
            Time.captureDeltaTime = 1f / 60f;
            try
            {
                var m = typeof(BagSwing).GetMethod("Update", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                for (int i = 0; i < 240 && !returned; i++) { m.Invoke(sw, null); maxAng = Mathf.Max(maxAng, sw.Angle); if (sw.Returning) returned = true; }
            }
            finally { Time.captureDeltaTime = 0f; UnityEngine.Object.DestroyImmediate(go); }
            Debug.Log($"[G10] 백 최대 각 {maxAng:F1}° · 돌아옴 {returned}");
        }

        // ───────────────────────── G11 스프롤: 페이크·창 밖
        [Test]
        public void G11_Sprawl_Fake_And_Window()
        {
            var h = new Harness(false);
            try
            {
                var d = Def("D07");
                var st = new SprawlStation();
                h.Runner.Begin(d, st, h.Actors);
                h.ToRound(1f / 60f);
                // 첫 진짜 샷에 0.9초 뒤 반응 → 창 밖(0.7) → 반응 실패 700 · 자세 0
                int guard = 0; bool realDone = false, fakeDone = false;
                while (!(realDone && fakeDone) && guard++ < 100000 && h.Runner.State == DrillRunner.Phase.Round)
                {
                    h.Runner.Step(1f / 60f);
                    if (st.Shots.Count == 0) continue;
                    var s = st.Shots[st.Shots.Count - 1];
                    if (!s.Fake && !realDone && h.Runner.T >= s.T + 0.75)
                    {
                        // 0.7초 창이 닫혀 Tick 이 '반응 700 · 자세 0' 을 적었다 → 뒤늦은 스프롤은 무시(샘플 수 그대로)
                        Assert.IsTrue(s.Reacted, "창 닫힘 처리");
                        int before = h.Runner.Log.Count("react"), beforeP = h.Runner.Log.Count("posture");
                        h.Runner.Inject(DrillBtn.Dodge, h.Runner.T, false, true);
                        Assert.AreEqual(before, h.Runner.Log.Count("react"), "0.7초 지난 입력은 무시");
                        Assert.AreEqual(beforeP, h.Runner.Log.Count("posture"));
                        realDone = true;
                    }
                    if (s.Fake && !fakeDone && !s.Reacted && h.Runner.T >= s.T + 0.2)
                    {
                        h.Runner.Inject(DrillBtn.Dodge, h.Runner.T, false, true);
                        Assert.AreEqual(1, h.Runner.Log.Count("fake_fail"), "페이크에 스프롤 = 실수");
                        fakeDone = true;
                    }
                }
                Assert.IsTrue(realDone && fakeDone, $"진짜 {realDone} · 페이크 {fakeDone} (샷 {st.Shots.Count})");
                Assert.IsTrue(h.Runner.Log.Of("react").Any(s => Math.Abs(s.Value - 700) < 1e-6), "창 밖 → 반응 700ms(실패)");
                Assert.IsTrue(h.Runner.Log.Of("posture").Any(s => s.Value == 0), "창 밖 → 자세 0");
                h.Runner.Abort();
            }
            finally { h.Dispose(); }
        }

        // ───────────────────────── G12 프레임레이트 독립
        [Test]
        public void G12_FrameRate_Independent()
        {
            double a = MittRun(1f / 30f, 90, out var la), b = MittRun(1f / 60f, 90, out var lb), c = MittRun(1f / 120f, 90, out var lc);
            Assert.AreEqual(a, b, 1.0); Assert.AreEqual(b, c, 1.0);
            var d = Def("D04");
            var ra = DrillGrader.Evaluate(d, la); var rb = DrillGrader.Evaluate(d, lb); var rc = DrillGrader.Evaluate(d, lc);
            Assert.AreEqual(ra.Values[0], rb.Values[0], 1.0); Assert.AreEqual(rb.Values[0], rc.Values[0], 1.0);
            Debug.Log($"[G12] 30/60/120fps 미트 오차 {a:F2}/{b:F2}/{c:F2} ms");
        }

        // ───────────────────────── G13 중단·완주
        [Test]
        public void G13_Abort_Vs_Complete()
        {
            var d = Four(); var save = new GymSave();
            var res = DrillGrader.Evaluate(d, DrillMetrics.Compute(d, LogOf(d, 0.8, 0.8, 0.8, 0.8))); res.Aborted = true;
            var r = DrillRewards.Apply(d, res, false, save);
            Assert.AreEqual(0, r.Sweat); Assert.AreEqual(0, r.MainDelta); Assert.AreEqual(5, r.Fatigue); Assert.AreEqual(5, save.Fatigue); Assert.AreEqual(0, save.Of("T4").Plays);
            var done = DrillGrader.Evaluate(d, DrillMetrics.Compute(d, LogOf(d, 0.8, 0.8, 0.8, 0.8)));
            Assert.AreEqual(Grade.A, done.Grade);
            var r2 = DrillRewards.Apply(d, done, false, save);
            Assert.AreEqual(27, r2.Sweat); Assert.AreEqual(3, r2.MainDelta); Assert.AreEqual(1, r2.SubDelta); Assert.AreEqual(10, r2.Fatigue); Assert.AreEqual(15, save.Fatigue); Assert.AreEqual((int)Grade.A, save.Of("T4").Best);
        }

        // ───────────────────────── G14 루틴(2부)
        [Test]
        public void G14_Routine()
        {
            var a = Four(); var b = Four(); b.Id = "T4b"; b.MainStat = Stat.Stamina; var c = Four(); c.Id = "T4c"; c.MainStat = Stat.Speed;
            var save = new GymSave(); int today = GymSave.DayKey(4, 1);
            var s = DrillGrader.Evaluate(a, DrillMetrics.Compute(a, LogOf(a, 1, 1, 1, 1)));
            foreach (var d in new[] { a, b, c }) DrillRewards.Apply(d, s, false, save);
            int fat = save.Fatigue, sweat = save.Sweat, pw = save.Stat(Stat.Power);
            var r = DrillSim.Routine(new[] { a, b, c }, save, today, out string why);
            Assert.IsTrue(r.HasValue, why);
            Assert.AreEqual(Mathf.RoundToInt(27 * 0.5f) * 3, r.Value.Sweat); Assert.AreEqual(Mathf.RoundToInt(30 * 0.8f), r.Value.Fatigue);
            Assert.AreEqual(fat + 24, save.Fatigue); Assert.AreEqual(sweat + 42, save.Sweat); Assert.AreEqual(pw + Mathf.RoundToInt(3 * 0.5f), save.Stat(Stat.Power));
            Assert.IsNull(DrillSim.Routine(new[] { a }, save, today, out why)); StringAssert.Contains("오늘", why);
            Assert.IsNull(DrillSim.Routine(new[] { a, b, c, a }, save, DrillSim.NextDay(today), out why)); StringAssert.Contains("1~3", why);
        }

        // ───────────────────────── G15 체육관 입장(Zone1 → Gym) · 봇 S → 시뮬
        [UnityTearDown] public IEnumerator Down() { yield return M3Lab.TearDown(); }

        [UnityTest, Timeout(900000)]
        public IEnumerator G15_Gym_Enter_Bot_S_Sim()
        {
            yield return M3Lab.OpenZone1();
            var r = M3Lab.Runner;
            GymState.Current = new GymSave();
            yield return r.Loader.Go(SceneLoader.Gym, "Spawn", false, Vector3.zero, 0f, false);
            yield return null; yield return null;
            var gym = GymManager.Instance;
            Assert.NotNull(gym, "체육관 GymManager — GymSetup.Build 를 돌릴 것");
            Assert.IsTrue(r.Loader.Indoor); Assert.AreEqual(3.0f, r.Loader.ExpectedCamDistance, 0.01f, "실내 카메라 거리 3.0");
            Assert.GreaterOrEqual(gym.Markers.Count, 14, "드릴 자리 마커");
            Assert.GreaterOrEqual(gym.Lights.Fluors.Count, 14, "형광등");
            Assert.IsTrue(gym.Book.PlayableCount >= 10, $"플레이 가능 드릴 {gym.Book.PlayableCount} ≥ 10");
            // 시간대 4종 적용 — 형광등 수
            var lit = new List<string>();
            foreach (var p in new[] { GymPart.Morning, GymPart.Day, GymPart.Evening, GymPart.Night }) { gym.ApplyPart(p); lit.Add($"{GymLights.Name(p)} {gym.Lights.LitCount}"); }
            gym.ApplyPart(GymPart.Evening);
            int evening = gym.Members.Count(m => m.gameObject.activeSelf);
            Assert.GreaterOrEqual(evening, 6, "저녁 피크 관원 6 이상");
            // 시우 발이 바닥(계단 위) · 자유 낙하 없음
            yield return Lab.Frames(30);
            var feet = M3Lab.Motor.Position; var spawn = gym.transform.TransformPoint(new Vector3(0.9f, 3.42f, 13.4f));
            Assert.Less(Mathf.Abs(feet.y - spawn.y), 0.3f, $"스폰 발 높이 {feet.y:F2} vs {spawn.y:F2}");
            // D01 봇 S → 시뮬 해금 → 시뮬 1회
            gym.SimMontageSec = 0.9f;
            DrillResult res = null; DrillReward rew = default;
            gym.DrillFinished += (d, rr, w) => { res = rr; rew = w; };
            yield return gym.PlayBot("D01", 1f, 60 * 240);
            Assert.NotNull(res, "D01 결과");
            Assert.AreEqual(Grade.S, res.Grade, res.ToString());
            Assert.IsTrue(rew.FirstS); Assert.IsTrue(GymState.Current.Of("D01").SimUnlocked);
            Assert.IsFalse(gym.Busy, "결과 카드 닫힘");
            int sweat = GymState.Current.Sweat;
            yield return gym.SimCo(gym.Book.Find("D01"));
            for (int i = 0; i < 300 && gym.Busy; i++) yield return null;
            Assert.AreEqual(1, GymState.Current.SimToday); Assert.Greater(GymState.Current.Sweat, sweat);
            // 자리 드릴은 시작 안 됨, 피로 85 는 막힘
            Assert.IsFalse(gym.StartDrill("D12", 1f), "D12 자리만");
            GymState.Current.Fatigue = 85;
            Assert.IsFalse(gym.StartDrill("D01", 1f), "피로 85 → 이윤준이 막음");
            Debug.Log($"[G15] 체육관: 조명 [{string.Join(" · ", lit)}] · 저녁 관원 {evening} · D01 {res} · {rew} · 시뮬 뒤 {GymState.Current.Summary}");
            GymState.Current.Fatigue = 0;
        }

        // ───────────────────────── G16 체육관 안 샌드백·미트: M2 판정으로 실제 타격이 집계됨
        [UnityTest, Timeout(900000)]
        public IEnumerator G16_Gym_Bag_And_Mitt_Hits()
        {
            yield return M3Lab.OpenZone1();
            var r = M3Lab.Runner;
            GymState.Current = new GymSave();
            yield return r.Loader.Go(SceneLoader.Gym, "Floor", false, Vector3.zero, 0f, false);
            yield return null; yield return null;
            var gym = GymManager.Instance;
            Assert.NotNull(gym);
            var book = UnityEngine.Object.Instantiate(gym.Book);
            foreach (var d in book.Drills) { d.RoundSec = 12f; d.RestSec = 1f; d.Rounds = 1; }
            gym.Book = book;
            DrillResult res = null;
            gym.DrillFinished += (d, rr, w) => res = rr;
            yield return gym.PlayBot("D05", 1f, 60 * 120);
            Assert.NotNull(res);
            int hits = M4Log(res);
            Assert.GreaterOrEqual(res.Values[res.Def.IndexOf("hits_per_round")], 10.0, "샌드백 라운드 12초 봇: 타격 10 이상 " + res);
            res = null;
            GymState.Current.Fatigue = 0;
            yield return gym.PlayBot("D04", 1f, 60 * 120);
            Assert.NotNull(res);
            Assert.GreaterOrEqual(res.Scores[res.Def.IndexOf("timing")], 0.9, "미트 타이밍 " + res);
            Assert.AreEqual(100.0, res.Values[res.Def.IndexOf("combo_acc")], 0.5, "콤비 정확 " + res);
            Debug.Log($"[G16] 샌드백·미트: {res}");
        }
        static int M4Log(DrillResult r) { Debug.Log("[G16] " + r); return 0; }
    }
}
