// 행인1의 메인이벤트 — M2 10단계 기세 액션 테스트 C15 (docs/08_M2_전투_설계.md 3-8·10-5, 11장 10단계)
// 캡슐 시우(전투 카메라 + CM_Heat) + 캡슐 적. 기세 100 에서 △:
//   ① 대상 등 뒤 1.2m 에 HeatSurface 벽 → 「벽 러시」 피해 47, 다른 적 멈춤·무적, 기세 0, CM_Heat 우선순위 30
//   ② 대상 등 뒤 0.8m 에 구경꾼 줄 → 「구경꾼 되받기」 피해 45
//   ③ 둘 다 없음 → 평소 △(앞차기), 기세 그대로 100
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.TestTools;

namespace Haengin.Tests
{
    public sealed class HeatTests
    {
        const float Dt = Lab.Dt;
        CombatTuning tune;
        readonly List<HitEvent> hits = new List<HitEvent>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.captureDeltaTime = Dt;
            TimeFx.Reset();
            hits.Clear();
            Fighter.AnyHit += Record;
            tune = ScriptableObject.CreateInstance<CombatTuning>();
            yield return Lab.FreshScene();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Fighter.AnyHit -= Record;
            TimeFx.Reset();
            GameState.SetPaused(false);
            Time.captureDeltaTime = 0f;
            yield return null;
        }

        void Record(HitEvent e) => hits.Add(e);

        (RigFactory.Rig r, PlayerCombat pc) Siwoo(Vector3 feet, float yaw)
        {
            var r = Lab.Rig(feet, yaw);
            var pc = CombatFactory.AddPlayer(r.Player, tune, MoveSet.CreateDefault());
            var cam = CombatFactory.BuildCombatCam(r, pc, r.CamRig.T, tune, null);
            CombatFactory.AddMode(r, pc, cam, false).Begin(0f);
            return (r, pc);
        }

        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        struct Run { public HeatKind Kind; public int Dealt, HitsOnTarget; public float HeatAfter, OtherMoved; public bool OtherShielded, OtherFrozen, CamUp; public string Log; }

        IEnumerator Play(System.Action<PlayerCombat, Fighter> scene, Run[] outRun)
        {
            Lab.Floor(0f, 60f);
            var (r, pc) = Siwoo(Vector3.zero, 0f);
            pc.Me.MaxHp = pc.Me.Hp = 9999;
            var dir = CombatFactory.Director(pc.Me, tune);
            var tgt = CombatFactory.Enemy(EnemyLib.Kkanjok(), new Vector3(0f, 0f, 1.2f), 180f, pc.Me, dir, 0, tune);
            tgt.Me.MaxHp = tgt.Me.Hp = 999;
            tgt.HoldPosition = true; tgt.NoAttack = true;
            var other = CombatFactory.Enemy(EnemyLib.Seokdal(), new Vector3(4f, 0f, 3f), 225f, pc.Me, dir, 1, tune);
            scene(pc, tgt.Me);
            yield return Frames(5);
            tgt.Activate(); other.Activate();
            yield return Frames(30);
            // 대상 자리 다시(접근으로 움직였을 수 있음) · 다른 적은 4m 떨어져 간보기
            tgt.Me.Body.Place(new Vector3(0f, 0f, 1.2f), 180f);
            ((PlayerBody)pc.Me.Body).Place(Vector3.zero, 0f);
            yield return Frames(2);
            pc.Heat.Set(100f);
            hits.Clear();
            var heatCam = Object.FindAnyObjectByType<HeatCam>();
            var o0 = other.Me.Position;
            var res = new Run { Kind = HeatKind.None };
            pc.Press(Btn.Heavy);
            float moved = 0f;
            bool shielded = false, frozen = false, camUp = false;
            for (int i = 0; i < 600; i++)
            {
                yield return null;
                var h = pc.HeatAct;
                if (h != null && h.Playing)
                {
                    res.Kind = h.Kind;
                    moved = Mathf.Max(moved, HitResolver.Flat(other.Me.Position - o0).magnitude);
                    shielded |= other.Me.Invulnerable;
                    frozen |= other.Frozen;
                    if (heatCam != null && heatCam.Cam != null && heatCam.Cam.Priority >= 30) camUp = true;
                }
                else if (i > 20) break;
            }
            yield return Frames(40);
            res.Dealt = pc.HeatAct != null ? pc.HeatAct.Dealt : 0;
            res.HitsOnTarget = hits.Where(e => e.Attacker == pc.Me && e.Victim == tgt.Me && e.Landed).Sum(e => e.Damage);
            res.HeatAfter = pc.Heat.Value;
            res.OtherMoved = moved;
            res.OtherShielded = shielded;
            res.OtherFrozen = frozen;
            res.CamUp = camUp;
            res.Log = string.Join(", ", hits.Where(e => e.Attacker == pc.Me).Select(e => $"{e.Move?.Label} {e.Damage}"));
            outRun[0] = res;
        }

        [UnityTest]
        public IEnumerator C15_Heat_Actions()
        {
            var log = new List<string>();
            var o = new Run[1];

            // ① 벽: 대상(z 1.2, 반경 0.3) 등 뒤 1.2m → 벽 면 z 2.7
            yield return Play((pc, t) =>
            {
                var w = Lab.Box("Wall", new Vector3(0f, 1.25f, 2.7f + 0.25f), new Vector3(6f, 2.5f, 0.5f), Quaternion.identity, Layers.Wall);
                w.AddComponent<HeatSurface>();
            }, o);
            var a = o[0];
            log.Add($"벽 1.2m: {a.Kind} 피해 {a.Dealt}(맞힘 합 {a.HitsOnTarget}: {a.Log}) · 기세 {a.HeatAfter:F0} · 다른 적 움직임 {a.OtherMoved:F2}m 무적 {a.OtherShielded} 멈춤 {a.OtherFrozen} · CM_Heat {a.CamUp}");
            Assert.AreEqual(HeatKind.WallRush, a.Kind, "벽 러시");
            Assert.AreEqual(47, a.Dealt, "벽 러시 피해 47");
            Assert.AreEqual(47, a.HitsOnTarget, "맞힘 기록 합 47");
            Assert.That(a.HeatAfter, Is.LessThan(1f), "기세 100 → 0");
            Assert.IsTrue(a.OtherShielded && a.OtherFrozen, "다른 적 멈춤·무적");
            Assert.That(a.OtherMoved, Is.LessThan(0.15f), "다른 적이 움직이지 않음");
            Assert.IsTrue(a.CamUp, "CM_Heat 우선순위 30");

            // ② 구경꾼: 대상 등 뒤 0.8m 에 구경꾼 줄(원 반경 6, 중심을 줄이 z 2.3 에 오게)
            yield return Lab.FreshScene();
            yield return Play((pc, t) =>
            {
                var ring = new GameObject("CrowdRing").AddComponent<CrowdRing>();
                ring.Radius = 6f;
                ring.transform.position = new Vector3(0f, 0f, 1.2f + 0.3f + 0.8f - 6f);
                for (int i = 0; i < 6; i++)
                {
                    var c = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    Object.DestroyImmediate(c.GetComponent<Collider>());
                    c.layer = Layers.Crowd;
                    c.transform.SetParent(ring.transform, false);
                    float ang = -25f + 10f * i;
                    c.transform.localPosition = HitResolver.YawDir(ang) * 6.6f + Vector3.up * 0.85f;
                }
            }, o);
            var b = o[0];
            log.Add($"구경꾼 0.8m: {b.Kind} 피해 {b.Dealt}(맞힘 합 {b.HitsOnTarget}: {b.Log}) · 기세 {b.HeatAfter:F0} · 다른 적 무적 {b.OtherShielded}");
            Assert.AreEqual(HeatKind.CrowdReturn, b.Kind, "구경꾼 되받기");
            Assert.AreEqual(45, b.Dealt, "되받기 피해 45");
            Assert.That(b.HeatAfter, Is.LessThan(1f), "기세 0");

            // ③ 둘 다 없음 → 평소 △, 기세 100 그대로
            yield return Lab.FreshScene();
            yield return Play((pc, t) => { }, o);
            var c3 = o[0];
            log.Add($"없음: {c3.Kind} · 맞힘 {c3.Log} · 기세 {c3.HeatAfter:F0}");
            Assert.AreEqual(HeatKind.None, c3.Kind, "기세 액션 안 나감");
            StringAssert.Contains("앞차기", c3.Log, "평소 △ = 앞차기");
            Assert.That(c3.HeatAfter, Is.GreaterThanOrEqualTo(99f), "기세 그대로 100(앞차기 기세는 MAX 에서 더 안 오름)");
            Debug.Log("[M2Test] C15 기세 액션: " + string.Join(" | ", log));
        }
    }
}
