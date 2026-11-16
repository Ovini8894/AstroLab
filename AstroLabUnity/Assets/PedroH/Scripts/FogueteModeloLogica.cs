using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace AstroLab.PedroH
{
    [Serializable]
    public class EstagioFogueteConfig
    {
        public string nomeEstagio = "Estágio Propulsor";

        [Tooltip("Subobjeto 3D do estágio dentro do modelo do foguete (opcional até o modelo ficar pronto).")]
        public GameObject objetoEstagio;

        [Tooltip("Subobjetos extras associados ao estágio (ex.: irmãos LOD _L1/_L2) ocultados na separação e restaurados conforme seu estado ativo inicial.")]
        public GameObject[] objetosAdicionaisEstagio;

        [Tooltip("Direção local em que o estágio descartado se afasta ao ser separado.")]
        public Vector3 direcaoDescarteLocal = Vector3.back;

        [Tooltip("Quando verdadeiro, interpreta direcaoDescarteLocal no referencial da nave (pivotVisualFoguete/raiz) em vez do Transform interno rotacionado do FBX.")]
        public bool usarEspacoDaNaveParaDescarte = true;

        [Min(0.5f)] public float velocidadeAfastamento = 6f;
        [Range(0f, 120f)] public float velocidadeGiroDescarte = 22f;
        [Min(1f)] public float tempoVidaEstagioSolto = 8f;

        [HideInInspector] public bool jaSeparado;

        [NonSerialized] private GameObject objetoEstagioCapturado;
        [NonSerialized] private bool objetoEstagioAtivoInicial = true;
        [NonSerialized] private GameObject[] adicionaisCapturadosRefs;
        [NonSerialized] private bool[] adicionaisAtivosIniciais;

        /// <summary>
        /// Captura o estado ativo inicial de objetoEstagio e objetosAdicionaisEstagio na primeira
        /// vez em que cada referência é vista, preservando irmãos LOD _L1/_L2 que iniciam desativados.
        /// </summary>
        public void CapturarEstadosIniciaisSeNecessario()
        {
            if (objetoEstagio != null && objetoEstagioCapturado != objetoEstagio)
            {
                objetoEstagioCapturado = objetoEstagio;
                objetoEstagioAtivoInicial = objetoEstagio.activeSelf;
            }
            else if (objetoEstagio == null)
            {
                objetoEstagioCapturado = null;
                objetoEstagioAtivoInicial = true;
            }

            if (objetosAdicionaisEstagio == null || objetosAdicionaisEstagio.Length == 0)
            {
                adicionaisCapturadosRefs = null;
                adicionaisAtivosIniciais = null;
                return;
            }

            int quantidade = objetosAdicionaisEstagio.Length;
            if (adicionaisCapturadosRefs == null || adicionaisCapturadosRefs.Length != quantidade)
            {
                var novasRefs = new GameObject[quantidade];
                var novosEstados = new bool[quantidade];
                for (int i = 0; i < quantidade; i++)
                {
                    GameObject item = objetosAdicionaisEstagio[i];
                    novasRefs[i] = item;
                    int indiceAnterior = adicionaisCapturadosRefs != null && item != null
                        ? Array.IndexOf(adicionaisCapturadosRefs, item)
                        : -1;
                    novosEstados[i] = indiceAnterior >= 0
                        ? adicionaisAtivosIniciais[indiceAnterior]
                        : (item != null && item.activeSelf);
                }

                adicionaisCapturadosRefs = novasRefs;
                adicionaisAtivosIniciais = novosEstados;
                return;
            }

            for (int i = 0; i < quantidade; i++)
            {
                GameObject item = objetosAdicionaisEstagio[i];
                if (item != null && adicionaisCapturadosRefs[i] != item)
                {
                    adicionaisCapturadosRefs[i] = item;
                    adicionaisAtivosIniciais[i] = item.activeSelf;
                }
                else if (item == null)
                {
                    adicionaisCapturadosRefs[i] = null;
                    adicionaisAtivosIniciais[i] = false;
                }
            }
        }

        /// <summary>
        /// Restaura objetoEstagio e objetosAdicionaisEstagio de acordo com o estado ativo inicial capturado.
        /// </summary>
        public void RestaurarEstadosIniciais()
        {
            CapturarEstadosIniciaisSeNecessario();

            if (objetoEstagio != null && objetoEstagio.activeSelf != objetoEstagioAtivoInicial)
                objetoEstagio.SetActive(objetoEstagioAtivoInicial);

            if (objetosAdicionaisEstagio == null)
                return;

            for (int i = 0; i < objetosAdicionaisEstagio.Length; i++)
            {
                GameObject item = objetosAdicionaisEstagio[i];
                if (item == null) continue;

                bool estadoAlvo = adicionaisAtivosIniciais != null && i < adicionaisAtivosIniciais.Length
                    ? adicionaisAtivosIniciais[i]
                    : item.activeSelf;

                if (item.activeSelf != estadoAlvo)
                    item.SetActive(estadoAlvo);
            }
        }

        /// <summary>
        /// Oculta os objetos adicionais configurados (ex.: irmãos LOD _L1/_L2) durante o desacoplamento.
        /// </summary>
        public void OcultarObjetosAdicionais()
        {
            if (objetosAdicionaisEstagio == null)
                return;

            for (int i = 0; i < objetosAdicionaisEstagio.Length; i++)
            {
                GameObject item = objetosAdicionaisEstagio[i];
                if (item != null && item.activeSelf)
                    item.SetActive(false);
            }
        }
    }

    [Serializable]
    public class BocaisRcsFoguete
    {
        [Header("Pontos Opcionais de Exaustão RCS")]
        public Transform pontoEsquerda;
        public Transform pontoDireita;
        public Transform pontoCima;
        public Transform pontoBaixo;

        [Header("Luzes Opcionais dos Jatos RCS")]
        public Light luzEsquerda;
        public Light luzDireita;
        public Light luzCima;
        public Light luzBaixo;

        [Range(0f, 10f)] public float intensidadeMaximaLuz = 4f;
    }

    /// <summary>
    /// Componente lógico destinado à raiz do futuro modelo 3D (ou Prefab) do foguete.
    /// Centraliza pontos internos, separação de estágios, bocais RCS e atitude/luzes de retro-freio
    /// sem criar nem exigir malhas 3D antes de a arte ficar pronta.
    /// </summary>
    [DisallowMultipleComponent]
    public class FogueteModeloLogica : MonoBehaviour
    {
        [Header("Pontos Estruturais do Modelo do Foguete")]
        [Tooltip("Transform visual rotacionado em manobras e giro de retro-freio. Se vazio, usa este Transform.")]
        public Transform pivotVisualFoguete;

        [Tooltip("Posição interna da cabine/assento para câmera ou personagem quando o modelo existir.")]
        public Transform pontoCabine;

        [Tooltip("Ponto da base do motor principal para futura chama/partículas de subida.")]
        public Transform pontoExaustaoPrincipal;

        [Tooltip("Ponto opcional dos bocais frontais caso use retropropulsores dedicados sem girar o modelo.")]
        public Transform pontoExaustaoRetrofreio;

        [Tooltip("Ponto opcional para câmera externa enquadrar o foguete.")]
        public Transform pontoCameraExterna;

        [Header("Ideia 1 — Estágios Destacáveis do Foguete")]
        public List<EstagioFogueteConfig> estagios = new List<EstagioFogueteConfig>
        {
            new EstagioFogueteConfig { nomeEstagio = "Estágio 1 — Propulsores Principais" },
            new EstagioFogueteConfig { nomeEstagio = "Estágio 2 — Estágio de Ascensão" }
        };

        [Header("Ideia 3 — Propulsores de Controle de Reação (RCS)")]
        public BocaisRcsFoguete bocaisRcs = new BocaisRcsFoguete();

        [Header("Ideia 5 — Motor Principal e Retro-Freio Orbital Lunar")]
        public Light[] luzesMotorPrincipal;
        public Light[] luzesRetroFreio;
        [Range(0f, 12f)] public float intensidadeMaximaMotorPrincipal = 6f;
        [Range(0f, 12f)] public float intensidadeMaximaRetroFreio = 5f;

        [Tooltip("Se verdadeiro, o modelo do foguete gira gradualmente para apontar o motor contra a órbita lunar durante o retro-freio.")]
        public bool girarModeloParaRetroFreio = true;
        public Vector3 eixoLocalGiroRetroFreio = Vector3.right;
        [Range(0f, 180f)] public float anguloMaximoGiroRetroFreio = 180f;

        [Header("Eventos para Futuros Efeitos Visuais e Sonoros do Modelo")]
        public UnityEvent<int> aoSepararEstagio = new UnityEvent<int>();
        public UnityEvent<Vector2> aoAcionarRcs = new UnityEvent<Vector2>();
        public UnityEvent<bool> aoAlterarRetroFreio = new UnityEvent<bool>();

        public float IntensidadeMotorPrincipal01 { get; private set; }
        public float IntensidadeRetroFreio01 { get; private set; }
        public float FatorGiroRetroFreio01 { get; private set; }
        public float IntensidadeRcsEsquerda { get; private set; }
        public float IntensidadeRcsDireita { get; private set; }
        public float IntensidadeRcsCima { get; private set; }
        public float IntensidadeRcsBaixo { get; private set; }

        public int TotalEstagios => estagios != null ? estagios.Count : 0;
        public int EstagiosSeparados
        {
            get
            {
                if (estagios == null) return 0;
                int total = 0;
                for (int i = 0; i < estagios.Count; i++)
                {
                    if (estagios[i] != null && estagios[i].jaSeparado)
                        total++;
                }
                return total;
            }
        }
        public int EstagiosRestantes => Mathf.Max(0, TotalEstagios - EstagiosSeparados);

        private readonly List<GameObject> copiasEstagiosAtivas = new List<GameObject>();
        private bool retroFreioEstavaAtivo;

        private void Awake()
        {
            InicializarReferenciaModelo();
        }

        private void OnDestroy()
        {
            LimparCopiasEstagiosEmDeriva();
        }

        /// <summary>
        /// Garante referências mínimas válidas do modelo sem exigir que todos os subobjetos existam.
        /// </summary>
        public void InicializarReferenciaModelo()
        {
            if (pivotVisualFoguete == null)
                pivotVisualFoguete = transform;

            CapturarEstadosIniciaisEstagios();
        }

        /// <summary>
        /// Restaura todos os estágios visuais e zera propulsores/RCS ao reiniciar a viagem na Terra.
        /// </summary>
        public void ReiniciarEstadoModelo()
        {
            InicializarReferenciaModelo();
            LimparCopiasEstagiosEmDeriva();

            if (estagios != null)
            {
                for (int i = 0; i < estagios.Count; i++)
                {
                    if (estagios[i] == null) continue;
                    estagios[i].jaSeparado = false;
                    estagios[i].RestaurarEstadosIniciais();
                }
            }

            retroFreioEstavaAtivo = false;
            AtualizarSistemasPropulsao(0f, Vector2.zero, 0f, 0f);
        }

        /// <summary>
        /// Atualiza intensidades de motor principal, bocais RCS (ação/reação) e retro-freio lunar.
        /// </summary>
        public void AtualizarSistemasPropulsao(
            float motorPrincipal01,
            Vector2 comandoRcs,
            float retroFreio01,
            float fatorGiroRetro01)
        {
            InicializarReferenciaModelo();

            IntensidadeMotorPrincipal01 = Mathf.Clamp01(motorPrincipal01);
            IntensidadeRetroFreio01 = Mathf.Clamp01(retroFreio01);
            FatorGiroRetroFreio01 = Mathf.Clamp01(fatorGiroRetro01);

            AtualizarListaLuzes(luzesMotorPrincipal, IntensidadeMotorPrincipal01 * intensidadeMaximaMotorPrincipal);
            AtualizarListaLuzes(luzesRetroFreio, IntensidadeRetroFreio01 * intensidadeMaximaRetroFreio);

            // Polaridade realista (ação/reação): virar para a direita aciona o bocal esquerdo, subir aciona o inferior
            IntensidadeRcsEsquerda = AtualizarCanalRcs(Mathf.Clamp01(comandoRcs.x), bocaisRcs?.luzEsquerda);
            IntensidadeRcsDireita = AtualizarCanalRcs(Mathf.Clamp01(-comandoRcs.x), bocaisRcs?.luzDireita);
            IntensidadeRcsBaixo = AtualizarCanalRcs(Mathf.Clamp01(comandoRcs.y), bocaisRcs?.luzBaixo);
            IntensidadeRcsCima = AtualizarCanalRcs(Mathf.Clamp01(-comandoRcs.y), bocaisRcs?.luzCima);

            if (comandoRcs.sqrMagnitude > 0.01f)
                aoAcionarRcs?.Invoke(comandoRcs);

            bool retroAtivoAgora = IntensidadeRetroFreio01 > 0.05f;
            if (retroAtivoAgora != retroFreioEstavaAtivo)
            {
                retroFreioEstavaAtivo = retroAtivoAgora;
                aoAlterarRetroFreio?.Invoke(retroAtivoAgora);
            }
        }

        /// <summary>
        /// Calcula o giro adicional local de atitude para a manobra de retro-freio orbital lunar.
        /// </summary>
        public Quaternion ObterRotacaoLocalAdicionalRetroFreio()
        {
            if (!girarModeloParaRetroFreio || FatorGiroRetroFreio01 <= 0.0001f)
                return Quaternion.identity;

            Vector3 eixo = eixoLocalGiroRetroFreio.sqrMagnitude > 0.001f
                ? eixoLocalGiroRetroFreio.normalized
                : Vector3.right;

            return Quaternion.AngleAxis(anguloMaximoGiroRetroFreio * FatorGiroRetroFreio01, eixo);
        }

        /// <summary>
        /// Separa o estágio informado (ou o próximo disponível se indice &lt; 0), desacoplando uma cópia
        /// em deriva caso o subobjeto 3D já esteja atribuído e disparando eventos mesmo sem malha 3D.
        /// </summary>
        public bool SepararEstagio(int indice)
        {
            if (estagios == null || estagios.Count == 0)
                return false;

            if (indice < 0)
                return SepararProximoEstagio();

            if (indice >= estagios.Count)
                return false;

            EstagioFogueteConfig config = estagios[indice];
            if (config == null || config.jaSeparado)
                return false;

            config.jaSeparado = true;
            ExecutarDesacoplamentoVisualSeguro(config);
            aoSepararEstagio?.Invoke(indice);
            return true;
        }

        /// <summary>
        /// Separa o primeiro estágio configurado ainda acoplado ao foguete.
        /// </summary>
        public bool SepararProximoEstagio()
        {
            if (estagios == null)
                return false;

            for (int i = 0; i < estagios.Count; i++)
            {
                if (estagios[i] != null && !estagios[i].jaSeparado)
                    return SepararEstagio(i);
            }

            return false;
        }

        private float AtualizarCanalRcs(float intensidade01, Light luzOpcional)
        {
            float max = bocaisRcs != null ? bocaisRcs.intensidadeMaximaLuz : 4f;
            if (luzOpcional != null)
                luzOpcional.intensity = intensidade01 * max;
            return intensidade01;
        }

        private static void AtualizarListaLuzes(Light[] luzes, float intensidade)
        {
            if (luzes == null) return;
            for (int i = 0; i < luzes.Length; i++)
            {
                if (luzes[i] != null)
                    luzes[i].intensity = intensidade;
            }
        }

        private void CapturarEstadosIniciaisEstagios()
        {
            if (estagios == null)
                return;

            for (int i = 0; i < estagios.Count; i++)
            {
                if (estagios[i] != null)
                    estagios[i].CapturarEstadosIniciaisSeNecessario();
            }
        }

        private Transform ObterReferencialDescarte(EstagioFogueteConfig config, Transform transformEstagio)
        {
            if (config != null && config.usarEspacoDaNaveParaDescarte)
                return pivotVisualFoguete != null ? pivotVisualFoguete : transform;

            return transformEstagio != null ? transformEstagio : transform;
        }

        private static Vector3 CalcularCentroVisualMundo(Transform raizEstagio)
        {
            if (raizEstagio == null)
                return Vector3.zero;

            Renderer[] renderers = raizEstagio.GetComponentsInChildren<Renderer>(true);
            bool possuiBounds = false;
            Bounds boundsCombinado = default;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy || r is ParticleSystemRenderer)
                    continue;

                Bounds b = r.bounds;
                if (b.size.sqrMagnitude <= 0.000001f)
                    continue;

                if (!possuiBounds)
                {
                    boundsCombinado = b;
                    possuiBounds = true;
                }
                else
                {
                    boundsCombinado.Encapsulate(b);
                }
            }

            return possuiBounds ? boundsCombinado.center : raizEstagio.position;
        }

        private void ExecutarDesacoplamentoVisualSeguro(EstagioFogueteConfig config)
        {
            if (config == null)
                return;

            InicializarReferenciaModelo();
            config.CapturarEstadosIniciaisSeNecessario();

            if (config.objetoEstagio == null)
            {
                config.OcultarObjetosAdicionais();
                return;
            }

            Transform t = config.objetoEstagio.transform;

            // Calcula o centro visual antes de desativar para que Renderer.bounds reflita a malha ativa no mundo
            Vector3 centroVisual = CalcularCentroVisualMundo(t);

            // Preserva o objeto original e seus irmãos LOD desativados dentro do prefab para restauração limpa
            config.objetoEstagio.SetActive(false);
            config.OcultarObjetosAdicionais();

            if (!Application.isPlaying)
                return;

            Transform referencialDescarte = ObterReferencialDescarte(config, t);
            Vector3 dirLocalSegura = config.direcaoDescarteLocal.sqrMagnitude > 0.001f
                ? config.direcaoDescarteLocal.normalized
                : Vector3.back;
            Vector3 direcaoMundo = referencialDescarte.TransformDirection(dirLocalSegura);

            // Pivô no centro visual real evita giro excêntrico em peças FBX cujo Transform fica distante da malha
            GameObject pivotDeriva = new GameObject(config.objetoEstagio.name + "_Separado");
            pivotDeriva.transform.SetPositionAndRotation(centroVisual, referencialDescarte.rotation);

            GameObject copiaVisual = Instantiate(config.objetoEstagio, t.position, t.rotation);
            copiaVisual.name = config.objetoEstagio.name + "_Visual";
            // Preserva a escala mundial exata antes de parentear ao pivô (ex.: lossyScale = 25 em nó FBX 100 sob nave 0.25)
            copiaVisual.transform.localScale = t.lossyScale;
            copiaVisual.transform.SetParent(pivotDeriva.transform, true);

            LimparComponentesBloqueantesDaCopia(copiaVisual);
            copiaVisual.SetActive(true);

            var deriva = pivotDeriva.AddComponent<EstagioDescartadoDeriva>();
            deriva.IniciarDeriva(direcaoMundo, config.velocidadeAfastamento, config.velocidadeGiroDescarte, config.tempoVidaEstagioSolto);
            copiasEstagiosAtivas.Add(pivotDeriva);
        }

        private static void LimparComponentesBloqueantesDaCopia(GameObject copia)
        {
            // Evita duplicar câmera, AudioListener, controladores, colisores ou luzes no estágio em deriva
            Camera[] cameras = copia.GetComponentsInChildren<Camera>(true);
            for (int i = 0; i < cameras.Length; i++)
                Destroy(cameras[i]);

            AudioListener[] listeners = copia.GetComponentsInChildren<AudioListener>(true);
            for (int i = 0; i < listeners.Length; i++)
                Destroy(listeners[i]);

            FogueteModeloLogica[] logicas = copia.GetComponentsInChildren<FogueteModeloLogica>(true);
            for (int i = 0; i < logicas.Length; i++)
                Destroy(logicas[i]);

            Collider[] colisores = copia.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colisores.Length; i++)
                Destroy(colisores[i]);

            Light[] luzes = copia.GetComponentsInChildren<Light>(true);
            for (int i = 0; i < luzes.Length; i++)
                Destroy(luzes[i]);
        }

        private void LimparCopiasEstagiosEmDeriva()
        {
            for (int i = 0; i < copiasEstagiosAtivas.Count; i++)
            {
                GameObject obj = copiasEstagiosAtivas[i];
                if (obj == null) continue;

                if (Application.isPlaying)
                    Destroy(obj);
                else
                    DestroyImmediate(obj);
            }
            copiasEstagiosAtivas.Clear();
        }
    }

    /// <summary>
    /// Driver leve que afasta suavemente o clone visual de um estágio descartado e o remove após alguns segundos.
    /// </summary>
    public class EstagioDescartadoDeriva : MonoBehaviour
    {
        private Vector3 velocidadeMundo;
        private float velocidadeGiro;
        private float tempoRestante;

        public void IniciarDeriva(Vector3 direcaoMundo, float velocidade, float giroGrausPorSeg, float duracaoSegundos)
        {
            velocidadeMundo = direcaoMundo.normalized * Mathf.Max(0.5f, velocidade);
            velocidadeGiro = giroGrausPorSeg;
            tempoRestante = Mathf.Max(0.5f, duracaoSegundos);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            transform.position += velocidadeMundo * dt;
            transform.Rotate(new Vector3(0.4f, 1f, 0.25f).normalized, velocidadeGiro * dt, Space.Self);

            tempoRestante -= dt;
            if (tempoRestante <= 0f)
                Destroy(gameObject);
        }
    }
}
