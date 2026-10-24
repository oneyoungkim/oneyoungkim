// 행인1의 메인이벤트 — 손 자세(docs/09_M3_버티컬슬라이스_설계.md 4-2 #2·#3, FUJIMOTO 7-1)
// 정식 모델의 손가락 뼈(tools/formal_model.py: {Left,Right}Hand{Thumb,Index,Middle,Ring,Pinky}{1,2,3} + 손바닥 표시 {Side}HandPalm → {Side}HandPalmN)를
// 마디 각도표(HandPoseSet — 인물·손마다 관통 검사로 맞춘 값)로 굽힌다. 애니메이터가 뼈를 쓴 뒤(LateUpdate)에 덮어쓴다.
//   설계 4-2 는 '애니메이터 손 층 + 구운 클립'이었으나, 클립 대신 각도표를 실행 중에 바로 넣는다(6-5-2 달라진 점): 자세마다 클립·마스크가 필요 없고,
//   주먹 무게(HandShape 0~100)를 그대로 받아 0.08초 보간 시간을 M2 와 똑같이 지킨다.
// 축은 뼈 로컬 축에 기대지 않는다: 처음 한 번 손바닥 표시 두 점(손바닥 쪽 n)·손가락 방향 d 로 굽힘 축 = d × n 을 구해 마디마다 로컬로 옮겨 둔다.
// 손가락 총 자세는 없다(FUJIMOTO 1-4).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Haengin
{
    public static class HandPoseLib
    {
        public const string Open = "편 손", Fist = "주먹", Collar = "멱살 잡기", ThumbUp = "엄지 척", Peace = "피스", Bottle = "병 쥐기", Pocket = "주머니 손", WristGrip = "손목 그립";
        public static readonly string[] Names = { Open, Fist, Collar, ThumbUp, Peace, Bottle, Pocket, WristGrip };

        static FingerPose P(float a, float b, float c, float s = 0f) => new FingerPose(a, b, c, s);

        static HandPoseDef D(string n, FingerPose t, FingerPose i, FingerPose m, FingerPose r, FingerPose p, float opp) =>
            new HandPoseDef { Name = n, F = new[] { t, i, m, r, p }, ThumbOpp = opp };

        /// 기본 각도표(도). 인물마다 FormalSetup 이 관통 검사로 줄여 HandPoseSet 에 저장한다
        public static HandPoseDef[] Defaults() => new[]
        {
            D(Open,      P(5, 5, 5),        P(8, 10, 5, -2),   P(10, 12, 6),     P(12, 14, 7, -2),  P(14, 16, 8, -3),  10f),
            D(Fist,      P(25, 35, 30),     P(85, 100, 60),    P(88, 100, 60),   P(90, 100, 60),    P(92, 100, 60),    40f),
            D(Collar,    P(20, 25, 20),     P(55, 70, 45),     P(58, 72, 45),    P(60, 72, 45),     P(62, 72, 45),     35f),
            D(ThumbUp,   P(-5, 0, 0, 10),   P(85, 100, 60),    P(88, 100, 60),   P(90, 100, 60),    P(92, 100, 60),    0f),
            D(Peace,     P(30, 40, 30),     P(0, 0, 0, 10),    P(0, 0, 0, 10),   P(85, 100, 60),    P(88, 100, 60),    45f),
            D(Bottle,    P(15, 15, 10),     P(45, 55, 35),     P(45, 55, 35),    P(45, 55, 35),     P(45, 55, 35),     55f),
            D(Pocket,    P(-5, 0, 0, 15),   P(25, 30, 20, -3), P(25, 30, 20),    P(25, 30, 20, -3), P(25, 30, 20, -3), 0f),
            D(WristGrip, P(25, 20, 15),     P(65, 75, 45),     P(65, 75, 45),    P(65, 75, 45),     P(65, 75, 45),     60f),
        };

        static Dictionary<string, HandPoseDef> byName;
        public static HandPoseDef Default(string name)
        {
            if (byName == null) { byName = new Dictionary<string, HandPoseDef>(); foreach (var d in Defaults()) byName[d.Name] = d; }
            return name != null && byName.TryGetValue(name, out var r) ? r : null;
        }

        /// 손가락 총 모양(검지·엄지 펴고 나머지 접음)인가 — D13 이 '없음'을 확인
        public static bool IsFingerGun(HandPoseDef d) =>
            d.F[1].A < 15f && d.F[1].B < 15f && d.F[0].A < 15f && d.F[2].A > 60f && d.F[3].A > 60f && d.F[4].A > 60f;
    }

    [DefaultExecutionOrder(17), DisallowMultipleComponent]
    public sealed class HandPoser : MonoBehaviour
    {
        public static readonly string[] FingerNames = { "Thumb", "Index", "Middle", "Ring", "Pinky" };

        public HandPoseSet Set;
        [Tooltip("연출(Force) 자세 보간 시간(초)")] public float Blend = 0.08f;

        sealed class Joint { public Transform T; public Quaternion Rest; public Vector3 Curl, Spread, Opp; public float SpreadSign = 1f, OppSign = 1f; }

        sealed class Hand
        {
            public bool Right, Ok;
            public Transform Root, Palm, PalmN;
            public readonly Joint[,] J = new Joint[5, 3];
            // 몰기(HandShape: 바로) · 강제(연출: 보간)
            public string DrivePose = HandPoseLib.Open; public float DriveW;
            public string DrivePose2; public float DriveW2;
            public string ForcePose; public float ForceW;
            public HandPoseDef From, To, Cur = new HandPoseDef(), Tgt = new HandPoseDef();
            public float T = 1f;
            public bool WasForced;
            public string Shown = HandPoseLib.Open; public float ShownW;
        }

        readonly Hand[] hands = { new Hand { Right = false }, new Hand { Right = true } };
        bool bound;

        public bool Ready { get { Bind(); return hands[0].Ok && hands[1].Ok; } }
        /// 지금 보이는 자세 이름 · 무게(테스트)
        public string PoseOf(bool right) => hands[right ? 1 : 0].Shown;
        public float WeightOf(bool right) => hands[right ? 1 : 0].ShownW;
        public Transform PalmOf(bool right) { Bind(); return hands[right ? 1 : 0].Palm; }
        public Transform PalmNOf(bool right) { Bind(); return hands[right ? 1 : 0].PalmN; }
        public Transform HandOf(bool right) { Bind(); return hands[right ? 1 : 0].Root; }

        void Awake() => Bind();

        static Transform Find(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var r = Find(c, name); if (r != null) return r; }
            return null;
        }

        /// 뼈 찾기 + 축 계산(지금 자세 = 손가락 휴지 자세여야 함 — 프리팹 기본값·FBX 노드 기본값은 손가락이 휴지 그대로)
        public void Bind(bool force = false)
        {
            if (bound && !force) return;
            bound = true;
            foreach (var h in hands)
            {
                string sd = h.Right ? "Right" : "Left";
                h.Root = Find(transform, sd + "Hand");
                h.Palm = Find(transform, sd + "HandPalm");
                h.PalmN = Find(transform, sd + "HandPalmN");
                h.Ok = h.Root != null && h.Palm != null && h.PalmN != null;
                for (int f = 0; f < 5 && h.Ok; f++)
                    for (int j = 0; j < 3; j++)
                    {
                        var t = Find(transform, $"{sd}Hand{FingerNames[f]}{j + 1}");
                        if (t == null) { h.Ok = false; break; }
                        h.J[f, j] = new Joint { T = t, Rest = t.localRotation };
                    }
                if (!h.Ok) continue;
                Vector3 n = (h.PalmN.position - h.Palm.position).normalized;
                Vector3 fwd = (h.J[2, 0].T.position - h.Root.position).normalized;
                n = (n - fwd * Vector3.Dot(n, fwd)).normalized;
                for (int f = 0; f < 5; f++)
                {
                    Vector3 d = (h.J[f, 2].T.position - h.J[f, 0].T.position).normalized;
                    Vector3 bend = n;
                    if (f == 0)
                    {
                        // 엄지는 손바닥 쪽 + 새끼 쪽(손바닥 가로질러)으로 굽는다
                        Vector3 across = h.J[4, 0].T.position - h.J[0, 0].T.position;
                        across = (across - d * Vector3.Dot(across, d)).normalized;
                        bend = (n * 0.7f + across * 0.7f).normalized;
                    }
                    Vector3 curl = Vector3.Cross(d, bend).normalized;
                    // 벌림 부호: + 면 기준 손가락(가운데 · 가운데는 검지 · 엄지는 검지)에서 멀어지게
                    int refF = f == 2 ? 1 : f == 0 ? 1 : 2;
                    Vector3 toRef = h.J[refF, 0].T.position - h.J[f, 0].T.position;
                    Vector3 moved = Quaternion.AngleAxis(10f, n) * d - d;
                    float ss = Vector3.Dot(moved, toRef) > 0f ? -1f : 1f;
                    // 엄지 맞섬 부호: + 면 엄지 끝이 손바닥 쪽(n)으로
                    Vector3 mo = Quaternion.AngleAxis(10f, fwd) * d - d;
                    float os = Vector3.Dot(mo, n) >= 0f ? 1f : -1f;
                    for (int j = 0; j < 3; j++)
                    {
                        var jt = h.J[f, j];
                        var inv = Quaternion.Inverse(jt.T.rotation);
                        jt.Curl = inv * curl;
                        jt.Spread = inv * n;
                        jt.Opp = inv * fwd;
                        jt.SpreadSign = ss;
                        jt.OppSign = os;
                    }
                }
                h.From = Pose(h.Right, HandPoseLib.Open);
                h.To = h.From;
                h.Cur = h.From.Clone();
            }
        }

        /// 이 인물·손의 자세(맞춘 값 → 없으면 기본값)
        public HandPoseDef Pose(bool right, string name)
        {
            var p = Set != null ? Set.Find(right, name) : null;
            return p ?? HandPoseLib.Default(name) ?? HandPoseLib.Default(HandPoseLib.Open);
        }

        /// 매 프레임 목표(HandShape): 편 손 → pose 를 w 만큼. 보간 없이 바로(무게 보간은 HandShape 가 0.08초로)
        public void Drive(bool right, string pose, float w, string pose2 = null, float w2 = 0f)
        {
            var h = hands[right ? 1 : 0];
            h.DrivePose = pose ?? HandPoseLib.Open;
            h.DriveW = Mathf.Clamp01(w);
            h.DrivePose2 = pose2;
            h.DriveW2 = Mathf.Clamp01(w2);
        }

        /// 연출 강제(컷신 손 트랙): w ≤ 0 이면 풂. side: null = 두 손
        public void Force(string pose, float w, bool? right = null)
        {
            foreach (var h in hands)
            {
                if (right.HasValue && right.Value != h.Right) continue;
                h.ForcePose = w > 0f ? pose : null;
                h.ForceW = Mathf.Clamp01(w);
            }
        }

        public string ForcedPose(bool right) => hands[right ? 1 : 0].ForcePose;

        void LateUpdate()
        {
            if (!Ready) return;
            float dt = Time.deltaTime;
            foreach (var h in hands)
            {
                bool forced = h.ForcePose != null;
                string name = forced ? h.ForcePose : h.DrivePose;
                float w = forced ? h.ForceW : h.DriveW;
                var open = Pose(h.Right, HandPoseLib.Open);
                var target = HandPoseDef.Lerp(open, Pose(h.Right, name), w, h.Tgt);
                if (!forced && h.DrivePose2 != null && h.DriveW2 > 0f)
                {
                    // 두 자세 섞기(주먹 → 멱살 잡기 넘어갈 때 튀지 않게): 편 손 + (A − 편 손)·wA + (B − 편 손)·wB
                    var b = Pose(h.Right, h.DrivePose2);
                    for (int f = 0; f < 5; f++)
                    {
                        var t = target.F[f]; var o = open.F[f]; var bb = b.F[f]; float k = h.DriveW2;
                        target.F[f] = new FingerPose(t.A + (bb.A - o.A) * k, t.B + (bb.B - o.B) * k, t.C + (bb.C - o.C) * k, t.Spread + (bb.Spread - o.Spread) * k);
                    }
                    target.ThumbOpp += (b.ThumbOpp - open.ThumbOpp) * h.DriveW2;
                    if (h.DriveW2 > w) name = h.DrivePose2;
                    w = Mathf.Max(w, h.DriveW2);
                }
                // 연출 자세가 켜지거나 바뀌거나 풀리면 지금 모양에서 Blend 초 보간. 그 밖(HandShape 몰기)은 바로 — 무게 보간은 HandShape 가 한다
                if (forced != h.WasForced || (forced && (h.Shown != name || !Mathf.Approximately(h.ShownW, w)))) { h.From = h.Cur.Clone(); h.T = 0f; }
                h.WasForced = forced;
                if (h.T < 1f)
                {
                    h.T = Mathf.Min(1f, h.T + dt / Mathf.Max(1e-3f, Blend));
                    HandPoseDef.Lerp(h.From, target, h.T, h.Cur);
                }
                else HandPoseDef.Lerp(target, target, 1f, h.Cur);
                h.Shown = name; h.ShownW = w;
                Apply(h, h.Cur);
            }
        }

        static void Apply(Hand h, HandPoseDef p)
        {
            for (int f = 0; f < 5; f++)
            {
                var fp = p.F[f];
                for (int j = 0; j < 3; j++)
                {
                    var jt = h.J[f, j];
                    var q = jt.Rest;
                    if (j == 0)
                    {
                        q *= Quaternion.AngleAxis(fp.Spread * jt.SpreadSign, jt.Spread);
                        if (f == 0) q *= Quaternion.AngleAxis(p.ThumbOpp * jt.OppSign, jt.Opp);
                    }
                    q *= Quaternion.AngleAxis(fp[j], jt.Curl);
                    jt.T.localRotation = q;
                }
            }
        }

        /// 바로 이 자세로(편집 모드 맞춤·테스트). 애니메이터가 없을 때 쓴다
        public void ApplyNow(bool right, HandPoseDef p)
        {
            Bind();
            var h = hands[right ? 1 : 0];
            if (!h.Ok) return;
            h.Cur = p.Clone();
            Apply(h, p);
        }

        /// 손가락을 휴지 자세로
        public void ResetRest()
        {
            Bind();
            foreach (var h in hands)
                if (h.Ok)
                    for (int f = 0; f < 5; f++) for (int j = 0; j < 3; j++) h.J[f, j].T.localRotation = h.J[f, j].Rest;
        }
    }
}
