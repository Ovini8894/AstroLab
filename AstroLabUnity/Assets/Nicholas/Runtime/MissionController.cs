using System;
using System.Collections.Generic;
using System.IO;
using AstroLab.Nicholas.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AstroLab.Nicholas
{
    /// <summary>Único ponto de entrada da camada Unity; lógica permanece no MissionEngine.</summary>
    [DefaultExecutionOrder(-100)]
    public sealed class MissionController : MonoBehaviour
    {
        public static MissionController Instance { get; private set; }
        public MissionEngine Engine { get; private set; }
        public event Action Updated;
        public event Action<string> Feedback;
        public event Action GameRestarted;

        [Header("Se vazio, o jogador e o HUD serão encontrados na cena")]
        [SerializeField] private Transform player;
        [SerializeField] private Transform innerAirlock;
        [SerializeField] private Transform outerAirlock;
        [SerializeField] private BaseConsertada baseConsertada;
        [SerializeField] private RadiationPanelVisual radiationPanel;
        [SerializeField] private float returnRadius = 7f;
        [SerializeField] private bool loadExistingSaveOnPlay;

        private static readonly Dictionary<EquipmentId, string[]> VisualPiecesMap = AstroLab.Gameplay.MissionPieceBindings.CreateMap();

        private float hudDelay;
        private MissionOverlay overlay;
        private MonoBehaviour helmet;
        public Transform PlayerTransform { get { return player; } }
        public Transform InnerAirlock { get { return innerAirlock; } }
        public Transform OuterAirlock { get { return outerAirlock; } }
        public float ReturnRadius { get { return returnRadius; } }
        public MissionTargetMarkers TargetMarkers { get { return targetMarkers; } }
        public string SavePath { get { return Path.Combine(Application.persistentDataPath, "astrolab_nicholas_save.json"); } }

        private GameObject airlockBeacon;
        private Transform airlockBeaconLabel;
        private MissionTargetMarkers targetMarkers;
        private MissionRepairEffects repairEffects;
        private bool suppressPieceEffects;
        private readonly Dictionary<string, int> lastRepairEffectFrame = new Dictionary<string, int>();

        private void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (!player)
            {
                NicholasFirstPersonController own = FindFirstObjectByType<NicholasFirstPersonController>();
                if (own) player = own.transform;
                if (!player)
                {
                    CharacterController firstController = FindFirstObjectByType<CharacterController>();
                    if (firstController) player = firstController.transform;
                }
            }
            if (!baseConsertada) baseConsertada = FindFirstObjectByType<BaseConsertada>();
            if (!radiationPanel) radiationPanel = FindFirstObjectByType<RadiationPanelVisual>();
            if (!innerAirlock) { GameObject go = GameObject.Find("INT_Eclusa"); if (go) innerAirlock = go.transform; }
            if (!outerAirlock) { GameObject go = GameObject.Find("WP_Rampa_Eclusa"); if (go) outerAirlock = go.transform; }
            Engine = new MissionEngine();
            Engine.EventRecorded += OnRecord;
            Engine.StateChanged += OnStateChange;
            if (loadExistingSaveOnPlay) LoadGame();
        }

        private void OnDestroy()
        {
            if (baseConsertada) baseConsertada.aoConsertar.RemoveListener(OnBasePieceRepaired);
            if (Engine != null)
            {
                Engine.EventRecorded -= OnRecord;
                Engine.StateChanged -= OnStateChange;
            }
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            overlay = FindFirstObjectByType<MissionOverlay>();
            helmet = OptionalTeamComponents.FindByName("HudCapacete");
            targetMarkers = GetComponent<MissionTargetMarkers>() ?? gameObject.AddComponent<MissionTargetMarkers>();
            repairEffects = GetComponent<MissionRepairEffects>() ?? gameObject.AddComponent<MissionRepairEffects>();
            if (baseConsertada)
            {
                baseConsertada.aoConsertar.RemoveListener(OnBasePieceRepaired);
                baseConsertada.aoConsertar.AddListener(OnBasePieceRepaired);
            }
            EnsureOkTargets();
            SyncVisualBase(false);
            EnsureAirlockBeacon();
            UpdateAirlockBeacon();
        }

        private void Update()
        {
            if (Engine == null) return;
            if (!overlay) overlay = FindFirstObjectByType<MissionOverlay>();
            if (!helmet) helmet = OptionalTeamComponents.FindByName("HudCapacete");
            bool modalOpen = (overlay && overlay.IsModalOpen) || OptionalTeamComponents.IsHelmetModalOpen(helmet);
            if (!modalOpen) Engine.Tick(Time.deltaTime);
            UpdateAirlockBeacon();
            hudDelay += Time.deltaTime;
            if (hudDelay > 0.15f) { hudDelay = 0f; Updated?.Invoke(); }
            if (Keyboard.current != null && Keyboard.current.f5Key.wasPressedThisFrame) SaveGame();
            if (Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame) LoadGame();
        }

        private void OnRecord(MissionLogEntry item)
        {
            if (item.Type == MissionEventType.Warning || item.Type == MissionEventType.Failure || item.Type == MissionEventType.Success)
                Feedback?.Invoke(item.Text);
            Debug.Log("[AstroLab][Nicholas][T" + item.Turn + "][" + item.Type + "] " + item.Text);
        }
        private void OnStateChange() { Updated?.Invoke(); }
        public OperationResult Repair(EquipmentId id)
        {
            OperationResult result = Engine.Repair(id);
            if (result.Success) SyncVisualBase(true, id);
            return Report(result);
        }
        public OperationResult Power(EquipmentId id, bool value) { return Report(Engine.SetPowered(id, value)); }
        public OperationResult Recharge() { return Report(Engine.RechargeSuit()); }
        public OperationResult AdvanceTurn() { return Report(Engine.AdvanceTurn()); }
        public OperationResult RegisterArtifact(ArtifactId id, string answer = null) { return Report(Engine.RegisterArtifact(id, answer)); }

        private OperationResult Report(OperationResult result)
        {
            Feedback?.Invoke(result.Message);
            return result;
        }

        public OperationResult ChangeEva()
        {
            if (!player) return Report(OperationResult.Fail("Jogador não conectado ao MissionController."));
            MissionSnapshot state = Engine.State;
            Transform viewer = Camera.main ? Camera.main.transform : player;
            if (!state.IsOutside)
            {
                if (innerAirlock)
                {
                    float dist = Vector3.Distance(player.position, innerAirlock.position);
                    if (dist > returnRadius)
                    {
                        int metros = Mathf.RoundToInt(dist);
                        string dir = DescribeDirection(viewer, innerAirlock.position);
                        return Report(OperationResult.Fail("A eclusa está a " + metros + " m, " + dir + ". Aproxime-se para sair."));
                    }
                }
                OperationResult result = Engine.StartEva();
                if (!result.Success) return Report(result);
                if (outerAirlock) TeleportPlayer(outerAirlock.position + Vector3.up * 1.2f);
                UpdateAirlockBeacon();
                Feedback?.Invoke("Ciclo da eclusa concluído - Modo EVA ativo");
                return result;
            }
            if (outerAirlock)
            {
                float dist = Vector3.Distance(player.position, outerAirlock.position);
                if (dist > returnRadius)
                {
                    int metros = Mathf.RoundToInt(dist);
                    string dir = DescribeDirection(viewer, outerAirlock.position);
                    return Report(OperationResult.Fail("A eclusa está a " + metros + " m, " + dir + "."));
                }
            }
            OperationResult back = Engine.FinishEva();
            if (!back.Success) return Report(back);
            if (innerAirlock) TeleportPlayer(innerAirlock.position + Vector3.up * 1.2f);
            UpdateAirlockBeacon();
            Feedback?.Invoke("Eclusa pressurizada - Interior do Habitat");
            return back;
        }

        private static string DescribeDirection(Transform viewer, Vector3 targetPos)
        {
            Vector3 toTarget = targetPos - viewer.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.01f) return "bem perto de você";
            toTarget.Normalize();
            Vector3 forward = viewer.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();
            Vector3 right = new Vector3(forward.z, 0f, -forward.x);
            float fwdDot = Vector3.Dot(forward, toTarget);
            float rightDot = Vector3.Dot(right, toTarget);
            if (Mathf.Abs(fwdDot) >= Mathf.Abs(rightDot) * 1.4f)
                return fwdDot >= 0f ? "à sua frente" : "atrás de você";
            if (Mathf.Abs(rightDot) >= Mathf.Abs(fwdDot) * 1.4f)
                return rightDot >= 0f ? "à sua direita" : "à sua esquerda";
            string fb = fwdDot >= 0f ? "à frente" : "atrás";
            string lr = rightDot >= 0f ? "à direita" : "à esquerda";
            return fb + " e " + lr;
        }

        private void EnsureAirlockBeacon()
        {
            if (airlockBeacon || !outerAirlock) return;
            airlockBeacon = new GameObject("Nicholas_Marcador_Eclusa");
            airlockBeacon.transform.position = outerAirlock.position;

            GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = "Baliza_Eclusa";
            pillar.transform.SetParent(airlockBeacon.transform, false);
            pillar.transform.localPosition = new Vector3(0f, 1.35f, 0f);
            pillar.transform.localScale = new Vector3(0.10f, 1.35f, 0.10f);
            Collider col = pillar.GetComponent<Collider>();
            if (col) Destroy(col);

            Renderer r = pillar.GetComponent<Renderer>();
            if (r)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (sh)
                {
                    Material m = new Material(sh);
                    m.color = new Color(0.18f, 0.90f, 0.95f);
                    r.sharedMaterial = m;
                }
            }

            GameObject labelGo = new GameObject("Rotulo_Eclusa");
            labelGo.transform.SetParent(airlockBeacon.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 3.1f, 0f);
            TextMesh tm = labelGo.AddComponent<TextMesh>();
            tm.text = "ECLUSA\n[L] ENTRAR";
            tm.alignment = TextAlignment.Center;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.characterSize = 0.09f;
            tm.fontSize = 36;
            tm.color = new Color(0.35f, 0.95f, 1f);
            airlockBeaconLabel = labelGo.transform;
        }

        private void UpdateAirlockBeacon()
        {
            if (!airlockBeacon) return;
            bool show = Engine != null && Engine.State.IsOutside && Engine.State.Outcome == MissionOutcome.Running;
            if (airlockBeacon.activeSelf != show) airlockBeacon.SetActive(show);
            if (show && airlockBeaconLabel && Camera.main)
            {
                Vector3 dir = airlockBeaconLabel.position - Camera.main.transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.001f)
                    airlockBeaconLabel.rotation = Quaternion.LookRotation(dir);
            }
        }

        private void TeleportPlayer(Vector3 position, float? rotY = null)
        {
            if (!player) return;
            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc) cc.enabled = false;
            player.position = position;
            if (rotY.HasValue)
            {
                Vector3 euler = player.eulerAngles;
                player.eulerAngles = new Vector3(euler.x, rotY.Value, euler.z);
            }
            if (cc) cc.enabled = true;
        }

        public void Configure(Transform playerTransform, Transform internalMarker, Transform externalMarker)
        {
            player = playerTransform;
            innerAirlock = internalMarker;
            outerAirlock = externalMarker;
        }

        public void NewGame()
        {
            if (Engine != null && Engine.State.IsOutside && player && innerAirlock)
                TeleportPlayer(innerAirlock.position + Vector3.up * 1.2f);
            if (Engine != null)
            {
                Engine.EventRecorded -= OnRecord;
                Engine.StateChanged -= OnStateChange;
            }
            Engine = new MissionEngine();
            Engine.EventRecorded += OnRecord;
            Engine.StateChanged += OnStateChange;
            if (repairEffects) repairEffects.StopAllEffects();
            SyncVisualBase(false);
            GameRestarted?.Invoke();
            Updated?.Invoke();
            Feedback?.Invoke("Nova missão iniciada.");
        }

        public bool SaveGame()
        {
            try
            {
                MissionSnapshot snapshot = Engine.State;
                if (player)
                {
                    snapshot.HasPlayerPose = true;
                    snapshot.PlayerPosX = player.position.x;
                    snapshot.PlayerPosY = player.position.y;
                    snapshot.PlayerPosZ = player.position.z;
                    snapshot.PlayerRotY = player.eulerAngles.y;
                }
                string path = SavePath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, JsonUtility.ToJson(snapshot, true));
                Feedback?.Invoke("Partida salva em " + path);
                return true;
            }
            catch (Exception error) { Debug.LogError("[AstroLab] Erro no save: " + error); Feedback?.Invoke("Erro ao salvar partida."); return false; }
        }

        public bool LoadGame()
        {
            try
            {
                if (!File.Exists(SavePath)) return false;
                MissionSnapshot data = JsonUtility.FromJson<MissionSnapshot>(File.ReadAllText(SavePath));
                MissionEngine newEngine = MissionEngine.Restore(data);
                if (Engine != null)
                {
                    Engine.EventRecorded -= OnRecord;
                    Engine.StateChanged -= OnStateChange;
                }
                Engine = newEngine;
                Engine.EventRecorded += OnRecord;
                Engine.StateChanged += OnStateChange;
                if (data != null && data.HasPlayerPose && player)
                    TeleportPlayer(new Vector3(data.PlayerPosX, data.PlayerPosY, data.PlayerPosZ), data.PlayerRotY);
                if (repairEffects) repairEffects.StopAllEffects();
                SyncVisualBase(false);
                Updated?.Invoke();
                Feedback?.Invoke("Partida carregada.");
                return true;
            }
            catch (Exception error) { Debug.LogWarning("[AstroLab] Save inválido: " + error.Message); Feedback?.Invoke("Não foi possível recuperar o save."); return false; }
        }

        private void EnsureOkTargets()
        {
            if (!baseConsertada) return;
            foreach (KeyValuePair<EquipmentId, string[]> entry in VisualPiecesMap)
            {
                foreach (string pieceId in entry.Value)
                {
                    GameObject okObj = baseConsertada.ObjetoConsertado(pieceId);
                    if (!okObj) continue;
                    if (!okObj.GetComponent<Collider>())
                    {
                        MeshFilter mf = okObj.GetComponent<MeshFilter>();
                        if (mf && mf.sharedMesh)
                        {
                            MeshCollider mc = okObj.AddComponent<MeshCollider>();
                            mc.sharedMesh = mf.sharedMesh;
                        }
                    }
                    MissionEquipmentTarget target = okObj.GetComponent<MissionEquipmentTarget>();
                    if (!target) target = okObj.AddComponent<MissionEquipmentTarget>();
                    target.Configure(entry.Key);
                    if (pieceId == "Porta_SuporteVida" &&
                        !okObj.GetComponent<AstroLab.Gameplay.RestoredSlidingDoor>())
                        okObj.AddComponent<AstroLab.Gameplay.RestoredSlidingDoor>();
                }
            }
        }

        private void SyncVisualBase(bool playRepairEffects = false, EquipmentId? repairedId = null)
        {
            if (Engine == null) return;
            if (!radiationPanel) radiationPanel = FindFirstObjectByType<RadiationPanelVisual>();
            if (radiationPanel)
            {
                radiationPanel.SetRepaired(Engine.IsRepaired(EquipmentId.RadiationShelter));
                if (playRepairEffects && repairedId == EquipmentId.RadiationShelter && repairEffects)
                    repairEffects.PlayAtTarget(radiationPanel.transform, 0.45f);
            }

            if (!baseConsertada) return;
            suppressPieceEffects = !playRepairEffects;
            try
            {
                foreach (KeyValuePair<EquipmentId, string[]> entry in VisualPiecesMap)
                {
                    bool repaired = Engine.IsRepaired(entry.Key);
                    foreach (string pieceId in entry.Value)
                    {
                        if (repaired) baseConsertada.Consertar(pieceId);
                        else baseConsertada.Quebrar(pieceId);
                    }
                }
                baseConsertada.LimparDetritos(Engine.IsRepaired(EquipmentId.RadiationShelter));
            }
            finally
            {
                suppressPieceEffects = false;
            }
            if (targetMarkers) targetMarkers.RefreshTargets();
        }

        private void OnBasePieceRepaired(string pieceId)
        {
            if (suppressPieceEffects || !repairEffects || !baseConsertada || string.IsNullOrEmpty(pieceId)) return;

            string groupKey = AstroLab.Gameplay.MissionPieceBindings.TryGetEquipment(pieceId, out EquipmentId group)
                ? group.ToString() : pieceId;

            int frame = Time.frameCount;
            if (lastRepairEffectFrame.TryGetValue(groupKey, out int lastFrame) && lastFrame == frame)
                return;
            lastRepairEffectFrame[groupKey] = frame;

            if (groupKey == "SolarPanels")
            {
                List<Transform> panels = new List<Transform>();
                Vector3 sum = Vector3.zero;
                foreach (string solarId in VisualPiecesMap[EquipmentId.SolarPanels])
                {
                    if (!solarId.StartsWith("Painel_", StringComparison.Ordinal)) continue;
                    GameObject okPanel = baseConsertada.ObjetoConsertado(solarId);
                    if (!okPanel) continue;
                    panels.Add(okPanel.transform);
                    Renderer r = okPanel.GetComponentInChildren<Renderer>(false);
                    sum += r ? r.bounds.center : okPanel.transform.position;
                }
                if (panels.Count > 0)
                    repairEffects.PlayForGroup((sum / panels.Count) + Vector3.up * 0.4f, panels.ToArray(), 1.6f);
                return;
            }

            GameObject preferredOk = null;
            if (groupKey == "LifeSupport") preferredOk = baseConsertada.ObjetoConsertado("Gerador_O2");
            else if (groupKey == "Greenhouse") preferredOk = baseConsertada.ObjetoConsertado("Estufa_Painel");
            else if (groupKey == "Communications") preferredOk = baseConsertada.ObjetoConsertado("Console_Controle") ?? baseConsertada.ObjetoConsertado("Antena_Prato");

            if (!preferredOk) preferredOk = baseConsertada.ObjetoConsertado(pieceId);
            if (preferredOk) repairEffects.PlayAtTarget(preferredOk.transform, 0.45f);
        }
    }
}
