// 행인1의 메인이벤트 — 기세 액션·벽꽝 표면 (docs/08_M2_전투_설계.md 3-8·8-1·10-2)
// 벽·옹벽·자판기·난간 충돌체에 붙이는 표시(레이어가 아니라 컴포넌트). 성벽에는 붙이지 않는다(03_맵 4장: 성벽 훼손 실격).
// 하체 밀기의 벽꽝(3-4)은 Wall 레이어 또는 이 표시가 있는 면에서 난다. 「벽 러시」(10단계)는 이 표시가 있는 면만.
using UnityEngine;

namespace Haengin
{
    [DisallowMultipleComponent]
    public sealed class HeatSurface : MonoBehaviour
    {
        public enum Kind { Wall }
        public Kind Type = Kind.Wall;

        /// 점에서 dir 쪽으로 dist 안에 기세 표면이 있는가(대상 등 뒤 검사 — 10단계 「벽 러시」 조건)
        public static bool Behind(Vector3 from, Vector3 dir, float dist, out RaycastHit hit)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) { hit = default; return false; }
            var hits = Physics.SphereCastAll(from, 0.15f, dir.normalized, dist, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            hit = default;
            bool found = false;
            foreach (var h in hits)
            {
                if (h.collider.GetComponentInParent<HeatSurface>() == null) continue;
                if (h.distance < best) { best = h.distance; hit = h; found = true; }
            }
            return found;
        }
    }
}
