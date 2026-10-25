// 행인1의 메인이벤트 — 무대 4곳 키트 사진(-stageshots <폴더>, 그래픽 필요, 09 9단계 2026-10-25): 목표 그림(concept/art/env/stages/frames/s0*.png)과 같은 자리
// 키트 소켓 Sock_Cam_s01~s08 에 카메라를 놓고 찍음 → stage_s0*.png. 비교 시트는 tools/env/stages/compose_compare.py
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Haengin.Tests
{
    public sealed class ZStageShots
    {
        [UnityTearDown] public IEnumerator TearDown() => M3Lab.TearDown();

        static readonly (string key, string[] cams)[] Stages =
        {
            ("Gukbap", new[] { "s01", "s02" }), ("Home", new[] { "s03", "s04" }), ("Conv", new[] { "s05", "s06" }), ("School", new[] { "s07", "s08" }),
        };

        [UnityTest, Timeout(1200000)]
        public IEnumerator Z_StageShots()
        {
            string dir = Shots.Arg("-stageshots");
            if (string.IsNullOrEmpty(dir)) { Assert.Ignore("-stageshots <폴더> 를 줄 때만"); yield break; }
            if (!Shots.Graphics) { Assert.Ignore("그래픽 장치 없음"); yield break; }
            Directory.CreateDirectory(dir);
            yield return M3Lab.OpenZone1();
            var loader = M3Lab.Runner.Loader;
            int n = 0;
            foreach (var (key, cams) in Stages)
            {
                yield return loader.Go(key, "Door", false, Vector3.zero, 0f, false);
                yield return Lab.Frames(30);
                var place = StagePlace.Find(key);
                Assert.IsNotNull(place, key);
                var cam = Camera.main;
                var brain = cam.GetComponent<Unity.Cinemachine.CinemachineBrain>();
                if (brain != null) brain.enabled = false;
                // 시우는 숨김(문 앞 스폰이 카메라 자리와 겹침 — 목표 그림의 시우는 비교에서 뺌)
                foreach (var r in M3Lab.Motor.GetComponentsInChildren<Renderer>()) r.enabled = false;
                foreach (var c in cams)
                {
                    Transform t = null;
                    foreach (var tr in place.GetComponentsInChildren<Transform>(true)) if (tr.name == "Sock_Cam_" + c) { t = tr; break; }
                    Assert.IsNotNull(t, "Sock_Cam_" + c);
                    cam.transform.SetPositionAndRotation(t.position, t.rotation);
                    cam.fieldOfView = 48f; cam.nearClipPlane = 0.05f;
                    yield return Lab.Frames(3);
                    Shots.Shot(cam, Path.Combine(dir, $"stage_{c}.png"), 1600, 900, 1, false);
                    n++;
                }
                if (brain != null) brain.enabled = true;
            }
            Debug.Log($"[M3Test] Z 무대 사진 {n}장 → {dir}");
        }
    }
}
