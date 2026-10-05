// 행인1의 메인이벤트 — M2 11·12단계 테스트: C10b 이펙트·효과음 연결, C16 HUD (docs/08_M2_전투_설계.md 5-5·5-7·7장·10-5)
// 실제 Player 프리팹(CombatUi = CombatFx + CombatHud, 묶음 Settings/FxKit.asset) + 석 달 모델 허수아비 + 메인 카메라.
//   C10b Fx_Smoke : 잽(약) → 이펙트·의성어 '퍽!'·hit_p1, 전투 첫 타격 쇼크 컷 / 어퍼(강) → 화면 번쩍 / 막힘 → '툭'·block / 다운 착지 → 고리·'쿵!'·slam
//   C16  Hud_Binds: 락온 → 대상 이름·바(= HP 비율 ± 0.01), 기세 100 → MAX 표시, 글꼴 KR_Bold_SDF, 길잡이 HUD 숨김, 조작 안내 = 전투 조작
using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace Haengin.Tests
{
    public sealed class HudTests
    {
        const float Dt = Lab.Dt;
        const string PlayerPath = "Assets/_Project/Prefabs/Player.prefab";
        const string DummyPath = "Assets/_Project/Prefabs/Enemy_Seokdal.prefab";
        CombatTuning tune;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.captureDeltaTime = Dt;
            TimeFx.Reset();
            tune = ScriptableObject.CreateInstance<CombatTuning>();
            yield return Lab.FreshScene();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            TimeFx.Reset();
            GameState.SetPaused(false);
            GameUi.HintOverride = null;
            Time.captureDeltaTime = 0f;
            yield return null;
        }

        static GameObject Load(string path)
        {
            GameObject g = null;
#if UNITY_EDITOR
            g = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
#endif
            if (g == null) Assert.Ignore(path + " 을 에디터에서만 읽을 수 있음");
            return g;
        }

        static PlayerCombat Siwoo()
        {
            var holder = new GameObject("SpawnHolder");
            holder.SetActive(false);
            var p = Object.Instantiate(Load(PlayerPath), Vector3.zero, Quaternion.identity, holder.transform);
            // PInput 은 지운다: CombatMode 가 찾아 맵을 바꾸면 실제 HInput 에셋이 켜져 뒤따르는 InputTests 의 가짜 장치 입력이 막힌다
            foreach (var i in p.GetComponentsInChildren<PInput>(true)) Object.DestroyImmediate(i);
            p.transform.SetParent(null, true);
            Object.Destroy(holder);
            return p.GetComponent<PlayerCombat>();
        }

        Fighter Dummy(Vector3 at, string label = "석 달")
        {
            var model = Object.Instantiate(Load(DummyPath));
            var f = CombatFactory.Dummy(label, at, 180f, 90, model, 1.77f, 0.30f, tune);
            return f;
        }

        static Camera MainCam()
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var c = go.AddComponent<Camera>();
            c.fieldOfView = 45f;
            go.transform.position = new Vector3(2.2f, 1.7f, -3.2f);
            go.transform.LookAt(new Vector3(0f, 1.1f, 0.6f));
            return c;
        }

        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        [UnityTest]
        public IEnumerator C10b_Fx_Smoke()
        {
            Lab.Floor(0f, 40f);
            MainCam();
            var pc = Siwoo();
            var fx = pc.GetComponentInChildren<CombatFx>();
            Assert.IsNotNull(fx, "프리팹에 CombatFx");
            Assert.IsNotNull(fx.Kit, "FxKit 연결");
            var d = Dummy(new Vector3(0f, 0f, 0.9f));
            d.MaxHp = d.Hp = 999;
            yield return Frames(5);
            CombatMode_Begin(pc);
            yield return Frames(20);
            var log = new System.Collections.Generic.List<string>();
            int h0 = CombatFx.Hits, w0 = CombatFx.Words, p0 = CombatFx.Plays, s0 = CombatFx.Shocks, f0 = CombatFx.Flashes;
            pc.Press(Btn.Light);
            yield return Frames(24);
            log.Add($"잽: 이펙트 {CombatFx.Hits - h0} · 의성어 {CombatFx.LastWord} · 소리 {CombatFx.Plays - p0}(마지막 {CombatFx.LastSound}) · 쇼크 컷 {CombatFx.Shocks - s0} · 살아 있는 조각 {fx.Live}");
            Assert.AreEqual(1, CombatFx.Hits - h0, "잽 1번 이펙트");
            Assert.AreEqual("퍽!", CombatFx.LastWord, "잽 의성어");
            Assert.That(CombatFx.Plays - p0, Is.GreaterThanOrEqualTo(2), "휘두름 + 맞음 소리");
            Assert.AreEqual(1, CombatFx.Shocks - s0, "전투 첫 타격 쇼크 컷");
            yield return Frames(40);
            // □△ 어퍼(강) → 화면 번쩍
            f0 = CombatFx.Flashes; s0 = CombatFx.Shocks;
            pc.Press(Btn.Light);
            yield return Frames(12);
            pc.Press(Btn.Heavy);
            yield return Frames(50);
            log.Add($"어퍼: 화면 번쩍 {CombatFx.Flashes - f0} · 쇼크 컷 {CombatFx.Shocks - s0} · 의성어 {CombatFx.LastWord}");
            Assert.That(CombatFx.Flashes - f0, Is.GreaterThanOrEqualTo(1), "강 → 화면 번쩍");
            Assert.AreEqual(0, CombatFx.Shocks - s0, "강은 쇼크 컷 없음(첫 타격·기세만)");
            // 막힘 → '툭'
            yield return Frames(40);
            d.ResetFighter();
            d.Body.Place(new Vector3(0f, 0f, 0.9f), 180f);
            d.AlwaysGuard = true;
            yield return Frames(10);
            pc.Press(Btn.Light);
            yield return Frames(30);
            log.Add($"막힘: 의성어 {CombatFx.LastWord} · 소리 {CombatFx.LastSound}");
            Assert.AreEqual("툭", CombatFx.LastWord, "막힘 '툭'");
            d.AlwaysGuard = false;
            // 다운 착지 → '쿵!' · slam
            int sl0 = CombatFx.Slams;
            d.StartDown(Vector3.forward, 0.3f);
            yield return Frames(60);
            log.Add($"다운 착지: 착지 {CombatFx.Slams - sl0} · 의성어 {CombatFx.LastWord}");
            Assert.AreEqual(1, CombatFx.Slams - sl0, "다운 착지 이펙트·소리 1번");
            Debug.Log("[M2Test] C10b 이펙트·효과음: " + string.Join(" | ", log));
        }

        static void CombatMode_Begin(PlayerCombat pc)
        {
            pc.Begin(0f);
            // 탐색 → 전투(첫 타격 쇼크 컷 다시 세기)
            var m = new GameObject("Combat").AddComponent<CombatMode>();
            m.Player = pc;
            m.Begin(0f);
        }

        [UnityTest]
        public IEnumerator C16_Hud_Binds()
        {
            Lab.Floor(0f, 40f);
            MainCam();
            // 길잡이 HUD(M1): 전투 시작하면 숨어야 함
            var routeGo = new GameObject("길잡이", typeof(RectTransform));
            var rc = routeGo.AddComponent<Canvas>();
            rc.renderMode = RenderMode.ScreenSpaceOverlay;
            var rtext = new GameObject("글", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            rtext.transform.SetParent(routeGo.transform, false);
            var route = routeGo.AddComponent<RouteHud>();
            route.Text = rtext;
            route.Plate = rtext.gameObject;
            var holderUi = new GameObject("UI 화면");
            holderUi.SetActive(false);
            var ui = holderUi.AddComponent<GameUi>();
            var pc = Siwoo();
            var hud = pc.GetComponentInChildren<CombatHud>();
            Assert.IsNotNull(hud, "프리팹에 CombatHud");
            ui.Font = hud.Kit != null ? hud.Kit.Font : null;      // 기본 TMP 글꼴로 한글을 그리면 TMP 기본 대체 글꼴 에셋이 바뀐다
            rtext.font = ui.Font;
            holderUi.SetActive(true);
            var d = Dummy(new Vector3(0f, 0f, 1.6f));
            yield return Frames(5);
            Assert.IsFalse(hud.Visible, "탐색 중 HUD 숨김");
            var m = new GameObject("Combat").AddComponent<CombatMode>();
            m.Player = pc;
            m.Begin(0f);
            yield return Frames(10);
            var log = new System.Collections.Generic.List<string>();
            Assert.IsTrue(hud.Visible, "전투 HUD 보임");
            Assert.IsFalse(route.enabled && rc.enabled, "길잡이 HUD 숨김");
            Assert.AreEqual("KR_Bold_SDF", hud.UsedFont != null ? hud.UsedFont.name : "", "한글 글꼴 KR_Bold_SDF");
            Assert.That(GameUi.HintOverride, Is.EqualTo(CombatHud.PadHint).Or.EqualTo(CombatHud.KeyHint), "조작 안내 = 전투 조작");
            log.Add($"전투 시작: HUD {hud.Visible} · 길잡이 {route.enabled} · 글꼴 {hud.UsedFont?.name} · 안내 \"{GameUi.HintOverride}\" · 시우 바 {hud.HpShown:F2} 기세 {hud.HeatShown:F2}");
            Assert.That(hud.HpShown, Is.EqualTo(1f).Within(0.01f), "시우 체력 가득");
            Assert.That(hud.HeatShown, Is.EqualTo(0.2f).Within(0.01f), "기세 20");

            // 락온 + 피해
            pc.Lock.Set(d);
            d.Hp = 60;
            yield return Frames(5);
            log.Add($"락온: 이름 \"{hud.TargetName}\" · 바 {hud.TargetBar:F3}(HP {d.Hp}/{d.MaxHp})");
            Assert.IsTrue(hud.TargetShown, "락온 대상 이름·바 표시");
            Assert.AreEqual(d.Label, hud.TargetName, "대상 이름");
            Assert.That(hud.TargetBar, Is.EqualTo(d.Hp / (float)d.MaxHp).Within(0.01f), "바 = HP 비율 ± 0.01");

            // 시우 피해 → 바
            pc.Me.Hp = 50;
            yield return Frames(3);
            Assert.That(hud.HpShown, Is.EqualTo(0.25f).Within(0.01f), "시우 바 = HP 비율");

            // 기세 MAX
            Assert.IsFalse(hud.HeatMaxShown, "기세 20 에선 MAX 표시 없음");
            pc.Heat.Set(100f);
            yield return Frames(3);
            log.Add($"기세 MAX: 표시 {hud.HeatMaxShown} · 바 {hud.HeatShown:F2} · 기세 판 {hud.HeatPlateShown}(\"{hud.HeatPlateText}\")");
            Assert.IsTrue(hud.HeatMaxShown, "기세 MAX 판");
            Assert.That(hud.HeatShown, Is.EqualTo(1f).Within(0.01f), "기세 바 가득");
            Assert.IsFalse(hud.HeatPlateShown, "벽·구경꾼이 없으면 「△ 기세」 판 없음");

            // 벽을 대상 뒤에 → 「△ 기세」 판
            var w = Lab.Box("Wall", new Vector3(0f, 1.25f, 1.6f + 0.3f + 1.0f + 0.25f), new Vector3(6f, 2.5f, 0.5f), Quaternion.identity, Layers.Wall);
            w.AddComponent<HeatSurface>();
            Physics.SyncTransforms();
            yield return Frames(15);
            var hk = pc.HeatAct.Check(out var htg);
            log.Add($"벽 앞: 기세 판 {hud.HeatPlateShown}(\"{hud.HeatPlateText}\") · 조건 {hk} 대상 {htg?.Label} · 시우 {pc.Me.Position} 상태 {pc.Me.State} · 대상 {d.Position}");
            if (!hud.HeatPlateShown) Debug.Log("[M2Test] C16 HUD(실패 전): " + string.Join(" | ", log));
            Assert.IsTrue(hud.HeatPlateShown, "조건이 맞으면 기세 판");
            StringAssert.Contains("기세", hud.HeatPlateText);

            // 전투 끝 → 숨김, 길잡이 다시
            m.End();
            yield return Frames(5);
            Assert.IsFalse(hud.Visible, "전투 끝 → HUD 숨김");
            Assert.IsTrue(route.enabled, "길잡이 HUD 다시");
            Assert.IsNull(GameUi.HintOverride, "조작 안내 탐색으로");
            Debug.Log("[M2Test] C16 HUD: " + string.Join(" | ", log));
            Object.Destroy(ui.gameObject);
        }
    }
}
