using System;
using AstroLab.Capacete;
using UnityEngine;
using UnityEngine.Events;

namespace AstroLab.PedroH
{
    /// <summary>
    /// Controlador principal da lógica de gameplay da viagem espacial Terra -> Lua (~2 minutos).
    /// Coordena entrada infantil, trajetória até a Lua, encaixe preparado para o futuro modelo 3D
    /// do foguete (FogueteModeloLogica), os 3 minigames espaciais escolhidos (Estágios, RCS e
    /// Retro-Freio Orbital) e oxigênio sempre em 100%, sem criar UI, modelos 3D ou cenas.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TrajetoriaViagemLunar))]
    [RequireComponent(typeof(ControleTrajetoriaJogador))]
    [RequireComponent(typeof(EncaixeModeloFoguete))]
    public class ViagemLunarController : MonoBehaviour
    {
        public const float OxigenioMaximo = 100f;

        [Header("Módulos Base da Viagem")]
        public TrajetoriaViagemLunar trajetoria;
        public ControleTrajetoriaJogador controleJogador;
        public EncaixeModeloFoguete encaixeFoguete;

        [Header("Minigames Lógicos Integrados (Ideias 1, 3 e 5)")]
        public bool habilitarMinigamesLogicos = true;
        public MinigameSeparacaoEstagios minigameEstagios;
        public MinigameCorrecaoRcs minigameRcs;
        public MinigameFreioOrbitalLunar minigameFreioOrbital;

        [Header("Fluxo da Subida e Viagem")]
        [Tooltip("Se verdadeiro, inicia a subida a partir da Terra automaticamente ao entrar em Play Mode.")]
        public bool iniciarAutomaticamenteAoIniciar = true;

        [Tooltip("Permite controlar a trajetória lateral/vertical da nave durante a subida e o cruzeiro.")]
        public bool permitirControleTrajetoria = true;

        [Header("Integração Opcional com HUD Existente (Sem Criar Interfaces)")]
        [Tooltip("Referência opcional a um HudCapacete já existente na cena para manter oxigênio em 100% e exibir fase/instrução.")]
        public HudCapacete hudCapaceteExistente;
        public bool sincronizarHudExistente = true;
        [Tooltip("Se verdadeiro, preenche o painel permanente de missão do HudCapacete. Mantenha falso ao usar HUD simplificado.")]
        public bool exibirMissaoPermanenteNoHud = false;

        [Header("Eventos da Viagem Espacial (Para Futuros Efeitos, Sons ou Interfaces)")]
        public UnityEvent aoIniciarSubidaDaTerra = new UnityEvent();
        public UnityEvent aoEntrarEmCruzeiroEspacial = new UnityEvent();
        public UnityEvent aoAproximarDaLua = new UnityEvent();
        public UnityEvent aoChegarNaLua = new UnityEvent();

        public event Action<FaseViagemEspacial, FaseViagemEspacial> AoMudarFase;

        /// <summary>
        /// O oxigênio é mantido fixo em 100% — não há consumo nem gerenciamento de oxigênio.
        /// </summary>
        public float NivelOxigenio => OxigenioMaximo;

        public float ProgressoViagem01 => trajetoria != null ? trajetoria.Progresso01 : 0f;
        public float TempoDecorridoSegundos => trajetoria != null ? trajetoria.TempoDecorridoSegundos : 0f;
        public float TempoRestanteEstimadoSegundos => trajetoria != null ? trajetoria.TempoRestanteEstimadoSegundos : 120f;
        public FaseViagemEspacial FaseAtual => trajetoria != null ? trajetoria.FaseAtual : FaseViagemEspacial.ProntaNaTerra;
        public bool ViagemEmAndamento => viagemIniciada && !viagemConcluida;
        public bool ViagemConcluida => viagemConcluida;
        public FogueteModeloLogica FogueteConectado => encaixeFoguete != null ? encaixeFoguete.FogueteConectado : null;

        private bool viagemIniciada;
        private bool viagemConcluida;
        private FaseViagemEspacial ultimaFaseNotificada = FaseViagemEspacial.ProntaNaTerra;

        private void Awake()
        {
            AutoVincularReferencias();
            PrepararNaTerra();
        }

        private void Start()
        {
            ManterOxigenioSempreCheio();

            if (iniciarAutomaticamenteAoIniciar)
                IniciarViagem();
        }

        private void Update()
        {
            ManterOxigenioSempreCheio();

            if (!viagemIniciada || viagemConcluida || trajetoria == null || encaixeFoguete == null)
                return;

            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            PassoDeAtualizacao(dt);
        }

        /// <summary>
        /// Localiza todos os módulos de gameplay (incluindo os 3 minigames) e um HudCapacete
        /// já existente na cena, sem criar UI, cenas ou modelos 3D.
        /// </summary>
        public void AutoVincularReferencias()
        {
            bool criarEmRuntime = Application.isPlaying;

            trajetoria = ResolverComponenteLocal(trajetoria, criarEmRuntime);
            controleJogador = ResolverComponenteLocal(controleJogador, criarEmRuntime);
            encaixeFoguete = ResolverComponenteLocal(encaixeFoguete, criarEmRuntime);

            if (encaixeFoguete != null)
                encaixeFoguete.InicializarPontosDeEncaixe();

            if (habilitarMinigamesLogicos)
            {
                minigameEstagios = ResolverComponenteLocal(minigameEstagios, criarEmRuntime);
                minigameRcs = ResolverComponenteLocal(minigameRcs, criarEmRuntime);
                minigameFreioOrbital = ResolverComponenteLocal(minigameFreioOrbital, criarEmRuntime);
            }

            if (hudCapaceteExistente == null && sincronizarHudExistente)
                hudCapaceteExistente = UnityEngine.Object.FindAnyObjectByType<HudCapacete>();
        }

        /// <summary>
        /// Prepara a nave parada no ponto inicial da Terra (0%), reinicia os 3 minigames e fixa O2 em 100%.
        /// </summary>
        public void PrepararNaTerra()
        {
            AutoVincularReferencias();

            viagemIniciada = false;
            viagemConcluida = false;
            ultimaFaseNotificada = FaseViagemEspacial.ProntaNaTerra;

            if (controleJogador != null)
                controleJogador.ZerarControles();

            if (encaixeFoguete != null)
                encaixeFoguete.ReiniciarModeloNaTerra();

            ReiniciarTodosMinigames();

            if (trajetoria != null)
            {
                trajetoria.PrepararInicioNaTerra();
                if (encaixeFoguete != null)
                {
                    encaixeFoguete.AplicarEstadoCompletoDaNave(
                        trajetoria.PosicaoAtualMundo,
                        trajetoria.OrientacaoBaseMundo,
                        Vector2.zero,
                        1f,
                        0f,
                        0f,
                        0f,
                        0f,
                        0f,
                        0f);
                }
            }

            ManterOxigenioSempreCheio();
            AtualizarHudCapaceteExistente();
        }

        /// <summary>
        /// Inicia a subida da nave a partir da Terra rumo à Lua com duração alvo aproximada de 2 minutos.
        /// </summary>
        public void IniciarViagem()
        {
            AutoVincularReferencias();

            viagemIniciada = true;
            viagemConcluida = false;

            if (controleJogador != null)
                controleJogador.ZerarControles();

            if (encaixeFoguete != null)
                encaixeFoguete.ReiniciarModeloNaTerra();

            ReiniciarTodosMinigames();

            if (trajetoria != null)
            {
                trajetoria.IniciarSubida();
                if (encaixeFoguete != null)
                {
                    encaixeFoguete.AplicarEstadoCompletoDaNave(
                        trajetoria.PosicaoAtualMundo,
                        trajetoria.OrientacaoBaseMundo,
                        Vector2.zero,
                        1f,
                        0f,
                        0.95f,
                        0f,
                        0f,
                        0f,
                        0f);
                }

                ultimaFaseNotificada = FaseViagemEspacial.ProntaNaTerra;
                NotificarMudancaDeFase(FaseViagemEspacial.SubindoDaTerra);
            }

            ManterOxigenioSempreCheio();
            AtualizarHudCapaceteExistente();
        }

        /// <summary>
        /// Executa um passo completo da viagem: leitura de controles, trajetória (~2 min),
        /// atualização dos minigames 1/3/5, envio de estado para o modelo do foguete e oxigênio em 100%.
        /// </summary>
        public void PassoDeAtualizacao(float deltaTime)
        {
            if (!viagemIniciada || viagemConcluida || trajetoria == null || encaixeFoguete == null)
                return;

            Vector2 direcao = Vector2.zero;
            float impulso = 0f;
            bool acaoSeparacao = false;
            bool retroFreioPressionado = false;

            if (controleJogador != null)
            {
                bool podeControlar = permitirControleTrajetoria && trajetoria.EmViagem;
                controleJogador.AtualizarLeituraControles(deltaTime, podeControlar);
                direcao = controleJogador.DirecaoSuave;
                impulso = controleJogador.RitmoImpulsoSuave;
                acaoSeparacao = controleJogador.AcaoSeparacaoPressionada;
                retroFreioPressionado = controleJogador.RetroFreioPressionado;
            }

            float bonusEstagio = minigameEstagios != null ? minigameEstagios.ImpulsoBonusAtual01 : 0f;
            float freioOrbitalAtual = minigameFreioOrbital != null ? minigameFreioOrbital.IntensidadeRetroFreioAtual01 : 0f;
            float ritmoCombinado = Mathf.Clamp(impulso + bonusEstagio - freioOrbitalAtual * 0.25f, -1f, 1f);

            FaseViagemEspacial faseAtualizada = trajetoria.AvancarTrajetoria(direcao, ritmoCombinado, deltaTime);

            if (habilitarMinigamesLogicos)
                AtualizarMinigamesDaFase(deltaTime, acaoSeparacao, retroFreioPressionado);

            CalcularEstadoPropulsaoParaModelo(
                faseAtualizada,
                impulso + bonusEstagio,
                out float motorPrincipal01,
                out float retroFreio01,
                out float fatorGiroRetro01,
                out float tremorExtra01);

            encaixeFoguete.AplicarEstadoCompletoDaNave(
                trajetoria.PosicaoAtualMundo,
                trajetoria.OrientacaoBaseMundo,
                direcao,
                trajetoria.FatorSubidaAtmosferica,
                ritmoCombinado,
                motorPrincipal01,
                retroFreio01,
                fatorGiroRetro01,
                tremorExtra01,
                deltaTime);

            if (faseAtualizada != ultimaFaseNotificada)
                NotificarMudancaDeFase(faseAtualizada);

            ManterOxigenioSempreCheio();
            AtualizarHudCapaceteExistente();

            if (faseAtualizada == FaseViagemEspacial.ChegouNaLua)
                ConcluirViagemNaLua();
        }

        /// <summary>
        /// Reinicia a viagem na Terra e inicia novamente a subida rumo à Lua.
        /// </summary>
        public void ReiniciarViagemDaTerra(bool iniciarSubidaImediatamente = true)
        {
            PrepararNaTerra();
            if (iniciarSubidaImediatamente)
                IniciarViagem();
        }

        /// <summary>
        /// Atalho público para conectar o modelo 3D (ou prefab com FogueteModeloLogica) quando estiver pronto.
        /// </summary>
        public bool ConectarModeloFoguete(GameObject modeloOuPrefab, bool instanciarSeForPrefab = true)
        {
            AutoVincularReferencias();
            return encaixeFoguete != null && encaixeFoguete.ConectarModeloFoguete(modeloOuPrefab, instanciarSeForPrefab);
        }

        /// <summary>
        /// Fachada para futuros botões de UI acionarem a separação de estágios (Ideia 1).
        /// </summary>
        public bool AcionarSeparacaoEstagio()
        {
            return minigameEstagios != null && minigameEstagios.TentarAcionarSeparacao(encaixeFoguete);
        }

        /// <summary>
        /// Fachada para futuros botões seguráveis de UI acionarem o retro-freio lunar (Ideia 5).
        /// </summary>
        public void DefinirRetroFreioPressionado(bool ativo)
        {
            if (controleJogador != null)
                controleJogador.DefinirRetroFreioExterno(ativo);

            if (minigameFreioOrbital != null)
                minigameFreioOrbital.DefinirFreioPressionadoExterno(ativo);
        }

        /// <summary>
        /// Garante que o oxigênio permaneça sempre em 100%, sem qualquer lógica de consumo ou gerenciamento.
        /// </summary>
        public void ManterOxigenioSempreCheio()
        {
            if (hudCapaceteExistente == null || !sincronizarHudExistente)
                return;

            if (!Mathf.Approximately(hudCapaceteExistente.oxigenio, OxigenioMaximo))
                hudCapaceteExistente.DefinirOxigenio(OxigenioMaximo);
        }

        private T ResolverComponenteLocal<T>(T atual, bool criarSeAusente) where T : Component
        {
            if (atual != null)
                return atual;

            T encontrado = GetComponent<T>();
            if (encontrado == null)
                encontrado = GetComponentInChildren<T>(true);

            if (encontrado == null && criarSeAusente)
                encontrado = gameObject.AddComponent<T>();

            return encontrado;
        }

        private void ReiniciarTodosMinigames()
        {
            if (minigameEstagios != null)
                minigameEstagios.ReiniciarMinigame();

            if (minigameRcs != null)
                minigameRcs.ReiniciarMinigame();

            if (minigameFreioOrbital != null)
                minigameFreioOrbital.ReiniciarMinigame();
        }

        private void AtualizarMinigamesDaFase(float deltaTime, bool acaoSeparacao, bool retroFreioPressionado)
        {
            FaseViagemEspacial fase = trajetoria.FaseAtual;
            float progresso = trajetoria.Progresso01;

            if (minigameEstagios != null)
            {
                minigameEstagios.AtualizarMinigame(
                    fase,
                    progresso,
                    trajetoria.duracaoAlvoSegundos,
                    acaoSeparacao,
                    encaixeFoguete,
                    deltaTime);
            }

            if (minigameRcs != null)
            {
                minigameRcs.AtualizarMinigame(
                    fase,
                    progresso,
                    trajetoria.DesvioCorredorMetros,
                    deltaTime);
            }

            if (minigameFreioOrbital != null)
            {
                minigameFreioOrbital.AtualizarMinigame(
                    fase,
                    progresso,
                    retroFreioPressionado,
                    deltaTime);
            }
        }

        private void CalcularEstadoPropulsaoParaModelo(
            FaseViagemEspacial fase,
            float impulsoCombinado,
            out float motorPrincipal01,
            out float retroFreio01,
            out float fatorGiroRetro01,
            out float tremorExtra01)
        {
            retroFreio01 = minigameFreioOrbital != null ? minigameFreioOrbital.IntensidadeRetroFreioAtual01 : 0f;
            fatorGiroRetro01 = minigameFreioOrbital != null ? minigameFreioOrbital.FatorGiroAtitudeRetro01 : 0f;
            tremorExtra01 = minigameRcs != null ? minigameRcs.TremorImpactoAtual01 : 0f;

            switch (fase)
            {
                case FaseViagemEspacial.SubindoDaTerra:
                    motorPrincipal01 = Mathf.Clamp01(0.90f + Mathf.Max(0f, impulsoCombinado) * 0.15f);
                    break;
                case FaseViagemEspacial.CruzeiroEspacial:
                    motorPrincipal01 = Mathf.Clamp01(0.45f + Mathf.Max(0f, impulsoCombinado) * 0.40f);
                    break;
                case FaseViagemEspacial.AproximandoDaLua:
                    motorPrincipal01 = Mathf.Clamp01(Mathf.Max((1f - fatorGiroRetro01) * 0.35f, retroFreio01));
                    break;
                default:
                    motorPrincipal01 = 0f;
                    break;
            }
        }

        private void NotificarMudancaDeFase(FaseViagemEspacial novaFase)
        {
            FaseViagemEspacial faseAnterior = ultimaFaseNotificada;
            ultimaFaseNotificada = novaFase;

            AoMudarFase?.Invoke(faseAnterior, novaFase);

            switch (novaFase)
            {
                case FaseViagemEspacial.SubindoDaTerra:
                    aoIniciarSubidaDaTerra?.Invoke();
                    break;
                case FaseViagemEspacial.CruzeiroEspacial:
                    aoEntrarEmCruzeiroEspacial?.Invoke();
                    break;
                case FaseViagemEspacial.AproximandoDaLua:
                    aoAproximarDaLua?.Invoke();
                    break;
                case FaseViagemEspacial.ChegouNaLua:
                    aoChegarNaLua?.Invoke();
                    break;
            }
        }

        private void ConcluirViagemNaLua()
        {
            viagemConcluida = true;
            viagemIniciada = false;

            if (controleJogador != null)
                controleJogador.ZerarControles();

            ManterOxigenioSempreCheio();
            AtualizarHudCapaceteExistente();
        }

        private void AtualizarHudCapaceteExistente()
        {
            if (!exibirMissaoPermanenteNoHud || hudCapaceteExistente == null || !sincronizarHudExistente || trajetoria == null)
                return;

            int percentual = Mathf.RoundToInt(trajetoria.Progresso01 * 100f);
            string titulo = "VIAGEM ESPACIAL TERRA → LUA (" + percentual + "%)";
            string etapa = ObterInstrucaoAtualParaHud();

            hudCapaceteExistente.DefinirMissao(titulo, etapa, percentual, 100);
        }

        private string ObterInstrucaoAtualParaHud()
        {
            if (minigameEstagios != null && minigameEstagios.EstaAtivo && !string.IsNullOrEmpty(minigameEstagios.InstrucaoAtual))
                return minigameEstagios.InstrucaoAtual;

            if (minigameRcs != null && minigameRcs.EstaAtivo && !string.IsNullOrEmpty(minigameRcs.InstrucaoAtual))
                return minigameRcs.InstrucaoAtual;

            if (minigameFreioOrbital != null && minigameFreioOrbital.EstaAtivo && !string.IsNullOrEmpty(minigameFreioOrbital.InstrucaoAtual))
                return minigameFreioOrbital.InstrucaoAtual;

            switch (trajetoria.FaseAtual)
            {
                case FaseViagemEspacial.ProntaNaTerra:
                    return "Nave pronta na Terra para decolagem";
                case FaseViagemEspacial.SubindoDaTerra:
                    return "Decolando da Terra — use WASD ou Setas para guiar a nave";
                case FaseViagemEspacial.CruzeiroEspacial:
                    return "Em voo rumo à Lua — conduza a trajetória da nave";
                case FaseViagemEspacial.AproximandoDaLua:
                    return "Aproximando da Lua — alinhando trajetória final";
                default:
                    return "Chegada concluída na Lua!";
            }
        }
    }
}
