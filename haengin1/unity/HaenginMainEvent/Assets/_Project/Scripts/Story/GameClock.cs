// 행인1의 메인이벤트 — 게임 시계(docs/09_M3_버티컬슬라이스_설계.md 2-1, 03 3.1 '게임 시계', docs/13 3-1 세계 시계)
// 날짜·요일·시각. 이야기 장면: 장면 시작 때 정한 시각으로 점프, 주행·걷기 장면(SceneDef.Clock)에서만 실제 1초 = 게임 3초(Tick — 자정에서 멈춤).
// 오픈월드(2026-10-25): Advance 는 자정을 넘기면 날짜를 넘긴다(DayChanged). 해는 바꿀 수 있다(기본 2026 — 3월 3일 = 화요일, 5월 4일 = 월요일).
using System;
using UnityEngine;

namespace Haengin
{
    public static class GameClock
    {
        /// 기본 해(원고 날짜와 요일이 맞는 해)
        public const int DefaultYear = 2026;
        /// 실제 1초당 게임 초
        public const float Rate = 3f;

        public static int Year { get; private set; } = DefaultYear;
        public static int Month { get; private set; } = 3;
        public static int Day { get; private set; } = 3;
        /// 자정부터 게임 분(소수)
        public static double Minutes { get; private set; } = 5 * 60 + 31;
        /// 흐르는 중(StoryRunner 가 장면마다 정함)
        public static bool Running;
        /// 시각이 점프했을 때(날짜 판 다시 보이기)
        public static event Action Jumped;
        /// 날짜가 넘어갔을 때(오픈월드 Advance·SetDate)
        public static event Action DayChanged;

        public static int Hour => (int)(Minutes / 60.0) % 24;
        public static int Minute => (int)Minutes % 60;
        /// 날짜 열쇠 = 월 × 100 + 일(GymSave.DayKey 와 같음)
        public static int DayKey => Month * 100 + Day;
        public static DateTime Date => SafeDate(Year, Month, Day);

        public static void Set(int month, int day, int hour, int minute)
        {
            bool dayChanged = month != Month || day != Day;
            Month = month; Day = day;
            Minutes = hour * 60 + minute;
            Jumped?.Invoke();
            if (dayChanged) DayChanged?.Invoke();
        }

        /// 해까지 정함(오픈월드 새 게임·불러오기·시험)
        public static void SetDate(int year, int month, int day, double minutes)
        {
            bool dayChanged = year != Year || month != Month || day != Day;
            Year = year; Month = month; Day = day; Minutes = minutes;
            Jumped?.Invoke();
            if (dayChanged) DayChanged?.Invoke();
        }

        /// 게임 시간 dt(초, 일시정지·히트스톱이면 0) 만큼 — Running 일 때만(이야기 장면 — 자정에서 멈춤)
        public static void Tick(float dt)
        {
            if (!Running || dt <= 0f) return;
            Minutes = Math.Min(Minutes + dt * Rate / 60.0, 24 * 60 - 1);
        }

        /// 오픈월드: 게임 분만큼 앞으로(자정을 넘으면 다음 날). 뒤로는 가지 않는다
        public static void Advance(double minutes)
        {
            if (minutes <= 0) return;
            Minutes += minutes;
            while (Minutes >= 24 * 60)
            {
                Minutes -= 24 * 60;
                var d = Date.AddDays(1);
                Year = d.Year; Month = d.Month; Day = d.Day;
                DayChanged?.Invoke();
            }
        }

        /// 오늘 그 시각까지(지났으면 그대로)
        public static void AdvanceTo(double minuteOfDay)
        {
            if (minuteOfDay > Minutes) Advance(minuteOfDay - Minutes);
        }

        static DateTime SafeDate(int y, int m, int d)
        {
            try { return new DateTime(y, m, d); }
            catch (ArgumentOutOfRangeException) { return new DateTime(DefaultYear, 3, 3); }
        }

        public static string WeekdayOf(int month, int day) => WeekdayOf(Year, month, day);

        public static string WeekdayOf(int year, int month, int day)
        {
            try { return "일월화수목금토"[(int)new DateTime(year, month, day).DayOfWeek].ToString(); }
            catch (ArgumentOutOfRangeException) { return "?"; }
        }

        public static string WeekdayOfKey(int key) => WeekdayOf(Year, key / 100, key % 100);

        /// 열쇠 + n일(해 넘김 없음 — 같은 해 안)
        public static int KeyPlus(int key, int days)
        {
            var d = SafeDate(Year, key / 100, key % 100).AddDays(days);
            return d.Month * 100 + d.Day;
        }

        /// 두 열쇠 사이 날 수(b − a)
        public static int DaysBetween(int a, int b) => (int)(SafeDate(Year, b / 100, b % 100) - SafeDate(Year, a / 100, a % 100)).TotalDays;

        public static string Weekday => WeekdayOf(Month, Day);

        /// 시간대 이름 표 하나(QA 2차 A3, 2026-10-26 — HUD·이야기 날짜판·휴대폰·카드 조건이 모두 이 표): 새벽 05:00 · 아침 08:00 · 낮 12:00 · 오후 16:00 · 저녁 18:00 · 밤 22:00
        ///   (예전: 세계 HUD '아침 08~16·오후 16~18' 와 이야기 날짜판 '낮 11~17' 이 달라 14:00 이 '아침'/'낮' 둘로 읽혔음). Data/World/balance.json clock.bands 도 같은 값(W00b 가 봄)
        public static readonly string[] BandNames = { "새벽", "아침", "낮", "오후", "저녁", "밤" };
        public static readonly int[] BandFrom = { 5 * 60, 8 * 60, 12 * 60, 16 * 60, 18 * 60, 22 * 60 };

        /// 분 → 시간대 이름(밤은 22:00 ~ 다음 날 05:00)
        public static string BandOf(double minutes)
        {
            double m = ((minutes % 1440.0) + 1440.0) % 1440.0;
            string n = BandNames[BandNames.Length - 1];
            for (int i = 0; i < BandFrom.Length; i++) if (m >= BandFrom[i]) n = BandNames[i];
            return n;
        }

        /// 시(時) → 시간대 이름(정시 기준)
        public static string PartOf(int hour) => BandOf(hour * 60);

        public static string TimeText => $"{Hour:00}:{Minute:00}";
        public static string Hm(int minutes) => $"{(minutes / 60) % 24:00}:{minutes % 60:00}";
        /// 「3월 3일(화) 05:31 새벽」
        public static string Label => $"{Month}월 {Day}일({Weekday}) {TimeText} {BandOf(Minutes)}";
        public static string DateText => $"{Month}월 {Day}일({Weekday})";

        /// 저장·불러오기
        public static void Restore(int month, int day, double minutes) { bool dc = month != Month || day != Day; Month = month; Day = day; Minutes = minutes; Jumped?.Invoke(); if (dc) DayChanged?.Invoke(); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Year = DefaultYear; Month = 3; Day = 3; Minutes = 5 * 60 + 31; Running = false; Jumped = null; DayChanged = null; }
    }
}
