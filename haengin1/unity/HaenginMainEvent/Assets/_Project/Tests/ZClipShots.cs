// 행인1의 메인이벤트 — 9단계 눈으로 확인(docs/08_M2_전투_설계.md 11장 9단계 '띠 사진, 손 확대'), -c9shots <폴더> 를 줄 때만, -nographics 없이
// Scenes/CombatLab.unity(시우 = 실제 Player 프리팹, 허수아비 = 석 달 정식 모델)에서
//   seq9_<기술>/f_000.png + meta.csv : 옆 카메라 480×270 매 프레임(기술 클립 띠 사진 — 밖에서 PIL 로 묶음). meta = 프레임·상태·기술·판정 여부·배율
//   hand_<누구>_<상태>.png          : 손 확대(시우 탐색·전투·잡기, 적 4명 편 손·주먹·쥐기)
//   lineup_front.png / lineup_side.png : 시우 + 적 4명 줄 세움(키 비교)
//   seq9_fight/f_000.png            : F5 1:3(깐족이·석 달·냉장고, AI + 공격권) 게임 카메라 960×540 2프레임마다 — 시우는 단순 봇
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Haengin.Tests
{
    public sealed class ZClipShots
    {
        const float Dt = Lab.Dt;
        string dir;
        Camera main, side;
        PlayerCombat pc;
        Fighter dummy;
        CombatMode mode;
        readonly List<string> notes = new List<string>();

        static string Arg(string name)
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++)
                if (string.Equals(a[i], name, StringComparison.OrdinalIgnoreCase)) return a[i + 1];
            return null;
        }

        [UnityTest, Timeout(3600000)]
        public IEnumerator Z_ClipShots()
        {
            dir = Arg("-c9shots");
            if (string.IsNullOrEmpty(dir)) { Assert.Ignore("-c9shots <폴더> 를 줄 때만 찍는다"); yield break; }
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) { Assert.Ignore("그래픽 장치 없음(-nographics)"); yield break; }
#if UNITY_EDITOR
            UnityEditor.ShaderUtil.allowAsyncCompilation = false;
#endif
            Directory.CreateDirectory(dir);
            Time.captureDeltaTime = Dt;
            TimeFx.Reset();
            GameState.SetPaused(false);
            yield return Lab.UnloadOurs(default);
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ZCombatShots.ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            var scene = SceneManager.GetSceneByPath(ZCombatShots.ScenePath);
            if (!scene.IsValid() || !scene.isLoaded) { Assert.Ignore("CombatLab 장면 없음(CombatSetup.Build 먼저)"); yield break; }
            SceneManager.SetActiveScene(scene);
            yield return null;
            pc = UnityEngine.Object.FindAnyObjectByType<PlayerCombat>();
            mode = UnityEngine.Object.FindAnyObjectByType<CombatMode>();
            dummy = Fighter.All.FirstOrDefault(f => !f.IsPlayer);
            foreach (var p in UnityEngine.Object.FindObjectsByType<PInput>(FindObjectsSortMode.None)) p.enabled = false;
            main = Camera.main;
            Assert.NotNull(pc, "PlayerCombat"); Assert.NotNull(dummy, "허수아비"); Assert.NotNull(mode, "CombatMode"); Assert.NotNull(main, "카메라");
            var sgo = new GameObject("SideCam");
            side = sgo.AddComponent<Camera>();
            side.enabled = false;
            side.fieldOfView = 34f; side.nearClipPlane = 0.05f; side.farClipPlane = 200f;
            side.clearFlags = main.clearFlags; side.backgroundColor = main.backgroundColor;
            for (int i = 0; i < 90; i++) yield return null;

            // (1) 시우 기술 띠
            yield return Strip("jab", "잽(□)", new[] { (0, Btn.Light) });
            yield return Strip("cross", "크로스(□□)", new[] { (0, Btn.Light), (12, Btn.Light) });
            yield return Strip("hook", "훅(□□□)", new[] { (0, Btn.Light), (12, Btn.Light), (26, Btn.Light) });
            yield return Chain("crossend", "크로스 끝(□□□□)", new[] { Btn.Light, Btn.Light, Btn.Light, Btn.Light });
            yield return Strip("kick", "앞차기(△)", new[] { (0, Btn.Heavy) });
            yield return Strip("upper", "어퍼(□△)", new[] { (0, Btn.Light), (12, Btn.Heavy) });
            yield return Strip("stepknee", "스텝 무릎(□□△)", new[] { (0, Btn.Light), (12, Btn.Light), (26, Btn.Heavy) });
            yield return Chain("bighook", "큰 훅(□□□△)", new[] { Btn.Light, Btn.Light, Btn.Light, Btn.Heavy });
            yield return Strip("grab", "잡기 → 무릎 → 밀기(○□△)", new[] { (0, Btn.Grab), (30, Btn.Light), (60, Btn.Heavy) }, 0.55f);

            yield return SideStrip();

            // (2) 적 기술 띠(허수아비 = 석 달 모델이 적 기술을 씀) + 다운·기상
            pc.Me.DebugInvuln = true;
            yield return EnemyStrip("e_onetwo", "깐족이 원투", MoveLib.KkOneTwo(), 1.4f);
            yield return EnemyStrip("e_charge", "깐족이 달려들기", MoveLib.KkCharge(), 3.2f);
            yield return EnemyStrip("e_counter", "석 달 카운터 훅", MoveLib.SdCounter(), 1.4f);
            yield return EnemyStrip("e_swing", "냉장고 큰 휘두르기", MoveLib.NjSwing(), 1.5f);
            yield return EnemyStrip("e_kick", "냉장고 앞차기", MoveLib.NjKick(), 1.6f);
            yield return EnemyStrip("e_hug", "냉장고 껴안기", MoveLib.NjHug(), 1.4f);
            yield return DownStrip();
            pc.Me.DebugInvuln = false;

            // (3) 손 확대 · (4) 줄 세움
            yield return Hands();
            yield return Lineup();

            // (5) 1:3 실전(F5)
            yield return Fight();

            File.WriteAllText(Path.Combine(dir, "notes9.txt"), string.Join("\n", notes));
            Debug.Log("[M2Shots9]\n" + string.Join("\n", notes));
            UnityEngine.Object.Destroy(sgo);
            var input = UnityEngine.Object.FindAnyObjectByType<PInput>(FindObjectsInactive.Include);
            if (input != null && input.Actions != null) input.Actions.Disable();
            TimeFx.Reset();
            yield return Lab.FreshScene();
            Time.captureDeltaTime = 0f;
        }

        IEnumerator Reset(Vector3 me, float meYaw, Vector3 dm, float dmYaw)
        {
            if (!mode.Active) mode.Begin(0f);
            pc.Me.ResetFighter(); dummy.ResetFighter();
            ((PlayerBody)pc.Me.Body).Place(me, meYaw);
            dummy.Body.Place(dm, dmYaw);
            if (pc.Lock != null) pc.Lock.Unlock();
            pc.CombatCam.Snap();
            for (int i = 0; i < 50; i++) yield return null;
        }

        void SideAt(float x, Vector3 look)
        {
            side.transform.position = new Vector3(x, 1.25f, look.z + 0.15f);
            side.transform.rotation = Quaternion.LookRotation(look - side.transform.position);
        }

        static FighterAnim AnimOf(Fighter f) => f.GetComponentInChildren<FighterAnim>();

        /// 띠: 처음부터 마지막 기술이 끝나고 20f 까지 매 프레임 — meta.csv 에 상태·판정을 적음
        IEnumerator Strip(string key, string label, (int f, Btn b)[] seq, float gap = 0.7f)
        {
            yield return Reset(new Vector3(0f, 0f, -gap), 0f, new Vector3(0f, 0f, gap), 180f);
            SideAt(4.2f, new Vector3(0f, 1.0f, 0f));
            var sd = Path.Combine(dir, "seq9_" + key);
            Directory.CreateDirectory(sd);
            var meta = new List<string> { "f,state,move,active,rate,clip" };
            var fa = AnimOf(pc.Me);
            int last = seq.Max(s => s.f), endAt = -1, n = 0;
            var states = new List<string>();
            for (int i = 0; i < 260; i++)
            {
                foreach (var s in seq) if (s.f == i) pc.Press(s.b);
                yield return null;
                var r = pc.Me.Run;
                meta.Add(Meta(n, fa, r, r?.Move?.Label));
                if (fa != null && (states.Count == 0 || states[states.Count - 1] != fa.Current)) states.Add(fa.Current);
                Shot(side, Path.Combine(sd, $"f_{n:000}.png"), 480, 270, 1); n++;
                if (i > last + 4 && endAt < 0 && pc.Me.State == Fighter.Phase.Free && pc.Held == null) endAt = i;
                if (endAt >= 0 && i >= endAt + 20) break;
            }
            File.WriteAllLines(Path.Combine(sd, "meta.csv"), meta);
            notes.Add($"띠 {label}: {sd} {n}장 · 상태 {string.Join(" → ", states)}");
        }

        /// 이어 누르기: 앞 기술 판정이 끝나면 다음 버튼(버퍼가 연결 창에서 꺼냄) — 고정 프레임으로 누르면 4타째가 버퍼 밖이었음
        IEnumerator Chain(string key, string label, Btn[] chain)
        {
            yield return Reset(new Vector3(0f, 0f, -0.7f), 0f, new Vector3(0f, 0f, 0.7f), 180f);
            SideAt(4.2f, new Vector3(0f, 1.0f, 0.3f));
            var sd = Path.Combine(dir, "seq9_" + key);
            Directory.CreateDirectory(sd);
            var meta = new List<string> { "f,state,move,active,rate,clip" };
            var fa = AnimOf(pc.Me);
            var states = new List<string>();
            int next = 0, n = 0, endAt = -1;
            AttackRun pressedOn = null;
            for (int i = 0; i < 300; i++)
            {
                var cr = pc.Me.Run;
                if (next == 0 || (next < chain.Length && cr != null && cr != pressedOn && cr.T >= cr.Move.ActiveEnd))
                {
                    pc.Press(chain[next]);
                    pressedOn = cr;
                    next++;
                }
                yield return null;
                var r = pc.Me.Run;
                meta.Add(Meta(n, fa, r, r?.Move?.Label));
                if (fa != null && (states.Count == 0 || states[states.Count - 1] != fa.Current)) states.Add(fa.Current);
                Shot(side, Path.Combine(sd, $"f_{n:000}.png"), 480, 270, 1); n++;
                if (next >= chain.Length && endAt < 0 && i > 10 && pc.Me.State == Fighter.Phase.Free) endAt = i;
                if (endAt >= 0 && i >= endAt + 20) break;
            }
            File.WriteAllLines(Path.Combine(sd, "meta.csv"), meta);
            notes.Add($"띠 {label}: {sd} {n}장 · 상태 {string.Join(" → ", states)}");
        }

        /// meta.csv 한 줄: 클립 시각은 '클립 초'(정규화 시간 × 표의 클립 길이 — 상태 길이에는 배율이 곱해져 있어 안 씀)
        static string Meta(int n, FighterAnim fa, AttackRun r, string what)
        {
            float clip = 0f;
            if (fa != null && fa.Animator != null)
            {
                var a = fa.Animator;
                int h = Animator.StringToHash(fa.Current ?? "");
                var info = a.IsInTransition(0) && a.GetNextAnimatorStateInfo(0).shortNameHash == h ? a.GetNextAnimatorStateInfo(0) : a.GetCurrentAnimatorStateInfo(0);
                float len = fa.Clips != null && fa.Current != null && fa.Clips.TryGet(fa.Current, out var e) ? e.Length : info.length;
                clip = info.normalizedTime * len;
                if (float.IsNaN(clip) || float.IsInfinity(clip)) clip = 0f;
            }
            return $"{n},{fa?.Current},{what},{(r != null && r.ActiveNow ? 1 : 0)},{(fa != null ? fa.Rate : 0f):F2},{clip:F3}";
        }

        /// 옆걸음(525/526 하체 + 전투 자세 상체 층): 락온한 채 오른쪽 1초 → 왼쪽 1초, 비스듬히 뒤에서 2프레임마다
        IEnumerator SideStrip()
        {
            yield return Reset(new Vector3(0f, 0f, -0.9f), 0f, new Vector3(0f, 0f, 1.1f), 180f);
            pc.Lock.Set(dummy);
            var sd = Path.Combine(dir, "seq9_side");
            Directory.CreateDirectory(sd);
            var meta = new List<string> { "f,state,move,active,rate,clip,cx,side" };
            var fa = AnimOf(pc.Me);
            side.fieldOfView = 40f;
            int n = 0;
            for (int i = 0; i < 150; i++)
            {
                if (i == 10) pc.SetStickWorld(Vector3.right, 1f);
                if (i == 75) pc.SetStickWorld(Vector3.left, 1f);
                if (i == 140) pc.SetStickWorld(Vector3.zero, 0f);
                yield return null;
                var p = pc.Me.Position;
                side.transform.position = p + new Vector3(1.6f, 1.5f, -3.4f);
                side.transform.rotation = Quaternion.LookRotation(p + Vector3.up * 0.9f - side.transform.position);
                var a = fa != null ? fa.Animator : null;
                float cx = a != null ? a.GetFloat(FighterAnim.HCX) : 0f;
                float sw = a != null && a.layerCount > FighterAnim.SideLayer ? a.GetLayerWeight(FighterAnim.SideLayer) : -1f;
                meta.Add(Meta(n, fa, null, "옆걸음") + $",{cx:F2},{sw:F2}");
                if (i % 2 == 0) { Shot(side, Path.Combine(sd, $"f_{n:000}.png"), 480, 270, 1); n++; }
            }
            side.fieldOfView = 34f;
            File.WriteAllLines(Path.Combine(sd, "meta.csv"), meta);
            notes.Add($"띠 옆걸음(오른쪽 → 왼쪽, 2프레임마다): {sd} {n}장 · 이동 {pc.Me.Position}");
        }

        IEnumerator EnemyStrip(string key, string label, MoveDef m, float gap)
        {
            yield return Reset(new Vector3(0f, 0f, -0.7f), 0f, new Vector3(0f, 0f, -0.7f + gap), 180f);
            SideAt(4.2f + Mathf.Max(0f, gap - 1.5f), new Vector3(0f, 1.0f, -0.7f + gap * 0.5f));
            var sd = Path.Combine(dir, "seq9_" + key);
            Directory.CreateDirectory(sd);
            var meta = new List<string> { "f,state,move,active,rate,clip" };
            var fa = AnimOf(dummy);
            var states = new List<string>();
            int n = 0, endAt = -1;
            for (int i = 0; i < 300; i++)
            {
                if (i == 4) dummy.StartAttack(m, pc.Me, -m.PreTime);
                yield return null;
                var r = dummy.Run;
                meta.Add(Meta(n, fa, r, r?.Move?.Label));
                if (fa != null && (states.Count == 0 || states[states.Count - 1] != fa.Current)) states.Add(fa.Current);
                Shot(side, Path.Combine(sd, $"f_{n:000}.png"), 480, 270, 1); n++;
                if (i > 8 && endAt < 0 && dummy.Run == null && dummy.State == Fighter.Phase.Free) endAt = i;
                if (endAt >= 0 && i >= endAt + 16) break;
            }
            File.WriteAllLines(Path.Combine(sd, "meta.csv"), meta);
            notes.Add($"띠 {label}: {sd} {n}장 · 상태 {string.Join(" → ", states)}");
        }

        IEnumerator DownStrip()
        {
            yield return Reset(new Vector3(0f, 0f, -0.7f), 0f, new Vector3(0f, 0f, 0.7f), 180f);
            SideAt(4.6f, new Vector3(0f, 0.8f, 0.9f));
            var sd = Path.Combine(dir, "seq9_down");
            Directory.CreateDirectory(sd);
            var meta = new List<string> { "f,state,move,active,rate,clip" };
            var fa = AnimOf(dummy);
            var states = new List<string>();
            int n = 0, freeAt = -1;
            for (int i = 0; i < 400; i++)
            {
                if (i == 4) dummy.StartDown(Vector3.forward, 0.8f);
                yield return null;
                meta.Add(Meta(n, fa, null, dummy.State.ToString()));
                if (fa != null && (states.Count == 0 || states[states.Count - 1] != fa.Current)) states.Add(fa.Current);
                if (i % 2 == 0) { Shot(side, Path.Combine(sd, $"f_{n:000}.png"), 480, 270, 1); n++; }
                if (i > 30 && freeAt < 0 && dummy.State == Fighter.Phase.Free) freeAt = i;
                if (freeAt >= 0 && i >= freeAt + 24) break;
            }
            File.WriteAllLines(Path.Combine(sd, "meta.csv"), meta);
            notes.Add($"띠 다운·기상(석 달 모델, 2프레임마다): {sd} {n}장 · 상태 {string.Join(" → ", states)}");
        }

        // ───────────────────────── 손 확대
        void HandCam(Animator a, bool left, Transform body)
        {
            var hand = a.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            var mid = a.GetBoneTransform(left ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
            var hp = hand.position;
            var tip = mid != null ? mid.position : hp + (hp - a.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm).position).normalized * 0.08f;
            var c = (hp + tip) * 0.5f + (hp - (hand.parent != null ? hand.parent.position : hp)).normalized * 0.04f;
            var outward = (left ? -1f : 1f) * body.right;
            var lower = a.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
            float dist = lower != null ? Mathf.Clamp((hp - lower.position).magnitude * 1.7f, 0.38f, 0.7f) : 0.42f;
            side.fieldOfView = 24f;
            side.transform.position = c + outward * dist + body.forward * dist * 0.43f + Vector3.up * 0.06f;
            side.transform.rotation = Quaternion.LookRotation(c - side.transform.position);
        }

        IEnumerator Hands()
        {
            var fa = AnimOf(pc.Me);
            var a = fa.Animator;
            var hs = pc.GetComponentInChildren<HandShape>();
            var body = pc.Me.transform;
            yield return Reset(new Vector3(0f, 0f, -0.7f), 0f, new Vector3(0f, 0f, 0.1f), 180f);
            // 탐색
            mode.End();
            dummy.Body.Place(new Vector3(0f, 0f, 3.5f), 180f);
            for (int i = 0; i < 60; i++) yield return null;
            HandCam(a, false, body);
            notes.Add($"손 시우 탐색(오른손, Fist_R {hs.Weight("Fist_R"):F0}): " + Shot(side, Path.Combine(dir, "hand_siwoo_explore.png"), 960, 720, 2));
            // 전투
            mode.Begin(0f);
            for (int i = 0; i < 40; i++) yield return null;
            HandCam(a, false, body);
            notes.Add($"손 시우 전투(오른손, Fist_R {hs.Weight("Fist_R"):F0}): " + Shot(side, Path.Combine(dir, "hand_siwoo_fist.png"), 960, 720, 2));
            HandCam(a, true, body);
            notes.Add($"손 시우 전투(왼손, Fist_L {hs.Weight("Fist_L"):F0}): " + Shot(side, Path.Combine(dir, "hand_siwoo_fist_l.png"), 960, 720, 2));
            // 잡기(왼손 쥐기)
            yield return Reset(new Vector3(0f, 0f, -0.7f), 0f, new Vector3(0f, 0f, 0.1f), 180f);
            pc.Press(Btn.Grab);
            for (int i = 0; i < 26; i++) yield return null;
            HandCam(a, true, body);
            notes.Add($"손 시우 잡기(왼손, Grip_L {hs.Weight("Grip_L"):F0} · 잡힘 {pc.Held != null}): " + Shot(side, Path.Combine(dir, "hand_siwoo_grip.png"), 960, 720, 2));
            for (int i = 0; i < 90; i++) yield return null;
            side.fieldOfView = 34f;
        }

        // ───────────────────────── 줄 세움 + 적 손
        IEnumerator Lineup()
        {
            // 구경꾼 원(반경 6) 안에서 찍는다 — 밖에서 찍으면 구경꾼 실루엣이 앞을 가림
            const float Row = -0.5f, Gap = 1.05f;
            yield return Reset(new Vector3(-2f * Gap, 0f, Row), 180f, new Vector3(8.5f, 0f, -8.5f), 0f);
            mode.End();
            ((PlayerBody)pc.Me.Body).Place(new Vector3(-2f * Gap, 0f, Row), 180f);
            var names = new[] { "Kkanjok", "Seokdal", "Naengjanggo", "Scrum" };
            var labels = new[] { "깐족이", "석 달", "냉장고", "스크럼" };
            var made = new List<GameObject>();
#if UNITY_EDITOR
            for (int k = 0; k < names.Length; k++)
            {
                var pf = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Prefabs/Enemy_{names[k]}.prefab");
                if (pf == null) { notes.Add("줄 세움: 프리팹 없음 " + names[k]); continue; }
                var go = UnityEngine.Object.Instantiate(pf, new Vector3(-Gap + Gap * k, 0f, Row), Quaternion.Euler(0f, 180f, 0f));
                go.name = "Line_" + names[k];
                made.Add(go);
            }
#endif
            for (int i = 0; i < 40; i++) yield return null;
            var heights = new List<string>();
            var all = new List<(string n, GameObject g)> { ("시우", pc.GetComponentInChildren<Animator>().gameObject) };
            for (int k = 0; k < made.Count; k++) all.Add((labels[k], made[k]));
            foreach (var (n, g) in all)
            {
                var an = g.GetComponent<Animator>();
                var head = an != null ? an.GetBoneTransform(HumanBodyBones.Head) : null;
                float top = g.GetComponentsInChildren<SkinnedMeshRenderer>().Select(r => r.bounds.max.y).DefaultIfEmpty(0f).Max();
                heights.Add($"{n} 렌더 경계 {top:F2}m · 머리 뼈 {(head != null ? head.position.y : 0f):F2}m · x {g.transform.position.x:F1}");
            }
            notes.Add("줄 세움 키: " + string.Join(" / ", heights));
            side.fieldOfView = 36f;
            side.transform.position = new Vector3(0f, 1.0f, Row - 4.8f);
            side.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0.95f, Row) - side.transform.position);
            notes.Add("줄 세움 정면: " + Shot(side, Path.Combine(dir, "lineup_front.png"), 1920, 1080, 2));
            side.transform.position = new Vector3(-3.6f, 1.0f, Row - 3.4f);
            side.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0.95f, Row) - side.transform.position);
            notes.Add("줄 세움 비스듬: " + Shot(side, Path.Combine(dir, "lineup_side.png"), 1920, 1080, 2));

            // 적 손: 편 손 · 주먹 · 쥐기(셰이프 키 직접)
            for (int k = 0; k < made.Count; k++)
            {
                var g = made[k];
                var hs = g.GetComponentInChildren<HandShape>();
                if (hs != null) hs.enabled = false;
                var smr = g.GetComponentsInChildren<SkinnedMeshRenderer>().FirstOrDefault(r => r.sharedMesh != null && r.sharedMesh.GetBlendShapeIndex("Fist_R") >= 0);
                if (smr == null) { notes.Add($"손 {labels[k]}: 셰이프 키 없음"); continue; }
                var an = g.GetComponent<Animator>();
                var gfa = g.GetComponent<FighterAnim>();
                if (gfa != null) gfa.enabled = false;
                an.enabled = false;     // 마지막 자세 그대로 멈춤 — 켜 두면 대기 클립의 셰이프 키 곡선이 0 으로 덮어씀
                foreach (var (tag, fist, grip) in new[] { ("open", 0f, 0f), ("fist", 100f, 0f), ("grip", 0f, 100f) })
                {
                    foreach (var s in new[] { "Fist_L", "Fist_R" }) smr.SetBlendShapeWeight(smr.sharedMesh.GetBlendShapeIndex(s), fist);
                    foreach (var s in new[] { "Grip_L", "Grip_R" }) smr.SetBlendShapeWeight(smr.sharedMesh.GetBlendShapeIndex(s), grip);
                    yield return null;
                    HandCam(an, false, g.transform);
                    notes.Add($"손 {labels[k]} {tag}: " + Shot(side, Path.Combine(dir, $"hand_{names[k].ToLower()}_{tag}.png"), 960, 720, 2));
                }
            }
            side.fieldOfView = 34f;
            foreach (var g in made) UnityEngine.Object.Destroy(g);
            yield return null;
        }

        // ───────────────────────── 1:3 실전
        IEnumerator Fight()
        {
            yield return Reset(new Vector3(0f, 0f, -2f), 0f, new Vector3(8.5f, 0f, 8.5f), 225f);
            var dbg = UnityEngine.Object.FindAnyObjectByType<CombatDebug>();
            Assert.NotNull(dbg, "CombatDebug");
            pc.Me.MaxHp = pc.Me.Hp = 9999;
            dbg.Spawn();
            var es = dbg.Spawned.ToList();
            var dirr = dbg.Director;
            var sd = Path.Combine(dir, "seq9_fight");
            Directory.CreateDirectory(sd);
            int n = 0, guards = 0, presses = 0, maxAtk = 0;
            int hits0 = ImpactFx.Count;
            var log = new List<string>();
            for (int i = 0; i < 60 * 30; i++)
            {
                var alive = es.Where(e => e != null && !e.Eliminated).ToList();
                if (alive.Count == 0) { log.Add($"f{i} 모두 탈락"); break; }
                var me = pc.Me.Position;
                var tgt = alive.OrderBy(e => (e.Me.Position - me).sqrMagnitude).First();
                if (pc.Lock.Target != tgt.Me) pc.Lock.Set(tgt.Me);
                var threat = alive.FirstOrDefault(e => e.Me.Run != null && e.Me.Run.T < e.Me.Run.Move.ActiveEnd && (e.Me.Position - me).magnitude < 3.2f);
                bool free = pc.Me.State == Fighter.Phase.Free || pc.Me.State == Fighter.Phase.Act;
                if (threat != null && threat.Me.Run.Move.Unblockable && pc.Me.State == Fighter.Phase.Free)
                {
                    pc.SetGuard(false);
                    pc.SetStickWorld(Vector3.Cross(Vector3.up, (threat.Me.Position - me).normalized), 1f);
                    pc.Press(Btn.Dodge);
                }
                else if (threat != null && pc.Me.Run == null)
                {
                    pc.SetGuard(true); guards++;
                    pc.SetStickWorld(Vector3.zero, 0f);
                }
                else
                {
                    pc.SetGuard(false);
                    float d = (tgt.Me.Position - me).magnitude;
                    if (d > 1.5f) pc.SetStickWorld(HitResolver.Flat(tgt.Me.Position - me).normalized, 1f);
                    else
                    {
                        pc.SetStickWorld(Vector3.zero, 0f);
                        if (free && i % 11 == 0) { pc.Press((i / 11) % 5 == 4 ? Btn.Heavy : Btn.Light); presses++; }
                    }
                }
                yield return null;
                maxAtk = Mathf.Max(maxAtk, dirr != null ? dirr.Attacking : 0);
                if (i % 2 == 0) { Shot(main, Path.Combine(sd, $"f_{n:000}.png"), 960, 540, 1); n++; }
                if (i % 300 == 0) log.Add($"{i / 60}초: " + string.Join(", ", es.Where(e => e != null).Select(e => $"{e.Def.Label} {e.State} HP {e.Me.Hp}")) + $" · 시우 {pc.Me.State}");
            }
            pc.SetGuard(false);
            pc.SetStickWorld(Vector3.zero, 0f);
            notes.Add($"1:3 실전(게임 카메라, 2프레임마다): {sd} {n}장 · 시우 누름 {presses} · 막기 프레임 {guards} · 맞힘·맞음 {ImpactFx.Count - hits0} · 동시 공격 최대 {maxAtk} · 시우 HP 잃음 {9999 - pc.Me.Hp} | " + string.Join(" | ", log));
            dbg.Clear();
            yield return null;
        }

        string Shot(Camera c, string path, int w, int h, int ss)
        {
            var big = new RenderTexture(new RenderTextureDescriptor(w * ss, h * ss, RenderTextureFormat.ARGB32, 24) { sRGB = true, msaaSamples = 1 });
            var small = new RenderTexture(new RenderTextureDescriptor(w, h, RenderTextureFormat.ARGB32, 0) { sRGB = true });
            big.Create(); small.Create();
            var prevT = c.targetTexture;
            var prevA = RenderTexture.active;
            var overlays = c == main ? UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(x => x.isRootCanvas && x.renderMode == RenderMode.ScreenSpaceOverlay).ToList() : new List<Canvas>();
            try
            {
                c.targetTexture = big;
                c.aspect = (float)w / h;
                foreach (var o in overlays) { o.renderMode = RenderMode.ScreenSpaceCamera; o.worldCamera = c; o.planeDistance = 0.5f; }
                Canvas.ForceUpdateCanvases();
                c.Render();
                Graphics.Blit(big, small);
                RenderTexture.active = small;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                UnityEngine.Object.Destroy(tex);
            }
            finally
            {
                foreach (var o in overlays) { o.renderMode = RenderMode.ScreenSpaceOverlay; o.worldCamera = null; }
                c.targetTexture = prevT;
                c.ResetAspect();
                RenderTexture.active = prevA;
                big.Release(); small.Release();
                UnityEngine.Object.Destroy(big); UnityEngine.Object.Destroy(small);
            }
            return path;
        }
    }
}
