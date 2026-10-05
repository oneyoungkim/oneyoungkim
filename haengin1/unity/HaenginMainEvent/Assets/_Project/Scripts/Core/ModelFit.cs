// 행인1의 메인이벤트 — 모델 맞추기: 키를 맞추고 발을 부모 원점 높이에, 수평 중심을 부모 원점에 (SandboxSetup.FitToGround 와 같은 계산)
using UnityEngine;

namespace Haengin
{
    public static class ModelFit
    {
        public static Bounds Fit(Transform model, float height)
        {
            var parent = model.parent;
            Vector3 origin = parent != null ? parent.position : Vector3.zero;
            var b = WorldBounds(model);
            if (height > 0f && b.size.y > 1e-4f)
            {
                model.localScale *= height / b.size.y;
                b = WorldBounds(model);
            }
            model.position += new Vector3(origin.x - b.center.x, origin.y - b.min.y, origin.z - b.center.z);
            return WorldBounds(model);
        }

        public static Bounds WorldBounds(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(t.position, Vector3.zero);
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }
    }
}
