// 행인1의 메인이벤트 — M2 전투 설정 (docs/08_M2_전투_설계.md 10장·11장 0단계)
// batchmode:
//   -executeMethod Haengin.EditorGame.CombatSetup.Build     레이어 12 Fighter·13 Crowd + 충돌 행렬 + CombatTuning·SiwooMoves 에셋(없을 때만)
//                                                          + Player 프리팹에 전투 부품 + Scenes/CombatLab.unity (시험장: 평지·벽·구경꾼 원·허수아비)
//   -executeMethod Haengin.EditorGame.CombatSetup.BuildAll  위 + M1 다시 만들기(Zone1 — CM_Explore 에 흔들림 확장)
using System;
using System.Collections.Generic;
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
            CharSetup.Setup();          // 시우 FBX(손 셰이프 키) 다시 가져오기
            ClipSetup.Setup();          // 9단계: 적 모델 · 전투 클립 · 측정표 · 애니메이터
            M1Setup.EnsureTuning();
            M1Setup.BuildPlayerPrefab();
            EnemyPrefabs();
            BuildLab();
        });

        [MenuItem("Haengin/M2 전투 설정 + M1 다시 만들기")]
        public static void BuildAll() => Run(() =>
        {
            EnsureLayers();
            EnsureAssets();
            CharSetup.Setup();
            ClipSetup.Setup();
            M1Setup.BuildCore();
            EnemyPrefabs();
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
            FxSetup.Ensure();
        }

        public static CombatTuning Tuning => AssetDatabase.LoadAssetAtPath<CombatTuning>(TuningPath);
        public static MoveSet Moves => AssetDatabase.LoadAssetAtPath<MoveSet>(MovesPath);

        /// 프리팹 원본 플레이어에 전투 부품(M1Setup.BuildPlayerPrefab 이 부름)
        public static void AddCombatToPlayer(GameObject player)
        {
            EnsureAssets();
            var pc = CombatFactory.AddPlayer(player, Tuning, Moves);
            var anim = player.GetComponentInChildren<Animator>(true);
            var table = AssetDatabase.LoadAssetAtPath<ClipTable>(ClipSetup.TablePath);
            string extra = "";
            if (anim != null && table != null)
            {
                var fa = anim.GetComponent<FighterAnim>() ?? anim.gameObject.AddComponent<FighterAnim>();
                fa.Clips = table;
                if (anim.GetComponent<HandShape>() == null) anim.gameObject.AddComponent<HandShape>();
                extra = " · FighterAnim(전투 클립) · HandShape(주먹·잡기 손)";
            }
            var kit = AssetDatabase.LoadAssetAtPath<FxKit>(FxSetup.KitPath);
            CombatFactory.AddUi(player, kit, Tuning);
            extra += $" · HeatAction · CombatUi(CombatFx·CombatHud, 묶음 {(kit != null ? "있음" : "없음")})";
            Debug.Log($"{Tag} 플레이어 전투 부품: Fighter(HP {pc.Me.MaxHp}) · HitReact(모델 {(pc.Me.React != null && pc.Me.React.Model != null ? pc.Me.React.Model.name : "-")}) · LockOn · PlayerCombat{extra}");
        }

        // ───────────────────────── 적 정의·프리팹(9단계 — 정식 모델 + Enemy.controller)
        public const string EnemyPrefabDir = Root + "/Prefabs";
        public static string EnemyPrefab(string n) => $"{EnemyPrefabDir}/Enemy_{n}.prefab";
        public static string EnemyDefPath(EnemyDef.Kind k) => $"{Root}/Settings/Enemy_{k}.asset";

        /// 적 정의 에셋(없을 때만): 4-4 표, 기술은 하위 에셋
        public static EnemyDef EnemyDefAsset(EnemyDef.Kind k)
        {
            var a = AssetDatabase.LoadAssetAtPath<EnemyDef>(EnemyDefPath(k));
            if (a != null) { SyncClips(a, k); return a; }
            var d = EnemyLib.Make(k);
            AssetDatabase.CreateAsset(d, EnemyDefPath(k));
            var subs = new List<MoveDef>();
            void Add(MoveDef m) { if (m == null || subs.Contains(m)) return; subs.Add(m); AssetDatabase.AddObjectToAsset(m, d); if (m.Followup != null) Add(m.Followup); }
            foreach (var m in d.Moves) Add(m);
            Add(d.Far); Add(d.Counter);
            EditorUtility.SetDirty(d);
            AssetDatabase.SaveAssets();
            Debug.Log($"{Tag} 적 정의 만듦: {EnemyDefPath(k)} ({d.Label} HP {d.Hp} · 기술 {string.Join(", ", subs.Select(m => m.Label))})");
            return d;
        }

        /// 적마다 대기(Move 블렌드 트리의 Idle)만 그 모델 자기 대기 클립으로(10-2 Override Controller). 같은 Idle_3 라도 깐족이 것을 덩치에 리타깃하면 팔이 등 뒤로 돌아감
        static RuntimeAnimatorController EnemyOverride(string n, RuntimeAnimatorController ctrl)
        {
            if (ctrl == null) return null;
            var baseIdle = CharSetup.Clip(ClipSetup.EnemyFbx(ClipSetup.Enemies[0]), "Idle");
            var own = CharSetup.Clip(ClipSetup.EnemyFbx(n), "Idle");
            if (baseIdle == null || own == null || baseIdle == own) return ctrl;
            string path = $"{ClipSetup.AnimDir}/Enemy_{n}.overrideController";
            var oc = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
            if (oc == null) { oc = new AnimatorOverrideController(ctrl); AssetDatabase.CreateAsset(oc, path); }
            oc.runtimeAnimatorController = ctrl;
            oc[baseIdle] = own;
            EditorUtility.SetDirty(oc);
            Debug.Log($"{Tag} 적 {n}: 대기 클립을 자기 Idle 로(Override {path})");
            return oc;
        }

        /// 이미 있는 적 정의의 기술 하위 에셋에 '클립 연결' 값만 표(EnemyLib)대로 다시 넣는다(ClipId · 젖힘 · 옆 넉백 — 클립이 바뀌면 같이 바뀌는 것).
        /// 피해·프레임 같은 손으로 고칠 수 있는 값은 그대로 둔다. 2026-10-06: 냉장고 큰 휘두르기 193 → 128
        static void SyncClips(EnemyDef d, EnemyDef.Kind k)
        {
            var fresh = EnemyLib.Make(k);
            var lib = new Dictionary<string, MoveDef>();
            void Add(MoveDef m) { if (m == null || lib.ContainsKey(m.name)) return; lib[m.name] = m; Add(m.Followup); }
            foreach (var m in fresh.Moves) Add(m);
            Add(fresh.Far); Add(fresh.Counter);
            foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(EnemyDefPath(k)).OfType<MoveDef>())
            {
                if (!lib.TryGetValue(sub.name, out var f)) continue;
                if (sub.ClipId == f.ClipId && sub.Flinch == f.Flinch && sub.KnockSide == f.KnockSide) continue;
                Debug.Log($"{Tag} 적 기술 클립 연결 고침 {d.Label}/{sub.Label}: 클립 {sub.ClipId} → {f.ClipId} · 젖힘 {sub.Flinch} → {f.Flinch} · 옆 넉백 {sub.KnockSide} → {f.KnockSide}");
                sub.ClipId = f.ClipId; sub.Flinch = f.Flinch; sub.KnockSide = f.KnockSide; sub.ClipRate = f.ClipRate;
                EditorUtility.SetDirty(sub);
            }
        }

        public static void EnemyPrefabs()
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ClipSetup.EnemyCtrl);
            var table = AssetDatabase.LoadAssetAtPath<ClipTable>(ClipSetup.TablePath);
            foreach (EnemyDef.Kind k in new[] { EnemyDef.Kind.Kkanjok, EnemyDef.Kind.Seokdal, EnemyDef.Kind.Naengjanggo }) EnemyDefAsset(k);
            foreach (var n in ClipSetup.Enemies)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(ClipSetup.EnemyFbx(n));
                if (model == null) continue;
                var go = (PrefabUtility.InstantiatePrefab(model) as GameObject) ?? Object.Instantiate(model);
                go.name = "Enemy_" + n;
                var mat = AssetDatabase.LoadAssetAtPath<Material>(ClipSetup.EnemyMat(n));
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    if (mat != null) { var ms = r.sharedMaterials; for (int i = 0; i < ms.Length; i++) ms[i] = mat; r.sharedMaterials = ms; }
                    r.shadowCastingMode = ShadowCastingMode.On;
                    r.receiveShadows = true;
                    if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = false;
                }
                var anim = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
                anim.avatar = CharSetup.AvatarOf(ClipSetup.EnemyFbx(n));
                anim.runtimeAnimatorController = EnemyOverride(n, ctrl);
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var fa = go.AddComponent<FighterAnim>();
                fa.Clips = table;
                go.AddComponent<HandShape>();
                PrefabUtility.SaveAsPrefabAsset(go, EnemyPrefab(n), out bool ok);
                string av = anim.avatar != null ? anim.avatar.name : "-", cn = anim.runtimeAnimatorController != null ? anim.runtimeAnimatorController.name : "-";
                Object.DestroyImmediate(go);
                Debug.Log($"{Tag} 적 프리팹 {EnemyPrefab(n)} {(ok ? "저장" : "실패")} · 아바타 {av} · 컨트롤러 {cn}");
            }
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
            var dbg = mode.gameObject.AddComponent<CombatDebug>();
            dbg.Player = pc;
            dbg.Tuning = Tuning;
            dbg.Defs = new[] { EnemyDefAsset(EnemyDef.Kind.Kkanjok), EnemyDefAsset(EnemyDef.Kind.Seokdal), EnemyDefAsset(EnemyDef.Kind.Naengjanggo) };
            dbg.Models = new[] { "Kkanjok", "Seokdal", "Naengjanggo" }.Select(n => AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefab(n))).ToArray();

            // 허수아비(맞기만 함): 9단계부터 석 달 정식 모델(그 전에는 태오 모델 회색)
            var dummyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefab("Seokdal"));
            var dummy = dummyPrefab != null ? (GameObject)PrefabUtility.InstantiatePrefab(dummyPrefab, scene) : SpawnDummyModel();
            var f = CombatFactory.Dummy("허수아비", CombatLab.DummyStart, CombatLab.DummyYaw, 999, dummy, 1.77f, 0.30f, Tuning);
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
                      $"시우 {CombatLab.PlayerStart} · 허수아비 {CombatLab.DummyStart}(석 달 모델) · CM_Explore + CM_Combat · 시작하면 바로 전투 · F5 1:3 소환");
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
