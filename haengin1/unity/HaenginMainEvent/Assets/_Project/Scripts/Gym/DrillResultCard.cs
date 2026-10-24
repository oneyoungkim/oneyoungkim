// 행인1의 메인이벤트 — 드릴 결과 카드(docs/12_체육관_설계.md 4-3 표시 · 4-6 DrillResultCard)
// 점수는 숨기고 등급 큰 글자(Black Han Sans, 먹 붓 등장 0.4초) + 지표 막대(= m_i) + 실제 값 + 코치 한 줄 + 보상 줄.
// 첫 S: 'S → 시뮬 해금' — 카드 뒤집힘(가로 뒤집기) 뒤 [시뮬] 아이콘에 먹 테두리. 시뮬 결과는 등급 없이 '시뮬' 표시.
using System.Collections;
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
        GameObject root; RectTransform card;
        TextMeshProUGUI grade, title, coach, reward, prompt, simChip, simNote;
        Image simFrame;
        readonly TextMeshProUGUI[] labels = new TextMeshProUGUI[5], values = new TextMeshProUGUI[5];
        readonly Image[] fills = new Image[5], backs = new Image[5];
        public bool Visible => root != null && root.activeSelf;
        public string GradeText => grade != null ? grade.text : "";
        public bool SimUnlockShown => simChip != null && simChip.gameObject.activeSelf;
        public float ShownFor { get; private set; }
        Coroutine anim;

        void Build()
        {
            if (built) return; built = true;
            UiKit.Canvas(gameObject, 14);
            root = UiKit.Node("결과", transform).gameObject;
            UiKit.Stretch((RectTransform)root.transform);
            var dim = UiKit.Panel("어둠", root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.06f, 0.05f, 0.05f, 0.72f)); UiKit.Stretch(dim.rectTransform);
            var f = Fonts != null ? Fonts.B : null; var br = Fonts != null ? Fonts.Br : null;
            var c5 = new Vector2(0.5f, 0.5f);
            var plate = UiKit.Panel("카드", root.transform, c5, c5, new Vector2(0f, 10f), new Vector2(1320f, 640f), UiKit.Paper);
            card = plate.rectTransform;
            var band = UiKit.Panel("먹 띠", plate.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(1320f, 78f), UiKit.Ink);
            title = UiKit.Text("제목", band.transform, br, 36f, UiKit.Paper, TextAlignmentOptions.Left); UiKit.Place(title.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(28f, 0f), new Vector2(1000f, 70f));
            grade = UiKit.Text("등급", plate.transform, br, 300f, UiKit.Ink, TextAlignmentOptions.Center); UiKit.Place(grade.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(230f, -30f), new Vector2(400f, 360f));
            var gl = UiKit.Text("등급 글", plate.transform, f, 24f, UiKit.Grey, TextAlignmentOptions.Center); UiKit.Place(gl.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(230f, -215f), new Vector2(400f, 34f)); gl.text = "등급";
            for (int i = 0; i < 5; i++)
            {
                float y = -120f - i * 84f;
                labels[i] = UiKit.Text("지표 " + i, plate.transform, f, 26f, UiKit.Ink, TextAlignmentOptions.Left); UiKit.Place(labels[i].rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(500f, y), new Vector2(300f, 34f));
                backs[i] = UiKit.Panel("막대 바탕 " + i, plate.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(500f, y - 36f), new Vector2(620f, 22f), new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.12f));
                fills[i] = UiKit.Panel("막대 " + i, plate.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(500f, y - 36f), new Vector2(0f, 22f), UiKit.Ink);
                values[i] = UiKit.Text("값 " + i, plate.transform, f, 24f, UiKit.Grey, TextAlignmentOptions.Right); UiKit.Place(values[i].rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(800f, y), new Vector2(320f, 34f));
            }
            coach = UiKit.Text("코치", plate.transform, f, 30f, UiKit.Ink, TextAlignmentOptions.Left, true); UiKit.Place(coach.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(500f, 92f), new Vector2(760f, 70f));
            reward = UiKit.Text("보상", plate.transform, f, 24f, UiKit.Accent, TextAlignmentOptions.Left); UiKit.Place(reward.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(500f, 40f), new Vector2(760f, 34f));
            simFrame = UiKit.Panel("시뮬 테두리", plate.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30f, 110f), new Vector2(252f, 84f), UiKit.Ink);
            var inner = UiKit.Panel("시뮬 안", simFrame.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(236f, 68f), UiKit.Paper);
            simChip = UiKit.Text("시뮬", inner.transform, br, 32f, UiKit.Ink, TextAlignmentOptions.Center); UiKit.Stretch(simChip.rectTransform); simChip.text = "[시뮬] 해금";
            simNote = UiKit.Text("시뮬 설명", plate.transform, f, 20f, UiKit.Grey, TextAlignmentOptions.Right); UiKit.Place(simNote.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30f, 80f), new Vector2(420f, 28f));
            simNote.text = "다음부터 이 드릴은 시뮬로 돌릴 수 있다(보상 75%)";
            simFrame.gameObject.SetActive(false); simNote.gameObject.SetActive(false);
            prompt = UiKit.Text("닫기", root.transform, f, 22f, new Color(UiKit.Paper.r, UiKit.Paper.g, UiKit.Paper.b, 0.8f), TextAlignmentOptions.Center); UiKit.Place(prompt.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(800f, 32f));
            prompt.text = "× (A) · Enter — 닫기";
            root.SetActive(false);
        }

        /// 결과 카드. coachLine = 코치 한 줄(등급 D 는 "…"). unlock = 이번에 시뮬이 열렸나
        public void Show(DrillResult r, DrillReward w, string coachLine, bool unlock)
        {
            Build();
            root.SetActive(true);
            ShownFor = 0f;
            var d = r.Def;
            title.text = $"{d.Id} {d.NameKr}{(r.Aborted ? " — 중단" : r.Concussion ? " — 스톱(뇌진탕 의심)" : "")}";
            grade.text = r.Aborted ? "—" : DrillGrader.Letter(r.Grade);
            grade.color = r.Grade == Grade.S ? UiKit.Accent : UiKit.Ink;
            for (int i = 0; i < 5; i++)
            {
                bool on = i < d.MetricCount;
                labels[i].gameObject.SetActive(on); values[i].gameObject.SetActive(on); fills[i].gameObject.SetActive(on); backs[i].gameObject.SetActive(on);
                if (!on) continue;
                labels[i].text = d.Metrics[i].LabelKr;
                values[i].text = d.Metrics[i].Format(r.Values[i]);
                fills[i].rectTransform.sizeDelta = new Vector2(620f * Mathf.Clamp01((float)r.Scores[i]), 22f);
                fills[i].color = r.Scores[i] >= 0.75 ? UiKit.Ink : new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.55f);
            }
            coach.text = string.IsNullOrEmpty(coachLine) ? "" : $"이윤준  「{coachLine}」";
            reward.text = r.Aborted ? $"보상 없음 · 피로 +{w.Fatigue}" : $"땀 +{w.Sweat} · {GymSave.NameOf(d.MainStat)} +{w.MainDelta}{(d.SubStat != Stat.None ? $" · {GymSave.NameOf(d.SubStat)} +{w.SubDelta}" : "")} · 피로 +{w.Fatigue}{(w.Relation > 0 ? $" · 관계 +{w.Relation}" : "")}{(w.FirstS ? " · 첫 S +50" : "")}{(w.Efficiency < 1f ? " · 피로 효율 ×0.6" : "")}";
            simFrame.gameObject.SetActive(false); simNote.gameObject.SetActive(false);
            if (anim != null) StopCoroutine(anim);
            anim = StartCoroutine(Appear(unlock));
        }

        /// 시뮬 결과(등급 없음)
        public void ShowSim(DrillDef d, DrillReward w, int left)
        {
            Build();
            root.SetActive(true);
            ShownFor = 0f;
            title.text = $"{d.Id} {d.NameKr} — 시뮬";
            grade.text = "시뮬"; grade.fontSize = 120f; grade.color = UiKit.Grey;
            for (int i = 0; i < 5; i++) { labels[i].gameObject.SetActive(false); values[i].gameObject.SetActive(false); fills[i].gameObject.SetActive(false); backs[i].gameObject.SetActive(false); }
            coach.text = "몽타주 — 준비 · 중반 · 마무리. 이벤트 없음.";
            reward.text = $"땀 +{w.Sweat} · {GymSave.NameOf(d.MainStat)} +{w.MainDelta}{(d.SubStat != Stat.None ? $" · {GymSave.NameOf(d.SubStat)} +{w.SubDelta}" : "")} · 피로 +{w.Fatigue} · 오늘 남은 시뮬 {left}";
            simFrame.gameObject.SetActive(false); simNote.gameObject.SetActive(false);
            if (anim != null) StopCoroutine(anim);
            anim = StartCoroutine(Appear(false));
        }

        IEnumerator Appear(bool unlock)
        {
            grade.fontSize = grade.text == "시뮬" ? 120f : 300f;
            float t = 0f;
            while (t < 0.4f)
            {
                t += UiKit.RealDt;
                float k = Mathf.SmoothStep(0f, 1f, t / 0.4f);
                grade.transform.localScale = Vector3.one * Mathf.Lerp(1.7f, 1f, k);
                grade.alpha = k;
                yield return null;
            }
            grade.transform.localScale = Vector3.one; grade.alpha = 1f;
            if (!unlock) yield break;
            // S → 시뮬 해금: 카드 뒤집힘(가로 0.5초) 뒤 [시뮬] 아이콘 먹 테두리
            yield return new WaitForSecondsRealtime(0.35f);
            t = 0f;
            while (t < 0.5f)
            {
                t += UiKit.RealDt;
                float k = t / 0.5f;
                card.localScale = new Vector3(Mathf.Abs(Mathf.Cos(k * Mathf.PI)), 1f, 1f);
                if (k >= 0.5f && !simFrame.gameObject.activeSelf) { simFrame.gameObject.SetActive(true); simNote.gameObject.SetActive(true); }
                yield return null;
            }
            card.localScale = Vector3.one;
        }

        public void Hide() { if (root != null) root.SetActive(false); }

        void Update() { if (Visible) ShownFor += UiKit.RealDt; }
    }
}
