// 행인1의 메인이벤트 — 대화 입력(docs/09_M3_버티컬슬라이스_설계.md 2-2 '대화 중 조작')
// 컷신·대화 중엔 Explore·Combat 맵을 끄고 Talk 맵만 켠다(InputMaps.Use — CombatMode 의 '한 번에 한 맵'과 같은 규칙).
// Talk 맵: Next(× · Enter · Space · E · 마우스 왼쪽) · Skip(Select/View · Tab) · Navigate · Pause(Esc · Options).
// 실행 순서 −35 = GameUi(−30) 앞: 일시정지 메뉴에서 '계속'을 누른 × 가 같은 프레임·다음 프레임에 대사로 새지 않게
// (일시정지였던 프레임과 그다음 프레임의 누름은 버리고, 누른 상태 기록만 남긴다 — D03).
using UnityEngine;
using UnityEngine.InputSystem;

namespace Haengin
{
    public static class InputMaps
    {
        public const string Explore = "Explore", Combat = "Combat", Talk = "Talk", Menu = "Menu";
        /// 조작 맵(Menu 는 GameUi 가 늘 켜 둠 — 셈에서 뺌)
        public static readonly string[] Play = { Explore, Combat, Talk };

        /// map 하나만 켜고 나머지 조작 맵은 끈다(null = 다 끔)
        public static void Use(InputActionAsset a, string map)
        {
            if (a == null) return;
            foreach (var n in Play)
            {
                var m = a.FindActionMap(n, false);
                if (m == null) continue;
                if (n == map) { if (!m.enabled) m.Enable(); }
                else if (m.enabled) m.Disable();
            }
        }

        public static int EnabledCount(InputActionAsset a)
        {
            int c = 0;
            if (a == null) return 0;
            foreach (var n in Play) { var m = a.FindActionMap(n, false); if (m != null && m.enabled) c++; }
            return c;
        }

        public static string Enabled(InputActionAsset a)
        {
            if (a == null) return "-";
            var s = "";
            foreach (var n in Play) { var m = a.FindActionMap(n, false); if (m != null && m.enabled) s += (s.Length > 0 ? "+" : "") + n; }
            return s.Length > 0 ? s : "없음";
        }
    }

    [DefaultExecutionOrder(-35), DisallowMultipleComponent]
    public sealed class TalkInput : MonoBehaviour
    {
        public InputActionAsset Actions;
        public static TalkInput Instance { get; private set; }

        /// 이번 프레임 '다음'을 누름
        public bool Next { get; private set; }
        /// 다음·건너뛰기를 누르고 있는 실제 시간(초) — 0.8초면 건너뛰기
        public float HoldTime { get; private set; }
        /// 이번 프레임 고르기 한 칸(−1 위 · +1 아래, 누르고 있으면 반복)
        public int NavStep { get; private set; }
        public bool MapOn => map != null && map.enabled;
        /// 패드로 마지막 입력(안내 글자 × / Enter)
        public bool PadLast { get; private set; }
        /// 일시정지 중이거나 막 풀린 프레임(메뉴 입력이 대사·선택지로 새지 않게)
        public bool Blocked { get; private set; }

        InputActionMap map;
        InputAction next, skip, nav, pause;
        bool pNext, pPause, wasPaused, wasOn;
        int heldDir;
        float repeatAt, clock;
        // 테스트·봇 주입(다음 Update 에서 한 번)
        bool injNext; int injNav; float injHold;

        void OnEnable() { Instance = this; }
        void OnDisable() { if (Instance == this) Instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }

        bool Bind()
        {
            if (map != null) return true;
            if (Actions == null) return false;
            map = Actions.FindActionMap(InputMaps.Talk, false);
            if (map == null) { Debug.LogError("[TalkInput] 입력 에셋에 Talk 맵이 없습니다(tools/input_maps.py)"); Actions = null; return false; }
            next = map.FindAction("Next"); skip = map.FindAction("Skip"); nav = map.FindAction("Navigate"); pause = map.FindAction("Pause");
            return true;
        }

        public void InjectNext() => injNext = true;
        public void InjectNav(int step) => injNav = step;
        /// 건너뛰기(누르고 있음)를 s 초로
        public void InjectHold(float s) => injHold = s;

        void Update()
        {
            float rdt = UiKit.RealDt;
            clock += rdt;
            Next = false; NavStep = 0;
            bool on = Bind() && map.enabled;
            bool paused = GameState.Paused;
            // 맵이 막 켜진 프레임: 이미 누르고 있던 것(타이틀의 × 등)은 누름으로 치지 않는다
            if (on && !wasOn) { pNext = next.IsPressed(); pPause = pause.IsPressed(); }
            wasOn = on;
            // 누른 상태 기록은 늘 갱신(일시정지여도)
            bool nEdge = on && GameUi.Edge(next, ref pNext);
            bool pEdge = on && GameUi.Edge(pause, ref pPause);
            if (pEdge && !GameState.Modal) { GameState.TogglePause(); paused = GameState.Paused; }

            bool blocked = paused || wasPaused || GameState.Modal;
            wasPaused = paused;
            Blocked = blocked;
            if (on && !blocked)
            {
                Next = nEdge;
                bool held = (next != null && next.IsPressed()) || (skip != null && skip.IsPressed());
                HoldTime = held ? HoldTime + rdt : 0f;
                Vector2 v = nav != null ? nav.ReadValue<Vector2>() : Vector2.zero;
                int dir = v.y > 0.5f ? -1 : v.y < -0.5f ? 1 : 0;
                if (dir == 0) heldDir = 0;
                else if (dir != heldDir) { NavStep = dir; heldDir = dir; repeatAt = clock + 0.40f; }
                else if (clock >= repeatAt) { NavStep = dir; repeatAt = clock + 0.15f; }
                var dev = next != null && next.activeControl != null ? next.activeControl.device : nav?.activeControl?.device;
                if (dev != null) PadLast = dev is Gamepad;
            }
            else HoldTime = 0f;

            if (injNext) { Next = true; injNext = false; }
            if (injNav != 0) { NavStep = injNav; injNav = 0; }
            if (injHold > 0f) { HoldTime = injHold; injHold = 0f; }
        }
    }
}
