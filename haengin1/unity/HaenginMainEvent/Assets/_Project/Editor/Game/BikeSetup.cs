// 행인1의 메인이벤트 — 배달 자전거 설정(docs/09_M3_버티컬슬라이스_설계.md 2-6, 6-1 8단계)
// ① 자전거 모델: Art/Bike/DeliveryBike.fbx(tools/bike_split.py — 소품 delivery_bicycle.glb 를 차체·바퀴살·크랭크로 나눔) + 소품 재질 → Prefabs/Bike/DeliveryBike.prefab(BikeVisual)
// ② 애니메이터: Anim/Siwoo.controller 바닥 층에 'Ride' 상태(동작 33 Chair_Sit_Idle_M = Art/Clips/M3/ChairSit.fbx) + IK 패스
//    2026-10-26(docs/14 6-4): 페달 클립 4종(concept/art/3d/anim/bike — tools/bike_anim.py, 자전거 안장 +12cm·크랭크 0.16m 뒤) → Art/Clips/Bike/ Humanoid
//    + 상태 BikeCruise·BikeClimb(Motion Time = BikePhase) · BikeCoast · BikeStart·BikeStop(Motion Time = BikeT, startstop 나눔) — 발 IK 켬
//    + 곡선 에셋 Settings/BikeClips.asset(bike_curves.json — θ·기울기·IK 목표). 'Ride'(의자 앉기)는 예전 판 호환으로 남김
// ③ 플레이어 프리팹(M1Setup.BuildPlayerPrefab 이 부름): BikeMotor(꺼 둠) · BikeInput · SoupGauge, 의상 모델마다 BikeRider
// ④ Zone1(M1Setup.AttachRig 가 부름): 루트 'Bike' = BikeCourse(1화 코스·정차 2·끝) · CM_Bike(BikeCam) · 주행 화면(BikeHud) · 자전거(꺼 둠) · 태오(PathRunner, 꺼 둠)
// batchmode: -executeMethod Haengin.EditorGame.BikeSetup.Build (에셋 → 플레이어 프리팹 → Zone1 다시 만들기)
using System;
using System.IO;
using System.Linq;
using Haengin.EditorTools;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace Haengin.EditorGame
{
    public static class BikeSetup
    {
        const string Tag = "[BikeSetup]";
        const string Root = "Assets/_Project";
        public const string BikeFbx = Root + "/Art/Bike/DeliveryBike.fbx";
        public const string BikePrefab = Root + "/Prefabs/Bike/DeliveryBike.prefab";
        public const string BikeMat = Root + "/Materials/Env/Props/M_P_delivery_bicycle.mat";
        public const string SitFbx = Root + "/Art/Clips/M3/ChairSit.fbx";
        public const string ClipDir = Root + "/Art/Clips/Bike", ClipsAsset = Root + "/Settings/BikeClips.asset";
        static string SrcClips => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../concept/art/3d/anim/bike"));

        /// 1화 코스(x, z — 높이는 실행 때 땅에서): 국밥집 앞 → 경비실(정차 0) → 와룡공원길 → 와룡 오르막 → 꼭대기 집(정차 1) → 같은 길 → 성균관로 동쪽 끝
        static readonly Vector2[] RouteXZ =
        {
            new Vector2(-184.0f, 102.4f), new Vector2(-188.5f, 100.6f), new Vector2(-196.0f, 98.4f), new Vector2(-200.0f, 96.8f), new Vector2(-202.4f, 95.9f),
            new Vector2(-206.5f, 96.6f), new Vector2(-212.0f, 97.8f), new Vector2(-219.0f, 96.9f), new Vector2(-226.0f, 96.0f), new Vector2(-233.0f, 95.0f),
            new Vector2(-240.0f, 94.0f), new Vector2(-245.5f, 96.5f), new Vector2(-251.0f, 99.0f), new Vector2(-255.8f, 103.5f), new Vector2(-260.5f, 108.0f),
            new Vector2(-262.3f, 113.3f), new Vector2(-264.0f, 118.5f), new Vector2(-263.5f, 122.8f), new Vector2(-263.3f, 125.6f),
            new Vector2(-263.6f, 121.5f), new Vector2(-264.0f, 118.5f), new Vector2(-262.3f, 113.3f), new Vector2(-260.5f, 108.0f), new Vector2(-255.8f, 103.5f),
            new Vector2(-251.0f, 99.0f), new Vector2(-245.5f, 96.5f), new Vector2(-240.0f, 94.0f), new Vector2(-233.0f, 95.0f), new Vector2(-226.0f, 96.0f),
            new Vector2(-219.0f, 96.9f), new Vector2(-212.0f, 97.8f), new Vector2(-206.0f, 98.4f), new Vector2(-200.0f, 98.6f), new Vector2(-193.0f, 99.3f),
            new Vector2(-186.0f, 100.0f), new Vector2(-180.5f, 98.8f), new Vector2(-175.0f, 97.6f), new Vector2(-167.5f, 95.3f), new Vector2(-160.0f, 93.0f),
            new Vector2(-153.0f, 90.8f), new Vector2(-146.0f, 88.6f), new Vector2(-139.5f, 86.4f), new Vector2(-134.5f, 85.0f),
        };
        static readonly int[] StopAt = { 4, 18 };
        static readonly Vector2[] TaeoXZ =
        {
            new Vector2(-222.0f, 96.4f), new Vector2(-226.0f, 96.0f), new Vector2(-233.0f, 95.0f), new Vector2(-240.0f, 94.0f), new Vector2(-245.5f, 96.5f),
            new Vector2(-251.0f, 99.0f), new Vector2(-255.8f, 103.5f), new Vector2(-260.5f, 108.0f), new Vector2(-262.3f, 113.3f), new Vector2(-264.0f, 118.5f),
            new Vector2(-263.5f, 122.8f), new Vector2(-263.0f, 127.0f), new Vector2(-261.6f, 129.0f),
        };

        public static void Build()
        {
            int code = 0;
            try
            {
                Assets();
                M1Setup.BuildPlayerPrefab();
                Zone1Builder.Rebuild();
                AssetDatabase.SaveAssets();
            }
            catch (Exception e) { Debug.LogError($"{Tag} 실패: {e}"); code = 1; }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        public static void Assets()
        {
            BikeModel();
            EnsureRide();
            PedalClips();
        }

        // ───────────────────────── ① 자전거 모델
        static void BikeModel()
        {
            var mi = AssetImporter.GetAtPath(BikeFbx) as ModelImporter ?? throw new Exception("자전거 FBX 가 없습니다: " + BikeFbx + " (tools/bike_split.py)");
            bool dirty = false;
            if (mi.animationType != ModelImporterAnimationType.None) { mi.animationType = ModelImporterAnimationType.None; dirty = true; }
            if (mi.importAnimation) { mi.importAnimation = false; dirty = true; }
            if (mi.materialImportMode != ModelImporterMaterialImportMode.None) { mi.materialImportMode = ModelImporterMaterialImportMode.None; dirty = true; }
            if (mi.importCameras || mi.importLights) { mi.importCameras = mi.importLights = false; dirty = true; }
            if (dirty) mi.SaveAndReimport();
            var model = CharSetup.Load<GameObject>(BikeFbx);
            var inst = (PrefabUtility.InstantiatePrefab(model) as GameObject) ?? Object.Instantiate(model);
            inst.name = "DeliveryBike";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(BikeMat);
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                if (mat != null) { var ms = r.sharedMaterials; for (int i = 0; i < ms.Length; i++) ms[i] = mat; r.sharedMaterials = ms; }
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.gameObject.layer = Layers.Player;
            }
            foreach (var t in inst.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layers.Player;
            Transform F(string n) => inst.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == n);
            var v = inst.GetComponent<BikeVisual>() ?? inst.AddComponent<BikeVisual>();
            v.WheelF = F("WheelF"); v.WheelR = F("WheelR"); v.Crank = F("Crank"); v.Saddle = F("Saddle"); v.FootDown = F("FootDown");
            // 이름의 좌우는 Blender 축 기준이라 Unity 에서 x 부호로 다시 고른다(왼쪽 = −x)
            var g1 = F("GripL"); var g2 = F("GripR");
            bool gl = g1 != null && g2 != null && inst.transform.InverseTransformPoint(g1.position).x < inst.transform.InverseTransformPoint(g2.position).x;
            v.GripL = gl ? g1 : g2; v.GripR = gl ? g2 : g1;
            var p1 = F("PedalL"); var p2 = F("PedalR");
            bool pl = p1 != null && p2 != null && inst.transform.InverseTransformPoint(p1.position).x < inst.transform.InverseTransformPoint(p2.position).x;
            v.PedalL = pl ? p1 : p2; v.PedalR = pl ? p2 : p1;
            if (v.FootDown != null && inst.transform.InverseTransformPoint(v.FootDown.position).x > 0f)
            {
                var lp = v.FootDown.localPosition; v.FootDown.localPosition = new Vector3(-lp.x, lp.y, lp.z);
            }
            var json = Path.ChangeExtension(Path.Combine(Path.GetDirectoryName(Application.dataPath), BikeFbx), ".json");
            string anchors = "FBX 빈 점";
            if (File.Exists(json) && MiniJson.Parse(File.ReadAllText(json)) is System.Collections.Generic.Dictionary<string, object> j)
            {
                if (j.TryGetValue("WheelF", out var wf) && wf is System.Collections.Generic.Dictionary<string, object> a) v.RadiusF = Convert.ToSingle(a["r"]);
                if (j.TryGetValue("WheelR", out var wr) && wr is System.Collections.Generic.Dictionary<string, object> b) v.RadiusR = Convert.ToSingle(b["r"]);
                // FBX 빈 점이 Unity 에서 안 들어올 때(2026-10-25 확인 — 안장·손잡이 없음): bike_split.py 가 남긴 Blender 좌표에서 직접 만든다
                //   Blender(x, y, z) → Unity(−x, z, −y) — 앞 = Blender −Y = Unity +Z, 탄 사람 왼쪽 = Blender +X = Unity −X
                if (v.Saddle == null || v.GripL == null || v.PedalL == null)
                {
                    Vector3 U(System.Collections.Generic.List<object> p) => new Vector3(-Convert.ToSingle(p[0]), Convert.ToSingle(p[2]), -Convert.ToSingle(p[1]));
                    Transform Anchor(string n, Transform parent, Vector3 world)
                    {
                        var old = parent.Find(n);
                        var t = old != null ? old : new GameObject(n).transform;
                        t.SetParent(parent, false);
                        t.position = inst.transform.TransformPoint(world);
                        return t;
                    }
                    var grips = (System.Collections.Generic.List<object>)j["grips"];
                    var g0 = U((System.Collections.Generic.List<object>)grips[0]); var gg1 = U((System.Collections.Generic.List<object>)grips[1]);
                    v.Saddle = Anchor("Saddle", inst.transform, U((System.Collections.Generic.List<object>)j["saddle"]));
                    v.GripL = Anchor("GripL", inst.transform, g0.x < gg1.x ? g0 : gg1);
                    v.GripR = Anchor("GripR", inst.transform, g0.x < gg1.x ? gg1 : g0);
                    var bb = (System.Collections.Generic.Dictionary<string, object>)j["BB"];
                    var pa = U((System.Collections.Generic.List<object>)bb["pedalL"]); var pb = U((System.Collections.Generic.List<object>)bb["pedalR"]);
                    var crank = v.Crank != null ? v.Crank : inst.transform;
                    v.PedalL = Anchor("PedalL", crank, pa.x < pb.x ? pa : pb);
                    v.PedalR = Anchor("PedalR", crank, pa.x < pb.x ? pb : pa);
                    var left = pa.x < pb.x ? pa : pb;
                    v.FootDown = Anchor("FootDown", inst.transform, new Vector3(left.x - 0.12f, 0.0f, Convert.ToSingle(bb["y"]) * -1f + 0.05f));
                    anchors = "json 에서 만듦";
                }
            }
            // 2026-10-26(docs/14 6-4): 페달 빈 점은 Crank 메시의 자식이라 FBX 를 들여오면 자리가 틀어진다(Unity 실측 — 크랭크 축에서 40cm 뒤·반지름도 다름, bike_anim 의 함정 ①과 같음).
            //   발 IK 가 이 빈 점을 따라가 발이 페달에서 30cm 넘게 떨어졌다 → json(BB·페달 가운데)으로 크랭크 기준 자리를 늘 다시 놓는다.
            if (File.Exists(json) && MiniJson.Parse(File.ReadAllText(json)) is System.Collections.Generic.Dictionary<string, object> jj && v.Crank != null && v.PedalL != null && v.PedalR != null)
            {
                Vector3 U(System.Collections.Generic.List<object> p) => new Vector3(-Convert.ToSingle(p[0]), Convert.ToSingle(p[2]), -Convert.ToSingle(p[1]));
                var bbj = (System.Collections.Generic.Dictionary<string, object>)jj["BB"];
                var pa = U((System.Collections.Generic.List<object>)bbj["pedalL"]); var pb = U((System.Collections.Generic.List<object>)bbj["pedalR"]);
                var left = pa.x < pb.x ? pa : pb; var right = pa.x < pb.x ? pb : pa;
                var crankRot = v.Crank.localRotation; v.Crank.localRotation = Quaternion.identity;
                v.PedalL.position = inst.transform.TransformPoint(left); v.PedalR.position = inst.transform.TransformPoint(right);
                v.Crank.localRotation = crankRot;
                anchors += $" · 페달 json(L {v.Crank.InverseTransformPoint(v.PedalL.position):F3} R {v.Crank.InverseTransformPoint(v.PedalR.position):F3} · 크랭크 축 {v.Crank.localPosition:F3})";
            }
            Debug.Log($"{Tag} 자전거 부품: 앞바퀴 {(v.WheelF != null ? v.WheelF.localPosition.ToString("F2") : "없음")} · 뒷바퀴 {(v.WheelR != null ? v.WheelR.localPosition.ToString("F2") : "없음")} · 크랭크 {(v.Crank != null ? v.Crank.localPosition.ToString("F2") : "없음")} · 기준점 {anchors} · 자식 [{string.Join(",", inst.GetComponentsInChildren<Transform>(true).Select(t => t.name))}]");
            Directory.CreateDirectory(Path.GetDirectoryName(BikePrefab));
            var b0 = new Bounds(inst.transform.position, Vector3.zero);
            foreach (var r in inst.GetComponentsInChildren<Renderer>()) b0.Encapsulate(r.bounds);
            PrefabUtility.SaveAsPrefabAsset(inst, BikePrefab, out bool ok);
            Object.DestroyImmediate(inst);
            if (!ok) throw new Exception("자전거 프리팹 저장 실패");
            Debug.Log($"{Tag} 자전거 프리팹 {BikePrefab}: 경계 {b0.size} · 바퀴 반지름 {v.RadiusF:F3}/{v.RadiusR:F3} · 안장 {(v.Saddle != null ? v.Saddle.localPosition.ToString("F2") : "없음")} · 손잡이 L {(v.GripL != null ? v.GripL.localPosition.ToString("F2") : "-")} R {(v.GripR != null ? v.GripR.localPosition.ToString("F2") : "-")} · 재질 {(mat != null ? mat.name : "없음")}");
        }

        // ───────────────────────── ② Ride 상태
        static void EnsureRide()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(SitFbx) == null) { Debug.LogWarning($"{Tag} 앉기 클립 없음 — Ride 상태를 못 만듦"); return; }
            var sit = AssetDatabase.LoadAllAssetsAtPath(SitFbx).OfType<AnimationClip>().FirstOrDefault(c => c.name == "ChairSit");
            if (sit == null) { CharSetup.Human(SitFbx, "ChairSit", -1, -1, false, null); sit = CharSetup.Clip(SitFbx, "ChairSit"); }
            var ctrl = CharSetup.Load<AnimatorController>(CharSetup.SiwooCtrl);
            var layers = ctrl.layers;
            layers[0].iKPass = true;
            ctrl.layers = layers;
            var sm = ctrl.layers[0].stateMachine;
            var st = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == BikeRider.RideState) ?? sm.AddState(BikeRider.RideState);
            st.motion = sit;
            st.iKOnFeet = false;
            st.writeDefaultValues = true;
            EditorUtility.SetDirty(st); EditorUtility.SetDirty(ctrl);
            Debug.Log($"{Tag} {CharSetup.SiwooCtrl}: 'Ride' 상태(ChairSit {sit.length:F2}s) · 바닥 층 IK 패스 켬");
        }

        // ───────────────────────── ②-2 페달 클립(docs/14 6-4)
        static void PedalClips()
        {
            string src = SrcClips;
            if (!Directory.Exists(src)) { Debug.LogWarning($"{Tag} 페달 클립 원본 없음: {src}"); return; }
            string proj = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            Directory.CreateDirectory(Path.Combine(proj, ClipDir));
            foreach (var n in new[] { "cruise", "climb", "coast", "startstop" })
                File.Copy(Path.Combine(src, $"siwoo_bike_{n}.fbx"), Path.Combine(proj, ClipDir, $"siwoo_bike_{n}.fbx"), true);
            AssetDatabase.Refresh();
            CharSetup.Human($"{ClipDir}/siwoo_bike_cruise.fbx", BikeRider.CruiseState, -1, -1);
            CharSetup.Human($"{ClipDir}/siwoo_bike_climb.fbx", BikeRider.ClimbState, -1, -1);
            CharSetup.Human($"{ClipDir}/siwoo_bike_coast.fbx", BikeRider.CoastState, -1, -1);
            var json = MiniJson.Parse(File.ReadAllText(Path.Combine(src, "bike_curves.json"))) as System.Collections.Generic.Dictionary<string, object> ?? throw new Exception("bike_curves.json 읽기 실패");
            var clipsJ = (System.Collections.Generic.Dictionary<string, object>)json["clips"];
            var ssJ = (System.Collections.Generic.Dictionary<string, object>)clipsJ["startstop"];
            var segs = (System.Collections.Generic.Dictionary<string, object>)ssJ["segments"];
            int Seg(string k, int i) => Convert.ToInt32(((System.Collections.Generic.List<object>)segs[k])[i]);
            ModelImporterClipAnimation Sub(string name, int a, int b) => new ModelImporterClipAnimation
            {
                name = name, firstFrame = a, lastFrame = b, loopTime = false, loopPose = false, wrapMode = WrapMode.ClampForever,
                lockRootRotation = true, keepOriginalOrientation = true, lockRootHeightY = true, keepOriginalPositionY = true, heightFromFeet = false,
                lockRootPositionXZ = true, keepOriginalPositionXZ = true, maskType = ClipAnimationMaskType.None,
            };
            CharSetup.Human($"{ClipDir}/siwoo_bike_startstop.fbx", BikeRider.StartState, -1, -1, false,
                new[] { Sub(BikeRider.StartState, Seg("start", 0), Seg("start", 1)), Sub(BikeRider.StopState, Seg("stop", 0), Seg("stop", 1)) });
            // 곡선 에셋
            var asset = AssetDatabase.LoadAssetAtPath<BikeClips>(ClipsAsset);
            if (asset == null) { asset = ScriptableObject.CreateInstance<BikeClips>(); AssetDatabase.CreateAsset(asset, ClipsAsset); }
            BikeClips.Track T(string key)
            {
                var c = (System.Collections.Generic.Dictionary<string, object>)clipsJ[key];
                float[] Arr(string k) => ((System.Collections.Generic.List<object>)c[k]).Select(Convert.ToSingle).ToArray();
                var ik = ((System.Collections.Generic.List<object>)c["ik"]).SelectMany(r => ((System.Collections.Generic.List<object>)r).Select(Convert.ToSingle)).ToArray();
                return new BikeClips.Track { Name = key, Frames = Convert.ToInt32(c["frames"]), Fps = Convert.ToSingle(c["fps"]), Loop = Convert.ToBoolean(c["loop"]), Theta = Arr("theta"), Lean = Arr("lean"), Ik = ik };
            }
            asset.Cruise = T("cruise"); asset.Climb = T("climb"); asset.Coast = T("coast"); asset.StartStop = T("startstop");
            asset.StartEnd = Seg("start", 1); asset.StopFrom = Seg("stop", 0);
            asset.CrankLen = Convert.ToSingle(json["crank_len"]);
            asset.CoastTheta = asset.Coast.Theta.Length > 0 ? asset.Coast.Theta[0] : 115.5f;
            float phi0 = Convert.ToSingle(json["phi0_right_pedal_deg"]);
            asset.ClimbLeanAmp = 8f; asset.ClimbLeanPhase = 10f - phi0;      // bike_anim: ψ = 8·sin(φR − 10°), φR = φ0 + θ
            EditorUtility.SetDirty(asset);
            // 애니메이터 상태
            var ctrl = CharSetup.Load<AnimatorController>(CharSetup.SiwooCtrl);
            foreach (var pn in new[] { BikeRider.PhaseParam, BikeRider.TimeParam })
                if (!ctrl.parameters.Any(x => x.name == pn)) ctrl.AddParameter(pn, AnimatorControllerParameterType.Float);
            var layers = ctrl.layers; layers[0].iKPass = true; ctrl.layers = layers;
            var sm = ctrl.layers[0].stateMachine;
            void State(string name, string fbx, string clipName, string timeParam)
            {
                var clip = CharSetup.Clip(fbx, clipName) ?? throw new Exception($"클립 없음: {fbx} {clipName}");
                var st = sm.states.Select(x => x.state).FirstOrDefault(x => x.name == name) ?? sm.AddState(name);
                st.motion = clip; st.iKOnFeet = true; st.writeDefaultValues = true;
                st.timeParameterActive = timeParam != null; st.timeParameter = timeParam ?? "";
                EditorUtility.SetDirty(st);
            }
            State(BikeRider.CruiseState, $"{ClipDir}/siwoo_bike_cruise.fbx", BikeRider.CruiseState, BikeRider.PhaseParam);
            State(BikeRider.ClimbState, $"{ClipDir}/siwoo_bike_climb.fbx", BikeRider.ClimbState, BikeRider.PhaseParam);
            State(BikeRider.CoastState, $"{ClipDir}/siwoo_bike_coast.fbx", BikeRider.CoastState, null);
            State(BikeRider.StartState, $"{ClipDir}/siwoo_bike_startstop.fbx", BikeRider.StartState, BikeRider.TimeParam);
            State(BikeRider.StopState, $"{ClipDir}/siwoo_bike_startstop.fbx", BikeRider.StopState, BikeRider.TimeParam);
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            Debug.Log($"{Tag} 페달 클립: 순항 {asset.Cruise.Frames}·댄싱 {asset.Climb.Frames}·관성 {asset.Coast.Frames}·출발/정지 {asset.StartStop.Frames} 키 · 크랭크 {asset.CrankLen:F3}m · 관성 θ {asset.CoastTheta:F1}° · 댄싱 기울기 8·sin(θ−{asset.ClimbLeanPhase:F1}°) · 상태 5 → {ClipsAsset}");
        }

        // ───────────────────────── ③ 플레이어
        public static void AddToPlayer(GameObject player)
        {
            var motor = player.GetComponent<BikeMotor>() ?? player.AddComponent<BikeMotor>();
            var soup = player.GetComponent<SoupGauge>() ?? player.AddComponent<SoupGauge>();
            motor.Soup = soup;
            motor.enabled = false;
            var inp = player.GetComponent<BikeInput>() ?? player.AddComponent<BikeInput>();
            inp.Actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(M1Setup.InputPath);
            inp.Motor = motor;
            int n = 0;
            foreach (var a in player.GetComponentsInChildren<Animator>(true))
            {
                var rd = a.GetComponent<BikeRider>() ?? a.gameObject.AddComponent<BikeRider>();
                rd.Motor = motor;
                rd.Clips = AssetDatabase.LoadAssetAtPath<BikeClips>(ClipsAsset);
                rd.enabled = false;
                n++;
            }
            Debug.Log($"{Tag} 플레이어 자전거 부품: BikeMotor(꺼 둠) · SoupGauge · BikeInput · BikeRider {n}(의상마다)");
        }

        // ───────────────────────── ④ Zone1
        public static void AddToZone1(RigFactory.Rig rig, Transform zoneRoot, Zone1Data d)
        {
            var scene = zoneRoot.gameObject.scene;
            foreach (var g in scene.GetRootGameObjects()) if (g.name == "Bike") Object.DestroyImmediate(g);
            var root = new GameObject("Bike");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            var course = root.AddComponent<BikeCourse>();
            float Y(Vector2 xz) => d.Grid != null ? Ground(d, xz) : 30f;
            course.Route = RouteXZ.Select(p => new Vector3(p.x, Y(p), p.y)).ToArray();
            course.RouteStop = Enumerable.Range(0, RouteXZ.Length).Select(i => Array.IndexOf(StopAt, i)).ToArray();
            course.Stops = new[]
            {
                new BikeCourse.Stop { Name = "후문 경비실", Caller = "경비실 아저씨", Pos = course.Route[StopAt[0]], Radius = 2.4f, Appt = 0 },
                new BikeCourse.Stop { Name = "꼭대기 집", Caller = "할아버지", Pos = course.Route[StopAt[1]], Radius = 2.4f, Appt = 6 * 3600 + 5 * 60 },
            };
            course.Start = course.Route[0];
            var d0 = course.Route[1] - course.Route[0];
            course.StartYaw = Mathf.Atan2(d0.x, d0.z) * Mathf.Rad2Deg;
            course.End = course.Route[course.Route.Length - 1];
            course.ClimbStart = new Vector3(-237.0f, Y(new Vector2(-237f, 94.4f)), 94.4f);
            course.Motor = rig.Player.GetComponent<BikeMotor>();
            course.In = rig.Player.GetComponent<BikeInput>();
            course.Soup = rig.Player.GetComponent<SoupGauge>();
            course.Actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(M1Setup.InputPath);
            // 카메라
            var camGo = new GameObject("CM_Bike");
            camGo.transform.SetParent(root.transform, false);
            var cm = camGo.AddComponent<CinemachineCamera>();
            cm.Priority = 0;
            var lens = LensSettings.Default; lens.FieldOfView = 50f; lens.NearClipPlane = 0.1f; lens.FarClipPlane = 700f; cm.Lens = lens;
            var bc = camGo.AddComponent<BikeCam>();
            bc.Cam = cm; bc.Bike = course.Motor; bc.Soup = course.Soup;
            course.Cam = bc;
            if (course.In != null) course.In.Cam = bc;
            // 화면
            var fonts = AssetDatabase.LoadAssetAtPath<UiFonts>(StorySetup.FontsPath);
            var hudGo = new GameObject("주행 화면") { layer = 5 };
            hudGo.transform.SetParent(root.transform, false);
            var hud = hudGo.AddComponent<BikeHud>();
            hud.Fonts = fonts; hud.Motor = course.Motor; hud.Soup = course.Soup;
            course.Hud = hud;
            // 자전거
            var bikePf = AssetDatabase.LoadAssetAtPath<GameObject>(BikePrefab);
            if (bikePf != null)
            {
                var bike = (GameObject)PrefabUtility.InstantiatePrefab(bikePf, scene);
                bike.transform.SetParent(root.transform, false);
                course.Bike = bike.GetComponent<BikeVisual>();
                bike.SetActive(false);
            }
            // 태오
            var taeoPf = AssetDatabase.LoadAssetAtPath<GameObject>(FormalSetup.TaeoPrefab);
            if (taeoPf != null)
            {
                var taeo = (GameObject)PrefabUtility.InstantiatePrefab(taeoPf, scene);
                taeo.name = "Taeo_Runner";
                taeo.transform.SetParent(root.transform, false);
                var pr = taeo.AddComponent<PathRunner>();
                pr.Path = TaeoXZ.Select(p => new Vector3(p.x, Y(p), p.y)).ToArray();
                pr.Offset = 0.9f;
                var bub = new GameObject("말풍선");
                bub.transform.SetParent(taeo.transform, false);
                bub.transform.localPosition = new Vector3(0f, 2.15f, 0f);
                var tmp = bub.AddComponent<TextMeshPro>();
                tmp.font = fonts != null ? fonts.B : null;
                tmp.fontSize = 3.2f; tmp.alignment = TextAlignmentOptions.Center; tmp.color = UiKit.Ink;
                tmp.rectTransform.sizeDelta = new Vector2(2f, 0.8f);
                tmp.text = "…";
                pr.Bubble = tmp;
                course.Taeo = pr;
                taeo.SetActive(false);
            }
            string house = TopHouse(root.transform, zoneRoot, d);
            Debug.Log($"{Tag} Zone1 'Bike': {house} · 코스 점 {course.Route.Length} · 정차 {course.Stops.Length}(경비실 {course.Stops[0].Pos.ToString("F1")} · 꼭대기 집 {course.Stops[1].Pos.ToString("F1")} 약속 05:36) · 끝 {course.End.ToString("F1")} · " +
                      $"오르막 입구 {course.ClimbStart.ToString("F1")} · CM_Bike · 자전거 {(course.Bike != null ? "있음" : "없음")} · 태오 {(course.Taeo != null ? "있음(경로 " + course.Taeo.Path.Length + "점)" : "없음")}");
        }

        static float Ground(Zone1Data d, Vector2 xz) => d.Grid.Sample(xz.x, xz.y);

        // ───────────────────────── 꼭대기 집 꾸미기(09 3-2 — 키트에 없는 새 블록이라 회색 상자 대신)
        const string MatDir = Root + "/Materials/Env";
        static Material M(string n) => AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/{n}.mat");

        /// 상자 메시(월드 미터 UV — 2m 에 텍스처 한 번)
        static GameObject Slab(string name, Transform parent, Vector3 center, Vector3 size, Material mat, bool shadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            var h = size * 0.5f;
            var v = new System.Collections.Generic.List<Vector3>();
            var n = new System.Collections.Generic.List<Vector3>();
            var uv = new System.Collections.Generic.List<Vector2>();
            var tri = new System.Collections.Generic.List<int>();
            void Face(Vector3 nrm, Vector3 u, Vector3 w)
            {
                int b = v.Count;
                var c = Vector3.Scale(nrm, h);
                var du = Vector3.Scale(u, h);
                var dw = Vector3.Scale(w, h);
                float[] su = { -1f, 1f, 1f, -1f }, sw = { -1f, -1f, 1f, 1f };
                for (int k = 0; k < 4; k++)
                {
                    var p = c + du * su[k] + dw * sw[k];
                    v.Add(p); n.Add(nrm);
                    uv.Add(new Vector2(Vector3.Dot(p + h, u) / 2f, Vector3.Dot(p + h, w) / 2f));
                }
                tri.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
            }
            Face(Vector3.right, Vector3.forward, Vector3.up); Face(Vector3.left, Vector3.back, Vector3.up);
            Face(Vector3.forward, Vector3.left, Vector3.up); Face(Vector3.back, Vector3.right, Vector3.up);
            Face(Vector3.up, Vector3.right, Vector3.forward); Face(Vector3.down, Vector3.right, Vector3.back);
            var mesh = new Mesh { name = name };
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(tri, 0);
            mesh.RecalculateTangents();
            mf.sharedMesh = mesh;
            return go;
        }

        /// 우진각 지붕(용마루가 x 방향): 처마 w×d, 높이 rise, 용마루 길이 ridge
        static GameObject Roof(string name, Transform parent, Vector3 eaveCenter, float w, float d, float rise, float ridge, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = eaveCenter;
            float hw = w / 2f, hd = d / 2f, hr = ridge / 2f;
            var a = new Vector3(-hw, 0f, -hd); var b = new Vector3(hw, 0f, -hd); var c = new Vector3(hw, 0f, hd); var e = new Vector3(-hw, 0f, hd);
            var r1 = new Vector3(-hr, rise, 0f); var r2 = new Vector3(hr, rise, 0f);
            var vs = new System.Collections.Generic.List<Vector3>();
            var tr = new System.Collections.Generic.List<int>();
            var uvs = new System.Collections.Generic.List<Vector2>();
            void T(Vector3 p0, Vector3 p1, Vector3 p2)
            {
                int i = vs.Count;
                vs.Add(p0); vs.Add(p1); vs.Add(p2);
                tr.Add(i); tr.Add(i + 1); tr.Add(i + 2);
                uvs.Add(new Vector2((p0.x + p0.z) / 1.5f, (p0.y + Mathf.Abs(p0.z)) / 1.5f));
                uvs.Add(new Vector2((p1.x + p1.z) / 1.5f, (p1.y + Mathf.Abs(p1.z)) / 1.5f));
                uvs.Add(new Vector2((p2.x + p2.z) / 1.5f, (p2.y + Mathf.Abs(p2.z)) / 1.5f));
            }
            T(a, r1, r2); T(a, r2, b);        // 남
            T(c, r2, r1); T(c, r1, e);        // 북
            T(b, r2, c);                      // 동
            T(e, r1, a);                      // 서
            T(a, b, c); T(a, c, e);           // 밑(처마 안쪽)
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vs); mesh.SetTriangles(tr, 0); mesh.SetUVs(0, uvs);
            mesh.RecalculateNormals(); mesh.RecalculateTangents();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            return go;
        }

        static string TopHouse(Transform root, Transform zoneRoot, Zone1Data d)
        {
            var blk = d.Blocks.FirstOrDefault(b => b.Name.StartsWith("꼭대기 집"));
            if (blk == null) return "꼭대기 집 블록 없음(zone1_gen)";
            var box = zoneRoot.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == blk.Name);
            int hidden = 0;
            if (box != null) foreach (var r in box.GetComponentsInChildren<MeshRenderer>(true)) { r.enabled = false; hidden++; }
            // 운동기구 마당 축대(새 retaining 블록) = 막돌
            var wall = zoneRoot.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "운동기구 마당 축대");
            var rubble = M("M_Stone_Rubble");
            if (wall != null && rubble != null) foreach (var r in wall.GetComponentsInChildren<MeshRenderer>(true)) r.sharedMaterial = rubble;
            float baseY = blk.Center.y - blk.Size.y / 2f;
            var c = new Vector3(blk.Center.x, baseY, blk.Center.z);
            float w = blk.Size.x, dd = blk.Size.z, wallH = 2.9f;
            var g = new GameObject("꼭대기 집(꾸밈)").transform;
            g.SetParent(root, false);
            Slab("벽", g, c + Vector3.up * (wallH / 2f + 0.4f), new Vector3(w - 0.3f, wallH, dd - 0.3f), M("M_Plaster_Cream"));
            Slab("기단", g, c + Vector3.up * 0.25f, new Vector3(w - 0.1f, 0.5f, dd - 0.1f), M("M_Granite_Dark"));
            Slab("처마 띠", g, c + Vector3.up * (wallH + 0.45f), new Vector3(w + 0.2f, 0.18f, dd + 0.2f), M("M_DoorFrame_Dark"));
            Roof("지붕", g, c + Vector3.up * (wallH + 0.54f), w + 1.1f, dd + 1.1f, 1.45f, 3.0f, M("M_Giwa_Dark"));
            // 대문(동쪽 면) — 철 대문 + 기둥 둘 + 대문 지붕 + 대문 등·불빛(할아버지 역광)
            float ex = c.x + w / 2f - 0.1f;
            Slab("대문 왼 기둥", g, new Vector3(ex + 0.1f, baseY + 1.2f, c.z - 1.15f), new Vector3(0.36f, 2.4f, 0.36f), M("M_Granite_Block"));
            Slab("대문 오른 기둥", g, new Vector3(ex + 0.1f, baseY + 1.2f, c.z + 1.15f), new Vector3(0.36f, 2.4f, 0.36f), M("M_Granite_Block"));
            Slab("대문", g, new Vector3(ex + 0.12f, baseY + 1.05f, c.z), new Vector3(0.08f, 2.0f, 1.9f), M("M_Metal_Green"));
            Roof("대문 지붕", g, new Vector3(ex + 0.25f, baseY + 2.45f, c.z), 1.1f, 2.9f, 0.45f, 2.0f, M("M_Giwa_Dark")).transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            Slab("대문 등", g, new Vector3(ex + 0.3f, baseY + 2.25f, c.z - 0.8f), new Vector3(0.18f, 0.22f, 0.18f), M("M_Glass_Lit"), false);
            var lg = new GameObject("대문 불빛");
            lg.transform.SetParent(g, false);
            lg.transform.position = new Vector3(ex + 0.6f, baseY + 2.2f, c.z - 0.8f);
            var li = lg.AddComponent<Light>();
            li.type = LightType.Point; li.color = new Color(1f, 0.74f, 0.45f); li.range = 6f; li.intensity = 2.2f; li.shadows = LightShadows.None;
            // 창(남쪽 2 · 동쪽 1 켜짐)
            Slab("창 남1", g, new Vector3(c.x - 1.6f, baseY + 1.9f, c.z - dd / 2f + 0.12f), new Vector3(1.3f, 1.0f, 0.06f), M("M_Glass_Dark"), false);
            Slab("창 남2", g, new Vector3(c.x + 1.2f, baseY + 1.9f, c.z - dd / 2f + 0.12f), new Vector3(1.3f, 1.0f, 0.06f), M("M_Glass_Dark"), false);
            Slab("창 동", g, new Vector3(ex + 0.01f, baseY + 1.9f, c.z + 2.3f), new Vector3(0.06f, 1.0f, 1.2f), M("M_Glass_Lit"), false);
            Slab("창살 동", g, new Vector3(ex + 0.05f, baseY + 1.9f, c.z + 2.3f), new Vector3(0.04f, 1.05f, 0.06f), M("M_Iron_Dark"), false);
            // 소품: 장독대·화분·가스 계량기·실외기
            int props = 0;
            void P(string pf, Vector3 pos, float yaw)
            {
                var a = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Prefabs/Env/P_{pf}.prefab");
                if (a == null) return;
                var o = (GameObject)PrefabUtility.InstantiatePrefab(a, root.gameObject.scene);
                o.transform.SetParent(g, true);
                o.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
                props++;
            }
            P("clay_pots_cluster", new Vector3(ex + 0.55f, baseY, c.z + 1.75f), 90f);
            P("jangdok_set", new Vector3(c.x - 1.5f, baseY, c.z - dd / 2f - 0.8f), 0f);
            P("gas_meter_pipe", new Vector3(ex + 0.05f, baseY, c.z - 2.2f), 90f);
            P("ac_outdoor_unit", new Vector3(c.x - w / 2f - 0.35f, baseY, c.z + 1.5f), -90f);
            // 할아버지(09 4-1 '실루엣' — 곽반장 몸 + 짙은 재질, 대문 불빛 역광). 대문 앞에서 길 쪽(동)을 봄
            var gw = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Prefabs/Cast/Gwak.prefab");
            string grandpa = "없음";
            if (gw != null)
            {
                var gp = (GameObject)PrefabUtility.InstantiatePrefab(gw, root.gameObject.scene);
                gp.name = "할아버지";
                gp.transform.SetParent(g, true);
                gp.transform.SetPositionAndRotation(new Vector3(ex + 0.75f, baseY, c.z + 0.2f), Quaternion.Euler(0f, 90f, 0f));
                var src = AssetDatabase.LoadAssetAtPath<Material>($"{Root}/Materials/M_Cast_Gwak_Toon.mat");
                string sp = $"{StorySetup.StageMatDir}/M_Silhouette.mat";
                var sil = AssetDatabase.LoadAssetAtPath<Material>(sp);
                if (sil == null && src != null && AssetDatabase.IsValidFolder(StorySetup.StageMatDir)) { sil = new Material(src); AssetDatabase.CreateAsset(sil, sp); }
                if (sil != null)
                {
                    var dk = new Color(0.16f, 0.14f, 0.15f);
                    sil.SetColor("_BaseColor", dk); sil.SetColor("_Color", dk); sil.SetColor("_1st_ShadeColor", dk * 0.8f); sil.SetColor("_2nd_ShadeColor", dk * 0.7f);
                    EditorUtility.SetDirty(sil);
                    CharSetup.Paint(gp, sil, null);
                }
                grandpa = "실루엣(곽반장 몸)";
            }
            return $"꼭대기 집 꾸밈(회색 상자 렌더러 {hidden}개 끔 · 벽·기단·우진각 지붕·철 대문·대문 등·창 3·소품 {props} · 할아버지 {grandpa}) · 운동기구 마당 축대 {(wall != null ? "막돌" : "없음")}";
        }
    }
}
