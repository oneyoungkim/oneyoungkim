// 행인1의 메인이벤트 — 시우 3인칭 이동 (docs/07_M1_조작_설계.md 3장·7-4)
// CharacterController 기반: 걷기·달리기·가감속·회전·중력·경사·턱·땅 붙이기·맵 밖 리스폰. 점프 없음.
// 입력은 SetMoveInput 으로 "한 프레임만" 넣는다(PInput·테스트·자동 걷기가 매 프레임 다시 넣음). 입력원이 꺼지면 저절로 멈춘다.
// M2(08 문서 2-4): 전투가 더한 것 — 걷기 속도 덮어쓰기·몸 방향 고정(락온, 한 프레임만) · 외부 이동(자석·넉백·회피 — AddDisplacement, CharacterController.Move 경유) · Halt · SetYaw.
using System;
using Unity.Cinemachine;
using UnityEngine;

namespace Haengin
{
    public enum MoveState { Idle, Walk, Run, Fall }

    [DefaultExecutionOrder(0), RequireComponent(typeof(CharacterController)), DisallowMultipleComponent]
    public sealed class PlayerMotor : MonoBehaviour
    {
        public MoveTuning Tuning;
        [Tooltip("카메라가 따라가는 점. 순간 이동 때 Cinemachine 에 알린다")] public Transform CamTarget;
        [Tooltip("이 높이 아래로 떨어지면 마지막 안전 지점으로 되돌린다(구역 bounds.min.y − 10)")] public float KillY = -1000f;

        CharacterController cc;
        MoveTuning t, fallback;

        // 이번 프레임 입력
        Vector3 inDir;
        float inAmt, inSpeed = -1f;
        bool inRun, hasInput, inKeepFacing;
        Vector3 ext;
        Collider sideHit, sideHitNow;

        // 상태
        Vector3 planarVel, prevPlanarVel, prevPos;
        float vy, yaw, prevYaw, airTime, safeTimer;
        bool grounded;
        Vector3 groundNormal = Vector3.up;
        float groundAngle;
        Vector3 wallSum;
        int wallHits;

        public MoveState State { get; private set; }
        public event Action<MoveState, MoveState> StateChanged;
        public event Action<Vector3> Teleported;

        /// 지난 프레임 실제 이동(m/s)
        public Vector3 Velocity { get; private set; }
        /// 실제 수평 속도(m/s)
        public float PlanarSpeed { get; private set; }
        /// 제 발로 움직인 수평 속도(외부 이동 뺌, 걷기 동작용 — M2)
        public float OwnSpeed { get; private set; }
        /// 목표로 움직이는 수평 속도(명령값, m/s)
        public float CommandSpeed => planarVel.magnitude;
        /// 명령 속도의 변화(m/s²) — 몸 기울기용
        public Vector3 PlanarAccel { get; private set; }
        /// 몸 방향 회전 속도(°/s, + = 위에서 볼 때 시계 방향)
        public float YawRate { get; private set; }
        public bool Grounded => grounded;
        public Vector3 GroundNormal => groundNormal;
        public float GroundAngle => groundAngle;
        /// 이번 프레임 턱·땅 붙이기로 생긴 높이 변화(BodyLean 이 비주얼로 흡수)
        public float StepDeltaY { get; private set; }
        public Vector3 LastSafePos { get; private set; }
        public Vector3 Position => transform.position;
        public float Yaw => yaw;
        /// 이번 프레임 달리기 입력이 들어왔는가
        public bool RunHeld { get; private set; }
        /// 지금 쓰는 조정값(Tuning 이 비어 있으면 기본값)
        public MoveTuning T => Tuning != null ? Tuning : (fallback != null ? fallback : fallback = ScriptableObject.CreateInstance<MoveTuning>());
        public CharacterController Controller => cc;
        public int Respawns { get; private set; }
        /// 지난 이동에서 옆으로 부딪힌 것(전투 벽꽝 검사)
        public Collider LastSideHit => sideHit;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            t = T;
            ApplyController(cc, t);
            yaw = prevYaw = transform.eulerAngles.y;
            prevPos = LastSafePos = transform.position;
        }

        void Start()
        {
            // AddComponent 직후 Tuning 을 넣는 경우(RigFactory·테스트)에도 캡슐이 조정값을 따르게
            t = T;
            ApplyController(cc, t);
        }

        /// 캡슐 값을 조정값대로(07 3-4). 장면 생성·테스트도 같은 값을 쓰게 공개.
        public static void ApplyController(CharacterController c, MoveTuning m)
        {
            c.height = m.height;
            c.radius = m.radius;
            c.skinWidth = m.skinWidth;
            c.center = new Vector3(0f, m.CenterY, 0f);
            c.stepOffset = m.stepOffset;
            c.slopeLimit = m.slopeLimit;
            c.minMoveDistance = 0f;
        }

        // ───────────────────────── 입력 주입
        /// worldDir: 월드 방향(수평만 씀), amount01: 0~1 기울기(걷기 속도), run: 달리기 버튼
        public void SetMoveInput(Vector3 worldDir, float amount01, bool run)
        {
            inDir = worldDir;
            inAmt = amount01;
            inRun = run;
            hasInput = true;
            inSpeed = -1f;
            inKeepFacing = false;
        }

        /// 전투(08 2-4): walkSpeed = 이 프레임 걷기 최고 속도(m/s, 앞 1.6 · 뒤·옆 1.3 · 막기 1.0), keepFacing = 몸 방향을 입력 방향으로 돌리지 않음(락온)
        public void SetMoveInput(Vector3 worldDir, float amount01, bool run, float walkSpeed, bool keepFacing)
        {
            SetMoveInput(worldDir, amount01, run);
            inSpeed = walkSpeed;
            inKeepFacing = keepFacing;
        }

        /// 외부 이동(자석·넉백·회피 등) — 다음 이동에 더해져 CharacterController.Move 로 간다(벽은 못 뚫음)
        public void AddDisplacement(Vector3 d) => ext += new Vector3(d.x, 0f, d.z);

        /// 걷던 속도를 0 으로(기술 시작)
        public void Halt()
        {
            planarVel = Vector3.zero;
        }

        /// 몸 방향을 바로(락온·소프트 조준)
        public void SetYaw(float yawDeg)
        {
            yaw = Mathf.DeltaAngle(0f, yawDeg);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        /// 테스트용 간단판: 월드 XZ 방향(길이 = 기울기, 1 이면 걷기 최고 속도)
        public void SetMoveInput(Vector2 worldDirXZ, bool run = false) =>
            SetMoveInput(new Vector3(worldDirXZ.x, 0f, worldDirXZ.y), Mathf.Clamp01(worldDirXZ.magnitude), run);

        public void ClearMoveInput()
        {
            hasInput = false;
            inAmt = 0f;
            inRun = false;
            inSpeed = -1f;
            inKeepFacing = false;
        }

        // ───────────────────────── 순간 이동
        public void Teleport(Vector3 feet, float yawDeg)
        {
            var delta = feet - transform.position;
            bool was = cc.enabled;
            cc.enabled = false;
            transform.SetPositionAndRotation(feet, Quaternion.Euler(0f, yawDeg, 0f));
            cc.enabled = was;
            Physics.SyncTransforms();
            yaw = prevYaw = yawDeg;
            planarVel = prevPlanarVel = Vector3.zero;
            vy = 0f;
            airTime = 0f;
            prevPos = LastSafePos = feet;
            hasInput = false;
            ext = Vector3.zero;
            Velocity = PlanarAccel = Vector3.zero;
            PlanarSpeed = OwnSpeed = YawRate = StepDeltaY = 0f;
            grounded = false;
            if (CamTarget != null) CinemachineCore.OnTargetObjectWarped(CamTarget, delta);
            Teleported?.Invoke(delta);
        }

        // ───────────────────────── 한 프레임
        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || !cc.enabled) return;
            t = T;

            // 1) 입력 꺼내기(한 프레임만 유효)
            var dir = new Vector3(inDir.x, 0f, inDir.z);
            float amt = hasInput ? Mathf.Clamp01(inAmt) : 0f;
            bool run = hasInput && inRun;
            float walkOverride = hasInput ? inSpeed : -1f;
            bool keepFacing = hasInput && inKeepFacing;
            hasInput = false;
            inSpeed = -1f;
            inKeepFacing = false;
            if (dir.sqrMagnitude < 1e-8f) amt = 0f;
            else dir.Normalize();
            RunHeld = run && amt > 0.5f;

            // 2) 목표 수평 속도 → 가감속(평면 속도 벡터를 목표로 MoveTowards: 급반전은 0 을 거쳐 감속)
            var target = Vector3.zero;
            if (amt >= 0.01f)
            {
                float walk = walkOverride > 0f ? walkOverride * Mathf.Clamp01(amt / t.stickFull)
                                               : Mathf.Lerp(t.minWalkSpeed, t.walkSpeed, Mathf.Clamp01(amt / t.stickFull));
                target = dir * (RunHeld ? t.runSpeed : walk);
            }
            float spd = planarVel.magnitude;
            float rate;
            if (target.magnitude > spd + 1e-4f && Vector3.Dot(planarVel, target) >= 0f)
                rate = spd < t.walkSpeed - 0.01f ? t.walkSpeed / t.walkAccelTime : (t.runSpeed - t.walkSpeed) / t.runAccelTime;
            else
                rate = t.decel;
            planarVel = Vector3.MoveTowards(planarVel, target, rate * dt);

            // 3) 몸 방향 = 입력 방향(속도 방향이 아니라). 정지 중에도 돈다
            if (amt > 0.1f && !keepFacing)
            {
                float want = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                float turn = State == MoveState.Run || RunHeld ? t.turnRateRun : t.turnRateWalk;
                yaw = Mathf.MoveTowardsAngle(yaw, want, turn * dt);
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }

            // 4) 땅·중력
            bool wasGrounded = grounded;
            Vector3 move;
            float expectDy;
            if (grounded)
            {
                vy = 0f;
                var pv = planarVel;
                if (groundAngle > cc.slopeLimit + 2f)
                {
                    // 너무 가파른 면: 내리막 쪽으로 미끄러짐(옹벽 모서리·급경사에 걸터앉지 않게)
                    var down = new Vector3(groundNormal.x, 0f, groundNormal.z);
                    if (down.sqrMagnitude > 1e-6f) pv += down.normalized * t.slideSpeed;
                }
                // 수평 속도는 그대로 두고 면을 따라 오르내리는 세로 속도만 더한다(경사에서 속도가 줄지 않게)
                float rise = groundNormal.y > 0.2f ? -(groundNormal.x * pv.x + groundNormal.z * pv.z) / groundNormal.y : 0f;
                expectDy = rise * dt;
                move = new Vector3(pv.x, rise - t.groundStick, pv.z);
            }
            else
            {
                vy = Mathf.Max(vy - t.gravity * dt, -t.maxFall);
                expectDy = vy * dt;
                move = new Vector3(planarVel.x, vy, planarVel.z);
            }

            // 5) 이동
            var p0 = transform.position;
            wallSum = Vector3.zero;
            wallHits = 0;
            sideHitNow = null;
            var extNow = ext;
            var flags = cc.Move(move * dt + ext);
            ext = Vector3.zero;
            sideHit = sideHitNow;
            if ((flags & CollisionFlags.Sides) != 0 && wallHits > 0)
            {
                // 벽으로 미는 성분 제거(벽에 붙어 속도가 쌓이지 않게)
                var n = new Vector3(wallSum.x, 0f, wallSum.z);
                if (n.sqrMagnitude > 1e-6f)
                {
                    n.Normalize();
                    float into = Vector3.Dot(planarVel, n);
                    if (into < 0f) planarVel -= n * into;
                }
            }
            bool below = (flags & CollisionFlags.Below) != 0;

            // 6) 땅 붙이기: 직전에 접지였는데 떨어졌으면 아래 snapDistance 안의 걷는 면으로
            if (wasGrounded && !below && vy <= 0f && CastDown(t.snapDistance, out var snapHit) &&
                Vector3.Angle(snapHit.normal, Vector3.up) <= cc.slopeLimit)
            {
                var f2 = cc.Move(Vector3.down * (snapHit.distance + 0.02f));
                below = (f2 & CollisionFlags.Below) != 0;
            }

            // 7) 접지·법선
            grounded = below || cc.isGrounded || CastDown(0.04f, out _);
            UpdateGroundNormal();
            if (!grounded) airTime += dt;
            else { airTime = 0f; vy = 0f; }

            // 8) 측정값
            var pos = transform.position;
            float dy = pos.y - p0.y;
            float stepD = dy - expectDy;
            StepDeltaY = wasGrounded && grounded && Mathf.Abs(stepD) > t.stepThreshold ? stepD : 0f;
            Velocity = (pos - prevPos) / dt;
            PlanarSpeed = new Vector3(Velocity.x, 0f, Velocity.z).magnitude;
            // 걷기 동작용 속도: 외부 이동(자석·넉백·회피, M2)을 뺀 제 발로 움직인 속도. 탐색(M1)에서는 외부 이동이 없어 PlanarSpeed 와 같다
            OwnSpeed = extNow.sqrMagnitude > 0f ? Mathf.Min(PlanarSpeed, planarVel.magnitude) : PlanarSpeed;
            PlanarAccel = (planarVel - prevPlanarVel) / dt;
            YawRate = Mathf.DeltaAngle(prevYaw, yaw) / dt;
            prevPos = pos;
            prevPlanarVel = planarVel;
            prevYaw = yaw;

            // 9) 상태
            float cmd = planarVel.magnitude;
            var s = airTime > 0.15f ? MoveState.Fall
                  : cmd < 0.05f ? MoveState.Idle
                  : RunHeld && cmd > t.walkSpeed + 0.3f ? MoveState.Run
                  : MoveState.Walk;
            if (s != State)
            {
                var old = State;
                State = s;
                StateChanged?.Invoke(old, s);
            }

            // 10) 안전 지점·맵 밖
            safeTimer += dt;
            if (safeTimer >= t.safeInterval)
            {
                safeTimer = 0f;
                if (grounded && groundAngle <= cc.slopeLimit) LastSafePos = pos;
            }
            if (pos.y < KillY)
            {
                Respawns++;
                Debug.LogWarning($"[PlayerMotor] 맵 밖으로 떨어짐(y {pos.y:F1} < {KillY:F1}) → 마지막 안전 지점 {LastSafePos}");
                Teleport(LastSafePos, yaw);
            }
        }

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (Mathf.Abs(hit.normal.y) < 0.3f)
            {
                wallSum += hit.normal;
                wallHits++;
                sideHitNow = hit.collider;
            }
        }

        // ───────────────────────── 땅 질의
        Vector3 BottomSphere => transform.position + Vector3.up * (cc.center.y - cc.height * 0.5f + cc.radius);

        /// 아래 sphere 를 dist(+skin) 만큼 내려 걷는 면이 있는가
        bool CastDown(float dist, out RaycastHit hit)
        {
            float r = cc.radius * 0.95f;
            return Physics.SphereCast(BottomSphere + Vector3.up * 0.02f, r, Vector3.down, out hit, dist + cc.skinWidth + 0.02f + (cc.radius - r),
                                      Layers.Solid, QueryTriggerInteraction.Ignore);
        }

        /// 발밑 법선: 가운데 아래로 쏜 광선(턱 모서리 법선에 속지 않게), 못 맞히면 구 검사 법선
        void UpdateGroundNormal()
        {
            var origin = transform.position + Vector3.up * 0.35f;
            if (Physics.Raycast(origin, Vector3.down, out var hit, 0.35f + 0.45f, Layers.Solid, QueryTriggerInteraction.Ignore))
                groundNormal = hit.normal;
            else if (CastDown(0.3f, out var sh))
                groundNormal = sh.normal;
            else
                groundNormal = Vector3.up;
            groundAngle = Vector3.Angle(groundNormal, Vector3.up);
        }
    }
}
