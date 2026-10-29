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
                if (mm.NapOnly) { var nr = mm.Nap(); hours = nr.Hours; }
                else { var sr = mm.Sleep(); hours = sr.Hours; }
                longest = Mathf.Max(longest, hours);
                if (hours >= 22) worst.Add($"{GameClock.Hm(min)} → {hours}시간");
            }
            Debug.Log($"[Q] Q38 낮잠·밤잠: 일어나자마자 낮잠 {n.Summary} · 하루 중 가장 긴 잠 {longest}시간 · 22시간 넘는 잠 {worst.Count}");
            Assert.IsEmpty(worst, "22시간 넘는 잠: " + string.Join(", ", worst));
            Assert.LessOrEqual(longest, 19);
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
                ("C1-18", 428, 19 * 60 + 10, "Home", 0), ("C1-19", 429, 9 * 60, "School", 0), ("C1-20", 501, 17 * 60 + 25, SceneLoader.Zone1, 2), ("C1-21", 501, 17 * 60 + 42, SceneLoader.Zone1, 1),
                ("C1-22", 504, 8 * 60 + 15, "School", 0), ("C1-26", 508, 17 * 60 + 30, "Gukbap", 0), ("N01", 310, 20 * 60 + 25, "Gukbap", 0), ("C1-24", 505, 16 * 60 + 40, SceneLoader.Zone1, 0),
            };
            var log = new List<string>(); var bad = new List<string>();
            foreach (var r in runs)
            {
                var c = w.Def.Card(r.card);
                Assert.NotNull(c, r.card);
                yield return Start(w, Mid(w.Def, r.day, r.min, r.place, m => { m.G.Fatigue = 0; m.L.Apply("envelope", "+40000"); }));
                var story = w.Story;
                int sb0 = story.Board.StoryboardShows, scenes0 = w.CardScenes, dlg0 = story.DlgPlayed;
                int talked = 0, castOn = 0;
                void OnScene(SceneDef sd) { if (story.Dialogue.Has(sd.Id)) talked++; }
                story.SceneStarted += OnScene;
                bool done = false;
                IEnumerator Wrap() { yield return w.RunCard(c, true); done = true; }
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
                w.Stop();
                yield return Lab.Frames(5);
            }
            Debug.Log("[Q] S02 핵심 카드 실제 장면\n" + string.Join("\n", log));
            Assert.IsEmpty(bad, string.Join(" | ", bad));
        }
    }
}
