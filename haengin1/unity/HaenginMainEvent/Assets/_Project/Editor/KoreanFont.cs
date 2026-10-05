// 행인1의 메인이벤트 — 한글 TMP 글꼴 에셋(이름표·HUD 공용)
// 원본 = Assets/_Project/Fonts/NotoSansKR-Bold.ttf (OFL 1.1, 같은 폴더 OFL.txt). TMP 기본 리소스(Assets/TextMesh Pro)는 레포에 들어 있다.
// 글꼴 에셋은 '동적'(쓰는 글자만 실행 중에 아틀라스에 그림) + Clear Dynamic Data On Build = 켬 →
// 저장된 에셋은 늘 비어 있는 상태(1x1 아틀라스)라 파일이 작고, 다시 만들어도·플레이해도 바뀌지 않는다.
using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Haengin.EditorTools
{
    public static class KoreanFont
    {
        public const string Dir = SandboxSetup.Root + "/Fonts";
        public const string Ttf = Dir + "/NotoSansKR-Bold.ttf";
        public const string AssetPath = Dir + "/KR_Bold_SDF.asset";
        public const string LabelMatPath = Dir + "/KR_Label.mat";
        const string Tag = "[KoreanFont]";

        static readonly Color Paper = new Color(0.957f, 0.937f, 0.902f); // #F4EFE6

        /// 글꼴 에셋을 찾거나 만든다. TMP 기본 리소스가 없으면 예외(레포에 Assets/TextMesh Pro 가 있어야 함).
        public static TMP_FontAsset Ensure(List<string> notes)
        {
            if (AssetDatabase.FindAssets("t:TMP_Settings").Length == 0)
                throw new Exception("TMP 기본 리소스(Assets/TextMesh Pro/Resources/TMP Settings.asset)가 없습니다. Window › TextMeshPro › Import TMP Essential Resources 로 넣으세요");

            var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetPath);
            if (fa != null) { EnsureFlags(fa); return fa; }

            var imp = AssetImporter.GetAtPath(Ttf) as TrueTypeFontImporter
                      ?? throw new Exception("한글 글꼴 파일이 없습니다: " + Ttf);
            if (!imp.includeFontData) { imp.includeFontData = true; imp.SaveAndReimport(); }
            var font = AssetDatabase.LoadAssetAtPath<Font>(Ttf) ?? throw new Exception("글꼴을 읽지 못했습니다: " + Ttf);

            // 표본 크기 64 · 여백 6(SDF 8% 남짓) · 2048 아틀라스(약 900자) · 모자라면 아틀라스를 더 만든다
            fa = TMP_FontAsset.CreateFontAsset(font, 64, 6, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true)
                 ?? throw new Exception("TMP 글꼴 에셋을 만들지 못했습니다(Include Font Data 확인)");
            fa.name = "KR_Bold_SDF";
            AssetDatabase.CreateAsset(fa, AssetPath);
            fa.material.name = "KR_Bold_SDF Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            fa.atlasTextures[0].name = "KR_Bold_SDF Atlas";
            AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
            EnsureFlags(fa);
            AssetDatabase.SaveAssets();
            notes?.Add($"한글 글꼴 에셋 새로 만듦: {AssetPath} (동적, 표본 64pt, 2048 아틀라스)");
            Debug.Log($"{Tag} 만듦: {AssetPath}");
            return fa;
        }

        static void EnsureFlags(TMP_FontAsset fa)
        {
            var so = new SerializedObject(fa);
            var p = so.FindProperty("m_ClearDynamicDataOnBuild");
            if (p != null && !p.boolValue) { p.boolValue = true; so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(fa); }
        }

        /// 이름표 재질: 모바일 SDF '오버레이'(깊이 검사 없이 맨 위 — 벽에 반쯤 묻혀 잘리지 않음. 가려진 이름표는 런타임 NameTags 가 숨김)
        /// + 종이색 외곽선 — 먹색 글자가 하늘·벽 어디서나 읽히게. 값은 매번 같게 덮어쓴다.
        public const string LabelShader = "TextMeshPro/Mobile/Distance Field Overlay";
        public static Material LabelMaterial(TMP_FontAsset fa)
        {
            var shader = Shader.Find(LabelShader) ?? fa.material.shader;
            var m = AssetDatabase.LoadAssetAtPath<Material>(LabelMatPath);
            if (m == null)
            {
                m = new Material(fa.material) { name = "KR_Label" };
                AssetDatabase.CreateAsset(m, LabelMatPath);
            }
            m.CopyPropertiesFromMaterial(fa.material);
            if (m.shader != shader) m.shader = shader;
            m.SetTexture(ShaderUtilities.ID_MainTex, fa.atlasTexture);
            m.SetColor(ShaderUtilities.ID_OutlineColor, Paper);
            m.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.28f);
            m.SetFloat(ShaderUtilities.ID_FaceDilate, 0.12f);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// 편집기에서 글자를 그리느라 아틀라스에 들어간 글리프를 비우고 저장(저장 파일이 늘 빈 상태 → diff 없음).
        public static void ResetDynamic(TMP_FontAsset fa)
        {
            if (fa == null) return;
            fa.ClearFontAssetData(true);
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssetIfDirty(fa);
        }
    }

    /// 에디터에서 플레이(또는 PlayMode 테스트)를 끝내면 한글 글꼴 에셋의 동적 아틀라스를 비운다 —
    /// 안 그러면 플레이 중 그린 글자가 KR_Bold_SDF.asset 에 남아 커밋 diff 가 생긴다.
    [InitializeOnLoad]
    static class KoreanFontPlayReset
    {
        static KoreanFontPlayReset()
        {
            EditorApplication.playModeStateChanged -= OnPlayMode;
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        static void OnPlayMode(PlayModeStateChange st)
        {
            if (st != PlayModeStateChange.EnteredEditMode) return;
            var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFont.AssetPath);
            if (fa != null && fa.characterTable != null && fa.characterTable.Count > 0) KoreanFont.ResetDynamic(fa);
        }
    }
}
