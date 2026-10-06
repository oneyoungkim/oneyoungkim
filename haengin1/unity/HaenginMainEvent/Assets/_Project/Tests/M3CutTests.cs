// 행인1의 메인이벤트 — M3 2단계 테스트 D05 · D06 (docs/09_M3_버티컬슬라이스_설계.md 6-2)
//   D05 Cutscene_Skip_Equivalence : 조작 없는 장면 전부(회차 카드·컷신·몽타주·화면 연출 — 타임라인·대사·자리 장면)를 끝까지 봄 / 처음에 건너뜀 →
//                                   끝 상태(플래그·가계부·도감·시우 위치·카메라·입력 맵·표정·소품·흑백·비네팅·가계부 표) 같음
//   D06 Cutscene_Timing           : 타임라인 컷신 길이 = 에셋 길이 ± 1프레임, 자막 클립 길이·실제 표시 ≥ 글자 수 ÷ 8 + 0.8초, 대기 칸이 있는 대사 줄도 같은 규칙
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Haengin.Tests
{
    public sealed class M3CutTests
    {
        [UnityTearDown] public IEnumerator TearDown() { if (M3Lab.Runner != null) M3Lab.Runner.OneScene = false; return M3Lab.TearDown(); }

        /// 데모 목록을 봇으로 한 번 돌며 장면마다 시작 상태를 모은다
        static IEnumerator Starts(Dictionary<string, SaveData> into)
        {
            var r = M3Lab.Runner;
            void On(SceneDef s) => into[s.Id] = r.SceneStart.Clone();
            r.SceneStarted += On;
            r.Bot = StoryRunner.BotMode.Fast;
            r.Begin(false, null, null);
            yield return M3Lab.WaitDone(60 * 60 * 6);
            r.SceneStarted -= On;
            r.Stop();
            yield return Lab.Frames(5);
        }

        static string End(StoryRunner r)
        {
            var snap = r.Snapshot(r.Scene);
            snap.SavedAt = snap.Label = ""; snap.Minutes = 0;
            var p = M3Lab.Motor.Position;
            string faces = string.Join(",", CutCtx.Faces.OrderBy(k => k.Key).Select(k => k.Key + "=" + k.Value));
            string props = string.Join(",", CutCtx.Props.OrderBy(k => k.Key).Select(k => k.Key + "=" + k.Value));
            return $"상태 {JsonUtility.ToJson(snap)}\n위치 ({p.x:F2},{p.y:F2},{p.z:F2}) · 카메라 yaw {M3Lab.Cam.Yaw:F0} · 맵 {InputMaps.Enabled(r.Actions)} · 장소 {r.Loader.Place} · " +
                   $"표정 [{faces}] · 소품 [{props}] · 흑백 {InkMode.On} · 비네팅 {(r.Endure != null && r.Endure.On)} · 가계부 표 {(r.LedgerUi != null && r.LedgerUi.TableShown)} · 자막 {(r.Talk.Sub.Visible || r.Talk.Inner.Visible)} · timeScale {Time.timeScale:F2}";
        }

        [UnityTest, Timeout(3600000)]
        public IEnumerator D05_Cutscene_Skip_Equivalence()
        {
            yield return M3Lab.OpenZone1();
            var r = M3Lab.Runner;
            var starts = new Dictionary<string, SaveData>();
            yield return Starts(starts);
            var ids = r.Def.Scenes.Where(s => s.Kind == SceneKind.Card || s.Kind == SceneKind.Cut || s.Kind == SceneKind.Montage || s.Kind == SceneKind.Ui).Select(s => s.Id).ToList();
            var log = new StringBuilder();
            var fails = new List<string>();
            r.OneScene = true;
            foreach (var id in ids)
            {
                var res = new string[2];
                int[] frames = new int[2];
                for (int k = 0; k < 2; k++)
                {
                    CutCtx.Faces.Clear(); CutCtx.Props.Clear();
                    r.Bot = k == 0 ? StoryRunner.BotMode.Read : StoryRunner.BotMode.Fast;
                    r.Begin(false, id, starts[id]);
                    int f = 0;
                    while (!r.Finished && f < 60 * 240) { f++; yield return null; }
                    Assert.IsTrue(r.Finished, $"{id} {(k == 0 ? "끝까지" : "건너뛰기")} 끝나지 않음");
                    yield return Lab.Frames(2);
                    res[k] = End(r);
                    frames[k] = f;
                    r.Stop();
                    yield return Lab.Frames(4);
                }
                bool same = res[0] == res[1];
                log.AppendLine($"{id,-4} 끝까지 {frames[0] * Lab.Dt,6:F1}초 / 건너뜀 {frames[1] * Lab.Dt,5:F1}초 · {(same ? "같음" : "다름")}");
                if (!same) fails.Add($"{id}\n  끝까지: {res[0]}\n  건너뜀: {res[1]}");
            }
            Debug.Log($"[M3Test] D05 Cutscene_Skip_Equivalence: {ids.Count}편 · 다름 {fails.Count}\n{log}");
            Assert.IsEmpty(fails, string.Join("\n", fails));
        }

        [UnityTest, Timeout(1200000)]
        public IEnumerator D06_Cutscene_Timing()
        {
            yield return M3Lab.OpenZone1();
            var r = M3Lab.Runner;
            Assert.NotNull(r.Cuts, "CutLib");
            var log = new StringBuilder();
            var fails = new List<string>();
            // 데이터: 자막 클립 길이
            foreach (var e in r.Cuts.Cuts)
                foreach (var (clip, secs) in Cutscene.Subtitles(e.Timeline))
                {
                    float need = clip.Text.Replace("\\n", "").Length / 8f + 0.8f;
                    log.AppendLine($"{e.Name} 자막 '{clip.Text.Replace("\\n", " ")}' {secs:F2}초(최소 {need:F2})");
                    if (secs + 1e-3 < need) fails.Add($"{e.Name} 자막 클립 {secs:F2} < {need:F2}");
                }
            // 데이터: 대기 칸이 있는 대사 줄
            foreach (var l in r.Dialogue.Lines.Where(l => l.Wait > 0f))
                if (l.Wait + 1e-3 < l.MinShow) fails.Add($"{l.Id} 대기 {l.Wait:F2} < {l.MinShow:F2}");
            // 실제 재생: 컷신이 걸린 장면(1-1 · 1-6)을 끝까지
            r.OneScene = true;
            var start = new SaveData();
            foreach (var e in r.Cuts.Cuts)
            {
                var s = r.Def.Find(e.Name);
                if (s == null) continue;
                r.Bot = StoryRunner.BotMode.Read;
                start.SceneId = s.Id;
                r.Begin(false, s.Id, start);
                int f = 0;
                while (!r.Finished && f < 60 * 120) { f++; yield return null; }
                int frames = r.Cut.Frames;
                double dur = r.Cut.Duration;
                float diff = Mathf.Abs(frames * Lab.Dt - (float)dur);
                var shown = CutCtx.SubTimes.ToList();
                log.AppendLine($"{e.Name} 재생 {frames}프레임 = {frames * Lab.Dt:F3}초 / 에셋 {dur:F3}초(차 {diff * 1000f:F1}ms) · 실제 자막 {string.Join(" ", shown.Select(x => x.secs.ToString("F2")))}");
                if (diff > Lab.Dt + 1e-4f) fails.Add($"{e.Name} 길이 {frames * Lab.Dt:F3} ≠ {dur:F3} ± 1프레임");
                foreach (var (text, secs) in shown)
                {
                    float need = text.Replace("\\n", "").Length / 8f + 0.8f;
                    if (secs + Lab.Dt * 1.5f < need) fails.Add($"{e.Name} 자막 실제 표시 {secs:F2} < {need:F2} '{text}'");
                }
                r.Stop();
                yield return Lab.Frames(4);
            }
            Debug.Log($"[M3Test] D06 Cutscene_Timing: 실패 {fails.Count}\n{log}");
            Assert.IsEmpty(fails, string.Join("\n", fails));
        }
    }
}
