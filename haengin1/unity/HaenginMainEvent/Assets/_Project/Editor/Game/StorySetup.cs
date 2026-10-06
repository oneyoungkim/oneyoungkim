// 행인1의 메인이벤트 — M3 이야기 설정(docs/09_M3_버티컬슬라이스_설계.md 6-1 0~5단계). '데이터 → 에디터 스크립트 → 에셋·장면' 패턴.
// batchmode:
//   -executeMethod Haengin.EditorGame.StorySetup.Build      글꼴 3종·UiFonts · Story.asset(m3_story.json) · Dialogue.asset(dlg/ep*.tsv) · 조명 프리셋 4종(없을 때만) ·
//                                                          자리 무대 6곳(St_*.unity) · 컷신(타임라인, 없을 때만)·CutLib · 카메라 프리셋 프리팹 · InkMode 렌더러 기능 ·
//                                                          Title.unity · Zone1 다시 만들기(이야기 루트·빛 자리·창문 불이 BeforeSave → M1Setup.AttachRig 끝에서 붙음) · 빌드 목록
//   -executeMethod Haengin.EditorGame.StorySetup.Data       데이터 에셋만(Story·Dialogue) — 장면은 그대로
//   -recut                                                   컷신 타임라인을 지우고 다시 만든다(기본은 없을 때만 — 손으로 고친 컷 보존)
// 무대·타이틀 장면은 저장 뒤 StableIds 로 fileID 를 경로 기준으로 바꿔 같은 입력이면 같은 파일(06 14장).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Haengin.EditorTools;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

namespace Haengin.EditorGame
{
    public static class StorySetup
    {
        const string Tag = "[StorySetup]";
        public const string Root = "Assets/_Project";
        public const string JsonPath = Root + "/Data/Story/m3_story.json";
        public const string DlgDir = Root + "/Data/Story/dlg";
        public const string CutDir = Root + "/Data/Story/cuts";
        public const string StoryPath = Root + "/Settings/Story.asset";
        public const string DialoguePath = Root + "/Settings/Dialogue.asset";
        public const string FontsPath = Root + "/Settings/UiFonts.asset";
        public const string CutLibPath = Root + "/Settings/CutLib.asset";
        public const string TitleScene = Root + "/Scenes/Title.unity";
        public const string CamPrefabDir = Root + "/Prefabs/Cam";
        public const string StageMatDir = Root + "/Materials/Stage";
        public const string LightMatPath = Root + "/Materials/Zone1/M_Z1_window_lit.mat";
        public static string PresetPath(DayPart p) => $"{Root}/Settings/Light_{p}.asset";
        public static readonly string[] StageKeys = { "Gukbap", "Home", "Conv", "School", "Site", "Flash" };
        static readonly string[] Renderers = { "Assets/Settings/PC_Renderer.asset", "Assets/Settings/Mobile_Renderer.asset" };

        static readonly List<string> Notes = new List<string>();

        // ───────────────────────── 진입점
        [MenuItem("Haengin/M3 이야기 설정 전부")]
        public static void Build() => Run(() =>
        {
            Fonts();
            Data();
            Stages();
            Cuts();
            CamPrefabs();
            InkFeature();
            Title();
            Zone1Builder.Rebuild();      // BeforeSave → M1Setup.AttachRig → AddStoryToZone1
            M1Setup.SetBuildScenes();
        });

        [MenuItem("Haengin/M3 이야기 데이터만")]
        public static void DataOnly() => Run(() => { Fonts(); Data(); });

        static void Run(Action body)
        {
            int code = 0;
            Notes.Clear();
            var t0 = DateTime.Now;
            try { body(); AssetDatabase.SaveAssets(); }
            catch (Exception e) { Debug.LogError($"{Tag} 실패: {e}"); code = 1; }
            foreach (var n in Notes) Debug.Log($"{Tag}   {n}");
            Debug.Log($"{Tag} 끝: {(code == 0 ? "성공" : "실패")} · {(DateTime.Now - t0).TotalSeconds:F1}초");
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        static bool Arg(string name) => Environment.GetCommandLineArgs().Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));

        // ───────────────────────── 글꼴
        public static UiFonts Fonts()
        {
            var body = KoreanFont.Ensure(Notes);
            var brush = KoreanFont.EnsureFont(KoreanFont.BrushTtf, KoreanFont.BrushAsset, "KR_Brush_SDF", Notes);
            var hand = File.Exists(KoreanFont.HandTtf) ? KoreanFont.EnsureFont(KoreanFont.HandTtf, KoreanFont.HandAsset, "KR_Hand_SDF", Notes) : null;
            if (hand == null) Notes.Add($"손글씨 글꼴 없음: {KoreanFont.HandTtf} — 자리만(본문 글꼴로 대신)");
            var f = AssetDatabase.LoadAssetAtPath<UiFonts>(FontsPath);
            if (f == null) { f = ScriptableObject.CreateInstance<UiFonts>(); AssetDatabase.CreateAsset(f, FontsPath); }
            f.Body = body; f.Brush = brush; f.Hand = hand;
            // 붓·손글씨 글꼴에 없는 기호(〈〉 등)는 본문 글꼴로 대신 그린다(TMP 대체 글꼴)
            foreach (var fa in new[] { brush, hand })
            {
                if (fa == null || body == null) continue;
                if (fa.fallbackFontAssetTable == null) fa.fallbackFontAssetTable = new List<TMP_FontAsset>();
                if (!fa.fallbackFontAssetTable.Contains(body)) { fa.fallbackFontAssetTable.Add(body); EditorUtility.SetDirty(fa); }
            }
            EditorUtility.SetDirty(f);
            foreach (var fa in new[] { body, brush, hand }) if (fa != null) KoreanFont.ResetDynamic(fa);
            Notes.Add($"글꼴: 본문 {body?.name} · 붓 {brush?.name} · 손글씨 {(hand != null ? hand.name : "없음")} → {FontsPath}");
            return f;
        }

        // ───────────────────────── 이야기·대사 데이터
        static double Num(object o, double d = 0) => o is double x ? x : o is long l ? l : o is int i ? i : d;
        static string Str(object o, string d = "") => o as string ?? d;
        static Dictionary<string, object> Obj(object o) => o as Dictionary<string, object> ?? new Dictionary<string, object>();
        static List<object> Arr(object o) => o as List<object> ?? new List<object>();
        static object Get(Dictionary<string, object> d, string k) => d.TryGetValue(k, out var v) ? v : null;

        public static void Data()
        {
            var j = Obj(MiniJson.Parse(File.ReadAllText(JsonPath)));
            var def = AssetDatabase.LoadAssetAtPath<StoryDef>(StoryPath);
            if (def == null) { def = ScriptableObject.CreateInstance<StoryDef>(); AssetDatabase.CreateAsset(def, StoryPath); }
            def.Version = (int)Num(Get(j, "version"), 1);
            def.Episodes = Arr(Get(j, "episodes")).Select(o =>
            {
                var e = Obj(o);
                int no = (int)Num(Get(e, "no"));
                return new EpisodeDef { No = no, Title = Str(Get(e, "title")), Dates = Str(Get(e, "dates")), Ledger = Str(Get(e, "ledger")), First = $"{no}-0" };
            }).ToArray();
            def.Names = Arr(Get(j, "names")).Select(o =>
            {
                var e = Obj(o);
                return new NameEntry
                {
                    Slot = (int)Num(Get(e, "slot")), Name = Str(Get(e, "name")), Caller = Str(Get(e, "caller")), Scene = Str(Get(e, "scene")), Note = Str(Get(e, "note")),
                    Tone = Str(Get(e, "tone")) switch { "warm" => NameTone.Warm, "mock" => NameTone.Mock, "locked" => NameTone.Locked, "bonus" => NameTone.Bonus, "empty" => NameTone.Empty, _ => NameTone.Grey },
                };
            }).ToArray();
            def.LedgerRows = Arr(Get(j, "ledger_reveal")).Select(o => Str(o)).ToArray();
            var quest = Obj(Get(j, "quest"));
            def.QuestHead = Str(Get(quest, "head"));
            def.QuestMemo = Arr(Get(quest, "items")).Select(o => Str(o)).ToArray();
            def.Scenes = Arr(Get(j, "scenes")).Select(Scene).ToArray();
            def.Test = Arr(Get(j, "test")).Select(Scene).ToArray();
            Validate(def);
            EditorUtility.SetDirty(def);

            var book = AssetDatabase.LoadAssetAtPath<DlgBook>(DialoguePath);
            if (book == null) { book = ScriptableObject.CreateInstance<DlgBook>(); AssetDatabase.CreateAsset(book, DialoguePath); }
            var lines = new List<DlgLine>();
            foreach (var path in Directory.GetFiles(DlgDir, "ep*.tsv").OrderBy(p => p, StringComparer.Ordinal))
                lines.AddRange(ReadTsv(path));
            book.Lines = lines.ToArray();
            EditorUtility.SetDirty(book);
            AssetDatabase.SaveAssets();
            int scenes = def.Scenes.Count(s => s.Kind != SceneKind.Card);
            Notes.Add($"이야기: 회차 {def.Episodes.Length} · 장면 {scenes} + 카드 {def.Scenes.Length - scenes} · 시험 장면 {def.Test.Length} · 도감 칸 {def.Names.Length} · 가계부 표 {def.LedgerRows.Length}줄 → {StoryPath}");
            Notes.Add($"대사: {lines.Count}줄 · 장면 {lines.Select(l => l.Scene).Distinct().Count()}개({string.Join(",", lines.Select(l => l.Scene).Distinct())}) → {DialoguePath}");
        }

        static SceneDef Scene(object o)
        {
            var e = Obj(o);
            var date = Str(Get(e, "date"), "03-03").Split('-');
            var time = Str(Get(e, "time"), "00:00").Split(':');
            var s = new SceneDef
            {
                Id = Str(Get(e, "id")),
                Ep = (int)Num(Get(e, "ep")),
                Month = int.Parse(date[0]), Day = int.Parse(date[1]),
                Hour = int.Parse(time[0]), Minute = int.Parse(time[1]),
                Place = Str(Get(e, "place"), "Zone1"),
                Spawn = Str(Get(e, "spawn")),
                Title = Str(Get(e, "title")),
                Summary = Str(Get(e, "summary")),
                Dlg = Str(Get(e, "dlg")),
                Len = (float)Num(Get(e, "len"), 3),
                Clock = Get(e, "clock") is bool b && b,
                Yaw = (float)Num(Get(e, "yaw")),
                Kind = Str(Get(e, "kind")) switch { "card" => SceneKind.Card, "move" => SceneKind.Move, "mini" => SceneKind.Mini, "fight" => SceneKind.Fight, "montage" => SceneKind.Montage, "ui" => SceneKind.Ui, _ => SceneKind.Cut },
                Light = Str(Get(e, "light"), "day") switch { "dawn" => DayPart.Dawn, "dusk" => DayPart.Dusk, "night" => DayPart.Night, _ => DayPart.Day },
            };
            var pos = Arr(Get(e, "pos"));
            if (pos.Count == 3) { s.HasPos = true; s.Pos = new Vector3((float)Num(pos[0]), (float)Num(pos[1]), (float)Num(pos[2])); }
            s.Targets = Arr(Get(e, "targets")).Select(t =>
            {
                var d = Obj(t); var p = Arr(Get(d, "pos"));
                return new TargetDef { Label = Str(Get(d, "label")), Pos = new Vector3((float)Num(p[0]), (float)Num(p[1]), (float)Num(p[2])), Radius = (float)Num(Get(d, "r"), 2.5) };
            }).ToArray();
            s.Effects = Arr(Get(e, "effects")).Select(x => Str(x)).ToArray();
            s.Start = Arr(Get(e, "start")).Select(x => Str(x)).ToArray();
            return s;
        }

        static void Validate(StoryDef def)
        {
            var ids = new HashSet<string>();
            foreach (var s in def.Scenes.Concat(def.Test))
            {
                if (!ids.Add(s.Id)) throw new Exception($"장면 id 중복: {s.Id}");
                if (GameClock.WeekdayOf(s.Month, s.Day) == "?") throw new Exception($"장면 {s.Id} 날짜가 이상함: {s.Month}/{s.Day}");
                if (!s.InZone1 && !StageKeys.Contains(s.Place)) throw new Exception($"장면 {s.Id} 장소를 모름: {s.Place}");
                if (s.Kind == SceneKind.Move && s.Targets.Length == 0) throw new Exception($"이동 장면 {s.Id} 에 목표가 없음");
                if (s.Kind == SceneKind.Card && def.Episode(s.Ep) == null) throw new Exception($"카드 {s.Id} 의 회차 {s.Ep} 없음");
            }
            // 날짜·시각은 목록 순서대로 거꾸로 가지 않는다(고정 일정)
            for (int i = 1; i < def.Scenes.Length; i++)
            {
                var a = def.Scenes[i - 1]; var b = def.Scenes[i];
                long ka = (a.Month * 100L + a.Day) * 10000 + a.Hour * 100 + a.Minute, kb = (b.Month * 100L + b.Day) * 10000 + b.Hour * 100 + b.Minute;
                if (kb < ka) throw new Exception($"장면 순서의 날짜·시각이 거꾸로: {a.Id} {a.TimeLabel} → {b.Id} {b.TimeLabel}");
            }
        }

        public static List<DlgLine> ReadTsv(string path)
        {
            var r = new List<DlgLine>();
            foreach (var raw in File.ReadAllLines(path))
            {
                if (string.IsNullOrWhiteSpace(raw) || raw.StartsWith("#") || raw.StartsWith("id\t")) continue;
                var c = raw.Split('\t');
                if (c.Length < 5) throw new Exception($"{Path.GetFileName(path)}: 열이 모자람: {raw}");
                string id = c[0].Trim();
                int dot = id.LastIndexOf('.');
                var l = new DlgLine
                {
                    Id = id,
                    Scene = dot > 0 ? id.Substring(0, dot) : id,
                    No = dot > 0 && int.TryParse(id.Substring(dot + 1), out int n) ? n : 0,
                    Who = c[1].Trim(), Face = c[2].Trim(), Mode = DlgLine.ParseMode(c[3]), Text = c[4].Trim(),
                    Wait = c.Length > 5 && float.TryParse(c[5], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float w) ? w : 0f,
                    Cam = c.Length > 6 ? c[6].Trim() : "", Note = c.Length > 7 ? c[7].Trim() : "",
                };
                if (l.Mode == DlgMode.Choice && (l.Options.Length < 1 || l.Options.Length > 3)) throw new Exception($"{id}: 선택지는 1~3개");
                r.Add(l);
            }
            return r;
        }

        // ───────────────────────── 조명 프리셋(없을 때만 — 손으로 고친 값 보존)
        /// scene = Zone1(낮 프리셋을 지금 장면 값으로) — 없으면 기본값
        public static LightingPreset[] Presets(Scene? zone)
        {
            var r = new LightingPreset[4];
            foreach (DayPart p in Enum.GetValues(typeof(DayPart)))
            {
                string path = PresetPath(p);
                var a = AssetDatabase.LoadAssetAtPath<LightingPreset>(path);
                if (a == null)
                {
                    a = ScriptableObject.CreateInstance<LightingPreset>();
                    Fill(a, p, zone);
                    AssetDatabase.CreateAsset(a, path);
                    Notes.Add($"조명 프리셋 만듦: {path}");
                }
                r[(int)p] = a;
            }
            return r;
        }

        static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

        static void Fill(LightingPreset a, DayPart p, Scene? zone)
        {
            a.Part = p;
            switch (p)
            {
                case DayPart.Dawn:   // 05:30 — 해 없음(동쪽 하늘만 밝음), 가로등·보안등 나트륨, 국밥집 김 오르는 창
                    a.SunPitch = 24f; a.SunYaw = 80f; a.SunColor = new Color(0.62f, 0.68f, 0.86f); a.SunIntensity = 0.75f; a.ShadowStrength = 0.35f;     // D07: 걷는 면 L* ≥ 28
                    a.AmbientSky = new Color(0.66f, 0.72f, 0.86f); a.AmbientEquator = new Color(0.58f, 0.60f, 0.70f); a.AmbientGround = new Color(0.36f, 0.36f, 0.41f);
                    a.Sky = Hex("#39465A"); a.Fog = new Color(0.168f, 0.205f, 0.265f); a.FogStart = 25f; a.FogEnd = 260f;
                    a.SplitShadows = new Color(0.20f, 0.30f, 0.45f); a.SplitHighlights = Hex("#C9A98A"); a.SplitBalance = -10f;
                    a.StreetLamps = a.SecurityLamps = a.Vending = a.ConvAwning = a.GukbapWindow = true; a.ShopSigns = false; a.LampIntensity = 6f; a.Windows = 0.15f;
                    break;
                case DayPart.Dusk:   // 17:40~18:00 — 서쪽 8°, 자판기·상가 간판 켜지기 시작
                    a.SunPitch = 8f; a.SunYaw = 280f; a.SunColor = new Color(1.0f, 0.70f, 0.45f); a.SunIntensity = 1.0f; a.ShadowStrength = 0.6f;
                    a.AmbientSky = new Color(0.75f, 0.63f, 0.56f); a.AmbientEquator = new Color(0.62f, 0.53f, 0.49f); a.AmbientGround = new Color(0.36f, 0.31f, 0.31f);
                    a.Sky = Hex("#E9C7A0"); a.Fog = new Color(0.80f, 0.67f, 0.55f); a.FogStart = 35f; a.FogEnd = 320f;
                    a.SplitShadows = new Color(0.30f, 0.22f, 0.36f); a.SplitHighlights = new Color(1.0f, 0.78f, 0.55f); a.SplitBalance = 10f;
                    a.Vending = a.ConvAwning = a.ShopSigns = true; a.StreetLamps = a.SecurityLamps = a.GukbapWindow = false; a.LampIntensity = 5f; a.Windows = 0.1f;
                    break;
                case DayPart.Night:  // 22:00 — 달빛 주변광, 편의점 형광 청록, 가로등 나트륨, 창문 불 40%
                    a.SunPitch = 50f; a.SunYaw = 200f; a.SunColor = new Color(0.62f, 0.70f, 0.95f); a.SunIntensity = 0.75f; a.ShadowStrength = 0.4f;     // D07: 걷는 면 L* ≥ 22(가로등 없는 와룡공원길·주차장 안쪽도)
                    a.AmbientSky = new Color(0.56f, 0.61f, 0.78f); a.AmbientEquator = new Color(0.45f, 0.48f, 0.60f); a.AmbientGround = new Color(0.27f, 0.27f, 0.32f);
                    a.Sky = Hex("#2A3140"); a.Fog = new Color(0.11f, 0.13f, 0.18f); a.FogStart = 20f; a.FogEnd = 220f;
                    a.SplitShadows = new Color(0.12f, 0.25f, 0.30f); a.SplitHighlights = new Color(0.95f, 0.75f, 0.50f); a.SplitBalance = -20f;
                    a.StreetLamps = a.SecurityLamps = a.Vending = a.ConvAwning = a.GukbapWindow = a.ShopSigns = true; a.LampIntensity = 7f; a.Windows = 0.4f;
                    break;
                default:             // 낮 = 지금 Zone1 값(남남동 48°, 종이 크림 하늘)
                    {
                        Light sun = null; Camera cam = null;
                        if (zone.HasValue)
                            foreach (var g in zone.Value.GetRootGameObjects())
                            {
                                if (sun == null) sun = g.GetComponentsInChildren<Light>(true).FirstOrDefault(l => l.type == LightType.Directional);
                                if (cam == null) cam = g.GetComponentsInChildren<Camera>(true).FirstOrDefault(c => c.CompareTag("MainCamera"));
                            }
                        if (sun != null)
                        {
                            var e = sun.transform.eulerAngles;
                            a.SunPitch = e.x; a.SunYaw = Mathf.Repeat(e.y - 180f, 360f);
                            a.SunColor = sun.color; a.SunIntensity = sun.intensity; a.ShadowStrength = sun.shadowStrength;
                        }
                        a.Sky = cam != null ? cam.backgroundColor : Hex("#EEE3D1");
                        a.Fog = RenderSettings.fogColor; a.FogStart = RenderSettings.fogStartDistance; a.FogEnd = RenderSettings.fogEndDistance;
                        if (RenderSettings.ambientMode == AmbientMode.Trilight)
                        { a.AmbientSky = RenderSettings.ambientSkyColor; a.AmbientEquator = RenderSettings.ambientEquatorColor; a.AmbientGround = RenderSettings.ambientGroundColor; }
                        else { var c = RenderSettings.ambientLight; a.AmbientSky = c; a.AmbientEquator = c * 0.85f; a.AmbientGround = c * 0.6f; }
                        a.SplitShadows = new Color(0.184f, 0.290f, 0.353f); a.SplitHighlights = new Color(0.953f, 0.851f, 0.690f); a.SplitBalance = 0f;
                        a.Windows = 0f;
                        break;
                    }
            }
        }

        // ───────────────────────── Zone1 에 붙이기(M1Setup.AttachRig 끝에서 부름)
        public static void AddStoryToZone1(RigFactory.Rig rig, Transform zoneRoot)
        {
            var scene = zoneRoot.gameObject.scene;
            foreach (var g in scene.GetRootGameObjects()) if (g.name == "Story") Object.DestroyImmediate(g);
            var old = zoneRoot.Find("Lights");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var fonts = AssetDatabase.LoadAssetAtPath<UiFonts>(FontsPath) ?? Fonts();
            var def = AssetDatabase.LoadAssetAtPath<StoryDef>(StoryPath);
            if (def == null) { Data(); def = AssetDatabase.LoadAssetAtPath<StoryDef>(StoryPath); }
            var book = AssetDatabase.LoadAssetAtPath<DlgBook>(DialoguePath);
            var cuts = AssetDatabase.LoadAssetAtPath<CutLib>(CutLibPath);
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(M1Setup.InputPath);
            var presets = Presets(scene);

            var root = new GameObject("Story");
            SceneManager.MoveGameObjectToScene(root, scene);
            var runner = root.AddComponent<StoryRunner>();
            var loader = root.AddComponent<SceneLoader>();
            var talkIn = root.AddComponent<TalkInput>();
            var talk = root.AddComponent<DialogueRunner>();
            var cut = root.AddComponent<Cutscene>();
            var dir = root.AddComponent<UnityEngine.Playables.PlayableDirector>();
            dir.playOnAwake = false;
            cut.Director = dir;
            var day = root.AddComponent<DayLight>();
            T Ui<T>(string name) where T : Component { var go = new GameObject(name) { layer = 5 }; go.transform.SetParent(root.transform, false); return go.AddComponent<T>(); }
            var hud = Ui<StoryHud>("이야기 화면"); hud.Fonts = fonts;
            var sub = Ui<SubtitleUi>("자막"); sub.Fonts = fonts;
            var inner = Ui<InnerVoiceUi>("속마음"); inner.Fonts = fonts; inner.Sub = sub;
            var choice = Ui<ChoiceUi>("선택지"); choice.Fonts = fonts; choice.In = talkIn;
            var endure = Ui<EndureFx>("참기");
            var ledger = Ui<LedgerUi>("가계부"); ledger.Fonts = fonts;
            var names = Ui<NameBookUi>("도감"); names.Fonts = fonts;
            var card = Ui<EpisodeCard>("회차 카드"); card.Fonts = fonts;
            var wipe = Ui<InkWipe>("먹 닦기");

            runner.Def = def; runner.Dialogue = book; runner.Cuts = cuts; runner.Actions = actions; runner.Fonts = fonts;
            runner.Loader = loader; runner.Hud = hud; runner.Talk = talk; runner.TalkIn = talkIn; runner.Cut = cut;
            runner.Card = card; runner.LedgerUi = ledger; runner.NameUi = names; runner.Endure = endure; runner.Light = day;
            runner.M2Stages = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Stages");
            loader.Wipe = wipe; loader.Motor = rig.Motor; loader.Cam = rig.CamRig; loader.ZoneRoot = zoneRoot.gameObject;
            talkIn.Actions = actions;
            talk.Sub = sub; talk.Inner = inner; talk.Choice = choice; talk.Hud = hud; talk.In = talkIn; talk.Endure = endure; talk.LedgerUi = ledger;
            day.Dawn = presets[(int)DayPart.Dawn]; day.Day = presets[(int)DayPart.Day]; day.Dusk = presets[(int)DayPart.Dusk]; day.Night = presets[(int)DayPart.Night];
            day.Sun = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Light>(true)).FirstOrDefault(l => l.type == LightType.Directional);
            day.Film = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Volume>(true)).FirstOrDefault(v => v.isGlobal);

            int lights = AddLights(zoneRoot, out int windows);
            Debug.Log($"{Tag} Zone1 이야기 루트: Story(StoryRunner·SceneLoader·TalkInput·DialogueRunner·Cutscene·DayLight + 화면 9개) · M2 무대 {(runner.M2Stages != null ? "연결" : "없음")} · " +
                      $"빛 자리 {lights}곳 · 창문 {windows}개 · 대사 {(book != null ? book.Lines.Length : 0)}줄 · 컷신 {(cuts != null ? cuts.Cuts.Length : 0)}편");
        }

        // ───────────────────────── 빛 자리·창문 불(3-2·3-5)
        static int AddLights(Transform zoneRoot, out int windows)
        {
            var root = new GameObject("Lights").transform;
            root.SetParent(zoneRoot, false);
            int n = 0;
            Light Point(string name, Vector3 pos, float range, LightKind kind, Color c)
            {
                var go = new GameObject(name);
                go.transform.SetParent(root, false);
                go.transform.position = pos;
                var l = go.AddComponent<Light>();
                l.type = LightType.Point; l.range = range; l.color = c; l.intensity = 0f; l.enabled = false;
                l.shadows = LightShadows.None;
                l.renderMode = LightRenderMode.ForcePixel;
                var s = go.AddComponent<LightSpot>(); s.Kind = kind;
                n++;
                return l;
            }
            var marks = zoneRoot.Find("Landmarks");
            if (marks != null)
                foreach (var t in marks.GetComponentsInChildren<Transform>(true).Where(x => x.name.Contains("가로등") || x.name.Contains("보안등")).Where(x => x.parent != null && !x.parent.name.Contains("가로등") && !x.parent.name.Contains("보안등")).OrderBy(x => x.name, StringComparer.Ordinal))
                {
                    var rs = t.GetComponentsInChildren<Renderer>(true);
                    if (rs.Length == 0) continue;
                    var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                    bool street = t.name.Contains("가로등");
                    Point("빛 " + t.name, new Vector3(b.center.x, b.max.y - 0.3f, b.center.z), street ? 15f : 11f, street ? LightKind.Street : LightKind.Security, new Color(1f, 0.62f, 0.28f));
                }
            var data = Zone1Data.Load();
            Vector3 Mark(string name) { var m = data.Landmarks.FirstOrDefault(x => x.Name == name); return m != null ? m.Pos : Vector3.zero; }
            Vector3 park = new Vector3(-172.49f, 27.2f, 84.38f);
            var vend = zoneRoot.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == "주차장 자판기");
            if (vend != null)
            {
                var toward = (new Vector3(park.x, vend.position.y, park.z) - vend.position).normalized;
                Point("빛 주차장 자판기", vend.position + toward * 1.0f + Vector3.up * 0.3f, 6f, LightKind.Vending, Color.white);
            }
            var conv = Mark("하루편의점 후문점 출입문");
            if (conv != Vector3.zero) { Point("빛 하루편의점 차양", conv + new Vector3(0f, 2.6f, -1.4f), 10f, LightKind.ConvAwning, Color.cyan); Point("빛 하루편의점 간판", Mark("하루편의점 간판") + new Vector3(0f, 0.2f, -0.8f), 6f, LightKind.ShopSign, Color.white); }
            var guk = Mark("성대후문 국밥 출입문");
            if (guk != Vector3.zero) Point("빛 국밥집 창", guk + new Vector3(0f, 1.8f, -1.1f), 7f, LightKind.GukbapWindow, Color.white);
            var bung = Mark("번개배달 사무실 출입구(2층 계단)");
            if (bung != Vector3.zero) Point("빛 번개배달 간판", bung + new Vector3(0f, 3.2f, -0.8f), 6f, LightKind.ShopSign, Color.white);

            // 창문 불: 집·상가·한옥 블록 면마다 층마다 하나(시드 고정 자리·켜짐 문턱)
            windows = 0;
            var mat = WindowMat();
            var blocks = zoneRoot.Find("Blocks");
            if (blocks != null)
            {
                foreach (var kind in new[] { "house", "shop", "hanok" })
                {
                    var g = blocks.Find(kind);
                    if (g == null) continue;
                    foreach (Transform b in g)
                    {
                        var col = b.GetComponent<BoxCollider>();
                        var mr = b.GetComponent<MeshRenderer>();
                        if (col == null || mr == null) continue;
                        var rng = new System.Random(StableHash(b.name));
                        var size = Vector3.Scale(col.size, b.lossyScale);
                        int floors = Mathf.Clamp(Mathf.FloorToInt((size.y - 0.6f) / 2.8f), 1, 4);
                        for (int face = 0; face < 4; face++)
                        {
                            bool alongX = face < 2;
                            float half = (alongX ? size.z : size.x) * 0.5f, width = alongX ? size.x : size.z;
                            if (width < 2.2f) continue;
                            for (int fl = 0; fl < floors; fl++)
                            {
                                float u = ((float)rng.NextDouble() - 0.5f) * (width - 1.6f);
                                float yy = -size.y * 0.5f + 1.5f + fl * 2.8f;
                                var local = alongX ? new Vector3(u, yy, (face == 0 ? 1 : -1) * (half + 0.03f)) : new Vector3((face == 2 ? 1 : -1) * (half + 0.03f), yy, u);
                                var w = GameObject.CreatePrimitive(PrimitiveType.Quad);
                                Object.DestroyImmediate(w.GetComponent<Collider>());
                                w.name = $"창 {b.name} {face}-{fl}";
                                w.transform.SetParent(root, false);
                                w.transform.position = b.TransformPoint(Vector3.Scale(local, new Vector3(1f / Mathf.Max(1e-4f, b.lossyScale.x), 1f / Mathf.Max(1e-4f, b.lossyScale.y), 1f / Mathf.Max(1e-4f, b.lossyScale.z))) + b.rotation * Vector3.zero);
                                var outward = b.rotation * (alongX ? new Vector3(0f, 0f, face == 0 ? 1f : -1f) : new Vector3(face == 2 ? 1f : -1f, 0f, 0f));
                                w.transform.rotation = Quaternion.LookRotation(-outward, Vector3.up);
                                w.transform.localScale = new Vector3(0.95f, 1.05f, 1f);
                                var r = w.GetComponent<MeshRenderer>();
                                r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; r.enabled = false;
                                var s = w.AddComponent<LightSpot>(); s.Kind = LightKind.Window; s.Threshold = (float)rng.NextDouble();
                                windows++;
                            }
                        }
                    }
                }
            }
            return n;
        }

        static int StableHash(string s) { unchecked { int h = 23; foreach (char c in s) h = h * 31 + c; return h; } }

        static Material WindowMat()
        {
            var sh = Shader.Find("Universal Render Pipeline/Unlit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(LightMatPath);
            if (m == null) { m = new Material(sh) { name = "M_Z1_window_lit" }; AssetDatabase.CreateAsset(m, LightMatPath); }
            if (m.shader != sh) m.shader = sh;
            m.SetColor("_BaseColor", new Color(1f, 0.84f, 0.55f));
            EditorUtility.SetDirty(m);
            return m;
        }

        // ───────────────────────── 자리 무대 6곳(9단계에서 StageKit 으로 바꿈)
        static readonly Dictionary<string, (string title, Vector2 size, bool indoor)> StageInfo = new Dictionary<string, (string, Vector2, bool)>
        {
            ["Gukbap"] = ("국밥집", new Vector2(10f, 8f), true),
            ["Home"] = ("시우네", new Vector2(6f, 7f), true),
            ["Conv"] = ("하루편의점", new Vector2(8f, 10f), true),
            ["School"] = ("혜성고(교실·복도·현관)", new Vector2(9f, 25f), true),
            ["Site"] = ("노가다 현장", new Vector2(20f, 24f), false),
            ["Flash"] = ("회상 거실(A′)", new Vector2(4f, 4f), true),
        };

        public static void Stages()
        {
            EnsureFolder(StageMatDir);
            var floor = Mat("M_St_Floor", new Color(0.78f, 0.74f, 0.68f));
            var wall = Mat("M_St_Wall", new Color(0.90f, 0.87f, 0.80f));
            var actorM = Mat("M_St_Actor", new Color(0.55f, 0.45f, 0.42f));
            var propM = Mat("M_St_Prop", new Color(0.35f, 0.33f, 0.32f));
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFont.AssetPath);
            for (int i = 0; i < StageKeys.Length; i++)
            {
                string key = StageKeys[i];
                var info = StageInfo[key];
                string path = SceneLoader.StagePath(key);
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var root = new GameObject("Stage_" + key);
                root.transform.position = new Vector3(2000f + 100f * i, 0f, 2000f);
                var place = root.AddComponent<StagePlace>();
                place.Key = key; place.Title = info.title; place.Indoor = info.indoor; place.KillY = -10f; place.Placeholder = true;
                float w = info.size.x, d = info.size.y, h = 3.0f;
                Box(root.transform, "바닥", new Vector3(0f, -0.25f, 0f), new Vector3(w + 2f, 0.5f, d + 2f), Layers.Ground, floor);
                if (info.indoor)
                {
                    Box(root.transform, "벽 북", new Vector3(0f, h / 2f, d / 2f + 0.1f), new Vector3(w + 0.4f, h, 0.2f), Layers.Wall, wall);
                    Box(root.transform, "벽 남", new Vector3(0f, h / 2f, -d / 2f - 0.1f), new Vector3(w + 0.4f, h, 0.2f), Layers.Wall, wall);
                    Box(root.transform, "벽 동", new Vector3(w / 2f + 0.1f, h / 2f, 0f), new Vector3(0.2f, h, d), Layers.Wall, wall);
                    Box(root.transform, "벽 서", new Vector3(-w / 2f - 0.1f, h / 2f, 0f), new Vector3(0.2f, h, d), Layers.Wall, wall);
                }
                else
                {
                    foreach (var (n, c, s) in new[] { ("울타리 북", new Vector3(0f, 1f, d / 2f + 1f), new Vector3(w + 2f, 2f, 0.2f)), ("울타리 남", new Vector3(0f, 1f, -d / 2f - 1f), new Vector3(w + 2f, 2f, 0.2f)),
                                                       ("울타리 동", new Vector3(w / 2f + 1f, 1f, 0f), new Vector3(0.2f, 2f, d + 2f)), ("울타리 서", new Vector3(-w / 2f - 1f, 1f, 0f), new Vector3(0.2f, 2f, d + 2f)) })
                        Box(root.transform, n, c, s, Layers.Wall, wall);
                }
                // 방 불(늘 켬)
                var lgo = new GameObject("방 불");
                lgo.transform.SetParent(root.transform, false);
                lgo.transform.localPosition = new Vector3(0f, h - 0.3f, 0f);
                var lt = lgo.AddComponent<Light>();
                lt.type = LightType.Point; lt.range = Mathf.Max(w, d) * 1.1f; lt.intensity = info.indoor ? 3f : 1.5f; lt.color = new Color(1f, 0.95f, 0.86f); lt.shadows = LightShadows.None;
                // 이름판
                if (font != null)
                {
                    var label = new GameObject("이름판");
                    label.transform.SetParent(root.transform, false);
                    label.transform.localPosition = new Vector3(0f, 2.2f, d / 2f - 0.05f);
                    var tmp = label.AddComponent<TextMeshPro>();
                    tmp.font = font; tmp.fontSize = 6f; tmp.alignment = TextAlignmentOptions.Center; tmp.color = UiKit.Ink;
                    tmp.text = $"{info.title}\n<size=60%>자리 무대 — 9단계에서 키트로</size>";
                    tmp.rectTransform.sizeDelta = new Vector2(Mathf.Max(4f, w - 0.6f), 2f);
                }
                // 스폰 자리
                var spawns = new List<Transform> { Spawn(root.transform, "Spawn", new Vector3(0f, 0f, -d / 2f + Mathf.Min(3.3f, d / 2f + 1.3f)), 0f) };
                if (key == "Site") { spawns.Add(Spawn(root.transform, "Gate", new Vector3(-w / 2f + 4f, 0f, -d / 2f + 5f), 30f)); spawns.Add(Spawn(root.transform, "Stairs", new Vector3(w / 2f - 3f, 0f, 2f), 0f)); }
                if (key == "School") spawns.Add(Spawn(root.transform, "Hall", new Vector3(0f, 0f, d / 2f - 4.5f), 180f));
                place.Spawns = spawns.ToArray();
                // 시험 컷신 배우(국밥집): 엄마 캡슐 · 가마솥 · 목도리 / 학교: 반 친구 캡슐 몇
                if (key == "Gukbap")
                {
                    Actor(root.transform, "엄마", new Vector3(-1.2f, 0f, 1.2f), 200f, 1.62f, actorM);
                    var pot = GameObject.CreatePrimitive(PrimitiveType.Cylinder); pot.name = "가마솥"; Object.DestroyImmediate(pot.GetComponent<Collider>());
                    pot.transform.SetParent(root.transform, false); pot.transform.localPosition = new Vector3(-2.6f, 0.45f, 2.4f); pot.transform.localScale = new Vector3(1.1f, 0.45f, 1.1f);
                    pot.GetComponent<Renderer>().sharedMaterial = propM;
                    var scarf = GameObject.CreatePrimitive(PrimitiveType.Cube); scarf.name = "목도리"; Object.DestroyImmediate(scarf.GetComponent<Collider>());
                    scarf.transform.SetParent(root.transform, false); scarf.transform.localPosition = new Vector3(-1.6f, 1.0f, 2.6f); scarf.transform.localScale = new Vector3(0.30f, 0.07f, 0.14f);
                    scarf.GetComponent<Renderer>().sharedMaterial = Mat("M_St_Scarf", new Color(0.62f, 0.25f, 0.22f));
                }
                if (key == "School")
                    for (int k = 0; k < 6; k++) Actor(root.transform, $"반 친구 {k + 1}", new Vector3(-3f + (k % 3) * 3f, 0f, -1f + (k / 3) * 2.5f), 180f, 1.7f, actorM);
                string spawnNames = string.Join(",", spawns.Select(s => s.name));
                EditorSceneManager.SaveScene(scene, path);
                Stable(path);
                Notes.Add($"자리 무대 {key}({info.title}) {w}×{d}m · 스폰 {spawnNames} → {path}");
            }
        }

        static Transform Spawn(Transform root, string name, Vector3 local, float yaw)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return go.transform;
        }

        static void Actor(Transform root, string name, Vector3 local, float yaw, float height, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var holder = new GameObject(name);
            holder.transform.SetParent(root, false);
            holder.transform.localPosition = local;
            holder.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.name = "몸";
            go.transform.SetParent(holder.transform, false);
            go.transform.localPosition = new Vector3(0f, height / 2f, 0f);
            go.transform.localScale = new Vector3(0.45f, height / 2f, 0.45f);
            go.GetComponent<Renderer>().sharedMaterial = m;
            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube); nose.name = "코";
            Object.DestroyImmediate(nose.GetComponent<Collider>());
            nose.transform.SetParent(holder.transform, false);
            nose.transform.localPosition = new Vector3(0f, height - 0.2f, 0.22f); nose.transform.localScale = new Vector3(0.08f, 0.08f, 0.12f);
            nose.GetComponent<Renderer>().sharedMaterial = m;
        }

        static GameObject Box(Transform root, string name, Vector3 local, Vector3 size, int layer, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name; go.layer = layer;
            go.transform.SetParent(root, false);
            go.transform.localPosition = local; go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        static Material Mat(string name, Color c)
        {
            string p = $"{StageMatDir}/{name}.mat";
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m == null) { m = new Material(sh) { name = name }; AssetDatabase.CreateAsset(m, p); }
            if (m.shader != sh) m.shader = sh;
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// 저장한 장면 fileID 를 경로 기준으로(같은 입력이면 같은 파일)
        static void Stable(string path)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            StableIds.RewriteScene(path, "", new Dictionary<long, long>());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        }

        // ───────────────────────── 컷신(2단계)
        public static void Cuts()
        {
            EnsureFolder(CutDir);
            bool re = Arg("-recut");
            var lib = AssetDatabase.LoadAssetAtPath<CutLib>(CutLibPath);
            if (lib == null) { lib = ScriptableObject.CreateInstance<CutLib>(); AssetDatabase.CreateAsset(lib, CutLibPath); }
            var list = new List<CutLib.Entry>();
            list.Add(new CutLib.Entry { Name = "1-1", Timeline = MakeCut("1-1", re, Opening) });
            list.Add(new CutLib.Entry { Name = "1-6", Timeline = MakeCut("1-6", re, PlayFlash) });
            lib.Cuts = list.ToArray();
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            Notes.Add($"컷신: {string.Join(", ", list.Select(e => $"{e.Name}({e.Timeline.duration:F1}초)"))} → {CutLibPath}{(re ? " (다시 만듦)" : "")}");
        }

        static TimelineAsset MakeCut(string name, bool re, Action<TimelineAsset> fill)
        {
            string path = $"{CutDir}/Cut_{name}.playable";
            var tl = AssetDatabase.LoadAssetAtPath<TimelineAsset>(path);
            if (tl != null && !re) return tl;
            if (tl != null) AssetDatabase.DeleteAsset(path);
            tl = ScriptableObject.CreateInstance<TimelineAsset>();
            tl.name = "Cut_" + name;
            AssetDatabase.CreateAsset(tl, path);
            fill(tl);
            EditorUtility.SetDirty(tl);
            AssetDatabase.SaveAssets();
            return tl;
        }

        static void Clip<T>(TrackAsset tr, double start, double dur, Action<T> set) where T : ScriptableObject, UnityEngine.Playables.IPlayableAsset
        {
            var c = tr.CreateClip<T>();
            c.start = start; c.duration = dur;
            c.displayName = typeof(T).Name.Replace("Clip", "") + " " + start.ToString("F1");
            set((T)c.asset);
            EditorUtility.SetDirty(c.asset);
        }

        /// 1-1 오프닝(캡슐 시험판): 가마솥 김 → 엄마 "국물 흘리면 너 죽어!" → 목도리 "추워. 바보." → 등 뒤 카메라
        static void Opening(TimelineAsset tl)
        {
            var cam = tl.CreateTrack<CamTrack>(null, "카메라");
            var sub = tl.CreateTrack<SubtitleTrack>(null, "자막");
            var face = tl.CreateTrack<FaceTrack>(null, "표정");
            var hand = tl.CreateTrack<HandPoseTrack>(null, "손");
            var prop = tl.CreateTrack<PropTrack>(null, "소품");
            var ui = tl.CreateTrack<UiTrack>(null, "UI");
            var sfx = tl.CreateTrack<SfxTrack>(null, "소리");
            var time = tl.CreateTrack<TimeTrack>(null, "시간");
            void Cam(double s, double d, CamPreset p, string subj, string other = "") => Clip<CamClip>(cam, s, d, c => { c.Preset = p; c.Subject = subj; c.Other = other; });
            void Sub(double s, double d, string who, string text, DlgMode m) => Clip<SubtitleClip>(sub, s, d, c => { c.Who = who; c.Text = text; c.Mode = m; });
            Cam(0, 3, CamPreset.Wide, "엄마");      // 가마솥은 북쪽 벽을 보고 있어 와이드가 벽에 막힘 → 방 가운데를 보는 엄마 쪽에서
            Cam(3, 7, CamPreset.Close, "엄마");
            Cam(10, 3, CamPreset.LowFront, "엄마", "시우");
            Cam(13, 2.2, CamPreset.LowAngle, "엄마");
            Cam(15.2, 6.6, CamPreset.Close, "시우");
            Cam(21.8, 2.4, CamPreset.BackTrack, "시우");
            Sub(3.2, 6.4, "엄마", "시우야! 꼭대기 집 두 개, 경비실 하나, 소극장 하나!\\n국물 흘리면 너 죽어!", DlgMode.Sub);
            Sub(9.8, 3.2, "시우", "엄마는 사랑을 협박으로 표현한다.", DlgMode.Inner);
            Sub(13.2, 1.9, "엄마", "추워. 바보.", DlgMode.Sub);
            Sub(15.3, 6.4, "시우", "엄마의 '바보'는 사전에 없는 뜻이다.\\n대충 '다녀와'랑 '사랑해' 사이 어디쯤.", DlgMode.Inner);
            Clip<FaceClip>(face, 3.0, 6.6, c => { c.Who = "엄마"; c.Face = "angry"; });
            Clip<FaceClip>(face, 13.0, 2.0, c => { c.Who = "엄마"; c.Face = "normal"; });
            Clip<FaceClip>(face, 15.2, 6.0, c => { c.Who = "시우"; c.Face = "normal"; });
            Clip<HandPoseClip>(hand, 3.0, 6.6, c => { c.Who = "엄마"; c.Pose = "병 쥐기"; });
            Clip<PropClip>(prop, 13.0, 0.5, c => { c.Prop = "목도리"; c.Socket = "시우"; c.Attach = true; });
            Clip<UiClip>(ui, 23.2, 0.5, c => c.Action = "flag:목도리");
            Clip<SfxClip>(sfx, 0.0, 3.0, c => c.Name = "가마솥 김");
            Clip<SfxClip>(sfx, 13.0, 0.6, c => c.Name = "목도리");
            Clip<TimeClip>(time, 13.0, 0.6, c => c.Scale = 0.6f);
        }

        /// 1-6 학예회 흑백 컷(A′ 시험판): 측면 트래킹 + 흑백 + 속마음
        static void PlayFlash(TimelineAsset tl)
        {
            var cam = tl.CreateTrack<CamTrack>(null, "카메라");
            var sub = tl.CreateTrack<SubtitleTrack>(null, "자막");
            var ui = tl.CreateTrack<UiTrack>(null, "UI");
            Clip<CamClip>(cam, 0, 4.4, c => { c.Preset = CamPreset.SideTrack; c.Subject = "시우"; });
            Clip<UiClip>(ui, 0.0, 0.2, c => c.Action = "ink:1");
            Clip<SubtitleClip>(sub, 0.3, 3.4, c => { c.Who = "시우"; c.Text = "중1 학예회. 우리 반 연극."; c.Mode = DlgMode.Inner; });
            Clip<UiClip>(ui, 4.0, 0.2, c => c.Action = "ink:0");
        }

        // ───────────────────────── 카메라 프리셋 프리팹(없을 때만)
        public static void CamPrefabs()
        {
            EnsureFolder(CamPrefabDir);
            int made = 0;
            foreach (CamPreset p in Enum.GetValues(typeof(CamPreset)))
            {
                string path = $"{CamPrefabDir}/CP_{p}.prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) continue;
                var d = CutCams.Make(p, null);
                d.gameObject.name = "CP_" + p;
                PrefabUtility.SaveAsPrefabAsset(d.gameObject, path);
                Object.DestroyImmediate(d.gameObject);
                made++;
            }
            CutCams.ReleaseAll();
            Notes.Add($"카메라 프리셋 프리팹 {Enum.GetValues(typeof(CamPreset)).Length}종(새로 {made}) → {CamPrefabDir}");
        }

        // ───────────────────────── InkMode 렌더러 기능(A′)
        public static void InkFeature()
        {
            string mp = "Assets/_Project/Materials/Fx/M_InkMode.mat";
            var sh = Shader.Find("Haengin/InkMode");
            if (sh == null) { Notes.Add("InkMode 셰이더 없음 — 렌더러 기능 건너뜀"); return; }
            EnsureFolder("Assets/_Project/Materials/Fx");
            var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
            if (m == null) { m = new Material(sh) { name = "M_InkMode" }; AssetDatabase.CreateAsset(m, mp); }
            if (m.shader != sh) m.shader = sh;
            foreach (var path in Renderers)
            {
                var data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(path);
                if (data == null) continue;
                var have = data.rendererFeatures.FirstOrDefault(f => f is FullScreenPassRendererFeature fs && fs.name == "InkMode") as FullScreenPassRendererFeature;
                if (have != null) { if (have.passMaterial != m) { have.passMaterial = m; EditorUtility.SetDirty(have); } continue; }
                var feat = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                feat.name = "InkMode";
                feat.passMaterial = m;
                feat.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;
                feat.fetchColorBuffer = true;
                feat.requirements = ScriptableRenderPassInput.Color;
                feat.passIndex = 0;
                AssetDatabase.AddObjectToAsset(feat, data);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feat, out _, out long localId);
                var so = new SerializedObject(data);
                var list = so.FindProperty("m_RendererFeatures");
                var map = so.FindProperty("m_RendererFeatureMap");
                // 쇼크 컷보다 앞(흑백 위에 쇼크 컷이 얹히게): 맨 끝에 넣고 쇼크 컷이 있으면 자리를 바꿈
                list.arraySize++; map.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feat;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                int shock = -1;
                for (int i = 0; i < list.arraySize - 1; i++) if (list.GetArrayElementAtIndex(i).objectReferenceValue is FullScreenPassRendererFeature f && f.name == "ShockCut") shock = i;
                if (shock >= 0) { list.MoveArrayElement(list.arraySize - 1, shock); map.MoveArrayElement(map.arraySize - 1, shock); }
                so.ApplyModifiedPropertiesWithoutUndo();
                data.SetDirty();
                EditorUtility.SetDirty(data);
                Notes.Add($"InkMode 전체 화면 패스 붙임: {path}");
            }
        }

        // ───────────────────────── 타이틀 장면
        public static void Title()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = UiKit.Paper;
            camGo.AddComponent<AudioListener>();
            var t = new GameObject("타이틀");
            var menu = t.AddComponent<TitleMenu>();
            menu.Actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(M1Setup.InputPath);
            menu.Fonts = AssetDatabase.LoadAssetAtPath<UiFonts>(FontsPath) ?? Fonts();
            menu.Def = AssetDatabase.LoadAssetAtPath<StoryDef>(StoryPath);
            EditorSceneManager.SaveScene(scene, TitleScene);
            Stable(TitleScene);
            Notes.Add($"타이틀 장면 → {TitleScene}");
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int i = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, i));
            AssetDatabase.CreateFolder(path.Substring(0, i), path.Substring(i + 1));
        }
    }
}
