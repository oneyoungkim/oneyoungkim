// 행인1의 메인이벤트 — 드릴 로그 → 지표 값(docs/12_체육관_설계.md 4-2 지표 정의) — 순수 C#, 결정론
// 샘플 종류(Kind):
//   공통  "beat_err"(ms, 부호 있음 — 박자 입력 오차) · "trip"(걸림) · "input"
//   D02   "du"(값 1 성공 / 0 실패 — 더블언더 박자)
//   D03/04 "combo"(1 맞음 / 0 틀림, 토큰마다) · "react"(ms) · "guard"(1/0 콤비 끝 가드 복귀) · "gap"(ms 콤비 안 타격 간격) · "timing"(ms, 미트 들림 ↔ 판정 시작 |오차|)
//   D05   "rhythm"(ms) · "power"(값 = 파워, 라운드) · "guard"(1/0) · "hit"(라운드)
//   D06   "sb"(값 1 맞음 / 0 실수) · "beat_err"
//   D07   "react"(ms) · "posture"(1/0) · "recover"(ms) · "fake_fail"(페이크에 반응 = 자세 실패로 셈)
//   D08   "lc_err"(ms) · "order"(1/0) · "finish"(1/0)
//   D10   "shrimp_err"(ms) · "escape"(1/0) · "bridge_err"(ms)
//   D11   "swing"(내 공격 1회, 값 1 명중 / 0 빗나감) · "incoming"(상대 판정 1회, 값 1 피함 / 0 맞음) · "read"(읽었다·가드) · "violation"(강타) · "position"(값 ±1)
//   어느 드릴이든 "metric:<id>" 샘플이 있으면 그 값을 그대로(마지막 값) — 자리 드릴·로드워크 기록·시험
using System;
using System.Collections.Generic;

namespace Haengin
{
    public static class DrillMetrics
    {
        public static double[] Compute(DrillDef def, DrillLog log)
        {
            int n = def.MetricCount;
            var v = new double[n];
            for (int i = 0; i < n; i++)
            {
                var m = def.Metrics[i];
                string key = "metric:" + m.Id;
                bool direct = false;
                foreach (var s in log.Samples) if (s.Kind == key) { v[i] = s.Value; direct = true; }
                if (direct) continue;
                v[i] = Derive(def, m, log);
            }
            return v;
        }

        /// 지표 하나를 로그에서 유도. 샘플이 없으면 D 기준(최악)으로 — 0 점
        static double Derive(DrillDef def, MetricDef m, DrillLog log)
        {
            double worst = m.D;
            switch (m.Id)
            {
                case "beat_sd": return Count(log, "beat_err") > 0 ? Sd(Values(log, "beat_err")) : worst;
                case "beat_err": return Count(log, "beat_err") > 0 ? MeanAbs(Values(log, "beat_err")) : worst;
                case "rhythm": return Count(log, "rhythm") > 0 ? MeanAbs(Values(log, "rhythm")) : worst;
                case "trips": return Count(log, "trip");
                case "misses": return Count(log, "sb") > 0 ? CountValue(log, "sb", 0) : worst;
                case "late_hold":
                    {
                        // D01: 라운드 마지막 30초 박자 중 허용 창(|오차| ≤ 60ms) 안 비율 / D04: 1R 평균 오차 ÷ 3R(마지막 라운드) 평균 오차 × 100(100 넘으면 100)
                        if (def.Id == "D04")
                        {
                            var first = Values(log, "timing", 1); var last = Values(log, "timing", log.Rounds > 0 ? log.Rounds : def.Rounds);
                            if (first.Count == 0 || last.Count == 0) return worst;
                            double a = MeanAbs(first) + 10.0, b = MeanAbs(last) + 10.0;      // 바닥 10ms: 둘 다 0 이면 100%
                            return Math.Min(100, a / b * 100);
                        }
                        int tot = 0, ok = 0;
                        double len = log.RoundSec > 0 ? log.RoundSec : def.RoundSec;
                        foreach (var s in log.Of("beat_err")) { if (s.T < len - 30.0) continue; tot++; if (Math.Abs(s.Value) <= 60.0) ok++; }
                        return tot > 0 ? 100.0 * ok / tot : worst;
                    }
                case "du_rate": return Count(log, "du") > 0 ? 100.0 * CountValue(log, "du", 1) / Count(log, "du") : worst;
                case "du_streak": return Streak(log, "du");
                case "streak": return Streak(log, "sb");
                case "combo_acc": return Count(log, "combo") > 0 ? 100.0 * CountValue(log, "combo", 1) / Count(log, "combo") : worst;
                case "react": return Count(log, "react") > 0 ? Mean(Values(log, "react")) : worst;
                case "guard_rate": return Count(log, "guard") > 0 ? 100.0 * CountValue(log, "guard", 1) / Count(log, "guard") : worst;
                case "gap_sd": return Count(log, "gap") > 1 ? Sd(Values(log, "gap")) : (Count(log, "gap") == 1 ? 0 : worst);
                case "timing": return Count(log, "timing") > 0 ? MeanAbs(Values(log, "timing")) : worst;
                case "power_hold":
                    {
                        // 3R 마지막 30초 평균 파워 ÷ 1R 첫 30초 평균 파워(%)
                        double len = log.RoundSec > 0 ? log.RoundSec : def.RoundSec;
                        int lastR = log.Rounds > 0 ? log.Rounds : def.Rounds;
                        var a = new List<double>(); var b = new List<double>();
                        foreach (var s in log.Of("power")) { if (s.Round == 1 && s.T < 30.0) a.Add(s.Value); if (s.Round == lastR && s.T >= len - 30.0) b.Add(s.Value); }
                        if (a.Count == 0 || b.Count == 0) return worst;
                        double ma = Mean(a); if (ma < 1e-6) return worst;
                        return Math.Min(100, Mean(b) / ma * 100);
                    }
                case "hits_per_round": { int r = log.Rounds > 0 ? log.Rounds : def.Rounds; return Count(log, "hit") > 0 ? (double)Count(log, "hit") / r : 0; }
                case "posture":
                    {
                        int tot = Count(log, "posture") + Count(log, "fake_fail");
                        return tot > 0 ? 100.0 * CountValue(log, "posture", 1) / tot : worst;
                    }
                case "recover": return Count(log, "recover") > 0 ? Mean(Values(log, "recover")) : worst;
                case "lc_err": return Count(log, "lc_err") > 0 ? MeanAbs(Values(log, "lc_err")) : worst;
                case "order": return Count(log, "order") > 0 ? 100.0 * CountValue(log, "order", 1) / Count(log, "order") : worst;
                case "finish": return Count(log, "finish") > 0 ? 100.0 * CountValue(log, "finish", 1) / Count(log, "finish") : worst;
                case "shrimp_err": return Count(log, "shrimp_err") > 0 ? MeanAbs(Values(log, "shrimp_err")) : worst;
                case "escape": return Count(log, "escape") > 0 ? 100.0 * CountValue(log, "escape", 1) / Count(log, "escape") : worst;
                case "bridge_err": return Count(log, "bridge_err") > 0 ? MeanAbs(Values(log, "bridge_err")) : worst;
                case "land_rate": return Count(log, "swing") > 0 ? 100.0 * CountValue(log, "swing", 1) / Count(log, "swing") : worst;
                case "avoid_rate": return Count(log, "incoming") > 0 ? 100.0 * CountValue(log, "incoming", 1) / Count(log, "incoming") : worst;
                case "reads": return Count(log, "read");
                case "violations": return Count(log, "violation");
                case "position": return log.Sum("position");
                default: return worst;
            }
        }

        static int Count(DrillLog log, string kind) => log.Count(kind);
        static int CountValue(DrillLog log, string kind, double value) { int n = 0; foreach (var s in log.Of(kind)) if (Math.Abs(s.Value - value) < 1e-9) n++; return n; }
        static List<double> Values(DrillLog log, string kind, int round = 0) { var l = new List<double>(); foreach (var s in log.Of(kind)) if (round == 0 || s.Round == round) l.Add(s.Value); return l; }
        static double Mean(List<double> v) { if (v.Count == 0) return 0; double s = 0; foreach (var x in v) s += x; return s / v.Count; }
        static double MeanAbs(List<double> v) { if (v.Count == 0) return 0; double s = 0; foreach (var x in v) s += Math.Abs(x); return s / v.Count; }
        static double Sd(List<double> v) { if (v.Count < 2) return 0; double m = Mean(v), s = 0; foreach (var x in v) s += (x - m) * (x - m); return Math.Sqrt(s / v.Count); }
        static int Streak(DrillLog log, string kind) { int best = 0, cur = 0; foreach (var s in log.Of(kind)) { if (s.Value >= 0.5) { cur++; if (cur > best) best = cur; } else cur = 0; } return best; }
    }
}
