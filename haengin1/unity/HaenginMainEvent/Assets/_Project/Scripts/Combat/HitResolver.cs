// 행인1의 메인이벤트 — 판정 계산 (docs/08_M2_전투_설계.md 2-3·3-2·10-3) — 순수 함수(테스트하기 쉽게)
// 판정 모양 = 공격자 기준 부채꼴: 사거리(공격자 중심 → 피격자 캡슐 표면, 수평), 반각, 높이 차 ±HeightTol.
// 시간 = 구간 방식: 이번 프레임 [t0, t1) 이 판정 구간 [a0, a1) 과 겹치면 검사(30·20fps 로 떨어져도 판정 프레임을 건너뛰지 않음).
using System.Collections.Generic;
using UnityEngine;

namespace Haengin
{
    public static class HitResolver
    {
        /// 부동소수 누적 오차 여유(초). 1/60 을 일곱 번 더한 값이 7/60 을 살짝 넘어 한 프레임 일찍 맞는 것을 막는다
        public const double Eps = 1e-5;

        /// 구간 [t0, t1) 과 [a0, a1) 이 겹치는가
        public static bool Overlaps(double t0, double t1, double a0, double a1) => t1 > a0 + Eps && t0 < a1 - Eps;

        /// 겹친 길이(초)
        public static double Overlap(double t0, double t1, double a0, double a1) => System.Math.Max(0.0, System.Math.Min(t1, a1) - System.Math.Max(t0, a0));

        public static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        public static float Yaw(Vector3 dir) => Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;

        public static Vector3 YawDir(float yaw) => new Vector3(Mathf.Sin(yaw * Mathf.Deg2Rad), 0f, Mathf.Cos(yaw * Mathf.Deg2Rad));

        /// 수평 거리(중심) · 표면 거리 · 정면과의 각도(°)
        public static void Measure(Vector3 aPos, float aYaw, Vector3 vPos, float vRadius, out float center, out float surface, out float angle)
        {
            var d = Flat(vPos - aPos);
            center = d.magnitude;
            surface = center - vRadius;
            angle = center < 1e-4f ? 0f : Vector3.Angle(YawDir(aYaw), d);
        }

        /// 부채꼴 안인가(08 3-2). 몸이 맞붙을 만큼 가까우면(중심 거리 < 두 반지름 + 0.05) 각도는 90° 까지 봐준다
        public static bool InFan(Vector3 aPos, float aYaw, float aRadius, Vector3 vPos, float vRadius, float range, float halfAngle, float heightTol)
        {
            if (Mathf.Abs(vPos.y - aPos.y) > heightTol) return false;
            Measure(aPos, aYaw, vPos, vRadius, out float c, out float s, out float ang);
            if (s > range) return false;
            float half = c < aRadius + vRadius + 0.05f ? Mathf.Max(halfAngle, 90f) : halfAngle;
            return ang <= half;
        }

        public static bool InFan(Fighter a, Fighter v, MoveDef m) =>
            InFan(a.Position, a.Yaw, a.Radius, v.Position, v.Radius, m.Range, m.HalfAngle, m.HeightTol);

        /// 막는 쪽 정면 ±angle 안에서 온 공격인가
        public static bool InGuardArc(Fighter def, Vector3 from, float angle)
        {
            var d = Flat(from - def.Position);
            if (d.sqrMagnitude < 1e-6f) return true;
            return Vector3.Angle(YawDir(def.Yaw), d) <= angle;
        }

        /// 막혔을 때 피해(올림)
        public static int BlockedDamage(int dmg, float ratio) => dmg <= 0 ? 0 : Mathf.CeilToInt(dmg * ratio - 1e-4f);

        /// 가드 게이지 깎임
        public static float GuardCost(MoveDef m, CombatTuning t) => m.GuardDmg >= 0f ? m.GuardDmg : t.GuardCost[Mathf.Clamp((int)m.Power, 0, 4)];

        /// 소프트 조준 점수(낮을수록 먼저): 거리 + 각도 × 0.02, 공격 예고 중 −0.5
        public static float AimScore(float dist, float angle, bool telegraph, CombatTuning t) =>
            dist + angle * t.AimAngleW - (telegraph ? t.AimTelegraphBonus : 0f);

        /// 소프트 조준 대상(08 2-3): 반경 3.5m, 기준 방향 ±75° 안의 서 있는 적 중 점수가 가장 낮은 것
        public static Fighter SoftAim(Fighter me, Vector3 refDir, CombatTuning t, IList<Fighter> all)
        {
            Fighter best = null;
            float bestScore = float.MaxValue;
            float refYaw = Yaw(refDir);
            foreach (var f in all)
            {
                if (f == null || f == me || f.Team == me.Team || !f.Targetable) continue;
                Measure(me.Position, refYaw, f.Position, f.Radius, out float c, out _, out float ang);
                if (c > t.AimRadius || ang > t.AimAngle) continue;
                float sc = AimScore(c, ang, f.Telegraphing, t);
                if (sc < bestScore) { bestScore = sc; best = f; }
            }
            return best;
        }

        /// 자석(08 2-3): 표면 거리가 사거리보다 멀고 사거리 + 0.8 안이면 (거리 − (사거리 − 0.15)) 만큼, 최대 move.Magnet
        public static float Reach(MoveDef m, CombatTuning t) => m.MagnetReach >= 0f ? m.MagnetReach : t.MagnetReach;

        public static float Magnet(float surface, MoveDef m, CombatTuning t)
        {
            if (m.Magnet <= 0f || surface <= m.Range || surface > m.Range + Reach(m, t)) return 0f;
            return Mathf.Min(m.Magnet, surface - (m.Range - t.MagnetMargin));
        }

        /// 부채꼴 안 대상(가까운 순). skip 에 든 것·쓰러진 것은 뺀다. includeInvuln = 무적도 넣기(읽었다 검사용)
        public static void Targets(Fighter a, MoveDef m, IList<Fighter> all, ICollection<Fighter> skip, bool includeInvuln, List<Fighter> into)
        {
            into.Clear();
            foreach (var f in all)
            {
                if (f == null || f == a || f.Team == a.Team || !f.Targetable) continue;
                if (skip != null && skip.Contains(f)) continue;
                if (!includeInvuln && f.Invulnerable) continue;
                if (!InFan(a, f, m)) continue;
                into.Add(f);
            }
            var p = a.Position;
            into.Sort((x, y) => Flat(x.Position - p).sqrMagnitude.CompareTo(Flat(y.Position - p).sqrMagnitude));
        }
    }
}
