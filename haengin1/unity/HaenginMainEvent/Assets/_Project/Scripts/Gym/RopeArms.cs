// 행인1의 메인이벤트 — 줄넘기 팔 보정(QA 2차 A8, 2026-10-26): 전용 클립 Jump_Rope(46)는 줄 없이 뛰는 동작이라 두 손이 어깨너비 바깥으로 벌어진 채였다(D01 녹화).
// 애니메이터가 'JumpRope' 상태일 때만 IK 로 손목을 허리 옆(골반 ±0.24m · 앞 0.14m · 골반 높이)으로 당기고, 줄 각(Phase)에 맞춰 손목만 작게(반지름 5cm) 돌린다.
// 팔꿈치 힌트는 옆구리 뒤 위(팔꿈치가 몸에 붙게). 애니메이터 기본 층 IK 패스가 켜져 있어야 함(Siwoo.controller — BikeSetup 이 켬).
using UnityEngine;

namespace Haengin
{
    [DisallowMultipleComponent]
    public sealed class RopeArms : MonoBehaviour
    {
        public const string State = "JumpRope";
        public const float Side = 0.24f, Front = 0.14f, Circle = 0.05f;
        [Range(0f, 1f)] public float Weight = 0.92f;
        /// 줄 각(라디안) — 줄넘기 자리(JumpRopeStation)가 박자에 맞춰 넣음. 없으면 시간으로 돎
        public float Phase = float.NaN;
        public bool Active { get; private set; }
        public float CurrentWeight => w;
        public Vector3 LeftGoal { get; private set; }
        public Vector3 RightGoal { get; private set; }

        Animator an;
        int hash;
        float w, t;

        void Awake() { an = GetComponent<Animator>(); hash = Animator.StringToHash(State); }

        void Update()
        {
            if (an == null) return;
            bool on = false;
            if (an.isActiveAndEnabled && an.isHuman && an.runtimeAnimatorController != null)
                for (int i = 0; i < an.layerCount && !on; i++) on = an.GetCurrentAnimatorStateInfo(i).shortNameHash == hash && (i == 0 || an.GetLayerWeight(i) > 0.5f);
            Active = on;
            w = Mathf.MoveTowards(w, Active ? Weight : 0f, 5f * Time.deltaTime);
            t += Time.deltaTime;
        }

        void OnAnimatorIK(int layer)
        {
            if (layer != 0 || an == null || !an.isHuman || w <= 0f) return;
            var hips = an.GetBoneTransform(HumanBodyBones.Hips);
            if (hips == null) return;
            var fwd = an.transform.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            fwd.Normalize();
            var right = Vector3.Cross(Vector3.up, fwd);
            float ph = float.IsNaN(Phase) ? t * Mathf.PI * 4f : Phase;
            var spin = (fwd * Mathf.Sin(ph) + Vector3.up * Mathf.Cos(ph)) * Circle;
            LeftGoal = hips.position - right * Side + fwd * Front + spin;
            RightGoal = hips.position + right * Side + fwd * Front + spin;
            Goal(AvatarIKGoal.LeftHand, LeftGoal, AvatarIKHint.LeftElbow, hips.position - right * (Side + 0.06f) - fwd * 0.12f + Vector3.up * 0.22f);
            Goal(AvatarIKGoal.RightHand, RightGoal, AvatarIKHint.RightElbow, hips.position + right * (Side + 0.06f) - fwd * 0.12f + Vector3.up * 0.22f);
        }

        void Goal(AvatarIKGoal g, Vector3 pos, AvatarIKHint h, Vector3 hint)
        {
            an.SetIKPositionWeight(g, w);
            an.SetIKPosition(g, pos);
            an.SetIKRotationWeight(g, 0f);
            an.SetIKHintPositionWeight(h, w * 0.7f);
            an.SetIKHintPosition(h, hint);
        }

        /// 이 모델(배우)의 애니메이터에 붙인다(켜진 것 — 의상이 여럿이면 지금 보이는 것)
        public static RopeArms On(Component owner)
        {
            if (owner == null) return null;
            Animator best = null;
            foreach (var a in owner.GetComponentsInChildren<Animator>(false)) if (a.isHuman && a.isActiveAndEnabled) { best = a; break; }
            if (best == null) return null;
            return best.GetComponent<RopeArms>() ?? best.gameObject.AddComponent<RopeArms>();
        }
    }
}
