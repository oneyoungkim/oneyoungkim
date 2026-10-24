// 행인1의 메인이벤트 — 체육관 빌드 스모크(docs/12_체육관_설계.md 8장 7단계): 실행 인자 -gymsmoke
// 타이틀 → 연습장 → 체육관 → D01 줄넘기 봇 S → 시뮬 1회 → '[Gym] 스모크' 줄을 Player.log 에 남기고 종료(시간 1/60 고정, 창·소리 없음)
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Haengin
{
    public sealed class GymSmoke : MonoBehaviour
    {
        static bool Arg(string a) => Environment.GetCommandLineArgs().Any(x => string.Equals(x, a, StringComparison.OrdinalIgnoreCase));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Application.isEditor || !Arg("-gymsmoke")) return;
            var go = new GameObject("체육관 스모크") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            go.AddComponent<GymSmoke>();
        }

        IEnumerator Start()
        {
            Time.captureDeltaTime = 1f / 60f;
            float t0 = Time.realtimeSinceStartup;
            TitleMenu title = null;
            for (int i = 0; i < 600 && title == null; i++) { title = FindAnyObjectByType<TitleMenu>(); yield return null; }
            if (title == null) { Debug.Log("[Gym] 스모크: 타이틀 없음 — 종료"); Application.Quit(); yield break; }
            yield return null;
            bool a = title.Choose("연습장"); yield return null;
            bool b = title.Choose("체육관");
            Debug.Log($"[Gym] 스모크: 타이틀 → 연습장 {a} → 체육관 {b}");
            GymManager gym = null;
            for (int i = 0; i < 1800 && gym == null; i++) { gym = GymManager.Instance; yield return null; }
            if (gym == null) { Debug.Log("[Gym] 스모크: 체육관이 열리지 않음 — 종료"); Application.Quit(); yield break; }
            yield return new WaitForSecondsRealtime(1.0f);
            Debug.Log($"[Gym] 스모크: 체육관 열림 · 장면 {string.Join(",", Enumerable.Range(0, SceneManager.sceneCount).Select(i => SceneManager.GetSceneAt(i).name))} · {GymLights.Name(gym.Part)} · 마커 {gym.Markers.Count}");
            gym.SimMontageSec = 1.5f;
            DrillResult res = null; DrillReward rew = default;
            gym.DrillFinished += (d, r, w) => { res = r; rew = w; };
            yield return gym.PlayBot("D01", 1f);
            Debug.Log($"[Gym] 스모크: D01 봇 → {(res != null ? res.ToString() : "결과 없음")} · {rew}");
            bool sOk = res != null && res.Grade == Grade.S;
            bool simDone = false;
            if (sOk)
            {
                yield return gym.SimCo(gym.Book.Find("D01"));
                for (int i = 0; i < 300 && gym.Busy; i++) yield return null;
                simDone = GymState.Current.SimToday >= 1;
                Debug.Log($"[Gym] 스모크: 시뮬 → {rew} · {GymState.Current.Summary}");
            }
            Debug.Log($"[Gym] 스모크: 끝 — S {sOk} · 시뮬 {(simDone ? "완료" : "건너뜀")} · {Time.realtimeSinceStartup - t0:F1}초 — 종료");
            Application.Quit();
        }
    }
}
