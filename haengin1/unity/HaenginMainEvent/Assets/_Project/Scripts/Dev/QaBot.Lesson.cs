// 행인1의 메인이벤트 — QA 봇 L(2026-10-31 레슨 카드 카메라): -qapart l [-qabase <바탕 저장>]
//   코치 6명을 실제 레슨 흐름(예약 → 그 시각 → 카드 → 드릴 → 끝 카드)으로 돌며 실제 화면을 찍음(ScreenCapture — 오버레이 캔버스가 먹선 위에 그대로).
//   시험의 Shots.Grab 은 오버레이를 카메라 캔버스로 바꿔 찍으므로 먹선이 판 위로 지나감(촬영 도구 탓) — 이 부분이 진짜 화면 기준
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Haengin
{
    public sealed partial class QaBot
    {
        IEnumerator PartL()
        {
            // G7(2026-11-05): 시작을 B·C·U 와 같은 LabReady 로(옛 대기 — 새 게임 1장 카드에서 약 335초 멈춤)
            yield return LabReady();
            if (labBase == null) { Issue("lab.nobase", "QA", "실험실 바탕 저장이 없음 — L 건너뜀"); yield break; }
            baseDay = labBase.Month * 100 + labBase.Day;
            yield return Section("L1 레슨 이윤준", () => L_Lesson("yunjun", "화목", 21 * 60 + 15, true), 240f);
            yield return Section("L2 레슨 고준", () => L_Lesson("gojun", "토", 16 * 60, false), 240f);
            yield return Section("L3 레슨 김승연", () => L_Lesson("seungyeon", "토", 17 * 60 + 30, false), 240f);
            yield return Section("L4 레슨 손진수", () => L_Lesson("jinsoo_son", "일", 11 * 60, false), 240f);
            yield return Section("L5 레슨 서진수", () => L_Lesson("jinsoo_seo", "일", 19 * 60 + 30, false), 240f);
            yield return Section("L6 레슨 백승민", () => L_Lesson("seungmin_baek", "토", 14 * 60, false), 240f);
            yield return Section("L7 N13", L_N13, 240f);
            yield return Section("L8 N13 뒤 서진수 레슨(같은 방문)", L_N13Lesson, 360f);
        }

        /// G7(2026-11-05): 5/3(일) 18:40 체육관 — N13 「다섯 사람」 장면이 끝난 뒤 같은 방문에서 19:30 서진수 첫 레슨. 예전엔 장면이 세운 코치 다섯이 반원 그대로 남고
        ///   시우(장면이 매트 가장자리 소켓에 세움)와 레슨 코치(같은 소켓)가 겹쳐 레슨 카드 카메라를 시우 뒤통수가 덮었음(t3 사진 193 — A8 봇 경로에서만 우연히 나왔음)
        IEnumerator L_N13Lesson()
        {
            yield return Load(503, 18 * 60 + 40, SceneLoader.Gym, m => { Unlocks(m); m.Unlock("lesson.seungmin"); m.L.SetWallet(900000); m.G.Fatigue = 0; new EventDirector(m).Done(W.Def.Card("N12"), 1); });
            var gm = GymManager.Instance;
            if (gm == null) { Issue("gym.none.l8", "진행 불가?", "체육관이 안 열림"); yield break; }
            string why = W.BookNext("jinsoo_seo");
            if (why != "") { Issue("lesson.book.l8", "눈에 띔", $"서진수 예약 안 됨: {why}"); yield break; }
            W.BotSkill = 0.9f; W.BotRead = 3f;
            W.BotLessonDrill = c => c.Drills.Contains("*") || c.Drills.Contains("D04") ? "D04" : c.Drills[0];
            Story().Bot = StoryRunner.BotMode.Read;
            bool done = false;
            IEnumerator Wrap() { yield return W.RunCardBusy(W.Def.Card("N13"), true); done = true; }
            W.StartCoroutine(Wrap());
            yield return WaitFor(() => done, 200f);
            Story().Bot = StoryRunner.BotMode.None;
            yield return Frames(30);
            var mat = gm.Sock("Sock_NPC_Coach_MatEdge");
            string GuestsOn() => string.Join(",", gm.Guests.Where(g => g != null && g.gameObject.activeSelf).Select(g => g.Id));
            int members = gm.Members.Count(m => m != null && m.gameObject.activeSelf);
            L($"N13 뒤: 켜진 출장 코치 [{GuestsOn()}] · 관원 {members} · 배역 남음 {Story().CastHeld} · 시우–매트 가장자리 {HitResolver.Flat(Mo.Position - mat.position).magnitude:F2}m");
            if (GuestsOn() != "") Issue("g7.guests.after", "눈에 띔", $"N13 뒤 출장 코치가 남음 [{GuestsOn()}]");
            if (Story().CastHeld > 0) Issue("g7.cast.after", "눈에 띔", $"N13 뒤 장면 배역 {Story().CastHeld}개가 안 되돌아감");
            yield return Shot("N13뒤_자유", $"출장 코치 [{GuestsOn()}] · 관원 {members}");
            GameClock.AdvanceTo(19 * 60 + 25);
            yield return WaitFor(() => W.Busy == "레슨", 40f);
            var board = Story().Board;
            yield return WaitFor(() => board.Shown && W.LessonCamera != null, 30f);
            yield return Frames(40);     // 블렌드 뒤
            if (board.Shown)
            {
                var seo = gm.Guest("jinsoo_seo");
                var cam = Camera.main;
                var v = cam.WorldToViewportPoint(seo.Anim.GetBoneTransform(HumanBodyBones.Head).position);
                float gap = HitResolver.Flat(Mo.Position - seo.transform.position).magnitude;
                L($"레슨 카드 {board.Title}: 켜진 출장 코치 [{GuestsOn()}] · 서진수 머리 화면 {v.x:F2},{v.y:F2} · 시우–서진수 {gap:F2}m · 돌림 {(W.LessonCamera != null ? W.LessonCamera.SpinDeg : 0f):F0}°");
                if (GuestsOn() != "jinsoo_seo") Issue("g7.guests.lesson", "눈에 띔", $"5/3 레슨 카드에 출장 코치 [{GuestsOn()}](서진수 한 명이어야)");
                if (gap < 1.2f) Issue("g7.overlap", "눈에 띔", $"5/3 레슨 카드: 시우와 서진수가 겹침 {gap:F2}m");
                yield return Shot("N13뒤_카드_jinsoo_seo", board.Title);
            }
            else Issue("g7.nocard", "눈에 띔", $"N13 뒤 서진수 레슨 카드가 안 뜸(바쁨 '{W.Busy}' · {GameClock.TimeText})");
            yield return WaitFor(() => gm.Runner.State == DrillRunner.Phase.Round, 40f);
            yield return Frames(30);
            gm.Runner.Abort();
            yield return Frames(5);
            for (int i = 0; i < 600 && gm.Busy && !board.Shown; i++) { gm.CloseCard(); yield return null; }
            yield return WaitFor(() => board.Shown || W.Busy != "레슨", 20f);
            if (board.Shown) { yield return Frames(40); yield return Shot("N13뒤_끝카드_jinsoo_seo", board.Title); }
            yield return Advance(60f);
            W.BotSkill = 0f; W.BotLessonDrill = null;
        }

        IEnumerator L_Lesson(string id, string days, int at, bool rec)
        {
            yield return Load(NextDay(days), at - 40, SceneLoader.Gym, m => { Unlocks(m); m.Unlock("lesson.seungmin"); m.L.SetWallet(900000); m.G.Fatigue = 0; });
            var coach = M.Sh.Coach(id);
            string why = W.BookNext(id);
            if (why != "") { Issue("lesson.book." + id, "눈에 띔", $"{coach.Name} 예약 안 됨: {why}"); yield break; }
            var gm = GymManager.Instance;
            W.BotSkill = 0.9f; W.BotRead = 3f;
            W.BotLessonDrill = c => c.Drills.Contains("*") || c.Drills.Contains("D04") ? "D04" : c.Drills[0];
            var mat = gm.Sock("Sock_NPC_Coach_MatEdge");
            var sp = mat.position + mat.forward * 2.4f; var to = mat.position - sp; to.y = 0f;
            Mo.Teleport(sp, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg);
            Ld.Cam?.SnapBehind();
            yield return Frames(20);
            GameClock.AdvanceTo(at - 5);
            yield return WaitFor(() => W.Busy == "레슨", 40f);
            var board = Story().Board;
            yield return WaitFor(() => board.Shown && W.LessonCamera != null, 20f);
            yield return Frames(40);     // 0.4초 블렌드 뒤
            if (board.Shown)
            {
                var cam = Camera.main;
                var npc = id == "yunjun" ? gm.Yunjun : id == "gojun" ? gm.Gojun : gm.Guest(id);
                var v = cam.WorldToViewportPoint(npc.Anim.GetBoneTransform(HumanBodyBones.Head).position);
                string canvases = string.Join(",", FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(x => x.isRootCanvas && x.isActiveAndEnabled).Select(x => x.renderMode.ToString()).Distinct());
                L($"{coach.Name} 카드: 제목 {board.Title} · 머리 화면 {v.x:F2},{v.y:F2} · 돌림 {(W.LessonCamera != null ? W.LessonCamera.SpinDeg : 0f):F0}° · 캔버스 {canvases}");
                yield return Shot("카드_" + id, board.Title);
            }
            if (rec) yield return Rec("레슨카드_드릴_" + id, 8f);       // 카드 → 드릴 넘어가는 곳
            yield return WaitFor(() => gm.Runner.State == DrillRunner.Phase.Round, 40f);
            yield return WaitFor(() => gm.Hud.CueShown, 12f);
            yield return Frames(4);
            L($"{coach.Name} 드릴: 말칸 이름 {gm.Hud.CueName}");
            yield return Shot("드릴_" + id, gm.Hud.CueName);
            gm.Runner.Abort();
            yield return Frames(5);      // 봇이 결과 상태를 본 뒤 닫음(바로 닫으면 봇이 결과를 기다리며 멈춤)
            for (int i = 0; i < 600 && gm.Busy && !board.Shown; i++) { gm.CloseCard(); yield return null; }
            yield return WaitFor(() => board.Shown || W.Busy != "레슨", 20f);
            if (board.Shown) { yield return Frames(40); yield return Shot("끝카드_" + id, board.Title); }
            yield return Advance(60f);
            W.BotSkill = 0f; W.BotLessonDrill = null;
        }

        IEnumerator L_N13()
        {
            yield return Load(502, 18 * 60 + 40, SceneLoader.Gym, m => { Unlocks(m); m.G.Fatigue = 0; new EventDirector(m).Done(W.Def.Card("N12"), 1); });
            W.BotSkill = 0.9f; W.BotRead = 2.2f;
            Story().Bot = StoryRunner.BotMode.Read;
            bool done = false;
            IEnumerator Wrap() { yield return W.RunCardBusy(W.Def.Card("N13"), true); done = true; }
            W.StartCoroutine(Wrap());
            yield return WaitFor(() => done || Story().Talk != null && Story().Talk.Current != null, 40f);
            yield return Rec("N13", 9f);
            yield return WaitFor(() => done, 120f);
            W.BotSkill = 0f;
        }
    }
}
