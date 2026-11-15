using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AstroLab.Capacete;
using AstroLab.Nicholas.Core;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;

namespace AstroLab.Gameplay
{
    [Serializable] public sealed class EquipmentKnowledge { public string id, description, fault, repair, benefit; }
    [Serializable] public sealed class EnvironmentKnowledge { public string match, title, description, category; }
    [Serializable] public sealed class ArtifactKnowledge { public string id, title, description, source; }
    [Serializable] public sealed class KnowledgeData { public int schemaVersion; public EquipmentKnowledge[] equipment; public EnvironmentKnowledge[] environment; public ArtifactKnowledge[] artifacts; }

    public static class BaseKnowledgeCatalog
    {
        private static KnowledgeData data;
        public static event Action Changed;
        public static KnowledgeData Data { get { EnsureLoaded(); return data; } }
        public static void EnsureLoaded()
        {
            if (data != null) return;
            var asset = Resources.Load<TextAsset>("base_knowledge");
            if (asset) Apply(JsonUtility.FromJson<KnowledgeData>(asset.text));
        }
        public static bool Apply(KnowledgeData candidate)
        {
            if (candidate == null || candidate.schemaVersion != 1 || candidate.equipment == null || candidate.environment == null || candidate.artifacts == null || candidate.artifacts.Length != 5) return false;
            if (candidate.equipment.Length != MissionRuleSet.Definitions.Length || candidate.equipment.Any(e => e == null ||
                !Enum.TryParse(e.id, out EquipmentId id) || string.IsNullOrWhiteSpace(e.description) || string.IsNullOrWhiteSpace(e.repair))) return false;
            if (candidate.equipment.Select(e => e.id).Distinct().Count() != MissionRuleSet.Definitions.Length) return false;
            if (candidate.environment.Any(e => e == null || string.IsNullOrWhiteSpace(e.match) || string.IsNullOrWhiteSpace(e.description))) return false;
            if (candidate.artifacts.Any(e => e == null || !Enum.TryParse(e.id, out ArtifactId id) || string.IsNullOrWhiteSpace(e.description)) || candidate.artifacts.Select(e => e.id).Distinct().Count() != 5) return false;
            data = candidate; Changed?.Invoke(); return true;
        }
        public static EquipmentKnowledge Find(EquipmentId id) => Data?.equipment.FirstOrDefault(e => e.id == id.ToString());

        public static bool Describe(string name, out EnvironmentKnowledge entry)
        {
            entry = Data?.environment.FirstOrDefault(e => name.IndexOf(e.match, StringComparison.OrdinalIgnoreCase) >= 0);
            return entry != null;
        }
    }

    public sealed class BaseKnowledgeBinder : MonoBehaviour
    {
        private void Awake() { BaseKnowledgeCatalog.EnsureLoaded(); }
        private void Start() { Bind(); BaseKnowledgeCatalog.Changed += Bind; }
        private void OnDestroy() { BaseKnowledgeCatalog.Changed -= Bind; }
        public void Bind()
        {
            int registered = 0;
            foreach (Transform node in FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (node.gameObject.scene != gameObject.scene || !node.GetComponent<Collider>()) continue;
                var artifact = node.GetComponentInParent<AstroLab.Nicholas.MissionHistoricalTarget>();
                if (artifact)
                {
                    var knowledge = BaseKnowledgeCatalog.Data.artifacts.FirstOrDefault(a => a.id == artifact.Artifact.ToString());
                    if (knowledge != null)
                    {
                        var info = artifact.GetComponent<AlvoExploracao>() ?? artifact.gameObject.AddComponent<AlvoExploracao>();
                        info.titulo = knowledge.title + " [RÉPLICA DIDÁTICA]";
                        info.descricao = knowledge.description + "\nFonte histórica: " + knowledge.source;
                        info.categoria = "APOLLO 15 · EXTRA OPCIONAL";
                    }
                    continue;
                }
                var target = node.GetComponentInParent<AstroLab.Nicholas.MissionEquipmentTarget>();
                if (target)
                {
                    var entry = BaseKnowledgeCatalog.Find(target.Equipment);
                    if (entry == null) continue;
                    var info = target.GetComponent<AlvoExploracao>() ?? target.gameObject.AddComponent<AlvoExploracao>();
                    info.titulo = MissionRuleSet.Find(target.Equipment).DisplayName;
                    info.descricao = entry.description + "\n\nSituação: " + entry.fault + "\n\nComo restaurar: " + entry.repair + "\n\nResultado: " + entry.benefit;
                    info.charada = "[E] Inspecionar manutenção · [R] Reparar junto ao equipamento · [Tab] Gestão da base";
                    info.categoria = "MANUTENÇÃO DA BASE"; registered++;
                }
                else if (BaseKnowledgeCatalog.Describe(node.name, out var entry))
                {
                    var info = node.GetComponent<AlvoExploracao>() ?? node.gameObject.AddComponent<AlvoExploracao>();
                    info.titulo = entry.title; info.descricao = entry.description; info.categoria = entry.category; info.charada = ""; registered++;
                }
            }
            Debug.Log("[AstroLab][Catalog] Objetos informados: " + registered);
        }
    }

    public sealed class CloudCatalogClient : MonoBehaviour
    {
        public string Status { get; private set; } = "Catálogo local disponível";
        public bool LoadedFromCloud { get; private set; }
        private async void Start()
        {
            try
            {
                if (Application.internetReachability == NetworkReachability.NotReachable) return;
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync(new InitializationOptions().SetEnvironmentName("production"));
                if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
                if (!this) return;
                Debug.Log("[AstroLab][Catalog] Identidade de convidado pronta para leitura do catálogo.");
                var values = await CloudSaveService.Instance.Data.Custom.LoadAsync("astrolab_catalog_v1", new HashSet<string> { "catalog" });
                if (!this) return;
                if (values.TryGetValue("catalog", out var item) && BaseKnowledgeCatalog.Apply(item.Value.GetAs<KnowledgeData>()))
                {
                    LoadedFromCloud = true; Status = "Catálogo atualizado da nuvem";
                    Debug.Log("[AstroLab][Catalog] Catálogo compartilhado carregado e validado.");
                }
                else Status = "Catálogo local disponível · publicação remota pendente";
            }
            catch { if (this) { Status = "Catálogo local disponível · sem atualização remota"; Debug.LogWarning("[AstroLab][Catalog] Nuvem indisponível; descrições locais preservadas."); } }
        }
    }
}
