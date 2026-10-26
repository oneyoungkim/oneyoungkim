// 행인1의 메인이벤트 — 체육관 장면 두뇌(docs/12_체육관_설계.md 2·3·4장): 시간대(조명·NPC 자리) · 드릴 자리 마커 → 메뉴 → DrillRunner · 드릴 카메라 프리셋 · 결과 카드 · 시뮬 몽타주
// 무대 Gym.unity 의 루트(StagePlace 와 같은 오브젝트)에 붙는다. 플레이어 리그(Zone1 바탕 장면)는 들어올 때 찾는다(SceneLoader 가 Spawn 에 놓은 뒤).
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Haengin
{
    [DisallowMultipleComponent]
    public sealed class GymManager : MonoBehaviour
    {
        public static GymManager Instance { get; private set; }

        public DrillBook Book;
        public GymClips Clips;
        public InputActionAsset Actions;
        public UiFonts Fonts;
        public GymLights Lights;
        public GymNpc Yunjun, Gojun;
        /// 출장 레슨 코치(김승연·손진수·서진수 — 오픈월드 레슨 때만 켬, docs/13 14-6)
        public List<GymNpc> Guests = new List<GymNpc>();
        /// 레슨 중인 코치 이름(드릴 큐의 '이윤준' 자리를 그 코치로 — 빈칸 = 그대로)
        [NonSerialized] public string CoachName = "";
        public GymNpc Guest(string id) => Guests.Find(g => g != null && g.Id == id);
        public List<GymNpc> Members = new List<GymNpc>();
        public List<Transform> Sockets = new List<Transform>();
        public List<DrillMarker> Markers = new List<DrillMarker>();
        public List<BagSwing> Bags = new List<BagSwing>();
        public Transform Dummy, SpeedBag, MirrorProbe;
        public TextMeshPro WallTimer;
        public CinemachineCamera DrillCam;
        public EnemyDef SparDef;
        public GameObject GojunFighterPrefab;
        public ReflectionProbe Probe;
        public MirrorPlane Mirror;
        [Tooltip("시뮬 몽타주 길이(초, 결정: 6초 3컷). 시험·봇은 줄임")] public float SimMontageSec = 6f;
        /// 시험·스모크: 시간대 강제(-1 = 게임 시계)
        public int ForcePart = -1;

        public DrillRunner Runner { get; private set; }
        public DrillHud Hud { get; private set; }
        public DrillResultCard Card { get; private set; }
        public DrillMenu Menu { get; private set; }
        public GymActors Actors { get; private set; }
        public GymPart Part { get; private set; }
        public bool Busy => (Runner != null && Runner.State != DrillRunner.Phase.Idle) || simRunning || (Menu != null && (Menu.Open || Menu.BoardOpen));
        public DrillDef Current { get; private set; }
        public bool SimRunning => simRunning;
        public string LastCoachLine { get; private set; } = "";
        public int DrillsDone { get; private set; }
        public event Action<DrillDef, DrillResult, DrillReward> DrillFinished;
        /// 오픈월드(docs/13 3-3): 드릴 문지기 — 빈칸이면 됨, 아니면 이윤준 한 줄(회원·체험·시간) · 끝난 드릴 알림(시간·카드)
        public static Func<DrillDef, string> Gate;
        public static event Action<DrillDef, DrillResult, DrillReward> Finished;

        readonly Dictionary<string, Transform> sock = new Dictionary<string, Transform>();
        string camPreset; Transform camAt; CamRig camRig; float roomRadius = 3.0f;
        Vector3 cageCam; bool cageCamOk;
        /// 사무실 안(14.2~19.5 × 10.2~13): 카메라 2.2(2장)
        public bool InOffice { get; private set; }
        bool simRunning, waitingClose;
        InputAction submit, dodge;
        bool pSub, pDodge;
        CamTuning savedIndoor;

        void Awake()
        {
            Instance = this;
            foreach (var s in Sockets) if (s != null) sock[s.name] = s;
            Runner = GetComponent<DrillRunner>() ?? gameObject.AddComponent<DrillRunner>();
            Runner.Actions = Actions;
            Runner.Finished += OnFinished;
            T Ui<T>(string n) where T : Component { var go = new GameObject(n) { layer = 5 }; go.transform.SetParent(transform, false); return go.AddComponent<T>(); }
            Hud = Ui<DrillHud>("드릴 화면"); Hud.Fonts = Fonts; Hud.Runner = Runner; Hud.WallTimer = WallTimer;
            Card = Ui<DrillResultCard>("결과 카드"); Card.Fonts = Fonts;
            Menu = Ui<DrillMenu>("드릴 메뉴"); Menu.Fonts = Fonts; Menu.Actions = Actions;
            foreach (var m in Markers) if (m != null) m.Gym = this;
            if (Actions != null) { submit = Actions.FindAction("Menu/Submit", false); dodge = Actions.FindAction("Explore/Interact", false); }
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Gate = null; Finished = null; }

        IEnumerator Start()
        {
            yield return null;
            GymState.Visits++;
            BindPlayer();
            ApplyPart(ForcePart >= 0 ? (GymPart)ForcePart : GymLights.PartOfHour(GameClock.Hour));
            if (Probe != null) Probe.RenderProbe();
            Debug.Log($"[Gym] 체육관 입장: {GymLights.Name(Part)} · 드릴 {Book?.Drills.Length ?? 0}종(플레이 가능 {Book?.PlayableCount ?? 0}) · 소켓 {sock.Count} · 마커 {Markers.Count} · {GymState.Current.Summary}");
        }

        public Transform Sock(string name) => sock.TryGetValue(name, out var t) ? t : null;

        void BindPlayer()
        {
            var motor = FindAnyObjectByType<PlayerMotor>();
            var pc = motor != null ? motor.GetComponent<PlayerCombat>() : FindAnyObjectByType<PlayerCombat>();
            Actors = new GymActors
            {
                Motor = motor, Player = pc, Input = motor != null ? motor.GetComponent<PInput>() : null,
                Visual = motor != null ? motor.transform.Find("Visual") : null,
                Yunjun = Yunjun, Gojun = Gojun, Bags = Bags, Dummy = Dummy, SpeedBag = SpeedBag,
                Mode = FindAnyObjectByType<CombatMode>(), Tuning = pc != null ? pc.T : null, Moves = pc != null ? pc.Moves : null,
                GojunFighterPrefab = GojunFighterPrefab, SparDef = SparDef, Clips = Clips,
                Say = (who, line) => { LastCoachLine = line; Hud.Say(who == "이윤준" && !string.IsNullOrEmpty(CoachName) ? CoachName : who, line); },
                Toast = s => Hud.ShowToast(s),
            };
            if (Yunjun != null && Actors.Mitt == null)
            {
                var f = Yunjun.GetComponent<Fighter>();
                if (f == null)
                {
                    f = CombatFactory.AddFighter(Yunjun.gameObject, "이윤준(미트)", 1, 9999, 1.75f, 0.30f, Actors.Tuning, Yunjun.transform.GetChild(0));
                    f.KeepStanding = true; f.StaggerMul = 0.15f; f.Body.SetYaw(Yunjun.transform.eulerAngles.y);
                }
                Actors.Mitt = f;
            }
        }

        // ───────────────────────── 시간대
        public void ApplyPart(GymPart p)
        {
            Part = p;
            if (Lights != null) Lights.Apply(p);
            foreach (var n in Members) if (n != null) n.gameObject.SetActive(false);
            if (Gojun != null) Gojun.gameObject.SetActive(false);
            if (Yunjun != null) Yunjun.gameObject.SetActive(p != GymPart.Dawn);
            switch (p)
            {
                case GymPart.Morning:
                    Put(Yunjun, "Sock_NPC_Coach_Weight", GymNpc.Routine.Sit);
                    Put(Member(0), "Sock_Drill_D05_01", GymNpc.Routine.Bag);
                    Put(Member(1), "Sock_NPC_Member_Stretch", GymNpc.Routine.Stretch);
                    Put(Member(4), "Sock_Drill_D12", GymNpc.Routine.Kettlebell);
                    break;
                case GymPart.Day:
                    Put(Yunjun, "Sock_NPC_Coach_Desk", GymNpc.Routine.Sit);
                    Put(Gojun, "Sock_NPC_Gojun_Bag", GymNpc.Routine.Bag);
                    Put(Member(2), "Sock_NPC_Member_Mirror", GymNpc.Routine.Shadow);
                    Put(Member(3), "Sock_Drill_D05_04", GymNpc.Routine.Bag);
                    Put(Member(5), "Sock_Drill_D12", GymNpc.Routine.Kettlebell);
                    break;
                case GymPart.Evening:
                    Put(Yunjun, "Sock_NPC_Coach_CageDoor", GymNpc.Routine.Watch);
                    Put(Gojun, "Sock_NPC_Gojun_Cage", GymNpc.Routine.Shadow);
                    for (int i = 0; i < 8; i++) Put(Member(i), $"Sock_NPC_Line_{i + 1:00}", i % 3 == 1 ? GymNpc.Routine.Rope : GymNpc.Routine.Shadow);
                    break;
                case GymPart.Night:
                    Put(Yunjun, "Sock_NPC_Coach_MatEdge", GymNpc.Routine.Mitts);
                    break;
            }
        }

        GymNpc Member(int i) => i < Members.Count ? Members[i] : null;

        void Put(GymNpc n, string socket, GymNpc.Routine r)
        {
            if (n == null) return;
            var s = Sock(socket);
            if (s == null) { n.gameObject.SetActive(false); return; }
            n.gameObject.SetActive(true);
            n.Place(s.position, s.forward, r);
        }

        // ───────────────────────── 드릴 자리
        public void OnMarkerUsed(DrillMarker m)
        {
            if (Busy || Book == null) return;
            var def = Book.Find(m.DrillId);
            if (def == null) return;
            if (Gate != null) { var gw = Gate(def); if (!string.IsNullOrEmpty(gw)) { Hud.Notice("이윤준", gw); StartCoroutine(HideHudLater(2.5f)); return; } }
            if (def.Id == "D14") { Menu.ShowBoard(Book, GymState.Current, null); return; }
            Menu.Show(def, GymState.Current, () => StartDrill(def.Id), () => StartCoroutine(SimCo(def)), null);
        }

        /// 드릴 직접 시작(봇 skill > 0 이면 자동 플레이). 돌려주는 것 = 시작했나
        public bool StartDrill(string id, float bot = 0f)
        {
            if (Busy || Book == null) return false;
            var def = Book.Find(id);
            if (def == null || Actors == null) return false;
            if (Gate != null) { var gw = Gate(def); if (!string.IsNullOrEmpty(gw)) { Hud.Notice("이윤준", gw); StartCoroutine(HideHudLater(2.5f)); return false; } }
            string blocked = DrillSim.DirectBlocked(def, GymState.Current, GymSave.Today);
            if (blocked.Length > 0) { Hud.Notice("이윤준", blocked); Debug.Log($"[Gym] 드릴 {id} 막힘: {blocked}"); StartCoroutine(HideHudLater(2.5f)); return false; }
            var station = DrillStations.Create(def.Id);
            if (station == null) { Debug.Log($"[Gym] 드릴 {id} 자리만(2부)"); return false; }
            Current = def;
            var s = Sock(def.Socket) ?? Sock("Sock_Drill_" + def.Id);
            Actors.Station = s != null ? s : transform;
            // 시우를 자리에, NPC 가 그 자리에 있으면 비켜 줌
            if (Actors.Motor != null && s != null)
            {
                Actors.Motor.ClearMoveInput();
                Actors.Motor.Teleport(s.position, HitResolver.Yaw(s.forward));
            }
            foreach (var n in Members) if (n != null && n.gameObject.activeSelf && s != null && (n.transform.position - s.position).sqrMagnitude < 1.5f) n.gameObject.SetActive(false);
            if (def.Id == "D04") { var mt = Sock("Sock_NPC_Coach_MatEdge"); if (Yunjun != null && mt != null) { Yunjun.gameObject.SetActive(true); Yunjun.Place(s.position + s.forward * 1.05f, -s.forward, GymNpc.Routine.Mitts); } }
            if (def.Id == "D07" || def.Id == "D10") { if (Gojun != null && s != null) { Gojun.gameObject.SetActive(true); Gojun.Place(s.position + s.forward * (def.Id == "D07" ? 2.2f : 1.0f), -s.forward, GymNpc.Routine.Watch); } }
            if (def.Id == "D11") { var cd = Sock("Sock_NPC_Coach_CageDoor"); if (Yunjun != null && cd != null) { Yunjun.gameObject.SetActive(true); Yunjun.Place(cd.position, cd.forward, GymNpc.Routine.Watch); } }
            Runner.Bot = bot;
            // QA Q33: 스파링(D11 'combat')은 바깥 카메라가 케이지 밖 윗테 높이에 걸려 화면 아래 ⅓을 검은 패드 레일이 덮었음 → 케이지 안 높은 카메라
            SetCam(def.CamPreset == "combat" && def.Id == "D11" ? "cage" : def.CamPreset, Actors.Station);
            if (Mirror != null) Mirror.Set(def.CamPreset == "mirror");
            Hud.Show(true);
            if (Actors.Input != null && Actors.Player != null) { }
            Runner.Begin(def, station, Actors);
            StartCoroutine(ClearSight());
            return true;
        }

        /// QA Q12: 드릴 카메라와 시우 사이에 선 관원(줄넘기 D01 — 수건 두른 관원이 화면 절반을 가림)은 그 드릴 동안 비켜 줌.
        /// 카메라가 드릴 자리로 넘어오는(블렌드) 동안·관원이 움직여 끼어드는 것까지 드릴 내내 0.25초마다 봄(드릴 카메라 자리 기준)
        IEnumerator ClearSight()
        {
            yield return null;
            while (Runner != null && Runner.State != DrillRunner.Phase.Idle)
            {
                var cam = DrillCam != null && DrillCam.isActiveAndEnabled ? DrillCam.transform : (Camera.main != null ? Camera.main.transform : null);
                if (cam != null && Actors != null && Actors.Motor != null)
                {
                    foreach (var ct in new[] { cam, Camera.main != null ? Camera.main.transform : cam })
                    {
                        Vector3 a = ct.position, b = Actors.Motor.Position + Vector3.up * 1.2f;
                        var fwd = ct.forward; fwd.y = 0f; fwd.Normalize();
                        foreach (var n in Members)
                        {
                            if (n == null || !n.gameObject.activeSelf) continue;
                            var p = n.transform.position + Vector3.up * 1.0f;
                            var ab = b - a; float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
                            var q = a + ab * t;
                            bool between = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(q.x, q.z)) < 0.9f && t > 0.02f && t < 0.98f;
                            var ap = p - a; ap.y = 0f;
                            bool nearCam = ap.magnitude < 1.4f || (ap.magnitude < 3.0f && Vector3.Angle(fwd, ap) < 70f);      // 카메라 옆·앞 관원 몸·뻗은 팔이 화면 가장자리를 덮던 것(D04 — 섀도복싱 관원 팔, 카메라에서 2.8m·63°)
                            if (between || nearCam) { n.gameObject.SetActive(false); SightHidden++; }
                        }
                    }
                }
                for (int f = 0; f < 15; f++) yield return null;
            }
        }
        public int SightHidden { get; private set; }

        IEnumerator HideHudLater(float s) { yield return new WaitForSecondsRealtime(s); if (Runner.State == DrillRunner.Phase.Idle) Hud.Show(false); }

        void OnFinished(DrillResult r, DrillReward w)
        {
            DrillsDone++;
            var ds = GymState.Current.Of(r.Def.Id);
            bool unlock = r.Grade == Grade.S && w.FirstS && r.Def.SimAllowed;
            string line = r.Aborted ? "" : r.Grade == Grade.S ? r.Def.CueS : r.Grade == Grade.D ? "…" : r.Grade == Grade.A ? "나쁘지 않아." : r.Grade == Grade.B ? "0.3초 늦어. 다시." : "처음부터.";
            if (r.Concussion) line = "오늘은 끝. 내일까지 머리 쓰는 건 없다.";
            LastCoachLine = line;
            Card.Show(r, w, line, unlock);
            Hud.ClearHint();      // QA Q35: 결과 카드가 뜬 그 프레임에도 드릴 안내가 '닫기' 안내와 겹치지 않게
            if (r.Grade == Grade.S && !string.IsNullOrEmpty(r.Def.CueGojun) && Gojun != null && Gojun.gameObject.activeSelf) Hud.Say("고준", r.Def.CueGojun);
            waitingClose = true;
            foreach (var m in Markers) if (m != null) m.Refresh();
            DrillFinished?.Invoke(r.Def, r, w);
            Finished?.Invoke(r.Def, r, w);
        }

        /// 결과 카드 닫기(× · Enter · 봇)
        public void CloseCard()
        {
            if (!waitingClose) return;
            waitingClose = false;
            Card.Hide();
            Hud.Show(false);
            Runner.Close();
            SetCam(null, null);
            if (Mirror != null) Mirror.Set(false);
            Current = null;
            if (!simRunning) ApplyPart(Part);        // NPC 자리 복구
        }

        void Update()
        {
            // 드릴 카메라 ±15°(오른스틱·마우스)
            if (camPreset != null && camAt != null && DrillCam != null && DrillCam.Priority.Value >= 30) Pose(camPreset, camAt, Runner != null ? Runner.Nudge * 15f : 0f);
            // 사무실 카메라
            if (Actors != null && Actors.Motor != null)
            {
                if (camRig == null) camRig = FindAnyObjectByType<CamRig>();
                var l = transform.InverseTransformPoint(Actors.Motor.Position);
                bool office = l.x > 14.2f && l.x < 19.5f && l.z > 10.2f && l.z < 13f;
                if (office != InOffice && camRig != null && camRig.Tuning != null)
                {
                    InOffice = office;
                    camRig.Tuning.radius = office ? 2.2f : roomRadius; camRig.Tuning.radiusRun = camRig.Tuning.radius + 0.2f;
                }
            }
            if (!waitingClose || Card.ShownFor < 0.6f) return;
            if (Runner.Bot > 0f && Card.ShownFor > 2.5f) { CloseCard(); return; }
            bool s = submit != null && submit.enabled && GameUi.Edge(submit, ref pSub);
            bool d = dodge != null && dodge.enabled && GameUi.Edge(dodge, ref pDodge);
            var kb = Keyboard.current; var gp = Gamepad.current;
            bool raw = (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) || (gp != null && gp.buttonSouth.wasPressedThisFrame);
            if (s || d || raw) CloseCard();
        }

        // ───────────────────────── 시뮬(4-5): 몽타주 3컷 → 보상 75% → 카드('시뮬')
        public IEnumerator SimCo(DrillDef def)
        {
            if (Busy) yield break;
            string why = DrillSim.Blocked(def, GymState.Current, GymSave.Today);
            if (why.Length > 0) { Hud.Notice("이윤준", why); yield return new WaitForSecondsRealtime(2f); Hud.Show(false); yield break; }
            simRunning = true; Current = def;
            var s = Sock(def.Socket) ?? transform;
            if (Actors.Motor != null) { Actors.Motor.ClearMoveInput(); Actors.Motor.Teleport(s.position, HitResolver.Yaw(s.forward)); }
            if (Actors.Input != null) Actors.Input.enabled = false;
            GameState.InputLocked = true;
            Hud.Show(true);
            var me = Actors.Player != null ? Actors.Player.Me : null;
            string[] cuts = { "mirror", "side", "shoulder" };
            string[] words = { "준비", "중반", "마무리" };
            float per = Mathf.Max(0.3f, SimMontageSec / 3f);
            int sweatShown = 0;
            var w = DrillRewards.Compute(def, Grade.S, true, GymState.Current, false);
            for (int c = 0; c < 3; c++)
            {
                SetCam(cuts[c], s);
                if (Mirror != null) Mirror.Set(cuts[c] == "mirror");
                Hud.Say("시뮬", $"{def.NameKr} — {words[c]}");
                float t = 0f;
                while (t < per)
                {
                    t += UiKit.RealDt;
                    if (me != null && me.State == Fighter.Phase.Free && Actors.Moves != null && def.Category == DrillCategory.Striking && Mathf.Repeat(t, 0.7f) < UiKit.RealDt) me.StartAttack(c % 2 == 0 ? Actors.Moves.Jab : Actors.Moves.Cross, null);
                    else if (me != null && def.Category != DrillCategory.Striking)
                    {
                        string st = def.Id == "D01" || def.Id == "D02" ? "JumpRope" : def.Id == "D12" || def.Id == "D13" ? "Kettlebell" : def.Id == "D07" ? "Burpee" : "Stance";
                        me.ScriptAnim = Actors.HasClip(st) ? st : "Stance"; me.ScriptRate = 1f;
                    }
                    int target = Mathf.RoundToInt(w.Sweat * (c * per + t) / (3f * per));
                    if (target != sweatShown) { sweatShown = target; Hud.ShowToast($"땀 {sweatShown}"); }
                    yield return null;
                }
            }
            if (me != null) me.ScriptAnim = null;
            var reward = DrillSim.Run(def, GymState.Current, GymSave.Today);
            Debug.Log($"[Gym] 시뮬 {def.Id}: {reward} · 오늘 남은 {GymState.Current.SimLeft(GymSave.Today)} · 검정까지 {DrillSim.CertEvery - GymState.Current.Of(def.Id).SimRunsSinceCert}");
            Card.ShowSim(def, reward, GymState.Current.SimLeft(GymSave.Today));
            GameState.InputLocked = false;
            if (Actors.Input != null) Actors.Input.enabled = true;
            simRunning = false;
            waitingClose = true;
            foreach (var m in Markers) if (m != null) m.Refresh();
            DrillFinished?.Invoke(def, new DrillResult { Def = def, Grade = Grade.S }, reward);
            Finished?.Invoke(def, new DrillResult { Def = def, Grade = Grade.S }, reward);
        }

        // ───────────────────────── 드릴 카메라(2장 표): 고정 프리셋, 드릴 중 플레이어 조작 끔
        public void SetCam(string preset, Transform at)
        {
            if (DrillCam == null) return;
            if (string.IsNullOrEmpty(preset) || at == null || preset == "combat") { DrillCam.Priority = 0; camPreset = null; camAt = null; return; }
            camPreset = preset; camAt = at; cageCamOk = false;
            Pose(preset, at, 0f);
            DrillCam.Lens.FieldOfView = preset == "mitt" ? 52f : 50f;
            DrillCam.Priority = 30;
        }

        /// 프리셋 자세(yawOff = 보는 점 둘레로 카메라를 돌리는 각)
        void Pose(string preset, Transform at, float yawOff)
        {
            var f = new Vector3(at.forward.x, 0f, at.forward.z).normalized; var r = Vector3.Cross(Vector3.up, f); var p = at.position;
            Vector3 pos, look;
            switch (preset)
            {
                case "mirror": pos = p + f * 2.6f + r * 1.5f + Vector3.up * 1.55f; look = p + Vector3.up * 1.05f; break;
                case "mitt": pos = p + r * 3.1f + f * 0.55f + Vector3.up * 1.5f; look = p + f * 0.6f + Vector3.up * 1.1f; break;   // 시우 주먹과 이윤준 미트가 한 화면
                case "side": pos = p + r * 3.0f + f * 0.8f + Vector3.up * 1.55f; look = p + f * 0.5f + Vector3.up * 1.05f; break;
                case "shoulder": pos = p - f * 2.2f + r * 0.7f + Vector3.up * 1.75f; look = p + f * 1.1f + Vector3.up * 1.15f; break;
                case "low": pos = p + r * 3.0f + f * 0.4f + Vector3.up * 0.9f; look = p + f * 0.4f + Vector3.up * 0.55f; break;
                case "cage":
                    {
                        // 케이지(tools/env/gym/build_gym.py CAGE_C (14.5, 6.0) · 외접 반지름 3.1 · 바닥 0.25) 안, 두 사람 옆에서 2.4m 높이로 내려다봄
                        var cc = transform.TransformPoint(new Vector3(14.5f, 0.25f, 6.0f));
                        var me = Actors != null && Actors.Motor != null ? Actors.Motor.Position : p;
                        var foe = Runner != null && Runner.Station is SparStation ss && ss.Foe != null ? ss.Foe.transform.position : me + f * 1.6f;
                        var mid = (me + foe) * 0.5f;
                        var axis = foe - me; axis.y = 0f; if (axis.sqrMagnitude < 0.04f) axis = f;
                        var side = Vector3.Cross(Vector3.up, axis.normalized);
                        var toC = cc - mid; toC.y = 0f;
                        if (Vector3.Dot(side, toC) < 0f) side = -side;          // 케이지 가운데 쪽 옆(공간이 넓은 쪽)
                        var want = mid + side * 2.6f; want.y = cc.y + 2.3f;
                        var h = want - cc; h.y = 0f;
                        if (h.magnitude > 2.45f) want = cc + h.normalized * 2.45f + Vector3.up * 2.3f;   // 철망 안(내접 반지름 2.86)
                        cageCam = cageCamOk ? Vector3.Lerp(cageCam, want, 1f - Mathf.Exp(-2.5f * Time.unscaledDeltaTime)) : want; cageCamOk = true;
                        pos = cageCam; look = mid + Vector3.up * 1.0f;
                        break;
                    }
                default: pos = p + f * 2.8f + r * 1.2f + Vector3.up * 1.55f; look = p + Vector3.up * 1.0f; break;
            }
            // 벽 안으로 들어가지 않게(실내 20×13): 방 안쪽으로 당김
            if (Mathf.Abs(yawOff) > 0.01f) pos = look + Quaternion.Euler(0f, yawOff, 0f) * (pos - look);
            var local = transform.InverseTransformPoint(pos);
            local.x = Mathf.Clamp(local.x, 0.4f, 19.6f); local.z = Mathf.Clamp(local.z, 0.4f, 12.6f); local.y = Mathf.Clamp(local.y, 0.5f, 2.6f);
            pos = transform.TransformPoint(local);
            DrillCam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos, Vector3.up));
        }

        // ───────────────────────── 시험·스모크 도우미
        /// 드릴을 봇으로 끝까지(결과 카드 닫기까지). skill 1 = S
        public IEnumerator PlayBot(string id, float skill, int maxFrames = 60 * 400)
        {
            if (!StartDrill(id, skill)) yield break;
            int n = 0;
            while (Runner.State != DrillRunner.Phase.Result && n++ < maxFrames) yield return null;
            while (waitingClose && n++ < maxFrames) yield return null;
        }
    }
}
