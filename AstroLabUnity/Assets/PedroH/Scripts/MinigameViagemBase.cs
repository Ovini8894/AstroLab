using UnityEngine;

namespace AstroLab.PedroH
{
    public enum EstadoMinigameEspacial
    {
        Aguardando = 0,
        Ativo = 1,
        Concluido = 2,
        EncerradoPorSeguranca = 3
    }

    /// <summary>
    /// Base comum para os módulos lógicos de minigames da viagem espacial,
    /// centralizando controle de estado, instruções para futura UI e reset limpo.
    /// </summary>
    public abstract class MinigameViagemBase : MonoBehaviour
    {
        public EstadoMinigameEspacial EstadoAtual { get; protected set; } = EstadoMinigameEspacial.Aguardando;
        public string InstrucaoAtual { get; protected set; } = string.Empty;

        public bool EstaAtivo => EstadoAtual == EstadoMinigameEspacial.Ativo;
        public bool EstaFinalizado => EstadoAtual == EstadoMinigameEspacial.Concluido ||
                                      EstadoAtual == EstadoMinigameEspacial.EncerradoPorSeguranca;

        /// <summary>
        /// Reinicia completamente o estado do minigame para uma nova partida na Terra.
        /// </summary>
        public abstract void ReiniciarMinigame();

        protected void AlterarEstado(EstadoMinigameEspacial novoEstado, string novaInstrucao = null)
        {
            EstadoAtual = novoEstado;
            if (novaInstrucao != null)
                InstrucaoAtual = novaInstrucao;
        }
    }
}
