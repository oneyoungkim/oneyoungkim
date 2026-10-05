// 행인1의 메인이벤트 — 인카운터 1:3 (docs/08_M2_전투_설계.md 2-5·8-1, 11장 13단계)
// 무대 = 주차장 로컬 좌표(x = 동쪽 길 방향, z = 북쪽 길 쪽, 원점 = 주차장 가운데). 흐름:
//   대기(적 셋 자판기 앞) → 시작 트리거(주차장 안쪽 1m + 적과 9m 안) → 시비 자막 2줄 0.8초 → 전투(CM_Combat 0.6초, 배너 「시비 붙음!」, 경계 벽 켬, 공격권)
//   → 셋 다 탈락(도주 포함) → 마무리(슬로 0.75초 30% + 「정리.」 2초) → +1초 결과 카드 2.5초(탈락한 적은 일어나 달아남, 구경꾼 흩어짐, 시우 숨 고르기 31)
//   → +2.5초 탐색 복귀. 이긴 뒤엔 일시정지 메뉴 '전투 다시'(근처에서)로만 다시.
//   패배(시우 HP 0) → 패배 화면 「…일어나.」 다시 / 그만 → 적·구경꾼·시우를 처음 자리로(시우는 북쪽 진입로 바깥 인도), HP·기세 처음으로.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Haengin
{
    [DefaultExecutionOrder(-40)]
    public sealed class Encounter : MonoBehaviour
    {
        public enum Phase { Armed, Taunt, Fight, Finish, Result, Cleared, Lost }

        public EncounterDef Def;
        public PlayerCombat Player;
        public CombatMode Mode;
        public CombatTuning Tuning;
        /// 시험(M1 경로 걷기 등): 시작 트리거를 끈다
        public static bool Suppress;

        public Phase State { get; private set; } = Phase.Armed;
        public double PhaseT { get; private set; }
        public int Starts, Wins, Losses, Retries;
        public AttackDirector Director { get; private set; }
        public readonly List<EnemyBrain> Brains = new List<EnemyBrain>();
        public readonly List<CrowdFigure> Crowd = new List<CrowdFigure>();
        public event Action<Phase> Changed;
        public int Eliminated { get { int n = 0; foreach (var b in Brains) if (b != null && b.Eliminated) n++; return n; } }

        readonly List<Collider> walls = new List<Collider>();
        CrowdRing ring;
        bool rearmOnLeave, lineDone;
        double lostAt = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Suppress = false; }

        void Start()
        {
            if (Player == null) Player = FindAnyObjectByType<PlayerCombat>();
            if (Mode == null) Mode = FindAnyObjectByType<CombatMode>();
            if (Def == null || Player == null) { enabled = false; return; }
            var t = Tuning != null ? Tuning : Player.Tuning;
            Director = CombatFactory.Director(Player.Me, t, "AttackDirector_" + name);
            Director.transform.SetParent(transform, true);
            for (int i = 0; i < Def.Foes.Length; i++)
            {
                var f = Def.Foes[i];
                if (f.Def == null) continue;
                var model = f.Model != null ? Instantiate(f.Model) : null;
                var feet = Ground(Def.ToWorld(f.Local));
                var b = CombatFactory.Enemy(f.Def, feet, FaceCenterYaw(feet), Player.Me, Director, i, t, model);
                b.transform.SetParent(transform, true);
                Brains.Add(b);
            }
            // 구경꾼(진입로·북쪽 인도) — 사람 모델 회색, CrowdRing 직선 구간의 자식(되받기 떠밂)
            var rg = new GameObject("CrowdRing");
            rg.transform.SetParent(transform, false);
            ring = rg.AddComponent<CrowdRing>();
            ring.IsLine = true;
            ring.LineA = Def.ToWorld(Def.CrowdLineA); ring.LineB = Def.ToWorld(Def.CrowdLineB);
            ring.transform.position = (ring.LineA + ring.LineB) * 0.5f;
            for (int i = 0; i < Def.Crowd.Length; i++)
            {
                var m = Def.CrowdModels.Length > 0 ? Def.CrowdModels[i % Def.CrowdModels.Length] : null;
                if (m == null) continue;
                var go = Instantiate(m, ring.transform);
                var p = Ground(Def.ToWorld(Def.Crowd[i]));
                go.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, FaceCenterYaw(p), 0f));
                var cf = go.GetComponent<CrowdFigure>() ?? go.AddComponent<CrowdFigure>();
                cf.SetHome();
                Crowd.Add(cf);
            }
            ring.enabled = false;      // 전투 동안만 「구경꾼 되받기」 구간
            // 경계 벽(전투 동안만)
            foreach (var w in Def.Walls)
            {
                var go = new GameObject("경계 벽") { layer = Layers.PlayerOnly };
                go.transform.SetParent(transform, false);
                var c = Def.ToWorld(w.Local);
                go.transform.SetPositionAndRotation(Ground(c) + Vector3.up * 1.5f, Quaternion.Euler(0f, Def.Yaw + w.LocalYaw, 0f));
                var bc = go.AddComponent<BoxCollider>();
                bc.size = new Vector3(w.Size.x, 3f, w.Size.y);
                bc.enabled = false;
                walls.Add(bc);
            }
        }

        float FaceCenterYaw(Vector3 p) => HitResolver.Yaw(HitResolver.Flat(Def.Center - p));

        static Vector3 Ground(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out var hit, 8f, 1 << Layers.Ground, QueryTriggerInteraction.Ignore)) p.y = hit.point.y;
            return p;
        }

        void Go(Phase p)
        {
            State = p;
            PhaseT = 0;
            Changed?.Invoke(p);
            Debug.Log($"[Encounter] {Def.Title}: {p}");
        }

        /// 시우가 시작 트리거 안인가(주차장 안쪽 1m)
        public bool Inside(Vector3 w)
        {
            var l = Def.ToLocal(w);
            return Mathf.Abs(l.x) <= Def.Size.x * 0.5f - Def.TriggerInset && Mathf.Abs(l.y) <= Def.Size.y * 0.5f - Def.TriggerInset;
        }

        void Update()
        {
            if (Def == null || Player == null || Player.Me == null) return;
            float rdt = TimeFx.RealDt > 0f ? TimeFx.RealDt : Time.unscaledDeltaTime;
            if (GameState.Paused) return;
            PhaseT += rdt;
            var me = Player.Me;
            var hud = StageHud.Instance;
            switch (State)
            {
                case Phase.Armed:
                    if (rearmOnLeave) { if (HitResolver.Flat(me.Position - Def.Center).magnitude > 15f) rearmOnLeave = false; break; }
                    if (Suppress || GameState.InputLocked || (Mode != null && Mode.Active)) break;
                    if (Inside(me.Position) && NearestFoe(me.Position) <= Def.TriggerEnemyDist) Trigger();
                    break;
                case Phase.Taunt:
                    // 0 ~ 0.8초: 탐색 조작 그대로, 시비 자막 두 줄
                    if (!lineDone && PhaseT >= 0.35) { lineDone = true; Say(Def.Line2Who, Def.Line2, 1.4f); }
                    if (PhaseT >= 0.8) BeginFight();
                    break;
                case Phase.Fight:
                    if (me.State == Fighter.Phase.Out || me.KO) { if (lostAt < 0) lostAt = PhaseT; if (PhaseT - lostAt >= 1.2) Lose(); }
                    else if (Eliminated >= Brains.Count && Brains.Count > 0) Finish();
                    break;
                case Phase.Finish:
                    if (PhaseT >= 1.0) ShowResult();
                    break;
                case Phase.Result:
                    if (PhaseT >= 2.5) EndToExplore();
                    break;
                case Phase.Cleared:
                    // 이긴 뒤 근처(20m)에서만 일시정지 메뉴 '전투 다시'
                    bool near = HitResolver.Flat(me.Position - Def.Center).magnitude < 20f && !(Mode != null && Mode.Active);
                    if (near && GameUi.RetryHook == null) GameUi.RetryHook = RetryFromMenu;
                    else if (!near && GameUi.RetryHook == (Action)RetryFromMenu) GameUi.RetryHook = null;
                    break;
            }
        }

        float NearestFoe(Vector3 p)
        {
            float best = float.MaxValue;
            foreach (var b in Brains) if (b != null && b.isActiveAndEnabled && !b.Eliminated) best = Mathf.Min(best, HitResolver.Flat(b.Me.Position - p).magnitude);
            return best;
        }

        void Say(string who, string line, float secs)
        {
            var hud = StageHud.Instance;
            Transform anchor = null;
            foreach (var b in Brains) if (b != null && b.Def != null && b.Def.Label == who) anchor = b.transform;
            if (hud != null && anchor != null) hud.Subtitle(anchor, 2.25f, $"{who}: {line}", secs);
        }

        public void Trigger()
        {
            if (State != Phase.Armed) return;
            Starts++;
            lineDone = false;
            lostAt = -1;
            Go(Phase.Taunt);
            Say(Def.Line1Who, Def.Line1, 1.6f);
        }

        void BeginFight()
        {
            foreach (var w in walls) w.enabled = true;
            ring.enabled = true;
            if (Mode != null) Mode.Begin(0.4f);       // 0.8 → 1.2초부터 공격 입력(2-5)
            else Player.Begin(0.4f);
            foreach (var b in Brains) if (b != null) b.Activate();
            StageHud.Instance?.Banner("시비 붙음!", 1.2f);
            CombatFx.Instance?.EdgeFlash();
            Go(Phase.Fight);
        }

        void Finish()
        {
            Wins++;
            if (!(Player.T.ReducedNow)) TimeFx.Slow(0.75f, 0.3f);
            StageHud.Instance?.BigWord("정리.", 2.0f);
            Go(Phase.Finish);
        }

        void ShowResult()
        {
            StageHud.Instance?.Result(Def.ResultText, 2.5f);
            if (Player.Me.State == Fighter.Phase.Free) { Player.Me.ScriptAnim = "Breath"; Player.Me.ScriptRate = 1f; }
            foreach (var b in Brains) if (b != null && b.isActiveAndEnabled) b.Leave();
            foreach (var c in Crowd) if (c != null && c.isActiveAndEnabled) c.Disperse(Def.Center);
            Go(Phase.Result);
        }

        void EndToExplore()
        {
            Player.Me.ScriptAnim = null;
            foreach (var w in walls) w.enabled = false;
            ring.enabled = false;
            if (Mode != null) Mode.End(); else Player.End();
            Go(Phase.Cleared);
        }

        void Lose()
        {
            Losses++;
            Go(Phase.Lost);
            var hud = StageHud.Instance;
            if (hud != null) hud.Defeat(() => ResetAll(true), () => ResetAll(false));
            else ResetAll(true);
        }

        /// 처음 자리로: 적·구경꾼·시우(북쪽 진입로 바깥 인도), HP·기세 처음. retry = 바로 다시 걸어 들어오면 시작 / 그만 = 15m 멀어졌다 와야 다시
        public void ResetAll(bool retry)
        {
            Retries++;
            foreach (var w in walls) w.enabled = false;
            ring.enabled = false;
            if (Mode != null && Mode.Active) Mode.End(); else if (Player.Active) Player.End();
            TimeFx.Reset();
            for (int i = 0; i < Brains.Count && i < Def.Foes.Length; i++)
            {
                var feet = Ground(Def.ToWorld(Def.Foes[i].Local));
                Brains[i].ResetBrain(feet, FaceCenterYaw(feet));
            }
            for (int i = 0; i < Crowd.Count && i < Def.Crowd.Length; i++)
            {
                var c = Crowd[i];
                c.gameObject.SetActive(true);
                var p = Ground(Def.ToWorld(Def.Crowd[i]));
                c.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, FaceCenterYaw(p), 0f));
                c.SetHome();
            }
            var me = Player.Me;
            me.ResetFighter();
            me.ScriptAnim = null;
            var spot = Ground(Def.ToWorld(Def.RetryLocal));
            (me.Body as PlayerBody)?.Place(spot, Def.Yaw + Def.RetryYaw);
            Player.Heat.Set(Player.T.HeatStart);
            rearmOnLeave = !retry;
            if (GameUi.RetryHook == (Action)RetryFromMenu) GameUi.RetryHook = null;
            Go(Phase.Armed);
        }

        /// 일시정지 메뉴 '전투 다시'(이긴 뒤): 처음 자리로 돌린 뒤 주차장 안쪽에서 바로 시비
        public void RetryFromMenu()
        {
            ResetAll(true);
            var me = Player.Me;
            var spot = Ground(Def.ToWorld(new Vector2(0f, 2.5f)));
            (me.Body as PlayerBody)?.Place(spot, Def.Yaw + 180f);
            Trigger();
        }
    }
}
