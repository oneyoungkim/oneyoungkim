// 행인1의 메인이벤트 — 리깅 캐릭터(시우·태오) 임포트 설정 + 툰 재질 + 애니메이터 컨트롤러 (docs/07_M1_조작_설계.md 5장)
// 재료: concept/art/3d/anim/*.glb (Tripo 자동 리깅 + 클립 1개) → tools/glb2fbx.py(Blender 헤드리스)로 FBX 변환 → 여기서 Humanoid 로 가져온다.
//   Siwoo.fbx(Idle) · SiwooWalk.fbx(Walk) · SiwooRun.fbx(Run) · Taeo.fbx(Idle) — 모두 메시+뼈+클립(아바타 기준 자세를 스킨 바인드 포즈에서 읽으므로 메시가 필요)
// 클립마다 리깅을 따로 해서 뼈 휴지 자세가 조금씩 다르다 → FBX 마다 자기 아바타(Humanoid)를 만들고, 클립은 근육 공간으로 시우 아바타에 리타깃된다.
// 걷기·달리기는 제자리 동작(루트 이동 없음, Bake Into Pose) — 이동은 PlayerMotor(CharacterController)가 한다.
// batchmode: -executeMethod Haengin.EditorTools.CharSetup.Build  (-nographics 가능). M1Setup.Build 도 먼저 이것을 부른다.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Haengin.EditorTools
{
    public static class CharSetup
    {
        const string Tag = "[CharSetup]";
        public const string Dir = "Assets/_Project/Art/Characters";
        public const string SiwooFbx = Dir + "/Siwoo/Siwoo.fbx";
        public const string SiwooWalkFbx = Dir + "/Siwoo/SiwooWalk.fbx";
        public const string SiwooRunFbx = Dir + "/Siwoo/SiwooRun.fbx";
        public const string SiwooTex = Dir + "/Siwoo/SiwooTex.jpg";
        public const string TaeoFbx = Dir + "/Taeo/Taeo.fbx";
        public const string TaeoTex = Dir + "/Taeo/TaeoTex.jpg";
        public const string AnimDir = "Assets/_Project/Anim";
        public const string SiwooCtrl = AnimDir + "/Siwoo.controller";
        public const string TaeoCtrl = AnimDir + "/Taeo.controller";
        public const string SiwooMat = "Assets/_Project/Materials/M_SiwooAnim_Toon.mat";
        public const string TaeoMat = "Assets/_Project/Materials/M_Taeo_Toon.mat";

        // 애니메이터 변수(LocoAnim 과 같은 이름)
        public const string PSpeed = "Speed", PBlend = "Blend", PRate = "Rate";
        public const string MoveState = "Move";

        /// 걷기 한 주기만 쓴다(원본 4.2초 = 같은 걸음 3주기). 블렌드 트리가 걷기·달리기의 정규화 시간을 맞추므로 한 주기끼리여야 발이 엇갈리지 않는다.
        /// FBX 프레임(30fps, 1부터): 이음새가 가장 매끄러운 주기 = 55~97(42프레임 = 1.4초, 처음·끝 자세 차 평균 2.2°)
        public const int WalkFirst = 55, WalkLast = 97;

        /// 외곽선: 예전 정적 모델(원본 키 1.0 을 ×1.74)과 같은 월드 두께(약 4.5mm)가 되게. 새 FBX 는 배율 1 에 키 1.74m
        const float OutlineWorldMm = 2.6f * 1.74f;

        /// Tripo 뼈 이름 → Humanoid. 손가락 뼈는 없다
        static readonly (HumanBodyBones h, string bone)[] Map =
        {
            (HumanBodyBones.Hips, "Hips"), (HumanBodyBones.Spine, "Spine02"), (HumanBodyBones.Chest, "Spine01"), (HumanBodyBones.UpperChest, "Spine"),
            (HumanBodyBones.Neck, "neck"), (HumanBodyBones.Head, "Head"),
            (HumanBodyBones.LeftShoulder, "LeftShoulder"), (HumanBodyBones.LeftUpperArm, "LeftArm"), (HumanBodyBones.LeftLowerArm, "LeftForeArm"), (HumanBodyBones.LeftHand, "LeftHand"),
            (HumanBodyBones.RightShoulder, "RightShoulder"), (HumanBodyBones.RightUpperArm, "RightArm"), (HumanBodyBones.RightLowerArm, "RightForeArm"), (HumanBodyBones.RightHand, "RightHand"),
            (HumanBodyBones.LeftUpperLeg, "LeftUpLeg"), (HumanBodyBones.LeftLowerLeg, "LeftLeg"), (HumanBodyBones.LeftFoot, "LeftFoot"), (HumanBodyBones.LeftToes, "LeftToeBase"),
            (HumanBodyBones.RightUpperLeg, "RightUpLeg"), (HumanBodyBones.RightLowerLeg, "RightLeg"), (HumanBodyBones.RightFoot, "RightFoot"), (HumanBodyBones.RightToes, "RightToeBase"),
        };

        /// 측정 결과(로그·문서용)
        public sealed class Gait
        {
            public string Clip;
            public float Length, NaturalSpeed, StanceShare, AnkleMin, PhaseL, Moves, LeftX;
        }

        [MenuItem("Haengin/캐릭터 임포트·재질·애니메이터 다시 만들기")]
        public static void Build()
        {
            int code = 0;
            try { Setup(); AssetDatabase.SaveAssets(); }
            catch (Exception e) { Debug.LogError($"{Tag} 실패: {e}"); code = 1; }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        /// 임포트 설정(바뀐 것만 다시 임포트) → 재질 → 걸음 측정 → 컨트롤러. M1Setup·SandboxSetup 이 부른다.
        public static void Setup()
        {
            EnsureFolder(AnimDir);
            Texture(SiwooTex);
            Texture(TaeoTex);
            Human(SiwooFbx, "Idle", -1, -1);
            Human(SiwooWalkFbx, "Walk", WalkFirst, WalkLast);
            Human(SiwooRunFbx, "Run", -1, -1);
            Human(TaeoFbx, "Idle", -1, -1);

            var siwoo = Load<GameObject>(SiwooFbx);
            var taeo = Load<GameObject>(TaeoFbx);
            Toon(siwoo, SiwooMat, SiwooTex);
            Toon(taeo, TaeoMat, TaeoTex);

            var idle = Clip(SiwooFbx, "Idle");
            var walk = Clip(SiwooWalkFbx, "Walk");
            var run = Clip(SiwooRunFbx, "Run");
            var avatar = AvatarOf(SiwooFbx);
            var gi = Measure(siwoo, avatar, idle);
            var gw = Measure(siwoo, avatar, walk);
            var gr = Measure(siwoo, avatar, run);
            Debug.Log($"{Tag} 걸음 측정(시우 아바타에 리타깃, 제자리 재생): " + string.Join(" | ", new[] { gi, gw, gr }.Select(g =>
                $"{g.Clip} {g.Length:F3}s · 디딤발 뒤로 {g.NaturalSpeed:F2} m/s · 디딤 {g.StanceShare * 100f:F0}% · 발 앞뒤 폭 {g.Moves:F2}m · 발목 최저 {g.AnkleMin:F3}m · 왼발 앞 끝 {g.PhaseL:F2} · 왼다리−오른다리 x {g.LeftX:F2}")));
            if (gw.Moves < 0.2f || gr.Moves < 0.2f) throw new Exception("걷기·달리기 샘플링에서 발이 움직이지 않았습니다(측정 실패)");

            // 재생 속도 = 게임 속도 ÷ 클립 고유 속도(디딤발이 땅에 붙어 있게). 달리기 주기를 걷기 주기에 맞춤(왼발 앞 끝 = 같은 정규화 시간)
            var mt = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/_Project/Settings/MoveTuning.asset");
            float walkSpeed = ReadFloat(mt, "walkSpeed", 1.6f), runSpeed = ReadFloat(mt, "runSpeed", 4.5f);
            float kw = walkSpeed / Mathf.Max(0.1f, gw.NaturalSpeed);
            float kr = runSpeed / Mathf.Max(0.1f, gr.NaturalSpeed);
            float offR = Mathf.Repeat(gr.PhaseL - gw.PhaseL, 1f);
            SiwooController(idle, walk, run, kw, kr, offR);
            Debug.Log($"{Tag} 컨트롤러 {SiwooCtrl}: Blend 0 Idle ×1 · 1 Walk ×{kw:F3}(주기 {gw.Length / kw:F3}s, 분당 {120f * kw / gw.Length:F0}걸음) · " +
                      $"2 Run ×{kr:F3}(주기 {gr.Length / kr:F3}s, 분당 {120f * kr / gr.Length:F0}걸음, 주기 맞춤 {offR:F3}) · 상태 속도 = Rate · Foot IK 켬");
            TaeoController(Clip(TaeoFbx, "Idle"));
            AssetDatabase.SaveAssets();
        }

        // ───────────────────────── 임포트 설정
        static void Texture(string path)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter ?? throw new Exception("텍스처가 없습니다: " + path);
            bool dirty = false;
            void Set<T>(T cur, T want, Action<T> apply) { if (!EqualityComparer<T>.Default.Equals(cur, want)) { apply(want); dirty = true; } }
            Set(ti.textureType, TextureImporterType.Default, v => ti.textureType = v);
            Set(ti.sRGBTexture, true, v => ti.sRGBTexture = v);
            Set(ti.maxTextureSize, 2048, v => ti.maxTextureSize = v);
            Set(ti.mipmapEnabled, true, v => ti.mipmapEnabled = v);
            Set(ti.textureCompression, TextureImporterCompression.CompressedHQ, v => ti.textureCompression = v);
            Set(ti.anisoLevel, 4, v => ti.anisoLevel = v);
            if (dirty) ti.SaveAndReimport();
        }

        /// Humanoid 로: 뼈 매핑을 표대로 직접 넣고(자동 매핑에 맡기지 않음), 클립 1개를 루프·제자리(Bake Into Pose)로.
        static void Human(string path, string clipName, int first, int last)
        {
            var mi = AssetImporter.GetAtPath(path) as ModelImporter ?? throw new Exception("FBX 가 없습니다: " + path);
            var model = Load<GameObject>(path);
            var all = model.GetComponentsInChildren<Transform>(true);
            var names = new HashSet<string>(all.Select(t => t.name));
            foreach (var (_, bone) in Map)
                if (!names.Contains(bone)) throw new Exception($"{path}: 뼈 '{bone}' 가 없습니다");

            float hipsY;
            var hd = new HumanDescription
            {
                human = Map.Select(m => new HumanBone
                {
                    humanName = HumanName(m.h),
                    boneName = m.bone,
                    limit = new HumanLimit { useDefaultValues = true },
                }).ToArray(),
                skeleton = BindSkeleton(model, out hipsY),
                upperArmTwist = 0.5f, lowerArmTwist = 0.5f, upperLegTwist = 0.5f, lowerLegTwist = 0.5f,
                armStretch = 0.05f, legStretch = 0.05f, feetSpacing = 0f, hasTranslationDoF = false,
            };

            mi.globalScale = 1f;
            mi.useFileScale = true;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.importBlendShapes = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importVisibility = false;
            mi.isReadable = false;
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.humanDescription = hd;
            mi.optimizeGameObjects = false;
            mi.importAnimation = true;
            mi.animationCompression = ModelImporterAnimationCompression.Off;   // 발 디딤 정확도(클립이 작아 용량 부담 없음)

            var take = mi.defaultClipAnimations.FirstOrDefault() ?? throw new Exception($"{path}: 애니메이션 테이크가 없습니다");
            var c = new ModelImporterClipAnimation
            {
                name = clipName,
                takeName = take.takeName,
                firstFrame = first >= 0 ? first : take.firstFrame,
                lastFrame = last >= 0 ? last : take.lastFrame,
                loopTime = true,
                loopPose = false,
                wrapMode = WrapMode.Loop,
                // 루트 회전·높이·수평 위치 모두 자세에 굽기(제자리). 기준 = 원본(클립이 만든 그대로: 정면 +Z, 발 = 바닥)
                lockRootRotation = true, keepOriginalOrientation = true,
                lockRootHeightY = true, keepOriginalPositionY = true, heightFromFeet = false,
                lockRootPositionXZ = true, keepOriginalPositionXZ = true,
                maskType = ClipAnimationMaskType.None,
            };
            mi.clipAnimations = new[] { c };
            mi.SaveAndReimport();

            foreach (var pr in typeof(ModelImporter).GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
                if ((pr.Name.Contains("Warning") || pr.Name.Contains("Error")) && pr.PropertyType == typeof(string) && pr.GetIndexParameters().Length == 0)
                {
                    string v = null;
                    try { v = pr.GetValue(mi) as string; } catch { /* 참고용 */ }
                    if (!string.IsNullOrWhiteSpace(v)) Debug.Log($"{Tag} {path} 임포트 메시지 {pr.Name}: {v.Trim()}");
                }
            var av = AvatarOf(path);
            if (av == null || !av.isValid || !av.isHuman) throw new Exception($"{path}: Humanoid 아바타를 만들지 못했습니다(valid {av?.isValid} human {av?.isHuman})");
            var clip = Clip(path, clipName);
            Debug.Log($"{Tag} {path}: Humanoid 아바타 OK · 기준 자세 = 바인드 포즈(골반 높이 {hipsY:F3}m) · 뼈 {all.Length - 1}개(매핑 {Map.Length}) · 클립 '{clip.name}' {clip.length:F3}s 루프 {clip.isLooping} " +
                      $"(테이크 '{take.takeName}' 프레임 {c.firstFrame}~{c.lastFrame}) · 루트 {Describe(model)}");
        }

        /// 아바타 기준 자세 = 스킨의 바인드 포즈(휴지 자세). FBX 노드 기본값은 Blender 가 내보낼 때의 '현재 자세'(클립 한 프레임)라서 그대로 쓰면
        /// 아바타마다 기준이 달라져 리타깃이 어긋난다(2026-10-06 실측: 걷기 발목이 3.8cm, 달리기 8.4cm 땅에 묻힘). 그래서 바인드 행렬에서 다시 계산한다.
        static SkeletonBone[] BindSkeleton(GameObject model, out float hipsY)
        {
            var smr = model.GetComponentInChildren<SkinnedMeshRenderer>(true) ?? throw new Exception($"{model.name}: 스킨 메시가 없어 바인드 포즈를 알 수 없습니다(glb2fbx.py 를 메시 포함 1 로)");
            var root = model.transform;
            var bind = new Dictionary<Transform, Matrix4x4>();
            var bp = smr.sharedMesh.bindposes;
            var bones = smr.bones;
            for (int i = 0; i < bones.Length; i++)
                if (bones[i] != null) bind[bones[i]] = root.worldToLocalMatrix * smr.transform.localToWorldMatrix * bp[i].inverse;
            var world = new Dictionary<Transform, Matrix4x4>();
            Matrix4x4 W(Transform t)
            {
                if (t == root) return Matrix4x4.identity;
                if (world.TryGetValue(t, out var m)) return m;
                m = bind.TryGetValue(t, out var b) ? b : W(t.parent) * Matrix4x4.TRS(t.localPosition, t.localRotation, t.localScale);
                world[t] = m;
                return m;
            }
            var list = new List<SkeletonBone>();
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
            {
                if (t == root) { list.Add(new SkeletonBone { name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale }); continue; }
                var local = W(t.parent).inverse * W(t);
                list.Add(new SkeletonBone { name = t.name, position = local.GetColumn(3), rotation = local.rotation, scale = local.lossyScale });
            }
            var hips = bones.FirstOrDefault(b => b != null && b.name == "Hips");
            hipsY = hips != null ? W(hips).GetColumn(3).y : -1f;
            return list.ToArray();
        }

        static string HumanName(HumanBodyBones b)
        {
            string want = b.ToString();
            return HumanTrait.BoneName.FirstOrDefault(n => n.Replace(" ", "") == want) ?? throw new Exception("Humanoid 이름 없음: " + want);
        }

        static string Describe(GameObject model)
        {
            var smr = model.GetComponentInChildren<SkinnedMeshRenderer>(true);
            var arm = model.transform.Cast<Transform>().FirstOrDefault(t => t.name != (smr != null ? smr.name : ""));
            string s = $"'{model.name}' 배율 {model.transform.localScale.x:F3}";
            if (arm != null) s += $" · '{arm.name}' 회전 {arm.localEulerAngles} 배율 {arm.localScale.x:F3}";
            if (smr != null)
                s += $" · 메시 '{smr.name}' 배율 {smr.transform.lossyScale.x:F3} 정점 {smr.sharedMesh.vertexCount} 경계 {smr.sharedMesh.bounds.size}";
            return s;
        }

        // ───────────────────────── 재질
        /// FUJIMOTO_STYLE 6장 규칙의 UTS 재질(SandboxSetup 과 같은 값: 그림자 = 텍스처 × 모브, 외곽선 #1A1417, 스펙큘러·림 0). 외곽선 폭은 월드 두께를 예전 모델과 맞춤
        static void Toon(GameObject model, string matPath, string texPath)
        {
            var tex = Load<Texture2D>(texPath);
            var name = System.IO.Path.GetFileNameWithoutExtension(matPath);
            var mat = SandboxSetup.MakeToonMaterial(name, tex, Color.white);
            var smr = model.GetComponentInChildren<SkinnedMeshRenderer>(true);
            float s = smr != null ? smr.transform.lossyScale.x : 1f;
            mat.SetFloat("_Outline_Width", OutlineWorldMm / Mathf.Max(1e-4f, s));
            EditorUtility.SetDirty(mat);
            Debug.Log($"{Tag} 툰 재질 {matPath}: 텍스처 {tex.name} · 외곽선 폭 {mat.GetFloat("_Outline_Width"):F2}(메시 배율 {s:F3} → 월드 {OutlineWorldMm:F1}mm)");
        }

        // ───────────────────────── 걸음 측정 (제자리 재생 → 디딤발이 뒤로 가는 속도 = 클립 고유 이동 속도)
        static Gait Measure(GameObject model, Avatar avatar, AnimationClip clip, bool footIK = true)
        {
            var go = UnityEngine.Object.Instantiate(model);
            var g = new Gait { Clip = clip.name, Length = clip.length };
            // 편집 모드에서는 PlayableGraph.Evaluate 가 뼈에 쓰이지 않아서(1차 시도: 발이 그대로) AnimationMode 로 샘플링한다(애니메이션 창·Timeline 미리보기와 같은 길)
            var graph = PlayableGraph.Create("CharSetup.Measure");
            bool started = false;
            try
            {
                var anim = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
                anim.avatar = avatar;
                anim.applyRootMotion = false;
                anim.runtimeAnimatorController = null;
                anim.Rebind();
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var output = AnimationPlayableOutput.Create(graph, "out", anim);
                var cp = AnimationClipPlayable.Create(graph, clip);
                cp.SetApplyFootIK(footIK);
                output.SetSourcePlayable(cp);
                if (!AnimationMode.InAnimationMode()) { AnimationMode.StartAnimationMode(); started = true; }
                var lf = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
                var rf = anim.GetBoneTransform(HumanBodyBones.RightFoot);
                var root = go.transform;
                const int N = 240;
                float dt = clip.length / N;
                var L = new Vector3[N + 1];
                var R = new Vector3[N + 1];
                for (int i = 0; i <= N; i++)
                {
                    AnimationMode.BeginSampling();
                    AnimationMode.SamplePlayableGraph(graph, 0, i * dt);
                    AnimationMode.EndSampling();
                    L[i] = root.InverseTransformPoint(lf.position);
                    R[i] = root.InverseTransformPoint(rf.position);
                }
                float minY = Mathf.Min(L.Min(p => p.y), R.Min(p => p.y));
                g.AnkleMin = minY;
                g.Moves = Mathf.Max(L.Max(p => p.z) - L.Min(p => p.z), R.Max(p => p.z) - R.Min(p => p.z));
                var speeds = new List<float>();
                int stance = 0;
                foreach (var P in new[] { L, R })
                    for (int i = 1; i < N; i++)
                    {
                        if (P[i].y > minY + 0.03f) continue;
                        stance++;
                        speeds.Add(-(P[i + 1].z - P[i - 1].z) / (2f * dt));
                    }
                speeds.Sort();
                g.NaturalSpeed = speeds.Count > 0 ? speeds[speeds.Count / 2] : 0f;
                g.StanceShare = stance / (2f * (N - 1));
                int imax = 0;
                for (int i = 0; i < N; i++) if (L[i].z > L[imax].z) imax = i;
                g.PhaseL = imax / (float)N;
                // 정면 확인: 캐릭터 왼쪽이 −X 면 +Z 를 보고 있다(Unity 왼손 좌표)
                var lu = root.InverseTransformPoint(anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg).position);
                var ru = root.InverseTransformPoint(anim.GetBoneTransform(HumanBodyBones.RightUpperLeg).position);
                g.LeftX = lu.x - ru.x;
            }
            finally
            {
                if (started) AnimationMode.StopAnimationMode();
                graph.Destroy();
                UnityEngine.Object.DestroyImmediate(go);
            }
            return g;
        }

        // ───────────────────────── 컨트롤러
        static void SiwooController(AnimationClip idle, AnimationClip walk, AnimationClip run, float kw, float kr, float offR)
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(SiwooCtrl) ?? AnimatorController.CreateAnimatorControllerAtPath(SiwooCtrl);
            Param(ctrl, PSpeed, 0f);
            Param(ctrl, PBlend, 0f);
            Param(ctrl, PRate, 1f);
            var sm = ctrl.layers[0].stateMachine;
            var st = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == MoveState) ?? sm.AddState(MoveState);
            if (!(st.motion is BlendTree bt))
            {
                bt = new BlendTree { name = "Locomotion", hideFlags = HideFlags.HideInHierarchy };
                AssetDatabase.AddObjectToAsset(bt, ctrl);
                st.motion = bt;
            }
            bt.blendType = BlendTreeType.Simple1D;
            bt.blendParameter = PBlend;
            bt.useAutomaticThresholds = false;
            bt.children = new[]
            {
                new ChildMotion { motion = idle, threshold = 0f, timeScale = 1f, cycleOffset = 0f, directBlendParameter = PBlend },
                new ChildMotion { motion = walk, threshold = 1f, timeScale = kw, cycleOffset = 0f, directBlendParameter = PBlend },
                new ChildMotion { motion = run, threshold = 2f, timeScale = kr, cycleOffset = offR, directBlendParameter = PBlend },
            };
            st.speed = 1f;
            st.speedParameter = PRate;
            st.speedParameterActive = true;
            st.iKOnFeet = true;
            st.writeDefaultValues = true;
            sm.defaultState = st;
            EditorUtility.SetDirty(bt); EditorUtility.SetDirty(st); EditorUtility.SetDirty(sm); EditorUtility.SetDirty(ctrl);
        }

        static void TaeoController(AnimationClip idle)
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(TaeoCtrl) ?? AnimatorController.CreateAnimatorControllerAtPath(TaeoCtrl);
            var sm = ctrl.layers[0].stateMachine;
            var st = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "Idle") ?? sm.AddState("Idle");
            st.motion = idle;
            st.iKOnFeet = true;
            sm.defaultState = st;
            EditorUtility.SetDirty(st); EditorUtility.SetDirty(ctrl);
        }

        static void Param(AnimatorController c, string name, float def)
        {
            var ps = c.parameters;
            int i = Array.FindIndex(ps, p => p.name == name);
            if (i < 0) { c.AddParameter(new AnimatorControllerParameter { name = name, type = AnimatorControllerParameterType.Float, defaultFloat = def }); return; }
            if (ps[i].type != AnimatorControllerParameterType.Float || !Mathf.Approximately(ps[i].defaultFloat, def))
            {
                ps[i].type = AnimatorControllerParameterType.Float;
                ps[i].defaultFloat = def;
                c.parameters = ps;
            }
        }

        // ───────────────────────── 공용
        public static Avatar AvatarOf(string fbx) => AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Avatar>().FirstOrDefault();

        public static AnimationClip Clip(string fbx, string name) =>
            AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().FirstOrDefault(c => c.name == name && !c.name.StartsWith("__preview__"))
            ?? throw new Exception($"{fbx}: 클립 '{name}' 이 없습니다");

        static T Load<T>(string path) where T : UnityEngine.Object =>
            AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new Exception($"에셋이 없습니다: {path}");

        static float ReadFloat(ScriptableObject so, string field, float def)
        {
            if (so == null) return def;
            var p = new SerializedObject(so).FindProperty(field);
            return p != null ? p.floatValue : def;
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
