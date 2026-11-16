using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AstroLab.PousoLunar.Editor
{
    // Resolve o objeto principal pelo importador real, sem adivinhar IDs de subassets do FBX.
    [InitializeOnLoad]
    public sealed class LunarLandingModelResolver : IProcessSceneWithReport
    {
        public const string ModelPath = "Assets/CentroTreinamento/Modelos/OnibusEspacial.fbx";
        public const string ModelIdentityPath = "Assets/PousoLunar/Documentacao/identidade-modelo-unity.json";
        const string LandingPrefabPath = "Assets/PousoLunar/Prefabs/RocketLandingRoot.prefab";
        public int callbackOrder => 0;
        static LunarLandingModelResolver()
        {
            EditorApplication.delayCall += CaptureModelIdentity;
            EditorApplication.delayCall += ResolveLoaded;
            UnityEditor.SceneManagement.EditorSceneManager.sceneOpened += (_, __) => ResolveLoaded();
        }

        [Serializable]
        sealed class ModelIdentity
        {
            // String evita arredondar IDs de 64 bits quando o relatório é lido por JavaScript.
            public string assetPath, name, type, guid, unityVersion, localId;
        }

        [MenuItem("AstroLab/Pouso Lunar/Inspecionar referência importada do modelo")]
        public static void CaptureModelIdentity()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (!model || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(model, out string guid, out long localId)) return;
            var identity = new ModelIdentity { assetPath=ModelPath, name=model.name, type=model.GetType().FullName,
                guid=guid, localId=localId.ToString(System.Globalization.CultureInfo.InvariantCulture), unityVersion=Application.unityVersion };
            string json = JsonUtility.ToJson(identity, true);
            if (!File.Exists(ModelIdentityPath) || File.ReadAllText(ModelIdentityPath) != json)
                File.WriteAllText(ModelIdentityPath, json);
        }

        // Persiste o GameObject retornado pelo importador, nunca o objeto Prefab de ID 100100000.
        // A cena aberta só é salva por solicitação explícita e se não havia alterações pendentes.
        [MenuItem("AstroLab/Pouso Lunar/Reparar referências serializadas do modelo")]
        public static void RepairOwnAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (!model) throw new InvalidOperationException("Modelo FBX ainda não importado: " + ModelPath);
            CaptureModelIdentity();
            var prefab = PrefabUtility.LoadPrefabContents(LandingPrefabPath);
            try
            {
                var spacecraft = prefab.GetComponent<LunarLandingSpacecraft>();
                if (!spacecraft) throw new InvalidOperationException("Prefab de pouso sem montador de nave.");
                spacecraft.ClearGenerated();
                spacecraft.sourceModel = model;
                PrefabUtility.SaveAsPrefabAsset(prefab, LandingPrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            var scene = SceneManager.GetSceneByPath(LunarLandingTransition.LandingScene);
            if (scene.IsValid() && scene.isLoaded)
            {
                if (scene.isDirty)
                    Debug.LogWarning("Prefab reparado. A cena de pouso possui alterações pendentes e não foi salva; a referência viva será resolvida sem descartar trabalho.");
                bool save = !scene.isDirty;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var spacecraft in root.GetComponentsInChildren<LunarLandingSpacecraft>(true))
                    {
                        spacecraft.ClearGenerated(); spacecraft.sourceModel = model;
                        if (save) EditorUtility.SetDirty(spacecraft);
                    }
                if (save)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene,LunarLandingTransition.LandingScene))
                        throw new IOException("Não foi possível salvar a referência da cena de pouso.");
                }
                ResolveLoaded();
            }
            Debug.Log("LANDING_MODEL_REFERENCE_REPAIRED: GameObject importado atribuído ao prefab e às instâncias de pouso elegíveis.");
        }
        static void ResolveLoaded()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            foreach (var spacecraft in Object.FindObjectsByType<LunarLandingSpacecraft>(FindObjectsInactive.Include))
                Resolve(spacecraft, true);
        }
        public static void Resolve(LunarLandingSpacecraft spacecraft, bool preview)
        {
            if (!spacecraft || !spacecraft.gameObject.scene.path.StartsWith("Assets/PousoLunar/")) return;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (!model) { Debug.LogError("Pouso: aguarde a importação de " + ModelPath); return; }
            if (spacecraft.sourceModel != model) { spacecraft.ClearGenerated(); spacecraft.sourceModel = model; }
            if (preview) spacecraft.EnsureBuilt();
        }
        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (!scene.path.StartsWith("Assets/PousoLunar/")) return;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var spacecraft in root.GetComponentsInChildren<LunarLandingSpacecraft>(true)) Resolve(spacecraft, false);
        }
    }

    [CustomEditor(typeof(LunarLandingSpacecraft))]
    public sealed class LunarLandingSpacecraftInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var spacecraft=(LunarLandingSpacecraft)target;
            EditorGUILayout.HelpBox("O Shuttle original NÃO possui apoios de pouso lunar. O contato inferior é provisório; consulte README.md.",MessageType.Warning);
            if (GUILayout.Button("Remontar nave e aplicar sockets"))
            { LunarLandingModelResolver.Resolve(spacecraft,false); spacecraft.Rebuild(); }
            if (GUILayout.Button("Testar bocal RCS selecionado") && spacecraft.rcs)
                spacecraft.rcs.TestThruster(spacecraft.rcs.manualThrusterIndex,spacecraft.rcs.manualIntensity);
            if (GUILayout.Button("Testar motor principal") && spacecraft.mainEngine) spacecraft.mainEngine.TestEngine();
            if (GUILayout.Button("Testar poeira") && spacecraft.lunarDust) spacecraft.lunarDust.TestDust();
            if (GUILayout.Button("Resetar todos os efeitos") && spacecraft.controller && spacecraft.controller.effects)
                spacecraft.controller.effects.ResetEffects();
            if (GUILayout.Button("Executar teste completo em Play isolado")) LunarLandingFinalValidation.Run();
        }
    }
}
