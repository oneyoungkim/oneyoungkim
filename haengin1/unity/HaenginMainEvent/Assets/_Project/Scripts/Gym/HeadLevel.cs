// 행인1의 메인이벤트 — 코치 머리 롤 보정(2026-10-30 대표 "얼굴이 삐뚤다"): 대기 클립(243 Idle) 자체에 머리가 좌우로 2~3° 기운 프레임이 있어 정면 얼굴의 눈선이 삐뚤어 보였다.
// 애니메이터 뒤(LateUpdate)에 머리 뼈의 좌우 기울기(롤)만 0 으로 — 위치·숙임·돌림은 그대로. 대기·대사·레슨 카드 때만(GymNpc.WantsLevelHead), 드릴·미트 중에는 부드럽게 꺼짐.
// 귀 축·얼굴 앞 축 = 바인드 포즈(몸 앞 +Z, 오른쪽 +X)에서 머리 뼈 기준으로 구함 — 뼈 축이 모델마다 달라도 됨. 지금 자세에서 다시 계산하므로 같은 프레임에 두 번 돌아도 그대로(멱등).
using UnityEngine;

namespace Haengin
{
    [DisallowMultipleComponent]
    public sealed class HeadLevel : MonoBehaviour
    {
        public GymNpc Npc;
        /// 끄면 재기만 함(시험 — v2 비교)
        public bool Apply = true;
        const float Ease = 3f;

        Animator an;
        Transform head;
        Vector3 earLocal, fwdLocal;
        float w = 1f;

        public bool Ready { get; private set; }
        public float CurrentWeight => w;
        /// 이번 프레임 머리 롤(도): 보정 전 · 보정 뒤
        public float RollBefore { get; private set; }
        public float RollAfter { get; private set; }

        void Awake() { Bind(); }
        void OnEnable() { w = 1f; }

        void Bind()
        {
            an = GetComponent<Animator>();
            if (an == null || !an.isHuman) return;
            head = an.GetBoneTransform(HumanBodyBones.Head);
            if (head == null) return;
            foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = smr.sharedMesh;
                if (mesh == null) continue;
                int i = System.Array.IndexOf(smr.bones, head);
                var binds = mesh.bindposes;
                if (i < 0 || i >= binds.Length) continue;
                var m = binds[i].inverse;      // 머리 뼈 → 메시 공간(바인드). 크기가 섞여 있어 축으로 회전을 만듦
                var boneInMesh = Quaternion.LookRotation(m.GetColumn(2), m.GetColumn(1));
                var meshInRoot = Quaternion.Inverse(transform.rotation) * smr.transform.rotation;
                var headInRoot = meshInRoot * boneInMesh;
                earLocal = Quaternion.Inverse(headInRoot) * Vector3.right;
                fwdLocal = Quaternion.Inverse(headInRoot) * Vector3.forward;
                Ready = true;
                return;
            }
        }

        static float Roll(Vector3 ear, Vector3 right, Vector3 fwd) => Vector3.SignedAngle(right, ear, fwd);

        void LateUpdate()
        {
            if (!Ready) return;
            w = Mathf.MoveTowards(w, Npc == null || Npc.WantsLevelHead ? 1f : 0f, Ease * Time.deltaTime);
            var rot = head.rotation;
            var fwd = rot * fwdLocal;
            var ear = rot * earLocal;
            var right = Vector3.Cross(Vector3.up, fwd);
            if (right.sqrMagnitude < 1e-3f) return;          // 얼굴이 곧장 위·아래를 봄(롤이 정의되지 않음)
            right.Normalize();
            if (Vector3.Dot(ear, right) < 0f) return;        // 귀 축이 뒤집힘(대기 자세에선 없음)
            RollBefore = Roll(ear, right, fwd);
            RollAfter = RollBefore;
            if (!Apply || w <= 0f) return;
            // 귀 축을 수평으로: 두 축 모두 얼굴 앞 축에 수직이므로 회전 축 = 얼굴 앞 축(숙임·돌림 그대로)
            var fix = Quaternion.FromToRotation(ear, right);
            head.rotation = Quaternion.Slerp(Quaternion.identity, fix, w) * rot;
            RollAfter = Roll(head.rotation * earLocal, right, fwd);
        }
    }
}
