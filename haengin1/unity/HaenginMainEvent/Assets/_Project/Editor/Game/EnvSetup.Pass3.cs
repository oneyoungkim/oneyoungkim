// 행인1의 메인이벤트 — 맵 아트 패스 3차(2026-10-24): 길바닥 표시(중앙선·주차선·배수로)·바닥 얼룩(곱하기) · 가로수 줄·담 위 초록 · 잎 카드 · 필로티 콜라이더(키트 철책 통로 meta)
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
        static readonly string[] TreeNames = { "pine_tree", "cherry_tree", "zelkova_tree", "plane_tree" };
        static string LeafCardFor(string tree) => tree == "pine_tree" ? "pine" : tree == "cherry_tree" ? "cherry" : "broad";

        /// 데칼 재질(3차 확장): multiply = 바닥·벽 얼룩(곱하기), fade/edge = 포스터·낙서 바랜 톤·거친 가장자리
        static Material DecalMat3(string atlasTex, string key, float[] rc, Color tint, bool multiply, float fade, float edge, string suffix)
        {
            var m = DecalMat(atlasTex, key + suffix, rc, tint, true);
            m.SetFloat("_Multiply", multiply ? 1f : 0f);
            m.SetFloat("_SrcBlend", multiply ? (float)BlendMode.DstColor : (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", multiply ? (float)BlendMode.Zero : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_Fade", fade); m.SetFloat("_EdgeRough", edge);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ───────────────────────── 길바닥: 중앙선(노랑)·주차선·배수로 띠·바닥 얼룩(곱하기)
        static string GroundMarks(Zone1Data d, Transform ground, Dictionary<string, float[]> rects)
        {
            var yellow = Flat("M_Paint_Yellow", new Color(0.85f, 0.70f, 0.22f));
            var paint = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/M_Paint_White.mat") ?? Flat("M_Paint_White", new Color(0.93f, 0.92f, 0.88f));
            var gutter = Flat("M_Gutter_Dark", new Color(0.36f, 0.36f, 0.35f));
            int center = 0, bays = 0, gutters = 0, stains = 0;
            // 중앙선: 큰길(road 종류) 폴리라인 가운데 — 세그먼트마다 상자
            foreach (var r in d.Roads.Where(x => x.Kind == "road"))
                for (int i = 0; i < r.Points.Length - 1; i++)
                {
                    Vector3 a = r.Points[i], b = r.Points[i + 1]; var dir = b - a; float len = dir.magnitude; if (len < 0.5f) continue;
                    Box($"중앙선 {r.Name} {i}", ground, (a + b) * 0.5f + Vector3.up * 0.014f, new Vector3(0.12f, 0.008f, len - 0.1f), Quaternion.LookRotation(dir.normalized, Vector3.up), yellow);
                    var side = Vector3.Cross(Vector3.up, Flat(dir).normalized).normalized;
                    foreach (float sg in new[] { 1f, -1f })
                        Box($"배수로 {r.Name} {i}{(sg > 0 ? "R" : "L")}", ground, (a + b) * 0.5f + side * sg * (r.Width * 0.5f - 0.2f) + Vector3.up * 0.011f, new Vector3(0.38f, 0.006f, len), Quaternion.LookRotation(dir.normalized, Vector3.up), gutter);
                    center++; gutters += 2;
                }
            // 주차선: 주차장 동쪽 절반, 남쪽 옹벽에서 북으로 5m 칸 2.5m 간격
            var pad = d.Pads.FirstOrDefault(p => p.Name.Contains("주차장"));
            if (pad != null)
            {
                var rot = Quaternion.Euler(0f, pad.Yaw, 0f);
                for (int k = 0; k < 5; k++)
                {
                    var local = new Vector3(-1.0f + k * 2.5f, 0f, -pad.Size.y * 0.5f + 2.6f);
                    var c = pad.Center + rot * local;
                    Box($"주차선 {k}", ground, GroundAt(c) + Vector3.up * 0.013f, new Vector3(0.1f, 0.008f, 5.0f), rot, paint);
                    bays++;
                }
                Box("주차선 가로", ground, GroundAt(pad.Center + rot * new Vector3(4.0f, 0f, -pad.Size.y * 0.5f + 5.1f)) + Vector3.up * 0.013f, new Vector3(10.1f, 0.008f, 0.1f), rot, paint);
            }
            // 바닥 얼룩·패치(곱하기): 큰길 12 · 주차장 6 · 골목 10
            var grime = rects.Keys.Where(k => k.StartsWith("decals_grime")).OrderBy(k => k, StringComparer.Ordinal).ToList();
            if (grime.Count > 0)
            {
                var rng = new System.Random(20261024);
                void Stain(string name, Vector3 p, float size)
                {
                    var key = grime[rng.Next(grime.Count)];
                    var m = DecalMat3("M_Decal_Atlas", key, rects[key], new Color(1f, 1f, 1f, 0.55f), true, 0f, 0.5f, "_mul");
                    var go = Quad(name, ground, GroundAt(p) + Vector3.up * 0.01f, Vector3.up, size, size * (0.7f + (float)rng.NextDouble() * 0.5f), m, 0f);
                    go.transform.rotation = Quaternion.Euler(90f, (float)rng.NextDouble() * 360f, 0f);
                    stains++;
                }
                foreach (var r in d.Roads.Where(x => x.Kind == "road"))
                {
                    float L = Length(r.Points);
                    for (float s = 4f; s < L - 2f; s += 9f + (float)rng.NextDouble() * 6f)
                    {
                        var (p, dir) = Along(r.Points, s); var side = Vector3.Cross(Vector3.up, dir).normalized;
                        Stain($"얼룩 {r.Name} {s:F0}", p + side * ((float)rng.NextDouble() - 0.5f) * (r.Width - 1.5f), 1.6f + (float)rng.NextDouble() * 1.6f);
                    }
                }
                foreach (var r in d.Roads.Where(x => x.Kind == "alley" || x.Kind == "driveway"))
                {
                    float L = Length(r.Points);
                    for (float s = 3f; s < L - 2f; s += 8f + (float)rng.NextDouble() * 5f)
                    {
                        var (p, dir) = Along(r.Points, s); var side = Vector3.Cross(Vector3.up, dir).normalized;
                        Stain($"얼룩 {r.Name} {s:F0}", p + side * ((float)rng.NextDouble() - 0.5f) * (r.Width - 1.0f), 1.0f + (float)rng.NextDouble() * 1.2f);
                    }
                }
                if (pad != null)
                {
                    var rot = Quaternion.Euler(0f, pad.Yaw, 0f);
                    for (int k = 0; k < 6; k++)
                        Stain($"얼룩 주차장 {k}", pad.Center + rot * new Vector3(((float)rng.NextDouble() - 0.5f) * (pad.Size.x - 3f), 0f, ((float)rng.NextDouble() - 0.5f) * (pad.Size.y - 3f)), 1.6f + (float)rng.NextDouble() * 2f);
                }
            }
            return $"중앙선 {center} · 주차선 {bays} · 배수로 {gutters} · 바닥 얼룩 {stains}";
        }

        // ───────────────────────── 가로수 줄 · 성곽길 소나무 · 오르막 관목 · 담 위 초록
        static string VegetationRows(Zone1Data d, Transform zoneRoot, Transform root, Dictionary<string, Dictionary<string, object>> objs, Func<string, GameObject> prefab)
        {
            var treeSpots = new List<Vector3>();
            var treeRoot = zoneRoot.Find("Blocks/tree");
            if (treeRoot != null) foreach (Transform t in treeRoot) treeSpots.Add(t.position);
            // 가로수를 두면 안 되는 자리: 간판 앞 4m · 골목·진입로·샛길 입구 3.5m · 주차장 패드 둘레 2m · 시작 지점 6m · 다른 나무 r
            var signSpots = objs.Where(x => S(x.Value, "kind") == "sign").Select(x => V3(G(D(G(x.Value, "info")), "center"))).ToList();
            float DistToRoads(Vector3 p, Func<Zone1Data.Road, bool> pick)
            {
                float best = float.MaxValue;
                foreach (var r in d.Roads.Where(pick))
                    for (int i = 0; i < r.Points.Length - 1; i++)
                    {
                        Vector3 a0 = r.Points[i], b0 = r.Points[i + 1]; var ab = Flat(b0 - a0); float L2 = ab.sqrMagnitude;
                        float t = L2 > 1e-4f ? Mathf.Clamp01(Vector3.Dot(Flat(p - a0), ab) / L2) : 0f;
                        best = Mathf.Min(best, Flat(a0 + (b0 - a0) * t - p).magnitude - r.Width * 0.5f);
                    }
                return best;
            }
            bool NearPad(Vector3 p)
            {
                foreach (var pd in d.Pads)
                {
                    var l = Quaternion.Euler(0f, -pd.Yaw, 0f) * (p - pd.Center);
                    float hx = pd.Shape == "circle" ? pd.Size.x * 0.5f : pd.Size.x * 0.5f, hz = pd.Shape == "circle" ? pd.Size.x * 0.5f : pd.Size.y * 0.5f;
                    if (Mathf.Abs(l.x) < hx + 2f && Mathf.Abs(l.z) < hz + 2f) return true;
                }
                return false;
            }
            bool FreeTree(Vector3 p, float r) => !treeSpots.Any(q => Flat(q - p).magnitude < r) && !signSpots.Any(q => Flat(q - p).magnitude < 4f);
            bool Free(Vector3 p, float r) => FreeTree(p, r)
                && DistToRoads(p, rr => rr.Kind != "sidewalk" && rr.Kind != "road") > 2.5f
                && !NearPad(p)
                && Flat(d.SpawnPos - p).magnitude > 6f;
            int rows = 0, trail = 0, climb = 0, tops = 0;
            bool plane = prefab("plane_tree") != null;
            var rng = new System.Random(3);
            // 1) 성균관로 가로수: 인도 바깥쪽(큰길 반대편) 가장자리, 9m 마다 느티나무·플라타너스 번갈아
            foreach (var r in d.Roads.Where(x => x.Kind == "sidewalk"))
            {
                float L = Length(r.Points); int k = 0;
                for (float s = 6f; s < L - 3f; s += 9f, k++)
                {
                    var (p, dir) = Along(r.Points, s); var side = Vector3.Cross(Vector3.up, dir).normalized;
                    float sg = Vector3.Dot(side, ToRoadDir(d, p)) > 0 ? -1f : 1f;
                    var pos = GroundAt(p + side * sg * (r.Width * 0.5f + 0.7f));
                    if (!Free(pos, 5f) || Mathf.Abs(pos.y - p.y) > 1.2f) continue;
                    string sp = plane && k % 2 == 1 ? "plane_tree" : "zelkova_tree";
                    PutProp(prefab, sp, root, pos, (float)rng.NextDouble() * 360f, $"가로수 {sp} ← {r.Name} {k}", Vector3.one * (0.85f + (float)rng.NextDouble() * 0.2f));
                    treeSpots.Add(pos); rows++;
                }
            }
            // 2) 성곽길: 성곽 반대편(비탈 아래쪽) 2.5m 에 소나무 9m 마다 + 관목 사이사이
            foreach (var r in d.Roads.Where(x => x.Kind == "wallpath"))
            {
                float L = Length(r.Points); int k = 0;
                for (float s = 3f; s < L - 3f; s += 4.5f, k++)
                {
                    var (p, dir) = Along(r.Points, s); var side = Vector3.Cross(Vector3.up, dir).normalized;
                    var a = GroundAt(p + side * 2.6f, 6f, 15f); var b = GroundAt(p - side * 2.6f, 6f, 15f);
                    var pos = a.y <= b.y ? a : b;   // 낮은 쪽 = 비탈
                    if (k % 2 == 0)
                    {
                        if (!FreeTree(pos, 4f)) continue;
                        PutProp(prefab, "pine_tree", root, pos, (float)rng.NextDouble() * 360f, $"성곽길 소나무 {k}", Vector3.one * (0.9f + (float)rng.NextDouble() * 0.4f));
                        treeSpots.Add(pos);
                    }
                    else PutProp(prefab, "shrub_bush", root, pos, (float)rng.NextDouble() * 360f, $"성곽길 관목 {k}", Vector3.one * (0.7f + (float)rng.NextDouble() * 0.5f));
                    trail++;
                }
            }
            // 3) 와룡 오르막(후문 옆 공원길): 양옆 5m 마다 관목, 12m 마다 소나무
            foreach (var r in d.Roads.Where(x => x.Name.Contains("와룡공원 오르막") || x.Name.Contains("공원길")))
            {
                float L = Length(r.Points); int k = 0;
                for (float s = 2.5f; s < L - 2f; s += 5f, k++)
                {
                    var (p, dir) = Along(r.Points, s); var side = Vector3.Cross(Vector3.up, dir).normalized;
                    foreach (float sg in new[] { 1f, -1f })
                    {
                        var pos = GroundAt(p + side * sg * (r.Width * 0.5f + 0.9f), 5f, 12f);
                        if (Mathf.Abs(pos.y - p.y) > 2.5f) continue;
                        bool big = k % 3 == 0 && sg > 0 && FreeTree(pos, 5f);
                        PutProp(prefab, big ? "pine_tree" : "shrub_bush", root, pos, (float)rng.NextDouble() * 360f, $"오르막 {(big ? "소나무" : "관목")} {k}{(sg > 0 ? "R" : "L")}", Vector3.one * (big ? 0.9f : 0.6f + (float)rng.NextDouble() * 0.4f));
                        if (big) treeSpots.Add(pos);
                        climb++;
                    }
                }
            }
            // 4) 담 위 초록: 키트 담(fence_wall, 1.4m 이상) 위 2.4m 마다 60% — 화분·풀·작은 관목
            string[] topKinds = { "potted_plant_b", "grass_tuft", "shrub_bush", "potted_plant_a" };
            foreach (var kv in objs.Where(x => S(x.Value, "kind") == "fences").OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                var blocksL = L(G(D(G(kv.Value, "info")), "blocks")); if (blocksL == null) continue;
                foreach (var bo in blocksL)
                {
                    var bd = D(bo); if ((S(bd, "btype") ?? "") != "fence_wall") continue;
                    var c = V3(G(bd, "center")); var sz = V3(G(bd, "size")); float yaw = F(G(bd, "yaw"));
                    if (sz.y < 1.4f || sz.x < 2f) continue;
                    var rot = Quaternion.Euler(0f, yaw, 0f); var along = rot * Vector3.right; float topY = c.y + sz.y * 0.5f;
                    var rr = new System.Random(Hash("top" + S(bd, "block") + c.x.ToString("F1") + c.z.ToString("F1")));
                    for (float u = -sz.x * 0.5f + 0.8f; u < sz.x * 0.5f - 0.6f; u += 2.4f)
                    {
                        if (rr.NextDouble() > 0.6) continue;
                        string kind = topKinds[rr.Next(topKinds.Length)];
                        var p = c + along * u; p.y = topY;
                        PutProp(prefab, kind, root, p, (float)rr.NextDouble() * 360f, $"담 위 {kind} ← {S(bd, "block")}", kind == "shrub_bush" ? Vector3.one * 0.45f : Vector3.one * 0.9f);
                        tops++;
                    }
                }
            }
            return $"가로수 {rows} · 성곽길 {trail} · 오르막 {climb} · 담 위 {tops}";
        }

        // ───────────────────────── 잎 카드: 나무 프리팹 수관 둘레에 알파 컷아웃 판(근경 잎 결). LOD0 에만
        static int AddLeafCards(GameObject root, string tree, Mesh crownMesh, Transform lod0, List<LOD> lods, float size)
        {
            string card = LeafCardFor(tree);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/Leaf_{card}.png");
            if (tex == null) return 0;
            var sh = Shader.Find("Haengin/ToonLeafCard"); if (sh == null) return 0;
            string mp = $"{PropMatDir}/M_V_card_{card}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
            if (m == null) { m = new Material(sh) { name = "M_V_card_" + card }; AssetDatabase.CreateAsset(m, mp); }
            if (m.shader != sh) m.shader = sh;
            m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", Color.white); m.SetColor("_ShadeColor", ShadeMul);
            m.SetFloat("_Step", 0.5f); m.SetFloat("_Feather", 0.04f); m.SetFloat("_WindAmp", 0.08f); m.SetFloat("_WindFreq", 1.3f); m.SetFloat("_Height", Mathf.Max(1f, size)); m.SetFloat("_Cutoff", 0.45f);
            m.enableInstancing = true; EditorUtility.SetDirty(m);
            var b = crownMesh.bounds;
            float h = b.size.y; float y0 = b.min.y + h * (tree == "pine_tree" ? 0.5f : 0.42f); float y1 = b.max.y - h * 0.04f;
            var cc = new Vector3(b.center.x, (y0 + y1) * 0.5f, b.center.z);
            var radii = new Vector3(b.extents.x * 0.92f, (y1 - y0) * 0.5f, b.extents.z * 0.92f);
            var parent = new GameObject("LeafCards").transform; parent.SetParent(root.transform, false);
            var rng = new System.Random(Hash(tree));
            int n = tree == "pine_tree" ? 10 : 12; float cardSize = Mathf.Max(0.9f, Mathf.Min(b.extents.x, b.extents.z) * 0.55f);
            var renderers = new List<Renderer>();
            for (int i = 0; i < n; i++)
            {
                float th = (float)rng.NextDouble() * Mathf.PI * 2f, ph = Mathf.Acos(1f - 2f * (float)rng.NextDouble()) * 0.5f + 0.5f;   // 아래쪽은 덜
                var dir = new Vector3(Mathf.Sin(ph) * Mathf.Cos(th), Mathf.Cos(ph) * 0.8f, Mathf.Sin(ph) * Mathf.Sin(th));
                var pos = cc + Vector3.Scale(dir.normalized, radii) * (0.85f + (float)rng.NextDouble() * 0.15f);
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.name = "잎 " + i; go.transform.SetParent(parent, false);
                go.transform.localPosition = pos;
                var outDir = (pos - cc); outDir.y *= 0.5f;
                go.transform.localRotation = Quaternion.LookRotation(-outDir.normalized, Vector3.up) * Quaternion.Euler(0f, 0f, (float)rng.NextDouble() * 360f);
                float sc = cardSize * (0.8f + (float)rng.NextDouble() * 0.5f);
                go.transform.localScale = new Vector3(sc, sc, 1f);
                var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = m; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = true;
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.OccludeeStatic);
                renderers.Add(r);
            }
            // 한 메시로 합침(나무 하나 = 카드 렌더러 하나 → 그리기 호출·SetPass 줄임). 메시 에셋 Art/Env/Props/Cards_<나무>.asset
            var combine = renderers.Select(r => new CombineInstance { mesh = r.GetComponent<MeshFilter>().sharedMesh, transform = parent.worldToLocalMatrix * r.transform.localToWorldMatrix }).ToArray();
            string meshPath = $"{PropDir}/Cards_{tree}.asset";
            var merged = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (merged == null) { merged = new Mesh { name = "Cards_" + tree }; AssetDatabase.CreateAsset(merged, meshPath); }
            merged.Clear(); merged.CombineMeshes(combine, true, true); merged.RecalculateBounds(); EditorUtility.SetDirty(merged);
            foreach (var r in renderers) Object.DestroyImmediate(r.gameObject);
            parent.gameObject.AddComponent<MeshFilter>().sharedMesh = merged;
            var mr = parent.gameObject.AddComponent<MeshRenderer>(); mr.sharedMaterial = m; mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = true;
            GameObjectUtility.SetStaticEditorFlags(parent.gameObject, StaticEditorFlags.OccludeeStatic);
            if (lods.Count > 0) { var l0 = lods[0]; l0.renderers = l0.renderers.Concat(new Renderer[] { mr }).ToArray(); lods[0] = l0; }
            return n;
        }

        // ───────────────────────── 필로티 콜라이더: 상자 하나 → 위층 상자 + 기둥 + 안쪽 벽 + 옆 벽 + 철책(통로 1.3m 비움). 레이어 Wall 그대로
        static int PilotiColliders(Transform zoneRoot, Dictionary<string, Dictionary<string, object>> objs)
        {
            int n = 0;
            var houses = zoneRoot.Find("Blocks/house"); if (houses == null) return 0;
            foreach (var kv in objs.Where(x => S(x.Value, "kind") == "building"))
            {
                var inf = D(G(kv.Value, "info")); var meta = D(G(inf, "meta"));
                var cols = L(G(meta, "piloti_cols")); if (cols == null) continue;
                string block = S(inf, "block");
                var tr = houses.Cast<Transform>().FirstOrDefault(t => t.name == block);
                if (tr == null) { Notes.Add($"필로티 블록 못 찾음: {block}"); continue; }
                var box = tr.GetComponent<BoxCollider>(); if (box == null) continue;
                var c = V3(G(inf, "center")); var sz = V3(G(inf, "size")); float yaw = F(G(inf, "yaw"));
                float y0 = F(G(meta, "piloti_y0"), c.y - sz.y * 0.5f), yc = F(G(meta, "piloti_yc"), y0 + 2.7f), top = c.y + sz.y * 0.5f;
                var outN = V3(G(meta, "piloti_out")).normalized;
                var front = L(G(meta, "piloti_front")); var back = L(G(meta, "piloti_back")); var gate = V3(G(meta, "piloti_gate")); float gap = F(G(meta, "piloti_gap"), 1.3f);
                Vector3 f0 = V3(front[0]), f1 = V3(front[1]), b0 = V3(back[0]), b1 = V3(back[1]);
                var rot = Quaternion.Euler(0f, yaw, 0f);
                var holder = new GameObject(block + " 콜라이더(필로티)"); holder.layer = tr.gameObject.layer; holder.transform.SetParent(houses, false);
                void Add(string name, Vector3 center, Vector3 size, Quaternion r)
                {
                    var go = new GameObject(name) { layer = tr.gameObject.layer }; go.transform.SetParent(holder.transform, false);
                    go.transform.SetPositionAndRotation(center, r); go.AddComponent<BoxCollider>().size = size;
                    GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
                }
                // 블록 바닥(상자 아래 0.5m 더)·위층: 블록 상자와 같은 발자국
                float bottom = tr.TransformPoint(box.center - new Vector3(0f, box.size.y * 0.5f, 0f)).y;
                Add("위층", new Vector3(c.x, (yc + top) * 0.5f, c.z), new Vector3(sz.x, top - yc, sz.z), rot);
                // 안쪽 벽 뒤(현관 뒤 ~ 블록 뒤까지)
                var backMid = (b0 + b1) * 0.5f; float W = Vector3.Distance(f0, f1), Dp = Vector3.Distance(f0, b0);
                float depth = Mathf.Abs(Vector3.Dot(rot * Vector3.forward, outN)) > 0.7f ? sz.z : sz.x;
                float rest = Mathf.Max(0.5f, depth - Dp);
                var rotFront = Quaternion.LookRotation(outN, Vector3.up);
                Add("안쪽 벽", new Vector3(backMid.x, (bottom + yc) * 0.5f, backMid.z) - outN * (rest * 0.5f), new Vector3(W, yc - bottom, rest), rotFront);
                // 옆 벽 2
                foreach (var (fa, ba, nm) in new[] { (f0, b0, "옆 벽 A"), (f1, b1, "옆 벽 B") })
                {
                    var mid = (fa + ba) * 0.5f; var inward = ((f0 + f1) * 0.5f - fa); inward.y = 0f; inward = inward.normalized;
                    Add(nm, new Vector3(mid.x, (bottom + yc) * 0.5f, mid.z) + inward * 0.2f, new Vector3(0.4f, yc - bottom, Dp), rotFront);
                }
                // 기둥
                int k = 0;
                foreach (var co in cols) { var p = V3(co); Add("기둥 " + k++, new Vector3(p.x, (bottom + yc) * 0.5f, p.z), new Vector3(0.5f, yc - bottom, 0.5f), rotFront); }
                // 철책 2 구간(통로 비움)
                var t = (f1 - f0).normalized; float half = gap * 0.5f;
                var g0 = gate - t * half; var g1 = gate + t * half;
                float lenA = Vector3.Distance(f0, g0), lenB = Vector3.Distance(g1, f1);
                Add("철책 A", (f0 + g0) * 0.5f + Vector3.up * (y0 + 0.8f) - outN * 0.05f + new Vector3(0f, -y0, 0f) + new Vector3(0f, y0, 0f), new Vector3(lenA, 1.6f, 0.12f), rotFront);
                Add("철책 B", (g1 + f1) * 0.5f + Vector3.up * (y0 + 0.8f) - outN * 0.05f + new Vector3(0f, -y0, 0f) + new Vector3(0f, y0, 0f), new Vector3(lenB, 1.6f, 0.12f), rotFront);
                Object.DestroyImmediate(box);
                Notes.Add($"필로티 콜라이더 교체: {block} — 위층·안쪽 벽·옆 벽 2·기둥 {k}·철책 2(통로 {gap}m)");
                n++;
            }
            return n;
        }
    }
}
