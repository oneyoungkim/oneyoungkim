// 행인1의 메인이벤트 — '불린 이름' 도감 화면(docs/09_M3_버티컬슬라이스_설계.md 2-5)
// 등록 연출: 호칭이 붓 글씨로 0.6초(부른 사람 머리 위 — 사람 모델이 없으면 화면 위 가운데) → 오른쪽 위 도감 아이콘으로 0.4초에 날아감 + 아이콘 맥박('삭' 소리 자리)
// 보기: 3×3 칸 + 덤 칸. 회색 / 따뜻한 색 / 먹 테(놀림) / 「반시우」 물음표 잠김. 일시정지 메뉴 '도감'·데모 결산에서 연다.
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Haengin
{
    [DisallowMultipleComponent]
    public sealed class NameBookUi : MonoBehaviour
    {
        public UiFonts Fonts;
        public const float WriteSecs = 0.6f, FlySecs = 0.4f;

        public int Shows { get; private set; }
        public string LastName { get; private set; } = "";
        public bool Busy { get; private set; }
        public bool GridShown => grid != null && grid.activeSelf;
        /// 칸별로 보이는 글(테스트 — "1:국밥:회색")
        public readonly List<string> Cells = new List<string>();

        TextMeshProUGUI word;
        Image icon;
        TextMeshProUGUI iconText;
        GameObject grid;
        readonly Queue<string> queue = new Queue<string>();
        float pulse, iconLeft;

        static readonly Color GreyCell = new Color(0.80f, 0.78f, 0.76f), WarmCell = new Color(0.93f, 0.78f, 0.58f);

        void Build()
        {
            if (word != null) return;
            UiKit.Canvas(gameObject, 47);
            word = UiKit.Text("호칭", transform, Fonts != null ? Fonts.Br : null, 76f, UiKit.Ink, TextAlignmentOptions.Center);
            UiKit.Place(word.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 300f), new Vector2(900f, 110f));
            var m = new Material(word.fontSharedMaterial);
            m.EnableKeyword(ShaderUtilities.Keyword_Outline);
            m.SetColor(ShaderUtilities.ID_OutlineColor, UiKit.Paper);
            m.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.28f);
            word.fontMaterial = m;
            word.gameObject.SetActive(false);

            icon = UiKit.Panel("도감 아이콘", transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -36f), new Vector2(64f, 64f), UiKit.Ink);
            iconText = UiKit.Text("글", icon.transform, Fonts != null ? Fonts.Br : null, 21f, UiKit.Paper, TextAlignmentOptions.Center);
            UiKit.Stretch(iconText.rectTransform);
            iconText.text = "도감";
            icon.gameObject.SetActive(false);

            var g = UiKit.Panel("도감", transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1240f, 900f), new Color(UiKit.Paper.r, UiKit.Paper.g, UiKit.Paper.b, 0.97f));
            grid = g.gameObject;
            grid.SetActive(false);
        }

        void Awake() => Build();

        /// 등록 연출을 줄에 세운다(겹치면 차례로)
        public void Register(string name)
        {
            Build();
            queue.Enqueue(name);
            if (!Busy) StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            Busy = true;
            while (queue.Count > 0)
            {
                string n = queue.Dequeue();
                LastName = n;
                Shows++;
                word.text = n;
                word.gameObject.SetActive(true);
                icon.gameObject.SetActive(true);
                var rt = word.rectTransform;
                Vector2 a = new Vector2(0f, 300f);
                float u = 0f;
                while (u < WriteSecs) { u += UiKit.RealDt; word.maxVisibleCharacters = Mathf.CeilToInt(n.Length * Mathf.Clamp01(u / (WriteSecs * 0.7f))); rt.anchoredPosition = a; rt.localScale = Vector3.one; yield return null; }
                word.maxVisibleCharacters = n.Length;
                // 오른쪽 위 아이콘으로(캔버스 기준: 아이콘 중심 ≈ (화면 폭/2 − 72, 540 − 68))
                var canvasRt = (RectTransform)transform;
                Vector2 b = new Vector2(canvasRt.rect.width * 0.5f - 72f, canvasRt.rect.height * 0.5f - 68f);
                u = 0f;
                while (u < FlySecs) { u += UiKit.RealDt; float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u / FlySecs)); rt.anchoredPosition = Vector2.Lerp(a, b, k); rt.localScale = Vector3.one * Mathf.Lerp(1f, 0.25f, k); yield return null; }
                word.gameObject.SetActive(false);
                pulse = 0.5f;
                iconLeft = 1.5f;
            }
            Busy = false;
        }

        public void ClearFx()
        {
            Build();
            StopAllCoroutines();
            queue.Clear();
            Busy = false;
            word.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (icon == null || !icon.gameObject.activeSelf) return;
            if (!Busy) { iconLeft -= UiKit.RealDt; if (iconLeft <= 0f) { icon.gameObject.SetActive(false); return; } }
            if (pulse > 0f) { pulse -= UiKit.RealDt; float k = 1f + 0.25f * Mathf.Sin(Mathf.Clamp01(pulse / 0.5f) * Mathf.PI); icon.rectTransform.localScale = Vector3.one * k; }
            else icon.rectTransform.localScale = Vector3.one;
        }

        /// 도감 보기를 연다(catalog = 칸 정의, got = 등록한 호칭)
        public void ShowGrid(NameEntry[] catalog, NameBook book)
        {
            Build();
            foreach (Transform c in grid.transform) Destroy(c.gameObject);
            Cells.Clear();
            var head = UiKit.Text("제목", grid.transform, Fonts != null ? Fonts.Br : null, 44f, UiKit.Ink, TextAlignmentOptions.Center);
            UiKit.Place(head.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(1100f, 60f));
            head.text = "불린 이름";
            for (int slot = 1; slot <= 9; slot++)
            {
                var e = System.Array.Find(catalog ?? new NameEntry[0], x => x.Slot == slot);
                int r = (slot - 1) / 3, c = (slot - 1) % 3;
                Cell(e, book, new Vector2(-390f + c * 390f, 230f - r * 220f), slot.ToString());
            }
            var bonus = System.Array.Find(catalog ?? new NameEntry[0], x => x.Slot == 0);
            if (bonus != null && book.Has(bonus.Name)) Cell(bonus, book, new Vector2(0f, -400f), "덤", small: true);
            grid.SetActive(true);
        }

        void Cell(NameEntry e, NameBook book, Vector2 pos, string tag, bool small = false)
        {
            bool got = e != null && book.Has(e.Name);
            var tone = e == null ? NameTone.Empty : e.Tone;
            Color bg = !got ? new Color(UiKit.Paper.r * 0.93f, UiKit.Paper.g * 0.93f, UiKit.Paper.b * 0.93f) : tone == NameTone.Warm ? WarmCell : GreyCell;
            if (tone == NameTone.Locked) bg = UiKit.Ink;
            var cell = UiKit.Panel("칸 " + tag, grid.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, small ? new Vector2(560f, 70f) : new Vector2(360f, 200f), bg);
            if (got && tone == NameTone.Mock)
            {
                // 먹 테(놀림)
                var o = cell.gameObject.AddComponent<Outline>();
                o.effectColor = UiKit.Ink; o.effectDistance = new Vector2(6f, -6f);
            }
            string big = tone == NameTone.Locked ? "?" : got ? e.Name : "";
            var t = UiKit.Text("호칭", cell.transform, Fonts != null ? Fonts.Br : null, small ? 32f : 46f, tone == NameTone.Locked ? UiKit.Paper : UiKit.Ink, TextAlignmentOptions.Center);
            UiKit.Place(t.rectTransform, new Vector2(0.5f, small ? 0.5f : 0.62f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(small ? 540f : 340f, 64f));
            t.text = big;
            if (!small && got)
            {
                var d = UiKit.Text("설명", cell.transform, Fonts != null ? Fonts.B : null, 19f, UiKit.Ink, TextAlignmentOptions.Center, wrap: true);
                UiKit.Place(d.rectTransform, new Vector2(0.5f, 0.22f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(330f, 64f));
                d.text = $"{e.Caller}\n{e.Note}";
            }
            Cells.Add($"{tag}:{(tone == NameTone.Locked ? "?" : got ? e.Name : "")}:{(got || tone == NameTone.Locked ? NameBook.ToneName(tone) : "빈칸")}");
        }

        public void HideGrid() { Build(); grid.SetActive(false); }
    }
}
