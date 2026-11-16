using System.Linq;
using AstroLab.Nicholas;
using AstroLab.Nicholas.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AstroLab.Gameplay
{
    /// <summary>Optional surface route to the next historical replica, so the globe can be explored deliberately.</summary>
    public sealed class LunarExplorationGuide : MonoBehaviour
    {
        public bool FollowApollo { get; private set; }
        public string RouteText => route ? route.text : "";
        private MissionController mission;
        private MissionHistoricalTarget[] artifacts;
        private GameObject panel;
        private TMP_Text route;
        private float refreshAt;
        public void Toggle(){FollowApollo=!FollowApollo;Refresh();}
        private void Start()
        {
            mission=MissionController.Instance;artifacts=FindObjectsByType<MissionHistoricalTarget>();
            var go=new GameObject("RotaExploracaoCanvas",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler));go.transform.SetParent(transform,false);
            go.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;go.GetComponent<Canvas>().sortingOrder=145;
            var scale=go.GetComponent<UnityEngine.UI.CanvasScaler>();scale.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1440,900);scale.matchWidthOrHeight=.5f;
            panel=new GameObject("RotaOpcional",typeof(RectTransform),typeof(UnityEngine.UI.Image));panel.transform.SetParent(go.transform,false);
            panel.GetComponent<UnityEngine.UI.Image>().color=new Color(.11f,.10f,.24f,.92f);panel.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;
            var rect=panel.GetComponent<RectTransform>();rect.anchorMin=new Vector2(.69f,.41f);rect.anchorMax=new Vector2(.92f,.59f);rect.offsetMin=rect.offsetMax=Vector2.zero;
            var text=new GameObject("OrientacaoApollo",typeof(RectTransform),typeof(TextMeshProUGUI));text.transform.SetParent(panel.transform,false);route=text.GetComponent<TextMeshProUGUI>();
            route.fontSize=18;route.color=new Color(.95f,.91f,.83f);route.raycastTarget=false;route.enableAutoSizing=true;route.fontSizeMin=16;route.fontSizeMax=18;
            rect=route.rectTransform;rect.anchorMin=new Vector2(.055f,.08f);rect.anchorMax=new Vector2(.945f,.92f);rect.offsetMin=rect.offsetMax=Vector2.zero;
            Refresh();
        }
        private void Update()
        {
            if(!panel||!mission)return;
            var management=FindAnyObjectByType<BaseResourceDashboard>();var overlay=FindAnyObjectByType<MissionOverlay>();var terminal=FindAnyObjectByType<AstroLab.Asteroids.AsteroidTerminal>();
            bool hidden=(management&&management.IsOpen)||(overlay&&overlay.IsModalOpen)||(terminal&&terminal.IsOpen)||OptionalTeamComponents.IsHelmetModalOpen(OptionalTeamComponents.FindByName("HudCapacete"));
            panel.SetActive(!hidden);
            if(!hidden&&Keyboard.current!=null&&Keyboard.current.hKey.wasPressedThisFrame)Toggle();
            if(Time.unscaledTime>=refreshAt){refreshAt=Time.unscaledTime+.2f;Refresh();}
        }
        private void Refresh()
        {
            if(!route||!mission||mission.Engine==null)return;
            var rect=panel.GetComponent<RectTransform>();
            rect.anchorMin=new Vector2(.69f,FollowApollo ? .41f : .54f);rect.anchorMax=new Vector2(.92f,.59f);rect.offsetMin=rect.offsetMax=Vector2.zero;
            if(!FollowApollo){route.text="[H] Rota Apollo 15 · opcional";return;}
            var state=mission.Engine.State;
            if(state.NextArtifactIndex>=Apollo15Trail.Items.Length){route.text="APOLLO 15 · CONCLUÍDO\nCinco registros históricos encontrados.\n[H] Ocultar rota";return;}
            bool emergency=state.IsOutside&&(state.SuitOxygen<30||state.SuitBattery<30);
            var item=Apollo15Trail.Items[state.NextArtifactIndex];
            var artifact=artifacts.FirstOrDefault(a=>a&&a.Artifact==item.Id);
            Transform target=emergency?mission.OuterAirlock:artifact?artifact.transform:null;
            if(!target){route.text="Rota em preparação…";return;}
            var viewer=Camera.main?Camera.main.transform:mission.PlayerTransform;
            var world=LunarWorld.Instance;
            int distance=Mathf.RoundToInt(world?world.Distance(viewer.position,target.position):Vector3.Distance(viewer.position,target.position));
            string bearing=world?world.Bearing(viewer,target.position):"";
            route.text=(emergency?"RETORNE À ECLUSA":"APOLLO 15 · "+(state.NextArtifactIndex+1)+"/5")+"\n"+(emergency?"Recursos do traje baixos":item.Name)+"\n"+distance+" m · "+bearing+"\n"+(emergency?"[L] Entrar ao chegar":"Aproxime-se e escaneie com o aparelho.")+"\n[H] Ocultar rota";
        }
    }
}

