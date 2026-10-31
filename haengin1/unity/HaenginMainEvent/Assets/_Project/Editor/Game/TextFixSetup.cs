// 행인1의 메인이벤트 — 글자·배경 정리 중 장면 손보기(2026-10-31 대표): 노가다 현장 안내 글을 깨끗한 판 위로(판 없이 벽·하늘에 걸쳐 있던 것).
//   -executeMethod Haengin.EditorGame.TextFixSetup.Site   (StorySetup.Stages 를 다시 돌려도 같은 판이 생김 — StorySetup.SiteBoard)
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Haengin.EditorGame
{
    public static class TextFixSetup
    {
        const string Mat = "Assets/_Project/Materials/Env/Stage_SiteBoard.mat";

        public static void Board(Transform label)
        {
            var old = label.parent.Find("이름판 판");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var m = AssetDatabase.LoadAssetAtPath<Material>(Mat);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "Stage_SiteBoard", color = new Color(0.93f, 0.90f, 0.80f) };
                m.SetColor("_BaseColor", new Color(0.93f, 0.90f, 0.80f));
                AssetDatabase.CreateAsset(m, Mat);
            }
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            b.name = "이름판 판";
            Object.DestroyImmediate(b.GetComponent<Collider>());
            b.transform.SetParent(label.parent, false);
            b.transform.localPosition = label.localPosition + new Vector3(0f, 0f, 0.06f);
            b.transform.localScale = new Vector3(5.4f, 1.5f, 0.06f);
            b.GetComponent<Renderer>().sharedMaterial = m;
            var t = label.GetComponent<TMP_Text>();
            if (t != null) { t.fontSize = 6f; t.rectTransform.sizeDelta = new Vector2(5.2f, 1.4f); t.transform.localPosition = label.localPosition + new Vector3(0f, 0f, -0.01f); }
        }

        public static void Site()
        {
            var s = EditorSceneManager.OpenScene("Assets/_Project/Scenes/St_Site.unity", OpenSceneMode.Single);
            foreach (var root in s.GetRootGameObjects())
            {
                var l = root.transform.Find("이름판");
                if (l == null) continue;
                Board(l);
                Debug.Log("[TextFix] 현장 안내판 " + root.name);
            }
            EditorSceneManager.SaveScene(s);
            AssetDatabase.SaveAssets();
        }
    }
}
