// 행인1의 메인이벤트 — 전투 클립 연결 (docs/08_M2_전투_설계.md 10-2·10-4, 11장 9단계)
// ① 적 정식 모델(깐족이·석 달·냉장고·스크럼 — tools/glb2fbx.py 손 셰이프 키 포함) Humanoid + 툰 재질
// ② 전투 클립 26개(Art/Clips/*.fbx — 대리 메시, 뼈 휴지 자세만) Humanoid: 수평 이동은 뽑아서 버림(몸은 제자리, 이동은 코드) · 높이·회전은 자세에 굽기 ·
//    잡기·밀기 / 쓰러짐·누움 / 일어서기 구간 / 무릎 꿇기 / 도발 1.4초 / 막는 프레임은 클립을 나눔
// ③ CombatClipProbe: 시우 아바타에 편집 모드로 재생해 타격 시각(치는 끝이 가슴에서 정면으로 가장 멀리 나간 때)·뻗은 거리를 잰다 → Anim/ClipTable.asset + CombatClips.json
// ④ 애니메이터: Siwoo.controller 에 전투 상태(+ 상체 막기 층), Enemy.controller(적 4명 공용 — Humanoid 리타깃) 새로
using System;
using System.Collections.Generic;
using System.Linq;
using Haengin.EditorTools;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Haengin.EditorGame
{
    public static class ClipSetup
    {
        const string Tag = "[ClipSetup]";
        public const string ClipDir = "Assets/_Project/Art/Clips";
        public const string EnemyDir = "Assets/_Project/Art/Characters/Enemy";
        public const string AnimDir = "Assets/_Project/Anim";
        public const string TablePath = AnimDir + "/ClipTable.asset";
        public const string JsonPath = AnimDir + "/CombatClips.json";
        public const string EnemyCtrl = AnimDir + "/Enemy.controller";
        public const string MaskPath = AnimDir + "/UpperBody.mask";
        public const string MatDir = "Assets/_Project/Materials";
        public static readonly string[] Enemies = { "Kkanjok", "Seokdal", "Naengjanggo", "Scrum" };
        const float Fps = 30f;      // glb2fbx.py 가 30fps 로 굽는다

        /// 파일 · 상태(첫 클립 이름) · 번호 · 반복 · 시험 끝(강·발·무릎 중 가장 멀리 나간 것으로 고름)
        static readonly (string file, int id, bool loop, bool strike)[] Files =
        {
            ("Stance", 89, true, false), ("FwdWalk", 21, true, false), ("BackWalk", 20, true, false),
            ("Jab", 191, false, true), ("Cross", 192, false, true), ("Hook", 193, false, true), ("Upper", 194, false, true), ("BigHook", 195, false, true),
            ("Kick", 209, false, true), ("FKick", 206, false, true), ("Knee", 211, false, true), ("PushFwd", 260, false, true), ("Tackle", 512, false, false),
            ("GrabPush", 259, false, false), ("Block", 138, false, false), ("Dodge", 156, false, false), ("HitFace", 174, false, false), ("HitBody", 178, false, false),
            ("Knockdown", 187, false, false), ("StandUp", 344, false, false), ("Kneel", 365, false, false), ("Stumble", 562, false, false),
            ("Charge", 510, false, false), ("Breath", 31, true, false), ("Victory", 403, false, false), ("Taunt", 88, false, false),
            // 2차(2026-10-06 c541edd): 366 쓰러짐(보통 다운 — 187 은 크게 날아갈 때만) · 128 양손 내려찍기(냉장고 큰 휘두르기, 태오 리그) · 525/526 웅크려 옆걸음(하체만)
            ("FallDown", 366, false, false), ("Smash", 128, false, true), ("SideL", 525, true, false), ("SideR", 526, true, false),
        };

        static string ClipPath(string f) => $"{ClipDir}/{f}.fbx";

        // ───────────────────────── 진입점(CombatSetup 이 부름)
        public static ClipTable Setup()
        {
            EnemyModels();
            var table = ImportClips();
            Controllers(table);
            AssetDatabase.SaveAssets();
            return table;
        }

        // ───────────────────────── ① 적 모델
        public static string EnemyFbx(string n) => $"{EnemyDir}/{n}.fbx";
        public static string EnemyTex(string n) => $"{EnemyDir}/{n}Tex.jpg";
        public static string EnemyMat(string n) => $"{MatDir}/M_{n}_Toon.mat";

        static void EnemyModels()
        {
            foreach (var n in Enemies)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(EnemyFbx(n)) == null) { Debug.LogWarning($"{Tag} 적 모델 없음: {EnemyFbx(n)}"); continue; }
                CharSetup.Texture(EnemyTex(n));
                CharSetup.Human(EnemyFbx(n), "Idle", -1, -1, true, null);
                if (n == "Naengjanggo" || n == "Scrum") DefaultArmLimits(EnemyFbx(n));
                CharSetup.Ground(EnemyFbx(n), "Idle", EnemyFbx(n));
                CharSetup.Toon(CharSetup.Load<GameObject>(EnemyFbx(n)), EnemyMat(n), EnemyTex(n));
                var mesh = CharSetup.Load<GameObject>(EnemyFbx(n)).GetComponentInChildren<SkinnedMeshRenderer>(true)?.sharedMesh;
                var b = mesh != null ? mesh.bounds.size : Vector3.zero;
                Debug.Log($"{Tag} 적 모델 {n}: Humanoid · 툰 재질 {EnemyMat(n)} · 셰이프 키 {(mesh != null ? mesh.blendShapeCount : -1)}개({(mesh != null ? string.Join(",", Enumerable.Range(0, mesh.blendShapeCount).Select(mesh.GetBlendShapeName)) : "")}) · 메시 경계 {b}");
            }
        }

        /// 덩치(냉장고·스크럼) 팔(08 4-8 · 11장 15단계 · 11-4): 처음엔 근육 범위(Arm Down-Up 아래 −38 · Front-Back 앞 75)를 줄였으나
        /// Editor/Game/ArmProbe 실측에서 Down-Up 은 효과 0, Front-Back 은 팔을 몸 쪽으로 당겨 오히려 더 닿았다(스크럼 15.1 → 17.7%).
        /// → 범위는 기본값으로 되돌리고, 재생 뒤 위팔을 몸 바깥으로 벌린다(FighterAnim.ArmSpread — 적 프리팹에 넣음).
        public const float BulkyArmSpread = 12f;

        static void DefaultArmLimits(string path)
        {
            var mi = (ModelImporter)AssetImporter.GetAtPath(path);
            var hd = mi.humanDescription;
            var hb = hd.human;
            bool changed = false;
            for (int i = 0; i < hb.Length; i++)
            {
                if (hb[i].humanName != "LeftUpperArm" && hb[i].humanName != "RightUpperArm") continue;
                var lim = hb[i].limit;
                if (lim.useDefaultValues) continue;
                lim.useDefaultValues = true;
                hb[i].limit = lim;
                changed = true;
            }
            if (!changed) return;
            hd.human = hb;
            mi.humanDescription = hd;
            mi.SaveAndReimport();
            Debug.Log($"{Tag} 덩치 팔 근육 범위 {System.IO.Path.GetFileName(path)}: 기본값으로 되돌림(벌림 {BulkyArmSpread}° 로 대신)");
        }

        // ───────────────────────── ② 클립 + ③ 측정
        static ModelImporterClipAnimation Make(string name, float first, float last, bool loop) => new ModelImporterClipAnimation
        {
            name = name,
            firstFrame = first,
            lastFrame = last,
            loopTime = loop,
            loopPose = false,
            wrapMode = loop ? WrapMode.Loop : WrapMode.ClampForever,
            lockRootRotation = true, keepOriginalOrientation = true,
            lockRootHeightY = true, keepOriginalPositionY = true, heightFromFeet = false,
            // 수평 이동은 굽지 않고 뽑아 둔다 → Animator.applyRootMotion 끔이라 버려짐: 몸이 늘 오브젝트 자리(무게중심 기준)에 있고 이동은 코드(자석·넉백·회피·돌진)
            lockRootPositionXZ = false, keepOriginalPositionXZ = false,
            maskType = ClipAnimationMaskType.None,
        };

        static (float first, float last) Take(string path)
        {
            var mi = (ModelImporter)AssetImporter.GetAtPath(path);
            var t = mi.defaultClipAnimations.FirstOrDefault() ?? throw new Exception(path + ": 테이크 없음");
            return (t.firstFrame, t.lastFrame);
        }

        sealed class Probe
        {
            public float Length;
            public float[] T;
            public Vector3[] Hips, Chest, LH, RH, LF, RF, LK, RK;
        }

        static Probe Sample(AnimationClip clip, int n = 120)
        {
            var model = CharSetup.Load<GameObject>(CharSetup.SiwooFbx);
            var avatar = CharSetup.AvatarOf(CharSetup.SiwooFbx);
            var go = UnityEngine.Object.Instantiate(model);
            var graph = PlayableGraph.Create("ClipSetup.Probe");
            bool started = false;
            var p = new Probe { Length = clip.length, T = new float[n + 1], Hips = new Vector3[n + 1], Chest = new Vector3[n + 1], LH = new Vector3[n + 1], RH = new Vector3[n + 1], LF = new Vector3[n + 1], RF = new Vector3[n + 1], LK = new Vector3[n + 1], RK = new Vector3[n + 1] };
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
                cp.SetApplyFootIK(false);
                output.SetSourcePlayable(cp);
                if (!AnimationMode.InAnimationMode()) { AnimationMode.StartAnimationMode(); started = true; }
                var root = go.transform;
                Transform B(HumanBodyBones b) => anim.GetBoneTransform(b);
                var chestB = B(HumanBodyBones.UpperChest) ?? B(HumanBodyBones.Chest);
                for (int i = 0; i <= n; i++)
                {
                    float t = Mathf.Min(clip.length * i / n, clip.length - 0.5f / Fps);    // 끝 = 길이 그대로면 첫 프레임으로 돌아가 잼(일어서기 끝이 누운 높이로 나왔음)
                    AnimationMode.BeginSampling();
                    AnimationMode.SamplePlayableGraph(graph, 0, t);
                    AnimationMode.EndSampling();
                    Vector3 L(Transform x) => root.InverseTransformPoint(x.position);
                    p.T[i] = t;
                    p.Hips[i] = L(B(HumanBodyBones.Hips)); p.Chest[i] = L(chestB);
                    p.LH[i] = L(B(HumanBodyBones.LeftHand)); p.RH[i] = L(B(HumanBodyBones.RightHand));
                    p.LF[i] = L(B(HumanBodyBones.LeftFoot)); p.RF[i] = L(B(HumanBodyBones.RightFoot));
                    p.LK[i] = L(B(HumanBodyBones.LeftLowerLeg)); p.RK[i] = L(B(HumanBodyBones.RightLowerLeg));
                }
            }
            finally
            {
                if (started) AnimationMode.StopAnimationMode();
                graph.Destroy();
                UnityEngine.Object.DestroyImmediate(go);
            }
            return p;
        }

        /// 치는 끝 = 손·발·무릎 중 가슴에서 정면으로 가장 멀리 나간 것. 앞 80% 안에서 그 최대 시각
        static (float hit, float reach, string limb) Strike(Probe p, float from = 0f, float to = 0.8f)
        {
            var limbs = new (string n, Vector3[] v)[] { ("왼손", p.LH), ("오른손", p.RH), ("왼발", p.LF), ("오른발", p.RF), ("왼무릎", p.LK), ("오른무릎", p.RK) };
            float best = float.MinValue, hit = 0f, reach = 0f;
            string limb = "";
            int n = p.T.Length;
            for (int i = 0; i < n; i++)
            {
                float u = p.T[i] / Mathf.Max(1e-4f, p.Length);
                if (u < from || u > to) continue;
                foreach (var (name, v) in limbs)
                {
                    float f = v[i].z - p.Chest[i].z;
                    if (f > best) { best = f; hit = p.T[i]; reach = v[i].z; limb = name; }     // 뻗은 거리 = 루트(= Fighter 위치) 기준 정면
                }
            }
            return (hit, reach, limb);
        }

        static ClipTable ImportClips()
        {
            var entries = new List<ClipTable.Entry>();
            var json = new List<string>();
            var siwoo = CharSetup.Load<GameObject>(CharSetup.SiwooFbx);
            var avatar = CharSetup.AvatarOf(CharSetup.SiwooFbx);
            foreach (var (file, id, loop, strike) in Files)
            {
                string path = ClipPath(file);
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { Debug.LogWarning($"{Tag} 클립 없음: {path}"); continue; }
                var (f0, f1) = Take(path);
                // 1차: 통째로 들여와 잰다
                CharSetup.Human(path, file, -1, -1, false, new[] { Make(file, f0, f1, loop) });
                var full = CharSetup.Clip(path, file);
                var p = Sample(full);
                Dump(file, p);
                float len = full.length;
                float Fr(float t) => f0 + Mathf.Round(t * Fps);
                var subs = new List<(ModelImporterClipAnimation c, float hit, float reach, string note)>();
                switch (file)
                {
                    case "GrabPush":
                    {
                        // 손이 처음 가장 멀리 닿는 때(앞 60%) = 잡기 끝, 그 뒤 = 밀기(밀기 타격 = 나머지에서 손이 가장 멀리)
                        // 뒷부분(3초 동안 몸을 낮춰 몰아붙이기)은 쓰지 않는다 — 하체 밀기·벽 러시 어깨 밀기 = 260 PushFwd(2026-10-06 결정)
                        var g = HandsForward(p, 0f, 0.6f);
                        subs.Add((Make("Grab", f0, Fr(g.t + 0.1f), false), g.t, g.f, "잡기(손 닿음)"));
                        break;
                    }
                    case "Knockdown":
                    case "FallDown":
                    {
                        // 보통 다운 = 366(무릎이 꺾이며 뒤로 눕기), 크게 날아가는 다운(기세 액션 되받기) = 187
                        string fall = file == "FallDown" ? "Fall" : "FallBig", lie = file == "FallDown" ? "Lie" : "LieBig";
                        float land = Landing(p);
                        subs.Add((Make(fall, f0, Fr(Mathf.Min(len, land + 0.15f)), false), land, 0f, "쓰러짐(바닥 닿음)"));
                        subs.Add((Make(lie, Fr(Mathf.Max(0f, len - 0.3f)), f1, true), 0f, 0f, "누움(끝 0.3초 반복)"));
                        break;
                    }
                    case "StandUp":
                    {
                        var (a, b) = RiseWindow(p);
                        subs.Add((Make("StandUp", Fr(Mathf.Max(0f, a - 0.1f)), Fr(Mathf.Min(len, b + 0.1f)), false), 0f, 0f, $"일어서기 구간 {a:F2}~{b:F2}초"));
                        break;
                    }
                    case "Kneel":
                        subs.Add((Make("Kneel", f0, Fr(Mathf.Min(len, 0.3f)), true), 0f, 0f, "무릎 꿇음(앞 0.3초 반복 — 365 앞부분)"));
                        break;
                    case "Taunt":
                    {
                        float a = BusiestWindow(p, 1.4f);
                        subs.Add((Make("Taunt", Fr(a), Fr(Mathf.Min(len, a + 1.4f)), false), 0f, 0f, $"가슴 치기 1.4초 창 {a:F2}초부터"));
                        break;
                    }
                    case "Block":
                    {
                        float a = HandsHighest(p);
                        subs.Add((Make("Guard", Fr(a), Fr(a) + 1f, true), a, 0f, $"막는 프레임 {a:F2}초(두 손이 가장 높을 때)"));
                        break;
                    }
                    case "Dodge":
                    {
                        float a = HipsFarthest(p);
                        subs.Add((Make("Dodge", f0, f1, false), a, 0f, $"가장 멀리 피한 때 {a:F2}초"));
                        break;
                    }
                    case "Smash":
                    {
                        // 양손 내려찍기: '정면으로 가장 멀리'는 손이 머리 높이일 때라 이르다 → 손이 꼭대기를 지나 가슴 높이로 내려온 때(11-4)
                        var s = SmashHit(p);
                        subs.Add((Make(file, f0, f1, loop), s.hit, s.reach, $"내려찍기(손이 꼭대기 {s.top:F2}초 → 가슴 높이 {s.hit:F2}초)"));
                        break;
                    }
                    default:
                    {
                        (float hit, float reach, string limb) s = strike ? Strike(p) : (0f, 0f, "");
                        subs.Add((Make(file, f0, f1, loop), s.hit, s.reach, strike ? $"치는 끝 {s.limb}" : ""));
                        break;
                    }
                }
                CharSetup.Human(path, file, -1, -1, false, subs.Select(x => x.c).ToArray());
                for (int i = 0; i < subs.Count; i++)
                {
                    try { CharSetup.Ground(path, subs[i].c.name, CharSetup.SiwooFbx, false, i); }
                    catch (Exception e) { Debug.LogWarning($"{Tag} 발바닥 맞춤 실패 {file}/{subs[i].c.name}: {e.Message}"); }
                    var clip = CharSetup.Clip(path, subs[i].c.name);
                    var e2 = new ClipTable.Entry { State = subs[i].c.name, Id = id, Length = clip.length, Hit = subs[i].hit, Reach = subs[i].reach };
                    entries.Add(e2);
                    json.Add($"  {{\"state\": \"{e2.State}\", \"id\": {id}, \"length\": {e2.Length:F3}, \"hit\": {e2.Hit:F3}, \"reach\": {e2.Reach:F3}, \"note\": \"{subs[i].note}\"}}");
                    Debug.Log($"{Tag} 클립 {file} → '{e2.State}'({id}) 길이 {e2.Length:F2}s{(e2.Hit > 0 ? $" · 기준 시각 {e2.Hit:F3}s" : "")}{(e2.Reach != 0 ? $" · 뻗은 거리 {e2.Reach:F2}m" : "")} {subs[i].note}");
                }
            }
            var table = AssetDatabase.LoadAssetAtPath<ClipTable>(TablePath);
            if (table == null) { table = ScriptableObject.CreateInstance<ClipTable>(); AssetDatabase.CreateAsset(table, TablePath); }
            table.Entries = entries.ToArray();
            EditorUtility.SetDirty(table);
            System.IO.File.WriteAllText(JsonPath, "[\n" + string.Join(",\n", json) + "\n]\n");
            AssetDatabase.ImportAsset(JsonPath);
            RateReport(table);
            return table;
        }

        /// 표의 발생 f 와 클립 타격 시각으로 재생 배율(08 10-4: 0.7~1.6 밖이면 경고)
        static void RateReport(ClipTable t)
        {
            var moves = MoveSet.CreateDefault().All.Concat(new[] { MoveLib.KkOneTwo(), MoveLib.KkOneTwo().Followup, MoveLib.KkCharge(), MoveLib.SdJab(), MoveLib.SdCounter(), MoveLib.NjSwing(), MoveLib.NjKick(), MoveLib.NjHug() });
            var lines = new List<string>();
            foreach (var m in moves)
            {
                string st = ClipTable.StateFor(m);
                if (st == null || !t.TryGet(st, out var e) || e.Hit <= 0f) continue;
                double toHit = m.ChargeTime > 0f ? m.ActiveStart : m.PreTime + m.ActiveStart;
                float rate = (float)(e.Hit / toHit);
                string warn = rate < 0.7f || rate > 1.6f ? " ⚠" : "";
                string play = rate > 1.6f ? $" → ×1.60 + 앞 {e.Hit - 1.6f * toHit:F2}s 건너뜀" : "";
                lines.Add($"{m.Label} {st} ×{rate:F2}{warn}{play}(클립 {e.Hit:F2}s → {toHit:F3}s) 뻗음 {e.Reach:F2}m / 사거리 {m.Range:F1}m");
            }
            Debug.Log($"{Tag} 재생 배율(클립 타격 시각 ÷ 예고·발생): " + string.Join(" | ", lines));
        }

        /// 측정 곡선을 Logs/ClipProbe/<클립>.csv 로(커밋 안 됨 — 판정 손볼 때 확인용)
        static void Dump(string file, Probe p)
        {
            try
            {
                string dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "Logs", "ClipProbe");
                System.IO.Directory.CreateDirectory(dir);
                var sb = new System.Text.StringBuilder("t,hipY,chestY,hipZ,LHf,RHf,LFf,RFf,LKf,RKf,LHy,RHy" + "\n");
                var ci = System.Globalization.CultureInfo.InvariantCulture;
                for (int i = 0; i < p.T.Length; i++)
                    sb.AppendLine(string.Join(",", new[] { p.T[i], p.Hips[i].y, p.Chest[i].y, p.Hips[i].z, p.LH[i].z - p.Chest[i].z, p.RH[i].z - p.Chest[i].z, p.LF[i].z - p.Chest[i].z, p.RF[i].z - p.Chest[i].z, p.LK[i].z - p.Chest[i].z, p.RK[i].z - p.Chest[i].z, p.LH[i].y, p.RH[i].y }.Select(x => x.ToString("F3", ci))));
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, file + ".csv"), sb.ToString());
            }
            catch (Exception e) { Debug.LogWarning($"{Tag} 곡선 저장 실패 {file}: {e.Message}"); }
        }

        static (float hit, float reach, float top) SmashHit(Probe p)
        {
            int n = p.T.Length, iTop = 0;
            float best = float.MinValue;
            for (int i = 0; i < n; i++) { float y = Mathf.Max(p.LH[i].y, p.RH[i].y); if (y > best) { best = y; iTop = i; } }
            for (int i = iTop; i < n; i++)
                if (Mathf.Max(p.LH[i].y, p.RH[i].y) <= p.Chest[i].y)
                    return (p.T[i], Mathf.Max(p.LH[i].z, p.RH[i].z), p.T[iTop]);
            return (p.T[n - 1], 0f, p.T[iTop]);
        }

        /// 두 손이 가슴에서 가장 멀리 나간 때와 그때 손이 루트에서 나간 거리
        static (float t, float f) HandsForward(Probe p, float from, float to)
        {
            float best = float.MinValue, bt = 0f, reach = 0f;
            for (int i = 0; i < p.T.Length; i++)
            {
                float u = p.T[i] / Mathf.Max(1e-4f, p.Length);
                if (u < from || u > to) continue;
                float f = Mathf.Max(p.LH[i].z, p.RH[i].z) - p.Chest[i].z;
                if (f > best) { best = f; bt = p.T[i]; reach = Mathf.Max(p.LH[i].z, p.RH[i].z); }
            }
            return (bt, reach);
        }

        static float Landing(Probe p)
        {
            float min = p.Hips.Min(h => h.y);
            // 바닥 닿음 = 골반이 가장 낮은 높이 + 8cm 안으로 처음 들어온 때(366 은 누운 뒤에도 골반이 2~5cm 오르내려 5cm 로는 0.7초 늦게 잡혔다)
            for (int i = 0; i < p.T.Length; i++) if (p.Hips[i].y <= min + 0.08f) return p.T[i];
            return p.Length;
        }

        /// 일어서기 구간: 골반 높이가 (누운 높이 → 선 높이)의 20% 를 넘는 때 ~ 95% 에 닿는 때.
        /// 344 는 누움(0~1.9초) → 앉아 버팀(1.9~4.4초) → 일어섬(4.4~6.0초) → 대기라, 앉는 부분은 빼고 일어서는 부분만 쓴다(0.85초에 맞춰 ×1.8 안팎)
        static (float a, float b) RiseWindow(Probe p)
        {
            float lo = p.Hips.Take(Mathf.Max(1, p.Hips.Length / 8)).Min(h => h.y), hi = p.Hips.Skip(p.Hips.Length * 3 / 4).Max(h => h.y);
            float a = 0f, b = p.Length;
            for (int i = 0; i < p.T.Length; i++) if (p.Hips[i].y >= lo + (hi - lo) * 0.2f) { a = p.T[i]; break; }
            for (int i = 0; i < p.T.Length; i++) if (p.T[i] > a && p.Hips[i].y >= lo + (hi - lo) * 0.95f) { b = p.T[i]; break; }
            Debug.Log($"{Tag} 일어서기: 골반 높이 누움 {lo:F2}m → 섬 {hi:F2}m · 20%~95% 구간 {a:F2}~{b:F2}초");
            return (a, b);
        }

        static float BusiestWindow(Probe p, float w)
        {
            float best = -1f, bt = 0f;
            int n = p.T.Length;
            for (int i = 0; i < n; i++)
            {
                if (p.T[i] + w > p.Length + 1e-3f) break;
                float s = 0f;
                for (int j = i + 1; j < n && p.T[j] <= p.T[i] + w; j++)
                    s += (p.LH[j] - p.LH[j - 1]).magnitude + (p.RH[j] - p.RH[j - 1]).magnitude;
                if (s > best) { best = s; bt = p.T[i]; }
            }
            return bt;
        }

        static float HandsHighest(Probe p)
        {
            float best = float.MinValue, bt = 0f;
            for (int i = 0; i < p.T.Length; i++)
            {
                float y = p.LH[i].y + p.RH[i].y;
                if (y > best) { best = y; bt = p.T[i]; }
            }
            return bt;
        }

        static float HipsFarthest(Probe p)
        {
            float best = -1f, bt = 0f;
            for (int i = 0; i < p.T.Length; i++)
            {
                float d = new Vector2(p.Hips[i].x - p.Hips[0].x, p.Hips[i].z - p.Hips[0].z).magnitude;
                if (d > best) { best = d; bt = p.T[i]; }
            }
            return bt;
        }

        // ───────────────────────── ④ 애니메이터
        static void Controllers(ClipTable table)
        {
            var mask = UpperMask();
            var siwoo = AssetDatabase.LoadAssetAtPath<AnimatorController>(CharSetup.SiwooCtrl) ?? throw new Exception("시우 컨트롤러 없음(CharSetup 먼저)");
            // 걷기·달리기 블렌드 트리 배율(CharSetup 이 잰 것)을 적 Move 에 그대로
            var move = siwoo.layers[0].stateMachine.states.Select(s => s.state).First(s => s.name == CharSetup.MoveState);
            var bt = (BlendTree)move.motion;
            var enemy = AssetDatabase.LoadAssetAtPath<AnimatorController>(EnemyCtrl) ?? AnimatorController.CreateAnimatorControllerAtPath(EnemyCtrl);
            EnemyMove(enemy, bt);
            var (kf, kb) = CombatWalkScales();
            foreach (var c in new[] { siwoo, enemy })
            {
                Param(c, "AtkRate", 1f);
                Param(c, "CX", 0f);
                Param(c, "CY", 0f);
                var sm = c.layers[0].stateMachine;
                CombatMoveState(c, sm, kf, kb);
                foreach (var e in table.Entries)
                {
                    if (e.State == "Stance" || e.State == "FwdWalk" || e.State == "BackWalk" || e.State == "Guard" || e.State == "SideL" || e.State == "SideR") continue;
                    var clip = FindClip(e.State);
                    if (clip == null) continue;
                    var st = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == e.State) ?? sm.AddState(e.State);
                    st.motion = clip;
                    st.speed = 1f;
                    st.speedParameter = "AtkRate";
                    st.speedParameterActive = true;
                    st.iKOnFeet = true;
                    st.writeDefaultValues = true;
                    EditorUtility.SetDirty(st);
                }
                GuardLayer(c, mask);
                SideUpperLayer(c, mask);
                EditorUtility.SetDirty(c);
                Debug.Log($"{Tag} 컨트롤러 {AssetDatabase.GetAssetPath(c)}: 상태 {string.Join(" ", c.layers[0].stateMachine.states.Select(s => s.state.name))} · 위층 {string.Join(", ", c.layers.Skip(1).Select(l => l.name))}");
            }
        }

        static AnimationClip FindClip(string state)
        {
            foreach (var (file, _, _, _) in Files)
            {
                var c = AssetDatabase.LoadAllAssetsAtPath(ClipPath(file)).OfType<AnimationClip>().FirstOrDefault(x => x.name == state && !x.name.StartsWith("__preview__"));
                if (c != null) return c;
            }
            return null;
        }

        /// 전투 걷기 재생 배율: 앞 1.6 · 뒤 1.3 m/s ÷ 클립 디딤발 속도
        static (float kf, float kb) CombatWalkScales()
        {
            var model = CharSetup.Load<GameObject>(CharSetup.SiwooFbx);
            var avatar = CharSetup.AvatarOf(CharSetup.SiwooFbx);
            var f = FindClip("FwdWalk");
            var b = FindClip("BackWalk");
            float vf = f != null ? Mathf.Abs(CharSetup.Measure(model, avatar, f).NaturalSpeed) : 1f;
            float vb = b != null ? Mathf.Abs(CharSetup.Measure(model, avatar, b).NaturalSpeed) : 1f;
            float kf = Mathf.Clamp(1.6f / Mathf.Max(0.2f, vf), 0.3f, 3f), kb = Mathf.Clamp(1.3f / Mathf.Max(0.2f, vb), 0.3f, 3f);
            Debug.Log($"{Tag} 전투 걷기: 앞 클립 디딤발 {vf:F2} m/s → ×{kf:F2}(1.6 m/s) · 뒤 {vb:F2} m/s → ×{kb:F2}(1.3 m/s)");
            return (kf, kb);
        }

        static void CombatMoveState(AnimatorController c, AnimatorStateMachine sm, float kf, float kb)
        {
            var st = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "CombatMove") ?? sm.AddState("CombatMove");
            if (!(st.motion is BlendTree tree))
            {
                tree = new BlendTree { name = "CombatMove", hideFlags = HideFlags.HideInHierarchy };
                AssetDatabase.AddObjectToAsset(tree, c);
                st.motion = tree;
            }
            tree.blendType = BlendTreeType.SimpleDirectional2D;
            tree.blendParameter = "CX";
            tree.blendParameterY = "CY";
            var stance = FindClip("Stance"); var fwd = FindClip("FwdWalk"); var back = FindClip("BackWalk");
            var sl = FindClip("SideL"); var sr = FindClip("SideR");
            // 옆 = 525/526 웅크려 옆걸음(하체만 — 상체는 SideUpper 층이 전투 자세로 덮음). 없으면 앞걸음을 옆 속도 배율로
            float kl = SideScale(sl, 1.04f), kr = SideScale(sr, 0.88f);
            tree.children = new[]
            {
                new ChildMotion { motion = stance, position = Vector2.zero, timeScale = 1f },
                new ChildMotion { motion = fwd, position = new Vector2(0f, 1f), timeScale = kf },
                new ChildMotion { motion = back, position = new Vector2(0f, -1f), timeScale = kb },
                new ChildMotion { motion = sr != null ? sr : fwd, position = new Vector2(1f, 0f), timeScale = sr != null ? kr : kf * 1.3f / 1.6f },
                new ChildMotion { motion = sl != null ? sl : fwd, position = new Vector2(-1f, 0f), timeScale = sl != null ? kl : kf * 1.3f / 1.6f },
            };
            st.iKOnFeet = true;
            st.writeDefaultValues = true;
            EditorUtility.SetDirty(tree); EditorUtility.SetDirty(st);
        }

        /// 옆걸음 재생 배율 = 옆 1.3 m/s ÷ 클립 옆 속도. 클립 루트 평균 속도(Humanoid 는 몸 크기 단위라 시우 골반 높이 0.92 를 곱함)가 이상하면
        /// Blender 로 잰 값(왼 1.22m/1.17초 = 1.04, 오른 1.14m/1.29초 = 0.88 m/s — 2026-10-06)
        static float SideScale(AnimationClip c, float measured)
        {
            if (c == null) return 1f;
            float v = Mathf.Abs(c.averageSpeed.x) * 0.92f;
            float use = v > 0.4f && v < 2.5f ? v : measured;
            float k = Mathf.Clamp(1.3f / use, 0.5f, 2.5f);
            Debug.Log($"{Tag} 옆걸음 {c.name}: 루트 평균 옆 속도 {v:F2} m/s(측정값 {measured:F2}) → 씀 {use:F2} · 배율 ×{k:F2}");
            return k;
        }

        /// 옆걸음 때 상체를 전투 자세로 덮는 층(무게는 FighterAnim 이 |CX| 로)
        static void SideUpperLayer(AnimatorController c, AvatarMask mask)
        {
            var stance = FindClip("Stance");
            if (stance == null) return;
            var layers = c.layers;
            int i = Array.FindIndex(layers, l => l.name == "SideUpper");
            if (i < 0)
            {
                c.AddLayer("SideUpper");
                layers = c.layers;
                i = layers.Length - 1;
            }
            layers[i].avatarMask = mask;
            layers[i].defaultWeight = 0f;
            layers[i].blendingMode = AnimatorLayerBlendingMode.Override;
            layers[i].iKPass = false;
            var sm = layers[i].stateMachine;
            var st = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "Stance") ?? sm.AddState("Stance");
            st.motion = stance;
            st.writeDefaultValues = true;
            sm.defaultState = st;
            c.layers = layers;
            EditorUtility.SetDirty(st);
        }

        static void EnemyMove(AnimatorController c, BlendTree siwooMove)
        {
            Param(c, CharSetup.PSpeed, 0f); Param(c, CharSetup.PBlend, 0f); Param(c, CharSetup.PRate, 1f);
            var sm = c.layers[0].stateMachine;
            var st = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == CharSetup.MoveState) ?? sm.AddState(CharSetup.MoveState);
            if (!(st.motion is BlendTree tree))
            {
                tree = new BlendTree { name = "Locomotion", hideFlags = HideFlags.HideInHierarchy };
                AssetDatabase.AddObjectToAsset(tree, c);
                st.motion = tree;
            }
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = CharSetup.PBlend;
            tree.useAutomaticThresholds = false;
            var ch = siwooMove.children.ToArray();
            // 대기 = 적 모델들의 Idle_3(243 — 깐족이 FBX 의 것, 모두 같은 동작)
            var idle = AssetDatabase.LoadAllAssetsAtPath(EnemyFbx("Kkanjok")).OfType<AnimationClip>().FirstOrDefault(x => x.name == "Idle");
            if (idle != null) ch[0].motion = idle;
            tree.children = ch;
            st.speedParameter = CharSetup.PRate;
            st.speedParameterActive = true;
            st.iKOnFeet = true;
            st.writeDefaultValues = true;
            sm.defaultState = st;
            EditorUtility.SetDirty(tree); EditorUtility.SetDirty(st);
        }

        static AvatarMask UpperMask()
        {
            var m = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
            if (m == null) { m = new AvatarMask(); AssetDatabase.CreateAsset(m, MaskPath); }
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            {
                var part = (AvatarMaskBodyPart)i;
                bool on = part == AvatarMaskBodyPart.Body || part == AvatarMaskBodyPart.Head || part == AvatarMaskBodyPart.LeftArm || part == AvatarMaskBodyPart.RightArm
                          || part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.RightFingers || part == AvatarMaskBodyPart.LeftHandIK || part == AvatarMaskBodyPart.RightHandIK;
                m.SetHumanoidBodyPartActive(part, on);
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        static void GuardLayer(AnimatorController c, AvatarMask mask)
        {
            var guard = FindClip("Guard");
            if (guard == null) return;
            var layers = c.layers;
            int i = Array.FindIndex(layers, l => l.name == "Guard");
            if (i < 0)
            {
                c.AddLayer("Guard");
                layers = c.layers;
                i = layers.Length - 1;
            }
            layers[i].avatarMask = mask;
            layers[i].defaultWeight = 0f;
            layers[i].blendingMode = AnimatorLayerBlendingMode.Override;
            layers[i].iKPass = false;
            var sm = layers[i].stateMachine;
            var st = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "Guard") ?? sm.AddState("Guard");
            st.motion = guard;
            st.writeDefaultValues = true;
            sm.defaultState = st;
            c.layers = layers;
            EditorUtility.SetDirty(st);
        }

        static void Param(AnimatorController c, string name, float def)
        {
            if (c.parameters.Any(p => p.name == name)) return;
            c.AddParameter(new AnimatorControllerParameter { name = name, type = AnimatorControllerParameterType.Float, defaultFloat = def });
        }
    }
}
