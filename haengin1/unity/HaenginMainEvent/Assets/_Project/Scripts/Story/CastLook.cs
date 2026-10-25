// 행인1의 메인이벤트 — 무대 배우 고개·팔 IK(1화 교실 — 2026-10-25): 대사 이벤트 "look:배우|반:대상", "raise:배우:대상"
// look = 고개(몸 30%)를 대상 머리 쪽으로(반 친구들은 이름 해시로 0~0.45초 흩어 돌아봄 — "반 전체가 뒤돌아봤다")
// raise = 오른팔을 대상 쪽 가슴 앞으로 들기(손 모양은 HandPoser '엄지 척'이 따로)
// 애니메이터 기본 층 IK 패스가 켜져 있어야 함(Cast.controller = CastSetup, 태오 = Siwoo.controller 의 Ride IK 패스).
using System.Collections.Generic;
using UnityEngine;

namespace Haengin
{
    [DisallowMultipleComponent]
    public sealed class CastLook : MonoBehaviour
    {
        Animator an;
        Transform lookAt, raiseTo, faceTo;
        float wLook, wRaise, delay, since;
        float? yaw0;
        float maxTurn = 180f;
        Vector3 raiseDir = Vector3.forward;
        const float Ease = 3f;

        public float LookWeight => wLook;
        public float RaiseWeight => wRaise;
        public Transform LookTarget => lookAt;

        static readonly HashSet<string> NotClass = new HashSet<string> { "도경민", "엄마", "하늘", "할아버지" };

        /// 이름(또는 "반" = 무대의 앉은 학생 전부)의 배우들 — 없으면 붙인다
        public static List<CastLook> For(string who)
        {
            var list = new List<CastLook>();
            if (who == "반")
            {
                foreach (var cp in Object.FindObjectsByType<CastPose>(FindObjectsSortMode.None))
                    if (!NotClass.Contains(cp.name)) Add(cp.transform, list);
            }
            else Add(CutCtx.Actor(who), list);
            return list;
        }

        static void Add(Transform t, List<CastLook> list)
        {
            if (t == null) return;
            var a = t.GetComponentInChildren<Animator>();
            if (a == null) return;
            var c = a.GetComponent<CastLook>() ?? a.gameObject.AddComponent<CastLook>();
            c.an = a;
            list.Add(c);
        }

        public void Look(Transform target)
        {
            lookAt = target != null ? target : lookAt;
            if (target == null) lookAt = null;
            delay = Mathf.Abs(transform.root.name.GetHashCode() % 45) / 100f + Mathf.Abs(name.GetHashCode() % 7) / 100f;
            since = 0f;
        }

        public void Raise(Transform target) { raiseTo = target; }

        /// 몸(뿌리)을 대상 쪽으로 돌림 — 앉은 채 돌아앉기(1-6 태오). null = 원래 방향으로
        public void Face(Transform target, float max = 180f)
        {
            if (yaw0 == null) yaw0 = transform.eulerAngles.y;
            faceTo = target; maxTurn = max;
            delay = Mathf.Abs(transform.root.name.GetHashCode() % 45) / 100f + Mathf.Abs(name.GetHashCode() % 7) / 100f;
            since = 0f;
        }

        static Vector3 HeadOf(Transform t)
        {
            var a = t.GetComponentInChildren<Animator>();
            var h = a != null && a.isHuman ? a.GetBoneTransform(HumanBodyBones.Head) : null;
            return h != null ? h.position + Vector3.up * 0.05f : t.position + Vector3.up * 1.6f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            since += dt;
            float wantLook = lookAt != null && since >= delay ? 1f : 0f;
            wLook = Mathf.MoveTowards(wLook, wantLook, Ease * dt);
            wRaise = Mathf.MoveTowards(wRaise, raiseTo != null ? 1f : 0f, Ease * dt);
            if (wLook <= 0f && lookAt == null && wRaise <= 0f && raiseTo == null) lastLook = null;
            if (yaw0.HasValue)
            {
                float want = yaw0.Value;
                if (faceTo != null)
                {
                    var d = faceTo.position - transform.position; d.y = 0f;
                    if (d.sqrMagnitude > 1e-3f) want = Quaternion.LookRotation(d).eulerAngles.y;
                    want = yaw0.Value + Mathf.Clamp(Mathf.DeltaAngle(yaw0.Value, want), -maxTurn, maxTurn);
                    if (since < delay) want = transform.eulerAngles.y;
                }
                float y = Mathf.MoveTowardsAngle(transform.eulerAngles.y, want, 240f * dt);
                transform.rotation = Quaternion.Euler(0f, y, 0f);
                if (faceTo == null && Mathf.Abs(Mathf.DeltaAngle(y, yaw0.Value)) < 0.01f) yaw0 = null;
            }
        }

        // 손목: 엄지가 위, 손가락 쪽이 대상 쪽(엄지 척이 보이게) — 애니메이터·IK 다음(LateUpdate)에 손 뼈를 직접 돌림. 손가락 굽힘은 HandPoser(손 기준 지역 회전)라 순서 상관없음
        void LateUpdate()
        {
            if (wRaise <= 0f || an == null || !an.isHuman) return;
            var hand = an.GetBoneTransform(HumanBodyBones.RightHand);
            var mid = an.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
            var th = an.GetBoneTransform(HumanBodyBones.RightThumbProximal);
            if (hand == null || mid == null || th == null) return;
            var fL = mid.localPosition.normalized;
            var tL = Vector3.ProjectOnPlane(th.localPosition, fL);
            if (tL.sqrMagnitude < 1e-8f) return;
            var want = Quaternion.LookRotation(raiseDir, Vector3.up) * Quaternion.Inverse(Quaternion.LookRotation(fL, tL.normalized));
            hand.rotation = Quaternion.Slerp(hand.rotation, want, wRaise);
        }

        Transform lastLook;

        void OnAnimatorIK(int layer)
        {
            if (layer != 0 || an == null) return;
            if (lookAt != null) lastLook = lookAt;
            if (wLook > 0f && lastLook != null)
            {
                an.SetLookAtWeight(wLook, 0.3f, 0.9f, 1f, 0.55f);
                an.SetLookAtPosition(HeadOf(lastLook));
            }
            if (wRaise > 0f && an.isHuman)
            {
                var sh = an.GetBoneTransform(HumanBodyBones.RightUpperArm);
                var chest = an.GetBoneTransform(HumanBodyBones.Chest) ?? an.GetBoneTransform(HumanBodyBones.Spine);
                if (sh != null && chest != null)
                {
                    var tgt = raiseTo != null ? HeadOf(raiseTo) : sh.position + an.transform.forward;
                    var dir = tgt - sh.position; dir.y = 0f;
                    dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : an.transform.forward;
                    raiseDir = dir;
                    var right = Vector3.Cross(Vector3.up, dir);
                    var hand = sh.position + dir * 0.42f + Vector3.up * 0.10f + right * 0.06f;
                    an.SetIKPositionWeight(AvatarIKGoal.RightHand, wRaise);
                    an.SetIKPosition(AvatarIKGoal.RightHand, hand);
                    an.SetIKHintPositionWeight(AvatarIKHint.RightElbow, wRaise * 0.8f);
                    an.SetIKHintPosition(AvatarIKHint.RightElbow, sh.position + Vector3.down * 0.28f + right * 0.12f + dir * 0.05f);
                }
            }
        }
    }
}
