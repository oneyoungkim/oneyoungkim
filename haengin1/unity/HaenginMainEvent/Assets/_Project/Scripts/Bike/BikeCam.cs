// 행인1의 메인이벤트 — CM_Bike(docs/09 2-6 '카메라'): 자전거 등 뒤 거리 4.6m · 따라가는 점 1.5m · 피치 10° · FOV 50°(속도 따라 56°까지).
// 손으로 돌린 시점은 1.0초 뒤 등 뒤로 돌아온다. 측면 트래킹(프리셋 ⑥ — 오르막 마지막 구간 태오 추월): 길 옆 4.2m·높이 1.3m 에서 자전거를 따라가며 봄, 0.6초 블렌드.
// 벽·담: 따라가는 점 → 카메라 선을 구(0.15m)로 쏴 막히면 당김(즉시), 풀리면 0.4초. 국물 출렁임(SoupGauge.Shake)만큼 흔들림.
// 자리를 매 프레임 코드로 계산(Cinemachine 몸·조준 부품 없음 — CutCamDriver 와 같은 방식, 결정적).
using Unity.Cinemachine;
using UnityEngine;

namespace Haengin
{
    [DefaultExecutionOrder(90), DisallowMultipleComponent]
    public sealed class BikeCam : MonoBehaviour
    {
        public const int LivePriority = 25;
        public BikeMotor Bike;
        public SoupGauge Soup;
        public CinemachineCamera Cam;
        public float Distance = 4.6f, TargetHeight = 1.5f, Pitch = 10f, Fov = 50f, FovFast = 56f, RecenterWait = 1.0f;
        public float SideOffset = 4.2f, SideHeight = 1.3f, SideBlend = 0.6f;

        /// 측면 트래킹 켬(코스가 켬) · 지금 블렌드(0 = 등 뒤, 1 = 측면)
        public bool Side;
        public Vector3 SideRight = Vector3.right;
        public float SideW { get; private set; }
        public bool Live => Cam != null && Cam.Priority == LivePriority;
        public float CurrentDistance { get; private set; }
        public float YawOffset => yawOff;

        float yawOff, pitchOff, lookIdle = 9f, pull = 9f, shakeT;

        public void SetLive(bool on)
        {
            if (Cam != null) Cam.Priority = on ? LivePriority : 0;
            if (on) Snap();
        }

        /// 손으로 돌림(도)
        public void Look(Vector2 d)
        {
            if (d.sqrMagnitude < 1e-6f) return;
            yawOff = Mathf.Clamp(yawOff + d.x, -150f, 150f);
            pitchOff = Mathf.Clamp(pitchOff - d.y, -8f, 30f);
            lookIdle = 0f;
        }

        public void Snap() { yawOff = pitchOff = 0f; SideW = Side ? 1f : 0f; pull = 9f; Place(0f, true); }

        void LateUpdate() => Place(Time.deltaTime, false);

        void Place(float dt, bool snap)
        {
            if (Bike == null || Cam == null) return;
            lookIdle += dt;
            if (lookIdle > RecenterWait)
            {
                yawOff = Mathf.MoveTowards(yawOff, 0f, 120f * dt);
                pitchOff = Mathf.MoveTowards(pitchOff, 0f, 40f * dt);
            }
            SideW = snap ? (Side ? 1f : 0f) : Mathf.MoveTowards(SideW, Side ? 1f : 0f, dt / Mathf.Max(0.01f, SideBlend));
            float w = SideW * SideW * (3f - 2f * SideW);
            var feet = Bike.Position;
            var target = feet + Vector3.up * TargetHeight;
            // 등 뒤
            var rot = Quaternion.Euler(Pitch + pitchOff, Bike.Yaw + yawOff, 0f);
            var back = target - rot * Vector3.forward * Distance;
            // 측면
            var side = feet + SideRight.normalized * SideOffset + Vector3.up * SideHeight - Bike.Forward * 0.8f;
            var want = Vector3.Lerp(back, side, w);
            var look = Vector3.Lerp(target, feet + Vector3.up * 1.1f + Bike.Forward * 0.6f, w);
            // 벽 당김
            var dir = want - target;
            float len = dir.magnitude;
            float ok = len;
            if (len > 0.01f && Physics.SphereCast(target, 0.15f, dir / len, out var hit, len, Layers.Solid & ~(1 << Layers.Player), QueryTriggerInteraction.Ignore))
                ok = Mathf.Max(0.8f, hit.distance - 0.05f);
            pull = ok < pull || snap ? ok : Mathf.MoveTowards(pull, ok, len / 0.4f * Mathf.Max(dt, 1e-4f));
            pull = Mathf.Min(pull, len);
            var pos = target + (len > 0.01f ? dir / len : Vector3.back) * pull;
            CurrentDistance = (pos - target).magnitude;
            // 국물 출렁임 흔들림
            shakeT += dt;
            float sh = Soup != null ? Soup.Shake * (Accessibility.Reduced ? 0.3f : 1f) : 0f;
            if (sh > 0.001f) pos += new Vector3(Mathf.PerlinNoise(shakeT * 7f, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, shakeT * 7f) - 0.5f, 0f) * 0.12f * sh;
            transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos, Vector3.up));
            var lens = Cam.Lens;
            lens.FieldOfView = Mathf.Lerp(Fov, FovFast, Mathf.Clamp01((Mathf.Abs(Bike.Speed) - 2f) / 4f));
            Cam.Lens = lens;
        }
    }
}
