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
        private bool collectionOnly, challenge;
        private float previousTimeScale, nextAlert, alertUntil;
        private CursorLockMode previousCursor;
        private bool previousVisible;
        private Transform skyMarker;
        private string alertedId;

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
            if (scene.name != "Lua" && scene.name != "MissaoNicholas") return;
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
            if (skyMarker && cam) skyMarker.position = cam.transform.position + new Vector3(40, 35, 60);
            if (skyMarker && cam) skyMarker.rotation = Quaternion.LookRotation(skyMarker.position - cam.transform.position);
            if (Keyboard.current != null)
            {
                if (IsOpen && Keyboard.current.escapeKey.wasPressedThisFrame) { Close(); return; }
                if (!IsOpen && !OtherModalOpen() && (Keyboard.current.f4Key.wasPressedThisFrame ||
                    (Keyboard.current.eKey.wasPressedThisFrame && LookingAtTerminal(cam)))) Open();
            }
            if (!IsOpen && !OtherModalOpen() && Ready() && Time.unscaledTime >= nextAlert)
            {
                // One notification per available object, then a substantial quiet interval.
                var target = Client.Records.FirstOrDefault(r => !r.Historical && !Progress.Contains(r.Id));
                bool historical = target == null;
                if (target == null) target = Client.Records.FirstOrDefault(r => !Progress.Contains(r.Id));
                if (target != null && target.Id != alertedId)
                {
                    alertedId = target.Id; alertUntil = Time.unscaledTime + 9;
                    alert.text = historical ? "ACERVO DISPONÍVEL  ·  " + target.Name + "\n[F4] Analisar no radar" :
                        "NOVO ALVO  ·  " + target.Name + "\nAproximação à Terra: " + DateLabel(target.ApproachDate) + "  ·  [F4] Analisar";
                    if (skyMarker) { skyMarker.gameObject.SetActive(true); skyLabel.text = "ALVO DE OBSERVAÇÃO\nIndicação ilustrativa · [F4]"; }
                }
                nextAlert = Time.unscaledTime + 90;
            }
            alertRoot.SetActive(!IsOpen && Time.unscaledTime < alertUntil);
            if (skyMarker && IsOpen) skyMarker.gameObject.SetActive(false);
        }

        private bool Ready()
        {
            var mission = MissionController.Instance;
            return !mission || (mission.Engine != null && mission.Engine.IsRepaired(EquipmentId.Communications));
        }
        private bool OtherModalOpen()
        {
            var overlay = FindAnyObjectByType<MissionOverlay>();
            return overlay && overlay.IsModalOpen || OptionalTeamComponents.IsHelmetModalOpen(OptionalTeamComponents.FindByName("HudCapacete"));
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
        private void OnDestroy() { if (Client) Client.Changed -= Reload; if (skyMarker) Destroy(skyMarker.gameObject); }

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
            if (selected != null) selected = Client.Records.FirstOrDefault(r => r.Id == selected.Id) ?? selected;
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
                Button(list, r.Name + "\n" + tag + " · " + DateLabel(r.ApproachDate), () => { selected = r; challenge = false; Reload(); },
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
            Text(centre, "Secao", "EXPLORE O TAMANHO", 19, Cream, .06f, .89f, .94f, .98f);
            var silhouette = new GameObject("AsteroideIlustrativo", typeof(RectTransform), typeof(CanvasRenderer), typeof(AsteroidSilhouette));
            silhouette.transform.SetParent(centre, false); Anchors((RectTransform)silhouette.transform, .31f, .55f, .69f, .88f);
            silhouette.GetComponent<AsteroidSilhouette>().color = new Color32(169, 165, 192, 255);
            Text(centre, "Aviso", "Modelo ilustrativo · formato real não disponível", 12, Cream, .05f, .505f, .95f, .55f).alignment = TextAlignmentOptions.Center;
            Text(centre, "Referencia", "Módulo de referência: 12 m  |  Astronauta: 1,8 m\nBarras abaixo compartilham a mesma escala.", 14, Cream, .06f, .405f, .94f, .50f);
            diameterBar = Box(centre, "DiametroEmEscala", Cyan, .08f, .345f, .9f, .37f);
            float referenceWidth = (float)(12 / Math.Max(12, selected.MaxMetres)) * .82f;
            Box(centre, "ModuloEmEscala", Gold, .08f, .31f, .08f + referenceWidth, .335f);
            float humanWidth = (float)(1.8 / Math.Max(12, selected.MaxMetres)) * .82f;
            Box(centre, "AstronautaEmEscala", Cream, .08f, .275f, .08f + humanWidth, .30f);
            sizeLabel = Text(centre, "Medida", "", 18, Cyan, .06f, .22f, .94f, .275f);
            slider = MakeSlider(centre, .08f, .15f, .92f, .21f);
            slider.onValueChanged.AddListener(UpdateSize); UpdateSize(0);
            Text(centre, "Incerteza", "Arraste para explorar a faixa estimada.\nA NASA não fornece uma medida exata única.", 13, Cream, .06f, .025f, .94f, .14f);

            Text(facts, "Secao", "FICHA CIENTÍFICA", 19, Cream, .07f, .89f, .93f, .98f);
            Text(facts, "Nome", selected.Name, 25, Cyan, .07f, .77f, .93f, .89f);
            string distance = Number(selected.DistanceKm, "N0") + " km";
            string lunar = selected.DistanceKm >= 0 ? "≈ " + (selected.DistanceKm / AsteroidScience.MeanLunarDistanceKm).ToString("N1", CultureInfo.GetCultureInfo("pt-BR")) + " distâncias médias Terra–Lua" : "Não informado";
            Text(facts, "Dados", "DIÂMETRO ESTIMADO\n" + Number(selected.MinMetres, "N1") + "–" + Number(selected.MaxMetres, "N1") + " m\n\n" +
                "VELOCIDADE NA APROXIMAÇÃO\n" + Number(selected.SpeedKmS, "N2") + " km/s\n\n" +
                "DISTÂNCIA NA APROXIMAÇÃO À TERRA\n" + distance + "\n" + lunar + "\n\nDATA DO ENCONTRO\n" + DateLabel(selected.ApproachDate) + (selected.Historical ? " · registro histórico" : " · período consultado"),
                16, Cream, .07f, .28f, .93f, .76f);
            if (!challenge)
            {
                feedback = Text(facts, "Instrucao", Progress.Contains(selected.Id) ? "Análise registrada na coleção.\nRevisar não aumenta a contagem." : "Explore o tamanho e responda uma pergunta para registrar a descoberta.", 15, Cream, .07f, .14f, .93f, .28f);
                var button = Button(facts, Progress.Contains(selected.Id) ? "Rever desafio" : "Iniciar análise", () => { challenge = true; DrawDetail(); }, .07f, .035f, .93f, .13f);
                button.interactable = AsteroidScience.CanAnalyse(selected);
            }
            else
            {
                feedback = Text(facts, "Pergunta", "Na MENOR estimativa, o diâmetro supera o módulo de referência de 12 m?", 15, Gold, .07f, .14f, .93f, .28f);
                Button(facts, "Sim", () => Answer(true), .07f, .035f, .47f, .13f);
                Button(facts, "Não", () => Answer(false), .53f, .035f, .93f, .13f);
            }
        }

        private void UpdateSize(float t)
        {
            if (selected == null || !sizeLabel) return;
            double metres = selected.MinMetres + (selected.MaxMetres - selected.MinMetres) * t;
            sizeLabel.text = AsteroidScience.CanAnalyse(selected) ? "Diâmetro explorado: " + Number(metres, "N1") + " m" : "Tamanho não informado";
            if (diameterBar) diameterBar.anchorMax = new Vector2(.08f + (float)(Math.Max(0, metres) / Math.Max(12, selected.MaxMetres)) * .82f, .37f);
        }
        public bool Answer(bool greater)
        {
            if (selected == null || !AsteroidScience.CanAnalyse(selected)) return false;
            if (greater != (selected.MinMetres > 12))
            { feedback.text = "Olhe a menor estimativa: " + Number(selected.MinMetres, "N1") + " m. Compare com 12 m e tente novamente."; return false; }
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
            challenge = false; Reload();
            feedback.text = "Análise concluída! A estimativa mínima é " + Number(selected.MinMetres, "N1") + " m." + milestone;
            nextAlert = Time.unscaledTime + 35;
            Debug.Log("[AstroLab][Asteroids] Análise concluída. Objetos únicos: " + Progress.Count + ". Nova descoberta: " + first);
            return true;
        }

        private void CreateProp()
        {
            Camera cam = Camera.main;
            Transform anchor = MissionController.Instance ? MissionController.Instance.InnerAirlock : null;
            if (!anchor && cam) anchor = cam.transform;
            if (!anchor) return;
            terminalProp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            terminalProp.name = "Terminal_Radar_Asteroides"; terminalProp.transform.SetParent(transform);
            Vector3 direction = Vector3.ProjectOnPlane(anchor.forward, Vector3.up).normalized;
            if (direction.sqrMagnitude < .1f) direction = Vector3.forward;
            terminalProp.transform.position = anchor.position + direction * 2 + anchor.right * 1.5f + Vector3.up * (cam && anchor == cam.transform ? -.4f : 1.1f);
            terminalProp.transform.rotation = Quaternion.LookRotation(-direction);
            terminalProp.transform.localScale = new Vector3(.85f, .65f, .16f);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader) { var material = new Material(shader); material.color = Panel; terminalProp.GetComponent<Renderer>().material = material; }
            var label = new GameObject("Rotulo", typeof(TextMeshPro)); label.transform.SetParent(terminalProp.transform, false);
            label.transform.localPosition = new Vector3(0, 0, -.6f); label.transform.localRotation = Quaternion.Euler(0, 180, 0);
            var text = label.GetComponent<TextMeshPro>(); text.text = "RADAR\n[E] OBSERVAR\nAtalho: [F4]";
            text.fontSize = 2.7f; text.alignment = TextAlignmentOptions.Center; text.color = Cyan;
            text.rectTransform.sizeDelta = new Vector2(1, .8f);
            var sky = new GameObject("IndicacaoIlustrativaDeAlvo"); skyMarker = sky.transform;
            var rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.name = "AsteroideIlustrativoSemOrbita"; rock.transform.SetParent(skyMarker, false);
            rock.transform.localPosition = new Vector3(0, 7, 0); rock.transform.localScale = new Vector3(3, 2.4f, 2.7f);
            Destroy(rock.GetComponent<Collider>());
            var rockShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (rockShader) { var material = new Material(rockShader); material.color = new Color32(169, 165, 192, 255); rock.GetComponent<Renderer>().material = material; }
            skyLabel = new GameObject("Legenda", typeof(TextMeshPro)).GetComponent<TextMeshPro>();
            skyLabel.transform.SetParent(skyMarker, false); skyLabel.fontSize = 9; skyLabel.color = Gold;
            skyLabel.alignment = TextAlignmentOptions.Center; skyLabel.rectTransform.sizeDelta = new Vector2(22, 8);
            sky.SetActive(false);
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
