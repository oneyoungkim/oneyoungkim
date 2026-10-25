// 행인1의 메인이벤트 — 오픈월드 데이터 에셋(docs/13_오픈월드_커리어_설계.md 5-2·9장·11장)
// 원본 = Data/World/{chapters,activities,balance}.json + events/*.json → WorldSetup(에디터)이 Settings/World.asset 으로 옮긴다(결정적 — W01).
// 손으로 고치지 말 것(JSON 을 고친다). 시각은 "HH:MM" → 자정부터 분, 날짜는 "MM-DD" → 월 × 100 + 일(DayKey).
using System;
using UnityEngine;

namespace Haengin
{
    public enum CardKind { Core, Season, Cond, Side, Repeat, Msg, System }

    [Serializable]
    public sealed class GoalDef
    {
        public string Label = "", Expr = "", Unit = "";
        public float Target;
        /// 상한형(결손 N회 이하) — 값이 Target 이하면 통과
        public bool Max;
    }

    [Serializable]
    public sealed class ChapterDef
    {
        public string Id = "";
        public int No;
        public string Title = "", Period = "", Age = "", Ladder = "", Goal = "", Climax = "", Unlocks = "", Promote = "";
        public GoalDef[] Goals = new GoalDef[0];
        /// 데모 구간(1장 3/3~5/8): 그 구간의 목표(없으면 빈 배열)
        public bool HasDemo;
        public int DemoFrom, DemoTo;
        public string DemoTitle = "", DemoGoal = "";
        public GoalDef[] DemoGoals = new GoalDef[0];
        public string Label => No > 0 ? $"{No}장 「{Title}」" : $"막간 「{Title}」";
    }

    [Serializable]
    public sealed class ActivityDef
    {
        public string Id = "", Name = "", Kind = "", Work = "", Place = "";
        public string[] Markers = new string[0];
        /// 요일 글자("월화수목금") — 빈칸 = 언제나
        public string Days = "";
        public int Open, Start, End, Latest;
        /// 실제 일한 분(점심 빼고). 0 = End − Start
        public int WorkMinutes;
        public int Pay;
        public int[] Units = new int[0];
        /// household(엄마 통장·생활비) · wallet(지갑) · none
        public string To = "none";
        public string Memo = "";
        public string[] Items = new string[0];
        public int Fatigue;
        /// "stamina:0.3"
        public string[] Stats = new string[0];
        public string Mini = "";
        public float Rep;
        /// 이 열쇠가 풀려야 할 수 있음(빈칸 = 늘)
        public string Unlock = "";
        public bool RainOff;

        public int Minutes => WorkMinutes > 0 ? WorkMinutes : Mathf.Max(0, End - Start);
        public bool IsJob => Kind == "job";
        public bool OnDay(string weekday) => string.IsNullOrEmpty(Days) || Days.Contains(weekday);
    }

    [Serializable]
    public sealed class CardChoice
    {
        public string Label = "";
        public string[] After = new string[0];
    }

    [Serializable]
    public sealed class CardVariant
    {
        public string Need = "";
        public string Title = "";
        public string[] Board = new string[0];
        public string[] Quotes = new string[0];
        public string[] After = new string[0];
    }

    [Serializable]
    public sealed class CardDef
    {
        public string Id = "", Group = "", Title = "", Chapter = "";
        public CardKind Kind;
        /// 개발용 모티프 표시(유·반·맥N·원) — 화면에 안 보임
        public string[] Motif = new string[0];
        public string[] Src = new string[0];
        /// 본편: M3 장면 id 체인(없으면 스토리보드 카드)
        public string[] Scenes = new string[0];
        public bool NoSceneEffects;
        public string[] Board = new string[0];
        public string[] Quotes = new string[0];
        /// 날짜: Date(박힌 날, DayKey) 또는 From~To 창, Monthly = 매달 그 날(0 = 아님)
        public int Date, WinFrom, WinTo, Monthly;
        public string Days = "";
        public string[] Bands = new string[0];
        /// 시각 창(분, −1 = 없음)
        public int From = -1, To = -1;
        public string Place = "", Zone = "";
        /// 뜨는 자리(여럿 = '|' 로 나눈 것을 배열로)
        public string[] At = new string[0];
        public string[] Need = new string[0];
        public string[] Block = new string[0];
        public string[] After = new string[0];
        public CardChoice[] Choice = new CardChoice[0];
        public CardVariant[] Ifs = new CardVariant[0];
        public string Fight = "";
        /// never · summary · wait · lost
        public string Miss = "wait";
        public int NoticeAt = -1;
        public string NoticeFrom = "", NoticeText = "";
        public float Weight = 1f;
        public int Limit;
        /// 데모(3/3~5/8)에서 감독이 본다
        public bool Demo;

        public string GroupId => string.IsNullOrEmpty(Group) ? Id : Group;
        public bool HasScenes => Scenes != null && Scenes.Length > 0;
        public bool AtHas(string trig) { foreach (var a in At) if (a == trig) return true; return false; }
        public int Priority => Kind switch { CardKind.Core => 0, CardKind.System => 0, CardKind.Season => 1, CardKind.Cond => 2, CardKind.Msg => 2, CardKind.Side => 3, _ => 4 };
        public override string ToString() => $"{Id} 「{Title}」";
    }

    [Serializable]
    public sealed class PointDef
    {
        public string Id = "", Zone = "", Label = "";
        public Vector3 Pos;
        public float Radius = 2.5f;
    }

    [Serializable]
    public sealed class RepTier
    {
        public int From;
        public string Name = "";
    }

    [Serializable]
    public sealed class FixedCost
    {
        public string Name = "";
        public int Amount;
    }

    [Serializable]
    public sealed class RumorLines
    {
        public string[] Lines = new string[0];
    }

    [Serializable]
    public sealed class BalanceDef
    {
        public int StartYear = 2026, StartDate = 303, StartTime = 350, StartWallet = 2600, Seed = 20260303;
        public float ClockRate = 3f;
        public int Wake = 330, WakeSunday = 480, NightLimit = 120;
        public string[] BandNames = new string[0];
        public int[] BandFrom = new int[0];
        public int[] Holidays = new int[0];
        public int MinorNightFrom = 1320, MinorNightTo = 360, PerWorkDay = 420, WeekSoft = 2100;
        public float OverworkMul = 1.5f;
        public int SleepPerHour = 9, Tired = 70, GymBlock = 85, Collapse = 95, SleptTiredAdd = 10, RestDay = 20, Nap = 9, DrillMinutes = 30, StreetFatigue = 12, NightOut = 10;
        public float TiredMul = 0.6f;
        public FixedCost[] Fixed = new FixedCost[0];
        public int AcademyMonth = 9, Academy = 350000, EnvelopeGoal = 40000, EnvelopeSunday = 5000, Cake2 = 38000, Cake1 = 28000;
        public int GymFee = 150000, CleanerFee = 50000, BusMinutes = 15, GymOpen = 420, GymClose = 1320;
        public string[] TrialDrills = new string[0];
        public RepTier[] RepTiers = new RepTier[0];
        public int StreetWin = 2, StreetWinMany = 3, StreetLoss = -1, SideDone = 3;
        public float DeliveryGood = 0.3f;
        public RumorLines[] Rumor = new RumorLines[0];
        public int DelegateShifts = 10;
        public float DelegateStat = 0.75f;
        public float[] GradeCuts = { 0.35f, 0.55f, 0.75f, 0.9f };
        public float[] RainByMonth = new float[13];
        public int[] RainOn = new int[0], ClearOn = new int[0];
        public string[] GameZones = new string[0];
        public PointDef[] Points = new PointDef[0];

        public int FixedTotal(int month) { int s = 0; foreach (var f in Fixed) s += f.Amount; if (month >= AcademyMonth) s += Academy; return s; }
        public string RepName(float v) { string n = ""; foreach (var t in RepTiers) if (v >= t.From) n = t.Name; return n; }
        public PointDef Point(string id) => Array.Find(Points, p => p.Id == id);
    }

    [CreateAssetMenu(menuName = "Haengin/World")]
    public sealed class WorldDef : ScriptableObject
    {
        public int Version = 1;
        public ChapterDef[] Chapters = new ChapterDef[0];
        public ActivityDef[] Activities = new ActivityDef[0];
        public CardDef[] Cards = new CardDef[0];
        public BalanceDef Balance = new BalanceDef();
        /// docs/03 map_zones 의 존 id(W01 대조용 — WorldSetup 이 data/map_zones.json 에서 넣음)
        public string[] MapZones = new string[0];
        /// 체육관 드릴(Settings/DrillBook.asset) — 주간 계획 맡기기·체험 드릴
        public DrillBook Drills;

        public ChapterDef Chapter(string id) => Array.Find(Chapters, c => c.Id == id);
        public ActivityDef Activity(string id) => Array.Find(Activities, a => a.Id == id);
        public CardDef Card(string id) => Array.Find(Cards, c => c.Id == id);
        public ActivityDef ActivityForMarker(string func) => Array.Find(Activities, a => a.Markers != null && Array.IndexOf(a.Markers, func) >= 0);
    }
}
