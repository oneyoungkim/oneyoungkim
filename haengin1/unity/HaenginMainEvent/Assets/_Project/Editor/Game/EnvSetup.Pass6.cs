// 행인1의 메인이벤트 — 맵 아트 패스 6차(2026-10-25): 간판 결함 → 남은 차이 → 그리기 호출.
//   ① 간판: 짓기 전 배치 점검(PlanSigns — 같은 면 겹친 띠 올림 + 창과 겹치는 띠는 창 사이로 줄이거나 뺌) · 창 시트지는 창살(문설주) 사이 칸마다 글자(Sheets6 —
//      간판 몸 앞이면 안 붙임, 방범창 창은 키트에서 뺌) · 정류장 광고판(가상 광고 2장, BusAds6) · 점검(SignAudit: 글자 빠짐·겹침·가림 → 메모, 시험이 합격 기준).
//   ② 맨땅·비탈 초록(Green6): 지형 위 풀색 곱하기 덮개(맨땅 → 풀밭, 길 가장자리는 옅게) + 잡초 띠(길·담 가장자리) + 비탈 관목 군락 + 흰 꽃 관목 + 축대·담 틈 풀.
//   ③ 밤 빛 웅덩이(Pools6): 가게 앞·가로등 아래 바닥에 따뜻한 빛을 칠함(더하기, 젖은 반사 없음 — Haengin/LightPool).
//   ④ 계단 손잡이(Rails6) · ⑤ 그리기 호출 줄이기(Merge6): 정적 소품·간판 몸·데칼·천을 재질 × 32m 칸마다 한 메시로(데칼은 아틀라스 칸을 정점에 구워 재질 하나로).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Haengin.EditorTools;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Haengin.EditorGame
{
    public static partial class EnvSetup
    {
        public const string Pass6Dir = EnvDir + "/Pass6", Pass6MatDir = MatDir + "/Pass6", Mesh6Path = Pass6Dir + "/Meshes6.asset";
        const int TempLayer = 30;

        // ───────────────────────── 메시 보관(한 에셋 아래 이름별 — 다시 지을 때 제자리 갱신, 안 쓴 것은 지움)
        static Mesh mesh6Root;
        static Dictionary<string, Mesh> mesh6;
        static HashSet<string> mesh6Used;
        static void Mesh6Reset() { mesh6Root = null; mesh6 = null; mesh6Used = null; }
        static Mesh Mesh6(string name)
        {
            if (mesh6Root == null)
            {
                EnsureFolder(Pass6Dir);
                mesh6Root = AssetDatabase.LoadAssetAtPath<Mesh>(Mesh6Path);
                if (mesh6Root == null) { mesh6Root = new Mesh { name = "Meshes6" }; AssetDatabase.CreateAsset(mesh6Root, Mesh6Path); }
                mesh6 = AssetDatabase.LoadAllAssetRepresentationsAtPath(Mesh6Path).OfType<Mesh>().GroupBy(m => m.name).ToDictionary(g => g.Key, g => g.First());
                mesh6Used = new HashSet<string>();
            }
            string nm = name; int k = 1;
            while (mesh6Used.Contains(nm)) nm = name + "#" + (++k);
            if (!mesh6.TryGetValue(nm, out var mesh) || mesh == null) { mesh = new Mesh { name = nm }; AssetDatabase.AddObjectToAsset(mesh, mesh6Root); mesh6[nm] = mesh; }
            mesh6Used.Add(nm);
            mesh.Clear();
            return mesh;
        }
        static void Mesh6Keep(string name)
        {
            if (mesh6Root == null) { var tmp = Mesh6("__init__"); mesh6Used.Remove("__init__"); }
            mesh6Used.Add(name);
        }
        static int Mesh6Prune()
        {
            if (mesh6Root == null) return 0;
            int n = 0;
            foreach (var kv in mesh6.ToList())
                if (!mesh6Used.Contains(kv.Key) && kv.Value != null) { AssetDatabase.RemoveObjectFromAsset(kv.Value); Object.DestroyImmediate(kv.Value, true); mesh6.Remove(kv.Key); n++; }
            EditorUtility.SetDirty(mesh6Root);
            return n;
        }

        sealed class MB6
        {
            public readonly List<Vector3> V = new List<Vector3>(), N = new List<Vector3>();
            public readonly List<Vector2> UV = new List<Vector2>(), UV1 = new List<Vector2>();
            public readonly List<Color> C = new List<Color>();
            public readonly List<int> T = new List<int>();
            public int Add(Vector3 p, Vector3 n, Vector2 uv, Vector2 uv1, Color c) { V.Add(p); N.Add(n); UV.Add(uv); UV1.Add(uv1); C.Add(c); return V.Count - 1; }
            public void Tri(int a, int b, int c) { T.Add(a); T.Add(b); T.Add(c); }
            public Mesh Save(string name)
            {
                var m = Mesh6(name);
                if (V.Count > 65000) m.indexFormat = IndexFormat.UInt32;
                m.SetVertices(V); m.SetNormals(N); m.SetUVs(0, UV); m.SetUVs(1, UV1); m.SetColors(C); m.SetTriangles(T, 0); m.RecalculateBounds();
                if (T.Count > 0) m.RecalculateTangents();   // UTS 는 탄젠트가 없으면 면 밝기가 깨짐(합친 상자가 하얗게 — 6차 실측)
                EditorUtility.SetDirty(m);
                return m;
            }
        }
        static GameObject Obj6(string name, Transform parent, Mesh mesh, Material mat, ShadowCastingMode shadows, bool receive = true)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); go.transform.localScale = Vector3.one;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = shadows; r.receiveShadows = receive;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.OccludeeStatic);
            return go;
        }
        static (int, int) Cell6(Vector3 p, float size) => (Mathf.FloorToInt(p.x / size), Mathf.FloorToInt(p.z / size));

        // ═════════════════════════ ① 간판 배치 점검(짓기 전)
        struct WinR { public Vector3 C, N; public float W, H; }
        static List<WinR> WinRects(Transform env)
        {
            var list = new List<WinR>();
            foreach (var mf in env.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf.name.StartsWith("WinLit_") || mf.sharedMesh == null) continue;
                var vs = mf.sharedMesh.vertices; var ts = mf.sharedMesh.triangles; var M = mf.transform.localToWorldMatrix;
                for (int i = 0; i + 5 < ts.Length; i += 6)
                {
                    var ps = new[] { ts[i], ts[i + 1], ts[i + 2], ts[i + 3], ts[i + 4], ts[i + 5] }.Distinct().Select(k => M.MultiplyPoint3x4(vs[k])).ToList();
                    if (ps.Count < 3) continue;
                    var n = Vector3.Cross(ps[1] - ps[0], ps[2] - ps[0]); n.y = 0f; if (n.sqrMagnitude < 1e-8f) continue; n.Normalize();
                    var r = Vector3.Cross(Vector3.up, n).normalized;
                    var c = ps.Aggregate(Vector3.zero, (a, b) => a + b) / ps.Count;
                    float w = ps.Max(p => Vector3.Dot(p - c, r)) - ps.Min(p => Vector3.Dot(p - c, r)), h = ps.Max(p => p.y) - ps.Min(p => p.y);
                    if (w < 0.2f || h < 0.2f) continue;
                    list.Add(new WinR { C = c, N = n, W = w, H = h });
                }
            }
            return list;
        }

        static string PlanSigns(Transform env, List<(Transform t, Dictionary<string, object> inf)> signs)
        {
            var wins = WinRects(env);
            var bands = signs.Where(s => (S(s.inf, "sign_kind") ?? "band") == "band" && L(G(s.inf, "size_m")) != null && L(G(s.inf, "size_m")).Count >= 2).ToList();
            Vector3 Cn(Dictionary<string, object> inf) => V3(G(inf, "center"));
            Vector3 Nn(Dictionary<string, object> inf) => V3(G(inf, "normal")).normalized;
            float Wd(Dictionary<string, object> inf) => F(L(G(inf, "size_m"))[0]);
            float Ht(Dictionary<string, object> inf) => F(L(G(inf, "size_m"))[1]);
            void SetC(Dictionary<string, object> inf, Vector3 c) => inf["center"] = new List<object> { (double)c.x, (double)c.y, (double)c.z };
            void SetH(Dictionary<string, object> inf, float h) => inf["size_m"] = new List<object> { (double)Wd(inf), (double)h };
            int lifted = 0, fitted = 0, hidden = 0;
            // 1) 같은 블록·같은 면에서 겹치는 띠: 작은 띠를 큰 띠 위로(5차 LiftOverlappingSigns 를 짓기 전으로)
            for (int i = 0; i < bands.Count; i++)
                for (int j = i + 1; j < bands.Count; j++)
                {
                    var a = bands[i].inf; var b = bands[j].inf;
                    if (S(a, "block") != S(b, "block") || Mathf.Abs(Vector3.Dot(Nn(a), Nn(b))) < 0.9f) continue;
                    var tg = Vector3.Cross(Vector3.up, Nn(a)).normalized;
                    if (Mathf.Abs(Vector3.Dot(Cn(b) - Cn(a), tg)) > (Wd(a) + Wd(b)) * 0.5f - 0.3f) continue;
                    float vo = Mathf.Min(Cn(a).y + Ht(a) / 2, Cn(b).y + Ht(b) / 2) - Mathf.Max(Cn(a).y - Ht(a) / 2, Cn(b).y - Ht(b) / 2);
                    if (vo <= 0.02f) continue;
                    bool aUp = Ht(a) < Ht(b);
                    var up = aUp ? a : b; var stay = aUp ? b : a;
                    float lift = (Cn(stay).y + Ht(stay) / 2 + 0.12f + Ht(up) / 2) - Cn(up).y;
                    SetC(up, Cn(up) + Vector3.up * lift); lifted++;
                    Notes.Add($"간판 올림(6차·짓기 전): {(aUp ? bands[i].t.name : bands[j].t.name)} +{lift:F2}m");
                }
            // 2) 창과 겹치는 띠: 아래(창 윗변·다른 띠)와 위(창 아랫변·다른 띠) 사이로 줄이고, 0.30m 도 안 남으면 뺀다
            foreach (var (t, inf) in bands)
            {
                var c = Cn(inf); var n = Nn(inf); float w = Wd(inf), h = Ht(inf);
                var tg = Vector3.Cross(Vector3.up, n).normalized;
                bool Near(Vector3 oc, Vector3 on, float ow) => Mathf.Abs(Vector3.Dot(on, n)) > 0.9f && Mathf.Abs(Vector3.Dot(oc - c, n)) < 0.7f && Mathf.Abs(Vector3.Dot(oc - c, tg)) < (w + ow) / 2 - 0.05f;
                var ws = wins.Where(x => Near(x.C, x.N, x.W)).ToList();
                if (!ws.Any(x => Mathf.Min(c.y + h / 2, x.C.y + x.H / 2) - Mathf.Max(c.y - h / 2, x.C.y - x.H / 2) > 0.03f)) continue;
                float lo = c.y - h / 2, hi = c.y + h / 2; bool hasLo = false, hasHi = false;
                foreach (var x in ws)
                {
                    if (x.C.y < c.y) { lo = hasLo ? Mathf.Max(lo, x.C.y + x.H / 2 + 0.06f) : x.C.y + x.H / 2 + 0.06f; hasLo = true; }
                    else { hi = hasHi ? Mathf.Min(hi, x.C.y - x.H / 2 - 0.06f) : x.C.y - x.H / 2 - 0.06f; hasHi = true; }
                }
                foreach (var (_, o) in bands)
                {
                    if (o == inf || o.ContainsKey("hidden") || !Near(Cn(o), Nn(o), Wd(o))) continue;
                    var oc = Cn(o);
                    if (oc.y < c.y) { float v = oc.y + Ht(o) / 2 + 0.08f; lo = hasLo ? Mathf.Max(lo, v) : Mathf.Max(lo, v); hasLo = true; }
                    else { float v = oc.y - Ht(o) / 2 - 0.08f; hi = hasHi ? Mathf.Min(hi, v) : Mathf.Min(hi, v); hasHi = true; }
                }
                float room = hi - lo;
                if (room < 0.30f) { inf["hidden"] = true; hidden++; Notes.Add($"간판 뺌(6차): {t.name} — 창과 겹치고 남는 자리 {room:F2}m"); continue; }
                float nh = Mathf.Min(h, room), cy = Mathf.Clamp(c.y, lo + nh / 2, hi - nh / 2);
                SetH(inf, nh); SetC(inf, new Vector3(c.x, cy, c.z)); fitted++;
                Notes.Add($"간판 맞춤(6차): {t.name} 높이 {h:F2}→{nh:F2} · 가운데 {c.y:F2}→{cy:F2} (겹친 창 {ws.Count})");
            }
            return $"간판 배치 점검(6차): 같은 면 올림 {lifted} · 창 피해 줄임 {fitted} · 뺌 {hidden} · 창 {wins.Count}";
        }

        // ───────────────────────── 창 시트지: 창살 사이 칸마다(문설주 뒤로 글자가 숨지 않게), 간판 몸 앞이면 안 붙임
        static bool SignPart(Transform t)
        {
            for (var x = t; x != null; x = x.parent)
            {
                string n = x.name;
                if (n.StartsWith("Sign_") || n.StartsWith("모서리 간판") || n.StartsWith("혜화 MMA")) return true;
                if (n == "Env") return false;
            }
            return false;
        }

        static string[] SplitPanes(string s, int k, bool dots)
        {
            var res = new string[k];
            if (k <= 1) { res[0] = s; return res; }
            var phrases = s.Split('·').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
            List<string> tokens; string sep;
            if (dots || phrases.Count >= k) { tokens = phrases; sep = " · "; }
            else { tokens = s.Replace("·", " ").Split(' ').Select(x => x.Trim()).Where(x => x.Length > 0).ToList(); sep = " "; }
            if (tokens.Count >= k)
            {
                int idx = 0;
                for (int i = 0; i < k; i++)
                {
                    int left = tokens.Skip(idx).Sum(x => x.Length); float target = left / (float)(k - i);
                    var part = new List<string>(); int len = 0;
                    while (idx < tokens.Count && (part.Count == 0 || (tokens.Count - idx > k - i - 1 && len + tokens[idx].Length * 0.5f <= target)))
                    { part.Add(tokens[idx]); len += tokens[idx].Length; idx++; }
                    res[i] = string.Join(sep, part);
                }
                if (idx < tokens.Count) res[k - 1] = string.Join(sep, new[] { res[k - 1] }.Concat(tokens.Skip(idx)));
                return res;
            }
            // 칸보다 낱말이 적으면: 낱말 하나씩 칸에(남는 칸은 맨 유리) — 가운데로
            int off = (k - tokens.Count) / 2;
            for (int i = 0; i < tokens.Count; i++) res[off + i] = tokens[i];
            return res;
        }

        static (int made, int skipped) Sheets6(Transform root, Transform env, Dictionary<string, Dictionary<string, object>> objs,
            Dictionary<(string, int), (string a, string b, Color band, Color text, bool hand)> table)
        {
            var temp = new List<(GameObject go, int layer, BoxCollider bc)>();
            foreach (var r in env.GetComponentsInChildren<MeshRenderer>(false))
            {
                if (!SignPart(r.transform)) continue;
                var m0 = r.sharedMaterial; if (m0 != null && m0.shader != null && m0.shader.name == "Haengin/SignGlow") continue;
                var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null || r.GetComponent<Collider>() != null) continue;
                var b = mf.sharedMesh.bounds;
                var bc = r.gameObject.AddComponent<BoxCollider>(); bc.center = b.center; bc.size = Vector3.Max(b.size, Vector3.one * 0.02f);
                temp.Add((r.gameObject, r.gameObject.layer, bc)); r.gameObject.layer = TempLayer;
            }
            Physics.SyncTransforms();
            int made = 0, skipped = 0;
            try
            {
                foreach (var (t, inf) in socks5.Where(s => S(s.inf, "sock_kind") == "win_sheet"))
                {
                    var p = V3(G(inf, "pos")); var n = V3(G(inf, "normal")).normalized; var sz = L(G(inf, "size"));
                    float w = sz != null ? F(sz[0]) : 1.2f, h = sz != null ? F(sz[1]) : 1.3f;
                    var rot = Quaternion.LookRotation(-n, Vector3.up); var R = rot * Vector3.right;
                    if (Physics.CheckBox(p + n * 0.48f, new Vector3(w / 2 + 0.06f, h / 2 + 0.06f, 0.46f), rot, 1 << TempLayer, QueryTriggerInteraction.Collide)) { skipped++; continue; }
                    int floor = int.TryParse((S(inf, "tenant") ?? "2F").TrimEnd('F'), out var fl) ? fl : 2;
                    string blk = BldgAt(objs, p);
                    var key = table.Keys.FirstOrDefault(k => blk.Contains(k.Item1) && k.Item2 == floor);
                    (string a, string b, Color band, Color text, bool hand) st = key.Item1 != null ? table[key] : ("하숙 · 원룸 있음", "문의 02-012-1180", new Color(0.95f, 0.95f, 0.92f), new Color(0.15f, 0.20f, 0.35f), true);
                    int panes = Mathf.Max(1, (int)F(G(inf, "mull"), 1f) + 1);
                    float W = w + 0.06f, pw = W / panes, iw = pw - 0.13f;
                    if (iw < 0.32f) { panes = 1; pw = W; iw = w - 0.1f; }
                    var titles = SplitPanes(st.a, panes, false);
                    var g = new GameObject("창 시트지 " + st.a).transform; g.SetParent(root, false); g.SetPositionAndRotation(p, rot);
                    float bandH = h * 0.24f;
                    var bandM = Flat("M_Vinyl_" + Mathf.RoundToInt(st.band.r * 9) + Mathf.RoundToInt(st.band.g * 9) + Mathf.RoundToInt(st.band.b * 9), st.band);
                    var font = st.hand ? FontHand : FontBold;
                    for (int i = 0; i < panes; i++)
                    {
                        var pc = p + R * (-W / 2 + pw * (i + 0.5f));
                        var bc0 = pc + Vector3.up * (h / 2 - bandH / 2 - 0.04f);
                        if (!string.IsNullOrEmpty(titles[i]))
                        {
                            Box("비닐 띠", g, bc0 + n * 0.004f, new Vector3(iw, bandH, 0.006f), rot, bandM);
                            Text3D(g, bc0 + n * 0.009f, n, titles[i], iw * 0.9f, bandH * 0.8f, st.text, font);
                        }
                        // 아랫줄(업종·전화)은 마지막 칸 하나에만(TMP 그리기 호출 줄이기 — 칸마다 나누면 창 하나에 글자 6개)
                        if (i == panes - 1)
                            Text3D(g, pc - Vector3.up * (h * 0.18f) + n * 0.006f, n, st.b, iw * 0.88f, h * 0.15f, Color.Lerp(st.band, Color.white, 0.55f), font, "글자 아래");
                    }
                    made++;
                }
            }
            finally
            {
                foreach (var (go, layer, bc) in temp) { Object.DestroyImmediate(bc); go.layer = layer; }
                Physics.SyncTransforms();
            }
            return (made, skipped);
        }

        // ───────────────────────── 정류장 광고판: 키트 밖 GLB 정류장의 세로 광고 상자(양면) — 가상 광고 2장(글자 눈으로 확인) + 아래 노선 안내
        static int BusAds6(Transform env)
        {
            if (!Faces5().ContainsKey("ad_concert") || !Faces5().ContainsKey("ad_academy")) { Notes.Add("정류장 광고 그림 없음 — 건너뜀"); return 0; }
            var root = new GameObject("BusAds6").transform; root.SetParent(env, false);
            int made = 0;
            var shelters = env.GetComponentsInChildren<MeshFilter>(true).Where(m => m.sharedMesh != null && m.sharedMesh.name.StartsWith("bus_stop_shelter_LOD0")).ToList();
            foreach (var mf in shelters)
            {
                var vs = mf.sharedMesh.vertices;
                var sel = vs.Where(v => Mathf.Abs(v.x) > 1.28f && Mathf.Abs(v.x) < 1.62f && v.y > 1.05f && v.y < 2.35f && Mathf.Abs(v.z) > 0.28f && Mathf.Abs(v.z) < 0.95f).ToList();
                if (sel.Count < 8) { Notes.Add($"정류장 광고 상자 못 찾음: {mf.name}"); continue; }
                float sx = Mathf.Sign(sel.Sum(v => v.x)), sz = Mathf.Sign(sel.Sum(v => v.z));
                sel = sel.Where(v => Mathf.Sign(v.x) == sx && Mathf.Sign(v.z) == sz).ToList();
                var b = new Bounds(sel[0], Vector3.zero); foreach (var v in sel) b.Encapsulate(v);
                var tr = mf.transform;
                float wz = tr.TransformVector(new Vector3(0, 0, b.size.z)).magnitude, hy = tr.TransformVector(new Vector3(0, b.size.y, 0)).magnitude;
                float innerW = wz - 0.07f, innerH = hy - 0.07f;
                if (innerW < 0.2f || innerH < 0.4f) { Notes.Add($"정류장 광고 상자 작음: {wz:F2}×{hy:F2}"); continue; }
                foreach (var (side, face) in new[] { (1f, "ad_concert"), (-1f, "ad_academy") })
                {
                    var f5 = Faces5()[face];
                    var n = tr.TransformDirection(new Vector3(side, 0f, 0f)).normalized;
                    float lx = side > 0 ? b.max.x + 0.004f : b.min.x - 0.004f;
                    var top = tr.TransformPoint(new Vector3(lx, b.max.y, b.center.z)) - Vector3.up * 0.035f;
                    float pw = innerW, ph = Mathf.Min(innerH * 0.72f, pw / f5.Aspect); pw = ph * f5.Aspect;
                    var g = new GameObject("정류장 광고 " + face).transform; g.SetParent(root, false);
                    var rot = Quaternion.LookRotation(-n, Vector3.up);
                    var mat = SignMat(face, 0.9f, 0.15f, Hash(face) % 11);
                    MeshObj("면", g, FaceMesh($"busad_{made}_{face}", pw, ph, f5.Aspect), mat, top - Vector3.up * (ph / 2), rot);
                    // 아래 노선 안내(남는 칸): 남색 판 + 흰 글자
                    float stripTop = top.y - ph - 0.03f, stripBot = tr.TransformPoint(new Vector3(0, b.min.y, 0)).y + 0.04f, sh = stripTop - stripBot;
                    if (sh > 0.12f)
                    {
                        var sc = new Vector3(top.x, (stripTop + stripBot) / 2, top.z);
                        Box("노선 판", g, sc - n * 0.002f, new Vector3(innerW, sh, 0.004f), rot, Flat("M_BusInfo_Navy", new Color(0.12f, 0.20f, 0.38f)));
                        Text3D(g, sc + n * 0.004f + Vector3.up * (sh * 0.22f), n, "마을버스 노선 안내", innerW * 0.86f, sh * 0.26f, new Color(0.98f, 0.85f, 0.35f), FontBold);
                        Text3D(g, sc + n * 0.004f - Vector3.up * (sh * 0.16f), n, "성대 후문 · 명륜3가 · 와룡공원", innerW * 0.9f, sh * 0.22f, Color.white, FontBold, "글자 아래");
                    }
                    made++;
                }
                // 뒤판 유리 두 칸(정류장에서 가장 큰 흰 판) — 칸마다 양면 광고 포스터(공연·학원)
                var back = vs.Where(v => Mathf.Abs(v.x) < 1.3f && v.y > 0.7f && v.y < 2.1f && Mathf.Abs(v.z) > 0.12f).ToList();
                if (back.Count > 8)
                {
                    float zc = back.GroupBy(v => Mathf.Round(v.z * 25f)).OrderByDescending(g0 => g0.Count()).First().Key / 25f;
                    var plane = back.Where(v => Mathf.Abs(v.z - zc) < 0.06f).ToList();
                    float zmin = plane.Min(v => v.z), zmax = plane.Max(v => v.z), xmin = plane.Min(v => v.x), xmax = plane.Max(v => v.x), ymin = plane.Min(v => v.y), ymax = plane.Max(v => v.y);
                    float hW = tr.TransformVector(new Vector3(0f, ymax - ymin, 0f)).magnitude;
                    var g2 = new GameObject("정류장 뒤판 광고").transform; g2.SetParent(root, false);
                    foreach (var (k, face) in new[] { (0, "ad_concert"), (1, "ad_academy") })
                    {
                        var f5 = Faces5()[face]; var mat = SignMat(face, 0.9f, 0.15f, Hash(face) % 11);
                        float ph = Mathf.Min(hW - 0.16f, 1.2f), pw = ph * f5.Aspect;
                        float cxl = Mathf.Lerp(xmin, xmax, k == 0 ? 0.25f : 0.75f), cyl = (ymin + ymax) / 2f;
                        foreach (float side in new[] { 1f, -1f })
                        {
                            var nW = tr.TransformDirection(new Vector3(0f, 0f, side)).normalized;
                            var pos = tr.TransformPoint(new Vector3(cxl, cyl, side > 0 ? zmax + 0.006f : zmin - 0.006f));
                            MeshObj("광고 포스터", g2, FaceMesh($"busad_back_{made}_{k}_{(side > 0 ? "f" : "b")}", pw, ph, f5.Aspect), mat, pos, Quaternion.LookRotation(-nW, Vector3.up));
                        }
                    }
                }
            }
            return made;
        }

        // ═════════════════════════ ② 맨땅·비탈 초록
        static Texture2D GrassTex6()
        {
            string path = TexDir + "/Grass6.png";
            const int S = 512;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            float Tile(float x, float y, int per, int seed)
            {
                // 감기는(tileable) 값 잡음: 격자 per 칸, 칸 좌표를 per 로 나머지
                float fx = x * per, fy = y * per; int ix = Mathf.FloorToInt(fx), iy = Mathf.FloorToInt(fy); float tx = fx - ix, ty = fy - iy;
                float H(int a, int b) { a = ((a % per) + per) % per; b = ((b % per) + per) % per; unchecked { int h = a * 374761393 + b * 668265263 + seed * 2147483; h = (h ^ (h >> 13)) * 1274126177; return (h & 0xFFFF) / 65535f; } }
                tx = tx * tx * (3 - 2 * tx); ty = ty * ty * (3 - 2 * ty);
                return Mathf.Lerp(Mathf.Lerp(H(ix, iy), H(ix + 1, iy), tx), Mathf.Lerp(H(ix, iy + 1), H(ix + 1, iy + 1), tx), ty);
            }
            var mid = new Color(0.52f, 0.67f, 0.42f); var lite = new Color(0.65f, 0.79f, 0.52f); var dark = new Color(0.40f, 0.54f, 0.35f); var soil = new Color(0.92f, 0.90f, 0.84f);   // 6차 2회: 라임빛 줄임
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = x / (float)S, v = y / (float)S;
                    float big = Tile(u, v, 4, 1) * 0.6f + Tile(u, v, 8, 2) * 0.4f, small = Tile(u, v, 32, 3) * 0.6f + Tile(u, v, 64, 4) * 0.4f;
                    float k = big * 0.75f + small * 0.25f;
                    // 붓 칠처럼 3~4 톤으로 나눔(그라데이션 대신)
                    Color c = k < 0.36f ? dark : k < 0.58f ? mid : lite;
                    if (Tile(u, v, 16, 5) > 0.80f && small > 0.55f) c = Color.Lerp(c, soil, 0.65f);   // 드문드문 흙이 보이는 자리
                    px[y * S + x] = new Color(c.r, c.g, c.b, 1f);
                }
            tex.SetPixels(px); tex.Apply();
            File.WriteAllBytes(Path.GetFullPath(path), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material GrassOverlayMat()
        {
            EnsureFolder(Pass6MatDir);
            var sh = Shader.Find("Haengin/DecalQuad") ?? throw new Exception("Haengin/DecalQuad 셰이더 없음");
            string path = Pass6MatDir + "/M_G6_GrassOverlay.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(sh) { name = "M_G6_GrassOverlay" }; AssetDatabase.CreateAsset(m, path); }
            if (m.shader != sh) m.shader = sh;
            m.SetTexture("_MainTex", GrassTex6()); m.SetTextureScale("_MainTex", Vector2.one); m.SetTextureOffset("_MainTex", Vector2.zero);
            m.SetColor("_Tint", Color.white); m.SetFloat("_Cull", 2f); m.SetFloat("_Multiply", 1f);
            m.SetFloat("_SrcBlend", (float)BlendMode.DstColor); m.SetFloat("_DstBlend", (float)BlendMode.Zero);
            m.SetFloat("_Fade", 0f); m.SetFloat("_EdgeRough", 0f); m.SetFloat("_AtlasUV", 1f);
            m.renderQueue = 2550;   // 불투명(≤2500) 다 그린 뒤 · 땅 데칼(Transparent-400=2600)보다 먼저: 풀 위에 얼룩이 올라가게
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material CardMat6(string key, string clump, Color tint, float wind, float ink, bool sphere)
        {
            EnsureFolder(Pass6MatDir);
            var sh = Shader.Find("Haengin/ToonLeafCard") ?? throw new Exception("Haengin/ToonLeafCard 셰이더 없음");
            string path = $"{Pass6MatDir}/M_G6_{key}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(sh) { name = "M_G6_" + key }; AssetDatabase.CreateAsset(m, path); }
            if (m.shader != sh) m.shader = sh;
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/Clump_{clump}.png"));
            m.SetColor("_BaseColor", tint); m.SetColor("_ShadeColor", LookdevSetup.LeafShade);
            m.SetFloat("_Step", 0.5f); m.SetFloat("_Feather", 0.03f); m.SetFloat("_WindAmp", wind); m.SetFloat("_WindFreq", 1.5f); m.SetFloat("_Height", 1f);
            m.SetFloat("_Cutoff", 0.4f); m.SetFloat("_Sphere", sphere ? 1f : 0f); m.SetFloat("_Ink", ink); m.SetFloat("_InkWidth", 0.25f);
            m.SetColor("_InkColor", LookdevSetup.LeafInk); m.SetFloat("_StencilRef", 1f); m.SetFloat("_SwayUV", 1f);
            m.enableInstancing = false; EditorUtility.SetDirty(m);
            return m;
        }

        /// 2×2 아틀라스 칸마다 알파가 있는 범위(0~1, 아래 = 땅) — 카드 아래쪽 빈칸만큼 떠 보이지 않게
        static Rect[] ClumpCrop(string clump)
        {
            var res = new Rect[4];
            for (int k = 0; k < 4; k++) res[k] = new Rect((k % 2) * 0.5f, (k / 2) * 0.5f, 0.5f, 0.5f);
            string src = Path.GetFullPath($"{TexDir}/Clump_{clump}.png");
            if (!File.Exists(src)) return res;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(File.ReadAllBytes(src))) { Object.DestroyImmediate(tex); return res; }
            int W = tex.width, H = tex.height; var px = tex.GetPixels32();
            for (int k = 0; k < 4; k++)
            {
                int x0 = (k % 2) * W / 2, y0 = (k / 2) * H / 2, minx = int.MaxValue, maxx = -1, miny = int.MaxValue, maxy = -1;
                for (int y = y0; y < y0 + H / 2; y += 2)
                    for (int x = x0; x < x0 + W / 2; x += 2)
                        if (px[y * W + x].a > 100) { minx = Mathf.Min(minx, x); maxx = Mathf.Max(maxx, x); miny = Mathf.Min(miny, y); maxy = Mathf.Max(maxy, y); }
                if (maxx > minx && maxy > miny) res[k] = Rect.MinMaxRect(minx / (float)W, miny / (float)H, (maxx + 2) / (float)W, (maxy + 2) / (float)H);
            }
            Object.DestroyImmediate(tex);
            return res;
        }

        static string Green6(Zone1Data d, Transform zoneRoot, Transform env)
        {
            var terrainGo = zoneRoot.Find("Ground/Terrain");
            var tmf = terrainGo != null ? terrainGo.GetComponent<MeshFilter>() : null; var tcol = terrainGo != null ? terrainGo.GetComponent<Collider>() : null;
            if (tmf == null || tcol == null || tmf.sharedMesh == null) return "초록(6차): 지형 없음";
            var root = new GameObject("Green6").transform; root.SetParent(env, false);
            Physics.SyncTransforms();
            // 1) 1m 격자: 위에서 쏜 빛이 지형에 바로 닿는 칸 = 맨땅
            float x0 = d.BoundsMin.x, z0 = d.BoundsMin.z;
            int nx = Mathf.CeilToInt(d.BoundsMax.x - x0), nz = Mathf.CeilToInt(d.BoundsMax.z - z0);
            var free = new bool[nx, nz]; var gp = new Vector3[nx, nz]; var gn = new Vector3[nx, nz]; var bld = new bool[nx, nz];
            int freeN = 0;
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    var o = new Vector3(x0 + i + 0.5f, 160f, z0 + j + 0.5f);
                    if (Physics.Raycast(o, Vector3.down, out var h, 260f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (h.collider == tcol) { free[i, j] = true; gp[i, j] = h.point; gn[i, j] = h.normal; freeN++; }
                        else for (var x = h.collider.transform; x != null; x = x.parent) if (x.name == "house" || x.name == "shop" || x.name == "hanok" || x.name == "campus") { bld[i, j] = true; break; }
                    }
                }
            // 맨땅 아닌 칸까지 거리(칸, 체비셰프, 최대 6)
            var dist = new int[nx, nz];
            for (int i = 0; i < nx; i++) for (int j = 0; j < nz; j++) dist[i, j] = free[i, j] ? 6 : 0;
            for (int pass = 0; pass < 6; pass++)
                for (int i = 0; i < nx; i++)
                    for (int j = 0; j < nz; j++)
                    {
                        if (dist[i, j] == 0) continue;
                        int m = dist[i, j];
                        for (int di = -1; di <= 1; di++) for (int dj = -1; dj <= 1; dj++)
                        {
                            int a = i + di, b = j + dj;
                            if (a < 0 || b < 0 || a >= nx || b >= nz) continue;
                            m = Mathf.Min(m, dist[a, b] + 1);
                        }
                        dist[i, j] = m;
                    }
            bool In(int i, int j) => i >= 0 && j >= 0 && i < nx && j < nz;
            // 집·상가까지 거리(칸, 최대 8) — 집 둘레 마당·골목 땅은 흙·시멘트 쪽(풀 덮개 옅게), 비탈·공원은 풀밭
            var dB = new int[nx, nz];
            for (int i = 0; i < nx; i++) for (int j = 0; j < nz; j++) dB[i, j] = bld[i, j] ? 0 : 8;
            for (int pass = 0; pass < 8; pass++)
                for (int i = 0; i < nx; i++)
                    for (int j = 0; j < nz; j++)
                    {
                        if (dB[i, j] == 0) continue;
                        int m = dB[i, j];
                        for (int di = -1; di <= 1; di++) for (int dj = -1; dj <= 1; dj++) { int a = i + di, b = j + dj; if (In(a, b)) m = Mathf.Min(m, dB[a, b] + 1); }
                        dB[i, j] = m;
                    }
            float Pn(Vector3 p, float s, float o) => Mathf.PerlinNoise(p.x * s + o, p.z * s + o * 1.7f);
            // 길 종류(오솔길·성곽길)까지 가까운지 — 흰 꽃 관목은 산책길 가장자리에
            var trails = d.Roads.Where(r => r.Kind == "path" || r.Kind == "trail" || r.Kind == "wallpath").ToList();
            float DistTrail(Vector3 p)
            {
                float best = 1e9f;
                foreach (var r in trails)
                    for (int k = 0; k < r.Points.Length - 1; k++)
                    {
                        var a = r.Points[k]; var b = r.Points[k + 1]; var ab = Flat(b - a);
                        float t = Mathf.Clamp01(Vector3.Dot(Flat(p - a), ab) / Mathf.Max(ab.sqrMagnitude, 1e-4f));
                        best = Mathf.Min(best, Flat(a + (b - a) * t - p).magnitude - r.Width / 2);
                    }
                return best;
            }

            // 2) 풀색 곱하기 덮개(지형 정점마다 알파) — 32m 칸
            var tm = tmf.sharedMesh; var tv = tm.vertices; var tt = tm.triangles; var TM = tmf.transform.localToWorldMatrix;
            var wv = new Vector3[tv.Length]; var al = new float[tv.Length];
            for (int k = 0; k < tv.Length; k++)
            {
                var w = TM.MultiplyPoint3x4(tv[k]); wv[k] = w;
                int i = Mathf.FloorToInt(w.x - x0), j = Mathf.FloorToInt(w.z - z0);
                float a = 0f;
                // 정점 둘레 2×2 칸이 모두 맨땅일 때만(길 가장자리로 번지지 않게)
                if (In(i, j) && In(i - 1, j - 1) && free[i, j] && free[i - 1, j] && free[i, j - 1] && free[i - 1, j - 1])
                {
                    a = 0.50f + 0.40f * Pn(w, 0.06f, 3.1f) + 0.15f * (Pn(w, 0.23f, 9.2f) - 0.5f);
                    if (gn[i, j].y < 0.93f) a += 0.2f;
                    if (dist[i, j] <= 1) a *= 0.8f;
                    a *= Mathf.Lerp(0.3f, 1f, Mathf.InverseLerp(1.5f, 7f, dB[i, j]));
                    if (InPlaza7(w, 0.4f)) a = 0f;
                    a = Mathf.Clamp01(a) * 0.95f;
                }
                al[k] = a;
            }
            var overlay = new Dictionary<(int, int), (MB6 mb, Dictionary<int, int> map)>();
            for (int t = 0; t + 2 < tt.Length; t += 3)
            {
                int a = tt[t], b = tt[t + 1], c = tt[t + 2];
                if (Mathf.Max(al[a], Mathf.Max(al[b], al[c])) < 0.02f) continue;
                var key = Cell6((wv[a] + wv[b] + wv[c]) / 3f, 64f);
                if (!overlay.TryGetValue(key, out var acc)) overlay[key] = acc = (new MB6(), new Dictionary<int, int>());
                int V(int k)
                {
                    if (acc.map.TryGetValue(k, out int vi)) return vi;
                    var w = wv[k];
                    vi = acc.mb.Add(w + Vector3.up * 0.025f, Vector3.up, new Vector2(w.x, w.z) / 4f, new Vector2(w.x, w.z) / 6f, new Color(1f, 1f, 1f, al[k]));
                    acc.map[k] = vi; return vi;
                }
                acc.mb.Tri(V(a), V(b), V(c));
            }
            var gmat = GrassOverlayMat();
            foreach (var kv in overlay.OrderBy(x => x.Key.Item1).ThenBy(x => x.Key.Item2))
                Obj6($"풀색 덮개 {kv.Key.Item1}_{kv.Key.Item2}", root, kv.Value.mb.Save($"g6_overlay_{kv.Key.Item1}_{kv.Key.Item2}"), gmat, ShadowCastingMode.Off, false);

            // 3) 잡초 띠(길·담 가장자리 진하게, 안쪽은 잡음으로 듬성) + 담·축대 틈 풀 — 카드(X 두 장), 24m 칸
            var weedCrop = ClumpCrop("weed"); var bushCrop = ClumpCrop("broad"); var flowerCrop = ClumpCrop("flower");
            var weeds = new Dictionary<(int, int), MB6>(); var bushes = new Dictionary<(int, int), MB6>(); var flowers = new Dictionary<(int, int), MB6>();
            MB6 Acc(Dictionary<(int, int), MB6> dd, Vector3 p) { var k = Cell6(p, 48f); if (!dd.TryGetValue(k, out var m)) dd[k] = m = new MB6(); return m; }
            var rng = new System.Random(606);
            float Rnd() => (float)rng.NextDouble();
            void Card(MB6 mb, Vector3 baseC, Vector3 right, float w, float h, Rect uv, Vector3? sphereC, float swayBase, float swayH)
            {
                var up = Vector3.up * h;
                var p = new[] { baseC - right * (w / 2), baseC + right * (w / 2), baseC + right * (w / 2) + up, baseC - right * (w / 2) + up };
                var t = new[] { new Vector2(uv.xMin, uv.yMin), new Vector2(uv.xMax, uv.yMin), new Vector2(uv.xMax, uv.yMax), new Vector2(uv.xMin, uv.yMax) };
                int b0 = mb.V.Count;
                for (int k = 0; k < 4; k++)
                {
                    var nrm = sphereC.HasValue ? (p[k] - sphereC.Value).normalized : Vector3.up;
                    mb.Add(p[k], nrm, t[k], new Vector2(0f, Mathf.Clamp01((p[k].y - swayBase) / Mathf.Max(0.1f, swayH))), Color.white);
                }
                mb.Tri(b0, b0 + 2, b0 + 1); mb.Tri(b0, b0 + 3, b0 + 2);
            }
            void Clump(MB6 mb, Vector3 g, float w, float h, Rect[] crop)
            {
                float yaw = Rnd() * 180f; var c = crop[rng.Next(4)];
                var r1 = Quaternion.Euler(0f, yaw, 0f) * Vector3.right; var r2 = Quaternion.Euler(0f, yaw + 90f, 0f) * Vector3.right;
                var b = g - Vector3.up * 0.04f;
                Card(mb, b, r1, w, h, c, null, b.y, h); Card(mb, b, r2, w * 0.85f, h * 0.9f, crop[rng.Next(4)], null, b.y, h);
            }
            int weedN = 0, crackN = 0, bushN = 0, flowerN = 0;
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    if (!free[i, j]) continue;
                    int dd = dist[i, j]; var p0 = gp[i, j];
                    if (InPlaza7(p0, 0.5f)) continue;
                    float pr = dd <= 1 ? 0.8f : dd == 2 ? 0.4f : 0.10f + 0.32f * Pn(p0, 0.09f, 5.5f);
                    if (gn[i, j].y < 0.93f) pr += 0.2f;
                    int cnt = Rnd() < pr ? (dd <= 1 ? 2 : 1) : 0;
                    for (int c = 0; c < cnt; c++)
                    {
                        var q = new Vector3(p0.x + (Rnd() - 0.5f) * 0.9f, p0.y + 3f, p0.z + (Rnd() - 0.5f) * 0.9f);
                        if (!Physics.Raycast(q, Vector3.down, out var hh, 8f, ~0, QueryTriggerInteraction.Ignore) || hh.collider != tcol) continue;
                        float w = dd <= 1 ? 0.6f + Rnd() * 0.5f : 0.45f + Rnd() * 0.45f;
                        Clump(Acc(weeds, hh.point), hh.point, w, w * 0.55f, weedCrop); weedN++;
                    }
                    // 담·축대·건물 밑 틈 풀: 맨땅 가장자리에서 옆 칸(맨땅 아님) 쪽으로 수평 빛
                    if (dd == 1)
                        foreach (var (di, dj) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                        {
                            if (!In(i + di, j + dj) || free[i + di, j + dj] || Rnd() > 0.35f) continue;
                            var dir = new Vector3(di, 0f, dj);
                            float hy = 0.08f + Rnd() * 1.7f;
                            if (!Physics.Raycast(p0 + Vector3.up * hy - dir * 0.3f, dir, out var wh, 1.5f, ~(1 << Layers.Player), QueryTriggerInteraction.Ignore)) continue;
                            if (wh.collider == tcol || Mathf.Abs(wh.normal.y) > 0.35f) continue;
                            string kind = "";
                            for (var x = wh.collider.transform; x != null; x = x.parent) { if (x.name == "retaining" || x.name == "Walls" || x.name == "fence") { kind = x.name; break; } }
                            float maxH = kind == "retaining" || kind == "Walls" ? 1.9f : kind == "fence" ? 0.5f : 0.3f;
                            if (hy > maxH) continue;
                            var nrm = new Vector3(wh.normal.x, 0f, wh.normal.z).normalized;
                            var right = Vector3.Cross(Vector3.up, nrm).normalized;
                            float s = 0.22f + Rnd() * 0.25f;
                            var bpos = wh.point + nrm * 0.035f - Vector3.up * (s * 0.25f);
                            Card(Acc(weeds, bpos), bpos, right, s * 1.3f, s, weedCrop[rng.Next(4)], null, bpos.y, s); crackN++;
                        }
                }
            // 4) 관목 군락: 비탈·산책길 가장자리(맨땅 안쪽 2칸 이상), 3.5m 간격 — 흰 꽃 관목은 산책길 가장자리에 많이
            var taken = new HashSet<(int, int)>();
            for (int i = 1; i < nx - 1; i++)
                for (int j = 1; j < nz - 1; j++)
                {
                    if (!free[i, j] || dist[i, j] < 2) continue;
                    if (InPlaza7(gp[i, j], 1.6f)) continue;
                    var p0 = gp[i, j]; bool slope = gn[i, j].y < 0.92f; float dt = DistTrail(p0);
                    bool trailEdge = dt > 0.6f && dt < 3.5f;
                    float pr = (slope ? 0.10f : 0.012f) + (trailEdge ? 0.10f : 0f);
                    pr *= 0.6f + 0.8f * Pn(p0, 0.07f, 1.3f);
                    if (Rnd() > pr) continue;
                    var cell = (i / 3, j / 3);
                    if (taken.Contains(cell)) continue;
                    taken.Add(cell);
                    bool flower = trailEdge ? Rnd() < 0.45f : Rnd() < 0.15f;
                    int blobs = 2 + rng.Next(3);
                    for (int bi = 0; bi < blobs; bi++)
                    {
                        float r = (flower ? 0.32f : 0.38f) + Rnd() * 0.38f;
                        var off = Quaternion.Euler(0f, Rnd() * 360f, 0f) * Vector3.forward * (bi == 0 ? 0f : 0.4f + Rnd() * 0.7f);
                        var q = p0 + off + Vector3.up * 3f;
                        if (!Physics.Raycast(q, Vector3.down, out var hh, 8f, ~0, QueryTriggerInteraction.Ignore) || hh.collider != tcol) continue;
                        var g = hh.point; var bc = g + Vector3.up * (r * 0.55f);
                        var mb = Acc(flower ? flowers : bushes, g); var crop = flower ? flowerCrop : bushCrop;
                        for (int k = 0; k < 9; k++)
                        {
                            float az = Rnd() * 360f, el = k < 3 ? 70f + Rnd() * 20f : Rnd() * 55f;
                            var dir = Quaternion.Euler(-el, az, 0f) * Vector3.forward;
                            var cc = bc + dir * (r * 0.5f);
                            var right = Quaternion.Euler(0f, az + 90f + (Rnd() - 0.5f) * 40f, 0f) * Vector3.forward;
                            float s = r * (1.05f + Rnd() * 0.3f);
                            Card(mb, cc - Vector3.up * (s / 2), right, s, s, crop[rng.Next(4)], bc - Vector3.up * (r * 0.3f), g.y, r * 2f);
                        }
                        if (flower) flowerN++; else bushN++;
                    }
                }
            var weedM = CardMat6("weed", "weed", new Color(0.93f, 0.96f, 0.90f), 0.035f, 0.55f, true);
            var bushM = CardMat6("bush", "broad", new Color(0.80f, 0.89f, 0.74f), 0.03f, 1f, true);
            var flowerM = CardMat6("flower", "flower", Color.white, 0.03f, 1f, true);
            foreach (var (dd, mat, nm, shadow) in new[] { (weeds, weedM, "잡초", false), (bushes, bushM, "관목", true), (flowers, flowerM, "꽃 관목", true) })
                foreach (var kv in dd.OrderBy(x => x.Key.Item1).ThenBy(x => x.Key.Item2))
                    Obj6($"{nm} {kv.Key.Item1}_{kv.Key.Item2}", root, kv.Value.Save($"g6_{mat.name}_{kv.Key.Item1}_{kv.Key.Item2}"), mat, shadow ? ShadowCastingMode.On : ShadowCastingMode.Off);
            return $"초록(6차): 맨땅 {freeN}칸 · 풀색 덮개 {overlay.Count}조각 · 잡초 {weedN} · 틈 풀 {crackN} · 관목 덩이 {bushN} · 흰 꽃 관목 덩이 {flowerN}";
        }

        // ═════════════════════════ ③ 밤 빛 웅덩이
        static Material PoolMat()
        {
            EnsureFolder(Pass6MatDir);
            var sh = Shader.Find("Haengin/LightPool") ?? throw new Exception("Haengin/LightPool 셰이더 없음");
            string path = Pass6MatDir + "/M_LightPool6.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(sh) { name = "M_LightPool6" }; AssetDatabase.CreateAsset(m, path); }
            if (m.shader != sh) m.shader = sh;
            m.SetFloat("_Strength", 1f); m.SetFloat("_Bands", 0.25f); m.SetFloat("_Cover", 0.55f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static string Pools6(Zone1Data d, Transform zoneRoot, Transform env, Dictionary<string, Dictionary<string, object>> objs)
        {
            var root = new GameObject("Pools6").transform; root.SetParent(env, false);
            var acc = new Dictionary<(int, int), MB6>();
            int lamps = 0, shops = 0;
            void Pool(Vector3 c, Vector3 fwd, float halfW, float len, bool shop, Color col, float k)
            {
                var key = Cell6(c, 48f);
                if (!acc.TryGetValue(key, out var mb)) acc[key] = mb = new MB6();
                fwd = Flat(fwd).normalized; var right = Vector3.Cross(Vector3.up, fwd).normalized;
                const int G = 8;
                int b0 = mb.V.Count;
                for (int b = 0; b <= G; b++)
                    for (int a = 0; a <= G; a++)
                    {
                        float u = -1f + 2f * a / G, v = shop ? b / (float)G : -1f + 2f * b / G;
                        var p = c + right * (u * halfW) + fwd * (shop ? v * len : v * halfW);
                        if (Physics.Raycast(p + Vector3.up * 2.5f, Vector3.down, out var h, 6f, 1 << Layers.Ground, QueryTriggerInteraction.Ignore)) p.y = h.point.y + 0.03f;
                        else p.y = c.y + 0.03f;
                        mb.Add(p, Vector3.up, new Vector2(u, v), new Vector2(shop ? 1f : 0f, shop ? 1f : 0f), new Color(col.r, col.g, col.b, k));
                    }
                for (int b = 0; b < G; b++)
                    for (int a = 0; a < G; a++)
                    {
                        int i0 = b0 + b * (G + 1) + a;
                        mb.Tri(i0, i0 + G + 1, i0 + 1); mb.Tri(i0 + 1, i0 + G + 1, i0 + G + 2);
                    }
            }
            foreach (var s in zoneRoot.GetComponentsInChildren<LightSpot>(true))
            {
                if (s.Kind != LightKind.Street && s.Kind != LightKind.Security) continue;
                var lp = s.transform.position; var g = GroundAt(lp, 0.5f, 14f);
                if (Mathf.Abs(g.y - lp.y) < 0.6f) continue;   // 땅을 못 찾음
                bool street = s.Kind == LightKind.Street;
                Pool(g, Vector3.forward, street ? 5.4f : 4.2f, 0f, false, new Color(1.0f, 0.72f, 0.42f), street ? 0.8f : 0.66f);   // 7차: 더 넓고 진하게(f01)
                lamps++;
            }
            foreach (var kv in objs.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                if (S(kv.Value, "kind") != "sign") continue;
                var inf = D(G(kv.Value, "info"));
                if ((S(inf, "sign_kind") ?? "band") != "band" || inf.ContainsKey("hidden")) continue;
                var sz = L(G(inf, "size_m")); if (sz == null || sz.Count < 2) continue;
                var c = V3(G(inf, "center")); var n = V3(G(inf, "normal")).normalized; float w = F(sz[0]);
                var wall = c - n * 0.123f; var foot = GroundAt(wall + n * 0.3f, 1f, 8f);
                if (c.y - foot.y > 4.6f) continue;   // 1층 가게만
                string tenant = S(inf, "tenant") ?? "";
                var col = tenant.Contains("편의점") ? new Color(0.95f, 0.95f, 0.86f) : new Color(1.0f, 0.76f, 0.46f);
                Pool(foot + n * 0.1f, n, w * 0.75f, 6.0f, true, col, 0.85f);   // 7차: 가게 앞 번짐 반경·세기 ↑
                shops++;
            }
            // 7차: 성균관로 전봇대 아래 길 쪽(프레임 f01 처럼 큰길이 주황으로 읽히게 — 전봇대 등 자리)
            int poles = 0;
            var main = d.Roads[0];
            foreach (var lm in d.Landmarks.Where(x => (x.Kind == "pole" || x.Kind == "board") && (x.Name.Contains("성균관로") || x.Name.Contains("전단지"))))
            {
                float best = 1e9f; Vector3 near = lm.Pos;
                for (int k = 0; k < main.Points.Length - 1; k++) { var a = main.Points[k]; var b = main.Points[k + 1]; var ab = Flat(b - a); float t = Mathf.Clamp01(Vector3.Dot(Flat(lm.Pos - a), ab) / Mathf.Max(ab.sqrMagnitude, 1e-4f)); var q = a + (b - a) * t; float dd = Flat(q - lm.Pos).magnitude; if (dd < best) { best = dd; near = q; } }
                var toRoad = Flat(near - lm.Pos).normalized;
                var gp = GroundAt(lm.Pos + toRoad * 1.6f, 2f, 6f);
                Pool(gp, Vector3.forward, 5.0f, 0f, false, new Color(1.0f, 0.70f, 0.40f), 0.55f); poles++;
            }
            var mat = PoolMat();
            foreach (var kv in acc.OrderBy(x => x.Key.Item1).ThenBy(x => x.Key.Item2))
                Obj6($"빛 웅덩이 {kv.Key.Item1}_{kv.Key.Item2}", root, kv.Value.Save($"p6_pool_{kv.Key.Item1}_{kv.Key.Item2}"), mat, ShadowCastingMode.Off, false);
            return $"밤 빛 웅덩이(6차): 가로등·보안등 {lamps} · 가게 앞 {shops} · 큰길 전봇대 {poles}(7차) · {acc.Count}조각";
        }

        // ═════════════════════════ ④ 계단 손잡이(골목 계단·와룡공원 계단 — 프레임 f03)
        static int Rails6(Zone1Data d, Transform env)
        {
            var root = new GameObject("Rails6").transform; root.SetParent(env, false);
            var steel = Flat("M_Rail_Steel", new Color(0.72f, 0.74f, 0.74f));
            int n = 0;
            foreach (var s in d.Stairs)
            {
                var a = s.From; var b = s.To; var df = Flat(b - a); float L = df.magnitude; if (L < 1f) continue;
                var dir = df / L; var right = Vector3.Cross(Vector3.up, dir).normalized;
                var sides = s.Width >= 2.4f ? new[] { -1f, 1f } : new[] { 1f };
                foreach (float sd in sides)
                {
                    float off = s.Width / 2 - 0.12f;
                    int posts = Mathf.Max(2, Mathf.CeilToInt(L / 1.6f) + 1);
                    var tops = new List<Vector3>(); var mids = new List<Vector3>();
                    for (int k = 0; k < posts; k++)
                    {
                        float t = k / (float)(posts - 1);
                        var line = Vector3.Lerp(a, b, t) + right * (sd * off);
                        var g = GroundAt(line, 1.5f, 4f);
                        float topY = line.y + 0.92f; float postH = Mathf.Max(0.4f, topY - g.y);
                        Box("난간 기둥", root, new Vector3(line.x, g.y + postH / 2, line.z), new Vector3(0.05f, postH, 0.05f), Quaternion.LookRotation(dir, Vector3.up), steel, true);
                        tops.Add(new Vector3(line.x, topY, line.z)); mids.Add(new Vector3(line.x, line.y + 0.48f, line.z));
                    }
                    foreach (var (pts, th) in new[] { (tops, 0.055f), (mids, 0.035f) })
                        for (int k = 0; k < pts.Count - 1; k++)
                        {
                            var seg = pts[k + 1] - pts[k]; float len = seg.magnitude; if (len < 0.05f) continue;
                            Box("난간 손잡이", root, (pts[k] + pts[k + 1]) / 2, new Vector3(th, th, len + (th > 0.05f ? 0.06f : 0f)), Quaternion.LookRotation(seg / len, Vector3.up), steel, th > 0.05f);
                        }
                }
                n++;
            }
            // 5차 대조표 남은 것: f02 주차장 옹벽 위 쇠 난간 · f06 성곽길 바깥(비탈) 쪽 나무 난간
            var pipe = Flat("M_Rail_Green", new Color(0.30f, 0.45f, 0.36f));
            foreach (var b in d.Blocks.Where(x => x.Kind == "retaining"))
            {
                var rot = Quaternion.Euler(0f, b.Yaw, 0f);
                bool alongX = b.Size.x >= b.Size.z; float L = alongX ? b.Size.x : b.Size.z, th = alongX ? b.Size.z : b.Size.x;
                var ax = rot * (alongX ? Vector3.right : Vector3.forward); var nrm = rot * (alongX ? Vector3.forward : Vector3.right);
                float top = b.Center.y + b.Size.y / 2f;
                float gA = GroundAt(b.Center + nrm * (th / 2 + 1.2f) + Vector3.up * (top - b.Center.y), 1.5f, 12f).y, gB = GroundAt(b.Center - nrm * (th / 2 + 1.2f) + Vector3.up * (top - b.Center.y), 1.5f, 12f).y;
                float hi = Mathf.Max(gA, gB); var side = gA >= gB ? nrm : -nrm;
                // 위쪽 땅이 옹벽 머리 높이면 그 가장자리에, 양쪽이 다 낮으면(지형이 옹벽 뒤를 안 올린 곳 — 주차장) 머리 가운데에
                bool edge = Mathf.Abs(hi - top) <= 0.6f;
                if (L < 3f || (!edge && top - hi < 1.0f)) continue;
                if (!edge) side = Vector3.zero;   // 위쪽 땅이 옹벽 머리와 비슷한 높이일 때만(떨어지는 쪽에 난간)
                int posts = Mathf.Max(2, Mathf.CeilToInt(L / 1.8f) + 1);
                var tops = new List<Vector3>();
                for (int k = 0; k < posts; k++)
                {
                    var p = b.Center + ax * (-L / 2 + 0.15f + (L - 0.3f) * k / (posts - 1)) + side * (th / 2 - 0.12f); p.y = top;
                    Box("옹벽 난간 기둥", root, p + Vector3.up * 0.5f, new Vector3(0.05f, 1.0f, 0.05f), rot, pipe, true);
                    tops.Add(p + Vector3.up * 1.0f);
                }
                foreach (float hh in new[] { 0f, -0.5f })
                {
                    var a0 = tops[0] + Vector3.up * hh; var a1 = tops[tops.Count - 1] + Vector3.up * hh; var sg = a1 - a0;
                    Box("옹벽 난간 가로대", root, (a0 + a1) / 2, new Vector3(0.045f, 0.045f, sg.magnitude), Quaternion.LookRotation(sg.normalized, Vector3.up), pipe, hh == 0f);
                }
                n++;
            }
            var wood = Flat("M_Fence_Wood", new Color(0.46f, 0.33f, 0.23f));
            var trail = d.Roads.FirstOrDefault(r => r.Kind == "wallpath");
            if (trail != null && trail.Points.Length >= 2)
            {
                float DistOther(Vector3 p)
                {
                    float best = 1e9f;
                    foreach (var r in d.Roads)
                    {
                        if (r == trail) continue;
                        for (int k = 0; k < r.Points.Length - 1; k++)
                        {
                            var a = r.Points[k]; var b2 = r.Points[k + 1]; var ab = Flat(b2 - a);
                            float t = Mathf.Clamp01(Vector3.Dot(Flat(p - a), ab) / Mathf.Max(ab.sqrMagnitude, 1e-4f));
                            best = Mathf.Min(best, Flat(a + (b2 - a) * t - p).magnitude - r.Width / 2);
                        }
                    }
                    return best;
                }
                // 비탈(낮은) 쪽 고르기: 길 전체에서 양쪽 땅 높이 평균
                float sumL = 0f, sumR = 0f;
                for (int k = 0; k < trail.Points.Length - 1; k++)
                {
                    var a = trail.Points[k]; var b2 = trail.Points[k + 1]; var dir = Flat(b2 - a).normalized; var rt = Vector3.Cross(Vector3.up, dir);
                    var mid = (a + b2) / 2;
                    sumR += GroundAt(mid + rt * (trail.Width / 2 + 1.5f), 4f, 10f).y; sumL += GroundAt(mid - rt * (trail.Width / 2 + 1.5f), 4f, 10f).y;
                }
                float sd = sumR < sumL ? 1f : -1f;
                Vector3? prev = null; int fp = 0;
                float total = 0f; for (int k = 0; k < trail.Points.Length - 1; k++) total += Flat(trail.Points[k + 1] - trail.Points[k]).magnitude;
                for (float sAt = 0.8f; sAt < total - 0.8f; sAt += 2.0f)
                {
                    var (pp, dir) = Along(trail.Points, sAt);
                    var rt = Vector3.Cross(Vector3.up, dir.normalized);
                    var p = GroundAt(pp + rt * (sd * (trail.Width / 2 + 0.35f)), 3f, 8f);
                    if (DistOther(p) < 0.6f || InPlaza7(p, 1.4f)) { prev = null; continue; }   // 갈림길 입구·7차 둥근 광장 입구는 비움
                    Box("나무 난간 기둥", root, p + Vector3.up * 0.45f, new Vector3(0.09f, 0.9f, 0.09f), Quaternion.LookRotation(dir, Vector3.up), wood, true);
                    if (prev.HasValue)
                        foreach (float hh in new[] { 0.42f, 0.82f })
                        {
                            var a0 = prev.Value + Vector3.up * hh; var a1 = p + Vector3.up * hh; var sg = a1 - a0;
                            Box("나무 난간 가로대", root, (a0 + a1) / 2, new Vector3(0.05f, 0.07f, sg.magnitude + 0.08f), Quaternion.LookRotation(sg.normalized, Vector3.up), wood, hh > 0.8f);
                        }
                    prev = p; fp++;
                }
                if (fp > 0) n++;
            }
            return n;
        }

        // ═════════════════════════ ⑤ 그리기 호출 줄이기: 같은 재질 × 32m 칸 → 한 메시
        static readonly HashSet<string> NoMergeShaders = new HashSet<string> { "Haengin/ToonLeaf", "Haengin/SkyCard", "Haengin/CloudCard", "Haengin/LightPool" };
        static readonly HashSet<string> FaceNames6 = new HashSet<string> { "면", "면 A", "면 B" };

        static bool Blocked6(Transform t, Transform stop)
        {
            for (var x = t; x != null && x != stop; x = x.parent)
            {
                if (x.GetComponent<LODGroup>() != null || x.GetComponent<Light>() != null || x.GetComponent<Animator>() != null) return true;
                foreach (var mb in x.GetComponents<MonoBehaviour>()) if (mb != null) return true;
            }
            return false;
        }

        /// 데칼 판 재질(아틀라스 칸·알파가 재질마다 다름)을 하나로: 칸 → 정점 uv2, 알파 → 정점 색
        static readonly Dictionary<string, Material> canon6 = new Dictionary<string, Material>();
        static Material Canon6(Material m)
        {
            if (m.shader == null || m.shader.name != "Haengin/DecalQuad" || m.GetFloat("_AtlasUV") > 0.5f) return m;
            var tex = m.GetTexture("_MainTex");
            string key = $"{(tex != null ? tex.name : "none")}_{m.GetFloat("_Multiply"):0}_{Mathf.RoundToInt(m.GetFloat("_Fade") * 100)}_{Mathf.RoundToInt(m.GetFloat("_EdgeRough") * 100)}_{m.GetFloat("_Cull"):0}_{m.renderQueue}";
            if (canon6.TryGetValue(key, out var c) && c != null) return c;
            EnsureFolder(Pass6MatDir);
            string path = $"{Pass6MatDir}/M_D6_{key}.mat";
            c = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (c == null) { c = new Material(m.shader) { name = "M_D6_" + key }; AssetDatabase.CreateAsset(c, path); }
            c.CopyPropertiesFromMaterial(m);
            c.SetTextureScale("_MainTex", Vector2.one); c.SetTextureOffset("_MainTex", Vector2.zero);
            c.SetColor("_Tint", Color.white); c.SetFloat("_AtlasUV", 1f); c.renderQueue = m.renderQueue;
            EditorUtility.SetDirty(c);
            canon6[key] = c;
            return c;
        }

        static string Merge6(Transform env, Transform zoneRoot)
        {
            canon6.Clear();
            var holders = new List<(Transform scan, Transform holder)>();
            foreach (var nm in new[] { "Decor", "Signs5", "Street5", "Surface5", "Motion5", "Rails6", "BusAds6", "Lot", "Plaza7" })
            { var t = env.Find(nm); if (t != null) holders.Add((t, t)); }
            // 구역 바탕(길·패드·계단): Zone1 을 새로 지을 때만(이미 합친 게 있으면 그 메시를 지키고 건너뜀)
            foreach (var nm in new[] { "Roads", "Ground", "Stairs" })
            {
                var t = zoneRoot.Find(nm); if (t == null) continue;
                var old = t.GetComponentsInChildren<MeshFilter>(true).Where(m => m.name.StartsWith("합침") && m.sharedMesh != null).ToList();
                if (old.Count > 0) { foreach (var m in old) Mesh6Keep(m.sharedMesh.name); continue; }
                holders.Add((t, t));
            }
            var signHolder = new GameObject("간판 몸 합침").transform; signHolder.SetParent(env, false);
            foreach (var t in env.GetComponentsInChildren<Transform>(true).Where(x => x.name.StartsWith("Sign_") && x.parent != null && x.parent.name.StartsWith("Z1_")).ToList())
                holders.Add((t, signHolder));
            var groups = new SortedDictionary<string, (Transform holder, Material mat, ShadowCastingMode sh, bool recv, List<(MeshRenderer r, MeshFilter mf, int sub, Material src)> list)>(StringComparer.Ordinal);
            var owners = new Dictionary<MeshRenderer, List<string>>();
            int before = 0;
            foreach (var (scan, holder) in holders)
                foreach (var r in scan.GetComponentsInChildren<MeshRenderer>(false))
                {
                    if (!r.enabled || FaceNames6.Contains(r.name) || r.name == "Terrain" || r.name.StartsWith("Apron")) continue;
                    var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
                    if (r.GetComponent<TMP_Text>() != null || Blocked6(r.transform, scan.parent)) continue;   // 위로는 훑는 뿌리까지만(키트 구역 뿌리의 Animator 는 안 봄)
                    var mats = r.sharedMaterials;
                    if (mats.Length != mf.sharedMesh.subMeshCount || mats.Any(m => m == null || m.shader == null)) continue;
                    if (mats.Any(m => NoMergeShaders.Contains(m.shader.name) || (m.shader.name == "Haengin/ToonLeafCard" && m.GetFloat("_SwayUV") < 0.5f))) continue;
                    var (cx, cz) = Cell6(r.bounds.center, 48f);
                    var keys = new List<string>();
                    for (int si = 0; si < mats.Length; si++)
                    {
                        var cm = Canon6(mats[si]);
                        string mp = AssetDatabase.GetAssetPath(cm); if (string.IsNullOrEmpty(mp)) mp = "mem" + cm.GetInstanceID();
                        string key = $"{holder.name}|{cm.name}|{mp}|{(int)r.shadowCastingMode}|{(r.receiveShadows ? 1 : 0)}|{cx}|{cz}";
                        if (!groups.TryGetValue(key, out var gr)) groups[key] = gr = (holder, cm, r.shadowCastingMode, r.receiveShadows, new List<(MeshRenderer, MeshFilter, int, Material)>());
                        gr.list.Add((r, mf, si, mats[si])); keys.Add(key);
                    }
                    owners[r] = keys; before++;
                }
            // 혼자인 한 재질 렌더러는 그대로 둔다
            var keep = new HashSet<MeshRenderer>(owners.Where(kv => kv.Value.Count == 1 && groups[kv.Value[0]].list.Count == 1).Select(kv => kv.Key));
            int made = 0, removed = 0, idx = 0;
            foreach (var kv in groups)
            {
                var (holder, mat, sh, recv, list) = kv.Value;
                var members = list.Where(x => !keep.Contains(x.r)).ToList();
                if (members.Count == 0) continue;
                var mb = new MB6(); bool decal = mat.shader.name == "Haengin/DecalQuad" && mat.GetFloat("_AtlasUV") > 0.5f;
                foreach (var (r, mf, si, src) in members)
                {
                    var mesh = mf.sharedMesh; var M = r.localToWorldMatrix; var NM = M.inverse.transpose; bool flip = M.determinant < 0f;
                    var vs = mesh.vertices; var ns = mesh.normals; var uv = mesh.uv; var uv2 = mesh.uv2; var cs = mesh.colors;
                    Vector4 st = decal && src.HasProperty("_MainTex") ? new Vector4(src.GetTextureScale("_MainTex").x, src.GetTextureScale("_MainTex").y, src.GetTextureOffset("_MainTex").x, src.GetTextureOffset("_MainTex").y) : new Vector4(1, 1, 0, 0);
                    Color tint = decal && src.HasProperty("_Tint") ? src.GetColor("_Tint") : Color.white;
                    var map = new Dictionary<int, int>();
                    var tri = mesh.GetTriangles(si);
                    int V(int k)
                    {
                        if (map.TryGetValue(k, out int vi)) return vi;
                        var u0 = uv.Length > k ? uv[k] : Vector2.zero;
                        var u1 = decal ? new Vector2(u0.x * st.x + st.z, u0.y * st.y + st.w) : (uv2.Length > k ? uv2[k] : Vector2.zero);
                        var col = decal ? tint : (cs.Length > k ? cs[k] : Color.white);
                        var nn = ns.Length > k ? NM.MultiplyVector(ns[k]).normalized : Vector3.up;
                        vi = mb.Add(M.MultiplyPoint3x4(vs[k]), nn, u0, u1, col); map[k] = vi; return vi;
                    }
                    for (int i = 0; i + 2 < tri.Length; i += 3)
                    { if (flip) mb.Tri(V(tri[i]), V(tri[i + 2]), V(tri[i + 1])); else mb.Tri(V(tri[i]), V(tri[i + 1]), V(tri[i + 2])); }
                }
                var parts = kv.Key.Split('|');
                Obj6($"합침 {mat.name} {parts[5]}_{parts[6]}", holder, mb.Save($"m6_{idx++:000}_{holder.name}_{mat.name}_{parts[5]}_{parts[6]}"), mat, sh, recv);
                made++;
            }
            foreach (var r in owners.Keys)
            {
                if (keep.Contains(r)) continue;
                var mf = r.GetComponent<MeshFilter>();
                Object.DestroyImmediate(r); if (mf != null) Object.DestroyImmediate(mf);
                removed++;
            }
            if (signHolder.childCount == 0) Object.DestroyImmediate(signHolder.gameObject);
            return $"그리기 합침(6차): 렌더러 {removed}개 → 합친 메시 {made}개(그대로 둔 혼자 {keep.Count})";
        }

        // ───────────────────────── 소품 합치기: 작은 소품(실외기·화분·계량기·상자 …, LOD 묶음 하나·재질 하나) → 같은 재질 × 12m 칸마다 LOD 0·1·2 합친 메시 + 칸 LOD 묶음
        //   원래 소품 전환 거리(작은 것 6/14m · 35m 컬링)를 칸 가운데 기준 7/16m · 36m 로(삼각형은 그대로 두고 그리기 호출만 줄임)
        const bool PropMergeOn = false;
        static string PropMerge6(Transform env)
        {
            var holder = new GameObject("소품 합침").transform; holder.SetParent(env, false);
            var groups = new SortedDictionary<string, (Material mat, ShadowCastingMode sh, List<(LODGroup lg, Renderer[][] lv)> list)>(StringComparer.Ordinal);
            int cand = 0;
            foreach (var lg in env.GetComponentsInChildren<LODGroup>(false))
            {
                if (lg.transform.IsChildOf(holder)) continue;
                var lods = lg.GetLODs(); if (lods.Length == 0 || lods.Length > 3) continue;
                if (lg.size * lg.transform.lossyScale.x > 2.6f) continue;   // 큰 것(나무·트럭·자판기 등)은 그대로
                bool blocked = false;   // 위로는 Env 바로 아래 묶음(구역 뿌리 — FBX Animator 가 붙음) 전까지만 본다
                for (var x = lg.transform.parent; x != null && x != env && x.parent != env; x = x.parent)
                    if (x.GetComponent<LODGroup>() != null || x.GetComponent<Light>() != null || x.GetComponents<MonoBehaviour>().Any(c => c != null)) { blocked = true; break; }
                if (blocked || lg.GetComponentsInChildren<MonoBehaviour>(true).Length > 0) continue;
                var lv = lods.Select(l => l.renderers.Where(r => r != null).ToArray()).ToArray();
                if (lv.Any(x => x.Length != 1) || lv.Any(x => !(x[0] is MeshRenderer) || x[0].GetComponent<MeshFilter>() == null || x[0].GetComponent<MeshFilter>().sharedMesh == null)) continue;
                var mats = lv.Select(x => x[0].sharedMaterial).Distinct().ToList();
                if (mats.Count != 1 || mats[0] == null || mats[0].shader == null || mats[0].shader.name.Contains("Leaf") || lv[0][0].GetComponent<MeshFilter>().sharedMesh.subMeshCount != 1) continue;
                if (!lv[0][0].enabled) continue;
                var (cx, cz) = Cell6(lg.transform.position, 12f);
                var sh = lv[0][0].shadowCastingMode;
                string key = $"{mats[0].name}|{AssetDatabase.GetAssetPath(mats[0])}|{(int)sh}|{cx}|{cz}";
                if (!groups.TryGetValue(key, out var g)) groups[key] = g = (mats[0], sh, new List<(LODGroup, Renderer[][])>());
                g.list.Add((lg, lv)); cand++;
            }
            int made = 0, removed = 0, idx = 0;
            foreach (var kv in groups)
            {
                var (mat, sh, list) = kv.Value;
                if (list.Count < 2) continue;
                var parts = kv.Key.Split('|');
                var go = new GameObject($"소품 {mat.name} {parts[3]}_{parts[4]}"); go.transform.SetParent(holder, false);
                var lodList = new List<LOD>(); Bounds? bb = null;
                for (int L = 0; L < 3; L++)
                {
                    var mb = new MB6();
                    foreach (var (lg, lv) in list)
                    {
                        var r = lv[Mathf.Min(L, lv.Length - 1)][0]; var mf = r.GetComponent<MeshFilter>(); var mesh = mf.sharedMesh;
                        var M = r.transform.localToWorldMatrix; var NM = M.inverse.transpose; bool flip = M.determinant < 0f;
                        var vs = mesh.vertices; var ns = mesh.normals; var uv = mesh.uv; int b0 = mb.V.Count;
                        for (int i = 0; i < vs.Length; i++) mb.Add(M.MultiplyPoint3x4(vs[i]), ns.Length > i ? NM.MultiplyVector(ns[i]).normalized : Vector3.up, uv.Length > i ? uv[i] : Vector2.zero, Vector2.zero, Color.white);
                        var tri = mesh.triangles;
                        for (int i = 0; i + 2 < tri.Length; i += 3) { if (flip) mb.Tri(b0 + tri[i], b0 + tri[i + 2], b0 + tri[i + 1]); else mb.Tri(b0 + tri[i], b0 + tri[i + 1], b0 + tri[i + 2]); }
                    }
                    var m = mb.Save($"pm6_{idx:000}_{San(mat.name)}_{parts[3]}_{parts[4]}_L{L}");
                    var o = Obj6("LOD" + L, go.transform, m, mat, L == 0 ? sh : ShadowCastingMode.Off);
                    bb = bb.HasValue ? bb : m.bounds;
                    lodList.Add(new LOD(0f, new Renderer[] { o.GetComponent<MeshRenderer>() }));
                }
                idx++;
                var lgNew = go.AddComponent<LODGroup>();
                float S = Mathf.Max(bb.Value.size.x, bb.Value.size.y, bb.Value.size.z);
                float H(float dist) => Mathf.Clamp(S / (dist * 0.8284f), 0.004f, 0.95f);
                float[] tr = { H(7f), H(16f), H(36f) };
                for (int L = 0; L < lodList.Count; L++) { var l = lodList[L]; l.screenRelativeTransitionHeight = Mathf.Min(tr[L], L > 0 ? lodList[L - 1].screenRelativeTransitionHeight - 0.001f : 1f); lodList[L] = l; }
                lgNew.SetLODs(lodList.ToArray()); lgNew.fadeMode = LODFadeMode.None; lgNew.RecalculateBounds();
                foreach (var (lg, lv) in list)
                {
                    var root = PrefabUtility.GetOutermostPrefabInstanceRoot(lg.gameObject);
                    if (root != null && root.transform.IsChildOf(env)) PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    foreach (var level in lv) foreach (var r in level) { var mf = r.GetComponent<MeshFilter>(); Object.DestroyImmediate(r); if (mf != null) Object.DestroyImmediate(mf); }
                    Object.DestroyImmediate(lg);
                    removed++;
                }
                made++;
            }
            if (holder.childCount == 0) Object.DestroyImmediate(holder.gameObject);
            return $"소품 합침(6차): 작은 소품 {removed}/{cand}개 → 칸 {made}개(LOD 3단)";
        }

        // ───────────────────────── 글자 굽기: 구역 TMP(간판 시트지·주소판·명판·정류장 안내 …)를 지금 아틀라스 사본 + 정적 메시로(글꼴 재질 × 48m 칸 → 그리기 1회)
        //   TMP 컴포넌트는 꺼 두고 글자(text)는 남긴다 — 글자 검사(SignAudit·시험)가 그대로 읽음. 동적 아틀라스가 빌드에서 다시 채워져도 사본은 그대로
        static readonly Dictionary<Texture, Texture2D> snapTex6 = new Dictionary<Texture, Texture2D>();
        static readonly Dictionary<Material, Material> snapMat6 = new Dictionary<Material, Material>();
        static Material SnapTextMat(Material m)
        {
            if (m == null) return null;
            if (snapMat6.TryGetValue(m, out var done)) return done;
            var src = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") as Texture2D : null;
            if (src == null || !src.isReadable) { snapMat6[m] = null; return null; }
            if (!snapTex6.TryGetValue(src, out var copy))
            {
                string tp = $"{Pass6Dir}/TextAtlas6_{San(src.name)}.asset";
                var c = new Texture2D(src.width, src.height, src.format, false) { name = "TextAtlas6_" + San(src.name), filterMode = src.filterMode, wrapMode = TextureWrapMode.Clamp, anisoLevel = 0 };
                c.LoadRawTextureData(src.GetRawTextureData()); c.Apply(false, false);
                var old = AssetDatabase.LoadAssetAtPath<Texture2D>(tp);
                if (old != null) { EditorUtility.CopySerialized(c, old); Object.DestroyImmediate(c); copy = old; EditorUtility.SetDirty(old); }
                else { AssetDatabase.CreateAsset(c, tp); copy = c; }
                snapTex6[src] = copy;
            }
            EnsureFolder(Pass6MatDir);
            string mp = $"{Pass6MatDir}/M_Text6_{San(m.name)}.mat";
            var sm = AssetDatabase.LoadAssetAtPath<Material>(mp);
            if (sm == null) { sm = new Material(m) { name = "M_Text6_" + San(m.name) }; AssetDatabase.CreateAsset(sm, mp); }
            if (sm.shader != m.shader) sm.shader = m.shader;
            sm.CopyPropertiesFromMaterial(m); sm.shaderKeywords = m.shaderKeywords; sm.renderQueue = m.renderQueue;
            sm.SetTexture("_MainTex", copy);
            EditorUtility.SetDirty(sm);
            snapMat6[m] = sm;
            return sm;
        }
        static string San(string s) => new string(s.Select(ch => char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_').ToArray());

        sealed class TextAcc
        {
            public readonly List<Vector3> V = new List<Vector3>(), N = new List<Vector3>(); public readonly List<Vector4> Tg = new List<Vector4>();
            public readonly List<Color32> C = new List<Color32>(); public readonly List<Vector4>[] UV = { new List<Vector4>(), new List<Vector4>(), new List<Vector4>(), new List<Vector4>() };
            public readonly List<int> T = new List<int>();
        }

        static string BakeText6(Transform env)
        {
            snapTex6.Clear(); snapMat6.Clear();
            var holder = new GameObject("글자 합침").transform; holder.SetParent(env, false);
            var groups = new SortedDictionary<string, (Material mat, TextAcc acc)>(StringComparer.Ordinal);
            int texts = 0, skipped = 0;
            var tmpUV = new List<Vector4>();
            foreach (var t in env.GetComponentsInChildren<TextMeshPro>(false))
            {
                if (!t.enabled || string.IsNullOrEmpty(t.text)) continue;
                t.ForceMeshUpdate(true, true);
                var parts = new List<(Mesh mesh, Material mat, Matrix4x4 M, Renderer r)>();
                var mr = t.GetComponent<MeshRenderer>();
                if (mr != null && t.mesh != null && t.mesh.vertexCount > 0) parts.Add((t.mesh, mr.sharedMaterial, t.transform.localToWorldMatrix, mr));
                foreach (var sub in t.GetComponentsInChildren<TMP_SubMesh>(false))
                {
                    var smr = sub.GetComponent<MeshRenderer>();
                    if (smr != null && sub.mesh != null && sub.mesh.vertexCount > 0) parts.Add((sub.mesh, smr.sharedMaterial, sub.transform.localToWorldMatrix, smr));
                }
                if (parts.Count == 0 || parts.Any(x => SnapTextMat(x.mat) == null)) { skipped++; continue; }
                foreach (var (mesh, mat, M, _) in parts)
                {
                    var sm = SnapTextMat(mat);
                    var (cx, cz) = Cell6(M.MultiplyPoint3x4(mesh.bounds.center), 48f);
                    string key = $"{sm.name}|{cx}|{cz}";
                    if (!groups.TryGetValue(key, out var g)) groups[key] = g = (sm, new TextAcc());
                    var acc = g.acc; int b0 = acc.V.Count;
                    var vs = mesh.vertices; var ns = mesh.normals; var tg = mesh.tangents; var cs = mesh.colors32;
                    for (int i = 0; i < vs.Length; i++)
                    {
                        acc.V.Add(M.MultiplyPoint3x4(vs[i]));
                        acc.N.Add(ns.Length > i ? M.MultiplyVector(ns[i]).normalized : -Vector3.forward);
                        if (tg.Length > i) { var d3 = M.MultiplyVector(new Vector3(tg[i].x, tg[i].y, tg[i].z)).normalized; acc.Tg.Add(new Vector4(d3.x, d3.y, d3.z, tg[i].w)); } else acc.Tg.Add(new Vector4(1, 0, 0, 1));
                        acc.C.Add(cs.Length > i ? cs[i] : new Color32(255, 255, 255, 255));
                    }
                    for (int k = 0; k < 4; k++)
                    {
                        tmpUV.Clear(); mesh.GetUVs(k, tmpUV);
                        for (int i = 0; i < vs.Length; i++) acc.UV[k].Add(tmpUV.Count > i ? tmpUV[i] : Vector4.zero);
                    }
                    foreach (int ix in mesh.triangles) acc.T.Add(b0 + ix);
                }
                foreach (var (_, _, _, r) in parts) r.enabled = false;
                t.enabled = false; texts++;
            }
            int made = 0;
            foreach (var kv in groups)
            {
                var (mat, acc) = kv.Value; var parts = kv.Key.Split('|');
                var m = Mesh6($"t6_{San(mat.name)}_{parts[1]}_{parts[2]}");
                if (acc.V.Count > 65000) m.indexFormat = IndexFormat.UInt32;
                m.SetVertices(acc.V); m.SetNormals(acc.N); m.SetTangents(acc.Tg); m.SetColors(acc.C);
                for (int k = 0; k < 4; k++) m.SetUVs(k, acc.UV[k]);
                m.SetTriangles(acc.T, 0); m.RecalculateBounds(); EditorUtility.SetDirty(m);
                Obj6($"글자 {mat.name} {parts[1]}_{parts[2]}", holder, m, mat, ShadowCastingMode.Off, false);
                made++;
            }
            return $"글자 굽기(6차): TMP {texts}개 → 메시 {made}개(못 구움 {skipped}) · 아틀라스 사본 {snapTex6.Count}";
        }

        // ═════════════════════════ 6차 묶음(AttachKit 끝에서)
        static string Pass6(Zone1Data d, Transform zoneRoot, Transform env, Dictionary<string, Dictionary<string, object>> objs, Func<string, GameObject> prefab)
        {
            var t0 = DateTime.Now;
            int ads = BusAds6(env);
            PlanPlaza7(d, zoneRoot);   // 7차: 둥근 흙 광장 자리를 먼저(풀·관목·난간이 비켜 감)
            string green = Green6(d, zoneRoot, env);
            string pools = Pools6(d, zoneRoot, env, objs);
            int rails = Rails6(d, env);
            string p7 = Pass7(d, zoneRoot, env, prefab);
            // 점검(합치기 전 — 면 이름이 남아 있을 때)
            var faces = SignAudit.Faces(env);
            var ov = SignAudit.Overlaps(faces);
            var oc = SignAudit.Occlusions(env, faces, 0.15f, out int checkedN);
            int miss = SignAudit.MissingGlyphs(out int texts, out string missDetail);
            foreach (var x in ov.Take(12)) Notes.Add("간판 겹침: " + x);
            foreach (var x in oc.Take(12)) Notes.Add("간판 가림: " + x);
            if (miss > 0) Notes.Add("글자 빠짐: " + missDetail);
            var gc = new GameObject("글자 확인"); gc.transform.SetParent(env, false); gc.AddComponent<GlyphCheck>();
            // 작은 것(대각 1m 미만: 화분·계량기·볼트·작은 상자)은 그림자 안 드리움 — 그림자 그리기 호출 ↓(셀 그늘은 큰 것만으로 충분)
            int noShadow = 0;
            foreach (var r in env.GetComponentsInChildren<MeshRenderer>(true))
                if (r.shadowCastingMode == ShadowCastingMode.On && r.bounds.size.magnitude < 1.0f) { r.shadowCastingMode = ShadowCastingMode.Off; noShadow++; }
            string merge = Merge6(env, zoneRoot);
            string propm = PropMergeOn ? PropMerge6(env) : "소품 합침 끔";   // 6차 실측: 그리기 −26 이지만 소품 메시가 인스턴스마다 복제돼 빌드 614MB → 1.73GB — 끔
            string baked = BakeText6(env);
            int pruned = Mesh6Prune();
            return $"6차: 정류장 광고 {ads}면 · {green} · {pools} · 계단 손잡이 {rails}곳 · 작은 것 그림자 끔 {noShadow} · 간판 점검 면 {faces.Count}(겹침 {ov.Count} · 가림 {oc.Count}/{checkedN}) · TMP {texts}개 빠진 글자 {miss} · {merge} · {propm} · {baked} · [{p7}] · 안 쓴 메시 {pruned} 지움 · {(DateTime.Now - t0).TotalSeconds:F1}초";
        }
    }
}
