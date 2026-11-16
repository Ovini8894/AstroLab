using System;
using AstroLab.Localization;
using UnityEngine;

namespace AstroLab.Settings
{
    public enum AntiAliasingMode { Off = 0, FXAA = 1, SMAA = 2 }

    /// <summary>
    /// Configurações do jogador (menu Opções), salvas em PlayerPrefs.
    /// Qualquer cena pode LER daqui; quem altera é o menu. A cada mudança dispara <see cref="OnChanged"/>.
    /// Tela/qualidade/VSync/FPS/volume geral/anti-aliasing/brilho são aplicados sozinhos por
    /// <see cref="GameSettingsRuntime"/> em todas as cenas. Sensibilidade, inverter Y, FOV e os volumes de
    /// música/efeitos devem ser lidos pelo controle do jogador e pelas fontes de áudio de cada cena.
    /// </summary>
    public static class GameSettings
    {
        /// <summary>Disparado depois de qualquer alteração (inclusive <see cref="ResetToDefaults"/>).</summary>
        public static event Action OnChanged;

        // ---------------------------------------------------------------- padrões
        public const float DefaultMouseSensitivity = 1f;
        public const float MinMouseSensitivity = 0.1f, MaxMouseSensitivity = 3f;
        public const float DefaultFieldOfView = 75f;
        public const float MinFieldOfView = 60f, MaxFieldOfView = 100f;
        public const float DefaultMasterVolume = 0.8f, DefaultMusicVolume = 0.7f, DefaultSfxVolume = 0.8f;

        const string P = "astrolab.settings.";

        // ---------------------------------------------------------------- controles
        /// <summary>Multiplicador da sensibilidade do mouse (0,1 a 3; padrão 1). Multiplique pela sensibilidade base do seu controle.</summary>
        public static float MouseSensitivity
        {
            get => GetF("mouseSensitivity", DefaultMouseSensitivity);
            set => SetF("mouseSensitivity", Mathf.Clamp(value, MinMouseSensitivity, MaxMouseSensitivity));
        }

        /// <summary>Inverter o eixo vertical da câmera.</summary>
        public static bool InvertY { get => GetB("invertY", false); set => SetB("invertY", value); }

        /// <summary>Campo de visão vertical da câmera do jogador, em graus (60 a 100; padrão 75).</summary>
        public static float FieldOfView
        {
            get => GetF("fov", DefaultFieldOfView);
            set => SetF("fov", Mathf.Clamp(value, MinFieldOfView, MaxFieldOfView));
        }

        // ---------------------------------------------------------------- áudio (0 a 1)
        /// <summary>Volume geral. Já aplicado em AudioListener.volume — não multiplique de novo.</summary>
        public static float MasterVolume { get => GetF("masterVolume", DefaultMasterVolume); set => SetF("masterVolume", Mathf.Clamp01(value)); }
        /// <summary>Volume de música: use em AudioSource.volume das músicas.</summary>
        public static float MusicVolume { get => GetF("musicVolume", DefaultMusicVolume); set => SetF("musicVolume", Mathf.Clamp01(value)); }
        /// <summary>Volume de efeitos sonoros: use em AudioSource.volume dos efeitos.</summary>
        public static float SfxVolume { get => GetF("sfxVolume", DefaultSfxVolume); set => SetF("sfxVolume", Mathf.Clamp01(value)); }

        // ---------------------------------------------------------------- gráficos
        /// <summary>Índice em QualitySettings.names (padrão: o nível atual do projeto).</summary>
        public static int QualityLevel
        {
            get => Mathf.Clamp(GetI("quality", QualitySettings.GetQualityLevel()), 0, Mathf.Max(0, QualitySettings.names.Length - 1));
            set => SetI("quality", value);
        }

        public static FullScreenMode ScreenMode
        {
            get => (FullScreenMode)GetI("screenMode", (int)FullScreenMode.FullScreenWindow);
            set => SetI("screenMode", (int)value);
        }

        /// <summary>Resolução escolhida; (0,0) = resolução nativa do monitor.</summary>
        public static Vector2Int Resolution
        {
            get => new Vector2Int(GetI("resW", 0), GetI("resH", 0));
            set
            {
                if (value == Resolution) return;
                PlayerPrefs.SetInt(P + "resW", value.x); PlayerPrefs.SetInt(P + "resH", value.y); Notify();
            }
        }

        public static bool VSync { get => GetB("vsync", true); set => SetB("vsync", value); }

        /// <summary>Limite de FPS quando o VSync está desligado; 0 = sem limite.</summary>
        public static int FrameRateLimit { get => GetI("fpsLimit", 0); set => SetI("fpsLimit", Mathf.Max(0, value)); }

        public static AntiAliasingMode AntiAliasing { get => (AntiAliasingMode)GetI("aa", (int)AntiAliasingMode.Off); set => SetI("aa", (int)value); }

        /// <summary>Brilho de -1 a 1 (0 = original da cena). Aplicado como exposição pós-processamento.</summary>
        public static float Brightness { get => GetF("brightness", 0f); set => SetF("brightness", Mathf.Clamp(value, -1f, 1f)); }

        /// <summary>Mostrar contador de FPS no canto da tela.</summary>
        public static bool ShowFps { get => GetB("showFps", false); set => SetB("showFps", value); }

        // ---------------------------------------------------------------- idioma
        /// <summary>Atalho para <see cref="Loc.Language"/> (código: "pt-BR", "en", "es").</summary>
        public static string Language { get => Loc.Language; set => Loc.Language = value; }

        // ---------------------------------------------------------------- operações
        /// <summary>Grava em disco (PlayerPrefs.Save). Os valores já valem em memória antes disso.</summary>
        public static void Save() => PlayerPrefs.Save();

        /// <summary>Volta tudo ao padrão (menos o idioma) e dispara <see cref="OnChanged"/> uma vez.</summary>
        public static void ResetToDefaults()
        {
            foreach (var k in new[] { "mouseSensitivity", "invertY", "fov", "masterVolume", "musicVolume", "sfxVolume",
                                      "quality", "screenMode", "resW", "resH", "vsync", "fpsLimit", "aa", "brightness", "showFps" })
                PlayerPrefs.DeleteKey(P + k);
            PlayerPrefs.Save();
            Notify();
        }

        static void Notify() => OnChanged?.Invoke();

        static float GetF(string k, float d) => PlayerPrefs.GetFloat(P + k, d);
        static int GetI(string k, int d) => PlayerPrefs.GetInt(P + k, d);
        static bool GetB(string k, bool d) => PlayerPrefs.GetInt(P + k, d ? 1 : 0) != 0;

        static void SetF(string k, float v)
        {
            if (PlayerPrefs.HasKey(P + k) && Mathf.Approximately(PlayerPrefs.GetFloat(P + k), v)) return;
            PlayerPrefs.SetFloat(P + k, v); Notify();
        }

        static void SetI(string k, int v)
        {
            if (PlayerPrefs.HasKey(P + k) && PlayerPrefs.GetInt(P + k) == v) return;
            PlayerPrefs.SetInt(P + k, v); Notify();
        }

        static void SetB(string k, bool v) => SetI(k, v ? 1 : 0);
    }
}
