// 행인1의 메인이벤트 — 기세 액션 카메라 CM_Heat (docs/08_M2_전투_설계.md 6-4·5-4)
// 대상 가슴 둘레 반지름 2.6m · FOV 40° · 피치 6° · yaw = 시우→대상 선의 옆(90°) 중 덜 막힌 쪽, 연출 동안 8°/s 로 돎(실제 시간).
// 보는 점 = 시우 가슴 → 대상 가슴 60% 쪽. 마지막 타격(위력 기세)에 집중: 보는 점을 대상 가슴 쪽으로 w × 60%, 거리 × (1 − 0.22w), w = e^(−1.6t).
// 막히면 당기고(0.5m 보다 가까워지면 반대 옆으로 컷). 우선순위 0 ↔ 30, 블렌드는 CombatMode.Blends(들어갈 때 0.15초, 나올 때 0.3초).
// CinemachineCamera 에 몸·조준 부품 없이 이 스크립트가 transform 을 놓는다(+ TraumaShake 확장이 흔들림·줌 펀치).
using Unity.Cinemachine;
using UnityEngine;

namespace Haengin
{
    [DefaultExecutionOrder(60), DisallowMultipleComponent]
    public sealed class HeatCam : MonoBehaviour
    {
        public CinemachineCamera Cam;
        public float Radius = 2.6f, Fov = 40f, Pitch = 6f, Spin = 8f, Priority = 30f;
        [Tooltip("카메라 막는 레이어(Default·Ground·Wall·CamBlock)")] public LayerMask Block = ~0;

        public bool Live { get; private set; }
        public float Side { get; private set; } = 1f;
        public int Cuts { get; private set; }
        Fighter me, tg;
        float yaw, focus, dist;

        void Awake()
        {
            if (Cam == null) Cam = GetComponent<CinemachineCamera>();
            Block = Layers.CameraBlock;
        }

        void OnEnable() { ImpactFx.Fired += OnHit; }
        void OnDisable() { ImpactFx.Fired -= OnHit; }

        public void Begin(Fighter siwoo, Fighter target)
        {
            me = siwoo; tg = target;
            Live = true;
            focus = 0f;
            Cuts = 0;
            var line = HitResolver.Flat(target.Position - siwoo.Position);
            float baseYaw = HitResolver.Yaw(line);
            // 옆 둘 중 덜 막힌 쪽
            float a = Free(baseYaw + 90f), b = Free(baseYaw - 90f);
            Side = a >= b ? 1f : -1f;
            yaw = baseYaw + 90f * Side;
            dist = Radius;
            if (Cam != null)
            {
                var lens = Cam.Lens;
                lens.FieldOfView = Fov;
                Cam.Lens = lens;
                Cam.Priority = (int)Priority;
            }
            Place(0f, true);
        }

        public void End()
        {
            Live = false;
            if (Cam != null) Cam.Priority = 0;
        }

        Vector3 Center => tg != null ? tg.Chest : transform.position;

        /// 그 yaw 로 반지름까지 막히지 않은 거리
        float Free(float y)
        {
            var c = Center;
            var dir = Quaternion.Euler(-Pitch, y, 0f) * Vector3.forward;   // 대상 → 카메라(수평 yaw 쪽, 피치만큼 위)
            if (Physics.SphereCast(c, 0.15f, dir, out var hit, Radius, Block, QueryTriggerInteraction.Ignore)) return hit.distance;
            return Radius;
        }

        void OnHit(HitEvent e, float stop)
        {
            if (!Live || e.Victim != tg || e.Power < Power.Heat) return;
            focus = 1f;
        }

        void Update()
        {
            if (!Live || tg == null || me == null) return;
            Place(TimeFx.RealDt, false);
        }

        void Place(float rdt, bool snap)
        {
            yaw += Spin * Side * rdt;
            focus *= Mathf.Exp(-1.6f * rdt);
            float free = Free(yaw);
            if (free < 0.5f)
            {
                // 반대 옆으로 컷
                Side = -Side;
                yaw += 180f;
                free = Free(yaw);
                Cuts++;
                snap = true;
            }
            float want = Mathf.Min(Radius, free - 0.1f) * (1f - 0.22f * focus);
            dist = snap ? want : Mathf.Lerp(dist, want, 1f - Mathf.Exp(-rdt * 12f));
            var c = Center;
            var rot = Quaternion.Euler(Pitch, yaw + 180f, 0f);
            var pos = c - rot * Vector3.forward * dist;
            var look = Vector3.Lerp(me.Chest, tg.Chest, 0.6f);
            look = Vector3.Lerp(look, tg.Chest, 0.6f * focus);
            transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos));
        }
    }
}
