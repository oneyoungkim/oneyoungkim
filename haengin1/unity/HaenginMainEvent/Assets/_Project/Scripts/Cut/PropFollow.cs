// 행인1의 메인이벤트 — 컷신 소품 따라가기(PropTrack). 부모를 바꾸지 않고 소켓을 따라간다.
using UnityEngine;

namespace Haengin
{
    /// 소품이 소켓을 따라간다(부모는 그대로 — 무대 장면을 내리면 함께 사라짐)
    public sealed class PropFollow : MonoBehaviour
    {
        public Transform Target;
        public Vector3 Offset = new Vector3(0f, 1.36f, 0.04f);     // 시우 목(입을 가리지 않게)
        void LateUpdate()
        {
            if (Target == null) return;
            transform.SetPositionAndRotation(Target.TransformPoint(Target.GetComponentInParent<PlayerMotor>() != null ? Offset : Vector3.zero), Target.rotation);
        }
    }
}
