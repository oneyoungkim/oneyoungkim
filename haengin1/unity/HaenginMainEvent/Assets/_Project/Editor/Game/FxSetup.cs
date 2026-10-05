// 행인1의 메인이벤트 — 전투 이펙트·효과음·HUD 그림 묶기 (docs/08_M2_전투_설계.md 5-5·5-7·7장, 11장 11·12단계)
// Art/Fx/*.png(tools/ink_fx_gen.py) · Audio/SFX/*.wav(tools/sfx_gen.py) 가져오기 설정 → 재질(Haengin/FxSprite 보통·맨 위) →
// 쇼크 컷: 재질(Haengin/ShockCut) + URP Full Screen Pass Renderer Feature 를 PC·Mobile 렌더러에(없을 때만) → Settings/FxKit.asset.
// 그림·소리를 새로 만들려면: python tools/ink_fx_gen.py <Art/Fx> <Fonts/BlackHanSans-Regular.ttf> · python tools/sfx_gen.py <Audio/SFX>
using System.Linq;
using Haengin.EditorTools;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Haengin.EditorGame
{
    public static class FxSetup
    {
        const string Tag = "[FxSetup]";
        public const string Root = "Assets/_Project";
        public const string FxDir = Root + "/Art/Fx";
        public const string SfxDir = Root + "/Audio/SFX";
        public const string MatDir = Root + "/Materials/Fx";
        public const string KitPath = Root + "/Settings/FxKit.asset";
        static readonly string[] Renderers = { "Assets/Settings/PC_Renderer.asset", "Assets/Settings/Mobile_Renderer.asset" };

        public static FxKit Ensure()
        {
            if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder(Root + "/Materials", "Fx");
            var kit = AssetDatabase.LoadAssetAtPath<FxKit>(KitPath);
            if (kit == null) { kit = ScriptableObject.CreateInstance<FxKit>(); AssetDatabase.CreateAsset(kit, KitPath); }

            Texture2D Tex(string n)
            {
                string p = $"{FxDir}/{n}.png";
                var ti = AssetImporter.GetAtPath(p) as TextureImporter;
                if (ti == null) { Debug.LogWarning($"{Tag} 그림 없음: {p}"); return null; }
                bool dirty = ti.textureType != TextureImporterType.Default || !ti.alphaIsTransparency || ti.wrapMode != TextureWrapMode.Clamp
                             || ti.textureCompression != TextureImporterCompression.Uncompressed || !ti.mipmapEnabled || ti.npotScale != TextureImporterNPOTScale.None;
                if (dirty)
                {
                    ti.textureType = TextureImporterType.Default;
                    ti.alphaIsTransparency = true;
                    ti.alphaSource = TextureImporterAlphaSource.FromInput;
                    ti.sRGBTexture = true;
                    ti.wrapMode = TextureWrapMode.Clamp;
                    ti.mipmapEnabled = true;
                    ti.npotScale = TextureImporterNPOTScale.None;
                    ti.textureCompression = TextureImporterCompression.Uncompressed;    // 먹 가장자리가 뭉개지지 않게(작은 그림 17장)
                    ti.SaveAndReimport();
                }
                return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
            }
            AudioClip Snd(string n)
            {
                string p = $"{SfxDir}/{n}.wav";
                var ai = AssetImporter.GetAtPath(p) as AudioImporter;
                if (ai == null) { Debug.LogWarning($"{Tag} 소리 없음: {p}"); return null; }
                var st = ai.defaultSampleSettings;
                if (st.loadType != AudioClipLoadType.DecompressOnLoad || st.compressionFormat != AudioCompressionFormat.PCM || !ai.forceToMono)
                {
                    st.loadType = AudioClipLoadType.DecompressOnLoad;
                    st.compressionFormat = AudioCompressionFormat.PCM;
                    ai.defaultSampleSettings = st;
                    ai.forceToMono = true;
                    ai.SaveAndReimport();
                }
                return AssetDatabase.LoadAssetAtPath<AudioClip>(p);
            }

            kit.Brush = Tex("fx_brush"); kit.Splat = Tex("fx_splat"); kit.Shard = Tex("fx_shard"); kit.Star = Tex("fx_star");
            kit.Lines = Tex("fx_lines"); kit.Ring = Tex("fx_ring"); kit.Dust = Tex("fx_dust");
            kit.WordPok = Tex("word_pok"); kit.WordPpak = Tex("word_ppak"); kit.WordKwajik = Tex("word_kwajik"); kit.WordKung = Tex("word_kung");
            kit.WordTuk = Tex("word_tuk"); kit.WordRead = Tex("word_read"); kit.WordHeat = Tex("word_heat");
            kit.Edge = Tex("ui_edge"); kit.Tri = Tex("ui_tri"); kit.Arc = Tex("ui_arc");
            kit.Font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFont.AssetPath);
            kit.Normal = Mat("M_FxSprite", "Haengin/FxSprite", 4f);
            kit.Top = Mat("M_FxSpriteTop", "Haengin/FxSprite", 8f);
            kit.Hit = new[] { null, Snd("hit_p1"), Snd("hit_p2"), Snd("hit_p3"), Snd("hit_p4") };
            kit.Whoosh = Snd("whoosh"); kit.Slam = Snd("slam"); kit.Heat = Snd("heat"); kit.Block = Snd("block"); kit.Dodge = Snd("dodge");
            EditorUtility.SetDirty(kit);

            var shock = Mat("M_ShockCut", "Haengin/ShockCut", -1f);
            foreach (var rp in Renderers) AddShockFeature(rp, shock);
            AssetDatabase.SaveAssets();
            int tex = new Object[] { kit.Brush, kit.Splat, kit.Shard, kit.Star, kit.Lines, kit.Ring, kit.Dust, kit.WordPok, kit.WordPpak, kit.WordKwajik, kit.WordKung, kit.WordTuk, kit.WordRead, kit.WordHeat, kit.Edge, kit.Tri, kit.Arc }.Count(o => o != null);
            int snd = kit.Hit.Count(c => c != null) + new Object[] { kit.Whoosh, kit.Slam, kit.Heat, kit.Block, kit.Dodge }.Count(o => o != null);
            Debug.Log($"{Tag} 묶음 {KitPath}: 그림 {tex}/17 · 소리 {snd}/9 · 재질 {(kit.Normal != null ? "보통" : "-")}·{(kit.Top != null ? "맨 위" : "-")} · 글꼴 {(kit.Font != null ? kit.Font.name : "-")} · 쇼크 컷 재질 {(shock != null ? "있음" : "없음")}");
            return kit;
        }

        static Material Mat(string name, string shader, float ztest)
        {
            string p = $"{MatDir}/{name}.mat";
            var sh = Shader.Find(shader);
            if (sh == null) { Debug.LogWarning($"{Tag} 셰이더 없음: {shader}"); return null; }
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m == null) { m = new Material(sh) { name = name }; AssetDatabase.CreateAsset(m, p); }
            if (m.shader != sh) m.shader = sh;
            if (ztest >= 0f && m.HasProperty("_ZTest")) m.SetFloat("_ZTest", ztest);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// 렌더러에 쇼크 컷 전체 화면 패스(후처리 뒤)를 한 번만 붙인다. 켜고 끄기는 전역 _HaenginShock(실행 중 에셋을 바꾸지 않음)
        static void AddShockFeature(string path, Material mat)
        {
            var data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(path);
            if (data == null || mat == null) return;
            if (data.rendererFeatures.Any(f => f is FullScreenPassRendererFeature fs && fs.name == "ShockCut"))
            {
                var have = (FullScreenPassRendererFeature)data.rendererFeatures.First(f => f is FullScreenPassRendererFeature fs && fs.name == "ShockCut");
                if (have.passMaterial != mat) { have.passMaterial = mat; EditorUtility.SetDirty(have); }
                return;
            }
            var feat = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
            feat.name = "ShockCut";
            feat.passMaterial = mat;
            feat.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;
            feat.fetchColorBuffer = true;
            feat.requirements = ScriptableRenderPassInput.Color;
            feat.passIndex = 0;
            AssetDatabase.AddObjectToAsset(feat, data);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feat, out _, out long localId);
            var so = new SerializedObject(data);
            var list = so.FindProperty("m_RendererFeatures");
            var map = so.FindProperty("m_RendererFeatureMap");
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feat;
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            so.ApplyModifiedPropertiesWithoutUndo();
            data.SetDirty();
            EditorUtility.SetDirty(data);
            Debug.Log($"{Tag} 쇼크 컷 전체 화면 패스 붙임: {path} (기능 {list.arraySize}개)");
        }
    }
}
