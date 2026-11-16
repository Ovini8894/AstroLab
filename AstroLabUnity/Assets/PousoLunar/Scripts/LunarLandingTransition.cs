using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using AstroLab.Menu;

namespace AstroLab.PousoLunar
{
    /// <summary>Entrada pública sem dependência da viagem; reutiliza TransicaoPreta na revelação da cena.</summary>
    public sealed class LunarLandingTransition : MonoBehaviour
    {
        public const string LandingScene = "Assets/PousoLunar/Cenas/PousoLunar.unity";
        public const string MoonScene = "Assets/BaseLunar/Cenas/Lua.unity";
        static LunarLandingTransition active;
        static string pendingLanding;
        Image curtain;
        [Min(.01f)] public float fadeDuration = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ClearStatics() { active = null; pendingLanding = null; }

        public static bool IsBusy => active != null;
        public static bool BeginLanding() => BeginLanding(LandingScene);
        public static bool BeginLanding(string scenePath)
        {
            if (IsBusy || !Application.CanStreamedLevelBeLoaded(scenePath))
            {
                Debug.LogWarning("Pouso não iniciado: transição ocupada ou cena fora do build: " + scenePath);
                return false;
            }
            Create().StartCoroutine(active.Load(scenePath,true));
            return true;
        }

        public static bool LoadMoon(string scenePath = MoonScene)
        {
            if (IsBusy || !Application.CanStreamedLevelBeLoaded(scenePath))
            {
                Debug.LogWarning("Lua não carregada: transição ocupada ou cena fora do build: " + scenePath);
                return false;
            }
            Create().StartCoroutine(active.Load(scenePath,false));
            return true;
        }

        static LunarLandingTransition Create()
        {
            var go = new GameObject("LunarLandingTransition",typeof(Canvas));
            DontDestroyOnLoad(go);
            var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 31999;
            var image = new GameObject("FadeOut",typeof(RectTransform),typeof(Image));
            image.transform.SetParent(go.transform,false);
            var rect = (RectTransform)image.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            active = go.AddComponent<LunarLandingTransition>();
            active.curtain = image.GetComponent<Image>(); active.curtain.color = new Color(0,0,0,0);
            return active;
        }

        public static bool ClaimsScene(string path) => pendingLanding == path;

        IEnumerator Load(string path, bool landing)
        {
            for (float t=0;t<fadeDuration;t+=Time.unscaledDeltaTime)
            {
                curtain.color = new Color(0,0,0,Mathf.SmoothStep(0,1,t/fadeDuration)); yield return null;
            }
            curtain.color = Color.black;
            if (landing) pendingLanding = path;
            // Overlay existente assume o fade-in ao receber activeSceneChanged.
            var reveal = TransicaoPreta.Criar(fadeDuration);
            var operation = TryLoadScene(path);
            if (operation == null)
            {
                pendingLanding = null;
                Destroy(reveal.gameObject);
                for (float t=0;t<fadeDuration;t+=Time.unscaledDeltaTime)
                {
                    curtain.color = new Color(0,0,0,1-Mathf.SmoothStep(0,1,t/fadeDuration));
                    yield return null;
                }
                Destroy(gameObject);
                yield break;
            }
            yield return operation;
            LunarLandingController controller = null;
            if (landing)
            {
                foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                {
                    controller = root.GetComponentInChildren<LunarLandingController>();
                    if (controller) break;
                }
                if (controller) controller.PrepareLanding();
                else Debug.LogError("Cena de pouso sem LunarLandingController: " + path);
            }
            curtain.enabled = false;
            // TransicaoPreta aguarda dois quadros antes de revelar.
            yield return null; yield return null;
            float end = Time.unscaledTime + fadeDuration;
            while (Time.unscaledTime < end) yield return null;
            pendingLanding = null;
            if (controller) controller.BeginLanding();
            active = null;
            Destroy(gameObject);
        }

        static AsyncOperation TryLoadScene(string path)
        {
            try { return SceneManager.LoadSceneAsync(path,LoadSceneMode.Single); }
            catch (System.Exception error)
            {
                Debug.LogError("Falha ao carregar cena do pouso: " + path + "\n" + error.Message);
                return null;
            }
        }

        void OnDestroy() { if (active == this) { active = null; pendingLanding = null; } }
    }
}
