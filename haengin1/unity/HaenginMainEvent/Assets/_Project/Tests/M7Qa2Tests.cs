// 행인1의 메인이벤트 — QA 2차 회귀 시험(docs/15_QA_챕터1.md 7장, 2026-10-26): 메인 세션이 정한 A 9가지(Q37~Q46) + 핵심 카드 실제 장면(S01·S02).
// Esc 일시정지(Q40)는 가짜 키보드가 필요해서 M7Qa2InputTests(InputTestFixture)에 따로.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace Haengin.Tests
{
    public sealed class M7Qa2Tests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var w = Object.FindAnyObjectByType<WorldRunner>();
            if (w != null) w.Stop();
            WorldRunner.BotPreset = null;
            GymManager.Gate = null; StageDoor.Gate = null;
            GameState.SetPaused(false);
            yield return M3Lab.TearDown();
        }

        [TearDown] public void Down() { GameClock.SetDate(GameClock.DefaultYear, 3, 3, 5 * 60 + 31); }

        internal static IEnumerator Open(System.Action<WorldRunner> got)
        {
            yield return M3Lab.OpenZone1();
            var w = Object.FindAnyObjectByType<WorldRunner>();
            Assert.NotNull(w, "Zone1 에 세계 루트(WorldRunner)가 없음 — WorldSetup.Build");
            w.BotSkill = 0.9f; w.BotRead = 0.3f; w.BotRide = false; w.BotDelegate = true;
            w.BotChoice = c => c.Id == "N02" ? 1 : 0;
            got(w);
        }

        internal static IEnumerator Free(WorldRunner w, int frames = 60 * 600)
        {
            for (int i = 0; i < frames && !(w.Free && !w.Loader.Busy); i++) yield return null;
        }

        /// 중간 저장(M6QaTests.Mid 와 같음): 그 날짜 전의 데모 카드는 끝낸 것으로 + 알바·체육관 열림
        internal static SaveData Mid(WorldDef def, int day, int minutes, string place, System.Action<WorldModel> tweak = null, Vector3? pos = null)
        {
            var m = M5WorldTests.Fresh(def);
            var d = new EventDirector(m);
            foreach (var c in def.Cards.Where(c => c.Demo && c.Kind != CardKind.Repeat))
            {
                int when = c.Date > 0 ? c.Date : c.WinTo > 0 ? c.WinTo : 0;
                if (when > 0 && when >= day) continue;
                d.ApplyAfter(c, c.Choice.Length > 1 ? c.Choice[1] : null); d.Done(c, 1);
            }
            m.Host.Clear();
            foreach (var u in new[] { "job.delivery", "job.gukbap", "job.stock", "job.counter", "job.site", "school", "planner", "lesson", "lesson.guests", "gym.cleaner" }) m.Unlock(u);
            if (m.S.GymMember == 0) m.S.GymMember = 1;
            GameClock.SetDate(2026, day / 100, day % 100, minutes);
            tweak?.Invoke(m);
            m.S.Active = true; m.S.Place = place; m.S.Spawn = place == "Home" || place == SceneLoader.Gym || place == "Site" ? "Spawn" : "Door"; m.S.HasPos = false; m.S.Card = "";
            if (place == SceneLoader.Zone1) { m.S.HasPos = true; m.S.Pos = pos ?? def.Balance.Point("bus_stop").Pos + new Vector3(1.6f, 0f, 0f); m.S.Spawn = ""; }
            return new SaveData { SceneId = "world", Month = day / 100, Day = day % 100, Minutes = minutes, Ledger = m.L, Flags = m.F, Gym = m.G, Names = m.N, World = m.S };
        }

        internal static IEnumerator Start(WorldRunner w, SaveData s)
        {
            w.Begin(s);
            yield return Lab.Frames(10);
            yield return Free(w);
            yield return Lab.Frames(10);
        }

        static IEnumerator Gym(WorldRunner w, int day, int minutes, System.Action<GymManager> got)
        {
            yield return Start(w, Mid(w.Def, day, minutes, SceneLoader.Gym, m => m.G.Fatigue = 0));
            GymManager gm = null;
            for (int i = 0; i < 300 && gm == null; i++) { gm = GymManager.Instance; yield return null; }
            Assert.NotNull(gm, "체육관");
            got(gm);
        }

        // ───────────────────────── Q37 미니게임 등급 → 기본급 그대로 + 등급 덤(S 30% · A 15% · B 5% · C·D 0) 지갑으로
        [Test]
        public void Q37_Grade_Tips_To_Wallet()
        {
            var def = M5WorldTests.Def();
            var log = new List<string>();
            void Case(string job, Grade g, bool del, float rate)
            {
                var m = M5WorldTests.Fresh(def);
                var a = def.Activity(job);
                Assert.NotNull(a, job);
                Assert.IsFalse(string.IsNullOrEmpty(a.Tip), $"{job} 덤 이름 없음");
                GameClock.SetDate(2026, 3, 10, a.Start);
                int w0 = m.L.Wallet, h0 = m.S.Household;
                var r = m.Complete(a, g, del, a.Start);
                int basePay = a.Units.Length > 0 ? a.Pay * r.Units : a.Pay;
                int want = Mathf.RoundToInt(basePay * rate / 100f) * 100;
                int wallet = m.L.Wallet - w0, house = m.S.Household - h0;
                log.Add($"{job} {DrillGrader.Letter(g)}{(del ? "(맡김)" : "")}: 기본 {basePay} · 덤 {r.Tip}(기대 {want}) · 지갑 +{wallet} · 생활비 +{house} · '{r.Summary}'");
                Assert.AreEqual(basePay, r.Pay, $"{job} 기본급이 바뀜");
                Assert.AreEqual(want, r.Tip, $"{job} {g} 덤");
                if (a.To == "household") { Assert.AreEqual(basePay, house, "기본급 → 생활비 그대로"); Assert.AreEqual(want, wallet, "덤 → 지갑"); }
                else Assert.AreEqual(basePay + want, wallet, "기본급 + 덤 → 지갑");
                if (want > 0) StringAssert.Contains(a.Tip, r.Summary, "결과 글에 덤 이름");
            }
            Case("job.gukbap", Grade.S, false, 0.30f); Case("job.gukbap", Grade.A, false, 0.15f); Case("job.gukbap", Grade.B, false, 0.05f);
            Case("job.gukbap", Grade.C, false, 0f); Case("job.gukbap", Grade.D, false, 0f);
            Case("job.counter", Grade.S, false, 0.30f); Case("job.site", Grade.A, false, 0.15f); Case("job.delivery", Grade.S, false, 0.30f);
            Case("job.site", Grade.B, true, 0.05f);
            Debug.Log("[Q] Q37 등급 덤\n" + string.Join("\n", log));
            Assert.AreEqual("별점 수당", def.Activity("job.delivery").Tip);
            Assert.AreEqual("반장 칭찬 일당", def.Activity("job.site").Tip);
            Assert.AreEqual("손님 팁", def.Activity("job.gukbap").Tip);
            Assert.AreEqual("손님 팁", def.Activity("job.counter").Tip);
        }

        // ───────────────────────── Q38 잠: 일어난 지 6시간 안이면 낮잠 2시간만 · 밤잠은 다음 기상까지(22~24시간 잠 없음)
        [Test]
        public void Q38_Nap_Two_Hours_And_No_Long_Sleep()
        {
            var def = M5WorldTests.Def();
            var m = M5WorldTests.Fresh(def);
            Assert.IsTrue(m.NapOnly, "새 게임 05:50 — 05:30 에 일어난 것으로(일어난 지 20분)");
            GameClock.SetDate(2026, 3, 9, 23 * 60);           // 월요일 밤 11시
            var r0 = m.Sleep();
            Assert.AreEqual(310, r0.WakeDay); Assert.AreEqual(def.Balance.Wake, r0.WakeMin, "밤잠 → 다음 날 05:30");
            Assert.AreEqual(0, m.AwakeMinutes);
            GameClock.SetDate(2026, 3, 10, 5 * 60 + 35);      // 일어나자마자(예전: 다음 날 05:30 까지 24시간)
            Assert.IsTrue(m.NapOnly);
            m.G.Fatigue = 50;
            var n = m.Nap();
            Assert.AreEqual(310, GameClock.DayKey, "낮잠은 날을 안 넘김");
            Assert.AreEqual(7 * 60 + 35, (int)GameClock.Minutes, "낮잠 2시간");
            Assert.AreEqual(50 - def.Balance.SleepPerHour * 2, m.G.Fatigue, "낮잠 회복 = 시간당 × 2");
            // 하루 중 어느 때 자도 22시간 넘게 자지 않는다(05:30 기상 뒤 30분마다)
            var worst = new List<string>(); int longest = 0;
            for (int t = 5 * 60 + 30; t < 24 * 60 + 2 * 60; t += 30)
            {
                var mm = M5WorldTests.Fresh(def);
                GameClock.SetDate(2026, 3, 10, def.Balance.Wake); mm.S.Set(WorldModel.WokeDayKey, 310); mm.S.Set(WorldModel.WokeMinKey, def.Balance.Wake);
                int day = t >= 1440 ? 311 : 310; int min = t % 1440;
                GameClock.SetDate(2026, day / 100, day % 100, min);
                int hours;
                // 2026-10-29 대표 결정: 06:00~18:00 에 잠들면 무조건 낮잠 2시간
                if (min >= WorldModel.NapFrom && min < WorldModel.NapUntil && !mm.NapOnly) worst.Add($"{GameClock.Hm(min)} 낮인데 밤잠");
                if (mm.NapOnly) { var nr = mm.Nap(); hours = nr.Hours; }
                else { var sr = mm.Sleep(); hours = sr.Hours; }
                longest = Mathf.Max(longest, hours);
                if (hours >= 22) worst.Add($"{GameClock.Hm(min)} → {hours}시간");
            }
            Debug.Log($"[Q] Q38 낮잠·밤잠: 일어나자마자 낮잠 {n.Summary} · 하루 중 가장 긴 잠 {longest}시간 · 22시간 넘는 잠 {worst.Count}");
            Assert.IsEmpty(worst, "22시간 넘는 잠 · 낮인데 밤잠: " + string.Join(", ", worst));
            Assert.LessOrEqual(longest, 12, "18:00 뒤에 자면 다음 날 05:30 까지 — 11시간 30분이 가장 김");
            // 18:30 에 자면 밤잠(다음 날 05:30)
            var m2 = M5WorldTests.Fresh(def);
            GameClock.SetDate(2026, 3, 10, 18 * 60 + 30); m2.S.Set(WorldModel.WokeDayKey, 310); m2.S.Set(WorldModel.WokeMinKey, def.Balance.Wake);
            Assert.IsFalse(m2.NapOnly, "18:30 = 밤잠");
            var s2 = m2.Sleep();
            Assert.AreEqual(311, s2.WakeDay); Assert.AreEqual(def.Balance.Wake, s2.WakeMin);
        }

        // ───────────────────────── Q39 시간대 이름 표 하나(HUD·이야기 날짜판·휴대폰·카드 조건)
        [Test]
        public void Q39_One_Time_Band_Table()
        {
            var def = M5WorldTests.Def();
            var m = M5WorldTests.Fresh(def);
            CollectionAssert.AreEqual(GameClock.BandNames, def.Balance.BandNames, "balance.json 시간대 이름 = GameClock 표");
            CollectionAssert.AreEqual(GameClock.BandFrom, def.Balance.BandFrom, "balance.json 시간대 시작 = GameClock 표");
            CollectionAssert.AreEqual(new[] { "새벽", "아침", "낮", "오후", "저녁", "밤" }, GameClock.BandNames);
            var bad = new List<string>();
            for (int t = 0; t < 1440; t += 15)
            {
                GameClock.SetDate(2026, 3, 11, t);
                string hud = m.Band(t), story = GameClock.BandOf(t), label = GameClock.Label;
                if (hud != story || !label.EndsWith(" " + story)) bad.Add($"{GameClock.Hm(t)}: HUD {hud} · 날짜판 '{label}'");
            }
            Assert.IsEmpty(bad, string.Join(" | ", bad.Take(6)));
            Assert.AreEqual("낮", GameClock.BandOf(14 * 60), "14:00 = 낮(예전 HUD '아침' / 날짜판 '낮')");
            Assert.AreEqual("오후", GameClock.BandOf(16 * 60 + 30));
            Assert.AreEqual("밤", GameClock.BandOf(3 * 60));
            foreach (var c in def.Cards) foreach (var b in c.Bands) Assert.Contains(b, GameClock.BandNames, $"{c.Id} 시간대 '{b}' 가 표에 없음");
        }

        // ───────────────────────── Q41 밤 체육관 — 시우가 검은 실루엣이 아님(채움빛·따뜻한 주변광)
        [UnityTest, Timeout(900000)]
        public IEnumerator Q41_Gym_Night_Siwoo_Not_Silhouette()
        {
            if (!Shots.Graphics) { Assert.Ignore("그래픽 장치 없음"); yield break; }
            WorldRunner w = null;
            yield return Open(x => w = x);
            GymManager gm = null;
            yield return Gym(w, 310, 21 * 60 + 15, x => gm = x);
            Assert.AreEqual(GymPart.Night, gm.Lights.Part, "21:15 = 밤");
            Assert.NotNull(gm.Lights.Fill, "밤 채움빛 없음");
            Assert.IsTrue(gm.Lights.Fill.enabled, "밤인데 채움빛 꺼짐");
            var me = M3Lab.Motor;
            var log = new List<string>(); float worst = 99f, worstGain = 99f;
            foreach (var st in new[] { "Sock_Drill_D03", "Sock_Drill_D05", "Sock_Drill_D06", "Sock_Drill_D01" })
            {
                var s = gm.Sock(st); if (s == null) continue;
                me.Teleport(s.position, s.eulerAngles.y);
                var cam = CutCams.Make(CamPreset.Mid, me.transform, null); cam.SetLive(true);
                yield return Lab.Frames(20);
                var rs = me.GetComponentsInChildren<Renderer>(false).Where(r => r.enabled).ToList();
                float lit = 0f, dark = 0f; int px = 0;
                var a = Shots.Grab(Camera.main, 480, 270, 1, false);
                foreach (var r in rs) r.enabled = false;
                yield return null;
                var b = Shots.Grab(Camera.main, 480, 270, 1, false);
                foreach (var r in rs) r.enabled = true;
                gm.Lights.Fill.enabled = false; yield return null;
                var c = Shots.Grab(Camera.main, 480, 270, 1, false);
                gm.Lights.Fill.enabled = true; yield return null;
                var pa = a.GetPixels32(); var pb = b.GetPixels32(); var pc = c.GetPixels32();
                for (int i = 0; i < pa.Length; i++)
                {
                    int d = Mathf.Abs(pa[i].r - pb[i].r) + Mathf.Abs(pa[i].g - pb[i].g) + Mathf.Abs(pa[i].b - pb[i].b);
                    if (d < 24) continue;
                    lit += Shots.LStar(pa[i]); dark += Shots.LStar(pc[i]); px++;
                }
                Object.Destroy(a); Object.Destroy(b); Object.Destroy(c);
                CutCams.Release(cam);
                if (px < 400) { log.Add($"{st}: 시우 화소 {px}(적음 — 건너뜀)"); continue; }
                lit /= px; dark /= px;
                log.Add($"{st}: 시우 화소 {px} · L* 채움빛 {lit:F1} / 없이 {dark:F1}");
                worst = Mathf.Min(worst, lit); worstGain = Mathf.Min(worstGain, lit - dark);
            }
            Debug.Log("[Q] Q41 밤 체육관 시우 밝기\n" + string.Join("\n", log));
            Assert.Less(worst, 98f, "잰 자리가 없음(시험 전제)");
            Assert.GreaterOrEqual(worst, 22f, "밤 체육관 시우가 어두움(검은 실루엣)");
            Assert.GreaterOrEqual(worstGain, 3f, "채움빛이 시우를 밝히지 않음");
        }

        // ───────────────────────── Q42 맵 끝 담 위에 올라서지 못함(보이지 않는 막)
        [UnityTest, Timeout(900000)]
        public IEnumerator Q42_Map_Edge_No_Standing_On_Wall_Tops()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            yield return Start(w, Mid(w.Def, 311, 14 * 60, SceneLoader.Zone1));
            var guard = GameObject.Find("EdgeGuard_Qa2");
            Assert.NotNull(guard, "EdgeGuard_Qa2 없음(EnvSetup.EdgeGuard — Zone1 다시 짓기)");
            int boxes = guard.GetComponentsInChildren<BoxCollider>().Length;
            var me = M3Lab.Motor;
            // QA 봇 B22 와 같은 곳: 경계 안쪽 땅에서 바깥으로 4초 걷기
            //   G3(2026-11-05): 동쪽은 z 120 이 '골목 끝 다세대 5' 상자 안(땅 26.7 은 건물 속 — 예전 기록 '시작 (-133.5, 26.7, 120.0)'은 건물 속을 지나 0.5m 틈에서 끝남)
            //   → 걸어서 닿는 동쪽 끝 = 명륜3가 골목 동쪽 끝(QaBot.EdgeStarts 와 같은 점·길 방향)
            var cases = new (string name, float x, float z, Vector3 dir)[]
            {
                ("동쪽", -133f, 114.64f, new Vector3(10f, 0f, -2.8f).normalized), ("남쪽", -200f, 73.5f, Vector3.back), ("서쪽", -270.5f, 120f, Vector3.left),
                ("북쪽", -150f, 170.5f, Vector3.forward), ("북동", -134.5f, 160f, new Vector3(1f, 0f, 1f).normalized), ("남서", -267.5f, 76.5f, new Vector3(-1f, 0f, -1f).normalized),
            };
            var log = new List<string>(); var bad = new List<string>();
            int ground = 1 << Layers.Ground;
            foreach (var c in cases)
            {
                var top = new Vector3(c.x, 120f, c.z);
                if (!Physics.Raycast(top, Vector3.down, out var gh, 200f, ground, QueryTriggerInteraction.Ignore)) { log.Add($"{c.name}: 땅 없음(건너뜀)"); continue; }
                me.Teleport(gh.point + Vector3.up * 0.05f, Mathf.Atan2(c.dir.x, c.dir.z) * Mathf.Rad2Deg);
                yield return Lab.Frames(10);
                float maxRise = 0f; string on = "";
                for (int f = 0; f < 240; f++)
                {
                    me.SetMoveInput(c.dir, 1f, false);
                    yield return null;
                    var feet = me.Position;
                    if (!Physics.Raycast(feet + Vector3.up * 0.3f, Vector3.down, out var h, 0.8f, Layers.Solid, QueryTriggerInteraction.Ignore)) continue;
                    if (h.collider.gameObject.layer == Layers.Ground) continue;
                    if (!Physics.Raycast(h.point - Vector3.up * 0.02f, Vector3.down, out var g2, 30f, ground, QueryTriggerInteraction.Ignore)) continue;
                    float rise = h.point.y - g2.point.y;
                    if (rise > maxRise) { maxRise = rise; on = h.collider.name; }
                }
                me.ClearMoveInput();
                log.Add($"{c.name}: 시작 {gh.point:F1} → 끝 {me.Position:F1} · 땅 아닌 윗면 최고 {maxRise:F2}m {on}");
                if (maxRise >= EdgeRiseLimit) bad.Add($"{c.name}: '{on}' 위 {maxRise:F2}m");
            }
            Debug.Log($"[Q] Q42 맵 끝: 막 {boxes}개\n" + string.Join("\n", log));
            Assert.Greater(boxes, 0, "막이 하나도 없음");
            Assert.IsEmpty(bad, "맵 끝 담 위에 올라섬: " + string.Join(" | ", bad));
        }
        const float EdgeRiseLimit = 0.45f;

        // ───────────────────────── Q43 드릴 자리 글 — 가까이 갈 때만 · 작게 · 사람 뒤로(깊이 검사)
        [UnityTest, Timeout(900000)]
        public IEnumerator Q43_Drill_Marker_Text_Small_Near_Behind_People()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            GymManager gm = null;
            yield return Gym(w, 310, 16 * 60 + 45, x => gm = x);
            var me = M3Lab.Motor;
            // 2026-10-31 대표: 공중 이름표·바닥 스티커 없앰 — 가까이 가면 화면 안내에 이름·최고 등급
            var mk = gm.Markers.First(x => x != null);
            var me0 = mk.transform.position + mk.transform.right * 1.2f + Vector3.up * 0.05f;
            me.Teleport(me0, 0f);
            yield return Lab.Frames(15);
            Assert.IsFalse(mk.LabelShown || mk.StickerShown, "드릴 자리 월드 글자가 남아 있음");
            Assert.IsTrue(mk.Interact != null && mk.Interact.Prompt.Contains("최고"), "화면 안내에 최고 등급이 없음: " + mk.Interact?.Prompt);
            Debug.Log($"[Q] Q43 드릴 자리 글: 월드 글자 없음 · 안내 {mk.Interact.Prompt}");
        }

        // ───────────────────────── Q44 계산대 알바 카메라 — 손님 쪽에서, 호빵 찜기 유리·포스기가 시우를 가리지 않음
        [UnityTest, Timeout(900000)]
        public IEnumerator Q44_Counter_Camera_Clear_Of_Steamer()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            yield return Start(w, Mid(w.Def, 315, 12 * 60 + 40, "Conv", m => { m.G.Fatigue = 0; }));
            w.BotDelegate = false;
            var pm = PlaceMarker.All.First(m => m.Func == "job_counter");
            var stage = StagePlace.Find("Conv");
            Assert.NotNull(stage);
            var steamer = stage.GetComponentsInChildren<BoxCollider>(true).Where(b => b.gameObject.layer == Layers.CamBlock).ToList();
            Assert.GreaterOrEqual(steamer.Count, 2, "포스기·찜기 카메라 막이(CamBlock) 둘");
            var act = typeof(WorldRunner).GetMethod("Activity", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            w.StartCoroutine((IEnumerator)act.Invoke(w, new object[] { w.Def.Activity("job.counter"), "job_counter", pm.transform }));
            var me = M3Lab.Motor;
            int seen = 0, bad = 0; string why = ""; float minFront = 1f;
            for (int i = 0; i < 60 * 120; i++)
            {
                if (w.Job != null && w.Job.Shown && i > 40 && Camera.main != null)
                {
                    var cam = Camera.main.transform.position;
                    var chest = me.Position + Vector3.up * 1.25f;
                    var to = cam - chest; to.y = 0f;
                    float front = Vector3.Dot(to.normalized, me.transform.forward);
                    minFront = Mathf.Min(minFront, front);
                    var dir = chest - cam; float len = dir.magnitude;
                    bool hit = false;
                    foreach (var b in steamer) { if (b.bounds.IntersectRay(new Ray(cam, dir / len), out float t) && t < len * 0.95f) { hit = true; if (why.Length == 0) why = "가림 " + b.name; } }
                    var vp = Camera.main.WorldToViewportPoint(chest);
                    bool inView = vp.z > 0f && vp.x > 0.1f && vp.x < 0.9f && vp.y > 0.15f && vp.y < 0.95f;
                    seen++;
                    float dist = Vector3.Distance(cam, chest);
                    if (hit || !inView || front < 0.5f || dist < 1.5f) { bad++; if (why.Length == 0) why = $"거리 {dist:F2} · 앞 {front:F2} · 당긴 것 {CutCamDriver.LastBlock}"; }
                    if (seen > 150) break;
                }
                else if (seen > 0 && (w.Job == null || !w.Job.Shown)) break;
                yield return null;
            }
            Debug.Log($"[Q] Q44 계산대 카메라: 본 프레임 {seen} · 나쁨 {bad} · 앞쪽 최저 {minFront:F2} · 가린 것 {why} · 카메라 {Camera.main.transform.position:F2}");
            Assert.Greater(seen, 0, "계산대 미니게임이 안 떴음(시험 전제)");
            Assert.AreEqual(0, bad, $"계산대 카메라가 찜기·포스기 너머 또는 옆에서 봄({why}, 앞쪽 {minFront:F2})");
        }

        // ───────────────────────── Q45 D01 줄넘기 — 손목이 허리 옆(팔이 옆으로 벌어지지 않음)
        [UnityTest, Timeout(900000)]
        public IEnumerator Q45_Jump_Rope_Hands_At_Waist()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            GymManager gm = null;
            yield return Gym(w, 310, 16 * 60 + 45, x => gm = x);
            w.StartCoroutine(gm.PlayBot("D01", 1f));
            RopeArms arms = null; var an = (Animator)null;
            var on = new List<float>(); var off = new List<float>();
            string dbg = "";
            int phase = 0, wait = 0;
            for (int i = 0; i < 60 * 70 && (gm.Busy || i < 60); i++)
            {
                yield return null;
                if (arms == null) { arms = M3Lab.Motor.GetComponentInChildren<RopeArms>(); if (arms != null) an = arms.GetComponent<Animator>(); continue; }
                if (gm.Runner == null || gm.Runner.State != DrillRunner.Phase.Round || !arms.Active) continue;
                var hips = an.GetBoneTransform(HumanBodyBones.Hips); var l = an.GetBoneTransform(HumanBodyBones.LeftHand); var r = an.GetBoneTransform(HumanBodyBones.RightHand);
                var right = an.transform.right;
                float lat = Mathf.Max(Mathf.Abs(Vector3.Dot(l.position - hips.position, right)), Mathf.Abs(Vector3.Dot(r.position - hips.position, right)));
                if (phase == 0) { if (arms.CurrentWeight >= arms.Weight - 0.01f) { on.Add(lat); if (dbg.Length == 0) dbg = $"손 L {an.transform.InverseTransformVector(l.position - hips.position):F2} R {an.transform.InverseTransformVector(r.position - hips.position):F2} · 목표 L {an.transform.InverseTransformVector(arms.LeftGoal - hips.position):F2}"; } if (on.Count >= 200) { phase = 1; arms.Weight = 0f; } }
                else if (phase == 1) { if (arms.CurrentWeight <= 0.001f && ++wait > 10) off.Add(lat); if (off.Count >= 150) break; }
            }
            if (arms != null) arms.Weight = 0.92f;
            if (gm.Runner != null && gm.Busy) gm.Runner.Abort();
            for (int i = 0; i < 300 && gm.Busy; i++) { gm.CloseCard(); yield return null; }
            Assert.NotNull(arms, "줄넘기 팔 보정(RopeArms)이 안 붙음");
            Assert.Greater(on.Count, 50, "줄넘기 라운드에서 보정 켠 프레임이 없음(시험 전제)");
            float Med(List<float> v) { var s = v.OrderBy(x => x).ToList(); return s.Count > 0 ? s[s.Count / 2] : -1f; }
            float mOn = Med(on), mOff = Med(off);
            Debug.Log($"[Q] Q45 줄넘기 손목 옆 거리(엉덩이 기준): 보정 {mOn:F3}m({on.Count}프레임) · 보정 끔(예전) {mOff:F3}m({off.Count}) · {dbg}");
            Assert.LessOrEqual(mOn, 0.34f, "줄넘기 손이 허리 옆이 아님(팔이 벌어짐)");
            if (off.Count > 30) Assert.Less(mOn, mOff - 0.05f, "보정이 팔을 모으지 않음");
        }

        // ───────────────────────── Q55 코치 손·미트(2026-10-31 대표): 코치 6명 D04 — 살짝 쥔 손 셰이프 키 · 미트가 손에(뜨지 않음) · 콤비 호출 때 미트 = 어깨~턱, 얼굴 안 가림
        [UnityTest, Timeout(1500000)]
        public IEnumerator Q55_Coach_Hands_And_Mitts()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            var bad = new List<string>();
            var log = new List<string>();
            foreach (var id in new[] { "yunjun", "gojun", "seungyeon", "jinsoo_son", "jinsoo_seo", "seungmin_baek" })
            {
                GymManager gm = null;
                yield return Gym(w, 312, 19 * 60, x => gm = x);
                GymState.Current.Fatigue = 0;
                Assert.IsTrue(gm.StartDrill("D04", 0.9f), id + " D04 시작");
                for (int i = 0; i < 3000 && gm.Runner.State != DrillRunner.Phase.Round; i++) yield return null;
                var npc = id == "yunjun" ? gm.Yunjun : id == "gojun" ? gm.Gojun : gm.Guest(id);
                Assert.NotNull(npc, id);
                if (npc != gm.Yunjun) gm.SetMittHolder(npc);
                var hs = npc.Anim.GetComponent<HandShape>();
                if (hs == null || !hs.Ready || hs.Relax <= 0f) bad.Add($"{id}: 살짝 쥔 손 없음");
                var toMe = npc.transform.position - M3Lab.Motor.transform.position; toMe.y = 0f;
                if (toMe.magnitude > 1.3f) bad.Add($"{id}: 코치가 드릴 자리에 없음(시우에서 {toMe.magnitude:F2}m)");
                npc.Anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;       // 배치는 화면을 안 그려 컬링됨(IK·미트가 한 박자 늦게 잡힘)
                var arms = npc.Anim.GetComponent<MittArms>() ?? npc.Anim.gameObject.AddComponent<MittArms>();
                float t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < 60f && !(arms.LiftWeight >= 0.99f && arms.PadsShown)) yield return null;
                yield return null; yield return null;
                if (!arms.PadsShown) { bad.Add($"{id}: 미트가 안 보임"); gm.Runner.Abort(); w.Stop(); yield return Lab.Frames(5); continue; }
                Vector3 B(HumanBodyBones b) => npc.Anim.GetBoneTransform(b).position;
                float sh = (B(HumanBodyBones.LeftUpperArm).y + B(HumanBodyBones.RightUpperArm).y) * 0.5f, head = B(HumanBodyBones.Head).y;
                float py = (arms.PadL.position.y + arms.PadR.position.y) * 0.5f;
                float gl = (arms.PadL.position - B(HumanBodyBones.LeftHand)).magnitude, gr = (arms.PadR.position - B(HumanBodyBones.RightHand)).magnitude;
                log.Add($"{id}: 어깨 {sh:F3} · 패드 {py:F3} · 머리 뼈 {head:F3} · 손~패드 {gl:F3}/{gr:F3}");
                if (py < sh - 0.03f || py > head) bad.Add($"{id}: 미트 높이 {py:F3} (어깨 {sh:F3} ~ 머리 뼈 {head:F3} 밖)");
                if (gl > 0.14f || gr > 0.14f) bad.Add($"{id}: 미트가 손에서 뜸 {gl:F3}/{gr:F3}");
                // 게임 카메라: 미트(반지름 0.1)가 얼굴(머리 뼈 + 0.08, 반지름 0.1)을 가리지 않음
                var cam = Camera.main;
                var face = cam.WorldToViewportPoint(B(HumanBodyBones.Head) + Vector3.up * 0.08f);
                float pxPerM = Mathf.Abs(cam.WorldToViewportPoint(B(HumanBodyBones.Head) + Vector3.up * 0.18f).y - face.y) / 0.1f;
                foreach (var p in new[] { arms.PadL, arms.PadR })
                {
                    var v = cam.WorldToViewportPoint(p.position);
                    if (v.z < face.z && Mathf.Abs(v.x - face.x) < 0.1f * pxPerM * 9f / 16f * 1.6f && Mathf.Abs(v.y - face.y) < 0.1f * pxPerM * 1.6f) bad.Add($"{id}: 게임 카메라에서 미트가 얼굴을 가림");
                }
                gm.Runner.Abort();
                for (int i = 0; i < 300 && gm.Busy; i++) { gm.CloseCard(); yield return null; }
                w.Stop();
                yield return Lab.Frames(5);
            }
            Debug.Log("[Q] Q55 미트\n" + string.Join("\n", log));
            Assert.IsEmpty(bad, string.Join("\n", bad));
        }

        // ───────────────────────── Q56 레슨 카드 카메라(2026-10-31 대표): 코치 6명 실제 레슨 흐름 — 카드 동안 코치 얼굴·가슴이 왼쪽 60%, 시우·기둥·샌드백에 안 가림, 카드 제목 이름, 드릴로 넘어가면 카메라 풀림
        [UnityTest, Timeout(1500000)]
        public IEnumerator Q56_Lesson_Card_Camera()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            var bad = new List<string>(); var log = new List<string>();
            foreach (var (id, day, enter, at) in new[] { ("yunjun", 312, 20 * 60 + 40, 21 * 60 + 10), ("gojun", 328, 15 * 60 + 20, 15 * 60 + 55), ("seungyeon", 328, 16 * 60 + 50, 17 * 60 + 25), ("jinsoo_son", 329, 9 * 60 + 30, 10 * 60 + 55), ("jinsoo_seo", 329, 18 * 60, 19 * 60 + 25), ("seungmin_baek", 328, 13 * 60, 13 * 60 + 55) })
            {
                yield return Start(w, Mid(w.Def, day, enter, SceneLoader.Gym, m => { m.G.Fatigue = 0; m.L.SetWallet(900000); m.Unlock("lesson"); m.Unlock("lesson.guests"); m.Unlock("lesson.seungmin"); }));
                GymManager gm = null;
                for (int i = 0; i < 300 && gm == null; i++) { gm = GymManager.Instance; yield return null; }
                Assert.NotNull(gm, id);
                yield return Lab.Frames(20);
                w.BotRead = 8f;
                var coach = w.Model.Sh.Coach(id);
                string why = w.BookNext(id);
                if (why != "") { bad.Add($"{id}: 예약 안 됨 {why}"); w.Stop(); yield return Lab.Frames(5); continue; }
                var mat = gm.Sock("Sock_NPC_Coach_MatEdge");
                var me = M3Lab.Motor;
                var sp = mat.position + mat.forward * 2.4f; var to = mat.position - sp; to.y = 0f;
                me.Teleport(sp, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg);
                yield return Lab.Frames(10);
                GameClock.AdvanceTo(at);
                var board = w.Story.Board;
                float t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < 60f && !(board != null && board.Shown && w.LessonCamera != null)) yield return null;
                if (board == null || !board.Shown || w.LessonCamera == null) { bad.Add($"{id}: 카드·카메라 안 뜸 (시각 {GameClock.TimeText})"); w.Stop(); yield return Lab.Frames(5); continue; }
                var lc = w.LessonCamera;
                var npc = id == "yunjun" ? gm.Yunjun : id == "gojun" ? gm.Gojun : gm.Guest(id);
                npc.Anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var head = npc.Anim.GetBoneTransform(HumanBodyBones.Head).position + Vector3.up * 0.08f;
                var chest = npc.Anim.GetBoneTransform(HumanBodyBones.Chest).position;
                // 레슨 카메라 자리 기준 화면 좌표(블렌드가 끝난 뒤 보일 그림 — FOV 30, 16:9)
                Vector3 Vp(Vector3 q) { var lp = lc.transform.InverseTransformPoint(q); float ty = Mathf.Tan(CutCams.Fov(CamPreset.Mid) * 0.5f * Mathf.Deg2Rad); return new Vector3(0.5f + 0.5f * (lp.x / lp.z) / (ty * 16f / 9f), 0.5f + 0.5f * (lp.y / lp.z) / ty, lp.z); }
                var vh = Vp(head); var vc = Vp(chest);
                string blockedBy = lc.Blocked(lc.transform.position, head) ? "가림" : "";
                log.Add($"{id}: 머리 화면 {vh.x:F2},{vh.y:F2} · 가슴 {vc.x:F2},{vc.y:F2} · 돌림 {lc.SpinDeg:F0}° · 카메라까지 {Vector3.Distance(lc.transform.position, chest):F1}m · 제목 {board.Title}");
                if (vh.z <= 0f || vh.x < 0.05f || vh.x > 0.6f || vh.y < 0.2f || vh.y > 0.98f) bad.Add($"{id}: 얼굴이 왼쪽 60% 밖 {vh}");
                if (vc.x < 0.05f || vc.x > 0.6f || vc.y < 0.05f) bad.Add($"{id}: 가슴이 왼쪽 60% 밖 {vc}");
                if (blockedBy != "") bad.Add($"{id}: 코치 얼굴이 가림");
                if (!board.Title.Contains(coach.Name)) bad.Add($"{id}: 카드 제목 이름 {board.Title}");
                w.Stop();
                yield return Lab.Frames(5);
                if (w.LessonCamera != null) bad.Add($"{id}: 세계를 멈춰도 카메라가 남음");
            }
            Debug.Log("[Q] Q56 레슨 카메라\n" + string.Join("\n", log));
            Assert.IsEmpty(bad, string.Join("\n", bad));
        }

        // ───────────────────────── Q58 글꼴 라이선스(2026-11-05 B1): UiFonts 가 가리키는 글꼴(대체 글꼴 포함)의 ttf 마다 빌드 licenses/ 에 OFL 이 복사됨
        //   빌드 뒤 정리(BatchTools.CopyFontLicenses — 원본이 없으면 빌드 실패)를 임시 폴더로 돌려 봄
        [Test]
        public void Q58_Font_Licenses_For_Every_UiFont()
        {
            var bt = System.Type.GetType("Haengin.EditorTools.BatchTools, Haengin.EditorTools");
            Assert.NotNull(bt, "BatchTools 없음");
            var fonts = UnityEditor.AssetDatabase.LoadAssetAtPath<UiFonts>("Assets/_Project/Settings/UiFonts.asset");
            Assert.NotNull(fonts, "UiFonts.asset 없음");
            string tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "haengin_q58_" + System.Guid.NewGuid().ToString("N"));
            var bad = new List<string>(); var log = new List<string>();
            try
            {
                bt.GetMethod("CopyFontLicenses").Invoke(null, new object[] { tmp });
                foreach (var (nm, fa) in new[] { ("Body", fonts.Body), ("Medium", fonts.Medium), ("Black", fonts.Black), ("Num", fonts.Num), ("Brush", fonts.Brush), ("Hand", fonts.Hand) })
                {
                    if (fa == null) { bad.Add(nm + ": 글꼴 없음"); continue; }
                    foreach (var f in new[] { fa }.Concat(fa.fallbackFontAssetTable ?? new List<TMP_FontAsset>()).Where(x => x != null))
                    {
                        string ttf = f.sourceFontFile != null ? UnityEditor.AssetDatabase.GetAssetPath(f.sourceFontFile) : UnityEditor.AssetDatabase.GUIDToAssetPath(f.creationSettings.sourceFontFileGUID);
                        if (string.IsNullOrEmpty(ttf)) { bad.Add($"{nm}/{f.name}: 원본 ttf 를 모름"); continue; }
                        string dst = (string)bt.GetMethod("LicenseFor").Invoke(null, new object[] { ttf });
                        if (dst == null) { bad.Add($"{nm}/{f.name}: {ttf} 가 라이선스 목록에 없음"); continue; }
                        var fi = new System.IO.FileInfo(System.IO.Path.Combine(tmp, dst));
                        if (!fi.Exists || fi.Length < 1000) bad.Add($"{nm}/{f.name}: {dst} 복사 안 됨");
                        else log.Add($"{nm}/{f.name}: {ttf} → {dst} ({fi.Length} bytes)");
                    }
                }
            }
            finally { if (System.IO.Directory.Exists(tmp)) System.IO.Directory.Delete(tmp, true); }
            Debug.Log("[Q] Q58 글꼴 라이선스\n" + string.Join("\n", log));
            Assert.IsEmpty(bad, string.Join("\n", bad));
        }

        // ───────────────────────── Q59 조작 안내(2026-11-05 B3·B8·결과-5): 펼친 안내는 대사·속마음 판의 실제 위 끝 위(자막 0.85/1.2 · 1줄/3줄 · 속마음+자막)
        //   설정 자동/접음/펼침 · 칩을 숨긴 화면(결과 카드 등)에서는 칩이 안 보이고 H · R3 토글도 막힘
        [UnityTest, Timeout(900000)]
        public IEnumerator Q59_Hint_Over_Talk_And_Modes()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            var bad = new List<string>(); var log = new List<string>();
            yield return Start(w, Mid(w.Def, 312, 14 * 60, SceneLoader.Zone1, m => { m.G.Fatigue = 0; }));
            yield return Lab.Frames(20);
            var ui = GameUi.Instance;
            Assert.NotNull(ui, "GameUi");
            var sub = Object.FindAnyObjectByType<SubtitleUi>(FindObjectsInactive.Include);
            var inner = Object.FindAnyObjectByType<InnerVoiceUi>(FindObjectsInactive.Include);
            Assert.NotNull(sub, "SubtitleUi"); Assert.NotNull(inner, "InnerVoiceUi");
            int keepMode = GameUi.HintMode, keepSize = PlayerPrefs.GetInt(SubtitleUi.SizeKey, 1);
            Rect R(RectTransform r) { var c = new Vector3[4]; r.GetWorldCorners(c); return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y); }
            bool Hit(Rect a, Rect b) => a.xMin < b.xMax - 1f && b.xMin < a.xMax - 1f && a.yMin < b.yMax - 1f && b.yMin < a.yMax - 1f;
            try
            {
                GameUi.HintMode = 0; yield return Lab.Frames(3);
                if (ui.HintExpanded || !ui.ChipShown) bad.Add($"접음: 펼침 {ui.HintExpanded} · 칩 {ui.ChipShown}");
                GameUi.HintMode = 1; yield return Lab.Frames(3);
                if (!ui.HintExpanded) bad.Add("펼침 설정인데 안 펼쳐짐");
                string one = "다 모였네. 얘 보러 온 거 맞지?";
                string three = "토요일 저녁, 레슨이 끝났는데 아무도 안 갔다. 사무실 앞에 관장이 다섯이었다. 다들 시우를 보러 왔다고 했다. 이윤준 관장은 웃기만 했다. 오늘은 그냥 넘어가지 않을 것 같았다.";
                foreach (int size in new[] { 0, 2 })
                    foreach (bool both in new[] { false, true })
                        foreach (string line in new[] { one, three })
                        {
                            PlayerPrefs.SetInt(SubtitleUi.SizeKey, size);
                            sub.Show("이윤준", line);
                            if (both) inner.Show("관장이 다섯. 다들 나를 보러 왔다고 했다."); else inner.Hide();
                            yield return Lab.Frames(3);
                            if (!ui.HintExpanded) { bad.Add($"크기 {size}: 대사 중 펼친 안내가 사라짐"); continue; }
                            var h = R(ui.HintRect);
                            var parts = new List<(string, Rect)> { ("대사 판", R(sub.PlateRect)) };
                            if (sub.TagRect != null && sub.TagRect.gameObject.activeInHierarchy) parts.Add(("이름띠", R(sub.TagRect)));
                            if (both && inner.Visible) parts.Add(("속마음 판", R(inner.PlateRect)));
                            foreach (var (nm, r) in parts) if (Hit(h, r)) bad.Add($"크기 {size} · {(line == one ? "1줄" : "여러 줄")} · 속마음 {both}: 펼친 안내가 {nm}과 겹침 {h} ↔ {r}");
                            log.Add($"크기 {size} · {(line == one ? "1줄" : "여러 줄")} · 속마음 {both}: 안내 아래 {h.yMin:0} · 대사 판 위 {R(sub.PlateRect).yMax:0}");
                        }
                sub.Hide(); inner.Hide();
                GameUi.HintMode = 0; yield return Lab.Frames(3);
                var owner = new object();
                GameUi.HideHints(owner, true); yield return Lab.Frames(3);
                if (ui.ChipShown || ui.HintExpanded) bad.Add("숨김 중 칩·안내가 보임");
                if (ui.ToggleHint()) bad.Add("숨김 중 토글이 먹힘");
                GameUi.HideHints(owner, false); yield return Lab.Frames(3);
                if (!ui.ChipShown) bad.Add("숨김을 풀어도 칩이 안 보임");
                if (!ui.ToggleHint()) bad.Add("칩이 보이는데 토글이 안 먹힘");
                yield return Lab.Frames(3);
                if (!ui.HintExpanded || GameUi.HintMode != 1) bad.Add($"토글 뒤 펼침이 아님(설정 {GameUi.HintMode})");
                TitleMenu.CycleHint(1);
                if (GameUi.HintMode != 0) bad.Add($"설정 펼침 → 다음은 접음이어야 함: {GameUi.HintMode}");
                TitleMenu.CycleHint(1);
                if (GameUi.HintMode != -1) bad.Add($"설정 접음 → 다음은 자동이어야 함: {GameUi.HintMode}");
            }
            finally { GameUi.HintMode = keepMode; PlayerPrefs.SetInt(SubtitleUi.SizeKey, keepSize); }
            Debug.Log("[Q] Q59 조작 안내" + System.Environment.NewLine + string.Join(System.Environment.NewLine, log));
            Assert.IsEmpty(bad, string.Join(System.Environment.NewLine, bad));
        }

        // ───────────────────────── Q60 사라지는 중에 다시 켜기(3단계 F1): UiMotion.Hide 뒤 100ms 안에 Show 하면 사라짐이 취소되고 계속 보임
        [UnityTest]
        public IEnumerator Q60_Motion_Show_Cancels_Hide()
        {
            var go = new GameObject("시험 판", typeof(RectTransform));
            try
            {
                var m = go.AddComponent<UiMotion>();
                if (Accessibility.Reduced) Assert.Ignore("흔들림 줄이기가 켜져 있으면 사라짐 없이 바로 끔");
                yield return null; yield return null;
                UiMotion.Hide(go);
                Assert.IsTrue(go.activeSelf && m.Leaving, "Hide 직후 = 사라지는 중");
                yield return null;
                UiMotion.Show(go);
                Assert.IsFalse(m.Leaving, "Show 가 사라짐을 취소");
                float t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < 0.4f) yield return null;
                Assert.IsTrue(go.activeSelf, "다시 켠 판이 0.4초 뒤에도 보임(옛: 100ms 뒤 꺼짐)");
                UiMotion.Hide(go);
                t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < 0.4f && go.activeSelf) yield return null;
                if (!Accessibility.Reduced) Assert.IsFalse(go.activeSelf, "Hide 는 그대로 100ms 뒤 끔");
            }
            finally { Object.Destroy(go); }
        }

        // ───────────────────────── Q61 일시정지 '휴대폰'·'도감'(3단계 G9·G10): 자유 이동이 아니면 칸이 흐리고 안 열림 · 도감처럼 메뉴 위 화면이 떠 있으면 메뉴를 숨김
        [UnityTest, Timeout(600000)]
        public IEnumerator Q61_Pause_Book_Gate_And_Sub_Screen()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            yield return Start(w, Mid(w.Def, 312, 14 * 60, SceneLoader.Zone1, m => { m.G.Fatigue = 0; }));
            yield return Lab.Frames(20);
            var ui = GameUi.Instance;
            Assert.NotNull(ui, "GameUi");
            Assert.IsTrue(w.FreePaused, "자유 이동 중엔 일시정지 '휴대폰'을 고를 수 있음");
            var keepHook = GameUi.BookHook; var keepAllowed = GameUi.BookAllowed; var keepSub = GameUi.SubOpen;
            bool opened = false;
            try
            {
                GameUi.BookHook = () => opened = true;
                GameUi.BookAllowed = () => false;
                GameState.SetPaused(true); yield return Lab.Frames(3);
                int bi = -1; for (int i = 0; i < ui.ItemCount; i++) if (ui.ItemAt(i) == GameUi.Item.Book) bi = i;
                Assert.GreaterOrEqual(bi, 0, "일시정지에 '휴대폰' 칸");
                yield return Lab.Frames(2);
                StringAssert.Contains("지금은 못 엶", ui.Label(bi), "못 고를 때 이유");
                ui.Choose(GameUi.Item.Book);
                Assert.IsFalse(opened, "자유 이동이 아니면 안 열림(G9)");
                GameUi.BookAllowed = () => true;
                ui.Choose(GameUi.Item.Book);
                Assert.IsTrue(opened, "자유 이동이면 열림");
                GameState.SetPaused(true); yield return Lab.Frames(3);
                GameUi.SubOpen = () => true; yield return Lab.Frames(3);
                Assert.IsFalse(ui.MenuVisible, "도감처럼 메뉴 위 화면이 떠 있으면 메뉴 숨김(G10)");
                GameUi.SubOpen = () => false; yield return Lab.Frames(3);
                Assert.IsTrue(ui.MenuVisible, "그 화면이 닫히면 메뉴가 다시 보임");
            }
            finally { GameUi.BookHook = keepHook; GameUi.BookAllowed = keepAllowed; GameUi.SubOpen = keepSub; GameState.SetPaused(false); }
        }

        /// 실제 시간 s 초(세계 평가 0.2초·알림처럼 실제 시간으로 도는 것을 기다릴 때 — 배치 시험은 프레임이 빠름)
        static IEnumerator Real(float s) { float t = Time.realtimeSinceStartup; while (Time.realtimeSinceStartup - t < s) yield return null; }
        /// 조건이 될 때까지(실제 시간 s 초까지)
        static IEnumerator Until(System.Func<bool> c, float s) { float t = Time.realtimeSinceStartup; while (!c() && Time.realtimeSinceStartup - t < s) yield return null; }

        /// 시비 반복(주차장 북쪽 25m — Q27·Q36 과 같은 자리)에서 말 걸기 → 전투가 켜질 때까지
        static IEnumerator StreetStart(WorldRunner w, int day, System.Action<Encounter, PlayerCombat> got)
        {
            var pk = w.Def.Balance.Point("parking");
            yield return Start(w, Mid(w.Def, day, 17 * 60, SceneLoader.Zone1, m => { m.G.Fatigue = 0; m.Unlock("street_repeat"); }, pk.Pos + new Vector3(0f, 0.5f, 25f)));
            yield return Lab.Frames(30);
            var enc = StoryRunner.ParkingEncounter();
            Assert.NotNull(enc, "주차장 시비 무대(45m 안)");
            var pc = Object.FindAnyObjectByType<PlayerCombat>();
            Assert.NotNull(pc);
            enc.Trigger();
            float t0 = Time.realtimeSinceStartup;      // 말 걸기 0.8초(실제 시간) 뒤 전투
            while (!pc.Active && Time.realtimeSinceStartup - t0 < 5f) yield return null;
            Assert.IsTrue(pc.Active, "시비 전투가 안 켜짐(시험 전제)");
            got(enc, pc);
        }

        /// 손 놓고 지기(체력 0) → 패배 화면까지(쓰러지고 1.2초 — 실제 시간)
        static IEnumerator LoseStreet(Encounter enc, PlayerCombat pc)
        {
            pc.Me.DebugInvuln = false;
            pc.Me.Hp = 0;
            float t0 = Time.realtimeSinceStartup;
            while (!(enc.State == Encounter.Phase.Lost && StageHud.Instance != null && StageHud.Instance.DefeatShown) && Time.realtimeSinceStartup - t0 < 8f) yield return null;
            Assert.AreEqual(Encounter.Phase.Lost, enc.State, "체력 0 → 패배");
            Assert.IsTrue(StageHud.Instance != null && StageHud.Instance.DefeatShown, "패배 화면('…일어나')");
        }

        // ───────────────────────── Q62 G1-b(2026-11-05): 시비 반복 싸움·패배 화면 동안엔 02:00 강제 잠이 안 걸림(예전: Free 가 참이라 싸움 한가운데 집으로 옮겨져 전투가 켜진 채 남음) ·
        //   지면 패 기록은 그대로 · '그만'이면 전투를 끄고 탐색으로 · 강제 잠은 싸움이 끝난 뒤에
        [UnityTest, Timeout(900000)]
        public IEnumerator Q62_Street_Fight_Holds_Night_Limit_And_Quit_Ends_Combat()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            Encounter enc = null; PlayerCombat pc = null;
            yield return StreetStart(w, 311, (e, p) => { enc = e; pc = p; });
            var pin = pc.GetComponent<PInput>() ?? Object.FindAnyObjectByType<PInput>();
            int sleeps0 = w.Sleeps, loss0 = w.Model.S.StreetLosses;
            GameClock.SetDate(2026, 3, 11, 2 * 60 + 1);        // 싸우는 중에 새벽 2시(같은 날 — 날 바뀜 카드 없이)
            yield return Real(1.0f);                              // 세계 평가는 실제 0.2초마다
            string mid = $"싸움 중 02:01 → 바쁨 '{w.Busy}' · 잠 +{w.Sleeps - sleeps0} · 장소 {w.Loader.Place} · 시비 {enc.State}";
            Assert.AreEqual("", w.Busy, "싸움 중에 02:00 강제 잠(또는 카드)이 걸림 — " + mid);
            Assert.AreEqual(sleeps0, w.Sleeps, mid);
            Assert.AreEqual(SceneLoader.Zone1, w.Loader.Place, mid);
            yield return LoseStreet(enc, pc);
            yield return Real(1.0f);
            string lost =$"패배 화면 → 바쁨 '{w.Busy}' · 시비 패 +{w.Model.S.StreetLosses - loss0}";
            Assert.AreEqual("", w.Busy, "패배 화면 중에 강제 잠·카드가 걸림 — " + lost);
            Assert.AreEqual(loss0 + 1, w.Model.S.StreetLosses, "싸움 중에도 시비 반복 승패 기록은 돌아야 함 — " + lost);
            StageHud.Instance.DefeatChoose(false);       // 그만
            yield return Lab.Frames(10);
            string quit = $"그만 → 전투 {pc.Active} · 전투 맵 {pin.CombatInput} · 맵 {InputMaps.Enabled(w.Actions)} · Modal {GameState.Modal} · 시비 {enc.State}";
            Assert.IsFalse(pc.Active, quit);
            Assert.IsFalse(pin.CombatInput, quit);
            Assert.AreEqual(InputMaps.Explore, InputMaps.Enabled(w.Actions), quit);
            Assert.IsFalse(GameState.Modal, quit);
            Assert.AreEqual(Encounter.Phase.Armed, enc.State, quit);
            // 싸움이 끝나면 강제 잠이 정상으로(집 · 05:30 · 전투 없음)
            yield return Until(() => w.Sleeps > sleeps0 && w.Free && !w.Loader.Busy, 90f);
            string after = $"그 뒤 → 잠 +{w.Sleeps - sleeps0} · {GameClock.TimeText} {w.Loader.Place} · 전투 {pc.Active}";
            Debug.Log($"[Q] Q62 시비 반복 싸움 중 밤 한계\n{mid}\n{lost}\n{quit}\n{after}");
            Assert.AreEqual(sleeps0 + 1, w.Sleeps, "싸움이 끝난 뒤 강제 잠이 안 옴 — " + after);
            Assert.AreEqual("Home", w.Loader.Place, after);
            Assert.IsFalse(pc.Active, after);
        }

        // ───────────────────────── Q62a G1(2026-11-05): 패배 화면에서 불러오기(QA 의 제자리 불러오기 = Stop → Begin) — 전투 조작·전투 HUD·패배 화면·KO 가 안 남음
        //   (예전 QA B16: 집 저장을 열어도 '…일어나'와 반시우 체력 바가 남고 E 가 안 먹어 하늘이 카드가 한 번도 안 열렸음)
        [UnityTest, Timeout(900000)]
        public IEnumerator Q62a_Load_After_Street_Loss_Leaves_No_Combat()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            Encounter enc = null; PlayerCombat pc = null;
            yield return StreetStart(w, 311, (e, p) => { enc = e; pc = p; });
            var pin = pc.GetComponent<PInput>() ?? Object.FindAnyObjectByType<PInput>();
            yield return LoseStreet(enc, pc);
            var pk = w.Def.Balance.Point("parking").Pos;
            yield return Start(w, Mid(w.Def, 315, 10 * 60, "Home", m => m.G.Fatigue = 0));
            var hud = StageHud.Instance;
            var cm = CombatMode.Current;
            string s = $"불러온 뒤 → 전투 {pc.Active} · 전투 모드 {(cm != null && cm.Active)} · 전투 맵 {pin.CombatInput} · 맵 {InputMaps.Enabled(w.Actions)} · 패배 화면 {(hud != null && hud.DefeatShown)} · Modal {GameState.Modal} · 몸 {pc.Me.State}/KO {pc.Me.KO} · {w.Loader.Place} · 시비 {StoryRunner.ParkingEncounter()?.State}";
            Assert.IsFalse(pc.Active, "불러온 뒤에도 전투 상태 — " + s);
            Assert.IsFalse(cm != null && cm.Active, s);
            Assert.IsFalse(pin.CombatInput, s);
            Assert.AreEqual(InputMaps.Explore, InputMaps.Enabled(w.Actions), s);
            Assert.IsFalse(hud != null && hud.DefeatShown, "불러온 뒤에도 패배 화면 — " + s);
            Assert.IsFalse(GameState.Modal, s);
            Assert.AreEqual(Fighter.Phase.Free, pc.Me.State, s);
            Assert.IsFalse(pc.Me.KO, s);
            Assert.AreEqual("Home", w.Loader.Place, s);
            var enc2 = StoryRunner.ParkingEncounter();
            Assert.IsTrue(enc2 == null || enc2.State == Encounter.Phase.Armed, s);
            // 남은 콜백 없음 — 패배 고르기를 불러도 시우가 주차장으로 안 옮겨짐(예전: Esc 가 남은 '그만'을 눌러 ResetAll 이 시우를 주차장 좌표로)
            var p0 = M3Lab.Motor.Position;
            hud?.DefeatChoose(false);
            yield return Lab.Frames(5);
            float moved = (M3Lab.Motor.Position - p0).magnitude;
            Debug.Log($"[Q] Q62a 패배 화면에서 불러오기\n{s}\n패배 고르기 뒤 시우 이동 {moved:F2}m · 주차장까지 {Vector3.Distance(M3Lab.Motor.Position, pk):F0}m");
            Assert.Less(moved, 0.5f, "패배 화면 콜백이 남아 시우를 옮김");
        }

        // ───────────────────────── Q62c G1(2026-11-05): 시비 카드(C1-24) 싸움이 시간 한도를 넘기면 패로 치고 전투를 끔 · 봇이 끈 입력·무적을 되돌림
        //   (예전엔 Fight 그대로 루프를 빠져 카드가 끝난 뒤 전투가 켜진 채 자유 이동)
        [UnityTest, Timeout(900000)]
        public IEnumerator Q62c_Street_Card_Timeout_Ends_Combat()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            var c = w.Def.Card("C1-24");
            Assert.NotNull(c, "C1-24");
            yield return Start(w, Mid(w.Def, 505, 16 * 60 + 40, SceneLoader.Zone1, m => m.G.Fatigue = 0));
            w.StreetFightLimit = 2f;
            int loss0 = w.Model.S.StreetLosses, win0 = w.Model.S.StreetWins;
            var pc = Object.FindAnyObjectByType<PlayerCombat>();
            var pin = pc.GetComponent<PInput>() ?? Object.FindAnyObjectByType<PInput>();
            bool done = false, fought = false;
            IEnumerator Wrap() { yield return w.RunCardBusy(c, true); done = true; }
            w.StartCoroutine(Wrap());
            float t0 = Time.realtimeSinceStartup;
            while (!done && Time.realtimeSinceStartup - t0 < 300f) { if (pc.Active) fought = true; yield return null; }
            yield return Free(w);
            yield return Lab.Frames(10);
            var enc = StoryRunner.ParkingEncounter();
            string s = $"끝 {done} · 싸움 {fought} · 전투 {pc.Active} · 전투 맵 {pin.CombatInput} · 입력 켬 {pin.enabled} · 무적 {pc.Me.DebugInvuln} · 시비 {enc?.State} · 승 +{w.Model.S.StreetWins - win0} 패 +{w.Model.S.StreetLosses - loss0}";
            Debug.Log("[Q] Q62c 시비 카드 시간 초과: " + s);
            Assert.IsTrue(done, "카드가 안 끝남 — " + s);
            Assert.IsTrue(fought, "싸움이 안 켜짐(시험 전제) — " + s);
            Assert.IsFalse(pc.Active, "시간 초과 뒤 전투가 켜진 채 자유 이동 — " + s);
            Assert.IsFalse(pin.CombatInput, s);
            Assert.IsTrue(pin.enabled, "봇이 끈 입력이 안 돌아옴 — " + s);
            Assert.IsFalse(pc.Me.DebugInvuln, s);
            Assert.AreEqual(Encounter.Phase.Armed, enc.State, s);
            Assert.AreEqual(loss0 + 1, w.Model.S.StreetLosses, "시간 초과 = 패 — " + s);
        }

        // ───────────────────────── Q62d G1(2026-11-05): 시비 카드(C1-24) 사람 손 — 대사 뒤 주차장에 들어가 손 놓고 지면 카드가 '그만'으로 끝내고 전투가 꺼짐
        //   (QA B15 의 C1-24 사람 손 부분은 카드 조건 'after:school' 탓에 한 번도 안 돌았음 — 여기서 봄)
        [UnityTest, Timeout(900000)]
        public IEnumerator Q62d_Street_Card_Human_Loss_Ends_Combat()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            var c = w.Def.Card("C1-24");
            Assert.NotNull(c, "C1-24");
            yield return Start(w, Mid(w.Def, 505, 16 * 60 + 40, SceneLoader.Zone1, m => m.G.Fatigue = 0));
            w.BotSkill = 0f;                                  // 싸움은 사람(손 놓기)
            w.Story.Bot = StoryRunner.BotMode.Fast;           // 대사는 빠르게
            var pc = Object.FindAnyObjectByType<PlayerCombat>();
            var pin = pc.GetComponent<PInput>() ?? Object.FindAnyObjectByType<PInput>();
            int loss0 = w.Model.S.StreetLosses;
            bool done = false, fought = false;
            IEnumerator Wrap() { yield return w.RunCardBusy(c, true); done = true; }
            w.StartCoroutine(Wrap());
            float t0 = Time.realtimeSinceStartup;
            Encounter enc = StoryRunner.ParkingEncounter();
            int el0 = enc != null ? enc.Losses : 0;      // 패배(Lost)는 카드가 같은 프레임쯤 '그만'으로 넘겨 단계로는 못 잡음 → 인카운터 패 수로
            while (!done && Time.realtimeSinceStartup - t0 < 240f)
            {
                enc = StoryRunner.ParkingEncounter();
                // 대사가 끝나 '안으로 들어가면 시작'이면 주차장 안쪽(상대 앞)으로 — 사람이 걸어 들어가는 대신
                if (enc != null && enc.State == Encounter.Phase.Armed && !Encounter.Suppress && !w.Story.InScene && !fought)
                {
                    M3Lab.Motor.Teleport(enc.Def.ToWorld(new Vector2(1.5f, -1.0f)) + Vector3.up * 0.1f, enc.Def.Yaw);
                    yield return Lab.Frames(5);
                }
                if (pc.Active && !fought) { fought = true; pc.Me.DebugInvuln = false; pc.Me.Hp = 0; }
                yield return null;
            }
            yield return Free(w);
            yield return Lab.Frames(10);
            enc = StoryRunner.ParkingEncounter();
            bool lost = enc != null && enc.Losses > el0;
            var hud = StageHud.Instance;
            string s = $"끝 {done} · 싸움 {fought} · 짐 {lost} · 전투 {pc.Active} · 전투 맵 {pin.CombatInput} · 맵 {InputMaps.Enabled(w.Actions)} · 패배 화면 {(hud != null && hud.DefeatShown)} · 시비 {enc?.State} · 패 +{w.Model.S.StreetLosses - loss0}";
            Debug.Log("[Q] Q62d 시비 카드 사람 손 패배: " + s);
            Assert.IsTrue(done, "카드가 안 끝남 — " + s);
            Assert.IsTrue(fought && lost, "싸움·패배가 안 일어남(시험 전제) — " + s);
            Assert.IsFalse(pc.Active, "카드 시비에 진 뒤 전투 상태 — " + s);
            Assert.IsFalse(pin.CombatInput, s);
            Assert.AreEqual(InputMaps.Explore, InputMaps.Enabled(w.Actions), s);
            Assert.IsFalse(hud != null && hud.DefeatShown, s);
            Assert.AreEqual(Encounter.Phase.Armed, enc.State, s);
            Assert.AreEqual(loss0 + 1, w.Model.S.StreetLosses, s);
        }

        // ───────────────────────── Q63 G1-N1(2026-11-05): 드릴 결과 카드가 떠 있는 동안 체육관 '나가기' 문이 안 열림 · 카드를 닫으면 문은 그대로 열림.
        //   결과 카드 동안 PInput·Explore 는 살아 있지만 '드릴 뒤' 바쁨(AfterDrill — 카드를 닫을 때까지)이 문지기를 막음 — 고치기 전 코드에서도 통과(조사가 짚은 '카드 뒤 문'은 재현 안 됨, 회귀 시험으로 남김)
        [UnityTest, Timeout(900000)]
        public IEnumerator Q63_Door_Closed_Behind_Drill_Card()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            GymManager gm = null;
            yield return Gym(w, 310, 16 * 60 + 45, x => gm = x);
            Assert.IsTrue(gm.StartDrill("D01"), "D01 시작(사람)");
            float r0 = Time.realtimeSinceStartup;
            while (gm.Runner.State != DrillRunner.Phase.Round && Time.realtimeSinceStartup - r0 < 30f) yield return null;
            gm.Runner.Abort();
            for (int i = 0; i < 120 && !gm.Card.Visible; i++) yield return null;
            Assert.IsTrue(gm.Card.Visible, "결과 카드(사람 — 자동으로 안 닫힘)");
            var pin = Object.FindAnyObjectByType<PInput>();
            string live = $"결과 카드 동안 입력 켬 {pin.enabled} · 맵 {InputMaps.Enabled(w.Actions)} · Free {w.Free} · 바쁨 '{w.Busy}'";
            var exit = w.Loader.Stage.GetComponentsInChildren<StageDoor>(true).First(d => d.Target == SceneLoader.Zone1);
            var sp = w.Loader.Stage.SpawnPoint("Spawn");
            M3Lab.Motor.Teleport(sp.position + Vector3.up * 0.05f, sp.eulerAngles.y);
            yield return Lab.Frames(3);
            var near = Interactable.Nearest(M3Lab.Motor.Position);
            int u0 = StageDoor.Uses;
            exit.GetComponent<Interactable>().Use();       // PInput 이 E 에서 부르는 것과 같음(가장 가까운 상호작용)
            yield return Lab.Frames(30);
            string behind = $"카드 뒤 문 → 쓰임 +{StageDoor.Uses - u0} · {w.Loader.Place} · 로더 바쁨 {w.Loader.Busy} · 카드 {gm.Card.Visible} · 가장 가까운 {(near != null ? near.Prompt : "-")}";
            Assert.AreEqual(u0, StageDoor.Uses, "결과 카드 뒤에서 문이 열림 — " + behind);
            Assert.AreEqual(SceneLoader.Gym, w.Loader.Place, behind);
            Assert.IsTrue(gm.Card.Visible, behind);
            // 카드를 닫으면(자유 이동) 문은 열림 — 문지기가 평소 문까지 막지 않음
            for (int i = 0; i < 300 && gm.Busy; i++) { gm.CloseCard(); yield return null; }
            yield return Free(w);
            exit.GetComponent<Interactable>().Use();
            yield return Lab.Frames(10);
            for (int i = 0; i < 1200 && w.Loader.Busy; i++) yield return null;
            string open = $"카드 닫은 뒤 문 → 쓰임 +{StageDoor.Uses - u0} · {w.Loader.Place}";
            Debug.Log($"[Q] Q63 드릴 결과 카드 뒤 문\n{live}\n{behind}\n{open}");
            Assert.AreEqual(u0 + 1, StageDoor.Uses, open);
            Assert.AreEqual(SceneLoader.Zone1, w.Loader.Place, open);
        }

        // ───────────────────────── Q64 G2(2026-11-05): 밤 경계마다 '다음' 판에 새벽 배달(06:00) — 22:00·23:00(내일 새벽) · 01:59(오늘 새벽) · 02:00 강제 귀가 뒤 · 05:02(강제 잠 구간) 뒤 ·
        //   05:31 배달통 안내 · 토요일 밤(내일 일요일 = 배달 없음)은 '잠(이불)' 그대로. 예전엔 밤 내내 '잠(이불)'만이라 다음 새벽 배달이 HUD 어디에도 안 보였음
        [UnityTest, Timeout(1200000)]
        public IEnumerator Q64_Delivery_Shown_At_Night_Boundaries()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            var log = new List<string>();
            void Quiet(WorldModel m) { m.G.Fatigue = 0; foreach (var c in w.Def.Cards) if (c.Demo && c.Kind != CardKind.Repeat && !m.CardDone(c.Id)) m.MarkCard(c, 3); }
            string Next() => w.Hud != null ? w.Hud.NextLine : "";
            void Check(string at, string want, bool has = true)
            {
                string n = Next();
                log.Add($"{at}: {GameClock.DateText} {GameClock.TimeText} {w.Loader.Place} · {n}");
                if (has) StringAssert.Contains(want, n, at + " '다음' 판");
                else StringAssert.DoesNotContain(want, n, at + " '다음' 판");
            }
            // ① 3/10(화) 22:00 국밥집 — 내일(수) 새벽 배달
            yield return Start(w, Mid(w.Def, 310, 22 * 60, "Gukbap", Quiet));
            yield return Lab.Frames(20);
            Check("22:00 국밥집", "배달"); Check("22:00 국밥집", "06:00");
            Assert.IsTrue(PlaceMarker.All.Any(m => m != null && m.Func == "job_delivery" && m.isActiveAndEnabled), "국밥집 배달통 표시(시각과 관계없이)");
            // ② 23:00
            GameClock.SetDate(2026, 3, 10, 23 * 60);
            yield return Lab.Frames(20);
            Check("23:00 국밥집", "배달");
            // ③ 3/11(수) 01:59 골목 — 오늘 새벽 배달 → 02:00 강제 귀가 → 집 05:30
            yield return Start(w, Mid(w.Def, 311, 1 * 60 + 59, SceneLoader.Zone1, Quiet));
            yield return Lab.Frames(10);
            Check("01:59 골목", "배달");
            int s0 = w.Sleeps;
            GameClock.SetDate(2026, 3, 11, 2 * 60);
            float z0 = Time.realtimeSinceStartup;
            while (w.Busy != "잠" && Time.realtimeSinceStartup - z0 < 5f) yield return null;
            Assert.AreEqual("잠", w.Busy, "02:00 강제 귀가가 안 걸림");
            yield return Until(() => w.Sleeps > s0 && w.Free && !w.Loader.Busy, 90f);
            yield return Lab.Frames(10);
            Assert.AreEqual("Home", w.Loader.Place, "강제 귀가 → 집");
            Assert.AreEqual(w.Def.Balance.Wake, (int)GameClock.Minutes, "05:30 기상");
            Check("02:00 강제 귀가 뒤", "새벽 국밥 배달"); Check("02:00 강제 귀가 뒤", "06:00");
            // ④ 3/12(목) 05:02 국밥집 — 02:00~05:30 은 깨어 있을 수 없음(강제 잠 → 집 05:30)
            s0 = w.Sleeps;
            yield return Start(w, Mid(w.Def, 312, 5 * 60 + 2, "Gukbap", Quiet));
            yield return Until(() => w.Sleeps > s0 && w.Free && !w.Loader.Busy, 90f);
            yield return Lab.Frames(10);
            Assert.AreEqual("Home", w.Loader.Place, "05:02 = 강제 잠 구간 → 집");
            Assert.AreEqual(w.Def.Balance.Wake, (int)GameClock.Minutes, "05:30 기상");
            Check("05:02 뒤", "새벽 국밥 배달");
            // ⑤ 05:31 국밥집 — 배달통 안내
            yield return Start(w, Mid(w.Def, 312, 5 * 60 + 31, "Gukbap", Quiet));
            yield return Lab.Frames(20);
            string dp = w.MarkerPrompt("job_delivery");
            log.Add($"05:31 배달통: {dp}");
            StringAssert.StartsWith("새벽 국밥 배달 06:00~06:50", dp, "05:31 배달통 안내");
            // ⑥ 3/14(토) 22:00 — 내일(일) 배달 없음
            yield return Start(w, Mid(w.Def, 314, 22 * 60, SceneLoader.Zone1, Quiet));
            yield return Lab.Frames(10);
            Check("토 22:00 골목", "배달", false);
            Debug.Log("[Q] Q64 밤 경계 배달 표시\n" + string.Join("\n", log));
        }

        // ───────────────────────── Q65 G5(2026-11-05): QA B07 순서(18:00 체육관 · 사람 드릴 1 + 플레이 10종 연속) — 드릴마다 게임 30분이라 8개 뒤 22:00 에 D08 부터 '문 닫았다'(게임 규칙 그대로, 이유는 관장 말로 뜸) ·
        //   QA 봇 규칙(QaBot.KeepGymOpen — 마감 전이면 같은 날 18:00 으로)이면 11개 모두 시작
        [UnityTest, Timeout(1500000)]
        public IEnumerator Q65_Drill_Sequence_Gym_Close_And_Bot_Rule()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            yield return Start(w, Mid(w.Def, 310, 18 * 60, SceneLoader.Gym, m => { m.G.Fatigue = 0; foreach (var c in w.Def.Cards) if (c.Demo && c.Kind != CardKind.Repeat && !m.CardDone(c.Id)) m.MarkCard(c, 3); }));
            GymManager gm = null;
            for (int i = 0; i < 300 && gm == null; i++) { gm = GymManager.Instance; yield return null; }
            Assert.NotNull(gm, "체육관");
            var b = w.Def.Balance;
            var ids = new List<string> { "D11" };
            ids.AddRange(gm.Book.Drills.Where(x => x.Playable).Select(x => x.Id));
            var log = new List<string>();
            bool ok = false; double spent = 0;
            IEnumerator One(string id)
            {
                GymState.Current.Fatigue = 0; GymState.Current.InjuryUntil = -1; GymState.Current.InjuryKind = "";
                double t0 = GameClock.Minutes;
                ok = gm.StartDrill(id, 0.9f);
                spent = 0;
                if (!ok) yield break;
                float r0 = Time.realtimeSinceStartup;
                while (gm.Runner.State != DrillRunner.Phase.Round && Time.realtimeSinceStartup - r0 < 30f) yield return null;
                gm.Runner.Abort();
                for (int i = 0; i < 300 && gm.Busy; i++) { gm.CloseCard(); yield return null; }
                yield return Free(w);
                spent = GameClock.Minutes - t0;
            }
            // ① 규칙 없이(예전 B07): 8개 뒤 22:00 — D08 부터 '문 닫았다'
            int started = 0; string first = null; double at = -1; bool notice = false; string cue = "";
            foreach (var id in ids)
            {
                yield return One(id);
                if (ok) { started++; log.Add($"{id} 시작 · {spent:F1}분 → {GameClock.TimeText}"); Assert.That(spent, Is.InRange(b.DrillMinutes - 0.5, b.DrillMinutes + 1.5), $"{id} 드릴 하나 = 게임 {b.DrillMinutes}분"); }
                else if (first == null) { first = id; at = GameClock.Minutes; notice = gm.Hud.NoticeShown; cue = gm.Hud.Cue; log.Add($"{id} 거절 {GameClock.TimeText} · 관장 말 '{cue}'"); }
            }
            Assert.AreEqual(8, started, "18:00 + 30분 × 8 = 22:00 까지 시작");
            Assert.AreEqual("D08", first, "처음 막히는 드릴(QA t3·t5 와 같음)");
            Assert.GreaterOrEqual((int)at, b.GymClose, "막힌 때 = 문 닫는 시각 뒤");
            Assert.IsTrue(notice, "막히면 관장 말 판만(Q34)");
            StringAssert.Contains("문 닫", cue, "막힌 이유가 화면에");
            yield return Lab.Frames(200);
            // ② QA 봇 규칙: 같은 날 18:00 부터 다시 — 드릴마다 KeepGymOpen
            GameClock.SetDate(2026, 3, 10, 18 * 60);
            yield return Free(w);
            started = 0; int backs = 0; var refused = new List<string>();
            foreach (var id in ids)
            {
                if (QaBot.KeepGymOpen(b)) { backs++; log.Add($"봇 규칙: 시계 되돌림 → {GameClock.TimeText}"); }
                yield return One(id);
                if (ok) started++; else refused.Add($"{id} {GameClock.TimeText} '{gm.Hud.Cue}'");
            }
            Debug.Log("[Q] Q65 드릴 순서와 체육관 마감\n" + string.Join("\n", log) + $"\n봇 규칙: 시작 {started}/{ids.Count} · 되돌림 {backs}");
            Assert.IsEmpty(refused, "봇 규칙으로도 막힌 드릴: " + string.Join(", ", refused));
            Assert.AreEqual(ids.Count, started);
            Assert.GreaterOrEqual(backs, 1, "되돌림이 한 번은 있어야(11개 × 30분 > 4시간)");
        }

        // ───────────────────────── Q66(2026-11-05 QA B16 에서 찾음): 드릴 메뉴를 열고 닫아도 Menu 맵이 켜진 채 — 일시정지 메뉴(GameUi)·휴대폰·고르기 판은 Menu 맵이 늘 켜져 있다고 봄.
        //   예전엔 드릴 메뉴가 닫을 때 Menu 맵을 꺼서, 드릴을 한 번 고른 뒤엔 일시정지 메뉴 ↑↓·Enter 가 안 먹었음(카드 중 '저장하고 타이틀로'를 못 고름)
        [UnityTest, Timeout(600000)]
        public IEnumerator Q66_Drill_Menu_Keeps_Menu_Map_On()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            GymManager gm = null;
            yield return Gym(w, 310, 18 * 60, x => gm = x);
            var menu = w.Actions.FindActionMap("Menu", false);
            Assert.NotNull(menu, "Menu 맵");
            bool before = menu.enabled;
            var mk = gm.Markers.First(x => x != null && x.isActiveAndEnabled && gm.Book.Find(x.DrillId) != null && gm.Book.Find(x.DrillId).Playable);
            gm.OnMarkerUsed(mk);
            yield return Lab.Frames(5);
            bool opened = gm.Menu.Open;
            gm.Menu.Close();
            yield return Lab.Frames(5);
            Debug.Log($"[Q] Q66 드릴 메뉴({mk.DrillId}) 열기 {opened} → 닫은 뒤 Menu 맵 {before} → {menu.enabled}");
            Assert.IsTrue(opened, "드릴 메뉴가 안 열림(시험 전제)");
            Assert.IsTrue(before, "Menu 맵은 처음부터 켜져 있어야(GameUi)");
            Assert.IsTrue(menu.enabled, "드릴 메뉴를 닫은 뒤 Menu 맵이 꺼짐 — 일시정지 메뉴 ↑↓·Enter 가 안 먹음");
        }

        // ───────────────────────── Q67(2026-11-05 QA B15 에서 찾음): 봇 스파링(D11) 뒤 시우에 전투 봇이 남지 않음 — 예전엔 남아 다음 시비를 혼자 싸워 이겼음('손 놓고 지기'가 이김)
        [UnityTest, Timeout(600000)]
        public IEnumerator Q67_Spar_Bot_Removed_After_Drill()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            GymManager gm = null;
            yield return Gym(w, 310, 18 * 60, x => gm = x);
            GymState.Current.Fatigue = 0; GymState.Current.InjuryUntil = -1;
            Assert.IsTrue(gm.StartDrill("D11", 0.9f), "D11 시작(봇)");
            yield return Until(() => gm.Runner.State == DrillRunner.Phase.Round, 30f);
            yield return Real(1.5f);                     // 봇이 라운드 중 붙음
            var pc = Object.FindAnyObjectByType<PlayerCombat>();
            bool during = pc.GetComponent<FightBot>() != null;
            gm.Runner.Abort();
            for (int i = 0; i < 300 && gm.Busy; i++) { gm.CloseCard(); yield return null; }
            yield return Free(w);
            yield return Lab.Frames(5);
            var left = pc.GetComponent<FightBot>();
            Debug.Log($"[Q] Q67 스파링 봇: 라운드 중 붙음 {during} · 끝난 뒤 남음 {(left != null ? (left.enabled ? "켜짐" : "꺼짐") : "없음")}");
            Assert.IsTrue(during, "라운드 중 봇이 안 붙음(시험 전제)");
            Assert.IsTrue(left == null, "스파링이 끝났는데 전투 봇이 시우에 남음");
        }

        // ───────────────────────── Q70(G7 — 2026-11-05): 체육관 카드 장면(N13 「다섯 사람」)이 끝나면 장면이 세운 배역이 제자리로 — 출장 코치는 꺼지고 이윤준·관원은 원래 자리.
        //   예전엔 장소를 옮길 때(먹 닦기)만 되돌려, 같은 방문 안에서 이어진 5/3 19:30 서진수 첫 레슨 카드에 코치 다섯이 반원 그대로 서 있었고
        //   시우(장면이 매트 가장자리 소켓에 세움)와 레슨 코치(같은 소켓)가 겹쳐 레슨 카메라 가운데를 시우 뒤통수가 덮었음(QA t3 사진 193)
        [UnityTest, Timeout(1500000)]
        public IEnumerator Q70_Gym_Scene_Cast_Restored_Before_Lesson()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            yield return Start(w, Mid(w.Def, 503, 18 * 60 + 40, SceneLoader.Gym, m => { m.G.Fatigue = 0; m.L.SetWallet(900000); m.Unlock("lesson"); m.Unlock("lesson.guests"); m.Unlock("lesson.seungmin"); new EventDirector(m).Done(w.Def.Card("N12"), 1); }));
            var gm = GymManager.Instance;
            Assert.NotNull(gm, "체육관");
            yield return Lab.Frames(20);
            var bad = new List<string>(); var log = new List<string>();
            int GuestsOn() => gm.Guests.Count(g => g != null && g.gameObject.activeSelf);
            int MembersOn() => gm.Members.Count(m => m != null && m.gameObject.activeSelf);
            int guests0 = GuestsOn(), members0 = MembersOn();
            var yun0 = gm.Yunjun != null ? gm.Yunjun.transform.position : Vector3.zero;
            string why = w.BookNext("jinsoo_seo");
            Assert.AreEqual("", why, "5/3(일) 19:30 서진수 레슨 예약(시험 전제)");
            var story = w.Story;
            story.Bot = StoryRunner.BotMode.Read;
            w.BotRead = 8f;
            bool done = false;
            IEnumerator Wrap() { yield return w.RunCardBusy(w.Def.Card("N13"), true); done = true; }
            w.StartCoroutine(Wrap());
            int guestsMid = 0;
            float t0 = Time.realtimeSinceStartup;
            while (!done && Time.realtimeSinceStartup - t0 < 300f) { guestsMid = Mathf.Max(guestsMid, GuestsOn()); yield return null; }
            Assert.IsTrue(done, "N13 안 끝남");
            yield return Lab.Frames(2);
            float yunMove = gm.Yunjun != null ? HitResolver.Flat(gm.Yunjun.transform.position - yun0).magnitude : 0f;
            yield return Lab.Frames(10);
            float yunMove10 = gm.Yunjun != null ? HitResolver.Flat(gm.Yunjun.transform.position - yun0).magnitude : 0f;
            log.Add($"N13 전: 출장 코치 {guests0} · 관원 {members0} · 장면 중 출장 코치 최대 {guestsMid}");
            log.Add($"N13 뒤: 배역 남음 {story.CastHeld} · 출장 코치 {GuestsOn()} · 관원 {MembersOn()} · 이윤준 자리 차 {yunMove:F2}m(10프레임 뒤 {yunMove10:F2}m)");
            if (guestsMid < 4) bad.Add($"장면 중 출장 코치가 {guestsMid}명(시험 전제 — N13 다섯이 섬)");
            if (story.CastHeld != 0) bad.Add($"N13 뒤 장면 배역 {story.CastHeld}개가 안 되돌아감");
            if (GuestsOn() != guests0) bad.Add($"N13 뒤 출장 코치 {GuestsOn()}명이 남음(장면 전 {guests0})");
            if (MembersOn() != members0) bad.Add($"N13 뒤 관원 {MembersOn()}명(장면 전 {members0})");
            if (yunMove > 0.3f || yunMove10 > 0.3f) bad.Add($"N13 뒤 이윤준이 제자리가 아님 {yunMove:F2}/{yunMove10:F2}m");
            // 이어지는 레슨(5/3 19:30): 카드 동안 출장 코치는 서진수 한 명 · 시우와 1.2m 넘게 · 레슨 카메라가 코치 얼굴을 봄 · 다른 코치 얼굴이 화면에 없음
            GameClock.AdvanceTo(19 * 60 + 25);
            var board = story.Board;
            t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 60f && !(board != null && board.Shown && w.LessonCamera != null)) yield return null;
            if (board == null || !board.Shown || w.LessonCamera == null) bad.Add($"레슨 카드·카메라 안 뜸(시각 {GameClock.TimeText} · 바쁨 '{w.Busy}')");
            else
            {
                yield return Lab.Frames(2);
                var lc = w.LessonCamera;
                var seo = gm.Guest("jinsoo_seo");
                var on = gm.Guests.Where(g => g != null && g.gameObject.activeSelf).Select(g => g.Id).ToList();
                float gap = HitResolver.Flat(M3Lab.Motor.Position - seo.transform.position).magnitude;
                seo.Anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var head = seo.Anim.GetBoneTransform(HumanBodyBones.Head).position + Vector3.up * 0.08f;
                Vector3 Vp(Vector3 q) { var lp = lc.transform.InverseTransformPoint(q); float ty = Mathf.Tan(CutCams.Fov(CamPreset.Mid) * 0.5f * Mathf.Deg2Rad); return new Vector3(0.5f + 0.5f * (lp.x / lp.z) / (ty * 16f / 9f), 0.5f + 0.5f * (lp.y / lp.z) / ty, lp.z); }
                var vh = Vp(head);
                bool blocked = lc.Blocked(lc.transform.position, head);
                var seen = new List<string>();
                foreach (var g in gm.Guests)
                {
                    if (g == null || g == seo || !g.gameObject.activeInHierarchy || g.Anim == null) continue;
                    var v = Vp(g.Anim.GetBoneTransform(HumanBodyBones.Head).position);
                    if (v.z > 0f && v.x > 0f && v.x < 1f && v.y > 0f && v.y < 1f) seen.Add(g.Id);
                }
                log.Add($"레슨 카드 {board.Title}: 켜진 출장 코치 [{string.Join(",", on)}] · 시우–서진수 {gap:F2}m · 서진수 머리 화면 {vh.x:F2},{vh.y:F2} · 가림 {blocked} · 화면 속 다른 코치 [{string.Join(",", seen)}] · 배역 남음 {story.CastHeld}");
                if (on.Count != 1 || on[0] != "jinsoo_seo") bad.Add($"레슨 카드에 출장 코치 [{string.Join(",", on)}](서진수 한 명이어야)");
                if (gap < 1.2f) bad.Add($"시우와 레슨 코치가 겹침 {gap:F2}m");
                if (vh.z <= 0f || vh.x < 0.05f || vh.x > 0.6f || vh.y < 0.2f || vh.y > 0.98f) bad.Add($"서진수 얼굴이 레슨 카메라 왼쪽 60% 밖 {vh}");
                if (blocked) bad.Add("서진수 얼굴이 가림(레슨 카메라)");
                if (seen.Count > 0) bad.Add($"레슨 카메라에 다른 코치 [{string.Join(",", seen)}]");
            }
            Debug.Log("[Q] Q70 N13 뒤 레슨\n" + string.Join("\n", log));
            Assert.IsEmpty(bad, string.Join(" | ", bad));
        }

        // ───────────────────────── Q71(G8 — 2026-11-05): 샌드백은 친 쪽으로 밀려 나감(예전 BagSwing.Hit 부호가 반대 — 매달린 백(피벗 아래)인데 피벗 위 점 기준으로 써서
        //   치는 사람·카메라 쪽으로 넘어와 시우 머리·몸을 덮었음, QA p4z 한 바퀴 D05 프레임) · 동쪽 벽(백 0.8m 옆)을 안 뚫게 각 한도
        [UnityTest, Timeout(300000)]
        public IEnumerator Q71a_Bag_Swings_Away_From_Hit()
        {
            var log = new List<string>(); var bad = new List<string>();
            float prevCap = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            try
            {
                foreach (var (name, dir, power, hits, every) in new[] { ("오른쪽 한 번", Vector3.right, 3f, 1, 1), ("앞 한 번", Vector3.forward, 3f, 1, 1), ("오른쪽 세게 0.1초마다 10번", Vector3.right, 5f, 10, 6) })
                {
                    var go = new GameObject("bag_q71"); var sw = go.AddComponent<BagSwing>(); sw.Length = 2.68f;
                    var holder = new GameObject("holder").transform; holder.SetParent(go.transform, false); holder.localPosition = new Vector3(0f, -2.08f, 0f);
                    float maxAng = 0f; Vector3 far = Vector3.zero; Vector3 first = Vector3.zero;
                    for (int i = 0; i < hits * every + 40; i++)
                    {
                        if (i % every == 0 && i / every < hits) sw.Hit(dir, power);
                        yield return null;
                        maxAng = Mathf.Max(maxAng, sw.Angle);
                        var off = holder.position - go.transform.position; off.y = 0f;
                        if (i == 8) first = off;
                        if (off.magnitude > far.magnitude) far = off;
                    }
                    Object.Destroy(go);
                    float along = Vector3.Dot(first, dir);
                    log.Add($"{name}: 8프레임 뒤 백 아래 끝 {first.x:+0.00;-0.00},{first.z:+0.00;-0.00}m(친 쪽 {along:+0.00;-0.00}) · 가장 멀리 {far.x:+0.00;-0.00},{far.z:+0.00;-0.00} · 최대 각 {maxAng:F1}°");
                    if (along < 0.05f) bad.Add($"{name}: 백이 친 쪽으로 안 감({along:+0.00;-0.00}m)");
                    if (maxAng > 15.5f) bad.Add($"{name}: 각 {maxAng:F1}° > 15° (동쪽 벽 0.8m — 백 아래 끝이 벽을 뚫음)");
                }
            }
            finally { Time.captureDeltaTime = prevCap; }
            Debug.Log("[Q] Q71a 샌드백 흔들림\n" + string.Join("\n", log));
            Assert.IsEmpty(bad, string.Join(" | ", bad));
        }

        static float SegSeg(Vector3 a0, Vector3 a1, Vector3 b0, Vector3 b1)
        {
            float best = float.MaxValue;
            for (int i = 0; i <= 40; i++)
            {
                var p = Vector3.Lerp(a0, a1, i / 40f);
                var ab = b1 - b0; float t = Mathf.Clamp01(Vector3.Dot(p - b0, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                best = Mathf.Min(best, Vector3.Distance(p, b0 + ab * t));
            }
            return best;
        }

        //   D05 드릴 카메라(네 화면비): 백이 시우 몸을 뚫거나 카메라–시우 머리 사이에 오지 않음 · 타격 별이 시우 얼굴 위에 안 그려짐 · 시우 머리·가슴이 화면 안(위 타이머 판 밑)
        [UnityTest, Timeout(900000)]
        public IEnumerator Q71b_D05_Bag_Clear_Of_Siwoo_All_Aspects()
        {
            yield return M3Lab.OpenZone1();
            var r = M3Lab.Runner;
            GymState.Current = new GymSave();
            yield return r.Loader.Go(SceneLoader.Gym, "Floor", false, Vector3.zero, 0f, false);
            yield return null; yield return null;
            var gm = GymManager.Instance;
            Assert.NotNull(gm, "체육관");
            var book = Object.Instantiate(gm.Book);
            foreach (var d in book.Drills) { d.RoundSec = 14f; d.RestSec = 1f; d.Rounds = 1; }
            gm.Book = book;
            var st = gm.Sock("Sock_Drill_D05_01");
            BagSwing bag = null; float bd = float.MaxValue;
            foreach (var b in gm.Bags) { if (b == null) continue; float d = (b.transform.position - st.position).sqrMagnitude; if (d < bd) { bd = d; bag = b; } }
            Assert.NotNull(bag, "D05 샌드백");
            var hitPts = new List<Vector3>();
            void OnHit(HitEvent ev) { if (bag != null && ev.Victim == bag.Target) hitPts.Add(ev.Point); }
            Fighter.AnyHit += OnHit;
            var log = new List<string>(); var bad = new List<string>();
            int frames = 0, through = 0, cover = 0, fxFace = 0, wall = 0, hits = 0; float minChest = 99f, minCover = 99f, minFx = 99f, maxWall = -99f, maxAng = 0f;
            var aspects = new[] { ("16:9", 16f / 9f), ("16:10", 16f / 10f), ("21:9", 2520f / 1080f), ("4:3", 4f / 3f) };
            var off = new Dictionary<string, int>(); foreach (var a in aspects) off[a.Item1] = 0;
            string offEx = "";
            try
            {
                Assert.IsTrue(gm.StartDrill("D05", 1f), "D05 시작(봇)");
                var an = M3Lab.Motor.GetComponentInChildren<Animator>();
                an.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                float t0 = Time.realtimeSinceStartup;
                while (gm.Runner.State != DrillRunner.Phase.Result && Time.realtimeSinceStartup - t0 < 120f)
                {
                    yield return null;
                    if (gm.Runner.State != DrillRunner.Phase.Round) { hitPts.Clear(); continue; }
                    frames++;
                    var cam = gm.DrillCam.transform;
                    float ty = Mathf.Tan(gm.DrillCam.Lens.FieldOfView * 0.5f * Mathf.Deg2Rad);
                    var head = an.GetBoneTransform(HumanBodyBones.Head).position + Vector3.up * 0.08f;
                    var chest = an.GetBoneTransform(HumanBodyBones.Chest).position;
                    var pv = bag.transform.position; var up = bag.transform.up;
                    Vector3 a0 = pv - up * 0.7f, a1 = pv - up * 2.08f;
                    maxAng = Mathf.Max(maxAng, bag.Angle);
                    // 1) 가슴 높이에서 백 축과 시우 가슴(수평)
                    float k = Mathf.InverseLerp(a0.y, a1.y, chest.y);
                    float dChest = HitResolver.Flat(Vector3.Lerp(a0, a1, k) - chest).magnitude;
                    minChest = Mathf.Min(minChest, dChest); if (dChest < 0.35f) through++;
                    // 2) 카메라 → 시우 머리 선과 백 축(백 반지름 0.22 + 0.03)
                    float dCov = SegSeg(cam.position, head, a0, a1);
                    minCover = Mathf.Min(minCover, dCov); if (dCov < 0.25f) cover++;
                    // 3) 네 화면비: 머리·가슴이 화면 안, 머리는 위 타이머 판(0.86) 밑
                    Vector3 Vp(Vector3 q, float asp) { var lp = cam.InverseTransformPoint(q); return new Vector3(0.5f + 0.5f * (lp.x / lp.z) / (ty * asp), 0.5f + 0.5f * (lp.y / lp.z) / ty, lp.z); }
                    foreach (var (an2, asp) in aspects)
                    {
                        var vh = Vp(head, asp); var vc = Vp(chest, asp);
                        if (vh.z <= 0f || vh.x < 0.03f || vh.x > 0.97f || vh.y < 0.03f || vh.y > 0.86f || vc.x < 0.03f || vc.x > 0.97f || vc.y < 0.03f) { off[an2]++; if (offEx == "") offEx = $"{an2} 머리 {vh.x:F2},{vh.y:F2} 가슴 {vc.x:F2},{vc.y:F2}"; }
                    }
                    // 4) 타격 별(깊이 무시 — 맞는 점에 그려짐)이 시우 얼굴 위(화면 세로 단위 0.15 안)
                    var vh0 = Vp(head, 16f / 9f);
                    foreach (var hp in hitPts)
                    {
                        hits++;
                        var vp = Vp(hp, 16f / 9f);
                        float dd = new Vector2((vp.x - vh0.x) * 16f / 9f, vp.y - vh0.y).magnitude;
                        minFx = Mathf.Min(minFx, dd); if (dd < 0.15f) fxFace++;
                    }
                    hitPts.Clear();
                    // 5) 동쪽 벽(체육관 x 20.0): 백 아래 끝 + 반지름
                    float wx = gm.transform.InverseTransformPoint(a1).x + 0.2f;
                    maxWall = Mathf.Max(maxWall, wx); if (wx > 19.95f) wall++;
                }
            }
            finally { Fighter.AnyHit -= OnHit; }
            for (int i = 0; i < 600 && gm.Busy; i++) { gm.CloseCard(); yield return null; }
            log.Add($"라운드 {frames}프레임 · 타격 {hits} · 백 최대 각 {maxAng:F1}°");
            log.Add($"가슴 높이 백–시우 최소 {minChest:F2}m(0.35 미만 {through}프레임) · 카메라–머리 선과 백 최소 {minCover:F2}m(0.25 미만 {cover}) · 타격 별–얼굴 최소 {minFx:F2}(0.15 미만 {fxFace}) · 백 아래 끝 동쪽 최대 x {maxWall:F2}(벽 19.95 넘음 {wall})");
            log.Add("화면 밖·타이머 판 아래 아님: " + string.Join(" · ", off.Select(kv => $"{kv.Key} {kv.Value}")) + (offEx != "" ? $" (예: {offEx})" : ""));
            Debug.Log("[Q] Q71b D05 샌드백·카메라\n" + string.Join("\n", log));
            if (frames < 60) bad.Add($"라운드 프레임 {frames}(시험 전제)");
            if (hits < 5) bad.Add($"타격 {hits}(시험 전제)");
            if (through > 0) bad.Add($"백이 시우 몸을 뚫음 {through}프레임(최소 {minChest:F2}m)");
            if (cover > 0) bad.Add($"백이 카메라–시우 머리 사이 {cover}프레임");
            if (fxFace > 0) bad.Add($"타격 별이 시우 얼굴 위 {fxFace}번");
            if (wall > 0) bad.Add($"백이 동쪽 벽을 뚫음 {wall}프레임(x {maxWall:F2})");
            foreach (var kv in off) if (kv.Value > 0) bad.Add($"{kv.Key} 시우 머리·가슴이 화면 밖·타이머 판 아래 아님 {kv.Value}프레임");
            Assert.IsEmpty(bad, string.Join(" | ", bad));
        }

        // ───────────────────────── Q68(G3 — 2026-11-05): 맵 끝 시험 점(QA B22 · QaBot.EdgeStarts)이 모두 걸을 땅에서 시작하고, 경계 쪽으로 3.5초 달려도
        //   QA 감시와 같은 가운데 광선(발 +0.6m 에서 아래로 3m)이 0.5초 넘게 땅을 놓치지 않음. 예전 동쪽 점(−133, 120)은 Layers.Solid 광선이
        //   '골목 끝 다세대 5' 지붕 위 3m 보이지 않는 막(EdgeGuard_Qa2) 윗면에 맞아 시우가 허공(막과 경계벽 사이 0.5m 틈 모서리)에 걸렸음('발밑에 땅 없음' 한 바퀴 약 300프레임)
        [UnityTest, Timeout(900000)]
        public IEnumerator Q68_Map_Edge_Starts_On_Ground()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            yield return Start(w, Mid(w.Def, 311, 14 * 60, SceneLoader.Zone1));
            var me = M3Lab.Motor;
            var log = new List<string>(); var bad = new List<string>();
            bool oldHit = Physics.Raycast(new Vector3(-133f, 80f, 120f), Vector3.down, out var oh, 90f, Layers.Solid, QueryTriggerInteraction.Ignore);
            string oldParent = oldHit && oh.collider.transform.parent != null ? oh.collider.transform.parent.name : "-";
            log.Add($"예전 동쪽 점 (−133, 120) — Layers.Solid 첫 맞음 {(oldHit ? $"{oh.collider.name}(층 {oh.collider.gameObject.layer}) · 부모 {oldParent} · y {oh.point.y:F2}" : "없음")}");
            foreach (var (p0, dir, n) in QaBot.EdgeStarts)
            {
                if (!QaBot.EdgeStart(p0, out var feet, out var col)) { bad.Add($"{n}: 시작점이 걸을 땅이 아님 — {(col != null ? $"{col.name}(층 {col.gameObject.layer})" : "없음")}"); continue; }
                me.Teleport(feet + Vector3.up * 0.05f, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg);
                yield return Lab.Frames(10);
                float miss = 0f, worst = 0f, t = 0f;
                while (t < 3.5f)
                {
                    me.SetMoveInput(dir, 1f, true);
                    yield return null;
                    t += Time.deltaTime;
                    var p = me.Position;
                    if (!Physics.Raycast(p + Vector3.up * 0.6f, Vector3.down, out _, 3.0f, Layers.Solid, QueryTriggerInteraction.Ignore)) { miss += Time.deltaTime; worst = Mathf.Max(worst, miss); }
                    else miss = 0f;
                }
                me.ClearMoveInput();
                yield return Lab.Frames(5);
                var q = me.Position;
                bool inside = q.x >= -275f && q.x <= -129f && q.z >= 69f && q.z <= 175f;
                log.Add($"{n}: 시작 {feet:F1}({col.name}) → 끝 {q:F1} · 발밑 땅 없음 최장 {worst:F2}초 · 경계 안 {inside} · 접지 {me.Grounded}");
                if (worst >= 0.5f) bad.Add($"{n}: 발밑 땅 없음 {worst:F2}초");
                if (!inside) bad.Add($"{n}: 경계 밖 {q:F1}");
                if (q.y < feet.y - 6f) bad.Add($"{n}: 아래로 떨어짐 {feet.y:F1} → {q.y:F1}");
            }
            Debug.Log("[Q] Q68 맵 끝 시작점\n" + string.Join("\n", log));
            Assert.IsTrue(oldHit && oldParent == "EdgeGuard_Qa2", "회귀 근거: 예전 동쪽 점은 보이지 않는 막(EdgeGuard_Qa2) 윗면에 맞았어야 — 맵·막이 바뀌었으면 이 줄을 고칠 것");
            Assert.IsEmpty(bad, string.Join(" | ", bad));
        }

        // ───────────────────────── Q69(G4 — 2026-11-05): '같은 상태 25초' 감시 서명(QaBot.StorySig)은 대사가 한 줄 넘어갈 때마다 바뀜.
        //   예전 서명(스토리보드·자막만)은 C1-11 장면 4-5 의 자막 없는 구간(속마음 6줄·참기·화면 글자 '탁.' — 봇 읽기 36.7초)에 안 바뀌어 QA C07 이 '진행 불가?'로 잡았음(오탐 — 장면은 정상으로 끝남)
        [UnityTest, Timeout(900000)]
        public IEnumerator Q69_Stuck_Sig_Changes_During_Monologue()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            yield return Start(w, Mid(w.Def, 316, 8 * 60 + 5, "School", m => { m.G.Fatigue = 0; m.L.Apply("envelope", "+40000"); }));
            var story = w.Story;
            story.Bot = StoryRunner.BotMode.Read;
            bool done = false;
            IEnumerator Wrap() { yield return w.RunCardBusy(w.Def.Card("C1-11"), true); done = true; }
            w.StartCoroutine(Wrap());
            string sNew = null, sOld = null; float tNew = 0f, tOld = 0f, maxNew = 0f, maxOld = 0f; string atNew = "", atOld = "", fromOld = "";
            int lines = 0; string lastLine = "";
            float t0 = Time.realtimeSinceStartup;
            while (!done && Time.realtimeSinceStartup - t0 < 600f)
            {
                yield return null;
                float dt = UiKit.RealDt;                        // 대사 시계(봇 읽기 = 글자 수 ÷ 8 + 0.8초)
                var cur = story.Talk != null ? story.Talk.Current : null;
                if (cur != null && cur.Id != lastLine) { lastLine = cur.Id; lines++; }
                string n = $"{w.Busy}|{QaBot.StorySig(story)}|{(int)GameClock.Minutes}";
                string o = $"{w.Busy}|{(story.Board != null && story.Board.Shown ? story.Board.Title : "")}|{(story.Talk != null && story.Talk.Sub != null && story.Talk.Sub.Visible ? story.Talk.Sub.Text : "")}|{(int)GameClock.Minutes}";
                if (n == sNew) tNew += dt; else { sNew = n; tNew = 0f; }
                if (o == sOld) tOld += dt; else { sOld = o; tOld = 0f; fromOld = cur != null ? cur.Id : ""; }
                if (tNew > maxNew) { maxNew = tNew; atNew = cur != null ? cur.Id : ""; }
                if (tOld > maxOld) { maxOld = tOld; atOld = $"{fromOld}~{(cur != null ? cur.Id : "")}"; }
            }
            Debug.Log($"[Q] Q69 감시 서명: 줄 {lines} · 새 서명 가장 긴 같은 상태 {maxNew:F1}초({atNew}) · 옛 서명(스토리보드·자막만) {maxOld:F1}초({atOld}) — 감시 문턱 25초");
            Assert.IsTrue(done, "C1-11 안 끝남");
            Assert.Greater(lines, 20, "대사 줄을 못 셈(시험 전제)");
            Assert.Less(maxNew, 12f, $"새 서명이 {maxNew:F1}초 동안 안 바뀜({atNew}) — 한 줄 최장 7.7초(글자 수 ÷ 8 + 0.8)");
        }

        // ───────────────────────── Q72(G3 함께 — 2026-11-05): 장소를 옮기는 동안(옛 장소를 내리고 새 장소를 읽는 사이) 시우가 바닥 없는 자리에서 떨어지지 않음.
        //   예전엔 무대 → 무대(N01 국밥집 → 체육관)에서 KillY(−10) 아래로 떨어져 '맵 밖으로 떨어짐' 경고·리스폰(QA t3 356.6초 · run6 — 먹 닦기에 가려 화면엔 안 보임)
        [UnityTest, Timeout(600000)]
        public IEnumerator Q72_Place_Change_No_Fall()
        {
            yield return M3Lab.OpenZone1();
            var r = M3Lab.Runner;
            var me = M3Lab.Motor;
            var log = new List<string>(); var bad = new List<string>();
            int warns = 0;
            void OnLog(string msg, string st, LogType type) { if (msg.Contains("맵 밖으로 떨어짐")) warns++; }
            bool tele = false;
            void OnTele(Vector3 d) { tele = true; }
            Application.logMessageReceived += OnLog;
            me.Teleported += OnTele;
            try
            {
                yield return r.Loader.Go("Gukbap", "Door", false, Vector3.zero, 0f, false);
                yield return Lab.Frames(20);
                foreach (var (to, sp) in new[] { (SceneLoader.Gym, "Spawn"), ("Home", "Spawn"), ("Conv", "Door"), (SceneLoader.Zone1, "") })
                {
                    string from = r.Loader.Place;
                    float y0 = me.Position.y, minY = y0; int resp0 = me.Respawns, w0 = warns; int frames = 0;
                    tele = false;
                    bool zone = to == SceneLoader.Zone1;
                    var zp = new Vector3(-197.5f, 30.7f, 97.4f);
                    r.StartCoroutine(r.Loader.Go(to, zone ? null : sp, zone, zp, 0f, false));
                    yield return null;
                    while (r.Loader.Busy && frames < 3000) { if (!tele) minY = Mathf.Min(minY, me.Position.y); frames++; yield return null; }
                    yield return Lab.Frames(10);
                    float drop = y0 - minY;
                    // 옮긴 뒤 다시 걸을 수 있음(붙들기가 풀림) — 네 방향 중 가장 멀리 간 거리
                    float walked = 0f;
                    foreach (var d in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
                    {
                        var a = me.Position;
                        for (int i = 0; i < 20; i++) { me.SetMoveInput(d, 1f, false); yield return null; }
                        me.ClearMoveInput();
                        walked = Mathf.Max(walked, HitResolver.Flat(me.Position - a).magnitude);
                    }
                    log.Add($"{from} → {to}: 옮기는 동안 {frames}프레임 · 순간 이동 전 최대 낙하 {drop:F2}m · 리스폰 +{me.Respawns - resp0} · 경고 +{warns - w0} · 끝 {me.Position:F1} 접지 {me.Grounded} · 뒤에 걸음 {walked:F2}m");
                    if (drop > 0.3f) bad.Add($"{from} → {to}: 옮기는 동안 {drop:F2}m 떨어짐");
                    if (me.Respawns != resp0) bad.Add($"{from} → {to}: 리스폰 {me.Respawns - resp0}");
                    if (warns != w0) bad.Add($"{from} → {to}: '맵 밖으로 떨어짐' 경고 {warns - w0}");
                    if (walked < 0.1f) bad.Add($"{from} → {to}: 옮긴 뒤 못 걸음(붙들림?) {walked:F2}m");
                }
            }
            finally { Application.logMessageReceived -= OnLog; me.Teleported -= OnTele; }
            Debug.Log("[Q] Q72 장소 옮기는 동안 낙하\n" + string.Join("\n", log));
            Assert.IsEmpty(bad, string.Join(" | ", bad));
        }

        // ───────────────────────── Q57 글자·배경(2026-10-31 대표 "글자와 배경이 어지럽게 겹친다"): 대표 화면마다 판 ≥ 85% · 대비 · 글자끼리 겹침 0 · 화면 밖 0 · 공중/누운 월드 글자 0
        //   화면은 사실 화면 대신 UI 뺀 카메라 그림으로 배경을 잼. 나머지 화면(알바·전투·자전거·일시정지…)은 QA 봇 실제 화면(text_bad.tsv) — docs/15 8-12
        [UnityTest, Timeout(1800000)]
        public IEnumerator Q57_Text_Audit()
        {
            if (!Shots.Graphics) { Assert.Ignore("그래픽 장치 없음(-nographics)"); yield break; }
            WorldRunner w = null;
            yield return Open(x => w = x);
            var bad = new List<string>(); var log = new List<string>();
            IEnumerator Check(string screen)
            {
                yield return null; yield return null; yield return null;
                var tex = Shots.Grab(Camera.main, Screen.width, Screen.height, 1, false);
                var items = TextAudit.Ui(tex, false);
                foreach (var pr in TextAudit.Problems(items))
                {
                    bad.Add(screen + ": " + pr);
                }
                foreach (var wi in TextAudit.World(Camera.main)) if (wi.Floating || wi.Lying) bad.Add($"{screen}: 월드 글자 {(wi.Floating ? "공중" : "누움")} {wi.Path} '{wi.Text}'");
                log.Add($"{screen}: 글자 {items.Count}");
                Object.Destroy(tex);
            }
            Assert.Less(CombatFx.WordOrder, 12, "의성어는 드릴 화면·결과 카드 아래");
            log.Add($"화면 {Screen.width}×{Screen.height}");
            yield return Start(w, Mid(w.Def, 312, 14 * 60, SceneLoader.Zone1, m => { m.G.Fatigue = 0; }));
            yield return Lab.Frames(30);
            yield return Check("Zone1 낮");
            foreach (var place in new[] { SceneLoader.Gym, "Gukbap", "Home", "Conv", "School" })
            {
                var d = w.FindDoor(place);
                if (d == null) continue;
                M3Lab.Motor.Teleport(d.transform.position + d.transform.forward * 1.2f + Vector3.up * 0.05f, 0f);
                w.Loader.Cam?.SnapBehind();
                yield return Lab.Frames(12);
                yield return Check("Zone1 문 " + place);
            }
            w.Phone.Open();
            for (int i = 0; i < PhoneUi.Tabs.Length; i++) { w.Phone.SetTab(i); yield return Check("휴대폰 " + PhoneUi.Tabs[i]); }
            w.Phone.Close();
            yield return Lab.Frames(5);
            var sub = Object.FindAnyObjectByType<SubtitleUi>(FindObjectsInactive.Include);
            var inner = Object.FindAnyObjectByType<InnerVoiceUi>(FindObjectsInactive.Include);
            if (sub != null) { sub.Show("이윤준", "다 모였네. 얘 보러 온 거 맞지?"); yield return Check("자막"); sub.Hide(); }
            // D9(2026-11-05): 자막 크기 0.85/1.2 · 여러 줄 · 속마음+자막 동시(펼친 조작 안내가 판 위로 비켜 가는지 — B3)
            int keepSize = PlayerPrefs.GetInt(SubtitleUi.SizeKey, 1), keepHint = GameUi.HintMode;
            GameUi.HintMode = 1;
            foreach (int size in new[] { 0, 2 })
            {
                PlayerPrefs.SetInt(SubtitleUi.SizeKey, size);
                if (sub != null) { sub.Show("이윤준", "토요일 저녁, 레슨이 끝났는데 아무도 안 갔다. 사무실 앞에 관장이 다섯이었다. 다들 시우를 보러 왔다고 했다. 이윤준 관장은 웃기만 했다."); yield return Check($"자막 크기 {size} 여러 줄"); }
                if (sub != null && inner != null) { inner.Show("관장이 다섯. 다들 나를 보러 왔다고 했다."); yield return Check($"속마음+자막 크기 {size}"); inner.Hide(); }
                if (sub != null) sub.Hide();
            }
            PlayerPrefs.SetInt(SubtitleUi.SizeKey, keepSize); GameUi.HintMode = keepHint;
            yield return null;
            var story = Object.FindAnyObjectByType<StoryHud>(FindObjectsInactive.Include);
            if (story != null) { story.ShowDate(); yield return Check("이야기 날짜 판"); story.HideDate(); }
            if (inner != null) { inner.Show("토요일 저녁, 레슨이 끝났는데 아무도 안 갔다. 사무실 앞에 관장이 다섯이었다."); yield return Check("속마음"); inner.Hide(); }
            var board = w.Story.Board;
            board.ShowCard("개인레슨   ·   3월 14일(토) 17:30", "김승연(딥마카이) — 첫 레슨", "레슨", true, new[] { "출장 레슨 — 타격 기본기 · 17:30 · 50,000원", "이번 판: 스탯 ×1.8 · 판정 +6 · 피로 ×1.5" }, new[] { "김승연|관장님한테 얘기 들었어." }, "× · Enter · 마우스 왼쪽   다음", true);
            yield return Check("레슨 카드");
            board.Hide();
            // 3단계 F3·F4: 스토리보드 소개 카드의 흐린 메모(옛 #5E5957 2.6:1) — 글자별 색으로 잼
            board.ShowIntro("1막 — 행인1", new[] { "혜화동. 시우는 오늘도 배달을 나간다." }, "새 게임 · 1장 오픈월드 데모");
            yield return Check("스토리보드 소개 카드");
            board.Hide();
            // 버프 칩(F5: 머리 글자 띠 — 반투명 mark 가 선형으로 섞여 4.5:1 밑이던 것)
            w.Model.AddBuff("stamina", 10f, 120, "시험"); w.Model.AddBuff("hp", 5f, 90, "시험"); w.Model.AddBuff("cond", 3f, 60, "시험");
            yield return Lab.Frames(5);
            yield return Check("버프 칩");
            board.ShowCard("식사   ·   3월 14일(토) 12:30", "국밥", "식사", false, new[] { "돈 8,000원 · 피로 −5" }, new[] { "사장 할머니|많이 먹어." }, "× · Enter · 마우스 왼쪽   다음", false);
            yield return Check("식사 카드");
            board.Hide();
            yield return Start(w, Mid(w.Def, 312, 22 * 60 + 10, SceneLoader.Zone1, m => { m.G.Fatigue = 0; }));
            yield return Lab.Frames(30);
            yield return Check("Zone1 밤");
            foreach (var place in new[] { "Gukbap", "Home", "Conv", "School" })
            {
                yield return Start(w, Mid(w.Def, 312, 14 * 60, place, m => { m.G.Fatigue = 0; }));
                yield return Lab.Frames(25);
                yield return Check("실내 " + place);
            }
            GymManager gm = null;
            yield return Gym(w, 312, 19 * 60, x => gm = x);
            yield return Check("체육관");
            var drillIds = gm.Markers.Where(x => x != null && gm.Book != null && gm.Book.Find(x.DrillId) != null && gm.Book.Find(x.DrillId).Playable).Select(x => x.DrillId).Distinct().ToList();
            log.Add("드릴 " + string.Join(",", drillIds));
            foreach (var drillId in drillIds)
            {
                // 드릴마다 새로 들어감(하루 횟수·피로 제한에 걸리지 않게)
                w.Stop();
                yield return Lab.Frames(5);
                yield return Gym(w, 312, 19 * 60, x => gm = x);
                GymState.Current.Fatigue = 0;
                if (!gm.StartDrill(drillId, 0.9f)) { bad.Add($"드릴 {drillId}: 시작 안 됨"); continue; }
                float t0 = Time.realtimeSinceStartup;
                while (gm.Runner.State != DrillRunner.Phase.Round && Time.realtimeSinceStartup - t0 < 30f) yield return null;
                float t1 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t1 < 1.5f) yield return null;
                yield return Check("드릴 " + drillId);
                gm.Runner.Abort();
                float t2 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t2 < 0.8f) yield return null;
                yield return Check("드릴 결과 " + drillId);
                for (int i = 0; i < 300 && gm.Busy; i++) { gm.CloseCard(); yield return null; }
                yield return Lab.Frames(5);
            }
            w.Stop();
            Debug.Log("[Q] Q57 글자\n" + string.Join("\n", log));
            System.IO.File.WriteAllText(System.IO.Path.Combine(Application.temporaryCachePath, "q57_bad.txt"), string.Join("\n", bad));
            Debug.Log("[Q] Q57 위반 " + bad.Count + "\n" + string.Join("\n", bad.Take(120)));
            Assert.IsEmpty(bad, string.Join("\n", bad.Take(40)));
        }

        // ───────────────────────── Q47 드릴 '팡!' 알림판이 두 사람 얼굴을 가리지 않음(2026-10-28 이윤준 캡처 중 발견 — 화면 가운데 위 +120 이 D04 드릴 카메라의 얼굴 자리)
        [UnityTest, Timeout(900000)]
        public IEnumerator Q47_Drill_Toast_Not_Over_Faces()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            GymManager gm = null;
            yield return Gym(w, 312, 19 * 60, x => gm = x);
            Assert.IsTrue(gm.StartDrill("D04", 1f), "D04 시작");
            var mitt = gm.Actors.Mitt;
            Assert.NotNull(mitt, "미트(이윤준)");
            int hurt = 0, checkedN = 0, wait = -1, last = 0;
            System.Action<HitEvent> on = e => hurt++;
            mitt.Hurt += on;
            var heads = new[] { gm.Yunjun.Anim.GetBoneTransform(HumanBodyBones.Head), M3Lab.Motor.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Head) };
            var bad = new List<string>();
            float t0 = Time.realtimeSinceStartup;
            while (checkedN < 4 && Time.realtimeSinceStartup - t0 < 120f && gm.Runner.State != DrillRunner.Phase.Result)
            {
                yield return null;
                if (hurt != last) { last = hurt; wait = 2; }
                if (wait >= 0 && wait-- == 0)
                {
                    var rt = gm.Hud.ToastRect;
                    if (rt == null || !rt.gameObject.activeInHierarchy) continue;
                    var c = new Vector3[4]; rt.GetWorldCorners(c);
                    var r = new Rect(c[0].x, c[0].y, c[2].x - c[0].x, c[2].y - c[0].y);
                    foreach (var h in heads)
                    {
                        var sp = Camera.main.WorldToScreenPoint(h.position + Vector3.up * 0.08f);
                        var face = new Rect(sp.x - Screen.height * 0.035f, sp.y - Screen.height * 0.06f, Screen.height * 0.07f, Screen.height * 0.10f);
                        if (sp.z > 0f && r.Overlaps(face)) bad.Add($"{h.root.name} 얼굴 {sp:F0} ∩ 알림판 {r}");
                    }
                    checkedN++;
                }
            }
            mitt.Hurt -= on;
            gm.Runner.Abort();
            for (int i = 0; i < 300 && gm.Busy; i++) { gm.CloseCard(); yield return null; }
            Debug.Log($"[Q] Q47 D04 '팡!' 알림판: 맞음 {hurt} · 본 장면 {checkedN} · 얼굴 겹침 {bad.Count}{(bad.Count > 0 ? "\n" + string.Join("\n", bad) : "")}");
            Assert.GreaterOrEqual(checkedN, 2, "알림판이 뜬 맞는 순간을 못 봄");
            Assert.IsEmpty(bad, "알림판이 얼굴을 가림");
        }

        // ───────────────────────── Q48 레슨 카드가 코치를 가리지 않음(2026-10-29 — 가운데 큰 카드 → 오른쪽 세로 판)
        [UnityTest, Timeout(900000)]
        public IEnumerator Q48_Lesson_Card_Leaves_Coach_Visible()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            GymManager gm = null;
            yield return Gym(w, 312, 20 * 60 + 30, x => gm = x);
            w.Model.L.SetWallet(300000);
            Assert.AreEqual("", w.BookNext("yunjun"), "이윤준 예약");
            var bk = w.Model.S.Lessons.Last();
            Assert.AreEqual(21 * 60 + 15, bk.At, "목 21:15 밤 개인 지도");
            w.BotRead = 3f;
            var mat = gm.Sock("Sock_NPC_Coach_MatEdge");
            var sp = mat.position + mat.forward * 2.2f;
            var to = mat.position - sp; to.y = 0f;
            M3Lab.Motor.Teleport(sp, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg);      // 코치를 정면으로(가장 나쁜 자리 — 화면 가운데)
            w.Loader.Cam?.SnapBehind();
            yield return Lab.Frames(10);
            GameClock.AdvanceTo(21 * 60 + 10);
            var board = w.Story.Board;
            float t0 = Time.realtimeSinceStartup;
            while (!board.Shown && Time.realtimeSinceStartup - t0 < 60f) yield return null;
            Assert.IsTrue(board.Shown, "레슨 카드가 안 뜸");
            yield return Lab.Frames(3);
            var c = new Vector3[4]; board.CardRect.GetWorldCorners(c);
            var r = new Rect(c[0].x, c[0].y, c[2].x - c[0].x, c[2].y - c[0].y);
            var head = gm.Yunjun.Anim.GetBoneTransform(HumanBodyBones.Head).position;
            var hs = Camera.main.WorldToScreenPoint(head);
            var face = new Rect(hs.x - Screen.height * 0.04f, hs.y - Screen.height * 0.06f, Screen.height * 0.08f, Screen.height * 0.12f);
            Debug.Log($"[Q] Q48 레슨 카드: 옆 판 {board.Side} · 카드 {r} · 코치 얼굴 {hs:F0} · 화면 {Screen.width}×{Screen.height} · 겹침 {r.Overlaps(face)}");
            Assert.IsTrue(board.Side, "레슨 카드가 옆 판이 아님");
            Assert.Greater(hs.z, 0f, "코치가 카메라 뒤");
            Assert.IsFalse(r.Overlaps(face), "레슨 카드가 코치 얼굴을 가림");
            for (int i = 0; i < 60 * 120 && w.LessonsRun < 1; i++) yield return null;
        }

        // ───────────────────────── Q49 백승민 관장(2026-10-29 — 실존 인물, 본인 허락): N12 「왼손잡이」 원정 스파링 → 레슨 열림 · N13 「다섯 사람」 딥마카이 크루 다섯이 한자리
        [UnityTest, Timeout(1200000)]
        public IEnumerator Q49_Seungmin_Baek_Cards_And_Lesson()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            var log = new List<string>(); var bad = new List<string>();
            foreach (var (card, day, min) in new[] { ("N12", 418, 13 * 60 + 30), ("N13", 502, 18 * 60 + 40) })
            {
                var c = w.Def.Card(card);
                Assert.NotNull(c, card);
                yield return Start(w, Mid(w.Def, day, min, SceneLoader.Gym, m => { m.G.Fatigue = 0; m.L.SetWallet(300000); if (card == "N13") new EventDirector(m).Done(w.Def.Card("N12"), 1); }));
                var gm = GymManager.Instance;
                Assert.NotNull(gm, "체육관");
                Assert.NotNull(gm.Guest("seungmin_baek"), "출장 코치 NPC(백승민)");
                var story = w.Story;
                story.Bot = StoryRunner.BotMode.Read;
                CutCamDriver.StoryBad.Clear(); int d0 = story.DlgPlayed;
                var seen = new HashSet<string>();
                bool done = false;
                IEnumerator Wrap() { yield return w.RunCardBusy(c, true); done = true; }
                w.StartCoroutine(Wrap());
                float t0 = Time.realtimeSinceStartup;
                var five = new[] { "이윤준", "백승민", "김승연", "손진수", "서진수" };
                bool laid = false;
                while (!done && Time.realtimeSinceStartup - t0 < 300f)
                {
                    foreach (var n in five) { var a = CutCtx.Actor(n); if (a != null && a.gameObject.activeInHierarchy) seen.Add(n); }
                    // 2026-10-30 대표 "균형도 안 맞고 이상하다": N13 = 시우 중심 반원(이웃 1.2m 넘게)·모두 시우를 봄·관원은 매트 밖·코치는 대기 자세(미트 자세 아님)
                    if (card == "N13" && !laid && story.Talk != null && story.Talk.Current != null && story.Talk.Current.Id == "N13.07")
                    {
                        laid = true;
                        var me = M3Lab.Motor.transform.position;
                        var ps = five.Select(n => CutCtx.Actor(n)).Where(a => a != null).ToList();
                        float gap = float.MaxValue;
                        for (int i = 0; i < ps.Count; i++) for (int j = i + 1; j < ps.Count; j++) { var d = ps[i].position - ps[j].position; d.y = 0f; gap = Mathf.Min(gap, d.magnitude); }
                        if (gap < 1.2f) bad.Add($"N13 코치 간격 {gap:F2}m < 1.2");
                        foreach (var a in ps)
                        {
                            var to = me - a.position; to.y = 0f;
                            var lp = M3Lab.Motor.transform.InverseTransformPoint(a.position);
                            if (Vector3.Angle(a.forward, to) > 20f) bad.Add($"N13 {a.name} 시우를 안 봄({Vector3.Angle(a.forward, to):F0}° · 시우 기준 앞 {lp.z:F2} 옆 {lp.x:F2})");
                            var npc = a.GetComponent<GymNpc>();
                            if (npc != null && npc.Current != "Idle") bad.Add($"N13 {a.name} 자세 {npc.Current}(대기 아님)");
                        }
                        int on = gm.Members.Count(m => m != null && m.gameObject.activeInHierarchy);
                        if (on > 0) bad.Add($"N13 관원 {on}명 남음");
                        log.Add($"N13 배치: 최소 간격 {gap:F2}m · 관원 {on}");
                    }
                    yield return null;
                }
                if (card == "N13" && !laid) bad.Add("N13.07 줄을 못 봄(배치 검사 안 됨)");
                log.Add($"{card} 「{c.Title}」: 끝 {done} · 대사 장면 {story.DlgPlayed - d0} · 보인 사람 [{string.Join(",", seen)}] · 컷 못 피함 {CutCamDriver.StoryBad.Count}");
                if (!done) bad.Add(card + " 안 끝남");
                if (story.DlgPlayed - d0 < 1) bad.Add(card + " 대사 장면 없음");
                var want = card == "N12" ? new[] { "이윤준", "백승민" } : new[] { "이윤준", "백승민", "김승연", "손진수", "서진수" };
                foreach (var n in want) if (!seen.Contains(n)) bad.Add($"{card} {n} 안 보임");
                foreach (var b in CutCamDriver.StoryBad) bad.Add($"{card} 컷 가림 {b}");
                if (card == "N12")
                {
                    Assert.IsTrue(w.Model.Unlocked("lesson.seungmin"), "N12 뒤 백승민 레슨 열림");
                    Assert.AreEqual("", w.BookNext("seungmin_baek"), "토 14:00 백승민 예약");
                }
                w.Stop();
                yield return Lab.Frames(5);
            }
            Debug.Log("[Q] Q49 백승민\n" + string.Join("\n", log));
            Assert.IsEmpty(bad, string.Join(" | ", bad));
        }

        // ───────────────────────── Q54 서 있는 자세(2026-10-31 대표 "등이 굽어 보인다"): 게임의 실제 컨트롤러로 대기 자세 재기 · 걷기↔대기 · 구경↔미트 전환이 튀지 않음
        static string StandBad(string name, Animator an, Transform root, float armMax, List<string> log)
        {
            Vector3 L(HumanBodyBones b) => root.InverseTransformPoint(an.GetBoneTransform(b).position);
            var sh = (L(HumanBodyBones.LeftUpperArm) + L(HumanBodyBones.RightUpperArm)) * 0.5f;
            var hip = (L(HumanBodyBones.LeftUpperLeg) + L(HumanBodyBones.RightUpperLeg)) * 0.5f;
            var down = (hip - sh).normalized;
            float Front(Vector3 v) => Vector3.Angle(Vector3.ProjectOnPlane(v, Vector3.forward), Vector3.ProjectOnPlane(down, Vector3.forward));
            float arm = Mathf.Max(Front(L(HumanBodyBones.LeftLowerArm) - L(HumanBodyBones.LeftUpperArm)), Front(L(HumanBodyBones.RightLowerArm) - L(HumanBodyBones.RightUpperArm)));
            float shHip = (sh.z - hip.z) * 100f, dy = Mathf.Abs(L(HumanBodyBones.LeftUpperArm).y - L(HumanBodyBones.RightUpperArm).y) * 100f;
            float head = (L(HumanBodyBones.Head).z - sh.z) * 100f, foot = Mathf.Max(L(HumanBodyBones.LeftFoot).y, L(HumanBodyBones.RightFoot).y) * 100f, hipY = hip.y;
            log.Add($"{name}: 어깨-엉덩이 {shHip:+0.0;-0.0} · 머리뼈-어깨 {head:+0.0;-0.0} · 위팔 {arm:F1}° · 어깨차 {dy:F1} · 발목 높이 {foot:F1}cm · 엉덩이 {hipY:F2}m");
            if (Mathf.Abs(shHip) > 3f) return $"{name} 어깨-엉덩이 {shHip:F1}";
            if (arm > armMax) return $"{name} 위팔 {arm:F1}°";
            if (dy > 3.8f) return $"{name} 어깨차 {dy:F1}";     // 2026-10-31 인물별 자세: 짝다리로 일부러 기움(실측 최대 깐족이 3.6, 인물별 값 = docs/15 8-11)
            if (foot > 25f || hipY < 0.45f) return $"{name} 다리 접힘(발목 {foot:F0}cm · 엉덩이 {hipY:F2}m)";
            return null;
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator Q54_Standing_Posture_And_Transitions()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            yield return Start(w, Mid(w.Def, 312, 14 * 60, SceneLoader.Gym, m => { m.G.Fatigue = 0; }));
            GymManager gm = null;
            for (int i = 0; i < 300 && gm == null; i++) { gm = GymManager.Instance; yield return null; }
            Assert.NotNull(gm, "체육관");
            yield return Lab.Frames(10);
            var log = new List<string>(); var bad = new List<string>();
            var mat = gm.Sock("Sock_NPC_Coach_MatEdge");
            var feet = mat.position + mat.forward * 2.4f;
            foreach (var (name, n) in new[] { ("이윤준", gm.Yunjun), ("김승연", gm.Guest("seungyeon")), ("손진수", gm.Guest("jinsoo_son")), ("서진수", gm.Guest("jinsoo_seo")), ("백승민", gm.Guest("seungmin_baek")), ("관원 1", gm.Members.Count > 0 ? gm.Members[0] : null), ("관원 3", gm.Members.Count > 2 ? gm.Members[2] : null) })
            {
                Assert.NotNull(n, name);
                n.gameObject.SetActive(true);
                n.Anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                n.Place(feet, mat.forward, GymNpc.Routine.Watch);
                yield return Lab.Frames(20);
                string b = StandBad(name, n.Anim, n.transform, 16f, log);
                if (b != null) bad.Add(b);
                // 구경 → 미트 → 구경: 한 프레임에 머리·엉덩이가 튀지 않음(스톡 클립 사이 블렌드)
                var head = n.Anim.GetBoneTransform(HumanBodyBones.Head); var hips = n.Anim.GetBoneTransform(HumanBodyBones.Hips);
                float worst = 0f;
                var ph = head.position; var pp = hips.position;
                n.Place(feet, mat.forward, GymNpc.Routine.Mitts);
                for (int i = 0; i < 30; i++) { yield return null; worst = Mathf.Max(worst, (head.position - ph).magnitude, (hips.position - pp).magnitude); ph = head.position; pp = hips.position; }
                n.Place(feet, mat.forward, GymNpc.Routine.Watch);
                for (int i = 0; i < 30; i++) { yield return null; worst = Mathf.Max(worst, (head.position - ph).magnitude, (hips.position - pp).magnitude); ph = head.position; pp = hips.position; }
                log.Add($"{name}: 구경↔미트 한 프레임 최대 {worst * 100f:F1}cm");
                if (worst > 0.05f) bad.Add($"{name} 구경↔미트 {worst * 100f:F1}cm 튐");
                n.gameObject.SetActive(false);
            }
            // 걷기 ↔ 대기(시우)
            {
                var me = M3Lab.Motor;
                me.Teleport(feet, 0f);
                yield return Lab.Frames(40);
                var an = me.GetComponentInChildren<Animator>();
                string b = StandBad("시우", an, an.transform, 16f, log);
                if (b != null) bad.Add(b);
                var hips = an.GetBoneTransform(HumanBodyBones.Hips);
                float worst = 0f, py = an.transform.InverseTransformPoint(hips.position).y;
                me.SetMoveInput(mat.forward, 1f, false);
                for (int i = 0; i < 60; i++) { yield return null; float y = an.transform.InverseTransformPoint(hips.position).y; worst = Mathf.Max(worst, Mathf.Abs(y - py)); py = y; }
                me.ClearMoveInput();
                for (int i = 0; i < 60; i++) { yield return null; float y = an.transform.InverseTransformPoint(hips.position).y; worst = Mathf.Max(worst, Mathf.Abs(y - py)); py = y; }
                log.Add($"시우: 걷기↔대기 엉덩이 높이 한 프레임 최대 {worst * 100f:F1}cm");
                if (worst > 0.03f) bad.Add($"시우 걷기↔대기 {worst * 100f:F1}cm 튐");
            }
            Debug.Log("[Q] Q54 서 있는 자세\n" + string.Join("\n", log));
            Assert.IsEmpty(bad, string.Join(" | ", bad));
            w.Stop();
            yield return Lab.Frames(5);
        }

        // ───────────────────────── Q51 체육관 캐릭터 채움(2026-10-30 대표 "얼굴이 어둡고 납작"): 체육관 안 시우·코치에만, 나가면 시우 원래대로
        [UnityTest, Timeout(300000)]
        public IEnumerator Q51_Gym_Char_Fill_Only_In_Gym()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            yield return Start(w, Mid(w.Def, 312, 14 * 60, SceneLoader.Gym, m => { m.G.Fatigue = 0; }));
            GymManager gm = null;
            for (int i = 0; i < 300 && gm == null; i++) { gm = GymManager.Instance; yield return null; }
            Assert.NotNull(gm, "체육관");
            yield return Lab.Frames(10);
            var mpb = new MaterialPropertyBlock();
            var id = Shader.PropertyToID("_Unlit_Intensity");
            var me = M3Lab.Motor.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.sharedMaterial != null && r.sharedMaterial.HasProperty(id)).ToList();
            Assert.IsNotEmpty(me, "시우 몸");
            var coach = gm.Guest("seungyeon").GetComponentsInChildren<SkinnedMeshRenderer>(true).ToList();
            Assert.IsNotEmpty(coach, "코치 몸");
            foreach (var r in me.Concat(coach)) { r.GetPropertyBlock(mpb); Assert.AreEqual(gm.Lights.CharFill, mpb.GetFloat(id), 0.001f, r.name + " 채움"); }
            yield return w.Loader.Go(SceneLoader.Zone1, "", true, w.Def.Balance.Point("gukbap_door").Pos + new Vector3(1.5f, 0f, -1.5f), 90f);
            yield return Lab.Frames(10);
            foreach (var r in me)
            {
                if (r == null) continue;
                r.GetPropertyBlock(mpb);
                if (!mpb.isEmpty) Assert.AreEqual(r.sharedMaterial.GetFloat(id), mpb.GetFloat(id), 0.001f, r.name + " 밖에서 원래대로");
            }
            w.Stop();
            yield return Lab.Frames(5);
        }

        // ───────────────────────── Q50 출장 코치 셋 고유 장면(2026-10-29 대표 "김승연 손진수 서진수도 넣어야지"): 1장 N14 김승연 「열에 셋」 · N15 손진수 「웃으면서」 · N16 서진수 「이름값」
        //   (2·3장 C2-K1·C2-S1·C3-J1 은 데이터·대사만 — 데모 밖) · 그 코치가 화면에 서고 · 대사 장면 · 컷 가림 0 · 2·3장 카드와 대사가 있는지
        [UnityTest, Timeout(1200000)]
        public IEnumerator Q50_Guest_Coach_Own_Scenes()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            var log = new List<string>(); var bad = new List<string>();
            foreach (var (card, who, day, min) in new[] { ("N14", "김승연", 506, 16 * 60 + 30), ("N15", "손진수", 329, 10 * 60 + 40), ("N16", "서진수", 405, 18 * 60 + 40) })
            {
                var c = w.Def.Card(card);
                Assert.NotNull(c, card);
                yield return Start(w, Mid(w.Def, day, min, SceneLoader.Gym, m => m.G.Fatigue = 0));
                var story = w.Story; story.Bot = StoryRunner.BotMode.Read;
                CutCamDriver.StoryBad.Clear(); int d0 = story.DlgPlayed;
                bool done = false, seen = false;
                IEnumerator Wrap() { yield return w.RunCardBusy(c, true); done = true; }
                w.StartCoroutine(Wrap());
                float t0 = Time.realtimeSinceStartup;
                while (!done && Time.realtimeSinceStartup - t0 < 300f) { var a = CutCtx.Actor(who); if (a != null && a.gameObject.activeInHierarchy) seen = true; yield return null; }
                log.Add($"{card} 「{c.Title}」 {who}: 끝 {done} · 대사 장면 {story.DlgPlayed - d0} · 보임 {seen} · 컷 못 피함 {CutCamDriver.StoryBad.Count}");
                if (!done) bad.Add(card + " 안 끝남");
                if (story.DlgPlayed - d0 < 1) bad.Add(card + " 대사 장면 없음");
                if (!seen) bad.Add($"{card} {who} 안 보임");
                foreach (var b in CutCamDriver.StoryBad) bad.Add($"{card} 컷 가림 {b}");
                w.Stop();
                yield return Lab.Frames(5);
            }
            foreach (var (card, ch) in new[] { ("C2-K1", "2"), ("C2-S1", "2"), ("C3-J1", "3") })
            {
                var c = w.Def.Card(card);
                if (c == null) { bad.Add(card + " 카드 없음"); continue; }
                if (!w.Story.Dialogue.Has(card)) bad.Add(card + " 대사 없음");
                log.Add($"{card} 「{c.Title}」 {ch}장 · 대사 {w.Story.Dialogue.Get(card).Count()}줄");
            }
            Debug.Log("[Q] Q50 출장 코치 고유 장면\n" + string.Join("\n", log));
            Assert.IsEmpty(bad, string.Join(" | ", bad));
        }

        // ───────────────────────── Q46 룩데브 소결정 ①②③ — 벽돌·잎 카드 색 고정 · 밤 빛 웅덩이 지름 3~4m · 잎 카드 안쪽 먹선
        [UnityTest, Timeout(600000)]
        public IEnumerator Q46_Lookdev_Palette_Pools_LeafInk()
        {
#if UNITY_EDITOR
            foreach (var n in new[] { "M_Brick_Brown", "M_Brick_Red" })
            {
                var m = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"Assets/_Project/Materials/Env/{n}.mat");
                Assert.NotNull(m, n);
                Assert.Greater(m.GetColor("_BaseColor").grayscale, 1.05f, $"{n} 바탕색 밝힘 없음(팔레트 고정)");
            }
            var crown = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Env/Props/M_V_crown_zelkova_tree.mat");
            if (crown != null) { var c = crown.GetColor("_BaseColor"); Assert.Greater(c.r, c.b + 0.08f, "잎 카드 따뜻하게 밝힘 없음"); }
            // 잎 카드 안쪽 먹선: 불투명 칸 안 짙은 갈색 화소
            var tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/Env/Textures/Clump_broad.png");
            Assert.NotNull(tex);
            var path = System.IO.Path.GetFullPath(UnityEditor.AssetDatabase.GetAssetPath(tex));
            var t2 = new Texture2D(2, 2); t2.LoadImage(System.IO.File.ReadAllBytes(path));
            var px = t2.GetPixels32(); int opaque = 0, ink = 0;
            for (int i = 0; i < px.Length; i += 7) { if (px[i].a < 200) continue; opaque++; if (px[i].r < 75 && px[i].g < 70 && px[i].b < 60 && px[i].r >= px[i].b) ink++; }
            float inkRatio = ink / (float)Mathf.Max(1, opaque);
            Object.Destroy(t2);
#else
            float inkRatio = 1f;
#endif
            // 빛 웅덩이: Pools6 메시 — 웅덩이 하나 = 9×9 정점, 가로·세로 4m 안
            yield return M3Lab.OpenZone1();
            var pools = GameObject.Find("Pools6");
            Assert.NotNull(pools, "Pools6 없음");
            int n6 = 0, big = 0; float maxSpan = 0f;
            foreach (var mf in pools.GetComponentsInChildren<MeshFilter>(true))
            {
                var v = mf.sharedMesh.vertices;
                for (int p = 0; p + 81 <= v.Length; p += 81)
                {
                    var b = new Bounds(mf.transform.TransformPoint(v[p]), Vector3.zero);
                    for (int k = 1; k < 81; k++) b.Encapsulate(mf.transform.TransformPoint(v[p + k]));
                    float span = Mathf.Max(b.size.x, b.size.z);
                    maxSpan = Mathf.Max(maxSpan, span); n6++;
                    if (span > 4.6f) big++;
                }
            }
            Debug.Log($"[Q] Q46 룩데브: 잎 먹선 화소 {inkRatio:P1} · 웅덩이 {n6}개 · 가장 큰 폭 {maxSpan:F2}m · 4.6m 넘음 {big}");
            Assert.Greater(n6, 10, "웅덩이가 거의 없음");
            Assert.AreEqual(0, big, $"지름 3~4m 넘는 웅덩이 {big}개(최대 {maxSpan:F2}m)");
            Assert.Greater(inkRatio, 0.012f, "잎 카드 안쪽 먹선이 없음");
        }

        // ───────────────────────── S01 카드 실제 장면 — 대사가 부르는 배우·자리·카메라 대상이 그 장소에 모두 있음
        static readonly Dictionary<string, string> ScenePlace = new Dictionary<string, string>
        {
            ["2-2"] = "Gukbap", ["2-3"] = "Gukbap", ["2-4"] = "Gukbap", ["7-3"] = "Gukbap", ["C1-26"] = "Gukbap",
            ["3-4"] = "Home", ["4-4"] = "Home", ["C1-18"] = "Home",
            ["3-5"] = "Site", ["4-1"] = "Site", ["4-2"] = "Site", ["4-3"] = "Site",
            ["4-5"] = "School", ["4-6"] = "School", ["7-4"] = "School", ["C1-19"] = "School", ["C1-19b"] = "School",
            ["6-1"] = "Zone1", ["6-2"] = "Zone1", ["6-4"] = "Zone1", ["7-2"] = "Zone1", ["N01"] = "Gym", ["C1-24"] = "Zone1",
        };
        static readonly HashSet<string> Skip = new HashSet<string> { "시우", "반", "해제", "" };
        static readonly HashSet<string> EnemyLabels = new HashSet<string> { "깐족이", "석 달", "냉장고", "스크럼" };

        static IEnumerable<string> NamesIn(DlgLine l)
        {
            if (l.Mode == DlgMode.Event)
            {
                var p = l.Text.Split(':');
                switch (p[0])
                {
                    case "cast": case "place": case "look": case "face": case "raise": case "front":
                        if (p.Length > 1) yield return p[1];
                        if (p.Length > 2 && p[0] != "front" || p[0] == "front" && p.Length > 2) yield return p[2];
                        break;
                    case "pose": case "hand": case "uncast":
                        if (p.Length > 1) yield return p[1];
                        break;
                }
            }
            var cp = (l.Cam ?? "").Split(':');
            if (cp.Length > 1) yield return cp[1].Trim();
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator S01_Card_Scene_Actors_Resolve()
        {
            yield return M3Lab.OpenZone1();
            var book = M3Lab.Runner.Dialogue;
            Assert.NotNull(book);
            var miss = new List<string>(); int names = 0, lines = 0;
            var byPlace = ScenePlace.GroupBy(kv => kv.Value);
            foreach (var g in byPlace)
            {
                UnityEngine.SceneManagement.Scene sc = default;
                if (g.Key != "Zone1")
                {
                    var path = SceneLoader.StagePath(g.Key);
                    var op = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(path, UnityEngine.SceneManagement.LoadSceneMode.Additive);
                    Assert.NotNull(op, path);
                    while (!op.isDone) yield return null;
                    sc = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
                    yield return null;
                }
                foreach (var kv in g)
                {
                    Assert.IsTrue(book.Has(kv.Key), $"대사 장면 {kv.Key} 없음(ep*.tsv)");
                    foreach (var l in book.Get(kv.Key))
                    {
                        lines++;
                        foreach (var n in NamesIn(l).Distinct())
                        {
                            if (Skip.Contains(n) || float.TryParse(n, out _)) continue;
                            if (l.Mode == DlgMode.Event && (l.Text.StartsWith("pose:") || l.Text.StartsWith("hand:")) && n != l.Text.Split(':')[1]) continue;
                            names++;
                            if (kv.Key == "C1-24" && EnemyLabels.Contains(n)) continue;     // 시비 상대 = 인카운터가 켜질 때(M2 무대)
                            if (CutCtx.Actor(n) == null) miss.Add($"{kv.Key} {l.Id}: '{n}' ({g.Key})");
                        }
                    }
                }
                if (sc.IsValid()) yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(sc);
            }
            Debug.Log($"[Q] S01 카드 장면 {ScenePlace.Count}개 · 줄 {lines} · 배우·자리 이름 {names} · 못 찾음 {miss.Count}");
            Assert.IsEmpty(miss, string.Join(" | ", miss.Take(12)));
        }

        // ───────────────────────── S03 보스전 6-3·7-1 = 실제 전투(2026-10-29 대표 결정): 스토리보드 없이 M2 주차장 인카운터 — 6-3 은 민재(석 달) 1:1, 7-1 은 1:3, 전후 대사
        [UnityTest, Timeout(1800000)]
        public IEnumerator S03_Boss_Fights_Are_Real_Combat()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            var pk = w.Def.Balance.Point("parking").Pos;
            var log = new List<string>();
            foreach (var (card, min, pos) in new[] { ("C1-20", 17 * 60 + 25, new Vector3(-191.5f, 30.3f, 98.3f)), ("C1-21", 17 * 60 + 42, pk + new Vector3(0f, 0f, 6f)) })
            {
                var c = w.Def.Card(card);
                // 다른 하나(C1-20 ↔ C1-21)는 끝난 것으로 — 세계가 같은 시각대에 그 카드를 따로 걸어 보스전이 겹치지 않게
                yield return Start(w, Mid(w.Def, 501, min, SceneLoader.Zone1, m => { m.G.Fatigue = 0; new EventDirector(m).Done(w.Def.Card(card == "C1-20" ? "C1-21" : "C1-20"), 1); }, pos));
                var story = w.Story;
                story.Bot = StoryRunner.BotMode.Read;            // 사람처럼 읽고, 싸움은 FightBot(무적)
                int f0 = story.BossFights, sb0 = story.Board.StoryboardShows, d0 = story.DlgPlayed;
                string boss = card == "C1-20" ? "6-3" : "7-1";
                bool done = false, won = false; int fights = 0, boards = 0, talks = 0;
                // 카드가 끝난 그 순간 값을 잡음(세계가 바로 다음 카드 — C1-20 뒤 C1-21 — 를 걸 수 있어서)
                IEnumerator Wrap() { yield return w.RunCardBusy(c, true); done = true; fights = story.BossFights - f0; won = story.BossLast; boards = story.Board.StoryboardShows - sb0; talks = story.DlgPlayed - d0; }
                w.StartCoroutine(Wrap());
                EnemyBrain solo = null; string soloName = "-"; int foes = 0; bool fought = false; bool castModel = false;
                float t0 = Time.realtimeSinceStartup;
                while (!done && Time.realtimeSinceStartup - t0 < 900f)
                {
                    var enc = StoryRunner.ParkingEncounter();
                    if (enc != null && enc.State == Encounter.Phase.Fight && story.Scene != null && story.Scene.Id == boss) { fought = true; if (enc.Solo != null) { solo = enc.Solo; soloName = solo.Def.name; } foes = enc.Brains.Count(b => b != null && !b.NoAttack); castModel |= enc.Brains.Any(b => b != null && b.GetComponentsInChildren<Animator>().Any(an => an.avatar != null && an.avatar.name == "MinjaeAvatar")); }
                    yield return null;
                }
                w.Stop();
                log.Add($"{card}: 끝 {done} · 보스전 {fights} · 이김 {won} · 싸움 중 공격하는 적 {foes} · 1:1 상대 {soloName} · 민재 배역 모델 {castModel} · 스토리보드 {boards} · 대사 {talks}");
                Assert.IsTrue(done, card + " 안 끝남");
                Assert.IsTrue(fought, card + " 싸움(Fight 단계)이 없음");
                Assert.AreEqual(1, fights, card + " 보스전이 실제 전투로 안 열림");
                Assert.IsTrue(won, card + " 보스전을 못 이김");
                Assert.IsTrue(castModel, card + " 상대가 이야기 배역 모델(민재)이 아님");
                Assert.AreEqual(0, boards, card + " 스토리보드 카드가 뜸");
                if (card == "C1-20") { Assert.NotNull(solo, "6-3 = 민재 1:1"); Assert.AreEqual("Enemy_Seokdal", soloName); Assert.AreEqual(1, foes, "6-3 공격하는 적 1"); }
                else Assert.GreaterOrEqual(foes, 3, "7-1 = 1:3");
                yield return Lab.Frames(5);
            }
            Debug.Log("[Q] S03 보스전 실제 전투\n" + string.Join("\n", log));
        }

        // ───────────────────────── S04 장면 컷 가림(2026-10-29 — C1-07·C1-11·C1-19·N01 각 1컷): 사람처럼 읽기(Read)로 컷을 실제로 잡아 보며, 돌려도 좋은 각이 없는 컷 0
        //   (S02 는 빠른 봇이라 대사·컷을 건너뜀 — 카메라는 여기서 봄) · 좋은 각 = 다른 배우·소품(가는 기둥 포함)에 안 가림 · 배우가 카메라 0.75m 안에 없음 · 벽에 60% 넘게 안 당김 · 지붕·바닥 안
        [UnityTest, Timeout(1800000)]
        public IEnumerator S04_Story_Cuts_Not_Occluded()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            var runs = new (string card, int day, int min, string place)[] { ("C1-07", 313, 21 * 60 + 25, "Home"), ("C1-11", 316, 8 * 60 + 5, "School"), ("C1-19", 429, 9 * 60, "School"), ("N01", 310, 20 * 60 + 25, "Gukbap") };
            var log = new List<string>(); var bad = new List<string>();
            foreach (var r in runs)
            {
                var c = w.Def.Card(r.card);
                yield return Start(w, Mid(w.Def, r.day, r.min, r.place, m => { m.G.Fatigue = 0; m.L.Apply("envelope", "+40000"); }));
                var story = w.Story;
                story.Bot = StoryRunner.BotMode.Read;
                CutCamDriver.StoryBad.Clear(); int fix0 = CutCamDriver.StoryFixes, d0 = story.DlgPlayed;
                bool done = false;
                IEnumerator Wrap() { yield return w.RunCardBusy(c, true); done = true; }
                w.StartCoroutine(Wrap());
                float t0 = Time.realtimeSinceStartup;
                // N01 은 대사 장면 뒤 체험 드릴(몇 분) — 대사가 끝나면 거기서 멈춤
                while (!done && Time.realtimeSinceStartup - t0 < 600f && !(r.card == "N01" && story.DlgPlayed > d0 && !story.InScene)) yield return null;
                log.Add($"{r.card}: 돌려 피함 {CutCamDriver.StoryFixes - fix0} · 못 피함 {CutCamDriver.StoryBad.Count}{(CutCamDriver.StoryBad.Count > 0 ? " — " + string.Join(" / ", CutCamDriver.StoryBad) : "")}");
                foreach (var b in CutCamDriver.StoryBad) bad.Add($"{r.card} {b}");
                w.Stop();
                yield return Lab.Frames(5);
            }
            Debug.Log("[Q] S04 장면 컷 가림\n" + string.Join("\n", log));
            Assert.IsEmpty(bad, string.Join(" | ", bad));
        }

        // ───────────────────────── S05 이야기 근접 컷은 얼굴 쪽에서(G4 — 2026-11-05): C1-11 4-5.10 '근접:임찬'(속마음 — 임찬이 내 가방 지퍼를 열었다)이
        //   돌려 피하기(시우가 카메라 0.75m 안)에서 −135° 를 골라 임찬 뒤통수·사물함만 보였음. S04 의 네 카드에서 근접 컷마다 카메라가 대상 얼굴 쪽(앞 방향과 95° 안)인지
        [UnityTest, Timeout(2400000)]
        public IEnumerator S05_Story_Close_Shows_Face()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            var runs = new (string card, int day, int min, string place)[] { ("C1-07", 313, 21 * 60 + 25, "Home"), ("C1-11", 316, 8 * 60 + 5, "School"), ("C1-19", 429, 9 * 60, "School"), ("N01", 310, 20 * 60 + 25, "Gukbap") };
            var log = new List<string>(); var bad = new List<string>();
            int closeCuts = 0;
            foreach (var r in runs)
            {
                var c = w.Def.Card(r.card);
                yield return Start(w, Mid(w.Def, r.day, r.min, r.place, m => { m.G.Fatigue = 0; m.L.Apply("envelope", "+40000"); }));
                var story = w.Story;
                story.Bot = StoryRunner.BotMode.Read;
                int d0 = story.DlgPlayed;
                var pending = new List<DlgLine>();
                void OnShown(DlgLine l) { if (l != null && l.Mode != DlgMode.Event && l.Mode != DlgMode.Choice && (l.Cam ?? "").StartsWith("근접")) pending.Add(l); }
                story.Talk.Shown += OnShown;
                bool done = false;
                IEnumerator Wrap() { yield return w.RunCardBusy(c, true); done = true; }
                w.StartCoroutine(Wrap());
                float t0 = Time.realtimeSinceStartup;
                while (!done && Time.realtimeSinceStartup - t0 < 600f && !(r.card == "N01" && story.DlgPlayed > d0 && !story.InScene))
                {
                    if (pending.Count > 0)
                    {
                        var l = pending[0]; pending.RemoveAt(0);
                        yield return null; yield return null;
                        var live = Object.FindObjectsByType<CutCamDriver>(FindObjectsSortMode.None).FirstOrDefault(d => d != null && d.Cam != null && d.Cam.Priority.Value == CutCams.LivePriority && d.Preset == CamPreset.Close);
                        if (live == null || live.Subject == null) { log.Add($"{r.card} {l.Id} {l.Cam}: 근접 카메라 없음"); continue; }
                        var an = live.Subject.GetComponentInChildren<Animator>();
                        if (an == null || !an.isHuman) { log.Add($"{r.card} {l.Id} {l.Cam}: 사람 아님 {live.Subject.name}"); continue; }
                        var fwd = an.transform.forward; fwd.y = 0f;
                        var head = an.GetBoneTransform(HumanBodyBones.Head).position;
                        var to = live.transform.position - head; to.y = 0f;
                        float ang = fwd.sqrMagnitude > 1e-4f && to.sqrMagnitude > 1e-4f ? Vector3.Angle(fwd, to) : 0f;
                        float dist = Vector3.Distance(live.transform.position, head);
                        closeCuts++;
                        log.Add($"{r.card} {l.Id} {l.Cam}: 얼굴 쪽에서 {ang:F0}° · 돌림 {live.SpinDeg:F0}° · 머리까지 {dist:F2}m");
                        if (ang > 95f) bad.Add($"{r.card} {l.Id} {l.Cam} 뒤통수 {ang:F0}°(돌림 {live.SpinDeg:F0}°)");
                    }
                    yield return null;
                }
                story.Talk.Shown -= OnShown;
                w.Stop();
                yield return Lab.Frames(5);
            }
            Debug.Log($"[Q] S05 근접 컷 얼굴 쪽 — {closeCuts}컷\n" + string.Join("\n", log));
            Assert.Greater(closeCuts, 3, "근접 컷을 못 잼(시험 전제)");
            Assert.IsEmpty(bad, string.Join(" | ", bad));
        }

        // ───────────────────────── S02 핵심 카드가 스토리보드가 아니라 실제 장면으로(배우가 켜지고, 장소를 옮기면 원래대로)
        [UnityTest, Timeout(1800000)]
        public IEnumerator S02_Core_Cards_Play_As_Scenes()
        {
            WorldRunner w = null;
            yield return Open(x => w = x);
            var runs = new (string card, int day, int min, string place, int boards)[]
            {
                ("C1-03", 306, 19 * 60 + 25, "Gukbap", 0), ("C1-04", 306, 20 * 60 + 45, "Gukbap", 0), ("C1-07", 313, 21 * 60 + 25, "Home", 0),
                ("C1-08", 314, 6 * 60 + 50, "Site", 0), ("C1-09", 314, 6 * 60 + 58, "Site", 0), ("C1-11", 316, 8 * 60 + 5, "School", 0), ("C1-12", 316, 8 * 60 + 12, "School", 0),
                ("C1-18", 428, 19 * 60 + 10, "Home", 0), ("C1-19", 429, 9 * 60, "School", 0), ("C1-20", 501, 17 * 60 + 25, SceneLoader.Zone1, 0), ("C1-21", 501, 17 * 60 + 42, SceneLoader.Zone1, 0),
                ("C1-22", 504, 8 * 60 + 15, "School", 0), ("C1-26", 508, 17 * 60 + 30, "Gukbap", 0), ("N01", 310, 20 * 60 + 25, "Gukbap", 0), ("C1-24", 505, 16 * 60 + 40, SceneLoader.Zone1, 0),
            };
            var log = new List<string>(); var bad = new List<string>();
            CutCamDriver.StoryBad.Clear(); int fix0 = CutCamDriver.StoryFixes;
            foreach (var r in runs)
            {
                var c = w.Def.Card(r.card);
                Assert.NotNull(c, r.card);
                int bad0 = CutCamDriver.StoryBad.Count;
                yield return Start(w, Mid(w.Def, r.day, r.min, r.place, m => { m.G.Fatigue = 0; m.L.Apply("envelope", "+40000"); }));
                var story = w.Story;
                int sb0 = story.Board.StoryboardShows, scenes0 = w.CardScenes, dlg0 = story.DlgPlayed;
                int talked = 0, castOn = 0;
                void OnScene(SceneDef sd) { if (story.Dialogue.Has(sd.Id)) talked++; }
                story.SceneStarted += OnScene;
                bool done = false;
                IEnumerator Wrap() { yield return w.RunCardBusy(c, true); done = true; }
                w.StartCoroutine(Wrap());
                for (int i = 0; i < 60 * 600 && !done; i++)
                {
                    castOn = Mathf.Max(castOn, story.CastHeld);
                    yield return null;
                }
                story.SceneStarted -= OnScene;
                talked += story.DlgPlayed - dlg0;
                int boards = story.Board.StoryboardShows - sb0;
                log.Add($"{r.card} 「{c.Title}」: 대사 장면 {talked} · 장면 카드 {w.CardScenes - scenes0} · 스토리보드 {boards}(허용 {r.boards}) · 켠 배우 최대 {castOn} · 끝 {(done ? "O" : "X(시간 초과)")} · {w.Loader.Place}");
                if (!done) bad.Add($"{r.card} 안 끝남");
                if (talked < 1) bad.Add($"{r.card} 대사 장면 없음");
                if (boards > r.boards) bad.Add($"{r.card} 스토리보드 {boards}장(허용 {r.boards})");
                // 2026-10-29 가림 검사: 돌려도 좋은 각이 없던 컷(다른 배우·소품 가림 · 벽에 60% 넘게 당김 · 지붕 밖)
                for (int k = bad0; k < CutCamDriver.StoryBad.Count; k++) bad.Add($"{r.card} 컷 가림 {CutCamDriver.StoryBad[k]}");
                w.Stop();
                yield return Lab.Frames(5);
            }
            log.Add($"이야기 컷 돌려 피함 {CutCamDriver.StoryFixes - fix0} · 못 피함 {CutCamDriver.StoryBad.Count}");
            Debug.Log("[Q] S02 핵심 카드 실제 장면\n" + string.Join("\n", log));
            Assert.IsEmpty(bad, string.Join(" | ", bad));
        }
    }
}
