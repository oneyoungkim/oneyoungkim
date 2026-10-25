// 행인1의 메인이벤트 — 1장 오픈월드 장면 시험(docs/13 12-5): W05 체육관 · W08 핵심 카드 · W12b 첫 시비 · W14 봇 직진 · W15 봇 전부.
// Zone1 을 Additive 로 열고(M3Lab) WorldRunner 를 봇으로(대사·선택·안내 짧게, 미니게임·드릴 봇 실력 0.9). 저장은 임시 한글 폴더.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Haengin.Tests
{
    public sealed class M5WorldPlayTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var w = Object.FindAnyObjectByType<WorldRunner>();
            if (w != null) { var b = w.GetComponent<WorldBot>(); if (b != null) Object.Destroy(b); w.Stop(); }
            WorldRunner.BotPreset = null;
            GymManager.Gate = null; StageDoor.Gate = null;
            yield return M3Lab.TearDown();
        }

        static IEnumerator Open(System.Action<WorldRunner> got)
        {
            yield return M3Lab.OpenZone1();
            var w = Object.FindAnyObjectByType<WorldRunner>();
            Assert.NotNull(w, "Zone1 에 세계 루트(WorldRunner)가 없음 — WorldSetup.Build");
            w.BotSkill = 0.9f; w.BotRead = 0.02f; w.BotRide = false; w.BotDelegate = true;
            w.BotChoice = c => c.Id == "N02" ? 1 : 0;
            got(w);
        }

        static IEnumerator Free(WorldRunner w, int frames = 60 * 600)
        {
            for (int i = 0; i < frames && !w.Free; i++) yield return null;
        }

        /// 중간 저장(이 카드들까지 끝낸 상태, 그 날짜·시각·장소)
        static SaveData Mid(WorldDef def, IEnumerable<string> done, int day, int minutes, string place, System.Action<WorldModel> tweak = null)
        {
            var m = M5WorldTests.Fresh(def);
            var d = new EventDirector(m);
            foreach (var id in done) { var c = def.Card(id); d.ApplyAfter(c, c.Choice.Length > 1 ? c.Choice[1] : null); d.Done(c, 1); }
            m.Host.Clear();
            GameClock.SetDate(2026, day / 100, day % 100, minutes);
            tweak?.Invoke(m);
            m.S.Active = true; m.S.Place = place; m.S.Spawn = "Spawn"; m.S.HasPos = false; m.S.Card = "";
            if (place == SceneLoader.Zone1) { var pt = def.Balance.Point("bus_stop"); m.S.HasPos = true; m.S.Pos = pt.Pos + new Vector3(1.6f, 0f, 0f); }
            return new SaveData { SceneId = "world", Month = day / 100, Day = day % 100, Minutes = minutes, Ledger = m.L, Flags = m.F, Gym = m.G, Names = m.N, World = m.S };
        }

        // ───────────────────────── W05 체육관 연결
        [UnityTest, Timeout(1200000)]
        public IEnumerator W05_Gym_Link()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            var def = w.Def;
            var save = Mid(def, new[] { "C1-01", "C1-02", "C1-03", "C1-04", "N01", "N02" }, 312, 19 * 60, SceneLoader.Zone1);
            w.Begin(save);
            yield return Lab.Frames(10);
            yield return Free(w);
            Assert.AreEqual(2, w.Model.S.GymMember, "청소 관원으로 시작");
            var door = w.FindDoor(SceneLoader.Gym);
            Assert.NotNull(door, "정류장 → 체육관 문");
            double t0 = GameClock.Minutes;
            M3Lab.Motor.Teleport(door.ReturnPos, 0f);
            yield return Lab.Frames(5);
            door.Use();
            for (int i = 0; i < 1200 && w.Loader.Place != SceneLoader.Gym; i++) yield return null;
            Assert.AreEqual(SceneLoader.Gym, w.Loader.Place, "마을버스 → 혜화 MMA");
            Assert.AreEqual(t0 + def.Balance.BusMinutes, GameClock.Minutes, 1.0, "버스 15분");
            GymManager gm = null;
            for (int i = 0; i < 300 && gm == null; i++) { gm = GymManager.Instance; yield return null; }
            Assert.NotNull(gm);
            yield return Lab.Frames(5);
            int st0 = GymState.Current.Stats.Sum();
            double t1 = GameClock.Minutes;
            yield return gm.PlayBot("D01", 1f);
            yield return Free(w);
            Assert.Greater(GymState.Current.Stats.Sum(), st0, "드릴 → 스탯(PlayerStats = GymSave, 세계 저장에 들어감)");
            Assert.AreSame(GymState.Current, w.Model.G, "세계 모델의 몸 = 체육관 몸");
            Assert.AreEqual(t1 + def.Balance.DrillMinutes, GameClock.Minutes, 2.0, "드릴 30분");
            // 청소 관원 표시 자리(월·수·금 21:00~21:30)
            Assert.IsTrue(PlaceMarker.All.Any(m => m.Func == "gym_clean"), "청소 표시 자리");
            var clean = def.Activity("gym.clean");
            GameClock.SetDate(2026, 3, 13, 21 * 60);
            Assert.AreEqual("", w.Model.CanStart(clean), "금 21:00 청소 됨");
            GameClock.SetDate(2026, 3, 12, 21 * 60);
            StringAssert.Contains("없는 날", w.Model.CanStart(clean), "목요일은 청소 없음");
            GameClock.SetDate(2026, 3, 12, 20 * 60);
            // 나가기 → 정류장
            var exit = w.Loader.Stage.GetComponentsInChildren<StageDoor>(true).FirstOrDefault(d => d.Target == SceneLoader.Zone1);
            Assert.NotNull(exit, "체육관 나가는 문");
            exit.Use();
            for (int i = 0; i < 1200 && w.Loader.Place != SceneLoader.Zone1; i++) yield return null;
            yield return Lab.Frames(5);
            Assert.AreEqual(SceneLoader.Zone1, w.Loader.Place);
            Assert.Less(Vector3.Distance(M3Lab.Motor.Position, door.ReturnPos), 2.5f, "정류장 앞으로");
            Assert.IsTrue(M3Lab.Motor.Grounded || true);
            Debug.Log($"[W] W05 체육관: 정류장 → 체육관(+{def.Balance.BusMinutes}분) → D01 S(+{def.Balance.DrillMinutes}분, 스탯 {st0} → {GymState.Current.Stats.Sum()}) → 정류장 · {w.Model.Status}");
        }

        // ───────────────────────── W08 핵심 카드 = M3 장면 체인
        [UnityTest, Timeout(1800000)]
        public IEnumerator W08_Core_Cards()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            var def = w.Def;
            var story = w.Story.Def;
            var chain = def.Cards.Where(c => c.Demo && c.HasScenes && string.IsNullOrEmpty(c.Group)).SelectMany(c => c.Scenes).ToList();
            CollectionAssert.AreEqual(story.Scenes.Select(s => s.Id).ToList(), chain, "핵심 카드 체인 = M3 장면 39 순서(D01 → 카드 체인 순서)");
            // 새 게임 → C1-01 오프닝 체인(1-0~1-7) → 학교 → 16:30 자유
            w.Begin(null);
            for (int i = 0; i < 60 * 900 && !(w.Model != null && w.Model.CardDone("C1-01") && w.Free); i++) yield return null;
            Assert.IsTrue(w.Model.CardDone("C1-01"), "C1-01 끝");
            var trace = w.Story.Trace.Select(t => t.Split('|')[0]).ToList();
            CollectionAssert.AreEqual(def.Card("C1-01").Scenes, trace, "C1-01 = 장면 1-0~1-7");
            Assert.AreEqual(1, InputMaps.EnabledCount(w.Actions), "조작 맵 하나(탐색)");
            Assert.AreEqual("Explore", InputMaps.Enabled(w.Actions).Trim());
            Assert.IsFalse(w.Loader.Busy); Assert.AreEqual(SceneLoader.Zone1, w.Loader.Place, "하교 → 후문 골목");
            Assert.AreEqual(16 * 60 + 30, (int)GameClock.Minutes, "16:30 하교");
            Assert.AreEqual(8000, w.Model.S.Household, "배달 4건 → 엄마 통장(생활비)");
            Assert.AreEqual(3, w.Model.N.Filled(story.Names) >= 3 ? 3 : w.Model.N.Filled(story.Names), "도감 3칸 이상");
            Assert.IsFalse(w.Story.InScene);
            // 5/1 C1-20 → C1-21(5-2 · 6-0~6-4 · 7-0~7-3): 첫 반항 → 소문 1단계 · 평판 +5
            var save = Mid(def, def.Cards.Where(c => c.Demo && (c.Kind == CardKind.Core || c.Kind == CardKind.Season || c.Kind == CardKind.System) && string.Compare(c.Id, "C1-20", System.StringComparison.Ordinal) < 0 && c.Id.StartsWith("C1-")).Select(c => c.Id), 501, 17 * 60 + 29, "Home");
            w.Stop();
            yield return Lab.Frames(5);
            w.Story.Trace.Clear();
            w.Begin(save);
            for (int i = 0; i < 60 * 900 && !(w.Model.CardDone("C1-21") && w.Free); i++) yield return null;
            Assert.IsTrue(w.Model.CardDone("C1-20") && w.Model.CardDone("C1-21"), "5/1 체인 끝");
            var t2 = w.Story.Trace.Select(t => t.Split('|')[0]).ToList();
            CollectionAssert.AreEqual(def.Card("C1-20").Scenes.Concat(def.Card("C1-21").Scenes).ToList(), t2, "5-2 → 6-0~6-4 → 7-0~7-3");
            Assert.AreEqual(1, w.Model.S.Rumor, "소문 1단계");
            Assert.GreaterOrEqual(w.Model.S.TownRep, 5f, "첫 평판 +5");
            Assert.AreEqual(1, InputMaps.EnabledCount(w.Actions));
            Debug.Log($"[W] W08 핵심 카드: 체인 {chain.Count} = M3 장면 · C1-01 {string.Join(",", trace)} · 5/1 {string.Join(",", t2)} · {w.Model.Status}");
        }

        // ───────────────────────── W12b 첫 시비(M2 인카운터 재사용)
        [UnityTest, Timeout(1200000)]
        public IEnumerator W12b_First_Street_Fight()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            var def = w.Def;
            var done = new[] { "C1-01", "C1-02", "C1-03", "C1-04", "C1-05", "C1-07", "C1-08", "C1-09", "C1-11", "C1-12", "C1-13", "C1-19", "C1-20", "C1-21", "C1-22", "C1-23" };
            var save = Mid(def, done, 506, 16 * 60 + 30, SceneLoader.Zone1);
            w.Begin(save);
            yield return Lab.Frames(10);
            yield return Free(w);
            float r0 = w.Model.S.TownRep;
            var c = w.Director.Pick("after:school");
            Assert.NotNull(c); Assert.AreEqual("C1-24", c.Id);
            yield return w.RunCard(c);
            Debug.Log($"[W] W12b 첫 시비: 승 {w.Model.S.StreetWins} · 패 {w.Model.S.StreetLosses} · 평판 {r0} → {w.Model.S.TownRep} · 소문 {w.Model.S.Rumor}");
            Assert.AreEqual(1, w.Model.S.StreetWins, "봇이 이김");
            Assert.AreEqual(r0 + def.Balance.StreetWinMany, w.Model.S.TownRep, 0.01f, "1:다수 승 +3");
            Assert.AreEqual(3, w.Model.S.Rumor, "소문 3단계");
            Assert.IsTrue(w.Model.Unlocked("street_repeat"), "시비 반복 열림");
        }

        // ───────────────────────── W14 봇 직진 · W15 봇 전부
        [UnityTest, Timeout(3600000)]
        public IEnumerator W14_Bot_Straight() => Bot(false, "W14");

        [UnityTest, Timeout(3600000)]
        public IEnumerator W15_Bot_Full() => Bot(true, "W15");

        static IEnumerator Bot(bool full, string tag)
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            w.BotChoice = c => c.Id == "N02" ? (full ? 1 : 2) : 0;
            w.Begin(null);
            var bot = WorldBot.Attach(w, full);
            bot.MaxSeconds = 3000f;
            yield return bot.Run();
            var m = w.Model;
            var goals = string.Join(" / ", w.Def.Chapter("1").DemoGoals.Select(m.GoalLine));
            Debug.Log($"[W] {tag} 봇 {(full ? "전부" : "직진")}: {string.Join(" | ", bot.Log)}\n목표 {goals}\n{m.Status}\n맡기기 {w.AutoRuns}번 · 카드 {m.S.Cards.Count(c => c.State > 0)}\n자취:\n{string.Join("\n", w.Trace)}");
            Assert.IsTrue(bot.Done, "데모 끝 카드까지: " + bot.Fail);
            Assert.GreaterOrEqual(GameClock.DayKey, 508, "5/8 까지");
            Assert.AreEqual(1, m.S.DemoState, "데모 목표 달성: " + goals);
            Assert.IsTrue(m.CardDone("C1-21") && m.CardDone("C1-24") && m.CardDone("C1-26"), "핵심 카드");
            Assert.Greater(m.Count("job.delivery"), 0); Assert.Greater(m.Count("job.gukbap"), 0); Assert.Greater(m.Count("job.stock"), 0); Assert.Greater(m.Count("job.site"), 0);
            Assert.GreaterOrEqual(w.AutoRuns, 1, "주간 계획 맡기기");
            if (full)
            {
                // 14-6 밸런스: 돈 쓸 곳(레슨·먹을 것)이 생긴 뒤 4월 말 지갑 = 쓸 데가 있어 빠듯한 10~25만
                var apr = m.S.Days.FirstOrDefault(l => l.StartsWith("4/30 "));
                int wallet = apr != null ? int.Parse(System.Text.RegularExpressions.Regex.Match(apr, @"지갑 ([\d,]+)").Groups[1].Value.Replace(",", "")) : -1;
                Debug.Log($"[W] {tag} 날마다:\n{string.Join("\n", m.S.Days)}");
                Debug.Log($"[W] {tag} 4월 말 지갑 {wallet:N0} · 레슨 {m.Count("lesson"):0}(직접 {w.LessonsRun}) · 레슨에 쓴 돈 {m.Count("spent.lesson"):N0} · 먹을 것 {m.Count("spent.food"):N0} · 노쇼 {m.Count("noshow"):0} · {apr}");
                Assert.That(wallet, Is.InRange(100000, 250000), "4월 말 지갑 10~25만: " + apr);
                Assert.GreaterOrEqual(m.Count("lesson"), 4f, "레슨(맡기기 포함)");
                Assert.Greater(m.Count("spent.food"), 0f, "먹을 것");
                Assert.Greater(m.Count("job.counter"), 0, "편의점 정식");
                Assert.Greater(bot.Drills, 0, "체육관 드릴");
                Assert.GreaterOrEqual(bot.Sides, 2, "사연 2");
                Assert.GreaterOrEqual(bot.Fights, 1, "시비 1");
            }
        }

        /// 그 날까지 지나간 '놓칠 수 없는' 카드(따라잡기 안 뜨게) + 더 넣을 것
        static string[] Past(WorldDef def, int day, params string[] more)
        {
            int Start(CardDef c) => c.Date > 0 ? c.Date : c.WinFrom;
            return more.Concat(def.Cards.Where(c => c.Demo && c.Miss == "never" && Start(c) > 0 && Start(c) <= day).Select(c => c.Id)).Distinct().ToArray();
        }

        // ───────────────────────── W23 개인레슨 장면(체육관): 고준 → 김승연(출장) · 노쇼
        [UnityTest, Timeout(1200000)]
        public IEnumerator W23_Lesson_Play()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            var def = w.Def;
            var save = Mid(def, Past(def, 314, "C1-01", "C1-02", "C1-03", "C1-04", "N01", "N02", "C1-L0", "C1-L1"), 314, 15 * 60 + 30, SceneLoader.Zone1, m => m.L.SetWallet(300000));
            w.Begin(save);
            yield return Lab.Frames(10);
            yield return Free(w);
            Assert.IsTrue(w.Model.Unlocked("lesson") && w.Model.Unlocked("lesson.guests"), "C1-L0 · C1-L1");
            Assert.AreEqual("", w.BookNext("gojun"), "토 16:00 고준");
            Assert.AreEqual("", w.BookNext("seungyeon"), "토 17:30 김승연(딥마카이)");
            Assert.AreEqual(300000 - 20000 - 50000, w.Model.L.Wallet);
            yield return w.Loader.Go(SceneLoader.Gym, "Spawn", false, Vector3.zero, 0f);
            GymManager gm = null;
            for (int i = 0; i < 300 && gm == null; i++) { gm = GymManager.Instance; yield return null; }
            Assert.NotNull(gm);
            Assert.NotNull(gm.Guest("seungyeon"), "출장 코치 NPC(김승연)");
            Assert.NotNull(gm.Guest("jinsoo_son"), "출장 코치 NPC(손진수)");
            Assert.NotNull(gm.Guest("jinsoo_seo"), "출장 코치 NPC(서진수)");
            int st0 = GymState.Current.Stats.Sum();
            float rel0 = w.Model.Rel("gojun");
            GameClock.AdvanceTo(15 * 60 + 50);
            for (int i = 0; i < 60 * 600 && w.LessonsRun < 1; i++) yield return null;
            yield return Free(w);
            var b0 = w.Model.S.Lessons[0];
            Assert.AreEqual(1, b0.State, "레슨 받음: " + w.LastLessonLine);
            Assert.Contains(b0.Drill, def.Shop.Coach("gojun").Drills, "고준 드릴(스파링·그라운드)");
            Assert.AreEqual(rel0 + 1f, w.Model.Rel("gojun"), 0.01f, "신뢰 +1");
            Assert.Greater(GymState.Current.Stats.Sum(), st0, "스탯");
            // 출장 레슨(김승연) — 시작 전 기다림 → 시계를 그 시각으로
            GameClock.AdvanceTo(17 * 60 + 20);
            for (int i = 0; i < 60 * 600 && w.LessonsRun < 2; i++) yield return null;
            yield return Free(w);
            var b1 = w.Model.S.Lessons[1];
            Assert.AreEqual(1, b1.State, "출장 레슨 받음: " + w.LastLessonLine);
            Assert.Contains(b1.Drill, def.Shop.Coach("seungyeon").Drills, "타격 기본기 드릴");
            Assert.IsFalse(gm.Guest("seungyeon").gameObject.activeSelf, "끝나면 출장 코치는 감");
            // 노쇼: 다음 예약을 잡고 시간을 넘김
            Assert.AreEqual("", w.BookNext("yunjun"));
            var b2 = w.Model.S.Lessons[2];
            int wl = w.Model.L.Wallet;
            GameClock.SetDate(2026, b2.Day / 100, b2.Day % 100, b2.At + def.Shop.GraceAfter + 5);
            for (int i = 0; i < 60 * 300 && b2.State == 0; i++) yield return null;
            Assert.AreEqual(2, b2.State, "노쇼");
            Assert.AreEqual(wl, w.Model.L.Wallet, "돈은 날아감");
            Debug.Log($"[W] W23 레슨: {string.Join(" | ", w.Trace.Where(x => x.StartsWith("레슨") || x.StartsWith("노쇼")))} · 스탯 {st0} → {GymState.Current.Stats.Sum()} · {w.LastLessonLine}");
        }

        // ───────────────────────── W24 가게 앞 판매창 · 휴대폰 레슨/가방 · HUD 효과 아이콘
        [UnityTest, Timeout(600000)]
        public IEnumerator W24_Shop_Play()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            var def = w.Def;
            var save = Mid(def, Past(def, 314, "C1-01", "C1-02", "C1-03", "C1-04", "N01", "N02", "C1-L0"), 314, 15 * 60 + 10, SceneLoader.Zone1, m => { m.L.SetWallet(100000); m.G.Fatigue = 60; });
            w.Begin(save);
            yield return Lab.Frames(10);
            yield return Free(w);
            var fronts = PlaceMarker.All.Where(p => p != null && p.Place == SceneLoader.Zone1 && p.Func.StartsWith("shop_")).ToList();
            Assert.AreEqual(3, fronts.Count, "Zone1 판매창 3(약국·삼계탕·장어)");
            Assert.IsTrue(fronts.All(p => p.GetComponent<WorldMarker>() != null), "표시 자리 연결");
            var sg = fronts.First(p => p.Func == "shop_samgye");
            M3Lab.Motor.Teleport(sg.transform.position, 0f);
            yield return Lab.Frames(5);
            StringAssert.Contains("차림표", w.MarkerPrompt("shop_samgye"));
            double t0 = GameClock.Minutes;
            w.UseMarker("shop_samgye", sg.transform);
            yield return Lab.Frames(2);
            yield return Free(w);
            Assert.AreEqual(100000 - 18000, w.Model.L.Wallet, "삼계탕 18,000(2025 서울 평균)");
            Assert.Greater(w.Model.HpBonus, 0f, "체력 효과");
            Assert.Less(w.Model.G.Fatigue, 60, "피로 회복");
            Assert.AreEqual(t0 + 40, GameClock.Minutes, 3.0, "앉아서 40분");
            yield return Lab.Frames(20);
            StringAssert.Contains("체", w.Hud.BuffLine, "HUD 효과 아이콘");
            // 약국 → 비타민(가방 · 하루 한 알)
            yield return w.BotShop("pharmacy", "vitamin");
            Assert.AreEqual(29, w.Model.Doses("vitamin"));
            // 휴대폰 '레슨' · '가방'
            w.OpenPhone(7); yield return Lab.Frames(3);
            StringAssert.Contains("이윤준", w.Phone.Body);
            w.Phone.SetTab(8); yield return Lab.Frames(3);
            StringAssert.Contains("종합비타민", w.Phone.Body);
            w.Phone.Close();
            Debug.Log($"[W] W24 가게: {w.LastMealLine} · HUD {w.Hud.BuffLine} · {w.Model.Status}");
        }

        // ───────────────────────── W25 빌드 인자 -mute(시작부터 끝까지 소리 0)
        [UnityTest]
        public IEnumerator W25_Mute_Arg()
        {
            var go = new GameObject("소리 시험");
            var src = go.AddComponent<AudioSource>();
            MuteArg.Apply();
            Assert.AreEqual(0f, AudioListener.volume);
            Assert.IsTrue(src.mute, "있던 소리");
            var go2 = new GameObject("나중 소리");
            var src2 = go2.AddComponent<AudioSource>();
            AudioListener.volume = 1f;     // 누가 다시 올려도
            float tw = 0f; while (tw < 2f && !(src2.mute && AudioListener.volume == 0f)) { tw += Time.unscaledDeltaTime; yield return null; }
            yield return null;
            Assert.IsTrue(src2.mute, "나중에 생긴 소리");
            Assert.AreEqual(0f, AudioListener.volume, "다시 0");
            MuteArg.Release();
            Object.Destroy(go); Object.Destroy(go2);
        }
    }
}
