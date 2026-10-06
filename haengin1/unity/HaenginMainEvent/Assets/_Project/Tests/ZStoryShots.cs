// 행인1의 메인이벤트 — M3 0~5단계 눈 확인·녹화(docs/09_M3_버티컬슬라이스_설계.md 6-1 '눈 확인' · 6-3) + D08 Perf_First
// -m3shots <폴더> 를 줄 때만(그래픽 필요 — -nographics 빼기). 창·소리 없이 GPU 로 그려 PNG 로(묶기·mp4 는 밖에서 ffmpeg, 소리 트랙 없음).
//   s0_transitions/f_0000.png : 시험 장면 3개(새벽 Zone1 걷기 → 국밥집 무대 대사 → 밤 Zone1 걷기) 전환, 3프레임마다(= 20fps)
//   s1_*.png                  : 대화 화면(자막 · 속마음 · '참기' 선택지 · 화면 글자 · 대화 중 일시정지 메뉴)
//   s2_cut11/ · s2_cut16/     : 시험 컷신 1-1 오프닝(캡슐) · 1-6 학예회 흑백(A′)
//   s3_<시간대>_<자리>.png     : 시간대 조명 4종 × Zone1 다섯 자리(실제 게임 카메라)
//   s4_reveal/ · s4_*.png     : 2-1 가계부 공개 녹화 · 회차 카드 · 가계부 한 줄 · 도감 화면 · '참기' 비네팅
//   s5_*.png                  : 타이틀 · 이어하기 있는 타이틀 · 설정 · 저장 경고
// D08 Perf_First(그래픽이 있으면 늘 — 인자 없이도): 밤 상가거리 걷기 · 새벽 와룡공원길 걷기(오르막 자전거길 전 대용) 각 10초, 1920×1080 렌더 한 번의 실제 시간(GPU 동기화 포함)·
//   삼각형·SetPass·그리기 호출 → Logs/perf_first.csv + 요약 줄 '[M3] 성능 첫 측정'. 합격 기준은 D30(3-7 예산) — 지금은 기록만.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Haengin.Tests
{
    public sealed class ZStoryShots
    {
        string dir;
        readonly List<string> notes = new List<string>();

        [UnityTearDown] public IEnumerator TearDown() { StoryBoot.LoadBase = p => SceneManager.LoadScene(p, LoadSceneMode.Single); StoryBoot.Request = null; return M3Lab.TearDown(); }

        bool Ready()
        {
            dir = Shots.Arg("-m3shots");
            if (string.IsNullOrEmpty(dir)) return false;
            if (!Shots.Graphics) return false;
#if UNITY_EDITOR
            UnityEditor.ShaderUtil.allowAsyncCompilation = false;
#endif
            Directory.CreateDirectory(dir);
            return true;
        }

        string Shot(string name, Camera c = null) => Shots.Shot(c != null ? c : Camera.main, Path.Combine(dir, name), 1920, 1080, 2, true);

        /// 끝날 때까지 3프레임마다 찍는다
        IEnumerator Record(string sub, Func<bool> done, int maxFrames)
        {
            string d = Path.Combine(dir, sub);
            if (Directory.Exists(d)) foreach (var f in Directory.GetFiles(d, "f_*.png")) File.Delete(f);
            Directory.CreateDirectory(d);
            int n = 0, k = 0;
            for (int i = 0; i < maxFrames && !done(); i++)
            {
                if (i % 3 == 0) { Shots.Shot(Camera.main, Path.Combine(d, $"f_{k++:0000}.png"), 1920, 1080, 1, true); }
                n++;
                yield return null;
            }
            notes.Add($"{sub}: {n}프레임 → PNG {k}장({k / 20f:F1}초 영상)");
        }

        void Note(string s) { notes.Add(s); Debug.Log("[M3Shots] " + s); }

        IEnumerator Finish(string file)
        {
            File.WriteAllText(Path.Combine(dir, file), string.Join("\n", notes));
            Debug.Log("[M3Shots]\n" + string.Join("\n", notes));
            yield return null;
        }

        // ───────────────────────── 0·1단계: 전환 녹화 · 대화 화면
        [UnityTest, Timeout(3600000)]
        public IEnumerator Z1_Transitions_And_Talk()
        {
            if (!Ready()) { Assert.Ignore("-m3shots <폴더> 와 그래픽이 있을 때만"); yield break; }
            yield return M3Lab.OpenZone1();
            var r = M3Lab.Runner;
            r.Bot = StoryRunner.BotMode.Read;
            r.Begin(true, null, null);
            yield return Record("s0_transitions", () => r.Finished, 60 * 150);
            Note($"시험 장면 3개: {string.Join(" → ", r.Trace)}");
            r.Stop(); yield return Lab.Frames(5);

            // 대화 화면: 국밥집(T2) 자막 · 속마음
            r.Bot = StoryRunner.BotMode.None;
            r.OneScene = true;
            r.Begin(true, "T2", null);
            yield return WaitLine(r, "T2.01"); yield return Lab.Frames(20);
            Note("자막: " + Shot("s1_subtitle.png"));
            r.TalkIn.InjectNext(); yield return WaitLine(r, "T2.02"); yield return Lab.Frames(20);
            Note("속마음: " + Shot("s1_inner.png"));
            r.Stop(); yield return Lab.Frames(5);
            // 교실(1-7): '참기' 선택지 + 비네팅 → 화면 글자
            r.Begin(false, "1-7", null);
            for (int i = 0; i < 900 && !r.Talk.Choice.Visible; i++) { if (i % 30 == 29) r.TalkIn.InjectNext(); yield return null; }
            Assert.IsTrue(r.Talk.Choice.Visible, "1-7 '참기' 선택지");
            yield return Lab.Seconds(0.6f);
            Note("'참기' 선택지·비네팅: " + Shot("s1_choice_endure.png"));
            r.Talk.Choice.Force(2);
            yield return Lab.Seconds(0.4f);
            Note("경고 팝업 → [참는다] 강제: " + Shot("s1_choice_warn.png"));
            for (int i = 0; i < 400 && (r.Talk.Current == null || r.Talk.Current.Mode != DlgMode.Screen); i++) { if (i % 40 == 39) r.TalkIn.InjectNext(); yield return null; }
            yield return Lab.Frames(10);
            Note("화면 글자: " + Shot("s1_screen_text.png"));
            GameState.SetPaused(true);
            yield return Lab.Frames(3);
            Note("대화 중 일시정지(도감·저장하고 타이틀로): " + Shot("s1_pause_menu.png"));
            GameState.SetPaused(false);
            r.OneScene = false;
            yield return Finish("notes_s01.txt");
        }

        static IEnumerator WaitLine(StoryRunner r, string id)
        {
            for (int i = 0; i < 600; i++) { if (r.Talk.Current != null && r.Talk.Current.Id == id) yield break; yield return null; }
            Assert.Fail("대사 " + id + " 가 뜨지 않음");
        }

        // ───────────────────────── 2단계: 시험 컷신
        [UnityTest, Timeout(3600000)]
        public IEnumerator Z2_Cutscenes()
        {
            if (!Ready()) { Assert.Ignore("-m3shots <폴더> 와 그래픽이 있을 때만"); yield break; }
            yield return M3Lab.OpenZone1();
            var r = M3Lab.Runner;
            r.Bot = StoryRunner.BotMode.Read;
            r.OneScene = true;
            foreach (var (id, sub) in new[] { ("1-1", "s2_cut11"), ("1-6", "s2_cut16") })
            {
                r.Begin(false, id, null);
                for (int i = 0; i < 120 && !(r.Cut.Playing); i++) yield return null;
                yield return Record(sub, () => r.Finished, 60 * 40);
                Note($"컷신 {id}: {r.Cut.Frames}프레임 · 기록 {CutCtx.Log.Count}줄 [{string.Join(" | ", CutCtx.Log.Take(30))}]");
                r.Stop(); yield return Lab.Frames(5);
            }
            r.OneScene = false;
            yield return Finish("notes_s2.txt");
        }

        // ───────────────────────── 3단계: 시간대 4종 × 다섯 자리
        [UnityTest, Timeout(3600000)]
        public IEnumerator Z3_Lighting()
        {
            if (!Ready()) { Assert.Ignore("-m3shots <폴더> 와 그래픽이 있을 때만"); yield break; }
            yield return M3Lab.OpenZone1();
            var day = M3Lab.Runner.Light;
            foreach (DayPart part in new[] { DayPart.Dawn, DayPart.Day, DayPart.Dusk, DayPart.Night })
            {
                day.Apply(part, true);
                var ls = new List<string>();
                for (int i = 0; i < M3LightTests.Spots.Length; i++)
                {
                    var (name, pos, yaw) = M3LightTests.Spots[i];
                    M3Lab.Motor.Teleport(pos, yaw);
                    M3Lab.Cam.SnapBehind();
                    yield return Lab.Frames(10);
                    float l = M3LightTests.GroundL(Camera.main, out int n);
                    Shot($"s3_{(int)part}{part}_{i + 1}_{name.Replace(' ', '_')}.png");
                    ls.Add($"{name} L*{l:F1}");
                }
                Note($"{part}: 빛 자리 {day.CountLit()} · 창문 {day.CountWindows()} · 걷는 면 {string.Join(" · ", ls)}");
            }
            yield return Finish("notes_s3.txt");
        }

        // ───────────────────────── 4단계: 가계부 공개 · 회차 카드 · 도감 · 비네팅
        [UnityTest, Timeout(3600000)]
        public IEnumerator Z4_Book()
        {
            if (!Ready()) { Assert.Ignore("-m3shots <폴더> 와 그래픽이 있을 때만"); yield break; }
            yield return M3Lab.OpenZone1();
            var r = M3Lab.Runner;
            // 2-1 가계부 공개(봇이 대사를 읽는 속도로)
            r.Bot = StoryRunner.BotMode.Read;
            r.OneScene = true;
            r.Begin(false, "2-1", null);
            yield return Record("s4_reveal", () => r.Finished, 60 * 60);
            r.Stop(); yield return Lab.Frames(5);
            // 회차 카드(2화 — 1화 줄) · 가계부 한 줄 · 비네팅
            r.Bot = StoryRunner.BotMode.None;
            r.Begin(false, "2-0", null);
            yield return Lab.Seconds(1.0f);
            Note("회차 카드: " + Shot("s4_episode_card.png"));
            r.Stop(); yield return Lab.Frames(5);
            r.Begin(false, "1-2", null);
            yield return M3Lab.WaitScene("1-2");
            r.ApplyNow("ledger:+2000:배달비(경비실) → 엄마 통장");
            r.ApplyNow("name:국밥");
            yield return Lab.Seconds(0.3f);
            Note("도감 등록(호칭 붓 글씨) · 가계부 한 줄: " + Shot("s4_toast_name.png"));
            r.Endure.Begin();
            yield return Lab.Seconds(0.5f);
            Note($"'참기' 비네팅(알파 {r.Endure.Alpha:F2}): " + Shot("s4_endure_vignette.png"));
            r.Endure.Clear();
            r.Stop(); yield return Lab.Frames(5);
            r.OneScene = false;
            // 데모 끝까지(봇) → 도감 화면
            r.Bot = StoryRunner.BotMode.Fast;
            r.Begin(false, null, null);
            yield return M3Lab.WaitDone(60 * 60 * 6);
            r.NameUi.ShowGrid(r.Def.Names, r.Names);
            yield return Lab.Frames(3);
            Note($"도감 화면 [{string.Join(" | ", r.NameUi.Cells)}]: " + Shot("s4_namebook.png"));
            r.NameUi.HideGrid();
            yield return Finish("notes_s4.txt");
        }

        // ───────────────────────── 5단계: 타이틀
        [UnityTest, Timeout(1200000)]
        public IEnumerator Z5_Title()
        {
            if (!Ready()) { Assert.Ignore("-m3shots <폴더> 와 그래픽이 있을 때만"); yield break; }
            Time.captureDeltaTime = Lab.Dt;
            SaveStore.Dir = M3Lab.SaveDir;
            Directory.CreateDirectory(M3Lab.SaveDir);
            SaveStore.Delete(SaveStore.Auto); SaveStore.Delete(SaveStore.Manual);
            yield return M3Lab.UnloadAll();
            string loaded = null;
            StoryBoot.LoadBase = p => loaded = p;
            yield return OpenTitle();
            Note("타이틀(저장 없음): " + Shot("s5_title.png", TitleCam()));
            var menu = UnityEngine.Object.FindAnyObjectByType<TitleMenu>();
            menu.Choose("설정");
            yield return Lab.Frames(2);
            Note("설정: " + Shot("s5_settings.png", TitleCam()));
            yield return M3Lab.UnloadAll();
            var save = new SaveData { SceneId = "3-2", Month = 3, Day = 10, Minutes = 22 * 60 + 25, Label = "3월 10일(화) 22:25 · 하루편의점 — 진열·폐기" };
            SaveStore.Write(SaveStore.Auto, save);
            yield return OpenTitle();
            Note("타이틀(이어하기): " + Shot("s5_title_continue.png", TitleCam()));
            yield return M3Lab.UnloadAll();
            File.WriteAllText(SaveStore.PathOf(SaveStore.Auto), "{\"Version\":1,\"Scene");
            yield return OpenTitle();
            UnityEngine.Object.FindAnyObjectByType<TitleMenu>().Choose("이어하기 — 저장 파일 문제");
            yield return Lab.Frames(2);
            Note("저장 경고: " + Shot("s5_title_broken.png", TitleCam()));
            SaveStore.Delete(SaveStore.Auto);
            yield return Finish("notes_s5.txt");
        }

        static Camera TitleCam() => Camera.allCameras.FirstOrDefault(c => c.gameObject.scene.name == "Title") ?? Camera.main;

        static IEnumerator OpenTitle()
        {
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(StoryBoot.TitleScene, new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return Lab.Frames(5);
        }

        // ───────────────────────── D08 성능 첫 측정(그래픽)
        [UnityTest, Timeout(1800000)]
        public IEnumerator Z_D08_Perf_First()
        {
            if (!Shots.Graphics) { Assert.Ignore("그래픽 장치 없음(-nographics) — D08 은 그래픽 켠 배치에서"); yield break; }
            yield return M3Lab.OpenZone1();
            var r = M3Lab.Runner;
            var cam = Camera.main;
            var rt = new RenderTexture(new RenderTextureDescriptor(1920, 1080, RenderTextureFormat.ARGB32, 24) { sRGB = true, msaaSamples = 1 });
            rt.Create();
            var one = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            UnityEngine.Profiling.Profiler.enabled = true;
            var tri = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            var setp = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            var draw = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            var csv = new StringBuilder("경로,프레임,렌더ms,삼각형,SetPass,그리기\n");
            var summary = new List<string>();
            var routes = new[]
            {
                ("밤 상가거리 걷기", DayPart.Night, new Vector3(-199.0f, 31.0f, 103.2f), new Vector3(-176.0f, 28.4f, 103.6f)),
                ("새벽 와룡공원길 걷기(오르막 대용)", DayPart.Dawn, new Vector3(-206.0f, 31.4f, 97.8f), new Vector3(-236.0f, 34.7f, 94.8f)),
            };
            foreach (var (name, part, a, b) in routes)
            {
                r.Light.Apply(part, true);
                var dirv = new Vector3(b.x - a.x, 0f, b.z - a.z);
                M3Lab.Motor.Teleport(a, Mathf.Atan2(dirv.x, dirv.z) * Mathf.Rad2Deg);
                M3Lab.Cam.SnapBehind();
                yield return Lab.Frames(20);
                var ms = new List<double>(); var tris = new List<long>(); var sps = new List<long>(); var dcs = new List<long>();
                for (int i = 0; i < 600; i++)
                {
                    var d = new Vector3(b.x - M3Lab.Motor.Position.x, 0f, b.z - M3Lab.Motor.Position.z);
                    M3Lab.Motor.SetMoveInput(d.normalized, 1f, false);
                    yield return null;
                    if (i % 6 != 0) continue;
                    var prev = cam.targetTexture;
                    cam.targetTexture = rt;
                    var sw = Stopwatch.StartNew();
                    cam.Render();
                    RenderTexture.active = rt;
                    one.ReadPixels(new Rect(0, 0, 1, 1), 0, 0, false);      // GPU 가 다 그릴 때까지 기다림
                    sw.Stop();
                    RenderTexture.active = null;
                    cam.targetTexture = prev;
                    long t = tri.LastValue, sp = setp.LastValue, dc = draw.LastValue;
#if UNITY_EDITOR
                    // 에디터 배치에선 ProfilerRecorder 렌더 카운터가 0 으로 남는 일이 있어 UnityStats(마지막으로 그린 화면)를 쓴다
                    if (t == 0) { t = UnityEditor.UnityStats.triangles; sp = UnityEditor.UnityStats.setPassCalls; dc = UnityEditor.UnityStats.drawCalls; }
#endif
                    ms.Add(sw.Elapsed.TotalMilliseconds); tris.Add(t); sps.Add(sp); dcs.Add(dc);
                    csv.AppendLine($"{name},{i},{sw.Elapsed.TotalMilliseconds:F2},{t},{sp},{dc}");
                }
                var sorted = ms.OrderBy(x => x).ToList();
                double p95 = sorted[(int)(sorted.Count * 0.95)], p99 = sorted[Math.Min(sorted.Count - 1, (int)(sorted.Count * 0.99))];
                summary.Add($"{name}: 1080p 렌더 한 번 평균 {ms.Average():F2}ms · 95% {p95:F2} · 99% {p99:F2} · 삼각형 최대 {tris.Max():N0} · SetPass 최대 {sps.Max()} · 그리기 최대 {dcs.Max()} · 빛 자리 {r.Light.CountLit()}");
            }
            tri.Dispose(); setp.Dispose(); draw.Dispose();
            rt.Release(); UnityEngine.Object.Destroy(rt); UnityEngine.Object.Destroy(one);
            string gpu = SystemInfo.graphicsDeviceName + " · " + SystemInfo.graphicsDeviceType;
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/perf_first.csv", csv.ToString(), new UTF8Encoding(true));
            string dirArg = Shots.Arg("-m3shots");
            if (!string.IsNullOrEmpty(dirArg)) { Directory.CreateDirectory(dirArg); File.WriteAllText(Path.Combine(dirArg, "perf_first.csv"), csv.ToString(), new UTF8Encoding(true)); }
            Debug.Log($"[M3] 성능 첫 측정(D08, 에디터 배치 — 빌드보다 무거움, 합격은 D30): {gpu}\n{string.Join("\n", summary)}\n→ Logs/perf_first.csv");
        }
    }
}
