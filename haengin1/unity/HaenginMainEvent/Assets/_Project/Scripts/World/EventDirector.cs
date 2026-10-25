// 행인1의 메인이벤트 — 이벤트 감독(docs/13 5-3): 장소·시간·조건이 맞는 카드 하나를 고른다(순수 C# — W06·W07).
// ① 평가 시점(트리거): start · time · wake · sleep · monthend · chain · enter:<무대> · point:<지점> · during:<활동> · after:<활동> · activity:<활동>
// ② 우선순위 핵심·시스템 > 계절 > 조건·문자 > 사연 > 반복 — 같은 자리에 둘이면 높은 것만(낮은 것은 다음 평가)
// ③ 하루 상한: 창(window) 핵심 1 · 사연 2 · 반복은 카드마다 limit. 날짜 박힌 핵심(never)은 상한을 넘는다(놓칠 수 없음)
// ④ 결정성: 반복 카드 확률 = hash(저장 시드, 날짜, 트리거, 카드 id) — 난수 상태를 저장하지 않는다
// ⑤ 막기: 진행 중인 핵심 카드가 block 을 걸면 그동안 사연·시비 꺼짐
// ⑥ 놓침: 창이 지나간 never 카드는 다음 평가에서 따라잡기(Overdue), summary 카드는 결과만(요약), lost 는 놓친 사연
using System;
using System.Collections.Generic;

namespace Haengin
{
    public sealed class EventDirector
    {
        public readonly WorldModel M;
        /// 지금 돌고 있는 카드(막기 판단)
        public CardDef Running;
        /// 고른 기록(W07 결정성 비교) "날짜 시각 트리거 → 카드"
        public readonly List<string> Trace = new List<string>();

        public EventDirector(WorldModel m) { M = m; }

        WorldDef D => M.Def;

        bool Blocked(string what) => Running != null && Array.IndexOf(Running.Block, what) >= 0;
        public bool StreetBlocked => Blocked("street");

        /// 그 카드가 그 트리거에서 지금 뜰 수 있나(이유 — 빈칸이면 됨)
        public string Why(CardDef c, string trig, int day = -1, int now = -1)
        {
            if (day < 0) day = M.Today;
            if (now < 0) now = M.Now;
            if (c == null) return "없음";
            if (M.DemoMode && !c.Demo) return "데모 밖";
            bool repeat = c.Kind == CardKind.Repeat;
            var st = M.S.Card0(c.Id, false);
            if (!repeat && c.Monthly == 0 && (M.CardDone(c.Id) || (c.Group.Length > 0 && M.CardDone(c.Group)))) return "끝남";
            if (c.Monthly > 0 && st != null && st.State > 0 && st.Day / 100 == day / 100) return "이번 달 끝남";
            if (repeat && st != null && st.Day == day && c.Limit > 0 && st.Times >= c.Limit) return "오늘 상한";
            if (!AtMatch(c, trig, now)) return "트리거 아님";
            if (c.Date > 0 && day != c.Date) return "날짜 아님";
            if (c.WinFrom > 0 && (day < c.WinFrom || day > c.WinTo)) return "기간 밖";
            if (c.Monthly > 0 && day % 100 != c.Monthly) return "날짜 아님";
            if (c.Days.Length > 0 && !c.Days.Contains(GameClock.WeekdayOfKey(day))) return "요일 아님";
            bool loose = trig == "sleep" || trig == "chain" || trig == "monthend" || trig == "start";
            if (!loose)
            {
                if (c.From >= 0 && now < c.From && !trig.StartsWith("during:")) return "아직 이름";
                if (c.To >= 0 && now > c.To && trig != "time") return "지남";
                if (c.Bands.Length > 0 && Array.IndexOf(c.Bands, M.Band(now)) < 0) return "시간대 아님";
            }
            if (!M.CheckAll(c.Need)) return "조건";
            if (c.Kind == CardKind.Side && Blocked("side")) return "막힘";
            // 하루 상한
            if (c.Kind == CardKind.Core && c.Date == 0 && c.WinFrom > 0 && trig != "chain" && M.S.CoreToday >= 1 && M.S.WorkDay == day) return "오늘 핵심 상한";
            if (c.Kind == CardKind.Side && M.S.SideToday >= 2 && M.S.WorkDay == day) return "오늘 사연 상한";
            if (repeat && WorldModel.Hash01(M.S.Seed, day, trig + "|" + c.Id) >= c.Weight) return "확률";
            return "";
        }

        /// "time" 은 그 카드의 시각에(다른 트리거가 있는 카드면 To 에 — 그때까지 안 왔으면 데려감)
        static bool AtMatch(CardDef c, string trig, int now)
        {
            if (c.AtHas(trig)) return trig != "time" || TimeDue(c, now);
            return false;
        }

        public static bool TimeDue(CardDef c, int now)
        {
            if (!c.AtHas("time")) return false;
            bool fallback = c.At.Length > 1 && c.To >= 0;
            int at = fallback ? c.To : Math.Max(0, c.From);
            return now >= at;
        }

        /// 트리거 하나에서 고른다(없으면 null). place = 지금 무대 키(enter 는 트리거에 이미 들어 있음)
        public CardDef Pick(string trig, int day = -1, int now = -1)
        {
            M.BeginDayIfNeeded();
            CardDef best = null;
            int bi = int.MaxValue;
            for (int i = 0; i < D.Cards.Length; i++)
            {
                var c = D.Cards[i];
                if (Why(c, trig, day, now).Length > 0) continue;
                int key = c.Priority * 10000 + i;
                if (key < bi) { bi = key; best = c; }
            }
            if (best != null) Trace.Add($"{GameClock.DateText} {GameClock.TimeText} {trig} → {best.Id}");
            return best;
        }

        /// 활동 중 카드(during:<활동>): 활동 시간 [s, e) 안에 From 이 있는 것 중 가장 이른 것
        public CardDef PickDuring(string act, int s, int e, int day = -1)
        {
            M.BeginDayIfNeeded();
            string trig = "during:" + act;
            CardDef best = null;
            foreach (var c in D.Cards)
            {
                if (!c.AtHas(trig) || c.From < 0 || c.From < s || c.From >= e) continue;
                if (Why(c, trig, day, c.From).Length > 0) continue;
                if (best == null || c.From < best.From || (c.From == best.From && c.Priority < best.Priority)) best = c;
            }
            if (best != null) Trace.Add($"{GameClock.DateText} {GameClock.Hm(best.From)} {trig} → {best.Id}");
            return best;
        }

        /// 카드가 끝났을 때(state 1 완료 · 2 요약 · 3 놓침) — 하루 셈
        public void Done(CardDef c, int state)
        {
            M.BeginDayIfNeeded();
            M.MarkCard(c, state);
            if (c.Kind == CardKind.Core && c.Date == 0 && c.WinFrom > 0) M.S.CoreToday++;
            if (c.Kind == CardKind.Side) M.S.SideToday++;
        }

        /// 창이 지나갔는데 안 끝난 카드: never = 따라잡기 대상, summary = 요약, lost = 놓침
        public List<CardDef> Overdue(int day = -1)
        {
            if (day < 0) day = M.Today;
            var r = new List<CardDef>();
            foreach (var c in D.Cards)
            {
                if (M.DemoMode && !c.Demo) continue;
                if (c.Kind == CardKind.Repeat || c.Monthly > 0) continue;
                if (M.CardDone(c.Id) || (c.Group.Length > 0 && M.CardDone(c.Group))) continue;
                int end = c.Date > 0 ? c.Date : c.WinTo;
                if (end <= 0 || day <= end) continue;
                if (c.AtHas("chain")) continue;
                if (!M.CheckAll(c.Need)) continue;
                r.Add(c);
            }
            return r;
        }

        /// 오늘 뜰 수 있는 핵심·계절 카드(휴대폰 지도 — 사연은 발견 뒤)
        public List<CardDef> Today(int day = -1)
        {
            if (day < 0) day = M.Today;
            var r = new List<CardDef>();
            foreach (var c in D.Cards)
            {
                if (M.DemoMode && !c.Demo) continue;
                if (c.Kind != CardKind.Core && c.Kind != CardKind.Season && c.Kind != CardKind.System) continue;
                if (M.CardDone(c.Id) || (c.Group.Length > 0 && M.CardDone(c.Group))) continue;
                if (c.Date > 0 && c.Date != day) continue;
                if (c.WinFrom > 0 && (day < c.WinFrom || day > c.WinTo)) continue;
                if (c.Date == 0 && c.WinFrom == 0) continue;
                if (c.AtHas("chain")) continue;
                if (c.Days.Length > 0 && !c.Days.Contains(GameClock.WeekdayOfKey(day))) continue;
                if (!M.CheckAll(c.Need)) continue;
                r.Add(c);
            }
            return r;
        }

        /// 그 날이 '이벤트 날'인가(주간 계획 맡기기가 멈추는 날): 날짜 박힌 핵심·시스템 카드
        public CardDef EventDay(int day)
        {
            foreach (var c in D.Cards)
            {
                if (M.DemoMode && !c.Demo) continue;
                if (c.Kind != CardKind.Core && c.Kind != CardKind.System) continue;
                if (c.Date != day || c.AtHas("chain")) continue;
                if (M.CardDone(c.Id) || (c.Group.Length > 0 && M.CardDone(c.Group))) continue;
                return c;
            }
            return null;
        }

        /// 지금 미리 보낼 문자(notice.at 이 지났고 오늘 카드)
        public List<CardDef> Notices(int day = -1, int now = -1)
        {
            if (day < 0) day = M.Today;
            if (now < 0) now = M.Now;
            var r = new List<CardDef>();
            foreach (var c in D.Cards)
            {
                if (c.NoticeAt < 0 || (M.DemoMode && !c.Demo)) continue;
                if (c.Date != day || now < c.NoticeAt) continue;
                if (M.CardDone(c.Id)) continue;
                if (M.Count("notice." + c.Id) > 0) continue;
                r.Add(c);
            }
            return r;
        }

        /// 카드 본문(ifs 변형 — 원고 문장이 기본, 조건 맞는 첫 변형)
        public CardVariant Variant(CardDef c)
        {
            if (c.Ifs == null) return null;
            foreach (var v in c.Ifs) if (M.Check(v.Need)) return v;
            return null;
        }

        public string[] BoardOf(CardDef c) { var v = Variant(c); return v != null && v.Board.Length > 0 ? v.Board : c.Board; }
        public string[] QuotesOf(CardDef c) { var v = Variant(c); return v != null && v.Quotes.Length > 0 ? v.Quotes : c.Quotes; }
        public string TitleOf(CardDef c) { var v = Variant(c); return v != null && !string.IsNullOrEmpty(v.Title) ? v.Title : c.Title; }

        /// 결과 적용(변형 after → 카드 after). 선택지는 부르는 쪽이 고른 뒤 그 after 를 넘긴다
        public void ApplyAfter(CardDef c, CardChoice choice = null)
        {
            var v = Variant(c);
            if (v != null) M.ApplyAll(v.After);
            if (choice != null) M.ApplyAll(choice.After);
            M.ApplyAll(c.After);
        }
    }
}
