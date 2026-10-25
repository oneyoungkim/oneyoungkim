// 행인1의 메인이벤트 — 정식 모델 조립(docs/09_M3_버티컬슬라이스_설계.md 4-2 '정식 모델 2명의 정의', 6·7단계)
// 재료: tools/formal_model.py(손가락 뼈 30 · 얼굴 데칼 Face_LOD0~2 · Body LOD 3단 · 바이블 등신) + tools/face_tex.py(눈·입 지운 몸 텍스처 · 그림자 · 외곽선 굵기 · 표정 아틀라스)
//   → Art/Characters/<이름>/<이름>.fbx · <이름>{Tex,Shade}.jpg · <이름>{OutW,Face,FaceMask}.png · <이름>Face.json · <이름>Info.json
// 여기서 하는 것(CharSetup 이 Humanoid 손가락 30 매핑·재질을 한 뒤):
//   ① 손 자세 8종 맞춤: HandPoseLib 기본 각도표에서 시작해 HandCheck(손가락끼리·손바닥 관통, 휴지 자세 붙음 뺌)가 3.8mm 넘으면
//      그 손가락(+ 부딪힌 상대가 엄지면 엄지)을 8% 씩 펴서 다시 잰다 → Settings/HandPoses_<이름>.asset(맞춘 값 · 남은 관통 mm)
//   ② Decorate(모델 인스턴스): FaceRig(얼굴 데칼 칸 바꾸기·깜빡임·말하기) · HandPoser(맞춘 손 자세) · FormalModel(측정값) · 소켓 5 · LODGroup 3단
//   ③ 프리팹: Prefabs/Char/Taeo.prefab(정식 태오 — 이야기·주행 태오 PathRunner 용, 컨트롤러 = 시우 컨트롤러에 태오 대기를 덮어쓴 Anim/TaeoRun.overrideController)
//      시우 교복·배달복은 Player.prefab 안(M1Setup.BuildPlayerPrefab → AddFormal · Outfit)
// batchmode: -executeMethod Haengin.EditorGame.FormalSetup.Build (CharSetup → 손 맞춤 → Player 프리팹 → 태오 프리팹, -nographics 가능)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Haengin.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

namespace Haengin.EditorGame
{
    public static class FormalSetup
    {
        const string Tag = "[FormalSetup]";
        const string Root = "Assets/_Project";
        public const string TaeoPrefab = Root + "/Prefabs/Char/Taeo.prefab";
        public const string TaeoRunCtrl = Root + "/Anim/TaeoRun.overrideController";
        public static string PoseSetPath(string name) => $"{Root}/Settings/HandPoses_{name}.asset";
        /// D13 기준 4mm — 맞춤은 여유 0.4mm 를 둔다
        public const float PenTarget = 3.6f;   // 바인드 자세와 대기 클립 0.17초 자세 둘 다 재서 큰 쪽(D13 = 대기 클립 자세)

        public static void Build()
        {
            int code = 0;
            try
            {
                CharSetup.Setup();
                FitAll();
                M1Setup.BuildPlayerPrefab();
                BuildTaeoPrefab();
                AssetDatabase.SaveAssets();
            }
            catch (Exception e) { Debug.LogError($"{Tag} 실패: {e}"); code = 1; }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        public static void FitAll()
        {
            foreach (var (n, fbx, _) in CharSetup.Formal)
            {
                var m = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
                if (m != null && CharSetup.IsFormal(m)) FitHands(n, m);
            }
        }

        // ───────────────────────── ① 손 자세 맞춤
        public static HandPoseSet FitHands(string name, GameObject model)
        {
            string path = PoseSetPath(name);
            var set = AssetDatabase.LoadAssetAtPath<HandPoseSet>(path);
            if (set == null) { set = ScriptableObject.CreateInstance<HandPoseSet>(); AssetDatabase.CreateAsset(set, path); }
            var go = Object.Instantiate(model);
            var goB = Object.Instantiate(model);
            var log = new List<string>();
            var graph = PlayableGraph.Create("FormalSetup.FitHands");
            bool started = false;
            try
            {
                var poser = go.AddComponent<HandPoser>();
                poser.Bind(true);
                if (!poser.Ready) throw new Exception($"{name}: 손가락 뼈·손바닥 표시 뼈를 찾지 못했습니다");
                poser.ResetRest();
                var skin = CharSetup.MainSkin(go);
                var map = HandCheck.Build(skin);
                HandCheck.SetRest(map);
                // 게임 자세(대기 클립 0.17초 — D13 이 재는 자리)도 같이 잰다: 손목·엄지 밑동 살이 팔 자세에 따라 1~3mm 달라져서(바인드 자세만 맞추면 게임에서 4.5mm 가 나왔다)
                var poserB = goB.AddComponent<HandPoser>();
                poserB.Bind(true);
                var animB = goB.GetComponent<Animator>() ?? goB.AddComponent<Animator>();
                var fbx = CharSetup.Formal.First(f => f.name == name).fbx;
                animB.avatar = CharSetup.AvatarOf(fbx);
                animB.runtimeAnimatorController = null;
                animB.applyRootMotion = false;
                animB.Rebind();
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var output = AnimationPlayableOutput.Create(graph, "out", animB);
                var idle = CharSetup.Clip(name == "Taeo" ? CharSetup.TaeoFbx : CharSetup.SiwooFbx, "Idle");
                output.SetSourcePlayable(AnimationClipPlayable.Create(graph, idle));
                if (!AnimationMode.InAnimationMode()) { AnimationMode.StartAnimationMode(); started = true; }
                AnimationMode.BeginSampling();
                AnimationMode.SamplePlayableGraph(graph, 0, 10f / 60f);
                AnimationMode.EndSampling();
                poserB.ResetRest();
                var mapB = HandCheck.Build(CharSetup.MainSkin(goB));
                HandCheck.CopyRest(map, mapB);
                var perB = new float[2, 5];
                var worstB = new int[2, 5];
                int nFinger = map.Cls.Count(c => c >= 0 && HandCheck.FingerOf(c) >= 0), nPalm = map.Cls.Count(c => c >= 0 && HandCheck.FingerOf(c) < 0);
                log.Add($"검지1 위치 {FindDeep(go.transform, "RightHandIndex1").localPosition * 100f} · 정점 {map.Cls.Length} · 손가락 {nFinger} · 손바닥 {nPalm} · 검사 {map.Probe[0].Length}/{map.Probe[1].Length}(물갈퀴 뺌 {map.WebCount}) · 어긋난 줄기 L{map.BadChain[0]}/R{map.BadChain[1]}");
                var defs = HandPoseLib.Defaults();
                var left = new HandPoseDef[defs.Length];
                var right = new HandPoseDef[defs.Length];
                var penL = new float[defs.Length];
                var penR = new float[defs.Length];
                var per = new float[2, 5];
                var worst = new int[2, 5];
                for (int k = 0; k < defs.Length; k++)
                {
                    var pl = defs[k].Clone();
                    var pr = defs[k].Clone();
                    var d0 = defs[k];
                    int it = 0;
                    for (; it < 60; it++)
                    {
                        poser.ApplyNow(false, pl);
                        poser.ApplyNow(true, pr);
                        HandCheck.Measure(map, per, worst);
                        poserB.ApplyNow(false, pl);
                        poserB.ApplyNow(true, pr);
                        HandCheck.Measure(mapB, perB, worstB);
                        for (int h = 0; h < 2; h++) for (int f = 0; f < 5; f++) if (perB[h, f] > per[h, f]) { per[h, f] = perB[h, f]; worst[h, f] = worstB[h, f]; }
                        bool ok = true;
                        for (int h = 0; h < 2; h++)
                        {
                            var p = h == 0 ? pl : pr;
                            for (int f = 0; f < 5; f++)
                            {
                                if (per[h, f] <= PenTarget) continue;
                                ok = false;
                                int o = worst[h, f];
                                // 엄지와 부딪히면 엄지를 먼저(주먹·병 쥐기에서 엄지가 검지를 파고듦), 이웃 손가락이면 벌림부터 0 쪽으로, 손바닥이면 그 손가락을 편다
                                if (o == 0 && f != 0) { if (!Ease(p, 0, d0)) Ease(p, f, d0); }
                                else if (f >= 1 && o >= 1 && o <= 4) { Apart(p, f, o); Ease(p, f, d0); }   // 이웃 손가락끼리: 서로 벌려 비킴 + 조금 폄
                                else if (o == 5) Ease(p, f, d0, 0.35f);                                   // 손바닥에 끝이 박힘 → 그 손가락을 더 편다(바닥 35%)
                                else if (!Ease(p, f, d0) && o >= 0 && o < 5) Ease(p, o, d0);
                            }
                        }
                        if (ok) break;
                    }
                    float mL = 0f, mR = 0f;
                    for (int f = 0; f < 5; f++) { mL = Mathf.Max(mL, per[0, f]); mR = Mathf.Max(mR, per[1, f]); }
                    left[k] = pl; right[k] = pr; penL[k] = mL; penR[k] = mR;
                    string det = "";
                    if (mL > 4f || mR > 4f)
                    {
                        var r = new List<string>();
                        for (int h = 0; h < 2; h++) for (int f = 0; f < 5; f++) if (per[h, f] > 4f) r.Add($"{(h == 0 ? "L" : "R")}{f}>{worst[h, f]}:{per[h, f]:F1}");
                        det = "[" + string.Join(" ", r) + "]";
                    }
                    log.Add($"{defs[k].Name} {mL:F1}/{mR:F1}mm({it}회){det}");
                    if (HandPoseLib.IsFingerGun(pl) || HandPoseLib.IsFingerGun(pr)) throw new Exception($"{name}: '{defs[k].Name}' 가 손가락 총 모양이 되었습니다");
                }
                poser.ResetRest();
                set.Left = left; set.Right = right; set.PenLeft = penL; set.PenRight = penR;
                set.Note = $"{DateTime.Now:yyyy-MM-dd} FormalSetup.FitHands — 기준 {PenTarget}mm(D13 4mm), 휴지 자세 붙음 뺌, 손가락 2·3마디 정점 검사. " + string.Join(" · ", log);
                EditorUtility.SetDirty(set);
                Object.DestroyImmediate(map.Baked);
            }
            finally
            {
                if (started) AnimationMode.StopAnimationMode();
                graph.Destroy();
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(goB);
            }
            Debug.Log($"{Tag} 손 자세 맞춤 {name} → {path}: " + string.Join(" · ", log) + " (왼손/오른손 남은 관통 — 바인드 자세·대기 클립 0.17초 중 큰 것)");
            return set;
        }

        /// 이웃 손가락 f·o 를 서로 1.5° 씩 벌린다(±10° 까지). 벌림 부호(HandPoseDef): 검지 + = 엄지 쪽, 가운데·약지·새끼 + = 새끼 쪽
        static void Apart(HandPoseDef p, int f, int o)
        {
            float TowardIndex(int k) => k == 1 ? 1f : -1f;
            void Add(int k, float d) { var x = p.F[k]; p.F[k] = new FingerPose(x.A, x.B, x.C, Mathf.Clamp(x.Spread + d, -10f, 10f)); }
            if (o > f) { Add(f, 1.5f * TowardIndex(f)); Add(o, -1.5f * TowardIndex(o)); }
            else { Add(f, -1.5f * TowardIndex(f)); Add(o, 1.5f * TowardIndex(o)); }
        }

        /// 손가락 하나를 푼다: 굽힘 ×0.92(엄지는 맞섬도). 바닥 = 기본값의 50%(엄지 0%) — 자세 모양(주먹이 손가락 총이 되지 않게)을 지킨다. 벌림은 Apart 가 맡는다.
        /// 더 풀 수 없으면 false
        static bool Ease(HandPoseDef p, int f, HandPoseDef d0, float fingerFloor = 0.5f)
        {
            var fp = p.F[f];
            var o = d0.F[f];
            float floor = f == 0 ? 0.0f : fingerFloor;
            float Lim(float v, float orig) => Mathf.Abs(orig) < 1e-3f ? v : (Mathf.Abs(v * 0.92f) < Mathf.Abs(orig) * floor ? orig * floor : v * 0.92f);
            var nf = new FingerPose(Lim(fp.A, o.A), Lim(fp.B, o.B), Lim(fp.C, o.C), f == 0 && Mathf.Abs(fp.Spread) > 0.3f ? fp.Spread * 0.8f : fp.Spread);   // 엄지는 벌림도 줄임(손바닥 쪽으로 파고듦)
            bool changed = !Mathf.Approximately(nf.A, fp.A) || !Mathf.Approximately(nf.B, fp.B) || !Mathf.Approximately(nf.C, fp.C) || !Mathf.Approximately(nf.Spread, fp.Spread);
            p.F[f] = nf;
            if (f == 0)
            {
                float t = Mathf.Abs(p.ThumbOpp * 0.92f) < Mathf.Abs(d0.ThumbOpp) * floor ? d0.ThumbOpp * floor : p.ThumbOpp * 0.92f;
                changed |= !Mathf.Approximately(t, p.ThumbOpp);
                p.ThumbOpp = t;
            }
            return changed;
        }

        // ───────────────────────── ② 모델 인스턴스 꾸미기
        /// 정식 모델 인스턴스(FBX 프리팹 인스턴스)에 FaceRig · HandPoser · FormalModel · 소켓 5 · LODGroup
        public static void Decorate(GameObject inst, string name, int seed)
        {
            var faces = inst.GetComponentsInChildren<Renderer>(true).Where(CharSetup.IsFace).OrderBy(r => r.name).ToArray();
            var fr = inst.GetComponent<FaceRig>() ?? inst.AddComponent<FaceRig>();
            fr.Faces = faces;
            fr.Seed = seed;
            var meta = Json(CharSetup.FormalFile(name, "Face.json"));
            var info = Json(CharSetup.FormalFile(name, "Info.json"));
            if (meta != null)
            {
                fr.Cells = (int)Num(meta, "cells", 5);
                fr.CellPx = (int)Num(meta, "cell_px", 409);
                fr.AtlasPx = (int)Num(meta, "atlas_px", 2048);
            }
            var hp = inst.GetComponent<HandPoser>() ?? inst.AddComponent<HandPoser>();
            hp.Set = AssetDatabase.LoadAssetAtPath<HandPoseSet>(PoseSetPath(name));
            var fm = inst.GetComponent<FormalModel>() ?? inst.AddComponent<FormalModel>();
            fm.Who = name;
            fm.HeadsTarget = name == "Taeo" ? 7.5f : 7.4f;
            if (meta != null)
            {
                fm.HeadsMeasured = (float)Num(meta, "heads", 0);
                fm.Height = (float)Num(meta, "height", 0);
                fm.ChinZ = (float)Num(meta, "chin_z", 0);
                fm.HeadScale = (float)Num(meta, "head_scale", 1);
                fm.BodyRescale = (float)Num(meta, "body_rescale", 1);
                fm.LidMm = (float)Num(meta, "lid_mm", 0);
                if (meta.TryGetValue("face_box_m", out var fb) && fb is List<object> l && l.Count >= 2) fm.FaceBox = new Vector2(Convert.ToSingle(l[0]), Convert.ToSingle(l[1]));
                fm.TrisLod0 = (int)Num(meta, "tris_lod0", 0);
                fm.TrisLod1 = (int)Num(meta, "tris_lod1", 0);
                fm.TrisLod2 = (int)Num(meta, "tris_lod2", 0);
                fm.TrisFace = (int)Num(meta, "tris_face", 0);
            }
            if (info != null && fm.Height <= 0f) fm.Height = (float)Num(info, "height", 0);
            Sockets(inst);
            Lods(inst);
        }

        /// 소켓 5(4-2 #8): 오른손·왼손(손바닥 가운데) · 머리(정수리 위) · 등(가슴 뒤) · 허리(골반 오른쪽 — 봉투 주머니)
        static void Sockets(GameObject inst)
        {
            var anim = inst.GetComponent<Animator>();
            Transform B(HumanBodyBones b, string fallback)
            {
                var t = anim != null && anim.isHuman ? anim.GetBoneTransform(b) : null;
                return t != null ? t : FindDeep(inst.transform, fallback);
            }
            var root = inst.transform;
            var head = B(HumanBodyBones.Head, "Head");
            var chest = B(HumanBodyBones.UpperChest, "Spine") ?? B(HumanBodyBones.Chest, "Spine01");
            var hips = B(HumanBodyBones.Hips, "Hips");
            float top = CharSetup.MainSkin(inst) is SkinnedMeshRenderer smr ? smr.sharedMesh.bounds.size.z * smr.transform.lossyScale.x : 1.74f;
            void Put(Transform parent, string name, Vector3 world)
            {
                if (parent == null) return;
                var old = parent.Find(name);
                var t = old != null ? old : new GameObject(name).transform;
                t.SetParent(parent, false);
                t.position = world;
                t.rotation = parent.rotation;
            }
            foreach (bool right in new[] { true, false })
            {
                var hand = FindDeep(root, right ? "RightHand" : "LeftHand");
                var palm = FindDeep(root, right ? "RightHandPalm" : "LeftHandPalm");
                var palmN = FindDeep(root, right ? "RightHandPalmN" : "LeftHandPalmN");
                var mid = FindDeep(root, right ? "RightHandMiddle1" : "LeftHandMiddle1");
                if (hand == null) continue;
                Vector3 c = mid != null ? Vector3.Lerp(hand.position, mid.position, 0.6f) : hand.position;
                if (palm != null && palmN != null) c += (palmN.position - palm.position).normalized * 0.025f;
                Put(hand, right ? FormalModel.SocketR : FormalModel.SocketL, c);
            }
            if (head != null) Put(head, FormalModel.SocketHead, new Vector3(head.position.x, root.position.y + top + 0.02f, head.position.z));
            if (chest != null) Put(chest, FormalModel.SocketBack, chest.position - root.forward * 0.16f);
            if (hips != null) Put(hips, FormalModel.SocketWaist, hips.position - root.right * 0.17f + root.forward * 0.02f);
        }

        /// LODGroup 3단: LOD0(몸 + 얼굴) 화면 높이 25% · LOD1 8% · LOD2 1%(그 아래 컬링). 임포터가 만든 LODGroup 이 있으면 그것을 고친다
        static void Lods(GameObject inst)
        {
            var rs = inst.GetComponentsInChildren<Renderer>(true);
            Renderer[] At(int k) => rs.Where(r => r.name == $"Body_LOD{k}" || r.name == $"Face_LOD{k}").ToArray();
            var lg = inst.GetComponentInChildren<LODGroup>(true) ?? inst.AddComponent<LODGroup>();
            lg.SetLODs(new[] { new LOD(0.25f, At(0)), new LOD(0.08f, At(1)), new LOD(0.01f, At(2)) });
            lg.fadeMode = LODFadeMode.None;
            lg.RecalculateBounds();
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var r = FindDeep(c, name); if (r != null) return r; }
            return null;
        }

        static Dictionary<string, object> Json(string path)
        {
            var full = Path.Combine(Path.GetDirectoryName(Application.dataPath), path);
            return File.Exists(full) ? MiniJson.Parse(File.ReadAllText(full)) as Dictionary<string, object> : null;
        }

        static double Num(Dictionary<string, object> d, string k, double def) => d != null && d.TryGetValue(k, out var v) && v != null ? Convert.ToDouble(v) : def;

        // ───────────────────────── 모델 인스턴스 만들기(플레이어·태오 공용)
        /// 정식 모델 FBX 인스턴스 + 재질(몸·얼굴) + Animator + Decorate
        public static GameObject Instance(string name, string fbx, string bodyMat, RuntimeAnimatorController ctrl, int seed)
        {
            var model = CharSetup.Load<GameObject>(fbx);
            var inst = (PrefabUtility.InstantiatePrefab(model) as GameObject) ?? Object.Instantiate(model);
            CharSetup.Paint(inst, AssetDatabase.LoadAssetAtPath<Material>(bodyMat), CharSetup.FaceMatFor(bodyMat));
            var anim = inst.GetComponent<Animator>() ?? inst.AddComponent<Animator>();
            anim.avatar = CharSetup.AvatarOf(fbx);
            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (CharSetup.IsFormal(model)) Decorate(inst, name, seed);
            return inst;
        }

        // ───────────────────────── ③ 태오 프리팹
        public static void BuildTaeoPrefab()
        {
            var baseCtrl = CharSetup.Load<RuntimeAnimatorController>(CharSetup.SiwooCtrl);
            var oc = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(TaeoRunCtrl);
            if (oc == null) { oc = new AnimatorOverrideController(baseCtrl); AssetDatabase.CreateAsset(oc, TaeoRunCtrl); }
            oc.runtimeAnimatorController = baseCtrl;
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            oc.GetOverrides(pairs);
            var taeoIdle = CharSetup.Clip(CharSetup.TaeoFbx, "Idle");
            for (int i = 0; i < pairs.Count; i++) if (pairs[i].Key != null && pairs[i].Key.name == "Idle") pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(pairs[i].Key, taeoIdle);
            oc.ApplyOverrides(pairs);
            EditorUtility.SetDirty(oc);
            Directory.CreateDirectory(Path.GetDirectoryName(TaeoPrefab));
            AssetDatabase.Refresh();
            var inst = Instance("Taeo", CharSetup.TaeoFbx, CharSetup.TaeoMat, oc, 31);
            inst.name = "Taeo";
            foreach (var t in inst.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 0;
            PrefabUtility.SaveAsPrefabAsset(inst, TaeoPrefab, out bool ok);
            Object.DestroyImmediate(inst);
            if (!ok) throw new Exception("태오 프리팹 저장 실패: " + TaeoPrefab);
            Debug.Log($"{Tag} 태오 프리팹 {TaeoPrefab}: 정식 모델(FaceRig · HandPoser · 소켓 5 · LOD 3) · 컨트롤러 {oc.name}(시우 걷기·달리기 + 태오 대기)");
        }
    }
}
