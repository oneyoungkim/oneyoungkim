// 행인1의 메인이벤트 — 주간 계획·맡기기(docs/13 0장 '시간 압축'·8-2·12-2 #7): 이벤트 없는 날은 계획대로 자동 진행(순수 C# — W10).
// 맡기기 = 체육관 시뮬(12 4-5) 규칙을 알바에도: 돈 100% · 부 스탯 75% · 관계 이벤트 0 · 시간·피로 100%.
// 멈춤: 날짜 박힌 핵심 카드가 있는 날(이벤트 날) 아침, 또는 자동 진행 중 핵심 카드가 뜨는 자리(그 직전). 계절·조건·사연·문자 카드는 요약(결과만).
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Haengin
{
    public sealed class AutoReport
    {
        public int DaysRun;
        public CardDef Stopped;
        public string StopWhy = "";
        public int WalletDelta, HouseholdDelta, EnvelopeDelta;
        public float RepDelta;
        public readonly List<string> Days = new List<string>();
        public readonly List<CardDef> Summaries = new List<CardDef>();
        public override string ToString() => $"{DaysRun}일 맡김 · 지갑 {Ledger.Signed(WalletDelta)} · 생활비 {Ledger.Signed(HouseholdDelta)} · 봉투 {Ledger.Signed(EnvelopeDelta)} · 평판 {RepDelta:+0.#;-0.#;0} · 요약 카드 {Summaries.Count}{(Stopped != null ? $" · 멈춤 {Stopped.Id}({StopWhy})" : "")}";
    }

    public sealed class WeekPlanner
    {
        public static readonly string[] DawnOpts = { "배달", "잠" };
        public static readonly string[] EveningOpts = { "국밥집", "체육관", "쉼" };
        public static readonly string[] NightOpts = { "입고", "청소", "쉼" };
        public static readonly string[] SatOpts = { "노가다", "쉼" };
        public static readonly string[] SunOpts = { "편의점", "쉼" };

        public readonly WorldModel M;
        public readonly EventDirector D;
        /// 체육관 맡기기에 쓰는 드릴(WorldDef.Drills — 없으면 체육관 칸은 '쉼'처럼)
        public DrillDef[] Drills = new DrillDef[0];
        /// 시험(W10): 맡기지 않고 '매일 직접 B 로 한 것'과 비교할 때 true(돈·스탯 100%)
        public bool Direct;

        public WeekPlanner(WorldModel m, EventDirector d) { M = m; D = d; }

        ActivityDef A(string id) => M.Def.Activity(id);

        /// 기본 계획(지금 열린 일 기준): 배달 · 학교 · 국밥집(관원이면 화·목은 체육관) · 입고(청소 관원이면 월·수·금 청소) · 토 노가다 · 일 편의점
        public List<PlanDay> Make(int fromDay, int days)
        {
            var r = new List<PlanDay>();
            for (int i = 0; i < days; i++)
            {
                int d = GameClock.KeyPlus(fromDay, i);
                string w = GameClock.WeekdayOfKey(d);
                var p = new PlanDay { Day = d };
                // 아직 안 열린 일도 적어 둔다(열리면 그날부터 함 — 못 하는 날은 넘김)
                p.Dawn = w != "일" ? "배달" : "잠";
                bool weekday = w != "토" && w != "일";
                p.Evening = weekday ? "국밥집" : (M.S.GymMember > 0 && w == "토" ? "체육관" : "쉼");
                p.Night = weekday ? (M.S.GymMember == 2 && "월수금".Contains(w) ? "청소" : "입고") : "쉼";
                p.Weekend = w == "토" ? "노가다" : w == "일" ? "편의점" : "";
                r.Add(p);
            }
            return r;
        }

        public static string[] OptionsFor(PlanDay p, int slot)
        {
            string w = GameClock.WeekdayOfKey(p.Day);
            return slot switch { 0 => DawnOpts, 1 => EveningOpts, 2 => NightOpts, _ => w == "토" ? SatOpts : w == "일" ? SunOpts : new string[0] };
        }

        public static string Get(PlanDay p, int slot) => slot switch { 0 => p.Dawn, 1 => p.Evening, 2 => p.Night, _ => p.Weekend };
        public static void SetSlot(PlanDay p, int slot, string v) { if (slot == 0) p.Dawn = v; else if (slot == 1) p.Evening = v; else if (slot == 2) p.Night = v; else p.Weekend = v; }

        /// 계획대로 맡긴다. 시계는 계획 첫날 아침(기상 뒤)이어야 한다. 이벤트 날 아침에 멈춘다(그 날은 사람이)
        public AutoReport Run(List<PlanDay> plan)
        {
            var rep = new AutoReport();
            int w0 = M.L.Wallet, h0 = M.S.Household, e0 = M.L.Envelope; float r0 = M.S.TownRep;
            int hBase = 0;   // 말일 정산으로 생활비가 0 이 되는 것을 이어서 셈
            foreach (var p in plan)
            {
                if (p.Day < M.Today) continue;
                // 그 날 아침까지(잠으로 넘어옴)
                int guard = 0;
                while (M.Today < p.Day && guard++ < 40) { int hb = M.S.Household; var s = M.Sleep(); if (s.MonthSettled) hBase += hb; Wake(rep); }
                if (M.Today != p.Day) break;
                var ev = D.EventDay(p.Day);
                if (ev != null) { rep.Stopped = ev; rep.StopWhy = "이벤트 날"; break; }
                var line = new List<string>();
                string w = M.Weekday;
                // 매주 고정 레슨은 그 날 아침에 예약(돈·회권 규칙 그대로 — 14-6)
                M.RenewStanding(M.Today);
                bool nightLesson = M.LessonsOn(M.Today).Any(x => x.At >= 21 * 60);
                // 새벽
                if (p.Dawn == "배달" && !Job("job.delivery", line, rep)) break;
                // 토: 노가다 / 평일: 학교
                if (w == "토" && p.Weekend == "노가다" && !Job("job.site", line, rep)) break;
                if (M.SchoolDay(M.Today) && M.Unlocked("school"))
                {
                    if (!School(line, rep)) break;
                }
                if (w == "일" && p.Weekend == "편의점" && !Job("job.counter", line, rep)) break;
                // 저녁
                if (p.Evening == "국밥집" && !Job("job.gukbap", line, rep)) break;
                if (p.Evening == "체육관") GymAuto(2, line);
                // 밤
                if (p.Night == "입고" && nightLesson) line.Add("입고 대신 레슨");
                else if (p.Night == "입고" && !Job("job.stock", line, rep)) break;
                if (p.Night == "청소") { if (!Job("gym.clean", line, rep)) break; GymAuto(1, line); }
                if (p.Evening == "쉼" && p.Night == "쉼" && (w == "토" || w == "일")) { M.AddFatigue(-M.B.RestDay / 2f); line.Add("쉼"); }
                // 그 날 예약한 개인레슨(매주 고정 포함) — 계획이 대신 감(B × 0.75, 14-6)
                foreach (var bk in M.LessonsOn(M.Today)) { string ls = M.LessonAuto(bk); if (ls.Length > 0) line.Add(ls.Split('·')[0].Trim() + "(맡김)"); }
                // 잠 전 카드(계절·문자는 요약, 핵심이면 멈춤)
                if (!Cards("sleep", line, rep)) break;
                rep.Days.Add($"{GameClock.DateText} — {(line.Count > 0 ? string.Join(" · ", line) : "쉼")}");
                rep.DaysRun++;
                int hb2 = M.S.Household;
                var sr = M.Sleep();
                if (sr.MonthSettled) { hBase += hb2; if (sr.Shortfall > 0) { var c = D.Pick("monthend"); if (c != null) Summary(c, rep, line); } }
                Wake(rep);
            }
            rep.WalletDelta = M.L.Wallet - w0;
            rep.HouseholdDelta = M.S.Household + hBase - h0;
            rep.EnvelopeDelta = M.L.Envelope - e0;
            rep.RepDelta = M.S.TownRep - r0;
            return rep;
        }

        void Wake(AutoReport rep)
        {
            var tmp = new List<string>();
            for (int k = 0; k < 4; k++)
            {
                var c = D.Pick("wake");
                if (c == null) break;
                if (c.Kind == CardKind.Core || c.Kind == CardKind.System) { rep.Stopped = c; rep.StopWhy = "아침 카드"; return; }
                Summary(c, rep, tmp);
            }
        }

        bool Cards(string trig, List<string> line, AutoReport rep)
        {
            for (int k = 0; k < 4; k++)
            {
                if (rep.Stopped != null) return false;
                var c = D.Pick(trig);
                if (c == null) return true;
                if (c.Kind == CardKind.Core || c.Kind == CardKind.System) { rep.Stopped = c; rep.StopWhy = trig; return false; }
                Summary(c, rep, line);
            }
            return rep.Stopped == null;
        }

        /// 결과만 적용(선택지는 첫 보기)
        public void Summary(CardDef c, AutoReport rep, List<string> line)
        {
            D.Running = null;
            D.ApplyAfter(c, c.Choice != null && c.Choice.Length > 0 ? c.Choice[0] : null);
            D.Done(c, c.Kind == CardKind.Repeat ? 1 : 2);
            if (c.Kind != CardKind.Repeat) { rep.Summaries.Add(c); line.Add($"「{c.Title}」"); }
        }

        bool Job(string id, List<string> line, AutoReport rep)
        {
            if (rep.Stopped != null) return false;
            var a = A(id);
            if (a == null) return true;
            int s = Mathf.Max(M.Now, a.Start);
            if (M.CanStart(a, s).Length > 0) return true;     // 못 하는 날(비·잠김·상한) — 넘김
            GameClock.AdvanceTo(s);
            // 활동 중 핵심 카드(N01 등)는 사람에게 넘긴다
            var dc = D.PickDuring(a.Id, s, a.End);
            if (dc != null && (dc.Kind == CardKind.Core || dc.Kind == CardKind.System)) { rep.Stopped = dc; rep.StopWhy = "활동 중 핵심"; return false; }
            if (dc != null) Summary(dc, rep, line);
            var r = M.Complete(a, Grade.B, !Direct, s);
            GameClock.AdvanceTo(a.End);
            line.Add(Short(a, r));
            return Cards("after:" + a.Id, line, rep);
        }

        bool School(List<string> line, AutoReport rep)
        {
            var a = A("school");
            if (a == null || M.CanStart(a, a.Start).Length > 0) return true;
            GameClock.AdvanceTo(a.Start);
            if (!Cards("activity:school", line, rep)) return false;
            M.Complete(a, Grade.B, true, a.Start);
            GameClock.AdvanceTo(a.End);
            line.Add("학교");
            return Cards("after:school", line, rep);
        }

        static string Short(ActivityDef a, ActivityResult r) => a.Id switch
        {
            "job.delivery" => $"배달 +{Ledger.Won(r.Pay)}",
            "job.gukbap" => $"국밥집 +{Ledger.Won(r.Pay)}",
            "job.stock" => "입고 삼각김밥 2",
            "job.counter" => $"편의점 +{Ledger.Won(r.Pay)}",
            "job.site" => $"노가다 +{Ledger.Won(r.Pay)}",
            "gym.clean" => "체육관 청소",
            _ => a.Name,
        };

        /// 체육관 맡기기: 시뮬 열린 드릴은 시뮬(4-5), 아니면 기본 훈련(B 의 75%). 회원이 아니면 없음
        public void GymAuto(int n, List<string> line)
        {
            if (M.S.GymMember == 0 || Drills == null || Drills.Length == 0) return;
            int done = 0;
            foreach (var d in Drills)
            {
                if (done >= n) break;
                if (d == null || !d.Playable || d.StoryOnly || d.HeadRisk) continue;
                if (M.G.Fatigue >= M.B.GymBlock) break;
                if (DrillSim.CanSim(d, M.G, GymSave.Today)) { DrillSim.Run(d, M.G, GymSave.Today); done++; continue; }
                var w = DrillRewards.Compute(d, Grade.B, false, M.G, false);
                M.G.Sweat += Mathf.RoundToInt(w.Sweat * 0.75f);
                M.AddStat(StatKey(d.MainStat), w.MainDelta * 0.75f);
                if (d.SubStat != Stat.None) M.AddStat(StatKey(d.SubStat), w.SubDelta * 0.75f);
                M.AddFatigue(w.Fatigue);
                done++;
            }
            if (done > 0) { GameClock.Advance(M.B.DrillMinutes * done); line.Add($"체육관 드릴 {done}"); }
        }

        public static string StatKey(Stat s) => s switch
        {
            Stat.Power => "power", Stat.Speed => "speed", Stat.Stamina => "stamina", Stat.Toughness => "toughness",
            Stat.Striking => "striking", Stat.Wrestling => "wrestling", Stat.Jiu => "jiu", Stat.Mental => "mental", _ => "",
        };
    }
}
