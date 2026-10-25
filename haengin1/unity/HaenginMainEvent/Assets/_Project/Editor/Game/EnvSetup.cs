// 행인1의 메인이벤트 — 맵 아트 패스 Unity 통합(docs/11_건물생성기.md 7장 체크리스트 · docs/10_맵_아트패스.md P1·P3 · docs/09 3-5)
//   배치: -executeMethod Haengin.EditorGame.EnvSetup.Build        에셋(재질·소품 프리팹·원경·외곽선·그림자·조명 프리셋) + Zone1 다시 만들기(키트 세우기)
//         -executeMethod Haengin.EditorGame.EnvSetup.AssetsOnly   에셋만
//         -executeMethod Haengin.EditorGame.EnvSetup.Shots -out <폴더> [-only 1,7]   비교 사진(그래픽 필요 — -nographics 빼기)
//   Zone1 쪽은 M1Setup.AttachRig 끝에서 AttachKit 이 붙는다(Zone1Builder.Build 만 돌려도 됨 — 결정적 저장 그대로).
//   입력: Art/Env/Buildings/Z1_*.fbx + zone1_buildings_manifest.json(소켓·간판·창문 불·빛 자리) · Art/Env/Props/*.glb(LOD 3단, tools/env/prop_lods.py) ·
//         Art/Env/Textures/M_*.png · Art/Env/Sky/* · Art/Env/Signs/sign_atlas.json
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
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Haengin.EditorGame
{
    public static partial class EnvSetup
    {
        const string Tag = "[EnvSetup]";
        public const string Root = "Assets/_Project";
        public const string EnvDir = Root + "/Art/Env";
        public const string BuildDir = EnvDir + "/Buildings", TexDir = EnvDir + "/Textures", PropDir = EnvDir + "/Props", SkyDir = EnvDir + "/Sky", SignDir = EnvDir + "/Signs";
        public const string MatDir = Root + "/Materials/Env", PropMatDir = MatDir + "/Props", SignMatDir = MatDir + "/Signs", SkyMatDir = MatDir + "/Sky";
        public const string PrefabDir = Root + "/Prefabs/Env";
        public const string ManifestPath = BuildDir + "/zone1_buildings_manifest.json";
        public const string ArcMeshPath = SkyDir + "/SkyArc.asset";
        public const string OutlineMatPath = Root + "/Materials/Fx/M_Outline.mat";
        static readonly string[] Districts = { "HoomunN", "HoomunS", "Alley", "Campus", "Waryong", "Wall", "Wires" };
        static readonly string[] Renderers = { "Assets/Settings/PC_Renderer.asset", "Assets/Settings/Mobile_Renderer.asset" };
        static readonly string[] Pipelines = { "Assets/Settings/PC_RPAsset.asset", "Assets/Settings/Mobile_RPAsset.asset" };
        static readonly Color ShadeMul = new Color(0.82f, 0.785f, 0.75f);     // FUJIMOTO 8-3 배경 곱셈(.78, .76, .74) → 룩데브 2026-10-26: 조금 밝고 따뜻하게(프레임 그늘 L* 41~47 · a* +3~6)
        public static readonly List<string> Notes = new List<string>();

        public static bool KitAvailable => File.Exists(ManifestPath) && AssetDatabase.LoadAssetAtPath<GameObject>(BuildDir + "/Z1_HoomunN.fbx") != null;

        // ───────────────────────── 배치 진입
        public static void Build() => Run(() => { Assets(); Zone1Builder.Rebuild(); });
        public static void AssetsOnly() => Run(Assets);

        static void Run(Action body)
        {
            int code = 0;
            Notes.Clear();
            var t0 = DateTime.Now;
            try { body(); AssetDatabase.SaveAssets(); }
            catch (Exception e) { Debug.LogError($"{Tag} 실패: {e}"); code = 1; }
            Debug.Log($"{Tag} 끝({(DateTime.Now - t0).TotalSeconds:F1}초) · 메모 {Notes.Count}건");
            foreach (var n in Notes) Debug.Log($"{Tag}   {n}");
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        /// 에셋 전부: 들여오기 → 재질 → 소품 프리팹 → 원경 → 외곽선 렌더러 기능 → 그림자 캐스케이드 → 조명 프리셋 → 캐릭터 스텐실
        public static void Assets()
        {
            foreach (var d in new[] { MatDir, PropMatDir, SignMatDir, SkyMatDir, PrefabDir, Root + "/Materials/Fx" }) EnsureFolder(d);
            Import();
            Materials();
            CopyClumpTextures();
            PropPrefabs();
            SkyAssets();
            OutlineFeature();
            Pipeline();
            Presets();
            CharacterStencil();
        }

        // ───────────────────────── 1) 들여오기 + 재질 바꿔 꿰기
        static Dictionary<string, object> manifest;
        static Dictionary<string, object> Manifest()
        {
            if (manifest != null) return manifest;
            if (!File.Exists(ManifestPath)) throw new FileNotFoundException(ManifestPath);
            manifest = (Dictionary<string, object>)MiniJson.Parse(File.ReadAllText(ManifestPath, System.Text.Encoding.UTF8));
            return manifest;
        }
        static Dictionary<string, object> D(object o) => o as Dictionary<string, object>;
        static List<object> L(object o) => o as List<object>;
        static float F(object o, float def = 0f) => o is double d ? (float)d : o is long l ? l : o is int i ? i : def;
        static object G(Dictionary<string, object> d, string k) => d != null && d.TryGetValue(k, out var v) ? v : null;
        static string S(Dictionary<string, object> d, string k) => d != null && d.TryGetValue(k, out var v) && v != null ? v.ToString() : null;
        static Vector3 V3(object o) { var l = L(o); return l != null && l.Count >= 3 ? new Vector3(F(l[0]), F(l[1]), F(l[2])) : Vector3.zero; }
        static Color Hex(string h, Color def) { return !string.IsNullOrEmpty(h) && ColorUtility.TryParseHtmlString(h, out var c) ? c : def; }

        static void Import()
        {
            // 들여오기 설정(EnvImport)이 아직 안 먹은 상태(처음 들여왔을 때 스크립트보다 먼저 임포트됨)면 폴더를 강제로 다시 들여온다
            var probeTex = AssetImporter.GetAtPath($"{TexDir}/M_Brick_Red.png") as TextureImporter;
            var probeFbx = AssetImporter.GetAtPath($"{BuildDir}/Z1_HoomunN.fbx") as ModelImporter;
            bool stale = (probeTex != null && (probeTex.wrapMode != TextureWrapMode.Repeat || probeTex.textureCompression != TextureImporterCompression.CompressedHQ))
                      || (probeFbx != null && (probeFbx.bakeAxisConversion || !probeFbx.useFileUnits || probeFbx.importVisibility || probeFbx.importTangents != ModelImporterTangents.CalculateMikk));
            var opt = ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceSynchronousImport | (stale ? ImportAssetOptions.ForceUpdate : 0);
            AssetDatabase.ImportAsset(EnvDir, opt);
            if (stale) Notes.Add("Art/Env 를 강제로 다시 들여옴(들여오기 설정 적용)");
            int n = 0;
            foreach (var d in Districts)
            {
                string p = $"{BuildDir}/Z1_{d}.fbx";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(p) == null) { Notes.Add($"FBX 없음: {p}"); continue; }
                n++;
            }
            Notes.Add($"건물 FBX {n}/7 · 소품 GLB {Directory.GetFiles(PropDir, "*.glb").Length}개 · 텍스처 {Directory.GetFiles(TexDir, "*.png").Length}장");
        }

        /// UTS(Toon/Toon) 배경 재질: 1단 그림자 = 고유색 × (.78,.76,.74), 스펙큘러·림·헐 외곽선 없음, 주변광은 Unlit_Intensity 1
        public static Material Toon(string path, Texture tex, Vector2 tiling, Vector2 offset, Color baseColor, bool instancing = true)
        {
            var sh = Shader.Find("Toon/Toon") ?? throw new Exception("Toon/Toon 셰이더(UTS) 없음");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(sh) { name = Path.GetFileNameWithoutExtension(path) }; AssetDatabase.CreateAsset(m, path); }
            if (m.shader != sh) m.shader = sh;
            foreach (var id in new[] { "_MainTex", "_BaseMap" })
            {
                m.SetTexture(id, tex); m.SetTextureScale(id, tiling); m.SetTextureOffset(id, offset);
            }
            m.SetColor("_BaseColor", baseColor); m.SetColor("_Color", baseColor);
            m.SetFloat("_Use_BaseAs1st", 1f); m.SetFloat("_Use_1stAs2nd", 1f);
            m.SetColor("_1st_ShadeColor", ShadeMul); m.SetColor("_2nd_ShadeColor", ShadeMul);
            m.SetFloat("_BaseColor_Step", 0.5f); m.SetFloat("_BaseShade_Feather", 0.02f);
            m.SetFloat("_ShadeColor_Step", 0.1f); m.SetFloat("_1st2nd_Shades_Feather", 0.02f);   // 0·0 이면 2단 마스크가 0 으로 나눠 NaN → 그늘 면이 검게(2026-10-24 실측)
            m.SetFloat("_Set_SystemShadowsToBase", 1f); m.SetFloat("_Tweak_SystemShadowsLevel", 0f);
            m.SetFloat("_Is_LightColor_Base", 1f); m.SetFloat("_Is_LightColor_1st_Shade", 1f); m.SetFloat("_Is_LightColor_2nd_Shade", 1f);
            m.SetFloat("_Is_Filter_LightColor", 1f); m.SetFloat("_Is_Filter_HiCutPointLightColor", 1f);
            m.SetFloat("_Unlit_Intensity", 1f); m.SetFloat("_GI_Intensity", 0f);
            m.SetFloat("_HighColor_Power", 0f); m.SetColor("_HighColor", Color.black);
            m.SetFloat("_RimLight", 0f); m.SetFloat("_RimLightVisible", 0f); m.SetFloat("_Add_Antipodean_RimLight", 0f);
            m.SetFloat("_MatCap", 0f);
            m.SetFloat("_Outline_Width", 0f); m.SetShaderPassEnabled("SRPDefaultUnlit", false);   // 헐 외곽선 패스 끔(배경은 화면 공간 외곽선)
            m.SetFloat("_StencilMode", 0f); m.SetFloat("_StencilComp", 8f); m.SetFloat("_StencilOpPass", 0f); m.SetFloat("_StencilOpFail", 0f);
            m.SetFloat("_ClippingMode", 0f); m.SetFloat("_TransparentEnabled", 0f); m.SetFloat("_AutoRenderQueue", 1f); m.renderQueue = -1;
            m.SetFloat("_EMISSIVE", 0f); m.SetTexture("_Emissive_Tex", null); m.SetColor("_Emissive_Color", Color.black);
            m.enableInstancing = instancing;
            EditorUtility.SetDirty(m);
            return m;
        }

        static readonly Dictionary<string, string> FlatColors = new Dictionary<string, string>
        {
            ["M_Granite_Dark"] = "#5E5A55", ["M_Granite_Block"] = "#CFC6B4", ["M_Hanji"] = "#F1EAD8", ["M_Wire_Black"] = "#1A1417",
            ["M_Fabric"] = "#A9B7C2", ["M_Plant_Green"] = "#7E9A5E", ["M_Tank_Blue"] = "#7FB2C9",
        };

        static void Materials()
        {
            var mats = D(Manifest()["materials"]);
            var made = new Dictionary<string, Material>();
            foreach (var kv in mats)
            {
                var info = D(kv.Value);
                string name = kv.Key;
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{name}.png");
                float tiling = F(G(info, "tiling"), 1f);
                if (tiling <= 0f) tiling = 1f;
                Color col = tex != null ? Color.white : Hex(FlatColors.TryGetValue(name, out var fc) ? fc : S(info, "color"), Color.gray);
                bool uv01 = S(info, "uv") == "01" || name == "M_Sign_Atlas" || name == "M_Glass_Lit" || name == "M_Decal_Atlas";
                var m = Toon($"{MatDir}/{name}.mat", tex, uv01 ? Vector2.one : Vector2.one * tiling, Vector2.zero, col);
                if (name == "M_Glass_Lit")
                {
                    m.EnableKeyword("_EMISSIVE_SIMPLE"); m.SetTexture("_Emissive_Tex", tex); m.SetColor("_Emissive_Color", new Color(1.15f, 0.86f, 0.55f, 1f));   // 저녁·밤 창문 불 = 따뜻한 주황
                    EditorUtility.SetDirty(m);
                }
                made[name] = m;
            }
            // FBX 재질 → Materials/Env 바꿔 꿰기(이름 같음)
            int remapped = 0;
            foreach (var d in Districts)
            {
                string p = $"{BuildDir}/Z1_{d}.fbx";
                var imp = AssetImporter.GetAtPath(p) as ModelImporter;
                if (imp == null) continue;
                bool dirty = false;
                var map = imp.GetExternalObjectMap();
                var srcMats = AssetDatabase.LoadAllAssetRepresentationsAtPath(p).OfType<Material>().Select(x => x.name).ToList();
                var sub = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (sub != null) foreach (var r in sub.GetComponentsInChildren<Renderer>(true)) foreach (var sm in r.sharedMaterials) if (sm != null && !srcMats.Contains(sm.name)) srcMats.Add(sm.name);
                foreach (var nm in srcMats.Distinct())
                {
                    if (!made.TryGetValue(nm, out var target)) { if (nm != "No Name" && nm != "Default-Material") Notes.Add($"{d}: 재질 '{nm}' 은 manifest 에 없음"); continue; }
                    var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), nm);
                    if (map.TryGetValue(id, out var cur) && cur == target) continue;
                    imp.AddRemap(id, target); dirty = true; remapped++;
                }
                if (dirty) { imp.SaveAndReimport(); }
            }
            Notes.Add($"재질 {made.Count}개(UTS) · FBX 재질 바꿔 꿴 것 {remapped}");
        }

        // ───────────────────────── 2) 소품 프리팹(LOD 3단 + GPU 인스턴싱)
        static void PropPrefabs()
        {
            int n = 0;
            var report = new List<string>();
            foreach (var glb in Directory.GetFiles(PropDir, "*.glb").OrderBy(x => x, StringComparer.Ordinal))
            {
                string path = glb.Replace('\\', '/');
                string name = Path.GetFileNameWithoutExtension(path);
                var src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (src == null) { Notes.Add($"소품 GLB 못 읽음: {path}"); continue; }
                var mfs = src.GetComponentsInChildren<MeshFilter>(true).Where(m => m.sharedMesh != null).ToList();
                if (mfs.Count == 0) { Notes.Add($"소품 메시 없음: {name}"); continue; }
                MeshFilter Lod(int k) => mfs.FirstOrDefault(m => m.name.EndsWith("_LOD" + k) || m.sharedMesh.name.EndsWith("_LOD" + k)) ?? (k == 0 ? mfs[0] : null);
                var lod0 = Lod(0);
                var lod0R = lod0.GetComponent<MeshRenderer>();
                var srcMat = lod0R != null ? lod0R.sharedMaterial : null;
                Texture tex = null;
                if (srcMat != null) foreach (var id in new[] { "baseColorTexture", "_BaseMap", "_MainTex" }) if (srcMat.HasProperty(id) && srcMat.GetTexture(id) != null) { tex = srcMat.GetTexture(id); break; }
                if (tex == null && srcMat != null) tex = srcMat.mainTexture;
                // glTFast 가 GLB 안 텍스처를 압축 없는 RGBA32 서브 에셋으로 두어 빌드가 커진다(소품 40개 ≈ 300MB) → PNG 로 뽑아 BC7 로 들인다(EnvImport)
                tex = ExtractedTexture(tex, name) ?? tex;
                Material mat;
                var b0 = lod0.sharedMesh.bounds; float h0 = b0.size.y * Mathf.Max(lod0.transform.lossyScale.y, 0.001f);
                if (CardTreeReady(name))
                {
                    // 4차: 나무는 카드 전용 수관(가지 뼈대 + 잎 덩어리 카드) — 크기는 Hunyuan 나무와 같게
                    float w0 = Mathf.Max(b0.size.x, b0.size.z) * Mathf.Max(lod0.transform.lossyScale.x, 0.001f);
                    var ct = BuildCardTree(name, h0, w0); report.Add(ct); Notes.Add(ct); n++;
                    continue;
                }
                if (IsVeg(name))
                {
                    float amp = name.EndsWith("_tree") ? 0.07f : name == "shrub_bush" ? 0.03f : name == "ivy_patch" ? 0.015f : name == "grass_tuft" ? 0.05f : 0.02f;
                    mat = Leaf($"{PropMatDir}/M_V_{name}.mat", tex, amp, Mathf.Max(0.3f, h0));
                }
                else mat = Toon($"{PropMatDir}/M_P_{name}.mat", tex, Vector2.one, Vector2.zero, Color.white);

                var root = new GameObject("P_" + name);
                var lg = root.AddComponent<LODGroup>();
                var lods = new List<LOD>();
                var b = lod0.sharedMesh.bounds;
                float size = Mathf.Max(b.size.x, b.size.y, b.size.z) * Mathf.Max(lod0.transform.lossyScale.x, 0.001f);
                // docs/10 10장: LOD1 50% @12m · LOD2 20% @30m · 1m 미만은 60m 컬링(그 밖은 화면 높이 0.6%). FOV 45 → 화면 높이 = 크기 / (거리 × 0.8284)
                float H(float dist) => Mathf.Clamp(size / (dist * 0.8284f), 0.002f, 0.95f);
                // 2026-10-24 D08: 60m 컬링·12/30m 이면 밤 상가거리 삼각형 2.68M(예산 2.5M) → 작은 소품(1m 미만)은 8/20m 전환·45m 컬링
                float cull = size < 1f ? H(35f) : 0.006f;
                // 작은 것(1m 미만, 실외기 등 338개) 6/15m·40m 컬링, 큰 것(나무 등 3m 이상) 22/55m, 중간(관목 등 1~3m) 10/25m — D08 삼각형 2.5M 예산
                float[] trans = { H(size < 1f ? 6f : size >= 3f ? 10f : 8f), H(size < 1f ? 14f : size >= 3f ? 35f : 20f), cull };   // 3차: 가로수 줄 추가 뒤 나무 10/35m·관목 8/20m
                for (int k = 0; k < 3; k++)
                {
                    var mf = Lod(k);
                    if (mf == null) break;
                    var go = new GameObject("LOD" + k);
                    go.transform.SetParent(root.transform, false);
                    go.transform.SetPositionAndRotation(mf.transform.position - src.transform.position, mf.transform.rotation);
                    go.transform.localScale = mf.transform.lossyScale;
                    go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                    var r = go.AddComponent<MeshRenderer>();
                    r.sharedMaterial = mat;
                    r.shadowCastingMode = k == 0 ? ShadowCastingMode.On : ShadowCastingMode.Off;   // 그림자는 LOD0 만(소품 338개 × 캐스케이드 2 비용)
                    r.receiveShadows = true;
                    GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.OccludeeStatic);   // Batching 은 끔 — 338개가 정적 배칭으로 합쳐지면 장면 메시가 280MB(빌드 실측), GPU 인스턴싱이 맡는다
                    lods.Add(new LOD(trans[Mathf.Min(k, trans.Length - 1)], new Renderer[] { r }));
                }
                // 마지막 단의 전환값 = 컬링
                if (lods.Count > 0) { var last = lods[lods.Count - 1]; last.screenRelativeTransitionHeight = cull; lods[lods.Count - 1] = last; }
                int cards = Array.IndexOf(TreeNames, name) >= 0 ? AddLeafCards(root, name, lod0.sharedMesh, root.transform, lods, size) : 0;
                lg.SetLODs(lods.ToArray());
                lg.fadeMode = LODFadeMode.None;
                lg.RecalculateBounds();
                GameObjectUtility.SetStaticEditorFlags(root, StaticEditorFlags.OccludeeStatic);
                string pp = $"{PrefabDir}/P_{name}.prefab";
                PrefabUtility.SaveAsPrefabAsset(root, pp);
                Object.DestroyImmediate(root);
                n++;
                report.Add($"{name} {size:F2}m LOD{lods.Count} ({string.Join("/", lods.Select(l => l.screenRelativeTransitionHeight.ToString("F3")))}){(cards > 0 ? " 잎카드 " + cards : "")}");
            }
            // 5차: GLB 없는 카드 나무(감나무 — f04) — 크기는 직접
            if (CardTreeReady("persimmon_tree")) { var ct = BuildCardTree("persimmon_tree", 6.0f, 5.4f); report.Add(ct); Notes.Add(ct); n++; }
            Notes.Add($"소품 프리팹 {n}개: {string.Join(" · ", report.Take(6))} …");
        }

        /// GLB 안 텍스처 → Art/Env/Props/Tex/<소품>.png(없을 때만 — 그래픽이 있어야 뽑힘) → 들여온 압축 텍스처
        static Texture ExtractedTexture(Texture src, string name)
        {
            string dir = PropDir + "/Tex"; EnsureFolder(dir);
            string png = $"{dir}/{name}.png";
            var have = AssetDatabase.LoadAssetAtPath<Texture2D>(png);
            if (have != null) return have;
            if (src == null || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) { if (src != null) Notes.Add($"소품 텍스처 추출 못 함(그래픽 없음): {name} — -nographics 없이 EnvSetup.AssetsOnly 한 번"); return null; }
            var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(src, rt);
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var t2 = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false, false);
            t2.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0); t2.Apply();
            RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
            File.WriteAllBytes(png, t2.EncodeToPNG());
            Object.DestroyImmediate(t2);
            AssetDatabase.ImportAsset(png, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(png);
        }

        // ───────────────────────── 3) 원경 카드 에셋(호 메시·재질)
        public const float SkyRadius = 480f;   // 3차: 600 → 480(산 카드가 더 크고 가깝게)
        // 4차: 카드 세로를 0.62 배로 눌러(산 꼭대기 23° → 약 10°) 0.2H 내림 — 도시 층이 지평선 위 0~7°, 가까운 지붕 층은 눈높이 아래(높은 곳에서 내려다볼 때만 보임)
        public const float SkySquash = 0.62f, SkyDrop = 0.2f;
        static void SkyAssets()
        {
            // 호 메시: 90° + 양쪽 2° 겹침, 폭 = 호 길이, 높이 = 폭 × 1152/2688, 지평선 y=0, 아래로 0.35H 더(아랫줄 Clamp)
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(ArcMeshPath);
            if (mesh == null) { mesh = new Mesh { name = "SkyArc" }; AssetDatabase.CreateAsset(mesh, ArcMeshPath); }
            const int Seg = 24; float a0 = -47f, a1 = 47f;
            float arc = SkyRadius * Mathf.Deg2Rad * 90f, H = arc * 1152f / 2688f * SkySquash;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            for (int i = 0; i <= Seg; i++)
            {
                float t = i / (float)Seg, a = Mathf.Lerp(a0, a1, t) * Mathf.Deg2Rad;
                float x = Mathf.Sin(a) * SkyRadius, z = Mathf.Cos(a) * SkyRadius;
                float u = Mathf.Lerp(-2f / 94f, 1f + 2f / 94f, t);
                // 4차: 줄 4개 — 아래 −0.9H(내려다봐도 끝이 안 보이게, 셰이더가 아랫줄을 집음) · 지평선 · 1.05H · 위 2.4H(반지름 0.55배로 오므림 —
                //      게임 카메라 먼 면 600m 안에 들게. 원통 그대로면 위쪽이 잘려 카메라 바탕색 띠가 보임)
                float[] hs = { -0.9f, 0f, 1.05f, 2.4f }, rs = { 1f, 1f, 1f, 0.55f };
                for (int r = 0; r < 4; r++) { v.Add(new Vector3(x * rs[r], hs[r] * H, z * rs[r])); uv.Add(new Vector2(u, hs[r])); }
                if (i > 0)
                    for (int r = 0; r < 3; r++)
                    {
                        int a2 = (i - 1) * 4 + r, b2 = a2 + 1, c2 = i * 4 + r, d2 = c2 + 1;
                        tri.AddRange(new[] { a2, b2, c2, b2, d2, c2 });
                    }
            }
            mesh.Clear(); mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(tri, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            var sh = Shader.Find("Haengin/SkyCard") ?? throw new Exception("Haengin/SkyCard 셰이더 없음");
            void Card(string name, string tex, bool flip)
            {
                string p = $"{SkyMatDir}/M_Sky_{name}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (m == null) { m = new Material(sh) { name = "M_Sky_" + name }; AssetDatabase.CreateAsset(m, p); }
                if (m.shader != sh) m.shader = sh;
                m.SetTexture("_LayersTex", AssetDatabase.LoadAssetAtPath<Texture2D>($"{SkyDir}/sky_{tex}_layers.png"));
                m.SetTexture("_DetailTex", AssetDatabase.LoadAssetAtPath<Texture2D>($"{SkyDir}/sky_{tex}_detail.png"));
                m.SetFloat("_Flip", flip ? 1f : 0f);
                m.enableInstancing = false;
                EditorUtility.SetDirty(m);
            }
            Card("North", "north_bugaksan", false); Card("East", "east_naksan", false); Card("South", "south_city", false); Card("West", "north_bugaksan", true);
            var csh = Shader.Find("Haengin/CloudCard") ?? throw new Exception("Haengin/CloudCard 셰이더 없음");
            for (int i = 0; i <= 8; i++)
            {
                string p = $"{SkyMatDir}/M_Cloud_{i:00}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (m == null) { m = new Material(csh) { name = $"M_Cloud_{i:00}" }; AssetDatabase.CreateAsset(m, p); }
                if (m.shader != csh) m.shader = csh;
                m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>($"{SkyDir}/cloud_{i:00}.png"));
                m.SetColor("_Tint", Color.white);
                EditorUtility.SetDirty(m);
            }
            Notes.Add($"원경: 호 메시({Seg}칸, 반지름 {SkyRadius}m, 높이 {H:F0}m) · 카드 재질 4 · 구름·달 재질 9");
        }

        // ───────────────────────── 4) 화면 공간 외곽선(렌더러 기능, 후처리 앞, 깊이·법선·스텐실)
        static void OutlineFeature()
        {
            var sh = Shader.Find("Haengin/ScreenOutline");
            if (sh == null) { Notes.Add("ScreenOutline 셰이더 없음 — 렌더러 기능 건너뜀"); return; }
            var m = AssetDatabase.LoadAssetAtPath<Material>(OutlineMatPath);
            if (m == null) { m = new Material(sh) { name = "M_Outline" }; AssetDatabase.CreateAsset(m, OutlineMatPath); }
            if (m.shader != sh) m.shader = sh;
            // 4차: 거리별 굵기·역깊이 모서리·근경 손그림 결 값(재질에 저장된 옛 값을 덮어씀)
            m.SetFloat("_Strength", 1f); m.SetFloat("_NearWidth", 2.4f); m.SetFloat("_WidthPow", 0.55f); m.SetFloat("_FarWidth", 0.55f);
            m.SetFloat("_FadeStart", 55f); m.SetFloat("_FadeEnd", 80f); m.SetFloat("_DepthRel", 0.022f); m.SetFloat("_DepthAbs", 0.03f);
            m.SetFloat("_CreaseInv", 0.0045f); m.SetFloat("_Grain", 0.035f); m.SetFloat("_Stain", 0.05f); m.SetFloat("_HandNear", 8f); m.SetFloat("_HandFar", 22f);
            EditorUtility.SetDirty(m);
            foreach (var path in Renderers)
            {
                var data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(path);
                if (data == null) continue;
                // SSAO 끔(09 3-7 '블룸·SSAO 없음'). 켜 두면 DepthNormals 프리패스가 돌고 UTS 에는 그 패스가 없어 툰 면이 가까이서 검게 뭉개진다(2026-10-24 실측)
                foreach (var f in data.rendererFeatures)
                    if (f != null && f.GetType().Name == "ScreenSpaceAmbientOcclusion" && f.isActive) { f.SetActive(false); EditorUtility.SetDirty(f); EditorUtility.SetDirty(data); Notes.Add($"SSAO 렌더러 기능 끔: {path}"); }
                var have = data.rendererFeatures.FirstOrDefault(f => f is FullScreenPassRendererFeature fs && fs.name == "Outline") as FullScreenPassRendererFeature;
                if (have != null)
                {
                    bool ch = false;
                    if (have.passMaterial != m) { have.passMaterial = m; ch = true; }
                    if (!have.bindDepthStencilAttachment) { have.bindDepthStencilAttachment = true; ch = true; }
                    if (have.requirements != (ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Color)) { have.requirements = ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Color; ch = true; }
                    // 룩데브 ③(2026-10-26): 후처리(색 보정·LUT) 뒤에 그림 — 앞에 그리면 Lift 가 먹선을 L* 34 로 띄우고 시간대 색이 물들었다(프레임 선 L* 16~29)
                    if (have.injectionPoint != FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing) { have.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing; ch = true; }
                    if (ch) { EditorUtility.SetDirty(have); EditorUtility.SetDirty(data); }
                    continue;
                }
                var feat = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                feat.name = "Outline";
                feat.passMaterial = m;
                feat.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;
                feat.fetchColorBuffer = true;
                feat.bindDepthStencilAttachment = true;
                feat.requirements = ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Color;
                feat.passIndex = 0;
                AssetDatabase.AddObjectToAsset(feat, data);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feat, out _, out long localId);
                var so = new SerializedObject(data);
                var list = so.FindProperty("m_RendererFeatures");
                var map = so.FindProperty("m_RendererFeatureMap");
                list.arraySize++; map.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feat;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                // 잉크 모드·쇼크 컷보다 앞
                int first = -1;
                for (int i = 0; i < list.arraySize - 1; i++) if (list.GetArrayElementAtIndex(i).objectReferenceValue is FullScreenPassRendererFeature) { first = i; break; }
                if (first >= 0) { list.MoveArrayElement(list.arraySize - 1, first); map.MoveArrayElement(map.arraySize - 1, first); }
                so.ApplyModifiedPropertiesWithoutUndo();
                data.SetDirty(); EditorUtility.SetDirty(data);
                Notes.Add($"Outline 전체 화면 패스 붙임: {path}");
            }
            RimFeature();
        }

        /// 7차: 하늘 역광 테두리(Haengin/ScreenRim) — 외곽선 바로 뒤, 스텐실 없음(인물·잎 덩어리에도)
        static void RimFeature()
        {
            var sh = Shader.Find("Haengin/ScreenRim");
            if (sh == null) { Notes.Add("ScreenRim 셰이더 없음 — 역광 건너뜀"); return; }
            string mp = Root + "/Materials/Fx/M_Rim.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
            if (m == null) { m = new Material(sh) { name = "M_Rim" }; AssetDatabase.CreateAsset(m, mp); }
            if (m.shader != sh) m.shader = sh;
            m.SetFloat("_NearWidth", 2.4f); m.SetFloat("_WidthPow", 0.55f); m.SetFloat("_RimPx", 3.2f); m.SetFloat("_SkyDepth", 1500f); m.SetFloat("_MaxDepth", 90f);
            EditorUtility.SetDirty(m);
            foreach (var path in Renderers)
            {
                var data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(path);
                if (data == null) continue;
                var have = data.rendererFeatures.FirstOrDefault(f => f is FullScreenPassRendererFeature fs && fs.name == "Rim") as FullScreenPassRendererFeature;
                if (have != null) { if (have.passMaterial != m) { have.passMaterial = m; EditorUtility.SetDirty(have); EditorUtility.SetDirty(data); } continue; }
                var feat = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                feat.name = "Rim"; feat.passMaterial = m;
                feat.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing;
                feat.fetchColorBuffer = true; feat.bindDepthStencilAttachment = false;
                feat.requirements = ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Color; feat.passIndex = 0;
                AssetDatabase.AddObjectToAsset(feat, data);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feat, out _, out long localId);
                var so = new SerializedObject(data);
                var list = so.FindProperty("m_RendererFeatures"); var map = so.FindProperty("m_RendererFeatureMap");
                list.arraySize++; map.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feat;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                int outline = -1;
                for (int i = 0; i < list.arraySize - 1; i++) if (list.GetArrayElementAtIndex(i).objectReferenceValue is FullScreenPassRendererFeature fo && fo.name == "Outline") { outline = i; break; }
                if (outline >= 0) { list.MoveArrayElement(list.arraySize - 1, outline + 1); map.MoveArrayElement(map.arraySize - 1, outline + 1); }
                so.ApplyModifiedPropertiesWithoutUndo();
                data.SetDirty(); EditorUtility.SetDirty(data);
                Notes.Add($"Rim 전체 화면 패스 붙임: {path}");
            }
        }

        // ───────────────────────── 5) 그림자 캐스케이드 2단 · 60m(09 3-5)
        static void Pipeline()
        {
            foreach (var p in Pipelines)
            {
                var a = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(p);
                if (a == null) continue;
                bool ch = false;
                if (a.shadowCascadeCount != 2) { a.shadowCascadeCount = 2; ch = true; }
                if (Mathf.Abs(a.shadowDistance - 38f) > 0.01f) { a.shadowDistance = 38f; ch = true; }   // 60 → 45m(2차) → 38m(3차, 가로수 줄 뒤 D08 삼각형 2.5M 예산). 그 밖은 외곽선 소실 거리(55~80m)와 겹침
                if (Mathf.Abs(a.cascade2Split - 0.25f) > 0.001f) { a.cascade2Split = 0.25f; ch = true; }
                if (ch) { EditorUtility.SetDirty(a); Notes.Add($"그림자 캐스케이드 2단·38m: {p}"); }
            }
            // LOD Bias 1(docs/11 6장 — 전환값은 Bias 1 기준). 템플릿 품질 단계는 2 라 소품 LOD 가 두 배 거리에서야 바뀜
            int cur = QualitySettings.GetQualityLevel(); int fixedLevels = 0;
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                if (Mathf.Abs(QualitySettings.lodBias - 1f) > 0.01f) { QualitySettings.lodBias = 1f; fixedLevels++; }
            }
            QualitySettings.SetQualityLevel(cur, false);
            if (fixedLevels > 0) { AssetDatabase.SaveAssets(); Notes.Add($"품질 단계 LOD Bias → 1: {fixedLevels}단계"); }
        }

        // ───────────────────────── 6) 시간대 조명 값(docs/10 9장 — 2026-10-26 룩데브 정정: docs/14 4-2) — 새벽·낮·노을·밤을 덮어쓴다.
        //   그늘은 시간대 색(청회·모브·남색)이 아니라 언제나 '따뜻한 밝은 회갈'(프레임 8장 실측 h 17~78°): 스플릿 톤 끄거나 회갈 몇 %, 채도 낮춤, 대비 0, 검정 들어 올림(Lift),
        //   주변광 15~25% 올림(셀 그늘이 너무 깊음 — 그늘 ÷ 밝은 면 0.18 ↔ 프레임 0.27). 남는 색 차이는 시간대 LUT(맨 끝, HDR 등급 모드)가 맞춘다.
        //   + 먹선 색(프레임 선 색) · 깊이 안개(4-4 표) · 땅 햇빛 얼룩 세기 · 밤 빛 웅덩이 배율(4-5) · LUT(Art/Env/Lut — LookdevSetup 이 들여옴)
        //   (예전 7차 코드는 노을 해 세기·그림자, 밤 Gamma·Gain·천정색이 주석 안에 들어가 적용되지 않았다 — 이번에 바로잡음)
        static void Presets()
        {
            void Set(DayPart part, Action<LightingPreset> f)
            {
                var a = AssetDatabase.LoadAssetAtPath<LightingPreset>(StorySetup.PresetPath(part));
                if (a == null) { Notes.Add($"프리셋 없음: {part}"); return; }
                f(a);
                var lut = AssetDatabase.LoadAssetAtPath<Texture2D>($"{LookdevSetup.LutDir}/LUT_Z1_{part}_URP32.png");
                a.Lut = lut; a.LutContribution = 1f;
                EditorUtility.SetDirty(a);
            }
            Color H(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
            Color Tint(string hex, float k) { var c = H(hex); return Color.Lerp(new Color(0.5f, 0.5f, 0.5f), c, k); }   // 스플릿 톤 색(0.5 회색 = 중립)
            var flat = new Vector4(1f, 1f, 1f, 0f);
            Set(DayPart.Dawn, a =>
            {
                a.SunPitch = 10f; a.SunYaw = 70f; a.SunColor = H("#DCD8D0"); a.SunIntensity = 0.75f; a.ShadowStrength = 0.3f;     // 청회 #C9D3E0 → 따뜻한 회색
                a.AmbientSky = new Color(0.98f, 0.97f, 0.95f); a.AmbientEquator = new Color(0.90f, 0.88f, 0.86f); a.AmbientGround = new Color(0.72f, 0.70f, 0.67f);   // 푸른 기 뺌(D07 새벽 ≥ 28 은 밝기 그대로)
                a.Sky = H("#CBD1D6"); a.Fog = H("#B5B2AA"); a.FogStart = 40f; a.FogEnd = 300f;
                a.SplitShadows = Tint("#6B5E55", 0.10f); a.SplitHighlights = Tint("#F3D9B0", 0.06f); a.SplitBalance = 0f;   // 그림자 청록 10% → 끔(회갈 아주 조금)
                a.StreetLamps = a.SecurityLamps = a.Vending = a.ConvAwning = a.GukbapWindow = true; a.ShopSigns = false; a.LampIntensity = 7f; a.Windows = 0.15f;
                a.Saturation = -6f; a.Contrast = 0f; a.Lift = new Vector4(1f, 1f, 1f, 0.03f); a.Gamma = flat; a.Gain = flat; a.Zenith = H("#9FA9BB");
                a.Ink = H("#3E3A33"); a.HazeColor = H("#B9B4A8"); a.HazeStart = 10f; a.HazeEnd = 95f; a.HazeStrength = 0.45f; a.Dapple = 0.35f; a.PoolBoost = 1f;
            });
            Set(DayPart.Day, a =>
            {
                a.SunPitch = 48f; a.SunYaw = 155f; a.SunColor = new Color(1f, 0.973f, 0.933f); a.SunIntensity = 1f; a.ShadowStrength = 0.5f;
                a.AmbientSky = new Color(0.85f, 0.83f, 0.79f); a.AmbientEquator = new Color(0.74f, 0.71f, 0.68f); a.AmbientGround = new Color(0.53f, 0.51f, 0.48f);   // +38%(그늘 L* 41~47 쪽)
                a.Sky = H("#EEE3D1"); a.Fog = H("#D4CEC3"); a.FogStart = 40f; a.FogEnd = 380f;
                a.SplitShadows = Tint("#6B5E55", 0.12f); a.SplitHighlights = Tint("#F3D9B0", 0.10f); a.SplitBalance = 0f;   // 그림자 청록 7% → 회갈 4% 상당
                a.Windows = 0f;
                a.Saturation = -10f; a.Contrast = 0f; a.Lift = new Vector4(1f, 1f, 1f, 0.03f); a.Gamma = flat; a.Gain = new Vector4(1f, 1f, 1.02f, 0f); a.Zenith = H("#D3C9B0");   // 밝은 면 노랑 덜기
                a.Ink = H("#423D33"); a.HazeColor = H("#D9D2C2"); a.HazeStart = 14f; a.HazeEnd = 120f; a.HazeStrength = 0.36f; a.Dapple = 1f; a.PoolBoost = 1f;
            });
            Set(DayPart.Dusk, a =>
            {
                a.SunPitch = 8f; a.SunYaw = 70f; a.SunColor = H("#F4B27A"); a.SunIntensity = 0.9f; a.ShadowStrength = 0.55f;   // 7차: f02 처럼 주차장 B 카메라의 왼쪽 뒤에서(275 → 70)
                a.AmbientSky = new Color(0.84f, 0.77f, 0.72f); a.AmbientEquator = new Color(0.73f, 0.67f, 0.64f); a.AmbientGround = new Color(0.49f, 0.44f, 0.42f);   // 모브 → 따뜻한 회색, 밝게
                a.Sky = H("#EBC9A6"); a.Fog = H("#CDB8A2"); a.FogStart = 50f; a.FogEnd = 340f;
                a.SplitShadows = Tint("#6B5E55", 0.08f); a.SplitHighlights = Tint("#F4B27A", 0.12f); a.SplitBalance = 20f;   // 그림자 보라 8% → 끔, 밝은 쪽 주황 8% → 4% 상당(높은 쪽에만)
                a.Vending = a.ConvAwning = a.ShopSigns = true; a.StreetLamps = a.SecurityLamps = a.GukbapWindow = false; a.LampIntensity = 5f; a.Windows = 0.25f;
                a.Saturation = -20f; a.Contrast = 0f; a.Lift = new Vector4(1f, 1f, 1f, 0.03f); a.Gamma = flat; a.Gain = flat; a.Zenith = H("#B59A96");   // 땅이 주황으로 물드는 것이 가장 큰 차이
                a.Ink = H("#41352A"); a.HazeColor = H("#C8B3A0"); a.HazeStart = 12f; a.HazeEnd = 110f; a.HazeStrength = 0.40f; a.Dapple = 0.4f; a.PoolBoost = 1f;
            });
            Set(DayPart.Night, a =>
            {
                a.SunPitch = 50f; a.SunYaw = 200f; a.SunColor = H("#8FA3C8"); a.SunIntensity = 0.14f; a.ShadowStrength = 0.3f;     // D07: 웅덩이 위 L* ≥ 22 · 웅덩이 사이 ≥ 6(완전한 검정 금지)
                a.AmbientSky = new Color(0.34f, 0.34f, 0.38f); a.AmbientEquator = new Color(0.27f, 0.26f, 0.27f); a.AmbientGround = new Color(0.12f, 0.11f, 0.11f);   // 땅 주변광 낮추고 웅덩이만 밝게
                a.Sky = H("#2A3345"); a.Fog = H("#221F2A"); a.FogStart = 30f; a.FogEnd = 240f;
                a.SplitShadows = Tint("#2E3550", 0.30f); a.SplitHighlights = new Color(0.98f, 0.72f, 0.44f); a.SplitBalance = -4f;   // 그림자 남색 12% → 5% 상당, 밝은 쪽 주황 유지
                a.StreetLamps = a.SecurityLamps = a.Vending = a.ConvAwning = a.GukbapWindow = a.ShopSigns = true; a.LampIntensity = 6f; a.Windows = 0.68f;   // 룩데브: 12 → 6(점광이 길 전체를 밝은 베이지로 — 웅덩이는 LightPool ×2.5 가 그림)
                a.Saturation = -8f; a.Contrast = 0f; a.Lift = new Vector4(1f, 1f, 1f, 0.012f); a.Gamma = flat; a.Gain = flat; a.Zenith = H("#151B2C");
                a.Ink = H("#2F2722"); a.HazeColor = H("#2A2C3A"); a.HazeStart = 8f; a.HazeEnd = 75f; a.HazeStrength = 0.30f; a.Dapple = 0f; a.PoolBoost = 1.4f;   // 가로등 원 ×1.4, 가게 앞 사각 ×0.5(LightPool)
            });
            Notes.Add("조명 프리셋 = docs/10 9장(2026-10-26 정정: 그늘 따뜻한 회갈·채도↓·대비 0·Lift) + 먹선 색·깊이 안개·햇빛 얼룩·웅덩이 배율·LUT(docs/14 4장)");
        }

        // ───────────────────────── 7) 캐릭터 UTS 재질 = 스텐실 1 쓰기(화면 공간 외곽선이 캐릭터를 건너뜀)
        static void CharacterStencil()
        {
            int n = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { Root + "/Materials" }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (p.Contains("/Materials/Env/") || p.Contains("/Materials/Zone1/") || p.Contains("/Materials/Fx/")) continue;
                var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (m == null || m.shader == null || m.shader.name != "Toon/Toon") continue;
                if (Mathf.Approximately(m.GetFloat("_StencilMode"), 2f) && Mathf.Approximately(m.GetFloat("_StencilOpPass"), 2f)) continue;
                m.SetFloat("_StencilMode", 2f); m.SetFloat("_StencilNo", 1f); m.SetFloat("_StencilComp", 8f); m.SetFloat("_StencilOpPass", 2f); m.SetFloat("_StencilOpFail", 0f);
                EditorUtility.SetDirty(m); n++;
            }
            if (n > 0) Notes.Add($"캐릭터 툰 재질 스텐실 1 쓰기: {n}개");
        }

        // ═════════════════════════ Zone1 에 세우기(M1Setup.AttachRig 끝에서)
        public static void AttachKit(Zone1Data d, Transform zoneRoot)
        {
            if (!KitAvailable) { Debug.Log($"{Tag} 키트 없음 — 회색 상자 그대로"); return; }
            var t0 = DateTime.Now;
            var man = Manifest();
            var objs = new Dictionary<string, Dictionary<string, object>>();
            foreach (var o in L(man["objects"])) { var od = D(o); objs[S(od, "name")] = od; }
            foreach (var old in new[] { "Env", "Sky" }) { var t = zoneRoot.Find(old); if (t != null) Object.DestroyImmediate(t.gameObject); }
            var env = new GameObject("Env").transform; env.SetParent(zoneRoot, false);
            var lights = zoneRoot.Find("Lights");
            if (lights == null) { lights = new GameObject("Lights").transform; lights.SetParent(zoneRoot, false); }
            var atlas = SignRects();
            socks5.Clear(); faces5 = null; signs5 = 0; signMeshRoot = null; signMeshes = null;   // 5차
            Mesh6Reset(); var signList = new List<(Transform t, Dictionary<string, object> inf)>();   // 6차: 간판은 배치 점검 뒤에 짓는다
            var winLitMat = WindowLitMat();
            int buildings = 0, lodGroups = 0, props = 0, missingProps = 0, winlit = 0, panes = 0, signs = 0, socks = 0, renderers = 0;
            var propPrefabs = new Dictionary<string, GameObject>();
            GameObject Prefab(string prop)
            {
                if (!propPrefabs.TryGetValue(prop, out var pf)) propPrefabs[prop] = pf = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/P_{prop}.prefab");
                return pf;
            }
            var staticFlags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic;

            foreach (var dist in Districts)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{BuildDir}/Z1_{dist}.fbx");
                if (prefab == null) { Notes.Add($"구역 FBX 없음: {dist}"); continue; }
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                inst.name = "Z1_" + dist;
                inst.transform.SetParent(env, false);
                inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                inst.transform.localScale = Vector3.one;
                var children = inst.transform.Cast<Transform>().ToList();
                foreach (var t in children)
                {
                    objs.TryGetValue(t.name, out var info);
                    string kind = S(info, "kind") ?? "";
                    var inf = D(G(info, "info"));
                    if (t.name.StartsWith("Prop_"))
                    {
                        string prop = S(inf, "prop") ?? t.name.Substring(5, t.name.LastIndexOf('_') - 5);
                        var pf = Prefab(prop);
                        if (pf == null) { missingProps++; continue; }
                        var pi = (GameObject)PrefabUtility.InstantiatePrefab(pf, t);
                        pi.transform.localPosition = Vector3.zero; pi.transform.localRotation = Quaternion.identity; pi.transform.localScale = Vector3.one;
                        props++;
                        continue;
                    }
                    if (t.name.StartsWith("Sock_"))
                    {
                        if (Sock5Kinds.Contains(S(inf, "sock_kind") ?? "")) { socks5.Add((t, inf)); continue; }   // 5차: 간판·데칼·천 자리
                        AddSocketLight(t, inf, lights); socks++;
                        continue;
                    }
                    if (t.name.StartsWith("WinLit_"))
                    {
                        var r = t.GetComponent<Renderer>();
                        var s = t.gameObject.AddComponent<LightSpot>();
                        s.Kind = LightKind.Window; s.Threshold = F(G(inf, "threshold"), 0.5f); s.Weight = Mathf.Max(1, (int)F(G(inf, "panes"), 1)); s.KeepColor = true;
                        if (r != null) { r.enabled = false; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; if (winLitMat != null) r.sharedMaterial = winLitMat; }
                        winlit++; panes += s.Weight;
                        continue;
                    }
                    if (t.name.StartsWith("Sign_"))
                    {
                        signList.Add((t, inf));   // 6차: 짓기 전에 겹침·창 점검(PlanSigns) — 아래에서 짓는다
                        signs++;
                        continue;
                    }
                    // 건물·담·성곽·전봇대: LOD 묶음
                    var rs = t.GetComponentsInChildren<Renderer>(true);
                    foreach (var r in rs)
                    {
                        GameObjectUtility.SetStaticEditorFlags(r.gameObject, staticFlags);
                        r.gameObject.layer = 0;
                        r.receiveShadows = true;
                        if (r.name.Contains("_Detail_") || r.name.EndsWith("_LOD2")) r.shadowCastingMode = ShadowCastingMode.Off;   // 디테일·LOD2 는 그림자 없음
                        renderers++;
                    }
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, staticFlags);
                    var lodNames = rs.Where(r => r.name.Contains("_LOD")).ToList();
                    if (lodNames.Count > 0)
                    {
                        var lg = t.GetComponent<LODGroup>();
                        if (lg == null) lg = t.gameObject.AddComponent<LODGroup>();   // (?? 는 유니티 가짜 null 을 못 걸러냄)
                        var heights = L(G(inf, "lod_screen_heights"));
                        float[] h = heights != null && heights.Count >= 3 ? new[] { F(heights[0]), F(heights[1]), F(heights[2]) } : new[] { 0.3f, 0.12f, 0.006f };
                        h[0] *= 1.8f; h[1] *= 1.2f;   // D08 삼각형 예산(2.5M): 키트 LOD0→1 을 40m → 22m(3차; 2차 27m), LOD1→2 를 110m → 92m
                        if (kind == "pole") { h[0] *= 1.6f; h[1] *= 1.4f; }   // 3차: 전선은 LOD1·2 가 더 굵다(0.016·0.02) → 일찍 바꿔 1080p 에서 보이게
                        var lods = new List<LOD>();
                        for (int k = 0; k < 3; k++)
                        {
                            var set = rs.Where(r => r.name.EndsWith("_LOD" + k)).ToArray();
                            if (set.Length == 0) continue;
                            lods.Add(new LOD(h[Mathf.Min(k, 2)], set));
                        }
                        if (lods.Count > 0)
                        {
                            var last = lods[lods.Count - 1]; last.screenRelativeTransitionHeight = h[2]; lods[lods.Count - 1] = last;
                            lg.SetLODs(lods.ToArray()); lg.fadeMode = LODFadeMode.None; lg.RecalculateBounds(); lodGroups++;
                        }
                    }
                    if (kind == "building") buildings++;
                }
            }

            // 6차: 간판 배치 점검(같은 면 겹친 띠 올림 · 창과 겹치는 띠 줄이기/빼기) 뒤에 짓기 — 5차: 몸체 + 디자인된 면(그림 없으면 예전 TMP)
            Notes.Add(PlanSigns(env, signList));
            foreach (var (t, inf) in signList)
            {
                if (inf.ContainsKey("hidden")) { var r0 = t.GetComponent<MeshRenderer>(); if (r0 != null) r0.enabled = false; continue; }
                if (!MakeSign5(t, inf)) MakeSign(t, inf, atlas);
            }
            // 겹치는 간판 띠는 위로 올림 — 6차부터는 PlanSigns 가 짓기 전에 처리(여기선 0 이어야 함)
            int lifted = LiftOverlappingSigns(env, objs);
            // 회색 상자 보이지 않게(충돌은 그대로 — 09 결정 11)
            int hidden = HideGreyBoxes(zoneRoot);
            // 키트 밖 회색 소품(벤치·운동기구·정류장·보안등)도 GLB 소품으로
            int swapped = SwapGreyProps(d, zoneRoot, env, Prefab);
            // 2차 패스: 바닥·벽 디테일·잡동사니·남은 회색 상자·보안등, 식생
            string decor = Decor(d, zoneRoot, env, objs, Prefab);
            string veg = Vegetation(d, zoneRoot, env, objs, Prefab);
            int piloti = PilotiColliders(zoneRoot, objs);
            // 5차: 간판 규칙 배치 · 거리 데코 · 표면 디테일 · 움직임
            string s5 = Signs5Rules(d, zoneRoot, env, objs, Prefab), st5 = Street5(d, zoneRoot, env, objs, Prefab), sf5 = Surface5(d, zoneRoot, env, objs), mo5 = Motion5(d, zoneRoot, env, Prefab);
            Notes.Add(s5); Notes.Add(st5); Notes.Add(sf5); Notes.Add(mo5);
            // 가로등·보안등 빛 반지름(빛 웅덩이 지름 6~8m)
            foreach (var s in zoneRoot.GetComponentsInChildren<LightSpot>(true))
            {
                var l = s.GetComponent<Light>();
                if (l == null) continue;
                if (s.Kind == LightKind.Street) l.range = 9f;
                else if (s.Kind == LightKind.Security) l.range = 7f;
            }
            // 주차장: 자판기·자전거·쓰레기통·차단기·국밥집 뒷문(09 3-2 원고 동선)
            string lot = ParkingLot(d, zoneRoot, env, Prefab);
            // 바닥·길 재질 = 아트 텍스처 + 툰(빛 웅덩이가 또렷하게 끊기도록)
            int ground = GroundMaterials(zoneRoot);
            // 6차: 정류장 광고 · 맨땅 초록 · 밤 빛 웅덩이 · 계단 손잡이 · 간판 점검 · 그리기 합침
            Physics.SyncTransforms();
            Notes.Add(Pass6(d, zoneRoot, env, objs, Prefab));
            // 룩데브 ⑤⑥(2026-10-26): 햇빛 얼룩·창틀 화분·빨래집게·전단·낙서·상자 — 합치기 뒤(Detail14_Z1 루트로 켜고 끔)
            Physics.SyncTransforms();
            Notes.Add(Pass8(d, zoneRoot, env, objs, Prefab));
            // 원경 카드
            SkyRig(zoneRoot);
            Physics.SyncTransforms();

            var msg = $"{Tag} Zone1 키트: 구역 7 · 건물 {buildings} · LOD 묶음 {lodGroups} · 렌더러 {renderers} · 소품 {props}(프리팹 없음 {missingProps}) · 창문 불 {winlit}묶음/{panes}장 · 간판 {signs} · 빛 자리 {socks} · " +
                      $"회색 상자 숨김 {hidden} · 회색 소품 교체 {swapped} · 간판 올림 {lifted} · 바닥 재질 {ground} · {lot} · [{decor}] · [{veg}] · 필로티 콜라이더 {piloti} · {(DateTime.Now - t0).TotalSeconds:F1}초";
            Debug.Log(msg);
            Notes.Add(msg);
        }

        static void AddSocketLight(Transform sock, Dictionary<string, object> inf, Transform lights)
        {
            string sk = S(inf, "sock_kind") ?? "";
            var go = new GameObject("빛 " + sock.name);
            go.transform.SetParent(lights, false);
            var normal = V3(G(inf, "normal"));
            go.transform.position = sock.position + normal * 0.15f;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.intensity = 0f; l.enabled = false; l.shadows = LightShadows.None; l.renderMode = LightRenderMode.ForcePixel;
            var s = go.AddComponent<LightSpot>();
            switch (sk)
            {
                case "shop_light": s.Kind = LightKind.ShopSign; l.range = 9f; l.color = new Color(1f, 0.80f, 0.52f); s.KeepColor = true; break;   // 3차: 상가 유리·간판이 길을 주황으로 비춤
                case "ceiling_light": s.Kind = LightKind.Security; l.range = 6f; l.color = new Color(1f, 0.95f, 0.85f); break;
                default: s.Kind = LightKind.Street; l.range = 7f; l.color = new Color(1f, 0.96f, 0.88f); s.KeepColor = true; break;   // lamp_globe: 흰 구 등
            }
        }

        // ───────────────────────── 간판: 아틀라스 칸 재질 + TMP 글자(docs/10 7장)
        static Dictionary<string, float[]> SignRects()
        {
            var r = new Dictionary<string, float[]>();
            string p = $"{SignDir}/sign_atlas.json";
            if (!File.Exists(p)) return r;
            var j = D(MiniJson.Parse(File.ReadAllText(p, System.Text.Encoding.UTF8)));
            var rects = D(j["rects"]);
            foreach (var kv in rects) { var l = L(kv.Value); r[kv.Key] = new[] { F(l[0]), F(l[1]), F(l[2]), F(l[3]) }; }
            return r;
        }

        static readonly Dictionary<string, (string color, bool brush)> SignStyle = new Dictionary<string, (string, bool)>
        {
            ["band_wood_gukbap"] = ("#F4EFE6", true), ["band_haru_conv"] = ("#F4EFE6", true), ["band_mustard_bungae"] = ("#2B3049", true),
            ["band_navy_alu"] = ("#FFFFFF", false), ["band_white_laundry"] = ("#4F6B58", false), ["band_rusty_hardware"] = ("#3A3A3A", true),
            ["band_cream_lightbox"] = ("#2B3049", false), ["band_redbrown_bunsik"] = ("#F4EFE6", true),
            ["vert_navy"] = ("#F4EFE6", false), ["vert_cream"] = ("#2B3049", false), ["vert_slate"] = ("#F4EFE6", false), ["vert_redbrown"] = ("#F4EFE6", false),
            ["vert_white"] = ("#2B3049", false), ["vert_green"] = ("#F4EFE6", false), ["round_white"] = ("#2B3049", false), ["hanging_wood"] = ("#F4EFE6", true),
        };

        static void MakeSign(Transform t, Dictionary<string, object> inf, Dictionary<string, float[]> atlas)
        {
            string key = S(inf, "atlas") ?? "band_cream_lightbox";
            var r = t.GetComponent<MeshRenderer>();
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/M_Sign_Atlas.png");
            if (r != null)
            {
                Vector2 tiling = Vector2.one, offset = Vector2.zero;
                if (atlas.TryGetValue(key, out var rc)) { tiling = new Vector2(rc[2] / 2048f, rc[3] / 2048f); offset = new Vector2(rc[0] / 2048f, 1f - (rc[1] + rc[3]) / 2048f); }
                var m = Toon($"{SignMatDir}/M_Sign_{key}.mat", tex, tiling, offset, Color.white, false);
                r.sharedMaterial = m;
                r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = true;
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            }
            string tenant = S(inf, "tenant");
            if (string.IsNullOrEmpty(tenant)) return;
            var size = L(G(inf, "size_m"));
            float w = size != null && size.Count >= 2 ? F(size[0]) : 1f, h = size != null && size.Count >= 2 ? F(size[1]) : 0.5f;
            var normal = V3(G(inf, "normal")); if (normal == Vector3.zero) normal = -t.forward;
            var center = V3(G(inf, "center")); if (center == Vector3.zero) center = r != null ? r.bounds.center : t.position;
            bool vertical = h > w * 1.3f;
            var (col, brush) = SignStyle.TryGetValue(key, out var st) ? st : ("#F4EFE6", false);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(brush ? Root + "/Fonts/KR_Brush_SDF.asset" : Root + "/Fonts/KR_Bold_SDF.asset")
                       ?? AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root + "/Fonts/KR_Bold_SDF.asset");
            int faces = Mathf.Max(1, (int)F(G(inf, "faces"), 1));
            for (int f = 0; f < faces; f++)
            {
                var n = f == 0 ? normal : -normal;
                var go = new GameObject(f == 0 ? "글자" : "글자(뒤)");
                go.transform.SetParent(t, false);
                go.transform.position = center + n * 0.02f;
                go.transform.rotation = Quaternion.LookRotation(-n, Vector3.up);
                var tmp = go.AddComponent<TextMeshPro>();
                tmp.font = font;
                tmp.text = vertical ? string.Join("\n", tenant.Replace(" ", "").Select(c => c.ToString())) : tenant;
                tmp.color = Hex(col, Color.white);
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.enableAutoSizing = true; tmp.fontSizeMin = 0.2f; tmp.fontSizeMax = 40f;
                tmp.rectTransform.sizeDelta = new Vector2(Mathf.Max(0.2f, w * 0.88f), Mathf.Max(0.15f, h * 0.78f));
                tmp.margin = Vector4.zero;
                tmp.sortingOrder = 1;
                var mr = go.GetComponent<MeshRenderer>();
                if (mr != null) { mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false; }
                tmp.ForceMeshUpdate(true, true);
            }
        }

        // ───────────────────────── 겹치는 간판 띠 올리기(키트가 2층 띠를 1층 띠와 같은 높이에 둔 곳 — 같은 블록·같은 면·가로 겹침)
        static int LiftOverlappingSigns(Transform env, Dictionary<string, Dictionary<string, object>> objs)
        {
            var signs = env.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Sign_") && objs.ContainsKey(t.name))
                .Select(t => { var inf = D(G(objs[t.name], "info")); var sz = L(G(inf, "size_m")); return (t, c: V3(G(inf, "center")), n: V3(G(inf, "normal")), w: sz != null ? F(sz[0]) : 1f, h: sz != null ? F(sz[1]) : 0.5f, block: S(inf, "block")); })
                .ToList();
            int n = 0;
            for (int i = 0; i < signs.Count; i++)
                for (int j = i + 1; j < signs.Count; j++)
                {
                    var a = signs[i]; var b = signs[j];
                    if (a.block != b.block || Mathf.Abs(Vector3.Dot(a.n, b.n)) < 0.9f) continue;
                    var tangent = Vector3.Cross(Vector3.up, a.n).normalized;
                    float dx = Mathf.Abs(Vector3.Dot(b.c - a.c, tangent));
                    if (dx > (a.w + b.w) * 0.5f - 0.3f) continue;
                    float vo = Mathf.Min(a.c.y + a.h * 0.5f, b.c.y + b.h * 0.5f) - Mathf.Max(a.c.y - a.h * 0.5f, b.c.y - b.h * 0.5f);
                    if (vo <= 0.02f) continue;
                    var up = a.h >= b.h ? b : a;       // 작은 띠(2층 띠)를 큰 띠 위로
                    var stay = a.h >= b.h ? a : b;
                    float lift = (stay.c.y + stay.h * 0.5f + 0.12f + up.h * 0.5f) - up.c.y;
                    up.t.position += Vector3.up * lift;
                    n++;
                    Notes.Add($"간판 올림: {up.t.name}({up.block}) +{lift:F2}m — {stay.t.name} 과 겹침 {vo:F2}m");
                }
            return n;
        }

        // ───────────────────────── 키트 밖 회색 소품 → GLB 소품(벤치·운동기구·마을버스 정류장·보안등). 충돌(상자·캡슐)은 그대로
        static int SwapGreyProps(Zone1Data d, Transform zoneRoot, Transform env, Func<string, GameObject> prefab)
        {
            var grp = new GameObject("Props").transform; grp.SetParent(env, false);
            int n = 0;
            Vector3 Ground(Vector3 p) { if (Physics.Raycast(p + Vector3.up * 4f, Vector3.down, out var h, 12f, 1 << Layers.Ground, QueryTriggerInteraction.Ignore)) p.y = h.point.y; return p; }
            Vector3 ToRoad(Vector3 p)
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
            void Put(string prop, Transform grey, Vector3 feet, float yaw, bool keepText)
            {
                var pf = prefab(prop); if (pf == null) return;
                foreach (var r in grey.GetComponentsInChildren<MeshRenderer>(true)) { if (keepText && r.GetComponent<TMP_Text>() != null) continue; r.enabled = false; }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, grp);
                go.name = $"소품 {prop} ← {grey.name}";
                go.transform.SetPositionAndRotation(Ground(feet), Quaternion.Euler(0f, yaw, 0f));
                n++;
            }
            var blocks = zoneRoot.Find("Blocks/prop");
            if (blocks != null)
                foreach (Transform b in blocks)
                {
                    string prop = b.name.Contains("벤치") ? "park_bench" : b.name.Contains("운동기구") ? "exercise_machine" : null;
                    if (prop == null) continue;
                    var col = b.GetComponent<BoxCollider>();
                    float bottom = col != null ? b.TransformPoint(col.center - new Vector3(0f, col.size.y * 0.5f, 0f)).y : b.position.y;
                    var feet = new Vector3(b.position.x, bottom, b.position.z);
                    // 공터(원형 패드) 쪽을 보게
                    var pad = d.Pads.FirstOrDefault(p => p.Shape == "circle" && Flat(p.Center - feet).magnitude < 25f);
                    float yaw = pad != null ? Mathf.Atan2(pad.Center.x - feet.x, pad.Center.z - feet.z) * Mathf.Rad2Deg : b.eulerAngles.y;
                    Put(prop, b, feet, yaw, false);
                }
            var marks = zoneRoot.Find("Landmarks");
            if (marks != null)
            {
                var bus = marks.Find("bus_stop");
                if (bus != null) foreach (Transform m in bus) { var dir = ToRoad(m.position); Put("bus_stop_shelter", m, m.position - dir * 0.3f, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, true); }
                var lamp = marks.Find("lamp");
                if (lamp != null) foreach (Transform m in lamp) if (m.name.Contains("보안등")) { var dir = ToRoad(m.position); Put("street_lamp_trail", m, m.position, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, true); }
            }
            return n;
        }

        // ───────────────────────── 회색 상자 숨기기(충돌 유지)
        static int HideGreyBoxes(Transform zoneRoot)
        {
            int n = 0;
            void Hide(Transform root, bool keepText)
            {
                if (root == null) return;
                foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (keepText && r.GetComponent<TMP_Text>() != null) continue;
                    if (!r.enabled) continue;
                    r.enabled = false; n++;
                }
            }
            var blocks = zoneRoot.Find("Blocks");
            if (blocks != null) foreach (var k in new[] { "shop", "house", "hanok", "campus", "pavilion", "gate", "fence", "retaining", "railing" }) Hide(blocks.Find(k), false);
            Hide(zoneRoot.Find("Walls"), true);
            var marks = zoneRoot.Find("Landmarks");
            if (marks != null)
            {
                foreach (var k in new[] { "pole", "door", "sign", "vista", "wall_gate" }) Hide(marks.Find(k), true);
                var wires = marks.Find("전선"); if (wires != null) Hide(wires, false);
            }
            return n;
        }

        // ───────────────────────── 주차장 배치(09 3-2 원고 동선): 자판기(서쪽 상가 뒷벽 가운데) · 자전거(남쪽 끝) · 뒷문(북쪽 끝) · 쓰레기통 · 상자·솥 · 차단기
        static string ParkingLot(Zone1Data d, Transform zoneRoot, Transform env, Func<string, GameObject> prefab)
        {
            var pad = d.Pads.FirstOrDefault(p => p.Name.Contains("주차장"));
            if (pad == null) return "주차장 패드 없음";
            var rot = Quaternion.Euler(0f, pad.Yaw, 0f);
            Vector3 W(float lx, float lz) { var p = pad.Center + rot * new Vector3(lx, 0f, lz); if (Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out var hit, 8f, 1 << Layers.Ground, QueryTriggerInteraction.Ignore)) p.y = hit.point.y; else p.y = pad.Center.y; return p; }
            var lot = new GameObject("Lot").transform; lot.SetParent(env, false);
            int placed = 0;
            GameObject Put(string prop, Vector3 pos, float yawLocal, Transform parent = null, string nameOverride = null)
            {
                var pf = prefab(prop); if (pf == null) return null;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent ?? lot);
                go.name = nameOverride ?? ("소품 " + prop);
                go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, pad.Yaw + yawLocal, 0f));
                placed++;
                return go;
            }
            float west = -pad.Size.x * 0.5f, north = pad.Size.y * 0.5f;
            // 자판기: Blocks/prop '주차장 자판기' 상자(충돌·HeatSurface·빛 자리 기준) 아래에 프리팹
            var vend = zoneRoot.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == "주차장 자판기");
            if (vend != null)
            {
                var mr = vend.GetComponent<MeshRenderer>(); if (mr != null) mr.enabled = false;
                var col = vend.GetComponent<BoxCollider>();
                float bottom = col != null ? vend.TransformPoint(col.center - new Vector3(0f, col.size.y * 0.5f, 0f)).y : vend.position.y - vend.lossyScale.y * 0.5f;
                var feet = new Vector3(vend.position.x, bottom, vend.position.z);
                if (Physics.Raycast(vend.position + Vector3.up * 2f, Vector3.down, out var gh, 8f, 1 << Layers.Ground, QueryTriggerInteraction.Ignore)) feet.y = gh.point.y;
                var go = Put("vending_machine", feet, 0f, vend, "소품 vending_machine");
                if (go != null) go.transform.rotation = vend.rotation;
            }
            // 서쪽 벽 로컬 z: 자전거 −5.0 · 자판기 −3.0(zone1_gen) · 쓰레기통 −1.95 · [계단 입구 −1.7~1.7 비움] · LPG 2.3 · 솥 3.1 · 뒷문 4.2 · 상자 5.3
            Put("delivery_bicycle", W(west + 0.75f, -5.0f), 0f, null, "소품 delivery_bicycle(시우 자전거 자리)");
            Put("trash_bin_wheeled", W(west + 0.6f, -1.95f), 90f);
            Put("lpg_cylinders", W(west + 0.55f, 2.3f), 90f);
            Put("stockpot_burner", W(west + 0.6f, 3.1f), 90f);
            Put("plastic_crates_stack", W(west + 0.55f, 5.3f), 90f);
            // 차단기: 'Blocks/prop/주차 차단기' 기둥 상자 자리에서 동쪽으로 팔(2.48m) — 입구 6m 중 서쪽 절반, 걷는 길은 동쪽
            var bar = zoneRoot.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == "주차 차단기");
            if (bar != null)
            {
                var mr = bar.GetComponent<MeshRenderer>(); if (mr != null) mr.enabled = false;
                var p = bar.position; if (Physics.Raycast(p + Vector3.up * 2f, Vector3.down, out var gh2, 8f, 1 << Layers.Ground, QueryTriggerInteraction.Ignore)) p.y = gh2.point.y;
                Put("parking_barrier", p + rot * new Vector3(1.1f, 0f, 0f), 0f, null, "소품 parking_barrier(내려간 차단기)");
            }
            // 국밥집 뒷문(판): 서쪽 상가 뒷벽 북쪽 끝 — 철문 녹색 1.0 × 2.1, 명판
            var door = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(door.GetComponent<Collider>());
            door.name = "성대후문 국밥 뒷문";
            door.transform.SetParent(lot, false);
            var dp = W(west + 0.06f, 4.2f);
            door.transform.SetPositionAndRotation(dp + Vector3.up * 1.05f, rot * Quaternion.Euler(0f, 90f, 0f));
            door.transform.localScale = new Vector3(1.0f, 2.1f, 0.08f);
            door.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/M_Metal_Green.mat");
            GameObjectUtility.SetStaticEditorFlags(door, StaticEditorFlags.BatchingStatic);
            // 인카운터 적 대기 자리 = 자판기 앞(동쪽 2.5~4m, 계단 입구 남쪽)
            var enc = AssetDatabase.LoadAssetAtPath<EncounterDef>(StageSetup.EncounterPath);
            if (enc != null && enc.Foes.Length >= 3)
            {
                var foes = enc.Foes;
                foes[0].Local = new Vector2(west + 3.4f, -4.2f); foes[1].Local = new Vector2(west + 4.5f, -2.6f); foes[2].Local = new Vector2(west + 3.1f, -1.2f);
                enc.Foes = foes; EditorUtility.SetDirty(enc);
            }
            return $"주차장 소품 {placed}개 + 국밥집 뒷문(서쪽 뒷벽 북쪽 끝) · 자판기 (로컬 x {west + 0.75f:F1}, z −3.0) · 적 대기 자판기 앞";
        }

        // ───────────────────────── 바닥·길 재질(Zone1Builder 의 M_Z1_<종류>)을 아트 텍스처 + 툰으로
        static readonly Dictionary<string, (string tex, float tile, Color tint)> GroundTex = new Dictionary<string, (string, float, Color)>
        {
            ["road"] = ("M_Asphalt", 3.0f, new Color(0.80f, 0.80f, 0.80f)), ["driveway"] = ("M_Asphalt", 3.0f, new Color(0.84f, 0.83f, 0.82f)), ["alley"] = ("M_Asphalt", 3.0f, new Color(0.98f, 0.96f, 0.93f)),
            ["asphalt"] = ("M_Asphalt", 3.0f, new Color(0.80f, 0.80f, 0.80f)), ["sidewalk"] = ("M_Paving", 1.2f, new Color(0.97f, 0.93f, 0.88f)), ["wallpath"] = ("M_Granite", 1.5f, Color.white),
            ["pad_stone"] = ("M_Granite", 1.5f, Color.white), ["ground"] = ("M_Dirt", 3.0f, Color.white), ["dirt"] = ("M_Dirt", 3.0f, Color.white),
            ["stairs"] = ("M_Granite", 1.5f, new Color(0.95f, 0.94f, 0.92f)),
            ["pad_asphalt"] = ("M_Asphalt", 3.0f, new Color(0.97f, 0.96f, 0.95f)),   // 0.78 이면 새벽 주차장 L* 22.8(D07 28 미달) ["pad_dirt"] = ("M_Dirt", 3.0f, Color.white), ["apron"] = ("M_Dirt", 3.0f, new Color(0.9f, 0.88f, 0.84f)),
        };

        static int GroundMaterials(Transform zoneRoot)
        {
            int n = 0;
            var used = new Dictionary<Material, Mesh>();
            foreach (var mf in zoneRoot.GetComponentsInChildren<MeshFilter>(true))
            {
                var r = mf.GetComponent<MeshRenderer>();
                if (r == null || r.sharedMaterial == null || mf.sharedMesh == null) continue;
                if (!used.ContainsKey(r.sharedMaterial)) used[r.sharedMaterial] = mf.sharedMesh;
            }
            foreach (var kv in used)
            {
                var m = kv.Key;
                if (!m.name.StartsWith("M_Z1_")) continue;
                string key = m.name.Substring(5);
                if (!GroundTex.TryGetValue(key, out var g)) continue;
                if (kv.Value.uv == null || kv.Value.uv.Length == 0) { Notes.Add($"바닥 재질 {m.name}: 메시에 UV 없음 — 그대로"); continue; }
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{g.tex}.png");
                if (tex == null) continue;
                string path = AssetDatabase.GetAssetPath(m);
                Toon(path, tex, Vector2.one / g.tile, Vector2.zero, g.tint);
                n++;
            }
            return n;
        }

        // ───────────────────────── 원경 카드 + 구름 + 달(Zone1/Sky)
        static void SkyRig(Transform zoneRoot)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(ArcMeshPath);
            if (mesh == null) { Notes.Add("원경 호 메시 없음 — 건너뜀"); return; }
            var sky = new GameObject("Sky").transform; sky.SetParent(zoneRoot, false); sky.localRotation = Quaternion.Euler(0f, SkyYaw7, 0f);   // 7차: 남산이 주차장(f02) 하늘 틈에
            var cards = sky.gameObject.AddComponent<SkyCards>();
            cards.Radius = SkyRadius;
            var arcs = new List<Renderer>();
            foreach (var (name, yaw) in new[] { ("North", 0f), ("East", 90f), ("South", 180f), ("West", 270f) })
            {
                var go = new GameObject("카드 " + name);
                go.transform.SetParent(sky, false);
                go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                go.transform.localPosition = new Vector3(0f, -SkyRadius * Mathf.PI / 2f * 1152f / 2688f * SkySquash * SkyDrop, 0f);   // 4차: 눌러서 내림(위 SkySquash 주석)
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{SkyMatDir}/M_Sky_{name}.mat");
                r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                r.allowOcclusionWhenDynamic = false;
                arcs.Add(r);
            }
            cards.Arcs = arcs.ToArray();
            var clouds = new List<Renderer>();
            Renderer Quad(string name, int tex, float yaw, float height, float size, float aspect)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.name = name;
                go.transform.SetParent(sky, false);
                float rr = SkyRadius * 0.93f;
                go.transform.localPosition = new Vector3(Mathf.Sin(yaw * Mathf.Deg2Rad) * rr, height, Mathf.Cos(yaw * Mathf.Deg2Rad) * rr);
                go.transform.localRotation = Quaternion.Euler(0f, yaw + 180f, 0f);
                go.transform.localScale = new Vector3(size, size / aspect, 1f);
                var r = go.GetComponent<MeshRenderer>();
                r.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{SkyMatDir}/M_Cloud_{tex:00}.mat");
                r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                return r;
            }
            // 4차: 구름 판(외곽선 있는 만화 구름) 대신 카드 셰이더의 절차 붓 구름(층운·적운, 산·도시 실루엣 뒤) — 판은 만들지 않음
            cards.Clouds = clouds.ToArray();
            cards.Moon = Quad("초승달", 8, 135f, 230f, 42f, 1f);
            cards.Apply(DayPart.Day);
            var vista = zoneRoot.Find("Landmarks/vista");
            if (vista != null) foreach (var r in vista.GetComponentsInChildren<MeshRenderer>(true)) if (r.GetComponent<TMP_Text>() == null) r.enabled = false;
        }

        // ═════════════════════════ 비교 사진(그래픽 필요): -out <폴더> [-only 1,7]
        // 키트 미리 보기 8곳(tools/env/preview_env.py shots) + 밤 2곳(1·7) + 시간대 4종 × 3곳(1·2·4)
        public static void Shots()
        {
            int code = 0;
            try
            {
                string outDir = Arg("-out") ?? throw new ArgumentException("-out <폴더>");
                if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) throw new Exception("그래픽 장치 없음(-nographics 빼기)");
                Directory.CreateDirectory(outDir);
                ShaderUtil.allowAsyncCompilation = false;
                var scene = EditorSceneManager.OpenScene(Root + "/Scenes/Zone1.unity", OpenSceneMode.Single);
                var d = Zone1Data.Load();
                var zone = GameObject.Find("Zone1")?.transform ?? throw new Exception("Zone1 루트 없음");
                var cam = Camera.main ?? throw new Exception("Main Camera 없음");
                var day = Object.FindFirstObjectByType<DayLight>() ?? throw new Exception("DayLight 없음");
                DayLight.EditModeFilm = true;
                if (LightSpot.All.Count == 0) foreach (var s in Object.FindObjectsByType<LightSpot>(FindObjectsInactive.Include, FindObjectsSortMode.None)) LightSpot.All.Add(s);
                var sky = Object.FindFirstObjectByType<SkyCards>();
                if (sky != null && Arg("-skyYaw") != null) sky.transform.rotation = Quaternion.Euler(0f, float.Parse(Arg("-skyYaw")), 0f);   // 개발용: 원경 돌려 보기
                var player = GameObject.Find("Player");
                // 게임 화면이 아닌 것 숨김: 체크포인트·스폰 표시·전투 무대 표시·이름표·HUD
                foreach (var n in new[] { "Zone1/Route", "Zone1/Spawn", "Zone1/Landmarks/arena", "Zone1/Landmarks/npc_spot", "Zone1/Shots", "Stages" })
                { var g = GameObject.Find(n); if (g != null) g.SetActive(false); }
                foreach (var t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) if (!t.name.StartsWith("글자")) t.gameObject.SetActive(false);
                foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) c.gameObject.SetActive(false);
                var unpose = BatchTools.PoseAnimators(1.0f);
                var spots = PreviewSpots(d);
                bool camB = Arg("-camA") == null;
                var only = (Arg("-only") ?? "").Split(',').Where(x => x.Length > 0).ToHashSet();
                var list = new List<(string file, int spot, DayPart part)>();
                var frameParts = new Dictionary<int, DayPart> { [1] = DayPart.Day, [2] = DayPart.Dusk, [3] = DayPart.Dawn, [4] = DayPart.Dawn, [5] = DayPart.Day, [6] = DayPart.Day, [7] = DayPart.Day, [8] = DayPart.Day };
                for (int i = 1; i <= 8; i++) list.Add(($"shot_{i}", i, frameParts[i]));
                list.Add(("shot_1_night", 1, DayPart.Night)); list.Add(("shot_7_night", 7, DayPart.Night));
                foreach (int sp in new[] { 1, 2, 4 }) foreach (DayPart p in new[] { DayPart.Dawn, DayPart.Day, DayPart.Dusk, DayPart.Night }) list.Add(($"tod_{sp}_{p}", sp, p));
                var log = new List<string>();
                foreach (var (file, sp, part) in list)
                {
                    if (only.Count > 0 && !only.Contains(sp.ToString())) continue;
                    var (name, feet, yaw, pitch, dist, fov) = spots[sp - 1];
                    float th = 1.40f;
                    if (camB) { pitch = 3f; dist = 5.5f; fov = 53f; th = 1.15f; }   // 5차: 탐색 카메라 B(2026-10-24 대표 확정)로 비교
                    if (player != null) player.transform.SetPositionAndRotation(feet, Quaternion.Euler(0f, yaw, 0f));
                    var r = Quaternion.Euler(pitch, yaw, 0f);
                    var tgt = feet + Vector3.up * th;
                    var back = -(r * Vector3.forward);
                    Physics.SyncTransforms();
                    float dd = dist;
                    if (Physics.SphereCast(tgt, 0.1f, back, out var hit, dist, (1 << Layers.Ground) | (1 << Layers.Wall), QueryTriggerInteraction.Ignore)) dd = Mathf.Max(0.6f, hit.distance - 0.05f);
                    cam.transform.SetPositionAndRotation(tgt + back * dd, r);
                    if (camB) cam.transform.position += (r * Vector3.right) * (Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * dd * 0.06f * 16f / 9f);
                    cam.fieldOfView = fov; cam.nearClipPlane = 0.08f; cam.farClipPlane = 900f;
                    day.Apply(part, true);
                    if (sky != null) { sky.Follow(cam); sky.Apply(part); }
                    string path = Path.Combine(outDir, file + ".png");
                    BatchTools.RenderToPng(cam, 1920, 1080, 2, path);
                    log.Add($"{file}: {name} {part} 발 ({feet.x:F1}, {feet.y:F1}, {feet.z:F1}) yaw {yaw:F0} 거리 {dd:F2}");
                }
                unpose?.Invoke();
                File.WriteAllLines(Path.Combine(outDir, "shots.txt"), log, new System.Text.UTF8Encoding(true));
                Debug.Log($"{Tag} 비교 사진 {log.Count}장 → {outDir}\n{string.Join("\n", log)}");
            }
            catch (Exception e) { Debug.LogError($"{Tag} Shots 실패: {e}"); code = 1; }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        /// tools/env/preview_env.py shots() 와 같은 8곳(이름, 발, yaw, 피치, 거리, FOV)
        public static (string, Vector3, float, float, float, float)[] PreviewSpots(Zone1Data d)
        {
            Vector3 On(string road, float frac)
            {
                var r = d.Roads.First(x => x.Name == road);
                var pts = r.Points; float tot = 0f;
                for (int i = 0; i < pts.Length - 1; i++) tot += Flat(pts[i + 1] - pts[i]).magnitude;
                float s = tot * frac;
                for (int i = 0; i < pts.Length - 1; i++) { float L = Flat(pts[i + 1] - pts[i]).magnitude; if (s <= L || i == pts.Length - 2) return Vector3.Lerp(pts[i], pts[i + 1], Mathf.Clamp01(s / Mathf.Max(L, 1e-4f))); s -= L; }
                return pts[pts.Length - 1];
            }
            float YawTo(Vector3 a, Vector3 b) => Mathf.Atan2(b.x - a.x, b.z - a.z) * Mathf.Rad2Deg;
            Vector3 Ground(Vector3 p) { if (Physics.Raycast(p + Vector3.up * 5f, Vector3.down, out var h, 20f, 1 << Layers.Ground, QueryTriggerInteraction.Ignore)) p.y = h.point.y; return p; }
            var mr = d.Roads[0].Points; Vector3 p1 = Vector3.zero;
            for (int i = 0; i < mr.Length - 1; i++) if (mr[i].x <= -193f && -193f <= mr[i + 1].x) { float t = (-193f - mr[i].x) / (mr[i + 1].x - mr[i].x); p1 = Vector3.Lerp(mr[i], mr[i + 1], t) + new Vector3(0f, 0f, 1.6f); }
            var pk = d.Pads.First(x => x.Name.Contains("주차장")).Center;
            var dw = d.Roads.First(x => x.Name == "주차장 진입로").Points;
            var p2 = new Vector3(dw[0].x, pk.y, dw[0].z);
            var st = d.Stairs.First(s => s.Name.Contains("꼭대기 계단"));
            Vector3 lo = st.From.y <= st.To.y ? st.From : st.To, hi = st.From.y <= st.To.y ? st.To : st.From;
            var dir = Flat(hi - lo).normalized;
            var p3 = Ground(lo - dir * 2f);
            var p4 = On("후문 옆 공원길(와룡공원 오르막)", 0.15f);
            var p5 = On("와룡공원 성곽길", 0.33f);
            var p6 = On("명륜3가 골목 동쪽(명륜1가 방면)", 0.18f);
            var p7 = On("명륜3가 골목(시우네 골목)", 0.04f);
            var p8 = On("성곽 안쪽 숲길(혜성고 방면)", 0.42f);
            return new[]
            {
                ("1_후문상가거리", Ground(p1), 72f, 4f, 4.0f, 45f),
                ("2_뒷골목주차장", Ground(p2), YawTo(p2, new Vector3(pk.x - 7f, 0f, pk.z - 4f)), 5f, 3.8f, 45f),
                ("3_골목계단", p3, YawTo(lo, hi), -4f, 4.0f, 50f),
                ("4_와룡오르막", Ground(p4), YawTo(p4, On("후문 옆 공원길(와룡공원 오르막)", 1f)) + 12f, 2f, 3.8f, 45f),
                ("5_성곽길", Ground(p5), YawTo(p5, On("와룡공원 성곽길", 0f)) + 8f, 3f, 3.8f, 45f),
                ("6_한옥골목", Ground(p6), YawTo(p6, On("명륜3가 골목 동쪽(명륜1가 방면)", 0.62f)) + 14f, 4f, 3.6f, 48f),
                ("7_명륜3가골목", Ground(p7), YawTo(p7, On("명륜3가 골목(시우네 골목)", 0.75f)), 3f, 3.4f, 50f),
                ("8_성곽아래빈집", Ground(p8), YawTo(p8, new Vector3(-142.5f, 0f, 145f)) - 10f, 3f, 4.0f, 45f),
            };
        }

        // ═════════════════════════ 실험(개발용): -executeMethod Haengin.EditorGame.EnvSetup.Exp -out <폴더> — 3번 자리에서 조건을 하나씩 바꿔 찍는다
        public static void Exp()
        {
            int code = 0;
            try
            {
                string outDir = Arg("-out") ?? Path.GetTempPath(); Directory.CreateDirectory(outDir);
                ShaderUtil.allowAsyncCompilation = false;
                EditorSceneManager.OpenScene(Root + "/Scenes/Zone1.unity", OpenSceneMode.Single);
                var d = Zone1Data.Load();
                var cam = Camera.main; var day = Object.FindFirstObjectByType<DayLight>(); var sky = Object.FindFirstObjectByType<SkyCards>();
                var (name, feet, yaw, pitch, dist, fov) = PreviewSpots(d)[2];
                var r = Quaternion.Euler(pitch, yaw, 0f); var tgt = feet + Vector3.up * 1.4f; var back = -(r * Vector3.forward);
                cam.transform.SetPositionAndRotation(tgt + back * dist, r); cam.fieldOfView = fov;
                var log = new List<string>();
                void Shot(string tag) { string pth = Path.Combine(outDir, $"exp_{tag}.png"); BatchTools.RenderToPng(cam, 960, 540, 1, pth); var t = new Texture2D(2, 2); t.LoadImage(File.ReadAllBytes(pth)); var c = t.GetPixel(750, 540 - 210); log.Add($"{tag}: ({c.r * 255:F0},{c.g * 255:F0},{c.b * 255:F0})"); Object.DestroyImmediate(t); }
                Shot("0_asis");
                if (sky != null) { sky.Follow(cam); Shot("1_skyfollow"); }
                if (day != null) { DayLight.EditModeFilm = true; day.Apply(DayPart.Day, true); Shot("2_daylight"); }
                var rd = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(Renderers[0]);
                var outline = rd.rendererFeatures.FirstOrDefault(f => f != null && f.name == "Outline");
                if (outline != null) { outline.SetActive(false); rd.SetDirty(); Shot("3_no_outline"); outline.SetActive(true); rd.SetDirty(); }
                if (sky != null) { sky.gameObject.SetActive(false); Shot("4_no_sky"); sky.gameObject.SetActive(true); }
                var sun = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).First(l => l.type == LightType.Directional);
                var ls = sun.shadows; sun.shadows = LightShadows.None; Shot("5_no_shadow"); sun.shadows = ls;
                var env = GameObject.Find("Zone1/Env");
                foreach (var mr in env.GetComponentsInChildren<MeshRenderer>()) if (mr.name.Contains("_Detail_")) mr.enabled = false;
                Shot("6_no_detail");
                foreach (var mr in env.GetComponentsInChildren<MeshRenderer>(true)) if (mr.name.Contains("_Detail_")) mr.enabled = true;
                var cd = cam.GetComponent<UniversalAdditionalCameraData>(); cd.renderPostProcessing = false; Shot("7_no_post"); cd.renderPostProcessing = true;
                var v = env.GetComponentsInChildren<Transform>(true).First(x => x.name == "villa_011");
                var lg = v.GetComponent<LODGroup>(); lg.ForceLOD(1); Shot("8_villa011_lod1"); lg.ForceLOD(-1);
                var mr0 = v.GetComponentsInChildren<MeshRenderer>().First(x => x.name == "villa_011_LOD0");
                var save = mr0.sharedMaterials; var lit = new Material(Shader.Find("Universal Render Pipeline/Lit")); lit.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/M_Brick_Brown.png"));
                mr0.sharedMaterials = save.Select(_ => lit).ToArray(); Shot("9_villa011_urplit"); mr0.sharedMaterials = save;
                Debug.Log($"{Tag} 실험 결과(화소 750,330):" + System.Environment.NewLine + string.Join(System.Environment.NewLine, log));
            }
            catch (Exception e) { Debug.LogError($"{Tag} Exp 실패: {e}"); code = 1; }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        // ═════════════════════════ 삼각형 내역(개발용): -executeMethod Haengin.EditorGame.EnvSetup.TriBreakdown — D08 밤 상가거리 시작 자리 근처(키트 미리 보기 1번 자리)에서 묶음별로 꺼 보며 UnityStats.triangles
        public static void TriBreakdown()
        {
            int code = 0;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                EditorSceneManager.OpenScene(Root + "/Scenes/Zone1.unity", OpenSceneMode.Single);
                var d = Zone1Data.Load(); var cam = Camera.main; var day = Object.FindFirstObjectByType<DayLight>();
                if (LightSpot.All.Count == 0) foreach (var s0 in Object.FindObjectsByType<LightSpot>(FindObjectsInactive.Include, FindObjectsSortMode.None)) LightSpot.All.Add(s0);
                day.Apply(DayPart.Night, true);
                var feet = new Vector3(-199.0f, 31.0f, 103.2f); if (Physics.Raycast(feet + Vector3.up * 3f, Vector3.down, out var gh, 10f, 1 << Layers.Ground)) feet = gh.point;
                float yaw = Mathf.Atan2(-176f + 199f, 103.6f - 103.2f) * Mathf.Rad2Deg;
                var r = Quaternion.Euler(8f, yaw, 0f); var tgt = feet + Vector3.up * 1.4f;
                cam.transform.SetPositionAndRotation(tgt - r * Vector3.forward * 4f, r); cam.fieldOfView = 45f;
                var player = GameObject.Find("Player"); if (player != null) player.transform.SetPositionAndRotation(feet, Quaternion.Euler(0f, yaw, 0f));
                var rt = new RenderTexture(1920, 1080, 24); cam.targetTexture = rt;
                long Tri() { cam.Render(); return UnityEditor.UnityStats.triangles; }
                var log = new List<string>();
                long all = Tri(); log.Add($"전부 {all:N0}");
                var lightsSun = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).First(l => l.type == LightType.Directional);
                var sh = lightsSun.shadows; lightsSun.shadows = LightShadows.None; log.Add($"그림자 끔 {Tri():N0}"); lightsSun.shadows = sh;
                foreach (var path in new[] { "Zone1/Env/Veg", "Zone1/Env/Decor", "Zone1/Env/Props", "Zone1/Env/Lot", "Zone1/Sky", "Player", "Stages", "Zone1/Env/Z1_HoomunN", "Zone1/Env/Z1_HoomunS", "Zone1/Env/Z1_Alley", "Zone1/Env/Z1_Campus", "Zone1/Env/Z1_Wires", "Zone1/Env/Z1_Wall", "Zone1/Env/Z1_Waryong", "Zone1/Ground", "Zone1/Roads", "Zone1/Blocks", "Zone1/Landmarks" })
                {
                    var g = GameObject.Find(path); if (g == null) { log.Add($"{path}: 없음"); continue; }
                    g.SetActive(false); long t = Tri(); g.SetActive(true);
                    log.Add($"{path} 끄면 {t:N0} (몫 {all - t:N0})");
                }
                // 소켓 소품만(Z1_* 아래 Prop_)
                var props = Object.FindObjectsByType<LODGroup>(FindObjectsSortMode.None).Where(l => l.transform.parent != null && l.transform.parent.name.StartsWith("Prop_")).Select(l => l.gameObject).ToList();
                foreach (var p in props) p.SetActive(false); long tp = Tri(); foreach (var p in props) p.SetActive(true);
                log.Add($"소켓 소품 {props.Count}개 끄면 {tp:N0} (몫 {all - tp:N0})");
                cam.targetTexture = null; rt.Release();
                Debug.Log($"{Tag} 삼각형 내역:" + System.Environment.NewLine + string.Join(System.Environment.NewLine, log));
            }
            catch (Exception e) { Debug.LogError($"{Tag} TriBreakdown 실패: {e}"); code = 1; }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        // ═════════════════════════ 진단(개발용): -executeMethod Haengin.EditorGame.EnvSetup.Diag -out <폴더> [-name villa_011]
        public static void Diag()
        {
            int code = 0;
            try
            {
                string outDir = Arg("-out") ?? Path.GetTempPath(); Directory.CreateDirectory(outDir);
                string name = Arg("-name") ?? "villa_011";
                EditorSceneManager.OpenScene(Root + "/Scenes/Zone1.unity", OpenSceneMode.Single);
                var t = GameObject.Find("Zone1/Env").GetComponentsInChildren<Transform>(true).First(x => x.name == name);
                var sb = new System.Text.StringBuilder();
                foreach (var r in t.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var mf = r.GetComponent<MeshFilter>(); var me = mf != null ? mf.sharedMesh : null;
                    sb.AppendLine($"{r.name}: enabled {r.enabled} layer {r.gameObject.layer} shadows {r.shadowCastingMode} mats {string.Join(",", r.sharedMaterials.Select(m => m == null ? "null" : m.name + "/" + (m.shader != null ? m.shader.name : "?") + "/" + (m.GetTexture("_MainTex") != null ? m.GetTexture("_MainTex").name : "noTex")))}");
                    if (me != null)
                    {
                        var ns = me.normals; var uv = me.uv; var vs = me.vertices;
                        int zero = ns.Count(v => v.sqrMagnitude < 0.25f), nan = ns.Count(v => float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z));
                        int nanV = vs.Count(v => float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z));
                        sb.AppendLine($"   mesh {me.name} v {me.vertexCount} sub {me.subMeshCount} normals {ns.Length} zero {zero} nan {nan} vertsNaN {nanV} uv {uv.Length} bounds {me.bounds.center} {me.bounds.size} tangents {me.tangents.Length} colors {me.colors.Length} uv2 {me.uv2.Length}");
                        if (ns.Length > 0) sb.AppendLine($"   n[0..3] {ns[0]} {ns[Mathf.Min(1, ns.Length - 1)]} {ns[Mathf.Min(2, ns.Length - 1)]}  uv[0..2] {(uv.Length > 0 ? uv[0].ToString() : "-")} {(uv.Length > 1 ? uv[1].ToString() : "-")}");
                    }
                }
                var lg = t.GetComponent<LODGroup>();
                if (lg != null) sb.AppendLine($"LODGroup size {lg.size} lods {string.Join(" ", lg.GetLODs().Select(l => l.screenRelativeTransitionHeight.ToString("F3") + ":" + l.renderers.Length))} localRef {lg.localReferencePoint}");
                Debug.Log($"{Tag} 진단 {name}:" + System.Environment.NewLine + sb);
                if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
                {
                    var cam = Camera.main;
                    var b = t.GetComponentsInChildren<Renderer>().First(r => r.name.EndsWith("_LOD0")).bounds;
                    float dist = float.TryParse(Arg("-dist"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var dv) ? dv : 14f;
                    var pos = b.center + new Vector3(-6f * dist / 14f, 0f, -dist); pos.y = b.min.y + 1.6f;
                    foreach (var tx in new[] { "M_Brick_Brown", "M_Brick_Red" }) { var t2 = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{tx}.png"); Debug.Log($"{Tag} 텍스처 {tx}: {t2.width}x{t2.height} {t2.format} mips {t2.mipmapCount} streaming {t2.streamingMipmaps} loadedMip {t2.loadedMipmapLevel} requested {t2.requestedMipmapLevel} minimum {t2.minimumMipmapLevel}"); }
                    Debug.Log($"{Tag} 품질: streaming {QualitySettings.streamingMipmapsActive} budget {QualitySettings.streamingMipmapsMemoryBudget} masterLimit {QualitySettings.globalTextureMipmapLimit}");
                    cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(b.center - pos));
                    cam.fieldOfView = 50f;
                    var lod0 = t.GetComponentsInChildren<MeshRenderer>().First(r => r.name.EndsWith("_LOD0") && !r.name.Contains("Detail"));
                    BatchTools.RenderToPng(cam, 1280, 720, 1, Path.Combine(outDir, $"diag_{name}_a.png"));
                    var save = lod0.sharedMaterials;
                    lod0.sharedMaterials = save.Select(_ => AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/M_Brick_Red.mat")).ToArray();
                    BatchTools.RenderToPng(cam, 1280, 720, 1, Path.Combine(outDir, $"diag_{name}_b_allred.png"));
                    var lit = new Material(Shader.Find("Universal Render Pipeline/Lit")); lit.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/M_Brick_Brown.png"));
                    lod0.sharedMaterials = save.Select(_ => lit).ToArray();
                    BatchTools.RenderToPng(cam, 1280, 720, 1, Path.Combine(outDir, $"diag_{name}_c_urplit.png"));
                    lod0.sharedMaterials = save;
                    foreach (var f in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) if (f.type == LightType.Directional) f.transform.rotation = Quaternion.Euler(50f, 20f, 0f);
                    BatchTools.RenderToPng(cam, 1280, 720, 1, Path.Combine(outDir, $"diag_{name}_d_sunN.png"));
                }
            }
            catch (Exception e) { Debug.LogError($"{Tag} Diag 실패: {e}"); code = 1; }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
        static string Arg(string name)
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (string.Equals(a[i], name, StringComparison.OrdinalIgnoreCase)) return a[i + 1];
            return null;
        }
        public static void EnsureFolderPublic(string path) => EnsureFolder(path);
        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
