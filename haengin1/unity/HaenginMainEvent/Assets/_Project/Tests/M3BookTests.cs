// 행인1의 메인이벤트 — M3 4단계 테스트 D09 · D10 (docs/09_M3_버티컬슬라이스_설계.md 6-2)
//   D09 Ledger_Lines : 회차 7개 진행 — 회차 카드 줄 = 부록 A-1 문구(지난 회차 줄), 그 줄의 숫자(지갑·봉투) = 가계부 계산, 봉투 적립 8번(40,000),
//                      '참기' 경고 팝업 → [참는다] 강제(참은 횟수 3 · 시작 기세 35), 돈이 움직이면 오른쪽 위 한 줄
//   D10 NameBook     : 등록 7칸 + 덤 칸 — 순서·색·같은 호칭 다시 등록 안 함·「반시우」 잠김·등록 연출 1번씩, 도감 화면 칸
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Haengin.Tests
{
    public sealed class M3BookTests
    {
        [UnityTearDown] public IEnumerator TearDown() => M3Lab.TearDown();

        static int Num(string s) => int.Parse(s.Replace(",", ""));

        [UnityTest, Timeout(1800000)]
        public IEnumerator D09_Ledger_Lines()
        {
            yield return M3Lab.OpenZone1();
            var r = M3Lab.Runner;
            var log = new StringBuilder();
            var fails = new List<string>();
            // 봇: '참기' 는 [주먹을 쥔다](2번)를 골라 경고 → 강제를 본다(4-5 는 [건우를 노려본다])
            r.BotAnswer = l => l.IsEndure ? 2 : 0;
            r.Bot = StoryRunner.BotMode.Read;
            int warns0 = r.LedgerUi.Warns, toasts0 = r.LedgerUi.Toasts;
            void OnStart(SceneDef s)
            {
                if (s.Kind != SceneKind.Card) return;
                var prev = r.Def.Episode(s.Ep - 1);
                string want = prev != null ? prev.Ledger : "";
                string got = r.Card.Line;
                var w = Regex.Match(want, @"지갑 ([\d,]+)원");
                var e1 = Regex.Match(want, @"하늘 케이크 ([\d,]+) / 40,000");
                var e2 = Regex.Match(want, @"봉투 ([\d,]+)원");
                string calc = $"지갑 {r.Ledger.Wallet} · 봉투 {r.Ledger.Envelope}({r.Ledger.EnvelopeAdds}번)";
                log.AppendLine($"{s.Id} 카드 「{r.Card.Title}」 줄 '{got}' · 계산 {calc}");
                if (got != want) fails.Add($"{s.Id} 카드 줄 '{got}' ≠ 부록 A '{want}'");
                if (w.Success && Num(w.Groups[1].Value) != r.Ledger.Wallet) fails.Add($"{s.Id} 지갑 {w.Groups[1].Value} ≠ 계산 {r.Ledger.Wallet}");
                if (e1.Success && Num(e1.Groups[1].Value) != r.Ledger.Envelope) fails.Add($"{s.Id} 봉투 {e1.Groups[1].Value} ≠ 계산 {r.Ledger.Envelope}");
                if (e2.Success && Num(e2.Groups[1].Value) != r.Ledger.Envelope) fails.Add($"{s.Id} 봉투 {e2.Groups[1].Value} ≠ 계산 {r.Ledger.Envelope}");
            }
            // 이동 장면은 봇이 순간 이동(Read 는 걸어감 — 오래 걸려서), 대사·카드는 Read 그대로
            void OnAny(SceneDef s) { r.Bot = s.Kind == SceneKind.Move ? StoryRunner.BotMode.Fast : StoryRunner.BotMode.Read; if (r.Talk != null) r.Talk.Bot = l => l.IsEndure ? 2 : 0; }
            r.SceneStarted += OnStart;
            r.SceneStarted += OnAny;
            r.Begin(false, null, null);
            yield return M3Lab.WaitDone(60 * 60 * 20);
            r.SceneStarted -= OnStart;
            r.SceneStarted -= OnAny;
            Assert.IsTrue(r.Finished, "끝까지(지금 " + r.Scene?.Id + ")");
            int warns = r.LedgerUi.Warns - warns0, toasts = r.LedgerUi.Toasts - toasts0;
            log.AppendLine($"끝: {r.Ledger.Summary} · {r.Flags.Summary} · 경고 팝업 {warns}번 · 가계부 한 줄 {toasts}번 · 공개 표 {r.LedgerUi.Rows.Count}줄");
            if (r.Ledger.EnvelopeAdds != 8) fails.Add($"봉투 적립 {r.Ledger.EnvelopeAdds}번 ≠ 8");
            if (r.Ledger.Envelope != 40000) fails.Add($"봉투 {r.Ledger.Envelope} ≠ 40,000");
            if (r.Ledger.Wallet != 3400) fails.Add($"지갑 {r.Ledger.Wallet} ≠ 3,400(5화)");
            if (r.Ledger.Earned != 8000 + 120000) fails.Add($"번 돈 {r.Ledger.Earned} ≠ 128,000");
            if (r.Flags.Endured != 3) fails.Add($"참은 횟수 {r.Flags.Endured} ≠ 3");
            if (r.Flags.EnduredWillingly != 0) fails.Add($"자발 {r.Flags.EnduredWillingly} ≠ 0(모두 강제)");
            if (warns != 3) fails.Add($"경고 팝업 {warns}번 ≠ 3");
            if (r.Flags.StartHeat != 35) fails.Add($"시작 기세 {r.Flags.StartHeat} ≠ 35");
            if (r.LedgerUi.QuestCount < 1 || !r.LedgerUi.LastQuestFoot.StartsWith("D-56")) fails.Add($"퀘스트 메모 {r.LedgerUi.QuestCount}번 · '{r.LedgerUi.LastQuestFoot}'(2-3 = 3월 6일 → D-56)");
            log.AppendLine($"퀘스트 메모: {r.LedgerUi.QuestCount}번 · '{r.LedgerUi.LastQuestFoot}'");
            if (r.LedgerUi.Rows.Count != r.Def.LedgerRows.Length) fails.Add($"가계부 공개 표 {r.LedgerUi.Rows.Count}줄 ≠ {r.Def.LedgerRows.Length}");
            if (toasts < 8 + 2 + 1) fails.Add($"가계부 한 줄 {toasts}번 — 돈이 움직일 때마다 떠야 함");
            Debug.Log($"[M3Test] D09 Ledger_Lines: 실패 {fails.Count}\n{log}");
            Assert.IsEmpty(fails, string.Join("\n", fails));
        }

        [UnityTest, Timeout(1200000)]
        public IEnumerator D10_NameBook()
        {
            yield return M3Lab.OpenZone1();
            var r = M3Lab.Runner;
            var ui = r.NameUi;
            int shows0 = ui.Shows;
            r.Bot = StoryRunner.BotMode.Fast;
            r.Begin(false, null, null);
            yield return M3Lab.WaitDone(60 * 60 * 6);
            Assert.IsTrue(r.Finished);
            yield return Lab.Frames(120);     // 등록 연출이 줄 서서 끝날 때까지
            var want = r.Def.Names.Where(n => n.Tone != NameTone.Locked && n.Tone != NameTone.Empty).OrderBy(n => r.Def.Scenes.ToList().FindIndex(s => s.Id == n.Scene)).Select(n => n.Name).ToList();
            CollectionAssert.AreEqual(want, r.Names.Got, "등록 순서 = 부록 A-2(장면 순서)");
            Assert.AreEqual(7, r.Names.Filled(r.Def.Names), "9칸 중 7칸");
            Assert.IsTrue(r.Names.Has("반하늘 님"), "덤 칸");
            Assert.AreEqual(want.Count, ui.Shows - shows0, "등록 연출 1번씩");
            // 같은 호칭 다시 → 무시, 「반시우」 잠김
            int s1 = ui.Shows;
            r.ApplyNow("name:행인1");
            r.ApplyNow("name:반시우");
            yield return Lab.Frames(60);
            Assert.AreEqual(s1, ui.Shows, "같은 호칭·잠긴 칸은 연출 없음");
            Assert.AreEqual(want.Count, r.Names.Got.Count, "등록 수 그대로");
            Assert.IsFalse(r.Names.Has("반시우"), "「반시우」 잠김");
            // 도감 화면
            ui.ShowGrid(r.Def.Names, r.Names);
            yield return Lab.Frames(2);
            var cells = ui.Cells.ToList();
            ui.HideGrid();
            string Cell(int slot) => cells.FirstOrDefault(c => c.StartsWith(slot + ":")) ?? "";
            Assert.AreEqual("1:국밥:회색", Cell(1));
            Assert.AreEqual("4:행인1:먹 테(놀림)", Cell(4));
            Assert.AreEqual("7:정호 아들:따뜻한 색", Cell(7));
            Assert.AreEqual("8::빈칸", Cell(8));
            Assert.AreEqual("9:?:잠김", Cell(9));
            Assert.IsTrue(cells.Any(c => c.StartsWith("덤:반하늘 님")), "덤 칸 보임");
            Debug.Log($"[M3Test] D10 NameBook: 등록 [{string.Join(", ", r.Names.Got)}] · 연출 {ui.Shows - shows0}번 · 칸 [{string.Join(" | ", cells)}]");
        }
    }
}
