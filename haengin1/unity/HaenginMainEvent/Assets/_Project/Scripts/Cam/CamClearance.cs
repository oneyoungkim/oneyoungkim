// 행인1의 메인이벤트 — 카메라 벽 간격·다리 가림 (07 문서 4-7 보강, 구현에서 더함)
// CinemachineDeoccluder 는 '따라가는 점(머리)과 카메라 사이를 막는 벽'만 처리한다. 그래서
//  ① 옆으로 스치는 벽에 카메라가 0.05m 까지 붙고(골목 시험에서 확인) ② 등 뒤 2m 펜스가 화면 아래 절반을 덮는 경우(시작 지점)를 못 잡는다.
// 이 확장은 파이프라인 맨 끝(Finalize, Deoccluder 다음)에서 카메라를 머리 쪽으로 당긴다:
//  · 몸: 카메라 구(camRadius + bodyMargin, 기본 0.095m)가 Default·Ground·Wall·CamBlock 에 닿지 않을 때까지
//  · 다리: 무릎(legHeight) → 카메라 선이 Wall·CamBlock 에 막히지 않을 때까지(legMinRadius 까지만)
// 당길 땐 즉시, 풀릴 땐 releaseDamping 으로 천천히. 머리→카메라 선 위에서만 움직이므로 화면 구도(인물 위치)는 그대로다.
// Orbit.Radius 를 건드리지 않아 Deoccluder 의 감쇠 상태와 서로 싸우지 않는다.
using Unity.Cinemachine;
using UnityEngine;

namespace Haengin
{
    [AddComponentMenu("Haengin/Cam Clearance")]
    public sealed class CamClearance : CinemachineExtension
    {
        public CamRig Rig;

        float held, vel;

        /// 이번 프레임 당긴 거리(m). 0 이면 안 당김
        public float Pull { get; private set; }

        protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage,
                                                          ref CameraState state, float deltaTime)
        {
            if (stage != CinemachineCore.Stage.Finalize) return;
            if (Rig == null) Rig = GetComponent<CamRig>();
            if (Rig == null || Rig.CamTarget == null || Rig.Motor == null) { Pull = 0f; return; }
            var t = Rig.T;
            var tgt = Rig.CamTarget.position;
            var v = state.GetCorrectedPosition() - tgt;
            float dist = v.magnitude;
            if (dist < 1e-3f) { Pull = 0f; return; }
            var dir = v / dist;

            float want = dist - Free(tgt, dir, dist, t);              // 당겨야 할 거리
            if (deltaTime < 0f || want >= held) { held = want; vel = 0f; }
            else held = Mathf.SmoothDamp(held, want, ref vel, Mathf.Max(0.01f, t.releaseDamping), Mathf.Infinity, deltaTime);
            held = Mathf.Clamp(held, 0f, Mathf.Max(0f, dist - Mathf.Min(dist, t.bodyMinRadius)));

            Pull = held;
            if (held > 1e-4f) state.PositionCorrection -= dir * held;
        }

        /// dist 에서 0.05m 씩 머리 쪽으로 줄여 가며 처음으로 괜찮은 거리
        float Free(Vector3 tgt, Vector3 dir, float dist, CamTuning t)
        {
            var knee = Rig.Motor.transform.position + Vector3.up * t.legHeight;
            int legMask = Layers.Mask(Layers.Wall, Layers.CamBlock);
            float rad = Mathf.Max(0.02f, t.camRadius + t.bodyMargin);
            for (float d = dist; d > t.bodyMinRadius; d -= 0.05f)
            {
                var cp = tgt + dir * d;
                if (Physics.CheckSphere(cp, rad, Layers.CameraBlock, QueryTriggerInteraction.Ignore)) continue;
                if (t.legCheck && d > t.legMinRadius)
                {
                    var kv = cp - knee;
                    float len = kv.magnitude;
                    if (len > 1e-3f && Physics.SphereCast(knee, 0.05f, kv / len, out _, len, legMask, QueryTriggerInteraction.Ignore)) continue;
                }
                return d;
            }
            return Mathf.Min(dist, t.bodyMinRadius);
        }
    }
}
