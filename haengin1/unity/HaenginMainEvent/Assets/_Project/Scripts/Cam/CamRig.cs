// 행인1의 메인이벤트 — 탐험 카메라 운영 (docs/07_M1_조작_설계.md 4장·7-6)
// CM_Explore(OrbitalFollow + RotationComposer + Deoccluder) 옆에 붙어서: 달리기 FOV·거리, 걷기·달리기 자동 정렬(각속도 제한, 4-8), Q/L1 등 뒤 정렬,
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
            yawVel = pitchVel = 0f;
            sinceManual = 999f;
            movingFor = 0f;
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

            AutoAlign(dt);
        }

        // ───────────────────────── 자동 정렬 (07 4-8)
        // Cinemachine 의 정렬(Recentering, SmoothDamp)은 최고 각속도가 각도 차에 비례한다(Time 2초: 차이 150° 면 최고 약 110°/s) → 꺾이는 골목에서 출렁인다.
        // 그래서 탐험 자동 정렬은 우리가 직접: 최대 각속도(걷기 30°/s · 달리기 60°/s) + 각가속도 제한 + 가까워질수록 느리게.
        // Cinemachine 정렬은 Wait 를 늘 크게 두고 Q/L1(TriggerRecentering)에만 쓴다. Enabled 는 켜 둬야 Center 가 인물 정면으로 갱신된다.
        public const string PrefKey = "haengin.cam.autoAlign";
        bool modeSet;
        CamTuning.Auto mode;
        float sinceManual = 999f, movingFor, yawVel, pitchVel;

        /// 지금 자동 정렬 방식(일시정지 메뉴 옵션). 처음 값 = 조정값(빌드에선 저장한 선택이 있으면 그것)
        public CamTuning.Auto AutoMode
        {
            get
            {
                if (!modeSet)
                {
                    modeSet = true;
                    mode = T.autoRecenter;
                    if (!Application.isBatchMode && PlayerPrefs.HasKey(PrefKey)) mode = (CamTuning.Auto)Mathf.Clamp(PlayerPrefs.GetInt(PrefKey), 0, 2);
                }
                return mode;
            }
        }

        /// 메뉴에서 바꿀 때 save = true(PlayerPrefs 에 남김 — 배치 실행·테스트에서는 안 남김)
        public void SetAutoMode(CamTuning.Auto m, bool save)
        {
            modeSet = true;
            mode = m;
            yawVel = pitchVel = 0f;
            if (save && !Application.isBatchMode) { PlayerPrefs.SetInt(PrefKey, (int)m); PlayerPrefs.Save(); }
        }

        /// 마우스·오른스틱으로 시점을 돌렸다(CamInput 이 부름). 자동 정렬은 이때부터 다시 기다린다.
        public void NoteManualLook()
        {
            sinceManual = 0f;
            yawVel = pitchVel = 0f;
        }

        /// 마지막 수동 시점 조작 뒤 지난 시간(초)
        public float SinceManualLook => sinceManual;
        /// 자동 정렬 가로 각속도(°/s, + = 시계 방향). 0 이면 안 돌리는 중
        public float AlignRate => yawVel;
        public float PitchAlignRate => pitchVel;

        void AutoAlign(float dt)
        {
            Orbit.HorizontalAxis.Recentering.Wait = 9999f;
            Orbit.VerticalAxis.Recentering.Wait = 9999f;
            sinceManual += dt;
            bool moving = Motor.CommandSpeed > 0.3f && Motor.State != MoveState.Fall;
            movingFor = moving ? movingFor + dt : 0f;

            // Q/L1 정렬 중에는 손대지 않는다(축 값을 쓰면 Cinemachine 이 입력으로 보고 강제 정렬을 취소)
            if (snapTimer > 0f)
            {
                yawVel = pitchVel = 0f;
                if ((snapTimer -= dt) <= 0f)
                {
                    Orbit.HorizontalAxis.Recentering.Time = T.recenterTime;
                    Orbit.VerticalAxis.Recentering.Time = T.recenterTime;
                }
                return;
            }

            var m = AutoMode;
            bool running = Motor.State == MoveState.Run;
            var M = Motor.T;

            // 가로: 인물 정면(몸 방향)으로
            float yaw = Orbit.HorizontalAxis.Value;
            float diff = Mathf.DeltaAngle(yaw, Motor.Yaw);
            bool allowH = m == CamTuning.Auto.Always ? moving : m == CamTuning.Auto.RunOnly && running;
            float want = 0f;
            if (allowH && sinceManual >= (running ? T.recenterWait : T.walkRecenterWait) && movingFor >= T.alignStartDelay
                && Mathf.Abs(diff) < T.alignBackAngle && Mathf.Abs(diff) > 0.3f)
            {
                // 패드를 살짝 기울인 느린 걷기는 그만큼 천천히
                float cap = running ? T.runAlignSpeed : T.walkAlignSpeed * Mathf.Clamp01(Motor.CommandSpeed / Mathf.Max(0.1f, M.walkSpeed));
                want = Mathf.Clamp(diff * T.alignGain, -cap, cap);
            }
            float acc = T.alignAccel * (running ? 2f : 1f);
            yawVel = Mathf.MoveTowards(yawVel, want, acc * dt);
            if (Mathf.Abs(yawVel) > 1e-3f)
            {
                float step = yawVel * dt;
                if (Mathf.Sign(step) == Mathf.Sign(diff) && Mathf.Abs(step) >= Mathf.Abs(diff)) { step = diff; yawVel = 0f; }   // 지나치지 않게
                Orbit.HorizontalAxis.Value = Mathf.DeltaAngle(0f, yaw + step);
            }

            // 세로: 움직이는 중 수동 조작 없이 pitchRecenterWait 가 지나면 기본 피치로(자동 정렬을 끄면 안 함)
            float pitch = Orbit.VerticalAxis.Value;
            float pd = T.pitchDefault - pitch;
            float wantP = 0f;
            if (m != CamTuning.Auto.Off && moving && sinceManual >= T.pitchRecenterWait && Mathf.Abs(pd) > 0.2f)
                wantP = Mathf.Clamp(pd * T.alignGain, -T.pitchAlignSpeed, T.pitchAlignSpeed);
            pitchVel = Mathf.MoveTowards(pitchVel, wantP, T.alignAccel * dt);
            if (Mathf.Abs(pitchVel) > 1e-3f)
            {
                float step = pitchVel * dt;
                if (Mathf.Sign(step) == Mathf.Sign(pd) && Mathf.Abs(step) >= Mathf.Abs(pd)) { step = pd; pitchVel = 0f; }
                Orbit.VerticalAxis.Value = Mathf.Clamp(pitch + step, Orbit.VerticalAxis.Range.x, Orbit.VerticalAxis.Range.y);
            }
        }
    }
}
