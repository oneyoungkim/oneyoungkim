// 행인1의 메인이벤트 — 배달 자전거 모습(docs/09 2-6 '모습', 8단계): 소품 delivery_bicycle.glb 를 tools/bike_split.py 로 나눈 FBX
//   Frame(차체·바구니·배달통·타이어·흙받이) · WheelF/WheelR(바퀴살·허브 — 축 원점) · Crank(크랭크·페달 — BB 원점, PedalL·PedalR 빈 점)
//   빈 점 Saddle · GripL · GripR · FootDown(정차 때 왼발 땅 자리)
// 바퀴 = 속도 ÷ 굴림 반지름(앞 0.309·뒤 0.298m)만큼 돌고, 크랭크 = BikeMotor.Crank, 기울기 = 조향 × 속도(최대 12°) + 일어서기 박자마다 좌우 8°.
// 기울기는 플레이어 Visual 을 돌린다(자전거와 시우가 같이 기울게 — 타는 동안 BodyLean 은 꺼짐).
using UnityEngine;

namespace Haengin
{
    [DefaultExecutionOrder(40), DisallowMultipleComponent]
    public sealed class BikeVisual : MonoBehaviour
    {
        public BikeMotor Motor;
        public Transform WheelF, WheelR, Crank, PedalL, PedalR, Saddle, GripL, GripR, FootDown;
        public float RadiusF = 0.309f, RadiusR = 0.298f, LeanMax = 12f, SwayDeg = 8f;
        /// 기울일 대상(플레이어 Visual)
        public Transform LeanRoot;
        public float Lean { get; private set; }
        public float Sway { get; private set; }
        public float WheelTurns { get; private set; }

        float spinF, spinR, swayTarget;
        bool hooked;

        void OnEnable() => Hook(true);
        void OnDisable() => Hook(false);

        void Hook(bool on)
        {
            if (Motor == null) return;
            if (on && !hooked) { Motor.Beat += OnBeat; hooked = true; }
            else if (!on && hooked) { Motor.Beat -= OnBeat; hooked = false; }
        }

        void OnBeat(bool ok) { if (ok) swayTarget = Motor.LastBeatRight ? SwayDeg : -SwayDeg; }

        public void Bind(BikeMotor m, Transform leanRoot)
        {
            Hook(false);
            Motor = m; LeanRoot = leanRoot;
            if (isActiveAndEnabled) Hook(true);
        }

        void LateUpdate()
        {
            if (Motor == null) return;
            float dt = Time.deltaTime;
            float v = Motor.Mounted ? Motor.Speed : 0f;
            spinF = Mathf.Repeat(spinF + v / RadiusF * Mathf.Rad2Deg * dt, 360f);
            spinR = Mathf.Repeat(spinR + v / RadiusR * Mathf.Rad2Deg * dt, 360f);
            WheelTurns += v / (2f * Mathf.PI * RadiusR) * dt;
            if (WheelF != null) WheelF.localRotation = Quaternion.Euler(spinF, 0f, 0f);
            if (WheelR != null) WheelR.localRotation = Quaternion.Euler(spinR, 0f, 0f);
            if (Crank != null) Crank.localRotation = Quaternion.Euler(Motor.Crank, 0f, 0f);
            // 기울기: 도는 쪽으로(속도가 있을 때), 일어서기 박자 좌우 흔들기
            float want = Motor.Mounted ? -Motor.Steer * LeanMax * Mathf.Clamp01(v / 3f) : 0f;
            Lean = Mathf.MoveTowards(Lean, want, 40f * dt);
            swayTarget = Mathf.MoveTowards(swayTarget, 0f, 24f * dt);
            Sway = Mathf.MoveTowards(Sway, swayTarget, 60f * dt);
            if (LeanRoot != null && Motor.Mounted) LeanRoot.localRotation = Quaternion.Euler(0f, 0f, Lean + Sway);
        }
    }
}
