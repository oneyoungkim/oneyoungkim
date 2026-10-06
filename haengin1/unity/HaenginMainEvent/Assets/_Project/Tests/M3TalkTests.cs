// 행인1의 메인이벤트 — M3 1단계 테스트 D03 · D04 (docs/09_M3_버티컬슬라이스_설계.md 6-2)
//   D03 Dialogue_Flow   : 대사 진행(입력·자동), 선택지(1~3개), 길게 눌러 건너뛰기 — 줄 순서·표정 이벤트 순서 = TSV, 선택 결과 플래그, '참기' 강제·경고 팝업,
//                         건너뛰기 = 남은 이벤트·선택의 끝 상태 적용(장치 입력·메뉴 × 새지 않음은 M3ATalkInputTests)
//   D04 Dialogue_Glyphs : 모든 대사·UI·도감·가계부·회차 카드 글자 — 글꼴 3종(본문·붓·손글씨)에 없는 글자 0, 한 줄 32자 넘는 줄 0
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace Haengin.Tests
{
    public sealed class M3TalkTests
    {
        DialogueRunner dr;
        TalkInput ti;
        StoryFlags flags;
        readonly List<string> applied = new List<string>();

        static T Load<T>(string path) where T : Object
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
#else
            return null;
#endif
        }

        IEnumerator Rig()
        {
            Time.captureDeltaTime = Lab.Dt;
            TimeFx.Reset();
            GameState.SetPaused(false);
            yield return Lab.FreshScene();
            var fonts = Load<UiFonts>("Assets/_Project/Settings/UiFonts.asset");
            var go = new GameObject("대화 시험");
            ti = go.AddComponent<TalkInput>();
            dr = go.AddComponent<DialogueRunner>();
            T Ui<T>(string n) where T : Component { var g = new GameObject(n); g.transform.SetParent(go.transform, false); return g.AddComponent<T>(); }
            var sub = Ui<SubtitleUi>("자막"); sub.Fonts = fonts;
            var inner = Ui<InnerVoiceUi>("속마음"); inner.Fonts = fonts; inner.Sub = sub;
            var choice = Ui<ChoiceUi>("선택지"); choice.Fonts = fonts; choice.In = ti;
            var hud = Ui<StoryHud>("화면"); hud.Fonts = fonts;
            var endure = Ui<EndureFx>("참기");
            var ledger = Ui<LedgerUi>("가계부"); ledger.Fonts = fonts;
            dr.Sub = sub; dr.Inner = inner; dr.Choice = choice; dr.Hud = hud; dr.In = ti; dr.Endure = endure; dr.LedgerUi = ledger;
            flags = new StoryFlags();
            applied.Clear();
            dr.Flags = () => flags;
            dr.ApplyNow = a => applied.Add(a);
            dr.ApplyCo = a => { applied.Add(a); return null; };
            DialogueRunner.Auto = false;
            yield return Lab.Frames(3);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            DialogueRunner.Auto = false;
            TimeFx.Reset();
            yield return Lab.FreshScene();
            Time.captureDeltaTime = 0f;
        }

        static List<string> Expected(IEnumerable<DlgLine> lines, int answer)
        {
            var e = new List<string>();
            foreach (var l in lines)
            {
                if (l.Mode == DlgMode.Event) e.Add($"이벤트:{l.Id}:{l.Text}");
                else if (l.Mode == DlgMode.Choice) e.Add($"선택:{l.Id}={answer}{(l.IsEndure && answer != 0 ? "→0" : "")}");
                else { e.Add($"줄:{l.Id}:{DlgLine.ModeName(l.Mode)}:{l.Who}:{l.Face}"); if (!string.IsNullOrEmpty(l.Face)) e.Add($"표정:{l.Who}:{l.Face}"); }
            }
            return e;
        }

        /// '다음'을 n 프레임마다 누르고, 선택지가 뜨면 answer 를 고른다
        IEnumerator Drive(IEnumerator play, int answer, int every = 20)
        {
            int f = 0;
            bool running = true;
            var co = dr.StartCoroutine(Wrap(play, () => running = false));
            while (running && f < 60 * 120)
            {
                f++;
                if (dr.Choice.Visible) { yield return Lab.Frames(10); dr.Choice.Force(answer); }
                else if (f % every == 0) ti.InjectNext();
                yield return null;
            }
            Assert.IsFalse(running, "대화가 끝나지 않음");
        }

        static IEnumerator Wrap(IEnumerator e, System.Action done) { yield return e; done(); }

        [UnityTest, Timeout(600000)]
        public IEnumerator D03_Dialogue_Flow()
        {
            yield return Rig();
            var book = Load<DlgBook>("Assets/_Project/Settings/Dialogue.asset");
            Assert.NotNull(book, "Dialogue.asset");
            var log = new StringBuilder();

            // ① 입력으로 진행 + '참기' 를 바로 [참는다]
            var l17 = book.Get("1-7");
            Assert.GreaterOrEqual(l17.Count, 3, "1-7 대사");
            dr.Log.Clear();
            yield return Drive(dr.Play(l17), 0);
            CollectionAssert.AreEqual(Expected(l17, 0), dr.Log, "줄·표정·선택 순서 = TSV");
            Assert.AreEqual(1, flags.Endured); Assert.AreEqual(1, flags.EnduredWillingly);
            Assert.AreEqual(0, dr.LedgerUi.Warns, "[참는다] 는 경고 없음");
            log.AppendLine($"① 1-7 입력 진행: {dr.Log.Count}개 기록 = TSV · 참기 {flags.Endured}(자발 {flags.EnduredWillingly})");

            // ② 같은 대화에서 [주먹을 쥔다](2) → 경고 팝업 1.5초 → [참는다] 강제
            dr.Log.Clear();
            yield return Drive(dr.Play(l17), 2);
            CollectionAssert.AreEqual(Expected(l17, 2), dr.Log, "강제 기록");
            Assert.AreEqual(2, flags.Endured, "강제된 것도 셈");
            Assert.AreEqual(1, flags.EnduredWillingly, "강제는 자발 아님");
            Assert.AreEqual(1, dr.LedgerUi.Warns, "경고 팝업 1번");
            Assert.AreEqual(DialogueRunner.EndureWarnings[1], dr.LedgerUi.LastWarn);
            Assert.Greater(dr.Endure.Shown, 0, "'참기' 비네팅");
            log.AppendLine($"② 강제: 경고 '{dr.LedgerUi.LastWarn}' · 참기 {flags.Endured}(자발 {flags.EnduredWillingly})");

            // ③ 플래그 선택(1~3개 보기)
            for (int n = 1; n <= 3; n++)
            {
                var opts = string.Join(" / ", Enumerable.Range(0, n).Select(i => $"[보기 {i + 1}]"));
                var flagsNote = "flag:" + string.Join(",", Enumerable.Range(0, n).Select(i => $"고름{n}_{i}"));
                var line = new DlgLine { Id = $"X.{n}", Scene = "X", No = n, Mode = DlgMode.Choice, Text = opts, Note = flagsNote };
                int pick = n - 1;
                yield return Drive(dr.Play(new List<DlgLine> { line }), pick);
                Assert.IsTrue(flags.Has($"고름{n}_{pick}"), $"보기 {n}개 → {pick} 플래그");
                Assert.AreEqual(n, dr.Choice.Count, "보기 수");
            }
            log.AppendLine($"③ 보기 1·2·3개: 플래그 [{string.Join(",", flags.Set)}]");

            // ④ 길게 눌러 건너뛰기: 2-1 첫 줄에서 0.9초 → 남은 이벤트(가계부 공개·흑백 켬·끔)·선택(0번) 끝 상태
            var l21 = book.Get("2-1");
            dr.Log.Clear(); applied.Clear();
            int before = flags.Endured;
            bool running = true;
            dr.StartCoroutine(Wrap(dr.Play(l21), () => running = false));
            yield return Lab.Frames(30);
            ti.InjectHold(0.9f);
            yield return Lab.Frames(3);
            Assert.IsFalse(running, "건너뛰면 바로 끝");
            Assert.IsTrue(dr.Skipped);
            var evs = l21.Where(x => x.Mode == DlgMode.Event).Select(x => x.Text).ToList();
            CollectionAssert.AreEqual(evs, applied, "건너뛰어도 이벤트는 순서대로 다 적용");
            Assert.AreEqual(before + 1, flags.Endured, "건너뛴 '참기' = [참는다]");
            Assert.IsFalse(dr.Sub.Visible || dr.Inner.Visible, "자막·속마음 닫힘");
            log.AppendLine($"④ 건너뛰기: 이벤트 [{string.Join(",", applied)}] · 참기 {flags.Endured}");

            // ⑤ 자동 진행: 입력 없이 줄마다 글자 수 ÷ 8 + 0.8초 이상 보이고 넘어감
            DialogueRunner.Auto = true;
            var lt2 = book.Get("T2");
            var shownAt = new List<(DlgLine l, int f)>();
            int frame = 0;
            void OnShown(DlgLine l) => shownAt.Add((l, frame));
            dr.Shown += OnShown;
            running = true;
            dr.StartCoroutine(Wrap(dr.Play(lt2), () => running = false));
            while (running && frame < 60 * 120) { frame++; yield return null; }
            dr.Shown -= OnShown;
            Assert.IsFalse(running, "자동 진행으로 끝남");
            var shortest = new List<string>();
            for (int i = 0; i + 1 < shownAt.Count; i++)
            {
                float secs = (shownAt[i + 1].f - shownAt[i].f) * Lab.Dt;
                float need = shownAt[i].l.MinShow;
                shortest.Add($"{shownAt[i].l.Id} {secs:F2}/{need:F2}");
                Assert.GreaterOrEqual(secs + Lab.Dt * 1.5f, need, $"{shownAt[i].l.Id} 표시 {secs:F2}초 < {need:F2}");
            }
            DialogueRunner.Auto = false;
            log.AppendLine($"⑤ 자동 진행 T2 {lt2.Count}줄: {string.Join(" · ", shortest)}");
            Debug.Log("[M3Test] D03 Dialogue_Flow\n" + log);
        }

        [UnityTest]
        public IEnumerator D04_Dialogue_Glyphs()
        {
            yield return null;
            var fonts = Load<UiFonts>("Assets/_Project/Settings/UiFonts.asset");
            var book = Load<DlgBook>("Assets/_Project/Settings/Dialogue.asset");
            var def = Load<StoryDef>("Assets/_Project/Settings/Story.asset");
            Assert.NotNull(fonts); Assert.NotNull(book); Assert.NotNull(def);
            Assert.NotNull(fonts.Body, "본문 글꼴"); Assert.NotNull(fonts.Brush, "붓 글꼴"); Assert.NotNull(fonts.Hand, "손글씨 글꼴");
            var body = new StringBuilder(); var brush = new StringBuilder(); var hand = new StringBuilder();
            var tooLong = new List<string>();
            void Line(string id, string text, int max = 32)
            {
                foreach (var part in text.Replace("\\n", "\n").Split('\n'))
                    if (part.Length > max) tooLong.Add($"{id}: {part.Length}자 '{part}'");
            }
            foreach (var l in book.Lines)
            {
                if (l.Mode == DlgMode.Event) continue;
                if (l.Mode == DlgMode.Screen) brush.Append(l.Display); else body.Append(l.Display);
                body.Append(l.Who);
                if (l.Mode == DlgMode.Choice) foreach (var o in l.Options) Line(l.Id, o); else Line(l.Id, l.Text);
            }
            foreach (var e in def.Episodes) { brush.Append(e.Title).Append(e.No).Append("화"); body.Append(e.Dates).Append(e.Ledger); }
            foreach (var n in def.Names) { brush.Append(n.Name); body.Append(n.Caller).Append(n.Note).Append(n.Scene); }
            foreach (var r in def.LedgerRows) { if (r.StartsWith("[")) brush.Append(r); else body.Append(r); Line("가계부 표", r, 40); }
            var tmpLedger = new Ledger();
            foreach (var s in def.Scenes.Concat(def.Test))
            {
                body.Append(s.Title).Append(s.Summary).Append(s.DateLabel).Append(s.TimeLabel);
                foreach (var t in s.Targets) body.Append("다음: ").Append(t.Label);
                foreach (var ef in s.Effects)
                {
                    int c = ef.IndexOf(':');
                    if (c <= 0) continue;
                    string verb = ef.Substring(0, c);
                    if (verb == "entry") continue;          // 기록만(화면에 안 뜸)
                    var toast = tmpLedger.Apply(verb, ef.Substring(c + 1));      // 오른쪽 위 가계부 한 줄(붓 글꼴)
                    if (toast != null) { brush.Append(toast); Line(s.Id + " 가계부 한 줄", toast, 40); }
                    if (verb == "name") brush.Append(ef.Substring(c + 1));
                }
            }
            // 화면 UI 글(안내·메뉴·타이틀·도감·가계부·회차 카드)
            body.Append("다음 × · Enter · 마우스 왼쪽   건너뛰기 길게 누르기   일시정지 Esc·Options");
            body.Append("새 게임이어하기 — 저장 파일 문제장면 고르기연습장설정끝내기돌아가기Zone1 — 뒷골목 인카운터 · 와룡공원 야차전투 연습장");
            body.Append("흔들림 줄이기 ◀ 켬 끔 ▶자막 크기 작게 보통 크게자동 진행진동↑↓ 고르기    × (A) · Enter 선택    ○ (B) · Backspace 돌아가기");
            body.Append("저장 파일을 읽지 못했습니다자동 저장: 깨짐 모르는 버전 이전 자동 저장 회차 첫 장면에서 시작(이야기 상태는 처음부터)M2 전투 — 이야기와 상관없음← → 로도 바꿈");
            body.Append("시우 루트 1막 「출석부」 데모도감저장하고 타이틀로 덤 빈칸 회색 따뜻한 색 먹 테(놀림) 잠김 ? 0123456789,.:/()[]+−-—·→…「」'\"");
            body.Append("컷신 자리 조작 미니게임 전투 몽타주 화면 연출 회차 카드 반시우 가계부. 2026년 3월. 봉투 지갑 원 얻음: ×");
            brush.Append("행인1의 메인이벤트불린 이름도감반시우 가계부. 2026년 3월.0123456789,+-원 봉투 지갑 · / 하늘 케이크 얻음: ×");
            body.Append(string.Concat(DialogueRunner.EndureWarnings));
            hand.Append(def.QuestHead).Append(string.Concat(def.QuestMemo)).Append("D-0123456789 · 하늘 케이크 40,000 / ");
            hand.Append("하늘 케이크 (5/1)반시우반하늘 생일 요구사항(최종_진짜최종)케이크 2호 초 11개 글씨 장소 노래 1절 응. 운. 파.");
            var miss = new List<string>();
            void Check(TMP_FontAsset f, string text, string name)
            {
                var set = new string(text.Where(c => !char.IsWhiteSpace(c)).Distinct().ToArray());
                bool own = f.HasCharacters(set, out uint[] ownMissing, false, true);
                bool ok = f.HasCharacters(set, out uint[] missing, true, true);      // 대체 글꼴(본문) 포함
                if (!ok && missing != null && missing.Length > 0) miss.Add($"{name}({f.name}): {missing.Length}자 '{new string(missing.Select(u => (char)u).ToArray())}'");
                string fb = own ? "" : $" · 이 글꼴엔 없어 대체 글꼴로 그리는 글자 '{new string(ownMissing.Select(u => (char)u).ToArray())}'";
                Debug.Log($"[M3Test] D04 {name} {f.name}: 글자 {set.Length}종 · 없는 글자 {(ok ? 0 : missing?.Length ?? -1)}{fb}");
            }
            Check(fonts.Body, body.ToString(), "본문");
            Check(fonts.Brush, brush.ToString(), "붓");
            Check(fonts.Hand, hand.ToString(), "손글씨");
            Debug.Log($"[M3Test] D04 Dialogue_Glyphs: 대사 {book.Lines.Length}줄 · 32자 넘는 줄 {tooLong.Count} · 없는 글자 {miss.Count}곳\n{string.Join("\n", tooLong.Concat(miss))}");
            Assert.IsEmpty(tooLong, string.Join("\n", tooLong));
            Assert.IsEmpty(miss, string.Join("\n", miss));
        }
    }
}
