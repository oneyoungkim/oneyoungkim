// 행인1의 메인이벤트 — 손가락 관통 검사(D13: 손 자세 8종에서 손가락끼리·손바닥 관통 ≤ 4mm, 정점 검사)
// 굽힌 자세로 손 정점을 직접 스키닝해(월드 m — BakeMesh 는 FBX 배율 0.01 에 단위가 흔들려 쓰지 않음) 손가락 2·3마디 정점(가장 큰 가중치 ≥ 0.5)마다
//   ① 다른 손가락: 마디마다 캡슐(뼈 → 다음 뼈, 끝마디는 끝점) — 반지름 = 휴지 자세에서 그 마디 정점이 뼈 축에서 떨어진 거리의 60% 백분위.
//      깊이 = 반지름 − 축까지 거리(캡슐 안으로 들어간 만큼)
//   ② 손바닥: 가장 가까운 손바닥(손 뼈) 정점의 노멀 면 아래로 들어간 깊이(손바닥은 면이 또렷해 노멀이 잘 맞음)
//   두 깊이 중 큰 것에서 휴지 자세에서 이미 있던 깊이(Tripo 손가락은 붙어 모델링됨)를 뺀다. 휴지 자세에서 이웃 캡슐 면 3mm 안에 있던 정점은 그 이웃과의 캡슐 검사에서 뺀다(붙은 살).
//   2026-10-25: 처음 판(가장 가까운 이웃 손가락 정점의 노멀 면 깊이)은 붙어 있는 손가락이 서로 다르게 굽을 때 손등 쪽 노멀 때문에 '옆으로 비킨 것'을
//   8mm 관통으로 셌다(가운뎃손가락만 20° 굽혀도 7.8mm) → 손가락끼리는 캡슐로 바꿈.
// 자기 마디 축에서 반지름 × 1.8 + 2mm 넘게 떨어진 정점(물갈퀴 살 — 손가락 나누기가 이웃 쪽 살을 붙인 것)도 뺀다(Map.WebCount 로 셈).
// 뼈 줄기가 가운뎃손가락과 45° 넘게 어긋난 손가락(BadChain — 태오 오른손 새끼가 손바닥을 가로질러 놓임)은 캡슐 쌍에서 뺀다(손바닥 검사는 함).
// 1마디(밑 마디)는 뺀다 — 손가락을 굽히면 밑 마디 주름이 손바닥 쪽으로 눌리는 것은 관통이 아니라 피부 접힘이라서.
// FormalSetup(편집 모드 — 각도표 맞춤)과 테스트 D13(실행 — 확인)이 같은 계산을 쓴다. 메시는 Read/Write 켜져 있어야 함(정식 모델 FBX).
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Haengin
{
    public static class HandCheck
    {
        public sealed class Map
        {
            public SkinnedMeshRenderer Smr;
            /// 정점 부위: -1 기타 · 손가락 f*3 + 마디 j(0~14) · 손 뼈 20 — 오른손은 +100
            public int[] Cls;
            public readonly int[][] Probe = new int[2][];       // 손마다 검사할 정점(2·3마디)
            public readonly int[][] Palm = new int[2][];        // 손마다 손바닥 정점
            public int[] Hand;                                  // 손 정점 전부
            public float[] Rest;                                // 휴지 자세 깊이(m)
            public int[] Fused;
            public bool[] Web;                                  // 자기 마디 축에서 반지름 × 1.8 + 2mm 넘게 떨어진 정점(손가락 사이 물갈퀴 살·잘못 나뉜 정점) — 검사에서 뺀다
            public int WebCount;
            /// 휴지 자세에서 뼈 줄기가 가운뎃손가락과 45° 넘게 어긋난 손가락(자동 리깅이 손가락 줄기를 잘못 놓은 것) — 손마다 비트. 그 손가락이 끼는 캡슐 쌍은 뺀다
            public readonly int[] BadChain = new int[2];                                 // 정점마다 휴지 자세에서 이미 붙어 있던 이웃 손가락(비트 0~4) — 그 쌍은 캡슐 검사에서 뺀다
            public Mesh Baked;                                  // (예전 호환 — 쓰지 않음)
            public Vector3[] V, N;
            public BoneWeight[] W;
            public Matrix4x4[] Bind;
            public Vector3[] P, PN;                             // 지금 자세 월드 위치·노멀(손 정점만)
            public readonly Transform[,,] Bone = new Transform[2, 5, 3];
            public readonly Vector3[,] TipLocal = new Vector3[2, 5];   // 끝마디 뼈 공간 끝점
            public readonly float[,,] Radius = new float[2, 5, 3];
            public readonly Vector3[,,] A = new Vector3[2, 5, 3], B = new Vector3[2, 5, 3];   // 지금 자세 캡슐 축
        }

        public static int FingerOf(int cls) => cls < 0 ? -1 : (cls % 100) >= 20 ? -1 : (cls % 100) / 3;
        public static int SegOf(int cls) => cls < 0 ? -1 : (cls % 100) >= 20 ? -1 : (cls % 100) % 3;
        public static bool RightOf(int cls) => cls >= 100;

        public static Map Build(SkinnedMeshRenderer smr)
        {
            var mesh = smr.sharedMesh;
            var bones = smr.bones;
            var bc = new int[bones.Length];
            var m = new Map { Smr = smr, Baked = new Mesh() };
            for (int i = 0; i < bones.Length; i++)
            {
                bc[i] = -1;
                var b = bones[i];
                if (b == null) continue;
                string n = b.name;
                int side = n.StartsWith("Right") ? 100 : n.StartsWith("Left") ? 0 : -1;
                if (side < 0) continue;
                string rest = n.Substring(side == 100 ? 5 : 4);
                if (rest == "Hand") { bc[i] = side + 20; continue; }
                for (int f = 0; f < 5; f++)
                    for (int j = 0; j < 3; j++)
                        if (rest == "Hand" + HandPoser.FingerNames[f] + (j + 1)) { bc[i] = side + f * 3 + j; m.Bone[side == 100 ? 1 : 0, f, j] = b; }
            }
            var bw = mesh.boneWeights;
            m.Cls = new int[bw.Length];
            var probe = new[] { new List<int>(), new List<int>() };
            var palm = new[] { new List<int>(), new List<int>() };
            var hand = new List<int>();
            for (int v = 0; v < bw.Length; v++)
            {
                var w = bw[v];
                int c = w.weight0 >= 0.5f ? bc[w.boneIndex0] : -1;
                m.Cls[v] = c;
                if (c < 0) continue;
                int h = c >= 100 ? 1 : 0;
                hand.Add(v);
                if (FingerOf(c) < 0) { if (w.weight0 >= 0.9f) palm[h].Add(v); }   // 손바닥 면 = 손 뼈 90% 이상(손가락 밑동과 섞인 손등 마디 살은 손가락이 굽으면 같이 움직여 기준 면이 안 됨)
                else if (SegOf(c) >= 1) probe[h].Add(v);
            }
            for (int h = 0; h < 2; h++) { m.Probe[h] = probe[h].ToArray(); m.Palm[h] = palm[h].ToArray(); }
            m.Hand = hand.ToArray();
            m.V = mesh.vertices; m.N = mesh.normals; m.W = bw; m.Bind = mesh.bindposes;
            m.P = new Vector3[bw.Length]; m.PN = new Vector3[bw.Length];
            return m;
        }

        /// 손 정점을 지금 뼈 자세로 월드에 놓고(선형 블렌드 스키닝 — Unity 와 같은 식) 캡슐 축을 구한다
        static void Skin(Map m)
        {
            var bones = m.Smr.bones;
            var mats = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++) mats[i] = bones[i] != null ? bones[i].localToWorldMatrix * m.Bind[i] : Matrix4x4.identity;
            foreach (int v in m.Hand)
            {
                var w = m.W[v];
                Vector3 p = Vector3.zero, n = Vector3.zero;
                void Add(int bi, float wt)
                {
                    if (wt <= 0f) return;
                    var M = mats[bi];
                    p += M.MultiplyPoint3x4(m.V[v]) * wt;
                    n += M.MultiplyVector(m.N[v]) * wt;
                }
                Add(w.boneIndex0, w.weight0); Add(w.boneIndex1, w.weight1); Add(w.boneIndex2, w.weight2); Add(w.boneIndex3, w.weight3);
                m.P[v] = p; m.PN[v] = n.normalized;
            }
            for (int h = 0; h < 2; h++)
                for (int f = 0; f < 5; f++)
                    for (int j = 0; j < 3; j++)
                    {
                        var t = m.Bone[h, f, j];
                        if (t == null) continue;
                        m.A[h, f, j] = t.position;
                        m.B[h, f, j] = j < 2 && m.Bone[h, f, j + 1] != null ? m.Bone[h, f, j + 1].position : t.TransformPoint(m.TipLocal[h, f]);
                    }
        }

        static float SegDist(Vector3 p, Vector3 a, Vector3 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-8f, ab.sqrMagnitude));
            return (p - (a + ab * t)).magnitude;
        }

        /// 휴지 자세 기준선(손가락 휴지 자세에서 부를 것): 끝마디 끝점 · 마디 반지름 · 휴지 깊이
        public static void SetRest(Map m)
        {
            // 끝점: 끝마디 정점 중 뼈에서 가장 먼 6개의 평균
            Skin(m);
            for (int h = 0; h < 2; h++)
                for (int f = 0; f < 5; f++)
                {
                    var t = m.Bone[h, f, 2];
                    if (t == null) continue;
                    int cls = (h == 1 ? 100 : 0) + f * 3 + 2;
                    var far = m.Hand.Where(v => m.Cls[v] == cls).OrderByDescending(v => (m.P[v] - t.position).sqrMagnitude).Take(6).ToList();
                    var tip = far.Count > 0 ? far.Aggregate(Vector3.zero, (s, v) => s + m.P[v]) / far.Count : t.position + (t.position - (m.Bone[h, f, 1] != null ? m.Bone[h, f, 1].position : t.parent.position)) * 0.8f;
                    m.TipLocal[h, f] = t.InverseTransformPoint(tip);
                }
            Skin(m);
            for (int h = 0; h < 2; h++)
                for (int f = 0; f < 5; f++)
                    for (int j = 0; j < 3; j++)
                    {
                        int cls = (h == 1 ? 100 : 0) + f * 3 + j;
                        var ds = m.Hand.Where(v => m.Cls[v] == cls).Select(v => SegDist(m.P[v], m.A[h, f, j], m.B[h, f, j])).OrderBy(x => x).ToList();
                        m.Radius[h, f, j] = ds.Count > 0 ? ds[(int)(ds.Count * 0.6f)] : 0.007f;
                    }
            for (int h = 0; h < 2; h++)
            {
                m.BadChain[h] = 0;
                if (m.Bone[h, 2, 0] == null) continue;
                var mid = (m.B[h, 2, 0] - m.A[h, 2, 0]).normalized;
                for (int f = 1; f < 5; f++)
                    if (m.Bone[h, f, 0] != null && Vector3.Angle(m.B[h, f, 0] - m.A[h, f, 0], mid) > 45f) m.BadChain[h] |= 1 << f;
            }
            // 물갈퀴: 자기 마디 캡슐에서 멀리 떨어진 정점(formal_model.py 의 손가락 나누기가 이웃 손가락 쪽 살을 붙인 것)
            m.Web = new bool[m.P.Length]; m.WebCount = 0;
            for (int h = 0; h < 2; h++)
                foreach (int v in m.Probe[h])
                {
                    int c = m.Cls[v], f = FingerOf(c), j = SegOf(c);
                    if (m.Bone[h, f, j] == null) continue;
                    float own = SegDist(m.P[v], m.A[h, f, j], m.B[h, f, j]);
                    if (own > m.Radius[h, f, j] * 1.8f + 0.002f) { m.Web[v] = true; m.WebCount++; }
                }
            // 붙은 쌍: 휴지 자세에서 이웃 손가락 캡슐 면에서 3mm 안(Tripo 손가락은 붙어 모델링돼 갈라진 틈이 없다 — 따로 굽으면 늘어난 살이 이웃 캡슐에 걸려 관통처럼 보임)
            m.Fused = new int[m.P.Length];
            for (int h = 0; h < 2; h++)
                foreach (int v in m.Probe[h])
                {
                    int fv = FingerOf(m.Cls[v]);
                    for (int g = 0; g < 5; g++)
                    {
                        if (g == fv) continue;
                        for (int k = 0; k < 3; k++)
                            if (m.Bone[h, g, k] != null && m.Radius[h, g, k] - SegDist(m.P[v], m.A[h, g, k], m.B[h, g, k]) > -0.003f) { m.Fused[v] |= 1 << g; break; }
                    }
                }
            m.Rest = Depths(m, null, null);
        }

        /// 기준선(휴지 깊이·붙은 쌍·물갈퀴·어긋난 줄기·반지름·끝점)을 다른 인스턴스(같은 메시)로 옮긴다 — 바인드 자세에서 잡은 기준으로 다른 팔 자세를 잴 때
        public static void CopyRest(Map from, Map to)
        {
            to.Rest = (float[])from.Rest.Clone();
            to.Fused = (int[])from.Fused.Clone();
            to.Web = (bool[])from.Web.Clone();
            to.WebCount = from.WebCount;
            for (int h = 0; h < 2; h++)
            {
                to.BadChain[h] = from.BadChain[h];
                for (int f = 0; f < 5; f++)
                {
                    to.TipLocal[h, f] = from.TipLocal[h, f];
                    for (int j = 0; j < 3; j++) to.Radius[h, f, j] = from.Radius[h, f, j];
                }
            }
        }

        /// 지금 자세 관통(mm): 손마다·손가락마다 최대. worstOther = 그 깊이를 만든 상대(손가락 0~4 · 손바닥 5)
        public static float Measure(Map m, float[,] perFinger, int[,] worstOther)
        {
            Skin(m);
            Depths(m, perFinger, worstOther);
            float max = 0f;
            for (int h = 0; h < 2; h++) for (int f = 0; f < 5; f++) max = Mathf.Max(max, perFinger[h, f]);
            return max;
        }

        const float Reach = 0.012f;   // 손바닥: 가장 가까운 정점을 찾는 반경(m)

        static float[] Depths(Map m, float[,] perFinger, int[,] worstOther)
        {
            var P = m.P; var N = m.PN;
            var depth = new float[P.Length];
            if (perFinger != null) for (int h = 0; h < 2; h++) for (int f = 0; f < 5; f++) { perFinger[h, f] = 0f; if (worstOther != null) worstOther[h, f] = -1; }
            float r2 = Reach * Reach;
            for (int h = 0; h < 2; h++)
            {
                foreach (int v in m.Probe[h])
                {
                    if (m.Web != null && m.Web[v]) continue;
                    int fv = FingerOf(m.Cls[v]);
                    bool badMe = (m.BadChain[h] & (1 << fv)) != 0;
                    var p = P[v];
                    float dep = float.NegativeInfinity; int who = -1;
                    // ① 다른 손가락 캡슐
                    for (int g = 0; g < 5; g++)
                    {
                        if (g == fv || (m.Fused != null && (m.Fused[v] & (1 << g)) != 0)) continue;
                        if (badMe || (m.BadChain[h] & (1 << g)) != 0) continue;
                        for (int k = 0; k < 3; k++)
                        {
                            if (m.Bone[h, g, k] == null) continue;
                            float d = m.Radius[h, g, k] - SegDist(p, m.A[h, g, k], m.B[h, g, k]);
                            if (d > dep) { dep = d; who = g; }
                        }
                    }
                    // ② 손바닥 면
                    int best = -1; float bd = r2;
                    foreach (int q in m.Palm[h])
                    {
                        float dd = (P[q] - p).sqrMagnitude;
                        if (dd < bd) { bd = dd; best = q; }
                    }
                    if (best >= 0)
                    {
                        float d = -Vector3.Dot(p - P[best], N[best]);
                        if (d > dep) { dep = d; who = 5; }
                    }
                    if (float.IsNegativeInfinity(dep)) continue;
                    depth[v] = dep;
                    if (perFinger == null) continue;
                    float pen = (dep - Mathf.Max(0f, m.Rest != null ? m.Rest[v] : 0f)) * 1000f;
                    if (pen > perFinger[h, fv])
                    {
                        perFinger[h, fv] = pen;
                        if (worstOther != null) worstOther[h, fv] = who;
                    }
                }
            }
            return depth;
        }
    }
}
