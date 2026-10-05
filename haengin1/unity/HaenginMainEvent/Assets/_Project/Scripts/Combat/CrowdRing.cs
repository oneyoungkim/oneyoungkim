// 행인1의 메인이벤트 — 구경꾼 원 (docs/08_M2_전투_설계.md 3-8 ②·8-2·10-2)
// 구경꾼이 선 자리(원 전체 또는 호 구간). 「구경꾼 되받기」(10단계) 조건 = 대상 등 뒤 1.0m 안에 이 구간.
// 구경꾼 실루엣은 레이어 Crowd(13): 플레이어·적은 통과(링 벽 PlayerOnly 가 막음), 카메라는 무시.
using UnityEngine;

namespace Haengin
{
    public sealed class CrowdRing : MonoBehaviour
    {
        [Tooltip("구경꾼이 선 반경 m(원 중심 = 이 오브젝트)")] public float Radius = 6.0f;
        [Tooltip("구간 시작·끝 각도(°, 북 = 0, 시계 방향). 둘 다 0 이면 원 전체")] public float FromDeg, ToDeg;

        public bool Full => Mathf.Approximately(FromDeg, ToDeg);

        /// 점이 구경꾼 줄에서 dist 안인가(수평)
        public bool Near(Vector3 p, float dist)
        {
            var d = p - transform.position;
            d.y = 0f;
            float r = d.magnitude;
            if (Mathf.Abs(r - Radius) > dist) return false;
            if (Full) return true;
            float a = Mathf.Repeat(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, 360f);
            float f = Mathf.Repeat(FromDeg, 360f), t = Mathf.Repeat(ToDeg, 360f);
            return f <= t ? a >= f && a <= t : a >= f || a <= t;
        }

        /// 점에서 dir 쪽으로 dist 안에 구경꾼 줄이 있는가
        public bool Behind(Vector3 from, Vector3 dir, float dist)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) return false;
            dir.Normalize();
            for (float s = 0f; s <= dist + 1e-3f; s += 0.1f)
                if (Near(from + dir * s, 0.15f)) return true;
            return false;
        }
    }
}
