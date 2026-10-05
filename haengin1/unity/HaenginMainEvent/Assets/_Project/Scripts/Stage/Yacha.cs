// 행인1의 메인이벤트 — 야차 1:1 (docs/08_M2_전투_설계.md 4-6·8-2, 11장 14단계)
// 와룡공원 성곽 아래 공터(지름 13m 흙바닥 원). 흐름:
//   심판 형(원 남쪽 가장자리, × / E) → 대화 「판돈 오만. …」 들어간다 / 다음에 → 입장 약 6초(조작 막힘): 구경꾼 12명이 3초에 원 둘레(6.8~7.6m)로 걸어와 섬,
//   시우 남쪽 (0, −3.5) · 스크럼 북쪽 (0, +3.5) 마주 봄 → 이름 카드 2.0초 → 심판 형 "시작!" → 조작
//   링: 반경 6.0m 부터 바깥으로 3 m/s 로 안쪽 밀어냄(가장 가까운 구경꾼이 떠밂), 6.8m 에 보이지 않는 원통 벽(PlayerOnly — 적도 막힘). 「구경꾼 되받기」 CrowdRing = 원 전체
//   승: 스크럼 HP 0 → 항복(한쪽 무릎 + "됐다, 됐어! 졌다!") → 심판 "끝! 그만!" → 결과 카드(시우 403 승리) — 슬로 없음
//   패: 시우 HP 0 → 다운 → 심판 스톱 / 시우 다운 3회 → "그만, 거기까지." / 일시정지 메뉴 '항복' → 결과 「야차 패 — 판돈 −50,000원」
//   끝: 구경꾼 흩어짐, 스크럼 "한 판 더?" → 탐색 복귀. 다시 하려면 심판 형에게 다시 말 건다.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Haengin
{
    [DefaultExecutionOrder(-40)]
    public sealed class Yacha : MonoBehaviour
    {
        public enum Phase { Idle, Ask, Entry, Fight, End }
        public enum Outcome { None, Win, LoseKO, LoseDowns, LoseSurrender }

        public YachaDef Def;
        public PlayerCombat Player;
        public CombatMode Mode;
        public CombatTuning Tuning;

        public Phase State { get; private set; } = Phase.Idle;
        public Outcome Result { get; private set; }
        public double PhaseT { get; private set; }
        public int Downs { get; private set; }
        public EnemyBrain Foe { get; private set; }
        public Interactable Referee { get; private set; }
        public readonly List<CrowdFigure> Crowd = new List<CrowdFigure>();
        public float MaxRingDist { get; private set; }
        public int Pushes { get; private set; }
        public event Action<Phase> Changed;

        AttackDirector director;
        CrowdRing ring;
        readonly List<Collider> walls = new List<Collider>();
        Transform refereeTr;
        double endAt;
        bool started, said;
        readonly Vector3[] crowdSpot = new Vector3[16];

        void Start()
        {
            if (Player == null) Player = FindAnyObjectByType<PlayerCombat>();
            if (Mode == null) Mode = FindAnyObjectByType<CombatMode>();
            if (Def == null || Player == null) { enabled = false; return; }
            var t = Tuning != null ? Tuning : Player.Tuning;
            var c = Ground(Def.Center);

            // 심판 형(원 남쪽 가장자리, 원 가운데를 봄) — 상호작용 「야차 — 들어가기」
            var rp = Ground(Def.Referee);
            GameObject rgo = Def.RefereeModel != null ? Instantiate(Def.RefereeModel) : GameObject.CreatePrimitive(PrimitiveType.Capsule);
            rgo.name = "심판 형";
            foreach (var col in rgo.GetComponentsInChildren<Collider>()) Destroy(col);
            rgo.transform.SetParent(transform, false);
            rgo.transform.SetPositionAndRotation(rp + (Def.RefereeModel == null ? Vector3.up * 0.9f : Vector3.zero), Quaternion.Euler(0f, HitResolver.Yaw(HitResolver.Flat(c - rp)), 0f));
            refereeTr = rgo.transform;
            Referee = rgo.AddComponent<Interactable>();
            Referee.Prompt = "야차 — 들어가기";
            Referee.Radius = 2.2f;
            Referee.Used += OnReferee;

            // 상대(스크럼) — 링 북쪽, 처음엔 숨김
            director = CombatFactory.Director(Player.Me, t, "AttackDirector_Yacha");
            director.transform.SetParent(transform, true);
            if (Def.Foe != null)
            {
                var model = Def.FoeModel != null ? Instantiate(Def.FoeModel) : null;
                var fp = Ground(c + new Vector3(0f, 0f, 3.5f));
                Foe = CombatFactory.Enemy(Def.Foe, fp, 180f, Player.Me, director, 0, t, model);
                Foe.transform.SetParent(transform, true);
                Foe.gameObject.SetActive(false);
            }

            // 구경꾼 12명(원 둘레 6.8~7.6m) + CrowdRing(원 전체)
            var rg = new GameObject("CrowdRing");
            rg.transform.SetParent(transform, false);
            rg.transform.position = c;
            ring = rg.AddComponent<CrowdRing>();
            ring.Radius = 6.6f;
            ring.enabled = false;
            for (int i = 0; i < Def.CrowdCount; i++)
            {
                var m = Def.CrowdModels.Length > 0 ? Def.CrowdModels[i % Def.CrowdModels.Length] : null;
                if (m == null) continue;
                float a = (i + 0.5f) / Def.CrowdCount * 360f;
                float r = 6.9f + (i % 3) * 0.3f;
                crowdSpot[i] = Ground(c + HitResolver.YawDir(a) * r);
                var go = Instantiate(m, rg.transform);
                go.transform.SetPositionAndRotation(crowdSpot[i], Quaternion.Euler(0f, a + 180f, 0f));
                var cf = go.GetComponent<CrowdFigure>() ?? go.AddComponent<CrowdFigure>();
                Crowd.Add(cf);
                go.SetActive(false);
            }

            // 6.8m 원통 벽(24조각, 높이 3m) — 야차 동안만
            for (int i = 0; i < 24; i++)
            {
                float a = i / 24f * 360f;
                var go = new GameObject("링 벽") { layer = Layers.PlayerOnly };
                go.transform.SetParent(transform, false);
                var p = c + HitResolver.YawDir(a) * (Def.RingWall + 0.25f);
                go.transform.SetPositionAndRotation(new Vector3(p.x, c.y + 1.5f, p.z), Quaternion.Euler(0f, a, 0f));
                var bc = go.AddComponent<BoxCollider>();
                bc.size = new Vector3(2f * Mathf.PI * (Def.RingWall + 0.25f) / 24f + 0.15f, 4f, 0.5f);
                bc.enabled = false;
                walls.Add(bc);
            }
        }

        static Vector3 Ground(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out var hit, 8f, 1 << Layers.Ground, QueryTriggerInteraction.Ignore)) p.y = hit.point.y;
            return p;
        }

        void Go(Phase p)
        {
            State = p;
            PhaseT = 0;
            said = false;
            Changed?.Invoke(p);
            Debug.Log($"[Yacha] {Def.Title}: {p}{(p == Phase.End ? " · " + Result : "")}");
        }

        void Say(Transform who, string line, float secs)
        {
            if (StageHud.Instance != null && who != null) StageHud.Instance.Subtitle(who, 2.3f, line, secs);
        }

        // ───────────────────────── 심판 형
        void OnReferee()
        {
            if (State != Phase.Idle || (Mode != null && Mode.Active)) return;
            Go(Phase.Ask);
            var hud = StageHud.Instance;
            if (hud != null) hud.Dialog("심판 형", Def.Ask, "들어간다", "다음에", yes => { if (yes) Enter(); else Go(Phase.Idle); });
            else Enter();
        }

        /// 입장(시험은 대화를 건너뛰고 바로 부를 수 있다)
        public void Enter()
        {
            if (State == Phase.Entry || State == Phase.Fight) return;
            Result = Outcome.None;
            Downs = 0;
            started = false;
            MaxRingDist = 0f;
            GameState.InputLocked = true;
            var c = Ground(Def.Center);
            var me = Player.Me;
            me.ResetFighter();
            (me.Body as PlayerBody)?.Place(Ground(c + new Vector3(0f, 0f, -3.5f)), 0f);
            if (Foe != null)
            {
                Foe.gameObject.SetActive(true);
                Foe.ResetBrain(Ground(c + new Vector3(0f, 0f, 3.5f)), 180f);
            }
            // 구경꾼: 원 바깥 4m 에서 3초에 걸어와 섬
            for (int i = 0; i < Crowd.Count; i++)
            {
                var cf = Crowd[i];
                var spot = crowdSpot[i];
                var outDir = HitResolver.Flat(spot - c).normalized;
                cf.gameObject.SetActive(true);
                cf.transform.position = Ground(spot + outDir * 4f);
                cf.WalkTo(spot, c, 3.0f);
            }
            ring.enabled = true;
            foreach (var w in walls) w.enabled = true;
            me.Downed -= OnSiwooDowned;
            me.Downed += OnSiwooDowned;
            if (Mode != null) Mode.Begin(99f); else Player.Begin(99f);       // 카메라는 전투로, 입력은 '시작!' 까지 막음
            Go(Phase.Entry);
        }

        void OnSiwooDowned(Fighter f) { if (State == Phase.Fight) { Downs++; StageHud.Instance?.YachaDowns(Downs); } }

        /// 일시정지 메뉴 '항복'(야차 중에만)
        public void Surrender()
        {
            if (State != Phase.Fight) return;
            End(Outcome.LoseSurrender);
        }

        void Update()
        {
            if (Def == null || Player == null) return;
            if (GameState.Paused) return;
            float rdt = TimeFx.RealDt > 0f ? TimeFx.RealDt : Time.unscaledDeltaTime;
            PhaseT += rdt;
            var me = Player.Me;
            var c = Def.Center;
            switch (State)
            {
                case Phase.Entry:
                    if (!said && PhaseT >= 3.0) { said = true; StageHud.Instance?.NameCard(Def.CardSmall, Def.CardBig, 2.0f); }
                    if (PhaseT >= 5.0 && !started) { started = true; Say(refereeTr, "심판 형: 시작!", 1.2f); }
                    if (PhaseT >= 6.0) StartFight();
                    break;
                case Phase.Fight:
                    Ring(TimeFx.Dt);
                    if (Foe != null && Foe.Me.State == Fighter.Phase.Out) End(Outcome.Win);
                    else if (me.State == Fighter.Phase.Out || me.KO) End(Outcome.LoseKO);
                    else if (Downs >= Def.DownLimit && me.State == Fighter.Phase.Lie) End(Outcome.LoseDowns);
                    break;
                case Phase.End:
                    if (PhaseT >= endAt) Finish();
                    break;
            }
        }

        void StartFight()
        {
            GameState.InputLocked = false;
            Player.Begin(0f, -1f);
            if (Foe != null) Foe.Activate();
            StageHud.Instance?.YachaBar(Foe != null ? Foe.Me : null, Def.CardBig);
            StageHud.Instance?.YachaDowns(0);
            GameUi.SurrenderHook = Surrender;
            Go(Phase.Fight);
        }

        /// 링: 6.0m 넘으면 3 m/s 로 안쪽(가장 가까운 구경꾼이 떠밂), 6.8m 원통 벽
        void Ring(float dt)
        {
            var c = Def.Center;
            foreach (var f in new[] { Player.Me, Foe != null ? Foe.Me : null })
            {
                if (f == null || f.Body == null) continue;
                var d = HitResolver.Flat(f.Position - c);
                float r = d.magnitude;
                if (f == Player.Me) MaxRingDist = Mathf.Max(MaxRingDist, r);
                if (r <= Def.RingPush || dt <= 0f) continue;
                var inward = -d / Mathf.Max(1e-4f, r);
                f.Body.Push(inward * Mathf.Min(Def.PushSpeed * dt, r - Def.RingPush + 0.01f));
                // 가장 가까운 구경꾼이 떠밂
                CrowdFigure best = null; float bd = 3f;
                foreach (var cf in Crowd) { if (cf == null || !cf.isActiveAndEnabled) continue; float dd = HitResolver.Flat(cf.transform.position - f.Position).magnitude; if (dd < bd) { bd = dd; best = cf; } }
                if (best != null) { int s0 = best.Shoves; best.Shove(f.Position); if (best.Shoves > s0) Pushes++; }
            }
        }

        void End(Outcome o)
        {
            if (State == Phase.End) return;
            Result = o;
            GameUi.SurrenderHook = null;
            var hud = StageHud.Instance;
            var me = Player.Me;
            if (Foe != null) Foe.Freeze(true);
            switch (o)
            {
                case Outcome.Win:
                    Say(Foe != null ? Foe.transform : null, "스크럼: 됐다, 됐어! 졌다!", 1.8f);
                    endAt = 4.0;
                    break;
                case Outcome.LoseKO:
                    Say(refereeTr, "심판 형: 그만! 끝!", 1.8f);
                    endAt = 3.5;
                    break;
                case Outcome.LoseDowns:
                    Say(refereeTr, "심판 형: 그만, 거기까지.", 1.8f);
                    endAt = 3.5;
                    break;
                case Outcome.LoseSurrender:
                    Say(refereeTr, "심판 형: 항복. 끝!", 1.8f);
                    if (Player.Me.Run != null) Player.Me.CancelAttack();
                    endAt = 3.0;
                    break;
            }
            Go(Phase.End);
            // 이기면 0.9초 뒤 심판 "끝! 그만!" + 결과, 시우 승리(403)
            if (o == Outcome.Win)
            {
                StartCoroutine(After(0.9f, () =>
                {
                    Say(refereeTr, "심판 형: 끝! 그만!", 1.6f);
                    hud?.Result(Def.WinText, 2.5f);
                    if (me.State == Fighter.Phase.Free) { me.ScriptAnim = "Victory"; me.ScriptRate = 1f; }
                }));
            }
            else hud?.Result(Def.LoseText, 2.5f);
        }

        System.Collections.IEnumerator After(float secs, Action a)
        {
            float t = 0f;
            while (t < secs) { t += TimeFx.RealDt > 0f ? TimeFx.RealDt : Time.unscaledDeltaTime; yield return null; }
            a();
        }

        void Finish()
        {
            var me = Player.Me;
            me.ScriptAnim = null;
            ring.enabled = false;
            foreach (var w in walls) w.enabled = false;
            foreach (var cf in Crowd) if (cf != null && cf.isActiveAndEnabled) cf.Disperse(Def.Center);
            if (Mode != null && Mode.Active) Mode.End(); else Player.End();
            if (me.State == Fighter.Phase.Out || me.Down) me.ResetFighter();
            if (Foe != null)
            {
                Foe.ResetBrain(Ground(Def.Center + new Vector3(0f, 0f, 3.5f)), 180f);
                Say(Foe.transform, "스크럼: 한 판 더?", 2.0f);
            }
            me.Downed -= OnSiwooDowned;
            StageHud.Instance?.YachaBar(null, "");
            Go(Phase.Idle);
        }

        void OnDisable() { if (GameUi.SurrenderHook == (Action)Surrender) GameUi.SurrenderHook = null; if (GameState.InputLocked && State == Phase.Entry) GameState.InputLocked = false; }
    }
}
