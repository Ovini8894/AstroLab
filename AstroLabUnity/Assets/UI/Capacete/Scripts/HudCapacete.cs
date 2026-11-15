using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AstroLab.Capacete
{
    [DefaultExecutionOrder(100)]
    public class HudCapacete : MonoBehaviour
    {
        [Header("Referências do visor")]
        public TMP_Text oxigenioTexto, energiaTexto, distanciaTexto, missaoTitulo, missaoEtapa, missaoProgresso;
        public UnityEngine.UI.Image oxigenioBarra;
        public UnityEngine.UI.Image energiaBarra;
        public UnityEngine.UI.Image[] segmentosEnergia;
        public GraficoCapacete oxigenioIcone, energiaIcone, mira;
        public GameObject interacaoPainel, descobertaPainel, alertaPainel, exploracaoPainel;
        public TMP_Text interacaoTitulo, interacaoDistancia, descobertaTitulo, descobertaDescricao, descobertaCharada, descobertaCategoria, alertaTexto, exploracaoAjuda;
        [Header("Dados — conectar aos sistemas do jogo")]
        [Range(0, 100)] public float oxigenio = 81;
        [Range(0, 100)] public float energia = 64;
        public Transform baseReferencia;
        public Camera cameraJogador;
        public Behaviour controleMovimento;
        public float alcanceInteracao = 4;
        public LayerMask camadasInteracao = ~0;
        public bool scannerAtivo;
        public bool FichaAberta => aberto;

        AlvoExploracao alvoAtual;
        bool aberto, movimentoAnterior, orientacaoExploracaoConcluida, modoGuiaEclusa;
        bool? ultimoModoScanner;
        Transform baseReferenciaPadrao, eclusaReferencia;
        TMP_Text distanciaRotulo, distanciaSinal;
        string distanciaSinalPadrao;
        AstroLab.Nicholas.MissionPlayerInteraction interacaoMissaoCache;
        CursorLockMode cursorAnterior;
        bool cursorVisivelAnterior;

        void Awake()
        {
            if(!cameraJogador)cameraJogador=Camera.main;
            baseReferenciaPadrao = baseReferencia;
            var distPanel = transform.Find("Instrumentos/DistanciaBase");
            if (distPanel)
            {
                var rotuloTr = distPanel.Find("Rotulo");
                if (rotuloTr) distanciaRotulo = rotuloTr.GetComponent<TMP_Text>();
                var sinalTr = distPanel.Find("Sinal");
                if (sinalTr)
                {
                    distanciaSinal = sinalTr.GetComponent<TMP_Text>();
                    if (distanciaSinal) distanciaSinalPadrao = distanciaSinal.text;
                }
                if (!distanciaRotulo)
                {
                    foreach (var t in distPanel.GetComponentsInChildren<TMP_Text>(true))
                        if (t != distanciaTexto && t != distanciaSinal) { distanciaRotulo = t; break; }
                }
            }
            if(!exploracaoPainel){var t=transform.Find("Instrumentos/Exploracao");if(t)exploracaoPainel=t.gameObject;}
            if(!exploracaoAjuda && exploracaoPainel){var t=exploracaoPainel.transform.Find("Ajuda");if(t)exploracaoAjuda=t.GetComponent<TMP_Text>();}
            if(interacaoPainel)interacaoPainel.SetActive(false);
            if(descobertaPainel)descobertaPainel.SetActive(false);
            ConfigurarBarraEnergia();
            ConfigurarRotulosBarras();
            LimparTextosPermanentes();
            ConfigurarDestaquesInterativos();
            AtualizarRecursos();
            AtualizarOrientacaoExploracao();
        }

        void ConfigurarBarraEnergia()
        {
            if (energiaBarra != null) return;
            var energiaTr = transform.Find("Instrumentos/Energia");
            if (!energiaTr) return;

            var cargaTr = energiaTr.Find("Carga") ?? energiaTr.Find("Reserva");
            if (cargaTr)
            {
                energiaBarra = cargaTr.GetComponent<UnityEngine.UI.Image>();
                return;
            }

            // Clona o trilho e barra do oxigênio para manter padrão idêntico na energia
            if (oxigenioBarra != null)
            {
                var oxigenioTrilho = oxigenioBarra.transform.parent.Find("Trilho");
                if (oxigenioTrilho)
                {
                    var energiaTrilho = Instantiate(oxigenioTrilho.gameObject, energiaTr);
                    energiaTrilho.name = "Trilho";
                    var rtT = energiaTrilho.GetComponent<RectTransform>();
                    rtT.anchoredPosition = oxigenioTrilho.GetComponent<RectTransform>().anchoredPosition;
                    rtT.sizeDelta = oxigenioTrilho.GetComponent<RectTransform>().sizeDelta;
                }

                var energiaBarraGo = Instantiate(oxigenioBarra.gameObject, energiaTr);
                energiaBarraGo.name = "Carga";
                var rtB = energiaBarraGo.GetComponent<RectTransform>();
                rtB.anchoredPosition = oxigenioBarra.GetComponent<RectTransform>().anchoredPosition;
                rtB.sizeDelta = oxigenioBarra.GetComponent<RectTransform>().sizeDelta;
                energiaBarra = energiaBarraGo.GetComponent<UnityEngine.UI.Image>();

                if (segmentosEnergia != null)
                {
                    foreach (var seg in segmentosEnergia)
                        if (seg) seg.gameObject.SetActive(false);
                }
            }
        }

        void ConfigurarRotulosBarras()
        {
            DefinirRotuloBarra("Instrumentos/Oxigenio/Rotulo", "OXIGÊNIO DO TRAJE");
            DefinirRotuloBarra("Instrumentos/Energia/Rotulo", "BATERIA DO TRAJE");
        }

        void DefinirRotuloBarra(string caminho, string texto)
        {
            var tr = transform.Find(caminho);
            if (!tr) return;
            tr.gameObject.SetActive(true);
            var tmp = tr.GetComponent<TMP_Text>();
            if (tmp)
            {
                tmp.text = texto;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.overflowMode = TextOverflowModes.Overflow;
                var rt = tmp.rectTransform;
                if (rt && rt.sizeDelta.x < 220f)
                    rt.sizeDelta = new Vector2(220f, Mathf.Max(24f, rt.sizeDelta.y));
            }
        }

        void LimparTextosPermanentes()
        {
            // Oculta elementos estáticos puramente decorativos para liberar a visualização do cenário
            var rodape = transform.Find("Instrumentos/Rodape");
            if (rodape) rodape.gameObject.SetActive(false);
            var identidade = transform.Find("Instrumentos/Identidade");
            if (identidade) identidade.gameObject.SetActive(false);
        }

        void ConfigurarDestaquesInterativos()
        {
            // Aplica animação de destaque sutil em elementos interativos da interface
            if (interacaoPainel)
            {
                var tecla = interacaoPainel.transform.Find("Tecla");
                if (tecla && !tecla.GetComponent<DestaqueInterativoUI>())
                    tecla.gameObject.AddComponent<DestaqueInterativoUI>();
            }

            if (descobertaPainel)
            {
                var fechar = descobertaPainel.transform.Find("FichaObjeto/Fechar");
                if (fechar && !fechar.GetComponent<DestaqueInterativoUI>())
                    fechar.gameObject.AddComponent<DestaqueInterativoUI>();
            }
        }

        void Update()
        {
            AtualizarRecursos();
            if(!orientacaoExploracaoConcluida && ultimoModoScanner != scannerAtivo) AtualizarOrientacaoExploracao();
            Transform alvoDistancia = (modoGuiaEclusa && eclusaReferencia) ? eclusaReferencia : baseReferencia;
            if(alvoDistancia && cameraJogador && distanciaTexto)
            {
                int metros = Mathf.RoundToInt(Vector3.Distance(cameraJogador.transform.position, alvoDistancia.position));
                distanciaTexto.text = metros + " <size=60%>m</size>";
                if (distanciaSinal)
                    distanciaSinal.text = modoGuiaEclusa
                        ? "Eclusa: " + metros + " m  ·  [L] ENTRAR"
                        : (string.IsNullOrEmpty(distanciaSinalPadrao) ? "SINAL LOCAL  /  CONECTADO" : distanciaSinalPadrao);
            }
            var kb = Keyboard.current;
            if(aberto) { if(kb != null && kb.escapeKey.wasPressedThisFrame)FecharDescoberta(); return; }
            if(scannerAtivo)return;

            AlvoExploracao candidato=null;float distancia=0;
            if(cameraJogador && Physics.Raycast(cameraJogador.transform.position,cameraJogador.transform.forward,out var hit,alcanceInteracao,camadasInteracao,QueryTriggerInteraction.Collide))
            { candidato=hit.collider.GetComponentInParent<AlvoExploracao>();distancia=hit.distance; }
            alvoAtual=candidato;

            // Suporte contextual a alvos de exploração e interação da missão
            string promptMissao = null;
            if (!candidato)
            {
                if (!interacaoMissaoCache)
                    interacaoMissaoCache = Object.FindAnyObjectByType<AstroLab.Nicholas.MissionPlayerInteraction>();
                if (interacaoMissaoCache && !string.IsNullOrEmpty(interacaoMissaoCache.CurrentPrompt))
                    promptMissao = interacaoMissaoCache.CurrentPrompt;
            }

            bool temInteracao = candidato != null || !string.IsNullOrEmpty(promptMissao);
            if(interacaoPainel) interacaoPainel.SetActive(temInteracao);
            if(mira) mira.color = TemaCapacete.Alpha(TemaCapacete.Ciano, temInteracao ? .95f : .35f);

            if(candidato)
            {
                if(interacaoTitulo) interacaoTitulo.text = candidato.titulo;
                if(interacaoDistancia) interacaoDistancia.text = distancia.ToString("0.0")+" m";
                if(kb != null && kb.eKey.wasPressedThisFrame) InspecionarAtual();
            }
            else if (!string.IsNullOrEmpty(promptMissao))
            {
                if(interacaoTitulo) interacaoTitulo.text = promptMissao;
                if(interacaoDistancia) interacaoDistancia.text = "";
            }
        }

        void AtualizarRecursos()
        {
            oxigenio=Mathf.Clamp(oxigenio,0,100);energia=Mathf.Clamp(energia,0,100);
            float pulso = 0.70f + 0.30f * Mathf.Sin(Time.time * 4f);
            bool oxCritico = oxigenio <= 20f;
            bool enCritica = energia <= 20f;

            Color oNormal = TemaCapacete.Ciano;
            Color eNormal = TemaCapacete.Ciano;
            Color oAlerta = Color.Lerp(TemaCapacete.Alerta, Color.white, pulso * 0.35f);
            Color eAlerta = Color.Lerp(TemaCapacete.Alerta, Color.white, pulso * 0.35f);

            var o = oxCritico ? oAlerta : oNormal;
            var e = enCritica ? eAlerta : eNormal;

            if(oxigenioTexto){oxigenioTexto.text=Mathf.CeilToInt(oxigenio)+"<size=65%>%</size>";oxigenioTexto.color=o;}
            if(energiaTexto){energiaTexto.text=Mathf.CeilToInt(energia)+"<size=65%>%</size>";energiaTexto.color=e;}
            if(oxigenioBarra){oxigenioBarra.fillAmount=oxigenio/100f;oxigenioBarra.color=o;}
            if(energiaBarra){energiaBarra.fillAmount=energia/100f;energiaBarra.color=e;}
            if(oxigenioIcone)oxigenioIcone.color=TemaCapacete.Alpha(o, oxCritico ? pulso : .85f);
            if(energiaIcone)energiaIcone.color=TemaCapacete.Alpha(e, enCritica ? pulso : .85f);

            if(segmentosEnergia!=null && energiaBarra == null)
            {
                for(int i=0;i<segmentosEnergia.Length;i++)
                    if(segmentosEnergia[i])segmentosEnergia[i].color=i<Mathf.CeilToInt(energia/100f*segmentosEnergia.Length)?e:TemaCapacete.Alpha(TemaCapacete.Secundario,.3f);
            }

            bool alerta=oxCritico || enCritica;
            if(alertaPainel)alertaPainel.SetActive(alerta);
            if(alertaTexto && alerta)
            {
                alertaTexto.color = Color.Lerp(TemaCapacete.Alerta, Color.white, pulso * 0.25f);
                if (oxCritico && enCritica)
                    alertaTexto.text = "ALERTA CRÍTICO // RECURSOS BAIXOS - RETORNE À BASE";
                else if (oxCritico)
                    alertaTexto.text = "OXIGÊNIO BAIXO // RETORNE À BASE";
                else
                    alertaTexto.text = "ENERGIA BAIXA // ECONOMIZE RECURSOS";
            }
        }

        public void DefinirOxigenio(float valor){oxigenio=valor;AtualizarRecursos();}
        public void DefinirEnergia(float valor){energia=valor;AtualizarRecursos();}
        public void DefinirGuiaEclusa(bool emEva, Transform eclusaAlvo)
        {
            modoGuiaEclusa = emEva && eclusaAlvo != null;
            eclusaReferencia = eclusaAlvo;
            baseReferencia = modoGuiaEclusa ? eclusaAlvo : (baseReferenciaPadrao ? baseReferenciaPadrao : baseReferencia);
            if (distanciaRotulo)
                distanciaRotulo.text = modoGuiaEclusa ? "ECLUSA [L]" : "BASE LUNAR";
            if (!modoGuiaEclusa && distanciaSinal && !string.IsNullOrEmpty(distanciaSinalPadrao))
                distanciaSinal.text = distanciaSinalPadrao;
        }
        public void DefinirMissao(string titulo,string etapa,int concluidas,int total)
        {
            if(missaoTitulo)missaoTitulo.text=titulo;
            if(missaoEtapa)missaoEtapa.text=etapa;
            if(missaoProgresso)
            {
                bool concluida = concluidas >= total && total > 0;
                missaoProgresso.text=Mathf.Max(0,concluidas).ToString("00")+" / "+Mathf.Max(1,total).ToString("00");
                missaoProgresso.color = concluida ? TemaCapacete.Hex("59FF86") : TemaCapacete.Ciano;
            }
        }
        public void ConcluirOrientacaoExploracao()
        {
            orientacaoExploracaoConcluida = true;
            if (exploracaoPainel) exploracaoPainel.SetActive(false);
        }

        void AtualizarOrientacaoExploracao()
        {
            ultimoModoScanner = scannerAtivo;
            if (orientacaoExploracaoConcluida)
            {
                if (exploracaoPainel) exploracaoPainel.SetActive(false);
                return;
            }
            if (exploracaoAjuda)
                exploracaoAjuda.text = scannerAtivo
                    ? "Mire em um objeto e segure [E] ou o botão esquerdo por 1,35 s para escanear."
                    : "Aproxime-se de um objeto, mire nele e pressione E para inspecionar.";
        }

        public void InspecionarAtual()
        {
            if(!alvoAtual)return;
            ConcluirOrientacaoExploracao();
            MostrarDescoberta(alvoAtual.titulo,alvoAtual.descricao,alvoAtual.charada,alvoAtual.categoria);
            alvoAtual.aoInspecionar?.Invoke();
        }
        public void MostrarDescoberta(string titulo,string descricao,string charada,string categoria="EXPLORAÇÃO")
        {
            ConcluirOrientacaoExploracao();
            if(!descobertaPainel)return;
            if(!aberto)
            {
                cursorAnterior=Cursor.lockState;cursorVisivelAnterior=Cursor.visible;
                movimentoAnterior=controleMovimento && controleMovimento.enabled;
                if(controleMovimento)controleMovimento.enabled=false;
            }
            aberto=true;descobertaTitulo.text=titulo;descobertaDescricao.text=descricao;
            descobertaCharada.text=string.IsNullOrWhiteSpace(charada)?"Nenhuma pista registrada para este objeto.":charada;
            descobertaCategoria.text=categoria;
            descobertaPainel.SetActive(true);if(interacaoPainel)interacaoPainel.SetActive(false);
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        }
        public void FecharDescoberta()
        {
            if(!aberto)return;
            aberto=false;if(descobertaPainel)descobertaPainel.SetActive(false);
            if(controleMovimento)controleMovimento.enabled=movimentoAnterior;
            Cursor.lockState=cursorAnterior;Cursor.visible=cursorVisivelAnterior;
        }
        void OnDisable(){FecharDescoberta();}
    }
}
