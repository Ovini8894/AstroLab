#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AstroLab.Gameplay.Editor
{
    public static class FinalizeLunarLook
    {
        public static string Apply()
        {
            if(Application.isPlaying||!Object.FindAnyObjectByType<LunarWorld>())throw new System.InvalidOperationException("Abra a cena lunar fora do Play.");
            var toon=Shader.Find("AstroLab/Toon");int count=0;
            foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{"Assets/Gameplay/Materials"}))
            {
                var material=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if(!material.name.StartsWith("Interior_")&&!material.name.StartsWith("Apollo_")&&material.name!="Lua_Rochas")continue;
                Color color=material.HasProperty("_BaseColor")?material.GetColor("_BaseColor"):material.color;
                material.shader=toon;material.SetColor("_BaseColor",color);material.SetFloat("_RimForca",.15f);material.SetFloat("_RimLargura",.25f);
                material.SetColor("_EmissionColor",material.name.Contains("Telemetria")?color*.22f:Color.black);
                EditorUtility.SetDirty(material);count++;
            }
            var terrain=AssetDatabase.LoadAssetAtPath<Material>("Assets/Gameplay/Materials/Lua_Regolito.mat");
            var source=AssetDatabase.LoadAssetAtPath<Material>("Assets/BaseLunar/Materiais/M_Regolito.mat");
            terrain.shader=source.shader;terrain.CopyPropertiesFromMaterial(source);terrain.SetTextureScale("_BaseMap",Vector2.one);EditorUtility.SetDirty(terrain);
            var original=AssetDatabase.LoadAssetAtPath<Material>("Assets/BaseLunar/Materiais/Ceu_Estrelas.mat");
            const string skyPath="Assets/Gameplay/Materials/Lua_CeuCompleto.mat";
            var sky=AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if(!sky){sky=new Material(Shader.Find("AstroLab/LuaSky"));AssetDatabase.CreateAsset(sky,skyPath);}
            sky.SetTexture("_Tex",original.GetTexture("_Tex"));sky.SetColor("_Tint",original.GetColor("_Tint"));sky.SetFloat("_Exposure",original.GetFloat("_Exposure"));sky.SetFloat("_Rotation",original.GetFloat("_Rotation"));
            RenderSettings.skybox=sky;EditorUtility.SetDirty(sky);
            InteriorDetails.Apply();
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
            return count+" materiais alinhados ao cartoon; céu nos dois hemisférios e placas voltadas ao interior.";
        }
    }
}
#endif
