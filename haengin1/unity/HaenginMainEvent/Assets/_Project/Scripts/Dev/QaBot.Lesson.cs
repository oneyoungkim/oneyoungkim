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
            if (labBase == null)
            {
                var f = Path.Combine(Out, "lab_base.json");
                string alt = ArgVal("-qabase", "");
                if (alt.Length > 0 && File.Exists(alt)) f = alt;
                if (File.Exists(f)) labBase = SaveStore.ReadFile(f, out _);
            }
            if (W == null || !W.Active)
            {
                var title = FindAnyObjectByType<TitleMenu>();
                if (title != null) title.Choose("새 게임");
                for (int i = 0; i < 3600 && (W == null || !W.Active); i++) yield return null;
                W.BotSkill = 0.9f; W.BotRead = 0.05f;
                for (int i = 0; i < 60 * 600 && !W.Free; i++) yield return null;
            }
            if (labBase == null) { Issue("lab.nobase", "QA", "실험실 바탕 저장이 없음 — L 건너뜀"); yield break; }
            baseDay = labBase.Month * 100 + labBase.Day;
            yield return Section("L1 레슨 이윤준", () => L_Lesson("yunjun", "화목", 21 * 60 + 15, true), 240f);
            yield return Section("L2 레슨 고준", () => L_Lesson("gojun", "토", 16 * 60, false), 240f);
            yield return Section("L3 레슨 김승연", () => L_Lesson("seungyeon", "토", 17 * 60 + 30, false), 240f);
            yield return Section("L4 레슨 손진수", () => L_Lesson("jinsoo_son", "일", 11 * 60, false), 240f);
            yield return Section("L5 레슨 서진수", () => L_Lesson("jinsoo_seo", "일", 19 * 60 + 30, false), 240f);
            yield return Section("L6 레슨 백승민", () => L_Lesson("seungmin_baek", "토", 14 * 60, false), 240f);
            yield return Section("L7 N13", L_N13, 240f);
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
