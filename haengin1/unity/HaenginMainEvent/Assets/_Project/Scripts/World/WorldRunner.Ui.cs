// 행인1의 메인이벤트 — 오픈월드 진행: 화면(HUD·휴대폰·길잡이) · 시간대 조명 · 저장 · 문(마을버스·현장)·표시 자리 붙이기.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Haengin
{
    public sealed partial class WorldRunner
    {
        // ───────────────────────── HUD
        void UpdateHud()
        {
            if (Hud == null || Model == null) return;
            bool show = busy.Count == 0 && (Story == null || !Story.InScene) && (Phone == null || !Phone.Shown) && (ChapterView == null || !ChapterView.Shown) && (Job == null || !Job.Shown);
            Hud.Shown = show;
            if (!show) return;
            string band = Model.Band(Model.Now);
            string date = $"{GameClock.Month}월 {GameClock.Day}일({GameClock.Weekday}) {GameClock.TimeText} · {band}{(Model.RainToday ? " · 비" : "")}";
            int due = Def.Balance.FixedTotal(GameClock.Month);
            string money = $"지갑 {Ledger.Won(Model.L.Wallet)} · 봉투 {Ledger.Won(Model.L.Envelope)} · 생활비 {Ledger.Won(Model.S.Household)}/{Ledger.Won(due)}";
            var ch = Def.Chapter(Model.S.Chapter);
            string goal = "";
            if (ch != null)
            {
                var gs = ch.HasDemo && Model.S.DemoState == 0 ? ch.DemoGoals : ch.Goals;
                int done = gs.Count(g => Model.GoalMet(g));
                goal = Model.S.DemoState == 1 ? "데모 목표 달성 — 계속 돌아다녀도 된다" : $"목표: {(ch.HasDemo ? ch.DemoGoal : ch.Goal)} ({done}/{gs.Length})";
            }
            Hud.Set(date, money, Model.G.Fatigue, goal, NextHint(out _, out _));
            HudBuffs();
        }

        /// '다음' 한 줄 + 길잡이 자리(Zone1 지점)
        string NextHint(out string pointId, out string label)
        {
            pointId = ""; label = "";
            int now = Model.Now;
            // 오늘 시각이 박힌 핵심 카드
            foreach (var c in Director.Today())
            {
                if (c.From < 0 || c.Kind != CardKind.Core) continue;
                pointId = PointFor(c.Place, c.Zone);
                label = c.Title;
                return $"◆ {GameClock.Hm(c.From)} 「{c.Title}」 — {PlaceName(c.Place)}";
            }
            if (now >= 22 * 60 || now < Def.Balance.Wake) { pointId = "home_door"; label = "집"; return "다음: 집에서 잠(이불)"; }
            ActivityDef best = null;
            foreach (var a in Def.Activities)
            {
                if (a.Kind != "job" && a.Kind != "school" && a.Kind != "gym") continue;
                if (!Model.Unlocked(a.Unlock) || !a.OnDay(Model.Weekday) || Model.DoneToday(a.Id)) continue;
                if (a.Id == "school" && Model.Holiday(Model.Today)) continue;
                if (now > a.Latest) continue;
                if (Model.CanStart(a, Mathf.Max(now, a.Open)).Length > 0) continue;
                if (best == null || a.Start < best.Start) best = a;
            }
            if (best == null) return "다음: 자유 — 동네·체육관·집";
            pointId = PointFor(best.Place, "");
            label = best.Name;
            return $"다음: {best.Name} {GameClock.Hm(best.Start)} — {PlaceName(best.Place)}";
        }

        static string PointFor(string place, string zone) => place switch
        {
            "Gukbap" => "gukbap_door", "Conv" => "conv_door", "Home" => "home_door", "School" => "school_sign", "Site" => "site_sign", "Gym" => "bus_stop",
            _ => zone == "hoomun_parking" ? "parking" : zone == "waryong_entrance" ? "waryong" : zone == "top_house" ? "top_house" : "",
        };

        void UpdateGuide()
        {
            var g = RouteGuide.Current;
            if (g == null || Loader == null || Loader.Place != SceneLoader.Zone1) return;
            NextHint(out string pid, out string label);
            if (label == guideLabel) return;
            guideLabel = label;
            var pt = Def.Balance.Point(pid);
            if (pt == null) { g.ClearTargets(); return; }
            g.SetTargets(new[] { new TargetDef { Label = label, Pos = pt.Pos, Radius = pt.Radius } });
        }

        // ───────────────────────── 시간대 조명(연속 보간 — 바깥만)
        void ApplyLight()
        {
            var day = Story != null ? Story.Light : null;
            if (day == null || Loader == null) return;
            if (Loader.Place != SceneLoader.Zone1 || (Story.InScene && !Free)) return;
            day.ApplyClock(GameClock.Minutes);
        }

        // ───────────────────────── 휴대폰
        void PhoneKey()
        {
            var kb = Keyboard.current; var gp = Gamepad.current;
            bool k = (kb != null && kb.tabKey.wasPressedThisFrame) || (gp != null && gp.selectButton.wasPressedThisFrame);
            if (k) OpenPhone(0);
        }

        public void OpenPhone(int tab)
        {
            if (Phone == null || !Active) return;
            if (GameState.Paused) GameState.SetPaused(false);
            Phone.Content = PhoneText;
            Phone.Open(tab);
        }

        string PhoneText(int tab)
        {
            if (tab >= 7) return ShopPhoneText(tab);
            var sb = new StringBuilder();
            var b = Def.Balance;
            switch (tab)
            {
                case 0:
                    foreach (var m in Model.S.Phone.Skip(Mathf.Max(0, Model.S.Phone.Count - 14))) sb.Append($"{m.Day / 100}/{m.Day % 100} {GameClock.Hm(m.Min)}  <b>{m.From}</b>  {m.Text}\n");
                    if (sb.Length == 0) sb.Append("문자 없음");
                    break;
                case 1:
                    sb.Append($"<b>오늘 {GameClock.DateText}</b>{(Model.RainToday ? " · 비" : "")} — 뜰 수 있는 이야기\n");
                    foreach (var c in Director.Today()) sb.Append($"◆ {(c.From >= 0 ? GameClock.Hm(c.From) + " " : "")}「{c.Title}」 — {PlaceName(c.Place)}\n");
                    sb.Append("\n<b>열린 곳</b>\n");
                    foreach (var p in b.Points) sb.Append($"· {p.Label}\n");
                    break;
                case 2:
                    sb.Append($"<b>{GameClock.DateText} 일정</b>\n");
                    foreach (var a in Def.Activities.Where(x => x.Kind == "job" || x.Kind == "school" || x.Kind == "gym"))
                    {
                        if (!Model.Unlocked(a.Unlock)) continue;
                        string st = Model.DoneToday(a.Id) ? "끝" : !a.OnDay(Model.Weekday) ? "오늘 없음" : Model.Now > a.Latest ? "지남" : $"{GameClock.Hm(a.Start)}~{GameClock.Hm(a.End)}";
                        sb.Append($"· {a.Name} — {st}{(Model.CanDelegate(a) ? " · 맡기기 됨" : "")}\n");
                    }
                    sb.Append($"\n내일 날씨: {(Model.RainTomorrow ? "비" : "맑음")}\n");
                    if (Model.S.Plan.Count > 0) sb.Append($"주간 계획 {Model.S.Plan.Count}일치\n");
                    sb.Append("\n<b>지난 날</b>\n");
                    foreach (var d in Model.S.Days.Skip(Mathf.Max(0, Model.S.Days.Count - 6))) sb.Append(d).Append('\n');
                    break;
                case 3:
                    {
                        int due = b.FixedTotal(GameClock.Month);
                        sb.Append($"지갑 <b>{Ledger.Won(Model.L.Wallet)}원</b> · {Model.L.EnvelopeText}\n");
                        sb.Append($"{GameClock.Month}월 생활비(엄마 통장) {Ledger.Won(Model.S.Household)} / 말일 {Ledger.Won(due)}\n");
                        foreach (var f in b.Fixed) sb.Append($"  · {f.Name} {Ledger.Won(f.Amount)}\n");
                        foreach (var h in Model.S.Holes) sb.Append($"  · 집 구멍 {h.Name} {Ledger.Won(h.Amount)}{(h.Paid ? "(메움)" : "")}\n");
                        sb.Append($"결손 {Model.S.Deficits}회 · 번 돈 {Ledger.Won(Model.L.Earned)}\n\n");
                        foreach (var e in Model.L.Entries.Skip(Mathf.Max(0, Model.L.Entries.Count - 8))) sb.Append("· ").Append(e).Append('\n');
                        break;
                    }
                case 4:
                    {
                        var ch = Def.Chapter(Model.S.Chapter);
                        if (ch == null) break;
                        sb.Append($"<b>{ch.Label}</b> {ch.Period}\n");
                        if (ch.HasDemo) { sb.Append($"\n<b>데모 — {ch.DemoGoal}</b>\n"); foreach (var g in ch.DemoGoals) sb.Append(Model.GoalLine(g)).Append('\n'); }
                        sb.Append($"\n<b>1장 — {ch.Goal}</b>\n");
                        foreach (var g in ch.Goals) sb.Append(Model.GoalLine(g)).Append('\n');
                        break;
                    }
                case 5:
                    sb.Append($"피로 {Model.G.Fatigue}/100 · 땀 {Model.G.Sweat}\n");
                    for (int i = 0; i < 8; i++) sb.Append($"{GymSave.StatNames[i]} {Model.G.Stats[i] + Model.S.StatFrac[i]:0.0}   ");
                    sb.Append('\n');
                    foreach (var a in Def.Activities.Where(x => x.IsJob && Model.Unlocked(x.Unlock)))
                        sb.Append($"· {a.Name} — {Model.Count(a.Id):0}번 · 최고 {DrillGrader.Letter((Grade)Mathf.RoundToInt(Model.Count("best." + a.Id)))}{(Model.CanDelegate(a) ? " · 맡기기" : "")}\n");
                    sb.Append($"체육관: {(Model.S.GymMember == 1 ? "관원" : Model.S.GymMember == 2 ? "청소 관원" : Model.S.GymTrial ? "체험함" : "모름")}\n");
                    foreach (var d in Model.G.Drills.Where(x => x.HasBest)) sb.Append($"  {d.Id} 최고 {DrillGrader.Letter(d.BestGrade)}{(d.SimUnlocked ? " · 시뮬" : "")}\n");
                    break;
                case 6:
                    sb.Append($"동네 평판 {Model.S.TownRep:0.#} — '{b.RepName(Model.S.TownRep)}'\n학교 평판 {Model.S.SchoolRep:0.#} · 소문 {Model.S.Rumor}단계 · 시비 {Model.S.StreetWins}승 {Model.S.StreetLosses}패\n\n<b>불린 이름</b> {Model.N.Filled(Story.Def.Names)}/9\n");
                    foreach (var n in Model.N.Got) sb.Append($"「{n}」 ");
                    break;
            }
            return sb.ToString();
        }

        /// 소문(W12): 단계별 동네 말(시드·날짜 고정)
        public string Bark(string who)
        {
            if (Model == null) return "";
            string line = Model.RumorLine(who);
            if (line.Length > 0) Hud?.Toast($"{who}: \"{line}\"");
            return line;
        }

        // ───────────────────────── 저장
        public SaveData Snapshot(string card)
        {
            var ws = Model.S;
            ws.Active = true;
            ws.Year = GameClock.Year;
            ws.Card = card ?? "";
            ws.Place = Loader != null ? Loader.Place : "Zone1";
            ws.HasPos = ws.Place == SceneLoader.Zone1 && Loader != null && Loader.Motor != null;
            ws.Spawn = ws.Place == "Home" ? "Spawn" : ws.Place == SceneLoader.Zone1 ? "" : "Door";
            if (ws.Place == SceneLoader.Gym) ws.Spawn = "Spawn";
            if (ws.HasPos) { ws.Pos = Loader.Motor.Position; ws.Yaw = Loader.Motor.Yaw; }
            var cd = string.IsNullOrEmpty(card) ? null : Def.Card(card);
            return new SaveData
            {
                SceneId = string.IsNullOrEmpty(card) ? "world" : card,
                Month = GameClock.Month, Day = GameClock.Day, Minutes = GameClock.Minutes,
                Ledger = JsonUtility.FromJson<Ledger>(JsonUtility.ToJson(Model.L)),
                Names = JsonUtility.FromJson<NameBook>(JsonUtility.ToJson(Model.N)),
                Flags = Model.F.Clone(),
                Gym = Model.G.Clone(),
                World = ws.Clone(),
                Label = $"{GameClock.DateText} {GameClock.TimeText} · {PlaceName(ws.Place)}{(cd != null ? " · 「" + cd.Title + "」" : "")}",
            };
        }

        public bool AutoSaveOn = true;
        public int Saves { get; private set; }

        void AutoSave(string card)
        {
            if (!AutoSaveOn) return;
            try { SaveStore.Write(SaveStore.Auto, Snapshot(card)); Saves++; }
            catch (Exception e) { Debug.LogWarning($"[World] 자동 저장 실패: {e.Message}"); }
        }

        void ManualSave()
        {
            try { SaveStore.Write(SaveStore.Manual, Snapshot("")); Saves++; Hud?.Toast("공책에 적었다 — 저장"); }
            catch (Exception e) { Hud?.Toast("저장 실패: " + e.Message); }
        }

        /// 일시정지 메뉴 '저장하고 타이틀로'
        public void SaveAndTitle()
        {
            try { SaveStore.Write(SaveStore.Manual, Snapshot(CurrentCard != null ? CurrentCard.Id : "")); }
            catch (Exception e) { Debug.LogWarning($"[World] 저장 실패: {e.Message}"); }
            Stop();
            GameState.SetPaused(false);
            StoryBoot.Go(null, StoryBoot.TitleScene);
        }

        // ───────────────────────── 문·표시 자리 붙이기
        public StageDoor FindDoor(string target)
        {
            foreach (var d in FindObjectsByType<StageDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (d != null && d.Target == target && d.gameObject.scene == gameObject.scene) return d;
            return null;
        }

        /// Zone1: 마을버스 정류장 → 혜화 MMA, 명륜1가 방면 표지 → 공사 현장(무대로 바로 — docs/13 7-1 W1)
        void SetupZone()
        {
            var b = Def.Balance;
            MakeZoneDoor("bus_stop", SceneLoader.Gym, "Spawn", "마을버스 1번 — 혜화 MMA(대학로 북쪽)");
            MakeZoneDoor("site_sign", "Site", "Spawn", "현장 가기 — 언덕 빌라 신축 현장");
            // Zone1 가게 앞 판매창(약국·삼계탕·장어 — WorldSetup.AddShopFronts)
            foreach (var pm in FindObjectsByType<PlaceMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (pm.Place == SceneLoader.Zone1 && pm.Func.StartsWith("shop_") && pm.GetComponent<WorldMarker>() == null) pm.gameObject.AddComponent<WorldMarker>().Bind(this, pm);
        }

        void MakeZoneDoor(string pointId, string target, string spawn, string label)
        {
            if (FindDoor(target) != null) return;
            var pt = Def.Balance.Point(pointId);
            if (pt == null) return;
            var go = new GameObject("World 문 " + target);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, gameObject.scene);
            go.transform.position = pt.Pos;
            var it = go.AddComponent<Interactable>(); it.Prompt = label; it.Radius = 1.9f;
            var sd = go.AddComponent<StageDoor>(); sd.Target = target; sd.Spawn = spawn; sd.Key = target;
            var back = pt.Pos;
            int mask = Layers.Mask(Layers.Ground, Layers.Default);
            for (int k = 0; k < 8; k++)
            {
                var dir = Quaternion.Euler(0f, k * 45f, 0f) * Vector3.forward;
                var c = pt.Pos + dir * 1.6f + Vector3.up * 1.2f;
                if (Physics.CheckCapsule(c - Vector3.up * 0.6f, c + Vector3.up * 0.6f, 0.3f, Layers.Mask(Layers.Default, Layers.Wall), QueryTriggerInteraction.Ignore)) continue;
                if (!Physics.Raycast(c, Vector3.down, out var hit, 4f, mask, QueryTriggerInteraction.Ignore)) continue;
                back = hit.point + Vector3.up * 0.05f; sd.ReturnYaw = k * 45f; break;
            }
            sd.ReturnPos = back;
        }

        /// 출입문 문지기(StageDoor.Gate): 체육관은 체험 뒤부터, 현장은 곽반장 연락 뒤 토요일, 마을버스 시간
        bool DoorGate(StageDoor d)
        {
            if (!Active) return true;
            if (!Free && busy.Count > 0) return false;
            if (d.Target == SceneLoader.Gym)
            {
                if (!Model.S.GymTrial && Model.S.GymMember == 0) { Hud?.Toast("마을버스 — 아직 대학로에 갈 일이 없다"); return false; }
                if (!GymOpenHour(Model.Now, Def.Balance)) { Hud?.Toast("혜화 MMA — 문 닫을 시간(07:00~22:00)"); return false; }
                GameClock.Advance(Def.Balance.BusMinutes);
                Hud?.Toast($"마을버스 {Def.Balance.BusMinutes}분");
            }
            else if (d.Key == SceneLoader.Gym && d.Target == SceneLoader.Zone1) { GameClock.Advance(Def.Balance.BusMinutes); }
            else if (d.Target == "Site")
            {
                if (!Model.F.Has("site_booked")) { Hud?.Toast("공사 현장 — 아직 갈 일이 없다"); return false; }
            }
            return true;
        }

        /// 무대가 열린 뒤: 표시 자리에 WorldMarker, 체육관·현장 나가는 문, 배달·청소·노가다 표시 자리
        void OnPlaced()
        {
            if (!Active || Loader == null) return;
            var st = Loader.Stage;
            if (st == null) return;
            foreach (var pm in st.GetComponentsInChildren<PlaceMarker>(true))
                if (pm.GetComponent<WorldMarker>() == null) pm.gameObject.AddComponent<WorldMarker>().Bind(this, pm);
            if (st.Key == "Gukbap") EnsureMarker(st, "job_delivery", "배달통 — 새벽 배달", new Vector3(-1.1f, 0f, 1.5f), 0f);
            if (st.Key == "Site") EnsureMarker(st, "job_site", "곽반장 — 노가다", new Vector3(1.2f, 0f, 2.0f), 0f);
            // 돈 쓸 곳(14-6): 편의점 계산대 손님 쪽 · 국밥집 홀(직원가)
            if (st.Key == "Conv") EnsureMarker(st, "shop_conv", "하루편의점 계산대", new Vector3(2.3f, 0f, -2.6f), 90f);
            if (st.Key == "Gukbap") EnsureMarker(st, "shop_gukbap", "국밥집 — 직원가", new Vector3(0.2f, 0f, 0.4f), 180f);
            if (st.Key == SceneLoader.Gym)
            {
                var desk = st.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Sock_NPC_Coach_Desk");
                EnsureMarker(st, "gym_clean", "마감 청소(청소 관원)", desk != null ? st.transform.InverseTransformPoint(desk.position) + new Vector3(-1.2f, 0f, -0.8f) : new Vector3(15f, 0f, 9f), 0f);
            }
            if ((st.Key == SceneLoader.Gym || st.Key == "Site") && !st.GetComponentsInChildren<StageDoor>(true).Any(x => x.Target == SceneLoader.Zone1))
            {
                var sp = st.SpawnPoint("Spawn");
                var go = new GameObject("World 나가는 문");
                go.transform.SetParent(st.transform, false);
                go.transform.position = sp.position - sp.forward * 0.6f;
                var it = go.AddComponent<Interactable>(); it.Prompt = st.Key == SceneLoader.Gym ? "나가기 — 마을버스(후문시장 앞)" : "나가기 — 후문 골목"; it.Radius = 1.6f;
                var sd = go.AddComponent<StageDoor>(); sd.Target = SceneLoader.Zone1; sd.Key = st.Key;
            }
        }

        void EnsureMarker(StagePlace st, string func, string label, Vector3 local, float yaw)
        {
            if (st.GetComponentsInChildren<PlaceMarker>(true).Any(m => m.Func == func)) return;
            var go = new GameObject("Marker_World_" + func);
            go.transform.SetParent(st.transform, false);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var pm = go.AddComponent<PlaceMarker>(); pm.Place = st.Key; pm.Func = func; pm.Label = label;
            go.AddComponent<WorldMarker>().Bind(this, pm);
        }
    }
}
