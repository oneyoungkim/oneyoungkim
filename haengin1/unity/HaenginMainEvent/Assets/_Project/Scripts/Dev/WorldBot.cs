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
        /// 돈 쓰기(Full 봇 — docs/13 14-6 밸런스): 레슨 예약·출석 · 먹을 것 · 영양제. 남겨 둘 돈(이 밑으로는 안 씀)
        public bool Spend = true;
        public int Reserve = 60000;
        public int Booked, Meals, Snacks, Supps;
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
            Log.Add($"봇 끝: {(Done ? "데모 끝 카드" : "실패 " + Fail)} · {GameClock.DateText} · 실제 {Seconds:F1}초 · 카드 {W.CardsRun} · 활동 {W.ActivitiesRun} · 잠 {W.Sleeps} · 드릴 {Drills} · 사연 {Sides} · 시비 {Fights} · 레슨 {W.LessonsRun}(예약 {Booked}·노쇼 {M.Count("noshow"):0}) · 식사 {Meals} · 간식 {Snacks} · 영양제 {Supps} · 알바 [{string.Join(",", JobsDone)}]");
        }

        /// 지금 시각에 할 일 하나
        IEnumerator Step()
        {
            int now = M.Now;
            string w = M.Weekday;
            var b = W.Def.Balance;
            if (now < b.Wake) { yield return Do("Home", "sleep"); yield break; }
            // 돈 쓰기(Full): 하루 한 번 예약·영양제 → 오늘 레슨이 가까우면 체육관으로
            if (Full && Spend)
            {
                PlanSpending();
                var nx = M.NextLesson();
                if (nx != null && nx.Day == M.Today && now >= nx.At - 45 && now <= nx.At + M.Sh.GraceAfter)
                {
                    int l0 = W.LessonsRun;
                    if (W.Loader.Place != SceneLoader.Gym) yield return Go(SceneLoader.Gym);
                    if (M.Now < nx.At - 10) GameClock.AdvanceTo(nx.At - 10);
                    for (int i = 0; i < 240 && W.Free && W.LessonsRun == l0; i++) yield return null;
                    for (int i = 0; i < 60 * 900 && !W.Free; i++) yield return null;
                    if (W.LessonsRun == l0) { Log.Add($"{GameClock.DateText} {GameClock.TimeText} 레슨 못 받음 {nx.Coach}"); GameClock.Advance(30); }
                    yield break;
                }
                if (w == "토" && now >= 15 * 60 && now < 15 * 60 + 40 && M.Count("bot.meal." + M.Today) < 1 && M.G.Fatigue >= 45 && M.L.Wallet >= Reserve + 18000)
                {
                    M.AddCount("bot.meal." + M.Today, 1);
                    yield return Shop("samgye", "samgyetang"); Meals++;
                    yield break;
                }
            }
            // 새벽 배달
            if (Can("job.delivery")) { yield return Do("Gukbap", "job_delivery"); yield break; }
            // 토 노가다
            if (Can("job.site")) { yield return Do("Site", "job_site"); yield break; }
            // 학교
            if (M.SchoolDay(M.Today) && Can("school")) { yield return Do("School", "seat"); yield break; }
            // 일 편의점 정식
            if (Can("job.counter")) { yield return Do("Conv", "job_counter"); yield return AfterWork("Conv"); yield break; }
            // 오후: 사연(Full) — 와룡공원 벽보 → 고양이 자리
            if (Full && now >= 16 * 60 && now < 17 * 60 + 30)
            {
                if (!M.CardDone("N09") && M.Today >= 307 && M.Today <= 430) { yield return Point("waryong"); if (M.CardDone("N09")) yield break; }
                if (M.F.Has("cat_search") && !M.CardDone("N09b")) { yield return Point("cat_spot"); if (M.CardDone("N09b")) yield break; }
            }
            // 저녁: 체육관(Full · 회원이면 화·목) 또는 국밥집
            bool gymDay = Full && M.S.GymMember > 0 && (w == "화" || w == "목") && now < 20 * 60;
            if (gymDay && M.Count("gym.day." + M.Today) < 1) { M.AddCount("gym.day." + M.Today, 1); yield return Gym(2); yield break; }
            if (Can("job.gukbap")) { yield return Do("Gukbap", "job_serving"); yield return AfterWork("Gukbap"); yield break; }
            // 밤: 청소 관원(월·수·금) 또는 입고
            if (Full && Can("gym.clean")) { yield return GymClean(); yield break; }
            if (Can("job.stock")) { yield return Do("Conv", "job_stock"); yield return AfterWork("Conv"); yield break; }
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
            // 오늘 레슨이 남았으면 그때까지 기다림
            if (Full && Spend) { var nl = M.NextLesson(); if (nl != null && nl.Day == M.Today && nl.At - 45 > now) { GameClock.AdvanceTo(nl.At - 45); yield break; } }
            // 집에서 잠
            yield return Do("Home", "sleep");
        }

        // ───────────────────────── 돈 쓰기(14-6)
        /// 오늘 받을 레슨과 겹치나(15분 전 ~ 끝)
        bool LessonClash(int from, int to)
        {
            foreach (var bk in M.LessonsOn(M.Today))
                if (from < bk.At + M.Sh.Minutes && to > bk.At - M.Sh.GraceBefore) return true;
            return false;
        }

        /// 하루 한 번: 영양제(있으면 먹고, 없으면 일요일에 약국) · 레슨 예약(이윤준 주 1 · 고준 주 1 · 출장 코치 주 1 돌아가며)
        void PlanSpending()
        {
            if (M.Count("bot.plan." + M.Today) > 0) return;
            M.AddCount("bot.plan." + M.Today, 1);
            var vit = M.Sh.Food("vitamin");
            if (vit != null && M.Doses("vitamin") > 0 && M.CanEat(vit, true).Length == 0) { M.Eat(vit, true); Supps++; }
            // 매주 고정(사람이 PT 를 고정 시간에 잡듯): 이윤준 화·목 21:15 · 고준 토 16:00 · 출장이 열리면 김승연 토 17:30 — 주간 계획 맡기기 중에도 그 날 아침 자동 예약
            foreach (var (id, wd, at) in StandingPlan)
            {
                var c = M.Sh.Coach(id);
                if (c == null || !M.CoachOpen(c)) continue;
                int key = -1;
                for (int d = 0; d < 7 && key < 0; d++) { int k = GameClock.KeyPlus(M.Today, d); if (GameClock.WeekdayOfKey(k) == wd) key = k; }
                if (key >= 0 && !M.IsStanding(id, key, at)) M.SetStanding(c, key, at);
            }
            var yj = M.Sh.Coach("yunjun");
            if (yj != null && M.CoachOpen(yj) && M.Tickets("yunjun") == 0 && M.L.Wallet >= yj.Ticket10 + Reserve) M.BuyTicket(yj);
            int l0 = M.S.Lessons.Count;
            M.RenewStanding(M.Today);
            Booked += M.S.Lessons.Count - l0;
        }

        /// 봇의 매주 고정 레슨(코치 · 요일 · 시각)
        public (string id, string wd, int at)[] StandingPlan = { ("yunjun", "화", 21 * 60 + 15), ("yunjun", "목", 21 * 60 + 15), ("gojun", "토", 16 * 60), ("seungyeon", "토", 17 * 60 + 30), ("jinsoo_seo", "일", 19 * 60 + 30) };

        /// 일 끝나고(피로가 높으면): 편의점 = 에너지바(지갑 여유면) · 국밥집 = 특국밥(직원가) · 일요일 아침 약국
        IEnumerator AfterWork(string place)
        {
            if (!Full || !Spend || !W.Active) yield break;
            if (place == "Conv" && M.G.Fatigue >= 40 && M.L.Wallet >= Reserve + 2000) { yield return W.BotShop("conv", M.G.Fatigue >= 60 ? "protein" : "bar"); Snacks++; }
            if (place == "Gukbap" && M.G.Fatigue >= 60 && M.L.Wallet >= Reserve + 9000) { yield return W.BotShop("gukbap", "gukbap_special"); Meals++; }
            if (place == "Conv" && M.Weekday == "일" && M.Doses("vitamin") <= 2 && M.L.Wallet >= Reserve + 18000) { yield return Shop("pharmacy", "vitamin"); }
        }

        /// 가게에 가서 고름(Zone1 판매창이면 그 앞으로)
        public IEnumerator Shop(string shopId, string foodId)
        {
            var sp = M.Sh.Shop(shopId);
            if (sp == null) yield break;
            if (sp.Place == SceneLoader.Zone1)
            {
                var pm = PlaceMarker.All.FirstOrDefault(m => m != null && m.Func == "shop_" + shopId);
                if (W.Loader.Place != SceneLoader.Zone1 || pm != null) yield return W.Loader.Go(SceneLoader.Zone1, null, pm != null, pm != null ? pm.transform.position : Vector3.zero, 0f);
                for (int i = 0; i < 30; i++) yield return null;
                for (int i = 0; i < 60 * 300 && !W.Free; i++) yield return null;
            }
            else yield return Go(sp.Place);
            yield return W.BotShop(shopId, foodId);
            for (int i = 0; i < 60 * 300 && !W.Free; i++) yield return null;
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
            if (Full && Spend && LessonClash(Mathf.Max(at, a.Open), a.End)) return false;
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
