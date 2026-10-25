// 행인1의 메인이벤트 — 자전거 탄 자세(docs/09 2-6 '모습' — IK). 시우 모델(교복·배달복 각각)에 붙는다.
// 바탕 자세 = 애니메이터 'Ride' 상태(동작 33 Chair_Sit_Idle_M 의 앉은 자세), 그 위에 Humanoid IK:
//   골반 → 안장(모델 자리를 매 프레임 옮겨 맞춤) · 발 → 페달(크랭크 각도대로) · 손 → 핸들 손잡이 · 무릎·팔꿈치 힌트
//   일어서기(9° 넘는 오르막) = 골반을 안장 위 0.18m · 앞 0.12m + 척추 앞 숙임 20°(+ 앉아서 12°, 엉덩이 들기 16°) — 박자마다 좌우 8° 는 BikeVisual
//   정차 = 왼발만 땅(FootDown), 오른발은 페달. **내리지 않는다.**
using UnityEngine;

namespace Haengin
{
    [DefaultExecutionOrder(30), DisallowMultipleComponent, RequireComponent(typeof(Animator))]
    public sealed class BikeRider : MonoBehaviour
    {
        public const string RideState = "Ride";
        public BikeMotor Motor;
        public BikeVisual Bike;
        public float SeatLift = 0.07f, StandUp = 0.18f, StandFwd = 0.12f, LeanSeat = 12f, LeanHip = 16f, LeanStand = 20f;
        public bool Riding { get; private set; }
        /// 안장과 골반 사이 거리(m, 테스트)
        public float HipGap { get; private set; }
        public float FootGapL { get; private set; }
        public float FootGapR { get; private set; }

        Animator anim;
        Transform hips, spine;
        Quaternion spine0; Vector3 spineAxis; bool cached;
        float stand, lean, offY;
        LocoAnim loco;
        Behaviour lean2;
        int frames;

        void Awake()
        {
            anim = GetComponent<Animator>();
            loco = GetComponent<LocoAnim>();
        }

        public void Begin(BikeMotor m, BikeVisual b)
        {
            Motor = m; Bike = b;
            if (anim == null) Awake();
            hips = anim.GetBoneTransform(HumanBodyBones.Hips);
            spine = anim.GetBoneTransform(HumanBodyBones.Chest) ?? anim.GetBoneTransform(HumanBodyBones.Spine);
            if (loco != null) loco.enabled = false;
            var bl = GetComponentInParent<BodyLean>();
            if (bl != null) { bl.enabled = false; lean2 = bl; bl.transform.localRotation = Quaternion.identity; bl.transform.localPosition = Vector3.zero; }
            if (anim.HasState(0, Animator.StringToHash(RideState))) anim.CrossFadeInFixedTime(RideState, 0.15f, 0);
            Riding = true; cached = false; frames = 0;
            enabled = true;
        }

        public void End()
        {
            Riding = false;
            if (loco != null) loco.enabled = true;
            if (lean2 != null) { lean2.transform.localRotation = Quaternion.identity; lean2.enabled = true; }
            transform.localPosition = Vector3.zero;
            if (anim != null && anim.isActiveAndEnabled) anim.CrossFadeInFixedTime(CharSetupNames.Move, 0.2f, 0);
        }

        void LateUpdate()
        {
            if (!Riding || Motor == null || Bike == null || hips == null) return;
            frames++;
            if (!cached && frames >= 3 && spine != null)
            {
                // 앉기 클립의 척추 자세를 한 번 기억(덮어쓰기 전) — 앞 숙임은 그 위에 더한다
                spine0 = spine.localRotation;
                spineAxis = Quaternion.Inverse(spine.rotation) * transform.right;
                cached = true;
            }
            // 골반을 안장에: 모델 자리를 옮긴다(애니메이션·IK 가 끝난 뒤의 골반 위치 기준 — 다음 프레임에 맞음)
            float dt = Time.deltaTime;
            float wantStand = Motor.Posture == BikePosture.Stand ? 1f : 0f;
            stand = Mathf.MoveTowards(stand, wantStand, dt / 0.25f);
            var parent = transform.parent;
            if (Bike.Saddle == null || parent == null) return;
            var seat = parent.InverseTransformPoint(Bike.Saddle.position) + Vector3.up * (SeatLift + StandUp * stand) + Vector3.forward * StandFwd * stand;
            var hipLocal = parent.InverseTransformPoint(hips.position) - transform.localPosition;
            var want = seat - hipLocal;
            transform.localPosition = frames < 3 ? want : Vector3.Lerp(transform.localPosition, want, 1f - Mathf.Exp(-dt * 20f));
            HipGap = (hips.position - Bike.Saddle.position).magnitude;
            lean = Mathf.MoveTowards(lean, Motor.Posture == BikePosture.Stand ? LeanStand : Motor.Posture == BikePosture.Hip ? LeanHip : LeanSeat, 40f * dt);
        }

        void OnAnimatorIK(int layer)
        {
            if (!Riding || !enabled || Motor == null || Bike == null || layer != 0) return;
            // 척추 앞 숙임
            if (cached && spine != null)
            {
                var bone = spine == anim.GetBoneTransform(HumanBodyBones.Chest) ? HumanBodyBones.Chest : HumanBodyBones.Spine;
                anim.SetBoneLocalRotation(bone, spine0 * Quaternion.AngleAxis(lean, spineAxis));
            }
            // 손 → 손잡이
            Goal(AvatarIKGoal.LeftHand, Bike.GripL, AvatarIKHint.LeftElbow, -transform.right * 0.35f + Vector3.down * 0.2f - transform.forward * 0.1f);
            Goal(AvatarIKGoal.RightHand, Bike.GripR, AvatarIKHint.RightElbow, transform.right * 0.35f + Vector3.down * 0.2f - transform.forward * 0.1f);
            // 발 → 페달(정차 = 왼발 땅)
            bool stop = Motor.Stopped && Motor.Posture != BikePosture.Stand;
            Goal(AvatarIKGoal.LeftFoot, stop && Bike.FootDown != null ? Bike.FootDown : Bike.PedalL, AvatarIKHint.LeftKnee, transform.forward * 0.45f + Vector3.up * 0.25f);
            Goal(AvatarIKGoal.RightFoot, Bike.PedalR, AvatarIKHint.RightKnee, transform.forward * 0.45f + Vector3.up * 0.25f);
            var lf = anim.GetBoneTransform(HumanBodyBones.LeftFoot); var rf = anim.GetBoneTransform(HumanBodyBones.RightFoot);
            if (lf != null && Bike.PedalL != null) FootGapL = (lf.position - (stop && Bike.FootDown != null ? Bike.FootDown.position : Bike.PedalL.position)).magnitude;
            if (rf != null && Bike.PedalR != null) FootGapR = (rf.position - Bike.PedalR.position).magnitude;
        }

        void Goal(AvatarIKGoal g, Transform t, AvatarIKHint h, Vector3 hintOff)
        {
            if (t == null) { anim.SetIKPositionWeight(g, 0f); anim.SetIKRotationWeight(g, 0f); return; }
            anim.SetIKPositionWeight(g, 1f);
            anim.SetIKPosition(g, t.position);
            anim.SetIKRotationWeight(g, 0.6f);
            anim.SetIKRotation(g, Quaternion.LookRotation(transform.forward, Vector3.up) * (g == AvatarIKGoal.LeftHand || g == AvatarIKGoal.RightHand ? Quaternion.Euler(0f, 0f, g == AvatarIKGoal.LeftHand ? 80f : -80f) : Quaternion.identity));
            anim.SetIKHintPositionWeight(h, 0.8f);
            var joint = anim.GetBoneTransform(h == AvatarIKHint.LeftKnee ? HumanBodyBones.LeftLowerLeg : h == AvatarIKHint.RightKnee ? HumanBodyBones.RightLowerLeg : h == AvatarIKHint.LeftElbow ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
            anim.SetIKHintPosition(h, (joint != null ? joint.position : t.position) + hintOff);
        }
    }

    /// 애니메이터 상태 이름(CharSetup 과 같은 값 — 런타임 어셈블리에서 쓰려고)
    public static class CharSetupNames { public const string Move = "Move"; }
}
