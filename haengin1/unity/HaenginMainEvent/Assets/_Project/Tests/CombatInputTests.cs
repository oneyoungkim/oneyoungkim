// 행인1의 메인이벤트 — 전투 입력 연기 시험 C21 (docs/08_M2_전투_설계.md 2-2·10-5)
// 가짜 패드·키보드·마우스(InputTestFixture) → 실제 HInput 에셋(Combat 맵) → PInput → PlayerCombat 까지 각 기술이 시작되는지.
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.TestTools;

namespace Haengin.Tests
{
    public sealed class CombatInputTests : InputTestFixture
    {
        const string InputPath = "Assets/_Project/Input/HInput.inputactions";

        PlayerCombat pc;
        AttackRun lastRun;
        readonly List<string> started = new List<string>();

        IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++)
            {
                yield return null;
                var r = pc.Me.Run;
                if (r != null && r != lastRun) started.Add(r.Move.Label);
                lastRun = r;
            }
        }

        IEnumerator Tap(ButtonControl b, int after = 1)
        {
            Press(b);
            yield return Frames(1);
            Release(b);
            yield return Frames(after);
        }

        [UnityTest]
        public IEnumerator C21_Input_Smoke_Combat()
        {
            Time.captureDeltaTime = Lab.Dt;
            TimeFx.Reset();
            yield return Lab.FreshScene();
            InputActionAsset asset = null;
#if UNITY_EDITOR
            asset = UnityEditor.AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
#endif
            if (asset == null) { Assert.Ignore("입력 에셋을 에디터에서만 읽을 수 있음"); yield break; }
            asset.Disable();
            Assert.IsNotNull(asset.FindActionMap("Combat", false), "Combat 맵");
            var gp = InputSystem.AddDevice<Gamepad>();
            var kb = InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.AddDevice<Mouse>();

            Lab.Floor(0f, 40f);
            var cap = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Object.DestroyImmediate(cap.GetComponent<Collider>());
            var tune = ScriptableObject.CreateInstance<CombatTuning>();
            var r = RigFactory.Build(Vector3.zero, 0f, ScriptableObject.CreateInstance<MoveTuning>(), ScriptableObject.CreateInstance<CamTuning>(), cap, asset);
            pc = CombatFactory.AddPlayer(r.Player, tune, MoveSet.CreateDefault());
            var d = CombatFactory.Dummy("허수아비", new Vector3(0f, 0f, 1.1f), 180f, 999, null, 1.8f, 0.3f, tune);
            yield return Frames(3);
            r.Input.SetCombatMap(true);
            pc.Begin(0f);
            Assert.IsFalse(asset.FindActionMap("Explore").enabled, "전투 중 Explore 꺼짐");
            Assert.IsTrue(asset.FindActionMap("Combat").enabled, "Combat 켜짐");
            yield return Frames(3);
            var log = new List<string>();
            string Last() => started.Count > 0 ? started[started.Count - 1] : "없음";

            // 패드: □□□□ (연결 창마다)
            started.Clear();
            for (int k = 0; k < 4; k++)
            {
                yield return Tap(gp.buttonWest, 1);
                // 다음 □ 는 연결 창이 열린 뒤(타이밍 시험은 C01·C03 — 여기서는 입력이 기술까지 가는지만)
                for (int w = 0; w < 80 && k < 3 && !(pc.Me.Run != null && pc.Me.Run.LinkOpen); w++) yield return Frames(1);
            }
            yield return Frames(40);
            log.Add("□□□□ → " + string.Join("·", started));
            Assert.That(started, Is.EqualTo(new[] { "잽", "크로스", "훅", "크로스(끝)" }), "패드 □□□□");
            yield return Frames(30);
            started.Clear(); yield return Tap(gp.buttonNorth, 50); log.Add("△ → " + Last());
            Assert.AreEqual("앞차기", Last(), "패드 △");
            d.ResetFighter(); d.Body.Place(new Vector3(0f, 0f, 1.0f), 180f);
            ((PlayerBody)pc.Me.Body).Place(Vector3.zero, 0f);
            yield return Frames(30);
            started.Clear(); yield return Tap(gp.buttonEast, 20); log.Add("○ → " + Last() + (pc.Held != null ? "(잡음)" : ""));
            Assert.AreEqual("멱살 잡기", Last(), "패드 ○");
            yield return Frames(160);
            yield return Tap(gp.buttonSouth, 1);
            bool dodged = pc.Dodging; log.Add("× → 회피 " + dodged);
            Assert.IsTrue(dodged, "패드 ×");
            yield return Frames(40);
            Press(gp.rightShoulder); yield return Frames(3);
            bool guard = pc.Me.Guarding; Release(gp.rightShoulder); log.Add("R1 → 막기 " + guard);
            Assert.IsTrue(guard, "패드 R1");
            yield return Frames(5);
            yield return Tap(gp.leftShoulder, 2);
            bool locked = pc.Lock.Target != null; log.Add("L1 → 락온 " + locked);
            Assert.IsTrue(locked, "패드 L1");
            yield return Tap(gp.leftShoulder, 2);
            Assert.IsNull(pc.Lock.Target, "패드 L1 다시 = 풀림");

            // 키보드·마우스
            yield return Frames(30);
            started.Clear(); yield return Tap(mouse.leftButton, 30); log.Add("좌클릭 → " + Last());
            Assert.AreEqual("잽", Last(), "좌클릭");
            yield return Frames(20);
            started.Clear(); yield return Tap(mouse.rightButton, 50); log.Add("우클릭 → " + Last());
            Assert.AreEqual("앞차기", Last(), "우클릭");
            d.ResetFighter(); d.Body.Place(new Vector3(0f, 0f, 1.0f), 180f);
            ((PlayerBody)pc.Me.Body).Place(Vector3.zero, 0f);
            yield return Frames(30);
            started.Clear(); yield return Tap(kb.fKey, 20); log.Add("F → " + Last());
            Assert.AreEqual("멱살 잡기", Last(), "F");
            yield return Frames(160);
            yield return Tap(kb.spaceKey, 1);
            dodged = pc.Dodging; log.Add("Space → 회피 " + dodged);
            Assert.IsTrue(dodged, "Space");
            yield return Frames(40);
            Press(kb.leftCtrlKey); yield return Frames(3);
            guard = pc.Me.Guarding; Release(kb.leftCtrlKey); log.Add("왼쪽 Ctrl → 막기 " + guard);
            Assert.IsTrue(guard, "왼쪽 Ctrl");
            yield return Frames(5);
            yield return Tap(kb.qKey, 2);
            locked = pc.Lock.Target != null; log.Add("Q → 락온 " + locked);
            Assert.IsTrue(locked, "Q");

            asset.Disable();
            TimeFx.Reset();
            Time.captureDeltaTime = 0f;
            Debug.Log("[M2Test] C21 전투 입력: " + string.Join(" / ", log));
        }
    }
}
