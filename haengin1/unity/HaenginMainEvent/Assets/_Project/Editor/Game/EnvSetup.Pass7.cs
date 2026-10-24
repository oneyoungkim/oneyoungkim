// 행인1의 메인이벤트 — 맵 아트 패스 7차(2026-10-25, 짧은 마감): 하늘 역광 테두리(셰이더 ScreenRim + DayLight 전역 값) · 옹벽 담쟁이 ·
//   성곽길 둥근 흙 광장(f06) · 밤 톤(주변광 중성 회갈·웅덩이 세기·창문 불) · 주차장(f02) 상자 더미·화분 줄·남산(원경 돌림) · 노을 해 방향 ·
//   와룡 오르막(f05) 길가 막돌 축대 + 덩굴. 6차 묶음(Pass6) 안에서 부른다(광장 자리는 초록보다 먼저 정해 풀·관목이 비켜 가게).
using System;
using System.Collections.Generic;
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
        public const float SkyYaw7 = 50f;   // 7차: 원경 카드를 50° 돌려 남산(서울타워)이 주차장 B 카메라의 하늘 틈(f02)에 오게
        static (Vector3 c, float r)? plaza7; static float plaza7Y;
        static readonly Vector3 Shot5Feet = new Vector3(-217.4f, 40.3f, 141.8f);

        static Collider TerrainCol7(Transform zoneRoot) { var t = zoneRoot.Find("Ground/Terrain"); return t != null ? t.GetComponent<Collider>() : null; }

        /// 성곽길 비탈(낮은) 쪽 = +1 이면 오른쪽(길 방향 기준)
        static float TrailSide(Zone1Data.Road trail)
        {
            float sumL = 0f, sumR = 0f;
            for (int k = 0; k < trail.Points.Length - 1; k++)
            {
                var a = trail.Points[k]; var b2 = trail.Points[k + 1]; var dir = Flat(b2 - a).normalized; var rt = Vector3.Cross(Vector3.up, dir);
                var mid = (a + b2) / 2;
                sumR += GroundAt(mid + rt * (trail.Width / 2 + 1.5f), 4f, 10f).y; sumL += GroundAt(mid - rt * (trail.Width / 2 + 1.5f), 4f, 10f).y;
            }
            return sumR < sumL ? 1f : -1f;
        }

        // ───────────────────────── 둥근 흙 광장(f06): 성곽길 비탈 쪽, 성곽길 사진 자리 앞 8~18m 의 평평한 맨땅
        static void PlanPlaza7(Zone1Data d, Transform zoneRoot)
        {
            plaza7 = null;
            var trail = d.Roads.FirstOrDefault(r => r.Kind == "wallpath"); var tcol = TerrainCol7(zoneRoot);
            if (trail == null || tcol == null) return;
            float sd = TrailSide(trail), total = 0f;
            for (int k = 0; k < trail.Points.Length - 1; k++) total += Flat(trail.Points[k + 1] - trail.Points[k]).magnitude;
            var fwd = Quaternion.Euler(0f, 96f, 0f) * Vector3.forward;
            const float R = 2.6f;
            float bestScore = -1e9f; Vector3 best = Vector3.zero; var why = new Dictionary<string, int>(); void W(string k) => why[k] = why.TryGetValue(k, out var v) ? v + 1 : 1;
            for (float s = 0.5f; s < total - 0.5f; s += 0.5f)
            {
                var (pp, dir) = Along(trail.Points, s);
                var toP = Flat(pp - Shot5Feet); float dd = toP.magnitude;
                if (dd < 6f || dd > 13f || Vector3.Dot(toP, fwd) < 0f) { W("거리"); continue; }
                var rt = Vector3.Cross(Vector3.up, dir.normalized);
                // 길에 붙여(길 가장자리를 0.35m 덮음) 길 높이로 평평하게 — 비탈 쪽이 낮으면 돌 테두리가 낮은 옹벽이 됨
                var c = pp + rt * (sd * (trail.Width / 2 + R - 0.2f));
                float ty = GroundAt(pp, 2f, 6f).y;
                float lo = 1e9f, hi = -1e9f; bool ok = true;
                for (int k = 0; k <= 16 && ok; k++)
                {
                    var q = k == 16 ? c : c + Quaternion.Euler(0f, k * 22.5f, 0f) * Vector3.forward * (k % 2 == 0 ? R : R * 0.55f);
                    if (!tcol.Raycast(new Ray(q + Vector3.up * 20f, Vector3.down), out var h, 40f)) { ok = false; W("지형밖"); break; }   // 지형 높이만(관목·나무 자리 상자는 무시)
                    lo = Mathf.Min(lo, h.point.y); hi = Mathf.Max(hi, h.point.y);
                }
                // 단단한 것(건물·담·성곽·울타리 상자)이 광장 안에 있으면 안 됨 — 걷는 면·식생 자리 상자는 괜찮음
                if (ok)
                    foreach (var col in Physics.OverlapSphere(c + Vector3.up * 1.3f, R * 0.95f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (col == tcol || col.gameObject.layer == Layers.Ground || col.gameObject.layer == Layers.Player) continue;
                        string pn = col.transform.parent != null ? col.transform.parent.name : "";
                        if (pn == "bush" || pn == "tree") continue;
                        ok = false; W("막힘 " + pn + "/" + col.name); break;
                    }
                if (!ok) continue;
                if (hi > ty + 0.3f || ty - lo > 1.3f) { W($"높이 hi{hi - ty:F1} lo{ty - lo:F1}"); continue; }
                float lateral = Vector3.Dot(Flat(c - Shot5Feet), Quaternion.Euler(0f, 96f + 90f, 0f) * Vector3.forward);
                if (Mathf.Abs(lateral) < 1.8f) { W("옆"); continue; }   // 사진 자리에서 시우 몸 뒤에 숨지 않게(f06 처럼 길 옆으로 보이게)
                float score = -(ty - lo) * 0.5f - Mathf.Abs(dd - 8.5f) * 0.08f;
                if (score > bestScore) { bestScore = score; best = c; plaza7Y = ty; }
            }
            if (bestScore > -1e8f) plaza7 = (best, R);
            else Notes.Add("광장 자리 못 찾은 까닭: " + string.Join(" · ", why.OrderByDescending(x => x.Value).Take(8).Select(x => $"{x.Key} {x.Value}")) + $" · 비탈 쪽 {sd}");
        }
        static bool InPlaza7(Vector3 p, float margin) => plaza7.HasValue && Flat(p - plaza7.Value.c).magnitude < plaza7.Value.r + margin;

        static string BuildPlaza7(Zone1Data d, Transform zoneRoot, Transform env, Func<string, GameObject> prefab)
        {
            if (!plaza7.HasValue) return "둥근 광장 자리 없음";
            var (c, R) = plaza7.Value; var tcol = TerrainCol7(zoneRoot);
            var root = new GameObject("Plaza7").transform; root.SetParent(env, false);
            float TerrainY(Vector3 p) => tcol != null && tcol.Raycast(new Ray(p + Vector3.up * 20f, Vector3.down), out var h, 40f) ? h.point.y : p.y;
            float cy = plaza7Y + 0.04f;   // 길 높이로 평평
            var mb = new MB6(); const int SEG = 32, RINGS = 4;
            int center = mb.Add(new Vector3(c.x, cy, c.z), Vector3.up, new Vector2(c.x, c.z) / 2.5f, Vector2.zero, Color.white);
            for (int ri = 1; ri <= RINGS; ri++)
                for (int k = 0; k < SEG; k++)
                {
                    var p = c + Quaternion.Euler(0f, k * 360f / SEG, 0f) * Vector3.forward * (R * ri / RINGS);
                    p.y = cy;
                    mb.Add(p, Vector3.up, new Vector2(p.x, p.z) / 2.5f, Vector2.zero, Color.white);
                }
            int V(int ring, int k) => ring == 0 ? center : 1 + (ring - 1) * SEG + ((k % SEG) + SEG) % SEG;
            for (int k = 0; k < SEG; k++)
            {
                mb.Tri(V(0, 0), V(1, k + 1), V(1, k));
                for (int ri = 1; ri < RINGS; ri++) { mb.Tri(V(ri, k), V(ri, k + 1), V(ri + 1, k + 1)); mb.Tri(V(ri, k), V(ri + 1, k + 1), V(ri + 1, k)); }
            }
            var dirt = Toon($"{MatDir}/M_Plaza_Dirt.mat", AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/M_Dirt.png"), Vector2.one, Vector2.zero, new Color(1.0f, 0.94f, 0.84f));
            Obj6("흙 광장", root, mb.Save("p7_plaza"), dirt, ShadowCastingMode.Off);
            // 돌 테두리(길 쪽 입구는 비움) — 화강석 토막 22
            var trail = d.Roads.FirstOrDefault(r => r.Kind == "wallpath");
            Vector3 toTrail = Vector3.forward;
            if (trail != null)
            {
                float bestD = 1e9f;
                for (int k = 0; k < trail.Points.Length - 1; k++) { var a = trail.Points[k]; var b = trail.Points[k + 1]; var ab = Flat(b - a); float t = Mathf.Clamp01(Vector3.Dot(Flat(c - a), ab) / Mathf.Max(ab.sqrMagnitude, 1e-4f)); var q = a + (b - a) * t; float dd = Flat(q - c).magnitude; if (dd < bestD) { bestD = dd; toTrail = Flat(q - c).normalized; } }
            }
            var granite = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/M_Granite.mat") ?? Flat("M_Plaza_Stone", new Color(0.80f, 0.77f, 0.70f));
            var rng = new System.Random(707); int stones = 0;
            for (int k = 0; k < 26; k++)
            {
                var dir = Quaternion.Euler(0f, k * 360f / 26f, 0f) * Vector3.forward;
                if (Vector3.Angle(dir, toTrail) < 34f) continue;
                var p = c + dir * (R + 0.08f); float ground = Mathf.Min(TerrainY(p), cy);
                float h = (cy - ground) + 0.16f + (float)rng.NextDouble() * 0.08f;   // 비탈 쪽은 낮은 옹벽처럼 땅까지
                p.y = ground - 0.05f;
                Box("광장 돌", root, p + Vector3.up * (h / 2), new Vector3(0.66f, h, 0.26f), Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(0f, ((float)rng.NextDouble() - 0.5f) * 6f, 0f), granite, true);
                stones++;
            }
            // 벤치 하나(광장 안쪽 끝, 가운데를 봄)
            var bp = c - toTrail * (R - 1.25f); bp.y = cy;
            var bench = PutProp(prefab, "park_bench", root, bp, Quaternion.LookRotation(toTrail, Vector3.up).eulerAngles.y, "광장 벤치");
            return $"둥근 흙 광장(f06): 반지름 {R}m · 돌 테두리 {stones} · 벤치 {(bench != null ? 1 : 0)} @({c.x:F1},{c.z:F1})";
        }

        // ───────────────────────── 담쟁이 카드(벽 면에 붙어 위에서 늘어짐, 아래가 흔들림)
        static void IvyCurtain(MB6 mb, Vector3 topCenter, Vector3 n, float w, float h, Rect uv)
        {
            n = Flat(n).normalized; var right = Vector3.Cross(Vector3.up, n).normalized;
            var o = topCenter + n * 0.035f;
            var p = new[] { o - right * (w / 2) - Vector3.up * h, o + right * (w / 2) - Vector3.up * h, o + right * (w / 2), o - right * (w / 2) };
            var t = new[] { new Vector2(uv.xMin, uv.yMin), new Vector2(uv.xMax, uv.yMin), new Vector2(uv.xMax, uv.yMax), new Vector2(uv.xMin, uv.yMax) };
            float[] sway = { 1f, 1f, 0f, 0f };
            int b0 = mb.V.Count;
            for (int k = 0; k < 4; k++) mb.Add(p[k], (n + Vector3.up * 0.25f).normalized, t[k], new Vector2(0f, sway[k]), Color.white);
            mb.Tri(b0, b0 + 2, b0 + 1); mb.Tri(b0, b0 + 3, b0 + 2);
            // 벽 머리 위로 넘어오는 잎(짧은 카드, 벽 안쪽으로 기울임)
            var top = topCenter + Vector3.up * 0.02f;
            var q = new[] { top - right * (w / 2) + n * 0.05f, top + right * (w / 2) + n * 0.05f, top + right * (w / 2) - n * 0.32f + Vector3.up * 0.12f, top - right * (w / 2) - n * 0.32f + Vector3.up * 0.12f };
            var tt = new[] { new Vector2(uv.xMin, uv.yMax - uv.height * 0.22f), new Vector2(uv.xMax, uv.yMax - uv.height * 0.22f), new Vector2(uv.xMax, uv.yMax), new Vector2(uv.xMin, uv.yMax) };
            b0 = mb.V.Count;
            for (int k = 0; k < 4; k++) mb.Add(q[k], Vector3.up, tt[k], new Vector2(0f, 0.2f), Color.white);
            mb.Tri(b0, b0 + 2, b0 + 1); mb.Tri(b0, b0 + 3, b0 + 2);
        }

        // ───────────────────────── 옹벽 담쟁이(f02 — 주차장 옹벽 · 계단 축대): 주차장 쪽 면, 머리에서 늘어짐
        static string Ivy7(Zone1Data d, Transform env, MB6 ivyMb, System.Random rng)
        {
            var pad = d.Pads.FirstOrDefault(p => p.Name.Contains("주차장"));
            int n = 0;
            var crop = ClumpCrop("ivy");
            foreach (var b in d.Blocks.Where(x => x.Kind == "retaining"))
            {
                var rot = Quaternion.Euler(0f, b.Yaw, 0f);
                bool alongX = b.Size.x >= b.Size.z; float L = alongX ? b.Size.x : b.Size.z, th = alongX ? b.Size.z : b.Size.x;
                var ax = rot * (alongX ? Vector3.right : Vector3.forward); var nrm = rot * (alongX ? Vector3.forward : Vector3.right);
                var toward = pad != null ? pad.Center : b.Center + nrm;
                float sg = Vector3.Dot(toward - b.Center, nrm) >= 0f ? 1f : -1f;
                var face = nrm * sg; float top = b.Center.y + b.Size.y / 2f;
                float baseY = GroundAt(b.Center + face * (th / 2 + 0.5f), 1f, 10f).y, H = top - baseY;
                if (H < 0.8f || L < 1.5f) continue;
                for (float t = -L / 2 + 0.4f; t < L / 2 - 0.3f;)
                {
                    float w = 0.9f + (float)rng.NextDouble() * 0.9f;
                    if (rng.NextDouble() < 0.7)
                    {
                        float h = Mathf.Min(H * 0.6f, 0.7f + (float)rng.NextDouble() * 0.8f);
                        var tc = b.Center + ax * (t + w / 2) + face * (th / 2); tc.y = top + 0.04f;
                        IvyCurtain(ivyMb, tc, face, w, h, crop[rng.Next(4)]); n++;
                    }
                    t += w * (0.8f + (float)rng.NextDouble() * 0.6f);
                }
            }
            return $"옹벽 담쟁이 {n}";
        }

        // ───────────────────────── 와룡 오르막(f05): 길 높은 쪽 막돌 축대(Toon M_Stone_Rubble, 월드 미터 UV) + 덩굴
        static string WaryongWall7(Zone1Data d, Transform env, MB6 ivyMb, System.Random rng)
        {
            var road = d.Roads.FirstOrDefault(r => r.Name.Contains("와룡공원 오르막"));
            if (road == null || road.Points.Length < 2) return "와룡 오르막 길 없음";
            var tcolW = TerrainCol7(env.parent);
            float total = 0f; for (int k = 0; k < road.Points.Length - 1; k++) total += Flat(road.Points[k + 1] - road.Points[k]).magnitude;
            // 높은 쪽 고르기
            float rise(float side)
            {
                float sum = 0f; int c = 0;
                for (float s = 1f; s < total - 1f; s += 2f)
                {
                    var (p, dir) = Along(road.Points, s); var rt = Vector3.Cross(Vector3.up, dir.normalized);
                    sum += GroundAt(p + rt * (side * (road.Width / 2 + 1.8f)), 6f, 10f).y - GroundAt(p, 3f, 6f).y; c++;
                }
                return c > 0 ? sum / c : 0f;
            }
            float sd = rise(1f) >= rise(-1f) ? 1f : -1f;
            var root = new GameObject("WaryongWall7").transform; root.SetParent(env, false);
            var mb = new MB6(); int segs = 0;
            var pts = new List<(Vector3 f, float baseY, float topY, Vector3 n, float s)>();
            for (float s = 0.3f; s <= total - 0.3f; s += 0.8f)
            {
                var (p, dir) = Along(road.Points, s); var rt = Vector3.Cross(Vector3.up, dir.normalized);
                var q = p + rt * (sd * (road.Width / 2 + 0.22f));
                float pathY = GroundAt(p, 3f, 6f).y;
                // 길가 지형은 걷는 면 밑으로 깎여 거의 평평하다(살핌: 3.5m 바깥까지 +0.6m 안) → 높이 0.8~1.6m 의 막돌 축대(화단 테두리)로 세우고 뒷면도 닫음.
                // 옆이 지형이 아니면(공원 패드·길) 비움
                bool onTerrain = Physics.Raycast(q + rt * (sd * 0.6f) + Vector3.up * 15f, Vector3.down, out var th, 40f, ~0, QueryTriggerInteraction.Ignore) && th.collider == tcolW;
                float riseHere = -9f;
                foreach (float dd in new[] { 0.8f, 1.6f, 2.4f, 3.2f }) riseHere = Mathf.Max(riseHere, GroundAt(q + rt * (sd * dd), 8f, 12f).y - pathY);
                float baseY = Mathf.Min(GroundAt(q, 3f, 6f).y, pathY) - 0.12f;
                float topY = baseY + Mathf.Clamp(1.0f + 0.22f * Mathf.Sin(s * 0.55f) + Mathf.Max(0f, riseHere) * 0.6f, 0.8f, 1.6f);
                if (!onTerrain || s < 1.5f || s > total - 2.0f) { pts.Add((Vector3.zero, 0f, -1f, Vector3.zero, s)); continue; }   // 빈 칸 표시
                pts.Add((new Vector3(q.x, 0f, q.z), baseY, topY, -rt * sd, s));
            }
            const float DEPTH = 0.45f;
            for (int i = 0; i < pts.Count - 1; i++)
            {
                var a = pts[i]; var b = pts[i + 1];
                if (a.topY < 0f || b.topY < 0f) continue;
                Vector3 A0 = new Vector3(a.f.x, a.baseY, a.f.z), A1 = new Vector3(a.f.x, a.topY, a.f.z), B0 = new Vector3(b.f.x, b.baseY, b.f.z), B1 = new Vector3(b.f.x, b.topY, b.f.z);
                var nf = ((a.n + b.n) * 0.5f).normalized;
                // 앞면(길 쪽) — 감기는 길 쪽에서 보이게
                int v0 = mb.Add(A0, nf, new Vector2(a.s, A0.y), Vector2.zero, Color.white), v1 = mb.Add(B0, nf, new Vector2(b.s, B0.y), Vector2.zero, Color.white);
                int v2 = mb.Add(B1, nf, new Vector2(b.s, B1.y), Vector2.zero, Color.white), v3 = mb.Add(A1, nf, new Vector2(a.s, A1.y), Vector2.zero, Color.white);
                var cross = Vector3.Cross(B0 - A0, A1 - A0);
                if (Vector3.Dot(cross, nf) > 0f) { mb.Tri(v0, v1, v2); mb.Tri(v0, v2, v3); } else { mb.Tri(v0, v2, v1); mb.Tri(v0, v3, v2); }
                // 머리(갓돌 없이 막돌 윗면)
                var back = -nf * DEPTH;
                int c0 = mb.Add(A1, Vector3.up, new Vector2(a.s, 0f), Vector2.zero, Color.white), c1 = mb.Add(B1, Vector3.up, new Vector2(b.s, 0f), Vector2.zero, Color.white);
                int c2 = mb.Add(B1 + back, Vector3.up, new Vector2(b.s, DEPTH), Vector2.zero, Color.white), c3 = mb.Add(A1 + back, Vector3.up, new Vector2(a.s, DEPTH), Vector2.zero, Color.white);
                if (Vector3.Dot(Vector3.Cross(B1 - A1, back), Vector3.up) > 0f) { mb.Tri(c0, c1, c2); mb.Tri(c0, c2, c3); } else { mb.Tri(c0, c2, c1); mb.Tri(c0, c3, c2); }
                // 뒷면(지형이 거의 평평해 뒤가 보임)
                {
                    Vector3 A2 = A1 + back, B2 = B1 + back, A3 = A0 + back, B3 = B0 + back; var nb = -nf;
                    int b0 = mb.Add(A3, nb, new Vector2(a.s, A3.y), Vector2.zero, Color.white), b1 = mb.Add(B3, nb, new Vector2(b.s, B3.y), Vector2.zero, Color.white);
                    int b2 = mb.Add(B2, nb, new Vector2(b.s, B2.y), Vector2.zero, Color.white), b3 = mb.Add(A2, nb, new Vector2(a.s, A2.y), Vector2.zero, Color.white);
                    if (Vector3.Dot(Vector3.Cross(B3 - A3, A2 - A3), nb) > 0f) { mb.Tri(b0, b1, b2); mb.Tri(b0, b2, b3); } else { mb.Tri(b0, b2, b1); mb.Tri(b0, b3, b2); }
                }
                // 끝마구리(앞뒤 칸이 비면)
                foreach (var (e, isStart) in new[] { (a, i == 0 || pts[i - 1].topY < 0f), (b, i + 2 >= pts.Count || pts[i + 2].topY < 0f) })
                {
                    if (!isStart) continue;
                    var E0 = new Vector3(e.f.x, e.baseY, e.f.z); var E1 = new Vector3(e.f.x, e.topY, e.f.z);
                    var en = ((e.Equals(a) ? A0 - B0 : B0 - A0)).normalized;
                    int e0 = mb.Add(E0, en, new Vector2(0f, E0.y), Vector2.zero, Color.white), e1 = mb.Add(E1, en, new Vector2(0f, E1.y), Vector2.zero, Color.white);
                    int e2 = mb.Add(E1 + back, en, new Vector2(DEPTH, E1.y), Vector2.zero, Color.white), e3 = mb.Add(E0 + back, en, new Vector2(DEPTH, E0.y), Vector2.zero, Color.white);
                    if (Vector3.Dot(Vector3.Cross(E1 - E0, back), en) > 0f) { mb.Tri(e0, e1, e2); mb.Tri(e0, e2, e3); } else { mb.Tri(e0, e2, e1); mb.Tri(e0, e3, e2); }
                }
                segs++;
                // 덩굴: 칸마다 55%
                if (rng.NextDouble() < 0.55)
                {
                    float hh = Mathf.Min((a.topY - a.baseY) * 0.65f, 0.5f + (float)rng.NextDouble() * 0.7f);
                    var tc = (A1 + B1) / 2 + Vector3.up * 0.03f;
                    IvyCurtain(ivyMb, tc, nf, 0.9f + (float)rng.NextDouble() * 0.7f, hh, ClumpCrop("ivy")[rng.Next(4)]);
                }
            }
            if (segs == 0) return "와룡 축대: 높은 쪽이 낮아 안 세움";
            var stone = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/M_Stone_Rubble.mat");
            if (stone == null) stone = Flat("M_Rubble7", new Color(0.72f, 0.68f, 0.61f));
            Obj6("막돌 축대", root, mb.Save("w7_rubble"), stone, ShadowCastingMode.On);
            return $"와룡 오르막 막돌 축대 {segs}칸({(sd > 0 ? "오른쪽" : "왼쪽")})";
        }

        // ───────────────────────── 주차장(f02): B 카메라 화면 안 남쪽 옹벽 밑에 파란 상자 더미 · 화분 줄(자판기 쪽부터)
        static string Parking7(Zone1Data d, Transform zoneRoot, Transform env, Func<string, GameObject> prefab)
        {
            var pad = d.Pads.FirstOrDefault(p => p.Name.Contains("주차장"));
            var vend = zoneRoot.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == "주차장 자판기");
            if (pad == null || vend == null) return "주차장 7차: 자리 없음";
            var root = new GameObject("Parking7").transform; root.SetParent(env, false);
            var spots = new List<(Vector3 p, Vector3 n)>();
            foreach (var b in d.Blocks.Where(x => x.Name.Contains("주차장 옹벽(남)")))
            {
                var rot = Quaternion.Euler(0f, b.Yaw, 0f);
                bool alongX = b.Size.x >= b.Size.z; float L = alongX ? b.Size.x : b.Size.z, th = alongX ? b.Size.z : b.Size.x;
                var ax = rot * (alongX ? Vector3.right : Vector3.forward); var nrm = rot * (alongX ? Vector3.forward : Vector3.right);
                float sg = Vector3.Dot(pad.Center - b.Center, nrm) >= 0f ? 1f : -1f;
                for (float t = -L / 2 + 0.4f; t <= L / 2 - 0.4f; t += 0.45f)
                    spots.Add((GroundAt(b.Center + ax * t + nrm * (sg * (th / 2 + 0.42f)), 2f, 6f), nrm * sg));
            }
            if (spots.Count == 0) return "주차장 7차: 남쪽 옹벽 없음";
            spots = spots.OrderBy(s => Flat(s.p - vend.position).magnitude).ToList();
            int placed = 0; var used = new List<Vector3>();
            bool Free(Vector3 p, float r) => used.All(u => Flat(u - p).magnitude > r);
            void Put(string prop, Vector3 p, Vector3 n, float yawJitter, float space)
            {
                if (!Free(p, space)) return;
                float yaw = Quaternion.LookRotation(n, Vector3.up).eulerAngles.y + yawJitter;
                if (PutProp(prefab, prop, root, p, yaw, "소품 " + prop + "(f02)") != null) { used.Add(p); placed++; }
            }
            // 자판기 쪽 끝: 파란 상자 더미 2 → 그다음 화분 줄
            int i = 0;
            foreach (var (p, n) in spots.Skip(1)) { if (i >= 2) break; int before = placed; Put("plastic_crates_stack", p, n, i * 23f, 0.85f); if (placed > before) i++; }
            var planters = new[] { "styrofoam_planter", "clay_pots_cluster", "potted_plant_b", "basin_planter_onion", "potted_plant_a", "styrofoam_planter", "clay_pots_cluster" };
            int k = 0;
            foreach (var (p, n) in spots.Where(s => Flat(s.p - vend.position).magnitude > 3.2f))
            {
                if (k >= planters.Length) break;
                int before = placed; Put(planters[k], p, n, (k % 3 - 1) * 12f, 1.25f); if (placed > before) k++;
            }
            return $"주차장(f02): 상자 더미 {i} · 화분 줄 {k} · 남산 원경 돌림 {SkyYaw7}°";
        }

        /// 개발용: 와룡 오르막 길 양쪽 땅 높이(길 기준) 찍기
        public static void ProbeWaryong7()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(Root + "/Scenes/Zone1.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
            var d = Zone1Data.Load(); var road = d.Roads.FirstOrDefault(r => r.Name.Contains("와룡공원 오르막"));
            var lines = new List<string> { $"길 {road.Name} 폭 {road.Width} 점 {string.Join(" ", road.Points.Select(x => $"({x.x:F1},{x.y:F1},{x.z:F1})"))}" };
            float total = 0f; for (int k = 0; k < road.Points.Length - 1; k++) total += Flat(road.Points[k + 1] - road.Points[k]).magnitude;
            for (float s = 0.5f; s < total; s += 2f)
            {
                var (p, dir) = Along(road.Points, s); var rt = Vector3.Cross(Vector3.up, dir.normalized); float py = GroundAt(p, 3f, 6f).y;
                string Side(float sd) => string.Join("/", new[] { 0.5f, 1.5f, 2.5f, 3.5f }.Select(dd => { var q = p + rt * (sd * (road.Width / 2 + dd)); bool hit = Physics.Raycast(q + Vector3.up * 15f, Vector3.down, out var h, 40f, ~0, QueryTriggerInteraction.Ignore); return hit ? $"{h.point.y - py:F1}{h.collider.name.Substring(0, Math.Min(4, h.collider.name.Length))}" : "x"; }));
                lines.Add($"s {s:F1} p ({p.x:F1},{p.z:F1}) dir ({dir.x:F2},{dir.z:F2}) 오른쪽 {Side(1f)} · 왼쪽 {Side(-1f)}");
            }
            Debug.Log("[EnvSetup] 와룡 살핌" + Environment.NewLine + string.Join(Environment.NewLine, lines));
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// 7차 묶음 — Pass6 에서(초록 뒤·난간 뒤·합치기 전)
        static string Pass7(Zone1Data d, Transform zoneRoot, Transform env, Func<string, GameObject> prefab)
        {
            var rng = new System.Random(77);
            var ivy = new MB6();
            string plaza = BuildPlaza7(d, zoneRoot, env, prefab);
            string ivyMsg = Ivy7(d, env, ivy, rng);
            string wall = WaryongWall7(d, env, ivy, rng);
            string lot = Parking7(d, zoneRoot, env, prefab);
            if (ivy.V.Count > 0)
            {
                var root = env.Find("Green6") ?? env;
                var m = CardMat6("ivy", "ivy", new Color(0.92f, 0.96f, 0.88f), 0.02f, 0.6f, true);
                Obj6("담쟁이", root, ivy.Save("g7_ivy"), m, ShadowCastingMode.On);
            }
            return $"7차: {plaza} · {ivyMsg} · {wall} · {lot}";
        }
    }
}
