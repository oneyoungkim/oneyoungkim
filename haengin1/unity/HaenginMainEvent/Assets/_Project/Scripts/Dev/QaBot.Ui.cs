// 행인1의 메인이벤트 — QA 봇 U(2026-11-05 B안 「중계 화면」 전/후 사진): -qapart u [-qabase <바탕 저장>]
//   1단계 화면(동네 HUD · 대화·속마음 · 선택지 · 드릴 HUD · 드릴 결과)을 같은 상황에서 실제 화면(ScreenCapture — 오버레이 그대로)으로 찍음.
//   바꾸기 전·뒤 빌드가 같은 코드로 돌도록 두 판 모두에 있는 공개 값만 씀. 사진마다 글자 검사(text_bad.tsv)도 같이 남음
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Haengin
{
    public sealed partial class QaBot
    {
        IEnumerator PartU()
        {
            yield return LabReady();
            if (labBase == null) { Issue("lab.nobase.u", "QA", "실험실 바탕 저장이 없음 — U 건너뜀"); yield break; }
            baseDay = labBase.Month * 100 + labBase.Day;
            yield return Section("U1 동네", U_World, 120f);
            yield return Section("U2 대화", U_Talk, 420f);
            yield return Section("U3 선택지", U_Choice, 120f);
            yield return Section("U4 드릴", U_Drill, 420f);
            yield return Section("U5 화면", U_Screens, 300f);
        }

        /// 2단계(D2)에서 B안으로 바꾼 화면: 하나씩 띄워 사진(실제 화면 글자 감사 같이) — 각 화면은 띄운 뒤 닫음
        IEnumerator ShotIf(string name, System.Func<bool> up, string why)
        {
            if (up()) { yield return Shot(name); yield break; }
            Issue("u5.none." + name, "눈에 띔", $"{name}: 대상 판이 안 떠 있음 — {why}");
        }

        IEnumerator U_Screens()
        {
            int day = NextDay("수");
            yield return Load(day, 15 * 60, SceneLoader.Zone1, m => { Unlocks(m); m.G.Fatigue = 30; });
            yield return Frames(40);
            // 일시정지
            GameState.SetPaused(true); yield return Frames(20);
            yield return ShotIf("화면_일시정지", () => GameUi.Instance != null && GameUi.Instance.MenuVisible, "일시정지 메뉴");
            GameState.SetPaused(false); yield return Frames(10);
            // 휴대폰(문자 · 가계부 탭)
            if (W.Phone != null)
            {
                W.Phone.Open(0); yield return Frames(20); yield return ShotIf("화면_휴대폰", () => W.Phone.Shown, "휴대폰");
                W.Phone.SetTab(3); yield return Frames(10); yield return ShotIf("화면_휴대폰_가계부", () => W.Phone.Shown, "휴대폰 가계부 탭");
                W.Phone.Close(); yield return Frames(10);
            }
            // 이야기 날짜 판
            var sh = St != null ? St.Hud : null;
            // 이야기 날짜 판은 자유 이동 중엔 세계 HUD 가 바로 내림(WorldRunner) — 실제 이야기 장면에서만 뜸 → 여기서는 찍지 않음(발견으로 남김)
            if (sh != null) Issue("u5.none.화면_날짜판", "참고", "이야기 날짜 판은 이야기 장면에서만 — 자유 이동 사진 없음");
            // 도감
            if (St != null && St.NameUi != null && St.Def != null)
            {
                St.NameUi.ShowGrid(St.Def.Names, St.Names); yield return Frames(20); yield return ShotIf("화면_도감", () => St.NameUi.GridShown, "도감");
                St.NameUi.HideGrid(); yield return Frames(5);
            }
            // 가계부 표
            var led = FindAnyObjectByType<LedgerUi>(FindObjectsInactive.Include);
            if (led != null)
            {
                var co = StartCoroutine(led.Reveal(new[] { "[3월 첫째 주]", "국밥집 알바 +45,000원", "생활비 −32,000원", "[남은 돈]", "지갑 15,100원" }));
                yield return Secs(2.5f); yield return ShotIf("화면_가계부", () => led.TableShown, "가계부 표");
                StopCoroutine(co); led.HideAll(); yield return Frames(5);
            }
            // 계획표(맡기기 전 — 바로 닫음)
            if (W.PlannerView != null && W.Planner != null)
            {
                bool closed = false;
                var plan = W.Planner.Make(day, 7);
                var co = StartCoroutine(W.PlannerView.Edit(plan, d => null, "이번 주 계획", ok => closed = true));
                yield return Frames(30); yield return ShotIf("화면_계획", () => W.PlannerView.Shown, "계획표");
                W.PlannerView.Force(1);
                for (int i = 0; i < 120 && !closed; i++) yield return null;
                if (!closed) StopCoroutine(co);
                yield return Frames(10);
            }
            // 알바(국밥집 서빙 몇 초)
            if (W.Job != null)
            {
                bool done = false;
                var co = StartCoroutine(W.Job.Play("serve", 0.8f, day, r => done = true));
                yield return Secs(2.0f); yield return ShotIf("화면_알바", () => W.Job.Shown, "알바 판");
                for (int i = 0; i < 900 && !done; i++) yield return null;
                if (!done) StopCoroutine(co);
                yield return Frames(10);
            }
            // 장 카드
            if (W.ChapterView != null)
            {
                var co = StartCoroutine(W.ChapterView.Show("intro", "1장", "행인1", "2026년 3월 3일 ~ 5월 8일", "혜화동. 시우는 오늘도 배달을 나간다.", "× · Enter 계속", 1.5f));
                for (int i = 0; i < 180 && !W.ChapterView.Shown; i++) yield return null;
                yield return Frames(30);
                yield return ShotIf("화면_장카드", () => W.ChapterView.Shown, "장 카드");
                yield return co;
                W.ChapterView.Hide(); yield return Frames(10);
            }
            // 카드: 스토리보드 소개 · 레슨 · 이벤트(4:3 포함 — 카드 1480 을 화면 폭 − 76 안으로 줄임)
            var board = St != null ? St.Board : null;
            if (board != null)
            {
                board.ShowIntro("1막 — 행인1", new[] { "혜화동. 시우는 오늘도 배달을 나간다." }, "새 게임 · 1장 오픈월드 데모");
                yield return Frames(20); yield return ShotIf("화면_스토리보드", () => board.Shown, "스토리보드 카드"); board.Hide(); yield return Frames(5);
                board.ShowCard("개인레슨   ·   3월 14일(토) 17:30", "김승연(딥마카이) — 첫 레슨", "레슨", true, new[] { "출장 레슨 — 타격 기본기 · 17:30 · 50,000원", "이번 판: 스탯 ×1.8 · 판정 +6 · 피로 ×1.5" }, new[] { "김승연|관장님한테 얘기 들었어." }, "[×] · [Enter] · [마우스 왼쪽]   다음", true);
                yield return Frames(20); yield return ShotIf("화면_레슨카드", () => board.Shown, "레슨 카드"); board.Hide(); yield return Frames(5);
                board.ShowCard("식사   ·   3월 14일(토) 12:30", "국밥", "식사", false, new[] { "돈 8,000원 · 피로 −5" }, new[] { "사장 할머니|많이 먹어." }, "[×] · [Enter] · [마우스 왼쪽]   다음");
                yield return Frames(20); yield return ShotIf("화면_이벤트카드", () => board.Shown, "이벤트 카드"); board.Hide(); yield return Frames(5);
            }
            // 패드 기호 모드(R3·×): 강제 플래그로 조작 보기 칩·상호작용 안내
            UiKit.ForcePad = true;
            yield return Frames(20); yield return Shot("화면_패드기호");
            UiKit.ForcePad = false;
            yield return Frames(5);
            // 패배(무대 화면이 있으면)
            var stage = StageHud.Instance;
            if (stage != null)
            {
                stage.Defeat(() => { }, () => { });
                yield return Secs(1.6f); yield return ShotIf("화면_패배", () => stage.DefeatShown, "패배 화면");
                stage.DefeatChoose(false); yield return Frames(10);
            }
        }

        /// 동네: 국밥집 문 앞(상호작용 안내) · 16:30 · 엄마 문자
        IEnumerator U_World()
        {
            int day = NextDay("화");
            yield return Load(day, 16 * 60 + 30, SceneLoader.Zone1, m => { Unlocks(m); m.G.Fatigue = 5; });
            // 문 바깥(문 앞쪽 = forward 의 반대 — +forward 는 건물 속)에서 문을 비스듬히 봄 → 가게 앞 거리와 상호작용 안내가 같이 보임
            var door = FindObjectsByType<StageDoor>(FindObjectsSortMode.None).FirstOrDefault(d => d.Target == "Gukbap");
            if (door != null)
            {
                var f = door.transform.forward; f.y = 0f; f.Normalize();
                var p = door.transform.position - f * 1.5f + Vector3.Cross(Vector3.up, f) * 0.4f;
                if (Physics.Raycast(p + Vector3.up * 2f, Vector3.down, out var hit, 5f, Layers.Solid, QueryTriggerInteraction.Ignore)) p.y = hit.point.y;
                var to = door.transform.position - p; to.y = 0f;
                Mo.Teleport(p + Vector3.up * 0.05f, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg + 20f);
                Ld.Cam?.SnapBehind();
            }
            else Issue("u.door", "QA", "국밥집 문 없음");
            yield return Frames(40);
            M.Phone("엄마", "저녁 다섯 시. 홀. 늦으면 바보.");
            yield return Secs(1.0f);
            yield return Shot("동네_HUD", $"{W.Hud.DateLine} | {W.Hud.MoneyLine} | {W.Hud.NextLine}");
            // 처음 보는 조작 안내(8초)가 접힌 뒤 — 「조작 보기」 칩만(B안 기본 상태)
            yield return Secs(8.5f);
            M.Phone("하늘", "오빠 오늘 몇 시에 와?");
            yield return Secs(1.0f);
            yield return Shot("동네_HUD_칩", $"{W.Hud.DateLine} | {W.Hud.NextLine}");
        }

        /// 대화·속마음: 핵심 카드 C1-03(국밥집 3/6 19:25) 실제 장면 — 화자가 있는 자막 한 장 · 속마음 한 장
        IEnumerator U_Talk()
        {
            var c = W.Def.Card("C1-03");
            if (c == null) { Issue("u.card", "QA", "카드 C1-03 없음"); yield break; }
            yield return Load(306, 19 * 60 + 25, "Gukbap", m => { Unlocks(m); m.G.Fatigue = 0; });
            W.BotSkill = 0.9f; W.BotRead = 2.6f; W.BotChoice = null;
            Story().Bot = StoryRunner.BotMode.Read; Story().BotAnswer = null;
            bool sub = false, inner = false;
            IEnumerator Watch()
            {
                while (!(sub && inner))
                {
                    var t = Story() != null ? Story().Talk : null;
                    if (!sub && t != null && t.Sub != null && t.Sub.Visible && t.Sub.Who.Length > 0 && t.Sub.Text.Length > 10)
                    { sub = true; for (int f = 0; f < 20; f++) yield return null; ShotNow("대화_자막", $"{t.Sub.Who}: {t.Sub.Text.Replace("\n", " ")}"); }
                    if (!inner && t != null && t.Inner != null && t.Inner.Visible)
                    { inner = true; for (int f = 0; f < 20; f++) yield return null; ShotNow("속마음", t.Inner.Text.Replace("\n", " ")); }
                    yield return null;
                }
            }
            var watch = StartCoroutine(Watch());
            yield return W.RunCardBusy(c, true);
            StopCoroutine(watch);
            Story().Bot = StoryRunner.BotMode.None; W.BotSkill = 0f; W.BotRead = 0.15f;
            yield return Frames(10);
        }

        /// 선택지: 수요일 13:00 집 이불 → 낮잠 묻기(실제 선택지)
        IEnumerator U_Choice()
        {
            int wk = NextDay("수");
            yield return Load(wk, 13 * 60, "Home", m => Unlocks(m));
            yield return UseMarker("sleep");
            yield return WaitFor(() => ChoiceShown, 4f);
            if (ChoiceShown)
            {
                yield return Frames(20);
                yield return Shot("선택지", string.Join(" / ", Enumerable.Range(0, Story().Talk.Choice.Count).Select(Story().Talk.Choice.Label)));
                yield return Choose(Story().Talk.Choice.Count - 1);
            }
            else Issue("u.choice", "QA", "낮잠 선택지가 안 뜸");
            yield return Advance(30f);
        }

        /// 드릴: D04 미트(코치 호출) · D05(타격 알림) · D11 스파링(전투 HUD 같이) — 라운드 중 한 장 → 일찍 끝내고 결과 카드 한 장
        IEnumerator U_Drill()
        {
            int day = NextDay("화목");
            yield return Load(day, 18 * 60, SceneLoader.Gym, m => { Unlocks(m); m.G.Fatigue = 0; });
            if (Gm == null) { Issue("gym.none.u", "진행 불가?", "체육관이 안 열림"); yield break; }
            foreach (var id in new[] { "D04", "D05", "D11" })
            {
                M.G.Fatigue = 0;
                yield return Frames(10);
                yield return UDrill(id);
            }
        }

        IEnumerator UDrill(string id)
        {
            var gm = Gm;
            if (gm == null || !gm.StartDrill(id, 0.95f)) { Issue("drill.start.u." + id, "눈에 띔", $"드릴 {id} 시작 안 됨"); yield break; }
            float t = 0f; bool shot = false, toastShot = false;
            while (gm != null && gm.Runner != null && gm.Runner.State != DrillRunner.Phase.Result && t < 90f)
            {
                t += Time.unscaledDeltaTime;
                var hud = gm.Hud;
                bool round = gm.Runner.State == DrillRunner.Phase.Round;
                if (hud != null && !shot && round && t > 4f && (id != "D04" || hud.CueShown)) { shot = true; ShotNow("드릴_" + id, hud.CueShown ? hud.Cue ?? "" : ""); }
                // 알림판이 실제로 떠 있을 때만(글자 칸은 판이 꺼져도 지난 글을 들고 있음) · 맞은 순간 전투 의성어도 같이 떴으면 그때(D05 판 두 개 버그 확인)
                bool toastUp = hud != null && hud.ToastRect != null && hud.ToastRect.gameObject.activeInHierarchy;
                string toast = toastUp ? hud.Toast ?? "" : "";
                bool word = CombatFx.Instance != null && CombatFx.Instance.WordShown;
                if (!toastShot && round && toast.EndsWith("!") && (word || t > 12f)) { toastShot = true; ShotNow($"드릴_{id}_알림", toast + (word ? " + 의성어 " + CombatFx.LastWord : "")); }
                var st = gm.Runner.Station;
                if (shot && (toastShot || t > 20f) && st != null) st.EndDrillNow = true;
                yield return null;
            }
            if (gm == null) yield break;
            yield return WaitFor(() => gm.Card != null && gm.Card.Visible, 8f);
            yield return Secs(1.2f);
            yield return Shot($"드릴_{id}_결과", gm.Card != null ? gm.Card.GradeText : "");
            yield return Secs(0.5f);
            if (gm.Card != null && gm.Card.Visible) yield return Tap(Key.Enter);
            yield return Advance(30f);
        }
    }
}
