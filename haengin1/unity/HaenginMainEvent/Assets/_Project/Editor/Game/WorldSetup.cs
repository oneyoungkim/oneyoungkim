// 행인1의 메인이벤트 — 오픈월드 1장 설정(docs/13_오픈월드_커리어_설계.md 12-4 0단계): '데이터 → 에디터 스크립트 → 에셋·장면' 패턴.
// batchmode:
//   -executeMethod Haengin.EditorGame.WorldSetup.Build     Settings/World.asset(Data/World/*.json — WorldJson 결정적 변환) · 이야기 데이터(StorySetup.Data — 카드 시각 고친 m3_story.json) ·
//                                                          무대 다시 만들기(실내 2차: 손님·동료·이불·창빛·스탠드·김 — DressLife) · Zone1 다시 만들기(세계 루트 World — M1Setup.AttachRig 끝) · 빌드 목록
//   -executeMethod Haengin.EditorGame.WorldSetup.DataOnly  World.asset 만
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Haengin.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Haengin.EditorGame
{
    public static class WorldSetup
    {
        const string Tag = "[WorldSetup]";
        public const string Root = "Assets/_Project";
        public const string DataDir = Root + "/Data/World";
        public const string WorldPath = Root + "/Settings/World.asset";
        public const string MapZonesPath = "../../data/map_zones.json";     // 레포 data/ (프로젝트 폴더 기준)
        const string MatDir = Root + "/Materials/Stage";
        const string SteamTex = Root + "/Art/World/T_Steam.png";

        static readonly List<string> Notes = new List<string>();

        [MenuItem("Haengin/오픈월드 1장 설정 전부")]
        public static void Build() => Run(() =>
        {
            Data();
            StorySetup.Fonts();
            StorySetup.Data();
            StorySetup.Stages();
            Zone1Builder.Rebuild();          // BeforeSave → M1Setup.AttachRig → … → WorldSetup.AddToZone1
            M1Setup.SetBuildScenes();
        });

        [MenuItem("Haengin/오픈월드 데이터만")]
        public static void DataOnly() => Run(() => Data());

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

        /// 원본 글 읽기(테스트도 같은 순서 — 파일 이름순)
        public static (string chapters, string activities, string balance, List<string> events, string zones) ReadSources()
        {
            string R(string p) => File.ReadAllText(p);
            var ev = Directory.GetFiles(DataDir + "/events", "*.json").OrderBy(p => p, StringComparer.Ordinal).Select(R).ToList();
            string zones = File.Exists(MapZonesPath) ? R(MapZonesPath) : null;
            return (R(DataDir + "/chapters.json"), R(DataDir + "/activities.json"), R(DataDir + "/balance.json"), ev, zones);
        }

        public static WorldDef Data()
        {
            var src = ReadSources();
            var def = AssetDatabase.LoadAssetAtPath<WorldDef>(WorldPath);
            if (def == null) { def = ScriptableObject.CreateInstance<WorldDef>(); AssetDatabase.CreateAsset(def, WorldPath); }
            WorldJson.Fill(def, src.chapters, src.activities, src.balance, src.events, src.zones);
            def.Drills = AssetDatabase.LoadAssetAtPath<DrillBook>(GymSetup.BookPath);
            Validate(def);
            EditorUtility.SetDirty(def);
            AssetDatabase.SaveAssets();
            Notes.Add($"오픈월드: 장 {def.Chapters.Length} · 활동 {def.Activities.Length} · 카드 {def.Cards.Length}(데모 {def.Cards.Count(c => c.Demo)}) · 지점 {def.Balance.Points.Length} · 03 존 {def.MapZones.Length} · 드릴 {(def.Drills != null ? def.Drills.Drills.Length : 0)} → {WorldPath}");
            return def;
        }

        static void Validate(WorldDef def)
        {
            var ids = new HashSet<string>();
            foreach (var c in def.Cards)
            {
                if (!ids.Add(c.Id)) throw new Exception($"카드 id 겹침: {c.Id}");
                foreach (var s in c.Scenes) if (string.IsNullOrEmpty(s)) throw new Exception($"{c.Id}: 빈 장면 id");
            }
            foreach (var c in def.Cards)
                foreach (var e in c.After.Concat(c.Need))
                {
                    foreach (var pre in new[] { "chain:", "card:", "!card:", "done:" })
                        if (e.StartsWith(pre) && def.Card(e.Substring(pre.Length)) == null) throw new Exception($"{c.Id}: 없는 카드 참조 {e}");
                }
        }

        // ───────────────────────── Zone1 세계 루트(M1Setup.AttachRig 끝에서)
        public static void AddToZone1(Transform zoneRoot)
        {
            var scene = zoneRoot.gameObject.scene;
            foreach (var g in scene.GetRootGameObjects()) if (g.name == "World") Object.DestroyImmediate(g);
            var def = AssetDatabase.LoadAssetAtPath<WorldDef>(WorldPath);
            if (def == null) def = Data();
            var fonts = AssetDatabase.LoadAssetAtPath<UiFonts>(StorySetup.FontsPath);
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(M1Setup.InputPath);
            var storyRoot = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Story");
            var story = storyRoot != null ? storyRoot.GetComponent<StoryRunner>() : null;
            var loader = storyRoot != null ? storyRoot.GetComponent<SceneLoader>() : null;
            var root = new GameObject("World");
            SceneManager.MoveGameObjectToScene(root, scene);
            var w = root.AddComponent<WorldRunner>();
            T Ui<T>(string name) where T : Component { var go = new GameObject(name) { layer = 5 }; go.transform.SetParent(root.transform, false); return go.AddComponent<T>(); }
            var hud = Ui<WorldHud>("세계 화면"); hud.Fonts = fonts;
            var phone = Ui<PhoneUi>("휴대폰"); phone.Fonts = fonts; phone.Actions = actions;
            var plan = Ui<PlannerUi>("주간 계획"); plan.Fonts = fonts; plan.Actions = actions;
            var ch = Ui<ChapterUi>("챕터 카드"); ch.Fonts = fonts; ch.Actions = actions;
            var job = Ui<JobGame>("알바 미니게임"); job.Fonts = fonts; job.Actions = actions;
            w.Def = def; w.Story = story; w.Loader = loader; w.Actions = actions; w.Fonts = fonts;
            w.Hud = hud; w.Phone = phone; w.PlannerView = plan; w.ChapterView = ch; w.Job = job;
            Debug.Log($"{Tag} Zone1 세계 루트: World(WorldRunner + 화면 5) · 카드 {def.Cards.Length} · 이야기 {(story != null ? "연결" : "없음")}");
        }

        // ───────────────────────── 실내 2차(StorySetup.Stages 가 무대를 다시 만들 때)
        static readonly Vector3[] Stools =
        {
            new Vector3(-4.15f, 0f, -2.55f), new Vector3(-3.05f, 0f, -2.55f), new Vector3(-3.56f, 0f, -3.07f), new Vector3(-3.56f, 0f, -2.03f),
            new Vector3(-1.8f, 0f, -2.55f), new Vector3(-0.7f, 0f, -2.55f), new Vector3(-1.21f, 0f, -3.07f), new Vector3(-1.21f, 0f, -2.03f),
            new Vector3(-4.15f, 0f, -0.95f), new Vector3(-3.05f, 0f, -0.95f), new Vector3(-3.56f, 0f, -1.47f), new Vector3(-3.56f, 0f, -0.43f),
            new Vector3(-1.8f, 0f, -0.95f), new Vector3(-0.7f, 0f, -0.95f), new Vector3(-1.21f, 0f, -1.47f), new Vector3(-1.21f, 0f, -0.43f),
            new Vector3(2.75f, 0f, 1.35f), new Vector3(3.34f, 0f, 0.83f), new Vector3(3.34f, 0f, 1.87f),
            new Vector3(2.75f, 0f, 3.05f), new Vector3(3.34f, 0f, 2.53f), new Vector3(3.34f, 0f, 3.57f),
        };
        static readonly Vector3[] Tables = { new Vector3(-3.56f, 0f, -2.55f), new Vector3(-1.21f, 0f, -2.55f), new Vector3(-3.56f, 0f, -0.95f), new Vector3(-1.21f, 0f, -0.95f), new Vector3(3.45f, 0f, 1.35f), new Vector3(3.45f, 0f, 3.05f) };

        static Vector3 TableOf(Vector3 stool) => Tables.OrderBy(t => (t - stool).sqrMagnitude).First();
        static float FaceYaw(Vector3 from, Vector3 to) { var d = to - from; d.y = 0f; return d.sqrMagnitude < 1e-6f ? 0f : Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; }

        public static string DressLife(string key, Transform root, StageKitSetup.Placed kp)
        {
            var old = root.Find("Life");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var life = new GameObject("Life").transform; life.SetParent(root, false);
            var sl = root.GetComponent<StageLife>() ?? root.gameObject.AddComponent<StageLife>();
            var slots = new List<StageLife.Slot>();
            var lights = new List<Light>(); var lamps = new List<Light>(); var steam = new List<ParticleSystem>();
            int npcs = 0;
            Transform Group(string name, int from, int to)
            {
                var g = new GameObject(name).transform; g.SetParent(life, false);
                slots.Add(new StageLife.Slot { Go = g.gameObject, From = from, To = to });
                return g;
            }
            Material bowlM = null, soupM = null;
            void Sitter(Transform g, string castKey, string who, int stool, string state, Color tint)
            {
                var s = Stools[stool];
                var n = CastSetup.Put(g, castKey, $"{who} {++npcs}", s, FaceYaw(s, TableOf(s)), state, tint);
                if (n != null) { var r = n.AddComponent<RumorNpc>(); r.Who = who; }
                if (key != "Gukbap") return;
                // 손님 앞 뚝배기(먹 테 검은 그릇 + 국물)
                bowlM ??= Flat("M_World_Ttukbaegi", new Color(0.16f, 0.13f, 0.12f));
                soupM ??= Flat("M_World_Soup", new Color(0.86f, 0.80f, 0.68f));
                var t = TableOf(s);
                var b = Vector3.Lerp(new Vector3(s.x, 0f, s.z), new Vector3(t.x, 0f, t.z), 0.55f);
                Cyl(g, $"뚝배기 {npcs}", new Vector3(b.x, 0.775f, b.z), new Vector3(0.17f, 0.04f, 0.17f), bowlM);
                Cyl(g, $"국물 {npcs}", new Vector3(b.x, 0.812f, b.z), new Vector3(0.14f, 0.004f, 0.14f), soupM);
            }
            void Stander(Transform g, string castKey, string who, Vector3 p, float yaw, Color tint)
            {
                var n = CastSetup.Put(g, castKey, $"{who} {++npcs}", p, yaw, null, tint);
                if (n != null) { var r = n.AddComponent<RumorNpc>(); r.Who = who; }
            }
            var work = new Color(0.62f, 0.66f, 0.70f); var work2 = new Color(0.72f, 0.64f, 0.52f); var stud = new Color(0.86f, 0.86f, 0.90f); var stud2 = new Color(0.80f, 0.82f, 0.90f);
            string note = "";
            switch (key)
            {
                case "Gukbap":
                    {
                        var dawn = Group("손님 새벽(05:30~08:30)", 5 * 60 + 30, 8 * 60 + 30);
                        Sitter(dawn, "Gwak", "손님", 9, "Sit", work); Sitter(dawn, "Gwak", "손님", 13, "SitTalk", work2);
                        var noon = Group("손님 점심(11:00~14:30)", 11 * 60, 14 * 60 + 30);
                        Sitter(noon, "Gwak", "손님", 0, "Sit", work2); Sitter(noon, "StudentF", "손님", 1, "SitTalk", stud2); Sitter(noon, "Gwak", "손님", 14, "Sit", work);
                        var eve = Group("손님 저녁(17:00~21:30)", 17 * 60, 21 * 60 + 30);
                        Sitter(eve, "StudentM", "손님", 0, "SitTalk", stud); Sitter(eve, "StudentF", "손님", 1, "Sit", stud2);
                        Sitter(eve, "Gwak", "손님", 5, "Sit", work); Sitter(eve, "Gwak", "손님", 4, "SitTalk", work2);
                        Sitter(eve, "StudentM", "손님", 16, "Sit", stud2); Sitter(eve, "Gwak", "손님", 20, "SitTalk", work);
                        var night = Group("손님 밤(21:30~23:30)", 21 * 60 + 30, 23 * 60 + 30);
                        Sitter(night, "Gwak", "손님", 19, "SitTalk", work2); Sitter(night, "Gwak", "손님", 21, "Sit", work);
                        foreach (var p in new[] { new Vector3(-3.95f, 1.0f, 3.25f), new Vector3(-2.7f, 1.0f, 3.25f), new Vector3(-1.45f, 1.05f, 2.85f) }) steam.Add(Steam(life, p));
                        var dl = root.GetComponentsInChildren<Light>(true).FirstOrDefault(l => l.name == "Sock_Light_Dawn");
                        if (dl != null) { lights.Add(dl); dl.shadows = LightShadows.None; }
                        sl.SteamFrom = 5 * 60 + 30; sl.SteamTo = 22 * 60;
                        note = $"국밥집: 손님 무리 4(새벽 2·점심 3·저녁 6·밤 2) · 김 3 · 창빛 {lights.Count}";
                        break;
                    }
                case "Conv":
                    {
                        var clerk = Group("알바(18:00~01:00)", 18 * 60, 60);
                        Stander(clerk, "StudentF", "알바생", new Vector3(4.05f, 0f, -2.2f), -90f, new Color(0.55f, 0.75f, 0.70f));
                        var day = Group("손님 낮(08:00~22:00)", 8 * 60, 22 * 60);
                        Stander(day, "StudentM", "손님", new Vector3(0.4f, 0f, 1.2f), 90f, stud);
                        var night = Group("손님 밤(21:00~01:00)", 21 * 60, 60);
                        Stander(night, "Gwak", "손님", new Vector3(-2.6f, 0f, -0.4f), -90f, work);
                        note = "편의점: 알바생(밤)·손님 낮 1·밤 1";
                        break;
                    }
                case "Home":
                    {
                        var cloth = Flat("M_World_Futon", new Color(0.86f, 0.74f, 0.76f));
                        var cloth2 = Flat("M_World_Futon2", new Color(0.62f, 0.70f, 0.84f));
                        var wood = Flat("M_World_Desk", new Color(0.58f, 0.44f, 0.31f));
                        var nightG = Group("이불 펼침(21:30~06:30)", 21 * 60 + 30, 6 * 60 + 30);
                        Box(nightG, "시우 이불", new Vector3(-2.0f, 0.06f, -1.65f), new Vector3(1.0f, 0.12f, 2.0f), cloth);
                        Box(nightG, "베개", new Vector3(-2.0f, 0.16f, -2.45f), new Vector3(0.5f, 0.12f, 0.3f), cloth2);
                        Box(nightG, "하늘 이불", new Vector3(1.6f, 0.08f, -1.2f), new Vector3(0.9f, 0.16f, 1.7f), cloth2);
                        Box(nightG, "하늘 이불 속", new Vector3(1.6f, 0.22f, -1.0f), new Vector3(0.45f, 0.18f, 0.9f), cloth2);
                        var dayG = Group("이불 갬(06:30~21:30)", 6 * 60 + 30, 21 * 60 + 30);
                        Box(dayG, "갠 이불", new Vector3(-2.55f, 0.18f, -2.9f), new Vector3(0.8f, 0.36f, 0.5f), cloth);
                        // 하늘(앉음)은 밤 22:00~06:30 동안 잠(이불 속)
                        var haneul = root.Find("하늘");
                        if (haneul != null) slots.Add(new StageLife.Slot { Go = haneul.gameObject, From = 6 * 60 + 30, To = 22 * 60 });
                        Box(life, "책상", new Vector3(1.9f, 0.36f, 2.95f), new Vector3(1.0f, 0.72f, 0.5f), wood);
                        var lampGo = new GameObject("스탠드 불"); lampGo.transform.SetParent(life, false); lampGo.transform.localPosition = new Vector3(2.15f, 1.05f, 2.95f);
                        var lamp = lampGo.AddComponent<Light>(); lamp.type = LightType.Point; lamp.color = new Color(1f, 0.78f, 0.48f); lamp.intensity = 1.6f; lamp.range = 3.0f; lamp.shadows = LightShadows.None;
                        lamps.Add(lamp);
                        Box(life, "스탠드", new Vector3(2.15f, 0.86f, 2.95f), new Vector3(0.12f, 0.28f, 0.12f), Flat("M_World_Lamp", new Color(0.95f, 0.88f, 0.70f)));
                        var wl = root.GetComponentsInChildren<Light>(true).FirstOrDefault(l => l.name == "Sock_Light_Window");
                        if (wl != null) lights.Add(wl);
                        sl.LampFrom = 19 * 60; sl.LampTo = 60;
                        note = $"시우네: 이불(밤 펼침·낮 갬)·하늘 밤엔 잠 · 책상·스탠드(19:00~01:00) · 창빛 {lights.Count}";
                        break;
                    }
                default:
                    Object.DestroyImmediate(life.gameObject);
                    if (sl != null && sl.Slots.Length == 0) Object.DestroyImmediate(sl);
                    return "";
            }
            sl.Slots = slots.ToArray(); sl.Windows = lights.ToArray(); sl.Lamps = lamps.ToArray(); sl.Steam = steam.ToArray();
            return note + $" · 사람 {npcs}";
        }

        static void Cyl(Transform parent, string name, Vector3 local, Vector3 size, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local; go.transform.localScale = size;
            var r = go.GetComponent<Renderer>(); r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        static void Box(Transform parent, string name, Vector3 local, Vector3 size, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local; go.transform.localScale = size;
            var r = go.GetComponent<Renderer>(); r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        static Material Flat(string name, Color c)
        {
            var m = EnvSetup.Toon($"{MatDir}/{name}.mat", null, Vector2.one, Vector2.zero, c, true);
            m.SetColor("_1st_ShadeColor", c * new Color(0.78f, 0.76f, 0.74f)); m.SetColor("_2nd_ShadeColor", c * new Color(0.78f, 0.76f, 0.74f));
            EditorUtility.SetDirty(m);
            return m;
        }

        static ParticleSystem Steam(Transform parent, Vector3 local)
        {
            var go = new GameObject("김");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 2.8f; main.startSpeed = 0.4f; main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
            main.startColor = new Color(1f, 1f, 1f, 0.6f); main.maxParticles = 60; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.02f; main.playOnAwake = true; main.loop = true; main.prewarm = true;
            var em = ps.emission; em.rateOverTime = 11f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 12f; sh.radius = 0.22f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var gr = new Gradient(); gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.7f, 0.2f), new GradientAlphaKey(0f, 1f) });
            col.color = gr;
            var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.8f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = SteamMat();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            return ps;
        }

        static Material steamMat;
        static Material SteamMat()
        {
            if (steamMat != null) return steamMat;
            if (!File.Exists(SteamTex))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SteamTex));
                var t = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
                    {
                        float d = new Vector2(x - 31.5f, y - 31.5f).magnitude / 31.5f;
                        float a = Mathf.Clamp01(1f - d); a = a * a * (3f - 2f * a);
                        t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                File.WriteAllBytes(SteamTex, t.EncodeToPNG());
                AssetDatabase.ImportAsset(SteamTex);
            }
            string p = $"{MatDir}/M_World_Steam.mat";
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            steamMat = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (steamMat == null) { steamMat = new Material(sh) { name = "M_World_Steam" }; AssetDatabase.CreateAsset(steamMat, p); }
            if (sh != null && steamMat.shader != sh) steamMat.shader = sh;
            steamMat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(SteamTex));
            steamMat.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.55f));
            steamMat.SetFloat("_Surface", 1f); steamMat.SetFloat("_Blend", 0f);
            steamMat.SetOverrideTag("RenderType", "Transparent");
            steamMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha); steamMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha); steamMat.SetInt("_ZWrite", 0);
            steamMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            steamMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(steamMat);
            return steamMat;
        }
    }
}
