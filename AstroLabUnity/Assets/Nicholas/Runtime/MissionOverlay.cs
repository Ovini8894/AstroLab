using AstroLab.Nicholas.Core;
using AstroLab.Nicholas.Integrations;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AstroLab.Nicholas
{
    /// <summary>
    /// UI de MVP executável, sem depender do prefab de outro integrante.
    /// Em produção, botões de UI Toolkit/uGUI podem invocar os mesmos comandos.
    /// F2 = painel; E = interação; L = eclusa; F5/F9 = save/load.
    /// </summary>
    public sealed class MissionOverlay : MonoBehaviour
    {
        private MissionController mission;
        private MissionPlayerInteraction interaction;
        private NasaImageLibraryClient nasa;
        private MonoBehaviour movement;
        private MonoBehaviour scanner;
        private MonoBehaviour teamHelmet;
        private bool movementWasEnabled, scannerWasEnabled;
        private bool panelOpen = false;
        private bool modalActive;
        private EquipmentId? inspectingEquipment;
        private bool repairInspectionFromWorld;
        private ArtifactId? inspectingArtifact;
        private ArtifactId? pendingArtifact;
        private bool showReport;
        private float feedbackTimeout;
        private string feedbackMessage = "Reative os sistemas da base. F2 abre o painel de operação.";
        private NasaMediaRecord nasaRecord;
        private Texture2D thumbnail;
        private GUIStyle heading, body, subtitle;
        private Vector2 scroll;

        public bool IsModalOpen { get { return inspectingEquipment.HasValue || inspectingArtifact.HasValue || showReport || panelOpen; } }

        private void Start()
        {
            mission = MissionController.Instance;
            nasa = FindFirstObjectByType<NasaImageLibraryClient>();
            interaction = FindFirstObjectByType<MissionPlayerInteraction>();
            movement = OptionalTeamComponents.FindByName("JogadorFPS") ??
                       OptionalTeamComponents.FindByName("NicholasFirstPersonController") ??
                       OptionalTeamComponents.FindByName("LaboratorioPlayerController");
            scanner = OptionalTeamComponents.FindByName("ScannerPrimeiraPessoa");
            teamHelmet = OptionalTeamComponents.FindByName("HudCapacete");
            if (mission) mission.Feedback += ShowFeedback;
            if (nasa) nasa.RecordUpdated += NasaUpdated;
            // Inicie com painel fechado para testar WASD imediatamente.
            panelOpen = false;
        }
        private void OnDestroy()
        {
            if (mission) mission.Feedback -= ShowFeedback;
            if (nasa) nasa.RecordUpdated -= NasaUpdated;
        }
        private void Update()
        {
            if (Keyboard.current == null) return;
            if (Keyboard.current.f2Key.wasPressedThisFrame) TogglePanel();
            if (Keyboard.current.escapeKey.wasPressedThisFrame && IsModalOpen) CloseModal();
            if (pendingArtifact.HasValue)
            {
                MonoBehaviour helmet = OptionalTeamComponents.FindByName("HudCapacete");
                if (!OptionalTeamComponents.IsHelmetModalOpen(helmet))
                {
                    ArtifactId value = pendingArtifact.Value;
                    pendingArtifact = null;
                    OpenArtifact(value);
                }
            }
        }

        private void ShowFeedback(string text)
        {
            feedbackMessage = text;
            feedbackTimeout = Time.unscaledTime + 6f;
        }

        private void NasaUpdated(ArtifactId id, NasaMediaRecord record)
        {
            if (!inspectingArtifact.HasValue || inspectingArtifact.Value != id) return;
            nasaRecord = record;
            StartThumbnailRequest();
            ShowFeedback("Dados científicos da NASA recuperados para " + Apollo15Trail.Find(id).Name + ".");
        }
        private void StartThumbnailRequest()
        {
            if (!nasa || nasaRecord == null || string.IsNullOrEmpty(nasaRecord.ThumbnailUrl)) return;
            string requestedArtifact = nasaRecord.Artifact;
            StartCoroutine(nasa.LoadThumbnail(nasaRecord, image =>
            {
                if (!this || !inspectingArtifact.HasValue || inspectingArtifact.Value.ToString() != requestedArtifact)
                { if (image) Destroy(image); return; }
                if (thumbnail) Destroy(thumbnail);
                thumbnail = image;
            }));
        }

        private void SetModalState(bool active)
        {
            if (modalActive == active) return;
            modalActive = active;
            if (active)
            {
                if (movement) { movementWasEnabled = movement.enabled; movement.enabled = false; }
                if (scanner) { scannerWasEnabled = scanner.enabled; scanner.enabled = false; }
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                if (movement) movement.enabled = movementWasEnabled;
                if (scanner) scanner.enabled = scannerWasEnabled;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
        private void TogglePanel()
        {
            if (panelOpen) CloseModal();
            else { inspectingEquipment = null; inspectingArtifact = null; showReport = false; panelOpen = true; SetModalState(true); }
        }
        private void CloseModal()
        {
            panelOpen = false; inspectingEquipment = null; inspectingArtifact = null; showReport = false;
            if (thumbnail) { Destroy(thumbnail); thumbnail = null; }
            SetModalState(false);
        }
        public void OpenEquipment(EquipmentId id, bool fromWorld = false)
        {
            panelOpen = false; showReport = false; inspectingArtifact = null; inspectingEquipment = id;
            repairInspectionFromWorld = fromWorld;
            SetModalState(true);
        }
        public void SetPendingArtifact(ArtifactId id) { pendingArtifact = id; }
        public void OpenArtifact(ArtifactId id)
        {
            panelOpen = false; showReport = false; inspectingEquipment = null; inspectingArtifact = id;
            nasaRecord = nasa ? nasa.GetOrStart(id) : null;
            if (thumbnail) { Destroy(thumbnail); thumbnail = null; }
            StartThumbnailRequest();
            SetModalState(true);
        }
        private void OpenReport() { panelOpen = false; inspectingEquipment = null; inspectingArtifact = null; showReport = true; SetModalState(true); }

        private void PrepareStyles()
        {
            if (heading != null) return;
            heading = new GUIStyle(GUI.skin.label) { fontSize = 21, fontStyle = FontStyle.Bold, wordWrap = true };
            heading.normal.textColor = new Color(0.35f, 0.94f, 0.92f);
            body = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
            body.normal.textColor = Color.white;
            subtitle = new GUIStyle(body) { fontSize = 12 };
            subtitle.normal.textColor = new Color(0.82f, 0.9f, 0.93f);
        }
        private void OnGUI()
        {
            if (mission == null || mission.Engine == null) return;
            PrepareStyles();
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(Mathf.Max(0.5f, scale), Mathf.Max(0.5f, scale), 1f));
            MissionSnapshot s = mission.Engine.State;
            // O HUD do colega já mostra O2, bateria e objetivo. Evite desenhar tudo por cima.
            if (!teamHelmet || !teamHelmet.isActiveAndEnabled) DrawStatus(s);
            if (Time.unscaledTime < feedbackTimeout)
                GUI.Box(new Rect(340, 18, 595, 44), feedbackMessage);
            if (!IsModalOpen)
            {
                string p = interaction ? interaction.CurrentPrompt : string.Empty;
                if (!string.IsNullOrEmpty(p)) GUI.Box(new Rect(450, 622, 385, 45), p);
                GUI.Box(new Rect(340, 673, 595, 32), "F2: sistemas  •  L: eclusa  •  E: alvo marcado  •  F5: salvar  •  F9: carregar");
            }
            if (IsModalOpen) DrawModal(s);
        }
        private void DrawStatus(MissionSnapshot s)
        {
            GUI.Box(new Rect(10, 8, 315, 128), "");
            GUI.Label(new Rect(22, 13, 300, 28), "BASE LUNAR · TURNO " + s.Turn + "/3", heading);
            GUI.Label(new Rect(22, 48, 290, 24), "O₂ traje " + s.SuitOxygen.ToString("0") + "%   ·   Bateria " + s.SuitBattery.ToString("0") + "%", subtitle);
            GUI.Label(new Rect(22, 72, 290, 24), "Energia base " + mission.Engine.PowerUsed + "/" + mission.Engine.PowerCapacity + "   ·   Peças " + s.SpareParts, subtitle);
            GUI.Label(new Rect(22, 96, 290, 24), "Água " + s.Water + "   ·   Comida " + s.Food + "   ·   Ar " + s.HabitatAir, subtitle);
            GUI.Box(new Rect(1040, 10, 225, 70), "");
            GUI.Label(new Rect(1052, 15, 206, 25), s.IsOutside ? "EVA · ÁREA EXTERNA" : "HABITAT · INTERIOR", subtitle);
            GUI.Label(new Rect(1052, 41, 204, 24), s.IsOutside ? "Retorno " + s.EvaSecondsRemaining.ToString("0") + " s" : "Tempo de ação " + s.RemainingActionTime + "/6", subtitle);
            if (s.Outcome != MissionOutcome.Running)
                GUI.Box(new Rect(400, 120, 480, 45), s.Outcome == MissionOutcome.Success ? "MISSÃO CONCLUÍDA! [F2]" : "MISSÃO FALHOU! [F2]");
        }
        private void DrawModal(MissionSnapshot s)
        {
            GUI.Box(new Rect(320, 140, 640, 500), "");
            GUILayout.BeginArea(new Rect(345, 155, 590, 470));
            GUILayout.BeginHorizontal();
            GUILayout.Label(inspectingEquipment.HasValue ? "INSPEÇÃO TÉCNICA" : inspectingArtifact.HasValue ? "MUSEU APOLLO 15" : showReport ? "RELATÓRIO FINAL" : "CONTROLE DA MISSÃO", heading);
            if (GUILayout.Button("Fechar [Esc]", GUILayout.Width(130))) { CloseModal(); GUILayout.EndHorizontal(); GUILayout.EndArea(); return; }
            GUILayout.EndHorizontal();
            scroll = GUILayout.BeginScrollView(scroll);
            if (inspectingEquipment.HasValue) DrawEquipment(s, inspectingEquipment.Value);
            else if (inspectingArtifact.HasValue) DrawArtifact(s, inspectingArtifact.Value);
            else if (showReport) DrawReport(s);
            else DrawControls(s);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
        private void DrawEquipment(MissionSnapshot s, EquipmentId id)
        {
            EquipmentDefinition definition = MissionRuleSet.Find(id);
            GUILayout.Label(definition.DisplayName, heading);
            GUILayout.Label("Condição: " + (mission.Engine.IsRepaired(id) ? "REPARADO" : "AVARIADO"), body);
            if (!repairInspectionFromWorld && !mission.Engine.IsRepaired(id))
                GUILayout.Label("Consulta apenas informativa. Aproxime-se do equipamento (alvo identificado) e pressione E para habilitar o reparo.", subtitle);
            GUILayout.Label("Custo do reparo: " + definition.RepairParts + " peças, " + definition.RepairTime + " tempos de ação.", body);
            GUILayout.Label(definition.NeedsEva ? "Local de trabalho: exterior, com traje." : "Local de trabalho: interior do habitat.", subtitle);
            GUILayout.Label("Demanda elétrica quando ligado: " + definition.PowerDemand + " unidades por turno.", subtitle);
            GUILayout.Space(12);
            if (!mission.Engine.IsRepaired(id))
            {
                if (repairInspectionFromWorld)
                {
                    if (GUILayout.Button("CONFIRMAR REPARO", GUILayout.Height(39))) mission.Repair(id);
                }
                else GUILayout.Label("Reparo indisponível nesta consulta: use E junto ao objeto.", subtitle);
            }
            else if (definition.PowerDemand > 0)
            {
                if (GUILayout.Button(mission.Engine.IsPowered(id) ? "Desligar sistema" : "Ligar sistema", GUILayout.Height(39)))
                    mission.Power(id, !mission.Engine.IsPowered(id));
            }
            else GUILayout.Label("Este sistema passivo já está restaurado.", body);
        }
        private void DrawControls(MissionSnapshot s)
        {
            GUILayout.Label("Objetivo: reparar a base para receber a tripulação.", body);
            GUILayout.Label("COMO TESTAR: procure o pequeno terminal azul TESTE O2 perto de onde apareceu. Mire nele e pressione E. Consulte aqui não repara: apenas mostra informações.", subtitle);
            GUILayout.Label("Energia: " + mission.Engine.PowerUsed + "/" + mission.Engine.PowerCapacity +
                            ". Suporte (4) + estufa (3) + recarga (2) + rádio (1) = 10. Disponíveis após painel solar: 8.", subtitle);
            GUILayout.Space(10);
            for (int i = 0; i < MissionRuleSet.Definitions.Length; i++)
            {
                EquipmentDefinition d = MissionRuleSet.Definitions[i];
                string stateText = mission.Engine.IsRepaired(d.Id) ? (d.PowerDemand > 0 ? (mission.Engine.IsPowered(d.Id) ? "LIGADO" : "DESLIGADO") : "PRONTO") : "AVARIADO";
                GUILayout.BeginHorizontal();
                GUILayout.Label(d.DisplayName + " — " + stateText, body, GUILayout.Width(360));
                if (GUILayout.Button("Consultar", GUILayout.Width(150))) { OpenEquipment(d.Id); GUILayout.EndHorizontal(); return; }
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(12);
            if (GUILayout.Button(s.IsOutside ? "RETORNAR PELA ECLUSA [L]" : "INICIAR EVA PELA ECLUSA [L]", GUILayout.Height(34)))
                mission.ChangeEva();
            if (GUILayout.Button("Recarregar traje (1 tempo, requer vida + recarga ligadas)", GUILayout.Height(34))) mission.Recharge();
            if (GUILayout.Button("AVANÇAR TURNO (atualiza recursos)", GUILayout.Height(34))) mission.AdvanceTurn();
            GUILayout.Space(8);
            if (GUILayout.Button("Relatório e histórico", GUILayout.Height(30))) { OpenReport(); return; }
            if (GUILayout.Button("Salvar partida [F5]", GUILayout.Height(30))) mission.SaveGame();
            if (GUILayout.Button("Carregar partida [F9]", GUILayout.Height(30))) mission.LoadGame();
            if (GUILayout.Button("Reiniciar missão (não altera o mapa)", GUILayout.Height(30))) mission.NewGame();
            GUILayout.Label("Apollo 15: " + s.NextArtifactIndex + "/5 registros. Explore réplicas didáticas no exterior.", subtitle);
        }
        private void DrawArtifact(MissionSnapshot s, ArtifactId id)
        {
            Apollo15Trail.Artifact item = Apollo15Trail.Find(id);
            GUILayout.Label(item.Name, heading);
            GUILayout.Label(item.History, body);
            GUILayout.Space(10);
            GUILayout.Label("Fonte histórica oficial: " + item.OfficialSource, subtitle);
            if (nasaRecord != null)
            {
                if (thumbnail) GUILayout.Box(thumbnail, GUILayout.Height(138));
                GUILayout.Label(nasaRecord.LiveFromNasa ? "NASA Images API — metadados recuperados online:" : "Descrição local (modo offline):", subtitle);
                GUILayout.Label(nasaRecord.Title, body);
                if (!string.IsNullOrEmpty(nasaRecord.Description)) GUILayout.Label(Trim(nasaRecord.Description, 450), subtitle);
                if (!string.IsNullOrEmpty(nasaRecord.NasaId)) GUILayout.Label("NASA ID: " + nasaRecord.NasaId, subtitle);
                if (!string.IsNullOrEmpty(nasaRecord.CatalogUrl)) GUILayout.Label("Biblioteca: " + nasaRecord.CatalogUrl, subtitle);
            }
            GUILayout.Space(8);
            if (s.NextArtifactIndex >= Apollo15Trail.Items.Length)
                GUILayout.Label("A coleção inteira já foi registrada.", body);
            else if (Apollo15Trail.Items[s.NextArtifactIndex].Id != id)
                GUILayout.Label("Não é a próxima pista. Procure: " + Apollo15Trail.Items[s.NextArtifactIndex].Name, body);
            else if (id == ArtifactId.Feather)
            {
                GUILayout.Label("Pergunta: ao soltar martelo e pena na Lua, quem chegou primeiro?", body);
                if (GUILayout.Button("A — Martelo")) mission.RegisterArtifact(id, "A");
                if (GUILayout.Button("B — Pena")) mission.RegisterArtifact(id, "B");
                if (GUILayout.Button("C — Os dois juntos")) mission.RegisterArtifact(id, "C");
            }
            else if (GUILayout.Button("Registrar descoberta (1 tempo de ação)", GUILayout.Height(37))) mission.RegisterArtifact(id);
            GUILayout.Space(7);
            GUILayout.Label("Estas posições são réplicas virtuais para ensino, não o mapa geográfico real da Apollo 15.", subtitle);
        }
        private void DrawReport(MissionSnapshot s)
        {
            GUILayout.Label("Situação: " + s.Outcome + (s.Outcome == MissionOutcome.Running ? " (em andamento)" : ""), heading);
            GUILayout.TextArea(MissionReport.Build(mission.Engine), GUILayout.MinHeight(355));
        }
        private static string Trim(string text, int limit) { return text != null && text.Length > limit ? text.Substring(0, limit) + "…" : text; }
    }
}
