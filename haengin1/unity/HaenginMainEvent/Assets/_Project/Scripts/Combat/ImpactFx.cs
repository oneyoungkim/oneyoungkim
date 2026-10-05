// 행인1의 메인이벤트 — 타격감 한 번에 (docs/08_M2_전투_설계.md 5-1·5-2·5-3·5-4)
// 맞을 때마다: 히트스톱(TimeFx) · 트라우마 흔들림(Shake → 카메라의 TraumaShake) · 줌 펀치(중 이상) · 피격자 번쩍 · 히트 셰이크 · 피격 젖힘 · 슬로(기술에 있으면).
// 넉백은 Fighter.Receive 가 한다. 막힌 타격은 약 단계 × 0.5 의 히트스톱·흔들림만(젖힘 없음).
// 이펙트·의성어·화면 번쩍·쇼크 컷·효과음은 11단계에서 여기에 더한다.
using System;
using UnityEngine;

namespace Haengin
{
    public static class ImpactFx
    {
        /// 마지막 피격자(히트 셰이크 대상)
        public static Fighter LastVictim { get; private set; }
        public static HitEvent Last { get; private set; }
        public static int Count { get; private set; }
        /// 타격감이 나갈 때마다(테스트·녹화)
        public static event Action<HitEvent, float> Fired;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { LastVictim = null; Last = default; Count = 0; Fired = null; }

        public static void OnHit(HitEvent e, CombatTuning t)
        {
            if (e.Outcome == HitOutcome.Read || e.Victim == null) return;
            bool blocked = e.Outcome == HitOutcome.Blocked;
            var p = e.Move != null && e.Move.StopPower != Power.None ? e.Move.StopPower : e.Power;
            if (e.Outcome == HitOutcome.Crushed) p = Power.Mid;
            if (p == Power.None) p = Power.Light;
            float stop = blocked ? t.Stop(Power.Light) * t.BlockFx : t.Stop(p);
            float trauma = blocked ? t.Tr(Power.Light) * t.BlockFx : t.Tr(p);
            Shake.Decay = t.TraumaDecay;
            Shake.PunchDecay = t.PunchDecay;
            Shake.Reduced = t.Reduced;
            Shake.PunchAmount = t.PunchAmount;

            TimeFx.HitStop(stop);
            Shake.Add(trauma);
            if (!blocked && (int)p >= 2) Shake.Kick(1f);

            var r = e.Victim.React;
            if (r != null)
            {
                if (!blocked && e.Move != null)
                {
                    float w = t.FlinchW[Mathf.Clamp((int)p, 0, 4)];
                    if (e.Outcome == HitOutcome.Braced || e.Outcome == HitOutcome.Armored) w *= 0.5f;
                    float side = 1f;
                    if (e.Attacker != null)
                        side = Vector3.Dot(e.Victim.Forward, Vector3.Cross(Vector3.up, e.Attacker.Forward)) >= 0f ? 1f : -1f;
                    r.Flinch(e.Move.Flinch, w, side);
                }
                r.Flash();
                if (LastVictim != null && LastVictim != e.Victim && LastVictim.React != null) LastVictim.React.Shaking = false;
                r.Shaking = true;
            }
            if (!blocked && e.Move != null && e.Move.Slow > 0f && !t.Reduced) TimeFx.Slow(e.Move.Slow, e.Move.SlowScale);

            LastVictim = e.Victim;
            Last = e;
            Count++;
            Fired?.Invoke(e, stop);
        }
    }
}
