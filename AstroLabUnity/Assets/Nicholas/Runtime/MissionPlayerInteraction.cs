using AstroLab.Nicholas.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AstroLab.Nicholas
{
    /// <summary>
    /// Interação contextual: E só responde a MissionEquipmentTarget e MissionHistoricalTarget.
    /// O terminal provisório é criado APENAS em Play, na cópia Nicholas da cena.
    /// Nenhum objeto/modelo da BaseLunar é alterado.
    /// </summary>
    public sealed class MissionPlayerInteraction : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField] private MissionOverlay overlay;
        [SerializeField, Min(1f)] private float distance = 5f;
        [SerializeField] private LayerMask layers = ~0;
        [Header("Somente para validar o loop de reparo em Play")]
        [SerializeField] private bool createTutorialRepairTerminal = false;

        public string CurrentPrompt { get; private set; }
        public MissionEquipmentTarget AimedEquipment { get; private set; }
        public MissionHistoricalTarget AimedHistory { get; private set; }
        private MonoBehaviour helmet;
        private MissionEquipmentTarget tutorialTarget;

        private void Start()
        {
            if (!playerCamera) playerCamera = GetComponentInChildren<Camera>();
            if (!playerCamera) playerCamera = Camera.main;
            if (!overlay) overlay = Object.FindAnyObjectByType<MissionOverlay>();
            helmet = OptionalTeamComponents.FindByName("HudCapacete");
            if (createTutorialRepairTerminal && playerCamera)
                CreateTutorialTerminal();
        }

        private void Update()
        {
            CurrentPrompt = string.Empty;
            AimedEquipment = null;
            AimedHistory = null;
            if (!MissionController.Instance || !playerCamera || !overlay || overlay.IsModalOpen ||
                OptionalTeamComponents.IsHelmetModalOpen(helmet)) return;

            if (Keyboard.current != null && Keyboard.current.lKey.wasPressedThisFrame)
                MissionController.Instance.ChangeEva();

            MissionEquipmentTarget equipment = null;
            MissionHistoricalTarget history = null;
            AstroLab.Asteroids.AsteroidObservationStation observation=null;
            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            RaycastHit[] hits = Physics.RaycastAll(ray, distance, layers, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            // A câmera ou partes do próprio jogador nunca devem impedir a interação.
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform)) continue;
                observation=hit.collider.GetComponentInParent<AstroLab.Asteroids.AsteroidObservationStation>();
                if(observation)break;
                equipment = hit.collider.GetComponentInParent<MissionEquipmentTarget>();
                history = hit.collider.GetComponentInParent<MissionHistoricalTarget>();
                if (equipment && !equipment.isActiveAndEnabled) equipment = null;
                if (history && !history.isActiveAndEnabled) history = null;
                if (equipment || history) break;
                // Uma superfície sólida deve bloquear a interação com máquinas atrás dela.
                break;
            }

            // Facilita o PRIMEIRO TESTE: a mira não precisa acertar um pequeno collider do terminal.
            // Não se aplica aos objetos finais do cenário.
            if (!equipment && !history && tutorialTarget)
            {
                Vector3 toTutorial = tutorialTarget.transform.position - playerCamera.transform.position;
                if (toTutorial.sqrMagnitude < distance * distance &&
                    Vector3.Dot(playerCamera.transform.forward, toTutorial.normalized) > 0.84f)
                    equipment = tutorialTarget;
            }

            AimedEquipment = equipment;
            AimedHistory = history;

            if(observation)CurrentPrompt="OBSERVATÓRIO NASA · [E] ou [F4] Abrir câmera e analisar asteroides";
            else if (equipment)
            {
                EquipmentDefinition def = MissionRuleSet.Find(equipment.Equipment);
                if (!MissionController.Instance.Engine.IsRepaired(equipment.Equipment))
                {
                    var availability = MissionController.Instance.Engine.CanRepair(equipment.Equipment);
                    CurrentPrompt = availability.Success
                        ? equipment.Label + " · [R] REPARAR (" + def.RepairParts + " peças, " + def.RepairTime + " ações) · [E] Detalhes"
                        : equipment.Label + " · " + availability.Message + " · [E] Detalhes";
                }
                else
                    CurrentPrompt = equipment.Equipment == EquipmentId.SuitCharger
                        ? "ESTAÇÃO DO TRAJE · [C] RECARREGAR O2 + BATERIA (1 ação) · [E] Detalhes"
                        : "[E] INSPECIONAR · " + equipment.Label;
            }
            else if (history)
                CurrentPrompt = "Segure [E] ou Clique Esquerdo com o Scanner: " + history.Label;
            else if (MissionController.Instance.Engine != null)
            {
                bool outside = MissionController.Instance.Engine.State.IsOutside;
                Transform anchor = outside ? MissionController.Instance.OuterAirlock : MissionController.Instance.InnerAirlock;
                if (anchor && Vector3.Distance(transform.position, anchor.position) <= MissionController.Instance.ReturnRadius)
                    CurrentPrompt = outside ? "[L] ENTRAR NA ECLUSA (Voltar para a base)" : "[L] SAIR PELA ECLUSA (Iniciar EVA)";
            }

            if (Keyboard.current == null) return;
            if (equipment && Keyboard.current.rKey.wasPressedThisFrame)
            {
                RepairAimed();
                return;
            }
            if (equipment && Keyboard.current.eKey.wasPressedThisFrame)
                overlay.OpenEquipment(equipment.Equipment, true);
        }

        public OperationResult RepairAimed()
        {
            if (!AimedEquipment || !AimedEquipment.isActiveAndEnabled)
                return OperationResult.Fail("Mire em um equipamento próximo.");
            return MissionController.Instance.Repair(AimedEquipment.Equipment);
        }

        private void CreateTutorialTerminal()
        {
            const string terminalName = "Nicholas_TERMINAL_TESTE_SuporteVida";
            GameObject existing = GameObject.Find(terminalName);
            if (existing)
            {
                tutorialTarget = existing.GetComponent<MissionEquipmentTarget>();
                return;
            }

            // Posição visível à frente e levemente à direita, sem modificar o cenário original.
            Vector3 forward = Vector3.ProjectOnPlane(playerCamera.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.001f) forward = transform.forward;
            forward.Normalize();
            Vector3 position = playerCamera.transform.position + forward * 1.9f +
                               playerCamera.transform.right * 0.75f + Vector3.down * 0.48f;

            GameObject terminal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            terminal.name = terminalName;
            terminal.transform.position = position;
            terminal.transform.rotation = Quaternion.LookRotation(playerCamera.transform.position - position, Vector3.up);
            terminal.transform.localScale = new Vector3(0.62f, 0.43f, 0.18f);
            tutorialTarget = terminal.AddComponent<MissionEquipmentTarget>();
            tutorialTarget.Configure(EquipmentId.LifeSupport);

            Renderer renderer = terminal.GetComponent<Renderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) shader = Shader.Find("Standard");
            if (shader)
            {
                Material material = new Material(shader);
                material.color = new Color(0.06f, 0.55f, 0.62f);
                renderer.material = material; // Apenas material temporário de runtime.
            }

            GameObject label = new GameObject("Rotulo_TESTE");
            label.transform.SetParent(terminal.transform, false);
            label.transform.localPosition = new Vector3(0f, 0f, 0.54f);
            label.transform.localScale = new Vector3(1f / terminal.transform.localScale.x,
                                                    1f / terminal.transform.localScale.y, 1f);
            TextMesh text = label.AddComponent<TextMesh>();
            text.text = "TESTE O2\n[E] INSPECIONAR";
            text.alignment = TextAlignment.Center;
            text.anchor = TextAnchor.MiddleCenter;
            text.characterSize = 0.045f;
            text.fontSize = 44;
            text.color = Color.white;

            Debug.Log("[AstroLab][Nicholas] Terminal de TESTE criado à frente do jogador. " +
                      "Olhe para o painel azul, pressione E e confirme o reparo de suporte de vida. " +
                      "Ele é temporário e não altera os modelos da Base Lunar.");
        }
    }
}
