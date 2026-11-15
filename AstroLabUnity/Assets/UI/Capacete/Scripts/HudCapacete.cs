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
        public UnityEngine.UI.Image[] segmentosEnergia;
        public GraficoCapacete oxigenioIcone, energiaIcone, mira;
        public GameObject interacaoPainel, descobertaPainel, alertaPainel;
        public TMP_Text interacaoTitulo, interacaoDistancia, descobertaTitulo, descobertaDescricao, descobertaCharada, descobertaCategoria, alertaTexto;
        [Header("Dados — conectar aos sistemas do jogo")]
        [Range(0, 100)] public float oxigenio = 81;
        [Range(0, 100)] public float energia = 64;
        public Transform baseReferencia;
        public Camera cameraJogador;
        public Behaviour controleMovimento;
        public float alcanceInteracao = 4;
        public LayerMask camadasInteracao = ~0;
        public bool scannerAtivo;
        [Header("Layout Compacto e Dicas")]
        public GameObject dicaPainel;
        [Min(0f)] public float tempoExibicaoDica = 8f;
        [Range(0.5f, 1f)] public float escalaCompacta = 0.85f;
        public bool FichaAberta => aberto;

        AlvoExploracao alvoAtual;
        bool aberto, movimentoAnterior, dicaConcluida;
        float tempoRestanteDica;
        ScannerPrimeiraPessoa scannerVinculado;
        CursorLockMode cursorAnterior;
        bool cursorVisivelAnterior;

        void Awake()
        {
            if(!cameraJogador)cameraJogador=Camera.main;
            if(interacaoPainel)interacaoPainel.SetActive(false);
            if(descobertaPainel)descobertaPainel.SetActive(false);
            AplicarLayoutCompacto();
            InicializarDica();
            AtualizarRecursos();
        }

        void Update()
        {
            AtualizarRecursos();
            AtualizarTemporizadorDica(Time.deltaTime);
            if(baseReferencia && cameraJogador && distanciaTexto)
                distanciaTexto.text = Mathf.RoundToInt(Vector3.Distance(cameraJogador.transform.position, baseReferencia.position)) + " <size=60%>m</size>";
            var kb = Keyboard.current;
            if(aberto) { if(kb != null && kb.escapeKey.wasPressedThisFrame)FecharDescoberta(); return; }
            if(scannerAtivo)return;
            AlvoExploracao candidato=null;float distancia=0;
            if(cameraJogador && Physics.Raycast(cameraJogador.transform.position,cameraJogador.transform.forward,out var hit,alcanceInteracao,camadasInteracao,QueryTriggerInteraction.Collide))
            { candidato=hit.collider.GetComponentInParent<AlvoExploracao>();distancia=hit.distance; }
            alvoAtual=candidato;
            if(interacaoPainel)interacaoPainel.SetActive(candidato != null);
            if(mira)mira.color=TemaCapacete.Alpha(TemaCapacete.Ciano,candidato ? 1f : .55f);
            if(candidato)
            {
                interacaoTitulo.text=candidato.titulo;
                interacaoDistancia.text=distancia.ToString("0.0")+" m";
                if(kb != null && kb.eKey.wasPressedThisFrame)InspecionarAtual();
            }
        }

        void AtualizarRecursos()
        {
            oxigenio=Mathf.Clamp(oxigenio,0,100);energia=Mathf.Clamp(energia,0,100);
            var o=oxigenio<=20 ? TemaCapacete.Alerta : TemaCapacete.Ciano;
            var e=energia<=20 ? TemaCapacete.Alerta : TemaCapacete.Ciano;
            if(oxigenioTexto){oxigenioTexto.text=Mathf.CeilToInt(oxigenio)+"<size=65%>%</size>";oxigenioTexto.color=o;}
            if(energiaTexto){energiaTexto.text=Mathf.CeilToInt(energia)+"<size=65%>%</size>";energiaTexto.color=e;}
            if(oxigenioBarra){oxigenioBarra.fillAmount=oxigenio/100f;oxigenioBarra.color=o;}
            if(oxigenioIcone)oxigenioIcone.color=o;
            if(energiaIcone)energiaIcone.color=e;
            if(segmentosEnergia!=null)for(int i=0;i<segmentosEnergia.Length;i++)
                if(segmentosEnergia[i])segmentosEnergia[i].color=i<Mathf.CeilToInt(energia/100f*segmentosEnergia.Length)?e:TemaCapacete.Alpha(TemaCapacete.Secundario,.3f);
            bool alerta=oxigenio<=20 || energia<=20;
            if(alertaPainel)alertaPainel.SetActive(alerta);
            if(alertaTexto && alerta)alertaTexto.text=oxigenio<=20?"OXIGÊNIO BAIXO  /  RETORNE À BASE":"ENERGIA BAIXA  /  ECONOMIZE RECURSOS";
        }

        public void DefinirOxigenio(float valor){oxigenio=valor;AtualizarRecursos();}
        public void DefinirEnergia(float valor){energia=valor;AtualizarRecursos();}
        public void DefinirMissao(string titulo,string etapa,int concluidas,int total)
        {
            if(missaoTitulo)missaoTitulo.text=titulo;
            if(missaoEtapa)missaoEtapa.text=etapa;
            if(missaoProgresso)missaoProgresso.text=Mathf.Max(0,concluidas).ToString("00")+" / "+Mathf.Max(1,total).ToString("00");
        }
        public void InspecionarAtual()
        {
            if(!alvoAtual)return;
            MostrarDescoberta(alvoAtual.titulo,alvoAtual.descricao,alvoAtual.charada,alvoAtual.categoria);
            alvoAtual.aoInspecionar?.Invoke();
        }
        public void MostrarDescoberta(string titulo,string descricao,string charada,string categoria="EXPLORAÇÃO")
        {
            ConcluirDica();
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
        void OnDestroy(){if(scannerVinculado)scannerVinculado.aoConcluir.RemoveListener(AoConcluirScan);}

        void AplicarLayoutCompacto()
        {
            var instrumentos=transform.Find("Instrumentos");
            if(!instrumentos)return;

            CompactarPainel(instrumentos,"Oxigenio",new Vector2(48f,-42f));
            CompactarPainel(instrumentos,"Energia",new Vector2(0f,-52f));
            CompactarPainel(instrumentos,"DistanciaBase",new Vector2(-48f,-42f));
            CompactarPainel(instrumentos,"Identidade",new Vector2(0f,-20f));
            CompactarPainel(instrumentos,"Missao",new Vector2(48f,66f));
            CompactarPainel(instrumentos,"Rodape",new Vector2(52f,34f));
            CompactarPainel(instrumentos,"Exploracao",new Vector2(-48f,56f));
            CompactarPainel(instrumentos,"Interacao",new Vector2(0f,82f));
        }

        void CompactarPainel(Transform raiz,string nome,Vector2 novaPosicao)
        {
            var filho=raiz.Find(nome) as RectTransform;
            if(!filho)return;
            filho.localScale=Vector3.one*escalaCompacta;
            filho.anchoredPosition=novaPosicao;
        }

        void InicializarDica()
        {
            if(!dicaPainel)dicaPainel=transform.Find("Instrumentos/Exploracao")?.gameObject;
            scannerVinculado=Object.FindAnyObjectByType<ScannerPrimeiraPessoa>();
            if(scannerVinculado)scannerVinculado.aoConcluir.AddListener(AoConcluirScan);
            tempoRestanteDica=tempoExibicaoDica;
            if(dicaConcluida && dicaPainel)dicaPainel.SetActive(false);
        }

        void AtualizarTemporizadorDica(float dt)
        {
            if(dicaConcluida || !dicaPainel || !dicaPainel.activeSelf)return;
            tempoRestanteDica-=Mathf.Max(0f,dt);
            if(tempoRestanteDica<=0f)ConcluirDica();
        }

        void AoConcluirScan(GameObject _)=>ConcluirDica();

        public void ConcluirDica()
        {
            dicaConcluida=true;
            if(dicaPainel)dicaPainel.SetActive(false);
        }
    }
}
