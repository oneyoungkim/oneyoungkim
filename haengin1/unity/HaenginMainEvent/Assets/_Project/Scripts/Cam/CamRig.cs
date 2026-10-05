// 행인1의 메인이벤트 — 탐험 카메라 운영 (docs/07_M1_조작_설계.md 4장·7-6)
// CM_Explore(OrbitalFollow + RotationComposer + Deoccluder) 옆에 붙어서: 달리기 FOV·거리, 자동 정렬 대기값, Q/L1 등 뒤 정렬,
// 이동 기준 방향(궤도 정면). 벽 간격·다리 가림은 CamClearance(Cinemachine 확장)가 맡는다.
using Unity.Cinemachine;
using UnityEngine;

namespace Haengin
{
    [DefaultExecutionOrder(10)]
    public sealed class CamRig : MonoBehaviour
    {
        public CinemachineCamera Cam;
        public CinemachineOrbitalFollow Orbit;
        public CinemachineDeoccluder Occ;
        public PlayerMotor Motor;
        public Transform CamTarget;
        public CamTuning Tuning;

        CamTuning fallback;
        float fovVel, radVel, snapTimer;

        public CamTuning T => Tuning != null ? Tuning : (fallback != null ? fallback : fallback = ScriptableObject.CreateInstance<CamTuning>());

        /// 이동 기준 방향 = 궤도 정면(카메라 정면이 아님 — 화면 구도 오프셋 때문에 빙글 도는 되먹임 방지, 07 4-4)
        public Quaternion MoveBasis => Quaternion.Euler(0f, Orbit != null ? Orbit.HorizontalAxis.Value : 0f, 0f);
        public float Yaw => Orbit != null ? Orbit.HorizontalAxis.Value : 0f;
        public float Pitch => Orbit != null ? Orbit.VerticalAxis.Value : 0f;
        /// 카메라 최종 위치 ↔ 따라가는 점 거리(지난 프레임)
        public float Distance => Cam != null && CamTarget != null ? Vector3.Distance(Cam.State.GetFinalPosition(), CamTarget.position) : 99f;
        public Vector3 CameraPosition => Cam != null ? Cam.State.GetFinalPosition() : transform.position;
        public CamClearance Clearance;
        /// 벽 간격·다리 가림 때문에 당긴 거리(CamClearance, 0 이면 안 당김)
        public float Pull => Clearance != null ? Clearance.Pull : 0f;

        void Awake()
        {
            if (Cam == null) Cam = GetComponent<CinemachineCamera>();
            if (Orbit == null) Orbit = GetComponent<CinemachineOrbitalFollow>();
            if (Occ == null) Occ = GetComponent<CinemachineDeoccluder>();
            if (Clearance == null) Clearance = GetComponent<CamClearance>();
        }

        void Start()
        {
            if (Motor == null) Motor = FindAnyObjectByType<PlayerMotor>();
            if (CamTarget == null && Motor != null) CamTarget = Motor.CamTarget != null ? Motor.CamTarget : Motor.transform;
        }

        /// Q / L1: 0.3초에 등 뒤·기본 피치로
        public void RecenterBehind()
        {
            if (Orbit == null) return;
            Orbit.HorizontalAxis.Recentering.Time = T.snapTime;
            Orbit.VerticalAxis.Recentering.Time = T.snapTime;
            Orbit.HorizontalAxis.TriggerRecentering();
            Orbit.VerticalAxis.TriggerRecentering();
            snapTimer = T.snapTime + 0.2f;
        }

        /// 즉시 등 뒤로(시작·순간 이동·테스트·촬영). 다음 카메라 계산은 감쇠 없이 제자리.
        public void SnapBehind()
        {
            if (Orbit == null || Motor == null) return;
            Orbit.HorizontalAxis.Value = Mathf.DeltaAngle(0f, Motor.Yaw);
            Orbit.HorizontalAxis.Center = Orbit.HorizontalAxis.Value;
            Orbit.VerticalAxis.Value = T.pitchDefault;
            Orbit.Radius = T.radius;
            fovVel = radVel = 0f;
            var lens = Cam.Lens;
            lens.FieldOfView = T.fov;
            Cam.Lens = lens;
            Cam.PreviousStateIsValid = false;
        }

        void Update()
        {
            if (Orbit == null || Cam == null || Motor == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            var M = Motor.T;
            float run01 = Mathf.InverseLerp(M.walkSpeed, M.runSpeed, Motor.PlanarSpeed);

            // 달리기 FOV — 버튼이 아니라 실제 속도에 묶는다(벽에 막혀 못 뛰면 안 바뀜)
            var lens = Cam.Lens;
            float f = Mathf.Lerp(T.fov, T.fovRun, run01);
            lens.FieldOfView = Mathf.SmoothDamp(lens.FieldOfView, f, ref fovVel, f > lens.FieldOfView ? T.fovUpTime : T.fovDownTime, Mathf.Infinity, dt);
            Cam.Lens = lens;

            // 궤도 반지름 = 달리기 거리(벽 간격·다리 가림은 CamClearance 가 파이프라인 끝에서 따로 당긴다)
            float r = Mathf.Lerp(T.radius, T.radiusRun, run01);
            Orbit.Radius = Mathf.SmoothDamp(Orbit.Radius, r, ref radVel, r > Orbit.Radius ? T.fovUpTime : T.fovDownTime, Mathf.Infinity, dt);

            // 자동 정렬: Enabled 는 항상 true(그래야 OrbitalFollow 가 Center 를 인물 정면으로 갱신). 끌 때는 Wait 를 크게.
            bool moving = Motor.CommandSpeed > 0.3f, running = Motor.State == MoveState.Run;
            bool autoH = T.autoRecenter == CamTuning.Auto.Always ? moving : T.autoRecenter == CamTuning.Auto.RunOnly && running;
            Orbit.HorizontalAxis.Recentering.Wait = autoH ? T.recenterWait : 9999f;
            Orbit.VerticalAxis.Recentering.Wait = moving ? T.pitchRecenterWait : 9999f;
            if (snapTimer > 0f && (snapTimer -= dt) <= 0f)
            {
                Orbit.HorizontalAxis.Recentering.Time = T.recenterTime;
                Orbit.VerticalAxis.Recentering.Time = T.recenterTime;
            }
        }
    }
}
