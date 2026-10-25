// 행인1의 메인이벤트 — 룩데브 비교 사진 한 번에(docs/14 4-11 · 9장 '구현 결과'): -lookshots <폴더> [-lookvariants base,ink,haze,light,detail,lut] (그래픽 필요)
//   Zone1 12장(EnvSetup.Shots 와 같은 자리·탐색 카메라 B — 발·방향은 shots_final7/shots.txt 값 고정) · 실내 4곳 8장(키트 소켓 Sock_Cam_s01~s08, 그 시각의 사람·불)
//   · 체육관 6장(Sock_Cam_g01~g06 — ZGymShots 와 같은 시우 자리·시간대) → <폴더>/<변형>/{z1_*,st_s0*,gym_g0*}.png + shots.txt
//   변형(-lookvariants): 단계별로 한 실행에서 켜고 끄며 찍는다(LookdevFlags) — base = 모두 끔(② 색 설계만), ink = +③ 먹선, haze = +④ 안개,
//   light = +⑤ 빛 얼룩·웅덩이, detail = +⑥ 디테일 소품, lut = +① 시간대 LUT(전부 켬). 주지 않으면 'now' 하나(지금 설정 그대로).
//   재기: python tools/lookdev/eval_unity.py <폴더> <결과 폴더> (짝·지표·비교 시트)
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.TestTools;

namespace Haengin.Tests
{
    public sealed class ZLookShots
    {
        [UnityTearDown] public IEnumerator Down() { yield return M3Lab.TearDown(); }

        /// EnvSetup.PreviewSpots 결과(2026-10-25 7차 shots.txt) — (파일, 발, yaw, 시간대)
        static readonly (string file, Vector3 feet, float yaw, DayPart part)[] Zone =
        {
            ("z1_shot_1_night", new Vector3(-193.0f, 30.1f, 100.9f), 72f, DayPart.Night),
            ("z1_shot_2", new Vector3(-171.0f, 27.2f, 89.2f), -136f, DayPart.Dusk),
            ("z1_shot_3", new Vector3(-161.5f, 28.0f, 125.2f), 3f, DayPart.Dawn),
            ("z1_shot_4", new Vector3(-198.5f, 31.2f, 106.6f), 8f, DayPart.Dawn),
            ("z1_shot_5", new Vector3(-217.4f, 40.3f, 141.8f), 96f, DayPart.Day),
            ("z1_shot_6", new Vector3(-158.3f, 27.4f, 120.3f), 115f, DayPart.Day),
            ("z1_shot_7_night", new Vector3(-172.0f, 27.1f, 115.9f), 52f, DayPart.Night),
            ("z1_shot_8", new Vector3(-162.7f, 36.1f, 150.7f), 96f, DayPart.Day),
            ("z1_shot_1", new Vector3(-193.0f, 30.1f, 100.9f), 72f, DayPart.Day),
            ("z1_shot_7", new Vector3(-172.0f, 27.1f, 115.9f), 52f, DayPart.Day),
            ("z1_tod_1_Dawn", new Vector3(-193.0f, 30.1f, 100.9f), 72f, DayPart.Dawn),
            ("z1_tod_1_Dusk", new Vector3(-193.0f, 30.1f, 100.9f), 72f, DayPart.Dusk),
        };
        /// 실내 — (무대, 카메라, 시각(분) — 프레임과 같은 때)
        static readonly (string key, string cam, int minutes)[] Indoor =
        {
            ("Gukbap", "s01", 5 * 60 + 40), ("Gukbap", "s02", 19 * 60 + 10), ("Home", "s03", 7 * 60 + 20), ("Home", "s04", 23 * 60 + 10),
            ("Conv", "s05", 22 * 60), ("Conv", "s06", 21 * 60 + 30), ("School", "s07", 10 * 60 + 20), ("School", "s08", 10 * 60 + 30),
        };

        [UnityTest, Timeout(3600000)]
        public IEnumerator Z_LookShots()
        {
            string dir = Shots.Arg("-lookshots");
            if (string.IsNullOrEmpty(dir)) { Assert.Ignore("-lookshots <폴더> 를 줄 때만"); yield break; }
            if (!Shots.Graphics) { Assert.Ignore("그래픽 장치 없음"); yield break; }
#if UNITY_EDITOR
            UnityEditor.ShaderUtil.allowAsyncCompilation = false;
#endif
            var variants = (Shots.Arg("-lookvariants") ?? "now").Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
            string only = Shots.Arg("-lookonly") ?? "";      // z1 · st · gym 중 일부만(쉼표)
            string files = Shots.Arg("-lookfiles") ?? "";    // 파일 이름 일부만(쉼표, 예: z1_shot_1_night)
            bool Want(string g) => only.Length == 0 || only.Split(',').Contains(g);
            foreach (var v in variants) Directory.CreateDirectory(Path.Combine(dir, v));
            var log = new List<string>();
            yield return M3Lab.OpenZone1();
            var r = M3Lab.Runner;
            var cam = Camera.main;
            var brain = cam.GetComponent<CinemachineBrain>();
            // 게임 화면이 아닌 것(체크포인트·스폰·전투 무대 표시·이름표) — EnvSetup.Shots 와 같게
            var hidden = new List<GameObject>();
            foreach (var n in new[] { "Zone1/Route", "Zone1/Spawn", "Zone1/Landmarks/arena", "Zone1/Landmarks/npc_spot", "Zone1/Shots", "Stages" })
            { var g = GameObject.Find(n); if (g != null) { g.SetActive(false); hidden.Add(g); } }
            void HideLabels()
            {
                foreach (var t in Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    if (t.GetComponentInParent<Canvas>() == null && !t.name.StartsWith("글자")) t.gameObject.SetActive(false);
            }
            HideLabels();

            IEnumerator Capture(string file)
            {
                foreach (var v in variants)
                {
                    LookFlags(v);
                    yield return null; yield return null;
                    Shots.Shot(cam, Path.Combine(dir, v, file + ".png"), 1920, 1080, 2, false);
                }
                LookFlags("now");
            }

            // ① Zone1
            if (Want("z1"))
            {
                brain.enabled = false;
                foreach (var (file, feet, yaw, part) in Zone)
                {
                    if (files.Length > 0 && !files.Split(',').Contains(file)) continue;
                    r.Light.Apply(part, true);
                    M3Lab.Motor.Teleport(feet, yaw);
                    yield return Lab.Frames(12);
                    var rot = Quaternion.Euler(3f, yaw, 0f);
                    var tgt = feet + Vector3.up * 1.15f;
                    var back = -(rot * Vector3.forward);
                    float dd = 5.5f;
                    if (Physics.SphereCast(tgt, 0.1f, back, out var hit, dd, (1 << Layers.Ground) | (1 << Layers.Wall), QueryTriggerInteraction.Ignore)) dd = Mathf.Max(0.6f, hit.distance - 0.05f);
                    cam.transform.SetPositionAndRotation(tgt + back * dd + (rot * Vector3.right) * (Mathf.Tan(53f * 0.5f * Mathf.Deg2Rad) * dd * 0.06f * 16f / 9f), rot);
                    cam.fieldOfView = 53f; cam.nearClipPlane = 0.08f; cam.farClipPlane = 900f;
                    yield return Lab.Frames(3);
                    yield return Capture(file);
                    log.Add($"{file}: {part} 발 {feet} yaw {yaw:F0} 거리 {dd:F2}");
                }
                brain.enabled = true;
            }

            // ② 실내 4곳
            foreach (var g in hidden) if (g != null && g.name == "Stages") g.SetActive(true);
            if (Want("st"))
            {
                var loader = r.Loader;
                foreach (var (key, c, minutes) in Indoor)
                {
                    GameClock.SetDate(2026, 3, 12, minutes);
                    r.Light.ApplyClock(minutes);
                    yield return loader.Go(key, "Door", false, Vector3.zero, 0f, false);
                    yield return Lab.Frames(20);
                    var place = StagePlace.Find(key);
                    Assert.IsNotNull(place, key);
                    var life = place.GetComponent<StageLife>();
                    if (life != null) life.Apply(minutes);
                    if (brain != null) brain.enabled = false;
                    foreach (var rr in M3Lab.Motor.GetComponentsInChildren<Renderer>()) rr.enabled = false;
                    Transform t = null;
                    foreach (var tr in place.GetComponentsInChildren<Transform>(true)) if (tr.name == "Sock_Cam_" + c) { t = tr; break; }
                    Assert.IsNotNull(t, "Sock_Cam_" + c);
                    cam.transform.SetPositionAndRotation(t.position, t.rotation);
                    cam.fieldOfView = 48f; cam.nearClipPlane = 0.05f;
                    HideLabels();
                    yield return Lab.Frames(4);
                    yield return Capture("st_" + c);
                    foreach (var rr in M3Lab.Motor.GetComponentsInChildren<Renderer>()) rr.enabled = true;
                    if (brain != null) brain.enabled = true;
                    log.Add($"st_{c}: {key} {minutes / 60:00}:{minutes % 60:00} · 사람 {(life != null ? life.ActiveSlots : -1)}");
                }
            }

            // ③ 체육관(ZGymShots 와 같은 자리·시간대)
            if (Want("gym"))
            {
                GymState.Current = new GymSave();
                yield return r.Loader.Go(SceneLoader.Gym, "Floor", false, Vector3.zero, 0f, false);
                yield return null; yield return null;
                var gym = GymManager.Instance;
                Assert.NotNull(gym, "GymManager");
                var frames = new (string key, GymPart part, DayPart light, Vector3 local, float yaw)[]
                {
                    ("g01", GymPart.Day, DayPart.Day, new Vector3(3.5f, 0f, 7.0f), 150f), ("g02", GymPart.Evening, DayPart.Dusk, new Vector3(13.1f, 0.25f, 5.0f), 55f),
                    ("g03", GymPart.Evening, DayPart.Dusk, new Vector3(9.0f, 0.03f, 3.3f), 180f), ("g04", GymPart.Morning, DayPart.Day, new Vector3(6.6f, 0f, 9.8f), 300f),
                    ("g05", GymPart.Day, DayPart.Day, new Vector3(16.0f, 0f, 9.2f), 0f), ("g06", GymPart.Night, DayPart.Night, new Vector3(8.5f, 0.03f, 4.0f), 90f),
                };
                foreach (var (key, part, light, local, yaw) in frames)
                {
                    r.Light.Apply(light, true);
                    gym.ApplyPart(part);
                    if (key == "g06") { var mt = gym.Sock("Sock_NPC_Coach_MatEdge"); if (gym.Yunjun != null && mt != null) { gym.Yunjun.gameObject.SetActive(true); gym.Yunjun.Place(mt.position, mt.forward, GymNpc.Routine.Mitts); } }
                    if (key == "g02") { var gc = gym.Sock("Sock_NPC_Gojun_Cage"); if (gym.Gojun != null && gc != null) { gym.Gojun.gameObject.SetActive(true); gym.Gojun.Place(gc.position, gc.forward, GymNpc.Routine.Shadow); } }
                    M3Lab.Motor.Teleport(gym.transform.TransformPoint(local), yaw);
                    var sock = gym.Sock("Sock_Cam_" + key);
                    yield return Lab.Frames(45);
                    HideLabels();
                    brain.enabled = false;
                    if (gym.Mirror != null) gym.Mirror.Set(key == "g01" || key == "g03");
                    cam.transform.SetPositionAndRotation(sock.position, sock.rotation);
                    float fovWas = cam.fieldOfView; cam.fieldOfView = 50f;
                    yield return null;
                    foreach (var v in variants)
                    {
                        LookFlags(v);
                        yield return null; yield return null;
                        if (gym.Mirror != null && gym.Mirror.Active) gym.Mirror.Render(cam);
                        Shots.Shot(cam, Path.Combine(dir, v, "gym_" + key + ".png"), 1920, 1080, 2, false);
                    }
                    LookFlags("now");
                    cam.fieldOfView = fovWas;
                    brain.enabled = true;
                    if (gym.Mirror != null) gym.Mirror.Set(false);
                    log.Add($"gym_{key}: {GymLights.Name(part)}");
                }
            }
            File.WriteAllLines(Path.Combine(dir, "shots.txt"), log);
            Debug.Log($"[Look] 룩데브 사진 {log.Count}곳 × 변형 {variants.Length}({string.Join(",", variants)}) → {dir}\n{string.Join("\n", log)}");
        }

        /// 변형 이름 → 단계 켜기(런타임 LookdevFlags 가 생기기 전 판에서는 아무것도 안 함)
        // 진단 변형(dbg_*): 밤 길이 왜 밝은지 — 해(달)·주변광·점광·빛 웅덩이를 하나씩 끔(장면 값을 바꿨다가 'now' 에서 되돌림)
        static float sunWas = -1f; static Color ambS, ambE, ambG; static readonly List<Light> offLights = new List<Light>(); static readonly List<Renderer> offPools = new List<Renderer>();
        static void DebugLight(string v)
        {
            var sun = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional && l.enabled);
            if (sunWas >= 0f && sun != null) sun.intensity = sunWas;
            if (sunWas >= 0f) { RenderSettings.ambientSkyColor = ambS; RenderSettings.ambientEquatorColor = ambE; RenderSettings.ambientGroundColor = ambG; }
            foreach (var l in offLights) if (l != null) l.enabled = true;
            foreach (var r in offPools) if (r != null) r.enabled = true;
            offLights.Clear(); offPools.Clear(); sunWas = -1f;
            if (!v.StartsWith("dbg_")) return;
            sunWas = sun != null ? sun.intensity : 0f; ambS = RenderSettings.ambientSkyColor; ambE = RenderSettings.ambientEquatorColor; ambG = RenderSettings.ambientGroundColor;
            if (v.Contains("nosun") && sun != null) sun.intensity = 0f;
            if (v.Contains("noamb")) RenderSettings.ambientSkyColor = RenderSettings.ambientEquatorColor = RenderSettings.ambientGroundColor = Color.black;
            if (v.Contains("nolights")) foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type != LightType.Directional && l.enabled) { l.enabled = false; offLights.Add(l); }
            if (v.Contains("nopool")) foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)) if (r.enabled && r.sharedMaterial != null && r.sharedMaterial.shader != null && r.sharedMaterial.shader.name == "Haengin/LightPool") { r.enabled = false; offPools.Add(r); }
        }

        static void LookFlags(string v)
        {
            DebugLight(v);
            var t = System.Type.GetType("Haengin.LookdevFlags, Haengin.Game");
            var m = t?.GetMethod("Variant", new[] { typeof(string) });
            m?.Invoke(null, new object[] { v });
        }
    }
}
