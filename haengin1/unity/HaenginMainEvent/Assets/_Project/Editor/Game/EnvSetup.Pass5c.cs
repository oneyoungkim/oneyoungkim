// 행인1의 메인이벤트 — 맵 아트 패스 5차(2026-10-25) ③ 거리 데코 · ④ 표면 디테일 · ⑤ 움직임(천·비둘기·고양이·날벌레) — 프레임 대조 목록(docs/09 6-5-3 5차)에서 빠진 것 채우기
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
        // ═════════════════════════ ③ 거리 데코
        static string Street5(Zone1Data d, Transform zoneRoot, Transform env, Dictionary<string, Dictionary<string, object>> objs, Func<string, GameObject> prefab)
        {
            var root = new GameObject("Street5").transform; root.SetParent(env, false);
            int n = 0;
            var rng = new System.Random(20261025);
            Landmark5 Lm(string name) { var l = d.Landmarks.FirstOrDefault(x => x.Name.Contains(name)); return l == null ? null : new Landmark5 { Pos = l.Pos, N = ToRoadDir(d, l.Pos) }; }
            // 1) 하루편의점 앞: 탁자 세트 2 · 접은 파라솔 · 아이스크림 냉동고 · 플라스틱 상자 더미(f08)
            var conv = Lm("하루편의점 후문점 출입문");
            if (conv != null)
            {
                var right = Vector3.Cross(Vector3.up, conv.N).normalized;
                var p0 = conv.Pos;
                PutProp(prefab, "ice_cream_freezer", root, GroundAt(p0 + right * 1.5f + conv.N * 0.55f), Mathf.Atan2(conv.N.x, conv.N.z) * Mathf.Rad2Deg, "냉동고 ← 하루편의점");
                PutProp(prefab, "plastic_crates_stack", root, GroundAt(p0 + right * 2.45f + conv.N * 0.35f), rng.Next(360), "상자 더미 ← 하루편의점");
                PutProp(prefab, "conv_table_set", root, GroundAt(p0 - right * 1.9f + conv.N * 1.0f), rng.Next(360), "탁자 ← 하루편의점");
                PutProp(prefab, "parasol_folded", root, GroundAt(p0 - right * 1.9f + conv.N * 1.0f), 0f, "파라솔 ← 하루편의점");
                n += 4;
            }
            // 2) 세워 둔 차: 1톤 트럭(성균관로 동쪽 갓길) · 배달 스쿠터(국밥집 앞 연석) · 자전거(골목)
            var main = d.Roads[0];
            {
                var (p, dir) = Along(main.Points, Length(main.Points) * 0.72f); var side = Vector3.Cross(Vector3.up, dir).normalized;
                PutProp(prefab, "truck_1ton_box", root, GroundAt(p - side * (main.Width * 0.5f - 1.15f)), Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, "1톤 트럭(갓길)");
                n++;
            }
            var guk = Lm("성대후문 국밥 출입문");
            if (guk != null)
            {
                var right = Vector3.Cross(Vector3.up, guk.N).normalized;
                PutProp(prefab, "delivery_scooter", root, GroundAt(guk.Pos + guk.N * 2.3f - right * 2.2f), Mathf.Atan2(right.x, right.z) * Mathf.Rad2Deg + 8f, "배달 스쿠터 ← 국밥집");
                PutProp(prefab, "cardboard_boxes", root, GroundAt(guk.Pos - right * 1.0f + guk.N * 0.4f), rng.Next(360), "배달 상자 ← 국밥집");
                n += 2;
            }
            foreach (var r in d.Roads.Where(x => x.Kind == "alley").Take(3))
            {
                var (p, dir) = Along(r.Points, Length(r.Points) * 0.55f); var side = Vector3.Cross(Vector3.up, dir).normalized;
                var pos = GroundAt(p + side * (r.Width * 0.5f - 0.35f));
                if (Mathf.Abs(pos.y - p.y) < 0.5f) { PutProp(prefab, "delivery_bicycle", root, pos, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg + 4f, "자전거 ← " + r.Name); n++; }
            }
            // 3) 반사경(골목 입구·꺾인 곳) · 라바콘(주차장 진입로) · 볼라드(후문 앞 인도)
            foreach (var r in d.Roads.Where(x => x.Kind == "alley" || x.Kind == "driveway"))
            {
                var a = r.Points[0]; var dir = Flat(r.Points[1] - a).normalized; var side = Vector3.Cross(Vector3.up, dir).normalized;
                var pos = GroundAt(a + dir * 1.2f + side * (r.Width * 0.5f + 0.35f));
                if (zoneRoot != null && Physics.CheckSphere(pos + Vector3.up * 1f, 0.3f, 1 << Layers.Wall)) pos = GroundAt(a + dir * 1.2f - side * (r.Width * 0.5f + 0.35f));
                PutProp(prefab, "traffic_mirror", root, pos, Mathf.Atan2(-dir.x, -dir.z) * Mathf.Rad2Deg + 25f, "반사경 ← " + r.Name);
                n++;
            }
            var drive = d.Roads.FirstOrDefault(x => x.Name.Contains("주차장 진입로"));
            if (drive != null)
            {
                var a = drive.Points[0]; var dir = Flat(drive.Points[drive.Points.Length - 1] - a).normalized; var side = Vector3.Cross(Vector3.up, dir).normalized;
                for (int k = 0; k < 2; k++) { PutProp(prefab, "traffic_cones", root, GroundAt(a + dir * (2.5f + k * 1.4f) + side * (drive.Width * 0.5f - 0.5f)), rng.Next(360), "라바콘 " + k); n++; }
            }
            {
                var (cp, cdir) = Along(main.Points, 0f); float bestD = float.MaxValue;
                for (float s = 0f; s < Length(main.Points); s += 0.5f) { var (p, dr) = Along(main.Points, s); float dd = Mathf.Abs(p.x + 197.5f); if (dd < bestD) { bestD = dd; cp = p; cdir = dr; } }
                var side = Vector3.Cross(Vector3.up, cdir).normalized;
                var dark = Flat("M_Bollard", new Color(0.24f, 0.25f, 0.27f)); var white = Flat("M_Paint_White", new Color(0.93f, 0.92f, 0.88f));
                foreach (float sg in new[] { 1f, -1f })
                    for (int k = -1; k <= 1; k++)
                    {
                        var p = GroundAt(cp + side * sg * (main.Width * 0.5f + 0.35f) + cdir * (k * 1.3f + 4.2f));
                        var b = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Object.DestroyImmediate(b.GetComponent<Collider>());
                        b.name = "볼라드"; b.transform.SetParent(root, false); b.transform.position = p + Vector3.up * 0.4f; b.transform.localScale = new Vector3(0.16f, 0.4f, 0.16f);
                        b.GetComponent<MeshRenderer>().sharedMaterial = dark; GameObjectUtility.SetStaticEditorFlags(b, StaticEditorFlags.BatchingStatic);
                        Box("볼라드 띠", root, p + Vector3.up * 0.62f, new Vector3(0.165f, 0.06f, 0.165f), Quaternion.identity, white);
                        n++;
                    }
            }
            // 4) 전봇대 안전띠(노랑·검정 사선) · 변압기(성균관로 전봇대 2개)
            var stripeTex = DiagStripe();
            var stripeMat = Toon($"{Sign5MatDir}/M_PoleGuard.mat", stripeTex, new Vector2(3f, 2f), Vector2.zero, Color.white);
            var poles = objs.Where(x => S(x.Value, "kind") == "pole").OrderBy(x => x.Key, StringComparer.Ordinal).ToList();
            int pk = 0;
            foreach (var kv in poles)
            {
                var inf = D(G(kv.Value, "info")); var bp = V3(G(inf, "pos")); if (bp == Vector3.zero) continue;
                var g = GroundAt(bp, 3f, 6f);
                var s = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Object.DestroyImmediate(s.GetComponent<Collider>());
                s.name = "안전띠 " + kv.Key; s.transform.SetParent(root, false); s.transform.position = g + Vector3.up * 1.05f; s.transform.localScale = new Vector3(0.43f, 0.75f, 0.43f);
                s.GetComponent<MeshRenderer>().sharedMaterial = stripeMat; GameObjectUtility.SetStaticEditorFlags(s, StaticEditorFlags.BatchingStatic);
                if ((pk++ % 3) == 1)
                {
                    var tm = Flat("M_Transformer", new Color(0.62f, 0.64f, 0.66f));
                    var toward = ToRoadDir(d, bp); var sd = Vector3.Cross(Vector3.up, toward).normalized;
                    foreach (float sg in new[] { -1f, 1f })
                    {
                        var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Object.DestroyImmediate(c.GetComponent<Collider>());
                        c.name = "변압기"; c.transform.SetParent(root, false); c.transform.position = g + Vector3.up * 7.3f + sd * sg * 0.42f - toward * 0.1f; c.transform.localScale = new Vector3(0.48f, 0.42f, 0.48f);
                        c.GetComponent<MeshRenderer>().sharedMaterial = tm; c.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On; GameObjectUtility.SetStaticEditorFlags(c, StaticEditorFlags.BatchingStatic);
                        Box("변압기 받침", root, g + Vector3.up * 6.82f + sd * sg * 0.22f - toward * 0.1f, new Vector3(0.5f, 0.06f, 0.08f), Quaternion.LookRotation(toward, Vector3.up), Flat("M_Iron_Dark", new Color(0.30f, 0.30f, 0.31f)));
                    }
                }
                n++;
            }
            // 5) 평상(시우네 골목) · 벽화(주차장 동쪽 옹벽) · 현수막 2 · 가로등 배너
            {
                var p = GroundAt(new Vector3(-166.6f, 28f, 118.6f)); var nn = ToRoadDir(d, p);
                var wood = Flat("M_Wood_Floor", new Color(0.55f, 0.40f, 0.27f));
                var rot = Quaternion.LookRotation(nn, Vector3.up);
                Box("평상 판", root, p + Vector3.up * 0.42f, new Vector3(1.7f, 0.08f, 1.15f), rot, wood, true);
                foreach (var (sx, sz) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
                    Box("평상 다리", root, p + rot * new Vector3(sx * 0.75f, 0.2f, sz * 0.5f), new Vector3(0.08f, 0.4f, 0.08f), rot, wood);
                n++;
            }
            var pad = d.Pads.FirstOrDefault(x => x.Name.Contains("주차장"));
            if (pad != null && Faces5().ContainsKey("mural"))
            {
                var prot = Quaternion.Euler(0f, pad.Yaw, 0f);
                var wallP = pad.Center + prot * new Vector3(pad.Size.x * 0.5f - 0.02f, 0f, -0.5f);   // 옹벽 안쪽 면 바로 앞(2차 낙서 판 자리)
                var gnd = GroundAt(wallP - prot * Vector3.right * 0.6f, 3f, 6f);
                float mh = 1.55f, mw = mh * Faces5()["mural"].Aspect;
                var mm = SignMat("mural", 0f, 0.55f, 2f);
                MeshObj("벽화(주차장 동쪽 옹벽)", root, FaceMesh("mural", Mathf.Min(mw, pad.Size.y - 1.5f), mh, Faces5()["mural"].Aspect), mm,
                        new Vector3(wallP.x, gnd.y + 0.15f + mh / 2, wallP.z), Quaternion.LookRotation(prot * Vector3.right, Vector3.up));
                // 2차 낙서 판(같은 자리)은 끔
                var walls = env.Find("Decor/벽"); if (walls != null) foreach (Transform t in walls) if (t.name.StartsWith("낙서 옹벽(동)")) t.gameObject.SetActive(false);
                n++;
            }
            n += Banners5(d, root, objs);
            n += Fills5(d, zoneRoot, root, objs, prefab);
            return $"거리 데코 {n}";
        }

        /// 프레임 대조 목록에서 빠진 것: 국밥집 뒤 주방 배기 덕트(f02) · 필로티 기둥 노랑·검정 띠(f04) · 감나무(f04) · 인도 화분 상자(f07)
        static int Fills5(Zone1Data d, Transform zoneRoot, Transform root, Dictionary<string, Dictionary<string, object>> objs, Func<string, GameObject> prefab)
        {
            int n = 0;
            var alu = Flat("M_Duct_Alu", new Color(0.78f, 0.79f, 0.80f));
            var pad = d.Pads.FirstOrDefault(x => x.Name.Contains("주차장"));
            if (pad != null)
            {
                var rot = Quaternion.Euler(0f, pad.Yaw, 0f); float west = -pad.Size.x * 0.5f;
                var basePt = GroundAt(pad.Center + rot * new Vector3(west + 0.3f, 0f, 3.65f), 3f, 6f);
                var outward = rot * Vector3.right;
                var wallPt = basePt - outward * 0.3f;
                void Cyl(string nm, Vector3 a, Vector3 b, float r)
                {
                    var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Object.DestroyImmediate(c.GetComponent<Collider>());
                    c.name = nm; c.transform.SetParent(root, false); c.transform.position = (a + b) / 2; c.transform.up = (b - a).normalized; c.transform.localScale = new Vector3(r * 2, (b - a).magnitude / 2, r * 2);
                    c.GetComponent<MeshRenderer>().sharedMaterial = alu; c.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On; GameObjectUtility.SetStaticEditorFlags(c, StaticEditorFlags.BatchingStatic);
                }
                var p0 = wallPt + Vector3.up * 2.5f; var p1 = p0 + outward * 0.45f; var p2 = p1 + Vector3.up * 2.4f;
                Cyl("배기 덕트(벽)", p0, p1 + outward * 0.14f, 0.15f); Cyl("배기 덕트(세움)", p1 - Vector3.up * 0.14f, p2, 0.15f);
                Box("덕트 갓", root, p2 + Vector3.up * 0.12f, new Vector3(0.48f, 0.05f, 0.48f), Quaternion.identity, alu, true);
                Box("환기 후드", root, wallPt + Vector3.up * 2.65f + outward * 0.12f + rot * Vector3.forward * 1.25f, new Vector3(0.5f, 0.32f, 0.26f), Quaternion.LookRotation(outward, Vector3.up), alu, true);
                n += 2;
            }
            // 필로티 기둥 안전띠(키트 meta piloti_cols)
            var stripeMat = AssetDatabase.LoadAssetAtPath<Material>($"{Sign5MatDir}/M_PoleGuard.mat");
            foreach (var kv in objs.Where(x => S(x.Value, "kind") == "building"))
            {
                var meta = D(G(D(G(kv.Value, "info")), "meta")); var cols = L(G(meta, "piloti_cols")); if (cols == null || stripeMat == null) continue;
                float y0 = F(G(meta, "piloti_y0"));
                foreach (var co in cols)
                {
                    var p = V3(co); p.y = y0;
                    Box("필로티 안전띠", root, p + Vector3.up * 0.55f, new Vector3(0.43f, 1.0f, 0.43f), Quaternion.Euler(0f, F(G(D(G(kv.Value, "info")), "yaw")), 0f), stripeMat);
                    n++;
                }
            }
            // 감나무 2: 마당 집(yard) 앞 담 안쪽 모서리
            if (prefab("persimmon_tree") != null)
            {
                int k = 0;
                foreach (var kv in objs.Where(x => S(x.Value, "kind") == "building").OrderBy(x => x.Key, StringComparer.Ordinal))
                {
                    if (k >= 2) break;
                    var inf = D(G(kv.Value, "info")); var meta = D(G(inf, "meta"));
                    if ((S(meta, "variant") ?? "") != "yard") continue;
                    var roles = D(G(inf, "roles")); string ff = roles.FirstOrDefault(r => r.Value?.ToString() == "front").Key; if (ff == null) continue;
                    var c = V3(G(inf, "center")); var sz = V3(G(inf, "size")); var rot = Quaternion.Euler(0f, F(G(inf, "yaw")), 0f);
                    var nf = FaceN(ff); float hf = Mathf.Abs(nf.x) > 0.5f ? sz.x / 2 : sz.z / 2; float hs = Mathf.Abs(nf.x) > 0.5f ? sz.z / 2 : sz.x / 2;
                    var side = Vector3.Cross(Vector3.up, nf);
                    var pos = GroundAt(c + rot * (nf * (hf - 0.75f) + side * (hs - 0.7f)), 6f, 12f);
                    PutProp(prefab, "persimmon_tree", root, pos, (Hash(kv.Key) % 360), "감나무 ← " + S(inf, "block"), Vector3.one * 0.95f);
                    k++; n++;
                }
            }
            // 인도 화분 상자(후문 앞 북쪽 인도 연석 쪽 3)
            var sw = d.Roads.FirstOrDefault(x => x.Name.Contains("북쪽 인도"));
            if (sw != null)
            {
                var conc = Flat("M_Planter_Conc", new Color(0.80f, 0.79f, 0.75f));
                foreach (float f in new[] { 0.18f, 0.24f, 0.62f })
                {
                    var (p, dir) = Along(sw.Points, Length(sw.Points) * f); var side = Vector3.Cross(Vector3.up, dir).normalized;
                    float sg = Vector3.Dot(side, ToRoadDir(d, p)) > 0 ? 1f : -1f;
                    var pos = GroundAt(p + side * sg * (sw.Width * 0.5f - 0.35f));
                    Box("화분 상자", root, pos + Vector3.up * 0.25f, new Vector3(1.2f, 0.5f, 0.45f), Quaternion.LookRotation(dir, Vector3.up), conc, true);
                    PutProp(prefab, "shrub_bush", root, pos + Vector3.up * 0.48f, Hash(f.ToString()) % 360, "화분 상자 덤불", new Vector3(0.6f, 0.5f, 0.4f));
                    n++;
                }
            }
            return n;
        }
        sealed class Landmark5 { public Vector3 Pos, N; }

        static Texture2D DiagStripe()
        {
            EnsureFolder(Sign5Dir + "/Gen");
            string path = $"{Sign5Dir}/Gen/PoleGuard.png";
            if (!File.Exists(path))
            {
                var t = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                for (int x = 0; x < 64; x++) for (int y = 0; y < 64; y++) t.SetPixel(x, y, ((x + y) / 16) % 2 == 0 ? new Color(0.93f, 0.78f, 0.12f) : new Color(0.12f, 0.12f, 0.13f));
                t.Apply(); File.WriteAllBytes(path, t.EncodeToPNG()); Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material ClothMat(string key, Texture tex, Color tint, float amp, float pin)
        {
            EnsureFolder(Sign5MatDir);
            var sh = Shader.Find("Haengin/ToonCloth") ?? throw new Exception("Haengin/ToonCloth 셰이더 없음");
            string path = $"{Sign5MatDir}/M_Cloth_{key}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(sh) { name = "M_Cloth_" + key }; AssetDatabase.CreateAsset(m, path); }
            if (m.shader != sh) m.shader = sh;
            m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", tint); m.SetColor("_ShadeColor", new Color(0.8f, 0.78f, 0.8f));
            m.SetFloat("_WindAmp", amp); m.SetFloat("_WindFreq", 1.7f); m.SetFloat("_Cutoff", 0.3f); m.SetFloat("_Pin", pin);
            m.enableInstancing = true; EditorUtility.SetDirty(m);
            return m;
        }

        /// 천 판(위 가운데 top, 법선 n, 폭 w·높이 h) — uv.y 1 = 위(묶인 끝)
        static GameObject ClothQuad(string name, Transform parent, Vector3 top, Vector3 n, float w, float h, Material m)
        {
            var go = Quad(name, parent, top - Vector3.up * (h / 2), n, w, h, m, 0f);
            var r = go.GetComponent<MeshRenderer>(); r.shadowCastingMode = ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.OccludeeStatic);   // 정적 배칭이면 정점이 합쳐져 바람 기준(uv)이 깨지지 않지만 그림자·인스턴싱 위해 배칭 끔
            return go;
        }

        static int Banners5(Zone1Data d, Transform root, Dictionary<string, Dictionary<string, object>> objs)
        {
            int n = 0;
            // 현수막: 주차장 진입로 옆 난간/옹벽 위(주정차 단속) · 명륜3가 골목 담장(골목 대청소)
            void Banner(string face, Vector3 top, Vector3 nn, float w)
            {
                if (!Faces5().ContainsKey(face)) return;
                float h = w / Faces5()[face].Aspect;
                var m = ClothMat(face, AssetDatabase.LoadAssetAtPath<Texture2D>($"{Sign5Dir}/Face_{face}.png"), Color.white, 0.05f, 1f);
                ClothQuad("현수막 " + face, root, top, nn, w, h, m);
                var right = Vector3.Cross(Vector3.up, nn).normalized;
                foreach (float s in new[] { -0.5f, 0.5f })
                    foreach (float yy in new[] { 0f, -h })
                        Box("현수막 끈", root, top + right * (w * s) + Vector3.up * yy - nn * 0.05f, new Vector3(0.02f, 0.02f, 0.12f), Quaternion.LookRotation(nn, Vector3.up), Flat("M_Wire_Rope", new Color(0.85f, 0.85f, 0.82f)));
                n++;
            }
            var pad = d.Pads.FirstOrDefault(x => x.Name.Contains("주차장"));
            if (pad != null)
            {
                var prot = Quaternion.Euler(0f, pad.Yaw, 0f);
                var top = pad.Center + prot * new Vector3(-1.5f, 0f, -pad.Size.y * 0.5f - 0.08f); top = GroundAt(top + prot * Vector3.forward * 0.6f, 3f, 6f) + Vector3.up * 2.05f;
                Banner("banner_parking", top - prot * Vector3.forward * 0.6f, prot * Vector3.forward, 2.8f);
            }
            var fence = objs.Where(x => S(x.Value, "kind") == "fences").SelectMany(x => L(G(D(G(x.Value, "info")), "blocks")) ?? new List<object>())
                            .Select(D).Where(b => (S(b, "btype") ?? "") == "fence_wall").OrderByDescending(b => V3(G(b, "size")).x).FirstOrDefault();
            if (fence != null)
            {
                var c = V3(G(fence, "center")); var sz = V3(G(fence, "size")); var rot = Quaternion.Euler(0f, F(G(fence, "yaw")), 0f);
                var nn = rot * Vector3.forward; if (Vector3.Dot(nn, ToRoadDir(d, c)) < 0f) nn = -nn;
                var top = c + nn * (sz.z * 0.5f + 0.04f); top.y = c.y + sz.y * 0.5f - 0.12f;
                Banner("banner_clean", top, nn, Mathf.Min(2.6f, sz.x - 0.3f));
            }
            // 가로등 배너(성곽길·공터 가로등): 파랑/빨강 무늬 세로 깃발(글자 없음) — 위아래 묶임
            var flagTex = FlagTex();
            var fm = ClothMat("flag", flagTex, Color.white, 0.04f, 1f);
            foreach (var l in d.Landmarks.Where(x => x.Kind == "lamp" && (x.Name.Contains("성곽길") || x.Name.Contains("공터"))))
            {
                var g = GroundAt(l.Pos, 3f, 6f); var nn = ToRoadDir(d, l.Pos); var side = Vector3.Cross(Vector3.up, nn).normalized;
                ClothQuad("가로등 배너", root, g + Vector3.up * 3.6f + side * 0.32f, nn, 0.48f, 1.25f, fm);
                Box("배너 막대", root, g + Vector3.up * 3.62f + side * 0.2f, new Vector3(0.025f, 0.025f, 0.55f), Quaternion.LookRotation(side, Vector3.up), SignBoltMat);
                n++;
            }
            return n;
        }

        static Texture2D FlagTex()
        {
            EnsureFolder(Sign5Dir + "/Gen");
            string path = $"{Sign5Dir}/Gen/PoleFlag.png";
            if (!File.Exists(path))
            {
                var t = new Texture2D(64, 160, TextureFormat.RGBA32, false);
                var bg = new Color(0.93f, 0.92f, 0.88f); var blue = new Color(0.47f, 0.60f, 0.78f); var red = new Color(0.85f, 0.42f, 0.36f);
                for (int x = 0; x < 64; x++) for (int y = 0; y < 160; y++)
                {
                    var c = bg;
                    if (y > 110) c = blue;
                    float dx = x - 32, dy = y - 62; if (dx * dx + dy * dy < 22 * 22) c = red;
                    if (x < 3 || x > 60) c = c * 0.85f;
                    t.SetPixel(x, y, c);
                }
                t.Apply(); File.WriteAllBytes(path, t.EncodeToPNG()); Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ═════════════════════════ ④ 표면 디테일(decals5 3×3 칸)
        static Material Decal5(int cell, bool multiply, float alpha, string suffix = "")
        {
            EnsureFolder(DecalMatDir);
            var sh = Shader.Find("Haengin/DecalQuad") ?? throw new Exception("Haengin/DecalQuad 셰이더 없음");
            string path = $"{DecalMatDir}/M_D5_{cell}{(multiply ? "_mul" : "")}{suffix}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(sh) { name = Path.GetFileNameWithoutExtension(path) }; AssetDatabase.CreateAsset(m, path); }
            if (m.shader != sh) m.shader = sh;
            m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Sign5Dir}/Face_decals5.png"));
            int cx = cell % 3, cy = cell / 3;
            m.SetTextureScale("_MainTex", new Vector2(1f / 3f, 1f / 3f)); m.SetTextureOffset("_MainTex", new Vector2(cx / 3f, 1f - (cy + 1) / 3f));
            m.SetColor("_Tint", new Color(1, 1, 1, alpha)); m.SetFloat("_Cull", 2f);
            m.SetFloat("_Multiply", multiply ? 1f : 0f);
            m.SetFloat("_SrcBlend", multiply ? (float)BlendMode.DstColor : (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", multiply ? (float)BlendMode.Zero : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_Fade", 0.15f); m.SetFloat("_EdgeRough", 0.2f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static string Surface5(Zone1Data d, Transform zoneRoot, Transform env, Dictionary<string, Dictionary<string, object>> objs)
        {
            if (!Faces5().ContainsKey("decals5")) return "표면 데칼 없음";
            var root = new GameObject("Surface5").transform; root.SetParent(env, false);
            int streaks = 0, road = 0, cracks = 0, stairs = 0, moss = 0;
            var rng = new System.Random(77);
            // 창 아래 빗물·녹물(키트 streak 소켓)
            foreach (var (t, inf) in socks5.Where(s => S(s.inf, "sock_kind") == "streak"))
            {
                var p = V3(G(inf, "pos")); var n = V3(G(inf, "normal")).normalized; var sz = L(G(inf, "size"));
                float w = sz != null ? F(sz[0]) : 1f, len = sz != null ? F(sz[1]) : 1f;
                if (len < 0.35f) continue;
                bool rust = S(inf, "tenant") == "rust";
                var m = rust ? Decal5(1, false, 0.75f) : Decal5(0, true, 0.7f);
                float ww = rust ? Mathf.Min(0.35f, w * 0.35f) : w * 0.85f, hh = Mathf.Min(len, rust ? 0.9f : 1.5f);
                var right = Vector3.Cross(Vector3.up, n).normalized;
                var pos = p - Vector3.up * (hh / 2) + (rust ? right * ((float)rng.NextDouble() - 0.5f) * w * 0.6f : Vector3.zero);
                Quad(rust ? "녹물" : "빗물 자국", root, pos, n, ww, hh, m, 0.006f);
                streaks++;
            }
            // 큰길·주차장·진입로: 아스팔트 균열(3) · 땜질(4)
            foreach (var r in d.Roads.Where(x => x.Kind == "road" || x.Kind == "driveway"))
            {
                float L = Length(r.Points);
                for (float s = 5f; s < L - 3f; s += 7f + (float)rng.NextDouble() * 6f)
                {
                    var (p, dir) = Along(r.Points, s); var side = Vector3.Cross(Vector3.up, dir).normalized;
                    bool patch = rng.NextDouble() < 0.4;
                    var pos = GroundAt(p + side * ((float)rng.NextDouble() - 0.5f) * (r.Width - 1.5f)) + Vector3.up * 0.012f;
                    float sz = patch ? 1.2f + (float)rng.NextDouble() * 1.4f : 1.4f + (float)rng.NextDouble() * 1.6f;
                    var go = Quad(patch ? "아스팔트 땜질" : "아스팔트 균열", root, pos, Vector3.up, sz, sz * (0.7f + (float)rng.NextDouble() * 0.5f), patch ? Decal5(4, true, 0.8f) : Decal5(3, true, 0.85f), 0f);
                    go.transform.rotation = Quaternion.Euler(90f, (float)rng.NextDouble() * 360f, 0f);
                    road++;
                }
            }
            // 인도·골목: 틈 풀(5) · 담·옹벽 밑 이끼(7)
            foreach (var r in d.Roads.Where(x => x.Kind == "sidewalk" || x.Kind == "alley" || x.Kind == "path"))
            {
                float L = Length(r.Points);
                for (float s = 2f; s < L - 1f; s += 5f + (float)rng.NextDouble() * 5f)
                {
                    var (p, dir) = Along(r.Points, s); var side = Vector3.Cross(Vector3.up, dir).normalized * (rng.NextDouble() < 0.5 ? 1f : -1f);
                    bool edge = rng.NextDouble() < 0.6;
                    var pos = GroundAt(p + side * (edge ? r.Width * 0.5f - 0.2f : ((float)rng.NextDouble() - 0.5f) * r.Width * 0.6f)) + Vector3.up * 0.013f;
                    if (Mathf.Abs(pos.y - p.y) > 0.4f) continue;
                    bool mossy = edge && rng.NextDouble() < 0.35;
                    var go = Quad(mossy ? "이끼" : "틈 풀", root, pos, Vector3.up, mossy ? 0.7f : 1.0f, mossy ? 0.55f : 0.5f, mossy ? Decal5(7, false, 0.85f) : Decal5(5, false, 0.95f), 0f);
                    go.transform.rotation = Quaternion.Euler(90f, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg + (float)rng.NextDouble() * 40f - 20f, 0f);
                    if (mossy) moss++; else cracks++;
                }
            }
            // 계단 모서리 닳음(6): 단마다 앞 모서리에 가는 띠
            foreach (var st in d.Stairs)
            {
                Vector3 lo = st.From.y <= st.To.y ? st.From : st.To, hi = st.From.y <= st.To.y ? st.To : st.From;
                var dir = Flat(hi - lo).normalized; var side = Vector3.Cross(Vector3.up, dir).normalized;
                int steps = Mathf.Max(1, st.Steps);
                for (int k = 1; k <= steps; k++)
                {
                    if (rng.NextDouble() < 0.35) continue;
                    var p = Vector3.Lerp(lo, hi, (k - 0.5f) / steps);
                    float rise = (hi.y - lo.y) / steps;
                    var nose = new Vector3(p.x, lo.y + rise * k + 0.004f, p.z) - dir * (Flat(hi - lo).magnitude / steps * 0.5f - 0.06f) + side * ((float)rng.NextDouble() - 0.5f) * st.Width * 0.3f;
                    var go = Quad("계단 닳음", root, nose, Vector3.up, st.Width * (0.4f + (float)rng.NextDouble() * 0.4f), 0.12f, Decal5(6, false, 0.8f), 0f);
                    go.transform.rotation = Quaternion.LookRotation(Vector3.down, dir);
                    stairs++;
                }
            }
            return $"표면: 빗물·녹물 {streaks} · 아스팔트 {road} · 틈 풀 {cracks} · 이끼 {moss} · 계단 닳음 {stairs}";
        }

        // ═════════════════════════ ⑤ 움직임: 빨래 천(키트 cloth 소켓) · 비둘기 · 고양이 · 가로등 날벌레
        static string Motion5(Zone1Data d, Transform zoneRoot, Transform env, Func<string, GameObject> prefab)
        {
            var root = new GameObject("Motion5").transform; root.SetParent(env, false);
            int cloth = 0, birds = 0, cats = 0, bugs = 0;
            var fabColor = new Dictionary<string, Color> { ["M_Fabric"] = new Color(0.66f, 0.72f, 0.76f), ["M_Fabric_White"] = new Color(0.91f, 0.89f, 0.85f), ["M_Fabric_Red"] = new Color(0.71f, 0.40f, 0.35f) };
            foreach (var (t, inf) in socks5.Where(s => S(s.inf, "sock_kind") == "cloth"))
            {
                var p = V3(G(inf, "pos")); var n = V3(G(inf, "normal")).normalized; var sz = L(G(inf, "size"));
                string fab = S(inf, "tenant") ?? "M_Fabric";
                var m = ClothMat(fab, null, fabColor.TryGetValue(fab, out var c) ? c : Color.white, 0.07f, 0f);
                ClothQuad("빨래", root, p, n, sz != null ? F(sz[0]) : 0.4f, sz != null ? F(sz[1]) : 0.5f, m);
                cloth++;
            }
            // 비둘기 4(후문 앞 인도·정류장 근처) — 몸·머리·부리·꼬리(기본 도형) + 쪼기
            var grey = Flat("M_Pigeon", new Color(0.55f, 0.57f, 0.62f)); var neck = Flat("M_PigeonNeck", new Color(0.36f, 0.45f, 0.44f)); var beak = Flat("M_PigeonBeak", new Color(0.25f, 0.22f, 0.22f));
            var spots = new[] { new Vector3(-196.2f, 30.6f, 101.2f), new Vector3(-195.4f, 30.6f, 100.6f), new Vector3(-194.6f, 30.5f, 101.5f), new Vector3(-188.5f, 30.0f, 101.3f) };
            var rng = new System.Random(9);
            foreach (var sp in spots)
            {
                var g = GroundAt(sp, 3f, 6f);
                var bird = new GameObject("비둘기"); bird.transform.SetParent(root, false); bird.transform.SetPositionAndRotation(g, Quaternion.Euler(0f, rng.Next(360), 0f));
                GameObject Part(PrimitiveType pt, string nm, Transform parent, Vector3 lp, Vector3 ls, Material m, Quaternion? lr = null)
                {
                    var o = GameObject.CreatePrimitive(pt); Object.DestroyImmediate(o.GetComponent<Collider>()); o.name = nm;
                    o.transform.SetParent(parent, false); o.transform.localPosition = lp; o.transform.localScale = ls; o.transform.localRotation = lr ?? Quaternion.identity;
                    var r = o.GetComponent<MeshRenderer>(); r.sharedMaterial = m; r.shadowCastingMode = ShadowCastingMode.On; return o;
                }
                Part(PrimitiveType.Sphere, "몸", bird.transform, new Vector3(0f, 0.12f, 0f), new Vector3(0.15f, 0.13f, 0.26f), grey);
                Part(PrimitiveType.Cube, "꼬리", bird.transform, new Vector3(0f, 0.13f, -0.16f), new Vector3(0.08f, 0.02f, 0.12f), grey, Quaternion.Euler(-12f, 0f, 0f));
                var headPivot = new GameObject("머리 축"); headPivot.transform.SetParent(bird.transform, false); headPivot.transform.localPosition = new Vector3(0f, 0.17f, 0.09f);
                Part(PrimitiveType.Sphere, "목", headPivot.transform, new Vector3(0f, 0.02f, 0.02f), new Vector3(0.085f, 0.09f, 0.09f), neck);
                Part(PrimitiveType.Sphere, "머리", headPivot.transform, new Vector3(0f, 0.07f, 0.05f), new Vector3(0.075f, 0.075f, 0.08f), grey);
                Part(PrimitiveType.Cube, "부리", headPivot.transform, new Vector3(0f, 0.065f, 0.1f), new Vector3(0.015f, 0.015f, 0.035f), beak);
                foreach (float sx in new[] { -0.03f, 0.03f }) Part(PrimitiveType.Cube, "다리", bird.transform, new Vector3(sx, 0.03f, 0.01f), new Vector3(0.012f, 0.06f, 0.012f), Flat("M_PigeonLeg", new Color(0.72f, 0.38f, 0.36f)));
                var idle = bird.AddComponent<AmbientIdle>(); idle.Kind = AmbientIdle.Mode.Peck; idle.Head = headPivot.transform; idle.Speed = 0.8f + (float)rng.NextDouble() * 0.5f;
                birds++;
            }
            // 고양이 2(담 위·화분 옆) — cat_loaf + 숨쉬기
            foreach (var (pos, yaw) in new[] { (new Vector3(-167.3f, 28f, 118.0f), 210f), (new Vector3(-158.4f, 28f, 121.7f), 40f) })
            {
                var cat = PutProp(prefab, "cat_loaf", root, GroundAt(pos, 3f, 6f), yaw, "고양이");
                if (cat != null) { var idle = cat.AddComponent<AmbientIdle>(); idle.Kind = AmbientIdle.Mode.Breathe; idle.Speed = 1.6f; cats++; }
            }
            // 가로등·보안등 날벌레(밤만, 아주 약하게): 빛 자리 아래 십자 판 2장
            var bugM = GlowMat("bugs", new Color(1f, 0.85f, 0.6f), 0.55f, 1f);
            var lights = zoneRoot.Find("Lights");
            if (lights != null)
                foreach (var s in lights.GetComponentsInChildren<LightSpot>(true).Where(x => x.Kind == LightKind.Street || x.Kind == LightKind.Security))
                {
                    var p = s.transform.position - Vector3.up * 0.45f;
                    for (int k = 0; k < 2; k++)
                    {
                        var q = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.DestroyImmediate(q.GetComponent<Collider>());
                        q.name = "날벌레"; q.transform.SetParent(root, false); q.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, 45f + 90f * k, 0f)); q.transform.localScale = Vector3.one * 1.1f;
                        var r = q.GetComponent<MeshRenderer>(); r.sharedMaterial = bugM; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
                    }
                    bugs++;
                }
            return $"움직임: 빨래 천 {cloth} · 비둘기 {birds} · 고양이 {cats} · 날벌레 {bugs}";
        }
    }
}
