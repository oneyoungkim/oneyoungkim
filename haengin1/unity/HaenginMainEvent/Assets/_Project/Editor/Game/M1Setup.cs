// 행인1의 메인이벤트 — M1 리그 설정 (docs/07_M1_조작_설계.md 7-9). '설정 파일 → 에디터 스크립트 → 장면' 패턴.
// batchmode:
//   -executeMethod Haengin.EditorGame.M1Setup.Build          조정값 + Player 프리팹 + Zone1 다시 만들기(리그 포함) + 빌드 목록   (-nographics 가능)
//   -executeMethod Haengin.EditorGame.M1Setup.AddRigToZone1  이미 있는 Zone1.unity 에 리그만 다시 붙이기
// Zone1Builder.Build 만 돌려도 BeforeSave 확장 지점으로 리그가 다시 붙는다(이 클래스가 [InitializeOnLoad] 로 등록).
using System;
using System.Linq;
using Haengin.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Haengin.EditorGame
{
    [InitializeOnLoad]
    public static class M1Setup
    {
        const string Tag = "[M1Setup]";
        public const string Root = "Assets/_Project";
        public const string InputPath = Root + "/Input/HInput.inputactions";
        public const string MoveTuningPath = Root + "/Settings/MoveTuning.asset";
        public const string CamTuningPath = Root + "/Settings/CamTuning.asset";
        public const string PrefabDir = Root + "/Prefabs";
        public const string PrefabPath = PrefabDir + "/Player.prefab";
        public const string GlbStatic = Root + "/Art/Characters/Siwoo/siwoo_tripo_v1.glb";
        public const string ToonMat = Root + "/Materials/M_Siwoo_Toon.mat";
        public const string Zone1Scene = Root + "/Scenes/Zone1.unity";
        public const string SandboxScene = Root + "/Scenes/Sandbox.unity";
        const float ModelYaw = 90f;   // Tripo GLB 정면이 Unity −X → +90° 로 +Z 를 보게(SandboxSetup 과 같음)

        static M1Setup()
        {
            Zone1Builder.BeforeSave -= OnZone1Built;
            Zone1Builder.BeforeSave += OnZone1Built;
        }

        // ───────────────────────── 진입점
        [MenuItem("Haengin/M1 다시 만들기(Zone1 + 플레이어)")]
        public static void Build() => Run(() =>
        {
            EnsureTuning();
            BuildPlayerPrefab();
            Zone1Builder.Rebuild();          // BeforeSave → OnZone1Built 가 리그를 붙인다
            SetBuildScenes();
        });

        [MenuItem("Haengin/Zone1 에 플레이어만 다시 붙이기")]
        public static void AddRigToZone1() => Run(() =>
        {
            EnsureTuning();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null) BuildPlayerPrefab();
            var scene = EditorSceneManager.OpenScene(Zone1Scene, OpenSceneMode.Single);
            var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Zone1")
                       ?? throw new Exception("Zone1 장면에 'Zone1' 루트가 없습니다. Zone1Builder.Build 를 먼저 돌리세요");
            AttachRig(Zone1Data.Load(), root.transform);
            EditorSceneManager.SaveScene(scene);
            SetBuildScenes();
        });

        static void Run(Action body)
        {
            int code = 0;
            try { body(); AssetDatabase.SaveAssets(); }
            catch (Exception e) { Debug.LogError($"{Tag} 실패: {e}"); code = 1; }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        static void OnZone1Built(Zone1Data d, Transform zoneRoot)
        {
            EnsureTuning();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null) BuildPlayerPrefab();
            AttachRig(d, zoneRoot);
            SetBuildScenes();
        }

        // ───────────────────────── 조정값·입력
        /// MoveTuning·CamTuning 에셋: 없을 때만 만든다(손으로 조정한 값 보존)
        public static void EnsureTuning()
        {
            EnsureFolder(Root + "/Settings");
            if (AssetDatabase.LoadAssetAtPath<MoveTuning>(MoveTuningPath) == null)
            {
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<MoveTuning>(), MoveTuningPath);
                Debug.Log($"{Tag} 조정값 만듦: {MoveTuningPath}");
            }
            if (AssetDatabase.LoadAssetAtPath<CamTuning>(CamTuningPath) == null)
            {
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<CamTuning>(), CamTuningPath);
                Debug.Log($"{Tag} 조정값 만듦: {CamTuningPath}");
            }
            // 07 3-5: 플레이어는 카메라 전용 벽(CamBlock)을 통과
            if (!Physics.GetIgnoreLayerCollision(Layers.Player, Layers.CamBlock))
            {
                Physics.IgnoreLayerCollision(Layers.Player, Layers.CamBlock, true);
                Debug.Log($"{Tag} 물리 충돌 행렬: Player × CamBlock 끔");
            }
        }

        static MoveTuning MT => AssetDatabase.LoadAssetAtPath<MoveTuning>(MoveTuningPath);
        static CamTuning CT => AssetDatabase.LoadAssetAtPath<CamTuning>(CamTuningPath);

        static InputActionAsset Actions =>
            AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath) ?? throw new Exception("입력 에셋이 없습니다: " + InputPath);

        // ───────────────────────── 프리팹
        /// Prefabs/Player.prefab: 시우 정적 GLB(툰 재질, 키 1.74) + CharacterController·PlayerMotor·PInput + Visual(BodyLean) + CamTarget. 매번 다시 저장(GUID 유지).
        public static void BuildPlayerPrefab()
        {
            EnsureFolder(PrefabDir);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(GlbStatic) ?? throw new Exception("시우 GLB 가 없습니다: " + GlbStatic);
            var toon = AssetDatabase.LoadAssetAtPath<Material>(ToonMat);
            var inst = (PrefabUtility.InstantiatePrefab(model) as GameObject) ?? Object.Instantiate(model);
            inst.name = "Siwoo_Model";
            inst.transform.localRotation = Quaternion.Euler(0f, ModelYaw, 0f);
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                if (toon != null)
                {
                    var ms = r.sharedMaterials;
                    for (int i = 0; i < ms.Length; i++) ms[i] = toon;
                    r.sharedMaterials = ms;
                }
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
            }
            var rig = RigFactory.BuildPlayer(Vector3.zero, 0f, MT, CT, inst, Actions);
            var b = ModelFit.WorldBounds(inst.transform);
            PrefabUtility.SaveAsPrefabAsset(rig.Player, PrefabPath, out bool ok);
            Object.DestroyImmediate(rig.Player);
            if (!ok) throw new Exception("프리팹 저장 실패: " + PrefabPath);
            Debug.Log($"{Tag} 프리팹 저장: {PrefabPath} | 모델 키 {b.size.y:F3}m · 발 y {b.min.y:F3} · 폭 {b.size.x:F2}×{b.size.z:F2} | 툰 재질 {(toon != null ? "있음" : "없음")}");
        }

        // ───────────────────────── 장면에 붙이기
        /// 지금 열린 장면(Zone1)에 리그를 붙인다: 옛 리그·키 기준 인형 지우기 → 프리팹을 spawn 에(정면 = spawn yaw) → 장면 Main Camera 에 브레인 + CM_Explore → RouteData
        public static void AttachRig(Zone1Data d, Transform zoneRoot)
        {
            var scene = zoneRoot.gameObject.scene;
            foreach (var g in scene.GetRootGameObjects())
                if (g.name == "Player" || g.name == "CM_Explore") Object.DestroyImmediate(g);
            var spawn = zoneRoot.Find("Spawn");
            if (spawn != null)
                foreach (Transform c in spawn.Cast<Transform>().ToList())
                    if (c.name.StartsWith("ScaleRef_Siwoo")) Object.DestroyImmediate(c.gameObject);

            var mt = MT; var ct = CT;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) ?? throw new Exception("프리팹이 없습니다: " + PrefabPath);

            // 발 높이 = spawn 아래 걷는 면(Ground)
            Physics.SyncTransforms();
            var feet = d.SpawnPos;
            if (Physics.Raycast(feet + Vector3.up * 2f, Vector3.down, out var hit, 6f, 1 << Layers.Ground, QueryTriggerInteraction.Ignore))
                feet.y = hit.point.y;

            var p = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            p.transform.SetPositionAndRotation(feet, Quaternion.Euler(0f, d.SpawnYaw, 0f));
            var rig = new RigFactory.Rig
            {
                Player = p,
                Motor = p.GetComponent<PlayerMotor>(),
                Visual = p.transform.Find("Visual"),
                CamTarget = p.transform.Find("CamTarget"),
                Lean = p.GetComponentInChildren<BodyLean>(true),
                Input = p.GetComponent<PInput>(),
            };
            if (rig.Motor == null || rig.CamTarget == null) throw new Exception("프리팹 구조가 예상과 다릅니다(PlayerMotor·CamTarget)");
            rig.Motor.KillY = d.BoundsMin.y - 10f;

            var cam = scene.GetRootGameObjects().Select(g => g.GetComponent<Camera>()).FirstOrDefault(c => c != null && c.CompareTag("MainCamera"));
            RigFactory.BuildCamera(rig, ct, cam, Actions);
            RigFactory.Wire(rig);
            if (rig.Cam.gameObject.scene != scene) SceneManager.MoveGameObjectToScene(rig.Cam.gameObject, scene);
            if (rig.Main.gameObject.scene != scene) SceneManager.MoveGameObjectToScene(rig.Main.gameObject, scene);

            PrefabUtility.RecordPrefabInstancePropertyModifications(rig.Motor);
            if (rig.Input != null) PrefabUtility.RecordPrefabInstancePropertyModifications(rig.Input);
            if (rig.Lean != null) PrefabUtility.RecordPrefabInstancePropertyModifications(rig.Lean);
            PrefabUtility.RecordPrefabInstancePropertyModifications(p.transform);

            // 경로 데이터(테스트·나중의 체크포인트 진행 코드용)
            var routeGo = zoneRoot.Find("Route")?.gameObject;
            if (routeGo != null)
            {
                var rd = routeGo.GetComponent<RouteData>() ?? routeGo.AddComponent<RouteData>();
                rd.Names = d.Route.Select(c => c.Name).ToArray();
                rd.Points = d.Route.Select(c => c.Pos).ToArray();
                rd.Radii = d.Route.Select(c => c.Radius).ToArray();
                rd.SpawnPos = feet;
                rd.SpawnYaw = d.SpawnYaw;
                rd.BoundsMin = d.BoundsMin;
                rd.BoundsMax = d.BoundsMax;
            }
            else Debug.LogWarning($"{Tag} Zone1/Route 가 없어 RouteData 를 붙이지 못했습니다");

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"{Tag} 리그 붙임: Player @ ({feet.x:F2}, {feet.y:F2}, {feet.z:F2}) yaw {d.SpawnYaw}° (데이터 y {d.SpawnPos.y:F2}) · " +
                      $"카메라 {(cam != null ? cam.name + "(장면 것 재사용)" : "새로 만듦")} + CM_Explore · KillY {rig.Motor.KillY:F1} · 체크포인트 {d.Route.Count}개");
        }

        /// 빌드 목록: Zone1 첫 장면, Sandbox 두 번째
        public static void SetBuildScenes()
        {
            var list = new[] { Zone1Scene, SandboxScene }
                .Where(path => AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
                .Select(path => new EditorBuildSettingsScene(path, true)).ToArray();
            EditorBuildSettings.scenes = list;
            Debug.Log($"{Tag} 빌드 목록: {string.Join(", ", list.Select(s => s.path))}");
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
