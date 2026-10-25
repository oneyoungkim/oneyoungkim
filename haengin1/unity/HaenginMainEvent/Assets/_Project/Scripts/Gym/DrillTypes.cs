// 행인1의 메인이벤트 — 트레이닝 드릴 공통 형식(docs/12_체육관_설계.md 4-6)
// DrillDef = 드릴 1종 데이터(원본 Data/Drill/drills.json → GymSetup 이 DrillBook 에셋에 넣음, 손으로 고치지 않음)
// DrillLog = 플레이 중 샘플 목록(종류·시각·값·라운드) — 같은 로그면 같은 등급(DrillGrader 는 난수 없음)
// DrillSave/GymSave = 드릴별 최고 등급·시뮬 상태 + 시우 몸 상태(스탯 8·피로·땀·부상) — SaveData.Gym 에 그대로 들어간다
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Haengin
{
    public enum Grade { D = 0, C = 1, B = 2, A = 3, S = 4 }

    /// 드릴 분류(4-6)
    public enum DrillCategory { Striking, Wrestling, Ground, Condition, Outdoor }

    /// 스탯(기획서 3장): 파워 · 스피드 · 스태미나 · 맷집 · 기술(타격·레슬링·주짓수) · 멘탈
    public enum Stat { None = -1, Power = 0, Speed = 1, Stamina = 2, Toughness = 3, Striking = 4, Wrestling = 5, Jiu = 6, Mental = 7 }

    [Serializable]
    public sealed class MetricDef
    {
        public string Id = "", LabelKr = "", Unit = "";
        public bool HigherIsBetter;
        /// D 기준 · S 기준 · 가중치(Σ = 1)
        public float D, S, Weight;

        /// 지표 점수 m = clamp01( (v − d)/(s − d) ) 또는 (d − v)/(d − s) — 4-3
        public double Score(double v)
        {
            double den = HigherIsBetter ? (S - D) : (D - S);
            if (Math.Abs(den) < 1e-9) return v >= S ? 1.0 : 0.0;
            double m = HigherIsBetter ? (v - D) / den : (D - v) / den;
            return m < 0 ? 0 : m > 1 ? 1 : m;
        }

        public string Format(double v)
        {
            string n = Unit == "ms" || Unit == "회" || Unit == "점" || Unit == "s" ? Math.Round(v).ToString("0") : v.ToString("0.#");
            return Unit == "%" ? $"{n}%" : Unit == "ms" ? $"{n} ms" : $"{n}{(string.IsNullOrEmpty(Unit) ? "" : " " + Unit)}";
        }
    }

    [Serializable]
    public sealed class DrillDef
    {
        public string Id = "", NameKr = "", RealName = "";
        public DrillCategory Category;
        public int Rounds = 3;
        public float RoundSec = 60f, RestSec = 8f;
        public int FatigueCost = 10, SweatBase = 20;
        public Stat MainStat = Stat.Power, SubStat = Stat.None;
        public MetricDef[] Metrics = new MetricDef[0];
        public string Socket = "", CamPreset = "front";
        /// 미트·샌드백·스파링 = M2 전투 판정 재사용
        public bool UsesCombat;
        /// S 뒤 시뮬 가능(D11 스파링은 불가 — 머리 안전)
        public bool SimAllowed = true;
        /// 이번에 플레이 가능한가(D09·D12·D13·D14 는 자리만)
        public bool Playable = true;
        /// 머리 충격 가능 드릴(뇌진탕 의심이면 잠김 — D09·D11)
        public bool HeadRisk;
        public bool StoryOnly;
        /// 큐: 시작 · 라운드 · S 결과 · 고준 · 스톱 · 피격
        public string CueStart = "", CueRound = "", CueS = "", CueGojun = "", CueStop = "", CueHurt = "";

        public int MetricCount => Metrics != null ? Metrics.Length : 0;
        public int IndexOf(string metricId) { for (int i = 0; i < MetricCount; i++) if (Metrics[i].Id == metricId) return i; return -1; }
        public override string ToString() => $"{Id} {NameKr}";
    }

    /// 플레이 중 샘플(종류 · 라운드 시작 기준 초 · 값 · 라운드 번호 1~). 종류 이름은 DrillMetrics 에 적혀 있다.
    /// 지표 값을 직접 넣을 수도 있다: Kind = "metric:<id>" (시험·자리 드릴·로드워크 기록 올리기)
    public sealed class DrillLog
    {
        public struct Sample
        {
            public string Kind; public double T; public double Value; public int Round;
            public override string ToString() => $"{Kind}@{T:F3}={Value:F1}(R{Round})";
        }

        public readonly List<Sample> Samples = new List<Sample>();
        public string DrillId = "";
        public int Rounds;
        public float RoundSec;
        /// 중단(그만두기) — 보상 0 · 피로 절반
        public bool Aborted;
        /// 뇌진탕 의심으로 끝남(D11)
        public bool Concussion;

        public void Add(string kind, double t, double value = 0, int round = 0) => Samples.Add(new Sample { Kind = kind, T = t, Value = value, Round = round });
        public void Metric(string id, double value) => Add("metric:" + id, 0, value);

        public IEnumerable<Sample> Of(string kind) { foreach (var s in Samples) if (s.Kind == kind) yield return s; }
        public int Count(string kind) { int n = 0; foreach (var s in Samples) if (s.Kind == kind) n++; return n; }
        public double Sum(string kind) { double v = 0; foreach (var s in Samples) if (s.Kind == kind) v += s.Value; return v; }
        public bool Has(string kind) { foreach (var s in Samples) if (s.Kind == kind) return true; return false; }

        public DrillLog Clone()
        {
            var c = new DrillLog { DrillId = DrillId, Rounds = Rounds, RoundSec = RoundSec, Aborted = Aborted, Concussion = Concussion };
            c.Samples.AddRange(Samples);
            return c;
        }
    }

    /// 등급 결과(4-3): 지표 값 · 지표 점수 m · 점수 0~100 · 등급
    public sealed class DrillResult
    {
        public DrillDef Def;
        public double[] Values = new double[0];
        public double[] Scores = new double[0];
        public double Score;
        public Grade Grade;
        public bool Aborted, Concussion;
        /// 판정 보정으로 더한 점수(레슨·컨디션·기술·교정)
        public double Assist;
        public double MinScore { get { double m = 1; foreach (var s in Scores) m = Math.Min(m, s); return Scores.Length > 0 ? m : 0; } }
        public override string ToString()
        {
            var parts = new List<string>();
            for (int i = 0; i < Values.Length && i < Def.MetricCount; i++) parts.Add($"{Def.Metrics[i].LabelKr} {Def.Metrics[i].Format(Values[i])}(m {Scores[i]:F2})");
            return $"{Def.Id} {Grade} {Score:F1} [{string.Join(" · ", parts)}]{(Aborted ? " 중단" : "")}{(Concussion ? " 뇌진탕 의심" : "")}";
        }
    }

    /// 보상(4-4): 땀 · 주/부 스탯 · 피로 · 관계도(코치)
    public struct DrillReward
    {
        public int Sweat, MainDelta, SubDelta, Fatigue, Relation;
        public bool FirstS;
        public float Efficiency;
        public override string ToString() => $"땀 +{Sweat} · 주 +{MainDelta} · 부 +{SubDelta} · 피로 +{Fatigue} · 관계 +{Relation}{(FirstS ? " · 첫 S" : "")}{(Efficiency < 1f ? $" · 효율 ×{Efficiency:F1}" : "")}";
    }

    [Serializable]
    public sealed class DrillSave
    {
        public string Id = "";
        /// 최고 등급(−1 = 아직) · 마지막 등급
        public int Best = -1, Last = -1;
        public int Plays, SCount;
        public bool SimUnlocked;
        /// 시뮬 횟수(검정 뒤부터) — 8 이면 다음 1회는 직접(B 이상)
        public int SimRunsSinceCert;
        public int SimRuns;
        public Grade BestGrade => (Grade)Mathf.Max(0, Best);
        public bool HasBest => Best >= 0;
        public bool CertDue => SimUnlocked && SimRunsSinceCert >= DrillSim.CertEvery;
    }

    /// 시우 몸 상태 + 드릴 기록(기획서 3장 · 12 문서 4-1·4-4·4-5)
    [Serializable]
    public sealed class GymSave
    {
        public const int StatCap = 100, StatSoftFrom = 90;
        /// 스탯 8(Stat 순서): 파워·스피드·스태미나·맷집·타격·레슬링·주짓수·멘탈
        public int[] Stats = { 20, 20, 20, 20, 20, 20, 20, 20 };
        public int Fatigue, Sweat, CoachRelation;
        /// 시뮬 하루 상한(날짜 열쇠 = 월 × 100 + 일)
        public int SimDay = -1, SimToday;
        public int RoutineDay = -1, RoutineToday;
        /// 부상(뇌진탕 의심): 이 날짜(열쇠) 전까지 머리 드릴 잠김
        public int InjuryUntil = -1;
        public string InjuryKind = "";
        public List<DrillSave> Drills = new List<DrillSave>();

        public static int DayKey(int month, int day) => month * 100 + day;
        public static int Today => DayKey(GameClock.Month, GameClock.Day);

        public int Stat(Stat s) => s == Haengin.Stat.None ? 0 : Stats[(int)s];
        public void AddStat(Stat s, int delta)
        {
            if (s == Haengin.Stat.None || delta == 0) return;
            int i = (int)s;
            Stats[i] = Mathf.Clamp(Stats[i] + delta, 0, StatCap);
        }

        public DrillSave Of(string id, bool create = true)
        {
            foreach (var d in Drills) if (d.Id == id) return d;
            if (!create) return null;
            var n = new DrillSave { Id = id };
            Drills.Add(n);
            return n;
        }

        public bool Injured(int today) => InjuryUntil >= 0 && today < InjuryUntil;
        public int SimLeft(int today) { if (SimDay != today) return DrillSim.SimPerDay; return Mathf.Max(0, DrillSim.SimPerDay - SimToday); }

        public GymSave Clone() => JsonUtility.FromJson<GymSave>(JsonUtility.ToJson(this));
        public string Summary => $"피로 {Fatigue} · 땀 {Sweat} · 스탯 [{string.Join(",", Stats)}] · 드릴 {Drills.Count}종 기록 · 시뮬 오늘 {SimToday}{(Injured(Today) ? " · 부상 " + InjuryKind : "")}";

        public static readonly string[] StatNames = { "파워", "스피드", "스태미나", "맷집", "타격", "레슬링", "주짓수", "멘탈" };
        public static string NameOf(Stat s) => s == Haengin.Stat.None ? "—" : StatNames[(int)s];
    }

    /// 지금 플레이의 몸 상태(저장 사이) — 이야기 저장(SaveData.Gym)에 들어가고, 연습장에서는 세션 동안만
    public static class GymState
    {
        public static GymSave Current = new GymSave();
        /// 체육관에 들어온 횟수(시험)
        public static int Visits;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Current = new GymSave(); Visits = 0; }
    }
}
