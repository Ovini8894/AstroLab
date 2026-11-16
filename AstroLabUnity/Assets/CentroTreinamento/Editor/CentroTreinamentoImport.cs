using UnityEditor;
using UnityEngine;

namespace AstroLab.Centro.Editor
{
    // Regras de importação dos arquivos do Centro de Treinamento (vindos do Blender~).
    public class CentroTreinamentoImport : AssetPostprocessor
    {
        const string Raiz = "Assets/CentroTreinamento/";

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Raiz + "Modelos/")) return;
            var mi = (ModelImporter)assetImporter;
            mi.globalScale = 1f;
            mi.indexFormat = ModelImporterIndexFormat.Auto;
            mi.meshCompression = ModelImporterMeshCompression.Off;
            mi.importNormals = ModelImporterNormals.Import;            // bordas duras/suaves vêm do Blender
            mi.importTangents = ModelImporterTangents.None;            // o shader Toon não usa normal map
            mi.importCameras = false;
            mi.importLights = false;
            mi.importAnimation = false;
            mi.importBlendShapes = false;
            mi.animationType = ModelImporterAnimationType.None;
            mi.isReadable = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            mi.generateSecondaryUV = false;
            if (assetPath.EndsWith("/NPC_Lima.fbx"))                     // instrutora MPFB: esqueleto Generic (animações feitas no próprio rig), shape keys
            {
                mi.importAnimation = true;
                mi.importBlendShapes = true;
                mi.animationType = ModelImporterAnimationType.Generic;
                mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            }
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Raiz + "Texturas/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.anisoLevel = 4;
            if (assetPath.Contains("T_Logo_"))
            {
                ti.alphaIsTransparency = true;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.maxTextureSize = 1024;
            }
            else if (assetPath.Contains("Ceu_"))
            {
                ti.wrapModeU = TextureWrapMode.Repeat; ti.wrapModeV = TextureWrapMode.Clamp;
                ti.mipmapEnabled = false;
                ti.maxTextureSize = 4096;
            }
            else
            {
                ti.wrapMode = TextureWrapMode.Repeat;
                ti.maxTextureSize = 1024;
            }
        }
    }
}
