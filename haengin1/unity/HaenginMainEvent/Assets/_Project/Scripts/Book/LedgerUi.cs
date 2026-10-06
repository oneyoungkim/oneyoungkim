// 행인1의 메인이벤트 — 가계부 화면(docs/09_M3_버티컬슬라이스_설계.md 2-4)
// ① 평소: 돈이 움직일 때만 오른쪽 위에 먹 붓 숫자 한 줄이 2초 떠올랐다 흩어짐(Toast)
// ② 2-1 공개: 화면 가운데 큰 표 — 줄이 하나씩 붓으로 써지고(0.4초씩) 멈출 줄에서 정지(Reveal)
// ③ '참기' 경고 팝업 2종(1.5초): "합의금 예상: ???원" / "학폭 심의 — 보호자 출석"(Warn)
// 글꼴: 숫자·표 = Brush(Black Han Sans), 설명 = Body. 실제 시간(TimeFx.RealDt).
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Haengin
{
    [DisallowMultipleComponent]
    public sealed class LedgerUi : MonoBehaviour
    {
        public UiFonts Fonts;
        public const float ToastSecs = 2f, RowSecs = 0.4f, WarnSecs = 1.5f;

        public string LastToast { get; private set; } = "";
        public int Toasts { get; private set; }
        public string LastWarn { get; private set; } = "";
        public int Warns { get; private set; }
        public bool TableShown => table != null && table.activeSelf;
        public int RowsShown { get; private set; }
        public bool ToastShown => toastPlate != null && toastPlate.gameObject.activeSelf;
        public bool WarnShown => warn != null && warn.gameObject.activeSelf;
        public readonly List<string> Rows = new List<string>();
        public bool QuestShown => quest != null && quest.activeSelf;
        public int QuestCount { get; private set; }
        public string LastQuestFoot { get; private set; } = "";

        TextMeshProUGUI toast, warnText, questText;
        Image toastPlate;
        CanvasGroup toastCg;
        GameObject quest;
        Image warn;
        GameObject table;
        RectTransform tableRows;
        float toastLeft, warnLeft;
        readonly List<TextMeshProUGUI> rowTexts = new List<TextMeshProUGUI>();

        void Build()
        {
            if (toast != null) return;
            UiKit.Canvas(gameObject, 46);
            // 종이 띠 위 먹 붓 숫자(외곽선 재질은 붓 글꼴이 뭉개져서 띠로)
            toastPlate = UiKit.Panel("가계부 한 줄 띠", transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -112f), new Vector2(200f, 56f), new Color(UiKit.Paper.r, UiKit.Paper.g, UiKit.Paper.b, 0.9f));
            UiKit.Fit(toastPlate, 22, 22, 6, 8);
            toast = UiKit.Text("가계부 한 줄", toastPlate.transform, Fonts != null ? Fonts.Br : null, 32f, UiKit.Ink, TextAlignmentOptions.Right);
            toastCg = toastPlate.gameObject.AddComponent<CanvasGroup>();
            toastPlate.gameObject.SetActive(false);

            // 공개 표
            var t = UiKit.Panel("가계부 표", transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(1180f, 760f), new Color(UiKit.Paper.r, UiKit.Paper.g, UiKit.Paper.b, 0.95f));
            table = t.gameObject;
            var head = UiKit.Text("제목", t.transform, Fonts != null ? Fonts.Br : null, 40f, UiKit.Ink, TextAlignmentOptions.Center);
            UiKit.Place(head.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(1100f, 56f));
            head.text = "반시우 가계부. 2026년 3월.";
            tableRows = UiKit.Node("줄", t.transform);
            UiKit.Place(tableRows, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(1080f, 640f));
            table.SetActive(false);

            warn = UiKit.Panel("경고", transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 180f), new Vector2(760f, 96f), UiKit.Ink);
            warnText = UiKit.Text("글", warn.transform, Fonts != null ? Fonts.B : null, 34f, UiKit.Paper, TextAlignmentOptions.Center);   // 본문 글꼴(— 가 붓 글꼴에 없음)
            UiKit.Stretch(warnText.rectTransform);
            UiKit.Panel("붉은 테", warn.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(760f, 6f), UiKit.Endure);
            warn.gameObject.SetActive(false);

            // 퀘스트 「하늘이 생일」: 하늘 코팅 메모가 그대로 체크리스트(손글씨 글꼴)
            var q = UiKit.Panel("하늘 메모", transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-46f, 40f), new Vector2(600f, 470f), new Color(0.99f, 0.96f, 0.86f, 0.97f));
            q.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -3f);
            quest = q.gameObject;
            questText = UiKit.Text("글", q.transform, Fonts != null ? Fonts.H : null, 30f, UiKit.Ink, TextAlignmentOptions.TopLeft, wrap: true);
            UiKit.Stretch(questText.rectTransform, 26f);
            questText.lineSpacing = 4f;
            quest.SetActive(false);
        }

        void Awake() => Build();

        public void Toast(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            Build();
            toast.text = line;
            toastLeft = ToastSecs;
            toastPlate.gameObject.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)toastPlate.transform);
            LastToast = line;
            Toasts++;
        }

        public void Warn(string line)
        {
            Build();
            warnText.text = line;
            warnLeft = WarnSecs;
            warn.gameObject.SetActive(true);
            LastWarn = line;
            Warns++;
        }

        /// 표를 줄마다 0.4초씩 써 나간다. "[수입]" 처럼 대괄호로 시작하는 줄은 머리 줄. 끝나도 표는 남는다(HideTable 로 닫음)
        public IEnumerator Reveal(IList<string> rows)
        {
            Build();
            foreach (var x in rowTexts) Destroy(x.gameObject);
            rowTexts.Clear();
            Rows.Clear();
            RowsShown = 0;
            table.SetActive(true);
            float y = 0f;
            foreach (var r in rows)
            {
                bool head = r.StartsWith("[");
                var tx = UiKit.Text("줄", tableRows, head ? (Fonts != null ? Fonts.Br : null) : (Fonts != null ? Fonts.B : null), head ? 32f : 27f, head ? UiKit.Endure : UiKit.Ink, TextAlignmentOptions.Left);
                UiKit.Place(tx.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(head ? 0f : 40f, -y), new Vector2(1040f, head ? 46f : 40f));
                tx.text = r;
                tx.maxVisibleCharacters = 0;
                rowTexts.Add(tx);
                Rows.Add(r);
                y += head ? 50f : 42f;
                // 붓으로 써지듯 글자를 0.4초에 걸쳐
                float u = 0f;
                int n = r.Length;
                while (u < RowSecs) { u += UiKit.RealDt; tx.maxVisibleCharacters = Mathf.CeilToInt(n * Mathf.Clamp01(u / RowSecs)); yield return null; }
                tx.maxVisibleCharacters = n;
                RowsShown++;
            }
        }

        /// 표를 바로 채운다(건너뛰기 — 끝 상태 같게)
        public void RevealNow(IList<string> rows)
        {
            Build();
            Rows.Clear(); Rows.AddRange(rows);
            RowsShown = rows.Count;
        }

        public void HideTable() { Build(); table.SetActive(false); }

        /// 하늘 메모(머리 줄 · 항목 · 아래 줄 = D-day · 봉투)
        public void ShowQuest(string head, IList<string> items, string foot)
        {
            Build();
            var sb = new System.Text.StringBuilder();
            sb.Append(head).Append("\n\n");
            foreach (var it in items) sb.Append(it).Append('\n');
            sb.Append("\n<align=right>").Append(foot).Append("</align>");
            questText.text = sb.ToString();
            quest.SetActive(true);
            LastQuestFoot = foot;
            QuestCount++;
        }

        public void HideQuest() { Build(); quest.SetActive(false); }

        public void HideAll()
        {
            Build();
            table.SetActive(false); warn.gameObject.SetActive(false); toastPlate.gameObject.SetActive(false); quest.SetActive(false);
            toastLeft = warnLeft = 0f;
        }

        void LateUpdate()
        {
            if (toast == null) return;
            float rdt = UiKit.RealDt;
            if (toastPlate.gameObject.activeSelf)
            {
                toastLeft -= rdt;
                // 떠오르며 흩어짐: 마지막 0.5초 동안 위로 20px · 투명
                float k = Mathf.Clamp01(toastLeft / 0.5f);
                toastCg.alpha = k;
                ((RectTransform)toastPlate.transform).anchoredPosition = new Vector2(-40f, -112f + (1f - k) * 20f);
                if (toastLeft <= 0f) toastPlate.gameObject.SetActive(false);
            }
            if (warn.gameObject.activeSelf) { warnLeft -= rdt; if (warnLeft <= 0f) warn.gameObject.SetActive(false); }
        }
    }
}
