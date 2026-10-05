// 행인1의 메인이벤트 — 플레이어 입력 (docs/07_M1_조작_설계.md 2장·7-5)
// 입력 에셋 Assets/_Project/Input/HInput.inputactions 의 Explore 맵을 읽어 PlayerMotor 에 넣는다.
// PlayerInput 컴포넌트·생성 C# 클래스는 쓰지 않는다(액션 인스턴스를 에셋 하나로 — CamInput 도 같은 에셋).
using UnityEngine;
using UnityEngine.InputSystem;

namespace Haengin
{
    [DefaultExecutionOrder(-50)]
    public sealed class PInput : MonoBehaviour
    {
        public const string MapName = "Explore";

        public InputActionAsset Actions;
        public PlayerMotor Motor;
        public CamRig Cam;
        [Tooltip("L3(왼스틱 누름) = 달리기 토글 옵션")] public bool UseRunToggle;

        InputAction move, run, runToggle, interact, lockOn, pause, debugHud;
        bool toggled, prevPause;

        void Awake()
        {
            if (Motor == null) Motor = GetComponent<PlayerMotor>();
        }

        void OnEnable()
        {
            // AddComponent 직후(Actions 를 넣기 전)에도 불리므로 여기서는 묶을 수 있을 때만 묶는다. 못 묶었으면 Update 에서 다시.
            if (Actions != null) Bind();
        }

        bool Bind()
        {
            if (move != null) return true;
            if (Actions == null) return false;
            InputAction A(string n) => Actions.FindAction(MapName + "/" + n, true);
            move = A("Move"); run = A("Run"); runToggle = A("RunToggle"); interact = A("Interact");
            lockOn = A("LockOn"); pause = A("Pause"); debugHud = A("DebugHud");
            Actions.FindActionMap(MapName, true).Enable();
            GameState.LockCursor(true);
            return true;
        }

        void OnDisable()
        {
            if (Motor != null) Motor.ClearMoveInput();
        }

        void Start()
        {
            if (Cam == null) Cam = FindAnyObjectByType<CamRig>();
            if (!Bind()) Debug.LogError("[PInput] 입력 에셋(HInput)이 비어 있습니다");
        }

        void Update()
        {
            if (!Bind()) return;
            if (GameUi.Edge(pause, ref prevPause)) GameState.TogglePause();   // Esc·Options: 열고 닫기(메뉴 안 고르기는 GameUi)
            if (debugHud.WasPressedThisFrame()) DebugHud.Toggle();
            if (GameState.Paused) { Motor.ClearMoveInput(); return; }

            // 창을 다시 클릭하면 커서를 다시 잡는다(Alt+Tab 뒤)
            if (!GameState.CursorLocked && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) GameState.LockCursor(true);

            Vector2 m = Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f);
            var basis = Cam != null ? Cam.MoveBasis : Quaternion.identity;            // 07 4-4: 궤도 정면 기준
            var dir = basis * new Vector3(m.x, 0f, m.y);

            if (UseRunToggle && runToggle.WasPressedThisFrame()) toggled = !toggled;
            if (toggled && Motor.CommandSpeed < 0.5f && m.sqrMagnitude < 0.01f) toggled = false;
            bool running = run.IsPressed() || toggled;
            Motor.SetMoveInput(dir, m.magnitude, running);

            if (interact.WasPressedThisFrame()) DebugHud.Toast("상호작용은 M1 에서 자리만 있어요");
            if (lockOn.WasPressedThisFrame() && Cam != null) Cam.RecenterBehind();     // M2: 대상이 있으면 락온, 없으면 지금처럼 정렬
        }
    }
}
