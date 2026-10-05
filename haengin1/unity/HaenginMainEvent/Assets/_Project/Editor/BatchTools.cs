// 행인1의 메인이벤트 — batchmode 전용 도구 모음
// 사용 예는 haengin1/unity/README.md 참고. GUI 없이 -batchmode 로만 부른다.
//   스크린샷 : -executeMethod Haengin.EditorTools.BatchTools.Screenshot   (-nographics 없이! GPU 렌더 필요)
//              [-view 오브젝트 경로] [-fov 도] [-nofog] [-topdown [-area x0,z0,x1,z1] [-shadows]] — Zone1 촬영용
//   윈도 빌드: -executeMethod Haengin.EditorTools.BatchTools.BuildWindows
//   임포트 점검: -executeMethod Haengin.EditorTools.BatchTools.ImportCheck
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Haengin.EditorTools
{
    public static class BatchTools
    {
        public const string SandboxScene = "Assets/_Project/Scenes/Sandbox.unity";
        const string Tag = "[BatchTools]";

        // ───────────────────────── 명령줄 인자
        static string Arg(string name, string def = null)
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++)
                if (string.Equals(a[i], name, StringComparison.OrdinalIgnoreCase)) return a[i + 1];
            return def;
        }

        static int ArgInt(string name, int def) =>
            int.TryParse(Arg(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : def;

        static float ArgFloat(string name, float def) =>
            float.TryParse(Arg(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : def;

        static bool HasFlag(string name) =>
            Environment.GetCommandLineArgs().Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));

        static string FullPath(string p) =>
            Path.IsPathRooted(p) ? p : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), p));

        // ───────────────────────── 1) 스크린샷
        /// <summary>
        /// -scene &lt;장면 경로&gt; -out &lt;png&gt; [-width 1920] [-height 1080] [-yaw 0] [-camera 이름] [-pivot Siwoo] [-supersample 2] [-show 이름,..] [-hide 이름,..]
        /// 메인 카메라를 RenderTexture 에 렌더해 PNG 로 저장. Cinemachine 카메라가 있으면 그 자세·렌즈를 메인 카메라에 옮긴 뒤 찍는다.
        /// -yaw 는 피사체(-pivot 오브젝트) 둘레로 카메라를 돌리는 각도(도). 3/4 컷 = 35 정도.
        /// -view 이름 : 그 오브젝트(예: Zone1/Shots/Shot_Start)의 위치·방향으로 찍는다. 꺼진 Camera 가 붙어 있으면 그 화각도 쓴다.
        /// -fov 도 : 화각 덮어쓰기. -nofog : 안개 끄고 찍기.
        /// -topdown : 위에서 똑바로 내려다보는 직교 투영(북쪽 = 위). -area x0,z0,x1,z1 로 찍을 범위(없으면 모든 렌더러 범위).
        ///            찍는 동안만 안개와 그림자를 끈다(-shadows 면 그림자 거리를 늘려 넣음). 장면·설정 파일은 저장하지 않는다.
        /// </summary>
        public static void Screenshot()
        {
            int code = 0;
            try
            {
                string scenePath = Arg("-scene", SandboxScene);
                string outPath = Arg("-out") ?? throw new ArgumentException("-out <png 경로> 가 필요합니다");
                int w = ArgInt("-width", 1920), h = ArgInt("-height", 1080);
                int ss = Mathf.Clamp(ArgInt("-supersample", 2), 1, 4);
                float yaw = ArgFloat("-yaw", 0f);
                string camName = Arg("-camera");
                string pivotName = Arg("-pivot", "Siwoo");

                if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                    throw new Exception("그래픽 장치가 없습니다. -nographics 를 빼고 -batchmode 만으로 실행하세요");

                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                ShaderUtil.allowAsyncCompilation = false; // 셰이더 자리표시(하늘색) 대신 실제 셰이더로 찍기

                // -show / -hide 이름1,이름2 : 찍을 때만 루트 오브젝트를 켜고 끈다(장면 파일은 저장하지 않음)
                SetRootsActive(Arg("-show"), true);
                SetRootsActive(Arg("-hide"), false);

                var cam = Camera.main ?? UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).FirstOrDefault()
                          ?? throw new Exception("장면에 카메라가 없습니다");

                string vcamUsed = ApplyCinemachine(cam, camName);
                if (Mathf.Abs(yaw) > 0.001f)
                {
                    var pivotGo = GameObject.Find(pivotName);
                    Vector3 pivot = pivotGo ? pivotGo.transform.position : Vector3.zero;
                    pivot.y = 0f;
                    cam.transform.RotateAround(pivot, Vector3.up, yaw);
                }

                string viewName = Arg("-view");
                if (!string.IsNullOrEmpty(viewName))
                {
                    var v = GameObject.Find(viewName) ?? throw new Exception($"-view 오브젝트 '{viewName}' 를 찾지 못했습니다");
                    cam.transform.SetPositionAndRotation(v.transform.position, v.transform.rotation);
                    var vc = v.GetComponent<Camera>();
                    if (vc != null) { cam.fieldOfView = vc.fieldOfView; cam.nearClipPlane = vc.nearClipPlane; cam.farClipPlane = vc.farClipPlane; }
                }
                float fov = ArgFloat("-fov", -1f);
                if (fov > 0f) cam.fieldOfView = fov;
                if (HasFlag("-nofog")) RenderSettings.fog = false;

                Action restore = null;
                if (HasFlag("-topdown")) restore = SetupTopDown(cam, Arg("-area"), (float)w / h);
                Directory.CreateDirectory(Path.GetDirectoryName(FullPath(outPath)));
                try { RenderToPng(cam, w, h, ss, FullPath(outPath)); }
                finally { restore?.Invoke(); }
                Debug.Log($"{Tag} 스크린샷 저장 완료: {FullPath(outPath)} ({w}x{h}, 슈퍼샘플 {ss}x) 장면={scenePath} 카메라={cam.name} CM={vcamUsed ?? "없음"} yaw={yaw} " +
                          $"view={viewName ?? "없음"} 위치={cam.transform.position} 회전={cam.transform.eulerAngles} " +
                          (cam.orthographic ? $"직교 크기={cam.orthographicSize:F1}" : $"FOV={cam.fieldOfView}"));
            }
            catch (Exception e)
            {
                Debug.LogError($"{Tag} 스크린샷 실패: {e}");
                code = 1;
            }
            EditorApplication.Exit(code);
        }

        /// 위에서 내려다보는 직교 카메라. 화면 위 = 북(+Z), 오른쪽 = 동(+X). 되돌리기 함수를 돌려준다.
        static Action SetupTopDown(Camera cam, string area, float aspect)
        {
            var rs = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r => r.enabled && r.gameObject.activeInHierarchy).ToList();
            if (rs.Count == 0) throw new Exception("장면에 렌더러가 없습니다");
            var all = rs[0].bounds;
            foreach (var r in rs) all.Encapsulate(r.bounds);
            float x0 = all.min.x, x1 = all.max.x, z0 = all.min.z, z1 = all.max.z;
            if (!string.IsNullOrEmpty(area))
            {
                var v = area.Split(',').Select(t => float.Parse(t.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture)).ToArray();
                if (v.Length != 4) throw new ArgumentException("-area 는 x0,z0,x1,z1 네 수입니다");
                x0 = Mathf.Min(v[0], v[2]); x1 = Mathf.Max(v[0], v[2]); z0 = Mathf.Min(v[1], v[3]); z1 = Mathf.Max(v[1], v[3]);
            }
            float top = all.max.y + 50f;
            cam.orthographic = true;
            cam.orthographicSize = Mathf.Max((z1 - z0) / 2f, (x1 - x0) / 2f / aspect) * 1.03f;
            cam.transform.SetPositionAndRotation(new Vector3((x0 + x1) / 2f, top, (z0 + z1) / 2f), Quaternion.Euler(90f, 0f, 0f));
            cam.nearClipPlane = 1f;
            cam.farClipPlane = top - all.min.y + 10f;

            bool fog = RenderSettings.fog;
            RenderSettings.fog = false;
            // 평면도는 그림자 없이(그림자 거리를 0 으로). -shadows 를 주면 그림자 거리를 카메라 깊이만큼 늘려 그림자를 넣는다
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            float shadowDist = urp != null ? urp.shadowDistance : 0f;
            if (urp != null) urp.shadowDistance = HasFlag("-shadows") ? cam.farClipPlane : 0f;
            Debug.Log($"{Tag} 위에서 보기: x {x0:F0}~{x1:F0}, z {z0:F0}~{z1:F0}, 카메라 높이 {top:F0}, 직교 크기 {cam.orthographicSize:F1}");
            return () =>
            {
                RenderSettings.fog = fog;
                if (urp != null) urp.shadowDistance = shadowDist;
            };
        }

        static void SetRootsActive(string names, bool active)
        {
            if (string.IsNullOrEmpty(names)) return;
            var wanted = names.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var n in wanted)
            {
                var go = roots.FirstOrDefault(r => r.name == n) ?? roots.FirstOrDefault(r => r.name.StartsWith(n, StringComparison.Ordinal));
                if (go == null) { Debug.LogWarning($"{Tag} 루트 오브젝트 '{n}' 없음"); continue; }
                go.SetActive(active);
                Debug.Log($"{Tag} {(active ? "켬" : "끔")}: {go.name}");
            }
        }

        /// batchmode 에서는 CinemachineBrain 이 프레임마다 돌지 않으므로, 쓸 CM 카메라의 상태를 직접 계산해 메인 카메라에 복사한다.
        static string ApplyCinemachine(Camera cam, string camName)
        {
            var vcams = UnityEngine.Object.FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (vcams.Length == 0) return null;
            CinemachineCamera vcam = !string.IsNullOrEmpty(camName)
                ? vcams.FirstOrDefault(v => v.name == camName) ?? throw new Exception($"CM 카메라 '{camName}' 를 찾지 못했습니다")
                : vcams.OrderByDescending(v => v.Priority.Value).First();
            vcam.InternalUpdateCameraState(Vector3.up, -1f);
            var st = vcam.State;
            cam.transform.SetPositionAndRotation(st.GetFinalPosition(), st.GetFinalOrientation());
            cam.fieldOfView = st.Lens.FieldOfView;
            cam.nearClipPlane = st.Lens.NearClipPlane;
            cam.farClipPlane = st.Lens.FarClipPlane;
            return vcam.name;
        }

        static void RenderToPng(Camera cam, int w, int h, int ss, string path)
        {
            var desc = new RenderTextureDescriptor(w * ss, h * ss, RenderTextureFormat.ARGB32, 24) { sRGB = true, msaaSamples = 1 };
            var big = new RenderTexture(desc) { name = "BatchShot_Big" };
            var small = new RenderTexture(new RenderTextureDescriptor(w, h, RenderTextureFormat.ARGB32, 0) { sRGB = true }) { name = "BatchShot" };
            big.Create(); small.Create();
            var prevTarget = cam.targetTexture;
            var prevActive = RenderTexture.active;
            try
            {
                cam.targetTexture = big;
                cam.Render(); // 1회 예열(셰이더·그림자 맵)
                cam.Render();
                // 슈퍼샘플 → 축소 (ss 단계만큼 반씩 줄여 평균)
                RenderTexture src = big;
                var temps = new List<RenderTexture>();
                int cw = w * ss, ch = h * ss;
                while (cw / 2 >= w && cw > w)
                {
                    cw /= 2; ch /= 2;
                    var t = (cw == w) ? small : RenderTexture.GetTemporary(new RenderTextureDescriptor(cw, ch, RenderTextureFormat.ARGB32, 0) { sRGB = true });
                    if (t != small) temps.Add(t);
                    Graphics.Blit(src, t);
                    src = t;
                }
                if (src != small) Graphics.Blit(src, small);

                RenderTexture.active = small;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                foreach (var t in temps) RenderTexture.ReleaseTemporary(t);
            }
            finally
            {
                cam.targetTexture = prevTarget;
                RenderTexture.active = prevActive;
                big.Release(); small.Release();
                UnityEngine.Object.DestroyImmediate(big);
                UnityEngine.Object.DestroyImmediate(small);
            }
        }

        // ───────────────────────── 2) Windows 빌드
        /// <summary>
        /// -out &lt;폴더&gt; : 빌드 목록의 장면(M1: Zone1 첫 장면, Sandbox 두 번째)을 Windows 64비트(Mono, 개발 빌드 아님)로 빌드. 실패하면 exit code 1.
        /// </summary>
        public static void BuildWindows()
        {
            int code = 1;
            try
            {
                string outDir = FullPath(Arg("-out", "Builds/Windows"));
                Directory.CreateDirectory(outDir);
                string exe = Path.Combine(outDir, "HaenginMainEvent.exe");

                // 빌드 목록(EditorBuildSettings)의 켜진 장면을 순서대로. 비어 있으면 Sandbox 하나(예전 동작)
                var scenes = EditorBuildSettings.scenes.Where(sc => sc.enabled && File.Exists(FullPath(sc.path))).Select(sc => sc.path).ToArray();
                if (scenes.Length == 0) scenes = new[] { SandboxScene };
                foreach (var sc in scenes)
                    if (!File.Exists(FullPath(sc))) throw new FileNotFoundException("장면이 없습니다: " + sc);
                Debug.Log($"{Tag} 빌드 장면: {string.Join(", ", scenes)}");
                if (!TempPathGuard.Ensure(true))
                    throw new Exception("임시 폴더(TMPDIR)를 쓸 수 없어 빌드를 시작하지 않습니다(Burst AOT 단계가 반드시 실패). " + TempPathGuard.Guide);

                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
                    EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
                EditorUserBuildSettings.development = false;

                var opts = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = exe,
                    target = BuildTarget.StandaloneWindows64,
                    targetGroup = BuildTargetGroup.Standalone,
                    options = BuildOptions.None,
                };
                // com.unity.collections → test-framework.performance 가 빌드 때 Assets/Resources 에 json 을 만들었다 지우고 빈 폴더만 남긴다
                bool hadResources = AssetDatabase.IsValidFolder("Assets/Resources");
                Debug.Log($"{Tag} 빌드 시작 → {exe}");
                BuildReport report = BuildPipeline.BuildPlayer(opts);
                if (!hadResources && AssetDatabase.IsValidFolder("Assets/Resources") &&
                    !Directory.EnumerateFileSystemEntries(FullPath("Assets/Resources")).Any())
                {
                    AssetDatabase.DeleteAsset("Assets/Resources");
                    Debug.Log($"{Tag} 빌드가 남긴 빈 Assets/Resources 폴더 정리");
                }
                var s = report.summary;
                Debug.Log($"{Tag} 빌드 결과: {s.result} | 출력={s.outputPath} | 크기={s.totalSize / (1024f * 1024f):F1}MB | " +
                          $"시간={s.totalTime.TotalSeconds:F0}초 | 에러={s.totalErrors} | 경고={s.totalWarnings} | 플랫폼={s.platform} | " +
                          $"백엔드={PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone)}");
                if (s.result != BuildResult.Succeeded)
                {
                    foreach (var step in report.steps)
                    foreach (var m in step.messages)
                        if (m.type == LogType.Error || m.type == LogType.Exception)
                            Debug.LogError($"{Tag} 빌드 에러 [{step.name}] {m.content}");
                }
                code = s.result == BuildResult.Succeeded ? 0 : 1;
            }
            catch (Exception e)
            {
                Debug.LogError($"{Tag} 빌드 실패: {e}");
                code = 1;
            }
            EditorApplication.Exit(code);
        }

        // ───────────────────────── 3) 임포트·컴파일 점검
        static readonly Regex CompileErr = new Regex(@"error CS\d{4}", RegexOptions.Compiled);
        static readonly Regex ImportErr = new Regex(
            @"(Failed to import|Error while importing|Could not create asset from|Import of asset .* failed|ScriptedImporter.*(error|exception)|Shader error in|Errors? during import|failed to import)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// 컴파일 에러·임포트 에러 수를 세서 로그에 출력. [-reimport 경로] 로 특정 에셋을 강제 재임포트해 볼 수 있다.
        /// 에러가 하나라도 있으면 exit code 1.
        /// </summary>
        public static void ImportCheck()
        {
            int code = 0;
            try
            {
                string reimport = Arg("-reimport");
                if (!string.IsNullOrEmpty(reimport))
                {
                    Debug.Log($"{Tag} 강제 재임포트: {reimport}");
                    AssetDatabase.ImportAsset(reimport, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ImportRecursive);
                }
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                bool compileFailed = EditorUtility.scriptCompilationFailed;
                var compileLines = new HashSet<string>();
                var importLines = new List<string>();
                string log = Application.consoleLogPath;
                if (!string.IsNullOrEmpty(log) && File.Exists(log))
                {
                    using var fs = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    using var sr = new StreamReader(fs);
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (line.Contains(Tag)) continue; // 자기 출력 제외
                        if (CompileErr.IsMatch(line)) compileLines.Add(line.Trim());
                        else if (ImportErr.IsMatch(line)) importLines.Add(line.Trim());
                    }
                }

                GetConsoleCounts(out int conErr, out int conWarn, out int conLog);

                Debug.Log($"{Tag} 임포트 점검 결과 | 컴파일 실패 플래그={compileFailed} | 컴파일 에러(CS) {compileLines.Count}건 | " +
                          $"임포트 에러 {importLines.Count}건 | 콘솔 에러 {conErr} / 경고 {conWarn} / 로그 {conLog}");
                foreach (var l in compileLines.Take(30)) Debug.Log($"{Tag}   컴파일: {l}");
                foreach (var l in importLines.Take(30)) Debug.Log($"{Tag}   임포트: {l}");

                if (compileFailed || compileLines.Count > 0 || importLines.Count > 0) code = 1;
            }
            catch (Exception e)
            {
                Debug.LogError($"{Tag} 점검 실패: {e}");
                code = 1;
            }
            EditorApplication.Exit(code);
        }

        static void GetConsoleCounts(out int err, out int warn, out int log)
        {
            err = warn = log = -1;
            try
            {
                var t = typeof(EditorApplication).Assembly.GetType("UnityEditor.LogEntries");
                var m = t?.GetMethod("GetCountsByType", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (m == null) return;
                object[] a = { 0, 0, 0 };
                m.Invoke(null, a);
                err = (int)a[0]; warn = (int)a[1]; log = (int)a[2];
            }
            catch { /* 콘솔 집계는 참고용 */ }
        }
    }
}
