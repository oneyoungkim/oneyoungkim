// 행인1의 메인이벤트 — 손가락 관통 검사(D13: 손 자세 8종에서 손가락끼리·손바닥 관통 ≤ 4mm, 정점 검사)
// 굽힌 자세로 메시를 구워(BakeMesh) 손가락 2·3마디 정점(가장 큰 가중치 ≥ 0.5)마다 같은 손의 다른 손가락·손바닥(손 뼈) 정점 중 가장 가까운 것을 찾고,
// 그 정점의 노멀 면 아래로 들어간 깊이를 잰다. 휴지 자세에서 이미 붙어 있던 깊이(Tripo 손가락은 붙어 모델링된 곳이 있음)는 빼고 센다.
// 1마디(밑 마디)는 뺀다 — 손가락을 굽히면 밑 마디 주름이 손바닥 쪽으로 눌리는 것은 관통이 아니라 피부 접힘이라서.
// FormalSetup(편집 모드 — 각도표 맞춤)과 테스트 D13(실행 — 확인)이 같은 계산을 쓴다. 메시는 Read/Write 켜져 있어야 함(정식 모델 FBX).
using System.Collections.Generic;
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
            public readonly int[][] Other = new int[2][];       // 손마다 그 손의 손가락·손바닥 정점 전부
            public float[] Rest;                                // 휴지 자세 깊이(m)
            public Mesh Baked;
        }

        public static int FingerOf(int cls) => cls < 0 ? -1 : (cls % 100) >= 20 ? -1 : (cls % 100) / 3;
        public static int SegOf(int cls) => cls < 0 ? -1 : (cls % 100) >= 20 ? -1 : (cls % 100) % 3;
        public static bool RightOf(int cls) => cls >= 100;

        public static Map Build(SkinnedMeshRenderer smr)
        {
            var mesh = smr.sharedMesh;
            var bones = smr.bones;
            var bc = new int[bones.Length];
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
                        if (rest == "Hand" + HandPoser.FingerNames[f] + (j + 1)) bc[i] = side + f * 3 + j;
            }
            var bw = mesh.boneWeights;
            var m = new Map { Smr = smr, Cls = new int[bw.Length], Baked = new Mesh() };
            var probe = new[] { new List<int>(), new List<int>() };
            var other = new[] { new List<int>(), new List<int>() };
            for (int v = 0; v < bw.Length; v++)
            {
                var w = bw[v];
                int c = w.weight0 >= 0.5f ? bc[w.boneIndex0] : -1;
                m.Cls[v] = c;
                if (c < 0) continue;
                int h = c >= 100 ? 1 : 0;
                other[h].Add(v);
                if (SegOf(c) >= 1) probe[h].Add(v);
            }
            for (int h = 0; h < 2; h++) { m.Probe[h] = probe[h].ToArray(); m.Other[h] = other[h].ToArray(); }
            return m;
        }

        /// 휴지 자세 기준선(손가락 휴지 자세에서 부를 것)
        public static void SetRest(Map m)
        {
            m.Smr.BakeMesh(m.Baked, true);
            m.Rest = Depths(m, m.Baked.vertices, m.Baked.normals, null, null);
        }

        /// 지금 자세 관통(mm): 손마다·손가락마다 최대. worstOther = 그 깊이를 만든 상대(손가락 0~4 · 손바닥 5)
        public static float Measure(Map m, float[,] perFinger, int[,] worstOther)
        {
            m.Smr.BakeMesh(m.Baked, true);
            var d = Depths(m, m.Baked.vertices, m.Baked.normals, perFinger, worstOther);
            float max = 0f;
            for (int h = 0; h < 2; h++) for (int f = 0; f < 5; f++) max = Mathf.Max(max, perFinger[h, f]);
            return max;
        }

        const float Reach = 0.012f;   // 가장 가까운 상대 정점을 찾는 반경(m)

        static float[] Depths(Map m, Vector3[] P, Vector3[] N, float[,] perFinger, int[,] worstOther)
        {
            var depth = new float[P.Length];
            if (perFinger != null) for (int h = 0; h < 2; h++) for (int f = 0; f < 5; f++) { perFinger[h, f] = 0f; if (worstOther != null) worstOther[h, f] = -1; }
            float r2 = Reach * Reach;
            for (int h = 0; h < 2; h++)
            {
                var others = m.Other[h];
                foreach (int v in m.Probe[h])
                {
                    int fv = FingerOf(m.Cls[v]);
                    var p = P[v];
                    int best = -1; float bd = r2;
                    foreach (int q in others)
                    {
                        if (FingerOf(m.Cls[q]) == fv) continue;
                        float dd = (P[q] - p).sqrMagnitude;
                        if (dd < bd) { bd = dd; best = q; }
                    }
                    if (best < 0) continue;
                    float dep = -Vector3.Dot(p - P[best], N[best]);
                    depth[v] = dep;
                    if (perFinger == null) continue;
                    float pen = (dep - Mathf.Max(0f, m.Rest != null ? m.Rest[v] : 0f)) * 1000f;
                    if (pen > perFinger[h, fv])
                    {
                        perFinger[h, fv] = pen;
                        if (worstOther != null) { int fo = FingerOf(m.Cls[best]); worstOther[h, fv] = fo < 0 ? 5 : fo; }
                    }
                }
            }
            return depth;
        }
    }
}
