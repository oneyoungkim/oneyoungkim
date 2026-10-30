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
            var cases = new (string name, float x, float z, Vector3 dir)[]
            {
                ("동쪽", -133.5f, 120f, Vector3.right), ("남쪽", -200f, 73.5f, Vector3.back), ("서쪽", -270.5f, 120f, Vector3.left),
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
            var mk = gm.Markers.First(x => x != null && x.Label != null);
            me.Teleport(mk.transform.position + mk.transform.forward * 6f + Vector3.up * 0.05f, 0f);
            yield return Lab.Frames(15);
            Assert.IsFalse(mk.StickerShown, "멀리서도 바닥 스티커가 보임");
            Assert.IsFalse(mk.LabelShown, "멀리서도 이름표가 보임");
            me.Teleport(mk.transform.position + mk.transform.right * 1.2f + Vector3.up * 0.05f, 0f);
            yield return Lab.Frames(15);
            Assert.IsTrue(mk.LabelShown && mk.StickerShown, "가까이 가도 글이 안 뜸");
            Assert.LessOrEqual(mk.Label.fontSize, DrillMarker.LabelSize + 0.01f, "이름표가 큼");
            Assert.LessOrEqual(mk.Sticker.fontSize, DrillMarker.StickerSize + 0.01f, "스티커가 큼");
            Assert.GreaterOrEqual(mk.Label.transform.localPosition.y, DrillMarker.LabelHeight - 0.01f, "이름표가 사람 몸 높이(머리 위가 아님)");
            foreach (var t in new TMP_Text[] { mk.Label, mk.Sticker })
            {
                var m = t.fontMaterial;
                Assert.AreEqual((float)UnityEngine.Rendering.CompareFunction.LessEqual, m.GetFloat("unity_GUIZTestMode"), 0.01f, $"{t.name} 깊이 검사 꺼짐(사람 앞에 그려짐)");
            }
            Debug.Log($"[Q] Q43 드릴 자리 글: 이름표 {mk.Label.fontSize} · {mk.Label.transform.localPosition.y:F2}m · 스티커 {mk.Sticker.fontSize} · 멀리 숨김·가까이 보임 · ZTest LessEqual");
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
            if (dy > 2.5f) return $"{name} 어깨차 {dy:F1}";     // 2026-10-31 인물별 자세: 시우·민재·깐족이·서진수는 일부러 기움(2° 안팎)
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
                string b = StandBad(name, n.Anim, n.transform, 12.5f, log);
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
                string b = StandBad("시우", an, an.transform, 12.5f, log);
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
