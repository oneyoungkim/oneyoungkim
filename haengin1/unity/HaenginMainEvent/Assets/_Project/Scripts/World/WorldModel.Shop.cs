// 행인1의 메인이벤트 — 돈 쓸 곳 규칙(docs/13 14-6, 순수 C# — 시험 W17~W22): 개인레슨 예약·노쇼·레슨 효과 · 먹을 것(피로·시간 제한 효과·배부름·체감 감소) · 영양제(며칠 꾸준히).
// 장면 쪽(체육관 레슨 진행·가게 고르기·가방·HUD 아이콘)은 WorldRunner.Shop.cs.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Haengin
{
    public sealed partial class WorldModel
    {
        public ShopDef Sh => Def.Shop;
        /// 잠 회복 배율(종합비타민 3일 이상)
        public const float VitaminSleep = 1.15f;
        public static readonly string WeekDays = "월화수목금토일";

        /// 시작일부터 센 분(효과 끝 시각 비교용)
        public int AbsNow => DayIndex * 1440 + Now;
        public int AbsOf(int day, int min) => GameClock.DaysBetween(B.StartDate, day) * 1440 + min;
        public int WeekStart(int day) { int i = WeekDays.IndexOf(GameClock.WeekdayOfKey(day)); return GameClock.KeyPlus(day, -Mathf.Max(0, i)); }

        // ───────────────────────── 효과(버프)
        public float BuffValue(string kind)
        {
            float v = 0f; int now = AbsNow;
            foreach (var b in S.Buffs) if (b.Kind == kind && b.Until > now) v = Mathf.Max(v, b.Value);
            return v;
        }

        public int BuffLeft(string kind)
        {
            int m = 0, now = AbsNow;
            foreach (var b in S.Buffs) if (b.Kind == kind && b.Until > now) m = Mathf.Max(m, b.Until - now);
            return m;
        }

        /// 같은 종류는 한 칸: 값은 큰 쪽, 끝 시각은 늦은 쪽
        public void AddBuff(string kind, float value, int minutes, string src)
        {
            if (value <= 0f || minutes <= 0) return;
            PruneBuffs();
            int until = AbsNow + minutes;
            foreach (var b in S.Buffs)
                if (b.Kind == kind) { b.Value = Mathf.Max(b.Value, value); b.Until = Mathf.Max(b.Until, until); b.Src = src; return; }
            S.Buffs.Add(new Buff { Kind = kind, Value = value, Until = until, Src = src });
        }

        public void PruneBuffs() { int now = AbsNow; S.Buffs.RemoveAll(b => b.Until <= now); }
        /// 드는 피로 깎는 비율(스태미나) · 싸움 체력 + 비율 · 드릴 판정 +점(컨디션)
        public float StaminaCut => Mathf.Clamp01(BuffValue("stamina") / 100f);
        public float HpBonus => BuffValue("hp") / 100f;
        public float CondBonus => BuffValue("cond");

        public void Heal(int days)
        {
            if (G.InjuryUntil < 0) return;
            for (int i = 0; i < days; i++) G.InjuryUntil = GameClock.KeyPlus(G.InjuryUntil, -1);
            if (!G.Injured(Today)) { G.InjuryUntil = -1; G.InjuryKind = ""; }
        }

        // ───────────────────────── 먹을 것
        Eaten EatRec(string id, int day) { foreach (var e in S.Eats) if (e.Id == id && e.Day == day) return e; return null; }

        /// 효과 배율: 같은 날 n번째(1 · .6 · .35 · .2) × 며칠 연달아(어제도 먹었으면 ×.85 씩, 최저 .55)
        public float EatFactor(FoodDef f)
        {
            if (f.IsSupp) return 1f;
            var today = EatRec(f.Id, Today);
            int n = today != null ? today.Times : 0;
            float k = Sh.SameDay.Length == 0 ? 1f : Sh.SameDay[Mathf.Min(n, Sh.SameDay.Length - 1)];
            int streak = 0;
            for (int d = 1; d <= 6; d++) { if (EatRec(f.Id, GameClock.KeyPlus(Today, -d)) != null) streak++; else break; }
            return k * Mathf.Max(Sh.StreakMin, Mathf.Pow(Sh.StreakMul, streak));
        }

        public int Doses(string supp) => Mathf.RoundToInt(Count("dose." + supp));
        public int SuppStreak(string supp) => Mathf.RoundToInt(Count("supp." + supp + ".streak"));
        /// 꾸준히 먹는 중(3일 이상 · 어제나 오늘 먹음)
        public bool SuppOn(string supp) { int last = Mathf.RoundToInt(Count("supp." + supp + ".last")) - 1; return last >= 0 && SuppStreak(supp) >= Sh.SuppDays && DayIndex - last <= 1; }

        /// 먹을 수 있나(빈 글 = 됨)
        public string CanEat(FoodDef f, bool fromBag)
        {
            if (f == null) return "없는 것";
            if (f.IsSupp)
            {
                if (Doses(f.Supp) <= 0) return "다 먹었다";
                if (Mathf.RoundToInt(Count("supp." + f.Supp + ".last")) == DayIndex + 1) return "오늘 먹었다";
                return "";
            }
            if (fromBag && !HasItem(f)) return "가방에 없다";
            if (S.Full + f.Full > Sh.FullCap) return $"배부르다(오늘 {S.Full}/{Sh.FullCap})";
            return "";
        }

        public bool HasItem(FoodDef f) { foreach (var it in L.Items) if (it == f.Name || Array.IndexOf(f.Aka, it) >= 0) return true; return false; }
        void TakeItem(FoodDef f) { int i = L.Items.FindIndex(it => it == f.Name || Array.IndexOf(f.Aka, it) >= 0); if (i >= 0) L.Items.RemoveAt(i); }

        /// 사기(가방에 넣거나 영양제 30일분). 돌려주는 것 = 막힌 까닭(빈 글 = 삼)
        public string Buy(FoodDef f)
        {
            if (f == null) return "없는 것";
            if (f.Free) return "파는 게 아니다";
            if (L.Wallet < f.Price) return $"돈이 모자란다({Ledger.Won(f.Price)}원)";
            L.Apply("wallet", $"-{f.Price}:{f.Name}");
            AddCount("spent.food", f.Price);
            if (f.IsSupp) { AddCount("dose." + f.Supp, f.Doses); Say($"{f.Name} 샀다 — {f.Effect}"); }
            else if (f.Carry) { L.Items.Add(f.Name); Say($"{f.Name} — 가방에(-{Ledger.Won(f.Price)})"); }
            return "";
        }

        /// 먹기(가게 자리에서 = 그 자리에서 사서 먹음, fromBag = 가방에서). 효과 한 줄을 돌려준다(막혔으면 까닭)
        public string Eat(FoodDef f, bool fromBag, bool free = false)
        {
            string why = CanEat(f, fromBag);
            if (why.Length > 0) return $"{f?.Name} — {why}";
            if (f.IsSupp)
            {
                int last = Mathf.RoundToInt(Count("supp." + f.Supp + ".last")) - 1;   // 저장은 DayIndex + 1(0 = 안 먹음)
                int gap = last < 0 ? 99 : DayIndex - last;
                int st = SuppStreak(f.Supp);
                st = gap == 1 ? st + 1 : Mathf.Max(0, st - Sh.SuppMiss * Mathf.Max(0, gap - 1)) + 1;
                if (last < 0) st = 1;
                S.Set("supp." + f.Supp + ".streak", st);
                S.Set("supp." + f.Supp + ".last", DayIndex + 1);
                AddCount("dose." + f.Supp, -1);
                AddCount("took." + f.Supp, 1);
                string eff = f.Effect.IndexOf('—') >= 0 ? f.Effect.Substring(f.Effect.IndexOf('—') + 1).Trim() : f.Effect;
                return $"{f.Name.Split('(')[0]} — {st}일째{(st >= Sh.SuppDays ? " · 효과 " + eff : $" (꾸준히 {Sh.SuppDays}일부터)")}";
            }
            if (!fromBag && !free && !f.Free)
            {
                if (L.Wallet < f.Price) return $"{f.Name} — 돈이 모자란다({Ledger.Won(f.Price)}원)";
                L.Apply("wallet", $"-{f.Price}:{f.Name}");
                AddCount("spent.food", f.Price);
            }
            if (fromBag) TakeItem(f);
            float k = EatFactor(f);
            int before = G.Fatigue;
            G.Fatigue = Mathf.Clamp(G.Fatigue - Mathf.RoundToInt(f.Fatigue * k), 0, 100);
            var parts = new List<string>();
            if (before != G.Fatigue) parts.Add($"피로 {before}→{G.Fatigue}");
            foreach (var b in f.Buffs)
            {
                float v = Mathf.Round(b.Value * k * 10f) / 10f;
                if (v <= 0f) continue;
                AddBuff(b.Kind, v, b.Minutes, f.Id);
                parts.Add($"{Sh.BuffName(b.Kind)} +{v:0.#}{(b.Kind == "cond" ? "" : "%")} {b.Minutes / 60f:0.#}시간");
            }
            if (f.Heal > 0 && G.InjuryUntil >= 0) { Heal(f.Heal); parts.Add("부상 하루 빨리"); }
            S.Full += f.Full;
            var rec = EatRec(f.Id, Today);
            if (rec == null) S.Eats.Add(rec = new Eaten { Id = f.Id, Day = Today });
            rec.Times++;
            AddCount("ate." + f.Id, 1);
            AddCount("ate.total", 1);
            string tail = k < 0.99f ? $" (체감 ×{k:0.##})" : "";
            return $"{f.Name} — {(parts.Count > 0 ? string.Join(" · ", parts) : "배만 부르다")}{tail}";
        }

        // ───────────────────────── 개인레슨
        public bool CoachOpen(CoachDef c) => c != null && (string.IsNullOrEmpty(c.Unlock) || Check(c.Unlock));
        public int Tickets(string coach) => Mathf.RoundToInt(Count("ticket." + coach));
        public bool CoachBlocked(CoachDef c) => Count("block." + c.Id) > DayIndex;

        public int WeekCount(string coach, int day)
        {
            int ws = WeekStart(day), n = 0;
            foreach (var b in S.Lessons) if (b.Coach == coach && WeekStart(b.Day) == ws) n++;
            return n;
        }

        bool Overlaps(int day, int at)
        {
            foreach (var b in S.Lessons) if (b.Day == day && (b.State == 0 || b.State == 1) && Mathf.Abs(b.At - at) < Sh.Minutes) return true;
            return false;
        }

        /// 앞으로 days 일 안에 예약할 수 있는 칸(지난 시각·주당 상한·겹침·체육관 시간 뺌)
        public List<(int day, int at, string label)> LessonSlots(CoachDef c, int days = 7)
        {
            var r = new List<(int, int, string)>();
            if (c == null) return r;
            int now = AbsNow;
            for (int d = 0; d < days; d++)
            {
                int key = GameClock.KeyPlus(Today, d);
                string w = GameClock.WeekdayOfKey(key);
                foreach (var s in c.Slots)
                {
                    if (s.Days.IndexOf(w, StringComparison.Ordinal) < 0) continue;
                    if (AbsOf(key, s.At) - Sh.GraceBefore <= now) continue;
                    if (s.At < B.GymOpen || s.At + Sh.Minutes > B.GymClose) continue;
                    if (WeekCount(c.Id, key) >= c.Week) continue;
                    if (Overlaps(key, s.At)) continue;
                    r.Add((key, s.At, s.Label));
                }
            }
            return r;
        }

        /// 예약(휴대폰). useTicket = 10회권에서 한 회. 돌려주는 것 = 막힌 까닭(빈 글 = 됨)
        public string Book(CoachDef c, int day, int at, bool useTicket)
        {
            if (c == null) return "없는 코치";
            if (!CoachOpen(c)) return "아직 안 열림";
            if (CoachBlocked(c)) return $"{c.Name} — 이번 주는 예약을 안 받는다(노쇼)";
            bool ok = false;
            foreach (var s in LessonSlots(c, 14)) if (s.day == day && s.at == at) { ok = true; break; }
            if (!ok) return "그 시간은 안 된다";
            var bk = new Booking { Coach = c.Id, Day = day, At = at };
            if (useTicket)
            {
                if (Tickets(c.Id) <= 0) return "회권이 없다";
                AddCount("ticket." + c.Id, -1); bk.Ticket = true;
            }
            else
            {
                if (L.Wallet < c.Price) return $"돈이 모자란다({Ledger.Won(c.Price)}원)";
                L.Apply("wallet", $"-{c.Price}:{c.Name} 레슨");
                AddCount("spent.lesson", c.Price);
                bk.Paid = c.Price;
            }
            S.Lessons.Add(bk);
            string line = Sh.Line(c.Id, "book", day);
            S.Phone.Add(new PhoneMsg { Day = Today, Min = Now, From = c.Name, Text = $"{day / 100}/{day % 100}({GameClock.WeekdayOfKey(day)}) {GameClock.Hm(at)} 레슨 — {line}" });
            Say($"레슨 예약 — {c.Name} {day / 100}/{day % 100} {GameClock.Hm(at)}{(bk.Ticket ? $" (회권 남은 {Tickets(c.Id)})" : $" -{Ledger.Won(c.Price)}")}");
            return "";
        }

        public string BuyTicket(CoachDef c)
        {
            if (c == null || c.Ticket10 <= 0) return "10회권이 없다";
            if (L.Wallet < c.Ticket10) return $"돈이 모자란다({Ledger.Won(c.Ticket10)}원)";
            L.Apply("wallet", $"-{c.Ticket10}:{c.Name} 10회권");
            AddCount("spent.lesson", c.Ticket10);
            AddCount("ticket." + c.Id, 10);
            Say($"{c.Name} 10회권 — 회당 {Ledger.Won(c.Ticket10 / 10)}원");
            return "";
        }

        /// 지금 받을 수 있는 레슨(시작 15분 전 ~ 20분 뒤)
        public Booking LessonNow()
        {
            int now = AbsNow;
            foreach (var b in S.Lessons)
            {
                if (b.State != 0) continue;
                int t = AbsOf(b.Day, b.At);
                if (now >= t - Sh.GraceBefore && now <= t + Sh.GraceAfter) return b;
            }
            return null;
        }

        /// 다음 예약(안내·봇)
        public Booking NextLesson()
        {
            Booking best = null; int bt = int.MaxValue, now = AbsNow;
            foreach (var b in S.Lessons)
            {
                if (b.State != 0) continue;
                int t = AbsOf(b.Day, b.At);
                if (t + Sh.GraceAfter >= now && t < bt) { bt = t; best = b; }
            }
            return best;
        }

        /// 시간이 지난 예약 = 노쇼: 돈은 날아가고 신뢰 −2(−4 까지 떨어지면 7일 예약 안 받음)
        public List<Booking> NoShows()
        {
            var r = new List<Booking>();
            int now = AbsNow;
            foreach (var b in S.Lessons)
            {
                if (b.State != 0 || now <= AbsOf(b.Day, b.At) + Sh.GraceAfter) continue;
                b.State = 2;
                r.Add(b);
                var c = Sh.Coach(b.Coach);
                AddCount("rel." + b.Coach, Sh.NoshowRel);
                AddCount("noshow." + b.Coach, 1);
                AddCount("noshow", 1);
                string name = c != null ? c.Name : b.Coach;
                S.Phone.Add(new PhoneMsg { Day = Today, Min = Now, From = name, Text = Sh.Line(b.Coach, "noshow", b.Day) });
                string tail = "";
                if (Rel(b.Coach) <= Sh.BlockRel) { S.Set("block." + b.Coach, DayIndex + Sh.BlockDays); S.Set("rel." + b.Coach, Sh.NoshowRel); tail = $" · {Sh.BlockDays}일 예약 안 받음"; }
                Say($"노쇼 — {name} {b.Day / 100}/{b.Day % 100} {GameClock.Hm(b.At)}{(b.Paid > 0 ? $" · {Ledger.Won(b.Paid)}원 날아감" : " · 회권 한 회 날아감")} · 신뢰 {Sh.NoshowRel}{tail}");
            }
            return r;
        }

        /// 레슨 중인 코치(장면이 넣음 — 드릴 판정 보정·보상 배율)
        public CoachDef LessonCoach;
        public Booking LessonBooking;

        /// 드릴 판정 보정(점수 +, 최저 지표 +): 컨디션 · 레슨 코치 · 익힌 기술 · 교정
        public (double score, double metric) DrillAssist(DrillDef d)
        {
            if (d == null) return (0, 0);
            double s = CondBonus, m = 0;
            if (LessonCoach != null) { s += LessonCoach.Bonus; m += LessonCoach.Metric; }
            foreach (var c in Sh.Coaches)
                if (c.HasTech && Unlocked(c.Tech.Id) && Array.IndexOf(c.Tech.Drills, d.Id) >= 0) s += c.Tech.Plus;
            s += Count("fix." + d.Id);
            return (s, m);
        }

        public static string Grade2(int g) => g >= 0 && g <= 4 ? DrillGrader.Letter((Grade)g) : "-";

        /// 레슨 드릴이 끝남 — 기본 보상(DrillRewards.Apply)에 더할 몫: 스탯 (배율−1) · 피로 (배율−1) · 신뢰 +1 · 기술 · 교정
        public string LessonDone(Booking bk, DrillDef d, DrillResult r, DrillReward w, bool delegated = false)
        {
            var c = bk != null ? Sh.Coach(bk.Coach) : null;
            if (c == null || d == null) return "";
            bk.State = delegated ? 3 : 1; bk.Drill = d.Id; bk.Grade = r != null ? (int)r.Grade : (int)Grade.B;
            float k = delegated ? Sh.AutoMul : 1f;
            float extraMain = w.MainDelta * (c.Mul - 1f) * k, extraSub = w.SubDelta * (c.Mul - 1f) * k;
            if (d.MainStat != Stat.None) AddStat(WeekPlanner.StatKey(d.MainStat), extraMain);
            if (d.SubStat != Stat.None) AddStat(WeekPlanner.StatKey(d.SubStat), extraSub);
            AddFatigue(w.Fatigue * (c.FatigueMul - 1f));
            AddCount("rel." + c.Id, Sh.LessonRel);
            AddCount("lesson." + c.Id, 1);
            AddCount("lesson", 1);
            var parts = new List<string> { $"{c.Name} 레슨 {d.NameKr} {Grade2(bk.Grade)}", $"스탯 ×{c.Mul * k:0.##}" };
            if (!delegated && c.HasTech && !Unlocked(c.Tech.Id) && Count("lesson." + c.Id) >= c.Tech.Lessons && Array.IndexOf(c.Tech.Drills, d.Id) >= 0 && bk.Grade >= c.Tech.Grade)
            {
                Unlock(c.Tech.Id);
                parts.Add($"기술 「{c.Tech.Name}」");
                Say($"기술 익힘 — 「{c.Tech.Name}」: {c.Tech.Text}");
            }
            if (!delegated && c.Fix > 0f && r != null && r.Scores.Length > 0)
            {
                int lo = 0; for (int i = 1; i < r.Scores.Length; i++) if (r.Scores[i] < r.Scores[lo]) lo = i;
                S.Set("fix." + d.Id, c.Fix);
                S.Set("fixm." + d.Id, lo);
                parts.Add($"교정 {d.Metrics[lo].LabelKr} — 다음 {d.NameKr} +{c.Fix:0}");
            }
            return string.Join(" · ", parts);
        }

        /// 교정 보정은 다음 한 판에만(레슨 아닌 판이 끝나면 지움)
        public void UseFix(DrillDef d) { if (d != null && LessonCoach == null && Count("fix." + d.Id) > 0) S.Set("fix." + d.Id, 0); }

        /// 주간 계획(맡기기)이 그 날 예약을 대신 감: B 등급 보상 × 0.75, 기술·교정 없음
        public string LessonAuto(Booking bk)
        {
            var c = Sh.Coach(bk.Coach);
            if (c == null || Def.Drills == null) return "";
            DrillDef d = null;
            foreach (var id in c.Drills) { if (id == "*") continue; d = Def.Drills.Find(id); if (d != null) break; }
            if (d == null) d = Def.Drills.Find("D04");
            if (d == null) return "";
            var w = DrillRewards.Compute(d, Grade.B, false, G, false);
            // 기본 몫(B) × 0.75 를 직접 + 레슨 몫
            if (d.MainStat != Stat.None) AddStat(WeekPlanner.StatKey(d.MainStat), w.MainDelta * Sh.AutoMul);
            if (d.SubStat != Stat.None) AddStat(WeekPlanner.StatKey(d.SubStat), w.SubDelta * Sh.AutoMul);
            AddFatigue(w.Fatigue);
            return LessonDone(bk, d, null, w, true);
        }

        // ───────────────────────── 매주 고정 레슨(PT 고정 시간 — 주간 계획 맡기기 중에도 그대로 감)
        public static int WeekIndex(int day) => Mathf.Max(0, WeekDays.IndexOf(GameClock.WeekdayOfKey(day)));
        public bool IsStanding(string coach, int day, int at) => S.Standing.Exists(s => s.Coach == coach && s.Day == WeekIndex(day) && s.At == at);

        public void SetStanding(CoachDef c, int day, int at)
        {
            if (c == null || IsStanding(c.Id, day, at)) return;
            S.Standing.Add(new Booking { Coach = c.Id, Day = WeekIndex(day), At = at });
            Say($"고정 레슨 — 매주 {WeekDays[WeekIndex(day)]} {GameClock.Hm(at)} {c.Name}");
        }

        public void ClearStanding() { if (S.Standing.Count > 0) Say("고정 레슨 모두 풀었다"); S.Standing.Clear(); }

        /// 그 날 고정 레슨 예약(하루 한 번). 돌려주는 것 = 새로 잡은 수
        public int RenewStanding(int day)
        {
            if (Count("stand." + day) > 0) return 0;
            S.Set("stand." + day, 1);
            int n = 0, wi = WeekIndex(day);
            foreach (var st in S.Standing.ToArray())
            {
                if (st.Day != wi) continue;
                if (S.Lessons.Exists(b => b.Coach == st.Coach && b.Day == day && b.At == st.At)) continue;
                var c = Sh.Coach(st.Coach);
                if (c == null) continue;
                string why = Book(c, day, st.At, Tickets(c.Id) > 0);
                if (why.Length == 0) n++;
                else Say($"고정 레슨 {c.Name} {GameClock.Hm(st.At)} — {why}");
            }
            return n;
        }

        /// 오늘 맡기기로 지나갈 예약(주간 계획)
        public List<Booking> LessonsOn(int day) { var r = new List<Booking>(); foreach (var b in S.Lessons) if (b.State == 0 && b.Day == day) r.Add(b); return r; }

        public string BuffLine()
        {
            var parts = new List<string>();
            foreach (var k in Sh.BuffKinds)
            {
                float v = BuffValue(k); if (v <= 0f) continue;
                int m = BuffLeft(k);
                parts.Add($"{Sh.BuffName(k)} +{v:0.#}{(k == "cond" ? "" : "%")} {m / 60}:{m % 60:00}");
            }
            foreach (var f in Sh.Foods) if (f.IsSupp && SuppStreak(f.Supp) > 0) parts.Add($"{f.Name.Split('(')[0]} {SuppStreak(f.Supp)}일{(SuppOn(f.Supp) ? "(효과)" : "")}");
            return string.Join(" · ", parts);
        }
    }
}
