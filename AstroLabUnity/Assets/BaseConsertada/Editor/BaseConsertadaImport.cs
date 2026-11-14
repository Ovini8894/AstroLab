using UnityEditor;

// Regras de importação do BaseConsertada.fbx (iguais às do BaseLunar.fbx).
public class BaseConsertadaImport : AssetPostprocessor
{
    void OnPreprocessModel()
    {
        if (!assetPath.EndsWith("BaseConsertada.fbx")) return;
        var mi = (ModelImporter)assetImporter;
        mi.globalScale = 1f;
        mi.indexFormat = ModelImporterIndexFormat.UInt32;
        mi.meshCompression = ModelImporterMeshCompression.Off;
        mi.importNormals = ModelImporterNormals.Import;
        mi.importTangents = ModelImporterTangents.CalculateMikk;
        mi.importCameras = false;
        mi.importLights = false;
        mi.importAnimation = false;
        mi.animationType = ModelImporterAnimationType.None;
        mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
    }
}
