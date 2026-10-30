// 행인1의 메인이벤트 — 혜화 MMA 체육관 통합(docs/12_체육관_설계.md 8장 5~7단계)
//   -executeMethod Haengin.EditorGame.GymSetup.Build   (-nographics 빼기: 소품 텍스처 추출에 GPU 필요 — 처음 한 번)
//   들여오기(Art/Env/Gym: 키트 FBX + 텍스처 13 + 소품 GLB 25 LOD) → 재질(UTS, 유리·철망·거울은 URP Lit) → 소품 프리팹 25 → 이윤준·고준 Humanoid + 툰 + 애니메이터(Gym.controller) ·
//   관원 8(적 모델 재사용 + 체육관 옷 색) · 고준 전투 몸(D11) · 스파링 적 정의 → 드릴 데이터(Data/Drill/drills.json → Settings/DrillBook.asset) → 장면 Gym.unity(소켓 115 → 소품·조명·NPC·마커·카메라) → 빌드 목록
// 재질·LOD·외곽선 파이프라인은 맵 아트 패스(EnvSetup) 그대로 재사용.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Haengin.EditorTools;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Haengin.EditorGame
{
    public static partial class GymSetup
    {
        const string Tag = "[GymSetup]";
        public const string Root = "Assets/_Project";
        public const string GymDir = Root + "/Art/Env/Gym";
        public const string KitFbx = GymDir + "/Gym_Hyehwa.fbx";
        public const string ManifestPath = GymDir + "/Gym_Hyehwa_manifest.json";
        public const string PosterJson = GymDir + "/poster_atlas.json";
        public const string TexDir = GymDir + "/Textures", PropDir = GymDir + "/Props";
        public const string MatDir = Root + "/Materials/Env/Gym", PropMatDir = MatDir + "/Props";
        public const string PrefabDir = Root + "/Prefabs/Gym";
        public const string CharDir = Root + "/Art/Characters/M3", ClipDir = Root + "/Art/Clips/M3";
        public const string GymCtrl = Root + "/Anim/Gym.controller";
        public const string BookPath = Root + "/Settings/DrillBook.asset";
        public const string ClipsPath = Root + "/Settings/GymClips.asset";
        public const string GymClipDir = Root + "/Art/Clips/Gym";
        /// 전용 동작(파일 · 상태 · Meshy 번호 · 반복 클립인가)
        static readonly (string file, string state, int id, bool loop)[] GymClipFiles = { ("JumpRope", "JumpRope", 46, true), ("Kettlebell", "Kettlebell", 327, true), ("Burpee", "Burpee", 396, true) };
        public const string SparDefPath = Root + "/Settings/Enemy_Gojun.asset";
        public const string DrillJson = Root + "/Data/Drill/drills.json";
        public const string RingTex = GymDir + "/T_DrillRing.png";
        public static readonly string[] Npcs = { "Yunjun_v3", "Gojun", "Seungyeon_v3", "JinsooSon_v3", "JinsooSeo_v3", "SeungminBaek" };
        /// NPC id · 보이는 이름(출장 레슨 코치 3 = 실존 인물, 본인 허락 2026-10-25 — docs/02 조연 표)
        // 2026-10-30 코치 넷 = *_v3(머리만 새것 — 몸·뼈·동작은 v2 그대로, 텍스처 6144×4096 한 장 → 8192 로 들임). v2 = 2026-10-29 닮음 고침
        public static readonly Dictionary<string, (string id, string name)> NpcNames = new Dictionary<string, (string, string)>
        {
            { "Yunjun_v3", ("yunjun", "이윤준") }, { "Gojun", ("gojun", "고준") }, { "Seungyeon_v3", ("seungyeon", "김승연") }, { "JinsooSon_v3", ("jinsoo_son", "손진수") }, { "JinsooSeo_v3", ("jinsoo_seo", "서진수") }, { "SeungminBaek", ("seungmin_baek", "백승민") },
        };
        public static readonly string[] GuestNpcs = { "Seungyeon_v3", "JinsooSon_v3", "JinsooSeo_v3", "SeungminBaek" };     // 2026-10-29 백승민(코너맨) 추가
        public static readonly List<string> Notes = new List<string>();

        public static void Build() => Run(() => { Assets(); Scene(); M1Setup.SetBuildScenes(); });
        public static void AssetsOnly() => Run(Assets);
        /// 드릴 데이터만(drills.json → DrillBook — 코치 말 cues 를 고친 뒤)
        public static void DrillsOnly() => Run(DrillData);
        public static void SceneOnly() => Run(() => { Scene(); M1Setup.SetBuildScenes(); });
        /// 룩데브(LookdevSetup.Build): 장면만 다시(종료 없이)
        public static void SceneForLookdev() => Scene();

        static void Run(Action body)
        {
            int code = 0;
            var t0 = DateTime.Now;
            try { Notes.Clear(); body(); AssetDatabase.SaveAssets(); Debug.Log($"{Tag} 완료 {(DateTime.Now - t0).TotalSeconds:F1}초\n - " + string.Join("\n - ", Notes)); }
            catch (Exception e) { Debug.LogError($"{Tag} 실패: {e}"); code = 1; }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        static void Assets()
        {
            Import();
            Materials();
            PropPrefabs();
            Characters();
            DrillData();
        }

        // ───────────────────────── json 도우미(EnvSetup 과 같음)
        static Dictionary<string, object> manifest;
        static Dictionary<string, object> Manifest()
        {
            if (manifest != null) return manifest;
            if (!File.Exists(ManifestPath)) throw new Exception("체육관 manifest 없음: " + ManifestPath);
            manifest = D(MiniJson.Parse(File.ReadAllText(ManifestPath)));
            return manifest;
        }
        static Dictionary<string, object> D(object o) => o as Dictionary<string, object>;
        static List<object> L(object o) => o as List<object>;
        static float F(object o, float def = 0f) => o is double d ? (float)d : o is long l ? l : o is int i ? i : def;
        static object G(Dictionary<string, object> d, string k) => d != null && d.TryGetValue(k, out var v) ? v : null;
        static string S(Dictionary<string, object> d, string k) => d != null && d.TryGetValue(k, out var v) && v != null ? v.ToString() : null;
        static bool B(object o, bool def = false) => o is bool b ? b : def;
        static Vector3 V3(object o) { var l = L(o); return l != null && l.Count >= 3 ? new Vector3(F(l[0]), F(l[1]), F(l[2])) : Vector3.zero; }
        static Color Hex(string h, Color def) => !string.IsNullOrEmpty(h) && ColorUtility.TryParseHtmlString(h, out var c) ? c : def;

        // ───────────────────────── 1) 들여오기
        static void Import()
        {
            manifest = null;
            EnsureFolder(MatDir); EnsureFolder(PropMatDir); EnsureFolder(PrefabDir); EnsureFolder(Root + "/Settings"); EnsureFolder(ClipDir);
            var probe = AssetImporter.GetAtPath(KitFbx) as ModelImporter;
            bool stale = probe != null && (probe.bakeAxisConversion || !probe.useFileUnits || probe.importTangents != ModelImporterTangents.CalculateMikk);
            var probeTex = AssetImporter.GetAtPath($"{TexDir}/M_Cage_Mesh.png") as TextureImporter;
            stale |= probeTex != null && !probeTex.alphaIsTransparency;
            AssetDatabase.ImportAsset(GymDir, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceSynchronousImport | (stale ? ImportAssetOptions.ForceUpdate : 0));
            if (stale) Notes.Add("Art/Env/Gym 강제 재임포트(들여오기 설정 적용)");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(KitFbx) == null) throw new Exception("키트 FBX 없음: " + KitFbx);
            // 바닥 먹 원 마커 텍스처(절차)
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(RingTex) == null)
            {
                int n = 256; var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                    {
                        float dx = (x + 0.5f) / n - 0.5f, dy = (y + 0.5f) / n - 0.5f; float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                        float ring = Mathf.Clamp01(1f - Mathf.Abs(r - 0.82f) / 0.11f);
                        float wobble = 0.85f + 0.15f * Mathf.Sin(Mathf.Atan2(dy, dx) * 7f + r * 20f);
                        byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(ring * wobble) * 255f);
                        px[y * n + x] = new Color32(26, 20, 23, a);
                    }
                t.SetPixels32(px); t.Apply();
                File.WriteAllBytes(RingTex, t.EncodeToPNG()); Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(RingTex, ImportAssetOptions.ForceSynchronousImport);
                var ti = (TextureImporter)AssetImporter.GetAtPath(RingTex); ti.alphaIsTransparency = true; ti.wrapMode = TextureWrapMode.Clamp; ti.SaveAndReimport();
            }
            Notes.Add($"키트 FBX · 텍스처 {Directory.GetFiles(TexDir, "*.png").Length} · 소품 GLB {Directory.GetFiles(PropDir, "*.glb").Length} · 캐릭터 FBX {Directory.GetFiles(CharDir, "*.fbx").Length}");
        }

        // ───────────────────────── 2) 재질(docs/12 5-1)
        static readonly Dictionary<string, Material> made = new Dictionary<string, Material>();

        static Material Lit(string path, Texture tex, Color col, bool transparent, bool cutout, bool twoSided, float metallic, float smooth)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(sh) { name = Path.GetFileNameWithoutExtension(path) }; AssetDatabase.CreateAsset(m, path); }
            m.shader = sh;
            m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", col);
            m.SetFloat("_Metallic", metallic); m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Surface", transparent ? 1f : 0f); m.SetFloat("_Blend", 0f);
            m.SetFloat("_AlphaClip", cutout ? 1f : 0f); m.SetFloat("_Cutoff", 0.5f);
            m.SetFloat("_Cull", twoSided ? (float)CullMode.Off : (float)CullMode.Back);
            if (transparent)
            {
                m.SetOverrideTag("RenderType", "Transparent"); m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha); m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = (int)RenderQueue.Transparent;
            }
            else { m.SetOverrideTag("RenderType", cutout ? "TransparentCutout" : "Opaque"); m.SetInt("_SrcBlend", 1); m.SetInt("_DstBlend", 0); m.SetInt("_ZWrite", 1); m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = cutout ? (int)RenderQueue.AlphaTest : -1; }
            if (cutout) m.EnableKeyword("_ALPHATEST_ON"); else m.DisableKeyword("_ALPHATEST_ON");
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        static void Materials()
        {
            made.Clear();
            var mats = D(Manifest()["materials"]);
            foreach (var kv in mats)
            {
                string name = kv.Key; var info = D(kv.Value);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{name}.png") ?? AssetDatabase.LoadAssetAtPath<Texture2D>($"{EnvSetup.TexDir}/{name}.png");
                float tiling = F(G(info, "tiling"), 1f); if (tiling <= 0f) tiling = 1f;
                Color col = Hex(S(info, "color"), Color.gray);
                string path = $"{MatDir}/{name}.mat";
                Material m;
                switch (name)
                {
                    case "M_Cage_Mesh": m = Lit(path, tex, Color.white, false, true, true, 0.2f, 0.2f); m.SetTextureScale("_BaseMap", Vector2.one * tiling); break;
                    case "M_Glass_Clear": m = Lit(path, null, new Color(0.86f, 0.90f, 0.92f, 0.16f), true, false, false, 0f, 0.85f); break;
                    case "M_Mirror":
                        {
                            // 거울 판: 꺼져 있을 땐 M_Mirror 결 그대로(Unlit), 드릴 중엔 MirrorPlane 이 반사 RT 로 바꿔 끼움
                            var ush = Shader.Find("Universal Render Pipeline/Unlit");
                            m = AssetDatabase.LoadAssetAtPath<Material>(path);
                            if (m == null) { m = new Material(ush) { name = "M_Mirror" }; AssetDatabase.CreateAsset(m, path); }
                            m.shader = ush; m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", new Color(0.50f, 0.52f, 0.55f, 1f)); EditorUtility.SetDirty(m);
                            break;
                        }
                    case "M_Fluorescent":
                        m = EnvSetup.Toon(path, tex, Vector2.one, Vector2.zero, Color.white);
                        m.EnableKeyword("_EMISSIVE_SIMPLE"); m.SetTexture("_Emissive_Tex", tex); m.SetColor("_Emissive_Color", GymLights.FluorColor * 1.4f); break;
                    case "M_Timer_Red":
                        m = EnvSetup.Toon(path, tex, Vector2.one, Vector2.zero, Color.white);
                        m.EnableKeyword("_EMISSIVE_SIMPLE"); m.SetTexture("_Emissive_Tex", tex); m.SetColor("_Emissive_Color", new Color(1.2f, 0.25f, 0.2f)); break;
                    case "M_Poster_Atlas": m = EnvSetup.Toon(path, tex, Vector2.one, Vector2.zero, Color.white); break;
                    case "M_Cage_Canvas": m = EnvSetup.Toon(path, tex, Vector2.one * tiling, Vector2.zero, new Color(0.84f, 0.83f, 0.81f)); break;   // 스포트 아래 하얗게 날아가지 않게
                    default:
                        bool unit = S(info, "uv") == "unit";
                        m = EnvSetup.Toon(path, tex, unit ? Vector2.one : Vector2.one * tiling, Vector2.zero, tex != null ? Color.white : col); break;
                }
                made[name] = m;
            }
            // 포스터 칸 재질(아틀라스 uv 사각형 → 타일/오프셋)
            if (File.Exists(PosterJson))
            {
                var pj = D(MiniJson.Parse(File.ReadAllText(PosterJson)));
                var atlasTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/M_Poster_Atlas.png");
                foreach (var kv in D(G(pj, "cells")))
                {
                    var uv = L(G(D(kv.Value), "uv"));
                    if (uv == null || uv.Count < 4) continue;
                    float u0 = F(uv[0]), v0 = F(uv[1]), u1 = F(uv[2]), v1 = F(uv[3]);
                    var pm = EnvSetup.Toon($"{MatDir}/M_Poster_{kv.Key}.mat", atlasTex, new Vector2(u1 - u0, v1 - v0), new Vector2(u0, v0), Color.white);
                    made["M_Poster_" + kv.Key] = pm;
                }
            }
            // 키트 FBX 재질 → Materials/Env/Gym 바꿔 꿰기
            var imp = AssetImporter.GetAtPath(KitFbx) as ModelImporter;
            int remapped = 0;
            if (imp != null)
            {
                var map = imp.GetExternalObjectMap();
                var names = AssetDatabase.LoadAllAssetRepresentationsAtPath(KitFbx).OfType<Material>().Select(x => x.name).ToList();
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(KitFbx);
                foreach (var r in go.GetComponentsInChildren<Renderer>(true)) foreach (var sm in r.sharedMaterials) if (sm != null && !names.Contains(sm.name)) names.Add(sm.name);
                bool dirty = false;
                foreach (var nm in names.Distinct())
                {
                    if (!made.TryGetValue(nm, out var target)) { if (nm != "No Name" && nm != "Default-Material") Notes.Add($"키트 재질 '{nm}' 은 manifest 에 없음"); continue; }
                    var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), nm);
                    if (map.TryGetValue(id, out var cur) && cur == target) continue;
                    imp.AddRemap(id, target); dirty = true; remapped++;
                }
                if (dirty) imp.SaveAndReimport();
            }
            Notes.Add($"재질 {made.Count}개 · 키트 재질 바꿔 꿴 것 {remapped}");
        }

        public static Material Mat(string name) => made.TryGetValue(name, out var m) ? m : AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/{name}.mat");

        // ───────────────────────── 3) 소품 프리팹(EnvSetup.PropPrefabs 와 같은 규칙: LOD 3단 · UTS · 인스턴싱 · 그림자 LOD0)
        static Texture ExtractedTexture(Texture src, string name)
        {
            string dir = PropDir + "/Tex"; EnsureFolder(dir);
            string png = $"{dir}/{name}.png";
            var have = AssetDatabase.LoadAssetAtPath<Texture2D>(png);
            if (have != null) return have;
            if (src == null || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) { if (src != null) Notes.Add($"소품 텍스처 추출 못 함(그래픽 없음): {name} — -nographics 없이 GymSetup.AssetsOnly 한 번"); return null; }
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
                if (mfs.Count == 0) { Notes.Add($"소품 메시 없음: {name}"); continue; }
                MeshFilter Lod(int k) => mfs.FirstOrDefault(m => m.name.EndsWith("_LOD" + k) || m.sharedMesh.name.EndsWith("_LOD" + k)) ?? (k == 0 ? mfs[0] : null);
                var lod0 = Lod(0); var lod0R = lod0.GetComponent<MeshRenderer>(); var srcMat = lod0R != null ? lod0R.sharedMaterial : null;
                Texture tex = null;
                if (srcMat != null) foreach (var id in new[] { "baseColorTexture", "_BaseMap", "_MainTex" }) if (srcMat.HasProperty(id) && srcMat.GetTexture(id) != null) { tex = srcMat.GetTexture(id); break; }
                if (tex == null && srcMat != null) tex = srcMat.mainTexture;
                tex = ExtractedTexture(tex, name) ?? tex;
                var mat = EnvSetup.Toon($"{PropMatDir}/M_P_{name}.mat", tex, Vector2.one, Vector2.zero, Color.white);
                var root = new GameObject("P_" + name);
                var lg = root.AddComponent<LODGroup>();
                var lods = new List<LOD>();
                var b = lod0.sharedMesh.bounds; float size = Mathf.Max(b.size.x, b.size.y, b.size.z) * Mathf.Max(lod0.transform.lossyScale.x, 0.001f);
                float H(float dist) => Mathf.Clamp(size / (dist * 0.8284f), 0.002f, 0.95f);
                float[] trans = { H(size < 1f ? 6f : 10f), H(size < 1f ? 14f : 25f), 0.004f };     // 실내 20m: 컬링은 거의 안 일어남
                for (int k = 0; k < 3; k++)
                {
                    var mf = Lod(k); if (mf == null) break;
                    var go = new GameObject("LOD" + k); go.transform.SetParent(root.transform, false);
                    go.transform.SetPositionAndRotation(mf.transform.position - src.transform.position, mf.transform.rotation); go.transform.localScale = mf.transform.lossyScale;
                    go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                    var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
                    r.shadowCastingMode = k == 0 ? ShadowCastingMode.On : ShadowCastingMode.Off; r.receiveShadows = true;
                    GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.OccludeeStatic);
                    lods.Add(new LOD(trans[Mathf.Min(k, 2)], new Renderer[] { r }));
                    if (k == 0) tris += mf.sharedMesh.triangles.Length / 3;
                }
                if (lods.Count > 0) { var last = lods[lods.Count - 1]; last.screenRelativeTransitionHeight = trans[2]; lods[lods.Count - 1] = last; }
                lg.SetLODs(lods.ToArray()); lg.fadeMode = LODFadeMode.None; lg.RecalculateBounds();
                GameObjectUtility.SetStaticEditorFlags(root, StaticEditorFlags.OccludeeStatic);
                PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/P_{name}.prefab");
                Object.DestroyImmediate(root); n++;
            }
            Notes.Add($"소품 프리팹 {n}개(LOD0 삼각형 합 {tris:N0})");
        }

        // ───────────────────────── 4) 캐릭터: 이윤준·고준 Humanoid + 툰, Gym.controller(기존 전투 클립 + ChairSit), 관원 8, 고준 전투 몸, 스파링 적 정의
        public static string NpcFbx(string n) => $"{CharDir}/{n}.fbx";
        public static string NpcTex(string n) => $"{CharDir}/{n}Tex.jpg";
        public static string NpcMat(string n) => $"{Root}/Materials/M_{n}_Toon.mat";
        public static string NpcPrefab(string n) => $"{PrefabDir}/Npc_{n}.prefab";
        public static string MemberPrefab(int i) => $"{PrefabDir}/Npc_Member_{i + 1:00}.prefab";
        public const string GojunFighterPrefab = PrefabDir + "/Gojun_Fighter.prefab";
        static readonly string[] GymStates = { "Stance", "Jab", "Cross", "Hook", "Upper", "Dodge", "Kneel", "Taunt", "Tackle", "Block", "Breath", "Charge", "FwdWalk" };
        static readonly string[] LoopStates = { "Stance", "Idle", "ChairSit", "Breath", "FwdWalk", "Kneel", "JumpRope", "Kettlebell", "Burpee" };

        /// 색·그늘 텍스처: 원본이 2048 보다 크면 4096 · PC 는 BC7(얼굴 글자·눈매가 뭉개지지 않게)
        static void HiTex(string path, bool srgb, int max)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) return;
            ti.GetSourceTextureWidthAndHeight(out int w, out int h);
            // 2026-10-30 v3: 몸 4096 + 머리 2048 을 한 장(6144×4096)에 — 8192 로 들여야 안 줄어듦
            int src = Mathf.Max(w, h);
            int want = src > 4096 ? 8192 : src > 2048 ? Mathf.Min(max, 4096) : 2048;
            CharSetup.Texture(path, srgb, want);
            // 2026-10-30: 6144×4096 v3 텍스처 여덟 장 — 밉맵 스트리밍(필요한 밉만 올림, 품질 설정 streamingMipmapsActive) — 프레임 시간 시험 W16
            if (want > 4096 && !ti.streamingMipmaps) { ti.streamingMipmaps = true; ti.SaveAndReimport(); }
            var ps = ti.GetPlatformTextureSettings("Standalone");
            if (want > 2048 && (!ps.overridden || ps.format != TextureImporterFormat.BC7 || ps.maxTextureSize != want))
            {
                ps.overridden = true; ps.format = TextureImporterFormat.BC7; ps.maxTextureSize = want;
                ti.SetPlatformTextureSettings(ps); ti.SaveAndReimport();
            }
        }

        static AnimationClip Clip(string fbx, string name) => AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__") && (name == null || c.name == name));

        static void Characters()
        {
            foreach (var n in Npcs)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(NpcFbx(n)) == null) { Notes.Add($"NPC 모델 없음: {NpcFbx(n)}"); continue; }
                // 2026-10-29: 코치 텍스처는 원본 4096 — 2048 로 줄여 얼굴이 약 240px 이던 것 → 4096(BC7)
                HiTex(NpcTex(n), true, 4096);
                CharSetup.Human(NpcFbx(n), "Idle", -1, -1, false, null);
                CharSetup.Ground(NpcFbx(n), "Idle", NpcFbx(n));
                CharSetup.Toon(CharSetup.Load<GameObject>(NpcFbx(n)), NpcMat(n), NpcTex(n));
                // 그늘 지도·외곽선 굵기 지도가 있으면(이윤준 v2) 정식 모델 몸 재질과 같게(CharSetup.FormalToon)
                string shadeP = $"{CharDir}/{n}Shade.jpg", outP = $"{CharDir}/{n}OutW.png";
                if (AssetDatabase.LoadAssetAtPath<Texture2D>(shadeP) != null)
                {
                    HiTex(shadeP, true, 4096);
                    if (AssetImporter.GetAtPath(outP) is TextureImporter oti) { oti.GetSourceTextureWidthAndHeight(out int ow, out int oh); CharSetup.Texture(outP, false, Mathf.Clamp(Mathf.Max(ow, oh), 1024, 2048)); }
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(NpcMat(n));
                    var shade = AssetDatabase.LoadAssetAtPath<Texture2D>(shadeP);
                    var outW = AssetDatabase.LoadAssetAtPath<Texture2D>(outP);
                    mat.SetFloat("_Use_BaseAs1st", 0f); mat.SetTexture("_1st_ShadeMap", shade); mat.SetColor("_1st_ShadeColor", Color.white);
                    mat.SetFloat("_Use_1stAs2nd", 0f); mat.SetTexture("_2nd_ShadeMap", shade); mat.SetColor("_2nd_ShadeColor", new Color(0.88f, 0.86f, 0.88f, 1f));
                    if (outW != null) mat.SetTexture("_Outline_Sampler", outW);
                    EditorUtility.SetDirty(mat);
                    Notes.Add($"{n}: 그늘 지도·외곽선 굵기 지도 재질");
                }
            }
            // 앉기 클립(Chair_Sit_Idle_M 33, 시우 리그 대리 메시) → Humanoid
            string sit = $"{ClipDir}/ChairSit.fbx";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(sit) != null) CharSetup.Human(sit, "ChairSit", -1, -1, false, null);
            // Gym.controller: 전투 클립 상태 + ChairSit + Idle(기본 대기 — 깐족이 Idle, NPC 마다 Override)
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(GymCtrl) ?? AnimatorController.CreateAnimatorControllerAtPath(GymCtrl);
            var sm = ctrl.layers[0].stateMachine;
            AnimatorState St(string name, AnimationClip clip)
            {
                var st = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == name) ?? sm.AddState(name);
                st.motion = clip; st.writeDefaultValues = true; st.iKOnFeet = false;
                return st;
            }
            var baseIdle = Clip(ClipSetup.EnemyFbx(ClipSetup.Enemies[0]), "Idle");
            var idleSt = St("Idle", baseIdle);
            St("Stance", Clip($"{ClipSetup.ClipDir}/Stance.fbx", null));
            foreach (var s in GymStates.Skip(1)) { var c = Clip($"{ClipSetup.ClipDir}/{s}.fbx", null); if (c != null) St(s, c); }
            var sitClip = Clip(sit, "ChairSit"); if (sitClip != null) St("ChairSit", sitClip);
            // 전용 동작 3종(있을 때만): Gym.controller(NPC) + Siwoo.controller(플레이어 ScriptAnim)
            var clipsAsset = AssetDatabase.LoadAssetAtPath<GymClips>(ClipsPath);
            if (clipsAsset == null) { clipsAsset = ScriptableObject.CreateInstance<GymClips>(); AssetDatabase.CreateAsset(clipsAsset, ClipsPath); }
            var entries = new List<GymClips.Entry>();
            var siwooCtrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(CharSetup.SiwooCtrl);
            foreach (var (file, state, id, loop) in GymClipFiles)
            {
                string fbx = $"{GymClipDir}/{file}.fbx";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(fbx) == null) { Notes.Add($"전용 동작 없음(보류): {fbx}"); continue; }
                CharSetup.Human(fbx, state, -1, -1, false, null);
                var clip = Clip(fbx, state); if (clip == null) continue;
                St(state, clip);
                if (siwooCtrl != null)
                {
                    var ssm = siwooCtrl.layers[0].stateMachine;
                    var st2 = ssm.states.Select(s => s.state).FirstOrDefault(s => s.name == state) ?? ssm.AddState(state);
                    st2.motion = clip; st2.writeDefaultValues = true; st2.speedParameter = "AtkRate"; st2.speedParameterActive = true; st2.iKOnFeet = false;
                    EditorUtility.SetDirty(siwooCtrl);
                }
                int cycles = Cycles(clip);
                entries.Add(new GymClips.Entry { State = state, Id = id, Length = clip.length, Cycles = cycles });
                Notes.Add($"전용 동작 {state}({id}): {clip.length:F2}s · 반복 {cycles}");
            }
            clipsAsset.Entries = entries.ToArray(); EditorUtility.SetDirty(clipsAsset);
            sm.defaultState = sm.states.Select(s => s.state).First(s => s.name == "Stance");
            EditorUtility.SetDirty(ctrl);
            // 비반복 동작은 끝나면 Stance 로(전이: 종료 시간)
            foreach (var s in sm.states.Select(x => x.state))
            {
                if (System.Array.IndexOf(LoopStates, s.name) >= 0) continue;
                if (s.transitions.Any(t => t.destinationState != null && t.destinationState.name == "Stance")) continue;
                var tr = s.AddTransition(sm.states.Select(x => x.state).First(x => x.name == "Stance"));
                tr.hasExitTime = true; tr.exitTime = 0.95f; tr.duration = 0.1f;
            }
            // NPC 프리팹(이윤준·고준: 자기 Idle 로 Override)
            var table = AssetDatabase.LoadAssetAtPath<ClipTable>(ClipSetup.TablePath);
            foreach (var n in Npcs)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(NpcFbx(n)); if (model == null) continue;
                var oc = Override($"{Root}/Anim/Gym_{n}.overrideController", ctrl, baseIdle, Clip(NpcFbx(n), "Idle"));
                var go = NpcInstance(model, NpcMat(n), oc, CharSetup.AvatarOf(NpcFbx(n)));
                go.name = "Npc_" + n;
                var npc = go.AddComponent<GymNpc>(); npc.Id = NpcNames[n].id; npc.DisplayName = NpcNames[n].name; npc.Anim = go.GetComponentInChildren<Animator>();
                PrefabUtility.SaveAsPrefabAsset(go, NpcPrefab(n)); Object.DestroyImmediate(go);
            }
            // 관원 8: 적 모델 4 × 옷 색 2(체육관 옷 = 티셔츠 색 tint)
            Color[] tints = { new Color(0.62f, 0.66f, 0.72f), new Color(0.80f, 0.74f, 0.60f), new Color(0.55f, 0.62f, 0.58f), new Color(0.74f, 0.58f, 0.56f), new Color(0.86f, 0.86f, 0.84f), new Color(0.50f, 0.52f, 0.60f), new Color(0.70f, 0.72f, 0.66f), new Color(0.64f, 0.56f, 0.66f) };
            for (int i = 0; i < 8; i++)
            {
                string en = ClipSetup.Enemies[i % ClipSetup.Enemies.Length];
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(ClipSetup.EnemyFbx(en)); if (model == null) continue;
                var baseMat = AssetDatabase.LoadAssetAtPath<Material>(ClipSetup.EnemyMat(en));
                string mp = $"{MatDir}/M_Member_{i + 1:00}.mat";
                var mm = AssetDatabase.LoadAssetAtPath<Material>(mp);
                if (mm == null && baseMat != null) { mm = new Material(baseMat) { name = Path.GetFileNameWithoutExtension(mp) }; AssetDatabase.CreateAsset(mm, mp); }
                if (mm != null) { mm.SetColor("_BaseColor", tints[i]); mm.SetColor("_Color", tints[i]); EditorUtility.SetDirty(mm); }
                var oc = Override($"{Root}/Anim/Gym_{en}.overrideController", ctrl, baseIdle, Clip(ClipSetup.EnemyFbx(en), "Idle"));
                var go = NpcInstance(model, mp, oc, CharSetup.AvatarOf(ClipSetup.EnemyFbx(en)));
                foreach (var rr in go.GetComponentsInChildren<Renderer>(true)) rr.shadowCastingMode = ShadowCastingMode.Off;   // 관원 8 은 그림자 없음(실내 삼각형 예산)
                go.name = $"Npc_Member_{i + 1:00}";
                var npc = go.AddComponent<GymNpc>(); npc.Id = $"member{i + 1}"; npc.DisplayName = "관원"; npc.Anim = go.GetComponentInChildren<Animator>();
                PrefabUtility.SaveAsPrefabAsset(go, MemberPrefab(i)); Object.DestroyImmediate(go);
            }
            // 고준 전투 몸(D11): 적 프리팹 규칙(FighterAnim + HandShape, Enemy.controller Override)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(NpcFbx("Gojun"));
                var enemyCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ClipSetup.EnemyCtrl);
                if (model != null && enemyCtrl != null)
                {
                    var go = NpcInstance(model, NpcMat("Gojun"), Override($"{Root}/Anim/Enemy_Gojun.overrideController", enemyCtrl, baseIdle, Clip(NpcFbx("Gojun"), "Idle")), CharSetup.AvatarOf(NpcFbx("Gojun")));
                    go.name = "Gojun_Fighter";
                    var anim = go.GetComponentInChildren<Animator>(); anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    var fa = go.AddComponent<FighterAnim>(); fa.Clips = table;
                    go.AddComponent<HandShape>();
                    PrefabUtility.SaveAsPrefabAsset(go, GojunFighterPrefab); Object.DestroyImmediate(go);
                }
            }
            SparDefAsset();
            Notes.Add($"캐릭터: {string.Join("·", Npcs)} Humanoid+툰 · Gym.controller 상태 {sm.states.Length} · 관원 8 · 고준 전투 몸 · 스파링 정의");
        }

        /// 클립 안 반복 수: 골반 높이(RootT.y) 곡선의 '아래 극소' 개수(줄넘기 점프 1회 = 극소 1). 곡선이 없으면 1
        static int Cycles(AnimationClip clip)
        {
            var bindings = AnimationUtility.GetCurveBindings(clip);
            var b = bindings.FirstOrDefault(x => x.propertyName == "RootT.y");
            if (string.IsNullOrEmpty(b.propertyName)) return 1;
            var c = AnimationUtility.GetEditorCurve(clip, b);
            int n = 120; var v = new float[n];
            for (int i = 0; i < n; i++) v[i] = c.Evaluate(clip.length * i / n);
            float min = v.Min(), max = v.Max(); if (max - min < 0.01f) return 1;
            float th = min + (max - min) * 0.35f;
            int cycles = 0; bool below = false;
            for (int i = 0; i < n; i++) { if (v[i] < th && !below) { cycles++; below = true; } else if (v[i] >= th) below = false; }
            return Mathf.Max(1, cycles);
        }

        static GameObject NpcInstance(GameObject model, string matPath, RuntimeAnimatorController ctrl, Avatar avatar)
        {
            var inst = (PrefabUtility.InstantiatePrefab(model) as GameObject) ?? Object.Instantiate(model);
            var root = new GameObject("npc");
            inst.name = "Model"; inst.transform.SetParent(root.transform, false);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                if (mat != null) { var ms = r.sharedMaterials; for (int i = 0; i < ms.Length; i++) ms[i] = mat; r.sharedMaterials = ms; }
                r.shadowCastingMode = ShadowCastingMode.On; r.receiveShadows = true;
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = false;
            }
            foreach (var c in inst.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            var anim = inst.GetComponent<Animator>() ?? inst.AddComponent<Animator>();
            anim.avatar = avatar; anim.runtimeAnimatorController = ctrl; anim.applyRootMotion = false; anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            return root;
        }

        static RuntimeAnimatorController Override(string path, RuntimeAnimatorController ctrl, AnimationClip baseIdle, AnimationClip own)
        {
            if (ctrl == null) return null;
            if (baseIdle == null || own == null || baseIdle == own) return ctrl;
            var oc = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
            if (oc == null) { oc = new AnimatorOverrideController(ctrl); AssetDatabase.CreateAsset(oc, path); }
            oc.runtimeAnimatorController = ctrl; oc[baseIdle] = own;
            EditorUtility.SetDirty(oc);
            return oc;
        }

        /// 스파링 상대 고준(docs/12 4-2 D11): 석 달 틀 + 스파링 행동 — 약한 기술만(머리 강타 금지), 막기형, 도주·도발 없음
        public static EnemyDef SparDefAsset()
        {
            var a = AssetDatabase.LoadAssetAtPath<EnemyDef>(SparDefPath);
            if (a != null) return a;
            var d = EnemyLib.Seokdal();
            d.name = "Enemy_Gojun"; d.Label = "고준"; d.Height = 1.76f; d.Radius = 0.30f; d.Hp = 120;
            d.TauntChance = 0f; d.FleeHp = 0f; d.Counter = null; d.Far = null; d.FarChance = 0f; d.Cooldown = 1.3f;
            var jab = MoveLib.SdJab(); jab.Power = Power.Light; jab.Damage = 3; jab.Heat = 0;
            var oneTwo = MoveLib.SdOneTwo(); oneTwo.Power = Power.Light; oneTwo.Damage = 3; oneTwo.Heat = 0;
            if (oneTwo.Followup != null) { oneTwo.Followup.Power = Power.Light; oneTwo.Followup.Damage = 4; oneTwo.Followup.Down = false; }
            var body = MoveLib.SdJab(); body.name = "SdBody"; body.Label = "바디 잽"; body.Flinch = FlinchKind.Body; body.Power = Power.Light; body.Damage = 3; body.Heat = 0;
            d.Moves = new[] { jab, oneTwo, body }; d.Weights = new[] { 0.5f, 0.3f, 0.2f };
            d.Moves2 = new MoveDef[0];
            AssetDatabase.CreateAsset(d, SparDefPath);
            void Add(MoveDef m) { if (m == null) return; AssetDatabase.AddObjectToAsset(m, d); if (m.Followup != null) Add(m.Followup); }
            foreach (var m in d.Moves) Add(m);
            EditorUtility.SetDirty(d); AssetDatabase.SaveAssets();
            return d;
        }

        // ───────────────────────── 5) 드릴 데이터(drills.json → DrillBook)
        static void DrillData()
        {
            if (!File.Exists(DrillJson)) throw new Exception("드릴 데이터 없음: " + DrillJson);
            var j = D(MiniJson.Parse(File.ReadAllText(DrillJson)));
            var list = new List<DrillDef>();
            foreach (var o in L(G(j, "drills")))
            {
                var d = D(o);
                var def = new DrillDef
                {
                    Id = S(d, "id"), NameKr = S(d, "name"), RealName = S(d, "real") ?? "",
                    Category = Enum.TryParse(S(d, "category"), out DrillCategory cat) ? cat : DrillCategory.Condition,
                    Rounds = (int)F(G(d, "rounds"), 1), RoundSec = F(G(d, "roundSec")), RestSec = F(G(d, "restSec")),
                    FatigueCost = (int)F(G(d, "fatigue")), SweatBase = (int)F(G(d, "sweat")),
                    MainStat = Enum.TryParse(S(d, "main"), out Stat ms) ? ms : Stat.None, SubStat = Enum.TryParse(S(d, "sub"), out Stat ss) ? ss : Stat.None,
                    Socket = S(d, "socket") ?? "", CamPreset = S(d, "cam") ?? "front",
                    UsesCombat = B(G(d, "combat")), SimAllowed = B(G(d, "sim"), true), Playable = B(G(d, "playable"), true), HeadRisk = B(G(d, "headRisk")),
                };
                var ms2 = new List<MetricDef>();
                foreach (var mo in L(G(d, "metrics"))) { var m = D(mo); ms2.Add(new MetricDef { Id = S(m, "id"), LabelKr = S(m, "label"), Unit = S(m, "unit") ?? "", HigherIsBetter = B(G(m, "higher")), D = F(G(m, "d")), S = F(G(m, "s")), Weight = F(G(m, "w")) }); }
                def.Metrics = ms2.ToArray();
                var cues = D(G(d, "cues"));
                def.CueStart = S(cues, "start") ?? ""; def.CueRound = S(cues, "round") ?? ""; def.CueS = S(cues, "s") ?? ""; def.CueGojun = S(cues, "gojun") ?? ""; def.CueStop = S(cues, "stop") ?? ""; def.CueHurt = S(cues, "hurt") ?? "";
                float wsum = def.Metrics.Sum(m => m.Weight);
                if (Mathf.Abs(wsum - 1f) > 1e-3f) Notes.Add($"드릴 {def.Id} 가중치 합 {wsum:F2}(1 이어야)");
                list.Add(def);
            }
            var book = AssetDatabase.LoadAssetAtPath<DrillBook>(BookPath);
            if (book == null) { book = ScriptableObject.CreateInstance<DrillBook>(); AssetDatabase.CreateAsset(book, BookPath); }
            book.Drills = list.ToArray();
            EditorUtility.SetDirty(book);
            Notes.Add($"드릴 {list.Count}종(플레이 가능 {list.Count(x => x.Playable)}) → {BookPath}");
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
