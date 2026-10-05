// 행인1의 메인이벤트 — M1 화면 글자(IMGUI): 조작 안내, F1 디버그 정보, 일시정지 메뉴, 짧은 알림
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Haengin
{
    public sealed class DebugHud : MonoBehaviour
    {
        public PlayerMotor Motor;
        public CamRig Cam;

        static bool show;
        static string toast;
        static float toastUntil;
        float fps, fpsTimer;
        int frames;
        GUIStyle small, big, box;

        public static void Toggle() => show = !show;

        public static void Toast(string msg, float seconds = 2f)
        {
            toast = msg;
            toastUntil = Time.unscaledTime + seconds;
            Debug.Log("[M1] " + msg);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { show = false; toast = null; toastUntil = 0f; }

        IEnumerator Start()
        {
            if (Motor == null) Motor = FindAnyObjectByType<PlayerMotor>();
            if (Cam == null) Cam = FindAnyObjectByType<CamRig>();
            // 실행 확인 한 줄(Player.log): 장면이 뜨고 시우가 땅에 서 있는지
            yield return new WaitForSecondsRealtime(3f);
            if (Motor != null)
            {
                var p = Motor.Position;
                Debug.Log($"[M1] 실행 확인: 장면 {SceneManager.GetActiveScene().name} · 시우 ({p.x:F1}, {p.y:F2}, {p.z:F1}) 접지 {Motor.Grounded} 상태 {Motor.State} · " +
                          $"카메라 거리 {(Cam != null ? Cam.Distance : 0f):F2}m · {fps:F0} fps · 화면 {Screen.width}x{Screen.height}");
            }
            else Debug.LogWarning("[M1] 실행 확인: 장면에 PlayerMotor 가 없음");
        }

        void Update()
        {
            frames++;
            fpsTimer += Time.unscaledDeltaTime;
            if (fpsTimer >= 0.5f) { fps = frames / fpsTimer; frames = 0; fpsTimer = 0f; }
        }

        void Styles()
        {
            if (small != null) return;
            small = new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true };
            small.normal.textColor = new Color(0.10f, 0.08f, 0.09f);
            big = new GUIStyle(GUI.skin.label) { fontSize = 34, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            big.normal.textColor = Color.white;
            box = new GUIStyle(GUI.skin.box) { fontSize = 15, alignment = TextAnchor.UpperLeft, richText = true, padding = new RectOffset(10, 10, 8, 8) };
            box.normal.textColor = Color.white;
        }

        void OnGUI()
        {
            Styles();
            float s = Mathf.Max(1f, Screen.height / 1080f);
            var m = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            float w = Screen.width / s, h = Screen.height / s;

            // 조작 안내(왼쪽 아래)
            GUI.Label(new Rect(16, h - 30, w - 32, 24),
                "이동 WASD·왼스틱   달리기 Shift·R2   카메라 마우스·오른스틱   카메라 정렬 Q·L1   일시정지 Esc·Options   정보 F1", small);

            if (show && Motor != null)
            {
                string cam = Cam != null
                    ? $"카메라 yaw {Cam.Yaw:F0}° pitch {Cam.Pitch:F0}° 거리 {Cam.Distance:F2}m FOV {Cam.Cam.Lens.FieldOfView:F1}° 당김 {Cam.Pull:F2}m"
                    : "카메라 없음";
                var p = Motor.Position;
                GUI.Box(new Rect(16, 16, 560, 128),
                    $"상태 {Motor.State}   속도 {Motor.PlanarSpeed:F2} m/s   접지 {(Motor.Grounded ? "예" : "아니오")}   경사 {Motor.GroundAngle:F0}°\n" +
                    $"위치 ({p.x:F1}, {p.y:F2}, {p.z:F1})   방향 {Motor.Yaw:F0}°\n{cam}\n{fps:F0} fps", box);
            }

            if (!string.IsNullOrEmpty(toast) && Time.unscaledTime < toastUntil)
                GUI.Box(new Rect(w / 2 - 220, h - 110, 440, 36), toast, box);

            if (GameState.Paused)
            {
                GUI.color = new Color(0f, 0f, 0f, 0.55f);
                GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(0, h / 2 - 120, w, 60), "일시정지", big);
                if (GUI.Button(new Rect(w / 2 - 110, h / 2 - 30, 220, 44), "계속 (Esc)")) GameState.SetPaused(false);
                if (GUI.Button(new Rect(w / 2 - 110, h / 2 + 26, 220, 44), "끝내기")) Application.Quit();
            }
            GUI.matrix = m;
        }
    }
}
