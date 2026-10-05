// 행인1의 메인이벤트 — 시점 입력 → 궤도 축 (docs/07_M1_조작_설계.md 4-5·7-6)
// 마우스(프레임당 픽셀, 시간 곱하지 않음)와 스틱(−1~1 기울기 → 응답 곡선 → °/s)을 따로 처리해서 OrbitalFollow 축에 직접 쓴다.
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Haengin
{
    [DefaultExecutionOrder(-40)]
    public sealed class CamInput : MonoBehaviour
    {
        public InputActionAsset Actions;
        public CinemachineOrbitalFollow Orbit;
        public CamTuning Tuning;

        InputAction lookPad, lookMouse;
        CamTuning fallback;
        CamRig rig;
        CamTuning T => Tuning != null ? Tuning : (fallback != null ? fallback : fallback = ScriptableObject.CreateInstance<CamTuning>());

        void Awake()
        {
            if (Orbit == null) Orbit = GetComponent<CinemachineOrbitalFollow>();
            rig = GetComponent<CamRig>();
        }

        void OnEnable() => Bind();   // AddComponent 직후에는 Actions 가 비어 있을 수 있음 → Update 에서 다시

        void Bind()
        {
            if (lookPad != null || Actions == null) return;
            lookPad = Actions.FindAction(PInput.MapName + "/LookPad", false);
            lookMouse = Actions.FindAction(PInput.MapName + "/LookMouse", false);
            Actions.FindActionMap(PInput.MapName, false)?.Enable();
        }

        void Update()
        {
            Bind();
            if (GameState.Paused || Orbit == null) return;
            var t = T;
            Vector2 ms = lookMouse != null ? lookMouse.ReadValue<Vector2>() : Vector2.zero;   // 픽셀/프레임
            Vector2 st = lookPad != null ? lookPad.ReadValue<Vector2>() : Vector2.zero;       // −1..1
            // 시간 배율과 무관한 프레임 시간(나중의 히트스톱 중에도 카메라는 돈다). 고정 프레임 시간(테스트·녹화)도 따른다
            float dt = Time.timeScale > 0f ? Time.deltaTime / Time.timeScale : Time.unscaledDeltaTime;
            float yaw = ms.x * t.mouseYaw * t.mouseScale + Curve(st.x, t) * t.padYawSpeed * t.padScale * dt;
            // 위로 밀면 위를 본다 = 카메라가 내려간다 = 피치(VerticalAxis) 감소
            float pitch = ms.y * t.mousePitch * t.mouseScale * (t.invertMouseY ? 1f : -1f)
                        + Curve(st.y, t) * t.padPitchSpeed * t.padScale * dt * (t.invertPadY ? 1f : -1f);
            if (yaw != 0f || pitch != 0f) Rotate(yaw, pitch);
        }

        static float Curve(float v, CamTuning t) => Mathf.Sign(v) * Mathf.Pow(Mathf.Abs(v), t.padExponent);

        /// 테스트·자동 시연도 같은 경로로 카메라를 돌린다. 수동 조작이므로 자동 정렬은 다시 기다린다(07 4-8)
        public void Rotate(float dYaw, float dPitch)
        {
            if (rig == null) rig = GetComponent<CamRig>();
            if (rig != null) rig.NoteManualLook();
            Orbit.HorizontalAxis.Value = Mathf.DeltaAngle(0f, Orbit.HorizontalAxis.Value + dYaw);
            Orbit.VerticalAxis.Value = Mathf.Clamp(Orbit.VerticalAxis.Value + dPitch, Orbit.VerticalAxis.Range.x, Orbit.VerticalAxis.Range.y);
        }
    }
}
