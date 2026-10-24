// 행인1의 메인이벤트 — 드릴 상태기(docs/12_체육관_설계.md 4-6 DrillRunner): Intro → Round(n) → Rest → Result
// 입력: HInput Combat 맵(□ Light · △ Heavy · ○ Grab · × Dodge · R1 Guard · L 스틱 · Kick)을 직접 읽는다(PInput 은 드릴 중 끔 — D11 스파링은 M2 그대로 켬).
// 시간: TimeFx.Dt(히트스톱·일시정지면 0, 1/20 상한) — 시험은 Time.captureDeltaTime 1/60 고정. 입력 시각은 그 프레임의 라운드 시각(봇은 Inject 로 정확 시각).
// '길게'(□─ △─ R2─) 는 누른 뒤 HoldTime(0.14초) 안에 떼면 톡, 아니면 길게 — 판정은 그때 나가고 시각은 누른 때로.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Haengin
{
    /// 한 프레임의 입력 상태(스테이션이 Tick 에서 봄)
    public struct DrillInput
    {
        public bool Guard, Run;
        public Vector2 Stick;
        public bool StickDown => Stick.y < -0.5f;
        public bool StickUp => Stick.y > 0.5f;
        public int StickSide => Stick.x > 0.5f ? 1 : Stick.x < -0.5f ? -1 : 0;
    }

    /// 드릴별 구현(4-6 IDrillStation). 스테이션은 로그에 샘플을 넣고, 시우·NPC·소품 연출을 맡는다
    public abstract class DrillStation
    {
        protected DrillRunner R;
        public DrillDef Def => R.Def;
        public DrillLog Log => R.Log;
        public GymActors Actors => R.Actors;
        /// 박자 띠(HUD 가 그림 — null 이면 없음)
        public BeatTrack Beat;
        /// 지금 콤비(HUD 아이콘 띠)
        public ComboRun Combo;
        /// HUD 한 줄(스테이션 안내)
        public virtual string Hint => "";
        public virtual void Begin(DrillRunner r) { R = r; }
        public virtual void RoundStart(int round) { }
        public virtual void Tick(float dt, double t, in DrillInput inp) { }
        public virtual void Press(DrillBtn b, bool hold, bool stickDown, double t) { }
        public virtual void RoundEnd(int round) { }
        public virtual void End(bool aborted) { }
        /// 봇(시험·스모크): 라운드 시각 t 에 넣을 입력. skill 1 = 완벽(S), 낮을수록 오차·실수
        public virtual void Bot(double t, float skill) { }
        /// 라운드가 조기 종료됐나(D11 스톱)
        public bool EndRoundNow;
        /// 드릴 전체 종료(뇌진탕)
        public bool EndDrillNow;

        protected double Jitter(int i, float skill, double maxMs)
        {
            // 결정적 '사람 오차': skill 1 → 0, 0 → ±maxMs
            double u = Math.Sin(i * 12.9898 + 78.233) * 43758.5453; u -= Math.Floor(u);
            return (u * 2.0 - 1.0) * maxMs * (1.0 - skill);
        }
        protected bool Flub(int i, float skill, double chanceAtZero)
        {
            double u = Math.Sin(i * 7.131 + 1.17) * 1234.567; u -= Math.Floor(u);
            return u < chanceAtZero * (1.0 - skill);
        }
    }

    /// 체육관 배우·소품 묶음(GymManager 가 채움)
    public sealed class GymActors
    {
        public PlayerMotor Motor;
        public PlayerCombat Player;
        /// 시우 몸(보통 Player.Me — 시험은 허수아비 Fighter 를 넣는다)
        public Fighter PlayerFighter;
        public Fighter Me => PlayerFighter != null ? PlayerFighter : Player != null ? Player.Me : null;
        public PInput Input;
        public Transform Visual;
        public GymNpc Yunjun, Gojun;
        public Fighter Mitt;                       // 이윤준 미트(Fighter)
        public List<BagSwing> Bags = new List<BagSwing>();
        public Transform Dummy, SpeedBag;
        public CombatMode Mode;
        public CombatTuning Tuning;
        public MoveSet Moves;
        public GameObject GojunFighterPrefab;
        public EnemyDef SparDef;
        /// 전용 동작 표(없으면 대체 동작)
        public GymClips Clips;
        public bool HasClip(string state) => Clips != null && Clips.Has(state);
        public Transform Station;                  // 지금 드릴 자리(소켓)
        public Action<string, string> Say;         // (누가, 말) → HUD 큐
        public Action<string> Toast;               // 짧은 알림("걸림!" "팡!")
    }

    [DisallowMultipleComponent]
    public sealed class DrillRunner : MonoBehaviour
    {
        public enum Phase { Idle, Intro, Round, Rest, Result }
        public const float IntroSec = 1.6f, HoldTime = 0.14f;

        public InputActionAsset Actions;
        public DrillDef Def { get; private set; }
        public DrillLog Log { get; private set; }
        public GymActors Actors { get; private set; }
        public DrillStation Station { get; private set; }
        public Phase State { get; private set; }
        public int Round { get; private set; }
        /// 라운드(또는 단계) 안 시각(초)
        public double T { get; private set; }
        public double RoundLeft => Def != null ? Math.Max(0, Def.RoundSec - T) : 0;
        public DrillResult Result { get; private set; }
        public DrillReward Reward { get; private set; }
        public bool Running => State != Phase.Idle && State != Phase.Result;
        public bool IsSim { get; private set; }
        public int Speed => GymState.Current.Stat(Stat.Speed);
        /// 봇: 0 = 끔, 아니면 skill(0.01~1)
        public float Bot;
        public event Action<DrillResult, DrillReward> Finished;
        public event Action<int> RoundStarted;
        public event Action<int> RoundEnded;
        public event Action<string> Toasted;
        public DrillInput Input => inp;
        public int Presses { get; private set; }

        InputAction aLight, aHeavy, aGrab, aDodge, aGuard, aMove, aRun, aKick, aPause, aLookPad, aLookMouse;
        /// 드릴 중 카메라 좌우 밀기(−1~1 → ±15°, 2장 '좌우 ±15° 만')
        public float Nudge { get; private set; }
        bool pL, pH, pG, pD, pK, pPause, mapOn;
        DrillInput inp;
        struct Pending { public DrillBtn B; public double T; public double Age; public bool StickDown; }
        readonly List<Pending> pending = new List<Pending>();
        float phaseT;
        bool prevGuard;

        void Bind()
        {
            if (Actions == null || aLight != null) return;
            InputAction C(string n) => Actions.FindAction("Combat/" + n, false);
            aLight = C("Light"); aHeavy = C("Heavy"); aGrab = C("Grab"); aDodge = C("Dodge"); aGuard = C("Guard"); aMove = C("Move"); aRun = C("Run"); aKick = C("Kick"); aPause = C("Pause"); aLookPad = C("LookPad"); aLookMouse = C("LookMouse");
        }

        /// 드릴 시작. station = 구현(null 이면 자리만 → 바로 끝). 플레이어 입력(PInput)은 끈다(D11 은 켠 채)
        public void Begin(DrillDef def, DrillStation station, GymActors actors, bool sim = false)
        {
            if (Running) Abort();
            Def = def; Actors = actors; Station = station; IsSim = sim;
            Log = new DrillLog { DrillId = def.Id, Rounds = def.Rounds, RoundSec = def.RoundSec };
            Result = null; Reward = default;
            Round = 0; T = 0; phaseT = 0; Presses = 0; Nudge = 0f;
            pending.Clear();
            pL = pH = pG = pD = pK = false;
            Bind();
            if (!def.UsesCombat || def.Id != "D11")
            {
                if (actors.Input != null) actors.Input.enabled = false;
                if (Actions != null) { Actions.FindActionMap("Explore", false)?.Disable(); Actions.FindActionMap("Combat", false)?.Enable(); mapOn = true; }
            }
            if (actors.Motor != null) actors.Motor.ClearMoveInput();
            State = Phase.Intro;
            Station?.Begin(this);
            Actors.Say?.Invoke("이윤준", def.CueStart);
            Debug.Log($"[Gym] 드릴 시작 {def.Id} {def.NameKr} · {def.Rounds}R × {def.RoundSec}s · 봇 {Bot:F2}");
        }

        /// 그만두기(일시정지 → 그만두기): 보상 0 · 피로 절반(4-1)
        public void Abort()
        {
            if (!Running) return;
            Log.Aborted = true;
            Station?.End(true);
            Finish();
        }

        void Finish()
        {
            Result = DrillGrader.Evaluate(Def, Log);
            var save = GymState.Current;
            Reward = DrillRewards.Apply(Def, Result, false, save);
            if (Log.Concussion) DrillSim.Concussion(save, GymSave.Today);
            State = Phase.Result;
            Release();
            Debug.Log($"[Gym] 드릴 끝 {Result} · {Reward} · {save.Summary}");
            Finished?.Invoke(Result, Reward);
        }

        void Release()
        {
            if (mapOn && Actions != null) { Actions.FindActionMap("Combat", false)?.Disable(); mapOn = false; }
            if (Actors != null)
            {
                if (Actors.Input != null) { Actors.Input.enabled = true; Actors.Input.SetCombatMap(false); }
                if (Actors.Player != null && Actors.Player.Me != null) Actors.Player.Me.ScriptAnim = null;
                if (Actors.Visual != null) Actors.Visual.localPosition = Vector3.zero;
            }
        }

        /// 결과 카드를 닫고 Idle 로
        public void Close() { if (State == Phase.Result) State = Phase.Idle; }

        public void Toast(string s) { Toasted?.Invoke(s); Actors?.Toast?.Invoke(s); }

        /// 봇·시험: 입력 하나를 정확한 라운드 시각에(Round 중일 때만)
        public void Inject(DrillBtn b, double t, bool hold = false, bool stickDown = false)
        {
            if (State != Phase.Round) return;
            Presses++;
            Station?.Press(b, hold, stickDown, t);
        }
        public void InjectGuard(bool on) { inp.Guard = on; }
        public void InjectStick(Vector2 s) { inp.Stick = s; }

        void Update()
        {
            if (State == Phase.Idle || State == Phase.Result) return;
            // 일시정지(Esc·Options) — PInput 이 꺼져 있을 때 여기서
            Bind();
            if (mapOn && aPause != null && GameUi.Edge(aPause, ref pPause)) GameState.TogglePause();
            if (GameState.Paused || GameState.Modal) return;
            Step(TimeFx.Dt);
        }

        /// 한 걸음(게임 시간 dt). Update 가 TimeFx.Dt 로 부르고, 시험은 고정 dt 로 직접 부른다(G12 프레임레이트 독립)
        public void Step(float dt)
        {
            if (State == Phase.Idle || State == Phase.Result || dt <= 0f) return;
            ReadInput(dt);
            if (Bot > 0f && State == Phase.Round) Station?.Bot(T, Bot);
            switch (State)
            {
                case Phase.Intro:
                    phaseT += dt;
                    if (phaseT >= IntroSec) StartRound(1);
                    break;
                case Phase.Round:
                    T += dt;
                    Station?.Tick(dt, T, in inp);
                    if (Station != null && Station.EndDrillNow) { EndRound(); Station.End(false); Finish(); return; }
                    if (T >= Def.RoundSec || (Station != null && Station.EndRoundNow))
                    {
                        EndRound();
                        if (Round >= Def.Rounds) { Station?.End(false); Finish(); }
                        else { State = Phase.Rest; phaseT = 0f; Actors.Say?.Invoke("이윤준", Def.CueRound); }
                    }
                    break;
                case Phase.Rest:
                    phaseT += dt;
                    if (phaseT >= Def.RestSec) StartRound(Round + 1);
                    break;
            }
        }

        void StartRound(int r)
        {
            Round = r; T = 0; State = Phase.Round;
            if (Station != null) { Station.EndRoundNow = false; Station.RoundStart(r); }
            RoundStarted?.Invoke(r);
        }

        void EndRound()
        {
            Station?.RoundEnd(Round);
            RoundEnded?.Invoke(Round);
        }

        void ReadInput(float dt)
        {
            if (mapOn && Bot <= 0f)
            {
                float look = (aLookPad != null ? aLookPad.ReadValue<Vector2>().x : 0f) + (aLookMouse != null ? Mathf.Clamp(aLookMouse.ReadValue<Vector2>().x * 0.02f, -1f, 1f) : 0f);
                Nudge = Mathf.Clamp(Mathf.MoveTowards(Nudge, Mathf.Clamp(look, -1f, 1f), dt * 4f) * (Mathf.Abs(look) < 0.05f ? 0.92f : 1f), -1f, 1f);
                inp.Stick = aMove != null ? Vector2.ClampMagnitude(aMove.ReadValue<Vector2>(), 1f) : Vector2.zero;
                inp.Guard = aGuard != null && aGuard.IsPressed();
                inp.Run = aRun != null && aRun.IsPressed();
                if (aLight != null && GameUi.Edge(aLight, ref pL)) Queue(DrillBtn.Light);
                if (aHeavy != null && GameUi.Edge(aHeavy, ref pH)) Queue(DrillBtn.Heavy);
                if (aGrab != null && GameUi.Edge(aGrab, ref pG)) Queue(DrillBtn.Grab);
                if (aDodge != null && GameUi.Edge(aDodge, ref pD)) Queue(DrillBtn.Dodge);
                if (aKick != null && GameUi.Edge(aKick, ref pK)) Queue(DrillBtn.Kick);
            }
            if (inp.Guard && !prevGuard && State == Phase.Round) { Presses++; Station?.Press(DrillBtn.Guard, false, inp.StickDown, T); }
            prevGuard = inp.Guard;
            // 길게/톡 판정
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                var p = pending[i];
                p.Age += dt;
                bool held = Held(p.B);
                if (!held || p.Age >= HoldTime)
                {
                    pending.RemoveAt(i);
                    if (State == Phase.Round) { Presses++; Station?.Press(p.B, held && p.Age >= HoldTime, p.StickDown, p.T); }
                }
                else pending[i] = p;
            }
        }

        void Queue(DrillBtn b) { if (State == Phase.Round) pending.Add(new Pending { B = b, T = T, StickDown = inp.StickDown }); }

        bool Held(DrillBtn b) => b switch
        {
            DrillBtn.Light => aLight != null && aLight.IsPressed(),
            DrillBtn.Heavy => aHeavy != null && aHeavy.IsPressed(),
            DrillBtn.Grab => aGrab != null && aGrab.IsPressed(),
            DrillBtn.Dodge => aDodge != null && aDodge.IsPressed(),
            DrillBtn.Kick => aKick != null && aKick.IsPressed(),
            _ => false,
        };

        void OnDisable() { if (Running) { Log.Aborted = true; Station?.End(true); Release(); State = Phase.Idle; } }
    }
}
