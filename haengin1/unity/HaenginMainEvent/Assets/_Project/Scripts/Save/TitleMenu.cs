// 행인1의 메인이벤트 — 타이틀(docs/09_M3_버티컬슬라이스_설계.md 2-10 · 1-4 T): 새 게임 / 이어하기 / (장면 고르기) / 연습장 / 설정 / 끝내기
// 이어하기 = 자동 저장 장면(수동 칸이 더 새것이면 그것). 깨진 파일·모르는 버전이면 경고 → '새 게임' 또는 '이전 자동 저장'.
// 장면 고르기 = 데모를 한 번 끝내면(PlayerPrefs haengin.demo.cleared) 회차 첫 장면 7곳 — 대표 확인용.
// 연습장 = M2 무대: Zone1(인카운터 Y4·야차 Y1 — 이야기 없이) · 전투 연습장(CombatLab). 7장 결정 21.
// 설정 = 흔들림 줄이기 · 자막 크기 3단 · 자동 진행 · 진동(PlayerPrefs).
// 입력 = HInput 의 Menu 맵(GameUi 일시정지 메뉴와 같은 규칙: 십자키·왼스틱·방향키·W/S 고르기, × · Enter · Space 선택, ○ · Backspace 돌아가기, 마우스).
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Haengin
{
    [DisallowMultipleComponent]
    public sealed class TitleMenu : MonoBehaviour
    {
        public InputActionAsset Actions;
        public UiFonts Fonts;
        public StoryDef Def;

        public static Action QuitHook;
        public const int MaxRows = 8;

        public string PageName => page != null ? page.Title : "";
        public int Selected { get; private set; }
        public int Count => page != null ? page.Items.Count : 0;
        public string Label(int i) => page != null && i >= 0 && i < page.Items.Count ? page.Items[i].Text() : "";
        public string Note => note != null ? note.text : "";
        public string LastError { get; private set; } = "";

        sealed class Item { public Func<string> Text; public Action Do; public Action<int> Side; }
        sealed class Page { public string Title; public string Note; public readonly List<Item> Items = new List<Item>(); public Page Back; }

        InputAction nav, submit, cancel;
        bool pSub, pCan, built;
        int heldDir, heldSide;
        float repeatAt, clock;
        Vector2 lastMouse;
        Page page;
        TextMeshProUGUI title, sub, head, note;
        GameObject band;
        struct Row { public RectTransform Rt; public Image Plate, Bar; public TextMeshProUGUI Label; }
        Row[] rows;

        void Start()
        {
            Build();
            if (Actions != null)
            {
                nav = Actions.FindAction("Menu/Navigate", false);
                submit = Actions.FindAction("Menu/Submit", false);
                cancel = Actions.FindAction("Menu/Cancel", false);
                InputMaps.Use(Actions, null);
                Actions.FindActionMap("Menu", false)?.Enable();
                pSub = submit != null && submit.IsPressed(); pCan = cancel != null && cancel.IsPressed();
            }
            GameState.SetPaused(false);
            GameState.LockCursor(false);
            if (!Application.isEditor && Array.Exists(Environment.GetCommandLineArgs(), a => a == "-m2smoke" || a == "-practice"))
            {
                Debug.Log("[M3] 타이틀: -m2smoke/-practice → 연습장 Zone1 로 바로");
                StoryBoot.Go(new StoryBoot.Req { Mode = StoryBoot.Mode.Practice });
                return;
            }
            Show(Main());
            Debug.Log($"[M3] 타이틀: 항목 {Count}개 · 이어하기 {(SaveStore.Exists(SaveStore.Auto) ? "있음" : "없음")}");
        }

        // ───────────────────────── 쪽
        Page Main()
        {
            var p = new Page { Title = "", Note = "" };
            Add(p, "새 게임", () => StoryBoot.Go(new StoryBoot.Req { Mode = StoryBoot.Mode.New }));
            var latest = Latest(out string err, out string slot);
            if (latest != null) Add(p, "이어하기 — " + latest.Label, () => StoryBoot.Go(new StoryBoot.Req { Mode = StoryBoot.Mode.Continue, Save = latest }));
            else if (err != null && err != "없음") Add(p, "이어하기 — 저장 파일 문제", () => Show(Broken(err)));
            if (PlayerPrefs.GetInt("haengin.demo.cleared", 0) == 1) Add(p, "장면 고르기", () => Show(Select(p)));
            Add(p, "연습장", () => Show(Practice(p)));
            Add(p, "설정", () => Show(Settings(p)));
            Add(p, "끝내기", Quit);
            return p;
        }

        /// 자동·수동 중 더 새것(둘 다 장면 시작 상태)
        public static SaveData Latest(out string error, out string slot)
        {
            var a = SaveStore.Read(SaveStore.Auto, out string ea);
            var m = SaveStore.Read(SaveStore.Manual, out string em);
            slot = SaveStore.Auto;
            error = ea;
            if (a == null && m == null) { error = ea != "없음" ? ea : em; return null; }
            if (a == null) { slot = SaveStore.Manual; error = null; return m; }
            if (m != null && string.CompareOrdinal(m.SavedAt, a.SavedAt) > 0) { slot = SaveStore.Manual; return m; }
            error = null;
            return a;
        }

        Page Broken(string err)
        {
            LastError = err;
            var p = new Page { Title = "저장 파일을 읽지 못했습니다", Note = $"자동 저장: {err}", Back = page };
            Add(p, "새 게임", () => StoryBoot.Go(new StoryBoot.Req { Mode = StoryBoot.Mode.New }));
            var prev = SaveStore.ReadPrev(SaveStore.Auto, out string pe);
            if (prev != null) Add(p, "이전 자동 저장 — " + prev.Label, () => StoryBoot.Go(new StoryBoot.Req { Mode = StoryBoot.Mode.Continue, Save = prev }));
            Add(p, "돌아가기", () => Show(p.Back));
            Debug.LogWarning($"[M3] 저장 파일 경고: {err} · 이전 자동 저장 {(prev != null ? "있음" : pe)}");
            return p;
        }

        Page Select(Page back)
        {
            var p = new Page { Title = "장면 고르기", Note = "회차 첫 장면에서 시작(이야기 상태는 처음부터)", Back = back };
            if (Def != null)
                foreach (var e in Def.Episodes)
                {
                    var id = $"{e.No}-0";
                    Add(p, e.CardTitle, () => StoryBoot.Go(new StoryBoot.Req { Mode = StoryBoot.Mode.Select, SceneId = id }));
                }
            Add(p, "돌아가기", () => Show(back));
            return p;
        }

        Page Practice(Page back)
        {
            var p = new Page { Title = "연습장", Note = "M2 전투 — 이야기와 상관없음", Back = back };
            Add(p, "Zone1 — 뒷골목 인카운터 · 와룡공원 야차", () => StoryBoot.Go(new StoryBoot.Req { Mode = StoryBoot.Mode.Practice }));
            Add(p, "전투 연습장", () => StoryBoot.Go(null, StoryBoot.LabScene));
            Add(p, "돌아가기", () => Show(back));
            return p;
        }

        static readonly string[] SizeNames = { "작게", "보통", "크게" };

        Page Settings(Page back)
        {
            var p = new Page { Title = "설정", Note = "← → 로도 바꿈", Back = back };
            Add(p, () => $"흔들림 줄이기 ◀ {(Accessibility.Reduced ? "켬" : "끔")} ▶", () => Accessibility.Reduced = !Accessibility.Reduced, s => Accessibility.Reduced = !Accessibility.Reduced);
            Add(p, () => $"자막 크기 ◀ {SizeNames[Mathf.Clamp(PlayerPrefs.GetInt(SubtitleUi.SizeKey, 1), 0, 2)]} ▶", () => CycleSize(1), CycleSize);
            Add(p, () => $"자동 진행 ◀ {(DialogueRunner.Auto ? "켬" : "끔")} ▶", () => DialogueRunner.Auto = !DialogueRunner.Auto, s => DialogueRunner.Auto = !DialogueRunner.Auto);
            Add(p, () => $"진동 ◀ {(PlayerPrefs.GetInt(EndureFx.RumbleKey, 1) == 1 ? "켬" : "끔")} ▶", ToggleRumble, s => ToggleRumble());
            Add(p, "돌아가기", () => Show(back));
            return p;
        }

        static void CycleSize(int d) { int v = (PlayerPrefs.GetInt(SubtitleUi.SizeKey, 1) + (d >= 0 ? 1 : 2)) % 3; PlayerPrefs.SetInt(SubtitleUi.SizeKey, v); PlayerPrefs.Save(); }
        static void ToggleRumble() { PlayerPrefs.SetInt(EndureFx.RumbleKey, PlayerPrefs.GetInt(EndureFx.RumbleKey, 1) == 1 ? 0 : 1); PlayerPrefs.Save(); }

        static void Add(Page p, string text, Action act) => p.Items.Add(new Item { Text = () => text, Do = act });
        static void Add(Page p, Func<string> text, Action act, Action<int> side) => p.Items.Add(new Item { Text = text, Do = act, Side = side });

        void Quit()
        {
            Debug.Log("[M3] 타이틀: 끝내기");
            if (QuitHook != null) { QuitHook(); return; }
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void Show(Page p)
        {
            page = p;
            Selected = 0;
            heldDir = heldSide = 0;
            Refresh();
        }

        /// 테스트·봇: 이름이 text 로 시작하는 항목을 고른다
        public bool Choose(string text)
        {
            for (int i = 0; i < Count; i++)
                if (Label(i).StartsWith(text)) { Selected = i; Refresh(); page.Items[i].Do?.Invoke(); return true; }
            return false;
        }

        // ───────────────────────── 한 프레임
        void Update()
        {
            if (!built) return;
            clock += UiKit.RealDt;
            bool s = GameUi.Edge(submit, ref pSub), c = GameUi.Edge(cancel, ref pCan);
            Vector2 v = nav != null ? nav.ReadValue<Vector2>() : Vector2.zero;
            int dir = v.y > 0.5f ? -1 : v.y < -0.5f ? 1 : 0;
            int side = v.x > 0.5f ? 1 : v.x < -0.5f ? -1 : 0;
            if (dir == 0) heldDir = 0;
            else if (dir != heldDir) { Move(dir); heldDir = dir; repeatAt = clock + 0.4f; }
            else if (clock >= repeatAt) { Move(dir); repeatAt = clock + 0.15f; }
            if (side != heldSide) { heldSide = side; if (side != 0 && page.Items[Selected].Side != null) { page.Items[Selected].Side(side); Refresh(); } }
            var m = Mouse.current;
            if (m != null)
            {
                var mp = m.position.ReadValue();
                int over = RowAt(mp);
                if ((mp - lastMouse).sqrMagnitude > 4f && over >= 0 && over != Selected) { Selected = over; Refresh(); }
                lastMouse = mp;
                if (m.leftButton.wasPressedThisFrame && over >= 0) { Selected = over; s = true; }
            }
            if (c && page.Back != null) { Show(page.Back); return; }
            if (s) { page.Items[Selected].Do?.Invoke(); Refresh(); }
        }

        void Move(int d) { if (Count == 0) return; Selected = (Selected + d + Count) % Count; Refresh(); }

        int RowAt(Vector2 screen)
        {
            for (int i = 0; i < Count && i < MaxRows; i++)
                if (RectTransformUtility.RectangleContainsScreenPoint(rows[i].Rt, screen, null)) return i;
            return -1;
        }

        void Refresh()
        {
            if (!built || page == null) return;
            head.text = page.Title;
            note.text = page.Note ?? "";
            bool main = string.IsNullOrEmpty(page.Title);
            title.gameObject.SetActive(main); sub.gameObject.SetActive(main); band.SetActive(main);      // 다른 쪽 제목(먹색)이 먹 띠에 묻히지 않게
            for (int i = 0; i < MaxRows; i++)
            {
                bool on = i < Count;
                rows[i].Rt.gameObject.SetActive(on);
                if (!on) continue;
                bool sel = i == Selected;
                rows[i].Label.text = page.Items[i].Text();
                rows[i].Plate.color = sel ? UiKit.Paper : new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.82f);
                rows[i].Bar.enabled = sel;
                rows[i].Label.color = sel ? UiKit.Ink : UiKit.Paper;
                rows[i].Rt.anchoredPosition = new Vector2(0f, (main ? -60f : 120f) - i * 66f);
            }
        }

        void Build()
        {
            if (built) return;
            built = true;
            UiKit.Canvas(gameObject, 10);
            var bg = UiKit.Panel("종이", transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, UiKit.Paper);
            UiKit.Stretch(bg.rectTransform);
            var bandRt = UiKit.Node("먹 띠", transform);
            band = bandRt.gameObject;
            var raw = band.AddComponent<RawImage>();
            raw.texture = UiKit.BrushBand(512, 64, 11, 0.08f);
            raw.raycastTarget = false;
            UiKit.Place(bandRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 250f), new Vector2(1500f, 200f));
            title = UiKit.Text("제목", transform, Fonts != null ? Fonts.Br : null, 110f, UiKit.Paper, TextAlignmentOptions.Center);
            UiKit.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 258f), new Vector2(1500f, 150f));
            title.text = "행인1의 메인이벤트";
            sub = UiKit.Text("부제", transform, Fonts != null ? Fonts.B : null, 30f, UiKit.Ink, TextAlignmentOptions.Center);
            UiKit.Place(sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 110f), new Vector2(1500f, 44f));
            sub.text = "시우 루트 1막 「출석부」 데모";
            head = UiKit.Text("쪽 제목", transform, Fonts != null ? Fonts.Br : null, 54f, UiKit.Ink, TextAlignmentOptions.Center);
            UiKit.Place(head.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 250f), new Vector2(1500f, 80f));
            note = UiKit.Text("설명", transform, Fonts != null ? Fonts.B : null, 24f, new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.75f), TextAlignmentOptions.Center);
            UiKit.Place(note.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 190f), new Vector2(1500f, 40f));
            var hint = UiKit.Text("조작", transform, Fonts != null ? Fonts.B : null, 20f, new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.6f), TextAlignmentOptions.Center);
            UiKit.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(1500f, 34f));
            hint.text = "↑↓ 고르기    × (A) · Enter 선택    ○ (B) · Backspace 돌아가기";
            rows = new Row[MaxRows];
            for (int i = 0; i < MaxRows; i++)
            {
                var c5 = new Vector2(0.5f, 0.5f);
                var plate = UiKit.Panel("항목 " + (i + 1), transform, c5, c5, Vector2.zero, new Vector2(760f, 56f), UiKit.Ink);
                var bar = UiKit.Panel("고른 표시", plate.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(8f, 56f), UiKit.Accent);
                var label = UiKit.Text("글", plate.transform, Fonts != null ? Fonts.B : null, 26f, UiKit.Paper, TextAlignmentOptions.Center);
                UiKit.Stretch(label.rectTransform);
                rows[i] = new Row { Rt = (RectTransform)plate.transform, Plate = plate, Bar = bar, Label = label };
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { QuitHook = null; }
    }
}
