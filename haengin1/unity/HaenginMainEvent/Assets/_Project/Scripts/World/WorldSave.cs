// 행인1의 메인이벤트 — 오픈월드 저장 칸(docs/13 10장 저장 v2): SaveData.World 에 들어간다(JsonUtility).
// 세계 시계 날짜·시각은 SaveData.Month/Day/Minutes(+ 여기 Year), 가계부·도감·플래그·몸은 SaveData 의 기존 칸(Ledger·Names·Flags·Gym)을 그대로 쓴다.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Haengin
{
    /// 카드 상태(10장): 0 대기 · 1 완료 · 2 요약(결과만) · 3 놓침(사연 — 도감 기록)
    [Serializable]
    public sealed class CardState
    {
        public string Id = "";
        public int State;
        /// 끝난 날(DayKey) · 그 날 몇 번(반복 카드)
        public int Day, Times;
    }

    [Serializable]
    public sealed class Counter
    {
        public string Key = "";
        public float Value;
    }

    [Serializable]
    public sealed class Hole
    {
        public string Name = "";
        public int Amount;
        public bool Paid;
    }

    [Serializable]
    public sealed class PhoneMsg
    {
        public int Day, Min;
        public string From = "", Text = "";
        public bool Read;
    }

    [Serializable]
    public sealed class PlanDay
    {
        public int Day;
        /// 새벽(배달·잠) · 저녁(국밥집·체육관·쉼) · 밤(입고·청소·쉼) · 주말(노가다·편의점·쉼)
        public string Dawn = "", Evening = "", Night = "", Weekend = "";
    }

    /// 개인레슨 예약(docs/13 14-6): 0 예약 · 1 받음 · 2 노쇼 · 3 맡김(주간 계획이 대신 감)
    [Serializable]
    public sealed class Booking
    {
        public string Coach = "", Drill = "";
        public int Day, At, Paid, State, Grade = -1;
        public bool Ticket;
    }

    /// 먹은 것 효과(시간 제한) — Until = 시작일부터 센 분(WorldModel.AbsNow)
    [Serializable]
    public sealed class Buff
    {
        public string Kind = "", Src = "";
        public float Value;
        public int Until;
    }

    /// 무엇을 언제 몇 번 먹었나(체감 감소)
    [Serializable]
    public sealed class Eaten
    {
        public string Id = "";
        public int Day, Times;
    }

    [Serializable]
    public sealed class WorldSave
    {
        public bool Active;
        public int Year = 2026;
        public string Chapter = "1";
        public int Seed = 20260303;
        /// 이어할 자리(무대 키·스폰 또는 Zone1 위치)
        public string Place = "Zone1", Spawn = "";
        public bool HasPos;
        public Vector3 Pos;
        public float Yaw;
        /// 카드 시작 저장이면 그 카드(불러오면 그 카드부터 — 09 6-5-1 '장면 중간 저장 없음')
        public string Card = "";
        /// 이번 달 생활비로 들어간 돈(엄마 통장·고지서 밑) · 결손 횟수 · 지난 말일 모자란 돈 · 정산한 달
        public int Household, Deficits, LastShortfall, SettledMonth;
        public List<Hole> Holes = new List<Hole>();
        public float TownRep, SchoolRep;
        public int Rumor;
        public int StreetWins, StreetLosses, YachaWins;
        public List<CardState> Cards = new List<CardState>();
        public List<string> Unlocks = new List<string>();
        /// 셈(알바 회수 job.* · 관계 rel.* · 숙련 skill.* · 그 밖)
        public List<Counter> Counters = new List<Counter>();
        public float[] StatFrac = new float[8];
        /// 오늘(WorkDay) 일터별 일한 분 · 이번 주 일한 분(월요일 0)
        public int WorkDay;
        public List<Counter> WorkToday = new List<Counter>();
        public int WeekMinutes;
        public List<string> DoneToday = new List<string>();
        public int CoreToday, SideToday;
        public List<PhoneMsg> Phone = new List<PhoneMsg>();
        /// 하루 결산 한 줄씩(휴대폰 '일정' 뒤 기록)
        public List<string> Days = new List<string>();
        /// 체육관: 0 없음 · 1 회비 · 2 청소 관원 / 체험 / 회비 낸 달 / 빚(고준이 대신 낸 달 수)
        public int GymMember;
        public bool GymTrial;
        public int GymPaidMonth, GymDebt;
        public int Late, Absent, Attended;
        public List<PlanDay> Plan = new List<PlanDay>();
        /// 데모 끝: 0 진행 · 1 목표 달성 · 2 목표 미달로 끝
        public int DemoState;
        /// 다음 아침에 볼 것(문자·근육통 등) — 표시용
        public int Slept;
        /// 돈 쓸 곳(14-6): 레슨 예약 · 먹은 효과 · 먹은 기록(오늘·어제) · 오늘 배부름
        public List<Booking> Lessons = new List<Booking>();
        /// 매주 고정 레슨(Day = 요일 0 월 ~ 6 일) — 그 날 아침 자동 예약(돈·회권 규칙 그대로)
        public List<Booking> Standing = new List<Booking>();
        public List<Buff> Buffs = new List<Buff>();
        public List<Eaten> Eats = new List<Eaten>();
        public int Full;

        public WorldSave Clone() => JsonUtility.FromJson<WorldSave>(JsonUtility.ToJson(this));

        public CardState Card0(string id, bool create)
        {
            foreach (var c in Cards) if (c.Id == id) return c;
            if (!create) return null;
            var n = new CardState { Id = id };
            Cards.Add(n);
            return n;
        }

        public float Get(string key)
        {
            foreach (var c in Counters) if (c.Key == key) return c.Value;
            return 0f;
        }

        public void Add(string key, float v)
        {
            foreach (var c in Counters) if (c.Key == key) { c.Value += v; return; }
            Counters.Add(new Counter { Key = key, Value = v });
        }

        public void Set(string key, float v)
        {
            foreach (var c in Counters) if (c.Key == key) { c.Value = v; return; }
            Counters.Add(new Counter { Key = key, Value = v });
        }

        public int Worked(string work)
        {
            foreach (var c in WorkToday) if (c.Key == work) return Mathf.RoundToInt(c.Value);
            return 0;
        }

        public void AddWork(string work, int minutes)
        {
            if (string.IsNullOrEmpty(work)) return;
            foreach (var c in WorkToday) if (c.Key == work) { c.Value += minutes; return; }
            WorkToday.Add(new Counter { Key = work, Value = minutes });
        }
    }
}
