// 행인1의 메인이벤트 — 체육관 장면 Gym.unity 만들기(docs/12_체육관_설계.md 1·2·3·5장): 키트 FBX(풀어서 원점) + LOD + 충돌 상자(평면도) + 소켓 115 → 소품·조명·NPC·드릴 마커·카메라
// 무대 규칙(StagePlace, SceneLoader Additive)대로 Zone1 과 겹치지 않는 자리(2000, 0, 2200)에 둔다. 좌표 = 키트 로컬(원점 = 지하 바닥 남서 모서리, x 동 · z 북).
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
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Haengin.EditorGame
{
    public static partial class GymSetup
    {
        public static readonly Vector3 Origin = new Vector3(2000f, 0f, 2200f);
        public const float RoomW = 20f, RoomD = 13f, RoomH = 3.2f;

        static void Scene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var man = Manifest();
            var root = new GameObject("Stage_Gym");
            root.transform.position = Origin;
            var place = root.AddComponent<StagePlace>();
            place.Key = SceneLoader.Gym; place.Title = "혜화 MMA"; place.Indoor = true; place.KillY = -10f; place.Placeholder = false;
            place.CamRadius = 3.0f; place.CamFov = 50f; place.CamTargetHeight = 1.5f; place.CamPitch = 6f; place.CamPitchMax = 22f;
            var gym = root.AddComponent<GymManager>();
            gym.Actions = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(M1Setup.InputPath);
            gym.Fonts = AssetDatabase.LoadAssetAtPath<UiFonts>(StorySetup.FontsPath) ?? StorySetup.Fonts();
            gym.Book = AssetDatabase.LoadAssetAtPath<DrillBook>(BookPath);
            gym.Clips = AssetDatabase.LoadAssetAtPath<GymClips>(ClipsPath);
            gym.SparDef = AssetDatabase.LoadAssetAtPath<EnemyDef>(SparDefPath) ?? SparDefAsset();
            gym.GojunFighterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GojunFighterPrefab);
            root.AddComponent<DrillRunner>().Actions = gym.Actions;

            // ① 키트
            var kitPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(KitFbx) ?? throw new Exception("키트 FBX 없음");
            var kit = (GameObject)PrefabUtility.InstantiatePrefab(kitPrefab);
            PrefabUtility.UnpackPrefabInstance(kit, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            kit.name = "Kit"; kit.transform.SetParent(root.transform, false); kit.transform.localPosition = Vector3.zero; kit.transform.localRotation = Quaternion.identity; kit.transform.localScale = Vector3.one;
            var staticFlags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic;
            int lodGroups = 0, renderers = 0, posters = 0;
            var tubes = new Dictionary<string, Renderer>();
            foreach (var t in kit.transform.Cast<Transform>().ToList())
            {
                var rs = t.GetComponentsInChildren<Renderer>(true);
                foreach (var r in rs)
                {
                    GameObjectUtility.SetStaticEditorFlags(r.gameObject, staticFlags);
                    r.gameObject.layer = 0; r.receiveShadows = true; renderers++;
                    if (r.name.EndsWith("_LOD1")) r.shadowCastingMode = ShadowCastingMode.Off;
                }
                if (t.name.StartsWith("Fluor_")) { var r = t.GetComponentInChildren<Renderer>(true); if (r != null) { tubes[t.name] = r; r.shadowCastingMode = ShadowCastingMode.Off; GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.OccludeeStatic); } continue; }
                if (t.name.StartsWith("Poster_"))
                {
                    var r = t.GetComponentInChildren<Renderer>(true);
                    string cell = t.name.Substring(7, t.name.LastIndexOf('_') - 7);
                    var pm = Mat("M_Poster_" + cell);
                    if (r != null && pm != null) { r.sharedMaterial = pm; r.shadowCastingMode = ShadowCastingMode.Off; posters++; }
                    continue;
                }
                var lodRs = rs.Where(r => r.name.Contains("_LOD")).ToArray();
                if (lodRs.Length > 0)
                {
                    var lg = t.GetComponent<LODGroup>(); if (lg == null) lg = t.gameObject.AddComponent<LODGroup>();
                    var hs = L(G(D(man["lod"]), "screen_heights"));
                    float h0 = hs != null && hs.Count > 0 ? F(hs[0], 0.12f) : 0.12f;
                    var lods = new List<LOD>();
                    var set0 = lodRs.Where(r => r.name.EndsWith("_LOD0")).ToArray(); var set1 = lodRs.Where(r => r.name.EndsWith("_LOD1")).ToArray();
                    if (set0.Length > 0) lods.Add(new LOD(h0, set0));
                    if (set1.Length > 0) lods.Add(new LOD(0.0001f, set1));
                    if (lods.Count > 0) { lg.SetLODs(lods.ToArray()); lg.fadeMode = LODFadeMode.None; lg.RecalculateBounds(); lodGroups++; }
                }
                // 천장·보·덕트는 카메라와 충돌하지 않음(충돌체가 없으므로 그대로) — 유리·철망도 같음
            }

            // ② 충돌(평면도 1장)
            var col = new GameObject("Colliders").transform; col.SetParent(root.transform, false);
            int boxes = Colliders(col);

            // ③ 소켓(manifest) → 빈 오브젝트
            var socks = new GameObject("Sockets").transform; socks.SetParent(root.transform, false);
            var sockList = L(man["sockets"]);
            var sockBy = new Dictionary<string, Transform>();
            foreach (var so in sockList)
            {
                var sd = D(so);
                string nm = S(sd, "name"); if (nm == null) continue;
                var go = new GameObject(nm); go.transform.SetParent(socks, false);
                go.transform.localPosition = V3(G(sd, "pos"));
                var fwd = V3(G(sd, "fwd")); var flat = new Vector3(fwd.x, 0f, fwd.z);
                go.transform.localRotation = flat.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(flat.normalized, Vector3.up) : Quaternion.identity;
                if (!sockBy.ContainsKey(nm)) sockBy[nm] = go.transform;
            }
            gym.Sockets = sockBy.Values.ToList();

            // ④ 소품(Prop_ 소켓 → 프리팹)
            var props = new GameObject("Props").transform; props.SetParent(root.transform, false);
            int placed = 0, missing = 0;
            var bags = new List<BagSwing>();
            Transform dummy = null, speedBag = null;
            var tuning = CombatSetup.Tuning;
            foreach (var so in sockList)
            {
                var sd = D(so); if (S(sd, "kind") != "prop") continue;
                var inf = D(G(sd, "info")); string prop = S(inf, "prop"); string nm = S(sd, "name");
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/P_{prop}.prefab");
                if (pf == null) { missing++; continue; }
                var holder = new GameObject(nm); holder.transform.SetParent(props, false);
                holder.transform.localPosition = V3(G(sd, "pos"));
                var fwd = V3(G(sd, "fwd")); var flat = new Vector3(fwd.x, 0f, fwd.z);
                holder.transform.localRotation = flat.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(flat.normalized, Vector3.up) : Quaternion.identity;
                var pi = (GameObject)PrefabUtility.InstantiatePrefab(pf, holder.transform);
                pi.transform.localPosition = Vector3.zero; pi.transform.localRotation = Quaternion.identity; pi.transform.localScale = Vector3.one;
                placed++;
                if (prop == "heavy_bag_long" || prop == "heavy_bag_short")
                {
                    // 체인 피벗(보 밑 레일 h 2.8) 아래에 백을 달아 흔들림 + 맞는 몸(Fighter)
                    var pivot = new GameObject(nm + "_Pivot"); pivot.transform.SetParent(props, false);
                    var lp = holder.transform.localPosition; pivot.transform.localPosition = new Vector3(lp.x, 2.8f, lp.z); pivot.transform.localRotation = Quaternion.identity;
                    holder.transform.SetParent(pivot.transform, true);
                    var swing = pivot.AddComponent<BagSwing>(); swing.Pivot = pivot.transform; swing.Length = 2.8f - lp.y + 0.6f;
                    // 맞는 몸은 백(holder) 아래에 — 백과 함께 흔들린다. 젖힘 대역(react)은 몸 아래 빈 오브젝트(장면 루트에 같은 이름 6개가 남지 않게)
                    var body = new GameObject(nm + "_Body"); body.transform.SetParent(holder.transform, false); body.transform.localPosition = new Vector3(0f, -lp.y, 0f);
                    var reactDummy = new GameObject(nm + "_React"); reactDummy.transform.SetParent(body.transform, false);
                    var f = CombatFactory.AddFighter(body, "샌드백", 1, 99999, prop == "heavy_bag_long" ? 1.6f : 1.4f, 0.22f, tuning, reactDummy.transform);
                    f.KeepStanding = true; f.StaggerMul = 0f; f.DebugInvuln = false;
                    var cc = body.GetComponent<CharacterController>(); if (cc != null) cc.enabled = false;        // 매달린 몸: 떨어지지 않게 몸 충돌은 끔(판정은 Fighter 좌표)
                    var fb = body.GetComponent<FighterBody>(); if (fb != null) fb.enabled = false;
                    swing.Target = f;
                    bags.Add(swing);
                }
                if (prop == "grappling_dummy") dummy = holder.transform;
                if (prop == "speed_bag_platform") speedBag = holder.transform;
            }
            gym.Bags = bags; gym.Dummy = dummy; gym.SpeedBag = speedBag;

            // ⑤ 조명(5-2): 형광등 14 + 계단 1(Spot 아래로, 발광 관) · 케이지 스포트 4 · 사무실 1
            var lightsGo = new GameObject("Lights"); lightsGo.transform.SetParent(root.transform, false);
            var lights = lightsGo.AddComponent<GymLights>();
            lights.Lut = LookdevSetup.Lut("Gym");     // 룩데브(docs/14): 체육관 LUT(그늘 회갈은 GymLights 볼륨)
            lights.FluorMat = Mat("M_Fluorescent");
            int fl = 0;
            foreach (var so in sockList)
            {
                var sd = D(so); if (S(sd, "kind") != "light") continue;
                var inf = D(G(sd, "info")); string kind = S(inf, "light"); string nm = S(sd, "name");
                var go = new GameObject("빛 " + nm); go.transform.SetParent(lightsGo.transform, false);
                go.transform.localPosition = V3(G(sd, "pos"));
                var fwd = V3(G(sd, "fwd")); go.transform.localRotation = fwd.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(fwd.normalized, Mathf.Abs(fwd.y) > 0.99f ? Vector3.forward : Vector3.up) : Quaternion.identity;
                var l = go.AddComponent<Light>(); l.shadows = LightShadows.None; l.renderMode = LightRenderMode.ForcePixel;
                switch (kind)
                {
                    case "fluor":
                        l.type = LightType.Spot; l.spotAngle = 130f; l.innerSpotAngle = 90f; l.range = 7f; l.intensity = 1.6f; l.color = GymLights.FluorColor;
                        if (nm == "Sock_Light_Stair") lights.Stair = l;
                        else
                        {
                            float row = F(G(inf, "row")); int rowIdx = row < 4f ? 0 : row < 8f ? 1 : 2;
                            fl++;
                            tubes.TryGetValue("Fluor_" + nm.Substring(nm.Length - 2), out var tube);
                            lights.Fluors.Add(new GymLights.Fluor { Light = l, Tube = tube, Row = rowIdx });
                        }
                        break;
                    case "spot":
                        l.type = LightType.Spot; l.spotAngle = F(G(inf, "angle"), 40f); l.innerSpotAngle = 25f; l.range = 8f; l.intensity = 2.4f; l.color = Hex(S(inf, "color"), new Color(0.95f, 0.91f, 0.85f));
                        lights.CageSpots.Add(l); break;
                    default:
                        l.type = LightType.Point; l.range = 4.5f; l.intensity = 1.4f; l.color = new Color(1f, 0.97f, 0.92f);
                        lights.Office = l; break;
                }
            }
            gym.Lights = lights;
            // 거울 평면 반사(남벽 x 1.6~10.2 · h 0.4~2.4, 드릴 중에만 켬 — 꺼지면 M_Mirror 판)
            var mirrorGo = new GameObject("거울 반사판"); mirrorGo.transform.SetParent(root.transform, false);
            mirrorGo.transform.localPosition = new Vector3(5.9f, 1.4f, 0.43f); mirrorGo.transform.localRotation = Quaternion.identity;
            var mf = mirrorGo.AddComponent<MeshFilter>(); var mr = mirrorGo.AddComponent<MeshRenderer>();
            mf.sharedMesh = MirrorQuad(4.3f, 1.0f); mr.sharedMaterial = Mat("M_Mirror"); mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
            var mirror = mirrorGo.AddComponent<MirrorPlane>(); mirror.Panel = mr; mirror.Fallback = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/M_Mirror.png"); mirror.Normal = Vector3.forward; mirror.HalfWidth = 4.3f; mirror.HalfHeight = 1.0f;
            gym.Mirror = mirror;

            // ⑥ 드릴 마커(Sock_Drill_*) + 라운드 타이머 + 소리 자리
            var fonts = gym.Fonts;
            var markers = new GameObject("Markers").transform; markers.SetParent(root.transform, false);
            var ringTex = AssetDatabase.LoadAssetAtPath<Texture2D>(RingTex);
            var ringMatPath = $"{MatDir}/M_DrillRing.mat";
            var ringMat = AssetDatabase.LoadAssetAtPath<Material>(ringMatPath);
            if (ringMat == null) { ringMat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "M_DrillRing" }; AssetDatabase.CreateAsset(ringMat, ringMatPath); }
            ringMat.SetTexture("_BaseMap", ringTex); ringMat.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.7f));
            ringMat.SetFloat("_Surface", 1f); ringMat.SetFloat("_Blend", 0f); ringMat.SetOverrideTag("RenderType", "Transparent"); ringMat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); ringMat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha); ringMat.SetInt("_ZWrite", 0); ringMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); ringMat.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(ringMat);
            var markerList = new List<DrillMarker>();
            var seen = new HashSet<string>();
            foreach (var so in sockList)
            {
                var sd = D(so); if (S(sd, "kind") != "drill") continue;
                var inf = D(G(sd, "info")); string id = S(inf, "drill"); string nm = S(sd, "name");
                var pos = V3(G(sd, "pos"));
                if (id == "D02") pos.x += 1.2f;            // D01 과 같은 자리 → 옆으로
                var mk = new GameObject("Marker_" + nm.Substring(11)); mk.transform.SetParent(markers, false); mk.transform.localPosition = new Vector3(pos.x, 0f, pos.z);
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad); quad.name = "먹 원"; Object.DestroyImmediate(quad.GetComponent<Collider>());
                quad.transform.SetParent(mk.transform, false); quad.transform.localPosition = new Vector3(0f, pos.y + 0.012f, 0f); quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); quad.transform.localScale = Vector3.one * 1.1f;
                var qr = quad.GetComponent<Renderer>(); qr.sharedMaterial = ringMat; qr.shadowCastingMode = ShadowCastingMode.Off; qr.receiveShadows = false;
                var labelGo = new GameObject("이름표"); labelGo.transform.SetParent(mk.transform, false); labelGo.transform.localPosition = new Vector3(0f, DrillMarker.LabelHeight, 0f);     // QA 2차 A7: 머리 위·작게
                var tmp = labelGo.AddComponent<TextMeshPro>(); tmp.font = fonts != null ? fonts.B : null; tmp.fontSize = DrillMarker.LabelSize; tmp.alignment = TextAlignmentOptions.Center; tmp.color = UiKit.Ink; tmp.rectTransform.sizeDelta = new Vector2(3f, 1f);
                var ia = mk.AddComponent<Interactable>(); ia.Radius = 1.7f;
                // 바닥 스티커(먹 원 옆 — QA 2차 A7: 가까이 갈 때만, 작게)
                var stGo = new GameObject("스티커"); stGo.transform.SetParent(mk.transform, false); stGo.transform.localPosition = new Vector3(0f, pos.y + 0.014f, -0.78f); stGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var st = stGo.AddComponent<TextMeshPro>(); st.font = fonts != null ? fonts.B : null; st.fontSize = DrillMarker.StickerSize; st.alignment = TextAlignmentOptions.Center; st.color = new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.85f); st.rectTransform.sizeDelta = new Vector2(2.2f, 0.3f);
                var dm = mk.AddComponent<DrillMarker>(); dm.DrillId = id; dm.Interact = ia; dm.Label = tmp; dm.Sticker = st; dm.Ring = qr; dm.Gym = gym;
                markerList.Add(dm);
                seen.Add(id);
            }
            gym.Markers = markerList;
            // 벽 라운드 타이머(남벽 x 10.5 h 2.5, 발광 숫자판 자리 → TMP)
            var timerGo = new GameObject("라운드 타이머"); timerGo.transform.SetParent(root.transform, false); timerGo.transform.localPosition = new Vector3(10.5f, 2.5f, 0.34f); timerGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var timer = timerGo.AddComponent<TextMeshPro>(); timer.font = fonts != null ? fonts.Br : null; timer.fontSize = 4f; timer.alignment = TextAlignmentOptions.Center; timer.color = new Color(1f, 0.25f, 0.2f); timer.text = "3:00"; timer.rectTransform.sizeDelta = new Vector2(1.6f, 0.6f);
            gym.WallTimer = timer;
            // 체육관 이름 현수막 글자(글자는 TMP — 포스터 아틀라스에는 글자 없음)
            var banner = new GameObject("현수막 글자"); banner.transform.SetParent(root.transform, false); banner.transform.localPosition = new Vector3(11.3f, 2.35f, 12.95f); banner.transform.localRotation = Quaternion.identity;   // 북벽(의자 위), 남쪽에서 읽힘
            var bt = banner.AddComponent<TextMeshPro>(); bt.font = fonts != null ? fonts.Br : null; bt.fontSize = 5f; bt.alignment = TextAlignmentOptions.Center; bt.color = UiKit.Paper; bt.text = "혜화 MMA"; bt.rectTransform.sizeDelta = new Vector2(4f, 0.8f);
            // 오디오 소켓(자리): 스피커 2 + 대학로 소음
            foreach (var so in sockList)
            {
                var sd = D(so); if (S(sd, "kind") != "audio") continue;
                var go = new GameObject("소리 " + S(sd, "name")); go.transform.SetParent(root.transform, false); go.transform.localPosition = V3(G(sd, "pos"));
                var a = go.AddComponent<AudioSource>(); a.playOnAwake = false; a.spatialBlend = 1f; a.maxDistance = 14f; a.volume = 0f;
            }

            // ⑥-2 공간 채우기(2차 패스): 천장 배관·케이블 트레이·바닥 테이프·사무실 집기·포스터 2
            int decor = Decor(root.transform, fonts);

            // ⑦ NPC(프리팹 인스턴스, 처음엔 꺼 둠 — GymManager 가 시간대별로 켬)
            var npcRoot = new GameObject("NPC").transform; npcRoot.SetParent(root.transform, false);
            GymNpc Npc(string prefab, string name)
            {
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>(prefab); if (pf == null) { Notes.Add("NPC 프리팹 없음: " + prefab); return null; }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, npcRoot); go.name = name; go.SetActive(false);
                return go.GetComponent<GymNpc>();
            }
            gym.Yunjun = Npc(NpcPrefab("Yunjun_v2"), "이윤준"); gym.Gojun = Npc(NpcPrefab("Gojun"), "고준");
            (gym.MittMeshL, gym.MittMeshR, gym.MittMat) = StandSetup.MittMeshes();
            gym.Guests = new List<GymNpc>();
            foreach (var gn in GuestNpcs) { var g = Npc(NpcPrefab(gn), NpcNames[gn].name); if (g != null) gym.Guests.Add(g); }
            gym.Members = new List<GymNpc>();
            for (int i = 0; i < 8; i++) { var m = Npc(MemberPrefab(i), $"관원 {i + 1}"); if (m != null) gym.Members.Add(m); }

            // ⑧ 드릴 카메라(고정 프리셋, 우선순위 30 일 때만) + 스폰
            var camGo = new GameObject("CM_Drill"); camGo.transform.SetParent(root.transform, false);
            var vcam = camGo.AddComponent<CinemachineCamera>(); vcam.Priority = 0; vcam.Lens.FieldOfView = 50f; vcam.Lens.NearClipPlane = 0.1f; vcam.Lens.FarClipPlane = 200f;
            gym.DrillCam = vcam;
            var spawns = new List<Transform>();
            spawns.Add(SpawnAt(root.transform, "Spawn", new Vector3(0.9f, 3.42f, 13.4f), 180f));     // 계단 꼭대기(1층 문 안쪽)
            spawns.Add(SpawnAt(root.transform, "Floor", new Vector3(0.9f, 0f, 7.0f), 180f));
            spawns.Add(SpawnAt(root.transform, "Mat", new Vector3(6.0f, 0.03f, 3.5f), 180f));
            spawns.Add(SpawnAt(root.transform, "Cage", new Vector3(13.6f, 0.25f, 6.0f), 90f));
            place.Spawns = spawns.ToArray();
            // Zone1 연결 자리(대학로 쪽 1층 문) — 전환은 2부
            var door = new GameObject("Door_Zone1(대학로, 자리만)"); door.transform.SetParent(root.transform, false); door.transform.localPosition = new Vector3(0.9f, 3.42f, 14.9f);

            // 장면 설정(안개는 바탕 장면 Zone1 의 DayLight 가 가짐 — 여기서는 건드리지 않음)
            foreach (var go in scene.GetRootGameObjects()) EnvSetup.LodDedup(go.transform, out _);     // QA Q25: 키트 뿌리 LOD 묶음 ↔ 부품별 LOD 묶음 겹침
            EditorSceneManager.SaveScene(scene, SceneLoader.GymScene);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            StableIds.RewriteScene(SceneLoader.GymScene, "", new Dictionary<long, long>());
            AssetDatabase.ImportAsset(SceneLoader.GymScene, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            string msg = $"{Tag} 장면 {SceneLoader.GymScene}: 키트 렌더러 {renderers} · LOD 묶음 {lodGroups} · 포스터 {posters} · 장식 {decor} · 충돌 상자 {boxes} · 소켓 {sockBy.Count} · 소품 {placed}(없음 {missing}) · 샌드백 {bags.Count} · 형광등 {fl} + 스포트 {lights.CageSpots.Count} · 마커 {markerList.Count}(드릴 {seen.Count}종) · NPC {(gym.Yunjun != null ? 1 : 0) + (gym.Gojun != null ? 1 : 0) + gym.Members.Count}";
            Debug.Log(msg); Notes.Add(msg);
        }

        /// 2차 패스 장식: 배관 3 · 케이블 트레이 1 · 전선관 2 · 바닥 테이프(케이지 둘레 8 · 매트 경계 4) · 사무실 집기(서류 상자 2 · 노트북 · 컵 · 선반) · 포스터 2. 충돌 없음
        static int Decor(Transform root, UiFonts fonts)
        {
            int n = 0;
            var d = new GameObject("Decor").transform; d.SetParent(root, false);
            var steel = Mat("M_Steel_Dark"); var grey = Mat("M_Metal_Grey"); var wood = Mat("M_Wood_Dark") ?? grey;
            string tapePath = $"{MatDir}/M_Tape.mat";
            var tape = AssetDatabase.LoadAssetAtPath<Material>(tapePath);
            if (tape == null) { tape = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "M_Tape" }; AssetDatabase.CreateAsset(tape, tapePath); }
            tape.SetColor("_BaseColor", new Color(0.86f, 0.80f, 0.52f)); EditorUtility.SetDirty(tape);
            GameObject Prim(PrimitiveType t, string name, Vector3 pos, Vector3 scale, Quaternion rot, Material m)
            {
                var go = GameObject.CreatePrimitive(t); go.name = name; Object.DestroyImmediate(go.GetComponent<Collider>());
                go.transform.SetParent(d, false); go.transform.localPosition = pos; go.transform.localRotation = rot; go.transform.localScale = scale;
                var r = go.GetComponent<Renderer>(); if (m != null) r.sharedMaterial = m; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = true;
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
                n++; return go;
            }
            // 천장 배관(x 방향 3) · 케이블 트레이(z 방향, 기둥 옆) · 전선관 드롭 2
            foreach (var z in new[] { 1.1f, 4.6f, 11.9f }) Prim(PrimitiveType.Cylinder, $"배관 z{z}", new Vector3(RoomW / 2f, 3.05f, z), new Vector3(0.12f, RoomW / 2f - 0.2f, 0.12f), Quaternion.Euler(0f, 0f, 90f), steel);
            Prim(PrimitiveType.Cube, "케이블 트레이", new Vector3(10.4f, 2.95f, RoomD / 2f), new Vector3(0.3f, 0.08f, RoomD - 0.4f), Quaternion.identity, grey);
            Prim(PrimitiveType.Cylinder, "전선관 1", new Vector3(14.05f, 1.6f, 12.95f), new Vector3(0.05f, 1.6f, 0.05f), Quaternion.identity, grey);
            Prim(PrimitiveType.Cylinder, "전선관 2", new Vector3(19.95f, 1.6f, 0.5f), new Vector3(0.05f, 1.6f, 0.05f), Quaternion.identity, grey);
            // 바닥 테이프: 케이지 둘레 팔각(apothem + 0.35) · 매트 경계
            var cage = D(G(D(Manifest()["room"]), "cage")); var cc = L(G(cage, "center")); float cx = cc != null ? F(cc[0]) : 14.5f, cz = cc != null ? F(cc[1]) : 6.0f; float R = F(G(cage, "circumradius"), 3.1f);
            float ap = R * Mathf.Cos(Mathf.PI / 8f) + 0.35f, side = 2f * ap * Mathf.Tan(Mathf.PI / 8f);
            for (int k = 0; k < 8; k++) { float a = 45f * k; var dir = HitResolver.YawDir(a); Prim(PrimitiveType.Cube, $"테이프 케이지 {k}", new Vector3(cx, 0.006f, cz) + dir * ap, new Vector3(side + 0.06f, 0.004f, 0.06f), Quaternion.Euler(0f, a, 0f), tape); }
            float mx0 = 1.5f, mx1 = 10.3f, mz0 = 0.5f, mz1 = 6.1f;
            Prim(PrimitiveType.Cube, "테이프 매트 남", new Vector3((mx0 + mx1) / 2f, 0.036f, mz0), new Vector3(mx1 - mx0, 0.004f, 0.05f), Quaternion.identity, tape);
            Prim(PrimitiveType.Cube, "테이프 매트 북", new Vector3((mx0 + mx1) / 2f, 0.036f, mz1), new Vector3(mx1 - mx0, 0.004f, 0.05f), Quaternion.identity, tape);
            Prim(PrimitiveType.Cube, "테이프 매트 서", new Vector3(mx0, 0.036f, (mz0 + mz1) / 2f), new Vector3(0.05f, 0.004f, mz1 - mz0), Quaternion.identity, tape);
            Prim(PrimitiveType.Cube, "테이프 매트 동", new Vector3(mx1, 0.036f, (mz0 + mz1) / 2f), new Vector3(0.05f, 0.004f, mz1 - mz0), Quaternion.identity, tape);
            // 사무실 집기(책상 위 y 0.75): 서류 상자 2 · 노트북 · 컵 · 벽 선반
            Prim(PrimitiveType.Cube, "서류 상자 1", new Vector3(16.45f, 0.81f, 12.2f), new Vector3(0.3f, 0.12f, 0.24f), Quaternion.Euler(0f, 8f, 0f), grey);
            Prim(PrimitiveType.Cube, "서류 상자 2", new Vector3(16.45f, 0.93f, 12.22f), new Vector3(0.28f, 0.12f, 0.22f), Quaternion.Euler(0f, -5f, 0f), wood);
            Prim(PrimitiveType.Cube, "노트북", new Vector3(17.1f, 0.765f, 12.15f), new Vector3(0.33f, 0.02f, 0.23f), Quaternion.Euler(0f, 12f, 0f), steel);
            Prim(PrimitiveType.Cube, "노트북 화면", new Vector3(17.1f, 0.87f, 12.26f), new Vector3(0.33f, 0.21f, 0.015f), Quaternion.Euler(-12f, 12f, 0f), steel);
            Prim(PrimitiveType.Cylinder, "컵", new Vector3(17.55f, 0.795f, 12.0f), new Vector3(0.08f, 0.045f, 0.08f), Quaternion.identity, Mat("M_Plaster_Cream") ?? grey);
            Prim(PrimitiveType.Cube, "선반", new Vector3(16.0f, 1.25f, 12.93f), new Vector3(1.2f, 0.04f, 0.24f), Quaternion.identity, wood);
            for (int i = 0; i < 4; i++) Prim(PrimitiveType.Cube, $"파일 {i + 1}", new Vector3(15.55f + 0.26f * i, 1.4f, 12.93f), new Vector3(0.2f, 0.26f, 0.2f), Quaternion.identity, i % 2 == 0 ? grey : wood);
            // 포스터 2(아틀라스 칸 재질): 북벽 규칙판 · 동벽 세로 현수막
            Quad(d, "포스터 규칙판", new Vector3(9.4f, 1.75f, RoomD - 0.03f), Quaternion.Euler(0f, 180f, 0f), new Vector3(0.7f, 1.0f, 1f), Mat("M_Poster_rules_board")); n++;
            Quad(d, "포스터 현수막", new Vector3(RoomW - 0.03f, 1.7f, 12.0f), Quaternion.Euler(0f, 90f, 0f), new Vector3(0.6f, 1.6f, 1f), Mat("M_Poster_gym_banner")); n++;
            return n;
        }

        static void Quad(Transform parent, string name, Vector3 pos, Quaternion rot, Vector3 scale, Material m)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = pos; go.transform.localRotation = rot; go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = MirrorQuad(0.5f, 0.5f);
            var r = go.AddComponent<MeshRenderer>(); if (m != null) r.sharedMaterial = m; r.shadowCastingMode = ShadowCastingMode.Off;
        }

        /// +Z 를 보는 사각형(UV u = −x → +x, v = 아래 → 위)
        static Mesh MirrorQuad(float hw, float hh)
        {
            string p = GymDir + (hw < 1f ? "/UnitQuad.asset" : "/MirrorQuad.asset");
            var m = AssetDatabase.LoadAssetAtPath<Mesh>(p);
            if (m == null) { m = new Mesh { name = "MirrorQuad" }; AssetDatabase.CreateAsset(m, p); }
            m.Clear();
            m.vertices = new[] { new Vector3(-hw, -hh, 0f), new Vector3(hw, -hh, 0f), new Vector3(hw, hh, 0f), new Vector3(-hw, hh, 0f) };
            m.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.RecalculateNormals();
            if (m.normals[0].z < 0f) { m.triangles = new[] { 0, 1, 2, 0, 2, 3 }; m.RecalculateNormals(); }
            m.RecalculateBounds();
            EditorUtility.SetDirty(m);
            return m;
        }

        static Transform SpawnAt(Transform root, string name, Vector3 local, float yaw)
        {
            var go = new GameObject(name); go.transform.SetParent(root, false); go.transform.localPosition = local; go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return go.transform;
        }

        static int Colliders(Transform parent)
        {
            int n = 0;
            void Box(string name, Vector3 center, Vector3 size, int layer, Quaternion? rot = null)
            {
                var go = new GameObject(name); go.layer = layer; go.transform.SetParent(parent, false);
                go.transform.localPosition = center; go.transform.localRotation = rot ?? Quaternion.identity;
                var bc = go.AddComponent<BoxCollider>(); bc.size = size; n++;
            }
            float h = RoomH;
            Box("바닥", new Vector3(RoomW / 2f, -0.25f, RoomD / 2f), new Vector3(RoomW + 2f, 0.5f, RoomD + 4f), Layers.Ground);
            Box("벽 남(거울)", new Vector3(RoomW / 2f, h / 2f, -0.1f), new Vector3(RoomW + 0.4f, h, 0.2f), Layers.Wall);
            Box("벽 북", new Vector3(RoomW / 2f + 0.9f, h / 2f, RoomD + 0.1f), new Vector3(RoomW - 1.8f + 0.4f, h, 0.2f), Layers.Wall);   // 계단실(x 0~1.8)은 터 둠
            Box("벽 동", new Vector3(RoomW + 0.1f, h / 2f, RoomD / 2f), new Vector3(0.2f, h, RoomD + 4f), Layers.Wall);
            Box("벽 서", new Vector3(-0.1f, h / 2f, RoomD / 2f + 1f), new Vector3(0.2f, h + 3.5f, RoomD + 2f), Layers.Wall);
            Box("기둥", new Vector3(10f, h / 2f, 6.5f), new Vector3(0.5f, h, 0.5f), Layers.Wall);
            // 계단실: 18단(0.19 × 0.28) 경사 + 위 층계참(y 3.42) + 동쪽 벽 + 끝 벽
            float rise = 3.42f, run = 5.04f, zTop = 12.44f, zBot = zTop - run;
            float ang = Mathf.Atan2(rise, run) * Mathf.Rad2Deg, len = Mathf.Sqrt(rise * rise + run * run);
            var rot = Quaternion.Euler(-ang, 0f, 0f); var nrm = rot * Vector3.up;
            Box("계단 경사", new Vector3(0.9f, rise / 2f, (zTop + zBot) / 2f) - nrm * 0.15f, new Vector3(1.7f, 0.3f, len), Layers.Ground, rot);
            Box("층계참", new Vector3(0.9f, rise - 0.15f, 13.9f), new Vector3(1.7f, 0.3f, 3.0f), Layers.Ground);
            Box("계단실 동벽", new Vector3(1.85f, h / 2f + 1f, 11.3f), new Vector3(0.1f, h + 3.5f, 7.6f), Layers.Wall);
            Box("계단실 끝벽(1층 문)", new Vector3(0.9f, rise + 1.2f, 15.45f), new Vector3(1.9f, 2.6f, 0.1f), Layers.Wall);
            Box("계단실 서벽 위", new Vector3(-0.1f, rise + 1.2f, 14.2f), new Vector3(0.2f, 2.6f, 2.6f), Layers.Wall);
            // 케이지: 단(팔각 → 상자) + 철망 7면(서쪽 문 쪽 비움, 얇은 PlayerOnly = 카메라 통과)
            var cage = D(G(D(Manifest()["room"]), "cage"));
            var cc = L(G(cage, "center")); float cx = cc != null ? F(cc[0]) : 14.5f, cz = cc != null ? F(cc[1]) : 6.0f; float R = F(G(cage, "circumradius"), 3.1f), plat = F(G(cage, "platform"), 0.25f);
            float side = 2f * R * Mathf.Sin(Mathf.PI / 8f), apothem = R * Mathf.Cos(Mathf.PI / 8f);
            Box("케이지 단", new Vector3(cx, plat / 2f, cz), new Vector3(apothem * 2f, plat, apothem * 2f), Layers.Ground);
            for (int k = 0; k < 8; k++)
            {
                float a = 45f * k;                      // 면의 중심 방위(0 = 북 … 270 = 서)
                if (Mathf.Abs(Mathf.DeltaAngle(a, 270f)) < 1f) continue;        // 서쪽 = 문
                var dir = HitResolver.YawDir(a);
                Box($"철망 {k}", new Vector3(cx, plat + 0.9f, cz) + dir * apothem, new Vector3(side, 1.8f, 0.06f), Layers.PlayerOnly, Quaternion.Euler(0f, a, 0f));
            }
            Box("케이지 단 턱(서)", new Vector3(cx - apothem - 0.3f, plat / 2f, cz), new Vector3(0.6f, plat, 1.0f), Layers.Ground);    // 문 앞 디딤
            // 사무실: 허리벽+유리(z 10.2, 문 14.4~15.3 비움) · 서벽 x 14.2 · 책상
            Box("사무실 앞벽", new Vector3(17.4f, 1.25f, 10.2f), new Vector3(4.2f, 2.5f, 0.16f), Layers.Wall);
            Box("사무실 앞벽(문 왼쪽)", new Vector3(14.3f, 1.25f, 10.2f), new Vector3(0.2f, 2.5f, 0.16f), Layers.Wall);
            Box("사무실 서벽", new Vector3(14.2f, 1.25f, 11.6f), new Vector3(0.16f, 2.5f, 2.8f), Layers.Wall);
            Box("책상", new Vector3(17.0f, 0.375f, 12.1f), new Vector3(1.4f, 0.75f, 0.7f), Layers.Wall);
            Box("서류장", new Vector3(19.1f, 0.6f, 12.5f), new Vector3(0.5f, 1.2f, 0.4f), Layers.Wall);
            // 가구·기구(큰 것 = Wall, 낮은 것 = PlayerOnly 카메라 통과)
            Box("락커", new Vector3(0.45f, 0.9f, 3.2f), new Vector3(0.5f, 1.8f, 4.8f), Layers.Wall);
            Box("신발장", new Vector3(0.35f, 0.6f, 6.8f), new Vector3(0.36f, 1.2f, 0.9f), Layers.Wall);
            Box("음수대", new Vector3(2.4f, 0.52f, 12.6f), new Vector3(0.36f, 1.05f, 0.36f), Layers.Wall);
            Box("바벨랙", new Vector3(7.8f, 0.9f, 12.0f), new Vector3(1.2f, 1.8f, 1.2f), Layers.Wall);
            Box("덤벨랙", new Vector3(4.5f, 0.4f, 12.35f), new Vector3(1.4f, 0.8f, 0.6f), Layers.PlayerOnly);
            Box("벤치", new Vector3(5.5f, 0.22f, 10.0f), new Vector3(1.2f, 0.45f, 0.35f), Layers.PlayerOnly);
            Box("타이어", new Vector3(7.5f, 0.22f, 9.3f), new Vector3(1.3f, 0.45f, 1.3f), Layers.PlayerOnly);
            Box("화이트보드", new Vector3(8.7f, 0.9f, 12.2f), new Vector3(1.2f, 1.85f, 0.5f), Layers.Wall);
            Box("더미", new Vector3(4.0f, 0.75f, 4.5f), new Vector3(0.45f, 1.5f, 0.45f), Layers.PlayerOnly);
            Box("매트 롤", new Vector3(10.6f, 0.9f, 5.6f), new Vector3(0.5f, 1.8f, 0.5f), Layers.Wall);
            Box("스피드백 판", new Vector3(19.0f, 1.65f, 0.9f), new Vector3(0.6f, 0.45f, 0.6f), Layers.PlayerOnly);
            for (int i = 0; i < 3; i++) Box($"접의자 {i + 1}", new Vector3(10.5f + 0.8f * i, 0.4f, 12.55f), new Vector3(0.45f, 0.8f, 0.5f), Layers.PlayerOnly);
            return n;
        }
    }
}
