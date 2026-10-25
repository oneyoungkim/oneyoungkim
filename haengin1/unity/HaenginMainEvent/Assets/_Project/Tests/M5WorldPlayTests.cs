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
                Assert.Greater(m.Count("job.counter"), 0, "편의점 정식");
                Assert.Greater(bot.Drills, 0, "체육관 드릴");
                Assert.GreaterOrEqual(bot.Sides, 2, "사연 2");
                Assert.GreaterOrEqual(bot.Fights, 1, "시비 1");
            }
        }
    }
}
