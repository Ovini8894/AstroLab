#if UNITY_EDITOR
using System;
using System.Linq;
using AstroLab.Nicholas;
using AstroLab.Nicholas.Core;
using AstroLab.Nicholas.Editor;
using AstroLab.Nicholas.Integrations;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AstroLab.Gameplay.Editor
{
    public static class BaseGameplaySetup
    {
        [MenuItem("AstroLab/Base/Integrar restauração e recursos nesta cena")]
        public static void InstallCurrentScene()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Saia do Play antes de configurar a cena.");
            var scene = SceneManager.GetActiveScene();
            var player = UnityEngine.Object.FindAnyObjectByType<JogadorFPS>();
            if (!scene.IsValid() || !player || !GameObject.Find("BaseLunar"))
                throw new InvalidOperationException("A cena precisa conter a BaseLunar e o jogador de primeira pessoa.");
            var inner = GameObject.Find("INT_Eclusa")?.transform;
            var outer = GameObject.Find("WP_Rampa_Eclusa")?.transform;
            if (!inner || !outer) throw new InvalidOperationException("Âncoras da eclusa ausentes.");
            bool newMission = !UnityEngine.Object.FindAnyObjectByType<MissionController>();
            NicholasMissionSceneBuilder.EnsureBaseConsertadaInScene(scene);
            var mission = UnityEngine.Object.FindAnyObjectByType<MissionController>();
            var host = mission ? mission.gameObject : new GameObject("AstroLab_SistemasDaBase");
            if (!mission) mission = host.AddComponent<MissionController>();
            mission.Configure(player.transform, inner, outer);
            Ensure<MissionOverlay>(host); Ensure<NasaImageLibraryClient>(host); Ensure<MissionHelmetBridge>(host); Ensure<MissionScannerBridge>(host);
            Ensure<BaseKnowledgeBinder>(host); Ensure<CloudCatalogClient>(host); Ensure<BaseResourceDashboard>(host);
            var interaction = player.GetComponent<MissionPlayerInteraction>() ?? player.gameObject.AddComponent<MissionPlayerInteraction>();
            var so = new SerializedObject(interaction); var tutorial = so.FindProperty("createTutorialRepairTerminal");
            if (tutorial != null) { tutorial.boolValue = false; so.ApplyModifiedPropertiesWithoutUndo(); }
            int repairs = NicholasMissionSceneBuilder.BindRepairTargets();
            if (!UnityEngine.Object.FindAnyObjectByType<RadiationPanelVisual>()) NicholasMissionSceneBuilder.AddRadiationTarget();
            int existing = UnityEngine.Object.FindObjectsByType<MissionHistoricalTarget>(FindObjectsInactive.Include).Length;
            if (existing == 0)
            {
                NicholasMissionSceneBuilder.AddScienceMarkers();
                Vector3[] positions = { new Vector3(35, 0, 36), new Vector3(-40, 0, 60), new Vector3(-110, 0, 15), new Vector3(40, 0, -24), new Vector3(-12, 0, -42) };
                for (int i = 0; i < Apollo15Trail.Items.Length; i++)
                {
                    var target = UnityEngine.Object.FindObjectsByType<MissionHistoricalTarget>().Single(t => t.Artifact == Apollo15Trail.Items[i].Id);
                    Vector3 at = positions[i];
                    if (Physics.Raycast(at + Vector3.up * 300, Vector3.down, out var hit, 500, ~0, QueryTriggerInteraction.Ignore)) at.y = hit.point.y;
                    target.transform.position = at;
                }
            }
            if (newMission)
            {
                player.transform.position = inner.position + Vector3.up * .12f;
                player.transform.rotation = Quaternion.Euler(0, 180, 0);
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log("[AstroLab][Gameplay] Cena integrada: " + scene.name + ". Alvos de manutenção: " + repairs + "; réplicas Apollo 15: " + UnityEngine.Object.FindObjectsByType<MissionHistoricalTarget>().Length);
        }
        private static T Ensure<T>(GameObject host) where T : Component => host.GetComponent<T>() ?? host.AddComponent<T>();
    }
}
#endif
