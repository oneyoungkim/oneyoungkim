// 행인1의 메인이벤트 — 1장 데모 봇(docs/13 12-4 11단계 · W14·W15 · 빌드 -ch1smoke): 하루 일과를 사람처럼 고른다(장소로 옮겨 표시 자리를 씀).
//   새벽 배달 → (평일) 학교 → (오후) 동네 사연 → 저녁 국밥집 또는 체육관 → 밤 입고 → 집에서 잠 / 토 노가다 / 일 편의점 / 일요일 밤 주간 계획(맡기기)
//   카드는 WorldRunner 가 알아서(시각·장소·조건). 봇 모드라 대사·선택·안내는 짧게 넘김(WorldRunner.BotSkill > 0).
// Full = 체육관(체험 뒤 청소 관원·드릴)·사연 2·시비까지 다 들름(W15). 아니면 직진(W14).
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Haengin
{
    public sealed class WorldBot : MonoBehaviour
    {
        public WorldRunner W;
        public bool Full;
        /// 이 날짜(열쇠)를 넘으면 멈춤(안전장치)
        public int StopAfter = 515;
        /// 실제 시간 상한(초)
        public float MaxSeconds = 1500f;
        /// 녹화: 이만큼 자면 멈춤(−1 = 데모 끝까지)
        public int MaxSleeps = -1;
        public bool Done { get; private set; }
        public string Fail { get; private set; } = "";
        public float Seconds { get; private set; }
        public readonly List<string> Log = new List<string>();
        public readonly HashSet<string> JobsDone = new HashSet<string>();
        public int Drills, Sides, Fights;
        bool hooked;

        WorldModel M => W.Model;

        public static WorldBot Attach(WorldRunner w, bool full)
        {
            var b = w.gameObject.GetComponent<WorldBot>() ?? w.gameObject.AddComponent<WorldBot>();
            b.W = w; b.Full = full; b.enabled = true;
            return b;
        }

        public IEnumerator Run()
        {
            float t0 = Time.realtimeSinceStartup;
            Done = false; Fail = ""; Log.Clear();
            if (!hooked)
            {
                hooked = true;
                W.CardStarted += c => { if (c.Kind == CardKind.Side) Sides++; if (c.Fight == "street") Fights++; };
                W.ActivityDone += (a, r) => JobsDone.Add(a.Id);
            }
            for (int i = 0; i < 3000 && !(W.Active && W.Free && W.Model != null && (W.CardsRun > 0 || W.Model.S.Cards.Count > 0)); i++) yield return null;
            int guard = 0;
            while (!Done && guard++ < 20000)
            {
                Seconds = Time.realtimeSinceStartup - t0;
                if (Seconds > MaxSeconds) { Fail = $"시간 초과 {Seconds:F0}초 · {GameClock.DateText}"; break; }
                if (!W.Active) { Fail = "WorldRunner 꺼짐"; break; }
                if (M.S.DemoState != 0) { Done = true; break; }
                if (MaxSleeps >= 0 && W.Sleeps >= MaxSleeps && W.Free) { Done = true; break; }
                if (M.Today > StopAfter) { Fail = $"{StopAfter / 100}/{StopAfter % 100} 넘음 — 데모 끝 안 옴"; break; }
                if (!W.Free) { guard--; yield return null; continue; }
                yield return Step();
                yield return null;
            }
            Seconds = Time.realtimeSinceStartup - t0;
            Log.Add($"봇 끝: {(Done ? "데모 끝 카드" : "실패 " + Fail)} · {GameClock.DateText} · 실제 {Seconds:F1}초 · 카드 {W.CardsRun} · 활동 {W.ActivitiesRun} · 잠 {W.Sleeps} · 드릴 {Drills} · 사연 {Sides} · 시비 {Fights} · 알바 [{string.Join(",", JobsDone)}]");
        }

        /// 지금 시각에 할 일 하나
        IEnumerator Step()
        {
            int now = M.Now;
            string w = M.Weekday;
            var b = W.Def.Balance;
            if (now < b.Wake) { yield return Do("Home", "sleep"); yield break; }
            // 새벽 배달
            if (Can("job.delivery")) { yield return Do("Gukbap", "job_delivery"); yield break; }
            // 토 노가다
            if (Can("job.site")) { yield return Do("Site", "job_site"); yield break; }
            // 학교
            if (M.SchoolDay(M.Today) && Can("school")) { yield return Do("School", "seat"); yield break; }
            // 일 편의점 정식
            if (Can("job.counter")) { yield return Do("Conv", "job_counter"); yield break; }
            // 오후: 사연(Full) — 와룡공원 벽보 → 고양이 자리
            if (Full && now >= 16 * 60 && now < 17 * 60 + 30)
            {
                if (!M.CardDone("N09") && M.Today >= 307 && M.Today <= 430) { yield return Point("waryong"); if (M.CardDone("N09")) yield break; }
                if (M.F.Has("cat_search") && !M.CardDone("N09b")) { yield return Point("cat_spot"); if (M.CardDone("N09b")) yield break; }
            }
            // 저녁: 체육관(Full · 회원이면 화·목) 또는 국밥집
            bool gymDay = Full && M.S.GymMember > 0 && (w == "화" || w == "목") && now < 20 * 60;
            if (gymDay && M.Count("gym.day." + M.Today) < 1) { M.AddCount("gym.day." + M.Today, 1); yield return Gym(2); yield break; }
            if (Can("job.gukbap")) { yield return Do("Gukbap", "job_serving"); yield break; }
            // 밤: 청소 관원(월·수·금) 또는 입고
            if (Full && Can("gym.clean")) { yield return GymClean(); yield break; }
            if (Can("job.stock")) { yield return Do("Conv", "job_stock"); yield break; }
            // 3화: 국밥집 일 끝나고 하루편의점 앞(21:00~21:30) — 입고가 열리는 자리
            if (!M.Unlocked("job.stock") && M.CardDone("C1-04") && M.Today >= 309 && "월화수목금".Contains(w) && now <= 21 * 60 + 30 && now >= 20 * 60 && M.Count("conv.visit." + M.Today) < 1)
            {
                M.AddCount("conv.visit." + M.Today, 1);
                GameClock.AdvanceTo(21 * 60);
                yield return Go("Conv");
                yield break;
            }
            // 오늘 아직 안 온 '시각' 핵심 카드 — 그 시각까지 기다림
            int tc = W.NextTimedCard();
            if (tc >= 0 && tc < 23 * 60) { GameClock.AdvanceTo(tc); for (int i = 0; i < 30; i++) yield return null; yield break; }
            // 사연(Full): 오후까지 기다렸다가
            if (Full && now < 16 * 60 && SidePending()) { GameClock.AdvanceTo(16 * 60); yield break; }
            // 아직 이른 시각이면 다음 일 시각까지 기다림(시계를 그 시각으로)
            int next = NextStart(now);
            if (next > now && next < 22 * 60) { GameClock.AdvanceTo(next); yield return null; yield break; }
            // 집에서 잠
            yield return Do("Home", "sleep");
        }

        bool SidePending() => (!M.CardDone("N09") && M.Today >= 307 && M.Today <= 430) || (M.F.Has("cat_search") && !M.CardDone("N09b"));

        bool Can(string id)
        {
            var a = W.Def.Activity(id);
            if (a == null) return false;
            int now = M.Now;
            int at = Mathf.Max(now, a.Open);
            if (at > a.Latest) return false;
            if (M.CanStart(a, at).Length > 0) return false;
            if (W.TimedCardInside(a, Mathf.Max(at, a.Start), out _)) return false;
            if (now < a.Open) return a.Open - now <= 90;     // 한 시간 반 안이면 미리 가서 기다림
            return true;
        }

        int NextStart(int now)
        {
            int best = int.MaxValue;
            foreach (var a in W.Def.Activities)
            {
                if (a.Kind != "job" && a.Kind != "school" && !(Full && a.Kind == "gym")) continue;
                if (!M.Unlocked(a.Unlock) || !a.OnDay(M.Weekday) || M.DoneToday(a.Id) || now > a.Latest) continue;
                int at = Mathf.Max(now, a.Open);
                if (M.CanStart(a, at).Length > 0) continue;
                best = Mathf.Min(best, a.Open);
            }
            return best == int.MaxValue ? -1 : best;
        }

        /// 그 장소로 옮겨 표시 자리를 씀
        public IEnumerator Do(string place, string func)
        {
            var a = W.Def.ActivityForMarker(func);
            if (a != null && M.Now < a.Open) GameClock.AdvanceTo(a.Open);
            yield return Go(place);
            if (!W.Active) yield break;
            var pm = PlaceMarker.All.FirstOrDefault(m => m != null && m.Func == func && m.isActiveAndEnabled);
            if (pm == null) { Log.Add($"{GameClock.DateText} {place}/{func} 표시 자리 없음"); GameClock.Advance(30); yield break; }
            int c0 = W.ActivitiesRun + W.Sleeps;
            W.UseMarker(func, pm.transform);
            yield return null;
            for (int i = 0; i < 60 * 900 && !W.Free; i++) yield return null;
            if (W.ActivitiesRun + W.Sleeps == c0 && a != null && a.Kind != "sleep") { Log.Add($"{GameClock.DateText} {GameClock.TimeText} {a.Id} 못 함: {M.CanStart(a)}"); GameClock.Advance(20); }
        }

        public IEnumerator Go(string place)
        {
            var L = W.Loader;
            if (L == null) yield break;
            if (place == SceneLoader.Zone1) yield break;
            if (L.Place != place)
            {
                if (place == SceneLoader.Gym) GameClock.Advance(W.Def.Balance.BusMinutes);
                yield return L.Go(place, place == "Home" || place == SceneLoader.Gym || place == "Site" ? "Spawn" : "Door", false, Vector3.zero, 0f);
            }
            // 감독이 장소 진입을 보게 두 번
            for (int i = 0; i < 30; i++) yield return null;
            for (int i = 0; i < 60 * 600 && !W.Free; i++) yield return null;
        }

        public IEnumerator Point(string id)
        {
            var pt = W.Def.Balance.Point(id);
            if (pt == null) yield break;
            if (W.Loader.Place != SceneLoader.Zone1 || true) yield return W.Loader.Go(SceneLoader.Zone1, null, true, pt.Pos, 0f);
            for (int i = 0; i < 40; i++) yield return null;
            for (int i = 0; i < 60 * 600 && !W.Free; i++) yield return null;
            // 지점 밖으로 한 번 나갔다(다시 들어올 수 있게)
            var away = pt.Pos + new Vector3(pt.Radius + 3f, 0f, 0f);
            if (W.Loader.Motor != null) W.Loader.Motor.Teleport(away, 0f);
            for (int i = 0; i < 20; i++) yield return null;
            GameClock.Advance(10);
        }

        public IEnumerator Gym(int drills)
        {
            yield return Go(SceneLoader.Gym);
            GymManager gm = null;
            for (int i = 0; i < 600 && gm == null; i++) { gm = GymManager.Instance; yield return null; }
            if (gm == null) yield break;
            yield return null; yield return null;
            string[] order = { "D01", "D05", "D04", "D02", "D06", "D12" };
            int n = 0;
            foreach (var id in order)
            {
                if (n >= drills) break;
                var def = gm.Book != null ? gm.Book.Find(id) : null;
                if (def == null || !def.Playable) continue;
                if (M.G.Fatigue >= W.Def.Balance.GymBlock - 15) break;
                bool started = false;
                if (DrillSim.CanSim(def, M.G, GymSave.Today)) { yield return gm.SimCo(def); started = true; for (int i = 0; i < 600 && gm.Busy; i++) { gm.CloseCard(); yield return null; } }
                else { yield return gm.PlayBot(id, Mathf.Max(0.8f, W.BotSkill)); started = true; }
                if (started) { n++; Drills++; }
                for (int i = 0; i < 600 && !W.Free; i++) yield return null;
            }
            // 나오기(마을버스)
            var exit = W.Loader.Stage != null ? W.Loader.Stage.GetComponentsInChildren<StageDoor>(true).FirstOrDefault(d => d.Target == SceneLoader.Zone1) : null;
            if (exit != null) { exit.Use(); for (int i = 0; i < 600 && (W.Loader.Busy || W.Loader.Place != SceneLoader.Zone1); i++) yield return null; }
            for (int i = 0; i < 60 * 300 && !W.Free; i++) yield return null;
        }

        IEnumerator GymClean()
        {
            var a = W.Def.Activity("gym.clean");
            if (a != null && M.Now < a.Open) GameClock.AdvanceTo(a.Open);
            yield return Go(SceneLoader.Gym);
            for (int i = 0; i < 60 * 30 && !PlaceMarker.All.Any(m => m != null && m.Func == "gym_clean"); i++) yield return null;
            yield return Do(SceneLoader.Gym, "gym_clean");
            yield return Gym(1);
        }
    }
}
