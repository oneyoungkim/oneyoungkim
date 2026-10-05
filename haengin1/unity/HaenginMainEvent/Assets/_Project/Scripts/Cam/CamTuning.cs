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

        [Header("자동 정렬 (07 4-8) — 우리 CamRig 가 각속도 제한으로 돌린다. Cinemachine 정렬은 Q/L1 에만 씀")]
        [Tooltip("Always = 걸을 때도(기본, 2차) · RunOnly = 달릴 때만 · Off = 끔. 일시정지 메뉴에서 바꿀 수 있음")]
        public Auto autoRecenter = Auto.Always;
        [Tooltip("달리기: 마지막 수동 시점 조작 뒤 기다리는 시간(초)")] public float recenterWait = 1.2f;
        [Tooltip("걷기: 마지막 수동 시점 조작 뒤 기다리는 시간(초)")] public float walkRecenterWait = 1.8f;
        [Tooltip("움직이기 시작해서 이만큼 지나야 정렬 시작(제자리 방향 바꾸기·짧은 톡 입력에 안 움직이게)")] public float alignStartDelay = 0.3f;
        [Tooltip("걷기 최대 각속도 °/s")] public float walkAlignSpeed = 30f;
        [Tooltip("달리기 최대 각속도 °/s")] public float runAlignSpeed = 60f;
        [Tooltip("각도 차 × 이 값 = 원하는 각속도(1/s). 가까워질수록 느려짐(끝에서 툭 멈추지 않게)")] public float alignGain = 1.2f;
        [Tooltip("각가속도 °/s² (걷기). 달리기는 2배. 시작·방향 바뀜이 부드럽게")] public float alignAccel = 60f;
        [Tooltip("인물이 카메라 쪽으로 걸어오면(궤도와 인물 방향 차가 이 각도 이상) 돌리지 않는다 — 180° 휙 돌기 방지")] public float alignBackAngle = 150f;
        [Tooltip("(Cinemachine 정렬 시간 기본값 — 지금은 Q/L1 뒤 복귀값으로만 씀)")] public float recenterTime = 2.0f;
        [Tooltip("세로: 움직이는 중 수동 조작 없이 이 시간이 지나면 기본 피치로")] public float pitchRecenterWait = 3.0f;
        [Tooltip("세로 정렬 최대 각속도 °/s")] public float pitchAlignSpeed = 12f;
        [Tooltip("Q / L1 등 뒤 정렬 시간")] public float snapTime = 0.30f;

        [Header("이동 기준 고정 (07 4-10) — 스틱을 누르는 동안 자동 정렬이 걷는 방향을 돌리지 않게")]
        [Tooltip("스틱 기울기가 이보다 작으면 놓은 것으로 보고 고정을 푼다")] public float basisRelease = 0.1f;
        [Tooltip("스틱 방향이 이 각도(°)보다 많이 바뀌어야 고정을 푼다(스틱 잡음 무시)")] public float basisStickDeadband = 4f;

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
