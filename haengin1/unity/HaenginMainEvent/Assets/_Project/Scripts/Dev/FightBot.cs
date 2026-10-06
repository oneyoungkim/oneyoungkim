// 행인1의 메인이벤트 — 전투 봇(테스트·녹화·빌드 스모크 공용, docs/08_M2_전투_설계.md 10-5 C18·C22, 11장 16단계 'InputScript 로 녹화')
// PlayerCombat 에 사람 손과 같은 길(Press · SetStickWorld · SetGuard)로 넣는다(InputScript 와 같은 방식).
//   Mash = true  : C22 '연타 봇' — 가장 가까운 적 쪽으로 걸으며 □ 를 계속(회피·막기·잡기·락온 안 씀)
//   Mash = false : 녹화·스모크·'회피·막기' 봇 — 가까운 적 락온, 앞에서 오는 막을 수 있는 공격은 막기, 막을 수 없는 것(막기 불가 · 막기 각 밖 = 옆·뒤)은
//                  판정 0.6초 전부터 손을 멈추고 0.12초 전에 옆 회피, 붙으면 □ 4번에 △ 1번, 기세 MAX + 조건이면 △(기세 액션)
using UnityEngine;

namespace Haengin
{
    [DefaultExecutionOrder(-47)]
    public sealed class FightBot : MonoBehaviour
    {
        public PlayerCombat Pc;
        public bool Mash;
        public int Presses, GuardFrames, Dodges, HeatActions;
        [Tooltip("막을 수 없는 공격: 판정 이만큼 전(초)에 회피 · 이만큼 전부터 공격 멈춤")] public float DodgeLead = 0.12f, HoldLead = 0.6f;
        int frame;

        public static FightBot On(PlayerCombat pc, bool mash)
        {
            var b = pc.gameObject.GetComponent<FightBot>() ?? pc.gameObject.AddComponent<FightBot>();
            b.Pc = pc; b.Mash = mash;
            b.enabled = true;
            return b;
        }

        void OnDisable() { if (Pc != null) { Pc.SetStickWorld(Vector3.zero, 0f); Pc.SetGuard(false); } }

        void Update()
        {
            if (Pc == null || !Pc.Active || Pc.Me == null || GameState.Paused || GameState.InputLocked || GameState.Modal) return;
            if (Pc.InHeatAction) return;
            frame++;
            var me = Pc.Me;
            Fighter tgt = null;
            float best = float.MaxValue;
            foreach (var f in Fighter.All)
            {
                if (f == null || f == me || f.Team == me.Team || !f.Targetable) continue;
                float d = HitResolver.Flat(f.Position - me.Position).sqrMagnitude;
                if (d < best) { best = d; tgt = f; }
            }
            if (tgt == null) { Pc.SetStickWorld(Vector3.zero, 0f); Pc.SetGuard(false); return; }
            HitResolver.Measure(me.Position, me.Yaw, tgt.Position, tgt.Radius, out _, out float surf, out _);
            var to = HitResolver.Flat(tgt.Position - me.Position).normalized;
            bool free = me.State == Fighter.Phase.Free || me.State == Fighter.Phase.Act;

            if (Mash)
            {
                if (surf > 1.0f) Pc.SetStickWorld(to, 1f);
                else Pc.SetStickWorld(Vector3.zero, 0f);
                if (free && frame % 6 == 0) { Pc.Press(Btn.Light); Presses++; }
                return;
            }

            if (Pc.Lock != null && Pc.Lock.Target != tgt) Pc.Lock.Set(tgt);
            // 위협: 판정 전이거나 판정 중인 적 공격(4m 안 — 돌진·옆에서 들어오는 것 포함), 판정이 가장 가까운 것
            Fighter threat = null;
            float tHit = 99f;
            foreach (var f in Fighter.All)
            {
                if (f == null || f == me || f.Team == me.Team || f.Run == null) continue;
                if (f.Run.T >= f.Run.Move.ActiveEnd || HitResolver.Flat(f.Position - me.Position).magnitude > 4f + f.Run.Move.ChargeDist) continue;
                float th = (float)(f.Run.Move.ActiveStart - f.Run.T);
                if (th < tHit) { tHit = th; threat = f; }
            }
            if (threat != null)
            {
                bool guardable = !threat.Run.Move.Unblockable && HitResolver.InGuardArc(me, threat.Position, Pc.T.GuardAngle);
                if (!guardable)
                {
                    Pc.SetGuard(false);
                    if (tHit <= DodgeLead && me.State == Fighter.Phase.Free)
                    {
                        Pc.SetStickWorld(Vector3.Cross(Vector3.up, HitResolver.Flat(threat.Position - me.Position).normalized), 1f);
                        Pc.Press(Btn.Dodge); Dodges++;
                        return;
                    }
                    if (tHit <= HoldLead) { Pc.SetStickWorld(Vector3.zero, 0f); return; }     // 손을 멈추고 회피 기다림
                }
                else if (me.Run == null && tHit <= 0.5f)
                {
                    Pc.SetGuard(true); GuardFrames++;
                    Pc.SetStickWorld(Vector3.zero, 0f);
                    return;
                }
            }
            Pc.SetGuard(false);
            if (Pc.Heat.Full && Pc.HeatAct != null && Pc.HeatAct.Available(out _, out _)) { Pc.Press(Btn.Heavy); HeatActions++; return; }
            if (surf > 1.0f) Pc.SetStickWorld(to, 1f);
            else
            {
                Pc.SetStickWorld(Vector3.zero, 0f);
                if (free && frame % 11 == 0) { Pc.Press((frame / 11) % 5 == 4 ? Btn.Heavy : Btn.Light); Presses++; }
            }
        }
    }
}
