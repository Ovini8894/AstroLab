using UnityEngine;

namespace AstroLab.PousoLunar
{
    [RequireComponent(typeof(LunarLandingController))]
    public sealed class LunarLandingSceneLink : MonoBehaviour
    {
        [Tooltip("Desative na demonstração para inspecionar o pouso sem sair da cena.")]
        public bool loadMoonOnCompletion;
        public string moonScenePath = LunarLandingTransition.MoonScene;
        LunarLandingController controller;
        void Awake()
        {
            controller = GetComponent<LunarLandingController>();
            if (LunarLandingTransition.ClaimsScene(gameObject.scene.path)) controller.playOnStart = false;
            controller.Completed += OnCompleted;
        }
        void OnDestroy() { if (controller) controller.Completed -= OnCompleted; }
        void OnCompleted() { if (loadMoonOnCompletion) LunarLandingTransition.LoadMoon(moonScenePath); }
        public void ContinueToMoon() => LunarLandingTransition.LoadMoon(moonScenePath);
    }
}
