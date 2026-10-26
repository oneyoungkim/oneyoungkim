// 행인1의 메인이벤트 — 오픈월드 화면(docs/13 3-1 '화면'): 왼쪽 위 날짜·시각·날씨 판 + 지갑·봉투 + 피로 막대 + 목표 한 줄 + '다음' 한 줄, 왼쪽 아래 알림 줄(쌓임).
// 카드 본편·활동 화면·휴대폰이 떠 있으면 판을 숨긴다(Shown = false). 글꼴 = UiFonts(본문·붓).
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Haengin
{
    [DisallowMultipleComponent]
    public sealed class WorldHud : MonoBehaviour
    {
        public UiFonts Fonts;
        public bool Shown;
        public string DateLine => dateText != null ? dateText.text : "";
        public string MoneyLine => moneyText != null ? moneyText.text : "";
        public string GoalLine => goalText != null ? goalText.text : "";
        public string NextLine => nextText != null ? nextText.text : "";
        public float FatigueFill { get; private set; }
        public readonly List<string> Toasts = new List<string>();
        public int ToastCount { get; private set; }
        /// 아직 안 뜬 알림 + 떠 있는 알림 수(시험)
        public int PendingToasts => pending.Count + live.Count;

        Image plate, fatBack, fatFill;
        TextMeshProUGUI dateText, moneyText, fatText, goalText, nextText;
        RectTransform toastRoot, buffRoot;
        readonly List<(Image p, TextMeshProUGUI t)> chips = new List<(Image, TextMeshProUGUI)>();
        public string BuffLine { get; private set; } = "";
        readonly List<(TextMeshProUGUI t, Image p, float left)> live = new List<(TextMeshProUGUI, Image, float)>();
        readonly Queue<string> pending = new Queue<string>();
        float gap;

        void Build()
        {
            if (plate != null) return;
            UiKit.Canvas(gameObject, 28);
            plate = UiKit.Panel("세계 판", transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -24f), new Vector2(560f, 214f), new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.80f));
            var band = UiKit.Panel("주황 띠", plate.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(6f, 214f), UiKit.Accent);
            band.rectTransform.anchorMax = new Vector2(0f, 1f); band.rectTransform.sizeDelta = new Vector2(6f, 0f);
            dateText = UiKit.Text("날짜", plate.transform, Fonts != null ? Fonts.Br : null, 34f, UiKit.Paper, TextAlignmentOptions.Left);
            UiKit.Place(dateText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -12f), new Vector2(520f, 44f));
            moneyText = UiKit.Text("돈", plate.transform, Fonts != null ? Fonts.B : null, 23f, UiKit.Paper, TextAlignmentOptions.Left);
            UiKit.Place(moneyText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -60f), new Vector2(520f, 32f));
            fatBack = UiKit.Panel("피로 바탕", plate.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(92f, -100f), new Vector2(300f, 16f), new Color(1f, 1f, 1f, 0.18f));
            fatFill = UiKit.Panel("피로", fatBack.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(300f, 16f), UiKit.Warm);
            fatFill.rectTransform.anchorMin = new Vector2(0f, 0f); fatFill.rectTransform.anchorMax = new Vector2(0f, 1f);
            fatFill.rectTransform.pivot = new Vector2(0f, 0.5f); fatFill.rectTransform.sizeDelta = new Vector2(0f, 0f);
            var fl = UiKit.Text("피로 글", plate.transform, Fonts != null ? Fonts.B : null, 20f, UiKit.Paper, TextAlignmentOptions.Left);
            UiKit.Place(fl.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -94f), new Vector2(70f, 28f));
            fl.text = "피로";
            fatText = UiKit.Text("피로 수", plate.transform, Fonts != null ? Fonts.B : null, 20f, UiKit.Paper, TextAlignmentOptions.Left);
            UiKit.Place(fatText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(402f, -94f), new Vector2(140f, 28f));
            goalText = UiKit.Text("목표", plate.transform, Fonts != null ? Fonts.B : null, 21f, new Color(UiKit.Paper.r, UiKit.Paper.g, UiKit.Paper.b, 0.92f), TextAlignmentOptions.Left);
            UiKit.Place(goalText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -130f), new Vector2(520f, 30f));
            nextText = UiKit.Text("다음", plate.transform, Fonts != null ? Fonts.B : null, 21f, UiKit.Warm, TextAlignmentOptions.Left);
            UiKit.Place(nextText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -166f), new Vector2(520f, 30f));
            buffRoot = UiKit.Node("효과 아이콘", plate.transform);
            UiKit.Place(buffRoot, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, -8f), new Vector2(560f, 40f));
            toastRoot = UiKit.Node("알림", transform);
            UiKit.Place(toastRoot, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(28f, 150f), new Vector2(760f, 400f));
        }

        void Awake() => Build();

        public void Set(string date, string money, int fatigue, string goal, string next)
        {
            Build();
            dateText.text = date;
            moneyText.text = money;
            FatigueFill = Mathf.Clamp01(fatigue / 100f);
            fatFill.rectTransform.sizeDelta = new Vector2(300f * FatigueFill, 0f);
            fatFill.color = fatigue >= 85 ? UiKit.Endure : fatigue >= 70 ? UiKit.Accent : UiKit.Warm;
            fatText.text = $"{fatigue}{(fatigue >= 85 ? " 한계" : fatigue >= 70 ? " 지침" : "")}";
            goalText.text = goal;
            nextText.text = next;
        }

        /// 먹은 효과 아이콘(판 아래 줄): (글자 한 자 · 남은 시간 글 · 색)
        public void SetBuffs(IList<(string tag, string text, Color color)> list)
        {
            Build();
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < list.Count || i < chips.Count; i++)
            {
                if (i >= chips.Count)
                {
                    var p = UiKit.Panel("효과 " + i, buffRoot, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(130f, 36f), new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.82f));
                    var t = UiKit.Text("글", p.transform, Fonts != null ? Fonts.B : null, 19f, UiKit.Paper, TextAlignmentOptions.Left);
                    UiKit.Place(t.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(240f, 34f));
                    chips.Add((p, t));
                }
                var (pl, tx) = chips[i];
                bool on = i < list.Count;
                pl.gameObject.SetActive(on);
                if (!on) continue;
                var (tag, text, col) = list[i];
                tx.text = $"<mark=#{ColorUtility.ToHtmlStringRGB(col)}cc><color=#F4EFE6> {tag} </color></mark> {text}";
                float w = Mathf.Min(260f, tx.preferredWidth + 18f);
                pl.rectTransform.sizeDelta = new Vector2(w, 36f);
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

        void Spawn(string line)
        {
            var p = UiKit.Panel("알림 줄", toastRoot, new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(740f, 40f), new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.78f));
            var t = UiKit.Text("글", p.transform, Fonts != null ? Fonts.B : null, 22f, UiKit.Paper, TextAlignmentOptions.Left);
            UiKit.Place(t.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(720f, 36f));
            t.text = line;
            // QA Q08: 긴 알림(배달 B — 돈·피로·스탯 …)이 한 줄로 판 밖까지 나가 화면 가운데 '× 들어가기' 안내와 겹치던 것 → 판 안에서 줄 바꿈, 판 높이를 글에 맞춤
            float w = t.preferredWidth;
            if (w > 720f)
            {
                t.textWrappingMode = TextWrappingModes.Normal;
                float h = Mathf.Max(36f, t.GetPreferredValues(line, 720f, 0f).y + 4f);
                t.rectTransform.sizeDelta = new Vector2(720f, h);
                p.rectTransform.sizeDelta = new Vector2(752f, h + 4f);
            }
            else p.rectTransform.sizeDelta = new Vector2(Mathf.Min(752f, w + 32f), 40f);
            live.Add((t, p, 3.2f));
        }

        void LateUpdate()
        {
            if (plate == null) return;
            if (plate.gameObject.activeSelf != Shown) plate.gameObject.SetActive(Shown);
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
                p.color = new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.78f * a);
                t.alpha = a;
            }
            float y = 0f;
            for (int i = live.Count - 1; i >= 0; i--) { live[i].p.rectTransform.anchoredPosition = new Vector2(0f, y); y += live[i].p.rectTransform.sizeDelta.y + 6f; }
        }

        public void ClearToasts()
        {
            pending.Clear();
            foreach (var l in live) if (l.p != null) Destroy(l.p.gameObject);
            live.Clear();
        }
    }
}
