// 행인1의 메인이벤트 — 정적 모델(리깅 전) 몸 기울기·턱 보정·가까울 때 숨김 (docs/07_M1_조작_설계.md 5-2·5-3·4-3)
// Player/Visual 에 붙는다. Visual 로컬 원점 = 발밑. 애니메이션이 없으니 "미끄러지듯 + 살짝 기울기"만.
using UnityEngine;
using UnityEngine.Rendering;

namespace Haengin
{
    [DefaultExecutionOrder(100)]
    public sealed class BodyLean : MonoBehaviour
    {
        public PlayerMotor Motor;
        public CamRig Cam;

        float pitch, roll, pitchVel, rollVel, yOff;
        bool hidden;
        Renderer[] rends;

        public float Pitch => pitch;
        public float Roll => roll;
        public bool Hidden => hidden;

        void Awake()
        {
            if (Motor == null) Motor = GetComponentInParent<PlayerMotor>();
            rends = GetComponentsInChildren<Renderer>(true);
        }

        void Start()
        {
            if (Cam == null) Cam = FindAnyObjectByType<CamRig>();
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || Motor == null) return;
            var t = Motor.T;

            // 앞 기울기 = 속도 + 가속(출발 때 앞으로, 멈출 때 뒤로)
            var fwd = Motor.transform.forward;
            float tp = Mathf.Clamp(t.leanBase * Motor.CommandSpeed / t.runSpeed + Vector3.Dot(Motor.PlanarAccel, fwd) * t.leanPerAccel,
                                   -t.leanMaxBack, t.leanMaxFwd);
            // 회전 쪽 기울기: 오른쪽으로 돌면(YawRate +) 머리가 오른쪽(roll −)
            float tr = Mathf.Clamp(-Motor.YawRate * Motor.CommandSpeed * t.bankK, -t.bankMax, t.bankMax);
            if (!Motor.Grounded) { tp = 0f; tr = 0f; }
            pitch = Mathf.SmoothDamp(pitch, tp, ref pitchVel, t.leanSmooth, Mathf.Infinity, dt);
            roll = Mathf.SmoothDamp(roll, tr, ref rollVel, t.bankSmooth, Mathf.Infinity, dt);
            transform.localRotation = Quaternion.Euler(pitch, 0f, roll);

            // 턱 보정: 한 프레임에 오른(내린) 높이를 비주얼에서 되돌려 두고 0.08초 시간상수로 따라간다
            if (Motor.StepDeltaY != 0f) yOff -= Motor.StepDeltaY;
            yOff = Mathf.Clamp(yOff * Mathf.Exp(-dt / Mathf.Max(0.01f, t.stepSmooth)), -0.4f, 0.4f);
            transform.localPosition = new Vector3(0f, yOff, 0f);

            // 카메라가 너무 가까우면 숨김(그림자는 남김)
            if (Cam != null && Cam.T != null)
            {
                float d = Cam.Distance;
                bool h = hidden ? d < Cam.T.showDist : d < Cam.T.hideDist;
                if (h != hidden)
                {
                    hidden = h;
                    foreach (var r in rends)
                        if (r != null) r.shadowCastingMode = h ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
                }
            }
        }
    }
}
