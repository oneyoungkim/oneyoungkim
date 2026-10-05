// 행인1의 메인이벤트 — 무대 흐름 화면(docs/08_M2_전투_설계.md 2-5·7-2·7-5·8장, 11장 13·14단계)
// 인카운터·야차가 부른다: 머리 위 자막 · 배너 「시비 붙음!」 · 큰 붓 글자 「정리.」 · 이름 카드(야차) · 결과 카드(오른쪽 아래 2.5초) ·
// 패배 화면(먹이 번지며 「…일어나.」 + 다시/그만) · 대화 판(심판 형 — 들어간다/다음에) · 상호작용 안내(× / E) · 야차 큰 바(이름 + 다운 점 3개).
// 글꼴 KR_Bold_SDF. 패배 화면·대화 판이 떠 있는 동안 GameState.Modal(일시정지 토글 막음 — Esc 가 '그만'·'다음에').
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Haengin
{
    [DefaultExecutionOrder(95), DisallowMultipleComponent]
    public sealed class StageHud : MonoBehaviour
    {
        public FxKit Kit;
        public TMP_FontAsset Font;
        public InputActionAsset Actions;

        public static StageHud Instance { get; private set; }
        static readonly Color Ink = new Color(0.102f, 0.078f, 0.090f), Paper = new Color(0.957f, 0.937f, 0.902f);

        // ── 시험·녹화가 읽는 것
        public string LastBanner { get; private set; } = "";
        public string LastBigWord { get; private set; } = "";
        public string LastResult { get; private set; } = "";
        public int ResultCount { get; private set; }
        public string LastSubtitle { get; private set; } = "";
        public bool DefeatShown => defeat != null && defeat.activeSelf;
        public bool DialogShown => dialog != null && dialog.activeSelf;
        public bool NameCardShown => nameCard != null && nameCard.activeSelf;
        public bool ResultShown => result != null && result.activeSelf;
        public bool YachaBarShown => ybar != null && ybar.activeSelf;
        public float YachaBarValue => yFill != null ? yFill.fillAmount : 0f;
        public string PromptText => prompt != null && prompt.activeSelf ? promptText.text : "";

        Canvas canvas, top;
        RectTransform root, topRoot;
        GameObject banner, bigWord, nameCard, result, defeat, dialog, prompt, ybar;
        TextMeshProUGUI bannerText, bigText, cardSmall, cardBig, resultText, defeatLine, defeatHint, dialogWho, dialogText, promptText, yName;
        TextMeshProUGUI[] dialogOpt = new TextMeshProUGUI[2];
        Image[] dialogOptPlate = new Image[2];
        Image defeatInk, yFill;
        Image[] yDots = new Image[3];
        float bannerLeft, bigLeft, cardLeft, resultLeft, defeatT;
        Action<bool> dialogDone;
        Action defeatRetry, defeatQuit;
        int dialogSel;
        bool prevSub, prevCan, prevEsc, prevNav;
        Fighter yFighter;

        sealed class Sub { public GameObject Go; public TextMeshProUGUI Text; public Transform Anchor; public float Height, Left; }
        readonly List<Sub> subs = new List<Sub>();

        void OnEnable() { Instance = this; }
        void OnDisable() { if (Instance == this) Instance = null; if (GameState.Modal && (DefeatShown || DialogShown)) GameState.Modal = false; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }

        // ───────────────────────── 부르는 것
        public void Subtitle(Transform anchor, float height, string text, float secs)
        {
            Build();
            var s = subs.Find(x => x.Anchor == anchor) ?? subs.Find(x => x.Left <= 0f);
            if (s == null)
            {
                var go = Plate("자막", root, new Color(Ink.r, Ink.g, Ink.b, 0.78f), out var t, 24f, Paper);
                s = new Sub { Go = go, Text = t };
                subs.Add(s);
            }
            s.Anchor = anchor; s.Height = height; s.Left = secs;
            s.Text.text = text;
            s.Go.SetActive(true);
            LastSubtitle = text;
            Debug.Log($"[Stage] 자막: {text}");
        }

        public void Banner(string text, float secs) { Build(); bannerText.text = text; bannerLeft = secs; banner.SetActive(true); LastBanner = text; }
        public void BigWord(string text, float secs) { Build(); bigText.text = text; bigLeft = secs; bigWord.SetActive(true); LastBigWord = text; }
        public void NameCard(string small, string big, float secs) { Build(); cardSmall.text = small; cardBig.text = big; cardLeft = secs; nameCard.SetActive(true); }

        public void Result(string text, float secs)
        {
            Build();
            resultText.text = text; resultLeft = secs; result.SetActive(true);
            LastResult = text; ResultCount++;
            Debug.Log($"[Stage] 결과 카드: {text}");
        }

        public void Defeat(Action retry, Action quit)
        {
            Build();
            defeatRetry = retry; defeatQuit = quit;
            defeatT = 0f;
            defeat.SetActive(true);
            GameState.Modal = true;
            prevSub = prevCan = prevEsc = true;     // 떠 있는 버튼으로 바로 고르지 않게
            Debug.Log("[Stage] 패배 화면");
        }

        /// 패배 화면 고르기(시험·입력): true = 다시
        public void DefeatChoose(bool retry)
        {
            if (!DefeatShown) return;
            defeat.SetActive(false);
            GameState.Modal = false;
            var a = retry ? defeatRetry : defeatQuit;
            defeatRetry = defeatQuit = null;
            a?.Invoke();
        }

        public void Dialog(string who, string text, string yes, string no, Action<bool> done)
        {
            Build();
            dialogWho.text = who; dialogText.text = text;
            dialogOpt[0].text = yes; dialogOpt[1].text = no;
            dialogSel = 0;
            dialogDone = done;
            dialog.SetActive(true);
            GameState.Modal = true;
            prevSub = prevCan = prevEsc = true;
            RefreshDialog();
        }

        /// 대화 판 고르기(시험·입력): true = 첫 항목(들어간다)
        public void Choose(bool yes)
        {
            if (!DialogShown) return;
            dialog.SetActive(false);
            GameState.Modal = false;
            var d = dialogDone;
            dialogDone = null;
            d?.Invoke(yes);
        }

        public void YachaBar(Fighter f, string name)
        {
            Build();
            yFighter = f; yName.text = name;
            ybar.SetActive(f != null);
        }

        public void YachaDowns(int downs) { Build(); for (int i = 0; i < yDots.Length; i++) yDots[i].color = i < downs ? Ink : new Color(Paper.r, Paper.g, Paper.b, 0.9f); }

        public void HideAll()
        {
            if (canvas == null) return;
            foreach (var s in subs) { s.Left = 0f; s.Go.SetActive(false); }
            banner.SetActive(false); bigWord.SetActive(false); nameCard.SetActive(false); result.SetActive(false); ybar.SetActive(false);
        }

        // ───────────────────────── 한 프레임
        void LateUpdate()
        {
            if (canvas == null) { if (Interactable.All.Count == 0) return; Build(); }
            // 실제 시간: 다른 HUD 와 같은 TimeFx 시계(테스트·녹화의 고정 프레임에서도 같은 길이 — unscaledDeltaTime 은 녹화 중 0 에 가까워 배너가 안 꺼졌음)
            float rdt = TimeFx.RealDt > 0f ? TimeFx.RealDt : Time.unscaledDeltaTime;
            var cam = Camera.main;
            float k = canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            PlaceSubs(cam, k, rdt);
            Tick(banner, ref bannerLeft, rdt);
            Tick(bigWord, ref bigLeft, rdt, fade: 0.4f, cg: bigWord.GetComponent<CanvasGroup>());
            Tick(nameCard, ref cardLeft, rdt);
            Tick(result, ref resultLeft, rdt);

            // 야차 큰 바
            if (ybar.activeSelf && yFighter != null) yFill.fillAmount = Mathf.Clamp01(yFighter.MaxHp > 0 ? yFighter.Hp / (float)yFighter.MaxHp : 0f);

            // 상호작용 안내(탐색 중, 창이 없을 때)
            var pc = GetComponentInParent<PlayerCombat>();
            Interactable near = null;
            if (pc != null && !pc.Active && !GameState.Modal && !GameState.InputLocked) near = Interactable.Nearest(pc.transform.position);
            bool showP = near != null;
            if (prompt.activeSelf != showP) prompt.SetActive(showP);
            if (showP) promptText.text = (CombatHud.PadLast ? "× " : "E ") + near.Prompt;

            // 패배 화면: 먹이 1초에 번짐 → 「…일어나.」 → 다시 / 그만
            if (DefeatShown)
            {
                defeatT += rdt;
                float u = Mathf.Clamp01(defeatT / 1.0f);
                defeatInk.color = new Color(Ink.r, Ink.g, Ink.b, 0.9f * u);
                defeatInk.rectTransform.localScale = Vector3.one * (1.6f - 0.6f * u);
                defeatLine.alpha = Mathf.Clamp01((defeatT - 0.6f) / 0.4f);
                defeatHint.alpha = Mathf.Clamp01((defeatT - 1.0f) / 0.3f);
                defeatHint.text = CombatHud.PadLast ? "× 다시      ○ 그만" : "Enter 다시      Esc 그만";
                if (defeatT > 1.0f)
                {
                    if (Pressed(Submit, ref prevSub)) DefeatChoose(true);
                    else if (Pressed(Cancel, ref prevCan) || Esc(ref prevEsc)) DefeatChoose(false);
                }
                else { Pressed(Submit, ref prevSub); Pressed(Cancel, ref prevCan); Esc(ref prevEsc); }
            }
            if (DialogShown)
            {
                var nav = Act("Navigate");
                float x = nav != null ? nav.ReadValue<Vector2>().x : 0f;
                bool side = Mathf.Abs(x) > 0.5f;
                if (side && !prevNav) { dialogSel = 1 - dialogSel; RefreshDialog(); }
                prevNav = side;
                if (Pressed(Submit, ref prevSub)) Choose(dialogSel == 0);
                else if (Pressed(Cancel, ref prevCan) || Esc(ref prevEsc)) Choose(false);
            }
        }

        /// 머리 위 자막: 말한 사람 머리 위. 둘이 겹치면 나중 것을 위로 쌓고, 머리가 화면 밖이면 아래 가운데(조작 안내 위)에 쌓는다. 화면 안으로 가둠.
        readonly List<Rect> placed = new List<Rect>();
        void PlaceSubs(Camera cam, float k, float rdt)
        {
            placed.Clear();
            float W = root.rect.width, H = root.rect.height;
            float lowY = 200f;
            foreach (var s in subs)
            {
                if (s.Left <= 0f) { if (s.Go.activeSelf) s.Go.SetActive(false); continue; }
                s.Left -= rdt;
                if (s.Anchor == null || cam == null) { s.Go.SetActive(false); continue; }
                if (!s.Go.activeSelf) s.Go.SetActive(true);
                var rt = (RectTransform)s.Go.transform;
                float w = rt.rect.width > 1f ? rt.rect.width : s.Text.preferredWidth + 44f;
                float h = rt.rect.height > 1f ? rt.rect.height : s.Text.preferredHeight + 22f;
                var sp = cam.WorldToScreenPoint(s.Anchor.position + Vector3.up * s.Height);
                bool onScreen = sp.z > 0.1f && sp.x >= 0f && sp.x <= cam.pixelWidth && sp.y >= 0f && sp.y <= cam.pixelHeight;
                Vector2 p = onScreen ? new Vector2(sp.x / k, sp.y / k) : new Vector2(W * 0.5f, lowY);
                p.x = Mathf.Clamp(p.x, w * 0.5f + 16f, Mathf.Max(w * 0.5f + 16f, W - w * 0.5f - 16f));
                p.y = Mathf.Clamp(p.y, 16f, Mathf.Max(16f, H - h - 16f));
                for (int guard = 0; guard < 6; guard++)
                {
                    var r = new Rect(p.x - w * 0.5f, p.y, w, h);
                    bool hit = false;
                    foreach (var q in placed) if (q.Overlaps(r)) { p.y = q.yMax + 6f; hit = true; break; }
                    if (!hit) break;
                }
                placed.Add(new Rect(p.x - w * 0.5f, p.y, w, h));
                rt.anchoredPosition = p;
            }
        }

        static void Tick(GameObject go, ref float left, float rdt, float fade = 0f, CanvasGroup cg = null)
        {
            if (left <= 0f) { if (go.activeSelf) go.SetActive(false); return; }
            left -= rdt;
            if (cg != null) cg.alpha = fade > 0f ? Mathf.Clamp01(left / fade) : 1f;
            if (left <= 0f) go.SetActive(false);
        }

        InputAction Act(string n) => Actions != null ? Actions.FindAction("Menu/" + n, false) : null;
        InputAction Submit => Act("Submit");
        InputAction Cancel => Act("Cancel");
        static bool Pressed(InputAction a, ref bool prev) => a != null && GameUi.Edge(a, ref prev);
        static bool Esc(ref bool prev)
        {
            var kb = Keyboard.current;
            bool p = kb != null && kb.escapeKey.isPressed;
            bool e = p && !prev;
            prev = p;
            return e;
        }

        void RefreshDialog()
        {
            for (int i = 0; i < 2; i++)
            {
                bool sel = i == dialogSel;
                dialogOptPlate[i].color = sel ? Paper : new Color(Ink.r, Ink.g, Ink.b, 0.7f);
                dialogOpt[i].color = sel ? Ink : Paper;
            }
        }

        // ───────────────────────── 만들기
        void Build()
        {
            if (canvas != null) return;
            if (Font == null && Kit != null) Font = Kit.Font;
            if (Actions == null)
            {
                var pi = GetComponentInParent<PInput>();
                if (pi != null) Actions = pi.Actions;
                else if (GameUi.Instance != null) Actions = GameUi.Instance.Actions;
            }
            canvas = MakeCanvas("무대 화면", 36, out root);
            top = MakeCanvas("무대 화면(위)", 45, out topRoot);

            banner = Plate("배너", root, Ink, out bannerText, 54f, Paper);
            Anchor(banner, new Vector2(0.5f, 1f), new Vector2(0f, -170f));
            banner.SetActive(false);

            bigWord = new GameObject("큰 붓 글자", typeof(RectTransform), typeof(CanvasGroup)) { layer = 5 };
            bigWord.transform.SetParent(root, false);
            Anchor(bigWord, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f));
            // 종이색 붓 획 판(어두운 배경에서도 먹 글자가 읽히게 — 기세 MAX 붓 획과 같은 그림)
            if (Kit != null && Kit.Brush != null)
            {
                var bgo = new GameObject("붓 판", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage)) { layer = 5 };
                bgo.transform.SetParent(bigWord.transform, false);
                var br = (RectTransform)bgo.transform; br.sizeDelta = new Vector2(1040f, 500f); br.anchoredPosition = new Vector2(-10f, -16f);
                var ri = bgo.GetComponent<RawImage>(); ri.texture = Kit.Brush; ri.color = new Color(Paper.r, Paper.g, Paper.b, 0.92f); ri.raycastTarget = false;
            }
            bigText = Text(bigWord.transform, 170f, Ink, new Vector2(900f, 240f));
            bigText.outlineWidth = 0.22f; bigText.outlineColor = Paper;
            bigWord.SetActive(false);

            nameCard = new GameObject("이름 카드", typeof(RectTransform)) { layer = 5 };
            nameCard.transform.SetParent(root, false);
            Anchor(nameCard, new Vector2(0.5f, 0.5f), new Vector2(0f, 120f));
            var ncPlate = Img(nameCard.transform, Ink, new Vector2(960f, 190f), Vector2.zero);
            ncPlate.color = new Color(Ink.r, Ink.g, Ink.b, 0.86f);
            cardSmall = Text(nameCard.transform, 30f, Paper, new Vector2(900f, 44f)); cardSmall.rectTransform.anchoredPosition = new Vector2(0f, 48f);
            cardBig = Text(nameCard.transform, 72f, Color.white, new Vector2(940f, 96f)); cardBig.rectTransform.anchoredPosition = new Vector2(0f, -18f);
            nameCard.SetActive(false);

            result = Plate("결과 카드", root, Paper, out resultText, 30f, Ink);
            Anchor(result, new Vector2(1f, 0f), new Vector2(-48f, 120f), pivot: new Vector2(1f, 0f));
            result.SetActive(false);

            prompt = Plate("상호작용", root, new Color(Ink.r, Ink.g, Ink.b, 0.85f), out promptText, 26f, Paper);
            Anchor(prompt, new Vector2(0.5f, 0f), new Vector2(0f, 150f));
            prompt.SetActive(false);

            // 야차 큰 바(위 가운데 600×14) + 이름 + 다운 점 3개
            ybar = new GameObject("야차 바", typeof(RectTransform)) { layer = 5 };
            ybar.transform.SetParent(root, false);
            Anchor(ybar, new Vector2(0.5f, 1f), new Vector2(0f, -70f));
            yName = Text(ybar.transform, 28f, Color.white, new Vector2(700f, 40f)); yName.rectTransform.anchoredPosition = new Vector2(0f, 26f);
            yName.outlineWidth = 0.25f; yName.outlineColor = Ink;
            var yBack = Img(ybar.transform, Ink, new Vector2(606f, 20f), new Vector2(0f, -6f));
            yFill = Img(yBack.transform, Color.white, new Vector2(600f, 14f), Vector2.zero);
            yFill.type = Image.Type.Filled; yFill.fillMethod = Image.FillMethod.Horizontal; yFill.fillOrigin = 0;
            for (int i = 0; i < 3; i++) { yDots[i] = Img(ybar.transform, Paper, new Vector2(16f, 16f), new Vector2(-24f + 24f * i, -34f)); }
            ybar.SetActive(false);

            // 패배 화면(맨 위 캔버스)
            defeat = new GameObject("패배 화면", typeof(RectTransform)) { layer = 5 };
            defeat.transform.SetParent(topRoot, false);
            var dr = (RectTransform)defeat.transform; dr.anchorMin = Vector2.zero; dr.anchorMax = Vector2.one; dr.offsetMin = dr.offsetMax = Vector2.zero;
            defeatInk = Img(defeat.transform, Ink, Vector2.zero, Vector2.zero);
            var ir = defeatInk.rectTransform; ir.anchorMin = Vector2.zero; ir.anchorMax = Vector2.one; ir.offsetMin = ir.offsetMax = Vector2.zero;
            defeatLine = Text(defeat.transform, 64f, Paper, new Vector2(1000f, 100f)); defeatLine.rectTransform.anchoredPosition = new Vector2(0f, 40f);
            defeatLine.text = "…일어나.";
            defeatHint = Text(defeat.transform, 30f, Paper, new Vector2(1000f, 50f)); defeatHint.rectTransform.anchoredPosition = new Vector2(0f, -70f);
            defeat.SetActive(false);

            // 대화 판(아래 가운데)
            dialog = new GameObject("대화", typeof(RectTransform)) { layer = 5 };
            dialog.transform.SetParent(topRoot, false);
            Anchor(dialog, new Vector2(0.5f, 0f), new Vector2(0f, 120f), pivot: new Vector2(0.5f, 0f));
            var dp = Img(dialog.transform, new Color(Ink.r, Ink.g, Ink.b, 0.9f), new Vector2(1200f, 230f), new Vector2(0f, 115f));
            dialogWho = Text(dialog.transform, 26f, new Color(0.886f, 0.345f, 0.173f), new Vector2(1140f, 36f)); dialogWho.alignment = TextAlignmentOptions.Left;
            dialogWho.rectTransform.anchoredPosition = new Vector2(0f, 200f);
            dialogText = Text(dialog.transform, 30f, Paper, new Vector2(1140f, 90f)); dialogText.alignment = TextAlignmentOptions.TopLeft;
            dialogText.textWrappingMode = TextWrappingModes.Normal;
            dialogText.rectTransform.anchoredPosition = new Vector2(0f, 135f);
            for (int i = 0; i < 2; i++)
            {
                dialogOptPlate[i] = Img(dialog.transform, Paper, new Vector2(260f, 52f), new Vector2(-150f + 300f * i, 42f));
                dialogOpt[i] = Text(dialogOptPlate[i].transform, 28f, Ink, new Vector2(260f, 52f));
            }
            dialog.SetActive(false);
        }

        Canvas MakeCanvas(string name, int order, out RectTransform r)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = 5 };
            go.transform.SetParent(transform, false);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = order;
            var sc = go.AddComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920f, 1080f);
            sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            sc.matchWidthOrHeight = 1f;
            r = (RectTransform)go.transform;
            return c;
        }

        static void Anchor(GameObject go, Vector2 a, Vector2 pos, Vector2? pivot = null)
        {
            var r = (RectTransform)go.transform;
            r.anchorMin = r.anchorMax = a;
            r.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            r.anchoredPosition = pos;
        }

        GameObject Plate(string name, Transform parent, Color c, out TextMeshProUGUI t, float size, Color tc)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)) { layer = 5 };
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = r.anchorMax = Vector2.zero;
            r.pivot = new Vector2(0.5f, 0f);
            var img = go.GetComponent<Image>(); img.color = c; img.raycastTarget = false;
            var lay = go.AddComponent<HorizontalLayoutGroup>();
            lay.padding = new RectOffset(22, 22, 10, 12);
            lay.childAlignment = TextAnchor.MiddleCenter;
            lay.childControlWidth = lay.childControlHeight = true;
            lay.childForceExpandWidth = lay.childForceExpandHeight = false;
            var fit = go.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            t = Text(go.transform, size, tc, Vector2.zero);
            return go;
        }

        Image Img(Transform parent, Color c, Vector2 size, Vector2 pos)
        {
            var go = new GameObject("판", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)) { layer = 5 };
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = size; r.anchoredPosition = pos;
            var img = go.GetComponent<Image>(); img.color = c; img.raycastTarget = false;
            return img;
        }

        TextMeshProUGUI Text(Transform parent, float size, Color c, Vector2 box)
        {
            var go = new GameObject("글", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)) { layer = 5 };
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = box;
            var t = go.GetComponent<TextMeshProUGUI>();
            if (Font != null) t.font = Font;
            t.fontSize = size; t.color = c;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            return t;
        }
    }
}
