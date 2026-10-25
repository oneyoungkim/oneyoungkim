// 행인1의 메인이벤트 — 1화 새벽 배달 코스(docs/09 2-6 '코스', 8단계): 국밥집 앞 → 성균관로 서쪽 → 후문 경비실(정차·건네기) → 와룡공원길(7°)
//   → 와룡 오르막 자전거길(13~14°, 일어서기) → 꼭대기 집(정차·건네기·지각 대사) → 같은 길 내리막 → 성균관로 동쪽 끝(명륜1가 방면 차단물). 약 270m · 보통 110초.
// 이야기 장면 1-2 · 1-3 · 1-4 가 다리(leg) 하나씩: 0 = 경비실 건네기까지 · 1 = 꼭대기 집 도착까지 · 2 = 건네기 → 지각 대사 → 끝까지.
// 태오(PathRunner): 와룡 오르막 입구를 시우가 지나면 12m 뒤에서 나타나 3.4 m/s 로 올라가며 앞지름('…' 말풍선 0.8초, 측면 트래킹 카메라),
//   꼭대기(꼭대기 집 북쪽)에서 숨 고르기 → 시우가 건네고 내려가면 2.0 m/s 로 뒤따라 내려가다 시우에게 앞질림(슬로 0.5초 "쉬이익.").
// 지각: 코스 시계(게임 05:35 시작, 실제 1초 = 게임 3초) — 꼭대기 집 약속 05:36 에 도착 시각을 빼 N분(내림, 1화는 1 이상) → "어이. N분 늦었다."
//   국물 70 이상 "국은 안 식었네. 됐다." · 아니면 "…식었다."
// 봇(Bot): 경로 가운데선을 따라 페달·조향·박자(0.55초)·정차·건네기를 사람 손과 같은 길(BikeMotor 입력)로.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Haengin
{
    [DefaultExecutionOrder(-30), DisallowMultipleComponent]
    public sealed class BikeCourse : MonoBehaviour
    {
        [Serializable]
        public sealed class Stop
        {
            public string Name, Caller;
            public Vector3 Pos;
            public float Radius = 2.4f;
            /// 약속 시각(게임 초, 0 = 약속 없음)
            public int Appt;
            [NonSerialized] public bool Done;
            [NonSerialized] public float ArriveClock;
        }

        public static BikeCourse Current { get; private set; }

        [Header("코스(BikeSetup 이 zone1.json 에서 채움)")]
        public Vector3[] Route = new Vector3[0];
        public int[] RouteStop = new int[0];          // Route 점 번호 → 정차 번호(−1 = 아님)
        public Stop[] Stops = new Stop[0];
        public Vector3 Start; public float StartYaw;
        public Vector3 End; public float EndRadius = 3.5f;
        public Vector3 ClimbStart;                      // 와룡 오르막 입구(태오 나타나는 기준)
        public int StartClock = 5 * 3600 + 35 * 60;
        public float GameRate = 3f;
        [Header("태오")]
        public PathRunner Taeo;
        public float TaeoBehind = 12f, TaeoUp = 3.4f, TaeoDown = 2.0f, TaeoRestS = -1f;
        [Header("부품")]
        public BikeMotor Motor;
        public BikeInput In;
        public SoupGauge Soup;
        public BikeVisual Bike;
        public BikeCam Cam;
        public BikeHud Hud;
        public UnityEngine.InputSystem.InputActionAsset Actions;

        public bool Bot;
        public bool Riding { get; private set; }
        public int Leg { get; private set; } = -1;
        public float Elapsed { get; private set; }
        public float Clock => StartClock + Elapsed * GameRate;
        public int UpPasses { get; private set; }
        public int DownPasses { get; private set; }
        public int LateMin { get; private set; } = -1;
        public float LateSec { get; private set; }
        public int SideShots { get; private set; }
        public readonly List<string> Lines = new List<string>();
        public readonly List<string> Log = new List<string>();
        public event Action<string> Said;

        float taeoState, restT; // 0 숨음 · 1 오르막 · 2 쉼 · 3 내리막 · 4 끝
        int taeoPhase;
        float sideLeft, sayLeft, lastSign;
        int botIdx;
        float botBeat, botStopT;
        BikeRider rider;
        Outfit outfit;
        SubtitleUi sub;
        InnerVoiceUi inner;
        readonly Queue<string> thoughts = new Queue<string>();
        float thinkLeft;
        bool saidHip, saidStand, saidTaeo;
        /// 시우 속마음(원고 1화 문장 — 주행 중 사건마다, 2.6초씩 차례로)
        public readonly List<string> Thoughts = new List<string>();

        public void Think(string line) { thoughts.Enqueue(line); Thoughts.Add(line); }

        void OnEnable() { Current = this; }
        void OnDisable() { if (Current == this) Current = null; }

        string ClockText(float sec) { int s = Mathf.FloorToInt(sec); return $"{s / 3600:00}:{s / 60 % 60:00}:{s % 60:00}"; }

        // ───────────────────────── 시작·끝
        /// 타기 시작(자리 = 다리의 시작점). leg 0 = 국밥집 앞, 1 = 경비실, 2 = 꼭대기 집
        public void Begin(int leg, bool tutorial = true)
        {
            Current = this;
            if (Motor == null) return;
            outfit = Motor.GetComponent<Outfit>();
            outfit?.Set(Outfit.Delivery);
            Vector3 p = Start; float yaw = StartYaw;
            if (leg >= 1 && Stops.Length > leg - 1) { p = Stops[leg - 1].Pos; yaw = YawAlong(Index(Stops[leg - 1].Pos)); }
            if (Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out var hit, 10f, Layers.Solid, QueryTriggerInteraction.Ignore)) p.y = hit.point.y;
            Motor.Soup = Soup;
            Motor.NoStand = false;
            Motor.Mount(p, yaw);
            Soup?.ResetTo(SoupGauge.Max, tutorial ? 30f : 0f, "국물");
            var visualRoot = Motor.transform.Find("Visual");
            if (Bike != null)
            {
                Bike.gameObject.SetActive(true);
                Bike.transform.SetParent(visualRoot != null ? visualRoot : Motor.transform, false);
                Bike.transform.localPosition = Vector3.zero; Bike.transform.localRotation = Quaternion.identity;
                Bike.Bind(Motor, visualRoot);
            }
            rider = outfit != null && outfit.Model != null ? outfit.Model.GetComponent<BikeRider>() : Motor.GetComponentInChildren<BikeRider>();
            rider?.Begin(Motor, Bike);
            if (Cam != null) { Cam.Bike = Motor; Cam.Soup = Soup; Cam.Side = false; Cam.SetLive(true); }
            if (In != null) { In.Motor = Motor; In.Cam = Cam; In.Suppressed = Bot; }
            InputMaps.Use(Actions, InputMaps.Bike);
            GameUi.HintOverride = Hint;
            Hud?.Show(true);
            if (Hud != null) { Hud.Motor = Motor; Hud.Soup = Soup; }
            Motor.Handover -= OnHandover; Motor.Handover += OnHandover;
            foreach (var s in Stops) { if (leg == 0) { s.Done = false; s.ArriveClock = 0f; } }
            if (leg <= 1) { UpPasses = DownPasses = 0; LateMin = -1; }
            if (Taeo != null && leg <= 1) { Taeo.gameObject.SetActive(false); taeoState = 0; }
            sub = CutCtx.Sub != null ? CutCtx.Sub : FindAnyObjectByType<SubtitleUi>(FindObjectsInactive.Include);
            inner = CutCtx.Inner != null ? CutCtx.Inner : FindAnyObjectByType<InnerVoiceUi>(FindObjectsInactive.Include);
            botIdx = Index(p); botBeat = 0f; botStopT = 0f;
            if (leg == 0)
            {
                Elapsed = 0f; Lines.Clear(); Log.Clear(); SideShots = 0; Thoughts.Clear(); thoughts.Clear(); saidHip = saidStand = saidTaeo = false;
                Think("처음 백 미터는 앉아서 밟는다.");
                Think("배달통 속에서 국물이 출렁인다.\n국물 흘리면 죽는다.");
            }
            Riding = true;
            Leg = leg;
            Log.Add($"타기 {leg} · {p} · {ClockText(Clock)}");
        }

        /// 탄 채로 다음 장면(다리)으로 — 입력 맵·조작 안내만 다시
        public void Resume()
        {
            InputMaps.Use(Actions, InputMaps.Bike);
            GameUi.HintOverride = Hint;
            if (In != null) In.Suppressed = Bot;
            Hud?.Show(true);
        }

        const string Hint = "페달 R2·W · 브레이크 L2·S · 방향 왼스틱·A D · 오르막 □ ○ 번갈아(J K) · 건네기 ×·E(멈춰서)";

        public void Finish()
        {
            if (!Riding) return;
            Riding = false;
            Motor.Handover -= OnHandover;
            rider?.End();
            Motor.Dismount();
            if (Bike != null) { Bike.transform.SetParent(transform, false); Bike.gameObject.SetActive(false); }
            var vr = Motor.transform.Find("Visual"); if (vr != null) vr.localRotation = Quaternion.identity;
            Cam?.SetLive(false);
            Hud?.Show(false);
            sub?.Hide();
            inner?.Hide(); thoughts.Clear(); thinkLeft = 0f;
            if (Taeo != null) Taeo.gameObject.SetActive(false);
            InputMaps.Use(Actions, InputMaps.Explore);
            GameUi.HintOverride = null;
            Leg = -1;
            Log.Add($"끝 · {ClockText(Clock)} · {Elapsed:F1}s");
        }

        // ───────────────────────── 다리(이야기 장면 1-2 · 1-3 · 1-4)
        public static int LegOf(string sceneId) => sceneId == "1-2" ? 0 : sceneId == "1-3" ? 1 : sceneId == "1-4" ? 2 : -1;

        public IEnumerator RunLeg(int leg, Func<bool> skip)
        {
            if (!Riding || Leg > leg) Begin(leg);
            else Resume();
            Leg = leg;
            float t0 = Elapsed;
            while (true)
            {
                if (skip != null && skip()) { Log.Add($"다리 {leg} 건너뜀"); SkipTo(leg); break; }
                if (leg == 0 && Stops.Length > 0 && Stops[0].Done) break;
                if (leg == 1 && Stops.Length > 1 && Near(Stops[1]) && Motor.Stopped) break;
                if (leg == 2 && (Motor.Position - End).sqrMagnitude < EndRadius * EndRadius) break;
                yield return null;
            }
            Log.Add($"다리 {leg} 끝 · {Elapsed - t0:F1}s · 국물 {Soup?.Value:F0}");
            if (leg == 2)
            {
                Think("어이. 국밥. 자전거.\n그중에 반시우는 없다.");
                if (!Bot) { float w = 0f; while (w < 2.8f && (skip == null || !skip())) { w += Time.deltaTime; yield return null; } }
                yield return Wait(0.4f);
                Finish();
            }
        }

        /// 봇·건너뛰기: 다리 끝 상태로 바로(정차 처리·태오 사건은 결과만)
        void SkipTo(int leg)
        {
            if (leg == 0 && Stops.Length > 0) { Stops[0].Done = true; Motor.Teleport(G(Stops[0].Pos), YawAlong(Index(Stops[0].Pos))); }
            if (leg == 1 && Stops.Length > 1) { Motor.Teleport(G(Stops[1].Pos), YawAlong(Index(Stops[1].Pos))); }
            if (leg == 2) { if (Stops.Length > 1) Stops[1].Done = true; Motor.Teleport(G(End), YawAlong(Route.Length - 1)); }
            botIdx = Index(Motor.Position);
        }

        public IEnumerator RunAll(Func<bool> skip = null)
        {
            Begin(0);
            for (int l = 0; l < 3; l++) yield return RunLeg(l, skip);
        }

        IEnumerator Wait(float s) { float t = 0f; while (t < s) { t += Time.deltaTime; yield return null; } }

        Vector3 G(Vector3 p) { if (Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out var h, 10f, Layers.Solid, QueryTriggerInteraction.Ignore)) p.y = h.point.y; return p; }

        bool Near(Stop s) => (Flat(Motor.Position) - Flat(s.Pos)).sqrMagnitude < s.Radius * s.Radius;
        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        // ───────────────────────── 정차·건네기
        void OnHandover()
        {
            for (int i = 0; i < Stops.Length; i++)
            {
                var s = Stops[i];
                if (s.Done || !Near(s)) continue;
                if (i == 1 && Leg < 2) return;          // 꼭대기 집은 1-4 에서 건넴
                s.Done = true;
                s.ArriveClock = Clock;
                Log.Add($"건네기 {s.Name} · {ClockText(Clock)}");
                if (i == 0) { Say(s.Caller, "국밥."); Think("후문 경비실 아저씨는\n나를 '국밥'이라고 부른다."); }
                if (i == 1 && s.Appt > 0)
                {
                    LateSec = Clock - s.Appt;
                    LateMin = Mathf.Max(1, Mathf.FloorToInt(LateSec / 60f));
                    StartCoroutine(Talk(new[] { (s.Caller, $"어이. {LateMin}분 늦었다."), (s.Caller, Soup != null && Soup.Value >= 70f ? "국은 안 식었네. 됐다." : "…식었다.") }));
                    // (태오는 시우가 꼭대기 집에 닿은 뒤 1초 쉬고 먼저 내려간다 — Taeoes)
                }
                return;
            }
        }

        IEnumerator Talk((string who, string line)[] ls)
        {
            foreach (var (w, l) in ls) { Say(w, l); yield return Wait(1.8f); }
            sub?.Hide();
        }

        void Say(string who, string line)
        {
            Lines.Add($"{who}: {line}");
            Said?.Invoke(line);
            sub?.Show(who, line);
            sayLeft = 1.8f;
        }

        // ───────────────────────── 매 프레임
        void Update()
        {
            if (!Riding || Motor == null) return;
            float dt = Time.deltaTime;
            Elapsed += dt;
            if (GameClock.Running) GameClock.Tick(dt);
            if (sayLeft > 0f) { sayLeft -= dt; if (sayLeft <= 0f) sub?.Hide(); }
            // 속마음: 자세가 바뀌는 첫 순간(원고 '처음 백 미터 · 다음 백 미터 · 마지막 오르막')
            if (!saidHip && Motor.Posture == BikePosture.Hip && Leg >= 1) { saidHip = true; Think("다음 백 미터는 엉덩이를 들고 밟는다."); }
            if (!saidStand && Motor.Posture == BikePosture.Stand) { saidStand = true; Think("마지막 오르막은 일어선 채로\n온몸으로 밟는다."); Think("내리면 편하다. 나는 안 내린다.\n내리면, 진 거다."); }
            if (inner != null)
            {
                if (thinkLeft > 0f) { thinkLeft -= dt; if (thinkLeft <= 0f) inner.Hide(); }
                if (thinkLeft <= 0f && thoughts.Count > 0) { inner.Show(thoughts.Dequeue().Replace("\\n", "\n")); thinkLeft = 2.6f; }
            }
            // 정차 안내
            bool prompt = false;
            for (int i = 0; i < Stops.Length; i++)
                if (!Stops[i].Done && Near(Stops[i]) && Motor.Stopped && !(i == 1 && Leg < 2)) prompt = true;
            if (Hud != null) Hud.PromptOn = prompt;
            Taeoes(dt);
            if (Bot) DriveBot(dt);
        }

        void Taeoes(float dt)
        {
            if (Taeo == null) return;
            float sS = Taeo.Project(Motor.Position);
            if (taeoState == 0)
            {
                // 와룡 오르막 입구를 시우가 지나면 12m 뒤에 나타남
                float sClimb = Taeo.Project(ClimbStart);
                if (Leg <= 1 && sS >= sClimb - 0.5f && (Flat(Motor.Position) - Flat(ClimbStart)).magnitude < 6f)
                {
                    Taeo.gameObject.SetActive(true);
                    Taeo.Place(Mathf.Max(0f, sS - TaeoBehind));
                    Taeo.Speed = TaeoUp;
                    taeoState = 1;
                    lastSign = Mathf.Sign(Taeo.S - sS);
                    Log.Add($"태오 나타남 · 시우 s {sS:F1} · 태오 s {Taeo.S:F1}");
                    if (!saidTaeo) { saidTaeo = true; Think("뒤에서 발소리가 올라왔다."); }
                }
                return;
            }
            float sign = Mathf.Sign(Taeo.S - sS);
            if (taeoState == 1)
            {
                // 측면 트래킹: 태오가 6m 뒤 ~ 앞지르고 2.5초
                if (Cam != null)
                {
                    bool want = (Taeo.S - sS) > -6f && sideLeft >= 0f;
                    if (want && !Cam.Side) { Cam.Side = true; SideShots++; Taeo.At(sS, out var d); Cam.SideRight = new Vector3(d.z, 0f, -d.x); sideLeft = 0f; }
                }
                if (sign > 0f && lastSign < 0f)
                {
                    UpPasses++;
                    Taeo.Say("…", 0.8f);
                    Think("강태오. 오르막에선 저쪽이 나를 앞지른다.");
                    Think("인사는 안 한다.\n나는 저 녀석 이름을 안다.");
                    Log.Add($"태오 앞지름(오르막) · {Elapsed:F1}s");
                    sideLeft = 0.001f;
                }
                if (sideLeft > 0f) { sideLeft += dt; if (sideLeft > 2.5f && Cam != null) { Cam.Side = false; sideLeft = -1f; } }
                if (Taeo.AtEnd) { taeoState = 2; Taeo.Speed = 0f; restT = 0f; if (Cam != null && Cam.Side) Cam.Side = false; Log.Add("태오 꼭대기 숨 고르기"); }
            }
            else if (taeoState == 2)
            {
                // 숨 고르기: 시우가 꼭대기 집에 닿고 1초 뒤 먼저 내려간다(시우가 건네고 내려오며 앞지름 — 원고 '성곽 꼭대기에서 숨을 고르던 덩치 옆을 바람처럼')
                if (Stops.Length > 1 && Near(Stops[1])) restT += dt;
                if (restT > 1.0f) { taeoState = 3; Taeo.Speed = -TaeoDown; Log.Add($"태오 내려감 · {Elapsed:F1}s"); }
            }
            else if (taeoState == 3)
            {
                // 태오가 아래(진행 거리 작음)에 있다가 시우가 더 아래로 = 내리막에서 시우가 앞지름
                if (sign > 0f && lastSign < 0f && Motor.Speed > 0.5f && Stops.Length > 1 && Stops[1].Done)
                {
                    DownPasses++;
                    TimeFx.Slow(0.5f, 0.35f);
                    Say("", "쉬이익.");
                    Think("내리막에선 내가 저쪽을 앞지른다.\n하루 두 번. 1년째 1대1.");
                    Log.Add($"시우 앞지름(내리막) · {Elapsed:F1}s");
                    taeoState = 4;
                }
            }
            else if (taeoState == 4 && Taeo.S <= 0.01f) { Taeo.gameObject.SetActive(false); }
            lastSign = sign;
        }

        // ───────────────────────── 경로
        public int Index(Vector3 p)
        {
            int best = 0; float bd = float.MaxValue;
            for (int i = 0; i < Route.Length; i++) { float d = (Flat(Route[i]) - Flat(p)).sqrMagnitude; if (d < bd) { bd = d; best = i; } }
            return best;
        }

        float YawAlong(int i)
        {
            if (Route.Length < 2) return StartYaw;
            var a = Route[Mathf.Clamp(i, 0, Route.Length - 2)]; var b = Route[Mathf.Clamp(i, 0, Route.Length - 2) + 1];
            var d = Flat(b - a);
            return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        }

        // ───────────────────────── 봇(사람 손과 같은 길: BikeMotor 입력)
        void DriveBot(float dt)
        {
            if (Route.Length < 2) return;
            var p = Flat(Motor.Position);
            // 지난 점은 넘긴다(아직 안 건넨 정차 점에서는 멈춰 기다림)
            while (botIdx < Route.Length - 1)
            {
                int st = botIdx < RouteStop.Length ? RouteStop[botIdx] : -1;
                if (st >= 0 && st < Stops.Length && !Stops[st].Done) break;
                if ((Flat(Route[botIdx]) - p).magnitude < 2.2f) botIdx++; else break;
            }
            int stop = botIdx < RouteStop.Length ? RouteStop[botIdx] : -1;
            if (stop >= Stops.Length || (stop >= 0 && Stops[stop].Done)) stop = -1;
            // 앞을 본다: 지금 점부터 2.5m 넘게 떨어진 첫 점(안 건넨 정차 점은 넘지 않음)
            int j = botIdx;
            while (stop < 0 && j < Route.Length - 1 && (Flat(Route[j]) - p).magnitude < 2.5f)
            {
                j++;
                int sj = j < RouteStop.Length ? RouteStop[j] : -1;
                if (sj >= 0 && sj < Stops.Length && !Stops[sj].Done) break;
            }
            var look = stop >= 0 ? Flat(Stops[stop].Pos) : Flat(Route[j]);
            var to = look - p;
            float want = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            float err = Mathf.DeltaAngle(Motor.Yaw, want);
            // 옆 가속 3 m/s² 안(국물): 빠를수록 덜 꺾고, 크게 꺾어야 하면 페달을 놓는다
            float turnRad = Mathf.Lerp(Motor.TurnLow, Motor.TurnHigh, Mathf.Clamp01(Motor.Speed / Motor.SeatMax)) * Mathf.Deg2Rad;
            float steerMax = Motor.Speed > 0.5f ? Mathf.Clamp01(2.6f / (Motor.Speed * turnRad)) : 1f;
            float steer = Mathf.Clamp(err / 25f, -steerMax, steerMax);
            float pedal = 1f, brake = 0f;
            float distStop = stop >= 0 ? (Flat(Stops[stop].Pos) - p).magnitude : 99f;
            if (stop >= 0 && !Stops[stop].Done)
            {
                // 정차: 4m 안에서 속도를 거리만큼 줄이고 멈추면 건네기
                float vmax = Mathf.Max(0f, distStop - 0.6f) * 1.2f;
                if (distStop < 1.3f) vmax = 0f;
                pedal = Motor.Speed < vmax - 0.2f ? 1f : 0f;
                brake = Motor.Speed > vmax + 0.1f ? 0.6f : 0f;      // 급브레이크(0.8 넘게) 없이
                if (Motor.Stopped && distStop < Stops[stop].Radius)
                {
                    botStopT += dt;
                    bool can = !(stop == 1 && Leg < 2);
                    if (can && botStopT > 0.4f) { Motor.PressHandover(); botStopT = -0.6f; }
                }
            }
            else
            {
                botStopT = 0f;
                if (Mathf.Abs(err) > 60f) { pedal = 0.35f; }
                if (Mathf.Abs(err) > 35f && Motor.Speed > 2.5f) { pedal = 0f; brake = 0.4f; }
                // 내리막: 5.5 m/s 넘지 않게(국물), 급브레이크 없이
                if (Motor.SlopeDeg < -3f && Motor.Speed > 5.2f) { pedal = 0f; brake = 0.5f; }
            }
            Motor.SetInput(steer, pedal, brake);
            // 일어서기 박자(0.55초)
            if (Motor.SlopeDeg > Motor.StandFrom && !Motor.NoStand && (stop < 0 || distStop > 2.5f))
            {
                botBeat += dt;
                if (botBeat >= 0.55f) { botBeat = 0f; Motor.PressBeat(!Motor.LastBeatRight); }
            }
        }
    }
}
