// 행인1의 메인이벤트 — 싸우는 사람 공통 (docs/08_M2_전투_설계.md 3-2·3-6·4-1·10-1·10-3)
// HP·팀·상태(자유/공격/행동/경직/다운/잡힘/탈락), 피격 처리(막기·버팀·슈퍼아머·경직·넉백·다운·무적), 기술 실행(AttackRun).
// 시우(PlayerCombat)와 적(EnemyBrain, 7단계)이 같이 쓴다. 기술 진행은 '초'로 재고 판정은 구간 방식(HitResolver).
// 실행 순서 −5: 입력(PlayerCombat −45) 다음, 몸(PlayerMotor·FighterBody 0) 전 — 자석·넉백이 같은 프레임 이동에 들어간다.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Haengin
{
    /// 몸(이동기) — 시우는 PlayerMotor, 적·허수아비는 FighterBody
    public interface IBody
    {
        Vector3 Position { get; }
        float Yaw { get; }
        void SetYaw(float yaw);
        /// 외부 이동(자석·넉백·루트 이동) — CharacterController.Move 로 들어가 벽은 못 뚫는다
        void Push(Vector3 delta);
        /// 걷던 속도를 0 으로
        void Halt();
        /// 지난 이동에서 옆으로 부딪힌 것(벽꽝 검사)
        Collider SideHit { get; }
        void Place(Vector3 feet, float yaw);
    }

    /// PlayerMotor 를 IBody 로
    public sealed class PlayerBody : IBody
    {
        readonly PlayerMotor m;
        public PlayerBody(PlayerMotor motor) { m = motor; }
        public Vector3 Position => m.Position;
        public float Yaw => m.Yaw;
        public void SetYaw(float yaw) => m.SetYaw(yaw);
        public void Push(Vector3 delta) => m.AddDisplacement(delta);
        public void Halt() => m.Halt();
        public Collider SideHit => m.LastSideHit;
        public void Place(Vector3 feet, float yaw) => m.Teleport(feet, yaw);
    }

    /// 실행 중인 기술 하나
    public sealed class AttackRun
    {
        public MoveDef Move;
        public Fighter Owner, Aim, Fixed;
        public double T;
        public int Combo;
        public float TurnLeft, MagnetTotal, MagnetLeft, MagnetUsed;
        /// 닿는 거리 자석의 멈출 표면 거리(m, 음수 = 예전 자석: 사거리 − 0.15)
        public float Contact = -1f;
        public bool Started, FirstTick = true;
        public int Hits;
        public readonly List<Fighter> Done = new List<Fighter>();
        /// 판정 안에 든 대상마다(잡기처럼 보통 피격 대신 따로 처리) — true 면 처리함
        public Func<AttackRun, Fighter, bool> OnTarget;
        public Action<AttackRun> OnEnd;
        /// 이 기술이 이어 받은 앞 기술(원투의 2타)
        public AttackRun FollowOf;
        // 이번 프레임 구간(실행 순서와 무관하게 '이번 프레임'을 말하려고)
        public double FrameT0, FrameT1;
        public int TickFrame = -1;

        public bool InStartup => T < Move.ActiveStart - HitResolver.Eps;
        public bool PastActive => T >= Move.ActiveEnd - HitResolver.Eps;
        public bool LinkOpen => T >= Move.LinkAt - HitResolver.Eps;
        public bool Finished => T >= Move.Total - HitResolver.Eps;
        /// 이번 프레임이 판정 구간과 겹치는가
        public bool ActiveNow
        {
            get
            {
                double a = TickFrame == Time.frameCount ? FrameT0 : T, b = TickFrame == Time.frameCount ? FrameT1 : T + TimeFx.Dt;
                return HitResolver.Overlaps(a, b, Move.ActiveStart, Move.ActiveEnd);
            }
        }
        /// 이번 프레임 구간의 시작(초)
        public double NowT => TickFrame == Time.frameCount ? FrameT0 : T;
    }

    [DefaultExecutionOrder(-5), DisallowMultipleComponent]
    public sealed class Fighter : MonoBehaviour
    {
        public enum Phase { Free, Act, Busy, Stagger, Fall, Lie, GetUp, Grabbed, Out }

        public static readonly List<Fighter> All = new List<Fighter>();
        /// 모든 타격(테스트·HUD·디버그)
        public static event Action<HitEvent> AnyHit;
        /// 누가 기술을 시작했다(막기형 적이 시우 공격 발생 시작을 본다 — 08 4-5)
        public static event Action<Fighter, AttackRun> AttackStarted;
        /// 다운으로 바닥에 닿았다(착지 쿵 — 이펙트·효과음·흔들림)
        public static event Action<Fighter> DownLanded;

        [Header("몸")]
        public int Team;
        public string Label = "?";
        public bool IsPlayer;
        public float Radius = 0.3f, Height = 1.74f, ChestHeight = 1.25f;
        [Header("체력")]
        public int MaxHp = 100;
        public int Hp = 100;
        [Tooltip("슈퍼아머(덩치·야차, 08 4-1): 경직 게이지가 남아 있는 동안 기세·다운 기술이 아닌 타격에 경직 없음(젖힘 0.5배)")] public bool Armor;
        [Tooltip("경직 게이지 최대(냉장고 30 · 스크럼 40/50). 0 이 되면 60f 경직(열림, 이때 잡힘) → 3초 뒤 가득")] public float ArmorMax = 30f;
        [Tooltip("막을 때 받는 피해 비율(음수 = 조정값 0.2 — 시우). 적은 0(08 4-5)")] public float GuardRatio = -1f;
        [Tooltip("가드 게이지 최대(음수 = 조정값 100). 석 달 60")] public float GuardCap = -1f;
        [Tooltip("받는 경직 배율(도발 중 1.5 — 08 4-1)")] public float StaggerMul = 1f;
        [Tooltip("시우에게 잡혔을 때 뿌리치기까지(초, 음수 = 조정값 2.0) — 깐족이 1.6 · 석 달 1.8")] public float GrabHoldTime = -1f;
        [Tooltip("디버그·연출: 무적")] public bool DebugInvuln;
        [Tooltip("시험용: 늘 막기(허수아비)")] public bool AlwaysGuard;
        [Header("연출(기세 액션 — 08 3-8)")]
        [Tooltip("기세 액션 중 다른 적: 무적(맞지 않음)")] public bool Shielded;
        [Tooltip("기세 액션 대상: HP 가 0 이 돼도 다운 기술이 아니면 서 있다(연출은 끝까지)")] public bool KeepStanding;
        [Tooltip("연출이 정한 애니메이터 상태(비우면 상태대로) — 되받기 비틀걸음 562")] public string ScriptAnim;
        public float ScriptRate = 1f;
        /// 다운 모양: 쓰러짐(366) / 벽에 기대 한쪽 무릎(365 앞부분 — 벽 러시 마무리) / 크게 날아감(187 — 구경꾼 되받기 마무리)
        public enum DownLook { Fall, Kneel, Big }
        public DownLook DownPose;
        public CombatTuning Tuning;
        public HitReact React;

        public IBody Body;
        public CombatTuning T => Tuning != null ? Tuning : CombatTuning.Default;

        // ── 상태
        public Phase State { get; private set; } = Phase.Free;
        public AttackRun Run { get; private set; }
        public float StaggerLeft { get; private set; }
        public bool Crouch { get; private set; }
        public float InvulnLeft { get; private set; }
        public bool KO => Hp <= 0;
        public bool Down => State == Phase.Fall || State == Phase.Lie || State == Phase.GetUp;
        /// 조준·판정 대상이 되는가(서 있음 — 잡힘·다운·탈락 아님)
        public bool Targetable => isActiveAndEnabled && State != Phase.Out && !Down && State != Phase.Grabbed;
        /// 공격 예고 중: 예고가 있는 기술의 판정 전(소프트 조준 우선·위협 중심 무게 2·락온 다음 대상 우선)
        public bool Telegraphing => Run != null && Run.Move.Warn > 0 && Run.T < Run.Move.ActiveStart - HitResolver.Eps;
        /// 경직 게이지(슈퍼아머)
        public float ArmorGauge { get; private set; } = 30f;
        /// 경직 게이지가 깨져 열린 동안(3초 뒤 가득 회복될 때까지 — 보통 타격처럼 경직)
        public bool ArmorBroken { get; private set; }
        float armorIdle;
        public float GuardMaxNow => GuardCap > 0f ? GuardCap : T.GuardMax;
        /// 회피 무적(PlayerCombat 이 줌)
        public Func<bool> IFrames;
        public bool InIFrames => IFrames != null && IFrames();
        public bool Invulnerable => DebugInvuln || Shielded || InvulnLeft > 0f || Down || State == Phase.Out || InIFrames;
        public float Guard { get; set; }
        /// 지금 막는 중(주인이 매 프레임 넣음)
        public bool Guarding;
        float guardIdle;
        public Fighter GrabbedBy { get; private set; }
        public double LieTotal { get; private set; }
        double stateT;
        int mash;

        // ── 주인 연결(시우)
        /// 적 공격의 판정이 시작될 때 내가 그 부채꼴 안이었다(무적 포함) — true 를 돌려주면 '읽었다': 그 공격은 나를 빼고 계속
        public Func<Fighter, MoveDef, bool> Threat;
        /// '회피 안 했으면 있었을 자리'(읽었다 판정 — 회피 시작 자리). 없으면 지금 자리
        public Func<Vector3> ThreatPoint;
        /// 약타를 경직 없이 버틸 수 있으면 true(전진 버팀)
        public Func<MoveDef, bool> Brace;
        /// 이 몸의 클립이 그 기술에서 실제로 닿는 거리(루트 → 치는 끝, m). 음수 = 모름(캡슐 몸) → 예전 자석. FighterAnim 이 넣음
        public Func<MoveDef, float> ReachOf;
        public event Action<HitEvent> Hurt;
        public event Action<HitEvent> Landed;
        public event Action<AttackRun> MoveEnded;
        /// 맞거나 잡혀서 하던 행동(공격·회피·잡기)이 끊겼다
        public event Action Interrupted;
        public event Action<Fighter> Downed;

        // ── 넉백
        Vector3 kbVel;
        struct Shove { public Vector3 Dir; public float Dist, Time, T; public bool Ease; public Fighter By; public bool Slam; }
        readonly List<Shove> shoves = new List<Shove>();
        static readonly List<Fighter> tmp = new List<Fighter>();

        public Vector3 Position => Body != null ? Body.Position : transform.position;
        public float Yaw => Body != null ? Body.Yaw : transform.eulerAngles.y;
        public Vector3 Forward => HitResolver.YawDir(Yaw);
        public Vector3 Chest => Position + Vector3.up * ChestHeight;

        void Awake()
        {
            if (Body == null) Body = FindBody();
            if (React == null) React = GetComponentInChildren<HitReact>(true);
            Guard = GuardMaxNow;
            ArmorGauge = ArmorMax;
        }

        IBody FindBody()
        {
            var fb = GetComponent<FighterBody>();
            if (fb != null) return fb;
            var pm = GetComponent<PlayerMotor>();
            return pm != null ? new PlayerBody(pm) : null;
        }

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { All.Clear(); AnyHit = null; AttackStarted = null; DownLanded = null; }

        /// 처음 상태로(HP 가득, 자유)
        public void ResetFighter(int hp = -1)
        {
            if (hp > 0) MaxHp = hp;
            Hp = MaxHp;
            State = Phase.Free;
            Run = null;
            StaggerLeft = InvulnLeft = 0f;
            Crouch = false;
            Guard = GuardMaxNow;
            ArmorGauge = ArmorMax;
            ArmorBroken = false;
            StaggerMul = 1f;
            GrabbedBy = null;
            kbVel = Vector3.zero;
            shoves.Clear();
            Shielded = KeepStanding = false;
            ScriptAnim = null;
            DownPose = DownLook.Fall;
            if (React != null) React.ResetPose();
        }

        // ───────────────────────── 기술
        /// 기술 시작. aim = 소프트 조준·락온 대상(몸 돌리기·자석), fixedTarget = 판정 없이 이 대상만(클린치 무릎·밀기).
        /// t0 = 연결 창에서 넘친 시간(+) 또는 발생 앞 준비 시간(−, 적의 예고·돌진 — MoveDef.PreTime)
        public AttackRun StartAttack(MoveDef m, Fighter aim, double t0 = 0, Fighter fixedTarget = null, int combo = 0)
        {
            var r = new AttackRun { Move = m, Owner = this, Aim = aim, Fixed = fixedTarget, T = t0, Combo = combo };
            if (aim != null && fixedTarget == null)
            {
                r.TurnLeft = T.AimTurnMax;
                HitResolver.Measure(Position, Yaw, aim.Position, aim.Radius, out _, out float surf, out _);
                r.MagnetTotal = HitResolver.Magnet(surf, m, T);
                // 자석 대상: 시작 때 사거리 + 0.8 안. 발생 동안 매 프레임 거리를 다시 잰다(넉백으로 물러나는 대상도 따라감 — 08 2-3 구현 메모)
                r.MagnetLeft = m.Magnet > 0f && surf <= m.Range + HitResolver.Reach(m, T) ? m.Magnet : 0f;
                // 닿는 거리 자석(08 11-3 결정 1, 아캄·용과 같이 방식): 판정 사거리는 그대로 두고, 클립이 실제로 닿는 거리(측정 뻗음 − 0.04)까지 붙인다.
                // 그만큼 미끄러지는 게 1.2m(반격처럼 자석이 더 큰 기술은 그 값)를 넘으면 예전 자석 그대로 — 판정만 하고 젖힘·밀림으로 거리감을 가린다
                float reach = m.Magnet > 0f && ReachOf != null ? ReachOf(m) : -1f;
                if (reach > 0f)
                {
                    float stop = Mathf.Max(0.05f, reach - T.ContactSink);
                    float lim = Mathf.Max(T.ContactMax, m.Magnet);
                    if (surf - stop <= lim) { r.Contact = stop; r.MagnetLeft = lim; r.MagnetTotal = Mathf.Max(0f, surf - stop); }
                }
            }
            Run = r;
            State = Phase.Act;
            Guarding = false;
            Body?.Halt();
            AttackStarted?.Invoke(this, r);
            return r;
        }

        public void CancelAttack()
        {
            if (Run == null) return;
            if (React != null) React.Strike(-1, 0f);
            Run = null;
            if (State == Phase.Act) State = Phase.Free;
        }

        /// 주인이 직접 모는 행동(회피·잡고 있기) 시작/끝
        public void SetBusy(bool on)
        {
            if (on) { Run = null; State = Phase.Busy; Guarding = false; }
            else if (State == Phase.Busy) State = Phase.Free;
        }

        void TickAttack(float dt)
        {
            var r = Run;
            var m = r.Move;
            double t0 = r.T, t1 = r.T + dt;
            double m0 = r.FirstTick ? 0 : t0;      // 연결 창에서 넘친 시간이 있어도 몸 돌리기·자석은 처음부터
            r.FirstTick = false;
            r.TickFrame = Time.frameCount;
            r.FrameT0 = t0; r.FrameT1 = t1;

            // 0) 발생 앞 준비(적: 예고 몸짓 → 돌진). 대상 쪽으로 돌고, 돌진 구간 [−ChargeTime, 0) 에는 대상 앞(사거리 − 0.15m)까지만 달려든다
            if (t0 < 0 && Body != null)
            {
                double pre1 = Math.Min(t1, 0.0);
                if (r.Aim != null)
                {
                    var to = HitResolver.Flat(r.Aim.Position - Position);
                    if (to.sqrMagnitude > 1e-6f) Body.SetYaw(Mathf.MoveTowardsAngle(Yaw, HitResolver.Yaw(to), 540f * dt));
                }
                if (m.ChargeTime > 0f && m.ChargeDist > 0f)
                {
                    double ov = HitResolver.Overlap(t0, pre1, -m.ChargeTime, 0);
                    if (ov > 0)
                    {
                        float step = (float)(m.ChargeDist * ov / m.ChargeTime);
                        if (r.Aim != null)
                        {
                            HitResolver.Measure(Position, Yaw, r.Aim.Position, r.Aim.Radius, out _, out float surf, out _);
                            step = Mathf.Min(step, Mathf.Max(0f, surf - (m.Range - T.MagnetMargin)));
                        }
                        if (step > 0f) Body.Push(Forward * step);
                    }
                }
            }

            // 1) 소프트 조준: 처음 AimTurnFrames 동안 최대 AimTurnMax° 대상 쪽으로
            if (r.Aim != null && r.TurnLeft > 0f && Body != null)
            {
                double tw = T.AimTurnFrames / MoveDef.Fps;
                double ov = HitResolver.Overlap(m0, t1, 0, tw);
                if (ov > 0)
                {
                    float allowed = (float)(T.AimTurnMax * ov / tw);
                    var to = HitResolver.Flat(r.Aim.Position - Position);
                    if (to.sqrMagnitude > 1e-6f)
                    {
                        float want = Mathf.DeltaAngle(Yaw, HitResolver.Yaw(to));
                        float lim = Mathf.Min(allowed, r.TurnLeft);
                        float step = Mathf.Clamp(want, -lim, lim);
                        Body.SetYaw(Yaw + step);
                        r.TurnLeft -= Mathf.Abs(step);
                    }
                }
            }

            // 2) 자석·들어가기: 발생 동안. 자석 = 지금 표면 거리가 사거리보다 멀면 (거리 − (사거리 − 0.15)) 를 남은 발생 시간에 나눠 미끄러짐(합계 최대 0.8m)
            double su = m.ActiveStart;
            if (su > 0 && Body != null && (r.MagnetLeft > 0f || m.Advance > 0f))
            {
                double ov = HitResolver.Overlap(m0, t1, 0, su);
                if (ov > 0)
                {
                    var d = Vector3.zero;
                    if (r.MagnetLeft > 0f && r.Aim != null)
                    {
                        HitResolver.Measure(Position, Yaw, r.Aim.Position, r.Aim.Radius, out _, out float surf, out _);
                        var to = HitResolver.Flat(r.Aim.Position - Position);
                        bool contact = r.Contact >= 0f;
                        float want = surf - (contact ? r.Contact : m.Range - T.MagnetMargin);
                        if ((contact || surf > m.Range || r.MagnetUsed > 0f) && want > 0f && to.sqrMagnitude > 1e-6f)
                        {
                            double left = Math.Max(1e-4, su - m0);
                            float step = Mathf.Min(r.MagnetLeft, want * (float)Math.Min(1.0, ov / left));
                            d += to.normalized * step;
                            r.MagnetLeft -= step;
                            r.MagnetUsed += step;
                        }
                    }
                    if (m.Advance > 0f) d += Forward * (m.Advance * (float)(ov / su));
                    if (d.sqrMagnitude > 0f) Body.Push(d);
                }
            }

            // 3) 판정: 이번 프레임 구간이 판정 구간과 겹치면(한 프레임에 다 지나가도 한 번)
            if (HitResolver.Overlaps(t0, t1, m.ActiveStart, m.ActiveEnd))
            {
                if (!r.Started)
                {
                    r.Started = true;
                    ThreatScan(r);
                }
                Resolve(r);
            }

            if (Run != r) return;     // 판정 처리 중 끊김(잡기 성공 등)
            r.T = t1;
            Pose(r);
            // 이어지는 기술(원투의 2타): 연결 창이 열리면 넘친 시간을 넘겨 바로
            if (m.Followup != null && r.LinkOpen)
            {
                var next = StartAttack(m.Followup, r.Aim, r.T - m.LinkAt, null, r.Combo + 1);
                next.FollowOf = r;
                r.OnEnd?.Invoke(r);
                MoveEnded?.Invoke(r);
                return;
            }
            if (r.Finished) EndAttack(r);
        }

        /// 임시 공격 자세(클립 연결 전, 9단계에서 클립으로): 발생 동안 뻗고 판정 동안 유지, 회복 동안 거둠
        void Pose(AttackRun r)
        {
            if (React == null) return;
            var m = r.Move;
            int limb; bool hook = false, upper = false;
            switch (m.ClipId)
            {
                case 191: limb = 0; break;
                case 192: limb = 1; break;
                case 193: limb = 0; hook = true; break;
                case 194: limb = 1; upper = true; break;
                case 195: limb = 1; hook = true; break;
                case 209: case 206: limb = 2; break;
                case 211: limb = 3; break;
                case 259: limb = 1; break;
                default: limb = -1; break;
            }
            double a0 = m.ActiveStart, a1 = m.ActiveEnd, end = m.Total;
            float amt = r.T < a0 ? (float)(r.T / Math.Max(1e-4, a0)) : r.T < a1 ? 1f : 1f - (float)((r.T - a1) / Math.Max(1e-4, end - a1));
            amt = Mathf.Clamp01(amt);
            React.Strike(limb, amt * amt * (3f - 2f * amt), hook, upper);
        }

        void EndAttack(AttackRun r)
        {
            if (React != null) React.Strike(-1, 0f);
            if (Run == r) { Run = null; if (State == Phase.Act) State = Phase.Free; }
            r.OnEnd?.Invoke(r);
            MoveEnded?.Invoke(r);
        }

        /// 판정이 시작되는 순간: 부채꼴 안(무적 포함)의 상대에게 알림 — 회피 무적 중이면 '읽었다'.
        /// '회피 안 했으면 맞았을 것'(08 3-5) = 회피를 시작한 자리(ThreatPoint)가 부채꼴 안인가로 본다(회피 이동으로 이미 빠져나갔어도)
        void ThreatScan(AttackRun r)
        {
            if (r.Fixed != null) return;
            var m = r.Move;
            foreach (var f in All.ToArray())
            {
                if (f == null || f == this || f.Team == Team || f.Threat == null || f.State == Phase.Out || f.Down || r.Done.Contains(f)) continue;
                var p = f.ThreatPoint != null ? f.ThreatPoint() : f.Position;
                if (!HitResolver.InFan(Position, Yaw, Radius, p, f.Radius, m.Range, m.HalfAngle, m.HeightTol)) continue;
                if (f.Threat(this, m))
                {
                    r.Done.Add(f);
                    var ev = new HitEvent { Attacker = this, Victim = f, Move = m, Outcome = HitOutcome.Read, Power = m.Power, Frame = Time.frameCount };
                    AnyHit?.Invoke(ev);
                }
            }
        }

        void Resolve(AttackRun r)
        {
            int left = r.Move.MaxTargets - r.Hits;
            if (left <= 0) return;
            if (r.Fixed != null)
            {
                if (r.Done.Contains(r.Fixed) || r.Fixed.State == Phase.Out) return;
                tmp.Clear();
                tmp.Add(r.Fixed);
            }
            else HitResolver.Targets(this, r.Move, All, r.Done, false, tmp);
            foreach (var f in tmp.ToArray())
            {
                if (left <= 0) break;
                r.Done.Add(f);
                if (r.OnTarget != null && r.OnTarget(r, f)) { left--; r.Hits++; if (Run != r) return; continue; }
                var ev = f.Receive(this, r.Move);
                left--; r.Hits++;
                RaiseLanded(ev);
                if (Run != r) return;
            }
        }

        public void RaiseLanded(HitEvent ev) => Landed?.Invoke(ev);

        // ───────────────────────── 맞기
        /// 공격을 받는다. 결과 기록을 돌려준다(막기·버팀·슈퍼아머·경직·다운)
        public HitEvent Receive(Fighter atk, MoveDef m)
        {
            var t = T;
            var ev = new HitEvent { Attacker = atk, Victim = this, Move = m, Power = m.Power, Frame = Time.frameCount, GameTime = Time.timeAsDouble };
            var dir = atk != null ? HitResolver.Flat(Position - atk.Position) : -Forward;
            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : (atk != null ? atk.Forward : -Forward);
            ev.Dir = dir;
            ev.Point = Chest - dir * Radius;
            var knockDir = m.KnockSide && atk != null ? Vector3.Cross(Vector3.up, atk.Forward) : dir;   // 왼손 훅: 공격자 오른쪽으로

            bool guarding = (Guarding || AlwaysGuard) && State == Phase.Free && !m.Unblockable && atk != null
                            && HitResolver.InGuardArc(this, atk.Position, t.GuardAngle);
            if (guarding && m.GuardBreak)
            {
                ev.Outcome = HitOutcome.GuardBroken;
                ev.Damage = m.Damage;
                Hp -= ev.Damage;
                Guard = GuardMaxNow;
                Interrupt();
                SetStagger(t.GuardBreakStagger / MoveDef.Fps);
                Knock(knockDir, m.Knock);
            }
            else if (guarding)
            {
                ev.Damage = HitResolver.BlockedDamage(m.Damage, GuardRatio >= 0f ? GuardRatio : t.GuardDmgRatio);
                Hp -= ev.Damage;
                Guard -= HitResolver.GuardCost(m, t);
                guardIdle = 0f;
                Knock(dir, m.BlockKnockDist);
                if (Guard <= 0f)
                {
                    ev.Outcome = HitOutcome.Crushed;
                    Guard = GuardMaxNow;
                    Interrupt();
                    SetStagger(t.CrushStagger / MoveDef.Fps);
                }
                else ev.Outcome = HitOutcome.Blocked;
            }
            else if (m.Power == Power.Light && !m.Down && Brace != null && State == Phase.Act && Brace(m))
            {
                ev.Outcome = HitOutcome.Braced;
                ev.Damage = m.Damage;
                Hp -= ev.Damage;
            }
            else if (Armor && !ArmorBroken && !m.IgnoreArmor && !m.Down && m.Power != Power.None && State != Phase.Grabbed && State != Phase.Stagger)
            {
                // 슈퍼아머(08 4-1): 경직 게이지 약 −6 · 중 −12 · 강 −30(스텝 무릎 −20). 남아 있으면 경직 없음, 0 이면 60f 경직(열림)
                ev.Damage = m.Damage;
                Hp -= ev.Damage;
                ArmorGauge -= m.StaggerGauge >= 0f ? m.StaggerGauge : t.ArmorCost[Mathf.Clamp((int)m.Power, 0, 4)];
                armorIdle = 0f;
                if (ArmorGauge > 1e-4f) ev.Outcome = HitOutcome.Armored;
                else
                {
                    ArmorGauge = 0f;
                    ArmorBroken = true;
                    ev.Outcome = HitOutcome.Hit;
                    Interrupt();
                    if (Hp > 0) { SetStagger(t.ArmorBreakStagger / MoveDef.Fps); Crouch = true; Knock(knockDir, m.Knock); }
                }
            }
            else
            {
                ev.Outcome = HitOutcome.Hit;
                ev.Damage = m.Damage;
                Hp -= ev.Damage;
                bool held = State == Phase.Grabbed && GrabbedBy == atk && m.Stagger <= 0 && !m.Down;   // 클린치 무릎: 잡힌 채
                // 슈퍼아머가 깨져 열린 동안 강타 = 다운(08 4-4 '경직 게이지 0 + 강타')
                bool openHeavy = Armor && ArmorBroken && State == Phase.Stagger && m.Power >= Power.Heavy;
                if (!held)
                {
                    if (State == Phase.Grabbed) Release(0f);
                    Interrupt();
                    if ((Hp <= 0 && !KeepStanding) || m.Down || openHeavy) StartDown(dir, m.Down ? m.DownKnock : openHeavy ? 1.0f : 0.3f);
                    else
                    {
                        SetStagger(m.Stagger * StaggerMul / MoveDef.Fps);
                        Crouch = m.Crouch;
                        Knock(knockDir, m.Knock);
                    }
                }
            }
            if (Hp <= 0)
            {
                Hp = 0;
                if (!Down && State != Phase.Out && !KeepStanding) { if (State == Phase.Grabbed) Release(0f); Interrupt(); StartDown(dir, 0.3f); }
            }
            ImpactFx.OnHit(ev, t);
            Hurt?.Invoke(ev);
            AnyHit?.Invoke(ev);
            return ev;
        }

        /// 하던 행동을 끊는다(공격·회피·잡고 있기)
        void Interrupt()
        {
            if (React != null) React.Strike(-1, 0f);
            Run = null;
            if (State == Phase.Act || State == Phase.Busy) State = Phase.Free;
            Interrupted?.Invoke();
        }

        public void SetStagger(float seconds)
        {
            if (State == Phase.Out || Down) return;
            if (State == Phase.Act || State == Phase.Busy) Interrupt();
            State = Phase.Stagger;
            StaggerLeft = Mathf.Max(StaggerLeft, seconds);
            if (seconds <= 0f) { State = Phase.Free; StaggerLeft = 0f; }
        }

        // ───────────────────────── 다운(3-6·4-1)
        public void StartDown(Vector3 dir, float knock)
        {
            if (State == Phase.Grabbed) Release(0f);
            Run = null;
            State = Phase.Fall;
            stateT = 0;
            mash = 0;
            StaggerLeft = 0f;
            Crouch = false;
            if (knock > 0f) Knock(dir, knock);
            Downed?.Invoke(this);
        }

        /// 누운 동안 공격 버튼 연타: 한 번에 −0.1초(최소 0.8초)
        public void Mash() { if (State == Phase.Lie || State == Phase.Fall) mash++; }

        // ───────────────────────── 넉백(08 3-2: 스프링 = 속도 거리×k, 감쇠 e^(−k·t) — 총거리 = 거리, 큰 넉백 ≥ 0.3m 는 0.25초 ease-out)
        public void Knock(Vector3 dir, float dist)
        {
            if (dist <= 0f) return;
            dir = HitResolver.Flat(dir);
            if (dir.sqrMagnitude < 1e-6f) return;
            dir.Normalize();
            if (dist >= T.BigKnock) shoves.Add(new Shove { Dir = dir, Dist = dist, Time = T.BigKnockTime, Ease = true });
            else kbVel += dir * (dist * T.KnockK);
        }

        /// 하체 밀기로 밀려남: 0.3초 ease-out, 벽에 닿으면 벽꽝(by 의 타격), 다른 적과 부딪히면 둘 다 경직
        public void Shoved(Vector3 dir, float dist, float time, Fighter by)
        {
            dir = HitResolver.Flat(dir).normalized;
            shoves.Add(new Shove { Dir = dir, Dist = dist, Time = time, Ease = true, By = by, Slam = true });
        }

        public bool Shoving { get { foreach (var s in shoves) if (s.Slam) return true; return false; } }

        void TickKnock(float dt)
        {
            if (Body == null) return;
            var d = Vector3.zero;
            if (kbVel.sqrMagnitude > 1e-8f)
            {
                float k = T.KnockK;
                float e = Mathf.Exp(-k * dt);
                d += kbVel * ((1f - e) / k);
                kbVel *= e;
                if (kbVel.sqrMagnitude < 1e-8f) kbVel = Vector3.zero;
            }
            for (int i = shoves.Count - 1; i >= 0; i--)
            {
                var s = shoves[i];
                float a = Mathf.Clamp01(s.T / s.Time), b = Mathf.Clamp01((s.T + dt) / s.Time);
                d += s.Dir * (s.Dist * (EaseOut(b) - EaseOut(a)));
                s.T += dt;
                if (s.Slam && CheckSlam(ref s)) { shoves.RemoveAt(i); continue; }
                if (s.T >= s.Time) shoves.RemoveAt(i);
                else shoves[i] = s;
            }
            if (d.sqrMagnitude > 0f) Body.Push(d);
        }

        static float EaseOut(float u) => 1f - (1f - u) * (1f - u) * (1f - u);

        /// 밀려나는 중 벽(Wall 레이어·HeatSurface)에 닿았나 / 다른 적과 부딪혔나
        bool CheckSlam(ref Shove s)
        {
            var hit = Body.SideHit;
            if (hit != null && (hit.gameObject.layer == Layers.Wall || hit.GetComponentInParent<HeatSurface>() != null) && s.By != null)
            {
                var ev = new HitEvent { Attacker = s.By, Victim = this, Move = MoveLib.Slam(T), Outcome = HitOutcome.Hit, Power = Power.Heavy, Frame = Time.frameCount, Dir = s.Dir };
                ev.Damage = T.SlamDamage;
                ev.Point = Chest + s.Dir * Radius;
                Hp -= ev.Damage;
                StaggerLeft = 0f;
                SetStagger(T.SlamStagger / MoveDef.Fps);
                if (Hp <= 0) { Hp = 0; StartDown(-s.Dir, 0f); }
                ImpactFx.OnHit(ev, T);
                Hurt?.Invoke(ev);
                AnyHit?.Invoke(ev);
                s.By.RaiseLanded(ev);
                return true;
            }
            foreach (var g in All)
            {
                if (g == this || g == s.By || g.Team != Team || !g.Targetable) continue;
                if (HitResolver.Flat(g.Position - Position).magnitude > Radius + g.Radius + 0.05f) continue;
                var bump = MoveLib.Bump(T);
                foreach (var who in new[] { this, g })
                {
                    var ev = new HitEvent { Attacker = s.By, Victim = who, Move = bump, Outcome = HitOutcome.Hit, Power = Power.Mid, Frame = Time.frameCount, Damage = T.BumpDamage, Point = who.Chest };
                    who.Hp -= ev.Damage;
                    who.StaggerLeft = 0f;
                    who.SetStagger(T.BumpStagger / MoveDef.Fps);
                    ImpactFx.OnHit(ev, T);
                    who.Hurt?.Invoke(ev);
                    AnyHit?.Invoke(ev);
                    if (s.By != null) s.By.RaiseLanded(ev);
                }
                return true;
            }
            return false;
        }

        // ───────────────────────── 잡힘(3-4)
        public void Grabbed(Fighter by)
        {
            Interrupt();
            State = Phase.Grabbed;
            GrabbedBy = by;
            StaggerLeft = 0f;
            Crouch = false;
        }

        /// 잡힘 풀림 — stagger 초 경직(0 이면 바로 자유)
        public void Release(float stagger)
        {
            if (State != Phase.Grabbed) return;
            State = Phase.Free;
            GrabbedBy = null;
            if (stagger > 0f) SetStagger(stagger);
        }

        // ───────────────────────── 한 프레임
        void Update()
        {
            float dt = TimeFx.Dt;
            if (dt <= 0f) return;
            var t = T;
            if (InvulnLeft > 0f) InvulnLeft = Mathf.Max(0f, InvulnLeft - dt);

            // 가드 게이지: 0.8초 막지 않으면 30/초 회복
            guardIdle += dt;
            float gmax = GuardMaxNow;
            if (guardIdle >= t.GuardRegenDelay && Guard < gmax) Guard = Mathf.Min(gmax, Guard + t.GuardRegen * dt);
            // 경직 게이지: 마지막으로 깎인 뒤 3초면 가득(깨진 것도 풀림)
            if (Armor && (ArmorGauge < ArmorMax || ArmorBroken))
            {
                armorIdle += dt;
                if (armorIdle >= t.ArmorRefill) { ArmorGauge = ArmorMax; ArmorBroken = false; }
            }

            TickKnock(dt);

            switch (State)
            {
                case Phase.Act:
                    if (Run != null) TickAttack(dt);
                    else State = Phase.Free;
                    break;
                case Phase.Stagger:
                    StaggerLeft -= dt;
                    if (StaggerLeft <= 1e-5f) { StaggerLeft = 0f; State = Phase.Free; Crouch = false; }
                    break;
                case Phase.Fall:
                    stateT += dt;
                    if (React != null) React.DownAmount = Mathf.Clamp01((float)(stateT / t.DownFall));
                    if (stateT >= t.DownFall - 1e-5)
                    {
                        Shake.Add(t.DownTrauma);
                        DownLanded?.Invoke(this);
                        stateT = 0;
                        LieTotal = IsPlayer ? t.LiePlayer : t.LieEnemy;
                        State = KO ? Phase.Out : Phase.Lie;
                    }
                    break;
                case Phase.Lie:
                    stateT += dt;
                    double lie = IsPlayer ? Math.Max(t.LieMin, LieTotal - mash * t.LieMashCut) : LieTotal;
                    if (stateT >= lie - 1e-5) { stateT = 0; State = Phase.GetUp; }
                    break;
                case Phase.GetUp:
                    stateT += dt;
                    if (React != null) React.DownAmount = 1f - Mathf.Clamp01((float)(stateT / t.GetUp));
                    if (stateT >= t.GetUp - 1e-5)
                    {
                        State = Phase.Free;
                        DownPose = DownLook.Fall;
                        if (React != null) React.DownAmount = 0f;
                        InvulnLeft = IsPlayer ? t.AfterUpPlayer : t.AfterUpEnemy;
                    }
                    break;
            }
        }

        /// 지금 누워 있는 시간(초, 정보용)
        public double StateTime => stateT;
        public int MashCount => mash;
    }
}
