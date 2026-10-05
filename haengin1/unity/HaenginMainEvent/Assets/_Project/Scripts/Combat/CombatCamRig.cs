// 행인1의 메인이벤트 — 전투 카메라 운영 (docs/08_M2_전투_설계.md 6장)
// CM_Combat(OrbitalFollow Sphere + RotationComposer + Deoccluder + TraumaShake) 옆에 붙어 CombatPivot 과 궤도 축을 직접 움직인다.
//   락온: 보는 점 = 시우 CamTarget(1.40) → 대상 가슴(1.25) 35%, 궤도 yaw = (시우→대상 yaw) − 옆 18°(오른쪽 어깨 = 시우 화면 왼쪽),
//         피치 10°, 반지름 clamp(3.6 + 0.6·거리, 4.2, 6.5), FOV 45, yaw 따라가기 최대 150°/s · 각가속도 400°/s² · 0.25초 지연.
//   다수(락온 안 함): 자유 궤도(마우스·오른스틱) 반지름 5.0 · 피치 14° · FOV 47, 손댄 뒤 1.5초 지나면 위협 중심 쪽으로 최대 40°/s.
//   옆 바꾸기(6-5): 락온 중 Deoccluder 당김으로 거리 < 원래 65% 가 0.4초 넘게 · 또는 시우–대상 선이 가려지면 빈 쪽을 고른다(0.5초, 다시 고르기까지 1.5초):
//     옆 각 후보 18° · 40° · 65° · 90° 중 열린(당김 없음 + 대상 안 가림) 가장 작은 각을 옆마다 찾고, 지금 옆을 우선(반대 옆이 40° 넘게 작을 때만 넘김).
//     어느 쪽도 안 열리면 그대로(벽을 등졌을 때 양쪽을 오가지 않게 — 11-4). 넓힌 뒤 더 작은 각이 2초 동안 열려 있으면 줄임.
//     넘어가는 길(지금 yaw → 새 yaw 사이 네 점)이 벽에 막혔으면 돌아가지 않고 컷(카메라가 벽에 붙어 시우 뒤통수를 지나가지 않게).
//     당김 판단은 지금 카메라 거리 + '따라갈 목표 자리가 이미 막혔는지'(지연 0.25초 전에 미리) 둘 다로.
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
        [Tooltip("옆 각 후보(18° 에 더하는 각)")] public float[] SideExtra = { 0f, 22f, 47f, 72f };
        [Tooltip("반대 옆이 이만큼 넘게 작은 각으로 열릴 때만 넘김(°)")] public float SwapGain = 40f;
        [Tooltip("넓힌 뒤 더 작은 각이 이만큼 열려 있으면 줄임(초)")] public float NarrowAfter = 2f;

        InputAction lookPad, lookMouse;
        float yawVel, radVel, pitchVel, fovVel, sinceManual = 99f, pulledFor, sideLockLeft;
        float side = 1f, sideNow = 1f, extra, extraNow, openFor;
        readonly Queue<(double t, float yaw)> hist = new Queue<(double, float)>();
        bool wasLocked;

        public CombatTuning T => Tuning != null ? Tuning : CombatTuning.Default;
        public bool Live => Cam != null && Cam.Priority.Value >= LivePriority;
        public float Yaw => Orbit != null ? Orbit.HorizontalAxis.Value : 0f;
        public float Side => side;
        public int SideSwaps { get; private set; }
        /// 같은 옆 넓히기 · 막혔지만 어느 쪽도 안 풀려 그대로 둔 횟수(C17② 기록용)
        public int SideWidens { get; private set; }
        public int SideStays { get; private set; }
        public float SideExtraNow => extra;
        public int SideCuts { get; private set; }
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
            return HitResolver.Yaw(to) - sideNow * (SideDeg + extraNow);
        }

        float YawFor(float s, float e)
        {
            var to = HitResolver.Flat(Lock.Target.Position - Motor.Position);
            return HitResolver.Yaw(to) - s * (SideDeg + e);
        }

        /// 그 옆에서 열린 가장 작은 더하는 각(없으면 −1)
        float BestExtra(float s)
        {
            foreach (var e in SideExtra) if (Open(YawFor(s, e), out _)) return e;
            return -1f;
        }

        /// 그 yaw 에 카메라를 두면 보는 점에서 막히지 않는 거리(Deoccluder 와 같은 레이어·반지름) + 대상이 가려지는지
        bool Open(float yaw, out float free)
        {
            float r = Orbit.Radius;
            free = r;
            if (Pivot == null) return true;
            var dir = Quaternion.Euler(Orbit.VerticalAxis.Value, yaw, 0f) * Vector3.back;
            float rad = Occ != null ? Mathf.Max(0.05f, Occ.AvoidObstacles.CameraRadius) : 0.1f;
            int mask = Occ != null ? (int)Occ.CollideAgainst : Layers.CameraBlock;
            if (Physics.SphereCast(Pivot.position, rad, dir, out var hit, r, mask, QueryTriggerInteraction.Ignore)) free = hit.distance;
            if (free < r * PullRatio) return false;
            var cp = Pivot.position + dir * free;
            return Lock.Target == null || !Physics.Linecast(cp, Lock.Target.Chest, 1 << Layers.Wall, QueryTriggerInteraction.Ignore);
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
            extraNow = Mathf.MoveTowards(extraNow, extra, dt * 144f);

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

        void CutToSide()
        {
            sideNow = side; extraNow = extra;
            hist.Clear(); yawVel = 0f;
            Orbit.HorizontalAxis.Value = Mathf.DeltaAngle(0f, LockYaw());
            Cam.PreviousStateIsValid = false;
            SideCuts++;
        }

        void SideCheck(float dt)
        {
            if (Pivot == null) return;
            float dist = Vector3.Distance(Cam.State.GetFinalPosition(), Pivot.position);
            bool pulled = Orbit.Radius > 0.5f && dist < Orbit.Radius * PullRatio;
            bool hidden = Lock.Target != null && Physics.Linecast(Cam.State.GetFinalPosition(), Lock.Target.Chest, 1 << Layers.Wall, QueryTriggerInteraction.Ignore);
            // 미리 보기: 카메라가 따라갈 목표 자리(지연 전)가 이미 막혔으면 당겨지기 전에 센다
            bool goalShut = Lock.Target != null && !Open(YawFor(side, extra), out _);
            pulledFor = pulled || hidden || goalShut ? pulledFor + dt : 0f;
            // 넓혀 둔 상태에서 더 작은 각이 열려 있으면 줄임
            if (extra > 0f && !pulled && !hidden && sideLockLeft <= 0f)
            {
                float e = BestExtra(side);
                openFor = e >= 0f && e < extra ? openFor + dt : 0f;
                if (openFor > NarrowAfter) { extra = e; openFor = 0f; sideLockLeft = SideLock; }
            }
            else openFor = 0f;
            if (pulledFor > PullHold && sideLockLeft <= 0f)
            {
                pulledFor = 0f;
                sideLockLeft = SideLock;
                float cur = BestExtra(side), other = BestExtra(-side);
                if (cur >= 0f && (other < 0f || cur <= other + SwapGain))
                {
                    if (cur > extra) SideWidens++;
                    extra = cur;
                }
                else if (other >= 0f)
                {
                    float y0 = Orbit.HorizontalAxis.Value, y1 = YawFor(-side, other), span = Mathf.DeltaAngle(y0, y1);
                    bool through = false;
                    for (int k = 1; k < 5 && !through; k++) through = !Open(y0 + span * k / 5f, out _);
                    side = -side; extra = other; SideSwaps++;
                    if (through) CutToSide();
                }
                else SideStays++;
            }
        }
    }
}
