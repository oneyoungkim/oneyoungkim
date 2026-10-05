// 행인1의 메인이벤트 — 일시정지·커서 (M1 은 자리만: 시간 정지 + 커서 풀기)
using UnityEngine;

namespace Haengin
{
    public static class GameState
    {
        public static bool Paused { get; private set; }
        public static bool CursorLocked => Cursor.lockState == CursorLockMode.Locked;

        public static void TogglePause() => SetPaused(!Paused);

        public static void SetPaused(bool p)
        {
            if (Paused == p) return;
            Paused = p;
            Time.timeScale = p ? 0f : 1f;
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
        }
    }
}
