// 행인1의 메인이벤트 — 혜화동 1구역(성대 후문 ~ 와룡공원) 그레이박스 장면 자동 생성
// batchmode: -executeMethod Haengin.EditorTools.Zone1Builder.Build   (-nographics 가능)
// 입력  Assets/_Project/Data/zone1.json  (tools/zone1_gen.py 가 만든 배치도, 규칙은 docs/06_M1_그레이박스_설계.md 14장)
// 출력  Assets/_Project/Scenes/Zone1.unity + Zone1_Mesh.asset(생성 메시) + Materials/Zone1/M_Z1_*.mat + Settings/Zone1_FilmVolume.asset
// 다시 실행하면 장면·메시를 덮어쓴다. 재질은 같은 경로에서 값만 고친다(GUID 유지).
// 레이어는 07_M1_조작_설계 3-5 대로 6 Ground / 7 Wall / 8 Player / 9 PlayerOnly / 10 Interact / 11 CamBlock (비어 있으면 이름을 넣음).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;
using NavBuilder = UnityEngine.AI.NavMeshBuilder;

namespace Haengin.EditorTools
{
    public static class Zone1Builder
    {
        const string Tag = "[Zone1Builder]";
        public const string ScenePath = SandboxSetup.Root + "/Scenes/Zone1.unity";
        public const string MeshPath = SandboxSetup.Root + "/Scenes/Zone1_Mesh.asset";
        public const string MatDir = SandboxSetup.MatDir + "/Zone1";
        public const string VolumePath = SandboxSetup.SettingsDir + "/Zone1_FilmVolume.asset";

        /// 07_M1_조작_설계 3-5. 번호 6 부터 차례로.
        public static readonly string[] LayerNames = { "Ground", "Wall", "Player", "PlayerOnly", "Interact", "CamBlock" };
        const int IgnoreRaycastLayer = 2;

        const float CarveMargin = 2.0f;  // 걷는 면 둘레 이 거리 안의 지형 점은 그 면 아래로 내린다(2m 격자 삼각형이 길 위로 솟지 않게)
        const float CarveEps = 0.12f;    // 걷는 면보다 이만큼 낮게
        const float Skirt = 3f;          // 길·패드를 아래로 내려 그리는 깊이(지형과 틈 없게)
        const float PlayerRadius = 0.25f;
        static readonly float[] ApronDist = { 0f, 40f, 420f };  // 구역 밖 지형(보이기만): 경계에서 거리
        static readonly float[] ApronDrop = { 0f, 6f, 70f };    //                        그 거리에서 내려가는 높이
        static readonly Color Ink = new Color(0.102f, 0.078f, 0.090f); // #1A1417

        // ── 빌드 중 상태
        static Zone1Data D;
        static Zone1Data.HeightGrid G;   // 걷는 면 아래를 깎은 지형
        static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();
        static readonly List<Mesh> Meshes = new List<Mesh>();
        static readonly List<string> Notes = new List<string>();
        static int LGround, LWall, LPlayerOnly, GroundMask, BlockMask;
        static Collider TerrainCol;
        static TMP_FontAsset LabelFont;
        static Material LabelMat;
        static Texture2D StoneTex, PaveTex, BeamTex;
        static int FixedBlocks, BuriedBlocks, FixedMarks, Labels;
        static float MaxBlockFix, MaxMarkFix;

        // 텍스처가 덮는 실제 크기(m) — MB 의 UV 가 미터 단위라 재질 Tiling = 1 / 크기
        const float StoneTileW = 2.4f, StoneTileH = 2.52f, PaveTile = 2.0f;
        /// 이름표 기본 글자 크기(TMP 3D: fontSize 1 = 0.1m). 런타임 NameTags 가 거리에 따라 키운다.
        public const float LabelSize = 4.5f, CheckpointLabelSize = 5.5f;
        /// 생성 체크포인트 부품 이름(런타임 RouteGuide 가 이 이름으로 찾는다)
        public const string CpDisc = "원판", CpRing = "테두리", CpPillar = "기둥", CpFlag = "깃발", CpBeam = "빛 기둥", CpTag = "이름표", CpTrigger = "트리거";

        /// 장면을 저장하기 직전에 부르는 확장 지점. 이 어셈블리는 게임 코드를 모르므로,
        /// M1 리그(플레이어·카메라)는 Haengin.EditorGame.M1Setup 이 여기에 등록해 붙인다 → Zone1 을 다시 만들어도 리그가 유지된다.
        public static event Action<Zone1Data, Transform> BeforeSave;

        // ───────────────────────── 진입점
        [MenuItem("Haengin/Zone1 장면 다시 만들기")]
        public static void Build()
        {
            int code = 0;
            try { Rebuild(); }
            catch (Exception e) { Debug.LogError($"{Tag} 실패: {e}"); code = 1; }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        /// 종료하지 않고 장면만 다시 만든다(실패하면 예외). 다른 배치 메서드가 이어서 쓸 때.
        public static void Rebuild()
        {
            try { BuildScene(); }
            finally
            {
                Mats.Clear(); Meshes.Clear(); D = null; G = null; TerrainCol = null; LabelFont = null; LabelMat = null;
                StoneTex = PaveTex = BeamTex = null;
            }
        }

        static void BuildScene()
        {
            var t0 = DateTime.Now;
            Notes.Clear();
            FixedBlocks = BuriedBlocks = FixedMarks = Labels = 0; MaxBlockFix = MaxMarkFix = 0f;

            D = Zone1Data.Load();
            Debug.Log($"{Tag} 배치도 읽음: {D.Name} v{D.Version} | 길 {D.Roads.Count} · 패드 {D.Pads.Count} · 계단 {D.Stairs.Count} · 블록 {D.Blocks.Count} · " +
                      $"성곽 {D.Walls.Count} · 랜드마크 {D.Landmarks.Count} · 체크포인트 {D.Route.Count} · 색 {D.Colors.Count} | 격자 {D.Grid.NX}x{D.Grid.NZ}");

            EnsureLayers();
            EnsureFolder(MatDir); EnsureFolder(SandboxSetup.SettingsDir); EnsureFolder(SandboxSetup.Root + "/Scenes");
            // 메시 에셋은 지우지 않는다(GUID 유지, SaveMeshes 가 안의 서브 에셋만 바꿈). 필름 볼륨도 제자리 갱신.
            var profile = SandboxSetup.MakeFilmVolume(VolumePath);
            LabelFont = KoreanFont.Ensure(Notes);
            LabelMat = KoreanFont.LabelMaterial(LabelFont);
            // 성곽 돌: 한 켜 0.50m(5켜/2.52m), 돌 길이 0.50~0.98m(가로세로 1:1~2:1 다듬돌) — 벽돌처럼 잘고 길쭉하게 보이지 않게. 줄눈은 돌보다 조금만 어둡게
            StoneTex = GenTexture("T_Z1_Stone", 512, 512, StonePixels(512, 5, 0.50f, 0.98f, StoneTileW, 11, 4, 0.66f, 0.80f), true);
            PaveTex = GenTexture("T_Z1_Paving", 512, 512, StonePixels(512, 5, 0.45f, 0.80f, PaveTile, 23, 4, 0.70f, 0.86f), true);
            BeamTex = GenTexture("T_Z1_Beam", 8, 64, BeamPixels(8, 64), false);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupEnvironment();

            var root = new GameObject("Zone1").transform;
            var gGround = Group(root, "Ground");
            var gRoads = Group(root, "Roads");
            var gBlocks = Group(root, "Blocks");
            var gWalls = Group(root, "Walls");
            var gStairs = Group(root, "Stairs");
            var gMarks = Group(root, "Landmarks");
            var gRoute = Group(root, "Route");

            BuildGround(gGround);
            BuildPads(gGround);
            BuildRoads(gRoads);
            BuildStairs(gStairs);
            BuildWalls(gWalls);
            BuildBounds(Group(root, "Bounds"));
            Physics.SyncTransforms();

            BuildBlocks(gBlocks);
            Physics.SyncTransforms();

            BuildLandmarks(gMarks);
            BuildRoute(gRoute);
            BuildSpawn(root);
            var shots = BuildShots(Group(root, "Shots"));
            BuildLightCameraVolume(profile, shots[1]);
            Physics.SyncTransforms();

            int blockedCenter = CheckClearance();
            var nav = CheckNavMesh();

            if (BeforeSave != null)
            {
                BeforeSave(D, root);
                Notes.Add($"확장 지점 BeforeSave 실행({BeforeSave.GetInvocationList().Length}개) — M1 리그 등");
            }

            SaveMeshes();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            int meshCount = Meshes.Count;
            Stabilize();
            KoreanFont.ResetDynamic(LabelFont);

            Debug.Log($"{Tag} 완료: {ScenePath} | 메시 {meshCount}개 → {MeshPath} | 재질 {Mats.Count}개 · 이름표 {Labels}개 | " +
                      $"떠 있던 블록 보정 {FixedBlocks}개(최대 {MaxBlockFix:F2}m) · 묻힌 블록 {BuriedBlocks}개 · 랜드마크 바닥 보정 {FixedMarks}개(최대 {MaxMarkFix:F2}m) | " +
                      $"길 가운데선 막힘 {blockedCenter}곳 | NavMesh {nav} | {(DateTime.Now - t0).TotalSeconds:F1}초");
            foreach (var n in Notes) Debug.Log($"{Tag}   {n}");

            // -shots <폴더>: 그래픽이 있으면(-nographics 아님) 점검용 장면 컷을 바로 찍는다(Unity 한 번 띄울 때 여러 장)
            string shotDir = CmdArg("-shots");
            if (!string.IsNullOrEmpty(shotDir)) TakeShots(shotDir);
        }

        // ───────────────────────── 결정적 저장(문제 9)
        /// 저장한 장면·메시 에셋의 무작위 fileID 를 경로·이름 기준 값으로 바꾸고(StableIds), 다시 읽어 참조가 깨지지 않았는지 본다.
        static void Stabilize()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);   // 디스크 파일을 고치기 전에 장면을 내린다
            var meshMap = StableIds.RewriteSubAssets(MeshPath, MeshMainId, out int nMesh);
            var (changed, kept) = StableIds.RewriteScene(ScenePath, AssetDatabase.AssetPathToGUID(MeshPath), meshMap);
            AssetDatabase.ImportAsset(MeshPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            var sc = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            int objs = 0, broken = 0, missing = 0;
            foreach (var root in sc.GetRootGameObjects())
                foreach (var c in root.GetComponentsInChildren<Component>(true))
                {
                    objs++;
                    if (c == null) { missing++; continue; }
                    var so = new SerializedObject(c);
                    var it = so.GetIterator();
                    while (it.Next(true))
                        if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue == null && it.objectReferenceInstanceIDValue != 0)
                        {
                            broken++;
                            Debug.LogWarning($"{Tag} 깨진 참조: {PathOf(c.transform)} · {c.GetType().Name}.{it.propertyPath}");
                        }
                }
            foreach (var m in AssetDatabase.LoadAllAssetRepresentationsAtPath(MeshPath)) if (m == null) broken++;
            Notes.Add($"결정적 저장: 장면 fileID {changed}개를 계층 경로 기준으로(그대로 둔 것 {kept}) · 메시 서브 에셋 {nMesh}개를 이름 기준으로 · " +
                      $"다시 읽은 컴포넌트 {objs}개 중 깨진 참조 {broken} · 빠진 스크립트 {missing}");
            if (broken > 0 || missing > 0) throw new Exception($"fileID 를 바꾼 뒤 참조가 깨졌습니다(깨진 참조 {broken}, 빠진 스크립트 {missing})");
        }

        static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;

        const long MeshMainId = 4300000; // .asset 의 주 오브젝트(Mesh) fileID — 메시 목록용 빈 메시

        static string CmdArg(string name)
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++)
                if (string.Equals(a[i], name, StringComparison.OrdinalIgnoreCase)) return a[i + 1];
            return null;
        }

        // ───────────────────────── 레이어·폴더·재질
        static void EnsureLayers()
        {
            var tm = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset").FirstOrDefault();
            if (tm != null)
            {
                var so = new SerializedObject(tm);
                var layers = so.FindProperty("layers");
                var added = new List<string>();
                for (int k = 0; k < LayerNames.Length; k++)
                {
                    var p = layers.GetArrayElementAtIndex(6 + k);
                    if (string.IsNullOrEmpty(p.stringValue)) { p.stringValue = LayerNames[k]; added.Add($"{6 + k} {LayerNames[k]}"); }
                    else if (p.stringValue != LayerNames[k])
                        Debug.LogWarning($"{Tag} 레이어 {6 + k} 에 이미 '{p.stringValue}' 가 있어 '{LayerNames[k]}' 를 넣지 않았습니다(Default 로 대신함)");
                }
                if (added.Count > 0)
                {
                    so.ApplyModifiedPropertiesWithoutUndo();
                    AssetDatabase.SaveAssets();
                    Debug.Log($"{Tag} 레이어 이름 추가: {string.Join(", ", added)}");
                }
            }
            LGround = LayerOr0("Ground"); LWall = LayerOr0("Wall"); LPlayerOnly = LayerOr0("PlayerOnly");
            GroundMask = 1 << LGround;
            BlockMask = (1 << LWall) | (1 << LPlayerOnly);
        }

        static int LayerOr0(string n) { int l = LayerMask.NameToLayer(n); return l < 0 ? 0 : l; }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        static Color Col(string key)
        {
            if (D.Colors.TryGetValue(key, out var c)) return c;
            if (key == "vista") return Color.Lerp(Col("wall"), Col("fog"), 0.3f);
            if (key == "wallcap" || key == "wallpath")   // 0.1 데이터(색 없음) 호환
                return key == "wallcap" ? Color.Lerp(Col("wall"), Ink, 0.55f) : Col("pad_stone");
            Debug.LogWarning($"{Tag} 색 '{key}' 가 zone1.json colors 에 없어 분홍으로 칠함");
            return Color.magenta;
        }

        /// 텍스처를 쓰는 종류: 성곽 = 돌 줄눈, 성곽길·돌 계단참 = 판석. 나머지는 단색.
        static void TexFor(string key, out Texture2D tex, out Vector2 tiling)
        {
            tex = null; tiling = Vector2.one;
            if (key == "wall") { tex = StoneTex; tiling = new Vector2(1f / StoneTileW, 1f / StoneTileH); }
            else if (key == "wallpath" || key == "pad_stone") { tex = PaveTex; tiling = new Vector2(1f / PaveTile, 1f / PaveTile); }
        }

        /// 종류별 단색 URP Lit(스무스니스 0, 스펙큘러·반사 끔) — 06 문서 10장 색 규칙. 성곽·판석은 생성 텍스처 × 색.
        static Material Mat(string key)
        {
            if (Mats.TryGetValue(key, out var m)) return m;
            m = LoadOrCreate($"M_Z1_{key}", "Universal Render Pipeline/Lit");
            m.SetColor("_BaseColor", Col(key));
            TexFor(key, out var tex, out var tiling);
            m.SetTexture("_BaseMap", tex); m.SetTexture("_MainTex", tex);
            m.SetTextureScale("_BaseMap", tiling); m.SetTextureScale("_MainTex", tiling);
            m.SetTextureOffset("_BaseMap", Vector2.zero); m.SetTextureOffset("_MainTex", Vector2.zero);
            m.SetFloat("_Smoothness", 0f); m.SetFloat("_Metallic", 0f);
            m.SetFloat("_SpecularHighlights", 0f); m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            m.SetFloat("_EnvironmentReflections", 0f); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            m.SetFloat("_Surface", 0f); m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = -1;
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return Mats[key] = m;
        }

        /// 반투명 무광(체크포인트·전투 무대 표시). 게임 중에도 보이게 그림자·깊이 쓰기 없음. tex = 알파 그라데이션(빛 기둥).
        static Material Fx(string key, string colorKey, float alpha, Texture2D tex = null)
        {
            if (Mats.TryGetValue(key, out var m)) return m;
            m = LoadOrCreate($"M_Z1_{key}", "Universal Render Pipeline/Unlit");
            var c = Col(colorKey); c.a = alpha;
            m.SetColor("_BaseColor", c);
            m.SetTexture("_BaseMap", tex); m.SetTexture("_MainTex", tex);
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One); m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f); m.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off); m.SetFloat("_AlphaClip", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return Mats[key] = m;
        }

        static Material LoadOrCreate(string name, string shaderName)
        {
            var shader = Shader.Find(shaderName) ?? throw new Exception($"셰이더 '{shaderName}' 를 찾지 못했습니다");
            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
            else if (mat.shader != shader) mat.shader = shader;
            return mat;
        }

        // ───────────────────────── 생성 텍스처(셰이더 그래프 없이 URP Lit/Unlit 의 _BaseMap 으로)
        /// PNG 를 Materials/Zone1 에 쓴다(내용이 같으면 파일을 건드리지 않음 → 재임포트·diff 없음).
        static Texture2D GenTexture(string name, int w, int h, Color32[] px, bool tile)
        {
            string path = $"{MatDir}/{name}.png";
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
            t.SetPixels32(px); t.Apply();
            byte[] png = t.EncodeToPNG();
            Object.DestroyImmediate(t);
            string full = System.IO.Path.GetFullPath(path);
            bool same = System.IO.File.Exists(full) && System.IO.File.ReadAllBytes(full).AsSpan().SequenceEqual(png);
            if (!same) System.IO.File.WriteAllBytes(full, png);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            bool dirty = !same;
            void Set<T>(T cur, T want, Action<T> apply) { if (!Equals(cur, want)) { apply(want); dirty = true; } }
            Set(imp.textureType, TextureImporterType.Default, v => imp.textureType = v);
            Set(imp.sRGBTexture, true, v => imp.sRGBTexture = v);
            Set(imp.alphaSource, tile ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput, v => imp.alphaSource = v);
            Set(imp.alphaIsTransparency, !tile, v => imp.alphaIsTransparency = v);
            Set(imp.wrapMode, tile ? TextureWrapMode.Repeat : TextureWrapMode.Clamp, v => imp.wrapMode = v);
            Set(imp.mipmapEnabled, tile, v => imp.mipmapEnabled = v);
            Set(imp.anisoLevel, tile ? 4 : 1, v => imp.anisoLevel = v);
            Set(imp.filterMode, FilterMode.Bilinear, v => imp.filterMode = v);
            Set(imp.textureCompression, TextureImporterCompression.CompressedHQ, v => imp.textureCompression = v);
            if (dirty) imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// 돌 켜쌓기(성곽)·판석(성곽길) 무늬. size 픽셀 정사각 = tileM 미터, rows 켜, 돌 길이 minM~maxM(m), 줄눈 joint 픽셀.
        /// 돌 면 밝기 lo~1.0(돌마다 다름) + 가장자리 살짝 어둡게, 줄눈 = mortar. 가로·세로 모두 이어 붙어도 이음매가 없다(같은 씨앗 = 같은 그림).
        static Color32[] StonePixels(int size, int rows, float minM, float maxM, float tileM, int seed, int joint, float mortar, float lo)
        {
            var rnd = new System.Random(seed);
            var v = new float[size * size];
            float pxPerM = size / tileM;
            for (int r = 0; r < rows; r++)
            {
                int y0 = Mathf.RoundToInt(r * size / (float)rows), y1 = Mathf.RoundToInt((r + 1) * size / (float)rows);
                // 이 켜의 돌 경계(가로로 이어지게 합이 size)
                var cuts = new List<int>();
                int start = (int)(rnd.NextDouble() * size), x = 0;
                while (true)
                {
                    int len = Mathf.RoundToInt(((float)rnd.NextDouble() * (maxM - minM) + minM) * pxPerM);
                    if (x + len > size - minM * pxPerM * 0.6f) break;
                    x += len; cuts.Add(x);
                }
                if (cuts.Count == 0) cuts.Add(size / 2);
                // 돌 = 이웃한 경계 사이(고리 모양: 마지막 경계 → size → 0 → 첫 경계 가 한 돌)
                var shades = new float[cuts.Count + 1];
                for (int k = 0; k < cuts.Count; k++) shades[k] = lo + (1f - lo) * (float)rnd.NextDouble();
                shades[cuts.Count] = shades[0];
                int half = (joint + 1) / 2;
                for (int y = y0; y < y1; y++)
                    for (int px = 0; px < size; px++)
                    {
                        int xx = (px - start + size) % size;           // 켜마다 엇갈리게 밀기
                        int k = 0; while (k < cuts.Count && xx >= cuts[k]) k++;
                        int left = k == 0 ? cuts[cuts.Count - 1] - size : cuts[k - 1];
                        int right = k < cuts.Count ? cuts[k] : cuts[0] + size;
                        int dx = Mathf.Min(xx - left, right - 1 - xx), dy = Mathf.Min(y - y0, y1 - 1 - y);
                        float val = shades[k];
                        if (dx < half || dy < half) val = mortar;
                        else if (dx < half + 2 || dy < half + 2) val *= 0.92f;
                        // 결: 아주 옅은 점 무늬(결정적)
                        uint hsh = (uint)(px * 73856093) ^ (uint)(y * 19349663) ^ (uint)(seed * 83492791);
                        hsh ^= hsh >> 13; hsh *= 0x5bd1e995; hsh ^= hsh >> 15;
                        val *= 0.97f + 0.03f * (hsh & 1023) / 1023f;
                        v[y * size + px] = val;
                    }
            }
            var c = new Color32[size * size];
            for (int i = 0; i < c.Length; i++) { byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(v[i] * 255f), 0, 255); c[i] = new Color32(b, b, b, 255); }
            return c;
        }

        /// 빛 기둥: 아래는 진하고 위로 갈수록 사라지는 알파(흰색 × 재질 색)
        static Color32[] BeamPixels(int w, int h)
        {
            var c = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float t = y / (h - 1f);
                byte a = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(Mathf.Pow(1f - t, 1.6f) * 0.95f + 0.05f * (1f - t)));
                for (int x = 0; x < w; x++) c[y * w + x] = new Color32(255, 255, 255, a);
            }
            return c;
        }

        // ───────────────────────── 환경
        static void SetupEnvironment()
        {
            // Sandbox 와 같은 톤: 평평한 주변광 + 따뜻한 흰빛 해 + 부드러운 그림자, 하늘·안개는 06 문서 색(#E9E0CC)
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.60f, 0.57f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Col("fog");
            RenderSettings.fogStartDistance = 40f;
            RenderSettings.fogEndDistance = 380f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = null;
            RenderSettings.reflectionIntensity = 0f;
        }

        static void BuildLightCameraVolume(VolumeProfile profile, Transform startShot)
        {
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.973f, 0.933f);
            light.intensity = 1f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.7f;
            light.shadowBias = 0.02f;
            light.shadowNormalBias = 0.3f;
            // 해는 남남동 위(48°) — 북쪽(성곽) 을 보는 화면에서 건물 남쪽 면이 밝고 그림자가 앞쪽 비스듬히 떨어진다
            lightGo.transform.rotation = Quaternion.Euler(48f, 335f, 0f);

            var volGo = new GameObject("Global Volume (Film)");
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = profile;

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Col("sky");
            cam.fieldOfView = 45f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 900f;
            camGo.AddComponent<AudioListener>();
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            camGo.transform.SetPositionAndRotation(startShot.position, startShot.rotation);
        }

        // ───────────────────────── 지형
        static void BuildGround(Transform parent)
        {
            var src = D.Grid;
            G = new Zone1Data.HeightGrid { Name = src.Name, Origin = src.Origin, Cell = src.Cell, NX = src.NX, NZ = src.NZ, H = (float[,])src.H.Clone() };
            int lowered = 0; float maxLow = 0f;
            for (int j = 0; j < G.NZ; j++)
            for (int i = 0; i < G.NX; i++)
            {
                var v = G.Vertex(i, j);
                float lim = WalkLimit(v.x, v.z, CarveMargin) - CarveEps;
                if (v.y > lim) { maxLow = Mathf.Max(maxLow, v.y - lim); G.H[j, i] = lim; lowered++; }
            }
            Notes.Add($"지형: {G.NX}x{G.NZ} 점 중 {lowered}점을 걷는 면 아래로 낮춤(최대 {maxLow:F2}m) — 2m 격자 삼각형이 길·계단·패드 위로 솟던 곳(최대 0.5m) 정리");

            // 1) 걷는 지형: 격자 그대로(대각선 (i,j)–(i+1,j+1), Zone1Data.HeightGrid.Sample 과 같은 분할), 부드러운 법선
            var vs = new List<Vector3>(G.NX * G.NZ);
            for (int j = 0; j < G.NZ; j++) for (int i = 0; i < G.NX; i++) vs.Add(G.Vertex(i, j));
            var ts = new List<int>((G.NX - 1) * (G.NZ - 1) * 6);
            for (int j = 0; j < G.NZ - 1; j++)
            for (int i = 0; i < G.NX - 1; i++)
            {
                int a = j * G.NX + i, b = a + 1, c = a + G.NX, d = c + 1; // a=(i,j) b=(i+1,j) c=(i,j+1) d=(i+1,j+1)
                ts.Add(a); ts.Add(c); ts.Add(d);
                ts.Add(a); ts.Add(d); ts.Add(b);
            }
            var terrain = MeshObj("Terrain", parent, Reg(Smooth("Z1_Terrain", vs, ts)), Mat("ground"), LGround, true);
            TerrainCol = terrain.GetComponent<MeshCollider>();

            // 2) 구역 밖 지형(충돌 없음): 경계 둘레에서 바깥으로 내려가는 띠 — 안개와 함께 먼 곳 정리
            var per = new List<(int i, int j)>();
            for (int i = 0; i < G.NX; i++) per.Add((i, 0));
            for (int j = 1; j < G.NZ; j++) per.Add((G.NX - 1, j));
            for (int i = G.NX - 2; i >= 0; i--) per.Add((i, G.NZ - 1));
            for (int j = G.NZ - 2; j >= 1; j--) per.Add((0, j));
            int P = per.Count, R = ApronDist.Length;
            var av = new List<Vector3>(P * R);
            for (int r = 0; r < R; r++)
                foreach (var (i, j) in per)
                {
                    var dir = new Vector3(i == 0 ? -1 : i == G.NX - 1 ? 1 : 0, 0, j == 0 ? -1 : j == G.NZ - 1 ? 1 : 0).normalized;
                    av.Add(G.Vertex(i, j) + dir * ApronDist[r] + Vector3.down * ApronDrop[r]);
                }
            var at = new List<int>();
            for (int r = 0; r < R - 1; r++)
            for (int k = 0; k < P; k++)
            {
                int a = r * P + k, b = r * P + (k + 1) % P, c = (r + 1) * P + (k + 1) % P, d = (r + 1) * P + k;
                AddQuadIdx(av, at, a, b, c, d);
            }
            MeshObj("Apron(구역 밖, 보이기만)", parent, Reg(Smooth("Z1_Apron", av, at)), Mat("ground"), 0, false);
        }

        /// (x,z) 가 어떤 걷는 면의 margin 안이면 그 면 높이(가장 낮은 것), 아니면 +∞. 계단은 단 높이 절반만큼 더 낮게.
        static float WalkLimit(float x, float z, float margin)
        {
            float lim = float.PositiveInfinity;
            foreach (var r in D.Roads)
            {
                float best = float.MaxValue, by = 0f;
                for (int k = 0; k < r.Points.Length - 1; k++)
                {
                    SegClosest(r.Points[k], r.Points[k + 1], x, z, out float d, out float y, out _);
                    if (d < best) { best = d; by = y; }
                }
                if (best - r.Width / 2f <= margin) lim = Mathf.Min(lim, by);
            }
            foreach (var s in D.Stairs)
            {
                SegClosest(s.From, s.To, x, z, out float d, out float y, out _);
                float halfRiser = Mathf.Abs(s.To.y - s.From.y) / s.Steps * 0.5f;
                if (d - s.Width / 2f <= margin) lim = Mathf.Min(lim, y - halfRiser);
            }
            foreach (var p in D.Pads)
                if (PadDistance(p, x, z) <= margin) lim = Mathf.Min(lim, p.Center.y);
            return lim;
        }

        static void SegClosest(Vector3 a, Vector3 b, float x, float z, out float dist, out float y, out float t)
        {
            float dx = b.x - a.x, dz = b.z - a.z, l2 = dx * dx + dz * dz;
            t = l2 < 1e-8f ? 0f : Mathf.Clamp01(((x - a.x) * dx + (z - a.z) * dz) / l2);
            float px = a.x + dx * t, pz = a.z + dz * t;
            dist = Mathf.Sqrt((x - px) * (x - px) + (z - pz) * (z - pz));
            y = a.y + (b.y - a.y) * t;
        }

        /// 패드 가장자리까지 수평 거리(안쪽이면 음수).
        static float PadDistance(Zone1Data.Pad p, float x, float z)
        {
            float dx = x - p.Center.x, dz = z - p.Center.z;
            if (p.Shape == "circle") return Mathf.Sqrt(dx * dx + dz * dz) - p.Size.x / 2f;
            float a = p.Yaw * Mathf.Deg2Rad, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
            float u = dx * ca - dz * sa, v = dx * sa + dz * ca; // 월드 → 패드 로컬(yaw 는 위에서 볼 때 시계 방향 +)
            float ex = Mathf.Abs(u) - p.Size.x / 2f, ez = Mathf.Abs(v) - p.Size.y / 2f;
            if (ex <= 0f && ez <= 0f) return Mathf.Max(ex, ez);
            return Mathf.Sqrt(Mathf.Max(ex, 0f) * Mathf.Max(ex, 0f) + Mathf.Max(ez, 0f) * Mathf.Max(ez, 0f));
        }

        /// 구역 밖 지형 높이(띠와 같은 식, 원경 배치용 근사).
        static float ApronY(float x, float z)
        {
            float cx = Mathf.Clamp(x, D.BoundsMin.x, D.BoundsMax.x), cz = Mathf.Clamp(z, D.BoundsMin.z, D.BoundsMax.z);
            float d = new Vector2(x - cx, z - cz).magnitude, hb = G.Sample(cx, cz);
            for (int k = 0; k < ApronDist.Length - 1; k++)
                if (d <= ApronDist[k + 1])
                    return hb - Mathf.Lerp(ApronDrop[k], ApronDrop[k + 1], (d - ApronDist[k]) / (ApronDist[k + 1] - ApronDist[k]));
            return hb - ApronDrop[ApronDist.Length - 1];
        }

        static void BuildPads(Transform parent)
        {
            foreach (var p in D.Pads)
            {
                var mat = Mat("pad_" + p.Surface);
                float top = p.Center.y + 0.01f;
                if (p.Shape == "circle")
                {
                    var m = Reg(Prism("Z1_Pad_" + Safe(p.Name), Circle(new Vector3(p.Center.x, 0, p.Center.z), p.Size.x / 2f, 40), top - Skirt, top));
                    MeshObj(p.Name, parent, m, mat, LGround, true);
                }
                else
                {
                    // 상자 메시(월드 미터 UV — 돌 계단참 판석 무늬) + 같은 모양 충돌
                    var mb = new MB();
                    mb.Box(new Vector3(p.Center.x, top - Skirt / 2f, p.Center.z), Quaternion.Euler(0f, p.Yaw, 0f), new Vector3(p.Size.x, Skirt, p.Size.y));
                    MeshObj(p.Name, parent, Reg(mb.ToMesh("Z1_Pad_" + Safe(p.Name))), mat, LGround, true);
                }
            }
        }

        // ───────────────────────── 길·계단·성곽·경계
        static void BuildRoads(Transform parent)
        {
            for (int k = 0; k < D.Roads.Count; k++)
            {
                var r = D.Roads[k];
                // 겹치는 자리에서 깜빡이지 않게 종류별로 1~2cm 차등(걷기에는 영향 없음)
                float lift = (r.Kind switch { "road" => 0f, "sidewalk" => 0.005f, "driveway" => 0.02f, _ => 0.015f }) + k * 0.002f;
                var m = Reg(Extrude("Z1_Road_" + k, r.Points, r.Width, Skirt, lift));
                MeshObj(r.Name, parent, m, Mat(r.Kind), LGround, true);
            }
        }

        static void BuildStairs(Transform parent)
        {
            for (int k = 0; k < D.Stairs.Count; k++)
            {
                var s = D.Stairs[k];
                Vector3 dh = Flat(s.To - s.From);
                float L = dh.magnitude;
                if (L < 0.01f) { Notes.Add($"계단 '{s.Name}' 길이 0 — 건너뜀"); continue; }
                Vector3 t = dh / L;
                float rise = s.To.y - s.From.y, riser = Mathf.Abs(rise) / s.Steps, tread = L / s.Steps;
                var rot = Quaternion.LookRotation(t, Vector3.up);
                // 보이는 단: 단 윗면이 경사선 위아래로 반 단씩(경사판 충돌과 발 높이 차 ±반 단). 충돌 없음.
                var mb = new MB();
                for (int i = 0; i < s.Steps; i++)
                {
                    float top = s.From.y + rise * (i + 0.5f) / s.Steps, bottom = top - (riser + 2.0f);
                    var c = s.From + t * ((i + 0.5f) * tread);
                    c.y = (top + bottom) / 2f;
                    mb.Box(c, rot, new Vector3(s.Width, top - bottom, tread));
                }
                var go = MeshObj(s.Name, parent, Reg(mb.ToMesh("Z1_Stair_" + k)), Mat("stairs"), 0, false);

                // 보이지 않는 경사판(Ground): 윗면이 아래 끝 ~ 위 끝을 잇는 선. 07 문서 3-6 ①
                var ramp = new GameObject("경사판 충돌") { layer = LGround };
                ramp.transform.SetParent(go.transform, false);
                Vector3 d3 = s.To - s.From;
                ramp.transform.SetPositionAndRotation((s.From + s.To) / 2f, Quaternion.LookRotation(d3.normalized, Vector3.up));
                var bc = ramp.AddComponent<BoxCollider>();
                bc.size = new Vector3(s.Width, 0.3f, d3.magnitude + 0.2f);
                bc.center = new Vector3(0f, -0.15f, 0f);
                Static(ramp);
                Notes.Add($"계단 '{s.Name}': {s.Steps}단, 단 높이 {riser:F3}m, 디딤 {tread:F2}m, 기울기 {Mathf.Atan2(Mathf.Abs(rise), L) * Mathf.Rad2Deg:F1}°, 폭 {s.Width}m");
            }
        }

        static void BuildWalls(Transform parent)
        {
            for (int k = 0; k < D.Walls.Count; k++)
            {
                var w = D.Walls[k];
                // 06 문서 14장·6장: 체성(아래 = 기초 − 1.5, 위 = 기초 + 높이, 돌 줄눈 텍스처) → 미석 띠 → 바깥 가장자리 여장(타·타구·총안) → 옥개석.
                // 충돌은 체성 그대로 위로 10m 더(넘을 수 없게). 여장·미석·옥개석은 보이기만(충돌 상자 안에 들어 있음).
                var vis = MeshObj(w.Name, parent, Reg(Extrude("Z1_Wall_" + k, w.Points, w.Thickness, 1.5f, w.Height)), Mat("wall"), LWall, false);
                var col = new GameObject("충돌(위로 +10m)") { layer = LWall };
                col.transform.SetParent(vis.transform, false);
                col.AddComponent<MeshCollider>().sharedMesh = Reg(Extrude("Z1_WallCol_" + k, w.Points, w.Thickness, 1.5f, w.Height + 10f));
                Static(col);

                var top = w.Points.Select(p => p + Vector3.up * w.Height).ToArray();
                MeshObj("미석(눈썹돌 띠)", vis.transform, Reg(Extrude("Z1_WallBrow_" + k, top, w.Thickness + 2f * w.BrowOver, 0.02f, w.BrowH)), Mat("wallcap"), LWall, false);

                var stone = new MB(); var caps = new MB();
                int merlons = 0;
                for (int i = 0; i < w.Points.Length - 1; i++)
                {
                    Vector3 a = w.Points[i], b = w.Points[i + 1];
                    var d = Flat(b - a); float L = d.magnitude;
                    if (L < 0.5f) continue;
                    var t = d / L;
                    var outN = new Vector3(-t.z, 0f, t.x);               // 진행 방향 왼쪽 = 성 밖(북)
                    var rot = Quaternion.LookRotation(t, Vector3.up);     // 상자 로컬 z = 성벽 방향
                    int n = Mathf.Max(1, Mathf.RoundToInt(L / (w.Merlon + w.Crenel)));
                    float pitch = L / n, len = Mathf.Max(0.6f, pitch - w.Crenel);
                    float slope = Mathf.Abs(b.y - a.y) / L;
                    for (int m = 0; m < n; m++)
                    {
                        float s = (m + 0.5f) * pitch;
                        float yTop = Mathf.Lerp(a.y, b.y, s / L) + w.Height + w.BrowH;   // 여장이 앉는 높이(타마다 수평 — 비탈에선 계단식)
                        float sink = 0.25f + slope * len / 2f;
                        var c = a + t * s; c.y = 0f;
                        var center = c + outN * (w.Thickness / 2f - w.ParapetT / 2f);
                        float h = w.ParapetH + sink;
                        stone.Box(center + Vector3.up * (yTop - sink + h / 2f), rot, new Vector3(w.ParapetT, h, len));
                        caps.Box(center + Vector3.up * (yTop + w.ParapetH + w.CapH / 2f), rot,
                                 new Vector3(w.ParapetT + 2f * w.CapOver, w.CapH, len + 2f * w.CapOver));
                        // 총안: 안쪽 면에 작은 어두운 구멍(그레이박스는 얇은 판)
                        var inner = center - outN * (w.ParapetT / 2f + 0.01f);
                        caps.Box(inner + Vector3.up * (yTop + 0.45f + w.Embrasure.y / 2f), rot, new Vector3(0.03f, w.Embrasure.y, w.Embrasure.x));
                        merlons++;
                    }
                }
                MeshObj("여장(타·타구)", vis.transform, Reg(stone.ToMesh("Z1_WallParapet_" + k)), Mat("wall"), LWall, false);
                MeshObj("옥개석·총안", vis.transform, Reg(caps.ToMesh("Z1_WallCap_" + k)), Mat("wallcap"), LWall, false);
                Notes.Add($"성곽 '{w.Name}': 체성 {w.Height}m + 미석 {w.BrowH}m + 여장 {w.ParapetH}m(타 {merlons}개, 타구 {w.Crenel}m) + 옥개석 {w.CapH}m · 돌 줄눈 텍스처 {StoneTileW}×{StoneTileH}m");

                // 이름표 '한양도성' — 두 군데: ① 꼭대기 계단참(체크포인트 6)에서 성곽이 처음 드러나는 쪽(Shot_Reveal 이 보는 곳)
                // ② 성곽길 암문 앞(체크포인트 8). 각 기준점에서 가장 가까운 성벽 점의 여장 위.
                var anchors = new List<(string tag, Vector3 at)>();
                if (D.Route.Count >= 6) anchors.Add(("계단참 쪽", D.Route[5].Pos + new Vector3(-15f, 0f, 17f)));
                if (D.Route.Count >= 8) anchors.Add(("암문 앞", D.Route[7].Pos));
                foreach (var (tag, at) in anchors)
                {
                    float bd = float.MaxValue; Vector3 p = default, tt = Vector3.right;
                    for (int i = 0; i < w.Points.Length - 1; i++)
                    {
                        SegClosest(w.Points[i], w.Points[i + 1], at.x, at.z, out float d, out _, out float u);
                        if (d >= bd) continue;
                        bd = d; p = Vector3.Lerp(w.Points[i], w.Points[i + 1], u); tt = Flat(w.Points[i + 1] - w.Points[i]).normalized;
                    }
                    var inward = -new Vector3(-tt.z, 0f, tt.x);
                    var holder = new GameObject($"이름표 자리 ({tag})");
                    holder.transform.SetParent(vis.transform, false);
                    // 성벽 충돌 상자(위로 +10m) 밖, 안쪽 면에서 0.8m 앞 — 안 그러면 런타임 가림 검사에서 늘 '가림'
                    holder.transform.position = p + inward * (w.Thickness / 2f + 0.8f) + Vector3.up * (w.Height + w.BrowH + w.ParapetH + w.CapH + 1.0f);
                    Static(holder);
                    Label(holder.transform, "한양도성", holder.transform.position, LabelSize * 1.15f, Vector3.Cross(Vector3.up, tt));
                }
            }
        }

        static void BuildBounds(Transform parent)
        {
            Vector3 mn = D.BoundsMin, mx = D.BoundsMax;
            float y0 = mn.y - 5f, y1 = mx.y + 5f, cy = (y0 + y1) / 2f, h = y1 - y0;
            float cx = (mn.x + mx.x) / 2f, cz = (mn.z + mx.z) / 2f, w = mx.x - mn.x + 2f, d = mx.z - mn.z + 2f;
            void Side(string n, Vector3 c, Vector3 s)
            {
                var go = new GameObject(n) { layer = LPlayerOnly };
                go.transform.SetParent(parent, false);
                go.transform.position = c;
                go.AddComponent<BoxCollider>().size = s;
                Static(go);
            }
            Side("경계벽 서", new Vector3(mn.x - 0.5f, cy, cz), new Vector3(1f, h, d));
            Side("경계벽 동", new Vector3(mx.x + 0.5f, cy, cz), new Vector3(1f, h, d));
            Side("경계벽 남", new Vector3(cx, cy, mn.z - 0.5f), new Vector3(w, h, 1f));
            Side("경계벽 북", new Vector3(cx, cy, mx.z + 0.5f), new Vector3(w, h, 1f));
        }

        // ───────────────────────── 블록
        static int BlockLayer(string kind) => kind switch
        {
            "railing" or "prop" or "tree" => LPlayerOnly,
            "bush" => 0,
            _ => LWall, // shop house hanok campus pavilion gate fence retaining barrier
        };

        static void BuildBlocks(Transform parent)
        {
            var groups = new Dictionary<string, Transform>();
            foreach (var b in D.Blocks)
            {
                if (!groups.TryGetValue(b.Kind, out var gp)) groups[b.Kind] = gp = Group(parent, b.Kind);
                if (b.Kind == "tree") { BuildTree(b, gp); continue; }
                var go = Cube(b.Name, gp, b.Center, b.Size, b.Yaw, Mat(b.Kind), BlockLayer(b.Kind), b.Kind != "bush");
                GroundBlock(go.transform, b);
            }
        }

        /// 상자 바닥이 발밑 땅보다 위면 아래로 늘인다(윗면 유지). 바닥이 땅 위 0.15m 이상이면 '떠 있음'.
        static void GroundBlock(Transform tr, Zone1Data.Block b)
        {
            float gmin = float.PositiveInfinity, gmax = float.NegativeInfinity;
            var rot = Quaternion.Euler(0f, b.Yaw, 0f);
            foreach (float u in new[] { -0.45f, 0f, 0.45f })
            foreach (float v in new[] { -0.45f, 0f, 0.45f })
            {
                var p = b.Center + rot * new Vector3(u * b.Size.x, 0f, v * b.Size.z);
                if (GroundY(p, out float gy, out _)) { gmin = Mathf.Min(gmin, gy); gmax = Mathf.Max(gmax, gy); }
            }
            if (float.IsInfinity(gmin)) return;
            float bottom = b.Center.y - b.Size.y / 2f, top = b.Center.y + b.Size.y / 2f;
            if (bottom > gmin - 0.15f)
            {
                float nb = gmin - 0.4f;
                MaxBlockFix = Mathf.Max(MaxBlockFix, bottom - nb); FixedBlocks++;
                if (bottom > gmin + 0.05f) Notes.Add($"떠 있던 블록 보정: '{b.Name}'({b.Kind}) 바닥 {bottom:F2} > 땅 {gmin:F2} → {nb:F2}");
                bottom = nb;
                var s = tr.localScale; s.y = top - bottom; tr.localScale = s;
                var c = tr.position; c.y = (top + bottom) / 2f; tr.position = c;
            }
            if (top < gmax + 0.2f) { BuriedBlocks++; Notes.Add($"묻힌 블록: '{b.Name}' 윗면 {top:F2} ≤ 땅 {gmax:F2}"); }
        }

        static void BuildTree(Zone1Data.Block b, Transform parent)
        {
            float H = b.Size.y, bottom = b.Center.y - H / 2f;
            var basePos = new Vector3(b.Center.x, bottom, b.Center.z);
            if (GroundY(basePos, out float gy, out _) && bottom > gy - 0.2f)
            {
                float nb = gy - 0.4f; MaxBlockFix = Mathf.Max(MaxBlockFix, bottom - nb); FixedBlocks++;
                H += bottom - nb; bottom = nb;
            }
            var go = new GameObject(b.Name) { layer = LPlayerOnly };
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(new Vector3(b.Center.x, bottom, b.Center.z), Quaternion.Euler(0f, b.Yaw, 0f));
            // 줄기 0.5m(콜라이더 지름 0.6m) + 잎 덩어리 상자
            var trunk = Cube("줄기", go.transform, Vector3.zero, new Vector3(0.5f, H * 0.45f, 0.5f), 0f, Mat("tree"), LPlayerOnly, false);
            trunk.transform.localPosition = new Vector3(0f, H * 0.225f, 0f); trunk.transform.localRotation = Quaternion.identity;
            var crown = Cube("잎", go.transform, Vector3.zero, new Vector3(b.Size.x, H * 0.62f, b.Size.z), 0f, Mat("tree"), LPlayerOnly, false);
            crown.transform.localPosition = new Vector3(0f, H * 0.69f, 0f); crown.transform.localRotation = Quaternion.identity;
            var cc = go.AddComponent<CapsuleCollider>();
            cc.radius = 0.3f; cc.height = H; cc.center = new Vector3(0f, H / 2f, 0f);
            Static(go);
        }

        // ───────────────────────── 랜드마크
        static void BuildLandmarks(Transform parent)
        {
            var groups = new Dictionary<string, Transform>();
            var poles = new List<(string group, int idx, Vector3 top)>();
            var poleRx = new Regex(@"전봇대\s*(\d+)\s*\((.+)\)");
            foreach (var lm in D.Landmarks)
            {
                if (!groups.TryGetValue(lm.Kind, out var gp)) groups[lm.Kind] = gp = Group(parent, lm.Kind);
                var root = new GameObject(lm.Name);
                root.transform.SetParent(gp, false);
                root.transform.position = lm.Pos;
                Static(root);
                bool grounded = GroundY(lm.Pos, out float gy, out _);
                if (!grounded) gy = lm.Pos.y;
                Vector3 baseP = new Vector3(lm.Pos.x, gy, lm.Pos.z);
                float gap = lm.Pos.y - gy;
                bool poleLike = lm.Kind is "pole" or "lamp" or "board" or "bus_stop" or "viewpoint" || (lm.Kind == "sign" && gap <= 1f);
                if (poleLike && Mathf.Abs(gap) > 0.25f)
                {
                    FixedMarks++; MaxMarkFix = Mathf.Max(MaxMarkFix, Mathf.Abs(gap));
                    Notes.Add($"랜드마크 바닥 보정: '{lm.Name}' 기준 y {lm.Pos.y:F2} → 땅 {gy:F2}");
                }
                float h = lm.Height > 0f ? lm.Height : DefaultHeight(lm.Kind);
                Vector3 toRoad = DirToRoad(lm.Pos);
                float labelY = gy + h + 0.6f;
                Vector3 labelXZ = lm.Pos;   // 이름표 수평 자리(출입문은 벽에서 바깥으로 띄움 — 글이 벽에 반쯤 묻히지 않게)

                switch (lm.Kind)
                {
                    case "pole":
                        Cyl("기둥", root.transform, baseP + Vector3.down * 0.3f, 0.3f, h + 0.3f, Mat("pole"), LPlayerOnly, true);
                        var m = poleRx.Match(lm.Name);
                        if (m.Success) poles.Add((m.Groups[2].Value, int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), baseP + Vector3.up * h));
                        break;
                    case "lamp":
                        Cyl("기둥", root.transform, baseP + Vector3.down * 0.3f, 0.18f, h + 0.3f, Mat("lamp"), LPlayerOnly, true);
                        Cube("등", root.transform, baseP + Vector3.up * (h - 0.05f) + toRoad * 0.35f, new Vector3(0.35f, 0.14f, 0.8f), Yaw(toRoad), Mat("lamp"), 0, false);
                        break;
                    case "board":
                        Cyl("기둥", root.transform, baseP + Vector3.down * 0.3f, 0.3f, h + 0.3f, Mat("pole"), LPlayerOnly, true);
                        Cube("전단 보드", root.transform, baseP + Vector3.up * 1.65f + toRoad * 0.18f, new Vector3(0.8f, 1.1f, 0.05f), Yaw(toRoad), Mat("board"), 0, false);
                        labelY = gy + 3f;
                        break;
                    case "bus_stop":
                        Cyl("기둥", root.transform, baseP + Vector3.down * 0.3f, 0.1f, h + 0.3f, Mat("bus_stop"), LPlayerOnly, true);
                        Cube("표지판", root.transform, baseP + Vector3.up * (h - 0.35f), new Vector3(0.9f, 0.6f, 0.06f), Yaw(toRoad), Mat("bus_stop"), 0, false);
                        break;
                    case "sign":
                        if (gap > 1f) MountedSign(lm, root.transform, h);
                        else
                        {
                            Cyl("기둥", root.transform, baseP + Vector3.down * 0.3f, 0.08f, h + 0.3f, Mat("sign"), LPlayerOnly, true);
                            Cube("표지판", root.transform, baseP + Vector3.up * (h - 0.4f), new Vector3(1.2f, 0.8f, 0.06f), Yaw(toRoad), Mat("sign"), 0, false);
                        }
                        labelY = Mathf.Max(gy, lm.Pos.y) + h + 0.6f;
                        break;
                    case "gate": labelY = gy + 4.6f; break; // 후문 기둥 2개·차단 펜스는 blocks 에 있다(상인방은 데이터에 없어 만들지 않음)
                    case "wall_gate": WallGate(lm, root.transform); labelY = lm.Pos.y + 4f; break;
                    case "door": labelXZ = Door(lm, root.transform, gy); labelY = gy + 2.75f; break;
                    case "viewpoint":
                        var vp = baseP + new Vector3(-2.2f, 0f, 1.8f);
                        if (GroundY(vp, out float vy, out _)) vp.y = vy;
                        Cyl("전망 표지 기둥", root.transform, vp + Vector3.down * 0.3f, 0.12f, 4.8f, Mat("viewpoint"), LPlayerOnly, true);
                        Cube("깃발", root.transform, vp + new Vector3(0.45f, 4.2f, 0f), new Vector3(0.9f, 0.55f, 0.04f), 0f, Mat("viewpoint"), 0, false);
                        labelY = vp.y + 5.2f;
                        break;
                    case "arena":
                        MeshObj("전투 무대 표시(디버그)", root.transform, Reg(Conform("Z1_Arena_" + Safe(lm.Name), lm.Pos, lm.Radius - 0.25f, lm.Radius, 0.07f)), Fx("arena_fx", "arena", 0.55f), 0, false, false);
                        labelY = gy + 2f;
                        break;
                    case "npc_spot":
                        Cube("자리 표시", root.transform, new Vector3(lm.Pos.x, Mathf.Max(lm.Pos.y, gy) + 0.15f, lm.Pos.z), new Vector3(0.3f, 0.3f, 0.5f), 0f, Mat("npc_spot"), 0, false);
                        if (lm.Pos.y - gy > 0.4f) Notes.Add($"NPC 자리 '{lm.Name}': 땅보다 {lm.Pos.y - gy:F2}m 위(담 위 자리로 보고 그대로 둠)");
                        labelY = Mathf.Max(lm.Pos.y, gy) + 1.2f;
                        break;
                    case "prop":
                        // 배드민턴 네트(새벽만): 기본은 꺼 둔다 — 밤엔 걷혀서 공터 = 야차 링
                        Cyl("기둥 서", root.transform, baseP + new Vector3(-3f, 0f, 0f), 0.06f, 1.55f, Mat("prop"), 0, false);
                        Cyl("기둥 동", root.transform, baseP + new Vector3(3f, 0f, 0f), 0.06f, 1.55f, Mat("prop"), 0, false);
                        Cube("그물", root.transform, baseP + new Vector3(0f, 1.2f, 0f), new Vector3(6f, 0.7f, 0.02f), 0f, Mat("prop"), 0, false);
                        root.SetActive(false);
                        break;
                    case "vista": Vista(lm, root.transform); continue;
                    default: Notes.Add($"알 수 없는 랜드마크 종류 '{lm.Kind}': '{lm.Name}' — 자리만 둠"); break;
                }
                if (Labelled(lm, gap))
                    Label(root.transform, LabelText(lm.Name), new Vector3(labelXZ.x, labelY, labelXZ.z), LabelSize, toRoad);
            }

            // 전선: 같은 줄 전봇대 꼭대기끼리 두 가닥(06 문서 7장 '전선 = 로우앵글 하늘')
            var wires = new MB();
            foreach (var grp in poles.GroupBy(p => p.group))
            {
                var list = grp.OrderBy(p => p.idx).ToList();
                for (int i = 0; i < list.Count - 1; i++)
                {
                    Vector3 a = list[i].top, b = list[i + 1].top, side = Vector3.Cross(Vector3.up, Flat(b - a).normalized) * 0.35f;
                    foreach (var off in new[] { new Vector3(0, -0.5f, 0) + side, new Vector3(0, -0.5f, 0) - side, new Vector3(0, -1.1f, 0) })
                        wires.Line(a + off, b + off, 0.035f);
                }
            }
            if (wires.V.Count > 0) MeshObj("전선", parent, Reg(wires.ToMesh("Z1_Wires")), Mat("pole"), 0, false, false);
        }

        static float DefaultHeight(string kind) => kind switch
        {
            "pole" => 9f, "lamp" => 4f, "sign" => 2f, "bus_stop" => 2.6f, "board" => 8f, _ => 2f,
        };

        static void MountedSign(Zone1Data.Landmark lm, Transform parent, float h)
        {
            var center = lm.Pos + Vector3.up * (h / 2f);
            if (NearestWallFace(center, 2.5f, out var q, out var n))
            {
                // 돌출 간판: 판이 건물 면에서 바깥(n)으로 튀어나오게, 길 방향 양쪽에서 읽힌다
                var t = Vector3.Cross(Vector3.up, n);
                var c = new Vector3(q.x, center.y, q.z) + n * 0.62f;
                var go = Cube("돌출 간판", parent, c, new Vector3(1.2f, h, 0.1f), 0f, Mat("sign"), 0, false);
                go.transform.rotation = Quaternion.LookRotation(t, Vector3.up);
                Cube("받침", parent, new Vector3(q.x, center.y + h / 2f - 0.05f, q.z) + n * 0.1f, new Vector3(0.2f, 0.08f, 0.08f), Yaw(n), Mat("pole"), 0, false)
                    .transform.rotation = Quaternion.LookRotation(t, Vector3.up);
            }
            else
            {
                Notes.Add($"간판 '{lm.Name}': 붙일 건물 면을 2.5m 안에서 못 찾아 기둥을 세움");
                GroundY(lm.Pos, out float gy, out _);
                Cyl("기둥", parent, new Vector3(lm.Pos.x, gy - 0.3f, lm.Pos.z), 0.1f, lm.Pos.y - gy + h + 0.3f, Mat("pole"), LPlayerOnly, true);
                Cube("간판", parent, center, new Vector3(1.2f, h, 0.1f), 0f, Mat("sign"), 0, false);
            }
        }

        static void WallGate(Zone1Data.Landmark lm, Transform parent)
        {
            Zone1Data.Wall best = null; Vector3 q = default, t = default; float bd = float.MaxValue;
            foreach (var w in D.Walls)
                for (int k = 0; k < w.Points.Length - 1; k++)
                {
                    SegClosest(w.Points[k], w.Points[k + 1], lm.Pos.x, lm.Pos.z, out float d, out float y, out float tt);
                    if (d < bd)
                    {
                        bd = d; best = w;
                        q = Vector3.Lerp(w.Points[k], w.Points[k + 1], tt);
                        t = Flat(w.Points[k + 1] - w.Points[k]).normalized;
                    }
                }
            if (best == null) return;
            var n = new Vector3(-t.z, 0f, t.x);
            var inside = Flat((D.BoundsMin + D.BoundsMax) / 2f - q);
            if (Vector3.Dot(n, inside) < 0f) n = -n;
            var c = new Vector3(q.x, lm.Pos.y + 1.6f, q.z) + n * (best.Thickness / 2f + 0.04f);
            var go = Cube("암문 문짝", parent, c, new Vector3(2.4f, 3.2f, 0.08f), 0f, Mat("wall_gate"), 0, false);
            go.transform.rotation = Quaternion.LookRotation(n, Vector3.up);
        }

        /// 문짝을 가장 가까운 건물 면에 붙이고, 이름표 자리(문 앞 0.9m)를 돌려준다.
        static Vector3 Door(Zone1Data.Landmark lm, Transform parent, float gy)
        {
            var probe = new Vector3(lm.Pos.x, gy + 1.0f, lm.Pos.z);
            if (!NearestWallFace(probe, 2.5f, out var q, out var n))
            {
                Notes.Add($"출입문 '{lm.Name}': 붙일 건물 면을 2.5m 안에서 못 찾아 바닥 표시만");
                Cube("문 자리", parent, new Vector3(lm.Pos.x, gy + 0.02f, lm.Pos.z), new Vector3(1f, 0.04f, 1f), 0f, Mat("door"), 0, false);
                return lm.Pos;
            }
            var c = new Vector3(q.x, gy + 1.05f, q.z) + n * 0.04f;
            var go = Cube("문짝", parent, c, new Vector3(1.0f, 2.1f, 0.08f), 0f, Mat("door"), 0, false);
            go.transform.rotation = Quaternion.LookRotation(n, Vector3.up);
            float off = Flat(q - lm.Pos).magnitude;
            if (off > 0.6f) Notes.Add($"출입문 '{lm.Name}': 데이터 위치에서 {off:F2}m 떨어진 건물 면에 붙임");
            return q + n * 0.9f;
        }

        static void Vista(Zone1Data.Landmark lm, Transform parent)
        {
            // 원경(충돌 없음, 그림자 없음): 바깥 지형 위에 단순한 덩어리. 안개에 반쯤 묻힌다.
            var p = lm.Pos;
            var mb = new MB();
            // 산 덩어리는 반지름을 '구역 경계까지 거리 − 25m' 이하로 줄여 구역 안으로 들어오지 않게 한다
            void Peak(Vector3 at, float maxR, float top, int sides, float skew, float sink) =>
                mb.Mountain(new Vector3(at.x, ApronY(at.x, at.z) - sink, at.z), Mathf.Min(maxR, OutsideDist(at) - 25f), top, sides, skew);
            if (lm.Name.Contains("말바위"))
            {
                // 말바위: 성곽을 따라 서북쪽으로 오르는 바위 능선
                Peak(p, 40f, p.y, 9, 0.5f, 4f);
                Peak(p + new Vector3(-30f, 0f, 22f), 45f, p.y + 8f, 9, 0.3f, 4f);
            }
            else if (lm.Name.Contains("북악산"))
            {
                // 북악산: 북서쪽 수평선의 넓은 능선(봉우리 셋)
                Peak(p, 120f, p.y, 11, 0.2f, 10f);
                Peak(p + new Vector3(80f, 0f, 70f), 100f, p.y - 22f, 10, 0.6f, 10f);
                Peak(p + new Vector3(-90f, 0f, -40f), 100f, p.y - 30f, 10, 0.4f, 10f);
            }
            else
            {
                // 지붕 무리: 성북동(북쪽 저지대) · 남쪽 도심. 같은 결과가 나오게 고정 씨앗
                bool city = lm.Name.Contains("도심");
                var rnd = new System.Random(city ? 7 : 3);
                int nx = city ? 7 : 6, nz = city ? 4 : 3;
                float step = city ? 22f : 15f;
                for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    float x = p.x + (i - (nx - 1) / 2f) * step + (float)(rnd.NextDouble() - 0.5) * 5f;
                    float z = p.z + (j - (nz - 1) / 2f) * step + (float)(rnd.NextDouble() - 0.5) * 5f;
                    float gy = ApronY(x, z), hh = city ? 14f + (float)rnd.NextDouble() * 26f : 6f + (float)rnd.NextDouble() * 5f;
                    var size = new Vector3(city ? 14f : 10f, hh + 3f, city ? 14f : 8f);
                    mb.Box(new Vector3(x, gy - 3f + size.y / 2f, z), Quaternion.Euler(0f, (float)rnd.NextDouble() * 30f - 15f, 0f), size);
                }
            }
            MeshObj("원경 덩어리", parent, Reg(mb.ToMesh("Z1_Vista_" + Safe(lm.Name))), Mat("vista"), 0, false, false);
        }

        /// 구역 경계(bounds) 사각형까지 수평 거리. 안이면 0.
        static float OutsideDist(Vector3 p)
        {
            float dx = Mathf.Max(D.BoundsMin.x - p.x, 0f, p.x - D.BoundsMax.x), dz = Mathf.Max(D.BoundsMin.z - p.z, 0f, p.z - D.BoundsMax.z);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// 한글 이름표(TMP 3D, 먹색 글자 + 종이색 외곽선). toViewer = 처음에 글자가 향할 쪽(보는 사람 쪽).
        /// 실행 중에는 NameTags(런타임)가 카메라를 보게 돌리고, 멀면 숨기고, 거리만큼 키운다. 그림자 없음.
        static GameObject Label(Transform parent, string text, Vector3 pos, float size, Vector3 toViewer = default)
        {
            if (LabelFont == null || string.IsNullOrWhiteSpace(text)) return null;
            var go = new GameObject("이름표");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var f = Flat(toViewer).sqrMagnitude > 1e-4f ? Flat(toViewer).normalized : Vector3.back;
            go.transform.rotation = Quaternion.LookRotation(-f, Vector3.up);   // TMP 는 로컬 -Z 쪽에서 읽힌다
            var t = go.AddComponent<TextMeshPro>();
            t.font = LabelFont;
            t.fontSharedMaterial = LabelMat;
            t.text = text;
            t.fontSize = size;
            t.alignment = TextAlignmentOptions.Bottom;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.color = Ink;
            t.rectTransform.sizeDelta = new Vector2(24f, 3f);
            t.rectTransform.pivot = new Vector2(0.5f, 0f);
            var mr = go.GetComponent<MeshRenderer>();
            mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
            Labels++;
            return go;
        }

        /// 데이터 이름 → 보여 줄 이름: 앞 번호("5. ") 떼고, 괄호 속은 작은 둘째 줄로, 출입문·간판·표지·안내판 꼬리말 정리.
        public static string LabelText(string name, bool small = true)
        {
            string s = Regex.Replace(name ?? "", @"^\d+\.\s*", "");
            s = Regex.Replace(s, @"\s*(출입문|출입구|표지|간판|안내판)(?=\(|$)", "");
            var m = Regex.Match(s, @"^(.*?)\((.+)\)\s*$");
            if (!m.Success) return s.Trim();
            return small ? $"{m.Groups[1].Value.Trim()}\n<size=62%>{m.Groups[2].Value.Trim()}</size>" : m.Groups[1].Value.Trim();
        }

        /// 이름표를 붙이는 랜드마크 종류(전봇대·가로등·원경·소품·NPC 자리·전투 무대 디버그는 안 붙임). 벽에 붙은 간판은 같은 가게 출입문 이름표가 대신.
        static bool Labelled(Zone1Data.Landmark lm, float gap) =>
            lm.Kind is "gate" or "bus_stop" or "board" or "wall_gate" or "viewpoint" or "door" || (lm.Kind == "sign" && gap <= 1f);

        // ───────────────────────── 체크포인트·시작 지점·촬영 자리
        /// 체크포인트(06 문서 8장·9장): 걷는 면에 붙인 원판·테두리 + 3m 기둥·깃발 + 30m 빛 기둥 + 이름표 + 트리거.
        /// 색 = 길잡이 주황(#E2582C 계열, 다른 소품과 겹치지 않음). 순서대로 켜는 일은 런타임 RouteGuide(RouteSetup 이 붙임):
        /// 지금 목표만 기둥·빛 기둥·깃발·이름표, 지난 것은 원판만 옅게, 아직 안 온 것은 숨김. 저장 상태 = 시작 때(1번 지남 · 2번 목표).
        public const int RouteStartIndex = 1;
        static void BuildRoute(Transform parent)
        {
            float worst = 0f;
            var fill = Fx("cp_fill", "checkpoint", 0.30f);
            var done = Fx("cp_done", "checkpoint", 0.12f);
            var edge = Fx("cp_edge", "checkpoint", 0.85f);
            var beam = Fx("cp_beam", "checkpoint", 0.55f, BeamTex);
            var beamMesh = Reg(BeamMesh("Z1_CP_Beam", 0.45f, 30f, 16));
            for (int i = 0; i < D.Route.Count; i++)
            {
                var cp = D.Route[i];
                var root = new GameObject($"CP{i + 1:00} {cp.Name}");
                root.transform.SetParent(parent, false);
                root.transform.position = cp.Pos;
                if (GroundY(cp.Pos, out float gy, out var hit))
                {
                    worst = Mathf.Max(worst, Mathf.Abs(gy - cp.Pos.y));
                    if (Mathf.Abs(gy - cp.Pos.y) > 0.3f) Notes.Add($"체크포인트 {i + 1}: 데이터 y {cp.Pos.y:F2} vs 발밑 {gy:F2}({hit.name})");
                }
                else { gy = cp.Pos.y; Notes.Add($"체크포인트 {i + 1}: 발밑에 걷는 면이 없음"); }

                var disc = MeshObj(CpDisc, root.transform, Reg(Conform($"Z1_CP{i + 1:00}", cp.Pos, 0f, cp.Radius - 0.2f, 0.05f)), fill, 0, false, false);
                MeshObj(CpRing, root.transform, Reg(Conform($"Z1_CP{i + 1:00}_Edge", cp.Pos, cp.Radius - 0.2f, cp.Radius, 0.06f)), edge, 0, false, false);
                var pillarBase = new Vector3(cp.Pos.x, gy, cp.Pos.z);
                // 시작 지점과 겹치는 체크포인트(1번)는 기둥·빛 기둥을 세우지 않는다 — 시우가 기둥 안에서 시작하게 된다(시작하자마자 지난 것으로 침)
                if (Flat(cp.Pos - D.SpawnPos).magnitude > 1f)
                {
                    Cyl(CpPillar, root.transform, pillarBase, 0.16f, 3f, Mat("checkpoint"), 0, false);
                    Cube(CpFlag, root.transform, pillarBase + new Vector3(0.33f, 2.72f, 0f), new Vector3(0.62f, 0.42f, 0.04f), 0f, Mat("checkpoint"), 0, false);
                    var b = MeshObj(CpBeam, root.transform, beamMesh, beam, 0, false, false);
                    b.transform.position = pillarBase;   // 빛 기둥 메시는 원점 기준(체크포인트끼리 같은 메시)
                    b.GetComponent<MeshRenderer>().receiveShadows = false;
                }
                Label(root.transform, LabelText(cp.Name), pillarBase + Vector3.up * 3.35f, CheckpointLabelSize, DirToRoad(cp.Pos));

                // 트리거(반경 × 높이 3m 원기둥) — RouteGuide 는 거리로 판정하고, 트리거는 나중 이벤트(대사·컷신)용 자리
                var trig = new GameObject(CpTrigger) { layer = IgnoreRaycastLayer };
                trig.transform.SetParent(root.transform, false);
                trig.transform.position = pillarBase;
                var mc = trig.AddComponent<MeshCollider>();
                mc.sharedMesh = Reg(Prism($"Z1_CP{i + 1:00}_Trig", Circle(Vector3.zero, cp.Radius, 24), 0f, 3f));
                mc.convex = true;
                mc.isTrigger = true;

                // 저장 상태 = 게임 시작 때 상태
                int state = i < RouteStartIndex ? 2 : i == RouteStartIndex ? 1 : 0;
                foreach (Transform c in root.transform)
                {
                    GameObjectUtility.SetStaticEditorFlags(c.gameObject, 0);   // 켜고 끄고 재질을 바꾸므로 정적 배칭에서 뺀다
                    if (c.name == CpTrigger) continue;
                    c.gameObject.SetActive(state == 1 || (state == 2 && c.name == CpDisc));
                }
                disc.GetComponent<MeshRenderer>().sharedMaterial = state == 2 ? done : fill;
            }
            Notes.Add($"체크포인트 {D.Route.Count}개: 데이터 높이와 발밑 면 높이 차 최대 {worst:F2}m · 길잡이 주황 · 시작 상태 = {RouteStartIndex + 1}번 목표");
        }

        /// 빛 기둥: 뚜껑 없는 원통(아래 = 원점), UV v = 높이 비율(위로 갈수록 투명한 알파 텍스처)
        static Mesh BeamMesh(string name, float radius, float height, int sides)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            for (int i = 0; i <= sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                var p = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                v.Add(p); uv.Add(new Vector2((float)i / sides, 0f));
                v.Add(p + Vector3.up * height); uv.Add(new Vector2((float)i / sides, 1f));
            }
            for (int i = 0; i < sides; i++)
            {
                int a0 = i * 2, a1 = a0 + 1, b0 = a0 + 2, b1 = a0 + 3;
                tri.Add(a0); tri.Add(a1); tri.Add(b1); tri.Add(a0); tri.Add(b1); tri.Add(b0);
            }
            var m = new Mesh { name = name };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        static void BuildSpawn(Transform root)
        {
            var sp = new GameObject("Spawn");
            sp.transform.SetParent(root, false);
            sp.transform.SetPositionAndRotation(D.SpawnPos, Quaternion.Euler(0f, D.SpawnYaw, 0f));
            if (GroundY(D.SpawnPos, out float gy, out var hit))
                Notes.Add($"시작 지점: 데이터 y {D.SpawnPos.y:F2} vs 발밑 {gy:F2}({hit.name}), yaw {D.SpawnYaw}°");

            // 바닥 화살표(바라보는 방향)
            var rot = Quaternion.Euler(0f, D.SpawnYaw, 0f);
            Vector3 P(float x, float z) => D.SpawnPos + rot * new Vector3(x, 0.07f, z);
            var mb = new MB();
            mb.Tri(P(0f, 1.1f), P(0.45f, 0.2f), P(-0.45f, 0.2f), Vector3.up);
            mb.Quad(P(-0.18f, 0.2f), P(0.18f, 0.2f), P(0.18f, -0.6f), P(-0.18f, -0.6f), Vector3.up);
            MeshObj("시작 방향 화살표", sp.transform, Reg(mb.ToMesh("Z1_SpawnArrow")), Mat("checkpoint"), 0, false, false);

            // 키 기준 인형: 시우 정적 모델(1.74m). EditorOnly 라 빌드에는 안 들어간다. M1 리그를 붙일 때 지울 것
            var refGo = new GameObject("ScaleRef_Siwoo (EditorOnly)") { tag = "EditorOnly" };
            refGo.transform.SetParent(sp.transform, false);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(SandboxSetup.GlbStatic);
            var toon = AssetDatabase.LoadAssetAtPath<Material>(SandboxSetup.MatDir + "/M_Siwoo_Toon.mat");
            if (model != null)
            {
                var inst = SandboxSetup.Spawn(model, refGo.transform, 90f);
                if (toon != null)
                    foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
                    {
                        var ms = r.sharedMaterials;
                        for (int i = 0; i < ms.Length; i++) ms[i] = toon;
                        r.sharedMaterials = ms;
                    }
                var bnd = SandboxSetup.FitToGround(refGo.transform, inst.transform, D.SiwooHeight, true);
                Notes.Add($"키 기준 인형: 시우 키 {bnd.size.y:F2}m, 발 y {bnd.min.y:F2}");
            }
            else
            {
                var cap = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                Object.DestroyImmediate(cap.GetComponent<Collider>());
                cap.name = "키 1.74m 캡슐";
                cap.transform.SetParent(refGo.transform, false);
                cap.transform.localScale = new Vector3(0.5f, D.SiwooHeight / 2f, 0.5f);
                cap.transform.localPosition = new Vector3(0f, D.SiwooHeight / 2f, 0f);
                cap.GetComponent<MeshRenderer>().sharedMaterial = Mat("checkpoint");
            }
        }

        /// 배치 스크린샷용 자리(BatchTools.Screenshot -view Zone1/Shots/이름). 꺼진 Camera 로 화각을 같이 담는다.
        static List<Transform> BuildShots(Transform parent)
        {
            var list = new List<Transform>();
            Transform Shot(string name, Vector3 pos, Quaternion rot, float fov)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.transform.SetPositionAndRotation(pos, rot);
                var c = go.AddComponent<Camera>();
                c.enabled = false; c.fieldOfView = fov; c.nearClipPlane = 0.1f; c.farClipPlane = 900f;
                list.Add(go.transform);
                return go.transform;
            }
            Transform Follow(string name, Vector3 feet, float yaw, float pitch, float dist, float fov)
            {
                // 07 문서 4장 탐색 카메라 근사: 따라가는 점 = 발 + 1.40m, 거리 dist, 피치 pitch.
                // 뒤가 막혀 있으면 Deoccluder 처럼 막힌 곳 앞으로 당긴다(카메라 반지름 0.10)
                var r = Quaternion.Euler(pitch, yaw, 0f);
                var tgt = feet + Vector3.up * 1.40f;
                var back = -(r * Vector3.forward);
                if (Physics.SphereCast(tgt, 0.1f, back, out var hit, dist, GroundMask | (1 << LWall), QueryTriggerInteraction.Ignore))
                {
                    Notes.Add($"촬영 자리 '{name}': 뒤에 '{hit.collider.name}' 이(가) 있어 카메라를 {dist:F1}m → {Mathf.Max(0.6f, hit.distance - 0.05f):F2}m 로 당김");
                    dist = Mathf.Max(0.6f, hit.distance - 0.05f);
                }
                return Shot(name, tgt + back * dist, r, fov);
            }
            Transform Look(string name, Vector3 pos, Vector3 at, float fov) => Shot(name, pos, Quaternion.LookRotation(at - pos, Vector3.up), fov);

            // (b) 시작 지점 눈높이(1.6m) — 시우가 서는 자리에서 바라보는 방향 그대로. 키 기준 인형 바로 앞
            var yawRot = Quaternion.Euler(0f, D.SpawnYaw, 0f);
            Shot("Shot_Start", D.SpawnPos + yawRot * new Vector3(0f, 1.6f, 0.6f), Quaternion.Euler(-2f, D.SpawnYaw, 0f), 60f);
            // (b′) 시작 지점 게임 카메라(거리 4.0, 피치 8°, FOV 45). 등 뒤가 닫힌 후문이라 당겨진다 — Main Camera 기본 자리
            Follow("Shot_StartCam", D.SpawnPos, D.SpawnYaw, 8f, 4.0f, 45f);

            // (c) 중간 오르막 — 시우네 앞에서 꼭대기 계단(24단)을 올려다봄
            var st = D.Stairs.FirstOrDefault(s => s.Name.Contains("꼭대기 계단")) ?? D.Stairs[D.Stairs.Count / 2];
            Vector3 lo = st.From.y <= st.To.y ? st.From : st.To, hi = st.From.y <= st.To.y ? st.To : st.From;
            var up = Flat(hi - lo).normalized;
            var feet = lo - up * 2.0f;
            if (GroundY(feet, out float fy, out _)) feet.y = fy;
            Follow("Shot_Uphill", feet, Yaw(up), -4f, 4.6f, 50f);

            // (d) 와룡공원 — 공터 남동쪽 비탈 위에서 공터·성벽·서쪽 성곽길을 봄
            var arena = D.Pads.FirstOrDefault(p => p.Shape == "circle")?.Center ?? D.Route[Mathf.Min(6, D.Route.Count - 1)].Pos;
            Look("Shot_Waryong", arena + new Vector3(10f, 3.4f, -11f), arena + new Vector3(-12f, 1.2f, 4f), 50f);

            // (보너스) 꼭대기 계단참에서 성곽이 처음 보이는 순간
            var cp6 = D.Route.Count >= 6 ? D.Route[5].Pos : D.SpawnPos;
            Look("Shot_Reveal", cp6 + new Vector3(-1.0f, 1.6f, 0.9f), cp6 + new Vector3(-15f, 4.5f, 17f), 55f);

            // (e) 명륜3가 골목 안 눈높이 — 체크포인트 4 를 지나 시우네 집 쪽(양옆 단독주택·담, 골목 바닥)
            var alley = D.Roads.FirstOrDefault(r => r.Name.Contains("시우네 골목"));
            if (alley != null)
                Look("Shot_Alley", PolyAt(alley.Points, 1.2f) + Vector3.up * 1.6f, PolyAt(alley.Points, 14.5f) + Vector3.up * 1.2f, 62f);
            // (f) 체크포인트 + 이름표 — 시작 지점 뒤에서 다음 목표(시작 상태 = 2번)를 봄
            var tgt = D.Route[Mathf.Min(RouteStartIndex, D.Route.Count - 1)].Pos;
            var dirT = Flat(tgt - D.SpawnPos).normalized;
            Look("Shot_Target", D.SpawnPos - dirT * 1.2f + Vector3.up * 2.1f, tgt + Vector3.up * 2.4f, 58f);
            return list;
        }

        /// 폴리라인 위 호 길이 s 의 점(끝을 넘으면 끝점)
        static Vector3 PolyAt(Vector3[] pts, float s)
        {
            for (int i = 0; i < pts.Length - 1; i++)
            {
                float L = Flat(pts[i + 1] - pts[i]).magnitude;
                if (s <= L || i == pts.Length - 2) return Vector3.Lerp(pts[i], pts[i + 1], Mathf.Clamp01(s / Mathf.Max(L, 1e-4f)));
                s -= L;
            }
            return pts[pts.Length - 1];
        }

        /// -shots &lt;폴더&gt;: 다시 연 장면에서 점검 컷을 찍는다(평면도 1 + 골목 + 계단참 성곽 + 와룡공원 + 체크포인트·이름표 + 시작 정적 카메라).
        /// 이름표는 런타임 NameTags 를 리플렉션으로 불러 그 카메라를 보게 한다(이 어셈블리는 게임 코드를 참조하지 않음).
        static void TakeShots(string dir)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) { Debug.LogWarning($"{Tag} -shots: 그래픽 장치가 없어 건너뜀(-nographics 빼기)"); return; }
            ShaderUtil.allowAsyncCompilation = false;
            System.IO.Directory.CreateDirectory(dir);
            var cam = Camera.main ?? throw new Exception("Main Camera 가 없습니다");
            var tagsType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Haengin.NameTags")).FirstOrDefault(t => t != null);
            var tags = tagsType != null ? Object.FindAnyObjectByType(tagsType) : null;
            void Tags(string method, params object[] args) => tagsType?.GetMethod(method)?.Invoke(tags, args);
            var shots = new (string file, string view)[]
            {
                ("z1_1_top", null), ("z1_2_alley", "Shot_Alley"), ("z1_3_reveal", "Shot_Reveal"), ("z1_4_waryong", "Shot_Waryong"),
                ("z1_5_target", "Shot_Target"), ("z1_6_startcam", "Shot_StartCam"), ("z1_7_uphill", "Shot_Uphill"),
            };
            // 화면 위 HUD(오버레이 캔버스)는 렌더 텍스처에 안 그려지므로, HUD 를 보여 줄 컷에서만 카메라 캔버스로 잠깐 바꾼다
            var hud = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(c => c.isRootCanvas && c.name.StartsWith("HUD"));
            foreach (var t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) t.ForceMeshUpdate(true, true);
            foreach (var (file, view) in shots)
            {
                string path = System.IO.Path.Combine(dir, file + ".png");
                cam.orthographic = false;
                if (view == null)
                {
                    if (tags != null) Tags("HideAll");
                    var restore = BatchTools.SetupTopDown(cam, "-274,70,-130,174", 1800f / 1300f);
                    try { BatchTools.RenderToPng(cam, 1800, 1300, 1, path); }
                    finally { restore(); cam.orthographic = false; }
                }
                else
                {
                    var v = GameObject.Find("Zone1/Shots/" + view);
                    if (v == null) { Debug.LogWarning($"{Tag} 촬영 자리 '{view}' 없음"); continue; }
                    cam.transform.SetPositionAndRotation(v.transform.position, v.transform.rotation);
                    var vc = v.GetComponent<Camera>();
                    if (vc != null) { cam.fieldOfView = vc.fieldOfView; cam.nearClipPlane = 0.08f; cam.farClipPlane = vc.farClipPlane; }
                    if (tags != null) Tags("Apply", cam);
                    bool withHud = hud != null && view == "Shot_Target";
                    float sf = withHud ? hud.scaleFactor : 1f;
                    if (withHud)
                    {
                        // 배치에서는 CanvasScaler 가 돌지 않으므로 2배 슈퍼샘플(2160p) 기준 배율을 직접 준다(기준 해상도 1080p)
                        hud.renderMode = RenderMode.ScreenSpaceCamera; hud.worldCamera = cam; hud.planeDistance = 0.5f; hud.scaleFactor = 2f;
                        Canvas.ForceUpdateCanvases();
                    }
                    try { BatchTools.RenderToPng(cam, 1920, 1080, 2, path); }
                    finally { if (withHud) { hud.renderMode = RenderMode.ScreenSpaceOverlay; hud.worldCamera = null; hud.scaleFactor = sf; } }
                }
                Debug.Log($"{Tag} 촬영: {path}");
            }
            KoreanFont.ResetDynamic(LabelFont);
        }

        // ───────────────────────── 점검
        /// 길·계단 위를 시우 캡슐(반지름 0.25, 키 1.74)로 0.5m마다 훑어 건물·담 등에 걸리는지 센다. 경계 차단물은 따로 센다.
        static int CheckClearance()
        {
            int centerBlocked = 0, edgeBlocked = 0, barrier = 0;
            var hits = new Dictionary<string, HashSet<string>>();
            void Probe(string where, Vector3 p, bool center)
            {
                var p0 = p + Vector3.up * (PlayerRadius + 0.06f);
                var p1 = p + Vector3.up * (D.SiwooHeight - PlayerRadius);
                var cols = Physics.OverlapCapsule(p0, p1, PlayerRadius - 0.01f, BlockMask, QueryTriggerInteraction.Ignore);
                if (cols.Length == 0) return;
                var names = cols.Select(Who).ToList();
                if (names.All(n => n.Contains("경계") || n.Contains("통제") || n.Contains("차단"))) { barrier++; return; }
                if (center) centerBlocked++; else edgeBlocked++;
                if (!hits.TryGetValue(where, out var set)) hits[where] = set = new HashSet<string>();
                foreach (var n in names) set.Add($"{n}{(center ? "" : "(가장자리)")} @({p.x:F1},{p.z:F1})");
            }
            foreach (var r in D.Roads)
            {
                float hw = r.Width / 2f, total = 0f;
                for (int k = 0; k < r.Points.Length - 1; k++) total += Flat(r.Points[k + 1] - r.Points[k]).magnitude;
                float acc = 0f;
                for (int k = 0; k < r.Points.Length - 1; k++)
                {
                    Vector3 a = r.Points[k], b = r.Points[k + 1];
                    float L = Flat(b - a).magnitude;
                    if (L < 1e-3f) continue;
                    var n = Vector3.Cross(Vector3.up, Flat(b - a).normalized);
                    for (float s = 0f; s <= L; s += 0.5f)
                    {
                        float along = acc + s;
                        if (along < 1f || along > total - 1f) continue;
                        var p = Vector3.Lerp(a, b, s / L) + Vector3.up * 0.03f;
                        Probe(r.Name, p, true);
                        if (hw > 0.6f) { Probe(r.Name, p + n * (hw - 0.35f), false); Probe(r.Name, p - n * (hw - 0.35f), false); }
                    }
                    acc += L;
                }
            }
            foreach (var s in D.Stairs)
            {
                var n = Vector3.Cross(Vector3.up, Flat(s.To - s.From).normalized);
                float L = Flat(s.To - s.From).magnitude;
                for (float d = 0.3f; d <= L - 0.3f; d += 0.5f)
                {
                    var p = Vector3.Lerp(s.From, s.To, d / L) + Vector3.up * 0.03f;
                    Probe(s.Name, p, true);
                    if (s.Width / 2f > 0.6f) { Probe(s.Name, p + n * (s.Width / 2f - 0.35f), false); Probe(s.Name, p - n * (s.Width / 2f - 0.35f), false); }
                }
            }
            Notes.Add($"길·계단 캡슐 검사: 가운데선 막힘 {centerBlocked}곳 · 가장자리만 닿음 {edgeBlocked}곳 · 경계 차단물(의도) {barrier}곳");
            foreach (var kv in hits) Notes.Add($"  닿는 것 '{kv.Key}': {string.Join(", ", kv.Value.Take(6))}{(kv.Value.Count > 6 ? $" 외 {kv.Value.Count - 6}" : "")}");
            return centerBlocked;
        }

        /// 콜라이더가 붙은 오브젝트가 '기둥'·'충돌…' 같은 부품이면 부모(랜드마크·성곽) 이름으로.
        static string Who(Collider c)
        {
            var t = c.transform;
            bool part = t.parent != null && (t.name.StartsWith("기둥") || t.name.StartsWith("충돌") || t.name.Contains("표지 기둥"));
            return part ? t.parent.name : t.name;
        }

        /// 시우 크기(반지름 0.25·키 1.74·턱 0.3·경사 40°)로 NavMesh 를 임시로 구워 체크포인트 1→10 을 잇는다(저장 안 함).
        /// 지형을 '걸을 수 없음'으로 둔 판(길·계단·패드만)과 지형도 걷는 판 두 가지.
        static string CheckNavMesh()
        {
            string strict = NavPass(false), loose = NavPass(true);
            return $"길만 {strict} / 맨땅 포함 {loose}";
        }

        static string NavPass(bool terrainWalkable)
        {
            var st = NavMesh.GetSettingsByID(0);
            if (st.agentTypeID == -1) st = NavMesh.CreateSettings();
            st.agentRadius = PlayerRadius; st.agentHeight = D.SiwooHeight; st.agentSlope = 40f; st.agentClimb = D.StepOffset;
            st.overrideVoxelSize = true; st.voxelSize = 0.08f; st.minRegionArea = 0.5f;
            var center = (D.BoundsMin + D.BoundsMax) / 2f;
            var bounds = new Bounds(center, D.BoundsMax - D.BoundsMin + new Vector3(2f, 10f, 2f));
            var sources = new List<NavMeshBuildSource>();
            NavBuilder.CollectSources(bounds, (1 << LGround) | BlockMask, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
            if (!terrainWalkable)
                for (int i = 0; i < sources.Count; i++)
                    if (sources[i].component == TerrainCol) { var s = sources[i]; s.area = 1; sources[i] = s; }
            var data = NavBuilder.BuildNavMeshData(st, sources, bounds, Vector3.zero, Quaternion.identity);
            if (data == null) return "굽기 실패";
            var inst = NavMesh.AddNavMeshData(data);
            try
            {
                var filter = new NavMeshQueryFilter { agentTypeID = st.agentTypeID, areaMask = NavMesh.AllAreas };
                var pts = new List<Vector3> { D.SpawnPos };
                pts.AddRange(D.Route.Select(c => c.Pos));
                var snapped = new List<Vector3>();
                var lines = new List<string>();
                int miss = 0;
                foreach (var p in pts)
                {
                    if (NavMesh.SamplePosition(p, out var h, 1.5f, filter)) snapped.Add(h.position);
                    else { snapped.Add(new Vector3(float.NaN, 0, 0)); miss++; }
                }
                int ok = 0, broken = 0; float total = 0f;
                var path = new NavMeshPath();
                for (int i = 1; i < snapped.Count - 1; i++)
                {
                    Vector3 a = snapped[i], b = snapped[i + 1];
                    string seg = $"{i}→{i + 1}";
                    if (float.IsNaN(a.x) || float.IsNaN(b.x)) { broken++; lines.Add($"{seg} 끝점이 NavMesh 밖"); continue; }
                    NavMesh.CalculatePath(a, b, filter, path);
                    float len = 0f;
                    var c = path.corners;
                    for (int k = 1; k < c.Length; k++) len += Vector3.Distance(c[k - 1], c[k]);
                    if (path.status == NavMeshPathStatus.PathComplete) { ok++; total += len; lines.Add($"{seg} {len:F1}m"); }
                    else { broken++; lines.Add($"{seg} 끊김({path.status}, {len:F1}m 까지)"); }
                }
                string head = $"{(terrainWalkable ? "맨땅 포함" : "길만")}: 9구간 중 완주 {ok} · 끊김 {broken} · 끝점 밖 {miss}";
                Notes.Add($"NavMesh {head} · 완주 구간 합 {total:F1}m | {string.Join(" · ", lines)}");
                return $"완주 {ok}/{snapped.Count - 2}{(broken > 0 ? $" 끊김 {broken}" : "")}, 합 {total:F1}m";
            }
            finally
            {
                inst.Remove();
                Object.DestroyImmediate(data);
            }
        }

        // ───────────────────────── 물리 질의 도우미
        /// 위에서 아래로 Ground 레이어만 쏴서 가장 위 걷는 면 높이.
        static bool GroundY(Vector3 p, out float y, out Collider col)
        {
            var origin = new Vector3(p.x, D.BoundsMax.y + 30f, p.z);
            if (Physics.Raycast(origin, Vector3.down, out var hit, 400f, GroundMask, QueryTriggerInteraction.Ignore))
            { y = hit.point.y; col = hit.collider; return true; }
            y = p.y; col = null; return false;
        }

        /// p 에서 radius 안 Wall 상자 중 가장 가까운 면의 점과 바깥 법선(수평).
        static bool NearestWallFace(Vector3 p, float radius, out Vector3 point, out Vector3 normal)
        {
            point = p; normal = Vector3.back;
            float best = float.MaxValue; bool found = false;
            foreach (var c in Physics.OverlapSphere(p, radius, 1 << LWall, QueryTriggerInteraction.Ignore))
            {
                if (!(c is BoxCollider bc)) continue;
                var q = bc.ClosestPoint(p);
                var tr = bc.transform;
                Vector3 n;
                if ((q - p).sqrMagnitude < 1e-6f)
                {
                    // p 가 상자 안: 가장 가까운 옆면으로 밀어낸다
                    var l = tr.InverseTransformPoint(p) - bc.center;
                    float dx = (bc.size.x / 2f - Mathf.Abs(l.x)) * tr.lossyScale.x, dz = (bc.size.z / 2f - Mathf.Abs(l.z)) * tr.lossyScale.z;
                    n = dx < dz ? tr.right * Mathf.Sign(l.x) : tr.forward * Mathf.Sign(l.z);
                    q = p + n * Mathf.Min(dx, dz);
                }
                else n = Flat(p - q).normalized;
                float d = Flat(q - p).sqrMagnitude;
                if (n.sqrMagnitude < 0.5f || d >= best) continue;
                best = d; point = q; normal = n; found = true;
            }
            return found;
        }

        static Vector3 DirToRoad(Vector3 p)
        {
            float best = float.MaxValue; Vector3 q = p;
            foreach (var r in D.Roads)
                for (int k = 0; k < r.Points.Length - 1; k++)
                {
                    SegClosest(r.Points[k], r.Points[k + 1], p.x, p.z, out float d, out _, out float t);
                    if (d < best) { best = d; q = Vector3.Lerp(r.Points[k], r.Points[k + 1], t); }
                }
            var dir = Flat(q - p);
            return dir.sqrMagnitude < 1e-4f ? Vector3.back : dir.normalized;
        }

        // ───────────────────────── 오브젝트·메시 도우미
        static Transform Group(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static void Static(GameObject go) => GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);

        static Mesh Reg(Mesh m) { Meshes.Add(m); return m; }

        static GameObject MeshObj(string name, Transform parent, Mesh mesh, Material mat, int layer, bool collider, bool shadows = true)
        {
            var go = new GameObject(name) { layer = layer };
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); // 메시 정점이 월드 좌표라 부모 위치와 상관없이 원점에 둔다
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            Static(go);
            return go;
        }

        static GameObject Cube(string name, Transform parent, Vector3 center, Vector3 size, float yaw, Material mat, int layer, bool collider)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.layer = layer;
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(center, Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            Static(go);
            return go;
        }

        /// 원기둥(바닥 중심 basePos, 지름, 높이). 콜라이더는 Unity 기본 캡슐.
        static GameObject Cyl(string name, Transform parent, Vector3 basePos, float dia, float height, Material mat, int layer, bool collider)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.layer = layer;
            go.transform.SetParent(parent, false);
            go.transform.position = basePos + Vector3.up * (height / 2f);
            go.transform.localScale = new Vector3(dia, height / 2f, dia);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            Static(go);
            return go;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
        static float Yaw(Vector3 dir) => Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        static string Safe(string s) => Regex.Replace(s ?? "x", @"[^\w가-힣]+", "_");

        static List<Vector3> Circle(Vector3 c, float r, int n)
        {
            var l = new List<Vector3>(n);
            for (int i = 0; i < n; i++) { float a = i * Mathf.PI * 2f / n; l.Add(c + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r)); }
            return l;
        }

        /// 볼록 다각형(수평)을 y0~y1 로 세운 기둥.
        static Mesh Prism(string name, List<Vector3> poly, float y0, float y1)
        {
            var mb = new MB();
            int n = poly.Count;
            Vector3 c = Vector3.zero; foreach (var p in poly) c += p; c /= n;
            for (int i = 0; i < n; i++)
            {
                Vector3 a = poly[i], b = poly[(i + 1) % n];
                Vector3 at = new Vector3(a.x, y1, a.z), bt = new Vector3(b.x, y1, b.z), ab = new Vector3(a.x, y0, a.z), bb = new Vector3(b.x, y0, b.z);
                mb.Tri(new Vector3(c.x, y1, c.z), at, bt, Vector3.up);
                mb.Tri(new Vector3(c.x, y0, c.z), ab, bb, Vector3.down);
                mb.Quad(ab, at, bt, bb, Flat((a + b) / 2f - c));
            }
            return mb.ToMesh(name);
        }

        /// 폴리라인을 폭 width 로 넓혀 위(y + above) ~ 아래(y − below)로 세운 띠(길·성곽). 꺾이는 점은 마이터.
        static Mesh Extrude(string name, Vector3[] P, float width, float below, float above)
        {
            int n = P.Length;
            var L = new Vector3[n]; var Rr = new Vector3[n];
            float hw = width / 2f;
            for (int k = 0; k < n; k++)
            {
                Vector3 tin = k > 0 ? Flat(P[k] - P[k - 1]).normalized : Vector3.zero;
                Vector3 tout = k < n - 1 ? Flat(P[k + 1] - P[k]).normalized : Vector3.zero;
                Vector3 t = (tin + tout).sqrMagnitude > 1e-6f ? (tin + tout).normalized : (tout.sqrMagnitude > 0 ? tout : tin);
                var nrm = new Vector3(-t.z, 0f, t.x);
                float scale = 1f;
                if (k > 0 && k < n - 1) scale = 1f / Mathf.Max(0.5f, Vector3.Dot(nrm, new Vector3(-tin.z, 0f, tin.x)));
                L[k] = P[k] + nrm * hw * scale; Rr[k] = P[k] - nrm * hw * scale;
            }
            var mb = new MB();
            Vector3 up = Vector3.up * above, dn = Vector3.down * below;
            for (int k = 0; k < n - 1; k++)
            {
                var seg = Flat(P[k + 1] - P[k]).normalized;
                var sn = new Vector3(-seg.z, 0f, seg.x);
                mb.Quad(L[k] + up, L[k + 1] + up, Rr[k + 1] + up, Rr[k] + up, Vector3.up);
                mb.Quad(L[k] + dn, L[k] + up, L[k + 1] + up, L[k + 1] + dn, sn);
                mb.Quad(Rr[k] + dn, Rr[k] + up, Rr[k + 1] + up, Rr[k + 1] + dn, -sn);
                mb.Quad(L[k] + dn, L[k + 1] + dn, Rr[k + 1] + dn, Rr[k] + dn, Vector3.down);
            }
            var t0 = Flat(P[1] - P[0]).normalized; var t1 = Flat(P[n - 1] - P[n - 2]).normalized;
            mb.Quad(L[0] + dn, L[0] + up, Rr[0] + up, Rr[0] + dn, -t0);
            mb.Quad(L[n - 1] + dn, L[n - 1] + up, Rr[n - 1] + up, Rr[n - 1] + dn, t1);
            return mb.ToMesh(name);
        }

        /// 걷는 면에 붙인 원판/고리(r0=0 이면 원판). 점마다 아래로 쏴서 면 높이 + lift.
        static Mesh Conform(string name, Vector3 c, float r0, float r1, float lift)
        {
            int seg = 48;
            int rings = Mathf.Max(1, Mathf.CeilToInt((r1 - r0) / 0.7f));
            Vector3 At(float r, float a)
            {
                var p = c + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                if (Physics.Raycast(new Vector3(p.x, c.y + 3f, p.z), Vector3.down, out var h, 8f, GroundMask, QueryTriggerInteraction.Ignore)) p.y = h.point.y + lift;
                else p.y = c.y + lift;
                return p;
            }
            var mb = new MB();
            for (int k = 0; k < rings; k++)
            {
                float ra = Mathf.Lerp(r0, r1, (float)k / rings), rb = Mathf.Lerp(r0, r1, (float)(k + 1) / rings);
                for (int i = 0; i < seg; i++)
                {
                    float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
                    if (ra < 1e-3f) mb.Tri(At(0f, 0f), At(rb, a0), At(rb, a1), Vector3.up);
                    else mb.Quad(At(ra, a0), At(rb, a0), At(rb, a1), At(ra, a1), Vector3.up);
                }
            }
            return mb.ToMesh(name);
        }

        static void AddQuadIdx(List<Vector3> v, List<int> t, int a, int b, int c, int d)
        {
            var n = Vector3.Cross(v[b] - v[a], v[c] - v[a]) + Vector3.Cross(v[c] - v[a], v[d] - v[a]);
            if (n.y < 0f) (b, d) = (d, b);
            t.Add(a); t.Add(b); t.Add(c); t.Add(a); t.Add(c); t.Add(d);
        }

        static Mesh Smooth(string name, List<Vector3> v, List<int> t)
        {
            var m = new Mesh { name = name };
            if (v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        /// 메시 에셋: 주 오브젝트는 빈 '목록' 메시(fileID 4300000, GUID 유지), 생성 메시는 모두 서브 에셋.
        /// 이미 있으면 지우지 않고 안의 서브 에셋만 갈아 끼운다 → .meta GUID 와 장면의 참조 GUID 가 바뀌지 않는다.
        static void SaveMeshes()
        {
            if (Meshes.Count == 0) return;
            var main = AssetDatabase.LoadMainAssetAtPath(MeshPath) as Mesh;
            if (main == null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(MeshPath) != null) AssetDatabase.DeleteAsset(MeshPath);
                main = new Mesh();
                AssetDatabase.CreateAsset(main, MeshPath);
            }
            else
                foreach (var o in AssetDatabase.LoadAllAssetRepresentationsAtPath(MeshPath))
                {
                    if (o == null) continue;
                    AssetDatabase.RemoveObjectFromAsset(o);
                    Object.DestroyImmediate(o, true);
                }
            main.Clear();
            main.name = "Zone1_Mesh";   // 파일 이름과 같게(다르면 임포트 경고). 빈 목록 메시 — 실제 메시는 서브 에셋
            EditorUtility.SetDirty(main);
            foreach (var m in Meshes) AssetDatabase.AddObjectToAsset(m, main);
            AssetDatabase.SaveAssets();
        }

        /// 면마다 정점을 따로 두는(각진 음영) 간단한 메시 조립기. 면 방향은 hint 와 같은 쪽으로 맞춘다.
        /// UV 는 면마다 월드 미터 단위로 붙인다(2026-10-05 2차): 윗면·바닥 = (x, z), 옆면 = (면을 따라간 거리, y).
        /// 그래서 돌 줄눈·판석 텍스처는 재질 Tiling 하나로 어디서나 같은 크기, 성벽 켜는 늘 수평이다.
        sealed class MB
        {
            public readonly List<Vector3> V = new List<Vector3>();
            readonly List<Vector2> UV = new List<Vector2>();
            readonly List<int> T = new List<int>();

            void AddUv(Vector3 n, params Vector3[] ps)
            {
                n = n.sqrMagnitude > 1e-12f ? n.normalized : Vector3.up;
                if (Mathf.Abs(n.y) > 0.7f) { foreach (var p in ps) UV.Add(new Vector2(p.x, p.z)); return; }
                var t = Vector3.Cross(Vector3.up, n).normalized;
                foreach (var p in ps) UV.Add(new Vector2(Vector3.Dot(p, t), p.y));
            }

            public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 hint)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), hint) < 0f) (b, c) = (c, b);
                int s = V.Count; V.Add(a); V.Add(b); V.Add(c);
                AddUv(Vector3.Cross(b - a, c - a), a, b, c);
                T.Add(s); T.Add(s + 1); T.Add(s + 2);
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 hint)
            {
                var n = Vector3.Cross(b - a, c - a) + Vector3.Cross(c - a, d - a);
                if (Vector3.Dot(n, hint) < 0f) { (b, d) = (d, b); n = -n; }
                int s = V.Count; V.Add(a); V.Add(b); V.Add(c); V.Add(d);
                AddUv(n, a, b, c, d);
                T.Add(s); T.Add(s + 1); T.Add(s + 2); T.Add(s); T.Add(s + 2); T.Add(s + 3);
            }

            public void Box(Vector3 c, Quaternion r, Vector3 size)
            {
                Vector3 ex = r * new Vector3(size.x / 2f, 0f, 0f), ey = r * new Vector3(0f, size.y / 2f, 0f), ez = r * new Vector3(0f, 0f, size.z / 2f);
                Vector3 P(int sx, int sy, int sz) => c + ex * sx + ey * sy + ez * sz;
                Quad(P(-1, 1, -1), P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), ey);
                Quad(P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1), -ey);
                Quad(P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1), ex);
                Quad(P(-1, -1, -1), P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), -ex);
                Quad(P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), ez);
                Quad(P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), P(1, -1, -1), -ez);
            }

            /// a→b 를 잇는 가는 각기둥(전선).
            public void Line(Vector3 a, Vector3 b, float thick)
            {
                var d = b - a;
                if (d.sqrMagnitude < 1e-6f) return;
                Box((a + b) / 2f, Quaternion.LookRotation(d.normalized, Vector3.up), new Vector3(thick, thick, d.magnitude));
            }

            /// 둥근 산: 바닥 반지름 r → 중턱(높이 50%) 0.62r → 어깨(82%) 0.3r → 꼭대기. 꼭대기는 sk 만큼 비스듬히.
            public void Mountain(Vector3 c, float r, float topY, int sides, float sk)
            {
                if (r <= 1f) return;
                float h = topY - c.y;
                var ringH = new[] { 0f, 0.5f, 0.82f };
                var ringR = new[] { 1f, 0.62f, 0.3f };
                var off = new Vector3(r * 0.15f * sk, 0f, -r * 0.1f * sk);
                Vector3 P(int ring, int i)
                {
                    float a = i * Mathf.PI * 2f / sides;
                    return c + off * ringH[ring] + new Vector3(Mathf.Cos(a) * r * ringR[ring], h * ringH[ring], Mathf.Sin(a) * r * ringR[ring]);
                }
                var apex = c + off + Vector3.up * h;
                for (int i = 0; i < sides; i++)
                {
                    for (int k = 0; k < 2; k++)
                    {
                        Vector3 a0 = P(k, i), a1 = P(k, i + 1), b1 = P(k + 1, i + 1), b0 = P(k + 1, i);
                        Quad(a0, a1, b1, b0, Flat((a0 + a1) / 2f - c) + Vector3.up * 0.2f);
                    }
                    Tri(P(2, i), P(2, i + 1), apex, Flat((P(2, i) + P(2, i + 1)) / 2f - c) + Vector3.up * 0.5f);
                }
            }

            public Mesh ToMesh(string name)
            {
                var m = new Mesh { name = name };
                if (V.Count > 65000) m.indexFormat = IndexFormat.UInt32;
                m.SetVertices(V); m.SetUVs(0, UV); m.SetTriangles(T, 0);
                m.RecalculateNormals(); m.RecalculateBounds();
                return m;
            }
        }
    }
}
