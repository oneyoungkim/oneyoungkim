// 행인1의 메인이벤트 — 미트 잡는 팔(2026-10-30 대표 "균형도 안 맞고 이상하다"): 미트 전용 클립이 없어 전투 Stance(두 팔 수평으로 벌리고 무릎 굽힘)를 쓰던 것을
// 코치 대기(243 Idle — 다리 폭 보통·팔 내림) 위에 IK 로 두 손을 가슴 앞으로 모은다(팔이 가슴 앞 45°로 안쪽을 향함). Up = 미트 들기(호출 순간 — 얼굴 높이로 조금 앞으로).
// 드릴이 실제로 도는 동안만 켠다(GymNpc.Mitt). 애니메이터 기본 층 IK 패스가 켜져 있어야 함(Gym.controller — GymSetup 이 켬).
using UnityEngine;

namespace Haengin
{
    [DisallowMultipleComponent]
    public sealed class MittArms : MonoBehaviour
    {
        /// 가슴(UpperChest/Chest) 기준: 앞·옆·높이(m)
        public const float Front = 0.34f, Side = 0.24f, Drop = 0.0f, UpFront = 0.40f, UpSide = 0.21f, UpRise = 0.18f, Turn = 0.85f;   // 손목 기준 — 옆 0.16 이면 두 손가락이 가운데서 겹침
        [Range(0f, 1f)] public float Weight = 0.95f;
        public bool On;
        public bool Up;
        public float CurrentWeight => w;
        public Vector3 LeftGoal { get; private set; }
        public Vector3 RightGoal { get; private set; }

        Animator an;
        float w, up;

        void Awake() { an = GetComponent<Animator>(); }

        void Update()
        {
            w = Mathf.MoveTowards(w, On ? Weight : 0f, 4f * Time.deltaTime);
            up = Mathf.MoveTowards(up, Up ? 1f : 0f, 7f * Time.deltaTime);
        }

        void OnAnimatorIK(int layer)
        {
            if (layer != 0 || an == null || !an.isHuman || w <= 0f) return;
            var chest = an.GetBoneTransform(HumanBodyBones.UpperChest) ?? an.GetBoneTransform(HumanBodyBones.Chest);
            var hips = an.GetBoneTransform(HumanBodyBones.Hips);
            if (chest == null || hips == null) return;
            var fwd = an.transform.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            fwd.Normalize();
            var right = Vector3.Cross(Vector3.up, fwd);
            float f = Mathf.Lerp(Front, UpFront, up), s = Mathf.Lerp(Side, UpSide, up), h = Mathf.Lerp(Drop, UpRise, up);
            var c = new Vector3(chest.position.x, chest.position.y + h, chest.position.z);
            LeftGoal = c + fwd * f - right * s;
            RightGoal = c + fwd * f + right * s;
            // 팔꿈치 = 옆구리 바깥 아래 조금 뒤(팔이 가슴 앞에서 45°로 모이게)
            var eh = Vector3.up * (-0.22f + 0.10f * up) - fwd * 0.02f;
            // 손 방향: 휴머노이드 손 IK 회전 0 = T-포즈 손(손가락 바깥·손바닥 아래) → 손가락 위·손바닥 앞(미트 면이 치는 사람 쪽). 손바닥이 위를 보던 것(받쳐 드는 손)
            var body = Quaternion.LookRotation(fwd, Vector3.up);
            Goal(AvatarIKGoal.LeftHand, LeftGoal, AvatarIKHint.LeftElbow, chest.position - right * 0.36f + eh, body * Quaternion.LookRotation(Vector3.right, Vector3.back));
            Goal(AvatarIKGoal.RightHand, RightGoal, AvatarIKHint.RightElbow, chest.position + right * 0.36f + eh, body * Quaternion.LookRotation(Vector3.left, Vector3.back));
        }

        void Goal(AvatarIKGoal g, Vector3 pos, AvatarIKHint hnt, Vector3 hint, Quaternion rot)
        {
            an.SetIKPositionWeight(g, w);
            an.SetIKPosition(g, pos);
            an.SetIKRotationWeight(g, w * Turn);
            an.SetIKRotation(g, rot);
            an.SetIKHintPositionWeight(hnt, w * 0.8f);
            an.SetIKHintPosition(hnt, hint);
        }
    }
}
