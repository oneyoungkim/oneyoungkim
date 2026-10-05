// 행인1의 메인이벤트 — 전투 클립 재생 (docs/08_M2_전투_설계.md 10-2 · 11장 9단계)
// 모델(Animator)에 붙는다. 상태 전이는 애니메이터에 그리지 않고 코드가 CrossFadeInFixedTime(상태, 0.05초)으로 넣는다(결정적·테스트 쉬움).
// 재생 배율(AtkRate) = 클립 타격 시각 ÷ (처음 본 순간부터 판정 첫 프레임까지 남은 시간) → 클립의 주먹이 정확히 판정 첫 프레임에 뻗는다(ClipTable = ClipSetup 측정).
// 배율은 1.6(MaxRate, 08 10-4 범위 위 끝)에서 자르고, 남는 만큼 클립 앞부분(준비 동작)을 건너뛰고 시작한다(클립들이 준비가 길어 표의 발생 0.10~0.27초에 맞추면 ×3~6 이 됨).
// 상태: Move(탐색 걷기·달리기, 적은 접근·도주) · CombatMove(전투 대기·이동: 89·21·20) · 기술(191~195·209·206·211·259 앞/뒤·510) · Dodge(156) ·
//       HitFace(174, 중)·HitBody(178, 강·잡힘) · Fall→Lie(187)·StandUp(344) · Kneel(365 앞부분, 탈락) · Taunt(88) · 위층 Guard(138 막는 프레임, 상체만).
// 젖힘·번쩍·셰이크(HitReact)는 그대로 위에 더하고, 클립 전 임시 공격 자세·다운 눕힘은 끈다(HitReact.UseClips).
using UnityEngine;

namespace Haengin
{
    [DefaultExecutionOrder(18), DisallowMultipleComponent, RequireComponent(typeof(Animator))]
    public sealed class FighterAnim : MonoBehaviour
    {
        public Fighter Me;
        public PlayerCombat Player;
        public EnemyBrain Brain;
        public ClipTable Clips;
        [Tooltip("상태 바꿀 때 섞는 시간(10-2: 0.05초)")] public float Fade = 0.05f;

        public static readonly int HRate = Animator.StringToHash("AtkRate"), HCX = Animator.StringToHash("CX"), HCY = Animator.StringToHash("CY"),
                                   HBlend = Animator.StringToHash("Blend"), HLocoRate = Animator.StringToHash("Rate"), HSpeed = Animator.StringToHash("Speed");
        public const int GuardLayer = 1;

        Animator anim;
        string cur;
        object curKey;
        float guardW, cx, cy, rate = 1f, loco, locoVel;
        Vector3 lastPos;
        int hurtCount;
        Power hurtPower;
        AttackRun seen;
        double preAtStart;     // 처음 본 순간 판정 첫 프레임까지 남은 시간(초)

        public string Current => cur;
        public float Rate => rate;
        public Animator Animator => anim;

        void Awake()
        {
            anim = GetComponent<Animator>();
            if (Me == null) Me = GetComponentInParent<Fighter>();
            if (Player == null) Player = GetComponentInParent<PlayerCombat>();
            if (Brain == null) Brain = GetComponentInParent<EnemyBrain>();
        }

        void OnEnable() { if (Me != null) Me.Hurt += OnHurt; }
        void OnDisable() { if (Me != null) Me.Hurt -= OnHurt; }

        void Start()
        {
            if (Me == null) Me = GetComponentInParent<Fighter>();
            if (Me != null) { Me.Hurt -= OnHurt; Me.Hurt += OnHurt; if (Me.React != null) Me.React.UseClips = Clips != null; lastPos = Me.Position; }
            if (Brain == null) Brain = GetComponentInParent<EnemyBrain>();
            if (Player == null) Player = GetComponentInParent<PlayerCombat>();
        }

        void OnHurt(HitEvent e)
        {
            if (e.Outcome == HitOutcome.Hit || e.Outcome == HitOutcome.GuardBroken || e.Outcome == HitOutcome.Crushed)
            {
                hurtCount++;
                hurtPower = e.Power;
            }
        }

        bool InCombat => Player != null ? Player.Active : Brain != null && Brain.State != EnemyBrain.S.Idle;

        [Tooltip("재생 배율 위 한도(08 10-4: 0.7~1.6). 클립 타격까지 더 빨라야 하면 그만큼 클립 앞부분(준비 동작)을 건너뛰고 시작")] public float MaxRate = 1.6f;

        /// 클립 타격 시각이 판정 첫 프레임(toHit 초 뒤)에 오게: 배율 = 타격 ÷ toHit(위 한도 MaxRate), 넘치면 앞을 건너뜀(offset)
        void ClipRate(string state, double toHit, out float r, out float offset)
        {
            r = 1f; offset = 0f;
            if (Clips == null || !Clips.TryGet(state, out var e) || e.Hit <= 0f || toHit <= 1e-4) return;
            float want = (float)(e.Hit / toHit);
            if (want <= MaxRate) { r = Mathf.Max(0.2f, want); return; }
            r = MaxRate;
            offset = Mathf.Max(0f, e.Hit - MaxRate * (float)toHit);
        }

        float FitRate(string state, float seconds)
        {
            if (Clips == null || !Clips.TryGet(state, out var e) || seconds <= 1e-4f) return 1f;
            return Mathf.Clamp(e.Length / seconds, 0.2f, 8f);
        }

        /// 지금 넣을 상태·배율·처음 위치(초)·다시 시작 열쇠(같은 상태라도 새 기술·새 피격이면 처음부터)
        void Decide(out string st, out float r, out float offset, out object key)
        {
            var t = Me.T;
            st = null; r = 1f; offset = 0f; key = null;
            switch (Me.State)
            {
                case Fighter.Phase.Out:
                    if (cur == "Fall" && Me.StateTime < 0.05) { st = "Fall"; r = rate; key = curKey; return; }
                    st = "Kneel"; r = 1f; key = "out"; return;
                case Fighter.Phase.Fall:
                    st = "Fall"; key = "down" + hurtCount;
                    r = Clips != null && Clips.TryGet("Fall", out var fe) && fe.Hit > 0f ? Mathf.Clamp(fe.Hit / t.DownFall, 0.3f, 6f) : 1f;
                    return;
                case Fighter.Phase.Lie:
                    st = "Lie"; key = "lie"; return;
                case Fighter.Phase.GetUp:
                    st = "StandUp"; key = "up" + hurtCount; r = FitRate("StandUp", t.GetUp); return;
                case Fighter.Phase.Grabbed:
                    st = "HitBody"; key = "grabbed"; r = Still; offset = 0.15f; return;
                case Fighter.Phase.Stagger:
                    if (hurtPower >= Power.Heavy) { st = "HitBody"; r = 1f; }
                    else if (hurtPower == Power.Mid) { st = "HitFace"; r = 1.3f; }
                    else { st = cur == "HitFace" || cur == "HitBody" ? cur : InCombat ? "CombatMove" : "Move"; r = 1.3f; }
                    key = st == "CombatMove" || st == "Move" ? (object)st : "hurt" + hurtCount;
                    return;
                case Fighter.Phase.Busy:
                    if (Player != null && Player.Dodging) { st = "Dodge"; key = "dodge"; r = FitRate("Dodge", t.DodgeTotal / MoveDef.Fps * 1.6f); return; }
                    if (Player != null && Player.Held != null) { Hold(out st, out r, out offset, out key); return; }
                    st = cur ?? "CombatMove"; key = curKey; r = rate; return;
                case Fighter.Phase.Act:
                    Attack(out st, out r, out offset, out key);
                    return;
            }
            // 자유(잡고 있는 동안 무릎 사이 한 프레임도 멱살 자세 유지)
            if (Player != null && Player.Held != null) { Hold(out st, out r, out offset, out key); return; }
            // 도발: 88 가슴 치기. 냉장고는 껌 풍선(4-4 — 클립 없이 전투 대기 그대로, 풍선은 11단계 이펙트)
            if (Brain != null && Brain.State == EnemyBrain.S.Taunt && (Brain.Def == null || Brain.Def.Type != EnemyDef.Kind.Naengjanggo)) { st = "Taunt"; key = "taunt"; r = FitRate("Taunt", t.TauntTime); return; }
            bool loco = !InCombat || (Brain != null && (Brain.State == EnemyBrain.S.Approach || Brain.State == EnemyBrain.S.Flee) && Speed() > 1.7f);
            st = loco ? "Move" : "CombatMove";
            key = st;
            r = 1f;
        }

        /// 멈춘 자세(배율 0 대신 아주 작은 값 — fixedTimeOffset 이 '클립 초 ÷ 배율'로 해석되므로 0 이면 처음 위치를 줄 수 없음)
        const float Still = 0.001f;

        /// 멱살 잡고 있음: 손 뻗기(Grab)에서 바로 이어지면 그 자리에 멈추고, 무릎 뒤에 돌아오면 손이 닿은 프레임(Grab 타격 시각)에서 멈춤
        void Hold(out string st, out float r, out float offset, out object key)
        {
            st = "Grab"; r = Still; offset = 0f;
            if (cur == "Grab") { key = curKey; return; }
            key = "hold";
            if (Clips != null && Clips.TryGet("Grab", out var e)) offset = e.Hit;
        }

        void Attack(out string st, out float r, out float offset, out object key)
        {
            var run = Me.Run;
            st = null; r = 1f; key = run; offset = 0f;
            if (run == null) { st = "CombatMove"; key = st; return; }
            var m = run.Move;
            // 처음 본 순간 판정까지 남은 시간(예고·준비 포함, 이어진 기술은 링크에서 넘어온 시간만큼 이미 지남)
            if (run != seen) { seen = run; preAtStart = m.ActiveStart - run.T; }
            double su = m.ActiveStart;
            if (m.ChargeTime > 0f && run.T < 0)
            {
                // 달려들기: 예고(몸 낮춤 = 돌진 첫 자세 고정) → 돌진
                st = "Charge";
                key = run.T < -m.ChargeTime ? (object)("lead" + run.GetHashCode()) : "charge" + run.GetHashCode();
                r = run.T < -m.ChargeTime ? 0f : FitRate("Charge", m.ChargeTime);
                return;
            }
            st = ClipTable.StateFor(m);
            if (st == null) { st = "CombatMove"; key = st; return; }
            double toHit = m.ChargeTime > 0f ? su : preAtStart;
            ClipRate(st, toHit, out r, out offset);
            key = m.ChargeTime > 0f ? (object)("strike" + run.GetHashCode()) : run;
        }

        float Speed()
        {
            var b = Me.Body as FighterBody;
            return b != null ? new Vector3(b.WalkVelocity.x, 0f, b.WalkVelocity.z).magnitude : 0f;
        }

        void Update()
        {
            if (anim == null || anim.runtimeAnimatorController == null || Me == null) return;
            Decide(out string st, out float r, out float offset, out object key);
            if (st != null && (st != cur || !Equals(key, curKey)))
            {
                if (anim.HasState(0, Animator.StringToHash(st)))
                {
                    // fixedTimeOffset 는 '배율 적용된 상태 시간'(클립 초 ÷ 배율)으로 해석된다(측정: ×1.6 에서 0.33초를 주면 클립 0.53초부터) → 클립 초를 배율로 나눠 넘김
                    float off = r > 1e-4f ? offset / r : 0f;
                    anim.CrossFadeInFixedTime(st, st == "Lie" || st == "Kneel" || st == "StandUp" ? 0.15f : Fade, 0, off);
                    cur = st;
                    curKey = key;
                }
            }
            rate = r;
            anim.SetFloat(HRate, r);

            float dt = TimeFx.Dt;
            if (dt > 0f)
            {
                var p = Me.Position;
                var v = (p - lastPos) / dt;
                lastPos = p;
                var local = Quaternion.Inverse(Quaternion.Euler(0f, Me.Yaw, 0f)) * new Vector3(v.x, 0f, v.z);
                var own = Me.Body as FighterBody;
                if (own != null) local = Quaternion.Inverse(Quaternion.Euler(0f, Me.Yaw, 0f)) * own.WalkVelocity;
                var motor = Player != null ? Player.Motor : null;
                if (motor != null) { float k = motor.OwnSpeed / Mathf.Max(1e-3f, local.magnitude); if (local.magnitude > 1e-3f) local *= Mathf.Min(1f, k); }
                float sx = Mathf.Clamp(local.x / 1.3f, -1f, 1f), sy = Mathf.Clamp(local.z / (local.z >= 0f ? 1.6f : 1.3f), -1f, 1f);
                float a = 1f - Mathf.Exp(-dt / 0.08f);
                cx += (sx - cx) * a;
                cy += (sy - cy) * a;
                anim.SetFloat(HCX, cx);
                anim.SetFloat(HCY, cy);
                // 막기 위층: 진입·해제 6f
                float gw = Me.Guarding && Me.State == Fighter.Phase.Free ? 1f : 0f;
                guardW = Mathf.MoveTowards(guardW, gw, dt / (6f / MoveDef.Fps));
                if (anim.layerCount > GuardLayer) anim.SetLayerWeight(GuardLayer, guardW);
                // 적 걷기·달리기(Move): LocoAnim 과 같은 계산
                if (Player == null)
                {
                    float spd = own != null ? new Vector3(own.WalkVelocity.x, 0f, own.WalkVelocity.z).magnitude : v.magnitude;
                    loco = Mathf.SmoothDamp(loco, spd, ref locoVel, 0.05f, Mathf.Infinity, dt);
                    LocoAnim.Evaluate(loco, 0.5f, 1.4f, 4.5f, out float b, out float lr);
                    anim.SetFloat(HBlend, b);
                    anim.SetFloat(HLocoRate, lr);
                    anim.SetFloat(HSpeed, loco);
                }
            }
        }
    }
}
