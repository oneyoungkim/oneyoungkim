// 행인1의 메인이벤트 — 1장 오픈월드 시험 W00~W15(docs/13 12-5). W16(성능)·녹화는 ZWorldShots.
// 클래스 이름 M5… = 입력 테스트·체육관 시험(M4) 뒤. 규칙 시험(W00~W04·W06·W07·W09~W11·W13)은 장면 없이 순수 C#, 나머지는 Zone1 을 열고 WorldRunner 를 봇으로.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Haengin.Tests
{
    public sealed class M5WorldTests
    {
        public const string WorldAsset = "Assets/_Project/Settings/World.asset";
        static string Proj => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        static string Repo => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));

        public static WorldDef Def()
        {
#if UNITY_EDITOR
            var d = UnityEditor.AssetDatabase.LoadAssetAtPath<WorldDef>(WorldAsset);
            if (d == null) Assert.Ignore("World.asset 없음 — WorldSetup.Build 먼저");
            return d;
#else
            Assert.Ignore("에디터 전용"); return null;
#endif
        }

        static StoryDef Story()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<StoryDef>("Assets/_Project/Settings/Story.asset");
#else
            return null;
#endif
        }

        /// 새 게임 직후 상태(3/3 05:50, 지갑 2,600)
        public static WorldModel Fresh(WorldDef def, int seed = 0)
        {
            var b = def.Balance;
            GameClock.SetDate(b.StartYear, b.StartDate / 100, b.StartDate % 100, b.StartTime);
            var s = WorldModel.NewSave(def);
            if (seed != 0) s.Seed = seed;
            var st = Story();
            return new WorldModel(def, s, new Ledger(), new StoryFlags(), new GymSave(), new NameBook(), st != null ? st.Names : null) { DemoMode = true };
        }

        static void Day(int key, int minutes) => GameClock.SetDate(GameClock.Year, key / 100, key % 100, minutes);

        [SetUp] public void Up() { GymState.Current = new GymSave(); }
        [TearDown] public void Down() { GameClock.SetDate(GameClock.DefaultYear, 3, 3, 5 * 60 + 31); }

        // ───────────────────────── W00 금칙어
        [Test]
        public void W00_Banned_Names()
        {
            string list = Path.Combine(Repo, "tools", "banned_names.txt");
            Assert.IsTrue(File.Exists(list), list);
            var rules = new List<(string name, Regex re, string except)>();
            foreach (var raw in File.ReadAllLines(list, Encoding.UTF8))
            {
                var l = raw.Trim();
                if (l.Length == 0 || l.StartsWith("#")) continue;
                string except = null;
                int ex = l.IndexOf("|예외:", StringComparison.Ordinal);
                if (ex >= 0) { except = l.Substring(ex + 4); l = l.Substring(0, ex); }
                bool acr = Regex.IsMatch(l, "^[A-Z]{2,5}$");
                var re = acr ? new Regex($"(?<![A-Za-z0-9]){Regex.Escape(l)}(?![A-Za-z0-9])") : new Regex(Regex.Escape(l), RegexOptions.IgnoreCase);
                rules.Add((l, re, except));
            }
            var files = new List<string>();
            files.AddRange(Directory.GetFiles(Path.Combine(Application.dataPath, "_Project", "Data"), "*.json", SearchOption.AllDirectories));
            files.AddRange(Directory.GetFiles(Path.Combine(Application.dataPath, "_Project", "Data"), "*.tsv", SearchOption.AllDirectories));
            files.AddRange(Directory.GetFiles(Path.Combine(Repo, "story"), "*.md", SearchOption.AllDirectories));
            files.AddRange(Directory.GetFiles(Path.Combine(Repo, "data"), "*.json", SearchOption.AllDirectories));
            files.AddRange(Directory.GetFiles(Path.Combine(Application.dataPath, "_Project", "Scripts"), "*.cs", SearchOption.AllDirectories));
            var hits = new List<string>();
            int lines = 0, exceptions = 0;
            foreach (var f in files)
            {
                int n = 0;
                foreach (var line in File.ReadAllLines(f, Encoding.UTF8))
                {
                    n++; lines++;
                    foreach (var (name, re, except) in rules)
                    {
                        if (!re.IsMatch(line)) continue;
                        if (except != null && except.Split(',').Any(x => x.Length > 0 && line.Contains(x))) { exceptions++; continue; }
                        hits.Add($"{Path.GetFileName(f)}:{n} '{name}' — {line.Trim().Substring(0, Math.Min(80, line.Trim().Length))}");
                    }
                }
            }
            // 데이터 에셋 글(카드·장)도 — 변환 결과에 새는 것
            var dump = WorldJson.Dump(Def());
            foreach (var (name, re, except) in rules) if (re.IsMatch(dump) && except == null) hits.Add($"World.asset '{name}'");
            Debug.Log($"[W] W00 금칙어: 파일 {files.Count} · 줄 {lines} · 이름 {rules.Count} · 예외(이윤준·출장 코치 3 이력) {exceptions} · 걸린 것 {hits.Count}\n{string.Join("\n", hits)}");
            Assert.IsEmpty(hits, "실존 단체·인물 이름\n" + string.Join("\n", hits));
        }

        // ───────────────────────── W01 데이터
        [Test]
        public void W01_Data()
        {
            var def = Def();
            Assert.AreEqual(7, def.Chapters.Length, "장 6 + 막간 1");
            Assert.IsNotNull(def.Chapter("M"), "막간");
            var ids = def.Cards.Select(c => c.Id).ToList();
            CollectionAssert.AllItemsAreUnique(ids, "카드 id 유일");
            var bad = new List<string>();
            var zones = new HashSet<string>(def.MapZones.Concat(def.Balance.GameZones));
            Assert.Greater(def.MapZones.Length, 30, "03 map_zones 존 id 를 읽음");
            var story = Story();
            foreach (var c in def.Cards)
            {
                foreach (var e in c.After.Concat(c.Need).Concat(c.Choice.SelectMany(x => x.After)).Concat(c.Ifs.SelectMany(x => x.After)))
                    foreach (var pre in new[] { "chain:", "card:", "!card:", "done:" })
                        if (e.StartsWith(pre) && def.Card(e.Substring(pre.Length)) == null) bad.Add($"{c.Id}: 없는 카드 {e}");
                if (!string.IsNullOrEmpty(c.Zone) && !zones.Contains(c.Zone)) bad.Add($"{c.Id}: 존 '{c.Zone}' 이 03 에도 게임 전용 목록에도 없음");
                if (story != null) foreach (var s in c.Scenes) if (story.Find(s) == null) bad.Add($"{c.Id}: 장면 {s} 없음");
                if (c.At.Length == 0) bad.Add($"{c.Id}: 뜨는 자리(at) 없음");
            }
            foreach (var p in def.Balance.Points) if (!zones.Contains(p.Zone)) bad.Add($"지점 {p.Id}: 존 {p.Zone}");
            // 두 번 변환해도 같은 에셋
            string Read(string rel) => File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Data", "World", rel));
            var ev = Directory.GetFiles(Path.Combine(Application.dataPath, "_Project", "Data", "World", "events"), "*.json").OrderBy(p => p, StringComparer.Ordinal).Select(File.ReadAllText).ToList();
            string zonesTxt = File.ReadAllText(Path.Combine(Repo, "data", "map_zones.json"));
            var a = ScriptableObject.CreateInstance<WorldDef>(); var b = ScriptableObject.CreateInstance<WorldDef>();
            WorldJson.Fill(a, Read("chapters.json"), Read("activities.json"), Read("balance.json"), ev, zonesTxt);
            WorldJson.Fill(b, Read("chapters.json"), Read("activities.json"), Read("balance.json"), ev, zonesTxt);
            WorldJson.FillShop(a, Read("shop.json"), Read("lesson_lines.tsv"));
            WorldJson.FillShop(b, Read("shop.json"), Read("lesson_lines.tsv"));
            string da = WorldJson.Dump(a), db = WorldJson.Dump(b), dd = WorldJson.Dump(def);
            Assert.AreEqual(da, db, "두 번 변환 같음");
            Assert.AreEqual(da, dd, "에셋 = 원본 JSON(손으로 고친 것 없음 — WorldSetup.DataOnly)");
            Debug.Log($"[W] W01 데이터: 장 {def.Chapters.Length} · 카드 {def.Cards.Length}(데모 {def.Cards.Count(c => c.Demo)}) · 활동 {def.Activities.Length} · 지점 {def.Balance.Points.Length} · 03 존 {def.MapZones.Length} · 문제 {bad.Count}\n{string.Join("\n", bad)}");
            Assert.IsEmpty(bad, string.Join("\n", bad));
        }

        // ───────────────────────── W02 시계
        [Test]
        public void W02_Clock()
        {
            var def = Def();
            var m = Fresh(def);
            Assert.AreEqual("화", GameClock.Weekday, "2026-03-03 = 화요일");
            Day(303, 23 * 60);
            GameClock.Advance(3 * 60 + 10);
            Assert.AreEqual(304, GameClock.DayKey, "자정 넘김"); Assert.AreEqual("수", GameClock.Weekday);
            Assert.AreEqual(2 * 60 + 10, (int)GameClock.Minutes);
            // 잠 → 05:30 기상(월~토), 일요일 08:00
            Day(303, 23 * 60);
            m.G.Fatigue = 60;
            var r = m.Sleep();
            Assert.AreEqual(304, r.WakeDay); Assert.AreEqual(5 * 60 + 30, r.WakeMin);
            Assert.AreEqual(304, GameClock.DayKey); Assert.AreEqual(5 * 60 + 30, (int)GameClock.Minutes);
            Assert.AreEqual(Mathf.Clamp(60 - Mathf.RoundToInt(9f * 6.5f), 0, 100), m.G.Fatigue, "시간당 −9 × 6.5시간");
            Day(307, 23 * 60);
            r = m.Sleep();
            Assert.AreEqual(308, r.WakeDay); Assert.AreEqual(8 * 60, r.WakeMin, "일요일 늦잠 08:00");
            // 70 이상에서 자면 +10
            Day(309, 23 * 60); m.G.Fatigue = 90; r = m.Sleep();
            Assert.AreEqual(Mathf.Clamp(90 - Mathf.RoundToInt(9f * 6.5f) + 10, 0, 100), m.G.Fatigue, "덜 풀림 +10");
            // 말일 이체 1번
            Day(331, 23 * 60);
            m.S.Household = 700000;
            r = m.Sleep();
            Assert.IsTrue(r.MonthSettled); Assert.AreEqual(3, m.S.SettledMonth);
            int w = m.L.Wallet;
            Assert.AreEqual(0, m.MonthEnd(3), "같은 달 두 번 정산 안 함");
            Assert.AreEqual(w, m.L.Wallet);
            Assert.AreEqual(401, GameClock.DayKey);
            // 2028 윤년
            GameClock.SetDate(2028, 2, 28, 600);
            GameClock.Advance(1440);
            Assert.AreEqual(229, GameClock.DayKey, "2028-02-29"); Assert.AreEqual(229, GameClock.KeyPlus(228, 1));
            GameClock.SetDate(2026, 3, 3, 600);
            // 날씨 표: 시드 고정
            var m2 = Fresh(def); var m3 = Fresh(def, 99);
            var t1 = new StringBuilder(); var t2 = new StringBuilder(); var t3 = new StringBuilder();
            for (int d = 0; d < 90; d++) { int k = GameClock.KeyPlus(303, d); t1.Append(m.Rain(k) ? 'R' : '.'); t2.Append(m2.Rain(k) ? 'R' : '.'); t3.Append(m3.Rain(k) ? 'R' : '.'); }
            Assert.AreEqual(t1.ToString(), t2.ToString(), "같은 시드 = 같은 날씨");
            Assert.AreNotEqual(t1.ToString(), t3.ToString(), "다른 시드 = 다른 날씨");
            Assert.IsTrue(m.Rain(313), "3/13 비(원고 교차점)"); Assert.IsFalse(m.Rain(314), "3/14 노가다 첫날 맑음"); Assert.IsTrue(m.Rain(328), "3/28 비(N07)");
            // 시간대
            Assert.AreEqual("새벽", m.Band(6 * 60)); Assert.AreEqual("아침", m.Band(12 * 60)); Assert.AreEqual("오후", m.Band(17 * 60)); Assert.AreEqual("저녁", m.Band(19 * 60)); Assert.AreEqual("밤", m.Band(23 * 60)); Assert.AreEqual("밤", m.Band(3 * 60));
            // 조명 연속 보간
            var (a, b2, kk) = DayLight.PartsAt(7 * 60);
            Assert.AreEqual(DayPart.Dawn, a); Assert.AreEqual(DayPart.Day, b2); Assert.That(kk, Is.InRange(0.1f, 0.9f));
            Assert.AreEqual(DayPart.Day, DayLight.PartsAt(12 * 60).Item1);
            Assert.AreEqual(DayPart.Night, DayLight.PartsAt(23 * 60).Item1);
            Debug.Log($"[W] W02 시계: 3월~5월 비 {t1.ToString().Count(c => c == 'R')}일 {t1}");
        }

        // ───────────────────────── W03 알바 정산(원고 숫자)
        [Test]
        public void W03_Job_Pay()
        {
            var def = Def();
            var m = Fresh(def);
            var del = def.Activity("job.delivery"); var gb = def.Activity("job.gukbap"); var site = def.Activity("job.site"); var ctr = def.Activity("job.counter"); var stock = def.Activity("job.stock");
            Assert.AreEqual(2000, del.Pay, "배달 건당 2,000(2화)");
            Day(303, 6 * 60); Assert.AreEqual(4, m.UnitsToday(del), "3/3 = 4건(1화 '네 집')");
            Assert.AreEqual(120000, site.Pay, "노가다 일당 120,000(4화)");
            Assert.AreEqual(10320 * 5, ctr.Pay, "편의점 시급 10,320 × 5(5화)");
            Assert.AreEqual(20000, gb.Pay, "국밥집 저녁 하루 ≈ 20,000(달 40만)");
            CollectionAssert.AreEqual(new[] { "폐기 직전 삼각김밥:2" }, stock.Items, "입고 = 삼각김밥 2(3화)");
            // 가계부 한 줄(09 부록 A-1) 그대로
            var st = Story();
            if (st != null) StringAssert.StartsWith("새벽 배달 4건 +8,000원 → 엄마 통장", st.Episodes[0].Ledger);
            // 기본 순서 3월: 배달(월~토)·국밥집(평일) + 노가다 첫날 12만은 고지서 밑(난방비 구멍) → 말일 이체 결손 없음
            m.Unlock("job.delivery"); m.Unlock("job.gukbap"); m.Unlock("school");
            int earnedDel = 0, earnedGb = 0;
            for (int d = 0; d < 29; d++)
            {
                int key = GameClock.KeyPlus(303, d);
                Day(key, 5 * 60 + 30);
                m.BeginDayIfNeeded();
                string w = m.Weekday;
                if (w != "일") { var r = m.Complete(del, Grade.B, false, del.Start); earnedDel += r.Pay; }
                if (w != "토" && w != "일") { Day(key, 17 * 60); var r = m.Complete(gb, Grade.B, false, gb.Start); earnedGb += r.Pay; }
                if (key == 306) m.Apply("hole:난방비:120000");
                if (key == 314) { m.OnLedgerLine(120000); m.Apply("holepay:난방비"); }
            }
            Day(331, 23 * 60);
            int hh = m.S.Household;
            var sr = m.Sleep();
            Debug.Log($"[W] W03 3월 기본 순서: 배달 {earnedDel:N0} · 국밥집 {earnedGb:N0} · 생활비 {hh:N0} · 말일 {def.Balance.FixedTotal(3):N0} · 모자람 {sr.Shortfall} · 지갑 {m.L.Wallet:N0}");
            Assert.AreEqual(0, sr.Shortfall, "3월 결손 없음");
            Assert.AreEqual(25 * 9000 - 1000, earnedDel, "배달 25일(4·5건 번갈아)");
            Assert.AreEqual(21 * 20000, earnedGb, "국밥집 21일");
            Assert.That(m.L.Wallet - 2600, Is.InRange(10000, 60000), "남는 돈 ≈ 2만(원고 '지갑 2,600원' 근처)");
        }

        // ───────────────────────── W04 7시간·피로
        [Test]
        public void W04_Minor_Hours_Fatigue()
        {
            var def = Def();
            var m = Fresh(def);
            foreach (var a in def.Activities.Where(a => a.IsJob || a.Kind == "gym"))
            {
                Assert.LessOrEqual(a.End, def.Balance.MinorNightFrom, $"{a.Id}: 밤 10시 전에 끝(결정 12)");
                Assert.GreaterOrEqual(a.Start, def.Balance.MinorNightTo, $"{a.Id}: 아침 6시 뒤 시작");
                Assert.LessOrEqual(a.Minutes, def.Balance.PerWorkDay, $"{a.Id}: 하루 7시간 이하");
            }
            // 같은 일터(국밥집) 하루 합계: 배달 50분 + 저녁 240분 = 290 ≤ 420
            m.Unlock("job.delivery"); m.Unlock("job.gukbap");
            var del = def.Activity("job.delivery"); var gb = def.Activity("job.gukbap");
            Day(309, 6 * 60);
            Assert.AreEqual("", m.CanStart(del)); m.Complete(del, Grade.B, false, del.Start);
            Day(309, 17 * 60);
            Assert.AreEqual("", m.CanStart(gb)); m.Complete(gb, Grade.B, false, gb.Start);
            Assert.AreEqual(290, m.Worked("gukbap"));
            // 7시간을 넘기려 하면 막음
            Day(310, 6 * 60);
            m.BeginDayIfNeeded();
            m.S.AddWork("gukbap", 200);
            StringAssert.Contains("7시간", m.CanStart(gb, 17 * 60));
            // 밤 10시 넘는 일(가짜 정의) 막음
            var night = new ActivityDef { Id = "x", Name = "밤일", Kind = "job", Work = "x", Days = "", Open = 21 * 60, Start = 21 * 60 + 30, End = 22 * 60 + 30, Latest = 22 * 60 };
            Day(310, 21 * 60 + 30);
            StringAssert.Contains("밤 10시", m.CanStart(night));
            // 피로 문턱: 70 = 스탯 ×0.6 · 85 = 체육관 막음 · 95 = 쓰러짐
            var m1 = Fresh(def); var m2 = Fresh(def);
            m1.Unlock("job.gukbap"); m2.Unlock("job.gukbap");
            Day(309, 17 * 60); m1.G.Fatigue = 10; m2.G.Fatigue = 75;
            m1.Complete(gb, Grade.B, false, gb.Start); m2.Complete(gb, Grade.B, false, gb.Start);
            Assert.AreEqual(0.2f, m1.S.StatFrac[7], 1e-4f); Assert.AreEqual(0.2f * 0.6f, m2.S.StatFrac[7], 1e-4f, "70 이상 ×0.6");
            var d01 = def.Drills != null ? def.Drills.Find("D01") : null;
            if (d01 != null) { m.G.Fatigue = 86; StringAssert.Contains("오늘은 끝", DrillSim.DirectBlocked(d01, m.G, GymSave.Today)); }
            m.G.Fatigue = 96; Day(311, 17 * 60);
            StringAssert.Contains("쓰러지기", m.CanStart(gb));
            // 근육통(N06): 노가다 다음 날 아침
            var mm = Fresh(def); var dir = new EventDirector(mm);
            Day(315, 8 * 60); mm.F.Raise("sore_next");
            var c = dir.Pick("wake");
            Assert.NotNull(c); Assert.AreEqual("N06", c.Id);
            dir.ApplyAfter(c); dir.Done(c, 1);
            Assert.IsTrue(mm.F.Has("sore_today")); Assert.IsFalse(mm.F.Has("sore_next"));
            Debug.Log("[W] W04 7시간·피로: 일터 국밥집 290분 · 7시간 넘김 막음 · 밤 10시 막음 · 70 ×0.6 · 85 체육관 · 95 쓰러짐 · 근육통 N06");
        }

        // ───────────────────────── W06 감독
        [Test]
        public void W06_Director()
        {
            var def = Def();
            var m = Fresh(def); var d = new EventDirector(m);
            // 조건 카드: 문턱 전 0 → 넘은 뒤 그 장소에서 1번
            Day(323, 21 * 60 + 45);
            m.S.Add("job.stock", 5);
            Assert.IsNull(d.Pick("after:job.stock"), "입고 5번 — 아직");
            m.S.Add("job.stock", 1);
            var c = d.Pick("after:job.stock");
            Assert.NotNull(c); Assert.AreEqual("C1-15", c.Id);
            Assert.IsNull(d.Pick("enter:Gukbap") is CardDef x && x.Id == "C1-15" ? x : null, "다른 장소에선 안 뜸");
            d.ApplyAfter(c); d.Done(c, 1);
            Assert.IsNull(d.Pick("after:job.stock"), "한 번만");
            Assert.IsTrue(m.Unlocked("job.counter"));
            // 우선순위: 계절(C1-16) > 사연(N10) > 반복(C1-10) — 4/2 배달 뒤
            var m2 = Fresh(def); var d2 = new EventDirector(m2);
            Day(402, 7 * 60);
            m2.S.Add("delivery_good", 5);
            var p = d2.Pick("after:job.delivery");
            Assert.AreEqual("C1-16", p.Id, "계절 먼저");
            d2.ApplyAfter(p); d2.Done(p, 1);
            p = d2.Pick("after:job.delivery");
            Assert.AreEqual("N10", p.Id, "그다음 사연");
            // 하루 상한: 사연 2
            m2.S.SideToday = 2; m2.S.WorkDay = m2.Today;
            Assert.AreNotEqual("N10", d2.Pick("after:job.delivery")?.Id, "사연 하루 2개 상한");
            // 놓칠 수 없음: C1-07(3/13 집) — 22:30 이 지나면 어디서든 '시각' 으로
            var m3 = Fresh(def); var d3 = new EventDirector(m3);
            m3.F.Raise("site_booked");
            Day(313, 22 * 60);
            Assert.IsNull(d3.Pick("time"), "22:00 — 아직 집에 갈 시간");
            Assert.AreEqual("C1-07", d3.Pick("enter:Home")?.Id, "집에 오면 바로");
            Day(313, 22 * 60 + 31);
            Assert.AreEqual("C1-07", d3.Pick("time")?.Id, "22:30 넘으면 데려감");
            Assert.AreEqual("C1-07", d3.Pick("sleep", 313, 23 * 60)?.Id, "자기 전에도");
            // 활동 중 카드(N01 20:30 국밥집 일 중)
            var m4 = Fresh(def); var d4 = new EventDirector(m4);
            m4.MarkCard(def.Card("C1-04"), 1);
            Day(309, 17 * 60);
            Assert.AreEqual("N01", d4.PickDuring("job.gukbap", 17 * 60, 21 * 60)?.Id);
            Assert.IsNull(d4.PickDuring("job.gukbap", 20 * 60 + 45, 21 * 60), "20:30 이 지난 뒤 시작이면 없음");
            // 창이 지난 핵심 = 따라잡기
            var m5 = Fresh(def); var d5 = new EventDirector(m5);
            m5.MarkCard(def.Card("C1-01"), 1);
            Day(306, 7 * 60);
            Assert.IsTrue(d5.Overdue().Any(o => o.Id == "C1-02"), "3/4~3/5 학교 카드를 놓치면 따라잡기");
            Debug.Log($"[W] W06 감독: 조건 C1-15 문턱 6 · 계절 C1-16 > 사연 N10 · 사연 상한 2 · C1-07 22:30 · N01 20:30 · 따라잡기 C1-02\n{string.Join("\n", d.Trace.Concat(d2.Trace).Concat(d3.Trace))}");
        }

        // ───────────────────────── W07 결정성
        [Test]
        public void W07_Deterministic()
        {
            var def = Def();
            string RunOnce()
            {
                var m = Fresh(def); var d = new EventDirector(m); var pl = new WeekPlanner(m, d) { Drills = def.Drills != null ? def.Drills.Drills : new DrillDef[0] };
                foreach (var id in new[] { "C1-01", "C1-02", "C1-03", "C1-04", "C1-05", "C1-07", "C1-08", "C1-09", "C1-11", "C1-12", "C1-13", "N01" }) { var c = def.Card(id); d.ApplyAfter(c); d.Done(c, 1); }
                m.Host.Clear();
                Day(317, 5 * 60 + 30);
                var plan = pl.Make(317, 44);
                var rep = pl.Run(plan);
                return $"{rep}\n{string.Join("\n", rep.Days)}\n{string.Join("\n", d.Trace)}\n{m.Status}\n{string.Join(",", m.G.Stats)} {string.Join(",", m.S.StatFrac.Select(f => f.ToString("F3")))}";
            }
            string a = RunOnce(), b = RunOnce();
            Assert.AreEqual(a, b, "같은 저장·같은 계획 두 번 → 카드 순서·반복 이벤트·돈·스탯 같음");
            Debug.Log("[W] W07 결정성: 두 번 같음\n" + a);
        }

        // ───────────────────────── W09 새 카드
        [Test]
        public void W09_New_Cards()
        {
            var def = Def();
            var m = Fresh(def); var d = new EventDirector(m);
            // N01 → 체험 → N02(선택 셋)
            m.MarkCard(def.Card("C1-04"), 1);
            Day(310, 17 * 60);
            var n1 = d.PickDuring("job.gukbap", 17 * 60, 21 * 60);
            Assert.AreEqual("N01", n1.Id);
            d.ApplyAfter(n1); d.Done(n1, 1);
            Assert.IsTrue(m.S.GymTrial); CollectionAssert.Contains(m.Host, "gymtrial");
            Day(310, 21 * 60 + 30);
            var n2 = d.Pick("after:gymtrial");
            Assert.AreEqual("N02", n2.Id); Assert.AreEqual(3, n2.Choice.Length);
            d.ApplyAfter(n2, n2.Choice[1]); d.Done(n2, 1);
            Assert.AreEqual(2, m.S.GymMember, "청소 관원"); Assert.IsTrue(m.Unlocked("gym.cleaner"));
            // N03 회비 날(4/1): 지갑 모자라면 고준이 한 번
            Day(401, 5 * 60 + 30);
            m.L.SetWallet(10000);
            var n3 = d.Pick("wake"); Assert.AreEqual("N03", n3.Id);
            d.ApplyAfter(n3); d.Done(n3, 1);
            Assert.AreEqual(1, m.S.GymDebt, "고준이 대신 냄");
            // N04 말일 결손
            var m2 = Fresh(def); var d2 = new EventDirector(m2);
            Day(331, 23 * 60); m2.S.Household = 100000; m2.L.SetWallet(0);
            var sr = m2.Sleep(); Assert.Greater(sr.Shortfall, 0);
            Assert.AreEqual("N04", d2.Pick("monthend")?.Id);
            // N07 비 예보 금요일 밤(3/27 → 3/28 비)
            var m3 = Fresh(def); var d3 = new EventDirector(m3);
            m3.Unlock("job.site"); Day(327, 22 * 60);
            Assert.AreEqual("N07", d3.Pick("sleep")?.Id);
            // N08 첫 S
            var m4 = Fresh(def); var d4 = new EventDirector(m4);
            m4.G.Of("D01").SCount = 1; Day(312, 19 * 60);
            Assert.AreEqual("N08", d4.Pick("after:drill")?.Id);
            // N09 → N09b 사연 두 칸
            var m5 = Fresh(def); var d5 = new EventDirector(m5);
            Day(318, 16 * 60 + 30);
            var n9 = d5.Pick("point:waryong"); Assert.AreEqual("N09", n9.Id);
            d5.ApplyAfter(n9); d5.Done(n9, 1);
            var n9b = d5.Pick("point:cat_spot"); Assert.AreEqual("N09b", n9b.Id);
            float rep0 = m5.S.TownRep; d5.ApplyAfter(n9b); d5.Done(n9b, 1);
            Assert.AreEqual(rep0 + 3f, m5.S.TownRep, 1e-3f); Assert.IsTrue(m5.CardDone("N09") && m5.CardDone("N09b"), "사연 두 칸 끝");
            // 변형 대사(9화 — 다님/안 다님 줄)
            var c25 = def.Card("C1-25");
            var m6 = Fresh(def); var d6 = new EventDirector(m6);
            Assert.IsFalse(string.Join(" ", d6.BoardOf(c25)).Contains("필요가 없다"), "안 다님 = 원고 줄");
            m6.S.GymMember = 2;
            StringAssert.Contains("필요가 없다", string.Join(" ", d6.BoardOf(c25)), "다님 = 변형 줄");
            StringAssert.Contains("청소하면서", string.Join(" ", d6.QuotesOf(c25)));
            // 케이크 변형(C1-19): 봉투 40,000 → 2호
            var m7 = Fresh(def); var d7 = new EventDirector(m7);
            m7.L.Apply("envelope", "+40000"); var c19 = def.Card("C1-19");
            d7.ApplyAfter(c19); Assert.IsTrue(m7.F.Has("cake2"));
            var m8 = Fresh(def); var d8 = new EventDirector(m8);
            m8.L.Apply("envelope", "+30000"); d8.ApplyAfter(c19); Assert.IsTrue(m8.F.Has("cake1"));
            Debug.Log("[W] W09 새 카드: N01 → 체험 → N02(청소 관원) · N03 고준 빚 · N04 결손 · N07 비 · N08 첫 S · N09 → N09b · C1-25 변형 줄 · C1-19 2호/1호");
        }

        // ───────────────────────── W10 주간 계획
        [Test]
        public void W10_Week_Planner()
        {
            var def = Def();
            WorldModel Ready(out EventDirector d, out WeekPlanner pl, bool direct)
            {
                var m = Fresh(def); d = new EventDirector(m); pl = new WeekPlanner(m, d) { Drills = def.Drills != null ? def.Drills.Drills : new DrillDef[0], Direct = direct };
                foreach (var id in new[] { "C1-01", "C1-02", "C1-03", "C1-04", "C1-05", "C1-07", "C1-08", "C1-09", "C1-11", "C1-12", "C1-13", "N01" }) { var c = def.Card(id); d.ApplyAfter(c); d.Done(c, 1); }
                m.Host.Clear();
                Day(317, 5 * 60 + 30);
                return m;
            }
            var A = Ready(out var dA, out var pA, false);
            float s0 = A.StatValue("stamina") + A.StatValue("speed") + A.StatValue("mental") + A.StatValue("power") + A.StatValue("toughness");
            var ra = pA.Run(pA.Make(317, 14));
            int endA = GameClock.DayKey;
            var Bm = Ready(out var dB, out var pB, true);
            var rb = pB.Run(pB.Make(317, 14));
            float sa = A.StatValue("stamina") + A.StatValue("speed") + A.StatValue("mental") + A.StatValue("power") + A.StatValue("toughness") - s0;
            float sb = Bm.StatValue("stamina") + Bm.StatValue("speed") + Bm.StatValue("mental") + Bm.StatValue("power") + Bm.StatValue("toughness") - s0;
            Debug.Log($"[W] W10 맡기기 {ra} / 직접 {rb} · 부 스탯 맡김 {sa:F2} · 직접 {sb:F2}\n{string.Join("\n", ra.Days)}");
            Assert.AreEqual(14, ra.DaysRun, "2주 다(이벤트 날 없음)");
            Assert.AreEqual(rb.WalletDelta, ra.WalletDelta, "돈 100%(지갑)"); Assert.AreEqual(rb.HouseholdDelta, ra.HouseholdDelta, "돈 100%(생활비)");
            Assert.AreEqual(sb * 0.75f, sa, 1.0f, "부 스탯 75%(±1)");
            Assert.AreEqual(Bm.G.Fatigue, A.G.Fatigue, "피로 100%");
            Assert.AreEqual(331, endA); Assert.AreEqual(331, GameClock.DayKey);
            // 이벤트 날에서 멈춤(5/1 「2호 초코 생크림」) · 계절 카드 요약(C1-16 4/1~4/7)
            var C = Ready(out var dC, out var pC, false);
            Day(330, 5 * 60 + 30);
            var rc = pC.Run(pC.Make(330, 40));
            Debug.Log($"[W] W10 3/30~: {rc}\n{string.Join("\n", rc.Days)}");
            Assert.NotNull(rc.Stopped); Assert.AreEqual("C1-20", rc.Stopped.Id, "5/1 이벤트 날 아침에 멈춤");
            Assert.AreEqual(501, GameClock.DayKey);
            Assert.IsTrue(rc.Summaries.Any(s => s.Id == "C1-16"), "계절 카드 C1-16 요약");
            Assert.IsTrue(C.CardDone("C1-19"), "4/29 케이크 예약(요약)");
        }

        // ───────────────────────── W11 챕터 화면
        [Test]
        public void W11_Chapter_Goals()
        {
            var def = Def();
            var ch = def.Chapter("1");
            var m = Fresh(def);
            Assert.IsTrue(ch.HasDemo); Assert.AreEqual(4, ch.DemoGoals.Length); Assert.AreEqual(3, ch.Goals.Length);
            Assert.IsFalse(m.GoalsMet(ch, true)); Assert.IsFalse(m.GoalsMet(ch, false));
            // 값 = 실제 상태
            m.S.TownRep = 23.5f; m.S.StreetWins = 1; m.S.Deficits = 1;
            var g = ch.DemoGoals.First(x => x.Expr == "rep.town");
            Assert.AreEqual(23.5f, m.GoalValue(g), 1e-4f);
            StringAssert.Contains("23.5", m.GoalLine(g));
            Assert.IsFalse(m.GoalsMet(ch, true), "케이크 아직");
            m.MarkCard(def.Card("C1-21"), 1);
            Assert.IsTrue(m.GoalsMet(ch, true), "데모 목표 4개 다");
            m.S.Deficits = 2;
            Assert.IsFalse(m.GoalsMet(ch, true), "결손 2번이면 데모 목표 미달(1회 이하)");
            // 1장 전체 목표가 차면 승격 카드(C1-48 첫눈의 야차)가 정한 자리(와룡공원 공터, 토 23:00 '시각')
            m.S.Deficits = 0; m.S.TownRep = 61f; m.S.YachaWins = 5;
            Assert.IsTrue(m.GoalsMet(ch, false));
            m.DemoMode = false;
            var d = new EventDirector(m);
            Day(1205, 23 * 60 + 1);
            var c = d.Pick("time");
            Assert.NotNull(c); Assert.AreEqual("C1-48", c.Id); Assert.AreEqual("waryong_entrance", c.Zone);
            Debug.Log($"[W] W11 챕터: 데모 목표 {string.Join(" / ", ch.DemoGoals.Select(m.GoalLine))} · 1장 {string.Join(" / ", ch.Goals.Select(m.GoalLine))} → C1-48 @ {c.Zone}");
        }

        // ───────────────────────── W12 평판·소문
        [Test]
        public void W12_Rumor_Rep()
        {
            var def = Def();
            var m = Fresh(def); var d = new EventDirector(m);
            Assert.AreEqual("", m.RumorLine("손님"), "첫 반항 전 — 소문 없음");
            var lines = new List<string>();
            foreach (var id in new[] { "C1-21" }) { var c = def.Card(id); d.ApplyAfter(c); d.Done(c, 1); }
            Assert.AreEqual(1, m.S.Rumor); lines.Add(m.RumorLine("손님"));
            CollectionAssert.Contains(def.Balance.Rumor[1].Lines, lines[0]);
            m.MarkCard(def.Card("C1-22"), 1);
            Day(505, 5 * 60 + 30);
            var c23 = d.Pick("wake"); Assert.AreEqual("C1-23", c23.Id); d.ApplyAfter(c23); d.Done(c23, 1);
            Assert.AreEqual(2, m.S.Rumor); lines.Add(m.RumorLine("손님")); CollectionAssert.Contains(def.Balance.Rumor[2].Lines, lines[1]);
            m.Apply("rumor:3"); lines.Add(m.RumorLine("손님")); CollectionAssert.Contains(def.Balance.Rumor[3].Lines, lines[2]);
            Assert.AreEqual(m.RumorLine("손님"), m.RumorLine("손님"), "같은 날·같은 사람 = 같은 말");
            // 시비 승 평판 +2(1:1) / +3(1:다수) · 패 −1
            float r0 = m.S.TownRep;
            m.AddRep(def.Balance.StreetWin); Assert.AreEqual(r0 + 2f, m.S.TownRep, 1e-4f);
            m.AddRep(def.Balance.StreetLoss); Assert.AreEqual(r0 + 1f, m.S.TownRep, 1e-4f);
            Assert.AreEqual("그 배달하는 애", def.Balance.RepName(20)); Assert.AreEqual("행인1", def.Balance.RepName(45));
            Debug.Log($"[W] W12 소문 1→3: {string.Join(" → ", lines)}");
        }

        // ───────────────────────── W13 저장 v2
        [Test]
        public void W13_Save_V2()
        {
            var def = Def();
            string dir = Path.Combine(Application.temporaryCachePath, "W13 저장 — 한글");
            SaveStore.Dir = dir; Directory.CreateDirectory(dir);
            try
            {
                var m = Fresh(def); var d = new EventDirector(m);
                foreach (var id in new[] { "C1-01", "C1-02", "C1-03" }) { var c = def.Card(id); d.ApplyAfter(c); d.Done(c, 1); }
                m.L.Apply("envelope", "+15000"); m.S.Household = 123456; m.S.TownRep = 7.5f; m.AddStat("stamina", 0.6f);
                Day(306, 21 * 60 + 5);
                var s = new SaveData { SceneId = "world", Month = GameClock.Month, Day = GameClock.Day, Minutes = GameClock.Minutes, Ledger = m.L, Flags = m.F, Gym = m.G, Names = m.N, World = m.S.Clone() };
                s.World.Active = true;
                SaveStore.Write(SaveStore.Auto, s);
                var back = SaveStore.Read(SaveStore.Auto, out string err);
                Assert.IsNull(err, err);
                Assert.IsTrue(s.SameAs(back), "왕복 같음");
                Assert.AreEqual(2, back.Version);
                Assert.AreEqual(3, back.World.Cards.Count(c => c.State == 1), "카드 상태");
                Assert.AreEqual(15000, back.Ledger.Envelope, "봉투");
                Assert.AreEqual(0.6f, back.World.StatFrac[2], 1e-4f);
                var s2 = back.Clone(); s2.World.TownRep = 9f; SaveStore.Write(SaveStore.Auto, s2);
                Assert.IsTrue(s.SameAs(SaveStore.ReadPrev(SaveStore.Auto, out _)), ".prev = 앞 저장");
                // M3 v1 저장(장면 4-1 시작) → 카드 C1-09 부터
                var v1 = new SaveData { SceneId = "4-1", Month = 3, Day = 14, Minutes = 7 * 60 };
                v1.Version = 1;
                File.WriteAllText(SaveStore.PathOf("v1"), JsonUtility.ToJson(v1));
                var r1 = SaveStore.Read("v1", out string e1);
                Assert.IsNull(e1, e1); Assert.AreEqual(1, r1.Version);
                var ws = WorldModel.MigrateV1(def, r1, out string card);
                Assert.AreEqual("C1-09", card);
                Assert.AreEqual("C1-09", ws.Card);
                foreach (var id in new[] { "C1-01", "C1-02", "C1-05", "C1-08" }) Assert.IsTrue(ws.Cards.Any(c => c.Id == id && c.State == 1), id + " 끝낸 것으로");
                CollectionAssert.Contains(ws.Unlocks, "job.stock");
                var lost = WorldModel.MigrateV1(def, new SaveData { SceneId = "없는장면", Version = 1 }, out string c0);
                Assert.AreEqual("C1-01", c0, "못 옮기면 1장 처음부터");
                Debug.Log($"[W] W13 저장 v2: 왕복·.prev·v1 4-1 → {card}(앞 카드 {ws.Cards.Count}장 끝 · 열림 {string.Join(",", ws.Unlocks)})");
            }
            finally { foreach (var sl in new[] { SaveStore.Auto, "v1" }) SaveStore.Delete(sl); SaveStore.Dir = null; }
        }
    }
}
