using System;
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

        [Header("Se vazio, o jogador e o HUD serão encontrados na cena")]
        [SerializeField] private Transform player;
        [SerializeField] private Transform innerAirlock;
        [SerializeField] private Transform outerAirlock;
        [SerializeField] private float returnRadius = 7f;
        [SerializeField] private bool loadExistingSaveOnPlay;

        private float hudDelay;
        public Transform PlayerTransform { get { return player; } }
        public string SavePath { get { return Path.Combine(Application.persistentDataPath, "astrolab_nicholas_save.json"); } }

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
            Engine = new MissionEngine();
            Engine.EventRecorded += OnRecord;
            Engine.StateChanged += OnStateChange;
            if (loadExistingSaveOnPlay) LoadGame();
        }

        private void OnDestroy()
        {
            if (Engine != null)
            {
                Engine.EventRecorded -= OnRecord;
                Engine.StateChanged -= OnStateChange;
            }
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (Engine == null) return;
            Engine.Tick(Time.deltaTime);
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
        public OperationResult Repair(EquipmentId id) { return Report(Engine.Repair(id)); }
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
            if (!state.IsOutside)
            {
                if (innerAirlock && Vector3.Distance(player.position, innerAirlock.position) > returnRadius)
                    return Report(OperationResult.Fail("Aproxime-se da eclusa para sair."));
                OperationResult result = Engine.StartEva();
                if (result.Success && outerAirlock)
                {
                    CharacterController cc = player.GetComponent<CharacterController>();
                    if (cc) cc.enabled = false;
                    player.position = outerAirlock.position + Vector3.up * 1.2f;
                    if (cc) cc.enabled = true;
                }
                return Report(result);
            }
            if (outerAirlock && Vector3.Distance(player.position, outerAirlock.position) > returnRadius)
                return Report(OperationResult.Fail("Retorne à entrada da eclusa (indicada pela base)."));
            OperationResult back = Engine.FinishEva();
            if (back.Success && innerAirlock)
            {
                CharacterController cc = player.GetComponent<CharacterController>();
                if (cc) cc.enabled = false;
                player.position = innerAirlock.position + Vector3.up * 1.2f;
                if (cc) cc.enabled = true;
            }
            return Report(back);
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
            {
                CharacterController cc = player.GetComponent<CharacterController>();
                if (cc) cc.enabled = false;
                player.position = innerAirlock.position + Vector3.up * 1.2f;
                if (cc) cc.enabled = true;
            }
            if (Engine != null)
            {
                Engine.EventRecorded -= OnRecord;
                Engine.StateChanged -= OnStateChange;
            }
            Engine = new MissionEngine();
            Engine.EventRecorded += OnRecord;
            Engine.StateChanged += OnStateChange;
            Updated?.Invoke();
            Feedback?.Invoke("Nova missão iniciada.");
        }

        public bool SaveGame()
        {
            if (Engine.State.IsOutside) { Feedback?.Invoke("Salve dentro da base para evitar posição inconsistente."); return false; }
            try
            {
                string path = SavePath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, JsonUtility.ToJson(Engine.State, true));
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
                if (data != null && data.IsOutside) throw new InvalidDataException("Save da EVA sem coordenadas do personagem.");
                MissionEngine newEngine = MissionEngine.Restore(data);
                if (Engine != null)
                {
                    Engine.EventRecorded -= OnRecord;
                    Engine.StateChanged -= OnStateChange;
                }
                Engine = newEngine;
                Engine.EventRecorded += OnRecord;
                Engine.StateChanged += OnStateChange;
                Updated?.Invoke();
                Feedback?.Invoke("Partida carregada. Posição do jogador não é persistida neste MVP.");
                return true;
            }
            catch (Exception error) { Debug.LogWarning("[AstroLab] Save inválido: " + error.Message); Feedback?.Invoke("Não foi possível recuperar o save."); return false; }
        }
    }
}
