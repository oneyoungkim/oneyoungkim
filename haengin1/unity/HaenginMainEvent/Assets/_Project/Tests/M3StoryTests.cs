// 행인1의 메인이벤트 — M3 0단계 테스트 D01 · D02 (docs/09_M3_버티컬슬라이스_설계.md 6-2)
//   D01 Story_Order            : 장면 목록을 봇(Fast)으로 끝까지 넘김 ×2 — 장면 순서·날짜·시각·장소·형식 = 데이터, 두 번의 기록이 같음
//   D02 Scene_Transition_State : 장면 39개(장면 32 + 회차 카드 7) 전환마다 — 시우 접지 · 카메라 거리 = 프리셋(실외 4.0 · 실내 2.8) ± 0.1m ·
//                                조작 입력 맵 하나만 · timeScale 1 · 일시정지 풀림 · 길잡이 HUD = 장면 데이터(이동 장면만 보임) · 내린 무대 오브젝트 0 · 이야기 중 M2 무대 꺼짐
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Haengin.Tests
{
    public sealed class M3StoryTests
    {
        [UnityTearDown] public IEnumerator TearDown() => M3Lab.TearDown();

        [UnityTest, Timeout(1800000)]
        public IEnumerator D01_Story_Order()
        {
            yield return M3Lab.OpenZone1();
            var r = M3Lab.Runner;
            var expect = r.Def.Scenes.Select(s => $"{s.Id}|{s.DateLabel}|{s.TimeLabel}|{s.Place}|{s.Kind}").ToList();
            var runs = new List<List<string>>();
            var clocks = new List<string>();
            for (int k = 0; k < 2; k++)
            {
                var seen = new List<string>();
                void OnStart(SceneDef s) => seen.Add($"{s.Id}@{GameClock.Month}/{GameClock.Day} {GameClock.TimeText}");
                r.SceneStarted += OnStart;
                r.Bot = StoryRunner.BotMode.Fast;
                r.Begin(false, null, null);
                yield return M3Lab.WaitDone(60 * 60 * 6);
                r.SceneStarted -= OnStart;
                Assert.IsTrue(r.Finished, $"{k + 1}번째: 끝까지 못 감(지금 {r.Scene?.Id})");
                runs.Add(r.Trace.ToList());
                clocks.Add(string.Join(",", seen));
                Debug.Log($"[M3Test] D01 {k + 1}번째: 장면 {r.Trace.Count}개 · {r.Flags.Summary} · {r.Ledger.Summary} · 도감 {string.Join(",", r.Names.Got)}");
                r.Stop();
                yield return Lab.Frames(5);
            }
            CollectionAssert.AreEqual(expect, runs[0], "장면 순서·날짜·시각·장소·형식 = 데이터");
            CollectionAssert.AreEqual(runs[0], runs[1], "두 번 기록이 같음");
            Assert.AreEqual(clocks[0], clocks[1], "게임 시계 기록이 같음");
            // 시계: 장면 시작 시각 = 데이터
            foreach (var s in r.Def.Scenes) StringAssert.Contains($"{s.Id}@{s.Month}/{s.Day} {s.TimeLabel}", clocks[0]);
            Debug.Log($"[M3Test] D01 Story_Order: 장면 {expect.Count}개 순서·날짜·시각 같음 ×2\n{string.Join("\n", runs[0])}");
        }

        [UnityTest, Timeout(1800000)]
        public IEnumerator D02_Scene_Transition_State()
        {
            yield return M3Lab.OpenZone1();
            var r = M3Lab.Runner;
            var log = new StringBuilder();
            var fails = new List<string>();
            r.Bot = StoryRunner.BotMode.None;
            r.Begin(false, null, null);
            float maxLoad = 0f, maxTotal = 0f;
            string prevStage = null;
            foreach (var s in r.Def.Scenes)
            {
                yield return M3Lab.WaitScene(s.Id);
                yield return Lab.Frames(4);
                var m = M3Lab.Motor;
                var ld = r.Loader;
                bool grounded = m.Grounded;
                int maps = InputMaps.EnabledCount(r.Actions);
                string mapName = InputMaps.Enabled(r.Actions);
                var hud = Object.FindAnyObjectByType<RouteHud>(FindObjectsInactive.Include);
                bool hudOn = hud != null && hud.isActiveAndEnabled && !string.IsNullOrEmpty(hud.Current) && hud.Plate != null && hud.Plate.activeInHierarchy;
                bool wantHud = s.Kind == SceneKind.Move;
                float dist = M3Lab.Cam.Distance, want = ld.ExpectedCamDistance, pull = M3Lab.Cam.Pull;
                var stages = M3Lab.LoadedStages();
                string want1 = s.Kind == SceneKind.Card ? (prevStage ?? "") : (s.InZone1 ? "" : "St_" + s.Place);
                bool stageOk = s.Kind == SceneKind.Card ? stages.Length <= 1 : (s.InZone1 ? stages.Length == 0 : stages.Length == 1 && stages[0] == want1);
                bool placesOk = StagePlace.All.Count == stages.Length;
                bool m2Off = r.M2Stages == null || !r.M2Stages.activeInHierarchy;
                string line = $"{s.Id,-4} {s.Kind,-7} {s.Place,-7} 접지 {grounded} · 맵 {mapName}({maps}) · 카메라 {dist:F2}m(목표 {want:F2}, 당김 {pull:F2}) · HUD {(hudOn ? "보임 '" + hud.Current + "'" : "숨김")} · " +
                              $"timeScale {Time.timeScale:F2} · 일시정지 {GameState.Paused} · 무대 [{string.Join(",", stages)}] · M2 꺼짐 {m2Off} · 전환 {ld.LastTotal:F2}s(무대 열기 {ld.LastLoad:F2}s)";
                log.AppendLine(line);
                if (s.Kind != SceneKind.Card)
                {
                    if (!grounded) fails.Add(s.Id + " 접지 아님");
                    if (Mathf.Abs(dist - want) > 0.1f) fails.Add($"{s.Id} 카메라 거리 {dist:F2} ≠ {want:F2}");
                    if (hudOn != wantHud) fails.Add($"{s.Id} HUD {(hudOn ? "보임" : "숨김")} — 데이터는 {(wantHud ? "보임" : "숨김")}");
                    if (!stageOk) fails.Add($"{s.Id} 무대 장면 [{string.Join(",", stages)}] (기대 {(want1 == "" ? "없음" : want1)})");
                }
                if (maps != 1) fails.Add($"{s.Id} 조작 맵 {maps}개({mapName})");
                if (Mathf.Abs(Time.timeScale - 1f) > 1e-4f) fails.Add($"{s.Id} timeScale {Time.timeScale}");
                if (GameState.Paused || GameState.InputLocked) fails.Add($"{s.Id} 일시정지 {GameState.Paused} · 입력 막힘 {GameState.InputLocked}");
                if (!placesOk) fails.Add($"{s.Id} 남은 무대 오브젝트(StagePlace {StagePlace.All.Count} · 열린 무대 {stages.Length})");
                if (!m2Off) fails.Add($"{s.Id} 이야기 중 M2 무대가 켜져 있음");
                if (s.Kind != SceneKind.Card) { maxLoad = Mathf.Max(maxLoad, ld.LastLoad); maxTotal = Mathf.Max(maxTotal, ld.LastTotal); }
                if (s.Kind != SceneKind.Card) prevStage = s.InZone1 ? null : "St_" + s.Place;
                r.SkipScene();
            }
            yield return M3Lab.WaitDone(600);
            Debug.Log($"[M3Test] D02 Scene_Transition_State: 장면 {r.Def.Scenes.Length}개 · 실패 {fails.Count} · 무대 열기 최대 {maxLoad:F2}s · 전환 최대 {maxTotal:F2}s(실제 시간)\n{log}");
            Assert.IsTrue(r.Finished, "끝까지");
            Assert.IsEmpty(fails, string.Join("\n", fails));
        }
    }
}
