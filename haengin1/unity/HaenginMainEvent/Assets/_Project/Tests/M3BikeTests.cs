// 행인1의 메인이벤트 — M3 8단계 배달 자전거 테스트(docs/09_M3_버티컬슬라이스_설계.md 6-2 D16·D17)
//   D16 Bike_Numbers : 시험장 경사로(0° · 7° · 12° · −8°)에서 속도표 ± 5% · 일어서기 박자 판정(0.45~0.70초 → 0.8~2.0 m/s, 빠른 박자·같은 쪽 = 헛발) ·
//                      국물 감소 규칙(턱 0.18m −8 · 0.10m 0 · 벽 −15 · 옆 가속 −10/초 · 내리막 과속 −4/초 · 급브레이크 −5 · 헛발 −2) ·
//                      모든 입력 조합(무작위 40초)에서 '내림' 0 · 오르막 입력 끊김 → 밀림 ≤ 0.5m · 넘어짐 0 · 5-2 케이크(일어서기 막음)
//   D17 Bike_Route_Bot : Zone1 1화 코스 봇 — 경비실·꼭대기 집 정차·건네기, 태오 추월 오르막 1·내리막 1, 지각 대사 N = 실제 늦은 분, ≤ 150초
//   D18 Bike_Pedal_Pose: 같은 코스 봇 — 페달 클립 상태(정차·출발·순항·댄싱·정지)를 모두 거치고 골반 = 클립 자리(평균 ≤ 2cm) · 발목 = 페달 + 클립 차이(95% ≤ 2cm) · 정차 왼발 = 땅(≤ 3cm)
// 실제 Player 프리팹(PInput 끔). 이름이 M3… = 입력 테스트 뒤.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Haengin.Tests
{
    public sealed class M3BikeTests
    {
        const string PlayerPath = "Assets/_Project/Prefabs/Player.prefab";
        GameObject player;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.captureDeltaTime = Lab.Dt;
            TimeFx.Reset();
            yield return Lab.FreshScene();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (player != null) Object.Destroy(player);
            TimeFx.Reset();
            Time.captureDeltaTime = 0f;
            yield return M3Lab.TearDown();
        }

        BikeMotor Spawn()
        {
            GameObject pf = null;
#if UNITY_EDITOR
            pf = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
#endif
            if (pf == null) Assert.Ignore("Player 프리팹 없음");
            var holder = new GameObject("H"); holder.SetActive(false);
            player = Object.Instantiate(pf, Vector3.zero, Quaternion.identity, holder.transform);
            foreach (var i in player.GetComponentsInChildren<PInput>(true)) i.enabled = false;
            foreach (var i in player.GetComponentsInChildren<BikeInput>(true)) i.enabled = false;
            player.transform.SetParent(null, true); Object.Destroy(holder);
            var m = player.GetComponent<BikeMotor>();
            Assert.IsNotNull(m, "Player 프리팹에 BikeMotor");
            Assert.IsNotNull(player.GetComponent<SoupGauge>(), "SoupGauge");
            return m;
        }

        /// 경사로(위쪽 면이 start 에서 시작해 +Z 로 angle 만큼 오름/내림) — 걷는 면(Ground)
        static void Ramp(Vector3 start, float angle, float length, float width = 6f)
        {
            float a = angle * Mathf.Deg2Rad;
            var dir = new Vector3(0f, Mathf.Sin(a), Mathf.Cos(a));
            var n = new Vector3(0f, Mathf.Cos(a), -Mathf.Sin(a));
            var c = start + dir * (length * 0.5f) - n * 0.25f;
            Lab.Box($"Ramp{angle}", c, new Vector3(width, 0.5f, length), Quaternion.Euler(-angle, 0f, 0f), Layers.Ground);
        }

        static IEnumerator Ride(BikeMotor m, float secs, float steer, float pedal, float brake, System.Action each = null)
        {
            for (float t = 0f; t < secs; t += Lab.Dt) { m.SetInput(steer, pedal, brake); each?.Invoke(); yield return null; }
        }

        // ───────────────────────── D16 ① 속도표
        [UnityTest, Timeout(600000)]
        public IEnumerator D16a_Bike_Speeds()
        {
            var log = new List<string>();
            Lab.Floor(0f, 400f);
            var m = Spawn();
            var soup = player.GetComponent<SoupGauge>();
            // 평지
            m.Mount(new Vector3(-60f, 0f, -150f), 0f);
            soup.ResetTo(100f, 0f, "국물");
            yield return Ride(m, 6f, 0f, 1f, 0f);
            float flat = m.Speed;
            yield return Ride(m, 1f, 0f, 0f, 0f);
            float coast = flat - m.Speed;
            log.Add($"평지 {flat:F2} m/s(5.0) · 페달 놓고 1초 −{coast:F2}(0.6)");
            Assert.That(flat, Is.EqualTo(5.0f).Within(0.25f), "평지 최고 5.0 ± 5%");
            Assert.That(coast, Is.EqualTo(0.6f).Within(0.06f), "페달 놓으면 0.6 m/s²");
            // 브레이크
            yield return Ride(m, 3f, 0f, 1f, 0f);
            float v0 = m.Speed;
            yield return Ride(m, 0.5f, 0f, 0f, 0.6f);
            float dec = (v0 - m.Speed) / 0.5f / 0.6f;
            log.Add($"브레이크 {dec:F2} m/s²(4.0)");
            Assert.That(dec, Is.EqualTo(4.0f).Within(0.3f), "브레이크 4 m/s²");
            m.Dismount();
            // 7°: 엉덩이 들기 3.0
            Ramp(new Vector3(0f, 0f, -150f), 7f, 60f);
            m.Mount(new Vector3(0f, 0.05f, -149f), 0f);
            yield return Ride(m, 9f, 0f, 1f, 0f);
            float v7 = m.Speed; var p7 = m.Posture; float s7 = m.SlopeDeg;
            log.Add($"7° 오르막 {v7:F2} m/s(3.0) · 자세 {p7} · 잰 경사 {s7:F1}°");
            Assert.That(s7, Is.EqualTo(7f).Within(0.6f), "경사 재기");
            Assert.That(v7, Is.EqualTo(3.0f).Within(0.15f), "5~9° 최고 3.0 ± 5%");
            Assert.AreEqual(BikePosture.Hip, p7, "5~9° = 엉덩이 들기(자동)");
            m.Dismount();
            // −8° 내리막: 7.0
            Ramp(new Vector3(30f, 12.6f, -150f), -8f, 90f);
            m.Mount(new Vector3(30f, 12.65f, -149f), 0f);
            soup.ResetTo(100f, 0f, "국물");
            yield return Ride(m, 10f, 0f, 1f, 0f);
            float vd = m.Speed; float lostDown = 100f - soup.Value;
            log.Add($"−8° 내리막 {vd:F2} m/s(7.0) · 국물 −{lostDown:F1}(6 m/s 넘으면 초당 −4)");
            Assert.That(vd, Is.EqualTo(7.0f).Within(0.35f), "내리막 최고 7.0 ± 5%");
            Assert.That(lostDown, Is.GreaterThan(1f), "내리막 과속은 국물이 준다");
            m.Dismount();
            // 12°: 일어서기 리듬
            Ramp(new Vector3(60f, 0f, -150f), 12f, 80f);
            var rows = new List<string>();
            foreach (float gap in new[] { 0.45f, 0.575f, 0.70f })
            {
                m.Mount(new Vector3(60f, 0.05f, -149f), 0f);
                int wl0 = m.Whiffs;
                float tb = 0f, sum = 0f; int n = 0; float vmin = 99f, vmax = 0f; bool right = false;
                for (float t = 0f; t < 10f; t += Lab.Dt)
                {
                    m.SetInput(0f, 0f, 0f);
                    tb += Lab.Dt;
                    if (tb >= gap) { tb -= gap; m.PressBeat(right); right = !right; }
                    if (t > 5f) { sum += m.Speed; n++; vmin = Mathf.Min(vmin, m.Speed); vmax = Mathf.Max(vmax, m.Speed); }
                    yield return null;
                }
                rows.Add($"박자 {gap:F3}초 → 평균 {sum / n:F2} · 범위 {vmin:F2}~{vmax:F2} m/s · 자세 {m.Posture} · 헛발 {m.Whiffs - wl0}");
                Assert.That(vmin, Is.GreaterThanOrEqualTo(0.8f * 0.95f), $"박자 {gap} 최저 ≥ 0.8");
                Assert.That(vmax, Is.LessThanOrEqualTo(2.0f * 1.05f), $"박자 {gap} 최고 ≤ 2.0");
                Assert.AreEqual(0, m.Whiffs - wl0, "바른 박자는 헛발 0");
                Assert.AreEqual(BikePosture.Stand, m.Posture, "9° 넘음 = 일어서기");
                m.Dismount();
            }
            log.Add("12° 리듬: " + string.Join(" | ", rows));
            // 빠른 박자(0.3초)·같은 쪽 두 번 = 헛발
            m.Mount(new Vector3(60f, 0.05f, -149f), 0f);
            soup.ResetTo(100f, 0f, "국물");
            int wb = m.Whiffs;
            m.PressBeat(false); yield return Ride(m, 0.6f, 0f, 0f, 0f);
            float vb = m.Speed;
            m.PressBeat(true); yield return Ride(m, 0.3f, 0f, 0f, 0f);
            m.PressBeat(false); yield return null;                        // 0.3초 만 = 헛발
            int w1 = m.Whiffs - wb;
            yield return Ride(m, 0.6f, 0f, 0f, 0f);
            m.PressBeat(false); yield return null;                        // 같은 쪽 두 번 = 헛발
            int w2 = m.Whiffs - wb;
            log.Add($"헛발: 0.3초 박자 {w1} · 같은 쪽 {w2 - w1} · 국물 −{100f - soup.Value:F0}(헛발마다 2)");
            Assert.AreEqual(1, w1, "0.45초보다 빠른 박자 = 헛발");
            Assert.AreEqual(2, w2, "같은 쪽 두 번 = 헛발");
            Assert.That(100f - soup.Value, Is.EqualTo(4f).Within(0.01f), "헛발마다 국물 −2");
            m.Dismount();
            // 5-2 케이크: 일어서기 막음(박자 무시) — 앉아서만, 9° 넘는 데선 최고 1.2
            m.Mount(new Vector3(60f, 0.05f, -149f), 0f);
            m.NoStand = true;
            int b0 = m.Beats, wh0 = m.Whiffs;
            bool rr = false;
            for (float t = 0f; t < 6f; t += Lab.Dt)
            {
                m.SetInput(0f, 1f, 0f);
                if (Mathf.Repeat(t, 0.55f) < Lab.Dt) { m.PressBeat(rr); rr = !rr; }
                yield return null;
            }
            log.Add($"케이크(일어서기 막음): 박자 {m.Beats - b0} · 헛발 {m.Whiffs - wh0} · 속도 {m.Speed:F2}(앉아서 최고 1.2) · 자세 {m.Posture}");
            Assert.AreEqual(0, m.Beats - b0, "5-2 는 일어서기 입력 무시");
            Assert.AreEqual(0, m.Whiffs - wh0, "5-2 는 헛발도 없음(무시)");
            Assert.That(m.Speed, Is.LessThanOrEqualTo(1.2f * 1.05f), "케이크 9° 넘음 최고 1.2");
            m.NoStand = false;
            m.Dismount();
            Debug.Log("[M3Test] D16a 자전거 속도표: " + string.Join(" · ", log));
        }

        // ───────────────────────── D16 ② 국물
        [UnityTest, Timeout(600000)]
        public IEnumerator D16b_Bike_Soup()
        {
            var log = new List<string>();
            Lab.Floor(0f, 400f);
            var m = Spawn();
            var soup = player.GetComponent<SoupGauge>();
            // 턱 0.18m 와 0.10m
            Lab.Box("턱18", new Vector3(-40f, 0.09f, -100f), new Vector3(8f, 0.18f, 30f), Quaternion.identity, Layers.Ground);
            Lab.Box("턱10", new Vector3(-20f, 0.05f, -100f), new Vector3(8f, 0.10f, 30f), Quaternion.identity, Layers.Ground);
            foreach (var (x, h) in new[] { (-40f, 0.18f), (-20f, 0.10f) })
            {
                m.Mount(new Vector3(x, 0f, -125f), 0f);
                soup.ResetTo(100f, 0f, "국물");
                int b0 = m.Bumps;
                yield return Ride(m, 5f, 0f, 1f, 0f);
                log.Add($"턱 {h:F2}m: 충격 {m.Bumps - b0} · 국물 −{100f - soup.Value:F0}");
                if (h >= 0.15f) { Assert.That(m.Bumps - b0, Is.GreaterThanOrEqualTo(1), "0.18m 턱 = 충격"); Assert.That(100f - soup.Value, Is.EqualTo(8f).Within(0.01f), "턱 −8"); }
                else { Assert.AreEqual(0, m.Bumps - b0, "0.10m 턱은 충격 아님"); Assert.That(100f - soup.Value, Is.LessThan(0.5f), "0.10m 턱은 국물 그대로"); }
                m.Dismount();
            }
            // 벽
            Lab.Box("벽", new Vector3(0f, 1.5f, -95f), new Vector3(8f, 3f, 0.6f), Quaternion.identity, Layers.Wall);
            m.Mount(new Vector3(0f, 0f, -110f), 0f);
            soup.ResetTo(100f, 0f, "국물");
            yield return Ride(m, 5f, 0f, 1f, 0f);
            log.Add($"벽: 부딪힘 {m.Hits} · 국물 −{100f - soup.Value:F0} · 원인 {soup.LastCause}");
            Assert.That(m.Hits, Is.GreaterThanOrEqualTo(1), "벽에 부딪힘");
            Assert.That(100f - soup.Value, Is.EqualTo(15f).Within(0.01f), "부딪힘 −15(한 번)");
            m.Dismount();
            // 옆 가속: 5 m/s 에서 끝까지 꺾기
            m.Mount(new Vector3(40f, 0f, -150f), 0f);
            yield return Ride(m, 5f, 0f, 1f, 0f);
            soup.ResetTo(100f, 0f, "국물");
            float latMax = 0f;
            yield return Ride(m, 1f, 1f, 1f, 0f, () => latMax = Mathf.Max(latMax, m.LateralAccel));
            log.Add($"옆 가속 최대 {latMax:F1} m/s² · 1초 국물 −{100f - soup.Value:F1}(3 넘으면 초당 10)");
            Assert.That(latMax, Is.GreaterThan(3f), "급회전 옆 가속 > 3");
            Assert.That(100f - soup.Value, Is.InRange(5f, 10.5f), "옆 가속 초당 −10");
            m.Dismount();
            // 급브레이크
            m.Mount(new Vector3(80f, 0f, -150f), 0f);
            yield return Ride(m, 4f, 0f, 1f, 0f);
            soup.ResetTo(100f, 0f, "국물");
            yield return Ride(m, 2f, 0f, 0f, 1f);
            log.Add($"급브레이크: 국물 −{100f - soup.Value:F0}(5)");
            Assert.That(100f - soup.Value, Is.EqualTo(5f).Within(0.01f), "급브레이크 −5(누를 때 한 번)");
            m.Dismount();
            // 1화 바닥 30
            m.Mount(new Vector3(0f, 0f, -110f), 0f);
            soup.ResetTo(40f, 30f, "국물");
            yield return Ride(m, 5f, 0f, 1f, 0f);
            log.Add($"1화 바닥: 40 → 벽 → {soup.Value:F0}(30 아래로 안 감)");
            Assert.That(soup.Value, Is.EqualTo(30f).Within(0.01f), "1화는 30 아래로 안 내려감");
            m.Dismount();
            Debug.Log("[M3Test] D16b 국물: " + string.Join(" · ", log));
        }

        // ───────────────────────── D16 ③ 내림 0 · 밀림 · 넘어짐
        [UnityTest, Timeout(600000)]
        public IEnumerator D16c_Bike_NoDismount()
        {
            Lab.Floor(0f, 400f);
            Ramp(new Vector3(0f, 0f, -150f), 12f, 60f);
            Ramp(new Vector3(30f, 0f, -150f), 7f, 60f);
            var m = Spawn();
            // 오르막 입력 끊김 → 밀림 ≤ 0.5m · 넘어짐 0
            m.Mount(new Vector3(0f, 0.05f, -149f), 0f);
            bool r = false;
            for (int i = 0; i < 10; i++) { m.PressBeat(r); r = !r; yield return Ride(m, 0.55f, 0f, 0f, 0f); }
            var stopAt = m.Position; float zMax = stopAt.z;
            float minY = 99f, upMin = 1f;
            for (float t = 0f; t < 6f; t += Lab.Dt)
            {
                m.SetInput(0f, 0f, 0f);
                zMax = Mathf.Max(zMax, m.Position.z);
                upMin = Mathf.Min(upMin, Vector3.Dot(player.transform.up, Vector3.up));
                yield return null;
            }
            float back = zMax - m.Position.z;
            Assert.That(back, Is.LessThanOrEqualTo(0.5f + 0.02f), "오르막 입력 끊김 → 밀림 ≤ 0.5m");
            Assert.That(back, Is.GreaterThan(0.2f), "입력이 끊기면 실제로 조금 밀림");
            Assert.That(m.Speed, Is.EqualTo(0f).Within(0.01f), "밀리다 멈춤");
            Assert.That(upMin, Is.GreaterThan(0.99f), "넘어짐 0(몸이 서 있음)");
            Assert.IsTrue(m.Mounted, "내리지 않음");
            // 모든 입력 조합(무작위 40초): '내림' 0
            var rng = new System.Random(16);
            int frames = 0, mountedFrames = 0;
            m.Teleport(new Vector3(30f, 0.05f, -149f), 0f);
            for (float t = 0f; t < 40f; t += Lab.Dt)
            {
                float st = (float)(rng.NextDouble() * 2 - 1), pd = (float)rng.NextDouble(), br = rng.NextDouble() < 0.2 ? (float)rng.NextDouble() : 0f;
                m.SetInput(st, pd, br);
                if (rng.NextDouble() < 0.08) m.PressBeat(rng.NextDouble() < 0.5);
                if (rng.NextDouble() < 0.03) m.PressHandover();
                frames++; if (m.Mounted) mountedFrames++;
                yield return null;
            }
            Assert.AreEqual(frames, mountedFrames, "모든 입력 조합에서 '내림' 상태 0");
            Assert.AreEqual(0, m.Dismounts, "입력으로 내린 횟수 0");
            Debug.Log($"[M3Test] D16c 내림·밀림: 12° 입력 끊김 → 뒤로 {back:F2}m 밀리고 멈춤(≤ 0.5) · 몸 기울기 최소 cos {upMin:F3} · 무작위 입력 {frames}프레임 모두 탄 채 · 내림 0");
            m.Dismount();
        }

        // ───────────────────────── D17 코스 봇
        [UnityTest, Timeout(1200000)]
        public IEnumerator D17_Bike_Route_Bot()
        {
            yield return M3Lab.OpenZone1();
            var bc = Object.FindAnyObjectByType<BikeCourse>(FindObjectsInactive.Include);
            Assert.IsNotNull(bc, "Zone1 에 BikeCourse(BikeSetup)");
            bc.gameObject.SetActive(true);
            bc.Bot = true;
            float g0 = Time.time;
            yield return bc.RunAll();
            float game = Time.time - g0;
            var log = $"게임 {game:F1}초 · 경비실 {bc.Stops[0].Done} · 꼭대기 집 {bc.Stops[1].Done}(도착 {Clk(bc.Stops[1].ArriveClock)} · 약속 {Clk(bc.Stops[1].Appt)}) · 지각 {bc.LateMin}분({bc.LateSec:F0}초) · " +
                      $"태오 오르막 {bc.UpPasses}·내리막 {bc.DownPasses} · 측면 트래킹 {bc.SideShots}번 · 국물 {bc.Soup.Value:F0} · 대사 [{string.Join(" / ", bc.Lines)}] · 기록 [{string.Join(" | ", bc.Log)}]";
            Debug.Log("[M3Test] D17 1화 코스 봇: " + log);
            Assert.IsTrue(bc.Stops[0].Done, "경비실 정차·건네기");
            Assert.IsTrue(bc.Stops[1].Done, "꼭대기 집 정차·건네기");
            Assert.AreEqual(1, bc.UpPasses, "태오 추월 오르막 1");
            Assert.AreEqual(1, bc.DownPasses, "태오 추월 내리막 1");
            int want = Mathf.Max(1, Mathf.FloorToInt((bc.Stops[1].ArriveClock - bc.Stops[1].Appt) / 60f));
            Assert.AreEqual(want, bc.LateMin, "지각 N = 실제 늦은 분(1화는 1 이상)");
            Assert.IsTrue(bc.Lines.Any(l => l.Contains($"어이. {bc.LateMin}분 늦었다.")), "지각 대사");
            Assert.That(game, Is.LessThanOrEqualTo(150f), "코스 ≤ 150초");
            Assert.IsFalse(bc.Riding, "끝나면 내림(코드)");
            Assert.That(bc.SideShots, Is.GreaterThanOrEqualTo(1), "오르막 측면 트래킹");
        }

        // ───────────────────────── D18 페달 클립 자세(docs/14 6-4, 2026-10-26): 코스 봇을 돌리며 발·골반이 클립·페달에 붙어 있는지
        [UnityTest, Timeout(1200000)]
        public IEnumerator D18_Bike_Pedal_Pose()
        {
            yield return M3Lab.OpenZone1();
            var bc = Object.FindAnyObjectByType<BikeCourse>(FindObjectsInactive.Include);
            Assert.IsNotNull(bc, "Zone1 에 BikeCourse");
            bc.gameObject.SetActive(true);
            bc.Bot = true;
            bool done = false;
            IEnumerator Run() { yield return bc.RunAll(); done = true; }
            bc.StartCoroutine(Run());
            var seen = new Dictionary<BikeRider.Mode, int>();
            var hips = new List<float>(); var feetOn = new List<float>(); var feetDown = new List<float>();
            var byMode = new Dictionary<BikeRider.Mode, List<float>>();
            int diag = 0;
            BikeRider rider = null;
            float t0 = Time.time;
            while (!done && Time.time - t0 < 200f)
            {
                yield return null;
                if (!bc.Riding) continue;
                if (rider == null || !rider.isActiveAndEnabled) rider = Object.FindObjectsByType<BikeRider>(FindObjectsSortMode.None).FirstOrDefault(r => r.Riding);
                if (rider == null) continue;
                seen[rider.Current] = (seen.TryGetValue(rider.Current, out var c) ? c : 0) + 1;
                if (rider.Current == BikeRider.Mode.Cruise || rider.Current == BikeRider.Mode.Climb || rider.Current == BikeRider.Mode.Coast) { hips.Add(rider.HipGap); feetOn.Add(Mathf.Max(rider.FootGapL, rider.FootGapR)); }
                if (!byMode.TryGetValue(rider.Current, out var bm)) byMode[rider.Current] = bm = new List<float>();
                bm.Add(Mathf.Max(rider.FootGapL, rider.FootGapR));
                if (rider.Current == BikeRider.Mode.Hold) feetDown.Add(rider.FootGapL);
                if (rider.Current == BikeRider.Mode.Cruise && diag < 3 && seen[BikeRider.Mode.Cruise] % 97 == 50)
                {
                    diag++;
                    var bt = bc.Bike.transform; var an = rider.GetComponent<Animator>();
                    Vector3 L(Vector3 w) => bt.InverseTransformPoint(w);
                    var st = an.GetCurrentAnimatorStateInfo(0);
                    Debug.Log($"[M3Test] D18 진단: 상태 {(st.IsName(BikeRider.CruiseState) ? "BikeCruise" : st.shortNameHash.ToString())} t {st.normalizedTime:F3} · θ {bc.Motor.Crank:F1} · 왼발 {L(an.GetBoneTransform(HumanBodyBones.LeftFoot).position):F3} 목표 {L(rider.TargetL):F3} 페달L {L(bc.Bike.PedalL.position):F3} 클립 차이 {rider.LastPose.AnkleL:F3} · 오른발 {L(an.GetBoneTransform(HumanBodyBones.RightFoot).position):F3} 목표 {L(rider.TargetR):F3} 페달R {L(bc.Bike.PedalR.position):F3} · 골반 {L(an.GetBoneTransform(HumanBodyBones.Hips).position):F3} 클립 {rider.LastPose.Hips:F3} · 모델 {rider.transform.localPosition:F3}/{rider.transform.localEulerAngles:F1} 자전거 {bt.localPosition:F3}/{bt.localEulerAngles:F1} · 손 {L(an.GetBoneTransform(HumanBodyBones.LeftHand).position):F3} 클립 {rider.LastPose.WristL:F3} · 아바타 {an.avatar?.name} 사람 {an.isHuman} 배율 {an.humanScale:F3}");
                }
            }
            float P95(List<float> v) { if (v.Count == 0) return -1f; var o = v.OrderBy(x => x).ToList(); return o[(int)(o.Count * 0.95f)]; }
            string log = $"상태 [{string.Join(", ", seen.Select(kv => kv.Key + " " + kv.Value))}] · 골반–클립 평균 {(hips.Count > 0 ? hips.Average() * 100f : -1):F2}cm 95% {P95(hips) * 100f:F2}cm · " +
                         $"발목–목표(페달) 평균 {(feetOn.Count > 0 ? feetOn.Average() * 100f : -1):F2}cm 95% {P95(feetOn) * 100f:F2}cm · 정차 왼발–땅 95% {P95(feetDown) * 100f:F2}cm · 상태 바뀜 {(rider != null ? rider.ModeChanges : 0)} · 상태별 발 평균 [{string.Join(", ", byMode.Select(kv => kv.Key + " " + (kv.Value.Average() * 100f).ToString("F1") + "cm"))}]";
            Debug.Log("[M3Test] D18 페달 클립: " + log);
            Assert.IsTrue(done, "코스 끝");
            foreach (var m in new[] { BikeRider.Mode.Start, BikeRider.Mode.Cruise, BikeRider.Mode.Climb, BikeRider.Mode.Stop })
                Assert.That(seen.TryGetValue(m, out var n) ? n : 0, Is.GreaterThan(0), $"상태 {m} 거침");
            Assert.That(hips.Average(), Is.LessThanOrEqualTo(0.02f), "골반 = 클립 자리(평균 ±2cm)");
            Assert.That(P95(feetOn), Is.LessThanOrEqualTo(0.02f), "발목 = 페달 자리(95% ≤ 2cm)");
            if (feetDown.Count > 0) Assert.That(P95(feetDown), Is.LessThanOrEqualTo(0.03f), "정차 왼발 = 땅 자리(95% ≤ 3cm)");
        }

        static string Clk(float s) { int x = Mathf.FloorToInt(s); return $"{x / 3600:00}:{x / 60 % 60:00}:{x % 60:00}"; }
    }
}
