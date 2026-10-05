// 행인1의 메인이벤트 — 시우 이동 애니메이션 구동 (docs/07_M1_조작_설계.md 5-4)
// 시우 모델(Animator, 컨트롤러 Anim/Siwoo.controller)에 붙는다. PlayerMotor 의 실제 수평 속도로 블렌드 트리 대기(0)→걷기(1)→달리기(2)를 고르고,
// 상태 재생 속도(Rate)로 걸음 빠르기를 실제 속도에 맞춰 디딤발이 미끄러지지 않게 한다.
//   속도 0 ~ 가장 느린 걷기(0.5): 대기 → 걷기 섞기 (멈추고 출발하는 순간뿐 — 가감속이 0.1초 안팎)
//   0.5 ~ 1.6 m/s: 걷기만, 재생 속도 = 속도 ÷ 1.6 (패드를 살짝 기울인 느린 걷기도 디딤발이 땅에 붙음)
//   1.6 ~ 4.5 m/s: 걷기 → 달리기 섞기(재생 속도 1). 걷기·달리기 한 주기 길이가 비슷하고(0.59·0.66초) 왼발 위상을 맞춰 둬서 다리가 엇갈리지 않는다
//   4.5 넘으면 달리기를 빠르게
// 클립 고유 속도에 맞춘 재생 배율(걷기 ×2.36·달리기 ×1.11)은 컨트롤러의 블렌드 트리 자식 timeScale 에 들어 있다(에디터 CharSetup 이 측정해서 넣음).
using UnityEngine;

namespace Haengin
{
    [DefaultExecutionOrder(20), RequireComponent(typeof(Animator))]
    public sealed class LocoAnim : MonoBehaviour
    {
        public enum Gait { Idle, Walk, Run }

        public PlayerMotor Motor;
        [Tooltip("속도 변화를 이 시간(초)으로 부드럽게 — 턱·벽에서 한 프레임 튀는 속도에 다리가 떨지 않게")]
        public float SpeedSmooth = 0.05f;

        public static readonly int SpeedId = Animator.StringToHash("Speed");
        public static readonly int BlendId = Animator.StringToHash("Blend");
        public static readonly int RateId = Animator.StringToHash("Rate");

        Animator anim;
        float v, vVel;

        /// 블렌드 값(0 대기 · 1 걷기 · 2 달리기)
        public float Blend { get; private set; }
        /// 상태 재생 속도 배율
        public float Rate { get; private set; } = 1f;
        /// 애니메이터에 넣은 속도(m/s, 부드럽게 한 값)
        public float Speed => v;
        public Animator Animator => anim != null ? anim : anim = GetComponent<Animator>();
        /// 지금 가장 많이 섞인 동작
        public Gait Dominant => Blend < 0.5f ? Gait.Idle : Blend < 1.5f ? Gait.Walk : Gait.Run;

        void Awake()
        {
            anim = GetComponent<Animator>();
            anim.applyRootMotion = false;   // 이동은 PlayerMotor 가 한다(클립은 제자리 동작)
            if (Motor == null) Motor = GetComponentInParent<PlayerMotor>();
        }

        void OnEnable()
        {
            if (Motor != null) Motor.Teleported += OnTeleported;
        }

        void OnDisable()
        {
            if (Motor != null) Motor.Teleported -= OnTeleported;
        }

        void OnTeleported(Vector3 _) { v = vVel = 0f; }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || Motor == null || anim == null) return;
            var t = Motor.T;
            v = Mathf.SmoothDamp(v, Motor.PlanarSpeed, ref vVel, SpeedSmooth, Mathf.Infinity, dt);
            if (v < 0.01f) v = 0f;
            Evaluate(v, t.minWalkSpeed, t.walkSpeed, t.runSpeed, out float b, out float r);
            Blend = b;
            Rate = r;
            anim.SetFloat(SpeedId, v);
            anim.SetFloat(BlendId, b);
            anim.SetFloat(RateId, r);
        }

        /// 속도(m/s) → 블렌드 값·재생 속도 (테스트도 같은 계산을 쓴다)
        public static void Evaluate(float v, float walkMin, float walk, float run, out float blend, out float rate)
        {
            walkMin = Mathf.Clamp(walkMin, 0.05f, walk);
            if (v <= walkMin)
            {
                blend = v / walkMin;
                rate = Mathf.Lerp(1f, walkMin / walk, blend);
            }
            else if (v <= walk)
            {
                blend = 1f;
                rate = v / walk;
            }
            else if (v <= run)
            {
                blend = 1f + (v - walk) / (run - walk);
                rate = 1f;
            }
            else
            {
                blend = 2f;
                rate = v / run;
            }
        }
    }
}
