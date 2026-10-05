// 행인1의 메인이벤트 — 기세 게이지 (docs/08_M2_전투_설계.md 3-7)
// 0~100, 100 = MAX 일 때만 기세 액션(10단계). 오름 = 기술표 '기세'(맞혔을 때)·읽었다 +15·막기 +2·버팀 +3,
// 내림 = 마지막으로 때리거나 맞은 뒤 4초 지나면 −3/초(게임 시간), 다운당함 −20, 가드 크러시 −5. 전투 시작 때 20.
using System;
using UnityEngine;

namespace Haengin
{
    [Serializable]
    public sealed class HeatGauge
    {
        public const float Max = 100f;
        [SerializeField] float value = 20f;
        float idle;

        public float Value => value;
        public bool Full => value >= Max - 1e-3f;
        public event Action<float, float> Changed;

        /// 전투 시작(08 3-7: 20)
        public void Begin(float start) { Set(start); idle = 0f; }

        /// 기세 더하기/빼기. touch = 때리거나 맞은 일(내림 시계 다시)
        public void Add(float v, bool touch = true)
        {
            if (touch) idle = 0f;
            Set(value + v);
        }

        public void Set(float v)
        {
            float old = value;
            value = Mathf.Clamp(v, 0f, Max);
            if (!Mathf.Approximately(old, value)) Changed?.Invoke(old, value);
        }

        public void Tick(float dt, CombatTuning t)
        {
            if (dt <= 0f) return;
            idle += dt;
            if (idle > t.HeatIdle && value > 0f) Set(value - t.HeatDecay * dt);
        }
    }
}
