// 행인1의 메인이벤트 — 오픈월드 화면(docs/13 3-1 '화면' · B안 「중계 화면」 docs/16): 왼쪽 위 상태 판(날짜·시각 / 지갑·생활비·피로) + 목표 띠,
// 오른쪽 위 '다음' 안내 판(길잡이 거리 — 위 가운데 길잡이 띠와 합침), 왼쪽 아래 알림(문자 = 이름 머리말 + Warm 줄, 나머지 = 주황 줄, 쌓임).
// 카드 본편·활동 화면·휴대폰이 떠 있으면 판을 숨긴다(Shown = false). 글꼴 = UiFonts(B안: Medium·Black·Barlow).
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Haengin
{
    [DisallowMultipleComponent]
    public sealed class WorldHud : MonoBehaviour
    {
        /// HUD 에 넘기는 값(WorldRunner.UpdateHud)
        public struct Info
        {
            public string Date, DateExtra, Time;      // "3월 3일 화" · "저녁 · 비" · "16:30"
            public long Wallet, Envelope, Household, Due;
            public bool EnvelopeKnown;
            public int Fatigue;
            public string Goal, GoalCount;            // "하늘이 생일까지 버틴다" · "1/4"
            public string NextTitle, NextTime, NextPlace;
        }

        public UiFonts Fonts;
        public bool Shown;
        /// 지금 장면의 세계 HUD(길잡이 띠가 '다음' 판과 겹치지 않게 — RouteHud)
        public static WorldHud Live { get; private set; }
        public string DateLine { get; private set; } = "";
        public string MoneyLine { get; private set; } = "";
        public string GoalLine { get; private set; } = "";
        public string NextLine { get; private set; } = "";
        public float FatigueFill { get; private set; }
        public readonly List<string> Toasts = new List<string>();
        public int ToastCount { get; private set; }
        /// 아직 안 뜬 알림 + 떠 있는 알림 수(시험)
        public int PendingToasts => pending.Count + live.Count;
        /// 오른쪽 위 '다음' 판이 이번 프레임에 보이나(Update 에서 정해짐 — 길잡이 띠는 이때 숨김)
        public bool NextShown => Shown && hasNext && !BoardCenter();
        static bool BoardCenter() { var b = WorldRunner.Instance != null && WorldRunner.Instance.Story != null ? WorldRunner.Instance.Story.Board : null; return b != null && b.Shown && !b.Side; }
        public RectTransform NextRect => next != null ? next.Plate.rectTransform : null;

        Image plate, goalPlate, fatFill;
        TextMeshProUGUI dateText, dateExtra, timeText, walletText, envText, houseText, dueText, fatText, fatState, goalText, goalCount;
        UiKit.NextPlate next;
        RectTransform toastRoot, buffRoot;
        readonly List<(Image p, TextMeshProUGUI t)> chips = new List<(Image, TextMeshProUGUI)>();
        public string BuffLine { get; private set; } = "";
        readonly List<(TextMeshProUGUI t, Image p, float left)> live = new List<(TextMeshProUGUI, Image, float)>();
        readonly Queue<string> pending = new Queue<string>();
        float gap;
        bool hasNext;
        string nextHead = "", nextTitle = "", nextTime = "", nextPlace = "", lastPlace, lastTime;
        int lastDist = -2;

        void Build()
        {
            if (plate != null) return;
            UiKit.Canvas(gameObject, 28);
            var f = Fonts;
            var tl = new Vector2(0f, 1f);
            // 상태 판(위 주황 줄)
            plate = UiKit.Plate("세계 판", transform, tl, tl, new Vector2(38f, -38f), new Vector2(624f, 197f), UiKit.Edge.Top);
            var pt = plate.transform;
            dateText = UiKit.TextAt("날짜", pt, UiKit.FBlack(f), 29f, UiKit.Paper, new Vector2(24f, -22f), new Vector2(380f, 52f), TextAlignmentOptions.BaselineLeft);
            dateExtra = UiKit.TextAt("날씨", pt, UiKit.FMed(f), 20f, UiKit.PaperDim, new Vector2(150f, -22f), new Vector2(260f, 52f), TextAlignmentOptions.BaselineLeft);
            timeText = UiKit.Text("시각", pt, UiKit.FNum(f), 50f, UiKit.Accent, TextAlignmentOptions.BaselineRight);
            UiKit.Place(timeText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(200f, 62f));
            UiKit.Rule(pt, new Vector2(24f, -67f), 576f);
            // 머리말 글 상자(TMP 는 글꼴 ascender~descender 로 잼)가 값(Barlow 50) 상자와 닿지 않게 -71
            UiKit.Label("지갑 머리말", pt, f, new Vector2(24f, -71f), 140f).text = "지갑";
            UiKit.Label("생활비 머리말", pt, f, new Vector2(HouseX, -71f), 200f).text = "생활비";
            UiKit.Label("피로 머리말", pt, f, new Vector2(FatX, -71f), 140f).text = "피로";
            walletText = UiKit.TextAt("지갑", pt, UiKit.FNum(f), 50f, UiKit.Paper, new Vector2(24f, -118f), new Vector2(140f, 64f), TextAlignmentOptions.BaselineLeft);
            envText = UiKit.TextAt("봉투", pt, UiKit.FNum(f), 22f, UiKit.PaperDim, new Vector2(24f, -170f), new Vector2(140f, 28f), TextAlignmentOptions.BaselineLeft);
            houseText = UiKit.TextAt("생활비", pt, UiKit.FNum(f), 50f, UiKit.Paper, new Vector2(HouseX, -118f), new Vector2(150f, 64f), TextAlignmentOptions.BaselineLeft);
            dueText = UiKit.TextAt("생활비 목표", pt, UiKit.FNum(f), 29f, UiKit.PaperDim, new Vector2(300f, -118f), new Vector2(150f, 64f), TextAlignmentOptions.BaselineLeft);
            fatText = UiKit.TextAt("피로 수", pt, UiKit.FNum(f), 50f, UiKit.Paper, new Vector2(FatX, -118f), new Vector2(70f, 64f), TextAlignmentOptions.BaselineLeft);
            fatState = UiKit.TextAt("피로 상태", pt, UiKit.FMed(f), 20f, UiKit.Warm, new Vector2(FatX + 48f, -118f), new Vector2(100f, 64f), TextAlignmentOptions.BaselineLeft);
            fatFill = UiKit.Bar("피로", pt, new Vector2(FatX, -158f), new Vector2(600f - FatX, 6f), UiKit.Accent, out _);
            // 목표 띠
            goalPlate = UiKit.Plate("목표 띠", transform, tl, tl, new Vector2(38f, -252f), new Vector2(624f, 48f));
            goalText = UiKit.Text("목표", goalPlate.transform, UiKit.FMed(f), 20f, UiKit.PaperDim, TextAlignmentOptions.MidlineLeft);
            UiKit.Stretch(goalText.rectTransform); goalText.rectTransform.offsetMin = new Vector2(24f, 0f); goalText.rectTransform.offsetMax = new Vector2(-96f, 0f);
            goalText.overflowMode = TextOverflowModes.Ellipsis;
            goalCount = UiKit.Text("목표 수", goalPlate.transform, UiKit.FNum(f), 29f, UiKit.Paper, TextAlignmentOptions.MidlineRight);
            UiKit.Place(goalCount.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(80f, 46f));
            buffRoot = UiKit.Node("효과 아이콘", transform);
            UiKit.Place(buffRoot, tl, tl, new Vector2(38f, -310f), new Vector2(624f, 40f));
            // '다음' 판(오른쪽 위)
            next = UiKit.NextPlate.Make(transform, f, "다음 판");
            next.Plate.gameObject.SetActive(false);
            toastRoot = UiKit.Node("알림", transform);
            UiKit.Place(toastRoot, Vector2.zero, Vector2.zero, new Vector2(0f, 120f), new Vector2(760f, 440f));
        }

        void Awake() { Build(); Live = this; }
        void OnDestroy() { if (Live == this) Live = null; }

        Info last;
        bool hasLast;

        static bool Same(in Info a, in Info b) =>
            a.Date == b.Date && a.DateExtra == b.DateExtra && a.Time == b.Time && a.Wallet == b.Wallet && a.Envelope == b.Envelope && a.EnvelopeKnown == b.EnvelopeKnown &&
            a.Household == b.Household && a.Due == b.Due && a.Fatigue == b.Fatigue && a.Goal == b.Goal && a.GoalCount == b.GoalCount &&
            a.NextTitle == b.NextTitle && a.NextTime == b.NextTime && a.NextPlace == b.NextPlace;

        public void Set(in Info i)
        {
            Build();
            // 매 프레임 불림 — 값이 같으면 글·폭 계산을 건너뜀(글자 폭 재기가 프레임마다 돌지 않게)
            if (hasLast && Same(i, last)) return;
            last = i; hasLast = true;
            string ex = string.IsNullOrEmpty(i.DateExtra) ? "" : " · " + i.DateExtra;
            DateLine = $"{i.Date} {i.Time}{ex}";
            MoneyLine = $"지갑 {UiKit.Won(i.Wallet)}{(i.EnvelopeKnown ? $" · 봉투 {UiKit.Won(i.Envelope)}" : "")} · 생활비 {UiKit.Won(i.Household)}/{UiKit.Won(i.Due)}";
            GoalLine = string.IsNullOrEmpty(i.GoalCount) ? i.Goal ?? "" : $"{i.Goal} ({i.GoalCount})";
            if (dateText.text != i.Date) { dateText.text = i.Date ?? ""; }
            dateExtra.text = i.DateExtra ?? "";
            var dr = dateExtra.rectTransform;
            dr.anchoredPosition = new Vector2(24f + dateText.GetPreferredValues(dateText.text).x + 12f, dr.anchoredPosition.y);
            timeText.text = UiKit.Tab(i.Time);
            walletText.text = UiKit.Tab(UiKit.Won(i.Wallet));
            UiKit.FitWidth(walletText, HouseX - 24f - 14f, 50f, 34f);      // 큰 지갑(10만 넘음)이 생활비 칸에 닿지 않게
            envText.text = i.EnvelopeKnown ? "봉투 " + UiKit.Tab(UiKit.Won(i.Envelope), 0.46f) : "";
            UiKit.FitWidth(envText, HouseX - 24f - 14f, 22f, 16f);
            houseText.text = UiKit.Tab(UiKit.Won(i.Household));
            dueText.text = "/ " + UiKit.Won(i.Due);
            // 생활비 + '/ 목표' 가 피로 칸 앞(12px)에서 끝나게
            float room = FatX - HouseX - 12f - 8f;
            float hw = UiKit.FitWidth(houseText, 160f, 50f, 34f), dw = UiKit.FitWidth(dueText, room - Mathf.Min(hw, 160f), 29f, 20f);
            var hr = dueText.rectTransform;
            hr.anchoredPosition = new Vector2(HouseX + hw + 8f, hr.anchoredPosition.y);
            hr.sizeDelta = new Vector2(Mathf.Max(dw, 10f) + 4f, hr.sizeDelta.y);
            FatigueFill = Mathf.Clamp01(i.Fatigue / 100f);
            UiKit.SetBar(fatFill, FatigueFill);
            fatFill.color = i.Fatigue >= 85 ? UiKit.EndureLit : UiKit.Accent;      // A6(2026-11-05): 85 이상 = 위험 붉은색(트랙 대비 3:1 넘게 밝힌 값), 그 밖 = 주황
            fatText.text = UiKit.Tab(i.Fatigue.ToString());
            fatState.text = i.Fatigue >= 85 ? "한계" : i.Fatigue >= 70 ? "지침" : "";
            var sr = fatState.rectTransform;
            sr.anchoredPosition = new Vector2(FatX + fatText.GetPreferredValues(fatText.text).x + 8f, sr.anchoredPosition.y);
            goalText.text = i.Goal ?? "";
            goalCount.text = UiKit.Tab(i.GoalCount ?? "");
            goalPlate.gameObject.SetActive(!string.IsNullOrEmpty(i.Goal) && plate.gameObject.activeSelf);
            nextTitle = i.NextTitle ?? ""; nextTime = i.NextTime ?? ""; nextPlace = i.NextPlace ?? "";
            hasNext = nextTitle.Length > 0;
            NextLine = hasNext ? $"다음: {nextTitle}{(nextTime.Length > 0 ? " " + nextTime : "")}{(nextPlace.Length > 0 ? " — " + nextPlace : "")}" : "";
        }

        /// 먹은 효과 아이콘(목표 띠 아래 줄): (글자 한 자 · 남은 시간 글 · 색)
        public void SetBuffs(IList<(string tag, string text, Color color)> list)
        {
            Build();
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < list.Count || i < chips.Count; i++)
            {
                if (i >= chips.Count)
                {
                    var p = UiKit.Plate("효과 " + i, buffRoot, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(130f, 40f));
                    var t = UiKit.Text("글", p.transform, UiKit.FMed(Fonts), 19f, UiKit.Paper, TextAlignmentOptions.MidlineLeft);
                    UiKit.Place(t.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(260f, 38f));
                    chips.Add((p, t));
                }
                var (pl, tx) = chips[i];
                bool on = i < list.Count;
                pl.gameObject.SetActive(on);
                if (!on) continue;
                var (tag, text, col) = list[i];
                tx.text = $"<mark=#{ColorUtility.ToHtmlStringRGB(col)}cc><color=#F4EFE6> {tag} </color></mark> {text}";
                float w = Mathf.Min(280f, tx.preferredWidth + 18f);
                pl.rectTransform.sizeDelta = new Vector2(w, 40f);
                float x = 0f; for (int k = 0; k < i; k++) x += chips[k].p.rectTransform.sizeDelta.x + 8f;
                pl.rectTransform.anchoredPosition = new Vector2(x, 0f);
                sb.Append(tag).Append(' ').Append(text).Append(" | ");
            }
            BuffLine = sb.ToString();
        }

        /// 왼쪽 아래 알림(3.2초, 최대 4줄 — 넘치면 줄 서서 기다림)
        public void Toast(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            Build();
            Toasts.Add(line);
            if (Toasts.Count > 200) Toasts.RemoveAt(0);
            ToastCount++;
            pending.Enqueue(line);
        }

        /// 상태 판 칸 자리(판 왼쪽에서): 지갑 24 · 생활비 · 피로(막대는 판 오른쪽 여백 24 까지)
        const float HouseX = 184f, FatX = 460f;

        /// 알림 판 폭 상한(1080 기준) — 아래 가운데 상호작용 안내(408)와 겹치지 않게
        public const float ToastMaxW = 578f, ToastTextX = 62f;

        void Spawn(string line)
        {
            // 문자(WorldModel.Phone → "문자 — 엄마: …") = 머리말 '문자 · 엄마' + Warm 줄 · 나머지 = 주황 줄 한 줄
            string head = null, body = line;
            if (line.StartsWith("문자 — "))
            {
                int c = line.IndexOf(": ", 5, System.StringComparison.Ordinal);
                if (c > 5) { head = "문자 · " + line.Substring(5, c - 5); body = line.Substring(c + 2); }
            }
            var p = UiKit.Panel("알림 줄", toastRoot, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(ToastMaxW, 72f), UiKit.InkPlate);
            var st = UiKit.Panel("강조 줄", p.transform, Vector2.zero, Vector2.zero, new Vector2(38f, 0f), new Vector2(UiKit.Stripe, 0f), head != null ? UiKit.Warm : UiKit.Accent);
            var sr = st.rectTransform; sr.anchorMin = Vector2.zero; sr.anchorMax = new Vector2(0f, 1f); sr.pivot = new Vector2(0f, 0.5f); sr.sizeDelta = new Vector2(UiKit.Stripe, 0f); sr.anchoredPosition = new Vector2(38f, 0f);
            float bodySize = head != null ? 28f : 24f, top = head != null ? 44f : 16f, wMax = ToastMaxW - ToastTextX - 24f;
            if (head != null) UiKit.Label("머리말", p.transform, Fonts, new Vector2(ToastTextX, -12f), wMax).text = head;
            var t = UiKit.TextAt("글", p.transform, UiKit.FMed(Fonts), bodySize, UiKit.Paper, new Vector2(ToastTextX, -top), new Vector2(wMax, bodySize * 1.5f));
            t.text = body;
            // QA Q08: 긴 알림은 판 안에서 줄 바꿈, 판 높이를 글에 맞춤
            float w = t.GetPreferredValues(body).x, h = bodySize * 1.5f;
            if (w > wMax)
            {
                t.textWrappingMode = TextWrappingModes.Normal;
                h = Mathf.Max(h, t.GetPreferredValues(body, wMax, 0f).y + 4f);
                t.rectTransform.sizeDelta = new Vector2(wMax, h);
            }
            p.rectTransform.sizeDelta = new Vector2(ToastMaxW, Mathf.Max(head != null ? 101f : 72f, top + h + 14f));
            live.Add((t, p, 3.2f));
        }

        void LateUpdate()
        {
            if (plate == null) return;
            // 2026-10-31: 가운데 큰 카드가 뜨면 왼쪽 위 판을 잠깐 숨김(카드 가장자리에 HUD 글자가 반쯤 걸림)
            bool showPlate = Shown && !BoardCenter();
            if (plate.gameObject.activeSelf != showPlate) plate.gameObject.SetActive(showPlate);
            bool goalOn = showPlate && !string.IsNullOrEmpty(goalText.text);
            if (goalPlate.gameObject.activeSelf != goalOn) goalPlate.gameObject.SetActive(goalOn);
            if (buffRoot.gameObject.activeSelf != showPlate) buffRoot.gameObject.SetActive(showPlate);
            NextUpdate();
            // QA Q15: 카드·미니게임 판·휴대폰이 떠 있는 동안(HUD 숨김)에도 알림 줄이 왼쪽 아래에 떠서 판 글자와 겹치던 것 → 그동안 알림을 멈추고 숨김(끝나면 이어서)
            if (toastRoot.gameObject.activeSelf != Shown) toastRoot.gameObject.SetActive(Shown);
            if (!Shown) return;
            float rdt = UiKit.RealDt;
            gap -= rdt;
            if (pending.Count > 0 && live.Count < 4 && gap <= 0f) { Spawn(pending.Dequeue()); gap = 0.25f; }
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var (t, p, left) = live[i];
                left -= rdt;
                if (left <= 0f) { Destroy(p.gameObject); live.RemoveAt(i); continue; }
                live[i] = (t, p, left);
                float a = Mathf.Clamp01(left / 0.4f);
                var cg = p.GetComponent<CanvasGroup>();
                if (a < 1f && cg == null) cg = p.gameObject.AddComponent<CanvasGroup>();
                if (cg != null) cg.alpha = a;
            }
            // 좁은 화면(4:3)에선 아래 가운데 상호작용 안내 위로 올려 쌓음
            var cv = (RectTransform)transform;
            float baseY = cv.rect.width * 0.5f - 214f < ToastMaxW ? 196f : 120f;
            if (Mathf.Abs(toastRoot.anchoredPosition.y - baseY) > 0.5f) toastRoot.anchoredPosition = new Vector2(0f, baseY);
            float y = 0f;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var pr = live[i].p.rectTransform;
                if (Mathf.Abs(pr.anchoredPosition.y - y) > 0.5f) pr.anchoredPosition = new Vector2(0f, y);
                y += pr.sizeDelta.y + 8f;
            }
        }

        /// '다음' 판: 머리말 = 다음 · 장소 · 길잡이 거리(Zone1 에서 목표가 있을 때)
        void NextUpdate()
        {
            bool on = NextShown;
            if (next.Plate.gameObject.activeSelf != on) next.Plate.gameObject.SetActive(on);
            if (!on) return;
            var g = RouteGuide.Current;
            int dm = g != null && g.isActiveAndEnabled && g.CurrentTarget != null ? Mathf.RoundToInt(g.Distance) : -1;
            if (dm == lastDist && next.Title.text == nextTitle && nextPlace == lastPlace && nextTime == lastTime) return;      // 바뀔 때만(매 프레임 문자열 없게)
            lastDist = dm; lastPlace = nextPlace; lastTime = nextTime;
            string head = "다음" + (nextPlace.Length > 0 ? " · " + nextPlace : "") + (dm >= 0 ? $" · {dm}m" : "");
            nextHead = head; next.Set(head, nextTitle, nextTime);
        }

        public void ClearToasts()
        {
            pending.Clear();
            foreach (var l in live) if (l.p != null) Destroy(l.p.gameObject);
            live.Clear();
        }
    }
}
