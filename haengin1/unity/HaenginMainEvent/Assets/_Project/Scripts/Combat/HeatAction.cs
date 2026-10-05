// 행인1의 메인이벤트 — 기세 액션 2종 (docs/08_M2_전투_설계.md 3-8, 11장 10단계)
// 조건(기세 100 · 대상이 앞 2.0m ±45° 에 서 있음 · 대상 등 뒤 1.4m 안 HeatSurface 벽 / 1.0m 안 CrowdRing) 이 맞을 때 △ →
//   ① 「벽 러시」 : 어깨로 밀어붙여 벽에 쿵(0.35) → 잽·크로스·훅·크로스·훅(약, 5·5·6·5·6) → 1.90 어퍼(기세 20, 슬로 0.75초 30%) → 벽에 기대 한쪽 무릎(다운)  = 47
//   ② 「구경꾼 되받기」: 앞차기(중 12)로 구경꾼 쪽에 밀어냄 → 0.45 구경꾼이 떠밂 → 0.60 비틀거리며 돌아옴 → 1.05 제자리 크로스 카운터(기세 33) → 크게 날아가 다운 = 45
// 코드 타임라인(게임 시간 — 히트스톱·슬로만큼 실제 시간은 늘어남). 시작하면 기세 100 → 0, 다른 적은 멈추고 무적(끝나고 0.5초 뒤 다시), 카메라 CM_Heat(6-4).
// 타격은 Fighter.StartAttack(기술, 고정 대상) 그대로라 히트스톱·흔들림·이펙트·효과음이 보통 타격과 같은 길로 나간다. 대상은 연출 동안 서 있고(KeepStanding) 막지 못한다.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Haengin
{
    public enum HeatKind { None, WallRush, CrowdReturn }

    [DefaultExecutionOrder(-44), DisallowMultipleComponent]
    public sealed class HeatAction : MonoBehaviour
    {
        public PlayerCombat Player;
        public HeatCam Cam;
        [Tooltip("조건: 대상까지(중심) m · 반각°")] public float Reach = 2.0f, HalfAngle = 45f;
        [Tooltip("조건: 대상 등 뒤 벽 · 구경꾼 m(대상 표면에서)")] public float WallBehind = 1.4f, CrowdBehind = 1.0f;
        [Tooltip("끝나고 다른 적이 다시 움직이기까지(실제 초)")] public float ResumeDelay = 0.5f;

        public static HeatAction Current { get; private set; }
        public static event Action<HeatAction> Started, Ended;
        /// 벽 러시의 벽 쿵(이펙트: 벽에 먹 균열)
        public static event Action<Vector3, Vector3> WallBang;
        /// 구경꾼이 떠밀 때(효과·소리 자리)
        public static event Action<Vector3> CrowdShove;

        public bool Playing { get; private set; }
        public HeatKind Kind { get; private set; }
        public Fighter Target { get; private set; }
        public double T { get; private set; }
        /// 이번 기세 액션에서 대상에게 들어간 피해 합(테스트)
        public int Dealt { get; private set; }
        public int Count { get; private set; }

        Fighter Me => Player != null ? Player.Me : null;
        CombatTuning Tu => Player != null ? Player.T : CombatTuning.Default;

        // 진행
        struct Step { public double At; public MoveDef Move; public bool Done; }
        readonly List<Step> steps = new List<Step>();
        struct Slide { public Fighter F; public Vector3 From, To; public double T0, T1; public bool Ease; }
        readonly List<Slide> slides = new List<Slide>();
        readonly List<EnemyBrain> frozen = new List<EnemyBrain>();
        readonly List<Fighter> shielded = new List<Fighter>();
        Vector3 dir, wallPoint, wallNormal;
        double endAt, resumeAt = -1;
        bool bangDone, shoveDone, stumbleDone, stumbleStop;
        float stumbleDist;
        CrowdRing ring;
        readonly List<(Transform t, Vector3 p, Quaternion r)> shovers = new List<(Transform, Vector3, Quaternion)>();
        double shoveT0 = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Current = null; Started = null; Ended = null; WallBang = null; CrowdShove = null; }

        void Awake() { if (Player == null) Player = GetComponent<PlayerCombat>(); }
        void OnEnable() { if (Me != null) Me.Landed += OnLanded; }
        void OnDisable() { if (Me != null) Me.Landed -= OnLanded; if (Current == this) Current = null; }
        void Start() { if (Me != null) { Me.Landed -= OnLanded; Me.Landed += OnLanded; } }

        // ───────────────────────── 조건
        /// 지금 △ 를 누르면 나갈 기세 액션(게이지와 무관 — HUD 판은 게이지도 본다)
        public HeatKind Check(out Fighter target)
        {
            target = null;
            var me = Me;
            if (Player == null || me == null || !Player.Active || Playing) return HeatKind.None;
            if (me.State != Fighter.Phase.Free && me.State != Fighter.Phase.Act) return HeatKind.None;
            target = Candidate();
            if (target == null) return HeatKind.None;
            var d = HitResolver.Flat(target.Position - me.Position);
            if (d.sqrMagnitude < 1e-6f) return HeatKind.None;
            d.Normalize();
            if (HeatSurface.Behind(target.Position + Vector3.up * 1.0f, d, WallBehind + target.Radius, out _)) return HeatKind.WallRush;
            foreach (var r in FindObjectsByType<CrowdRing>(FindObjectsSortMode.None))
                if (r.isActiveAndEnabled && r.Behind(target.Position, d, CrowdBehind + target.Radius)) return HeatKind.CrowdReturn;
            target = null;
            return HeatKind.None;
        }

        /// 게이지 100 + 조건(HUD 의 「△ 기세」 판)
        public bool Available(out HeatKind kind, out Fighter target)
        {
            kind = HeatKind.None;
            target = null;
            if (Player == null || !Player.Heat.Full) return false;
            kind = Check(out target);
            return kind != HeatKind.None;
        }

        /// 대상: 락온 대상, 아니면 앞 2.0m ±45° 안 가장 가까운 서 있는 적
        Fighter Candidate()
        {
            var me = Me;
            bool Ok(Fighter f) => f != null && f != me && f.Team != me.Team && f.Targetable && !f.Down && f.State != Fighter.Phase.Grabbed
                                  && HitResolver.Flat(f.Position - me.Position).magnitude <= Reach + 1e-3f
                                  && Vector3.Angle(me.Forward, HitResolver.Flat(f.Position - me.Position)) <= HalfAngle + 1e-3f;
            if (Player.Locked && Ok(Player.Lock.Target)) return Player.Lock.Target;
            Fighter best = null;
            float bd = float.MaxValue;
            foreach (var f in Fighter.All)
            {
                if (!Ok(f)) continue;
                float d = HitResolver.Flat(f.Position - me.Position).sqrMagnitude;
                if (d < bd) { bd = d; best = f; }
            }
            return best;
        }

        // ───────────────────────── 시작
        public bool TryStart()
        {
            if (Playing || Player == null || !Player.Heat.Full) return false;
            var kind = Check(out var target);
            if (kind == HeatKind.None) return false;
            Begin(kind, target);
            return true;
        }

        void Begin(HeatKind kind, Fighter target)
        {
            var me = Me;
            Playing = true;
            Current = this;
            Kind = kind;
            Target = target;
            T = 0;
            Dealt = 0;
            Count++;
            steps.Clear(); slides.Clear(); shovers.Clear();
            bangDone = shoveDone = stumbleDone = stumbleStop = false;
            shoveT0 = -1;
            resumeAt = -1;
            ring = null;
            Player.Heat.Set(0f);
            if (me.Run != null) me.CancelAttack();
            me.SetBusy(false);
            if (Player.Motor != null) { Player.Motor.SetMoveInput(Vector3.zero, 0f, false); Player.Motor.Halt(); }
            dir = HitResolver.Flat(target.Position - me.Position).normalized;
            me.Body.SetYaw(HitResolver.Yaw(dir));
            if (Player.Lock != null) Player.Lock.Set(target);

            // 대상: 하던 것 끊고 연출 동안 서 있게(막기·슈퍼아머 무시는 기술 쪽)
            if (target.Run != null) target.CancelAttack();
            target.Guarding = false;
            target.KeepStanding = true;
            target.ScriptAnim = null;
            var tb = target.GetComponent<EnemyBrain>();
            // 다른 적: 멈춤 + 무적, 공격권도 멈춤
            frozen.Clear(); shielded.Clear();
            foreach (var b in FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None))
            {
                b.Freeze(true);
                frozen.Add(b);
                if (b.Director != null) b.Director.Frozen = true;
            }
            foreach (var f in Fighter.All)
            {
                if (f == me || f == target || f.Team == me.Team) continue;
                f.Shielded = true;
                shielded.Add(f);
            }

            if (kind == HeatKind.WallRush) PlanWallRush(target);
            else PlanCrowd(target);

            if (Cam == null) Cam = FindAnyObjectByType<HeatCam>();
            if (Cam != null)
            {
                // 되받기: 대상 뒤 구경꾼도 화면에(떠미는 동작이 보이게)
                Vector3? also = null;
                if (kind == HeatKind.CrowdReturn) also = target.Position + HitResolver.Flat(target.Position - me.Position).normalized * (CrowdBehind + target.Radius);
                Cam.Begin(me, target, also);
            }
            Started?.Invoke(this);
            Debug.Log($"[HeatAction] {(kind == HeatKind.WallRush ? "벽 러시" : "구경꾼 되받기")} 시작 · 대상 {target.Label} · 멈춘 적 {frozen.Count - (tb != null ? 1 : 0)}");
        }

        // ───────────────────────── ① 벽 러시
        void PlanWallRush(Fighter tg)
        {
            var me = Me;
            var from = tg.Position + Vector3.up * 1.0f;
            float back = 0.6f;
            if (HeatSurface.Behind(from, dir, WallBehind + tg.Radius, out var hit))
            {
                back = Mathf.Max(0f, hit.distance + 0.15f - tg.Radius - 0.03f);
                wallPoint = hit.point; wallNormal = hit.normal;
            }
            else { wallPoint = tg.Position + dir * (tg.Radius + back); wallNormal = -dir; }
            var tEnd = tg.Position + dir * back;
            var mEnd = tEnd - dir * (tg.Radius + me.Radius + 0.12f);
            // 0.00 어깨로 밀어붙임(260 짧은 밀기, 0.35초에 쿵) — 대상은 0.10~0.35 에 벽까지, 시우는 따라붙음
            Add(0.0, HeatMoves.Shoulder);
            AddSlide(tg, tg.Position, tEnd, 0.10, 0.35, true);
            AddSlide(me, me.Position, mEnd, 0.0, 0.35, true);
            // 0.35~ 잽·크로스·훅·크로스·훅(약, 1.35배 리듬 0.25~0.35초) → 1.90 어퍼
            double[] at = { 0.40, 0.67, 0.97, 1.24, 1.54 };
            var ms = new[] { HeatMoves.Jab, HeatMoves.Cross, HeatMoves.Hook, HeatMoves.Cross2, HeatMoves.Hook2 };
            for (int i = 0; i < at.Length; i++) Add(at[i], ms[i]);
            Add(1.75, HeatMoves.Upper);
            endAt = 2.60;
        }

        // ───────────────────────── ② 구경꾼 되받기
        void PlanCrowd(Fighter tg)
        {
            var me = Me;
            float s = 1.0f;
            foreach (var r in FindObjectsByType<CrowdRing>(FindObjectsSortMode.None))
            {
                if (!r.isActiveAndEnabled) continue;
                for (float k = 0f; k <= CrowdBehind + tg.Radius + 3f; k += 0.05f)
                    if (r.Near(tg.Position + dir * k, 0.05f)) { if (ring == null || k < s) { s = k; ring = r; } break; }
            }
            var back = tg.Position + dir * Mathf.Max(0f, s - tg.Radius - 0.05f);
            // 0.00 앞차기(중 12) → 맞은 뒤 0.45 까지 구경꾼 쪽으로
            Add(0.0, HeatMoves.Kick);
            AddSlide(tg, tg.Position, back, HeatMoves.Kick.ActiveStart, 0.45, true);
            // 0.95 제자리 크로스 카운터(1.05 판정) — 비틀거리며 돌아오는 대상이 1.05 에 크로스가 닿는 자리에 오게(문서 1.6m 는 그 거리로 맞춤)
            Add(0.95, HeatMoves.Counter);
            endAt = 1.95;
        }

        void Add(double at, MoveDef m) => steps.Add(new Step { At = at, Move = m });
        void AddSlide(Fighter f, Vector3 from, Vector3 to, double t0, double t1, bool ease) =>
            slides.Add(new Slide { F = f, From = new Vector3(from.x, f.Position.y, from.z), To = new Vector3(to.x, f.Position.y, to.z), T0 = t0, T1 = t1, Ease = ease });

        // ───────────────────────── 한 프레임
        void Update()
        {
            if (!Playing)
            {
                if (resumeAt >= 0 && TimeFx.Real >= resumeAt) Resume();
                return;
            }
            float dt = TimeFx.Dt;
            if (dt <= 0f) return;
            var me = Me;
            var tg = Target;
            double t0 = T, t1 = T + dt;
            T = t1;

            // 기술 시작
            for (int i = 0; i < steps.Count; i++)
            {
                var s = steps[i];
                if (s.Done || s.At > t1) continue;
                s.Done = true;
                steps[i] = s;
                StartStep(s.Move);
            }

            // 미끄러짐(대상 밀려남 · 시우 따라붙음 · 되받기 비틀걸음)
            for (int i = slides.Count - 1; i >= 0; i--)
            {
                var sl = slides[i];
                if (sl.F == null || sl.F.Body == null) { slides.RemoveAt(i); continue; }
                if (t1 < sl.T0) continue;
                float u = (float)Math.Min(1.0, (t1 - sl.T0) / Math.Max(1e-4, sl.T1 - sl.T0));
                if (sl.Ease) u = u * u * (3f - 2f * u);
                var want = Vector3.Lerp(sl.From, sl.To, u);
                var d = HitResolver.Flat(want - sl.F.Position);
                if (d.sqrMagnitude > 1e-8f) sl.F.Body.Push(d);
                if (t1 >= sl.T1) slides.RemoveAt(i);
            }

            if (Kind == HeatKind.CrowdReturn) TickCrowd(t0, t1);
            TickShovers();

            // 서로 보게
            if (tg != null && me.State != Fighter.Phase.Act)
            {
                var to = HitResolver.Flat(tg.Position - me.Position);
                if (to.sqrMagnitude > 1e-4f) me.Body.SetYaw(Mathf.MoveTowardsAngle(me.Yaw, HitResolver.Yaw(to), 720f * dt));
            }
            if (tg != null && !tg.Down && tg.State != Fighter.Phase.Out && string.IsNullOrEmpty(tg.ScriptAnim))
            {
                var to = HitResolver.Flat(me.Position - tg.Position);
                if (to.sqrMagnitude > 1e-4f) tg.Body?.SetYaw(Mathf.MoveTowardsAngle(tg.Yaw, HitResolver.Yaw(to), 720f * dt));
            }

            if (T >= endAt && me.Run == null) Finish();
        }

        void StartStep(MoveDef m)
        {
            var me = Me;
            var tg = Target;
            if (tg == null) return;
            // 주먹이 닿는 자리로(클립 뻗음 — 없으면 0.75m) 발생 동안 붙는다
            if (m != HeatMoves.Shoulder && m != HeatMoves.Kick && m != HeatMoves.Counter)
            {
                float reach = me.ReachOf != null ? me.ReachOf(m) : -1f;
                float d = (reach > 0f ? reach - Tu.ContactSink : 0.72f) + tg.Radius;
                var goal = tg.Position - dir * d;
                AddSlide(me, me.Position, goal, T, T + Math.Max(0.03, m.ActiveStart * 0.8), true);
            }
            if (m == HeatMoves.Upper) tg.DownPose = Fighter.DownLook.Kneel;
            if (m == HeatMoves.Counter) tg.DownPose = Fighter.DownLook.Big;
            me.StartAttack(m, null, 0, tg, 0);
        }

        void OnLanded(HitEvent e)
        {
            if (!Playing || e.Victim != Target || e.Move == null || !HeatMoves.Is(e.Move)) return;
            Dealt += e.Damage;
            if (e.Move == HeatMoves.Shoulder && !bangDone)
            {
                bangDone = true;
                WallBang?.Invoke(wallPoint, wallNormal);
            }
        }

        void TickCrowd(double t0, double t1)
        {
            var me = Me;
            var tg = Target;
            // 0.45 구경꾼이 두 손으로 떠밂(몸 20° 숙이며 0.2m 안으로)
            if (!shoveDone && t1 >= 0.45)
            {
                shoveDone = true;
                shoveT0 = t1;
                if (ring != null)
                {
                    var c = ring.transform.position;
                    var near = new List<(Transform, float)>();
                    foreach (Transform ch in ring.transform)
                    {
                        if (ch.GetComponentInChildren<Renderer>() == null) continue;
                        float d = HitResolver.Flat(ch.position - tg.Position).magnitude;
                        if (d < 2.0f) near.Add((ch, d));
                    }
                    near.Sort((a, b) => a.Item2.CompareTo(b.Item2));
                    for (int i = 0; i < near.Count && i < 3; i++)
                    {
                        var t = near[i].Item1;
                        var cf = t.GetComponent<CrowdFigure>();
                        if (cf != null) { cf.Shove(tg.Position); continue; }     // 사람 모델: 떠미는 동작(260) + 숙이며 0.2m
                        shovers.Add((t, t.position, t.rotation));
                    }
                }
                CrowdShove?.Invoke(tg.Position);
            }
            // 0.60 비틀거리며 돌아옴 → 1.05 에 크로스가 닿는 자리
            if (!stumbleDone && t1 >= 0.60)
            {
                stumbleDone = true;
                tg.ScriptAnim = "Stumble";
                tg.ScriptRate = 1.6f;
                float reach = me.ReachOf != null ? me.ReachOf(HeatMoves.Counter) : -1f;
                float d = (reach > 0f ? reach - Tu.ContactSink : 0.72f) + tg.Radius;
                var goal = me.Position + dir * d;
                stumbleDist = HitResolver.Flat(goal - tg.Position).magnitude;
                AddSlide(tg, tg.Position, goal, 0.60, 1.05, true);
            }
            if (!stumbleStop && t1 >= 1.05 - 1e-4) { stumbleStop = true; tg.ScriptAnim = null; }
        }

        void TickShovers()
        {
            if (shoveT0 < 0 || shovers.Count == 0) return;
            float u = (float)((T - shoveT0) / 0.45);
            // 0 → 0.15 숙이며 들어옴 → 0.3 유지 → 0.45 돌아감
            float w = u < 0.33f ? u / 0.33f : u < 0.66f ? 1f : Mathf.Max(0f, 1f - (u - 0.66f) / 0.34f);
            var c = ring != null ? ring.transform.position : Vector3.zero;
            foreach (var (t, p, r) in shovers)
            {
                if (t == null) continue;
                var inward = HitResolver.Flat(c - p).normalized;
                t.position = p + inward * (0.2f * w);
                var axis = Vector3.Cross(Vector3.up, inward);
                t.rotation = Quaternion.AngleAxis(20f * w, axis) * r;
            }
            if (u >= 1f)
            {
                foreach (var (t, p, r) in shovers) if (t != null) { t.position = p; t.rotation = r; }
                shovers.Clear();
            }
        }

        // ───────────────────────── 끝
        void Finish()
        {
            var tg = Target;
            Playing = false;
            if (tg != null)
            {
                tg.KeepStanding = false;
                tg.ScriptAnim = null;
                // 연출 중 HP 0 이 됐으면 지금 다운(이미 다운이면 그대로 — 일어나지 않고 탈락)
                if (tg.Hp <= 0 && !tg.Down && tg.State != Fighter.Phase.Out) tg.StartDown(dir, 0.3f);
            }
            foreach (var (t, p, r) in shovers) if (t != null) { t.position = p; t.rotation = r; }
            shovers.Clear();
            if (Cam != null) Cam.End();
            resumeAt = TimeFx.Real + ResumeDelay;
            if (Current == this) Current = null;
            Debug.Log($"[HeatAction] {(Kind == HeatKind.WallRush ? "벽 러시" : "구경꾼 되받기")} 끝 · 피해 {Dealt} · 게임 시간 {T:F2}초{(Kind == HeatKind.CrowdReturn ? $" · 비틀걸음 {stumbleDist:F2}m" : "")}");
            Ended?.Invoke(this);
        }

        void Resume()
        {
            resumeAt = -1;
            foreach (var b in frozen) if (b != null) { b.Freeze(false); if (b.Director != null) b.Director.Frozen = false; }
            foreach (var f in shielded) if (f != null) f.Shielded = false;
            frozen.Clear();
            shielded.Clear();
        }

        /// 즉시 끝내기(전투 끝·장면 바뀜)
        public void Abort()
        {
            if (Playing) Finish();
            Resume();
        }
    }

    /// 기세 액션 기술(08 3-8 표 숫자). 고정 대상(판정 없이 그 대상만), 막기·슈퍼아머 무시, 기세 0
    public static class HeatMoves
    {
        static MoveDef M(string id, string label, int clip, int s, int a, int r, int dmg, Power p, string word, FlinchKind fk = FlinchKind.Head)
        {
            var m = ScriptableObject.CreateInstance<MoveDef>();
            m.name = id; m.Label = label; m.State = id; m.ClipId = clip;
            m.Startup = s; m.Active = a; m.Recovery = r; m.LinkAfter = 0;
            m.Range = 1.6f; m.HalfAngle = 180f; m.MaxTargets = 1;
            m.Damage = dmg; m.Stagger = 150; m.Knock = 0f; m.Power = p; m.Heat = 0; m.Word = word; m.Flinch = fk;
            m.Unblockable = true; m.IgnoreArmor = true; m.Magnet = 0f;
            m.hideFlags = HideFlags.DontSave;
            return m;
        }

        public static readonly MoveDef Shoulder = Make(() => { var m = M("HeatShoulder", "벽 러시: 밀어붙임", 260, 21, 2, 6, 0, Power.Mid, "쿵!", FlinchKind.Body); m.State = "Push"; return m; });
        public static readonly MoveDef Jab = M("HeatJab", "벽 러시: 잽", 191, 3, 2, 12, 5, Power.Light, "퍽!");
        public static readonly MoveDef Cross = M("HeatCross", "벽 러시: 크로스", 192, 3, 2, 12, 5, Power.Light, "퍽!");
        public static readonly MoveDef Hook = Make(() => { var m = M("HeatHook", "벽 러시: 훅", 193, 3, 2, 12, 6, Power.Light, "퍽!", FlinchKind.Hook); return m; });
        public static readonly MoveDef Cross2 = M("HeatCross2", "벽 러시: 크로스", 192, 3, 2, 12, 5, Power.Light, "퍽!");
        public static readonly MoveDef Hook2 = Make(() => { var m = M("HeatHook2", "벽 러시: 훅", 193, 3, 2, 12, 6, Power.Light, "퍽!", FlinchKind.Hook); return m; });
        public static readonly MoveDef Upper = Make(() =>
        {
            var m = M("HeatUpper", "벽 러시: 어퍼", 194, 9, 4, 30, 20, Power.Heat, "콰직!", FlinchKind.Upper);
            m.Down = true; m.DownKnock = 0f; m.Slow = 0.75f; m.SlowScale = 0.3f;
            return m;
        });
        public static readonly MoveDef Kick = Make(() => { var m = M("HeatKick", "되받기: 앞차기", 209, 8, 3, 14, 12, Power.Mid, "빡!", FlinchKind.Body); return m; });
        public static readonly MoveDef Counter = Make(() =>
        {
            var m = M("HeatCounter", "되받기: 크로스 카운터", 192, 6, 3, 30, 33, Power.Heat, "콰직!");
            m.Down = true; m.DownKnock = 1.2f; m.Slow = 0.75f; m.SlowScale = 0.3f;
            return m;
        });

        static MoveDef Make(Func<MoveDef> f) => f();

        static readonly HashSet<MoveDef> all = new HashSet<MoveDef> { Shoulder, Jab, Cross, Hook, Cross2, Hook2, Upper, Kick, Counter };
        public static bool Is(MoveDef m) => m != null && all.Contains(m);
    }
}
