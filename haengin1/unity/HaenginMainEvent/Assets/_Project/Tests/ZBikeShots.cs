// 행인1의 메인이벤트 — 8단계 눈 확인(-s8shots <폴더>, 그래픽 필요): 1화 새벽 배달 코스(1-2~1-4) 봇 주행 녹화
//   <폴더>/run/f_0000.jpg … (1280×720, 3프레임마다 = 20fps — 밖에서 ffmpeg 로 무음 mp4) + 장면 사진 s8_*.png + notes_s8.txt
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Haengin.Tests
{
    public sealed class ZBikeShots
    {
        [UnityTearDown] public IEnumerator TearDown() => M3Lab.TearDown();

        [UnityTest, Timeout(3600000)]
        public IEnumerator Z_BikeShots()
        {
            string dir = Shots.Arg("-s8shots");
            if (string.IsNullOrEmpty(dir)) { Assert.Ignore("-s8shots <폴더> 를 줄 때만"); yield break; }
            if (!Shots.Graphics) { Assert.Ignore("그래픽 장치 없음"); yield break; }
#if UNITY_EDITOR
            UnityEditor.ShaderUtil.allowAsyncCompilation = false;
#endif
            var run = Path.Combine(dir, "run");
            Directory.CreateDirectory(run);
            yield return M3Lab.OpenZone1();
            var dl = Object.FindAnyObjectByType<DayLight>();
            if (dl != null) dl.Apply(DayPart.Dawn, true);
            var bc = Object.FindAnyObjectByType<BikeCourse>(FindObjectsInactive.Include);
            Assert.IsNotNull(bc, "BikeCourse");
            bc.Bot = true;
            var notes = new List<string>();
            int n = 0, frame = 0;
            bool upShot = false, downShot = false, topShot = false, gateShot = false, climbShot = false;
            var cam = Camera.main;
            var co = bc.StartCoroutine(bc.RunAll());
            yield return Lab.Frames(2);
            Shots.Shot(cam, Path.Combine(dir, "s8_start.png"), 1920, 1080, 1, true);
            float t0 = Time.time;
            while (bc.Riding || bc.Leg >= 0)
            {
                if (frame++ % 3 == 0)
                {
                    var tex = Shots.Grab(cam, 1280, 720, 1, true);
                    File.WriteAllBytes(Path.Combine(run, $"f_{n++:0000}.jpg"), tex.EncodeToJPG(88));
                    Object.Destroy(tex);
                }
                if (!gateShot && bc.Stops[0].Done) { gateShot = true; Shots.Shot(cam, Path.Combine(dir, "s8_gate.png"), 1920, 1080, 1, true); notes.Add($"경비실 건넴 {Time.time - t0:F1}s"); }
                if (!climbShot && bc.Motor.Posture == BikePosture.Stand) { climbShot = true; Shots.Shot(cam, Path.Combine(dir, "s8_stand.png"), 1920, 1080, 1, true); notes.Add($"일어서기 {Time.time - t0:F1}s · 경사 {bc.Motor.SlopeDeg:F1}°"); }
                if (!upShot && bc.Cam != null && bc.Cam.SideW > 0.95f) { upShot = true; Shots.Shot(cam, Path.Combine(dir, "s8_side_overtake.png"), 1920, 1080, 1, true); notes.Add($"측면 트래킹 {Time.time - t0:F1}s"); }
                if (!topShot && bc.Stops[1].Done) { topShot = true; Shots.Shot(cam, Path.Combine(dir, "s8_top_house.png"), 1920, 1080, 1, true); notes.Add($"꼭대기 집 건넴 {Time.time - t0:F1}s · 지각 {bc.LateMin}분"); }
                if (!downShot && bc.DownPasses > 0) { downShot = true; Shots.Shot(cam, Path.Combine(dir, "s8_down_pass.png"), 1920, 1080, 1, true); notes.Add($"내리막 앞지름 {Time.time - t0:F1}s"); }
                if (Time.time - t0 > 200f) break;
                yield return null;
            }
            notes.Add($"끝 {Time.time - t0:F1}s · 프레임 {n} · 태오 오르막 {bc.UpPasses}·내리막 {bc.DownPasses} · 국물 {bc.Soup.Value:F0} · 대사 [{string.Join(" / ", bc.Lines)}]");
            File.WriteAllLines(Path.Combine(dir, "notes_s8.txt"), notes);
            Debug.Log("[M3Test] Z 자전거 녹화: " + string.Join(" · ", notes));
        }

        // 측면 트래킹 녹화(docs/14 6-4, 2026-10-26 — -bikeside <폴더>): 코스 봇을 옆 3.6m 에서 따라가며 페달 클립 상태가 바뀌는 구간만(정차·출발·순항·댄싱·관성·정지)
        //   <폴더>/side/f_00000.jpg … 1280×720(2프레임마다 = 30fps) + side.txt(프레임마다 상태·크랭크·기울기·속도) → ffmpeg -framerate 30 -i f_%05d.jpg -an
        [UnityTest, Timeout(3600000)]
        public IEnumerator Z_BikeSide()
        {
            string dir = Shots.Arg("-bikeside");
            if (string.IsNullOrEmpty(dir)) { Assert.Ignore("-bikeside <폴더> 를 줄 때만"); yield break; }
            if (!Shots.Graphics) { Assert.Ignore("그래픽 장치 없음"); yield break; }
#if UNITY_EDITOR
            UnityEditor.ShaderUtil.allowAsyncCompilation = false;
#endif
            var outDir = Path.Combine(dir, "side");
            Directory.CreateDirectory(outDir);
            yield return M3Lab.OpenZone1();
            var dl = Object.FindAnyObjectByType<DayLight>();
            if (dl != null) dl.Apply(DayPart.Day, true);
            var bc = Object.FindAnyObjectByType<BikeCourse>(FindObjectsInactive.Include);
            Assert.IsNotNull(bc, "BikeCourse");
            bc.Bot = true;
            bool done = false;
            IEnumerator Run() { yield return bc.RunAll(); done = true; }
            bc.StartCoroutine(Run());
            var cam = Camera.main;
            var brain = cam.GetComponent<Unity.Cinemachine.CinemachineBrain>();
            var lines = new List<string>();
            int frame = 0, n = 0, budget = 2700;
            var lastMode = BikeRider.Mode.None; float modeAt = 0f, t0 = Time.time;
            var modeFrames = new Dictionary<BikeRider.Mode, int>();
            BikeRider rider = null;
            Vector3 camPos = Vector3.zero; bool camInit = false;
            float sideSign = 1f;
            while (!done && Time.time - t0 < 220f && n < budget)
            {
                yield return null;
                if (!bc.Riding || bc.Motor == null) continue;
                if (rider == null || !rider.Riding) rider = Object.FindObjectsByType<BikeRider>(FindObjectsSortMode.None).FirstOrDefault(r => r.Riding);
                if (rider == null) continue;
                if (rider.Current != lastMode) { lastMode = rider.Current; modeAt = Time.time; }
                // 상태마다 앞 4초(순항은 6초)만 — 같은 상태가 길게 이어지면 건너뜀
                int got = modeFrames.TryGetValue(rider.Current, out var g) ? g : 0;
                float window = rider.Current == BikeRider.Mode.Cruise ? 6f : rider.Current == BikeRider.Mode.Climb ? 8f : 4f;
                bool rec = Time.time - modeAt < window && got < (int)(window * 30f * 2f);
                var m = bc.Motor;
                var right = Quaternion.Euler(0f, m.Yaw + 90f, 0f) * Vector3.forward;
                // 옆 3.6m — 막히면(정류장·담) 반대쪽, 둘 다 막히면 막힌 곳 앞까지
                var eye = m.Position + Vector3.up * 1.15f;
                float Free(Vector3 dir) => Physics.SphereCast(eye, 0.25f, dir, out var h, 3.6f, Layers.Solid & ~(1 << Layers.Player), QueryTriggerInteraction.Ignore) ? h.distance - 0.1f : 3.6f;
                float fr = Free(right * sideSign), fl = Free(-right * sideSign);
                if (fr < 2.2f && fl > fr + 0.8f) { sideSign = -sideSign; camInit = false; float t = fr; fr = fl; fl = t; }
                right *= sideSign;
                float dist = Mathf.Max(1.6f, fr);
                var want = eye + right * dist - m.Forward * 0.25f;
                camPos = camInit ? Vector3.Lerp(camPos, want, 0.25f) : want; camInit = true;
                if (brain != null) brain.enabled = false;
                cam.transform.SetPositionAndRotation(camPos, Quaternion.LookRotation(m.Position + Vector3.up * 0.95f + m.Forward * 0.1f - camPos, Vector3.up));
                cam.fieldOfView = 42f;
                if (rec && frame++ % 2 == 0)
                {
                    var tex = Shots.Grab(cam, 1280, 720, 1, false);
                    File.WriteAllBytes(Path.Combine(outDir, $"f_{n++:00000}.jpg"), tex.EncodeToJPG(90));
                    Object.Destroy(tex);
                    modeFrames[rider.Current] = got + 1;
                    lines.Add($"{n - 1}	{rider.Current}	θ {m.Crank:F1}	기울기 {(bc.Bike != null ? bc.Bike.ClipLean : 0f):F1}	속도 {m.Speed:F2}	경사 {m.SlopeDeg:F1}	발 {rider.FootGapL * 100f:F1}/{rider.FootGapR * 100f:F1}cm");
                }
            }
            if (brain != null) brain.enabled = true;
            File.WriteAllLines(Path.Combine(dir, "side.txt"), lines);
            Debug.Log($"[M3Test] Z 자전거 측면 녹화: {n}장 · 상태 [{string.Join(", ", modeFrames.Select(kv => kv.Key + " " + kv.Value))}] → {outDir}");
        }
    }
}
