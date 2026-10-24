// 행인1의 메인이벤트 — 맵 아트 패스 4차(2026-10-24): 카드 전용 수관 나무 · 탐색 카메라 비교안 사진
//   나무: Hunyuan 덩어리 수관('솜뭉치') 대신 코드로 짠 가지 뼈대 + 잎 덩어리 카드 다발(Clump_<종>.png 2×2 아틀라스, 힉스필드 3장 8.25cr).
//         카드 정점 법선 = 수관(덩어리) 둥근 법선 → 셀 2톤이 덩어리째로 갈리고, 먹선은 덩어리 가장자리에만(ToonLeafCard _Sphere·_Ink),
//         스텐실 1 로 화면 공간 외곽선이 수관 안쪽에 잔선을 긋지 않게. 크기(높이·폭)는 옛 Hunyuan 나무와 같게 맞춰 배치 코드는 그대로.
//   카메라: -executeMethod Haengin.EditorGame.EnvSetup.CameraAB -out <폴더> — 8곳 × [지금(거리 4.0·피치 8°·FOV 45·표적 1.40) ∣ 넓게(5.5·피치 3°·FOV 53·표적 1.15)]
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Haengin.EditorTools;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Haengin.EditorGame
{
    public static partial class EnvSetup
    {
        sealed class TreeSpec
        {
            public string Clump; public Color Bark; public float Base, Center, Rv; public int Clumps; public bool Pads, Bush; public float Wind = 0.07f; public Color Tint = Color.white;
        }
        static Color C(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
        static readonly Dictionary<string, TreeSpec> CardTrees = new Dictionary<string, TreeSpec>
        {
            ["zelkova_tree"] = new TreeSpec { Clump = "broad", Bark = C("#6A5646"), Base = 0.30f, Center = 0.63f, Rv = 0.30f, Clumps = 15 },
            ["plane_tree"] = new TreeSpec { Clump = "broad", Bark = C("#9A947F"), Base = 0.34f, Center = 0.65f, Rv = 0.30f, Clumps = 15 },
            ["cherry_tree"] = new TreeSpec { Clump = "cherry", Bark = C("#4D3933"), Base = 0.26f, Center = 0.60f, Rv = 0.30f, Clumps = 14 },
            ["pine_tree"] = new TreeSpec { Clump = "pine", Bark = C("#7A4B36"), Base = 0.50f, Center = 0.75f, Rv = 0.25f, Clumps = 6, Pads = true, Wind = 0.06f },
            ["shrub_bush"] = new TreeSpec { Clump = "broad", Bark = C("#4E4434"), Clumps = 6, Bush = true, Wind = 0.03f, Tint = new Color(0.74f, 0.83f, 0.72f) },
        };
        public static bool CardTreeReady(string tree) => CardTrees.TryGetValue(tree, out var sp) && File.Exists($"{TexDir}/Clump_{sp.Clump}.png");

        /// concept/art/env/veg/clump_<종>.png(2048², 투명 배경) → Art/Env/Textures/Clump_<종>.png(없을 때만 복사)
        static void CopyClumpTextures()
        {
            int n = 0;
            foreach (var k in new[] { "broad", "pine", "cherry" })
            {
                string src = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../concept/art/env/veg", $"clump_{k}.png"));
                string dst = $"{TexDir}/Clump_{k}.png";
                if (!File.Exists(src) || File.Exists(dst)) continue;
                File.Copy(src, dst); AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceSynchronousImport); n++;
            }
            if (n > 0) Notes.Add($"잎 덩어리 아틀라스 {n}장 들여옴(Clump_*)");
        }

        // ───────────────────────── 메시 짜기 도우미
        sealed class MB4
        {
            public readonly List<Vector3> V = new List<Vector3>(); public readonly List<Vector3> N = new List<Vector3>();
            public readonly List<Vector2> UV = new List<Vector2>(); public readonly List<int> T = new List<int>();
            public int Quads;
            /// 가운데 c, 오른쪽 r·위 u(반폭·반높이 포함), 아틀라스 칸(0~3), 법선 = nf(정점)
            public void Card(Vector3 c, Vector3 r, Vector3 u, int cell, Func<Vector3, Vector3> nf)
            {
                int b = V.Count;
                float ox = (cell % 2) * 0.5f, oy = (cell / 2) * 0.5f;
                var p = new[] { c - r - u, c + r - u, c + r + u, c - r + u };
                var t = new[] { new Vector2(ox, oy), new Vector2(ox + 0.5f, oy), new Vector2(ox + 0.5f, oy + 0.5f), new Vector2(ox, oy + 0.5f) };
                for (int i = 0; i < 4; i++) { V.Add(p[i]); N.Add(nf(p[i])); UV.Add(t[i]); }
                T.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
                Quads++;
            }
            /// 굵기가 줄어드는 관(줄기·가지): 점 pts, 반지름 rs, 옆면 수 sides
            public void Tube(IList<Vector3> pts, IList<float> rs, int sides)
            {
                Vector3 prevSide = Vector3.zero;
                int start = V.Count;
                for (int i = 0; i < pts.Count; i++)
                {
                    var tan = (pts[Mathf.Min(i + 1, pts.Count - 1)] - pts[Mathf.Max(i - 1, 0)]).normalized;
                    var side = i == 0 ? Vector3.Cross(tan, Mathf.Abs(tan.y) < 0.9f ? Vector3.up : Vector3.right).normalized : Vector3.ProjectOnPlane(prevSide, tan).normalized;
                    if (side.sqrMagnitude < 1e-6f) side = Vector3.Cross(tan, Vector3.right).normalized;
                    prevSide = side;
                    var other = Vector3.Cross(tan, side);
                    for (int k = 0; k <= sides; k++)
                    {
                        float a = k * Mathf.PI * 2f / sides;
                        var dir = side * Mathf.Cos(a) + other * Mathf.Sin(a);
                        V.Add(pts[i] + dir * rs[i]); N.Add(dir); UV.Add(new Vector2(k / (float)sides, i / (float)(pts.Count - 1)));
                    }
                }
                int row = sides + 1;
                for (int i = 0; i < pts.Count - 1; i++)
                    for (int k = 0; k < sides; k++)
                    {
                        int a = start + i * row + k, b2 = a + row;
                        T.AddRange(new[] { a, a + 1, b2, a + 1, b2 + 1, b2 });   // 바깥을 앞면으로(Unity 시계 방향)
                    }
            }
            public Mesh Save(string path)
            {
                var m = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (m == null) { m = new Mesh(); AssetDatabase.CreateAsset(m, path); }
                m.Clear(); m.name = Path.GetFileNameWithoutExtension(path);
                m.indexFormat = V.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                m.SetVertices(V); m.SetNormals(N); m.SetUVs(0, UV); m.SetTriangles(T, 0); m.RecalculateBounds(); m.RecalculateTangents();
                EditorUtility.SetDirty(m);
                return m;
            }
        }

        static Vector3 Rnd(System.Random r) { float a = (float)r.NextDouble() * Mathf.PI * 2f, y = (float)r.NextDouble() * 2f - 1f, s = Mathf.Sqrt(1f - y * y); return new Vector3(Mathf.Cos(a) * s, y, Mathf.Sin(a) * s); }
        static float Rf(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);

        /// 카드 전용 수관 나무 프리팹(LOD0 = 줄기·가지 + 잎 카드 전부, LOD1 = 줄기 + 덩어리당 큰 카드 3장). H·W = 옛 Hunyuan 나무 높이·수관 폭
        static string BuildCardTree(string tree, float H, float W)
        {
            var sp = CardTrees[tree];
            var rng = new System.Random(Hash(tree + "/card4"));
            float R = W * 0.46f, Rv = sp.Rv * H;
            var bark0 = new MB4(); var bark1 = new MB4(); var cards0 = new MB4(); var cards1 = new MB4();
            float r0 = Mathf.Clamp(H * 0.026f, 0.11f, 0.3f);
            var lean = new Vector3(Rf(rng, -1f, 1f), 0f, Rf(rng, -1f, 1f)).normalized * H * (sp.Pads ? 0.07f : 0.025f);
            // 줄기(소나무는 굽은 줄기)
            var trunk = new List<Vector3>(); var tr = new List<float>();
            int segs = 6;
            float trunkTop = sp.Pads ? H * 0.93f : H * sp.Center;
            for (int i = 0; i <= segs; i++)
            {
                float t = i / (float)segs;
                var p = new Vector3(0f, trunkTop * t, 0f) + lean * t * t;
                if (sp.Pads) p += new Vector3(Mathf.Sin(t * 5.1f + 0.7f), 0f, Mathf.Cos(t * 4.3f)) * H * 0.035f * t;
                trunk.Add(p); tr.Add(Mathf.Lerp(r0 * 1.15f, r0 * 0.35f, t));
            }
            if (!sp.Bush) { bark0.Tube(trunk, tr, 8); bark1.Tube(new[] { trunk[0], trunk[3], trunk[6] }, new[] { tr[0], tr[3], tr[6] }, 5); }
            Vector3 TrunkAt(float y) { float t = Mathf.Clamp01(y / trunkTop); int i = Mathf.Min(segs - 1, (int)(t * segs)); return Vector3.Lerp(trunk[i], trunk[i + 1], t * segs - i); }
            var up = Vector3.up;
            int clumps = 0;
            if (sp.Bush)
            {
                // 덤불: 땅에 붙은 낮은 돔 — 덩어리 4~6개(속 1 + 둘레), 줄기 없음
                var Cb = new Vector3(0f, H * 0.42f, 0f);
                float Rb = W * 0.5f;
                var list = new List<(Vector3 c, float r)> { (Cb + up * H * 0.1f, Mathf.Min(Rb, H) * 0.62f) };
                for (int k = 0; k < sp.Clumps - 1; k++)
                {
                    float th = k * Mathf.PI * 2f / (sp.Clumps - 1) + Rf(rng, -0.3f, 0.3f);
                    list.Add((new Vector3(Mathf.Cos(th) * Rb * 0.5f, H * Rf(rng, 0.32f, 0.5f), Mathf.Sin(th) * Rb * 0.5f), Mathf.Min(Rb, H) * Rf(rng, 0.45f, 0.58f)));
                }
                foreach (var (cc, rc) in list)
                {
                    var cc2 = cc; var rr = rc;
                    Vector3 Nb(Vector3 v) { var a = v - new Vector3(0f, H * 0.1f, 0f); a.y *= 1.4f; return (a.normalized * 0.7f + (v - cc2).normalized * 0.3f + up * 0.15f).normalized; }
                    float yaw0 = Rf(rng, 0f, 180f);
                    for (int j = 0; j < 3; j++)
                    {
                        var rot = Quaternion.Euler(Rf(rng, -8f, 8f), yaw0 + j * 60f, Rf(rng, -10f, 10f));
                        float sz = rc * Rf(rng, 1.05f, 1.2f);
                        cards0.Card(cc, rot * Vector3.right * sz, rot * Vector3.up * sz, rng.Next(4), Nb);
                        if (j < 2) cards1.Card(cc, rot * Vector3.right * sz * 1.1f, rot * Vector3.up * sz * 1.1f, rng.Next(4), Nb);
                    }
                    clumps++;
                }
            }
            else if (!sp.Pads)
            {
                var Cc = new Vector3(lean.x, H * sp.Center, lean.z);
                var radii = new Vector3(R, Rv, R);
                Vector3 Nf(Vector3 v, Vector3 cc, float rc)
                {
                    var a = Vector3.Scale(v - Cc, new Vector3(1f / R, 1f / Rv, 1f / R)); var b = (v - cc) / Mathf.Max(rc, 0.01f);
                    return (a.normalized * 0.62f + b.normalized * 0.38f + up * 0.12f).normalized;
                }
                int Nn = sp.Clumps; float golden = Mathf.PI * (3f - Mathf.Sqrt(5f));
                var centers = new List<(Vector3 c, float r)>();
                for (int k = 0; k < Nn; k++)
                {
                    float y = Mathf.Lerp(0.95f, -0.5f, (k + 0.5f) / Nn); float s = Mathf.Sqrt(1f - y * y); float th = k * golden + Rf(rng, -0.25f, 0.25f);
                    var dir = new Vector3(Mathf.Cos(th) * s, y, Mathf.Sin(th) * s);
                    var cc = Cc + Vector3.Scale(dir, radii) * Rf(rng, 0.58f, 0.74f);
                    centers.Add((cc, R * Rf(rng, 0.36f, 0.46f)));
                }
                centers.Add((Cc + new Vector3(0f, Rv * 0.15f, 0f), R * 0.5f));   // 속 채움
                centers.Add((Cc + new Vector3(R * 0.25f, -Rv * 0.2f, -R * 0.2f), R * 0.42f));
                foreach (var (cc, rc) in centers)
                {
                    // 가지: 줄기 → 덩어리(덩어리 속에서 끝남)
                    var from = TrunkAt(Mathf.Lerp(H * sp.Base, Cc.y, Rf(rng, 0.15f, 0.6f)));
                    var to = cc - (cc - from).normalized * rc * 0.35f;
                    var mid = Vector3.Lerp(from, to, 0.5f) + up * Rf(rng, 0.1f, 0.4f);
                    float rb = Mathf.Max(0.035f, r0 * Rf(rng, 0.3f, 0.45f));
                    if ((to - from).magnitude > 0.4f) bark0.Tube(new[] { from, mid, to }, new[] { rb, rb * 0.7f, rb * 0.35f }, 5);
                    // 잎 카드: 덩어리 가운데를 지나는 별 2장(90°) + 바깥으로 향한 겉 카드 3장(겹침 = 그리기 비용을 줄임)
                    float yaw0 = Rf(rng, 0f, 180f);
                    for (int j = 0; j < 2; j++)
                    {
                        var rot = Quaternion.Euler(Rf(rng, -10f, 10f), yaw0 + j * 90f, Rf(rng, -12f, 12f));
                        float sz = rc * Rf(rng, 1.05f, 1.25f);
                        var cc2 = cc; var rr = rc;
                        cards0.Card(cc, rot * Vector3.right * sz, rot * Vector3.up * sz, rng.Next(4), v => Nf(v, cc2, rr));
                        cards1.Card(cc, rot * Vector3.right * sz * 1.18f, rot * Vector3.up * sz * 1.18f, rng.Next(4), v => Nf(v, cc2, rr));
                    }
                    var outDir = cc - Cc; outDir.y *= 0.4f; outDir = outDir.sqrMagnitude > 1e-4f ? outDir.normalized : Vector3.forward;
                    for (int j = 0; j < 3; j++)
                    {
                        var d = (outDir + Rnd(rng) * 0.9f); d.y *= 0.35f; d = d.normalized;
                        var pos = cc + d * rc * Rf(rng, 0.35f, 0.55f) + up * Rf(rng, -0.2f, 0.25f) * rc;
                        var rot = Quaternion.LookRotation(-d, up) * Quaternion.Euler(0f, 0f, Rf(rng, -14f, 14f));
                        float sz = rc * Rf(rng, 0.8f, 1.0f);
                        var cc2 = cc; var rr = rc;
                        cards0.Card(pos, rot * Vector3.right * sz, rot * Vector3.up * sz, rng.Next(4), v => Nf(v, cc2, rr));
                        if (j == 0) cards1.Card(pos, rot * Vector3.right * sz * 1.2f, rot * Vector3.up * sz * 1.2f, rng.Next(4), v => Nf(v, cc2, rr));
                    }
                    clumps++;
                }
            }
            else
            {
                // 소나무: 층층이 넓적한 잎 판(가지 끝) — 판마다 세로 별 4장 + 위를 덮는 눕힌 판 1장
                int pads = sp.Clumps; float golden = Mathf.PI * (3f - Mathf.Sqrt(5f));
                for (int k = 0; k < pads; k++)
                {
                    float t = k / (float)(pads - 1);
                    float y = Mathf.Lerp(H * sp.Base, H * 0.95f, t);
                    float th = k * golden * 1.3f + Rf(rng, -0.3f, 0.3f);
                    var tp = TrunkAt(Mathf.Min(y, trunkTop));
                    float rd = (k == pads - 1) ? 0.2f * R : Mathf.Lerp(1.05f, 0.5f, t) * R * Rf(rng, 0.8f, 1.0f);
                    var pc = new Vector3(tp.x + Mathf.Cos(th) * rd, y + Rf(rng, -0.15f, 0.25f), tp.z + Mathf.Sin(th) * rd);
                    float pw = Mathf.Lerp(R * 1.05f, R * 0.75f, t) * Rf(rng, 0.85f, 1.1f) + 0.4f;
                    var from = TrunkAt(Mathf.Max(0.5f, y - Rf(rng, 0.4f, 0.9f)));
                    var mid = Vector3.Lerp(from, pc, 0.55f) + up * 0.25f;
                    float rb = Mathf.Max(0.04f, r0 * 0.42f);
                    if ((pc - from).magnitude > 0.4f) bark0.Tube(new[] { from, mid, pc }, new[] { rb, rb * 0.7f, rb * 0.4f }, 5);
                    var pcc = pc;
                    Vector3 Np(Vector3 v) { var a = v - pcc; a.y *= 2.4f; return (a.normalized * 0.6f + up * 0.45f).normalized; }
                    float yaw0 = Rf(rng, 0f, 90f);
                    for (int j = 0; j < 4; j++)
                    {
                        var rot = Quaternion.Euler(0f, yaw0 + j * 45f, Rf(rng, -6f, 6f));
                        cards0.Card(pc, rot * Vector3.right * pw * 0.5f, rot * Vector3.up * pw * 0.5f, rng.Next(4), Np);
                        if (j % 2 == 0) cards1.Card(pc, rot * Vector3.right * pw * 0.58f, rot * Vector3.up * pw * 0.58f, rng.Next(4), Np);
                    }
                    var rt = Quaternion.Euler(-80f, yaw0, 0f);
                    cards0.Card(pc + up * pw * 0.06f, rt * Vector3.right * pw * 0.42f, rt * Vector3.up * pw * 0.42f, rng.Next(4), Np);
                    clumps++;
                }
            }
            // 에셋·재질
            string mdir = PropDir + "/CardTrees"; EnsureFolder(mdir);
            var mB0 = bark0.Save($"{mdir}/{tree}_bark0.asset"); var mB1 = bark1.Save($"{mdir}/{tree}_bark1.asset");
            var mC0 = cards0.Save($"{mdir}/{tree}_crown0.asset"); var mC1 = cards1.Save($"{mdir}/{tree}_crown1.asset");
            Material barkMat = null;
            if (!sp.Bush) { barkMat = Leaf($"{PropMatDir}/M_V_bark_{tree}.mat", null, sp.Wind, Mathf.Max(1f, H)); barkMat.SetColor("_BaseColor", sp.Bark); EditorUtility.SetDirty(barkMat); }
            var sh = Shader.Find("Haengin/ToonLeafCard") ?? throw new Exception("Haengin/ToonLeafCard 셰이더 없음");
            string mp = $"{PropMatDir}/M_V_crown_{tree}.mat";
            var cm = AssetDatabase.LoadAssetAtPath<Material>(mp);
            if (cm == null) { cm = new Material(sh) { name = "M_V_crown_" + tree }; AssetDatabase.CreateAsset(cm, mp); }
            if (cm.shader != sh) cm.shader = sh;
            cm.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/Clump_{sp.Clump}.png"));
            cm.SetColor("_BaseColor", sp.Tint); cm.SetColor("_ShadeColor", new Color(0.80f, 0.80f, 0.84f));
            cm.SetFloat("_Step", 0.5f); cm.SetFloat("_Feather", 0.03f); cm.SetFloat("_WindAmp", sp.Wind); cm.SetFloat("_WindFreq", 1.2f); cm.SetFloat("_Height", Mathf.Max(1f, H));
            cm.SetFloat("_Cutoff", 0.38f); cm.SetFloat("_Sphere", 1f); cm.SetFloat("_Ink", 1f); cm.SetFloat("_InkWidth", 0.3f); cm.SetColor("_InkColor", new Color(0.102f, 0.078f, 0.09f)); cm.SetFloat("_StencilRef", 1f);
            cm.enableInstancing = true; EditorUtility.SetDirty(cm);
            // 프리팹
            var root = new GameObject("P_" + tree);
            var lg = root.AddComponent<LODGroup>();
            Renderer Part(string name, Mesh mesh, Material mat, bool shadows)
            {
                var go = new GameObject(name); go.transform.SetParent(root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
                r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off; r.receiveShadows = true;
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.OccludeeStatic);
                return r;
            }
            var l0 = sp.Bush ? new[] { Part("LOD0 수관", mC0, cm, true) } : new[] { Part("LOD0 줄기", mB0, barkMat, true), Part("LOD0 수관", mC0, cm, true) };
            var l1 = sp.Bush ? new[] { Part("LOD1 수관", mC1, cm, false) } : new[] { Part("LOD1 줄기", mB1, barkMat, false), Part("LOD1 수관", mC1, cm, false) };
            float size = Mathf.Max(H, W);
            float Hs(float dist) => Mathf.Clamp(size / (dist * 0.8284f), 0.002f, 0.95f);
            lg.SetLODs(sp.Bush ? new[] { new LOD(Hs(18f), l0), new LOD(Hs(45f), l1) } : new[] { new LOD(Hs(32f), l0), new LOD(0.008f, l1) });
            lg.fadeMode = LODFadeMode.None; lg.RecalculateBounds();
            GameObjectUtility.SetStaticEditorFlags(root, StaticEditorFlags.OccludeeStatic);
            PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/P_{tree}.prefab");
            Object.DestroyImmediate(root);
            int tri0 = (bark0.T.Count + cards0.T.Count) / 3, tri1 = (bark1.T.Count + cards1.T.Count) / 3;
            return $"{tree} 카드 수관 {H:F1}×{W:F1}m 덩어리 {clumps} · 카드 {cards0.Quads}/{cards1.Quads} · 삼각형 {tri0}/{tri1}";
        }

        // ═════════════════════════ 탐색 카메라 비교안(결정용, 기본값은 그대로): -out <폴더>
        public static void CameraAB()
        {
            int code = 0;
            try
            {
                string outDir = Arg("-out") ?? throw new ArgumentException("-out <폴더>");
                if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) throw new Exception("그래픽 장치 없음(-nographics 빼기)");
                Directory.CreateDirectory(outDir);
                ShaderUtil.allowAsyncCompilation = false;
                EditorSceneManager.OpenScene(Root + "/Scenes/Zone1.unity", OpenSceneMode.Single);
                var d = Zone1Data.Load();
                var cam = Camera.main ?? throw new Exception("Main Camera 없음");
                var day = Object.FindFirstObjectByType<DayLight>() ?? throw new Exception("DayLight 없음");
                DayLight.EditModeFilm = true;
                if (LightSpot.All.Count == 0) foreach (var s in Object.FindObjectsByType<LightSpot>(FindObjectsInactive.Include, FindObjectsSortMode.None)) LightSpot.All.Add(s);
                var sky = Object.FindFirstObjectByType<SkyCards>();
                var player = GameObject.Find("Player");
                foreach (var nm in new[] { "Zone1/Route", "Zone1/Spawn", "Zone1/Landmarks/arena", "Zone1/Landmarks/npc_spot", "Zone1/Shots", "Stages" })
                { var g = GameObject.Find(nm); if (g != null) g.SetActive(false); }
                foreach (var t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) if (!t.name.StartsWith("글자")) t.gameObject.SetActive(false);
                foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) c.gameObject.SetActive(false);
                var unpose = BatchTools.PoseAnimators(1.0f);
                var spots = PreviewSpots(d);
                var parts = new Dictionary<int, DayPart> { [1] = DayPart.Day, [2] = DayPart.Dusk, [3] = DayPart.Dawn, [4] = DayPart.Dawn, [5] = DayPart.Day, [6] = DayPart.Day, [7] = DayPart.Day, [8] = DayPart.Day };
                var log = new List<string>();
                // A = 지금 게임 기본(CamTuning: 거리 4.0 · 피치 8° · FOV 45 · 표적 1.40) / B = 넓게(거리 5.5 · 피치 3° · FOV 53 · 표적 1.15)
                var modes = new[] { ("A", 4.0f, 8f, 45f, 1.40f), ("B", 5.5f, 3f, 53f, 1.15f) };
                for (int i = 1; i <= 8; i++)
                {
                    var (name, feet, yaw, _, _, _) = spots[i - 1];
                    if (player != null) player.transform.SetPositionAndRotation(feet, Quaternion.Euler(0f, yaw, 0f));
                    day.Apply(parts[i], true);
                    foreach (var (tag, dist, pitch, fov, th) in modes)
                    {
                        var r = Quaternion.Euler(pitch, yaw, 0f);
                        var tgt = feet + Vector3.up * th;
                        var back = -(r * Vector3.forward);
                        Physics.SyncTransforms();
                        float dd = dist;
                        if (Physics.SphereCast(tgt, 0.1f, back, out var hit, dist, (1 << Layers.Ground) | (1 << Layers.Wall), QueryTriggerInteraction.Ignore)) dd = Mathf.Max(0.6f, hit.distance - 0.05f);
                        // 화면 구도: CamTuning.screen x −0.06 → 인물이 화면 왼쪽으로 조금(카메라를 오른쪽으로 비켜 봄)
                        var right = r * Vector3.right;
                        cam.transform.SetPositionAndRotation(tgt + back * dd, r);
                        cam.transform.position += right * (Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * dd * 0.06f * 16f / 9f);
                        cam.fieldOfView = fov; cam.nearClipPlane = 0.08f; cam.farClipPlane = 900f;
                        if (sky != null) { sky.Follow(cam); sky.Apply(parts[i]); }
                        string path = Path.Combine(outDir, $"ab_{i}_{tag}.png");
                        BatchTools.RenderToPng(cam, 1920, 1080, 2, path);
                        // 인물 화면 높이(발·머리 투영)
                        var f = cam.WorldToViewportPoint(feet); var hd = cam.WorldToViewportPoint(feet + Vector3.up * 1.74f);
                        log.Add($"ab_{i}_{tag}: {name} {parts[i]} 거리 {dd:F2}(요청 {dist}) 피치 {pitch} FOV {fov} 표적 {th} · 인물 화면 높이 {(hd.y - f.y) * 100f:F0}% · 발 {f.y * 100f:F0}%");
                    }
                }
                unpose?.Invoke();
                File.WriteAllLines(Path.Combine(outDir, "camera_ab.txt"), log, new System.Text.UTF8Encoding(true));
                Debug.Log($"{Tag} 카메라 비교 {log.Count}장 → {outDir}\n{string.Join("\n", log)}");
            }
            catch (Exception e) { Debug.LogError($"{Tag} CameraAB 실패: {e}"); code = 1; }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }
    }
}
