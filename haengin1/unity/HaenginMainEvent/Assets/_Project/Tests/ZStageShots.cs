// 행인1의 메인이벤트 — 13~16단계 눈 확인·녹화(docs/08_M2_전투_설계.md 11장: 15 '적 4명 정면·옆', 16 '1:3·야차 한 판씩 녹화'), -c13shots <폴더>, -nographics 없이
//   lineup_front.png · lineup_side.png : 연습장에 적 4명 전투 자세(89) 줄 세움 — 덩치 팔 근육 범위 확인
//   bulky_attack.png                  : 냉장고 내려찍기(128) · 스크럼 밀기(260) 맞는 순간 자세(앞·옆)
//   seq13_enc/f_000.png  + enc_*.png  : Zone1 인카운터 Y4 한 판(걸어 들어가 시비 → 봇 전투 → 정리. → 결과 → 탐색), 게임 카메라 3프레임마다(= 20fps 실시간)
//   defeat.png                        : 패배 화면
//   seq14_yacha/f_000.png + ya_*.png  : 야차 Y1 한 판(심판 형 대화 → 입장 · 이름 카드 → 봇 싸움 → 결과)
//   pause_menu.png                    : 야차 중 일시정지 메뉴(흔들림 줄이기 · 항복)
// 봇 = FightBot(InputScript 와 같은 길로 PlayerCombat 에 입력). 녹화용이라 시우 HP 600.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Haengin.Tests
{
    public sealed class ZStageShots
    {
        const float Dt = Lab.Dt;
        string dir;
        Camera main, side;
        readonly List<string> notes = new List<string>();

        static string Arg(string name)
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++)
                if (string.Equals(a[i], name, StringComparison.OrdinalIgnoreCase)) return a[i + 1];
            return null;
        }

        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        [UnityTest, Timeout(3600000)]
        public IEnumerator Z_StageShots()
        {
            dir = Arg("-c13shots");
            if (string.IsNullOrEmpty(dir)) { Assert.Ignore("-c13shots <폴더> 를 줄 때만 찍는다"); yield break; }
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) { Assert.Ignore("그래픽 장치 없음(-nographics)"); yield break; }
#if UNITY_EDITOR
            UnityEditor.ShaderUtil.allowAsyncCompilation = false;
#endif
            Directory.CreateDirectory(dir);
            Time.captureDeltaTime = Dt;
            TimeFx.Reset();
            GameState.SetPaused(false);
            Encounter.Suppress = false;

            yield return Lineup();
            if (Arg("-lineuponly") == null) { yield return EncounterRun(); yield return YachaRun(); }

            File.WriteAllText(Path.Combine(dir, "notes13.txt"), string.Join("\n", notes));
            Debug.Log("[M2Shots13]\n" + string.Join("\n", notes));
            foreach (var p in UnityEngine.Object.FindObjectsByType<PInput>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (p.Actions != null) p.Actions.Disable();
            GameState.Modal = GameState.InputLocked = false;
            GameUi.RetryHook = GameUi.SurrenderHook = null;
            TimeFx.Reset();
            yield return Lab.FreshScene();
            Time.captureDeltaTime = 0f;
        }

        IEnumerator Open(string path)
        {
            yield return Lab.UnloadOurs(default);
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            var scene = SceneManager.GetSceneByPath(path);
            Assert.IsTrue(scene.IsValid() && scene.isLoaded, path);
            SceneManager.SetActiveScene(scene);
            yield return null;
            foreach (var p in UnityEngine.Object.FindObjectsByType<PInput>(FindObjectsSortMode.None)) p.enabled = false;
            foreach (var c in UnityEngine.Object.FindObjectsByType<CamInput>(FindObjectsSortMode.None)) c.enabled = false;
            main = Camera.main;
            var sgo = new GameObject("SideCam");
            side = sgo.AddComponent<Camera>();
            side.enabled = false;
            side.nearClipPlane = 0.05f; side.farClipPlane = 300f;
            side.clearFlags = main.clearFlags; side.backgroundColor = main.backgroundColor;
            yield return Frames(30);
        }

        // ───────────────────────── 15단계: 적 4명 전투 자세
        IEnumerator Lineup()
        {
            yield return Open(ZCombatShots.ScenePath);
            var pc = UnityEngine.Object.FindAnyObjectByType<PlayerCombat>();
            var dummy = Fighter.All.FirstOrDefault(f => !f.IsPlayer);
            ((PlayerBody)pc.Me.Body).Place(new Vector3(-6f, 0f, -6f), 0f);
            if (dummy != null) dummy.Body.Place(new Vector3(6f, 0f, -6f), 0f);
            string[] names = { "Kkanjok", "Seokdal", "Naengjanggo", "Scrum" };
            var made = new List<Animator>();
#if UNITY_EDITOR
            for (int k = 0; k < names.Length; k++)
            {
                var pf = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Prefabs/Enemy_{names[k]}.prefab");
                if (pf == null) continue;
                var go = UnityEngine.Object.Instantiate(pf, new Vector3(-1.8f + 1.2f * k, 0f, 0f), Quaternion.Euler(0f, 180f, 0f));
                var fa = go.GetComponent<FighterAnim>(); if (fa != null) fa.enabled = false;
                var an = go.GetComponent<Animator>();
                an.Play("CombatMove", 0, 0.3f);
                var hs = go.GetComponent<HandShape>(); if (hs != null) hs.ForceFist = 100f;
                made.Add(an);
            }
#endif
            yield return Frames(30);
            side.fieldOfView = 34f;
            side.transform.position = new Vector3(0f, 1.05f, -5.4f);
            side.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0.95f, 0f) - side.transform.position);
            notes.Add("적 4명 전투 자세(정면): " + Shot(side, Path.Combine(dir, "lineup_front.png"), 1920, 1080, 2));
            side.transform.position = new Vector3(-4.6f, 1.05f, -2.2f);
            side.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0.95f, 0f) - side.transform.position);
            notes.Add("적 4명 전투 자세(옆): " + Shot(side, Path.Combine(dir, "lineup_side.png"), 1920, 1080, 2));
            // 덩치 둘의 공격 자세: 냉장고 내려찍기(타격 1.4초 근처) · 스크럼 밀기
            if (made.Count >= 4)
            {
                made[0].gameObject.SetActive(false); made[1].gameObject.SetActive(false);
                made[2].Play("Smash", 0, 0.70f); made[2].speed = 0f;
                made[3].Play("PushFwd", 0, 0.74f); made[3].speed = 0f;
                yield return Frames(3);
                side.transform.position = new Vector3(1.8f, 1.1f, -4.6f);
                side.transform.rotation = Quaternion.LookRotation(new Vector3(1.8f, 1.0f, 0f) - side.transform.position);
                notes.Add("덩치 공격 자세(앞): " + Shot(side, Path.Combine(dir, "bulky_attack_front.png"), 1920, 1080, 2));
                side.transform.position = new Vector3(5.6f, 1.1f, -0.4f);
                side.transform.rotation = Quaternion.LookRotation(new Vector3(1.8f, 1.0f, 0f) - side.transform.position);
                notes.Add("덩치 공격 자세(옆): " + Shot(side, Path.Combine(dir, "bulky_attack_side.png"), 1920, 1080, 2));
            }
            foreach (var a in made) if (a != null) UnityEngine.Object.Destroy(a.gameObject);
            UnityEngine.Object.Destroy(side.gameObject);
            yield return null;
        }

        // ───────────────────────── 13단계: 인카운터 한 판
        IEnumerator EncounterRun()
        {
            yield return Open(ZoneTests.ScenePath);
            var pc = UnityEngine.Object.FindAnyObjectByType<PlayerCombat>();
            var enc = UnityEngine.Object.FindAnyObjectByType<Encounter>();
            var mode = UnityEngine.Object.FindAnyObjectByType<CombatMode>();
            var hud = StageHud.Instance;
            var d = enc.Def;
            pc.Me.MaxHp = pc.Me.Hp = 600;
            ((PlayerBody)pc.Me.Body).Place(d.ToWorld(d.RetryLocal) + Vector3.up * 0.1f, d.Yaw + 180f);
            yield return Frames(60);
            var sd = Path.Combine(dir, "seq13_enc");
            Directory.CreateDirectory(sd);
            int n = 0, i = 0;
            bool sTaunt = false, sBanner = false, sFinish = false, sResult = false, sHeat = false;
            FightBot bot = null;
            float t = 0f;
            var motor = pc.Motor;
            while (t < 240f)
            {
                if (enc.State == Encounter.Phase.Armed)
                {
                    var goal = d.ToWorld(new Vector2(1.5f, -1.0f));
                    motor.SetMoveInput(HitResolver.Flat(goal - motor.Position).normalized, 1f, false);
                }
                else motor.ClearMoveInput();
                if (enc.State == Encounter.Phase.Fight && bot == null) bot = FightBot.On(pc, false);
                yield return null;
                t += Dt; i++;
                if (i % 3 == 0) { Shot(main, Path.Combine(sd, $"f_{n:0000}.png"), 960, 540, 1); n++; }
                if (!sTaunt && enc.State == Encounter.Phase.Taunt && enc.PhaseT > 0.3) { sTaunt = true; notes.Add("시비 자막: " + Shot(main, Path.Combine(dir, "enc_taunt.png"), 1920, 1080, 2)); }
                if (!sBanner && enc.State == Encounter.Phase.Fight && enc.PhaseT > 0.5) { sBanner = true; notes.Add("전투 시작 배너: " + Shot(main, Path.Combine(dir, "enc_banner.png"), 1920, 1080, 2)); }
                if (!sHeat && pc.InHeatAction && pc.HeatAct.T > 1.0) { sHeat = true; notes.Add($"인카운터 기세 액션({pc.HeatAct.Kind}): " + Shot(main, Path.Combine(dir, "enc_heat.png"), 1920, 1080, 2)); }
                if (!sFinish && enc.State == Encounter.Phase.Finish && enc.PhaseT > 0.4) { sFinish = true; notes.Add("마무리 「정리.」: " + Shot(main, Path.Combine(dir, "enc_finish.png"), 1920, 1080, 2)); }
                if (!sResult && enc.State == Encounter.Phase.Result && enc.PhaseT > 0.6) { sResult = true; notes.Add("결과 카드: " + Shot(main, Path.Combine(dir, "enc_result.png"), 1920, 1080, 2)); }
                if (enc.State == Encounter.Phase.Cleared && enc.PhaseT > 1.2) break;
            }
            if (bot != null) UnityEngine.Object.Destroy(bot);
            notes.Add($"인카운터 한 판: {sd} {n}장 · {t:F1}초 · 결과 {enc.State} · 탈락 {enc.Eliminated}/3 · 시우 HP {pc.Me.Hp}/{pc.Me.MaxHp} · 봇 누름 {bot?.Presses} 막기 {bot?.GuardFrames}f 회피 {bot?.Dodges} 기세 액션 {bot?.HeatActions}");
            // 패배 화면
            if (enc.State == Encounter.Phase.Cleared)
            {
                GameUi.RetryHook?.Invoke();
                yield return Frames(80);
                var kill = MoveLib.EnemyJab(); kill.Damage = 9999;
                pc.Me.Receive(enc.Brains[0].Me, kill);
                float w = 0f;
                while (!hud.DefeatShown && w < 6f) { yield return null; w += Dt; }
                yield return Frames(90);
                notes.Add("패배 화면: " + Shot(main, Path.Combine(dir, "defeat.png"), 1920, 1080, 2));
                hud.DefeatChoose(true);
                yield return Frames(20);
            }
            pc.Me.MaxHp = 200; pc.Me.ResetFighter();
        }

        // ───────────────────────── 14단계: 야차 한 판
        IEnumerator YachaRun()
        {
            var pc = UnityEngine.Object.FindAnyObjectByType<PlayerCombat>();
            var ya = UnityEngine.Object.FindAnyObjectByType<Yacha>();
            var hud = StageHud.Instance;
            if (ya == null || pc == null) { notes.Add("야차 없음"); yield break; }
            pc.Me.MaxHp = pc.Me.Hp = 600;
            ((PlayerBody)pc.Me.Body).Place(ya.Def.Referee + new Vector3(0.6f, 0.1f, -1.6f), 20f);
            yield return Frames(40);
            notes.Add($"심판 형 안내(\"{hud.PromptText}\"): " + Shot(main, Path.Combine(dir, "ya_prompt.png"), 1920, 1080, 2));
            Interactable.TryUse(pc.Me.Position);
            yield return Frames(10);
            notes.Add("심판 형 대화: " + Shot(main, Path.Combine(dir, "ya_dialog.png"), 1920, 1080, 2));
            hud.Choose(true);
            var sd = Path.Combine(dir, "seq14_yacha");
            Directory.CreateDirectory(sd);
            int n = 0, i = 0;
            bool sCard = false, sCrowd = false, sMenu = false, sBar = false, sEnd = false;
            FightBot bot = null;
            float t = 0f;
            while (t < 260f)
            {
                if (ya.State == Yacha.Phase.Fight && bot == null) bot = FightBot.On(pc, false);
                yield return null;
                t += Dt; i++;
                if (i % 3 == 0) { Shot(main, Path.Combine(sd, $"f_{n:0000}.png"), 960, 540, 1); n++; }
                if (!sCrowd && ya.State == Yacha.Phase.Entry && ya.PhaseT > 1.5) { sCrowd = true; notes.Add("입장(구경꾼 걸어옴): " + Shot(main, Path.Combine(dir, "ya_entry.png"), 1920, 1080, 2)); }
                if (!sCard && ya.State == Yacha.Phase.Entry && ya.PhaseT > 3.6) { sCard = true; notes.Add("이름 카드: " + Shot(main, Path.Combine(dir, "ya_card.png"), 1920, 1080, 2)); }
                if (!sBar && ya.State == Yacha.Phase.Fight && ya.PhaseT > 6.0) { sBar = true; notes.Add("야차 큰 바: " + Shot(main, Path.Combine(dir, "ya_bar.png"), 1920, 1080, 2)); }
                if (!sMenu && ya.State == Yacha.Phase.Fight && ya.PhaseT > 8.0)
                {
                    sMenu = true;
                    GameState.SetPaused(true);
                    yield return null; yield return null;
                    notes.Add("일시정지 메뉴(야차 중): " + Shot(main, Path.Combine(dir, "pause_menu.png"), 1920, 1080, 2));
                    GameState.SetPaused(false);
                }
                if (!sEnd && ya.State == Yacha.Phase.End && ya.PhaseT > 1.4) { sEnd = true; notes.Add($"야차 끝({ya.Result}): " + Shot(main, Path.Combine(dir, "ya_end.png"), 1920, 1080, 2)); }
                if (ya.State == Yacha.Phase.Idle && ya.PhaseT > 1.5 && t > 10f) break;
            }
            if (bot != null) UnityEngine.Object.Destroy(bot);
            notes.Add($"야차 한 판: {sd} {n}장 · {t:F1}초 · 결과 {ya.Result} · 스크럼 HP {ya.Foe?.Me.Hp} · 태클 {ya.Foe?.Tackles}(헛방 {ya.Foe?.TackleWhiffs}) · 페이즈 2 {ya.Foe?.Phase2} · 다운 {ya.Downs} · 링 떠밂 {ya.Pushes} · 시우 HP {pc.Me.Hp}");
        }

        string Shot(Camera c, string path, int w, int h, int ss)
        {
            var big = new RenderTexture(new RenderTextureDescriptor(w * ss, h * ss, RenderTextureFormat.ARGB32, 24) { sRGB = true, msaaSamples = 1 });
            var small = new RenderTexture(new RenderTextureDescriptor(w, h, RenderTextureFormat.ARGB32, 0) { sRGB = true });
            big.Create(); small.Create();
            var prevT = c.targetTexture;
            var prevA = RenderTexture.active;
            var overlays = c == main ? UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(x => x.isRootCanvas && x.renderMode == RenderMode.ScreenSpaceOverlay && x.isActiveAndEnabled).ToList() : new List<Canvas>();
            try
            {
                c.targetTexture = big;
                c.aspect = (float)w / h;
                foreach (var o in overlays) { o.renderMode = RenderMode.ScreenSpaceCamera; o.worldCamera = c; o.planeDistance = 0.5f - o.sortingOrder * 0.002f; }
                Canvas.ForceUpdateCanvases();
                c.Render();
                Graphics.Blit(big, small);
                RenderTexture.active = small;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                UnityEngine.Object.Destroy(tex);
            }
            finally
            {
                foreach (var o in overlays) { o.renderMode = RenderMode.ScreenSpaceOverlay; o.worldCamera = null; }
                c.targetTexture = prevT;
                c.ResetAspect();
                RenderTexture.active = prevA;
                big.Release(); small.Release();
                UnityEngine.Object.Destroy(big); UnityEngine.Object.Destroy(small);
            }
            return path;
        }
    }
}
