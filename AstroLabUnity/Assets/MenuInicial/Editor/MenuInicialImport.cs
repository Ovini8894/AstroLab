using UnityEditor;
using UnityEngine;

namespace AstroLab.Menu.EditorTools
{
    /// <summary>Regras de importação dos arquivos de Assets/MenuInicial (modelo do Blender, texturas e sprites de UI).</summary>
    public sealed class MenuInicialImport : AssetPostprocessor
    {
        const string Raiz = "Assets/MenuInicial/";

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Raiz + "Modelos/")) return;
            var mi = (ModelImporter)assetImporter;
            mi.globalScale = 1f;
            mi.indexFormat = ModelImporterIndexFormat.UInt32;
            mi.meshCompression = ModelImporterMeshCompression.Off;
            mi.importNormals = ModelImporterNormals.Import;
            mi.importTangents = ModelImporterTangents.None;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importAnimation = false;
            mi.animationType = ModelImporterAnimationType.None;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;   // materiais são do menu (Toon)
            mi.addCollider = false;
            mi.isReadable = true;                                           // a bandeira é animada na CPU
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Raiz)) return;
            var ti = (TextureImporter)assetImporter;
            string nome = System.IO.Path.GetFileNameWithoutExtension(assetPath);

            if (assetPath.StartsWith(Raiz + "UI/"))
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.filterMode = FilterMode.Bilinear;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.spriteBorder = nome switch
                {
                    "UI_Arredondado" => new Vector4(20, 20, 20, 20),
                    "UI_Borda" => new Vector4(20, 20, 20, 20),
                    "UI_Brilho" => new Vector4(56, 56, 56, 56),
                    _ => Vector4.zero,
                };
                return;
            }
            if (assetPath.StartsWith(Raiz + "Texturas/"))
            {
                ti.textureType = TextureImporterType.Default;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = true;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.anisoLevel = 4;
                ti.maxTextureSize = nome.StartsWith("FX_") ? 256 : nome.StartsWith("T_Menu_Terra") ? 2048 : 1024;
                if (nome.StartsWith("T_Menu_Terra")) ti.wrapModeU = TextureWrapMode.Repeat;   // emenda da longitude
                ti.textureCompression = TextureImporterCompression.Compressed;
            }
        }
    }
}
