// 행인1의 메인이벤트 — 1화 배역(docs/09 4-1 '데모 등장 인물' 중 1화에 나오는 사람, 2026-10-25 대표 "1화만은 제대로")
// 재료: concept/art/3d/m3/anim/<이름>_idle243.glb(이미 뽑아 둔 Tripo + 리깅 + 대기 — 크레딧 0) → tools/glb2fbx.py(메시 + 손 셰이프 키) →
//   Art/Characters/Cast/<키>.fbx · <키>Tex.jpg(GLB 안 JPEG 그대로)
// 동작: Anim/Cast.controller = Idle(사람마다 자기 대기로 덮어씀) · Sit(동작 33 앉기) · TalkOpen(313 두 손 펴고 말하기) · TalkHip(309 허리에 손 얹고 말하기)
//        · SitClap(354 앉아 박수) · SitFist(304 앉아 주먹 흔들기) — Art/Clips/M3/*.fbx(대리 메시)
// 무대(StorySetup.Stages 가 무대를 다시 만들 때 Dress 를 부름): 국밥집 = 엄마(캡슐 대신)·가마솥 · 시우네 = 하늘(앉음)·식탁·아빠 사진·망치 ·
//   교실 = 칠판·교탁·책상 4×3·도경민(교탁)·태오(창가)·민재·건우·임찬(깐족이 몸)·반 친구 9(남·여 학생 몸, 머리색·옷 색 바꿈)·뒷문 칸막이(스폰 'BackDoor')
// 배우 이름(GameObject 이름) = 대사 화자 이름 — CutCtx.Actor 가 무대 아래에서 이름으로 찾는다(카메라·표정·손).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Haengin.EditorTools;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Haengin.EditorGame
{
    public static class CastSetup
    {
        const string Tag = "[CastSetup]";
        const string Root = "Assets/_Project";
        public const string CastDir = Root + "/Art/Characters/Cast";
        public const string ClipDir = Root + "/Art/Clips/M3";
        public const string CastCtrl = Root + "/Anim/Cast.controller";
        public const string PrefabDir = Root + "/Prefabs/Cast";
        public static readonly (string key, string name)[] Cast =
        {
            ("Mom", "엄마"), ("Haneul", "하늘"), ("Dokyungmin", "도경민"), ("Minjae", "민재"), ("Geonwoo", "건우"),
            ("StudentM", "남학생"), ("StudentF", "여학생"), ("Gwak", "곽반장"),
        };
        /// 동작(파일 · 상태 · 처음·끝 프레임(−1 = 전부)). 2026-10-25 바른 앉기: Sit = 364 Sit_on_Chair_Arms_Crossed 끝 자세(팔짱, 숙임 3.8°) ·
        /// SitTalk = 307 Sitting_Answering_Questions(손짓, 숙임 6.4°) · SitFloor = 363 Sit_Cross_Legged_on_Floor 끝 자세(좌식 밥상) — 예전 Sit = 33 Chair_Sit_Idle_M(숙임 34°, 책상에 엎드린 듯)
        static readonly (string file, string state, int first, int last)[] Clips =
        {
            ("SitUp", "Sit", 46, 57), ("SitTalk", "SitTalk", -1, -1), ("SitFloor", "SitFloor", 45, 60),
            ("TalkOpen", "TalkOpen", -1, -1), ("TalkHip", "TalkHip", -1, -1), ("SitClap", "SitClap", -1, -1), ("SitFist", "SitFist", -1, -1),
        };
        public static readonly List<string> Notes = new List<string>();

        public static string Fbx(string k) => $"{CastDir}/{k}.fbx";
        public static string Tex(string k) => $"{CastDir}/{k}Tex.jpg";
        public static string Mat(string k) => $"{Root}/Materials/M_Cast_{k}_Toon.mat";
        public static string Prefab(string k) => $"{PrefabDir}/{k}.prefab";
        public static bool Ready => AssetDatabase.LoadAssetAtPath<GameObject>(Prefab("Mom")) != null;

        public static void Build()
        {
            int code = 0;
            try { Assets(); AssetDatabase.SaveAssets(); }
            catch (Exception e) { Debug.LogError($"{Tag} 실패: {e}"); code = 1; }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        public static void Assets()
        {
            Notes.Clear();
            // 동작 클립
            foreach (var (file, state, first, last) in Clips)
            {
                string f = $"{ClipDir}/{file}.fbx";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(f) == null) { Notes.Add($"동작 없음: {f}"); continue; }
                if (!AssetDatabase.LoadAllAssetsAtPath(f).OfType<AnimationClip>().Any(c => c.name == state)) CharSetup.Human(f, state, first, last, false, null);
            }
            // 모델
            foreach (var (k, n) in Cast)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(Fbx(k)) == null) { Notes.Add($"모델 없음: {Fbx(k)}"); continue; }
                CharSetup.Texture(Tex(k));
                CharSetup.Human(Fbx(k), "Idle", -1, -1, true, null);
                CharSetup.Ground(Fbx(k), "Idle", Fbx(k));
                CharSetup.Toon(CharSetup.Load<GameObject>(Fbx(k)), Mat(k), Tex(k));
            }
            // QA Q04(2026-10-26): 하늘 양반다리(SitFloor = 363 끝 자세)가 원본 루트 높이 그대로 구워져 엉덩이가 바닥 위 0.83m — 방석 위 공중에 앉아 있었음
            //   → 하늘 아바타로 재생해 메시 가장 낮은 점(접은 발)을 루트 높이(배우 자리 = 방석 위)로
            string sitFloor = $"{ClipDir}/SitFloor.fbx";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(sitFloor) != null && AssetDatabase.LoadAssetAtPath<GameObject>(Fbx("Haneul")) != null) CharSetup.Ground(sitFloor, "SitFloor", Fbx("Haneul"));
            // 컨트롤러
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(CastCtrl) ?? AnimatorController.CreateAnimatorControllerAtPath(CastCtrl);
            var sm = ctrl.layers[0].stateMachine;
            AnimatorState St(string name, AnimationClip clip)
            {
                var st = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == name) ?? sm.AddState(name);
                st.motion = clip; st.writeDefaultValues = true; st.iKOnFeet = true;
                return st;
            }
            var baseIdle = CharSetup.Clip(CharSetup.SiwooFbx, "Idle");
            var idle = St("Idle", baseIdle);
            foreach (var (file, state, _, _) in Clips)
            {
                string f = $"{ClipDir}/{file}.fbx";
                var c = AssetDatabase.LoadAllAssetsAtPath(f).OfType<AnimationClip>().FirstOrDefault(x => x.name == state);
                if (c != null) St(state, c);
            }
            // 태오(정식 모델 = 시우 컨트롤러)도 바른 앉기: Siwoo.controller 에 'Sit' 상태(전이 없음 — CastPose 가 부름)
            var sc = AssetDatabase.LoadAssetAtPath<AnimatorController>(CharSetup.SiwooCtrl);
            var sitClip = AssetDatabase.LoadAllAssetsAtPath($"{ClipDir}/SitUp.fbx").OfType<AnimationClip>().FirstOrDefault(x => x.name == "Sit");
            if (sc != null && sitClip != null)
            {
                var ssm = sc.layers[0].stateMachine;
                var st = ssm.states.Select(x => x.state).FirstOrDefault(x => x.name == "Sit") ?? ssm.AddState("Sit", new Vector3(600f, 400f, 0f));
                st.motion = sitClip; st.writeDefaultValues = true; st.iKOnFeet = true;
                EditorUtility.SetDirty(sc);
                Notes.Add("Siwoo.controller: 'Sit' 상태(바른 앉기)");
            }
            sm.defaultState = idle;
            var cls = ctrl.layers;
            if (!cls[0].iKPass) { cls[0].iKPass = true; ctrl.layers = cls; }      // CastLook(고개·팔 IK)
            EditorUtility.SetDirty(ctrl);
            // 프리팹
            Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(Application.dataPath), PrefabDir));
            AssetDatabase.Refresh();
            foreach (var (k, n) in Cast)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx(k));
                if (model == null) continue;
                var oc = Override($"{Root}/Anim/Cast_{k}.overrideController", ctrl, baseIdle, CharSetup.Clip(Fbx(k), "Idle"));
                var inst = (PrefabUtility.InstantiatePrefab(model) as GameObject) ?? Object.Instantiate(model);
                inst.name = n;
                CharSetup.Paint(inst, AssetDatabase.LoadAssetAtPath<Material>(Mat(k)), null);
                var anim = inst.GetComponent<Animator>() ?? inst.AddComponent<Animator>();
                anim.avatar = CharSetup.AvatarOf(Fbx(k));
                anim.runtimeAnimatorController = oc;
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                if (inst.GetComponent<HandShape>() == null) inst.AddComponent<HandShape>();
                foreach (var c in inst.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                var b = CharSetup.MainSkin(inst);
                float h = b != null ? b.sharedMesh.bounds.size.z * b.transform.lossyScale.x : -1f;
                PrefabUtility.SaveAsPrefabAsset(inst, Prefab(k));
                Object.DestroyImmediate(inst);
                Notes.Add($"{n}({k}) 키 {h:F2}m");
            }
            Debug.Log($"{Tag} 배역 {Cast.Length}명 · 컨트롤러 {CastCtrl}(Idle·Sit·TalkOpen·TalkHip·SitClap·SitFist) · " + string.Join(" · ", Notes));
        }

        static RuntimeAnimatorController Override(string path, RuntimeAnimatorController ctrl, AnimationClip baseIdle, AnimationClip own)
        {
            var oc = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
            if (oc == null) { oc = new AnimatorOverrideController(ctrl); AssetDatabase.CreateAsset(oc, path); }
            oc.runtimeAnimatorController = ctrl;
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            oc.GetOverrides(pairs);
            for (int i = 0; i < pairs.Count; i++) if (pairs[i].Key == baseIdle && own != null) pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(pairs[i].Key, own);
            oc.ApplyOverrides(pairs);
            EditorUtility.SetDirty(oc);
            return oc;
        }

        // ───────────────────────── 무대 꾸미기(StorySetup.Stages 가 부름)
        public static GameObject Put(Transform root, string key, string name, Vector3 local, float yaw, string state = null, Color? tint = null)
        {
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab(key));
            if (pf == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, root.gameObject.scene);
            go.name = name;
            go.transform.SetParent(root, false);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            if (state != null) { var a = go.AddComponent<CastPose>(); a.State = state; }
            if (tint.HasValue)
            {
                var src = AssetDatabase.LoadAssetAtPath<Material>(Mat(key));
                string tp = $"{StorySetup.StageMatDir}/M_Cast_{key}_{ColorUtility.ToHtmlStringRGB(tint.Value)}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(tp);
                if (m == null && src != null) { m = new Material(src); AssetDatabase.CreateAsset(m, tp); }
                if (m != null) { m.SetColor("_BaseColor", tint.Value); m.SetColor("_Color", tint.Value); EditorUtility.SetDirty(m); CharSetup.Paint(go, m, null); }
            }
            return go;
        }

        static GameObject Prop(Transform root, string name, Vector3 local, Vector3 size, Material m, float yaw = 0f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(root, false);
            go.transform.localPosition = local + Vector3.up * size.y * 0.5f;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = m;
            go.layer = Layers.Wall;
            return go;
        }

        static Material M(string n, Color c)
        {
            string p = $"{StorySetup.StageMatDir}/{n}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m == null)
            {
                var envM = AssetDatabase.LoadAssetAtPath<Material>($"{Root}/Materials/Env/M_Paint_White.mat");
                m = envM != null ? new Material(envM) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, p);
            }
            m.SetColor("_BaseColor", c); m.SetColor("_Color", c);
            if (m.HasProperty("_1st_ShadeColor")) m.SetColor("_1st_ShadeColor", c * 0.78f);
            if (m.HasProperty("_2nd_ShadeColor")) m.SetColor("_2nd_ShadeColor", c * 0.66f);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// 무대에 배역·가구. 캡슐 배우가 있으면 같은 이름 모델로 바꿈. 돌려준 값 = 로그용 요약
        /// 무대 꾸밈: 키트 무대(9단계 — StageKitSetup.Placed)면 소켓 자리에 배역만, 아니면 예전 자리 무대 꾸밈(상자 가구)
        public static string Dress(string key, Transform root, Vector2 size, List<Transform> spawns, StageKitSetup.Placed kp)
        {
            if (kp == null) return DressPlaceholder(key, root, size, spawns);
            if (!Ready) return "배역 프리팹 없음(CastSetup.Build 먼저)";
            var log = new List<string>();
            void Replace(string name)
            {
                var old = root.Find(name);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }
            Transform Mark(string name, (Vector3 pos, float yaw, string state) at)
            {
                var t = new GameObject(name).transform; t.SetParent(root, false);
                t.localPosition = at.pos; t.localRotation = Quaternion.Euler(0f, at.yaw, 0f);
                return t;
            }
            if (key == "Gukbap")
            {
                Replace("엄마");
                (Vector3 pos, float yaw, string state) at = kp.Actors.TryGetValue("엄마", out var a0) ? a0 : (new Vector3(-2f, 0f, 2.3f), 120f, "TalkHip");
                var mom = Put(root, "Mom", "엄마", at.pos, at.yaw, at.state ?? "TalkHip");
                log.Add(mom != null ? "엄마(주방 가마솥 앞)" : "엄마 없음");
            }
            else if (key == "Home")
            {
                (Vector3 pos, float yaw, string state) at = kp.Actors.TryGetValue("하늘", out var a0) ? a0 : (new Vector3(0.3f, 0.06f, 1.8f), 180f, "SitFloor");
                var h = Put(root, "Haneul", "하늘", at.pos, at.yaw, at.state ?? "SitFloor");
                if (kp.Seats.TryGetValue("Braid", out var br)) Mark("땋기 자리", br);
                if (kp.Photos.TryGetValue("Dad", out var ph))
                {
                    // 아빠 사진: 키트의 액자 틀 앞에 사진 면(Unlit). 배우 이름 '아빠 사진'(대사 카메라 "근접:아빠 사진")
                    var photo = new GameObject("아빠 사진").transform; photo.SetParent(root, false);
                    photo.localPosition = ph.pos; photo.localRotation = Quaternion.Euler(0f, ph.yaw, 0f);
                    var ptex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/Art/Story/DadPhoto.png");
                    if (ptex != null)
                    {
                        string pm = $"{StorySetup.StageMatDir}/M_DadPhoto.mat";
                        var mat = AssetDatabase.LoadAssetAtPath<Material>(pm);
                        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(mat, pm); }
                        mat.SetTexture("_BaseMap", ptex); mat.SetColor("_BaseColor", new Color(0.92f, 0.9f, 0.86f));
                        EditorUtility.SetDirty(mat);
                        var face = GameObject.CreatePrimitive(PrimitiveType.Quad);
                        face.name = "사진 면"; Object.DestroyImmediate(face.GetComponent<Collider>());
                        face.transform.SetParent(photo, false);
                        face.transform.localPosition = Vector3.zero;
                        face.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);      // 쿼드 앞면(-Z)을 방 쪽(+Z 로컬 = 소켓 앞)으로
                        face.transform.localScale = new Vector3(ph.w * 0.86f, ph.h * 0.88f, 1f);
                        face.GetComponent<Renderer>().sharedMaterial = mat;
                        face.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    }
                }
                log.Add(h != null ? "하늘(방석에 앉음)·아빠 사진" : "하늘 없음");
            }
            else if (key == "School")
            {
                (Vector3 pos, float yaw, string state) at = kp.Actors.TryGetValue("도경민", out var a0) ? a0 : (new Vector3(-2f, 0f, 11.9f), 180f, "TalkOpen");
                var teacher = Put(root, "Dokyungmin", "도경민", at.pos, at.yaw, at.state ?? "TalkOpen");
                if (kp.Seats.TryGetValue("시우", out var me)) Mark("시우 자리", me);
                Color[] tints = { new Color(0.85f, 0.85f, 0.88f), new Color(0.80f, 0.82f, 0.90f), new Color(0.92f, 0.88f, 0.84f), new Color(0.78f, 0.80f, 0.84f) };
                // 6 × 5 = 30 자리: 시우 자리(뒷줄 4) + 빈자리 4 → 25 명. 배역: 태오(앞줄 창가) · 민재 · 건우 · 임찬
                var empty = new HashSet<(int, int)> { (4, 4), (4, 5), (3, 0), (2, 5), (4, 1) };
                int students = 0;
                foreach (var (r, c, pos, yaw) in kp.Desks.OrderBy(x => x.row).ThenBy(x => x.col))
                {
                    if (empty.Contains((r, c))) continue;
                    string who = (r, c) == (0, 0) ? "태오" : (r, c) == (1, 1) ? "민재" : (r, c) == (2, 0) ? "건우" : (r, c) == (1, 2) ? "임찬" : null;
                    string state = (r * 6 + c) % 5 == 2 ? "SitTalk" : "Sit";
                    if (who == "태오")
                    {
                        var tf = AssetDatabase.LoadAssetAtPath<GameObject>(FormalSetup.TaeoPrefab);
                        if (tf != null)
                        {
                            var t = (GameObject)PrefabUtility.InstantiatePrefab(tf, root.gameObject.scene);
                            t.name = "태오"; t.transform.SetParent(root, false); t.transform.localPosition = pos; t.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                            t.AddComponent<CastPose>().State = "Sit";
                        }
                    }
                    else if (who == "민재") Put(root, "Minjae", "민재", pos, yaw, "Sit");
                    else if (who == "건우") Put(root, "Geonwoo", "건우", pos, yaw, "Sit");
                    else if (who == "임찬")
                    {
                        var kk = AssetDatabase.LoadAssetAtPath<GameObject>(ClipSetup.EnemyFbx("Kkanjok"));
                        if (kk != null)
                        {
                            var g = (GameObject)PrefabUtility.InstantiatePrefab(kk, root.gameObject.scene);
                            g.name = "임찬"; g.transform.SetParent(root, false); g.transform.localPosition = pos; g.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                            CharSetup.Paint(g, AssetDatabase.LoadAssetAtPath<Material>(ClipSetup.EnemyMat("Kkanjok")), null);
                            var an = g.GetComponent<Animator>() ?? g.AddComponent<Animator>();
                            an.avatar = CharSetup.AvatarOf(ClipSetup.EnemyFbx("Kkanjok"));
                            an.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CastCtrl);
                            g.AddComponent<CastPose>().State = "Sit";
                        }
                    }
                    else
                    {
                        bool girl = (r + c) % 2 == 1;
                        Put(root, girl ? "StudentF" : "StudentM", $"반 친구 {++students}", pos, yaw, state, tints[(r * 6 + c) % tints.Length]);
                    }
                }
                log.Add($"교실 키트: 도경민·태오·민재·건우·임찬·반 친구 {students}(책상 30, 시우 자리 비움)");
                if (teacher == null) log.Add("도경민 없음");
            }
            return string.Join(" · ", log);
        }

        static string DressPlaceholder(string key, Transform root, Vector2 size, List<Transform> spawns)
        {
            if (!Ready) return "배역 프리팹 없음(CastSetup.Build 먼저)";
            var wood = M("M_St_Wood", new Color(0.55f, 0.40f, 0.28f));
            var dark = M("M_St_Board", new Color(0.18f, 0.24f, 0.21f));
            var white = M("M_St_White", new Color(0.92f, 0.90f, 0.86f));
            var steel = M("M_St_Steel", new Color(0.45f, 0.46f, 0.48f));
            var log = new List<string>();
            void Replace(string name)
            {
                var old = root.Find(name);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }
            if (key == "Gukbap")
            {
                Replace("엄마");
                var mom = Put(root, "Mom", "엄마", new Vector3(-1.2f, 0f, 1.2f), 200f, "TalkHip");
                Prop(root, "조리대", new Vector3(-2.4f, 0f, 3.2f), new Vector3(3.6f, 0.9f, 0.7f), steel);
                Prop(root, "식탁 1", new Vector3(2.0f, 0f, 0.5f), new Vector3(1.4f, 0.72f, 0.8f), wood);
                Prop(root, "식탁 2", new Vector3(2.0f, 0f, -2.0f), new Vector3(1.4f, 0.72f, 0.8f), wood);
                log.Add(mom != null ? "엄마 모델(허리 손 말하기)" : "엄마 없음");
            }
            else if (key == "Home")
            {
                // 하늘은 방 가운데 의자(시우와 1.2m 마주 봄 — 땋을 때 시우가 등 뒤로), 식탁은 벽 쪽으로(예전: 식탁이 둘 사이를 막아 근접 카메라가 식탁에 걸림)
                Prop(root, "식탁", new Vector3(1.75f, 0f, 2.6f), new Vector3(1.2f, 0.72f, 0.8f), wood);
                Prop(root, "의자", new Vector3(0f, 0f, 1.05f), new Vector3(0.42f, 0.42f, 0.42f), wood);
                var h = Put(root, "Haneul", "하늘", new Vector3(0f, 0f, 1.1f), 180f, "Sit");
                // 벽(북쪽)에 아빠 사진·망치 — 방 안쪽을 보게(근접 카메라 "근접:아빠 사진")
                var photo = Prop(root, "아빠 사진", new Vector3(-1.4f, 1.42f, 3.4f), new Vector3(0.36f, 0.46f, 0.04f), dark, 180f);
                // 사진 면: 안전모 쓰고 웃는 아빠(힉스필드 GPT Image 2.5 한 장, tools/env/story/ep1_props.json) — 빛과 상관없이 보이게 Unlit
                var ptex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/Art/Story/DadPhoto.png");
                if (ptex != null)
                {
                    string pm = $"{StorySetup.StageMatDir}/M_DadPhoto.mat";
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(pm);
                    if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(mat, pm); }
                    mat.SetTexture("_BaseMap", ptex); mat.SetColor("_BaseColor", new Color(0.92f, 0.9f, 0.86f));
                    EditorUtility.SetDirty(mat);
                    var face = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    face.name = "사진 면";
                    Object.DestroyImmediate(face.GetComponent<Collider>());
                    face.transform.SetParent(photo.transform, false);
                    face.transform.localPosition = new Vector3(0f, 0f, 0.51f);     // 상자 앞면(방 쪽 = 로컬 +z, 상자가 180° 돌아 있음)
                    face.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    face.transform.localScale = new Vector3(0.9f, 0.9f, 1f);
                    face.GetComponent<Renderer>().sharedMaterial = mat;
                }
                Prop(root, "망치", new Vector3(-1.02f, 1.48f, 3.41f), new Vector3(0.06f, 0.36f, 0.05f), wood, 180f);
                Prop(root, "빨래 건조대", new Vector3(2.2f, 0f, -1.5f), new Vector3(1.2f, 1.1f, 0.5f), steel);
                log.Add(h != null ? "하늘(앉음)·식탁·아빠 사진·망치" : "하늘 없음");
            }
            else if (key == "School")
            {
                for (int i = 1; i <= 6; i++) Replace($"반 친구 {i}");
                float d = size.y;
                // 교실 = 북쪽 절반(z 0 ~ d/2), 칸막이 벽(뒷문 틈 x −3.4 ~ −2.4)
                Prop(root, "칸막이 동", new Vector3(1.0f, 0f, 0f), new Vector3(7.0f, 3.0f, 0.2f), white);
                Prop(root, "칸막이 서", new Vector3(-4.25f, 0f, 0f), new Vector3(0.5f, 3.0f, 0.2f), white);
                Prop(root, "뒷문 위", new Vector3(-2.9f, 2.2f, 0f), new Vector3(1.0f, 0.8f, 0.2f), white);
                Prop(root, "칠판", new Vector3(0f, 0.9f, d / 2f - 0.08f), new Vector3(4.6f, 1.3f, 0.06f), dark);
                Prop(root, "교탁", new Vector3(0f, 0f, d / 2f - 1.6f), new Vector3(1.0f, 1.0f, 0.6f), wood);
                var teacher = Put(root, "Dokyungmin", "도경민", new Vector3(0.4f, 0f, d / 2f - 1.1f), 180f, "TalkOpen");
                // 책상 4 × 3(앞이 북쪽), 학생은 남쪽을 등지고 북쪽을 봄
                float[] xs = { -3.0f, -1.0f, 1.0f, 3.0f };
                float[] zs = { d / 2f - 4.0f, d / 2f - 6.0f, d / 2f - 8.0f };
                Color[] tints = { new Color(0.85f, 0.85f, 0.88f), new Color(0.80f, 0.82f, 0.90f), new Color(0.92f, 0.88f, 0.84f), new Color(0.78f, 0.80f, 0.84f) };
                int students = 0;
                for (int r = 0; r < zs.Length; r++)
                    for (int c = 0; c < xs.Length; c++)
                    {
                        var p = new Vector3(xs[c], 0f, zs[r]);
                        Prop(root, $"책상 {r + 1}-{c + 1}", p + new Vector3(0f, 0f, 0.55f), new Vector3(0.7f, 0.72f, 0.5f), wood);
                        Prop(root, $"의자 {r + 1}-{c + 1}", p + new Vector3(0f, 0f, -0.08f), new Vector3(0.42f, 0.44f, 0.40f), wood);
                        string who = (r, c) == (0, 3) ? "태오" : (r, c) == (1, 0) ? "민재" : (r, c) == (2, 0) ? "건우" : (r, c) == (1, 1) ? "임찬" : (r, c) == (2, 1) ? "" : null;
                        if (who == "") continue;   // 시우 자리(맨 뒷줄 빈자리)
                        if (who == "태오")
                        {
                            var tf = AssetDatabase.LoadAssetAtPath<GameObject>(FormalSetup.TaeoPrefab);
                            if (tf != null)
                            {
                                var t = (GameObject)PrefabUtility.InstantiatePrefab(tf, root.gameObject.scene);
                                t.name = "태오"; t.transform.SetParent(root, false); t.transform.localPosition = p; t.transform.localRotation = Quaternion.identity;
                                t.AddComponent<CastPose>().State = "Sit";
                            }
                        }
                        else if (who == "민재") Put(root, "Minjae", "민재", p, 0f, "Sit");
                        else if (who == "건우") Put(root, "Geonwoo", "건우", p, 0f, "Sit");
                        else if (who == "임찬")
                        {
                            var kk = AssetDatabase.LoadAssetAtPath<GameObject>(ClipSetup.EnemyFbx("Kkanjok"));
                            if (kk != null)
                            {
                                var g = (GameObject)PrefabUtility.InstantiatePrefab(kk, root.gameObject.scene);
                                g.name = "임찬"; g.transform.SetParent(root, false); g.transform.localPosition = p;
                                CharSetup.Paint(g, AssetDatabase.LoadAssetAtPath<Material>(ClipSetup.EnemyMat("Kkanjok")), null);
                                var an = g.GetComponent<Animator>() ?? g.AddComponent<Animator>();
                                an.avatar = CharSetup.AvatarOf(ClipSetup.EnemyFbx("Kkanjok"));
                                an.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CastCtrl);
                                g.AddComponent<CastPose>().State = "Sit";
                            }
                        }
                        else
                        {
                            bool girl = (r + c) % 2 == 1;
                            Put(root, girl ? "StudentF" : "StudentM", $"반 친구 {++students}", p, 0f, (r + c) % 3 == 0 ? "SitClap" : "Sit", tints[(r * 4 + c) % tints.Length]);
                        }
                    }
                var bd = new GameObject("BackDoor").transform;
                bd.SetParent(root, false); bd.localPosition = new Vector3(-2.9f, 0f, -1.4f); bd.localRotation = Quaternion.identity;
                spawns.Add(bd);
                log.Add($"교실: 칠판·교탁·책상 12·도경민·태오·민재·건우·임찬·반 친구 {students}·뒷문(스폰 BackDoor)");
                if (teacher == null) log.Add("도경민 없음");
            }
            return string.Join(" · ", log);
        }
    }
}
