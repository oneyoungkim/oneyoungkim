// 행인1의 메인이벤트 — M2 전투 설정 (docs/08_M2_전투_설계.md 10장·11장 0단계)
// batchmode:
//   -executeMethod Haengin.EditorGame.CombatSetup.Build     레이어 12 Fighter·13 Crowd + 충돌 행렬 + CombatTuning·SiwooMoves 에셋(없을 때만)
//                                                          + Player 프리팹에 전투 부품 + Scenes/CombatLab.unity (시험장: 평지·벽·구경꾼 원·허수아비)
//   -executeMethod Haengin.EditorGame.CombatSetup.BuildAll  위 + M1 다시 만들기(Zone1 — CM_Explore 에 흔들림 확장)
using System;
using System.Linq;
using Haengin.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Haengin.EditorGame
{
    public static class CombatSetup
    {
        const string Tag = "[CombatSetup]";
        public const string Root = "Assets/_Project";
        public const string TuningPath = Root + "/Settings/CombatTuning.asset";
        public const string MovesPath = Root + "/Settings/SiwooMoves.asset";
        public const string BlendsPath = Root + "/Settings/CombatBlends.asset";
        public const string LabScene = Root + "/Scenes/CombatLab.unity";
        public const string LabMatDir = Root + "/Materials/Lab";
        public const string DummyMat = LabMatDir + "/M_Dummy_Toon.mat";
        public const string FilmVolume = Root + "/Settings/Sandbox_FilmVolume.asset";

        [MenuItem("Haengin/M2 전투 설정 + 전투 연습장")]
        public static void Build() => Run(() =>
        {
            EnsureLayers();
            EnsureAssets();
            M1Setup.EnsureTuning();
            M1Setup.BuildPlayerPrefab();
            BuildLab();
        });

        [MenuItem("Haengin/M2 전투 설정 + M1 다시 만들기")]
        public static void BuildAll() => Run(() =>
        {
            EnsureLayers();
            EnsureAssets();
            M1Setup.BuildCore();
            BuildLab();
        });

        static void Run(Action body)
        {
            int code = 0;
            try { body(); AssetDatabase.SaveAssets(); }
            catch (Exception e) { Debug.LogError($"{Tag} 실패: {e}"); code = 1; }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        // ───────────────────────── 레이어·충돌(08 10-2)
        public static void EnsureLayers()
        {
            var tm = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset").FirstOrDefault();
            if (tm != null)
            {
                var so = new SerializedObject(tm);
                var layers = so.FindProperty("layers");
                bool changed = false;
                void Name(int i, string n)
                {
                    var p = layers.GetArrayElementAtIndex(i);
                    if (string.IsNullOrEmpty(p.stringValue)) { p.stringValue = n; changed = true; Debug.Log($"{Tag} 레이어 {i} {n}"); }
                    else if (p.stringValue != n) Debug.LogWarning($"{Tag} 레이어 {i} 에 이미 '{p.stringValue}' 가 있습니다('{n}' 자리)");
                }
                Name(Layers.Fighter, "Fighter");
                Name(Layers.Crowd, "Crowd");
                if (changed) { so.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssets(); }
            }
            // 구경꾼은 플레이어·적이 통과(링 벽 PlayerOnly 가 막음), 카메라 전용 벽은 적도 통과
            Ignore(Layers.Player, Layers.Crowd);
            Ignore(Layers.Fighter, Layers.Crowd);
            Ignore(Layers.Fighter, Layers.CamBlock);
            Ignore(Layers.Player, Layers.CamBlock);
        }

        static void Ignore(int a, int b)
        {
            if (Physics.GetIgnoreLayerCollision(a, b)) return;
            Physics.IgnoreLayerCollision(a, b, true);
            Debug.Log($"{Tag} 물리 충돌 행렬: {LayerMask.LayerToName(a)}({a}) × {LayerMask.LayerToName(b)}({b}) 끔");
        }

        // ───────────────────────── 조정값·기술 에셋(없을 때만 — 손으로 고친 값 보존)
        public static void EnsureAssets()
        {
            EnsureFolder(Root + "/Settings");
            if (AssetDatabase.LoadAssetAtPath<CombatTuning>(TuningPath) == null)
            {
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<CombatTuning>(), TuningPath);
                Debug.Log($"{Tag} 조정값 만듦: {TuningPath}");
            }
            if (AssetDatabase.LoadAssetAtPath<MoveSet>(MovesPath) == null)
            {
                var set = MoveSet.CreateDefault();
                AssetDatabase.CreateAsset(set, MovesPath);
                foreach (var m in set.All) AssetDatabase.AddObjectToAsset(m, set);
                EditorUtility.SetDirty(set);
                AssetDatabase.SaveAssets();
                Debug.Log($"{Tag} 시우 기술 만듦: {MovesPath} ({set.All.Length}개: {string.Join(", ", set.All.Select(m => $"{m.Label} {m.Startup}/{m.Active}/{m.Recovery}"))})");
            }
            if (AssetDatabase.LoadAssetAtPath<Unity.Cinemachine.CinemachineBlenderSettings>(BlendsPath) == null)
            {
                AssetDatabase.CreateAsset(CombatMode.Blends(), BlendsPath);
                Debug.Log($"{Tag} 카메라 블렌드 만듦: {BlendsPath}");
            }
        }

        public static CombatTuning Tuning => AssetDatabase.LoadAssetAtPath<CombatTuning>(TuningPath);
        public static MoveSet Moves => AssetDatabase.LoadAssetAtPath<MoveSet>(MovesPath);

        /// 프리팹 원본 플레이어에 전투 부품(M1Setup.BuildPlayerPrefab 이 부름)
        public static void AddCombatToPlayer(GameObject player)
        {
            EnsureAssets();
            var pc = CombatFactory.AddPlayer(player, Tuning, Moves);
            Debug.Log($"{Tag} 플레이어 전투 부품: Fighter(HP {pc.Me.MaxHp}) · HitReact(모델 {(pc.Me.React != null && pc.Me.React.Model != null ? pc.Me.React.Model.name : "-")}) · LockOn · PlayerCombat");
        }

        // ───────────────────────── 전투 연습장
        static Material LabMat(string name, Color c)
        {
            EnsureFolder(LabMatDir);
            string path = $"{LabMatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", 0f);
            m.SetFloat("_SpecularHighlights", 0f);
            m.SetFloat("_EnvironmentReflections", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// 허수아비 재질: 태오 툰 재질을 복사해 회색 단색(08 12장 결정 5 — 임시 적 몸)
        static Material DummyMaterial()
        {
            EnsureFolder(LabMatDir);
            var m = AssetDatabase.LoadAssetAtPath<Material>(DummyMat);
            if (m == null)
            {
                if (!AssetDatabase.CopyAsset(CharSetup.TaeoMat, DummyMat)) throw new Exception("태오 툰 재질 복사 실패: " + CharSetup.TaeoMat);
                m = AssetDatabase.LoadAssetAtPath<Material>(DummyMat);
            }
            foreach (var t in new[] { "_MainTex", "_BaseMap", "_1st_ShadeMap", "_2nd_ShadeMap" }) if (m.HasProperty(t)) m.SetTexture(t, null);
            m.SetColor("_BaseColor", new Color(0.64f, 0.65f, 0.67f));
            m.SetColor("_Color", new Color(0.64f, 0.65f, 0.67f));
            m.SetColor("_1st_ShadeColor", new Color(0.47f, 0.47f, 0.51f));
            m.SetColor("_2nd_ShadeColor", new Color(0.38f, 0.38f, 0.42f));
            EditorUtility.SetDirty(m);
            return m;
        }

        public static void BuildLab()
        {
            EnsureFolder(Root + "/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.60f, 0.57f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.914f, 0.878f, 0.800f);
            RenderSettings.fogStartDistance = 30f;
            RenderSettings.fogEndDistance = 140f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.reflectionIntensity = 0f;

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

            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(FilmVolume);
            if (profile != null)
            {
                var volGo = new GameObject("Global Volume (Film)");
                var vol = volGo.AddComponent<Volume>();
                vol.isGlobal = true;
                vol.sharedProfile = profile;
            }

            CombatLab.Build(LabMat("M_Lab_Floor", CombatLab.FloorColor), LabMat("M_Lab_Wall", CombatLab.WallColor), LabMat("M_Lab_Crowd", CombatLab.CrowdColor));

            // 시우(실제 Player 프리팹) + 카메라 + 전투 카메라 + 전환
            var mt = AssetDatabase.LoadAssetAtPath<MoveTuning>(M1Setup.MoveTuningPath);
            var ct = AssetDatabase.LoadAssetAtPath<CamTuning>(M1Setup.CamTuningPath);
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(M1Setup.InputPath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(M1Setup.PrefabPath) ?? throw new Exception("Player 프리팹이 없습니다");
            var p = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            p.transform.SetPositionAndRotation(CombatLab.PlayerStart, Quaternion.Euler(0f, CombatLab.PlayerYaw, 0f));
            var rig = new RigFactory.Rig
            {
                Player = p,
                Motor = p.GetComponent<PlayerMotor>(),
                Visual = p.transform.Find("Visual"),
                CamTarget = p.transform.Find("CamTarget"),
                Lean = p.GetComponentInChildren<BodyLean>(true),
                Input = p.GetComponent<PInput>(),
            };
            rig.Motor.KillY = -20f;
            RigFactory.BuildCamera(rig, ct, null, actions);
            RigFactory.Wire(rig);
            rig.Main.clearFlags = CameraClearFlags.SolidColor;
            rig.Main.backgroundColor = new Color(0.957f, 0.937f, 0.902f);     // 종이색 #F4EFE6(Sandbox 와 같음)
            var pc = p.GetComponent<PlayerCombat>() ?? throw new Exception("프리팹에 PlayerCombat 이 없습니다(M1Setup.BuildPlayerPrefab)");
            var cam = CombatFactory.BuildCombatCam(rig, pc, ct, Tuning, actions);
            rig.Brain.CustomBlends = AssetDatabase.LoadAssetAtPath<Unity.Cinemachine.CinemachineBlenderSettings>(BlendsPath);
            var mode = CombatFactory.AddMode(rig, pc, cam, true);
            mode.gameObject.AddComponent<CombatDebug>().Player = pc;

            // 허수아비: 태오 모델(회색) — 맞기만 함
            var dummy = SpawnDummyModel();
            var f = CombatFactory.Dummy("허수아비", CombatLab.DummyStart, CombatLab.DummyYaw, 999, dummy, 1.83f, 0.30f, Tuning);
            dummy.transform.localPosition = Vector3.zero;
            dummy.transform.localRotation = Quaternion.identity;

            // 화면 UI(조작 안내·알림·일시정지)
            var uiGo = new GameObject(M1Setup.UiName) { layer = 5 };
            var ui = uiGo.AddComponent<GameUi>();
            ui.Actions = actions;
            ui.Font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(KoreanFont.AssetPath);
            ui.Cam = rig.CamRig;

            foreach (var go in new[] { lightGo, p, rig.Main.gameObject, rig.Cam.gameObject, cam.gameObject, cam.Pivot.gameObject, mode.gameObject, f.gameObject, uiGo })
                if (go != null && go.scene != scene) SceneManager.MoveGameObjectToScene(go, scene);
            PrefabUtility.RecordPrefabInstancePropertyModifications(p.transform);
            EditorSceneManager.SaveScene(scene, LabScene);
            Debug.Log($"{Tag} 전투 연습장 저장: {LabScene} | 바닥 {CombatLab.Size}×{CombatLab.Size} · 벽 z {CombatLab.WallZ}(HeatSurface) · 구경꾼 원 반경 {CombatLab.RingRadius} · " +
                      $"시우 {CombatLab.PlayerStart} · 허수아비 {CombatLab.DummyStart}(태오 모델, 회색) · CM_Explore + CM_Combat · 시작하면 바로 전투");
        }

        static GameObject SpawnDummyModel()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(CharSetup.TaeoFbx) ?? throw new Exception("태오 모델이 없습니다");
            var go = PrefabUtility.InstantiatePrefab(model) as GameObject ?? Object.Instantiate(model);
            go.name = "Dummy_Model";
            var mat = DummyMaterial();
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var ms = r.sharedMaterials;
                for (int i = 0; i < ms.Length; i++) ms[i] = mat;
                r.sharedMaterials = ms;
                r.shadowCastingMode = ShadowCastingMode.On;
                r.receiveShadows = true;
            }
            var anim = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
            anim.avatar = CharSetup.AvatarOf(CharSetup.TaeoFbx);
            anim.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CharSetup.TaeoCtrl);
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            return go;
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
