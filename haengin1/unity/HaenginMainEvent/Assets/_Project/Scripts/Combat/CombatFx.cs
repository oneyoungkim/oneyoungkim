// 행인1의 메인이벤트 — 타격 이펙트·의성어·화면 번쩍·쇼크 컷·효과음 (docs/08_M2_전투_설계.md 5-1·5-5·5-7, 11장 11단계)
// 시안 FX.inkHit/inkSlam/word 를 그대로: 포인트 색 튀김 · 먹 붓 획 · 집중선(중 이상) · 흰 섬광 별 · 흰 조각 4+3p · 먹 방울 2+2p / 다운 착지 = 바닥 붓 고리 + 집중선 + 만화 먼지 9 + 방울 8.
// 갱신은 실제 시간 × (히트스톱 0.15 / 슬로 0.45 / 1)(5-2). 의성어는 0.12초 안에 또 나오면 생략. 화면 번쩍 = 강 0.32 · 기세 0.40(0.16초),
// 쇼크 컷 = 전투 첫 타격·기세 마무리에 2프레임(전역 _HaenginShock → ShockCut 렌더러 기능). 접근성 '흔들림 줄이기'면 번쩍·쇼크 컷 끔.
// 효과음: 맞음 hit_p1~4(덩치는 크기 ×1.3·음높이 ×0.85) · 휘두름(판정 0.09초 전) · 다운 착지·벽꽝 slam · 기세 MAX·기세 액션 heat · 막기 block · 회피 dodge.
// 배치 모드(테스트·녹화)에서는 소리를 끈다(AudioListener 0) — 재생 횟수만 센다.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Haengin
{
    [DefaultExecutionOrder(80), DisallowMultipleComponent]
    public sealed class CombatFx : MonoBehaviour
    {
        public FxKit Kit;
        public CombatTuning Tuning;
        [Tooltip("빌보드 풀 크기")] public int Pool = 256;

        public static CombatFx Instance { get; private set; }
        /// 테스트·녹화용 기록
        public static int Hits, Words, WordsCut, Flashes, Shocks, Slams, Plays;
        Item lastWordItem;
        public static string LastWord = "", LastSound = "";
        public int Live => live.Count;

        static readonly Color Ink = new Color(0.102f, 0.078f, 0.090f);       // #1A1417
        static readonly Color Accent = new Color(0.910f, 0.341f, 0.165f);    // #E8572A
        static readonly int IdShock = Shader.PropertyToID("_HaenginShock");
        static readonly int IdColor = Shader.PropertyToID("_Color"), IdTex = Shader.PropertyToID("_MainTex");

        CombatTuning T => Tuning != null ? Tuning : CombatTuning.Default;

        // ── 빌보드
        sealed class Item
        {
            public Transform Tr; public MeshRenderer R; public bool Busy;
            public float Life, Max, S0, S1, Aspect, O0, Rot, Spin, Grav;
            public Vector3 Vel; public bool Pop, Flat, Top, Crack; public Color Col; public Texture Tex;
            public Quaternion Plane;    // Flat: 바닥·벽 면 방향
        }
        readonly List<Item> pool = new List<Item>();
        readonly List<Item> live = new List<Item>();
        Mesh quad;
        MaterialPropertyBlock mpb;
        System.Random rng;

        // ── 화면
        Canvas canvas;
        Image flash, edge;
        float flashA, flashT, edgeT = -1f;
        int shockUntil = -1;
        bool firstHitDone;
        double lastWord = -1;

        // ── 소리
        readonly List<AudioSource> voices = new List<AudioSource>();
        int voice;
        struct Swing { public AttackRun Run; public double At; }
        readonly List<Swing> swings = new List<Swing>();
        PlayerCombat pc;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; Hits = Words = WordsCut = Flashes = Shocks = Slams = Plays = 0; LastWord = LastSound = ""; }

        void Awake()
        {
            rng = new System.Random(T.Seed * 7 + 3);
            mpb = new MaterialPropertyBlock();
            quad = new Mesh { name = "FxQuad" };
            quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
            quad.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            quad.RecalculateBounds();
            quad.bounds = new Bounds(Vector3.zero, Vector3.one * 4f);
            if (Application.isBatchMode) AudioListener.volume = 0f;      // 테스트·녹화: 소리 없이
        }

        void OnEnable()
        {
            Instance = this;
            ImpactFx.Fired += OnHit;
            Fighter.DownLanded += OnDownLanded;
            Fighter.AttackStarted += OnAttackStarted;
            HeatAction.WallBang += OnWallBang;
            HeatAction.Started += OnHeatStarted;
            CombatMode.AnyChanged += OnCombat;
            UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += FaceCamera;
            Shader.SetGlobalFloat(IdShock, 0f);
        }

        /// 빌보드는 그리는 카메라마다 그 카메라를 보게(게임 카메라 · 녹화용 옆 카메라 · CM_Heat 블렌드 모두)
        void FaceCamera(UnityEngine.Rendering.ScriptableRenderContext ctx, Camera cam)
        {
            if (cam == null || cam.cameraType == CameraType.Preview) return;
            var rot = cam.transform.rotation;
            foreach (var it in live)
                if (!it.Flat && it.Tr != null) it.Tr.rotation = rot * Quaternion.Euler(0f, 0f, it.Rot * Mathf.Rad2Deg);
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
            ImpactFx.Fired -= OnHit;
            Fighter.DownLanded -= OnDownLanded;
            Fighter.AttackStarted -= OnAttackStarted;
            HeatAction.WallBang -= OnWallBang;
            HeatAction.Started -= OnHeatStarted;
            CombatMode.AnyChanged -= OnCombat;
            UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= FaceCamera;
            Unhook();
            Shader.SetGlobalFloat(IdShock, 0f);
            foreach (var it in live) Free(it);
            live.Clear();
        }

        void Start() { Hook(); }

        void Hook()
        {
            Unhook();
            pc = GetComponentInParent<PlayerCombat>();
            if (pc == null) pc = FindAnyObjectByType<PlayerCombat>();
            if (pc == null) return;
            pc.Notice += OnNotice;
            pc.DodgeStarted += OnDodge;
            pc.Heat.Changed += OnHeat;
        }

        void Unhook()
        {
            if (pc == null) return;
            pc.Notice -= OnNotice;
            pc.DodgeStarted -= OnDodge;
            pc.Heat.Changed -= OnHeat;
            pc = null;
        }

        void OnCombat(bool on) { if (on) firstHitDone = false; }

        float R() => (float)rng.NextDouble();

        // ───────────────────────── 사건
        void OnHit(HitEvent e, float stop)
        {
            if (Kit == null || e.Victim == null || e.Outcome == HitOutcome.Read) return;
            Hits++;
            var pos = e.Point;
            var dir = e.Dir.sqrMagnitude > 1e-6f ? e.Dir : (e.Attacker != null ? e.Attacker.Forward : Vector3.forward);
            if (e.Outcome == HitOutcome.Blocked)
            {
                Word(pos, "툭", 1, 0.55f);
                Play(Kit.Block, 0.8f, 1f);
                return;
            }
            int p = e.Outcome == HitOutcome.Crushed ? 2 : Mathf.Clamp((int)(e.Move != null && e.Move.StopPower != Power.None ? e.Move.StopPower : e.Power), 1, 4);
            InkHit(pos, Mathf.Min(p, 3), dir);
            string w = e.Outcome == HitOutcome.Crushed ? "빡!" : e.Move != null ? e.Move.Word : "";
            if (!string.IsNullOrEmpty(w)) Word(pos, w, Mathf.Min(p, 3));
            bool bulky = e.Victim.Armor || e.Victim.Height >= 1.83f;
            Play(Kit.Hit != null && p < Kit.Hit.Length ? Kit.Hit[p] : null, bulky ? 1.3f : 1f, bulky ? 0.85f : 1f);
            if (e.Move != null && e.Move.Label == "벽꽝") { Play(Kit.Slam, 1f, 1f); Slams++; }
            bool reduced = T.ReducedNow;
            if (!reduced)
            {
                if (p >= 4) Flash(0.40f);
                else if (p >= 3) Flash(0.32f);
                if (p >= 4 || (!firstHitDone && e.Landed && e.Attacker != null && e.Attacker.IsPlayer)) Shock();
            }
            if (e.Landed && e.Attacker != null && e.Attacker.IsPlayer) firstHitDone = true;
        }

        void OnDownLanded(Fighter f)
        {
            if (Kit == null || f == null) return;
            Slam(f.Position);
            Word(f.Position + Vector3.up * 0.4f, "쿵!", 3);
            Play(Kit.Slam, 1f, 1f);
            Slams++;
        }

        void OnWallBang(Vector3 point, Vector3 normal)
        {
            if (Kit == null) return;
            Crack(point, normal);
            Play(Kit.Slam, 0.8f, 1.1f);
        }

        void OnHeatStarted(HeatAction h) { if (Kit != null) Play(Kit.Heat, 1f, 1f); }

        void OnHeat(float old, float now)
        {
            if (Kit == null || pc == null || !pc.Active) return;
            if (old < HeatGauge.Max - 1e-3f && now >= HeatGauge.Max - 1e-3f) Play(Kit.Heat, 1f, 1f);
        }

        void OnDodge() { if (Kit != null) Play(Kit.Dodge, 1f, 1f); }

        void OnNotice(string s)
        {
            if (Kit == null || s != "읽었다" || pc == null || pc.Me == null) return;
            edgeT = 0f;
            Word(pc.Me.Position + Vector3.up * 2.05f, "읽었다", 1, 0.5f);
        }

        void OnAttackStarted(Fighter f, AttackRun r)
        {
            if (Kit == null || r == null || r.Move == null || r.Move.Power == Power.None) return;
            swings.Add(new Swing { Run = r, At = Math.Max(r.T, r.Move.ActiveStart - 0.09) });
        }

        // ───────────────────────── 시안 inkHit · inkSlam · word
        Vector3 Out(Vector3 dir) => new Vector3(dir.x * (0.5f + R()) + (R() - 0.5f), 0.2f + R() * 1.2f, dir.z * (0.5f + R()) + (R() - 0.5f));

        public void InkHit(Vector3 pos, int p, Vector3 dir)
        {
            var cam = Camera.main;
            var right = cam != null ? cam.transform.right : Vector3.right;
            var up = cam != null ? cam.transform.up : Vector3.up;
            float dx = Vector3.Dot(dir, right);
            float sx = Mathf.Abs(dx) > 0.2f ? Mathf.Sign(dx) : (R() < 0.5f ? -1f : 1f);
            Spawn(Kit.Splat, pos, 0.2f, 0.5f + p * 0.14f, 0.16f, Accent, rot: R() * Mathf.PI * 2f, top: true, pop: true);
            Spawn(Kit.Brush, pos, 0.3f, 0.42f + p * 0.14f, 0.2f + 0.03f * p, Ink, aspect: 2f, rot: (sx > 0f ? -0.35f : Mathf.PI + 0.35f) + (R() - 0.5f) * 0.7f, top: true, pop: true);
            if (p >= 2) Spawn(Kit.Lines, pos, 0.9f, 1.5f + p * 0.35f, 0.17f, Ink, rot: R() * Mathf.PI * 2f, top: true, opacity: 0.9f);
            Spawn(Kit.Star, pos, 0.12f, 0.36f + p * 0.15f, 0.13f, Color.white, rot: R() * Mathf.PI * 2f, top: true, pop: true);
            for (int i = 0; i < 4 + p * 3; i++)
            {
                var v = Out(dir) * (2.6f + p * 0.8f) + right * (sx * 1.2f);
                float vx = Vector3.Dot(v, right), vy = Vector3.Dot(v, up);
                Spawn(Kit.Shard, pos, 0.13f + R() * 0.06f, 0.03f, 0.2f + R() * 0.1f, Color.white, vel: v, aspect: 0.3f, rot: Mathf.Atan2(vy, vx) - Mathf.PI / 2f, top: true);
            }
            for (int i = 0; i < 2 + p * 2; i++)
                Spawn(Kit.Splat, pos, 0.04f + R() * 0.05f, 0.02f, 0.35f + R() * 0.2f, Ink, vel: Out(dir) * (1.8f + p * 0.5f), grav: -6f, rot: R() * Mathf.PI * 2f, top: true);
        }

        public void Slam(Vector3 feet)
        {
            var g = new Vector3(feet.x, feet.y + 0.03f, feet.z);
            Spawn(Kit.Ring, g, 0.4f, 3.0f, 0.5f, Ink, flat: Quaternion.Euler(90f, 0f, 0f), rot: R() * Mathf.PI * 2f);
            Spawn(Kit.Lines, feet + Vector3.up * 0.5f, 1.2f, 3.2f, 0.3f, Ink, rot: R() * Mathf.PI * 2f, top: true, opacity: 0.85f);
            for (int i = 0; i < 9; i++)
            {
                float a = i / 9f * Mathf.PI * 2f + R() * 0.3f;
                Spawn(Kit.Dust, feet + Vector3.up * 0.18f, 0.25f, 0.75f + R() * 0.3f, 0.55f + R() * 0.25f, Color.white,
                      vel: new Vector3(Mathf.Cos(a) * 2.2f, 0.25f + R() * 0.4f, Mathf.Sin(a) * 1.2f), rot: R() * Mathf.PI * 2f, pop: true);
            }
            for (int i = 0; i < 8; i++)
                Spawn(Kit.Splat, feet + Vector3.up * 0.2f, 0.05f + R() * 0.05f, 0.02f, 0.45f, Ink,
                      vel: new Vector3((R() - 0.5f) * 4f, 1.5f + R() * 2f, (R() - 0.5f) * 2f), grav: -8f, rot: R() * Mathf.PI * 2f);
        }

        /// 벽 러시: 대상 등이 벽에 쿵 — 벽에 먹 균열(튀김 + 붓 획) 1.5초(08 3-8)
        public void Crack(Vector3 point, Vector3 normal)
        {
            var n = normal.sqrMagnitude > 1e-6f ? normal.normalized : Vector3.back;
            var plane = Quaternion.LookRotation(-n);
            var p = point + n * 0.02f;
            Spawn(Kit.Splat, p, 0.5f, 1.1f, 1.5f, Ink, flat: plane, rot: R() * Mathf.PI * 2f, crack: true);
            Spawn(Kit.Brush, p + n * 0.005f, 0.6f, 1.3f, 1.5f, Ink, aspect: 2f, flat: plane, rot: R() * Mathf.PI * 2f, crack: true);
            Spawn(Kit.Lines, p + n * 0.01f, 0.6f, 1.6f, 0.4f, Ink, flat: plane, rot: R() * Mathf.PI * 2f, opacity: 0.8f);
        }

        /// 의성어(맞은 점 위 0.32m, 가로 ±0.1m, 기울임 ±8°, 크기 0.2 → 0.55 + 0.12p, 0.55초, 위로 0.35m/s)
        public void Word(Vector3 pos, string text, int p, float scale = 1f)
        {
            if (Kit == null) return;
            var tex = Kit.WordFor(text);
            if (tex == null) return;
            double now = TimeFx.Real;     // 게임의 실제 시간(테스트의 고정 프레임에서도 같은 시계)
            if (lastWord >= 0 && now - lastWord < 0.12) return;
            lastWord = now;
            Words++;
            LastWord = text;
            var cam = Camera.main;
            var right = cam != null ? cam.transform.right : Vector3.right;
            var at = pos + Vector3.up * 0.32f + right * ((R() - 0.5f) * 0.2f);
            // 앞 글자가 아직 떠 있으면 지운다(연타·마무리에서 '퍽!' 위에 '콰직!' 이 겹치던 것 — 11-4)
            if (lastWordItem != null && lastWordItem.Busy && live.Contains(lastWordItem)) { live.Remove(lastWordItem); Free(lastWordItem); WordsCut++; }
            lastWordItem = Spawn(tex, at, 0.2f * scale, (0.55f + 0.12f * p) * scale, 0.55f, Color.white, vel: Vector3.up * 0.35f, aspect: 2f,
                  rot: (R() - 0.5f) * 2f * 8f * Mathf.Deg2Rad, top: true, pop: true, keepVel: true);
        }

        // ───────────────────────── 화면
        /// 전투 시작: 화면 가장자리 먹 붓 테두리가 0.3초에 들어옴(2-5)
        public void EdgeFlash() { if (Kit != null) edgeT = 0f; }

        public void Flash(float a)
        {
            if (T.ReducedNow) return;
            Ui();
            flashA = Mathf.Max(flashA * (1f - flashT / 0.16f), a);
            flashT = 0f;
            Flashes++;
        }

        public void Shock()
        {
            if (T.ReducedNow) return;
            shockUntil = Time.frameCount + 2;     // 이 프레임과 다음 프레임(2프레임)
            Shocks++;
            Shader.SetGlobalFloat(IdShock, 1f);
        }

        void Ui()
        {
            if (canvas != null) return;
            var go = new GameObject("화면 효과", typeof(RectTransform)) { layer = 5 };
            go.transform.SetParent(transform, false);
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var sc = go.AddComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920f, 1080f);
            sc.matchWidthOrHeight = 1f;
            edge = Full("먹 테두리", Kit != null ? Kit.Edge : null, new Color(Ink.r, Ink.g, Ink.b, 0f));
            flash = Full("번쩍", null, new Color(1f, 1f, 1f, 0f));
        }

        Image Full(string name, Texture2D tex, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)) { layer = 5 };
            go.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            if (tex != null) img.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        // ───────────────────────── 소리
        public void Play(AudioClip c, float vol, float pitch)
        {
            if (c == null) return;
            Plays++;
            LastSound = c.name;
            if (voices.Count == 0)
                for (int i = 0; i < 8; i++)
                {
                    var s = gameObject.AddComponent<AudioSource>();
                    s.playOnAwake = false; s.spatialBlend = 0f; s.ignoreListenerPause = false;
                    voices.Add(s);
                }
            var v = voices[voice];
            voice = (voice + 1) % voices.Count;
            v.pitch = pitch;
            v.PlayOneShot(c, Mathf.Clamp(vol, 0f, 1.5f) * 0.8f);
        }

        // ───────────────────────── 빌보드 풀
        Item Spawn(Texture tex, Vector3 pos, float s0, float s1, float life, Color col, Vector3 vel = default, float aspect = 1f, float rot = 0f,
                   float grav = 0f, bool top = false, bool pop = false, float opacity = 1f, Quaternion? flat = null, bool crack = false, bool keepVel = false)
        {
            if (tex == null || Kit == null || Kit.Normal == null) return null;
            var it = Get();
            if (it == null) return null;
            it.Life = 0f; it.Max = life; it.S0 = s0; it.S1 = s1; it.Aspect = aspect; it.O0 = opacity; it.Rot = rot; it.Grav = grav;
            it.Vel = vel; it.Pop = pop; it.Top = top; it.Col = col; it.Tex = tex;
            it.Flat = flat.HasValue; it.Plane = flat ?? Quaternion.identity;
            it.Crack = crack;     // 균열: 75% 까지 진하게, 그 뒤 사라짐
            it.Tr.position = pos;
            it.R.sharedMaterial = top ? Kit.Top : Kit.Normal;
            it.Tr.gameObject.SetActive(true);
            live.Add(it);
            Apply(it, 0f);
            return it;
        }

        Item Get()
        {
            foreach (var p in pool) if (!p.Busy) { p.Busy = true; return p; }
            if (pool.Count >= Pool) return null;
            var go = new GameObject("fx") { layer = 0 };
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = quad;
            var r = go.AddComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            var it = new Item { Tr = go.transform, R = r, Busy = true };
            pool.Add(it);
            return it;
        }

        void Free(Item it)
        {
            it.Busy = false;
            if (it.Tr != null) it.Tr.gameObject.SetActive(false);
        }

        static float BackOut(float t) { const float c1 = 1.70158f, c3 = c1 + 1f; return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f); }
        static float EaseOut(float t) => 1f - Mathf.Pow(1f - t, 3f);

        void Apply(Item it, float dt)
        {
            it.Life += dt;
            float u = Mathf.Min(1f, it.Life / Mathf.Max(1e-4f, it.Max));
            float k = it.Pop ? BackOut(Mathf.Min(1f, u * 3f)) : EaseOut(u);
            float sc = Mathf.LerpUnclamped(it.S0, it.S1, k);
            if (dt > 0f)
            {
                it.Vel.y += it.Grav * dt;
                it.Tr.position += it.Vel * dt;
                it.Vel *= Mathf.Exp(-dt * 2.5f);
            }
            var cam = Camera.main;
            var face = it.Flat ? it.Plane : cam != null ? cam.transform.rotation : Quaternion.identity;
            it.Tr.rotation = face * Quaternion.Euler(0f, 0f, it.Rot * Mathf.Rad2Deg);
            it.Tr.localScale = new Vector3(sc * it.Aspect, sc, 1f);
            float a = it.Crack ? (u < 0.75f ? 1f : 1f - (u - 0.75f) / 0.25f)
                                   : it.O0 * (it.Pop ? (u < 0.7f ? 1f : 1f - (u - 0.7f) / 0.3f) : 1f - u * u);
            var c = it.Col; c.a *= Mathf.Clamp01(a);
            mpb.Clear();
            mpb.SetTexture(IdTex, it.Tex);
            mpb.SetColor(IdColor, c);
            it.R.SetPropertyBlock(mpb);
        }

        // ───────────────────────── 한 프레임
        void LateUpdate()
        {
            float rdt = TimeFx.RealDt > 0f ? TimeFx.RealDt : Time.unscaledDeltaTime;
            float k = TimeFx.Paused ? 0f : TimeFx.InHitStop ? 0.15f : TimeFx.InSlow ? 0.45f : 1f;
            float fdt = rdt * k;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var it = live[i];
                Apply(it, fdt);
                if (it.Life >= it.Max) { Free(it); live.RemoveAt(i); }
            }
            // 휘두름 소리(판정 0.09초 전)
            for (int i = swings.Count - 1; i >= 0; i--)
            {
                var s = swings[i];
                if (s.Run == null || s.Run.Owner == null || s.Run.Owner.Run != s.Run) { swings.RemoveAt(i); continue; }
                if (s.Run.T >= s.At) { Play(Kit != null ? Kit.Whoosh : null, 1f, 1f); swings.RemoveAt(i); }
            }
            // 화면 번쩍(실제 시간 0.16초 ease-out) · 먹 테두리('읽었다' 0.3초 + 0.3초 사라짐)
            if (flash != null)
            {
                flashT += rdt;
                float u = Mathf.Clamp01(flashT / 0.16f);
                float a = flashA * (1f - (1f - (1f - u) * (1f - u)));
                flash.color = new Color(1f, 1f, 1f, a);
                if (u >= 1f) flashA = 0f;
            }
            if (edgeT >= 0f)
            {
                Ui();
                edgeT += rdt;
                float a = edgeT < 0.3f ? 0.85f : Mathf.Max(0f, 0.85f * (1f - (edgeT - 0.3f) / 0.3f));
                edge.color = new Color(Ink.r, Ink.g, Ink.b, a);
                if (edgeT > 0.6f) edgeT = -1f;
            }
            if (shockUntil >= 0 && Time.frameCount >= shockUntil)
            {
                shockUntil = -1;
                Shader.SetGlobalFloat(IdShock, 0f);
            }
        }
    }
}
