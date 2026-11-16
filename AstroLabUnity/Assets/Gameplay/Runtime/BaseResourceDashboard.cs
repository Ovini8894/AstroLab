using System.Collections.Generic;
using System.Linq;
using AstroLab.Nicholas;
using AstroLab.Nicholas.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace AstroLab.Gameplay
{
    public sealed class BaseResourceDashboard : MonoBehaviour
    {
        public bool IsOpen => modal && modal.activeSelf;
        private static readonly Color Ink = new Color32(28, 26, 61, 245), Panel = new Color32(62, 76, 120, 255);
        private static readonly Color Cream = new Color32(244, 233, 212, 255), Cyan = new Color32(71, 230, 255, 255), Gold = new Color32(255, 194, 58, 255);
        private MissionController mission;
        private MissionPlayerInteraction interaction;
        private MissionOverlay overlay;
        private GameObject modal, strip, promptRoot;
        private TMP_Text resources, prompt, top, detail, feedback, forecast, checklist, location;
        private TMP_Text notice;
        private GameObject noticeRoot;
        private float noticeUntil;
        private RectTransform equipmentList;
        private UnityEngine.UI.Button repairButton, powerButton;
        private TMP_Text repairCaption, powerCaption;
        private readonly List<UnityEngine.UI.Button> systemButtons = new List<UnityEngine.UI.Button>();
        private EquipmentId selected = EquipmentId.LifeSupport;
        private readonly Dictionary<Behaviour, bool> suspended = new Dictionary<Behaviour, bool>();
        private float previousScale, refreshAt;
        private CursorLockMode previousCursor;
        private bool previousVisible;

        private void Start()
        {
            mission = MissionController.Instance;
            interaction = FindAnyObjectByType<MissionPlayerInteraction>();
            overlay = FindAnyObjectByType<MissionOverlay>();
            if (!mission) { enabled = false; return; }
            Build(); mission.Updated += Refresh; mission.Feedback += ShowFeedback;
            BaseKnowledgeCatalog.Changed += Refresh;
            Refresh();
        }
        private void Update()
        {
            if (!modal) return;
            if (Keyboard.current != null)
            {
                if (IsOpen && Keyboard.current.escapeKey.wasPressedThisFrame) { Close(); return; }
                if (Keyboard.current.tabKey.wasPressedThisFrame) { if (IsOpen) Close(); else Open(); }
            }
            bool otherModal = !IsOpen && ((overlay && overlay.IsModalOpen) ||
                OptionalTeamComponents.IsHelmetModalOpen(OptionalTeamComponents.FindByName("HudCapacete")) ||
                (FindAnyObjectByType<AstroLab.Asteroids.AsteroidTerminal>()?.IsOpen ?? false));
            strip.SetActive(!IsOpen && !otherModal);
            promptRoot.SetActive(!IsOpen && !otherModal);
            if (!IsOpen) prompt.text = interaction && !string.IsNullOrWhiteSpace(interaction.CurrentPrompt)
                ? interaction.CurrentPrompt : "Mire em um equipamento para interagir.\n[Tab] Gestão e objetivos   ·   [L] Eclusa";
            noticeRoot.SetActive(!IsOpen && !otherModal && Time.unscaledTime < noticeUntil);
            if (Time.unscaledTime >= refreshAt) { refreshAt = Time.unscaledTime + .25f; Refresh(); }
        }
        public void Open()
        {
            if (IsOpen || !modal || (overlay && overlay.IsModalOpen) ||
                OptionalTeamComponents.IsHelmetModalOpen(OptionalTeamComponents.FindByName("HudCapacete")) ||
                (FindAnyObjectByType<AstroLab.Asteroids.AsteroidTerminal>()?.IsOpen ?? false)) return;
            if (interaction && interaction.AimedEquipment) selected = interaction.AimedEquipment.Equipment;
            previousScale = Time.timeScale; previousCursor = Cursor.lockState; previousVisible = Cursor.visible;
            foreach (var component in FindObjectsByType<MonoBehaviour>())
            {
                string type = component.GetType().Name;
                if (type == "JogadorFPS" || type == "NicholasFirstPersonController" || type == "LaboratorioPlayerController" ||
                    type == "LaboratorioPlayerMotion" || type == "ScannerPrimeiraPessoa" || type == "MissionPlayerInteraction" ||
                    type == "MissionOverlay" || type == "HudCapacete")
                { suspended[component] = component.enabled; component.enabled = false; }
            }
            Time.timeScale = 0; Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            modal.SetActive(true); Refresh();
        }
        public void Close()
        {
            if (!IsOpen) return;
            modal.SetActive(false); Time.timeScale = previousScale;
            foreach (var item in suspended) if (item.Key) item.Key.enabled = item.Value;
            suspended.Clear(); Cursor.lockState = previousCursor; Cursor.visible = previousVisible;
        }
        private void OnDisable() { Close(); }
        private void OnDestroy()
        {
            if (mission) { mission.Updated -= Refresh; mission.Feedback -= ShowFeedback; }
            BaseKnowledgeCatalog.Changed -= Refresh;
        }
        private void ShowFeedback(string message)
        {
            if (feedback) feedback.text = message;
            if (notice) { notice.text = message; noticeUntil = Time.unscaledTime + 6; }
            Refresh();
        }
        private bool CanRepairHere()
        {
            if (!interaction || !interaction.AimedEquipment || interaction.AimedEquipment.Equipment != selected) return false;
            Camera camera = Camera.main;
            var target = interaction.AimedEquipment;
            if (!camera || !target.gameObject.activeInHierarchy) return false;
            var collider = target.GetComponent<Collider>();
            // Non-convex model colliders do not support ClosestPoint. Use the same aimed ray as interaction.
            return collider && collider.Raycast(new Ray(camera.transform.position, camera.transform.forward), out _, 5f);
        }
        private void Repair()
        {
            if (!CanRepairHere()) { feedback.text = "Aproxime-se e mire no equipamento antes de abrir a gestão, ou use E junto a ele."; return; }
            feedback.text = mission.Repair(selected).Message; Refresh();
        }
        private void Power() { feedback.text = mission.Power(selected, !mission.Engine.IsPowered(selected)).Message; Refresh(); }

        public void Refresh()
        {
            if (!resources || !mission || mission.Engine == null) return;
            var state = mission.Engine.State;
            string summary = "PEÇAS   " + state.SpareParts + "     AÇÕES   " + state.RemainingActionTime + "/6\n" +
                "ENERGIA   " + mission.Engine.PowerUsed + "/" + mission.Engine.PowerCapacity + " em uso\n" +
                "ÁGUA   " + state.Water + "     ALIMENTOS   " + state.Food + "\nAR DO HABITAT   " + state.HabitatAir + "%";
            resources.text = "ESTOQUE DA BASE  ·  [Tab]\n" + summary;
            top.text = "TURNO " + state.Turn + "/3   ·   " + (state.IsOutside ? "EXTERIOR / EVA" : "INTERIOR DA BASE") + "\n" + summary.Replace("\n", "    ·    ");
            var definition = MissionRuleSet.Find(selected);
            var knowledge = BaseKnowledgeCatalog.Find(selected);
            bool repaired = mission.Engine.IsRepaired(selected), powered = mission.Engine.IsPowered(selected);
            detail.text = "<size=26><color=#47E6FF>" + definition.DisplayName + "</color></size>\n" +
                (repaired ? (definition.PowerDemand > 0 ? (powered ? "REPARADO · LIGADO" : "REPARADO · DESLIGADO") : "RESTAURADO · SISTEMA PASSIVO") : "AVARIADO · PRECISA DE REPARO") +
                "\n\n" + knowledge?.description + "\n\n<color=#FFC23A>DIAGNÓSTICO INICIAL</color>\n" + knowledge?.fault +
                "\n\n<color=#FFC23A>MANUTENÇÃO</color>\n" + knowledge?.repair +
                "\n\n<color=#FFC23A>O QUE MUDA NO JOGO</color>\n" + knowledge?.benefit;
            location.text = "REPARO: " + definition.RepairParts + " peças + " + definition.RepairTime + " tempos   ·   LOCAL: " + (definition.NeedsEva ? "exterior" : "interior") +
                "\nCONSUMO ELÉTRICO: " + definition.PowerDemand + "   ·   Custos e estoques são unidades da simulação.";
            if (!repaired)
            {
                var availability = mission.Engine.CanRepair(selected);
                location.text += "\n" + (!availability.Success ? availability.Message : !CanRepairHere()
                    ? "Para reparar: aproxime-se, mire no equipamento e use [R] ou [E]."
                    : "Equipamento ao alcance: reparo disponível.");
            }
            repairCaption.text = repaired ? "Reparo já concluído" : "Reparar (-" + definition.RepairParts + " peças)";
            repairButton.interactable = !repaired && CanRepairHere() && mission.Engine.CanRepair(selected).Success;
            powerCaption.text = definition.PowerDemand == 0 ? "Sistema sem chave elétrica" : powered ? "Desligar sistema" : "Ligar sistema";
            powerButton.interactable = repaired && definition.PowerDemand > 0 && state.Outcome == MissionOutcome.Running;
            var next = mission.Engine.PreviewNextTurn();
            int food = next.Food - state.Food;
            int water = next.Water - state.Water;
            int air = next.HabitatAir - state.HabitatAir;
            forecast.text = "PRÓXIMO TURNO\nAlimentos " + Signed(food) + "  ·  Água " + Signed(water) + "  ·  Ar " + Signed(air) +
                "\n" + (state.IsOutside ? "Retorne à base para avançar o turno." : state.Turn == 2 ? "Evento solar simulado incluído na previsão." : "Produção e consumo ocorrem ao avançar.") +
                "\n\nMETA FINAL\nÁgua ≥ 1  ·  Alimentos ≥ 2  ·  Ar ≥ 30%";
            checklist.text = "PRÓXIMA AÇÃO\n" + MissionGuidance.Next(mission.Engine) + "\n\nRESTAURAÇÃO DA BASE\n" + string.Join("\n", mission.Engine.GetVictoryChecklist().Select(c => (c.Met ? "[OK] " : "[ ] ") + c.Label)) +
                "\n\nAPOLLO 15 · EXTRA OPCIONAL\n" + state.NextArtifactIndex + "/5 registros históricos\nAs réplicas estão no exterior.\nNão são exigidas para restaurar a base.";
            if (state.Outcome != MissionOutcome.Running) feedback.text = state.OutcomeReason;
            DrawEquipmentList();
        }
        private static string Signed(int value) => value > 0 ? "+" + value : value.ToString();

        private void Build()
        {
            var go = new GameObject("GestaoBaseCanvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            go.transform.SetParent(transform, false); go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay; go.GetComponent<Canvas>().sortingOrder = 180;
            var scale = go.GetComponent<UnityEngine.UI.CanvasScaler>(); scale.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scale.referenceResolution = new Vector2(1440, 900); scale.matchWidthOrHeight = .5f;
            if (!EventSystem.current) new GameObject("BaseEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            strip = Box(go.transform, "Estoques", Ink, .69f, .18f, .92f, .375f).gameObject;
            resources = Text(strip.transform, "Recursos", "", 17, Cream, .055f, .06f, .945f, .94f);
            promptRoot = Box(go.transform, "OrientacaoDeManutencao", Ink, .32f, .18f, .67f, .28f).gameObject;
            prompt = Text(promptRoot.transform, "AjudaContextual", "", 16, Cyan, .03f, .05f, .97f, .95f);
            noticeRoot = Box(go.transform, "RespostaDaAcao", Ink, .32f, .3f, .68f, .40f).gameObject;
            notice = Text(noticeRoot.transform, "Mensagem", "", 18, Gold, .04f, .08f, .96f, .92f);
            noticeRoot.SetActive(false);
            modal = Box(go.transform, "GestaoDaBase", Ink, .025f, .035f, .975f, .965f).gameObject;
            Text(modal.transform, "Titulo", "RESTAURAR A BASE", 31, Cream, .025f, .9f, .68f, .975f);
            Button(modal.transform, "Fechar [Tab / Esc]", Close, .8f, .91f, .975f, .975f);
            top = Text(modal.transform, "Resumo", "", 15, Cyan, .025f, .815f, .975f, .9f);
            equipmentList = Box(modal.transform, "Equipamentos", Panel, .025f, .235f, .235f, .79f);
            var centre = Box(modal.transform, "Inspecao", Panel, .25f, .235f, .66f, .79f);
            detail = Text(centre, "Conhecimento", "", 17, Cream, .045f, .03f, .955f, .97f);
            var right = Box(modal.transform, "Objetivos", Panel, .675f, .235f, .975f, .79f);
            checklist = Text(right, "Metas", "", 15, Cream, .055f, .33f, .945f, .97f);
            forecast = Text(right, "Previsao", "", 15, Gold, .055f, .025f, .945f, .34f);
            location = Text(modal.transform, "Custos", "", 16, Gold, .025f, .15f, .65f, .225f);
            repairButton = Button(modal.transform, "Reparar", Repair, .025f, .085f, .23f, .15f); repairCaption = repairButton.GetComponentInChildren<TMP_Text>();
            powerButton = Button(modal.transform, "Ligar sistema", Power, .245f, .085f, .45f, .15f); powerCaption = powerButton.GetComponentInChildren<TMP_Text>();
            Button(modal.transform, "Recarregar traje", () => { feedback.text = mission.Recharge().Message; Refresh(); }, .465f, .085f, .65f, .15f);
            Button(modal.transform, "Avançar turno", () => { feedback.text = mission.AdvanceTurn().Message; Refresh(); }, .675f, .15f, .975f, .22f);
            Button(modal.transform, "Salvar [F5]", () => mission.SaveGame(), .675f, .085f, .82f, .15f);
            Button(modal.transform, "Carregar [F9]", () => mission.LoadGame(), .83f, .085f, .975f, .15f);
            feedback = Text(modal.transform, "Feedback", "Aproxime-se dos equipamentos e use E para manutenção. Os recursos da missão ficam pausados nesta tela.", 16, Cream, .025f, .015f, .975f, .075f);
            modal.SetActive(false);
        }
        private void DrawEquipmentList()
        {
            if (systemButtons.Count == 0)
            {
                Text(equipmentList, "Titulo", "SISTEMAS DA BASE", 18, Cream, .06f, .89f, .94f, .98f);
                for (int i = 0; i < MissionRuleSet.Definitions.Length; i++)
                {
                    var id = MissionRuleSet.Definitions[i].Id;
                    systemButtons.Add(Button(equipmentList, "", () => { selected = id; Refresh(); }, .06f, .77f - i * .105f, .94f, .865f - i * .105f));
                }
            }
            for (int i = 0; i < MissionRuleSet.Definitions.Length; i++)
            {
                var d = MissionRuleSet.Definitions[i];
                string state = !mission.Engine.IsRepaired(d.Id) ? "AVARIADO" : d.PowerDemand == 0 ? "RESTAURADO" : mission.Engine.IsPowered(d.Id) ? "LIGADO" : "DESLIGADO";
                var button = systemButtons[i];
                button.GetComponentInChildren<TMP_Text>().text = d.DisplayName + "\n" + state;
                button.GetComponentInChildren<TMP_Text>().color = selected == d.Id ? Gold : Cyan;
            }
        }
        private static RectTransform Box(Transform parent, string name, Color color, float x0, float y0, float x1, float y1)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image)); go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform; r.anchorMin = new Vector2(x0,y0); r.anchorMax = new Vector2(x1,y1); r.offsetMin = r.offsetMax = Vector2.zero;
            go.GetComponent<UnityEngine.UI.Image>().color=color; go.GetComponent<UnityEngine.UI.Image>().raycastTarget=false; return r;
        }
        private static TMP_Text Text(Transform parent, string name, string text, float size, Color color, float x0, float y0, float x1, float y1)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent,false);
            var r=(RectTransform)go.transform; r.anchorMin=new Vector2(x0,y0); r.anchorMax=new Vector2(x1,y1); r.offsetMin=r.offsetMax=Vector2.zero;
            var t=go.GetComponent<TextMeshProUGUI>(); t.text=text; t.fontSize=size; t.color=color; t.raycastTarget=false; t.alignment=TextAlignmentOptions.MidlineLeft;
            t.enableAutoSizing=true; t.fontSizeMin=size-2; t.fontSizeMax=size; return t;
        }
        private static UnityEngine.UI.Button Button(Transform parent, string caption, UnityEngine.Events.UnityAction action, float x0,float y0,float x1,float y1)
        {
            var r=Box(parent,"Acao",Ink,x0,y0,x1,y1); var image=r.GetComponent<UnityEngine.UI.Image>(); image.raycastTarget=true;
            var b=r.gameObject.AddComponent<UnityEngine.UI.Button>(); b.targetGraphic=image; b.onClick.AddListener(action);
            Text(r,"Legenda",caption,16,Cyan,.04f,.04f,.96f,.96f).alignment=TextAlignmentOptions.Center; return b;
        }
    }
}
