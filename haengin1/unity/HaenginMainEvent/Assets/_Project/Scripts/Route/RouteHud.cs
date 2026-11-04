// 행인1의 메인이벤트 — 길잡이 HUD 한 줄: "다음: 반시우네 집 앞 · 23m" — B안(docs/16): 오른쪽 위 '다음' 판(머리말 '다음 · 23m' + 제목). 자유 이동 중엔 세계 HUD 의 '다음' 판이 맡고 이 판은 숨김
// 글은 RouteGuide.Current 에서 읽기만 한다(HudLine). 도착하면 1.6초 동안 "도착: …" 을 보이고 다음 목표로 넘어간다.
// 마지막 체크포인트에 닿으면 "도착: …" 을 4초 보인 뒤 판을 접는다. 부품(Canvas·판·글)은 RouteSetup(에디터)이 만든다.
// 다른 HUD(대사·퀘스트)는 RouteGuide 의 공개 값·이벤트(CurrentTarget · Distance · Reached · Completed)를 직접 써도 된다.
// 2026-10-06(3차 검수): 다음 목표가 화면 밖·등 뒤면 화면 가장자리에 주황 방향 화살표(목표 전용 색 #E2582C, 먹색 테두리)를 띄운다.
//   자리 = 화면 둘레 타원 위, 카메라 기준 수평 각도(앞 = 위, 오른쪽 = 오른쪽, 뒤 = 아래). 위쪽이면 길잡이 판 밑으로. 부품은 실행할 때 만든다.
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Haengin
{
    [DefaultExecutionOrder(60)]   // RouteGuide(50) 다음 — 같은 프레임 거리로 그린다
    public sealed class RouteHud : MonoBehaviour
    {
        public TextMeshProUGUI Text;
        [Tooltip("판(배경). 글이 비면 숨김")]
        public GameObject Plate;
        public RouteGuide Guide;
        public float ArriveFlash = 1.6f, FinishHold = 4f;
        [Header("방향 화살표(목표가 화면 밖·등 뒤일 때)")]
        [Tooltip("화살표 크기(1080p 기준 픽셀)")] public float ArrowSize = 54f;
        [Tooltip("화면 가장자리에서 띄우는 거리(1080p 기준 픽셀)")] public float ArrowMargin = 64f;
        [Tooltip("목표 기준점 높이(원판 위 m) — 기둥 가운데쯤")] public float ArrowAimHeight = 1.5f;
        [Tooltip("이 뷰포트 안쪽에 목표가 보이면 화살표를 숨긴다(가장자리 여유)")] public float ArrowInset = 0.04f;

        static readonly Color Accent = new Color(0.886f, 0.345f, 0.173f);   // #E2582C 길잡이 주황
        static readonly Color Ink = new Color(0.102f, 0.078f, 0.090f);      // #1A1417
        RectTransform arrow;
        Image arrowImg;
        float arrowAlpha;
        /// 화살표가 보이는가 · 화면 위치(픽셀) · 가리키는 각도(도, 0 = 위, + = 시계 방향)
        public bool ArrowShown { get; private set; }
        public Vector2 ArrowScreen { get; private set; }
        public float ArrowAngle { get; private set; }

        RouteGuide hooked;
        string flash;
        float flashUntil = -1f;
        bool finished;
        string last;

        void OnEnable() { Hook(); BStyle(); }
        /// 지금 '도착'(1.6초)·완주(4초) 알림 중인지 — 자유 이동 중엔 이 판 대신 세계 HUD '다음' 판 머리말이 '도착'으로 바뀜(B12)
        public bool Flashing => !string.IsNullOrEmpty(flash) && Time.unscaledTime < flashUntil;
        void OnDisable() => Unhook();

        /// 실행 확인 한 줄(Player.log, DebugHud 의 '[M1] 실행 확인' 옆): HUD 글·한글 글리프가 실제로 그려지는지
        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(3.2f);
            if (Text == null) yield break;
            Text.ForceMeshUpdate();
            uint[] missing = null;
            bool all = Text.font != null && Text.font.HasCharacters(Text.text, out missing, false, true);
            var tags = FindAnyObjectByType<NameTags>();
            Debug.Log($"[M1] 길잡이 확인: HUD \"{Text.text}\" · 그린 글자 {Text.textInfo.characterCount} · 글꼴에 없는 글자 {(all ? 0 : missing?.Length ?? -1)} · " +
                      $"목표 {(hooked != null && hooked.CurrentTarget != null ? hooked.CurrentTarget.Label : "없음")} · 보이는 이름표 {(tags != null ? tags.VisibleCount : -1)}");
        }

        void Hook()
        {
            var g = Guide != null ? Guide : RouteGuide.Current;
            if (g == hooked) return;
            Unhook();
            hooked = g;
            if (hooked == null) return;
            hooked.Reached += OnReached;
            hooked.Completed += OnCompleted;
        }

        void Unhook()
        {
            if (hooked == null) return;
            hooked.Reached -= OnReached;
            hooked.Completed -= OnCompleted;
            hooked = null;
        }

        void OnReached(int i)
        {
            var s = hooked != null && i >= 0 && i < hooked.Stops.Length ? hooked.Stops[i] : null;
            if (s == null) return;
            flash = "도착: " + s.Label;
            flashUntil = Time.unscaledTime + ArriveFlash;
        }

        void OnCompleted()
        {
            finished = true;
            flashUntil = Time.unscaledTime + FinishHold;
        }

        void LateUpdate()
        {
            Hook();
            BStyle();
            string line;
            if (hooked != null && !hooked.isActiveAndEnabled) line = "";      // M3: 무대 장면(Zone1 루트 꺼짐)에선 도착 알림도 숨김
            else if (Time.unscaledTime < flashUntil) line = flash;
            else if (finished || hooked == null || !hooked.isActiveAndEnabled) line = "";      // M3: 무대 장면에선 Zone1 루트(길잡이)가 꺼짐
            else line = hooked.HudLine();
            Show(line);
            UpdateArrow(string.IsNullOrEmpty(line) || finished ? null : hooked != null ? hooked.CurrentTarget : null);
        }

        /// 길잡이 판의 화면 사각형(픽셀, 아래 왼쪽 원점). 판이 꺼져 있으면 false. 이름표가 판 밑으로 비켜 갈 때 쓴다(NameTags)
        /// B안: 자유 이동 중엔 세계 HUD 의 '다음' 판이 이 판 대신 뜸 → 그 판의 사각형
        public bool TryGetPlateRect(out Rect r)
        {
            r = default;
            var world = WorldHud.Live;
            if (world != null && world.NextShown && world.NextRect != null && world.NextRect.gameObject.activeInHierarchy) return ScreenRect(world.NextRect, out r);
            if (Plate == null || !Plate.activeInHierarchy) return false;
            return ScreenRect((RectTransform)Plate.transform, out r);
        }

        static bool ScreenRect(RectTransform rt, out Rect r)
        {
            r = default;
            var cv = rt.GetComponentInParent<Canvas>();
            if (cv == null) return false;
            cv = cv.rootCanvas;
            var uiCam = cv.renderMode == RenderMode.ScreenSpaceOverlay ? null : cv.worldCamera;
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            Vector2 a = RectTransformUtility.WorldToScreenPoint(uiCam, c[0]), b = RectTransformUtility.WorldToScreenPoint(uiCam, c[2]);
            r = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
            return r.width > 1f && r.height > 1f;
        }

        // ───────────────────────── 방향 화살표
        /// 지금 카메라 기준으로 화살표를 바로 다시 계산(페이드 없이). 촬영처럼 화면 크기·캔버스 방식이 바뀐 직후에 부른다
        public void RefreshArrow()
        {
            UpdateArrow(finished || hooked == null || string.IsNullOrEmpty(last) ? null : hooked.CurrentTarget);
            arrowAlpha = ArrowShown ? 1f : 0f;
            if (arrow == null) return;
            arrow.gameObject.SetActive(ArrowShown);
            if (ArrowShown) arrowImg.color = Color.white;
        }

        void EnsureArrow()
        {
            if (arrow != null) return;
            var go = new GameObject("방향 화살표", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)) { layer = gameObject.layer };
            go.transform.SetParent(transform, false);
            arrow = (RectTransform)go.transform;
            arrow.anchorMin = arrow.anchorMax = new Vector2(0.5f, 0.5f);
            arrow.pivot = new Vector2(0.5f, 0.5f);
            arrow.sizeDelta = new Vector2(ArrowSize, ArrowSize);
            arrowImg = go.GetComponent<Image>();
            arrowImg.sprite = MakeArrowSprite();
            arrowImg.raycastTarget = false;
            arrowImg.color = new Color(1f, 1f, 1f, 0f);
            go.SetActive(false);
        }

        /// 위를 가리키는 화살촉(주황 + 먹색 테두리) 64×64 — 글꼴에 기대지 않게 코드로 그림
        static Sprite MakeArrowSprite()
        {
            const int N = 64;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { name = "RouteArrow", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            // 오목한 화살촉: 끝 (32,60) · 오른쪽 날개 (58,8) · 안쪽 홈 (32,21) · 왼쪽 날개 (6,8)
            var P = new[] { new Vector2(32f, 60f), new Vector2(58f, 8f), new Vector2(32f, 21f), new Vector2(6f, 8f) };
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    var q = new Vector2(x + 0.5f, y + 0.5f);
                    float d = SignedDist(q, P);                      // 안쪽 음수
                    float fill = Mathf.Clamp01(0.5f - (d + 3.2f));   // 테두리 3px 안쪽 = 주황
                    float all = Mathf.Clamp01(0.5f - d);             // 바깥 경계까지(먹색 테두리 포함)
                    var c = Color.Lerp(Ink, Accent, fill);
                    c.a = all;
                    px[y * N + x] = c;
                }
            tex.SetPixels(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
        }

        static float SignedDist(Vector2 q, Vector2[] poly)
        {
            float d = float.MaxValue;
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                Vector2 a = poly[j], b = poly[i], ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / ab.sqrMagnitude);
                d = Mathf.Min(d, (q - (a + ab * t)).magnitude);
                if ((a.y > q.y) != (b.y > q.y) && q.x < (b.x - a.x) * (q.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside ? -d : d;
        }

        void UpdateArrow(RouteGuide.Stop target)
        {
            EnsureArrow();
            var cam = Camera.main;
            bool want = false;
            if (target != null && cam != null)
            {
                var p = target.Pos + Vector3.up * ArrowAimHeight;
                var vp = cam.WorldToViewportPoint(p);
                bool onScreen = vp.z > 0.1f && vp.x > ArrowInset && vp.x < 1f - ArrowInset && vp.y > ArrowInset && vp.y < 1f - ArrowInset;
                if (!onScreen)
                {
                    want = true;
                    var v = cam.transform.InverseTransformDirection(p - cam.transform.position);
                    float ang = Mathf.Atan2(v.x, v.z);                 // 수평 각도: 0 = 앞, + = 오른쪽, ±π = 뒤
                    var cv = GetComponent<Canvas>();
                    float scale = cv != null ? cv.scaleFactor : 1f;
                    float W = cam.pixelWidth, H = cam.pixelHeight, m = ArrowMargin * scale;
                    float rx = W * 0.5f - m, ry = H * 0.5f - m;
                    var sp = new Vector2(W * 0.5f + Mathf.Sin(ang) * rx, H * 0.5f + Mathf.Cos(ang) * ry);
                    // 위쪽이면 길잡이 판 밑으로
                    if (TryGetPlateRect(out var plate) && sp.y > plate.yMin - ArrowSize * scale && Mathf.Abs(sp.x - plate.center.x) < plate.width * 0.5f + ArrowSize * scale)
                        sp.y = plate.yMin - ArrowSize * 0.9f * scale;
                    ArrowScreen = sp;
                    ArrowAngle = ang * Mathf.Rad2Deg;
                    var root = (RectTransform)transform;
                    var uiCam = cv != null && cv.renderMode != RenderMode.ScreenSpaceOverlay ? cv.worldCamera : null;
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(root, sp, uiCam, out var local))
                        arrow.anchoredPosition = local;
                    arrow.localRotation = Quaternion.Euler(0f, 0f, -ArrowAngle);
                    arrow.sizeDelta = new Vector2(ArrowSize, ArrowSize);
                }
            }
            ArrowShown = want;
            arrowAlpha = Mathf.MoveTowards(arrowAlpha, want ? 1f : 0f, Time.unscaledDeltaTime / 0.15f);
            if (want && Time.unscaledDeltaTime <= 0f) arrowAlpha = 1f;
            bool vis = arrowAlpha > 0.001f;
            if (arrow.gameObject.activeSelf != vis) arrow.gameObject.SetActive(vis);
            if (vis) arrowImg.color = new Color(1f, 1f, 1f, arrowAlpha);
        }

        /// 지금 글(테스트용) — B안 판은 머리말·제목으로 나눠 보여 주므로 원래 한 줄을 돌려줌
        public string Current => last ?? (Text != null ? Text.text : "");

        /// 도착 알림·완주 상태를 지운다(순간 이동·이어하기·촬영 때 RouteGuide.SetIndex 와 같이 부름)
        public void ResetFlash()
        {
            flash = null;
            flashUntil = -1f;
            finished = false;
        }

        public void Show(string line)
        {
            if (line != last)
            {
                last = line;
                bool has = !string.IsNullOrEmpty(line);
                if (bHead != null)
                {
                    // "다음: 국밥집 · 73m" → 머리말 「다음 · 73m」 + 제목 「국밥집」 · "도착: 국밥집" → 「도착」 + 「국밥집」
                    string s = line ?? "", head = "", title = s;
                    int c = s.IndexOf(": ", System.StringComparison.Ordinal);
                    if (c > 0) { head = s.Substring(0, c); title = s.Substring(c + 2); }
                    int dot = title.LastIndexOf(" · ", System.StringComparison.Ordinal);
                    if (dot > 0 && title.EndsWith("m")) { head += " · " + title.Substring(dot + 3); title = title.Substring(0, dot); }
                    bHead.text = UiKit.Tab(head);
                    Text.text = title;
                    UiKit.FitWidth(Text, UiKit.NextPlate.Inner, 32f, 22f);
                }
                else if (Text != null) Text.text = line ?? "";
                if (Text != null) Text.enabled = has;
            }
            // B안: 자유 이동 중엔 세계 HUD 오른쪽 위 '다음' 판이 같은 안내(거리 포함)를 맡음 → 이 판은 숨김(겹침·중복 없앰)
            var world = WorldHud.Live;
            bool on = !string.IsNullOrEmpty(last) && !(world != null && world.NextShown);
            if (Plate != null && Plate.activeSelf != on) Plate.SetActive(on);
        }

        // ───────────────────────── B안 판(docs/16): 장면에 저장된 옛 판(위 가운데 종이 띠)을 실행할 때 오른쪽 위 '다음' 판 모양으로
        TextMeshProUGUI bHead;
        bool bDone;

        void BStyle()
        {
            if (bDone || Plate == null || Text == null) return;
            bDone = true;
            var img = Plate.GetComponent<UnityEngine.UI.Image>();
            if (img == null || Text.gameObject == Plate || !Text.transform.IsChildOf(Plate.transform)) return;      // 시험용 맨 글 판은 그대로
            var f = UiFonts.Current;
            if (UiKit.Fallback == null) UiKit.Fallback = Text.font;
            foreach (var lg in Plate.GetComponents<UnityEngine.UI.LayoutGroup>()) lg.enabled = false;
            foreach (var cf in Plate.GetComponents<UnityEngine.UI.ContentSizeFitter>()) cf.enabled = false;
            for (int i = 0; i < Plate.transform.childCount; i++) { var ch = Plate.transform.GetChild(i); if (ch != Text.transform) ch.gameObject.SetActive(false); }
            var rt = (RectTransform)Plate.transform;
            var tr = new Vector2(1f, 1f);
            UiKit.Place(rt, tr, tr, new Vector2(-38f, -38f), new Vector2(UiKit.NextPlate.W, UiKit.NextPlate.H));
            img.color = UiKit.InkPlate;
            UiKit.EdgeLine(Plate.transform, UiKit.Edge.Left, UiKit.Accent);
            bHead = UiKit.Label("머리말", Plate.transform, f, new Vector2(UiKit.NextPlate.X, -12f), UiKit.NextPlate.Inner);
            if (f != null) Text.font = f.K;
            Text.fontSize = 32f; Text.color = UiKit.Paper; Text.alignment = TextAlignmentOptions.BaselineLeft;
            Text.textWrappingMode = TextWrappingModes.NoWrap; Text.overflowMode = TextOverflowModes.Ellipsis;
            UiKit.Place(Text.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(UiKit.NextPlate.X, -46f), new Vector2(UiKit.NextPlate.Inner, 60f));
            string l = last; last = null; Show(l);
        }
    }
}
