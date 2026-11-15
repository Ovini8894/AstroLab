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

        private static int BindRepairTargets()
        {
            int count = 0;
            Scene scene = SceneManager.GetActiveScene();
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform node in root.GetComponentsInChildren<Transform>(true))
            {
                string name = node.name;
                if (!name.StartsWith("REPAIR_", StringComparison.Ordinal)) continue;
                EquipmentId id;
                if (name.StartsWith("REPAIR_Painel_", StringComparison.Ordinal)) id = EquipmentId.SolarPanels;
                else if (name.StartsWith("REPAIR_Gerador_O2", StringComparison.Ordinal)) id = EquipmentId.LifeSupport;
                else if (name.StartsWith("REPAIR_Reciclador_H2O", StringComparison.Ordinal)) id = EquipmentId.WaterRecycler;
                else if (name.StartsWith("REPAIR_Estufa_Painel", StringComparison.Ordinal)) id = EquipmentId.Greenhouse;
                else if (name.StartsWith("REPAIR_Baterias", StringComparison.Ordinal)) id = EquipmentId.SuitCharger;
                else if (name.StartsWith("REPAIR_Antena_Prato", StringComparison.Ordinal)) id = EquipmentId.Communications;
                else continue;
                MissionEquipmentTarget target = node.GetComponent<MissionEquipmentTarget>();
                if (!target) target = node.gameObject.AddComponent<MissionEquipmentTarget>();
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

        private static void AddRadiationTarget()
        {
            Transform location = Find("INT_Abrigo");
            if (!location) location = Find("INT_Controle");
            GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "Nicholas_Painel_Manutencao_Abrigo";
            panel.transform.position = location ? location.position + new Vector3(0, 1.2f, 0) : new Vector3(-2, 1.2f, 5);
            panel.transform.localScale = new Vector3(0.7f, 0.55f, 0.13f);
            panel.GetComponent<Renderer>().sharedMaterial = null;
            panel.AddComponent<MissionEquipmentTarget>().Configure(EquipmentId.RadiationShelter);
        }

        private static int AddScienceMarkers()
        {
            Transform anchor = Find("WP_Usina_Solar") ?? Find("WP_Antena") ??
                               Find("WP_Rampa_Eclusa") ?? Find("Nicholas_WP_Rampa_Eclusa");
            if (!anchor) anchor = CreateAnchor("Nicholas_ScienceAnchor", new Vector3(0, 1, 15));
            // Posições de gameplay FICTÍCIAS, não coordenadas históricas. O modelador pode substituir os cubos.
            Vector3[] offsets =
            {
                new Vector3(7, 1.1f, 1), new Vector3(10, 1.1f, 3), new Vector3(13, 1.1f, 1),
                new Vector3(15, 1.1f, 4), new Vector3(18, 1.1f, 1)
            };
            for (int i = 0; i < Apollo15Trail.Items.Length; i++)
            {
                Apollo15Trail.Artifact item = Apollo15Trail.Items[i];
                GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                marker.name = "NICHOLAS_REPLICA_" + item.Id;
                marker.transform.position = anchor.position + offsets[i];
                marker.transform.localScale = new Vector3(0.7f, 0.8f, 0.7f);
                MissionHistoricalTarget target = marker.AddComponent<MissionHistoricalTarget>();
                target.Configure(item.Id);
                // AlvoExploracao está em outra branch; adicione por reflexão quando ela existir.
                Type infoType = Type.GetType("AstroLab.Capacete.AlvoExploracao, Assembly-CSharp");
                if (infoType != null)
                {
                    Component info = marker.AddComponent(infoType);
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
