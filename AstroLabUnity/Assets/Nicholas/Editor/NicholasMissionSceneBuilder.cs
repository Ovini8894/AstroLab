#if UNITY_EDITOR
using System;
using System.IO;
using AstroLab.Nicholas;
using AstroLab.Nicholas.Core;
using AstroLab.Nicholas.Integrations;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AstroLab.Nicholas.Editor
{
    /// <summary>
    /// Gera uma CÓPIA da cena do time; nunca toca BaseLunar/ ou no arquivo fonte.
    /// Reexecutar NÃO sobrescreve trabalho já feito na cena Nicholas.
    /// </summary>
    public static class NicholasMissionSceneBuilder
    {
        private const string SourceWithHud = "Assets/Treinamentos/CapacetePedro.unity";
        private const string SourceMap = "Assets/BaseLunar/Cenas/Lua.unity";
        private const string SourceBruno = "Assets/BrunoLaboratorio/Scenes/BaseLunarBruno.unity";
        private const string SourceSample = "Assets/Scenes/SampleScene.unity";
        private const string Destination = "Assets/Nicholas/Cenas/MissaoNicholas.unity";
        private const string BaseConsertadaPrefabPath = "Assets/BaseConsertada/Prefabs/BaseConsertada.prefab";

        [MenuItem("AstroLab/Nicholas/1 - Criar cena de missão (cópia segura)")]
        public static void CreateMissionScene()
        {
            if (File.Exists(Destination))
            {
                EditorUtility.DisplayDialog("Cena Nicholas já existe",
                    "A cena " + Destination + " não será sobrescrita. Trabalhe nela ou renomeie manualmente para gerar outra.", "Entendi");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string source = File.Exists(SourceWithHud) ? SourceWithHud :
                            File.Exists(SourceMap) ? SourceMap :
                            File.Exists(SourceBruno) ? SourceBruno : SourceSample;
            if (!File.Exists(source))
            {
                EditorUtility.DisplayDialog("Base ausente", "Não encontrei uma cena fonte para copiar. Verifique os assets enviados pelo time.", "OK");
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(Destination));
            if (!AssetDatabase.CopyAsset(source, Destination))
            {
                EditorUtility.DisplayDialog("Falha de cópia", "Não foi possível copiar a cena fonte.", "OK");
                return;
            }
            AssetDatabase.ImportAsset(Destination);
            Scene scene = EditorSceneManager.OpenScene(Destination, OpenSceneMode.Single);
            GameObject host = new GameObject("Nicholas_MissionSystems");
            MissionController mission = host.AddComponent<MissionController>();
            MissionOverlay overlay = host.AddComponent<MissionOverlay>();
            host.AddComponent<NasaImageLibraryClient>();
            host.AddComponent<MissionHelmetBridge>();
            host.AddComponent<MissionScannerBridge>();

            MonoBehaviour player = FindScript("JogadorFPS");
            if (!player)
            {
                // A branch nicholas fornecida não contém o JogadorFPS nem o HUD do Pedro.
                // Quando a cena do Bruno for usada, não modificamos o prefab dele.
                // Desabilitamos somente a instância na CÓPIA e criamos um FPS próprio.
                MonoBehaviour bruno = FindScript("LaboratorioPlayerController");
                if (bruno) bruno.gameObject.SetActive(false);
                foreach (Camera existing in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                {
                    existing.enabled = false;
                    AudioListener listener = existing.GetComponent<AudioListener>();
                    if (listener) listener.enabled = false;
                }
                GameObject substitute = new GameObject("Nicholas_FPS_Player");
                substitute.transform.position = new Vector3(0, 2.2f, 0);
                CharacterController controller = substitute.AddComponent<CharacterController>();
                controller.height = 1.8f;
                controller.center = new Vector3(0, 0.9f, 0);
                GameObject cameraGo = new GameObject("Main Camera Nicholas");
                cameraGo.tag = "MainCamera";
                cameraGo.transform.SetParent(substitute.transform, false);
                cameraGo.transform.localPosition = new Vector3(0, 1.65f, 0);
                cameraGo.AddComponent<Camera>();
                cameraGo.AddComponent<AudioListener>();
                player = substitute.AddComponent<NicholasFirstPersonController>();
            }
            player.gameObject.AddComponent<MissionPlayerInteraction>();
            Transform eclusa = Find("INT_Eclusa");
            Transform externo = Find("WP_Rampa_Eclusa");
            if (!eclusa) eclusa = CreateAnchor("Nicholas_INT_Eclusa", new Vector3(0, 1, 5));
            if (!externo) externo = CreateAnchor("Nicholas_WP_Rampa_Eclusa", new Vector3(0, 1, 15));
            mission.Configure(player.transform, eclusa, externo);
            EnsureBaseConsertadaInScene(scene);
            int assigned = BindRepairTargets();
            if (assigned == 0) assigned += CreateFallbackStations();
            AddRadiationTarget();
            int artifacts = AddScienceMarkers();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, Destination);
            Debug.Log("[Nicholas] Cena independente criada em " + Destination +
                      ". Equipamentos: " + assigned + "; marcadores Apollo 15: " + artifacts +
                      ". A cena e o FBX do modelador NÃO foram modificados.");
            EditorUtility.DisplayDialog("AstroLab / Nicholas", "Cena pronta: " + Destination +
                "\nInspeções detectadas: " + assigned + "\nRéplicas Apollo 15: " + artifacts +
                "\n\nAperte Play. F2 abre a missão; L usa a eclusa; E inspeciona.", "Abrir");
        }

        [MenuItem("AstroLab/Nicholas/2 - Abrir cena Nicholas")]
        public static void OpenMissionScene()
        {
            if (!File.Exists(Destination))
            {
                EditorUtility.DisplayDialog("Cena ausente", "Rode primeiro o menu 1 - Criar cena de missão.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(Destination, OpenSceneMode.Single);
        }

        [MenuItem("AstroLab/Nicholas/Validar âncoras da eclusa")]
        public static void ValidateAirlockAnchorsInCurrentScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != Destination)
            {
                EditorUtility.DisplayDialog("Cena incorreta",
                    "Abra primeiro a cena " + Destination + " para validar as âncoras da eclusa.", "OK");
                return;
            }
            MissionController mission = UnityEngine.Object.FindFirstObjectByType<MissionController>();
            Transform intEclusa = Find("INT_Eclusa");
            Transform extEclusa = Find("WP_Rampa_Eclusa");
            if (!mission || !intEclusa || !extEclusa)
            {
                EditorUtility.DisplayDialog("Âncoras ausentes",
                    "Verifique se MissionController, INT_Eclusa e WP_Rampa_Eclusa existem na cena.", "OK");
                return;
            }
            bool changed = mission.InnerAirlock != intEclusa || mission.OuterAirlock != extEclusa;
            if (changed)
            {
                mission.Configure(mission.PlayerTransform, intEclusa, extEclusa);
                EditorUtility.SetDirty(mission);
                EditorSceneManager.MarkSceneDirty(scene);
            }
            EditorUtility.DisplayDialog("AstroLab / Nicholas",
                changed ? "Âncoras da eclusa religadas a INT_Eclusa e WP_Rampa_Eclusa. Salve a cena."
                        : "Âncoras da eclusa já apontam para INT_Eclusa e WP_Rampa_Eclusa da BaseLunar.", "OK");
        }

        [MenuItem("AstroLab/Nicholas/Adicionar BaseConsertada à cena existente")]
        public static void AddBaseConsertadaToCurrentScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != Destination)
            {
                EditorUtility.DisplayDialog("Cena incorreta",
                    "Abra primeiro a cena " + Destination + " para adicionar a BaseConsertada.", "OK");
                return;
            }
            if (EnsureBaseConsertadaInScene(scene))
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorUtility.DisplayDialog("AstroLab / Nicholas",
                    "BaseConsertada.prefab instanciado em " + Destination + ". Salve a cena (Ctrl+S).", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("AstroLab / Nicholas",
                    "A cena já possui uma instância de BaseConsertada (ou o prefab não foi encontrado).", "OK");
            }
        }

        public static bool EnsureBaseConsertadaInScene(Scene scene)
        {
            if (UnityEngine.Object.FindFirstObjectByType<BaseConsertada>()) return false;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BaseConsertadaPrefabPath);
            if (!prefab)
            {
                Debug.LogWarning("[Nicholas] Prefab não encontrado em " + BaseConsertadaPrefabPath);
                return false;
            }
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            return instance != null;
        }

        private static Transform CreateAnchor(string name, Vector3 at)
        {
            GameObject node = new GameObject(name);
            node.transform.position = at;
            return node.transform;
        }

        private static MonoBehaviour FindScript(string typeName)
        {
            foreach (MonoBehaviour component in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (component && component.GetType().Name == typeName) return component;
            return null;
        }

        private static int CreateFallbackStations()
        {
            // Dependências do time ainda não disponíveis: protótipos clicáveis preservam loop de reparos.
            EquipmentId[] ids = { EquipmentId.SolarPanels, EquipmentId.LifeSupport,
                EquipmentId.WaterRecycler, EquipmentId.Greenhouse, EquipmentId.SuitCharger,
                EquipmentId.Communications };
            Vector3[] positions = { new Vector3(2, 1.2f, 17), new Vector3(2, 1.2f, 1),
                new Vector3(-2, 1.2f, 1), new Vector3(2, 1.2f, 3),
                new Vector3(-2, 1.2f, 3), new Vector3(0, 1.2f, 3) };
            for (int i = 0; i < ids.Length; i++)
            {
                GameObject mock = GameObject.CreatePrimitive(PrimitiveType.Cube);
                mock.name = "Nicholas_Prototipo_" + ids[i];
                mock.transform.position = positions[i];
                mock.transform.localScale = new Vector3(0.8f, 1f, 0.35f);
                mock.AddComponent<MissionEquipmentTarget>().Configure(ids[i]);
            }
            return ids.Length;
        }

        private static Transform Find(string name)
        {
            Scene scene = SceneManager.GetActiveScene();
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform node in root.GetComponentsInChildren<Transform>(true))
                    if (node.name == name) return node;
            return null;
        }

        public static int BindRepairTargets()
        {
            int count = 0;
            Scene scene = SceneManager.GetActiveScene();
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform node in root.GetComponentsInChildren<Transform>(true))
            {
                string name = node.name;
                EquipmentId id;
                if (!AstroLab.Gameplay.MissionPieceBindings.TryGetEquipment(name, out id)) continue;
                MissionEquipmentTarget target = node.GetComponent<MissionEquipmentTarget>();
                if (!target) target = node.gameObject.AddComponent<MissionEquipmentTarget>();
                bool architecture = AstroLab.Gameplay.MissionPieceBindings.IsArchitecture(PecasDaBase.Encontrar(name));
                target.enabled = !architecture;
                target.Configure(id);
                if (!node.GetComponent<Collider>())
                {
                    MeshFilter mf = node.GetComponent<MeshFilter>();
                    if (mf && mf.sharedMesh)
                    {
                        MeshCollider meshCollider = node.gameObject.AddComponent<MeshCollider>();
                        meshCollider.sharedMesh = mf.sharedMesh;
                    }
                }
                count++;
            }
            // Havendo console, permite descobrir o ponto de comando, sem alterar os assets da equipe.
            return count;
        }

        [MenuItem("AstroLab/Nicholas/Atualizar visuais de abrigo e réplicas Apollo 15")]
        public static void RefreshMissionVisualsInCurrentScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != Destination)
            {
                EditorUtility.DisplayDialog("Cena incorreta",
                    "Abra primeiro a cena " + Destination + " para atualizar os visuais.", "OK");
                return;
            }
            AddRadiationTarget();
            int artifacts = AddScienceMarkers();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorUtility.DisplayDialog("AstroLab / Nicholas",
                "Painel do abrigo e " + artifacts + " balizas Apollo 15 atualizados em " + Destination + ". Salve a cena (Ctrl+S).", "OK");
        }

        private static Material LoadFirstMaterial(params string[] paths)
        {
            foreach (string path in paths)
            {
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat != null) return mat;
            }
            return null;
        }

        public static void AddRadiationTarget()
        {
            Transform existing = Find("Nicholas_Painel_Manutencao_Abrigo");
            Vector3 pos;
            Quaternion rot = Quaternion.identity;
            if (existing)
            {
                pos = existing.position;
                rot = existing.rotation;
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }
            else
            {
                Transform location = Find("INT_Abrigo");
                if (!location) location = Find("INT_Controle");
                pos = location ? location.position + new Vector3(0, 1.2f, 0) : new Vector3(-2, 1.2f, 5);
            }

            Material bodyMat = LoadFirstMaterial(
                "Assets/BaseLunar/Materiais/M_Console_Branco.mat",
                "Assets/BaseLunar/Materiais/M_Branco.mat",
                "Assets/BaseLunar/Materiais/M_Casco.mat");
            Material brokenMat = LoadFirstMaterial(
                "Assets/BaseLunar/Materiais/M_Vermelho.mat",
                "Assets/BaseLunar/Materiais/M_Luz_Laranja.mat");
            Material repairedMat = LoadFirstMaterial(
                "Assets/BaseLunar/Materiais/M_Detalhes_Ciano.mat",
                "Assets/Personagem/PrimeiraPessoa/Materiais/LuzCiano.mat",
                "Assets/BaseLunar/Materiais/M_Faixa_Azul.mat");

            GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "Nicholas_Painel_Manutencao_Abrigo";
            panel.transform.SetPositionAndRotation(pos, rot);
            panel.transform.localScale = new Vector3(0.7f, 0.55f, 0.13f);
            if (bodyMat) panel.GetComponent<Renderer>().sharedMaterial = bodyMat;
            panel.AddComponent<MissionEquipmentTarget>().Configure(EquipmentId.RadiationShelter);

            GameObject indicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
            indicator.name = "Indicador_Estado";
            indicator.transform.SetParent(panel.transform, false);
            indicator.transform.localPosition = new Vector3(0f, 0.12f, 0.52f);
            indicator.transform.localScale = new Vector3(0.75f, 0.38f, 0.12f);
            Collider indCol = indicator.GetComponent<Collider>();
            if (indCol) UnityEngine.Object.DestroyImmediate(indCol);
            Renderer indRenderer = indicator.GetComponent<Renderer>();
            if (brokenMat) indRenderer.sharedMaterial = brokenMat;

            RadiationPanelVisual visual = panel.AddComponent<RadiationPanelVisual>();
            visual.Configure(indRenderer, brokenMat, repairedMat);
            visual.SetRepaired(false);
        }

        public static int AddScienceMarkers()
        {
            Transform anchor = Find("WP_Usina_Solar") ?? Find("WP_Antena") ??
                               Find("WP_Rampa_Eclusa") ?? Find("Nicholas_WP_Rampa_Eclusa");
            if (!anchor) anchor = CreateAnchor("Nicholas_ScienceAnchor", new Vector3(0, 1, 15));
            // Posições de gameplay FICTÍCIAS, não coordenadas históricas.
            Vector3[] offsets =
            {
                new Vector3(7, 0f, 1), new Vector3(10, 0f, 3), new Vector3(13, 0f, 1),
                new Vector3(15, 0f, 4), new Vector3(18, 0f, 1)
            };

            Material darkMetal = LoadFirstMaterial(
                "Assets/BaseLunar/Materiais/M_Metal_Escuro.mat",
                "Assets/BaseLunar/Materiais/M_Casco_Escuro.mat",
                "Assets/BaseLunar/Materiais/M_Metal.mat");
            Material cyanAccent = LoadFirstMaterial(
                "Assets/BaseLunar/Materiais/M_Detalhes_Ciano.mat",
                "Assets/Personagem/PrimeiraPessoa/Materiais/LuzCiano.mat",
                "Assets/BaseLunar/Materiais/M_Faixa_Azul.mat");

            for (int i = 0; i < Apollo15Trail.Items.Length; i++)
            {
                Apollo15Trail.Artifact item = Apollo15Trail.Items[i];
                string markerName = "NICHOLAS_REPLICA_" + item.Id;
                Transform existing = Find(markerName);
                Vector3 pos = existing ? existing.position : anchor.position + offsets[i];
                if (existing) UnityEngine.Object.DestroyImmediate(existing.gameObject);

                GameObject root = new GameObject(markerName);
                root.transform.position = pos;

                BoxCollider rootCollider = root.AddComponent<BoxCollider>();
                rootCollider.center = new Vector3(0f, 0.85f, 0f);
                rootCollider.size = new Vector3(0.85f, 1.7f, 0.85f);

                // Base cilíndrica
                GameObject baseCyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                baseCyl.name = "Base";
                baseCyl.transform.SetParent(root.transform, false);
                baseCyl.transform.localPosition = new Vector3(0f, 0.1f, 0f);
                baseCyl.transform.localScale = new Vector3(0.75f, 0.1f, 0.75f);
                UnityEngine.Object.DestroyImmediate(baseCyl.GetComponent<Collider>());
                if (darkMetal) baseCyl.GetComponent<Renderer>().sharedMaterial = darkMetal;

                // Haste
                GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                stem.name = "Haste";
                stem.transform.SetParent(root.transform, false);
                stem.transform.localPosition = new Vector3(0f, 0.65f, 0f);
                stem.transform.localScale = new Vector3(0.14f, 0.5f, 0.14f);
                UnityEngine.Object.DestroyImmediate(stem.GetComponent<Collider>());
                if (darkMetal) stem.GetComponent<Renderer>().sharedMaterial = darkMetal;

                // Topo emissivo ciano com forma distinta por artefato
                PrimitiveType topShape = PrimitiveType.Sphere;
                Vector3 topScale = new Vector3(0.32f, 0.32f, 0.32f);
                Vector3 topEuler = Vector3.zero;
                switch (item.Id)
                {
                    case ArtifactId.Hammer:
                        topShape = PrimitiveType.Cube;
                        topScale = new Vector3(0.42f, 0.16f, 0.16f);
                        topEuler = new Vector3(0f, 25f, 15f);
                        break;
                    case ArtifactId.Feather:
                        topShape = PrimitiveType.Capsule;
                        topScale = new Vector3(0.14f, 0.28f, 0.14f);
                        topEuler = new Vector3(0f, 0f, 35f);
                        break;
                    case ArtifactId.Rover:
                        topShape = PrimitiveType.Cube;
                        topScale = new Vector3(0.46f, 0.20f, 0.32f);
                        break;
                    case ArtifactId.Retroreflector:
                        topShape = PrimitiveType.Cube;
                        topScale = new Vector3(0.36f, 0.08f, 0.36f);
                        topEuler = new Vector3(-28f, 0f, 0f);
                        break;
                    case ArtifactId.FalconDescentStage:
                        topShape = PrimitiveType.Cylinder;
                        topScale = new Vector3(0.44f, 0.14f, 0.44f);
                        break;
                }
                GameObject top = GameObject.CreatePrimitive(topShape);
                top.name = "Topo_Emissivo";
                top.transform.SetParent(root.transform, false);
                top.transform.localPosition = new Vector3(0f, 1.28f, 0f);
                top.transform.localScale = topScale;
                top.transform.localEulerAngles = topEuler;
                UnityEngine.Object.DestroyImmediate(top.GetComponent<Collider>());
                if (cyanAccent) top.GetComponent<Renderer>().sharedMaterial = cyanAccent;

                MissionHistoricalTarget target = root.AddComponent<MissionHistoricalTarget>();
                target.Configure(item.Id);

                Type infoType = Type.GetType("AstroLab.Capacete.AlvoExploracao, Assembly-CSharp");
                if (infoType != null)
                {
                    Component info = root.AddComponent(infoType);
                    infoType.GetField("titulo")?.SetValue(info, item.Name + " [RÉPLICA DIDÁTICA]");
                    infoType.GetField("categoria")?.SetValue(info, "APOLLO 15 · NASA");
                    infoType.GetField("descricao")?.SetValue(info, item.History);
                    infoType.GetField("charada")?.SetValue(info, item.ClueToNext);
                }
            }
            return Apollo15Trail.Items.Length;
        }
    }
}
#endif
