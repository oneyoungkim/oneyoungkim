// 행인1의 메인이벤트 — 맵 아트 패스 5차(2026-10-25) ① 간판: 글자만 떠 있던 간판 → 몸체(절차 메시) + 디자인된 면(힉스필드 GPT Image 2.5 간판 그림 27장 + 2,
//   한국어 글자는 한 장씩 눈으로 확인 — tools/env_assets/specs/signs5*.json · sign_faces.py → Art/Env/Signs5/Face_*.png + faces.json).
//   판류형 띠(알루미늄 테두리·뒤 상자·모서리 볼트·아래 배선관·받침 쇠) · 돌출(양면 상자·위아래 뚜껑·벽 팔) · 모서리 세로 층별 간판(45° 양면) ·
//   채널 문자(레이스웨이 바 + 두께 있는 글자) · 입간판(A형) · 원통 회전 입간판 · LED 띠(밤에 흐름) · 어닝(줄무늬 천 + 앞단 상호) · 셔터(골판 + 페인트 상호) ·
//   창 시트지(2~4층 상가 창, TMP 비닐 글자) · 혜화 MMA 1층 계단 입구(지하 안내 돌출 간판 + 입구 판) · 주소판.
//   면 셰이더 Haengin/SignFace(낡음·밤 조명 상자·LED), 빛 번짐 Haengin/SignGlow(노을·밤, 더하기 — 블룸 없이).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Haengin.EditorTools;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Haengin.EditorGame
{
    public static partial class EnvSetup
    {
        public const string Sign5Dir = EnvDir + "/Signs5", Sign5MatDir = MatDir + "/Signs5", Sign5MeshPath = Sign5Dir + "/SignMeshes.asset";

        sealed class Face5 { public float Aspect; public Color Avg, Glow; }
        static Dictionary<string, Face5> faces5;
        static Dictionary<string, Face5> Faces5()
        {
            if (faces5 != null) return faces5;
            faces5 = new Dictionary<string, Face5>();
            string p = Sign5Dir + "/faces.json";
            if (!File.Exists(p)) return faces5;
            var j = D(MiniJson.Parse(File.ReadAllText(p, System.Text.Encoding.UTF8)));
            foreach (var kv in j)
            {
                var v = D(kv.Value);
                Color Col(string k, Color def) { var l = L(G(v, k)); return l != null && l.Count >= 3 ? new Color(F(l[0]), F(l[1]), F(l[2])) : def; }
                if (AssetDatabase.LoadAssetAtPath<Texture2D>($"{Sign5Dir}/Face_{kv.Key}.png") == null) continue;
                faces5[kv.Key] = new Face5 { Aspect = F(G(v, "aspect"), 1f), Avg = Col("avg", Color.gray), Glow = Col("glow", Color.white) };
            }
            return faces5;
        }
        public static bool Signs5Ready => Faces5().Count >= 20;

        // ───────────────────────── 재질
        static Material SignMat(string face, float emit, float age, float seed, bool led = false)
        {
            EnsureFolder(Sign5MatDir);
            var sh = Shader.Find("Haengin/SignFace") ?? throw new Exception("Haengin/SignFace 셰이더 없음");
            string path = $"{Sign5MatDir}/M_SF_{face}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(sh) { name = "M_SF_" + face }; AssetDatabase.CreateAsset(m, path); }
            if (m.shader != sh) m.shader = sh;
            m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Sign5Dir}/Face_{face}.png"));
            m.SetColor("_Tint", Color.white); m.SetFloat("_Emit", emit); m.SetFloat("_Age", age); m.SetFloat("_Seed", seed);
            m.SetColor("_ShadeColor", new Color(0.86f, 0.85f, 0.85f)); m.SetFloat("_DayDim", 1f); m.SetFloat("_Glass", 0f);
            m.SetFloat("_Cutoff", 0.5f); m.SetFloat("_Led", led ? 1f : 0f); m.SetFloat("_ScrollU", led ? 0.035f : 0f);
            m.SetVector("_LedGrid", new Vector4(260, 18, 0, 0));
            m.enableInstancing = true; EditorUtility.SetDirty(m);
            return m;
        }
        static Material GlowMat(string key, Color c, float strength, float mode = 0f)
        {
            EnsureFolder(Sign5MatDir);
            var sh = Shader.Find("Haengin/SignGlow") ?? throw new Exception("Haengin/SignGlow 셰이더 없음");
            string path = $"{Sign5MatDir}/M_SG_{key}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(sh) { name = "M_SG_" + key }; AssetDatabase.CreateAsset(m, path); }
            if (m.shader != sh) m.shader = sh;
            m.SetColor("_Color", c); m.SetFloat("_Strength", strength); m.SetFloat("_Mode", mode); m.SetFloat("_Seed", Hash(key) % 97);
            m.enableInstancing = true; EditorUtility.SetDirty(m);
            return m;
        }

        // ───────────────────────── 메시 보관(하나의 에셋 아래 이름별 서브 메시 — 다시 만들 때 제자리 갱신)
        static Mesh signMeshRoot;
        static Dictionary<string, Mesh> signMeshes;
        static Mesh SubMesh(string name)
        {
            if (signMeshRoot == null)
            {
                signMeshRoot = AssetDatabase.LoadAssetAtPath<Mesh>(Sign5MeshPath);
                if (signMeshRoot == null) { signMeshRoot = new Mesh { name = "SignMeshes" }; AssetDatabase.CreateAsset(signMeshRoot, Sign5MeshPath); }
                signMeshes = AssetDatabase.LoadAllAssetRepresentationsAtPath(Sign5MeshPath).OfType<Mesh>().GroupBy(m => m.name).ToDictionary(g => g.Key, g => g.First());
            }
            if (!signMeshes.TryGetValue(name, out var mesh) || mesh == null)
            {
                mesh = new Mesh { name = name };
                AssetDatabase.AddObjectToAsset(mesh, signMeshRoot);
                signMeshes[name] = mesh;
            }
            return mesh;
        }

        /// 간판 면 메시(가운데 원점, XY 평면, 앞 = −Z): 그림 비율을 지키고 남는 쪽(가로 또는 세로)은 가장자리 바로 안쪽 띠를 늘려 채움(9칸 늘이기).
        /// uv0 = 그림, uv1 = 면 전체 0~1(낡음·조명 무늬)
        static Mesh FaceMesh(string name, float W, float H, float aspect)
        {
            var mesh = SubMesh(name);
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var uv2 = new List<Vector2>(); var tri = new List<int>();
            float s = Mathf.Min(W / aspect, H), cw = aspect * s, ch = s;
            const float cf = 0.03f, fill = 0.006f;
            bool alongX = W - cw >= H - ch;
            float L = alongX ? W : H, C = alongX ? cw : ch;
            float cap = cf * C;
            float[] xs = { -L / 2, -L / 2 + cap, -C / 2 + cap, C / 2 - cap, L / 2 - cap, L / 2 };
            float[] us = { 0f, cf, cf + fill, 1f - cf - fill, 1f - cf, 1f };
            if (L - C < 0.01f) { xs = new[] { -L / 2, L / 2 }; us = new[] { 0f, 1f }; }
            for (int i = 0; i < xs.Length - 1; i++)
            {
                if (xs[i + 1] - xs[i] < 1e-4f) continue;
                int b = v.Count;
                for (int k = 0; k < 4; k++)
                {
                    float a = (k & 1) == 0 ? xs[i] : xs[i + 1], ua = (k & 1) == 0 ? us[i] : us[i + 1];
                    float o = (k & 2) == 0 ? -1f : 1f;
                    if (alongX) { v.Add(new Vector3(a, o * H / 2, 0f)); uv.Add(new Vector2(ua, o < 0 ? 0f : 1f)); uv2.Add(new Vector2(a / W + 0.5f, o < 0 ? 0f : 1f)); }
                    else { v.Add(new Vector3(o * W / 2, a, 0f)); uv.Add(new Vector2(o < 0 ? 0f : 1f, ua)); uv2.Add(new Vector2(o < 0 ? 0f : 1f, a / H + 0.5f)); }
                }
                // 0 = (−,−) 1 = (+,−) 2 = (−,+) 3 = (+,+) — alongX 이면 0 왼아래 1 오른아래 2 왼위 3 오른위
                if (alongX) tri.AddRange(new[] { b, b + 3, b + 1, b + 3, b, b + 2 });
                else tri.AddRange(new[] { b, b + 1, b + 2, b + 2, b + 1, b + 3 });
            }
            mesh.Clear(); mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetUVs(1, uv2); mesh.SetTriangles(tri, 0);
            mesh.SetNormals(Enumerable.Repeat(new Vector3(0, 0, -1), v.Count).ToList());
            mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh);
            return mesh;
        }

        /// 원통(회전 입간판): 반지름 r, 높이 h, 옆면 uv u = 둘레, v = 높이
        static Mesh CylMesh(string name, float r, float h, int seg = 28)
        {
            var mesh = SubMesh(name);
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var uv2 = new List<Vector2>(); var tri = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg; var d = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                v.Add(d * r); n.Add(d); uv.Add(new Vector2(i / (float)seg, 0f)); uv2.Add(new Vector2(i / (float)seg, 0f));
                v.Add(d * r + Vector3.up * h); n.Add(d); uv.Add(new Vector2(i / (float)seg, 1f)); uv2.Add(new Vector2(i / (float)seg, 1f));
                if (i > 0) { int k = i * 2; tri.AddRange(new[] { k - 2, k - 1, k, k - 1, k + 1, k }); }
            }
            mesh.Clear(); mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetUVs(1, uv2); mesh.SetTriangles(tri, 0); mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh);
            return mesh;
        }

        static GameObject MeshObj(string name, Transform parent, Mesh mesh, Material mat, Vector3 pos, Quaternion rot, bool shadows = false)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(pos, rot);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off; r.receiveShadows = true;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.OccludeeStatic);
            return go;
        }
        static GameObject QuadObj(string name, Transform parent, Vector3 pos, Vector3 n, float w, float h, Material m)
        {
            var go = Quad(name, parent, pos, n, w, h, m, 0f);
            return go;
        }

        static Material WindowLitMat()
        {
            var sh = Shader.Find("Haengin/WindowLit"); if (sh == null) return null;
            EnsureFolder(Sign5MatDir);
            string path = $"{Sign5MatDir}/M_WinLit5.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(sh) { name = "M_WinLit5" }; AssetDatabase.CreateAsset(m, path); }
            if (m.shader != sh) m.shader = sh;
            m.SetColor("_Color", new Color(1.15f, 0.86f, 0.55f)); m.SetColor("_Cool", new Color(0.82f, 0.92f, 1.0f)); m.SetFloat("_Strength", 1f);
            m.enableInstancing = true; EditorUtility.SetDirty(m);
            return m;
        }

        // 간판 몸체 재질(평면색 UTS)
        static Material SignFrameMat => Flat("M_SignFrame", new Color(0.74f, 0.76f, 0.78f));
        static Material SignBackMat => Flat("M_SignBack", new Color(0.30f, 0.31f, 0.33f));
        static Material SignBoltMat => Flat("M_SignBolt", new Color(0.20f, 0.20f, 0.21f));

        // 상호 → 면 그림
        static readonly (string key, string band, string proj)[] TenantFaces =
        {
            ("반짝 문구", "band_munbang", null), ("번개배달", "band_bungae", "proj_bungae"), ("국밥", "band_gukbap", null),
            ("하루편의점", "band_haru", "proj_haru"), ("빨랫줄", "band_laundry", null), ("나사 철물", "band_hardware", null),
            ("고쳐요", "band_pc", null), ("넘김", "band_jebon", null), ("계단분식", "band_bunsik", null),
        };
        static readonly HashSet<string> LightBoxes = new HashSet<string> { "band_munbang", "band_haru", "band_pc", "band_jebon", "proj_haru", "proj_bungae", "proj_noraebang", "proj_pcbang", "proj_dokseosil", "proj_hanuiwon", "proj_math", "proj_gym", "corner_a", "corner_b", "corner_c2" };

        static int signs5 = 0;

        /// 키트 간판 자리 하나 → 몸체 + 면. 없던 그림이면 false(예전 방식으로)
        static bool MakeSign5(Transform t, Dictionary<string, object> inf)
        {
            if (!Signs5Ready) return false;
            string kind = S(inf, "sign_kind") ?? "band", tenant = S(inf, "tenant") ?? "";
            var size = L(G(inf, "size_m"));
            float w = size != null && size.Count >= 2 ? F(size[0]) : 1f, h = size != null && size.Count >= 2 ? F(size[1]) : 0.5f;
            var n = V3(G(inf, "normal")).normalized; var c = V3(G(inf, "center"));
            if (n == Vector3.zero || c == Vector3.zero) return false;
            string band = null, proj = null;
            foreach (var tf in TenantFaces) if (tenant.Contains(tf.key)) { band = tf.band; proj = tf.proj; break; }
            var r0 = t.GetComponent<MeshRenderer>();
            float seed = (Hash(t.name) & 1023) / 37f;
            if (kind == "band" && band != null)
            {
                if (r0 != null) r0.enabled = false;
                if (t.name == "Sign_007") { ChannelLetters(t, c, n, w, h, "하루편의점"); return true; }   // 하루편의점 옆면 = 채널 문자
                BandSign(t, c, n, w, h, band, seed);
                return true;
            }
            if (kind == "projecting" && proj != null)
            {
                if (r0 != null) r0.enabled = false;
                ProjSign(t, c, n, w, h, proj, seed, F(G(inf, "faces"), 2) > 1.5f);
                return true;
            }
            if (kind == "plate")
            {
                if (r0 != null) r0.enabled = false;
                if (tenant.Contains("번개배달")) { PlateFace(t, c, n, w, h, "proj_bungae", seed); return true; }
                string text = tenant.Contains("경비실") ? "경 비 실" : tenant.Contains("후문") ? "후\n문" : t.name == "Sign_014" ? "박 영 순" : "김 정 례";
                bool stone = tenant.Contains("후문");
                PlateText(t, c, n, w, h, text, stone);
                return true;
            }
            return false;
        }

        /// 판류형 가로 간판: 뒤 상자 · 알루미늄 테두리 · 볼트 4 · 아래 배선관(+ 벽으로 내려가는 관) · 받침 쇠 2 · 면 · 밤 빛 번짐
        static void BandSign(Transform parent, Vector3 c, Vector3 n, float w, float h, string face, float seed)
        {
            var f5 = Faces5()[face];
            var rot = Quaternion.LookRotation(-n, Vector3.up); var right = rot * Vector3.right; var up = Vector3.up;
            var root = new GameObject("간판 " + face).transform; root.SetParent(parent, true);
            Box("뒤 상자", root, c - n * 0.065f, new Vector3(w + 0.07f, h + 0.07f, 0.12f), rot, SignBackMat, true);
            var fm = SignFrameMat;
            Box("테두리 위", root, c + up * (h / 2 + 0.02f) - n * 0.005f, new Vector3(w + 0.1f, 0.045f, 0.06f), rot, fm);
            Box("테두리 아래", root, c - up * (h / 2 + 0.02f) - n * 0.005f, new Vector3(w + 0.1f, 0.045f, 0.06f), rot, fm);
            Box("테두리 왼", root, c - right * (w / 2 + 0.02f) - n * 0.005f, new Vector3(0.045f, h + 0.08f, 0.06f), rot, fm);
            Box("테두리 오른", root, c + right * (w / 2 + 0.02f) - n * 0.005f, new Vector3(0.045f, h + 0.08f, 0.06f), rot, fm);
            foreach (var (sx, sy) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
                Box("볼트", root, c + right * sx * (w / 2 + 0.02f) + up * sy * (h / 2 + 0.02f) + n * 0.03f, new Vector3(0.022f, 0.022f, 0.012f), rot, SignBoltMat);
            var bm = SignBackMat;
            Box("배선관", root, c - up * (h / 2 + 0.075f) - n * 0.07f, new Vector3(w * 0.8f, 0.03f, 0.03f), rot, bm);
            Box("배선관 내림", root, c - right * (w * 0.4f) - up * (h / 2 + 0.36f) - n * 0.07f, new Vector3(0.03f, 0.58f, 0.03f), rot, bm);
            foreach (float sx in new[] { -0.35f, 0.35f })
                Box("받침 쇠", root, c + right * (w * sx) - up * (h / 2 + 0.05f) - n * 0.06f, new Vector3(0.04f, 0.035f, 0.13f), rot, fm);
            bool lit = LightBoxes.Contains(face);
            var mat = SignMat(face, lit ? 0.9f : 0f, 0.25f + (seed % 0.2f), seed);
            MeshObj("면", root, FaceMesh($"band_{parent.name}", w, h, f5.Aspect), mat, c + n * 0.004f, rot);
            if (lit) Glow(root, c + n * 0.02f, n, w, h, face, f5.Glow);
            signs5++;
        }

        static void Glow(Transform root, Vector3 c, Vector3 n, float w, float h, string face, Color col, float strength = 0.32f)
        {
            float gw = w + 0.9f + w * 0.12f, gh = h * 1.7f + 0.6f;
            var m = GlowMat(face, col, strength);
            m.SetVector("_Inner", new Vector4(w / 2f / gw, h / 2f / gh, 0, 0)); EditorUtility.SetDirty(m);
            var go = Quad("빛 번짐", root, c, n, gw, gh, m, 0f);
            GameObjectUtility.SetStaticEditorFlags(go, 0);
        }

        /// 돌출 간판(키트 자리): 양면 상자 · 위아래 뚜껑 · 벽 팔(사선 보강) · 면 2 · 빛 번짐 2
        static void ProjSign(Transform parent, Vector3 c, Vector3 n, float reach, float h, string face, float seed, bool twoSided)
        {
            var f5 = Faces5()[face];
            // 키트: 면 A 가운데 = c(법선 n 쪽), 판 두께 0.2 → 상자 가운데 = c − n·0.104
            var mid = c - n * 0.104f;
            var rot = Quaternion.LookRotation(n, Vector3.up);   // 로컬 z = n(판 두께 방향), x = 벽에서 멀어지는 쪽
            var root = new GameObject("돌출 간판 " + face).transform; root.SetParent(parent, true);
            var back = SignBackMat; var fm = SignFrameMat;
            Box("상자", root, mid, new Vector3(reach + 0.05f, h + 0.05f, 0.205f), rot, back, true);
            var outDir = rot * Vector3.right;
            Box("위 뚜껑", root, mid + Vector3.up * (h / 2 + 0.05f), new Vector3(reach + 0.1f, 0.06f, 0.26f), rot, fm);
            Box("아래 뚜껑", root, mid - Vector3.up * (h / 2 + 0.05f), new Vector3(reach + 0.1f, 0.06f, 0.26f), rot, fm);
            // 벽 쪽 끝 = mid − outDir·(reach/2) — 사선 보강대
            var wallTop = mid - outDir * (reach / 2 + 0.12f) + Vector3.up * (h / 2 - 0.1f);
            var tip = mid + outDir * (reach * 0.2f) + Vector3.up * (h / 2 + 0.08f);
            var brace = Box("보강대", root, (wallTop + tip) / 2f + Vector3.up * 0.25f, new Vector3((tip - wallTop).magnitude, 0.03f, 0.03f), Quaternion.LookRotation(n, Vector3.up) * Quaternion.Euler(0, 0, -25f), fm);
            bool lit = LightBoxes.Contains(face);
            var mat = SignMat(face, lit ? 0.9f : 0f, 0.4f + (seed % 0.3f), seed);
            var mesh = FaceMesh($"proj_{parent.name}", reach, h, f5.Aspect);
            MeshObj("면 A", root, mesh, mat, c + n * 0.003f, Quaternion.LookRotation(-n, Vector3.up));
            if (lit) Glow(root, c + n * 0.02f, n, reach, h, face, f5.Glow, 0.28f);
            if (twoSided)
            {
                var cb = mid - n * 0.107f;
                MeshObj("면 B", root, mesh, mat, cb, Quaternion.LookRotation(n, Vector3.up));
                if (lit) Glow(root, cb - n * 0.02f, -n, reach, h, face, f5.Glow, 0.28f);
            }
            signs5++;
        }

        /// 벽에 붙은 세로 판(번개배달 2층 계단 입구)
        static void PlateFace(Transform parent, Vector3 c, Vector3 n, float w, float h, string face, float seed)
        {
            var f5 = Faces5()[face];
            var rot = Quaternion.LookRotation(-n, Vector3.up);
            var root = new GameObject("판 " + face).transform; root.SetParent(parent, true);
            Box("판 몸", root, c - n * 0.02f, new Vector3(w + 0.04f, h + 0.04f, 0.035f), rot, SignFrameMat, true);
            MeshObj("면", root, FaceMesh($"plate_{parent.name}", w, h, f5.Aspect), SignMat(face, 0.5f, 0.5f, seed), c + n * 0.003f, rot);
            signs5++;
        }

        /// 문패·경비실·후문 현판: 나무(또는 돌) 판 + 새긴 글자(TMP)
        static void PlateText(Transform parent, Vector3 c, Vector3 n, float w, float h, string text, bool stone)
        {
            var rot = Quaternion.LookRotation(-n, Vector3.up);
            var root = new GameObject("명판").transform; root.SetParent(parent, true);
            var body = stone ? Flat("M_Plate_Stone", new Color(0.80f, 0.77f, 0.70f)) : Flat("M_Plate_Wood", new Color(0.42f, 0.30f, 0.21f));
            Box("판", root, c - n * 0.015f, new Vector3(w + 0.04f, h + 0.04f, 0.04f), rot, body, true);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root + "/Fonts/KR_Bold_SDF.asset");
            AddText(root, c + n * 0.012f, n, text, w * 0.85f, h * 0.85f, stone ? new Color(0.22f, 0.20f, 0.18f) : new Color(0.93f, 0.88f, 0.76f), font);
            signs5++;
        }

        /// 채널 문자: 벽 쪽 바탕판(흰) + 레이스웨이 바(슬레이트) + 글자 6겹(두께) — 앞 글자 크림
        static void ChannelLetters(Transform parent, Vector3 c, Vector3 n, float w, float h, string text)
        {
            var rot = Quaternion.LookRotation(-n, Vector3.up);
            var root = new GameObject("채널 문자 " + text).transform; root.SetParent(parent, true);
            Box("바탕판", root, c - n * 0.06f, new Vector3(w + 0.07f, h + 0.07f, 0.12f), rot, Flat("M_Fascia_White", new Color(0.90f, 0.89f, 0.86f)), true);
            var slate = new Color(0.33f, 0.40f, 0.47f);
            Box("레이스웨이", root, c + n * 0.04f - Vector3.up * (h * 0.05f), new Vector3(w * 0.86f, h * 0.18f, 0.08f), rot, Flat("M_Raceway", slate));
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root + "/Fonts/KR_Brush_SDF.asset") ?? AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root + "/Fonts/KR_Bold_SDF.asset");
            for (int k = 6; k >= 0; k--)
            {
                var col = k == 0 ? new Color(0.96f, 0.93f, 0.84f) : Color.Lerp(slate, slate * 0.7f, k / 6f);
                AddText(root, c + n * (0.09f + 0.012f * (6 - k)), n, text, w * 0.82f, h * 0.9f, col, font);
            }
            Glow(root, c + n * 0.2f, n, w * 0.8f, h * 0.8f, "chan_haru", new Color(0.95f, 0.88f, 0.7f), 0.3f);
            signs5++;
        }
    }
}
