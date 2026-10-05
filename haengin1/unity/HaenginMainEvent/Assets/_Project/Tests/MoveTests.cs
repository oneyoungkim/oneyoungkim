// 행인1의 메인이벤트 — 이동·카메라 기본 테스트(코드로 만든 시험장, docs/07_M1_조작_설계.md 9-2 의 T01~T13 중 일부)
// 시간은 1/60초 고정(Time.captureDeltaTime) → 결과가 매번 같다. 입력은 PlayerMotor.SetMoveInput 으로 주입.
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Haengin.Tests
{
    public sealed class MoveTests
    {
        const float Dt = Lab.Dt;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.captureDeltaTime = Dt;
            yield return Lab.FreshScene();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.captureDeltaTime = 0f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator T01_Feet_OnFloor()
        {
            Lab.Floor();
            var r = Lab.Rig(new Vector3(0f, 1f, 0f), 0f);
            yield return Lab.Seconds(0.5f);
            Debug.Log($"[M1Test] T01 발 높이 {r.Motor.Position.y:F4} 접지 {r.Motor.Grounded}");
            Assert.IsTrue(r.Motor.Grounded, "0.5초 뒤 접지");
            Assert.That(r.Motor.Position.y, Is.EqualTo(0f).Within(0.015f), "루트(발) y = 바닥 y");
        }

        [UnityTest]
        public IEnumerator T02_Walk_Speed()
        {
            Lab.Floor();
            var r = Lab.Rig(new Vector3(0f, 0f, -30f), 0f);
            var m = r.Motor;
            yield return Lab.Seconds(0.3f);
            float t = 0f, t95 = -1f;
            Vector3 mark = Vector3.zero;
            int n = Mathf.RoundToInt(2f / Dt);
            for (int i = 0; i < n; i++)
            {
                m.SetMoveInput(Vector3.forward, 1f, false);
                yield return null;
                t += Dt;
                if (t95 < 0f && m.PlanarSpeed >= 0.95f * 1.6f) t95 = t;
                if (i == n - 61) mark = m.Position;
            }
            float avg = Lab.Flat(m.Position - mark).magnitude / (60 * Dt);
            Debug.Log($"[M1Test] T02 걷기 평균 {avg:F3} m/s, 95% 도달 {t95:F3}s");
            Assert.That(avg, Is.EqualTo(1.6f).Within(0.03f), "걷기 속도");
            Assert.That(t95, Is.InRange(0f, 0.17f), "0 → 걷기 95% 시간");
        }

        [UnityTest]
        public IEnumerator T03_Run_StartStop()
        {
            Lab.Floor();
            var r = Lab.Rig(new Vector3(0f, 0f, -35f), 0f);
            var m = r.Motor;
            yield return Lab.Seconds(0.3f);
            float t = 0f, t95 = -1f;
            Vector3 mark = Vector3.zero;
            int n = Mathf.RoundToInt(3f / Dt);
            for (int i = 0; i < n; i++)
            {
                m.SetMoveInput(Vector3.forward, 1f, true);
                yield return null;
                t += Dt;
                if (t95 < 0f && m.PlanarSpeed >= 0.95f * 4.5f) t95 = t;
                if (i == n - 61) mark = m.Position;
            }
            float avg = Lab.Flat(m.Position - mark).magnitude / (60 * Dt);
            Assert.AreEqual(MoveState.Run, m.State, "달리는 중 상태");
            var stopFrom = m.Position;
            float ts = 0f, tStop = -1f;
            for (int i = 0; i < 60; i++)
            {
                yield return null;
                ts += Dt;
                if (tStop < 0f && m.PlanarSpeed < 0.05f) tStop = ts;
            }
            float stopDist = Lab.Flat(m.Position - stopFrom).magnitude;
            Debug.Log($"[M1Test] T03 달리기 평균 {avg:F3} m/s, 95% 도달 {t95:F3}s, 정지 {tStop:F3}s·{stopDist:F3}m");
            Assert.That(avg, Is.EqualTo(4.5f).Within(0.05f), "달리기 속도");
            Assert.That(t95, Is.InRange(0f, 0.62f), "0 → 달리기 95% 시간");
            Assert.That(tStop, Is.InRange(0f, 0.32f), "정지 시간");
            Assert.That(stopDist, Is.LessThanOrEqualTo(0.65f), "정지 거리");
            Assert.AreEqual(MoveState.Idle, m.State, "멈춘 뒤 상태");
        }

        [UnityTest]
        public IEnumerator T04_Turn_180()
        {
            Lab.Floor();
            var r = Lab.Rig(Vector3.zero, 0f);
            var m = r.Motor;
            yield return Lab.Seconds(0.3f);
            float walkT = -1f, runT = -1f;
            foreach (bool run in new[] { false, true })
            {
                m.Teleport(Vector3.zero, 0f);
                yield return Lab.Seconds(0.3f);
                float t = 0f;
                for (int i = 0; i < 60; i++)
                {
                    m.SetMoveInput(Vector3.back, 1f, run);
                    yield return null;
                    t += Dt;
                    if (Mathf.Abs(Mathf.DeltaAngle(m.Yaw, 180f)) <= 5f) { if (run) runT = t; else walkT = t; break; }
                }
            }
            Debug.Log($"[M1Test] T04 180° 돌기 걷기 {walkT:F3}s 달리기 {runT:F3}s");
            Assert.That(walkT, Is.InRange(0f, 0.30f), "걷기 180°");
            Assert.That(runT, Is.InRange(0f, 0.40f), "달리기 180°");
        }

        [UnityTest]
        public IEnumerator T05_Slopes()
        {
            Lab.Floor();
            var r = Lab.Rig(new Vector3(0f, 0f, -30f), 0f);
            var m = r.Motor;
            float[] angles = { 30f, 35f, 45f, 50f };
            var result = new float[angles.Length];
            for (int k = 0; k < angles.Length; k++)
            {
                float x = -12f + k * 8f;
                Lab.Ramp(x, 0f, angles[k]);
                Physics.SyncTransforms();
                m.Teleport(new Vector3(x, 0f, -2f), 0f);
                yield return Lab.Seconds(0.2f);
                float maxY = 0f;
                yield return Lab.Hold(m, Vector3.forward, 8f, false, () => maxY = Mathf.Max(maxY, m.Position.y));   // 경사판 수평 6.9m + 출발 2m ÷ 1.6m/s ≈ 5.6초
                result[k] = maxY;
            }
            Debug.Log($"[M1Test] T05 경사 최고 높이 30° {result[0]:F2} · 35° {result[1]:F2} · 45° {result[2]:F2} · 50° {result[3]:F2} (꼭대기 30° {8f * Mathf.Sin(30f * Mathf.Deg2Rad):F2} · 35° {8f * Mathf.Sin(35f * Mathf.Deg2Rad):F2})");
            Assert.That(result[0], Is.GreaterThan(8f * Mathf.Sin(30f * Mathf.Deg2Rad) - 0.3f), "30° 오름");
            Assert.That(result[1], Is.GreaterThan(8f * Mathf.Sin(35f * Mathf.Deg2Rad) - 0.3f), "35° 오름");
            Assert.That(result[2], Is.LessThan(0.5f), "45° 못 오름");
            Assert.That(result[3], Is.LessThan(0.5f), "50° 못 오름");
        }

        [UnityTest]
        public IEnumerator T06_Steps()
        {
            Lab.Floor();
            var r = Lab.Rig(new Vector3(0f, 0f, -30f), 0f);
            var m = r.Motor;
            float[] h = { 0.15f, 0.30f, 0.45f };
            var endY = new float[h.Length];
            var endZ = new float[h.Length];
            for (int k = 0; k < h.Length; k++)
            {
                float x = -6f + k * 6f;
                Lab.Box($"Step{h[k]}", new Vector3(x, h[k] / 2f, 4f), new Vector3(3f, h[k], 4f), Quaternion.identity, Layers.Ground);
                Physics.SyncTransforms();
                m.Teleport(new Vector3(x, 0f, 0f), 0f);
                yield return Lab.Seconds(0.2f);
                yield return Lab.Hold(m, Vector3.forward, 2.5f, false);
                endY[k] = m.Position.y;
                endZ[k] = m.Position.z;
            }
            Debug.Log($"[M1Test] T06 턱 0.15 → y {endY[0]:F3} z {endZ[0]:F2} · 0.30 → y {endY[1]:F3} z {endZ[1]:F2} · 0.45 → y {endY[2]:F3} z {endZ[2]:F2}");
            Assert.That(endY[0], Is.EqualTo(0.15f).Within(0.03f), "0.15 턱 오름");
            Assert.That(endY[1], Is.EqualTo(0.30f).Within(0.03f), "0.30 턱 오름");
            Assert.That(endY[2], Is.LessThan(0.05f), "0.45 턱 막힘");
            Assert.That(endZ[2], Is.LessThan(2.0f), "0.45 턱 앞에서 멈춤");
        }

        [UnityTest]
        public IEnumerator T07_Alley_NoWallClip()
        {
            Lab.Floor();
            // 폭 2.4m 골목(벽 높이 6m, Wall)
            Lab.Box("WallL", new Vector3(-1.45f, 3f, 0f), new Vector3(0.5f, 6f, 40f), Quaternion.identity, Layers.Wall);
            Lab.Box("WallR", new Vector3(1.45f, 3f, 0f), new Vector3(0.5f, 6f, 40f), Quaternion.identity, Layers.Wall);
            var r = Lab.Rig(new Vector3(0f, 0f, -15f), 0f);
            var m = r.Motor;
            yield return Lab.Seconds(0.5f);
            int bad = 0, frames = 0;
            string first = null;
            int mask = Layers.Mask(Layers.Wall, Layers.Ground);
            void Check()
            {
                frames++;
                var cp = r.CamRig.CameraPosition;
                bool inside = Physics.CheckSphere(cp, 0.05f, mask, QueryTriggerInteraction.Ignore);
                bool blocked = Physics.Linecast(cp, r.CamTarget.position, mask, QueryTriggerInteraction.Ignore);
                if (inside || blocked) { bad++; first ??= $"{frames}프레임 카메라 {cp} 안={inside} 가림={blocked}"; }
            }
            // 1) 골목을 걸으며 카메라를 초당 90° 로 돌리기(8초 = 두 바퀴)
            for (int i = 0; i < Mathf.RoundToInt(8f / Dt); i++)
            {
                m.SetMoveInput(Vector3.forward, 1f, false);
                r.Orbit.HorizontalAxis.Value = Mathf.DeltaAngle(0f, r.Orbit.HorizontalAxis.Value + 90f * Dt);
                yield return null;
                if (i > 20) Check();
            }
            // 2) 등을 벽에 붙이고 서서 한 바퀴
            m.Teleport(new Vector3(1.2f - 0.27f, 0f, 0f), -90f);
            r.CamRig.SnapBehind();
            yield return Lab.Seconds(0.5f);
            for (int i = 0; i < Mathf.RoundToInt(4f / Dt); i++)
            {
                r.Orbit.HorizontalAxis.Value = Mathf.DeltaAngle(0f, r.Orbit.HorizontalAxis.Value + 90f * Dt);
                yield return null;
                Check();
            }
            Debug.Log($"[M1Test] T07 골목 카메라 검사 {frames}프레임 중 벽 안·가림 {bad} {first}");
            Assert.AreEqual(0, bad, "카메라가 벽 안에 들어가거나 벽에 가린 프레임 " + first);
        }

        [UnityTest]
        public IEnumerator T08_Framing_And_Fov()
        {
            Lab.Floor();
            var r = Lab.Rig(new Vector3(0f, 0f, -35f), 0f);
            var m = r.Motor;
            yield return Lab.Seconds(1f);
            var vp = r.Main.WorldToViewportPoint(r.CamTarget.position);
            yield return Lab.Hold(m, Vector3.forward, 2.5f, true);
            float fovRun = r.Cam.Lens.FieldOfView;
            yield return Lab.Seconds(1.5f);
            float fovStop = r.Cam.Lens.FieldOfView;
            Debug.Log($"[M1Test] T08 구도 뷰포트 ({vp.x:F3}, {vp.y:F3}) 거리 {r.CamRig.Distance:F2}m · FOV 달리기 {fovRun:F2} 정지 {fovStop:F2}");
            Assert.That(vp.x, Is.EqualTo(0.44f).Within(0.02f), "인물 가로 위치(중앙에서 6% 왼쪽)");
            Assert.That(vp.y, Is.EqualTo(0.50f).Within(0.02f), "인물 세로 위치");
            Assert.That(fovRun, Is.EqualTo(49f).Within(0.5f), "달리기 FOV");
            Assert.That(fovStop, Is.EqualTo(45f).Within(0.5f), "정지 FOV");
        }

        [UnityTest]
        public IEnumerator T09_MoveBasis_NoDrift()
        {
            Lab.Floor(0f, 120f);
            var r = Lab.Rig(new Vector3(0f, 0f, -50f), 0f);
            var m = r.Motor;
            yield return Lab.Seconds(0.5f);
            float yaw0 = m.Yaw;
            for (int i = 0; i < Mathf.RoundToInt(6f / Dt); i++)
            {
                m.SetMoveInput(r.CamRig.MoveBasis * Vector3.forward, 1f, true);   // PInput 과 같은 계산(스틱 앞으로)
                yield return null;
            }
            float drift = Mathf.Abs(Mathf.DeltaAngle(yaw0, m.Yaw));
            float camGap = Mathf.Abs(Mathf.DeltaAngle(r.CamRig.Yaw, m.Yaw));
            Debug.Log($"[M1Test] T09 6초 달리기 방향 변화 {drift:F2}° · 카메라-인물 차 {camGap:F2}°");
            Assert.That(drift, Is.LessThanOrEqualTo(1f), "앞으로 달리는 동안 인물 방향 변화");
            Assert.That(camGap, Is.LessThanOrEqualTo(2f), "카메라 궤도와 인물 방향 차");
        }

        [UnityTest]
        public IEnumerator T10_Ledge_Fall_Respawn()
        {
            Lab.Box("Platform", new Vector3(0f, -0.5f, 0f), new Vector3(10f, 1f, 10f), Quaternion.identity, Layers.Ground);
            var r = Lab.Rig(Vector3.zero, 0f);
            var m = r.Motor;
            m.KillY = -10f;
            bool fell = false;
            m.StateChanged += (a, b) => { if (b == MoveState.Fall) fell = true; };
            yield return Lab.Seconds(0.5f);
            for (int i = 0; i < Mathf.RoundToInt(8f / Dt) && m.Respawns == 0; i++)
            {
                m.SetMoveInput(Vector3.forward, 1f, true);
                yield return null;
            }
            yield return Lab.Seconds(1f);
            Debug.Log($"[M1Test] T10 낙하 상태 거침 {fell} · 리스폰 {m.Respawns}회 · 위치 {m.Position} · 접지 {m.Grounded}");
            Assert.IsTrue(fell, "Fall 상태를 거침");
            Assert.That(m.Respawns, Is.GreaterThanOrEqualTo(1), "맵 밖 → 리스폰");
            Assert.IsTrue(m.Grounded, "리스폰 뒤 접지");
            Assert.That(m.Position.y, Is.EqualTo(0f).Within(0.05f), "플랫폼 위");
            Assert.That(Mathf.Abs(m.Position.z), Is.LessThan(5f), "플랫폼 안");
        }

        [UnityTest]
        public IEnumerator T11_Curb_VisualSmooth()
        {
            Lab.Floor();
            Lab.Box("Curb", new Vector3(0f, 0.075f, 5f), new Vector3(4f, 0.15f, 6f), Quaternion.identity, Layers.Ground);
            var r = Lab.Rig(new Vector3(0f, 0f, 0f), 0f);
            var m = r.Motor;
            yield return Lab.Seconds(0.3f);
            // 코루틴은 Update 뒤·LateUpdate 앞에 돈다 → 지난 프레임에 그려진 비주얼 높이 = 지난 프레임 루트 y + 지금 Visual 로컬 y(지난 LateUpdate 값)
            float prevRoot = m.Position.y, prevDrawn = float.NaN, maxJump = 0f, maxRoot = 0f;
            yield return Lab.Hold(m, Vector3.forward, 3f, false, () =>
            {
                float drawn = prevRoot + r.Visual.localPosition.y;
                if (!float.IsNaN(prevDrawn)) maxJump = Mathf.Max(maxJump, Mathf.Abs(drawn - prevDrawn));
                maxRoot = Mathf.Max(maxRoot, Mathf.Abs(m.Position.y - prevRoot));
                prevDrawn = drawn;
                prevRoot = m.Position.y;
            });
            Debug.Log($"[M1Test] T11 턱 0.15 오르기: 루트 한 프레임 최대 {maxRoot:F3}m → 비주얼 {maxJump:F3}m, 끝 y {m.Position.y:F3}");
            Assert.That(m.Position.y, Is.EqualTo(0.15f).Within(0.03f), "턱 위로 올라감");
            Assert.That(maxJump, Is.LessThanOrEqualTo(0.04f), "비주얼 한 프레임 높이 변화(턱 보정)");
        }
    }
}
