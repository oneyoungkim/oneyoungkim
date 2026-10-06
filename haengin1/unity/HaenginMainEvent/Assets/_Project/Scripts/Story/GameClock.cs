// 행인1의 메인이벤트 — 게임 시계(docs/09_M3_버티컬슬라이스_설계.md 2-1, 03 3.1 '게임 시계')
// 날짜·요일·시각. 장면 시작 때 장면이 정한 시각으로 점프, 주행·걷기 장면(SceneDef.Clock)에서만 실제 1초 = 게임 3초.
// 해 = 2026년(3월 3일 = 화요일, 5월 4일 = 월요일 — 원고 날짜와 요일이 맞는 해).
using System;
using UnityEngine;

namespace Haengin
{
    public static class GameClock
    {
        public const int Year = 2026;
        /// 실제 1초당 게임 초
        public const float Rate = 3f;

        public static int Month { get; private set; } = 3;
        public static int Day { get; private set; } = 3;
        /// 자정부터 게임 분(소수)
        public static double Minutes { get; private set; } = 5 * 60 + 31;
        /// 흐르는 중(StoryRunner 가 장면마다 정함)
        public static bool Running;
        /// 시각이 점프했을 때(날짜 판 다시 보이기)
        public static event Action Jumped;

        public static int Hour => (int)(Minutes / 60.0) % 24;
        public static int Minute => (int)Minutes % 60;

        public static void Set(int month, int day, int hour, int minute)
        {
            Month = month; Day = day;
            Minutes = hour * 60 + minute;
            Jumped?.Invoke();
        }

        /// 게임 시간 dt(초, 일시정지·히트스톱이면 0) 만큼 — Running 일 때만
        public static void Tick(float dt)
        {
            if (!Running || dt <= 0f) return;
            Minutes = Math.Min(Minutes + dt * Rate / 60.0, 24 * 60 - 1);
        }

        public static string WeekdayOf(int month, int day)
        {
            try { return "일월화수목금토"[(int)new DateTime(Year, month, day).DayOfWeek].ToString(); }
            catch (ArgumentOutOfRangeException) { return "?"; }
        }

        public static string Weekday => WeekdayOf(Month, Day);

        /// 시간대 말: 새벽 4~6 · 아침 7~10 · 낮 11~16 · 저녁 17~20 · 밤 21~3
        public static string PartOf(int hour) => hour >= 4 && hour < 7 ? "새벽" : hour >= 7 && hour < 11 ? "아침" : hour >= 11 && hour < 17 ? "낮" : hour >= 17 && hour < 21 ? "저녁" : "밤";

        public static string TimeText => $"{Hour:00}:{Minute:00}";
        /// 「3월 3일(화) 05:31 새벽」
        public static string Label => $"{Month}월 {Day}일({Weekday}) {TimeText} {PartOf(Hour)}";

        /// 저장·불러오기
        public static void Restore(int month, int day, double minutes) { Month = month; Day = day; Minutes = minutes; Jumped?.Invoke(); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Month = 3; Day = 3; Minutes = 5 * 60 + 31; Running = false; Jumped = null; }
    }
}
