// 행인1의 메인이벤트 — 1구역 자동 걷기·달리기 (docs/07_M1_조작_설계.md 9-2 T14) + 시작 구도(T21) + 게임 화면 촬영(+ 2026-10-06 대기 앞모습·걷기 옆모습·발 미끄러짐 띠)
// Zone1.unity 를 열고, 테스트가 NavMesh 를 임시로 구워(저장 안 함) 체크포인트 1→10 사이 경로 모서리를 따라
// PlayerMotor.SetMoveInput 으로 걷게(또는 달리게) 한다. 합격(07 9-2): 구간마다 1.3 × 경로 ÷ 속도 안 도착 · 땅 아래로 떨어지지 않음(y > 지면 − 1) ·
// 끼어서 멈추지 않음(3초 동안 이동 < 0.2m 면 실패) · 리스폰 0 · 10프레임마다 카메라가 벽·땅 안에 들어가거나 인물이 가린 프레임 0(T09 와 같은 검사).
// 카메라는 실제 게임처럼 자동 정렬(걷기·달리기)이 켜진 채로 따라온다.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Haengin.Tests
{
    public sealed class ZoneTests
    {
        public const string ScenePath = "Assets/_Project/Scenes/Zone1.unity";
        const float Dt = Lab.Dt;
        const float StuckWindow = 3f, StuckDist = 0.2f;

        PlayerMotor motor;
        CamRig camRig;
        RouteData route;
        NavMeshDataInstance nav;
        NavMeshQueryFilter filter;
        float navBakeSec;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.captureDeltaTime = Dt;
            GameState.SetPaused(false);
            // 테스트 실행기가 든 처음 장면은 남겨야 하므로 Single 이 아니라 Additive 로 열고 활성 장면으로 바꾼다
            yield return Lab.UnloadOurs(default);
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
#else
            yield return SceneManager.LoadSceneAsync("Zone1", LoadSceneMode.Additive);
#endif
            var zone = SceneManager.GetSceneByPath(ScenePath);
            Assert.IsTrue(zone.IsValid() && zone.isLoaded, "Zone1 장면 열기");
            SceneManager.SetActiveScene(zone);
            yield return null;
            yield return null;
            motor = UnityEngine.Object.FindAnyObjectByType<PlayerMotor>();
            camRig = UnityEngine.Object.FindAnyObjectByType<CamRig>();
            route = UnityEngine.Object.FindAnyObjectByType<RouteData>();
            if (motor == null || camRig == null || route == null)
                Assert.Fail($"Zone1 장면에 리그가 없습니다(PlayerMotor {motor != null} · CamRig {camRig != null} · RouteData {route != null}). M1Setup.Build 를 먼저 돌리세요");
            // 사람 입력을 끄고 테스트가 직접 넣는다
            foreach (var p in UnityEngine.Object.FindObjectsByType<PInput>(FindObjectsSortMode.None)) p.enabled = false;
            foreach (var c in UnityEngine.Object.FindObjectsByType<CamInput>(FindObjectsSortMode.None)) c.enabled = false;
            var t0 = Time.realtimeSinceStartup;
            nav = BakeNav(route, out filter);
            navBakeSec = Time.realtimeSinceStartup - t0;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (nav.valid) nav.Remove();
            GameState.SetPaused(false);
            Time.captureDeltaTime = 0f;
            yield return null;
        }

        // ───────────────────────── 테스트
        [UnityTest, Timeout(900000)]
        public IEnumerator T14_Zone1_Route_Walk() => Route(false);

        [UnityTest, Timeout(900000)]
        public IEnumerator T14_Zone1_Route_Run() => Route(true);

        IEnumerator Route(bool run)
        {
            string mode = run ? "달리기" : "걷기";
            Assert.That(route.Count, Is.GreaterThanOrEqualTo(2), "체크포인트 수");
            motor.Teleport(route.SpawnPos, route.SpawnYaw);
            camRig.SnapBehind();
            yield return Lab.Seconds(0.5f);

            var segs = new List<Seg>();
            var cam = new CamStats();
            float real0 = Time.realtimeSinceStartup;
            for (int i = 1; i < route.Count; i++)
            {
                var s = new Seg { Name = $"{i}→{i + 1} {route.Names[i]}" };
                segs.Add(s);
                yield return Walk(route.Points[i], Mathf.Min(route.Radii[i], 2.5f), run, s, cam);
                if (!s.Ok) break;
            }
            float realSec = Time.realtimeSinceStartup - real0;

            float total = segs.Sum(s => s.Time), len = segs.Sum(s => s.Len);
            var sb = new StringBuilder();
            sb.AppendLine($"[M1Test] Zone1 {mode} 완주 {segs.Count(s => s.Ok)}/{route.Count - 1} 구간 · 게임 시간 {total:F1}초({Mathf.FloorToInt(total / 60f)}분 {total % 60f:F1}초) · 경로 {len:F1}m · " +
                          $"평균 {(total > 0 ? len / total : 0):F2} m/s · 실제 {realSec:F1}초 · NavMesh 굽기 {navBakeSec:F1}초 · 리스폰 {motor.Respawns}");
            foreach (var s in segs)
                sb.AppendLine($"  {s.Name}: {(s.Ok ? "도착" : "실패 — " + s.Fail)} · {s.Time:F1}초 / 제한 {s.Limit:F1}초 · 경로 {s.Len:F1}m · 다시 찾기 {s.Repaths} · 공중 프레임 {s.Air}");
            sb.Append($"  카메라: 검사 {cam.Frames}프레임 · 벽 안 {cam.Inside} · 인물 가림 {cam.Blocked} · 벽 간격·다리 가림으로 0.05m 넘게 당김 {cam.LegPulled} · 평균 거리 {(cam.Frames > 0 ? cam.SumDist / cam.Frames : 0f):F2}m · 최소 거리 {cam.MinDist:F2}m{(cam.First != null ? " · 첫 사례 " + cam.First : "")}");
            Debug.Log(sb.ToString());

            foreach (var s in segs) Assert.IsTrue(s.Ok, $"{mode} {s.Name}: {s.Fail}");
            Assert.AreEqual(route.Count - 1, segs.Count, "모든 구간");
            Assert.AreEqual(0, motor.Respawns, "맵 밖으로 떨어져 리스폰된 적 없음");
            Assert.That(cam.Frames, Is.GreaterThan(50), "카메라 검사 프레임 수");
            Assert.AreEqual(0, cam.Inside, $"{mode}: 카메라가 벽·땅 안에 들어간 프레임 {cam.First}");
            Assert.AreEqual(0, cam.Blocked, $"{mode}: 카메라→인물 선이 벽·땅에 막힌 프레임 {cam.First}");
        }

        // ───────────────────────── 경로 걷기
        sealed class Seg
        {
            public string Name, Fail;
            public bool Ok;
            public float Len, Time, Limit;
            public int Repaths, Air;
        }

        sealed class CamStats
        {
            public int Frames, Inside, Blocked, LegPulled;
            public float SumDist;
            public float MinDist = 99f;
            public string First;
        }

        bool PathTo(Vector3 goal, out Vector3[] corners, out float len)
        {
            corners = null;
            len = 0f;
            if (!NavMesh.SamplePosition(motor.Position, out var a, 2.5f, filter)) return false;
            if (!NavMesh.SamplePosition(goal, out var b, 3f, filter)) return false;
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(a.position, b.position, filter, path) || path.status != NavMeshPathStatus.PathComplete) return false;
            corners = path.corners;
            for (int k = 1; k < corners.Length; k++) len += Vector3.Distance(corners[k - 1], corners[k]);
            return true;
        }

        IEnumerator Walk(Vector3 goal, float arrive, bool run, Seg s, CamStats cam)
        {
            if (!PathTo(goal, out var pts, out s.Len)) { s.Fail = "NavMesh 경로 없음"; yield break; }
            float speed = run ? motor.T.runSpeed : motor.T.walkSpeed;
            s.Limit = 1.3f * s.Len / speed;   // 07 9-2: 걸린 시간 ≤ 1.3 × 경로 길이 ÷ 속도
            int idx = 1, frame = 0;
            float tm = 0f;
            var hist = new List<(float t, Vector3 p)> { (0f, motor.Position) };
            while (true)
            {
                var p = motor.Position;
                if (Lab.Flat(p - goal).magnitude <= arrive) { s.Ok = true; break; }

                // 다음 모서리: 가까워지거나 지나쳤으면 넘어감
                while (idx < pts.Length - 1)
                {
                    var c = pts[idx];
                    float dist = Lab.Flat(c - p).magnitude;
                    bool passed = dist < 1.5f && Vector3.Dot(Lab.Flat(pts[idx + 1] - c), Lab.Flat(p - c)) > 0f;
                    if (dist < 0.45f || passed) idx++;
                    else break;
                }
                // 경로에서 2m 넘게 벗어나면 지금 자리에서 다시 찾기
                if (idx > 0 && idx < pts.Length && SegDist(p, pts[idx - 1], pts[idx]) > 2f && s.Repaths < 8)
                {
                    if (PathTo(goal, out var np, out _)) { pts = np; idx = 1; s.Repaths++; }
                }
                var aim = idx < pts.Length - 1 ? pts[idx] : goal;
                var d = Lab.Flat(aim - p);
                if (d.sqrMagnitude < 1e-4f) d = Lab.Flat(goal - p);
                motor.SetMoveInput(d.normalized, 1f, run);

                yield return null;
                tm += Dt;
                frame++;
                p = motor.Position;
                if (!motor.Grounded) s.Air++;

                // 땅 아래로 떨어졌나
                float gy = GroundBelow(p);
                if (p.y < gy - 1f) { s.Fail = $"땅 아래로 떨어짐 y {p.y:F2} < 지면 {gy:F2} − 1 @({p.x:F1},{p.z:F1})"; break; }
                // 끼었나: 3초 전 위치에서 0.2m 도 못 갔으면 실패
                if (frame % 15 == 0) hist.Add((tm, p));
                if (tm >= StuckWindow)
                {
                    // 3초 이상 전 표본 중 가장 최근 것(첫 표본 t=0 이 항상 있음)
                    var old = hist.Last(h => h.t <= tm - StuckWindow + 1e-4f);
                    float moved = Lab.Flat(p - old.p).magnitude;
                    if (moved < StuckDist) { s.Fail = $"끼어서 멈춤({tm - old.t:F1}초 동안 {moved:F2}m) @({p.x:F1},{p.y:F1},{p.z:F1})"; break; }
                }
                if (tm > s.Limit) { s.Fail = $"시간 초과 {tm:F1}초 > {s.Limit:F1}초 @({p.x:F1},{p.y:F1},{p.z:F1}) 남은 {Lab.Flat(goal - p).magnitude:F1}m"; break; }

                // 카메라 검사(10프레임마다): 벽·땅 안에 들어갔나, 카메라→인물 선이 막혔나
                if (frame % 10 == 0 && frame > 30) CheckCam(cam);
            }
            s.Time = tm;
            motor.ClearMoveInput();
        }

        void CheckCam(CamStats cam)
        {
            int mask = Layers.Mask(Layers.Wall, Layers.Ground);
            var cp = camRig.CameraPosition;
            var tp = camRig.CamTarget.position;
            cam.Frames++;
            bool inside = Physics.CheckSphere(cp, 0.05f, mask, QueryTriggerInteraction.Ignore);
            bool blocked = Physics.Linecast(cp, tp, mask, QueryTriggerInteraction.Ignore);
            if (inside) cam.Inside++;
            if (blocked) cam.Blocked++;
            if (camRig.Pull > 0.05f) cam.LegPulled++;
            cam.SumDist += Vector3.Distance(cp, tp);
            cam.MinDist = Mathf.Min(cam.MinDist, Vector3.Distance(cp, tp));
            if ((inside || blocked) && cam.First == null) cam.First = $"카메라 ({cp.x:F1},{cp.y:F1},{cp.z:F1}) 안={inside} 가림={blocked}";
        }

        static float SegDist(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = Lab.Flat(b - a), ap = Lab.Flat(p - a);
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(ap, ab) / ab.sqrMagnitude) : 0f;
            return (ap - ab * t).magnitude;
        }

        /// 발밑 걷는 면 높이: 위에서 아래로 Ground 를 모두 맞혀, 발보다 0.6m 위 이하 중 가장 높은 면. 없으면(모든 면이 발 위) 가장 낮은 면.
        static float GroundBelow(Vector3 p)
        {
            var hits = Physics.RaycastAll(p + Vector3.up * 30f, Vector3.down, 80f, 1 << Layers.Ground, QueryTriggerInteraction.Ignore);
            if (hits.Length == 0) return float.NegativeInfinity;
            float best = float.NegativeInfinity, lowest = float.PositiveInfinity;
            foreach (var h in hits)
            {
                float y = h.point.y;
                lowest = Mathf.Min(lowest, y);
                if (y <= p.y + 0.6f && y > best) best = y;
            }
            return best > float.NegativeInfinity ? best : lowest;
        }

        // ───────────────────────── NavMesh (테스트 때만, 저장 안 함)
        /// 시우보다 조금 큰 반지름(0.35)으로 구워 모서리를 벽에서 띄운다. 맨땅(Terrain)은 비용 4 → 길·계단을 먼저 쓴다.
        static NavMeshDataInstance BakeNav(RouteData r, out NavMeshQueryFilter f)
        {
            var st = NavMesh.GetSettingsByID(0);
            st.agentRadius = 0.35f;
            st.agentHeight = 1.74f;
            st.agentSlope = 40f;
            st.agentClimb = 0.3f;
            st.overrideVoxelSize = true;
            st.voxelSize = 0.1f;
            st.minRegionArea = 0.5f;
            var center = (r.BoundsMin + r.BoundsMax) / 2f;
            var bounds = new Bounds(center, r.BoundsMax - r.BoundsMin + new Vector3(2f, 10f, 2f));
            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(bounds, Layers.Mask(Layers.Default, Layers.Ground, Layers.Wall, Layers.PlayerOnly),
                                          NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
            var terrain = GameObject.Find("Zone1/Ground/Terrain");
            for (int i = 0; i < sources.Count; i++)
            {
                var s = sources[i];
                if (terrain != null && s.component != null && s.component.gameObject == terrain) { s.area = 3; sources[i] = s; }
            }
            var data = NavMeshBuilder.BuildNavMeshData(st, sources, bounds, Vector3.zero, Quaternion.identity);
            Assert.NotNull(data, "NavMesh 굽기 실패");
            f = new NavMeshQueryFilter { agentTypeID = st.agentTypeID, areaMask = NavMesh.AllAreas };
            f.SetAreaCost(3, 4f);
            Debug.Log($"[M1Test] NavMesh 임시 굽기: 소스 {sources.Count}개 · 반지름 {st.agentRadius} · 턱 {st.agentClimb} · 경사 {st.agentSlope}° (맨땅 비용 4)");
            return NavMesh.AddNavMeshData(data);
        }

        // ───────────────────────── T21 시작 구도 (07 4-3: 인물 화면 높이 약 50%, 발 아래 여백 10%) + 달리기 구도
        [UnityTest, Timeout(900000)]
        public IEnumerator T21_Start_Framing()
        {
            var cam = Camera.main;
            Assert.NotNull(cam, "Main Camera");
            motor.Teleport(route.SpawnPos, route.SpawnYaw);
            camRig.SnapBehind();
            yield return Lab.Seconds(1.5f);
            Frame(cam, out float feet, out float head, out float vx);
            float dist = camRig.Distance, pull = camRig.Pull;

            // 달리기: 시작 지점에서 다음 체크포인트(2) 쪽으로 3초 — 자동 정렬로 등 뒤에 온 뒤의 구도(07: FOV 49·4.3m → 발 16%·정수리 59%)
            var s = new Seg();
            float t = 0f;
            yield return WalkUntil(route.Points[1], 1.0f, true, s, () => (t += Dt) > 3.0f);
            Frame(cam, out float rFeet, out float rHead, out _);
            float rDist = camRig.Distance, rPull = camRig.Pull, rFov = camRig.Cam.Lens.FieldOfView;
            float rGap = Mathf.Abs(Mathf.DeltaAngle(camRig.Yaw, motor.Yaw));
            var p = motor.Position;
            Debug.Log($"[M1Test] T21 시작 구도: 시우 ({p.x:F1},{p.z:F1}) · 카메라 거리 {dist:F2}m · 당김 {pull:F2}m · 발 {feet * 100f:F0}% · 정수리 {head * 100f:F0}% → 인물 {(head - feet) * 100f:F0}% · 가로 {vx:F2} | " +
                      $"달리기 3초: 거리 {rDist:F2}m · 당김 {rPull:F2}m · FOV {rFov:F1} · 발 {rFeet * 100f:F0}% · 정수리 {rHead * 100f:F0}% → 인물 {(rHead - rFeet) * 100f:F0}% · 카메라-인물 방향 차 {rGap:F1}°");
            Assert.That(pull, Is.LessThanOrEqualTo(0.05f), "시작: 벽·펜스 때문에 카메라를 당기지 않음");
            Assert.That(dist, Is.EqualTo(4.0f).Within(0.1f), "시작: 카메라 거리 4.0m");
            Assert.That(head - feet, Is.EqualTo(0.50f).Within(0.04f), "시작: 인물 화면 높이 약 50%");
            Assert.That(feet, Is.EqualTo(0.10f).Within(0.03f), "시작: 발 아래 여백 약 10%");
            Assert.That(rPull, Is.LessThanOrEqualTo(0.05f), "달리기: 카메라를 당기지 않음");
            Assert.That(rGap, Is.LessThanOrEqualTo(15f), "달리기: 자동 정렬로 등 뒤");
            Assert.That(rHead - rFeet, Is.InRange(0.38f, 0.48f), "달리기: 인물 화면 높이 약 43%");
        }

        /// 지금 화면에서 시우 발·정수리 뷰포트 높이, 가로 위치(카메라 최종 자세 기준)
        void Frame(Camera cam, out float feet, out float head, out float x)
        {
            var p = motor.Position;
            var f = cam.WorldToViewportPoint(p);
            var h = cam.WorldToViewportPoint(p + Vector3.up * motor.T.height);
            feet = f.y; head = h.y; x = (f.x + h.x) / 2f;
        }

        // ───────────────────────── 게임 화면 촬영 (-m1shots <폴더> 를 줄 때만, -nographics 없이). 화면 UI(길잡이 HUD·조작 안내·일시정지)까지 찍는다
        [UnityTest, Timeout(900000)]
        public IEnumerator Z_GameShots()
        {
            string dir = Arg("-m1shots");
            if (string.IsNullOrEmpty(dir)) { Assert.Ignore("-m1shots <폴더> 를 줄 때만 찍는다"); yield break; }
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) { Assert.Ignore("그래픽 장치 없음(-nographics)"); yield break; }
#if UNITY_EDITOR
            UnityEditor.ShaderUtil.allowAsyncCompilation = false;
#endif
            Directory.CreateDirectory(dir);
            var camera = Camera.main;
            Assert.NotNull(camera, "Main Camera");
            var notes = new List<string>();
            var guide = UnityEngine.Object.FindAnyObjectByType<RouteGuide>();
            var hud = UnityEngine.Object.FindAnyObjectByType<RouteHud>();
            void Target(int i) { if (guide != null) guide.SetIndex(i); if (hud != null) hud.ResetFlash(); }

            // (1) 시작 지점: 서 있는 그대로 1.5초
            motor.Teleport(route.SpawnPos, route.SpawnYaw);
            camRig.SnapBehind();
            yield return Lab.Seconds(1.5f);
            notes.Add(Shot(camera, Path.Combine(dir, "m1_game_1_start.png"), "시작 지점"));

            // (2) 명륜3가 골목: 골목 입구(4) → 시우네 집 앞(5)을 걷다가 절반쯤(자동 정렬로 카메라가 등 뒤)
            Target(4);
            motor.Teleport(Ground(route.Points[3]), Yaw(route.Points[4] - route.Points[3]));
            camRig.SnapBehind();
            yield return Lab.Seconds(0.3f);
            float half = Lab.Flat(route.Points[4] - route.Points[3]).magnitude * 0.5f;
            var s2 = new Seg();
            yield return WalkUntil(route.Points[4], 1.5f, false, s2, () => Lab.Flat(motor.Position - route.Points[3]).magnitude >= half);
            notes.Add(Shot(camera, Path.Combine(dir, "m1_game_2_alley.png"), "명륜3가 골목"));

            // (3) 꼭대기 계단참(6): 시우네 집 앞(5)에서 계단을 올라 도착한 뒤 1.5초 — 성곽이 드러나는 자리
            Target(5);
            motor.Teleport(Ground(route.Points[4]), Yaw(route.Points[5] - route.Points[4]));
            camRig.SnapBehind();
            yield return Lab.Seconds(0.3f);
            var s3 = new Seg();
            yield return WalkUntil(route.Points[5], 1.0f, false, s3, null);
            yield return Lab.Seconds(1.5f);
            notes.Add(Shot(camera, Path.Combine(dir, "m1_game_3_reveal.png"), "꼭대기 계단참 성곽"));

            // (4) 와룡공원 도착: 계단참(6) → 공터(7)까지 걸어 들어온 뒤 2초(자동 정렬이 등 뒤로) — 도착 HUD 가 뜬 화면
            Target(6);
            motor.Teleport(Ground(route.Points[5]), Yaw(route.Points[6] - route.Points[5]));
            camRig.SnapBehind();
            yield return Lab.Seconds(0.2f);
            var s4 = new Seg();
            yield return WalkUntil(route.Points[6], Mathf.Min(route.Radii[6], 2.5f), false, s4, null);
            yield return Lab.Seconds(0.6f);
            notes.Add(Shot(camera, Path.Combine(dir, "m1_game_4_waryong.png"), "와룡공원 공터 도착"));

            // (5) 상가거리 달리기 3초(FOV 49·거리 4.3, 자동 정렬)
            Target(1);
            motor.Teleport(route.SpawnPos, route.SpawnYaw);
            camRig.SnapBehind();
            yield return Lab.Seconds(0.3f);
            var s5 = new Seg();
            float t5 = 0f;
            yield return WalkUntil(route.Points[1], 1.0f, true, s5, () => (t5 += Dt) > 3.0f);
            notes.Add(Shot(camera, Path.Combine(dir, "m1_game_5_run.png"), "상가거리 달리기"));

            // (7) 대기 앞 3/4(리깅 모델 얼굴·옷·툰 외곽선 확인): 시작 지점에서 카메라를 앞쪽 145°로
            Target(1);
            motor.Teleport(route.SpawnPos, route.SpawnYaw);
            camRig.SnapBehind();
            camRig.SetAutoMode(CamTuning.Auto.Off, false);
            camRig.Orbit.HorizontalAxis.Value = Mathf.DeltaAngle(0f, route.SpawnYaw + 145f);
            camRig.Cam.PreviousStateIsValid = false;
            yield return Lab.Seconds(1.5f);
            notes.Add(Shot(camera, Path.Combine(dir, "m1_game_7_idle_front.png"), "대기 앞 3/4"));

            // (8) 걷기 옆모습(게임 카메라): 상가거리 쪽으로 곧게 걸으며 카메라를 시우 오른쪽 옆(몸 방향 − 90°)에
            var toCp2 = Lab.Flat(route.Points[1] - route.SpawnPos).normalized;
            float yaw2 = Yaw(toCp2);
            motor.Teleport(route.SpawnPos, yaw2);
            camRig.SnapBehind();
            camRig.Orbit.HorizontalAxis.Value = Mathf.DeltaAngle(0f, yaw2 - 90f);
            camRig.Cam.PreviousStateIsValid = false;
            for (int i = 0; i < Mathf.RoundToInt(1.6f / Dt); i++) { motor.SetMoveInput(toCp2, 1f, false); yield return null; }
            motor.SetMoveInput(toCp2, 1f, false);
            notes.Add(Shot(camera, Path.Combine(dir, "m1_game_8_walk_side.png"), "걷기 옆모습"));
            motor.ClearMoveInput();
            camRig.SetAutoMode(CamTuning.Auto.Always, false);

            // (9)(10) 발 미끄러짐 확인 띠: 체크무늬 바닥(구역 밖 촬영장)에서 고정 카메라로 옆에서 8컷 — 디딤발이 바닥 칸 위 같은 자리에 머무는지
            yield return Strip(Path.Combine(dir, "m1_game_9_walk_strip.png"), false, 4, notes);
            yield return Strip(Path.Combine(dir, "m1_game_10_run_strip.png"), true, 2, notes);
            motor.Teleport(route.SpawnPos, route.SpawnYaw);
            camRig.SnapBehind();
            yield return Lab.Seconds(0.5f);

            // (6) 일시정지 메뉴(패드로 '카메라 자동 정렬' 항목을 고른 상태)
            var ui = UnityEngine.Object.FindAnyObjectByType<GameUi>();
            if (ui != null)
            {
                GameState.SetPaused(true);
                yield return null;
                yield return null;
                notes.Add(Shot(camera, Path.Combine(dir, "m1_game_6_pause.png"), "일시정지 메뉴"));
                GameState.SetPaused(false);
                yield return null;
            }
            else notes.Add("일시정지: 장면에 GameUi 가 없음");

            Debug.Log("[M1Test] 게임 화면 촬영\n  " + string.Join("\n  ", notes));
        }

        /// 경로를 걷다가 until 이 참이 되면(또는 도착하면) 멈춘다
        IEnumerator WalkUntil(Vector3 goal, float arrive, bool run, Seg s, Func<bool> until)
        {
            if (!PathTo(goal, out var pts, out s.Len)) { Assert.Fail("촬영 경로 없음"); yield break; }
            int idx = 1;
            for (int f = 0; f < 60 * 120; f++)
            {
                var p = motor.Position;
                if (Lab.Flat(p - goal).magnitude <= arrive || (until != null && until())) break;
                while (idx < pts.Length - 1)
                {
                    var c = pts[idx];
                    float dist = Lab.Flat(c - p).magnitude;
                    bool passed = dist < 1.5f && Vector3.Dot(Lab.Flat(pts[idx + 1] - c), Lab.Flat(p - c)) > 0f;
                    if (dist < 0.45f || passed) idx++;
                    else break;
                }
                var aim = idx < pts.Length - 1 ? pts[idx] : goal;
                motor.SetMoveInput(Lab.Flat(aim - p).normalized, 1f, run);
                yield return null;
            }
            motor.ClearMoveInput();
        }

        /// 발 미끄러짐 확인 띠: 구역 밖(x 1000, 높이 100) 체크무늬 바닥(0.25m 칸)에서 +Z 로 걷거나 달리게 하고, 옆 고정 카메라로 every 프레임마다 8컷을 2×4 로 붙인다.
        /// 고정 카메라라서 디딤발은 컷이 바뀌어도 같은 칸 위에 있어야 한다(미끄러지면 칸을 따라 밀림). 세로 보조선 50px 마다.
        IEnumerator Strip(string path, bool run, int every, List<string> notes)
        {
            const int cols = 4, rows = 2, tw = 720, th = 540;
            var origin = new Vector3(1000f, 100f, 0f);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "SlipStudio";
            floor.layer = Layers.Ground;
            floor.transform.SetPositionAndRotation(origin + new Vector3(0f, -0.5f, 20f), Quaternion.identity);
            floor.transform.localScale = new Vector3(12f, 1f, 80f);
            var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++)
                tex.SetPixel(x, y, ((x / 4) + (y / 4)) % 2 == 0 ? new Color(0.93f, 0.90f, 0.84f) : new Color(0.62f, 0.58f, 0.52f));
            tex.Apply();
            var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture");
            var mat = new Material(sh);
            mat.mainTexture = tex;
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            mat.mainTextureScale = new Vector2(12f / 0.5f, 80f / 0.5f);   // 8px 텍스처 = 2×2 칸 = 0.5m → 칸 0.25m
            if (mat.HasProperty("_BaseMap")) mat.SetTextureScale("_BaseMap", mat.mainTextureScale);
            floor.GetComponent<Renderer>().sharedMaterial = mat;
            Physics.SyncTransforms();

            float speed = run ? motor.T.runSpeed : motor.T.walkSpeed;
            float warm = run ? 1.2f : 0.8f;
            int frames = cols * rows;
            float span = frames * every * Dt;
            motor.Teleport(origin + new Vector3(0f, 0f, -2f), 0f);
            camRig.SnapBehind();
            yield return Lab.Seconds(0.3f);
            var mid = origin + new Vector3(0f, 0f, -2f + speed * (warm + span * 0.5f));
            var go = new GameObject("StripCam");
            var c = go.AddComponent<Camera>();
            c.enabled = false;
            c.fieldOfView = run ? 32f : 30f;
            c.nearClipPlane = 0.1f;
            c.farClipPlane = 200f;
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = new Color(0.93f, 0.89f, 0.82f);
            go.transform.SetPositionAndRotation(new Vector3(origin.x + (run ? 6.0f : 4.5f), origin.y + 0.95f, mid.z), Quaternion.Euler(0f, -90f, 0f));

            var atlas = new Texture2D(tw * cols, th * rows, TextureFormat.RGBA32, false);
            var rt = new RenderTexture(new RenderTextureDescriptor(tw * 2, th * 2, RenderTextureFormat.ARGB32, 24) { sRGB = true, msaaSamples = 1 });
            var small = new RenderTexture(new RenderTextureDescriptor(tw, th, RenderTextureFormat.ARGB32, 0) { sRGB = true });
            rt.Create(); small.Create();
            var tile = new Texture2D(tw, th, TextureFormat.RGBA32, false);
            var feet = new List<string>();
            var loco = motor.GetComponentInChildren<LocoAnim>();
            var an = loco != null ? loco.Animator : null;
            int shot = 0, f = 0;
            int warmFrames = Mathf.RoundToInt(warm / Dt);
            while (shot < frames && f < warmFrames + frames * every + 10)
            {
                motor.SetMoveInput(Vector3.forward, 1f, run);
                yield return null;
                f++;
                if (f < warmFrames || (f - warmFrames) % every != 0) continue;
                var prevA = RenderTexture.active;
                c.targetTexture = rt;
                c.Render();
                Graphics.Blit(rt, small);
                RenderTexture.active = small;
                tile.ReadPixels(new Rect(0, 0, tw, th), 0, 0);
                tile.Apply();
                RenderTexture.active = prevA;
                c.targetTexture = null;
                var px = tile.GetPixels();
                for (int x = 0; x < tw; x += 50)
                    for (int y = 0; y < th; y++) px[y * tw + x] = Color.Lerp(px[y * tw + x], new Color(0.10f, 0.08f, 0.09f), 0.35f);
                int cx = shot % cols, cy = rows - 1 - shot / cols;
                atlas.SetPixels(cx * tw, cy * th, tw, th, px);
                if (an != null)
                {
                    var l = an.GetBoneTransform(HumanBodyBones.LeftFoot).position;
                    var r = an.GetBoneTransform(HumanBodyBones.RightFoot).position;
                    feet.Add($"{shot + 1}: 왼발 z {l.z:F2} y {l.y - origin.y:F2} · 오른발 z {r.z:F2} y {r.y - origin.y:F2}");
                }
                shot++;
            }
            motor.ClearMoveInput();
            atlas.Apply();
            File.WriteAllBytes(path, atlas.EncodeToPNG());
            rt.Release(); small.Release();
            UnityEngine.Object.Destroy(rt); UnityEngine.Object.Destroy(small);
            UnityEngine.Object.Destroy(tile); UnityEngine.Object.Destroy(atlas);
            UnityEngine.Object.Destroy(go); UnityEngine.Object.Destroy(floor); UnityEngine.Object.Destroy(mat); UnityEngine.Object.Destroy(tex);
            notes.Add($"{(run ? "달리기" : "걷기")} 발 띠: {path} | {every}프레임({every * Dt:F3}s) 간격 8컷 · 속도 {motor.PlanarSpeed:F2} m/s | 발목(월드): {string.Join(" / ", feet)}");
        }

        string Shot(Camera cam, string path, string what)
        {
            const int w = 1920, h = 1080, ss = 2;
            var big = new RenderTexture(new RenderTextureDescriptor(w * ss, h * ss, RenderTextureFormat.ARGB32, 24) { sRGB = true, msaaSamples = 1 });
            var small = new RenderTexture(new RenderTextureDescriptor(w, h, RenderTextureFormat.ARGB32, 0) { sRGB = true });
            big.Create(); small.Create();
            var prevT = cam.targetTexture;
            var prevA = RenderTexture.active;
            // 화면 오버레이 UI(길잡이 HUD·조작 안내·일시정지)는 카메라 렌더에 안 들어가므로, 찍는 동안만 카메라 공간 캔버스로 바꾼다
            var overlays = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToList();
            try
            {
                cam.targetTexture = big;
                foreach (var c in overlays) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = 0.5f; }
                Canvas.ForceUpdateCanvases();
                // 촬영 해상도(16:9)로 이름표·방향 화살표를 다시 계산(배치 실행의 화면 크기는 다를 수 있음)
                UnityEngine.Object.FindAnyObjectByType<NameTags>()?.Apply(cam);
                UnityEngine.Object.FindAnyObjectByType<RouteHud>()?.RefreshArrow();
                Canvas.ForceUpdateCanvases();
                cam.Render();
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
            var p = motor.Position;
            Frame(cam, out float feet, out float head, out _);
            var hud = UnityEngine.Object.FindAnyObjectByType<RouteHud>();
            return $"{what}: {path} | 인물 ({p.x:F1},{p.y:F2},{p.z:F1}) yaw {motor.Yaw:F0}° · 화면 발 {feet * 100f:F0}% 정수리 {head * 100f:F0}% | 카메라 yaw {camRig.Yaw:F0}° 거리 {camRig.Distance:F2}m · 당김 {camRig.Pull:F2}m · FOV {camRig.Cam.Lens.FieldOfView:F1}° | HUD \"{(hud != null ? hud.Current : "없음")}\"";
        }

        static Vector3 Ground(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 2f, Vector3.down, out var hit, 6f, 1 << Layers.Ground, QueryTriggerInteraction.Ignore)) p.y = hit.point.y;
            return p;
        }

        static float Yaw(Vector3 d) => Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;

        static string Arg(string name)
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++)
                if (string.Equals(a[i], name, StringComparison.OrdinalIgnoreCase)) return a[i + 1];
            return null;
        }
    }
}
