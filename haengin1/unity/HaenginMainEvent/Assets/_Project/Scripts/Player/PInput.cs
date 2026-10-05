// 행인1의 메인이벤트 — 플레이어 입력 (docs/07_M1_조작_설계.md 2장·7-5)
// 입력 에셋 Assets/_Project/Input/HInput.inputactions 의 Explore 맵을 읽어 PlayerMotor 에 넣는다.
// PlayerInput 컴포넌트·생성 C# 클래스는 쓰지 않는다(액션 인스턴스를 에셋 하나로 — CamInput 도 같은 에셋).
// M2(08 문서 2-2): Combat 맵. 한 번에 한 맵 — 전투 시작 때 SetCombatMap(true) 가 Explore 를 끄고 Combat 을 켠다.
// 전투 중에는 스틱·버튼을 PlayerCombat 에 넘긴다(이동·기술·막기·락온·대상 전환·일시정지).
using UnityEngine;
using UnityEngine.InputSystem;

namespace Haengin
{
    [DefaultExecutionOrder(-50)]
    public sealed class PInput : MonoBehaviour
    {
        public const string MapName = "Explore", CombatMap = "Combat";

        public InputActionAsset Actions;
        public PlayerMotor Motor;
        public CamRig Cam;
        [Tooltip("전투 중 입력을 받는 곳(M2)")] public PlayerCombat Combat;
        [Tooltip("L3(왼스틱 누름) = 달리기 토글 옵션")] public bool UseRunToggle;

        InputAction move, run, runToggle, interact, lockOn, pause, debugHud;
        InputAction cMove, cLookPad, cLight, cHeavy, cGrab, cDodge, cGuard, cLock, cNext, cPrev, cRun, cPause, cDebug;
        bool toggled, prevPause, prevCPause, pL, pH, pG, pD, pLock, pNext, pPrev;

        /// 지금 Combat 맵으로 읽는가
        public bool CombatInput { get; private set; }

        void Awake()
        {
            if (Motor == null) Motor = GetComponent<PlayerMotor>();
            if (Combat == null) Combat = GetComponent<PlayerCombat>();
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
            InputAction C(string n) => Actions.FindAction(CombatMap + "/" + n, false);
            cMove = C("Move"); cLookPad = C("LookPad"); cLight = C("Light"); cHeavy = C("Heavy"); cGrab = C("Grab"); cDodge = C("Dodge");
            cGuard = C("Guard"); cLock = C("LockOn"); cNext = C("SwitchNext"); cPrev = C("SwitchPrev"); cRun = C("Run"); cPause = C("Pause"); cDebug = C("DebugHud");
            if (CombatInput) Actions.FindActionMap(CombatMap, true).Enable();
            else Actions.FindActionMap(MapName, true).Enable();
            GameState.LockCursor(true);
            return true;
        }

        void OnDisable()
        {
            if (Motor != null) Motor.ClearMoveInput();
        }

        /// 한 번에 한 맵(08 2-2): on = Explore 끔·Combat 켬, off = 반대. 맵을 바꿀 때 누른 상태 기록을 지운다
        public void SetCombatMap(bool on)
        {
            CombatInput = on;
            if (Combat == null) Combat = GetComponent<PlayerCombat>();
            if (Actions == null) return;
            var e = Actions.FindActionMap(MapName, false);
            var c = Actions.FindActionMap(CombatMap, false);
            if (on) { e?.Disable(); c?.Enable(); }
            else { c?.Disable(); e?.Enable(); }
            prevPause = prevCPause = pL = pH = pG = pD = pLock = pNext = pPrev = false;
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
            if (CombatInput) { CombatUpdate(); return; }
            if (GameUi.Edge(pause, ref prevPause)) GameState.TogglePause();   // Esc·Options: 열고 닫기(메뉴 안 고르기는 GameUi)
            if (debugHud.WasPressedThisFrame()) DebugHud.Toggle();
            if (GameState.Paused) { Motor.ClearMoveInput(); return; }

            // 창을 다시 클릭하면 커서를 다시 잡는다(Alt+Tab 뒤)
            if (!GameState.CursorLocked && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) GameState.LockCursor(true);

            Vector2 m = Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f);
            // 07 4-4: 궤도 정면 기준 + 4-10: 누르는 동안 자동 정렬이 돌린 만큼은 빼서 고정(대각선으로 걸을 때 빙글 돌지 않게)
            var dir = Cam != null ? Cam.StickToWorld(m) : new Vector3(m.x, 0f, m.y);

            if (UseRunToggle && runToggle.WasPressedThisFrame()) toggled = !toggled;
            if (toggled && Motor.CommandSpeed < 0.5f && m.sqrMagnitude < 0.01f) toggled = false;
            bool running = run.IsPressed() || toggled;
            Motor.SetMoveInput(dir, m.magnitude, running);

            if (interact.WasPressedThisFrame()) DebugHud.Toast("상호작용은 M1 에서 자리만 있어요");
            if (lockOn.WasPressedThisFrame() && Cam != null) Cam.RecenterBehind();     // 탐색: 카메라 등 뒤 정렬(전투 중 L1 = 락온, CombatUpdate)
        }

        /// 전투 맵(08 2-1): 스틱·버튼 → PlayerCombat. 누름은 GameUi.Edge(이번 프레임 눌림 또는 지난 프레임 안 눌림 → 지금 눌림)
        void CombatUpdate()
        {
            if (GameUi.Edge(cPause, ref prevCPause)) GameState.TogglePause();
            if (cDebug != null && cDebug.WasPressedThisFrame()) DebugHud.Toggle();
            if (Combat == null) Combat = GetComponent<PlayerCombat>();
            if (Combat == null) return;
            if (GameState.Paused) { Combat.SetStick(Vector2.zero, false); Combat.SetGuard(false); return; }
            if (!GameState.CursorLocked && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) GameState.LockCursor(true);

            Vector2 m = cMove != null ? Vector2.ClampMagnitude(cMove.ReadValue<Vector2>(), 1f) : Vector2.zero;
            bool running = cRun != null && cRun.IsPressed();
            Combat.SetStick(m, running);
            Combat.SetGuard(cGuard != null && cGuard.IsPressed());
            if (GameUi.Edge(cLight, ref pL)) Combat.Press(Btn.Light);
            if (GameUi.Edge(cHeavy, ref pH)) Combat.Press(Btn.Heavy);
            if (GameUi.Edge(cGrab, ref pG)) Combat.Press(Btn.Grab);
            if (GameUi.Edge(cDodge, ref pD)) Combat.Press(Btn.Dodge);
            if (GameUi.Edge(cLock, ref pLock)) Combat.ToggleLock();
            bool prev = GameUi.Edge(cPrev, ref pPrev);
            bool next = GameUi.Edge(cNext, ref pNext);
            if (prev) Combat.SwitchTarget(-1);
            else if (next) Combat.SwitchTarget(+1);
            if (cLookPad != null) Combat.LookStick(cLookPad.ReadValue<Vector2>());
        }
    }
}
