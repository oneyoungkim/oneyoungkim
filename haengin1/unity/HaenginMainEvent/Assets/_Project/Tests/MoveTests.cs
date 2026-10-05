// 행인1의 메인이벤트 — 이동·카메라 기본 테스트(코드로 만든 시험장, docs/07_M1_조작_설계.md 9-2 의 T01~T13 + T19 + T22)
// 번호·합격 기준은 07 문서 9-2 표 그대로(2차에서 문서 번호로 맞추고, 1차 구현이 말없이 늘린 허용치를 되돌림).
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
            Assert.That(r.Motor.Position.y, Is.EqualTo(0f).Within(0.01f), "루트(발) y = 바닥 y (07: ≤ 0.01)");
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
                if (t95 < 0f && m.PlanarSpeed >= 0.95f * m.T.walkSpeed) t95 = t;
                if (i == n - 61) mark = m.Position;
            }
            float avg = Lab.Flat(m.Position - mark).magnitude / (60 * Dt);
            Debug.Log($"[M1Test] T02 걷기 평균 {avg:F3} m/s, 95% 도달 {t95:F3}s");
            Assert.That(avg, Is.EqualTo(m.T.walkSpeed).Within(0.02f), $"걷기 속도 {m.T.walkSpeed:F2} ± 0.02");
            Assert.That(t95, Is.InRange(0f, 0.15f), "0 → 걷기 95% ≤ 0.15초");
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
            Assert.That(avg, Is.EqualTo(4.5f).Within(0.05f), "달리기 속도 4.50 ± 0.05");
            Assert.That(t95, Is.InRange(0f, 0.60f), "0 → 달리기 95% ≤ 0.60초");
            Assert.That(tStop, Is.InRange(0f, 0.30f), "정지 ≤ 0.30초");
            Assert.That(stopDist, Is.LessThanOrEqualTo(0.65f), "정지 거리 ≤ 0.65m");
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

            // 30° 내리막 달리기: 꼭대기 단(높이 4m)에서 경사판으로 뛰어 내려간다. 경사판 위 프레임만 센다.
            const float DownX = 22f, Ang = 30f, Len = 8f;
            float top = Len * Mathf.Sin(Ang * Mathf.Deg2Rad), run = Len * Mathf.Cos(Ang * Mathf.Deg2Rad);   // 4.0m, 6.93m
            Lab.Ramp(DownX, 0f, Ang, Len);
            Lab.Box("TopDeck", new Vector3(DownX, top - 0.5f, run + 4f), new Vector3(3f, 1f, 8f), Quaternion.identity, Layers.Ground);
            Physics.SyncTransforms();
            m.Teleport(new Vector3(DownX, top, run + 5f), 180f);
            yield return Lab.Seconds(0.3f);
            int onRamp = 0, grounded = 0, lifted = 0;
            float maxGap = 0f;
            yield return Lab.Hold(m, Vector3.back, 4f, true, () =>
            {
                var p = m.Position;
                if (p.z < 0.3f || p.z > run - 0.3f) return;   // 경사판 위(양 끝 0.3m 빼고)
                onRamp++;
                if (m.Grounded) grounded++;
                float gap = SphereGap(m);
                maxGap = Mathf.Max(maxGap, gap);
                if (gap > 0.05f) lifted++;
            });
            float groundedPct = onRamp > 0 ? 100f * grounded / onRamp : 0f;
            Debug.Log($"[M1Test] T05 경사 최고 높이 30° {result[0]:F2} · 35° {result[1]:F2} · 45° {result[2]:F2} · 50° {result[3]:F2} (꼭대기 30° {8f * Mathf.Sin(30f * Mathf.Deg2Rad):F2} · 35° {8f * Mathf.Sin(35f * Mathf.Deg2Rad):F2}) · " +
                      $"30° 내리막 달리기: 경사판 위 {onRamp}프레임 · 접지 {groundedPct:F1}% · 땅과 0.05m 넘게 뜬 프레임 {lifted} · 최대 틈 {maxGap:F3}m");
            Assert.That(result[0], Is.GreaterThan(8f * Mathf.Sin(30f * Mathf.Deg2Rad) - 0.1f), "30° 꼭대기 도착");
            Assert.That(result[1], Is.GreaterThan(8f * Mathf.Sin(35f * Mathf.Deg2Rad) - 0.1f), "35° 꼭대기 도착");
            Assert.That(result[2], Is.LessThan(0.5f), "45° 높이 0.5m 못 넘음");
            Assert.That(result[3], Is.LessThan(0.5f), "50° 높이 0.5m 못 넘음");
            Assert.That(onRamp, Is.GreaterThan(60), "내리막을 실제로 달림");
            Assert.That(groundedPct, Is.GreaterThanOrEqualTo(98f), "30° 내리막 달리기 접지 98% 이상");
            Assert.AreEqual(0, lifted, "30° 내리막 달리기: 땅과 0.05m 넘게 뜬 프레임 없음");
        }

        /// 캡슐 아래 구와 바로 아래 걷는 면 사이 틈(m). 붙어 있으면 skinWidth(0.025) 정도.
        static float SphereGap(PlayerMotor m)
        {
            var cc = m.Controller;
            var c = m.Position + Vector3.up * (cc.center.y - cc.height * 0.5f + cc.radius);
            const float lift = 0.2f;
            if (Physics.SphereCast(c + Vector3.up * lift, cc.radius, Vector3.down, out var hit, 2f, 1 << Layers.Ground, QueryTriggerInteraction.Ignore))
                return hit.distance - lift;
            return 9f;
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
        public IEnumerator T07_Wall_Slide()
        {
            Lab.Floor();
            const float Face = 1.5f;   // 벽 안쪽 면 x
            Lab.Box("Wall", new Vector3(Face + 0.25f, 3f, 0f), new Vector3(0.5f, 6f, 60f), Quaternion.identity, Layers.Wall);
            var r = Lab.Rig(new Vector3(0.6f, 0f, -20f), 45f);
            var m = r.Motor;
            yield return Lab.Seconds(0.3f);
            var dir = new Vector3(1f, 0f, 1f).normalized;   // 벽으로 45° 비스듬히
            int frames = 0, through = 0;
            float maxX = -9f, z0 = 0f, t = 0f, touchAt = -1f;
            yield return Lab.Hold(m, dir, 5f, false, () =>
            {
                t += Dt;
                frames++;
                float x = m.Position.x;
                maxX = Mathf.Max(maxX, x);
                if (x + m.Controller.radius > Face + 0.01f) through++;
                if (touchAt < 0f && x + m.Controller.radius > Face - 0.05f) touchAt = t;
                if (Mathf.Abs(t - 3f) < Dt / 2f) z0 = m.Position.z;
            });
            float along = (m.Position.z - z0) / 2f;   // 마지막 2초 벽 따라 평균 속도
            Debug.Log($"[M1Test] T07 벽 따라 걷기: 벽에 닿음 {touchAt:F2}s · 마지막 2초 벽 따라 {along:F2} m/s(걷기의 {along / m.T.walkSpeed * 100f:F0}%) · 벽 관통 {through}프레임 · 최대 x+반지름 {maxX + m.Controller.radius:F3}(벽 면 {Face})");
            Assert.That(touchAt, Is.InRange(0f, 2.9f), "3초 안에 벽에 닿음");
            Assert.That(along, Is.GreaterThanOrEqualTo(0.6f * m.T.walkSpeed), "벽 따라 속도 ≥ 걷기의 0.6배");
            Assert.AreEqual(0, through, "벽 관통 0");
        }

        [UnityTest]
        public IEnumerator T08_Ledge_Fall_Respawn()
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
            Debug.Log($"[M1Test] T08 낙하 상태 거침 {fell} · 리스폰 {m.Respawns}회 · 위치 {m.Position} · 접지 {m.Grounded} · 상태 {m.State}");
            Assert.IsTrue(fell, "Fall 상태를 거침");
            Assert.That(m.Respawns, Is.GreaterThanOrEqualTo(1), "맵 밖 → 리스폰");
            Assert.IsTrue(m.Grounded, "리스폰 뒤 접지");
            Assert.That(m.State, Is.EqualTo(MoveState.Idle).Or.EqualTo(MoveState.Walk), "착지 후 Idle/Walk 복귀");
            Assert.That(m.Position.y, Is.EqualTo(0f).Within(0.05f), "플랫폼 위");
            Assert.That(Mathf.Abs(m.Position.z), Is.LessThan(5f), "플랫폼 안");
        }

        [UnityTest]
        public IEnumerator T09_Alley_NoWallClip()
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
            // 1) 골목을 걸으며 카메라를 초당 90° 로 돌리기(8초 = 두 바퀴). 사람이 돌리는 것과 같게 수동 조작으로 알린다(자동 정렬이 끼어들지 않게)
            for (int i = 0; i < Mathf.RoundToInt(8f / Dt); i++)
            {
                m.SetMoveInput(Vector3.forward, 1f, false);
                r.CamRig.NoteManualLook();
                r.Orbit.HorizontalAxis.Value = Mathf.DeltaAngle(0f, r.Orbit.HorizontalAxis.Value + 90f * Dt);
                yield return null;
                Check();
            }
            // 2) 등을 벽에 붙이고 서서 한 바퀴
            m.Teleport(new Vector3(1.2f - 0.27f, 0f, 0f), -90f);
            r.CamRig.SnapBehind();
            yield return Lab.Seconds(0.5f);
            for (int i = 0; i < Mathf.RoundToInt(4f / Dt); i++)
            {
                r.CamRig.NoteManualLook();
                r.Orbit.HorizontalAxis.Value = Mathf.DeltaAngle(0f, r.Orbit.HorizontalAxis.Value + 90f * Dt);
                yield return null;
                Check();
            }
            // 3) 자동 정렬에 맡기고 골목을 끝까지 걷기(카메라가 등 뒤로 돌아오며 벽을 스침)
            m.Teleport(new Vector3(0f, 0f, -15f), 0f);
            r.Orbit.HorizontalAxis.Value = 70f;
            yield return Lab.Seconds(0.3f);
            for (int i = 0; i < Mathf.RoundToInt(10f / Dt); i++)
            {
                m.SetMoveInput(Vector3.forward, 1f, false);
                yield return null;
                Check();
            }
            float endGap = Mathf.Abs(Mathf.DeltaAngle(r.CamRig.Yaw, m.Yaw));
            Debug.Log($"[M1Test] T09 골목 카메라 검사 {frames}프레임 중 벽 안·가림 {bad} · 자동 정렬 뒤 카메라-인물 차 {endGap:F1}° {first}");
            Assert.AreEqual(0, bad, "카메라가 벽 안에 들어가거나 벽에 가린 프레임 " + first);
            Assert.That(endGap, Is.LessThan(2f), "골목에서도 자동 정렬로 등 뒤에 옴");
        }

        [UnityTest]
        public IEnumerator T10_ThinPole_NoPop()
        {
            Lab.Floor();
            // 전봇대(PlayerOnly, 지름 0.3m)가 카메라–인물 선을 가로지르게: 카메라를 왼쪽 뒤 30° 에 두고(자동 정렬 끔) 전봇대 오른쪽으로 걷는다
            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Pole";
            pole.layer = Layers.PlayerOnly;
            pole.transform.position = new Vector3(-0.8f, 4f, 0f);
            pole.transform.localScale = new Vector3(0.3f, 4f, 0.3f);
            var r = Lab.Rig(new Vector3(0f, 0f, -8f), 0f);
            var m = r.Motor;
            r.CamRig.SetAutoMode(CamTuning.Auto.Off, false);
            r.Orbit.HorizontalAxis.Value = 30f;
            yield return Lab.Seconds(0.5f);
            float minD = 99f, maxD = 0f, crossAt = -99f;
            int i0 = 0;
            yield return Lab.Hold(m, Vector3.forward, 7f, false, () =>
            {
                i0++;
                if (i0 < 60) return;   // 걷기 속도·감쇠가 자리 잡은 뒤
                float d = r.CamRig.Distance;
                minD = Mathf.Min(minD, d); maxD = Mathf.Max(maxD, d);
                var cp = r.CamRig.CameraPosition;
                if (Physics.Linecast(cp, r.CamTarget.position, 1 << Layers.PlayerOnly, QueryTriggerInteraction.Ignore) && crossAt < -90f) crossAt = m.Position.z;
            });
            Debug.Log($"[M1Test] T10 전봇대가 카메라–인물 사이를 지날 때(인물 z {crossAt:F1}) 카메라 거리 {minD:F3}~{maxD:F3}m (변화 {maxD - minD:F3}m)");
            Assert.That(crossAt, Is.GreaterThan(-90f), "전봇대가 실제로 카메라–인물 사이를 지나감");
            Assert.That(maxD - minD, Is.LessThanOrEqualTo(0.05f), "카메라–CamTarget 거리 변화 ≤ 0.05m");
        }

        [UnityTest]
        public IEnumerator T11_Curb_CameraSmooth()
        {
            Lab.Floor();
            Lab.Box("Curb", new Vector3(0f, 0.075f, 5f), new Vector3(4f, 0.15f, 6f), Quaternion.identity, Layers.Ground);
            var r = Lab.Rig(new Vector3(0f, 0f, 0f), 0f);
            var m = r.Motor;
            yield return Lab.Seconds(0.5f);
            // 코루틴은 Update 뒤·LateUpdate 앞에 돈다 → 지난 프레임에 그려진 비주얼 높이 = 지난 프레임 루트 y + 지금 Visual 로컬 y(지난 LateUpdate 값)
            float prevRoot = m.Position.y, prevDrawn = float.NaN, prevCam = float.NaN, maxJump = 0f, maxRoot = 0f, maxCam = 0f, peak = 0f;
            yield return Lab.Hold(m, Vector3.forward, 7f, false, () =>
            {
                float drawn = prevRoot + r.Visual.localPosition.y;
                if (!float.IsNaN(prevDrawn)) maxJump = Mathf.Max(maxJump, Mathf.Abs(drawn - prevDrawn));
                float cy = r.CamRig.CameraPosition.y;
                if (!float.IsNaN(prevCam)) maxCam = Mathf.Max(maxCam, Mathf.Abs(cy - prevCam));
                maxRoot = Mathf.Max(maxRoot, Mathf.Abs(m.Position.y - prevRoot));
                peak = Mathf.Max(peak, m.Position.y);
                prevDrawn = drawn;
                prevCam = cy;
                prevRoot = m.Position.y;
            });
            Debug.Log($"[M1Test] T11 턱 0.15 오르내리기: 루트 한 프레임 최대 {maxRoot:F3}m → 비주얼 {maxJump:F3}m · 카메라 y {maxCam:F3}m · 최고 y {peak:F3} · 끝 y {m.Position.y:F3} z {m.Position.z:F1}");
            Assert.That(peak, Is.EqualTo(0.15f).Within(0.03f), "턱 위로 올라감");
            Assert.That(m.Position.y, Is.EqualTo(0f).Within(0.03f), "턱 끝에서 내려옴");
            Assert.That(maxJump, Is.LessThanOrEqualTo(0.03f), "비주얼 y 튐 ≤ 0.03m");
            Assert.That(maxCam, Is.LessThanOrEqualTo(0.02f), "카메라 y 프레임당 변화 ≤ 0.02m");
        }

        [UnityTest]
        public IEnumerator T12_Framing_And_Fov()
        {
            Lab.Floor();
            var r = Lab.Rig(new Vector3(0f, 0f, -35f), 0f);
            var m = r.Motor;
            yield return Lab.Seconds(1f);
            var vp = r.Main.WorldToViewportPoint(r.CamTarget.position);
            var feet = r.Main.WorldToViewportPoint(m.Position);
            var head = r.Main.WorldToViewportPoint(m.Position + Vector3.up * m.T.height);
            yield return Lab.Hold(m, Vector3.forward, 2f, true);
            float fovRun = r.Cam.Lens.FieldOfView;
            int k = 0;
            while (m.PlanarSpeed >= 0.05f && k++ < 120) yield return null;   // 멈출 때까지
            yield return Lab.Seconds(1f);
            float fovStop = r.Cam.Lens.FieldOfView;
            Debug.Log($"[M1Test] T12 구도 뷰포트 ({vp.x:F3}, {vp.y:F3}) · 발 {feet.y:F3} 정수리 {head.y:F3} → 인물 {(head.y - feet.y) * 100f:F0}% · 거리 {r.CamRig.Distance:F2}m · FOV 달리기 2초 {fovRun:F2} 멈춘 뒤 1초 {fovStop:F2}");
            Assert.That(vp.x, Is.EqualTo(0.44f).Within(0.01f), "인물 가로 위치(중앙에서 6% 왼쪽)");
            Assert.That(vp.y, Is.EqualTo(0.50f).Within(0.01f), "인물 세로 위치");
            Assert.That(fovRun, Is.EqualTo(49f).Within(0.3f), "달리기 FOV 49 ± 0.3");
            Assert.That(fovStop, Is.EqualTo(45f).Within(0.3f), "정지 FOV 45 ± 0.3");
        }

        [UnityTest]
        public IEnumerator T13_MoveBasis_NoDrift()
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
            Debug.Log($"[M1Test] T13 6초 달리기(자동 정렬 {r.CamRig.AutoMode}) 방향 변화 {drift:F2}° · 카메라-인물 차 {camGap:F2}°");
            Assert.That(drift, Is.LessThanOrEqualTo(1f), "앞으로 달리는 동안 인물 방향 변화");
            Assert.That(camGap, Is.LessThanOrEqualTo(2f), "카메라 궤도와 인물 방향 차");
        }

        [UnityTest]
        public IEnumerator T19_Walk_AutoAlign()
        {
            Lab.Floor(0f, 200f);
            var r = Lab.Rig(new Vector3(0f, 0f, -80f), 0f);
            var m = r.Motor;
            var T = r.CamRig.T;
            yield return Lab.Seconds(0.5f);
            Assert.AreEqual(CamTuning.Auto.Always, r.CamRig.AutoMode, "기본 = 걷기·달리기 자동 정렬");

            // (1) 걷기: 카메라를 오른쪽으로 90° 돌린(수동 조작) 직후 앞으로 걷는다
            var w = new Track();
            yield return Turn(r, 90f, false, 10f, w);
            // (2) 달리기
            m.Teleport(new Vector3(6f, 0f, -80f), 0f);
            r.CamRig.SnapBehind();
            yield return Lab.Seconds(0.3f);
            var rn = new Track();
            yield return Turn(r, -90f, true, 8f, rn);
            // (3) 끔: 그대로
            m.Teleport(new Vector3(12f, 0f, -80f), 0f);
            r.CamRig.SnapBehind();
            r.CamRig.SetAutoMode(CamTuning.Auto.Off, false);
            yield return Lab.Seconds(0.3f);
            var off = new Track();
            yield return Turn(r, 90f, false, 5f, off);
            r.CamRig.SetAutoMode(CamTuning.Auto.Always, false);
            // (4) 서 있을 때는 안 돌림
            r.CamRig.NoteManualLook();
            r.Orbit.HorizontalAxis.Value = 60f;
            yield return Lab.Seconds(4f);
            float idleYaw = r.CamRig.Yaw;
            // (5) 카메라 쪽으로 걸어오면(차 180°) 안 돌림
            m.Teleport(new Vector3(18f, 0f, -80f), 0f);
            r.CamRig.SnapBehind();
            yield return Lab.Seconds(0.3f);
            var back = new Track();
            yield return Turn(r, 180f, false, 4f, back);

            Debug.Log($"[M1Test] T19 자동 정렬 — 걷기: 기다림 {w.Start:F2}s(기준 {T.walkRecenterWait}) · 최대 {w.MaxRate:F1}°/s(기준 {T.walkAlignSpeed}) · 최대 각가속도 {w.MaxAcc:F0}°/s² · 넘침 {w.Over:F2}° · 끝 차 {w.End:F2}° · 1° 안 도착 {w.Done:F2}s | " +
                      $"달리기: 기다림 {rn.Start:F2}s(기준 {T.recenterWait}) · 최대 {rn.MaxRate:F1}°/s(기준 {T.runAlignSpeed}) · 끝 차 {rn.End:F2}° · 1° 안 {rn.Done:F2}s | " +
                      $"끔: 끝 차 {off.End:F1}° · 서 있을 때 yaw 60 → {idleYaw:F1} · 카메라 쪽으로 걷기: 끝 차 {back.End:F1}°");
            Assert.That(w.Start, Is.GreaterThanOrEqualTo(T.walkRecenterWait - 0.02f), "걷기: 수동 조작 직후엔 기다림");
            Assert.That(w.Start, Is.LessThanOrEqualTo(T.walkRecenterWait + 0.2f), "걷기: 기다린 뒤 바로 시작");
            Assert.That(w.MaxRate, Is.LessThanOrEqualTo(T.walkAlignSpeed + 0.5f), "걷기: 각속도 제한");
            Assert.That(w.MaxAcc, Is.LessThanOrEqualTo(T.alignAccel * 1.05f + 1f), "걷기: 각가속도 제한(출렁임 없음)");
            Assert.That(w.Over, Is.LessThanOrEqualTo(0.5f), "걷기: 등 뒤를 지나치지 않음");
            Assert.That(w.End, Is.LessThanOrEqualTo(1f), "걷기: 등 뒤로 돌아옴");
            Assert.That(rn.Start, Is.InRange(T.recenterWait - 0.02f, T.recenterWait + 0.2f), "달리기: 기다림");
            Assert.That(rn.MaxRate, Is.LessThanOrEqualTo(T.runAlignSpeed + 0.5f), "달리기: 각속도 제한");
            Assert.That(rn.End, Is.LessThanOrEqualTo(1f), "달리기: 등 뒤로 돌아옴");
            Assert.That(off.End, Is.EqualTo(90f).Within(0.5f), "끔: 카메라를 그대로 둠");
            Assert.That(idleYaw, Is.EqualTo(60f).Within(0.5f), "서 있을 때는 돌리지 않음");
            Assert.That(back.End, Is.EqualTo(180f).Within(0.5f), "카메라 쪽으로 걸어올 때는 돌리지 않음");
        }

        // ───────────────────────── T22 대각선 + 자동 정렬 (3차 검수: W+D 를 누르고 있으면 3초 뒤 45° → 119° 로 계속 돎)
        // PInput 과 같은 경로(CamRig.StickToWorld)로 스틱 입력을 넣고, 이동 방향(실제 속도의 수평 방향)이 처음 방향에서 얼마나 도는지 잰다.
        [UnityTest]
        public IEnumerator T22_Diagonal_AutoAlign_NoSpiral()
        {
            Lab.Floor(0f, 400f);
            var r = Lab.Rig(new Vector3(0f, 0f, -150f), 0f);
            var m = r.Motor;
            yield return Lab.Seconds(0.5f);
            var diag = new Vector2(0.7071f, 0.7071f);   // W + D

            // (1) 걷기 대각선 3초: 고친 길(이동 기준 고정)
            var walk = new Heading();
            float cam0 = r.CamRig.Yaw;
            yield return Stick(r, diag, false, 3f, walk, true);
            float camTurn = Mathf.Abs(Mathf.DeltaAngle(cam0, r.CamRig.Yaw));
            // (2) 같은 입력, 예전 길(이동 기준 = 지금 궤도 yaw) — 테스트가 문제를 잡는지 보여 주는 비교(기록만)
            m.Teleport(new Vector3(20f, 0f, -150f), 0f);
            r.CamRig.SnapBehind();
            yield return Lab.Seconds(0.3f);
            var old = new Heading();
            yield return Stick(r, diag, false, 3f, old, false);
            // (3) 달리기 대각선 3초
            m.Teleport(new Vector3(-20f, 0f, -150f), 0f);
            r.CamRig.SnapBehind();
            yield return Lab.Seconds(0.3f);
            var run = new Heading();
            float camR0 = r.CamRig.Yaw;
            yield return Stick(r, diag, true, 3f, run, true);
            float camRunTurn = Mathf.Abs(Mathf.DeltaAngle(camR0, r.CamRig.Yaw));
            // (4) W+D 2초 뒤 D 만 떼기(W): 카메라가 따라온 만큼 '화면 앞 = 지금 걷던 방향' → 휙 꺾이지 않고 거의 그대로
            m.Teleport(new Vector3(40f, 0f, -150f), 0f);
            r.CamRig.SnapBehind();
            yield return Lab.Seconds(0.3f);
            var a = new Heading();
            yield return Stick(r, diag, false, 2f, a, true, keepLatch: true);
            float before = a.Last;
            var b = new Heading();
            yield return Stick(r, new Vector2(0f, 1f), false, 1.0f, b, true, keepLatch: true);
            float release = Mathf.Abs(Mathf.DeltaAngle(before, b.Last));
            float releaseMax = Mathf.Max(Mathf.Abs(Mathf.DeltaAngle(before, b.Min)), Mathf.Abs(Mathf.DeltaAngle(before, b.Max)));
            // (5) 오른쪽(D)만 3초: 옆으로 걷는 방향도 그대로
            m.Teleport(new Vector3(60f, 0f, -150f), 0f);
            r.CamRig.SnapBehind();
            yield return Lab.Seconds(0.3f);
            var side = new Heading();
            yield return Stick(r, new Vector2(1f, 0f), false, 3f, side, true);

            Debug.Log($"[M1Test] T22 대각선(W+D)·자동 정렬 — 걷기 3초: 이동 방향 {walk.First:F1}° → 최대 변화 {walk.Drift:F1}° (카메라는 {camTurn:F1}° 따라옴) | " +
                      $"예전 길(기준 = 궤도 yaw): {old.First:F1}° → {old.Last:F1}°, 변화 {old.Drift:F1}° | 달리기 3초: 변화 {run.Drift:F1}° (카메라 {camRunTurn:F1}°) | " +
                      $"W+D 2초 → W: 방향 {before:F1}° → {b.Last:F1}° (변화 {release:F1}°, 도중 최대 {releaseMax:F1}°) | D 만 3초: 변화 {side.Drift:F1}°");
            Assert.That(camTurn, Is.GreaterThanOrEqualTo(20f), "걷기: 자동 정렬이 실제로 카메라를 돌림(시험 조건)");
            Assert.That(walk.Drift, Is.LessThanOrEqualTo(15f), "걷기 대각선 3초: 이동 방향이 처음에서 15° 넘게 돌지 않음");
            Assert.That(run.Drift, Is.LessThanOrEqualTo(15f), "달리기 대각선 3초: 이동 방향이 처음에서 15° 넘게 돌지 않음");
            Assert.That(releaseMax, Is.LessThanOrEqualTo(15f), "W+D → W: 휙 꺾이지 않음");
            Assert.That(side.Drift, Is.LessThanOrEqualTo(15f), "D 만: 이동 방향 그대로");
        }

        sealed class Heading
        {
            public float First = float.NaN, Last, Min = float.MaxValue, Max = float.MinValue, Drift;
            public void Add(float h)
            {
                if (float.IsNaN(First)) First = h;
                float d = Mathf.DeltaAngle(First, h);
                Drift = Mathf.Max(Drift, Mathf.Abs(d));
                Min = Mathf.Min(Min, First + d);
                Max = Mathf.Max(Max, First + d);
                Last = h;
            }
        }

        /// 스틱(화면 기준)을 seconds 동안 누른다. viaBasis = PInput 과 같은 CamRig.StickToWorld, 아니면 예전 계산(궤도 yaw 그대로).
        /// 실제 이동 방향(속도 1.0 m/s 넘을 때)을 기록. keepLatch = 앞 구간에서 이어서 누르는 중(스틱을 놓지 않음)
        static IEnumerator Stick(RigFactory.Rig r, Vector2 stick, bool run, float seconds, Heading h, bool viaBasis, bool keepLatch = false)
        {
            var m = r.Motor;
            int n = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < n; i++)
            {
                var dir = viaBasis ? r.CamRig.StickToWorld(stick) : Quaternion.Euler(0f, r.CamRig.Yaw, 0f) * new Vector3(stick.x, 0f, stick.y);
                m.SetMoveInput(dir, stick.magnitude, run);
                yield return null;
                var v = m.Velocity;
                if (new Vector2(v.x, v.z).magnitude > 1.0f) h.Add(Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg);
            }
            if (!keepLatch) { r.CamRig.StickToWorld(Vector2.zero); m.ClearMoveInput(); }
        }

        sealed class Track { public float Start = -1f, MaxRate, MaxAcc, Over, End, Done = -1f; }

        /// 카메라를 인물 등 뒤에서 offset 만큼 돌려 놓고(수동 조작) 앞(+Z)으로 seconds 동안 걷는/달리는 동안 카메라 yaw 를 기록
        static IEnumerator Turn(RigFactory.Rig r, float offset, bool run, float seconds, Track tr)
        {
            var m = r.Motor;
            r.CamRig.NoteManualLook();
            r.Orbit.HorizontalAxis.Value = Mathf.DeltaAngle(0f, m.Yaw + offset);
            float prev = r.CamRig.Yaw, prevRate = 0f, t = 0f;
            float sign = Mathf.Sign(offset);
            int n = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < n; i++)
            {
                m.SetMoveInput(Vector3.forward, 1f, run);   // 인물 yaw 0(+Z)
                yield return null;
                t += Dt;
                float yaw = r.CamRig.Yaw;
                float rate = Mathf.DeltaAngle(prev, yaw) / Dt;
                if (tr.Start < 0f && Mathf.Abs(rate) > 0.01f) tr.Start = t - Dt;
                tr.MaxRate = Mathf.Max(tr.MaxRate, Mathf.Abs(rate));
                if (i > 0) tr.MaxAcc = Mathf.Max(tr.MaxAcc, Mathf.Abs(rate - prevRate) / Dt);
                float diff = Mathf.DeltaAngle(m.Yaw, yaw);   // 남은 차(부호 = 처음 돌린 쪽)
                if (Mathf.Sign(diff) == -sign) tr.Over = Mathf.Max(tr.Over, Mathf.Abs(diff));
                if (tr.Done < 0f && Mathf.Abs(diff) <= 1f) tr.Done = t;
                prev = yaw;
                prevRate = rate;
            }
            tr.End = Mathf.Abs(Mathf.DeltaAngle(m.Yaw, r.CamRig.Yaw));
            m.ClearMoveInput();
        }
    }
}
