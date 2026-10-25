// 행인1의 메인이벤트 — 실내 장소 빌드 스모크(09 9단계, 2026-10-25 오픈월드 방향): 실행 인자 -stagesmoke
// 타이틀 → 연습장 Zone1 → Zone1 출입문 4곳(국밥집·하루편의점·집·혜성고 방면)을 차례로 써서 들어가고(접지·표시 자리 수) 안 문으로 나옴 → '[Stage] 스모크' 줄
using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Haengin
{
    public sealed class StageSmoke : MonoBehaviour
    {
        static bool Arg(string a) => Environment.GetCommandLineArgs().Any(x => string.Equals(x, a, StringComparison.OrdinalIgnoreCase));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Application.isEditor || !Arg("-stagesmoke")) return;
            var go = new GameObject("장소 스모크") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            go.AddComponent<StageSmoke>();
        }

        IEnumerator Start()
        {
            float t0 = Time.realtimeSinceStartup;
            SceneLoader loader = null;
            for (int i = 0; i < 1800 && (loader == null || loader.Motor == null); i++) { loader = FindAnyObjectByType<SceneLoader>(FindObjectsInactive.Include); yield return null; }
            if (loader == null) { Debug.Log("[Stage] 스모크: SceneLoader 없음 — 종료"); Application.Quit(); yield break; }
            yield return new WaitForSecondsRealtime(1.0f);
            var motor = loader.Motor;
            var outs = FindObjectsByType<StageDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(d => d.Target != SceneLoader.Zone1).OrderBy(d => d.Target).ToList();
            var parts = new System.Collections.Generic.List<string>();
            foreach (var d in outs)
            {
                motor.Teleport(d.ReturnPos, d.ReturnYaw);
                yield return new WaitForSecondsRealtime(0.3f);
                d.Use();
                for (int i = 0; i < 1200 && (loader.Busy || loader.Place != d.Target); i++) yield return null;
                yield return new WaitForSecondsRealtime(0.5f);
                bool inOk = loader.Place == d.Target;
                bool grounded = motor.Grounded;
                int markers = PlaceMarker.All.Count(m => m.Place == d.Target);
                var inner = loader.Stage != null ? loader.Stage.GetComponentsInChildren<StageDoor>(true).FirstOrDefault(x => x.Target == SceneLoader.Zone1) : null;
                if (inner != null) inner.Use();
                for (int i = 0; i < 1200 && (loader.Busy || loader.Place != SceneLoader.Zone1); i++) yield return null;
                yield return new WaitForSecondsRealtime(0.3f);
                float back = Vector3.Distance(motor.Position, d.transform.position);
                parts.Add($"{d.Target} 들어감 {inOk} · 접지 {grounded} · 표시 {markers} · 나옴 {(loader.Place == SceneLoader.Zone1 ? back.ToString("F1") + "m" : "실패")}");
            }
            Debug.Log($"[Stage] 스모크: 문 {outs.Count} — {string.Join(" | ", parts)}");
            Debug.Log($"[Stage] 스모크: 끝 — {Time.realtimeSinceStartup - t0:F1}초 — 종료");
            Application.Quit();
        }
    }
}
