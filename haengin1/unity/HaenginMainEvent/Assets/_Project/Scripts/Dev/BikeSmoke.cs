// 행인1의 메인이벤트 — 배달 자전거 빌드 스모크(docs/09 2-6, 8단계): 실행 인자 -bikesmoke
// 타이틀 → 연습장 Zone1(이야기 없이) → 1화 새벽 배달 코스를 봇으로 끝까지(경비실·꼭대기 집 건네기, 태오 추월) → '[Bike] 스모크' 줄을 남기고 종료
using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Haengin
{
    public sealed class BikeSmoke : MonoBehaviour
    {
        static bool Arg(string a) => Environment.GetCommandLineArgs().Any(x => string.Equals(x, a, StringComparison.OrdinalIgnoreCase));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Application.isEditor || !Arg("-bikesmoke")) return;
            var go = new GameObject("자전거 스모크") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            go.AddComponent<BikeSmoke>();
        }

        IEnumerator Start()
        {
            Time.captureDeltaTime = 1f / 60f;
            float t0 = Time.realtimeSinceStartup;
            BikeCourse bc = null;
            for (int i = 0; i < 1800 && bc == null; i++) { bc = BikeCourse.Current ?? FindAnyObjectByType<BikeCourse>(FindObjectsInactive.Include); yield return null; }
            if (bc == null) { Debug.Log("[Bike] 스모크: 코스(BikeCourse) 없음 — 종료"); Application.Quit(); yield break; }
            yield return new WaitForSecondsRealtime(1.0f);
            foreach (var p in FindObjectsByType<PInput>(FindObjectsSortMode.None)) p.enabled = false;
            bc.gameObject.SetActive(true);
            bc.Bot = true;
            float g0 = Time.time;
            yield return bc.RunAll();
            float game = Time.time - g0;
            Debug.Log($"[Bike] 스모크: 코스 끝 — 게임 {game:F1}초 · 경비실 {(bc.Stops.Length > 0 && bc.Stops[0].Done)} · 꼭대기 집 {(bc.Stops.Length > 1 && bc.Stops[1].Done)} · " +
                      $"지각 {bc.LateMin}분 · 국물 {(bc.Soup != null ? bc.Soup.Value : -1):F0} · 태오 추월 오르막 {bc.UpPasses}·내리막 {bc.DownPasses} · 내림 {(bc.Motor != null ? bc.Motor.Dismounts : -1)}(끝 1) · " +
                      $"대사 [{string.Join(" / ", bc.Lines)}]");
            Debug.Log($"[Bike] 스모크: 끝 — {Time.realtimeSinceStartup - t0:F1}초 — 종료");
            Application.Quit();
        }
    }
}
