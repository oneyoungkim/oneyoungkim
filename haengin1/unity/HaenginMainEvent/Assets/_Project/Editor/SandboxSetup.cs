// 행인1의 메인이벤트 — 시험 장면(Sandbox) 자동 구성
// batchmode: -executeMethod Haengin.EditorTools.SandboxSetup.Build [-modelYaw 90]
// 다시 실행하면 재질·볼륨·장면을 덮어쓴다(같은 경로, GUID 유지).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Haengin.EditorTools
{
    public static class SandboxSetup
    {
        const string Tag = "[SandboxSetup]";
        public const string Root = "Assets/_Project";
        public const string SiwooDir = Root + "/Art/Characters/Siwoo";
        public const string GlbStatic = SiwooDir + "/siwoo_tripo_v1.glb";
        public const string GlbRigged = SiwooDir + "/siwoo_tripo_v1_rigged.glb";
        public const string MatDir = Root + "/Materials";
        public const string SettingsDir = Root + "/Settings";
        public const string ScenePath = Root + "/Scenes/Sandbox.unity";
        public const string VolumePath = SettingsDir + "/Sandbox_FilmVolume.asset";

        // FUJIMOTO_STYLE 6장 색표
        static readonly Color Paper = Hex("#F4EFE6");     // 종이(배경)
        static readonly Color SandFloor = Hex("#E9E0CC"); // 모래 베이지 바닥 = 깊이 안개색
        static readonly Color Ink = Hex("#1A1417");       // 외곽선 따뜻한 검정
        // 6-3 곱셈값: 텍스처 한 장(아틀라스)에 피부·옷·머리가 섞여 있어 skin(.80,.72,.72)·dark(.70,.70,.78)·mid 의 중간(모브)으로 1단을 잡는다.
        static readonly Color Shade1 = new Color(0.78f, 0.71f, 0.74f, 1f);
        // 2단(가려진 곳) = 1단 × (.84, .80, .80)
        static readonly Color Shade2 = new Color(0.78f * 0.84f, 0.71f * 0.80f, 0.74f * 0.80f, 1f);

        public const float SiwooHeight = 1.74f;   // 2026-10-05: Siwoo is 174 cm, bantamweight 61 kg

        static Color Hex(string h) => ColorUtility.TryParseHtmlString(h, out var c) ? c : Color.magenta;

        static float ArgFloat(string name, float def)
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++)
                if (string.Equals(a[i], name, StringComparison.OrdinalIgnoreCase) &&
                    float.TryParse(a[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) return v;
            return def;
        }

        [MenuItem("Haengin/Sandbox 장면 다시 만들기")]
        public static void Build()
        {
            // Tripo GLB 는 모델 정면이 glTF +X. glTFast 가 오른손→왼손 변환(X 반전)을 하므로 Unity 에선 정면이 -X → Y +90° 로 +Z 를 보게 한다.
            float modelYaw = ArgFloat("-modelYaw", 90f);

            EnsureFolder(MatDir); EnsureFolder(SettingsDir); EnsureFolder(Root + "/Scenes");
            AssetDatabase.ImportAsset(GlbStatic, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(GlbRigged, ImportAssetOptions.ForceSynchronousImport);

            var staticModel = AssetDatabase.LoadAssetAtPath<GameObject>(GlbStatic) ?? throw new Exception("GLB 임포트 결과가 없습니다: " + GlbStatic);
            var riggedModel = AssetDatabase.LoadAssetAtPath<GameObject>(GlbRigged) ?? throw new Exception("GLB 임포트 결과가 없습니다: " + GlbRigged);
            LogSubAssets(GlbStatic); LogSubAssets(GlbRigged);

            var floorMat = MakeFloorMaterial();
            var profile = MakeFilmVolume();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 환경: 종이색 배경 + 모래 안개 + 평평한 주변광
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.60f, 0.57f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = SandFloor;
            RenderSettings.fogStartDistance = 30f;
            RenderSettings.fogEndDistance = 140f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = null;
            RenderSettings.reflectionIntensity = 0f;

            // 조명: 앞 왼쪽 위에서 비추는 따뜻한 흰빛, 부드러운 그림자
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.973f, 0.933f);
            light.intensity = 1f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.7f;
            light.shadowBias = 0.02f;
            light.shadowNormalBias = 0.3f;
            lightGo.transform.rotation = Quaternion.Euler(48f, 150f, 0f);

            // 바닥
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor_Sand";
            floor.transform.localScale = new Vector3(30f, 1f, 30f); // 300m x 300m — 끝선이 안개(30~140m)에 묻히게
            floor.GetComponent<MeshRenderer>().sharedMaterial = floorMat;

            // 필름 후처리 볼륨
            var volGo = new GameObject("Global Volume (Film)");
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = profile;

            // 시우(리깅 없음): 원점, +Z 를 바라봄, 키 1.74m, 발이 바닥에
            var siwoo = new GameObject("Siwoo");
            var siwooModel = Spawn(staticModel, siwoo.transform, modelYaw);
            var toonStatic = ApplyToon(siwooModel, "M_Siwoo_Toon");
            var bStatic = FitToGround(siwoo.transform, siwooModel.transform, SiwooHeight, true);

            // 리깅 버전: 옆에 비활성으로 (팔 스키닝 깨짐 — 재생성 예정)
            var rigged = new GameObject("Siwoo_Rigged (비활성, 팔 스키닝 깨짐)");
            rigged.transform.position = new Vector3(1.6f, 0f, 0f);
            var riggedModelGo = Spawn(riggedModel, rigged.transform, modelYaw);
            ApplyToon(riggedModelGo, "M_SiwooRigged_Toon");
            var bRigged = FitToGround(rigged.transform, riggedModelGo.transform, -1f, false);
            rigged.SetActive(false);

            // 카메라: 메인 카메라 + Cinemachine Brain, CM 카메라 1대(전신)
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Paper;
            cam.fieldOfView = 30f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            camGo.AddComponent<AudioListener>();
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camData.antialiasingQuality = AntialiasingQuality.High;
            camGo.AddComponent<CinemachineBrain>();

            var cmGo = new GameObject("CM_FullBody");
            var cmc = cmGo.AddComponent<CinemachineCamera>();
            var lens = LensSettings.Default;
            lens.FieldOfView = 30f; lens.NearClipPlane = 0.1f; lens.FarClipPlane = 200f;
            cmc.Lens = lens;
            cmc.Priority = 10;
            // 전신이 들어오게: 시선 높이 = 키의 절반 남짓, 거리는 세로 화각 30°에서 키+여백이 들어오는 값
            float h = bStatic.size.y;
            var look = new Vector3(0f, h * 0.5f + 0.02f, 0f);
            float dist = (h * 1.3f) * 0.5f / Mathf.Tan(15f * Mathf.Deg2Rad);
            cmGo.transform.position = new Vector3(0f, h * 0.6f, dist);
            cmGo.transform.LookAt(look);
            camGo.transform.SetPositionAndRotation(cmGo.transform.position, cmGo.transform.rotation);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();

            Debug.Log($"{Tag} 완료: {ScenePath} | 시우 경계 중심={bStatic.center} 크기={bStatic.size} (키 {bStatic.size.y:F3}m, 발 y={bStatic.min.y:F3}) | " +
                      $"리깅판 크기={bRigged.size} 발 y={bRigged.min.y:F3} | 툰 재질 {toonStatic.Count}개 | 카메라 거리 {dist:F2}m | modelYaw={modelYaw}");
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        static void LogSubAssets(string path)
        {
            var all = AssetDatabase.LoadAllAssetsAtPath(path);
            var groups = all.GroupBy(o => o.GetType().Name).Select(g => $"{g.Key}×{g.Count()}");
            Debug.Log($"{Tag} {path} 하위 에셋: {string.Join(", ", groups)}");
            foreach (var m in all.OfType<Material>())
                Debug.Log($"{Tag}   재질 '{m.name}' 셰이더={m.shader.name} 기본텍스처={(GetBaseTex(m) ? GetBaseTex(m).name : "없음")}");
            foreach (var t in all.OfType<Texture2D>())
                Debug.Log($"{Tag}   텍스처 '{t.name}' {t.width}x{t.height}");
        }

        static GameObject Spawn(GameObject model, Transform parent, float yaw)
        {
            var go = PrefabUtility.InstantiatePrefab(model) as GameObject;
            if (go == null) go = UnityEngine.Object.Instantiate(model);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one;
            return go;
        }

        /// 키를 맞추고(targetHeight>0 일 때) 발이 y=0 에 닿고 수평 중심이 부모 원점에 오게 한다.
        static Bounds FitToGround(Transform root, Transform model, float targetHeight, bool center)
        {
            var b = WorldBounds(model);
            if (targetHeight > 0f && b.size.y > 1e-4f)
            {
                float k = targetHeight / b.size.y;
                model.localScale = model.localScale * k;
                b = WorldBounds(model);
            }
            var offset = new Vector3(center ? root.position.x - b.center.x : 0f, root.position.y - b.min.y, center ? root.position.z - b.center.z : 0f);
            model.position += offset;
            return WorldBounds(model);
        }

        /// 렌더러 경계. 스킨드 메시는 renderer.bounds 가 넉넉하게 잡히므로 현재 자세를 구워 실제 정점으로 잰다.
        static Bounds WorldBounds(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(t.position, Vector3.zero);
            bool has = false;
            var b = new Bounds();
            foreach (var r in rs)
            {
                Bounds rb;
                if (r is SkinnedMeshRenderer smr && smr.sharedMesh != null)
                {
                    var baked = new Mesh();
                    smr.BakeMesh(baked, true); // useScale=true → 렌더러 로컬 공간 정점, 거기에 로컬→월드 행렬을 곱한다
                    var m = smr.transform.localToWorldMatrix;
                    var vs = baked.vertices;
                    rb = new Bounds(m.MultiplyPoint3x4(vs[0]), Vector3.zero);
                    foreach (var v in vs) rb.Encapsulate(m.MultiplyPoint3x4(v));
                    UnityEngine.Object.DestroyImmediate(baked);
                }
                else rb = r.bounds;
                if (!has) { b = rb; has = true; } else b.Encapsulate(rb);
            }
            return b;
        }

        static Texture GetBaseTex(Material m)
        {
            foreach (var p in new[] { "baseColorTexture", "_BaseMap", "_MainTex", "_BaseColorMap" })
                if (m.HasProperty(p) && m.GetTexture(p) != null) return m.GetTexture(p);
            return null;
        }

        static Color GetBaseFactor(Material m)
        {
            foreach (var p in new[] { "baseColorFactor", "_BaseColor", "_Color" })
                if (m.HasProperty(p)) return m.GetColor(p);
            return Color.white;
        }

        /// 원래 glTF 재질의 기본 텍스처를 그대로 쓰는 UTS(Toon/Toon) 재질을 만들어 바꿔 끼운다.
        static List<Material> ApplyToon(GameObject model, string prefix)
        {
            var map = new Dictionary<Material, Material>();
            int idx = 0;
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var src = mats[i];
                    if (src == null) continue;
                    if (!map.TryGetValue(src, out var toon))
                    {
                        string name = idx == 0 ? prefix : $"{prefix}_{idx}";
                        toon = MakeToonMaterial(name, GetBaseTex(src), GetBaseFactor(src));
                        map[src] = toon; idx++;
                    }
                    mats[i] = toon;
                }
                r.sharedMaterials = mats;
                r.shadowCastingMode = ShadowCastingMode.On;
                r.receiveShadows = true;
            }
            return map.Values.ToList();
        }

        static Material MakeToonMaterial(string name, Texture baseTex, Color baseFactor)
        {
            var shader = Shader.Find("Toon/Toon") ?? throw new Exception("Unity Toon Shader(Toon/Toon) 를 찾지 못했습니다");
            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
            else mat.shader = shader;

            var baseCol = new Color(baseFactor.r, baseFactor.g, baseFactor.b, 1f);
            // 기본색 = 원래 텍스처(기본 텍스처 유지)
            mat.SetTexture("_MainTex", baseTex); mat.SetTexture("_BaseMap", baseTex);
            mat.SetColor("_BaseColor", baseCol); mat.SetColor("_Color", baseCol);
            mat.SetFloat("_Is_LightColor_Base", 1f);
            // 그림자 = 고유색 × 곱셈값 (1단), 2단은 가려진 곳만
            mat.SetFloat("_Use_BaseAs1st", 1f); mat.SetTexture("_1st_ShadeMap", baseTex);
            mat.SetColor("_1st_ShadeColor", Shade1);
            mat.SetFloat("_Use_1stAs2nd", 1f); mat.SetTexture("_2nd_ShadeMap", baseTex);
            mat.SetColor("_2nd_ShadeColor", Shade2);
            // 경계 또렷하게 (7-3: aa .02 수준)
            mat.SetFloat("_BaseColor_Step", 0.52f); mat.SetFloat("_BaseShade_Feather", 0.02f);
            mat.SetFloat("_ShadeColor_Step", 0.18f); mat.SetFloat("_1st2nd_Shades_Feather", 0.02f);
            mat.SetFloat("_1st_ShadeColor_Step", 0.52f); mat.SetFloat("_1st_ShadeColor_Feather", 0.02f);
            mat.SetFloat("_2nd_ShadeColor_Step", 0.18f); mat.SetFloat("_2nd_ShadeColor_Feather", 0.02f);
            mat.SetFloat("_Set_SystemShadowsToBase", 1f); mat.SetFloat("_Tweak_SystemShadowsLevel", 0f);
            // 스펙큘러 0, 림 끔, 매트캡·천사링 끔
            mat.SetColor("_HighColor", Color.black); mat.SetFloat("_HighColor_Power", 0f);
            mat.SetFloat("_Is_SpecularToHighColor", 0f);
            mat.SetFloat("_RimLight", 0f); mat.SetFloat("_Add_Antipodean_RimLight", 0f);
            mat.SetFloat("_MatCap", 0f); mat.SetFloat("_AngelRing", 0f);
            mat.SetFloat("_GI_Intensity", 0f);
            // 외곽선: 인버티드 헐, 따뜻한 검정, 조명색 영향 없음
            mat.SetFloat("_OUTLINE", 0f); // NML
            mat.SetFloat("_Outline_Width", 2.6f); // 오브젝트 공간 x0.001 (모델 원본 키 1.0 기준) → 1080p 전신 컷에서 실루엣 2~3px
            mat.SetColor("_Outline_Color", Ink);
            mat.SetFloat("_Is_BlendBaseColor", 0f);
            mat.SetFloat("_Is_LightColor_Outline", 0f);
            mat.SetFloat("_Farthest_Distance", 60f); mat.SetFloat("_Nearest_Distance", 0.5f);
            mat.SetFloat("_Offset_Z", 0f);
            mat.SetFloat("_CullMode", 2f); mat.SetFloat("_SRPDefaultUnlitColMode", 1f);
            mat.SetFloat("_ClippingMode", 0f); mat.SetFloat("_TransparentEnabled", 0f);
            mat.SetFloat("_ZWriteMode", 1f);
            foreach (var k in new[] { "_IS_CLIPPING_OFF", "_IS_TRANSCLIPPING_OFF", "_EMISSIVE_SIMPLE", "_OUTLINE_NML", "_IS_OUTLINE_CLIPPING_NO" })
                mat.EnableKeyword(k);
            foreach (var k in new[] { "_IS_CLIPPING_MODE", "_IS_CLIPPING_TRANSMODE", "_IS_TRANSCLIPPING_ON", "_EMISSIVE_ANIMATION", "_OUTLINE_POS", "_IS_OUTLINE_CLIPPING_YES", "_IS_ANGELRING_ON", "_SHADINGGRADEMAP" })
                mat.DisableKeyword(k);
            mat.SetShaderPassEnabled("SRPDefaultUnlit", true);
            mat.renderQueue = (int)RenderQueue.Geometry;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material MakeFloorMaterial()
        {
            string path = $"{MatDir}/M_Floor_Sand.mat";
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
            else mat.shader = shader;
            mat.SetColor("_BaseColor", SandFloor);
            mat.SetFloat("_Smoothness", 0f);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_SpecularHighlights", 0f); mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            mat.SetFloat("_EnvironmentReflections", 0f); mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// 6-4 필름 후처리: 채도 .85, 대비 약간 억제, 스플릿 톤(그림자 청록·밝은 쪽 따뜻하게), 블룸·톤매핑 없음
        static VolumeProfile MakeFilmVolume()
        {
            var old = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);
            if (old != null) AssetDatabase.DeleteAsset(VolumePath);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, VolumePath);

            var ca = profile.Add<ColorAdjustments>(true);
            ca.saturation.Override(-15f);
            ca.contrast.Override(-6f);
            ca.postExposure.Override(0f);

            var st = profile.Add<SplitToning>(true);
            st.shadows.Override(Color.Lerp(new Color(0.5f, 0.5f, 0.5f), Hex("#2F4A5A"), 0.35f));
            st.highlights.Override(Color.Lerp(new Color(0.5f, 0.5f, 0.5f), Hex("#F3D9B0"), 0.35f));
            st.balance.Override(0f);

            var tm = profile.Add<Tonemapping>(true);
            tm.mode.Override(TonemappingMode.None);

            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0f);
            bloom.active = false;

            foreach (var c in profile.components)
            {
                c.name = c.GetType().Name;
                AssetDatabase.AddObjectToAsset(c, profile);
            }
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }
    }
}
