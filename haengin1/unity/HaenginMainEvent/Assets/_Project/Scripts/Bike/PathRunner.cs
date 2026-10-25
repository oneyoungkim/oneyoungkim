// 행인1의 메인이벤트 — 길 따라 달리는 사람(docs/09 2-6 '태오'): 태오(정식 모델 프리팹)가 길 가운데선(옆으로 Offset 만큼)을 따라 달린다.
// M1 PathWalker 방식: 경로 = 점 목록, 진행 거리 S(m)를 속도만큼 늘리고 그 자리 땅에 붙인다(광선). 동작은 시우 컨트롤러의 블렌드 트리(Blend 2 = 달리기)를
// 태오 아바타에 Humanoid 로(Anim/TaeoRun.overrideController — 대기만 태오 Idle_3). 재생 배율 = 속도 ÷ 컨트롤러 달리기 속도.
// 말풍선 '…' (0.8초) · 끝에서 숨 고르기(대기).
using TMPro;
using UnityEngine;

namespace Haengin
{
    [DefaultExecutionOrder(-10), DisallowMultipleComponent]
    public sealed class PathRunner : MonoBehaviour
    {
        public Vector3[] Path = new Vector3[0];
        [Tooltip("진행 방향 왼쪽(+)으로 비켜 달리기(m)")] public float Offset = 0.9f;
        public float Speed;
        [Tooltip("컨트롤러 달리기·걷기 고유 속도(배율 계산)")] public float RunClip = 4.5f, WalkClip = 1.4f;
        public TextMeshPro Bubble;
        public float S { get; private set; }
        public float Length { get; private set; }
        public bool Running => Speed > 0.05f;
        public bool AtEnd => S >= Length - 0.01f;
        public int Bubbles { get; private set; }

        Animator anim;
        float[] cum = new float[0];
        float bubbleLeft;
        static readonly int HBlend = Animator.StringToHash("Blend"), HRate = Animator.StringToHash("Rate"), HSpeed = Animator.StringToHash("Speed");

        void Awake() { anim = GetComponentInChildren<Animator>(); Build(); if (Bubble != null) Bubble.gameObject.SetActive(false); }

        public void Build()
        {
            cum = new float[Path.Length];
            float L = 0f;
            for (int i = 1; i < Path.Length; i++) { L += Flat(Path[i] - Path[i - 1]).magnitude; cum[i] = L; }
            Length = L;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        /// 경로 위 s 자리(가운데선)와 방향
        void Ensure() { if (cum == null || cum.Length != Path.Length) Build(); }

        public Vector3 At(float s, out Vector3 dir)
        {
            Ensure();
            dir = Vector3.forward;
            if (Path.Length == 0) return transform.position;
            if (Path.Length == 1) return Path[0];
            s = Mathf.Clamp(s, 0f, Length);
            int i = 1;
            while (i < Path.Length - 1 && cum[i] < s) i++;
            float seg = cum[i] - cum[i - 1];
            float t = seg > 1e-4f ? (s - cum[i - 1]) / seg : 0f;
            dir = Flat(Path[i] - Path[i - 1]).normalized;
            return Vector3.Lerp(Path[i - 1], Path[i], t);
        }

        /// 점 p 를 경로에 투영한 진행 거리
        public float Project(Vector3 p)
        {
            Ensure();
            float best = float.MaxValue, bs = 0f;
            for (int i = 1; i < Path.Length; i++)
            {
                var a = Flat(Path[i - 1]); var b = Flat(Path[i]); var q = Flat(p);
                var ab = b - a;
                float t = Mathf.Clamp01(Vector3.Dot(q - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                float d = (a + ab * t - q).sqrMagnitude;
                if (d < best) { best = d; bs = cum[i - 1] + t * Mathf.Sqrt(ab.sqrMagnitude); }
            }
            return bs;
        }

        public void Place(float s)
        {
            Ensure();
            S = Mathf.Clamp(s, 0f, Length);
            Snap();
        }

        public void Say(string text, float secs = 0.8f)
        {
            if (Bubble == null) return;
            Bubble.text = text;
            Bubble.gameObject.SetActive(true);
            bubbleLeft = secs;
            Bubbles++;
        }

        void Snap()
        {
            var c = At(S, out var dir);
            var left = new Vector3(-dir.z, 0f, dir.x);
            var p = c + left * Offset;
            if (Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out var hit, 8f, Layers.Solid, QueryTriggerInteraction.Ignore)) p.y = hit.point.y;
            float sgn = Speed >= 0f ? 1f : -1f;
            transform.SetPositionAndRotation(p, Quaternion.LookRotation(dir * sgn, Vector3.up));
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (Path.Length >= 2)
            {
                S = Mathf.Clamp(S + Speed * dt, 0f, Length);
                if ((S <= 0f && Speed < 0f) || (S >= Length && Speed > 0f)) Speed = 0f;
                Snap();
            }
            if (anim != null)
            {
                float v = Mathf.Abs(Speed);
                float blend = v < 0.05f ? 0f : v <= WalkClip ? 1f : 2f;
                float rate = v < 0.05f ? 1f : v <= WalkClip ? v / WalkClip : v / RunClip;
                anim.SetFloat(HBlend, blend, 0.15f, dt);
                anim.SetFloat(HRate, Mathf.Max(0.3f, rate));
                anim.SetFloat(HSpeed, v);
            }
            if (bubbleLeft > 0f)
            {
                bubbleLeft -= dt;
                if (Bubble != null)
                {
                    var cam = Camera.main;
                    if (cam != null) Bubble.transform.rotation = Quaternion.LookRotation(Bubble.transform.position - cam.transform.position, Vector3.up);
                    if (bubbleLeft <= 0f) Bubble.gameObject.SetActive(false);
                }
            }
        }
    }
}
