using System;
using System.Collections.Generic;
using System.Linq;
using AstroLab.Localization;
using AstroLab.Settings;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AstroLab.Menu
{
    /// <summary>
    /// Painel de Opções (Gráficos / Áudio / Controles / Jogo). As linhas são montadas em runtime a partir do
    /// <see cref="TemaMenu"/> e ligadas direto ao <see cref="GameSettings"/>: cada mudança vale na hora e é salva.
    /// Funciona sozinho: pode ser reaproveitado num menu de pausa (prefab Assets/MenuInicial/Prefabs/PainelOpcoes).
    /// </summary>
    public sealed class PainelOpcoes : MonoBehaviour
    {
        [SerializeField] TemaMenu tema;
        [SerializeField] BotaoMenu[] abas = new BotaoMenu[4];
        [SerializeField] RectTransform[] paginas = new RectTransform[4];
        [SerializeField] BotaoMenu botaoRestaurar, botaoVoltar;

        /// <summary>Clique em Voltar (ou Esc, via <see cref="Voltar"/>).</summary>
        public event Action AoVoltar;

        int abaAtual;
        bool montado;
        readonly List<Action> atualizadores = new List<Action>();
        readonly Dictionary<int, Selectable> primeiraDaAba = new Dictionary<int, Selectable>();
        List<Vector2Int> resolucoes;
        Image[] marcasAba;

        public void Configurar(TemaMenu tema, BotaoMenu[] abas, RectTransform[] paginas, BotaoMenu restaurar, BotaoMenu voltar)
        {
            this.tema = tema; this.abas = abas; this.paginas = paginas; botaoRestaurar = restaurar; botaoVoltar = voltar;
        }

        void Awake() => Montar();

        void OnEnable()
        {
            GameSettings.OnChanged += AtualizarTudo;
            Loc.OnLanguageChanged += AoTrocarIdioma;
            if (montado) { AtualizarTudo(); MostrarAba(abaAtual, false); }
        }

        void OnDisable()
        {
            GameSettings.OnChanged -= AtualizarTudo;
            Loc.OnLanguageChanged -= AoTrocarIdioma;
            GameSettings.Save();
        }

        public void Voltar() => AoVoltar?.Invoke();

        /// <summary>Seleciona o primeiro controle (navegação por teclado/controle).</summary>
        public void Focar()
        {
            if (EventSystem.current && abas.Length > 0 && abas[abaAtual])
                EventSystem.current.SetSelectedGameObject(abas[abaAtual].gameObject);
        }

        void Montar()
        {
            if (montado) return;
            montado = true;
            for (int i = 0; i < abas.Length; i++)
            {
                int k = i;
                abas[i].Botao.onClick.AddListener(() => MostrarAba(k, true));
            }
            botaoRestaurar.Botao.onClick.AddListener(() => { GameSettings.ResetToDefaults(); AtualizarTudo(); });
            botaoVoltar.Botao.onClick.AddListener(Voltar);

            marcasAba = new Image[abas.Length];
            for (int i = 0; i < abas.Length; i++)
            {
                var marca = FabricaUI.Imagem("MarcaAtiva", abas[i].transform, tema.arredondado, tema.destaque);
                marca.rectTransform.anchorMin = new Vector2(0.18f, 0); marca.rectTransform.anchorMax = new Vector2(0.82f, 0);
                marca.rectTransform.sizeDelta = new Vector2(0, 5); marca.rectTransform.anchoredPosition = new Vector2(0, -9);
                marcasAba[i] = marca;
            }

            MontarGraficos(paginas[0]);
            MontarAudio(paginas[1]);
            MontarControles(paginas[2]);
            MontarJogo(paginas[3]);
            AtualizarTudo();
            MostrarAba(0, false);
        }

        void MostrarAba(int i, bool focarConteudo)
        {
            abaAtual = i;
            for (int k = 0; k < paginas.Length; k++)
            {
                paginas[k].gameObject.SetActive(k == i);
                if (marcasAba != null) marcasAba[k].enabled = k == i;
            }
            LigarNavegacao();
            if (focarConteudo && primeiraDaAba.TryGetValue(i, out var s) && EventSystem.current)
                EventSystem.current.SetSelectedGameObject(s.gameObject);
        }

        // ---------------------------------------------------------------- páginas
        void MontarGraficos(RectTransform p)
        {
            var modo = Seletor(p, 0, "opcoes.modo_tela");
            var modos = new[] { FullScreenMode.FullScreenWindow, FullScreenMode.ExclusiveFullScreen, FullScreenMode.Windowed };
            modo.AoMudar += i => GameSettings.ScreenMode = modos[i];
            atualizadores.Add(() => modo.DefinirOpcoes(
                new[] { Loc.Get("opcoes.tela.cheia"), Loc.Get("opcoes.tela.exclusiva"), Loc.Get("opcoes.tela.janela") },
                Mathf.Max(0, Array.IndexOf(modos, GameSettings.ScreenMode))));

            var res = Seletor(p, 0, "opcoes.resolucao");
            resolucoes = Screen.resolutions.Select(r => new Vector2Int(r.width, r.height)).Distinct()
                .Where(r => r.x >= 1024).OrderByDescending(r => r.x * r.y).ToList();
            resolucoes.Insert(0, Vector2Int.zero);
            res.AoMudar += i => GameSettings.Resolution = resolucoes[i];
            atualizadores.Add(() =>
            {
                var nativa = Screen.currentResolution;
                var nomes = resolucoes.Select(r => r == Vector2Int.zero
                    ? Loc.Get("opcoes.resolucao.nativa", $"{nativa.width}×{nativa.height}") : $"{r.x} × {r.y}").ToList();
                res.DefinirOpcoes(nomes, Mathf.Max(0, resolucoes.IndexOf(GameSettings.Resolution)));
            });

            var qual = Seletor(p, 0, "opcoes.qualidade");
            qual.AoMudar += i => GameSettings.QualityLevel = i;
            atualizadores.Add(() => qual.DefinirOpcoes(
                QualitySettings.names.Select(n => Loc.Has("qualidade." + n) ? Loc.Get("qualidade." + n) : n).ToList(), GameSettings.QualityLevel));

            var vs = LigaDesliga(p, 0, "opcoes.vsync", () => GameSettings.VSync, v => GameSettings.VSync = v);

            var fps = Seletor(p, 0, "opcoes.limite_fps");
            int[] limites = { 0, 30, 60, 120, 144, 240 };
            fps.AoMudar += i => GameSettings.FrameRateLimit = limites[i];
            atualizadores.Add(() => fps.DefinirOpcoes(limites.Select(l => l == 0 ? Loc.Get("opcoes.sem_limite") : l.ToString()).ToList(),
                Mathf.Max(0, Array.IndexOf(limites, GameSettings.FrameRateLimit))));
            atualizadores.Add(() => fps.interactable = !GameSettings.VSync);

            var aa = Seletor(p, 0, "opcoes.antialiasing");
            aa.AoMudar += i => GameSettings.AntiAliasing = (AntiAliasingMode)i;
            atualizadores.Add(() => aa.DefinirOpcoes(new[] { Loc.Get("opcoes.desligado"), "FXAA", "SMAA" }, (int)GameSettings.AntiAliasing));

            var brilho = Slider(p, 0, "opcoes.brilho", -1f, 1f, false);
            brilho.Formatar = v => (v >= 0 ? "+" : "") + Mathf.RoundToInt(v * 100f) + "%";
            brilho.AoMudar += v => GameSettings.Brightness = v;
            atualizadores.Add(() => brilho.DefinirValor(GameSettings.Brightness));
        }

        void MontarAudio(RectTransform p)
        {
            Volume(p, 1, "opcoes.volume_geral", () => GameSettings.MasterVolume, v => GameSettings.MasterVolume = v);
            Volume(p, 1, "opcoes.volume_musica", () => GameSettings.MusicVolume, v => GameSettings.MusicVolume = v);
            Volume(p, 1, "opcoes.volume_efeitos", () => GameSettings.SfxVolume, v => GameSettings.SfxVolume = v);
        }

        void MontarControles(RectTransform p)
        {
            var sens = Slider(p, 2, "opcoes.sensibilidade", GameSettings.MinMouseSensitivity, GameSettings.MaxMouseSensitivity, false);
            sens.Formatar = v => v.ToString("0.00") + "x";
            sens.AoMudar += v => GameSettings.MouseSensitivity = Mathf.Round(v * 20f) / 20f;
            atualizadores.Add(() => sens.DefinirValor(GameSettings.MouseSensitivity));

            LigaDesliga(p, 2, "opcoes.inverter_y", () => GameSettings.InvertY, v => GameSettings.InvertY = v);

            var fov = Slider(p, 2, "opcoes.fov", GameSettings.MinFieldOfView, GameSettings.MaxFieldOfView, true);
            fov.Formatar = v => Mathf.RoundToInt(v) + "°";
            fov.AoMudar += v => GameSettings.FieldOfView = v;
            atualizadores.Add(() => fov.DefinirValor(GameSettings.FieldOfView));

            var cab = FabricaUI.Texto("Cabecalho_Teclas", p, tema, "opcoes.teclas", 22f, TMPro.TextAlignmentOptions.BottomLeft, true);
            cab.color = tema.textoSecundario; cab.characterSpacing = 6f;
            var le = cab.gameObject.AddComponent<LayoutElement>(); le.preferredHeight = 40f;
            cab.margin = new Vector4(22, 0, 0, 4);

            FabricaUI.LinhaInfo(p, tema, "tecla.mover", "W  A  S  D");
            FabricaUI.LinhaInfo(p, tema, "tecla.olhar", "@tecla.mouse");
            FabricaUI.LinhaInfo(p, tema, "tecla.correr", "SHIFT");
            FabricaUI.LinhaInfo(p, tema, "tecla.pular", "@tecla.espaco");
            FabricaUI.LinhaInfo(p, tema, "tecla.interagir", "E");
            FabricaUI.LinhaInfo(p, tema, "tecla.pausa", "ESC");
        }

        void MontarJogo(RectTransform p)
        {
            var idioma = Seletor(p, 3, "opcoes.idioma");
            idioma.AoMudar += i => Loc.Language = Loc.Languages[i].Code;
            atualizadores.Add(() => idioma.DefinirOpcoes(Loc.Languages.Select(l => l.NativeName).ToList(), Loc.IndexOf(Loc.Language)));

            LigaDesliga(p, 3, "opcoes.mostrar_fps", () => GameSettings.ShowFps, v => GameSettings.ShowFps = v);
        }

        // ---------------------------------------------------------------- helpers
        SeletorOpcao Seletor(RectTransform p, int aba, string chave)
        {
            var s = FabricaUI.Seletor(p, tema, chave);
            if (!primeiraDaAba.ContainsKey(aba)) primeiraDaAba[aba] = s;
            return s;
        }

        SliderOpcao Slider(RectTransform p, int aba, string chave, float min, float max, bool inteiro)
        {
            var s = FabricaUI.Slider(p, tema, chave, min, max, inteiro);
            if (!primeiraDaAba.ContainsKey(aba)) primeiraDaAba[aba] = s.Slider;
            return s;
        }

        SeletorOpcao LigaDesliga(RectTransform p, int aba, string chave, Func<bool> ler, Action<bool> gravar)
        {
            var s = Seletor(p, aba, chave);
            s.AoMudar += i => gravar(i == 1);
            atualizadores.Add(() => s.DefinirOpcoes(new[] { Loc.Get("opcoes.desligado"), Loc.Get("opcoes.ligado") }, ler() ? 1 : 0));
            return s;
        }

        void Volume(RectTransform p, int aba, string chave, Func<float> ler, Action<float> gravar)
        {
            var s = Slider(p, aba, chave, 0f, 100f, true);
            s.Formatar = v => Mathf.RoundToInt(v) + "%";
            s.AoMudar += v => gravar(v / 100f);
            atualizadores.Add(() => s.DefinirValor(ler() * 100f));
        }

        void AtualizarTudo()
        {
            foreach (var a in atualizadores) a();
        }

        void AoTrocarIdioma() => AtualizarTudo();

        /// <summary>Navegação explícita: abas na horizontal, conteúdo da aba na vertical, rodapé embaixo.</summary>
        void LigarNavegacao()
        {
            var conteudo = paginas[abaAtual].GetComponentsInChildren<Selectable>(false)
                .Where(s => s.transform.parent == paginas[abaAtual]).ToList();
            Selectable primeiro = conteudo.Count > 0 ? conteudo[0] : botaoVoltar.Botao;
            Selectable ultimo = conteudo.Count > 0 ? conteudo[conteudo.Count - 1] : null;

            for (int i = 0; i < abas.Length; i++)
            {
                var n = new Navigation { mode = Navigation.Mode.Explicit };
                n.selectOnLeft = abas[(i + abas.Length - 1) % abas.Length].Botao;
                n.selectOnRight = abas[(i + 1) % abas.Length].Botao;
                n.selectOnDown = primeiro;
                abas[i].Botao.navigation = n;
            }
            for (int i = 0; i < conteudo.Count; i++)
            {
                var n = new Navigation { mode = Navigation.Mode.Explicit };
                n.selectOnUp = i == 0 ? abas[abaAtual].Botao : conteudo[i - 1];
                n.selectOnDown = i == conteudo.Count - 1 ? botaoVoltar.Botao : conteudo[i + 1];
                conteudo[i].navigation = n;
            }
            var nv = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = ultimo ? ultimo : abas[abaAtual].Botao, selectOnLeft = botaoRestaurar.Botao };
            botaoVoltar.Botao.navigation = nv;
            var nr = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = ultimo ? ultimo : abas[abaAtual].Botao, selectOnRight = botaoVoltar.Botao };
            botaoRestaurar.Botao.navigation = nr;
        }
    }
}
