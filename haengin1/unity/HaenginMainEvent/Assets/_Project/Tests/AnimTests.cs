// 행인1의 메인이벤트 — 시우 리깅 모델·애니메이터 테스트 (docs/07_M1_조작_설계.md 5-4·9-2 T24·T25)
// 실제 Player 프리팹(시우 FBX + Animator + LocoAnim)을 코드로 만든 평지에 세워, PlayerMotor 에 입력을 넣고
//   T24: 애니메이터가 정지 → 대기, 걷기 속도 → 걷기, 달리기 → 달리기, 다시 멈추면 대기로 가는지(+ 패드 느린 걷기 = 걷기를 느리게)
//   T25: 걷기·달리기 중 디딤발이 땅에서 미끄러지는 속도(발목 뼈의 수평 속도, 디딤 구간만)와 발이 땅에 붙어 있는지(발목 높이)를 잰다.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Haengin.Tests
{
    public sealed class AnimTests
    {
        const float Dt = Lab.Dt;
        const string PrefabPath = "Assets/_Project/Prefabs/Player.prefab";

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

        /// 실제 Player 프리팹을 평지에 세운다(사람 입력 끔 — 테스트가 SetMoveInput 으로 넣음)
        static PlayerMotor Spawn(Vector3 feet, float yaw)
        {
            GameObject prefab = null;
#if UNITY_EDITOR
            prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
#endif
            if (prefab == null) { Assert.Ignore("Player 프리팹을 에디터에서만 읽을 수 있음"); return null; }
            // 꺼진 부모 아래에서 만들어 PInput 을 켜지기 전에 끈다(켜지면 실제 HInput 에셋을 켜서 뒤따르는 InputTests 의 가짜 장치 입력이 막혔다)
            var holder = new GameObject("SpawnHolder");
            holder.SetActive(false);
            var p = Object.Instantiate(prefab, feet, Quaternion.Euler(0f, yaw, 0f), holder.transform);
            foreach (var i in p.GetComponentsInChildren<PInput>(true)) i.enabled = false;
            p.transform.SetParent(null, true);
            Object.Destroy(holder);
            return p.GetComponent<PlayerMotor>();
        }

        static IEnumerator Hold(PlayerMotor m, float amount, bool run, float seconds, System.Action each = null)
        {
            int n = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < n; i++)
            {
                if (amount > 0f) m.SetMoveInput(m.transform.forward, amount, run);
                yield return null;
                each?.Invoke();
            }
        }

        /// 지금 가장 많이 섞인 클립 이름과 무게
        static (string name, float w) Top(Animator a)
        {
            var info = a.GetCurrentAnimatorClipInfo(0);
            if (info.Length == 0) return ("없음", 0f);
            var best = info.OrderByDescending(c => c.weight).First();
            return (best.clip.name, best.weight);
        }

        [UnityTest]
        public IEnumerator T24_Anim_States()
        {
            Lab.Floor(0f, 200f);
            var m = Spawn(new Vector3(0f, 0f, -80f), 0f);
            var loco = m.GetComponentInChildren<LocoAnim>();
            Assert.IsNotNull(loco, "프리팹에 LocoAnim");
            var a = loco.Animator;
            Assert.IsNotNull(a.runtimeAnimatorController, "애니메이터 컨트롤러");
            Assert.IsTrue(a.isHuman, "Humanoid 아바타");
            Assert.IsFalse(a.applyRootMotion, "루트 모션 끔(이동은 PlayerMotor)");

            var log = new List<string>();
            void Check(string when, string clip, LocoAnim.Gait gait)
            {
                var (name, w) = Top(a);
                var st = a.GetCurrentAnimatorStateInfo(0);
                log.Add($"{when}: 속도 {m.PlanarSpeed:F2} · Blend {loco.Blend:F2} · Rate {loco.Rate:F2} · 클립 {name} {w:F2} · 상태 Move {st.IsName("Move")}");
                Assert.IsTrue(st.IsName("Move"), $"{when}: 애니메이터 상태 Move");
                Assert.AreEqual(clip, name, $"{when}: 가장 많이 섞인 클립");
                Assert.That(w, Is.GreaterThanOrEqualTo(0.9f), $"{when}: {clip} 무게 0.9 이상");
                Assert.AreEqual(gait, loco.Dominant, $"{when}: LocoAnim.Dominant");
            }

            yield return Lab.Seconds(1.0f);
            Check("정지", "Idle", LocoAnim.Gait.Idle);
            yield return Hold(m, 1f, false, 2.0f);
            Check("걷기 1.6", "Walk", LocoAnim.Gait.Walk);
            Assert.That(loco.Rate, Is.EqualTo(1f).Within(0.03f), "걷기 1.6: 재생 속도 ×1(블렌드 트리의 걷기 배율 그대로)");
            yield return Hold(m, 1f, true, 2.0f);
            Check("달리기 4.5", "Run", LocoAnim.Gait.Run);
            yield return Hold(m, 0f, false, 1.5f);
            Check("다시 정지", "Idle", LocoAnim.Gait.Idle);
            // 패드를 살짝 기울인 느린 걷기(0.5~1.6 m/s): 걷기 클립을 느리게(대기와 섞지 않음)
            float amt = (0.8f - m.T.minWalkSpeed) / (m.T.walkSpeed - m.T.minWalkSpeed) * m.T.stickFull;
            yield return Hold(m, amt, false, 1.5f);
            Check("느린 걷기 0.8", "Walk", LocoAnim.Gait.Walk);
            Assert.That(loco.Rate, Is.EqualTo(0.8f / m.T.walkSpeed).Within(0.05f), "느린 걷기: 재생 속도 = 속도 ÷ 1.6");
            Debug.Log("[M1Test] T24 애니메이터 상태\n  " + string.Join("\n  ", log));
        }

        [UnityTest]
        public IEnumerator T25_Anim_FootSlip()
        {
            Lab.Floor(0f, 300f);
            var m = Spawn(new Vector3(0f, 0f, -120f), 0f);
            var a = m.GetComponentInChildren<LocoAnim>().Animator;
            var lf = a.GetBoneTransform(HumanBodyBones.LeftFoot);
            var rf = a.GetBoneTransform(HumanBodyBones.RightFoot);
            yield return Lab.Seconds(0.5f);
            var idle = Measure(lf, rf, 0f);
            yield return Hold(m, 0f, false, 1.0f, () => idle.Add());
            var walk = Measure(lf, rf, 1.6f);
            yield return Hold(m, 1f, false, 1.0f);
            yield return Hold(m, 1f, false, 3.0f, () => walk.Add());
            yield return Hold(m, 0f, false, 1.0f);
            var run = Measure(lf, rf, 4.5f);
            yield return Hold(m, 1f, true, 1.5f);
            yield return Hold(m, 1f, true, 3.0f, () => run.Add());
            walk.Done(); run.Done(); idle.Done();
            Debug.Log($"[M1Test] T25 발 미끄러짐(디딤발 발목의 수평 속도, 디딤 = 발목 높이가 최저 + {Slip.Stance * 100f:F1}cm 안) — " +
                      $"대기: 발목 최저 {idle.MinY:F3}m | 걷기 1.6: 중앙값 {walk.Median:F3} m/s(진행 방향 성분 {walk.Signed:+0.000;-0.000}) · 90% {walk.P90:F3} · 디딤 {walk.Share * 100f:F0}% · 발목 최저 {walk.MinY:F3}m | " +
                      $"달리기 4.5: 중앙값 {run.Median:F3} m/s(진행 방향 성분 {run.Signed:+0.000;-0.000}) · 90% {run.P90:F3} · 디딤 {run.Share * 100f:F0}% · 발목 최저 {run.MinY:F3}m");
            // 재생 속도가 맞으면 디딤발의 진행 방향 성분(계통 미끄러짐)이 0 근처. 절대값 중앙값에는 클립 자체의 발 구름·옆 흔들림이 섞여 있어 넉넉히(재생 배율이 크게 틀리면 걸림)
            Assert.That(Mathf.Abs(walk.Signed), Is.LessThanOrEqualTo(0.05f * 1.6f), "걷기: 디딤발이 진행 방향으로 밀리는 속도 ≤ 걷기 속도의 5%");
            Assert.That(Mathf.Abs(run.Signed), Is.LessThanOrEqualTo(0.05f * 4.5f), "달리기: 디딤발이 진행 방향으로 밀리는 속도 ≤ 달리기 속도의 5%");
            Assert.That(walk.Median, Is.LessThanOrEqualTo(0.15f * 1.6f), "걷기: 디딤발 수평 속도 중앙값 ≤ 걷기 속도의 15%");
            Assert.That(run.Median, Is.LessThanOrEqualTo(0.15f * 4.5f), "달리기: 디딤발 수평 속도 중앙값 ≤ 달리기 속도의 15%");
            foreach (var (n, s) in new[] { ("대기", idle), ("걷기", walk), ("달리기", run) })
                Assert.That(s.MinY, Is.InRange(0.09f, 0.17f), $"{n}: 발목 최저 높이(휴지 자세 0.13m) — 발이 땅에 묻히거나 뜨지 않음");
        }

        sealed class Slip
        {
            readonly Transform l, r;
            readonly float speed;
            readonly List<Vector3> L = new List<Vector3>(), R = new List<Vector3>();
            public float Median, P90, Share, MinY, Signed;
            /// 디딤 판정: 발목 높이가 최저 + 1.5cm 안(발바닥이 바닥에 붙은 구간 — 뒤꿈치가 들리며 발목이 앞으로 굴러가는 구간은 뺌)
            public const float Stance = 0.015f;
            public Slip(Transform l, Transform r, float speed) { this.l = l; this.r = r; this.speed = speed; }
            public void Add() { L.Add(l.position); R.Add(r.position); }
            public void Done()
            {
                MinY = Mathf.Min(L.Min(p => p.y), R.Min(p => p.y));
                if (speed <= 0f) return;
                var v = new List<float>();
                var sv = new List<float>();
                int stance = 0;
                foreach (var P in new[] { L, R })
                {
                    float min = P.Min(p => p.y);
                    for (int i = 1; i < P.Count; i++)
                    {
                        if (P[i].y > min + Stance || P[i - 1].y > min + Stance) continue;
                        stance++;
                        var d = P[i] - P[i - 1];
                        v.Add(new Vector2(d.x, d.z).magnitude / Dt);
                        sv.Add(d.z / Dt);   // + = 디딤발이 앞(진행 방향)으로 밀림 = 재생이 느림
                    }
                }
                v.Sort();
                Median = v.Count > 0 ? v[v.Count / 2] : 99f;
                sv.Sort();
                Signed = sv.Count > 0 ? sv[sv.Count / 2] : 0f;
                P90 = v.Count > 0 ? v[Mathf.Min(v.Count - 1, (int)(v.Count * 0.9f))] : 99f;
                Share = stance / (2f * Mathf.Max(1, L.Count - 1));
            }
        }

        static Slip Measure(Transform l, Transform r, float speed) => new Slip(l, r, speed);
    }
}
