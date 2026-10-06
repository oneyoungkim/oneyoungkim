// 행인1의 메인이벤트 — M3 3단계 테스트 D07 (docs/09_M3_버티컬슬라이스_설계.md 6-2)
//   D07 Lighting_Presets : 4종 적용 — 값 = 에셋(방향광·주변광·하늘·안개·빛 자리 켜짐·창문 불 비율), 하늘 ↔ 안개 L* 차 ≥ 6,
//                          (그래픽이 있을 때) 걷는 면 화면 L* 밤 ≥ 22 · 새벽 ≥ 28(Zone1 다섯 자리, 실제 게임 카메라),
//                          프리셋을 바꿔도 Zone1 장면·필름 볼륨·프리셋 에셋 파일이 그대로(결정적 저장 같음)
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Haengin.Tests
{
    public sealed class M3LightTests
    {
        [UnityTearDown] public IEnumerator TearDown() => M3Lab.TearDown();

        public static readonly (string name, Vector3 pos, float yaw)[] Spots =
        {
            ("상가거리", new Vector3(-186.0f, 29.6f, 103.4f), 262f),
            ("주차장", new Vector3(-172.0f, 27.2f, 87.6f), 200f),
            ("명륜3가 골목", new Vector3(-170.0f, 27.2f, 117.5f), 40f),
            ("와룡공원길", new Vector3(-222.0f, 33.0f, 96.4f), 272f),
            ("시우네 앞", new Vector3(-163.5f, 28.0f, 122.4f), 50f),
        };

        static string Hash(string assetPath)
        {
            var full = Path.GetFullPath(assetPath);
            if (!File.Exists(full)) return "없음";
            using var md5 = MD5.Create();
            return System.BitConverter.ToString(md5.ComputeHash(File.ReadAllBytes(full))).Replace("-", "").Substring(0, 12);
        }

        static bool Near(Color a, Color b, float e = 0.01f) => Mathf.Abs(a.r - b.r) < e && Mathf.Abs(a.g - b.g) < e && Mathf.Abs(a.b - b.b) < e;

        /// 걷는 면(Ground) 화소의 평균 L* — 화면 32×18 격자에서 카메라 광선이 Ground 에 닿는 칸만
        public static float GroundL(Camera cam, out int samples)
        {
            const int W = 640, H = 360;
            var tex = Shots.Grab(cam, W, H, 1, false);
            float sum = 0f; samples = 0;
            for (int gy = 0; gy < 18; gy++)
                for (int gx = 0; gx < 32; gx++)
                {
                    float u = (gx + 0.5f) / 32f, v = (gy + 0.5f) / 18f;
                    var ray = cam.ViewportPointToRay(new Vector3(u, v, 0f));
                    if (!Physics.Raycast(ray, out var hit, 120f, ~Layers.Mask(Layers.IgnoreRaycast, Layers.Player, Layers.Interact), QueryTriggerInteraction.Ignore)) continue;
                    if (hit.collider.gameObject.layer != Layers.Ground) continue;
                    sum += Shots.LStar(tex.GetPixel((int)(u * W), (int)(v * H)));
                    samples++;
                }
            Object.Destroy(tex);
            return samples > 0 ? sum / samples : 0f;
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator D07_Lighting_Presets()
        {
            yield return M3Lab.OpenZone1();
            var r = M3Lab.Runner;
            var day = r.Light;
            Assert.NotNull(day, "DayLight");
            string[] files = { "Assets/_Project/Scenes/Zone1.unity", "Assets/_Project/Settings/Zone1_FilmVolume.asset" };
            var files2 = files.Concat(new[] { DayPart.Dawn, DayPart.Day, DayPart.Dusk, DayPart.Night }.Select(p => $"Assets/_Project/Settings/Light_{p}.asset")).ToArray();
            var before = files2.Select(Hash).ToArray();
            var log = new StringBuilder();
            var fails = new List<string>();
            int windowsTotal = LightSpot.All.Count(s => s.Kind == LightKind.Window);
            foreach (DayPart part in new[] { DayPart.Dawn, DayPart.Day, DayPart.Dusk, DayPart.Night })
            {
                var p = day.Of(part);
                Assert.NotNull(p, part + " 프리셋");
                day.Apply(part, true);
                yield return Lab.Frames(3);
                var sun = day.Sun;
                if (!Near(RenderSettings.fogColor, p.Fog)) fails.Add($"{part} 안개 {RenderSettings.fogColor} ≠ {p.Fog}");
                if (!Near(RenderSettings.ambientSkyColor, p.AmbientSky)) fails.Add($"{part} 주변광(하늘) 다름");
                if (Camera.main != null && !Near(Camera.main.backgroundColor, p.Sky)) fails.Add($"{part} 하늘 {Camera.main.backgroundColor} ≠ {p.Sky}");
                if (sun != null && Mathf.Abs(sun.intensity - p.SunIntensity) > 0.01f) fails.Add($"{part} 해 세기 {sun.intensity} ≠ {p.SunIntensity}");
                if (Mathf.Abs(RenderSettings.fogEndDistance - p.FogEnd) > 0.1f) fails.Add($"{part} 안개 끝 다름");
                bool Want(LightKind k) => k switch { LightKind.Street => p.StreetLamps, LightKind.Security => p.SecurityLamps, LightKind.Vending => p.Vending, LightKind.ConvAwning => p.ConvAwning, LightKind.GukbapWindow => p.GukbapWindow, LightKind.ShopSign => p.ShopSigns, _ => false };
                foreach (var s in LightSpot.All.Where(x => x.Kind != LightKind.Window))
                    if (s.Lit != Want(s.Kind)) fails.Add($"{part} {s.name} 켜짐 {s.Lit} ≠ {Want(s.Kind)}");
                int lit = LightSpot.All.Count(s => s.Kind == LightKind.Window && s.Lit);
                float ratio = windowsTotal > 0 ? lit / (float)windowsTotal : 0f;
                if (Mathf.Abs(ratio - p.Windows) > 0.08f) fails.Add($"{part} 창문 불 {ratio:P0} ≠ {p.Windows:P0}");
                float lSky = DayLight.LStar(p.Sky), lFog = DayLight.LStar(p.Fog);
                if (Mathf.Abs(lSky - lFog) < 6f) fails.Add($"{part} 하늘 L* {lSky:F1} ↔ 안개 {lFog:F1} 차 < 6");
                string lstar = "그래픽 없음";
                if (Shots.Graphics && (part == DayPart.Night || part == DayPart.Dawn || part == DayPart.Dusk))
                {
                    var vals = new List<string>();
                    float min = 999f;
                    foreach (var (name, pos, yaw) in Spots)
                    {
                        M3Lab.Motor.Teleport(pos, yaw);
                        M3Lab.Cam.SnapBehind();
                        yield return Lab.Frames(8);
                        float l = GroundL(Camera.main, out int n);
                        vals.Add($"{name} {l:F1}({n})");
                        if (n > 20) min = Mathf.Min(min, l);
                    }
                    float need = part == DayPart.Night ? 22f : part == DayPart.Dawn ? 28f : 0f;
                    lstar = $"걷는 면 L* 최소 {min:F1}(기준 {need}) — {string.Join(" · ", vals)}";
                    if (min < need) fails.Add($"{part} 걷는 면 L* {min:F1} < {need}");
                }
                log.AppendLine($"{part}: 해 {p.SunPitch:F0}°/{p.SunYaw:F0}° ×{p.SunIntensity:F2} · 하늘 L* {lSky:F1} / 안개 {lFog:F1} · 빛 자리 {day.CountLit()}곳 · 창문 {lit}/{windowsTotal}({ratio:P0}) · {lstar}");
            }
            yield return Lab.Frames(3);
            var after = files2.Select(Hash).ToArray();
            for (int i = 0; i < files2.Length; i++)
                if (before[i] != after[i]) fails.Add($"파일이 바뀜: {files2[i]}");
            log.AppendLine($"파일 그대로: {string.Join(", ", files2.Select((f, i) => Path.GetFileName(f) + " " + after[i]))}");
            Debug.Log($"[M3Test] D07 Lighting_Presets: 실패 {fails.Count}\n{log}");
            Assert.IsEmpty(fails, string.Join("\n", fails));
        }
    }
}
