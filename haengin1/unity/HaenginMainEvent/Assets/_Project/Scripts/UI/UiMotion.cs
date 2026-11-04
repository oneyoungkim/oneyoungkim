// 행인1의 메인이벤트 — B안 움직임(docs/16 5장, 2026-11-05 D8)
//   등장: 판이 켜지면 붙은 쪽에서 10px · 150ms ease-out 으로 미끄러져 들어옴(투명도는 그대로 — 글자 감사·사진이 등장 중에도 판 불투명도를 그대로 잼)
//   사라짐: UiMotion.Hide 로 끄면 100ms 동안 옅어진 뒤 꺼짐(이 부품이 없으면 바로 끔)
//   흔들림 줄이기(Accessibility.Reduced)면 미끄러짐·옅어짐 없이 바로. 시간 = UiKit.RealDt(일시정지·느린 화면에도 같은 빠르기)
//   다른 코드가 그 사이 자리를 옮기면(쌓이는 알림·화면비 배치) 미끄러짐은 거기서 멈춤 — 남의 자리 계산과 싸우지 않음
using UnityEngine;

namespace Haengin
{
    [DisallowMultipleComponent]
    public sealed class UiMotion : MonoBehaviour
    {
        public const float InSecs = 0.15f, OutSecs = 0.10f, Slide = 10f;
        /// 들어오는 방향(시작 자리 = 제자리 − 방향 × 10px). 위에 붙은 판 = (0,−1)이면 위에서 아래로가 아니라 '붙은 쪽에서' 들어오게 (0,1)… 아래 On 이 붙은 쪽으로 정함
        public Vector2 FromDir = new Vector2(0f, -1f);
        RectTransform rt;
        CanvasGroup cg;
        float t = -1f, outT = -1f;
        Vector2 home, written;
        bool sliding;

        /// 판에 붙임. edge = 판이 화면 어느 쪽에 붙어 있나(왼쪽 = (−1,0) · 위 = (0,1) · 아래 = (0,−1) · 오른쪽 = (1,0)) — 그쪽에서 들어옴
        public static UiMotion On(Component c, Vector2 edge)
        {
            if (c == null) return null;
            var m = c.GetComponent<UiMotion>();
            if (m == null) m = c.gameObject.AddComponent<UiMotion>();
            m.FromDir = edge;
            return m;
        }

        /// 100ms 옅어진 뒤 끔(부품이 없거나 흔들림 줄이기면 바로)
        public static void Hide(GameObject go)
        {
            if (go == null || !go.activeSelf) return;
            var m = go.GetComponent<UiMotion>();
            if (m == null || !m.isActiveAndEnabled || Accessibility.Reduced) { go.SetActive(false); return; }
            if (m.outT < 0f) m.outT = 0f;
        }

        /// 사라지는 중
        public bool Leaving => outT >= 0f;
        /// 들어오는 중(시험)
        public bool Entering => t >= 0f;

        void Awake() => rt = transform as RectTransform;

        void OnEnable()
        {
            outT = -1f;
            if (cg != null) cg.alpha = 1f;
            if (rt == null) return;
            home = rt.anchoredPosition;
            sliding = !Accessibility.Reduced;
            t = sliding ? 0f : -1f;
            if (sliding) Apply(0f);
        }

        void OnDisable()
        {
            if (sliding && rt != null && rt.anchoredPosition == written) rt.anchoredPosition = home;
            sliding = false; t = -1f; outT = -1f;
            if (cg != null) cg.alpha = 1f;
        }

        void LateUpdate()
        {
            float dt = UiKit.RealDt;
            if (outT >= 0f)
            {
                if (cg == null) { cg = GetComponent<CanvasGroup>(); if (cg == null) cg = gameObject.AddComponent<CanvasGroup>(); }
                outT += dt;
                cg.alpha = Mathf.Clamp01(1f - outT / OutSecs);
                if (outT >= OutSecs) { outT = -1f; gameObject.SetActive(false); }
                return;
            }
            if (t < 0f) return;
            t += dt;
            float u = Mathf.Clamp01(t / InSecs);
            Apply(u);
            if (u >= 1f) t = -1f;
        }

        void Apply(float u)
        {
            if (!sliding || rt == null) return;
            if (u > 0f && (rt.anchoredPosition - written).sqrMagnitude > 0.01f) { sliding = false; t = -1f; return; }      // 남이 자리를 옮김
            float e = 1f - (1f - u) * (1f - u) * (1f - u);
            written = home + FromDir * (Slide * (1f - e));
            rt.anchoredPosition = written;
            if (u >= 1f) sliding = false;
        }
    }
}
