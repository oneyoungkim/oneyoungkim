// 행인1의 메인이벤트 — 공격권: 동시에 때리는 수 제한 (docs/08_M2_전투_설계.md 4-2 '자리'·4-3)
// 인카운터마다 1개. 동시 공격 최대 1명(어려움 2), '공격 중' = 공격권을 받은 뒤 들어가기·예고·발생·판정·회복·연결 전체.
// 받을 조건: 간보기 상태, 개인 쿨다운 끝, 시우가 다운·기상·기세 액션 중이 아님, 시우가 맞아 경직 중이 아님.
// 고르는 순서: 기다린 시간이 긴 적(화면 밖은 기다린 시간 × 0.5) → 가까운 적. 한 적 공격이 끝난 뒤 다음 적 시작까지 0.6초. 굶김 한도 15초.
// 자리: 공격권 가진 적 2.0~2.6m, 나머지 3.5~5.0m 링, 시우 둘레 70° 이상 떨어지게, 카메라 정면 ±40° 쪽으로 당김.
using System.Collections.Generic;
using UnityEngine;

namespace Haengin
{
    [DefaultExecutionOrder(-30), DisallowMultipleComponent]
    public sealed class AttackDirector : MonoBehaviour
    {
        public Fighter Player;
        public CombatTuning Tuning;
        [Tooltip("음수 = 조정값(보통 1 · 어려움 2)")] public int MaxAttackers = -1;
        [Tooltip("기세 액션 중(10단계): 공격권 안 줌")] public bool Frozen;
        public readonly List<EnemyBrain> Members = new List<EnemyBrain>();

        public CombatTuning T => Tuning != null ? Tuning : CombatTuning.Default;
        public int Max => MaxAttackers > 0 ? MaxAttackers : T.MaxAttackers;
        double clock, lastEnd = -1e9;

        /// 공격 시작 기록(테스트·디버그): (적, 게임 시각)
        public readonly List<(EnemyBrain who, double at)> Starts = new List<(EnemyBrain, double)>();
        public double Clock => clock;
        public int Attacking { get { int n = 0; foreach (var m in Members) if (m != null && m.HasToken) n++; return n; } }

        public void Add(EnemyBrain b) { if (b != null && !Members.Contains(b)) { Members.Add(b); b.Director = this; } }

        /// 시우가 새 공격권을 받을 수 있는 상태인가(다운·기상·탈락·경직 아님)
        public bool PlayerOpen
        {
            get
            {
                if (Player == null) return true;
                var s = Player.State;
                return !(Player.Down || s == Fighter.Phase.Out || s == Fighter.Phase.Stagger || s == Fighter.Phase.Grabbed || Player.KO);
            }
        }

        public void Release(EnemyBrain b)
        {
            if (b == null || !b.HasToken) return;
            b.HasToken = false;
            lastEnd = clock;
        }

        void Update()
        {
            float dt = TimeFx.Dt;
            if (dt <= 0f) return;
            clock += dt;
            AssignSlots();
            if (Frozen || !PlayerOpen) return;
            if (Attacking >= Max) return;
            if (clock - lastEnd < T.AttackGap - 1e-6) return;
            EnemyBrain best = null;
            float bestScore = float.MinValue;
            var cam = Camera.main;
            foreach (var m in Members)
            {
                if (m == null || !m.WantsToken) continue;
                float wait = (float)(clock - m.WaitSince);
                // 화면 밖은 기다린 시간 × 0.5 — 단 굶김 한도의 2/3 를 넘으면 화면 안처럼(15초 안에 꼭 받게)
                bool onScreen = OnScreen(cam, m.Me) || wait >= T.StarveLimit * 0.66f;
                float score = wait * (onScreen ? 1f : 0.5f) + (onScreen ? 0.01f : 0f) - Dist(m) * 0.001f;
                if (score > bestScore) { bestScore = score; best = m; }
            }
            if (best == null) return;
            best.Grant();
            Starts.Add((best, clock));
        }

        float Dist(EnemyBrain m) => Player != null ? HitResolver.Flat(m.Me.Position - Player.Position).magnitude : 0f;

        public static bool OnScreen(Camera cam, Fighter f)
        {
            if (cam == null || f == null) return true;
            var v = cam.WorldToViewportPoint(f.Chest);
            return v.z > 0f && v.x > 0f && v.x < 1f && v.y > 0f && v.y < 1f;
        }

        // ───────────────────────── 자리(4-2)
        readonly List<EnemyBrain> live = new List<EnemyBrain>();
        void AssignSlots()
        {
            if (Player == null) return;
            var t = T;
            live.Clear();
            foreach (var m in Members) if (m != null && m.UsesSlot) live.Add(m);
            if (live.Count == 0) return;
            var p = Player.Position;
            // 지금 각도에서 시작(시우 기준, 북 = 0, 시계 방향)
            foreach (var m in live) if (!m.SlotSet) { m.SlotAngle = HitResolver.Yaw(HitResolver.Flat(m.Me.Position - p)); m.SlotSet = true; }
            // 카메라 정면(시우 → 카메라가 보는 쪽) ±CamArc 쪽으로 천천히 당김
            var cam = Camera.main;
            if (cam != null)
            {
                float cf = HitResolver.Yaw(HitResolver.Flat(cam.transform.forward));
                foreach (var m in live)
                {
                    float d = Mathf.DeltaAngle(cf, m.SlotAngle);
                    if (Mathf.Abs(d) > t.CamArc) m.SlotAngle = Mathf.MoveTowardsAngle(m.SlotAngle, cf + Mathf.Sign(d) * t.CamArc, 20f * TimeFx.Dt);
                }
            }
            // 서로 70° 이상(먼저 온 적 우선 = 목록 순서): 세 번 밀어내기
            for (int it = 0; it < 3; it++)
                for (int i = 0; i < live.Count; i++)
                    for (int j = i + 1; j < live.Count; j++)
                    {
                        float d = Mathf.DeltaAngle(live[i].SlotAngle, live[j].SlotAngle);
                        if (Mathf.Abs(d) >= t.SlotSep) continue;
                        float push = (t.SlotSep - Mathf.Abs(d)) * 0.5f * (d >= 0f ? 1f : -1f);
                        live[i].SlotAngle -= push * 0.5f;
                        live[j].SlotAngle += push * 1.5f;
                    }
            foreach (var m in live)
            {
                float r = m.HasToken ? t.RingNear : t.RingFar + m.SlotJitter;
                m.Slot = p + HitResolver.YawDir(m.SlotAngle) * r;
            }
        }
    }
}
