using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using AstroLab.Nicholas;
using AstroLab.Nicholas.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace AstroLab.Asteroids
{
    /// <summary>Optional scene-local science terminal; never changes the shared Blender geometry.</summary>
    public sealed class AsteroidTerminal : MonoBehaviour
    {
        private static readonly Color Ink = new Color32(28, 26, 61, 255);
        private static readonly Color Panel = new Color32(62, 76, 120, 255);
        private static readonly Color Cyan = new Color32(71, 230, 255, 255);
        private static readonly Color Cream = new Color32(244, 233, 212, 255);
        private static readonly Color Gold = new Color32(255, 194, 58, 255);
        public bool IsOpen => screen && screen.activeSelf;
        public AsteroidProgress Progress { get; private set; } = new AsteroidProgress();
        public NeoWsClient Client { get; private set; }
        private string SavePath => Path.Combine(Application.persistentDataPath, "astrolab_asteroid_progress.json");
        private readonly Dictionary<Behaviour, bool> suspended = new Dictionary<Behaviour, bool>();
        private GameObject screen, body, terminalProp, alertRoot;
        private RectTransform list, centre, facts;
        private TMP_Text status, alert, footer, feedback, sizeLabel, progressLabel, skyLabel;
        private RectTransform diameterBar;
        private UnityEngine.UI.Slider slider;
        private AsteroidRecord selected;
        private int page;
        private bool collectionOnly, estimateMoved;
        private AsteroidAnalysis analysis;
        private int analysisRound;
        private double estimateMetres;
        private UnityEngine.UI.Button estimateButton;
        public AsteroidAnalysis CurrentAnalysis => analysis;
        private float previousTimeScale, nextAlert, alertUntil;
        private CursorLockMode previousCursor;
        private bool previousVisible;
        private Transform skyMarker;
        private string alertedId;
        private AsteroidObservationStation station;
        private int alertIndex;
        public int AlertCount { get; private set; }
        public AsteroidObservationStation Station => station;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= OnScene;
            SceneManager.sceneLoaded += OnScene;
            Install(SceneManager.GetActiveScene());
        }
        private static void OnScene(Scene scene, LoadSceneMode mode) { Install(scene); }
        private static void Install(Scene scene)
        {
            if (scene.name != "Lua" && scene.name != "MissaoNicholas" && scene.name != "CapacetePedro" && !FindAnyObjectByType<MissionController>()) return;
            if (FindAnyObjectByType<AsteroidTerminal>()) return;
            new GameObject("RadarDeDescobertas").AddComponent<AsteroidTerminal>();
        }

        private IEnumerator Start()
        {
            Client = gameObject.AddComponent<NeoWsClient>();
            try { if (File.Exists(SavePath)) Progress = JsonUtility.FromJson<AsteroidProgress>(File.ReadAllText(SavePath)) ?? new AsteroidProgress(); }
            catch { Debug.LogWarning("[AstroLab][Asteroids] Progresso ilegível; nenhuma ficha fictícia criada."); }
            yield return null;
            BuildUI(); CreateProp();
            Client.Changed += Reload;
            Reload(); nextAlert = Time.unscaledTime + 5;
        }

        private void Update()
        {
            if (!screen) return;
            Camera cam = Camera.main;

            if (Keyboard.current != null)
            {
                if (IsOpen && Keyboard.current.escapeKey.wasPressedThisFrame) { Close(); return; }
                if (!IsOpen && !OtherModalOpen() && (Keyboard.current.f4Key.wasPressedThisFrame ||
                    (Keyboard.current.eKey.wasPressedThisFrame && LookingAtTerminal(cam)))) Open();
            }
            if (!IsOpen && !OtherModalOpen() && Ready() && Time.unscaledTime >= nextAlert)
            {
                // One notification per available object, then a substantial quiet interval.
                var available=Client.Records.Where(r=>!Progress.Contains(r.Id)).OrderBy(r=>r.Historical).ToList();
                if(available.Count==0)available=Client.Records.ToList();
                var target=available.Count>0?available[alertIndex++%available.Count]:null;
                bool historical=target!=null&&target.Historical;
                if (target != null && target.Id != alertedId)
                {
                    alertedId = target.Id; selected=target; analysis=null; AlertCount++; if(station)station.ShowTarget(target); alertUntil = Time.unscaledTime + 9;
                    alert.text = historical ? "ASTEROIDE NO OBSERVATÓRIO · ACERVO NASA · " + target.Name + "\n[F4] Analisar no radar" :
                        "NOVO ALVO  ·  " + target.Name + "\nAproximação à Terra: " + DateLabel(target.ApproachDate) + "  ·  [F4] Analisar";
                    if (skyMarker) { skyMarker.gameObject.SetActive(true); skyLabel.text = "ALVO DE OBSERVAÇÃO\nIndicação ilustrativa · [F4]"; }
                }
                nextAlert = Time.unscaledTime + 90;
            }
            alertRoot.SetActive(!IsOpen && Time.unscaledTime < alertUntil);

        }

        private bool Ready()
        {
            var mission = MissionController.Instance;
            return !mission || (mission.Engine != null && mission.Engine.IsRepaired(EquipmentId.Communications));
        }
        private bool OtherModalOpen()
        {
            var overlay = FindAnyObjectByType<MissionOverlay>();
            var management=FindAnyObjectByType<AstroLab.Gameplay.BaseResourceDashboard>();
            return (overlay && overlay.IsModalOpen) || (management && management.IsOpen) || OptionalTeamComponents.IsHelmetModalOpen(OptionalTeamComponents.FindByName("HudCapacete"));
        }
        private bool LookingAtTerminal(Camera cam)
        {
            if (!cam || !terminalProp) return false;
            return Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit hit, 4f) && hit.transform == terminalProp.transform;
        }

        public void Open()
        {
            if (IsOpen || !screen || OtherModalOpen()) return;
            if (!Ready()) { alert.text = "RADAR SEM SINAL\nConserte a antena de comunicações para liberar o terminal."; alertUntil = Time.unscaledTime + 6; return; }
            previousTimeScale = Time.timeScale; previousCursor = Cursor.lockState; previousVisible = Cursor.visible;
            foreach (MonoBehaviour component in FindObjectsByType<MonoBehaviour>())
            {
                string name = component.GetType().Name;
                if (name == "JogadorFPS" || name == "NicholasFirstPersonController" || name == "LaboratorioPlayerController" ||
                    name == "LaboratorioPlayerMotion" || name == "ScannerPrimeiraPessoa" || name == "MissionPlayerInteraction" ||
                    name == "MissionOverlay" || name == "HudCapacete")
                { suspended[component] = component.enabled; component.enabled = false; }
            }
            Time.timeScale = 0; Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            screen.SetActive(true); Reload();
            Debug.Log("[AstroLab][Asteroids] Terminal aberto; simulação da missão pausada durante a análise.");
        }
        public void Close()
        {
            if (!IsOpen) return;
            screen.SetActive(false); Time.timeScale = previousTimeScale;
            foreach (var item in suspended) if (item.Key) item.Key.enabled = item.Value;
            suspended.Clear(); Cursor.lockState = previousCursor; Cursor.visible = previousVisible;
        }
        private void OnDisable() { if (IsOpen) Close(); }
        private void OnDestroy() { if (Client) Client.Changed -= Reload;  }

        private void BuildUI()
        {
            var canvas = new GameObject("RadarCanvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvas.transform.SetParent(transform, false);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.GetComponent<Canvas>().sortingOrder = 220;
            var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440, 900); scaler.matchWidthOrHeight = .5f;
            if (!EventSystem.current)
                new GameObject("RadarEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            alertRoot = Box(canvas.transform, "AvisoDeObservacao", Ink, .27f, .68f, .73f, .80f).gameObject;
            alert = Text(alertRoot.transform, "Alerta", "", 20, Gold, .035f, .06f, .965f, .94f);
            alert.alignment = TextAlignmentOptions.Center;
            screen = Box(canvas.transform, "Terminal", Ink, .035f, .045f, .965f, .955f).gameObject;
            Text(screen.transform, "Eyebrow", "ASTROLAB  /  OBSERVATÓRIO LUNAR", 15, Cyan, .025f, .93f, .75f, .97f);
            Text(screen.transform, "Titulo", "RADAR DE DESCOBERTAS", 33, Cream, .025f, .855f, .78f, .93f);
            Button(screen.transform, "Fechar  [Esc]", Close, .82f, .88f, .975f, .96f);
            status = Text(screen.transform, "Fonte", "", 15, Cyan, .025f, .80f, .975f, .855f);
            body = Box(screen.transform, "Conteudo", Ink, .025f, .16f, .975f, .78f).gameObject;
            list = Box(body.transform, "Alvos", Panel, 0, 0, .245f, 1);
            centre = Box(body.transform, "Comparacao", Panel, .26f, 0, .655f, 1);
            facts = Box(body.transform, "Ficha", Panel, .67f, 0, 1, 1);
            progressLabel = Text(screen.transform, "Progresso", "", 16, Gold, .025f, .09f, .66f, .15f);
            footer = Text(screen.transform, "Rodape", "", 14, Cream, .025f, .02f, .975f, .085f);
            Button(screen.transform, "Atualizar NASA", () => Client.Refresh(), .79f, .09f, .975f, .15f);
            screen.SetActive(false);
        }

        private void Reload()
        {
            if (!status) return;
            status.text = Client.Status;
            progressLabel.text = Progress.Count + " asteroides analisados  ·  " + (Progress.Count >= 1 ? "● Primeira análise" : "○ Primeira análise") +
                                 "  ·  " + (Progress.Count >= 10 ? "● Dez descobertas" : "○ Dez descobertas (" + Math.Min(Progress.Count, 10) + "/10)");
            footer.text = "Dados NASA NeoWs · Distâncias em relação à Terra · Análises salvas neste aparelho\n[F4] abre o radar com comunicações operacionais · No terminal, a missão fica pausada.";
            DrawList();
            if (selected == null) selected = Client.Records.FirstOrDefault();
            if (selected != null) selected = analysis != null && !analysis.Completed ? analysis.Record : Client.Records.FirstOrDefault(r => r.Id == selected.Id) ?? selected;
            DrawDetail();
        }
        private List<AsteroidRecord> VisibleRecords() => Client.Records.Where(r => !collectionOnly || Progress.Contains(r.Id)).ToList();
        private void DrawList()
        {
            Clear(list);
            Text(list, "Secao", collectionOnly ? "MINHA COLEÇÃO" : "ALVOS DISPONÍVEIS", 19, Cream, .06f, .89f, .94f, .98f);
            Button(list, collectionOnly ? "Ver todos os alvos" : "Minha coleção", () => { collectionOnly = !collectionOnly; page = 0; DrawList(); }, .06f, .78f, .94f, .88f);
            var visible = VisibleRecords();
            page = Mathf.Clamp(page, 0, Math.Max(0, (visible.Count - 1) / 5));
            for (int i = 0; i < 5 && page * 5 + i < visible.Count; i++)
            {
                AsteroidRecord r = visible[page * 5 + i];
                string tag = Progress.Contains(r.Id) ? "ANALISADO" : r.Historical ? "ACERVO" : "RECENTE";
                Button(list, r.Name + "\n" + tag + " · " + DateLabel(r.ApproachDate), () => { selected = r; analysis = null; Reload(); },
                    .06f, .64f - i * .125f, .94f, .755f - i * .125f, selected != null && selected.Id == r.Id ? Cyan : Cream);
            }
            if (visible.Count == 0) Text(list, "Vazio", "Suas descobertas\naparecerão aqui.", 17, Cream, .08f, .35f, .92f, .65f);
            Button(list, "<", () => { page--; DrawList(); }, .06f, .025f, .27f, .12f);
            Text(list, "Pagina", (page + 1) + " / " + Math.Max(1, (visible.Count + 4) / 5), 15, Cream, .33f, .025f, .67f, .12f).alignment = TextAlignmentOptions.Center;
            Button(list, ">", () => { page = Math.Min(page + 1, Math.Max(0, (visible.Count - 1) / 5)); DrawList(); }, .73f, .025f, .94f, .12f);
        }

        private void DrawDetail()
        {
            Clear(centre); Clear(facts);
            if (selected == null) { Text(centre, "Vazio", "Nenhum alvo disponível.\nTente atualizar a consulta.", 22, Cream, .08f, .3f, .92f, .7f); return; }
            bool eligible = AsteroidScience.CanAnalyse(selected);
            bool estimating = analysis != null && !analysis.SizeAccepted;
            bool questioning = analysis != null && analysis.SizeAccepted && !analysis.Completed;
            bool hideDiameter = eligible && (estimating || (!Progress.Contains(selected.Id) && (analysis == null || !analysis.SizeAccepted)));
            var comparison = analysis ?? (eligible ? new AsteroidAnalysis(selected, 0) : null);
            if (analysis == null) estimateMetres = 0;
            Text(centre, "Secao", "TELESCÓPIO + COMPARAÇÃO", 19, Cream, .06f, .89f, .94f, .98f);
            if(station)
            {
                station.ShowTarget(selected);
                var preview=new GameObject("CameraAoVivo",typeof(RectTransform),typeof(UnityEngine.UI.RawImage));
                preview.transform.SetParent(centre,false);Anchors((RectTransform)preview.transform,.06f,.56f,.94f,.88f);
                var image=preview.GetComponent<UnityEngine.UI.RawImage>();image.texture=station.Preview;image.raycastTarget=false;
            }
            Text(centre, "Aviso", "Câmera ao vivo · forma e posição ilustrativas", 12, Cream, .05f, .505f, .95f, .55f).alignment = TextAlignmentOptions.Center;
            Text(centre, "Referencia", "Ciano: alvo aproximado · Amarelo: módulo de 12 m\nBranco: astronauta de 1,8 m · Mesma escala", 14, Cream, .06f, .405f, .94f, .50f);
            double scale = comparison?.ScaleMaximum ?? 12;
            double targetMetres = comparison?.RepresentativeMetres ?? 0;
            Box(centre, "AlvoEmEscala", Cyan, .08f, .345f, .08f + (float)(targetMetres / scale) * .82f, .37f);
            float referenceWidth = (float)(12 / scale) * .82f;
            Box(centre, "ModuloEmEscala", Gold, .08f, .31f, .08f + referenceWidth, .335f);
            float humanWidth = (float)(1.8 / scale) * .82f;
            Box(centre, "AstronautaEmEscala", Cream, .08f, .275f, .08f + humanWidth, .30f);
            diameterBar = Box(centre, "PalpiteEmEscala", Gold, .08f, .25f, .08f, .265f);
            sizeLabel = Text(centre, "Medida", "", 16, Cyan, .06f, .21f, .94f, .25f);
            slider = MakeSlider(centre, .08f, .15f, .92f, .21f);
            slider.SetValueWithoutNotify((float)(estimateMetres / scale));
            slider.interactable = estimating;
            slider.onValueChanged.AddListener(UpdateSize); RefreshEstimate();
            Text(centre, "Incerteza", estimating ? "1 / 2 · Compare as barras e estime o diâmetro.\nModelo didático; qualquer valor na faixa NASA é aceito." :
                questioning ? "2 / 2 · Estimativa aceita. Interprete os dados na ficha.\nA NASA informa um intervalo, não um diâmetro exato." :
                "Inicie a análise para estimar o tamanho.\nA resposta aparece depois do seu palpite.", 13, Cream, .06f, .025f, .94f, .14f);

            Text(facts, "Secao", "FICHA CIENTÍFICA", 19, Cream, .07f, .89f, .93f, .98f);
            Text(facts, "Nome", selected.Name, 25, Cyan, .07f, .77f, .93f, .89f);
            string distance = Number(selected.DistanceKm, "N0") + " km";
            string diameter = hideDiameter ? "Oculto até confirmar a estimativa" : Number(selected.MinMetres, "N1") + "–" + Number(selected.MaxMetres, "N1") + " m";
            if (questioning)
            {
                Text(facts, "Dados", "DIÂMETRO ESTIMADO  " + diameter + "\nVELOCIDADE  " + Number(selected.SpeedKmS, "N2") + " km/s\nDISTÂNCIA À TERRA  " + distance,
                    14, Cream, .07f, .59f, .93f, .76f);
                feedback = Text(facts, "Pergunta", analysis.Question.Prompt, 17, Gold, .07f, .325f, .93f, .585f);
                feedback.fontSizeMin = 12;
                for (int i = 0; i < 3; i++)
                {
                    int option = i;
                    Button(facts, analysis.Question.Options[i], () => Answer(option), .07f, .215f - i * .10f, .93f, .305f - i * .10f);
                }
                return;
            }
            string lunar = selected.DistanceKm >= 0 ? "≈ " + (selected.DistanceKm / AsteroidScience.MeanLunarDistanceKm).ToString("N1", CultureInfo.GetCultureInfo("pt-BR")) + " distâncias médias Terra–Lua" : "Não informado";
            Text(facts, "Dados", "DIÂMETRO ESTIMADO\n" + diameter + "\n\n" +
                "VELOCIDADE NA APROXIMAÇÃO\n" + Number(selected.SpeedKmS, "N2") + " km/s\n\n" +
                "DISTÂNCIA NA APROXIMAÇÃO À TERRA\n" + distance + "\n" + lunar + "\n\nDATA DO ENCONTRO\n" + DateLabel(selected.ApproachDate) + (selected.Historical ? " · registro histórico" : " · período consultado"),
                16, Cream, .07f, analysis != null && analysis.Completed ? .36f : .28f, .93f, .76f);
            if (!estimating)
            {
                feedback = Text(facts, "Instrucao", Progress.Contains(selected.Id) ? "Análise registrada na coleção.\nRevisar não aumenta a contagem." : "Estime o tamanho e interprete os dados para registrar a descoberta.", 15, Cream, .07f, .14f, .93f, analysis != null && analysis.Completed ? .36f : .28f);
                feedback.fontSizeMin = 12;
                var button = Button(facts, Progress.Contains(selected.Id) ? "Rever desafio" : "Iniciar análise", BeginAnalysis, .07f, .035f, .93f, .13f);
                button.interactable = eligible;
            }
            else
            {
                feedback = Text(facts, "Instrucao", "Compare o alvo ciano com as referências. Arraste a régua para registrar seu palpite.", 15, Gold, .07f, .14f, .93f, .28f);
                estimateButton = Button(facts, "Confirmar estimativa", ConfirmSizeEstimate, .07f, .035f, .93f, .13f);
                estimateButton.interactable = estimateMoved;
            }
        }

        public void BeginAnalysis()
        {
            if (!AsteroidScience.CanAnalyse(selected)) return;
            analysis = new AsteroidAnalysis(selected, analysisRound++);
            estimateMetres = 0; estimateMoved = false;
            DrawDetail();
        }

        private void UpdateSize(float t)
        {
            if (analysis == null || analysis.SizeAccepted) return;
            estimateMetres = t * analysis.ScaleMaximum; estimateMoved = true;
            if (estimateButton) estimateButton.interactable = true;
            RefreshEstimate();
        }
        private void RefreshEstimate()
        {
            if (!sizeLabel) return;
            sizeLabel.text = analysis == null ? "Estimativa ainda não iniciada" : !estimateMoved ? "Arraste para dar seu palpite" : "Seu palpite: " + Number(estimateMetres, "N1") + " m";
            if (diameterBar) diameterBar.anchorMax = new Vector2(.08f + (float)(estimateMetres / (analysis?.ScaleMaximum ?? 12)) * .82f, .265f);
        }
        public void ConfirmSizeEstimate()
        {
            if (analysis == null || !estimateMoved || analysis.SizeAccepted) return;
            if (!analysis.SubmitEstimate(estimateMetres))
            {
                feedback.text = estimateMetres < selected.MinMetres ? "Seu palpite ficou pequeno. Compare a barra ciano com a régua do módulo e tente aumentar." :
                    "Seu palpite ficou grande. Compare a barra ciano com a régua do módulo e tente diminuir.";
                return;
            }
            DrawDetail();
        }
        public bool Answer(int option)
        {
            if (analysis == null || !analysis.SizeAccepted || analysis.Completed) return false;
            if (!analysis.Answer(option))
            { if (feedback) feedback.text = analysis.Question.Prompt + "\nDica: " + analysis.Question.Hint; return false; }
            string explanation = analysis.Question.Explanation;
            bool first = Progress.Complete(selected);
            string milestone = "";
            if (first)
            {
                if (Progress.Count == 1) milestone = "  CONQUISTA: Primeira análise!";
                if (Progress.Count == 10) milestone = "  CONQUISTA: Dez descobertas!";
                try
                {
                    string temp = SavePath + ".tmp";
                    File.WriteAllText(temp, JsonUtility.ToJson(Progress, true));
                    if (File.Exists(SavePath)) File.Copy(temp, SavePath, true); else File.Move(temp, SavePath);
                    if (File.Exists(temp)) File.Delete(temp);
                }
                catch { milestone += " Não foi possível salvar; mantenha esta sessão aberta."; }
            }
            Reload();
            feedback.text = "Análise concluída! " + explanation + milestone;
            nextAlert = Time.unscaledTime + 35;
            Debug.Log("[AstroLab][Asteroids] Análise concluída. Objetos únicos: " + Progress.Count + ". Nova descoberta: " + first);
            return true;
        }

        private void CreateProp()
        {
            var go=new GameObject("EstacaoDoObservatorio");go.transform.SetParent(transform,false);
            station=go.AddComponent<AsteroidObservationStation>();station.Configure(this);
            terminalProp=go;skyMarker=station.SkyTarget;skyLabel=station.SkyLabel;
        }

        private static string Number(double n, string format) => n < 0 ? "Não informado" : n.ToString(format, CultureInfo.GetCultureInfo("pt-BR"));
        private static string DateLabel(string date) => DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime d) ? d.ToString("dd/MM/yyyy") : "Não informada";
        private static void Clear(Transform t) { foreach (Transform child in t) { child.gameObject.SetActive(false); Destroy(child.gameObject); } }
        private static void Anchors(RectTransform r, float x0, float y0, float x1, float y1)
        { r.anchorMin = new Vector2(x0, y0); r.anchorMax = new Vector2(x1, y1); r.offsetMin = r.offsetMax = Vector2.zero; }
        private static RectTransform Box(Transform parent, string name, Color color, float x0, float y0, float x1, float y1)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image)); go.transform.SetParent(parent, false);
            Anchors((RectTransform)go.transform, x0, y0, x1, y1); var image = go.GetComponent<UnityEngine.UI.Image>(); image.color = color; image.raycastTarget = false;
            return (RectTransform)go.transform;
        }
        private static TMP_Text Text(Transform parent, string name, string value, float size, Color color, float x0, float y0, float x1, float y1)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
            Anchors((RectTransform)go.transform, x0, y0, x1, y1); var t = go.GetComponent<TextMeshProUGUI>();
            t.text = value; t.fontSize = size; t.color = color; t.raycastTarget = false;
            t.enableAutoSizing = true; t.fontSizeMin = size - 2; t.fontSizeMax = size;
            t.alignment = TextAlignmentOptions.MidlineLeft; return t;
        }
        private static UnityEngine.UI.Button Button(Transform parent, string caption, UnityEngine.Events.UnityAction action,
            float x0, float y0, float x1, float y1, Color? tint = null)
        {
            RectTransform box = Box(parent, "Botao", Ink, x0, y0, x1, y1);
            var image = box.GetComponent<UnityEngine.UI.Image>(); image.raycastTarget = true;
            var b = box.gameObject.AddComponent<UnityEngine.UI.Button>(); b.targetGraphic = image;
            var colors = b.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(.65f, .9f, 1); colors.pressedColor = new Color(.35f, .75f, .8f); b.colors = colors;
            b.onClick.AddListener(action); Text(box, "Legenda", caption, 16, tint ?? Cyan, .06f, .06f, .94f, .94f).alignment = TextAlignmentOptions.Center;
            return b;
        }
        private static UnityEngine.UI.Slider MakeSlider(Transform parent, float x0, float y0, float x1, float y1)
        {
            RectTransform root = Box(parent, "ExplorarEstimativa", Ink, x0, y0, x1, y1);
            var slider = root.gameObject.AddComponent<UnityEngine.UI.Slider>();
            var handle = Box(root, "Controle", Cyan, 0, .05f, .04f, .95f);
            handle.sizeDelta = new Vector2(18, 0); handle.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            slider.handleRect = handle; slider.targetGraphic = handle.GetComponent<UnityEngine.UI.Image>();
            root.GetComponent<UnityEngine.UI.Image>().raycastTarget = true; return slider;
        }
    }
}
