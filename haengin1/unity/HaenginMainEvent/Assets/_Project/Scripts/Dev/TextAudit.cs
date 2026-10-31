// 행인1의 메인이벤트 — 글자·배경 검사(2026-10-31 대표 "텍스트랑 배경이랑 어지럽게 겹쳐 보이는 게 많다")
//   화면 글자: 보이는 TMP UGUI 를 모아 ① 판 불투명도(뒤에 깔린 그림을 합성) ≥ 85% ② 대비 ≥ 4.5:1(큰 글자 3:1) ③ 글자끼리 안 겹침 ④ 화면 안.
//     배경 색 = 뒤의 판을 합성 + 판이 모자라면 실제 화면 표본(글자 둘레 띠). 앞의 불투명 판에 덮인 글자는 안 보이는 것으로 뺌.
//   월드 글자: TextMeshPro 3D — 뒤(글 읽는 면의 반대쪽)에 붙은 표면이 없으면 '공중', 눕거나 뒤집히면 표시. QA 봇(실제 화면)과 시험이 함께 씀.
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Haengin
{
    public static class TextAudit
    {
        public const float MinPlate = 0.85f, MinContrast = 4.5f, MinContrastBig = 3f, BigSize = 24f;

        public sealed class Item
        {
            public TextMeshProUGUI T;
            public string Path, Text, Plate = "", CutBy = "", Across = "";
            public Rect R;
            public float Size, PlateA, Contrast = 99f;
            public float Alpha = 1f;
            public Color TextC, Back;
            public bool Big => Size >= BigSize;
        }

        public sealed class WorldItem
        {
            public string Path, Text;
            public Vector3 Pos;
            public bool Floating, Lying;
        }

        static readonly Regex Tag = new Regex("<[^>]+>");
        static string Clean(string s) => Tag.Replace(s ?? "", "").Replace('\n', ' ').Trim();

        public static string PathOf(Transform t)
        {
            var parts = new List<string>();
            for (int i = 0; i < 4 && t != null; i++, t = t.parent) parts.Add(t.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        static float GroupAlpha(Component c)
        {
            float a = 1f;
            foreach (var cg in c.GetComponentsInParent<CanvasGroup>()) a *= cg.alpha;
            return a;
        }

        static Rect WorldRect(RectTransform rt, Bounds local)
        {
            var a = rt.TransformPoint(local.min); var b = rt.TransformPoint(local.max);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        static Rect GraphicRect(RectTransform rt)
        {
            var c = new Vector3[4]; rt.GetWorldCorners(c);
            return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
        }

        static float Cover(Rect plate, Rect text)
        {
            float w = Mathf.Min(plate.xMax, text.xMax) - Mathf.Max(plate.xMin, text.xMin), h = Mathf.Min(plate.yMax, text.yMax) - Mathf.Max(plate.yMin, text.yMin);
            if (w <= 0f || h <= 0f) return 0f;
            return w * h / Mathf.Max(1f, text.width * text.height);
        }

        static long Order(Graphic g)
        {
            var cv = g.canvas != null ? g.canvas.rootCanvas : null;
            return (cv != null ? (long)cv.sortingOrder : 0L) * 100000L + g.canvasRenderer.absoluteDepth;
        }

        static float Lum(Color c)
        {
            float L(float v) => v <= 0.04045f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
            return 0.2126f * L(c.r) + 0.7152f * L(c.g) + 0.0722f * L(c.b);
        }

        public static float Ratio(Color a, Color b)
        {
            float x = Lum(a), y = Lum(b);
            return (Mathf.Max(x, y) + 0.05f) / (Mathf.Min(x, y) + 0.05f);
        }

        static Color Over(Color top, float a, Color back) => new Color(top.r * a + back.r * (1f - a), top.g * a + back.g * (1f - a), top.b * a + back.b * (1f - a), 1f);

        /// 글자 둘레 띠의 어두운 쪽·밝은 쪽 표본(판이 모자라는 글자의 실제 배경)
        static void Ring(Texture2D shot, Rect r, out Color dark, out Color light)
        {
            dark = light = new Color(0.5f, 0.5f, 0.5f);
            if (shot == null) return;
            float sx = shot.width / (float)Screen.width, sy = shot.height / (float)Screen.height;
            var list = new List<Color>();
            const int N = 24;
            for (int i = 0; i < N; i++)
            {
                float t = (i + 0.5f) / N;
                foreach (var p in new[] { new Vector2(Mathf.Lerp(r.xMin, r.xMax, t), r.yMin - 4f), new Vector2(Mathf.Lerp(r.xMin, r.xMax, t), r.yMax + 4f), new Vector2(r.xMin - 4f, Mathf.Lerp(r.yMin, r.yMax, t)), new Vector2(r.xMax + 4f, Mathf.Lerp(r.yMin, r.yMax, t)) })
                {
                    int x = Mathf.RoundToInt(p.x * sx), y = Mathf.RoundToInt(p.y * sy);
                    if (x < 0 || y < 0 || x >= shot.width || y >= shot.height) continue;
                    list.Add(shot.GetPixel(x, y));
                }
            }
            if (list.Count == 0) return;
            list.Sort((a, b) => Lum(a).CompareTo(Lum(b)));
            dark = list[list.Count / 10]; light = list[list.Count * 9 / 10];
        }

        /// 지금 화면에 보이는 UI 글자. shot = 실제 화면(또는 UI 뺀 화면) — 판이 모자란 글자의 배경 표본
        public static List<Item> Ui(Texture2D shot, bool shotHasUi = true)
        {
            var graphics = Object.FindObjectsByType<Graphic>(FindObjectsSortMode.None)
                .Where(g => !(g is TMP_Text) && g.isActiveAndEnabled && g.canvas != null && g.canvas.isActiveAndEnabled && g.canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay && g.color.a > 0.02f)
                .Select(g => (g, order: Order(g), rect: GraphicRect(g.rectTransform), a: g.color.a * GroupAlpha(g)))
                .Where(x => x.a > 0.02f).OrderBy(x => x.order).ToList();
            var r = new List<Item>();
            foreach (var t in Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None))
            {
                if (t == null || !t.isActiveAndEnabled || string.IsNullOrWhiteSpace(t.text)) continue;
                if (t.canvas == null || !t.canvas.isActiveAndEnabled || t.canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                float a = t.color.a * GroupAlpha(t);
                if (a < 0.05f) continue;
                string clean = Clean(t.text);
                if (clean.Length == 0 || t.maxVisibleCharacters <= 0) continue;
                if (t.textInfo == null || t.textInfo.characterCount == 0) t.ForceMeshUpdate();
                var bounds = t.textBounds;
                if (bounds.size.x <= 0.5f || bounds.size.y <= 0.5f) continue;
                var rect = WorldRect(t.rectTransform, bounds);
                long ord = Order(t);
                // 앞의 불투명 판에 덮이면 안 보임
                if (graphics.Any(x => x.order > ord && x.a >= MinPlate && Cover(x.rect, rect) >= 0.9f)) continue;
                var cut = graphics.FirstOrDefault(x => x.order > ord && x.a >= 0.5f && x.g.canvas.rootCanvas == t.canvas.rootCanvas && Cover(x.rect, rect) >= 0.04f && Cover(x.rect, rect) < 0.9f);      // 같은 화면 안에서 일부만 덮임(다른 화면의 카드가 HUD 위에 뜬 건 정상)
                var it = new Item { T = t, Path = PathOf(t.transform), Text = clean.Length > 40 ? clean.Substring(0, 40) : clean, R = rect, Size = t.fontSize, TextC = t.color, CutBy = cut.g != null ? cut.g.name : "" };
                // 뒤의 판 합성
                float clear = 1f; Color acc = Color.black; string plate = "";
                var behind = graphics.Where(x => x.order < ord && Cover(x.rect, rect) >= 0.75f).ToList();
                foreach (var x in behind) { acc = Over(x.g.color, x.a, acc); clear *= 1f - x.a; plate = x.g.name; }
                it.PlateA = 1f - clear; it.Plate = plate;
                Ring(shot, rect, out var dark, out var light);
                // 판 합성: 맨 뒤(화면)부터
                Color Comp(Color bg) { var c = bg; foreach (var x in behind) c = Over(x.g.color, x.a, c); return c; }
                var cd = Comp(dark); var cl = Comp(light);
                // 그림이 든 판(종이·붓 띠)은 색 값으로 못 잼 → 화면 표본(UI 포함 화면일 때) · 없으면 대비 검사 안 함
                // (맨 위 불투명 판만 봄 — 그 뒤 전환 그림은 안 보임)
                bool textured = false;
                for (int b = behind.Count - 1; b >= 0; b--)
                {
                    var g = behind[b].g;
                    if (g.mainTexture != null && g.mainTexture != Texture2D.whiteTexture && !(g is Image im && im.sprite == null)) textured = true;
                    if (behind[b].a >= MinPlate) break;
                }
                if (textured && shotHasUi) { cd = dark; cl = light; }
                float rd = Ratio(Over(t.color, a, cd), cd), rl = Ratio(Over(t.color, a, cl), cl);
                if (textured && !shotHasUi) rd = rl = 99f;
                it.Contrast = Mathf.Min(rd, rl);
                it.Alpha = a;
                var across = graphics.FirstOrDefault(x => x.order < ord && x.a >= 0.5f && x.g.canvas.rootCanvas == t.canvas.rootCanvas && !t.transform.IsChildOf(x.g.transform) && Cover(x.rect, rect) >= 0.05f && Cover(x.rect, rect) < 0.75f);
                if (across.g != null) it.Across = across.g.name;
                it.Back = rd < rl ? cd : cl;
                r.Add(it);
            }
            return r;
        }

        /// 규칙 위반 글귀들
        public static List<string> Problems(List<Item> items)
        {
            var bad = new List<string>();
            items = items.Where(i => i.Alpha >= 0.6f).ToList();      // 사라지는 중인 글(알림 끝 0.4초)은 뺌
            foreach (var i in items)
            {
                if (i.PlateA < MinPlate) bad.Add($"판 {i.PlateA:0.00}\t{i.Path}\t{i.Text}");
                if (i.CutBy.Length > 0) bad.Add($"가림 {i.CutBy}\t{i.Path}\t{i.Text}");
                if (i.Across.Length > 0) bad.Add($"걸침 {i.Across}\t{i.Path}\t{i.Text}");
                if (i.Contrast < (i.Big ? MinContrastBig : MinContrast)) bad.Add($"대비 {i.Contrast:0.0}\t{i.Path}\t{i.Text}");
                if (i.R.xMin < -1f || i.R.yMin < -1f || i.R.xMax > Screen.width + 1f || i.R.yMax > Screen.height + 1f) bad.Add($"화면 밖\t{i.Path}\t{i.Text}");
            }
            for (int a = 0; a < items.Count; a++)
                for (int b = a + 1; b < items.Count; b++)
                {
                    float w = Mathf.Min(items[a].R.xMax, items[b].R.xMax) - Mathf.Max(items[a].R.xMin, items[b].R.xMin), h = Mathf.Min(items[a].R.yMax, items[b].R.yMax) - Mathf.Max(items[a].R.yMin, items[b].R.yMin);
                    if (w <= 2f || h <= 2f) continue;
                    float small = Mathf.Min(items[a].R.width * items[a].R.height, items[b].R.width * items[b].R.height);
                    if (w * h > small * 0.08f) bad.Add($"겹침\t{items[a].Path}\t{items[a].Text} ↔ {items[b].Path} {items[b].Text}");
                }
            return bad;
        }

        /// 지금 켜져 있는 월드 글자. only = 카메라 시야 안만
        public static List<WorldItem> World(Camera cam = null)
        {
            var rends = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(m => m.enabled && m.gameObject.activeInHierarchy && m.GetComponent<TMP_Text>() == null).Select(m => m.bounds).ToList();
            var r = new List<WorldItem>();
            foreach (var t in Object.FindObjectsByType<TextMeshPro>(FindObjectsSortMode.None))
            {
                if (t == null || !t.isActiveAndEnabled || t.renderer == null || !t.renderer.enabled || string.IsNullOrWhiteSpace(t.text)) continue;
                var c = t.renderer.bounds.center;
                if (cam != null) { var v = cam.WorldToViewportPoint(c); if (v.z <= 0f || v.z > 45f || v.x < 0f || v.x > 1f || v.y < 0f || v.y > 1f) continue; }
                var n = t.transform.forward;
                bool stuck = false;
                foreach (float d in new[] { 0f, 0.03f, 0.06f, 0.12f, 0.25f })
                {
                    var p = c + n * d;
                    foreach (var b in rends) { var e = b; e.Expand(0.02f); if (e.Contains(p)) { stuck = true; break; } }
                    if (stuck) break;
                }
                if (t.name == "이름표" || t.GetComponentInParent<NameTags>(true) != null && t.name.Contains("이름표")) stuck = false;      // 거리 이름표는 뒤 건물 경계와 상관없이 공중
                r.Add(new WorldItem { Path = PathOf(t.transform), Text = Clean(t.text), Pos = c, Floating = !stuck, Lying = Mathf.Abs(n.y) > 0.7f });
            }
            return r;
        }
    }
}
