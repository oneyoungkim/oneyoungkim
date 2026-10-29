// 행인1의 메인이벤트 — QA 봇 C(QA 2차, 2026-10-26 — docs/15 7장): -qapart c
//   C00 A 항목 고친 뒤 사진(시간대 이름 · 낮잠 · 알바 중 Esc · 밤 체육관 시우 · 드릴 자리 글 · 밤 거리 웅덩이 · 낮 나무·벽돌)
//   C01~ 핵심 카드 실제 장면 녹화: 실험실 바탕 저장을 카드 날짜·장소로 고쳐 불러와 그 카드를 읽기 속도(대사 = 글자 수 ÷ 8 + 0.8초, 카드 2.6초)로 끝까지 —
//        rec/장면_<카드>/f_*.jpg(1280×720 · 30fps — 묶기는 밖에서 ffmpeg) + 장면마다 사진 몇 장(shots/) + 길이(qa_log '장면 길이')
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Haengin
{
    public sealed partial class QaBot
    {
        /// 녹화 화소(0 = 화면 그대로) — 장면 녹화는 1280×720 로 줄여 씀
        int recW, recH;
        RenderTexture recRt; Texture2D recTex;

        byte[] RecBytes(Texture2D src)
        {
            if (recW <= 0) return src.EncodeToJPG(78);
            if (recRt == null || recRt.width != recW) { recRt = new RenderTexture(recW, recH, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB); recTex = new Texture2D(recW, recH, TextureFormat.RGB24, false); }
            var prev = RenderTexture.active;
            Graphics.Blit(src, recRt);
            RenderTexture.active = recRt;
            recTex.ReadPixels(new Rect(0, 0, recW, recH), 0, 0);
            recTex.Apply(false);
            RenderTexture.active = prev;
            return recTex.EncodeToJPG(80);
        }

        /// 장면 녹화할 카드(날짜·시각·장소 = 그 카드가 실제로 뜨는 때)
        static readonly (string card, int day, int min, string place)[] SceneCards =
        {
            ("C1-03", 306, 19 * 60 + 25, "Gukbap"), ("C1-04", 306, 20 * 60 + 45, "Gukbap"), ("N01", 310, 20 * 60 + 25, "Gukbap"),
            ("C1-07", 313, 21 * 60 + 25, "Home"), ("C1-08", 314, 6 * 60 + 50, "Site"), ("C1-09", 314, 6 * 60 + 58, "Site"),
            ("C1-11", 316, 8 * 60 + 5, "School"), ("C1-12", 316, 8 * 60 + 12, "School"), ("C1-18", 428, 19 * 60 + 10, "Home"),
            ("C1-19", 429, 9 * 60, "School"), ("C1-20", 501, 17 * 60 + 25, "Zone1"), ("C1-21", 501, 17 * 60 + 42, "Zone1"),
            ("C1-22", 504, 8 * 60 + 15, "School"), ("C1-24", 505, 16 * 60 + 40, "Zone1"), ("C1-26", 508, 17 * 60 + 30, "Gukbap"),
        };

        IEnumerator PartC()
        {
            yield return LabReady();
            if (labBase == null) { Issue("lab.nobase.c", "QA", "실험실 바탕 저장이 없음 — C 건너뜀"); yield break; }
            baseDay = labBase.Month * 100 + labBase.Day;
            if (ArgVal("-qaconly", "") != "scenes") yield return Section("C00 A 항목 — 고친 뒤", C_AfterA, 900f);
            int n = 1;
            foreach (var sc in SceneCards)
            {
                var s = sc;
                yield return Section($"C{n++:00} 장면 — {s.card}", () => C_Scene(s.card, s.day, s.min, s.place), 900f);
            }
        }

        /// B 만·C 만 돌릴 때: 타이틀 → 새 게임 → 바탕 저장(-qabase)
        IEnumerator LabReady()
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
        }

        // ───────────────────────── C00 A 항목 고친 뒤
        IEnumerator C_AfterA()
        {
            // A3 시간대 이름: 14:00 = 낮(예전 HUD '아침' · 날짜판 '낮')
            yield return Load(311, 14 * 60, SceneLoader.Zone1, m => Unlocks(m));
            yield return Shot("A3_HUD_14시_낮", $"HUD {M.Band(M.Now)} · 날짜판 {GameClock.Label}");
            // A2 낮잠: 05:30 에 일어나 07:00 에 이불 → 낮잠 2시간(예전: 다음 날 05:30 까지 22시간 반)
            yield return Load(310, 7 * 60, "Home", m => { Unlocks(m); m.S.Set(WorldModel.WokeDayKey, 310); m.S.Set(WorldModel.WokeMinKey, 330); m.G.Fatigue = 40; });
            yield return Shot("A2_잠_표시", W.MarkerPrompt("sleep"));
            yield return UseMarker("sleep");
            if (ChoiceShown) { yield return Shot("A2_낮잠_묻기", string.Join(" / ", Enumerable.Range(0, Story().Talk.Choice.Count).Select(Story().Talk.Choice.Label))); yield return Choose(0); }
            yield return Advance(60f);
            yield return Shot("A2_낮잠_뒤", $"{GameClock.DateText} {GameClock.TimeText} · 피로 {M.G.Fatigue} · 낮잠 {W.Naps}");
            // A4 알바 중 Esc = 일시정지(판 시간 멈춤)
            yield return Load(310, 16 * 60 + 40, "Gukbap", m => { Unlocks(m); ResetJob(m, "job.gukbap"); });
            yield return UseMarker("job_serving");
            if (ChoiceShown) yield return Choose(0);
            for (int i = 0; i < 300 && !W.Job.Shown; i++) yield return null;
            yield return Frames(60);
            int p0 = W.Job.Prompts;
            yield return Tap(Key.Escape);
            yield return Frames(30);
            yield return Shot("A4_알바_Esc_일시정지", $"일시정지 {GameState.Paused} · 문제 {W.Job.Prompts}");
            yield return Secs(3f);
            L($"A4 알바 Esc: 일시정지 {GameState.Paused} · 3초 동안 문제 {p0} → {W.Job.Prompts}");
            if (!GameState.Paused || W.Job.Prompts != p0) Issue("qa2.a4.job", "눈에 띔", $"알바 중 Esc 일시정지가 판을 멈추지 않음(일시정지 {GameState.Paused} · 문제 {p0} → {W.Job.Prompts})");
            yield return Tap(Key.Escape);
            yield return Frames(20);
            yield return JobPlay();
            yield return Advance(60f);
            // A5 밤 체육관 — 레슨 자리 시우 앞모습(21:15)
            yield return Load(310, 21 * 60 + 15, SceneLoader.Gym, m => Unlocks(m));
            for (int i = 0; i < 300 && Gm == null; i++) yield return null;
            foreach (var st in new[] { "Sock_Drill_D05", "Sock_Drill_D03" })
            {
                var s = Gm != null ? Gm.Sock(st) : null; if (s == null) continue;
                Mo.Teleport(s.position, s.eulerAngles.y);
                var cam = CutCams.Make(CamPreset.Mid, Mo.transform, null); cam.SetLive(true);
                yield return Frames(30);
                yield return Shot($"A5_밤_체육관_시우_{st.Substring(11)}", $"{GymLights.Name(Gm.Lights.Part)} · 채움빛 {(Gm.Lights.Fill != null && Gm.Lights.Fill.enabled)}");
                CutCams.Release(cam);
                yield return Frames(5);
            }
            // A7 드릴 자리 글 — 가까이(작게·머리 위·사람 뒤로)
            yield return Load(310, 16 * 60 + 45, SceneLoader.Gym, m => Unlocks(m));
            for (int i = 0; i < 300 && Gm == null; i++) yield return null;
            var mk = Gm != null ? Gm.Markers.FirstOrDefault(x => x != null && x.DrillId == "D08") ?? Gm.Markers.FirstOrDefault(x => x != null) : null;
            if (mk != null)
            {
                Mo.Teleport(mk.transform.position - mk.transform.forward * 0.9f + Vector3.up * 0.05f, mk.transform.eulerAngles.y);
                Ld.Cam?.SnapBehind();
                yield return Frames(30);
                yield return Shot("A7_드릴_자리_글_가까이", $"{mk.DrillId} · 이름표 {mk.Label.fontSize} · 스티커 {mk.StickerShown}");
            }
            // A9 밤 거리 빛 웅덩이(지름 3~4m) · 낮 나무·벽돌(팔레트 고정·잎 안쪽 먹선)
            var stop = W.Def.Balance.Point("bus_stop").Pos;
            yield return Load(311, 22 * 60 + 10, SceneLoader.Zone1, m => Unlocks(m), true, stop + new Vector3(2.5f, 0f, -6f), 20f);
            Ld.Cam?.SnapBehind();
            yield return Frames(40);
            yield return Shot("A9_밤_거리_웅덩이");
            var wr = W.Def.Balance.Point("waryong").Pos;
            yield return Load(311, 13 * 60, SceneLoader.Zone1, m => Unlocks(m), true, wr + new Vector3(4f, 0f, -6f), 330f);
            Ld.Cam?.SnapBehind();
            yield return Frames(40);
            yield return Shot("A9_낮_나무_벽돌");
        }

        // ───────────────────────── C01~ 핵심 카드 실제 장면 녹화
        IEnumerator C_Scene(string card, int day, int min, string place)
        {
            var c = W.Def.Card(card);
            if (c == null) { Issue("qa2.scene.none." + card, "QA", $"카드 {card} 없음"); yield break; }
            Vector3? pos = null; float yaw = 0f;
            if (place == SceneLoader.Zone1)
            {
                var pk = W.Def.Balance.Point("parking").Pos;
                pos = card == "C1-20" ? new Vector3(-191.5f, 30.3f, 98.3f) : pk + new Vector3(0f, 0f, 6f);
                yaw = card == "C1-20" ? 100f : 180f;
            }
            yield return Load(day, min, place, m =>
            {
                Unlocks(m); m.G.Fatigue = 0;
                if (card == "C1-19") { int need = 40000 - m.L.Envelope; if (need > 0) m.L.Apply("envelope", $"+{need}"); }
                foreach (var f in new[] { "cake_quest", "site_booked", "consent", "envelope_rule" }) m.F.Raise(f);
            }, true, pos, yaw);
            // 읽기 속도: 대사 = 글자 수 ÷ 8 + 0.8초 · 스토리보드·문자 카드 2.6초 · 선택지 첫 보기 · 시비는 싸움 봇
            W.BotSkill = 0.9f; W.BotRead = 2.6f; W.BotRide = true; W.BotChoice = null;
            Story().Bot = StoryRunner.BotMode.Read; Story().BotAnswer = null;
            recW = 1280; recH = 720;
            var watch = StartCoroutine(SceneShots(card));
            float t0 = T; int f0 = 0;
            string name = "장면_" + card;
            yield return RecWhile(name, 900f, W.RunCard(c, true));
            f0 = recFrame;
            StopCoroutine(watch);
            recW = recH = 0;
            Story().Bot = StoryRunner.BotMode.None; W.BotSkill = 0f; W.BotRead = 0.15f; W.BotRide = false;
            L($"장면 길이: {card} 「{c.Title}」 {f0 / 30f:0.0}초({f0}장) · 카드 장면 {W.LastCardScene} · 스토리보드 {Story().Board.StoryboardShows}");
            yield return Frames(10);
        }

        /// 장면 중 사진: 장면(대사 열쇠)마다 셋째 자막 줄과 마지막 자막 줄에서 한 장씩
        IEnumerator SceneShots(string card)
        {
            string last = ""; var taken = new HashSet<string>();
            while (true)
            {
                var st = Story();
                var cur = st != null && st.Talk != null ? st.Talk.Current : null;
                string id = cur != null ? cur.Id : "";
                if (id != last && cur != null && cur.Mode != DlgMode.Event && cur.Mode != DlgMode.Choice && st.Dialogue != null)
                {
                    last = id;
                    var lines = st.Dialogue.Get(cur.Scene).Where(x => x.Mode == DlgMode.Sub || x.Mode == DlgMode.Inner || x.Mode == DlgMode.Screen).ToList();
                    int k = lines.FindIndex(x => x.Id == id);
                    bool want = (k == 2 || k == lines.Count - 1 || (lines.Count > 14 && k == lines.Count / 2)) && taken.Add(id);
                    if (want)
                    {
                        for (int f = 0; f < 24; f++) yield return null;
                        ShotNow($"장면_{card}_{cur.Scene}_{k + 1:00}", $"{cur.Who}: {cur.Display.Replace("\n", " ")}");
                    }
                }
                yield return null;
            }
        }
    }
}
