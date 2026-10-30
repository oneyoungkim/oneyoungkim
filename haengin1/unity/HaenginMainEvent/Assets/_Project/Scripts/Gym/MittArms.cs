// 행인1의 메인이벤트 — 미트 잡는 팔(2026-10-30 대표 "균형도 안 맞고 이상하다"): 미트 전용 클립이 없어 대기 위에 IK 로 두 손을 가슴 앞으로 모은다. 드릴이 실제로 도는 동안만 켠다(GymNpc.Mitt).
// 2026-10-31 대표: 미트 소품을 양손에(패드가 앞, 손을 파고들거나 뜨지 않게 — 손 뼈 자리에 몸 기준 방향으로 매 프레임 맞춤) · 콤비 호출 때 미트 높이 = 턱~어깨(얼굴을 가리지 않게, 두 손은 어깨 너비로 벌림).
// 애니메이터 기본 층 IK 패스가 켜져 있어야 함(Gym.controller — GymSetup 이 켬).
using UnityEngine;

namespace Haengin
{
    [DisallowMultipleComponent]
    public sealed class MittArms : MonoBehaviour
    {
        /// 손목 기준: 가슴에서 앞·옆(m) · 어깨에서 높이(m). 들었을 때 = 어깨~머리 뼈(턱 높이) 사이
        public const float Front = 0.33f, Side = 0.17f, Drop = -0.20f, UpFront = 0.40f, UpSide = 0.23f, UpHead = 0.30f, Turn = 0.85f;
        /// 미트: 메시 크기 배율 · 손목에서 손끝 쪽·앞쪽으로(m)
        public const float PadScale = 0.66f, PadAlong = 0.075f, PadFront = 0.012f;
        /// 미트 안에 든 손 뼈 크기(살짝 쥔 손이 미트 옆으로 삐져나오지 않게 — 미트를 낀 동안만)
        public const float HandTuck = 0.55f;
        [Range(0f, 1f)] public float Weight = 0.95f;
        public bool On;
        public bool Up;
        public float CurrentWeight => w;
        public Vector3 LeftGoal { get; private set; }
        public Vector3 RightGoal { get; private set; }
        /// 패드 자리(타격 이펙트가 여기서 남)
        public Transform PadL => padL;
        public Transform PadR => padR;
        public bool PadsShown => padL != null && padL.gameObject.activeSelf;
        public float LiftWeight => up;

        Animator an;
        Transform padL, padR, handL, handR;
        float w, up;

        void Awake() { an = GetComponent<Animator>(); }

        void Pads()
        {
            if (padL != null || an == null || !an.isHuman) return;
            var gm = GymManager.Instance;
            if (gm == null || gm.MittMeshL == null || gm.MittMeshR == null) return;
            padL = Make("미트 왼손", gm.MittMeshL, gm.MittMat);
            padR = Make("미트 오른손", gm.MittMeshR, gm.MittMat);
            handL = an.GetBoneTransform(HumanBodyBones.LeftHand);
            handR = an.GetBoneTransform(HumanBodyBones.RightHand);
        }

        Transform Make(string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            float ls = Mathf.Max(1e-3f, transform.lossyScale.x);
            go.transform.localScale = Vector3.one * (PadScale / ls);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            go.SetActive(false);
            return go.transform;
        }

        void Update()
        {
            w = Mathf.MoveTowards(w, On ? Weight : 0f, 4f * Time.deltaTime);
            up = Mathf.MoveTowards(up, Up ? 1f : 0f, 7f * Time.deltaTime);
        }

        void OnAnimatorIK(int layer)
        {
            if (layer != 0 || an == null || !an.isHuman || w <= 0f) return;
            var chest = an.GetBoneTransform(HumanBodyBones.UpperChest) ?? an.GetBoneTransform(HumanBodyBones.Chest);
            var ul = an.GetBoneTransform(HumanBodyBones.LeftUpperArm); var ur = an.GetBoneTransform(HumanBodyBones.RightUpperArm);
            var head = an.GetBoneTransform(HumanBodyBones.Head);
            if (chest == null || ul == null || ur == null || head == null) return;
            var fwd = an.transform.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            fwd.Normalize();
            var right = Vector3.Cross(Vector3.up, fwd);
            float shY = (ul.position.y + ur.position.y) * 0.5f;
            float f = Mathf.Lerp(Front, UpFront, up), s = Mathf.Lerp(Side, UpSide, up);
            float y = Mathf.Lerp(shY + Drop, Mathf.Lerp(shY, head.position.y, UpHead) - PadAlong, up);     // 들어도 미트 가운데가 턱 아래
            var c = new Vector3(chest.position.x, y, chest.position.z);
            LeftGoal = c + fwd * f - right * s;
            RightGoal = c + fwd * f + right * s;
            // 팔꿈치 = 옆구리 바깥 아래 조금 뒤
            var eh = Vector3.up * (-0.22f + 0.10f * up) - fwd * 0.02f;
            // 손 방향: 손가락 위·손바닥 앞(미트 면이 치는 사람 쪽)
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

        void Show(bool on)
        {
            padL.gameObject.SetActive(on); padR.gameObject.SetActive(on);
            if (handL != null) handL.localScale = Vector3.one * (on ? HandTuck : 1f);
            if (handR != null) handR.localScale = Vector3.one * (on ? HandTuck : 1f);
        }

        void OnDisable() { if (padL != null && padL.gameObject.activeSelf) Show(false); }

        // 애니메이터·IK 다음: 미트를 손에. 메시 축 = 패드 +Y · 손끝 −Z(소품 GLB 그대로) → 패드가 몸 앞, 손끝이 위
        void LateUpdate()
        {
            if (!On && w <= 0.01f) { if (padL != null && padL.gameObject.activeSelf) Show(false); return; }
            Pads();
            if (padL == null) return;
            bool show = w > 0.35f;
            if (padL.gameObject.activeSelf != show) Show(show);
            if (!show) return;
            var fwd = an.transform.forward; fwd.y = 0f; fwd.Normalize();
            var rot = Quaternion.LookRotation(Vector3.down, fwd);
            padL.SetPositionAndRotation(handL.position + Vector3.up * PadAlong + fwd * PadFront, Quaternion.AngleAxis(6f, Vector3.up) * rot);
            padR.SetPositionAndRotation(handR.position + Vector3.up * PadAlong + fwd * PadFront, Quaternion.AngleAxis(-6f, Vector3.up) * rot);
        }
    }
}
