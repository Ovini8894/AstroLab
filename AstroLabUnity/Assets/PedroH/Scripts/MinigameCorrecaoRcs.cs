using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace AstroLab.PedroH
{
    public enum TipoEventoRcs
    {
        AnelDeRota = 0,
        DesvioDeDetrito = 1
    }

    [Serializable]
    public class EventoCorrecaoRcs
    {
        public string nomeEvento = "Correção RCS";
        public TipoEventoRcs tipo = TipoEventoRcs.AnelDeRota;

        [Tooltip("Progresso normalizado [0..1] em que o aviso começa a aparecer para a criança.")]
        [Range(0.20f, 0.82f)] public float progressoAviso = 0.24f;

        [Tooltip("Progresso normalizado [0..1] em que a nave cruza o ponto do anel/detrito.")]
        [Range(0.22f, 0.83f)] public float progressoAlvo = 0.33f;

        [Tooltip("Progresso final da janela de cruzamento deste evento.")]
        [Range(0.23f, 0.84f)] public float progressoFim = 0.35f;

        [Tooltip("Coordenada no corredor elíptico (metros: X lateral, Y vertical).")]
        public Vector2 coordenadaCorredor = new Vector2(-12f, 3f);

        [Tooltip("Para AnelDeRota: raio para acertar o anel. Para DesvioDeDetrito: raio mínimo de segurança.")]
        [Min(1f)] public float raioToleranciaMetros = 9.5f;

        [HideInInspector] public bool iniciado;
        [HideInInspector] public bool concluido;
        [HideInInspector] public bool sucesso;
        [HideInInspector] public float menorDistanciaObservada;
    }

    /// <summary>
    /// Lógica da Ideia 3: Correção de Meio de Curso e Desvios com Propulsores RCS no CruzeiroEspacial.
    /// Agenda anéis de alinhamento e desvios de micrometeoroides ao longo do corredor espacial,
    /// sem criar modelos 3D nem penalizar oxigênio ou impedir a chegada à Lua.
    /// </summary>
    [DisallowMultipleComponent]
    public class MinigameCorrecaoRcs : MinigameViagemBase
    {
        [Header("Sequência de Desafios no Corredor Espacial (Cruzeiro)")]
        public List<EventoCorrecaoRcs> eventosRcs = new List<EventoCorrecaoRcs>
        {
            new EventoCorrecaoRcs
            {
                nomeEvento = "Anel de Correção Orbital 1",
                tipo = TipoEventoRcs.AnelDeRota,
                progressoAviso = 0.24f,
                progressoAlvo = 0.33f,
                progressoFim = 0.35f,
                coordenadaCorredor = new Vector2(-14f, 4f),
                raioToleranciaMetros = 10f
            },
            new EventoCorrecaoRcs
            {
                nomeEvento = "Desvio de Micrometeoroide",
                tipo = TipoEventoRcs.DesvioDeDetrito,
                progressoAviso = 0.37f,
                progressoAlvo = 0.46f,
                progressoFim = 0.48f,
                coordenadaCorredor = new Vector2(6f, 2f),
                raioToleranciaMetros = 8.5f
            },
            new EventoCorrecaoRcs
            {
                nomeEvento = "Anel de Alinhamento Lunar",
                tipo = TipoEventoRcs.AnelDeRota,
                progressoAviso = 0.51f,
                progressoAlvo = 0.60f,
                progressoFim = 0.62f,
                coordenadaCorredor = new Vector2(14f, -5f),
                raioToleranciaMetros = 10f
            },
            new EventoCorrecaoRcs
            {
                nomeEvento = "Campo de Detritos Espaciais",
                tipo = TipoEventoRcs.DesvioDeDetrito,
                progressoAviso = 0.65f,
                progressoAlvo = 0.75f,
                progressoFim = 0.77f,
                coordenadaCorredor = new Vector2(-8f, -2f),
                raioToleranciaMetros = 8.5f
            }
        };

        [Header("Feedback Visual Lógico (Sem Dano Punitivo)")]
        [Min(0.1f)] public float duracaoTremorImpactoLeve = 0.85f;
        [Range(0.005f, 0.05f)] public float margemCruzamentoAntesDoAlvo = 0.02f;

        [Header("Eventos para Futuros Anéis, Detritos, Sons e HUD")]
        public UnityEvent<EventoCorrecaoRcs> aoIniciarEventoRcs = new UnityEvent<EventoCorrecaoRcs>();
        public UnityEvent<EventoCorrecaoRcs, bool> aoConcluirEventoRcs = new UnityEvent<EventoCorrecaoRcs, bool>();
        public UnityEvent aoFinalizarTodosEventosRcs = new UnityEvent();

        public int IndiceEventoAtual { get; private set; } = -1;
        public EventoCorrecaoRcs EventoAtual => IndiceEventoAtual >= 0 && eventosRcs != null && IndiceEventoAtual < eventosRcs.Count
            ? eventosRcs[IndiceEventoAtual]
            : null;
        public bool EventoAtualAtivo => EstaAtivo && EventoAtual != null && EventoAtual.iniciado && !EventoAtual.concluido;
        public int TotalEventos => eventosRcs != null ? eventosRcs.Count : 0;
        public int EventosConcluidos { get; private set; }
        public int EventosComSucesso { get; private set; }
        public float DistanciaAtualParaAlvoMetros { get; private set; }
        public float ProgressoAproximacaoEvento01 { get; private set; }
        public float TremorImpactoAtual01 { get; private set; }

        private float tempoRestanteTremor;

        public override void ReiniciarMinigame()
        {
            if (eventosRcs != null)
            {
                for (int i = 0; i < eventosRcs.Count; i++)
                {
                    if (eventosRcs[i] == null) continue;
                    eventosRcs[i].iniciado = false;
                    eventosRcs[i].concluido = false;
                    eventosRcs[i].sucesso = false;
                    eventosRcs[i].menorDistanciaObservada = float.MaxValue;
                }
            }

            IndiceEventoAtual = -1;
            EventosConcluidos = 0;
            EventosComSucesso = 0;
            DistanciaAtualParaAlvoMetros = 0f;
            ProgressoAproximacaoEvento01 = 0f;
            TremorImpactoAtual01 = 0f;
            tempoRestanteTremor = 0f;
            AlterarEstado(EstadoMinigameEspacial.Aguardando, "Aguardando entrada em cruzeiro espacial.");
        }

        /// <summary>
        /// Atualiza o progresso de eventos RCS durante o CruzeiroEspacial usando o desvio no corredor.
        /// </summary>
        public void AtualizarMinigame(
            FaseViagemEspacial fase,
            float progresso01,
            Vector2 desvioCorredorAtual,
            float deltaTime)
        {
            AtualizarDecaimentoTremor(deltaTime);

            if (fase < FaseViagemEspacial.CruzeiroEspacial)
                return;

            if (eventosRcs == null || eventosRcs.Count == 0)
            {
                FinalizarCruzeiroRcs();
                return;
            }

            if (fase > FaseViagemEspacial.CruzeiroEspacial)
            {
                EncerrarEventosPendentes();
                return;
            }

            AlterarEstado(EstadoMinigameEspacial.Ativo, InstrucaoAtual);

            int proximoIndice = ObterProximoIndicePendente();
            if (proximoIndice < 0)
            {
                FinalizarCruzeiroRcs();
                return;
            }

            IndiceEventoAtual = proximoIndice;
            EventoCorrecaoRcs ev = eventosRcs[proximoIndice];
            float aviso = Mathf.Min(ev.progressoAviso, ev.progressoAlvo - 0.01f);
            float fim = Mathf.Max(ev.progressoFim, ev.progressoAlvo + 0.01f);

            if (progresso01 < aviso)
            {
                DistanciaAtualParaAlvoMetros = Vector2.Distance(desvioCorredorAtual, ev.coordenadaCorredor);
                ProgressoAproximacaoEvento01 = 0f;
                InstrucaoAtual = "Cruzeiro espacial — use WASD ou Setas para manobrar os jatos RCS.";
                return;
            }

            if (!ev.iniciado)
            {
                ev.iniciado = true;
                ev.menorDistanciaObservada = float.MaxValue;
                aoIniciarEventoRcs?.Invoke(ev);
            }

            float distanciaAtual = Vector2.Distance(desvioCorredorAtual, ev.coordenadaCorredor);
            DistanciaAtualParaAlvoMetros = distanciaAtual;
            ProgressoAproximacaoEvento01 = Mathf.Clamp01(Mathf.InverseLerp(aviso, ev.progressoAlvo, progresso01));

            InstrucaoAtual = ev.tipo == TipoEventoRcs.AnelDeRota
                ? "Manobre com os jatos RCS até o " + ev.nomeEvento
                : "Desvie com os jatos RCS: " + ev.nomeEvento + " à frente!";

            // Apenas mede cruzamento real perto do ponto alvo para dar tempo justo de manobra à criança
            bool naBandaDeCruzamento = progresso01 >= ev.progressoAlvo - margemCruzamentoAntesDoAlvo;
            if (naBandaDeCruzamento)
            {
                ev.menorDistanciaObservada = Mathf.Min(ev.menorDistanciaObservada, distanciaAtual);

                if (ev.tipo == TipoEventoRcs.AnelDeRota && distanciaAtual <= ev.raioToleranciaMetros)
                {
                    ConcluirEvento(ev, sucesso: true);
                    return;
                }
            }

            if (progresso01 >= fim)
            {
                bool passou = ev.tipo == TipoEventoRcs.AnelDeRota
                    ? ev.menorDistanciaObservada <= ev.raioToleranciaMetros
                    : ev.menorDistanciaObservada > ev.raioToleranciaMetros;

                if (!passou && ev.tipo == TipoEventoRcs.DesvioDeDetrito)
                    AtivarTremorLeve();

                ConcluirEvento(ev, passou);
            }
        }

        /// <summary>
        /// Calcula a posição exata 3D no mundo onde o evento RCS ocorre dentro da trajetória atual,
        /// pronta para instanciar anéis, detritos ou marcadores visuais quando existirem.
        /// </summary>
        public Vector3 ObterPosicaoMundoEvento(int indice, TrajetoriaViagemLunar trajetoria)
        {
            if (trajetoria == null || eventosRcs == null || indice < 0 || indice >= eventosRcs.Count || eventosRcs[indice] == null)
                return Vector3.zero;

            EventoCorrecaoRcs ev = eventosRcs[indice];
            return trajetoria.AvaliarPosicaoNaCurvaComDesvio(ev.progressoAlvo, ev.coordenadaCorredor);
        }

        public Vector3 ObterPosicaoMundoEventoAtual(TrajetoriaViagemLunar trajetoria)
        {
            return ObterPosicaoMundoEvento(IndiceEventoAtual, trajetoria);
        }

        private void ConcluirEvento(EventoCorrecaoRcs ev, bool sucesso)
        {
            if (ev.concluido)
                return;

            ev.concluido = true;
            ev.sucesso = sucesso;
            EventosConcluidos++;
            if (sucesso)
                EventosComSucesso++;

            ProgressoAproximacaoEvento01 = 1f;
            aoConcluirEventoRcs?.Invoke(ev, sucesso);

            if (ObterProximoIndicePendente() < 0)
                FinalizarCruzeiroRcs();
        }

        private void EncerrarEventosPendentes()
        {
            if (EstaFinalizado)
                return;

            for (int i = 0; i < eventosRcs.Count; i++)
            {
                EventoCorrecaoRcs ev = eventosRcs[i];
                if (ev == null || ev.concluido) continue;

                // Se encerrado por transição de fase sem cruzar detrito, considera livre com segurança
                bool sucesso = ev.tipo == TipoEventoRcs.DesvioDeDetrito ||
                               ev.menorDistanciaObservada <= ev.raioToleranciaMetros;
                ConcluirEvento(ev, sucesso);
            }

            FinalizarCruzeiroRcs();
        }

        private void FinalizarCruzeiroRcs()
        {
            if (EstaFinalizado)
                return;

            AlterarEstado(EstadoMinigameEspacial.Concluido, "Correções RCS de cruzeiro concluídas!");
            aoFinalizarTodosEventosRcs?.Invoke();
        }

        private int ObterProximoIndicePendente()
        {
            for (int i = 0; i < eventosRcs.Count; i++)
            {
                if (eventosRcs[i] != null && !eventosRcs[i].concluido)
                    return i;
            }
            return -1;
        }

        private void AtivarTremorLeve()
        {
            tempoRestanteTremor = duracaoTremorImpactoLeve;
            TremorImpactoAtual01 = 1f;
        }

        private void AtualizarDecaimentoTremor(float deltaTime)
        {
            if (tempoRestanteTremor <= 0f || deltaTime <= 0f)
            {
                TremorImpactoAtual01 = 0f;
                return;
            }

            tempoRestanteTremor = Mathf.Max(0f, tempoRestanteTremor - deltaTime);
            TremorImpactoAtual01 = Mathf.Clamp01(tempoRestanteTremor / Mathf.Max(0.01f, duracaoTremorImpactoLeve));
        }
    }
}
