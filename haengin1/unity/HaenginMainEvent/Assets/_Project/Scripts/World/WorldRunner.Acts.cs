// 행인1의 메인이벤트 — 오픈월드 진행: 활동(알바·학교·잠·가계부·하늘이) · 배달 자전거 · 체육관(체험·회비·드릴 시간) · 시비 · 주간 계획 · 데모 끝.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Haengin
{
    public sealed partial class WorldRunner
    {
        // ───────────────────────── 표시 자리(PlaceMarker → WorldMarker)
        public void UseMarker(string func, Transform at)
        {
            if (!Free) return;
            if (func.StartsWith("shop_")) { StartCoroutine(Run("가게", ShopFlow(func.Substring(5)))); return; }
            var a = Def.ActivityForMarker(func);
            if (a == null) { Hud?.Toast("여기선 할 일이 없다"); return; }
            switch (a.Kind)
            {
                case "sleep": StartCoroutine(Run("잠", Sleep(false))); break;
                case "save": ManualSave(); break;
                case "ledger": StartCoroutine(Run("가계부", LedgerView())); break;
                case "family": StartCoroutine(Run("하늘이", Haneul())); break;
                case "school": StartCoroutine(Run("학교", School(false))); break;
                default: StartCoroutine(Run("활동", Activity(a, func, at))); break;
            }
        }

        /// 표시 자리 안내 글(× · E 옆) — 지금 할 수 있나·시각·돈
        public string MarkerPrompt(string func)
        {
            if (!Active || Model == null) return "";
            if (ShopMarker(func, out var shopPrompt)) return shopPrompt;
            var a = Def.ActivityForMarker(func);
            if (a == null) return "";
            switch (a.Kind)
            {
                case "sleep":
                    {
                        if (Model.NapOnly) return $"낮잠 — 2시간({GameClock.Hm(((int)GameClock.Minutes + WorldModel.NapMinutes) % 1440)}까지)";
                        var (d, m) = Model.NextWake(); return $"잠 — {d / 100}/{d % 100} {GameClock.Hm(m)} 기상";
                    }
                case "save": return "공책 — 저장";
                case "ledger": return $"가계부 · 봉투 {Ledger.Won(Model.L.Envelope)}원";
                case "family": return "하늘이";
            }
            string why = Model.CanStart(a);
            string time = $"{GameClock.Hm(a.Start)}~{GameClock.Hm(a.End)}";
            return why.Length == 0 ? $"{a.Name} {time}{(a.Pay > 0 ? $" · {Ledger.Won(a.Units.Length > 0 ? a.Pay * Model.UnitsToday(a) : a.Pay)}원" : "")}" : $"{a.Name} — {why}";
        }

        IEnumerator Activity(ActivityDef a, string func, Transform at)
        {
            string why = Model.CanStart(a);
            if (why.Length > 0) { Hud?.Toast($"{a.Name} — {why}"); yield break; }
            if (TimedCardInside(a, Mathf.Max(Model.Now, a.Start), out var tcard)) { Hud?.Toast($"◆ {GameClock.Hm(tcard.From)} 「{tcard.Title}」 약속 — 지금 {a.Name}을(를) 시작하면 늦는다"); yield break; }
            bool canDel = Model.CanDelegate(a);
            int pick;
            if (BotSkill > 0f) pick = canDel && BotDelegate ? 1 : 0;
            else
            {
                pick = 0;
                var opts = new List<string> { $"직접 한다({GameClock.Hm(Mathf.Max(Model.Now, a.Start))}~{GameClock.Hm(a.End)})" };
                if (canDel) opts.Add("맡긴다 — 미니게임 없이(돈 100% · 부 스탯 75%)");
                opts.Add("그만둔다");
                yield return Ask(opts.ToArray(), v => pick = v);
                if (pick == opts.Count - 1) yield break;
            }
            bool delegated = pick == 1 && canDel;
            int s = Mathf.Max(Model.Now, a.Start);
            GameClock.AdvanceTo(s);
            CurrentActivity = a;
            if (Hud != null) Hud.Shown = false;
            Grade g = Grade.B;
            if (delegated) yield return Delegated(a);
            else yield return PlayMini(a, func, at, v => g = v);
            // 일하는 중 카드(그 시각에) — 끝나면 남은 시간을 마저
            for (int k = 0; k < 3; k++)
            {
                var dc = Director.PickDuring(a.Id, s, a.End);
                if (dc == null) break;
                GameClock.AdvanceTo(dc.From);
                yield return RunCard(dc);
            }
            var r = Model.Complete(a, g, delegated, s);
            GameClock.AdvanceTo(a.End);
            CurrentActivity = null;
            ActivitiesRun++;
            Trace.Add($"{GameClock.DateText} 활동 {a.Id} {DrillGrader.Letter(g)}{(delegated ? "(맡김)" : "")}");
            Debug.Log($"[World] 활동 {r} · {Model.Status}");
            Hud?.Toast($"{a.Name} {DrillGrader.Letter(g)}{(delegated ? "(맡김)" : "")} — {r.Summary}");
            ActivityDone?.Invoke(a, r);
            for (int k = 0; k < 3; k++)
            {
                var ac = Director.Pick("after:" + a.Id);
                if (ac == null) break;
                yield return RunCard(ac);
            }
            if (Hud != null) Hud.Shown = true;
        }

        /// 그 활동 시간 안에 시각이 박힌 핵심 카드(놓칠 수 없음)가 있나 — 있으면 시작하지 않는다(5-3 '놓칠 수 없음')
        public bool TimedCardInside(ActivityDef a, int start, out CardDef card)
        {
            card = null;
            if (Director == null) return false;
            foreach (var c in Director.Today())
            {
                if (c.Kind != CardKind.Core || !c.AtHas("time") || c.From < 0) continue;
                int at = c.At.Length > 1 && c.To >= 0 ? c.To : c.From;
                if (at >= start && at < a.End) { card = c; return true; }
            }
            return false;
        }

        /// 오늘 아직 안 온 '시각' 핵심 카드의 시각(없으면 −1)
        public int NextTimedCard()
        {
            int best = -1;
            if (Director == null) return best;
            foreach (var c in Director.Today())
            {
                if (c.Kind != CardKind.Core || !c.AtHas("time") || c.From < 0) continue;
                int at = c.At.Length > 1 && c.To >= 0 ? c.To : c.From;
                if (at >= Model.Now && (best < 0 || at < best)) best = at;
            }
            return best;
        }

        IEnumerator Delegated(ActivityDef a)
        {
            var board = Story.Board;
            if (board == null) yield break;
            board.ShowCard($"{GameClock.DateText}   ·   맡기기", a.Name, "맡김", false, new[] { $"{GameClock.Hm(Mathf.Max(Model.Now, a.Start))} ~ {GameClock.Hm(a.End)} — 손에 익은 일. 몸이 알아서 한다.", "돈 100% · 부 스탯 75% · 시간·피로는 그대로" }, null, "");
            GameState.Modal = true;
            float t = 0f; while (t < (BotSkill > 0f ? BotRead : 1.0f)) { t += UiKit.RealDt; yield return null; }
            GameState.Modal = false;
            board.Hide();
        }

        IEnumerator PlayMini(ActivityDef a, string func, Transform at, Action<Grade> done)
        {
            if (a.Mini == "bike") { yield return Delivery(done); yield break; }
            string mode = func == "job_dishes" ? "dish" : func == "job_waste" ? "stock" : a.Mini;
            var motor = Loader != null ? Loader.Motor : null;
            if (motor != null && at != null)
            {
                motor.ClearMoveInput();
                var p = at.position;
                if (Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out var hit, 4f, Layers.Mask(Layers.Ground, Layers.Default), QueryTriggerInteraction.Ignore)) p.y = hit.point.y;
                motor.Teleport(p + Vector3.up * 0.02f, at.eulerAngles.y);
                Loader.Cam?.SnapBehind();
            }
            InputMaps.Use(Actions, null);
            GameState.Modal = true;
            CutCamDriver cam = null;
            if (motor != null)
            {
                cam = CutCams.Make(CamPreset.Mid, motor.transform, null); cam.AvoidProps = true; cam.Snap();     // QA Q20: 포스기·모니터가 시우를 가리면 돌려 피함
                if (mode == "counter") cam.SetFixed(CounterCam);     // QA 2차 A8: 계산대 = 손님 쪽 왼편 위에서(호빵 찜기 유리가 화면에 걸리지 않게)
                cam.SetLive(true);
            }
            float score = 0.6f;
            if (Job != null && !string.IsNullOrEmpty(mode)) yield return Job.Play(mode, BotSkill, Model.S.Seed ^ (Model.Today * 31 + a.Id.Length), v => score = v);
            if (cam != null) CutCams.Release(cam);
            RoomAfterJob();
            GameState.Modal = false;
            InputMaps.Use(Actions, InputMaps.Explore);
            done(Model.GradeOf(score));
        }

        /// 계산대 알바 카메라 자리(시우 발 기준: 앞 2.2m · 왼쪽 1.25m · 높이 1.8m — 손님 쪽 왼편 위에서 계산대 너머로, 포스기 화면(카메라 막이)·호빵 찜기를 비켜)
        public static readonly Vector3 CounterCam = new Vector3(2.2f, -1.25f, 1.8f);

        /// QA Q20(2차): 좁은 일자리(편의점 계산대 안쪽 — 앞 계산대·뒤 담배 진열대)에서 끝나면 바깥 카메라가 머리 속으로 당겨져 손만 보였음
        /// → 실내면 카메라 뒤 공간(1.6m)이 나는 자리·방향(지금 방향 · 옆 · 뒤 중)으로 몇 걸음 옮김
        public void RoomAfterJob()
        {
            var motor = Loader != null ? Loader.Motor : null;
            if (motor == null || !Loader.Indoor) return;
            const float want = 1.6f;
            var bestP = motor.Position; float bestYaw = motor.Yaw;
            float bestC = SceneLoader.BackClear(bestP, Quaternion.Euler(0f, bestYaw, 0f) * Vector3.forward);
            if (bestC >= want) return;
            foreach (float off in new[] { 0f, 90f, -90f, 180f })
            {
                float y = motor.Yaw + off;
                var p = SceneLoader.RoomForCamera(motor.Position, ref y, want);
                float c = SceneLoader.BackClear(p, Quaternion.Euler(0f, y, 0f) * Vector3.forward);
                if (c > bestC + 0.2f) { bestC = c; bestP = p; bestYaw = y; }
            }
            if ((bestP - motor.Position).sqrMagnitude < 1e-4f && Mathf.Approximately(bestYaw, motor.Yaw)) return;
            motor.Teleport(bestP, bestYaw);
            Loader.Cam?.SnapBehind();
        }

        /// 새벽 배달 = 1화 자전거 코스(국밥집 앞 → 경비실 → 와룡 오르막 → 꼭대기 집 → 내리막). 지각·국물로 등급
        IEnumerator Delivery(Action<Grade> done)
        {
            var bc = BikeCourse.Current != null ? BikeCourse.Current : FindAnyObjectByType<BikeCourse>(FindObjectsInactive.Include);
            if (bc == null || Loader == null) { done(Grade.B); yield break; }
            bool skip = BotSkill > 0f && !BotRide;
            if (skip) { yield return Delegated(Def.Activity("job.delivery")); done(Grade.B); yield break; }
            yield return Loader.Go(SceneLoader.Zone1, null, true, bc.Start, bc.StartYaw);
            bc.gameObject.SetActive(true);
            bc.Bot = BotSkill > 0f;
            GameClock.AdvanceTo(6 * 60 + 4);
            bc.StartClock = 6 * 3600 + 4 * 60;
            if (bc.Stops.Length > 1) bc.Stops[1].Appt = 6 * 3600 + 5 * 60;
            if (Hud != null) Hud.Shown = false;
            yield return bc.RunAll();
            int late = Mathf.Max(0, bc.LateMin);
            float soup = bc.Soup != null ? bc.Soup.Value : 100f;
            bc.Finish();
            InputMaps.Use(Actions, InputMaps.Explore);
            var of = Loader.Motor != null ? Loader.Motor.GetComponent<Outfit>() : null;
            if (of != null) of.Set(Outfit.Uniform);
            var g = late <= 1 && soup >= 85f ? Grade.S : late <= 2 && soup >= 70f ? Grade.A : late <= 4 ? Grade.B : late <= 6 ? Grade.C : Grade.D;
            Debug.Log($"[World] 배달: 지각 {late}분 · 국물 {soup:F0} → {DrillGrader.Letter(g)}");
            var door = FindDoor("Gukbap");
            if (door != null) yield return Loader.Go(SceneLoader.Zone1, null, true, door.ReturnPos, door.ReturnYaw);
            done(g);
        }

        // ───────────────────────── 학교
        IEnumerator School(bool fromCard)
        {
            var a = Def.Activity("school");
            if (a == null) yield break;
            if (!fromCard)
            {
                string why = Model.CanStart(a);
                if (why.Length > 0) { Hud?.Toast($"학교 — {why}"); yield break; }
            }
            int s = fromCard ? a.Start : Mathf.Max(Model.Now, a.Start);
            int startMin = Mathf.Max(Model.Now, a.Start);
            GameClock.AdvanceTo(startMin);
            if (Hud != null) Hud.Shown = false;
            for (int k = 0; k < 3; k++)
            {
                var c = Director.Pick("activity:school");
                if (c == null) break;
                yield return RunCard(c);
            }
            var board = Story.Board;
            if (board != null)
            {
                board.ShowCard($"{GameClock.DateText}   ·   혜성고 2학년 3반", "학교", "하루", false,
                    new[] { "1교시부터 7교시까지. 오늘도 출석부는 내 이름에서 한 번 멈칫했다.", $"{GameClock.Hm(Mathf.Max(Model.Now, a.Start))} ~ {GameClock.Hm(a.End)} · 피로 +{a.Fatigue} · 멘탈 조금" }, null, "");
                GameState.Modal = true;
                float t = 0f; while (t < (BotSkill > 0f ? BotRead : 1.2f)) { t += UiKit.RealDt; yield return null; }
                GameState.Modal = false;
                board.Hide();
            }
            var r = Model.Complete(a, Grade.B, false, fromCard ? a.Start : startMin);
            GameClock.AdvanceTo(a.End);
            ActivitiesRun++;
            Trace.Add($"{GameClock.DateText} 활동 school");
            ActivityDone?.Invoke(a, r);
            var door = FindDoor("School");
            if (Loader != null && door != null) yield return Loader.Go(SceneLoader.Zone1, null, true, door.ReturnPos, door.ReturnYaw);
            for (int k = 0; k < 3; k++)
            {
                var c = Director.Pick("after:school");
                if (c == null) break;
                yield return RunCard(c);
            }
            if (Hud != null) Hud.Shown = true;
        }

        // ───────────────────────── 잠
        public IEnumerator Sleep(bool forced)
        {
            // QA 2차 A2: 일어난 지 6시간 안이면 낮잠 2시간만(카드·주간 계획·말일 없이)
            if (!forced && Model.NapOnly) { yield return Nap(); yield break; }
            if (!forced && BotSkill <= 0f)
            {
                var (d, m) = Model.NextWake();
                int pick = 0;
                yield return Ask(new[] { $"잔다 — {d / 100}/{d % 100} {GameClock.Hm(m)} 기상", "아직" }, v => pick = v);
                if (pick != 0) yield break;
            }
            for (int k = 0; k < 4; k++)
            {
                var c = Director.Pick("sleep");
                if (c == null) break;
                yield return RunCard(c);
            }
            // 일요일 밤 — 다음 2주 주간 계획(맡기기면 계획이 잠까지 넘김)
            if (!forced && Model.Weekday == "일" && Model.Unlocked("planner") && Model.Count("plan.day." + Model.Today) < 1)
            {
                Model.AddCount("plan.day." + Model.Today, 1);
                bool ran = false;
                yield return PlanWeek(GameClock.KeyPlus(Model.Today, 1), v => ran = v);
                if (ran) { Sleeps++; AutoSave(""); yield return DemoCheck(false); if (Hud != null) Hud.Shown = true; yield break; }
            }
            if (Hud != null) Hud.Shown = false;
            if (Loader != null && Loader.Wipe != null) yield return Loader.Wipe.Cover();
            var r = Model.Sleep(forced);
            Sleeps++;
            Trace.Add($"잠 → {GameClock.DateText}");
            Debug.Log($"[World] 잠: {r.Summary} · {Model.Status}");
            if (forced) Hud?.Toast("새벽 두 시 — 집까지 겨우 왔다(피로 +10)");
            Hud?.Toast(r.Summary);
            if (Loader != null)
            {
                if (Loader.Place != "Home") yield return Loader.Go("Home", "Spawn", false, Vector3.zero, 0f, false);
                if (Loader.Wipe != null) yield return Loader.Wipe.Uncover();
            }
            AutoSave("");
            if (r.MonthSettled && r.Shortfall > 0)
            {
                var mc = Director.Pick("monthend");
                if (mc != null) yield return RunCard(mc);
            }
            for (int k = 0; k < 4; k++)
            {
                var c = Director.Pick("wake");
                if (c == null) break;
                yield return RunCard(c);
            }
            lastPlace = Loader != null ? Loader.Place : "";
            yield return DemoCheck(false);
            if (Hud != null) Hud.Shown = true;
        }

        /// 낮잠 2시간(일어난 지 6시간 안) — 먹 닦기로 덮고 시계 +2시간, 피로만 조금
        public IEnumerator Nap()
        {
            if (BotSkill <= 0f)
            {
                int pick = 0;
                yield return Ask(new[] { $"낮잠 — 2시간({GameClock.Hm(((int)GameClock.Minutes + WorldModel.NapMinutes) % 1440)}까지)", "아직" }, v => pick = v);
                if (pick != 0) yield break;
            }
            if (Hud != null) Hud.Shown = false;
            if (Loader != null && Loader.Wipe != null) yield return Loader.Wipe.Cover();
            var r = Model.Nap();
            Naps++;
            Trace.Add($"낮잠 → {GameClock.TimeText}");
            Debug.Log($"[World] 낮잠: {r.Summary} · {Model.Status}");
            Hud?.Toast(r.Summary + " — 일어난 지 얼마 안 돼서 낮잠만");
            if (Loader != null && Loader.Wipe != null) yield return Loader.Wipe.Uncover();
            AutoSave("");
            if (Hud != null) Hud.Shown = true;
        }

        public int Naps { get; private set; }

        // ───────────────────────── 집: 가계부·하늘이
        IEnumerator LedgerView()
        {
            var ui = Story.LedgerUi;
            var b = Def.Balance;
            int due = b.FixedTotal(GameClock.Month);
            foreach (var h in Model.S.Holes) if (!h.Paid) due += h.Amount;
            var rows = new List<string> { $"[{GameClock.Month}월] 생활비로 들어간 돈  {Ledger.Won(Model.S.Household)}원", $"말일에 나갈 돈  {Ledger.Won(due)}원" };
            foreach (var f in b.Fixed) rows.Add($"  · {f.Name}  {Ledger.Won(f.Amount)}");
            foreach (var h in Model.S.Holes) if (!h.Paid) rows.Add($"  · 집 구멍 — {h.Name}  {Ledger.Won(h.Amount)}");
            rows.Add($"지갑 {Ledger.Won(Model.L.Wallet)}원{(EnvelopeKnown ? " · " + Model.L.EnvelopeText : "")}");
            // QA Q03: RevealNow 는 줄만 기록하고 표를 띄우지 않아(이야기 '건너뛰기'용) 사람이 공책 옆 가계부를 눌러도 빈 화면이었음 → 사람은 표를 써 나감
            if (ui != null) { if (BotSkill > 0f) ui.RevealNow(rows); else { GameState.InputLocked = true; yield return ui.Reveal(rows); GameState.InputLocked = false; } }
            float t = 0f; while (t < (BotSkill > 0f ? BotRead : 0.4f)) { t += UiKit.RealDt; yield return null; }
            // 봉투에 넣기(목표 전·지갑 5,000 이상)
            if (Model.F.Has("cake_quest") && Model.L.Envelope < b.EnvelopeGoal && Model.L.Wallet >= b.EnvelopeSunday)
            {
                int pick = 1;
                yield return Ask(new[] { $"봉투에 {Ledger.Won(b.EnvelopeSunday)}원 넣는다", "그냥 둔다" }, v => pick = v, 0);
                if (pick == 0) { Model.Apply($"wallet:-{b.EnvelopeSunday}:봉투로"); Model.Apply($"envelope:+{b.EnvelopeSunday}"); }
            }
            else yield return WaitPress();
            ui?.HideTable();
        }

        IEnumerator Haneul()
        {
            bool first = Model.Count("haneul." + Model.Today) < 1;
            Model.AddCount("haneul." + Model.Today, 1);
            if (first) Model.Apply("rel.haneul:+1");
            string[] lines = { "오빠 냄새나. 국밥 냄새.", "양갈래 오늘은 한 번에 됐어. 오빠 늘었네.", "봉투 어디 숨겼는지 나 알아. 말 안 해.", "오빠 오늘 몇 시에 와?", "고양이 이름 생각해 놨어." };
            int i = Mathf.Abs(Model.Today + (int)Model.Count("haneul." + Model.Today)) % lines.Length;
            var board = Story.Board;
            if (board != null)
            {
                board.ShowCard($"{GameClock.DateText} {GameClock.TimeText}   ·   시우네", "하늘이", "", false, new[] { $"하늘이 — \"{lines[i]}\"", $"관계(하늘) {Model.Rel("haneul") + Model.F.Haneul:0}" }, null, "× 다음");
                yield return WaitPress();
                board.Hide();
            }
        }

        // ───────────────────────── 체육관
        static bool GymOpenHour(int now, BalanceDef b) => now >= b.GymOpen && now < b.GymClose;

        /// 드릴 문지기(GymManager.Gate): 회원·체험·시간
        string GymGate(DrillDef d)
        {
            if (!Active) return "";
            if (Model.S.GymMember == 0 && !inTrial && !inLesson) return "회원만 — 사무실에서 관장에게(회비 또는 청소 관원)";
            if (!GymOpenHour(Model.Now, Def.Balance)) return "문 닫았다";
            return "";
        }
        bool inTrial;

        void OnDrillFinished(DrillDef d, DrillResult r, DrillReward w)
        {
            if (!Active) return;
            LessonDrillFinished(d, r, w);
            GameClock.Advance(Def.Balance.DrillMinutes);
            Model.AddCount("drill." + d.Id, 1);
            Trace.Add($"드릴 {d.Id} {DrillGrader.Letter(r.Grade)}");
            if (busy.Count == 0) StartCoroutine(Run("드릴 뒤", AfterDrill()));
        }

        IEnumerator AfterDrill()
        {
            yield return null;
            // QA Q16: '첫 S' 같은 드릴 뒤 카드가 드릴 결과 카드 위에 겹쳐 떴음 → 결과 카드를 닫을 때까지 기다림
            var gm = GymManager.Instance;
            for (int i = 0; i < 60 * 600 && gm != null && ((gm.Card != null && gm.Card.Visible) || gm.Busy); i++) yield return null;
            var c = Director.Pick("after:drill");
            if (c != null) yield return RunCard(c);
        }

        /// N01 뒤: 혜화 MMA 체험(D01 줄넘기 + D04 미트) → N02 회비냐 청소냐 → 마을버스로 후문
        IEnumerator GymTrial()
        {
            var b = Def.Balance;
            GameClock.AdvanceTo(GameClock.Minutes + 15);
            if (Loader == null) yield break;
            yield return Loader.Go(SceneLoader.Gym, "Spawn", false, Vector3.zero, 0f);
            GymManager gm = null;
            for (int i = 0; i < 300 && gm == null; i++) { gm = GymManager.Instance; yield return null; }
            yield return null; yield return null;
            inTrial = true;
            if (gm != null)
            {
                foreach (var id in b.TrialDrills)
                {
                    if (BotSkill > 0f) yield return gm.PlayBot(id, BotSkill);
                    else
                    {
                        if (!gm.StartDrill(id)) continue;
                        while (gm.Busy) yield return null;
                    }
                    yield return null;
                }
            }
            inTrial = false;
            Trace.Add("체험 끝");
            var c = Director.Pick("after:gymtrial");
            if (c != null) yield return RunCard(c);
            GameClock.Advance(b.BusMinutes);
            var door = FindDoor(SceneLoader.Gym);
            if (door != null) yield return Loader.Go(SceneLoader.Zone1, null, true, door.ReturnPos, door.ReturnYaw);
        }

        // ───────────────────────── 시비(M2 인카운터 Y4 재사용)
        IEnumerator StreetFight(CardDef c)
        {
            var enc = FindAnyObjectByType<Encounter>(FindObjectsInactive.Include);
            var pc = FindAnyObjectByType<PlayerCombat>();
            if (enc == null || pc == null || Loader == null) { if (HasDlg(Director.DlgOf(c))) yield return ShowBoard(c); Model.S.StreetWins++; yield break; }
            if (Loader.Place != SceneLoader.Zone1) yield return Loader.Go(SceneLoader.Zone1, null, false, Vector3.zero, 0f);
            if (Story.M2Stages != null) Story.M2Stages.SetActive(true);
            enc.gameObject.SetActive(true);
            yield return null;
            var d = enc.Def;
            enc.ResetAll(true);
            // 먹은 효과 '체력': 싸움 최대 체력 +%(14-6)
            int hpBase = pc.Me.MaxHp;
            if (Model.HpBonus > 0f) { pc.Me.MaxHp = Mathf.RoundToInt(hpBase * (1f + Model.HpBonus)); pc.Me.Hp = pc.Me.MaxHp; Hud?.Toast($"체력 효과 — 최대 체력 {hpBase} → {pc.Me.MaxHp}"); }
            (pc.Me.Body as PlayerBody)?.Place(d.ToWorld(d.RetryLocal) + Vector3.up * 0.1f, d.Yaw + 180f);
            Loader.Cam?.SnapBehind();
            // QA 2차 B: 시비 카드의 실제 장면 — 상대 셋이 선 뒤, 싸움 전에(대사 동안 인카운터는 잠금)
            string sdlg = Director.DlgOf(c);
            if (HasDlg(sdlg))
            {
                Encounter.Suppress = true;
                for (int i = 0; i < 3; i++) yield return null;
                CardScenes++; LastCardScene = sdlg; Trace.Add($"장면 {sdlg}");
                yield return Story.PlayDlg(sdlg);
                Loader.Cam?.SnapBehind();
            }
            Encounter.Suppress = false;
            FightBot bot = null;
            PInput pin = null;
            if (BotSkill > 0f)
            {
                pc.Me.DebugInvuln = true;
                pin = FindAnyObjectByType<PInput>(); if (pin != null) pin.enabled = false;
                var motor = pc.Motor;
                for (int i = 0; i < 900 && enc.State == Encounter.Phase.Armed; i++)
                {
                    var goal = d.ToWorld(new Vector2(1.5f, -1.0f));
                    motor.SetMoveInput(HitResolver.Flat(goal - motor.Position).normalized, 1f, false);
                    yield return null;
                }
                motor.ClearMoveInput();
                bot = FightBot.On(pc, false);
            }
            else
            {
                Hud?.Toast("후문 뒷골목 주차장 — 안으로 들어가면 시작");
                Story.Talk?.Sub?.Hide();
            }
            float w = 0f;
            while (enc.State != Encounter.Phase.Cleared && enc.State != Encounter.Phase.Lost && w < 240f) { w += Time.deltaTime; yield return null; }
            bool won = enc.State == Encounter.Phase.Cleared;
            if (enc.State == Encounter.Phase.Lost) { StageHud.Instance?.DefeatChoose(false); yield return null; }
            if (bot != null) Destroy(bot);
            if (pin != null) pin.enabled = true;
            pc.Me.DebugInvuln = false;
            if (pc.Me.MaxHp != hpBase) { pc.Me.MaxHp = hpBase; pc.Me.Hp = Mathf.Min(pc.Me.Hp, hpBase); }
            Encounter.Suppress = true;
            watched = enc; watchedPhase = enc.State;
            var b = Def.Balance;
            if (won) { Model.S.StreetWins++; Model.AddRep(b.StreetWinMany); Model.Apply("rumor:3"); Model.AddFatigue(b.StreetFatigue); Hud?.Toast($"시비 승 — 동네 평판 +{b.StreetWinMany}"); }
            else { Model.S.StreetLosses++; Model.AddRep(b.StreetLoss); Model.AddFatigue(b.StreetFatigue); Hud?.Toast("시비 패 — 다음에(평판 −1)"); }
            Trace.Add($"시비 {(won ? "승" : "패")}");
            Model.AddCount("street.day", Model.Today - Model.Count("street.day"));     // 같은 날엔 다시 안 걸림(Q27)
            Debug.Log($"[World] 시비 {c?.Id}: {(won ? "승" : "패")} · 전투 {w:F1}초 · 평판 {Model.S.TownRep:0.#}");
        }

        public AutoReport LastAuto { get; private set; }

        Encounter watched;
        Encounter.Phase watchedPhase;
        /// 첫 시비 뒤(unlock:street_repeat): 주차장 인카운터가 다시 걸림 — 결과를 평판에
        void StreetRepeat()
        {
            // M2 무대(주차장 인카운터)는 시비가 열린 뒤, 주차장 45m 안에서만 켬(멀면 꺼서 그리기 아낌 — W16)
            var stages = Story != null ? Story.M2Stages : null;
            bool near = false;
            var pk = Def.Balance.Point("parking");
            if (pk != null && Loader != null && Loader.Motor != null) near = (new Vector2(pk.Pos.x - Loader.Motor.Position.x, pk.Pos.z - Loader.Motor.Position.z)).magnitude < 45f;
            bool want = Model.Unlocked("street_repeat") && near && !Director.StreetBlocked;
            if (stages != null && stages.activeSelf != want && (watched == null || watched.State == Encounter.Phase.Armed || watched.State == Encounter.Phase.Cleared || watched.State == Encounter.Phase.Lost)) { stages.SetActive(want); if (want) SetYacha(false); }
            if (!want) { Encounter.Suppress = true; return; }
            if (watched == null) watched = FindAnyObjectByType<Encounter>(FindObjectsInactive.Include);
            if (watched == null) return;
            // QA Q27: 이긴 뒤(Cleared)엔 다시 안 걸렸음 → 하루 지나고 주차장 45m 안으로 다시 오면 시비를 다시 걸어 둠(하루 한 번)
            if (watched.State == Encounter.Phase.Cleared && Model.Count("street.day") != Model.Today) { watched.Rearm(); watchedPhase = watched.State; }
            Encounter.Suppress = false;
            var ph = watched.State;
            if (ph != watchedPhase)
            {
                if (ph == Encounter.Phase.Cleared && watchedPhase == Encounter.Phase.Result) { Model.S.StreetWins++; Model.AddRep(Def.Balance.StreetWinMany); Hud?.Toast($"시비 승 — 평판 +{Def.Balance.StreetWinMany}"); Model.AddCount("street.day", Model.Today - Model.Count("street.day")); }
                if (ph == Encounter.Phase.Lost) { Model.S.StreetLosses++; Model.AddRep(Def.Balance.StreetLoss); }
                watchedPhase = ph;
            }
        }

        // ───────────────────────── 주간 계획
        IEnumerator PlanWeek() { yield return PlanWeek(Model.Today, null); }

        IEnumerator PlanWeek(int fromDay, Action<bool> ran)
        {
            if (PlannerView == null) { ran?.Invoke(false); yield break; }
            bool any = false;
            // 2주씩 — 이벤트 날에 멈추지 않고 다 맡겼으면 다음 2주를 바로 묻는다(데모 3/17~4/30 = 2주 × 3번)
            for (int round = 0; round < 4; round++)
            {
                var plan = Planner.Make(fromDay, 14);
                bool go = false;
                yield return PlannerView.Edit(plan, d => { var e = Director.EventDay(d); return e != null ? $"「{e.Title}」" : null; }, $"주간 계획 — {fromDay / 100}월 {fromDay % 100}일부터 2주{(round > 0 ? " (이어서)" : "")}", v => go = v, BotSkill > 0f ? 0 : -1, Mathf.Max(0.05f, BotRead * 4f));
                Model.S.Plan = plan;
                if (!go) { Hud?.Toast("직접 한다 — 계획은 휴대폰 '일정'에"); break; }
                any = true;
                if (Hud != null) Hud.Shown = false;
                var rep = Planner.Run(plan);
                Trace.Add($"맡기기 {rep.DaysRun}일{(rep.Stopped != null ? " → " + rep.Stopped.Id : "")}");
                LastAuto = rep;
                AutoRuns++;
                Debug.Log($"[World] 주간 계획 맡기기 {round + 1}번째: {rep} · {Model.Status}");
                yield return PlannerView.Montage(rep.Days, rep.ToString(), BotSkill > 0f);
                // QA Q19: 2주를 맡기면 그동안 쌓인 알림(레슨 예약 3/24·일요일 봉투…)이 5/1 에 줄줄이 떠 지난 소식처럼 보였음 → 몽타주가 결산이므로 비움
                Hud?.ClearToasts();
                yield return HostCommands();
                if (rep.Stopped != null || rep.DaysRun == 0 || Model.S.DemoState != 0) { StoppedAt(rep); break; }
                fromDay = Model.Today;
            }
            ran?.Invoke(any);
            if (any && Loader != null && Model.Now < 12 * 60 && Loader.Place != "Home") yield return Loader.Go("Home", "Spawn", false, Vector3.zero, 0f);
            lastPlace = Loader != null ? Loader.Place : "";
            if (Hud != null) Hud.Shown = true;
        }

        public int AutoRuns { get; private set; }

        void StoppedAt(AutoReport rep)
        {
            if (rep.Stopped == null) return;
            Hud?.Toast($"이벤트 — 「{rep.Stopped.Title}」");
            foreach (var t in rep.Stopped.At)
            {
                if (t.StartsWith("during:") || t.StartsWith("after:") || t == "activity:school")
                {
                    var act = Def.Activity(t.Substring(t.IndexOf(':') + 1));
                    if (act != null) Model.Phone("하늘", $"오늘은 {act.Name} — {GameClock.Hm(act.Start)}.");
                    break;
                }
            }
        }

        // ───────────────────────── 데모 끝(12-2 #10)
        IEnumerator DemoCheck(bool force)
        {
            var ch = Def.Chapter("1");
            if (ch == null || Model.S.DemoState != 0) yield break;
            bool due = force || (Model.Today >= ch.DemoTo && Model.CardDone("C1-26")) || Model.Today > ch.DemoTo;
            if (!due) yield break;
            bool met = Model.GoalsMet(ch, true);
            Model.S.DemoState = met ? 1 : 2;
            var sb = new System.Text.StringBuilder();
            foreach (var g in ch.DemoGoals) sb.Append(Model.GoalLine(g)).Append('\n');
            sb.Append("\n1장 전체 목표(12월 첫눈까지)\n");
            foreach (var g in ch.Goals) sb.Append(Model.GoalLine(g)).Append('\n');
            var best = Model.G.Drills.Where(x => x.HasBest).OrderByDescending(x => x.Best).FirstOrDefault();
            sb.Append($"\n가계부 — 지갑 {Ledger.Won(Model.L.Wallet)} · 번 돈 {Ledger.Won(Model.L.Earned)} · 결손 {Model.S.Deficits}  ·  드릴 최고 {(best != null ? best.Id + " " + DrillGrader.Letter(best.BestGrade) : "없음")}  ·  도감 {Model.N.Filled(Story.Def.Names)}/9");
            Debug.Log($"[World] 데모 끝: {(met ? "목표 달성" : "목표 미달")} · {Model.Status}\n{sb}");
            Trace.Add($"데모 끝 {(met ? "달성" : "미달")}");
            if (Hud != null) Hud.Shown = false;
            yield return ChapterView.Show("데모 끝", $"{GameClock.DateText} · 1장 「행인1」 데모", met ? "목표 달성" : "목표 미달", ch.DemoTitle, sb.ToString(), "× · Enter   다음", BotSkill > 0f ? BotRead * 3f : 0f);
            var next = Def.Chapter("2");
            if (next != null)
            {
                var pb = new System.Text.StringBuilder();
                pb.Append(next.Goal).Append('\n');
                foreach (var g in next.Goals) pb.Append("· ").Append(g.Label).Append('\n');
                pb.Append($"\n해금 — {next.Unlocks}\n\n1장 「행인1」은 계속됩니다. 5월엔 등굣길 시비, 8월엔 대학로, 9월엔 와룡공원의 밤, 그리고 첫눈의 야차.");
                yield return ChapterView.Show("예고", "다음 장 예고", next.Label, $"{next.Period} · {next.Age} · {next.Ladder}", pb.ToString(), "× · Enter   계속 돌아다니기", BotSkill > 0f ? BotRead * 3f : 0f);
            }
            PlayerPrefs.SetInt("haengin.world.demo", met ? 1 : 2); PlayerPrefs.Save();
            DemoEnded?.Invoke();
            if (Hud != null) Hud.Shown = true;
        }
    }
}
