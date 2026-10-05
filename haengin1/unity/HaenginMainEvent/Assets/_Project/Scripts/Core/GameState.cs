// 행인1의 메인이벤트 — 일시정지·커서 (M1 은 자리만: 시간 정지 + 커서 풀기)
using UnityEngine;

namespace Haengin
{
    public static class GameState
    {
        public static bool Paused { get; private set; }
        public static bool CursorLocked => Cursor.lockState == CursorLockMode.Locked;

        /// 화면을 덮는 창(패배 화면·대화)이 떠 있음: 일시정지 토글을 막는다(Esc 가 그 창의 '그만'·'다음에')
        public static bool Modal;
        /// 연출 중 조작 막음(야차 입장 6초 등): PInput 이 이동·버튼을 넣지 않는다
        public static bool InputLocked;

        public static void TogglePause() { if (Modal && !Paused) return; SetPaused(!Paused); }

        public static void SetPaused(bool p)
        {
            if (Paused == p) return;
            Paused = p;
            TimeFx.SetPaused(p);      // timeScale 은 TimeFx 하나만 쓴다(08 5-2)
            LockCursor(!p);
        }

        /// 실행 인자 -nocursorlock 이면 커서를 잡지 않는다(자동 실행 점검 때 사람 마우스를 뺏지 않게)
        public static readonly bool NoCursorLock = System.Array.Exists(System.Environment.GetCommandLineArgs(),
            a => string.Equals(a, "-nocursorlock", System.StringComparison.OrdinalIgnoreCase));

        /// 배치 실행(테스트)·-nocursorlock 일 때는 건드리지 않는다
        public static void LockCursor(bool on)
        {
            if (Application.isBatchMode || NoCursorLock) return;
            Cursor.lockState = on ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !on;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Paused = false;
            Modal = InputLocked = false;
        }
    }
}
