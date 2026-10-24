// 행인1의 메인이벤트 — 간판 점검(맵 아트 패스 6차 2026-10-25): 글자 빠짐 · 간판끼리 겹침 · 건물 부재(발코니·실외기·창틀)에 가림.
// 편집기 빌드(EnvSetup — 보고)와 플레이 모드 시험(SignTextTests — 합격 기준)이 같은 규칙을 쓴다. 빌드에선 GlyphCheck 가 글자만 한 번 세어 로그에 남긴다.
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace Haengin
{
    public static class SignAudit
    {
        /// 간판 면 하나(가운데 C, 바깥 법선 N, 오른쪽 R, 위 U, 폭 W, 높이 H) — Owner 가 같은 면끼리는 서로 겹침으로 안 본다(돌출 간판 양면 등)
        public struct Face { public string Name, Owner; public Vector3 C, N, R, U; public float W, H; public bool Sheet; }

        static readonly HashSet<string> FaceNames = new HashSet<string> { "면", "면 A", "면 B" };

        /// 장면의 TMP 글자 전수: 글꼴(대체 글꼴 포함)에 없는 글자 수. detail 에 처음 몇 개
        public static int MissingGlyphs(out int texts, out string detail)
        {
            int miss = 0; texts = 0; var bad = new List<string>();
            foreach (var t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID))   // 꺼 둔 컴포넌트도 찾음
            {
                if (t.font == null || string.IsNullOrEmpty(t.text)) continue;
                texts++;
                // 6차: 구워 둔 글자(컴포넌트 꺼짐)는 원문에서 태그만 빼고 센다
                bool live = t.isActiveAndEnabled;
                if (live) t.ForceMeshUpdate(true, true);
                string s = live ? t.GetParsedText() : System.Text.RegularExpressions.Regex.Replace(t.text, "<[^>]+>", "");
                foreach (char c in s)
                {
                    if (char.IsWhiteSpace(c) || char.IsControl(c) || c == '​') continue;
                    if (t.font.HasCharacter(c, true, true)) continue;
                    miss++;
                    if (bad.Count < 12) bad.Add($"'{c}'(U+{(int)c:X4}) in \"{s.Replace('\n', ' ')}\" @{Path(t.transform)}");
                }
                // 글꼴이 '없음 글자(□)'로 바꿔 그린 칸도 센다
                var ti = t.textInfo;
                for (int i = 0; live && ti != null && i < ti.characterCount; i++)
                {
                    var ci = ti.characterInfo[i];
                    if (ci.elementType != TMP_TextElementType.Character || char.IsWhiteSpace(ci.character)) continue;
                    if (ci.textElement == null || ci.textElement.glyph == null || ci.textElement.glyphIndex == 0 || ci.character == '□')
                    { miss++; if (bad.Count < 12) bad.Add($"없음 칸 '{ci.character}' @{Path(t.transform)}"); }
                }
            }
            detail = string.Join(" · ", bad);
            return miss;
        }

        public static string Path(Transform t) { var l = new List<string>(); while (t != null && l.Count < 4) { l.Insert(0, t.name); t = t.parent; } return string.Join("/", l); }

        /// 간판 면('면'·'면 A'·'면 B' 이름의 판) + 창 시트지(이름 '창 시트지 …' 묶음의 글자 범위)
        public static List<Face> Faces(Transform root)
        {
            var list = new List<Face>();
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(false))
            {
                if (!FaceNames.Contains(mf.name) || mf.sharedMesh == null) continue;
                var r = mf.GetComponent<MeshRenderer>(); if (r == null || !r.enabled) continue;
                var b = mf.sharedMesh.bounds; var tr = mf.transform;
                list.Add(new Face { Name = Path(tr), Owner = tr.parent != null ? tr.parent.GetInstanceID().ToString() : tr.name, C = tr.TransformPoint(b.center), N = -tr.forward, R = tr.right, U = tr.up, W = b.size.x * tr.lossyScale.x, H = b.size.y * tr.lossyScale.y });
            }
            foreach (var g in root.GetComponentsInChildren<Transform>(false).Where(x => x.name.StartsWith("창 시트지")))
            {
                var tmps = g.GetComponentsInChildren<TMP_Text>(false); if (tmps.Length == 0) continue;
                var fr = tmps[0].transform; Vector3 n = -fr.forward, rr = fr.right, uu = Vector3.up;
                float r0 = 1e9f, r1 = -1e9f, u0 = 1e9f, u1 = -1e9f; Vector3 o = tmps[0].transform.position;
                foreach (var t in tmps)
                {
                    var tb = t.rectTransform.rect; if (tb.width * tb.height < 1e-6f) continue;   // 글자 상자(자동 크기라 글자는 이 안) — 구운 글자도 그대로 읽힘
                    foreach (var corner in new[] { new Vector3(tb.xMin, tb.yMin, 0), new Vector3(tb.xMax, tb.yMax, 0), new Vector3(tb.xMin, tb.yMax, 0), new Vector3(tb.xMax, tb.yMin, 0) })
                    {
                        var w = t.transform.TransformPoint(corner) - o;
                        float a = Vector3.Dot(w, rr), b = Vector3.Dot(w, uu);
                        r0 = Mathf.Min(r0, a); r1 = Mathf.Max(r1, a); u0 = Mathf.Min(u0, b); u1 = Mathf.Max(u1, b);
                    }
                }
                if (r0 > r1) continue;
                list.Add(new Face { Name = Path(g), Owner = g.GetInstanceID().ToString(), C = o + rr * ((r0 + r1) / 2) + uu * ((u0 + u1) / 2), N = n, R = rr, U = uu, W = r1 - r0, H = u1 - u0, Sheet = true });
            }
            return list;
        }

        /// 겹침: B 의 네 모서리를 A 판 좌표로 — A 앞 1.2m 안(또는 같은 면)에 있고 A 면을 덮는 넓이가 0.015㎡·A 의 4% 를 넘으면 겹침
        public static List<string> Overlaps(List<Face> faces)
        {
            var bad = new List<string>();
            for (int i = 0; i < faces.Count; i++)
                for (int j = 0; j < faces.Count; j++)
                {
                    if (i == j) continue;
                    var A = faces[i]; var B = faces[j];
                    if (A.Owner == B.Owner) continue;
                    if ((A.C - B.C).sqrMagnitude > 64f) continue;
                    float dmin = 1e9f, dmax = -1e9f, r0 = 1e9f, r1 = -1e9f, u0 = 1e9f, u1 = -1e9f;
                    foreach (var (sx, sy) in new[] { (-1f, -1f), (1f, -1f), (1f, 1f), (-1f, 1f) })
                    {
                        var p = B.C + B.R * (sx * B.W / 2) + B.U * (sy * B.H / 2) - A.C;
                        float d = Vector3.Dot(p, A.N); dmin = Mathf.Min(dmin, d); dmax = Mathf.Max(dmax, d);
                        float a = Vector3.Dot(p, A.R), b = Vector3.Dot(p, A.U);
                        r0 = Mathf.Min(r0, a); r1 = Mathf.Max(r1, a); u0 = Mathf.Min(u0, b); u1 = Mathf.Max(u1, b);
                    }
                    if (dmax < -0.03f || dmin > 1.2f) continue;
                    float ow = Mathf.Min(r1, A.W / 2) - Mathf.Max(r0, -A.W / 2), oh = Mathf.Min(u1, A.H / 2) - Mathf.Max(u0, -A.H / 2);
                    if (ow <= 0f || oh <= 0f) continue;
                    float area = ow * oh;
                    if (area > Mathf.Max(0.015f, 0.04f * A.W * A.H)) bad.Add($"{A.Name} ← {B.Name} 겹침 {area:F2}㎡");
                }
            return bad;
        }

        /// 가림: 키트 건물·소품 LOD0 메시에 잠시 콜라이더(층 30)를 달고, 면 앞 1.6m 에서 면의 8×4 점으로 쏜 빛이 면보다 먼저 맞는 비율(발코니·실외기·창틀처럼 면 바로 앞 부재)
        public static List<string> Occlusions(Transform zone, List<Face> faces, float maxFrac, out int checkedFaces)
        {
            var bad = new List<string>(); checkedFaces = 0;
            const int L = 30;
            var temp = new List<(GameObject go, int layer, MeshCollider mc)>();
            foreach (var mf in zone.GetComponentsInChildren<MeshFilter>(false))
            {
                if (mf.sharedMesh == null || !mf.name.Contains("_LOD0")) continue;
                if (mf.name.StartsWith("Sign_") || mf.name.StartsWith("WinLit_")) continue;
                var r = mf.GetComponent<MeshRenderer>(); if (r == null || !r.enabled) continue;
                if (r.sharedMaterial != null && r.sharedMaterial.shader != null && r.sharedMaterial.shader.name.Contains("Leaf")) continue;   // 나무 잎 카드(알파)는 가림으로 안 셈
                if (mf.GetComponent<Collider>() != null) continue;
                var mc = mf.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh;
                temp.Add((mf.gameObject, mf.gameObject.layer, mc)); mf.gameObject.layer = L;
            }
            Physics.SyncTransforms();
            try
            {
                foreach (var f in faces)
                {
                    if (f.Sheet || f.Name.Contains("입간판")) continue;   // 창 시트지·입간판(땅에 세운 판 — 뒷면은 원래 가게를 봄)은 빼고
                    checkedFaces++;
                    int hit = 0, all = 0; var who = new Dictionary<string, int>();
                    for (int a = 0; a < 8; a++)
                        for (int b = 0; b < 4; b++)
                        {
                            float x = (a + 0.5f) / 8f - 0.5f, y = (b + 0.5f) / 4f - 0.5f;
                            var p = f.C + f.R * (x * f.W * 0.88f) + f.U * (y * f.H * 0.88f);
                            all++;
                            if (Physics.Raycast(p + f.N * 1.6f, -f.N, out var h, 1.6f - 0.04f, 1 << L, QueryTriggerInteraction.Ignore)) { hit++; string nm = h.collider.name; who[nm] = who.TryGetValue(nm, out var c0) ? c0 + 1 : 1; }
                        }
                    float frac = hit / (float)all;
                    if (frac > maxFrac) bad.Add($"{f.Name} 가림 {frac:P0} ← {string.Join(", ", who.OrderByDescending(x => x.Value).Take(3).Select(x => x.Key))}");
                }
            }
            finally
            {
                foreach (var (go, layer, mc) in temp) { if (Application.isPlaying) Object.Destroy(mc); else Object.DestroyImmediate(mc); go.layer = layer; }
                Physics.SyncTransforms();
            }
            return bad;
        }
    }
}
