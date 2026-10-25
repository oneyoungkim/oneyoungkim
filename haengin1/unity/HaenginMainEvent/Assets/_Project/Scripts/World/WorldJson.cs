// 행인1의 메인이벤트 — Data/World/*.json → WorldDef(결정적 변환, docs/13 12-4 0단계). 에디터 WorldSetup 이 에셋에 쓰고, 시험 W01 이 두 번 돌려 같은지 본다.
// JsonUtility 는 배열 속 배열(points·rumor)·선택 칸을 못 읽어서 작은 JSON 읽기를 같이 둔다(Editor/MiniJson 과 같은 규칙 — 게임 어셈블리용).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Haengin
{
    public static class WorldJson
    {
        // ───────────────────────── 읽기
        public static object Parse(string text) { var r = new R(text); r.Ws(); var v = r.Value(); return v; }

        sealed class R
        {
            readonly string s; public int I;
            public R(string s) { this.s = s ?? ""; }
            Exception Err(string m) => new FormatException($"JSON 읽기 실패: {m} (위치 {I})");
            char Peek() => I < s.Length ? s[I] : throw Err("파일이 중간에 끝남");
            public void Ws() { while (I < s.Length && (s[I] == ' ' || s[I] == '\t' || s[I] == '\n' || s[I] == '\r' || s[I] == '﻿')) I++; }
            public object Value()
            {
                switch (Peek())
                {
                    case '{': return Obj();
                    case '[': return Arr();
                    case '"': return Str();
                    case 't': I += 4; return true;
                    case 'f': I += 5; return false;
                    case 'n': I += 4; return null;
                    default: return Num();
                }
            }
            Dictionary<string, object> Obj()
            {
                var d = new Dictionary<string, object>(); I++; Ws();
                if (Peek() == '}') { I++; return d; }
                while (true)
                {
                    Ws(); string k = Str(); Ws();
                    if (Peek() != ':') throw Err("':' 없음");
                    I++; Ws(); d[k] = Value(); Ws();
                    char c = Peek(); I++;
                    if (c == ',') continue;
                    if (c == '}') return d;
                    throw Err("',' 또는 '}' 없음");
                }
            }
            List<object> Arr()
            {
                var l = new List<object>(); I++; Ws();
                if (Peek() == ']') { I++; return l; }
                while (true)
                {
                    Ws(); l.Add(Value()); Ws();
                    char c = Peek(); I++;
                    if (c == ',') continue;
                    if (c == ']') return l;
                    throw Err("',' 또는 ']' 없음");
                }
            }
            string Str()
            {
                var sb = new StringBuilder(); I++;
                while (I < s.Length)
                {
                    char c = s[I++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }
                    char e = Peek(); I++;
                    switch (e)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'r': sb.Append('\r'); break;
                        case 'u': sb.Append((char)Convert.ToInt32(s.Substring(I, 4), 16)); I += 4; break;
                        default: sb.Append(e); break;
                    }
                }
                throw Err("문자열이 닫히지 않음");
            }
            double Num()
            {
                int st = I;
                while (I < s.Length && "+-0123456789.eE".IndexOf(s[I]) >= 0) I++;
                return double.Parse(s.Substring(st, I - st), NumberStyles.Float, CultureInfo.InvariantCulture);
            }
        }

        static Dictionary<string, object> O(object o) => o as Dictionary<string, object> ?? new Dictionary<string, object>();
        static List<object> A(object o) => o as List<object> ?? new List<object>();
        static object G(Dictionary<string, object> d, string k) => d.TryGetValue(k, out var v) ? v : null;
        static string S(object o, string d = "") => o as string ?? d;
        static double N(object o, double d = 0) => o is double x ? x : d;
        static bool B(object o) => o is bool b && b;
        static string[] SA(object o) => A(o).Select(x => S(x)).ToArray();
        static float[] FA(object o) => A(o).Select(x => (float)N(x)).ToArray();

        /// "HH:MM" → 분(빈칸 = −1)
        public static int Hm(string s)
        {
            if (string.IsNullOrEmpty(s)) return -1;
            var p = s.Split(':');
            return p.Length < 2 ? -1 : int.Parse(p[0], CultureInfo.InvariantCulture) * 60 + int.Parse(p[1], CultureInfo.InvariantCulture);
        }

        /// "MM-DD" → 월 × 100 + 일(빈칸 = 0)
        public static int Md(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            var p = s.Split('-');
            return p.Length < 2 ? 0 : int.Parse(p[0], CultureInfo.InvariantCulture) * 100 + int.Parse(p[1], CultureInfo.InvariantCulture);
        }

        static CardKind Kind(string k) => k switch
        {
            "core" => CardKind.Core, "season" => CardKind.Season, "cond" => CardKind.Cond, "side" => CardKind.Side,
            "repeat" => CardKind.Repeat, "msg" => CardKind.Msg, "system" => CardKind.System, _ => CardKind.Cond,
        };

        static GoalDef Goal(object o)
        {
            var e = O(o);
            return new GoalDef { Label = S(G(e, "label")), Expr = S(G(e, "expr")), Unit = S(G(e, "unit")), Target = (float)N(G(e, "target")), Max = B(G(e, "max")) };
        }

        /// 원본 글들을 WorldDef 에 채운다(같은 입력 → 같은 결과). events = 파일 이름순(ch1, ch2_6 …)
        public static void Fill(WorldDef def, string chapters, string activities, string balance, IList<string> events, string mapZones = null)
        {
            var cj = O(Parse(chapters));
            def.Version = (int)N(G(cj, "version"), 1);
            def.Chapters = A(G(cj, "chapters")).Select(o =>
            {
                var e = O(o);
                var c = new ChapterDef
                {
                    Id = S(G(e, "id")), No = (int)N(G(e, "no")), Title = S(G(e, "title")), Period = S(G(e, "period")), Age = S(G(e, "age")),
                    Ladder = S(G(e, "ladder")), Goal = S(G(e, "goal")), Climax = S(G(e, "climax")), Unlocks = S(G(e, "unlocks")), Promote = S(G(e, "promote")),
                    Goals = A(G(e, "conds")).Select(Goal).ToArray(),
                };
                var demo = G(e, "demo") as Dictionary<string, object>;
                if (demo != null)
                {
                    c.HasDemo = true; c.DemoFrom = Md(S(G(demo, "from"))); c.DemoTo = Md(S(G(demo, "to")));
                    c.DemoTitle = S(G(demo, "title")); c.DemoGoal = S(G(demo, "goal"));
                    c.DemoGoals = A(G(demo, "conds")).Select(Goal).ToArray();
                }
                return c;
            }).ToArray();

            var aj = O(Parse(activities));
            def.Activities = A(G(aj, "activities")).Select(o =>
            {
                var e = O(o);
                return new ActivityDef
                {
                    Id = S(G(e, "id")), Name = S(G(e, "name")), Kind = S(G(e, "kind")), Work = S(G(e, "work")), Place = S(G(e, "place")),
                    Markers = SA(G(e, "markers")), Days = S(G(e, "days")),
                    Open = Math.Max(0, Hm(S(G(e, "open")))), Start = Math.Max(0, Hm(S(G(e, "start")))), End = Math.Max(0, Hm(S(G(e, "end")))), Latest = Math.Max(0, Hm(S(G(e, "latest")))),
                    WorkMinutes = (int)N(G(e, "work_minutes")), Pay = (int)N(G(e, "pay")), Units = A(G(e, "units")).Select(x => (int)N(x)).ToArray(),
                    To = S(G(e, "to"), "none"), Memo = S(G(e, "memo")), Items = SA(G(e, "items")), Fatigue = (int)N(G(e, "fatigue")),
                    Stats = SA(G(e, "stats")), Mini = S(G(e, "mini")), Rep = (float)N(G(e, "rep")), Unlock = S(G(e, "unlock")), RainOff = B(G(e, "rain_off")),
                };
            }).ToArray();

            var bj = O(Parse(balance));
            var b = new BalanceDef();
            var st = O(G(bj, "start"));
            b.StartYear = (int)N(G(st, "year"), 2026); b.StartDate = Md(S(G(st, "date"), "03-03")); b.StartTime = Hm(S(G(st, "time"), "05:50"));
            b.StartWallet = (int)N(G(st, "wallet"), 2600); b.Seed = (int)N(G(st, "seed"), 20260303);
            var ck = O(G(bj, "clock"));
            b.ClockRate = (float)N(G(ck, "rate"), 3); b.Wake = Hm(S(G(ck, "wake"), "05:30")); b.WakeSunday = Hm(S(G(ck, "wake_sunday"), "08:00")); b.NightLimit = Hm(S(G(ck, "night_limit"), "02:00"));
            var bands = A(G(ck, "bands")).Select(A).ToList();
            b.BandNames = bands.Select(x => S(x[0])).ToArray();
            b.BandFrom = bands.Select(x => Hm(S(x[1]))).ToArray();
            b.Holidays = A(G(bj, "holidays")).Select(x => Md(S(x))).ToArray();
            var mi = O(G(bj, "minor"));
            b.MinorNightFrom = Hm(S(G(mi, "night_from"), "22:00")); b.MinorNightTo = Hm(S(G(mi, "night_to"), "06:00"));
            b.PerWorkDay = (int)N(G(mi, "per_work_day"), 420); b.WeekSoft = (int)N(G(mi, "week_soft"), 2100); b.OverworkMul = (float)N(G(mi, "overwork_mul"), 1.5);
            var fa = O(G(bj, "fatigue"));
            b.SleepPerHour = (int)N(G(fa, "sleep_per_hour"), 9); b.Tired = (int)N(G(fa, "tired"), 70); b.TiredMul = (float)N(G(fa, "tired_mul"), 0.6);
            b.GymBlock = (int)N(G(fa, "gym_block"), 85); b.Collapse = (int)N(G(fa, "collapse"), 95); b.SleptTiredAdd = (int)N(G(fa, "slept_tired_add"), 10);
            b.RestDay = (int)N(G(fa, "rest_day"), 20); b.Nap = (int)N(G(fa, "nap"), 9); b.DrillMinutes = (int)N(G(fa, "drill_minutes"), 30);
            b.StreetFatigue = (int)N(G(fa, "street"), 12); b.NightOut = (int)N(G(fa, "night_out"), 10);
            var mo = O(G(bj, "money"));
            b.Fixed = A(G(mo, "fixed")).Select(x => { var p = A(x); return new FixedCost { Name = S(p[0]), Amount = (int)N(p[1]) }; }).ToArray();
            b.AcademyMonth = (int)N(G(mo, "academy_month"), 9); b.Academy = (int)N(G(mo, "academy"), 350000);
            b.EnvelopeGoal = (int)N(G(mo, "envelope_goal"), 40000); b.EnvelopeSunday = (int)N(G(mo, "envelope_sunday"), 5000);
            b.Cake2 = (int)N(G(mo, "cake2"), 38000); b.Cake1 = (int)N(G(mo, "cake1"), 28000);
            var gy = O(G(bj, "gym"));
            b.GymFee = (int)N(G(gy, "fee"), 150000); b.CleanerFee = (int)N(G(gy, "cleaner_fee"), 50000); b.BusMinutes = (int)N(G(gy, "bus_minutes"), 15);
            b.TrialDrills = SA(G(gy, "trial_drills")); b.GymOpen = Hm(S(G(gy, "open"), "07:00")); b.GymClose = Hm(S(G(gy, "close"), "22:00"));
            var rp = O(G(bj, "rep"));
            b.RepTiers = A(G(rp, "tiers")).Select(x => { var p = A(x); return new RepTier { From = (int)N(p[0]), Name = S(p[1]) }; }).ToArray();
            b.StreetWin = (int)N(G(rp, "street_win"), 2); b.StreetWinMany = (int)N(G(rp, "street_win_many"), 3); b.StreetLoss = (int)N(G(rp, "street_loss"), -1);
            b.SideDone = (int)N(G(rp, "side_done"), 3); b.DeliveryGood = (float)N(G(rp, "delivery_good"), 0.3);
            b.Rumor = A(G(bj, "rumor")).Select(x => new RumorLines { Lines = SA(x) }).ToArray();
            var sk = O(G(bj, "skill"));
            b.DelegateShifts = (int)N(G(sk, "delegate_shifts"), 10); b.DelegateStat = (float)N(G(sk, "delegate_stat"), 0.75);
            var gc = FA(G(sk, "grades")); if (gc.Length == 4) b.GradeCuts = gc;
            var we = O(G(bj, "weather"));
            b.RainByMonth = new float[13];
            foreach (var kv in O(G(we, "rain"))) if (int.TryParse(kv.Key, out int m) && m >= 0 && m < 13) b.RainByMonth[m] = (float)N(kv.Value);
            b.RainOn = A(G(we, "rain_on")).Select(x => Md(S(x))).ToArray();
            b.ClearOn = A(G(we, "clear_on")).Select(x => Md(S(x))).ToArray();
            b.GameZones = SA(G(bj, "zones_game"));
            b.Points = A(G(bj, "points")).Select(x =>
            {
                var e = O(x); var p = A(G(e, "pos"));
                return new PointDef { Id = S(G(e, "id")), Zone = S(G(e, "zone")), Label = S(G(e, "label")), Radius = (float)N(G(e, "r"), 2.5), Pos = p.Count == 3 ? new Vector3((float)N(p[0]), (float)N(p[1]), (float)N(p[2])) : Vector3.zero };
            }).ToArray();
            def.Balance = b;

            var cards = new List<CardDef>();
            foreach (var t in events)
            {
                var ej = O(Parse(t));
                string chap = S(G(ej, "chapter"));
                foreach (var o in A(G(ej, "cards")))
                {
                    var e = O(o);
                    var c = new CardDef
                    {
                        Id = S(G(e, "id")), Group = S(G(e, "group")), Title = S(G(e, "title")), Chapter = S(G(e, "chapter"), chap),
                        Kind = Kind(S(G(e, "kind"))), Motif = SA(G(e, "motif")), Src = SA(G(e, "src")),
                        Scenes = SA(G(e, "scenes")), NoSceneEffects = B(G(e, "no_scene_effects")),
                        Board = SA(G(e, "board")), Quotes = SA(G(e, "quotes")),
                        Date = Md(S(G(e, "date"))), Monthly = (int)N(G(e, "monthly") is string ms ? double.Parse(ms, CultureInfo.InvariantCulture) : N(G(e, "monthly"))),
                        Days = S(G(e, "days")), Bands = SA(G(e, "bands")),
                        From = Hm(S(G(e, "from"))), To = Hm(S(G(e, "to"))),
                        Place = S(G(e, "place")), Zone = S(G(e, "zone")),
                        At = S(G(e, "at")).Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToArray(),
                        Need = SA(G(e, "need")), Block = SA(G(e, "block")), After = SA(G(e, "after")),
                        Fight = S(G(e, "fight")), Miss = S(G(e, "miss"), "wait"),
                        Weight = (float)N(G(e, "weight"), 1), Limit = (int)N(G(e, "limit")), Demo = B(G(e, "demo")),
                    };
                    var win = SA(G(e, "window"));
                    if (win.Length == 2) { c.WinFrom = Md(win[0]); c.WinTo = Md(win[1]); }
                    var no = G(e, "notice") as Dictionary<string, object>;
                    if (no != null) { c.NoticeAt = Hm(S(G(no, "at"))); c.NoticeFrom = S(G(no, "from")); c.NoticeText = S(G(no, "text")); }
                    c.Choice = A(G(e, "choice")).Select(x => { var d = O(x); return new CardChoice { Label = S(G(d, "label")), After = SA(G(d, "after")) }; }).ToArray();
                    c.Ifs = A(G(e, "ifs")).Select(x => { var d = O(x); return new CardVariant { Need = S(G(d, "need")), Title = S(G(d, "title")), Board = SA(G(d, "board")), Quotes = SA(G(d, "quotes")), After = SA(G(d, "after")) }; }).ToArray();
                    cards.Add(c);
                }
            }
            def.Cards = cards.ToArray();

            if (mapZones != null)
            {
                var mz = O(Parse(mapZones));
                def.MapZones = A(G(mz, "zones")).Select(x => S(G(O(x), "id"))).Where(x => x.Length > 0).ToArray();
            }
        }

        /// 돈 쓸 곳(shop.json + lesson_lines.tsv) → def.Shop(docs/13 14-6). tsv = 코치 id · 종류 · 대사(# 줄은 주석)
        public static void FillShop(WorldDef def, string shop, string lines)
        {
            var sh = new ShopDef();
            if (!string.IsNullOrEmpty(shop))
            {
                var j = O(Parse(shop));
                var ls = O(G(j, "lesson"));
                sh.Minutes = (int)N(G(ls, "minutes"), 40); sh.GraceBefore = (int)N(G(ls, "grace_before"), 15); sh.GraceAfter = (int)N(G(ls, "grace_after"), 20);
                sh.NoticeBefore = (int)N(G(ls, "notice_before"), 60); sh.NoshowRel = (int)N(G(ls, "noshow_rel"), -2); sh.LessonRel = (int)N(G(ls, "lesson_rel"), 1);
                sh.BlockRel = (int)N(G(ls, "block_rel"), -4); sh.BlockDays = (int)N(G(ls, "block_days"), 7);
                sh.AutoMul = (float)N(G(ls, "auto_mul"), 0.75); sh.CueEvery = (float)N(G(ls, "cue_every"), 7);
                sh.Coaches = A(G(j, "coaches")).Select(x =>
                {
                    var e = O(x);
                    var c = new CoachDef
                    {
                        Id = S(G(e, "id")), Name = S(G(e, "name")), Title = S(G(e, "title")), Gym = S(G(e, "gym")), Bio = S(G(e, "bio")), Unlock = S(G(e, "unlock")),
                        Visit = B(G(e, "visit")), Week = (int)N(G(e, "week"), 1), Price = (int)N(G(e, "price")), List = (int)N(G(e, "list")),
                        Ticket10 = (int)N(G(e, "ticket10")), List10 = (int)N(G(e, "list10")), Mul = (float)N(G(e, "mul"), 1.5), Bonus = (float)N(G(e, "bonus")),
                        Metric = (float)N(G(e, "metric")), FatigueMul = (float)N(G(e, "fatigue_mul"), 1.5), Fix = (float)N(G(e, "fix")), Drills = SA(G(e, "drills")),
                    };
                    c.Slots = A(G(e, "slots")).Select(s => { var p = A(s); return new LessonSlot { Days = S(p[0]), At = Hm(S(p[1])), Label = p.Count > 2 ? S(p[2]) : "" }; }).ToArray();
                    if (G(e, "tech") is Dictionary<string, object> te)
                    {
                        c.HasTech = true;
                        string g = S(G(te, "grade"), "B");
                        c.Tech = new TechDef
                        {
                            Id = S(G(te, "id")), Name = S(G(te, "name")), Text = S(G(te, "text")), Lessons = (int)N(G(te, "lessons"), 2), Drills = SA(G(te, "drills")), Plus = (float)N(G(te, "plus"), 4),
                            Grade = g == "S" ? 4 : g == "A" ? 3 : g == "B" ? 2 : g == "C" ? 1 : 0,
                        };
                    }
                    return c;
                }).ToArray();
                var fr = O(G(j, "food_rules"));
                sh.FullCap = (int)N(G(fr, "full_cap"), 12); sh.StreakMul = (float)N(G(fr, "streak_mul"), 0.85); sh.StreakMin = (float)N(G(fr, "streak_min"), 0.55);
                sh.SuppDays = (int)N(G(fr, "supp_days"), 3); sh.SuppMiss = (int)N(G(fr, "supp_miss"), 2);
                var sd = FA(G(fr, "same_day")); if (sd.Length > 0) sh.SameDay = sd;
                sh.Shops = A(G(j, "shops")).Select(x => { var p = A(x); return new ShopPlace { Id = S(p[0]), Name = S(p[1]), Place = S(p[2]) }; }).ToArray();
                sh.Foods = A(G(j, "foods")).Select(x =>
                {
                    var e = O(x);
                    return new FoodDef
                    {
                        Id = S(G(e, "id")), Name = S(G(e, "name")), Shop = S(G(e, "shop")), Supp = S(G(e, "supp")), Effect = S(G(e, "effect")),
                        Price = (int)N(G(e, "price")), List = (int)N(G(e, "list")), Minutes = (int)N(G(e, "minutes")), Full = (int)N(G(e, "full")), Doses = (int)N(G(e, "doses")),
                        Heal = (int)N(G(e, "heal")), Fatigue = (float)N(G(e, "fatigue")), Carry = B(G(e, "carry")), Free = B(G(e, "free")), Aka = SA(G(e, "aka")),
                        Buffs = A(G(e, "buffs")).Select(b => { var p = A(b); return new BuffSpec { Kind = S(p[0]), Minutes = (int)N(p[1]), Value = (float)N(p[2]) }; }).ToArray(),
                    };
                }).ToArray();
                var bn = A(G(j, "buff_names")).Select(x => A(x)).ToArray();
                sh.BuffKinds = bn.Select(p => S(p[0])).ToArray(); sh.BuffNames = bn.Select(p => S(p[1])).ToArray(); sh.BuffNotes = bn.Select(p => p.Count > 2 ? S(p[2]) : "").ToArray();
            }
            var ll = new List<LessonLine>();
            if (!string.IsNullOrEmpty(lines))
                foreach (var raw in lines.Replace("\r", "").Split('\n'))
                {
                    if (raw.Length == 0 || raw.StartsWith("#")) continue;
                    var p = raw.Split('\t');
                    if (p.Length >= 3) ll.Add(new LessonLine { Coach = p[0].Trim(), Kind = p[1].Trim(), Text = p[2].Trim() });
                }
            sh.Lines = ll.ToArray();
            def.Shop = sh;
        }

        /// 비교용 한 줄(W01 — 두 번 변환해 같은지)
        public static string Dump(WorldDef def) => JsonUtility.ToJson(new Wrap { Version = def.Version, Chapters = def.Chapters, Activities = def.Activities, Cards = def.Cards, Balance = def.Balance, MapZones = def.MapZones, Shop = def.Shop });

        [Serializable]
        sealed class Wrap
        {
            public int Version;
            public ChapterDef[] Chapters;
            public ActivityDef[] Activities;
            public CardDef[] Cards;
            public BalanceDef Balance;
            public string[] MapZones;
            public ShopDef Shop;
        }
    }
}
