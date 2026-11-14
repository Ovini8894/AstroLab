using UnityEditor;
using UnityEngine;

// Regras de importação automáticas para os arquivos da Base Lunar (vindos do Blender).
public class BaseLunarImport : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.Contains("BaseLunar/Texturas")) return;
        var ti = (TextureImporter)assetImporter;
        if (assetPath.EndsWith("_Normal.png")) ti.textureType = TextureImporterType.NormalMap;
        ti.wrapMode = TextureWrapMode.Repeat;
        ti.anisoLevel = 4;
        ti.maxTextureSize = assetPath.Contains("Ceu_") ? 4096 : 1024;
    }

    void OnPreprocessModel()
    {
        if (!assetPath.EndsWith("BaseLunar.fbx")) return;
        var mi = (ModelImporter)assetImporter;
        mi.globalScale = 1f;
        mi.indexFormat = ModelImporterIndexFormat.UInt32;      // terreno tem > 65k vértices
        mi.meshCompression = ModelImporterMeshCompression.Off;
        mi.importNormals = ModelImporterNormals.Import;        // mantém as bordas chanfradas do Blender
        mi.importTangents = ModelImporterTangents.CalculateMikk;
        mi.importCameras = false;
        mi.importLights = false;
        mi.importAnimation = false;
        mi.animationType = ModelImporterAnimationType.None;
        mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
    }
}
