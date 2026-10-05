// 행인1의 메인이벤트 — 1구역 자동 걷기 테스트 (docs/07_M1_조작_설계.md 9-2 T14) + 게임 화면 촬영
// Zone1.unity 를 열고, 테스트가 NavMesh 를 임시로 구워(저장 안 함) 체크포인트 1→10 사이 경로 모서리를 따라
// PlayerMotor.SetMoveInput 으로 걷게(또는 달리게) 한다. 검사: 제한 시간 안 도착 · 땅 아래로 떨어지지 않음(y > 지면 − 1) ·
// 끼어서 멈추지 않음(3초 동안 이동 < 0.2m 면 실패). 카메라가 벽 안에 들어가거나 가린 프레임 수도 센다.
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
            Time.captureDeltaTime = 0f;
            yield return null;
        }

        // ───────────────────────── 테스트
        [UnityTest, Timeout(900000)]
        public IEnumerator T14_Zone1_Route_Walk() => Route(false);

        [UnityTest, Timeout(900000)]
        public IEnumerator T15_Zone1_Route_Run() => Route(true);

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
            s.Limit = 1.5f * s.Len / speed + 4f;
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

        // ───────────────────────── 게임 화면 촬영 (-m1shots <폴더> 를 줄 때만, -nographics 없이)
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

            // (1) 시작 지점: 서 있는 그대로 1.5초
            motor.Teleport(route.SpawnPos, route.SpawnYaw);
            camRig.SnapBehind();
            yield return Lab.Seconds(1.5f);
            notes.Add(Shot(camera, Path.Combine(dir, "m1_game_1_start.png"), "시작 지점"));

            // (2) 오르막 중간: 시우네 집 앞(5) → 꼭대기 계단참(6)으로 걷다가 높이 절반에서
            motor.Teleport(Ground(route.Points[4]), Yaw(route.Points[5] - route.Points[4]));
            camRig.SnapBehind();
            yield return Lab.Seconds(0.3f);
            float midY = (route.Points[4].y + route.Points[5].y) / 2f;
            var s2 = new Seg();
            yield return WalkUntil(route.Points[5], 1.5f, false, s2, () => motor.Position.y >= midY);
            notes.Add(Shot(camera, Path.Combine(dir, "m1_game_2_uphill.png"), $"오르막 중간(y {motor.Position.y:F1})"));

            // (3) 와룡공원 도착: 계단참(6) → 공터(7)까지 걸어 들어와 멈춘 뒤 1.5초
            motor.Teleport(Ground(route.Points[5]), 0f);
            camRig.SnapBehind();
            yield return Lab.Seconds(0.2f);
            var s3 = new Seg();
            yield return WalkUntil(route.Points[6], Mathf.Min(route.Radii[6], 2.5f), false, s3, null);
            camRig.RecenterBehind();          // 걷는 동안은 카메라가 따라 돌지 않으므로(07: 자동 정렬은 달릴 때만) 도착해서 Q/L1 정렬
            yield return Lab.Seconds(1.5f);
            notes.Add(Shot(camera, Path.Combine(dir, "m1_game_3_waryong.png"), "와룡공원 공터 도착"));

            // (4) 덤: 상가거리 달리기 중(FOV 49·거리 4.3)
            motor.Teleport(route.SpawnPos, route.SpawnYaw);
            camRig.SnapBehind();
            yield return Lab.Seconds(0.3f);
            var s4 = new Seg();
            float t4 = 0f;
            yield return WalkUntil(route.Points[1], 1.0f, true, s4, () => (t4 += Dt) > 2.2f);
            notes.Add(Shot(camera, Path.Combine(dir, "m1_game_4_run.png"), "상가거리 달리기"));

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

        string Shot(Camera cam, string path, string what)
        {
            const int w = 1920, h = 1080, ss = 2;
            var big = new RenderTexture(new RenderTextureDescriptor(w * ss, h * ss, RenderTextureFormat.ARGB32, 24) { sRGB = true, msaaSamples = 1 });
            var small = new RenderTexture(new RenderTextureDescriptor(w, h, RenderTextureFormat.ARGB32, 0) { sRGB = true });
            big.Create(); small.Create();
            var prevT = cam.targetTexture;
            var prevA = RenderTexture.active;
            try
            {
                cam.targetTexture = big;
                cam.Render();
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
                cam.targetTexture = prevT;
                RenderTexture.active = prevA;
                big.Release(); small.Release();
                UnityEngine.Object.Destroy(big); UnityEngine.Object.Destroy(small);
            }
            var p = motor.Position;
            return $"{what}: {path} | 인물 ({p.x:F1},{p.y:F2},{p.z:F1}) yaw {motor.Yaw:F0}° | 카메라 거리 {camRig.Distance:F2}m · 당김 {camRig.Pull:F2}m · FOV {camRig.Cam.Lens.FieldOfView:F1}°";
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
