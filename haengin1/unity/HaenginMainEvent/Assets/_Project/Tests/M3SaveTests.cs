// 행인1의 메인이벤트 — M3 5단계 테스트 D11 · D12 (docs/09_M3_버티컬슬라이스_설계.md 6-2)
//   D11 Save_RoundTrip : 저장 → 불러오기(필드 전부 같음) / 깨진 파일 · 모르는 버전 → 경고 + 이전 파일 유지 / 쓰다 끊김 → 이전 파일 그대로 /
//                        한글 경로(임시 한글 폴더 + 실제 persistentDataPath 의 '행인1의 메인이벤트' 폴더) 정상 / 타이틀이 깨진 저장에 경고 → '이전 자동 저장'
//   D12 Save_Resume    : 장면마다 자동 저장 → 그 저장으로 다시 시작 → 장면 첫 프레임 상태(가계부·도감·플래그·시각·장소·시우 자리) = 처음 왔을 때, 타이틀 '이어하기' 길
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Haengin.Tests
{
    public sealed class M3SaveTests
    {
        [UnityTearDown] public IEnumerator TearDown() { TitleMenu.QuitHook = null; StoryBoot.LoadBase = p => SceneManager.LoadScene(p, LoadSceneMode.Single); StoryBoot.Request = null; return M3Lab.TearDown(); }

        static SaveData Sample()
        {
            var d = new SaveData { SceneId = "4-3", Month = 3, Day = 14, Minutes = 15 * 60 + 2.5, Label = "3월 14일(토) 15:00 · 현장 — 정산" };
            d.Ledger.Apply("ledger", "+120000:일당 → 관리비 고지서 밑");
            d.Ledger.Apply("envelope", "+5000");
            d.Ledger.Apply("item", "폐기 직전 삼각김밥:2");
            d.Names.Got.AddRange(new[] { "국밥", "어이", "자전거", "행인1" });
            d.Flags.Endured = 2; d.Flags.EnduredWillingly = 1; d.Flags.Haneul = 3; d.Flags.BraidTries = 3;
            d.Flags.Raise("퀘스트_하늘생일"); d.Flags.Choices.Add("1-7.04=2");
            return d;
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator D11_Save_RoundTrip()
        {
            yield return null;
            var log = new StringBuilder();
            string dir = M3Lab.SaveDir;
            SaveStore.Dir = dir;
            Directory.CreateDirectory(dir);
            foreach (var s in new[] { SaveStore.Auto, SaveStore.Manual }) SaveStore.Delete(s);
            try
            {
                // ① 저장 → 불러오기
                var a = Sample();
                SaveStore.Write(SaveStore.Auto, a);
                var b = SaveStore.Read(SaveStore.Auto, out string e1);
                Assert.IsNull(e1, e1);
                Assert.IsTrue(a.SameAs(b), "필드 전부 같음\n" + a.ToJson() + "\n" + b?.ToJson());
                StringAssert.Contains("한글", SaveStore.PathOf(SaveStore.Auto));
                log.AppendLine($"① 왕복 같음 · {SaveStore.PathOf(SaveStore.Auto)} · {new FileInfo(SaveStore.PathOf(SaveStore.Auto)).Length}바이트");

                // ② 두 번째 저장 → 이전 파일이 .prev 로
                var a2 = Sample(); a2.SceneId = "4-4"; a2.Ledger.Apply("envelope", "+5000");
                SaveStore.Write(SaveStore.Auto, a2);
                var prev = SaveStore.ReadPrev(SaveStore.Auto, out _);
                Assert.IsTrue(a.SameAs(prev), "이전 자동 저장 = 첫 저장");
                Assert.IsTrue(a2.SameAs(SaveStore.Read(SaveStore.Auto, out _)), "새 저장");

                // ③ 쓰다 끊김(임시 파일까지 쓰고 바꿔 넣기 전) → 이전 파일 그대로
                SaveStore.FailBeforeSwap = true;
                var a3 = Sample(); a3.SceneId = "5-1";
                Assert.Throws<IOException>(() => SaveStore.Write(SaveStore.Auto, a3));
                SaveStore.FailBeforeSwap = false;
                Assert.IsTrue(a2.SameAs(SaveStore.Read(SaveStore.Auto, out _)), "끊겨도 이전 파일 그대로");
                Assert.IsTrue(File.Exists(SaveStore.PathOf(SaveStore.Auto) + ".tmp"), "임시 파일만 남음");
                log.AppendLine("② .prev 유지 · ③ 쓰다 끊김 → 이전 파일 그대로(임시 파일만 남음)");

                // ④ 깨진 파일 → 오류 + 이전 파일로
                File.WriteAllText(SaveStore.PathOf(SaveStore.Auto), "{\"Version\":1,\"SceneId\":\"4-4\",\"Ledger\":{\"Wal");
                var c = SaveStore.Read(SaveStore.Auto, out string e2);
                Assert.IsNull(c); StringAssert.StartsWith("깨짐", e2);
                File.WriteAllText(SaveStore.PathOf(SaveStore.Auto), "이건 저장 파일이 아니다");
                Assert.IsNull(SaveStore.Read(SaveStore.Auto, out string e2b)); StringAssert.StartsWith("깨짐", e2b);
                Assert.IsNotNull(SaveStore.ReadPrev(SaveStore.Auto, out _), "이전 자동 저장은 남음");
                // ⑤ 모르는 버전
                var v = Sample(); v.Version = 99;
                File.WriteAllText(SaveStore.PathOf(SaveStore.Manual), JsonUtility.ToJson(v));
                Assert.IsNull(SaveStore.Read(SaveStore.Manual, out string e3));
                Assert.AreEqual("모르는 버전 99", e3);
                log.AppendLine($"④ 깨짐: '{e2}' · '{e2b}' · ⑤ '{e3}'");

                // ⑥ 타이틀: 깨진 자동 저장 → '이어하기 — 저장 파일 문제' → 경고 쪽 → '이전 자동 저장'
                SaveStore.Delete(SaveStore.Manual);
                yield return M3Lab.UnloadAll();
                string loaded = null;
                StoryBoot.LoadBase = p => loaded = p;
#if UNITY_EDITOR
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode(StoryBoot.TitleScene, new LoadSceneParameters(LoadSceneMode.Additive));
#endif
                yield return Lab.Frames(5);
                var title = Object.FindAnyObjectByType<TitleMenu>();
                Assert.NotNull(title, "타이틀 장면");
                var labels = Enumerable.Range(0, title.Count).Select(title.Label).ToList();
                log.AppendLine($"⑥ 타이틀 항목 [{string.Join(" / ", labels)}]");
                Assert.IsTrue(title.Choose("이어하기 — 저장 파일 문제"), "깨진 저장 경고 항목");
                yield return null;
                Assert.AreEqual("저장 파일을 읽지 못했습니다", title.PageName);
                StringAssert.StartsWith("깨짐", title.LastError);
                var warn = Enumerable.Range(0, title.Count).Select(title.Label).ToList();
                Assert.IsTrue(title.Choose("이전 자동 저장"), "이전 자동 저장 항목: " + string.Join("/", warn));
                Assert.AreEqual(StoryBoot.Zone1Scene, loaded, "Zone1 을 연다");
                Assert.AreEqual(StoryBoot.Mode.Continue, StoryBoot.Request.Mode);
                Assert.IsTrue(a.SameAs(StoryBoot.Request.Save), "이전 자동 저장으로 이어하기");
                log.AppendLine($"   경고 쪽 [{string.Join(" / ", warn)}] → 이어하기 {StoryBoot.Request.Save.SceneId}");
                StoryBoot.Request = null;

                // ⑦ 실제 저장 폴더(회사·제품 이름 — 한글)
                SaveStore.Dir = null;
                string real = SaveStore.Folder;
                var t = Sample(); t.SceneId = "D11";
                SaveStore.Write("d11", t);
                Assert.IsTrue(t.SameAs(SaveStore.Read("d11", out _)), "실제 폴더 왕복");
                SaveStore.Delete("d11");
                log.AppendLine($"⑦ 실제 폴더 {real} 왕복 정상(시험 칸 d11 지움)");
                StringAssert.Contains("행인1의 메인이벤트", real, "제품 이름 폴더");
            }
            finally
            {
                SaveStore.FailBeforeSwap = false;
                SaveStore.Dir = dir;
                foreach (var s in new[] { SaveStore.Auto, SaveStore.Manual }) SaveStore.Delete(s);
            }
            Debug.Log("[M3Test] D11 Save_RoundTrip\n" + log);
        }

        [UnityTest, Timeout(1800000)]
        public IEnumerator D12_Save_Resume()
        {
            yield return M3Lab.OpenZone1();
            var r = M3Lab.Runner;
            var first = new Dictionary<string, (SaveData save, string place, Vector3 pos)>();
            void On(SceneDef s) => first[s.Id] = (r.SceneStart.Clone(), r.Loader.Place, M3Lab.Motor.Position);
            r.SceneStarted += On;
            r.Bot = StoryRunner.BotMode.Fast;
            r.Begin(false, null, null);
            yield return M3Lab.WaitDone(60 * 60 * 6);
            r.SceneStarted -= On;
            Assert.IsTrue(r.Finished);
            r.Stop();
            yield return Lab.Frames(5);
            // 마지막 자동 저장 = 마지막 장면 시작
            var auto = SaveStore.Read(SaveStore.Auto, out string err);
            Assert.IsNull(err, err);
            Assert.AreEqual(r.Def.Scenes.Last().Id, auto.SceneId, "자동 저장 = 마지막 장면");

            var log = new StringBuilder();
            var fails = new List<string>();
            r.OneScene = true;
            foreach (var s in r.Def.Scenes)
            {
                var (save, place, pos) = first[s.Id];
                SaveData again = null; string place2 = null; Vector3 pos2 = default;
                void On2(SceneDef x) { again = r.SceneStart.Clone(); place2 = r.Loader.Place; pos2 = M3Lab.Motor.Position; }
                r.SceneStarted += On2;
                r.Bot = StoryRunner.BotMode.Fast;
                r.Begin(save.Test, save.SceneId, save);
                int f = 0;
                while (!r.Finished && f < 60 * 60) { f++; yield return null; }
                r.SceneStarted -= On2;
                r.Stop();
                yield return Lab.Frames(4);
                bool same = save.SameAs(again);
                bool where = s.Kind == SceneKind.Card || (place == place2 && Vector3.Distance(pos, pos2) < 0.05f);   // 카드는 장소를 바꾸지 않음(앞 장면 자리)
                if (!same) fails.Add($"{s.Id} 상태 다름\n  처음: {save.ToJson()}\n  다시: {again?.ToJson()}");
                if (!where) fails.Add($"{s.Id} 자리 다름 처음 {place} {pos} / 다시 {place2} {pos2}");
                log.Append($"{s.Id}{(same && where ? "" : "✗")} ");
            }
            r.OneScene = false;

            // 타이틀 '이어하기' 길: 수동 칸이 더 새것이면 그것
            yield return M3Lab.UnloadAll();
            var man = first["3-2"].save.Clone();
            SaveStore.Write(SaveStore.Manual, man);
            string loaded = null;
            StoryBoot.LoadBase = p => loaded = p;
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(StoryBoot.TitleScene, new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return Lab.Frames(5);
            var title = Object.FindAnyObjectByType<TitleMenu>();
            Assert.NotNull(title);
            Assert.IsTrue(title.Choose("이어하기"), "이어하기 항목");
            Assert.AreEqual(StoryBoot.Zone1Scene, loaded);
            Assert.AreEqual("3-2", StoryBoot.Request.Save.SceneId, "더 새것(수동 칸)");
            StoryBoot.Request = null;
            Debug.Log($"[M3Test] D12 Save_Resume: 장면 {r.Def.Scenes.Length}개 다시 시작 · 다름 {fails.Count}\n{log}\n타이틀 이어하기 → {loaded} · 3-2");
            Assert.IsEmpty(fails, string.Join("\n", fails));
        }
    }
}
