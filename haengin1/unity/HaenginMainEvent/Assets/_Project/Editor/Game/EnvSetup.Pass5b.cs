// 행인1의 메인이벤트 — 맵 아트 패스 5차(2026-10-25) ② 규칙 배치 간판(모서리 층별·창 시트지·입간판·원통·LED·어닝·셔터·혜화 MMA 입구·주소판) ·
//   ③ 거리 데코(편의점 앞 탁자·파라솔·냉동고·상자, 세워 둔 1톤 트럭·스쿠터, 반사경, 라바콘, 볼라드, 전봇대 안전띠·변압기, 평상, 벽화·현수막·가로등 배너) ·
//   ④ 표면(창 아래 빗물·녹물, 아스팔트 균열·땜질, 틈 풀, 계단 모서리 닳음, 이끼, 스티커 자국) · ⑤ 움직임(빨래 천·현수막·배너 = ToonCloth, 비둘기·고양이, 날벌레).
//   키트가 남긴 소켓(win_sheet·streak·addr_plate·gym_door·cloth)은 AttachKit 이 socks5 로 모아 넘긴다.
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
        static readonly List<(Transform t, Dictionary<string, object> inf)> socks5 = new List<(Transform, Dictionary<string, object>)>();
        static readonly HashSet<string> Sock5Kinds = new HashSet<string> { "win_sheet", "streak", "addr_plate", "gym_door", "cloth" };

        sealed class Bldg { public string Name, Block; public Vector3 C, Size; public float Yaw, Y0, Top; public int Floors; public Dictionary<string, object> Roles; }
        static Bldg FindBldg(Dictionary<string, Dictionary<string, object>> objs, string blockContains)
        {
            foreach (var kv in objs.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                if (S(kv.Value, "kind") != "building") continue;
                var inf = D(G(kv.Value, "info")); string blk = S(inf, "block") ?? "";
                if (!blk.Contains(blockContains)) continue;
                var meta = D(G(inf, "meta")); var c = V3(G(inf, "center")); var sz = V3(G(inf, "size"));
                return new Bldg { Name = kv.Key, Block = blk, C = c, Size = sz, Yaw = F(G(inf, "yaw")), Y0 = F(G(meta, "y0"), c.y - sz.y / 2), Top = F(G(meta, "top"), c.y + sz.y / 2), Floors = (int)F(G(inf, "floors"), 1), Roles = D(G(inf, "roles")) };
            }
            return null;
        }
        static Vector3 FaceN(string f) => f == "+x" ? Vector3.right : f == "-x" ? Vector3.left : f == "+z" ? Vector3.forward : Vector3.back;
        static string BldgAt(Dictionary<string, Dictionary<string, object>> objs, Vector3 p)
        {
            foreach (var kv in objs)
            {
                if (S(kv.Value, "kind") != "building") continue;
                var inf = D(G(kv.Value, "info")); var c = V3(G(inf, "center")); var sz = V3(G(inf, "size"));
                var l = Quaternion.Euler(0f, -F(G(inf, "yaw")), 0f) * (p - c);
                if (Mathf.Abs(l.x) < sz.x / 2 + 0.6f && Mathf.Abs(l.z) < sz.z / 2 + 0.6f) return S(inf, "block") ?? "";
            }
            return "";
        }

        static TMP_FontAsset FontBold => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root + "/Fonts/KR_Bold_SDF.asset");
        static TMP_FontAsset FontBrush => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root + "/Fonts/KR_Brush_SDF.asset") ?? FontBold;
        static TMP_FontAsset FontHand => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root + "/Fonts/KR_Hand_SDF.asset") ?? FontBold;

        static TextMeshPro Text3D(Transform parent, Vector3 pos, Vector3 n, string text, float w, float h, Color col, TMP_FontAsset font, string name = "글자")
        {
            var go = new GameObject(name); go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(-n, Vector3.up));
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.font = font; tmp.text = text; tmp.color = col; tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap; tmp.enableAutoSizing = true; tmp.fontSizeMin = 0.1f; tmp.fontSizeMax = 30f;
            tmp.rectTransform.sizeDelta = new Vector2(w, h); tmp.margin = Vector4.zero;
            var mr = go.GetComponent<MeshRenderer>(); if (mr != null) { mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false; }
            tmp.ForceMeshUpdate(true, true);
            return tmp;
        }

        // ═════════════════════════ ② 규칙 배치 간판
        static string Signs5Rules(Zone1Data d, Transform zoneRoot, Transform env, Dictionary<string, Dictionary<string, object>> objs, Func<string, GameObject> prefab)
        {
            if (!Signs5Ready) return "간판 그림 없음";
            var root = new GameObject("Signs5").transform; root.SetParent(env, false);
            int corners = 0, sheets = 0, aframes = 0, misc = 0, addr = 0;
            var main = d.Roads[0];
            float DistMain(Vector3 p) { float best = 1e9f; for (int i = 0; i < main.Points.Length - 1; i++) { var a = main.Points[i]; var b = main.Points[i + 1]; var ab = Flat(b - a); float t = Mathf.Clamp01(Vector3.Dot(Flat(p - a), ab) / Mathf.Max(ab.sqrMagnitude, 1e-4f)); best = Mathf.Min(best, Flat(a + (b - a) * t - p).magnitude); } return best; }
            Vector3? gymSignPos = null;
            // 혜화 MMA 입구(키트 gym_door 소켓) — 모서리 간판보다 먼저(겹침 피하기)
            foreach (var (t, inf) in socks5.Where(s => S(s.inf, "sock_kind") == "gym_door"))
            {
                var p = V3(G(inf, "pos")); var n = V3(G(inf, "normal")).normalized;
                gymSignPos = GymEntrance(root, p, n);
                misc++;
            }
            // 모서리 세로 층별 간판(3)
            foreach (var (blk, face) in new[] { ("후문상가 A동", "corner_a"), ("명륜3가 다세대 1", "corner_b"), ("명륜1가 방면 상가주택", "corner_c2") })
            {
                var b = FindBldg(objs, blk); if (b == null || !Faces5().ContainsKey(face)) continue;
                var rot = Quaternion.Euler(0f, b.Yaw, 0f);
                string ff = b.Roles.FirstOrDefault(kv => kv.Value?.ToString() == "front").Key ?? "-z";
                var nf = FaceN(ff); float hf = Mathf.Abs(nf.x) > 0.5f ? b.Size.x / 2 : b.Size.z / 2;
                Vector3 best = Vector3.zero; Vector3 bestDir = Vector3.zero; float bestD = 1e9f;
                foreach (var kv in b.Roles)
                {
                    if (kv.Value?.ToString() != "street") continue;
                    var ns = FaceN(kv.Key); if (Mathf.Abs(Vector3.Dot(ns, nf)) > 0.5f) continue;
                    float hs = Mathf.Abs(ns.x) > 0.5f ? b.Size.x / 2 : b.Size.z / 2;
                    var corner = b.C + rot * (nf * hf + ns * hs); corner.y = b.Y0;
                    float dm = DistMain(corner);
                    if (dm < bestD) { bestD = dm; best = corner; bestDir = (rot * (nf + ns)).normalized; }
                }
                if (bestD > 1e8f) continue;
                if (gymSignPos.HasValue && Flat(gymSignPos.Value - best).magnitude < 2.5f) continue;
                var f5 = Faces5()[face];
                float w = 0.52f, h = Mathf.Min(w / f5.Aspect, b.Top - b.Y0 - 3.9f);
                if (h < 1.2f) continue;
                var pos = best + bestDir * 0.5f + Vector3.up * (3.45f + h / 2);
                CornerSign(root, pos, bestDir, w, h, face, b.Name);
                corners++;
            }
            // 창 시트지(키트 win_sheet 소켓)
            var sheetT = new Dictionary<(string, int), (string a, string b, Color band, Color text, bool hand)>
            {
                [("후문상가 A동", 2)] = ("번개배달 사무실", "퀵 · 배달 대행", new Color(0.86f, 0.67f, 0.18f), new Color(0.15f, 0.18f, 0.32f), false),
                [("후문상가 A동", 3)] = ("한빛 수학교실", "수학 · 내신 · 수능", new Color(0.13f, 0.42f, 0.80f), Color.white, false),
                [("명륜3가 다세대 1", 2)] = ("명륜 독서실", "1인석 · 24시간", new Color(0.15f, 0.38f, 0.26f), Color.white, false),
                [("명륜3가 다세대 1", 3)] = ("솔빛 한의원", "침 · 뜸 · 추나", new Color(0.92f, 0.94f, 0.90f), new Color(0.12f, 0.42f, 0.25f), false),
                [("명륜3가 다세대 1", 4)] = ("별빛 코인노래방", "1곡 500원", new Color(0.43f, 0.20f, 0.52f), new Color(1f, 0.85f, 0.95f), false),
                [("명륜1가 방면 상가주택", 2)] = ("명륜 부동산", "원룸 · 전세 · 매매", new Color(0.90f, 0.45f, 0.15f), Color.white, false),
                [("명륜1가 방면 상가주택", 3)] = ("로그인 PC방", "고사양 · 24시간", new Color(0.10f, 0.15f, 0.30f), new Color(0.55f, 0.92f, 1f), false),
            };
            foreach (var (t, inf) in socks5.Where(s => S(s.inf, "sock_kind") == "win_sheet"))
            {
                var p = V3(G(inf, "pos")); var n = V3(G(inf, "normal")).normalized; var sz = L(G(inf, "size"));
                float w = sz != null ? F(sz[0]) : 1.2f, h = sz != null ? F(sz[1]) : 1.3f;
                int floor = int.TryParse((S(inf, "tenant") ?? "2F").TrimEnd('F'), out var fl) ? fl : 2;
                string blk = BldgAt(objs, p);
                var key = sheetT.Keys.FirstOrDefault(k => blk.Contains(k.Item1) && k.Item2 == floor);
                (string a, string b, Color band, Color text, bool hand) st = key.Item1 != null ? sheetT[key] : ("하숙 · 원룸 있음", "문의 02-012-1180", new Color(0.95f, 0.95f, 0.92f), new Color(0.15f, 0.20f, 0.35f), true);
                var g = new GameObject("창 시트지 " + st.a).transform; g.SetParent(root, false);
                float bandH = h * 0.24f;
                var bandM = Flat("M_Vinyl_" + Mathf.RoundToInt(st.band.r * 9) + Mathf.RoundToInt(st.band.g * 9) + Mathf.RoundToInt(st.band.b * 9), st.band);
                Box("비닐 띠", g, p + Vector3.up * (h / 2 - bandH / 2) + n * 0.004f, new Vector3(w, bandH, 0.006f), Quaternion.LookRotation(-n, Vector3.up), bandM);
                Text3D(g, p + Vector3.up * (h / 2 - bandH / 2) + n * 0.009f, n, st.a, w * 0.92f, bandH * 0.8f, st.text, st.hand ? FontHand : FontBold);
                Text3D(g, p - Vector3.up * (h * 0.18f) + n * 0.006f, n, st.b, w * 0.85f, h * 0.16f, Color.Lerp(st.band, Color.white, 0.55f), st.hand ? FontHand : FontBold, "글자 아래");
                sheets++;
            }
            // 입간판(A형) 4 · 원통 회전 입간판 · LED 띠 · 어닝 2 · 셔터 — 키트 간판 띠 자리 기준
            var signInfo = objs.Where(x => S(x.Value, "kind") == "sign").ToDictionary(x => x.Key, x => D(G(x.Value, "info")));
            (Vector3 c, Vector3 n, float w, float h)? Band(string sign) { if (!signInfo.TryGetValue(sign, out var inf)) return null; var sz = L(G(inf, "size_m")); return (V3(G(inf, "center")), V3(G(inf, "normal")).normalized, F(sz[0]), F(sz[1])); }
            void AF(string sign, string face, float side)
            {
                var bnd = Band(sign); if (bnd == null || !Faces5().ContainsKey(face)) return;
                var (c, n, w, h) = bnd.Value; var right = Vector3.Cross(Vector3.up, n).normalized;
                var feet = GroundAt(c + n * 1.15f + right * side * (w * 0.3f), 6f, 8f);
                AFrame(root, feet, n, face); aframes++;
            }
            AF("Sign_005", "aframe_gukbap", -1f); AF("Sign_006", "aframe_haru", 1f); AF("Sign_013", "aframe_bunsik", -1f); AF("Sign_009", "aframe_laundry", 1f);
            var gb = Band("Sign_005");
            if (gb != null)
            {
                var (c, n, w, h) = gb.Value; var right = Vector3.Cross(Vector3.up, n).normalized;
                CylSign(root, GroundAt(c + n * 0.8f + right * (w * 0.42f), 6f, 8f), n, "cyl_gukbap"); misc++;
                LedStrip(root, c - Vector3.up * (h / 2 + 0.32f) + n * 0.05f, n, Mathf.Min(2.4f, w * 0.7f), 0.2f); misc++;
            }
            foreach (var (sg, col) in new[] { ("Sign_009", new Color(0.20f, 0.45f, 0.30f)), ("Sign_010", new Color(0.25f, 0.33f, 0.58f)) })
            {
                var bnd = Band(sg); if (bnd == null) continue;
                var (c, n, w, h) = bnd.Value; Awning(root, c - Vector3.up * (h / 2 + 0.5f) - n * 0.1f, n, w, col, sg == "Sign_009" ? "빨랫줄 세탁" : "나사 철물"); misc++;
            }
            // 가게 안(유리 너머, 노을·밤에 켜짐): 간판 띠 아래 유리 면 앞
            foreach (var (sg, face) in new[] { ("Sign_002", "int_shop"), ("Sign_005", "int_food"), ("Sign_006", "int_conv"), ("Sign_007", "int_conv"), ("Sign_009", "int_shop"), ("Sign_010", "int_shop"), ("Sign_011", "int_shop"), ("Sign_013", "int_food") })
            {
                var bnd = Band(sg); if (bnd == null || !Faces5().ContainsKey(face)) continue;
                var (c, n, w, h) = bnd.Value;
                Interior(root, c, n, w, h, face, sg); misc++;
            }
            var jb = Band("Sign_012");
            if (jb != null) { var (c, n, w, h) = jb.Value; Shutter(root, c - Vector3.up * (h / 2 + 0.06f), n, w, "넘김 제본"); misc++; }
            // 주소판(키트 addr_plate 소켓)
            var rng = new System.Random(5);
            foreach (var (t, inf) in socks5.Where(s => S(s.inf, "sock_kind") == "addr_plate"))
            {
                var p = V3(G(inf, "pos")); var n = V3(G(inf, "normal")).normalized;
                var g = new GameObject("주소판").transform; g.SetParent(root, false);
                Box("판", g, p, new Vector3(0.36f, 0.16f, 0.015f), Quaternion.LookRotation(-n, Vector3.up), Flat("M_AddrBlue", new Color(0.13f, 0.28f, 0.55f)));
                string road = p.x > -175f ? "명륜1길" : "명륜3가길";
                Text3D(g, p + n * 0.009f + Vector3.up * 0.022f, n, road, 0.3f, 0.06f, Color.white, FontBold);
                Text3D(g, p + n * 0.009f - Vector3.up * 0.035f, n, (11 + rng.Next(70)).ToString(), 0.2f, 0.07f, Color.white, FontBold, "글자 번지");
                addr++;
            }
            return $"간판 5차: 키트 자리 {signs5}개 몸체·면 · 모서리 층별 {corners} · 창 시트지 {sheets} · 입간판 {aframes} · 원통·LED·어닝·셔터·체육관 {misc} · 주소판 {addr}";
        }

        /// 가게 안 판: 간판 띠(면 가운데 c — 벽에서 0.123) 아래, 유리(벽에서 −0.10) 바로 앞 0.015 에. 높이 = 땅 위 0.05 ~ 띠 아래 셔터 박스(0.42) 밑
        static void Interior(Transform root, Vector3 c, Vector3 n, float w, float h, string face, string key)
        {
            var wall = c - n * 0.123f; var glass = wall - n * 0.085f;
            var g = GroundAt(glass + n * 0.6f, 2f, 8f);
            float top = c.y - h / 2 - 0.47f, bot = g.y + 0.06f, H = top - bot;
            if (H < 1.2f) return;
            var f5 = Faces5()[face];
            var mat = SignMat(face, 1.0f, 0.1f, Hash(key) % 17, false);
            mat.SetFloat("_DayDim", 0.32f); mat.SetFloat("_Glass", 1f); EditorUtility.SetDirty(mat);
            var mesh = FaceMesh("int_" + key, w - 0.25f, H, f5.Aspect);
            MeshObj("가게 안 " + face, root, mesh, mat, new Vector3(glass.x, bot + H / 2, glass.z), Quaternion.LookRotation(-n, Vector3.up));
        }

        static void CornerSign(Transform root, Vector3 pos, Vector3 dd, float w, float h, string face, string bname)
        {
            var g = new GameObject("모서리 간판 " + face).transform; g.SetParent(root, false);
            var fn = Vector3.Cross(Vector3.up, dd).normalized;
            var rot = Quaternion.LookRotation(fn, Vector3.up);   // 로컬 x = dd(벽에서 바깥), z = 면 법선
            Box("상자", g, pos, new Vector3(w + 0.05f, h + 0.05f, 0.18f), rot, SignBackMat, true);
            Box("위 뚜껑", g, pos + Vector3.up * (h / 2 + 0.05f), new Vector3(w + 0.12f, 0.06f, 0.24f), rot, SignFrameMat);
            Box("아래 뚜껑", g, pos - Vector3.up * (h / 2 + 0.05f), new Vector3(w + 0.12f, 0.06f, 0.24f), rot, SignFrameMat);
            foreach (float sy in new[] { -0.35f, 0.35f })
                Box("벽 팔", g, pos - dd * (w / 2 + 0.25f) + Vector3.up * (h * sy), new Vector3(0.55f, 0.05f, 0.05f), rot, SignFrameMat);
            var f5 = Faces5()[face];
            var mat = SignMat(face, 0.9f, 0.4f, Hash(bname) % 13);
            var mesh = FaceMesh("corner_" + bname, w, h, f5.Aspect);
            MeshObj("면 A", g, mesh, mat, pos + fn * 0.093f, Quaternion.LookRotation(-fn, Vector3.up));
            MeshObj("면 B", g, mesh, mat, pos - fn * 0.093f, Quaternion.LookRotation(fn, Vector3.up));
            Glow(g, pos + fn * 0.11f, fn, w, h, face, f5.Glow, 0.25f);
            Glow(g, pos - fn * 0.11f, -fn, w, h, face, f5.Glow, 0.25f);
        }

        static void AFrame(Transform root, Vector3 feet, Vector3 n, string face)
        {
            var g = new GameObject("입간판 " + face).transform; g.SetParent(root, false);
            var f5 = Faces5()[face];
            float H = 0.95f, W = H * f5.Aspect, lean = 13f;
            var mat = SignMat(face, 0f, 0.35f, Hash(face) % 11);
            var right = Vector3.Cross(Vector3.up, n).normalized;
            var mesh = FaceMesh("aframe_" + face, W, H, f5.Aspect);
            foreach (float s in new[] { 1f, -1f })
            {
                var fdir = n * s;
                var rot = Quaternion.AngleAxis(-lean * s, right) * Quaternion.LookRotation(-fdir, Vector3.up);
                var c = feet + Vector3.up * (H / 2 * Mathf.Cos(lean * Mathf.Deg2Rad)) + fdir * (H / 2 * Mathf.Sin(lean * Mathf.Deg2Rad) + 0.02f);
                Box("판", g, c - (rot * Vector3.forward) * 0.012f, new Vector3(W, H, 0.018f), rot, SignBackMat, true);
                MeshObj("면", g, mesh, mat, c - (rot * Vector3.forward) * 0.023f, rot);
            }
        }

        static void CylSign(Transform root, Vector3 feet, Vector3 n, string face)
        {
            var g = new GameObject("원통 입간판").transform; g.SetParent(root, false);
            Box("받침", g, feet + Vector3.up * 0.04f, new Vector3(0.44f, 0.08f, 0.44f), Quaternion.identity, SignBackMat, true);
            var spin = new GameObject("돌아가는 통"); spin.transform.SetParent(g, false); spin.transform.position = feet + Vector3.up * 0.1f;
            var mat = SignMat(face, 0.7f, 0.3f, 3f);
            MeshObj("통", spin.transform, CylMesh("cyl_" + face, 0.2f, 0.92f), mat, spin.transform.position, Quaternion.identity, true);
            var cap = GameObject.CreatePrimitive(PrimitiveType.Sphere); Object.DestroyImmediate(cap.GetComponent<Collider>());
            cap.transform.SetParent(spin.transform, false); cap.transform.position = feet + Vector3.up * 1.02f; cap.transform.localScale = new Vector3(0.4f, 0.12f, 0.4f);
            cap.GetComponent<MeshRenderer>().sharedMaterial = Flat("M_SignWhite", new Color(0.93f, 0.92f, 0.88f));
            var idle = spin.AddComponent<AmbientIdle>(); idle.Kind = AmbientIdle.Mode.Spin; idle.Speed = 40f;
            Glow(g, feet + Vector3.up * 0.56f + n * 0.22f, n, 0.42f, 0.95f, face, new Color(1f, 0.55f, 0.45f), 0.25f);
        }

        static void LedStrip(Transform root, Vector3 c, Vector3 n, float w, float h)
        {
            var g = new GameObject("LED 띠").transform; g.SetParent(root, false);
            var rot = Quaternion.LookRotation(-n, Vector3.up);
            Box("틀", g, c - n * 0.025f, new Vector3(w + 0.05f, h + 0.05f, 0.04f), rot, SignBoltMat, true);
            var mat = SignMat("led_strip", 0f, 0.1f, 1f, true);
            var m = SubMesh("led_strip_quad");
            var v = new List<Vector3> { new Vector3(-w / 2, -h / 2, 0), new Vector3(w / 2, -h / 2, 0), new Vector3(-w / 2, h / 2, 0), new Vector3(w / 2, h / 2, 0) };
            float span = w / (h * Faces5()["led_strip"].Aspect);   // 그림 한 장이 띠 몇 개 폭인지 → uv 를 그만큼(반복)
            m.Clear(); m.SetVertices(v); m.SetUVs(0, new List<Vector2> { new Vector2(0, 0), new Vector2(span, 0), new Vector2(0, 1), new Vector2(span, 1) });
            m.SetUVs(1, new List<Vector2> { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) });
            m.SetTriangles(new[] { 0, 3, 1, 3, 0, 2 }, 0); m.SetNormals(Enumerable.Repeat(Vector3.back, 4).ToList()); m.RecalculateBounds(); EditorUtility.SetDirty(m);
            mat.SetVector("_LedGrid", new Vector4((w / 0.012f) / Mathf.Max(span, 0.01f), h / 0.012f, 0, 0)); EditorUtility.SetDirty(mat);
            MeshObj("LED", g, m, mat, c, rot);
            Glow(g, c + n * 0.02f, n, w, h, "led", new Color(1f, 0.35f, 0.15f), 0.35f);
        }

        static Texture2D StripeTex(string name, Color a, Color b, int stripes, bool horizontal)
        {
            EnsureFolder(Sign5Dir + "/Gen");
            string path = $"{Sign5Dir}/Gen/{name}.png";
            if (!File.Exists(path))
            {
                var t = new Texture2D(256, 64, TextureFormat.RGBA32, false);
                for (int x = 0; x < 256; x++) for (int y = 0; y < 64; y++) t.SetPixel(x, y, horizontal ? (y < 22 ? b : y < 26 ? b * 0.85f : a) : (((x * stripes / 256) % 2 == 0) ? a : b));
                t.Apply(); File.WriteAllBytes(path, t.EncodeToPNG()); Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// 어닝: 띠 아래 벽에서 비스듬히 나온 줄무늬 천(앞단 늘어진 띠 + 인쇄 상호) — 양면 UTS, 그림자 양면
        static void Awning(Transform root, Vector3 top, Vector3 n, float w, Color col, string text)
        {
            var g = new GameObject("어닝 " + text).transform; g.SetParent(root, false);
            var tex = StripeTex("Awning_" + Mathf.Abs(text.GetHashCode() % 99991), col, new Color(0.95f, 0.94f, 0.90f), Mathf.Max(6, Mathf.RoundToInt(w / 0.32f)), false);
            var mat = Toon($"{Sign5MatDir}/M_Awning_{Mathf.Abs(text.GetHashCode() % 99991)}.mat", tex, Vector2.one, Vector2.zero, Color.white);
            mat.SetFloat("_CullMode", 0f); EditorUtility.SetDirty(mat);
            float depth = 0.95f, drop = 0.42f;
            var right = Vector3.Cross(Vector3.up, n).normalized;
            var front = top + n * depth - Vector3.up * drop;
            var m = SubMesh("awning_" + Mathf.Abs(text.GetHashCode() % 99991));
            var v = new List<Vector3> { top - right * w / 2, top + right * w / 2, front - right * w / 2, front + right * w / 2 };
            var ctr = (top + front) / 2; for (int i = 0; i < 4; i++) v[i] -= ctr;
            var nn = Vector3.Cross(front - top, right).normalized; if (nn.y < 0) nn = -nn;
            m.Clear(); m.SetVertices(v); m.SetUVs(0, new List<Vector2> { new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 0), new Vector2(1, 0) });
            m.SetNormals(Enumerable.Repeat(nn, 4).ToList()); m.SetTriangles(new[] { 0, 1, 2, 2, 1, 3 }, 0); m.RecalculateBounds(); m.RecalculateTangents(); EditorUtility.SetDirty(m);
            MeshObj("천", g, m, mat, ctr, Quaternion.identity, true).GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.TwoSided;
            var val = Quad("앞단", g, front - Vector3.up * 0.13f, n, w, 0.26f, mat, 0f);
            val.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.TwoSided;
            Box("앞 막대", g, front, new Vector3(w + 0.04f, 0.035f, 0.035f), Quaternion.LookRotation(-n, Vector3.up), SignFrameMat);
            foreach (float sx in new[] { -0.5f, 0.5f }) Box("팔", g, ctr + right * (w * sx) - nn * 0.03f, new Vector3(0.025f, 0.025f, (front - top).magnitude), Quaternion.LookRotation((front - top).normalized, Vector3.up), SignFrameMat);
            Text3D(g, front - Vector3.up * 0.13f + n * 0.006f, n, text, w * 0.6f, 0.17f, new Color(0.98f, 0.97f, 0.93f), FontBold);
        }

        /// 셔터(문 닫은 가게): 골판 줄무늬 + 페인트 상호 + 임대 종이
        static void Shutter(Transform root, Vector3 top, Vector3 n, float w, string text)
        {
            var g = new GameObject("셔터 " + text).transform; g.SetParent(root, false);
            var feet = GroundAt(top + n * 0.15f, 2f, 8f); float H = top.y - feet.y - 0.05f;
            if (H < 1.5f) return;
            var tex = StripeTex("Shutter", new Color(0.66f, 0.68f, 0.70f), new Color(0.50f, 0.52f, 0.55f), 1, true);
            var mat = Toon($"{Sign5MatDir}/M_Shutter.mat", tex, new Vector2(1f, H / 0.085f), Vector2.zero, Color.white);
            var c = new Vector3(top.x, feet.y + H / 2 + 0.02f, top.z) + n * 0.1f;
            var rot = Quaternion.LookRotation(-n, Vector3.up);
            Box("셔터", g, c, new Vector3(w, H, 0.03f), rot, mat, true);
            Box("셔터 박스", g, new Vector3(top.x, top.y - 0.05f, top.z) + n * 0.12f, new Vector3(w + 0.06f, 0.3f, 0.22f), rot, SignBackMat, true);
            Text3D(g, c + n * 0.02f + Vector3.up * (H * 0.12f), n, text, w * 0.6f, H * 0.18f, new Color(0.55f, 0.16f, 0.13f), FontBrush);
            Text3D(g, c + n * 0.02f - Vector3.up * (H * 0.08f), n, "휴업 중", w * 0.3f, H * 0.1f, new Color(0.55f, 0.16f, 0.13f), FontHand, "글자 휴업");
            var paper = Box("임대 종이", g, c + n * 0.02f - Vector3.up * (H * 0.3f) + Vector3.Cross(Vector3.up, n).normalized * (w * 0.25f), new Vector3(0.42f, 0.56f, 0.004f), rot, Flat("M_Paper", new Color(0.95f, 0.94f, 0.88f)));
            Text3D(g, paper.transform.position + n * 0.004f, n, "임대 문의\n02-012-6620", 0.36f, 0.4f, new Color(0.15f, 0.15f, 0.18f), FontHand, "글자 임대");
        }

        /// 혜화 MMA 1층 계단 출입구: 문틀·어두운 계단 안쪽·작은 처마·입구 판(벽) · 지하 안내 돌출 간판(문 옆, 2.6~4.6m)
        static Vector3 GymEntrance(Transform root, Vector3 foot, Vector3 n)
        {
            var g = new GameObject("혜화 MMA 입구").transform; g.SetParent(root, false);
            var rot = Quaternion.LookRotation(-n, Vector3.up); var right = Vector3.Cross(Vector3.up, n).normalized;
            float W = 1.1f, H = 2.25f;
            var c = foot + Vector3.up * (H / 2);
            Box("안쪽(어둠)", g, c + n * 0.004f, new Vector3(W, H, 0.005f), rot, Flat("M_Doorway_Dark", new Color(0.07f, 0.07f, 0.08f)));
            for (int k = 0; k < 4; k++)   // 내려가는 계단 끝(밝은 줄) 몇 개 — 문 안쪽에서 보이게
                Box("계단 끝 " + k, g, foot + n * 0.006f + Vector3.up * (0.06f + 0.16f * k) , new Vector3(W - 0.1f, 0.025f, 0.004f), rot, Flat("M_StepEdge", new Color(0.32f, 0.31f, 0.30f)));
            var fm = Flat("M_DoorFrame_Dark", new Color(0.22f, 0.23f, 0.25f));
            Box("문틀 왼", g, c - right * (W / 2 + 0.05f) + n * 0.03f, new Vector3(0.1f, H + 0.1f, 0.08f), rot, fm, true);
            Box("문틀 오른", g, c + right * (W / 2 + 0.05f) + n * 0.03f, new Vector3(0.1f, H + 0.1f, 0.08f), rot, fm, true);
            Box("문틀 위", g, c + Vector3.up * (H / 2 + 0.05f) + n * 0.03f, new Vector3(W + 0.2f, 0.1f, 0.08f), rot, fm, true);
            Box("처마", g, c + Vector3.up * (H / 2 + 0.2f) + n * 0.32f, new Vector3(W + 0.6f, 0.06f, 0.65f), rot, Flat("M_SignWhite", new Color(0.93f, 0.92f, 0.88f)), true);
            // 입구 판(벽, 문 옆)
            if (Faces5().TryGetValue("gym_plate", out var pf))
            {
                float pw = 0.62f, ph = pw / pf.Aspect;
                var pc = foot + Vector3.up * 1.55f + right * (W / 2 + 0.55f);
                Box("판 몸", g, pc - n * 0.0f + n * 0.012f, new Vector3(pw + 0.03f, ph + 0.03f, 0.025f), rot, SignBoltMat, true);
                MeshObj("면", g, FaceMesh("gym_plate", pw, ph, pf.Aspect), SignMat("gym_plate", 0.5f, 0.3f, 9f), pc + n * 0.027f, rot);
            }
            // 지하 안내 돌출 간판(문 반대쪽 옆, 벽에서 0.15m, 판 두께 방향 = right)
            Vector3 sp = foot;
            if (Faces5().TryGetValue("proj_gym", out var gf))
            {
                float h = 2.0f, reach = 0.5f;
                sp = foot - right * (W / 2 + 0.35f) + n * (0.15f + reach / 2) + Vector3.up * (2.65f + h / 2);
                var prot = Quaternion.LookRotation(right, Vector3.up);
                Box("상자", g, sp, new Vector3(reach + 0.05f, h + 0.05f, 0.2f), prot, SignBackMat, true);
                Box("위 뚜껑", g, sp + Vector3.up * (h / 2 + 0.05f), new Vector3(reach + 0.1f, 0.06f, 0.26f), prot, SignFrameMat);
                Box("아래 뚜껑", g, sp - Vector3.up * (h / 2 + 0.05f), new Vector3(reach + 0.1f, 0.06f, 0.26f), prot, SignFrameMat);
                foreach (float sy in new[] { -0.4f, 0.4f }) Box("벽 팔", g, sp - n * (reach / 2 + 0.08f) + Vector3.up * (h * sy), new Vector3(0.05f, 0.05f, 0.2f), prot, SignFrameMat);
                var mat = SignMat("proj_gym", 1f, 0.3f, 4f);
                var mesh = FaceMesh("proj_gym", reach, h, gf.Aspect);
                MeshObj("면 A", g, mesh, mat, sp + right * 0.103f, Quaternion.LookRotation(-right, Vector3.up));
                MeshObj("면 B", g, mesh, mat, sp - right * 0.103f, Quaternion.LookRotation(right, Vector3.up));
                Glow(g, sp + right * 0.12f, right, reach, h, "proj_gym", gf.Glow, 0.3f);
                Glow(g, sp - right * 0.12f, -right, reach, h, "proj_gym", gf.Glow, 0.3f);
            }
            // 문 위 작은 채널 문자
            Text3D(g, c + Vector3.up * (H / 2 + 0.45f) + n * 0.04f, n, "혜화 MMA", W + 0.4f, 0.28f, new Color(0.85f, 0.18f, 0.16f), FontBrush);
            return sp;
        }
    }
}
