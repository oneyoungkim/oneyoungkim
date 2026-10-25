// 행인1의 메인이벤트 — 무대 4곳 실제 공간(09 9단계 · 2026-10-25 오픈월드 방향) 시험
//   K01 Stage_WalkIn : Zone1 출입문(국밥집·하루편의점·집·혜성고 방면) → 무대 'Door' 스폰에 접지 · 키트(자리 무대 아님) · 표시 자리(알바·잠·저장·가계부·자리) ·
//                      무대 안 문으로 나오면 Zone1 짝 문 1.6m 안 빈 땅에
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Haengin.Tests
{
    public sealed class M3StageTests
    {
        [UnityTearDown] public IEnumerator TearDown() => M3Lab.TearDown();

        static readonly (string key, string[] funcs)[] Expect =
        {
            ("Gukbap", new[] { "job_serving", "job_dishes" }),
            ("Conv", new[] { "job_counter", "job_stock", "job_waste" }),
            ("Home", new[] { "sleep", "save", "ledger", "haneul" }),
            ("School", new[] { "seat", "class" }),
        };

        [UnityTest, Timeout(900000)]
        public IEnumerator K01_Stage_WalkIn()
        {
            yield return M3Lab.OpenZone1();
            var loader = M3Lab.Runner.Loader;
            var motor = M3Lab.Motor;
            var log = new System.Collections.Generic.List<string>();
            foreach (var (key, funcs) in Expect)
            {
                // Zone1 쪽 문
                var outer = Object.FindObjectsByType<StageDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(d => d.Target == key);
                Assert.IsNotNull(outer, $"Zone1 에 {key} 출입문");
                Assert.IsNotNull(outer.GetComponent<Interactable>(), "출입문 상호작용");
                motor.Teleport(outer.ReturnPos, outer.ReturnYaw);
                yield return Lab.Frames(10);
                // 문 쓰기 → 무대
                outer.Use();
                for (int i = 0; i < 600 && (loader.Busy || loader.Place != key); i++) yield return null;
                Assert.AreEqual(key, loader.Place, $"{key} 로 들어감");
                yield return Lab.Frames(20);
                var place = StagePlace.Find(key);
                Assert.IsFalse(place.Placeholder, $"{key} = 키트 무대(자리 무대 아님)");
                Assert.IsTrue(motor.Grounded, $"{key} 문 안쪽에 접지");
                var mk = PlaceMarker.All.Where(m => m.Place == key).Select(m => m.Func).ToList();
                foreach (var f in funcs) CollectionAssert.Contains(mk, f, $"{key} 표시 자리 {f}");
                Assert.IsNotNull(place.GetComponentInChildren<StageLights>(), "실내 조명");
                // 무대 안 문 → Zone1 짝 문 앞
                var inner = place.GetComponentsInChildren<StageDoor>(true).FirstOrDefault(d => d.Target == SceneLoader.Zone1);
                Assert.IsNotNull(inner, $"{key} 안 문");
                inner.Use();
                for (int i = 0; i < 600 && (loader.Busy || loader.Place != SceneLoader.Zone1); i++) yield return null;
                yield return Lab.Frames(20);
                Assert.AreEqual(SceneLoader.Zone1, loader.Place, "Zone1 로 나옴");
                float dd = Vector3.Distance(motor.Position, outer.transform.position);
                Assert.That(dd, Is.LessThan(2.5f), $"{key}: 나온 자리가 문 앞({dd:F1}m)");
                Assert.IsTrue(motor.Grounded, $"{key}: 나온 자리 접지");
                log.Add($"{key} 표시 {mk.Count} · 나옴 {dd:F1}m");
            }
            Debug.Log("[M3Test] K01 무대 걸어 들어가기: " + string.Join(" · ", log));
        }
    }
}
