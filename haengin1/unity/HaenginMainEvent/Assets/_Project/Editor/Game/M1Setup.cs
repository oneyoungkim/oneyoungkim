// 행인1의 메인이벤트 — M1 리그 설정 (docs/07_M1_조작_설계.md 7-9). '설정 파일 → 에디터 스크립트 → 장면' 패턴.
// batchmode:
//   -executeMethod Haengin.EditorGame.M1Setup.Build          캐릭터 임포트·애니메이터(CharSetup) + 조정값 + Player 프리팹 + Zone1 다시 만들기(리그 포함) + 빌드 목록   (-nographics 가능)
//   -executeMethod Haengin.EditorGame.M1Setup.BuildAll       위 + Sandbox 다시 만들기(시우·태오 리깅 모델)
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
        // 시우 = 리깅 FBX(Humanoid, 대기·걷기·달리기) — 2026-10-06 정적 GLB(siwoo_tripo_v1.glb, Y +90°) 대체
        public const string SiwooModel = CharSetup.SiwooFbx;
        public const string ToonMat = CharSetup.SiwooMat;
        public const string Zone1Scene = Root + "/Scenes/Zone1.unity";
        public const string SandboxScene = Root + "/Scenes/Sandbox.unity";
        const float ModelYaw = 0f;    // 리깅 FBX 는 정면이 이미 Unity +Z(Humanoid 아바타 측정: 왼다리 −X) — 예전 정적 GLB 는 +90° 였음
        public const string UiName = "UI 화면";

        static M1Setup()
        {
            Zone1Builder.BeforeSave -= OnZone1Built;
            Zone1Builder.BeforeSave += OnZone1Built;
        }

        // ───────────────────────── 진입점
        [MenuItem("Haengin/M1 다시 만들기(Zone1 + 플레이어)")]
        public static void Build() => Run(BuildCore);

        /// Build 의 본체(종료하지 않음 — CombatSetup.BuildAll 이 이어서 부름)
        public static void BuildCore()
        {
            CharSetup.Setup();
            EnsureTuning();
            BuildPlayerPrefab();
            Zone1Builder.Rebuild();          // BeforeSave → OnZone1Built 가 리그를 붙인다
            SetBuildScenes();
        }

        [MenuItem("Haengin/M1 + Sandbox 모두 다시 만들기")]
        public static void BuildAll() => Run(() =>
        {
            CharSetup.Setup();
            SandboxSetup.BuildScene();
            EnsureTuning();
            BuildPlayerPrefab();
            Zone1Builder.Rebuild();
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
        /// Prefabs/Player.prefab: 시우 리깅 FBX(툰 재질, 키 1.74, Animator + LocoAnim) + CharacterController·PlayerMotor·PInput + Visual(BodyLean) + CamTarget. 매번 다시 저장(GUID 유지).
        public static void BuildPlayerPrefab()
        {
            EnsureFolder(PrefabDir);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(SiwooModel) ?? throw new Exception("시우 모델이 없습니다: " + SiwooModel);
            var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CharSetup.SiwooCtrl) ?? throw new Exception("애니메이터 컨트롤러가 없습니다: " + CharSetup.SiwooCtrl);
            var avatar = CharSetup.AvatarOf(SiwooModel) ?? throw new Exception("시우 아바타가 없습니다(CharSetup)");
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
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = false;
            }
            var anim = inst.GetComponent<Animator>() ?? inst.AddComponent<Animator>();
            anim.avatar = avatar;
            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;                              // 이동은 PlayerMotor, 클립은 제자리
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;      // 카메라가 붙어 인물을 숨겨도(그림자만) 자세는 계속
            anim.updateMode = AnimatorUpdateMode.Normal;
            var rig = RigFactory.BuildPlayer(Vector3.zero, 0f, MT, CT, inst, Actions);
            // ModelFit(경계 상자로 키 맞추기)는 정적 모델용 — 스킨 메시의 편집 모드 경계는 클립 첫 프레임 자세(대기 = 웅크린 1.66m)라 키가 5% 커진다.
            // FBX 는 이미 미터 단위 실제 크기(바인드 자세 키 1.74m, 원점 = 두 발 사이 바닥)라 그대로 둔다.
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.Euler(0f, ModelYaw, 0f);
            inst.transform.localScale = Vector3.one;
            var skin = inst.GetComponentInChildren<SkinnedMeshRenderer>(true);
            float bindH = skin != null ? skin.sharedMesh.bounds.size.z * skin.transform.lossyScale.x : -1f;   // Blender Z-up 메시: z = 키
            var loco = inst.AddComponent<LocoAnim>();
            loco.Motor = rig.Motor;
            // M2(08 문서 10-1): 전투 부품(Fighter·HitReact·LockOn·PlayerCombat) — 탐색 중에는 꺼진 채(전투 시작 때 CombatMode 가 켬)
            CombatSetup.AddCombatToPlayer(rig.Player);
            var b = ModelFit.WorldBounds(inst.transform);
            string fit = $"배율 {inst.transform.localScale.x:F4} · 자리 {inst.transform.localPosition}";
            PrefabUtility.SaveAsPrefabAsset(rig.Player, PrefabPath, out bool ok);
            Object.DestroyImmediate(rig.Player);
            if (!ok) throw new Exception("프리팹 저장 실패: " + PrefabPath);
            Debug.Log($"{Tag} 프리팹 저장: {PrefabPath} | 모델 {SiwooModel} 바인드 자세 키 {bindH:F3}m · 편집 모드 경계(첫 프레임 자세) 높이 {b.size.y:F3}m 발 y {b.min.y:F3} · {fit} | " +
                      $"툰 재질 {(toon != null ? toon.name : "없음")} · 애니메이터 {ctrl.name}(아바타 {avatar.name}, 루트 모션 끔) + LocoAnim");
        }

        // ───────────────────────── 장면에 붙이기
        /// 지금 열린 장면(Zone1)에 리그를 붙인다: 옛 리그·키 기준 인형 지우기 → 프리팹을 spawn 에(정면 = spawn yaw) → 장면 Main Camera 에 브레인 + CM_Explore → RouteData
        public static void AttachRig(Zone1Data d, Transform zoneRoot)
        {
            var scene = zoneRoot.gameObject.scene;
            foreach (var g in scene.GetRootGameObjects())
                if (g.name == "Player" || g.name == "CM_Explore" || g.name == UiName) Object.DestroyImmediate(g);
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

            // 화면 UI(조작 안내·알림·일시정지 메뉴, TMP 한글) — 부품은 실행할 때 GameUi 가 만든다
            var uiGo = new GameObject(UiName) { layer = 5 };
            SceneManager.MoveGameObjectToScene(uiGo, scene);
            var ui = uiGo.AddComponent<GameUi>();
            ui.Actions = Actions;
            ui.Font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(KoreanFont.AssetPath);
            ui.Cam = rig.CamRig;
            if (ui.Font == null) Debug.LogWarning($"{Tag} 한글 글꼴 에셋이 없어 UI 가 기본 글꼴을 씁니다: {KoreanFont.AssetPath}");

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

            // M2(08 8장): CM_Combat · CM_Heat · CombatMode · 인카운터 Y4 · 야차 Y1
            StageSetup.AddCombatToZone1(rig, d, zoneRoot);
            // M3(09 2-1): 이야기 루트(StoryRunner·화면들) · 빛 자리 · 창문 불
            StorySetup.AddStoryToZone1(rig, zoneRoot);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"{Tag} 리그 붙임: Player @ ({feet.x:F2}, {feet.y:F2}, {feet.z:F2}) yaw {d.SpawnYaw}° (데이터 y {d.SpawnPos.y:F2}) · " +
                      $"카메라 {(cam != null ? cam.name + "(장면 것 재사용)" : "새로 만듦")} + CM_Explore · KillY {rig.Motor.KillY:F1} · 체크포인트 {d.Route.Count}개");
        }

        /// 빌드 목록: (M3) 타이틀 첫 장면 → Zone1 → 무대 St_* → Sandbox → 전투 연습장(타이틀 '연습장'). 없는 장면은 뺀다
        public static void SetBuildScenes()
        {
            var list = new[] { StorySetup.TitleScene, Zone1Scene }
                .Concat(StorySetup.StageKeys.Select(SceneLoader.StagePath))
                .Concat(new[] { SandboxScene, CombatSetup.LabScene })
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
