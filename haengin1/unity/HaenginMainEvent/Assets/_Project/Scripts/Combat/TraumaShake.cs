// 행인1의 메인이벤트 — 카메라 흔들림·줌 펀치 (docs/08_M2_전투_설계.md 5-3·5-4 — 시안 05_fx.js CamRig 식 그대로)
// Cinemachine 확장(Noise 단계 = Deoccluder 다음, CamClearance 앞). 상태는 Shake(정적): 트라우마 누적 → 세기 t² → 위치·보는 점·기울기 흔들림,
// 줌 펀치 = 보는 점 쪽으로 거리 × 0.10·p. 실제 시간으로 돈다(TimeFx: 히트스톱 중에도 흔들림·카메라는 멈추지 않음).
// 08 5-3 의 Impulse + 2차 노이즈 대신 문서에 적힌 대안(시안 식을 옮긴 확장)을 바로 썼다 — 이유는 08 5-3 '구현 메모'.
using Unity.Cinemachine;
using UnityEngine;

namespace Haengin
{
    [AddComponentMenu("Haengin/Trauma Shake")]
    public sealed class TraumaShake : CinemachineExtension
    {
        /// 이번 프레임 넣은 흔들림(카메라 기준 가로·세로 m) — 테스트가 읽음
        public Vector2 LastPos { get; private set; }
        public float LastRoll { get; private set; }
        public float LastPunch { get; private set; }
        /// 이번 흔들림을 계산한 흔들림 시계(Shake.Clock, 실제 시간 초)
        public double LastClock { get; private set; }

        protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage,
                                                          ref CameraState state, float deltaTime)
        {
            if (stage != CinemachineCore.Stage.Noise) return;
            LastPos = Vector2.zero; LastRoll = 0f; LastPunch = 0f;
            LastClock = Shake.Clock;
            var rot = state.GetCorrectedOrientation();
            var pos = state.GetCorrectedPosition();

            // 줌 펀치: 보는 점 쪽으로 거리 × Amount × p
            float p = Shake.Punch;
            if (p > 1e-4f)
            {
                var look = state.HasLookAt() ? state.ReferenceLookAt : pos + rot * Vector3.forward * 4f;
                var to = look - pos;
                float amount = Shake.PunchAmount * p;
                state.PositionCorrection += to * amount;
                LastPunch = to.magnitude * amount;
            }

            float s = Shake.Strength;
            if (s > 1e-7f)
            {
                var sp = Shake.Pos;
                state.PositionCorrection += rot * new Vector3(sp.x, sp.y, 0f);
                // 보는 점 가로 흔들림 → 그 거리에서의 yaw, 기울기 → roll
                var look = state.HasLookAt() ? state.ReferenceLookAt : pos + rot * Vector3.forward * 4f;
                float d = Mathf.Max(0.5f, Vector3.Distance(look, pos));
                float yaw = Mathf.Atan2(Shake.Look, d) * Mathf.Rad2Deg;
                float roll = Shake.Roll * Mathf.Rad2Deg;
                state.OrientationCorrection = state.OrientationCorrection * Quaternion.Euler(0f, yaw, roll);
                LastPos = sp;
                LastRoll = roll;
            }
        }
    }
}
