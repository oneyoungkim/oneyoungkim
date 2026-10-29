// 행인1의 메인이벤트 — 이윤준 관장 게임 화면 캡처(2026-10-28 대표 요청). -yunjunshots <폴더> 일 때만(그래픽 필요 — -nographics 빼기)
//   오픈월드 체육관(3/12 목 — 낮 14:00 · 저녁 19:00 · 밤 21:15 개인 지도), 최신 룩데브, 1920×1080(2배 슈퍼샘플)
//   game_01 정면 근접(상반신) · game_02 전신 정면 · game_08 옆 3/4                      — HUD 끔
//   game_03 케이지 문 앞(시우와) · game_04 미트 드릴 맞는 순간 · game_05 사무실 책상 ·
//   game_06 밤 개인 지도 · game_07 레슨 대사 카드                                       — HUD 그대로(오버레이 캔버스 포함)
//   alt/*.png = 같은 장면 다른 카메라(실제 드릴 카메라 등) · mitt/f_###.jpg = D04 미트 10초 30fps(mp4 는 밖에서 ffmpeg, 소리 없음)
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.TestTools;

namespace Haengin.Tests
{
    public sealed class ZYunjunShots
    {
        [UnityTearDown]
        public IEnumerator Down()
        {
            Time.captureDeltaTime = 0f;
            var w = Object.FindAnyObjectByType<WorldRunner>();
            if (w != null) { w.BotLessonDrill = null; w.Stop(); }
            GameState.SetPaused(false);
            yield return M3Lab.TearDown();
        }

        static Transform Bone(GymNpc n, HumanBodyBones b) => n.Anim != null && n.Anim.isHuman && n.Anim.GetBoneTransform(b) != null ? n.Anim.GetBoneTransform(b) : n.transform;

        /// 몸통이 보는 쪽(어깨 두 점 — 'Stance' 같은 옆으로 선 클립에서도 얼굴 쪽)
        static Vector3 FaceDir(GymNpc n)
        {
            var l = Bone(n, HumanBodyBones.LeftUpperArm).position; var r = Bone(n, HumanBodyBones.RightUpperArm).position;
            var f = Vector3.Cross(r - l, Vector3.up); f.y = 0f;
            return f.sqrMagnitude > 1e-4f ? f.normalized : n.transform.forward;
        }

        /// 월드 점을 화면 가로 위치 x(0~1)에 오게 카메라를 옆으로 돌림
        static void ShiftX(Camera cam, Vector3 point, float x)
        {
            float h = Mathf.Atan(Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * (16f / 9f));
            float want = Mathf.Atan((x - 0.5f) * 2f * Mathf.Tan(h)) * Mathf.Rad2Deg;
            cam.transform.rotation = Quaternion.LookRotation(point - cam.transform.position, Vector3.up);
            cam.transform.Rotate(0f, -want, 0f, Space.World);
        }

        /// 실제 시간으로 기다림(배치 렌더는 프레임이 빨라 프레임 수로는 몇 초 안 됨)
        static IEnumerator Until(System.Func<bool> ok, float secs)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!ok() && Time.realtimeSinceStartup - t0 < secs) yield return null;
        }

        /// 사람 몸(발~머리 기둥)이 카메라–대상 선을 가리는지
        static bool Blocks(Transform body, Vector3 cam, Vector3 target)
        {
            if (body == null) return false;
            foreach (float hgt in new[] { 0.6f, 1.1f, 1.5f })
            {
                var p = body.position + Vector3.up * hgt;
                var ab = target - cam; float t = Mathf.Clamp01(Vector3.Dot(p - cam, ab) / ab.sqrMagnitude);
                if (t > 0.02f && t < 0.92f && Vector3.Distance(cam + ab * t, p) < 0.32f) return true;
            }
            return false;
        }

        /// 얼굴 쪽 기준 yaw 후보를 차례로 — 대상까지 막힘 없고, 카메라 둘레 0.6m 안에 벽·기둥 없고(오버레이 판이 0.5m 에 그려짐), 사람이 안 가리는 첫 자리
        static string Solve(Camera cam, Vector3 target, Vector3 face, float dist, float up, float fov, float[] yaws, Transform avoid)
        {
            foreach (var y in yaws)
                foreach (var dd in new[] { dist, dist * 0.8f, dist * 1.25f })
                {
                    var d = (Quaternion.Euler(0f, y, 0f) * face).normalized;
                    var pos = target + d * dd + Vector3.up * up;
                    var v = pos - target;
                    if (Physics.SphereCast(target, 0.15f, v.normalized, out _, v.magnitude, Layers.Solid, QueryTriggerInteraction.Ignore)) continue;
                    if (Physics.CheckSphere(pos, 0.6f, Layers.Solid, QueryTriggerInteraction.Ignore)) continue;
                    if (Blocks(avoid, pos, target)) continue;
                    cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(target - pos, Vector3.up));
                    cam.fieldOfView = fov;
                    return $"{y:0}° {dd:F1}m";
                }
            Pose(cam, target, target + face * dist + Vector3.up * up, fov);
            return "기본(막힘)";
        }

        static readonly float[] Front = { 25f, -25f, 40f, -40f, 10f, -10f, 55f, -55f, 70f, -70f, 0f, 85f, -85f };

        /// 대상에서 카메라 쪽으로 막힌 데가 있으면 그 앞까지만(벽 속 카메라 막기)
        static Vector3 Clear(Vector3 target, Vector3 want)
        {
            var d = want - target; float len = d.magnitude;
            if (len < 1e-3f) return want;
            if (Physics.SphereCast(target, 0.12f, d / len, out var h, len, Layers.Solid, QueryTriggerInteraction.Ignore)) return target + d / len * Mathf.Max(0.5f, h.distance - 0.1f);
            return want;
        }

        static void Pose(Camera cam, Vector3 target, Vector3 pos, float fov)
        {
            pos = Clear(target, pos);
            cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(target - pos, Vector3.up));
            cam.fieldOfView = fov;
        }

        static void Snap(GymManager gm, Camera cam, string path, bool ui)
        {
            if (gm.Mirror != null && gm.Mirror.Active) gm.Mirror.Render(cam);
            Shots.Shot(cam, path, 1920, 1080, 2, ui);
        }

        static void JpgFrame(Camera cam, string path)
        {
            var t = Shots.Grab(cam, 1920, 1080, 1, true);
            File.WriteAllBytes(path, t.EncodeToJPG(90));
            Object.Destroy(t);
        }

        /// 가장 트인 방향(가슴 높이에서 6m 안 막힌 거리 최대)
        static Vector3 OpenDir(Vector3 chest, Vector3 prefer)
        {
            Vector3 best = prefer; float bestLen = -1f;
            for (int a = 0; a < 360; a += 15)
            {
                var d = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                float len = 6f;
                if (Physics.SphereCast(chest, 0.25f, d, out var h, 6f, Layers.Solid, QueryTriggerInteraction.Ignore)) len = h.distance;
                float score = len + Vector3.Dot(d, prefer) * 0.6f;
                if (score > bestLen) { bestLen = score; best = d; }
            }
            return best;
        }

        /// 맞을 때마다(미트 Hurt) 몇 프레임 뒤 실제 화면 + 가까운 화면(HUD 그대로) — 번쩍임(맞는 순간 흰 칠)이 지난 뒤를 고르려고 늦춤 여럿
        static int[] Delays = { 2, 4, 7 };
        IEnumerator HitShots(GymManager gm, Camera cam, CinemachineBrain brain, GymNpc yj, string dir, string key, int maxHits, int maxFrames, bool record, List<string> log)
        {
            var mitt = gm.Actors.Mitt;
            int hurt = 0;
            System.Action<HitEvent> onHurt = e => hurt++;
            if (mitt != null) mitt.Hurt += onHurt;
            var due = new List<(int frame, int hit, int delay)>();
            int saved = 0, lastHurt = 0, shots = 0;
            for (int i = 0; i < maxFrames && gm.Runner.State == DrillRunner.Phase.Round; i++)
            {
                yield return null;
                if (record && gm.Runner.T >= 0.5 && saved < 300) JpgFrame(cam, Path.Combine(dir, "mitt", $"f_{saved++:000}.jpg"));
                if (hurt != lastHurt) { lastHurt = hurt; if (hurt <= maxHits) foreach (var dl in Delays) due.Add((i + dl, hurt, dl)); }
                foreach (var dd in due.Where(x => x.frame == i).ToList())
                {
                    Snap(gm, cam, Path.Combine(dir, "alt", $"{key}_hit{dd.hit}_d{dd.delay}.png"), true);
                    var head = Bone(yj, HumanBodyBones.Head).position;
                    var tgt = head - Vector3.up * 0.32f;
                    brain.enabled = false;
                    Solve(cam, tgt, FaceDir(yj), 2.5f, 0.12f, 42f, Front, M3Lab.Motor.transform);
                    Snap(gm, cam, Path.Combine(dir, "alt", $"{key}_close{dd.hit}_d{dd.delay}.png"), true);
                    brain.enabled = true;
                    shots += 2;
                }
                if (!record && hurt > maxHits && due.All(x => x.frame < i)) break;
                if (record && saved >= 300 && hurt > maxHits) break;
            }
            if (mitt != null) mitt.Hurt -= onHurt;
            log.Add($"{key}: 맞음 {hurt} · 사진 {shots}{(record ? $" · 녹화 {saved}장(30fps)" : "")}");
        }

        /// 이윤준이 콤비를 부를 때(미트 들림 — 맞기 전이라 '팡!' 판이 얼굴을 안 가림)마다 0.2·0.4초 뒤: 시우 어깨 너머로 이윤준 얼굴(HUD 그대로)
        IEnumerator CallShots(GymManager gm, Camera cam, CinemachineBrain brain, GymNpc yj, PlayerMotor me, string dir, string key, int maxCalls, List<string> log)
        {
            int calls = 0;
            var say0 = gm.Actors.Say;
            gm.Actors.Say = (who, line) => { say0?.Invoke(who, line); if (gm.Runner.State == DrillRunner.Phase.Round) calls++; };
            var due = new List<(int frame, int call, int delay)>();
            int last = 0, shots = 0;
            for (int i = 0; i < 60 * 40 && gm.Runner.State == DrillRunner.Phase.Round; i++)
            {
                yield return null;
                if (calls != last) { last = calls; if (calls <= maxCalls) foreach (var dl in new[] { 6, 12 }) due.Add((i + dl, calls, dl)); }
                foreach (var dd in due.Where(x => x.frame == i).ToList())
                {
                    Snap(gm, cam, Path.Combine(dir, "alt", $"{key}_play{dd.call}_d{dd.delay}.png"), true);
                    var head = Bone(yj, HumanBodyBones.Head).position;
                    var tgt = head - Vector3.up * 0.3f;
                    var toMe = me.transform.position - yj.transform.position; toMe.y = 0f; toMe.Normalize();
                    brain.enabled = false;
                    foreach (var (nm, dist, fov) in new[] { ("over", 3.0f, 40f), ("close", 2.2f, 36f) })
                    {
                        string how = Solve(cam, tgt, FaceDir(yj), dist, 0.15f, fov, Front, me.transform);
                        if (dd.call == 1 && dd.delay == 6) log.Add($"{key} {nm}: {how}");
                        Snap(gm, cam, Path.Combine(dir, "alt", $"{key}_{nm}{dd.call}_d{dd.delay}.png"), true);
                        shots++;
                    }
                    brain.enabled = true;
                }
                if (calls > maxCalls && due.All(x => x.frame < i)) break;
            }
            gm.Actors.Say = say0;
            log.Add($"{key}: 부름 {calls} · 사진 {shots}");
        }

        static IEnumerator HoldAt(GymNpc yj, Vector3 feet, Vector3 dir)
        {
            yj.gameObject.SetActive(true);
            if ((yj.transform.position - feet).sqrMagnitude > 0.01f || yj.Current != "Idle") { yj.Place(feet, dir, GymNpc.Routine.Watch); yj.Play("Idle", 0f); }
            yield return null; yield return null; yield return null;
        }

        static IEnumerator Enter(WorldRunner w, int day, int min, System.Action<GymManager> got)
        {
            yield return M7Qa2Tests.Start(w, M7Qa2Tests.Mid(w.Def, day, min, SceneLoader.Gym, m => { m.G.Fatigue = 0; m.L.SetWallet(300000); }));
            GymManager gm = null;
            for (int i = 0; i < 300 && gm == null; i++) { gm = GymManager.Instance; yield return null; }
            Assert.NotNull(gm, "체육관");
            yield return Lab.Frames(30);
            got(gm);
        }

        // ───────────────────────── 출장 코치 셋(김승연·손진수·서진수) 게임 화면 + N13 다섯 사람(2026-10-29 대표 "어디 들어가 있지? 캡처 보여줘") — -coachshots <폴더>
        //   사람마다 ① 레슨 시작 카드 + 매트 옆 코치(실제 탐색 카메라) ② 레슨 드릴 중(실제 드릴 카메라 + 코치 쪽) ③ 얼굴 근접(HUD 끔) · N13 다섯이 다 보이는 컷 3
        [UnityTest, Timeout(2400000)]
        public IEnumerator Z_Coach_Shots()
        {
            string dir = Shots.Arg("-coachshots");
            if (string.IsNullOrEmpty(dir)) { Assert.Ignore("-coachshots <폴더> 없음"); yield break; }
            if (!Shots.Graphics) { Assert.Ignore("그래픽 장치 없음(-nographics)"); yield break; }
            Directory.CreateDirectory(dir);
            var log = new List<string>();
            WorldRunner w = null;
            yield return M7Qa2Tests.Open(x => w = x);
            w.BotRead = 6f;
            var cam = Camera.main; var brain = cam.GetComponent<CinemachineBrain>(); var me = M3Lab.Motor;
            foreach (var (id, key, day, enter, at) in new[] { ("seungyeon", "kim", 328, 16 * 60 + 50, 17 * 60 + 25), ("jinsoo_son", "son", 329, 9 * 60 + 30, 10 * 60 + 55), ("jinsoo_seo", "seo", 329, 18 * 60, 19 * 60 + 25) })
            {
                GymManager gm = null;
                yield return Enter(w, day, enter, x => gm = x);
                w.BotRead = 6f;
                var coach = w.Model.Sh.Coach(id);
                Assert.AreEqual("", w.BookNext(id), id + " 예약");
                w.BotLessonDrill = c => c.Drills.Contains("*") ? "D03" : (c.Drills.FirstOrDefault(x => x == "D03" || x == "D04" || x == "D05") ?? c.Drills[0]);
                var mat = gm.Sock("Sock_NPC_Coach_MatEdge");
                var sp = mat.position + mat.forward * 2.4f; var to = mat.position - sp; to.y = 0f;
                me.Teleport(sp, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg);
                w.Loader.Cam?.SnapBehind();
                yield return Lab.Frames(20);
                GameClock.AdvanceTo(at);
                var board = w.Story.Board;
                yield return Until(() => board != null && board.Shown, 60f);
                yield return Until(() => false, 0.6f);
                var npc = gm.Guest(id);
                Snap(gm, cam, Path.Combine(dir, $"{key}_1_lesson_card.png"), true);
                if (npc != null)
                {
                    var head = Bone(npc, HumanBodyBones.Head).position;
                    brain.enabled = false;
                    Solve(cam, head - Vector3.up * 0.45f, FaceDir(npc), 3.0f, 0.1f, 42f, Front, me.transform);
                    ShiftX(cam, head - Vector3.up * 0.45f, 0.3f);
                    yield return null;
                    Snap(gm, cam, Path.Combine(dir, $"{key}_1b_lesson_card_coach.png"), true);
                    brain.enabled = true;
                }
                // ② 드릴 중
                yield return Until(() => gm.Busy && gm.Runner.State == DrillRunner.Phase.Round, 90f);
                yield return Until(() => false, 3f);
                Snap(gm, cam, Path.Combine(dir, $"{key}_2_drill.png"), true);
                if (npc != null)
                {
                    var head = Bone(npc, HumanBodyBones.Head).position;
                    var toMe = me.transform.position - npc.transform.position; toMe.y = 0f;
                    brain.enabled = false;
                    Solve(cam, Vector3.Lerp(head, me.transform.position + Vector3.up * 1.4f, 0.35f), toMe.sqrMagnitude > 1e-3f ? toMe.normalized : FaceDir(npc), 3.4f, 0.2f, 44f, Front, null);
                    yield return null;
                    Snap(gm, cam, Path.Combine(dir, $"{key}_2b_drill_coach.png"), true);
                    brain.enabled = true;
                }
                log.Add($"{coach.Name}: 드릴 {gm.Runner.Def?.Id} · NPC {(npc != null)}");
                gm.Runner.Abort();
                yield return Until(() => w.Free, 60f);
                for (int i = 0; i < 300 && gm.Busy; i++) { gm.CloseCard(); yield return null; }
                // ③ 얼굴 근접(HUD 끔)
                if (npc != null)
                {
                    var feet = mat.position;
                    var dirOpen = OpenDir(feet + Vector3.up * 1.2f, mat.forward);
                    foreach (var r in me.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                    yield return HoldAt(npc, feet, dirOpen);
                    yield return Lab.Frames(20);
                    yield return HoldAt(npc, feet, dirOpen);
                    var head = Bone(npc, HumanBodyBones.Head).position + Vector3.up * 0.06f;
                    brain.enabled = false;
                    Pose(cam, head, head + dirOpen * 1.7f + Vector3.up * 0.04f, 30f);
                    yield return null;
                    Snap(gm, cam, Path.Combine(dir, $"{key}_3_face.png"), false);
                    brain.enabled = true;
                    foreach (var r in me.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
                }
                w.BotLessonDrill = null;
                w.Stop();
                yield return Lab.Frames(5);
            }
            // N13 다섯 사람
            {
                yield return M7Qa2Tests.Start(w, M7Qa2Tests.Mid(w.Def, 502, 18 * 60 + 40, SceneLoader.Gym, m => { m.G.Fatigue = 0; new EventDirector(m).Done(w.Def.Card("N12"), 1); }));
                var gm = GymManager.Instance;
                var story = w.Story; story.Bot = StoryRunner.BotMode.Read;
                var c = w.Def.Card("N13");
                bool done = false;
                IEnumerator Wrap() { yield return w.RunCardBusy(c, true); done = true; }
                w.StartCoroutine(Wrap());
                var names = new[] { "이윤준", "김승연", "손진수", "서진수", "백승민" };
                bool All() => names.All(n => { var a = CutCtx.Actor(n); return a != null && a.gameObject.activeInHierarchy; });
                yield return Until(() => All() || done, 120f);
                yield return Until(() => false, 1.0f);
                Snap(gm, cam, Path.Combine(dir, "n13_1_play.png"), true);
                // 시우 뒤 높은 데서 다섯을 한 화면에
                var ctr = names.Select(n => CutCtx.Actor(n)).Where(a => a != null).Aggregate(Vector3.zero, (acc, a) => acc + a.position) / names.Length;
                var back = me.transform.position - ctr; back.y = 0f; back.Normalize();
                brain.enabled = false;
                Pose(cam, ctr + Vector3.up * 1.3f, ctr + back * 5.2f + Vector3.up * 2.3f, 48f);
                yield return null;
                Snap(gm, cam, Path.Combine(dir, "n13_2_wide.png"), true);
                Pose(cam, ctr + Vector3.up * 1.4f, ctr + (Quaternion.Euler(0f, 35f, 0f) * back) * 4.6f + Vector3.up * 1.7f, 50f);
                yield return null;
                Snap(gm, cam, Path.Combine(dir, "n13_3_side.png"), true);
                brain.enabled = true;
                yield return Until(() => false, 5f);
                Snap(gm, cam, Path.Combine(dir, "n13_4_play.png"), true);
                log.Add($"N13: 다섯 다 보임 {All()}");
                yield return Until(() => done, 120f);
                w.Stop();
            }
            File.WriteAllText(Path.Combine(dir, "summary.txt"), string.Join("\n", log), new UTF8Encoding(true));
            Debug.Log("[Coach] 코치 게임 화면 → " + dir + "\n" + string.Join("\n", log));
        }

        [UnityTest, Timeout(2400000)]
        public IEnumerator Z_Yunjun_Shots()
        {
            string dir = Shots.Arg("-yunjunshots");
            if (string.IsNullOrEmpty(dir)) { Assert.Ignore("-yunjunshots <폴더> 없음"); yield break; }
            if (!Shots.Graphics) { Assert.Ignore("그래픽 장치 없음(-nographics)"); yield break; }
            Directory.CreateDirectory(dir);
            Directory.CreateDirectory(Path.Combine(dir, "alt"));
            Directory.CreateDirectory(Path.Combine(dir, "mitt"));
            var log = new List<string>();
            WorldRunner w = null;
            yield return M7Qa2Tests.Open(x => w = x);
            w.BotRead = 6f;
            GymManager gm = null;
            var me = M3Lab.Motor;
            var cam = Camera.main;
            var brain = cam.GetComponent<CinemachineBrain>();
            float fov0 = cam.fieldOfView;
            var meR = me.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled).ToList();

            // ───── 낮 14:00 — ⑤ 유리창 사무실 책상 · ①②⑧ 서 있는 모습
            yield return Enter(w, 312, 14 * 60, x => gm = x);
            gm.ApplyPart(GymPart.Day);
            var yj = gm.Yunjun; Assert.NotNull(yj, "이윤준");
            var desk = gm.Sock("Sock_NPC_Coach_Desk");
            yj.gameObject.SetActive(true);
            yj.Place(desk.position, desk.forward, GymNpc.Routine.Sit);
            var g05 = gm.Sock("Sock_Cam_g05");
            me.Teleport(gm.transform.TransformPoint(new Vector3(16.0f, 0f, 9.2f)), 0f);
            yield return Lab.Frames(60);
            {
                var head = Bone(yj, HumanBodyBones.Head).position;
                var tgt = head - Vector3.up * 0.25f;
                brain.enabled = false;
                // 사무실 밖(스타일 프레임 g05 자리)에서 유리 너머 — 이윤준이 화면 높이의 절반쯤 차게 화각을 좁힘
                float d = Vector3.Distance(g05.position, tgt);
                Pose(cam, tgt, g05.position, Mathf.Clamp(2f * Mathf.Atan(1.0f / d) * Mathf.Rad2Deg, 18f, 50f));
                yield return null;
                Snap(gm, cam, Path.Combine(dir, "game_05.png"), true);
                // 다른 각: 책상 앞 대각선 2.4m
                var side = Quaternion.Euler(0f, 35f, 0f) * desk.forward;
                Pose(cam, tgt, tgt + side * 2.4f + Vector3.up * 0.25f, 40f);
                yield return null;
                Snap(gm, cam, Path.Combine(dir, "alt", "game_05_near.png"), true);
                brain.enabled = true;
                log.Add($"⑤ 사무실 책상 {GameClock.Label} · 카메라 g05 {d:F1}m");
            }
            // ①②⑧ 서 있는 모습(HUD 끔) — 매트 가장자리, 가장 트인 쪽을 보게, 대기(Idle)
            {
                var mat = gm.Sock("Sock_NPC_Coach_MatEdge");
                var feet = mat.position;
                var dirOpen = OpenDir(feet + Vector3.up * 1.2f, mat.forward);
                yj.Place(feet, dirOpen, GymNpc.Routine.Watch);
                yj.Play("Idle", 0.05f);
                foreach (var r in meR) r.enabled = false;
                yield return Lab.Frames(45);
                var fwd = dirOpen;
                brain.enabled = false;
                // 체육관이 시간대 자리로 되돌려 놓을 수 있어 찍기 직전마다 다시 세움
                System.Func<IEnumerator> Hold = () => HoldAt(yj, feet, dirOpen);
                // ① 정면 근접 — 머리~가슴(1.35m · 화각 30)
                yield return Hold();
                var head = Bone(yj, HumanBodyBones.Head).position;
                var t1 = head - Vector3.up * 0.12f;
                Pose(cam, t1, t1 + fwd * 1.55f + Vector3.up * 0.06f, 30f);
                yield return null;
                Snap(gm, cam, Path.Combine(dir, "game_01.png"), false);
                // ② 전신 정면 — 발~머리(4.0m · 화각 32)
                yield return Hold();
                var t2 = feet + Vector3.up * 0.95f;
                Pose(cam, t2, t2 + fwd * 4.0f + Vector3.up * 0.15f, 32f);
                yield return null;
                Snap(gm, cam, Path.Combine(dir, "game_02.png"), false);
                // ⑧ 옆 3/4 — 왼쪽 55°, 허리 위(2.1m · 화각 32)
                yield return Hold();
                var d8 = Quaternion.Euler(0f, -55f, 0f) * fwd;
                var t8 = feet + Vector3.up * 1.32f;
                Pose(cam, t8, t8 + d8 * 2.1f + Vector3.up * 0.1f, 32f);
                yield return null;
                Snap(gm, cam, Path.Combine(dir, "game_08.png"), false);
                yield return Hold();
                var d8b = Quaternion.Euler(0f, 55f, 0f) * fwd;
                Pose(cam, t8, t8 + d8b * 2.1f + Vector3.up * 0.1f, 32f);
                yield return null;
                Snap(gm, cam, Path.Combine(dir, "alt", "game_08_right.png"), false);
                brain.enabled = true;
                foreach (var r in meR) r.enabled = true;
                log.Add($"①②⑧ 매트 가장자리 · 방향 {Mathf.Atan2(dirOpen.x, dirOpen.z) * Mathf.Rad2Deg:F0}° · 머리 높이 {head.y - feet.y:F2}m");
            }

            // ───── 저녁 19:00 — ③ 케이지 문 앞(시우와) · ④ D04 미트(맞는 순간 + 10초 녹화)
            yield return Enter(w, 312, 19 * 60, x => gm = x);
            gm.ApplyPart(GymPart.Evening);
            yj = gm.Yunjun;
            {
                var cd = gm.Sock("Sock_NPC_Coach_CageDoor");
                var gc = gm.Sock("Sock_NPC_Gojun_Cage");
                var watch = gc != null ? gc.position - cd.position : cd.forward; watch.y = 0f; watch.Normalize();
                yj.gameObject.SetActive(true);
                yj.Place(cd.position, watch, GymNpc.Routine.Watch);
                yj.Play("Idle", 0.05f);                                           // 케이지 안(고준)을 보는 대기 자세
                var right = Vector3.Cross(Vector3.up, watch).normalized;
                me.Teleport(cd.position + right * 0.85f + watch * 0.3f, Mathf.Atan2(watch.x, watch.z) * Mathf.Rad2Deg);
                yield return Lab.Frames(60);
                var head = Bone(yj, HumanBodyBones.Head).position;
                var tgt = Vector3.Lerp(head, me.transform.position + Vector3.up * 1.5f, 0.3f) - Vector3.up * 0.35f;
                brain.enabled = false;
                var cams3 = new (string name, float yaw, float dist, float fov)[] { ("game_03", -90f, 2.6f, 40f), ("alt/game_03_3q", -70f, 2.4f, 38f), ("alt/game_03_wide", -85f, 3.6f, 42f) };
                foreach (var c3 in cams3)
                {
                    var d3 = (Quaternion.Euler(0f, c3.yaw, 0f) * watch).normalized;
                    Pose(cam, tgt, tgt + d3 * c3.dist + Vector3.up * 0.08f, c3.fov);
                    yield return null;
                    Snap(gm, cam, Path.Combine(dir, c3.name + ".png"), true);
                }
                brain.enabled = true;
                yield return Lab.Frames(30);
                Snap(gm, cam, Path.Combine(dir, "alt", "game_03_play.png"), true);
                log.Add($"③ 케이지 문 앞 {GameClock.Label}");
            }
            {
                Assert.IsTrue(gm.StartDrill("D04", 1f), "D04 시작");
                yield return Until(() => gm.Runner.State == DrillRunner.Phase.Round, 60f);
                Time.captureDeltaTime = 1f / 30f;
                yield return HitShots(gm, cam, brain, yj, dir, "game_04", 4, 60 * 60, true, log);
                Time.captureDeltaTime = 0f;
                log.Add($"④ D04 미트 {GameClock.Label} · {GymLights.Name(gm.Lights.Part)}");
                gm.Runner.Abort();
                for (int i = 0; i < 300 && gm.Busy; i++) { gm.CloseCard(); yield return null; }
            }

            // ───── 밤(20:30 들어가 예약 → 21:10) — 이윤준 '밤 개인 지도'(화·목 21:15) 예약 → ⑦ 레슨 대사 카드 → ⑥ 밤 미트(D04)
            yield return Enter(w, 312, 20 * 60 + 30, x => gm = x);
            yj = gm.Yunjun;
            Assert.AreEqual("", w.BookNext("yunjun"), "이윤준 예약");
            var bk = w.Model.S.Lessons.Last();
            Assert.AreEqual(312, bk.Day, "예약 날"); Assert.AreEqual(21 * 60 + 15, bk.At, "예약 시각(목 21:15 밤 개인 지도)");
            w.BotLessonDrill = c => "D04";
            {
                var mat = gm.Sock("Sock_NPC_Coach_MatEdge");
                // 카드 뒤로도 이윤준이 보이게: 시우를 매트 가장자리 앞 1.6m 에, 카메라는 시우 어깨 너머로 이윤준(게임 카메라 그대로 + 한 장 더)
                // ⑦ = 실제 탐색 카메라 그대로: 시우를 이윤준 앞 2.2m 에, 이윤준이 시우 정면에서 오른쪽 38° 에 오게 돌려 세움 → 카드(가운데 ¾) 오른쪽 끝에 이윤준
                var sp7 = mat.position + mat.forward * 2.2f;
                var toY = mat.position - sp7; toY.y = 0f; toY.Normalize();
                var f7 = Quaternion.Euler(0f, -38f, 0f) * toY;
                me.Teleport(sp7, Mathf.Atan2(f7.x, f7.z) * Mathf.Rad2Deg);
                w.Loader.Cam?.SnapBehind();
                yield return Lab.Frames(20);
                GameClock.AdvanceTo(21 * 60 + 10);
                gm.ApplyPart(GymPart.Night);
                var board = w.Story.Board;
                yield return Until(() => board != null && board.Shown, 60f);
                Assert.IsTrue(board != null && board.Shown, "레슨 카드가 안 뜸");
                yield return Until(() => false, 0.6f);
                Snap(gm, cam, Path.Combine(dir, "game_07.png"), true);
                // 카드(가운데 ¾)를 피해 이윤준 머리·어깨를 화면 오른쪽 끝(x 0.9)·왼쪽 끝(x 0.1)에 — 옆 70°에서 1.6m
                var head = Bone(yj, HumanBodyBones.Head).position;
                var fd = FaceDir(yj);
                brain.enabled = false;
                foreach (var (name, x, dist) in new[] { ("alt/game_07_rightedge", 0.9f, 3.0f), ("alt/game_07_left", 0.1f, 3.0f) })
                {
                    string how = Solve(cam, head - Vector3.up * 0.25f, fd, dist, 0.05f, 38f, Front, me.transform);
                    ShiftX(cam, head - Vector3.up * 0.25f, x);
                    log.Add($"⑦ {name}: {how}");
                    yield return null;
                    Snap(gm, cam, Path.Combine(dir, name + ".png"), true);
                }
                brain.enabled = true;
                log.Add($"⑦ 레슨 카드 {GameClock.Label} · {GymLights.Name(gm.Lights.Part)}");
            }
            {
                yield return Until(() => gm.Busy && gm.Runner.State == DrillRunner.Phase.Round, 120f);
                Assert.AreEqual(DrillRunner.Phase.Round, gm.Runner.State, "밤 레슨 드릴이 안 시작");
                Time.captureDeltaTime = 1f / 30f;
                yield return CallShots(gm, cam, brain, yj, me, dir, "game_06", 4, log);
                Time.captureDeltaTime = 0f;
                log.Add($"⑥ 밤 개인 지도 D04 {GameClock.Label} · {GymLights.Name(gm.Lights.Part)} · 채움빛 {(gm.Lights.Fill != null && gm.Lights.Fill.enabled)}");
                gm.Runner.Abort();
                for (int i = 0; i < 600 && !w.Free; i++) { if (gm.Busy) gm.CloseCard(); yield return null; }
            }
            cam.fieldOfView = fov0;
            File.WriteAllText(Path.Combine(dir, "summary.txt"), string.Join("\n", log), new UTF8Encoding(true));
            Debug.Log("[Yunjun] 이윤준 게임 화면 → " + dir + "\n" + string.Join("\n", log));
        }
    }
}
