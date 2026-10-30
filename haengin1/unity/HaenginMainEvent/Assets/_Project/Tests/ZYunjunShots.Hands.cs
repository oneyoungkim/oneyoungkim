// 행인1의 메인이벤트 — 손가락·미트·녹화 사진(2026-10-31 대표 승인 「손가락, 미트 드릴, 인물별 서는 자세 차이」)
//   -handshots <폴더> [-standonly 키,키] : 인물별 양손 근접(정면·옆) 전(Relax 0)/후 — 게임 먹선 그대로
//   -mittshots <폴더> : 레슨 코치 6명 D04 — 콤비 호출 때 게임 카메라·코치 쪽·미트 근접·맞은 순간·내렸을 때
//   -standrec <폴더> : 미트 드릴 10초 + N13 6초 프레임(30fps, ffmpeg 로 잇기)
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.TestTools;

namespace Haengin.Tests
{
    public sealed partial class ZYunjunShots
    {
        static readonly (string id, string key)[] MittCoaches = { ("yunjun", "yunjun"), ("gojun", "gojun"), ("seungyeon", "kim"), ("jinsoo_son", "son"), ("jinsoo_seo", "seo"), ("seungmin_baek", "baek") };
        static GymNpc CoachNpc(GymManager gm, string id) => id == "yunjun" ? gm.Yunjun : id == "gojun" ? gm.Gojun : gm.Guest(id);

        [UnityTest, Timeout(1800000)]
        public IEnumerator Z_Hands_Shots()
        {
            string dir = Shots.Arg("-handshots");
            if (string.IsNullOrEmpty(dir)) { Assert.Ignore("-handshots <폴더> 없음"); yield break; }
            if (!Shots.Graphics) { Assert.Ignore("그래픽 장치 없음(-nographics)"); yield break; }
#if UNITY_EDITOR
            Directory.CreateDirectory(dir);
            WorldRunner w = null;
            yield return M7Qa2Tests.Open(x => w = x);
            GymManager gm = null;
            yield return Enter(w, 312, 14 * 60, x => gm = x);
            gm.ApplyPart(GymPart.Day);
            foreach (var m in gm.Members) if (m != null) m.gameObject.SetActive(false);
            if (gm.Gojun != null) gm.Gojun.gameObject.SetActive(false);
            if (gm.Yunjun != null) gm.Yunjun.gameObject.SetActive(false);
            foreach (var g in gm.Guests) if (g != null) g.gameObject.SetActive(false);
            var cam = Camera.main; var brain = cam.GetComponent<CinemachineBrain>(); var me = M3Lab.Motor;
            brain.enabled = false;
            var meR = me.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled).ToList();
            foreach (var r in meR) r.enabled = false;
            var mat = gm.Sock("Sock_NPC_Coach_MatEdge");
            var feet = mat.position + mat.forward * 2.4f;
            var dir0 = OpenDir(feet + Vector3.up * 1.2f, mat.forward);
            var right = Vector3.Cross(Vector3.up, dir0);
            float yaw0 = Mathf.Atan2(dir0.x, dir0.z) * Mathf.Rad2Deg;
            const string GymP = "Assets/_Project/Prefabs/Gym/", CastP = "Assets/_Project/Prefabs/Cast/";
            var who = new List<(string key, string prefab)>
            {
                ("Yunjun_v2", GymP + "Npc_Yunjun_v2.prefab"), ("Seungyeon_v2", GymP + "Npc_Seungyeon_v2.prefab"), ("JinsooSon_v2", GymP + "Npc_JinsooSon_v2.prefab"),
                ("JinsooSeo_v2", GymP + "Npc_JinsooSeo_v2.prefab"), ("SeungminBaek", GymP + "Npc_SeungminBaek.prefab"), ("Gojun", GymP + "Npc_Gojun.prefab"),
                ("Taeo", "Assets/_Project/Prefabs/Char/Taeo.prefab"),
            };
            string[] enemies = { "Kkanjok", "Seokdal", "Naengjanggo", "Scrum" };
            for (int i = 0; i < 4; i++) who.Add((enemies[i], GymP + $"Npc_Member_{i + 1:00}.prefab"));
            foreach (var k in new[] { "Mom", "Haneul", "Dokyungmin", "Minjae", "Geonwoo", "StudentM", "StudentF", "Gwak", "Halmeoni", "BakeryStaff", "Dad" }) who.Add((k, CastP + k + ".prefab"));
            string only = Shots.Arg("-standonly");
            var pick = string.IsNullOrEmpty(only) ? null : new HashSet<string>(only.Split(','));
            var log = new List<string>();
            foreach (var (key, prefab) in who)
            {
                if (pick != null && !pick.Contains(key)) continue;
                var pf = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefab);
                if (pf == null) { log.Add($"{key}: 프리팹 없음"); continue; }
                var go = Object.Instantiate(pf, gm.transform);
                go.transform.SetPositionAndRotation(feet, Quaternion.Euler(0f, yaw0, 0f));
                var an = go.GetComponentInChildren<Animator>();
                an.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                gm.Lights.PushCharFill();
                var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimationClip>($"Assets/_Project/Anim/Stand/Stand_{key}.anim");
                var pc = AnimationPlayableUtilities.PlayClip(an, clip, out var graph);
                pc.SetTime(clip.length * 0.3f); pc.SetSpeed(0f);
                var hs = go.GetComponentInChildren<HandShape>(true);
                float want = hs != null ? hs.Relax : 0f;
                yield return Lab.Frames(4);
                log.Add($"{key}: HandShape {(hs != null ? (hs.Ready ? (hs.UsesPoser ? "손가락 뼈" : "셰이프 키") : "셰이프 키 없음") : "없음")} · Relax {want}");
                Vector3 P(HumanBodyBones b) => an.GetBoneTransform(b).position;
                foreach (var (ver, weight) in new[] { ("before", 0f), ("after", want) })
                {
                    if (hs != null) hs.Relax = weight;
                    yield return Lab.Frames(30);
                    foreach (var (tag, hb, fb, sign) in new[] { ("L", HumanBodyBones.LeftHand, HumanBodyBones.LeftLowerArm, -1f), ("R", HumanBodyBones.RightHand, HumanBodyBones.RightLowerArm, 1f) })
                    {
                        var hp = P(hb); var c = hp + (hp - P(fb)).normalized * 0.075f;
                        foreach (var (view, d) in new[] { ("front", dir0), ("side", right * sign) })
                        {
                            cam.transform.SetPositionAndRotation(c + d * 1f, Quaternion.LookRotation(-d, Vector3.up));
                            cam.fieldOfView = 16f; float n0 = cam.nearClipPlane; cam.nearClipPlane = 0.5f;
                            yield return null;
                            Shots.Shot(cam, Path.Combine(dir, $"{key}_{tag}_{view}_{ver}.png"), 640, 640, 2, false);
                            cam.nearClipPlane = n0;
                        }
                    }
                }
                graph.Destroy();
                Object.Destroy(go);
                yield return null;
            }
            brain.enabled = true;
            foreach (var r in meR) r.enabled = true;
            File.WriteAllText(Path.Combine(dir, "hands_log.txt"), string.Join("\n", log), new UTF8Encoding(true));
            Debug.Log("[Hands] 손 사진 → " + dir + "\n" + string.Join("\n", log));
#else
            yield break;
#endif
        }

        [UnityTest, Timeout(2400000)]
        public IEnumerator Z_Mitt_Shots()
        {
            string dir = Shots.Arg("-mittshots");
            if (string.IsNullOrEmpty(dir)) { Assert.Ignore("-mittshots <폴더> 없음"); yield break; }
            if (!Shots.Graphics) { Assert.Ignore("그래픽 장치 없음(-nographics)"); yield break; }
            Directory.CreateDirectory(dir);
            var log = new List<string>();
            WorldRunner w = null;
            yield return M7Qa2Tests.Open(x => w = x);
            var cam = Camera.main; var brain = cam.GetComponent<CinemachineBrain>(); var me = M3Lab.Motor;
            string only = Shots.Arg("-mittonly");
            foreach (var (id, key) in MittCoaches)
            {
                if (!string.IsNullOrEmpty(only) && !only.Split(',').Contains(key)) continue;
                GymManager gm = null;
                yield return Enter(w, 312, 19 * 60, x => gm = x);
                GymState.Current.Fatigue = 0;
                Assert.IsTrue(gm.StartDrill("D04", 0.9f), key + " D04 시작");
                yield return Until(() => gm.Runner.State == DrillRunner.Phase.Round, 90f);
                var npc = CoachNpc(gm, id);
                Assert.NotNull(npc, key);
                var y0 = gm.Yunjun.transform.position; var f0 = gm.Yunjun.transform.forward;
                if (npc != gm.Yunjun) gm.SetMittHolder(npc);
                log.Add($"{key}: 시우 {me.transform.position:F2} · 이윤준 {y0:F2} 앞 {f0:F2} · 코치 뿌리 {npc.transform.position:F2} · 모델 {npc.Anim.transform.position:F2} · 켜짐 {npc.gameObject.activeInHierarchy} · 상태 {npc.Now} {npc.Current}");
                npc.Anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;       // 배치는 화면을 안 그려 컬링됨(IK·미트가 한 박자 늦게 잡힘)
                var arms = npc.Anim.gameObject.GetComponent<MittArms>() ?? npc.Anim.gameObject.AddComponent<MittArms>();
                int hurt = 0; System.Action<HitEvent> on = e => hurt++;
                gm.Actors.Mitt.Hurt += on;
                // ① 콤비 호출(미트 들림) — 맞기 전
                int hurt0 = hurt;
                yield return Until(() => arms.LiftWeight >= 0.99f && arms.PadsShown, 40f);
                yield return null;
                yield return null;
                Assert.IsTrue(arms.PadsShown, key + " 미트 보임");
                log.Add($"{key}: 호출 때 코치 뿌리 {npc.transform.position:F2} · 모델 {npc.Anim.transform.position:F2} · 시우 {me.transform.position:F2}");
                Snap(gm, cam, Path.Combine(dir, $"{key}_1_call_game.png"), true);
                var an = npc.Anim;
                Vector3 B(HumanBodyBones b) => an.GetBoneTransform(b).position;
                float shY = (B(HumanBodyBones.LeftUpperArm).y + B(HumanBodyBones.RightUpperArm).y) * 0.5f, headY = B(HumanBodyBones.Head).y;
                float padY = (arms.PadL.position.y + arms.PadR.position.y) * 0.5f;
                log.Add($"{key}: 어깨 {shY:F3} · 패드 가운데 {padY:F3} · 머리 뼈 {headY:F3} · 왼손-패드 {(arms.PadL.position - B(HumanBodyBones.LeftHand)).magnitude:F3} · 오른손-패드 {(arms.PadR.position - B(HumanBodyBones.RightHand)).magnitude:F3}");
                var toMe = me.transform.position - npc.transform.position; toMe.y = 0f; toMe.Normalize();
                var head = B(HumanBodyBones.Head);
                brain.enabled = false;
                Solve(cam, Vector3.Lerp(head, me.transform.position + Vector3.up * 1.4f, 0.35f), toMe, 3.4f, 0.2f, 44f, Front, null);
                yield return null;
                Snap(gm, cam, Path.Combine(dir, $"{key}_2_call_coach.png"), true);
                // 미트 근접(HUD 끔): 시우 몸은 잠깐 숨김
                var meR = me.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled).ToList();
                foreach (var r in meR) r.enabled = false;
                var mid = (arms.PadL.position + arms.PadR.position) * 0.5f + Vector3.up * 0.08f;
                var side = Vector3.Cross(Vector3.up, toMe);
                foreach (var (view, d) in new[] { ("front", toMe), ("q", (toMe + side * 0.8f).normalized), ("side", side) })
                {
                    var cp = mid + d * 1.5f;
                    cam.transform.SetPositionAndRotation(cp, Quaternion.LookRotation(mid - cp, Vector3.up)); cam.fieldOfView = 34f;
                    float n0 = cam.nearClipPlane; cam.nearClipPlane = 0.4f;
                    yield return null;
                    Snap(gm, cam, Path.Combine(dir, $"{key}_3_zoom_{view}.png"), false);
                    cam.nearClipPlane = n0;
                }
                foreach (var r in meR) r.enabled = true;
                brain.enabled = true;
                // ② 맞은 순간(번쩍임 지난 뒤)
                yield return Until(() => hurt > hurt0, 30f);
                yield return Lab.Frames(4);
                Snap(gm, cam, Path.Combine(dir, $"{key}_4_hit_game.png"), true);
                brain.enabled = false;
                Solve(cam, Vector3.Lerp(B(HumanBodyBones.Head), me.transform.position + Vector3.up * 1.4f, 0.35f), toMe, 3.4f, 0.2f, 44f, Front, null);
                yield return null;
                Snap(gm, cam, Path.Combine(dir, $"{key}_5_hit_coach.png"), true);
                brain.enabled = true;
                // ③ 내렸을 때
                yield return Until(() => arms.LiftWeight <= 0.01f, 30f);
                yield return Lab.Frames(4);
                Snap(gm, cam, Path.Combine(dir, $"{key}_6_rest_game.png"), true);
                gm.Actors.Mitt.Hurt -= on;
                gm.Runner.Abort();
                for (int i = 0; i < 300 && gm.Busy; i++) { gm.CloseCard(); yield return null; }
                w.Stop();
                yield return Lab.Frames(5);
            }
            File.WriteAllText(Path.Combine(dir, "mitt_log.txt"), string.Join("\n", log), new UTF8Encoding(true));
            Debug.Log("[Mitt] 미트 사진 → " + dir + "\n" + string.Join("\n", log));
        }

        [UnityTest, Timeout(1800000)]
        public IEnumerator Z_Stand_Rec()
        {
            string dir = Shots.Arg("-standrec");
            if (string.IsNullOrEmpty(dir)) { Assert.Ignore("-standrec <폴더> 없음"); yield break; }
            if (!Shots.Graphics) { Assert.Ignore("그래픽 장치 없음(-nographics)"); yield break; }
            Directory.CreateDirectory(dir);
            WorldRunner w = null;
            yield return M7Qa2Tests.Open(x => w = x);
            var cam = Camera.main; int n = 0;
            GymManager gm = null;
            yield return Enter(w, 312, 19 * 60, x => gm = x);
            GymState.Current.Fatigue = 0;
            Assert.IsTrue(gm.StartDrill("D04", 0.9f), "D04 시작");
            yield return Until(() => gm.Runner.State == DrillRunner.Phase.Round, 90f);
            gm.Yunjun.Anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            Time.captureDeltaTime = 1f / 30f;
            for (int i = 0; i < 300 && gm.Runner.State == DrillRunner.Phase.Round; i++) { yield return null; JpgFrame(cam, Path.Combine(dir, $"f_{n++:0000}.jpg")); }
            Time.captureDeltaTime = 0f;
            gm.Runner.Abort();
            for (int i = 0; i < 300 && gm.Busy; i++) { gm.CloseCard(); yield return null; }
            w.Stop();
            yield return Lab.Frames(5);
            // N13
            yield return M7Qa2Tests.Start(w, M7Qa2Tests.Mid(w.Def, 502, 18 * 60 + 40, SceneLoader.Gym, m => { m.G.Fatigue = 0; new EventDirector(m).Done(w.Def.Card("N12"), 1); }));
            var story = w.Story; story.Bot = StoryRunner.BotMode.Read;
            w.BotRead = 6f;
            bool done = false;
            IEnumerator Wrap() { yield return w.RunCardBusy(w.Def.Card("N13"), true); done = true; }
            w.StartCoroutine(Wrap());
            var names = new[] { "이윤준", "김승연", "손진수", "서진수", "백승민" };
            yield return Until(() => done || names.All(k => { var a = CutCtx.Actor(k); return a != null && a.gameObject.activeInHierarchy; }), 120f);
            Time.captureDeltaTime = 1f / 30f;
            for (int i = 0; i < 180 && !done; i++) { yield return null; JpgFrame(cam, Path.Combine(dir, $"f_{n++:0000}.jpg")); }
            Time.captureDeltaTime = 0f;
            yield return Until(() => done, 120f);
            w.Stop();
            Debug.Log($"[Rec] 프레임 {n}장 → {dir}");
        }
    }
}
