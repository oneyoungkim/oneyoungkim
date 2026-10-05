// 행인1의 메인이벤트 — 무대(인카운터 Y4 · 야차 Y1) 와 구경꾼 (docs/08_M2_전투_설계.md 8장, 11장 13·14단계)
// ① 구경꾼 프리팹 Prefabs/Crowd_<이름>.prefab: 적 모델 + 회색 툰 재질 3가지(캡슐 실루엣 대신 — 11-4) + 적 애니메이터(대기·걷기·260 밀기) + CrowdFigure
// ② 심판 형 프리팹 Prefabs/Referee.prefab(태오 모델 짙은 회색)
// ③ 무대 정의 Settings/Encounter_Y4.asset · Settings/Yacha_Y1.asset(없을 때만 — 손으로 고친 값 보존, 프리팹 연결은 매번)
// ④ Zone1(M1Setup.AttachRig 끝에서): CM_Combat + CM_Heat + CombatMode(탐색으로 시작) + 인카운터 Y4 + 야차 Y1 + 주차장 벽에 HeatSurface
// ⑤ 전투 연습장 구경꾼 캡슐 → 구경꾼 프리팹
using System;
using System.Collections.Generic;
using System.Linq;
using Haengin.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Haengin.EditorGame
{
    public static class StageSetup
    {
        const string Tag = "[StageSetup]";
        public const string Root = "Assets/_Project";
        public const string EncounterPath = Root + "/Settings/Encounter_Y4.asset";
        public const string YachaPath = Root + "/Settings/Yacha_Y1.asset";
        public const string RefereePrefab = Root + "/Prefabs/Referee.prefab";
        public static string CrowdPrefab(string n) => $"{Root}/Prefabs/Crowd_{n}.prefab";
        static readonly Color[] Greys = { new Color(0.58f, 0.58f, 0.61f), new Color(0.46f, 0.46f, 0.50f), new Color(0.68f, 0.67f, 0.66f) };

        // ───────────────────────── ① 구경꾼 · ② 심판 형
        static Material GreyMat(int i)
        {
            string path = $"{CombatSetup.LabMatDir}/M_Crowd_{i + 1}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                if (!AssetDatabase.CopyAsset(CombatSetup.DummyMat, path)) throw new Exception("회색 재질 복사 실패: " + CombatSetup.DummyMat);
                m = AssetDatabase.LoadAssetAtPath<Material>(path);
            }
            var c = Greys[i % Greys.Length];
            m.SetColor("_BaseColor", c); m.SetColor("_Color", c);
            m.SetColor("_1st_ShadeColor", c * 0.74f); m.SetColor("_2nd_ShadeColor", c * 0.6f);
            EditorUtility.SetDirty(m);
            return m;
        }

        public static void CrowdPrefabs()
        {
            var names = ClipSetup.Enemies;
            for (int k = 0; k < names.Length; k++)
            {
                var n = names[k];
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(ClipSetup.EnemyFbx(n));
                if (model == null) continue;
                var go = (PrefabUtility.InstantiatePrefab(model) as GameObject) ?? Object.Instantiate(model);
                go.name = "Crowd_" + n;
                var mat = GreyMat(k % 3);
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    var ms = r.sharedMaterials; for (int i = 0; i < ms.Length; i++) ms[i] = mat; r.sharedMaterials = ms;
                    r.shadowCastingMode = ShadowCastingMode.On;
                    if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = false;
                }
                foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                RigLayer(go, Layers.Crowd);
                var anim = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
                anim.avatar = CharSetup.AvatarOf(ClipSetup.EnemyFbx(n));
                var oc = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>($"{ClipSetup.AnimDir}/Enemy_{n}.overrideController");
                anim.runtimeAnimatorController = oc != null ? oc : AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ClipSetup.EnemyCtrl);
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                var cf = go.AddComponent<CrowdFigure>();
                cf.Anim = anim;
                PrefabUtility.SaveAsPrefabAsset(go, CrowdPrefab(n), out bool ok);
                Object.DestroyImmediate(go);
                Debug.Log($"{Tag} 구경꾼 프리팹 {CrowdPrefab(n)} {(ok ? "저장" : "실패")} · 회색 {k % 3 + 1}");
            }
            // 심판 형: 태오 모델(183cm) 짙은 회색
            var taeo = AssetDatabase.LoadAssetAtPath<GameObject>(CharSetup.TaeoFbx);
            if (taeo != null)
            {
                var go = (PrefabUtility.InstantiatePrefab(taeo) as GameObject) ?? Object.Instantiate(taeo);
                go.name = "Referee";
                var mat = GreyMat(1);
                foreach (var r in go.GetComponentsInChildren<Renderer>(true)) { var ms = r.sharedMaterials; for (int i = 0; i < ms.Length; i++) ms[i] = mat; r.sharedMaterials = ms; }
                foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                var anim = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
                anim.avatar = CharSetup.AvatarOf(CharSetup.TaeoFbx);
                anim.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CharSetup.TaeoCtrl);
                anim.applyRootMotion = false;
                PrefabUtility.SaveAsPrefabAsset(go, RefereePrefab, out bool ok);
                Object.DestroyImmediate(go);
                Debug.Log($"{Tag} 심판 형 프리팹 {RefereePrefab} {(ok ? "저장" : "실패")}");
            }
        }

        static void RigLayer(GameObject go, int layer) { foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer; }

        static GameObject[] CrowdSet() => ClipSetup.Enemies.Select(n => AssetDatabase.LoadAssetAtPath<GameObject>(CrowdPrefab(n))).Where(g => g != null).ToArray();

        // ───────────────────────── ③ 무대 정의
        public static EncounterDef EncounterAsset(Zone1Data d)
        {
            var e = AssetDatabase.LoadAssetAtPath<EncounterDef>(EncounterPath);
            bool fresh = e == null;
            if (fresh) { e = ScriptableObject.CreateInstance<EncounterDef>(); AssetDatabase.CreateAsset(e, EncounterPath); }
            var pad = d?.Pads.FirstOrDefault(p => p.Name.Contains("후문 뒷골목 주차장"));
            if (pad != null) { e.Center = pad.Center; e.Yaw = pad.Yaw; e.Size = pad.Size; }
            if (fresh)
            {
                e.Title = "Y4 후문 뒷골목 주차장";
                e.Foes = new[]
                {
                    new EncounterDef.Foe { Local = new Vector2(3.6f, -3.4f) },
                    new EncounterDef.Foe { Local = new Vector2(5.2f, -3.0f) },
                    new EncounterDef.Foe { Local = new Vector2(6.4f, -4.2f) },
                };
                // 구경꾼: 진입로 3명 + 북쪽 인도 3명(08 8-1)
                e.Crowd = new[] { new Vector2(-2.3f, 7.0f), new Vector2(2.4f, 7.2f), new Vector2(-1.0f, 8.4f), new Vector2(-6.0f, 7.6f), new Vector2(-4.6f, 8.0f), new Vector2(5.2f, 7.7f) };
                e.CrowdLineA = new Vector2(-3.0f, 6.6f); e.CrowdLineB = new Vector2(3.0f, 6.6f);
                // 경계 벽(전투 동안만): 북쪽 진입로 입구 6m · 서쪽 계단 입구
                e.Walls = new[]
                {
                    new EncounterDef.Box { Local = new Vector2(0f, 6.25f), Size = new Vector2(6.6f, 0.4f) },
                    new EncounterDef.Box { Local = new Vector2(-9.25f, 0f), Size = new Vector2(0.4f, 3.4f) },
                };
                e.RetryLocal = new Vector2(0.6f, 7.0f); e.RetryYaw = 180f;
            }
            var kinds = new[] { EnemyDef.Kind.Kkanjok, EnemyDef.Kind.Seokdal, EnemyDef.Kind.Naengjanggo };
            var models = new[] { "Kkanjok", "Seokdal", "Naengjanggo" };
            var foes = e.Foes;
            for (int i = 0; i < foes.Length && i < 3; i++)
            {
                foes[i].Def = CombatSetup.EnemyDefAsset(kinds[i]);
                foes[i].Model = AssetDatabase.LoadAssetAtPath<GameObject>(CombatSetup.EnemyPrefab(models[i]));
            }
            e.Foes = foes;
            e.CrowdModels = CrowdSet();
            EditorUtility.SetDirty(e);
            return e;
        }

        public static YachaDef YachaAsset()
        {
            var y = AssetDatabase.LoadAssetAtPath<YachaDef>(YachaPath);
            if (y == null) { y = ScriptableObject.CreateInstance<YachaDef>(); AssetDatabase.CreateAsset(y, YachaPath); }
            y.Foe = CombatSetup.EnemyDefAsset(EnemyDef.Kind.Scrum);
            y.FoeModel = AssetDatabase.LoadAssetAtPath<GameObject>(CombatSetup.EnemyPrefab("Scrum"));
            y.CrowdModels = CrowdSet();
            y.RefereeModel = AssetDatabase.LoadAssetAtPath<GameObject>(RefereePrefab);
            EditorUtility.SetDirty(y);
            return y;
        }

        // ───────────────────────── ④ Zone1
        /// M1Setup.AttachRig 끝에서: 전투 카메라·전환 + 인카운터 Y4 + 야차 Y1(장면은 Zone1 이 열린 상태)
        public static void AddCombatToZone1(RigFactory.Rig rig, Zone1Data d, Transform zoneRoot)
        {
            var scene = zoneRoot.gameObject.scene;
            foreach (var g in scene.GetRootGameObjects())
                if (g.name == CombatFactory.CombatCamName || g.name == CombatFactory.PivotName || g.name == CombatFactory.HeatCamName || g.name == "Combat" || g.name == "Stages")
                    Object.DestroyImmediate(g);
            var pc = rig.Player.GetComponent<PlayerCombat>();
            if (pc == null) { Debug.LogWarning($"{Tag} Zone1: 프리팹에 PlayerCombat 이 없어 전투를 붙이지 않음"); return; }
            var t = CombatSetup.Tuning;
            var cam = CombatFactory.BuildCombatCam(rig, pc, AssetDatabase.LoadAssetAtPath<CamTuning>(M1Setup.CamTuningPath), t, rig.Input != null ? rig.Input.Actions : null);
            var blends = AssetDatabase.LoadAssetAtPath<Unity.Cinemachine.CinemachineBlenderSettings>(CombatSetup.BlendsPath);
            if (blends != null && rig.Brain != null) rig.Brain.CustomBlends = blends;
            var mode = CombatFactory.AddMode(rig, pc, cam, false);

            var stages = new GameObject("Stages");
            var enc = new GameObject("Encounter_Y4").AddComponent<Encounter>();
            enc.transform.SetParent(stages.transform, false);
            enc.Def = EncounterAsset(d);
            enc.Player = pc; enc.Mode = mode; enc.Tuning = t;
            var ya = new GameObject("Yacha_Y1").AddComponent<Yacha>();
            ya.transform.SetParent(stages.transform, false);
            ya.Def = YachaAsset();
            ya.Player = pc; ya.Mode = mode; ya.Tuning = t;

            int tagged = TagHeatSurfaces(zoneRoot, enc.Def);
            foreach (var go in new[] { cam.gameObject, cam.Pivot.gameObject, mode.gameObject, stages })
                if (go.scene != scene) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
            var heat = Object.FindObjectsByType<HeatCam>(FindObjectsSortMode.None).FirstOrDefault(h => h.gameObject.scene == scene || h.gameObject.scene.name == null);
            if (heat != null && heat.gameObject.scene != scene) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(heat.gameObject, scene);
            Debug.Log($"{Tag} Zone1 전투: CM_Combat · CM_Heat · CombatMode(탐색으로 시작) · 인카운터 {enc.Def.Title}(가운데 {enc.Def.Center}, yaw {enc.Def.Yaw:F2}) · " +
                      $"야차 {ya.Def.Title} · HeatSurface {tagged}곳");
        }

        /// 주차장 남·동 옹벽 · 자판기 · 북쪽 난간 · 서쪽 상가 뒷벽(08 8-1)
        static int TagHeatSurfaces(Transform zoneRoot, EncounterDef e)
        {
            int n = 0;
            string[] names = { "주차장 옹벽(남)", "주차장 옹벽(동)", "주차장 자판기", "주차장 난간" };
            foreach (var tr in zoneRoot.GetComponentsInChildren<Transform>(true))
            {
                bool hit = names.Contains(tr.name);
                if (!hit && tr.gameObject.layer == Layers.Wall && tr.GetComponent<BoxCollider>() != null)
                {
                    // 서쪽 가장자리(로컬 x = −9) 1.5m 안의 벽(상가 뒷벽)
                    var b = tr.GetComponent<BoxCollider>().bounds;
                    var west = e.ToWorld(new Vector2(-e.Size.x * 0.5f, 0f));
                    var cp = b.ClosestPoint(new Vector3(west.x, b.center.y, west.z));
                    var l = e.ToLocal(cp);
                    hit = l.x < -e.Size.x * 0.5f + 0.3f && l.x > -e.Size.x * 0.5f - 1.5f && Mathf.Abs(l.y) < e.Size.y * 0.5f;
                }
                if (!hit || tr.GetComponent<HeatSurface>() != null) continue;
                tr.gameObject.AddComponent<HeatSurface>();
                n++;
            }
            return n;
        }

        // ───────────────────────── ⑤ 연습장 구경꾼
        public static void ReplaceLabCrowd(GameObject labRoot)
        {
            var ring = labRoot.GetComponentInChildren<CrowdRing>(true);
            if (ring == null) return;
            var set = CrowdSet();
            if (set.Length == 0) return;
            var caps = ring.transform.Cast<Transform>().Where(c => c.name.StartsWith("Crowd_")).ToList();
            for (int i = 0; i < caps.Count; i++)
            {
                var c = caps[i];
                var p = c.position; p.y = 0f;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(set[i % set.Length]);
                go.transform.SetParent(ring.transform, false);
                go.transform.SetPositionAndRotation(p, Quaternion.LookRotation(HitResolver.Flat(ring.transform.position - p)));
                Object.DestroyImmediate(c.gameObject);
            }
            Debug.Log($"{Tag} 연습장 구경꾼 {caps.Count}명 → 사람 모델(회색)");
        }
    }
}
