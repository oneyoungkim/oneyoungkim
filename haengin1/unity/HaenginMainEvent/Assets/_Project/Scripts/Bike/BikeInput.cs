// 행인1의 메인이벤트 — 배달 자전거 입력(docs/09 2-6 '입력'): HInput 의 Bike 맵 → BikeMotor
//   Steer 왼스틱/A·D · Pedal R2/W(아날로그) · Brake L2/S · RhythmL □/J/마우스 왼쪽 · RhythmR ○/K/마우스 오른쪽 · Handover ×/E(정차 때만)
//   LookPad 오른스틱 · LookMouse 마우스 · Pause Esc/Options. **내리기 버튼은 없다.**
// 자전거를 타는 동안 InputMaps.Use(Bike) — Explore·Combat·Talk 맵은 꺼진다(한 번에 한 맵).
using UnityEngine;
using UnityEngine.InputSystem;

namespace Haengin
{
    [DefaultExecutionOrder(-40), DisallowMultipleComponent]
    public sealed class BikeInput : MonoBehaviour
    {
        public const string MapName = "Bike";
        public InputActionAsset Actions;
        public BikeMotor Motor;
        public BikeCam Cam;
        /// 봇·테스트가 몰 때 사람 입력을 읽지 않음
        public bool Suppressed;

        InputAction steer, pedal, brake, rL, rR, hand, lookPad, lookMouse, pause;
        bool pPause;

        void Awake()
        {
            if (Motor == null) Motor = GetComponent<BikeMotor>();
            Bind();
        }

        void Bind()
        {
            if (Actions == null || steer != null) return;
            var m = Actions.FindActionMap(MapName, false);
            if (m == null) return;
            steer = m.FindAction("Steer"); pedal = m.FindAction("Pedal"); brake = m.FindAction("Brake");
            rL = m.FindAction("RhythmL"); rR = m.FindAction("RhythmR"); hand = m.FindAction("Handover");
            lookPad = m.FindAction("LookPad"); lookMouse = m.FindAction("LookMouse"); pause = m.FindAction("Pause");
        }

        public bool MapOn => Actions != null && Actions.FindActionMap(MapName, false) is InputActionMap m && m.enabled;

        void Update()
        {
            Bind();
            if (Motor == null || !Motor.Mounted || steer == null) return;
            if (GameUi.Edge(pause, ref pPause)) GameState.TogglePause();
            if (Suppressed || GameState.Paused || !MapOn) return;
            float st = steer.ReadValue<float>();
            float pd = pedal.ReadValue<float>();
            float br = brake.ReadValue<float>();
            Motor.SetInput(st, pd, br);
            if (rL.WasPressedThisFrame()) Motor.PressBeat(false);
            if (rR.WasPressedThisFrame()) Motor.PressBeat(true);
            if (hand.WasPressedThisFrame()) Motor.PressHandover();
            if (Cam != null)
            {
                var lp = lookPad != null ? lookPad.ReadValue<Vector2>() : Vector2.zero;
                var lm = lookMouse != null ? lookMouse.ReadValue<Vector2>() : Vector2.zero;
                Cam.Look(lp * 120f * Time.unscaledDeltaTime + lm * 0.12f);
            }
        }
    }
}
