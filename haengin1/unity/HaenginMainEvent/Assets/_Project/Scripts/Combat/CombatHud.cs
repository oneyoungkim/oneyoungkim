// 행인1의 메인이벤트 — 전투 HUD (docs/08_M2_전투_설계.md 7장, 11장 12단계)
// UGUI 캔버스(정렬 30, 기준 1920×1080), 글꼴 KR_Bold_SDF. 색: 먹 #1A1417 · 종이 #F4EFE6 · 시우 주황 #E2582C(기세 게이지에만).
//   7-1 시우(왼쪽 위 여백 48): 「반시우」 28px · 체력 420×18(먹 테 3px, 종이색 채움, 깎인 만큼 흰색 0.4초 남았다 줄어듦, 30% 아래 0.8초 주기 깜빡)
//       · 기세 420×10 주황(100 이면 먹 붓 획 일렁임 + 「기세」 20px) · 막는 중 발밑 반원 가드 게이지(30% 아래 흰색 깜빡)
//   7-2 적: 락온 대상 머리 위 0.35m — 이름 24px + 160×8 바(먹 바탕 흰 채움) / 다른 적은 맞은 뒤 2초만 바(이름 없이, 0.3초에 흐려짐)
//   7-3 락온: 발밑 먹 붓 원(1.0m, 먹 60%, 5°/s) + 머리 위 역삼각형 / 소프트 조준 대상 발밑 원 30% / 기세 액션 판 「△ 기세」(0.15초에 나타남)
//   7-4 화면 밖 화살표(공격권 가진 적, 먹 + 흰 테, 예고 시작 때 0.2초 간격 2번 커짐)
//   7-5 조작 안내(전투 내용, 마지막 입력 장치 따라) · 처음 한 번 도움말(첫 '!!' 「피해!(×)」, 첫 기세 MAX 「벽이나 구경꾼 앞에서 △」)
// 결과 카드·패배 화면·야차 큰 바는 인카운터·야차(13·14단계) 차례.
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Haengin
{
    [DefaultExecutionOrder(90), DisallowMultipleComponent]
    public sealed class CombatHud : MonoBehaviour
    {
        public PlayerCombat Player;
        public FxKit Kit;
        public TMP_FontAsset Font;

        public static readonly Color Ink = new Color(0.102f, 0.078f, 0.090f), Paper = new Color(0.957f, 0.937f, 0.902f), Orange = new Color(0.886f, 0.345f, 0.173f);
        public const string PadHint = "□ 약 · △ 강 · ○ 잡기 · × 회피 · R1 막기 · L1 락온";
        public const string KeyHint = "좌클릭 약 · 우클릭 강 · F 잡기 · Space 회피 · Ctrl 막기 · Q 락온";

        // ── 시험·녹화가 읽는 것
        public bool Visible { get; private set; }
        public float HpShown => hpFill != null ? hpFill.fillAmount : 0f;
        public float HeatShown => heatFill != null ? heatFill.fillAmount : 0f;
        public bool HeatMaxShown => maxRoot != null && maxRoot.activeSelf;
        public bool TargetShown => tgRoot != null && tgRoot.activeSelf;
        public string TargetName => tgName != null ? tgName.text : "";
        public float TargetBar => tgFill != null ? tgFill.fillAmount : 0f;
        public bool HeatPlateShown => plate != null && plate.activeSelf && plateGroup.alpha > 0.5f;
        public string HeatPlateText => plateText != null ? plateText.text : "";
        public bool ArrowShown => arrow != null && arrow.gameObject.activeSelf;
        public bool GuardShown => guard != null && guard.gameObject.activeSelf;
        public int OtherBarsShown { get { int n = 0; foreach (var b in bars.Values) if (b.Root.activeSelf) n++; return n; } }
        public TMP_FontAsset UsedFont => nameText != null ? nameText.font : null;
        public static bool PadLast;

        Canvas canvas;
        RectTransform root;
        TextMeshProUGUI nameText, maxText, tgName, plateText;
        Image hpFill, hpRecent, heatFill, maxBrush, tgFill, guard, arrow, arrowBack, tgTri;
        GameObject maxRoot, tgRoot, plate;
        CanvasGroup plateGroup;
        float recent = 1f, recentHold, lastHp = 1f, plateA, arrowPulse = -1f, t;
        Fighter lastArrowWho;
        bool lastTelegraph, tipDanger, tipHeat;
        Transform lockRing, softRing;
        MeshRenderer lockR, softR;
        MaterialPropertyBlock mpb;

        sealed class Bar { public GameObject Root; public Image Fill; public float Shown; public CanvasGroup Group; }
        readonly Dictionary<Fighter, Bar> bars = new Dictionary<Fighter, Bar>();

        void Awake() { if (Player == null) Player = GetComponentInParent<PlayerCombat>(); mpb = new MaterialPropertyBlock(); }
        void OnEnable() { Fighter.AnyHit += OnAnyHit; }
        void OnDisable() { Fighter.AnyHit -= OnAnyHit; if (GameUi.HintOverride != null) GameUi.HintOverride = null; }

        void OnAnyHit(HitEvent e)
        {
            if (e.Victim == null || e.Victim.IsPlayer || e.Outcome == HitOutcome.Read) return;
            Build();
            if (!bars.TryGetValue(e.Victim, out var b)) { b = MakeBar(); bars[e.Victim] = b; }
            b.Shown = 2.0f;
        }

        // ───────────────────────── 만들기(실행할 때 한 번)
        void Build()
        {
            if (canvas != null) return;
            var font = Font != null ? Font : Kit != null && Kit.Font != null ? Kit.Font : TMP_Settings.defaultFontAsset;
            Font = font;
            var go = new GameObject("전투 HUD", typeof(RectTransform)) { layer = 5 };
            go.transform.SetParent(transform, false);
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30;
            var sc = go.AddComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920f, 1080f);
            sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            sc.matchWidthOrHeight = 1f;
            root = (RectTransform)go.transform;

            // 7-1 시우(왼쪽 위)
            var tl = new Vector2(0f, 1f);
            var me = Rect("시우", root, tl, tl, tl, new Vector2(48f, -48f), new Vector2(440f, 110f));
            nameText = Text("이름", me, font, 28f, Color.white, TextAlignmentOptions.TopLeft, new Vector2(0f, 0f), new Vector2(420f, 36f));
            nameText.text = "반시우";
            Outline(nameText, 0.22f);
            var hpFrame = Img("체력 테", me, null, Ink, new Vector2(0f, -40f), new Vector2(426f, 24f));
            hpRecent = Img("최근 피해", hpFrame.rectTransform, null, Color.white, new Vector2(3f, -3f), new Vector2(420f, 18f));
            Filled(hpRecent);
            hpFill = Img("체력", hpFrame.rectTransform, null, Paper, new Vector2(3f, -3f), new Vector2(420f, 18f));
            Filled(hpFill);
            var heatFrame = Img("기세 테", me, null, Ink, new Vector2(0f, -70f), new Vector2(426f, 16f));
            var heatBack = Img("기세 바탕", heatFrame.rectTransform, null, new Color(0.25f, 0.22f, 0.22f), new Vector2(3f, -3f), new Vector2(420f, 10f));
            heatFill = Img("기세", heatFrame.rectTransform, null, Orange, new Vector2(3f, -3f), new Vector2(420f, 10f));
            Filled(heatFill);
            maxRoot = new GameObject("기세 MAX", typeof(RectTransform)) { layer = 5 };
            maxRoot.transform.SetParent(me, false);
            var mr = (RectTransform)maxRoot.transform;
            mr.anchorMin = mr.anchorMax = mr.pivot = tl;
            mr.anchoredPosition = new Vector2(0f, -52f);
            mr.sizeDelta = new Vector2(440f, 50f);
            maxBrush = Img("먹 붓 획", mr, Kit != null ? Kit.Brush : null, new Color(Ink.r, Ink.g, Ink.b, 0.9f), new Vector2(-8f, 6f), new Vector2(460f, 56f));
            maxText = Text("기세 글", mr, font, 20f, Color.white, TextAlignmentOptions.Left, new Vector2(432f, -12f), new Vector2(80f, 28f));
            maxText.text = "기세";
            Outline(maxText, 0.25f);
            maxRoot.transform.SetSiblingIndex(heatFrame.transform.GetSiblingIndex());     // 바 뒤에 붓 획
            maxRoot.SetActive(false);

            // 막기 반원(발밑)
            guard = Img("가드", root, Kit != null ? Kit.Arc : null, Color.white, Vector2.zero, new Vector2(160f, 80f), center: true, bottom: true);
            guard.type = Image.Type.Filled;
            guard.fillMethod = Image.FillMethod.Radial180;
            guard.fillOrigin = (int)Image.Origin180.Bottom;
            guard.gameObject.SetActive(false);

            // 7-2·7-3 락온 대상(머리 위)
            tgRoot = new GameObject("락온 대상", typeof(RectTransform)) { layer = 5 };
            tgRoot.transform.SetParent(root, false);
            var tr = (RectTransform)tgRoot.transform;
            tr.anchorMin = tr.anchorMax = Vector2.zero;
            tr.pivot = new Vector2(0.5f, 0f);
            tr.sizeDelta = new Vector2(220f, 70f);
            tgTri = Img("역삼각형", tr, Kit != null ? Kit.Tri : null, Color.white, new Vector2(0f, 46f), new Vector2(22f, 22f), center: true, bottom: true);
            tgName = Text("이름", tr, font, 24f, Color.white, TextAlignmentOptions.Bottom, new Vector2(-110f, 14f), new Vector2(220f, 30f), bottom: true);
            Outline(tgName, 0.25f);
            var tgBack = Img("바 바탕", tr, null, Ink, new Vector2(0f, 0f), new Vector2(166f, 14f), center: true, bottom: true);
            tgFill = Img("바", tgBack.rectTransform, null, Color.white, new Vector2(3f, -3f), new Vector2(160f, 8f));
            Filled(tgFill);
            tgRoot.SetActive(false);

            // 기세 액션 판
            plate = new GameObject("기세 판", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)) { layer = 5 };
            plate.transform.SetParent(root, false);
            var pr = (RectTransform)plate.transform;
            pr.anchorMin = pr.anchorMax = Vector2.zero; pr.pivot = new Vector2(0f, 0.5f);
            pr.sizeDelta = new Vector2(150f, 46f);
            var pimg = plate.GetComponent<Image>(); pimg.color = Ink; pimg.raycastTarget = false;
            var pin = Img("판", pr, null, Paper, new Vector2(3f, -3f), new Vector2(144f, 40f));
            plateText = Text("글", pin.rectTransform, font, 24f, Ink, TextAlignmentOptions.Center, Vector2.zero, new Vector2(144f, 40f));
            plateGroup = plate.AddComponent<CanvasGroup>();
            plateGroup.alpha = 0f;
            plate.SetActive(false);

            // 7-4 화면 밖 화살표(흰 테 + 먹)
            arrowBack = Img("화살표 테", root, Kit != null ? Kit.Tri : null, Color.white, Vector2.zero, new Vector2(60f, 60f), center: true);
            arrow = Img("화살표", arrowBack.rectTransform, Kit != null ? Kit.Tri : null, Ink, Vector2.zero, new Vector2(46f, 46f), center: true);
            arrowBack.gameObject.SetActive(false);
            arrow = arrowBack;    // 보임 여부는 테로

            // 발밑 원(월드)
            lockRing = Ring("락온 원", out lockR);
            softRing = Ring("조준 원", out softR);
        }

        Transform Ring(string name, out MeshRenderer r)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            r = go.GetComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (Kit != null && Kit.Normal != null) r.sharedMaterial = Kit.Normal;
            go.SetActive(false);
            return go.transform;
        }

        Bar MakeBar()
        {
            var go = new GameObject("적 바", typeof(RectTransform)) { layer = 5 };
            go.transform.SetParent(root, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = r.anchorMax = Vector2.zero; r.pivot = new Vector2(0.5f, 0f);
            r.sizeDelta = new Vector2(166f, 14f);
            var back = Img("바 바탕", r, null, Ink, Vector2.zero, new Vector2(166f, 14f), center: true, bottom: true);
            var fill = Img("바", back.rectTransform, null, Color.white, new Vector2(3f, -3f), new Vector2(160f, 8f));
            Filled(fill);
            var g = go.AddComponent<CanvasGroup>();
            go.SetActive(false);
            return new Bar { Root = go, Fill = fill, Group = g };
        }

        // ───────────────────────── 한 프레임
        void LateUpdate()
        {
            var pc = Player;
            bool on = pc != null && pc.Active && pc.Me != null;
            if (!on)
            {
                if (Visible) Hide();
                return;
            }
            Build();
            if (!canvas.gameObject.activeSelf) canvas.gameObject.SetActive(true);
            Visible = true;
            float rdt = TimeFx.RealDt > 0f ? TimeFx.RealDt : Time.unscaledDeltaTime;
            t += rdt;
            var cam = Camera.main;
            var me = pc.Me;

            // 조작 안내(마지막 입력 장치)
            var gp = Gamepad.current; var kb = Keyboard.current; var ms = Mouse.current;
            double tg = gp != null ? gp.lastUpdateTime : -1, tk = System.Math.Max(kb != null ? kb.lastUpdateTime : -1, ms != null ? ms.lastUpdateTime : -1);
            if (tg > 0 || tk > 0) PadLast = tg > tk;
            GameUi.HintOverride = PadLast ? PadHint : KeyHint;

            // 7-1 체력(깎인 만큼 흰색 0.4초 남았다 줄어듦) · 30% 아래 깜빡
            float hp = Mathf.Clamp01(me.MaxHp > 0 ? me.Hp / (float)me.MaxHp : 0f);
            hpFill.fillAmount = hp;
            if (hp < lastHp - 1e-5f) recentHold = 0.4f;          // 새로 깎임: 흰색 0.4초 남김
            lastHp = hp;
            if (hp < recent)
            {
                if (recentHold > 0f) recentHold -= rdt;
                else recent = Mathf.MoveTowards(recent, hp, rdt * 1.5f);
            }
            else recent = hp;
            hpRecent.fillAmount = recent;
            float blink = hp < 0.3f ? 0.55f + 0.45f * Mathf.Cos(t * Mathf.PI * 2f / 0.8f) : 1f;
            hpFill.color = new Color(Paper.r, Paper.g, Paper.b, blink);

            // 기세
            heatFill.fillAmount = pc.Heat.Value / HeatGauge.Max;
            bool max = pc.Heat.Full;
            if (maxRoot.activeSelf != max) maxRoot.SetActive(max);
            if (max)
            {
                float w = 0.85f + 0.15f * Mathf.Sin(t * 9f);
                maxBrush.rectTransform.localScale = new Vector3(1f + 0.03f * Mathf.Sin(t * 7f), w, 1f);
                maxBrush.color = new Color(Ink.r, Ink.g, Ink.b, 0.75f + 0.2f * Mathf.Sin(t * 5f));
                if (!tipHeat) { tipHeat = true; DebugHud.Toast(PadLast ? "벽이나 구경꾼 앞에서 △" : "벽이나 구경꾼 앞에서 우클릭", 3f); }
            }

            // 가드 반원(막는 중, 발밑)
            bool guarding = me.Guarding && me.State == Fighter.Phase.Free;
            if (guard.gameObject.activeSelf != guarding) guard.gameObject.SetActive(guarding);
            if (guarding && cam != null)
            {
                var feet = me.Position + Vector3.up * 0.05f;
                var c = cam.WorldToScreenPoint(feet);
                var side = cam.WorldToScreenPoint(feet + cam.transform.right * 0.6f);
                float px = Mathf.Abs(side.x - c.x) / Scale();
                guard.rectTransform.sizeDelta = new Vector2(px * 2f, px);
                Place(guard.rectTransform, c, new Vector2(0f, 0f));
                float g = me.Guard / Mathf.Max(1f, me.GuardMaxNow);
                guard.fillAmount = g;
                guard.color = new Color(1f, 1f, 1f, g < 0.3f ? 0.5f + 0.5f * Mathf.Cos(t * Mathf.PI * 2f / 0.5f) : 0.9f);
            }

            // 7-2·7-3 락온 대상
            var tgt = pc.Locked ? pc.Lock.Target : null;
            bool showT = tgt != null && cam != null && InFront(cam, tgt.Position);
            if (tgRoot.activeSelf != showT) tgRoot.SetActive(showT);
            if (showT)
            {
                if (tgName.text != tgt.Label) tgName.text = tgt.Label;
                tgFill.fillAmount = Mathf.Clamp01(tgt.MaxHp > 0 ? tgt.Hp / (float)tgt.MaxHp : 0f);
                var head = tgt.Position + Vector3.up * (tgt.Height + 0.35f);
                Place((RectTransform)tgRoot.transform, cam.WorldToScreenPoint(head), Vector2.zero);
            }
            Floor(lockRing, lockR, tgt, 1.0f, 0.6f);
            var soft = tgt == null ? pc.PickTarget(pc.Moves != null ? pc.Moves.Jab : null) : null;
            Floor(softRing, softR, soft, 1.0f, 0.3f);

            // 다른 적: 맞은 뒤 2초(0.3초에 흐려짐)
            foreach (var kv in bars)
            {
                var f = kv.Key; var b = kv.Value;
                if (f == null || f == tgt || f.State == Fighter.Phase.Out || cam == null || !InFront(cam, f.Position)) { if (b.Root.activeSelf) b.Root.SetActive(false); b.Shown = Mathf.Min(b.Shown, 0f); continue; }
                b.Shown -= rdt;
                bool vis = b.Shown > -0.3f;
                if (b.Root.activeSelf != vis) b.Root.SetActive(vis);
                if (!vis) continue;
                b.Group.alpha = b.Shown > 0f ? 1f : 1f + b.Shown / 0.3f;
                b.Fill.fillAmount = Mathf.Clamp01(f.MaxHp > 0 ? f.Hp / (float)f.MaxHp : 0f);
                Place((RectTransform)b.Root.transform, cam.WorldToScreenPoint(f.Position + Vector3.up * (f.Height + 0.25f)), Vector2.zero);
            }

            // 기세 액션 판
            Fighter ht = null;
            bool can = pc.HeatAct != null && pc.HeatAct.Available(out _, out ht) && ht != null && cam != null && InFront(cam, ht.Position);
            if (can)
            {
                if (!plate.activeSelf) { plate.SetActive(true); plateA = 0f; }
                plateA = Mathf.MoveTowards(plateA, 1f, rdt / 0.15f);
                plateGroup.alpha = plateA;
                plateText.text = PadLast ? "△ 기세" : "우클릭 기세";
                var at = cam.WorldToScreenPoint(ht.Chest);
                Place((RectTransform)plate.transform, at, new Vector2(70f, 0f));
            }
            else if (plate.activeSelf) plate.SetActive(false);

            // 7-4 화면 밖 화살표: 공격권 가진 적
            Fighter who = null; bool tele = false;
            foreach (var b in FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None))
                if (b.HasToken && b.Me != null && !b.Eliminated) { who = b.Me; tele = b.Me.Telegraphing; break; }
            bool off = who != null && cam != null && Offscreen(cam, who.Chest);
            if (arrowBack.gameObject.activeSelf != off) arrowBack.gameObject.SetActive(off);
            if (off)
            {
                if (tele && (!lastTelegraph || who != lastArrowWho)) arrowPulse = 0f;
                EdgeArrow(cam, who.Chest);
                float s = 1f;
                if (arrowPulse >= 0f)
                {
                    arrowPulse += rdt;
                    float ph = arrowPulse / 0.2f;
                    s = ph < 2f ? 1f + 0.4f * Mathf.Sin(Mathf.Repeat(ph, 1f) * Mathf.PI) : 1f;
                    if (ph >= 2f) arrowPulse = -1f;
                }
                arrowBack.rectTransform.localScale = Vector3.one * s;
            }
            lastTelegraph = tele;
            lastArrowWho = who;

            // 처음 '!!'
            if (!tipDanger)
                foreach (var f in Fighter.All)
                    if (f != me && f.Run != null && f.Run.Move.Warn >= 2 && f.Telegraphing) { tipDanger = true; DebugHud.Toast(PadLast ? "피해!(×)" : "피해!(Space)", 2.5f); break; }
        }

        void Hide()
        {
            Visible = false;
            if (canvas != null) canvas.gameObject.SetActive(false);
            if (lockRing != null) lockRing.gameObject.SetActive(false);
            if (softRing != null) softRing.gameObject.SetActive(false);
            GameUi.HintOverride = null;
        }

        float Scale() => canvas != null ? canvas.scaleFactor : 1f;

        static bool InFront(Camera cam, Vector3 p) => cam.WorldToViewportPoint(p).z > 0.1f;

        /// 화면 밖(가슴이 뷰포트 밖 — 공격권 판정의 '화면 안'(가장자리 여유 포함)보다 엄격: 가장자리에 반쯤 보이는 적엔 화살표 없음)
        static bool Offscreen(Camera cam, Vector3 p)
        {
            var v = cam.WorldToViewportPoint(p);
            return v.z <= 0f || v.x < 0f || v.x > 1f || v.y < 0f || v.y > 1f;
        }

        void Place(RectTransform r, Vector3 screen, Vector2 offset)
        {
            float k = Scale();
            r.anchoredPosition = new Vector2(screen.x / k, screen.y / k) + offset;
        }

        void EdgeArrow(Camera cam, Vector3 world)
        {
            var v = cam.WorldToViewportPoint(world);
            var d = new Vector2(v.x - 0.5f, v.y - 0.5f);
            if (v.z < 0f) d = -d;
            if (d.sqrMagnitude < 1e-6f) d = Vector2.down;
            float k = Scale();
            float w = Screen.width / k, h = Screen.height / k, m = 64f;
            float sx = (w * 0.5f - m) / Mathf.Max(1e-4f, Mathf.Abs(d.x * w)), sy = (h * 0.5f - m) / Mathf.Max(1e-4f, Mathf.Abs(d.y * h));
            float s = Mathf.Min(sx, sy);
            var p = new Vector2(w * 0.5f + d.x * w * s, h * 0.5f + d.y * h * s);
            arrowBack.rectTransform.anchoredPosition = p;
            float ang = Mathf.Atan2(d.y * h, d.x * w) * Mathf.Rad2Deg;
            arrowBack.rectTransform.localRotation = Quaternion.Euler(0f, 0f, ang + 90f);      // 역삼각형 꼭짓점(아래)이 적 쪽
        }

        void Floor(Transform ring, MeshRenderer r, Fighter f, float size, float alpha)
        {
            if (ring == null) return;
            bool on = f != null && f.Targetable && Kit != null && Kit.Ring != null;
            if (ring.gameObject.activeSelf != on) ring.gameObject.SetActive(on);
            if (!on) return;
            ring.position = f.Position + Vector3.up * 0.03f;
            ring.rotation = Quaternion.Euler(90f, t * 5f, 0f);
            ring.localScale = new Vector3(size, size, 1f);
            mpb.Clear();
            mpb.SetTexture("_MainTex", Kit.Ring);
            mpb.SetColor("_Color", new Color(Ink.r, Ink.g, Ink.b, alpha));
            r.SetPropertyBlock(mpb);
        }

        // ───────────────────────── 부품
        static RectTransform Rect(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = 5 };
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = aMin; r.anchorMax = aMax; r.pivot = pivot;
            r.anchoredPosition = pos; r.sizeDelta = size;
            return r;
        }

        static Image Img(string name, Transform parent, Texture2D tex, Color c, Vector2 pos, Vector2 size, bool center = false, bool bottom = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)) { layer = 5 };
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            var a = center ? new Vector2(0.5f, bottom ? 0f : 0.5f) : new Vector2(0f, 1f);
            r.anchorMin = r.anchorMax = r.pivot = a;
            if (center && parent is RectTransform pr && pr.GetComponent<Canvas>() != null) { r.anchorMin = r.anchorMax = Vector2.zero; }
            r.anchoredPosition = pos; r.sizeDelta = size;
            var img = go.GetComponent<Image>();
            if (tex != null) img.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        static void Filled(Image img)
        {
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillOrigin = (int)Image.OriginHorizontal.Left;
            img.fillAmount = 1f;
        }

        static TextMeshProUGUI Text(string name, Transform parent, TMP_FontAsset font, float size, Color c, TextAlignmentOptions align, Vector2 pos, Vector2 box, bool bottom = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)) { layer = 5 };
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            var a = bottom ? new Vector2(0.5f, 0f) : new Vector2(0f, 1f);
            r.anchorMin = r.anchorMax = a;
            r.pivot = bottom ? new Vector2(0f, 0f) : new Vector2(0f, 1f);
            r.anchoredPosition = pos; r.sizeDelta = box;
            var t = go.GetComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.color = c;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            return t;
        }

        static void Outline(TextMeshProUGUI t, float w)
        {
            t.outlineWidth = w;
            t.outlineColor = Ink;
        }
    }
}
