// 행인1의 메인이벤트 — 체크포인트 길잡이·HUD·이름표 테스트 (06 문서 8·9장)
// T17: Zone1 을 열고 시우를 체크포인트마다 순간 이동시켜, 길잡이가 순서대로만 넘어가는지(건너뛴 목표는 안 켜짐),
//      지금 목표만 기둥·깃발·빛 기둥·이름표가 켜지고 지난 것은 원판만 옅게 남는지, HUD 글이 맞는지 본다.
// T18: 이름표 — 지금 목표 이름표는 보이고, 먼 출입문 이름표는 숨고, 화면에 보이는 이름표끼리 겹치지 않는다.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Haengin.Tests
{
    public sealed class RouteTests
    {
        PlayerMotor motor;
        RouteGuide guide;
        RouteHud hud;
        NameTags tags;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.captureDeltaTime = Lab.Dt;
            GameState.SetPaused(false);
            yield return Lab.UnloadOurs(default);
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ZoneTests.ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
#else
            yield return SceneManager.LoadSceneAsync("Zone1", LoadSceneMode.Additive);
#endif
            var zone = SceneManager.GetSceneByPath(ZoneTests.ScenePath);
            Assert.IsTrue(zone.IsValid() && zone.isLoaded, "Zone1 장면 열기");
            SceneManager.SetActiveScene(zone);
            yield return null;
            yield return null;
            motor = Object.FindAnyObjectByType<PlayerMotor>();
            guide = Object.FindAnyObjectByType<RouteGuide>();
            hud = Object.FindAnyObjectByType<RouteHud>();
            tags = Object.FindAnyObjectByType<NameTags>();
            if (motor == null || guide == null || hud == null || tags == null)
                Assert.Fail($"Zone1 장면에 길잡이가 없습니다(PlayerMotor {motor != null} · RouteGuide {guide != null} · RouteHud {hud != null} · NameTags {tags != null}). Zone1Builder.Build 를 먼저 돌리세요");
            foreach (var p in Object.FindObjectsByType<PInput>(FindObjectsSortMode.None)) p.enabled = false;
            foreach (var c in Object.FindObjectsByType<CamInput>(FindObjectsSortMode.None)) c.enabled = false;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.captureDeltaTime = 0f;
            yield return null;
        }

        static bool On(GameObject g) => g != null && g.activeInHierarchy;

        void AssertStates(string when)
        {
            for (int k = 0; k < guide.Stops.Length; k++)
            {
                var s = guide.Stops[k];
                string who = $"{when} · {k + 1}번 '{s.Label}'";
                if (k < guide.Index)
                {
                    Assert.IsTrue(On(s.Disc.gameObject), $"{who}: 지난 체크포인트 원판은 남음");
                    Assert.AreSame(guide.DoneDisc, s.Disc.sharedMaterial, $"{who}: 지난 원판은 옅은 재질");
                    Assert.IsFalse(On(s.Pillar) || On(s.Beam) || On(s.Flag) || On(s.Ring) || On(s.Tag), $"{who}: 지난 체크포인트 기둥·빛 기둥·깃발·테두리·이름표 꺼짐");
                }
                else if (k == guide.Index)
                {
                    Assert.IsTrue(On(s.Disc.gameObject) && On(s.Ring) && On(s.Tag), $"{who}: 지금 목표 원판·테두리·이름표 켜짐");
                    Assert.AreSame(guide.ActiveDisc, s.Disc.sharedMaterial, $"{who}: 지금 목표 원판은 진한 재질");
                    Assert.IsTrue(On(s.Pillar) && On(s.Beam) && On(s.Flag), $"{who}: 지금 목표 기둥·빛 기둥·깃발 켜짐");
                }
                else
                    Assert.IsFalse(On(s.Disc.gameObject) || On(s.Pillar) || On(s.Beam) || On(s.Tag), $"{who}: 아직 안 온 체크포인트는 숨김");
            }
        }

        IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        [UnityTest]
        public IEnumerator T17_Route_Guide_Order()
        {
            int reached = 0, completed = 0;
            var order = new List<int>();
            guide.Reached += i => { reached++; order.Add(i); };
            guide.Completed += () => completed++;

            yield return Frames(3);
            Assert.AreEqual(Zone1StartIndex, guide.Index, "시작 목표 = 2번 체크포인트(1번 = 시작 지점)");
            AssertStates("시작");
            StringAssert.StartsWith($"다음: {guide.Stops[1].Label} · ", hud.Current, "HUD 첫 글");
            Assert.That(guide.Distance, Is.InRange(10f, 40f), "시작 지점 → 2번 거리");

            // 순서를 건너뛰어 4번 자리에 가도 넘어가지 않는다
            var s4 = guide.Stops[3];
            motor.Teleport(s4.Pos, 0f);
            yield return Frames(3);
            Assert.AreEqual(1, guide.Index, "건너뛴 체크포인트(4번)에 가도 목표는 그대로 2번");
            Assert.AreEqual(0, reached, "도착 이벤트 없음");

            for (int k = 1; k < guide.Stops.Length; k++)
            {
                var s = guide.Stops[k];
                motor.Teleport(s.Pos, 0f);
                yield return Frames(2);
                Assert.AreEqual(k + 1, guide.Index, $"{k + 1}번 '{s.Label}' 도착 → 다음 목표");
                if (k + 1 < guide.Stops.Length)
                {
                    yield return Frames(1);
                    AssertStates($"{k + 1}번 도착 후");
                    StringAssert.StartsWith("도착: " + s.Label, hud.Current, "도착 직후 HUD = 도착 알림");
                    // 알림(ArriveFlash 초)이 끝나면 다음 목표 글로 돌아온다(시간 고정 여부와 상관없이 기다림, 최대 6000프레임)
                    int n = 0;
                    while (!hud.Current.StartsWith("다음:") && n++ < 6000) yield return null;
                    StringAssert.StartsWith($"다음: {guide.Stops[k + 1].Label} · ", hud.Current, "알림 뒤 HUD = 다음 목표");
                }
            }
            Assert.IsTrue(guide.Finished, "마지막까지 끝남");
            Assert.IsNull(guide.CurrentTarget, "끝나면 목표 없음");
            Assert.AreEqual(guide.Stops.Length - 1, reached, "도착 이벤트 수");
            CollectionAssert.AreEqual(Enumerable.Range(1, guide.Stops.Length - 1).ToList(), order, "도착 순서");
            Assert.AreEqual(1, completed, "완주 이벤트 1번");
            AssertStates("완주");
            yield return Frames(1);
            StringAssert.StartsWith("도착: " + guide.Stops[guide.Stops.Length - 1].Label, hud.Current, "완주 HUD");
            Debug.Log($"[M1Test] 길잡이: 체크포인트 {guide.Stops.Length}개 순서대로 도착 {reached} · 완주 {completed} · 마지막 HUD \"{hud.Current}\"");
        }

        const int Zone1StartIndex = 1;

        [UnityTest]
        public IEnumerator T18_NameTags_Visible_No_Overlap()
        {
            // 시작 지점에서 지금 목표(2번) 쪽을 보고 카메라를 등 뒤로
            var route = Object.FindAnyObjectByType<RouteData>();
            var rig = Object.FindAnyObjectByType<CamRig>();
            var to = guide.CurrentTarget.Pos - route.SpawnPos;
            motor.Teleport(route.SpawnPos, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg);
            if (rig != null) rig.SnapBehind();
            yield return Frames(10);
            var cam = Camera.main;
            Assert.IsNotNull(cam, "Main Camera");
            var target = guide.CurrentTarget.Tag.GetComponent<TextMeshPro>();
            Assert.IsTrue(NameTags.IsShown(target), $"지금 목표 이름표 '{target.text}' 보임");
            var far = tags.Find("반시우네");
            Assert.IsNotNull(far, "반시우네 출입문 이름표가 있음");
            Assert.That(Vector3.Distance(cam.transform.position, far.transform.position), Is.GreaterThan(30f), "시작 지점에서 반시우네는 멂");
            Assert.IsFalse(NameTags.IsShown(far), "먼 출입문 이름표는 숨김");

            // 화면에 보이는 이름표 사각형끼리 겹치지 않음(체크포인트 도는 길 몇 군데에서)
            int checkedViews = 0, maxShown = 0;
            foreach (int k in new[] { 1, 2, 4, 6, 7 })
            {
                var s = guide.Stops[k];
                motor.Teleport(s.Pos, 0f);
                yield return Frames(20);
                var shown = tags.Tags.Where(t => NameTags.IsShown(t.Text)).Select(t => t.Text).ToList();
                maxShown = Mathf.Max(maxShown, shown.Count);
                var rects = shown.Select(t => ScreenRect(cam, t)).ToList();
                for (int a = 0; a < rects.Count; a++)
                    for (int b = a + 1; b < rects.Count; b++)
                        Assert.IsFalse(rects[a].Overlaps(rects[b]), $"{k + 1}번 근처: 이름표 '{shown[a].text}' 와 '{shown[b].text}' 가 화면에서 겹침");
                checkedViews++;
            }
            Debug.Log($"[M1Test] 이름표: 전체 {tags.Tags.Length} · 검사한 자리 {checkedViews} · 한 화면 최대 {maxShown}개 보임 · 겹침 0");
        }

        /// 화면 사각형(NameTags 와 같은 셈, 여유 없이)
        static Rect ScreenRect(Camera cam, TextMeshPro t)
        {
            var vp = cam.WorldToViewportPoint(t.transform.position);
            float pxPerM = cam.pixelHeight / (2f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad));
            float s = t.transform.localScale.x, k = pxPerM / Mathf.Max(0.1f, vp.z);
            float w = t.textBounds.size.x * s * k, h = t.textBounds.size.y * s * k;
            return new Rect(vp.x * cam.pixelWidth - w / 2f, vp.y * cam.pixelHeight, w, h);
        }
    }
}
