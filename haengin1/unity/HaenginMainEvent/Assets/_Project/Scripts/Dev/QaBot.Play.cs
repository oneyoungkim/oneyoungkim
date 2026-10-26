// 행인1의 메인이벤트 — QA 봇 A: 새 게임을 사람 손(가상 키보드)으로 — 장 카드·안내·1화 체인·HUD·휴대폰·일시정지·첫 저녁 알바·문 6곳·집·잠·새벽 배달·학교 → 남은 데모는 봇(카드마다 사진)
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Haengin
{
    public sealed partial class QaBot
    {
        SaveData labBase, labLate;

        IEnumerator PartA()
        {
            yield return Section("A1 새 게임 → 1화", A_NewGame, 1000f);
            yield return Section("A2 첫 자유 — HUD·휴대폰·일시정지", A_FirstFree, 240f);
            yield return Section("A3 첫 저녁 알바(처음·막 누름)", A_Evening, 420f);
            yield return Section("A4 문 들어가고 나오기", A_Doors, 600f);
            yield return Section("A5 걷기·달리기 녹화(사람 키)", A_Walk, 160f);
            yield return Section("A6 집 — 가계부·하늘이·공책·잠", A_Night, 300f);
            yield return Section("A7 새벽 배달·학교", A_Morning, 600f);
            yield return Section("A8 데모 끝까지(봇 · 카드마다 사진)", A_Bot, 2700f);
        }

        void RecStart(string name, float secs)
        {
            if (recLeft > 0) return;
            recDir = Path.Combine(Out, "rec", Safe(name));
            Directory.CreateDirectory(recDir);
            Time.captureDeltaTime = 1f / 30f;
            recFrame = 0; recLeft = Mathf.RoundToInt(secs * 30f); recAsync = true;
            L($"녹화 시작 {name} {secs:0}초(비동기)");
        }

        IEnumerator A_NewGame()
        {
            var title = FindAnyObjectByType<TitleMenu>();
            if (title == null) { Issue("title.none", "진행 불가", "타이틀 없음"); yield break; }
            if (title.Label(title.Selected) != "새 게임") Issue("title.sel", "사소", $"타이틀 처음 고른 칸이 '새 게임'이 아님: {title.Label(title.Selected)}");
            yield return Tap(Key.Enter);
            for (int i = 0; i < 3600 && (W == null || !W.Active); i++) yield return null;
            if (W == null || !W.Active) { Issue("newgame.none", "진행 불가", "새 게임 → 세계가 시작 안 됨"); yield break; }
            W.BotSkill = 0f;
            Hook();
            Story().SceneStarted += s =>
            {
                if (s.Id == "1-6") RecStart("앉기_교실_1-6", 8f);
                if (s.Id == "1-0" || s.Id == "1-1" || s.Id == "1-5") RecStart("이야기_" + s.Id, 5f);
            };
            LineShotEvery = 3;
            yield return Advance(950f, () => M != null && M.CardDone("C1-01") && W.Free && !Ld.Busy && !Story().InScene);
            yield return Frames(30);
            yield return Shot("1화_끝_첫자유", M?.Status);
        }

        IEnumerator A_FirstFree()
        {
            if (!W.Free) yield return Advance(120f);
            yield return Shot("HUD");
            // 휴대폰 9칸(Tab → → … → Esc)
            yield return Tap(Key.Tab);
            yield return Frames(15);
            if (!W.Phone.Shown) Issue("phone.tab", "눈에 띔", "Tab 으로 휴대폰이 안 열림");
            else
            {
                for (int t = 0; t < PhoneUi.Tabs.Length; t++)
                {
                    yield return Frames(8);
                    yield return Shot($"휴대폰_{W.Phone.Tab}_{PhoneUi.Tabs[W.Phone.Tab]}", Snip(W.Phone.Body, 300));
                    yield return Tap(Key.RightArrow);
                }
                if (W.Phone.Tab != 0) Issue("phone.wrap", "사소", $"→ 아홉 번 뒤 첫 칸으로 안 돌아옴(지금 {W.Phone.Tab})");
                yield return Tap(Key.Escape);
                yield return Frames(15);
                if (W.Phone.Shown) Issue("phone.esc", "눈에 띔", "Esc 로 휴대폰이 안 닫힘");
                if (GameState.Paused) { Issue("phone.esc.pause", "눈에 띔", "Esc 로 휴대폰을 닫았더니 일시정지 메뉴까지 열림(한 번 누름에 두 가지)"); yield return Shot("휴대폰Esc_일시정지도"); yield return Tap(Key.Escape); yield return Frames(10); }
            }
            // 일시정지
            yield return Tap(Key.Escape);
            yield return Frames(15);
            var gu = GameUi.Instance;
            if (gu == null || !gu.MenuVisible) Issue("pause.esc", "눈에 띔", "Esc 로 일시정지 메뉴가 안 열림");
            else
            {
                yield return Shot("일시정지", string.Join(" / ", Enumerable.Range(0, gu.ItemCount).Select(gu.Label)));
                // 메뉴의 '휴대폰(Tab)'
                int pi = Enumerable.Range(0, gu.ItemCount).FirstOrDefault(i => gu.Label(i).Contains("휴대폰"));
                for (int k = 0; k < 8 && gu.Selected != pi; k++) { yield return Tap(Key.DownArrow); yield return Frames(3); }
                yield return Tap(Key.Enter);
                yield return Frames(15);
                if (!W.Phone.Shown) Issue("pause.phone", "눈에 띔", "일시정지 '휴대폰'으로 휴대폰이 안 열림");
                else { yield return Shot("일시정지_휴대폰"); yield return Tap(Key.Escape); yield return Frames(10); }
                if (GameState.Paused) { yield return Tap(Key.Escape); yield return Frames(10); }
            }
            if (GameState.Paused) Issue("pause.stuck", "진행 불가?", "일시정지가 안 풀림");
            yield return Frames(10);
        }

        IEnumerator A_Evening()
        {
            if (!W.Free) yield return Advance(120f);
            var d = W.FindDoor("Gukbap");
            yield return UseDoor(d, "국밥집");
            yield return Advance(240f);
            if (Ld.Place != "Gukbap") { Issue("gukbap.enter", "진행 불가?", "국밥집에 못 들어감"); yield break; }
            yield return Shot("국밥집_저녁_들어옴");
            RecStart("앉기_국밥집손님", 5f);
            yield return Secs(5.2f);
            JobPolicy = "mash";
            yield return Shot("서빙_표시", W.MarkerPrompt("job_serving"));
            yield return UseMarker("job_serving");
            yield return Advance(400f);
            yield return Shot("서빙_끝", M?.Status);
        }

        IEnumerator A_Doors()
        {
            if (!W.Free) yield return Advance(120f);
            if (Ld.Place != SceneLoader.Zone1)
            {
                var exit = Ld.Stage != null ? Ld.Stage.GetComponentsInChildren<StageDoor>(true).FirstOrDefault(x => x.Target == SceneLoader.Zone1) : null;
                yield return UseDoor(exit, Ld.Place + " 나가기");
                yield return Advance(120f);
            }
            var doors = FindObjectsByType<StageDoor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(x => x.Target != SceneLoader.Zone1 && x.gameObject.scene == W.gameObject.scene).OrderBy(x => x.Target).ToList();
            L($"Zone1 문 {doors.Count}: {string.Join(", ", doors.Select(x => x.Target))}");
            foreach (var d in doors)
            {
                if (!W.Free) yield return Advance(200f);
                if (Ld.Place != SceneLoader.Zone1) { var ex = Ld.Stage?.GetComponentsInChildren<StageDoor>(true).FirstOrDefault(x => x.Target == SceneLoader.Zone1); yield return UseDoor(ex, "나가기"); yield return Advance(120f); }
                string tgt = d.Target;
                yield return UseDoor(d, tgt);
                yield return Frames(10);
                if (Ld.Place != tgt)
                {
                    yield return Shot($"문_{tgt}_안열림");
                    L($"문 {tgt}: 안 열림(문지기) — {GameClock.TimeText}");
                    yield return Advance(200f);
                    continue;
                }
                yield return Advance(240f);
                if (Ld.Place != tgt) continue;
                yield return Shot($"실내_{tgt}_들어옴");
                CheckSpawn($"{tgt} 들어옴");
                // 사람처럼 둘러보기(W·D·S·A)
                yield return Hold(1.0f, Key.W);
                yield return Hold(0.8f, Key.D);
                yield return Shot($"실내_{tgt}_둘러봄");
                yield return Hold(1.0f, Key.S);
                yield return Hold(0.8f, Key.A);
                if (!W.Free) yield return Advance(200f);
                var exit = Ld.Stage != null ? Ld.Stage.GetComponentsInChildren<StageDoor>(true).FirstOrDefault(x => x.Target == SceneLoader.Zone1) : null;
                if (exit == null) { Issue($"door.noexit.{tgt}", "진행 불가", $"{tgt} 안에 나가는 문 없음"); continue; }
                yield return UseDoor(exit, tgt + " 나가기");
                yield return Advance(200f);
                if (Ld.Place != SceneLoader.Zone1) { Issue($"door.out.{tgt}", "진행 불가", $"{tgt} 에서 못 나옴"); yield return Shot($"못나옴_{tgt}"); continue; }
                yield return Shot($"나옴_{tgt}");
                float back = Vector3.Distance(Mo.Position, d.ReturnPos);
                if (back > 3f) Issue($"door.return.{tgt}", "눈에 띔", $"{tgt} 에서 나왔는데 문 앞이 아님({back:F1}m)");
                CheckSpawn($"{tgt} 나옴");
            }
        }

        /// 문으로 들어오고 나온 자리: 땅 위·벽 밖·카메라 벽 밖
        void CheckSpawn(string what)
        {
            var mo = Mo; if (mo == null) return;
            var p = mo.Position;
            if (!Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, out var h, 2f, Layers.Solid, QueryTriggerInteraction.Ignore)) Issue("spawn.noground." + what, "눈에 띔", $"{what}: 발밑 땅 없음 {p:F1}");
            else if (Mathf.Abs(p.y - h.point.y) > 0.25f) Issue("spawn.gap." + what, "사소", $"{what}: 땅과 {p.y - h.point.y:F2}m 차이");
            if (Physics.CheckCapsule(p + Vector3.up * 0.45f, p + Vector3.up * 1.35f, 0.15f, Layers.Mask(Layers.Wall, Layers.Default), QueryTriggerInteraction.Ignore)) Issue("spawn.wall." + what, "눈에 띔", $"{what}: 몸이 벽·물체에 겹침 {p:F1}");
            var cam = Camera.main;
            if (cam != null && Physics.CheckSphere(cam.transform.position, 0.08f, Layers.CameraBlock, QueryTriggerInteraction.Ignore)) Issue("spawn.cam." + what, "눈에 띔", $"{what}: 카메라가 벽 속 {cam.transform.position:F1}");
        }

        IEnumerator A_Walk()
        {
            if (!W.Free) yield return Advance(120f);
            if (Ld.Place != SceneLoader.Zone1) { var ex = Ld.Stage?.GetComponentsInChildren<StageDoor>(true).FirstOrDefault(x => x.Target == SceneLoader.Zone1); yield return UseDoor(ex, "나가기"); yield return Advance(120f); }
            // 후문 상가거리 한복판(트인 길)
            var p = new Vector3(-186f, 29.3f, 100.5f);
            if (Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out var h, 8f, Layers.Solid, QueryTriggerInteraction.Ignore)) p.y = h.point.y;
            Mo.Teleport(p + Vector3.up * 0.03f, 100f);
            Ld.Cam?.SnapBehind();
            yield return Frames(30);
            PinOn();
            yield return RecWhile("걷기_사람_W", 6f, Hold(5.5f, Key.W));
            yield return RecWhile("서기_대기", 3f, Secs(2.8f));
            yield return RecWhile("달리기_사람_Shift_W", 5f, Hold(4.5f, Key.LeftShift, Key.W));
            yield return RecWhile("대각선_WA", 4f, Hold(3.5f, Key.W, Key.A));
            yield return RecWhile("돌아서기_S", 3f, Hold(2.5f, Key.S));
            yield return Shot("걷기_끝");
        }

        IEnumerator A_Night()
        {
            if (!W.Free) yield return Advance(120f);
            if (Ld.Place != "Home")
            {
                if (Ld.Place != SceneLoader.Zone1) { var ex = Ld.Stage?.GetComponentsInChildren<StageDoor>(true).FirstOrDefault(x => x.Target == SceneLoader.Zone1); yield return UseDoor(ex, "나가기"); yield return Advance(120f); }
                yield return UseDoor(W.FindDoor("Home"), "집");
                yield return Advance(200f);
            }
            if (Ld.Place != "Home") { Issue("home.enter", "진행 불가?", "집에 못 들어감"); yield break; }
            yield return Shot("집_밤");
            var funcs = PlaceMarker.All.Where(m => m != null && m.Place == "Home").Select(m => m.Func).Distinct().ToList();
            L("집 표시 자리: " + string.Join(", ", funcs));
            foreach (var f in funcs.Where(f => f != "sleep"))
            {
                yield return Shot("표시_" + f, W.MarkerPrompt(f));
                yield return UseMarker(f);
                yield return Frames(20);
                yield return Shot("표시_" + f + "_누름");
                yield return Advance(60f);
            }
            yield return Shot("잠_표시", W.MarkerPrompt("sleep"));
            ChoicePolicy = null;
            yield return UseMarker("sleep");
            yield return Advance(300f);
            yield return Frames(30);
            yield return Shot("기상", M?.Status);
        }

        IEnumerator A_Morning()
        {
            if (!W.Free) yield return Advance(120f);
            // 새벽 배달: 국밥집 배달통 → 직접 → 3초 사람이 페달(W) → 코스 봇에게
            if (Ld.Place != SceneLoader.Zone1) { var ex = Ld.Stage?.GetComponentsInChildren<StageDoor>(true).FirstOrDefault(x => x.Target == SceneLoader.Zone1); yield return UseDoor(ex, "집 나가기"); yield return Advance(120f); }
            yield return UseDoor(W.FindDoor("Gukbap"), "국밥집(새벽)");
            yield return Advance(200f);
            yield return Shot("배달_표시", W.MarkerPrompt("job_delivery"));
            yield return UseMarker("job_delivery");
            yield return WaitFor(() => ChoiceShown, 4f);
            if (ChoiceShown) { yield return Shot("배달_묻기"); yield return Choose(0); }
            yield return WaitFor(() => BikeCourse.Current != null && BikeCourse.Current.Riding, 30f);
            var bc = BikeCourse.Current;
            if (bc == null || !bc.Riding) { Issue("delivery.start", "진행 불가?", "새벽 배달을 골랐는데 자전거가 안 나옴"); yield return Advance(200f); yield break; }
            yield return Frames(30);
            yield return Shot("배달_출발");
            yield return RecWhile("자전거_사람_페달W", 5f, Hold(4.5f, Key.W));
            yield return Shot("배달_사람3초");
            bc.Bot = true;
            L("배달: 사람 4.5초 페달 → 코스 봇");
            RecStart("자전거_코스봇", 20f);
            yield return WaitFor(() => !bc.Riding, 240f);
            if (bc.Riding) Issue("delivery.end", "진행 불가?", "배달이 240초 안에 안 끝남");
            yield return Advance(240f);
            yield return Shot("배달_끝", M?.Status);
            // 학교: 혜성고 방면 표지 → 교실 자리
            if (!W.Free) yield return Advance(120f);
            var sd = W.FindDoor("School");
            if (sd != null && M.SchoolDay(M.Today))
            {
                if (Ld.Place != SceneLoader.Zone1) { var ex = Ld.Stage?.GetComponentsInChildren<StageDoor>(true).FirstOrDefault(x => x.Target == SceneLoader.Zone1); yield return UseDoor(ex, "나가기"); yield return Advance(120f); }
                yield return UseDoor(sd, "학교");
                yield return Advance(200f);
                if (Ld.Place == "School")
                {
                    var seat = PlaceMarker.All.FirstOrDefault(m => m != null && m.Place == "School" && (m.Func == "seat" || m.Func.Contains("seat")));
                    yield return Shot("교실_아침", seat != null ? W.MarkerPrompt(seat.Func) : "자리 없음");
                    if (seat != null) { yield return UseMarker(seat.Func); yield return Advance(400f); }
                    yield return Shot("학교_끝", M?.Status);
                }
            }
        }

        IEnumerator A_Bot()
        {
            if (!W.Free) yield return Advance(200f);
            // 사람 손 → 봇(카드·안내·이야기는 '읽기' 속도로 넘김) — 남은 데모 카드를 모두 지나며 사진
            botMode = true;
            W.BotSkill = 0.9f; W.BotRead = 0.45f; W.BotRide = false; W.BotDelegate = true;
            W.BotChoice = c => c.Id == "N02" ? 1 : 0;
            Story().Bot = StoryRunner.BotMode.Read;
            if (Story().Talk != null) Story().Talk.Bot = l => 0;
            humanSkill = 0.9f;
            watchBot = true;
            baseDelta = 1f / 30f; Time.captureDeltaTime = baseDelta;     // 봇 구간은 두 배 빠르게(드릴 라운드가 실제 시간이라 느림)
            Story().SceneStarted += s => { if (watchBot) ShotNow("장면_" + s.Id, s.Title); };
            var bot = WorldBot.Attach(W, true);
            bot.MaxSeconds = 2500f;
            yield return bot.Run();
            watchBot = false;
            baseDelta = 1f / 60f; Time.captureDeltaTime = baseDelta;
            foreach (var l in bot.Log) L("봇: " + l);
            if (!bot.Done) Issue("bot.fail", "진행 불가?", "봇이 데모 끝에 못 감 — " + bot.Fail);
            yield return Shot("데모끝_뒤", M?.Status);
            // 데모 카드 중 안 본 것
            var unseen = W.Def.Cards.Where(c => c.Demo && !M.CardDone(c.Id)).Select(c => c.Id).ToArray();
            L($"데모 카드 {W.Def.Cards.Count(c => c.Demo)}장 중 안 본 것 {unseen.Length}: {string.Join(" ", unseen)}");
            L("자취: " + string.Join(" | ", W.Trace.Take(400)));
            if (timeWordHits.Count > 0) File.WriteAllLines(Path.Combine(Out, "time_words.tsv"), timeWordHits);
            Destroy(bot);
            botMode = false;
            humanSkill = 0f;
        }

        void SnapLab()
        {
            if (W == null || !W.Active || M == null || !W.Free) return;
            if (labBase == null && M.Today >= 310 && M.S.GymMember > 0 && M.Unlocked("job.stock"))
            {
                labBase = W.Snapshot("");
                File.WriteAllText(Path.Combine(Out, "lab_base.json"), labBase.ToJson());
                L($"실험실 바탕 저장: {M.Status}");
            }
            if (labLate == null && M.CardDone("C1-23") && !M.CardDone("C1-24"))
            {
                labLate = W.Snapshot("");
                File.WriteAllText(Path.Combine(Out, "lab_late.json"), labLate.ToJson());
                L($"실험실 늦은 바탕(C1-24 전): {M.Status}");
            }
        }
    }
}
