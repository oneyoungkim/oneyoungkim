// 행인1의 메인이벤트 — 룩데브 Unity 적용(docs/14_룩데브_계획.md 4장 · 9장 '구현 결과', 2026-10-26)
//   batchmode: -executeMethod Haengin.EditorGame.LookdevSetup.Build     LUT·아틀라스 들여오기 → URP HDR 등급 → 맵 에셋(시간대 프리셋 = 그늘 회갈·먹선·안개·얼룩·웅덩이)
//                                                                       → 자전거(치수 고친 모델·페달 클립 4종) → 무대 키트(디테일) → 플레이어 프리팹 → 무대 다시 만들기 → Zone1 다시 만들기 → 체육관 장면
//              -executeMethod Haengin.EditorGame.LookdevSetup.LutOnly   LUT 띠만 다시 들여오기(같은 경로·GUID — 재질·장면 참조 그대로)
//   LUT: concept/art/env/lut/LUT_<묶음>_URP32.png(tools/lookdev/eval_unity.py --fit-lut) → Art/Env/Lut/ (sRGB 끔·밉 끔·Clamp·Bilinear·압축 없음·읽기 켬 — 시계 연속 섞기용)
//   URP 등급 모드 = HDR: LDR 모드는 사용자 LUT 를 내부 등급(채도·스플릿 톤·Lift/Gamma/Gain) '앞'에 적용한다(Common.hlsl ApplyColorGrading — 14 문서 4-1 의 가정과 반대).
//     HDR 모드는 내부 등급 뒤 sRGB 값에 적용 → 오프라인 LUT 맞춤(Unity 사진 → 프레임)과 같은 순서.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Haengin.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Haengin.EditorGame
{
    public static class LookdevSetup
    {
        const string Tag = "[Lookdev]";
        public const string Root = "Assets/_Project";
        public const string LutDir = Root + "/Art/Env/Lut";
        public const string AtlasPath = EnvSetup.TexDir + "/detail14_atlas.png";
        static readonly string[] Pipelines = { "Assets/Settings/PC_RPAsset.asset", "Assets/Settings/Mobile_RPAsset.asset" };
        /// 잎 덩어리 먹선 색(#1A1417 → 따뜻한 짙은 갈색 #3A322A — 프레임 선 색 쪽)
        public static readonly Color LeafInk = new Color(0.227f, 0.196f, 0.165f);
        /// 잎 카드 그늘 곱셈(.80, .80, .84 푸른 → 따뜻하게 — 낮 그늘이 a* −4~−6 초록으로 재던 것)
        /// QA 2차 룩데브 소결정 ①(2026-10-26, 14 문서 4-9 '재질별 팔레트 고정'): 0.86·0.81·0.77 → 조금 밝고 따뜻하게(프레임 수관 그늘 L* 41~47 · a* +3~6 — Unity 는 L* 33~40 · a* −1~−5)
        public static readonly Color LeafShade = new Color(0.93f, 0.86f, 0.80f);
        /// 잎 카드 밝힘(바탕색 곱 — 초록을 조금 덜고 밝게). 수관·관목·잡초 카드 모두
        public static readonly Color LeafLift = new Color(1.14f, 1.08f, 1.0f);
        public static readonly string[] Groups = { "Z1_Dawn", "Z1_Day", "Z1_Dusk", "Z1_Night", "Gukbap", "Home", "Conv", "School", "Gym" };
        static readonly List<string> Notes = new List<string>();
        static string SrcLut => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../concept/art/env/lut"));
        static string SrcAtlas => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../concept/art/env/decals/detail14_atlas.png"));
        public static string LutPath(string group) => $"{LutDir}/LUT_{group}_URP32.png";
        public static Texture2D Lut(string group) => AssetDatabase.LoadAssetAtPath<Texture2D>(LutPath(group));

        public static void Build() => Run(() =>
        {
            EnvSetup.Notes.Clear();
            Luts();
            Atlas();
            Pipeline();
            EnvSetup.Assets();              // 재질·소품·외곽선·그림자·시간대 프리셋(LUT 연결)
            BikeSetup.Assets();             // 자전거 모델(안장 +12cm·크랭크 0.16m)·페달 클립 4종·애니메이터
            StageKitSetup.Assets();         // 무대 키트(Detail14 부분·벽 그림)
            M1Setup.BuildPlayerPrefab();
            StorySetup.Stages();            // 무대 다시 만들기(StageKitSetup.Place 가 장소 LUT 를 StageLights 에)
            Zone1Builder.Rebuild();         // AttachKit → … → Pass8(햇빛 얼룩·디테일)
            GymSetup.SceneForLookdev();     // 체육관 장면(GymLights 에 LUT)
            M1Setup.SetBuildScenes();
        });

        public static void LutOnly() => Run(() => { Luts(); });

        static void Run(Action body)
        {
            int code = 0;
            Notes.Clear();
            var t0 = DateTime.Now;
            try { body(); AssetDatabase.SaveAssets(); }
            catch (Exception e) { Debug.LogError($"{Tag} 실패: {e}"); code = 1; }
            foreach (var n in Notes) Debug.Log($"{Tag}   {n}");
            foreach (var n in EnvSetup.Notes) Debug.Log($"{Tag}   [Env] {n}");
            Debug.Log($"{Tag} 끝: {(code == 0 ? "성공" : "실패")} · {(DateTime.Now - t0).TotalSeconds:F1}초");
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        /// LUT 띠 복사·들여오기(없는 묶음은 항등 띠 — 맞추기 전 자리)
        static void Luts()
        {
            EnvSetup.EnsureFolderPublic(LutDir);
            string proj = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            int copied = 0, ident = 0;
            foreach (var g in Groups)
            {
                string src = Path.Combine(SrcLut, $"LUT_{g}_URP32.png"), dst = Path.Combine(proj, LutPath(g));
                if (File.Exists(src)) { File.Copy(src, dst, true); copied++; }
                else if (!File.Exists(dst)) { File.WriteAllBytes(dst, Identity().EncodeToPNG()); ident++; }
            }
            AssetDatabase.Refresh();
            foreach (var g in Groups)
            {
                var ti = AssetImporter.GetAtPath(LutPath(g)) as TextureImporter;
                if (ti == null) continue;
                ti.textureType = TextureImporterType.Default; ti.sRGBTexture = false; ti.mipmapEnabled = false; ti.wrapMode = TextureWrapMode.Clamp;
                ti.filterMode = FilterMode.Bilinear; ti.textureCompression = TextureImporterCompression.Uncompressed; ti.npotScale = TextureImporterNPOTScale.None;
                ti.isReadable = true; ti.alphaSource = TextureImporterAlphaSource.None; ti.maxTextureSize = 2048;
                ti.SaveAndReimport();
            }
            Notes.Add($"LUT 띠 {Groups.Length}(복사 {copied} · 항등 자리 {ident}) → {LutDir}");
        }

        static Texture2D Identity()
        {
            var t = new Texture2D(1024, 32, TextureFormat.RGBA32, false, true);
            var px = new Color32[1024 * 32];
            for (int y = 0; y < 32; y++)
                for (int b = 0; b < 32; b++)
                    for (int r = 0; r < 32; r++)
                        px[y * 1024 + b * 32 + r] = new Color32((byte)(r * 255 / 31), (byte)(y * 255 / 31), (byte)(b * 255 / 31), 255);
            t.SetPixels32(px); t.Apply();
            return t;
        }

        static void Atlas()
        {
            if (!File.Exists(SrcAtlas)) { Notes.Add("detail14_atlas.png 없음(tools/lookdev/detail_tex.py)"); return; }
            string proj = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            File.Copy(SrcAtlas, Path.Combine(proj, AtlasPath), true);
            var json = Path.ChangeExtension(SrcAtlas, ".json");
            if (File.Exists(json)) File.Copy(json, Path.Combine(proj, Path.ChangeExtension(AtlasPath, ".json")), true);
            AssetDatabase.Refresh();
            var ti = AssetImporter.GetAtPath(AtlasPath) as TextureImporter;
            if (ti != null)
            {
                ti.textureType = TextureImporterType.Default; ti.sRGBTexture = true; ti.alphaIsTransparency = true; ti.alphaSource = TextureImporterAlphaSource.FromInput;
                ti.mipmapEnabled = true; ti.wrapMode = TextureWrapMode.Clamp; ti.textureCompression = TextureImporterCompression.CompressedHQ; ti.maxTextureSize = 2048;
                ti.SaveAndReimport();
            }
            Notes.Add("거리 디테일 아틀라스 → " + AtlasPath);
        }

        /// URP 등급 모드 HDR(사용자 LUT 가 내부 등급 뒤) · LUT 크기 32
        static void Pipeline()
        {
            foreach (var p in Pipelines)
            {
                var a = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(p);
                if (a == null) continue;
                var so = new SerializedObject(a);
                var mode = so.FindProperty("m_ColorGradingMode"); var size = so.FindProperty("m_ColorGradingLutSize");
                bool ch = false;
                if (mode != null && mode.intValue != (int)ColorGradingMode.HighDynamicRange) { mode.intValue = (int)ColorGradingMode.HighDynamicRange; ch = true; }
                if (size != null && size.intValue != 32) { size.intValue = 32; ch = true; }
                if (ch) { so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(a); Notes.Add($"색 등급 HDR · LUT 32: {p}"); }
            }
        }
    }
}
