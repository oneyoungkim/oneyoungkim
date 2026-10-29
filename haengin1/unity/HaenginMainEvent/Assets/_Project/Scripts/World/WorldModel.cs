// 행인1의 메인이벤트 — 오픈월드 규칙(순수 C# — 장면 없이 시험 W02~W04·W10·W11·W13): 시계·돈·피로·스탯·평판·가족 지출·알바·잠·말일 이체·목표.
// docs/13 3장 핵심 루프 · 9장 밸런스 · 10장 저장. 상태 = WorldSave + 기존 Ledger(지갑·봉투)·StoryFlags(플래그·참기)·GymSave(스탯·피로)·NameBook(도감).
// 결정 12(2026-10-25 확정): 시우(17)는 밤 10시~아침 6시 근로 없음 · 일터마다 하루 7시간 — 막는다. 주 합계는 막지 않고 과로 피로 ×1.5.
// 효과 말(Apply): flag:x · unflag:x · unlock:x · did:<활동> · rep.town:+N · rep.school:+N · rel.<누구>:+N · stat.<스탯>:+N · fatigue:+N · count:x[:N] ·
//   wallet:±N:메모 · envelope:+N · spend:N:메모 · item:이름:N · entry:글 · name:호칭 · household:+N:메모 · hole:이름:N · holepay:이름 · endure ·
//   rumor:N · gym:trial|member|cleaner|fee · phone:보낸이:글 · done:<카드> · 그 밖(chain:· activity:· plan:· demo:· promote:)은 Host 로 넘김.
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Haengin
{
    public struct ActivityResult
    {
        public string Id;
        public Grade Grade;
        public bool Delegated;
        public int Pay, Fatigue, Minutes, Units;
        /// 등급 덤(팁·별점 수당·칭찬 일당 — QA 2차 A1), 지갑으로
        public int Tip;
        public string Summary;
        public override string ToString() => $"{Id} {DrillGrader.Letter(Grade)}{(Delegated ? "(맡김)" : "")} · {Summary}";
    }

    public struct SleepResult
    {
        public int FromDay, WakeDay, WakeMin, Hours, FatigueBefore, FatigueAfter;
        public bool MonthSettled;
        public int Shortfall;
        public string Summary;
    }

    public sealed partial class WorldModel
    {
        public readonly WorldDef Def;
        public WorldSave S;
        public Ledger L;
        public StoryFlags F;
        public GymSave G;
        public NameBook N;
        public NameEntry[] Catalog;
        /// 데모(1장 3/3~5/8)에서는 demo 카드만 본다
        public bool DemoMode = true;
        public BalanceDef B => Def.Balance;
        /// 효과 중 이 규칙이 못 하는 것(장면·화면이 할 일) — WorldRunner 가 꺼내 처리
        public readonly List<string> Host = new List<string>();
        /// 화면에 띄울 한 줄(알림)
        public event Action<string> Toast;
        /// 도감 등록(화면 연출)
        public event Action<string> NameGot;
        public readonly List<string> Log = new List<string>();

        static readonly string[] StatKeys = { "power", "speed", "stamina", "toughness", "striking", "wrestling", "jiu", "mental" };

        public WorldModel(WorldDef def, WorldSave s, Ledger l, StoryFlags f, GymSave g, NameBook n, NameEntry[] catalog)
        {
            Def = def; S = s ?? new WorldSave(); L = l ?? new Ledger(); F = f ?? new StoryFlags(); G = g ?? new GymSave(); N = n ?? new NameBook(); Catalog = catalog ?? new NameEntry[0];
            if (S.StatFrac == null || S.StatFrac.Length != 8) S.StatFrac = new float[8];
        }

        /// 새 게임 저장(3/3 05:50, 지갑 2,600원)
        public static WorldSave NewSave(WorldDef def)
        {
            var b = def.Balance;
            var s = new WorldSave { Active = true, Year = b.StartYear, Chapter = "1", Seed = b.Seed, Place = "Gukbap", Spawn = "Spawn" };
            s.Set(WokeDayKey, b.StartDate); s.Set(WokeMinKey, b.Wake);      // QA 2차 A2: 첫날도 05:30 에 일어난 것으로(낮잠 규칙)
            return s;
        }

        public void Say(string line) { if (string.IsNullOrEmpty(line)) return; Log.Add(line); Toast?.Invoke(line); }

        // ───────────────────────── 시계
        public int Today => GameClock.DayKey;
        public int Now => (int)GameClock.Minutes;
        public string Weekday => GameClock.Weekday;
        public static int DayKeyOf(int m, int d) => m * 100 + d;
        public bool Holiday(int key) => Array.IndexOf(B.Holidays, key) >= 0;
        public bool SchoolDay(int key) { var w = GameClock.WeekdayOfKey(key); return w != "토" && w != "일" && !Holiday(key); }
        public int DayIndex => GameClock.DaysBetween(B.StartDate, Today);

        /// 비(날씨 표 — 저장 시드 고정 · 원고 고정일 우선, W02)
        public bool Rain(int key)
        {
            if (Array.IndexOf(B.RainOn, key) >= 0) return true;
            if (Array.IndexOf(B.ClearOn, key) >= 0) return false;
            int m = key / 100;
            float p = m >= 0 && m < B.RainByMonth.Length ? B.RainByMonth[m] : 0.2f;
            return Hash01(S.Seed, key, "rain") < p;
        }
        public bool RainToday => Rain(Today);
        public bool RainTomorrow => Rain(GameClock.KeyPlus(Today, 1));

        /// 시간대(docs/13 3-1 · QA 2차 A3): 새벽 05~08 · 아침 08~12 · 낮 12~16 · 오후 16~18 · 저녁 18~22 · 밤 22~05.
        /// 데이터 표(balance.json)가 비었으면 GameClock 표 — 둘은 같아야 함(W00b)
        public string Band(int minutes)
        {
            if (B.BandNames.Length == 0) return GameClock.BandOf(minutes);
            string n = B.BandNames[B.BandNames.Length - 1];
            int best = -1;
            for (int i = 0; i < B.BandFrom.Length; i++)
                if (minutes >= B.BandFrom[i] && B.BandFrom[i] > best) { best = B.BandFrom[i]; n = B.BandNames[i]; }
            return n;
        }

        public static float Hash01(int seed, int day, string key)
        {
            unchecked
            {
                uint h = 2166136261;
                void Mix(int v) { for (int i = 0; i < 4; i++) { h ^= (uint)((v >> (8 * i)) & 0xFF); h *= 16777619; } }
                Mix(seed); Mix(day);
                foreach (char c in key ?? "") { h ^= c; h *= 16777619; }
                h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        // ───────────────────────── 셈·관계·스탯
        public float Count(string k) => S.Get(k);
        public void AddCount(string k, float v) => S.Add(k, v);
        public float Rel(string who) => S.Get("rel." + who);
        public bool Unlocked(string k) => string.IsNullOrEmpty(k) || S.Unlocks.Contains(k);
        public void Unlock(string k) { if (!string.IsNullOrEmpty(k) && !S.Unlocks.Contains(k)) { S.Unlocks.Add(k); Log.Add("열림: " + k); } }

        public static int StatIndex(string name) => Array.IndexOf(StatKeys, name);

        /// 부 스탯 소수 쌓기 → 1이 차면 GymSave 스탯 +1
        public void AddStat(string name, float v)
        {
            int i = StatIndex(name);
            if (i < 0 || Mathf.Abs(v) < 1e-6f) return;
            S.StatFrac[i] += v;
            while (S.StatFrac[i] >= 1f) { S.StatFrac[i] -= 1f; G.AddStat((Stat)i, 1); }
            while (S.StatFrac[i] <= -1f) { S.StatFrac[i] += 1f; G.AddStat((Stat)i, -1); }
        }

        /// 스탯 값(정수 + 소수)
        public float StatValue(string name) { int i = StatIndex(name); return i < 0 ? 0f : G.Stats[i] + S.StatFrac[i]; }

        public void AddFatigue(float v) { if (v > 0f) v *= 1f - StaminaCut; G.Fatigue = Mathf.Clamp(G.Fatigue + Mathf.RoundToInt(v), 0, 100); }

        public void AddRep(float v)
        {
            float before = S.TownRep;
            S.TownRep = Mathf.Clamp(S.TownRep + v, 0f, 100f);
            if (B.RepName(before) != B.RepName(S.TownRep)) Say($"동네 평판 — '{B.RepName(S.TownRep)}'");
        }

        // ───────────────────────── 카드 상태
        public bool CardDone(string id)
        {
            foreach (var c in S.Cards)
            {
                if (c.State == 0) continue;
                if (c.Id == id) return true;
                var d = Def.Card(c.Id);
                if (d != null && d.GroupId == id) return true;
            }
            return false;
        }

        public void MarkCard(CardDef c, int state)
        {
            var st = S.Card0(c.Id, true);
            st.State = state; st.Day = Today; st.Times++;
        }

        // ───────────────────────── 값·조건(need 칸)
        public float Value(string key)
        {
            key = key.Trim();
            if (key.StartsWith("card.")) return CardDone(key.Substring(5)) ? 1 : 0;
            if (key.StartsWith("flag.")) return F.Has(key.Substring(5)) ? 1 : 0;
            if (key.StartsWith("count.")) return Count(key.Substring(6));
            if (key.StartsWith("rel.")) return Rel(key.Substring(4));
            if (key.StartsWith("stat.")) return StatValue(key.Substring(5));
            switch (key)
            {
                case "wallet": return L.Wallet;
                case "envelope": return L.Envelope;
                case "household": return S.Household;
                case "fatigue": return G.Fatigue;
                case "rumor": return S.Rumor;
                case "deficits": return S.Deficits;
                case "shortfall": return S.LastShortfall;
                case "rep.town": return S.TownRep;
                case "rep.school": return S.SchoolRep;
                case "wins.street": return S.StreetWins;
                case "losses.street": return S.StreetLosses;
                case "wins.yacha": return S.YachaWins;
                case "gym.member": return S.GymMember;
                case "gym.trial": return S.GymTrial ? 1 : 0;
                case "day": return Today;
                case "hour": return GameClock.Hour;
                case "endured": return F.Endured;
                case "drills.s": { int n = 0; foreach (var d in G.Drills) if (d.SCount > 0) n++; return n; }
                case "drills.b": { int n = 0; foreach (var d in G.Drills) if (d.Best >= (int)Grade.B) n++; return n; }
            }
            return 0f;
        }

        static readonly string[] Ops = { ">=", "<=", "!=", "==", ">", "<", "=" };

        public bool Check(string expr)
        {
            if (string.IsNullOrWhiteSpace(expr)) return true;
            expr = expr.Trim();
            bool not = expr.StartsWith("!");
            string e = not ? expr.Substring(1) : expr;
            bool r;
            if (e.StartsWith("card:")) r = CardDone(e.Substring(5));
            else if (e.StartsWith("flag:")) r = F.Has(e.Substring(5));
            else if (e.StartsWith("unlocked:")) r = Unlocked(e.Substring(9));
            else if (e == "rain") r = RainToday;
            else if (e == "rain.tomorrow") r = RainTomorrow;
            else if (e.StartsWith("goal:")) r = GoalsMet(Def.Chapter(e.Substring(5)), false);
            else
            {
                r = false;
                foreach (var op in Ops)
                {
                    int i = e.IndexOf(op, StringComparison.Ordinal);
                    if (i <= 0) continue;
                    float a = Value(e.Substring(0, i));
                    float b = float.TryParse(e.Substring(i + op.Length).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ? x : 0f;
                    r = op switch { ">=" => a >= b - 1e-4f, "<=" => a <= b + 1e-4f, ">" => a > b, "<" => a < b, "!=" => Mathf.Abs(a - b) > 1e-4f, _ => Mathf.Abs(a - b) <= 1e-4f };
                    break;
                }
            }
            return not ? !r : r;
        }

        public bool CheckAll(string[] need) { if (need != null) foreach (var n in need) if (!Check(n)) return false; return true; }

        // ───────────────────────── 효과
        public void ApplyAll(IEnumerable<string> effs) { if (effs != null) foreach (var e in effs) Apply(e); }

        static int Int(string s) => Ledger.ParseInt(s ?? "");
        static float Num(string s) { s = (s ?? "").Replace("+", "").Trim(); return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0f; }

        public void Apply(string eff)
        {
            if (string.IsNullOrWhiteSpace(eff)) return;
            eff = eff.Trim();
            int c = eff.IndexOf(':');
            string verb = c > 0 ? eff.Substring(0, c) : eff, arg = c > 0 ? eff.Substring(c + 1) : "";
            var p = arg.Split(new[] { ':' }, 3);
            switch (verb)
            {
                case "flag": F.Raise(arg); break;
                case "unflag": F.Set.Remove(arg); break;
                case "unlock": Unlock(arg); break;
                case "did": { var a = Def.Activity(arg); AddCount(arg, 1); MarkDoneToday(arg); if (a != null && !string.IsNullOrEmpty(a.Work)) S.AddWork(a.Work, a.Minutes); break; }
                case "rep.town": AddRep(Num(arg)); break;
                case "rep.school": S.SchoolRep = Mathf.Clamp(S.SchoolRep + Num(arg), 0f, 100f); break;
                case "fatigue": AddFatigue(Num(arg)); break;
                case "count": AddCount(p[0], p.Length > 1 ? Num(p[1]) : 1f); break;
                case "rumor": S.Rumor = Mathf.Max(S.Rumor, Int(arg)); break;
                case "endure": F.Endured++; break;
                case "wallet": case "envelope": case "spend": case "item": case "entry":
                    {
                        var line = L.Apply(verb, arg);
                        if (verb != "entry") Say(line);
                        break;
                    }
                case "name":
                    if (N.Register(arg, Catalog)) { Say($"도감 — 「{arg}」"); NameGot?.Invoke(arg); }
                    break;
                case "household": AddHousehold(Int(p[0]), p.Length > 1 ? p[1] : "생활비"); break;
                case "hole":
                    S.Holes.Add(new Hole { Name = p[0], Amount = p.Length > 1 ? Int(p[1]) : 0 });
                    Say($"집 구멍 — {p[0]} {Ledger.Won(p.Length > 1 ? Int(p[1]) : 0)}원(말일까지)");
                    break;
                case "holepay":
                    // 생활비(엄마 통장·고지서 밑)로 바로 메움 — 말일 이체에서 빠짐
                    foreach (var h in S.Holes) if (h.Name == arg && !h.Paid) { h.Paid = true; S.Household -= h.Amount; Say($"집 구멍 메움 — {h.Name} {Ledger.Won(h.Amount)}원(생활비에서)"); }
                    break;
                case "gym": Gym(arg); break;
                case "eat": { var fd = Def.Shop.Food(arg); if (fd != null) Say(Eat(fd, false, true)); break; }
                case "phone": Phone(p[0], p.Length > 1 ? arg.Substring(p[0].Length + 1) : ""); break;
                case "done":
                    {
                        var cd = Def.Card(arg);
                        if (cd != null) MarkCard(cd, 1);
                        break;
                    }
                default:
                    if (verb.StartsWith("rel.")) AddCount(verb, Num(arg));
                    else if (verb.StartsWith("stat.")) AddStat(verb.Substring(5), Num(arg));
                    else Host.Add(eff);
                    break;
            }
        }

        public void Phone(string from, string text)
        {
            S.Phone.Add(new PhoneMsg { Day = Today, Min = Now, From = from, Text = text });
            Say($"문자 — {from}: {text}");
        }

        void Gym(string arg)
        {
            switch (arg)
            {
                case "trial": S.GymTrial = true; Unlock("gym"); Host.Add("gymtrial"); break;
                case "member": S.GymMember = 1; Unlock("gym"); S.GymPaidMonth = GameClock.Month; Say("혜화 MMA — 관원 등록(회비 다음 달 1일부터)"); break;
                case "cleaner": S.GymMember = 2; Unlock("gym"); Unlock("gym.cleaner"); S.GymPaidMonth = GameClock.Month; Say("혜화 MMA — 청소 관원(월·수·금 21:00~21:30)"); break;
                case "fee": PayGymFee(); break;
            }
        }

        /// 회비 날(N03): 지갑에서. 모자라면 고준이 한 번 대신 냄(빚) → 다음 달 같이
        public void PayGymFee()
        {
            if (S.GymMember == 0 || S.GymPaidMonth == GameClock.Month) return;
            int fee = S.GymMember == 2 ? B.CleanerFee : B.GymFee;
            int due = fee * (1 + S.GymDebt);
            if (L.Wallet >= due) { L.Apply("wallet", $"-{due}:체육관 회비{(S.GymDebt > 0 ? "(고준 빚 포함)" : "")}"); S.GymDebt = 0; Say($"회비 -{Ledger.Won(due)}원"); }
            else if (S.GymDebt == 0) { S.GymDebt = 1; AddCount("rel.gojun", 1); Say("회비 모자람 — 고준이 이번 달만 대신 냈다(빚)"); }
            else { S.GymMember = 0; Say("회비 두 달 밀림 — 체육관 쉬기(다시 등록하려면 관장에게)"); }
            S.GymPaidMonth = GameClock.Month;
        }

        // ───────────────────────── 돈
        public void AddHousehold(int v, string memo)
        {
            if (v == 0) return;
            S.Household += v;
            L.Earned += v;
            string line = $"{Ledger.Signed(v)} {memo}".Trim();
            L.Entries.Add(line);
            Say(line);
        }

        /// 이야기 장면의 "ledger:+N:…" 줄(엄마 통장·고지서 밑) — 번 돈을 생활비로 셈(오픈월드에서 StoryRunner 가 부름)
        public void OnLedgerLine(int v) { S.Household += v; }

        /// 말일 이체(docs/13 9-2): 고정 지출 + 집 구멍을 생활비로 → 남으면 지갑, 모자라면 지갑에서 → 그래도 모자라면 결손(N04)
        public int MonthEnd(int month)
        {
            if (S.SettledMonth == month) return S.LastShortfall;
            int due = B.FixedTotal(month);
            int holes = 0;
            foreach (var h in S.Holes) if (!h.Paid) holes += h.Amount;
            due += holes;
            int have = S.Household;
            int shortfall = 0;
            if (have >= due)
            {
                int surplus = have - due;
                if (surplus > 0) L.Apply("wallet", $"+{surplus}:{month}월 생활비 남은 돈");
                Say($"{month}월 말일 이체 — {Ledger.Won(due)}원 완료{(surplus > 0 ? $" · 남은 돈 {Ledger.Won(surplus)}원 → 지갑" : "")}");
            }
            else
            {
                int need = due - have;
                int take = Mathf.Min(need, Mathf.Max(0, L.Wallet));
                if (take > 0) L.Apply("wallet", $"-{take}:{month}월 생활비 모자란 돈");
                shortfall = need - take;
                if (shortfall > 0) { S.Deficits++; Say($"{month}월 말일 이체 — {Ledger.Won(shortfall)}원 모자람(결손 {S.Deficits}번째)"); }
                else Say($"{month}월 말일 이체 — 지갑에서 {Ledger.Won(take)}원 보태 완료");
            }
            foreach (var h in S.Holes) h.Paid = true;
            S.Holes.RemoveAll(h => h.Paid);
            S.Household = 0;
            S.LastShortfall = shortfall;
            S.SettledMonth = month;
            L.Entries.Add($"[{month}월 말일] 이체 {Ledger.Won(due)} · 모자람 {Ledger.Won(shortfall)}");
            return shortfall;
        }

        // ───────────────────────── 알바·활동
        public bool DoneToday(string id) => S.WorkDay == Today && S.DoneToday.Contains(id);
        void MarkDoneToday(string id) { BeginDayIfNeeded(); if (!S.DoneToday.Contains(id)) S.DoneToday.Add(id); }

        /// 날이 바뀌었으면 오늘 칸을 비운다(일터 시간·한 일·하루 상한)
        public void BeginDayIfNeeded()
        {
            if (S.WorkDay == Today) return;
            S.WorkDay = Today;
            S.WorkToday.Clear(); S.DoneToday.Clear();
            S.CoreToday = 0; S.SideToday = 0;
            if (Weekday == "월") S.WeekMinutes = 0;
            F.Set.Remove("sore_today");
            F.Set.Remove("gym_later_today");
            S.Full = 0;
            S.Eats.RemoveAll(e => GameClock.DaysBetween(e.Day, Today) > 7);
        }

        public int Worked(string work) { BeginDayIfNeeded(); return S.Worked(work); }

        /// 지금 시작할 수 있나(빈칸 = 됨, 아니면 이유)
        public string CanStart(ActivityDef a, int now = -1)
        {
            if (a == null) return "없는 일";
            BeginDayIfNeeded();
            if (now < 0) now = Now;
            if (!Unlocked(a.Unlock)) return "아직 열리지 않았다";
            if (a.Kind == "sleep" || a.Kind == "save" || a.Kind == "ledger" || a.Kind == "family") return "";
            if (!a.OnDay(Weekday)) return $"오늘({Weekday})은 없는 날";
            if (a.Id == "school" && Holiday(Today)) return "공휴일 — 학교 안 감";
            if (a.RainOff && (RainToday || F.Has("site_off_tomorrow"))) return "비 — 오늘 현장 없음(곽반장 '내일 없음')";
            if (a.Id == "job.site" && !F.Has("site_booked")) return "곽반장 연락이 아직 없다";
            if (DoneToday(a.Id)) return "오늘은 이미 했다";
            if (now < a.Open) return $"아직 이르다({GameClock.Hm(a.Open)}부터)";
            if (now > a.Latest) return $"늦었다(늦어도 {GameClock.Hm(a.Latest)})";
            // 결정 12: 밤 10시~아침 6시 근로 없음 · 일터마다 하루 7시간
            if (a.IsJob || a.Kind == "gym")
            {
                int s = Mathf.Max(now, a.Start);
                if (a.End > B.MinorNightFrom || s < B.MinorNightTo) return "밤 10시~아침 6시엔 일 못 함(청소년 근로)";
                if (Worked(a.Work) + a.Minutes > B.PerWorkDay) return $"오늘 이 일터 7시간을 다 썼다({Worked(a.Work) / 60f:F1}시간)";
            }
            if (G.Fatigue >= B.Collapse) return "쓰러지기 직전 — 쉬어야 한다";
            return "";
        }

        /// 맡기기(9-1): S 한 번 또는 근무 10번
        public bool CanDelegate(ActivityDef a) => a != null && a.IsJob && (Count("best." + a.Id) >= (int)Grade.S || Count(a.Id) >= B.DelegateShifts);

        public Grade GradeOf(float score)
        {
            var g = B.GradeCuts;
            return score >= g[3] ? Grade.S : score >= g[2] ? Grade.A : score >= g[1] ? Grade.B : score >= g[0] ? Grade.C : Grade.D;
        }

        /// 등급 덤(QA 2차 A1): 기본급 × 등급 비율(S 30% · A 15% · B 5% · C·D 0), 100원 단위. 맡기면 B(데이터 tips.delegated 가 false 면 0)
        public int TipFor(ActivityDef a, int pay, Grade grade, bool delegated)
        {
            if (a == null || !a.IsJob || pay <= 0 || string.IsNullOrEmpty(a.Tip)) return 0;
            if (delegated && !B.TipDelegated) return 0;
            int gi = Mathf.Clamp((int)grade, 0, B.TipRates.Length - 1);
            float rate = B.TipRates.Length > 0 ? B.TipRates[gi] : 0f;
            return Mathf.Max(0, Mathf.RoundToInt(pay * rate / 100f) * 100);
        }

        public int UnitsToday(ActivityDef a) => a.Units != null && a.Units.Length > 0 ? a.Units[Mathf.Abs(DayIndex) % a.Units.Length] : 1;

        /// 활동 끝 — 돈·피로·스탯·평판·셈·일터 시간. 시계는 부르는 쪽이 End 로(직접 놀이는 이미 흐른 만큼)
        public ActivityResult Complete(ActivityDef a, Grade grade, bool delegated, int startMin = -1)
        {
            BeginDayIfNeeded();
            if (startMin < 0) startMin = Now;
            var r = new ActivityResult { Id = a.Id, Grade = grade, Delegated = delegated };
            int s = Mathf.Max(startMin, a.Start);
            float frac = a.End > a.Start ? Mathf.Clamp01((a.End - s) / (float)(a.End - a.Start)) : 1f;
            r.Units = UnitsToday(a);
            int pay = a.Units != null && a.Units.Length > 0 ? a.Pay * r.Units : (frac >= 0.999f ? a.Pay : Mathf.RoundToInt(a.Pay * frac / 100f) * 100);
            r.Pay = pay;
            var parts = new List<string>();
            if (pay > 0)
            {
                if (a.To == "household") AddHousehold(pay, a.Memo);
                else if (a.To == "wallet") L.Apply("wallet", $"+{pay}:{a.Memo}");
                parts.Add($"+{Ledger.Won(pay)}원{(a.To == "household" ? " → 엄마 통장" : " → 지갑")}");
                int tip = TipFor(a, pay, grade, delegated);
                if (tip > 0)
                {
                    r.Tip = tip;
                    L.Apply("wallet", $"+{tip}:{a.Tip}");
                    AddCount("tips", tip);
                    parts.Add($"{a.Tip} +{Ledger.Won(tip)}원({DrillGrader.Letter(grade)}) → 지갑");
                }
            }
            foreach (var it in a.Items) { L.Apply("item", it); parts.Add("얻음 " + it.Replace(":", " ×")); }
            bool over = S.WeekMinutes > B.WeekSoft;
            float fat = a.Fatigue * (over ? B.OverworkMul : 1f);
            r.Fatigue = Mathf.RoundToInt(fat);
            bool tired = G.Fatigue >= B.Tired;
            AddFatigue(fat);
            parts.Add($"피로 +{r.Fatigue}{(over ? "(과로)" : "")}");
            float sm = (delegated ? B.DelegateStat : 1f) * (tired ? B.TiredMul : 1f);
            foreach (var st in a.Stats)
            {
                var kv = st.Split(':');
                if (kv.Length < 2) continue;
                float v = Num(kv[1]) * sm;
                AddStat(kv[0], v);
                int si = StatIndex(kv[0]);
                parts.Add($"{(si >= 0 ? GymSave.StatNames[si] : kv[0])} +{v:0.##}");
            }
            if (a.Rep > 0f && grade >= Grade.B) AddRep(a.Rep);
            if (a.Id == "job.delivery" && grade >= Grade.B) AddCount("delivery_good", 1);
            AddCount(a.Id, 1);
            if (!delegated)
            {
                AddCount("skill." + a.Id, grade switch { Grade.S => 10, Grade.A => 7, Grade.B => 5, Grade.C => 3, _ => 2 });
                S.Set("best." + a.Id, Mathf.Max((int)grade, Count("best." + a.Id)));
            }
            if (a.Id == "school")
            {
                if (startMin > a.Start) { S.Late++; parts.Add("지각"); } else S.Attended++;
            }
            MarkDoneToday(a.Id);
            int minutes = a.End > s ? (a.WorkMinutes > 0 ? Mathf.RoundToInt(a.WorkMinutes * frac) : a.End - s) : 0;
            r.Minutes = minutes;
            if (a.IsJob || a.Kind == "gym") { S.AddWork(a.Work, minutes); S.WeekMinutes += minutes; }
            r.Summary = string.Join(" · ", parts);
            Log.Add($"{GameClock.DateText} {a.Name} {DrillGrader.Letter(grade)}{(delegated ? "(맡김)" : "")}: {r.Summary}");
            return r;
        }

        // ───────────────────────── 잠·하루 넘기기
        /// 다음 기상(월~토 05:30 · 일 08:00). 지금이 기상 시각 전(새벽)이면 오늘, 아니면 내일
        public (int day, int min) NextWake()
        {
            int now = Now;
            int day = Today;
            if (now >= 12 * 60) day = GameClock.KeyPlus(day, 1);
            int wake = GameClock.WeekdayOfKey(day) == "일" ? B.WakeSunday : B.Wake;
            if (day == Today && now >= wake) { day = GameClock.KeyPlus(day, 1); wake = GameClock.WeekdayOfKey(day) == "일" ? B.WakeSunday : B.Wake; }
            return (day, wake);
        }

        // ───────────────────────── 낮잠(QA 2차 A2, 2026-10-26 메인 세션 결정): 일어난 지 6시간이 안 됐으면 '낮잠 2시간'만 — 일어나자마자 다시 자면 다음 기상까지 22~24시간 자던 것
        public const string WokeDayKey = "woke.day", WokeMinKey = "woke.min";
        public const int NapAfterMinutes = 6 * 60, NapMinutes = 120;

        /// 마지막 밤잠에서 일어난 뒤 지난 분(기록이 없으면 — 옛 저장 — 하루)
        public int AwakeMinutes
        {
            get
            {
                int d = Mathf.RoundToInt(S.Get(WokeDayKey));
                if (d <= 0) return 24 * 60;
                return GameClock.DaysBetween(d, Today) * 1440 + Now - Mathf.RoundToInt(S.Get(WokeMinKey));
            }
        }

        /// 지금 자면 낮잠(2시간)인가 — 플레이어가 이불에서 잘 때만(밤 한계 강제 잠·주간 계획의 날 넘기기는 늘 밤잠)
        /// 2026-10-29 대표 결정: 06:00~18:00 에 잠들면 무조건 낮잠 2시간, 18:00 뒤에 잠들어야 다음 날 아침까지.
        /// (밤 시간대라도 깬 지 6시간 안이면 낮잠 — 05:30 에 깨자마자 다시 누우면 24시간 자던 것 막이)
        public const int NapFrom = 6 * 60, NapUntil = 18 * 60;
        public bool NapOnly => (Now >= NapFrom && Now < NapUntil) || (AwakeMinutes >= 0 && AwakeMinutes < NapAfterMinutes);

        /// 낮잠 2시간: 시계 +2시간 · 피로 −(시간당 잠 회복 × 2) · 날짜·말일·봉투·기상 기록은 그대로
        public SleepResult Nap()
        {
            var r = new SleepResult { FromDay = Today, FatigueBefore = G.Fatigue, Hours = NapMinutes / 60 };
            float rec = B.SleepPerHour * NapMinutes / 60f * (SuppOn("vitamin") ? VitaminSleep : 1f);
            G.Fatigue = Mathf.Clamp(Mathf.RoundToInt(G.Fatigue - rec), 0, 100);
            r.FatigueAfter = G.Fatigue;
            GameClock.Advance(NapMinutes);
            r.WakeDay = Today; r.WakeMin = Now;
            r.Summary = $"낮잠 {r.Hours}시간 · 피로 {r.FatigueBefore} → {r.FatigueAfter}";
            Log.Add($"{GameClock.DateText} {r.Summary}");
            return r;
        }

        /// 잠(집): 시간당 피로 −9, 70 이상에서 잤으면 +10('덜 풀림'), 말일을 넘기면 이체, 일요일 밤이면 봉투 적립 5,000(원고).
        /// 밤잠은 다음 기상 시각(월~토 05:30 · 일 08:00)까지 — NextWake 가 상한. 일어난 지 6시간 안이면 플레이어 잠은 낮잠(Nap)으로(WorldRunner.Sleep)
        public SleepResult Sleep(bool forced = false)
        {
            var r = new SleepResult { FromDay = Today, FatigueBefore = G.Fatigue };
            var (wd, wm) = NextWake();
            int mins = GameClock.DaysBetween(Today, wd) * 1440 + wm - Now;
            r.Hours = Mathf.Max(0, Mathf.RoundToInt(mins / 60f));
            // 일요일 밤 봉투(원고 5화) — 규칙이 열린 뒤, 봉투가 목표 전이면
            if (Weekday == "일" && F.Has("envelope_rule") && L.Envelope < B.EnvelopeGoal && L.Wallet >= B.EnvelopeSunday)
            {
                L.Apply("wallet", $"-{B.EnvelopeSunday}:봉투로");
                L.Apply("envelope", $"+{B.EnvelopeSunday}");
                Say($"일요일 밤 봉투 +{Ledger.Won(B.EnvelopeSunday)} · {L.EnvelopeText}");
            }
            int fromMonth = GameClock.Month;
            int toMonth = wd / 100;
            if (toMonth != fromMonth) { r.Shortfall = MonthEnd(fromMonth); r.MonthSettled = true; }
            float rec = B.SleepPerHour * mins / 60f * (SuppOn("vitamin") ? VitaminSleep : 1f);
            bool tiredSleep = G.Fatigue >= B.Tired;
            int tiredAdd = tiredSleep ? (SuppOn("magnesium") ? B.SleptTiredAdd / 2 : B.SleptTiredAdd) : 0;
            G.Fatigue = Mathf.Clamp(Mathf.RoundToInt(G.Fatigue - rec) + tiredAdd + (forced ? B.NightOut : 0), 0, 100);
            if (SuppOn("magnesium") && G.InjuryUntil >= 0) Heal(1);
            r.FatigueAfter = G.Fatigue;
            r.WakeDay = wd; r.WakeMin = wm;
            int y = GameClock.Year;
            if (wd < Today) y++;
            GameClock.SetDate(y, wd / 100, wd % 100, wm);
            BeginDayIfNeeded();
            S.Slept++;
            S.Set(WokeDayKey, wd); S.Set(WokeMinKey, wm);
            r.Summary = $"{r.Hours}시간 잠 · 피로 {r.FatigueBefore} → {r.FatigueAfter}{(tiredSleep ? "(덜 풀림)" : "")}{(r.MonthSettled ? $" · {fromMonth}월 말일 이체" : "")}";
            S.Days.Add($"{r.FromDay / 100}/{r.FromDay % 100} — 지갑 {Ledger.Won(L.Wallet)} · 생활비 {Ledger.Won(S.Household)} · 피로 {r.FatigueBefore} · 평판 {S.TownRep:0.#}");
            if (S.Days.Count > 120) S.Days.RemoveAt(0);
            F.Set.Remove("site_off_tomorrow");
            return r;
        }

        // ───────────────────────── 목표(4-1·W11)
        public float GoalValue(GoalDef g)
        {
            var e = g.Expr;
            if (e.StartsWith("card.")) return CardDone(e.Substring(5)) ? 1 : 0;
            return Value(e);
        }

        public bool GoalMet(GoalDef g) => g.Max ? GoalValue(g) <= g.Target + 1e-4f : GoalValue(g) >= g.Target - 1e-4f;

        public bool GoalsMet(ChapterDef ch, bool demo)
        {
            if (ch == null) return false;
            var gs = demo ? ch.DemoGoals : ch.Goals;
            if (gs == null || gs.Length == 0) return false;
            foreach (var g in gs) if (!GoalMet(g)) return false;
            return true;
        }

        public string GoalLine(GoalDef g)
        {
            float v = GoalValue(g);
            string val = g.Expr.StartsWith("card.") || g.Expr.StartsWith("flag.") ? (v >= 1 ? "완료" : "아직") : g.Max ? $"{v:0}{g.Unit} (≤{g.Target:0})" : $"{v:0.#} / {g.Target:0}{g.Unit}";
            return $"{(GoalMet(g) ? "■" : "□")} {g.Label} — {val}";
        }

        // ───────────────────────── 소문(W12)
        /// 소문 단계(0~3)별 동네 말 — 누가·날짜로 고정(결정적)
        public string RumorLine(string who)
        {
            var r = B.Rumor;
            if (r == null || r.Length == 0) return "";
            int st = Mathf.Clamp(S.Rumor, 0, r.Length - 1);
            var lines = r[st].Lines;
            if (lines == null || lines.Length == 0 || lines[0].Length == 0) return "";
            int i = Mathf.FloorToInt(Hash01(S.Seed, Today, who ?? "") * lines.Length) % lines.Length;
            return lines[i];
        }

        // ───────────────────────── M3 저장(v1) 이주(W13)
        /// v1 '장면 시작 저장' → 그 장면이 든 카드부터. 앞선 카드(기본 순서)는 끝낸 것으로, 그 카드들의 열림·플래그는 다시 건다
        public static WorldSave MigrateV1(WorldDef def, SaveData v1, out string card)
        {
            var ws = NewSave(def);
            card = null;
            int at = -1;
            for (int i = 0; i < def.Cards.Length && v1 != null; i++)
            {
                var c = def.Cards[i];
                if (!c.Demo || !c.HasScenes) continue;
                if (System.Array.IndexOf(c.Scenes, v1.SceneId) >= 0) { at = i; card = c.Id; break; }
            }
            if (at < 0) { card = "C1-01"; ws.Card = card; return ws; }
            for (int i = 0; i < at; i++)
            {
                var c = def.Cards[i];
                if (!c.Demo || !c.HasScenes || c.Group.Length > 0) continue;
                ws.Cards.Add(new CardState { Id = c.Id, State = 1, Day = c.Date > 0 ? c.Date : c.WinFrom, Times = 1 });
                foreach (var e in c.After)
                {
                    if (e.StartsWith("unlock:")) { var k = e.Substring(7); if (!ws.Unlocks.Contains(k)) ws.Unlocks.Add(k); }
                    if (e.StartsWith("did:")) { var k = e.Substring(4); bool found = false; foreach (var cn in ws.Counters) if (cn.Key == k) { cn.Value += 1; found = true; } if (!found) ws.Counters.Add(new Counter { Key = k, Value = 1 }); }
                }
            }
            var target = def.Cards[at];
            var sc = target.Place;
            ws.Card = card;
            ws.Place = string.IsNullOrEmpty(sc) ? "Zone1" : sc;
            ws.Spawn = "Spawn";
            ws.HasPos = false;
            return ws;
        }

        // ───────────────────────── 요약
        public string Status => $"{GameClock.Label} · 지갑 {Ledger.Won(L.Wallet)} · 봉투 {Ledger.Won(L.Envelope)} · 생활비 {Ledger.Won(S.Household)} · 피로 {G.Fatigue} · 평판 {S.TownRep:0.#}({B.RepName(S.TownRep)}) · 결손 {S.Deficits} · 카드 {S.Cards.FindAll(c => c.State > 0).Count}";
    }
}
