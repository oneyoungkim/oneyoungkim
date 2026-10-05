// 행인1의 메인이벤트 — 체크포인트 길잡이(06 문서 8·9장). 체크포인트를 순서대로 켠다.
// 지금 목표만 기둥·빛 기둥·깃발·이름표를 보이고, 지난 것은 바닥 원판만 옅게, 아직 안 온 것은 숨긴다.
// 도착 = 시우 발이 원판 반경 안(수평) + 높이차 3m 안. 부품은 Zone1Builder 가 만들고 RouteSetup(에디터)이 이 컴포넌트에 연결한다.
// HUD·대사·퀘스트 코드는 RouteGuide.Current 의 공개 값과 이벤트만 쓰면 된다(CurrentTarget · Index · Distance · Reached · Completed).
using System;
using UnityEngine;

namespace Haengin
{
    [DefaultExecutionOrder(50)]
    public sealed class RouteGuide : MonoBehaviour
    {
        [Serializable]
        public sealed class Stop
        {
            public string Name;          // 데이터 이름("5. 반시우네 집 앞")
            public string Label;         // 보여 줄 이름("반시우네 집 앞") — HUD 용, 괄호 뺌
            public Vector3 Pos;          // 원판 가운데(발밑 높이)
            public float Radius = 2.5f;
            public MeshRenderer Disc;    // 바닥 원판(지금 = 진하게, 지남 = 옅게)
            public GameObject Ring, Pillar, Flag, Beam, Tag;  // 테두리 · 3m 기둥 · 깃발 · 빛 기둥 · 이름표
        }

        public enum State { Hidden, Active, Done }

        public Stop[] Stops = new Stop[0];
        public Material ActiveDisc, DoneDisc;
        [Tooltip("시작할 때 목표(0부터). 1번 체크포인트가 시작 지점이라 기본 1 = 2번 체크포인트")]
        public int StartIndex = 1;
        [Tooltip("도착 판정 높이차(m)")]
        public float ArriveHeight = 3f;

        /// 지금 장면의 길잡이(없으면 null)
        public static RouteGuide Current { get; private set; }
        /// 지금 목표 인덱스(Stops 기준). Stops.Length 면 끝남
        public int Index { get; private set; } = -1;
        public bool Finished => Stops == null || Index >= Stops.Length;
        /// 지금 목표(끝났으면 null)
        public Stop CurrentTarget => Finished || Index < 0 ? null : Stops[Index];
        /// 마지막으로 도착한 체크포인트(없으면 null)
        public Stop LastReached { get; private set; }
        /// 시우에서 지금 목표까지 수평 거리(m). 끝났거나 시우가 없으면 0
        public float Distance { get; private set; }
        /// 체크포인트에 도착했을 때(도착한 인덱스)
        public event Action<int> Reached;
        /// 마지막 체크포인트까지 도착했을 때
        public event Action Completed;

        Transform player;

        void OnEnable() { Current = this; }
        void OnDisable() { if (Current == this) Current = null; }

        void Start()
        {
            if (Index < 0) SetIndex(StartIndex);
        }

        /// 따라갈 대상(기본 = 장면의 PlayerMotor)
        public void SetPlayer(Transform t) => player = t;

        void Update()
        {
            if (Index < 0) SetIndex(StartIndex);
            if (player == null)
            {
                var m = FindAnyObjectByType<PlayerMotor>();
                if (m == null) return;
                player = m.transform;
            }
            var t = CurrentTarget;
            if (t == null) { Distance = 0f; return; }
            var p = player.position;
            Distance = new Vector2(p.x - t.Pos.x, p.z - t.Pos.z).magnitude;
            if (Distance <= t.Radius && Mathf.Abs(p.y - t.Pos.y) <= ArriveHeight) Arrive();
        }

        void Arrive()
        {
            int i = Index;
            LastReached = Stops[i];
            SetIndex(i + 1);
            Reached?.Invoke(i);
            if (Finished) Completed?.Invoke();
        }

        /// 목표를 i 로(앞은 지남, 뒤는 숨김). 테스트·디버그·이어하기용
        public void SetIndex(int i)
        {
            Index = Mathf.Clamp(i, 0, Stops != null ? Stops.Length : 0);
            if (Stops == null) return;
            for (int k = 0; k < Stops.Length; k++)
                Apply(Stops[k], k < Index ? State.Done : k == Index ? State.Active : State.Hidden);
            var t = CurrentTarget;
            if (t != null && player != null)
                Distance = new Vector2(player.position.x - t.Pos.x, player.position.z - t.Pos.z).magnitude;
        }

        public void Apply(Stop s, State st)
        {
            if (s == null) return;
            bool on = st == State.Active;
            if (s.Disc != null)
            {
                s.Disc.gameObject.SetActive(st != State.Hidden);
                var m = st == State.Done ? DoneDisc : ActiveDisc;
                if (m != null && s.Disc.sharedMaterial != m) s.Disc.sharedMaterial = m;
            }
            Set(s.Ring, on); Set(s.Pillar, on); Set(s.Flag, on); Set(s.Beam, on); Set(s.Tag, on);
        }

        static void Set(GameObject g, bool on) { if (g != null && g.activeSelf != on) g.SetActive(on); }

        /// HUD 한 줄: "다음: 반시우네 집 앞 · 23m" (끝나면 "도착: …")
        public string HudLine()
        {
            var t = CurrentTarget;
            if (t != null) return $"다음: {t.Label} · {Mathf.RoundToInt(Distance)}m";
            return LastReached != null ? $"도착: {LastReached.Label}" : "";
        }
    }
}
