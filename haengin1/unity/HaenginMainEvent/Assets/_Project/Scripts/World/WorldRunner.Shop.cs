// 행인1의 메인이벤트 — 돈 쓸 곳 장면(docs/13 14-6): 체육관 개인레슨 진행(카드 → 드릴 고르기 → 코치 큐 → 결과 카드) · 휴대폰 '레슨' 예약 · '가방' 먹기 ·
// 가게 차림표(편의점 계산대 · 국밥집 직원가 · Zone1 가게 앞 판매창 3곳) · HUD 효과 아이콘 · 길거리 싸움 체력 효과. 규칙은 WorldModel.Shop.cs.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Haengin
{
    public sealed partial class WorldRunner
    {
        public PickUi Picker;
        /// 봇이 레슨에서 고를 드릴(null = 코치 목록에서 날마다 돌아가며)
        public Func<CoachDef, string> BotLessonDrill;
        public int LessonsRun { get; private set; }
        public int MealsRun { get; private set; }
        public string LastLessonLine { get; private set; } = "";
        public string LastMealLine { get; private set; } = "";
        bool inLesson;
        (DrillDef d, DrillResult r, DrillReward w)? lastDrill;

        void ShopBegin()
        {
            DrillGrader.Assist = d => Model != null ? Model.DrillAssist(d) : (0, 0);
            if (Phone != null) { Phone.Act = PhoneAct; Phone.ActLabel = t => t == 7 ? "예약하기" : t == 8 ? "먹기" : ""; }
        }

        void ShopStop()
        {
            DrillGrader.Assist = null;
            if (Phone != null) { Phone.Act = null; Phone.ActLabel = null; }
            Picker?.Hide();
            if (Model != null) { Model.LessonCoach = null; Model.LessonBooking = null; }
            inLesson = false;
            if (GymManager.Instance != null) GymManager.Instance.CoachName = "";
        }

        // ───────────────────────── Evaluate 에서(자유 이동 중) — 노쇼 · 미리 알림 · 체육관에 있으면 레슨 시작
        bool LessonTick()
        {
            if (Model.RenewStanding(Model.Today) > 0) Trace.Add("고정 레슨 예약 " + Model.Today);
            foreach (var ns in Model.NoShows()) Trace.Add($"노쇼 {ns.Coach} {ns.Day}");
            var nx = Model.NextLesson();
            if (nx != null)
            {
                int left = Model.AbsOf(nx.Day, nx.At) - Model.AbsNow;
                string key = $"lnote.{nx.Day}.{nx.At}";
                if (left <= Model.Sh.NoticeBefore && left > 0 && Model.Count(key) < 1)
                {
                    Model.AddCount(key, 1);
                    var c = Model.Sh.Coach(nx.Coach);
                    Model.Phone(c != null ? c.Name : nx.Coach, $"{GameClock.Hm(nx.At)} 레슨 — {left}분 뒤. 혜화 MMA(마을버스 15분)");
                }
            }
            var bk = Model.LessonNow();
            if (bk != null && Loader != null && Loader.Place == SceneLoader.Gym && GymManager.Instance != null)
            {
                StartCoroutine(Run("레슨", Lesson(bk)));
                return true;
            }
            return false;
        }

        // ───────────────────────── 레슨 한 회
        IEnumerator Lesson(Booking bk)
        {
            var c = Model.Sh.Coach(bk.Coach);
            var gm = GymManager.Instance;
            if (c == null || gm == null) yield break;
            // 시작 시각까지 기다림(일찍 왔으면 시계를 그 시각으로)
            int t0 = Model.AbsOf(bk.Day, bk.At);
            if (Model.AbsNow < t0) GameClock.AdvanceTo(bk.At);
            Model.LessonCoach = c; Model.LessonBooking = bk;
            inLesson = true;
            GymNpc coachNpc = c.Id == "yunjun" ? gm.Yunjun : c.Id == "gojun" ? gm.Gojun : gm.Guest(c.Id);
            var mat = gm.Sock("Sock_NPC_Coach_MatEdge");
            if (coachNpc != null && mat != null) { coachNpc.gameObject.SetActive(true); coachNpc.Place(mat.position, mat.forward, GymNpc.Routine.Watch); }
            bool first = Model.Count("lesson." + c.Id) < 1;
            int seed = Model.DayIndex * 7 + bk.At;
            string say = Model.Sh.Line(c.Id, first ? "intro" : "start", seed);
            if (string.IsNullOrEmpty(say)) say = Model.Sh.Line(c.Id, "start", seed);
            var lines = new List<string> { $"{c.Title} · {GameClock.Hm(bk.At)} · {(bk.Ticket ? $"회권(남은 {Model.Tickets(c.Id)})" : Ledger.Won(bk.Paid) + "원")}" };
            if (c.Visit && !string.IsNullOrEmpty(c.Bio)) lines.Add(c.Bio);
            lines.Add($"이번 판: 스탯 ×{c.Mul:0.#} · 판정 +{c.Bonus:0}{(Model.CondBonus > 0 ? $" · 컨디션 +{Model.CondBonus:0.#}" : "")} · 피로 ×{c.FatigueMul:0.#}");
            yield return Board($"개인레슨   ·   {GameClock.DateText} {GameClock.TimeText}   ·   혜화 MMA", $"{c.Name}{(c.Visit ? $"({c.Gym})" : "")} — {(first ? "첫 레슨" : "레슨")}", "레슨", c.Visit, lines.ToArray(), new[] { $"{c.Name}|{say}" });
            // 드릴 고르기
            var opts = LessonDrills(c);
            string drill = null;
            if (opts.Count > 0)
            {
                if (BotSkill > 0f) drill = BotLessonDrill?.Invoke(c) ?? opts[Mathf.Abs(Model.DayIndex) % opts.Count].Id;
                else if (Picker != null)
                {
                    int pick = 0;
                    yield return Picker.Pick($"{c.Name} — 무엇을 할까", opts.Select(d => $"{d.NameKr}  ·  {GymSave.NameOf(d.MainStat)}{(d.SubStat != Stat.None ? "·" + GymSave.NameOf(d.SubStat) : "")}{(Model.Count("fix." + d.Id) > 0 ? "  · 교정 받음" : "")}").ToArray(), "↑ ↓ 고르기   Enter 시작", v => pick = v);
                    drill = opts[Mathf.Clamp(pick, 0, opts.Count - 1)].Id;
                }
                else drill = opts[0].Id;
            }
            lastDrill = null;
            if (drill != null)
            {
                gm.CoachName = c.Name;
                var cue = StartCoroutine(Cues(c, gm));
                if (BotSkill > 0f) yield return gm.PlayBot(drill, BotSkill);
                else if (gm.StartDrill(drill)) { while (gm.Busy) yield return null; }
                StopCoroutine(cue);
                gm.CoachName = "";
            }
            string sum = "";
            if (lastDrill.HasValue)
            {
                var (d, r, w) = lastDrill.Value;
                sum = Model.LessonDone(bk, d, r, w);
                GameClock.Advance(Mathf.Max(0, Model.Sh.Minutes - Def.Balance.DrillMinutes));
                string kind = r.Grade >= Grade.A ? "good" : r.Grade == Grade.B ? "ok" : "bad";
                var q = new List<string> { $"{c.Name}|{Model.Sh.Line(c.Id, kind, seed)}" };
                if (c.HasTech && Model.Unlocked(c.Tech.Id) && sum.Contains(c.Tech.Name)) q.Add($"{c.Name}|{Model.Sh.Line(c.Id, "tech", seed)}");
                if (c.Fix > 0f && Model.Count("fix." + d.Id) > 0)
                {
                    int mi = Mathf.RoundToInt(Model.Count("fixm." + d.Id));
                    string label = mi >= 0 && mi < d.MetricCount ? d.Metrics[mi].LabelKr : "자세";
                    q.Add($"{c.Name}|{Model.Sh.Line(c.Id, "fix", seed).Replace("{지표}", label)}");
                }
                if (c.Visit) { string tip = Model.Sh.Line(c.Id, "tip", seed); if (tip.Length > 0) q.Add($"{c.Name}|{tip}"); }
                var body = new List<string> { $"{d.NameKr} {DrillGrader.Letter(r.Grade)} · 점수 {r.Score:0.#}{(r.Assist > 0 ? $"(코치 큐 +{r.Assist:0.#})" : "")}", sum, $"신뢰 {Model.Rel(c.Id):+0;-0;0} · 피로 {Model.G.Fatigue}" };
                yield return Board($"개인레슨   ·   {GameClock.TimeText}", $"{c.Name} — 끝", "레슨", c.Visit, body.ToArray(), q.ToArray());
            }
            else
            {
                // 드릴을 못 함(피로·부상) — 이야기만 듣고 끝: 레슨은 받은 것으로(스탯 없음)
                bk.State = 1; Model.AddCount("lesson." + c.Id, 1); Model.AddCount("rel." + c.Id, 1);
                GameClock.Advance(Model.Sh.Minutes);
                sum = $"{c.Name} 레슨 — 오늘은 몸 상태 때문에 말로만";
                yield return Board($"개인레슨   ·   {GameClock.TimeText}", $"{c.Name} — 말로 하는 레슨", "레슨", c.Visit, new[] { sum }, new[] { $"{c.Name}|{Model.Sh.Line(c.Id, "bad", seed)}" });
            }
            LastLessonLine = sum;
            LessonsRun++;
            Trace.Add($"레슨 {c.Id} {bk.Drill} {WorldModel.Grade2(bk.Grade)}");
            Debug.Log($"[World] 레슨 {c.Name} {bk.Day / 100}/{bk.Day % 100} {GameClock.Hm(bk.At)} — {sum}");
            Model.LessonCoach = null; Model.LessonBooking = null;
            inLesson = false;
            if (coachNpc != null && c.Visit) coachNpc.gameObject.SetActive(false);
            gm.ApplyPart(gm.Part);
        }

        IEnumerator Board(string head, string title, string tag, bool accent, string[] lines, string[] quotes)
        {
            var board = Story != null ? Story.Board : null;
            if (board == null) yield break;
            board.ShowCard(head, title, tag, accent, lines, quotes, "× · Enter · 마우스 왼쪽   다음");
            yield return WaitPress();
            board.Hide();
        }

        /// 드릴 중 코치 큐(옆에서 한마디 — 레슨 판정 보정의 화면 쪽)
        IEnumerator Cues(CoachDef c, GymManager gm)
        {
            int n = 0;
            float every = Mathf.Max(2f, Model.Sh.CueEvery);
            // 미트(D04)는 원래 이윤준이 잡는다 — 다른 코치 레슨이면 그 코치가 잡음
            for (int k = 0; k < 600 && gm.Runner != null && gm.Runner.State == DrillRunner.Phase.Idle; k++) yield return null;
            var holder = c.Id == "gojun" ? gm.Gojun : c.Id == "yunjun" ? null : gm.Guest(c.Id);
            if (holder != null && gm.Current != null && gm.Current.Id == "D04" && gm.Yunjun != null && gm.Yunjun.gameObject.activeSelf)
            {
                var y = gm.Yunjun.transform;
                holder.gameObject.SetActive(true);
                holder.Place(y.position, y.forward, GymNpc.Routine.Mitts);
                gm.Yunjun.gameObject.SetActive(false);
            }
            yield return new WaitForSeconds(2.5f);
            while (true)
            {
                if (gm.Runner != null && gm.Runner.State == DrillRunner.Phase.Round)
                {
                    string line = Model.Sh.Line(c.Id, "cue", Model.DayIndex + n++);
                    if (line.Length > 0) gm.Hud.Say(c.Name, line);
                }
                yield return new WaitForSeconds(every);
            }
        }

        public List<DrillDef> LessonDrills(CoachDef c)
        {
            var r = new List<DrillDef>();
            if (Def.Drills == null) return r;
            var g = Model.G;
            IEnumerable<DrillDef> pool = c.AnyDrill ? Def.Drills.Drills.Where(d => g.Of(d.Id, false) != null && g.Of(d.Id, false).Plays > 0) : c.Drills.Select(id => Def.Drills.Find(id)).Where(d => d != null);
            foreach (var d in pool)
            {
                if (DrillStations.Create(d.Id) == null) continue;
                if (DrillSim.DirectBlocked(d, g, GymSave.Today).Length > 0) continue;
                r.Add(d);
            }
            if (r.Count == 0 && c.AnyDrill) { var d4 = Def.Drills.Find("D04"); if (d4 != null && DrillSim.DirectBlocked(d4, g, GymSave.Today).Length == 0) r.Add(d4); }
            return r;
        }

        void LessonDrillFinished(DrillDef d, DrillResult r, DrillReward w)
        {
            if (inLesson) lastDrill = (d, r, w);
            else Model.UseFix(d);
            // 스태미나 효과: 드릴이 든 피로 일부를 돌려줌(드릴 보상은 GymSave 에 바로 들어가서 AddFatigue 를 안 거침)
            float cut = Model.StaminaCut;
            if (cut > 0f && w.Fatigue > 0) Model.G.Fatigue = Mathf.Clamp(Model.G.Fatigue - Mathf.RoundToInt(w.Fatigue * cut), 0, 100);
        }

        // ───────────────────────── 휴대폰 '레슨'(7) · '가방'(8)
        bool PhoneAct(int tab)
        {
            if (tab == 7) { Phone.Close(); StartCoroutine(Run("레슨 예약", BookFlow())); return true; }
            if (tab == 8) { Phone.Close(); StartCoroutine(Run("가방", BagFlow())); return true; }
            return false;
        }

        string ShopPhoneText(int tab)
        {
            var sb = new StringBuilder();
            var sh = Model.Sh;
            if (tab == 7)
            {
                var up = Model.S.Lessons.Where(b => b.State == 0).OrderBy(b => Model.AbsOf(b.Day, b.At)).ToList();
                sb.Append("<b>예약</b>\n");
                if (up.Count == 0) sb.Append("  없음\n");
                foreach (var b in up) { var c = sh.Coach(b.Coach); sb.Append($"  {b.Day / 100}/{b.Day % 100}({GameClock.WeekdayOfKey(b.Day)}) {GameClock.Hm(b.At)}  {c?.Name}{(b.Ticket ? " · 회권" : "")}\n"); }
                if (Model.S.Standing.Count > 0) sb.Append($"<b>매주 고정</b> {string.Join(" · ", Model.S.Standing.Select(s => $"{WorldModel.WeekDays[s.Day]} {GameClock.Hm(s.At)} {sh.Coach(s.Coach)?.Name}"))}\n");
                sb.Append("<b>코치</b>\n");
                foreach (var c in sh.Coaches)
                {
                    if (!Model.CoachOpen(c)) continue;
                    string slots = string.Join(" · ", c.Slots.Select(s => $"{s.Days} {GameClock.Hm(s.At)}"));
                    sb.Append($"  <b>{c.Name}</b>{(c.Visit ? $"({c.Gym}, 출장)" : "")} {Ledger.Won(c.Price)}원{(c.List > c.Price ? $"(정가 {Ledger.Won(c.List)})" : "")}{(c.Ticket10 > 0 ? $" · 10회 {Ledger.Won(c.Ticket10)}" : "")} · 주 {c.Week}회\n");
                    sb.Append($"     {slots} · 신뢰 {Model.Rel(c.Id):+0;-0;0}{(Model.Tickets(c.Id) > 0 ? $" · 회권 {Model.Tickets(c.Id)}" : "")}{(Model.CoachBlocked(c) ? " · 예약 막힘" : "")}\n");
                }
                if (!sh.Coaches.Any(c => Model.CoachOpen(c))) sb.Append("  아직 없음 — 혜화 MMA 체험 뒤 관장 사무실\n");
                var techs = sh.Coaches.Where(c => c.HasTech && Model.Unlocked(c.Tech.Id)).Select(c => $"「{c.Tech.Name}」({c.Name})").ToArray();
                if (techs.Length > 0) sb.Append($"<b>익힌 기술</b> {string.Join(" · ", techs)}\n");
                sb.Append($"노쇼 {Model.Count("noshow"):0} · 받은 레슨 {Model.Count("lesson"):0}");
            }
            else if (tab == 8)
            {
                sb.Append($"<b>가방</b> · 오늘 배부름 {Model.S.Full}/{sh.FullCap}\n");
                int n = 0;
                foreach (var g in BagFoods()) { sb.Append($"  {g.f.Name} ×{g.n}  — {FoodNote(g.f)}\n"); n++; }
                foreach (var f in sh.Foods.Where(f => f.IsSupp && Model.Doses(f.Supp) > 0)) { sb.Append($"  {f.Name.Split('(')[0]} {Model.Doses(f.Supp)}일분 · {Model.SuppStreak(f.Supp)}일째{(Model.SuppOn(f.Supp) ? " (효과 중)" : "")} — {f.Effect}\n"); n++; }
                if (n == 0) sb.Append("  비어 있음 — 하루편의점·성곽약국\n");
                string bl = Model.BuffLine();
                sb.Append($"<b>지금 효과</b> {(bl.Length > 0 ? bl : "없음")}");
            }
            return sb.ToString();
        }

        List<(FoodDef f, int n)> BagFoods()
        {
            var r = new List<(FoodDef, int)>();
            foreach (var grp in Model.L.Items.GroupBy(x => x))
            {
                var f = Model.Sh.FoodByName(grp.Key);
                if (f != null && !f.IsSupp) r.Add((f, grp.Count()));
            }
            return r;
        }

        string FoodNote(FoodDef f)
        {
            var p = new List<string>();
            if (f.Fatigue > 0) p.Add($"피로 −{f.Fatigue:0}");
            foreach (var b in f.Buffs) p.Add($"{Model.Sh.BuffName(b.Kind)} +{b.Value:0.#}{(b.Kind == "cond" ? "" : "%")} {b.Minutes / 60f:0.#}h");
            if (f.Heal > 0) p.Add("부상 하루 빨리");
            if (f.IsSupp) p.Add(f.Effect);
            return string.Join(" · ", p);
        }

        IEnumerator BagFlow()
        {
            var items = BagFoods().Select(g => g.f).ToList();
            items.AddRange(Model.Sh.Foods.Where(f => f.IsSupp && Model.Doses(f.Supp) > 0));
            if (items.Count == 0) { Hud?.Toast("가방이 비었다"); yield break; }
            int pick = 0;
            if (BotSkill > 0f || Picker == null) pick = 0;
            else yield return Picker.Pick($"가방 — 오늘 배부름 {Model.S.Full}/{Model.Sh.FullCap}", items.Select(f => $"{f.Name}{(f.IsSupp ? $" {Model.Doses(f.Supp)}일분" : "")}  —  {FoodNote(f)}").ToArray(), null, v => pick = v);
            if (pick < 0) yield break;
            var line = Model.Eat(items[pick], !items[pick].IsSupp);
            LastMealLine = line;
            Hud?.Toast(line);
            Trace.Add("가방 " + items[pick].Id);
        }

        IEnumerator BookFlow()
        {
            var coaches = Model.Sh.Coaches.Where(c => Model.CoachOpen(c)).ToList();
            if (coaches.Count == 0) { Hud?.Toast("레슨 — 아직 없음(혜화 MMA 체험 뒤 관장 사무실)"); yield break; }
            int ci = 0;
            var rows = coaches.Select(c => $"{c.Name}{(c.Visit ? $"({c.Gym})" : "")} — {c.Title} · {Ledger.Won(c.Price)}원{(Model.Tickets(c.Id) > 0 ? $" · 회권 {Model.Tickets(c.Id)}" : "")}").ToList();
            if (Model.S.Standing.Count > 0) rows.Add($"고정 레슨 모두 풀기({Model.S.Standing.Count})");
            yield return Picker.Pick("레슨 — 누구에게", rows.ToArray(), null, v => ci = v);
            if (ci < 0) yield break;
            if (ci >= coaches.Count) { Model.ClearStanding(); yield break; }
            var co = coaches[ci];
            var slots = Model.LessonSlots(co, 8);
            if (slots.Count == 0) { Hud?.Toast($"{co.Name} — 이번 주 빈 시간이 없다(주 {co.Week}회)"); yield break; }
            int si = 0;
            yield return Picker.Pick($"{co.Name} — 언제", slots.Take(PickUi.Max).Select(s => $"{s.day / 100}/{s.day % 100}({GameClock.WeekdayOfKey(s.day)}) {GameClock.Hm(s.at)}  {s.label}").ToArray(), null, v => si = v);
            if (si < 0) yield break;
            var slot = slots[si];
            bool ticket = Model.Tickets(co.Id) > 0;
            if (!ticket && co.Ticket10 > 0)
            {
                int pi = 0;
                yield return Picker.Pick($"{co.Name} — 결제(지갑 {Ledger.Won(Model.L.Wallet)})", new[] { $"1회 {Ledger.Won(co.Price)}원", $"10회권 {Ledger.Won(co.Ticket10)}원 (회당 {Ledger.Won(co.Ticket10 / 10)})" }, null, v => pi = v);
                if (pi < 0) yield break;
                if (pi == 1) { string tw = Model.BuyTicket(co); if (tw.Length > 0) { Hud?.Toast(tw); yield break; } ticket = true; }
            }
            string why = Model.Book(co, slot.day, slot.at, ticket);
            if (why.Length > 0) { Hud?.Toast(why); yield break; }
            int fixPick = 0;
            yield return Picker.Pick($"{co.Name} — {GameClock.WeekdayOfKey(slot.day)} {GameClock.Hm(slot.at)}", new[] { "이번 한 번", $"매주 {GameClock.WeekdayOfKey(slot.day)} {GameClock.Hm(slot.at)} 고정(맡기기 중에도)" }, null, v => fixPick = v);
            if (fixPick == 1) Model.SetStanding(co, slot.day, slot.at);
        }

        /// 봇·시험: 다음 빈 칸 예약(돈·회권 규칙 그대로). 돌려주는 것 = 막힌 까닭
        public string BookNext(string coach, bool buyTicket = false)
        {
            var c = Model.Sh.Coach(coach);
            if (c == null || !Model.CoachOpen(c)) return "안 열림";
            var s = Model.LessonSlots(c, 8);
            if (s.Count == 0) return "빈 칸 없음";
            if (buyTicket && Model.Tickets(coach) == 0 && c.Ticket10 > 0) Model.BuyTicket(c);
            return Model.Book(c, s[0].day, s[0].at, Model.Tickets(coach) > 0);
        }

        // ───────────────────────── 가게
        IEnumerator ShopFlow(string shopId)
        {
            var sp = Model.Sh.Shop(shopId);
            var foods = Model.Sh.Foods.Where(f => f.Shop == shopId && !f.Free).ToList();
            if (sp == null || foods.Count == 0) yield break;
            int pick = 0;
            if (BotSkill > 0f || Picker == null) pick = botShopPick != null ? botShopPick(foods) : 0;
            else yield return Picker.Pick($"{sp.Name} — 지갑 {Ledger.Won(Model.L.Wallet)} · 배부름 {Model.S.Full}/{Model.Sh.FullCap}", foods.Select(f => $"{f.Name} {Ledger.Won(f.Price)}원{(f.List > f.Price ? $"(정가 {Ledger.Won(f.List)})" : "")}  —  {FoodNote(f)}").ToArray(), null, v => pick = v);
            if (pick < 0 || pick >= foods.Count) yield break;
            var fd = foods[pick];
            if (fd.Carry || fd.IsSupp)
            {
                string why = Model.Buy(fd);
                if (why.Length > 0) { Hud?.Toast($"{fd.Name} — {why}"); yield break; }
                if (fd.IsSupp) { LastMealLine = Model.Eat(fd, true); Hud?.Toast(LastMealLine); yield break; }
                int now = 0;
                if (BotSkill <= 0f && Picker != null) yield return Picker.Pick(fd.Name, new[] { "바로 먹기", "가방에 넣기" }, null, v => now = v);
                if (now == 0) { LastMealLine = Model.Eat(fd, true); Hud?.Toast(LastMealLine); MealsRun++; }
                yield break;
            }
            // 식당: 앉아서 먹음(시간이 감) — 식사 카드
            string w2 = Model.CanEat(fd, false);
            if (w2.Length > 0) { Hud?.Toast($"{fd.Name} — {w2}"); yield break; }
            if (Model.L.Wallet < fd.Price) { Hud?.Toast($"{fd.Name} — 돈이 모자란다({Ledger.Won(fd.Price)}원)"); yield break; }
            string line = Model.Eat(fd, false);
            GameClock.Advance(fd.Minutes);
            LastMealLine = line;
            MealsRun++;
            Trace.Add("식사 " + fd.Id);
            Debug.Log($"[World] 식사 {sp.Name} — {line}");
            yield return Board($"{sp.Name}   ·   {GameClock.DateText} {GameClock.TimeText}", fd.Name, "식사", false, new[] { MealText(fd), line }, new string[0]);
        }

        /// 봇: 가게에서 고를 것(null = 0번)
        Func<List<FoodDef>, int> botShopPick;
        public IEnumerator BotShop(string shopId, string foodId)
        {
            botShopPick = list => Mathf.Max(0, list.FindIndex(f => f.Id == foodId));
            yield return Run("가게", ShopFlow(shopId));
            botShopPick = null;
        }

        static string MealText(FoodDef f) => f.Id switch
        {
            "samgyetang" => "뚝배기 안에서 국물이 아직 끓는다. 인삼 한 뿌리, 대추 두 알. 땀이 이마에서 턱으로.",
            "eel" => "숯불 위에서 장어가 몸을 뒤집는다. 사장님이 \"학생이 무슨 장어야\" 하면서도 한 점 더 얹는다.",
            "suyuk" => "엄마가 접시를 내려놓고 말없이 새우젓을 밀어 준다. \"직원가야. 남기지 마.\"",
            "gukbap_special" => "특 자는 고기가 두 배라는 뜻이다. 엄마가 계산대에서 고개를 젓는다 — 직원가.",
            _ => "다 먹었다.",
        };

        /// 표시 자리 shop_* (편의점 계산대 · 국밥집 · Zone1 판매창)
        bool ShopMarker(string func, out string prompt)
        {
            prompt = "";
            if (!func.StartsWith("shop_")) return false;
            var sp = Model?.Sh.Shop(func.Substring(5));
            prompt = sp != null ? $"{sp.Name} — 차림표" : "가게";
            return true;
        }

        void HudBuffs()
        {
            if (Hud == null || Model == null) return;
            var list = new List<(string, string, Color)>();
            var cols = new Dictionary<string, Color> { { "stamina", new Color(0.36f, 0.55f, 0.45f) }, { "hp", new Color(0.62f, 0.25f, 0.22f) }, { "cond", new Color(0.30f, 0.42f, 0.62f) } };
            foreach (var k in Model.Sh.BuffKinds)
            {
                float v = Model.BuffValue(k); if (v <= 0f) continue;
                int m = Model.BuffLeft(k);
                list.Add((Model.Sh.BuffName(k).Substring(0, 1), $"+{v:0.#}{(k == "cond" ? "" : "%")} {m / 60}:{m % 60:00}", cols.TryGetValue(k, out var cc) ? cc : UiKit.Grey));
            }
            foreach (var f in Model.Sh.Foods.Where(f => f.IsSupp && Model.SuppStreak(f.Supp) > 0))
                list.Add((f.Name.Substring(0, 1), $"{Model.SuppStreak(f.Supp)}일{(Model.SuppOn(f.Supp) ? " 효과" : "")}", Model.SuppOn(f.Supp) ? new Color(0.55f, 0.48f, 0.25f) : UiKit.Grey));
            var nx = Model.NextLesson();
            if (nx != null && nx.Day == Model.Today) { var c = Model.Sh.Coach(nx.Coach); list.Add(("레", $"{GameClock.Hm(nx.At)} {c?.Name}", UiKit.Accent)); }
            Hud.SetBuffs(list);
        }
    }
}
