// 행인1의 메인이벤트 — 카메라 조정값 (docs/07_M1_조작_설계.md 4-3·7-3). 기본값 = 문서 숫자.
using UnityEngine;

namespace Haengin
{
    [CreateAssetMenu(menuName = "Haengin/CamTuning", fileName = "CamTuning")]
    public sealed class CamTuning : ScriptableObject
    {
        public enum Auto { Off, RunOnly, Always }

        [Header("렌즈")]
        public float fov = 45f;
        public float fovRun = 49f;
        public float fovUpTime = 0.6f;
        public float fovDownTime = 0.4f;
        public float near = 0.08f;
        public float far = 600f;

        [Header("궤도")]
        public float radius = 4.0f;
        public float radiusRun = 4.3f;
        [Tooltip("따라가는 점(CamTarget) 높이")] public float targetHeight = 1.40f;
        public float pitchDefault = 8f;
        public float pitchMin = -10f;
        public float pitchMax = 50f;
        [Tooltip("화면 구도. x 음수 = 인물이 화면 왼쪽")] public Vector2 screen = new Vector2(-0.06f, 0f);
        public Vector3 posDamping = new Vector3(0.15f, 0.40f, 0.25f);
        public Vector2 aimDamping = new Vector2(0.10f, 0.20f);

        [Header("감도")]
        [Tooltip("°/픽셀")] public float mouseYaw = 0.08f;
        [Tooltip("°/픽셀")] public float mousePitch = 0.07f;
        public float mouseScale = 1f;
        [Tooltip("°/s (스틱 끝까지)")] public float padYawSpeed = 150f;
        [Tooltip("°/s (스틱 끝까지)")] public float padPitchSpeed = 95f;
        public float padExponent = 1.6f;
        public float padScale = 1f;
        public bool invertMouseY;
        public bool invertPadY;

        [Header("자동 정렬")]
        public Auto autoRecenter = Auto.RunOnly;
        public float recenterWait = 1.2f;
        public float recenterTime = 2.0f;
        public float pitchRecenterWait = 3.0f;
        [Tooltip("Q / L1 등 뒤 정렬 시간")] public float snapTime = 0.30f;

        [Header("가림·벽")]
        public float camRadius = 0.10f;
        public float occludedDamping = 0f;
        public float releaseDamping = 0.5f;
        public float holdTime = 0.25f;
        [Tooltip("카메라가 이보다 가까우면 인물 숨김(그림자만)")] public float hideDist = 0.55f;
        public float showDist = 0.70f;

        [Header("벽 간격·다리 가림(구현 추가, CamClearance)")]
        [Tooltip("벽 간격 검사 구 = camRadius + 이 값. 살짝 음수 = Deoccluder 가 벽에서 camRadius 만큼 띄워 둔 자리를 다시 당기지 않게")] public float bodyMargin = -0.005f;
        [Tooltip("벽 간격 때문에 당길 수 있는 가장 가까운 거리(머리에서). 그보다 가까운 경우는 Deoccluder 가 맡음")] public float bodyMinRadius = 0.3f;
        [Tooltip("무릎 높이 → 카메라 선이 벽(Wall·CamBlock)에 막히면 카메라를 머리 쪽으로 당긴다. 등 뒤 펜스·담이 화면 아래를 덮는 것 방지")]
        public bool legCheck = true;
        public float legHeight = 0.5f;
        public float legMinRadius = 1.2f;
    }
}
