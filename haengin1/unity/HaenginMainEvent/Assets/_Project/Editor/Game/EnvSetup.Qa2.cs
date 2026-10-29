// 행인1의 메인이벤트 — QA 2차(2026-10-26, docs/15 7장): Zone1 키트 끝에 붙는 고침
//   A6 맵 끝 담 위 막기(EdgeGuard): 구역 경계 4m 안에서 '딛을 수 있는 땅이 아닌 윗면'(담·옹벽·축대·지붕 끝 — 바로 아래 땅보다 0.25m 넘게 높은 Wall/Default 윗면)을 찾아
//      그 위 0.05 ~ 3.0m 에 보이지 않는 막(PlayerOnly — 몸만 막고 카메라는 통과). 예전: 맵 끝 담장 꼭대기에 올라서 경계벽까지 걸어가 '발밑에 땅 없음'(B22).
//   배치(다시 짓지 않고 막만): -executeMethod Haengin.EditorGame.EnvSetup.EdgeGuardOnly
using System;
using System.Collections.Generic;
using System.Linq;
using Haengin.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Haengin.EditorGame
{
    public static partial class EnvSetup
    {
        public const string EdgeGuardRoot = "EdgeGuard_Qa2";
        public const float EdgeBand = 4.0f, EdgeStep = 0.5f, EdgeRise = 0.25f, EdgeHeight = 3.0f;

        /// 경계 띠(4m)의 담 윗면 위에 몸 막이. 돌려준 값 = 로그 한 줄
        public static string EdgeGuard(Zone1Data d, Transform zoneRoot)
        {
            var old = zoneRoot.Find(EdgeGuardRoot);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            Physics.SyncTransforms();
            var root = new GameObject(EdgeGuardRoot).transform; root.SetParent(zoneRoot, false);
            Vector3 mn = d.BoundsMin, mx = d.BoundsMax;
            int hitMask = ~Layers.Mask(Layers.Player, Layers.IgnoreRaycast, Layers.Interact, Layers.CamBlock, Layers.Fighter, Layers.Crowd, Layers.PlayerOnly);
            int groundMask = Layers.Mask(Layers.Ground);
            float top = mx.y + 30f;
            // (칸 x·z 정수 열쇠) → 윗면 높이 · 이름
            var cells = new Dictionary<(int, int), (float y, string what)>();
            void Probe(float x, float z)
            {
                var o = new Vector3(x, top, z);
                if (!Physics.Raycast(o, Vector3.down, out var h, top - mn.y + 40f, hitMask, QueryTriggerInteraction.Ignore)) return;
                int layer = h.collider.gameObject.layer;
                if (layer == Layers.Ground || h.normal.y < 0.7f) return;
                float rise = 99f;
                if (Physics.Raycast(h.point - Vector3.up * 0.02f, Vector3.down, out var g, 30f, groundMask, QueryTriggerInteraction.Ignore)) rise = h.point.y - g.point.y;
                if (rise < EdgeRise) return;
                var k = (Mathf.FloorToInt(x / EdgeStep), Mathf.FloorToInt(z / EdgeStep));
                if (!cells.TryGetValue(k, out var c) || h.point.y > c.y) cells[k] = (h.point.y, h.collider.name);
            }
            for (float off = EdgeStep * 0.5f; off < EdgeBand; off += EdgeStep)
            {
                for (float x = mn.x; x <= mx.x; x += EdgeStep) { Probe(x + EdgeStep * 0.5f, mn.z + off); Probe(x + EdgeStep * 0.5f, mx.z - off); }
                for (float z = mn.z; z <= mx.z; z += EdgeStep) { Probe(mn.x + off, z + EdgeStep * 0.5f); Probe(mx.x - off, z + EdgeStep * 0.5f); }
            }
            // 같은 줄(z 열쇠)에서 이웃하고 높이가 비슷한(±0.3m) 칸끼리 합쳐 상자 하나로
            int boxes = 0;
            var done = new HashSet<(int, int)>();
            var names = new Dictionary<string, int>();
            foreach (var kv in cells.OrderBy(c => c.Key.Item2).ThenBy(c => c.Key.Item1))
            {
                if (done.Contains(kv.Key)) continue;
                int x0 = kv.Key.Item1, z = kv.Key.Item2, x1 = x0;
                float yMin = kv.Value.y, yMax = kv.Value.y;
                done.Add(kv.Key);
                while (cells.TryGetValue((x1 + 1, z), out var nx) && Mathf.Abs(nx.y - kv.Value.y) <= 0.3f && !done.Contains((x1 + 1, z))) { x1++; done.Add((x1, z)); yMin = Mathf.Min(yMin, nx.y); yMax = Mathf.Max(yMax, nx.y); }
                var go = new GameObject($"막 {boxes:000}") { layer = Layers.PlayerOnly };
                go.transform.SetParent(root, false);
                float cx = (x0 + x1 + 1) * 0.5f * EdgeStep, cz = (z + 0.5f) * EdgeStep;
                float y0 = yMin + 0.05f, y1 = yMax + EdgeHeight;
                go.transform.position = new Vector3(cx, (y0 + y1) * 0.5f, cz);
                go.AddComponent<BoxCollider>().size = new Vector3((x1 - x0 + 1) * EdgeStep, y1 - y0, EdgeStep);
                GameObjectUtility.SetStaticEditorFlags(go, 0);
                boxes++;
                string nm = kv.Value.what; int sp = nm.IndexOf(' '); if (sp > 0) nm = nm.Substring(0, sp);
                names[nm] = names.TryGetValue(nm, out int c0) ? c0 + 1 : 1;
            }
            Physics.SyncTransforms();
            string top5 = string.Join(", ", names.OrderByDescending(x => x.Value).Take(6).Select(x => $"{x.Key} {x.Value}"));
            return $"맵 끝 담 위 막기(QA 2차 A6): 경계 띠 {EdgeBand}m · 담 윗면 칸 {cells.Count} → 막 {boxes}개(PlayerOnly) · 많은 것: {top5}";
        }

        /// 배치: Zone1 을 다시 짓지 않고 막만 다시 깐다
        public static void EdgeGuardOnly() => Run(() =>
        {
            string path = $"{Root}/Scenes/Zone1.unity";
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Transform zoneRoot = null;
            foreach (var g in scene.GetRootGameObjects()) if (g.name.StartsWith("Zone1")) { zoneRoot = g.transform; break; }
            if (zoneRoot == null) zoneRoot = scene.GetRootGameObjects().FirstOrDefault(g => g.transform.Find("Env") != null)?.transform;
            if (zoneRoot == null) throw new Exception("Zone1 루트를 못 찾음");
            Notes.Add(EdgeGuard(Zone1Data.Load(), zoneRoot));
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        });
    }
}
