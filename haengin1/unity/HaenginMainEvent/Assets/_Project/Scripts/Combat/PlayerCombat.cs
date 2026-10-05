// 행인1의 메인이벤트 — 시우 전투 조작 (docs/08_M2_전투_설계.md 2장·3장)
// 입력 버퍼(실제 시간 0.20초) → 기술 실행: 약 4타(잽-크로스-훅-크로스) + △ 마무리 4종(앞차기·어퍼·스텝 무릎·큰 훅), 연결 창(판정 끝 + 3f ~ 끝),
// 콤보 끊김(끝 + 0.15초), 소프트 조준·자석(Fighter.StartAttack), 회피(무적 2~13f·읽었다·반격·연속 제한), 막기(가드 게이지·크러시 — Fighter),
// 전진 버팀(크로스 발생~판정 끝, 약타 1번, 쿨다운 2초), 잡기 3갈래(클린치 무릎×3 → 자동 밀기 · 하체 밀기(벽꽝) · 돌려세우기), 기세 게이지.
// 입력은 PInput(Combat 맵)·테스트·InputScript 가 Press/SetStick/SetGuard 로 넣는다. 실행 순서 −45: PInput(−50) 다음, Fighter(−5)·모터(0) 전.
using System;
using UnityEngine;

namespace Haengin
{
    [DefaultExecutionOrder(-45), DisallowMultipleComponent]
    public sealed class PlayerCombat : MonoBehaviour
    {
        public PlayerMotor Motor;
        public Fighter Me;
        public CombatTuning Tuning;
        public MoveSet Moves;
        public LockOn Lock;
        public CamRig ExploreCam;
        public CombatCamRig CombatCam;
        public HeatGauge Heat = new HeatGauge();
        [Tooltip("시험용: 회피 이동 배율(C06 = 0)")] public float DodgeMoveScale = 1f;

        public CombatTuning T => Tuning != null ? Tuning : CombatTuning.Default;
        public bool Active { get; private set; }

        // ── 입력
        Vector2 stick;
        Vector3 worldStick;
        bool useWorld, runHeld, guardHeld;
        readonly double[] pressAt = { -1e9, -1e9, -1e9, -1e9 };
        readonly int[] pressFrame = { -1, -1, -1, -1 };
        double acceptAfter;

        // ── 콤보
        /// 지금(또는 마지막) 약공격 콤보 순번 0~4. 마무리 뒤 0
        public int Combo { get; private set; }
        double gameNow, comboEndAt = -1e9;
        bool lightLocked;

        // ── 회피
        public bool Dodging { get; private set; }
        double dodgeT, dodgeF0, dodgeF1, lastDodgeEnd = -1e9;
        int dodgeFrame = -1, dodgeChain;
        Vector3 dodgeDir, dodgeOrigin;
        public int DodgeChain => dodgeChain;

        // ── 읽었다·반격
        double readUntil = -1e9;
        Fighter readTarget;
        Btn? counterReq;
        public int ReadCount { get; private set; }
        public bool ReadActive => TimeFx.Real <= readUntil;

        // ── 버팀
        double braceReady = -1e9;
        public int BraceCount { get; private set; }

        // ── 잡기
        Fighter held;
        double holdLeft;
        int kneeCount;
        bool turning;
        float turnFrom, turnTo;
        double turnT;
        public Fighter Held => held;
        public bool Turning => turning;

        /// 알림('읽었다'·'뿌리침' 등 — HUD 12단계, 지금은 DebugHud 토스트)
        public event Action<string> Notice;

        public double GameNow => gameNow;
        public bool Locked => Lock != null && Lock.Target != null && Lock.Target.Targetable;

        void Awake()
        {
            if (Motor == null) Motor = GetComponent<PlayerMotor>();
            if (Me == null) Me = GetComponent<Fighter>();
            if (Lock == null) Lock = GetComponent<LockOn>();
            if (Moves == null) Moves = MoveSet.CreateDefault();
        }

        void OnEnable()
        {
            if (Me == null) Me = GetComponent<Fighter>();
            if (Me == null) return;
            Me.IsPlayer = true;
            Me.IFrames = InIFrames;
            Me.Threat = OnThreat;
            Me.Brace = TryBrace;
            Me.ThreatPoint = () => Dodging ? dodgeOrigin : Me.Position;
            Me.Interrupted += OnInterrupted;
            Me.Hurt += OnHurt;
            Me.Landed += OnLanded;
            Me.Downed += OnDowned;
        }

        void OnDisable()
        {
            if (Me == null) return;
            Me.Interrupted -= OnInterrupted;
            Me.Hurt -= OnHurt;
            Me.Landed -= OnLanded;
            Me.Downed -= OnDowned;
        }

        void Start()
        {
            if (ExploreCam == null) ExploreCam = FindAnyObjectByType<CamRig>();
        }

        // ───────────────────────── 시작·끝
        /// 전투 시작: 입력 받기 전 대기(inputDelay 초, 08 2-5: 0.4), 기세 20
        public void Begin(float inputDelay = 0f, float heat = -1f)
        {
            Active = true;
            acceptAfter = TimeFx.Real + Mathf.Max(0f, inputDelay);
            ClearInput();
            Heat.Begin(heat >= 0f ? heat : T.HeatStart);
            Combo = 0;
            comboEndAt = -1e9;
            lightLocked = false;
            if (Me != null) Me.IsPlayer = true;
        }

        public void End()
        {
            Active = false;
            ClearInput();
            if (Dodging) EndDodge();
            if (held != null) ReleaseHold(0f);
            if (Me != null) { Me.CancelAttack(); Me.SetBusy(false); Me.Guarding = false; }
            if (Lock != null) Lock.Unlock();
        }

        public void ClearInput()
        {
            for (int i = 0; i < pressAt.Length; i++) { pressAt[i] = -1e9; pressFrame[i] = -1; }
            stick = Vector2.zero;
            useWorld = false;
            runHeld = guardHeld = false;
            counterReq = null;
        }

        // ───────────────────────── 입력 넣기(PInput·테스트)
        public void Press(Btn b)
        {
            if (!Active) return;
            double now = TimeFx.Real;
            double clock = TimeFx.InputClock;
            // 누워 있을 때 공격 버튼 = 빨리 일어나기
            if (Me != null && (Me.State == Fighter.Phase.Lie || Me.State == Fighter.Phase.Fall) && b != Btn.Dodge) { Me.Mash(); return; }
            if (now < acceptAfter) return;
            if (b == Btn.Light && lightLocked) return;          // 4타 뒤 □ 무시
            if (ReadActive && (b == Btn.Light || b == Btn.Heavy)) counterReq = b;
            pressAt[(int)b] = clock;
            pressFrame[(int)b] = Time.frameCount;
        }

        /// 화면 기준 스틱(−1..1)
        public void SetStick(Vector2 s, bool run) { stick = Vector2.ClampMagnitude(s, 1f); runHeld = run; useWorld = false; }
        /// 월드 방향 그대로(테스트·봇)
        public void SetStickWorld(Vector3 dir, float amount, bool run = false)
        {
            dir.y = 0f;
            worldStick = dir.sqrMagnitude > 1e-6f ? dir.normalized * Mathf.Clamp01(amount) : Vector3.zero;
            runHeld = run;
            useWorld = true;
        }
        public void SetGuard(bool held) => guardHeld = held;

        /// 버퍼(08 2-2): 0.20초. 시계 = TimeFx.InputClock(실제 시간, 히트스톱 프레임 뺌)
        bool Buffered(Btn b) => TimeFx.InputClock - pressAt[(int)b] <= T.Buffer + 1e-6;
        bool PressedThisFrame(Btn b) => pressFrame[(int)b] == Time.frameCount;
        void Clear(Btn b) { pressAt[(int)b] = -1e9; pressFrame[(int)b] = -1; }

        int takenFrame = -1;

        /// 버퍼에 있는 것 중 가장 나중에 누른 것(같이 든 다른 것은 지움)
        Btn? TakeLatest(params Btn[] bs)
        {
            Btn? best = null;
            double at = -1e9;
            foreach (var b in bs)
                if (Buffered(b) && pressAt[(int)b] > at) { at = pressAt[(int)b]; best = b; }
            takenFrame = best != null ? pressFrame[(int)best.Value] : -1;
            if (best != null) foreach (var b in bs) Clear(b);
            return best;
        }

        bool Take(Btn b)
        {
            if (!Buffered(b)) return false;
            Clear(b);
            return true;
        }

        /// 스틱 → 월드 방향(락온 중 = 대상 기준, 아니면 화면 기준 — 08 2-1)
        public Vector3 StickWorld(out float amount)
        {
            if (useWorld) { amount = worldStick.magnitude; return amount > 1e-4f ? worldStick / amount : Vector3.zero; }
            amount = stick.magnitude;
            if (amount < 1e-4f) return Vector3.zero;
            Vector3 d;
            if (Locked)
            {
                float basis = HitResolver.Yaw(HitResolver.Flat(Lock.Target.Position - Me.Position));
                d = Quaternion.Euler(0f, basis, 0f) * new Vector3(stick.x, 0f, stick.y);
            }
            else if (CombatCam != null && CombatCam.Live) d = CombatCam.StickToWorld(stick);
            else if (ExploreCam != null) d = ExploreCam.StickToWorld(stick);
            else d = new Vector3(stick.x, 0f, stick.y);
            d.y = 0f;
            return d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.zero;
        }

        // ───────────────────────── 한 프레임
        void Update()
        {
            if (!Active || Me == null) return;
            var t = T;
            float dt = TimeFx.Dt;
            gameNow += dt;
            Heat.Tick(dt, t);
            Me.Guarding = false;
            if (dt <= 0f) return;    // 히트스톱·일시정지: 입력은 버퍼에 남는다(실제 시간)
            // 전투 달리기(R2) 중엔 락온을 내려 두고 놓으면 같은 대상으로(08 2-3)
            if (Lock != null) Lock.Suspend(runHeld && (useWorld ? worldStick.sqrMagnitude : stick.sqrMagnitude) > 0.04f && Me.State == Fighter.Phase.Free);

            switch (Me.State)
            {
                case Fighter.Phase.Stagger:
                case Fighter.Phase.Fall:
                case Fighter.Phase.Lie:
                case Fighter.Phase.GetUp:
                case Fighter.Phase.Grabbed:
                case Fighter.Phase.Out:
                    return;
            }

            if (Dodging)
            {
                TickDodge(dt);
                if (Dodging && !DodgeExit()) return;
            }
            if (held != null) { TickHold(dt); return; }
            if (Me.State == Fighter.Phase.Act) { Links(); return; }
            if (Me.State == Fighter.Phase.Busy) return;

            // ── 자유: 콤보 끊김 → 반격 → 회피 → 잡기 → 공격 → 이동·막기
            if (Combo > 0 && gameNow - comboEndAt > t.ComboReset + 1e-6) Combo = 0;
            if (counterReq != null) { StartCounter(); return; }
            if (Buffered(Btn.Dodge) && CanDodge()) { Clear(Btn.Dodge); StartDodge(); return; }
            if (Take(Btn.Grab)) { StartGrab(); return; }
            var b = TakeLatest(Btn.Light, Btn.Heavy);
            if (b != null)
            {
                var m = Next(b.Value, out int idx);
                if (m != null) { StartMove(m, 0, idx); return; }
            }
            Locomotion(dt);
        }

        void Locomotion(float dt)
        {
            var t = T;
            bool guard = guardHeld && !runHeld;
            Me.Guarding = guard;
            var dir = StickWorld(out float amt);
            if (Locked && !runHeld)
            {
                // 늘 대상을 본다(540°/s), 이동 = 대상 기준
                var to = HitResolver.Flat(Lock.Target.Position - Me.Position);
                if (to.sqrMagnitude > 1e-4f)
                    Me.Body.SetYaw(Mathf.MoveTowardsAngle(Me.Yaw, HitResolver.Yaw(to), t.LockTurnRate * dt));
                float speed = guard ? t.WalkGuard : WalkSpeed(dir, Me.Forward, t);
                Motor.SetMoveInput(dir, amt, false, speed, true);
            }
            else
            {
                float speed = guard ? t.WalkGuard : t.WalkFwd;
                Motor.SetMoveInput(dir, amt, runHeld && !guard, speed, false);
            }
        }

        /// 앞 1.6 · 뒤 1.3 · 옆 1.3 m/s(몸 방향 기준)
        public static float WalkSpeed(Vector3 dir, Vector3 fwd, CombatTuning t)
        {
            if (dir.sqrMagnitude < 1e-6f) return t.WalkFwd;
            float c = Vector3.Dot(dir.normalized, fwd);
            return c > 0.5f ? t.WalkFwd : t.WalkSide;
        }

        // ───────────────────────── 기술
        /// 다음 기술(08 3-3): □ = 순번 다음(4타 뒤 없음), △ = 지금 순번의 마무리
        MoveDef Next(Btn b, out int idx)
        {
            idx = 0;
            if (b == Btn.Light)
            {
                if (Combo >= 4) return null;
                idx = Combo + 1;
                return Moves.Light(idx);
            }
            if (b == Btn.Heavy) return Moves.Finisher(Combo);
            return null;
        }

        void StartMove(MoveDef m, double carry, int idx)
        {
            var aim = PickTarget(m);
            var r = Me.StartAttack(m, aim, carry, null, idx);
            Combo = idx;
            lightLocked = m == Moves.CrossEnd;
            counterReq = null;
            r.OnEnd = OnMoveEnd;
            if (Dodging) EndDodge();
            dodgeChain = 0;
        }

        void OnMoveEnd(AttackRun r)
        {
            comboEndAt = gameNow;
            lightLocked = false;
        }

        /// 공격 중: 판정 뒤 회피 캔슬 · 연결 창에서 다음 기술(버퍼에 있던 입력은 창이 열린 정확한 시각부터 — 넘친 시간을 넘긴다)
        void Links()
        {
            var r = Me.Run;
            if (r == null) return;
            if (r.PastActive && Buffered(Btn.Dodge) && r.Fixed == null && CanDodge())
            {
                Clear(Btn.Dodge);
                Me.CancelAttack();
                StartDodge();
                return;
            }
            bool chain = r.Move == Moves.Jab || r.Move == Moves.Cross || r.Move == Moves.Hook || r.Move == Moves.CrossEnd;
            if (!chain || !r.LinkOpen) return;
            var b = TakeLatest(Btn.Light, Btn.Heavy);
            if (b == null) return;
            var m = Next(b.Value, out int idx);
            if (m == null) return;
            bool waited = takenFrame != Time.frameCount;     // 창이 열리기 전에 눌러 둔 입력
            double carry = waited ? Math.Max(0, r.T - r.Move.LinkAt) : 0;
            Me.CancelAttack();
            comboEndAt = gameNow;
            StartMove(m, carry, idx);
        }

        /// 락온 대상(서 있으면) 또는 소프트 조준(기준 방향 = 스틱 또는 몸 정면)
        Fighter PickTarget(MoveDef m)
        {
            if (Locked) return Lock.Target;
            var dir = StickWorld(out float amt);
            var refDir = amt > 0.2f ? dir : Me.Forward;
            return HitResolver.SoftAim(Me, refDir, T, Fighter.All);
        }

        // ───────────────────────── 회피(3-5)
        bool CanDodge()
        {
            var t = T;
            if (Dodging)
            {
                double need = dodgeChain >= 2 ? t.DodgeChain + t.DodgeDoubleWait : t.DodgeChain;
                return dodgeT >= need / MoveDef.Fps - 1e-6;
            }
            if (dodgeChain >= 2)
            {
                double need = (t.DodgeChain + t.DodgeDoubleWait - t.DodgeTotal) / MoveDef.Fps;
                return gameNow - lastDodgeEnd >= need - 1e-6;
            }
            return true;
        }

        void StartDodge()
        {
            var t = T;
            bool chained = Dodging || gameNow - lastDodgeEnd <= 0.15;
            if (Dodging) EndDodge();
            dodgeChain = chained ? dodgeChain + 1 : 1;
            var dir = StickWorld(out float amt);
            dodgeDir = amt > 0.2f ? dir : -Me.Forward;     // 입력 없음 = 뒤
            dodgeOrigin = Me.Position;
            Dodging = true;
            dodgeT = 0;
            Combo = 0;
            Me.SetBusy(true);
            Motor.Halt();
            TickDodge(TimeFx.Dt);
        }

        void TickDodge(float dt)
        {
            var t = T;
            double t0 = dodgeT, t1 = dodgeT + dt;
            dodgeFrame = Time.frameCount;
            dodgeF0 = t0; dodgeF1 = t1;
            double mv = t.DodgeMoveF / MoveDef.Fps;
            float a = EaseOut((float)Math.Min(1, t0 / mv)), b = EaseOut((float)Math.Min(1, t1 / mv));
            if (b > a && DodgeMoveScale > 0f) Motor.AddDisplacement(dodgeDir * (t.DodgeDist * (b - a) * DodgeMoveScale));
            dodgeT = t1;
            if (dodgeT >= t.DodgeTotal / MoveDef.Fps - 1e-6) EndDodge();
        }

        /// 회피 중 끊기: 반격(읽었다) · 공격(15f~) · 연속 회피(18f~). 끊으면 true(자유 처리로 넘어감)
        bool DodgeExit()
        {
            var t = T;
            bool canCancel = dodgeT >= t.DodgeCancel / MoveDef.Fps - 1e-6;
            if (counterReq != null && canCancel) { EndDodge(); return true; }
            if (canCancel && (Buffered(Btn.Light) || Buffered(Btn.Heavy) || Buffered(Btn.Grab))) { EndDodge(); return true; }
            if (Buffered(Btn.Dodge) && CanDodge()) { Clear(Btn.Dodge); StartDodge(); }
            return false;
        }

        void EndDodge()
        {
            Dodging = false;
            lastDodgeEnd = gameNow;
            Me.SetBusy(false);
        }

        static float EaseOut(float u) => 1f - (1f - u) * (1f - u) * (1f - u);

        /// 이번 프레임이 회피 무적 2~13f 와 겹치는가(실행 순서와 무관하게 이번 프레임 구간으로)
        public bool InIFrames()
        {
            if (!Dodging) return false;
            var t = T;
            double a = dodgeFrame == Time.frameCount ? dodgeF0 : dodgeT, b = dodgeFrame == Time.frameCount ? dodgeF1 : dodgeT + TimeFx.Dt;
            return HitResolver.Overlaps(a, b, t.IFrameStart / MoveDef.Fps, t.IFrameEnd / MoveDef.Fps);
        }

        /// 적 공격의 판정이 시작될 때 부채꼴 안: 회피 무적이면 '읽었다'(08 3-5)
        bool OnThreat(Fighter atk, MoveDef m)
        {
            if (!InIFrames()) return false;
            var t = T;
            ReadCount++;
            if (!t.Reduced) TimeFx.Slow(t.ReadSlow, t.ReadScale);
            Heat.Add(t.ReadHeat);
            readUntil = TimeFx.Real + t.ReadWindow;
            readTarget = atk;
            Say("읽었다");
            return true;
        }

        void StartCounter()
        {
            var b = counterReq.Value;
            counterReq = null;
            readUntil = -1e9;
            var m = b == Btn.Light ? Moves.CounterCross : Moves.DuckUpper;
            Clear(Btn.Light); Clear(Btn.Heavy);
            var aim = readTarget != null && readTarget.Targetable ? readTarget : PickTarget(m);
            var r = Me.StartAttack(m, aim, 0, null, 0);
            Combo = 0;
            r.OnEnd = OnMoveEnd;
        }

        // ───────────────────────── 전진 버팀(3-5)
        bool TryBrace(MoveDef incoming)
        {
            var r = Me.Run;
            if (r == null || r.Move != Moves.Cross || r.Combo != 2) return false;
            if (r.NowT >= r.Move.ActiveEnd - HitResolver.Eps) return false;
            if (gameNow < braceReady) return false;
            braceReady = gameNow + T.BraceCooldown;
            BraceCount++;
            Heat.Add(T.BraceHeat);
            return true;
        }

        // ───────────────────────── 잡기(3-4)
        void StartGrab()
        {
            var aim = PickTarget(Moves.Grab);
            var r = Me.StartAttack(Moves.Grab, aim, 0, null, 0);
            r.OnTarget = OnGrabTarget;
            r.OnEnd = OnMoveEnd;
            Combo = 0;
            if (Dodging) EndDodge();
        }

        bool OnGrabTarget(AttackRun r, Fighter f)
        {
            var t = T;
            if (f.Down || (f.Run != null && f.Run.ActiveNow)) return true;      // 다운·공격 판정 중인 적은 못 잡음(헛잡기)
            bool open = f.State == Fighter.Phase.Stagger || f.Crouch;
            if (f.Armor && !open)
            {
                // 뿌리침: 시우 18f 경직, 0.3m 밀림
                Me.SetStagger(t.ShakeOffStagger / MoveDef.Fps);
                Me.Knock(-Me.Forward, t.ShakeOffPush);
                Say("뿌리침");
                return true;
            }
            Me.CancelAttack();
            held = f;
            f.Grabbed(Me);
            holdLeft = f.GrabHoldTime > 0f ? f.GrabHoldTime : t.GrabHold;
            kneeCount = 0;
            turning = false;
            Me.SetBusy(true);
            Heat.Add(Moves.Grab.Heat);
            PlaceHeld();
            var ev = new HitEvent { Attacker = Me, Victim = f, Move = Moves.Grab, Outcome = HitOutcome.Hit, Power = Power.None, Frame = Time.frameCount };
            Me.RaiseLanded(ev);
            return true;
        }

        void PlaceHeld()
        {
            if (held == null || held.Body == null) return;
            held.Body.Place(Me.Position + Me.Forward * T.GrabDist, Me.Yaw + 180f);
        }

        void TickHold(float dt)
        {
            var t = T;
            if (held == null || held.State != Fighter.Phase.Grabbed || held.GrabbedBy != Me) { held = null; Me.SetBusy(false); return; }
            holdLeft -= dt;
            if (turning)
            {
                turnT += dt;
                float u = Mathf.Clamp01((float)(turnT / t.TurnTime));
                float e = u * u * (3f - 2f * u);
                Me.Body.SetYaw(turnFrom + Mathf.DeltaAngle(turnFrom, turnTo) * e);
                PlaceHeld();
                if (u >= 1f) turning = false;
                return;
            }
            PlaceHeld();
            if (Me.Run != null) return;              // 무릎·밀기 진행 중(Fighter 가 돌림)
            if (Me.State != Fighter.Phase.Busy) Me.SetBusy(true);
            if (holdLeft <= 0) { ReleaseHold(t.ReleaseStagger / MoveDef.Fps); return; }

            if (Take(Btn.Heavy)) { StartPush(); return; }
            if (Take(Btn.Light)) { StartKnee(); return; }
            if (Buffered(Btn.Grab))
            {
                var dir = StickWorld(out float amt);
                Clear(Btn.Grab);
                if (amt > 0.2f)
                {
                    turning = true;
                    turnT = 0;
                    turnFrom = Me.Yaw;
                    turnTo = HitResolver.Yaw(dir);
                    Heat.Add(3);
                }
            }
        }

        void StartKnee()
        {
            kneeCount++;
            var m = kneeCount >= 3 ? Moves.Knee3 : Moves.Knee;
            var r = Me.StartAttack(m, null, 0, held, 0);
            r.OnEnd = run => { if (kneeCount >= 3 && held != null) StartPush(); };
        }

        void StartPush()
        {
            var f = held;
            var r = Me.StartAttack(Moves.Push, null, 0, f, 0);
            r.OnEnd = OnMoveEnd;
            r.OnTarget = (run, v) =>
            {
                var t = T;
                held = null;
                var ev = v.Receive(Me, Moves.Push);
                Me.RaiseLanded(ev);
                if (!v.Down && v.State != Fighter.Phase.Out) v.Shoved(Me.Forward, t.PushDist, t.PushTime, Me);
                return true;
            };
        }

        /// 잡기 풀기: 둘이 0.3m 떨어지고 시우 경직(풀림 12f)
        void ReleaseHold(float myStagger)
        {
            var f = held;
            held = null;
            turning = false;
            if (f != null && f.State == Fighter.Phase.Grabbed && f.GrabbedBy == Me)
            {
                f.Release(0f);
                f.Knock(Me.Forward, T.ReleaseSep);
            }
            Me.SetBusy(false);
            if (myStagger > 0f) { Me.Knock(-Me.Forward, T.ReleaseSep); Me.SetStagger(myStagger); }
        }

        // ───────────────────────── 사건
        void OnInterrupted()
        {
            if (Dodging) { Dodging = false; lastDodgeEnd = gameNow; }
            if (held != null)
            {
                var f = held;
                held = null;
                turning = false;
                if (f.State == Fighter.Phase.Grabbed && f.GrabbedBy == Me) f.Release(0f);
            }
            lightLocked = false;
        }

        void OnHurt(HitEvent e)
        {
            var t = T;
            switch (e.Outcome)
            {
                case HitOutcome.Blocked: Heat.Add(t.BlockHeat); break;
                case HitOutcome.Crushed: Heat.Add(t.CrushHeat); Say("가드 크러시"); break;
                case HitOutcome.Hit: Heat.Add(0f); Combo = 0; break;
            }
        }

        void OnLanded(HitEvent e)
        {
            if (e.Move == null || e.Move == Moves.Grab) return;
            if (e.Landed && e.Move.Heat != 0) Heat.Add(e.Move.Heat);
        }

        void OnDowned(Fighter f) => Heat.Add(T.DownHeat);

        void Say(string s)
        {
            Notice?.Invoke(s);
            DebugHud.Toast(s, 1.2f);
        }

        // ───────────────────────── 락온 입력(4단계 LockOn 으로 넘김)
        public void ToggleLock() { if (Active && Lock != null) Lock.Toggle(); }
        public void SwitchTarget(int dir) { if (Active && Lock != null) Lock.Cycle(dir); }
        public void LookStick(Vector2 s) { if (Active && Lock != null) Lock.Flick(s); }
    }
}
