// 행인1의 메인이벤트 — QA Q25(docs/15, 2026-10-26): LOD 묶음 겹침 정리
//   구역 FBX(Z1_Alley·Z1_Wires …)를 들여올 때 유니티가 뿌리에 만든 LOD 묶음이 건물·담·전봇대 자식의 LOD 렌더러까지 쥐고 있고,
//   EnvSetup 이 자식마다 LOD 묶음을 또 달아 같은 렌더러가 두 묶음에 걸렸다('registered with more than one LODGroup' 경고 — Zone1 을 켤 때마다 240줄).
//   체육관 키트도 같은 꼴(Room_Shell·Cage …). 바깥 묶음에서 안쪽 묶음이 쥔 렌더러를 빼고, 비면 바깥 묶음을 지운다.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Haengin.EditorGame
{
    public static partial class EnvSetup
    {
        /// 배치: Zone1·체육관 장면을 열어 겹침만 정리하고 저장(다시 짓지 않음)
        public static void LodDedupOnly() => Run(() =>
        {
            foreach (var path in new[] { Root + "/Scenes/Zone1.unity", Root + "/Scenes/Gym.unity" })
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                int n = 0; var notes = new List<string>();
                foreach (var r in scene.GetRootGameObjects()) { var s = LodDedup(r.transform, out int k); n += k; if (k > 0) notes.Add(s); }
                Notes.Add($"{System.IO.Path.GetFileNameWithoutExtension(path)}: {(notes.Count > 0 ? string.Join(" · ", notes) : "겹침 없음")}");
                if (n > 0) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
            }
        });

        /// 안쪽(자식) LOD 묶음이 쥔 렌더러를 바깥 묶음에서 뺀다. 바꾼 묶음 수를 돌려줌
        public static string LodDedup(Transform root, out int changedGroups)
        {
            changedGroups = 0; int removed = 0, dropped = 0;
            foreach (var g in root.GetComponentsInChildren<LODGroup>(true))
            {
                var inner = new HashSet<Renderer>();
                foreach (var c in g.GetComponentsInChildren<LODGroup>(true))
                    if (c != g) foreach (var l in c.GetLODs()) foreach (var r in l.renderers) if (r != null) inner.Add(r);
                if (inner.Count == 0) continue;
                var lods = g.GetLODs(); bool changed = false; int left = 0;
                for (int i = 0; i < lods.Length; i++)
                {
                    var rs = lods[i].renderers ?? Array.Empty<Renderer>();
                    var keep = rs.Where(r => r != null && !inner.Contains(r)).ToArray();
                    if (keep.Length != rs.Length) { removed += rs.Count(r => r != null && inner.Contains(r)); lods[i].renderers = keep; changed = true; }
                    left += keep.Length;
                }
                if (!changed) continue;
                changedGroups++;
                if (left == 0) { Object.DestroyImmediate(g); dropped++; }
                else { g.SetLODs(lods); g.RecalculateBounds(); }
            }
            return changedGroups == 0 ? "" : $"{root.name} LOD 묶음 겹침 정리: 묶음 {changedGroups}개에서 렌더러 {removed}개 뺌 · 빈 묶음 {dropped}개 지움";
        }
    }
}

namespace Haengin.EditorGame
{
    public static partial class EnvSetup
    {
        /// QA Q30: 소품 프리팹은 충돌이 없음(작은 것은 일부러) — 그런데 갓길 1톤 트럭(5.1m)·레미콘(8.6m)까지 없어 시우가 트럭을 뚫고 지나가고,
        /// 카메라도 트럭을 뚫어 화면이 트럭 옆판으로 가득 찼다 → 큰 차는 LOD0 상자만 한 BoxCollider(Default 레이어 = 몸·카메라 둘 다 막힘)
        static readonly string[] SolidProps = { "truck_1ton_box", "mixer_truck" };

        static bool SolidCollider(GameObject root, string name)
        {
            if (Array.IndexOf(SolidProps, name) < 0 || root.GetComponent<BoxCollider>() != null) return false;
            var lg = root.GetComponent<LODGroup>();
            var rs = lg != null && lg.GetLODs().Length > 0 ? lg.GetLODs()[0].renderers.Where(r => r != null).ToArray() : root.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return false;
            var bb = rs[0].bounds; foreach (var r in rs) bb.Encapsulate(r.bounds);
            var bc = root.AddComponent<BoxCollider>();
            bc.center = root.transform.InverseTransformPoint(bb.center);
            var s = root.transform.lossyScale;
            bc.size = new Vector3(bb.size.x / Mathf.Max(1e-4f, s.x), bb.size.y / Mathf.Max(1e-4f, s.y), bb.size.z / Mathf.Max(1e-4f, s.z)) * 0.96f;
            return true;
        }

        /// 배치: 이미 있는 소품 프리팹에 큰 차 충돌만 더함(Zone1 을 다시 짓지 않음 — 장면의 트럭은 프리팹 인스턴스라 그대로 따라옴)
        public static void SolidPropsOnly() => Run(() =>
        {
            foreach (var name in SolidProps)
            {
                string pp = $"{PrefabDir}/P_{name}.prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(pp) == null) { Notes.Add($"{name}: 프리팹 없음"); continue; }
                var root = PrefabUtility.LoadPrefabContents(pp);
                bool added = SolidCollider(root, name);
                if (added) PrefabUtility.SaveAsPrefabAsset(root, pp);
                var bc = root.GetComponent<BoxCollider>();
                Notes.Add($"{name}: 충돌 {(added ? "더함" : "이미 있음/없음")} {(bc != null ? bc.size.ToString("F2") : "")}");
                PrefabUtility.UnloadPrefabContents(root);
            }
        });
    }
}

namespace Haengin.EditorGame
{
    public static partial class EnvSetup
    {
        /// 배치(QA Q31): 무대 장면 4곳의 낮은 충돌 상자(창 밑 턱 0.35 · 밥상 0.32 · 받침 0.30)를 0.6m 로 — StageKitSetup.LowTop 과 같은 규칙, 무대를 다시 짓지 않음
        public static void StageLowCollidersOnly() => Run(() =>
        {
            foreach (var key in new[] { "Conv", "Gukbap", "Home", "School" })
            {
                string path = $"{Root}/Scenes/St_{key}.unity";
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                int n = 0;
                foreach (var go in scene.GetRootGameObjects())
                    foreach (var t in go.GetComponentsInChildren<Transform>(true))
                    {
                        if (t.name != "Colliders") continue;
                        foreach (Transform c in t)
                        {
                            var bc = c.GetComponent<BoxCollider>();
                            if (bc == null || c.gameObject.layer == Haengin.Layers.Ground) continue;
                            float bottom = c.localPosition.y + bc.center.y - bc.size.y * 0.5f, top = c.localPosition.y + bc.center.y + bc.size.y * 0.5f;
                            float nt = StageKitSetup.LowTop(bottom, top);
                            if (Mathf.Approximately(nt, top)) continue;
                            bc.size = new Vector3(bc.size.x, nt - bottom, bc.size.z);
                            c.localPosition = new Vector3(c.localPosition.x, (bottom + nt) * 0.5f - bc.center.y, c.localPosition.z);
                            n++;
                            Notes.Add($"{key}/{c.name}: 윗면 {top:F2} → {nt:F2}m");
                        }
                    }
                if (n > 0) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
            }
        });
    }
}

namespace Haengin.EditorGame
{
    public static partial class EnvSetup
    {
        /// 배치(QA Q20 2차): 하루편의점 포스기 화면에 카메라 막이(CamBlock) — 키트 manifest 에 넣은 것과 같은 상자, 무대를 다시 짓지 않음
        public static void StageCamBlockOnly() => Run(() =>
        {
            string path = $"{Root}/Scenes/St_Conv.unity";
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Transform cols = null;
            foreach (var go in scene.GetRootGameObjects())
                foreach (var t in go.GetComponentsInChildren<Transform>(true)) if (t.name == "Colliders") { cols = t; break; }
            if (cols == null) throw new Exception("St_Conv 에 Colliders 없음");
            if (cols.Find("Col_POS(카메라)") != null) { Notes.Add("이미 있음"); return; }
            Vector3 a = new Vector3(2.45f, 1.04f, -2.4f), b = new Vector3(2.85f, 1.45f, -2.0f);
            var c = new GameObject("Col_POS(카메라)") { layer = Haengin.Layers.CamBlock };
            c.transform.SetParent(cols, false);
            c.transform.localPosition = (a + b) * 0.5f;
            c.AddComponent<BoxCollider>().size = b - a;
            Notes.Add($"St_Conv 포스기 카메라 막이 {c.transform.position:F2}");
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        });
    }
}
