// 행인1의 메인이벤트 — 드릴 등급 산식 · 보상 · 시뮬 규칙(docs/12_체육관_설계.md 4-3 · 4-4 · 4-5) — 순수 C#, 난수 없음(G01~G06·G13·G14)
using System;
using UnityEngine;

namespace Haengin
{
    public static class DrillGrader
    {
        /// 4-3: m_i → score = round(100 Σ w m, 1) → S(≥90 and min m ≥ 0.75) / A ≥ 75 / B ≥ 60 / C ≥ 45 / D
        public static DrillResult Evaluate(DrillDef def, double[] values)
        {
            int n = def.MetricCount;
            var r = new DrillResult { Def = def, Values = new double[n], Scores = new double[n] };
            double sum = 0, wsum = 0, min = 1;
            for (int i = 0; i < n; i++)
            {
                double v = i < values.Length ? values[i] : def.Metrics[i].HigherIsBetter ? def.Metrics[i].D : def.Metrics[i].D;
                r.Values[i] = v;
                double m = def.Metrics[i].Score(v);
                r.Scores[i] = m;
                sum += def.Metrics[i].Weight * m;
                wsum += def.Metrics[i].Weight;
                min = Math.Min(min, m);
            }
            if (wsum > 1e-9 && Math.Abs(wsum - 1.0) > 1e-6) sum /= wsum;     // 가중치 합이 1이 아니면 정규화(데이터 실수 방어)
            r.Score = Math.Round(100.0 * sum, 1, MidpointRounding.AwayFromZero);
            r.Grade = GradeOf(r.Score, n > 0 ? min : 0);
            return r;
        }

        public static Grade GradeOf(double score, double minMetric)
        {
            if (score >= 90.0 && minMetric >= 0.75) return Grade.S;
            if (score >= 75.0) return Grade.A;
            if (score >= 60.0) return Grade.B;
            if (score >= 45.0) return Grade.C;
            return Grade.D;
        }

        /// 판정 보정(오픈월드 — 레슨 코치 큐·컨디션·익힌 기술·교정, docs/13 14-6): (점수 +, 최저 지표 +). 연습장·체육관 단독에선 null
        public static Func<DrillDef, (double score, double metric)> Assist;

        public static DrillResult Evaluate(DrillDef def, DrillLog log)
        {
            var r = Evaluate(def, DrillMetrics.Compute(def, log));
            r.Aborted = log.Aborted;
            r.Concussion = log.Concussion;
            if (Assist != null && !log.Aborted)
            {
                var (s, m) = Assist(def);
                if (s != 0 || m != 0)
                {
                    r.Assist = s;
                    r.Score = Math.Min(100.0, Math.Round(r.Score + s, 1, MidpointRounding.AwayFromZero));
                    r.Grade = GradeOf(r.Score, (def.MetricCount > 0 ? r.MinScore : 0) + m);
                }
            }
            return r;
        }

        public static string Letter(Grade g) => g switch { Grade.S => "S", Grade.A => "A", Grade.B => "B", Grade.C => "C", _ => "D" };
    }

    /// 4-4 등급별 배율표
    public static class DrillRewards
    {
        public static float SweatMul(Grade g) => g switch { Grade.S => 1.8f, Grade.A => 1.35f, Grade.B => 1.0f, Grade.C => 0.7f, _ => 0.4f };
        public static int MainDelta(Grade g) => g switch { Grade.S => 4, Grade.A => 3, Grade.B => 2, Grade.C => 1, _ => 0 };
        public static int SubDelta(Grade g) => g switch { Grade.S => 2, Grade.A => 1, Grade.B => 1, _ => 0 };
        public static int Relation(Grade g) => g switch { Grade.S => 2, Grade.A => 1, _ => 0 };
        public const float SimSweatMul = 1.35f;
        public const int SimMain = 3, SimSub = 1;
        public const int FirstSBonus = 50;
        /// 피로 70 이상이면 땀·스탯 ×0.6
        public const int TiredFrom = 70;
        public const float TiredMul = 0.6f;
        /// 피로 85 이상이면 이윤준이 막음(직접), 60 이상이면 시뮬 불가
        public const int BlockFrom = 85;

        /// 4-4: 보상 계산(상태는 바꾸지 않음). firstS = 이 드릴 첫 S 인가. aborted = 중단(보상 0 · 피로 절반)
        public static DrillReward Compute(DrillDef def, Grade grade, bool isSim, GymSave save, bool firstS, bool aborted = false)
        {
            var r = new DrillReward { Efficiency = 1f };
            if (aborted) { r.Fatigue = Mathf.CeilToInt(def.FatigueCost * 0.5f); return r; }
            float eff = save != null && save.Fatigue >= TiredFrom ? TiredMul : 1f;
            r.Efficiency = eff;
            float sweat = def.SweatBase * (isSim ? SimSweatMul : SweatMul(grade));
            int main = isSim ? SimMain : MainDelta(grade), sub = isSim ? SimSub : SubDelta(grade);
            r.Sweat = Mathf.RoundToInt(sweat * eff);
            r.MainDelta = Mathf.RoundToInt(main * eff);
            r.SubDelta = Mathf.RoundToInt(sub * eff);
            if (def.SubStat == Stat.None) r.SubDelta = 0;
            // 스탯 상한 −10 부터 ×0.5(4-1)
            if (save != null)
            {
                if (save.Stat(def.MainStat) >= GymSave.StatSoftFrom) r.MainDelta = Mathf.FloorToInt(r.MainDelta * 0.5f);
                if (def.SubStat != Stat.None && save.Stat(def.SubStat) >= GymSave.StatSoftFrom) r.SubDelta = Mathf.FloorToInt(r.SubDelta * 0.5f);
            }
            r.Relation = isSim ? 0 : Relation(grade);
            r.Fatigue = def.FatigueCost;
            if (!isSim && grade == Grade.S && firstS) { r.Sweat += FirstSBonus; r.FirstS = true; }
            return r;
        }

        /// 보상을 저장에 적용(드릴 기록도 갱신). 돌려주는 것 = 적용한 보상
        public static DrillReward Apply(DrillDef def, DrillResult res, bool isSim, GymSave save)
        {
            var ds = save.Of(def.Id);
            bool firstS = !isSim && res.Grade == Grade.S && ds.SCount == 0;
            var r = Compute(def, res.Grade, isSim, save, firstS, res.Aborted);
            save.Sweat += r.Sweat;
            save.AddStat(def.MainStat, r.MainDelta);
            save.AddStat(def.SubStat, r.SubDelta);
            save.Fatigue = Mathf.Clamp(save.Fatigue + r.Fatigue, 0, 100);
            save.CoachRelation += r.Relation;
            if (!res.Aborted)
            {
                if (isSim) { ds.SimRuns++; ds.SimRunsSinceCert++; }
                else
                {
                    ds.Plays++;
                    ds.Last = (int)res.Grade;
                    ds.Best = Mathf.Max(ds.Best, (int)res.Grade);
                    if (res.Grade == Grade.S) { ds.SCount++; if (def.SimAllowed) ds.SimUnlocked = true; ds.SimRunsSinceCert = 0; }
                    else if (ds.CertDue)
                    {
                        // 검정(4-5): 8회 뒤 직접 1회 — B 이상이면 유지, 미만이면 잠김(다시 S)
                        if (res.Grade >= Grade.B) ds.SimRunsSinceCert = 0;
                        else ds.SimUnlocked = false;
                    }
                }
            }
            return r;
        }
    }

    /// 4-5 시뮬(자동 진행) 규칙
    public static class DrillSim
    {
        public const int SimPerDay = 2, CertEvery = 8, FatigueMax = 60;
        public const float RoutineReward = 0.5f, RoutineFatigue = 0.8f;
        public const int RoutineMax = 3;

        /// 시뮬 가능? 아니면 이유 글(빈칸 = 가능)
        public static string Blocked(DrillDef def, GymSave save, int today)
        {
            var ds = save.Of(def.Id, false);
            if (!def.SimAllowed) return def.Id == "D11" ? "스파링은 코치가 봐야 한다(시뮬 불가)" : "시뮬 불가";
            if (def.StoryOnly) return "이야기 드릴(시뮬 불가)";
            if (ds == null || !ds.SimUnlocked) return "S 를 한 번 찍어야 열린다";
            if (ds.CertDue) return "검정: 직접 1회(B 이상) 뒤 다시";
            if (save.SimLeft(today) <= 0) return $"오늘 시뮬 {SimPerDay}회 다 썼다";
            if (save.Fatigue >= FatigueMax) return $"피로 {save.Fatigue} — 피곤할 땐 코치가 봐야 한다({FatigueMax} 미만)";
            if (def.HeadRisk && save.Injured(today)) return "머리 부상 — 내일까지 쉰다";
            return "";
        }

        public static bool CanSim(DrillDef def, GymSave save, int today) => Blocked(def, save, today).Length == 0;

        /// 직접 하기 가능? (피로 85 이상 · 머리 부상 잠김)
        public static string DirectBlocked(DrillDef def, GymSave save, int today)
        {
            if (def.HeadRisk && save.Injured(today)) return "머리 부상 — 내일까지 D09·D11 잠김";
            if (save.Fatigue >= DrillRewards.BlockFrom) return "오늘은 끝. 자라.";
            if (!def.Playable) return "이번 판에서는 자리만(2부)";
            return "";
        }

        /// 시뮬 1회 실행: 하루 수 세기 + 보상(S 의 75%) — 결과는 등급 없음('시뮬')
        public static DrillReward Run(DrillDef def, GymSave save, int today)
        {
            if (save.SimDay != today) { save.SimDay = today; save.SimToday = 0; }
            save.SimToday++;
            var res = new DrillResult { Def = def, Grade = Grade.S };
            return DrillRewards.Apply(def, res, true, save);
        }

        /// 루틴(2부, 4-5): 시뮬 가능한 드릴 ≤ 3개를 한 슬롯에 — 보상 각 50%, 피로 합의 80%, 하루 1회. 돌려주는 것 = 합친 보상(실패면 null + 이유)
        public static DrillReward? Routine(DrillDef[] defs, GymSave save, int today, out string why)
        {
            why = "";
            if (defs == null || defs.Length == 0 || defs.Length > RoutineMax) { why = $"루틴은 1~{RoutineMax}개"; return null; }
            if (save.RoutineDay == today && save.RoutineToday >= 1) { why = "오늘 루틴은 끝"; return null; }
            foreach (var d in defs) { var b = Blocked(d, save, today); if (b.Length > 0) { why = $"{d.NameKr}: {b}"; return null; } }
            var total = new DrillReward { Efficiency = 1f };
            int fatigue = 0;
            foreach (var d in defs)
            {
                var r = DrillRewards.Compute(d, Grade.S, true, save, false);
                int sweat = Mathf.RoundToInt(r.Sweat * RoutineReward), main = Mathf.RoundToInt(r.MainDelta * RoutineReward), sub = Mathf.RoundToInt(r.SubDelta * RoutineReward);
                save.Sweat += sweat; save.AddStat(d.MainStat, main); save.AddStat(d.SubStat, sub);
                total.Sweat += sweat; total.MainDelta += main; total.SubDelta += sub;
                fatigue += d.FatigueCost;
                var ds = save.Of(d.Id); ds.SimRuns++; ds.SimRunsSinceCert++;
            }
            total.Fatigue = Mathf.RoundToInt(fatigue * RoutineFatigue);
            save.Fatigue = Mathf.Clamp(save.Fatigue + total.Fatigue, 0, 100);
            if (save.RoutineDay != today) { save.RoutineDay = today; save.RoutineToday = 0; }
            save.RoutineToday++;
            return total;
        }

        /// 뇌진탕 의심(D11, 4-2): 그 시간대 훈련 종료 · 피로 +20 · 다음 날까지 D09·D11 잠김
        public static void Concussion(GymSave save, int today)
        {
            save.Fatigue = Mathf.Clamp(save.Fatigue + 20, 0, 100);
            save.InjuryKind = "머리(뇌진탕 의심)";
            save.InjuryUntil = NextDay(today);
        }

        public static int NextDay(int key)
        {
            int m = key / 100, d = key % 100;
            try { var dt = new DateTime(GameClock.Year, m, d).AddDays(1); return GymSave.DayKey(dt.Month, dt.Day); }
            catch (ArgumentOutOfRangeException) { return key + 1; }
        }
    }
}
