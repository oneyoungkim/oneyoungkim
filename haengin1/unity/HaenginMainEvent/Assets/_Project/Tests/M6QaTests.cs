// 행인1의 메인이벤트 — 플레이 QA 버그 재현 시험(docs/15_QA_챕터1.md): QA 봇(-qa)이 빌드에서 사람처럼 하다 찾은 것마다 하나씩(Q01~).
// Zone1 을 Additive 로 열고 WorldRunner 를 중간 저장으로 시작(봇 실력 0.9 · 카드 짧게). 사진이 아닌 숫자로 본다(카메라 거리·글 칸·보이는 화면).
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace Haengin.Tests
{
    public sealed class M6QaTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var w = Object.FindAnyObjectByType<WorldRunner>();
            if (w != null) w.Stop();
            WorldRunner.BotPreset = null;
            GymManager.Gate = null; StageDoor.Gate = null;
            GameState.SetPaused(false);
            yield return M3Lab.TearDown();
        }

        static IEnumerator Open(System.Action<WorldRunner> got)
        {
            yield return M3Lab.OpenZone1();
            var w = Object.FindAnyObjectByType<WorldRunner>();
            Assert.NotNull(w, "Zone1 에 세계 루트(WorldRunner)가 없음 — WorldSetup.Build");
            w.BotSkill = 0.9f; w.BotRead = 0.3f; w.BotRide = false; w.BotDelegate = true;
            w.BotChoice = c => c.Id == "N02" ? 1 : 0;
            got(w);
        }

        static IEnumerator Free(WorldRunner w, int frames = 60 * 600)
        {
            for (int i = 0; i < frames && !(w.Free && !w.Loader.Busy); i++) yield return null;
        }

        /// 중간 저장: 그 날짜 전의 이야기 카드(데모)는 모두 끝낸 것으로 + 알바·체육관 열림
        static SaveData Mid(WorldDef def, int day, int minutes, string place, System.Action<WorldModel> tweak = null, Vector3? pos = null)
        {
            var m = M5WorldTests.Fresh(def);
            var d = new EventDirector(m);
            foreach (var c in def.Cards.Where(c => c.Demo && c.Kind != CardKind.Repeat))
            {
                int when = c.Date > 0 ? c.Date : c.WinTo > 0 ? c.WinTo : 0;
                if (when > 0 && when >= day) continue;
                d.ApplyAfter(c, c.Choice.Length > 1 ? c.Choice[1] : null); d.Done(c, 1);
            }
            m.Host.Clear();
            foreach (var u in new[] { "job.delivery", "job.gukbap", "job.stock", "job.counter", "job.site", "school", "planner", "lesson", "lesson.guests", "gym.cleaner" }) m.Unlock(u);
            if (m.S.GymMember == 0) m.S.GymMember = 1;
            GameClock.SetDate(2026, day / 100, day % 100, minutes);
            tweak?.Invoke(m);
            m.S.Active = true; m.S.Place = place; m.S.Spawn = place == "Home" || place == SceneLoader.Gym ? "Spawn" : "Door"; m.S.HasPos = false; m.S.Card = "";
            if (place == SceneLoader.Zone1) { m.S.HasPos = true; m.S.Pos = pos ?? def.Balance.Point("bus_stop").Pos + new Vector3(1.6f, 0f, 0f); m.S.Spawn = ""; }
            return new SaveData { SceneId = "world", Month = day / 100, Day = day % 100, Minutes = minutes, Ledger = m.L, Flags = m.F, Gym = m.G, Names = m.N, World = m.S };
        }

        static IEnumerator Start(WorldRunner w, SaveData s)
        {
            w.Begin(s);
            yield return Lab.Frames(10);
            yield return Free(w);
            yield return Lab.Frames(10);
        }

        // ───────────────────────── Q01 상호작용 안내는 이야기·카드·일시정지 중엔 숨김
        [UnityTest, Timeout(600000)]
        public IEnumerator Q01_Prompt_Hidden_While_Busy()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            var door = w.FindDoor("Gukbap");
            Assert.NotNull(door);
            yield return Start(w, Mid(w.Def, 324, 15 * 60, SceneLoader.Zone1, null, door.ReturnPos));
            var hud = StageHud.Instance;
            Assert.NotNull(hud, "StageHud");
            M3Lab.Motor.Teleport(door.transform.position + (door.ReturnPos - door.transform.position).normalized * 0.9f, 0f);
            yield return Lab.Frames(10);
            StringAssert.Contains("국밥집", hud.PromptText, "자유 이동 중 문 앞 = 안내 보임");
            GameState.SetPaused(true);
            yield return Lab.Frames(3);
            Assert.AreEqual("", hud.PromptText, "일시정지 중엔 안내 숨김(메뉴 조작 안내와 겹치던 것)");
            GameState.SetPaused(false);
            yield return Lab.Frames(3);
            w.OpenPhone(0);
            yield return Lab.Frames(3);
            Assert.AreEqual("", hud.PromptText, "휴대폰 중엔 숨김");
            w.Phone.Close();
            yield return Lab.Frames(3);
            // 이야기 카드(스토리보드) 중
            w.BotRead = 1.0f;
            var card = w.Def.Card("C1-06");
            w.StartCoroutine(w.RunCard(card));
            bool sawBoard = false;
            for (int i = 0; i < 60 && !sawBoard; i++) { yield return null; sawBoard = w.Story.Board.Shown; }
            Assert.IsTrue(sawBoard, "카드 판이 뜸");
            Assert.AreEqual("", hud.PromptText, "카드 중엔 안내 숨김(자막·카드 뒤로 '× 국밥집 — …' 가 비치던 것)");
            yield return Free(w);
        }

        // ───────────────────────── Q02 실내 문 스폰 — 카메라가 머리에 붙지 않음
        [UnityTest, Timeout(600000)]
        public IEnumerator Q02_Indoor_Door_Spawn_Has_Camera_Room()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            yield return Start(w, Mid(w.Def, 324, 15 * 60, SceneLoader.Zone1));
            var report = new List<string>(); var bad = new List<string>();
            foreach (var key in new[] { "Gukbap", "Conv", "Home", "School" })
            {
                yield return w.Loader.Go(key, "Door", false, Vector3.zero, 0f);
                yield return Lab.Frames(40);
                var cam = Camera.main;
                float d = Vector3.Distance(cam.transform.position, M3Lab.Motor.Position + Vector3.up * 1.5f);
                float want = w.Loader.ExpectedCamDistance;
                var sp = w.Loader.Stage.SpawnPoint("Door");
                report.Add($"{key} {d:F2}/{want:F2}m(스폰에서 {Vector3.Distance(sp.position, M3Lab.Motor.Position):F1}m · {Mathf.DeltaAngle(sp.eulerAngles.y, M3Lab.Motor.Yaw):F0}°)");
                if (d < Mathf.Min(1.6f, want * 0.6f)) bad.Add($"{key} {d:F2}m");
            }
            Debug.Log("[Q] Q02 실내 문 스폰 카메라 거리: " + string.Join(" · ", report));
            Assert.IsEmpty(bad, "문으로 들어오면 카메라가 뒤통수에 붙음(문 바로 앞 스폰): " + string.Join(", ", bad));
            yield return w.Loader.Go(SceneLoader.Zone1, null, true, w.FindDoor("Gukbap").ReturnPos, 0f);
        }

        // ───────────────────────── Q03 가계부 표시 자리 — 표가 실제로 뜸
        [UnityTest, Timeout(600000)]
        public IEnumerator Q03_Ledger_Marker_Shows_Table()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            yield return Start(w, Mid(w.Def, 304, 21 * 60, "Home"));
            w.BotSkill = 0f;      // 사람(표를 써 나감 · 누를 때까지)
            var pm = PlaceMarker.All.First(m => m.Func == "ledger");
            w.UseMarker("ledger", pm.transform);
            var ui = w.Story.LedgerUi;
            bool shown = false;
            for (int i = 0; i < 300 && !shown; i++) { yield return null; shown = ui.TableShown && ui.RowsShown >= 3; }
            Assert.IsTrue(shown, $"가계부를 눌렀는데 표가 안 뜸(빈 화면 — RevealNow 는 줄만 기록) · 표 {ui.TableShown} · 줄 {ui.RowsShown}");
            w.BotSkill = 0.9f; w.BotRead = 0.05f;
            yield return Free(w);
            Assert.IsFalse(ui.TableShown, "끝나면 표 닫힘");
        }

        // ───────────────────────── Q04 하늘이 방석 위에 앉음(떠 있지 않음)
        [UnityTest, Timeout(600000)]
        public IEnumerator Q04_Haneul_Sits_On_Cushion()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            yield return Start(w, Mid(w.Def, 304, 8 * 60, "Home"));
            var h = CutCtx.Actor("하늘");
            Assert.NotNull(h, "하늘 배역");
            var an = h.GetComponentInChildren<Animator>();
            Assert.NotNull(an);
            an.cullingMode = AnimatorCullingMode.AlwaysAnimate;     // 시험 카메라 밖이어도 자세를 그림
            yield return Lab.Frames(30);
            var hips = an.GetBoneTransform(HumanBodyBones.Hips);
            // 방석 = 배우 자리(Sock_Actor_하늘 은 방석 가운데 · 높이 0.06) · 바닥 = 엉덩이 아래로 쏜 바닥 충돌
            float floor = h.position.y - 0.06f;
            if (Physics.Raycast(hips.position, Vector3.down, out var hit, 3f, Layers.Mask(Layers.Ground, Layers.Default), QueryTriggerInteraction.Ignore)) floor = Mathf.Min(floor, hit.point.y);
            float up = hips.position.y - floor;
            // 방석 = 위에서 내려다본 사진(하늘·밥상 숨김)의 파란 칸(M_Cushion #7E8FB0) 가운데 — 키트 메시는 Read/Write 꺼짐이라 꼭짓점을 못 읽음
            var hide = h.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToList();
            foreach (var r in hide) r.enabled = false;
            var topGo = new GameObject("Q04 위 카메라"); var top = topGo.AddComponent<Camera>();
            topGo.transform.position = h.position + Vector3.up * 0.25f; topGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            top.orthographic = true; top.orthographicSize = 1.0f; top.nearClipPlane = 0.01f; top.farClipPlane = 1.0f; top.clearFlags = CameraClearFlags.SolidColor; top.backgroundColor = Color.black;
            var trt = new RenderTexture(200, 200, 24); top.targetTexture = trt; top.Render();
            RenderTexture.active = trt; var ttex = new Texture2D(200, 200, TextureFormat.RGB24, false); ttex.ReadPixels(new Rect(0, 0, 200, 200), 0, 0); ttex.Apply(); RenderTexture.active = null;
            foreach (var r in hide) r.enabled = true;
            var px = ttex.GetPixels32(); double sx = 0, sz = 0; int nb = 0;
            for (int y = 0; y < 200; y++) for (int x = 0; x < 200; x++) { var c = px[y * 200 + x]; if (c.b > c.r + 25 && c.b > c.g + 8 && c.b > 60) { sx += x; sz += y; nb++; } }
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "q04_haneul_top.png"), ttex.EncodeToPNG());
            Object.Destroy(trt); Object.Destroy(topGo); Object.Destroy(ttex);
            Assert.Greater(nb, 50, "하늘 1m 안에 방석(파란 칸)이 안 보임");
            var cc = new Vector2(h.position.x + (float)((sx / nb + 0.5) / 200.0 - 0.5) * 2f, h.position.z + (float)((sz / nb + 0.5) / 200.0 - 0.5) * 2f);
            Debug.Log($"[Q] Q04 방석(파란 칸 {nb}화소) 가운데 {cc:F2} · 배우 자리 {new Vector2(h.position.x, h.position.z):F2}");
            float off = Vector2.Distance(new Vector2(hips.position.x, hips.position.z), cc);
            var st = an.GetCurrentAnimatorStateInfo(0);
            var cp = h.GetComponent<CastPose>();
            Debug.Log($"[Q] Q04 하늘 엉덩이 높이 {up:F2}m · 방석 가운데(배우 자리)까지 {off:F2}m · 자세 {cp?.State} 재생 {(cp != null && cp.Holding)} · 상태 {st.shortNameHash} · 배우 {h.position:F2} · 엉덩이 {hips.position:F2}");
            // 옆에서 본 사진(그래픽이 있을 때만) — %TEMP%/q04_haneul_side.png
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                var go = new GameObject("Q04 옆 카메라"); var cam = go.AddComponent<Camera>();
                var right = Quaternion.Euler(0f, h.eulerAngles.y + 90f, 0f) * Vector3.forward;
                go.transform.position = h.position + right * 2.2f + Vector3.up * 0.5f; go.transform.LookAt(h.position + Vector3.up * 0.4f);
                cam.fieldOfView = 40f;
                var rt = new RenderTexture(960, 540, 24); cam.targetTexture = rt; cam.Render();
                RenderTexture.active = rt; var tex = new Texture2D(960, 540, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); tex.Apply(); RenderTexture.active = null;
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "q04_haneul_side.png"), tex.EncodeToPNG());
                cam.targetTexture = null; Object.Destroy(rt); Object.Destroy(go); Object.Destroy(tex);
            }
            var bones = new[] { HumanBodyBones.Hips, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.Head };
            Debug.Log("[Q] Q04 뼈 높이: " + string.Join(" · ", bones.Select(b => { var t = an.GetBoneTransform(b); return t != null ? $"{b} {t.position.y - floor:F2}" : b + " -"; })));
            Assert.LessOrEqual(up, 0.36f, "하늘 엉덩이가 바닥에서 떠 있음(양반다리인데 공중)");
            Assert.LessOrEqual(off, 0.12f, "하늘 엉덩이가 방석 가운데에서 벗어남(방석 옆 맨바닥 위에 떠 앉음)");
        }

        // ───────────────────────── Q05 문에서 나온 자리 — 등 뒤(카메라 쪽)가 트임
        [UnityTest, Timeout(600000)]
        public IEnumerator Q05_Door_Return_Faces_Open_Space()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            yield return Lab.Frames(5);
            var rows = new List<string>();
            foreach (var d in Object.FindObjectsByType<StageDoor>(FindObjectsSortMode.None).Where(x => x.Target != SceneLoader.Zone1 && x.ReturnPos != Vector3.zero))
            {
                var f = Quaternion.Euler(0f, d.ReturnYaw, 0f) * Vector3.forward;
                float c = SceneLoader.BackClear(d.ReturnPos, f);
                // 충돌 없는 표지(문 자리)가 카메라(뒤 4.5m)–머리 선 0.8m 안이면 거기서 가림
                var back = -new Vector2(f.x, f.z); var dp = new Vector2(d.transform.position.x - d.ReturnPos.x, d.transform.position.z - d.ReturnPos.z);
                float along = Vector2.Dot(dp, back);
                if (along > 0f && along < 4.5f && (dp - back * along).magnitude < 0.8f) c = Mathf.Min(c, along);
                rows.Add($"{d.Target} {c:F1}m");
                if (d.Target == "School") Assert.GreaterOrEqual(c, 2.5f, "혜성고 방면 표지에서 나오면 표지 기둥이 카메라와 시우 사이(머리를 가림)");
                Assert.GreaterOrEqual(c, 1.2f, $"{d.Target} 문에서 나온 자리 등 뒤가 막힘");
            }
            Debug.Log("[Q] Q05 문 나온 자리 등 뒤 빈 거리: " + string.Join(" · ", rows));
        }

        // ───────────────────────── Q06 실내에서도 해가 시계를 따라감(밤 9시 햇살 줄기 없음)
        [UnityTest, Timeout(600000)]
        public IEnumerator Q06_Indoor_Sun_Follows_Clock()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            yield return Start(w, Mid(w.Def, 304, 12 * 60, "Home"));
            var sun = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional && l.gameObject.scene != w.Loader.Stage.gameObject.scene);
            Assert.NotNull(sun, "해(Zone1 방향광)");
            yield return Lab.Seconds(0.6f);
            float noon = sun.intensity;
            GameClock.SetDate(2026, 3, 4, 21 * 60 + 30);
            yield return Lab.Seconds(0.6f);
            float night = sun.intensity;
            Debug.Log($"[Q] Q06 시우네 안 해 세기: 12:00 {noon:F2} → 21:30 {night:F2} · 시간대 {DayLight.Instance?.ClockFrom}");
            Assert.Less(night, noon * 0.5f, "집 안에서 밤 9시 반인데 해가 낮 세기 그대로(창으로 햇살 줄기)");
        }

        // ───────────────────────── Q07 창밖 그림이 밤엔 어두움
        [UnityTest, Timeout(600000)]
        public IEnumerator Q07_Window_View_Darkens_At_Night()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            yield return Start(w, Mid(w.Def, 304, 12 * 60, "Home"));
            var life = w.Loader.Stage.GetComponentInChildren<StageLife>();
            Assert.NotNull(life, "시우네 StageLife");
            life.Apply(12 * 60);
            var day = life.LastViewTint;
            life.Apply(22 * 60 + 30);
            var night = life.LastViewTint;
            Debug.Log($"[Q] Q07 창밖 그림 색: 낮 {day} · 밤 {night}");
            Assert.Greater(day.r, 0.95f, "낮엔 그대로");
            Assert.Less(night.r + night.g + night.b, 1.2f, "밤엔 창밖이 한낮처럼 밝음");
        }

        // ───────────────────────── Q08 긴 알림은 판 안에서 줄 바꿈
        [UnityTest, Timeout(600000)]
        public IEnumerator Q08_Long_Toast_Stays_In_Plate()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            yield return Start(w, Mid(w.Def, 304, 15 * 60, SceneLoader.Zone1));
            w.Hud.ClearToasts();
            w.Hud.Toast("새벽 국밥 배달 B — +10,000원 → 엄마 통장 · 피로 +6 · 스태미나 +0.3 · 스피드 +0.2 · 동네 평판 +0.3 · 멘탈 +0.1");
            yield return Lab.Frames(30);
            var t = w.Hud.GetComponentsInChildren<TextMeshProUGUI>().FirstOrDefault(x => x.name == "글" && x.transform.parent.name == "알림 줄");
            Assert.NotNull(t, "알림 줄");
            t.ForceMeshUpdate();
            var plate = (RectTransform)t.transform.parent;
            Debug.Log($"[Q] Q08 알림 글 {t.textBounds.size.x:F0}×{t.textBounds.size.y:F0} · 판 {plate.rect.width:F0}×{plate.rect.height:F0}");
            Assert.LessOrEqual(t.textBounds.size.x, plate.rect.width + 2f, "알림 글이 판 밖으로(가운데 '× 들어가기' 안내와 겹침)");
            Assert.LessOrEqual(t.textBounds.size.y, plate.rect.height + 2f, "줄 바꾼 글이 판 위아래로 넘침");
        }

        // ───────────────────────── Q09 미니게임 판 첫 프레임에 지난 판 글 없음
        [UnityTest, Timeout(600000)]
        public IEnumerator Q09_Job_Panel_Starts_Clean()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            yield return Start(w, Mid(w.Def, 304, 15 * 60, SceneLoader.Zone1));
            var job = w.Job;
            float s1 = -1f;
            yield return job.Play("serve", 1f, 7, v => s1 = v);
            Assert.GreaterOrEqual(s1, 0f);
            float s2 = -1f;
            var co = w.StartCoroutine(job.Play("serve", 1f, 8, v => s2 = v));
            Assert.IsTrue(job.Shown, "판이 바로 켜짐");
            Assert.IsFalse(job.PromptText.Contains("끝"), $"새 판 첫 프레임에 지난 판 결과가 보임: '{job.PromptText}'");
            for (int i = 0; i < 60 * 120 && s2 < 0f; i++) yield return null;
        }

        // ───────────────────────── Q10 · Q12 드릴 중 — 세계 HUD 숨김 · 관원이 카메라를 가리지 않음
        [UnityTest, Timeout(900000)]
        public IEnumerator Q10_Q12_Drill_Hud_And_Camera()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            yield return Start(w, Mid(w.Def, 310, 16 * 60 + 45, SceneLoader.Gym, m => m.G.Fatigue = 0));
            GymManager gm = null;
            for (int i = 0; i < 300 && gm == null; i++) { gm = GymManager.Instance; yield return null; }
            Assert.NotNull(gm);
            Assert.IsTrue(gm.StartDrill("D01", 1f), "D01 시작");
            yield return Lab.Frames(150);
            Assert.IsFalse(w.Hud.Shown, "드릴 중에 세계 HUD(날짜·지갑)가 드릴 이름판 위에 겹침");
            var cam = Camera.main;
            Vector3 a = cam.transform.position, b = M3Lab.Motor.Position + Vector3.up * 1.2f;
            foreach (var n in gm.Members.Where(n => n != null && n.gameObject.activeSelf))
            {
                var p = n.transform.position + Vector3.up;
                var ab = b - a; float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
                var q = a + ab * t;
                float dd = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(q.x, q.z));
                Assert.IsFalse(dd < 0.8f && t > 0.02f && t < 0.98f, $"관원 {n.name} 이 드릴 카메라와 시우 사이({dd:F2}m)");
            }
            Debug.Log($"[Q] Q10·Q12 D01: 세계 HUD {w.Hud.Shown} · 비켜 준 관원 {gm.SightHidden}");
            gm.Runner.Abort();
            for (int i = 0; i < 300 && gm.Busy; i++) { gm.CloseCard(); yield return null; }
        }

        // ───────────────────────── Q11 땋기 — 시우가 하늘 뒤 자리로
        [UnityTest, Timeout(600000)]
        public IEnumerator Q11_Braid_Places_Siwoo_Behind_Haneul()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            yield return Start(w, Mid(w.Def, 304, 8 * 60, "Home"));
            var spot = CutCtx.Actor("땋기 자리");
            Assert.NotNull(spot, "시우네 '땋기 자리'");
            w.Story.ApplyNow("place:시우:땋기 자리");
            yield return Lab.Frames(3);
            float d = Vector2.Distance(new Vector2(M3Lab.Motor.Position.x, M3Lab.Motor.Position.z), new Vector2(spot.position.x, spot.position.z));
            Assert.Less(d, 0.15f, "place 이벤트로 시우가 땋기 자리에");
            var lines = w.Story.Dialogue.Get("1-5");
            int pi = lines.FindIndex(l => l.Mode == DlgMode.Event && l.Text.StartsWith("place:시우:땋기 자리"));
            int bi = lines.FindIndex(l => l.Mode == DlgMode.Event && l.Text.StartsWith("braidgame:1"));
            Assert.IsTrue(pi >= 0 && pi < bi, "1-5 대사: 첫 땋기 전에 시우를 땋기 자리로(2m 밖에 서서 머리를 땋던 것)");
        }

        // ───────────────────────── Q13 하늘 메모가 종이 안에
        [UnityTest, Timeout(600000)]
        public IEnumerator Q13_Quest_Memo_Fits_Paper()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            var ui = w.Story.LedgerUi;
            ui.ShowQuest("〈반하늘 11세 생일 요구사항 (최종_진짜최종)〉", new[] { "1. 케이크 — 2호 초코 생크림(비싸면 1호도 됨)", "2. 초 열한 개(숫자 초 금지)", "3. 오빠 노래 금지", "4. 소원은 내가 빈다", "5. 엄마 일찍 오기", "6. 사진 세 장 이상", "7. 고양이 이름 짓기", "8. 오빠 국밥 냄새 금지", "9. 반장 할머니 초대" }, "D-56 · 봉투 0 / 40,000");
            yield return Lab.Frames(3);
            var t = ui.GetComponentsInChildren<TextMeshProUGUI>().First(x => x.name == "글" && x.transform.parent.name == "하늘 메모");
            t.ForceMeshUpdate();
            var r = t.rectTransform.rect;
            Debug.Log($"[Q] Q13 하늘 메모 글 {t.fontSize:F0}pt · 글 {t.preferredWidth:F0}×{t.preferredHeight:F0} · 칸 {r.width:F0}×{r.height:F0} · 넘침 {t.isTextOverflowing}");
            Assert.IsFalse(t.isTextOverflowing, "하늘 메모 글이 종이 밖으로 넘침");
            Assert.LessOrEqual(t.textBounds.size.y, r.height + 2f);
            ui.HideQuest();
        }

        // ───────────────────────── Q14 땋기 판 — 제목과 박자 글이 겹치지 않음
        [UnityTest, Timeout(600000)]
        public IEnumerator Q14_Braid_Title_Clear_Of_Score()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            var braid = w.Story.Braid;
            Assert.NotNull(braid);
            bool stop = false;
            var co = w.StartCoroutine(braid.Play(1, true, () => stop));
            yield return Lab.Frames(10);
            var texts = braid.GetComponentsInChildren<TextMeshProUGUI>();
            var title = texts.First(x => x.name == "제목"); var score = texts.First(x => x.name == "점수");
            title.ForceMeshUpdate(); score.ForceMeshUpdate();
            Rect R(TextMeshProUGUI t) { var b = t.textBounds; Vector3 a0 = t.rectTransform.TransformPoint(b.min), a1 = t.rectTransform.TransformPoint(b.max); return Rect.MinMaxRect(a0.x, a0.y, a1.x, a1.y); }
            var rt = R(title); var rs = R(score);
            Debug.Log($"[Q] Q14 땋기 제목 「{title.text}」 {rt} · 박자 「{score.text}」 {rs}");
            Assert.IsFalse(rt.Overlaps(rs) || (rs.xMin - rt.xMax) < 8f && Mathf.Abs(rs.center.y - rt.center.y) < 30f, "땋기 제목과 '박자 0/8' 이 붙어 한 줄처럼 읽힘");
            stop = true;
            for (int i = 0; i < 600 && braid.Shown; i++) yield return null;
        }

        // ───────────────────────── Q15 판이 떠 있는 동안 알림 줄 숨김
        [UnityTest, Timeout(600000)]
        public IEnumerator Q15_Toasts_Wait_While_Panel()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            yield return Start(w, Mid(w.Def, 304, 15 * 60, SceneLoader.Zone1));
            w.Hud.ClearToasts();
            float score = -1f;
            var co = w.StartCoroutine(w.Job.Play("serve", 0.3f, 3, v => score = v));
            yield return Lab.Frames(5);
            w.Hud.Toast("국밥집 저녁(서빙·설거지) C — +20,000원 → 엄마 통장");
            yield return Lab.Frames(30);
            Assert.IsTrue(w.Job.Shown);
            Assert.IsFalse(w.Hud.Shown, "판 중엔 HUD 숨김");
            var live = w.Hud.GetComponentsInChildren<TextMeshProUGUI>(false).Where(x => x.name == "글" && x.transform.parent.name == "알림 줄").ToList();
            Assert.AreEqual(0, live.Count, "미니게임 판 위로 알림 줄이 겹침(왼쪽 아래 '← 1번 상')");
            for (int i = 0; i < 60 * 120 && score < 0f; i++) yield return null;
            yield return Free(w);
            yield return Lab.Frames(30);
            Assert.Greater(w.Hud.GetComponentsInChildren<TextMeshProUGUI>(false).Count(x => x.name == "글" && x.transform.parent.name == "알림 줄"), 0, "판이 닫히면 기다린 알림이 뜸");
        }

        // ───────────────────────── Q17 미트 드릴 뒤 전투 HUD 가 남지 않음
        [UnityTest, Timeout(900000)]
        public IEnumerator Q17_No_Combat_Hud_After_Mitts()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            yield return Start(w, Mid(w.Def, 310, 16 * 60 + 45, SceneLoader.Gym, m => m.G.Fatigue = 0));
            GymManager gm = null;
            for (int i = 0; i < 300 && gm == null; i++) { gm = GymManager.Instance; yield return null; }
            Assert.NotNull(gm);
            yield return gm.PlayBot("D04", 1f);
            yield return Free(w);
            yield return Lab.Frames(20);
            var hud = Object.FindAnyObjectByType<CombatHud>();
            Assert.NotNull(hud);
            var canvas = hud.transform.Find("전투 HUD");
            var pc = hud.Player;
            Debug.Log($"[Q] Q17 D04 미트 뒤: 전투 {(pc != null && pc.Active)} · 전투 HUD 캔버스 {(canvas != null && canvas.gameObject.activeSelf)}");
            Assert.IsFalse(pc != null && pc.Active, "미트 드릴 뒤 전투 상태");
            Assert.IsFalse(canvas != null && canvas.gameObject.activeSelf, "미트를 친 뒤 전투 HUD('반시우' 체력 바)가 켜진 채 남음 — 세계 HUD 와 겹침");
        }

        // ───────────────────────── Q18 하늘 케이크 봉투는 생긴 뒤에만 보임
        [UnityTest, Timeout(600000)]
        public IEnumerator Q18_Envelope_Hidden_Until_Cake_Quest()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            yield return Start(w, Mid(w.Def, 304, 15 * 60, SceneLoader.Zone1));
            yield return Lab.Frames(5);
            Assert.IsFalse(w.Model.F.Has("cake_quest"));
            StringAssert.DoesNotContain("봉투", w.Hud.MoneyLine, "3/4 — 아직 하늘 생일 요구사항(3/6) 전인데 HUD 에 '봉투'");
            w.OpenPhone(3); yield return Lab.Frames(3);
            StringAssert.DoesNotContain("하늘 케이크", w.Phone.Body, "휴대폰 가계부 칸에 아직 모르는 '하늘 케이크'");
            w.Phone.Close(); yield return Lab.Frames(3);
            w.Model.F.Raise("cake_quest");
            yield return Lab.Frames(5);
            StringAssert.Contains("봉투", w.Hud.MoneyLine, "생일 요구사항 뒤엔 봉투가 보임");
            // Q21: 자유 이동(세계 HUD 보임) 중엔 이야기 날짜 판이 남지 않음
            w.Story.Hud.ShowDate();
            yield return Lab.Frames(3);
            Assert.IsTrue(w.Hud.Shown);
            Assert.IsFalse(w.Story.Hud.DateShown, "세계 HUD 와 이야기 날짜 판이 왼쪽 위에 겹쳐 뜸");
        }

        // ───────────────────────── Q19 주간 계획 맡긴 뒤 쌓인 알림을 비움 · Q20 미디엄 카메라가 막히면 빈 쪽으로
        [UnityTest, Timeout(900000)]
        public IEnumerator Q19_Q20_Planner_Toasts_And_Mid_Camera()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            yield return Start(w, Mid(w.Def, 322, 22 * 60 + 30, "Home"));
            w.BotRead = 0.02f;
            // 일요일 밤 잠 → 주간 계획(봇이면 바로 맡기기) → 2주 몽타주 — 그동안 쌓인 알림이 몽타주 뒤에 줄줄이 뜨지 않음
            bool slept = false;
            IEnumerator Wrap() { yield return w.Sleep(false); slept = true; }
            w.StartCoroutine(Wrap());
            for (int i = 0; i < 60 * 900 && !slept; i++) yield return null;
            Assert.IsTrue(slept, "잠·주간 계획이 끝남");
            Debug.Log($"[Q] Q19 맡긴 뒤 남은 알림 {w.Hud.PendingToasts} · 맡기기 {w.AutoRuns}번 · {w.Model.Status}");
            Assert.Greater(w.AutoRuns, 0, "일요일 밤 주간 계획이 돌아감");
            Assert.LessOrEqual(w.Hud.PendingToasts, 4, "2주 맡긴 뒤 지난 알림(레슨 예약·봉투…)이 줄줄이 남음");
            // Q20: 하루편의점 계산대(job_counter) 앞에서 미디엄 카메라
            yield return w.Loader.Go("Conv", "Door", false, Vector3.zero, 0f);
            yield return Lab.Frames(20);
            var pm = PlaceMarker.All.First(m => m.Func == "job_counter");
            M3Lab.Motor.Teleport(M3Lab.Motor.Position + new Vector3(-2.5f, 0f, 1.5f), 90f);    // 다른 데 서 있다가
            yield return Lab.Frames(10);
            M3Lab.Motor.Teleport(pm.transform.position, pm.transform.eulerAngles.y);            // 알바처럼 자리로 옮긴 그 프레임에 카메라를 만듦
            var cam = CutCams.Make(CamPreset.Mid, M3Lab.Motor.transform, null);
            cam.AvoidProps = true; cam.Snap();          // 알바 미니게임과 같은 카메라(WorldRunner 알바)
            cam.SetLive(true);
            yield return Lab.Frames(10);
            var head = M3Lab.Motor.Position + Vector3.up * 1.4f;
            float d = Vector3.Distance(cam.transform.position, head);
            // 카메라 → 가슴 사이를 가리는 소품(충돌 없는 포스기·모니터 등 — 렌더러 경계로 따로 셈)
            var chest = M3Lab.Motor.Position + Vector3.up * 1.25f;
            var from = cam.transform.position; var seg = chest - from; float len = seg.magnitude * 0.85f;
            var hide = new List<string>();
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || r.transform.IsChildOf(M3Lab.Motor.transform)) continue;
                var b = r.bounds; if (b.size.x > 3f || b.size.z > 3f || b.size.y > 2.6f) continue;
                b.Expand(-0.05f);
                if (b.IntersectRay(new Ray(from, seg.normalized), out float t) && t < len) hide.Add(r.name);
            }
            var pos = PlaceMarker.All.First(m => m.Func == "job_counter").transform.position;
            bool posBlock = Physics.OverlapSphere(pos + Vector3.up * 1.25f, 1.3f, 1 << Layers.CamBlock, QueryTriggerInteraction.Ignore).Length > 0;
            bool lineHit = Physics.Linecast(from, Vector3.Lerp(from, chest, 0.85f), out var lh, Layers.CameraBlock, QueryTriggerInteraction.Ignore);
            if (lineHit) hide.Add("충돌 " + lh.collider.name);
            Debug.Log($"[Q] Q20 계산대 미디엄 카메라 — 머리까지 {d:F2}m · 돌림 {cam.SpinDeg:F0}° · 포스기 막이 {posBlock} · 가린 것 [{string.Join(", ", hide)}]");
            Assert.IsTrue(posBlock, "하루편의점 포스기 화면(키트 메시 — 렌더러 경계로 못 잡음)에 카메라 막이(CamBlock)가 없음");
            CutCams.Release(cam);
            Assert.GreaterOrEqual(d, 1.0f, "계산대 알바 미니게임 카메라가 시우 몸 속(앞이 막혀 당겨짐)");
            Assert.IsEmpty(hide, "계산대 알바 미니게임 카메라 앞을 소품이 가림(포스기 등)");
            // 알바가 끝난 뒤: 계산대 안쪽에 그대로 서 있으면 바깥 카메라가 머리 속 → 카메라 뒤 공간이 나는 자리로
            w.RoomAfterJob();
            yield return Lab.Frames(10);
            float back = SceneLoader.BackClear(M3Lab.Motor.Position, Quaternion.Euler(0f, M3Lab.Motor.Yaw, 0f) * Vector3.forward);
            Debug.Log($"[Q] Q20 알바 뒤 카메라 뒤 공간 {back:F2}m · 자리 {M3Lab.Motor.Position:F2}");
            Assert.GreaterOrEqual(back, 1.2f, "알바가 끝난 뒤 카메라가 시우 머리 속");
        }

        // ───────────────────────── Q24 빌드 화면에서 시우·가게 불빛 둘레가 하얗게 날아감(후문 상가거리 — 새벽 배달 출발·밤 걷기·한낮)
        //   원인: URP 기본 볼륨 프로필(PC_RPAsset → SampleSceneProfile)의 블룸 0.25 · 클램프 없음 — 화면(HDR 버퍼)에서만 켜져, 가게 창 점광에 비친
        //   시우 몸의 HDR 값이 화면 반을 덮는 흰 덩어리로 번짐. 시험 사진(Shots.Grab · LDR RT)·룩데브 평가엔 안 보였음 → 볼륨 스택의 블룸 세기를 직접 봄
        [UnityTest, Timeout(900000)]
        public IEnumerator Q24_No_Bloom_Anywhere()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            var bc = Object.FindAnyObjectByType<BikeCourse>(FindObjectsInactive.Include);
            yield return Start(w, Mid(w.Def, 310, 6 * 60 + 4, SceneLoader.Zone1, null, bc != null ? bc.Start : (Vector3?)null));
            var rows = new List<string>(); var bad = new List<string>();
            foreach (var (place, min) in new[] { (SceneLoader.Zone1, 6 * 60 + 4), (SceneLoader.Zone1, 12 * 60), (SceneLoader.Zone1, 21 * 60 + 30), ("Home", 21 * 60 + 30), ("Gukbap", 18 * 60), ("Conv", 22 * 60) })
            {
                GameClock.SetDate(2026, 3, 10, min);
                if (w.Loader.Place != place) yield return w.Loader.Go(place, place == SceneLoader.Zone1 ? null : "Door", place == SceneLoader.Zone1, bc != null ? bc.Start : Vector3.zero, 0f);
                yield return Lab.Seconds(0.6f);
                var bloom = UnityEngine.Rendering.VolumeManager.instance.stack.GetComponent<UnityEngine.Rendering.Universal.Bloom>();
                float k = bloom != null && bloom.active ? bloom.intensity.value : 0f;
                rows.Add($"{place} {GameClock.TimeText} 블룸 {k:F2}");
                if (k > 0.001f) bad.Add($"{place} {GameClock.TimeText} {k:F2}");
            }
#if UNITY_EDITOR
            var prof = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>("Assets/Settings/SampleSceneProfile.asset");
            if (prof != null && prof.TryGet<UnityEngine.Rendering.Universal.Bloom>(out var db)) { rows.Add($"기본 프로필 블룸 {db.intensity.value:F2}"); if (db.active && db.intensity.overrideState && db.intensity.value > 0.001f) bad.Add("기본 프로필(SampleSceneProfile) 블룸 " + db.intensity.value); }
#endif
            Debug.Log("[Q] Q24 블룸: " + string.Join(" · ", rows));
            Assert.IsEmpty(bad, "블룸이 켜져 있음(빌드 화면에서 불빛 둘레가 하얗게 날아감): " + string.Join(", ", bad));
        }

        // ───────────────────────── Q26 드릴 콤비 아이콘 글자가 붓 글꼴에 다 있음('✕' → □)
        [Test]
        public void Q26_Drill_Icon_Glyphs_In_Brush_Font()
        {
#if UNITY_EDITOR
            var fonts = UnityEditor.AssetDatabase.LoadAssetAtPath<UiFonts>("Assets/_Project/Settings/UiFonts.asset");
            Assert.NotNull(fonts, "UiFonts");
            Assert.NotNull(fonts.Br, "붓 글꼴");
            string need = "□△─↓R2×";     // □ △ ─ ↓ R 2 × — 콤비 토큰 아이콘 + 틀림 표시
            bool all = fonts.Br.HasCharacters(need, out uint[] miss, true, false);
            Assert.IsTrue(all, "붓 글꼴에 없는 드릴 아이콘 글자: " + (miss != null ? string.Concat(miss.Select(u => char.ConvertFromUtf32((int)u))) : ""));
            var src = System.IO.File.ReadAllText("Assets/_Project/Scripts/Gym/DrillHud.cs");
            Assert.IsFalse(src.Contains("✕"), "DrillHud 가 붓 글꼴에 없는 '✕'(U+2715)를 씀 — 화면에 네모(□)");
#else
            Assert.Ignore("편집기 전용");
#endif
        }

        // ───────────────────────── Q27 시비 반복 — 이긴 다음 날 주차장에 다시 오면 다시 걸림
        [UnityTest, Timeout(600000)]
        public IEnumerator Q27_Street_Fight_Rearms_Next_Day()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            var pk = w.Def.Balance.Point("parking");
            yield return Start(w, Mid(w.Def, 311, 17 * 60, SceneLoader.Zone1, m => m.Unlock("street_repeat"), pk.Pos + new Vector3(0f, 0.5f, 25f)));
            yield return Lab.Frames(30);
            var enc = Object.FindAnyObjectByType<Encounter>();
            Assert.NotNull(enc, "주차장 45m 안 — 시비 무대가 켜짐");
            // 이긴 상태로(그날)
            typeof(Encounter).GetMethod("Go", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(enc, new object[] { Encounter.Phase.Cleared });
            w.Model.AddCount("street.day", w.Model.Today - w.Model.Count("street.day"));
            yield return Lab.Frames(30);
            Assert.AreEqual(Encounter.Phase.Cleared, enc.State, "같은 날엔 다시 안 걸림");
            GameClock.SetDate(2026, 3, 12, 17 * 60);
            yield return Lab.Frames(10);
            yield return Free(w);
            yield return Lab.Frames(30);
            Assert.AreEqual(Encounter.Phase.Armed, enc.State, "다음 날 주차장 근처에 오면 시비가 다시 걸림(이긴 뒤 영영 안 걸리던 것)");
            Assert.Greater(Vector3.Distance(M3Lab.Motor.Position, pk.Pos), 10f, "다시 걸어 둘 때 시우를 옮기지 않음");
        }

        // ───────────────────────── Q22 휴대폰 '오늘' 칸이 빈칸이 아님 · Q23 밤엔 혜성고 교문이 닫힘
        [UnityTest, Timeout(600000)]
        public IEnumerator Q22_Q23_Phone_Today_And_School_Gate_At_Night()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            var door = w.FindDoor("School");
            Assert.NotNull(door, "Zone1 혜성고 방면 표지(문)");
            yield return Start(w, Mid(w.Def, 324, 21 * 60, SceneLoader.Zone1, null, door.ReturnPos));
            var pt = typeof(WorldRunner).GetMethod("PhoneText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            string today = (string)pt.Invoke(w, new object[] { 1 });
            var head = today.Split("열린 곳")[0];
            Assert.IsTrue(head.Contains("◆") || head.Contains("오늘은 없음"), "오늘 뜰 이야기 칸이 비어 있음: " + head);
            int uses = StageDoor.Uses;
            door.Use();
            yield return Lab.Frames(30);
            for (int i = 0; i < 600 && w.Loader.Busy; i++) yield return null;
            Assert.AreEqual(SceneLoader.Zone1, w.Loader.Place, "밤 9시에 학교(불 켜진 교실)로 들어가졌음");
            Assert.AreEqual(uses, StageDoor.Uses, "문지기가 막아야 함");
        }

        // ───────────────────────── Q28 무대에 개발용 자리 글('자리 무대 — 9단계에서 키트로')이 없음
        [UnityTest, Timeout(600000)]
        public IEnumerator Q28_No_Dev_Placeholder_Text_In_Stages()
        {
            var bad = new List<string>(); int seen = 0;
            foreach (var key in new[] { "Site", "Flash", "Gukbap", "Home", "Conv", "School" })
            {
                var path = SceneLoader.StagePath(key);
                var op = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(path, UnityEngine.SceneManagement.LoadSceneMode.Additive);
                Assert.NotNull(op, path);
                while (!op.isDone) yield return null;
                var sc = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
                foreach (var r in sc.GetRootGameObjects())
                    foreach (var t in r.GetComponentsInChildren<TMP_Text>(true))
                    {
                        seen++;
                        if (t.text != null && (t.text.Contains("자리 무대") || t.text.Contains("단계에서"))) bad.Add($"{key}/{t.name}: {t.text}");
                    }
                yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(sc);
            }
            Debug.Log($"[Q] Q28 무대 글 {seen}개 · 개발용 글 {bad.Count}");
            Assert.GreaterOrEqual(seen, 2, "무대 이름판(현장·회상 거실)을 못 읽음(장면 경로)");
            Assert.IsEmpty(bad, string.Join(" | ", bad));
        }

        // ───────────────────────── Q29 무대 자리에 선 채 장소만 Zone1 로 돌아간 순간 '맵 밖으로 떨어짐' 순간이동을 매 프레임 되풀이하지 않음
        [UnityTest, Timeout(300000)]
        public IEnumerator Q29_No_Respawn_Loop_While_Place_Changes()
        {
            yield return M3Lab.OpenZone1();
            var m = M3Lab.Motor;
            Assert.NotNull(m);
            float kill = m.KillY;
            var home = m.Position;
            m.Teleport(new Vector3(2003f, 0f, 1998.65f), 0f);    // 무대(편의점 쪽) 자리 — Zone1 바닥 없음, KillY 는 Zone1 값
            m.KillY = Mathf.Max(kill, 5f);
            int r0 = m.Respawns;
            yield return Lab.Frames(60);
            Debug.Log($"[Q] Q29 대기 중 리스폰 {m.Respawns - r0} · KillY {m.KillY:F1}");
            Assert.AreEqual(r0, m.Respawns, "안전 지점도 KillY 아래(곧 옮겨질 자리)면 리스폰을 되풀이하지 않음");
            m.KillY = kill;
            m.Teleport(home, 0f);
            yield return Lab.Frames(10);
        }

        // ───────────────────────── Q25 한 렌더러가 LOD 묶음 둘에 걸리지 않음(Zone1·체육관)
        [UnityTest, Timeout(600000)]
        public IEnumerator Q25_No_Renderer_In_Two_Lod_Groups()
        {
            yield return M3Lab.OpenZone1();
            var op = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(SceneLoader.GymScene, UnityEngine.SceneManagement.LoadSceneMode.Additive);
            while (op != null && !op.isDone) yield return null;
            var owner = new Dictionary<Renderer, LODGroup>();
            var dup = new List<string>();
            int groups = 0;
            foreach (var g in Object.FindObjectsByType<LODGroup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                groups++;
                foreach (var l in g.GetLODs())
                    foreach (var r in l.renderers)
                    {
                        if (r == null) continue;
                        if (owner.TryGetValue(r, out var o) && o != g) { if (dup.Count < 8) dup.Add($"{r.name}({o.name}·{g.name})"); }
                        else owner[r] = g;
                    }
            }
            Debug.Log($"[Q] Q25 LOD 묶음 {groups} · 렌더러 {owner.Count} · 겹침 {dup.Count}");
            var gs = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(SceneLoader.GymScene);
            if (gs.IsValid()) yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(gs);
            Assert.Greater(groups, 50);
            Assert.IsEmpty(dup, "LOD 묶음 둘에 걸린 렌더러: " + string.Join(" ", dup));
        }

        // ───────────────────────── Q30 갓길 1톤 트럭은 몸·카메라가 뚫고 지나가지 않음
        [UnityTest, Timeout(300000)]
        public IEnumerator Q30_Parked_Truck_Blocks_Body_And_Camera()
        {
            yield return M3Lab.OpenZone1();
            var truck = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.name == "1톤 트럭(갓길)");
            Assert.NotNull(truck, "Zone1 갓길 1톤 트럭");
            var col = truck.GetComponentInChildren<Collider>();
            Assert.NotNull(col, "트럭에 충돌이 없음 — 시우·카메라가 뚫고 지나감");
            Physics.SyncTransforms();
            var c = col.bounds.center;
            bool camHit = Physics.Raycast(c + truck.right * 4f, -truck.right, out var hit, 6f, Layers.CameraBlock, QueryTriggerInteraction.Ignore) && hit.collider == col;
            var m = M3Lab.Motor;
            bool bodyHits = !Physics.GetIgnoreLayerCollision(m.gameObject.layer, col.gameObject.layer);
            Debug.Log($"[Q] Q30 트럭 충돌 {col.bounds.size:F2} · 카메라 막힘 {camHit} · 몸 충돌 {bodyHits}");
            Assert.IsTrue(camHit, "카메라 레이(CameraBlock)가 트럭에 막혀야 함");
            Assert.IsTrue(bodyHits, "시우 레이어가 트럭 레이어와 부딪혀야 함");
            // 시우를 트럭 옆에서 트럭 쪽으로 2초 걷게 — 트럭 상자 안으로 못 들어감
            var start = c - truck.right * (col.bounds.extents.magnitude * 0.0f + 2.6f); start.y = truck.position.y + 0.1f;
            m.Teleport(start, Quaternion.LookRotation(truck.right).eulerAngles.y);
            yield return Lab.Frames(5);
            for (int i = 0; i < 120; i++) { m.SetMoveInput(truck.right, 1f, false); yield return null; }
            m.ClearMoveInput();
            var body = m.Position + Vector3.up * 0.9f;
            float gap = Vector3.Distance(col.ClosestPoint(body), body);      // 상자 안이면 0
            Debug.Log($"[Q] Q30 걸어 붙은 뒤 몸–트럭 {gap:F2}m");
            Assert.Greater(gap, 0.05f, "시우가 트럭 속으로 들어감");
        }

        // ───────────────────────── Q31 무대의 낮은 턱(국밥집 창 밑 0.35 · 밥상 0.32 · 편의점 받침 0.30)에 몸이 올라타지 않음
        [UnityTest, Timeout(600000)]
        public IEnumerator Q31_No_Walking_On_Low_Ledges()
        {
            var low = new List<string>();
            foreach (var key in new[] { "Conv", "Gukbap", "Home", "School" })
            {
                var path = SceneLoader.StagePath(key);
                var op = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(path, UnityEngine.SceneManagement.LoadSceneMode.Additive);
                while (!op.isDone) yield return null;
                var sc = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
                foreach (var r in sc.GetRootGameObjects())
                    foreach (var bc in r.GetComponentsInChildren<BoxCollider>(true))
                    {
                        if (bc.gameObject.layer == Layers.Ground || bc.isTrigger) continue;
                        var b = bc.bounds; float floor = r.transform.position.y;
                        if (b.min.y - floor < 0.05f && b.max.y - floor > 0.2f && b.max.y - floor < 0.5f) low.Add($"{key}/{bc.name} 윗면 {b.max.y - floor:F2}");
                    }
                yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(sc);
            }
            Assert.IsEmpty(low, "몸이 올라탈 수 있는 낮은 상자: " + string.Join(" · ", low));
            // 국밥집 창 밑 턱을 따라 걸어 봄(QA 봇이 문으로 가다 0.31m 떠서 걸었던 자리)
            WorldRunner w = null;
            yield return Open(x => w = x);
            yield return Start(w, Mid(w.Def, 324, 15 * 60, "Gukbap"));
            var m = M3Lab.Motor;
            m.Teleport(new Vector3(1999.0f, 0.02f, 1996.45f), 180f);
            yield return Lab.Frames(5);
            float maxY = 0f;
            for (int i = 0; i < 150; i++) { m.SetMoveInput(i < 60 ? Vector3.back : new Vector3(1f, 0f, -0.4f).normalized, 1f, false); maxY = Mathf.Max(maxY, m.Position.y); yield return null; }
            m.ClearMoveInput();
            Debug.Log($"[Q] Q31 창 밑 턱 따라 걷기 최고 높이 {maxY:F2}m");
            Assert.Less(maxY, 0.1f, "국밥집 창 밑 턱에 올라탐");
        }

        // ───────────────────────── Q32 스파링(D11) 중 드릴 이름판이 전투 HUD('반시우' 체력·기세)와 겹치지 않음
        [UnityTest, Timeout(900000)]
        public IEnumerator Q32_Spar_Plate_Clear_Of_Combat_Hud()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            yield return Start(w, Mid(w.Def, 310, 16 * 60 + 45, SceneLoader.Gym, m => m.G.Fatigue = 0));
            GymManager gm = null;
            for (int i = 0; i < 300 && gm == null; i++) { gm = GymManager.Instance; yield return null; }
            Assert.NotNull(gm);
            var run = w.StartCoroutine(gm.PlayBot("D11", 1f));
            CombatHud ch = null; int both = 0, clash = 0; float plate = 0f;
            for (int i = 0; i < 60 * 120 && (gm.Busy || i < 60); i++)
            {
                if (ch == null) ch = Object.FindAnyObjectByType<CombatHud>();
                if (ch != null && ch.Visible && gm.Hud != null && gm.Busy)
                {
                    both++; plate = gm.Hud.PlateY;
                    if (gm.Hud.PlateY > -150f) clash++;      // 전투 HUD '시우' 판 = 위 48 + 높이 110 → 이름판은 그 아래(-176)
                    if (both > 120) break;
                }
                yield return null;
            }
            Debug.Log($"[Q] Q32 스파링 중 전투 HUD·드릴 판 같이 뜬 프레임 {both} · 겹침 {clash} · 이름판 y {plate}");
            Assert.Greater(both, 0, "스파링 중 전투 HUD 가 안 떴음(시험 전제)");
            Assert.AreEqual(0, clash, "스파링 드릴 이름판이 전투 HUD 와 겹침");
        }

        // ───────────────────────── Q33 스파링(D11) 카메라가 케이지 안(철망·윗테 너머가 아님)
        [UnityTest, Timeout(900000)]
        public IEnumerator Q33_Spar_Camera_Inside_Cage()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            yield return Start(w, Mid(w.Def, 310, 16 * 60 + 45, SceneLoader.Gym, m => m.G.Fatigue = 0));
            GymManager gm = null;
            for (int i = 0; i < 300 && gm == null; i++) { gm = GymManager.Instance; yield return null; }
            Assert.NotNull(gm);
            var cc = gm.transform.TransformPoint(new Vector3(14.5f, 0.25f, 6.0f));
            w.StartCoroutine(gm.PlayBot("D11", 1f));
            int n = 0, outside = 0; float worst = 0f, lowest = 9f;
            for (int i = 0; i < 60 * 60 && (gm.Busy || i < 60); i++)
            {
                if (i > 90 && gm.Runner != null && gm.Runner.State == DrillRunner.Phase.Round && Camera.main != null)
                {
                    var c = Camera.main.transform.position; var hz = c - cc; hz.y = 0f;
                    n++; worst = Mathf.Max(worst, hz.magnitude); lowest = Mathf.Min(lowest, c.y - cc.y);
                    if (hz.magnitude > 2.8f) outside++;
                    if (n > 240) break;
                }
                yield return null;
            }
            Debug.Log($"[Q] Q33 스파링 카메라: 본 프레임 {n} · 케이지 밖 {outside} · 가운데서 최대 {worst:F2}m · 바닥 위 최저 {lowest:F2}m");
            Assert.Greater(n, 0, "스파링 라운드가 안 돌았음(시험 전제)");
            Assert.AreEqual(0, outside, "스파링 카메라가 케이지 밖(윗테 너머)");
            Assert.Greater(lowest, 1.9f, "스파링 카메라가 너무 낮음(두 사람이 서로 가림)");
        }

        // ───────────────────────── Q34 드릴을 못 시작할 때(22시 문 닫음) 관장 말만 — 지난 드릴 이름판·타이머가 다시 뜨지 않음
        [UnityTest, Timeout(900000)]
        public IEnumerator Q34_Refused_Drill_Shows_Only_Coach_Line()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            yield return Start(w, Mid(w.Def, 310, 16 * 60 + 45, SceneLoader.Gym, m => m.G.Fatigue = 0));
            GymManager gm = null;
            for (int i = 0; i < 300 && gm == null; i++) { gm = GymManager.Instance; yield return null; }
            Assert.NotNull(gm);
            yield return gm.PlayBot("D01", 1f);          // 지난 드릴 값(이름판·타이머)이 남게
            yield return Free(w);
            GameClock.SetDate(2026, 3, 10, 22 * 60 + 1);
            yield return Lab.Frames(5);
            bool ok = gm.StartDrill("D02");
            yield return Lab.Frames(10);
            var plate = gm.Hud.transform.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(r => r.name == "이름판");
            var timer = gm.Hud.transform.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(r => r.name == "타이머");
            Debug.Log($"[Q] Q34 22:01 드릴 시작 {ok} · 관장 말만 {gm.Hud.NoticeShown} · 이름판 {(plate != null && plate.gameObject.activeInHierarchy)} · 타이머 {(timer != null && timer.gameObject.activeInHierarchy)}");
            Assert.IsFalse(ok, "22시 넘어 드릴이 시작됨");
            Assert.IsTrue(gm.Hud.NoticeShown, "문 닫음 안내가 관장 말로 안 뜸");
            Assert.IsFalse(plate != null && plate.gameObject.activeInHierarchy, "못 시작했는데 지난 드릴 이름판이 다시 뜸");
            Assert.IsFalse(timer != null && timer.gameObject.activeInHierarchy, "못 시작했는데 지난 드릴 타이머가 다시 뜸");
        }

        // ───────────────────────── Q20(2차) 실제 계산대 알바 흐름 — 미니게임 내내 시우 가슴이 카메라에 보임(가림·몸 속 없음)
        [UnityTest, Timeout(900000)]
        public IEnumerator Q20b_Counter_Job_Camera_Sees_Siwoo()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            yield return Start(w, Mid(w.Def, 315, 12 * 60 + 40, "Conv", m => { m.G.Fatigue = 0; }));
            w.BotDelegate = false;
            var pm = PlaceMarker.All.First(m => m.Func == "job_counter");
            var me = M3Lab.Motor;
            me.Teleport(me.Position + new Vector3(-1.5f, 0f, 1.0f), 0f);     // 가게 안 다른 데서 걸어온 것처럼(얼굴 방향이 다르게)
            yield return Lab.Frames(20);
            var act = typeof(WorldRunner).GetMethod("Activity", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var a = w.Def.Activity("job.counter");
            Assert.NotNull(a);
            w.StartCoroutine((IEnumerator)act.Invoke(w, new object[] { a, "job_counter", pm.transform }));
            int seen = 0, bad = 0; string why = "";
            for (int i = 0; i < 60 * 120; i++)
            {
                if (w.Job != null && w.Job.Shown && i > 30 && Camera.main != null)
                {
                    var cam = Camera.main.transform.position;
                    var chest = me.Position + Vector3.up * 1.25f;
                    float d = Vector3.Distance(cam, chest);
                    var vp = Camera.main.WorldToViewportPoint(chest);
                    bool inView = vp.z > 0f && vp.x > 0.15f && vp.x < 0.85f && vp.y > 0.2f && vp.y < 0.95f;
                    bool hit = Physics.Linecast(cam, Vector3.Lerp(cam, chest, 0.85f), out var h, Layers.CameraBlock, QueryTriggerInteraction.Ignore);
                    var bl = me.GetComponentInChildren<BodyLean>(true) ?? Object.FindAnyObjectByType<BodyLean>();
                    bool hiddenNow = bl != null && bl.Hidden;
                    seen++;
                    if (d < 1.0f || !inView || hit || hiddenNow) { bad++; if (why.Length == 0) why = $"거리 {d:F2} · 화면 {vp:F2} · 막힘 {(hit ? h.collider.name : "-")} · 숨김 {hiddenNow}"; }
                    if (seen > 200) break;
                }
                else if (seen > 0 && (w.Job == null || !w.Job.Shown)) break;
                yield return null;
            }
            Debug.Log($"[Q] Q20b 계산대 알바 카메라: 본 프레임 {seen} · 나쁨 {bad} · {why} · 카메라 {Camera.main.transform.position:F2} · 시우 {me.Position:F2} · 얼굴 {me.Yaw:F0}");
            Assert.Greater(seen, 0, "계산대 미니게임이 안 떴음(시험 전제)");
            Assert.AreEqual(0, bad, "계산대 알바 카메라가 시우를 못 봄: " + why);
        }

        // ───────────────────────── Q35 드릴 결과 카드의 '닫기' 안내와 드릴 안내 글이 화면 아래에서 겹치지 않음
        [UnityTest, Timeout(900000)]
        public IEnumerator Q35_Result_Card_Hides_Drill_Hint()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            yield return Start(w, Mid(w.Def, 310, 16 * 60 + 45, SceneLoader.Gym, m => m.G.Fatigue = 0));
            GymManager gm = null;
            for (int i = 0; i < 300 && gm == null; i++) { gm = GymManager.Instance; yield return null; }
            Assert.NotNull(gm);
            Assert.IsTrue(gm.StartDrill("D01", 1f));
            int both = 0, cardFrames = 0; string seenHint = "";
            for (int i = 0; i < 60 * 300; i++)
            {
                bool card = gm.Card != null && gm.Card.Visible;
                if (card)
                {
                    cardFrames++;
                    if (gm.Hud.HintText.Length > 0) { both++; seenHint = gm.Hud.HintText; }
                    if (cardFrames > 60) break;
                }
                yield return null;
            }
            Debug.Log($"[Q] Q35 결과 카드 {cardFrames}프레임 · 드릴 안내 같이 {both} 「{seenHint}」");
            Assert.Greater(cardFrames, 0, "결과 카드가 안 떴음(시험 전제)");
            Assert.AreEqual(0, both, "결과 카드 위로 드릴 안내 글이 남아 '닫기' 안내와 겹침");
        }

        // ───────────────────────── Q12(2차) 저녁(관원 8 줄 서서 섀도복싱) 미트 드릴 — 카메라 옆·앞 관원 몸·팔이 화면 가장자리를 덮지 않음
        [UnityTest, Timeout(600000)]
        public IEnumerator Q12b_Evening_Mitts_No_Member_At_Camera()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            yield return Start(w, Mid(w.Def, 312, 19 * 60 + 30, SceneLoader.Gym, m => m.G.Fatigue = 0));
            GymManager gm = null;
            for (int i = 0; i < 300 && gm == null; i++) { gm = GymManager.Instance; yield return null; }
            Assert.AreEqual(GymPart.Evening, gm.Part, "저녁 관원 배치(시험 전제)");
            Assert.IsTrue(gm.StartDrill("D04", 0.9f));
            yield return Lab.Frames(300);
            var c = Camera.main.transform; var f = c.forward; f.y = 0f;
            var near = new List<string>();
            foreach (var n in gm.Members)
            {
                if (n == null || !n.gameObject.activeInHierarchy) continue;
                var d = n.transform.position - c.position; d.y = 0f;
                if (d.magnitude < 3.0f && Vector3.Angle(f, d) < 70f) near.Add($"{n.name} {d.magnitude:F1}m {Vector3.Angle(f, d):F0}°");
            }
            Debug.Log($"[Q] Q12b 저녁 D04 카메라 옆 관원 [{string.Join(", ", near)}] · 비켜 준 {gm.SightHidden}");
            Assert.IsEmpty(near, "미트 드릴 카메라 옆·앞에 관원(팔이 화면 가장자리를 덮음)");
        }

        // ───────────────────────── Q36 주차장 시비(길거리 싸움) 중엔 세계 HUD 를 숨김(전투 HUD '반시우' 체력과 왼쪽 위에서 겹쳤음)
        [UnityTest, Timeout(600000)]
        public IEnumerator Q36_World_Hud_Hidden_During_Street_Fight()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            var pk = w.Def.Balance.Point("parking");
            yield return Start(w, Mid(w.Def, 311, 17 * 60, SceneLoader.Zone1, m => m.Unlock("street_repeat"), pk.Pos + new Vector3(0f, 0.5f, 25f)));
            yield return Lab.Frames(30);
            Assert.IsTrue(w.HudShown, "시비 전엔 세계 HUD 가 보임(시험 전제)");
            var enc = Object.FindAnyObjectByType<Encounter>();
            Assert.NotNull(enc, "주차장 시비 무대");
            enc.Trigger();
            var pc = Object.FindAnyObjectByType<PlayerCombat>();
            int fight = 0, both = 0;
            for (int i = 0; i < 60 * 30; i++)
            {
                if (pc != null && pc.Active) { fight++; if (w.HudShown) both++; if (fight > 120) break; }
                yield return null;
            }
            Debug.Log($"[Q] Q36 시비 중 프레임 {fight} · 세계 HUD 같이 {both} · 단계 {enc.State}");
            Assert.Greater(fight, 0, "시비 전투가 안 켜짐(시험 전제)");
            Assert.AreEqual(0, both, "싸우는 동안 세계 HUD 가 전투 HUD 와 같이 뜸");
        }

        // ───────────────────────── Q16 드릴 뒤 카드는 결과 카드를 닫은 뒤에
        [UnityTest, Timeout(900000)]
        public IEnumerator Q16_After_Drill_Card_Waits_For_Result()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            yield return Start(w, Mid(w.Def, 310, 16 * 60 + 45, SceneLoader.Gym, m => { m.G.Fatigue = 0; var n08 = m.Def.Card("N08"); if (n08 != null) m.S.Card0("N08", true).State = 0; }));
            GymManager gm = null;
            for (int i = 0; i < 300 && gm == null; i++) { gm = GymManager.Instance; yield return null; }
            Assert.NotNull(gm);
            bool overlap = false, sawCard = false;
            var run = w.StartCoroutine(gm.PlayBot("D01", 1f));
            for (int i = 0; i < 60 * 400; i++)
            {
                bool rc = gm.Card != null && gm.Card.Visible;
                if (rc && w.Story.Board.Shown) overlap = true;
                if (w.Story.Board.Shown) sawCard = true;
                if (!gm.Busy && !rc && i > 120 && w.Free) break;
                yield return null;
            }
            Debug.Log($"[Q] Q16 드릴 뒤 카드: 결과 카드와 겹침 {overlap} · 카드 봄 {sawCard} · N08 {w.Model.CardDone("N08")}");
            Assert.IsFalse(overlap, "드릴 결과 카드 위에 '첫 S' 카드가 겹쳐 뜸");
        }
    }
}
