// 행인1의 메인이벤트 — 무대 배우 자세(1화 배역, CastSetup): 켜질 때 애니메이터 상태(Idle · Sit · TalkOpen · TalkHip · SitClap · SitFist)로.
// 같은 상태를 쓰는 반 친구들이 똑같이 움직이지 않게 이름 해시로 시작 시점을 흩는다.
using UnityEngine;

namespace Haengin
{
    [DisallowMultipleComponent]
    public sealed class CastPose : MonoBehaviour
    {
        public string State = "Idle";

        Animator an;
        int hash;

        void OnEnable() => Apply();

        public void Set(string state) { State = state; centered = false; Apply(); }

        /// 지금 상태 이름 해시(없으면 0) — 시험용
        public bool Holding => an != null && hash != 0 && an.GetCurrentAnimatorStateInfo(0).shortNameHash == hash;

        void Apply()
        {
            if (an == null) an = GetComponentInChildren<Animator>();
            hash = 0;
            if (an == null || an.runtimeAnimatorController == null || string.IsNullOrEmpty(State)) return;
            int h = Animator.StringToHash(State);
            if (!an.HasState(0, h) && State == "Sit") h = Animator.StringToHash("Ride");     // 'Sit' 이 없는 컨트롤러: 자전거 'Ride'(의자 앉기 동작)
            if (!an.HasState(0, h) && (State == "SitTalk" || State == "SitFloor")) h = Animator.StringToHash("Sit");
            if (!an.HasState(0, h)) return;
            hash = h;
            float off = Mathf.Abs(name.GetHashCode() % 97) / 97f;
            an.Play(h, 0, off);
        }

        bool centered;

        /// QA Q04: 양반다리(SitFloor) 끝 자세는 몸이 루트보다 앞(밥상 쪽)으로 0.3m 쯤 나가 있어, 배우 자리(방석 가운데)에 세워도 방석 옆 맨바닥 위에 떠 앉았음
        ///   → 자세가 처음 잡힌 프레임에 엉덩이를 배우 자리 위로(모델만 옮김, 한 번)
        void CenterHips()
        {
            if (centered || State != "SitFloor" || an == null || !an.isHuman || hash == 0) return;
            if (an.GetCurrentAnimatorStateInfo(0).shortNameHash != hash || an.IsInTransition(0)) return;
            var hips = an.GetBoneTransform(HumanBodyBones.Hips);
            if (hips == null || hips.position.y - transform.position.y > 0.5f) return;      // 아직 앉은 자세가 안 그려짐(화면 밖 — 뼈가 기본 자세)
            var d = hips.position - transform.position; d.y = 0f;
            if (d.magnitude > 0.02f && d.magnitude < 0.8f) an.transform.position -= d;
            centered = true;
        }

        void LateUpdate()
        {
            CenterHips();
            // 무대가 켜질 때 애니메이터가 기본 상태(Idle)로 되돌리면 다시 — OnEnable 의 Play 가 씹히던 것(1화: 하늘·반 친구가 서 있음)
            if (an == null || hash == 0 || !an.isActiveAndEnabled) return;
            if (an.GetCurrentAnimatorStateInfo(0).shortNameHash != hash && !an.IsInTransition(0))
            {
                float off = Mathf.Abs(name.GetHashCode() % 97) / 97f;
                an.Play(hash, 0, off);
            }
        }
    }
}
