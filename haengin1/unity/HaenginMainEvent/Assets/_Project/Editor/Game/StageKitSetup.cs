// 행인1의 메인이벤트 — 무대 4곳 키트(09 설계 9단계: 국밥집·시우네·하루편의점·혜성고 2-3반) 들여오기·재질·소품 프리팹·무대에 놓기
// 원본(저장소): concept/art/env/stages/St_<Key>.fbx + _manifest.json(tools/env/stages/build_stages.py) · textures/M_*.png(make_textures.py) · props/*.glb(process_props.py)
//   → Assets/_Project/Art/Env/Stages/ 로 복사(Import) → 재질 Materials/Env/Stages/M_*.mat(UTS, 유리 = URP Lit 투명, 창밖 = Unlit, 등판·냉장고 상품 = 발광)
//   → 소품 프리팹 Prefabs/Stage/P_*.prefab(LOD 3단 — 체육관 규칙). 무대 장면은 StorySetup.Stages 가 Place(key, root) 를 불러 키트·충돌·소품·불을 놓는다.
// 좌표: 키트 = 무대 루트 로컬(원점 = 방 가운데 바닥), 소켓 Sock_<종류>_<이름> 은 자리(스폰·배우·좌석·사진·불)로 돌려준다.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Haengin.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Haengin.EditorGame
{
    public static class StageKitSetup
    {
        const string Tag = "[StageKit]";
        const string Root = "Assets/_Project";
        public const string Dir = Root + "/Art/Env/Stages", TexDir = Dir + "/Textures", PropDir = Dir + "/Props";
        public const string MatDir = Root + "/Materials/Env/Stages", PropMatDir = MatDir + "/Props", PrefabDir = Root + "/Prefabs/Stage";
        public static readonly string[] Keys = { "Gukbap", "Home", "Conv", "School" };
        public static readonly List<string> Notes = new List<string>();
        static string SrcDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../concept/art/env/stages"));

        public static string Fbx(string key) => $"{Dir}/St_{key}.fbx";
        public static string ManifestPath(string key) => $"{Dir}/St_{key}_manifest.json";
        public static bool Has(string key) => AssetDatabase.LoadAssetAtPath<GameObject>(Fbx(key)) != null && File.Exists(ManifestPath(key));

        /// 배치: 들여오기 → 재질 → 소품 프리팹(그다음 StorySetup.Build 가 무대를 다시 만들며 Place 를 부름)
        public static void Build()
        {
            int code = 0;
            try { Assets(); AssetDatabase.SaveAssets(); foreach (var n in Notes) Debug.Log($"{Tag}   {n}"); }
            catch (Exception e) { Debug.LogError($"{Tag} 실패: {e}"); code = 1; }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        public static void Assets()
        {
            Notes.Clear();
            Import();
            Materials();
            PropPrefabs();
        }

        // ───────────────────────── 1) 복사·들여오기
        static void Import()
        {
            foreach (var d in new[] { Dir, TexDir, PropDir, MatDir, PropMatDir, PrefabDir }) EnsureFolder(d);
            string src = SrcDir;
            if (!Directory.Exists(src)) throw new Exception("원본 폴더 없음: " + src);
            int n = 0;
            void Copy(string from, string to)
            {
                if (!File.Exists(from)) return;
                var fi = new FileInfo(from); var ti = new FileInfo(to);
                if (ti.Exists && ti.Length == fi.Length && ti.LastWriteTimeUtc >= fi.LastWriteTimeUtc) return;
                File.Copy(from, to, true); n++;
            }
            string proj = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            foreach (var key in Keys)
            {
                Copy(Path.Combine(src, $"St_{key}.fbx"), Path.Combine(proj, Fbx(key)));
                Copy(Path.Combine(src, $"St_{key}_manifest.json"), Path.Combine(proj, ManifestPath(key)));
            }
            foreach (var f in Directory.GetFiles(Path.Combine(src, "textures"), "M_*.png")) Copy(f, Path.Combine(proj, TexDir, Path.GetFileName(f)));
            // 소품 GLB(LOD 3단)는 tools/env/prop_lods.py 가 바로 Art/Env/Stages/Props 에 쓴다(원본 props/*.glb 는 LOD 없음 — 복사하지 않음)
            AssetDatabase.Refresh();
            AssetDatabase.ImportAsset(Dir, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceSynchronousImport);
            Notes.Add($"복사 {n}개 · 키트 {Keys.Count(Has)}/4 · 텍스처 {Directory.GetFiles(Path.Combine(proj, TexDir), "*.png").Length} · 소품 GLB {Directory.GetFiles(Path.Combine(proj, PropDir), "*.glb").Length}");
        }

        static Dictionary<string, object> Man(string key) => File.Exists(ManifestPath(key)) ? MiniJson.Parse(File.ReadAllText(ManifestPath(key))) as Dictionary<string, object> : null;
        static Dictionary<string, object> D(object o) => o as Dictionary<string, object>;
        static List<object> L(object o) => o as List<object>;
        static float F(object o, float def = 0f) => o is double d ? (float)d : o is long l ? l : o is int i ? i : def;
        static object G(Dictionary<string, object> d, string k) => d != null && d.TryGetValue(k, out var v) ? v : null;
        static string S(Dictionary<string, object> d, string k) => d != null && d.TryGetValue(k, out var v) && v != null ? v.ToString() : null;
        static Vector3 V3(object o) { var l = L(o); return l != null && l.Count >= 3 ? new Vector3(F(l[0]), F(l[1]), F(l[2])) : Vector3.zero; }
        static Color Hex(string h, Color def) => !string.IsNullOrEmpty(h) && ColorUtility.TryParseHtmlString(h, out var c) ? c : def;

        // ───────────────────────── 2) 재질
        static readonly Dictionary<string, Material> made = new Dictionary<string, Material>();

        static Material Lit(string path, Color col, float smooth)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(sh) { name = Path.GetFileNameWithoutExtension(path) }; AssetDatabase.CreateAsset(m, path); }
            m.shader = sh;
            m.SetColor("_BaseColor", col); m.SetFloat("_Metallic", 0f); m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f); m.SetFloat("_AlphaClip", 0f); m.SetFloat("_Cull", (float)CullMode.Off);
            m.SetOverrideTag("RenderType", "Transparent"); m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha); m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Unlit(string path, Texture tex, Color col)
        {
            var sh = Shader.Find("Universal Render Pipeline/Unlit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(sh) { name = Path.GetFileNameWithoutExtension(path) }; AssetDatabase.CreateAsset(m, path); }
            m.shader = sh; m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", col);
            EditorUtility.SetDirty(m);
            return m;
        }

        static void Materials()
        {
            made.Clear();
            var all = new Dictionary<string, Dictionary<string, object>>();
            foreach (var key in Keys)
            {
                var man = Man(key); if (man == null) continue;
                foreach (var kv in D(man["materials"])) if (!all.ContainsKey(kv.Key)) all[kv.Key] = D(kv.Value);
            }
            int reused = 0;
            foreach (var kv in all)
            {
                string name = kv.Key; var info = kv.Value;
                // 맵 규약 재질(M_Metal_Grey·M_AC_White … 텍스처 없는 평면 색)은 Zone1 재질을 그대로
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{name}.png");
                if (tex == null && S(info, "owner") != "stage")
                {
                    var zm = AssetDatabase.LoadAssetAtPath<Material>($"{EnvSetup.MatDir}/{name}.mat");
                    if (zm != null) { made[name] = zm; reused++; continue; }
                }
                float tiling = F(G(info, "tiling"), 1f); if (tiling <= 0f) tiling = 1f;
                bool unit = S(info, "uv") == "unit";
                Color col = Hex(S(info, "color"), Color.gray);
                string path = $"{MatDir}/{name}.mat";
                Material m;
                switch (name)
                {
                    case "M_Glass_Fridge": m = Lit(path, new Color(0.80f, 0.90f, 0.92f, 0.14f), 0.9f); break;
                    case "M_View_Fortress": case "M_View_Night": case "M_View_Dawn": m = Unlit(path, tex, Color.white); break;
                    case "M_Fluor_Panel":
                        m = EnvSetup.Toon(path, tex, Vector2.one, Vector2.zero, col);
                        m.EnableKeyword("_EMISSIVE_SIMPLE"); m.SetColor("_Emissive_Color", new Color(1.25f, 1.28f, 1.2f)); break;
                    case "M_Products":
                        m = EnvSetup.Toon(path, tex, Vector2.one, Vector2.zero, Color.white);
                        m.EnableKeyword("_EMISSIVE_SIMPLE"); m.SetTexture("_Emissive_Tex", tex); m.SetColor("_Emissive_Color", new Color(0.28f, 0.28f, 0.28f)); break;
                    default:
                        m = EnvSetup.Toon(path, tex, unit ? Vector2.one : Vector2.one * tiling, Vector2.zero, tex != null ? Color.white : col); break;
                }
                made[name] = m;
            }
            // 키트 FBX 재질 바꿔 꿰기
            int remapped = 0;
            foreach (var key in Keys)
            {
                var imp = AssetImporter.GetAtPath(Fbx(key)) as ModelImporter;
                if (imp == null) continue;
                var map = imp.GetExternalObjectMap();
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx(key));
                var names = new HashSet<string>();
                foreach (var r in go.GetComponentsInChildren<Renderer>(true)) foreach (var sm in r.sharedMaterials) if (sm != null) names.Add(sm.name);
                bool dirty = false;
                foreach (var nm in names)
                {
                    if (!made.TryGetValue(nm, out var target)) { if (nm != "No Name") Notes.Add($"{key}: 키트 재질 '{nm}' 은 manifest 에 없음"); continue; }
                    var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), nm);
                    if (map.TryGetValue(id, out var cur) && cur == target) continue;
                    imp.AddRemap(id, target); dirty = true; remapped++;
                }
                if (dirty) imp.SaveAndReimport();
            }
            Notes.Add($"재질 {made.Count}개(맵 재질 그대로 {reused}) · 키트 재질 바꿔 꿴 것 {remapped}");
        }

        // ───────────────────────── 3) 소품 프리팹(GymSetup.PropPrefabs 와 같은 규칙)
        static Texture ExtractedTexture(Texture src, string name)
        {
            string dir = PropDir + "/Tex"; EnsureFolder(dir);
            string png = $"{dir}/{name}.png";
            var have = AssetDatabase.LoadAssetAtPath<Texture2D>(png);
            if (have != null) return have;
            if (src == null || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) { if (src != null) Notes.Add($"소품 텍스처 추출 못 함(그래픽 없음): {name}"); return null; }
            var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(src, rt);
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var t2 = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false, false);
            t2.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0); t2.Apply();
            RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
            File.WriteAllBytes(png, t2.EncodeToPNG()); Object.DestroyImmediate(t2);
            AssetDatabase.ImportAsset(png, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(png);
        }

        static void PropPrefabs()
        {
            int n = 0; long tris = 0;
            foreach (var glb in Directory.GetFiles(PropDir, "*.glb").OrderBy(x => x, StringComparer.Ordinal))
            {
                string path = glb.Replace('\\', '/'); string name = Path.GetFileNameWithoutExtension(path);
                var src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (src == null) { Notes.Add($"소품 GLB 못 읽음: {path}"); continue; }
                var mfs = src.GetComponentsInChildren<MeshFilter>(true).Where(m => m.sharedMesh != null).ToList();
                if (mfs.Count == 0) continue;
                MeshFilter Lod(int k) => mfs.FirstOrDefault(m => m.name.EndsWith("_LOD" + k) || m.sharedMesh.name.EndsWith("_LOD" + k)) ?? (k == 0 ? mfs[0] : null);
                var lod0 = Lod(0); var lod0R = lod0.GetComponent<MeshRenderer>(); var srcMat = lod0R != null ? lod0R.sharedMaterial : null;
                Texture tex = null;
                if (srcMat != null) foreach (var id in new[] { "baseColorTexture", "_BaseMap", "_MainTex" }) if (srcMat.HasProperty(id) && srcMat.GetTexture(id) != null) { tex = srcMat.GetTexture(id); break; }
                tex = ExtractedTexture(tex, name) ?? tex;
                var mat = EnvSetup.Toon($"{PropMatDir}/M_P_{name}.mat", tex, Vector2.one, Vector2.zero, Color.white);
                var root = new GameObject("P_" + name);
                var lg = root.AddComponent<LODGroup>();
                var lods = new List<LOD>();
                var b = lod0.sharedMesh.bounds; float size = Mathf.Max(b.size.x, b.size.y, b.size.z) * Mathf.Max(lod0.transform.lossyScale.x, 0.001f);
                float H(float dist) => Mathf.Clamp(size / (dist * 0.8284f), 0.002f, 0.95f);
                float[] trans = { H(size < 1f ? 5f : 9f), H(size < 1f ? 12f : 20f), 0.004f };
                for (int k = 0; k < 3; k++)
                {
                    var mf = Lod(k); if (mf == null) break;
                    var go = new GameObject("LOD" + k); go.transform.SetParent(root.transform, false);
                    go.transform.SetPositionAndRotation(mf.transform.position - src.transform.position, mf.transform.rotation); go.transform.localScale = mf.transform.lossyScale;
                    go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                    var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
                    r.shadowCastingMode = k == 0 ? ShadowCastingMode.On : ShadowCastingMode.Off; r.receiveShadows = true;
                    lods.Add(new LOD(trans[Mathf.Min(k, 2)], new Renderer[] { r }));
                    if (k == 0) tris += mf.sharedMesh.triangles.Length / 3;
                }
                if (lods.Count > 0) { var last = lods[lods.Count - 1]; last.screenRelativeTransitionHeight = trans[2]; lods[lods.Count - 1] = last; }
                lg.SetLODs(lods.ToArray()); lg.fadeMode = LODFadeMode.None; lg.RecalculateBounds();
                PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/P_{name}.prefab");
                Object.DestroyImmediate(root); n++;
            }
            Notes.Add($"소품 프리팹 {n}개(LOD0 삼각형 합 {tris:N0})");
        }

        // ───────────────────────── 4) 무대에 놓기(StorySetup.Stages 가 부름)
        public sealed class Placed
        {
            public readonly Dictionary<string, (Vector3 pos, float yaw, string state)> Spawns = new Dictionary<string, (Vector3, float, string)>();
            public readonly Dictionary<string, (Vector3 pos, float yaw, string state)> Actors = new Dictionary<string, (Vector3, float, string)>();
            public readonly Dictionary<string, (Vector3 pos, float yaw, string state)> Seats = new Dictionary<string, (Vector3, float, string)>();
            public readonly List<(int row, int col, Vector3 pos, float yaw)> Desks = new List<(int, int, Vector3, float)>();
            public readonly Dictionary<string, (Vector3 pos, float yaw, float w, float h)> Photos = new Dictionary<string, (Vector3, float, float, float)>();
            public readonly Dictionary<string, (Vector3 pos, Vector3 look, float fov)> Cams = new Dictionary<string, (Vector3, Vector3, float)>();
            public int Doors, Markers;
            public Vector3 Size;
            public string Summary;
        }

        static GameObject FindPropPrefab(string prop)
        {
            foreach (var d in new[] { PrefabDir, EnvSetup.PrefabDir, GymSetup.PrefabDir })
            {
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"{d}/P_{prop}.prefab");
                if (pf != null) return pf;
            }
            return null;
        }

        static float Yaw(Vector3 fwd) { var f = new Vector3(fwd.x, 0f, fwd.z); return f.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(f.normalized).eulerAngles.y : 0f; }

        public static Placed Place(string key, Transform root)
        {
            if (!Has(key)) return null;
            var man = Man(key);
            var res = new Placed();
            var room = D(man["room"]);
            res.Size = new Vector3(F(G(room, "w")), F(G(room, "h")), F(G(room, "d")));
            // ① 키트(풀어서) — 배경 정적, 그림자 받음
            var kitPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx(key));
            var kit = (GameObject)PrefabUtility.InstantiatePrefab(kitPrefab, root.gameObject.scene);
            PrefabUtility.UnpackPrefabInstance(kit, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            kit.name = "Kit"; kit.transform.SetParent(root, false); kit.transform.localPosition = Vector3.zero; kit.transform.localRotation = Quaternion.identity; kit.transform.localScale = Vector3.one;
            int rends = 0;
            foreach (var r in kit.GetComponentsInChildren<Renderer>(true))
            {
                r.receiveShadows = true; rends++;
                r.shadowCastingMode = r.name.Contains("Fixtures") ? ShadowCastingMode.Off : ShadowCastingMode.On;
                GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
                r.gameObject.layer = 0;
            }
            // 빈 오브젝트(소켓·소품 자리)는 키트에서 지우고 아래에서 다시 만든다
            foreach (var t in kit.GetComponentsInChildren<Transform>(true).Where(t => t != kit.transform && (t.name.StartsWith("Sock_") || t.name.StartsWith("Prop_"))).ToList())
                if (t != null) Object.DestroyImmediate(t.gameObject);
            // 실내 조명(해·안개 끔 + 주변광 + 볼륨) — 장소마다 색
            var sl = root.GetComponent<StageLights>() ?? root.gameObject.AddComponent<StageLights>();
            switch (key)
            {
                case "Gukbap": sl.Ambient = new Color(0.55f, 0.53f, 0.49f); sl.Filter = new Color(1f, 0.97f, 0.92f); break;
                case "Home": sl.Ambient = new Color(0.58f, 0.55f, 0.49f); sl.Filter = new Color(1f, 0.96f, 0.90f); break;
                case "Conv": sl.Ambient = new Color(0.58f, 0.60f, 0.61f); sl.Filter = new Color(0.96f, 0.99f, 1f); break;
                default: sl.Ambient = new Color(0.64f, 0.63f, 0.60f); sl.Filter = new Color(1f, 0.99f, 0.96f); break;
            }
            // ② 충돌 상자
            var colRoot = new GameObject("Colliders").transform; colRoot.SetParent(root, false);
            int cols = 0;
            foreach (var co in L(man["colliders"]))
            {
                var c = L(co); if (c == null || c.Count < 7) continue;
                var go = new GameObject("Col_" + cols); go.transform.SetParent(colRoot, false);
                var a = new Vector3(F(c[0]), F(c[2]), F(c[4])); var b = new Vector3(F(c[1]), F(c[3]), F(c[5]));
                go.transform.localPosition = (a + b) * 0.5f;
                var bc = go.AddComponent<BoxCollider>(); bc.size = new Vector3(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y), Mathf.Abs(b.z - a.z));
                string layer = c[6] as string;
                go.layer = layer == "Ground" ? Layers.Ground : layer == "Wall" ? Layers.Wall : 0;
                cols++;
            }
            // ③ 소켓 → 소품·불·자리
            var props = new GameObject("Props").transform; props.SetParent(root, false);
            var lights = new GameObject("Lights").transform; lights.SetParent(root, false);
            var markers = new GameObject("Markers").transform; markers.SetParent(root, false);
            int placed = 0, missing = 0, nl = 0;
            var miss = new HashSet<string>();
            foreach (var so in L(man["sockets"]))
            {
                var sd = D(so); string nm = S(sd, "name"), kind = S(sd, "kind"); var inf = D(G(sd, "info"));
                var pos = V3(G(sd, "pos")); float yaw = Yaw(V3(G(sd, "fwd")));
                switch (kind)
                {
                    case "prop":
                        {
                            string prop = S(inf, "prop");
                            var pf = FindPropPrefab(prop);
                            if (pf == null) { missing++; miss.Add(prop); break; }
                            var pi = (GameObject)PrefabUtility.InstantiatePrefab(pf, root.gameObject.scene);
                            pi.name = nm; pi.transform.SetParent(props, false);
                            pi.transform.localPosition = pos; pi.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                            placed++;
                            break;
                        }
                    case "light":
                        {
                            var go = new GameObject(nm); go.transform.SetParent(lights, false); go.transform.localPosition = pos;
                            var lt = go.AddComponent<Light>();
                            lt.type = LightType.Point; lt.color = Hex(S(inf, "color"), Color.white); lt.intensity = F(G(inf, "intensity"), 1.5f); lt.range = F(G(inf, "range"), 6f);
                            lt.shadows = LightShadows.None; lt.renderMode = LightRenderMode.Auto;
                            nl++;
                            break;
                        }
                    case "spawn": res.Spawns[nm.Substring("Sock_Spawn_".Length)] = (pos, yaw, null); break;
                    case "actor": res.Actors[nm.Substring("Sock_Actor_".Length)] = (pos, yaw, S(inf, "state")); break;
                    case "seat": res.Seats[nm.Substring("Sock_Seat_".Length)] = (pos, yaw, null); break;
                    case "desk": res.Desks.Add(((int)F(G(inf, "row")), (int)F(G(inf, "col")), pos, yaw)); break;
                    case "photo": res.Photos[nm.Substring("Sock_Photo_".Length)] = (pos, yaw, F(G(inf, "w"), 0.36f), F(G(inf, "h"), 0.46f)); break;
                    case "door":
                        {
                            // 무대 안 문: 들어오면 서는 자리(스폰 'Door') + 나가는 상호작용(Zone1 쪽 짝 문의 ReturnPos 로)
                            res.Spawns["Door"] = (pos, yaw, null);
                            var go = new GameObject("Door_" + nm.Substring("Sock_Door_".Length)); go.transform.SetParent(markers, false);
                            go.transform.localPosition = pos - Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * 0.35f; go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                            var it = go.AddComponent<Interactable>(); it.Prompt = S(inf, "label") ?? "나가기"; it.Radius = 1.3f;
                            var sd2 = go.AddComponent<StageDoor>(); sd2.Target = SceneLoader.Zone1; sd2.Key = key;
                            res.Doors++;
                            break;
                        }
                    case "marker":
                        {
                            var go = new GameObject("Marker_" + nm.Substring("Sock_Marker_".Length)); go.transform.SetParent(markers, false);
                            go.transform.localPosition = pos; go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                            var pm = go.AddComponent<PlaceMarker>(); pm.Place = key; pm.Func = S(inf, "func"); pm.Label = S(inf, "label");
                            var ring = GameObject.CreatePrimitive(PrimitiveType.Quad); ring.name = "Ring"; Object.DestroyImmediate(ring.GetComponent<Collider>());
                            ring.transform.SetParent(go.transform, false); ring.transform.localPosition = new Vector3(0f, 0.012f, 0f); ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); ring.transform.localScale = Vector3.one * 0.85f;
                            var rr = ring.GetComponent<MeshRenderer>(); rr.sharedMaterial = RingMat(); rr.shadowCastingMode = ShadowCastingMode.Off; rr.receiveShadows = false;
                            res.Markers++;
                            break;
                        }
                    case "cam":
                        {
                            var look = V3(G(inf, "look"));
                            res.Cams[nm.Substring("Sock_Cam_".Length)] = (pos, look, F(G(inf, "fov"), 48f));
                            var go = new GameObject(nm); go.transform.SetParent(markers, false); go.transform.localPosition = pos;
                            go.transform.localRotation = Quaternion.LookRotation((look - pos).normalized, Vector3.up);
                            break;
                        }
                }
            }
            res.Summary = $"키트 렌더러 {rends} · 충돌 {cols} · 소품 {placed}{(missing > 0 ? $"(없음 {missing}: {string.Join(",", miss)})" : "")} · 불 {nl} · 문 {res.Doors} · 표시 자리 {res.Markers} · 스폰 {string.Join(",", res.Spawns.Keys)}";
            return res;
        }

        static Material ringMat;
        static Material RingMat()
        {
            if (ringMat != null) return ringMat;
            string path = $"{MatDir}/M_MarkerRing.mat";
            ringMat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (ringMat == null) { ringMat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "M_MarkerRing" }; AssetDatabase.CreateAsset(ringMat, path); }
            ringMat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(GymSetup.RingTex)); ringMat.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.45f));
            ringMat.SetFloat("_Surface", 1f); ringMat.SetFloat("_Blend", 0f); ringMat.SetOverrideTag("RenderType", "Transparent"); ringMat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); ringMat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha); ringMat.SetInt("_ZWrite", 0); ringMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); ringMat.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(ringMat);
            return ringMat;
        }

        // ───────────────────────── 5) Zone1 쪽 문(랜드마크 '…출입문' + 혜성고 방면 표지) — M1Setup.AttachRig 이 Zone1 을 다시 만들 때 부름
        static readonly (string landmark, string key, string label)[] Zone1Doors =
        {
            ("성대후문 국밥 출입문", "Gukbap", "들어가기 — 국밥집"),
            ("하루편의점 후문점 출입문", "Conv", "들어가기 — 하루편의점"),
            ("반시우네 출입문", "Home", "들어가기 — 집"),
            ("혜성고 방면 표지", "School", "학교 가기 — 혜성고"),
        };

        public static string AddDoorsToZone1(Transform zoneRoot)
        {
            var log = new List<string>();
            var all = zoneRoot.GetComponentsInChildren<Transform>(true);
            int block = Layers.Mask(Layers.Default, Layers.Wall, Layers.CamBlock);
            foreach (var (lm, key, label) in Zone1Doors)
            {
                if (!Has(key)) continue;
                var t = all.FirstOrDefault(x => x.name == lm);
                if (t == null) { log.Add(lm + " 없음"); continue; }
                var ex = t.Find("StageDoor");
                var door = ex != null ? ex.gameObject : new GameObject("StageDoor");
                door.transform.SetParent(t, false); door.transform.localPosition = Vector3.zero;
                var it = door.GetComponent<Interactable>(); if (it == null) it = door.AddComponent<Interactable>();
                it.Prompt = label; it.Radius = 1.8f;
                var sd = door.GetComponent<StageDoor>(); if (sd == null) sd = door.AddComponent<StageDoor>();
                sd.Target = key; sd.Spawn = "Door"; sd.Key = key;
                // 나왔을 때 설 자리: 문에서 1.5m 떨어진 8 방향 중 막히지 않고 아래에 땅이 있는 곳
                var p = t.position; Vector3 best = p; float bestYaw = 0f; bool ok = false;
                for (int k = 0; k < 8 && !ok; k++)
                {
                    float a = k * 45f; var dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                    var c = p + dir * 1.5f + Vector3.up * 1.2f;
                    if (Physics.CheckCapsule(c - Vector3.up * 0.6f, c + Vector3.up * 0.6f, 0.3f, block, QueryTriggerInteraction.Ignore)) continue;
                    if (!Physics.Raycast(c, Vector3.down, out var hit, 3f, Layers.Mask(Layers.Ground, Layers.Default), QueryTriggerInteraction.Ignore)) continue;
                    best = hit.point + Vector3.up * 0.05f; bestYaw = a; ok = true;
                }
                sd.ReturnPos = ok ? best : p + Vector3.up * 0.1f; sd.ReturnYaw = bestYaw;
                log.Add(key + (ok ? "" : "(설 자리 못 찾음)"));
            }
            return "출입문 " + string.Join("·", log);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int i = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, i));
            AssetDatabase.CreateFolder(path.Substring(0, i), path.Substring(i + 1));
        }
    }
}
