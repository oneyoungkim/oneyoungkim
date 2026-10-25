// 행인1의 메인이벤트 — 룩데브 ⑤⑥(docs/14 4-5·4-6, 2026-10-26): Zone1 의 '빛 얼룩'과 '잔 디테일' — 모두 env/Detail14_Z1 아래(비교 사진에서 켜고 끔, 합치기 뒤라 Merge6 에 안 섞임)
//   ⑤ 햇빛 얼룩(Dapple8): 길에서 가까운 나무마다 해 반대쪽(낮 프리셋 해 방향 × 수관 높이)으로 땅에 잎 그림자 판(Haengin/LeafDapple, 곱하기) — 48m 칸마다 한 메시
//   ⑥ 창틀 화분(WindowBoxes8, 키트 streak 소켓 = 창턱 · 2층 이상 28%) · 빨래집게(Pins8, cloth 소켓 위 양 끝) · 전단지·스티커·낙서(Flyers8, 전봇대·1층 벽 —
//      tools/lookdev/detail_tex.py 아틀라스, DecalQuad 합친 판) · 상자·플라스틱 상자·자전거(Clutter8, 문 앞 화분·계량기 둘레 — 콜라이더 없음)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Haengin.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Haengin.EditorGame
{
    public static partial class EnvSetup
    {
        const string Pass8MatDir = MatDir + "/Pass8";

        static string Pass8(Zone1Data d, Transform zoneRoot, Transform env, Dictionary<string, Dictionary<string, object>> objs, Func<string, GameObject> prefab)
        {
            var t0 = DateTime.Now;
            EnsureFolder(Pass8MatDir);
            var root = new GameObject("Detail14_Z1").transform; root.SetParent(env, false);
            var light = new GameObject("Dapple14_Z1").transform; light.SetParent(env, false);     // ⑤ 빛(이름에 Detail14 없음 — 비교 사진 'light' 단계에서 켜짐, 셰이더가 _HaenginLightOn 으로 끔)
            Physics.SyncTransforms();
            string a = Dapple8(d, env, light);
            string b = WindowBoxes8(root);
            string c = Pins8(root);
            string e = Flyers8(d, root);
            string f = Clutter8(d, env, root, prefab);
            return $"룩데브 ⑤⑥(Detail14): {a} · {b} · {c} · {e} · {f} · {(DateTime.Now - t0).TotalSeconds:F1}초";
        }

        static float RoadDist8(Zone1Data d, Vector3 p, out float width)
        {
            float best = float.MaxValue; width = 3f;
            foreach (var r in d.Roads)
                for (int i = 0; i < r.Points.Length - 1; i++)
                {
                    Vector3 a = r.Points[i], b = r.Points[i + 1]; var ab = Flat(b - a); float L2 = ab.sqrMagnitude;
                    float t = L2 > 1e-4f ? Mathf.Clamp01(Vector3.Dot(Flat(p - a), ab) / L2) : 0f;
                    float dist = Flat(a + (b - a) * t - p).magnitude;
                    if (dist < best) { best = dist; width = r.Width; }
                }
            return best;
        }

        // ═════════════════════════ ⑤ 햇빛 얼룩(낮 잎 그림자)
        static string Dapple8(Zone1Data d, Transform env, Transform root)
        {
            var sh = Shader.Find("Haengin/LeafDapple");
            if (sh == null) return "햇빛 얼룩: 셰이더 없음";
            string mp = $"{Pass8MatDir}/M_LeafDapple.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
            if (m == null) { m = new Material(sh) { name = "M_LeafDapple" }; AssetDatabase.CreateAsset(m, mp); }
            if (m.shader != sh) m.shader = sh;
            m.SetColor("_Shade", new Color(0.60f, 0.58f, 0.60f)); m.SetFloat("_Scale", 0.85f); m.SetFloat("_Holes", 0.42f);
            EditorUtility.SetDirty(m);
            var day = AssetDatabase.LoadAssetAtPath<LightingPreset>(StorySetup.PresetPath(DayPart.Day));
            float pitch = day != null ? day.SunPitch : 48f, yaw = day != null ? day.SunYaw : 155f;
            var light = Quaternion.Euler(pitch, yaw + 180f, 0f) * Vector3.forward;       // 빛이 나아가는 방향
            var push = Flat(light).normalized / Mathf.Tan(Mathf.Max(10f, pitch) * Mathf.Deg2Rad);
            // 나무: 이름에 _tree 가 들어간 맨 위 오브젝트(키트 소켓 P_*_tree · 거리 나무 줄 · 카드 수관)
            var trees = new List<(Vector3 c, float r, float h, float gy)>();
            var seen = new HashSet<Transform>();
            foreach (var t in env.GetComponentsInChildren<Transform>(true))
            {
                if (!t.gameObject.activeInHierarchy) continue;
                string n = t.name.ToLowerInvariant();
                if (!(n.Contains("_tree") || n.Contains("나무"))) continue;
                bool inner = false;
                for (var p = t.parent; p != null && p != env; p = p.parent) if (seen.Contains(p)) { inner = true; break; }
                if (inner) continue;
                var rs = t.GetComponentsInChildren<Renderer>(true);
                if (rs.Length == 0) continue;
                seen.Add(t);
                var bb = rs[0].bounds; foreach (var r in rs) bb.Encapsulate(r.bounds);
                if (bb.size.y < 2f) continue;
                var g = GroundAt(new Vector3(t.position.x, bb.max.y, t.position.z), 2f, bb.size.y + 6f);
                float crownH = Mathf.Max(1.5f, bb.center.y + bb.extents.y * 0.25f - g.y);
                trees.Add((new Vector3(t.position.x, g.y, t.position.z), Mathf.Max(bb.extents.x, bb.extents.z), crownH, g.y));
            }
            var cells = new Dictionary<(int, int), MB6>();
            var rng = new System.Random(814);
            int made = 0, far = 0;
            foreach (var (c, r, h, gy) in trees)
            {
                float rd = RoadDist8(d, c, out float rw);
                float R = Mathf.Clamp(r * 1.25f, 1.6f, 6.0f);
                if (rd > rw * 0.5f + R + 1.0f) { far++; continue; }
                var center = c + push * h;
                var k = Cell6(center, 48f);
                if (!cells.TryGetValue(k, out var mb)) cells[k] = mb = new MB6();
                const int N = 9;
                int b0 = mb.V.Count;
                float seed = (float)rng.NextDouble();
                for (int j = 0; j <= N; j++)
                    for (int i = 0; i <= N; i++)
                    {
                        float u = i / (float)N * 2f - 1f, v = j / (float)N * 2f - 1f;
                        var p = center + new Vector3(u * R, 0f, v * R);
                        var g = GroundAt(new Vector3(p.x, gy + 2.5f, p.z), 2f, 8f);
                        mb.Add(g + Vector3.up * 0.02f, Vector3.up, new Vector2(u, v), new Vector2(seed, 0f), new Color(1f, 1f, 1f, Mathf.Clamp01(0.55f + r * 0.12f)));
                    }
                for (int j = 0; j < N; j++)
                    for (int i = 0; i < N; i++)
                    {
                        int q = b0 + j * (N + 1) + i;
                        mb.Tri(q, q + N + 1, q + 1); mb.Tri(q + 1, q + N + 1, q + N + 2);
                    }
                made++;
            }
            foreach (var kv in cells.OrderBy(x => x.Key.Item1).ThenBy(x => x.Key.Item2))
            {
                var go = Obj6($"햇빛 얼룩 {kv.Key.Item1}_{kv.Key.Item2}", root, kv.Value.Save($"d8_dapple_{kv.Key.Item1}_{kv.Key.Item2}"), m, ShadowCastingMode.Off, false);
                GameObjectUtility.SetStaticEditorFlags(go, 0);
            }
            return $"햇빛 얼룩 {made}(나무 {trees.Count} · 길에서 먼 것 {far} · 칸 {cells.Count})";
        }

        // ═════════════════════════ ⑥-1 창틀 화분(2층 이상 창 28%)
        static string WindowBoxes8(Transform root)
        {
            var boxes = new MB6(); var leaves = new MB6();
            var terra = Flat("M_D14_Planter_Terra", new Color(0.66f, 0.38f, 0.28f));
            var white = Flat("M_D14_Planter_White", new Color(0.86f, 0.85f, 0.82f));
            var whiteBoxes = new MB6();
            var crop = ClumpCrop("weed"); var fcrop = ClumpCrop("flower");
            var flowers = new MB6();
            int n = 0;
            var rng = new System.Random(808);
            foreach (var (t, inf) in socks5.Where(s => S(s.inf, "sock_kind") == "streak"))
            {
                var p = V3(G(inf, "pos")); var nrm = V3(G(inf, "normal")); nrm.y = 0f; nrm.Normalize();
                var sz = L(G(inf, "size")); float w = sz != null ? F(sz[0]) : 1.2f;
                var g = GroundAt(p, 0.5f, 14f);
                if (p.y - g.y < 2.4f) continue;
                if ((Hash(t.name) & 0x7fffffff) % 100 >= 28) continue;
                var right = Vector3.Cross(Vector3.up, nrm).normalized;
                float bw = Mathf.Clamp(w * 0.55f, 0.5f, 1.1f);
                var c = p + nrm * 0.13f + Vector3.up * 0.09f;
                Box6(rng.Next(3) == 0 ? whiteBoxes : boxes, c, right, nrm, bw, 0.17f, 0.2f);
                bool fl = rng.Next(3) == 0;
                var lm = fl ? flowers : leaves; var cr = fl ? fcrop : crop;
                for (int k = 0; k < 3; k++)
                {
                    float off = (k - 1) * bw * 0.32f;
                    var basep = c + right * off + Vector3.up * 0.06f;
                    var dir = Quaternion.AngleAxis(rng.Next(-35, 35), Vector3.up) * right;
                    float s = 0.32f + (float)rng.NextDouble() * 0.12f;
                    Card8(lm, basep, dir, s, s * 0.8f, cr[rng.Next(4)]);
                    Card8(lm, basep, Vector3.Cross(Vector3.up, dir), s * 0.8f, s * 0.7f, cr[rng.Next(4)]);
                }
                n++;
            }
            var weedM = CardMat6("weed", "weed", new Color(0.93f, 0.96f, 0.90f), 0.035f, 0.55f, true);
            var flowerM = CardMat6("flower", "flower", Color.white, 0.03f, 1f, true);
            Obj6("창틀 화분", root, boxes.Save("d8_winbox"), terra, ShadowCastingMode.Off);
            Obj6("창틀 화분(흰)", root, whiteBoxes.Save("d8_winbox_w"), white, ShadowCastingMode.Off);
            Obj6("창틀 화분 풀", root, leaves.Save("d8_winbox_leaf"), weedM, ShadowCastingMode.Off);
            Obj6("창틀 화분 꽃", root, flowers.Save("d8_winbox_flower"), flowerM, ShadowCastingMode.Off);
            return $"창틀 화분 {n}";
        }

        static void Box6(MB6 mb, Vector3 c, Vector3 right, Vector3 fwd, float w, float h, float dpt)
        {
            var up = Vector3.up;
            var hx = right * (w / 2); var hy = up * (h / 2); var hz = fwd * (dpt / 2);
            void Face(Vector3 n, Vector3 a, Vector3 b)
            {
                var o = c + n;
                int i0 = mb.Add(o - a - b, n.normalized, new Vector2(0, 0), Vector2.zero, Color.white);
                mb.Add(o + a - b, n.normalized, new Vector2(1, 0), Vector2.zero, Color.white);
                mb.Add(o + a + b, n.normalized, new Vector2(1, 1), Vector2.zero, Color.white);
                mb.Add(o - a + b, n.normalized, new Vector2(0, 1), Vector2.zero, Color.white);
                mb.Tri(i0, i0 + 2, i0 + 1); mb.Tri(i0, i0 + 3, i0 + 2);
            }
            Face(hz, -hx, hy); Face(-hz, hx, hy); Face(hx, hz, hy); Face(-hx, -hz, hy); Face(hy, hx, hz); Face(-hy, hx, -hz);
        }

        static void Card8(MB6 mb, Vector3 baseC, Vector3 right, float w, float h, Rect uv)
        {
            var up = Vector3.up * h;
            var p = new[] { baseC - right * (w / 2), baseC + right * (w / 2), baseC + right * (w / 2) + up, baseC - right * (w / 2) + up };
            var t = new[] { new Vector2(uv.xMin, uv.yMin), new Vector2(uv.xMax, uv.yMin), new Vector2(uv.xMax, uv.yMax), new Vector2(uv.xMin, uv.yMax) };
            int b0 = mb.V.Count;
            for (int k = 0; k < 4; k++) mb.Add(p[k], (p[k] - (baseC - Vector3.up * 0.1f)).normalized, t[k], new Vector2(0f, Mathf.Clamp01((p[k].y - baseC.y) / Mathf.Max(0.1f, h))), Color.white);
            mb.Tri(b0, b0 + 2, b0 + 1); mb.Tri(b0, b0 + 3, b0 + 2);
        }

        // ═════════════════════════ ⑥-2 빨래집게(천 위 양 끝)
        static string Pins8(Transform root)
        {
            var cols = new[] { new Color(0.80f, 0.30f, 0.26f), new Color(0.30f, 0.50f, 0.76f), new Color(0.90f, 0.78f, 0.30f), new Color(0.40f, 0.68f, 0.44f) };
            var mbs = cols.Select(_ => new MB6()).ToArray();
            int n = 0;
            var rng = new System.Random(809);
            foreach (var (t, inf) in socks5.Where(s => S(s.inf, "sock_kind") == "cloth"))
            {
                var p = V3(G(inf, "pos")); var nrm = V3(G(inf, "normal")); nrm.y = 0f; nrm.Normalize();
                var sz = L(G(inf, "size")); float w = sz != null ? F(sz[0]) : 0.4f;
                var right = Vector3.Cross(Vector3.up, nrm).normalized;
                foreach (float sgn in new[] { -1f, 1f })
                {
                    var c = p + right * sgn * (w / 2f - 0.035f) + nrm * 0.012f - Vector3.up * 0.012f;
                    Box6(mbs[rng.Next(cols.Length)], c, right, nrm, 0.018f, 0.06f, 0.022f);
                    n++;
                }
            }
            for (int k = 0; k < cols.Length; k++) Obj6($"빨래집게 {k}", root, mbs[k].Save($"d8_pins_{k}"), Flat($"M_D14_Pin_{k}", cols[k]), ShadowCastingMode.Off);
            return $"빨래집게 {n}";
        }

        // ═════════════════════════ ⑥-3 전단지·스티커·낙서(아틀라스 판)
        static string Flyers8(Zone1Data d, Transform root)
        {
            string json = Path.ChangeExtension(LookdevSetup.AtlasPath, ".json");
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(LookdevSetup.AtlasPath);
            if (tex == null || !File.Exists(json)) return "전단지: 아틀라스 없음";
            var j = D(MiniJson.Parse(File.ReadAllText(json, System.Text.Encoding.UTF8)));
            float size = F(G(j, "size"), 2048f);
            var rects = new Dictionary<string, Rect>();
            foreach (var kv in D(j["rects"])) { var l = L(kv.Value); rects[kv.Key] = new Rect(F(l[0]) / size, 1f - (F(l[1]) + F(l[3])) / size, F(l[2]) / size, F(l[3]) / size); }
            var sh = Shader.Find("Haengin/DecalQuad");
            string mp = $"{Pass8MatDir}/M_D8_Flyers.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
            if (m == null) { m = new Material(sh) { name = "M_D8_Flyers" }; AssetDatabase.CreateAsset(m, mp); }
            if (m.shader != sh) m.shader = sh;
            m.SetTexture("_MainTex", tex); m.SetTextureScale("_MainTex", Vector2.one); m.SetTextureOffset("_MainTex", Vector2.zero);
            m.SetColor("_Tint", Color.white); m.SetFloat("_Cull", 2f); m.SetFloat("_Multiply", 0f); m.SetFloat("_Fade", 0.25f); m.SetFloat("_EdgeRough", 0.35f); m.SetFloat("_AtlasUV", 1f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            EditorUtility.SetDirty(m);
            var cells = new Dictionary<(int, int), MB6>();
            MB6 Acc(Vector3 p) { var k = Cell6(p, 48f); if (!cells.TryGetValue(k, out var mb)) cells[k] = mb = new MB6(); return mb; }
            void Plate(Vector3 c, Vector3 n, float w, string key, float alpha, float tilt)
            {
                var r = rects[key];
                float h = w * (r.height / r.width);
                var right = Vector3.Cross(Vector3.up, n).normalized;
                var up = Quaternion.AngleAxis(tilt, n) * Vector3.up; right = Quaternion.AngleAxis(tilt, n) * right;
                var mb = Acc(c);
                var o = c + n * 0.012f;
                int i0 = mb.V.Count;
                var col = new Color(1f, 1f, 1f, alpha);
                mb.Add(o - right * (w / 2) - up * (h / 2), n, new Vector2(0, 0), new Vector2(r.xMin, r.yMin), col);
                mb.Add(o + right * (w / 2) - up * (h / 2), n, new Vector2(1, 0), new Vector2(r.xMax, r.yMin), col);
                mb.Add(o + right * (w / 2) + up * (h / 2), n, new Vector2(1, 1), new Vector2(r.xMax, r.yMax), col);
                mb.Add(o - right * (w / 2) + up * (h / 2), n, new Vector2(0, 1), new Vector2(r.xMin, r.yMax), col);
                mb.Tri(i0, i0 + 2, i0 + 1); mb.Tri(i0, i0 + 3, i0 + 2);
            }
            string[] flyers = { "flyer_room", "flyer_cat", "flyer_tutor", "flyer_piano", "flyer_bike", "flyer_job" };
            string[] stickers = { "sticker_delivery", "sticker_star", "sticker_noparking" };
            string[] tags = { "graffiti_a", "graffiti_b", "doodle_cat", "doodle_smile" };
            string[] posters = { "poster_market", "poster_walk" };
            var rng = new System.Random(810);
            int nf = 0, ns = 0, ng = 0, np = 0;
            // 전봇대: 길 쪽 두 면에 전단 2~4(겹쳐 붙음) + 스티커 · '전단지 부착 금지' 가끔
            foreach (var lm in d.Landmarks.Where(x => x.Kind == "pole"))
            {
                var bp = GroundAt(lm.Pos, 3f, 8f);
                var toRoad = ToRoadDir(d, bp);
                foreach (var side in new[] { toRoad, Quaternion.Euler(0f, 90f + rng.Next(-20, 20), 0f) * toRoad })
                {
                    int k = 1 + rng.Next(3);
                    for (int q = 0; q < k; q++)
                    {
                        var c = bp + Vector3.up * (1.35f + q * 0.18f + (float)rng.NextDouble() * 0.15f) + side * 0.17f + Vector3.Cross(Vector3.up, side) * ((float)rng.NextDouble() - 0.5f) * 0.06f;
                        Plate(c + side * 0.004f * q, side, 0.21f, flyers[rng.Next(flyers.Length)], 0.95f, rng.Next(-8, 8)); nf++;
                    }
                    if (rng.Next(3) == 0) { Plate(bp + Vector3.up * 2.15f + side * 0.172f, side, 0.24f, "sticker_noflyer", 0.9f, 0f); ns++; }
                    if (rng.Next(2) == 0) { Plate(bp + Vector3.up * (0.9f + (float)rng.NextDouble() * 0.3f) + side * 0.171f, side, 0.08f, stickers[rng.Next(stickers.Length)], 0.95f, rng.Next(-20, 20)); ns++; }
                }
            }
            // 1층 벽(창 옆): 전단·포스터·스티커·낙서 — 1층 창(키트 streak, 땅 위 2.2m 안)
            foreach (var (t, inf) in socks5.Where(s => S(s.inf, "sock_kind") == "streak"))
            {
                var p = V3(G(inf, "pos")); var nrm = V3(G(inf, "normal")); nrm.y = 0f; nrm.Normalize();
                var g = GroundAt(p, 0.5f, 6f);
                if (p.y - g.y > 2.2f) continue;
                if (RoadDist8(d, p, out float rw) > rw * 0.5f + 4f) continue;
                var sz = L(G(inf, "size")); float w = sz != null ? F(sz[0]) : 1.2f;
                var right = Vector3.Cross(Vector3.up, nrm).normalized;
                int h = Hash(t.name) & 0x7fffffff;
                float sgn = (h & 1) == 0 ? 1f : -1f;
                var edge = p + right * sgn * (w / 2f + 0.32f);
                int roll = h % 100;
                if (roll < 22) { Plate(new Vector3(edge.x, g.y + 1.55f, edge.z), nrm, 0.34f, flyers[h % flyers.Length], 0.92f, rng.Next(-5, 5)); nf++; }
                else if (roll < 30) { Plate(new Vector3(edge.x, g.y + 1.5f, edge.z) + right * sgn * 0.1f, nrm, 0.44f, posters[h % posters.Length], 0.9f, rng.Next(-3, 3)); np++; }
                else if (roll < 40) { Plate(new Vector3(edge.x, g.y + 1.2f, edge.z), nrm, 0.1f, stickers[h % stickers.Length], 0.95f, rng.Next(-15, 15)); ns++; }
                if (roll % 7 == 3) { var gp = p + right * -sgn * (w * 0.2f); Plate(new Vector3(gp.x, g.y + 0.55f, gp.z), nrm, 0.75f, tags[h % tags.Length], 0.8f, rng.Next(-6, 6)); ng++; }
            }
            foreach (var kv in cells.OrderBy(x => x.Key.Item1).ThenBy(x => x.Key.Item2))
                Obj6($"전단·낙서 {kv.Key.Item1}_{kv.Key.Item2}", root, kv.Value.Save($"d8_flyer_{kv.Key.Item1}_{kv.Key.Item2}"), m, ShadowCastingMode.Off, false);
            return $"전단 {nf} · 포스터 {np} · 스티커 {ns} · 낙서 {ng}";
        }

        // ═════════════════════════ ⑥-4 상자·플라스틱 상자·자전거(문 앞 잡동사니 옆, 콜라이더 없음)
        static string Clutter8(Zone1Data d, Transform env, Transform root, Func<string, GameObject> prefab)
        {
            var anchors = new List<Transform>();
            foreach (var t in env.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("Prop_styrofoam_planter") || t.name.StartsWith("Prop_clay_pots_cluster") || t.name.StartsWith("Prop_basin_planter_onion") ||
                    t.name.StartsWith("Prop_potted_plant") || t.name.StartsWith("Prop_electric_meter_box") || t.name.StartsWith("Prop_mailbox_cluster"))
                    anchors.Add(t);
            var taken = anchors.Select(t => t.position).ToList();
            int boxes = 0, crates = 0, bikes = 0, pots = 0, skip = 0;
            foreach (var t in anchors.OrderBy(x => x.name, StringComparer.Ordinal))
            {
                int h = Hash(t.name) & 0x7fffffff;
                int roll = h % 100;
                string prop = roll < 18 ? "cardboard_boxes" : roll < 26 ? "plastic_crates_stack" : roll < 32 && t.name.Contains("meter") ? "delivery_bicycle" : roll < 44 ? (h % 2 == 0 ? "potted_plant_a" : "potted_plant_b") : null;
                if (prop == null) continue;
                var fwd = Flat(t.forward).normalized; if (fwd.sqrMagnitude < 0.5f) fwd = Vector3.forward;
                var right = Vector3.Cross(Vector3.up, fwd).normalized * ((h & 2) == 0 ? 1f : -1f);
                float off = prop == "delivery_bicycle" ? 1.3f : 0.85f;
                var p = GroundAt(t.position + right * off + fwd * (prop == "delivery_bicycle" ? 0.35f : 0.05f), 1.5f, 4f);
                if (taken.Any(q => Flat(q - p).magnitude < 0.5f)) { skip++; continue; }
                if (RoadDist8(d, p, out float rw) < rw * 0.5f - 0.3f) { skip++; continue; }       // 길 가운데로 나오면 안 둠
                if (Physics.CheckSphere(p + Vector3.up * 0.5f + fwd * 0.1f, 0.1f, (1 << Layers.Wall), QueryTriggerInteraction.Ignore)) { skip++; continue; }   // 벽 속
                float yaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg + (prop == "delivery_bicycle" ? 90f : (h % 30) - 15);
                var go = PutProp(prefab, prop, root, p, yaw, "D14 " + prop);
                if (go == null) continue;
                foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                taken.Add(p);
                if (prop == "cardboard_boxes") boxes++; else if (prop == "plastic_crates_stack") crates++; else if (prop == "delivery_bicycle") bikes++; else pots++;
            }
            return $"상자 {boxes} · 플라스틱 상자 {crates} · 자전거 {bikes} · 화분 {pots}(자리 겹침·길·벽 {skip} 건너뜀)";
        }
    }
}
