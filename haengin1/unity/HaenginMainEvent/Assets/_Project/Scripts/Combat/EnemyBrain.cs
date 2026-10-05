// 행인1의 메인이벤트 — 적 AI (docs/08_M2_전투_설계.md 4-1·4-2·4-4·4-5)
// 상태: 대기 → 접근 → 간보기(자리 걷기·도발) → 공격(공격권 → 들어가기 → 예고·발생·판정·회복·연결) → 간보기 / 막기(막기형) / 피격·잡힘 / 다운·기상 / 탈락 / 도주(깐족이).
// 몸 = FighterBody(조향: 자리 쪽 + 서로 밀어내기 0.9m), 피격·다운·무적은 Fighter 가 한다. 공격권은 AttackDirector.
// 실행 순서 −20: 시우 입력(−45) 다음, 공격권(−30) 다음, Fighter(−5)·몸(0) 전.
using System;
using UnityEngine;

namespace Haengin
{
    [DefaultExecutionOrder(-20), DisallowMultipleComponent]
    public sealed class EnemyBrain : MonoBehaviour
    {
        public enum S { Idle, Approach, Strafe, Taunt, AttackIn, Attack, Block, Hurt, Down, Out, Flee, Stumble, BackOff, Leave }

        public EnemyDef Def;
        public Fighter Me;
        public FighterBody Body;
        public Fighter Target;
        public AttackDirector Director;
        public CombatTuning Tuning;
        [Tooltip("시험용: 공격 안 함(공격권 안 청함)")] public bool NoAttack;
        [Tooltip("시험용: 막은 뒤 카운터 안 함")] public bool NoCounter;
        [Tooltip("시험용: 막기 확률을 이 값으로(음수 = 4-5 표)")] public float BlockOverride = -1f;
        [Tooltip("시험용: 제자리(자리 걷기 안 함, 시우만 봄)")] public bool HoldPosition;
        [Tooltip("기세 액션 중(08 3-8): AI 정지 — 서서 시우만 본다")] public bool Frozen;

        public CombatTuning T => Tuning != null ? Tuning : CombatTuning.Default;
        public S State { get; private set; } = S.Idle;
        public bool HasToken { get; internal set; }
        public double WaitSince { get; private set; }
        public float SlotAngle, SlotJitter;
        public bool SlotSet;
        public Vector3 Slot;
        /// 도주했다(결과 집계 = 탈락)
        public bool Fled { get; private set; }
        public bool Eliminated => Me == null || Me.State == Fighter.Phase.Out || Fled;
        public bool UsesSlot => State == S.Approach || State == S.Strafe || State == S.Taunt || State == S.Block || State == S.Hurt;
        public bool WantsToken => !NoAttack && State == S.Strafe && cooldown <= 0f && Target != null && Me.State == Fighter.Phase.Free;
        public MoveDef Pending => pending;
        public int Attacks, BlocksStarted, Counters, Taunts;
        public event Action<EnemyBrain, MoveDef> AttackBegan;
        /// 처음 막았을 때 한 번(도움말 — 4-5)
        public static event Action<EnemyBrain> FirstBlock;
        static bool firstBlockShown;

        System.Random rng;
        float cooldown, repick, tauntRoll, stateT;
        double clock, blockUntil, counterBefore = -1;
        MoveDef pending;
        AttackRun lastRun;
        float stumbleLeft, backLeft;
        Vector3 inStart;
        /// 페이즈 2(스크럼 HP ≤ 50%)
        public bool Phase2 { get; private set; }
        public int Tackles, TackleWhiffs;
        bool tauntHit, tauntPaid, counterArmed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { FirstBlock = null; firstBlockShown = false; }

        /// 적 정의를 몸(Fighter)에 넣는다
        public void Setup(EnemyDef def, Fighter target, AttackDirector director, int index)
        {
            Def = def;
            if (Me == null) Me = GetComponent<Fighter>();
            if (Body == null) Body = GetComponent<FighterBody>();
            Target = target;
            Me.Label = def.Label;
            Me.MaxHp = Me.Hp = def.Hp;
            Me.Armor = def.Armor;
            Me.ArmorMax = def.ArmorGauge;
            Me.GuardCap = def.GuardMax;
            Me.GuardRatio = 0f;
            Me.GrabHoldTime = def.GrabHold;
            Me.Tuning = Tuning;
            Me.ResetFighter();
            rng = new System.Random(T.Seed * 31 + index * 7919 + (int)def.Type * 131);
            if (director != null) director.Add(this);
            SlotJitter = (float)(rng.NextDouble() * 1.1 - 0.5);
            repick = 1.5f + (float)rng.NextDouble() * 1.5f;
            tauntRoll = 2f;
        }

        void OnEnable() { Fighter.AttackStarted += OnAttackStarted; if (Me != null) Me.Hurt += OnHurt; }
        void OnDisable() { Fighter.AttackStarted -= OnAttackStarted; if (Me != null) Me.Hurt -= OnHurt; }

        void Awake()
        {
            if (Me == null) Me = GetComponent<Fighter>();
            if (Body == null) Body = GetComponent<FighterBody>();
        }

        void Start() { if (Me != null) { Me.Hurt -= OnHurt; Me.Hurt += OnHurt; } }

        /// 전투 시작: 접근
        public void Activate()
        {
            if (Me.State == Fighter.Phase.Out) return;
            SlotSet = false;
            WaitSince = clock;      // 굶김 시계: 전투 시작 또는 마지막 공격 끝부터(도발·피격으로 다시 세지 않음 — 4-3 '15초')
            Go(S.Approach);
        }

        public void ForceBlock(float seconds) { Go(S.Block); blockUntil = clock + seconds; }

        /// 인카운터 다시(패배 뒤 '다시' · '전투 다시'): 처음 상태로 — 대기 자리에서 HP 가득, 공격권·도주·페이즈 지움
        public void ResetBrain(Vector3 feet, float yaw)
        {
            gameObject.SetActive(true);
            Release();
            HasToken = false;
            Fled = false;
            Phase2 = false;
            pending = null; lastRun = null;
            counterArmed = false;
            cooldown = 0f; stumbleLeft = backLeft = 0f;
            if (Def != null) Me.ArmorMax = Def.ArmorGauge;
            Me.ResetFighter();
            Me.Body?.Place(feet, yaw);
            if (Body != null) Body.WalkVelocity = Vector3.zero;
            State = S.Idle;
            stateT = 0f;
        }

        /// 이긴 뒤: 탈락한 적이 일어나 달아남(3초 뒤 사라짐)
        public void Leave()
        {
            Release();
            if (Me.State == Fighter.Phase.Out || Me.Down) Me.ResetFighter();
            Me.ScriptAnim = null;
            Me.Leaving = true;
            Fled = true;
            Go(S.Leave);
        }

        /// 기세 액션 시작: 하던 공격을 거두고 공격권을 돌려준 뒤 멈춤(끝나면 Frozen = false 로 간보기부터)
        public void Freeze(bool on)
        {
            Frozen = on;
            if (!on) { if (State == S.AttackIn || State == S.Attack || State == S.Block || State == S.Taunt) Go(S.Strafe); return; }
            if (Me != null && Me.State == Fighter.Phase.Act) Me.CancelAttack();
            if (Me != null) Me.Guarding = false;
            Release();
            pending = null;
            counterArmed = false;
        }

        void Go(S s)
        {
            if (s == State) return;
            if (State == S.Taunt) Me.StaggerMul = 1f;
            State = s;
            stateT = 0f;
            if (s != S.Block && Me != null) Me.Guarding = false;
        }

        /// 공격권을 받았다(AttackDirector)
        internal void Grant()
        {
            HasToken = true;
            pending = Choose();
            inStart = Me.Position;
            if (pending == null)
            {
                // 물러나기(1.0초 뒤로) — 공격권은 돌려줌
                Release();
                cooldown = 0.6f;
                backLeft = 1.0f;
                Go(S.BackOff);
                return;
            }
            Attacks++;
            if (pending == Def.Tackle) Tackles++;
            Go(S.AttackIn);
        }

        MoveDef Choose()
        {
            if (Def.Tackle != null && Target != null)
            {
                // 야차 상대(4-6): 거리 > 3m → 태클(페이즈 1 50% · 2 80%) / 다가가서 가까운 기술, ≤ 1.5m → 밀기·휘두르기 40·40 · 물러나기 20
                HitResolver.Measure(Me.Position, Me.Yaw, Target.Position, Target.Radius, out _, out float s2, out _);
                if (s2 > 3.0f && rng.NextDouble() < (Phase2 ? Def.TackleChance2 : Def.TackleChance)) return Def.Tackle;
                if (s2 <= 1.5f && rng.NextDouble() < Def.BackOff) return null;      // 물러나기
                var ms = Phase2 && Def.Moves2 != null && Def.Moves2.Length > 0 ? Def.Moves2 : Def.Moves;
                return ms[rng.NextDouble() < 0.5 ? 0 : Mathf.Min(1, ms.Length - 1)];
            }
            if (Def.Far != null && Target != null)
            {
                HitResolver.Measure(Me.Position, Me.Yaw, Target.Position, Target.Radius, out _, out float surf, out _);
                if (surf > 2.0f && rng.NextDouble() < Def.FarChance) return Def.Far;
            }
            if (Def.Moves == null || Def.Moves.Length == 0) return MoveLib.EnemyJab();
            float sum = 0f;
            for (int i = 0; i < Def.Moves.Length; i++) sum += i < Def.Weights.Length ? Def.Weights[i] : 1f;
            double r = rng.NextDouble() * sum;
            for (int i = 0; i < Def.Moves.Length; i++)
            {
                r -= i < Def.Weights.Length ? Def.Weights[i] : 1f;
                if (r <= 0) return Def.Moves[i];
            }
            return Def.Moves[Def.Moves.Length - 1];
        }

        void StartMove(MoveDef m)
        {
            double pre = m.PreTime + (AttackDirector.OnScreen(Camera.main, Me) ? 0.0 : T.OffscreenDelay);
            lastRun = Me.StartAttack(m, Target, -pre);
            Go(S.Attack);
            AttackBegan?.Invoke(this, m);
        }

        void Release()
        {
            if (!HasToken) return;
            if (Director != null) Director.Release(this);
            else HasToken = false;
        }

        // ───────────────────────── 한 프레임
        void Update()
        {
            float dt = TimeFx.Dt;
            if (dt <= 0f || Me == null) return;
            clock += dt;
            stateT += dt;
            var t = T;

            // Fighter 상태 맞추기
            switch (Me.State)
            {
                case Fighter.Phase.Out:
                    if (State != S.Out) { Release(); Go(S.Out); }
                    break;
                case Fighter.Phase.Fall:
                case Fighter.Phase.Lie:
                case Fighter.Phase.GetUp:
                    if (State != S.Down) { Release(); Go(S.Down); }
                    break;
                case Fighter.Phase.Stagger:
                case Fighter.Phase.Grabbed:
                    if (State != S.Hurt) { if (HasToken) WaitSince = clock; Release(); Go(S.Hurt); }
                    break;
            }
            if ((State == S.Hurt || State == S.Down) && Me.State == Fighter.Phase.Free) Go(S.Strafe);
            if (!Phase2 && Def != null && Def.Phase2Hp > 0f && Me.Hp > 0 && Me.Hp <= Me.MaxHp * Def.Phase2Hp)
            {
                Phase2 = true;
                Me.ArmorMax = Def.ArmorGauge2;
                if (State == S.Taunt) Go(S.Strafe);
            }
            if (State == S.Leave)
            {
                // 이긴 뒤 탈락한 적이 일어나 달아남(2-5 '결과' — 14 Run_02)
                var away = Target != null ? HitResolver.Flat(Me.Position - Target.Position) : Me.Forward;
                var v = (away.sqrMagnitude > 1e-4f ? away.normalized : Me.Forward) * 4.5f;
                if (Body != null) { Body.WalkVelocity = v; Body.SetYaw(Mathf.MoveTowardsAngle(Me.Yaw, HitResolver.Yaw(v), 540f * dt)); }
                if (stateT > 3f) gameObject.SetActive(false);
                return;
            }
            if (Frozen)
            {
                // 기세 액션: 멈춰 서서 본다(공격권·막기·도발 시계도 멈춤)
                if (Body != null) Body.WalkVelocity = Vector3.zero;
                if (Target != null && Me.State == Fighter.Phase.Free)
                {
                    var to = HitResolver.Flat(Target.Position - Me.Position);
                    if (to.sqrMagnitude > 1e-4f) Body.SetYaw(Mathf.MoveTowardsAngle(Me.Yaw, HitResolver.Yaw(to), 360f * dt));
                }
                return;
            }
            if (cooldown > 0f) cooldown -= dt;
            if (State == S.Stumble)
            {
                // 태클 헛방(4-6): 비틀걸음 — 등 노출, 맞으면 경직 ×1.5. 끝나면 간보기
                stumbleLeft -= dt;
                if (Body != null) Body.WalkVelocity = Vector3.zero;
                if (stumbleLeft <= 0f || Me.State != Fighter.Phase.Free) { Me.ScriptAnim = null; Me.StaggerMul = 1f; Go(S.Strafe); }
                return;
            }

            // 도주(깐족이: HP ≤ 25% + 동료 모두 탈락)
            if (!Fled && Def != null && Def.FleeHp > 0f && Me.Hp > 0 && Me.Hp <= Me.MaxHp * Def.FleeHp && AlliesGone() && Me.State == Fighter.Phase.Free
                && (State == S.Strafe || State == S.Approach))
            {
                Fled = true;
                Release();
                Go(S.Flee);
            }

            Vector3 vel = Vector3.zero;
            bool face = Me.State == Fighter.Phase.Free;
            switch (State)
            {
                case S.Idle:
                    // 대기(인카운터 시작 전): 12m 안에 오면 본다
                    face = Target != null && Me.State == Fighter.Phase.Free && HitResolver.Flat(Target.Position - Me.Position).magnitude < 12f;
                    break;
                case S.BackOff:
                    if (Target != null)
                    {
                        var away = HitResolver.Flat(Me.Position - Target.Position);
                        vel = (away.sqrMagnitude > 1e-4f ? away.normalized : -Me.Forward) * Def.StrafeSpeed;
                    }
                    backLeft -= dt;
                    if (backLeft <= 0f) Go(S.Strafe);
                    break;
                case S.Approach:
                    vel = Steer(Slot, Def.ApproachSpeed);
                    if (Target != null && HitResolver.Flat(Me.Position - Target.Position).magnitude <= t.RingFar + 0.6f) Go(S.Strafe);
                    face = vel.sqrMagnitude < 0.01f;
                    if (!face && vel.sqrMagnitude > 0.01f) Body.SetYaw(Mathf.MoveTowardsAngle(Me.Yaw, HitResolver.Yaw(vel), 540f * dt));
                    break;
                case S.Strafe:
                    vel = Steer(Slot, Def.StrafeSpeed);
                    repick -= dt;
                    if (repick <= 0f)
                    {
                        repick = 1.5f + (float)rng.NextDouble() * 1.5f;
                        SlotAngle += (float)(rng.NextDouble() * 40.0 - 20.0);
                        SlotJitter = (float)(rng.NextDouble() * 1.1 - 0.5);
                    }
                    tauntRoll -= dt;
                    if (tauntRoll <= 0f)
                    {
                        tauntRoll = 2f;
                        if (!HasToken && !Phase2 && Def.TauntChance > 0f && rng.NextDouble() < Def.TauntChance) { Taunts++; tauntHit = tauntPaid = false; Me.StaggerMul = t.TauntMul; Go(S.Taunt); }
                    }
                    break;
                case S.Taunt:
                    if (!tauntPaid && !tauntHit && stateT >= t.TauntHold)
                    {
                        tauntPaid = true;
                        var pc = Target != null ? Target.GetComponent<PlayerCombat>() : null;
                        if (pc != null && pc.Active) pc.Heat.Add(t.TauntHeat, false);     // 도발에 안 넘어가면(기획서 2장)
                    }
                    if (stateT >= t.TauntTime) { Me.StaggerMul = 1f; Go(S.Strafe); }
                    break;
                case S.AttackIn:
                    if (pending == null || Target == null) { Release(); Go(S.Strafe); break; }
                    {
                        HitResolver.Measure(Me.Position, Me.Yaw, Target.Position, Target.Radius, out _, out float surf, out _);
                        bool far = pending.ChargeTime > 0f || pending.ActiveAdvance > 0f;
                        float moved = HitResolver.Flat(Me.Position - inStart).magnitude;
                        if (far || surf <= pending.Range - 0.05f || moved >= t.AttackInMax || stateT > 1.2f)
                        {
                            Body.WalkVelocity = Vector3.zero;
                            StartMove(pending);
                            vel = Vector3.zero;
                        }
                        else vel = HitResolver.Flat(Target.Position - Me.Position).normalized * Def.AttackInSpeed;
                    }
                    break;
                case S.Attack:
                    face = false;
                    if (Me.Run == null && Me.State == Fighter.Phase.Free)
                    {
                        cooldown = Phase2 ? Def.Cooldown2 : Def.Cooldown;
                        Release();
                        WaitSince = clock;
                        if (lastRun != null && lastRun.Move == Def.Tackle && lastRun.Hits == 0)
                        {
                            // 헛방 → 비틀(562) 1.2초(페이즈 2 0.9)
                            TackleWhiffs++;
                            stumbleLeft = Phase2 ? Def.Stumble2 : Def.Stumble;
                            Me.ScriptAnim = "Stumble";
                            Me.ScriptRate = 1f;
                            Me.StaggerMul = 1.5f;
                            Go(S.Stumble);
                        }
                        else Go(S.Strafe);
                    }
                    break;
                case S.Block:
                    Me.Guarding = true;
                    if (counterArmed && clock <= counterBefore && Def.Counter != null && !NoCounter && Director != null
                        && Director.Attacking < Director.Max && Director.PlayerOpen)
                    {
                        counterArmed = false;
                        HasToken = true;
                        Counters++;
                        Director.Starts.Add((this, Director.Clock));
                        Me.Guarding = false;
                        StartMove(Def.Counter);
                        break;
                    }
                    if (counterArmed && clock > counterBefore) counterArmed = false;
                    if (clock > blockUntil) Go(S.Strafe);
                    break;
                case S.Flee:
                    if (Target != null)
                    {
                        var away = HitResolver.Flat(Me.Position - Target.Position);
                        float d = away.magnitude;
                        vel = (d > 1e-3f ? away / d : Me.Forward) * Def.ApproachSpeed;
                        face = false;
                        Body.SetYaw(Mathf.MoveTowardsAngle(Me.Yaw, HitResolver.Yaw(vel), 540f * dt));
                        // 무대 가장자리(구경꾼)에 막혀 되돌아옴
                        if (d >= 5.6f || stateT > 3f) Go(S.Strafe);
                    }
                    break;
            }

            if (Me.State != Fighter.Phase.Free || (HoldPosition && State != S.AttackIn && State != S.Flee)) vel = Vector3.zero;
            if (Body != null) Body.WalkVelocity = vel;
            if (face && Target != null)
            {
                var to = HitResolver.Flat(Target.Position - Me.Position);
                if (to.sqrMagnitude > 1e-4f) Body.SetYaw(Mathf.MoveTowardsAngle(Me.Yaw, HitResolver.Yaw(to), 360f * dt));
            }
        }

        bool AlliesGone()
        {
            if (Director == null) return false;
            foreach (var m in Director.Members) if (m != null && m != this && !m.Eliminated) return false;
            return true;
        }

        /// 자리 쪽으로(가까우면 느리게) + 서로 밀어내기(0.9m, 시우와는 1.5m)
        Vector3 Steer(Vector3 goal, float speed)
        {
            var t = T;
            var p = Me.Position;
            var d = HitResolver.Flat(goal - p);
            float dist = d.magnitude;
            var v = dist > 0.25f ? d / dist * Mathf.Min(speed, dist * 3f) : Vector3.zero;
            foreach (var f in Fighter.All)
            {
                if (f == null || f == Me || !f.isActiveAndEnabled) continue;
                var off = HitResolver.Flat(p - f.Position);
                float r = off.magnitude;
                float lim = t.Separate + (f == Target ? 0.6f : 0f) + (f.Radius - 0.3f) + (Me.Radius - 0.3f);
                if (r < lim && r > 1e-3f) v += off / r * (lim - r) * 3f;
            }
            float max = speed * 1.2f;
            return v.sqrMagnitude > max * max ? v.normalized * max : v;
        }

        // ───────────────────────── 막기형(4-5)
        void OnAttackStarted(Fighter atk, AttackRun run)
        {
            if (Frozen || Def == null || !Def.Blocks || atk != Target || Me == null || Me.State != Fighter.Phase.Free) return;
            if (!(State == S.Strafe || State == S.Block || State == S.Approach || State == S.Taunt)) return;
            var m = run.Move;
            HitResolver.Measure(Me.Position, Me.Yaw, atk.Position, atk.Radius, out _, out float surf, out float ang);
            if (surf > m.Range + 0.5f || ang > 70f) return;
            double hold = Math.Max(0.0, m.ActiveEnd - run.T) + T.BlockHold;
            if (State == S.Block) { blockUntil = Math.Max(blockUntil, clock + hold); return; }
            if (!RollBlock(run)) return;
            BlocksStarted++;
            Go(S.Block);
            blockUntil = clock + hold;
            if (!firstBlockShown) { firstBlockShown = true; FirstBlock?.Invoke(this); DebugHud.Toast("막는 상대엔 □→△ 어퍼, 또는 ○ 잡기", 3f); }
        }

        /// 이 공격을 막기 시작할지 한 번 굴린다(난수 = 시드 고정) — 테스트가 확률을 직접 잰다
        public bool RollBlock(AttackRun run)
        {
            float p = BlockOverride >= 0f ? BlockOverride : BlockChance(run);
            return p > 0f && rng.NextDouble() < p;
        }

        /// 막기 확률(4-5): 약 1·2타 50% · 3·4타 90% · △ 앞차기·큰 훅 40% · 어퍼·잡기(그리고 반격)는 못 막음
        public static float BlockChance(AttackRun run)
        {
            switch (run.Move.State)
            {
                case "Jab": case "Cross": case "Hook": case "CrossEnd":
                    return run.Combo >= 3 ? 0.9f : 0.5f;
                case "FrontKick": case "BigHook":
                    return 0.4f;
                default:
                    return 0f;
            }
        }

        void OnHurt(HitEvent e)
        {
            if (State == S.Taunt) tauntHit = true;
            if (e.Outcome == HitOutcome.Blocked && Def != null && Def.Counter != null && !NoCounter && rng.NextDouble() < T.CounterChance)
            {
                counterArmed = true;
                counterBefore = clock + T.CounterWindow;
            }
        }
    }
}
