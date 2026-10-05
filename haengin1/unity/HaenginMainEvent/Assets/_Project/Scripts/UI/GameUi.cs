// 행인1의 메인이벤트 — 화면 UI(TMP UGUI, 한글 글꼴 KR_Bold_SDF): 조작 안내 줄 · 짧은 알림 · 일시정지 메뉴 (07 문서 2-1·6장)
// 일시정지 메뉴는 패드·키보드·마우스 모두로 고른다:
//   열기/닫기 = Options(Menu) · Esc (Explore/Pause, PInput 이 토글)   고르기 = 십자키·왼스틱·방향키·W/S (Menu/Navigate)
//   선택 = × (A) · Enter · Space (Menu/Submit)   돌아가기 = ○ (B) · Backspace (Menu/Cancel)   마우스 = 올리면 고르고 누르면 선택
// 항목: 계속 / 카메라 자동 정렬(걷기·달리기 → 달리기만 → 끔, 좌우로도 바꿈) / 끝내기.
// 부품(Canvas·글)은 실행할 때 이 컴포넌트가 만든다(장면에는 컴포넌트 하나만 저장 → 장면 diff 작게). 실행 순서 −30 = PInput(−50) 다음:
// 같은 프레임에 열린 일시정지를 보고, 메뉴에서 누른 × 는 PInput 이 이미 이번 프레임을 끝낸 뒤라 상호작용으로 새지 않는다.
using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Haengin
{
    [DefaultExecutionOrder(-30)]
    public sealed class GameUi : MonoBehaviour
    {
        public const string MenuMap = "Menu";
        public InputActionAsset Actions;
        public TMP_FontAsset Font;
        public CamRig Cam;

        public static GameUi Instance { get; private set; }
        /// 테스트용: '끝내기'에서 Application.Quit 대신 부른다
        public static Action QuitHook;
        /// 조작 안내 줄을 바꿔 쓴다(전투 중 CombatHud 가 전투 조작으로 — 08 7-5). null = 탐색 안내
        public static string HintOverride;

        public enum Item { Resume = 0, AutoAlign = 1, Quit = 2 }
        public const int ItemCount = 3;

        /// 지금 고른 항목(0 = 계속)
        public int Selected { get; private set; }
        public bool MenuVisible => menuRoot != null && menuRoot.activeSelf;
        public string Label(int i) => rows != null && i >= 0 && i < rows.Length ? rows[i].Label.text : "";
        public string HintText => hint != null ? hint.text : "";

        static readonly Color Ink = new Color(0.102f, 0.078f, 0.090f);          // #1A1417
        static readonly Color Paper = new Color(0.957f, 0.937f, 0.902f);         // #F4EFE6
        static readonly Color Accent = new Color(0.886f, 0.345f, 0.173f);        // #E2582C 길잡이 주황
        const string Hint = "이동 WASD·왼스틱   달리기 Shift·R2   카메라 마우스·오른스틱   카메라 정렬 Q·L1   일시정지 Esc·Options   정보 F1";
        const string MenuHint = "↑↓ 고르기    × (A) · Enter 선택    ○ (B) · Esc 돌아가기";

        struct Row { public RectTransform Rt; public Image Plate, Bar; public TextMeshProUGUI Label; }

        InputAction nav, submit, cancel;
        bool prevSubmit, prevCancel, wasPaused, built;
        int heldDir, heldSide;
        float repeatAt;
        Vector2 lastMouse;
        GameObject menuRoot, toastPlate;
        TextMeshProUGUI hint, toastText;
        Row[] rows;

        void OnEnable() { Instance = this; }
        void OnDisable() { if (Instance == this) Instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; QuitHook = null; HintOverride = null; }

        System.Collections.IEnumerator Start()
        {
            if (Cam == null) Cam = FindAnyObjectByType<CamRig>();
            Build();
            Bind();
            // 실행 확인 한 줄(Player.log): 메뉴·안내 글자가 한글 글꼴에 다 있는지(기호 ↑↓×○◀▶ 포함)
            yield return new WaitForSecondsRealtime(3.4f);
            var f = hint != null ? hint.font : null;
            string all = Hint + MenuHint + "일시정지계속끝내기카메라 자동 정렬◀▶걷기·달리기달리기만끔";
            uint[] missing = null;
            bool ok = f != null && f.HasCharacters(all, out missing, false, true);
            Debug.Log($"[M1] 화면 UI 확인: 글꼴 {(f != null ? f.name : "없음")} · 글꼴에 없는 글자 {(ok ? 0 : missing?.Length ?? -1)} · 메뉴 입력 {(nav != null ? "Menu 맵" : "없음")} · 카메라 자동 정렬 {(Cam != null ? AutoName(Cam.AutoMode) : "-")}");
        }

        void Bind()
        {
            if (nav != null || Actions == null) return;
            nav = Actions.FindAction(MenuMap + "/Navigate", false);
            submit = Actions.FindAction(MenuMap + "/Submit", false);
            cancel = Actions.FindAction(MenuMap + "/Cancel", false);
            Actions.FindActionMap(MenuMap, false)?.Enable();
            if (nav == null) Debug.LogError("[GameUi] 입력 에셋에 Menu 맵(Navigate·Submit·Cancel)이 없습니다");
        }

        /// 누른 순간: 이번 프레임에 눌렸거나(WasPressedThisFrame), 지난 프레임엔 안 눌렸는데 지금 눌려 있음(테스트 장치처럼 프레임 밖에서 처리된 입력)
        public static bool Edge(InputAction a, ref bool prev)
        {
            if (a == null) return false;
            bool p = a.IsPressed();
            bool e = a.WasPressedThisFrame() || (p && !prev);
            prev = p;
            return e;
        }

        void Update()
        {
            if (!built) Build();
            Bind();
            bool paused = GameState.Paused;
            bool sub = Edge(submit, ref prevSubmit), can = Edge(cancel, ref prevCancel);   // 열려 있지 않을 때도 이전 상태는 기록
            if (paused != wasPaused)
            {
                wasPaused = paused;
                menuRoot.SetActive(paused);
                if (paused) { Selected = 0; heldDir = heldSide = 0; lastMouse = MousePos(); }
                Refresh();
                sub = can = false;   // 여는 프레임의 입력은 버린다
            }
            if (paused) Menu(sub, can);

            // 조작 안내 줄(전투 중엔 전투 조작)
            if (hint != null)
            {
                string want = HintOverride ?? Hint;
                if (hint.text != want) hint.text = want;
            }

            // 짧은 알림(DebugHud.Toast 가 넣음)
            bool on = !string.IsNullOrEmpty(DebugHud.ToastText) && Time.unscaledTime < DebugHud.ToastUntil;
            if (toastPlate.activeSelf != on) toastPlate.SetActive(on);
            if (on && toastText.text != DebugHud.ToastText) toastText.text = DebugHud.ToastText;
        }

        void Menu(bool sub, bool can)
        {
            Vector2 v = nav != null ? nav.ReadValue<Vector2>() : Vector2.zero;
            int dir = v.y > 0.5f ? -1 : v.y < -0.5f ? 1 : 0;      // 위 = 앞 항목
            int side = v.x > 0.5f ? 1 : v.x < -0.5f ? -1 : 0;
            float now = Time.unscaledTime;
            if (dir == 0) heldDir = 0;
            else if (dir != heldDir) { Move(dir); heldDir = dir; repeatAt = now + 0.40f; }
            else if (now >= repeatAt) { Move(dir); repeatAt = now + 0.15f; }   // 누르고 있으면 반복
            if (side != heldSide) { heldSide = side; if (side != 0 && Selected == (int)Item.AutoAlign) CycleAuto(side); }

            // 마우스: 움직였을 때만 올린 항목을 고른다(패드로 고른 것을 가만히 있는 커서가 빼앗지 않게), 누르면 선택
            var mouse = Mouse.current;
            if (mouse != null)
            {
                var mp = mouse.position.ReadValue();
                int over = RowAt(mp);
                if ((mp - lastMouse).sqrMagnitude > 4f && over >= 0 && over != Selected) { Selected = over; Refresh(); }
                lastMouse = mp;
                if (mouse.leftButton.wasPressedThisFrame && over >= 0) { Selected = over; Refresh(); sub = true; }
            }

            if (can) { GameState.SetPaused(false); return; }
            if (sub) Activate((Item)Selected);
        }

        void Move(int d)
        {
            Selected = (Selected + d + ItemCount) % ItemCount;
            Refresh();
        }

        void Activate(Item it)
        {
            switch (it)
            {
                case Item.Resume: GameState.SetPaused(false); break;
                case Item.AutoAlign: CycleAuto(1); break;
                case Item.Quit:
                    Debug.Log("[M1] 끝내기");
                    if (QuitHook != null) { QuitHook(); break; }
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif
                    break;
            }
        }

        /// 걷기·달리기(Always) → 달리기만 → 끔 → … (side −1 이면 반대로)
        void CycleAuto(int side)
        {
            if (Cam == null) return;
            CamTuning.Auto[] order = { CamTuning.Auto.Always, CamTuning.Auto.RunOnly, CamTuning.Auto.Off };
            int i = Array.IndexOf(order, Cam.AutoMode);
            if (i < 0) i = 0;
            var m = order[(i + (side >= 0 ? 1 : order.Length - 1)) % order.Length];
            Cam.SetAutoMode(m, true);
            Debug.Log($"[M1] 카메라 자동 정렬: {AutoName(m)}");
            Refresh();
        }

        public static string AutoName(CamTuning.Auto m) => m switch
        {
            CamTuning.Auto.Always => "걷기·달리기",
            CamTuning.Auto.RunOnly => "달리기만",
            _ => "끔",
        };

        void Refresh()
        {
            if (rows == null) return;
            rows[0].Label.text = "계속";
            rows[1].Label.text = "카메라 자동 정렬 ◀ " + AutoName(Cam != null ? Cam.AutoMode : CamTuning.Auto.Always) + " ▶";
            rows[2].Label.text = "끝내기";
            for (int i = 0; i < rows.Length; i++)
            {
                bool sel = i == Selected;
                rows[i].Plate.color = sel ? Paper : new Color(Ink.r, Ink.g, Ink.b, 0.62f);
                rows[i].Bar.enabled = sel;
                rows[i].Label.color = sel ? Ink : Paper;
            }
        }

        static Vector2 MousePos() => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

        int RowAt(Vector2 screen)
        {
            if (rows == null) return -1;
            for (int i = 0; i < rows.Length; i++)
                if (RectTransformUtility.RectangleContainsScreenPoint(rows[i].Rt, screen, null)) return i;
            return -1;
        }

        // ───────────────────────── 부품 만들기(실행할 때 한 번)
        void Build()
        {
            if (built) return;
            built = true;
            var font = Font != null ? Font : TMP_Settings.defaultFontAsset;

            if (!TryGetComponent<Canvas>(out var canvas)) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;   // 길잡이 HUD(10) 위
            if (!TryGetComponent<CanvasScaler>(out var scaler)) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            gameObject.layer = 5;   // UI

            // 조작 안내(왼쪽 아래 한 줄)
            var hp = Panel("조작 안내", transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(16f, 14f), Vector2.zero, new Color(Paper.r, Paper.g, Paper.b, 0.78f));
            Fit(hp, 12, 14, 5, 6);
            hint = Text("글", hp.transform, font, 18f, Ink, TextAlignmentOptions.MidlineLeft);
            hint.text = Hint;

            // 짧은 알림(아래 가운데)
            var b0 = new Vector2(0.5f, 0f);
            toastPlate = Panel("알림", transform, b0, b0, b0, new Vector2(0f, 96f), Vector2.zero, new Color(Ink.r, Ink.g, Ink.b, 0.82f)).gameObject;
            Fit(toastPlate.GetComponent<Image>(), 18, 18, 8, 9);
            toastText = Text("글", toastPlate.transform, font, 22f, Paper, TextAlignmentOptions.Center);
            toastPlate.SetActive(false);

            // 일시정지 메뉴
            menuRoot = new GameObject("일시정지", typeof(RectTransform)) { layer = 5 };
            menuRoot.transform.SetParent(transform, false);
            Stretch((RectTransform)menuRoot.transform);
            var dim = menuRoot.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.55f);
            dim.raycastTarget = false;

            var title = Text("제목", menuRoot.transform, font, 44f, Paper, TextAlignmentOptions.Center);
            Place(title.rectTransform, new Vector2(0f, 150f), new Vector2(600f, 64f));
            title.text = "일시정지";

            rows = new Row[ItemCount];
            for (int i = 0; i < ItemCount; i++)
            {
                var c5 = new Vector2(0.5f, 0.5f);
                var plate = Panel("항목 " + (i + 1), menuRoot.transform, c5, c5, c5, new Vector2(0f, 60f - i * 68f), new Vector2(560f, 56f), Ink);
                var bar = Panel("고른 표시", plate.transform, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(8f, 0f), Accent);
                var label = Text("글", plate.transform, font, 26f, Paper, TextAlignmentOptions.Center);
                Stretch(label.rectTransform);
                rows[i] = new Row { Rt = (RectTransform)plate.transform, Plate = plate, Bar = bar, Label = label };
            }
            var mh = Text("조작", menuRoot.transform, font, 20f, Paper, TextAlignmentOptions.Center);
            Place(mh.rectTransform, new Vector2(0f, -170f), new Vector2(900f, 34f));
            mh.text = MenuHint;
            menuRoot.SetActive(false);
            Refresh();
        }

        static Image Panel(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)) { layer = 5 };
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        /// 판 크기를 글에 맞춘다
        static void Fit(Image plate, int l, int r, int t, int b)
        {
            var lay = plate.gameObject.AddComponent<HorizontalLayoutGroup>();
            lay.padding = new RectOffset(l, r, t, b);
            lay.childAlignment = TextAnchor.MiddleCenter;
            lay.childControlWidth = lay.childControlHeight = true;
            lay.childForceExpandWidth = lay.childForceExpandHeight = false;
            var fit = plate.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        static TextMeshProUGUI Text(string name, Transform parent, TMP_FontAsset font, float size, Color c, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)) { layer = 5 };
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.color = c;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            return t;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static void Place(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }
    }
}
