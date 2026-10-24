// 행인1의 메인이벤트 — D11 기술 스파링(라이트) (docs/12_체육관_설계.md 4-2 D11 · G09)
// 케이지 안, M2 전투 그대로(CombatMode·PlayerCombat·EnemyBrain) + 라이트 규칙: 헤드기어(머리 뼈에 캡) · 피해 ×0.25(HP ×4 로 같음) · 다운 없음(KeepStanding) ·
// 시우가 머리 강타(위력 중 이상·△ 마무리)를 넣으면 이윤준 "스톱." → 위반 +1 · 그 라운드 즉시 종료 / 시우 머리 피격 중 이상 → "스톱. 앉아." → 뇌진탕 의심(드릴 종료, 피로 +20, D09·D11 잠김)
// 새로 쓰는 것은 관찰(DrillObserver 역할 = 이 클래스)뿐: 명중·회피·읽었다·위반·포지션 집계
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Haengin
{
    public sealed class SparStation : DrillStation
    {
        public EnemyBrain Foe { get; private set; }
        AttackDirector director;
        GameObject headgearMe, headgearFoe;
        int playerHpBefore, playerMaxBefore;
        bool observeOnly;
        public bool ObserveOnly => observeOnly;
        readonly HashSet<AttackRun> incoming = new HashSet<AttackRun>();
        readonly HashSet<AttackRun> landed = new HashSet<AttackRun>();
        AttackRun myRun; bool myRunHit;
        public int Violations { get; private set; }
        public override string Hint => "라이트 — 머리는 터치(□만). △ 머리 강타는 스톱";

        public override void Begin(DrillRunner r)
        {
            base.Begin(r);
            var me = Actors.Me;
            if (me == null) return;
            Fighter.AnyHit += OnHit;
            Fighter.AttackStarted += OnAttackStarted;
            me.MoveEnded += OnMyMoveEnded;
            if (Actors.Mode == null || Actors.GojunFighterPrefab == null || Actors.SparDef == null || Actors.Station == null) { observeOnly = true; return; }   // 시험: 관찰만
            // 피해 25% = 체력 4배(둘 다), 다운 없음
            playerHpBefore = me.Hp; playerMaxBefore = me.MaxHp;
            me.MaxHp = me.Hp = Mathf.Max(1, me.MaxHp) * 4;
            me.KeepStanding = true;
            headgearMe = Headgear(Actors.Visual != null ? Actors.Visual.GetComponentInChildren<Animator>() : null, "헤드기어(시우)");
            // 상대: 고준(석 달 틀 + 스파링 행동 — 약한 기술만)
            var feet = Actors.Station.position + Actors.Station.forward * 1.6f;
            var model = Actors.GojunFighterPrefab != null ? UnityEngine.Object.Instantiate(Actors.GojunFighterPrefab) : null;
            director = CombatFactory.Director(me, Actors.Tuning, "AttackDirector_스파링");
            var yaw = HitResolver.Yaw(Actors.Station.position - feet);
            Foe = CombatFactory.Enemy(Actors.SparDef, feet, yaw, me, director, 0, Actors.Tuning, model);
            Foe.Me.KeepStanding = true;
            Foe.Me.MaxHp = Foe.Me.Hp = Actors.SparDef.Hp * 4;
            Foe.transform.SetParent(R.transform, true);
            headgearFoe = Headgear(Foe.GetComponentInChildren<Animator>(), "헤드기어(고준)");
            if (Actors.Gojun != null) Actors.Gojun.gameObject.SetActive(false);        // 자리 NPC 는 숨김(전투 몸이 대신)
            Foe.Me.MoveEnded += OnFoeMoveEnded;
            if (Actors.Mode != null) Actors.Mode.Begin(0.4f);
            if (Actors.Player != null) Actors.Player.Me.DebugInvuln = false;
            Foe.Activate();
            Foe.Freeze(true);
        }

        static GameObject Headgear(Animator anim, string name)
        {
            if (anim == null || !anim.isHuman) return null;
            var head = anim.GetBoneTransform(HumanBodyBones.Head);
            if (head == null) return null;
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            UnityEngine.Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(head, false);
            go.transform.localPosition = new Vector3(0f, 0.07f, -0.01f);
            go.transform.localScale = new Vector3(0.21f, 0.20f, 0.23f);
            var r = go.GetComponent<Renderer>();
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh != null) { var m = new Material(sh); m.SetColor("_BaseColor", new Color(0.12f, 0.10f, 0.11f)); m.SetFloat("_Smoothness", 0.35f); r.sharedMaterial = m; }
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return go;
        }

        public override void RoundStart(int round)
        {
            Foe?.Freeze(false);
            GameState.InputLocked = false;
            Actors.Say?.Invoke("이윤준", round == 1 ? Def.CueStart : Def.CueRound);
        }

        public override void RoundEnd(int round)
        {
            Foe?.Freeze(true);
            Actors.Me?.CancelAttack();
        }

        public override void Tick(float dt, double t, in DrillInput inp) { }

        void OnAttackStarted(Fighter f, AttackRun run)
        {
            if (!R.Running || R.State != DrillRunner.Phase.Round) return;
            if (Foe != null && f == Foe.Me) incoming.Add(run);
            else if (f == Actors.Me) { myRun = run; myRunHit = false; }
        }

        void OnMyMoveEnded(AttackRun run)
        {
            if (!R.Running || R.State != DrillRunner.Phase.Round) return;
            if (run == myRun && !myRunHit) Log.Add("swing", R.T, 0, R.Round);
            myRun = null;
        }

        void OnFoeMoveEnded(AttackRun run)
        {
            if (!R.Running || R.State != DrillRunner.Phase.Round) return;
            if (incoming.Remove(run) && !landed.Contains(run)) Log.Add("incoming", R.T, 1, R.Round);   // 판정이 지나갔는데 안 맞음 = 피함
            landed.Remove(run);
        }

        void OnHit(HitEvent ev) => Observe(ev);

        /// 타격 하나 관찰(시험에서 직접 넣을 수 있게 공개)
        public void Observe(HitEvent ev)
        {
            if (!R.Running || R.State != DrillRunner.Phase.Round) return;
            var me = Actors.Me;
            bool head = ev.Move != null && ev.Move.Flinch != FlinchKind.Body;
            if (ev.Attacker == me && (Foe == null || ev.Victim == Foe.Me))
            {
                if (ev.Move != null && ev.Move == Actors.Moves?.Grab) { Log.Add("position", R.T, 1, R.Round); return; }
                Log.Add("swing", R.T, ev.Landed ? 1 : 0, R.Round); myRunHit = true;
                // 머리 강타(4-2 D11): 위력 강 이상, 또는 △ 마무리(앞차기·어퍼·스텝 무릎·큰 훅)의 중 이상. □ 연타의 크로스·훅(중)은 '터치'로 봐 준다
                var mv = Actors.Moves;
                bool finisher = mv != null && (ev.Move == mv.FrontKick || ev.Move == mv.Upper || ev.Move == mv.StepKnee || ev.Move == mv.BigHook);
                if (ev.Landed && head && (ev.Power >= Power.Heavy || (ev.Power >= Power.Mid && (finisher || mv == null))))
                {
                    Violations++;
                    Log.Add("violation", R.T, 1, R.Round);
                    Actors.Say?.Invoke("이윤준", Def.CueStop);
                    R.Toast("스톱.");
                    EndRoundNow = true;
                }
            }
            else if (ev.Victim == me)
            {
                if (Foe != null && ev.Attacker == Foe.Me && Foe.Me.Run != null) landed.Add(Foe.Me.Run);
                bool hit = ev.Outcome == HitOutcome.Hit || ev.Outcome == HitOutcome.Crushed || ev.Outcome == HitOutcome.GuardBroken;
                if (ev.Outcome == HitOutcome.Blocked || ev.Outcome == HitOutcome.Read) Log.Add("read", R.T, 1, R.Round);
                Log.Add("incoming", R.T, hit ? 0 : 1, R.Round);
                if (hit && head && ev.Power >= Power.Mid)
                {
                    Log.Concussion = true;
                    Actors.Say?.Invoke("이윤준", Def.CueHurt);
                    R.Toast("스톱. 앉아.");
                    EndDrillNow = true;
                }
            }
        }

        public override void End(bool aborted)
        {
            Fighter.AnyHit -= OnHit;
            Fighter.AttackStarted -= OnAttackStarted;
            var me = Actors.Me;
            if (me != null)
            {
                me.MoveEnded -= OnMyMoveEnded;
                if (!observeOnly) { me.KeepStanding = false; me.MaxHp = playerMaxBefore; me.Hp = Mathf.Min(playerHpBefore, playerMaxBefore); }
                me.CancelAttack();
            }
            if (Foe != null) { Foe.Me.MoveEnded -= OnFoeMoveEnded; UnityEngine.Object.Destroy(Foe.gameObject); Foe = null; }
            if (director != null) UnityEngine.Object.Destroy(director.gameObject);
            if (headgearMe != null) UnityEngine.Object.Destroy(headgearMe);
            if (headgearFoe != null) UnityEngine.Object.Destroy(headgearFoe);
            if (Actors.Mode != null && Actors.Mode.Active) Actors.Mode.End();
            if (Actors.Gojun != null) Actors.Gojun.gameObject.SetActive(true);
            GameState.InputLocked = false;
        }

        FightBot bot;
        public override void Bot(double t, float skill)
        {
            if (bot == null && Actors.Player != null) { bot = FightBot.On(Actors.Player, true); }     // 라이트: □ 연타만(△ 머리 강타 없음)
            if (bot != null && (R.State != DrillRunner.Phase.Round)) bot.enabled = false;
            else if (bot != null) bot.enabled = true;
        }
    }
}
