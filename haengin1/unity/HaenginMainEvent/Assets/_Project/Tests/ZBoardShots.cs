// 행인1의 메인이벤트 — 스토리보드 카드 사진(-boardshots <폴더>, 그래픽 필요): 1막 소개 · 자리 장면(1-5) · 조작 장면 소개(1-2) — board_*.png
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Haengin.Tests
{
    public sealed class ZBoardShots
    {
        [UnityTearDown] public IEnumerator TearDown() => M3Lab.TearDown();

        [UnityTest, Timeout(900000)]
        public IEnumerator Z_BoardShots()
        {
            string dir = Shots.Arg("-boardshots");
            if (string.IsNullOrEmpty(dir)) { Assert.Ignore("-boardshots <폴더> 를 줄 때만"); yield break; }
            if (!Shots.Graphics) { Assert.Ignore("그래픽 장치 없음"); yield break; }
            Directory.CreateDirectory(dir);
            yield return M3Lab.OpenZone1();
            var r = M3Lab.Runner;
            r.Bot = StoryRunner.BotMode.None;
            r.Begin(false, null, null);
            yield return Lab.Frames(30);
            var cam = Camera.main;
            Shots.Shot(cam, Path.Combine(dir, "board_intro.png"), 1920, 1080, 1, true);
            r.Stop(); yield return Lab.Frames(5);
            r.Begin(false, "3-2", null);
            yield return M3Lab.WaitScene("3-2");
            yield return Lab.Frames(30);
            Shots.Shot(Camera.main, Path.Combine(dir, "board_3-2.png"), 1920, 1080, 1, true);
            r.Stop(); yield return Lab.Frames(5);
            r.Begin(false, "1-2", null);
            yield return M3Lab.WaitScene("1-2");
            yield return Lab.Frames(30);
            Shots.Shot(Camera.main, Path.Combine(dir, "board_1-2.png"), 1920, 1080, 1, true);
            Debug.Log("[M3Test] Z 스토리보드 사진 3장 → " + dir);
        }
    }
}
