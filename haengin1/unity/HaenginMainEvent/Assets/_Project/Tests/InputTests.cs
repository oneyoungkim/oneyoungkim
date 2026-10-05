// 행인1의 메인이벤트 — 입력 연기 시험 (docs/07_M1_조작_설계.md 9-2 T16·T20·T23)
// 가짜 패드·키보드(InputTestFixture) → 실제 HInput 에셋 → PInput·CamInput → 모터·카메라까지 한 번 통과하는지(T16),
// 일시정지 메뉴를 패드·키보드만으로 열고·고르고·닫는지(T20)를 본다.
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Haengin.Tests
{
    public sealed class InputTests : InputTestFixture
    {
        const string InputPath = "Assets/_Project/Input/HInput.inputactions";

        [UnityTest]
        public IEnumerator T16_Input_Smoke()
        {
            Time.captureDeltaTime = Lab.Dt;
            yield return Lab.FreshScene();
            InputActionAsset asset = null;
#if UNITY_EDITOR
            asset = UnityEditor.AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
#endif
            if (asset == null) { Assert.Ignore("입력 에셋을 에디터에서만 읽을 수 있음"); yield break; }
            asset.Disable();

            var gp = InputSystem.AddDevice<Gamepad>();
            var kb = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();

            Lab.Floor(0f, 140f);
            var cap = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Object.DestroyImmediate(cap.GetComponent<Collider>());
            var r = RigFactory.Build(new Vector3(0f, 0f, -60f), 0f, ScriptableObject.CreateInstance<MoveTuning>(),
                                     ScriptableObject.CreateInstance<CamTuning>(), cap, asset);
            yield return Lab.Frames(5);

            // 패드: 왼스틱 앞으로 → 걷기, R2 → 달리기, 오른스틱 오른쪽 → 카메라 yaw 증가
            Set(gp.leftStick, new Vector2(0f, 1f));
            yield return Lab.Seconds(0.6f);
            float padWalk = r.Motor.PlanarSpeed;
            Press(gp.rightTrigger);
            yield return Lab.Seconds(1.0f);
            float padRun = r.Motor.PlanarSpeed;
            Release(gp.rightTrigger);
            Set(gp.leftStick, Vector2.zero);
            yield return Lab.Seconds(0.6f);
            float yaw0 = r.CamRig.Yaw;
            Set(gp.rightStick, new Vector2(1f, 0f));
            yield return Lab.Seconds(0.5f);
            float yaw1 = r.CamRig.Yaw;
            Set(gp.rightStick, Vector2.zero);
            yield return Lab.Seconds(0.3f);

            // 키보드: W → 걷기, W + Shift → 달리기
            Press(kb.wKey);
            yield return Lab.Seconds(0.6f);
            float kbWalk = r.Motor.PlanarSpeed;
            Press(kb.leftShiftKey);
            yield return Lab.Seconds(1.0f);
            float kbRun = r.Motor.PlanarSpeed;
            Release(kb.leftShiftKey);
            Release(kb.wKey);
            yield return null;

            asset.Disable();
            Time.captureDeltaTime = 0f;
            Debug.Log($"[M1Test] T16 입력: 패드 걷기 {padWalk:F2} · R2 달리기 {padRun:F2} · 오른스틱 yaw {yaw0:F1}→{yaw1:F1} · 키보드 W {kbWalk:F2} · W+Shift {kbRun:F2}");
            Assert.That(padWalk, Is.GreaterThan(1.5f), "패드 왼스틱 걷기");
            Assert.That(padRun, Is.GreaterThan(4.0f), "패드 R2 달리기");
            Assert.That(Mathf.DeltaAngle(yaw0, yaw1), Is.GreaterThan(20f), "패드 오른스틱 카메라 회전");
            Assert.That(kbWalk, Is.GreaterThan(1.5f), "키보드 W 걷기");
            Assert.That(kbRun, Is.GreaterThan(4.0f), "키보드 Shift 달리기");
        }

        /// T23 (3차 검수 재현): 키보드 W+D 를 3초 누르고 있기 — 실제 HInput → PInput → CamRig.StickToWorld → 모터. 자동 정렬이 카메라를 돌려도 이동 방향은 그대로
        [UnityTest]
        public IEnumerator T23_Keys_WD_NoSpiral()
        {
            Time.captureDeltaTime = Lab.Dt;
            yield return Lab.FreshScene();
            InputActionAsset asset = null;
#if UNITY_EDITOR
            asset = UnityEditor.AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
#endif
            if (asset == null) { Assert.Ignore("입력 에셋을 에디터에서만 읽을 수 있음"); yield break; }
            asset.Disable();
            var kb = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            Lab.Floor(0f, 200f);
            var cap = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Object.DestroyImmediate(cap.GetComponent<Collider>());
            var r = RigFactory.Build(new Vector3(0f, 0f, -60f), 0f, ScriptableObject.CreateInstance<MoveTuning>(),
                                     ScriptableObject.CreateInstance<CamTuning>(), cap, asset);
            yield return Lab.Frames(5);
            float cam0 = r.CamRig.Yaw;
            Press(kb.wKey);
            Press(kb.dKey);
            float first = float.NaN, last = 0f, drift = 0f;
            for (int i = 0; i < Mathf.RoundToInt(3f / Lab.Dt); i++)
            {
                yield return null;
                var v = r.Motor.Velocity;
                if (new Vector2(v.x, v.z).magnitude < 1.0f) continue;
                float h = Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
                if (float.IsNaN(first)) first = h;
                drift = Mathf.Max(drift, Mathf.Abs(Mathf.DeltaAngle(first, h)));
                last = h;
            }
            Release(kb.dKey);
            Release(kb.wKey);
            yield return null;
            float camTurn = Mathf.Abs(Mathf.DeltaAngle(cam0, r.CamRig.Yaw));
            asset.Disable();
            Time.captureDeltaTime = 0f;
            Debug.Log($"[M1Test] T23 키보드 W+D 3초: 이동 방향 {first:F1}° → {last:F1}° · 최대 변화 {drift:F1}° · 카메라 자동 정렬 {camTurn:F1}°");
            Assert.That(camTurn, Is.GreaterThanOrEqualTo(20f), "자동 정렬이 카메라를 돌림(시험 조건)");
            Assert.That(drift, Is.LessThanOrEqualTo(15f), "W+D 3초: 이동 방향이 처음에서 15° 넘게 돌지 않음(예전: 45° → 119°)");
        }

        [UnityTest]
        public IEnumerator T20_PauseMenu_PadKeys()
        {
            Time.captureDeltaTime = Lab.Dt;
            yield return Lab.FreshScene();
            InputActionAsset asset = null;
            TMPro.TMP_FontAsset font = null;
#if UNITY_EDITOR
            asset = UnityEditor.AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
            font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/_Project/Fonts/KR_Bold_SDF.asset");
#endif
            if (asset == null) { Assert.Ignore("입력 에셋을 에디터에서만 읽을 수 있음"); yield break; }
            asset.Disable();
            var gp = InputSystem.AddDevice<Gamepad>();
            var kb = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();

            Lab.Floor(0f, 60f);
            var cap = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Object.DestroyImmediate(cap.GetComponent<Collider>());
            var r = RigFactory.Build(Vector3.zero, 0f, ScriptableObject.CreateInstance<MoveTuning>(), ScriptableObject.CreateInstance<CamTuning>(), cap, asset);
            var ui = new GameObject("UI 화면").AddComponent<GameUi>();
            ui.Actions = asset; ui.Font = font; ui.Cam = r.CamRig;
            bool quit = false;
            GameUi.QuitHook = () => quit = true;
            yield return Lab.Frames(5);
            var log = new System.Text.StringBuilder();
            try
            {
                // ── 패드: Options 로 열기
                yield return Tap(gp.startButton);
                log.Append($"Options → 일시정지 {GameState.Paused}·메뉴 {ui.MenuVisible}·고름 {ui.Selected}; ");
                Assert.IsTrue(GameState.Paused, "패드 Options → 일시정지");
                Assert.IsTrue(ui.MenuVisible, "메뉴 보임");
                Assert.AreEqual(0, ui.Selected, "처음 고른 항목 = 계속");
                Assert.AreEqual(0f, Time.timeScale, "시간 정지");

                // 십자키 아래 두 번 → 끝내기, 왼스틱 위 → 카메라 자동 정렬
                yield return Tap(gp.dpad.down);
                Assert.AreEqual(1, ui.Selected, "십자키 ↓ → 2번째");
                yield return Tap(gp.dpad.down);
                Assert.AreEqual(2, ui.Selected, "십자키 ↓ → 끝내기");
                Set(gp.leftStick, new Vector2(0f, 1f));
                yield return Lab.Frames(2);
                Set(gp.leftStick, Vector2.zero);
                yield return Lab.Frames(2);
                Assert.AreEqual(1, ui.Selected, "왼스틱 ↑ → 카메라 자동 정렬");

                // × (A) → 자동 정렬 바꾸기(걷기·달리기 → 달리기만), 십자키 → 로 한 번 더(→ 끔)
                var m0 = r.CamRig.AutoMode;
                yield return Tap(gp.buttonSouth);
                var m1 = r.CamRig.AutoMode;
                yield return Tap(gp.dpad.right);
                var m2 = r.CamRig.AutoMode;
                log.Append($"자동 정렬 {m0} → × {m1} → → {m2} (글 \"{ui.Label(1)}\"); ");
                Assert.AreEqual(CamTuning.Auto.Always, m0, "기본 = 걷기·달리기");
                Assert.AreEqual(CamTuning.Auto.RunOnly, m1, "× 로 다음 값");
                Assert.AreEqual(CamTuning.Auto.Off, m2, "→ 로 다음 값");
                Assert.IsTrue(GameState.Paused, "옵션을 바꿔도 메뉴 유지");
                yield return Tap(gp.dpad.left);
                yield return Tap(gp.dpad.left);
                Assert.AreEqual(CamTuning.Auto.Always, r.CamRig.AutoMode, "← 두 번 → 끔 → 달리기만 → 걷기·달리기(이전 값으로)");

                // 끝내기: 아래로 가서 ×
                yield return Tap(gp.dpad.down);
                yield return Tap(gp.buttonSouth);
                Assert.IsTrue(quit, "패드로 끝내기 선택");

                // ○ (B) → 닫기
                yield return Tap(gp.buttonEast);
                Assert.IsFalse(GameState.Paused, "○ (B) → 계속");
                Assert.IsFalse(ui.MenuVisible, "메뉴 닫힘");
                Assert.AreEqual(1f, Time.timeScale, "시간 다시 흐름");

                // ── 키보드: Esc 열기 → ↓ ↑ → Enter(계속)
                yield return Tap(kb.escapeKey);
                Assert.IsTrue(GameState.Paused, "Esc → 일시정지");
                yield return Tap(kb.downArrowKey);
                Assert.AreEqual(1, ui.Selected, "↓");
                yield return Tap(kb.upArrowKey);
                Assert.AreEqual(0, ui.Selected, "↑");
                yield return Tap(kb.enterKey);
                Assert.IsFalse(GameState.Paused, "Enter(계속) → 닫힘");

                // 키보드로 끝내기: Esc → ↓↓ → Space
                quit = false;
                yield return Tap(kb.escapeKey);
                yield return Tap(kb.sKey);
                yield return Tap(kb.downArrowKey);
                Assert.AreEqual(2, ui.Selected, "S·↓ → 끝내기");
                yield return Tap(kb.spaceKey);
                Assert.IsTrue(quit, "키보드로 끝내기 선택");
                // Esc 로 닫기 + 메뉴에서 누른 × 가 상호작용 알림으로 새지 않았는지
                yield return Tap(kb.escapeKey);
                Assert.IsFalse(GameState.Paused, "Esc → 닫힘");
                log.Append($"끝내기 선택 {quit} · 알림 \"{DebugHud.ToastText}\"");
                Assert.IsFalse((DebugHud.ToastText ?? "").Contains("상호작용"), "메뉴의 × 가 상호작용으로 새지 않음");
                Debug.Log("[M1Test] T20 일시정지 메뉴(패드·키보드): " + log);
            }
            finally
            {
                GameUi.QuitHook = null;
                GameState.SetPaused(false);
                asset.Disable();
                Time.captureDeltaTime = 0f;
            }
        }

        /// 한 번 눌렀다 떼기(프레임 사이에 처리되는 장치 입력 — GameUi.Edge 가 잡는다)
        IEnumerator Tap(UnityEngine.InputSystem.Controls.ButtonControl b)
        {
            Press(b);
            yield return Lab.Frames(2);
            Release(b);
            yield return Lab.Frames(2);
        }
    }
}
