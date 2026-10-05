// 행인1의 메인이벤트 — 손 모양(셰이프 키) (docs/08_M2_전투_설계.md 9-3)
// 모델의 셰이프 키 Fist_L · Fist_R · Grip_L · Grip_R(tools/hand_keys.py 가 만듦)를 상태에 따라 0.08초 보간(게임 시간).
//   탐색 0 / 전투 대기·이동·공격·막기 Fist 100 / 잡기 중 Grip_L 100 · Fist_R 100 / 피격·다운 Fist 40 / 승리 Fist 100
using UnityEngine;

namespace Haengin
{
    [DefaultExecutionOrder(16), DisallowMultipleComponent]
    public sealed class HandShape : MonoBehaviour
    {
        public Fighter Me;
        public PlayerCombat Player;
        public EnemyBrain Brain;
        [Tooltip("보간 시간(초)")] public float Blend = 0.08f;
        [Tooltip("승리·연출에서 강제(음수 = 상태대로)")] public float ForceFist = -1f;

        SkinnedMeshRenderer smr;
        int iFL = -1, iFR = -1, iGL = -1, iGR = -1;
        float fl, fr, gl, gr;

        public bool Ready => smr != null && iFL >= 0 && iFR >= 0 && iGL >= 0 && iGR >= 0;
        public float FistL => fl;
        public float FistR => fr;
        public float GripL => gl;
        public float GripR => gr;

        void Awake() => Find();
        void Start() => Find();     // 적 모델은 만든 뒤 Fighter 밑으로 옮겨지므로 한 번 더

        public void Find()
        {
            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var m = r.sharedMesh;
                if (m == null) continue;
                int a = m.GetBlendShapeIndex("Fist_L"), b = m.GetBlendShapeIndex("Fist_R"), c = m.GetBlendShapeIndex("Grip_L"), d = m.GetBlendShapeIndex("Grip_R");
                if (a >= 0 && b >= 0) { smr = r; iFL = a; iFR = b; iGL = c; iGR = d; break; }
            }
            if (Me == null) Me = GetComponentInParent<Fighter>();
            if (Player == null) Player = GetComponentInParent<PlayerCombat>();
            if (Brain == null) Brain = GetComponentInParent<EnemyBrain>();
        }

        /// 지금 상태의 목표 무게(0~100)
        public void Targets(out float tFL, out float tFR, out float tGL, out float tGR)
        {
            tFL = tFR = tGL = tGR = 0f;
            bool combat = Player != null ? Player.Active : Brain != null && Brain.State != EnemyBrain.S.Idle;
            if (ForceFist >= 0f) { tFL = tFR = ForceFist; return; }
            if (!combat || Me == null) return;
            var s = Me.State;
            if (s == Fighter.Phase.Stagger || s == Fighter.Phase.Fall || s == Fighter.Phase.Lie || s == Fighter.Phase.GetUp || s == Fighter.Phase.Grabbed || s == Fighter.Phase.Out)
            {
                tFL = tFR = 40f;
                return;
            }
            bool holding = Player != null && Player.Held != null;
            bool grabbing = Me.Run != null && Me.Run.Move.ClipId == 259 && Me.Run.Move.State == "Grab";
            if (holding || grabbing) { tGL = 100f; tFR = 100f; return; }
            tFL = tFR = 100f;
        }

        // LateUpdate: 애니메이터가 셰이프 키 곡선(모델 FBX 의 대기 클립 등)을 0 으로 덮어쓴 뒤에 넣는다
        void LateUpdate()
        {
            if (!Ready) return;
            float dt = TimeFx.Dt;
            Targets(out float a, out float b, out float c, out float d);
            if (dt > 0f)
            {
                float step = 100f * dt / Mathf.Max(1e-3f, Blend);
                fl = Mathf.MoveTowards(fl, a, step);
                fr = Mathf.MoveTowards(fr, b, step);
                gl = Mathf.MoveTowards(gl, c, step);
                gr = Mathf.MoveTowards(gr, d, step);
            }
            smr.SetBlendShapeWeight(iFL, fl);
            smr.SetBlendShapeWeight(iFR, fr);
            if (iGL >= 0) smr.SetBlendShapeWeight(iGL, gl);
            if (iGR >= 0) smr.SetBlendShapeWeight(iGR, gr);
        }

        /// 지금 렌더러에 들어간 무게(테스트)
        public float Weight(string name)
        {
            if (smr == null) return -1f;
            int i = smr.sharedMesh.GetBlendShapeIndex(name);
            return i >= 0 ? smr.GetBlendShapeWeight(i) : -1f;
        }
    }
}
