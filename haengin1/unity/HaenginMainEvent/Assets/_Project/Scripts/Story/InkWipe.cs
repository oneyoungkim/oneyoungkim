// 행인1의 메인이벤트 — 장면 전환 먹 닦기(docs/09_M3_버티컬슬라이스_설계.md 2-1): 붓 획이 왼쪽에서 화면을 덮었다가(0.25초) 오른쪽으로 걷힘(0.25초).
// 덮인 동안 SceneLoader 가 장면을 열고 시우·카메라를 옮긴다. 실제 시간(TimeFx.RealDt)으로 움직인다(일시정지·슬로와 무관).
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Haengin
{
    [DisallowMultipleComponent]
    public sealed class InkWipe : MonoBehaviour
    {
        public float Half = 0.25f;
        /// 0 = 걷힘(왼쪽 밖) · 1 = 덮음 · 2 = 걷힘(오른쪽 밖)
        public float Phase { get; private set; }
        public bool Covered => Phase > 0.999f && Phase < 1.001f;
        public bool Visible => band != null && band.gameObject.activeSelf;
        public int Count { get; private set; }

        RawImage band;
        RectTransform rt;

        void Build()
        {
            if (band != null) return;
            UiKit.Canvas(gameObject, 60);
            var n = UiKit.Node("먹 닦기", transform);
            rt = n;
            band = n.gameObject.AddComponent<RawImage>();
            band.texture = UiKit.BrushBand(512, 96, 7, 0.1f);
            band.raycastTarget = false;
            rt.anchorMin = new Vector2(0.5f, 0f); rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            band.gameObject.SetActive(false);
        }

        float Width => ((RectTransform)transform).rect.width;

        void Set(float phase)
        {
            Build();
            Phase = phase;
            float w = Mathf.Max(1f, Width);
            float bw = w * 1.3f;
            rt.sizeDelta = new Vector2(bw, 40f);
            // 가운데 x: 0 → −(w/2 + bw/2), 1 → 0, 2 → +(w/2 + bw/2)
            float off = w * 0.5f + bw * 0.5f;
            rt.anchoredPosition = new Vector2(Mathf.Lerp(-off, off, phase * 0.5f), 0f);
            bool on = phase > 0.001f && phase < 1.999f;
            if (band.gameObject.activeSelf != on) band.gameObject.SetActive(on);
        }

        public IEnumerator Cover()
        {
            Count++;
            float t = 0f;
            Set(0f);
            while (t < Half) { t += UiKit.RealDt; Set(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / Half))); yield return null; }
            Set(1f);
        }

        public IEnumerator Uncover()
        {
            float t = 0f;
            while (t < Half) { t += UiKit.RealDt; Set(1f + Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / Half))); yield return null; }
            Set(2f);
        }

        public void Clear() { Build(); Set(0f); }

        void Awake() => Build();
    }
}
