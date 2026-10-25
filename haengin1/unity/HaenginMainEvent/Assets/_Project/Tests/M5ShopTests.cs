// 행인1의 메인이벤트 — 돈 쓸 곳 시험 W17~W22(docs/13 14-6, 순수 C#): 레슨 예약 · 노쇼 · 효과 지속 · 섭취 한도 · 체감 감소 · 영양제 꾸준히 + 레슨 효과(판정 보정·기술·교정).
// 장면 시험(체육관 레슨 진행·가게 앞 판매창)은 M5WorldPlayTests W23·W24.
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Haengin.Tests
{
    public sealed class M5ShopTests
    {
        static WorldDef Def() => M5WorldTests.Def();
        static void Day(int key, int minutes) => GameClock.SetDate(GameClock.Year, key / 100, key % 100, minutes);

        static WorldModel Rich(WorldDef def, int wallet = 600000)
        {
            var m = M5WorldTests.Fresh(def);
            m.L.SetWallet(wallet);
            m.Unlock("lesson");
            return m;
        }

        [SetUp] public void Up() { GymState.Current = new GymSave(); }
        [TearDown] public void Down() { GameClock.SetDate(GameClock.DefaultYear, 3, 3, 5 * 60 + 31); }

        // ───────────────────────── W17 레슨 예약
        [Test]
        public void W17_Lesson_Booking()
        {
            var def = Def();
            Assert.IsNotNull(def.Shop, "WorldDef.Shop");
            Assert.AreEqual(5, def.Shop.Coaches.Length, "코치 5(이윤준·고준·출장 3)");
            Assert.Greater(def.Shop.Lines.Length, 40, "대사 tsv");
            var m = Rich(def);
            var yj = def.Shop.Coach("yunjun"); var gj = def.Shop.Coach("gojun"); var sy = def.Shop.Coach("seungyeon");
            Assert.IsFalse(m.CoachOpen(sy), "출장 코치는 '형들 불렀다'(C1-L1) 뒤");
            Day(309, 6 * 60);     // 3/9 월 06:00
            var slots = m.LessonSlots(yj);
            Assert.AreEqual((309, 7 * 60 + 10), (slots[0].day, slots[0].at), "월 07:10 아침 PT 가 첫 칸");
            int w0 = m.L.Wallet;
            Assert.AreEqual("", m.Book(yj, 309, 7 * 60 + 10, false));
            Assert.AreEqual(w0 - yj.Price, m.L.Wallet, "1회 50,000(학생 반값 — 정가 100,000)");
            Assert.AreEqual("", m.Book(yj, 310, 21 * 60 + 15, false), "화 21:15 밤 개인 지도");
            StringAssert.Contains("안 된다", m.Book(yj, 311, 7 * 60 + 10, false), "주 2회 상한");
            Assert.IsTrue(m.LessonSlots(yj, 14).Any(s => s.day == 316), "다음 주는 다시 열림");
            // 10회권: 회당 45,000 · 예약은 회권에서
            Assert.AreEqual("", m.BuyTicket(yj));
            Assert.AreEqual(10, m.Tickets("yunjun"));
            Assert.AreEqual("", m.Book(yj, 316, 7 * 60 + 10, true));
            Assert.AreEqual(9, m.Tickets("yunjun")); Assert.AreEqual(0, m.S.Lessons.Last().Paid);
            // 고준 20,000 · 출장 코치 열림
            Assert.AreEqual(20000, gj.Price);
            m.Unlock("lesson.guests");
            Assert.IsTrue(m.CoachOpen(sy));
            Assert.AreEqual(def.Balance.FixedTotal(3), def.Balance.FixedTotal(3));
            // 지금 받을 레슨 창: 시작 15분 전 ~ 20분 뒤
            Day(309, 6 * 60 + 50); Assert.IsNull(m.LessonNow(), "06:50 은 아직");
            Day(309, 7 * 60); Assert.IsNotNull(m.LessonNow(), "06:55~07:30");
            // 돈이 모자라면 예약 안 됨
            var poor = Rich(def, 30000); Day(309, 6 * 60);
            StringAssert.Contains("모자란다", poor.Book(yj, 309, 7 * 60 + 10, false));
            Debug.Log($"[W] W17 레슨 예약: 이윤준 {yj.Price}/{yj.Ticket10} · 고준 {gj.Price} · 출장 {sy.Price} · 칸 {slots.Count} · 문자 {m.S.Phone.Count}");
        }

        // ───────────────────────── W18 노쇼
        [Test]
        public void W18_Lesson_NoShow()
        {
            var def = Def();
            var m = Rich(def);
            var yj = def.Shop.Coach("yunjun");
            Day(309, 6 * 60);
            Assert.AreEqual("", m.Book(yj, 309, 7 * 60 + 10, false));
            int w1 = m.L.Wallet;
            Day(309, 7 * 60 + 30); Assert.AreEqual(0, m.NoShows().Count, "20분 늦음까지는 아님");
            Day(309, 7 * 60 + 31);
            var ns = m.NoShows();
            Assert.AreEqual(1, ns.Count);
            Assert.AreEqual(2, ns[0].State, "노쇼");
            Assert.AreEqual(w1, m.L.Wallet, "돈은 안 돌아옴");
            Assert.AreEqual(def.Shop.NoshowRel, (int)m.Rel("yunjun"), "신뢰 −2");
            Assert.AreEqual(0, m.NoShows().Count, "한 번만");
            StringAssert.Contains("못 돌려준다", m.S.Phone.Last().Text);
            // 신뢰 −4 이하 = 7일 예약 막힘
            m.S.Set("rel.yunjun", -3);
            Day(310, 6 * 60); Assert.AreEqual("", m.Book(yj, 310, 21 * 60 + 15, false));
            Day(310, 21 * 60 + 40); m.NoShows();
            Assert.IsTrue(m.CoachBlocked(yj));
            StringAssert.Contains("예약을 안 받는다", m.Book(yj, 311, 21 * 60 + 15, false));
            // 주간 계획(맡기기)은 예약을 대신 감 — 노쇼 아님
            var m2 = Rich(def); Day(309, 6 * 60);
            m2.Book(yj, 310, 21 * 60 + 15, false);
            float s0 = m2.StatValue("striking") + m2.StatValue("speed") + m2.StatValue("power") + m2.StatValue("stamina");
            Day(310, 21 * 60);
            var bk = m2.LessonsOn(310).Single();
            StringAssert.Contains("이윤준", m2.LessonAuto(bk));
            Assert.AreEqual(3, bk.State, "맡김");
            Assert.Greater(m2.StatValue("striking") + m2.StatValue("speed") + m2.StatValue("power") + m2.StatValue("stamina"), s0);
            Day(310, 23 * 60); Assert.AreEqual(0, m2.NoShows().Count);
        }

        // ───────────────────────── W19 효과 지속
        [Test]
        public void W19_Buff_Duration()
        {
            var def = Def();
            var m = Rich(def);
            Day(309, 12 * 60);
            m.AddBuff("stamina", 10, 120, "bar");
            Assert.AreEqual(10f, m.BuffValue("stamina"));
            Day(309, 13 * 60 + 59); Assert.AreEqual(10f, m.BuffValue("stamina"), "2시간 안");
            Day(309, 14 * 60); Assert.AreEqual(0f, m.BuffValue("stamina"), "2시간 뒤 끝");
            // 같은 종류는 한 칸(값 큰 쪽 · 끝 늦은 쪽)
            m.AddBuff("hp", 10, 180, "a"); m.AddBuff("hp", 20, 60, "b");
            Assert.AreEqual(20f, m.HpBonus * 100f, 0.01f); Assert.AreEqual(180, m.BuffLeft("hp"));
            // 스태미나 = 드는 피로 −% · 컨디션 = 드릴 판정 +
            m.G.Fatigue = 20; m.AddBuff("stamina", 20, 60, "x");
            m.AddFatigue(10); Assert.AreEqual(28, m.G.Fatigue, "10 × 0.8");
            m.AddBuff("cond", 3, 60, "y");
            var d04 = def.Drills.Find("D04");
            Assert.AreEqual(3.0, m.DrillAssist(d04).score, 1e-6);
            // 가방의 에너지바: 스태미나 +10% 2시간
            var bar = def.Shop.Food("bar");
            Day(310, 12 * 60);
            Assert.AreEqual("", m.Buy(bar)); Assert.IsTrue(m.HasItem(bar));
            StringAssert.Contains("스태미나", m.Eat(bar, true));
            Assert.AreEqual(10f, m.BuffValue("stamina")); Assert.AreEqual(120, m.BuffLeft("stamina"));
            Assert.IsFalse(m.HasItem(bar));
            // 잠을 자면 지나감(장어 8시간 — 20시에 먹고 05:30 기상)
            Day(310, 20 * 60);
            m.Eat(def.Shop.Food("eel"), false);
            Assert.Greater(m.BuffValue("hp"), 0f);
            m.Sleep();
            Assert.AreEqual(0f, m.BuffValue("hp"), "자고 나면 끝");
        }

        // ───────────────────────── W20 섭취 한도
        [Test]
        public void W20_Intake_Limit()
        {
            var def = Def();
            var m = Rich(def);
            var sh = def.Shop;
            Day(309, 18 * 60);
            Assert.AreEqual(12, sh.FullCap);
            StringAssert.Contains("삼계탕", m.Eat(sh.Food("samgyetang"), false));   // 6
            StringAssert.Contains("수육", m.Eat(sh.Food("suyuk"), false));           // 11
            StringAssert.Contains("배부르다", m.CanEat(sh.Food("gukbap_special"), false));
            m.L.Items.Add("삼각김밥");
            StringAssert.Contains("배부르다", m.CanEat(sh.Food("gimbap"), true));
            Assert.AreEqual("", m.CanEat(sh.Food("banana"), false), "바나나우유(1)는 들어감");
            m.Eat(sh.Food("banana"), false);
            Assert.AreEqual(12, m.S.Full);
            Day(310, 7 * 60); m.BeginDayIfNeeded();
            Assert.AreEqual(0, m.S.Full, "다음 날 0");
            // 폐기 직전 삼각김밥(입고 알바 보상)도 같은 음식
            m.L.Items.Add("폐기 직전 삼각김밥");
            Assert.AreEqual("", m.CanEat(sh.Food("gimbap"), true));
            // 영양제는 하루 한 알
            var vit = sh.Food("vitamin");
            Assert.AreEqual("", m.Buy(vit)); Assert.AreEqual(30, m.Doses("vitamin"));
            StringAssert.Contains("1일째", m.Eat(vit, true));
            StringAssert.Contains("오늘 먹었다", m.CanEat(vit, true));
            Assert.AreEqual(29, m.Doses("vitamin"));
            // 돈이 모자라면 못 먹음
            var poor = Rich(def, 5000); Day(309, 18 * 60);
            StringAssert.Contains("모자란다", poor.Eat(sh.Food("samgyetang"), false));
        }

        // ───────────────────────── W21 체감 감소
        [Test]
        public void W21_Diminishing()
        {
            var def = Def();
            var m = Rich(def);
            var sp = def.Shop.Food("gukbap_special");
            Day(309, 21 * 60);
            m.G.Fatigue = 60;
            Assert.AreEqual(1f, m.EatFactor(sp)); m.Eat(sp, false); Assert.AreEqual(52, m.G.Fatigue, "−8");
            Assert.AreEqual(0.6f, m.EatFactor(sp), 1e-4f); m.Eat(sp, false); Assert.AreEqual(47, m.G.Fatigue, "−8×0.6 ≈ 5");
            Assert.AreEqual(0.35f, m.EatFactor(sp), 1e-4f); m.Eat(sp, false); Assert.AreEqual(44, m.G.Fatigue, "−8×0.35 ≈ 3");
            // 며칠 연달아 = ×0.85 씩(최저 0.55)
            var ban = def.Shop.Food("banana");
            var m2 = Rich(def);
            float[] want = { 1f, 0.85f, 0.7225f, 0.614125f, 0.55f, 0.55f };
            for (int d = 0; d < want.Length; d++)
            {
                Day(GameClock.KeyPlus(309, d), 15 * 60); m2.BeginDayIfNeeded();
                Assert.AreEqual(want[d], m2.EatFactor(ban), 1e-4f, $"{d + 1}일째");
                m2.Eat(ban, false);
            }
            Assert.AreEqual(2f * 0.55f, m2.BuffValue("cond"), 0.06f, "효과 값도 줄어듦");
            // 하루 거르면 다시 1
            Day(GameClock.KeyPlus(309, 7), 15 * 60); m2.BeginDayIfNeeded();
            Assert.AreEqual(1f, m2.EatFactor(ban), 1e-4f);
        }

        // ───────────────────────── W22 영양제 — 즉효가 아니라 며칠 꾸준히
        [Test]
        public void W22_Supplement_Streak()
        {
            var def = Def();
            var vit = def.Shop.Food("vitamin"); var mg = def.Shop.Food("magnesium");
            var a = Rich(def); var b = Rich(def);
            a.Buy(vit); a.Buy(mg);
            for (int d = 0; d < 3; d++)
            {
                Day(GameClock.KeyPlus(309, d), 7 * 60); a.BeginDayIfNeeded();
                a.Eat(vit, true); a.Eat(mg, true);
                if (d < 2) Assert.IsFalse(a.SuppOn("vitamin"), $"{d + 1}일째는 아직");
            }
            Assert.IsTrue(a.SuppOn("vitamin"), "3일째부터");
            // 잠 회복 ×1.15: 같은 피로 · 같은 시각에 자면 a 가 더 풀림
            Day(311, 22 * 60);
            a.G.Fatigue = 80; b.G.Fatigue = 80;
            var ra = a.Sleep();
            Day(311, 22 * 60);
            var rb = b.Sleep();
            Assert.Less(ra.FatigueAfter, rb.FatigueAfter, $"비타민 {ra.FatigueAfter} < {rb.FatigueAfter}");
            // 마그네슘: 부상이 하루 빨리 낫는다(밤마다)
            var c = Rich(def); c.Buy(mg);
            for (int d = 0; d < 3; d++) { Day(GameClock.KeyPlus(320, d), 7 * 60); c.BeginDayIfNeeded(); c.Eat(mg, true); }
            Day(322, 22 * 60);
            c.G.InjuryUntil = GameClock.KeyPlus(322, 3); c.G.InjuryKind = "어깨";
            c.Sleep();
            Assert.AreEqual(GameClock.KeyPlus(322, 2), c.G.InjuryUntil, "하루 당겨짐");
            // 이틀 거르면 끊김(꾸준히 2일씩 깎임)
            Day(GameClock.KeyPlus(311, 3), 7 * 60); a.BeginDayIfNeeded();
            a.Eat(vit, true);
            Assert.IsFalse(a.SuppOn("vitamin"), "거르면 다시 쌓아야");
        }

        // ───────────────────────── 레슨 효과(판정 보정 · 기술 · 교정)
        [Test]
        public void W17b_Lesson_Effects()
        {
            var def = Def();
            var m = Rich(def);
            var yj = def.Shop.Coach("yunjun"); var seo = def.Shop.Coach("jinsoo_seo");
            var d04 = def.Drills.Find("D04");
            m.LessonCoach = yj;
            var (s, mm) = m.DrillAssist(d04);
            Assert.AreEqual(yj.Bonus, s, 1e-6); Assert.AreEqual(yj.Metric, mm, 1e-6);
            // 두 번째 레슨 B 이상(D04) = 기술 「거리 재고 원투」
            Day(309, 6 * 60);
            m.Book(yj, 309, 7 * 60 + 10, false); m.Book(yj, 310, 21 * 60 + 15, false);
            var w = DrillRewards.Compute(d04, Grade.A, false, m.G, false);
            var res = new DrillResult { Def = d04, Grade = Grade.A, Scores = new double[d04.MetricCount], Values = new double[d04.MetricCount] };
            float st0 = m.StatValue(WeekPlanner.StatKey(d04.MainStat));
            m.LessonDone(m.S.Lessons[0], d04, res, w);
            Assert.AreEqual(w.MainDelta * (yj.Mul - 1f), m.StatValue(WeekPlanner.StatKey(d04.MainStat)) - st0, 0.01f, "스탯 몫 ×1.8(기본 몫은 DrillRewards)");
            Assert.IsFalse(m.Unlocked(yj.Tech.Id), "첫 레슨엔 아직");
            m.LessonDone(m.S.Lessons[1], d04, res, w);
            Assert.IsTrue(m.Unlocked(yj.Tech.Id), "두 번째 B 이상");
            m.LessonCoach = null;
            Assert.AreEqual(yj.Tech.Plus, m.DrillAssist(d04).score, 1e-6, "익힌 기술은 평소에도 +4");
            // 서진수 교정: 가장 낮은 지표를 짚어 다음 판정 +12(한 판)
            m.Unlock("lesson.guests");
            var bk = new Booking { Coach = seo.Id, Day = 315, At = 19 * 60 + 30 };
            var r2 = new DrillResult { Def = d04, Grade = Grade.C, Scores = Enumerable.Range(0, d04.MetricCount).Select(i => i == 1 ? 0.2 : 0.8).ToArray(), Values = new double[d04.MetricCount] };
            string sum = m.LessonDone(bk, d04, r2, DrillRewards.Compute(d04, Grade.C, false, m.G, false));
            StringAssert.Contains("교정", sum);
            Assert.AreEqual(1, Mathf.RoundToInt(m.Count("fixm.D04")));
            Assert.AreEqual(yj.Tech.Plus + seo.Fix, m.DrillAssist(d04).score, 1e-6);
            m.UseFix(d04);
            Assert.AreEqual(yj.Tech.Plus, m.DrillAssist(d04).score, 1e-6, "한 판 뒤 지움");
            Debug.Log($"[W] W17b 레슨 효과: {sum}");
        }
    }
}
