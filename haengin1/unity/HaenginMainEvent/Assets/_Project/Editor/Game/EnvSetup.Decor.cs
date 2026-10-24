// 행인1의 메인이벤트 — 맵 아트 패스 2차(2026-10-24): 바닥 디테일(연석·횡단보도·정지선·맨홀·배수구) · 얼룩/낙서/포스터 판 · 골목 잡동사니 규칙 배치 ·
// 남은 회색 상자(가로등·표지판·게시판·차단 펜스) · 밤 보안등 1개 · 식생(나무·관목·담쟁이·풀 — Hunyuan 소품 8종, ToonLeaf 바람 셰이더)
// EnvSetup.AttachKit 끝에서 Decor()·Vegetation() 으로 붙는다. 모두 결정적(이름 해시 시드).
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
        public const string DecalMatDir = MatDir + "/Decals";
        static readonly string[] VegNames = { "pine_tree", "cherry_tree", "zelkova_tree", "shrub_bush", "ivy_patch", "potted_plant_a", "potted_plant_b", "grass_tuft" };
        static bool IsVeg(string name) => Array.IndexOf(VegNames, name) >= 0;

        static int Hash(string s) { unchecked { int h = 17; foreach (char c in s) h = h * 31 + c; return h; } }
        static Vector3 GroundAt(Vector3 p, float up = 4f, float down = 12f)
        {
            if (Physics.Raycast(p + Vector3.up * up, Vector3.down, out var h, up + down, 1 << Layers.Ground, QueryTriggerInteraction.Ignore)) p.y = h.point.y;
            return p;
        }
        static Material Flat(string name, Color c) => Toon($"{MatDir}/{name}.mat", null, Vector2.one, Vector2.zero, c);

        /// 식생 재질(ToonLeaf — 바람 흔들림). amp = 흔들림 m, height = 흔들림이 최대가 되는 높이
        static Material Leaf(string path, Texture tex, float amp, float height)
        {
            var sh = Shader.Find("Haengin/ToonLeaf") ?? throw new Exception("Haengin/ToonLeaf 셰이더 없음");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(sh) { name = Path.GetFileNameWithoutExtension(path) }; AssetDatabase.CreateAsset(m, path); }
            if (m.shader != sh) m.shader = sh;
            m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", Color.white); m.SetColor("_ShadeColor", ShadeMul);
            m.SetFloat("_Step", 0.5f); m.SetFloat("_Feather", 0.04f); m.SetFloat("_WindAmp", amp); m.SetFloat("_WindFreq", 1.1f); m.SetFloat("_Height", height);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// 데칼 판 재질(아틀라스 칸) — M_Decal_Atlas 의 grime/graffiti, M_Sign_Atlas 의 poster
        static Material DecalMat(string atlasTex, string key, float[] rc, Color tint, bool twoSided = false)
        {
            EnsureFolder(DecalMatDir);
            var sh = Shader.Find("Haengin/DecalQuad") ?? throw new Exception("Haengin/DecalQuad 셰이더 없음");
            string path = $"{DecalMatDir}/M_D_{key}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(sh) { name = "M_D_" + key }; AssetDatabase.CreateAsset(m, path); }
            if (m.shader != sh) m.shader = sh;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{atlasTex}.png");
            m.SetTexture("_MainTex", tex);
            m.SetTextureScale("_MainTex", new Vector2(rc[2] / 2048f, rc[3] / 2048f));
            m.SetTextureOffset("_MainTex", new Vector2(rc[0] / 2048f, 1f - (rc[1] + rc[3]) / 2048f));
            m.SetColor("_Tint", tint); m.SetFloat("_Cull", twoSided ? 0f : 2f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Dictionary<string, float[]> DecalRects()
        {
            var r = new Dictionary<string, float[]>();
            string p = "concept/art/env/decals/decal_atlas.json";
            string full = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../", p));
            if (!File.Exists(full)) { Notes.Add("decal_atlas.json 없음 — 얼룩·낙서 판 건너뜀"); return r; }
            var j = D(MiniJson.Parse(File.ReadAllText(full, System.Text.Encoding.UTF8)));
            foreach (var kv in D(j["rects"])) { var l = L(kv.Value); r[kv.Key] = new[] { F(l[0]), F(l[1]), F(l[2]), F(l[3]) }; }
            return r;
        }

        /// 판(쿼드) 하나 — 법선 n 쪽에서 보임, 바닥·벽에서 off 만큼 띄움
        static GameObject Quad(string name, Transform parent, Vector3 pos, Vector3 n, float w, float h, Material m, float off = 0.015f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos + n * off;
            var up = Mathf.Abs(n.y) > 0.9f ? Vector3.forward : Vector3.up;
            go.transform.rotation = Quaternion.LookRotation(-n, up);
            go.transform.localScale = new Vector3(w, h, 1f);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = m; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Quaternion rot, Material m, bool shadows = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(center, rot);
            go.transform.localScale = size;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = m; r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        /// 폴리라인 위 호 길이 s 의 점과 방향
        static (Vector3 p, Vector3 dir) Along(Vector3[] pts, float s)
        {
            for (int i = 0; i < pts.Length - 1; i++)
            {
                var seg = pts[i + 1] - pts[i]; float L = Flat(seg).magnitude;
                if (s <= L || i == pts.Length - 2) { float t = Mathf.Clamp01(s / Mathf.Max(L, 1e-4f)); return (Vector3.Lerp(pts[i], pts[i + 1], t), Flat(seg).normalized); }
                s -= L;
            }
            return (pts[pts.Length - 1], Flat(pts[pts.Length - 1] - pts[pts.Length - 2]).normalized);
        }
        static float Length(Vector3[] pts) { float t = 0f; for (int i = 0; i < pts.Length - 1; i++) t += Flat(pts[i + 1] - pts[i]).magnitude; return t; }

        static Vector3 ToRoadDir(Zone1Data d, Vector3 p)
        {
            float best = float.MaxValue; Vector3 bp = p + Vector3.forward;
            foreach (var r in d.Roads)
                for (int i = 0; i < r.Points.Length - 1; i++)
                {
                    Vector3 a = r.Points[i], b = r.Points[i + 1]; var ab = Flat(b - a); float L2 = ab.sqrMagnitude;
                    float t = L2 > 1e-4f ? Mathf.Clamp01(Vector3.Dot(Flat(p - a), ab) / L2) : 0f;
                    var q = a + (b - a) * t; float dist = Flat(q - p).sqrMagnitude;
                    if (dist < best) { best = dist; bp = q; }
                }
            var dir = Flat(bp - p); return dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.forward;
        }

        static GameObject PutProp(Func<string, GameObject> prefab, string prop, Transform parent, Vector3 pos, float yawDeg, string name = null, Vector3? scale = null)
        {
            var pf = prefab(prop); if (pf == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent);
            go.name = name ?? ("소품 " + prop);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yawDeg, 0f));
            if (scale.HasValue) go.transform.localScale = scale.Value;
            return go;
        }

        // ═════════════════════════ 바닥·벽 디테일 + 잡동사니 + 남은 회색 상자
        static string Decor(Zone1Data d, Transform zoneRoot, Transform env, Dictionary<string, Dictionary<string, object>> objs, Func<string, GameObject> prefab)
        {
            var root = new GameObject("Decor").transform; root.SetParent(env, false);
            var ground = new GameObject("바닥").transform; ground.SetParent(root, false);
            var walls = new GameObject("벽").transform; walls.SetParent(root, false);
            var clutter = new GameObject("잡동사니").transform; clutter.SetParent(root, false);
            var concrete = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/M_Concrete.mat");
            var paint = Flat("M_Paint_White", new Color(0.93f, 0.92f, 0.88f));
            var iron = Flat("M_Iron_Dark", new Color(0.30f, 0.30f, 0.31f));
            int curbs = 0, marks = 0, decals = 0, posters = 0, items = 0, swapped = 0;

            // 1) 연석: 인도 양 가장자리(높이 0.05 — 걷기엔 영향 없음, 콜라이더 없음)
            foreach (var r in d.Roads.Where(x => x.Kind == "sidewalk"))
                for (int i = 0; i < r.Points.Length - 1; i++)
                {
                    Vector3 a = r.Points[i], b = r.Points[i + 1];
                    var dir = (b - a); float len = dir.magnitude; if (len < 0.3f) continue;
                    var side = Vector3.Cross(Vector3.up, Flat(dir).normalized).normalized;
                    foreach (float sg in new[] { 1f, -1f })
                    {
                        var c = (a + b) * 0.5f + side * sg * (r.Width * 0.5f - 0.07f) + Vector3.up * 0.03f;
                        Box($"연석 {r.Name} {i}{(sg > 0 ? "R" : "L")}", ground, c, new Vector3(0.14f, 0.07f, len), Quaternion.LookRotation(dir.normalized, Vector3.up), concrete);
                        curbs++;
                    }
                }
            // 2) 횡단보도(후문 앞 성균관로) + 정지선(골목·진입로가 큰길과 만나는 곳) + 맨홀(큰길 40m 마다) + 빗물받이(인도 가장자리 20m 마다)
            var main = d.Roads[0];
            {
                var (cp, cdir) = Along(main.Points, 0f); float bestD = float.MaxValue;
                for (float s = 0f; s < Length(main.Points); s += 0.5f) { var (p, dr) = Along(main.Points, s); float dd = Mathf.Abs(p.x + 197.5f); if (dd < bestD) { bestD = dd; cp = p; cdir = dr; } }
                var side = Vector3.Cross(Vector3.up, cdir).normalized;
                for (int k = 0; k < 7; k++)
                {
                    float off = (k - 3) * 0.95f;
                    Box($"횡단보도 {k}", ground, GroundAt(cp + side * off) + Vector3.up * 0.012f, new Vector3(0.45f, 0.01f, main.Width - 0.6f), Quaternion.LookRotation(side, Vector3.up), paint);
                    marks++;
                }
                foreach (var r in d.Roads.Where(x => x.Kind == "alley" || x.Kind == "driveway"))
                {
                    foreach (var end in new[] { r.Points[0], r.Points[r.Points.Length - 1] })
                    {
                        float dmin = float.MaxValue; Vector3 q = end;
                        for (int i = 0; i < main.Points.Length - 1; i++)
                        {
                            Vector3 a = main.Points[i], b = main.Points[i + 1]; var ab = Flat(b - a); float t = Mathf.Clamp01(Vector3.Dot(Flat(end - a), ab) / Mathf.Max(ab.sqrMagnitude, 1e-4f));
                            var pp = a + (b - a) * t; float dd = Flat(pp - end).magnitude; if (dd < dmin) { dmin = dd; q = pp; }
                        }
                        if (dmin > main.Width * 0.5f + 2.5f) continue;
                        var inward = Flat(end - q).normalized; var (_, rd) = Along(r.Points, end == r.Points[0] ? 0.3f : Length(r.Points) - 0.3f);
                        var pos = GroundAt(end + inward * 1.2f) + Vector3.up * 0.012f;
                        Box($"정지선 {r.Name}", ground, pos, new Vector3(r.Width - 0.4f, 0.01f, 0.35f), Quaternion.LookRotation(inward, Vector3.up), paint);
                        marks++;
                    }
                }
                float L = Length(main.Points);
                for (float s = 18f; s < L; s += 40f)
                {
                    var (p, dr) = Along(main.Points, s); var side2 = Vector3.Cross(Vector3.up, dr).normalized;
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Object.DestroyImmediate(go.GetComponent<Collider>());
                    go.name = $"맨홀 {s:F0}"; go.transform.SetParent(ground, false);
                    go.transform.position = GroundAt(p + side2 * 1.2f) + Vector3.up * 0.012f; go.transform.localScale = new Vector3(0.66f, 0.006f, 0.66f);
                    var mr = go.GetComponent<MeshRenderer>(); mr.sharedMaterial = iron; mr.shadowCastingMode = ShadowCastingMode.Off;
                    GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic); marks++;
                }
                foreach (var r in d.Roads.Where(x => x.Kind == "sidewalk"))
                    for (float s = 7f; s < Length(r.Points); s += 20f)
                    {
                        var (p, dr) = Along(r.Points, s); var side2 = Vector3.Cross(Vector3.up, dr).normalized;
                        var toRoad = ToRoadDir(d, p); float sg = Vector3.Dot(side2, toRoad) > 0 ? 1f : -1f;
                        Box($"빗물받이 {r.Name} {s:F0}", ground, GroundAt(p + side2 * sg * (r.Width * 0.5f + 0.25f)) + Vector3.up * 0.012f, new Vector3(0.55f, 0.01f, 0.32f), Quaternion.LookRotation(dr, Vector3.up), iron);
                        marks++;
                    }
            }
            // 3) 얼룩(벽 밑)·낙서(주차장 옹벽)·포스터(전봇대)
            var rects = DecalRects();
            var grime = rects.Keys.Where(k => k.StartsWith("decals_grime")).OrderBy(k => k, StringComparer.Ordinal).ToList();
            var graffiti = rects.Keys.Where(k => k.StartsWith("decals_graffiti")).OrderBy(k => k, StringComparer.Ordinal).ToList();
            var signRects = SignRects();
            var posterKeys = signRects.Keys.Where(k => k.StartsWith("poster_")).OrderBy(k => k, StringComparer.Ordinal).ToList();
            if (grime.Count > 0)
                foreach (var kv in objs.Where(x => S(x.Value, "kind") == "building").OrderBy(x => x.Key, StringComparer.Ordinal))
                {
                    var inf = D(G(kv.Value, "info")); var meta = D(G(inf, "meta")); var roles = D(G(inf, "roles"));
                    if (inf == null || roles == null) continue;
                    var c = V3(G(inf, "center")); var sz = V3(G(inf, "size")); float yaw = F(G(inf, "yaw")); float y0 = F(G(meta, "y0"), c.y - sz.y * 0.5f);
                    var rot = Quaternion.Euler(0f, yaw, 0f); var rng = new System.Random(Hash(kv.Key));
                    foreach (var face in roles)
                    {
                        string role = face.Value?.ToString();
                        if (role != "front" && role != "street") continue;
                        if (rng.NextDouble() > (role == "front" ? 0.8 : 0.55)) continue;
                        bool xf = face.Key.EndsWith("x"); float sg = face.Key.StartsWith("+") ? 1f : -1f;
                        var n = rot * (xf ? new Vector3(sg, 0f, 0f) : new Vector3(0f, 0f, sg));
                        var t = rot * (xf ? Vector3.forward : Vector3.right);
                        float half = xf ? sz.z * 0.5f : sz.x * 0.5f, depth = xf ? sz.x * 0.5f : sz.z * 0.5f;
                        float u = ((float)rng.NextDouble() - 0.5f) * (half * 2f - 1.6f);
                        float w = 0.7f + (float)rng.NextDouble() * 0.6f;
                        var pos = new Vector3(c.x, y0, c.z) + n * depth + t * u + Vector3.up * (0.25f + w * 0.3f);
                        var key = grime[rng.Next(grime.Count)];
                        Quad($"얼룩 {kv.Key} {face.Key}", walls, pos, n, w, w * 0.6f, DecalMat("M_Decal_Atlas", key, rects[key], new Color(1f, 1f, 1f, 0.5f)), 0.03f);
                        decals++;
                    }
                }
            var pad = d.Pads.FirstOrDefault(p => p.Name.Contains("주차장"));
            if (pad != null && graffiti.Count > 0)
            {
                var rot = Quaternion.Euler(0f, pad.Yaw, 0f);
                for (int k = 0; k < 3; k++)
                {
                    var local = new Vector3(-4.5f + k * 4.5f, 1.1f, -pad.Size.y * 0.5f - 0.05f);
                    var key = graffiti[k % graffiti.Count];
                    Quad($"낙서 옹벽(남) {k}", walls, pad.Center + rot * local, rot * Vector3.forward, 2.2f, 1.65f, DecalMat("M_Decal_Atlas", key, rects[key], new Color(1f, 1f, 1f, 0.9f)), 0.03f);
                    decals++;
                }
                var keyE = graffiti[3 % graffiti.Count];
                Quad("낙서 옹벽(동)", walls, pad.Center + rot * new Vector3(pad.Size.x * 0.5f + 0.05f, 0.75f, -2.0f), rot * Vector3.left, 1.8f, 1.1f, DecalMat("M_Decal_Atlas", keyE, rects[keyE], new Color(1f, 1f, 1f, 0.9f)), 0.03f);
                decals++;
            }
            var poles = objs.Where(x => S(x.Value, "kind") == "pole").OrderBy(x => x.Key, StringComparer.Ordinal).ToList();
            foreach (var kv in poles)
            {
                var inf = D(G(kv.Value, "info")); var basePos = V3(G(inf, "pos")); if (basePos == Vector3.zero) continue;
                var n = ToRoadDir(d, basePos); var rng = new System.Random(Hash(kv.Key));
                if (posterKeys.Count > 0)
                    for (int k = 0; k < 2; k++)
                    {
                        var key = posterKeys[rng.Next(posterKeys.Count)];
                        var rotY = Quaternion.Euler(0f, (k == 0 ? -25f : 30f), 0f) * n;
                        Quad($"포스터 {kv.Key} {k}", walls, basePos + Vector3.up * (1.25f + k * 0.62f) + rotY * 0.19f, rotY, 0.42f, 0.58f, DecalMat("M_Sign_Atlas", key, signRects[key], Color.white), 0.0f);
                        posters++;
                    }
                // 전봇대 밑 쓰레기 봉투(배출 장소)
                if (rng.NextDouble() < 0.6)
                {
                    var side = Vector3.Cross(Vector3.up, n).normalized * ((float)rng.NextDouble() < 0.5f ? 1f : -1f);
                    PutProp(prefab, "trash_bag_pile", clutter, GroundAt(basePos + side * 0.75f - n * 0.1f), (float)rng.NextDouble() * 360f);
                    items++;
                }
            }
            // 4) 골목 잡동사니: 길 가장자리 4~5m 마다(걷는 가운데 1.2m 비움)
            string[] cycle = { "styrofoam_planter", "basin_planter_onion", "clay_pots_cluster", "potted_plant_a", "cardboard_boxes", "potted_plant_b", "grass_tuft", "basin_planter_onion", "styrofoam_planter", "clay_pots_cluster" };
            foreach (var r in d.Roads.Where(x => x.Kind == "alley"))
            {
                var rng = new System.Random(Hash(r.Name)); float L = Length(r.Points); int k = 0;
                for (float s = 2.5f + (float)rng.NextDouble() * 2f; s < L - 2f; s += 4f + (float)rng.NextDouble() * 1.5f, k++)
                {
                    var (p, dir) = Along(r.Points, s); var side = Vector3.Cross(Vector3.up, dir).normalized * (k % 2 == 0 ? 1f : -1f);
                    var pos = GroundAt(p + side * (r.Width * 0.5f - 0.32f));
                    if (Mathf.Abs(pos.y - p.y) > 0.6f) continue;   // 축대·계단 위면 건너뜀
                    string prop = cycle[(k + rng.Next(2)) % cycle.Length];
                    PutProp(prefab, prop, clutter, pos, Mathf.Atan2(-side.x, -side.z) * Mathf.Rad2Deg + ((float)rng.NextDouble() - 0.5f) * 40f, $"소품 {prop} ← {r.Name} {k}");
                    items++;
                }
            }
            // 5) 남은 회색 상자: 가로등 4.5m → 코브라 가로등(0.62 배), 표지판 → 기둥+판+글자, 게시판·차단 펜스·차단물 → 키트 재질
            var marksRoot = zoneRoot.Find("Landmarks");
            if (marksRoot != null)
            {
                var lamp = marksRoot.Find("lamp");
                if (lamp != null) foreach (Transform m in lamp) if (m.name.Contains("가로등"))
                {
                    foreach (var rr in m.GetComponentsInChildren<MeshRenderer>(true)) if (rr.GetComponent<TMP_Text>() == null) rr.enabled = false;
                    var dir = ToRoadDir(d, m.position);
                    PutProp(prefab, "street_lamp_cobra", clutter, GroundAt(m.position), Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, $"소품 street_lamp_cobra ← {m.name}", Vector3.one * 0.62f);
                    swapped++;
                }
                var sign = marksRoot.Find("sign");
                var white = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/M_AC_White.mat");
                var grey = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/M_Metal_Grey.mat");
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root + "/Fonts/KR_Bold_SDF.asset");
                if (sign != null) foreach (Transform m in sign)
                {
                    if (m.name.Contains("간판")) continue;   // 하루편의점 간판은 키트
                    foreach (var rr in m.GetComponentsInChildren<MeshRenderer>(true)) if (rr.GetComponent<TMP_Text>() == null) rr.enabled = false;
                    var basePos = GroundAt(m.position); var n = ToRoadDir(d, basePos);
                    var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Object.DestroyImmediate(post.GetComponent<Collider>());
                    post.name = "표지 기둥"; post.transform.SetParent(clutter, false); post.transform.position = basePos + Vector3.up * 1.1f; post.transform.localScale = new Vector3(0.07f, 1.1f, 0.07f);
                    post.GetComponent<MeshRenderer>().sharedMaterial = grey; GameObjectUtility.SetStaticEditorFlags(post, StaticEditorFlags.BatchingStatic);
                    var plate = Box($"표지판 {m.name}", clutter, basePos + Vector3.up * 1.85f + n * 0.03f, new Vector3(0.7f, 0.42f, 0.03f), Quaternion.LookRotation(-n, Vector3.up), white);
                    AddText(plate.transform, basePos + Vector3.up * 1.85f + n * 0.05f, n, m.name.Replace("(", "\n(").Replace(" 표지", "").Replace(" 안내판", ""), 0.62f, 0.36f, new Color(0.17f, 0.19f, 0.29f), font);
                    swapped++;
                }
                var board = marksRoot.Find("board");
                if (board != null) foreach (Transform m in board)
                {
                    foreach (var rr in m.GetComponentsInChildren<MeshRenderer>(true)) if (rr.GetComponent<TMP_Text>() == null) rr.sharedMaterial = grey;
                    var basePos = GroundAt(m.position); var n = ToRoadDir(d, basePos); var rng = new System.Random(Hash(m.name));
                    if (posterKeys.Count > 0)
                        for (int k = 0; k < 4; k++)
                        {
                            var key = posterKeys[rng.Next(posterKeys.Count)];
                            var rotY = Quaternion.Euler(0f, k * 90f, 0f) * n;
                            Quad($"포스터 {m.name} {k}", walls, basePos + Vector3.up * (1.3f + (k % 2) * 0.5f) + rotY * 0.22f, rotY, 0.42f, 0.58f, DecalMat("M_Sign_Atlas", key, signRects[key], Color.white), 0f);
                            posters++;
                        }
                }
            }
            var blocks = zoneRoot.Find("Blocks/barrier");
            if (blocks != null)
            {
                var green = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/M_Metal_Green.mat");
                var grey = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/M_Metal_Grey.mat");
                foreach (Transform b in blocks)
                {
                    var rr = b.GetComponent<MeshRenderer>(); if (rr == null) continue;
                    rr.sharedMaterial = b.name.Contains("펜스") ? green : grey; swapped++;
                }
            }
            // 6) 밤 보안등 1개: 시우네 앞 골목(가장 어두운 자리, D07 최소 6.7)
            {
                var pos = GroundAt(new Vector3(-162.3f, 28f, 121.1f)); var n = ToRoadDir(d, pos);
                PutProp(prefab, "street_lamp_trail", clutter, pos, Mathf.Atan2(n.x, n.z) * Mathf.Rad2Deg, "소품 street_lamp_trail(시우네 앞 보안등)");
                var lights = zoneRoot.Find("Lights");
                if (lights != null)
                {
                    var go = new GameObject("빛 보안등 시우네 앞"); go.transform.SetParent(lights, false); go.transform.position = pos + Vector3.up * 3.3f;
                    var l = go.AddComponent<Light>(); l.type = LightType.Point; l.range = 7f; l.intensity = 0f; l.enabled = false; l.shadows = LightShadows.None; l.renderMode = LightRenderMode.ForcePixel; l.color = new Color(1f, 0.62f, 0.28f);
                    go.AddComponent<LightSpot>().Kind = LightKind.Security;
                }
            }
            return $"연석 {curbs} · 노면 표시 {marks} · 얼룩·낙서 {decals} · 포스터 {posters} · 잡동사니 {items} · 회색 교체 {swapped} · 보안등 +1";
        }

        static void AddText(Transform parent, Vector3 pos, Vector3 n, string text, float w, float h, Color col, TMP_FontAsset font)
        {
            var go = new GameObject("글자");
            go.transform.SetParent(parent, true);
            go.transform.position = pos; go.transform.rotation = Quaternion.LookRotation(-n, Vector3.up); go.transform.localScale = Vector3.one;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.font = font; tmp.text = text; tmp.color = col; tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap; tmp.enableAutoSizing = true; tmp.fontSizeMin = 0.2f; tmp.fontSizeMax = 30f;
            tmp.rectTransform.sizeDelta = new Vector2(w, h); tmp.margin = Vector4.zero; tmp.sortingOrder = 1;
            var mr = go.GetComponent<MeshRenderer>(); if (mr != null) { mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false; }
            tmp.ForceMeshUpdate(true, true);
        }

        // ═════════════════════════ 식생: 회색 나무·풀숲 → 소나무·벚나무·느티나무·관목, 담쟁이(담), 풀(축대 위·성곽 아래)
        static string Vegetation(Zone1Data d, Transform zoneRoot, Transform env, Dictionary<string, Dictionary<string, object>> objs, Func<string, GameObject> prefab)
        {
            bool have = VegNames.All(n => prefab(n) != null);
            if (!have) { Notes.Add("식생 프리팹이 아직 없음(P_pine_tree …) — 회색 나무·풀숲 그대로"); return "식생 없음"; }
            var root = new GameObject("Veg").transform; root.SetParent(env, false);
            int trees = 0, bushes = 0, ivy = 0, grass = 0, wall = 0;
            var tree = zoneRoot.Find("Blocks/tree");
            if (tree != null)
                foreach (Transform t in tree)
                {
                    foreach (var rr in t.GetComponentsInChildren<MeshRenderer>(true)) rr.enabled = false;   // 콜라이더(줄기 캡슐)는 그대로
                    var rng = new System.Random(Hash(t.name + t.position.x.ToString("F1") + t.position.z.ToString("F1")));
                    var feet = GroundAt(t.position, 6f, 20f);
                    float top = t.GetComponentsInChildren<MeshFilter>(true).Select(mf => mf.GetComponent<Renderer>()).Where(r => r != null).Select(r => r.bounds.max.y).DefaultIfEmpty(feet.y + 9f).Max();
                    float height = Mathf.Max(5f, top - feet.y);
                    string species; float baseH;
                    if (feet.z >= 93f && feet.z <= 109f) { species = "zelkova_tree"; baseH = 9f; }
                    else if (feet.x < -200f && feet.z < 100f) { species = "cherry_tree"; baseH = 6.5f; }
                    else { species = "pine_tree"; baseH = 7f; }
                    float sc = Mathf.Clamp(height / baseH, 0.85f, 1.45f) * (0.92f + (float)rng.NextDouble() * 0.16f);
                    PutProp(prefab, species, root, feet, (float)rng.NextDouble() * 360f, $"나무 {species} ← {t.name}", Vector3.one * sc);
                    trees++;
                }
            var bush = zoneRoot.Find("Blocks/bush");
            if (bush != null)
                foreach (Transform b in bush)
                {
                    var rr = b.GetComponent<MeshRenderer>(); if (rr != null) rr.enabled = false;
                    var rng = new System.Random(Hash(b.name + b.position.x.ToString("F1") + b.position.z.ToString("F1")));
                    var size = b.lossyScale; var feet = GroundAt(b.position, 4f, 12f);
                    var sc = new Vector3(size.x / 1.6f, Mathf.Clamp(size.y / 1.1f, 1f, 2.6f), size.z / 1.4f) * (0.9f + (float)rng.NextDouble() * 0.2f);
                    PutProp(prefab, "shrub_bush", root, feet, b.eulerAngles.y + (float)rng.NextDouble() * 30f, $"관목 ← {b.name}", sc);
                    bushes++;
                    if (rng.NextDouble() < 0.5)
                    {
                        var off = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f) * Vector3.forward * (size.x * 0.5f + 0.3f);
                        PutProp(prefab, "grass_tuft", root, GroundAt(b.position + off, 4f, 12f), (float)rng.NextDouble() * 360f, $"풀 ← {b.name}");
                        grass++;
                    }
                }
            // 담쟁이·축대 위 풀: 키트 담·옹벽 블록(manifest fences)
            foreach (var kv in objs.Where(x => S(x.Value, "kind") == "fences").OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                var blocksL = L(G(D(G(kv.Value, "info")), "blocks")); if (blocksL == null) continue;
                foreach (var bo in blocksL)
                {
                    var bd = D(bo); string bt = S(bd, "btype") ?? ""; var c = V3(G(bd, "center")); var sz = V3(G(bd, "size")); float yaw = F(G(bd, "yaw"));
                    var rot = Quaternion.Euler(0f, yaw, 0f); var rng = new System.Random(Hash(kv.Key + S(bd, "block") + c.x.ToString("F1") + c.z.ToString("F1")));
                    var n = rot * Vector3.forward; var toRoad = ToRoadDir(d, c); if (Vector3.Dot(n, toRoad) < 0f) n = -n;
                    var along = rot * Vector3.right;
                    if (bt == "fence_wall" && sz.x > 1.6f && rng.NextDouble() < 0.4)
                    {
                        float u = ((float)rng.NextDouble() - 0.5f) * (sz.x - 1.6f);
                        var feet = GroundAt(c + along * u + n * (sz.z * 0.5f + 0.08f), 3f, 8f);
                        float sc = Mathf.Clamp((c.y + sz.y * 0.5f - feet.y) / 1.8f, 0.6f, 1.2f);
                        PutProp(prefab, "ivy_patch", root, feet, Mathf.Atan2(n.x, n.z) * Mathf.Rad2Deg, $"담쟁이 ← {S(bd, "block")}", new Vector3(sc, sc, 1f));
                        ivy++;
                    }
                    if (bt.StartsWith("retaining") && sz.x > 2f)
                    {
                        float topY = c.y + sz.y * 0.5f;
                        for (float u = -sz.x * 0.5f + 1f; u < sz.x * 0.5f - 0.8f; u += 2.2f + (float)rng.NextDouble())
                        {
                            var p = c + along * u - n * (sz.z * 0.5f - 0.25f); p.y = topY;
                            PutProp(prefab, rng.NextDouble() < 0.7 ? "grass_tuft" : "shrub_bush", root, p, (float)rng.NextDouble() * 360f, $"축대 위 풀 ← {S(bd, "block")}", rng.NextDouble() < 0.7 ? Vector3.one : Vector3.one * 0.55f);
                            grass++;
                        }
                    }
                }
            }
            // 성곽 아래 초록: 성곽 폴리라인 안쪽(남) 1.8m 띠에 관목·풀 7m 마다
            if (d.Walls.Count > 0)
            {
                var pts = d.Walls[0].Points; var rng = new System.Random(20261024); float L = Length(pts);
                for (float s = 3f; s < L - 3f; s += 6.5f + (float)rng.NextDouble() * 2f)
                {
                    var (p, dir) = Along(pts, s); var side = Vector3.Cross(Vector3.up, dir).normalized;
                    var inward = Vector3.Dot(side, new Vector3(-200f, 0f, 110f) - Flat(p)) > 0f ? side : -side;
                    var feet = GroundAt(p + inward * (d.Walls[0].Thickness * 0.5f + 1.6f), 6f, 15f);
                    if (feet.y > p.y + 1f) continue;
                    bool shrub = rng.NextDouble() < 0.55;
                    PutProp(prefab, shrub ? "shrub_bush" : "grass_tuft", root, feet, (float)rng.NextDouble() * 360f, $"성곽 아래 {(shrub ? "관목" : "풀")} {s:F0}", shrub ? Vector3.one * (0.8f + (float)rng.NextDouble() * 0.5f) : Vector3.one * 1.3f);
                    wall++;
                }
            }
            return $"나무 {trees} · 관목 {bushes} · 담쟁이 {ivy} · 풀 {grass} · 성곽 아래 {wall}";
        }
    }
}
