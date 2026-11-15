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
        private enum TutorialStep
        {
            Intro,
            LookSolarIcon,
            OpenPanelF2,
            ExitAirlockL,
            RepairSolar,
            ReturnAirlock,
            PowerLifeSupport,
            Done
        }

        private MissionController mission;
        private MissionPlayerInteraction interaction;
        private NasaImageLibraryClient nasa;
        private MonoBehaviour movement;
        private MonoBehaviour scanner;
        private MonoBehaviour teamHelmet;
        private System.Reflection.MethodInfo hideHelmetExploreHint;
        private bool movementWasEnabled, scannerWasEnabled;
        private bool panelOpen = false;
        private bool modalActive;
        private bool openedFromPanel;
        private EquipmentId? inspectingEquipment;
        private bool repairInspectionFromWorld;
        private ArtifactId? inspectingArtifact;
        private ArtifactId? pendingArtifact;
        private bool showReport;
        private float feedbackTimeout;
        private string feedbackMessage = string.Empty;
        private bool hintF2Done, hintLDone, hintEDone;
        private MissionEngine trackedEngine;
        private TutorialStep tutorialStep = TutorialStep.Intro;
        private bool tutorialSkipped;
        private bool tutorialCompleted;
        private bool step4OpenedSolar;
        private float solarLookHoldTimer;
        private float solarLookDirectionSign;
        private bool solarIconInFront;
        private NasaMediaRecord nasaRecord;
        private Texture2D thumbnail;
        private Texture2D sheenTexture;
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
            if (teamHelmet)
            {
                hideHelmetExploreHint = teamHelmet.GetType().GetMethod("ConcluirOrientacaoExploracao",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                hideHelmetExploreHint?.Invoke(teamHelmet, null);
            }
            if (mission)
            {
                mission.Feedback += ShowFeedback;
                mission.GameRestarted += ResetTutorial;
                trackedEngine = mission.Engine;
                InitTutorialFromEngine();
            }
            if (nasa) nasa.RecordUpdated += NasaUpdated;
            // Inicie com painel fechado para testar WASD imediatamente.
            panelOpen = false;
        }
        private void OnDestroy()
        {
            if (mission)
            {
                mission.Feedback -= ShowFeedback;
                mission.GameRestarted -= ResetTutorial;
            }
            if (nasa) nasa.RecordUpdated -= NasaUpdated;
            if (sheenTexture) { Destroy(sheenTexture); sheenTexture = null; }
        }
        private void Update()
        {
            if (mission && mission.Engine != trackedEngine)
            {
                trackedEngine = mission.Engine;
                SyncTutorialOnEngineSwap();
            }
            UpdateTutorial();
            if (Keyboard.current == null) return;
            if (Keyboard.current.f2Key.wasPressedThisFrame) TogglePanel();
            if (Keyboard.current.lKey.wasPressedThisFrame && !IsModalOpen) hintLDone = true;
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
            hintF2Done = true;
            if (panelOpen) CloseModal();
            else { inspectingEquipment = null; inspectingArtifact = null; showReport = false; openedFromPanel = false; panelOpen = true; SetModalState(true); }
        }
        private void CloseModal()
        {
            panelOpen = false; inspectingEquipment = null; inspectingArtifact = null; showReport = false; openedFromPanel = false;
            if (thumbnail) { Destroy(thumbnail); thumbnail = null; }
            SetModalState(false);
        }
        private void GoBackToPanel()
        {
            inspectingEquipment = null; inspectingArtifact = null; showReport = false; openedFromPanel = false;
            if (thumbnail) { Destroy(thumbnail); thumbnail = null; }
            panelOpen = true;
            SetModalState(true);
        }
        public void OpenEquipment(EquipmentId id, bool fromWorld = false)
        {
            if (fromWorld)
            {
                hintEDone = true;
                if (id == EquipmentId.SolarPanels && tutorialStep == TutorialStep.RepairSolar)
                    step4OpenedSolar = true;
            }
            openedFromPanel = !fromWorld && panelOpen;
            panelOpen = false; showReport = false; inspectingArtifact = null; inspectingEquipment = id;
            repairInspectionFromWorld = fromWorld;
            SetModalState(true);
        }
        public void SetPendingArtifact(ArtifactId id) { pendingArtifact = id; }
        public void OpenArtifact(ArtifactId id)
        {
            hintEDone = true;
            openedFromPanel = panelOpen;
            panelOpen = false; showReport = false; inspectingEquipment = null; inspectingArtifact = id;
            nasaRecord = nasa ? nasa.GetOrStart(id) : null;
            if (thumbnail) { Destroy(thumbnail); thumbnail = null; }
            StartThumbnailRequest();
            SetModalState(true);
        }
        private void OpenReport() { openedFromPanel = panelOpen; panelOpen = false; inspectingEquipment = null; inspectingArtifact = null; showReport = true; SetModalState(true); }

        private void PrepareStyles()
        {
            if (sheenTexture == null)
            {
                sheenTexture = new Texture2D(32, 1, TextureFormat.RGBA32, false);
                sheenTexture.wrapMode = TextureWrapMode.Clamp;
                for (int x = 0; x < 32; x++)
                {
                    float norm = (x / 31f) * 2f - 1f; // -1 a +1
                    float alpha = Mathf.Max(0f, Mathf.Cos(norm * Mathf.PI * 0.5f));
                    sheenTexture.SetPixel(x, 0, new Color(1f, 1f, 1f, alpha * 0.35f));
                }
                sheenTexture.Apply();
            }
            if (heading != null) return;
            heading = new GUIStyle(GUI.skin.label) { fontSize = 21, fontStyle = FontStyle.Bold, wordWrap = true };
            heading.normal.textColor = new Color(0.35f, 0.94f, 0.92f);
            body = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
            body.normal.textColor = Color.white;
            subtitle = new GUIStyle(body) { fontSize = 12 };
            subtitle.normal.textColor = new Color(0.82f, 0.9f, 0.93f);
        }

        private bool InteractiveButton(string text, params GUILayoutOption[] options)
        {
            bool clicked = GUILayout.Button(text, options);
            if (Event.current.type == EventType.Repaint && sheenTexture)
            {
                Rect rect = GUILayoutUtility.GetLastRect();
                float ciclo = 2.0f;
                float duracao = 0.55f;
                float t = (Time.unscaledTime % ciclo);
                if (t <= duracao)
                {
                    float progresso = t / duracao;
                    float larguraBrilho = rect.width * 0.35f;
                    float minX = rect.x - larguraBrilho;
                    float maxX = rect.x + rect.width;
                    float posX = Mathf.Lerp(minX, maxX, progresso);

                    Rect brilhoRect = new Rect(posX, rect.y, larguraBrilho, rect.height);
                    // Intersecta com o botão para não vazar
                    float x1 = Mathf.Max(rect.x, brilhoRect.x);
                    float x2 = Mathf.Min(rect.xMax, brilhoRect.xMax);
                    if (x2 > x1)
                    {
                        float u1 = (x1 - brilhoRect.x) / brilhoRect.width;
                        float u2 = (x2 - brilhoRect.x) / brilhoRect.width;
                        GUI.DrawTextureWithTexCoords(new Rect(x1, rect.y, x2 - x1, rect.height), sheenTexture, new Rect(u1, 0, u2 - u1, 1));
                    }
                }
            }
            return clicked;
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
                GUI.Box(new Rect(340, 12, 595, 42), feedbackMessage);
            DrawTutorialHud(s);
            if (!IsModalOpen)
            {
                // Se o HUD do capacete estiver ativo, ele já renderiza a interação na uGUI
                if (!teamHelmet || !teamHelmet.isActiveAndEnabled)
                {
                    string p = interaction ? interaction.CurrentPrompt : string.Empty;
                    if (!string.IsNullOrEmpty(p)) GUI.Box(new Rect(450, 622, 385, 45), p);
                    string guidance = BuildActiveGuidance();
                    if (!string.IsNullOrEmpty(guidance)) GUI.Box(new Rect(290, 673, 700, 32), guidance);
                }
            }
            if (IsModalOpen) DrawModal(s);
        }
        private string BuildActiveGuidance()
        {
            if (IsTutorialActive() || tutorialSkipped || tutorialCompleted) return string.Empty;
            string text = string.Empty;
            if (!hintF2Done) text = "F2: abrir painel de sistemas";
            if (!hintLDone) text = string.IsNullOrEmpty(text) ? "L: usar eclusa (entrar/sair)" : text + "  •  L: usar eclusa (entrar/sair)";
            if (!hintEDone) text = string.IsNullOrEmpty(text) ? "E: inspecionar alvo na mira" : text + "  •  E: inspecionar alvo na mira";
            return text;
        }

        private bool IsTutorialActive()
        {
            return mission != null && mission.Engine != null &&
                   !tutorialSkipped && !tutorialCompleted &&
                   mission.Engine.State.Outcome == MissionOutcome.Running &&
                   mission.Engine.State.Turn == 1;
        }

        private void ResetTutorial()
        {
            tutorialSkipped = false;
            tutorialCompleted = false;
            step4OpenedSolar = false;
            solarLookHoldTimer = 0f;
            tutorialStep = TutorialStep.Intro;
            InitTutorialFromEngine();
        }

        private void InitTutorialFromEngine()
        {
            if (!mission || mission.Engine == null) return;
            MissionSnapshot s = mission.Engine.State;
            if (s.Outcome != MissionOutcome.Running || s.Turn > 1)
            {
                FinishTutorial(false);
                return;
            }
            if (mission.Engine.IsRepaired(EquipmentId.SolarPanels) && mission.Engine.IsPowered(EquipmentId.LifeSupport))
            {
                FinishTutorial(false);
                return;
            }
            SyncTutorialCatchUp(s);
            SyncTutorialMarker();
        }

        private void SyncTutorialOnEngineSwap()
        {
            if (tutorialSkipped)
            {
                SyncTutorialMarker();
                return;
            }
            tutorialCompleted = false;
            step4OpenedSolar = false;
            solarLookHoldTimer = 0f;
            if (tutorialStep == TutorialStep.Done) tutorialStep = TutorialStep.Intro;
            InitTutorialFromEngine();
        }

        private void SyncTutorialCatchUp(MissionSnapshot s)
        {
            if (mission.Engine.IsRepaired(EquipmentId.SolarPanels))
                tutorialStep = s.IsOutside ? TutorialStep.ReturnAirlock : TutorialStep.PowerLifeSupport;
            else if (s.IsOutside)
                tutorialStep = TutorialStep.RepairSolar;
        }

        private void SkipTutorial()
        {
            tutorialSkipped = true;
            tutorialCompleted = true;
            tutorialStep = TutorialStep.Done;
            solarLookHoldTimer = 0f;
            if (mission && mission.TargetMarkers) mission.TargetMarkers.SetTutorialTarget(null);
            hideHelmetExploreHint?.Invoke(teamHelmet, null);
        }

        private void FinishTutorial(bool showCelebration)
        {
            tutorialCompleted = true;
            tutorialStep = TutorialStep.Done;
            solarLookHoldTimer = 0f;
            if (mission && mission.TargetMarkers) mission.TargetMarkers.SetTutorialTarget(null);
            if (showCelebration) ShowFeedback("Muito bem! Você completou o guia do Turno 1!");
        }

        private void UpdateTutorial()
        {
            if (!mission || mission.Engine == null || tutorialSkipped || tutorialCompleted) return;
            MissionSnapshot s = mission.Engine.State;
            if (s.Outcome != MissionOutcome.Running || s.Turn > 1)
            {
                FinishTutorial(false);
                return;
            }

            if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame)
            {
                SkipTutorial();
                return;
            }

            switch (tutorialStep)
            {
                case TutorialStep.Intro:
                    if (Keyboard.current != null &&
                        (Keyboard.current.enterKey.wasPressedThisFrame ||
                         Keyboard.current.numpadEnterKey.wasPressedThisFrame ||
                         Keyboard.current.gKey.wasPressedThisFrame))
                    {
                        tutorialStep = TutorialStep.LookSolarIcon;
                        solarLookHoldTimer = 0f;
                        SyncTutorialCatchUp(s);
                    }
                    break;

                case TutorialStep.LookSolarIcon:
                    if (mission.Engine.IsRepaired(EquipmentId.SolarPanels) || s.IsOutside)
                    {
                        SyncTutorialCatchUp(s);
                        break;
                    }
                    if (CheckLookingAtSolarMarker(out solarIconInFront, out solarLookDirectionSign))
                    {
                        solarLookHoldTimer += Time.unscaledDeltaTime;
                        if (solarLookHoldTimer >= 0.25f)
                            tutorialStep = TutorialStep.OpenPanelF2;
                    }
                    else
                    {
                        solarLookHoldTimer = Mathf.Max(0f, solarLookHoldTimer - Time.unscaledDeltaTime);
                    }
                    break;

                case TutorialStep.OpenPanelF2:
                    if (mission.Engine.IsRepaired(EquipmentId.SolarPanels) || s.IsOutside)
                        SyncTutorialCatchUp(s);
                    else if (panelOpen)
                        tutorialStep = TutorialStep.ExitAirlockL;
                    break;

                case TutorialStep.ExitAirlockL:
                    if (mission.Engine.IsRepaired(EquipmentId.SolarPanels))
                        SyncTutorialCatchUp(s);
                    else if (s.IsOutside)
                        tutorialStep = TutorialStep.RepairSolar;
                    break;

                case TutorialStep.RepairSolar:
                    if (mission.Engine.IsRepaired(EquipmentId.SolarPanels))
                        tutorialStep = s.IsOutside ? TutorialStep.ReturnAirlock : TutorialStep.PowerLifeSupport;
                    break;

                case TutorialStep.ReturnAirlock:
                    if (!s.IsOutside)
                        tutorialStep = TutorialStep.PowerLifeSupport;
                    break;

                case TutorialStep.PowerLifeSupport:
                    if (mission.Engine.IsPowered(EquipmentId.LifeSupport))
                        FinishTutorial(true);
                    break;
            }

            SyncTutorialMarker();
        }

        private void SyncTutorialMarker()
        {
            if (!mission || !mission.TargetMarkers) return;
            if (!IsTutorialActive())
            {
                mission.TargetMarkers.SetTutorialTarget(null);
                return;
            }
            if (tutorialStep == TutorialStep.LookSolarIcon || tutorialStep == TutorialStep.RepairSolar)
                mission.TargetMarkers.SetTutorialTarget(EquipmentId.SolarPanels);
            else if (tutorialStep == TutorialStep.PowerLifeSupport)
                mission.TargetMarkers.SetTutorialTarget(EquipmentId.LifeSupport);
            else
                mission.TargetMarkers.SetTutorialTarget(null);
        }

        private bool CheckLookingAtSolarMarker(out bool inFront, out float horizontalSign)
        {
            inFront = false;
            horizontalSign = 0f;
            if (!mission || !mission.TargetMarkers) return false;
            if (!mission.TargetMarkers.TryGetEquipmentMarkerPosition(EquipmentId.SolarPanels, out Vector3 solarPos))
                return false;

            Camera cam = Camera.main;
            Transform viewer = cam ? cam.transform : mission.PlayerTransform;
            if (!viewer) return false;

            Vector3 toTarget = (solarPos - viewer.position);
            if (toTarget.sqrMagnitude < 0.01f) return true;
            Vector3 dir = toTarget.normalized;
            float dot = Vector3.Dot(viewer.forward, dir);
            inFront = dot > 0.65f;
            horizontalSign = Vector3.Dot(viewer.right, dir);
            return dot >= 0.88f;
        }

        private string GetTutorialStepMessage()
        {
            switch (tutorialStep)
            {
                case TutorialStep.Intro:
                    return "A base perdeu luz! Quer ajuda para ligar tudo?";
                case TutorialStep.LookSolarIcon:
                    if (solarIconInFront) return "1/6: Olhe para o ícone laranja do painel solar.";
                    string turnDir = solarLookDirectionSign >= 0f ? " (Vire →)" : " (← Vire)";
                    return "1/6: Vire e ache o ícone do painel solar." + turnDir;
                case TutorialStep.OpenPanelF2:
                    return "2/6: Aperte F2 para abrir o painel de controle.";
                case TutorialStep.ExitAirlockL:
                    return IsModalOpen
                        ? "3/6: Feche com Esc e aperte L na eclusa."
                        : "3/6: Vá até a eclusa e aperte L para sair.";
                case TutorialStep.RepairSolar:
                    return step4OpenedSolar
                        ? "4/6: Clique em Consertar ou feche e aperte R."
                        : "4/6: Vá ao painel solar. Aperte E e depois R.";
                case TutorialStep.ReturnAirlock:
                    return "5/6: Volte à eclusa e aperte L para entrar.";
                case TutorialStep.PowerLifeSupport:
                    if (!mission.Engine.IsRepaired(EquipmentId.LifeSupport))
                        return "6/6: Vá ao suporte de vida. Aperte E ou R.";
                    return inspectingEquipment == EquipmentId.LifeSupport
                        ? "6/6: Agora clique no botão Ligar sistema!"
                        : "6/6: Aperte E no suporte de vida e ligue.";
                default:
                    return string.Empty;
            }
        }

        private void DrawTutorialHud(MissionSnapshot s)
        {
            if (!IsTutorialActive()) return;
            string message = GetTutorialStepMessage();
            if (string.IsNullOrEmpty(message)) return;

            GUI.Box(new Rect(335, 62, 610, 72), "");
            GUILayout.BeginArea(new Rect(347, 67, 586, 62));
            GUILayout.BeginHorizontal();
            GUILayout.Label(tutorialStep == TutorialStep.Intro ? "GUIA DO TURNO 1 (Opcional)" : "GUIA DO TURNO 1", subtitle);
            GUILayout.FlexibleSpace();
            if (tutorialStep == TutorialStep.Intro && GUILayout.Button("Começar [Enter]", GUILayout.Height(21)))
            {
                tutorialStep = TutorialStep.LookSolarIcon;
                solarLookHoldTimer = 0f;
                SyncTutorialCatchUp(s);
                SyncTutorialMarker();
            }
            if (GUILayout.Button("Pular [P]", GUILayout.Height(21)))
            {
                SkipTutorial();
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
                return;
            }
            GUILayout.EndHorizontal();
            GUILayout.Label(message, body);
            GUILayout.EndArea();

            // Durante o Passo 1, mostra uma seta na tela caso o ícone R4 do painel solar esteja atrás de paredes internas.
            if (tutorialStep == TutorialStep.LookSolarIcon && Camera.main && mission.TargetMarkers &&
                mission.TargetMarkers.TryGetEquipmentMarkerPosition(EquipmentId.SolarPanels, out Vector3 solarPos))
            {
                Vector3 camPos = Camera.main.transform.position;
                Vector3 toSolar = solarPos - camPos;
                float dist = toSolar.magnitude;
                if (dist > 2.2f)
                {
                    Vector3 dir = toSolar / dist;
                    if (Physics.Linecast(camPos + dir * 0.8f, solarPos - dir * 0.8f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    {
                        Vector3 vp = Camera.main.WorldToViewportPoint(solarPos);
                        if (vp.z > 0f && vp.x > 0.05f && vp.x < 0.95f && vp.y > 0.05f && vp.y < 0.95f)
                        {
                            float guiScale = Mathf.Max(0.5f, Mathf.Min(Screen.width / 1280f, Screen.height / 720f));
                            float guiW = Screen.width / guiScale;
                            float guiH = Screen.height / guiScale;
                            float sx = Mathf.Clamp(vp.x * guiW - 72f, 20f, guiW - 164f);
                            float sy = Mathf.Clamp((1f - vp.y) * guiH - 28f, 145f, guiH - 50f);
                            GUI.Box(new Rect(sx, sy, 144f, 28f), "▲ Painel solar");
                        }
                    }
                }
            }
        }

        private void DrawStatus(MissionSnapshot s)
        {
            GUI.Box(new Rect(10, 8, 315, 128), "");
            GUI.Label(new Rect(22, 13, 300, 28), "BASE LUNAR · TURNO " + s.Turn + "/3", heading);
            GUI.Label(new Rect(22, 48, 290, 24), "O₂ traje " + s.SuitOxygen.ToString("0") + "%   ·   Bateria " + s.SuitBattery.ToString("0") + "%", subtitle);
            GUI.Label(new Rect(22, 72, 290, 24), "Energia base " + mission.Engine.PowerUsed + "/" + mission.Engine.PowerCapacity + "   ·   Peças " + s.SpareParts, subtitle);
            GUI.Label(new Rect(22, 96, 290, 24), "Água " + s.Water + "   ·   Comida " + s.Food + "   ·   Ar " + s.HabitatAir, subtitle);
            GUI.Box(new Rect(1000, 10, 265, 70), "");
            GUI.Label(new Rect(1012, 15, 245, 25), s.IsOutside ? "EVA · ÁREA EXTERNA" : "HABITAT · INTERIOR", subtitle);
            string eclusaDist = (s.IsOutside && mission.PlayerTransform && mission.OuterAirlock)
                ? " · Eclusa: " + Mathf.RoundToInt(Vector3.Distance(mission.PlayerTransform.position, mission.OuterAirlock.position)) + " m"
                : string.Empty;
            GUI.Label(new Rect(1012, 41, 245, 24), s.IsOutside ? "Retorno " + s.EvaSecondsRemaining.ToString("0") + " s" + eclusaDist : "Tempo de ação " + s.RemainingActionTime + "/6", subtitle);
            if (s.Outcome != MissionOutcome.Running)
                GUI.Box(new Rect(400, 120, 480, 45), s.Outcome == MissionOutcome.Success ? "MISSÃO CONCLUÍDA! [F2]" : "MISSÃO FALHOU! [F2]");
        }
        private void DrawModal(MissionSnapshot s)
        {
            GUI.Box(new Rect(320, 140, 640, 500), "");
            GUILayout.BeginArea(new Rect(345, 155, 590, 470));
            GUILayout.BeginHorizontal();
            GUILayout.Label(inspectingEquipment.HasValue ? "INSPEÇÃO TÉCNICA" : inspectingArtifact.HasValue ? "MUSEU APOLLO 15" : showReport ? "RELATÓRIO FINAL" : "CONTROLE DA MISSÃO", heading);
            if (inspectingEquipment.HasValue || inspectingArtifact.HasValue || showReport)
            {
                if (InteractiveButton("« Voltar", GUILayout.Width(100))) { GoBackToPanel(); GUILayout.EndHorizontal(); GUILayout.EndArea(); return; }
            }
            if (InteractiveButton("Fechar [Esc]", GUILayout.Width(120))) { CloseModal(); GUILayout.EndHorizontal(); GUILayout.EndArea(); return; }
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
            GUILayout.Label("Custo do reparo: " + definition.RepairParts + " peças (disponível: " + s.SpareParts + "), " +
                            definition.RepairTime + " tempos (restante no turno: " + s.RemainingActionTime + "/" + MissionRuleSet.ActionMinutesPerTurn + ").", body);
            GUILayout.Label(definition.NeedsEva ? "Local de trabalho: exterior, com traje." : "Local de trabalho: interior do habitat.", subtitle);
            GUILayout.Label("Demanda elétrica quando ligado: " + definition.PowerDemand + " unidades por turno.", subtitle);
            GUILayout.Space(12);
            if (!mission.Engine.IsRepaired(id))
            {
                if (repairInspectionFromWorld)
                {
                    OperationResult canRepair = mission.Engine.CanRepair(id);
                    if (canRepair.Success)
                    {
                        if (InteractiveButton("CONFIRMAR REPARO", GUILayout.Height(39))) mission.Repair(id);
                    }
                    else
                    {
                        Color prev = GUI.contentColor;
                        GUI.contentColor = new Color(1f, 0.72f, 0.45f);
                        GUILayout.Label("Não dá para consertar agora: " + canRepair.Message, body);
                        GUI.contentColor = prev;
                        GUI.enabled = false;
                        GUILayout.Button("CONFIRMAR REPARO (" + canRepair.Message + ")", GUILayout.Height(39));
                        GUI.enabled = true;
                        if (s.RemainingActionTime < definition.RepairTime && HasPendingChecklistItems())
                        {
                            GUILayout.Label("Dica: clique em « Voltar e use AVANÇAR TURNO para ganhar mais tempo.", subtitle);
                        }
                    }
                }
                else GUILayout.Label("Reparo indisponível nesta consulta: use E junto ao objeto.", subtitle);
            }
            else if (definition.PowerDemand > 0)
            {
                if (InteractiveButton(mission.Engine.IsPowered(id) ? "Desligar sistema" : "Ligar sistema", GUILayout.Height(39)))
                    mission.Power(id, !mission.Engine.IsPowered(id));
            }
            else GUILayout.Label("Este sistema passivo já está restaurado.", body);
        }

        private bool HasPendingChecklistItems()
        {
            VictoryCriterion[] checklist = mission.Engine.GetVictoryChecklist();
            for (int i = 0; i < checklist.Length; i++)
                if (!checklist[i].Met) return true;
            return false;
        }
        private void DrawControls(MissionSnapshot s)
        {
            GUILayout.Label("Objetivo: reparar a base para receber a tripulação.", body);
            GUILayout.Label("Turno " + s.Turn + "/" + MissionRuleSet.MaximumTurns +
                            "  ·  Tempo restante: " + s.RemainingActionTime + "/" + MissionRuleSet.ActionMinutesPerTurn +
                            "  ·  Peças disponíveis: " + s.SpareParts, subtitle);
            if (s.RemainingActionTime <= 0 && s.Outcome == MissionOutcome.Running && HasPendingChecklistItems())
            {
                Color prev = GUI.contentColor;
                GUI.contentColor = new Color(1f, 0.80f, 0.35f);
                GUILayout.Label("O tempo deste turno acabou e ainda há metas pendentes. Use AVANÇAR TURNO abaixo para continuar!", body);
                GUI.contentColor = prev;
            }
            if (!mission.Engine.IsRepaired(EquipmentId.LifeSupport))
                GUILayout.Label("ORIENTAÇÃO: aproxime-se dos equipamentos da base (Suporte de Vida, Painéis Solares no exterior, Estufa, Reciclador ou Painel do Abrigo), mire neles e pressione E para inspecionar ou R para reparar.", subtitle);
            GUILayout.Label("Energia: " + mission.Engine.PowerUsed + "/" + mission.Engine.PowerCapacity +
                            ". Suporte (4) + estufa (3) + recarga (2) + rádio (1) = 10. Disponíveis após painel solar: 8.", subtitle);
            GUILayout.Space(8);
            GUILayout.Label("METAS PARA A PRÓXIMA TRIPULAÇÃO (FIM DO TURNO 3)", body);
            VictoryCriterion[] checklist = mission.Engine.GetVictoryChecklist();
            for (int i = 0; i < checklist.Length; i++)
            {
                VictoryCriterion item = checklist[i];
                Color prevColor = GUI.contentColor;
                GUI.contentColor = item.Met ? new Color(0.45f, 1f, 0.58f) : new Color(1f, 0.85f, 0.45f);
                GUILayout.Label((item.Met ? "[OK] " : "[PENDENTE] ") + item.Label, subtitle);
                GUI.contentColor = prevColor;
            }
            GUILayout.Space(10);
            for (int i = 0; i < MissionRuleSet.Definitions.Length; i++)
            {
                EquipmentDefinition d = MissionRuleSet.Definitions[i];
                string stateText = mission.Engine.IsRepaired(d.Id) ? (d.PowerDemand > 0 ? (mission.Engine.IsPowered(d.Id) ? "LIGADO" : "DESLIGADO") : "PRONTO") : "AVARIADO";
                GUILayout.BeginHorizontal();
                GUILayout.Label(d.DisplayName + " — " + stateText, body, GUILayout.Width(360));
                if (InteractiveButton("Consultar", GUILayout.Width(150))) { OpenEquipment(d.Id); GUILayout.EndHorizontal(); return; }
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(12);
            if (InteractiveButton(s.IsOutside ? "RETORNAR PELA ECLUSA [L]" : "INICIAR EVA PELA ECLUSA [L]", GUILayout.Height(34)))
                mission.ChangeEva();
            if (InteractiveButton("Recarregar traje (1 tempo, requer vida + recarga ligadas)", GUILayout.Height(34))) mission.Recharge();
            if (InteractiveButton("AVANÇAR TURNO (atualiza recursos)", GUILayout.Height(34))) mission.AdvanceTurn();
            GUILayout.Space(8);
            if (InteractiveButton("Relatório e histórico", GUILayout.Height(30))) { OpenReport(); return; }
            if (InteractiveButton("Salvar partida [F5]", GUILayout.Height(30))) mission.SaveGame();
            if (InteractiveButton("Carregar partida [F9]", GUILayout.Height(30))) mission.LoadGame();
            if (InteractiveButton("Reiniciar missão (não altera o mapa)", GUILayout.Height(30))) mission.NewGame();
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
                if (InteractiveButton("A — Martelo")) mission.RegisterArtifact(id, "A");
                if (InteractiveButton("B — Pena")) mission.RegisterArtifact(id, "B");
                if (InteractiveButton("C — Os dois juntos")) mission.RegisterArtifact(id, "C");
            }
            else if (InteractiveButton("Registrar descoberta (1 tempo de ação)", GUILayout.Height(37))) mission.RegisterArtifact(id);
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
