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

        public void Set(string state) { State = state; Apply(); }

        /// 지금 상태 이름 해시(없으면 0) — 시험용
        public bool Holding => an != null && hash != 0 && an.GetCurrentAnimatorStateInfo(0).shortNameHash == hash;

        void Apply()
        {
            if (an == null) an = GetComponentInChildren<Animator>();
            hash = 0;
            if (an == null || an.runtimeAnimatorController == null || string.IsNullOrEmpty(State)) return;
            int h = Animator.StringToHash(State);
            if (!an.HasState(0, h) && State == "Sit") h = Animator.StringToHash("Ride");     // 태오(정식 모델 = 시우 컨트롤러): 앉기 = 자전거 'Ride'(의자 앉기 동작)
            if (!an.HasState(0, h)) return;
            hash = h;
            float off = Mathf.Abs(name.GetHashCode() % 97) / 97f;
            an.Play(h, 0, off);
        }

        void LateUpdate()
        {
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
