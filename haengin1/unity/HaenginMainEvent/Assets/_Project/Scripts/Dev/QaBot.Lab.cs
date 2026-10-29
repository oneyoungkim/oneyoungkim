// 행인1의 메인이벤트 — QA 봇 B: 실험실 — A 에서 남긴 중간 저장(3/22 뒤 · 관원)을 고쳐 불러와 한 가지씩 사람 손으로
//   알바 5(+설거지·폐기) × 처음 손 놓기(실패)·S·맡기기 · 배달 막 몰기 · 드릴 10 + 사람 드릴 · 청소 관원 · 레슨 5 예약·출석·노쇼 · 가게 5 + 돈 0 · 배부름 한도
//   휴대폰 · 주간 계획 손으로 · 시비 패배 · 저장·불러오기(카드 중·알바 중·밤 10시·말일) · 경계(피로·밤 근로·잠 시각·맵 끝·계단·벽 카메라·문 연타)
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Haengin
{
    public sealed partial class QaBot
    {
        IEnumerator PartB()
        {
            if (labBase == null)
            {
                var f = Path.Combine(Out, "lab_base.json");
                string alt = ArgVal("-qabase", "");
                if (alt.Length > 0 && File.Exists(alt)) f = alt;
                if (File.Exists(f)) labBase = SaveStore.ReadFile(f, out _);
            }
            if (W == null || !W.Active)
            {
                // B 만 돌릴 때: 타이틀 → 새 게임으로 세계를 띄운 뒤 바탕 저장으로 갈아탐
                var title = FindAnyObjectByType<TitleMenu>();
                if (title != null) title.Choose("새 게임");
                for (int i = 0; i < 3600 && (W == null || !W.Active); i++) yield return null;
                W.BotSkill = 0.9f; W.BotRead = 0.05f;
                for (int i = 0; i < 60 * 600 && !W.Free; i++) yield return null;
            }
            if (labBase == null) { Issue("lab.nobase", "QA", "실험실 바탕 저장이 없음 — B 건너뜀"); yield break; }
            baseDay = labBase.Month * 100 + labBase.Day;
            L($"실험실 바탕: {labBase.Label}");
            yield return Section("B00 새벽 눈부심 원인(룩데브 단계·해·볼륨 끄기)", B_Glare, 300f);
            yield return Section("B01 알바 — 국밥집 서빙", () => B_Job("job.gukbap", "Gukbap", "job_serving", "월화수목금", 16 * 60 + 40), 900f);
            yield return Section("B02 알바 — 국밥집 설거지", () => B_Job("job.gukbap", "Gukbap", "job_dishes", "월화수목금", 16 * 60 + 40, 2), 600f);
            yield return Section("B03 알바 — 편의점 입고", () => B_Job("job.stock", "Conv", "job_stock", "월화수목금", 21 * 60 + 2), 900f);
            yield return Section("B04 알바 — 편의점 계산대", () => B_Job("job.counter", "Conv", "job_counter", "일", 12 * 60 + 40), 900f);
            yield return Section("B05 알바 — 노가다", () => B_Job("job.site", "Site", "job_site", "토", 6 * 60 + 40), 900f);
            yield return Section("B06 알바 — 새벽 배달(막 몰기·S·맡기기)", B_Delivery, 900f);
            yield return Section("B07 체육관 — 드릴 10 + 사람 드릴", B_Drills, 1200f);
            yield return Section("B08 체육관 — 청소 관원", B_Clean, 300f);
            yield return Section("B09 레슨 — 5명 예약·출석", B_Lessons, 1500f);
            yield return Section("B10 레슨 — 노쇼", B_NoShow, 400f);
            yield return Section("B11 가게 — 편의점·국밥집·판매창 3", B_Shops, 900f);
            yield return Section("B12 가게 — 돈 0 · 배부름 한도", B_ShopEdge, 500f);
            yield return Section("B13 휴대폰 — 9칸 · 가방 먹기", B_Phone, 300f);
            yield return Section("B14 주간 계획 — 손으로 고쳐 맡기기", B_Planner, 600f);
            yield return Section("B15 시비 — 손 놓고 지기", B_Street, 600f);
            yield return Section("B16 저장·불러오기 — 카드 중", B_SaveCard, 400f);
            yield return Section("B17 저장·불러오기 — 알바 중(Esc·Tab)", B_SaveJob, 400f);
            yield return Section("B18 밤 10시 경계 · 새벽 2시", B_Night, 500f);
            yield return Section("B19 말일 이체 직전 저장·불러오기", B_MonthEnd, 500f);
            yield return Section("B20 경계 — 피로 최대", B_Fatigue, 500f);
            yield return Section("B21 경계 — 이상한 잠 시각", B_OddSleep, 400f);
            yield return Section("B22 경계 — 맵 끝 · 벽 카메라", B_Edges, 500f);
            yield return Section("B23 경계 — 계단 5곳", B_Stairs, 500f);
            yield return Section("B24 경계 — 문 연타 · 휴대폰 연타", B_Spam, 300f);
            yield return Section("B25 데모 끝 카드 · 2장 예고(사람 손)", B_DemoEnd, 300f);
        }

        int baseDay;

        /// 바탕 날짜 이후 첫 그 요일(days 중 하나), 비 오는 날은 건너뜀(rainOk 아니면)
        int NextDay(string days, int after = -1, bool rainOk = true)
        {
            int k = after < 0 ? baseDay : after;
            for (int i = 0; i < 40; i++)
            {
                int d = GameClock.KeyPlus(k, i);
                string w = GameClock.WeekdayOfKey(d);
                if (!days.Contains(w)) continue;
                if (M != null && M.Holiday(d) && days != "일" && days != "토") continue;
                if (!rainOk && M != null && M.Rain(d)) continue;
                if (d >= 508) break;
                return d;
            }
            return k;
        }

        /// 바탕 저장을 고쳐 불러옴(quiet = 앞으로 올 이야기 카드를 끝난 것으로 — 그 일만 보이게)
        IEnumerator Load(int day, int min, string place, Action<WorldModel> tweak, bool quiet = true, Vector3? pos = null, float yaw = 0f)
        {
            var sd = labBase.Clone();
            sd.Month = day / 100; sd.Day = day % 100; sd.Minutes = min;
            var ws = sd.World;
            ws.Place = place; ws.Card = "";
            ws.Spawn = place == SceneLoader.Zone1 ? "" : (place == "Home" || place == SceneLoader.Gym || place == "Site") ? "Spawn" : "Door";
            ws.HasPos = place == SceneLoader.Zone1;
            if (ws.HasPos) { ws.Pos = pos ?? (W.Def.Balance.Point("bus_stop").Pos + new Vector3(1.6f, 0f, 0f)); ws.Yaw = yaw; }
            ws.Lessons.Clear(); ws.Standing.Clear();
            if (W.Active) W.Stop();
            yield return Frames(6);
            botMode = false; humanSkill = 0f; watchBot = false;
            W.BotSkill = 0f; W.BotRead = 0.15f; W.BotRide = false; W.BotChoice = null; W.BotDelegate = true;
            ChoicePolicy = null; PickPolicy = null; PlannerPolicy = 1; JobPolicy = "none";
            W.Begin(sd);
            Story().Bot = StoryRunner.BotMode.None;
            if (Story().Talk != null) Story().Talk.Bot = null;
            if (quiet) foreach (var c in W.Def.Cards) if (c.Demo && c.Kind != CardKind.Repeat && !M.CardDone(c.Id)) M.MarkCard(c, 3);
            M.G.Fatigue = Mathf.Min(M.G.Fatigue, 20);
            tweak?.Invoke(M);
            yield return Advance(180f);
            yield return Frames(20);
            L($"불러옴 {day / 100}/{day % 100} {GameClock.Hm(min)} {place} — {M.Status}");
        }

        void ResetJob(WorldModel m, string id) { m.AddCount(id, -m.Count(id)); m.AddCount("best." + id, -m.Count("best." + id)); m.S.DoneToday.Remove(id); }   // QA 2차: 바탕 저장 그날 이미 한 일이면 '오늘은 이미 했다'로 판이 안 열림(B17·C00 A4)
        void SetBest(WorldModel m, string id, Grade g) { m.AddCount("best." + id, (int)g - m.Count("best." + id)); }

        void Unlocks(WorldModel m)
        {
            foreach (var u in new[] { "job.delivery", "job.gukbap", "job.stock", "job.counter", "job.site", "school", "planner", "lesson", "lesson.guests", "gym.cleaner" }) m.Unlock(u);
            m.F.Raise("site_booked"); m.F.Raise("consent");
            if (m.S.GymMember == 0) m.S.GymMember = 1;
        }

        // ───────────────────────── 알바(처음 손 놓기 · S · 맡기기)
        IEnumerator B_Job(string act, string place, string func, string days, int at, int only = 3)
        {
            var a = W.Def.Activity(act);
            int d1 = NextDay(days, -1, act != "job.site");
            for (int run = 0; run < only; run++)
            {
                int day = run == 0 ? d1 : NextDay(days, GameClock.KeyPlus(d1, run * 7 - 6 + 6), act != "job.site");
                if (run > 0) day = NextDay(days, GameClock.KeyPlus(d1, 1 + (run - 1) * 7), act != "job.site");
                string tag = run == 0 ? "처음_손놓기" : run == 1 ? "S" : "맡기기";
                yield return Load(day, at, place, m =>
                {
                    Unlocks(m);
                    ResetJob(m, act);
                    if (run == 2) SetBest(m, act, Grade.S);
                });
                if (Ld.Place != place) { Issue($"lab.place.{func}", "QA", $"{place} 로 못 불러옴(지금 {Ld.Place})"); continue; }
                int pay0 = M.L.Wallet + M.S.Household;
                yield return Shot($"{func}_{tag}_표시", W.MarkerPrompt(func));
                var pm = Marker(func);
                if (pm == null) { Issue($"marker.none.{func}", "눈에 띔", $"{place} 에 '{func}' 표시 자리 없음"); yield break; }
                yield return UseMarker(func);
                yield return WaitFor(() => ChoiceShown || (W.Job != null && W.Job.Shown) || W.Busy == "", 4f);
                if (ChoiceShown)
                {
                    var ch = Story().Talk.Choice;
                    var labels = Enumerable.Range(0, ch.Count).Select(ch.Label).ToArray();
                    yield return Shot($"{func}_{tag}_묻기", string.Join(" / ", labels));
                    if (run == 2 && labels.Length < 3) Issue($"job.nodelegate.{func}", "눈에 띔", $"S 받은 뒤인데 '맡긴다'가 없음: {string.Join(" / ", labels)}");
                    if (run == 1) { W.BotSkill = 0.98f; JobPolicy = "bot"; }
                    else JobPolicy = "none";
                    yield return Choose(run == 2 && labels.Length >= 3 ? 1 : 0);
                }
                else { yield return Shot($"{func}_{tag}_안묻고"); }
                yield return Advance(240f);
                W.BotSkill = 0f;
                int pay1 = M.L.Wallet + M.S.Household;
                yield return Shot($"{func}_{tag}_끝", $"번 돈 {pay1 - pay0} · {M.Status}");
                L($"{func} {tag}: 번 돈 {pay1 - pay0} · 셈 {M.Count(act)} · 최고 {M.Count("best." + act)} · 피로 {M.G.Fatigue}");
                if (run == 0 && pay1 - pay0 <= 0 && a != null && a.Pay > 0) L($"{func} 손 놓기: 돈 0 — 등급 D 돈 규칙 확인");
            }
        }

        // ───────────────────────── 새벽 배달 출발 화면이 하얗게 날아가는 원인 찾기
        IEnumerator B_Glare()
        {
            var bc = BikeCourse.Current != null ? BikeCourse.Current : FindAnyObjectByType<BikeCourse>(FindObjectsInactive.Include);
            Vector3 at = bc != null ? bc.Start : W.Def.Balance.Point("bus_stop").Pos;
            float yaw = bc != null ? bc.StartYaw : 90f;
            int day = NextDay("월화수목금");
            foreach (var (min, tag) in new[] { (6 * 60 + 4, "0604"), (12 * 60, "1200"), (18 * 60 + 10, "1810") })
            {
                yield return Load(day, min, SceneLoader.Zone1, m => Unlocks(m), true, at, yaw);
                Mo.Teleport(at, yaw); Ld.Cam?.SnapBehind();
                yield return Frames(40);
                foreach (var v in new[] { "base", "ink", "haze", "light", "detail", "lut" })
                {
                    LookdevFlags.Variant(v);
                    yield return Frames(12);
                    yield return Shot($"눈부심_{tag}_{v}");
                }
                if (tag == "0604")
                {
                    var sun = RenderSettings.sun;
                    float si = sun != null ? sun.intensity : 0f;
                    if (sun != null) { sun.intensity = 0f; yield return Frames(6); yield return Shot("눈부심_0604_해끔"); sun.intensity = si; }
                    var vols = FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None);
                    var ws = vols.Select(v => v.weight).ToArray();
                    foreach (var v in vols) v.weight = 0f;
                    yield return Frames(6); yield return Shot("눈부심_0604_볼륨끔", string.Join(",", vols.Select(v => v.name)));
                    for (int i = 0; i < vols.Length; i++) vols[i].weight = ws[i];
                    foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l => l.enabled && l.intensity > 0f && Vector3.Distance(l.transform.position, Mo.Position) < 25f).OrderBy(l => Vector3.Distance(l.transform.position, Mo.Position)).Take(12))
                        L($"눈부심 근처 빛: {PathOf(l.transform)} {l.type} 세기 {l.intensity:F2} 범위 {l.range:F1} 거리 {Vector3.Distance(l.transform.position, Mo.Position):F1} 색 {l.color}");
                    L($"눈부심 해: {(sun != null ? PathOf(sun.transform) + " " + sun.intensity.ToString("F2") + " 각 " + sun.transform.eulerAngles : "없음")}");
                }
            }
            LookdevFlags.Variant("lut");
            // 숫자로: 흰 화소 비율 — 렌더러 기능·셰이더 무리·가까운 빛을 하나씩 꺼 봄(빌드에서만 생기는 눈부심 — 편집기 시험 Q24 는 0.9%)
            yield return Load(day, 6 * 60 + 4, SceneLoader.Zone1, m => Unlocks(m), true, at, yaw);
            Mo.Teleport(at, yaw); Ld.Cam?.SnapBehind();
            yield return Frames(40);
            var cam = Camera.main;
            float all = WhiteNow(cam);
            L($"눈부심 수치: 지금 흰 화소 {all:P1}");
            var rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            var fi = rp != null ? typeof(UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset).GetField("m_RendererDataList", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) : null;
            var datas = fi != null ? fi.GetValue(rp) as UnityEngine.Rendering.Universal.ScriptableRendererData[] : null;
            if (datas != null)
                foreach (var d in datas)
                {
                    if (d == null) continue;
                    foreach (var f in d.rendererFeatures)
                    {
                        if (f == null || !f.isActive) continue;
                        f.SetActive(false); yield return Frames(3);
                        float w = WhiteNow(cam);
                        f.SetActive(true); yield return Frames(2);
                        L($"눈부심 수치: 렌더러 기능 '{f.name}' 끄면 {w:P1}");
                    }
                }
            foreach (var g in FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r => r.enabled && r.isVisible && r.sharedMaterial != null).GroupBy(r => r.sharedMaterial.shader.name))
            {
                var rs = g.ToList();
                foreach (var r in rs) r.enabled = false;
                float w = WhiteNow(cam);
                foreach (var r in rs) r.enabled = true;
                if (all - w > 0.01f) L($"눈부심 수치: 셰이더 '{g.Key}' 끄면 {w:P1} ({rs.Count}개: {string.Join(", ", rs.Take(5).Select(r => PathOf(r.transform)))})");
            }
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l => l.enabled && l.intensity > 0f && l.type != LightType.Directional && Vector3.Distance(l.transform.position, Mo.Position) < 30f).ToList())
            {
                float was = l.intensity; l.intensity = 0f;
                float w = WhiteNow(cam);
                l.intensity = was;
                if (all - w > 0.005f) L($"눈부심 수치: 빛 '{PathOf(l.transform)}' 세기 {was:F1} 범위 {l.range:F0} 끄면 {w:P1}");
            }
            yield return Shot("눈부심_수치_뒤");
            // 화면(백버퍼)에서만 생기는지 — 화면 캡처로 재고, 카메라·오버레이 캔버스를 하나씩 끔
            yield return ScreenWhite(v => screenWhite = v);
            L($"눈부심 수치(화면 캡처): 지금 {screenWhite:P1} · 카메라 RT 렌더 {WhiteNow(cam):P1}");
            foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                var ad = c.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                L($"눈부심 카메라: {PathOf(c.transform)} 켬 {c.isActiveAndEnabled} · 깊이 {c.depth} · RT {(c.targetTexture != null ? c.targetTexture.name : "-")} · 후처리 {(ad != null && ad.renderPostProcessing)} · 종류 {(ad != null ? ad.renderType.ToString() : "-")} · HDR {c.allowHDR} · 태그 {c.tag} · 화면칸 {c.rect}");
            }
            foreach (var cv in FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(x => x.isRootCanvas && x.isActiveAndEnabled && x.renderMode == RenderMode.ScreenSpaceOverlay).ToList())
            {
                cv.enabled = false;
                yield return ScreenWhite(v => screenWhite = v);
                cv.enabled = true;
                L($"눈부심 수치(화면): 캔버스 '{PathOf(cv.transform)}'(순서 {cv.sortingOrder}) 끄면 {screenWhite:P1}");
            }
            foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None).Where(c => c.isActiveAndEnabled && c != cam).ToList())
            {
                c.enabled = false;
                yield return ScreenWhite(v => screenWhite = v);
                c.enabled = true;
                L($"눈부심 수치(화면): 카메라 '{PathOf(c.transform)}' 끄면 {screenWhite:P1}");
            }
        }

        float screenWhite;
        IEnumerator ScreenWhite(Action<float> got)
        {
            yield return null;
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            var px = tex.GetPixels32(); int n = 0;
            for (int i = 0; i < px.Length; i += 7) { var cc = px[i]; if (cc.r > 240 && cc.g > 240 && cc.b > 240) n++; }
            Destroy(tex);
            got(n / (px.Length / 7f));
        }

        float WhiteNow(Camera c)
        {
            var big = new RenderTexture(new RenderTextureDescriptor(640, 360, RenderTextureFormat.ARGB32, 24) { sRGB = true, msaaSamples = 1 });
            big.Create();
            var prevT = c.targetTexture; var prevA = RenderTexture.active;
            c.targetTexture = big; c.Render(); c.targetTexture = prevT;
            RenderTexture.active = big;
            var tex = new Texture2D(640, 360, TextureFormat.RGBA32, false, false);
            tex.ReadPixels(new Rect(0, 0, 640, 360), 0, 0); tex.Apply();
            RenderTexture.active = prevA;
            var px = tex.GetPixels32(); int n = 0;
            for (int i = 0; i < px.Length; i += 3) { var cc = px[i]; if (cc.r > 240 && cc.g > 240 && cc.b > 240) n++; }
            Destroy(tex); big.Release(); Destroy(big);
            return n / (px.Length / 3f);
        }

        // ───────────────────────── 새벽 배달
        IEnumerator B_Delivery()
        {
            int d1 = NextDay("월화수목금토");
            for (int run = 0; run < 3; run++)
            {
                int day = NextDay("월화수목금토", GameClock.KeyPlus(d1, run));
                string tag = run == 0 ? "막몰기" : run == 1 ? "S" : "맡기기";
                yield return Load(day, 5 * 60 + 40, "Gukbap", m => { Unlocks(m); ResetJob(m, "job.delivery"); if (run == 2) SetBest(m, "job.delivery", Grade.S); });
                yield return UseMarker("job_delivery");
                yield return WaitFor(() => ChoiceShown, 4f);
                if (ChoiceShown)
                {
                    yield return Shot($"배달_{tag}_묻기");
                    if (run == 1) { W.BotSkill = 0.98f; W.BotRide = true; }
                    yield return Choose(run == 2 && Story().Talk.Choice.Count >= 3 ? 1 : 0);
                }
                if (run == 0)
                {
                    yield return WaitFor(() => BikeCourse.Current != null && BikeCourse.Current.Riding, 30f);
                    var bc = BikeCourse.Current;
                    if (bc != null && bc.Riding)
                    {
                        yield return Frames(20);
                        var p0 = bc.Motor.Position;
                        // 사람이 엉뚱하게: 오른쪽으로 꺾어 벽·난간 쪽으로, 뒤로, 왼쪽 끝까지
                        yield return RecWhile("자전거_막몰기", 14f, MashBike());
                        var p1 = bc.Motor.Position;
                        yield return Shot("배달_막몰기_뒤", $"{p0:F1} → {p1:F1}");
                        if (!Physics.Raycast(p1 + Vector3.up * 0.8f, Vector3.down, out var h, 3f, Layers.Solid, QueryTriggerInteraction.Ignore)) Issue("bike.noground", "진행 불가?", $"자전거 발밑 땅 없음 {p1:F1}");
                        if (p1.y < 10f) Issue("bike.fall", "진행 불가", $"자전거가 맵 아래로 {p1:F1}");
                        bc.Bot = true;
                        RecStart("자전거_막몰기뒤_봇", 8f);
                        yield return WaitFor(() => !bc.Riding, 300f);
                    }
                }
                else if (run == 1) RecStart("자전거_S_봇", 15f);
                yield return Advance(400f);
                W.BotSkill = 0f; W.BotRide = false;
                yield return Shot($"배달_{tag}_끝", M.Status);
            }
        }

        IEnumerator MashBike()
        {
            yield return Hold(2.5f, Key.W);
            yield return Hold(2.0f, Key.W, Key.D);
            yield return Hold(1.5f, Key.S);
            yield return Hold(2.5f, Key.W, Key.A);
            yield return Hold(2.0f, Key.W);
            yield return Hold(1.0f, Key.E);
        }

        // ───────────────────────── 체육관 드릴
        IEnumerator B_Drills()
        {
            int day = NextDay("화목");
            yield return Load(day, 18 * 60, SceneLoader.Gym, m => { Unlocks(m); m.G.Fatigue = 0; });
            if (Gm == null) { Issue("gym.none", "진행 불가?", "체육관이 안 열림"); yield break; }
            yield return Shot("체육관_저녁");
            // 사람: 드릴 표시 앞으로 가서 × → 메뉴 → 직접 → 손 놓고 끝까지
            var mk = Gm.Markers.FirstOrDefault(x => x != null && x.isActiveAndEnabled);
            if (mk != null)
            {
                yield return WalkTo(mk.transform.position, 0.8f, 12f, false, "드릴 표시");
                yield return Tap(Key.E);
                yield return Frames(20);
                if (Gm.Menu != null && Gm.Menu.Open)
                {
                    yield return Shot("드릴메뉴_사람", string.Join(" / ", Enumerable.Range(0, 4).Select(Gm.Menu.Label)));
                    yield return Tap(Key.Enter);
                    yield return Frames(30);
                    yield return Shot("드릴_사람_손놓기_시작");
                    QuietStuck = true;
                    yield return WaitFor(() => Gm.Runner.State == DrillRunner.Phase.Result || !Gm.Busy, 120f);
                    QuietStuck = false;
                    yield return Advance(60f);
                }
                else Issue("gym.marker", "눈에 띔", $"드릴 표시에서 × 를 눌러도 메뉴가 안 뜸({mk.name})");
            }
            string[] recs = { "D01", "D04", "D07", "D08" };
            foreach (var def in Gm.Book.Drills.Where(x => x.Playable))
            {
                M.G.Fatigue = 0;
                yield return Frames(10);
                L($"드릴 {def.Id} {def.NameKr}");
                bool rec = recs.Contains(def.Id);
                var play = PlayDrillShots(def);
                if (rec) yield return RecWhile("드릴_" + def.Id + "_" + def.NameKr, 12f, play);
                else yield return play;
                yield return Advance(60f);
            }
        }

        IEnumerator PlayDrillShots(DrillDef def)
        {
            if (!Gm.StartDrill(def.Id, 0.95f)) { Issue("drill.start." + def.Id, "눈에 띔", $"드릴 {def.Id} 시작 안 됨"); yield break; }
            float t = 0f; int s = 0;
            while (Gm.Runner.State != DrillRunner.Phase.Result && t < 200f)
            {
                t += Time.unscaledDeltaTime;
                if ((s == 0 && t > 2.5f) || (s == 1 && t > 9f)) { s++; yield return Shot($"드릴_{def.Id}_{s}", def.NameKr); }
                yield return null;
            }
            yield return Frames(20);
            yield return Shot($"드릴_{def.Id}_결과", Gm.Card != null ? Gm.Card.GradeText : "");
            yield return WaitFor(() => Gm.Card == null || !Gm.Card.Visible, 6f);
            if (Gm.Card != null && Gm.Card.Visible) yield return Tap(Key.Enter);
            yield return Frames(10);
        }

        IEnumerator B_Clean()
        {
            int day = NextDay("월수금");
            yield return Load(day, 20 * 60 + 50, SceneLoader.Gym, m => { Unlocks(m); m.S.GymMember = 2; });
            yield return Shot("청소_표시", W.MarkerPrompt("gym_clean"));
            yield return UseMarker("gym_clean");
            yield return Advance(200f);
            yield return Shot("청소_끝", M.Status);
        }

        // ───────────────────────── 레슨
        IEnumerator BookByPhone(int coachIdx, int payIdx = 0, int fixIdx = 0)
        {
            if (!W.Free) yield return Advance(60f);
            yield return Tap(Key.Tab);
            yield return Frames(12);
            if (!W.Phone.Shown) { Issue("phone.tab2", "눈에 띔", "Tab 으로 휴대폰 안 열림"); yield break; }
            for (int k = 0; k < 12 && W.Phone.Tab != 7; k++) { yield return Tap(Key.RightArrow); yield return Frames(3); }
            yield return Shot("휴대폰_레슨칸", Snip(W.Phone.Body, 400));
            yield return Tap(Key.Enter);
            yield return Frames(10);
            PickPolicy = title => title.StartsWith("레슨 — 누구에게") ? coachIdx : title.Contains("결제") ? payIdx : title.Contains("—") && title.Contains(":") && !title.Contains("언제") ? fixIdx : 0;
            yield return Advance(60f);
            PickPolicy = null;
        }

        IEnumerator B_Lessons()
        {
            int day = NextDay("월");
            yield return Load(day, 8 * 60, SceneLoader.Zone1, m => { Unlocks(m); m.L.SetWallet(900000); m.G.Fatigue = 0; });
            var coaches = M.Sh.Coaches.Where(c => M.CoachOpen(c)).ToList();
            L($"열린 코치 {coaches.Count}: {string.Join(", ", coaches.Select(c => c.Name))}");
            if (coaches.Count < 5) Issue("lesson.open", "QA", $"코치 {coaches.Count}명만 열림");
            for (int i = 0; i < coaches.Count; i++)
            {
                int n0 = M.S.Lessons.Count;
                yield return BookByPhone(i, i == 0 ? 1 : 0);
                if (M.S.Lessons.Count == n0) Issue($"lesson.book.{coaches[i].Id}", "눈에 띔", $"{coaches[i].Name} 휴대폰 예약이 안 됨");
            }
            yield return Tap(Key.Tab); yield return Frames(10);
            for (int k = 0; k < 12 && W.Phone.Tab != 7; k++) { yield return Tap(Key.RightArrow); yield return Frames(3); }
            yield return Shot("레슨_예약5_뒤", Snip(W.Phone.Body, 500));
            yield return Tap(Key.Escape); yield return Frames(10);
            // 차례로 출석: 그 날 그 시각 25분 전 → 정류장 → 마을버스 → 체육관
            foreach (var bk in M.S.Lessons.Where(b => b.State == 0).OrderBy(b => M.AbsOf(b.Day, b.At)).ToList())
            {
                var c = M.Sh.Coach(bk.Coach);
                if (Ld.Place != SceneLoader.Zone1) { var ex = Ld.Stage?.GetComponentsInChildren<StageDoor>(true).FirstOrDefault(x => x.Target == SceneLoader.Zone1); yield return UseDoor(ex, "나가기"); yield return Advance(120f); }
                GameClock.SetDate(GameClock.Year, bk.Day / 100, bk.Day % 100, bk.At - 26);
                M.G.Fatigue = 0;
                yield return Frames(20);
                yield return Advance(120f);
                yield return UseDoor(W.FindDoor(SceneLoader.Gym), "정류장(레슨 " + c.Name + ")");
                PickPolicy = title => { if (title.Contains("무엇을 할까")) W.BotSkill = 0.9f; return 0; };
                yield return WaitFor(() => W.Busy == "레슨", 30f);
                if (W.Busy != "레슨") { Issue($"lesson.start.{c.Id}", "눈에 띔", $"{c.Name} 레슨 시각에 체육관인데 레슨이 안 시작 ({GameClock.TimeText}, 예약 {GameClock.Hm(bk.At)})"); yield return Shot("레슨_안시작_" + c.Id); }
                yield return Advance(400f);
                W.BotSkill = 0f; PickPolicy = null;
                yield return Shot($"레슨_{c.Id}_끝", W.LastLessonLine);
                var exit = Ld.Stage != null ? Ld.Stage.GetComponentsInChildren<StageDoor>(true).FirstOrDefault(x => x.Target == SceneLoader.Zone1) : null;
                if (exit != null) { yield return UseDoor(exit, "체육관 나가기"); yield return Advance(120f); }
            }
        }

        IEnumerator B_NoShow()
        {
            int day = NextDay("화");
            yield return Load(day, 9 * 60, SceneLoader.Zone1, m => { Unlocks(m); m.L.SetWallet(300000); });
            string why = W.BookNext("gojun");
            var bk = M.S.Lessons.LastOrDefault(b => b.State == 0);
            if (bk == null) { Issue("noshow.book", "QA", "노쇼 시험 예약 실패 " + why); yield break; }
            int w0 = M.L.Wallet;
            L($"노쇼 예약 {bk.Day} {GameClock.Hm(bk.At)} 고준");
            GameClock.SetDate(GameClock.Year, bk.Day / 100, bk.Day % 100, bk.At - 70);
            yield return Frames(30);
            yield return Advance(60f);
            yield return Shot("노쇼_60분전_문자");
            GameClock.SetDate(GameClock.Year, bk.Day / 100, bk.Day % 100, bk.At + 25);
            yield return Frames(30);
            yield return Advance(60f);
            yield return Shot("노쇼_뒤", $"노쇼 {M.Count("noshow")} · 신뢰 {M.Rel("gojun")}");
            yield return Tap(Key.Tab); yield return Frames(12);
            yield return Shot("노쇼_뒤_문자", Snip(W.Phone.Body, 400));
            yield return Tap(Key.Escape); yield return Frames(10);
            if (M.Count("noshow") < 1) Issue("noshow.none", "눈에 띔", "예약 20분 넘게 안 갔는데 노쇼로 안 셈");
        }

        // ───────────────────────── 가게
        IEnumerator B_Shops()
        {
            int sun = NextDay("일");
            yield return Load(sun, 12 * 60 + 10, "Conv", m => { Unlocks(m); m.L.SetWallet(200000); m.S.Full = 0; m.G.Fatigue = 40; });
            yield return Shot("편의점_계산대_표시", W.MarkerPrompt("shop_conv"));
            PickPolicy = t => t.Contains("하루편의점") ? 1 : t.Contains("바로") || t.Length < 12 ? 0 : 0;
            yield return UseMarker("shop_conv");
            yield return Advance(60f);
            yield return Shot("편의점_산뒤", M.Status);
            // 국밥집 직원가
            int wk = NextDay("월화수목금");
            yield return Load(wk, 16 * 60 + 35, "Gukbap", m => { Unlocks(m); m.L.SetWallet(200000); m.S.Full = 0; m.G.Fatigue = 50; });
            yield return Shot("국밥집_차림표_표시", W.MarkerPrompt("shop_gukbap"));
            PickPolicy = t => 0;
            yield return UseMarker("shop_gukbap");
            yield return Advance(80f);
            yield return Shot("국밥집_먹은뒤", M.Status);
            // Zone1 판매창 3(약국·삼계탕·장어)
            foreach (var f in new[] { "shop_pharmacy", "shop_samgye", "shop_eel" })
            {
                var pm0 = PlaceMarker.All.FirstOrDefault(x => x != null && x.Func == f);
                Vector3 at = pm0 != null ? pm0.transform.position + pm0.transform.forward * 3f : W.Def.Balance.Point("alley").Pos;
                yield return Load(sun, 13 * 60, SceneLoader.Zone1, m => { Unlocks(m); m.L.SetWallet(200000); m.S.Full = 0; m.G.Fatigue = 50; }, true, at);
                var pm = Marker(f);
                if (pm == null) { Issue("shop.none." + f, "눈에 띔", $"판매창 {f} 없음"); continue; }
                yield return WalkTo(pm.transform.position + pm.transform.forward * 4f, 0.6f, 6f, false);
                yield return Shot($"{f}_앞", W.MarkerPrompt(f));
                yield return UseMarker(f);
                yield return Advance(90f);
                yield return Shot($"{f}_뒤", M.Status);
            }
            PickPolicy = null;
        }

        IEnumerator B_ShopEdge()
        {
            int sun = NextDay("일");
            yield return Load(sun, 12 * 60 + 10, "Conv", m => { Unlocks(m); m.L.SetWallet(0); m.S.Full = 0; });
            PickPolicy = t => 0;
            yield return UseMarker("shop_conv");
            yield return Advance(60f);
            yield return Shot("돈0_편의점", M.Status);
            // 레슨 예약도 돈 0
            yield return BookByPhone(0, 0);
            yield return Shot("돈0_레슨예약", M.Status);
            // 배부름 한도 — 삼각김밥 바로 먹기 다섯 번
            M.L.SetWallet(50000);
            for (int i = 0; i < 6; i++)
            {
                int f0 = M.S.Full;
                PickPolicy = t => 0;
                yield return UseMarker("shop_conv");
                yield return Advance(60f);
                L($"배부름 {f0} → {M.S.Full} · 지갑 {M.L.Wallet} · 피로 {M.G.Fatigue}");
                yield return Shot($"배부름_{i + 1}", $"배부름 {M.S.Full}/{M.Sh.FullCap}");
                yield return Frames(80);
            }
            PickPolicy = null;
        }

        IEnumerator B_Phone()
        {
            int wk = NextDay("수");
            yield return Load(wk, 15 * 60, SceneLoader.Zone1, m => { Unlocks(m); m.L.SetWallet(120000); m.L.Items.Add("에너지바"); m.L.Items.Add("바나나우유"); }, true, W.Def.Balance.Point("alley").Pos);
            yield return Tap(Key.Tab); yield return Frames(12);
            for (int t = 0; t < PhoneUi.Tabs.Length; t++)
            {
                yield return Frames(8);
                yield return Shot($"휴대폰_{W.Phone.Tab}_{PhoneUi.Tabs[W.Phone.Tab]}", Snip(W.Phone.Body, 300));
                if (W.Phone.Tab == 8) break;
                yield return Tap(Key.RightArrow);
            }
            yield return Tap(Key.Enter);
            yield return Frames(10);
            PickPolicy = t => 0;
            yield return Advance(60f);
            PickPolicy = null;
            yield return Shot("가방_먹은뒤", W.LastMealLine);
            // ← 로 거꾸로
            yield return Tap(Key.Tab); yield return Frames(12);
            yield return Tap(Key.LeftArrow); yield return Frames(6);
            yield return Shot("휴대폰_왼쪽으로", PhoneUi.Tabs[W.Phone.Tab]);
            yield return Tap(Key.Escape); yield return Frames(10);
        }

        // ───────────────────────── 주간 계획(손으로)
        IEnumerator B_Planner()
        {
            int sun = NextDay("일");
            if (sun > 426) sun = NextDay("일", GameClock.KeyPlus(baseDay, -7));
            yield return Load(sun, 22 * 60 + 20, "Home", m => { Unlocks(m); m.AddCount("plan.day." + sun, -m.Count("plan.day." + sun)); });
            yield return UseMarker("sleep");
            yield return WaitFor(() => ChoiceShown, 4f);
            if (ChoiceShown) yield return Choose(0);
            yield return WaitFor(() => W.PlannerView.Shown, 20f);
            if (!W.PlannerView.Shown) { Issue("planner.none", "눈에 띔", "일요일 밤 잠인데 주간 계획이 안 뜸"); yield return Advance(200f); yield break; }
            yield return Frames(20);
            yield return Shot("주간계획_처음");
            var pv = W.PlannerView;
            for (int k = 0; k < 16 && pv.Row > 0; k++) { yield return Tap(Key.UpArrow); yield return Frames(2); }
            yield return Tap(Key.DownArrow); yield return Frames(3);
            yield return Tap(Key.RightArrow); yield return Frames(3);
            yield return Shot("주간계획_칸고름");
            yield return Tap(Key.Enter); yield return Frames(4);
            yield return Shot("주간계획_칸바꿈");
            yield return Tap(Key.Enter); yield return Frames(4);
            yield return Tap(Key.Escape); yield return Frames(6);
            if (!pv.Shown) Issue("planner.esc", "사소", "주간 계획에서 Esc 를 누르니 계획이 닫힘(직접 하기로)");
            else yield return PlannerChoose(0);
            yield return WaitFor(() => pv.InMontage, 30f);
            if (pv.InMontage) { yield return Frames(30); yield return Shot("몽타주_중간"); }
            yield return WaitFor(() => !pv.InMontage, 60f);
            yield return Advance(400f);
            yield return Shot("맡긴뒤", M.Status);
        }

        // ───────────────────────── 시비(손 놓고 지기)
        IEnumerator B_Street()
        {
            int wk = NextDay("월화수목금");
            var pk = W.Def.Balance.Point("parking");
            yield return Load(wk, 17 * 60, SceneLoader.Zone1, m => { Unlocks(m); m.Unlock("street_repeat"); }, true, pk.Pos + new Vector3(0f, 0.5f, 14f));
            yield return Shot("주차장_가기전");
            yield return WalkTo(pk.Pos, 1.5f, 15f, false, "주차장");
            QuietStuck = true;
            var enc = FindAnyObjectByType<Encounter>();
            yield return WaitFor(() => enc != null && enc.State != Encounter.Phase.Armed, 12f);
            yield return Shot("시비_시작", enc != null ? enc.State.ToString() : "인카운터 없음");
            if (enc == null || enc.State == Encounter.Phase.Armed) { Issue("street.none", "눈에 띔", "시비가 열렸는데 주차장에 들어가도 안 걸림"); QuietStuck = false; yield break; }
            RecStart("시비_손놓기", 10f);
            yield return WaitFor(() => enc.State == Encounter.Phase.Lost || enc.State == Encounter.Phase.Cleared, 200f);
            yield return Shot("시비_결과", enc.State.ToString());
            QuietStuck = false;
            yield return Frames(60);
            yield return Advance(120f);
            yield return Shot("시비_뒤", $"{M.S.StreetWins}승 {M.S.StreetLosses}패 · {M.Status}");
            // 다시 걸리나(다시 들어가기)
            var away = pk.Pos + new Vector3(0f, 0f, 16f);
            yield return WalkTo(away, 2f, 10f, true);
            yield return WalkTo(pk.Pos, 1.5f, 10f, true);
            yield return Frames(120);
            yield return Shot("시비_다시", enc.State.ToString());
            yield return Advance(240f);
            // 첫 시비 카드(C1-24)를 사람 손으로 — 늦은 바탕이 있으면
            if (labLate != null)
            {
                var keep = labBase; labBase = labLate; int bd = baseDay; baseDay = labLate.Month * 100 + labLate.Day;
                int d = NextDay("월화수목금");
                yield return Load(d, 16 * 60 + 20, "School", m => { }, false);
                labBase = keep; baseDay = bd;
                yield return Advance(500f);
                yield return Shot("C1-24_뒤", $"{M.S.StreetWins}승 {M.S.StreetLosses}패 · C1-24 {M.CardDone("C1-24")}");
            }
        }

        // ───────────────────────── 저장·불러오기
        IEnumerator SaveTitleContinue(string what)
        {
            var gu = GameUi.Instance;
            if (gu == null || !gu.MenuVisible) { yield return Tap(Key.Escape); yield return Frames(15); }   // QA 2차: 알바 판 Esc 로 이미 열렸으면 또 누르지 않음(누르면 닫힘)
            if (gu == null || !gu.MenuVisible) { Issue("savetitle.nomenu." + what, "사소", $"{what}: Esc 로 일시정지가 안 열림"); yield return Shot(what + "_Esc_안열림"); yield break; }
            yield return Shot(what + "_일시정지", string.Join(" / ", Enumerable.Range(0, gu.ItemCount).Select(gu.Label)));
            int si = Enumerable.Range(0, gu.ItemCount).FirstOrDefault(i => gu.Label(i).Contains("타이틀"));
            for (int k = 0; k < 8 && gu.Selected != si; k++) { yield return Tap(Key.DownArrow); yield return Frames(3); }
            yield return Tap(Key.Enter);
            TitleMenu title = null;
            for (int i = 0; i < 1800 && title == null; i++) { title = FindAnyObjectByType<TitleMenu>(); yield return null; }
            if (title == null) { Issue("savetitle.notitle." + what, "진행 불가", $"{what}: '저장하고 타이틀로' 뒤 타이틀이 안 뜸"); yield break; }
            yield return Frames(30);
            yield return Shot(what + "_타이틀", title.Label(1));
            if (!title.Label(1).StartsWith("이어하기")) Issue("savetitle.nocont." + what, "진행 불가", $"{what}: 타이틀에 '이어하기'가 없음 ({title.Label(1)})");
            for (int k = 0; k < 4 && title.Selected != 1; k++) { yield return Tap(Key.DownArrow); yield return Frames(3); }
            yield return Tap(Key.Enter);
            for (int i = 0; i < 3600 && (W == null || !W.Active); i++) yield return null;
            storyCache = null; hooked = false;
            yield return Frames(10);
            W.BotSkill = 0f;
            yield return Advance(240f);
            yield return Shot(what + "_이어하기뒤", M?.Status);
        }

        IEnumerator B_SaveCard()
        {
            int sun = NextDay("일");
            yield return Load(sun, 10 * 60, "Home", m => Unlocks(m));
            int w0 = M.L.Wallet, h0 = M.S.Household;
            yield return UseMarker("haneul");
            yield return WaitFor(() => Story().Board.Shown, 5f);
            yield return Shot("하늘이_카드중");
            yield return SaveTitleContinue("카드중");
            if (M != null) L($"카드 중 저장 → 이어하기: 지갑 {w0} → {M.L.Wallet} · 생활비 {h0} → {M.S.Household} · {GameClock.TimeText} · {Ld.Place}");
            if (M != null && (M.L.Wallet != w0 || M.S.Household != h0)) Issue("savecard.money", "눈에 띔", $"카드 중 저장·불러오기 뒤 돈이 바뀜 지갑 {w0}→{M.L.Wallet} 생활비 {h0}→{M.S.Household}");
        }

        IEnumerator B_SaveJob()
        {
            int wk = NextDay("월화수목금");
            yield return Load(wk, 16 * 60 + 50, "Gukbap", m => { Unlocks(m); ResetJob(m, "job.gukbap"); });
            int pay0 = M.L.Wallet + M.S.Household;
            yield return UseMarker("job_serving");
            yield return WaitFor(() => ChoiceShown, 4f);
            if (ChoiceShown) { JobPolicy = "none"; yield return Choose(0); }
            yield return WaitFor(() => W.Job.Shown, 10f);
            yield return Frames(60);
            yield return Tap(Key.Escape); yield return Frames(15);
            yield return Shot("알바중_Esc", $"일시정지 {GameState.Paused}");
            if (GameState.Paused) { L("알바 중 Esc → 일시정지 열림"); yield return SaveTitleContinue("알바중"); int pay1 = M.L.Wallet + M.S.Household; L($"알바 중 저장 → 이어하기 돈 {pay0} → {pay1}"); yield break; }
            yield return Tap(Key.Tab); yield return Frames(15);
            yield return Shot("알바중_Tab", $"휴대폰 {W.Phone.Shown}");
            if (W.Phone.Shown) { Issue("job.phone", "눈에 띔", "미니게임 중 Tab 으로 휴대폰이 열림"); yield return Tap(Key.Escape); }
            yield return Advance(200f);
        }

        IEnumerator B_Night()
        {
            int wk = NextDay("월화수목금");
            var cd = W.FindDoor("Conv");
            yield return Load(wk, 21 * 60 + 56, SceneLoader.Zone1, m => { Unlocks(m); }, true, cd != null ? cd.ReturnPos : (Vector3?)null);
            yield return Shot("21시56분_HUD");
            yield return Secs(2.0f);
            yield return Shot("22시_HUD", GameClock.TimeText);
            yield return UseDoor(cd, "편의점(밤)");
            yield return Advance(120f);
            yield return Shot("밤_입고_표시", W.MarkerPrompt("job_stock"));
            yield return UseMarker("job_stock");
            yield return Frames(30);
            yield return Shot("밤_입고_누름");
            yield return Advance(60f);
            yield return UseMarker("job_counter");
            yield return Frames(30);
            yield return Shot("밤_계산대_누름", W.MarkerPrompt("job_counter"));
            yield return Advance(60f);
            // 새벽 5시 배달 표시(05:30 열림 전)
            yield return Load(NextDay("월화수목금", GameClock.KeyPlus(wk, 1)), 5 * 60 + 2, "Gukbap", m => Unlocks(m));
            yield return Shot("새벽5시_배달_표시", W.MarkerPrompt("job_delivery"));
            yield return UseMarker("job_delivery");
            yield return Frames(30);
            yield return Shot("새벽5시_배달_누름");
            yield return Advance(300f);
            // 국밥집 23시
            yield return Load(wk, 23 * 60, "Gukbap", m => Unlocks(m));
            yield return Shot("23시_서빙_표시", W.MarkerPrompt("job_serving"));
            yield return UseMarker("job_serving");
            yield return Frames(30);
            yield return Shot("23시_서빙_누름");
            yield return Advance(60f);
            // 새벽 1:58 바깥 → 2시 강제 귀가
            yield return Load(wk, 1 * 60 + 57, SceneLoader.Zone1, m => Unlocks(m), true, W.Def.Balance.Point("alley").Pos);
            yield return Shot("새벽1시57분");
            float t = 0f;
            while (t < 30f && W.Free) { t += Time.unscaledDeltaTime; yield return null; }
            yield return Frames(10);
            yield return Shot("새벽2시_강제귀가");
            yield return Advance(300f);
            yield return Shot("강제귀가_뒤", M.Status);
        }

        IEnumerator B_MonthEnd()
        {
            int day = 331;
            if (baseDay > 331) day = 430;
            yield return Load(day, 23 * 60 + 25, "Home", m => { Unlocks(m); m.S.Household = 0; m.L.SetWallet(0); });
            int def0 = M.S.Deficits;
            yield return Shot("말일_23시25분", M.Status);
            yield return SaveTitleContinue("말일");
            if (M == null) yield break;
            yield return Shot("말일_이어하기", M.Status);
            yield return UseMarker("sleep");
            yield return Advance(400f);
            yield return Shot("말일_잔뒤", $"결손 {def0} → {M.S.Deficits} · {M.Status}");
            if (M.S.Deficits - def0 > 1) Issue("monthend.double", "눈에 띔", $"말일 결손이 두 번 셈 {def0} → {M.S.Deficits}");
            if (M.S.Deficits == def0) Issue("monthend.none", "눈에 띔", "생활비 0·지갑 0 인데 말일 결손이 안 셈");
            yield return Tap(Key.Tab); yield return Frames(12);
            for (int k = 0; k < 6 && W.Phone.Tab != 3; k++) { yield return Tap(Key.RightArrow); yield return Frames(3); }
            yield return Shot("말일_가계부칸", Snip(W.Phone.Body, 400));
            yield return Tap(Key.Escape); yield return Frames(10);
        }

        // ───────────────────────── 경계
        IEnumerator B_Fatigue()
        {
            int wk = NextDay("화목");
            yield return Load(wk, 16 * 60 + 40, "Gukbap", m => { Unlocks(m); m.G.Fatigue = 96; });
            yield return Shot("피로96_HUD");
            yield return UseMarker("job_serving");
            yield return Frames(30);
            yield return Shot("피로96_서빙누름");
            if (ChoiceShown) { JobPolicy = "none"; yield return Choose(0); }
            yield return Advance(240f);
            yield return Shot("피로96_뒤", M.Status);
            yield return Load(wk, 19 * 60, SceneLoader.Gym, m => { Unlocks(m); m.G.Fatigue = 88; });
            var ok = Gm != null && Gm.StartDrill("D01");
            yield return Frames(30);
            yield return Shot("피로88_드릴", $"시작 {ok}");
            yield return Advance(120f);
            yield return Load(wk, 23 * 60 + 50, SceneLoader.Zone1, m => { Unlocks(m); m.G.Fatigue = 100; }, true, W.Def.Balance.Point("alley").Pos);
            yield return Shot("피로100_밤");
            yield return Hold(3f, Key.W);
            yield return Shot("피로100_걸음");
            yield return Advance(120f);
        }

        IEnumerator B_OddSleep()
        {
            int wk = NextDay("수");
            yield return Load(wk, 13 * 60, "Home", m => Unlocks(m));
            yield return Shot("낮1시_잠표시", W.MarkerPrompt("sleep"));
            yield return UseMarker("sleep");
            yield return WaitFor(() => ChoiceShown, 4f);
            if (ChoiceShown) yield return Shot("낮1시_잠묻기", Story().Talk.Choice.Label(0));
            yield return Advance(300f);
            yield return Shot("낮1시_잔뒤", M.Status);
            yield return Load(wk, 1 * 60 + 30, "Home", m => Unlocks(m));
            yield return Shot("새벽1시반_잠표시", W.MarkerPrompt("sleep"));
            yield return UseMarker("sleep");
            yield return Advance(300f);
            yield return Shot("새벽1시반_잔뒤", M.Status);
            yield return Load(wk, 5 * 60 + 35, "Home", m => Unlocks(m));
            yield return Shot("새벽5시35분_잠표시", W.MarkerPrompt("sleep"));
            yield return UseMarker("sleep");
            yield return Advance(300f);
            yield return Shot("새벽5시35분_잔뒤", M.Status);
        }

        static readonly Vector3 BMin = new Vector3(-274f, 15f, 70f), BMax = new Vector3(-130f, 62f, 174f);

        IEnumerator B_Edges()
        {
            int wk = NextDay("수");
            yield return Load(wk, 14 * 60, SceneLoader.Zone1, m => Unlocks(m), true, W.Def.Balance.Point("alley").Pos);
            var pts = new (Vector3 p, Vector3 dir, string n)[]
            {
                (new Vector3(-133f, 0f, 120f), Vector3.right, "동쪽끝"), (new Vector3(-200f, 0f, 73f), Vector3.back, "남쪽끝"),
                (new Vector3(-271f, 0f, 120f), Vector3.left, "서쪽끝"), (new Vector3(-150f, 0f, 171f), Vector3.forward, "북쪽끝"),
                (new Vector3(-135f, 0f, 160f), new Vector3(1f, 0f, 1f).normalized, "북동모서리"), (new Vector3(-268f, 0f, 76f), new Vector3(-1f, 0f, -1f).normalized, "남서모서리"),
            };
            foreach (var (p0, dir, n) in pts)
            {
                var p = p0; p.y = 80f;
                if (!Physics.Raycast(p, Vector3.down, out var h, 90f, Layers.Solid, QueryTriggerInteraction.Ignore)) { L($"맵 끝 {n}: 땅 없음 {p0}"); continue; }
                p = h.point + Vector3.up * 0.05f;
                Mo.Teleport(p, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg);
                Ld.Cam?.SnapBehind();
                yield return Frames(20);
                PinOff();
                float t = 0f;
                while (t < 3.5f) { Mo.SetMoveInput(dir, 1f, true); t += Time.unscaledDeltaTime; yield return null; }
                Mo.ClearMoveInput();
                PinOn();
                yield return Frames(20);
                var q = Mo.Position;
                bool inside = q.x >= BMin.x - 1f && q.x <= BMax.x + 1f && q.z >= BMin.z - 1f && q.z <= BMax.z + 1f;
                yield return Shot("맵끝_" + n, $"{p:F1} → {q:F1} · 안 {inside} · 접지 {Mo.Grounded}");
                if (!inside) Issue("edge.out." + n, "눈에 띔", $"맵 {n}에서 경계 밖으로 나감 {q:F1}");
                if (q.y < p.y - 6f) Issue("edge.fall." + n, "눈에 띔", $"맵 {n}에서 아래로 떨어짐 {p.y:F1} → {q.y:F1}");
                if (!W.Free) yield return Advance(120f);
            }
            // 좁은 골목 벽에 붙어 카메라
            var al = W.Def.Balance.Point("alley").Pos;
            Mo.Teleport(al + Vector3.up * 0.05f, 90f); Ld.Cam?.SnapBehind();
            yield return Frames(20);
            PinOn();
            yield return Hold(2.5f, Key.D);
            yield return Shot("골목_벽붙기_오른쪽");
            yield return Hold(2.5f, Key.A);
            yield return Shot("골목_벽붙기_왼쪽");
            yield return Hold(1.5f, Key.S);
            yield return Shot("골목_뒤로_카메라");
        }

        static readonly (Vector3 a, Vector3 b, string n)[] Stairs =
        {
            (new Vector3(-190.4f, 30.16f, 84.4f), new Vector3(-180.52f, 27.2f, 86.85f), "상가뒤골목계단"),
            (new Vector3(-173.9f, 27.96f, 105.0f), new Vector3(-173.8f, 27.0f, 107.0f), "골목계단내리막"),
            (new Vector3(-161.4f, 28.2f, 127.2f), new Vector3(-160.9f, 32.5f, 135.6f), "시우네옆꼭대기계단"),
            (new Vector3(-161.6f, 32.5f, 138.6f), new Vector3(-166.8f, 36.37f, 148.9f), "성곽아래좁은계단"),
            (new Vector3(-199.5f, 33.6f, 119.8f), new Vector3(-197.4f, 36.9f, 129.8f), "와룡공원계단"),
        };

        IEnumerator B_Stairs()
        {
            int wk = NextDay("수");
            yield return Load(wk, 14 * 60, SceneLoader.Zone1, m => Unlocks(m), true, W.Def.Balance.Point("alley").Pos);
            int i = 0;
            foreach (var (a, b, n) in Stairs)
            {
                var dir = (b - a); dir.y = 0f; dir.Normalize();
                var s = a - dir * 2.5f;
                if (Physics.Raycast(s + Vector3.up * 3f, Vector3.down, out var h, 8f, Layers.Solid, QueryTriggerInteraction.Ignore)) s = h.point;
                Mo.Teleport(s + Vector3.up * 0.05f, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg);
                Ld.Cam?.SnapBehind();
                yield return Frames(30);
                var e = b + dir * 2.0f;
                if (i == 2) yield return RecWhile("계단_" + n, 12f, WalkTo(e, 0.8f, 12f, false, "계단 " + n + " 오르기"));
                else yield return WalkTo(e, 0.8f, 12f, false, "계단 " + n + " 오르기");
                yield return Shot($"계단_{n}_끝", $"{Mo.Position:F1} (목표 {e:F1})");
                yield return WalkTo(s, 0.8f, 12f, true, "계단 " + n + " 내려오기");
                yield return Shot($"계단_{n}_내려옴", $"{Mo.Position:F1}");
                if (!W.Free) yield return Advance(120f);
                i++;
            }
        }

        IEnumerator B_DemoEnd()
        {
            // 5/8 밤 — 어버이날(C1-26)을 끝낸 것으로 두고 잠 → 데모 끝 판정 → 데모 끝 카드 · 2장 예고를 Enter 로
            yield return Load(508, 21 * 60 + 30, "Home", m => { Unlocks(m); var c = W.Def.Card("C1-26"); if (c != null) m.MarkCard(c, 1); m.S.DemoState = 0; }, true);
            yield return UseMarker("sleep");
            float t0 = T; int presses = 0; string lastTitle = "";
            while (T - t0 < 120f)
            {
                if (W.ChapterView != null && W.ChapterView.Shown)
                {
                    if (W.ChapterView.Title != lastTitle) { lastTitle = W.ChapterView.Title; presses = 0; yield return Frames(40); yield return Shot("데모끝_" + W.ChapterView.Kind, Snip(W.ChapterView.Body, 300)); }
                    yield return Secs(1.0f);
                    yield return Tap(Key.Enter); presses++;
                    yield return Frames(20);
                    if (W.ChapterView.Shown && W.ChapterView.Title == lastTitle) { L($"데모 끝 카드 '{lastTitle}' — Enter {presses}번째도 그대로"); if (presses == 3) { Issue("demoend.press." + W.ChapterView.Kind, "진행 불가?", $"'{W.ChapterView.Kind}' 카드가 Enter 세 번에도 안 닫힘"); yield return Shot("데모끝_안닫힘"); } }
                    continue;
                }
                if (ChoiceShown) { yield return Choose(0); continue; }
                if (W.Free && M.S.DemoState != 0) break;
                if (!W.Free && Story().Board.Shown) { yield return Secs(1f); yield return Tap(Key.Enter); }
                yield return null;
            }
            yield return Advance(120f);
            yield return Shot("데모끝_뒤", $"DemoState {M.S.DemoState} · {M.Status}");
        }

        IEnumerator B_Spam()
        {
            int wk = NextDay("수");
            var gd = W.FindDoor("Gukbap");
            yield return Load(wk, 15 * 60, SceneLoader.Zone1, m => Unlocks(m), true, gd != null ? gd.ReturnPos : (Vector3?)null);
            int u0 = StageDoor.Uses, t0 = Ld.Transitions;
            yield return WalkTo(gd.transform.position, 0.8f, 8f, false, "국밥집 문");
            for (int k = 0; k < 6; k++) { yield return Tap(Key.E, 2); }
            yield return WaitFor(() => !Ld.Busy, 20f);
            yield return Advance(120f);
            yield return Shot("문연타_뒤", $"문 쓰임 {StageDoor.Uses - u0} · 옮김 {Ld.Transitions - t0} · {Ld.Place}");
            if (Ld.Transitions - t0 > 1) Issue("door.spam", "눈에 띔", $"문 앞 E 연타에 장면이 {Ld.Transitions - t0}번 바뀜");
            // 휴대폰 Tab 연타
            for (int k = 0; k < 7; k++) { yield return Tap(Key.Tab, 2); yield return Frames(2); }
            yield return Frames(20);
            yield return Shot("Tab연타_뒤", $"휴대폰 {W.Phone.Shown}");
            if (W.Phone.Shown) { yield return Tap(Key.Escape); yield return Frames(10); }
            // Esc 연타
            for (int k = 0; k < 5; k++) { yield return Tap(Key.Escape, 2); yield return Frames(2); }
            yield return Frames(20);
            yield return Shot("Esc연타_뒤", $"일시정지 {GameState.Paused}");
            if (GameState.Paused) { yield return Tap(Key.Escape); yield return Frames(10); }
        }
    }
}
