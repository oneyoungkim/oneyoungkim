// 행인1의 메인이벤트 — 5차 확인 사진: 간판 근경 8장(낮·밤 × 4곳) · 근경 디테일 6장. -executeMethod Haengin.EditorGame.EnvSetup.Closeups5 -out <폴더>(그래픽 필요)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Haengin.EditorTools;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Haengin.EditorGame
{
    public static partial class EnvSetup
    {
        public static void Closeups5()
        {
            int code = 0;
            try
            {
                string outDir = Arg("-out") ?? throw new ArgumentException("-out <폴더>");
                if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) throw new Exception("그래픽 장치 없음(-nographics 빼기)");
                Directory.CreateDirectory(outDir);
                ShaderUtil.allowAsyncCompilation = false;
                EditorSceneManager.OpenScene(Root + "/Scenes/Zone1.unity", OpenSceneMode.Single);
                var d = Zone1Data.Load();
                var cam = Camera.main ?? throw new Exception("Main Camera 없음");
                var day = Object.FindFirstObjectByType<DayLight>() ?? throw new Exception("DayLight 없음");
                DayLight.EditModeFilm = true;
                if (LightSpot.All.Count == 0) foreach (var s in Object.FindObjectsByType<LightSpot>(FindObjectsInactive.Include, FindObjectsSortMode.None)) LightSpot.All.Add(s);
                var sky = Object.FindFirstObjectByType<SkyCards>();
                foreach (var nm in new[] { "Zone1/Route", "Zone1/Spawn", "Zone1/Landmarks/arena", "Zone1/Landmarks/npc_spot", "Zone1/Shots", "Stages" })
                { var g = GameObject.Find(nm); if (g != null) g.SetActive(false); }
                foreach (var t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    if (!t.name.StartsWith("글자") && !t.name.StartsWith("번지")) t.gameObject.SetActive(false);
                foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) c.gameObject.SetActive(false);
                var player = GameObject.Find("Player"); if (player != null) player.SetActive(false);
                var man = Manifest(); var objs = new Dictionary<string, Dictionary<string, object>>();
                foreach (var o in L(man["objects"])) { var od = D(o); objs[S(od, "name")] = od; }
                Vector3 SignC(string n) => V3(G(D(G(objs[n], "info")), "center"));
                Vector3 SignN(string n) => V3(G(D(G(objs[n], "info")), "normal")).normalized;
                var log = new List<string>();
                void Shot(string file, Vector3 from, Vector3 at, float fov, DayPart part)
                {
                    cam.transform.SetPositionAndRotation(from, Quaternion.LookRotation(at - from, Vector3.up));
                    cam.fieldOfView = fov; cam.nearClipPlane = 0.05f; cam.farClipPlane = 900f;
                    day.Apply(part, true);
                    if (sky != null) { sky.Follow(cam); sky.Apply(part); }
                    BatchTools.RenderToPng(cam, 1600, 900, 2, Path.Combine(outDir, file + ".png"));
                    log.Add($"{file}: {part} 카메라 ({from.x:F1},{from.y:F1},{from.z:F1}) → ({at.x:F1},{at.y:F1},{at.z:F1}) FOV {fov}");
                }
                Vector3 G0(Vector3 p) => GroundAt(p, 6f, 10f);
                // 간판 4곳 × 낮·밤
                var spots = new List<(string name, Vector3 from, Vector3 at, float fov)>();
                {
                    var c = SignC("Sign_002"); var n = SignN("Sign_002"); var r = Vector3.Cross(Vector3.up, n).normalized;
                    spots.Add(("a_hoomunA", G0(c + n * 8.5f - r * 2.5f) + Vector3.up * 1.6f, c + Vector3.up * 1.2f, 50f));
                }
                {
                    var c = (SignC("Sign_005") + SignC("Sign_006")) / 2; var n = SignN("Sign_005"); var r = Vector3.Cross(Vector3.up, n).normalized;
                    spots.Add(("b_gukbap_haru", G0(c + n * 6.5f + r * 1.5f) + Vector3.up * 1.6f, c - Vector3.up * 1.0f, 55f));
                }
                {
                    var c = SignC("Sign_009"); var n = SignN("Sign_009"); var r = Vector3.Cross(Vector3.up, n).normalized;
                    spots.Add(("c_laundry", G0(c + n * 7f + r * 3f) + Vector3.up * 1.6f, c + Vector3.up * 1.5f, 55f));
                }
                {
                    var gym = GameObject.Find("Zone1/Env/Signs5/혜화 MMA 입구");
                    var door = gym != null ? gym.transform.Find("안쪽(어둠)") : null;
                    if (door != null)
                    {
                        var n = -door.forward; var c = door.position;
                        spots.Add(("d_gym", c + n * 6f + Vector3.Cross(Vector3.up, n).normalized * 2.0f + Vector3.up * 0.4f, c + Vector3.up * 1.3f, 50f));
                    }
                }
                foreach (var (name, from, at, fov) in spots) { Shot($"sign_{name}_day", from, at, fov, DayPart.Day); Shot($"sign_{name}_night", from, at, fov, DayPart.Night); }
                // 근경 디테일 6
                {
                    var p = G0(new Vector3(-170.6f, 27.2f, 116.6f)); Shot("detail_1_alley_walls", p + Vector3.up * 1.5f, p + new Vector3(6f, 2.6f, 6.5f), 55f, DayPart.Day);
                }
                {
                    var c = SignC("Sign_009"); var n = SignN("Sign_009"); var r = Vector3.Cross(Vector3.up, n).normalized;
                    Shot("detail_2_streaks_laundry", G0(c + n * 4.5f - r * 1.5f) + Vector3.up * 1.7f, c + Vector3.up * 3.5f - r * 1f, 55f, DayPart.Day);
                }
                {
                    var pad = d.Pads.First(x => x.Name.Contains("주차장")); var rot = Quaternion.Euler(0f, pad.Yaw, 0f);
                    var at = pad.Center + rot * new Vector3(pad.Size.x * 0.5f, 1.0f, -0.5f); var from = pad.Center + rot * new Vector3(-pad.Size.x * 0.15f, 1.6f, -pad.Size.y * 0.3f);
                    Shot("detail_3_parking_mural", G0(from) + Vector3.up * 1.6f, G0(at) + Vector3.up * 1.0f, 55f, DayPart.Dusk);
                }
                {
                    var l = d.Landmarks.First(x => x.Name.Contains("하루편의점 후문점 출입문")); var n = ToRoadDir(d, l.Pos); var r = Vector3.Cross(Vector3.up, n).normalized;
                    Shot("detail_4_conv_front", G0(l.Pos + n * 5f + r * 2.5f) + Vector3.up * 1.5f, G0(l.Pos) + Vector3.up * 0.8f, 55f, DayPart.Night);
                }
                {
                    var st = d.Stairs.First(x => x.Name.Contains("골목 계단")); Vector3 lo = st.From.y <= st.To.y ? st.From : st.To, hi = st.From.y <= st.To.y ? st.To : st.From;
                    var dir = Flat(hi - lo).normalized;
                    Shot("detail_5_stairs_cracks", lo - dir * 2.2f + Vector3.up * 1.4f, Vector3.Lerp(lo, hi, 0.35f), 55f, DayPart.Dawn);
                }
                {
                    var pole = new Vector3(-196.0f, 30.6f, 102.9f); var g = G0(pole);
                    Shot("detail_6_pole_pigeons", g + new Vector3(3.2f, 1.2f, -2.6f), g + new Vector3(0f, 1.1f, -1.0f), 55f, DayPart.Day);
                }
                File.WriteAllLines(Path.Combine(outDir, "closeups.txt"), log, new System.Text.UTF8Encoding(true));
                Debug.Log($"{Tag} 5차 근경 사진 {log.Count}장 → {outDir}");
            }
            catch (Exception e) { Debug.LogError($"{Tag} Closeups5 실패: {e}"); code = 1; }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }
    }
}
