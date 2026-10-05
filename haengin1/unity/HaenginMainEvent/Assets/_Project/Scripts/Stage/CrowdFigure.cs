// 행인1의 메인이벤트 — 구경꾼 한 사람 (docs/08_M2_전투_설계.md 3-8 ②·8-1·8-2)
// 적 모델을 회색 재질로 쓴 사람(Prefabs/Crowd_*.prefab — 캡슐 실루엣 대신, 11-4). 애니메이터 = 적 컨트롤러(대기·걷기·260 밀기).
//   Shove: 「구경꾼 되받기」·야차 링 가장자리에서 두 손으로 떠밂(260 Push_Forward 의 미는 부분 + 몸 20° 숙이며 0.2m 들어옴, 0.45초)
//   WalkTo: 야차 입장(원 둘레로 걸어와 섬) · Disperse: 끝나면 흩어짐(뒤로 걸어 나가 사라짐)
using UnityEngine;

namespace Haengin
{
    [DisallowMultipleComponent]
    public sealed class CrowdFigure : MonoBehaviour
    {
        public Animator Anim;
        static readonly int HBlend = Animator.StringToHash("Blend"), HRate = Animator.StringToHash("Rate"), HSpeed = Animator.StringToHash("Speed");

        Vector3 from, to, home;
        Quaternion homeRot, faceRot;
        float walkT = -1f, walkDur, shoveT = -1f, leaveT = -1f;
        Vector3 shoveDir;

        public bool Walking => walkT >= 0f;
        public int Shoves { get; private set; }

        void Awake()
        {
            if (Anim == null) Anim = GetComponentInChildren<Animator>();
            home = transform.position;
            homeRot = transform.rotation;
        }

        /// 지금 자리를 '제자리'로(떠민 뒤 돌아올 곳)
        public void SetHome() { home = transform.position; homeRot = transform.rotation; }

        public void Shove(Vector3 toward)
        {
            if (shoveT >= 0f) return;
            var d = new Vector3(toward.x - transform.position.x, 0f, toward.z - transform.position.z);
            shoveDir = d.sqrMagnitude > 1e-4f ? d.normalized : transform.forward;
            SetHome();
            transform.rotation = Quaternion.LookRotation(shoveDir);
            shoveT = 0f;
            Shoves++;
            if (Anim != null && Anim.HasState(0, Animator.StringToHash("PushFwd")))
                Anim.CrossFadeInFixedTime("PushFwd", 0.08f, 0, 1.55f);     // 260 의 미는 부분(타격 1.95초 앞 0.4초)
        }

        public void WalkTo(Vector3 pos, Vector3 lookAt, float secs)
        {
            from = transform.position;
            to = pos;
            walkDur = Mathf.Max(0.1f, secs);
            walkT = 0f;
            var d = new Vector3(to.x - from.x, 0f, to.z - from.z);
            if (d.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(d);
            var f = new Vector3(lookAt.x - pos.x, 0f, lookAt.z - pos.z);
            faceRot = f.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(f) : transform.rotation;
            leaveT = -1f;
        }

        /// 흩어짐: 바깥쪽으로 걸어 나가 1.6초 뒤 사라짐
        public void Disperse(Vector3 center)
        {
            var d = new Vector3(transform.position.x - center.x, 0f, transform.position.z - center.z);
            var dir = d.sqrMagnitude > 1e-4f ? d.normalized : -transform.forward;
            WalkTo(transform.position + dir * 2.4f, transform.position + dir * 5f, 1.6f);
            leaveT = 0f;
        }

        void Update()
        {
            float dt = TimeFx.Dt > 0f ? TimeFx.Dt : 0f;
            if (walkT >= 0f)
            {
                walkT += dt;
                float u = Mathf.Clamp01(walkT / walkDur);
                var p = Vector3.Lerp(from, to, u);
                p.y = Ground(p, from.y);
                transform.position = p;
                float speed = Vector3.Distance(from, to) / walkDur;
                if (Anim != null) { Anim.SetFloat(HBlend, u < 1f ? 1f : 0f); Anim.SetFloat(HRate, Mathf.Clamp(speed / 1.4f, 0.5f, 2f)); Anim.SetFloat(HSpeed, u < 1f ? speed : 0f); }
                if (u >= 1f)
                {
                    walkT = -1f;
                    transform.rotation = faceRot;
                    SetHome();
                    if (leaveT >= 0f) gameObject.SetActive(false);
                }
            }
            if (shoveT >= 0f)
            {
                shoveT += dt;
                // 0 → 0.15 숙이며 들어옴 → 0.30 유지 → 0.45 돌아감
                float u = shoveT / 0.45f;
                float w = u < 0.33f ? u / 0.33f : u < 0.66f ? 1f : Mathf.Max(0f, 1f - (u - 0.66f) / 0.34f);
                transform.position = home + shoveDir * (0.2f * w);
                transform.rotation = Quaternion.AngleAxis(20f * w, Vector3.Cross(Vector3.up, shoveDir)) * Quaternion.LookRotation(shoveDir);
                if (u >= 1.6f)
                {
                    shoveT = -1f;
                    transform.position = home;
                    if (Anim != null && Anim.HasState(0, Animator.StringToHash("Move"))) Anim.CrossFadeInFixedTime("Move", 0.2f);
                }
            }
        }

        static float Ground(Vector3 p, float fallback)
        {
            return Physics.Raycast(p + Vector3.up * 2f, Vector3.down, out var hit, 6f, 1 << Layers.Ground, QueryTriggerInteraction.Ignore) ? hit.point.y : fallback;
        }
    }
}
