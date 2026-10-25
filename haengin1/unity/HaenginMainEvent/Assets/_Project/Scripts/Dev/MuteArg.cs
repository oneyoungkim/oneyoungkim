// 행인1의 메인이벤트 — 빌드 실행 인자 -mute: 시작부터 끝까지 소리 0(AudioListener.volume = 0 + 모든 AudioSource.mute). 대표 PC 에서 그래픽 켠 채 플레이 QA 할 때.
// 장면이 바뀌어도(Additive 무대·체육관) 새로 생긴 소리까지 매 프레임 끈다(숨은 DontDestroyOnLoad 하나).
using System;
using UnityEngine;

namespace Haengin
{
    public sealed class MuteArg : MonoBehaviour
    {
        public static bool On { get; private set; }
        static MuteArg inst;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            On = false;
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-mute") >= 0) Apply();
        }

        /// 음소거 켬(시험은 직접 부름)
        public static void Apply()
        {
            On = true;
            AudioListener.volume = 0f;
            if (inst == null)
            {
                var go = new GameObject("음소거(-mute)") { hideFlags = HideFlags.HideInHierarchy };
                DontDestroyOnLoad(go);
                inst = go.AddComponent<MuteArg>();
            }
            MuteAll();
            Debug.Log("[Mute] -mute: 소리 0");
        }

        /// 시험 정리용
        public static void Release()
        {
            On = false;
            AudioListener.volume = 1f;
            if (inst != null) { Destroy(inst.gameObject); inst = null; }
            foreach (var s in FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (s != null) s.mute = false;
        }

        static void MuteAll()
        {
            foreach (var s in FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (s != null) s.mute = true;
        }

        float left;

        void LateUpdate()
        {
            if (!On) return;
            if (AudioListener.volume != 0f) AudioListener.volume = 0f;
            left -= Time.unscaledDeltaTime;
            if (left > 0f) return;
            left = 0.25f;
            MuteAll();
        }
    }
}
