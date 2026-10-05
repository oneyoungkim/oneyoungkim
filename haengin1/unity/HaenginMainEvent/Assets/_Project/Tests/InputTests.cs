// 행인1의 메인이벤트 — 입력 연기 시험 (docs/07_M1_조작_설계.md 9-2 T16)
// 가짜 패드·키보드(InputTestFixture) → 실제 HInput 에셋 → PInput·CamInput → 모터·카메라까지 한 번 통과하는지만 본다.
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
    }
}
