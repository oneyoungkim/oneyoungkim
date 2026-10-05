// 행인1의 메인이벤트 — 전투 연습장 배치 (docs/08_M2_전투_설계.md 1-2·10-5: 평지 20×20 · 벽 1면 · 구경꾼 원 반경 6.0 · 허수아비)
// 에디터(CombatSetup → Scenes/CombatLab.unity)와 PlayMode 테스트가 같은 함수로 만든다.
// 좌표: 원점 = 연습장 가운데, +Z = 북. 벽(옹벽 느낌, Wall + HeatSurface) = 북쪽 z 8.0~8.5, 구경꾼 원 = 원점 반경 6.0(12명, Crowd 레이어).
using UnityEngine;

namespace Haengin
{
    public static class CombatLab
    {
        public const float Size = 20f, RingRadius = 6.0f, WallZ = 8.0f, WallHeight = 2.5f, WallLength = 16f;
        public static readonly Vector3 PlayerStart = new Vector3(0f, 0f, -0.7f);
        public const float PlayerYaw = 0f;
        public static readonly Vector3 DummyStart = new Vector3(0f, 0f, 0.7f);
        public const float DummyYaw = 180f;

        public static readonly Color FloorColor = new Color(0.80f, 0.78f, 0.74f), WallColor = new Color(0.70f, 0.67f, 0.62f), CrowdColor = new Color(0.16f, 0.14f, 0.15f);

        static Material Mat(Material given, string name, Color c)
        {
            if (given != null) return given;
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) return null;
            var m = new Material(sh) { name = name };
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", 0f);
            return m;
        }

        static GameObject Box(string name, Vector3 center, Vector3 size, int layer, Material m, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.layer = layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale = size;
            if (m != null) go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        /// 바닥·벽·구경꾼 원(구경꾼 n 명). 돌려주는 것 = 루트
        public static GameObject Build(Material floor = null, Material wall = null, Material crowd = null, int crowdCount = 12, bool withWall = true)
        {
            var root = new GameObject("CombatLab").transform;
            var fm = Mat(floor, "Lab_Floor", FloorColor);
            Box("Floor", new Vector3(0f, -0.5f, 0f), new Vector3(Size, 1f, Size), Layers.Ground, fm, root);
            // 보이기만 하는 넓은 바닥(충돌 없음) — 옆에서 찍어도 바닥 끝이 화면에 안 걸리게
            var apron = Box("Apron", new Vector3(0f, -0.52f, 0f), new Vector3(240f, 1f, 240f), Layers.Ground, fm, root);
            Object.DestroyImmediate(apron.GetComponent<Collider>());
            if (withWall)
            {
                var w = Box("Wall_North", new Vector3(0f, WallHeight * 0.5f, WallZ + 0.25f), new Vector3(WallLength, WallHeight, 0.5f), Layers.Wall, Mat(wall, "Lab_Wall", WallColor), root);
                w.AddComponent<HeatSurface>();
            }
            if (crowdCount > 0)
            {
                var ring = new GameObject("CrowdRing").transform;
                ring.SetParent(root, false);
                var cr = ring.gameObject.AddComponent<CrowdRing>();
                cr.Radius = RingRadius;
                var cm = Mat(crowd, "Lab_Crowd", CrowdColor);
                for (int i = 0; i < crowdCount; i++)
                {
                    float a = (i + 0.5f) / crowdCount * 360f;
                    var dir = HitResolver.YawDir(a);
                    float r = RingRadius + 0.6f + (i % 3) * 0.3f;
                    float h = 1.62f + (i * 37 % 5) * 0.05f;
                    var p = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    p.name = $"Crowd_{i + 1:00}";
                    Object.DestroyImmediate(p.GetComponent<Collider>());
                    p.layer = Layers.Crowd;
                    p.transform.SetParent(ring, false);
                    p.transform.localPosition = dir * r + Vector3.up * (h * 0.5f);
                    p.transform.localScale = new Vector3(0.46f, h * 0.5f, 0.34f);
                    p.transform.localRotation = Quaternion.Euler(0f, a + 180f, 0f);
                    if (cm != null) p.GetComponent<Renderer>().sharedMaterial = cm;
                }
            }
            return root.gameObject;
        }
    }
}
