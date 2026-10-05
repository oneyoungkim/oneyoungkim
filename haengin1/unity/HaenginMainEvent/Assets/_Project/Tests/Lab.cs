// 행인1의 메인이벤트 — PlayMode 테스트 공용 도구: 빈 장면, 코드로 만든 지형, 캡슐 리그, 시간 고정
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Haengin.Tests
{
    public static class Lab
    {
        public const float Dt = 1f / 60f;
        static int sceneNo;

        /// 새 빈 장면을 만들어 활성화하고, 테스트가 전에 만든·연 장면(Lab_*, Zone1)은 내린다.
        /// 테스트 실행기(PlaymodeTestsController)가 들어 있는 처음 장면(InitTestScene…)은 절대 내리지 않는다 — 내리면 실행이 멈춘다.
        public static IEnumerator FreshScene()
        {
            GameState.SetPaused(false);
            var s = SceneManager.CreateScene("Lab_" + (++sceneNo));
            SceneManager.SetActiveScene(s);
            yield return UnloadOurs(s);
            yield return null;
        }

        /// 테스트가 만든·연 장면만 내린다(keep 은 남김) — Lab_*, Zone1, CombatLab(M2 녹화)
        public static IEnumerator UnloadOurs(Scene keep)
        {
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var o = SceneManager.GetSceneAt(i);
                if (o == keep || !o.isLoaded) continue;
                if (o.name.StartsWith("Lab_") || o.name == "Zone1" || o.name == "CombatLab") yield return SceneManager.UnloadSceneAsync(o);
            }
        }

        public static GameObject Box(string name, Vector3 center, Vector3 size, Quaternion rot, int layer)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.layer = layer;
            go.transform.SetPositionAndRotation(center, rot);
            go.transform.localScale = size;
            return go;
        }

        /// 윗면이 y = top 인 평평한 바닥
        public static GameObject Floor(float top = 0f, float size = 80f) =>
            Box("Floor", new Vector3(0f, top - 0.5f, 0f), new Vector3(size, 1f, size), Quaternion.identity, Layers.Ground);

        /// 바닥(y=0)에서 +Z 쪽으로 angle 도로 올라가는 경사판. 아래 끝 z = z0, 윗면이 바닥과 이어진다
        public static GameObject Ramp(float x, float z0, float angle, float length = 8f, float width = 3f)
        {
            var rot = Quaternion.Euler(-angle, 0f, 0f);       // +Z 쪽이 올라간다
            var dir = rot * Vector3.forward;
            var n = rot * Vector3.up;
            var center = new Vector3(x, 0f, z0) + dir * (length / 2f) - n * 0.25f;
            return Box($"Ramp{angle}", center, new Vector3(width, 0.5f, length), rot, Layers.Ground);
        }

        /// 캡슐 모양 시우(1.74m) + 카메라 리그. 입력 에셋 없이(테스트가 SetMoveInput 으로 넣는다)
        public static RigFactory.Rig Rig(Vector3 feet, float yaw, MoveTuning mt = null, CamTuning ct = null)
        {
            mt = mt != null ? mt : ScriptableObject.CreateInstance<MoveTuning>();
            ct = ct != null ? ct : ScriptableObject.CreateInstance<CamTuning>();
            var cap = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            cap.name = "CapsuleSiwoo";
            Object.DestroyImmediate(cap.GetComponent<Collider>());
            return RigFactory.Build(feet, yaw, mt, ct, cap, null);
        }

        public static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        public static IEnumerator Seconds(float s) => Frames(Mathf.RoundToInt(s / Dt));

        /// 매 프레임 같은 입력을 넣으며 s 초
        public static IEnumerator Hold(PlayerMotor m, Vector3 dir, float s, bool run, System.Action perFrame = null)
        {
            int n = Mathf.RoundToInt(s / Dt);
            for (int i = 0; i < n; i++)
            {
                m.SetMoveInput(dir, 1f, run);
                yield return null;
                perFrame?.Invoke();
            }
        }

        public static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
