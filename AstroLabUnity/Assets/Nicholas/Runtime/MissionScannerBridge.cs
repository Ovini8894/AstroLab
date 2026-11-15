using System.Reflection;
using UnityEngine;
using UnityEngine.Events;

namespace AstroLab.Nicholas
{
    /// <summary>
    /// Integra o scanner do colega e remove SOMENTE o painel de progresso central
    /// na cena Nicholas. O scanner portátil continua exibindo o progresso.
    /// Não altera nem sobrescreve os scripts/assets da equipe.
    /// </summary>
    [DefaultExecutionOrder(210)]
    public sealed class MissionScannerBridge : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour scanner;
        [SerializeField] private MissionOverlay overlay;
        [SerializeField] private bool hideCentralProgressPanel = true;

        private UnityEvent<GameObject> finishedScan;
        private GameObject centralProgressPanel;

        private void Start()
        {
            if (!scanner) scanner = OptionalTeamComponents.FindByName("ScannerPrimeiraPessoa");
            if (!overlay) overlay = Object.FindAnyObjectByType<MissionOverlay>();
            if (!scanner) return;
            System.Type type = scanner.GetType();
            FieldInfo eventField = type.GetField("aoConcluir", BindingFlags.Public | BindingFlags.Instance);
            if (eventField != null) finishedScan = eventField.GetValue(scanner) as UnityEvent<GameObject>;
            if (finishedScan != null) finishedScan.AddListener(Scanned);

            FieldInfo progressField = type.GetField("painelProgresso", BindingFlags.Public | BindingFlags.Instance);
            if (progressField != null) centralProgressPanel = progressField.GetValue(scanner) as GameObject;
        }

        private void LateUpdate()
        {
            // O scanner do time mostra um painel central para qualquer parede ou piso.
            // Mantemos o scanner funcional, mas escondemos esse painel redundante.
            // A tela do aparelho continua mostrando "ANALISANDO XX%".
            if (hideCentralProgressPanel && centralProgressPanel && centralProgressPanel.activeSelf)
                centralProgressPanel.SetActive(false);
        }

        private void Scanned(GameObject scanned)
        {
            if (!scanned || !overlay) return;
            MissionHistoricalTarget target = scanned.GetComponentInParent<MissionHistoricalTarget>();
            if (!target) target = scanned.GetComponentInChildren<MissionHistoricalTarget>();
            if (target) overlay.SetPendingArtifact(target.Artifact);
        }

        private void OnDestroy()
        {
            if (finishedScan != null) finishedScan.RemoveListener(Scanned);
        }
    }
}
