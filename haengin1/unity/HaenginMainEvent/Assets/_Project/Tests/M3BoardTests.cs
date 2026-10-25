// 행인1의 메인이벤트 — 스토리보드 카드 테스트(2026-10-25 대표 피드백 "뭔 스토리인지도 모르겠고 막 지나감")
//   B01 Board_Data  : 회차 카드 말고 모든 장면에 흐름 3~5줄 · 대사 0~3개(한 줄 32자 안), 1막 소개 4줄 이상
//   B02 Board_Waits : 새 게임 → 1막 소개 카드(사람 입력 없으면 그대로 — 20초 지나도 첫 장면으로 안 감) /
//                     자리 장면(3-2 편의점 진열 — 1-5 는 1화 채우기로 실제 장면이 됨) 카드가 len 의 두 배가 지나도 그대로 · 봇이 넘기면 다음 장면 /
//                     조작 장면(1-2)은 소개 카드 → 출발, 배달복으로 갈아입음(7단계)
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Haengin.Tests
{
    public sealed class M3BoardTests
    {
        [UnityTearDown] public IEnumerator TearDown() => M3Lab.TearDown();

        [UnityTest, Timeout(600000)]
        public IEnumerator B01_Board_Data()
        {
            yield return M3Lab.OpenZone1();
            var def = M3Lab.Runner.Def;
            int n = 0;
            foreach (var s in def.Scenes.Where(x => x.Kind != SceneKind.Card))
            {
                Assert.That(s.Board.Length, Is.InRange(3, 5), $"{s.Id} 흐름 줄 수");
                Assert.That(s.Quotes.Length, Is.InRange(0, 3), $"{s.Id} 대사 수");
                foreach (var q in s.Quotes) Assert.That(q.Substring(q.IndexOf('|') + 1).Length, Is.LessThanOrEqualTo(32), $"{s.Id} 대사 32자: {q}");
                n++;
            }
            Assert.That(def.Intro.Length, Is.GreaterThanOrEqualTo(4), "1막 소개 줄");
            Assert.IsFalse(string.IsNullOrEmpty(def.IntroTitle), "1막 소개 제목");
            Debug.Log($"[M3Test] B01 스토리보드: 장면 {n}개 모두 흐름 3~5줄 · 1막 소개 「{def.IntroTitle}」 {def.Intro.Length}줄");
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator B02_Board_Waits()
        {
            yield return M3Lab.OpenZone1();
            var r = M3Lab.Runner;
            Assert.NotNull(r.Board, "Story 루트에 StoryBoardUi");
            r.Bot = StoryRunner.BotMode.None;
            // ① 새 게임 → 1막 소개
            r.Begin(false, null, null);
            yield return Lab.Seconds(20f);
            Assert.IsTrue(r.Board.Shown && r.Board.Mode == "소개", "새 게임 첫 화면 = 1막 소개 카드(입력 없으면 그대로)");
            Assert.AreEqual(-1, r.Index, "누르기 전엔 첫 장면으로 안 감");
            string intro = r.Board.Title;
            r.Stop();
            yield return Lab.Frames(5);
            // ② 자리 장면 3-2: 카드가 그대로 있다가 봇이 넘기면 다음으로
            r.Begin(false, "3-2", null);
            yield return M3Lab.WaitScene("3-2");
            yield return Lab.Frames(30);
            Assert.IsTrue(r.Board.Shown && r.Board.Mode == "자리", "3-2 자리 장면 = 스토리보드 카드");
            StringAssert.Contains("편의점", r.Board.Title);
            StringAssert.Contains("참치마요", r.Board.Body);
            float len = r.Scene.Len;
            yield return Lab.Seconds(len * 2f);
            Assert.AreEqual("3-2", r.Scene.Id, $"len {len}초의 두 배가 지나도 그대로(예전엔 저절로 넘어감)");
            Assert.IsTrue(r.Board.Shown, "카드 그대로");
            r.Bot = StoryRunner.BotMode.Fast;
            yield return M3Lab.WaitScene("3-3", 600);
            Assert.IsFalse(r.Board.Shown && r.Board.Mode == "자리" && r.Board.Title.Contains("편의점"), "넘긴 뒤 카드 닫힘");
            r.Stop();
            yield return Lab.Frames(5);
            // ③ 조작 장면 1-2: 소개 카드 → (봇이 넘김) 출발, 배달복
            r.Bot = StoryRunner.BotMode.None;
            r.Begin(false, "1-2", null);
            yield return M3Lab.WaitScene("1-2");
            yield return Lab.Frames(20);
            Assert.IsTrue(r.Board.Shown && r.Board.Mode == "조작", "조작 장면 앞 소개 카드");
            var outfit = M3Lab.Motor.GetComponent<Outfit>();
            if (outfit != null) Assert.AreEqual(Outfit.Delivery, outfit.CurrentName, "새벽 배달 = 배달복");
            r.Bot = StoryRunner.BotMode.Fast;
            yield return Lab.Frames(30);
            Assert.IsFalse(r.Board.Shown, "넘기면 출발(카드 닫힘)");
            Debug.Log($"[M3Test] B02 스토리보드: 1막 소개 「{intro}」 20초 그대로 · 3-2 카드 {len * 2f:F0}초 그대로 → 봇이 넘겨 3-3 · 1-2 소개 카드 → 출발 · 의상 {(outfit != null ? outfit.CurrentName : "-")}");
        }

        // ───────────────────────── B03 1화 실제 내용: 양갈래 땋기(1-5) · 교실 출석(1-6) 연출
        [UnityTest, Timeout(900000)]
        public IEnumerator B03_Ep1_Content()
        {
            yield return M3Lab.OpenZone1();
            var r = M3Lab.Runner;
            Assert.NotNull(r.Braid, "Story 루트에 BraidGame");
            Assert.IsTrue(r.Dialogue.Has("1-5") && r.Dialogue.Has("1-6"), "1-5·1-6 대사(ep01.tsv)");
            r.Bot = StoryRunner.BotMode.Read;
            r.OneScene = true;
            var results = new System.Collections.Generic.List<string>();
            int p0 = r.Braid.Plays;
            r.Begin(false, "1-5", null);
            yield return M3Lab.WaitScene("1-5");
            var haneul = CutCtx.Actor("하늘");
            Assert.IsNotNull(haneul, "시우네 무대에 하늘(배역 모델)");
            Assert.IsNotNull(haneul.GetComponentInChildren<Animator>(), "하늘 애니메이터");
            yield return Lab.Frames(10);
            var hp0 = haneul.GetComponent<CastPose>();
            Assert.IsTrue(hp0 != null && hp0.Holding, "하늘 = 앉은 자세 유지(무대가 켜질 때 Idle 로 되돌던 것)");
            string last = "";
            for (int i = 0; i < 60 * 240 && !r.Finished; i++)
            {
                if (r.Braid.LastResult != last && !r.Braid.Shown && r.Braid.Plays > p0 + results.Count) { results.Add(r.Braid.LastResult); last = r.Braid.LastResult; }
                yield return null;
            }
            if (r.Braid.Plays > p0 + results.Count) results.Add(r.Braid.LastResult);
            Assert.AreEqual(3, r.Braid.Plays - p0, "땋기 3번");
            CollectionAssert.AreEqual(new[] { "양파", "다시", "합격" }, results, "1회차 양파 · 2회차 다시 · 3회차 합격(원고 고정)");
            r.Stop(); yield return Lab.Frames(5);
            // 1-6 교실
            r.Begin(false, "1-6", null);
            yield return M3Lab.WaitScene("1-6");
            foreach (var n in new[] { "도경민", "태오", "민재", "반 친구 1" }) Assert.IsNotNull(CutCtx.Actor(n), $"교실에 '{n}'");
            yield return Lab.Frames(10);
            foreach (var n in new[] { "태오", "민재", "반 친구 1" }) Assert.IsTrue(CutCtx.Actor(n).GetComponent<CastPose>().Holding, $"{n} 앉은 자세");
            var taeo = CutCtx.Actor("태오").GetComponentInChildren<HandPoser>();
            Assert.IsNotNull(taeo, "태오 = 정식 모델(HandPoser)");
            int camMax = 0, turned = 0; bool thumb = false, ink = false, raised = false;
            for (int i = 0; i < 60 * 300 && !r.Finished; i++)
            {
                camMax = Mathf.Max(camMax, CutCams.LiveCount);
                if (taeo.ForcedPose(true) == HandPoseLib.ThumbUp) thumb = true;
                if (InkMode.Amount > 0.5f) ink = true;
                if (i % 10 == 0)
                {
                    int n2 = 0;
                    foreach (var cl in Object.FindObjectsByType<CastLook>(FindObjectsSortMode.None)) { if (cl.LookWeight > 0.9f && cl.LookTarget == CutCtx.Player) n2++; if (cl.RaiseWeight > 0.9f) raised = true; }
                    turned = Mathf.Max(turned, n2);
                }
                yield return null;
            }
            Assert.IsTrue(r.Finished, "1-6 끝까지");
            Assert.That(camMax, Is.GreaterThanOrEqualTo(1), "대사 카메라(프리셋) 켜짐");
            Assert.IsTrue(thumb, "태오 엄지 척(손 자세 — 정식 모델 손가락 뼈)");
            Assert.IsTrue(ink, "학예회 회상(A′ 흑백) 컷이 대사 사이에서");
            Assert.That(turned, Is.GreaterThanOrEqualTo(6), "'반 전체가 뒤돌아봤다' — 반 친구들이 시우 쪽으로 고개(look:반:시우)");
            Assert.IsTrue(raised, "태오 팔 들기(raise:태오:시우)");
            Assert.AreEqual(0, CutCams.LiveCount, "장면 끝나면 대사 카메라 정리");
            Debug.Log($"[M3Test] B03 1화: 땋기 [{string.Join(", ", results)}] · 하늘 모델 · 교실 배역 · 대사 카메라 최대 {camMax} · 돌아본 학생 {turned} · 태오 엄지 척 {thumb}·팔 {raised} · 회상 흑백 {ink}");
        }
    }
}
