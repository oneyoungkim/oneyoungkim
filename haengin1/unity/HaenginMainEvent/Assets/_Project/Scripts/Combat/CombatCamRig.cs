// 행인1의 메인이벤트 — 전투 카메라 운영 (docs/08_M2_전투_설계.md 6장)
// CM_Combat(OrbitalFollow Sphere + RotationComposer + Deoccluder + TraumaShake) 옆에 붙어 CombatPivot 과 궤도 축을 직접 움직인다.
//   락온: 보는 점 = 시우 CamTarget(1.40) → 대상 가슴(1.25) 35%, 궤도 yaw = (시우→대상 yaw) − 옆 18°(오른쪽 어깨 = 시우 화면 왼쪽),
//         피치 10°, 반지름 clamp(3.6 + 0.6·거리, 4.2, 6.5), FOV 45, yaw 따라가기 최대 150°/s · 각가속도 400°/s² · 0.25초 지연.
//   다수(락온 안 함): 자유 궤도(마우스·오른스틱) 반지름 5.0 · 피치 14° · FOV 47, 손댄 뒤 1.5초 지나면 위협 중심 쪽으로 최대 40°/s.
//   옆 바꾸기(6-5): 락온 중 Deoccluder 당김으로 거리 < 원래 65% 가 0.4초 넘게 · 또는 시우–대상 선이 가려지면 옆 18° ↔ −18°(0.5초, 다시 넘기기까지 1.5초).
// 줌 펀치·흔들림은 TraumaShake 확장. 시간은 실제 시간(히트스톱 중에도 카메라는 돈다).
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Haengin
{
    [DefaultExecutionOrder(11), DisallowMultipleComponent]
    public sealed class CombatCamRig : MonoBehaviour
    {
        public const int LivePriority = 20;

        public CinemachineCamera Cam;
        public CinemachineOrbitalFollow Orbit;
        public CinemachineDeoccluder Occ;
        public Transform Pivot;
        public PlayerMotor Motor;
        public Transform CamTarget;
        public LockOn Lock;
        public CamTuning CamT;
        public CombatTuning Tuning;
        public InputActionAsset Actions;

        [Header("6-2 락온")]
        public float PivotToTarget = 0.35f, SideDeg = 18f, LockPitch = 10f, LockFov = 45f, RadiusBase = 3.6f, RadiusPerM = 0.6f, RadiusMin = 4.2f, RadiusMax = 6.5f;
        public float YawMaxSpeed = 150f, YawAccel = 400f, YawDelay = 0.25f, TargetChest = 1.25f;
        [Tooltip("보는 점을 이만큼 내림(m) — 6m 떨어진 락온에서 시우 발이 화면 아래 10% 밖으로 나가서(C17①, 08 6-2 구현 메모)")] public float PivotDrop = 0.25f;
        [Header("6-3 다수")]
        public float FreeRadius = 5.0f, FreePitch = 14f, FreeFov = 47f, AlignWait = 1.5f, AlignSpeed = 40f;
        [Header("6-5 옆 바꾸기")]
        public float PullRatio = 0.65f, PullHold = 0.4f, SideTime = 0.5f, SideLock = 1.5f;

        InputAction lookPad, lookMouse;
        float yawVel, radVel, pitchVel, fovVel, sinceManual = 99f, pulledFor, sideLockLeft;
        float side = 1f, sideNow = 1f;
        readonly Queue<(double t, float yaw)> hist = new Queue<(double, float)>();
        bool wasLocked;

        public CombatTuning T => Tuning != null ? Tuning : CombatTuning.Default;
        public bool Live => Cam != null && Cam.Priority.Value >= LivePriority;
        public float Yaw => Orbit != null ? Orbit.HorizontalAxis.Value : 0f;
        public float Side => side;
        public int SideSwaps { get; private set; }
        public Vector3 CameraPosition => Cam != null ? Cam.State.GetFinalPosition() : transform.position;

        void Awake()
        {
            if (Cam == null) Cam = GetComponent<CinemachineCamera>();
            if (Orbit == null) Orbit = GetComponent<CinemachineOrbitalFollow>();
            if (Occ == null) Occ = GetComponent<CinemachineDeoccluder>();
        }

        void Bind()
        {
            if (lookPad != null || Actions == null) return;
            lookPad = Actions.FindAction(PInput.CombatMap + "/LookPad", false);
            lookMouse = Actions.FindAction(PInput.CombatMap + "/LookMouse", false);
        }

        /// 켜기(우선순위 20)/끄기(0). 켤 때 궤도를 시우 등 뒤로
        public void Activate(bool on)
        {
            if (Cam == null) return;
            Cam.Priority = on ? LivePriority : 0;
            if (on) Snap();
        }

        /// 즉시 지금 구도로(감쇠 없이)
        public void Snap()
        {
            if (Orbit == null || Motor == null) return;
            UpdatePivot(true);
            float yaw = Lock != null && Lock.Target != null ? LockYaw() : Motor.Yaw;
            Orbit.HorizontalAxis.Value = Mathf.DeltaAngle(0f, yaw);
            Orbit.VerticalAxis.Value = Lock != null && Lock.Target != null ? LockPitch : FreePitch;
            Orbit.Radius = Lock != null && Lock.Target != null ? LockRadius() : FreeRadius;
            yawVel = radVel = pitchVel = fovVel = 0f;
            hist.Clear();
            Cam.PreviousStateIsValid = false;
        }

        /// 스틱 → 월드(락온 안 했을 때 = 궤도 정면 기준)
        public Vector3 StickToWorld(Vector2 s) => Quaternion.Euler(0f, Yaw, 0f) * new Vector3(s.x, 0f, s.y);

        float LockYaw()
        {
            var to = HitResolver.Flat(Lock.Target.Position - Motor.Position);
            return HitResolver.Yaw(to) - sideNow * SideDeg;
        }

        float LockRadius()
        {
            float d = HitResolver.Flat(Lock.Target.Position - Motor.Position).magnitude;
            return Mathf.Clamp(RadiusBase + RadiusPerM * d, RadiusMin, RadiusMax);
        }

        void UpdatePivot(bool snap)
        {
            if (Pivot == null || CamTarget == null) return;
            var p = CamTarget.position;
            if (Lock != null && Lock.Target != null)
                p = Vector3.Lerp(p, Lock.Target.Position + Vector3.up * TargetChest, PivotToTarget) - Vector3.up * PivotDrop;
            Pivot.position = p;
        }

        /// 마우스·스틱으로 돌림(락온 안 했을 때)
        public void Rotate(float dYaw, float dPitch)
        {
            if (Orbit == null) return;
            sinceManual = 0f;
            yawVel = 0f;
            Orbit.HorizontalAxis.Value = Mathf.DeltaAngle(0f, Orbit.HorizontalAxis.Value + dYaw);
            Orbit.VerticalAxis.Value = Mathf.Clamp(Orbit.VerticalAxis.Value + dPitch, Orbit.VerticalAxis.Range.x, Orbit.VerticalAxis.Range.y);
        }

        void Update()
        {
            if (Orbit == null || Motor == null || Cam == null) return;
            Bind();
            float dt = TimeFx.RealDt > 0f ? TimeFx.RealDt : Time.unscaledDeltaTime;
            if (dt <= 0f || TimeFx.Paused) return;
            bool locked = Lock != null && Lock.Target != null;
            UpdatePivot(false);
            sinceManual += dt;
            sideLockLeft = Mathf.Max(0f, sideLockLeft - dt);
            sideNow = Mathf.MoveTowards(sideNow, side, dt * 2f / Mathf.Max(0.05f, SideTime));

            var lens = Cam.Lens;
            float wantFov, wantRad, wantPitch;
            if (locked)
            {
                wantFov = LockFov; wantRad = LockRadius(); wantPitch = LockPitch;
                // 지연된 목표 yaw 를 각속도·각가속도 제한으로 따라감
                double now = TimeFx.Real;
                hist.Enqueue((now, LockYaw()));
                float delayed = hist.Peek().yaw;
                while (hist.Count > 1 && now - hist.Peek().t > YawDelay) delayed = hist.Dequeue().yaw;
                if (!wasLocked) { hist.Clear(); hist.Enqueue((now, LockYaw())); delayed = LockYaw(); }
                FollowYaw(delayed, YawMaxSpeed, YawAccel, dt);
                SideCheck(dt);
            }
            else
            {
                wantFov = FreeFov; wantRad = FreeRadius; wantPitch = Orbit.VerticalAxis.Value;
                hist.Clear();
                // 손으로 돌리기
                if (Live && !TimeFx.Paused)
                {
                    var c = CamT != null ? CamT : null;
                    Vector2 ms = lookMouse != null && lookMouse.enabled ? lookMouse.ReadValue<Vector2>() : Vector2.zero;
                    Vector2 st = lookPad != null && lookPad.enabled ? lookPad.ReadValue<Vector2>() : Vector2.zero;
                    float my = c != null ? c.mouseYaw : 0.08f, mp = c != null ? c.mousePitch : 0.07f, py = c != null ? c.padYawSpeed : 150f, pp = c != null ? c.padPitchSpeed : 95f;
                    float yaw = ms.x * my + Mathf.Sign(st.x) * Mathf.Pow(Mathf.Abs(st.x), 1.6f) * py * dt;
                    float pitch = -ms.y * mp - Mathf.Sign(st.y) * Mathf.Pow(Mathf.Abs(st.y), 1.6f) * pp * dt;
                    if (Mathf.Abs(yaw) > 1e-4f || Mathf.Abs(pitch) > 1e-4f) Rotate(yaw, pitch);
                }
                // 위협 중심 쪽 자동 정렬(손댄 뒤 1.5초)
                if (sinceManual >= AlignWait && ThreatCenter(out var center))
                {
                    var to = HitResolver.Flat(center - Motor.Position);
                    if (to.sqrMagnitude > 0.25f) FollowYaw(HitResolver.Yaw(to), AlignSpeed, AlignSpeed * 4f, dt);
                    else yawVel = 0f;
                }
                else if (sinceManual < AlignWait) yawVel = 0f;
            }
            wasLocked = locked;
            Orbit.Radius = Mathf.SmoothDamp(Orbit.Radius, wantRad, ref radVel, 0.25f, Mathf.Infinity, dt);
            Orbit.VerticalAxis.Value = Mathf.SmoothDamp(Orbit.VerticalAxis.Value, wantPitch, ref pitchVel, 0.3f, Mathf.Infinity, dt);
            lens.FieldOfView = Mathf.SmoothDamp(lens.FieldOfView, wantFov, ref fovVel, 0.3f, Mathf.Infinity, dt);
            Cam.Lens = lens;
        }

        void FollowYaw(float goal, float maxSpeed, float accel, float dt)
        {
            float cur = Orbit.HorizontalAxis.Value;
            float diff = Mathf.DeltaAngle(cur, goal);
            // 남은 각도에서 멈출 수 있는 속도(v² = 2·a·d) 와 최대 속도 중 작은 것
            float want = Mathf.Sign(diff) * Mathf.Min(maxSpeed, Mathf.Sqrt(2f * accel * Mathf.Abs(diff)));
            yawVel = Mathf.MoveTowards(yawVel, want, accel * dt);
            float step = yawVel * dt;
            if (Mathf.Abs(step) >= Mathf.Abs(diff) && Mathf.Sign(step) == Mathf.Sign(diff)) { step = diff; yawVel = 0f; }
            Orbit.HorizontalAxis.Value = Mathf.DeltaAngle(0f, cur + step);
        }

        /// 위협 중심(6-3): 공격권 가진 적 무게 2(7단계), 나머지 1, 다운·탈락 0
        public bool ThreatCenter(out Vector3 c)
        {
            c = Vector3.zero;
            float w = 0f;
            var me = Motor != null ? Motor.GetComponent<Fighter>() : null;
            foreach (var f in Fighter.All)
            {
                if (f == null || f == me || (me != null && f.Team == me.Team) || !f.Targetable) continue;
                if (HitResolver.Flat(f.Position - Motor.Position).magnitude > 12f) continue;
                float k = f.Telegraphing ? 2f : 1f;
                c += f.Position * k;
                w += k;
            }
            if (w <= 0f) return false;
            c /= w;
            return true;
        }

        void SideCheck(float dt)
        {
            if (Pivot == null) return;
            float dist = Vector3.Distance(Cam.State.GetFinalPosition(), Pivot.position);
            bool pulled = Orbit.Radius > 0.5f && dist < Orbit.Radius * PullRatio;
            bool hidden = Lock.Target != null && Physics.Linecast(Cam.State.GetFinalPosition(), Lock.Target.Chest, 1 << Layers.Wall, QueryTriggerInteraction.Ignore);
            pulledFor = pulled || hidden ? pulledFor + dt : 0f;
            if (pulledFor > PullHold && sideLockLeft <= 0f)
            {
                side = -side;
                sideLockLeft = SideLock;
                pulledFor = 0f;
                SideSwaps++;
            }
        }
    }
}
