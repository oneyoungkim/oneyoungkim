// 행인1의 메인이벤트 — 체육관 눈 확인·녹화·성능(docs/12_체육관_설계.md 8장 6단계 확인 사진)
// -gymshots <폴더> 를 줄 때만(그래픽 필요 — -nographics 빼기). 창·소리 없이 GPU 로 그려 PNG 로(묶기·mp4 는 밖에서 PIL·ffmpeg, 소리 없음).
//   compare/g01~g06.png : 스타일 프레임 6앵글의 카메라 소켓(Sock_Cam_g##)에서 Unity 게임 카메라, 시간대 = 프레임과 같게(낮·저녁·저녁·아침·낮·밤)
//   cards/D##.png       : 플레이 가능 드릴 10종을 봇(실력 섞음)으로 돌려 결과 카드(960×540)
//   seq_mitt/f_###.png · seq_spar/f_###.png : D04 미트·D11 스파링 1R 앞 8초(30fps 240장, 960×540, UI 포함)
//   perf_gym.csv + '[Gym] 성능' 줄 : 저녁 피크 매트 쪽·케이지 쪽 1080p 렌더 ms·삼각형(D08 방식)
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Unity.Cinemachine;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;

namespace Haengin.Tests
{
    public sealed class ZGymShots
    {
        [UnityTearDown] public IEnumerator Down() { yield return M3Lab.TearDown(); }

        [UnityTest, Timeout(2400000)]
        public IEnumerator Z_Gym_Shots()
        {
            string dir = Shots.Arg("-gymshots");
            if (string.IsNullOrEmpty(dir)) { Assert.Ignore("-gymshots <폴더> 없음"); yield break; }
            if (!Shots.Graphics) { Assert.Ignore("그래픽 장치 없음(-nographics)"); yield break; }
            Directory.CreateDirectory(dir);
            yield return M3Lab.OpenZone1();
            var r = M3Lab.Runner;
            GymState.Current = new GymSave();
            yield return r.Loader.Go(SceneLoader.Gym, "Floor", false, Vector3.zero, 0f, false);
            yield return null; yield return null;
            var gym = GymManager.Instance;
            Assert.NotNull(gym, "GymManager");
            var cam = Camera.main;
            var brain = cam.GetComponent<CinemachineBrain>();
            var log = new List<string>();

            // ① 스타일 프레임 6앵글 비교
            var frames = new (string key, GymPart part, DayPart light)[]
            {
                ("g01", GymPart.Day, DayPart.Day), ("g02", GymPart.Evening, DayPart.Dusk), ("g03", GymPart.Evening, DayPart.Dusk),
                ("g04", GymPart.Morning, DayPart.Day), ("g05", GymPart.Day, DayPart.Day), ("g06", GymPart.Night, DayPart.Night),
            };
            // 시우를 프레임 자리에: g01 계단 아래, g02 케이지 안(고준과), g03 매트 줄 끝, g04 벤치 옆, g05 사무실 앞, g06 매트 가장자리(미트)
            var siwooSpots = new Dictionary<string, (Vector3 local, float yaw)>
            {
                ["g01"] = (new Vector3(3.5f, 0f, 7.0f), 150f), ["g02"] = (new Vector3(13.6f, 0.25f, 6.0f), 90f), ["g03"] = (new Vector3(9.0f, 0.03f, 3.3f), 180f),
                ["g04"] = (new Vector3(6.6f, 0f, 9.8f), 300f), ["g05"] = (new Vector3(16.0f, 0f, 9.2f), 0f), ["g06"] = (new Vector3(8.5f, 0.03f, 4.0f), 90f),
            };
            Directory.CreateDirectory(Path.Combine(dir, "compare"));
            foreach (var (key, part, light) in frames)
            {
                r.Light.Apply(light, true);
                gym.ApplyPart(part);
                if (key == "g06") { var mt = gym.Sock("Sock_NPC_Coach_MatEdge"); if (gym.Yunjun != null && mt != null) { gym.Yunjun.gameObject.SetActive(true); gym.Yunjun.Place(mt.position, mt.forward, GymNpc.Routine.Mitts); } }
                if (key == "g02") { var gc = gym.Sock("Sock_NPC_Gojun_Cage"); if (gym.Gojun != null && gc != null) { gym.Gojun.gameObject.SetActive(true); gym.Gojun.Place(gc.position, gc.forward, GymNpc.Routine.Shadow); } }
                var sp = siwooSpots[key];
                M3Lab.Motor.Teleport(gym.transform.TransformPoint(sp.local), sp.yaw);
                var sock = gym.Sock("Sock_Cam_" + key);
                yield return Lab.Frames(45);
                brain.enabled = false;
                if (gym.Mirror != null) gym.Mirror.Set(key == "g01" || key == "g03");
                cam.transform.SetPositionAndRotation(sock.position, sock.rotation);
                float fovWas = cam.fieldOfView; cam.fieldOfView = 50f;
                yield return null;
                if (gym.Mirror != null && gym.Mirror.Active) gym.Mirror.Render(cam);
                Shots.Shot(cam, Path.Combine(dir, "compare", key + ".png"), 1920, 1080, 2, false);
                cam.fieldOfView = fovWas;
                brain.enabled = true;
                if (gym.Mirror != null) gym.Mirror.Set(false);
                log.Add($"{key} {GymLights.Name(part)} 형광등 {gym.Lights.LitCount}");
            }

            // ② 드릴 10종 결과 카드(라운드를 20초로 줄인 사본으로 — 등급은 봇 실력대로)
            r.Light.Apply(DayPart.Dusk, true);
            gym.ApplyPart(GymPart.Evening);
            var bookWas = gym.Book;
            var book = UnityEngine.Object.Instantiate(bookWas);
            foreach (var d in book.Drills) { d.RoundSec = Mathf.Min(d.RoundSec, 20f); d.RestSec = Mathf.Min(d.RestSec, 2f); }
            gym.Book = book;
            Directory.CreateDirectory(Path.Combine(dir, "cards"));
            var skills = new (string id, float skill)[] { ("D01", 1f), ("D02", 0.8f), ("D03", 0.9f), ("D04", 1f), ("D05", 0.7f), ("D06", 0.85f), ("D07", 0.9f), ("D08", 0.75f), ("D10", 0.8f), ("D11", 1f) };
            var cardLines = new List<string>();
            foreach (var (id, skill) in skills)
            {
                DrillResult res = null; DrillReward rew = default;
                Action<DrillDef, DrillResult, DrillReward> h = (d, rr, w) => { res = rr; rew = w; };
                gym.DrillFinished += h;
                GymState.Current.Fatigue = 0;           // 드릴마다 쉬고 온 몸(피로 85 면 이윤준이 막음)
                bool started = gym.StartDrill(id, skill);
                int n = 0;
                string seq = id == "D04" ? "seq_mitt" : id == "D11" ? "seq_spar" : null;
                int frame = 0, saved = 0;
                if (seq != null) Directory.CreateDirectory(Path.Combine(dir, seq));
                while (started && gym.Runner.State != DrillRunner.Phase.Result && n++ < 60 * 400)
                {
                    yield return null;
                    if (seq != null && gym.Runner.State == DrillRunner.Phase.Round && gym.Runner.Round == 1 && gym.Runner.T < 8.0 && (frame++ % 2 == 0) && saved < 240)
                        Shots.Shot(cam, Path.Combine(dir, seq, $"f_{saved++:000}.png"), 960, 540, 1, true);
                }
                // 결과 카드가 뜬 뒤 0.9초(연출 끝) 찍고 닫기
                for (int i = 0; i < 400 && !(gym.Card.Visible && gym.Card.ShownFor >= 0.9f); i++) yield return null;
                if (gym.Card.Visible) Shots.Shot(cam, Path.Combine(dir, "cards", id + ".png"), 960, 540, 2, true);
                gym.CloseCard();
                for (int i = 0; i < 60 && gym.Busy; i++) yield return null;
                gym.DrillFinished -= h;
                cardLines.Add($"{id} skill {skill:F2} → {(res != null ? res.ToString() : (started ? "결과 없음" : "시작 못 함"))} · {rew}");
                yield return Lab.Frames(10);
            }
            gym.Book = bookWas;

            // ③ 성능(D08 방식): 저녁 피크, 매트 쪽(g03)·케이지 쪽(g02) 카메라 자리, 1080p 60장
            var rt = new RenderTexture(new RenderTextureDescriptor(1920, 1080, RenderTextureFormat.ARGB32, 24) { sRGB = true, msaaSamples = 1 }); rt.Create();
            var one = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            var tri = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            var setp = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            var draw = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            var csv = new StringBuilder("자리,프레임,렌더ms,삼각형,SetPass,그리기\n");
            var perf = new List<string>();
            gym.ApplyPart(GymPart.Evening);
            r.Light.Apply(DayPart.Dusk, true);
            M3Lab.Motor.Teleport(gym.transform.TransformPoint(new Vector3(9.0f, 0.03f, 3.3f)), 180f);
            yield return Lab.Frames(30);
            bool camWas = cam.enabled; cam.enabled = false; brain.enabled = false;
            foreach (var key in new[] { "g03", "g02" })
            {
                var sock = gym.Sock("Sock_Cam_" + key);
                cam.transform.SetPositionAndRotation(sock.position, sock.rotation); cam.fieldOfView = 50f;
                var ms = new List<double>(); var tris = new List<long>(); var sps = new List<long>(); var dcs = new List<long>();
                for (int i = 0; i < 60; i++)
                {
                    yield return null;
                    var prev = cam.targetTexture; cam.targetTexture = rt;
                    var sw = Stopwatch.StartNew();
                    cam.Render();
                    RenderTexture.active = rt; one.ReadPixels(new Rect(0, 0, 1, 1), 0, 0, false);
                    sw.Stop();
                    RenderTexture.active = null; cam.targetTexture = prev;
                    long t = tri.LastValue, sp = setp.LastValue, dc = draw.LastValue;
#if UNITY_EDITOR
                    if (t == 0) { t = UnityEditor.UnityStats.triangles; sp = UnityEditor.UnityStats.setPassCalls; dc = UnityEditor.UnityStats.drawCalls; }
#endif
                    ms.Add(sw.Elapsed.TotalMilliseconds); tris.Add(t); sps.Add(sp); dcs.Add(dc);
                    csv.AppendLine($"{key},{i},{sw.Elapsed.TotalMilliseconds:F2},{t},{sp},{dc}");
                }
                var sorted = ms.OrderBy(x => x).ToList();
                perf.Add($"{key}: 1080p 평균 {ms.Average():F2}ms · 95% {sorted[(int)(sorted.Count * 0.95)]:F2} · 삼각형 최대 {tris.Max():N0} · SetPass 최대 {sps.Max()} · 그리기 최대 {dcs.Max()}");
            }
            cam.enabled = camWas; brain.enabled = true;
            tri.Dispose(); setp.Dispose(); draw.Dispose(); rt.Release(); UnityEngine.Object.Destroy(rt); UnityEngine.Object.Destroy(one);
            File.WriteAllText(Path.Combine(dir, "perf_gym.csv"), csv.ToString(), new UTF8Encoding(true));
            File.WriteAllText(Path.Combine(dir, "summary.txt"), string.Join("\n", log) + "\n\n" + string.Join("\n", cardLines) + "\n\n" + string.Join("\n", perf), new UTF8Encoding(true));
            Debug.Log($"[Gym] 눈 확인 → {dir}\n{string.Join("\n", log)}\n{string.Join("\n", cardLines)}\n[Gym] 성능(에디터 배치, 실내 1080p): {SystemInfo.graphicsDeviceName}\n{string.Join("\n", perf)}");
        }
    }
}
