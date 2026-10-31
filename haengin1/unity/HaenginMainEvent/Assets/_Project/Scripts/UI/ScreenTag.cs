// 행인1의 메인이벤트 — 사람 머리 위 짧은 글을 공중 3D 글자 대신 화면 판으로(2026-10-31 대표 "글자와 배경이 어지럽게 겹침")
//   월드 자리를 따라다니되 먹 판 위 종이색 글자 — 배경과 안 섞임. 화면 끝에서는 안쪽으로 밀림, 카메라 뒤면 숨김.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Haengin
{
    [DisallowMultipleComponent]
    public sealed class ScreenTag : MonoBehaviour
    {
        Image plate;
        TextMeshProUGUI text;
        Vector3 world;
        public bool Shown => plate != null && plate.gameObject.activeSelf;
        public string Text => text != null ? text.text : "";

        public static ScreenTag Make(string name, TMP_FontAsset font, float size, int order = 22)
        {
            var go = new GameObject(name);
            UiKit.Canvas(go, order);
            var tag = go.AddComponent<ScreenTag>();
            tag.plate = UiKit.Panel("판", go.transform, new Vector2(0f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(40f, 40f), new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.94f));
            UiKit.Fit(tag.plate, 14, 14, 5, 7);
            tag.text = UiKit.Text("글", tag.plate.transform, font, size, UiKit.Paper, TextAlignmentOptions.Center);
            tag.plate.gameObject.SetActive(false);
            return tag;
        }

        public void Show(string s, Vector3 worldPos)
        {
            if (plate == null) return;
            text.text = s;
            world = worldPos;
            plate.gameObject.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)plate.transform);
            Place();
        }

        public void Follow(Vector3 worldPos) { world = worldPos; if (Shown) Place(); }

        public void Hide() { if (plate != null) plate.gameObject.SetActive(false); }

        void Place()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var sp = cam.WorldToScreenPoint(world);
            bool front = sp.z > 0.1f;
            if (plate.enabled != front) { plate.enabled = front; text.enabled = front; }
            if (!front) return;
            var r = (RectTransform)plate.transform;
            float k = r.lossyScale.x;
            float hw = r.rect.width * 0.5f * k, h = r.rect.height * k;
            sp.x = Mathf.Clamp(sp.x, hw + 8f, Screen.width - hw - 8f);
            sp.y = Mathf.Clamp(sp.y, 8f, Screen.height - h - 8f);
            r.position = new Vector3(sp.x, sp.y, 0f);
        }

        void LateUpdate() { if (Shown) Place(); }
    }
}
