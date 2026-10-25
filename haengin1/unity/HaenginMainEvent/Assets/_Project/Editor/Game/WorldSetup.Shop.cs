// 행인1의 메인이벤트 — 돈 쓸 곳 무대(docs/13 14-6): Zone1 명륜3가 원룸 2 1층 가게 앞 판매창 3(성곽약국 · 와룡 삼계탕 · 와룡 장어) — 판매대·차양·간판(TMP)·소품 + PlaceMarker(shop_*).
// 실내 무대는 만들지 않는다(가게 앞 창). 상호는 가상(실존 상호 금지). AddToZone1 끝에서 부른다.
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Haengin.EditorGame
{
    public static partial class WorldSetup
    {
        /// 원룸 2(docs/03 블록 '명륜3가 원룸 2') 앞면: 중심 (-155.3, 101.48) · yaw 17.45 · 깊이 8 → 앞면 가운데에서 오른쪽(+x) −3 · 0 · +3
        static readonly (string id, string sign, float along, Color awning, string prop)[] Fronts =
        {
            ("pharmacy", "성곽약국", -3f, new Color(0.24f, 0.48f, 0.36f), ""),
            ("samgye", "와룡 삼계탕", 0f, new Color(0.64f, 0.26f, 0.21f), "P_gamasot_stove"),
            ("eel", "와룡 장어", 3f, new Color(0.19f, 0.25f, 0.34f), "P_stockpot_burner"),
        };

        public static int AddShopFronts(Transform world)
        {
            var root = new GameObject("가게 앞 판매창").transform;
            root.SetParent(world, false);
            const float yaw = 17.45f;
            var rot = Quaternion.Euler(0f, yaw, 0f);
            Vector3 center = new Vector3(-155.3f, 0f, 101.48f), right = rot * Vector3.right, outward = rot * Vector3.back;
            Vector3 face = center + outward * 4.0f;
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root + "/Fonts/KR_Bold_SDF.asset");
            var wood = Flat("M_Shop_Counter", new Color(0.55f, 0.42f, 0.30f));
            var board = Flat("M_Shop_Board", new Color(0.93f, 0.90f, 0.84f));
            var glass = Flat("M_Shop_Glass", new Color(0.70f, 0.80f, 0.82f));
            int n = 0;
            Physics.SyncTransforms();
            foreach (var f in Fronts)
            {
                var p = face + right * f.along + outward * 0.05f;
                // 인도 높이: 아래로 쏜 것 중 성균관로 북쪽 인도 높이(25.7 언저리)에 가장 가까운 것(발코니·차양에 맞은 건 버림)
                float y = 25.7f, best = 3f;
                foreach (var h in Physics.RaycastAll(new Vector3(p.x, 80f, p.z) + outward * 0.8f, Vector3.down, 120f, ~0, QueryTriggerInteraction.Ignore))
                    if (Mathf.Abs(h.point.y - 25.7f) < best) { best = Mathf.Abs(h.point.y - 25.7f); y = h.point.y; }
                var go = new GameObject(f.sign).transform;
                go.SetParent(root, false);
                go.position = new Vector3(p.x, y, p.z);
                go.rotation = Quaternion.Euler(0f, yaw + 180f, 0f);     // 앞(+z) = 길 쪽
                // 판매대(부딪힘 있음) · 유리 진열장 · 차양 · 간판
                var counter = GameObject.CreatePrimitive(PrimitiveType.Cube);
                counter.name = "판매대"; counter.transform.SetParent(go, false);
                counter.transform.localPosition = new Vector3(0f, 0.5f, 0.3f); counter.transform.localScale = new Vector3(1.7f, 1.0f, 0.55f);
                counter.GetComponent<Renderer>().sharedMaterial = wood;
                Box(go, "판매대 윗판", new Vector3(0f, 1.02f, 0.32f), new Vector3(1.8f, 0.05f, 0.65f), board);
                Box(go, "창", new Vector3(0f, 1.75f, 0.03f), new Vector3(1.9f, 1.2f, 0.04f), glass);
                var aw = GameObject.CreatePrimitive(PrimitiveType.Cube);
                aw.name = "차양"; Object.DestroyImmediate(aw.GetComponent<Collider>());
                aw.transform.SetParent(go, false);
                aw.transform.localPosition = new Vector3(0f, 2.55f, 0.45f); aw.transform.localRotation = Quaternion.Euler(16f, 0f, 0f); aw.transform.localScale = new Vector3(2.2f, 0.06f, 0.95f);
                aw.GetComponent<Renderer>().sharedMaterial = Flat("M_Shop_Awning_" + f.id, f.awning);
                Box(go, "간판", new Vector3(0f, 3.05f, 0.06f), new Vector3(2.1f, 0.5f, 0.08f), Flat("M_Shop_Sign_" + f.id, f.awning * 0.85f + new Color(0.02f, 0.02f, 0.02f)));
                if (font != null)
                {
                    var tg = new GameObject("간판 글");
                    tg.transform.SetParent(go, false);
                    tg.transform.localPosition = new Vector3(0f, 3.05f, 0.105f);
                    tg.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    var tmp = tg.AddComponent<TextMeshPro>();
                    tmp.font = font; tmp.text = f.sign; tmp.fontSize = 3.2f; tmp.alignment = TextAlignmentOptions.Center; tmp.color = new Color(0.97f, 0.95f, 0.90f);
                    tmp.rectTransform.sizeDelta = new Vector2(2.0f, 0.5f); tmp.enableAutoSizing = true; tmp.fontSizeMin = 1.5f; tmp.fontSizeMax = 3.2f;
                }
                if (f.id == "pharmacy")
                {
                    Box(go, "진열장", new Vector3(0.45f, 1.25f, 0.3f), new Vector3(0.6f, 0.4f, 0.4f), glass);
                    Box(go, "약 상자", new Vector3(-0.35f, 1.12f, 0.32f), new Vector3(0.3f, 0.16f, 0.2f), Flat("M_Shop_Box", new Color(0.86f, 0.82f, 0.62f)));
                    Box(go, "십자", new Vector3(0.75f, 3.05f, 0.11f), new Vector3(0.22f, 0.06f, 0.02f), board);
                    Box(go, "십자2", new Vector3(0.75f, 3.05f, 0.11f), new Vector3(0.06f, 0.22f, 0.02f), board);
                }
                if (!string.IsNullOrEmpty(f.prop))
                {
                    var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Prefabs/Env/{f.prop}.prefab");
                    if (pf != null)
                    {
                        var pi = (GameObject)PrefabUtility.InstantiatePrefab(pf, go);
                        pi.transform.localPosition = new Vector3(1.25f, 0f, 0.45f); pi.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                    }
                }
                var mk = new GameObject("Marker_shop_" + f.id);
                mk.transform.SetParent(go, false);
                mk.transform.localPosition = new Vector3(0f, 0f, 1.05f);
                var pm = mk.AddComponent<PlaceMarker>(); pm.Place = "Zone1"; pm.Func = "shop_" + f.id; pm.Label = f.sign;
                n++;
            }
            return n;
        }
    }
}
