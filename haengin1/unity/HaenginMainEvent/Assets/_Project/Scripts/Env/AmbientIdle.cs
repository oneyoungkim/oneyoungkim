// 행인1의 메인이벤트 — 거리의 작은 움직임(맵 아트 패스 5차 2026-10-25): 원통 입간판 돌기 · 비둘기 쪼기/몸 돌리기 · 고양이 숨쉬기.
// 실행 중에만 움직인다(편집 모드 장면 저장값은 그대로 — 결정적 저장). 한 프레임 비용은 Transform 한두 개 대입.
using UnityEngine;

namespace Haengin
{
    [DisallowMultipleComponent]
    public sealed class AmbientIdle : MonoBehaviour
    {
        public enum Mode { Spin, Peck, Breathe }
        public Mode Kind;
        public float Speed = 1f;
        public Transform Head;          // 비둘기: 쪼는 머리

        Quaternion baseRot, headRot;
        Vector3 baseScale;
        float seed, nextTurn, yaw;

        void Start()
        {
            baseRot = transform.localRotation; baseScale = transform.localScale;
            if (Head != null) headRot = Head.localRotation;
            seed = (transform.position.x * 13.1f + transform.position.z * 7.7f) % 10f;
            nextTurn = 2f + seed % 3f;
        }

        void Update()
        {
            if (!Application.isPlaying) return;
            float t = Time.time;
            switch (Kind)
            {
                case Mode.Spin:
                    transform.localRotation = baseRot * Quaternion.Euler(0f, t * Speed, 0f);
                    break;
                case Mode.Peck:
                    if (Head != null)
                    {
                        float ph = Mathf.Repeat(t * Speed + seed, 3.2f);
                        float peck = ph < 0.9f ? Mathf.Sin(ph / 0.9f * Mathf.PI * 3f) * 0.5f + 0.5f : 0f;   // 세 번 콕콕
                        Head.localRotation = headRot * Quaternion.Euler(peck * 55f, 0f, 0f);
                    }
                    if (t > nextTurn) { yaw += (seed % 2f > 1f ? 1f : -1f) * (30f + 40f * Mathf.PerlinNoise(t, seed)); nextTurn = t + 2.5f + 3f * Mathf.PerlinNoise(seed, t); }
                    transform.localRotation = Quaternion.Slerp(transform.localRotation, baseRot * Quaternion.Euler(0f, yaw, 0f), Time.deltaTime * 4f);
                    break;
                case Mode.Breathe:
                    float b = 1f + Mathf.Sin(t * Speed + seed) * 0.02f;
                    transform.localScale = new Vector3(baseScale.x * (2f - b), baseScale.y * b, baseScale.z);
                    break;
            }
        }
    }
}
