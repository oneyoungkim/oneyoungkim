// 행인1의 메인이벤트 — 배달 자전거 몸(docs/09_M3_버티컬슬라이스_설계.md 2-6, 8단계)
// 플레이어(시우)의 CharacterController 를 그대로 쓰고, 타는 동안 PlayerMotor 를 끄고 이것이 움직인다(캡슐 반지름 0.35·높이 1.6).
// 진행 방향 속도 + 조향(자전거 앞뒤 축) — 옆으로 미끄러지지 않는다. 경사 = 앞바퀴·뒷바퀴 자리 아래 땅 면 법선의 진행 방향 기울기 평균.
// 속도표(2-6):
//   평지·5° 아래 = 앉아 밟기 최고 5.0 m/s(가속 1.5 m/s², 페달 놓으면 0.6 m/s² 로 줄어듦)
//   5~9° = 엉덩이 들기(자동) 최고 3.0 m/s
//   9° 넘음 = 일어서기 리듬: □·○ 번갈아 한 번에 +0.4 m/s, 감쇠는 속도 비례 0.5/s → 박자 0.45~0.70초면 0.8~2.0 m/s.
//            0.45초(프레임 오차 0.02초 허용)보다 빨리 누르거나 같은 쪽을 두 번 = 헛발(추진 0, 국물 −2). (설계의 '감쇠 1.2 m/s²'를 그대로 빼면 박자 0.45~0.70초로는 속도가 0 으로 떨어져서 비례 감쇠로 바꿈)
//   오르막에서 1.2초 넘게 입력 없음 = 버티기: 뒤로 0.2 m/s 로 최대 0.5m 밀리다 멈춤 — 넘어지지 않고 내리지도 않는다
//   내리막 = 앉아 최고 7.0 m/s(내리막 가속 = 중력 × sin θ × 0.55), 브레이크 4 m/s²
// 내리기 입력은 없다. Dismount() 는 장면·코스가 끝날 때 코드만 부른다(D16 '모든 입력 조합에서 내림 0').
// 국물(SoupGauge)에 알리는 것: 턱 충격 · 벽·사람 부딪힘 · 옆 가속 · 내리막 과속 · 급브레이크 · 헛발.
using System;
using UnityEngine;

namespace Haengin
{
    public enum BikePosture { Seat, Hip, Stand, Hold }

    [DefaultExecutionOrder(-25), DisallowMultipleComponent]
    public sealed class BikeMotor : MonoBehaviour
    {
        [Header("속도표(2-6)")]
        public float SeatMax = 5.0f, HipMax = 3.0f, StandMax = 2.0f, DownMax = 7.0f;
        public float Accel = 1.5f, Coast = 0.6f, BrakeDecel = 4.0f;
        public float HipFrom = 5f, StandFrom = 9f, DownFrom = -3f;
        [Tooltip("일어서기 한 박자 추진(m/s)")] public float BeatBoost = 0.4f;
        [Tooltip("일어서기 감쇠(속도 비례, /s)")] public float BeatDecay = 0.5f;
        [Tooltip("이보다 빠른 박자는 헛발(초)")] public float BeatMin = 0.45f;
        public float HoldDelay = 1.2f, RollBack = 0.2f, RollMax = 0.5f;
        [Tooltip("오르막 감속(중력 × sin θ × 이 값, 앉기·엉덩이 들기)")] public float UphillDrag = 0.35f;
        [Tooltip("내리막 가속(중력 × sin θ × 이 값)")] public float DownhillPull = 0.55f;
        [Header("조향")]
        public float TurnLow = 85f, TurnHigh = 55f, TurnStand = 35f;
        [Header("몸")]
        public float Radius = 0.35f, Height = 1.6f, Gravity = 20f, Wheelbase = 1.0f;
        [Tooltip("케이크 운반(5-2): 일어서기 막음, 앉아서만 — 9° 넘는 데선 최고 1.2 m/s")] public bool NoStand;
        public float CakeSteepMax = 1.2f;

        public SoupGauge Soup;

        public bool Mounted { get; private set; }
        /// 지금까지 내린 횟수(코드가 Dismount 를 부른 것만 — 입력으로는 0)
        public int Dismounts { get; private set; }
        public float Speed { get; private set; }
        public float Yaw { get; private set; }
        public float SlopeDeg { get; private set; }
        public BikePosture Posture { get; private set; }
        public bool Stopped => Mathf.Abs(Speed) < 0.15f;
        public float Pedal { get; private set; }
        public float Steer { get; private set; }
        public float Brake { get; private set; }
        /// 크랭크 각(도, 페달질 시각화·발 IK)
        public float Crank { get; private set; }
        public float LateralAccel { get; private set; }
        public float RolledBack { get; private set; }
        public int Beats { get; private set; }
        public int Whiffs { get; private set; }
        public int Bumps { get; private set; }
        public int Hits { get; private set; }
        public float LastBeat { get; private set; } = -9f;
        public bool LastBeatRight { get; private set; }
        public bool Grounded { get; private set; }
        public Vector3 Position => transform.position;
        public Vector3 Forward => Quaternion.Euler(0f, Yaw, 0f) * Vector3.forward;
        /// 일어서기 리듬 한 박자마다(true = 제대로, false = 헛발) — 몸 좌우 흔들기(BikeRider)
        public event Action<bool> Beat;
        public event Action Handover;

        CharacterController cc;
        PlayerMotor pm;
        float pRadius, pHeight; Vector3 pCenter; float pStep, pSlope;
        float inSteer, inPedal, inBrake; bool inHas;
        bool beatL, beatR, handover;
        float idleUp, clock, prevY, lastBump = -9f;
        Vector3 lastHitPos = new Vector3(1e6f, 0f, 0f);
        readonly float[] devV = new float[12], devT = new float[12];
        int devI;
        bool hardBraked;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            pm = GetComponent<PlayerMotor>();
            if (pm != null) pm.Teleported += OnTeleported;
            enabled = false;
        }

        void OnDestroy() { if (pm != null) pm.Teleported -= OnTeleported; }

        // 장면 전환(SceneLoader)이 PlayerMotor.Teleport 로 옮기면 자전거 방향·속도도 맞춘다
        void OnTeleported(Vector3 delta)
        {
            if (!Mounted) return;
            Yaw = transform.eulerAngles.y; Speed = 0f; RolledBack = 0f; prevY = transform.position.y;
            Settle();
        }

        // ───────────────────────── 타기·내리기(코드만)
        public void Mount(Vector3 feet, float yaw)
        {
            if (cc == null) Awake();
            if (!Mounted)
            {
                pRadius = cc.radius; pHeight = cc.height; pCenter = cc.center; pStep = cc.stepOffset; pSlope = cc.slopeLimit;
            }
            if (pm != null) { pm.Teleport(feet, yaw); pm.ClearMoveInput(); pm.enabled = false; }
            else { cc.enabled = false; transform.SetPositionAndRotation(feet, Quaternion.Euler(0f, yaw, 0f)); cc.enabled = true; }
            cc.radius = Radius; cc.height = Height; cc.center = new Vector3(0f, Height * 0.5f + cc.skinWidth, 0f);
            cc.stepOffset = 0.3f; cc.slopeLimit = 40f;
            Mounted = true;
            Yaw = yaw; Speed = 0f; RolledBack = 0f; idleUp = 0f; Crank = 0f;
            lastHitPos = new Vector3(1e6f, 0f, 0f);
            LastBeat = -9f; LastBeatRight = true;      // 새로 타면 첫 박자는 □(왼쪽)부터
            Settle();
            prevY = feet.y;
            enabled = true;
        }

        /// 코스·장면이 끝날 때만(입력에는 없음)
        public void Dismount()
        {
            if (!Mounted) return;
            Mounted = false;
            Dismounts++;
            cc.radius = pRadius; cc.height = pHeight; cc.center = pCenter; cc.stepOffset = pStep; cc.slopeLimit = pSlope;
            enabled = false;
            if (pm != null) { pm.enabled = true; pm.Teleport(transform.position, Yaw); }
            Speed = 0f;
        }

        public void Teleport(Vector3 feet, float yaw)
        {
            cc.enabled = false;
            transform.SetPositionAndRotation(feet, Quaternion.Euler(0f, yaw, 0f));
            cc.enabled = true;
            Physics.SyncTransforms();
            Yaw = yaw; Speed = 0f; RolledBack = 0f; prevY = feet.y;
            lastHitPos = new Vector3(1e6f, 0f, 0f);
            Settle();
        }

        /// 올라타거나 옮긴 직후 0.3초는 턱 충격을 세지 않음(캡슐이 자리를 잡으며 생기는 높이 변화)
        void Settle()
        {
            for (int k = 0; k < devV.Length; k++) devV[k] = 0f;
            lastBump = clock + 0.05f;
        }

        // ───────────────────────── 입력(한 프레임만 유효 — 매 프레임 넣는다)
        public void SetInput(float steer, float pedal, float brake)
        {
            inSteer = Mathf.Clamp(steer, -1f, 1f); inPedal = Mathf.Clamp01(pedal); inBrake = Mathf.Clamp01(brake); inHas = true;
        }

        /// 일어서기 리듬 한 번(□ = 왼쪽 false, ○ = 오른쪽 true)
        public void PressBeat(bool right) { if (right) beatR = true; else beatL = true; }

        /// 건네기(× · E) — 멈춰 있을 때만 받는다
        public void PressHandover() { handover = true; }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || !Mounted || !cc.enabled) return;
            clock += dt;
            float steer = inHas ? inSteer : 0f, pedal = inHas ? inPedal : 0f, brake = inHas ? inBrake : 0f;
            inHas = false;
            Pedal = pedal; Steer = steer; Brake = brake;

            // 1) 경사(진행 방향): 앞·뒤 바퀴 아래 땅
            var fwd = Forward;
            SlopeDeg = MeasureSlope(fwd);
            float sin = Mathf.Sin(SlopeDeg * Mathf.Deg2Rad);
            bool steep = SlopeDeg > StandFrom;
            bool uphill = SlopeDeg > 1.5f;

            // 2) 리듬 박자
            bool anyBeat = beatL || beatR;
            if (anyBeat)
            {
                bool right = beatR && !beatL ? true : beatL && !beatR ? false : !LastBeatRight;
                float gap = clock - LastBeat;
                bool valid = !NoStand && steep && gap >= BeatMin - 0.02f && (Beats + Whiffs == 0 || right != LastBeatRight || gap > 1.2f);
                if (!NoStand && steep)
                {
                    if (valid) { Beats++; Speed = Mathf.Min(StandMax, Mathf.Max(Speed, 0f) + BeatBoost); idleUp = 0f; RolledBack = 0f; }
                    else { Whiffs++; Soup?.Whiff(); }
                    Beat?.Invoke(valid);
                    LastBeat = clock; LastBeatRight = right;
                }
            }
            beatL = beatR = false;

            // 3) 자세·속도
            float prevSpeed = Speed;
            if (steep && !NoStand)
            {
                Posture = Speed > 0.05f || clock - LastBeat < HoldDelay ? BikePosture.Stand : BikePosture.Hold;
                Speed -= Speed * BeatDecay * dt;
                if (Speed > 0f && clock - LastBeat > HoldDelay) Speed = Mathf.MoveTowards(Speed, 0f, 2f * dt);
            }
            else
            {
                float max = SlopeDeg <= DownFrom ? DownMax : SlopeDeg < HipFrom ? SeatMax : steep ? CakeSteepMax : HipMax;
                Posture = SlopeDeg >= HipFrom && SlopeDeg <= StandFrom ? BikePosture.Hip : BikePosture.Seat;
                if (pedal > 0.01f && Speed < max) Speed = Mathf.Min(max, Speed + Accel * pedal * dt);
                else if (pedal <= 0.01f && brake <= 0.01f && Speed > 0f && SlopeDeg > DownFrom) Speed = Mathf.Max(0f, Speed - Coast * dt);
                if (uphill && Speed > 0f) Speed = Mathf.Max(0f, Speed - Gravity * 0.5f * sin * UphillDrag * dt * (pedal > 0.01f ? 0.25f : 1f));
                if (SlopeDeg <= DownFrom) Speed = Mathf.Min(DownMax, Speed + 9.8f * -sin * DownhillPull * dt);
                if (Speed > max + 0.01f && SlopeDeg > DownFrom) Speed = Mathf.MoveTowards(Speed, max, 1.5f * dt);
                if (pedal > 0.01f || Speed > 0.05f) idleUp = 0f;
            }
            if (brake > 0.01f && Speed > 0f) Speed = Mathf.Max(0f, Speed - BrakeDecel * brake * dt);
            // 급브레이크: 2 m/s 넘게 달리다 브레이크를 0.8 넘게 — 누를 때 한 번
            if (brake > 0.8f && prevSpeed > 2f && !hardBraked) { hardBraked = true; Soup?.HardBrake(); }
            if (brake < 0.3f) hardBraked = false;

            // 4) 오르막 버티기: 멈춘 채 1.2초 넘게 입력 없음 → 뒤로 0.2 m/s, 최대 0.5m 밀리고 멈춤(넘어지지 않음)
            bool noInput = pedal <= 0.01f && !anyBeat;
            if (uphill && Speed <= 0.02f && noInput) idleUp += dt; else if (!uphill || !noInput) idleUp = 0f;
            float back = 0f;
            if (uphill && idleUp > HoldDelay && RolledBack < RollMax && brake < 0.5f)
            {
                back = Mathf.Min(RollBack * dt, RollMax - RolledBack);
                RolledBack += back;
                Posture = BikePosture.Hold;
            }
            if (Speed > 0.05f) RolledBack = 0f;

            // 5) 조향: 빠를수록 덜 돈다, 서 있으면 천천히(트랙 스탠드)
            float turn = Posture == BikePosture.Stand ? TurnStand : Mathf.Lerp(TurnLow, TurnHigh, Mathf.Clamp01(Speed / SeatMax));
            if (Speed < 0.2f) turn *= 0.5f;
            float yawRate = steer * turn;
            Yaw = Mathf.Repeat(Yaw + yawRate * dt, 360f);
            transform.rotation = Quaternion.Euler(0f, Yaw, 0f);
            LateralAccel = Mathf.Abs(Speed * yawRate * Mathf.Deg2Rad);

            // 6) 이동(진행 방향 + 땅 붙이기)
            fwd = Forward;
            var planar = fwd * (Speed - back / Mathf.Max(dt, 1e-4f));
            var p0 = transform.position;
            var flags = cc.Move((planar + Vector3.down * 2.0f) * dt);
            if ((flags & CollisionFlags.Below) == 0)
            {
                if (Physics.SphereCast(transform.position + Vector3.up * (Radius + 0.05f), Radius * 0.9f, Vector3.down, out var hit, 0.6f, Layers.Solid, QueryTriggerInteraction.Ignore))
                    cc.Move(Vector3.down * Mathf.Max(0f, hit.distance - 0.05f));
                else cc.Move(Vector3.down * Gravity * dt * dt * 10f);
            }
            Grounded = (flags & CollisionFlags.Below) != 0 || cc.isGrounded;
            var moved = transform.position - p0;
            // 벽·사람: 앞으로 가려던 만큼 못 갔으면 부딪힘
            if ((flags & CollisionFlags.Sides) != 0)
            {
                float along = Vector3.Dot(new Vector3(moved.x, 0f, moved.z), fwd) / dt;
                // 같은 벽에 대고 다시 밀 때는 안 셈(마지막 부딪힌 자리에서 0.6m 넘게 벗어나야 새 충돌)
                if (prevSpeed > 1.0f && along < prevSpeed * 0.5f && (transform.position - lastHitPos).sqrMagnitude > 0.36f) { Hits++; lastHitPos = transform.position; Soup?.Hit(); }
                // 막힌 만큼 속도를 깎는다(벽에 대고 페달을 밟아도 속도가 쌓이지 않게 — 한 번 부딪히면 한 번만 셈)
                if (along < Speed) Speed = Mathf.Max(0f, along);
            }
            // 턱: 0.12초 안에 땅 높이가 경사로 설명 안 되게 0.15m 넘게 튐(캡슐이 턱을 두세 프레임에 걸쳐 넘어서 창으로 모음)
            float dy = transform.position.y - prevY;
            float expect = Mathf.Tan(SlopeDeg * Mathf.Deg2Rad) * Vector3.Dot(new Vector3(moved.x, 0f, moved.z), fwd);
            devT[devI] = clock; devV[devI] = dy - expect; devI = (devI + 1) % devV.Length;
            float dev = 0f; for (int k = 0; k < devV.Length; k++) if (clock - devT[k] <= 0.12f) dev += devV[k];
            if (Mathf.Abs(dev) > 0.15f && clock - lastBump > 0.25f)
            {
                Bumps++; lastBump = clock; Soup?.Bump(Mathf.Abs(dev));
                for (int k = 0; k < devV.Length; k++) devV[k] = 0f;
            }
            prevY = transform.position.y;
            // 옆 가속·내리막 과속(국물)
            Soup?.Tick(dt, LateralAccel, SlopeDeg <= DownFrom ? Speed : 0f);

            // 7) 크랭크(페달질 시각화): 앉기·엉덩이 = 뒷바퀴 굴림에 맞춘 회전(기어비 2.2), 일어서기 = 박자마다 반 바퀴
            if (Posture == BikePosture.Stand) Crank = Mathf.MoveTowardsAngle(Crank, (Beats * 180f) % 360f, 720f * dt);
            else if (pedal > 0.01f || Speed > 0.1f) Crank = Mathf.Repeat(Crank + Speed / (2f * Mathf.PI * 0.30f) * 360f / 2.2f * dt * (pedal > 0.01f ? 1f : 0f), 360f);

            // 8) 건네기(멈춰 있을 때만)
            if (handover && Stopped) Handover?.Invoke();
            handover = false;
        }

        float MeasureSlope(Vector3 fwd)
        {
            var p = transform.position + Vector3.up * 1.2f;
            float half = Wheelbase * 0.5f;
            bool a = Physics.Raycast(p + fwd * half, Vector3.down, out var hf, 3f, Layers.Solid, QueryTriggerInteraction.Ignore);
            bool b = Physics.Raycast(p - fwd * half, Vector3.down, out var hr, 3f, Layers.Solid, QueryTriggerInteraction.Ignore);
            if (!a && !b) return SlopeDeg;
            // 면 법선으로 진행 방향 경사(턱 모서리는 높이 차로 재면 '오르막'으로 읽혀 일어서기·충격 판정이 틀어져서 법선을 쓴다)
            float S(RaycastHit h) => Mathf.Atan2(-Vector3.Dot(h.normal, fwd), Mathf.Max(0.05f, h.normal.y)) * Mathf.Rad2Deg;
            if (a && b) return (S(hf) + S(hr)) * 0.5f;
            return a ? S(hf) : S(hr);
        }
    }
}
