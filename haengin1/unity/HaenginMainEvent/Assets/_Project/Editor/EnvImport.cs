// 행인1의 메인이벤트 — 맵 아트 패스 에셋 들여오기 설정(docs/11_건물생성기.md 7장 체크리스트 1, docs/10 5·8장)
//   Art/Env/Buildings/Z1_*.fbx : Scale 1 · Convert Units 켬 · Bake Axis Conversion 끔 · Read/Write 끔 · 콜라이더 없음 · 카메라·빛 없음 · 메시 압축 끔 ·
//                                법선 Import · Visibility·Constraints 끔 · 재질은 MaterialDescription(이름 M_* — EnvSetup 이 Materials/Env 로 바꿔 꿴다)
//   Art/Env/Textures/M_*.png   : sRGB · 밉맵 · Repeat(아틀라스 2장은 Clamp + 알파) · BC7(CompressedHQ) · 2048
//   Art/Env/Sky/*_layers.png   : 선형(마스크) · Clamp / *_detail.png · cloud_*.png : sRGB · Clamp
using UnityEditor;
using UnityEngine;

namespace Haengin.EditorTools
{
    public sealed class EnvImport : AssetPostprocessor
    {
        public const string Root = "Assets/_Project/Art/Env";

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Root + "/Buildings/") && !assetPath.StartsWith(Root + "/Gym/")) return;
            var imp = (ModelImporter)assetImporter;
            imp.globalScale = 1f;
            imp.useFileUnits = true;
            imp.bakeAxisConversion = false;
            imp.isReadable = false;
            imp.addCollider = false;
            imp.importCameras = false;
            imp.importLights = false;
            imp.meshCompression = ModelImporterMeshCompression.Off;
            imp.importNormals = ModelImporterNormals.Import;
            imp.importTangents = ModelImporterTangents.CalculateMikk;   // UTS 는 노멀 맵이 없어도 탄젠트 공간을 쓴다 — 탄젠트가 없으면(None) normalize(0) 로 그늘 면이 검게 된다(2026-10-24 실측)
            imp.importVisibility = false;
            imp.importConstraints = false;
            imp.importBlendShapes = false;
            imp.importAnimation = false;
            imp.animationType = ModelImporterAnimationType.None;
            imp.generateSecondaryUV = false;
            imp.optimizeMeshPolygons = false;
            imp.optimizeMeshVertices = false;
            imp.weldVertices = false;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            imp.materialLocation = ModelImporterMaterialLocation.InPrefab;
            imp.materialName = ModelImporterMaterialName.BasedOnMaterialName;
            imp.materialSearch = ModelImporterMaterialSearch.Local;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root + "/")) return;
            var imp = (TextureImporter)assetImporter;
            string name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            bool tex = assetPath.StartsWith(Root + "/Textures/") || assetPath.StartsWith(Root + "/Props/Tex/") || assetPath.StartsWith(Root + "/Gym/Textures/") || assetPath.StartsWith(Root + "/Gym/Props/Tex/");
            bool sky = assetPath.StartsWith(Root + "/Sky/");
            imp.textureType = TextureImporterType.Default;
            imp.mipmapEnabled = true;
            imp.filterMode = FilterMode.Trilinear;
            imp.anisoLevel = 4;
            imp.maxTextureSize = 2048;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.isReadable = false;
            if (tex)
            {
                bool atlas = name.EndsWith("_Atlas") || name == "M_Cage_Mesh" || name == "M_Glass_Clear";   // 체육관: 철망 알파 컷아웃 · 유리 알파(둘은 Repeat)
                imp.sRGBTexture = true;
                imp.wrapMode = name.EndsWith("_Atlas") ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                imp.alphaIsTransparency = atlas;
                imp.alphaSource = atlas ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            }
            else if (sky)
            {
                bool layers = name.EndsWith("_layers");
                imp.sRGBTexture = !layers;        // 층 마스크는 선형
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.alphaIsTransparency = !name.EndsWith("_detail");
                imp.alphaSource = name.EndsWith("_detail") ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
                imp.textureCompression = TextureImporterCompression.CompressedHQ;   // 층 마스크도 BC7(빌드 10.7MB → 2.7MB 장당, 경계 품질 충분)
            }
        }
    }
}
