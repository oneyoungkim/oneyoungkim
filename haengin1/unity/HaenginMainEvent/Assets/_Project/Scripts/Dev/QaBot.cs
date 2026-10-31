// 행인1의 메인이벤트 — 플레이 QA 봇(docs/15_QA_챕터1.md): 빌드 실행 인자 -qa [-qaout <폴더>] [-qapart abc] [-qalap 0] (c = QA 2차: A 고친 뒤 사진 · 핵심 카드 장면 녹화 — QaBot.Scenes.cs)  (그래픽 켜고 · -mute · -nocursorlock 같이)
// "자동 테스트가 통과해도 사람이 하면 이상한 것"을 찾는다 — 가상 키보드(Input System 장치)로 사람처럼 눌러 가며 새 게임부터 돈다.
//   사진 = <폴더>/shots/NNN_구역_이름.jpg + shots.tsv(그때 날짜·시각·장소·바쁨·지갑·피로) · 녹화 = <폴더>/rec/<이름>/f_*.jpg(30fps) · 한 바퀴 = <폴더>/lap/f_*.jpg
//   로그 = qa_log.txt(예외·경고·NaN·멈춤·같은 화면·카메라 벽 속·발밑 땅 없음·글꼴에 없는 글자·글자 넘침·화면 밖 글자·값 이상) · 발견 = findings.tsv
// A = 새 게임 사람 손(QaBot.Play.cs) → 남은 데모는 봇(WorldBot)이 돌며 카드마다 사진 · B = 실험실(QaBot.Lab.cs, A 중간 저장을 고쳐 불러옴)
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace Haengin
{
    public sealed partial class QaBot : MonoBehaviour
    {
        static string[] Args => Environment.GetCommandLineArgs();
        static bool Arg(string a) => Args.Any(x => string.Equals(x, a, StringComparison.OrdinalIgnoreCase));
        static string ArgVal(string a, string def)
        {
            var g = Args;
            for (int i = 0; i < g.Length - 1; i++) if (string.Equals(g[i], a, StringComparison.OrdinalIgnoreCase)) return g[i + 1];
            return def;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Application.isEditor || !Arg("-qa")) return;
            var go = new GameObject("QA 봇") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            go.AddComponent<QaBot>();
        }

        // ───────────────────────── 바탕
        string Out;
        StreamWriter logW, shotW;
        float tStart;
        float T => Time.realtimeSinceStartup - tStart;
        string sec = "시작";
        int shotN;
        string lastShot = "";
        Keyboard kb;
        readonly HashSet<Key> held = new HashSet<Key>();
        bool lapOn = true;
        int lapEvery = 12;

        WorldRunner W => WorldRunner.Instance;
        WorldModel M => W != null ? W.Model : null;
        StoryRunner St => W != null ? W.Story : FindAnyObjectByType<StoryRunner>(FindObjectsInactive.Include);
        SceneLoader Ld => W != null ? W.Loader : null;
        PlayerMotor Mo => Ld != null ? Ld.Motor : null;
        GymManager Gm => GymManager.Instance;

        void Awake()
        {
            tStart = Time.realtimeSinceStartup;
            Out = ArgVal("-qaout", Path.Combine(Application.temporaryCachePath, "qa"));
            foreach (var d in new[] { "", "shots", "rec", "lap", "saves" }) Directory.CreateDirectory(Path.Combine(Out, d));
            logW = new StreamWriter(Path.Combine(Out, "qa_log.txt"), false, new UTF8Encoding(false)) { AutoFlush = true };
            shotW = new StreamWriter(Path.Combine(Out, "shots.tsv"), false, new UTF8Encoding(false)) { AutoFlush = true };
            shotW.WriteLine("번호\t파일\t구역\t이름\t날짜시각\t장소\t바쁨\t지갑\t피로\t메모");
            lapOn = ArgVal("-qalap", "1") != "0";
            Application.runInBackground = true;
            try { InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus; } catch (Exception e) { L("입력 배경 설정 실패 " + e.Message); }
            kb = InputSystem.AddDevice<Keyboard>("QA 가상 키보드");
            Application.logMessageReceived += OnLog;
            SaveStore.Dir = Path.Combine(Out, "saves");
            Time.captureDeltaTime = 1f / 60f;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 120;
            NoThrottle();
            L($"QA 봇 시작 · 출력 {Out} · 화면 {Screen.width}x{Screen.height} · 음소거 {MuteArg.On} · 인자 {string.Join(" ", Args.Skip(1))}");
        }

        void OnDestroy() { Application.logMessageReceived -= OnLog; }

        // 창이 뒤에 있으면 Windows 11 전원 조절(EcoQoS)이 이 프로세스를 늦춤 → QA 실행 동안만 이 프로세스의 조절을 끔
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        struct PowerThrottle { public uint Version, ControlMask, StateMask; }
        [System.Runtime.InteropServices.DllImport("kernel32.dll")] static extern bool SetProcessInformation(IntPtr h, int cls, ref PowerThrottle info, int size);
        [System.Runtime.InteropServices.DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
        [System.Runtime.InteropServices.DllImport("kernel32.dll")] static extern bool SetPriorityClass(IntPtr h, uint cls);

        void NoThrottle()
        {
            try
            {
                var st = new PowerThrottle { Version = 1, ControlMask = 0x1 | 0x4, StateMask = 0 };
                bool ok = SetProcessInformation(GetCurrentProcess(), 4, ref st, System.Runtime.InteropServices.Marshal.SizeOf(st));
                L($"전원 조절 끔: {ok}");
            }
            catch (Exception e) { L("전원 조절 끄기 실패 " + e.Message); }
        }

        float fpsT; int fpsFrames;
        void Update()
        {
            fpsFrames++;
            float now = Time.realtimeSinceStartup;
            if (fpsT <= 0f) fpsT = now;
            if (now - fpsT >= 20f) { L($"속도: {fpsFrames / (now - fpsT):F1} fps(실제)"); fpsT = now; fpsFrames = 0; }
        }

        void L(string s)
        {
            try { logW?.WriteLine($"[{T,7:0.0}] [{sec}] {s}"); } catch { }
        }

        static string Safe(string s) => Regex.Replace(s ?? "", @"[\\/:*?""<>|\s]+", "_");
        static string Snip(string s, int n = 60) { s = (s ?? "").Replace("\n", " / "); return s.Length > n ? s.Substring(0, n) + "…" : s; }

        // ───────────────────────── 로그(예외·경고)
        readonly Dictionary<string, int> logCount = new Dictionary<string, int>();
        int nErr, nExc, nWarn;

        void OnLog(string msg, string stack, LogType type)
        {
            if (type == LogType.Log) return;
            string key = type + ":" + (msg.Length > 140 ? msg.Substring(0, 140) : msg);
            logCount.TryGetValue(key, out int n);
            logCount[key] = n + 1;
            if (type == LogType.Warning) { nWarn++; if (n == 0) L("경고: " + Snip(msg, 300)); return; }
            if (type == LogType.Exception) nExc++; else nErr++;
            if (n == 0)
            {
                L($"{type}: {msg}\n{stack}");
                Issue("log." + key, type == LogType.Exception ? "예외" : "오류", $"{type}: {Snip(msg, 200)}", Snip((stack ?? "").Split('\n').FirstOrDefault(), 160));
            }
        }

        // ───────────────────────── 발견
        sealed class Finding { public string Key, Sev, Sec, What, Extra, Shot, When; public int N; }
        readonly Dictionary<string, Finding> found = new Dictionary<string, Finding>();
        readonly List<Finding> order = new List<Finding>();
        string pendingIssueShot;

        void Issue(string key, string sev, string what, string extra = "")
        {
            if (found.TryGetValue(key, out var f)) { f.N++; return; }
            f = new Finding { Key = key, Sev = sev, Sec = sec, What = what, Extra = extra ?? "", N = 1, When = StateLine() };
            found[key] = f; order.Add(f);
            L($"◆ 발견 [{sev}] {what}{(f.Extra.Length > 0 ? " — " + f.Extra : "")}");
            if (pendingIssueShot == null) pendingIssueShot = key;
        }

        void WriteFindings()
        {
            try
            {
                using (var w = new StreamWriter(Path.Combine(Out, "findings.tsv"), false, new UTF8Encoding(false)))
                {
                    w.WriteLine("번호\t심각도\t구역\t내용\t덧붙임\t횟수\t사진\t그때");
                    int i = 0;
                    foreach (var f in order) w.WriteLine($"{++i}\t{f.Sev}\t{f.Sec}\t{f.What}\t{f.Extra}\t{f.N}\t{f.Shot}\t{f.When}");
                }
                using (var w = new StreamWriter(Path.Combine(Out, "log_counts.tsv"), false, new UTF8Encoding(false)))
                    foreach (var kv in logCount.OrderByDescending(x => x.Value)) w.WriteLine($"{kv.Value}\t{kv.Key.Replace('\n', ' ')}");
            }
            catch (Exception e) { L("발견 쓰기 실패 " + e.Message); }
        }

        string StateLine()
        {
            string busy = W != null ? W.Busy : "";
            string place = Ld != null ? Ld.Place : "";
            string money = M != null ? Ledger.Won(M.L.Wallet) : "";
            string fat = M != null ? M.G.Fatigue.ToString() : "";
            return $"{GameClock.DateText} {GameClock.TimeText}\t{place}\t{busy}\t{money}\t{fat}";
        }

        // ───────────────────────── 가상 키보드
        void Push()
        {
            if (kb == null) return;
            kb.MakeCurrent();
            InputSystem.QueueStateEvent(kb, new KeyboardState(held.ToArray()));
        }
        void Down(params Key[] ks) { foreach (var k in ks) held.Add(k); Push(); }
        void Up(params Key[] ks) { foreach (var k in ks) held.Remove(k); Push(); }
        void UpAll() { held.Clear(); Push(); }

        IEnumerator Tap(Key k, int frames = 3)
        {
            Down(k);
            for (int i = 0; i < frames; i++) yield return null;
            Up(k);
            yield return null; yield return null;
        }

        IEnumerator Taps(Key k, int n)
        {
            for (int i = 0; i < n; i++) { yield return Tap(k); yield return null; }
        }

        IEnumerator Hold(float secs, params Key[] ks)
        {
            Down(ks);
            yield return Secs(secs);
            Up(ks);
            yield return null;
        }

        IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
        /// 게임 시간 초(프레임 = 1/60초 고정)
        IEnumerator Secs(float s) { float t = 0f; while (t < s) { t += Time.deltaTime > 0f ? Time.unscaledDeltaTime : 1f / 60f; yield return null; } }

        IEnumerator WaitFor(Func<bool> cond, float secs)
        {
            float t = 0f;
            while (t < secs && !cond()) { t += Time.unscaledDeltaTime; yield return null; }
        }

        // ───────────────────────── 사진·녹화·한 바퀴
        readonly Queue<string> shotQ = new Queue<string>();
        string recDir; int recFrame, recLeft;
        int frameN, lapN;
        bool recAsync;
        /// 평소 한 프레임 시간(녹화가 끝나면 되돌릴 값). A8 봇 구간은 1/30(두 배 빠르게)
        float baseDelta = 1f / 60f;
        RenderTexture smallRt; Texture2D smallTex;
        int screenHash, sameScreenFrames;

        IEnumerator Shot(string name, string note = "") { ShotNow(name, note); yield return null; yield return null; }

        void ShotNow(string name, string note = "")
        {
            shotN++;
            string file = $"{shotN:000}_{Safe(sec)}_{Safe(name)}.jpg";
            if (file.Length > 110) file = file.Substring(0, 106) + ".jpg";
            lastShot = file;
            shotW.WriteLine($"{shotN}\t{file}\t{sec}\t{name}\t{StateLine()}\t{note}");
            try { CheckTexts(file); } catch (Exception e) { L("글자 검사 실패 " + e.Message); }
            shotQ.Enqueue(Path.Combine(Out, "shots", file));
        }

        IEnumerator Rec(string name, float secs, Func<bool> stop = null)
        {
            recDir = Path.Combine(Out, "rec", Safe(name));
            Directory.CreateDirectory(recDir);
            Time.captureDeltaTime = 1f / 30f;
            recFrame = 0; recLeft = Mathf.RoundToInt(secs * 30f);
            L($"녹화 {name} {secs:0.#}초");
            while (recLeft > 0 && (stop == null || !stop())) yield return null;
            recLeft = 0;
            Time.captureDeltaTime = baseDelta;
            L($"녹화 {name} 끝 — {recFrame}장");
        }

        /// 녹화를 켠 채로 다른 일을 함
        IEnumerator RecWhile(string name, float maxSecs, IEnumerator body)
        {
            recDir = Path.Combine(Out, "rec", Safe(name));
            Directory.CreateDirectory(recDir);
            Time.captureDeltaTime = 1f / 30f;
            recFrame = 0; recLeft = Mathf.RoundToInt(maxSecs * 30f);
            L($"녹화 {name} (일 하며, 최대 {maxSecs:0}초)");
            yield return body;
            recLeft = 0;
            Time.captureDeltaTime = baseDelta;
            L($"녹화 {name} 끝 — {recFrame}장");
        }

        // 글자·배경 검사(2026-10-31): 사진마다 실제 화면으로 — text_inv.tsv(보인 글자 전부) · text_bad.tsv(규칙 위반) · text_world.tsv(월드 글자)
        StreamWriter auditInv, auditBad, auditWorld;
        void AuditShot(Texture2D tex, string file)
        {
            try
            {
                if (auditInv == null)
                {
                    auditInv = new StreamWriter(Path.Combine(Out, "text_inv.tsv"), false, new UTF8Encoding(false)) { AutoFlush = true };
                    auditBad = new StreamWriter(Path.Combine(Out, "text_bad.tsv"), false, new UTF8Encoding(false)) { AutoFlush = true };
                    auditWorld = new StreamWriter(Path.Combine(Out, "text_world.tsv"), false, new UTF8Encoding(false)) { AutoFlush = true };
                }
                var items = TextAudit.Ui(tex);
                foreach (var i in items) auditInv.WriteLine($"{sec}\t{file}\t{i.Path}\t{i.Text}\t{i.Size:0}\t{i.PlateA:0.00}\t{i.Plate}\t{i.Contrast:0.0}\t{i.R.xMin:0},{i.R.yMin:0},{i.R.width:0},{i.R.height:0}");
                foreach (var b in TextAudit.Problems(items)) auditBad.WriteLine($"{sec}\t{file}\t{b}");
                foreach (var w in TextAudit.World(Camera.main)) auditWorld.WriteLine($"{sec}\t{file}\t{w.Path}\t{w.Text}\t{(w.Floating ? "공중" : "표면")}\t{(w.Lying ? "누움" : "")}\t{w.Pos.x:0.0},{w.Pos.y:0.0},{w.Pos.z:0.0}");
            }
            catch (Exception e) { L("글자 검사 실패 " + e.Message); }
        }

        IEnumerator EndFrames()
        {
            var eof = new WaitForEndOfFrame();
            while (true)
            {
                yield return eof;
                frameN++;
                if (pendingIssueShot != null && shotQ.Count == 0) { string k = pendingIssueShot; pendingIssueShot = null; ShotNow("발견_" + k); if (found.TryGetValue(k, out var f)) f.Shot = lastShot; }
                bool lap = lapOn && frameN % lapEvery == 0;
                bool rec = recLeft > 0;
                bool hash = frameN % 30 == 0;
                if (shotQ.Count == 0 && !lap && !rec && !hash) continue;
                Texture2D tex = null;
                try { tex = ScreenCapture.CaptureScreenshotAsTexture(); }
                catch (Exception e) { L("캡처 실패 " + e.Message); continue; }
                if (tex == null) continue;
                try
                {
                    while (shotQ.Count > 0) { string shotPath = shotQ.Dequeue(); File.WriteAllBytes(shotPath, tex.EncodeToJPG(88)); AuditShot(tex, Path.GetFileName(shotPath)); }
                    if (rec) { File.WriteAllBytes(Path.Combine(recDir, $"f_{recFrame:00000}.jpg"), RecBytes(tex)); recFrame++; recLeft--; if (recLeft == 0 && recAsync) { recAsync = false; Time.captureDeltaTime = baseDelta; } }
                    if (lap || hash)
                    {
                        var bytes = Small(tex, 960, 540, lap ? 62 : -1);
                        if (lap && bytes != null) File.WriteAllBytes(Path.Combine(Out, "lap", $"f_{lapN++:00000}.jpg"), bytes);
                        if (hash) HashScreen();
                    }
                }
                catch (Exception e) { L("사진 쓰기 실패 " + e.Message); }
                Destroy(tex);
            }
        }

        byte[] Small(Texture2D src, int w, int h, int q)
        {
            if (smallRt == null) { smallRt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB); smallTex = new Texture2D(w, h, TextureFormat.RGB24, false); }
            var prev = RenderTexture.active;
            Graphics.Blit(src, smallRt);
            RenderTexture.active = smallRt;
            smallTex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            smallTex.Apply(false);
            RenderTexture.active = prev;
            return q > 0 ? smallTex.EncodeToJPG(q) : null;
        }

        void HashScreen()
        {
            if (smallTex == null) return;
            var px = smallTex.GetPixels32();
            int h = 17;
            for (int i = 0; i < px.Length; i += 211) { var c = px[i]; h = h * 31 + (c.r >> 3) * 1024 + (c.g >> 3) * 32 + (c.b >> 3); }
            if (h == screenHash) sameScreenFrames += 30; else sameScreenFrames = 0;
            screenHash = h;
        }

        // ───────────────────────── 글자 검사(사진마다)
        static readonly Regex Tag = new Regex("<[^>]+>");

        void CheckTexts(string file)
        {
            var boxes = new List<(Rect r, string path, string txt, Transform top)>();
            foreach (var t in FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None))
            {
                if (t == null || !t.isActiveAndEnabled || string.IsNullOrWhiteSpace(t.text)) continue;
                if (t.canvas == null || !t.canvas.isActiveAndEnabled || t.canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                if (t.color.a < 0.05f || t.alpha < 0.05f) continue;
                bool hidden = false;
                foreach (var cg in t.GetComponentsInParent<CanvasGroup>()) if (cg.alpha < 0.05f) hidden = true;
                if (hidden) continue;
                string raw = Tag.Replace(t.text, "");
                if (t.maxVisibleCharacters < raw.Length) raw = raw.Substring(0, Mathf.Max(0, t.maxVisibleCharacters));
                string where = PathOf(t.transform);
                if (t.font != null)
                {
                    string chk = new string(raw.Where(c => !char.IsWhiteSpace(c) && c != '​').ToArray());
                    if (chk.Length > 0 && !t.font.HasCharacters(chk, out uint[] miss, true, true) && miss != null && miss.Length > 0)
                    {
                        string m = string.Concat(miss.Distinct().Select(u => char.ConvertFromUtf32((int)u)));
                        Issue("glyph." + m, "눈에 띔", $"글꼴에 없는 글자 '{m}' — {where}", Snip(raw));
                    }
                }
                var rt = t.rectTransform;
                var size = rt.rect.size;
                if (size.x > 4f && size.y > 4f && t.overflowMode == TextOverflowModes.Overflow)
                {
                    bool wrap = t.textWrappingMode != TextWrappingModes.NoWrap;
                    float ph = t.preferredHeight, pw = t.preferredWidth;
                    if ((wrap && ph > size.y * 1.10f + 8f) || (!wrap && pw > size.x * 1.08f + 8f))
                        Issue("over." + where, "눈에 띔", $"글자가 칸을 넘침 — {where} (칸 {size.x:0}×{size.y:0}, 글 {pw:0}×{ph:0})", Snip(raw));
                }
                if (t.textInfo == null || t.textInfo.characterCount == 0) continue;
                var b = t.textBounds;
                if (b.size.x <= 0.5f || b.size.y <= 0.5f) continue;
                Vector3 a0 = rt.TransformPoint(b.min), a1 = rt.TransformPoint(b.max);
                var r = Rect.MinMaxRect(Mathf.Min(a0.x, a1.x), Mathf.Min(a0.y, a1.y), Mathf.Max(a0.x, a1.x), Mathf.Max(a0.y, a1.y));
                if (r.xMin < -4f || r.yMin < -4f || r.xMax > Screen.width + 4f || r.yMax > Screen.height + 4f)
                    Issue("off." + where, "눈에 띔", $"글자가 화면 밖으로 — {where} ({r.xMin:0},{r.yMin:0})~({r.xMax:0},{r.yMax:0}) 화면 {Screen.width}×{Screen.height}", Snip(raw));
                boxes.Add((r, where, raw, TopPanel(t.transform)));
            }
            // 서로 다른 판의 글자끼리 겹침
            for (int i = 0; i < boxes.Count; i++)
                for (int j = i + 1; j < boxes.Count; j++)
                {
                    var A = boxes[i]; var B = boxes[j];
                    if (A.top == B.top) continue;
                    float ix = Mathf.Min(A.r.xMax, B.r.xMax) - Mathf.Max(A.r.xMin, B.r.xMin);
                    float iy = Mathf.Min(A.r.yMax, B.r.yMax) - Mathf.Max(A.r.yMin, B.r.yMin);
                    if (ix <= 0f || iy <= 0f) continue;
                    float small = Mathf.Min(A.r.width * A.r.height, B.r.width * B.r.height);
                    if (ix * iy > small * 0.25f)
                        Issue($"overlap.{A.path}|{B.path}", "눈에 띔", $"글자끼리 겹침 — {A.path} 「{Snip(A.txt, 24)}」 ↔ {B.path} 「{Snip(B.txt, 24)}」", file);
                }
        }

        static Transform TopPanel(Transform t)
        {
            var c = t.GetComponentInParent<Canvas>();
            if (c == null) return t.root;
            var cur = t;
            while (cur.parent != null && cur.parent != c.transform) cur = cur.parent;
            return cur;
        }

        static string PathOf(Transform t)
        {
            var parts = new List<string>();
            for (var c = t; c != null && parts.Count < 4; c = c.parent) parts.Insert(0, c.name);
            return string.Join("/", parts);
        }

        // ───────────────────────── 매 프레임 감시
        float camWall, groundMiss, floatT, sigT, sigSame, wallT;
        string sig = "";
        Vector3 lastPos;
        public bool QuietStuck;

        void LateUpdate()
        {
            if (W == null || !W.Active) return;
            float dt = Time.unscaledDeltaTime;
            var mo = Mo;
            if (mo != null)
            {
                var p = mo.Position;
                if (float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z) || float.IsInfinity(p.x)) Issue("nan.pos", "진행 불가", $"시우 위치 NaN/무한 {p}");
                var cam = Camera.main;
                if (cam != null)
                {
                    var cp = cam.transform.position;
                    if (float.IsNaN(cp.x) || float.IsNaN(cp.y) || float.IsNaN(cam.transform.rotation.x)) Issue("nan.cam", "진행 불가", $"카메라 NaN {cp}");
                }
                bool free = W.Free && Ld != null && !Ld.Busy && !(Story() != null && Story().InScene);
                if (free)
                {
                    // 발밑
                    if (!Physics.Raycast(p + Vector3.up * 0.6f, Vector3.down, out var hit, 3.0f, Layers.Solid, QueryTriggerInteraction.Ignore)) groundMiss += dt;
                    else
                    {
                        groundMiss = 0f;
                        float gap = p.y - hit.point.y;
                        if (gap > 0.30f && mo.Grounded) floatT += dt; else floatT = 0f;
                        if (floatT > 0.6f) Issue($"float.{Ld.Place}.{Mathf.RoundToInt(p.x / 3)}.{Mathf.RoundToInt(p.z / 3)}", "눈에 띔", $"시우가 땅 위에 떠 있음 {gap:F2}m ({Ld.Place} {p:F1})");
                    }
                    if (groundMiss > 0.5f) Issue($"noground.{Ld.Place}.{Mathf.RoundToInt(p.x / 3)}.{Mathf.RoundToInt(p.z / 3)}", "눈에 띔", $"발밑에 땅 없음 0.5초+ ({Ld.Place} {p:F1})");
                    // 카메라 벽 속
                    if (cam != null && Physics.CheckSphere(cam.transform.position, 0.08f, Layers.CameraBlock, QueryTriggerInteraction.Ignore)) camWall += dt; else camWall = 0f;
                    if (camWall > 0.5f) Issue($"camwall.{Ld.Place}.{Mathf.RoundToInt(p.x / 4)}.{Mathf.RoundToInt(p.z / 4)}", "눈에 띔", $"카메라가 벽·물체 속 0.5초+ ({Ld.Place} 시우 {p:F1} · 카메라 {cam.transform.position:F1})");
                    // 몸이 벽 속
                    var c0 = p + Vector3.up * 0.45f; var c1 = p + Vector3.up * 1.35f;
                    if (Physics.CheckCapsule(c0, c1, 0.12f, Layers.Mask(Layers.Wall), QueryTriggerInteraction.Ignore)) wallT += dt; else wallT = 0f;
                    if (wallT > 0.5f) Issue($"inwall.{Ld.Place}.{Mathf.RoundToInt(p.x / 3)}.{Mathf.RoundToInt(p.z / 3)}", "눈에 띔", $"시우 몸이 벽 속 ({Ld.Place} {p:F1})");
                }
                else { camWall = 0f; groundMiss = 0f; floatT = 0f; wallT = 0f; }
                if (Ld != null && Ld.Stage != null && Ld.Place != SceneLoader.Zone1 && p.y < Ld.Stage.KillY + 1f) Issue("fall." + Ld.Place, "진행 불가?", $"실내에서 바닥 아래로 {p:F1}");
                lastPos = p;
            }
            // 값
            var m = M;
            if (m != null)
            {
                if (m.L.Wallet < 0) Issue("val.wallet", "눈에 띔", $"지갑이 음수 {m.L.Wallet}");
                if (m.L.Envelope < 0) Issue("val.env", "눈에 띔", $"봉투가 음수 {m.L.Envelope}");
                if (m.G.Fatigue < 0 || m.G.Fatigue > 100) Issue("val.fat", "눈에 띔", $"피로 범위 밖 {m.G.Fatigue}");
                if (m.S.Full > m.Sh.FullCap) Issue("val.full", "사소", $"배부름 한도 넘음 {m.S.Full}/{m.Sh.FullCap}");
                double mm = GameClock.Minutes;
                if (double.IsNaN(mm) || mm < 0 || mm >= 24 * 60) Issue("val.clock", "눈에 띔", $"시계 분 범위 밖 {mm}");
                for (int i = 0; i < m.G.Stats.Length; i++) if (m.G.Stats[i] < 0 || float.IsNaN(m.S.StatFrac[i])) Issue("val.stat" + i, "눈에 띔", $"스탯 {i} 이상 {m.G.Stats[i]} + {m.S.StatFrac[i]}");
            }
            // 멈춤: 같은 상태 + 같은 화면
            sigT += dt;
            if (sigT >= 0.5f)
            {
                sigT = 0f;
                string s = Sig();
                if (s == sig && !W.Free) sigSame += 0.5f; else sigSame = 0f;
                sig = s;
                if (Gm != null && Gm.Busy) sigSame = 0f;     // 드릴은 라운드 내내 같은 자리(멈춤 아님)
                if (sigSame >= 25f && !QuietStuck) { Issue($"stuck.{W.Busy}.{Ld?.Place}", "진행 불가?", $"같은 상태 {sigSame:0}초 — 바쁨 '{W.Busy}' · {Ld?.Place}"); sigSame = -1000f; }
            }
            if (sameScreenFrames >= 60 * 20 && !W.Free && !QuietStuck) { Issue($"samescreen.{W.Busy}", "진행 불가?", $"같은 화면 20초 넘게 — 바쁨 '{W.Busy}'"); sameScreenFrames = -100000; }
            // 전투 상태가 바뀌면 기록 · 자유 이동 중 전투 HUD 가 2초 넘게 켜져 있으면 발견
            var pcb = pcCache != null ? pcCache : (pcCache = FindAnyObjectByType<PlayerCombat>());
            if (pcb != null)
            {
                if (pcb.Active != pcActiveWas) { pcActiveWas = pcb.Active; L($"전투 상태 {(pcb.Active ? "켬" : "끔")} — 바쁨 '{W.Busy}' · 장소 {Ld?.Place} · 드릴 {(Gm != null && Gm.Runner != null ? Gm.Runner.State.ToString() : "-")} · 카드 {W.CurrentCard?.Id}"); }
                if (pcb.Active && W.Free && !(Gm != null && Gm.Busy)) pcFreeT += dt; else pcFreeT = 0f;
                if (pcFreeT > 2f) Issue("combat.free." + Ld?.Place, "눈에 띔", $"자유 이동 중인데 전투 상태(전투 HUD·전투 조작)가 켜져 있음 — {Ld?.Place}");
            }
            BotWatch();
        }
        PlayerCombat pcCache; bool pcActiveWas; float pcFreeT;

        StoryRunner storyCache;
        StoryRunner Story() { if (storyCache == null) storyCache = W != null ? W.Story : null; return storyCache; }

        string Sig()
        {
            var st = Story();
            string board = st != null && st.Board != null && st.Board.Shown ? st.Board.Title : "";
            string sub = st != null && st.Talk != null && st.Talk.Sub != null && st.Talk.Sub.Visible ? st.Talk.Sub.Text : "";
            string job = W.Job != null && W.Job.Shown ? W.Job.PromptText : "";
            var p = Mo != null ? Vector3Int.RoundToInt(Mo.Position * 2f) : Vector3Int.zero;
            string drill = Gm != null && Gm.Runner != null ? Gm.Runner.State.ToString() : "";
            return $"{W.Busy}|{board}|{sub}|{(int)GameClock.Minutes}|{job}|{Ld?.Place}|{p}|{drill}|{held.Count}";
        }

        // ───────────────────────── 구역 실행(예외·시간 초과에도 다음 구역으로)
        IEnumerator Section(string name, Func<IEnumerator> body, float timeout)
        {
            string only = ArgVal("-qaonly", "");
            if (only.Length > 0 && name != "타이틀" && !only.Split(',').Any(o => name.StartsWith(o))) yield break;
            string prev = sec;
            sec = name;
            L($"──────── {name} 시작 · {StateLine().Replace('\t', ' ')}");
            float t0 = T;
            var stack = new Stack<IEnumerator>();
            IEnumerator first = null;
            try { first = body(); } catch (Exception e) { L($"QA 구역 만들기 실패 {e}"); }
            if (first != null) stack.Push(first);
            bool timedOut = false;
            while (stack.Count > 0)
            {
                if (T - t0 > timeout) { timedOut = true; break; }
                var top = stack.Peek();
                bool moved; object cur = null;
                try { moved = top.MoveNext(); if (moved) cur = top.Current; }
                catch (Exception e) { L($"QA 코드 예외 [{name}]: {e}"); Issue("qa.exc." + name + "." + e.GetType().Name, "QA", $"QA 봇 예외 — {e.GetType().Name}: {Snip(e.Message, 120)}", Snip(e.StackTrace, 160)); break; }
                if (!moved) { stack.Pop(); continue; }
                if (cur is IEnumerator sub) { stack.Push(sub); continue; }
                yield return cur;
            }
            UpAll();
            PinOn();
            if (W != null && W.Active) { W.BotSkill = humanSkill; }
            Time.captureDeltaTime = baseDelta; recLeft = 0; recAsync = false;
            if (timedOut)
            {
                Issue("timeout." + name, "진행 불가?", $"구역 '{name}' {timeout:0}초 넘음 — 바쁨 '{W?.Busy}' · {Ld?.Place}");
                yield return Shot("시간초과");
            }
            L($"──────── {name} 끝 {T - t0:F0}초 · {StateLine().Replace('\t', ' ')}");
            WriteFindings();
            sec = prev;
        }

        float humanSkill = 0f;

        // ───────────────────────── 시작
        IEnumerator Start()
        {
            StartCoroutine(EndFrames());
            string parts = ArgVal("-qapart", "ab");
            yield return Section("타이틀", TitleWait, 120f);
            if (parts.Contains("a")) yield return PartA();
            if (parts.Contains("b")) yield return PartB();
            if (parts.Contains("c")) yield return PartC();      // QA 2차: A 항목 고친 뒤 사진 · 핵심 카드 실제 장면 녹화
            if (parts.Contains("l")) yield return PartL();      // 레슨 카드 카메라: 코치 6명 실제 흐름 · 실제 화면(오버레이 그대로) · N13
            Summary();
            yield return Frames(30);
            Application.Quit();
        }

        void Summary()
        {
            WriteFindings();
            var bySev = order.GroupBy(f => f.Sev).Select(g => $"{g.Key} {g.Count()}").ToArray();
            L($"QA 끝 · 실제 {T:0}초 · 사진 {shotN} · 한 바퀴 {lapN}장 · 예외 {nExc} · 오류 {nErr} · 경고 {nWarn} · 발견 {order.Count} ({string.Join(", ", bySev)})");
            Debug.Log($"[QA] 끝 · 사진 {shotN} · 예외 {nExc} · 오류 {nErr} · 경고 {nWarn} · 발견 {order.Count} — {Out}");
        }

        IEnumerator TitleWait()
        {
            TitleMenu title = null;
            for (int i = 0; i < 3600 && title == null; i++) { title = FindAnyObjectByType<TitleMenu>(); yield return null; }
            if (title == null) { Issue("title.none", "진행 불가", "타이틀 화면이 안 뜸"); yield break; }
            yield return Frames(40);
            yield return Shot("타이틀", $"항목 {title.Count}: {string.Join(" / ", Enumerable.Range(0, title.Count).Select(title.Label))}");
        }

        // ───────────────────────── 사람 손 도우미
        PInput pin;
        bool pinOff;
        void PinOff() { if (pin == null) pin = FindAnyObjectByType<PInput>(); if (pin != null) { pin.enabled = false; pinOff = true; } }
        void PinOn() { if (pin == null) pin = FindAnyObjectByType<PInput>(); if (pin != null && pinOff) pin.enabled = true; pinOff = false; Mo?.ClearMoveInput(); }

        /// 그 자리로 걸어감(직선 · 막히면 옆으로 비켜 보고, 그래도 막히면 순간 이동 + 기록)
        IEnumerator WalkTo(Vector3 target, float radius, float timeout, bool run = true, string what = "")
        {
            var mo = Mo;
            if (mo == null) yield break;
            PinOff();
            float t = 0f, stuck = 0f; Vector3 last = mo.Position; int side = 0;
            while (t < timeout)
            {
                var d = target - mo.Position; d.y = 0f;
                if (d.magnitude <= radius) break;
                if (!W.Free && !(Story() != null && Story().InScene)) break;     // 카드가 떴으면 멈춤
                var dir = d.normalized;
                if (side != 0) dir = Quaternion.Euler(0f, side * 70f, 0f) * dir;
                mo.SetMoveInput(dir, 1f, run);
                t += Time.unscaledDeltaTime;
                if ((mo.Position - last).sqrMagnitude < 0.0009f) stuck += Time.unscaledDeltaTime; else { stuck = 0f; if (side != 0 && UnityEngine.Random.value < 0.02f) side = 0; }
                last = mo.Position;
                if (stuck > 1.0f && side == 0) { side = UnityEngine.Random.value < 0.5f ? 1 : -1; stuck = 0f; }
                else if (stuck > 1.0f && side != 0) { side = -side; stuck = 0f; }
                if (t > timeout * 0.75f) break;
                yield return null;
            }
            mo.ClearMoveInput();
            var rest = target - mo.Position; rest.y = 0f;
            if (rest.magnitude > radius + 0.3f)
            {
                L($"걸어가기 막힘 {what} — 남은 {rest.magnitude:F1}m · {mo.Position:F1} → {target:F1}");
                if (!string.IsNullOrEmpty(what)) Issue($"walk.{what}", "사소?", $"{what} 까지 직선으로 못 감(남은 {rest.magnitude:F1}m) — 순간 이동", $"{mo.Position:F1}");
                if (Physics.Raycast(target + Vector3.up * 1.5f, Vector3.down, out var h, 4f, Layers.Solid, QueryTriggerInteraction.Ignore)) target.y = h.point.y;
                mo.Teleport(target + Vector3.up * 0.03f, Mathf.Atan2(rest.x, rest.z) * Mathf.Rad2Deg);
                Ld?.Cam?.SnapBehind();
                yield return Frames(3);
            }
            PinOn();
            yield return Frames(2);
        }

        /// 문 앞(돌아올 자리)으로 옮겨 서서 걸어 들어가 × · E
        IEnumerator UseDoor(StageDoor d, string label)
        {
            if (d == null) { Issue("door.none." + label, "진행 불가?", $"문 없음 {label}"); yield break; }
            var mo = Mo;
            var it = d.GetComponent<Interactable>();
            var p = d.transform.position;
            if (Ld.Place == SceneLoader.Zone1 && d.Target != SceneLoader.Zone1)
            {
                var back = d.ReturnPos; if (back == Vector3.zero) back = p + Vector3.back * 2f;
                var away = back + (back - p).normalized * 2.5f; away.y = back.y + 0.05f;
                mo.Teleport(away, Mathf.Atan2(p.x - away.x, p.z - away.z) * Mathf.Rad2Deg);
                Ld.Cam?.SnapBehind();
                yield return Frames(4);
            }
            yield return WalkTo(p, Mathf.Max(0.6f, (it != null ? it.Radius : 1.5f) - 0.5f), 10f, false, "문 " + label);
            yield return Frames(6);
            string place0 = Ld.Place;
            yield return Tap(Key.E);
            yield return WaitFor(() => Ld.Busy || Ld.Place != place0 || !W.Free, 1.5f);
            yield return WaitFor(() => !Ld.Busy, 20f);
            yield return Frames(20);
        }

        PlaceMarker Marker(string func) => PlaceMarker.All.FirstOrDefault(m => m != null && m.Func == func && m.isActiveAndEnabled);

        /// 표시 자리 앞으로 걸어가 × · E
        IEnumerator UseMarker(string func)
        {
            var pm = Marker(func);
            if (pm == null) { Issue("marker.none." + func, "진행 불가?", $"표시 자리 '{func}' 없음 ({Ld?.Place})"); yield break; }
            var it = pm.GetComponent<Interactable>();
            yield return WalkTo(pm.transform.position, 0.7f, 12f, false, "표시 " + func);
            yield return Frames(8);
            yield return Tap(Key.E);
            yield return Frames(6);
        }

        bool ChoiceShown => Story() != null && Story().Talk != null && Story().Talk.Choice != null && Story().Talk.Choice.Visible;

        /// 선택지(ChoiceUi) — ↓ 로 옮겨 Enter
        IEnumerator Choose(int idx)
        {
            var ch = Story().Talk.Choice;
            yield return Frames(10);
            for (int k = 0; k < 6 && ch.Selected != idx; k++) { yield return Tap(ch.Selected < idx ? Key.DownArrow : Key.UpArrow); yield return Frames(3); }
            if (ch.Selected != idx) Issue("choice.nav", "눈에 띔", $"선택지 ↓↑ 로 {idx}번에 못 감(지금 {ch.Selected}/{ch.Count})");
            yield return Tap(Key.Enter);
            yield return Frames(4);
        }

        /// 목록(PickUi) — ↓ 로 옮겨 Enter (idx < 0 = Esc)
        IEnumerator Pick(int idx)
        {
            var pk = W.Picker;
            yield return Frames(10);
            if (idx < 0) { yield return Tap(Key.Escape); yield return Frames(4); yield break; }
            for (int k = 0; k < 12 && pk.Selected != idx; k++) { yield return Tap(pk.Selected < idx ? Key.DownArrow : Key.UpArrow); yield return Frames(3); }
            if (pk.Selected != idx) Issue("pick.nav", "눈에 띔", $"목록 ↓↑ 로 {idx}번에 못 감(지금 {pk.Selected}/{pk.Count})");
            yield return Tap(Key.Enter);
            yield return Frames(4);
        }

        // ───────────────────────── 넘기기(사람처럼 화면을 보고 누름) — 자유 이동이 될 때까지
        /// 선택지 고르기(보기 → 번호). null = 0번
        public Func<string[], int> ChoicePolicy;
        /// 목록 고르기(제목 → 번호, −1 = 그만). null = 0번
        public Func<string, int> PickPolicy;
        /// 주간 계획: 0 맡기기 · 1 직접
        public int PlannerPolicy = 1;
        /// 미니게임: "none" 손 놓기 · "mash" 아무거나 누르기 · "bot" 봇(W.BotSkill 을 미리)
        public string JobPolicy = "mash";
        /// 이야기 줄 사진: N줄마다
        public int LineShotEvery = 4;
        SceneDef curScene;
        bool hooked;
        int bikeLeg = -1;

        void Hook()
        {
            if (hooked || Story() == null) return;
            hooked = true;
            Story().SceneStarted += s => { curScene = s; bikeLeg = s.InZone1 && s.Kind == SceneKind.Move ? BikeCourse.LegOf(s.Id) : -1; L($"장면 {s.Id} 「{s.Title}」 {s.Kind}"); };
            Story().SceneEnded += s => { if (bikeLeg >= 0 && Story().Bot == StoryRunner.BotMode.Read && !botMode) Story().Bot = StoryRunner.BotMode.None; curScene = null; bikeLeg = -1; };
        }

        bool botMode;

        float periodicAt, blankLogAt;
        readonly Dictionary<string, int> pressCount = new Dictionary<string, int>();

        /// 넘기기 누름 — 같은 화면에서 두 번째 누름이면 '누름이 안 먹음' 후보로
        IEnumerator PressOn(string key, string kind)
        {
            pressCount.TryGetValue(key, out int n);
            pressCount[key] = n + 1;
            if (n == 1) { L($"같은 화면에서 다시 누름(첫 누름이 안 먹었나) — {kind} · {Snip(key, 60)}"); }
            if (n == 3) Issue($"press.ignored.{kind}", "눈에 띔?", $"{kind} 화면에서 Enter 를 네 번 눌러도 안 넘어감 — {Snip(key, 60)}");
            yield return Tap(Key.Enter);
        }

        IEnumerator Advance(float timeout, Func<bool> done = null)
        {
            Hook();
            float t0 = T, since = 0f, nothing = 0f;
            int freeFrames = 0, lines = 0;
            string seen = "";
            float seenAt = 0f;
            while (T - t0 < timeout)
            {
                if (done != null && done()) yield break;
                if (W == null || !W.Active) { yield return null; continue; }
                bool free = W.Free && (Ld == null || !Ld.Busy) && !(Story() != null && Story().InScene);
                if (free && done == null) { if (++freeFrames > 10) yield break; yield return null; continue; }
                freeFrames = 0;
                string kind, key; UiNow(out kind, out key);
                if (key != seen) { seen = key; seenAt = T; since = 0f; }
                else since += Time.unscaledDeltaTime;
                if (kind != "") nothing = 0f; else nothing += Time.unscaledDeltaTime;
                switch (kind)
                {
                    case "chapter":
                        if (since == 0f) yield return Shot("장카드_" + W.ChapterView.Kind, Snip(W.ChapterView.Title + " · " + W.ChapterView.Body, 200));
                        if (since > 1.8f) { yield return PressOn(seen, kind); seen = ""; yield return Frames(10); }
                        break;
                    case "pause":
                        if (since == 0f) yield return Shot("일시정지_뜻밖");
                        yield return Tap(Key.Escape); yield return Frames(10);
                        break;
                    case "phone":
                        yield return Tap(Key.Escape); yield return Frames(10);
                        break;
                    case "planner":
                        if (since == 0f) yield return Shot("주간계획", W.PlannerView.Shown ? "" : "");
                        if (since > 1.2f) { yield return PlannerChoose(PlannerPolicy); seen = ""; }
                        break;
                    case "montage":
                        if (since == 0f) yield return Shot("몽타주_시작");
                        if (since > 2.5f && since - Time.unscaledDeltaTime <= 2.5f) yield return Shot("몽타주_끝쪽", Snip(W.PlannerView.MontageText, 240));
                        break;
                    case "epcard":
                        if (since == 0f) yield return Shot("회차카드");
                        break;
                    case "pick":
                        if (since == 0f) yield return Shot("목록_" + Snip(W.Picker.Title, 20), string.Join(" / ", Enumerable.Range(0, W.Picker.Count).Select(W.Picker.Row)));
                        if (since > 0.8f) { int pi = PickPolicy != null ? PickPolicy(W.Picker.Title) : 0; yield return Pick(pi); seen = ""; }
                        break;
                    case "choice":
                        {
                            var ch = Story().Talk.Choice;
                            if (since == 0f) yield return Shot("선택지", string.Join(" / ", Enumerable.Range(0, ch.Count).Select(ch.Label)));
                            if (since > 0.9f) { int ci = ChoicePolicy != null ? ChoicePolicy(Enumerable.Range(0, ch.Count).Select(ch.Label).ToArray()) : 0; yield return Choose(Mathf.Clamp(ci, 0, ch.Count - 1)); seen = ""; }
                            break;
                        }
                    case "board":
                        if (since == 0f)
                        {
                            var bd = Story().Board;
                            yield return Shot("카드_" + Snip(bd.Title, 24), Snip(bd.Body, 240));
                            CheckCardTime(bd.Title + " " + bd.Body);
                            if (bikeLeg >= 0 && !botMode) { Story().Bot = StoryRunner.BotMode.Read; L($"자전거 다리 {bikeLeg} — 코스 봇이 몬다(사람 대신)"); }
                        }
                        if (since > 1.2f && (bikeLeg < 0 || botMode)) { yield return PressOn(seen, kind); seen = ""; yield return Frames(6); }
                        break;
                    case "line":
                        if (since == 0f)
                        {
                            lines++;
                            var cl = Story().Talk.Current;
                            string who = cl != null ? cl.Who : "", txt = cl != null ? cl.Display : "";
                            if (lines % LineShotEvery == 1) yield return Shot("대사_" + Snip(who, 8), Snip(who + ": " + txt, 160));
                            CheckCardTime(txt);
                        }
                        if (since > 0.7f) { yield return PressOn(seen, kind); seen = ""; yield return Frames(4); }
                        break;
                    case "job":
                        yield return JobPlay();
                        seen = "";
                        break;
                    case "braid":
                        if (since == 0f) yield return Shot("땋기");
                        if (UnityEngine.Random.value < 0.08f) yield return Tap(UnityEngine.Random.value < 0.5f ? Key.J : Key.K);
                        break;
                    case "drillcard":
                        if (since == 0f) yield return Shot("드릴결과_" + Gm.Card.GradeText);
                        if (since > 1.2f) { yield return PressOn(seen, kind); seen = ""; yield return Frames(10); }
                        break;
                    case "drillmenu":
                        if (since == 0f) yield return Shot("드릴메뉴");
                        if (since > 1.0f) { yield return Tap(Key.Escape); seen = ""; yield return Frames(10); }
                        break;
                    case "ledger":
                        if (since == 0f) yield return Shot("가계부표");
                        if (since > 1.2f) { yield return PressOn(seen, kind); seen = ""; yield return Frames(10); }
                        break;
                    case "defeat":
                        if (since == 0f) yield return Shot("패배화면");
                        if (since > 2.0f) { yield return PressOn(seen, kind); seen = ""; yield return Frames(10); }
                        break;
                    case "stagedialog":
                        if (since == 0f) yield return Shot("대화창");
                        if (since > 1.0f) { yield return PressOn(seen, kind); seen = ""; yield return Frames(6); }
                        break;
                    case "move":
                        yield return MoveStep();
                        break;
                    case "cut":
                    case "bike":
                        if (since == 0f || T >= periodicAt) { periodicAt = T + (kind == "bike" ? 6f : 4f); yield return Shot(kind == "cut" ? "컷신_" + Story().Cut.Name : "자전거_" + (curScene != null ? curScene.Id : W.Busy)); }
                        break;
                    default:
                        if (nothing > 5f && T >= blankLogAt)
                        {
                            blankLogAt = T + 5f;
                            L($"보이는 것 없이 바쁨 {nothing:0}초 — '{W.Busy}' · 장면 {curScene?.Id} · 장소 {Ld?.Place} · 로더 {Ld?.Busy}");
                            if (nothing > 12f) { yield return Shot("빈화면_바쁨"); Issue($"busy.blank.{W.Busy}.{curScene?.Id}", "눈에 띔?", $"아무 안내 없이 {nothing:0}초 바쁨 — '{W.Busy}' · 장면 {curScene?.Id}"); yield return Tap(Key.Enter); nothing = 0f; }
                        }
                        break;
                }
                yield return null;
            }
            Issue($"advance.timeout.{W?.Busy}", "진행 불가?", $"넘기기 {timeout:0}초 안에 자유 이동이 안 됨 — 바쁨 '{W?.Busy}' · 장면 {curScene?.Id} · {Ld?.Place}");
            yield return Shot("넘기기_시간초과");
        }

        void UiNow(out string kind, out string key)
        {
            kind = ""; key = "";
            var st = Story();
            if (GameUi.Instance != null && GameUi.Instance.MenuVisible) { kind = "pause"; key = "pause"; return; }
            if (W.Phone != null && W.Phone.Shown) { kind = "phone"; key = "phone" + W.Phone.Tab; return; }
            if (W.ChapterView != null && W.ChapterView.Shown) { kind = "chapter"; key = "ch:" + W.ChapterView.Title + W.ChapterView.Kind; return; }
            if (W.PlannerView != null && W.PlannerView.Shown) { bool mont = W.PlannerView.InMontage; kind = mont ? "montage" : "planner"; key = kind; return; }
            if (W.Picker != null && W.Picker.Shown) { kind = "pick"; key = "pk:" + W.Picker.Title; return; }
            if (W.Job != null && W.Job.Shown) { kind = "job"; key = "job:" + W.Job.Mode; return; }
            if (ChoiceShown) { kind = "choice"; key = "c:" + st.Talk.Choice.Label(0) + st.Talk.Choice.Count; return; }
            if (st != null && st.Braid != null && st.Braid.Shown) { kind = "braid"; key = "braid"; return; }
            if (Gm != null && Gm.Card != null && Gm.Card.Visible) { kind = "drillcard"; key = "dc:" + Gm.Card.GradeText; return; }
            if (Gm != null && Gm.Menu != null && (Gm.Menu.Open || Gm.Menu.BoardOpen) && (Gm.Runner == null || Gm.Runner.State == DrillRunner.Phase.Idle)) { kind = "drillmenu"; key = "dm"; return; }
            if (StageHud.Instance != null && StageHud.Instance.DefeatShown) { kind = "defeat"; key = "defeat"; return; }
            if (StageHud.Instance != null && StageHud.Instance.DialogShown) { kind = "stagedialog"; key = "sd"; return; }
            if (st != null && st.Cut != null && st.Cut.Playing) { kind = "cut"; key = "cut:" + st.Cut.Name; return; }
            if (BikeCourse.Current != null && BikeCourse.Current.Riding && !(st != null && st.Board != null && st.Board.Shown)) { kind = "bike"; key = "bike"; return; }
            if (st != null && st.Board != null && st.Board.Shown) { kind = "board"; key = "b:" + st.Board.Title + "|" + st.Board.Body.Length; return; }
            if (st != null && st.Talk != null && st.Talk.Playing && ((st.Talk.Sub != null && st.Talk.Sub.Visible) || (st.Talk.Inner != null && st.Talk.Inner.Visible))) { kind = "line"; key = "l:" + (st.Talk.Current != null ? st.Talk.Current.Display : "") + st.Talk.Log.Count; return; }
            if (st != null && st.Card != null && st.Card.Shown) { kind = "epcard"; key = "ep"; return; }
            if (st != null && st.LedgerUi != null && st.LedgerUi.TableShown && !st.InScene) { kind = "ledger"; key = "ledger" + st.LedgerUi.RowsShown; return; }
            if (st != null && st.InScene && curScene != null && curScene.Kind == SceneKind.Move && bikeLeg < 0 && RouteGuide.Current != null && !RouteGuide.Current.Finished) { kind = "move"; key = "mv:" + RouteGuide.Current.Index; return; }
        }

        float moveStuck; Vector3 moveLast; int moveSide;
        IEnumerator MoveStep()
        {
            var g = RouteGuide.Current; var mo = Mo;
            if (g == null || mo == null || g.CurrentTarget == null) yield break;
            if (!pinOff) PinOff();
            var d = g.CurrentTarget.Pos - mo.Position; d.y = 0f;
            var dir = d.normalized;
            if (moveSide != 0) dir = Quaternion.Euler(0f, moveSide * 70f, 0f) * dir;
            mo.SetMoveInput(dir, 1f, d.magnitude > 6f);
            if ((mo.Position - moveLast).sqrMagnitude < 0.0009f) moveStuck += Time.unscaledDeltaTime; else { moveStuck = 0f; if (moveSide != 0 && UnityEngine.Random.value < 0.03f) moveSide = 0; }
            moveLast = mo.Position;
            if (moveStuck > 1.2f) { moveSide = moveSide == 0 ? 1 : -moveSide; moveStuck = moveSide == 1 ? 0f : moveStuck; }
            if (moveStuck > 4f)
            {
                Issue($"move.stuck.{curScene?.Id}.{g.Index}", "사소?", $"이야기 이동 장면 {curScene?.Id} 목표 「{g.CurrentTarget.Label}」 까지 직선으로 막힘 — 순간 이동", $"{mo.Position:F1}");
                mo.Teleport(g.CurrentTarget.Pos + Vector3.up * 0.05f, mo.Yaw); Ld?.Cam?.SnapBehind(); moveStuck = 0f; moveSide = 0;
            }
            yield break;
        }

        IEnumerator PlannerChoose(int pick)
        {
            var pv = W.PlannerView;
            // ↓ 로 맨 아래 단추 줄까지, → 로 '직접 하기'
            for (int k = 0; k < 18 && pv.Row < PlannerUi.Rows; k++) { yield return Tap(Key.DownArrow); yield return Frames(2); }
            if (pick == 1) { yield return Tap(Key.RightArrow); yield return Frames(3); }
            yield return Shot(pick == 0 ? "주간계획_맡기기" : "주간계획_직접");
            yield return Tap(Key.Enter);
            yield return Frames(10);
        }

        /// 미니게임 한 판(정책대로) — 처음·중간·끝 사진
        IEnumerator JobPlay()
        {
            var j = W.Job;
            string mode = j.Mode;
            yield return Frames(20);
            yield return Shot($"알바_{mode}_시작", j.PromptText);
            float t = 0f; int shots = 0;
            while (j.Shown && t < 120f)
            {
                t += Time.unscaledDeltaTime;
                if (JobPolicy == "mash" && W.BotSkill <= 0f && UnityEngine.Random.value < 0.06f)
                {
                    var keys = new[] { Key.LeftArrow, Key.UpArrow, Key.RightArrow, Key.DownArrow, Key.Enter, Key.Space };
                    yield return Tap(keys[UnityEngine.Random.Range(0, keys.Length)]);
                }
                if (t > 4f + shots * 7f && shots < 2) { shots++; yield return Shot($"알바_{mode}_중간{shots}", j.PromptText); }
                yield return null;
            }
            if (j.Shown) Issue($"job.long.{mode}", "진행 불가?", $"미니게임 {mode} 이 120초 넘게 안 끝남");
            L($"미니게임 {mode} 끝 — 점수 {j.LastScore:F2} · 문제 {j.Prompts} · 맞힘 {j.Hits} · {t:0.0}초");
            yield return Frames(4);
            yield return Shot($"알바_{mode}_끝", $"점수 {j.LastScore:F2}");
        }

        static readonly (string word, int from, int to)[] TimeWords = { ("새벽", 3 * 60, 7 * 60), ("아침", 5 * 60, 11 * 60), ("점심", 11 * 60, 15 * 60), ("저녁", 16 * 60, 22 * 60), ("밤", 18 * 60, 30 * 60), ("오후", 12 * 60, 19 * 60) };

        /// 카드·대사 글의 때 말(새벽·아침·밤…)이 지금 시각과 안 맞으면 후보로 남김(사람이 사진으로 다시 봄)
        void CheckCardTime(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            int now = (int)GameClock.Minutes;
            foreach (var (w, a, b) in TimeWords)
            {
                int i = text.IndexOf(w, StringComparison.Ordinal);
                if (i < 0) continue;
                // '밤' 이 다른 낱말 안(밤새·밤톨…)이어도 일단 후보
                bool ok = (now >= a && now < b) || (now + 24 * 60 >= a && now + 24 * 60 < b);
                if (!ok) { L($"때 말 후보: '{w}' — 지금 {GameClock.TimeText} · 「{Snip(text, 90)}」 ({lastShot})"); timeWordHits.Add($"{w}\t{GameClock.DateText} {GameClock.TimeText}\t{lastShot}\t{Snip(text, 120)}"); }
            }
        }
        readonly List<string> timeWordHits = new List<string>();

        // ───────────────────────── 봇이 돌 때 사진(카드마다)
        bool watchBot;
        string watchBoard = "", watchChapter = "";
        float watchPeriodic;
        bool watchJob;

        void BotWatch()
        {
            if (!watchBot || W == null) return;
            SnapLab();
            var st = Story();
            if (st != null && st.Board != null && st.Board.Shown)
            {
                string k = st.Board.Title + "|" + st.Board.Body.Length;
                if (k != watchBoard) { watchBoard = k; ShotNow("카드_" + Snip(st.Board.Title, 24), Snip(st.Board.Body, 240)); CheckCardTime(st.Board.Title + " " + st.Board.Body); }
            }
            else watchBoard = "";
            if (W.ChapterView != null && W.ChapterView.Shown) { string k = W.ChapterView.Title; if (k != watchChapter) { watchChapter = k; ShotNow("장카드_" + W.ChapterView.Kind, Snip(W.ChapterView.Body, 240)); } }
            else watchChapter = "";
            if (W.Job != null && W.Job.Shown) { if (!watchJob) { watchJob = true; ShotNow("알바_" + W.Job.Mode); } } else watchJob = false;
            watchPeriodic += Time.unscaledDeltaTime;
            if (watchPeriodic > 25f && W.Free) { watchPeriodic = 0f; ShotNow("자유_주기"); }
        }
    }
}
