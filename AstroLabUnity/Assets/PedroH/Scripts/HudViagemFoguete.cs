using AstroLab.Capacete;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AstroLab.PedroH
{
    /// <summary>
    /// Gerencia o HUD simplificado da viagem espacial Terra -> Lua na cena NaveEspacial:
    /// mantém oxigênio em 100%, progresso rumo à Lua, dica clara de controles e
    /// avisos centrais temporários (desacoplamento, alerta antecipado de asteroides e chegada).
    /// Sem qualquer referência à etapa de frenagem orbital nesta cena.
    /// </summary>
    [DisallowMultipleComponent]
    public class HudViagemFoguete : MonoBehaviour
    {
        [Header("Controlador e Sistemas da Viagem")]
        public ViagemLunarController viagemController;
        public GerenciadorAsteroidesViagem gerenciadorAsteroides;

        [Header("Indicadores Essenciais do HUD")]
        public TMP_Text oxigenioValorTexto;
        public Image oxigenioBarra;
        public TMP_Text progressoLuaValorTexto;
        public Image progressoLuaBarra;
        public TMP_Text dicaControlesTexto;

        [Header("Elementos Ocultos na Fase (HUD Simplificado)")]
        public GameObject[] elementosOcultarNaFase;

        [Header("Aviso Central Temporário (Desacoplamento / Asteroides)")]
        public GameObject painelAvisoCentral;
        public CanvasGroup canvasGroupAviso;
        public TMP_Text tituloAvisoTexto;
        public TMP_Text subtituloAvisoTexto;

        [Min(1f)] public float duracaoAvisoDesacoplar = 5.5f;
        [Min(0.4f)] public float duracaoConfirmacaoDesacoplado = 1.25f;
        [Min(0.8f)] public float duracaoAlertaAsteroide = 6.0f;
        [Min(0.8f)] public float duracaoImpactoAsteroide = 1.6f;
        [Min(1f)] public float velocidadeFade = 7f;

        private MinigameSeparacaoEstagios estagiosVinculados;
        private GerenciadorAsteroidesViagem asteroidesVinculados;
        private ViagemLunarController controllerEventos;

        private bool avisoVisivel;
        private bool avisoDeDesacoplamentoAtivo;
        private bool avisoDeAsteroideRotaAtivo;
        private AsteroideObstaculo ultimoAsteroideAlertadoNoHud;
        private float tempoRestanteAviso;
        private int ultimoPercentualExibido = -1;

        private void Awake()
        {
            AutoVincular();
            AplicarSimplificacaoHud();
            OcultarAvisoImediatamente();
            AtualizarIndicadoresFixos();
        }

        private void OnEnable()
        {
            AutoVincular();
            AplicarSimplificacaoHud();
            VincularEventos();
        }

        private void Start()
        {
            AutoVincular();
            AplicarSimplificacaoHud();
            VincularEventos();
            AtualizarIndicadoresFixos();
        }

        private void OnDisable()
        {
            DesvincularEventos();
        }

        private void Update()
        {
            if (viagemController == null)
                AutoVincular();

            if (viagemController != null &&
                (controllerEventos != viagemController ||
                 (viagemController.minigameEstagios != null && estagiosVinculados != viagemController.minigameEstagios) ||
                 (gerenciadorAsteroides != null && asteroidesVinculados != gerenciadorAsteroides)))
            {
                VincularEventos();
            }

            AtualizarProgressoLua();
            VerificarAlertaAsteroidePendente();
            AtualizarAvisoTemporario(Time.deltaTime);
        }

        private void VerificarAlertaAsteroidePendente()
        {
            if (avisoDeDesacoplamentoAtivo)
                return;

            if (avisoDeAsteroideRotaAtivo && gerenciadorAsteroides != null && gerenciadorAsteroides.AsteroideAlertaAtual != null)
            {
                if (subtituloAvisoTexto != null)
                    subtituloAvisoTexto.text = FormatarDicaDesvio(gerenciadorAsteroides.DirecaoSugeridaDesvio);
                return;
            }

            if (avisoVisivel)
                return;

            if (gerenciadorAsteroides != null &&
                gerenciadorAsteroides.AsteroideAlertaAtual != null &&
                gerenciadorAsteroides.AsteroideAlertaAtual != ultimoAsteroideAlertadoNoHud)
            {
                AoDetectarAsteroideNaRota(
                    gerenciadorAsteroides.AsteroideAlertaAtual,
                    gerenciadorAsteroides.DirecaoSugeridaDesvio);
            }
        }

        /// <summary>
        /// Exibe manualmente o aviso central temporário no centro da tela.
        /// </summary>
        public void ExibirAvisoTemporario(string titulo, string subtitulo, Color corTitulo, float duracaoSegundos)
        {
            if (painelAvisoCentral == null)
                return;

            painelAvisoCentral.SetActive(true);

            if (tituloAvisoTexto != null)
            {
                tituloAvisoTexto.text = titulo;
                tituloAvisoTexto.color = corTitulo;
            }

            if (subtituloAvisoTexto != null)
                subtituloAvisoTexto.text = subtitulo;

            avisoVisivel = true;
            tempoRestanteAviso = Mathf.Max(0.4f, duracaoSegundos);

            if (canvasGroupAviso != null)
                canvasGroupAviso.alpha = Mathf.Max(canvasGroupAviso.alpha, 0.15f);
        }

        public void OcultarAvisoImediatamente()
        {
            avisoVisivel = false;
            avisoDeDesacoplamentoAtivo = false;
            avisoDeAsteroideRotaAtivo = false;
            ultimoAsteroideAlertadoNoHud = null;
            tempoRestanteAviso = 0f;

            if (canvasGroupAviso != null)
                canvasGroupAviso.alpha = 0f;

            if (painelAvisoCentral != null)
            {
                painelAvisoCentral.transform.localScale = Vector3.one;
                painelAvisoCentral.SetActive(false);
            }
        }

        private void AutoVincular()
        {
            if (viagemController == null)
                viagemController = Object.FindAnyObjectByType<ViagemLunarController>();

            if (gerenciadorAsteroides == null && viagemController != null)
                gerenciadorAsteroides = viagemController.gerenciadorAsteroides != null
                    ? viagemController.gerenciadorAsteroides
                    : viagemController.GetComponent<GerenciadorAsteroidesViagem>();

            if (painelAvisoCentral != null && canvasGroupAviso == null)
                canvasGroupAviso = painelAvisoCentral.GetComponent<CanvasGroup>();
        }

        private void AplicarSimplificacaoHud()
        {
            if (elementosOcultarNaFase == null)
                return;

            for (int i = 0; i < elementosOcultarNaFase.Length; i++)
            {
                if (elementosOcultarNaFase[i] != null && elementosOcultarNaFase[i].activeSelf)
                    elementosOcultarNaFase[i].SetActive(false);
            }
        }

        private void AtualizarIndicadoresFixos()
        {
            if (oxigenioValorTexto != null)
                oxigenioValorTexto.text = "100<size=65%>%</size>";

            if (oxigenioBarra != null)
            {
                oxigenioBarra.fillAmount = 1f;
                oxigenioBarra.color = TemaCapacete.Ciano;
            }

            if (dicaControlesTexto != null)
                dicaControlesTexto.text = ControleTrajetoriaJogador.TextoResumoControles;
        }

        private void AtualizarProgressoLua()
        {
            if (viagemController == null)
                return;

            int percentual = Mathf.Clamp(Mathf.RoundToInt(viagemController.ProgressoViagem01 * 100f), 0, 100);
            if (percentual != ultimoPercentualExibido)
            {
                ultimoPercentualExibido = percentual;
                if (progressoLuaValorTexto != null)
                    progressoLuaValorTexto.text = percentual + "<size=65%>%</size>";
            }

            if (progressoLuaBarra != null)
            {
                progressoLuaBarra.fillAmount = Mathf.Clamp01(viagemController.ProgressoViagem01);
                progressoLuaBarra.color = percentual >= 100 ? TemaCapacete.Hex("59FF86") : TemaCapacete.Ciano;
            }
        }

        private void AtualizarAvisoTemporario(float deltaTime)
        {
            if (painelAvisoCentral == null)
                return;

            if (avisoDeDesacoplamentoAtivo && estagiosVinculados != null && !estagiosVinculados.JanelaAtualAtiva)
            {
                avisoVisivel = false;
                avisoDeDesacoplamentoAtivo = false;
            }

            if (avisoVisivel)
            {
                tempoRestanteAviso -= Mathf.Max(0f, deltaTime);
                if (tempoRestanteAviso <= 0f)
                {
                    avisoVisivel = false;
                    avisoDeDesacoplamentoAtivo = false;
                    avisoDeAsteroideRotaAtivo = false;
                }
            }

            if (canvasGroupAviso != null)
            {
                float alvoAlpha = avisoVisivel ? 1f : 0f;
                canvasGroupAviso.alpha = Mathf.MoveTowards(canvasGroupAviso.alpha, alvoAlpha, velocidadeFade * Mathf.Max(0.001f, deltaTime));

                if (!avisoVisivel && canvasGroupAviso.alpha <= 0.01f && painelAvisoCentral.activeSelf)
                {
                    painelAvisoCentral.transform.localScale = Vector3.one;
                    painelAvisoCentral.SetActive(false);
                    return;
                }
            }
            else if (!avisoVisivel && painelAvisoCentral.activeSelf)
            {
                painelAvisoCentral.SetActive(false);
                return;
            }

            if (avisoVisivel && Application.isPlaying)
            {
                float pulso = 1f + Mathf.Sin(Time.time * 4.5f) * 0.025f;
                painelAvisoCentral.transform.localScale = new Vector3(pulso, pulso, 1f);
            }
        }

        private void VincularEventos()
        {
            DesvincularEventos();

            if (viagemController == null)
                return;

            controllerEventos = viagemController;
            controllerEventos.aoIniciarSubidaDaTerra.AddListener(AoIniciarNovaSubida);
            controllerEventos.aoChegarNaLua.AddListener(AoChegarNaLua);

            estagiosVinculados = viagemController.minigameEstagios;
            if (estagiosVinculados != null)
            {
                estagiosVinculados.aoLiberarJanelaSeparacao.AddListener(AoLiberarJanelaDesacoplamento);
                estagiosVinculados.aoSepararEstagioComSucesso.AddListener(AoDesacoplarManualComSucesso);
                estagiosVinculados.aoAutoSepararEstagio.AddListener(AoAutoDesacoplarEstagio);
            }

            asteroidesVinculados = gerenciadorAsteroides;
            if (asteroidesVinculados != null)
            {
                asteroidesVinculados.aoDetectarAsteroideNaRota.AddListener(AoDetectarAsteroideNaRota);
                asteroidesVinculados.aoLimparAlertaAsteroide.AddListener(AoLimparAlertaAsteroide);
                asteroidesVinculados.aoColidirComAsteroide.AddListener(AoColidirComAsteroide);
            }
        }

        private void DesvincularEventos()
        {
            if (controllerEventos != null)
            {
                controllerEventos.aoIniciarSubidaDaTerra.RemoveListener(AoIniciarNovaSubida);
                controllerEventos.aoChegarNaLua.RemoveListener(AoChegarNaLua);
                controllerEventos = null;
            }

            if (estagiosVinculados != null)
            {
                estagiosVinculados.aoLiberarJanelaSeparacao.RemoveListener(AoLiberarJanelaDesacoplamento);
                estagiosVinculados.aoSepararEstagioComSucesso.RemoveListener(AoDesacoplarManualComSucesso);
                estagiosVinculados.aoAutoSepararEstagio.RemoveListener(AoAutoDesacoplarEstagio);
                estagiosVinculados = null;
            }

            if (asteroidesVinculados != null)
            {
                asteroidesVinculados.aoDetectarAsteroideNaRota.RemoveListener(AoDetectarAsteroideNaRota);
                asteroidesVinculados.aoLimparAlertaAsteroide.RemoveListener(AoLimparAlertaAsteroide);
                asteroidesVinculados.aoColidirComAsteroide.RemoveListener(AoColidirComAsteroide);
                asteroidesVinculados = null;
            }
        }

        private void AoLiberarJanelaDesacoplamento(int indice)
        {
            avisoDeAsteroideRotaAtivo = false;
            avisoDeDesacoplamentoAtivo = true;
            ExibirAvisoTemporario(
                "HORA DE DESACOPLAR!",
                "Pressione [E] para soltar o estágio " + (indice + 1),
                TemaCapacete.Alerta,
                duracaoAvisoDesacoplar);
        }

        private void AoDesacoplarManualComSucesso(int indice)
        {
            avisoDeDesacoplamentoAtivo = false;
            avisoDeAsteroideRotaAtivo = false;
            ExibirAvisoTemporario(
                "ESTÁGIO DESACOPLADO!",
                "Impulso extra ativado!",
                TemaCapacete.Hex("59FF86"),
                duracaoConfirmacaoDesacoplado);
        }

        private void AoAutoDesacoplarEstagio(int indice)
        {
            if (!avisoDeDesacoplamentoAtivo)
                return;

            avisoDeDesacoplamentoAtivo = false;
            avisoVisivel = false;
            tempoRestanteAviso = 0f;
        }

        private void AoDetectarAsteroideNaRota(AsteroideObstaculo asteroide, Vector2 direcaoSugerida)
        {
            if (avisoDeDesacoplamentoAtivo)
                return;

            avisoDeAsteroideRotaAtivo = true;
            ultimoAsteroideAlertadoNoHud = asteroide;
            string dica = FormatarDicaDesvio(direcaoSugerida);
            ExibirAvisoTemporario(
                "ASTEROIDE À FRENTE!",
                dica,
                TemaCapacete.Alerta,
                duracaoAlertaAsteroide);
        }

        private void AoLimparAlertaAsteroide()
        {
            ultimoAsteroideAlertadoNoHud = null;
            if (!avisoDeAsteroideRotaAtivo)
                return;

            avisoDeAsteroideRotaAtivo = false;
            avisoVisivel = false;
            tempoRestanteAviso = 0f;
        }

        private void AoIniciarNovaSubida()
        {
            if (!avisoDeDesacoplamentoAtivo)
                OcultarAvisoImediatamente();
        }

        private void AoColidirComAsteroide(AsteroideObstaculo asteroide)
        {
            if (avisoDeDesacoplamentoAtivo)
                return;

            avisoDeAsteroideRotaAtivo = false;
            ExibirAvisoTemporario(
                "ESBARRÃO EM ASTEROIDE!",
                "Use WASD ou Setas para desviar da rota",
                TemaCapacete.Alerta,
                duracaoImpactoAsteroide);
        }

        private void AoChegarNaLua()
        {
            avisoDeDesacoplamentoAtivo = false;
            avisoDeAsteroideRotaAtivo = false;
            ExibirAvisoTemporario(
                "CHEGAMOS À LUA!",
                "Viagem concluída com sucesso!",
                TemaCapacete.Hex("59FF86"),
                4.5f);
        }

        private static string FormatarDicaDesvio(Vector2 dir)
        {
            if (Mathf.Abs(dir.x) >= Mathf.Abs(dir.y))
                return dir.x >= 0f ? "Desvie para a direita [D / ->]" : "Desvie para a esquerda [A / <-]";

            return dir.y >= 0f ? "Suba para desviar [W / Cima]" : "Desça para desviar [S / Baixo]";
        }
    }
}
