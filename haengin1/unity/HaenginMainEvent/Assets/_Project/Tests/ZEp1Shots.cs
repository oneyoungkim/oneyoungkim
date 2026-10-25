// 행인1의 메인이벤트 — 1화 「출석부에 없는 이름」 사진(-ep1shots <폴더>, 그래픽 필요, 2026-10-25 1화 채우기)
//   1-1 오프닝 컷(엄마 모델) 4장 · 1-5 시우네(하늘·땋기·아빠 사진) · 1-6 교실(뒷문·도경민·태오 엄지 척·민재·회상 흑백) — ep1_*.jpg
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Haengin.Tests
{
    public sealed class ZEp1Shots
    {
        [UnityTearDown] public IEnumerator TearDown() => M3Lab.TearDown();

        static readonly HashSet<string> Lines = new HashSet<string>
        {
            "1-5.01", "1-5.04", "1-5.08", "1-5.12", "1-5.20", "1-5.24",
            "1-6.01", "1-6.02", "1-6.06", "1-6.07", "1-6.10", "1-6.13", "1-6.14", "1-6.15", "1-6.18", "1-6.21", "1-6.24",
        };

        [UnityTest, Timeout(1800000)]
        public IEnumerator Z_Ep1Shots()
        {
            string dir = Shots.Arg("-ep1shots");
            if (string.IsNullOrEmpty(dir)) { Assert.Ignore("-ep1shots <폴더> 를 줄 때만"); yield break; }
            if (!Shots.Graphics) { Assert.Ignore("그래픽 장치 없음"); yield break; }
            Directory.CreateDirectory(dir);
            yield return M3Lab.OpenZone1();
            var r = M3Lab.Runner;
            r.Bot = StoryRunner.BotMode.Read;
            int n = 0;
            // 1-1 오프닝 컷: 2·8·14·20초
            r.OneScene = true;
            r.Begin(false, "1-1", null);
            yield return M3Lab.WaitScene("1-1");
            float t = 0f; int k = 0; float[] at = { 2f, 8f, 14f, 20f };
            while (!r.Finished && k < at.Length)
            {
                t += Time.deltaTime;
                if (t >= at[k]) { Shots.Shot(Camera.main, Path.Combine(dir, $"ep1_1-1_{k}.jpg"), 1280, 720, 1, true); k++; n++; }
                yield return null;
            }
            r.Stop(); yield return Lab.Frames(5);
            // 1-5 → 1-6 (1-7 이 시작하면 멈춤)
            r.OneScene = false;
            r.Begin(false, "1-5", null);
            yield return M3Lab.WaitScene("1-5");
            string last = ""; int wait = -1; string pend = null; int braid = 0; bool ink = false;
            for (int i = 0; i < 60 * 600 && !r.Finished; i++)
            {
                if (r.Scene != null && r.Scene.Id == "1-7") break;
                var cur = r.Talk != null ? r.Talk.Current : null;
                string id = cur != null ? cur.Id : "";
                if (id != last) { last = id; if (Lines.Contains(id)) { pend = id; wait = 24; } }
                if (wait > 0 && --wait == 0 && pend != null) { Shots.Shot(Camera.main, Path.Combine(dir, $"ep1_{pend}.jpg"), 1280, 720, 1, true); n++; pend = null; }
                if (r.Braid != null && r.Braid.Shown && braid < r.Braid.Plays && r.Braid.Plays <= 3)
                {
                    braid = r.Braid.Plays;
                    for (int f = 0; f < 200; f++) yield return null;   // 박자 몇 개 지난 뒤
                    Shots.Shot(Camera.main, Path.Combine(dir, $"ep1_braid{braid}.jpg"), 1280, 720, 1, true); n++;
                }
                if (!ink && InkMode.Amount > 0.9f) { ink = true; for (int f = 0; f < 40; f++) yield return null; Shots.Shot(Camera.main, Path.Combine(dir, "ep1_1-6_flash.jpg"), 1280, 720, 1, true); n++; }
                yield return null;
            }
            Debug.Log($"[M3Test] Z 1화 사진 {n}장 → {dir}");
        }
    }
}
