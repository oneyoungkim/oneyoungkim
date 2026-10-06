// 행인1의 메인이벤트 — 이야기 진행(docs/09_M3_버티컬슬라이스_설계.md 2-1 '하루 진행'): 장면 목록을 순서대로 실행한다(데모 = 고정 일정).
// 장면 하나 = 시각 점프 → 자동 저장(장면 시작 상태) → 장소 이동(먹 닦기, 덮인 동안 시간대 조명) → 날짜 판 →
//   몸통(회차 카드 3초 / 이동 목표 / 컷신(타임라인) / 대사 / 아직 없는 장면은 '자리 장면' 요약 자막) → 끝 명령(가계부·도감·플래그) → 다음.
// 이야기 중에는 Zone1 의 M2 무대(인카운터 Y4·야차 Y1)를 끈다(7장 결정 21 — M2 는 타이틀 '연습장'에서만).
// Zone1 장면 루트 'Story' 에 붙는다(StorySetup). Zone1 을 그냥 열면(M1·M2 테스트·연습장) 아무것도 하지 않는다 — 타이틀이 StoryBoot.Request 를 넣었을 때만 시작.
// 봇(테스트·녹화): Fast = 이동 목표로 순간 이동·대사와 컷신은 처음에 건너뜀, Read = 대사를 글자 수 ÷ 8 + 0.8초에 넘기고 목표까지 걸어감.
using System;
using System.Collections;
using System.Collections.Generic;
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
        public LedgerUi LedgerUi;
        public NameBookUi NameUi;
        public EndureFx Endure;
        public DayLight Light;
        /// Zone1 의 M2 무대 루트('Stages')
        public GameObject M2Stages;

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
        DayPart pendingLight;

        void OnEnable() { Instance = this; }
        void OnDisable() { if (Instance == this) Instance = null; }

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
        }

        IEnumerator Start()
        {
            yield return null;
            var r = StoryBoot.Take();
            if (r == null || Suppress) yield break;
            switch (r.Mode)
            {
                case StoryBoot.Mode.New: Begin(false, null, null); break;
                case StoryBoot.Mode.Continue: if (r.Save != null) Begin(r.Save.Test, r.Save.SceneId, r.Save); break;
                case StoryBoot.Mode.Select: Begin(false, r.SceneId, null); break;
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
            co = StartCoroutine(Run(start));
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
            GameUi.BookHook = GameUi.TitleHook = null;
            GameUi.SubOpen = null; GameUi.SubClose = null;
            SetM2(true);
            running = false; InScene = false;
        }

        void SetM2(bool on)
        {
            if (M2Stages != null && M2Stages.activeSelf != on) M2Stages.SetActive(on);
        }

        IEnumerator Run(int start)
        {
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
            GameClock.Set(s.Month, s.Day, s.Hour, s.Minute);
            GameClock.Running = false;
            SceneStart = Snapshot(s);
            if (AutoSave)
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
                ApplyAll(s.Effects);
                End(s);
                yield break;
            }

            pendingLight = s.Light;
            if (Loader != null) yield return Loader.Go(s.Place, s.Spawn, s.HasPos, s.Pos, s.Yaw, true);
            else { OnCovered(); OnPlaced(); }
            Hud?.ShowDate();
            Mark(s);
            ApplyAll(s.Start);

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
                if (tl != null && Cut != null) yield return Cut.Play(tl, WantSkip);
                if (hasTalk && Talk != null)
                {
                    if (Bot == BotMode.Fast || skipScene) Talk.RequestSkip();
                    yield return Talk.Play(Dialogue.Get(dk));
                }
                if (tl == null && !hasTalk)
                {
                    Hud?.Stub($"[{s.Id} · {KindName(s.Kind)} 자리]  {s.Title}\n{s.Summary}");
                    yield return Wait(s.Len, true);
                    Hud?.Stub(null);
                }
            }
            ApplyAll(s.Effects);
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

        void End(SceneDef s)
        {
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

        void OnCovered() { Card?.Hide(); }

        void OnPlaced() { if (Light != null) Light.Apply(pendingLight, true); }

        public static string KindName(SceneKind k) => k switch
        {
            SceneKind.Card => "회차 카드", SceneKind.Cut => "컷신", SceneKind.Move => "조작", SceneKind.Mini => "미니게임",
            SceneKind.Fight => "전투", SceneKind.Montage => "몽타주", _ => "화면 연출",
        };

        bool WantSkip() => skipScene || Bot == BotMode.Fast || (TalkIn != null && TalkIn.HoldTime >= DialogueRunner.HoldToSkip);

        /// 지금 장면을 끝냄(봇·디버그). 끝 상태는 같은 길로 적용된다
        public void SkipScene() { skipScene = true; Talk?.RequestSkip(); }

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
                        if (verb != "entry") LedgerUi?.Toast(line);
                        break;
                    }
                case "name":
                    if (Names.Register(arg, Def != null ? Def.Names : null)) NameUi?.Register(arg);
                    break;
                case "flag": Flags.Raise(arg); break;
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
