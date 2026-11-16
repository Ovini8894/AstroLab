using AstroLab.Capacete;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AstroLab.PedroH
{
    /// <summary>
    /// Gerencia o HUD simplificado da viagem espacial Terra -> Lua na cena NaveEspacial:
    /// mantém apenas o essencial (oxigênio em 100% e progresso rumo à Lua), oculta o painel
    /// permanente de missão ativa e exibe avisos centrais claros e temporários ao desacoplar.
    /// </summary>
    [DisallowMultipleComponent]
    public class HudViagemFoguete : MonoBehaviour
    {
        [Header("Controlador da Viagem")]
        public ViagemLunarController viagemController;

        [Header("Indicadores Essenciais do HUD")]
        public TMP_Text oxigenioValorTexto;
        public Image oxigenioBarra;
        public TMP_Text progressoLuaValorTexto;
        public Image progressoLuaBarra;

        [Header("Elementos Ocultos na Fase (HUD Simplificado)")]
        public GameObject[] elementosOcultarNaFase;

        [Header("Aviso Central Temporário (Desacoplamento / Manobras)")]
        public GameObject painelAvisoCentral;
        public CanvasGroup canvasGroupAviso;
        public TMP_Text tituloAvisoTexto;
        public TMP_Text subtituloAvisoTexto;

        [Min(1f)] public float duracaoAvisoDesacoplar = 5.5f;
        [Min(0.4f)] public float duracaoConfirmacaoDesacoplado = 1.25f;
        [Min(1f)] public float duracaoAvisoFreioLunar = 4.0f;
        [Min(1f)] public float velocidadeFade = 7f;

        private MinigameSeparacaoEstagios estagiosVinculados;
        private MinigameFreioOrbitalLunar freioVinculado;
        private ViagemLunarController controllerEventos;

        private bool avisoVisivel;
        private bool avisoDeDesacoplamentoAtivo;
        private bool avisoDeFreioAtivo;
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
                 (viagemController.minigameFreioOrbital != null && freioVinculado != viagemController.minigameFreioOrbital)))
            {
                VincularEventos();
            }

            AtualizarProgressoLua();
            AtualizarAvisoTemporario(Time.deltaTime);
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
            avisoDeFreioAtivo = false;
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

            if (avisoDeFreioAtivo && freioVinculado != null && freioVinculado.OrbitaEstabilizada)
            {
                avisoVisivel = false;
                avisoDeFreioAtivo = false;
            }

            if (avisoVisivel)
            {
                tempoRestanteAviso -= Mathf.Max(0f, deltaTime);
                if (tempoRestanteAviso <= 0f)
                {
                    avisoVisivel = false;
                    avisoDeDesacoplamentoAtivo = false;
                    avisoDeFreioAtivo = false;
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
            controllerEventos.aoChegarNaLua.AddListener(AoChegarNaLua);

            estagiosVinculados = viagemController.minigameEstagios;
            if (estagiosVinculados != null)
            {
                estagiosVinculados.aoLiberarJanelaSeparacao.AddListener(AoLiberarJanelaDesacoplamento);
                estagiosVinculados.aoSepararEstagioComSucesso.AddListener(AoDesacoplarManualComSucesso);
                estagiosVinculados.aoAutoSepararEstagio.AddListener(AoAutoDesacoplarEstagio);
            }

            freioVinculado = viagemController.minigameFreioOrbital;
            if (freioVinculado != null)
            {
                freioVinculado.aoIniciarAproximacaoRetroFreio.AddListener(AoIniciarFreioLunar);
                freioVinculado.aoEstabilizarOrbitaLunar.AddListener(AoEstabilizarOrbita);
            }
        }

        private void DesvincularEventos()
        {
            if (controllerEventos != null)
            {
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

            if (freioVinculado != null)
            {
                freioVinculado.aoIniciarAproximacaoRetroFreio.RemoveListener(AoIniciarFreioLunar);
                freioVinculado.aoEstabilizarOrbitaLunar.RemoveListener(AoEstabilizarOrbita);
                freioVinculado = null;
            }
        }

        private void AoLiberarJanelaDesacoplamento(int indice)
        {
            avisoDeFreioAtivo = false;
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
            avisoDeFreioAtivo = false;
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

        private void AoIniciarFreioLunar()
        {
            avisoDeDesacoplamentoAtivo = false;
            avisoDeFreioAtivo = true;
            ExibirAvisoTemporario(
                "FREIO ORBITAL LUNAR",
                "Segure [ESPAÇO] para reduzir a velocidade",
                TemaCapacete.Ciano,
                duracaoAvisoFreioLunar);
        }

        private void AoEstabilizarOrbita()
        {
            if (!avisoDeFreioAtivo)
                return;

            avisoDeFreioAtivo = false;
            ExibirAvisoTemporario(
                "ÓRBITA ESTABILIZADA!",
                "Preparando chegada na Lua",
                TemaCapacete.Hex("59FF86"),
                duracaoConfirmacaoDesacoplado);
        }

        private void AoChegarNaLua()
        {
            avisoDeDesacoplamentoAtivo = false;
            avisoDeFreioAtivo = false;
            ExibirAvisoTemporario(
                "CHEGAMOS À LUA!",
                "Viagem concluída com sucesso!",
                TemaCapacete.Hex("59FF86"),
                4.5f);
        }
    }
}
