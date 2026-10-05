// 행인1의 메인이벤트 — 10·11·12단계 눈으로 확인(docs/08_M2_전투_설계.md 11장: 10 '녹화', 11 '약·중·강·기세 스크린샷', 12 '스크린샷'), -c10shots <폴더> 를 줄 때만, -nographics 없이
// CombatLab 에서(시우 = 실제 Player 프리팹 + CombatUi, 허수아비 = 석 달 모델):
//   hud_ready.png            : 기세 MAX + 락온 + 「△ 기세」 판 + 다른 적 두 명(게임 카메라 1920×1080)
//   seq10_wall/f_000.png     : 「벽 러시」 게임 카메라(CM_Heat) 960×540 2프레임마다 + heat_wall_hit.png(마지막 어퍼 히트스톱 첫 프레임)
//   seq10_crowd/f_000.png    : 「구경꾼 되받기」 같은 방식 + heat_crowd_hit.png
//   fx_light/mid/heavy/heat.png : 옆 카메라로 약(잽)·중(크로스)·강(어퍼)·기세(벽 러시 어퍼) 맞은 순간(히트스톱 첫 프레임) — 이펙트·의성어·번쩍·쇼크 컷
//   hud_offscreen.png        : 공격권 가진 적이 화면 밖 → 가장자리 화살표
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
    public sealed class ZHeatShots
    {
        const float Dt = Lab.Dt;
        string dir;
        Camera main, side;
        PlayerCombat pc;
        Fighter dummy;
        CombatMode mode;
        readonly List<string> notes = new List<string>();
        readonly List<GameObject> spawned = new List<GameObject>();

        static string Arg(string name)
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++)
                if (string.Equals(a[i], name, StringComparison.OrdinalIgnoreCase)) return a[i + 1];
            return null;
        }

        [UnityTest, Timeout(3600000)]
        public IEnumerator Z_HeatShots()
        {
            dir = Arg("-c10shots");
            if (string.IsNullOrEmpty(dir)) { Assert.Ignore("-c10shots <폴더> 를 줄 때만 찍는다"); yield break; }
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) { Assert.Ignore("그래픽 장치 없음(-nographics)"); yield break; }
#if UNITY_EDITOR
            UnityEditor.ShaderUtil.allowAsyncCompilation = false;
#endif
            Directory.CreateDirectory(dir);
            Time.captureDeltaTime = Dt;
            TimeFx.Reset();
            GameState.SetPaused(false);
            yield return Lab.UnloadOurs(default);
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ZCombatShots.ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            var scene = SceneManager.GetSceneByPath(ZCombatShots.ScenePath);
            if (!scene.IsValid() || !scene.isLoaded) { Assert.Ignore("CombatLab 장면 없음"); yield break; }
            SceneManager.SetActiveScene(scene);
            yield return null;
            pc = UnityEngine.Object.FindAnyObjectByType<PlayerCombat>();
            mode = UnityEngine.Object.FindAnyObjectByType<CombatMode>();
            dummy = Fighter.All.FirstOrDefault(f => !f.IsPlayer);
            foreach (var p in UnityEngine.Object.FindObjectsByType<PInput>(FindObjectsSortMode.None)) p.enabled = false;
            main = Camera.main;
            Assert.NotNull(pc); Assert.NotNull(dummy); Assert.NotNull(mode); Assert.NotNull(main);
            var sgo = new GameObject("SideCam");
            side = sgo.AddComponent<Camera>();
            side.enabled = false;
            side.fieldOfView = 34f; side.nearClipPlane = 0.05f; side.farClipPlane = 200f;
            side.clearFlags = main.clearFlags; side.backgroundColor = main.backgroundColor;
            for (int i = 0; i < 90; i++) yield return null;
            pc.Me.MaxHp = pc.Me.Hp = 9999;

            yield return FxStills();
            yield return Wall();
            yield return Crowd();
            yield return Offscreen();

            File.WriteAllText(Path.Combine(dir, "notes10.txt"), string.Join("\n", notes));
            Debug.Log("[M2Shots10]\n" + string.Join("\n", notes));
            foreach (var g in spawned) if (g != null) UnityEngine.Object.Destroy(g);
            UnityEngine.Object.Destroy(sgo);
            var input = UnityEngine.Object.FindAnyObjectByType<PInput>(FindObjectsInactive.Include);
            if (input != null && input.Actions != null) input.Actions.Disable();
            TimeFx.Reset();
            yield return Lab.FreshScene();
            Time.captureDeltaTime = 0f;
        }

        IEnumerator Reset(Vector3 me, float meYaw, Vector3 dm, float dmYaw)
        {
            if (!mode.Active) mode.Begin(0f);
            pc.Me.ResetFighter(); dummy.ResetFighter();
            pc.Me.MaxHp = pc.Me.Hp = 9999;
            dummy.MaxHp = dummy.Hp = 999;
            ((PlayerBody)pc.Me.Body).Place(me, meYaw);
            dummy.Body.Place(dm, dmYaw);
            if (pc.Lock != null) pc.Lock.Unlock();
            pc.CombatCam.Snap();
            for (int i = 0; i < 60; i++) yield return null;
        }

        void SideAt(Vector3 look, float x = 4.2f)
        {
            side.transform.position = new Vector3(look.x + x, 1.25f, look.z + 0.15f);
            side.transform.rotation = Quaternion.LookRotation(look - side.transform.position);
        }

        // ───────────────────────── 11단계: 약·중·강 맞은 순간(옆 카메라) — 기세는 벽 러시에서
        IEnumerator FxStills()
        {
            // 먼저 한 대(전투 첫 타격의 쇼크 컷이 '약' 사진에 들어가지 않게)
            yield return Reset(new Vector3(0f, 0f, -0.7f), 0f, new Vector3(0f, 0f, 0.7f), 180f);
            pc.Press(Btn.Light);
            for (int i = 0; i < 40; i++) yield return null;
            var keys = new[] { ("light", "약 · 잽", new[] { (0, Btn.Light) }, 1), ("mid", "중 · 크로스", new[] { (0, Btn.Light), (12, Btn.Light) }, 2), ("heavy", "강 · 어퍼", new[] { (0, Btn.Light), (12, Btn.Heavy) }, 2) };
            foreach (var (key, label, seq, nth) in keys)
            {
                yield return Reset(new Vector3(0f, 0f, -0.7f), 0f, new Vector3(0f, 0f, 0.7f), 180f);
                SideAt(new Vector3(0f, 1.1f, 0f));
                int c0 = ImpactFx.Count;
                for (int i = 0; i < 120; i++)
                {
                    foreach (var s in seq) if (s.Item1 == i) pc.Press(s.Item2);
                    yield return null;
                    if (ImpactFx.Count - c0 >= nth)
                    {
                        yield return null;     // 그린 다음 프레임(이펙트가 한 번 갱신된 모습)
                        notes.Add($"이펙트 {label}: " + Shot(side, Path.Combine(dir, $"fx_{key}.png"), 1920, 1080, 2) + $" | 의성어 {CombatFx.LastWord} · 조각 {CombatFx.Instance?.Live} · 번쩍 {CombatFx.Flashes} · 쇼크 {CombatFx.Shocks}");
                        break;
                    }
                }
                for (int i = 0; i < 50; i++) yield return null;
            }
        }

        GameObject EnemyAt(string prefab, EnemyDef def, Vector3 at, float yaw, AttackDirector d, int idx)
        {
            GameObject model = null;
#if UNITY_EDITOR
            var pf = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Prefabs/{prefab}.prefab");
            if (pf != null) model = UnityEngine.Object.Instantiate(pf);
#endif
            var b = CombatFactory.Enemy(def, at, yaw, pc.Me, d, idx, pc.Tuning, model);
            b.NoAttack = true; b.HoldPosition = true;
            spawned.Add(b.gameObject);
            b.Activate();
            return b.gameObject;
        }

        // ───────────────────────── 10단계: 벽 러시
        IEnumerator Wall()
        {
            // 북쪽 벽(z 8.0) 앞: 허수아비 z 6.9(등 뒤 벽까지 0.8m), 시우 z 5.6
            yield return Reset(new Vector3(0f, 0f, 5.6f), 0f, new Vector3(0f, 0f, 6.9f), 180f);
            var d = CombatFactory.Director(pc.Me, pc.Tuning);
            spawned.Add(d.gameObject);
            EnemyAt("Enemy_Kkanjok", EnemyLib.Kkanjok(), new Vector3(-3.2f, 0f, 4.2f), 60f, d, 0);
            EnemyAt("Enemy_Naengjanggo", EnemyLib.Naengjanggo(), new Vector3(3.4f, 0f, 4.0f), -60f, d, 1);
            for (int i = 0; i < 30; i++) yield return null;
            pc.Lock.Set(dummy);
            pc.Heat.Add(100f);
            for (int i = 0; i < 40; i++) yield return null;
            var hud = pc.GetComponentInChildren<CombatHud>();
            notes.Add("HUD(기세 MAX·락온·기세 판): " + Shot(main, Path.Combine(dir, "hud_ready.png"), 1920, 1080, 2) + $" | 판 {hud?.HeatPlateShown}(\"{hud?.HeatPlateText}\") · MAX {hud?.HeatMaxShown} · 대상 {hud?.TargetName} {hud?.TargetBar:F2}");
            yield return Record("wall", "벽 러시", HeatMoves.Upper);
            foreach (var g in spawned) if (g != null) UnityEngine.Object.Destroy(g);
            spawned.Clear();
            for (int i = 0; i < 40; i++) yield return null;
        }

        // ───────────────────────── 10단계: 구경꾼 되받기
        IEnumerator Crowd()
        {
            // 남쪽 구경꾼 줄(반경 6): 허수아비 z −5.0(등 뒤 줄까지 0.7m), 시우 z −3.7 남쪽 보기
            yield return Reset(new Vector3(0.3f, 0f, -3.7f), 180f, new Vector3(0.3f, 0f, -5.0f), 0f);
            pc.Lock.Set(dummy);
            pc.Heat.Add(100f);
            for (int i = 0; i < 40; i++) yield return null;
            {
                var k = pc.HeatAct.Check(out var ct);
                notes.Add($"되받기 직전: 조건 {k} 대상 {ct?.Label} · 기세 {pc.Heat.Value:F0} · 시우 {pc.Me.Position} yaw {pc.Me.Yaw:F0} 상태 {pc.Me.State} · 허수아비 {dummy.Position} 상태 {dummy.State} · 락온 {pc.Lock.Target?.Label}");
            }
            yield return Record("crowd", "구경꾼 되받기", HeatMoves.Counter);
            for (int i = 0; i < 40; i++) yield return null;
        }

        IEnumerator Record(string key, string label, MoveDef final)
        {
            var sd = Path.Combine(dir, "seq10_" + key);
            Directory.CreateDirectory(sd);
            int n = 0;
            bool shot = false;
            int dealt0 = 0;
            pc.Heat.Add(100f);      // 더하기(=때린 것처럼 내림 시계 다시) — Set 만 하면 마지막 타격 뒤 4초가 지나 바로 줄어 MAX 가 아니다
            pc.Press(Btn.Heavy);
            var h = pc.HeatAct;
            for (int i = 0; i < 600; i++)
            {
                yield return null;
                if (!shot && ImpactFx.Last.Move == final && ImpactFx.Last.Attacker == pc.Me && TimeFx.InHitStop)
                {
                    shot = true;
                    notes.Add($"{label} 마무리(히트스톱 첫 프레임): " + Shot(main, Path.Combine(dir, $"heat_{key}_hit.png"), 1920, 1080, 2) + $" | 의성어 {CombatFx.LastWord} · 번쩍 {CombatFx.Flashes} · 쇼크 {CombatFx.Shocks}");

                }
                if (i % 2 == 0) { Shot(main, Path.Combine(sd, $"f_{n:000}.png"), 960, 540, 1); n++; }
                if (h != null && !h.Playing && i > 30 && i > 120) break;
            }
            dealt0 = h != null ? h.Dealt : -1;
            notes.Add($"{label}: {sd} {n}장 · 종류 {h?.Kind} · 피해 {dealt0} · 대상 {dummy.State} HP {dummy.Hp}");
        }

        // ───────────────────────── 12단계: 화면 밖 화살표
        IEnumerator Offscreen()
        {
            yield return Reset(new Vector3(0f, 0f, 0f), 0f, new Vector3(0f, 0f, 1.6f), 180f);
            var d = CombatFactory.Director(pc.Me, pc.Tuning);
            spawned.Add(d.gameObject);
            // 시우 왼쪽 뒤 4.2m(간보기 고리 안 — 바로 간보기 → 공격권). 락온 카메라(시우 뒤 4~5m, 가로 ±37°)에서 73° 밖
            var g = EnemyAt("Enemy_Kkanjok", EnemyLib.Kkanjok(), new Vector3(-3.0f, 0f, -3.0f), 45f, d, 0);
            var b = g.GetComponent<EnemyBrain>();
            b.NoAttack = false;
            pc.Lock.Set(dummy);
            var hud = pc.GetComponentInChildren<CombatHud>();
            bool shot = false;
            for (int i = 0; i < 600 && !shot; i++)
            {
                yield return null;
                if (hud != null && hud.ArrowShown)
                {
                    shot = true;
                    notes.Add("화면 밖 화살표: " + Shot(main, Path.Combine(dir, "hud_offscreen.png"), 1920, 1080, 2) + $" | 공격권 {b.HasToken} · 예고 {b.Me.Telegraphing}");
                }
            }
            if (!shot) notes.Add("화면 밖 화살표: 안 나옴");
        }

        string Shot(Camera c, string path, int w, int h, int ss)
        {
            var big = new RenderTexture(new RenderTextureDescriptor(w * ss, h * ss, RenderTextureFormat.ARGB32, 24) { sRGB = true, msaaSamples = 1 });
            var small = new RenderTexture(new RenderTextureDescriptor(w, h, RenderTextureFormat.ARGB32, 0) { sRGB = true });
            big.Create(); small.Create();
            var prevT = c.targetTexture;
            var prevA = RenderTexture.active;
            var overlays = c == main ? UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(x => x.isRootCanvas && x.renderMode == RenderMode.ScreenSpaceOverlay).ToList() : new List<Canvas>();
            try
            {
                c.targetTexture = big;
                c.aspect = (float)w / h;
                foreach (var o in overlays) { o.renderMode = RenderMode.ScreenSpaceCamera; o.worldCamera = c; o.planeDistance = 0.5f; }
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
