// 행인1의 메인이벤트 — 국물 게이지(docs/09 2-6): 100 에서 시작, 흔들림·급정거·턱에 줄어든다. 5-2 케이크 운반은 이름만 '생크림 게이지'
//   턱 0.15m 넘는 충격 −8 · 벽·사람 부딪힘 −15 · 옆 가속 3 m/s² 넘으면 초당 −10 · 내리막 6 m/s 넘으면 초당 −4 · 급브레이크 −5 · 헛발 −2
//   Floor(1화 = 30, 교습): 그 아래로 안 내려감. 출렁임(Slosh 0~1) = 카메라 흔들림·배달통 흔들기에 씀(게이지가 낮을수록 크게)
using System;
using UnityEngine;

namespace Haengin
{
    [DisallowMultipleComponent]
    public sealed class SoupGauge : MonoBehaviour
    {
        public const float Max = 100f;
        public float Value = Max;
        [Tooltip("이 아래로 안 내려감(1화 교습 30)")] public float Floor = 0f;
        public string Label = "국물";
        public float BumpCost = 8f, HitCost = 15f, LateralLimit = 3f, LateralPerSec = 10f, DownLimit = 6f, DownPerSec = 4f, BrakeCost = 5f, WhiffCost = 2f;

        /// 출렁임 0~1(사건마다 튀고 0.6초에 가라앉음)
        public float Slosh { get; private set; }
        public int Events { get; private set; }
        public float Lost { get; private set; }
        public string LastCause { get; private set; } = "";
        public event Action<string, float> Spilled;

        public void ResetTo(float v, float floor, string label)
        {
            Value = Mathf.Clamp(v, 0f, Max); Floor = floor; Label = label; Slosh = 0f; Events = 0; Lost = 0f; LastCause = "";
        }

        void Take(float amount, string cause, float slosh)
        {
            if (amount <= 0f) return;
            float before = Value;
            Value = Mathf.Max(Floor, Value - amount);
            Lost += before - Value;
            Slosh = Mathf.Min(1f, Slosh + slosh);
            Events++;
            LastCause = cause;
            Spilled?.Invoke(cause, before - Value);
        }

        public void Bump(float height) { if (height >= 0.15f) Take(BumpCost, "턱", 0.6f); }
        public void Hit() => Take(HitCost, "부딪힘", 1f);
        public void HardBrake() => Take(BrakeCost, "급브레이크", 0.5f);
        public void Whiff() => Take(WhiffCost, "헛발", 0.25f);

        /// 매 프레임: 옆 가속(m/s²)·내리막 속도(m/s, 내리막이 아니면 0)
        public void Tick(float dt, float lateral, float downSpeed)
        {
            if (lateral > LateralLimit) { float before = Value; Value = Mathf.Max(Floor, Value - LateralPerSec * dt); Lost += before - Value; Slosh = Mathf.Min(1f, Slosh + dt * 1.5f); LastCause = "옆 흔들림"; }
            if (downSpeed > DownLimit) { float before = Value; Value = Mathf.Max(Floor, Value - DownPerSec * dt); Lost += before - Value; Slosh = Mathf.Min(1f, Slosh + dt); LastCause = "내리막 과속"; }
            Slosh = Mathf.MoveTowards(Slosh, 0f, dt / 0.6f);
        }

        /// 카메라 흔들림 세기(게이지가 낮을수록 출렁임이 크다)
        public float Shake => Slosh * Mathf.Lerp(0.6f, 1.4f, 1f - Value / Max);
    }
}
