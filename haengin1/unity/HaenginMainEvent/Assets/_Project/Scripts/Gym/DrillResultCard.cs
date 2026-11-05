// 행인1의 메인이벤트 — 드릴 결과 카드(docs/12_체육관_설계.md 4-3 표시 · 4-6 DrillResultCard · B안 「중계 화면」 docs/16)
// 화면 0.86 먹 덮개 + 위 가로 주황 줄 · 제목 = 코드(Warm Barlow) + 이름(Noto Black 43) · 왼쪽 종이 판 '등급'(Barlow 480 — S 만 주황)
// 오른쪽 지표 표(항목 · 얇은 주황 막대 = m_i · 값 Barlow 60) · 아래 코치 이름띠 + 한 줄 · 보상 칸(머리말 + Barlow 58, 피로만 Warm) · 닫기 키 칩.
// 나타남: 표 한 줄씩 70ms 간격, 등급이 마지막(Update 가 ShownFor 로 그림 — 코루틴이 끊겨 등급이 빈칸으로 남던 것을 없앰).
// 첫 S: 등급 판이 가로로 뒤집힌 뒤 '시뮬 해금' 칩. 시뮬 결과는 등급 대신 '시뮬'.
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Haengin
{
    [DisallowMultipleComponent]
    public sealed class DrillResultCard : MonoBehaviour
    {
        public UiFonts Fonts;
        bool built;
        GameObject root; RectTransform card, gradePanel;
        TextMeshProUGUI grade, gradeLabel, code, title, coach, coachWho, note, simChip;
        Image coachTag, coachPlate, simPlate;
        readonly UiKit.TableRow[] rows = new UiKit.TableRow[5];
        readonly TextMeshProUGUI[] rLabels = new TextMeshProUGUI[5], rValues = new TextMeshProUGUI[5];
        readonly Image[] rSeps = new Image[5];
        RectTransform rewardRoot;
        readonly float[] scores = new float[5];
        int rowN, rewardN;
        bool unlockAnim, sim;
        public bool Visible => root != null && root.activeSelf;
        public string GradeText => grade != null ? grade.text : "";
        public bool SimUnlockShown => simPlate != null && simPlate.gameObject.activeSelf;
        public float ShownFor { get; private set; }
        /// 등급까지 다 나타났나(시험·QA 사진)
        public bool Revealed => Visible && ShownFor >= (sim ? 0f : GradeAt) + GradeIn;
        public const float RowStep = 0.07f, RowIn = 0.15f, GradeIn = 0.2f, Content = 1704f;
        float GradeAt => rowN * RowStep + 0.08f;

        void Build()
        {
            if (built) return; built = true;
            UiKit.Canvas(gameObject, 14);
            root = UiKit.Node("결과", transform).gameObject;
            UiKit.Stretch((RectTransform)root.transform);
            UiKit.Cover("어둠", root.transform);
            var f = Fonts;
            var tl = new Vector2(0f, 1f);
            var c5 = new Vector2(0.5f, 0.5f);
            card = UiKit.Node("카드", root.transform);
            UiKit.Place(card, c5, c5, Vector2.zero, new Vector2(Content, 888f));
            UiKit.Panel("주황 줄", card, tl, tl, Vector2.zero, new Vector2(Content, UiKit.Stripe), UiKit.Accent);
            code = UiKit.TextAt("코드", card, UiKit.FNum(f), 38f, UiKit.Warm, new Vector2(0f, -30f), new Vector2(120f, 60f), TextAlignmentOptions.BaselineLeft);
            title = UiKit.TextAt("제목", card, UiKit.FBlack(f), 43f, UiKit.Paper, new Vector2(72f, -30f), new Vector2(1300f, 60f), TextAlignmentOptions.BaselineLeft);
            var rl = UiKit.Text("결과 머리말", card, UiKit.FMed(f), 18f, UiKit.PaperDim, TextAlignmentOptions.BaselineRight);
            UiKit.Place(rl.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -30f), new Vector2(200f, 60f));
            rl.text = "결과";
            // 등급 판(종이)
            var gp = UiKit.Panel("등급 판", card, tl, tl, new Vector2(0f, -96f), new Vector2(432f, 528f), UiKit.Paper);
            gradePanel = gp.rectTransform;
            gradeLabel = UiKit.TextAt("등급 글", gp.transform, UiKit.FMed(f), 23f, UiKit.Grey, new Vector2(24f, -18f), new Vector2(380f, 34f));
            gradeLabel.text = "등급";
            grade = UiKit.Text("등급", gp.transform, UiKit.FNum(f), 480f, UiKit.Ink, TextAlignmentOptions.Baseline);
            UiKit.Place(grade.rectTransform, tl, c5, new Vector2(216f, -468f), new Vector2(432f, 560f));
            simPlate = UiKit.Panel("시뮬 칩", gp.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 24f), new Vector2(384f, 60f), UiKit.InkPlate);
            UiKit.EdgeLine(simPlate.transform, UiKit.Edge.Left, UiKit.Accent);
            simChip = UiKit.Text("시뮬", simPlate.transform, UiKit.FBlack(f), 28f, UiKit.Paper, TextAlignmentOptions.MidlineLeft);
            UiKit.Stretch(simChip.rectTransform); simChip.rectTransform.offsetMin = new Vector2(24f, 0f);
            simChip.text = "시뮬 해금";
            simPlate.gameObject.SetActive(false);
            // 지표 표
            for (int i = 0; i < 5; i++)
                rows[i] = UiKit.Table("지표 " + i, card, f, new Vector2(480f, -96f - i * 106f), 1224f, 106f, 276f, 160f, 30f, 60f, 12f);
            // A6: 막대 뜻(D→S 점수) — 막대 칸 오른쪽 끝 위에 작은 머리말
            var sh = UiKit.TextAt("표 머리말", card, UiKit.FMed(f), 18f, UiKit.PaperDim, new Vector2(480f + 300f + 740f - 300f, -100f), new Vector2(300f, 26f), TextAlignmentOptions.TopRight);
            sh.text = "S 기준까지";
            // 코치 한 줄
            coachPlate = UiKit.Plate("코치 판", card, tl, tl, new Vector2(0f, -713f), new Vector2(912f, 89f));
            coachWho = UiKit.NameTag("이름띠", coachPlate.transform, f, tl, Vector2.zero, Vector2.zero, new Vector2(180f, 41f), 24f, out coachTag);
            coach = UiKit.Text("코치", coachPlate.transform, UiKit.FMed(f), 31f, UiKit.Paper, TextAlignmentOptions.MidlineLeft, true);
            UiKit.Stretch(coach.rectTransform); coach.rectTransform.offsetMin = new Vector2(20f, 4f); coach.rectTransform.offsetMax = new Vector2(-20f, -4f);
            coach.enableAutoSizing = true; coach.fontSizeMin = 22f; coach.fontSizeMax = 31f;
            // 보상 칸(위아래 선 · 칸 사이 선)
            rewardRoot = UiKit.Node("보상", card);
            UiKit.Place(rewardRoot, tl, tl, new Vector2(960f, -672f), new Vector2(744f, 118f));
            var rLine = UiKit.RuleColor;
            UiKit.Panel("보상 위 선", rewardRoot, tl, tl, Vector2.zero, new Vector2(744f, UiKit.RuleW), rLine);
            UiKit.Panel("보상 아래 선", rewardRoot, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(744f, UiKit.RuleW), rLine);
            for (int i = 0; i < 5; i++)
            {
                rLabels[i] = UiKit.TextAt("보상 머리말 " + i, rewardRoot, UiKit.FMed(f), 18f, UiKit.PaperDim, new Vector2(0f, -14f), new Vector2(170f, 26f));
                rValues[i] = UiKit.TextAt("보상 " + i, rewardRoot, UiKit.FNum(f), 58f, UiKit.Paper, new Vector2(0f, -70f), new Vector2(170f, 60f), TextAlignmentOptions.BaselineLeft);
                rSeps[i] = UiKit.Panel("칸 선 " + i, rewardRoot, tl, tl, Vector2.zero, new Vector2(UiKit.RuleW, 118f), rLine);
            }
            note = UiKit.Text("덧말", card, UiKit.FMed(f), 22f, UiKit.PaperDim, TextAlignmentOptions.TopRight, true);
            UiKit.Place(note.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -802f), new Vector2(744f, 34f));
            // 닫기 키 칩(오른쪽 아래)
            var close = UiKit.Node("닫기", card);
            UiKit.Place(close, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -852f), new Vector2(420f, 36f));
            var hl = close.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 8f; hl.childAlignment = TextAnchor.MiddleRight; hl.childControlWidth = hl.childControlHeight = false; hl.childForceExpandWidth = hl.childForceExpandHeight = false;
            foreach (var k in new[] { "×", "A", "Enter" }) UiKit.KeyChip("키 " + k, close, f, k, 36f, false, out _);
            var cl = UiKit.Text("닫기 글", close, UiKit.FMed(f), 24f, UiKit.Paper, TextAlignmentOptions.MidlineLeft);
            cl.text = "닫기"; cl.rectTransform.sizeDelta = new Vector2(cl.GetPreferredValues("닫기").x + 4f, 36f);
            root.SetActive(false);
        }

        /// 결과 카드. coachLine = 코치 한 줄(등급 D 는 "…"). unlock = 이번에 시뮬이 열렸나
        public void Show(DrillResult r, DrillReward w, string coachLine, bool unlock)
        {
            Build();
            root.SetActive(true);
            ShownFor = 0f; sim = false; unlockAnim = unlock;
            var d = r.Def;
            code.text = d.Id;
            title.text = $"{d.NameKr}{(r.Aborted ? " — 중단" : r.Concussion ? " — 스톱(뇌진탕 의심)" : "")}";
            LayoutTitle();
            grade.text = r.Aborted ? "—" : DrillGrader.Letter(r.Grade);
            grade.font = UiKit.FNum(Fonts); grade.fontSize = 480f;
            grade.rectTransform.anchoredPosition = new Vector2(216f, -468f);
            grade.color = r.Grade == Grade.S && !r.Aborted ? UiKit.Accent : UiKit.Ink;      // S 만 주황(종이 판 위 큰 글자 3.3:1)
            rowN = 0;
            for (int i = 0; i < 5; i++)
            {
                bool on = i < d.MetricCount;
                rows[i].Rt.gameObject.SetActive(on);
                if (!on) continue;
                rowN++;
                rows[i].Label.text = d.Metrics[i].LabelKr;
                rows[i].Value.text = d.Metrics[i].Format(r.Values[i]);
                scores[i] = Mathf.Clamp01((float)r.Scores[i]);
                UiKit.SetBar(rows[i].Fill, scores[i]);
            }
            var gm = GymManager.Instance;
            string who = gm != null && !string.IsNullOrEmpty(gm.CoachName) ? gm.CoachName : "이윤준";
            SetCoach(string.IsNullOrEmpty(coachLine) ? "" : who, coachLine);
            rewardN = 0;
            if (r.Aborted) Reward("피로", $"+{w.Fatigue}", true);
            else
            {
                Reward("땀", $"+{w.Sweat}", false);
                Reward(GymSave.NameOf(d.MainStat), $"+{w.MainDelta}", false);
                if (d.SubStat != Stat.None) Reward(GymSave.NameOf(d.SubStat), $"+{w.SubDelta}", false);
                Reward("피로", $"+{w.Fatigue}", true);
                if (w.Relation > 0) Reward("관계", $"+{w.Relation}", false);
            }
            LayoutRewards();
            var extra = new System.Collections.Generic.List<string>();
            if (r.Aborted) extra.Add("보상 없음");
            if (w.FirstS) extra.Add("첫 S +50");
            if (w.Efficiency < 1f) extra.Add("피로 효율 ×0.6");
            if (unlock) extra.Add("다음부터 이 드릴은 시뮬로 돌릴 수 있다(보상 75%)");
            note.text = UiKit.NumRich(string.Join(" · ", extra), Fonts);      // B10: 덧말 숫자 Barlow
            simPlate.gameObject.SetActive(false);
            gradePanel.localScale = Vector3.one;
            Reveal();
        }

        /// 시뮬 결과(등급 없음)
        public void ShowSim(DrillDef d, DrillReward w, int left)
        {
            Build();
            root.SetActive(true);
            ShownFor = 0f; sim = true; unlockAnim = false;
            code.text = d.Id;
            title.text = $"{d.NameKr} — 시뮬";
            LayoutTitle();
            grade.text = "시뮬"; grade.font = UiKit.FBlack(Fonts); grade.fontSize = 150f; grade.color = UiKit.Grey;
            grade.rectTransform.anchoredPosition = new Vector2(216f, -330f);
            rowN = 0;
            for (int i = 0; i < 5; i++) rows[i].Rt.gameObject.SetActive(false);
            SetCoach("", "몽타주 — 준비 · 중반 · 마무리. 이벤트 없음.");
            rewardN = 0;
            Reward("땀", $"+{w.Sweat}", false);
            Reward(GymSave.NameOf(d.MainStat), $"+{w.MainDelta}", false);
            if (d.SubStat != Stat.None) Reward(GymSave.NameOf(d.SubStat), $"+{w.SubDelta}", false);
            Reward("피로", $"+{w.Fatigue}", true);
            LayoutRewards();
            note.text = UiKit.NumRich($"오늘 남은 시뮬 {left}", Fonts);
            simPlate.gameObject.SetActive(false);
            gradePanel.localScale = Vector3.one;
            Reveal();
        }

        void LayoutTitle()
        {
            float cw = code.GetPreferredValues(code.text).x;
            title.rectTransform.anchoredPosition = new Vector2(cw + 16f, title.rectTransform.anchoredPosition.y);
            title.rectTransform.sizeDelta = new Vector2(Content - cw - 16f - 220f, 60f);
            UiKit.FitWidth(title, Content - cw - 16f - 220f, 43f, 30f);
        }

        void SetCoach(string who, string line)
        {
            bool on = !string.IsNullOrEmpty(line);
            coachPlate.gameObject.SetActive(on);
            if (!on) return;
            UiKit.SetTag(coachTag, coachWho, who, 180f);
            coach.text = line;
        }

        void Reward(string head, string val, bool warm)
        {
            if (rewardN >= 5) return;
            rLabels[rewardN].text = head;
            rValues[rewardN].text = val;
            rValues[rewardN].color = warm ? UiKit.Warm : UiKit.Paper;
            rewardN++;
        }

        void LayoutRewards()
        {
            float cw = 744f / Mathf.Max(1, rewardN);
            for (int i = 0; i < 5; i++)
            {
                bool on = i < rewardN;
                rLabels[i].gameObject.SetActive(on); rValues[i].gameObject.SetActive(on); rSeps[i].gameObject.SetActive(on && i > 0);
                if (!on) continue;
                float x = i * cw;
                rLabels[i].rectTransform.anchoredPosition = new Vector2(x + 18f, -14f);
                rValues[i].rectTransform.anchoredPosition = new Vector2(x + 18f, -70f);      // 기준선 −100(머리말 상자 아래)
                rLabels[i].rectTransform.sizeDelta = rValues[i].rectTransform.sizeDelta = new Vector2(cw - 24f, rValues[i].rectTransform.sizeDelta.y);
                rLabels[i].rectTransform.sizeDelta = new Vector2(cw - 24f, 26f);
                UiKit.FitWidth(rValues[i], cw - 30f, 58f, 34f);
                rSeps[i].rectTransform.anchoredPosition = new Vector2(x, 0f);
            }
        }

        /// 나타남(ShownFor 기준): 표 한 줄씩 70ms → 등급(마지막, 1.15 → 1) → 첫 S 면 등급 판 뒤집기 뒤 '시뮬 해금'
        // V2-02(3단계): 4:3 에서 카드를 줄이면 1px 선이 빠지고 주황 줄이 4px 가 됨 → 줄인 만큼 선 두께를 키워 화면에서 같게
        readonly List<(RectTransform rt, Vector2 size, bool horiz)> lines = new List<(RectTransform, Vector2, bool)>();
        void KeepLines(float k)
        {
            if (lines.Count == 0)
                foreach (var im in card.GetComponentsInChildren<Image>(true))
                {
                    string n = im.name;
                    if (n != "주황 줄" && n != "구분선" && n != "강조 줄" && n != "보상 위 선" && n != "보상 아래 선" && !n.StartsWith("칸 선")) continue;
                    var rt = im.rectTransform; var sz = rt.sizeDelta;
                    bool stretchX = rt.anchorMin.x != rt.anchorMax.x, stretchY = rt.anchorMin.y != rt.anchorMax.y;
                    bool horiz = stretchX ? true : stretchY ? false : sz.x >= sz.y;
                    lines.Add((rt, sz, horiz));
                }
            float f = k > 0.01f && k < 1f ? 1f / k : 1f;
            foreach (var (rt, sz, horiz) in lines)
            {
                if (rt == null) continue;
                var s = rt.sizeDelta;
                if (horiz) s.y = sz.y * f; else s.x = sz.x * f;
                rt.sizeDelta = s;
            }
        }

        void Reveal()
        {
            float t = Accessibility.Reduced ? 99f : ShownFor;      // C2-02: 흔들림 줄이기면 바로 다 보임
            // 좁은 화면(4:3)에선 카드 전체를 줄여 화면 안에
            float cw = ((RectTransform)transform).rect.width;
            float k = cw > 10f ? Mathf.Min(1f, (cw - 76f) / Content) : 1f;      // 결과-7: 좌우 여백 38 씩
            if (Mathf.Abs(card.localScale.y - k) > 0.001f) { card.localScale = new Vector3(k, k, 1f); KeepLines(k); }
            int shown = 0;
            for (int i = 0; i < 5; i++)
            {
                if (!rows[i].Rt.gameObject.activeSelf) continue;
                float a = Mathf.Clamp01((t - shown * RowStep) / RowIn);
                SetRowAlpha(i, a);
                UiKit.SetBar(rows[i].Fill, scores[i] * Mathf.SmoothStep(0f, 1f, a));
                shown++;
            }
            float g = sim ? Mathf.Clamp01(t / GradeIn) : Mathf.Clamp01((t - GradeAt) / GradeIn);
            grade.alpha = g;
            grade.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.15f, 1f, Mathf.SmoothStep(0f, 1f, g));
            if (unlockAnim)
            {
                // 등급이 다 나온 뒤 0.35초 쉬고 0.5초 동안 가로 뒤집기 — 반쯤에서 '시뮬 해금' 칩
                float u = Mathf.Clamp01((t - GradeAt - GradeIn - 0.35f) / 0.5f);
                gradePanel.localScale = new Vector3(u > 0f && u < 1f ? Mathf.Abs(Mathf.Cos(u * Mathf.PI)) : 1f, 1f, 1f);
                if (u >= 0.5f && !simPlate.gameObject.activeSelf) simPlate.gameObject.SetActive(true);
            }
        }

        void SetRowAlpha(int i, float a)
        {
            var r = rows[i];
            r.Label.alpha = a; r.Value.alpha = a;
            r.Back.color = new Color(UiKit.Track.r, UiKit.Track.g, UiKit.Track.b, a);
            r.Rule.color = new Color(UiKit.RuleColor.r, UiKit.RuleColor.g, UiKit.RuleColor.b, a);
            r.Fill.color = new Color(UiKit.Accent.r, UiKit.Accent.g, UiKit.Accent.b, a);
            r.STick.color = new Color(UiKit.PaperDim.r, UiKit.PaperDim.g, UiKit.PaperDim.b, a);
        }

        public void Hide() { if (root != null) root.SetActive(false); }

        void OnDisable() => GameUi.HideHints(this, false);

        void Update()
        {
            GameUi.HideHints(this, Visible);      // 결과-5: 카드가 떠 있는 동안 'H 조작 보기' 칩·펼친 안내를 숨기고 H · R3 토글도 막음
            if (!Visible) return;
            ShownFor += UiKit.RealDt;
            Reveal();
        }
    }
}
