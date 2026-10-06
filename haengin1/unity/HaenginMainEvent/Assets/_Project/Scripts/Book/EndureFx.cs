// 행인1의 메인이벤트 — '참기' 연출(docs/09_M3_버티컬슬라이스_설계.md 2-3): 화면 가장자리 붉은 비네팅(#7A1E1E, 0 → 0.45, 맥박 0.9초)
// + 패드 진동(약하게 0.2초 × 2, 설정 haengin.rumble 끔 가능) + 환경음 30% 로 낮춤(Ambience.Duck — 소리 자리).
// 6화 점화(2-12)는 같은 비네팅을 주황(#E2582C)으로 바꾸며 걷는다(ToOrangeAndClear).
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Haengin
{
    /// 환경음 크기 배율(소리 자리 — 17단계 환경음이 읽는다)
    public static class Ambience
    {
        public static float Duck = 1f;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Duck = 1f; }
    }

    [DisallowMultipleComponent]
    public sealed class EndureFx : MonoBehaviour
    {
        public const string RumbleKey = "haengin.rumble";
        public const float MaxAlpha = 0.45f, Pulse = 0.9f;

        public bool On { get; private set; }
        public float Alpha => img != null && img.gameObject.activeSelf ? img.color.a : 0f;
        public Color Tint { get; private set; } = UiKit.Endure;
        public int Rumbles { get; private set; }
        public int Shown { get; private set; }

        RawImage img;
        float t, level;
        Coroutine rumble;

        void Build()
        {
            if (img != null) return;
            UiKit.Canvas(gameObject, 35);
            var n = UiKit.Node("참기 비네팅", transform);
            UiKit.Stretch(n);
            img = n.gameObject.AddComponent<RawImage>();
            img.texture = Vignette(256);
            img.raycastTarget = false;
            img.color = new Color(1f, 1f, 1f, 0f);
            img.gameObject.SetActive(false);
        }

        void Awake() => Build();

        public void Begin()
        {
            Build();
            if (!On) Shown++;
            On = true;
            Tint = UiKit.Endure;
            img.gameObject.SetActive(true);
            Ambience.Duck = 0.3f;
            if (PlayerPrefs.GetInt(RumbleKey, 1) == 1 && Gamepad.current != null)
            {
                if (rumble != null) StopCoroutine(rumble);
                rumble = StartCoroutine(Rumble());
            }
        }

        public void End()
        {
            On = false;
            Ambience.Duck = 1f;
        }

        /// 즉시 끔(건너뛰기·장면 끝)
        public void Clear()
        {
            Build();
            On = false; level = 0f;
            Ambience.Duck = 1f;
            img.gameObject.SetActive(false);
            if (Gamepad.current != null) Gamepad.current.SetMotorSpeeds(0f, 0f);
        }

        /// 점화(2-12 7.5초): 붉은색 → 주황으로 바뀌며 가장자리에서 걷힘
        public IEnumerator ToOrangeAndClear(float secs)
        {
            Build();
            img.gameObject.SetActive(true);
            On = false;
            float u = 0f;
            while (u < secs)
            {
                u += UiKit.RealDt;
                float k = Mathf.Clamp01(u / secs);
                Tint = Color.Lerp(UiKit.Endure, UiKit.Accent, Mathf.Clamp01(k * 2f));
                level = MaxAlpha * (1f - Mathf.Clamp01((k - 0.4f) / 0.6f));
                img.color = new Color(Tint.r, Tint.g, Tint.b, level);
                yield return null;
            }
            Clear();
        }

        IEnumerator Rumble()
        {
            for (int i = 0; i < 2; i++)
            {
                Rumbles++;
                Gamepad.current?.SetMotorSpeeds(0.15f, 0.25f);
                float u = 0f; while (u < 0.2f) { u += UiKit.RealDt; yield return null; }
                Gamepad.current?.SetMotorSpeeds(0f, 0f);
                u = 0f; while (u < 0.25f) { u += UiKit.RealDt; yield return null; }
            }
            rumble = null;
        }

        void LateUpdate()
        {
            if (img == null || !img.gameObject.activeSelf) return;
            float rdt = UiKit.RealDt;
            t += rdt;
            if (On) level = Mathf.MoveTowards(level, 1f, rdt / 0.35f);
            else if (Tint == UiKit.Endure) { level = Mathf.MoveTowards(level, 0f, rdt / 0.5f); if (level <= 0f) { img.gameObject.SetActive(false); return; } }
            else return;   // 점화 연출이 직접 그림
            float pulse = 0.82f + 0.18f * Mathf.Sin(t * Mathf.PI * 2f / Pulse);
            img.color = new Color(Tint.r, Tint.g, Tint.b, MaxAlpha * level * pulse);
        }

        /// 가장자리만 짙은 동그란 비네팅(가운데 투명)
        static Texture2D Vignette(int n)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Vignette", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Sqrt(u * u * 0.8f + v * v * 1.1f);
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1.0f, d));      // 가장자리 가운데도 거의 다 차게(처음 값은 너무 옅었음)
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }
    }
}
