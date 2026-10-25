// 행인1의 메인이벤트 — 오픈월드 진행(docs/13 3장 핵심 루프 · 5장 이벤트 · 12장 데모): Zone1 루트 'World' 에 붙는다(WorldSetup).
// 타이틀 '새 게임' → StoryBoot.Mode.World → StoryRunner.Start 가 Begin 을 부른다. 걷는 동안 실제 1초 = 게임 3초, 활동·잠은 정한 시간을 한 번에 쓴다.
// 감독 평가 시점: 장소 진입(enter) · 지점(point) · 시각(time — 놓칠 수 없는 핵심 카드) · 활동 중/끝(during/after) · 잠·기상 · 말일 · 불러오기.
// 카드 본편 = M3 장면 체인(StoryRunner.RunChain) 또는 스토리보드 카드(원고 전) + 선택지 + 시비(M2 인카운터).
// 이 파일 = 시작·끝·평가·카드. 활동·잠·학교·체육관·시비 = WorldRunner.Acts.cs, 화면·휴대폰·저장·문 = WorldRunner.Ui.cs.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Haengin
{
    [DefaultExecutionOrder(-40), DisallowMultipleComponent]
    public sealed partial class WorldRunner : MonoBehaviour
    {
        public WorldDef Def;
        public StoryRunner Story;
        public SceneLoader Loader;
        public InputActionAsset Actions;
        public UiFonts Fonts;
        public WorldHud Hud;
        public PhoneUi Phone;
        public PlannerUi PlannerView;
        public ChapterUi ChapterView;
        public JobGame Job;

        public static WorldRunner Instance { get; private set; }
        /// 빌드 스모크·녹화: Begin 직전에 봇 설정을 넣는다(타이틀 '새 게임'으로 들어올 때)
        public static System.Action<WorldRunner> BotPreset;
        public static bool On => Instance != null && Instance.Active;

        public bool Active { get; private set; }
        public WorldModel Model { get; private set; }
        public EventDirector Director { get; private set; }
        public WeekPlanner Planner { get; private set; }
        /// 지금 하는 일(빈칸 = 자유 이동)
        public string Busy => busy.Count > 0 ? busy.Peek() : "";
        public bool Free => Active && busy.Count == 0 && (Loader == null || !Loader.Busy) && !GameState.Paused && (Phone == null || !Phone.Shown) && (GymManager.Instance == null || !GymManager.Instance.Busy);

        [Header("봇(시험·스모크·녹화)")]
        /// 0 = 사람. 0~1 = 미니게임·드릴 봇 실력(결정적)
        public float BotSkill;
        /// 봇이 맡기기가 되면 맡김
        public bool BotDelegate = true;
        /// 봇 배달: 자전거 코스를 실제로 탐(false 면 건너뜀 — 등급 B)
        public bool BotRide;
        /// 봇이 카드·안내를 넘기는 시간(초)
        public float BotRead = 0.15f;
        /// 봇 선택지(카드 id·보기 → 번호). null = 0번
        public Func<CardDef, int> BotChoice;

        public readonly List<string> Trace = new List<string>();
        public event Action<CardDef> CardStarted, CardEnded;
        public event Action<ActivityDef, ActivityResult> ActivityDone;
        public event Action DemoEnded;
        public CardDef CurrentCard { get; private set; }
        public ActivityDef CurrentActivity { get; private set; }
        public int CardsRun { get; private set; }
        public int ActivitiesRun { get; private set; }
        public int Sleeps { get; private set; }

        readonly Stack<string> busy = new Stack<string>();
        string lastPlace = "", lastPoint = "";
        float evalLeft, lightLeft;
        int dayEndPending = -1;
        string guideLabel = "";

        void OnEnable() { Instance = this; GameClock.DayChanged += OnDayChanged; }
        void OnDisable() { if (Instance == this) Instance = null; GameClock.DayChanged -= OnDayChanged; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; BotPreset = null; }

        void Awake()
        {
            if (Story == null) Story = FindAnyObjectByType<StoryRunner>(FindObjectsInactive.Include);
            if (Loader == null && Story != null) Loader = Story.Loader;
            if (Loader != null) Loader.Placed += OnPlaced;
        }

        void OnDayChanged() { if (Active && busy.Count == 0) dayEndPending = GameClock.KeyPlus(GameClock.DayKey, -1); }

        // ───────────────────────── 시작·끝
        /// 새 게임(save = null) 또는 이어하기(v2 오픈월드 저장 · v1 M3 저장은 카드로 이주)
        public void Begin(SaveData save)
        {
            if (Def == null) { Debug.LogError("[World] WorldDef 가 없습니다 — WorldSetup.Build"); return; }
            if (Active) Stop();
            BotPreset?.Invoke(this);
            if (Story == null) Story = FindAnyObjectByType<StoryRunner>(FindObjectsInactive.Include);
            if (Loader == null) Loader = Story.Loader;
            Story.Bot = BotSkill > 0f ? StoryRunner.BotMode.Fast : StoryRunner.BotMode.None;
            Story.AutoSave = false;
            Story.BeginWorld(save);
            WorldSave ws;
            string migrated = null;
            if (save != null && save.World != null && save.World.Active) ws = save.World.Clone();
            else if (save != null && save.Version == 1) { ws = WorldModel.MigrateV1(Def, save, out migrated); }
            else ws = WorldModel.NewSave(Def);
            Model = new WorldModel(Def, ws, Story.Ledger, Story.Flags, GymState.Current, Story.Names, Story.Def != null ? Story.Def.Names : null) { DemoMode = true };
            Model.Toast += s => { if (Hud != null) Hud.Toast(s); };
            Model.NameGot += n => { if (Story.NameUi != null) Story.NameUi.Register(n); };
            Director = new EventDirector(Model);
            Planner = new WeekPlanner(Model, Director) { Drills = Def.Drills != null ? Def.Drills.Drills : new DrillDef[0] };
            Story.HouseholdHook = v => Model.OnLedgerLine(v);
            GameUi.BookHook = () => OpenPhone(0);
            GameUi.BookLabel = "휴대폰(Tab)";
            GameUi.TitleHook = SaveAndTitle;
            GymManager.Gate = GymGate;
            GymManager.Finished += OnDrillFinished;
            StageDoor.Gate = DoorGate;
            Encounter.Suppress = true;
            SetYacha(false);
            Trace.Clear();
            busy.Clear();
            Active = true;
            lastPlace = ""; lastPoint = ""; dayEndPending = -1;
            SetupZone();
            if (Phone != null) Phone.Content = PhoneText;
            Debug.Log($"[World] 시작: {(save == null ? "새 게임" : migrated != null ? "M3 저장 이주 → " + migrated : "이어하기 " + ws.Card)} · 카드 {Def.Cards.Length} · 활동 {Def.Activities.Length} · 봇 {BotSkill:F2}");
            StartCoroutine(Run("시작", Boot(save, migrated)));
        }

        public void Stop()
        {
            if (!Active) return;
            Active = false;
            StopAllCoroutines();
            busy.Clear();
            GymManager.Gate = null;
            GymManager.Finished -= OnDrillFinished;
            GameUi.BookLabel = "도감";
            StageDoor.Gate = null;
            Encounter.Suppress = false;
            SetYacha(true);
            if (Story != null) { Story.HouseholdHook = null; Story.Stop(); }
            Phone?.Close(); Job?.Hide(); ChapterView?.Hide();
            if (Hud != null) { Hud.Shown = false; Hud.ClearToasts(); }
            GameState.Modal = false;
            CutCams.ReleaseAll();
            Debug.Log("[World] 멈춤");
        }

        void SetYacha(bool on)
        {
            foreach (var y in FindObjectsByType<Yacha>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (y != null) y.gameObject.SetActive(on);
        }

        /// busy 표시를 걸고 코루틴을 돈다(겹쳐 부르면 쌓임)
        IEnumerator Run(string label, IEnumerator body)
        {
            busy.Push(label);
            try { yield return body; }
            finally { if (busy.Count > 0) busy.Pop(); }
        }

        IEnumerator Boot(SaveData save, string migrated)
        {
            var b = Def.Balance;
            if (save == null)
            {
                GameClock.SetDate(b.StartYear, b.StartDate / 100, b.StartDate % 100, b.StartTime);
                Model.L.SetWallet(b.StartWallet);
                yield return ChapterCard(Def.Chapter("1"), true);
                yield return Tutorial();
                var c = Director.Pick("start");
                if (c != null) yield return RunCard(c);
            }
            else
            {
                GameClock.SetDate(Model.S.Year, save.Month, save.Day, save.Minutes);
                var ws = Model.S;
                if (Loader != null) yield return Loader.Go(ws.Place, string.IsNullOrEmpty(ws.Spawn) ? "Spawn" : ws.Spawn, ws.HasPos, ws.Pos, ws.Yaw);
                if (migrated != null && Hud != null) Hud.Toast($"M3 저장을 오픈월드로 옮김 — 카드 {migrated}부터");
                var c = string.IsNullOrEmpty(ws.Card) ? null : Def.Card(ws.Card);
                if (c != null && !Model.CardDone(c.Id)) yield return RunCard(c);
            }
            if (Hud != null) Hud.Shown = true;
        }

        IEnumerator Tutorial()
        {
            string[][] cards =
            {
                new[] { "하루가 흐른다", "걸어 다니는 동안 시간이 흐른다(실제 1초 = 3분). 알바·학교·잠은 정한 시간을 한 번에 쓴다.", "새벽 05:00 · 아침 08:00 · 오후 16:00 · 저녁 18:00 · 밤 22:00. 밤 10시부터 아침 6시까지는 일하지 않는다(17세).", "말일에 집에 61만 원(엄마 통장·하늘 방과후·휴대폰)이 나간다. 모자라면 지갑에서, 그래도 모자라면 결손." },
                new[] { "알바 = 돈, 체육관 = 실력, 싸움 = 이름", "국밥집·하루편의점·현장 안의 동그란 표시에서 × · E — 일을 시작한다. 열 번 하거나 S 를 받으면 '맡기기'가 열린다.", "피로가 70을 넘으면 얻는 게 줄고, 85면 체육관이 막고, 95면 쓰러진다. 집 이불에서 자면 풀린다.", "휴대폰(Tab · Select) — 문자·오늘 지도·일정·가계부·목표·몸·이름." },
                new[] { "이벤트 카드는 그때그때", "장소·시간·조건이 맞으면 카드가 뜬다. 주황 기둥 = 놓칠 수 없는 이야기, 휴대폰 문자로 미리 온다.", "이벤트 없는 날은 일요일 밤 '주간 계획'으로 맡겨 넘길 수 있다(3월 17일부터).", "데모: 3월 3일 ~ 5월 8일. 하늘이 생일(5월 1일)까지 버티는 것이 목표." },
            };
            var board = Story.Board;
            if (board == null) yield break;
            for (int i = 0; i < cards.Length; i++)
            {
                var c = cards[i];
                board.ShowCard($"하루 루프 안내 {i + 1} / {cards.Length}", c[0], "안내", false, new[] { c[1], c[2], c[3] }, null, "× · Enter · 마우스 왼쪽   다음");
                yield return WaitPress();
                board.Hide();
            }
        }

        // ───────────────────────── 한 프레임
        void Update()
        {
            if (!Active) return;
            UpdateHud();
            lightLeft -= UiKit.RealDt;
            if (lightLeft <= 0f) { lightLeft = 0.25f; ApplyLight(); }
            if (!Free) return;
            // 걷는 동안 시계(일시정지·히트스톱이면 0)
            GameClock.Advance(TimeFx.Dt * Def.Balance.ClockRate / 60.0);
            PhoneKey();
            evalLeft -= UiKit.RealDt;
            if (evalLeft <= 0f) { evalLeft = 0.2f; Evaluate(); }
        }

        void Evaluate()
        {
            if (!Free) return;
            var b = Def.Balance;
            Model.BeginDayIfNeeded();
            // 자정을 넘김 — 어제의 '잠 전' 카드
            if (dayEndPending > 0)
            {
                int d = dayEndPending; dayEndPending = -1;
                var dc = Director.Pick("sleep", d, 24 * 60 - 1);
                if (dc != null) { StartCoroutine(Run("카드", RunCard(dc))); return; }
            }
            // 밤 한계(02:00) — 집으로 데려가 재움
            if (Model.Now >= b.NightLimit && Model.Now < b.Wake) { StartCoroutine(Run("잠", Sleep(true))); return; }
            // 미리 오는 문자
            foreach (var n in Director.Notices()) { Model.Phone(n.NoticeFrom, n.NoticeText); Model.AddCount("notice." + n.Id, 1); }
            // 놓칠 수 없는 시각 카드
            var tc = Director.Pick("time");
            if (tc != null) { StartCoroutine(Run("카드", RunCard(tc, true))); return; }
            // 창이 지나간 카드
            foreach (var oc in Director.Overdue())
            {
                if (oc.Miss == "never") { Debug.Log($"[World] 따라잡기: {oc}"); StartCoroutine(Run("카드", RunCard(oc, true))); return; }
                if (oc.Miss == "summary") { SummaryCard(oc); continue; }
                Director.Done(oc, 3);
            }
            // 장소 진입
            string place = Loader != null ? Loader.Place : "Zone1";
            if (place != lastPlace)
            {
                lastPlace = place;
                var ec = Director.Pick("enter:" + place);
                if (ec != null) { StartCoroutine(Run("카드", RunCard(ec))); return; }
            }
            // 지점(Zone1)
            if (place == SceneLoader.Zone1 && Loader != null && Loader.Motor != null)
            {
                var p = Loader.Motor.Position;
                string inside = "";
                foreach (var pt in b.Points)
                {
                    var d = new Vector2(pt.Pos.x - p.x, pt.Pos.z - p.z);
                    if (d.magnitude <= pt.Radius && Mathf.Abs(pt.Pos.y - p.y) < 4f) { inside = pt.Id; break; }
                }
                if (inside != lastPoint)
                {
                    lastPoint = inside;
                    if (inside.Length > 0)
                    {
                        var pc = Director.Pick("point:" + inside);
                        if (pc != null) { StartCoroutine(Run("카드", RunCard(pc))); return; }
                    }
                }
                StreetRepeat();
            }
            UpdateGuide();
            if (Model.S.DemoState == 0 && Model.Today > Def.Chapter("1").DemoTo) StartCoroutine(Run("데모 끝", DemoCheck(true)));
        }

        /// 결과만(요약 카드 — 계절 카드를 놓쳤을 때)
        void SummaryCard(CardDef c)
        {
            Director.ApplyAfter(c, c.Choice.Length > 0 ? c.Choice[0] : null);
            Director.Done(c, 2);
            Hud?.Toast($"지난 일 — 「{c.Title}」");
            Trace.Add($"요약 {c.Id}");
            StartCoroutine(Run("요약", HostCommands()));
        }

        // ───────────────────────── 카드
        public IEnumerator RunCard(CardDef c, bool travel = false)
        {
            if (c == null) yield break;
            busy.Push("카드 " + c.Id);
            try
            {
                CurrentCard = c; Director.Running = c; CardsRun++;
                Trace.Add($"{GameClock.DateText} {GameClock.TimeText} 카드 {c.Id}");
                Debug.Log($"[World] 카드 {c.Id} 「{c.Title}」 시작: {GameClock.Label} · {Loader?.Place} · {c.Kind}");
                CardStarted?.Invoke(c);
                if (c.Kind == CardKind.Core || c.Kind == CardKind.System) AutoSave(c.Id);
                if (c.Kind == CardKind.Repeat)
                {
                    var lines = Director.BoardOf(c);
                    if (lines.Length > 0) Hud?.Toast(lines[0]);
                    Director.ApplyAfter(c);
                    Director.Done(c, 1);
                }
                else
                {
                    if (Hud != null) Hud.Shown = false;
                    if (travel && !c.HasScenes && !string.IsNullOrEmpty(c.Place) && c.Place != SceneLoader.Zone1 && Loader != null && Loader.Place != c.Place)
                        yield return Loader.Go(c.Place, "Spawn", false, Vector3.zero, 0f);
                    if (c.HasScenes) yield return Story.RunChain(c.Scenes, c.NoSceneEffects);
                    var board = Director.BoardOf(c);
                    if (!c.HasScenes && board.Length > 0) yield return ShowBoard(c);
                    CardChoice choice = null;
                    if (c.Choice.Length > 0)
                    {
                        int pick = 0;
                        yield return Ask(System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(c.Choice, x => x.Label)), v => pick = v, BotChoice != null ? BotChoice(c) : 0);
                        choice = c.Choice[Mathf.Clamp(pick, 0, c.Choice.Length - 1)];
                        Trace.Add($"선택 {c.Id}={pick}");
                    }
                    if (c.Fight == "street") yield return StreetFight(c);
                    Director.ApplyAfter(c, choice);
                    Director.Done(c, 1);
                }
                Director.Running = null;
                CurrentCard = null;
                Debug.Log($"[World] 카드 {c.Id} 끝: {Model.Status}");
                CardEnded?.Invoke(c);
                yield return HostCommands();
            }
            finally { if (busy.Count > 0) busy.Pop(); if (Hud != null && busy.Count == 0) Hud.Shown = true; }
        }

        IEnumerator ShowBoard(CardDef c)
        {
            var board = Story.Board;
            if (board == null) yield break;
            string kind = c.Kind switch { CardKind.Core => "핵심", CardKind.Season => "계절", CardKind.Cond => "조건", CardKind.Side => "동네 사연", CardKind.Msg => "문자", CardKind.System => "시스템", _ => "반복" };
            string where = PlaceName(c.Place);
            board.ShowCard($"{Def.Chapter(c.Chapter)?.Label ?? ""}   ·   {GameClock.DateText} {GameClock.TimeText}{(where.Length > 0 ? "   ·   " + where : "")}", Director.TitleOf(c),
                kind + (c.HasScenes ? "" : " · 스토리보드"), c.Kind == CardKind.Core, Director.BoardOf(c), Director.QuotesOf(c), "× · Enter · 마우스 왼쪽   다음");
            yield return WaitPress();
            board.Hide();
        }

        /// 효과 중 장면·화면이 할 일
        IEnumerator HostCommands()
        {
            int guard = 0;
            while (Model.Host.Count > 0 && guard++ < 20)
            {
                string cmd = Model.Host[0];
                Model.Host.RemoveAt(0);
                int c = cmd.IndexOf(':');
                string verb = c > 0 ? cmd.Substring(0, c) : cmd, arg = c > 0 ? cmd.Substring(c + 1) : "";
                switch (verb)
                {
                    case "chain": { var nc = Def.Card(arg); if (nc != null && !Model.CardDone(nc.Id)) yield return RunCard(nc); break; }
                    case "activity": if (arg == "school") yield return School(true); break;
                    case "plan": if (arg == "open") yield return PlanWeek(); break;
                    case "gymtrial": yield return GymTrial(); break;
                    case "demo": if (arg == "check") yield return DemoCheck(false); break;
                    default: Debug.Log($"[World] 처리 안 한 명령: {cmd}"); break;
                }
            }
        }

        // ───────────────────────── 입력 도우미
        IEnumerator WaitPress()
        {
            var talkIn = Story != null ? Story.TalkIn : null;
            bool lockWas = GameState.InputLocked;
            GameState.InputLocked = true;    // 카드가 떠 있는 동안 걷기·상호작용 안내 막음(Modal 은 TalkInput 다음 입력까지 막아서 쓰지 않음)
            InputMaps.Use(Actions, InputMaps.Talk);
            GameUi.HintOverride = "다음 × · Enter · 마우스 왼쪽";
            yield return null;
            float t = 0f;
            while (true)
            {
                t += UiKit.RealDt;
                if (BotSkill > 0f) { if (t >= BotRead) break; }
                else if (talkIn != null && talkIn.Next) break;
                yield return null;
            }
            InputMaps.Use(Actions, InputMaps.Explore);
            GameUi.HintOverride = null;
            GameState.InputLocked = lockWas;
        }

        IEnumerator Ask(string[] options, Action<int> done, int botPick = 0)
        {
            var choice = Story != null && Story.Talk != null ? Story.Talk.Choice : null;
            if (choice == null || BotSkill > 0f) { done(Mathf.Clamp(botPick, 0, options.Length - 1)); yield break; }
            InputMaps.Use(Actions, InputMaps.Talk);
            int pick = 0;
            yield return choice.Ask(options, v => pick = v);
            InputMaps.Use(Actions, InputMaps.Explore);
            done(pick);
        }

        IEnumerator ChapterCard(ChapterDef ch, bool demo)
        {
            if (ChapterView == null || ch == null) yield break;
            var sb = new System.Text.StringBuilder();
            sb.Append("목표 — ").Append(demo && ch.HasDemo ? ch.DemoGoal : ch.Goal).Append('\n');
            foreach (var g in demo && ch.HasDemo ? ch.DemoGoals : ch.Goals) sb.Append("□ ").Append(g.Label).Append('\n');
            if (demo && ch.HasDemo) { sb.Append("\n1장 전체 목표 — ").Append(ch.Goal).Append('\n'); foreach (var g in ch.Goals) sb.Append("· ").Append(g.Label).Append('\n'); }
            yield return ChapterView.Show("장", $"{ch.Period} · {ch.Age} · {ch.Ladder}", ch.Label, demo && ch.HasDemo ? ch.DemoTitle : ch.Goal, sb.ToString(), "× · Enter   시작", BotSkill > 0f ? BotRead * 2f : 0f);
        }

        public static string PlaceName(string key) => key switch
        {
            "Gukbap" => "국밥집", "Home" => "시우네", "Conv" => "하루편의점", "School" => "혜성고", "Site" => "공사 현장", "Gym" => "혜화 MMA", "Zone1" => "후문 골목", _ => key ?? "",
        };
    }
}
