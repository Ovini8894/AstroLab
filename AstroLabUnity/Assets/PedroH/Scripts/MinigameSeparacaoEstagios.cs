using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace AstroLab.PedroH
{
    [Serializable]
    public class JanelaSeparacaoEstagio
    {
        public string nomeEstagio = "Estágio Propulsor";
        [Range(0.01f, 0.25f)] public float progressoInicioJanela = 0.07f;
        [Range(0.02f, 0.30f)] public float progressoFimJanela = 0.13f;

        [HideInInspector] public bool janelaJaNotificada;
        [HideInInspector] public bool separado;
        [HideInInspector] public bool separadoManualmente;
    }

    /// <summary>
    /// Lógica da Ideia 1: Separação de Estágios (Staging) na fase SubindoDaTerra.
    /// Abre janelas generosas para crianças acionarem o descarte de estágios vazios,
    /// aliviando a nave no modelo 3D (via EncaixeModeloFoguete/FogueteModeloLogica) com impulso extra.
    /// </summary>
    [DisallowMultipleComponent]
    public class MinigameSeparacaoEstagios : MinigameViagemBase
    {
        [Header("Configuração dos Estágios na Subida")]
        public List<JanelaSeparacaoEstagio> janelasEstagios = new List<JanelaSeparacaoEstagio>
        {
            new JanelaSeparacaoEstagio
            {
                nomeEstagio = "Estágio 1 — Propulsor Inicial",
                progressoInicioJanela = 0.065f,
                progressoFimJanela = 0.125f
            },
            new JanelaSeparacaoEstagio
            {
                nomeEstagio = "Estágio 2 — Estágio de Ascensão",
                progressoInicioJanela = 0.135f,
                progressoFimJanela = 0.195f
            }
        };

        [Header("Acessibilidade Infantil e Impulso")]
        [Tooltip("Se o tempo da janela acabar sem clique, o piloto automático separa com segurança.")]
        public bool autoSepararAoExpirarJanela = true;

        [Range(0f, 1f)] public float intensidadeImpulsoBonus = 0.45f;
        [Min(0.2f)] public float duracaoImpulsoBonusSegundos = 2.4f;

        [Header("Eventos (Para Futuros Botões, Áudio e Partículas)")]
        public UnityEvent<int> aoLiberarJanelaSeparacao = new UnityEvent<int>();
        public UnityEvent<int> aoSepararEstagioComSucesso = new UnityEvent<int>();
        public UnityEvent<int> aoAutoSepararEstagio = new UnityEvent<int>();
        public UnityEvent aoConcluirTodosEstagios = new UnityEvent();

        public int IndiceEstagioAtual { get; private set; } = -1;
        public int TotalEstagios => janelasEstagios != null ? janelasEstagios.Count : 0;
        public int EstagiosSeparadosTotal { get; private set; }
        public int EstagiosSeparadosManualmente { get; private set; }
        public bool JanelaAtualAtiva { get; private set; }
        public float ProgressoJanela01 { get; private set; }
        public float TempoRestanteJanelaSegundos { get; private set; }
        public float ImpulsoBonusAtual01 { get; private set; }

        private float tempoRestanteBonusSegundos;
        private EncaixeModeloFoguete ultimoEncaixeConhecido;

        public override void ReiniciarMinigame()
        {
            if (janelasEstagios != null)
            {
                for (int i = 0; i < janelasEstagios.Count; i++)
                {
                    if (janelasEstagios[i] == null) continue;
                    janelasEstagios[i].janelaJaNotificada = false;
                    janelasEstagios[i].separado = false;
                    janelasEstagios[i].separadoManualmente = false;
                }
            }

            IndiceEstagioAtual = -1;
            EstagiosSeparadosTotal = 0;
            EstagiosSeparadosManualmente = 0;
            JanelaAtualAtiva = false;
            ProgressoJanela01 = 0f;
            TempoRestanteJanelaSegundos = 0f;
            ImpulsoBonusAtual01 = 0f;
            tempoRestanteBonusSegundos = 0f;
            AlterarEstado(EstadoMinigameEspacial.Aguardando, "Aguardando decolagem da Terra.");
        }

        /// <summary>
        /// Atualiza as janelas de estágio durante a subida e consome o comando de separação.
        /// </summary>
        public void AtualizarMinigame(
            FaseViagemEspacial fase,
            float progresso01,
            float duracaoTotalSegundos,
            bool acaoSolicitadaPeloJogador,
            EncaixeModeloFoguete encaixe,
            float deltaTime)
        {
            ultimoEncaixeConhecido = encaixe;
            AtualizarDecaimentoBonus(deltaTime);

            if (fase == FaseViagemEspacial.ProntaNaTerra)
                return;

            if (janelasEstagios == null || janelasEstagios.Count == 0)
            {
                FinalizarEstagiosSePendente(manual: true);
                return;
            }

            // Se a fase de subida já terminou, garante que todos os estágios pendentes sejam encerrados
            if (fase > FaseViagemEspacial.SubindoDaTerra)
            {
                GarantirEstagiosPendentesAoSairDaSubida(encaixe);
                return;
            }

            AlterarEstado(EstadoMinigameEspacial.Ativo, InstrucaoAtual);

            int proximoIndice = ObterIndiceProximoEstagioPendente();
            if (proximoIndice < 0)
            {
                FinalizarEstagiosSePendente(EstagiosSeparadosManualmente > 0);
                return;
            }

            JanelaSeparacaoEstagio janela = janelasEstagios[proximoIndice];
            float inicio = Mathf.Min(janela.progressoInicioJanela, janela.progressoFimJanela);
            float fim = Mathf.Max(inicio + 0.01f, janela.progressoFimJanela);

            if (progresso01 < inicio)
            {
                JanelaAtualAtiva = false;
                IndiceEstagioAtual = proximoIndice;
                ProgressoJanela01 = 0f;
                TempoRestanteJanelaSegundos = Mathf.Max(0f, (fim - progresso01) * duracaoTotalSegundos);
                InstrucaoAtual = "Subindo da Terra — prepare-se para soltar o " + janela.nomeEstagio;
                return;
            }

            IndiceEstagioAtual = proximoIndice;
            JanelaAtualAtiva = true;
            ProgressoJanela01 = Mathf.Clamp01(Mathf.InverseLerp(inicio, fim, progresso01));
            TempoRestanteJanelaSegundos = Mathf.Max(0f, (fim - progresso01) * duracaoTotalSegundos);
            InstrucaoAtual = "Pressione [E] para soltar o " + janela.nomeEstagio + "!";

            if (!janela.janelaJaNotificada)
            {
                janela.janelaJaNotificada = true;
                aoLiberarJanelaSeparacao?.Invoke(proximoIndice);
            }

            if (acaoSolicitadaPeloJogador)
            {
                ExecutarSeparacao(proximoIndice, encaixe, manual: true);
                return;
            }

            if (progresso01 >= fim)
            {
                if (autoSepararAoExpirarJanela)
                    ExecutarSeparacao(proximoIndice, encaixe, manual: false);
                else
                    janela.separado = true;
            }
        }

        /// <summary>
        /// Permite que botões de UI ou scripts externos acionem a separação do estágio na janela ativa.
        /// </summary>
        public bool TentarAcionarSeparacao(EncaixeModeloFoguete encaixeOpcional = null)
        {
            if (!EstaAtivo || !JanelaAtualAtiva || IndiceEstagioAtual < 0 || janelasEstagios == null)
                return false;

            if (IndiceEstagioAtual >= janelasEstagios.Count || janelasEstagios[IndiceEstagioAtual].separado)
                return false;

            ExecutarSeparacao(
                IndiceEstagioAtual,
                encaixeOpcional != null ? encaixeOpcional : ultimoEncaixeConhecido,
                manual: true);
            return true;
        }

        private void ExecutarSeparacao(int indice, EncaixeModeloFoguete encaixe, bool manual)
        {
            JanelaSeparacaoEstagio janela = janelasEstagios[indice];
            janela.separado = true;
            janela.separadoManualmente = manual;

            EstagiosSeparadosTotal++;
            if (manual)
            {
                EstagiosSeparadosManualmente++;
                tempoRestanteBonusSegundos = duracaoImpulsoBonusSegundos;
                ImpulsoBonusAtual01 = intensidadeImpulsoBonus;
            }

            JanelaAtualAtiva = false;
            ProgressoJanela01 = 1f;
            TempoRestanteJanelaSegundos = 0f;

            if (encaixe != null)
                encaixe.SepararEstagioNoModelo(indice);

            if (manual)
                aoSepararEstagioComSucesso?.Invoke(indice);
            else
                aoAutoSepararEstagio?.Invoke(indice);

            if (ObterIndiceProximoEstagioPendente() < 0)
                FinalizarEstagiosSePendente(EstagiosSeparadosManualmente > 0);
        }

        private void GarantirEstagiosPendentesAoSairDaSubida(EncaixeModeloFoguete encaixe)
        {
            if (EstaFinalizado)
                return;

            for (int i = 0; i < janelasEstagios.Count; i++)
            {
                JanelaSeparacaoEstagio j = janelasEstagios[i];
                if (j == null || j.separado) continue;

                if (autoSepararAoExpirarJanela)
                    ExecutarSeparacao(i, encaixe, manual: false);
                else
                    j.separado = true;
            }

            FinalizarEstagiosSePendente(EstagiosSeparadosManualmente > 0);
        }

        private void FinalizarEstagiosSePendente(bool manual)
        {
            JanelaAtualAtiva = false;
            if (EstaFinalizado)
                return;

            EstadoMinigameEspacial estadoFinal = manual
                ? EstadoMinigameEspacial.Concluido
                : EstadoMinigameEspacial.EncerradoPorSeguranca;

            AlterarEstado(estadoFinal, "Separação de estágios concluída!");
            aoConcluirTodosEstagios?.Invoke();
        }

        private int ObterIndiceProximoEstagioPendente()
        {
            for (int i = 0; i < janelasEstagios.Count; i++)
            {
                if (janelasEstagios[i] != null && !janelasEstagios[i].separado)
                    return i;
            }
            return -1;
        }

        private void AtualizarDecaimentoBonus(float deltaTime)
        {
            if (tempoRestanteBonusSegundos <= 0f || deltaTime <= 0f)
            {
                ImpulsoBonusAtual01 = 0f;
                return;
            }

            tempoRestanteBonusSegundos = Mathf.Max(0f, tempoRestanteBonusSegundos - deltaTime);
            float fator = Mathf.Clamp01(tempoRestanteBonusSegundos / Mathf.Max(0.01f, duracaoImpulsoBonusSegundos));
            ImpulsoBonusAtual01 = intensidadeImpulsoBonus * fator;
        }
    }
}
