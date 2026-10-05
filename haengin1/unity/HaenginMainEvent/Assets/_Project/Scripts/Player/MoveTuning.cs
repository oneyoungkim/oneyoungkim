// 행인1의 메인이벤트 — 이동 조정값 (docs/07_M1_조작_설계.md 3장·5-2·7-3). 기본값 = 문서 숫자.
using UnityEngine;

namespace Haengin
{
    [CreateAssetMenu(menuName = "Haengin/MoveTuning", fileName = "MoveTuning")]
    public sealed class MoveTuning : ScriptableObject
    {
        [Header("속도 m/s")]
        public float walkSpeed = 1.6f;
        public float runSpeed = 4.5f;
        [Tooltip("패드를 조금 기울였을 때 가장 느린 걷기")] public float minWalkSpeed = 0.5f;
        [Tooltip("이 기울기 이상이면 걷기 최고 속도")] public float stickFull = 0.9f;

        [Header("가감속")]
        [Tooltip("0 → 걷기 시간(초)")] public float walkAccelTime = 0.12f;
        [Tooltip("걷기 → 달리기 시간(초)")] public float runAccelTime = 0.40f;
        [Tooltip("감속 m/s²")] public float decel = 18f;

        [Header("회전 °/s")]
        public float turnRateWalk = 720f;
        public float turnRateRun = 540f;

        [Header("중력·땅")]
        public float gravity = 20f;
        public float maxFall = 20f;
        [Tooltip("접지 중 아래로 누르는 속도 m/s")] public float groundStick = 1f;
        [Tooltip("경사 꼭대기·턱 끝에서 이 거리 안의 땅으로 붙인다")] public float snapDistance = 0.35f;
        [Tooltip("slopeLimit 보다 가파른 면에서 미끄러지는 속도")] public float slideSpeed = 4f;

        [Header("캡슐 (CharacterController)")]
        public float height = 1.74f;
        public float radius = 0.25f;
        public float skinWidth = 0.025f;
        public float stepOffset = 0.30f;
        public float slopeLimit = 40f;

        [Header("몸 기울기 (BodyLean)")]
        public float leanBase = 5f;
        public float leanPerAccel = 0.3f;
        public float leanMaxFwd = 8f;
        public float leanMaxBack = 4f;
        public float bankK = 0.006f;
        public float bankMax = 8f;
        public float leanSmooth = 0.12f;
        public float bankSmooth = 0.10f;
        public float stepSmooth = 0.08f;
        [Tooltip("루트가 경사로 예상보다 한 프레임에 이만큼 넘게 오르내리면 턱으로 보고 비주얼·카메라에서 흡수(캡슐이 연석을 넘을 때 프레임당 0.01~0.04m)")] public float stepThreshold = 0.01f;

        [Header("안전")]
        [Tooltip("마지막 안전 지점을 기록하는 간격(초)")] public float safeInterval = 0.5f;

        public float CenterY => height * 0.5f + skinWidth;
    }
}
