// 행인1의 메인이벤트 — M3 테스트 공용(docs/09_M3_버티컬슬라이스_설계.md 6-2): Zone1 을 Additive 로 열고 이야기 루트를 잡는다,
// 사람 입력(PInput·CamInput) 끔, 저장 폴더를 임시 한글 폴더로, 정리 때 무대 장면·이야기·입력 맵을 되돌린다.
// 입력 테스트(InputTestFixture)보다 뒤에 돌게 클래스 이름을 'M3…'(I 다음)로 둔다 — 실제 HInput 이 켜지면 가짜 장치 입력이 막히므로(08 11-3).
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Haengin.Tests
{
    public static class M3Lab
    {
        public const float Dt = Lab.Dt;
        public static StoryRunner Runner;
        public static PlayerMotor Motor;
        public static CamRig Cam;

        public static string SaveDir => Path.Combine(Application.temporaryCachePath, "M3 시험 저장 — 한글 경로");

        public static IEnumerator OpenZone1()
        {
            Time.captureDeltaTime = Dt;
            TimeFx.Reset();
            GameState.SetPaused(false);
            GameState.Modal = GameState.InputLocked = false;
            Encounter.Suppress = true;
            StoryRunner.Suppress = false;
            SaveStore.Dir = SaveDir;
            Directory.CreateDirectory(SaveDir);
            yield return UnloadAll();
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ZoneTests.ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            var zone = SceneManager.GetSceneByPath(ZoneTests.ScenePath);
            if (!zone.IsValid() || !zone.isLoaded) { Assert.Ignore("Zone1 장면 없음"); yield break; }
            SceneManager.SetActiveScene(zone);
            yield return null; yield return null;
            Runner = Object.FindAnyObjectByType<StoryRunner>();
            Motor = Object.FindAnyObjectByType<PlayerMotor>();
            Cam = Object.FindAnyObjectByType<CamRig>();
            Assert.NotNull(Runner, "Zone1 에 이야기 루트(StoryRunner)가 없음 — StorySetup.Build 를 돌릴 것");
            Assert.NotNull(Runner.Def, "StoryDef");
            foreach (var p in Object.FindObjectsByType<PInput>(FindObjectsSortMode.None)) p.enabled = false;
            foreach (var c in Object.FindObjectsByType<CamInput>(FindObjectsSortMode.None)) c.enabled = false;
            Runner.AutoSave = true;
            yield return Lab.Frames(10);
        }

        /// 테스트가 연 장면(Zone1·무대·타이틀·Lab_*)을 내린다(테스트 실행기 장면은 남김)
        public static IEnumerator UnloadAll()
        {
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var o = SceneManager.GetSceneAt(i);
                if (!o.isLoaded) continue;
                if (o.name.StartsWith("St_") || o.name == "Title") yield return SceneManager.UnloadSceneAsync(o);
            }
            yield return Lab.UnloadOurs(default);
        }

        public static IEnumerator TearDown()
        {
            if (Runner != null)
            {
                Runner.Bot = StoryRunner.BotMode.None;
                Runner.BotAnswer = null;
                Runner.Stop();
                yield return null; yield return null;
                if (Runner.Actions != null) Runner.Actions.Disable();
            }
            foreach (var p in Object.FindObjectsByType<PInput>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (p.Actions != null) p.Actions.Disable();
            GameState.Modal = GameState.InputLocked = false;
            GameUi.RetryHook = GameUi.SurrenderHook = GameUi.BookHook = GameUi.TitleHook = null;
            GameUi.SubOpen = null; GameUi.SubClose = null;
            GameUi.HintOverride = null;
            TimeFx.Reset();
            InkMode.Set(0f);
            GameState.SetPaused(false);
            SaveStore.Dir = SaveDir;
            SaveStore.FailBeforeSwap = false;
            Runner = null; Motor = null; Cam = null;
            yield return UnloadAll();
            yield return Lab.FreshScene();
            Time.captureDeltaTime = 0f;
        }

        /// 이야기가 그 장면에 들어와 전환이 끝날 때까지(최대 frames)
        public static IEnumerator WaitScene(string id, int frames = 1200)
        {
            for (int i = 0; i < frames; i++)
            {
                if (Runner.Scene != null && Runner.Scene.Id == id && Runner.InScene && (Runner.Loader == null || !Runner.Loader.Busy)) yield break;
                yield return null;
            }
            Assert.Fail($"장면 {id} 에 {frames}프레임 안에 들어오지 못함(지금 {Runner.Scene?.Id})");
        }

        public static IEnumerator WaitDone(int frames)
        {
            for (int i = 0; i < frames && !Runner.Finished; i++) yield return null;
        }

        public static string[] LoadedStages() =>
            Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(s => s.isLoaded && s.name.StartsWith("St_")).Select(s => s.name).ToArray();
    }
}
