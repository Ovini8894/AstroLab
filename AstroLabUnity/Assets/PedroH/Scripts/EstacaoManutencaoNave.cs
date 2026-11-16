using AstroLab.Capacete;
using UnityEngine;

namespace AstroLab.PedroH
{
    public enum TipoEstacaoNave
    {
        ConsolePilotagem,
        ConsoleEngenharia,
        GeradorOxigenio,
        RecicladorAgua,
        BancoBaterias,
        PainelBiodomo,
        EscotilhaProa
    }

    /// <summary>
    /// Identifica uma estação interativa da nave durante a viagem até a Lua
    /// e sincroniza seus textos com o sistema AlvoExploracao do HudCapacete.
    /// </summary>
    [DisallowMultipleComponent]
    public class EstacaoManutencaoNave : MonoBehaviour
    {
        public TipoEstacaoNave tipo = TipoEstacaoNave.ConsolePilotagem;
        public string nomeEstacao = "Sistema da Nave";
        public string moduloLocalizacao = "Convés Principal";
        [TextArea(2, 4)] public string descricaoOperacional = "Sistema operacional da nave.";
        [TextArea(2, 4)] public string instrucaoManutencao = "Pressione [E] para operar ou estabilizar este sistema.";

        [HideInInspector] public bool emAlerta;

        private AlvoExploracao alvoExploracao;

        private void Awake()
        {
            GarantirAlvoExploracao();
        }

        public void Configurar(
            TipoEstacaoNave novoTipo,
            string nome,
            string modulo,
            string descricao,
            string instrucao)
        {
            tipo = novoTipo;
            nomeEstacao = nome;
            moduloLocalizacao = modulo;
            descricaoOperacional = descricao;
            instrucaoManutencao = instrucao;
            GarantirAlvoExploracao();
            AtualizarEstadoVisual(false);
        }

        public void GarantirAlvoExploracao()
        {
            if (!alvoExploracao)
                alvoExploracao = GetComponent<AlvoExploracao>();
            if (!alvoExploracao)
                alvoExploracao = gameObject.AddComponent<AlvoExploracao>();

            AtualizarTextoExploracao();
        }

        public void AtualizarEstadoVisual(bool alertaAtivo)
        {
            emAlerta = alertaAtivo;
            AtualizarTextoExploracao();
        }

        private void AtualizarTextoExploracao()
        {
            if (!alvoExploracao) return;

            alvoExploracao.categoria = "NAVE ESPACIAL · " + moduloLocalizacao.ToUpperInvariant();
            alvoExploracao.titulo = emAlerta
                ? "[ALERTA] " + nomeEstacao
                : nomeEstacao;
            alvoExploracao.descricao = descricaoOperacional;
            alvoExploracao.charada = instrucaoManutencao;
        }
    }
}
