// 행인1의 메인이벤트 — 돈 쓸 곳(docs/13 14-6): 개인레슨(돈 → 실력) · 영양제·보양식(돈 → 회복). 원본 Data/World/shop.json + lesson_lines.tsv → WorldDef.Shop.
using System;
using UnityEngine;

namespace Haengin
{
    [Serializable]
    public sealed class LessonSlot
    {
        /// 요일 글자 묶음("월수금") · 이름(아침 PT 등) · 시작 분
        public string Days = "", Label = "";
        public int At;
    }

    [Serializable]
    public sealed class TechDef
    {
        public string Id = "", Name = "", Text = "";
        /// 이 코치 레슨 몇 번째부터 · 몇 등급 이상(Grade 숫자)이면 익힘 · 어느 드릴에서 · 익히면 그 드릴 판정 +점
        public int Lessons = 2, Grade = 2;
        public string[] Drills = new string[0];
        public float Plus = 4f;
    }

    [Serializable]
    public sealed class CoachDef
    {
        public string Id = "", Name = "", Title = "", Gym = "", Bio = "", Unlock = "";
        /// 출장(혜화 MMA 로 오는 다른 체육관 코치)
        public bool Visit;
        public LessonSlot[] Slots = new LessonSlot[0];
        /// 주당 횟수 · 시우 가격(설정) · 정가 · 10회권(0 = 없음) · 10회 정가
        public int Week = 1, Price, List, Ticket10, List10;
        /// 스탯 배율 · 판정 +점 · 최저 지표 보정 · 피로 배율 · 교정 +점(다음 판정, 서진수)
        public float Mul = 1.5f, Bonus, Metric, FatigueMul = 1.5f, Fix;
        /// 고를 수 있는 드릴("*" = 해 본 드릴 아무거나)
        public string[] Drills = new string[0];
        public bool HasTech;
        public TechDef Tech = new TechDef();
        public bool AnyDrill => Array.IndexOf(Drills, "*") >= 0;
    }

    [Serializable]
    public sealed class BuffSpec
    {
        /// stamina(드는 피로 −%) · hp(싸움 체력 +%) · cond(드릴 판정 +점)
        public string Kind = "";
        public int Minutes;
        public float Value;
    }

    [Serializable]
    public sealed class FoodDef
    {
        public string Id = "", Name = "", Shop = "", Supp = "", Effect = "";
        public int Price, List, Minutes, Full, Doses, Heal;
        public float Fatigue;
        /// 가방에 넣을 수 있나 · 공짜(엄마 곰탕)
        public bool Carry, Free;
        public string[] Aka = new string[0];
        public BuffSpec[] Buffs = new BuffSpec[0];
        public bool IsSupp => !string.IsNullOrEmpty(Supp);
    }

    [Serializable]
    public sealed class ShopPlace
    {
        public string Id = "", Name = "", Place = "";
    }

    [Serializable]
    public sealed class LessonLine
    {
        public string Coach = "", Kind = "", Text = "";
    }

    [Serializable]
    public sealed class ShopDef
    {
        public int Minutes = 40, GraceBefore = 15, GraceAfter = 20, NoticeBefore = 60, NoshowRel = -2, LessonRel = 1, BlockRel = -4, BlockDays = 7;
        public float AutoMul = 0.75f, CueEvery = 7f;
        public CoachDef[] Coaches = new CoachDef[0];
        public int FullCap = 12, SuppDays = 3, SuppMiss = 2;
        public float[] SameDay = { 1f, 0.6f, 0.35f, 0.2f };
        public float StreakMul = 0.85f, StreakMin = 0.55f;
        public ShopPlace[] Shops = new ShopPlace[0];
        public FoodDef[] Foods = new FoodDef[0];
        public string[] BuffKinds = new string[0], BuffNames = new string[0], BuffNotes = new string[0];
        public LessonLine[] Lines = new LessonLine[0];

        public CoachDef Coach(string id) => Array.Find(Coaches, c => c.Id == id);
        public FoodDef Food(string id) => Array.Find(Foods, f => f.Id == id);
        public FoodDef FoodByName(string name) => Array.Find(Foods, f => f.Name == name || Array.IndexOf(f.Aka, name) >= 0);
        public ShopPlace Shop(string id) => Array.Find(Shops, s => s.Id == id);
        public string BuffName(string kind) { int i = Array.IndexOf(BuffKinds, kind); return i >= 0 && i < BuffNames.Length ? BuffNames[i] : kind; }
        public string[] LinesOf(string coach, string kind) => Array.ConvertAll(Array.FindAll(Lines, l => l.Coach == coach && l.Kind == kind), l => l.Text);
        /// 그 종류 대사 하나(seed 로 고름 — 같은 날 같은 대사)
        public string Line(string coach, string kind, int seed)
        {
            var a = LinesOf(coach, kind);
            return a.Length == 0 ? "" : a[Mathf.Abs(seed) % a.Length];
        }
    }
}
