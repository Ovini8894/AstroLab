using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace AstroLab.Capacete
{
    [DefaultExecutionOrder(110)]
    public class ScannerPrimeiraPessoa : MonoBehaviour
    {
        public const float DuracaoScan = 1.35f;
        public HudCapacete visor;
        public Camera cameraMira;
        public Transform origemFeixe;
        [Min(.1f)] public float alcance = 18f;
        public LayerMask camadas = Physics.DefaultRaycastLayers;
        public bool lerMouseAutomaticamente = true;
        public GameObject painelProgresso;
        public TMP_Text nomeAlvo, instrucao, percentual, telaAparelho;
        public UnityEngine.UI.Image barra, barraAparelho;
        public LineRenderer feixe;
        public AudioSource audioScanner;
        public AudioClip somVarredura, somConcluido;
        public UnityEvent<GameObject> aoConcluir = new UnityEvent<GameObject>();

        public float Progresso => Mathf.Clamp01(tempo / DuracaoScan);
        public bool Escaneando => tempo > 0f && !aguardarSoltar;
        public int ScansConcluidos { get; private set; }
        public GameObject AlvoAtual => alvo;
        public string UltimoTitulo { get; private set; }
        public Vector3 PontoAlvo { get; private set; }

        GameObject alvo;
        CatalogoScanner.Dados dados;
        float tempo;
        bool aguardarSoltar, iniciouSom;
        bool modoVisorAnterior;
        readonly RaycastHit[] acertos = new RaycastHit[128];

        void Awake()
        {
            if(!visor)visor=Object.FindAnyObjectByType<HudCapacete>();
            if(!cameraMira)cameraMira=Camera.main;
            AtualizarVisual(false);
        }

        void OnEnable(){if(visor){modoVisorAnterior=visor.scannerAtivo;visor.scannerAtivo=true;}}

        void Update()
        {
            if(lerMouseAutomaticamente)
            {
                bool pressionado = Application.isFocused &&
                    ((Mouse.current != null && Mouse.current.leftButton.isPressed) ||
                     (Keyboard.current != null && Keyboard.current.eKey.isPressed));
                AtualizarVarredura(pressionado, Time.deltaTime);
            }
        }

        // Permite conectar outro binding do Input System sem duplicar as regras de tempo/alvo.
        public void AtualizarVarredura(bool pressionado,float deltaSegundos)
        {
            if(!pressionado)aguardarSoltar=false;
            if(visor && visor.FichaAberta){CancelarProgresso();if(painelProgresso)painelProgresso.SetActive(false);if(feixe)feixe.enabled=false;return;}
            if(!cameraMira || !visor || Time.timeScale<=0f || !isActiveAndEnabled)
            {CancelarProgresso();AtualizarVisual(false);return;}
            var ray=new Ray(cameraMira.transform.position,cameraMira.transform.forward);
            int quantidade=Physics.RaycastNonAlloc(ray,acertos,alcance,camadas,QueryTriggerInteraction.Ignore);
            var hit=new RaycastHit();float maisPerto=float.PositiveInfinity;
            for(int i=0;i<quantidade;i++)
            {
                var candidato=acertos[i];
                if(visor.controleMovimento && candidato.collider.transform.IsChildOf(visor.controleMovimento.transform))continue;
                if(candidato.distance<maisPerto){maisPerto=candidato.distance;hit=candidato;}
            }
            if(!hit.collider)
            {alvo=null;CancelarProgresso();AtualizarVisual(false);return;}
            if(hit.collider.GetComponentInParent<AstroLab.Nicholas.MissionEquipmentTarget>() != null)
            {alvo=null;CancelarProgresso();AtualizarVisual(false);return;}
            var novo=CatalogoScanner.Descrever(hit.collider);
            if(novo.objeto!=alvo){CancelarProgresso();alvo=novo.objeto;dados=novo;}
            PontoAlvo=hit.point;
            if(!pressionado || aguardarSoltar){CancelarProgresso();AtualizarVisual(true);return;}
            if(!iniciouSom)
            {
                iniciouSom=true;
                if(audioScanner && somVarredura){audioScanner.clip=somVarredura;audioScanner.loop=true;audioScanner.Play();}
            }
            if(!float.IsNaN(deltaSegundos) && !float.IsInfinity(deltaSegundos))tempo+=Mathf.Max(0f,deltaSegundos);
            AtualizarVisual(true);
            if(tempo+0.00001f<DuracaoScan)return;
            tempo=DuracaoScan;aguardarSoltar=true;ScansConcluidos++;UltimoTitulo=dados.titulo;
            if(audioScanner){audioScanner.Stop();audioScanner.loop=false;if(somConcluido)audioScanner.PlayOneShot(somConcluido);}
            iniciouSom=false;
            bool tratadoPorMissao = (alvo.GetComponentInParent<AstroLab.Nicholas.MissionHistoricalTarget>() != null ||
                                     alvo.GetComponentInChildren<AstroLab.Nicholas.MissionHistoricalTarget>() != null) &&
                                    Object.FindAnyObjectByType<AstroLab.Nicholas.MissionScannerBridge>() != null;
            if (!tratadoPorMissao)
                visor.MostrarDescoberta(dados.titulo,dados.descricao,dados.charada,dados.categoria);
            else
                visor.ConcluirOrientacaoExploracao();
            dados.cadastro?.aoInspecionar?.Invoke();aoConcluir?.Invoke(alvo);
            Debug.Log("[SCANNER] Scan concluído após 1,35 segundos: "+dados.titulo);
            if(painelProgresso)painelProgresso.SetActive(false);
            if(feixe)feixe.enabled=false;
            if(telaAparelho)telaAparelho.text="REGISTRADO\n100%";
        }

        void CancelarProgresso()
        {
            tempo=0f;
            if(iniciouSom && audioScanner)audioScanner.Stop();
            iniciouSom=false;
        }

        void AtualizarVisual(bool temAlvo)
        {
            if(visor)
            {
                if(visor.interacaoPainel)visor.interacaoPainel.SetActive(false);
                if(visor.mira)visor.mira.color=TemaCapacete.Alpha(TemaCapacete.Ciano,temAlvo?.9f:.35f);
            }
            if(painelProgresso)painelProgresso.SetActive(temAlvo);
            if(nomeAlvo)nomeAlvo.text=temAlvo?dados.titulo:"SEM ALVO";
            if(instrucao)
            {
                bool mostrarInstrucao=ScansConcluidos==0 || Escaneando;
                instrucao.gameObject.SetActive(mostrarInstrucao);
                if(mostrarInstrucao)
                    instrucao.text=Escaneando?"MANTENHA A MIRA NO ALVO ATÉ 100%":"SEGURE [E] OU BOTÃO ESQUERDO PARA ESCANEAR (1,35 s)";
            }
            if(percentual)percentual.text=Mathf.FloorToInt(Progresso*100)+"%";
            if(barra)barra.fillAmount=Progresso;
            if(barraAparelho)barraAparelho.fillAmount=Progresso;
            if(telaAparelho)telaAparelho.text=Escaneando?"ANALISANDO\n"+Mathf.FloorToInt(Progresso*100)+"%":"SCANNER\n"+(temAlvo?"PRONTO":"BUSCANDO");
            if(feixe)
            {
                feixe.enabled=Escaneando && origemFeixe;
                if(feixe.enabled){feixe.SetPosition(0,origemFeixe.position);feixe.SetPosition(1,PontoAlvo);}
            }
        }

        void OnApplicationFocus(bool foco){if(!foco){CancelarProgresso();AtualizarVisual(false);}}
        void OnDisable()
        {
            CancelarProgresso();if(painelProgresso)painelProgresso.SetActive(false);if(feixe)feixe.enabled=false;
            if(visor)visor.scannerAtivo=modoVisorAnterior;
        }
    }
}
