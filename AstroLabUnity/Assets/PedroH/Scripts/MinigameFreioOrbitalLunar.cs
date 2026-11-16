using UnityEngine;
using UnityEngine.Events;

namespace AstroLab.PedroH
{
    /// <summary>
    /// Lógica da Ideia 5: Retro-Freio Orbital Lunar (Retro-burn) na fase AproximandoDaLua.
    /// Prepara o giro de atitude do modelo do foguete e permite que a criança segure o
    /// retropropulsor até estabilizar a velocidade na faixa verde segura de órbita lunar.
    /// </summary>
    [DisallowMultipleComponent]
    public class MinigameFreioOrbitalLunar : MinigameViagemBase
    {
        [Header("Faixa Verde Segura de Órbita Lunar")]
        [Range(0.05f, 0.45f)] public float faixaVerdeMin = 0.18f;
        [Range(0.25f, 0.75f)] public float faixaVerdeMax = 0.42f;
        [Min(0.5f)] public float tempoNecessarioNaFaixaVerde = 2.0f;

        [Header("Resposta Suave dos Retropropulsores")]
        [Range(0.1f, 0.8f)] public float taxaFrenagemJogador = 0.30f;
        [Range(0.02f, 0.3f)] public float taxaFrenagemSuaveNaFaixa = 0.08f;
        [Range(1f, 12f)] public float velocidadeRampaGiroAtitude = 1.4f;
        [Range(1f, 15f)] public float velocidadeRampaPropulsor = 6.5f;

        [Header("Piloto Automático de Segurança Infantil")]
        [Tooltip("Garante desaceleração segura caso a criança não segure o freio próximo à Lua.")]
        public bool habilitarAssistenciaAutomatica = true;
        [Range(0.86f, 0.98f)] public float progressoInicioAssistencia = 0.93f;

        [Header("Eventos (Para Futuros Indicadores, Sons e Efeitos de Retro-Burn)")]
        public UnityEvent aoIniciarAproximacaoRetroFreio = new UnityEvent();
        public UnityEvent aoEntrarNaFaixaVerde = new UnityEvent();
        public UnityEvent aoEstabilizarOrbitaLunar = new UnityEvent();

        public float VelocidadeAproximacao01 { get; private set; } = 1f;
        public float IntensidadeRetroFreioAtual01 { get; private set; }
        public float FatorGiroAtitudeRetro01 { get; private set; }
        public bool FreioAcionadoPeloJogador { get; private set; }
        public bool AssistenciaAutomaticaAtiva { get; private set; }
        public bool EstaNaFaixaVerde => VelocidadeAproximacao01 >= faixaVerdeMin && VelocidadeAproximacao01 <= faixaVerdeMax;
        public float TempoAcumuladoNaFaixaVerde { get; private set; }
        public float ProgressoEstabilizacao01 => Mathf.Clamp01(TempoAcumuladoNaFaixaVerde / Mathf.Max(0.1f, tempoNecessarioNaFaixaVerde));
        public bool OrbitaEstabilizada { get; private set; }
        public bool OrbitaEstabilizadaManualmente { get; private set; }

        private bool freioExternoPressionado;
        private bool aproximacaoIniciada;
        private bool estavaNaFaixaVerde;

        public override void ReiniciarMinigame()
        {
            VelocidadeAproximacao01 = 1f;
            IntensidadeRetroFreioAtual01 = 0f;
            FatorGiroAtitudeRetro01 = 0f;
            FreioAcionadoPeloJogador = false;
            AssistenciaAutomaticaAtiva = false;
            freioExternoPressionado = false;
            aproximacaoIniciada = false;
            estavaNaFaixaVerde = false;
            TempoAcumuladoNaFaixaVerde = 0f;
            OrbitaEstabilizada = false;
            OrbitaEstabilizadaManualmente = false;
            AlterarEstado(EstadoMinigameEspacial.Aguardando, "Aguardando aproximação da Lua.");
        }

        /// <summary>
        /// Permite controlar o retro-freio por botão segurável da futura UI.
        /// </summary>
        public void DefinirFreioPressionadoExterno(bool ativo)
        {
            freioExternoPressionado = ativo;
        }

        /// <summary>
        /// Atualiza a lógica do retro-burn durante AproximandoDaLua até ChegouNaLua.
        /// </summary>
        public void AtualizarMinigame(
            FaseViagemEspacial fase,
            float progresso01,
            bool freioPressionadoNoInput,
            float deltaTime)
        {
            if (fase < FaseViagemEspacial.AproximandoDaLua)
                return;

            if (fase == FaseViagemEspacial.ChegouNaLua)
            {
                EncerrarNaChegadaLunar(deltaTime);
                return;
            }

            if (!aproximacaoIniciada)
            {
                aproximacaoIniciada = true;
                AlterarEstado(EstadoMinigameEspacial.Ativo, "Segure [ESPAÇO] ou [CTRL] para acionar o freio orbital lunar!");
                aoIniciarAproximacaoRetroFreio?.Invoke();
            }

            // Rampa do giro de atitude do modelo do foguete para apontar contra o movimento
            if (deltaTime > 0f)
            {
                FatorGiroAtitudeRetro01 = Mathf.MoveTowards(
                    FatorGiroAtitudeRetro01,
                    1f,
                    velocidadeRampaGiroAtitude * deltaTime);
            }

            bool jogadorSegurando = freioPressionadoNoInput || freioExternoPressionado;
            FreioAcionadoPeloJogador = jogadorSegurando;

            float centroFaixaVerde = (faixaVerdeMin + faixaVerdeMax) * 0.5f;
            bool precisaAssistencia = habilitarAssistenciaAutomatica &&
                                      !OrbitaEstabilizada &&
                                      progresso01 >= progressoInicioAssistencia &&
                                      !jogadorSegurando &&
                                      VelocidadeAproximacao01 > faixaVerdeMax;

            AssistenciaAutomaticaAtiva = precisaAssistencia;

            if (OrbitaEstabilizada)
            {
                VelocidadeAproximacao01 = Mathf.MoveTowards(VelocidadeAproximacao01, centroFaixaVerde, 0.25f * deltaTime);
                IntensidadeRetroFreioAtual01 = Mathf.MoveTowards(IntensidadeRetroFreioAtual01, 0.15f, velocidadeRampaPropulsor * deltaTime);
                InstrucaoAtual = "Órbita lunar estabilizada! Preparando chegada segura na Lua.";
                return;
            }

            bool retroLigado = jogadorSegurando || precisaAssistencia;
            float alvoPropulsor = retroLigado ? 1f : 0f;
            IntensidadeRetroFreioAtual01 = Mathf.MoveTowards(IntensidadeRetroFreioAtual01, alvoPropulsor, velocidadeRampaPropulsor * deltaTime);

            if (deltaTime > 0f)
            {
                if (retroLigado)
                {
                    float taxa = EstaNaFaixaVerde ? taxaFrenagemSuaveNaFaixa : taxaFrenagemJogador;
                    float minimoSeguro = Mathf.Max(0.05f, faixaVerdeMin - 0.04f);
                    VelocidadeAproximacao01 = Mathf.Max(minimoSeguro, VelocidadeAproximacao01 - taxa * deltaTime);
                }
                else
                {
                    // Se ainda está muito rápido, mantém estável sem punição; se passou abaixo da faixa, volta gentilmente
                    if (VelocidadeAproximacao01 < faixaVerdeMin)
                        VelocidadeAproximacao01 = Mathf.MoveTowards(VelocidadeAproximacao01, faixaVerdeMin, 0.10f * deltaTime);
                }
            }

            bool naFaixaAgora = EstaNaFaixaVerde;
            if (naFaixaAgora && !estavaNaFaixaVerde)
                aoEntrarNaFaixaVerde?.Invoke();
            estavaNaFaixaVerde = naFaixaAgora;

            if (naFaixaAgora)
            {
                TempoAcumuladoNaFaixaVerde += deltaTime;
                InstrucaoAtual = "Ótimo! Mantenha na faixa verde para estabilizar a órbita lunar!";

                if (TempoAcumuladoNaFaixaVerde >= tempoNecessarioNaFaixaVerde)
                {
                    OrbitaEstabilizada = true;
                    OrbitaEstabilizadaManualmente = !precisaAssistencia;
                    AlterarEstado(
                        OrbitaEstabilizadaManualmente ? EstadoMinigameEspacial.Concluido : EstadoMinigameEspacial.EncerradoPorSeguranca,
                        "Órbita lunar estabilizada! Preparando chegada segura na Lua.");
                    aoEstabilizarOrbitaLunar?.Invoke();
                }
            }
            else if (precisaAssistencia)
            {
                InstrucaoAtual = "Piloto automático auxiliando o freio orbital lunar!";
            }
            else
            {
                InstrucaoAtual = "Segure [ESPAÇO] ou [CTRL] para reduzir a velocidade até a faixa verde!";
            }
        }

        private void EncerrarNaChegadaLunar(float deltaTime)
        {
            float centroFaixa = (faixaVerdeMin + faixaVerdeMax) * 0.5f;
            VelocidadeAproximacao01 = Mathf.Min(VelocidadeAproximacao01, centroFaixa);
            IntensidadeRetroFreioAtual01 = Mathf.MoveTowards(IntensidadeRetroFreioAtual01, 0f, velocidadeRampaPropulsor * Mathf.Max(0.016f, deltaTime));
            FreioAcionadoPeloJogador = false;
            AssistenciaAutomaticaAtiva = false;

            if (!OrbitaEstabilizada)
            {
                OrbitaEstabilizada = true;
                AlterarEstado(EstadoMinigameEspacial.EncerradoPorSeguranca, "Chegada concluída na Lua!");
                aoEstabilizarOrbitaLunar?.Invoke();
            }
        }
    }
}
