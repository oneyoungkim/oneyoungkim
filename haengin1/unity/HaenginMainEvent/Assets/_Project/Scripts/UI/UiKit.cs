// 행인1의 메인이벤트 — M3 화면 UI 공용 부품(GameUi 의 만들기 함수와 같은 규칙: TMP UGUI · 1920×1080 기준 · 실행할 때 만듦)
// 색: 먹 #1A1417 · 종이 #F4EFE6 · 길잡이 주황 #E2582C(UI 강조·점화 비네팅만 — 09 3-5) · '참기' 붉은 #7A1E1E
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Haengin
{
    public static class UiKit
    {
        public static readonly Color Ink = new Color(0.102f, 0.078f, 0.090f);
        public static readonly Color Paper = new Color(0.957f, 0.937f, 0.902f);
        public static readonly Color Accent = new Color(0.886f, 0.345f, 0.173f);
        public static readonly Color Endure = new Color(0.478f, 0.118f, 0.118f);
        public static readonly Color Warm = new Color(0.851f, 0.584f, 0.353f);
        public static readonly Color Grey = new Color(0.55f, 0.53f, 0.52f);

        /// 화면 덮는 캔버스(오버레이, 1920×1080 기준, 높이 맞춤)
        public static Canvas Canvas(GameObject go, int order)
        {
            go.layer = 5;
            if (!go.TryGetComponent<Canvas>(out var c)) c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = order;
            if (!go.TryGetComponent<CanvasScaler>(out var s)) s = go.AddComponent<CanvasScaler>();
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(1920f, 1080f);
            s.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            s.matchWidthOrHeight = 1f;
            return c;
        }

        public static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = 5 };
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static Image Panel(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)) { layer = 5 };
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        public static TextMeshProUGUI Text(string name, Transform parent, TMP_FontAsset font, float size, Color c, TextAlignmentOptions align, bool wrap = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)) { layer = 5 };
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.color = c;
            t.alignment = align;
            t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            return t;
        }

        public static void Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
        }

        public static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        /// 판 크기를 글에 맞춘다(가로·세로 여백)
        public static void Fit(Image plate, int l, int r, int t, int b)
        {
            var lay = plate.gameObject.AddComponent<VerticalLayoutGroup>();
            lay.padding = new RectOffset(l, r, t, b);
            lay.childAlignment = TextAnchor.MiddleCenter;
            lay.childControlWidth = lay.childControlHeight = true;
            lay.childForceExpandWidth = lay.childForceExpandHeight = false;
            var fit = plate.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        /// 실제 시간(TimeFx 시계 — 고정 프레임 녹화에서도 같은 길이)
        public static float RealDt => TimeFx.RealDt > 0f ? TimeFx.RealDt : Mathf.Min(Time.unscaledDeltaTime, TimeFx.MaxDt);

        /// 먹 붓 획 모양 텍스처(가로 띠, 양 끝이 들쭉날쭉) — 장면 전환 먹 닦기·가계부 줄 긋기·도감 칸 테두리
        public static Texture2D BrushBand(int w, int h, int seed, float edge = 0.12f)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "BrushBand", wrapMode = TextureWrapMode.Clamp };
            var rng = new System.Random(seed);
            var px = new Color32[w * h];
            // 줄마다 왼쪽·오른쪽 끝이 다르게(붓털 결)
            float[] l = new float[h], r = new float[h];
            float a = 0f, b = 0f;
            for (int y = 0; y < h; y++)
            {
                a = Mathf.Lerp(a, (float)rng.NextDouble(), 0.35f);
                b = Mathf.Lerp(b, (float)rng.NextDouble(), 0.35f);
                l[y] = edge * w * (0.15f + 0.85f * a);
                r[y] = w - edge * w * (0.15f + 0.85f * b);
            }
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float d = Mathf.Min(x - l[y], r[y] - x);
                    byte al = (byte)(Mathf.Clamp01(d / 6f) * 255f);
                    px[y * w + x] = new Color32(26, 20, 23, al);
                }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }
    }
}
