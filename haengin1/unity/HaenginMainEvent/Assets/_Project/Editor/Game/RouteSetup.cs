// 행인1의 메인이벤트 — 체크포인트 길잡이·이름표·HUD 를 Zone1 장면에 붙인다 (06 문서 8·9장)
// Zone1Builder 가 만든 부품(Zone1/Route/CPxx/원판·테두리·기둥·깃발·빛 기둥·이름표, 곳곳의 '이름표' TMP)을
// 런타임 컴포넌트에 연결한다:
//   Zone1/Route  ← RouteGuide(체크포인트를 순서대로 켬)   Zone1  ← NameTags(이름표 빌보드·거리 숨김)
//   장면 루트 'HUD 길잡이'(Canvas + RouteHud: 화면 위 "다음: <이름> · NNm", TMP UGUI 한글 글꼴)
// Zone1Builder.BeforeSave 확장 지점에 등록([InitializeOnLoad]) → Zone1 을 다시 만들 때마다 다시 붙는다(M1Setup 과 같은 방식).
using System;
using System.Collections.Generic;
using System.Linq;
using Haengin.EditorTools;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Haengin.EditorGame
{
    [InitializeOnLoad]
    public static class RouteSetup
    {
        const string Tag = "[RouteSetup]";
        public const string HudName = "HUD 길잡이";

        static readonly Color Ink = new Color(0.102f, 0.078f, 0.090f);        // #1A1417
        static readonly Color Paper = new Color(0.957f, 0.937f, 0.902f, 0.90f); // #F4EFE6, 90%
        static readonly Color Accent = new Color(0.886f, 0.345f, 0.173f);      // #E2582C 길잡이 주황

        /// 이름표 종류별 (보이는 거리 m, 우선순위 — 작을수록 겹칠 때 남음). 체크포인트는 지금 목표만 켜지므로 멀리서도·가려도 보이게.
        static readonly Dictionary<string, (float dist, int prio)> Rule = new Dictionary<string, (float, int)>
        {
            { "cp", (160f, 0) }, { "wall", (60f, 1) }, { "gate", (45f, 1) }, { "viewpoint", (70f, 1) }, { "wall_gate", (30f, 2) },
            { "bus_stop", (20f, 3) }, { "sign", (20f, 3) }, { "door", (14f, 4) }, { "board", (10f, 5) },
        };

        static RouteSetup()
        {
            Zone1Builder.BeforeSave -= OnZone1Built;
            Zone1Builder.BeforeSave += OnZone1Built;
        }

        [MenuItem("Haengin/Zone1 에 길잡이·이름표·HUD 다시 붙이기")]
        public static void AttachToOpenZone1()
        {
            var scene = EditorSceneManager.OpenScene(M1Setup.Zone1Scene, OpenSceneMode.Single);
            var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Zone1")
                       ?? throw new Exception("Zone1 장면에 'Zone1' 루트가 없습니다");
            Attach(Zone1Data.Load(), root.transform);
            EditorSceneManager.SaveScene(scene);
        }

        static void OnZone1Built(Zone1Data d, Transform zoneRoot) => Attach(d, zoneRoot);

        public static void Attach(Zone1Data d, Transform zoneRoot)
        {
            var scene = zoneRoot.gameObject.scene;
            var route = zoneRoot.Find("Route") ?? throw new Exception("Zone1/Route 가 없습니다");

            // 1) RouteGuide: 체크포인트 부품 연결
            var guide = route.GetComponent<RouteGuide>() ?? route.gameObject.AddComponent<RouteGuide>();
            var stops = new List<RouteGuide.Stop>();
            for (int i = 0; i < d.Route.Count; i++)
            {
                var cp = d.Route[i];
                var t = route.Cast<Transform>().FirstOrDefault(c => c.name.StartsWith($"CP{i + 1:00} "));
                if (t == null) throw new Exception($"체크포인트 {i + 1} 오브젝트가 없습니다");
                GameObject Part(string n) => t.Find(n)?.gameObject;
                stops.Add(new RouteGuide.Stop
                {
                    Name = cp.Name,
                    Label = Zone1Builder.LabelText(cp.Name, false),
                    Pos = cp.Pos,
                    Radius = cp.Radius,
                    Disc = Part(Zone1Builder.CpDisc)?.GetComponent<MeshRenderer>(),
                    Ring = Part(Zone1Builder.CpRing),
                    Pillar = Part(Zone1Builder.CpPillar),
                    Flag = Part(Zone1Builder.CpFlag),
                    Beam = Part(Zone1Builder.CpBeam),
                    Tag = Part(Zone1Builder.CpTag),
                });
            }
            guide.Stops = stops.ToArray();
            guide.StartIndex = Zone1Builder.RouteStartIndex;
            guide.ActiveDisc = AssetDatabase.LoadAssetAtPath<Material>(Zone1Builder.MatDir + "/M_Z1_cp_fill.mat");
            guide.DoneDisc = AssetDatabase.LoadAssetAtPath<Material>(Zone1Builder.MatDir + "/M_Z1_cp_done.mat");
            if (guide.ActiveDisc == null || guide.DoneDisc == null) throw new Exception("체크포인트 원판 재질(M_Z1_cp_fill / cp_done)이 없습니다");

            // 2) NameTags: Zone1 아래 모든 이름표
            var tags = zoneRoot.GetComponent<NameTags>() ?? zoneRoot.gameObject.AddComponent<NameTags>();
            var list = new List<NameTags.Tag>();
            foreach (var tmp in zoneRoot.GetComponentsInChildren<TextMeshPro>(true))
            {
                string kind = KindOf(tmp.transform, zoneRoot);
                (float dist, int prio) r = Rule.TryGetValue(kind, out var rr) ? rr : (30f, 4);
                list.Add(new NameTags.Tag { Text = tmp, Kind = kind, MaxDist = r.dist, Priority = r.prio, Always = kind == "cp" });
            }
            tags.Tags = list.ToArray();

            // 3) HUD
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFont.AssetPath) ?? throw new Exception("한글 TMP 글꼴 에셋이 없습니다: " + KoreanFont.AssetPath);
            foreach (var g in scene.GetRootGameObjects()) if (g.name == HudName) Object.DestroyImmediate(g);
            var hud = BuildHud(scene, font, guide, d);

            EditorUtility.SetDirty(guide); EditorUtility.SetDirty(tags);
            EditorSceneManager.MarkSceneDirty(scene);
            var byKind = list.GroupBy(x => x.Kind).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => $"{g.Key} {g.Count()}");
            Debug.Log($"{Tag} 길잡이 체크포인트 {stops.Count}개(시작 목표 {guide.StartIndex + 1}번, 기둥 있는 것 {stops.Count(s => s.Pillar != null)}) · " +
                      $"이름표 {list.Count}개({string.Join(", ", byKind)}) · HUD '{hud.name}' 첫 글 \"{hud.GetComponent<RouteHud>().Current}\"");
        }

        static string KindOf(Transform t, Transform zoneRoot)
        {
            // 경로: Zone1/Route/CPxx/이름표 · Zone1/Walls/<성곽>/이름표 자리/이름표 · Zone1/Landmarks/<kind>/<이름>/이름표
            var path = new List<string>();
            for (var p = t; p != null && p != zoneRoot; p = p.parent) path.Insert(0, p.name);
            if (path.Count == 0) return "?";
            return path[0] switch
            {
                "Route" => "cp",
                "Walls" => "wall",
                "Landmarks" when path.Count > 1 => path[1],
                _ => path[0],
            };
        }

        /// 화면 위 가운데 작은 판: 주황 점 + "다음: 후문 상가거리 · 25m". 1920×1080 기준(화면 높이에 맞춰 늘고 줄음).
        static GameObject BuildHud(UnityEngine.SceneManagement.Scene scene, TMP_FontAsset font, RouteGuide guide, Zone1Data d)
        {
            var root = new GameObject(HudName, typeof(RectTransform)) { layer = 5 }; // UI
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            // 판(가로는 글 길이에 맞춤)
            var plate = new GameObject("판", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter)) { layer = 5 };
            plate.transform.SetParent(root.transform, false);
            var prt = (RectTransform)plate.transform;
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 1f);
            prt.pivot = new Vector2(0.5f, 1f);
            prt.anchoredPosition = new Vector2(0f, -28f);
            var img = plate.GetComponent<Image>();
            img.color = Paper;
            img.raycastTarget = false;
            var lay = plate.GetComponent<HorizontalLayoutGroup>();
            lay.padding = new RectOffset(16, 20, 7, 8);
            lay.spacing = 11f;
            lay.childAlignment = TextAnchor.MiddleCenter;
            lay.childControlWidth = lay.childControlHeight = true;
            lay.childForceExpandWidth = lay.childForceExpandHeight = false;
            var fit = plate.GetComponent<ContentSizeFitter>();
            fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 왼쪽 주황 막대(체크포인트 색과 같은 길잡이 색)
            var dot = new GameObject("길잡이 색", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement)) { layer = 5 };
            dot.transform.SetParent(plate.transform, false);
            var dimg = dot.GetComponent<Image>();
            dimg.color = Accent; dimg.raycastTarget = false;
            var le = dot.GetComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = 6f; le.preferredHeight = le.minHeight = 26f;

            var txtGo = new GameObject("글", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)) { layer = 5 };
            txtGo.transform.SetParent(plate.transform, false);
            var txt = txtGo.GetComponent<TextMeshProUGUI>();
            txt.font = font;
            txt.fontSize = 26f;
            txt.color = Ink;
            txt.alignment = TextAlignmentOptions.MidlineLeft;
            txt.textWrappingMode = TextWrappingModes.NoWrap;
            txt.raycastTarget = false;

            var hud = root.AddComponent<RouteHud>();
            hud.Text = txt;
            hud.Plate = plate;
            hud.Guide = guide;

            // 저장 상태 = 시작 때 글(실행하면 RouteHud 가 매 프레임 갱신)
            var first = guide.Stops.Length > guide.StartIndex ? guide.Stops[guide.StartIndex] : null;
            float dist = first != null ? new Vector2(first.Pos.x - d.SpawnPos.x, first.Pos.z - d.SpawnPos.z).magnitude : 0f;
            hud.Show(first != null ? $"다음: {first.Label} · {Mathf.RoundToInt(dist)}m" : "");
            return root;
        }
    }
}
