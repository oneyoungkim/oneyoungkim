// 행인1의 메인이벤트 — 시간 효과: 히트스톱·슬로·일시정지 (docs/08_M2_전투_설계.md 5-2·10-3)
// Time.timeScale 은 TimeFx 하나만 쓴다. 값 = 일시정지 0 / 히트스톱 0 / 슬로 배율 / 1.
// 히트스톱·슬로 남은 시간은 '실제 시간'으로 줄인다. 실제 시간 = Time.captureDeltaTime(테스트·녹화 고정 프레임) 또는 unscaledDeltaTime, 한 프레임 최대 1/20초.
// 히트스톱 중 새 히트스톱 = 더 긴 쪽. 슬로 중 히트스톱 = 히트스톱 먼저, 끝나면 남은 슬로(슬로 시계는 히트스톱 동안 멈춤 — 시안 frame()).
// 카메라: 전투 중(CameraRealTime)에는 Cinemachine 의 deltaTime 을 실제 시간으로 덮어써서 히트스톱·슬로에도 카메라·흔들림이 돈다
// (CinemachineBrain.IgnoreTimeScale 대신 UniformDeltaTimeOverride — 고정 프레임 테스트에서도 결정적).
using System;
using Unity.Cinemachine;
using UnityEngine;

namespace Haengin
{
    public static class TimeFx
    {
        /// 한 프레임 최대 길이(10-3)
        public const float MaxDt = 1f / 20f;
        const float Eps = 1e-4f;

        static float hitstop, slow, slowScale = 1f;
        static bool paused;

        /// 누적 실제 시간(초) — 입력 버퍼·읽었다 창·흔들림이 쓴다
        public static double Real { get; private set; }
        /// 이번 프레임 실제 시간(초)
        public static float RealDt { get; private set; }
        /// 입력 버퍼 시계(초) = 실제 시간에서 히트스톱·일시정지로 멈춘 프레임만 뺀 것.
        /// 슬로 중에는 실제 시간대로 흐르고(버퍼가 늘어나지 않음), 히트스톱 중에는 멈춘다(히트스톱 중·직전에 누른 것도 남음 — 08 2-2 구현 메모)
        public static double InputClock { get; private set; }
        /// 이번 프레임 게임 시간(초) = deltaTime(1/20 로 자름)
        public static float Dt => Mathf.Min(Time.deltaTime, MaxDt * Time.timeScale);
        public static bool Paused => paused;
        public static bool InHitStop => hitstop > 0f;
        public static bool InSlow => slow > 0f;
        public static float HitStopLeft => hitstop;
        public static float SlowLeft => slow;
        public static float SlowScale => slowScale;
        /// 지금 시간 배율
        public static float Scale => paused ? 0f : hitstop > 0f ? 0f : slow > 0f ? slowScale : 1f;
        /// 전투 카메라가 실제 시간으로 도는가(CombatMode·시험장이 켬)
        public static bool CameraRealTime;
        /// 실제 시간이 지날 때마다(흔들림 감소 등)
        public static event Action<float> RealTick;

        public static void HitStop(float seconds)
        {
            if (seconds > hitstop) hitstop = seconds;
            Apply();
        }

        public static void Slow(float seconds, float scale)
        {
            if (seconds <= 0f) return;
            if (slow <= 0f || seconds >= slow || scale < slowScale) { slow = Mathf.Max(slow, seconds); slowScale = Mathf.Clamp(scale, 0.05f, 1f); }
            Apply();
        }

        public static void SetPaused(bool p)
        {
            paused = p;
            Apply();
        }

        /// 전투 끝·장면 바뀜·테스트 정리: 배율 1, 효과 지움
        public static void Reset()
        {
            hitstop = slow = 0f;
            slowScale = 1f;
            paused = false;
            CameraRealTime = false;
            Shake.Clear();
            CinemachineCore.UniformDeltaTimeOverride = -1f;
            Apply();
        }

        static void Apply() => Time.timeScale = Scale;

        static float FrameReal()
        {
            float d = Time.captureDeltaTime > 0f ? Time.captureDeltaTime : Time.unscaledDeltaTime;
            return Mathf.Clamp(d, 0f, MaxDt);
        }

        internal static void Tick()
        {
            float rdt = FrameReal();
            RealDt = rdt;
            Real += rdt;
            if (Time.timeScale > 0f) InputClock += rdt;      // 이번 프레임이 멈춘 프레임(배율 0 으로 시작)이 아니면
            if (!paused)
            {
                if (hitstop > 0f) { hitstop -= rdt; if (hitstop <= Eps) hitstop = 0f; }
                else if (slow > 0f) { slow -= rdt; if (slow <= Eps) slow = 0f; }
            }
            Apply();
            CinemachineCore.UniformDeltaTimeOverride = CameraRealTime && !paused ? rdt : -1f;
            Shake.Tick(rdt);
            RealTick?.Invoke(rdt);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            hitstop = slow = 0f; slowScale = 1f; paused = false; CameraRealTime = false; Real = 0; InputClock = 0; RealDt = 0f; RealTick = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            if (Runner.Instance != null) return;
            var go = new GameObject("TimeFx") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<Runner>();
        }

        /// 매 프레임 가장 먼저 시간을 센다
        [DefaultExecutionOrder(-1000)]
        public sealed class Runner : MonoBehaviour
        {
            public static Runner Instance;
            void Awake() => Instance = this;
            void OnDestroy() { if (Instance == this) Instance = null; }
            void Update() => Tick();
        }
    }

    /// 시안 트라우마 흔들림·줌 펀치 상태(05_fx.js CamRig). 카메라마다 붙은 TraumaShake 확장이 읽는다.
    /// t 누적(최대 1, 실제 시간 1.8/초 감소), 세기 s = t². 위치 가로 ±0.14·s, 세로 ±0.10·s, 보는 점 가로 ±0.05·s, 기울기 ±0.05·s rad.
    /// 진동 n(k) = sin(61·k·t)·0.6 + sin(37·k·t + 1.3)·0.4 (k: 가로 1 · 세로 1.3 · 기울기 0.9 · 보는 점 0.7).
    public static class Shake
    {
        public static float Trauma { get; private set; }
        public static float Punch { get; private set; }
        public static double Clock { get; private set; }
        public static float Decay = 1.8f, PunchDecay = 5f, ReduceK = 0.3f, PunchAmount = 0.10f;
        public static bool Reduced;

        public static void Add(float tr)
        {
            if (tr <= 0f) return;
            Trauma = Mathf.Min(1f, Trauma + tr * (Reduced ? ReduceK : 1f));
        }

        public static void Kick(float p) => Punch = Mathf.Max(Punch, p);

        public static void Clear() { Trauma = 0f; Punch = 0f; }

        internal static void Tick(float rdt)
        {
            Clock += rdt;
            Trauma = Mathf.Max(0f, Trauma - Decay * rdt);
            Punch *= Mathf.Exp(-PunchDecay * rdt);
            if (Punch < 1e-4f) Punch = 0f;
        }

        public static float N(float k, double clock) => (float)(Math.Sin(clock * 61.0 * k) * 0.6 + Math.Sin(clock * 37.0 * k + 1.3) * 0.4);
        public static float Strength => Trauma * Trauma;
        /// 카메라 기준 위치 흔들림(가로·세로 m)
        public static Vector2 Pos => new Vector2(Strength * 0.14f * N(1f, Clock), Strength * 0.10f * N(1.3f, Clock));
        /// 보는 점 가로 흔들림(m)
        public static float Look => Strength * 0.05f * N(0.7f, Clock);
        /// 기울기(rad)
        public static float Roll => Strength * 0.05f * N(0.9f, Clock);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Trauma = Punch = 0f; Clock = 0; Reduced = false; }
    }
}
