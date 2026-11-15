using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace AstroLab.Settings
{
    /// <summary>
    /// Aplica as configurações globais em TODAS as cenas (criado sozinho antes da primeira cena):
    /// resolução/modo de tela, qualidade, VSync, limite de FPS, volume geral, anti-aliasing das câmeras,
    /// brilho (exposição) e contador de FPS. Ninguém precisa colocar este componente na cena.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameSettingsRuntime : MonoBehaviour
    {
        static GameSettingsRuntime instancia;

        Volume volumeBrilho;
        ColorAdjustments ajusteBrilho;
        float fpsSuavizado;
        GUIStyle estiloFps;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Criar()
        {
            if (instancia != null) return;
            var go = new GameObject("[AstroLab] Configurações");
            go.hideFlags = HideFlags.DontSave;
            DontDestroyOnLoad(go);
            instancia = go.AddComponent<GameSettingsRuntime>();
        }

        void Awake()
        {
            CriarVolumeBrilho();
            GameSettings.OnChanged += AplicarTudo;
            SceneManager.sceneLoaded += AoCarregarCena;
            AplicarTela();
            AplicarTudo();
        }

        void OnDestroy()
        {
            GameSettings.OnChanged -= AplicarTudo;
            SceneManager.sceneLoaded -= AoCarregarCena;
            if (ajusteBrilho != null) Destroy(volumeBrilho.profile);
            if (instancia == this) instancia = null;
        }

        void AoCarregarCena(Scene s, LoadSceneMode m) => AplicarCameras();

        Vector2Int ultimaResolucao = new Vector2Int(-1, -1);
        FullScreenMode ultimoModo = (FullScreenMode)(-1);
        int ultimaQualidade = -1;

        void AplicarTudo()
        {
            AudioListener.volume = GameSettings.MasterVolume;
            AplicarTela();
            AplicarCameras();
            if (ajusteBrilho != null)
            {
                float b = GameSettings.Brightness;
                ajusteBrilho.postExposure.overrideState = Mathf.Abs(b) > 0.001f;
                ajusteBrilho.postExposure.value = b * 1.25f;          // -1..1 -> -1,25..+1,25 EV
            }
        }

        void AplicarTela()
        {
            Application.targetFrameRate = GameSettings.VSync || GameSettings.FrameRateLimit <= 0 ? -1 : GameSettings.FrameRateLimit;
#if !UNITY_EDITOR
            // No Editor estes valores ficariam gravados no ProjectSettings: só valem na build.
            QualitySettings.vSyncCount = GameSettings.VSync ? 1 : 0;
            int q = GameSettings.QualityLevel;
            if (q != ultimaQualidade) { QualitySettings.SetQualityLevel(q, true); ultimaQualidade = q; }

            var res = GameSettings.Resolution;
            var modo = GameSettings.ScreenMode;
            if (res != ultimaResolucao || modo != ultimoModo)
            {
                int w = res.x > 0 ? res.x : Screen.currentResolution.width;
                int h = res.y > 0 ? res.y : Screen.currentResolution.height;
                Screen.SetResolution(w, h, modo);
                ultimaResolucao = res; ultimoModo = modo;
            }
#endif
        }

        void AplicarCameras()
        {
            var aa = GameSettings.AntiAliasing switch
            {
                AntiAliasingMode.FXAA => AntialiasingMode.FastApproximateAntialiasing,
                AntiAliasingMode.SMAA => AntialiasingMode.SubpixelMorphologicalAntiAliasing,
                _ => AntialiasingMode.None,
            };
            foreach (var cam in Camera.allCameras)
            {
                if (!cam.TryGetComponent<UniversalAdditionalCameraData>(out var dados)) continue;
                if (dados.renderType != CameraRenderType.Base) continue;
                dados.antialiasing = aa;
            }
        }

        void CriarVolumeBrilho()
        {
            volumeBrilho = gameObject.AddComponent<Volume>();
            volumeBrilho.isGlobal = true;
            volumeBrilho.priority = 1000f;
            var perfil = ScriptableObject.CreateInstance<VolumeProfile>();
            perfil.hideFlags = HideFlags.DontSave;
            ajusteBrilho = perfil.Add<ColorAdjustments>(false);
            ajusteBrilho.active = true;
            volumeBrilho.sharedProfile = perfil;
        }

        void Update()
        {
            if (!GameSettings.ShowFps) return;
            float dt = Time.unscaledDeltaTime;
            if (dt > 0f) fpsSuavizado = fpsSuavizado <= 0f ? 1f / dt : Mathf.Lerp(fpsSuavizado, 1f / dt, 0.05f);
        }

        void OnGUI()
        {
            if (!GameSettings.ShowFps) return;
            estiloFps ??= new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(Screen.height / 45f), fontStyle = FontStyle.Bold };
            var r = new Rect(12, 8, 200, 40);
            estiloFps.normal.textColor = new Color(0, 0, 0, 0.6f);
            GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), $"{fpsSuavizado:0} FPS", estiloFps);
            estiloFps.normal.textColor = new Color(0.55f, 0.95f, 1f, 0.95f);
            GUI.Label(r, $"{fpsSuavizado:0} FPS", estiloFps);
        }
    }
}
