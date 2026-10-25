// 행인1의 메인이벤트 — 8단계 눈 확인(-s8shots <폴더>, 그래픽 필요): 1화 새벽 배달 코스(1-2~1-4) 봇 주행 녹화
//   <폴더>/run/f_0000.jpg … (1280×720, 3프레임마다 = 20fps — 밖에서 ffmpeg 로 무음 mp4) + 장면 사진 s8_*.png + notes_s8.txt
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
    }
}
