using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AstroLab.PousoLunar.Editor
{
    /// <summary>Extrai referências visuais da cena em PreviewScene; o prefab final não possui gameplay.</summary>
    public static class LunarLandingEnvironmentBuilder
    {
        public const string SourceScene = "Assets/BaseLunar/Cenas/Lua.unity";
        public const string SourceModel = "Assets/BaseLunar/Modelos/BaseLunar.fbx";
        public const string PrefabPath = "Assets/PousoLunar/Prefabs/LunarBaseVisualEnvironment.prefab";
        static readonly Vector3 LandingSite = new Vector3(24.2f, -.071624f, 20.3f);

        [MenuItem("AstroLab/Pouso Lunar/Ambiente/Recriar prefab visual da base")]
        public static GameObject BuildPrefab()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Saia do Play Mode antes de extrair o ambiente.");
            Scene previous = SceneManager.GetActiveScene();
            Scene preview = default, construction = default;
            try
            {
                // Cenas Preview não entram no build nem são ativadas como gameplay.
                preview = EditorSceneManager.OpenPreviewScene(SourceScene);
                var terrain = preview.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshFilter>(true))
                    .Single(f => f.name == "Terreno_Lua" && AssetDatabase.GetAssetPath(f.sharedMesh) == SourceModel);
                GameObject sourceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(terrain.gameObject);
                if (!sourceRoot) throw new InvalidOperationException("Terreno da Lua sem instância visual de origem.");

                construction = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(construction);
                var root = new GameObject("LunarBaseVisualEnvironment");
                var marker = root.AddComponent<LunarLandingEnvironment>();
                marker.sourceScenePath = SourceScene; marker.sourceModelPath = SourceModel;
                marker.sourceTerrainMesh = terrain.sharedMesh;
                marker.sourceTerrainPosition = terrain.transform.position;
                marker.sourceTerrainRotation = terrain.transform.rotation;
                marker.sourceTerrainScale = terrain.transform.lossyScale;

                var transformMap = new Dictionary<Transform, Transform>();
                Transform CloneTransform(Transform source)
                {
                    if (transformMap.TryGetValue(source, out var existing)) return existing;
                    var clone = new GameObject(source.name).transform;
                    if (source == sourceRoot.transform)
                    {
                        clone.SetParent(root.transform, false);
                        clone.SetPositionAndRotation(source.position, source.rotation);
                        clone.localScale = source.lossyScale;
                    }
                    else
                    {
                        clone.SetParent(CloneTransform(source.parent), false);
                        clone.localPosition = source.localPosition;
                        clone.localRotation = source.localRotation;
                        clone.localScale = source.localScale;
                    }
                    clone.gameObject.layer = 0;
                    transformMap.Add(source, clone);
                    return clone;
                }

                bool hasBounds = false;
                foreach (var filter in sourceRoot.GetComponentsInChildren<MeshFilter>(true))
                {
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (!filter.sharedMesh || !renderer || !renderer.enabled || !filter.gameObject.activeInHierarchy) continue;
                    // Só referências importadas persistentes: ignora meshes temporários de texto/gameplay.
                    if (AssetDatabase.GetAssetPath(filter.sharedMesh) != SourceModel) continue;
                    if (filter == terrain || filter.sharedMesh == terrain.sharedMesh || IsHelper(filter.name)) continue;
                    if (renderer.sharedMaterials.Any(m => !m || !AssetDatabase.Contains(m)))
                        throw new InvalidOperationException("Material ausente/transitório em " + filter.name + "; snapshot recusado.");
                    bool rock = filter.name.StartsWith("Rocha_", StringComparison.Ordinal);
                    // O fundo usa o terreno já existente; não instancia 2.332 rochas de toda a Lua.
                    if (rock && Vector3.ProjectOnPlane(renderer.bounds.center - LandingSite, Vector3.up).sqrMagnitude > 70 * 70) continue;
                    var target = CloneTransform(filter.transform).gameObject;
                    var mf = target.AddComponent<MeshFilter>(); mf.sharedMesh = filter.sharedMesh;
                    var mr = target.AddComponent<MeshRenderer>();
                    mr.sharedMaterials = renderer.sharedMaterials;
                    mr.shadowCastingMode = renderer.shadowCastingMode;
                    mr.receiveShadows = renderer.receiveShadows;
                    mr.lightProbeUsage = renderer.lightProbeUsage;
                    mr.reflectionProbeUsage = renderer.reflectionProbeUsage;
                    mr.renderingLayerMask = renderer.renderingLayerMask;
                    marker.visualMeshCount++;
                    if (rock) marker.nearbyRockCount++;
                    if (filter.name.StartsWith("Painel_R", StringComparison.Ordinal)) marker.solarPanelCount++;
                    if (!rock)
                    {
                        if (!hasBounds) { marker.structureBounds = renderer.bounds; hasBounds = true; }
                        else marker.structureBounds.Encapsulate(renderer.bounds);
                    }
                }
                if (!marker.ValidateVisualOnly(out string error)) throw new InvalidOperationException(error);
                Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (!prefab) throw new IOException("Não foi possível salvar o prefab visual da base.");
                Debug.Log("LANDING_ENVIRONMENT_BUILT: source=" + SourceScene + "; meshes=" + marker.visualMeshCount
                    + "; panels=" + marker.solarPanelCount + "; nearbyRocks=" + marker.nearbyRockCount
                    + "; terrain=excluded; cameras/listeners/gameplay=none; structures=" + marker.structureBounds.ToString("F3"));
                return prefab;
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (construction.IsValid() && construction.isLoaded) EditorSceneManager.CloseScene(construction, true);
                if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
                // Fechar o Preview desliga o ExecuteAlways da iluminação de origem; restaura o estilo da cena ativa.
                if (previous.IsValid() && previous.isLoaded)
                    foreach (var lighting in previous.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<IluminacaoCartoon>(true)))
                        if (lighting.isActiveAndEnabled) lighting.Aplicar();
            }
        }

        static bool IsHelper(string name) => name.StartsWith("COL_", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("SPAWN_", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("WP_", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("Ceu", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("Estrela", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("Terra_", StringComparison.OrdinalIgnoreCase) && !name.Contains("Vaso");

        public static LunarLandingEnvironment InstallInLandingScene(Scene scene, bool save = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != LunarLandingTransition.LandingScene)
                throw new InvalidOperationException("Instalação permitida somente na cena de pouso em Edit Mode.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (!prefab) prefab = BuildPrefab();
            var existing = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<LunarLandingEnvironment>(true)).ToArray();
            if (existing.Length > 1) throw new InvalidOperationException("Mais de um ambiente visual na cena de pouso.");
            if (existing.Length == 1) return existing[0];
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); instance.transform.localScale = Vector3.one;
            var environment = instance.GetComponent<LunarLandingEnvironment>();
            var terrain = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshFilter>(true))
                .Single(f => f.name == "LandingTerrain_ExistingBlenderMesh");
            if (!environment.MatchesTerrain(terrain))
            {
                Object.DestroyImmediate(instance);
                throw new InvalidOperationException("TRS/malha do terreno divergem da Lua; ambiente não instalado e destino não alterado.");
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (save && !EditorSceneManager.SaveScene(scene, LunarLandingTransition.LandingScene))
                throw new IOException("Não foi possível salvar a cena de pouso.");
            return environment;
        }

        [MenuItem("AstroLab/Pouso Lunar/Ambiente/Instalar base visual na cena de pouso aberta")]
        static void InstallOpened()
        {
            var scene = SceneManager.GetSceneByPath(LunarLandingTransition.LandingScene);
            if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("Abra somente a cena de pouso antes de instalar o ambiente.");
            InstallInLandingScene(scene, false);
        }
    }
}
