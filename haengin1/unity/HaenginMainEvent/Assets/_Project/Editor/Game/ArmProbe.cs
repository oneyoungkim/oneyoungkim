// 행인1의 메인이벤트 — 덩치 팔 파고듦 측정 (docs/08_M2_전투_설계.md 4-8 · 11장 15단계 · 11-4)
// 냉장고·스크럼 아바타를 '팔 근육 범위 후보'마다 새로 만들어(AvatarBuilder — 에셋은 안 바꿈) 적 컨트롤러의 클립 전부를 30점씩 편집 모드로 재생하고,
// 아래팔(팔꿈치→손목 4점)·주먹 가운데가 몸통 안으로 얼마나 들어갔는지 잰다.
//   몸통 = 바인드 포즈 메시에서 몸통 뼈(엉덩이·척추·가슴·윗가슴)에 주로 붙은 정점. 점을 가장 가까운 몸통 뼈 기준으로 바인드 자리로 옮긴 뒤,
//   같은 높이(±2cm) 몸통 정점 단면의 타원(가로·앞뒤 끝)에 넣어 본다. 깊이 > 0 = 아래팔 축이 몸 안(살 두께 약 4cm 를 생각하면 −0.04 부터 닿음).
// 실행: -executeMethod Haengin.EditorGame.ArmProbe.Run [-armshots <폴더>](그래픽 필요 — 가장 깊은 순간을 위·앞에서 찍음)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Haengin.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Haengin.EditorGame
{
    public static class ArmProbe
    {
        const string Tag = "[ArmProbe]";
        /// 후보: 팔 근육 범위(Down-Up 아래 끝 · Front-Back 앞 끝) + 재생 뒤 위팔을 몸 바깥으로 벌리는 각(spread, FighterAnim.ArmSpread 와 같은 계산)
        public static readonly (string name, float downMin, float frontMax, float spread)[] Variants =
        {
            ("기본(−60·100)", -60f, 100f, 0f), ("범위(−38·75)", -38f, 75f, 0f), ("범위(−15·100)", -15f, 100f, 0f),
            ("벌림 6°", -60f, 100f, 6f), ("벌림 12°(채택)", -60f, 100f, 12f), ("벌림 18°", -60f, 100f, 18f),
        };

        static string Arg(string n) { var a = Environment.GetCommandLineArgs(); for (int i = 0; i < a.Length - 1; i++) if (a[i] == n) return a[i + 1]; return null; }

        public static void Run()
        {
            int code = 0;
            try { Probe(Arg("-armshots")); }
            catch (Exception e) { Debug.LogError($"{Tag} 실패: {e}"); code = 1; }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        sealed class Torso
        {
            public Vector3[] V;                  // 몸통 정점(월드, 바인드 포즈 — 인스턴스를 원점에 둔 채 재생 전에 잼)
            public int[] BoneIdx;                // smr.bones 안 몸통 뼈 번호들
            public Matrix4x4[] BindL2W;          // 그 뼈들의 바인드 때 localToWorld(BoneIdx 순서)
        }

        public struct Result { public int Frames, Inside, Touch; public float MaxDepth; public string Worst; public float WorstT; public AnimationClip WorstClip; public Dictionary<string, int> PerClip; }

        static void Probe(string shotDir)
        {
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ClipSetup.EnemyCtrl);
            var clips = ctrl.animationClips.Where(c => c != null).Distinct().OrderBy(c => c.name).ToList();
            var csv = new StringBuilder("모델,후보,클립,프레임,몸 안,닿음,최대 깊이(cm)\n");
            var summary = new List<string>();
            foreach (var n in new[] { "Naengjanggo", "Scrum" })
            {
                string fbx = ClipSetup.EnemyFbx(n);
                var model = CharSetup.Load<GameObject>(fbx);
                var mi = (ModelImporter)AssetImporter.GetAtPath(fbx);
                var idle = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>($"{ClipSetup.AnimDir}/Enemy_{n}.overrideController");
                var useClips = new List<AnimationClip>(clips);
                if (idle != null) foreach (var c in idle.animationClips) if (c != null && !useClips.Contains(c)) useClips.Add(c);
                foreach (var v in Variants)
                {
                    var r = Measure(model, mi.humanDescription, v.downMin, v.frontMax, v.spread, useClips, csv, n, v.name, shotDir);
                    string top = string.Join(", ", r.PerClip.Where(kv => kv.Value > 0).OrderByDescending(kv => kv.Value).Take(4).Select(kv => $"{kv.Key} {kv.Value}"));
                    string line = $"{n} {v.name}: {r.Frames}프레임 중 아래팔·주먹 축이 몸 안 {r.Inside}({r.Inside * 100f / Mathf.Max(1, r.Frames):F1}%) · 살이 닿음(축 4cm 안) {r.Touch}({r.Touch * 100f / Mathf.Max(1, r.Frames):F1}%) · 가장 깊음 {r.MaxDepth * 100f:F1}cm({r.Worst}) | 많은 클립: {top}";
                    summary.Add(line);
                    Debug.Log($"{Tag} {line}");
                }
            }
            string dir = shotDir ?? Path.Combine(Path.GetDirectoryName(Application.dataPath), "Logs");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "arm_probe.csv"), csv.ToString(), new UTF8Encoding(true));
            File.WriteAllText(Path.Combine(dir, "arm_probe.txt"), string.Join("\n", summary), new UTF8Encoding(false));
        }

        static HumanDescription WithLimits(HumanDescription hd, float downMin, float frontMax)
        {
            var hb = hd.human.ToArray();
            for (int i = 0; i < hb.Length; i++)
            {
                if (hb[i].humanName != "LeftUpperArm" && hb[i].humanName != "RightUpperArm") continue;
                int bone = Array.IndexOf(HumanTrait.BoneName, hb[i].humanName);
                var lim = hb[i].limit;
                Vector3 mn = Vector3.zero, mx = Vector3.zero;
                for (int dof = 0; dof < 3; dof++)
                {
                    int mus = HumanTrait.MuscleFromBone(bone, dof);
                    if (mus < 0) continue;
                    mn[dof] = HumanTrait.GetMuscleDefaultMin(mus);
                    mx[dof] = HumanTrait.GetMuscleDefaultMax(mus);
                    string name = HumanTrait.MuscleName[mus];
                    if (name.Contains("Down-Up")) mn[dof] = downMin;
                    else if (name.Contains("Front-Back")) mx[dof] = frontMax;
                }
                lim.useDefaultValues = false;
                lim.min = mn; lim.max = mx; lim.center = Vector3.zero;
                if (lim.axisLength <= 0f) lim.axisLength = 0.28f;
                hb[i].limit = lim;
            }
            hd.human = hb;
            return hd;
        }

        static Result Measure(GameObject model, HumanDescription baseHd, float downMin, float frontMax, float spread, List<AnimationClip> clips, StringBuilder csv, string who, string vname, string shotDir)
        {
            var go = UnityEngine.Object.Instantiate(model);
            go.name = model.name;
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var res = new Result { PerClip = new Dictionary<string, int>(), Worst = "-", MaxDepth = -1f };
            var graph = PlayableGraph.Create("ArmProbe");
            bool started = false;
            try
            {
                var avatar = AvatarBuilder.BuildHumanAvatar(go, WithLimits(baseHd, downMin, frontMax));
                if (avatar == null || !avatar.isValid || !avatar.isHuman) throw new Exception($"{who} {vname}: 아바타 만들기 실패");
                var anim = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
                anim.avatar = avatar; anim.applyRootMotion = false; anim.runtimeAnimatorController = null; anim.Rebind();
                var smr = go.GetComponentInChildren<SkinnedMeshRenderer>(true);
                var torso = TorsoOf(smr, anim);
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var output = AnimationPlayableOutput.Create(graph, "out", anim);
                if (!AnimationMode.InAnimationMode()) { AnimationMode.StartAnimationMode(); started = true; }
                foreach (var clip in clips)
                {
                    var cp = AnimationClipPlayable.Create(graph, clip);
                    cp.SetApplyFootIK(false);
                    output.SetSourcePlayable(cp);
                    int inside = 0, touch = 0, frames = 0;
                    float maxD = -1f;
                    const int N = 30;
                    for (int i = 0; i <= N; i++)
                    {
                        float t = Mathf.Min(clip.length * i / N, clip.length - 1f / 120f);
                        AnimationMode.BeginSampling();
                        AnimationMode.SamplePlayableGraph(graph, 0, t);
                        AnimationMode.EndSampling();
                        if (spread > 0f) FighterAnim.SpreadArms(anim, spread);
                        float d = Depth(anim, smr, torso);
                        frames++;
                        if (d > 0f) inside++;
                        if (d > -0.04f) touch++;
                        if (d > maxD) maxD = d;
                        if (d > res.MaxDepth) { res.MaxDepth = d; res.Worst = $"{clip.name} {t:F2}초"; res.WorstClip = clip; res.WorstT = t; }
                    }
                    res.Frames += frames; res.Inside += inside; res.Touch += touch;
                    res.PerClip[clip.name] = touch;
                    csv.Append($"{who},{vname},{clip.name},{frames},{inside},{touch},{maxD * 100f:F1}\n");
                    cp.Destroy();
                }
                bool shoot = vname.StartsWith("기본") || vname.Contains("채택");
                if (shoot && !string.IsNullOrEmpty(shotDir) && !SystemInfo.graphicsDeviceType.ToString().Contains("Null"))
                {
                    Directory.CreateDirectory(shotDir);
                    foreach (var (clipName, t) in new[] { ("Stance", 0.5f), ("BigHook", 0.4f), ("HitFace", 0.66f), ("FKick", 1.05f) })
                    {
                        var clip = clips.FirstOrDefault(c => c.name == clipName);
                        if (clip == null) continue;
                        var cp = AnimationClipPlayable.Create(graph, clip);
                        cp.SetApplyFootIK(false);
                        output.SetSourcePlayable(cp);
                        AnimationMode.BeginSampling();
                        AnimationMode.SamplePlayableGraph(graph, 0, Mathf.Min(t, clip.length - 1f / 120f));
                        AnimationMode.EndSampling();
                        if (spread > 0f) FighterAnim.SpreadArms(anim, spread);
                        string safe = vname.Replace("(", "_").Replace(")", "").Replace("·", "_").Replace("−", "m");
                        Shots(anim, Path.Combine(shotDir, $"arm_{who}_{safe}_{clipName}"));
                        cp.Destroy();
                    }
                }
                UnityEngine.Object.DestroyImmediate(avatar);
            }
            finally
            {
                if (started) AnimationMode.StopAnimationMode();
                graph.Destroy();
                UnityEngine.Object.DestroyImmediate(go);
            }
            return res;
        }

        static Torso TorsoOf(SkinnedMeshRenderer smr, Animator anim)
        {
            var mesh = smr.sharedMesh;
            var bones = smr.bones;
            var set = new HashSet<int>();
            foreach (var hb in new[] { HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest })
            {
                var t = anim.GetBoneTransform(hb);
                if (t == null) continue;
                int k = Array.IndexOf(bones, t);
                if (k >= 0) set.Add(k);
            }
            var verts = mesh.vertices;
            var w = mesh.boneWeights;
            var m = smr.localToWorldMatrix;          // 메시 공간(Blender: cm · Z 위) → 월드(m · Y 위)
            var list = new List<Vector3>();
            for (int i = 0; i < verts.Length; i++)
            {
                var b = w[i];
                float tw = (set.Contains(b.boneIndex0) ? b.weight0 : 0f) + (set.Contains(b.boneIndex1) ? b.weight1 : 0f) + (set.Contains(b.boneIndex2) ? b.weight2 : 0f) + (set.Contains(b.boneIndex3) ? b.weight3 : 0f);
                if (tw >= 0.5f) list.Add(m.MultiplyPoint3x4(verts[i]));
            }
            var idx = set.ToArray();
            return new Torso { V = list.ToArray(), BoneIdx = idx, BindL2W = idx.Select(k => bones[k].localToWorldMatrix).ToArray() };
        }

        /// 아래팔 4점 + 주먹 가운데 중 가장 깊이 들어간 깊이(m, 음수 = 몸 밖까지 거리 근사)
        static float Depth(Animator anim, SkinnedMeshRenderer smr, Torso torso)
        {
            float best = -1f;
            var bones = smr.bones;
            foreach (var side in new[] { (HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand), (HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand) })
            {
                var e = anim.GetBoneTransform(side.Item1).position;
                var h = anim.GetBoneTransform(side.Item2).position;
                var dir = (h - e).normalized;
                var pts = new[] { Vector3.Lerp(e, h, 0.25f), Vector3.Lerp(e, h, 0.5f), Vector3.Lerp(e, h, 0.75f), h, h + dir * 0.08f };
                foreach (var p in pts)
                {
                    // 가장 가까운 몸통 뼈 기준으로 바인드 자리로
                    int bi = -1; float bd = float.MaxValue;
                    for (int j = 0; j < torso.BoneIdx.Length; j++) { float dd = (bones[torso.BoneIdx[j]].position - p).sqrMagnitude; if (dd < bd) { bd = dd; bi = j; } }
                    if (bi < 0) continue;
                    var q = torso.BindL2W[bi].MultiplyPoint3x4(bones[torso.BoneIdx[bi]].worldToLocalMatrix.MultiplyPoint3x4(p));
                    best = Mathf.Max(best, SliceDepth(torso.V, q));
                }
            }
            return best;
        }

        static float SliceDepth(Vector3[] v, Vector3 q)
        {
            float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
            int n = 0;
            for (int i = 0; i < v.Length; i++)
            {
                if (Mathf.Abs(v[i].y - q.y) > 0.02f) continue;     // 월드 m
                n++;
                if (v[i].x < x0) x0 = v[i].x; if (v[i].x > x1) x1 = v[i].x;
                if (v[i].z < z0) z0 = v[i].z; if (v[i].z > z1) z1 = v[i].z;
            }
            if (n < 8) return -1f;
            float cx = (x0 + x1) * 0.5f, cz = (z0 + z1) * 0.5f, rx = Mathf.Max(0.01f, (x1 - x0) * 0.5f), rz = Mathf.Max(0.01f, (z1 - z0) * 0.5f);
            float u = (q.x - cx) / rx, w = (q.z - cz) / rz;
            float r = Mathf.Sqrt(u * u + w * w);
            return (1f - r) * Mathf.Min(rx, rz);
        }

        // ───────────────────────── 사진(위 · 앞 비스듬)
        static void Shots(Animator anim, string pathNoExt)
        {
            var camGo = new GameObject("ArmProbeCam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.95f, 0.93f, 0.9f);
            cam.fieldOfView = 30f; cam.nearClipPlane = 0.05f;
            var chest = anim.GetBoneTransform(HumanBodyBones.Chest) ?? anim.GetBoneTransform(HumanBodyBones.Spine);
            var c = chest.position;
            try
            {
                cam.transform.SetPositionAndRotation(c + Vector3.up * 2.2f + Vector3.forward * 0.15f, Quaternion.LookRotation(Vector3.down, Vector3.forward));
                Render(cam, pathNoExt + "_top.png");
                var from = c + new Vector3(0.9f, 0.25f, 1.9f);
                cam.transform.SetPositionAndRotation(from, Quaternion.LookRotation(c - from));
                Render(cam, pathNoExt + "_front.png");
                from = c + new Vector3(2.1f, 0.1f, 0.15f);
                cam.transform.SetPositionAndRotation(from, Quaternion.LookRotation(c - from));
                Render(cam, pathNoExt + "_side.png");
            }
            finally { UnityEngine.Object.DestroyImmediate(camGo); }
        }

        static void Render(Camera cam, string path)
        {
            int w = 900, h = 900;
            var rt = new RenderTexture(new RenderTextureDescriptor(w, h, RenderTextureFormat.ARGB32, 24) { sRGB = true });
            rt.Create();
            var prev = RenderTexture.active;
            try
            {
                cam.targetTexture = rt;
                cam.Render(); cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
            }
            finally { cam.targetTexture = null; RenderTexture.active = prev; rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
        }
    }
}
