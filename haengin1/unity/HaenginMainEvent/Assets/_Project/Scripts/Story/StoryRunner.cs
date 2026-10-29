// 행인1의 메인이벤트 — 이야기 진행(docs/09_M3_버티컬슬라이스_설계.md 2-1 '하루 진행'): 장면 목록을 순서대로 실행한다(데모 = 고정 일정).
// 장면 하나 = 시각 점프 → 자동 저장(장면 시작 상태) → 장소 이동(먹 닦기, 덮인 동안 시간대 조명) → 날짜 판 →
//   몸통(회차 카드 3초 / 이동 목표 / 컷신(타임라인) / 대사 / 아직 없는 장면은 '자리 장면' 요약 자막) → 끝 명령(가계부·도감·플래그) → 다음.
// 이야기 중에는 Zone1 의 M2 무대(인카운터 Y4·야차 Y1)를 끈다(7장 결정 21 — M2 는 타이틀 '연습장'에서만).
// Zone1 장면 루트 'Story' 에 붙는다(StorySetup). Zone1 을 그냥 열면(M1·M2 테스트·연습장) 아무것도 하지 않는다 — 타이틀이 StoryBoot.Request 를 넣었을 때만 시작.
// 봇(테스트·녹화): Fast = 이동 목표로 순간 이동·대사와 컷신은 처음에 건너뜀, Read = 대사를 글자 수 ÷ 8 + 0.8초에 넘기고 목표까지 걸어감.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Haengin
{
    [DefaultExecutionOrder(-45), DisallowMultipleComponent]
    public sealed class StoryRunner : MonoBehaviour
    {
        public StoryDef Def;
        public DlgBook Dialogue;
        public CutLib Cuts;
        public InputActionAsset Actions;
        public UiFonts Fonts;
        public SceneLoader Loader;
        public StoryHud Hud;
        public DialogueRunner Talk;
        public TalkInput TalkIn;
        public Cutscene Cut;
        public EpisodeCard Card;
        /// 스토리보드 카드(자리 장면·조작 장면 소개·1막 소개 — 2026-10-25)
        public StoryBoardUi Board;
        /// 양갈래 땋기 1화 최소판(대사 이벤트 "braidgame:N")
        public BraidGame Braid;
        public LedgerUi LedgerUi;
        public NameBookUi NameUi;
        public EndureFx Endure;
        public DayLight Light;
        /// Zone1 의 M2 무대 루트('Stages')
        public GameObject M2Stages;
        /// 장면 배역(QA 2차 B): 무대 밖(Zone1)에 숨겨 둔 배우들의 뿌리 — 대사 이벤트 cast: 가 켬(WorldSetup 이 World 아래에 만듦)
        public Transform SceneCast;

        public static StoryRunner Instance { get; private set; }
        /// 시험(M1 경로 걷기 등): 타이틀 요청이 있어도 시작하지 않음
        public static bool Suppress;
        public static bool Active => Instance != null && Instance.running;

        public enum BotMode { None, Fast, Read }
        public BotMode Bot;
        /// 봇이 고를 선택지(null = 0번)
        public Func<DlgLine, int> BotAnswer;
        public bool AutoSave = true;
        /// 장면 하나만 돌고 멈춤(테스트 — 건너뛰기 같음 D05·이어하기 D12)
        public bool OneScene;

        public StoryFlags Flags = new StoryFlags();
        public Ledger Ledger = new Ledger();
        public NameBook Names = new NameBook();

        public SceneDef Scene { get; private set; }
        public int Index { get; private set; } = -1;
        public bool TestList { get; private set; }
        public bool Finished { get; private set; }
        public bool InScene { get; private set; }
        public SceneDef[] List => Def != null ? Def.List(TestList) : new SceneDef[0];
        /// "id|날짜|시각|장소|형식" — D01(두 번 돌려 같은지)
        public readonly List<string> Trace = new List<string>();
        /// 지금 장면 시작 상태(자동 저장에 쓴 것)
        public SaveData SceneStart { get; private set; }
        public event Action<SceneDef> SceneStarted, SceneEnded;
        public event Action Ended;

        bool running, skipScene;
        Coroutine co;
        /// 오픈월드(docs/13): 장면 목록을 돌지 않고, 감독(WorldRunner)이 카드 본편 체인을 하나씩 넘긴다(RunChain)
        public bool WorldMode { get; private set; }
        /// 오픈월드: 장면 효과 "ledger:+N:…"(엄마 통장·고지서 밑) → 생활비로 셈
        public Action<int> HouseholdHook;
        bool chainNoEffects;
        DayPart pendingLight;

        void OnEnable() { Instance = this; if (SceneCast != null && !CutCtx.CastRoots.Contains(SceneCast)) CutCtx.CastRoots.Add(SceneCast); }
        void OnDisable() { if (Instance == this) Instance = null; if (SceneCast != null) CutCtx.CastRoots.Remove(SceneCast); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; Suppress = false; }

        void Awake()
        {
            if (Loader == null) Loader = GetComponent<SceneLoader>();
            if (Talk == null) Talk = GetComponent<DialogueRunner>();
            if (TalkIn == null) TalkIn = GetComponent<TalkInput>();
            if (Cut == null) Cut = GetComponent<Cutscene>();
            if (Light == null) Light = GetComponent<DayLight>();
            if (Hud == null) Hud = GetComponentInChildren<StoryHud>(true);
            if (Card == null) Card = GetComponentInChildren<EpisodeCard>(true);
            if (Board == null) Board = GetComponentInChildren<StoryBoardUi>(true);
            if (Braid == null) Braid = GetComponentInChildren<BraidGame>(true);
            if (LedgerUi == null) LedgerUi = GetComponentInChildren<LedgerUi>(true);
            if (NameUi == null) NameUi = GetComponentInChildren<NameBookUi>(true);
            if (Endure == null) Endure = GetComponentInChildren<EndureFx>(true);
            if (Talk != null)
            {
                Talk.ApplyCo = ApplyCo;
                Talk.ApplyNow = ApplyNow;
                Talk.Flags = () => Flags;
                if (Talk.Endure == null) Talk.Endure = Endure;
                if (Talk.LedgerUi == null) Talk.LedgerUi = LedgerUi;
                if (Talk.Hud == null) Talk.Hud = Hud;
            }
            if (Loader != null) { Loader.Covered += OnCovered; Loader.Placed += OnPlaced; }
            if (Talk != null) Talk.Shown += OnLine;
        }

        IEnumerator Start()
        {
            yield return null;
            var r = StoryBoot.Take();
            if (r == null || Suppress) yield break;
            switch (r.Mode)
            {
                case StoryBoot.Mode.New: Begin(false, null, null); break;
                case StoryBoot.Mode.World:
                    if (WorldRunner.Instance != null) WorldRunner.Instance.Begin(null);
                    else Debug.LogError("[World] Zone1 에 WorldRunner 가 없습니다 — WorldSetup.Build");
                    break;
                case StoryBoot.Mode.Continue:
                    if (r.Save == null) break;
                    if (WorldRunner.Instance != null && (r.Save.Version == 1 || (r.Save.World != null && r.Save.World.Active))) WorldRunner.Instance.Begin(r.Save);
                    else Begin(r.Save.Test, r.Save.SceneId, r.Save);
                    break;
                case StoryBoot.Mode.Select: Begin(false, r.SceneId, null); break;
                case StoryBoot.Mode.Gym:
                    Debug.Log("[M3] 연습장: 체육관(혜화 MMA)으로 — 이야기 없이");
                    if (Loader != null) StartCoroutine(Loader.Go("Gym", "Spawn", false, Vector3.zero, 0f, true));
                    break;
                default: Debug.Log("[M3] 연습장: 이야기 없이 Zone1(M2 무대 켬)"); break;
            }
        }

        // ───────────────────────── 시작·끝
        public void Begin(bool test, string startId, SaveData save)
        {
            if (running) Stop();
            if (Def == null) { Debug.LogError("[M3] StoryDef 가 없습니다"); return; }
            TestList = test;
            var s = save != null ? save.Clone() : null;
            Flags = s != null ? s.Flags : new StoryFlags();
            Ledger = s != null ? s.Ledger : new Ledger();
            Names = s != null ? s.Names : new NameBook();
            GymState.Current = s != null && s.Gym != null ? s.Gym : new GymSave();
            int start = string.IsNullOrEmpty(startId) ? 0 : Def.IndexOf(List, startId);
            if (start < 0) { Debug.LogWarning($"[M3] 장면 {startId} 없음 → 처음부터"); start = 0; }
            running = true; Finished = false;
            Trace.Clear();
            SetM2(false);
            CutCtx.Sub = Talk != null ? Talk.Sub : null;
            CutCtx.Inner = Talk != null ? Talk.Inner : null;
            CutCtx.Hud = Hud;
            CutCtx.ApplyNow = ApplyNow;
            CutCtx.Player = Loader != null && Loader.Motor != null ? Loader.Motor.transform : null;
            // M1 체크포인트 길잡이를 숨기고 이야기 목표만(빈 목록으로 시작)
            var guide = FindAnyObjectByType<RouteGuide>(FindObjectsInactive.Include);
            if (guide != null) guide.SetTargets(new TargetDef[0]);
            GameUi.BookHook = OpenBook;
            GameUi.TitleHook = SaveAndTitle;
            if (Talk != null) Talk.Bot = Bot != BotMode.None ? (l => BotAnswer != null ? BotAnswer(l) : 0) : null;
            Debug.Log($"[M3] 이야기 시작: {(test ? "시험 장면" : "데모")} {List.Length}개 중 {start + 1}번째({List[start].Id}){(save != null ? " · 이어하기" : "")}{(Bot != BotMode.None ? " · 봇 " + Bot : "")}");
            co = StartCoroutine(Run(start, !test && start == 0 && save == null));
        }

        /// 오픈월드 시작(WorldRunner 가 부름): 가계부·도감·플래그·몸을 저장에서 받고, 장면 목록은 돌지 않는다
        public void BeginWorld(SaveData save)
        {
            if (running || WorldMode) Stop();
            if (Def == null) { Debug.LogError("[M3] StoryDef 가 없습니다"); return; }
            TestList = false;
            var s = save != null ? save.Clone() : null;
            Flags = s != null && s.Flags != null ? s.Flags : new StoryFlags();
            Ledger = s != null && s.Ledger != null ? s.Ledger : new Ledger();
            Names = s != null && s.Names != null ? s.Names : new NameBook();
            GymState.Current = s != null && s.Gym != null ? s.Gym : new GymSave();
            WorldMode = true; Finished = false; running = false;
            Trace.Clear();
            SetM2(false);      // M2 무대(주차장 인카운터·야차)는 오픈월드가 그때그때 켬(시비 카드·시비 반복 — 성능 W16)
            CutCtx.Sub = Talk != null ? Talk.Sub : null;
            CutCtx.Inner = Talk != null ? Talk.Inner : null;
            CutCtx.Hud = Hud;
            CutCtx.ApplyNow = ApplyNow;
            CutCtx.Player = Loader != null && Loader.Motor != null ? Loader.Motor.transform : null;
            var guide = FindAnyObjectByType<RouteGuide>(FindObjectsInactive.Include);
            if (guide != null) guide.SetTargets(new TargetDef[0]);
            if (Talk != null) Talk.Bot = Bot != BotMode.None ? (l => BotAnswer != null ? BotAnswer(l) : 0) : null;
            Debug.Log($"[World] 이야기 뼈대 오픈월드 모드{(save != null ? " · 이어하기" : "")}{(Bot != BotMode.None ? " · 봇 " + Bot : "")}");
        }

        /// 오픈월드 카드 본편: 장면 id 체인을 순서대로(시각은 앞으로만 — 같은 날 그 시각까지, 회차 카드는 시계를 건드리지 않음). noEffects = 장면 끝 명령 빼기(몽타주 5-1 등)
        public IEnumerator RunChain(IList<string> ids, bool noEffects = false)
        {
            if (Def == null || ids == null) yield break;
            running = true; skipScene = false;
            chainNoEffects = noEffects;
            Talk?.ClearSkip();
            if (Talk != null) Talk.Bot = Bot != BotMode.None ? (l => BotAnswer != null ? BotAnswer(l) : 0) : null;
            foreach (var id in ids)
            {
                var sc = Def.Find(id);
                if (sc == null) { Debug.LogWarning($"[World] 장면 {id} 없음 — 건너뜀"); continue; }
                Index = Array.IndexOf(Def.Scenes, sc);
                yield return RunScene(sc);
                skipScene = false;
            }
            chainNoEffects = false;
            running = false; InScene = false;
            GameClock.Running = false;
            if (Hud != null) Hud.KeepChip = false;
            InputMaps.Use(Actions, InputMaps.Explore);
            GameUi.HintOverride = null;
            if (BikeCourse.Current != null && BikeCourse.Current.Riding) BikeCourse.Current.Finish();
        }

        /// 이야기를 멈추고 Zone1 탐색으로 되돌린다(테스트 정리·타이틀로)
        public void Stop()
        {
            if (co != null) StopCoroutine(co);
            co = null;
            if (Cut != null) Cut.Abort();
            CutCtx.End();
            Talk?.HideAll();
            Hud?.HideAll();
            Card?.Hide();
            Endure?.Clear();
            LedgerUi?.HideAll();
            NameUi?.ClearFx(); NameUi?.HideGrid();
            InkMode.Set(0f);
            CutCtx.Sub = null; CutCtx.Inner = null; CutCtx.Hud = null; CutCtx.ApplyNow = null; CutCtx.Player = null;
            GameClock.Running = false;
            var g = FindAnyObjectByType<RouteGuide>(FindObjectsInactive.Include);
            if (g != null) g.RestoreDefault();
            if (Loader != null) StartCoroutine(Loader.Reset());
            InputMaps.Use(Actions, InputMaps.Explore);
            GameUi.HintOverride = null;
            Board?.Hide();
            if (BikeCourse.Current != null && BikeCourse.Current.Riding) BikeCourse.Current.Finish();
            var of = Loader != null && Loader.Motor != null ? Loader.Motor.GetComponent<Outfit>() : null;
            if (of != null) of.Set(Outfit.Uniform);
            GameUi.BookHook = GameUi.TitleHook = null;
            GameUi.SubOpen = null; GameUi.SubClose = null;
            SetM2(true);
            RestoreCast();
            running = false; InScene = false; WorldMode = false; chainNoEffects = false;
        }

        void SetM2(bool on)
        {
            if (M2Stages != null && M2Stages.activeSelf != on) M2Stages.SetActive(on);
        }

        IEnumerator Run(int start, bool intro = false)
        {
            if (intro && Board != null && Def != null && Def.Intro != null && Def.Intro.Length > 0)
            {
                // 새 게임 첫 화면: 1막 소개(누가·어디서·무엇을) — 눌러야 시작
                InputMaps.Use(Actions, InputMaps.Talk);
                GameUi.HintOverride = TalkHint;
                Board.ShowIntro(Def.IntroTitle, Def.Intro, Def.IntroNote);
                yield return WaitPress();
                Board.Hide();
            }
            for (int i = start; i < List.Length; i++)
            {
                Index = i;
                yield return RunScene(List[i]);
                if (OneScene) break;
            }
            Finished = true;
            InScene = false;
            if (!TestList && !OneScene && Index == List.Length - 1) { PlayerPrefs.SetInt("haengin.demo.cleared", 1); PlayerPrefs.Save(); }
            Debug.Log($"[M3] 이야기 끝: 장면 {Trace.Count}개 · {Flags.Summary} · {Ledger.Summary} · 도감 {Names.Filled(Def.Names)}/9");
            Ended?.Invoke();
        }

        // ───────────────────────── 장면 하나
        IEnumerator RunScene(SceneDef s)
        {
            Scene = s; skipScene = false; InScene = true;
            // 오픈월드: 날짜는 세계 시계 그대로, 시각은 앞으로만(장면 시각까지). 회차 카드는 시계를 건드리지 않음
            if (WorldMode) { if (s.Kind != SceneKind.Card) GameClock.AdvanceTo(s.Hour * 60 + s.Minute); }
            else GameClock.Set(s.Month, s.Day, s.Hour, s.Minute);
            GameClock.Running = false;
            SceneStart = Snapshot(s);
            if (AutoSave && !WorldMode)
            {
                try { SaveStore.Write(SaveStore.Auto, SceneStart); }
                catch (Exception e) { Debug.LogWarning($"[M3] 자동 저장 실패: {e.Message}"); }
            }

            if (s.Kind == SceneKind.Card)
            {
                InputMaps.Use(Actions, InputMaps.Talk);
                GameUi.HintOverride = TalkHint;
                var ep = Def.Episode(s.Ep);
                var prev = Def.Episode(s.Ep - 1);
                if (ep != null) Card?.Show(ep, prev != null ? prev.Ledger : "");
                Mark(s);
                yield return Wait(EpisodeCard.Secs, false);
                if (!chainNoEffects) ApplyAll(s.Effects);
                End(s);
                yield break;
            }

            pendingLight = s.Light;
            // 8단계 배달 자전거: 1-2 → 1-3 → 1-4 는 한 번 탄 채 이어 간다(자리 옮김·먹 닦기 없이)
            var bike = s.InZone1 && s.Kind == SceneKind.Move ? BikeCourse.Current : null;
            int leg = bike != null ? BikeCourse.LegOf(s.Id) : -1;
            bool keepRiding = leg >= 0 && bike.Riding;
            if (Loader != null) yield return Loader.Go(s.Place, s.Spawn, s.HasPos && !keepRiding, s.Pos, s.Yaw, !keepRiding);
            else { OnCovered(); OnPlaced(); }
            Hud?.ShowDate();
            // 7단계 의상: 새벽 배달·현장 = 배달복, 그 밖 = 교복(장면 데이터 "outfit")
            var outfit = Loader != null && Loader.Motor != null ? Loader.Motor.GetComponent<Outfit>() : null;
            if (outfit != null) outfit.Set(string.IsNullOrEmpty(s.Outfit) ? Outfit.Uniform : s.Outfit);
            Mark(s);
            ApplyAll(s.Start);

            if (s.Kind == SceneKind.Move && Board != null && s.Board != null && s.Board.Length > 0 && Bot == BotMode.None)
            {
                // 조작 장면 소개 — 무슨 장면인지 보고 출발(봇은 건너뜀)
                InputMaps.Use(Actions, InputMaps.Talk);
                GameUi.HintOverride = TalkHint;
                Board.Show(s, Def.Episode(s.Ep), true);
                yield return WaitPress();
                Board.Hide();
            }
            if (s.Kind == SceneKind.Move)
            {
                InputMaps.Use(Actions, InputMaps.Explore);
                GameUi.HintOverride = null;
                if (Hud != null) Hud.KeepChip = s.Clock;
                GameClock.Running = s.Clock;
                var g = RouteGuide.Current;
                if (g != null && s.Targets.Length > 0)
                {
                    g.SetTargets(s.Targets);
                    foreach (var h in FindObjectsByType<RouteHud>(FindObjectsSortMode.None)) h.ResetFlash();
                }
                if (leg >= 0)
                {
                    // 주행 장면: 코스 다리 하나(1-2 경비실 · 1-3 꼭대기 집 도착 · 1-4 건네기 → 지각 대사 → 끝). 봇이면 코스 봇이 몬다(빠른 봇 = 건너뜀)
                    bike.Bot = Bot != BotMode.None;
                    Debug.Log($"[M3] 주행 {s.Id}: 다리 {leg} · {(bike.Riding ? "타는 중" : "새로 탐")}{(Bot != BotMode.None ? " · 봇 " + Bot : "")}");
                    yield return bike.RunLeg(leg, () => skipScene || Bot == BotMode.Fast);
                    if (bike.Riding) InputMaps.Use(Actions, InputMaps.Bike);
                    GameClock.Running = false;
                    if (Hud != null) Hud.KeepChip = false;
                    if (!chainNoEffects) ApplyAll(s.Effects);
                    Debug.Log($"[M3] 장면 {s.Id} 끝(주행{(skipScene ? " — 건너뜀" : "")})");
                    End(s);
                    yield break;
                }
                Debug.Log($"[M3] 이동 목표 {(g != null ? g.Stops.Length : -1)}개 · 첫 목표 {(g != null && g.CurrentTarget != null ? g.CurrentTarget.Label + " " + g.Distance.ToString("F1") + "m" : "없음")} · 시우 {(Loader != null && Loader.Motor != null ? Loader.Motor.Position.ToString("F1") : "-")}");
                float stuck = 0f; Vector3 last = Vector3.zero;
                while (g != null && !g.Finished && !skipScene)
                {
                    if (Bot == BotMode.Fast) BotJump(g);
                    else if (Bot == BotMode.Read) BotWalk(g, ref stuck, ref last);
                    GameClock.Tick(TimeFx.Dt);
                    yield return null;
                }
                if (Loader != null && Loader.Motor != null) Loader.Motor.ClearMoveInput();
                GameClock.Running = false;
                if (Hud != null) Hud.KeepChip = false;
            }
            else
            {
                InputMaps.Use(Actions, InputMaps.Talk);
                GameUi.HintOverride = TalkHint;
                var tl = Cuts != null ? Cuts.Get(s.Id) : null;
                string dk = string.IsNullOrEmpty(s.Dlg) ? s.Id : s.Dlg;
                bool hasTalk = Dialogue != null && Dialogue.Has(dk);
                // 대사 안에 "cut:<장면>" 이벤트가 있으면 컷신은 그 자리에서(1-6 학예회 회상 — 출석 대사 사이)
                bool cutInTalk = hasTalk && Dialogue.Get(dk).Any(x => x.Mode == DlgMode.Event && x.Text.Trim() == "cut:" + s.Id);
                if (tl != null && Cut != null && !cutInTalk) yield return Cut.Play(tl, WantSkip);
                if (hasTalk && Talk != null)
                {
                    if (Bot == BotMode.Fast || skipScene) Talk.RequestSkip();
                    yield return Talk.Play(Dialogue.Get(dk));
                }
                if (tl == null && !hasTalk)
                {
                    // 자리 장면 = 스토리보드 카드(원고 흐름·핵심 대사·'제작 중 장면'). 사람은 눌러야 넘어감(예전: len 초 뒤 저절로 — "막 지나감")
                    if (Board != null)
                    {
                        Board.Show(s, Def.Episode(s.Ep), false);
                        if (Bot != BotMode.None) yield return Wait(s.Len, true);
                        else yield return WaitPress();
                        Board.Hide();
                    }
                    else
                    {
                        Hud?.Stub($"[{s.Id} · {KindName(s.Kind)} 자리]  {s.Title}\n{s.Summary}");
                        yield return Wait(s.Len, true);
                        Hud?.Stub(null);
                    }
                }
            }
            if (!chainNoEffects) ApplyAll(s.Effects);
            Debug.Log($"[M3] 장면 {s.Id} 끝{(skipScene ? "(건너뜀)" : "")}");
            End(s);
        }

        const string TalkHint = "다음 × · Enter · 마우스 왼쪽   건너뛰기 길게 누르기   일시정지 Esc·Options";

        void Mark(SceneDef s)
        {
            foreach (var h in FindObjectsByType<RouteHud>(FindObjectsSortMode.None)) h.ResetFlash();     // 앞 장면 '도착' 알림이 다음 장면에 남지 않게
            Trace.Add($"{s.Id}|{s.DateLabel}|{s.TimeLabel}|{s.Place}|{s.Kind}");
            Debug.Log($"[M3] 장면 {s.Id} 시작: {s.DateLabel} {s.TimeLabel} · {s.Place} · {KindName(s.Kind)} · {s.Title}");
            SceneStarted?.Invoke(s);
        }

        // ───────────────────────── 대사 연출(1화 — 2026-10-25): 카메라 칸 = 프리셋 카메라(말하는 사람), 표정 칸 = 얼굴 칸(정식 모델), 말하는 동안 입 2장
        CutCamDriver lineCam;
        FaceRig talker;

        static CamPreset? PresetOf(string cam) => (cam ?? "").Trim() switch
        {
            "근접" => CamPreset.Close, "낮은 정면" => CamPreset.LowFront, "와이드" => CamPreset.Wide, "로우앵글" => CamPreset.LowAngle,
            "등 뒤 트래킹" => CamPreset.BackTrack, "측면 트래킹" => CamPreset.SideTrack, "미디엄" => CamPreset.Mid, _ => (CamPreset?)null,
        };

        void OnLine(DlgLine l)
        {
            if (talker != null) { talker.Talk = false; talker = null; }
            string who = l.Mode == DlgMode.Inner || l.Mode == DlgMode.Screen ? "시우" : l.Who;
            if (!string.IsNullOrEmpty(l.Face) && l.Face != "참기") { CutCtx.Face(l.Who, l.Face); lineFaces.Add(l.Who); }
            var actor = CutCtx.Actor(who);
            if (l.Mode == DlgMode.Sub && actor != null)
            {
                talker = actor.GetComponentInChildren<FaceRig>(false);
                if (talker != null) talker.Talk = true;
            }
            // 카메라 칸 "프리셋[:배우]" — 배우를 안 쓰면 말하는 사람(속마음·화면 글자는 시우)
            var cp = (l.Cam ?? "").Split(':');
            var pr = PresetOf(cp[0]);
            var camActor = cp.Length > 1 ? CutCtx.Actor(cp[1].Trim()) : actor;
            if (pr.HasValue && camActor != null)
            {
                if (lineCam != null) CutCams.Release(lineCam);
                var brain = Camera.main != null ? Camera.main.GetComponent<Unity.Cinemachine.CinemachineBrain>() : null;
                if (brain != null && !lineBlendSaved)
                {
                    // 대사 카메라 사이는 컷(영화식) — 블렌드 중간 자리가 배우 머리·몸을 지나가던 것(1화 1-5)
                    lineBlend = brain.DefaultBlend; lineBlendSaved = true;
                    brain.DefaultBlend = new Unity.Cinemachine.CinemachineBlendDefinition(Unity.Cinemachine.CinemachineBlendDefinition.Styles.Cut, 0f);
                }
                var other = camActor == CutCtx.Player ? null : CutCtx.Player;
                lineCam = CutCams.Make(pr.Value, camActor, other);
                lineCam.SetLive(true);
            }
        }

        Unity.Cinemachine.CinemachineBlendDefinition lineBlend;
        bool lineBlendSaved;

        readonly HashSet<string> lineFaces = new HashSet<string>();

        void EndLines()
        {
            foreach (var w in lineFaces) CutCtx.ClearFace(w);
            lineFaces.Clear();
            if (talker != null) { talker.Talk = false; talker = null; }
            if (lineCam != null) { CutCams.Release(lineCam); lineCam = null; }
            if (lineBlendSaved)
            {
                var brain = Camera.main != null ? Camera.main.GetComponent<Unity.Cinemachine.CinemachineBrain>() : null;
                if (brain != null) brain.DefaultBlend = lineBlend;
                lineBlendSaved = false;
            }
        }

        void End(SceneDef s)
        {
            EndLines();
            var g = RouteGuide.Current;
            if (g != null) g.ClearTargets();
            Hud?.Screen(null, 0f);
            LedgerUi?.HideTable();
            LedgerUi?.HideQuest();
            if (Endure != null && Endure.On) Endure.End();
            InkMode.Set(0f);
            Talk?.ClearSkip();
            InScene = false;
            SceneEnded?.Invoke(s);
        }

        void OnCovered() { Card?.Hide(); RestoreCast(); }

        // ───────────────────────── 장면 배역(QA 2차 B, 2026-10-26): cast: 로 켠 배우 · place:/pose: 로 옮긴 배우는 다음 먹 닦기(장소 이동)·멈춤 때 원래대로
        sealed class Kept { public Vector3 Pos; public Quaternion Rot; public bool Active; public bool HadPose; public string Pose = ""; }
        readonly Dictionary<Transform, Kept> kept = new Dictionary<Transform, Kept>();
        /// 지금 켜 두었거나 옮겨 둔 배우 수(시험)
        public int CastHeld => kept.Count;

        void Remember(Transform t)
        {
            if (t == null || kept.ContainsKey(t) || (Loader != null && Loader.Motor != null && t == Loader.Motor.transform)) return;
            var cp = t.GetComponent<CastPose>();
            kept[t] = new Kept { Pos = t.localPosition, Rot = t.localRotation, Active = t.gameObject.activeSelf, HadPose = cp != null, Pose = cp != null ? cp.State : "" };
        }

        public void RestoreCast()
        {
            foreach (var kv in kept)
            {
                var t = kv.Key; var k = kv.Value;
                if (t == null) continue;
                foreach (var cl in t.GetComponentsInChildren<CastLook>(true)) { cl.Look(null); cl.Raise(null); cl.Face(null); }
                t.localPosition = k.Pos; t.localRotation = k.Rot;
                var cp = t.GetComponent<CastPose>();
                if (cp != null) { if (k.HadPose) { if (cp.State != k.Pose) cp.Set(k.Pose); } else Destroy(cp); }
                if (t.gameObject.activeSelf != k.Active) t.gameObject.SetActive(k.Active);
            }
            kept.Clear();
        }

        /// 이름 붙은 자리(무대의 빈 오브젝트)로 배우를 옮김 — cast:/place: 공용
        static void PutAt(Transform a, Transform spot)
        {
            a.position = spot.position;
            a.rotation = Quaternion.Euler(0f, spot.eulerAngles.y, 0f);
        }

        /// 카드 실제 장면(QA 2차 B): 대사 TSV 장면 하나를 지금 자리에서(배우·카메라·자막·이벤트). 오픈월드 카드가 스토리보드 대신 부름
        public IEnumerator PlayDlg(string key)
        {
            if (Dialogue == null || Talk == null || string.IsNullOrEmpty(key) || !Dialogue.Has(key)) yield break;
            bool wasRunning = running;
            running = true; InScene = true; skipScene = false;
            Talk.ClearSkip();
            Talk.Bot = Bot != BotMode.None ? (l => BotAnswer != null ? BotAnswer(l) : 0) : null;
            InputMaps.Use(Actions, InputMaps.Talk);
            GameUi.HintOverride = TalkHint;
            Hud?.ShowDate();
            DlgPlayed++;
            Debug.Log($"[M3] 카드 장면 {key} 시작: {GameClock.Label} · {(Loader != null ? Loader.Place : "-")}");
            if (Bot == BotMode.Fast) Talk.RequestSkip();
            yield return Talk.Play(Dialogue.Get(key));
            bool skipped = Talk.Skipped;
            EndLines();
            Hud?.Screen(null, 0f);
            if (Endure != null && Endure.On) Endure.End();
            InkMode.Set(0f);
            Talk.ClearSkip();
            InScene = false; running = wasRunning;
            InputMaps.Use(Actions, InputMaps.Explore);
            GameUi.HintOverride = null;
            Debug.Log($"[M3] 카드 장면 {key} 끝{(skipped ? "(건너뜀)" : "")}");
        }

        /// 카드 실제 장면을 튼 횟수(시험)
        public int DlgPlayed { get; private set; }

        void OnPlaced() { if (Light != null) Light.Apply(pendingLight, true); }

        public static string KindName(SceneKind k) => k switch
        {
            SceneKind.Card => "회차 카드", SceneKind.Cut => "컷신", SceneKind.Move => "조작", SceneKind.Mini => "미니게임",
            SceneKind.Fight => "전투", SceneKind.Montage => "몽타주", _ => "화면 연출",
        };

        bool WantSkip() => skipScene || Bot == BotMode.Fast || (TalkIn != null && TalkIn.HoldTime >= DialogueRunner.HoldToSkip);

        /// 지금 장면을 끝냄(봇·디버그). 끝 상태는 같은 길로 적용된다
        public void SkipScene() { skipScene = true; Talk?.RequestSkip(); }

        /// 사람이 누를 때까지(× · Enter · Space · 마우스 왼쪽 — Talk 맵 Next). 건너뛰기(길게)·봇이면 바로
        IEnumerator WaitPress()
        {
            pressed = false;
            yield return null;
            while (true)
            {
                if (WantSkip() || Bot == BotMode.Fast) yield break;
                if (Bot == BotMode.Read) { yield return Wait(1.2f, true); yield break; }
                if (TalkIn != null && TalkIn.Next) yield break;
                if (pressed) { pressed = false; yield break; }
                yield return null;
            }
        }

        bool pressed;
        /// 카드 넘기기(테스트·스모크 — 사람이 × 를 누른 것과 같음)
        public void Press() { pressed = true; }

        IEnumerator Wait(float secs, bool allowNext)
        {
            float t = 0f;
            yield return null;
            while (t < secs)
            {
                if (WantSkip()) yield break;
                if (Bot == BotMode.Read && t >= Mathf.Min(secs, 1.2f)) yield break;
                if (allowNext && TalkIn != null && TalkIn.Next) yield break;
                if (!GameState.Paused) t += UiKit.RealDt;
                yield return null;
            }
        }

        // ───────────────────────── 봇
        void BotJump(RouteGuide g)
        {
            var t = g.CurrentTarget;
            if (t == null || Loader == null || Loader.Motor == null) return;
            var m = Loader.Motor;
            var d = new Vector3(t.Pos.x - m.Position.x, 0f, t.Pos.z - m.Position.z);
            if (d.magnitude <= t.Radius) return;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            m.Teleport(t.Pos + Vector3.up * 0.05f, yaw);
            Loader.Cam?.SnapBehind();
        }

        void BotWalk(RouteGuide g, ref float stuck, ref Vector3 last)
        {
            var t = g.CurrentTarget;
            if (t == null || Loader == null || Loader.Motor == null) return;
            var m = Loader.Motor;
            var d = new Vector3(t.Pos.x - m.Position.x, 0f, t.Pos.z - m.Position.z);
            m.SetMoveInput(d.normalized, 1f, false);
            stuck = (m.Position - last).sqrMagnitude < 0.0004f ? stuck + TimeFx.Dt : 0f;
            last = m.Position;
            if (stuck > 3f) { BotJump(g); stuck = 0f; }
        }

        // ───────────────────────── 명령(가계부·도감·플래그 …)
        public IEnumerator ApplyCo(string cmd)
        {
            if (cmd.StartsWith("braidgame"))
            {
                int n = cmd.Length > 10 ? Ledger.ParseInt(cmd.Substring(10)) : 1;
                if (Braid != null) yield return Braid.Play(n, Bot != BotMode.None, WantSkip);
                yield break;
            }
            if (cmd.StartsWith("cut:"))
            {
                var tl = Cuts != null ? Cuts.Get(cmd.Substring(4)) : null;
                if (tl != null && Cut != null) yield return Cut.Play(tl, WantSkip);
                yield break;
            }
            if (cmd.StartsWith("wait:"))
            {
                // wait:초 — 대사 사이 침묵(무음 2초 같은 것). 건너뛰기·봇은 짧게
                float secs = float.TryParse(cmd.Substring(5), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float ws) ? ws : 1f;
                yield return Wait(secs, true);
                yield break;
            }
            if (cmd == "reveal" && LedgerUi != null && Def != null)
            {
                if (Bot == BotMode.Fast) { LedgerUi.RevealNow(Def.LedgerRows); yield break; }
                yield return LedgerUi.Reveal(Def.LedgerRows);
                yield break;
            }
            ApplyNow(cmd);
        }

        public void ApplyAll(string[] cmds) { if (cmds != null) foreach (var c in cmds) ApplyNow(c); }

        public void ApplyNow(string cmd)
        {
            if (string.IsNullOrEmpty(cmd)) return;
            string verb = cmd, arg = "";
            int eq = cmd.IndexOf('='), co2 = cmd.IndexOf(':');
            if (eq > 0 && (co2 < 0 || eq < co2)) { verb = cmd.Substring(0, eq); arg = cmd.Substring(eq + 1); if (verb == "wallet") { Ledger.SetWallet(Ledger.ParseInt(arg)); return; } }
            else if (co2 > 0) { verb = cmd.Substring(0, co2); arg = cmd.Substring(co2 + 1); }
            switch (verb)
            {
                case "ledger": case "wallet": case "envelope": case "spend": case "item": case "entry":
                    {
                        var line = Ledger.Apply(verb, arg);
                        if (verb == "ledger" && WorldMode) HouseholdHook?.Invoke(Ledger.ParseInt(arg.Split(':')[0]));
                        if (verb != "entry") LedgerUi?.Toast(line);
                        break;
                    }
                case "name":
                    if (Names.Register(arg, Def != null ? Def.Names : null)) NameUi?.Register(arg);
                    break;
                case "flag": Flags.Raise(arg); break;
                case "braidgame": break;                                  // 건너뛰면 땋기 없음(결과는 장면 끝 braid:3)
                case "cut": InkMode.Set(0f); break;                       // 건너뛴 회상의 끝 상태
                case "hand":
                    {
                        // hand:배우:자세[:무게] — '해제'면 풂(정식 모델 = HandPoser 8종, 그 밖 = 주먹 셰이프 키)
                        var hp = arg.Split(':');
                        if (hp.Length >= 2) CutCtx.Hand(hp[0], hp[1] == "해제" ? "" : hp[1], hp[1] == "해제" ? 0f : hp.Length >= 3 ? Ledger.ParseInt(hp[2]) / 100f : 1f);
                        break;
                    }
                case "look":
                case "raise":
                case "face":
                    {
                        // look:배우|반:대상|해제 — 고개(몸 조금)를 대상 쪽으로 / raise:배우:대상|해제 — 오른팔을 대상 쪽으로 들기(엄지 척) / face:배우:대상|해제 — 앉은 채 돌아앉기
                        var lp = arg.Split(':');
                        if (lp.Length < 2) break;
                        var target = lp[1] == "해제" ? null : CutCtx.Actor(lp[1]);
                        foreach (var cl in CastLook.For(lp[0]))
                        {
                            if (verb == "look") cl.Look(target);
                            else if (verb == "face") cl.Face(target, lp[0] == "반" ? 110f : 180f);     // 반 전체는 의자에서 비틀어 돌아보는 만큼(110°)
                            else cl.Raise(target);
                        }
                        break;
                    }
                case "place":
                    {
                        // QA Q11: place:배우:자리 — 그 자리(무대의 이름 붙은 점, 예: '땋기 자리')로 옮기고 그 방향으로. 시우면 몸(PlayerMotor)째 — 1-5 땋기에서 시우가 2m 밖에 서 있던 것
                        var pp = arg.Split(':');
                        if (pp.Length < 2) break;
                        var spot = CutCtx.Actor(pp[1]);
                        if (spot == null) { Debug.LogWarning($"[M3] place: 자리 '{pp[1]}' 없음"); break; }
                        if (pp[0] == "시우" && Loader != null && Loader.Motor != null)
                        {
                            // 바닥에 붙여(자리 높이 0 그대로 두면 한두 프레임 공중 — D02 접지)
                            var p = spot.position;
                            if (Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, out var gh, 1.5f, Layers.Mask(Layers.Ground, Layers.Default, Layers.Wall), QueryTriggerInteraction.Ignore)) p.y = gh.point.y;
                            Loader.Motor.ClearMoveInput(); Loader.Motor.Teleport(p + Vector3.up * 0.02f, spot.eulerAngles.y); Loader.Cam?.SnapBehind();
                        }
                        else { var a = CutCtx.Actor(pp[0]); if (a != null) { Remember(a); PutAt(a, spot); } }
                        break;
                    }
                case "cast":
                    {
                        // QA 2차 B: cast:배우[:자리] — 숨겨 둔 배우를 켜고(자리가 있으면 그리로). 다음 장소 이동 때 원래대로(숨김)
                        var cp3 = arg.Split(':');
                        var a = CutCtx.Actor(cp3[0]);
                        if (a == null) { Debug.LogWarning($"[M3] cast: 배우 '{cp3[0]}' 없음"); break; }
                        Remember(a);
                        if (cp3.Length >= 2) { var spot = CutCtx.Actor(cp3[1]); if (spot != null) PutAt(a, spot); else Debug.LogWarning($"[M3] cast: 자리 '{cp3[1]}' 없음"); }
                        if (!a.gameObject.activeSelf) a.gameObject.SetActive(true);
                        break;
                    }
                case "front":
                    {
                        // QA 2차 B: front:배우:대상:거리 — 배우를 대상 앞(대상 방향으로 거리 m)에 세우고 대상을 보게(무대에 이름 붙은 자리가 없을 때 — 체육관 N01)
                        var fp = arg.Split(':');
                        if (fp.Length < 2) break;
                        var a = CutCtx.Actor(fp[0]); var to = CutCtx.Actor(fp[1]);
                        if (a == null || to == null) { Debug.LogWarning($"[M3] front: '{arg}' 배우·대상 없음"); break; }
                        float dist = fp.Length >= 3 && float.TryParse(fp[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float fd) ? fd : 1.6f;
                        var f = to.forward; f.y = 0f; if (f.sqrMagnitude < 1e-4f) f = Vector3.forward; f.Normalize();
                        Remember(a);
                        if (!a.gameObject.activeSelf) a.gameObject.SetActive(true);
                        a.position = to.position + f * dist;
                        a.rotation = Quaternion.LookRotation(-f, Vector3.up);
                        break;
                    }
                case "uncast":
                    {
                        var a = CutCtx.Actor(arg);
                        if (a != null) { Remember(a); a.gameObject.SetActive(false); }
                        break;
                    }
                case "wait": break;                                       // 건너뛰면 침묵 없음
                case "pose":
                    {
                        // pose:배우:상태(Idle · Sit · TalkOpen · TalkHip · SitClap · SitFist)
                        var pp = arg.Split(':');
                        var a = pp.Length >= 2 ? CutCtx.Actor(pp[0]) : null;
                        if (a != null && pp[0] != "시우") { Remember(a); var cp = a.GetComponent<CastPose>() ?? a.gameObject.AddComponent<CastPose>(); cp.Set(pp[1]); }
                        break;
                    }
                case "endure": Flags.Endured++; break;
                case "endurefx": if (arg == "on") Endure?.Begin(); else Endure?.End(); break;
                case "rel":
                    {
                        var p = arg.Split(':');
                        int v = p.Length > 1 ? Ledger.ParseInt(p[1]) : 1;
                        if (p[0] == "haneul") Flags.Haneul += v; else if (p[0] == "nari") Flags.Nari += v;
                        break;
                    }
                case "braid": Flags.BraidTries += arg.Length > 0 ? Ledger.ParseInt(arg) : 1; break;
                case "ink": InkMode.Set(arg == "0" ? 0f : 1f); break;
                case "reveal": LedgerUi?.RevealNow(Def != null ? Def.LedgerRows : new string[0]); break;
                case "reveal_hide": LedgerUi?.HideTable(); break;
                case "quest":
                    if (LedgerUi != null && Def != null)
                    {
                        int dday = (new DateTime(GameClock.Year, 5, 1) - new DateTime(GameClock.Year, GameClock.Month, GameClock.Day)).Days;
                        LedgerUi.ShowQuest(Def.QuestHead, Def.QuestMemo, $"D-{dday} · {Ledger.EnvelopeText}");
                    }
                    break;
                case "quest_hide": LedgerUi?.HideQuest(); break;
                case "screen": Hud?.Screen(arg, 2.5f); break;
                default: Debug.LogWarning($"[M3] 모르는 명령: {cmd}"); break;
            }
        }

        // ───────────────────────── 저장
        public SaveData Snapshot(SceneDef s) => new SaveData
        {
            SceneId = s.Id, Test = TestList,
            Month = GameClock.Month, Day = GameClock.Day, Minutes = GameClock.Minutes,
            Ledger = JsonUtility.FromJson<Ledger>(JsonUtility.ToJson(Ledger)),
            Names = JsonUtility.FromJson<NameBook>(JsonUtility.ToJson(Names)),
            Flags = Flags.Clone(),
            Gym = GymState.Current != null ? GymState.Current.Clone() : new GymSave(),
            Label = $"{s.DateLabel} {s.TimeLabel} · {s.Title}",
        };

        /// 일시정지 메뉴 '저장하고 타이틀로': 지금 장면 시작 상태를 수동 칸에 → 타이틀
        public void SaveAndTitle()
        {
            if (SceneStart != null)
            {
                try { SaveStore.Write(SaveStore.Manual, SceneStart); }
                catch (Exception e) { Debug.LogWarning($"[M3] 저장 실패: {e.Message}"); }
            }
            Stop();
            GameState.SetPaused(false);
            StoryBoot.Go(null, StoryBoot.TitleScene);
        }

        /// 일시정지 메뉴 '도감'
        public void OpenBook()
        {
            if (NameUi == null || Def == null) return;
            NameUi.ShowGrid(Def.Names, Names);
            GameUi.SubOpen = () => NameUi != null && NameUi.GridShown;
            GameUi.SubClose = () => { if (NameUi != null) NameUi.HideGrid(); };
        }
    }
}
