// 행인1의 메인이벤트 — 1장 오픈월드 W16 성능 + 녹화·사진(docs/13 12-4 12단계). 그래픽 켠 배치에서만(-nographics 빼기).
//   Z_W16_Perf   : 늘(그래픽이면) — 밤 상가거리 오픈월드(HUD·시계·길잡이·표시 자리) 1080p 렌더 한 번 95% ≤ 12ms + 실내(손님·김) 95% ≤ 8ms → Logs/perf_world.csv
//   Z_World_Shots: -worldshots <폴더> 를 줄 때만 — 녹화(JPG 프레임 → 밖에서 ffmpeg 무음 mp4) a_day(하루 루프 1일) · b_jobs(노가다·계산대) ·
//                  c_cards(유통기한 한 시간 전·컵라면 말고 국밥 + 체험·니가 행인1이냐 시비) · d_goal(어버이날 → 데모 끝 판정 → 2장 예고) + 사진(실내 2차·HUD·휴대폰·주간 계획)
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Haengin.Tests
{
    public sealed class ZWorldShots
    {
        string dir;
        readonly List<string> notes = new List<string>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var r in Object.FindObjectsByType<Rec>(FindObjectsSortMode.None)) Object.Destroy(r.gameObject);
            var w = Object.FindAnyObjectByType<WorldRunner>();
            if (w != null) { var b = w.GetComponent<WorldBot>(); if (b != null) Object.Destroy(b); w.Stop(); }
            WorldRunner.BotPreset = null;
            GymManager.Gate = null; StageDoor.Gate = null;
            yield return M3Lab.TearDown();
        }

        /// 화면 UI 를 깨끗하게: 장면(렌더러 기능 켬 — 화면 공간 외곽선·역광) + UI 는 기능을 끈 채 따로 찍어, UI 가 있는 화소만 갈아 끼움.
        /// (오버레이 캔버스를 카메라 공간으로 돌려 찍으면 화면 공간 외곽선이 UI 위로 번진다 — 실제 게임(오버레이)에는 없는 녹화 흠)
        public static Texture2D CleanGrab(Camera c, int w, int h)
        {
            var s = Shots.Grab(c, w, h, 1, false);
            var feats = Features();
            var was = feats.Select(f => f.isActive).ToList();
            foreach (var f in feats) f.SetActive(false);
            Texture2D s0, u;
            try { s0 = Shots.Grab(c, w, h, 1, false); u = Shots.Grab(c, w, h, 1, true); }
            finally { for (int i = 0; i < feats.Count; i++) feats[i].SetActive(was[i]); }
            var ps = s.GetPixels32(); var p0 = s0.GetPixels32(); var pu = u.GetPixels32();
            for (int i = 0; i < ps.Length; i++)
            {
                int d = Math.Abs(pu[i].r - p0[i].r) + Math.Abs(pu[i].g - p0[i].g) + Math.Abs(pu[i].b - p0[i].b);
                if (d > 10) ps[i] = pu[i];
            }
            s.SetPixels32(ps); s.Apply();
            Object.Destroy(s0); Object.Destroy(u);
            return s;
        }

        static List<UnityEngine.Rendering.Universal.ScriptableRendererFeature> feats;
        static List<UnityEngine.Rendering.Universal.ScriptableRendererFeature> Features()
        {
            if (feats != null) return feats;
            feats = new List<UnityEngine.Rendering.Universal.ScriptableRendererFeature>();
#if UNITY_EDITOR
            foreach (var path in new[] { "Assets/Settings/PC_Renderer.asset", "Assets/Settings/Mobile_Renderer.asset" })
            {
                var rd = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.ScriptableRendererData>(path);
                if (rd != null) feats.AddRange(rd.rendererFeatures.Where(f => f != null));
            }
#endif
            return feats;
        }

        static void SavePng(Camera c, string path, int w, int h) { var t = CleanGrab(c, w, h); File.WriteAllBytes(path, t.EncodeToPNG()); Object.Destroy(t); }

        /// 프레임 JPG 기록(LateUpdate — Every 프레임마다 한 장)
        sealed class Rec : MonoBehaviour
        {
            public string Dir; public int Every = 3, W = 1280, H = 720;
            public int Frames, Saved; public bool On = true;
            void LateUpdate()
            {
                if (!On) return;
                var c = Camera.main;
                if (c == null || !c.isActiveAndEnabled) return;
                if (Frames++ % Every != 0) return;
                var t = CleanGrab(c, W, H);
                File.WriteAllBytes(Path.Combine(Dir, $"f_{Saved++:00000}.jpg"), t.EncodeToJPG(86));
                Object.Destroy(t);
            }
        }

        Rec StartRec(string sub, int every)
        {
            var d = Path.Combine(dir, sub);
            if (Directory.Exists(d)) foreach (var f in Directory.GetFiles(d, "f_*.jpg")) File.Delete(f);
            Directory.CreateDirectory(d);
            var go = new GameObject("녹화 " + sub);
            var r = go.AddComponent<Rec>(); r.Dir = d; r.Every = every;
            return r;
        }

        void StopRec(Rec r, string label)
        {
            r.On = false;
            Note($"{label}: {r.Saved}장(매 {r.Every}프레임 — 60fps 기준 {60f / r.Every:F0}fps · {r.Saved * r.Every / 60f:F1}초)");
            Object.Destroy(r.gameObject);
        }

        void Note(string s) { notes.Add(s); Debug.Log("[WShots] " + s); }

        static IEnumerator Open(Action<WorldRunner> got)
        {
            yield return M3Lab.OpenZone1();
            var w = Object.FindAnyObjectByType<WorldRunner>();
            Assert.NotNull(w, "WorldRunner 없음 — WorldSetup.Build");
            w.BotSkill = 0.9f; w.BotRead = 1.2f; w.BotRide = false; w.BotDelegate = false; w.AutoSaveOn = false;
            w.BotChoice = c => c.Id == "N02" ? 1 : 0;
            got(w);
        }

        static IEnumerator Free(WorldRunner w, int frames = 60 * 900) { for (int i = 0; i < frames && !w.Free; i++) yield return null; }

        static SaveData Mid(WorldDef def, IEnumerable<string> done, int day, int minutes, string place, Vector3? pos = null, Action<WorldModel> tweak = null)
        {
            var m = M5WorldTests.Fresh(def);
            var d = new EventDirector(m);
            foreach (var id in done) { var c = def.Card(id); d.ApplyAfter(c, c.Choice.Length > 1 ? c.Choice[1] : null); d.Done(c, 1); }
            m.Host.Clear();
            GameClock.SetDate(2026, day / 100, day % 100, minutes);
            tweak?.Invoke(m);
            m.S.Active = true; m.S.Place = place; m.S.Spawn = place == "Home" ? "Spawn" : "Door"; m.S.HasPos = false; m.S.Card = "";
            if (place == SceneLoader.Zone1) { m.S.HasPos = true; m.S.Pos = pos ?? def.Balance.Point("gukbap_door").Pos + new Vector3(1.5f, 0f, -1.5f); m.S.Yaw = 90f; }
            return new SaveData { SceneId = "world", Month = day / 100, Day = day % 100, Minutes = minutes, Ledger = m.L, Flags = m.F, Gym = m.G, Names = m.N, World = m.S };
        }

        static readonly string[] Early = { "C1-01", "C1-02", "C1-03", "C1-04" };

        // ───────────────────────── W16 성능
        [UnityTest, Timeout(1800000)]
        public IEnumerator Z_W16_Perf()
        {
            if (!Shots.Graphics) { Assert.Ignore("그래픽 장치 없음(-nographics) — W16 은 그래픽 켠 배치에서"); yield break; }
            WorldRunner w = null;
            yield return Open(x => w = x);
            w.BotRead = 0.02f;
            var def = w.Def;
            var a0 = new Vector3(-199.0f, 31.0f, 103.2f); var b0 = new Vector3(-176.0f, 28.4f, 103.6f);
            w.Begin(Mid(def, Early.Concat(new[] { "C1-05", "N01", "N02" }), 312, 21 * 60 + 35, SceneLoader.Zone1, a0));
            yield return Lab.Frames(10);
            yield return Free(w);
            var cam = Camera.main;
            var rt = new RenderTexture(new RenderTextureDescriptor(1920, 1080, RenderTextureFormat.ARGB32, 24) { sRGB = true, msaaSamples = 1 }); rt.Create();
            var one = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            UnityEngine.Profiling.Profiler.enabled = true;
            var tri = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            var setp = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            var draw = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            var csv = new StringBuilder("경로,프레임,렌더ms,삼각형,SetPass,그리기\n");
            var summary = new List<string>();
            double streetP95 = 0, indoorP95 = 0;
            IEnumerator Measure(string name, Func<int, bool> step, int frames)
            {
                bool was = cam.enabled; cam.enabled = false;
                var ms = new List<double>(); var tris = new List<long>(); var sps = new List<long>(); var dcs = new List<long>();
                for (int i = 0; i < frames; i++)
                {
                    step(i);
                    yield return null;
                    if (i % 6 != 0) continue;
                    var prev = cam.targetTexture; cam.targetTexture = rt;
                    var sw = Stopwatch.StartNew();
                    cam.Render();
                    RenderTexture.active = rt; one.ReadPixels(new Rect(0, 0, 1, 1), 0, 0, false); sw.Stop();
                    RenderTexture.active = null; cam.targetTexture = prev;
                    long t = tri.LastValue, sp = setp.LastValue, dc = draw.LastValue;
#if UNITY_EDITOR
                    if (t == 0) { t = UnityEditor.UnityStats.triangles; sp = UnityEditor.UnityStats.setPassCalls; dc = UnityEditor.UnityStats.drawCalls; }
#endif
                    ms.Add(sw.Elapsed.TotalMilliseconds); tris.Add(t); sps.Add(sp); dcs.Add(dc);
                    csv.AppendLine($"{name},{i},{sw.Elapsed.TotalMilliseconds:F2},{t},{sp},{dc}");
                }
                cam.enabled = was;
                var sorted = ms.OrderBy(x => x).ToList();
                double p95 = sorted[(int)(sorted.Count * 0.95)];
                if (name.StartsWith("밤 상가거리")) streetP95 = p95;
                if (name.Contains("실내")) indoorP95 = System.Math.Max(indoorP95, p95);
                summary.Add($"{name}: 1080p 렌더 한 번 평균 {ms.Average():F2}ms · 95% {p95:F2} · 삼각형 최대 {tris.Max():N0} · SetPass 최대 {sps.Max()} · 그리기 최대 {dcs.Max()}");
            }
            // ① 밤 상가거리 걷기 — 오픈월드(세계 화면·시계·길잡이·문·지점 평가)
            var dv = b0 - a0; M3Lab.Motor.Teleport(a0, Mathf.Atan2(dv.x, dv.z) * Mathf.Rad2Deg); M3Lab.Cam.SnapBehind();
            yield return Lab.Frames(20);
            yield return Measure("밤 상가거리 걷기 · 오픈월드", i => { var d = b0 - M3Lab.Motor.Position; d.y = 0f; M3Lab.Motor.SetMoveInput(d.normalized, 1f, false); return true; }, 600);
            M3Lab.Motor.ClearMoveInput();
            // ② 실내: 국밥집 저녁(손님 6 · 김 3) · 시우네 밤(이불·스탠드)
            GameClock.SetDate(2026, 3, 12, 19 * 60);
            yield return w.Loader.Go("Gukbap", "Door", false, Vector3.zero, 0f);
            yield return Lab.Frames(30);
            yield return Measure("국밥집 저녁 실내(손님·김)", i => true, 240);
            GameClock.SetDate(2026, 3, 12, 23 * 60);
            yield return w.Loader.Go("Home", "Spawn", false, Vector3.zero, 0f);
            yield return Lab.Frames(30);
            yield return Measure("시우네 밤 실내(이불·스탠드)", i => true, 240);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/perf_world.csv", csv.ToString(), new UTF8Encoding(true));
            var outDir = Shots.Arg("-worldshots");
            if (!string.IsNullOrEmpty(outDir)) { Directory.CreateDirectory(outDir); File.WriteAllText(Path.Combine(outDir, "perf_world.csv"), csv.ToString(), new UTF8Encoding(true)); }
            Debug.Log($"[W] W16 성능(에디터 배치, {SystemInfo.graphicsDeviceName}):\n{string.Join("\n", summary)}");
            Assert.LessOrEqual(streetP95, 12.0, "밤 상가거리 오픈월드 95% ≤ 12ms");
            Assert.LessOrEqual(indoorP95, 8.0, "실내(국밥집 저녁·시우네 밤) 95% ≤ 8ms(docs/14 룩데브 성능 기준)");
        }

        // ───────────────────────── 녹화·사진
        [UnityTest, Timeout(3600000)]
        public IEnumerator Z_World_Shots()
        {
            dir = Shots.Arg("-worldshots");
            if (string.IsNullOrEmpty(dir) || !Shots.Graphics) { Assert.Ignore("-worldshots <폴더> + 그래픽 필요"); yield break; }
#if UNITY_EDITOR
            UnityEditor.ShaderUtil.allowAsyncCompilation = false;
#endif
            Directory.CreateDirectory(dir);
            WorldRunner w = null;
            yield return Open(x => w = x);
            var def = w.Def;
            var bot = WorldBot.Attach(w, false);
            bot.enabled = false;

            bool stillsOnly = Shots.Arg("-worldstills") == "only";
            if (!stillsOnly)
            {
            // ── a. 하루 루프 1일(3/11 수): 새벽 배달(자전거) → 학교 → 국밥집 서빙 → 편의점 입고 → 집 잠 → 다음 날 아침
            w.BotRide = true;
            w.Begin(Mid(def, Early.Concat(new[] { "C1-05", "N01", "N02" }), 311, 5 * 60 + 30, "Home"));
            w.Story.Bot = StoryRunner.BotMode.Read;
            yield return Lab.Frames(10); yield return Free(w);
            var rec = StartRec("a_day", 4);
            bot.MaxSleeps = w.Sleeps + 1; bot.MaxSeconds = 900f;
            yield return bot.Run();
            yield return Lab.Frames(60);
            StopRec(rec, $"a_day 하루 루프 1일({string.Join(" · ", w.Trace.Where(t => t.Contains("활동") || t.Contains("잠")).Take(8))})");
            w.Stop(); yield return Lab.Frames(5);

            // ── b. 노가다(3/21 토) · 편의점 계산대(3/22 일)
            w.BotRide = false;
            var done2 = Early.Concat(new[] { "C1-05", "C1-07", "C1-08", "C1-09", "C1-11", "C1-12", "C1-13", "C1-15", "N01", "N02" });
            w.Begin(Mid(def, done2, 321, 6 * 60 + 55, SceneLoader.Zone1));
            yield return Lab.Frames(10); yield return Free(w);
            rec = StartRec("b_jobs_site", 3);
            yield return bot.Do("Site", "job_site");
            yield return Lab.Frames(60);
            StopRec(rec, "b_jobs 노가다(유로폼·못질)");
            GameClock.SetDate(2026, 3, 22, 12 * 60 + 50);
            rec = StartRec("b_jobs_counter", 3);
            yield return bot.Do("Conv", "job_counter");
            yield return Lab.Frames(60);
            StopRec(rec, "b_jobs 편의점 계산대(거스름돈)");
            w.Stop(); yield return Lab.Frames(5);

            // ── c. 이벤트 카드 3: 유통기한 한 시간 전(C1-05) · 컵라면 말고 국밥(N01 → 체험 → N02) · 니가 행인1이냐?(C1-24 시비)
            w.Begin(Mid(def, Early, 310, 21 * 60 + 1, SceneLoader.Zone1, def.Balance.Point("conv_door").Pos + new Vector3(1.6f, 0f, -1.4f)));
            w.Story.Bot = StoryRunner.BotMode.Read;
            yield return Lab.Frames(10); yield return Free(w);
            rec = StartRec("c_card_C1-05", 3);
            yield return bot.Go("Conv");
            for (int i = 0; i < 60 * 600 && !(w.Model.CardDone("C1-05") && w.Free); i++) yield return null;
            yield return Lab.Frames(30);
            StopRec(rec, "c 카드 C1-05 「유통기한 한 시간 전」(장면 3-0~3-3)");
            w.Stop(); yield return Lab.Frames(5);
            w.BotDelegate = true;
            w.Begin(Mid(def, Early, 309, 17 * 60, "Gukbap", null, m => { m.S.Add("job.gukbap", 12); }));
            yield return Lab.Frames(10); yield return Free(w);
            rec = StartRec("c_card_N01", 3);
            yield return bot.Do("Gukbap", "job_serving");
            yield return Lab.Frames(60);
            StopRec(rec, "c 카드 N01 「컵라면 말고 국밥」 → 체험 D01·D04 → N02 「회비냐 청소냐」");
            w.Stop(); yield return Lab.Frames(5);
            var done3 = done2.Concat(new[] { "C1-16", "C1-17", "C1-18", "C1-19", "C1-20", "C1-21", "C1-22", "C1-23" });
            w.Begin(Mid(def, done3, 506, 16 * 60 + 30, SceneLoader.Zone1));
            yield return Lab.Frames(10); yield return Free(w);
            rec = StartRec("c_card_C1-24", 3);
            var c24 = w.Director.Pick("after:school");
            Assert.NotNull(c24, "C1-24");
            yield return w.RunCard(c24);
            yield return Lab.Frames(60);
            StopRec(rec, $"c 카드 C1-24 「니가 행인1이냐?」 시비 {(w.Model.S.StreetWins > 0 ? "승" : "패")}");
            w.Stop(); yield return Lab.Frames(5);

            // ── d. 챕터 목표 달성: 새 게임부터 봇(전부)이 실제로 5/8 까지 → 그날 어버이날(C1-26) → 데모 끝 판정 → 2장 예고(5/8 부터만 녹화 — 숫자는 실제 진행 값)
            w.BotRead = 0.02f; w.BotDelegate = true; w.BotRide = false;
            w.Begin(null);
            var full = WorldBot.Attach(w, true);
            full.MaxSleeps = -1; full.MaxSeconds = 1500f;
            Rec recD = null;
            var runD = full.Run();
            while (runD.MoveNext())
            {
                if (recD == null && w.Model != null && w.Model.Today >= 508 && w.Model.Now >= 16 * 60) { recD = StartRec("d_goal", 3); w.BotRead = 2.2f; }
                yield return runD.Current;
            }
            for (int i = 0; i < 60 * 30 && !w.Free; i++) yield return null;
            yield return Lab.Frames(60);
            if (recD != null) StopRec(recD, $"d 챕터 목표 — 3/3 부터 봇 실제 진행 → 5/8 어버이날 → 데모 끝 {(w.Model.S.DemoState == 1 ? "목표 달성" : "미달")} → 2장 예고 · {w.Model.Status}");
            Assert.AreEqual(1, w.Model.S.DemoState, "녹화 d: 목표 달성");
            Object.Destroy(full);
            w.Stop(); yield return Lab.Frames(5);
            w.Begin(Mid(def, Early.Concat(new[] { "C1-05", "N01", "N02" }), 312, 16 * 60 + 40, SceneLoader.Zone1, null, m => { m.S.Household = 186000; m.L.SetWallet(12600); m.L.Apply("envelope", "+5000"); m.S.TownRep = 9.6f; }));
            yield return Lab.Frames(10); yield return Free(w);

            }
            else
            {
                w.Begin(Mid(def, Early.Concat(new[] { "C1-05", "N01", "N02" }), 312, 16 * 60 + 40, SceneLoader.Zone1, null, m => { m.S.Household = 186000; m.L.SetWallet(12600); m.L.Apply("envelope", "+5000"); m.S.TownRep = 9.6f; }));
                yield return Lab.Frames(10); yield return Free(w);
            }

            // ── 사진: 실내 2차(시간대) · HUD · 휴대폰 · 주간 계획
            w.BotRead = 0.02f;
            string[][] stills =
            {
                new[] { "Gukbap", "1140", "Sock_Cam_s02", "st_gukbap_evening" }, new[] { "Gukbap", "370", "Sock_Cam_s01", "st_gukbap_dawn" },
                new[] { "Home", "450", "Sock_Cam_s03", "st_home_morning" }, new[] { "Home", "1380", "Sock_Cam_s04", "st_home_night" },
                new[] { "Conv", "1290", "Sock_Cam_s05", "st_conv_night" }, new[] { "Conv", "1290", "Sock_Cam_s06", "st_conv_counter" },
            };
            foreach (var s in stills)
            {
                GameClock.SetDate(2026, 3, 12, int.Parse(s[1]));
                yield return w.Loader.Go(s[0], s[0] == "Home" ? "Spawn" : "Door", false, Vector3.zero, 0f);
                yield return Lab.Frames(40);
                var sock = w.Loader.Stage != null ? w.Loader.Stage.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == s[2]) : null;
                if (sock == null) { Note($"사진 {s[3]}: 소켓 {s[2]} 없음"); continue; }
                var go = new GameObject("사진 카메라"); var c = go.AddComponent<Camera>();
                go.transform.SetPositionAndRotation(sock.position, sock.rotation); c.fieldOfView = 48f; c.nearClipPlane = 0.05f;
                var main = Camera.main; c.CopyFrom(main); go.transform.SetPositionAndRotation(sock.position, sock.rotation); c.fieldOfView = 48f;
                var life = w.Loader.Stage.GetComponent<StageLife>();
                if (life != null) life.Apply(GameClock.Minutes);
                var vis = M3Lab.Motor != null ? M3Lab.Motor.transform.Find("Visual") : null;
                if (vis != null) vis.gameObject.SetActive(false);       // 실내 사진엔 시우를 빼고(소켓 카메라 앞을 가림)
                yield return Lab.Frames(30);
                Shots.Shot(c, Path.Combine(dir, s[3] + ".png"), 1920, 1080, 2, false);
                if (vis != null) vis.gameObject.SetActive(true);
                Note($"사진 {s[3]}.png — {s[0]} {GameClock.TimeText} · 켠 무리 {(life != null ? life.ActiveSlots : -1)}");
                Object.Destroy(go);
            }
            GameClock.SetDate(2026, 3, 12, 16 * 60 + 40);
            var door = w.FindDoor("Gukbap");
            yield return w.Loader.Go(SceneLoader.Zone1, null, true, door.ReturnPos, door.ReturnYaw + 180f);
            yield return Lab.Frames(60);
            SavePng(Camera.main, Path.Combine(dir, "ui_hud_street.png"), 1920, 1080);
            w.OpenPhone(4); yield return Lab.Frames(5);
            SavePng(Camera.main, Path.Combine(dir, "ui_phone_goals.png"), 1920, 1080);
            w.Phone.SetTab(3); yield return Lab.Frames(3);
            SavePng(Camera.main, Path.Combine(dir, "ui_phone_ledger.png"), 1920, 1080);
            w.Phone.Close(); yield return Lab.Frames(3);
            var plan = w.Planner.Make(GameClock.DayKey, 14);
            var co = w.PlannerView.Edit(plan, d => { var e = w.Director.EventDay(d); return e != null ? $"「{e.Title}」" : null; }, "주간 계획 — 3월 12일부터 2주", v => { }, 1, 0.5f);
            w.StartCoroutine(co);
            yield return Lab.Frames(6);
            SavePng(Camera.main, Path.Combine(dir, "ui_planner.png"), 1920, 1080);
            yield return Lab.Frames(60);
            Note("사진 ui_hud_street · ui_phone_goals · ui_phone_ledger · ui_planner");
            File.WriteAllText(Path.Combine(dir, "notes_world.txt"), string.Join("\n", notes), new UTF8Encoding(true));
            Debug.Log("[WShots]\n" + string.Join("\n", notes));
        }

        // ───────────────────────── 돈 쓸 곳 녹화·사진(14-6): 출장 레슨(김승연) · 가게 앞 판매창 식사 · 고르기·휴대폰 레슨/가방 · HUD 효과 아이콘
        [UnityTest, Timeout(1800000)]
        public IEnumerator Z_World_Shop_Shots()
        {
            dir = Shots.Arg("-worldshots");
            if (string.IsNullOrEmpty(dir) || !Shots.Graphics) { Assert.Ignore("-worldshots <폴더> + 그래픽 필요"); yield break; }
#if UNITY_EDITOR
            UnityEditor.ShaderUtil.allowAsyncCompilation = false;
#endif
            Directory.CreateDirectory(dir);
            WorldRunner w = null;
            yield return Open(x => w = x);
            var def = w.Def;
            int Start(CardDef c) => c.Date > 0 ? c.Date : c.WinFrom;
            var done = Early.Concat(new[] { "N01", "N02", "C1-L0", "C1-L1" }).Concat(def.Cards.Where(c => c.Demo && c.Miss == "never" && Start(c) > 0 && Start(c) <= 314).Select(c => c.Id)).Distinct().ToArray();
            // ── e. 출장 레슨(3/14 토 17:30 김승연 · 딥마카이): 카드 → 드릴(코치 큐) → 결과 카드
            w.Begin(Mid(def, done, 314, 16 * 60 + 50, SceneLoader.Zone1, null, m => { m.L.SetWallet(240000); m.G.Fatigue = 30; }));
            w.Story.Bot = StoryRunner.BotMode.Read;
            yield return Lab.Frames(10); yield return Free(w);
            Assert.AreEqual("", w.BookNext("seungyeon"));
            yield return w.Loader.Go(SceneLoader.Gym, "Spawn", false, Vector3.zero, 0f);
            GymManager gm = null;
            for (int i = 0; i < 300 && gm == null; i++) { gm = GymManager.Instance; yield return null; }
            yield return Lab.Frames(30);
            w.BotLessonDrill = c => "D04";
            var rec = StartRec("e_lesson", 3);
            GameClock.AdvanceTo(17 * 60 + 20);
            bool cardShot = false, cueShot = false;
            for (int i = 0; i < 60 * 900 && w.LessonsRun < 1; i++)
            {
                if (!cardShot && w.Story.Board != null && w.Busy == "레슨" && gm.Runner.State == DrillRunner.Phase.Idle && i > 20) { yield return Lab.Frames(8); SavePng(Camera.main, Path.Combine(dir, "st_lesson_card.png"), 1920, 1080); cardShot = true; }
                if (!cueShot && gm.Runner.State == DrillRunner.Phase.Round && gm.Runner.T > 9f) { SavePng(Camera.main, Path.Combine(dir, "st_lesson_cue.png"), 1920, 1080); cueShot = true; }
                yield return null;
            }
            yield return Lab.Frames(90);
            StopRec(rec, $"e 출장 레슨 — {w.LastLessonLine}");
            yield return Free(w);
            // ── f. 가게 앞 판매창(와룡 삼계탕) 식사
            GameClock.SetDate(2026, 3, 15, 12 * 60 + 10);
            var front = Object.FindObjectsByType<PlaceMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(p => p.Func == "shop_samgye");
            Assert.NotNull(front, "판매창 표시 자리");
            var fp = front.transform.position; var ff = front.transform.parent.forward;
            yield return w.Loader.Go(SceneLoader.Zone1, null, true, fp + ff * 1.2f, Quaternion.LookRotation(-ff).eulerAngles.y);
            yield return Lab.Frames(60);
            var cgo = new GameObject("사진 카메라"); var cam = cgo.AddComponent<Camera>(); cam.CopyFrom(Camera.main);
            cgo.transform.position = fp + ff * 6.5f + Vector3.up * 1.7f - front.transform.parent.right * 1.2f;
            cgo.transform.rotation = Quaternion.LookRotation((fp + Vector3.up * 1.6f) - cgo.transform.position);
            cam.fieldOfView = 50f;
            yield return Lab.Frames(10);
            Shots.Shot(cam, Path.Combine(dir, "st_shopfront.png"), 1920, 1080, 2, false);
            Object.Destroy(cgo);
            w.Model.G.Fatigue = 62;
            rec = StartRec("f_meal", 3);
            w.UseMarker("shop_samgye", front.transform);
            bool mealShot = false;
            for (int i = 0; i < 60 * 120 && (w.MealsRun < 1 || !w.Free); i++)
            {
                if (!mealShot && w.Busy == "가게" && w.MealsRun >= 1) { yield return Lab.Frames(6); SavePng(Camera.main, Path.Combine(dir, "st_meal_card.png"), 1920, 1080); mealShot = true; }
                yield return null;
            }
            yield return Lab.Frames(60);
            StopRec(rec, $"f 식사 — {w.LastMealLine}");
            SavePng(Camera.main, Path.Combine(dir, "ui_hud_buffs.png"), 1920, 1080);
            // ── 사람 모드 화면: 편의점 차림표(고르기) · 휴대폰 레슨 · 가방
            w.Model.L.Items.Add("에너지바"); w.Model.L.Items.Add("바나나우유"); w.Model.L.Items.Add("폐기 직전 삼각김밥");
            w.Model.SetStanding(def.Shop.Coach("yunjun"), 317, 21 * 60 + 15);
            float bs = w.BotSkill; w.BotSkill = 0f;
            var pick = w.Picker;
            w.StartCoroutine(pick.Pick("하루편의점 계산대 — 지갑 " + Ledger.Won(w.Model.L.Wallet), def.Shop.Foods.Where(f => f.Shop == "conv").Select(f => $"{f.Name} {Ledger.Won(f.Price)}원").ToArray(), null, v => { }));
            yield return Lab.Frames(6);
            SavePng(Camera.main, Path.Combine(dir, "ui_pick_shop.png"), 1920, 1080);
            pick.Force(-1); yield return Lab.Frames(4);
            w.OpenPhone(7); yield return Lab.Frames(5);
            SavePng(Camera.main, Path.Combine(dir, "ui_phone_lesson.png"), 1920, 1080);
            w.Phone.SetTab(8); yield return Lab.Frames(3);
            SavePng(Camera.main, Path.Combine(dir, "ui_phone_bag.png"), 1920, 1080);
            w.Phone.Close(); w.BotSkill = bs;
            Note("사진 st_lesson_card · st_lesson_cue · st_shopfront · st_meal_card · ui_hud_buffs · ui_pick_shop · ui_phone_lesson · ui_phone_bag");
            File.WriteAllText(Path.Combine(dir, "notes_shop.txt"), string.Join("\n", notes), new UTF8Encoding(true));
            Debug.Log("[WShots]\n" + string.Join("\n", notes));
        }
    }
}
