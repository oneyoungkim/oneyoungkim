// 행인1의 메인이벤트 — 전투 연습장 녹화·스크린샷 (docs/08_M2_전투_설계.md 11장 3·4단계 '눈으로 비교', -c2shots <폴더> 를 줄 때만, -nographics 없이)
// Scenes/CombatLab.unity 를 열고(시우 = 실제 Player 프리팹, 허수아비 = 태오 모델 회색) 정해진 입력을 넣으며 프레임을 PNG 로 남긴다.
//   hit_<기술>.png        : 히트스톱 첫 프레임(1920×1080) — 시안 페이지 __freezeOnHit 와 같은 순간
//   seq_<이름>/f_000.png  : 960×540 연속 프레임(1/60초) — mp4·띠 사진은 밖에서(ffmpeg·PIL)
// 코루틴은 LateUpdate(카메라·젖힘) 전에 돌아서, 찍은 그림은 '지난 프레임이 끝난 모습'이다(히트스톱 중이면 같은 순간).
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
    // 이름이 Z 로 시작: 실행 순서가 InputTests(가짜 장치) 뒤가 되게 — 장면의 PInput 이 실제 HInput 에셋을 켜면 그 뒤 InputTestFixture 의 가짜 입력이 막힌다(AnimTests 주석과 같은 문제)
    public sealed class ZCombatShots
    {
        public const string ScenePath = "Assets/_Project/Scenes/CombatLab.unity";
        const float Dt = Lab.Dt;
        string dir;
        Camera cam;
        PlayerCombat pc;
        Fighter dummy;
        CombatMode mode;
        readonly List<string> notes = new List<string>();

        static string Arg(string name)
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++)
                if (string.Equals(a[i], name, StringComparison.OrdinalIgnoreCase)) return a[i + 1];
            return null;
        }

        [UnityTest, Timeout(1800000)]
        public IEnumerator Z_CombatShots()
        {
            dir = Arg("-c2shots");
            if (string.IsNullOrEmpty(dir)) { Assert.Ignore("-c2shots <폴더> 를 줄 때만 찍는다"); yield break; }
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
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded) { Assert.Ignore("CombatLab 장면 없음(CombatSetup.Build 먼저)"); yield break; }
            SceneManager.SetActiveScene(scene);
            yield return null;
            pc = UnityEngine.Object.FindAnyObjectByType<PlayerCombat>();
            mode = UnityEngine.Object.FindAnyObjectByType<CombatMode>();
            dummy = Fighter.All.FirstOrDefault(f => !f.IsPlayer);
            foreach (var p in UnityEngine.Object.FindObjectsByType<PInput>(FindObjectsSortMode.None)) p.enabled = false;
            cam = Camera.main;
            Assert.NotNull(pc, "PlayerCombat"); Assert.NotNull(dummy, "허수아비"); Assert.NotNull(mode, "CombatMode"); Assert.NotNull(cam, "카메라");
            for (int i = 0; i < 90; i++) yield return null;
            Assert.IsTrue(mode.Active, "시작하면 전투");
            MakeCompareCam();

            // (0) 연습장 전경(전투 카메라, 락온 전)
            notes.Add("연습장(전투 카메라): " + Shot(Path.Combine(dir, "lab_0_overview.png"), 1920, 1080, 2));

            // (1) 타격감 1차(3단계): 약 잽 · 중 크로스 · 중 훅 · 강 어퍼 · 기세 더킹 어퍼 — 히트스톱 첫 프레임 + 연속 프레임
            yield return Hit("jab", "약 잽", new[] { (0, Btn.Light) }, 1);
            yield return Hit("cross", "중 크로스(□□)", new[] { (0, Btn.Light), (12, Btn.Light) }, 2);
            yield return Hit("hook", "중 훅(□□□)", new[] { (0, Btn.Light), (12, Btn.Light), (26, Btn.Light) }, 3);
            yield return Hit("upper", "강 어퍼(□△)", new[] { (0, Btn.Light), (12, Btn.Heavy) }, 2);
            yield return HeatHit();
            yield return Hit("kick", "중 앞차기(△)", new[] { (0, Btn.Heavy) }, 1);

            // (2) 락온·전환(4단계): 전투 → 탐색(0.8초 블렌드) → 전투(0.6초) → 락온 → 옆으로 돌기
            yield return Transition();

            // (3) 회피 '읽었다' + 반격, 막기 크러시, 잡기 → 벽꽝(5·6단계)
            yield return ReadCounter();
            yield return GuardCrush();
            yield return GrabSlam();

            File.WriteAllText(Path.Combine(dir, "notes.txt"), string.Join("\n", notes));
            Debug.Log("[M2Shots]\n" + string.Join("\n", notes));
            if (compare != null) UnityEngine.Object.Destroy(compare.gameObject);
            var input = UnityEngine.Object.FindAnyObjectByType<PInput>(FindObjectsInactive.Include);
            if (input != null && input.Actions != null) input.Actions.Disable();
            TimeFx.Reset();
            yield return Lab.FreshScene();      // CombatLab 을 내린다(뒤 테스트에 시우·카메라가 남지 않게)
            Time.captureDeltaTime = 0f;
        }

        IEnumerator Reset(Vector3 me, float meYaw, Vector3 dm, float dmYaw)
        {
            pc.Me.ResetFighter(); dummy.ResetFighter();
            pc.Heat.Set(40f);
            ((PlayerBody)pc.Me.Body).Place(me, meYaw);
            dummy.Body.Place(dm, dmYaw);
            if (pc.Lock != null) pc.Lock.Unlock();
            pc.CombatCam.Snap();
            for (int i = 0; i < 70; i++) yield return null;
        }

        // ───────────────────────── 시안과 같은 화각의 옆 카메라(시안 99_app.js: PerspectiveCamera 34°, CamRig d 4.5 · az .1 · el .14 · 보는 점 1.1 · y −.2)
        // 시안은 두 사람이 x 축에 마주 서고 카메라가 +z 에서 본다 → 연습장(두 사람이 z 축)에서는 +x 쪽에서 본다. 흔들림·줌 펀치 확장(TraumaShake)도 붙임
        Unity.Cinemachine.CinemachineCamera compare;
        void MakeCompareCam()
        {
            var look = new GameObject("CompareLook").transform;
            look.position = new Vector3(0f, 1.1f, 0f);
            var go = new GameObject("CM_Compare");
            go.transform.position = new Vector3(4.5f * Mathf.Cos(0.14f) * Mathf.Cos(0.1f), 1.1f + 4.5f * Mathf.Sin(0.14f) - 0.2f, 4.5f * Mathf.Cos(0.14f) * Mathf.Sin(0.1f));
            go.transform.rotation = Quaternion.LookRotation(look.position - go.transform.position);
            compare = go.AddComponent<Unity.Cinemachine.CinemachineCamera>();
            compare.Priority = 0;
            var lens = Unity.Cinemachine.LensSettings.Default;
            lens.FieldOfView = 34f; lens.NearClipPlane = 0.1f; lens.FarClipPlane = 400f;
            compare.Lens = lens;
            compare.Target = new Unity.Cinemachine.CameraTarget { TrackingTarget = null, LookAtTarget = look, CustomLookAtTarget = true };
            go.AddComponent<Unity.Cinemachine.CinemachineHardLookAt>();
            go.AddComponent<TraumaShake>();
        }

        void UseCompare(bool on) { if (compare != null) compare.Priority = on ? 50 : 0; }

        /// 기술 하나 — 1회: 옆 카메라(시안과 같은 화각)로 맞은 순간(히트스톱 첫 프레임) 1장 + 처음부터 맞은 뒤 48f 까지 연속, 2회: 게임 카메라(락온)로 맞은 순간 1장
        IEnumerator Hit(string key, string label, (int f, Btn b)[] seq, int nth)
        {
            UseCompare(true);
            yield return Reset(CombatLab.PlayerStart, CombatLab.PlayerYaw, CombatLab.DummyStart, CombatLab.DummyYaw);
            int count0 = ImpactFx.Count;
            var seqDir = Path.Combine(dir, "seq_" + key);
            Directory.CreateDirectory(seqDir);
            int hitAt = -1, saved = 0;
            for (int i = 0; i < 150; i++)
            {
                foreach (var s in seq) if (s.f == i) pc.Press(s.b);
                yield return null;
                if (hitAt < 0 && ImpactFx.Count - count0 >= nth) hitAt = i;
                if (hitAt >= 0 && i == hitAt + 1)
                    notes.Add($"{label}(옆, 시안 화각): " + Shot(Path.Combine(dir, $"hit_{key}.png"), 1920, 1080, 2) + $" | {ImpactFx.Last.Move?.Label} p{(int)ImpactFx.Last.Power} 피해 {ImpactFx.Last.Damage} · 히트스톱 남음 {TimeFx.HitStopLeft:F3}");
                Shot(Path.Combine(seqDir, $"f_{saved:000}.png"), 960, 540, 1); saved++;
                if (hitAt >= 0 && i >= hitAt + 48) break;
            }
            UseCompare(false);
            notes.Add($"  연속 {label}: {seqDir} {saved}장(f_{hitAt + 1:000} = 맞은 순간)");
            if (hitAt < 0) { notes.Add($"{label}: 안 맞음"); yield break; }
            // 2회: 게임 카메라(락온 — 시우 등 뒤 오른쪽 어깨 18°)
            yield return Reset(CombatLab.PlayerStart, CombatLab.PlayerYaw, CombatLab.DummyStart, CombatLab.DummyYaw);
            pc.Lock.Set(dummy);
            for (int i = 0; i < 60; i++) yield return null;
            count0 = ImpactFx.Count;
            for (int i = 0; i < 150; i++)
            {
                foreach (var s in seq) if (s.f == i) pc.Press(s.b);
                yield return null;
                if (ImpactFx.Count - count0 >= nth)
                {
                    yield return null;
                    notes.Add($"  {label}(게임 카메라 락온): " + Shot(Path.Combine(dir, $"hit_{key}_game.png"), 1920, 1080, 2));
                    break;
                }
            }
            for (int i = 0; i < 40; i++) yield return null;
            pc.Lock.Unlock();
        }

        /// 기세 단계: 적 잽을 '읽었다'로 피하고 △ 더킹 어퍼(위력 기세, 히트스톱 0.2 + 슬로 0.5초 30%)
        IEnumerator HeatHit()
        {
            UseCompare(true);
            yield return Reset(CombatLab.PlayerStart, CombatLab.PlayerYaw, CombatLab.DummyStart, CombatLab.DummyYaw);
            var jab = MoveLib.EnemyJab();
            pc.DodgeMoveScale = 0f;
            int count0 = ImpactFx.Count;
            var seqDir = Path.Combine(dir, "seq_heat");
            Directory.CreateDirectory(seqDir);
            int saved = 0, hitAt = -1;
            for (int i = 0; i < 150; i++)
            {
                if (i == 10) { dummy.Body.SetYaw(HitResolver.Yaw(pc.Me.Position - dummy.Position)); dummy.StartAttack(jab, pc.Me); }
                if (i == 10 + 9 - 6) pc.Press(Btn.Dodge);
                if (i == 10 + 9 - 6 + 9) pc.Press(Btn.Heavy);
                yield return null;
                if (hitAt < 0 && ImpactFx.Count > count0 && ImpactFx.Last.Attacker == pc.Me) hitAt = i;
                if (hitAt >= 0 && i == hitAt + 1)
                    notes.Add("기세 더킹 어퍼(읽었다 → △): " + Shot(Path.Combine(dir, "hit_heat.png"), 1920, 1080, 2) + $" | {ImpactFx.Last.Move?.Label} p{(int)ImpactFx.Last.Power} 피해 {ImpactFx.Last.Damage} · 슬로 남음 {TimeFx.SlowLeft:F2}");
                if (i >= 6) { Shot(Path.Combine(seqDir, $"f_{saved:000}.png"), 960, 540, 1); saved++; }
            }
            pc.DodgeMoveScale = 1f;
            UseCompare(false);
            notes.Add($"  연속 기세: {seqDir} {saved}장(f_{Math.Max(0, hitAt + 1 - 6):000} = 맞은 순간) · 읽었다 {pc.ReadCount}");
        }

        IEnumerator Transition()
        {
            yield return Reset(CombatLab.PlayerStart, CombatLab.PlayerYaw, CombatLab.DummyStart, CombatLab.DummyYaw);
            var seqDir = Path.Combine(dir, "seq_transition");
            Directory.CreateDirectory(seqDir);
            int saved = 0;
            var log = new List<string>();
            for (int i = 0; i < 330; i++)
            {
                if (i == 10) { mode.End(); log.Add($"f{i} 전투 끝(→ 탐색, 0.8초)"); }
                if (i == 100) { mode.Begin(0f); log.Add($"f{i} 전투 시작(→ CM_Combat, 0.6초)"); }
                if (i == 170) { pc.ToggleLock(); log.Add($"f{i} 락온 {(pc.Lock.Target != null ? pc.Lock.Target.Label : "없음")}"); }
                if (i >= 190 && i < 300) pc.SetStickWorld(Vector3.Cross(Vector3.up, (dummy.Position - pc.Me.Position).normalized), 1f);
                if (i == 300) pc.SetStickWorld(Vector3.zero, 0f);
                yield return null;
                if (i % 2 == 0) { Shot(Path.Combine(seqDir, $"f_{saved:000}.png"), 960, 540, 1); saved++; }
                if (i == 60) notes.Add("탐색 복귀 화면: " + Shot(Path.Combine(dir, "lock_1_explore.png"), 1920, 1080, 2));
                if (i == 165) notes.Add("전투 카메라(락온 전): " + Shot(Path.Combine(dir, "lock_2_combat_free.png"), 1920, 1080, 2));
                if (i == 240) notes.Add("락온 + 옆으로 돌기: " + Shot(Path.Combine(dir, "lock_3_locked.png"), 1920, 1080, 2));
            }
            notes.Add($"  연속 전환(2프레임마다): {seqDir} {saved}장 · {string.Join(" · ", log)}");
            pc.Lock.Unlock();
        }

        IEnumerator ReadCounter()
        {
            yield return Reset(CombatLab.PlayerStart, CombatLab.PlayerYaw, CombatLab.DummyStart, CombatLab.DummyYaw);
            var jab = MoveLib.EnemyJab();
            var seqDir = Path.Combine(dir, "seq_read");
            Directory.CreateDirectory(seqDir);
            int saved = 0, reads0 = pc.ReadCount;
            bool shotRead = false;
            pc.Lock.Set(dummy);
            for (int i = 0; i < 40; i++) yield return null;
            for (int i = 0; i < 110; i++)
            {
                if (i == 10) { dummy.Body.SetYaw(HitResolver.Yaw(pc.Me.Position - dummy.Position)); dummy.StartAttack(jab, pc.Me); }
                if (i == 10 + 9 - 6) { pc.SetStickWorld(Vector3.right, 1f); pc.Press(Btn.Dodge); }
                if (i == 10 + 9 - 4) pc.SetStickWorld(Vector3.zero, 0f);
                if (pc.ReadCount > reads0 && i < 40) pc.Press(Btn.Light);
                yield return null;
                if (!shotRead && pc.ReadCount > reads0) { shotRead = true; notes.Add("회피 '읽었다'(슬로 50%): " + Shot(Path.Combine(dir, "read_1_slow.png"), 1920, 1080, 2)); }
                Shot(Path.Combine(seqDir, $"f_{saved:000}.png"), 960, 540, 1); saved++;
            }
            notes.Add($"  연속 읽었다 → 카운터: {seqDir} {saved}장 · 읽었다 {pc.ReadCount - reads0}");
            pc.Lock.Unlock();
        }

        IEnumerator GuardCrush()
        {
            yield return Reset(CombatLab.PlayerStart, CombatLab.PlayerYaw, CombatLab.DummyStart, CombatLab.DummyYaw);
            var jab = MoveLib.EnemyJab();
            var seqDir = Path.Combine(dir, "seq_guard");
            Directory.CreateDirectory(seqDir);
            int saved = 0;
            bool shot = false;
            pc.Lock.Set(dummy);
            for (int i = 0; i < 7 * 30 + 50; i++)
            {
                pc.SetGuard(i < 7 * 30 + 5);
                if (i % 30 == 0 && i < 7 * 30) { dummy.Body.SetYaw(HitResolver.Yaw(pc.Me.Position - dummy.Position)); dummy.StartAttack(jab, pc.Me); }
                yield return null;
                if (!shot && pc.Me.State == Fighter.Phase.Stagger) { shot = true; notes.Add("가드 크러시: " + Shot(Path.Combine(dir, "guard_crush.png"), 1920, 1080, 2) + $" | 가드 게이지 다시 {pc.Me.Guard:F0}"); }
                if (i >= 160) { Shot(Path.Combine(seqDir, $"f_{saved:000}.png"), 960, 540, 1); saved++; }
            }
            pc.SetGuard(false);
            pc.Lock.Unlock();
            notes.Add($"  연속 막기 → 크러시: {seqDir} {saved}장");
        }

        IEnumerator GrabSlam()
        {
            // 벽(z 8.0) 앞: 시우 z 6.0 북쪽 보기, 허수아비 z 6.8 → 잡으면 z 6.55, 등 뒤 벽까지 1.45m
            yield return Reset(new Vector3(0.5f, 0f, 6.0f), 0f, new Vector3(0.5f, 0f, 6.8f), 180f);
            var seqDir = Path.Combine(dir, "seq_grab");
            Directory.CreateDirectory(seqDir);
            int saved = 0, count0 = ImpactFx.Count;
            bool shotHold = false, shotSlam = false;
            for (int i = 0; i < 120; i++)
            {
                if (i == 5) pc.Press(Btn.Grab);
                if (i == 40) pc.Press(Btn.Heavy);
                yield return null;
                if (!shotHold && pc.Held != null && i > 25) { shotHold = true; notes.Add("잡기(멱살): " + Shot(Path.Combine(dir, "grab_1_hold.png"), 1920, 1080, 2)); }
                if (!shotSlam && ImpactFx.Count > count0 && ImpactFx.Last.Move != null && ImpactFx.Last.Move.Label == "벽꽝")
                { shotSlam = true; notes.Add("하체 밀기 → 벽꽝: " + Shot(Path.Combine(dir, "grab_2_slam.png"), 1920, 1080, 2) + $" | 피해 {ImpactFx.Last.Damage} · 허수아비 경직 {dummy.StaggerLeft * 60f:F0}f"); }
                Shot(Path.Combine(seqDir, $"f_{saved:000}.png"), 960, 540, 1); saved++;
            }
            notes.Add($"  연속 잡기 → 밀기 → 벽꽝: {seqDir} {saved}장");
        }

        string Shot(string path, int w, int h, int ss)
        {
            var big = new RenderTexture(new RenderTextureDescriptor(w * ss, h * ss, RenderTextureFormat.ARGB32, 24) { sRGB = true, msaaSamples = 1 });
            var small = new RenderTexture(new RenderTextureDescriptor(w, h, RenderTextureFormat.ARGB32, 0) { sRGB = true });
            big.Create(); small.Create();
            var prevT = cam.targetTexture;
            var prevA = RenderTexture.active;
            var overlays = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToList();
            try
            {
                cam.targetTexture = big;
                foreach (var c in overlays) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = 0.5f; }
                Canvas.ForceUpdateCanvases();
                cam.Render();
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
                foreach (var c in overlays) { c.renderMode = RenderMode.ScreenSpaceOverlay; c.worldCamera = null; }
                cam.targetTexture = prevT;
                RenderTexture.active = prevA;
                big.Release(); small.Release();
                UnityEngine.Object.Destroy(big); UnityEngine.Object.Destroy(small);
            }
            return $"{path} | 시우 ({pc.Me.Position.x:F2},{pc.Me.Position.z:F2}) 상태 {pc.Me.State} · 허수아비 ({dummy.Position.x:F2},{dummy.Position.z:F2}) HP {dummy.Hp} · 시간 배율 {Time.timeScale:F2} · 흔들림 {Shake.Trauma:F2} 펀치 {Shake.Punch:F2} · 카메라 {cam.transform.position}";
        }
    }
}
