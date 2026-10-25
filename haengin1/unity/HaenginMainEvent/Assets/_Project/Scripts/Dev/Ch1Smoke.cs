// 행인1의 메인이벤트 — 1장 오픈월드 빌드 스모크(docs/13 12-4 11단계 · W15 '-worldsmoke' → 대표 지시 이름 -ch1smoke): 실행 인자 -ch1smoke [-ch1straight]
// 타이틀 → '새 게임' → 1장 오픈월드 → 봇(WorldBot)이 3/3 부터 데모 끝 카드(5/8 · 목표 판정)까지 → '[World] 스모크' 줄 → 종료(창·소리 없음, 시간 1/60 고정).
// 기본 = 전부(체육관 체험·청소 관원·드릴·사연 2·시비·알바 4종), -ch1straight = 직진.
using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Haengin
{
    public sealed class Ch1Smoke : MonoBehaviour
    {
        static bool Arg(string a) => Environment.GetCommandLineArgs().Any(x => string.Equals(x, a, StringComparison.OrdinalIgnoreCase));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Application.isEditor || !Arg("-ch1smoke")) return;
            var go = new GameObject("1장 스모크") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            go.AddComponent<Ch1Smoke>();
        }

        IEnumerator Start()
        {
            Time.captureDeltaTime = 1f / 60f;
            bool full = !Arg("-ch1straight");
            float t0 = Time.realtimeSinceStartup;
            SaveStore.Dir = System.IO.Path.Combine(Application.temporaryCachePath, "ch1smoke");
            WorldRunner.BotPreset = w =>
            {
                w.BotSkill = 0.9f; w.BotDelegate = true; w.BotRide = false; w.BotRead = 0.05f;
                w.BotChoice = c => c.Id == "N02" ? (full ? 1 : 2) : 0;
            };
            TitleMenu title = null;
            for (int i = 0; i < 1200 && title == null; i++) { title = FindAnyObjectByType<TitleMenu>(); yield return null; }
            if (title == null) { Debug.Log("[World] 스모크: 타이틀 없음 — 종료"); Application.Quit(); yield break; }
            yield return null;
            bool ok = title.Choose("새 게임");
            WorldRunner w = null;
            for (int i = 0; i < 3600 && (w == null || !w.Active); i++) { w = WorldRunner.Instance; yield return null; }
            Debug.Log($"[World] 스모크: 타이틀 → 새 게임 {ok} · 세계 {(w != null && w.Active ? "시작" : "없음")} · {(full ? "전부" : "직진")}");
            if (w == null || !w.Active) { Application.Quit(); yield break; }
            var bot = WorldBot.Attach(w, full);
            bot.MaxSeconds = 2400f;
            yield return bot.Run();
            foreach (var l in bot.Log) Debug.Log("[World] 스모크: " + l);
            var m = w.Model;
            Debug.Log($"[World] 스모크: 상태 {m.Status}");
            Debug.Log($"[World] 스모크: 목표 — " + string.Join(" / ", w.Def.Chapter("1").DemoGoals.Select(m.GoalLine)));
            Debug.Log($"[World] 스모크: 카드 {m.S.Cards.Count(c => c.State > 0)}장 끝 · 맡기기 {w.AutoRuns}번 · 저장 {w.Saves} · 자취 {w.Trace.Count}");
            Debug.Log($"[World] 스모크: 끝 — {(bot.Done ? "데모 끝 " + (m.S.DemoState == 1 ? "목표 달성" : "목표 미달") : "실패 " + bot.Fail)} · 실제 {Time.realtimeSinceStartup - t0:F1}초 — 종료");
            Application.Quit();
        }
    }
}
