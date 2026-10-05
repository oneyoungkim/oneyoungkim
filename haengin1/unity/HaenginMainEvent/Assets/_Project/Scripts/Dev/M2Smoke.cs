// 행인1의 메인이벤트 — M2 빌드 실행 확인(docs/08_M2_전투_설계.md 11장 16단계)
// 늘: 장면이 뜨고 3.5초 뒤 '[M2] 실행 확인' 한 줄(인카운터·야차·전투 부품·접근성)을 Player.log 에.
// 실행 인자 -m2smoke 면(빌드를 -batchmode -nographics 로 띄워 소리·창 없이): 인카운터 Y4 를 봇으로 한 판(걸어 들어가 시비 → 승리 → 결과 → 탐색),
// 야차 Y1 을 한 판(입장 → 승 → 탐색) 돌며 단계마다 '[M2] 스모크' 줄을 남기고 끝낸다(시우 무적, 시간 1/60 고정).
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Haengin
{
    public sealed class M2Smoke : MonoBehaviour
    {
        static bool Arg(string a) => Environment.GetCommandLineArgs().Any(x => string.Equals(x, a, StringComparison.OrdinalIgnoreCase));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Application.isEditor) return;
            var go = new GameObject("M2 실행 확인") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            go.AddComponent<M2Smoke>();
        }

        IEnumerator Start()
        {
            bool smoke = Arg("-m2smoke");
            if (smoke) Time.captureDeltaTime = 1f / 60f;
            yield return new WaitForSecondsRealtime(3.5f);
            var enc = FindAnyObjectByType<Encounter>();
            var ya = FindAnyObjectByType<Yacha>();
            var pc = FindAnyObjectByType<PlayerCombat>();
            var mode = FindAnyObjectByType<CombatMode>();
            var fx = FindAnyObjectByType<CombatFx>();
            var hud = FindAnyObjectByType<CombatHud>();
            Debug.Log($"[M2] 실행 확인: 장면 {SceneManager.GetActiveScene().name} · 인카운터 {(enc != null ? enc.Def.Title + " 적 " + enc.Brains.Count + " " + enc.State : "없음")} · " +
                      $"야차 {(ya != null ? ya.Def.Title + " 심판 형 " + (ya.Referee != null) : "없음")} · 전투 {(pc != null ? "PlayerCombat" : "-")}·{(mode != null ? "CombatMode" : "-")}·{(fx != null && fx.Kit != null ? "이펙트" : "-")}·{(hud != null ? "HUD" : "-")} · " +
                      $"흔들림 줄이기 {Accessibility.Reduced} · 배치 {Application.isBatchMode} · 소리 {AudioListener.volume:F1}");
            if (!smoke || enc == null || pc == null) yield break;

            float t0 = Time.time;
            pc.Me.DebugInvuln = true;
            // 실제 입력(PInput)은 끈다 — 켜 두면 매 프레임 '입력 없음'으로 이동·스틱을 덮어써 걸어 들어가지 못함(테스트와 같은 방식)
            var pin = FindAnyObjectByType<PInput>();
            if (pin != null) pin.enabled = false;
            var d = enc.Def;
            (pc.Me.Body as PlayerBody)?.Place(d.ToWorld(d.RetryLocal) + Vector3.up * 0.1f, d.Yaw + 180f);
            var motor = pc.Motor;
            for (int i = 0; i < 900 && enc.State == Encounter.Phase.Armed; i++)
            {
                var goal = d.ToWorld(new Vector2(1.5f, -1.0f));
                motor.SetMoveInput(HitResolver.Flat(goal - motor.Position).normalized, 1f, false);
                yield return null;
            }
            motor.ClearMoveInput();
            Debug.Log($"[M2] 스모크: 인카운터 {enc.State} ({Time.time - t0:F1}초)");
            var bot = FightBot.On(pc, false);
            float w = 0f;
            while (enc.State != Encounter.Phase.Cleared && w < 200f) { yield return null; w += Time.deltaTime; }
            Debug.Log($"[M2] 스모크: 인카운터 끝 {enc.State} · 승 {enc.Wins} · 탈락 {enc.Eliminated}/{enc.Brains.Count} · 전투 {w:F1}초 · 탐색 {(!mode.Active)} · 시우 HP {pc.Me.Hp}");
            if (ya != null)
            {
                ya.Enter();
                w = 0f;
                while (ya.State != Yacha.Phase.Fight && w < 15f) { yield return null; w += Time.deltaTime; }
                Debug.Log($"[M2] 스모크: 야차 {ya.State} (입장 {w:F1}초)");
                w = 0f;
                int foeMin = int.MaxValue;
                while (ya.State == Yacha.Phase.Fight && w < 200f) { yield return null; w += Time.deltaTime; if (ya.Foe != null) foeMin = Mathf.Min(foeMin, ya.Foe.Me.Hp); }
                while (ya.State == Yacha.Phase.End && w < 220f) { yield return null; w += Time.deltaTime; }
                Debug.Log($"[M2] 스모크: 야차 끝 {ya.Result} · {w:F1}초 · 스크럼 HP 최저 {(foeMin == int.MaxValue ? -1 : foeMin)} · 태클 {(ya.Foe != null ? ya.Foe.Tackles : 0)} · 탐색 {(!mode.Active)}");
            }
            Destroy(bot);
            if (pin != null) pin.enabled = true;
            Debug.Log("[M2] 스모크: 끝 — 종료");
            Application.Quit();
        }
    }
}
