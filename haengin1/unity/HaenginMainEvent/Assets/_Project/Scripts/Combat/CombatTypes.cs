// 행인1의 메인이벤트 — 전투 공통 형식 (docs/08_M2_전투_설계.md 3장·5장)
using UnityEngine;

namespace Haengin
{
    /// 위력 단계(08 3-1): 약 1 · 중 2 · 강 3 · 기세 4. 히트스톱·흔들림·젖힘·의성어 크기를 정한다
    public enum Power { None = 0, Light = 1, Mid = 2, Heavy = 3, Heat = 4 }

    /// 전투 버튼(입력 버퍼에 들어가는 것)
    public enum Btn { Light = 0, Heavy = 1, Grab = 2, Dodge = 3 }

    /// 피격 젖힘 종류(08 5-6)
    public enum FlinchKind { Head, Hook, Upper, Body }

    /// 타격 결과
    public enum HitOutcome { Hit, Blocked, Crushed, GuardBroken, Braced, Armored, Read }

    /// 한 번의 타격(맞힌 쪽·맞은 쪽 모두 이 기록을 받는다)
    public struct HitEvent
    {
        public Fighter Attacker, Victim;
        public MoveDef Move;
        public HitOutcome Outcome;
        public int Damage;
        public Power Power;
        public Vector3 Point, Dir;
        public int Frame;
        public double GameTime;
        public bool Landed => Outcome == HitOutcome.Hit || Outcome == HitOutcome.Armored || Outcome == HitOutcome.GuardBroken || Outcome == HitOutcome.Braced;
        public override string ToString() => $"{(Attacker != null ? Attacker.Label : "?")}→{(Victim != null ? Victim.Label : "?")} {(Move != null ? Move.Label : "?")} {Outcome} {Damage} p{(int)Power} f{Frame}";
    }
}
