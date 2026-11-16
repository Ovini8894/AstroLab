using UnityEngine;

namespace AstroLab.PedroH
{
    /// <summary>
    /// Gerencia os pontos de encaixe (sockets) e as referências da nave para permitir
    /// conectar o modelo 3D do foguete quando ele estiver pronto, sem presumir que
    /// o modelo visual já exista no projeto ou na cena.
    /// </summary>
    [DisallowMultipleComponent]
    public class EncaixeModeloFoguete : MonoBehaviour
    {
        [Header("Pontos de Encaixe para o Futuro Modelo do Foguete")]
        [Tooltip("Transform principal movimentado ao longo da trajetória. Se vazio, usa o próprio Transform.")]
        public Transform raizNave;

        [Tooltip("Transform filho onde o prefab ou FBX do foguete será acoplado quando estiver pronto.")]
        public Transform pontoEncaixeModelo;

        [Tooltip("Referência opcional ao objeto visual do foguete atualmente conectado (pode ficar nulo).")]
        public GameObject modeloFogueteAtual;

        [Tooltip("Ponto opcional interno da cabine/assento para futura câmera ou personagem embarcado.")]
        public Transform pontoCabineOpcional;

        [Tooltip("Ponto opcional na base dos propulsores para futura conexão de chama/partículas de subida.")]
        public Transform pontoExaustaoPropulsorOpcional;

        [Tooltip("Ponto opcional para câmera externa acompanhar o foguete durante a subida até a Lua.")]
        public Transform pontoCameraExternaOpcional;

        [Header("Luzes Opcionais dos Propulsores (Se Existirem na Cena/Modelo)")]
        [Tooltip("Lista opcional de luzes associadas aos propulsores; mantida segura caso esteja vazia.")]
        public Light[] luzesPropulsoresOpcionais;

        [Range(0f, 12f)] public float intensidadeBaseLuzPropulsor = 2.5f;
        [Range(0f, 12f)] public float intensidadeExtraLuzImpulso = 3.5f;

        [Header("Inclinação Divertida e Suave Durante Manobras")]
        [Range(0f, 35f)] public float anguloMaximoInclinacaoLateral = 16f;
        [Range(0f, 25f)] public float anguloMaximoArfagemVisual = 12f;
        [Range(0f, 20f)] public float anguloMaximoGuinadaVisual = 8f;
        [Range(1f, 25f)] public float velocidadeSuavizacaoRotacao = 6f;

        [Tooltip("Vibração leve e divertida na subida atmosférica a partir da Terra (0 desativa).")]
        [Range(0f, 0.25f)] public float amplitudeVibracaoSubida = 0.03f;

        private Vector3 inclinacaoEulerAtual;
        private Vector3 posicaoBasePontoEncaixe;
        private bool pontoEncaixeDistintoCapturado;
        private bool modeloAtualInstanciadoEmRuntime;

        /// <summary>
        /// Referência ao script lógico do próprio modelo do foguete (FogueteModeloLogica),
        /// detectada automaticamente quando o modelo é conectado ou colocado como filho.
        /// </summary>
        public FogueteModeloLogica FogueteConectado { get; private set; }

        private void Awake()
        {
            InicializarPontosDeEncaixe();
        }

        /// <summary>
        /// Prepara as referências padrão de encaixe sem criar nem exigir nenhum modelo 3D.
        /// Caso já exista um modelo com FogueteModeloLogica como filho ou em modeloFogueteAtual, sincroniza-o.
        /// </summary>
        public void InicializarPontosDeEncaixe()
        {
            if (raizNave == null)
                raizNave = transform;

            if (pontoEncaixeModelo == null)
                pontoEncaixeModelo = raizNave;

            if (pontoEncaixeModelo != raizNave && !pontoEncaixeDistintoCapturado)
            {
                posicaoBasePontoEncaixe = pontoEncaixeModelo.localPosition;
                pontoEncaixeDistintoCapturado = true;
            }

            SincronizarFogueteModeloLogica();
        }

        /// <summary>
        /// Conecta o futuro modelo 3D (Prefab/FBX ou instância já na cena) ao ponto de encaixe.
        /// Detecta e vincula automaticamente um FogueteModeloLogica presente no modelo.
        /// </summary>
        public bool ConectarModeloFoguete(GameObject modeloOuPrefab, bool instanciarSeForPrefab = true)
        {
            if (modeloOuPrefab == null)
                return false;

            InicializarPontosDeEncaixe();
            DesconectarModeloAtual();

            Transform destino = ObterPontoEncaixeAtual();
            bool foraDaCena = !modeloOuPrefab.scene.IsValid();
#if UNITY_EDITOR
            bool ehAssetPersistente = UnityEditor.EditorUtility.IsPersistent(modeloOuPrefab);
#else
            bool ehAssetPersistente = foraDaCena;
#endif
            bool deveInstanciar = ehAssetPersistente || (instanciarSeForPrefab && foraDaCena);

            GameObject alvo;
            if (deveInstanciar)
            {
                alvo = Instantiate(modeloOuPrefab, destino, false);
                alvo.name = modeloOuPrefab.name;
                modeloAtualInstanciadoEmRuntime = true;
            }
            else
            {
                alvo = modeloOuPrefab;
                alvo.transform.SetParent(destino, false);
                modeloAtualInstanciadoEmRuntime = false;
            }

            alvo.transform.localPosition = Vector3.zero;
            alvo.transform.localRotation = Quaternion.identity;
            modeloFogueteAtual = alvo;

            SincronizarFogueteModeloLogica();
            if (FogueteConectado != null)
                FogueteConectado.ReiniciarEstadoModelo();

            return true;
        }

        /// <summary>
        /// Desconecta o modelo visual atual com segurança caso seja necessário trocar por outro modelo.
        /// </summary>
        public void DesconectarModeloAtual()
        {
            if (modeloFogueteAtual != null && modeloAtualInstanciadoEmRuntime)
            {
                if (Application.isPlaying)
                    Destroy(modeloFogueteAtual);
                else
                    DestroyImmediate(modeloFogueteAtual);
            }

            modeloFogueteAtual = null;
            FogueteConectado = null;
            modeloAtualInstanciadoEmRuntime = false;
        }

        /// <summary>
        /// Repassa um comando de separação de estágio para o modelo conectado (ou conclui logicamente sem malha 3D).
        /// </summary>
        public bool SepararEstagioNoModelo(int indice = -1)
        {
            InicializarPontosDeEncaixe();
            return FogueteConectado != null && FogueteConectado.SepararEstagio(indice);
        }

        /// <summary>
        /// Restaura o estado lógico/visual do modelo conectado para uma nova decolagem na Terra.
        /// </summary>
        public void ReiniciarModeloNaTerra()
        {
            InicializarPontosDeEncaixe();
            if (FogueteConectado != null)
                FogueteConectado.ReiniciarEstadoModelo();
        }

        /// <summary>
        /// Sobrecarga simplificada compatível com chamadores diretos de pose básica da nave.
        /// </summary>
        public void AplicarTransformDaNave(
            Vector3 posicaoMundo,
            Quaternion rotacaoBaseMundo,
            Vector2 entradaControleSuave,
            float fatorSubidaAtmosferica,
            float ritmoImpulso,
            float deltaTime)
        {
            AplicarEstadoCompletoDaNave(
                posicaoMundo,
                rotacaoBaseMundo,
                entradaControleSuave,
                fatorSubidaAtmosferica,
                ritmoImpulso,
                Mathf.Clamp01(0.45f + fatorSubidaAtmosferica * 0.55f),
                0f,
                0f,
                0f,
                deltaTime);
        }

        /// <summary>
        /// Aplica posição, orientação, inclinação amigável, giro de retro-freio orbital, vibração e
        /// estado completo de propulsores/RCS ao encaixe e ao FogueteModeloLogica acoplado.
        /// </summary>
        public void AplicarEstadoCompletoDaNave(
            Vector3 posicaoMundo,
            Quaternion rotacaoBaseMundo,
            Vector2 entradaControleSuave,
            float fatorSubidaAtmosferica,
            float ritmoImpulso,
            float intensidadeMotorPrincipal01,
            float intensidadeRetroFreio01,
            float fatorGiroRetroFreio01,
            float tremorExtra01,
            float deltaTime)
        {
            InicializarPontosDeEncaixe();

            Vector3 inclinacaoAlvo = new Vector3(
                -entradaControleSuave.y * anguloMaximoArfagemVisual,
                entradaControleSuave.x * anguloMaximoGuinadaVisual,
                -entradaControleSuave.x * anguloMaximoInclinacaoLateral);

            if (deltaTime > 0f)
            {
                float fatorLerp = 1f - Mathf.Exp(-velocidadeSuavizacaoRotacao * deltaTime);
                inclinacaoEulerAtual = Vector3.Lerp(inclinacaoEulerAtual, inclinacaoAlvo, fatorLerp);
            }
            else
            {
                inclinacaoEulerAtual = Vector3.zero;
            }

            // Atualiza sistemas do modelo antes de consultar a rotação local adicional de retro-freio
            if (FogueteConectado != null)
            {
                FogueteConectado.AtualizarSistemasPropulsao(
                    intensidadeMotorPrincipal01,
                    entradaControleSuave,
                    intensidadeRetroFreio01,
                    fatorGiroRetroFreio01);
            }

            Quaternion rotacaoRetro = FogueteConectado != null
                ? FogueteConectado.ObterRotacaoLocalAdicionalRetroFreio()
                : Quaternion.AngleAxis(180f * Mathf.Clamp01(fatorGiroRetroFreio01), Vector3.right);

            Vector3 vibracaoLocal = CalcularVibracaoLocal(fatorSubidaAtmosferica, ritmoImpulso, tremorExtra01);
            Quaternion rotacaoInclinacaoLocal = Quaternion.Euler(inclinacaoEulerAtual) * rotacaoRetro;

            if (pontoEncaixeModelo != null && pontoEncaixeModelo != raizNave)
            {
                raizNave.SetPositionAndRotation(posicaoMundo, rotacaoBaseMundo);
                pontoEncaixeModelo.localPosition = posicaoBasePontoEncaixe + vibracaoLocal;
                pontoEncaixeModelo.localRotation = rotacaoInclinacaoLocal;
            }
            else if (FogueteConectado != null && FogueteConectado.pivotVisualFoguete != null && FogueteConectado.pivotVisualFoguete != raizNave)
            {
                Vector3 posComVibracao = posicaoMundo + rotacaoBaseMundo * vibracaoLocal;
                raizNave.SetPositionAndRotation(posComVibracao, rotacaoBaseMundo);
                FogueteConectado.pivotVisualFoguete.localRotation = rotacaoInclinacaoLocal;
            }
            else
            {
                Vector3 posComVibracao = posicaoMundo + rotacaoBaseMundo * vibracaoLocal;
                raizNave.SetPositionAndRotation(posComVibracao, rotacaoBaseMundo * rotacaoInclinacaoLocal);
            }

            AtualizarLuzesPropulsores(fatorSubidaAtmosferica, ritmoImpulso);
        }

        /// <summary>
        /// Retorna o Transform destinado a receber o futuro modelo 3D do foguete.
        /// </summary>
        public Transform ObterPontoEncaixeAtual()
        {
            InicializarPontosDeEncaixe();
            return pontoEncaixeModelo != null ? pontoEncaixeModelo : raizNave;
        }

        /// <summary>
        /// Retorna a posição do ponto de cabine (se configurado no encaixe ou no modelo) ou da raiz da nave.
        /// </summary>
        public Vector3 ObterPosicaoCabineOuNave()
        {
            InicializarPontosDeEncaixe();
            if (pontoCabineOpcional != null)
                return pontoCabineOpcional.position;
            if (FogueteConectado != null && FogueteConectado.pontoCabine != null)
                return FogueteConectado.pontoCabine.position;
            return raizNave.position;
        }

        private void SincronizarFogueteModeloLogica()
        {
            if (FogueteConectado == null && modeloFogueteAtual != null)
                FogueteConectado = modeloFogueteAtual.GetComponentInChildren<FogueteModeloLogica>(true);

            if (FogueteConectado == null && pontoEncaixeModelo != null)
                FogueteConectado = pontoEncaixeModelo.GetComponentInChildren<FogueteModeloLogica>(true);

            if (FogueteConectado == null)
                return;

            FogueteConectado.InicializarReferenciaModelo();

            if (pontoCabineOpcional == null && FogueteConectado.pontoCabine != null)
                pontoCabineOpcional = FogueteConectado.pontoCabine;

            if (pontoExaustaoPropulsorOpcional == null && FogueteConectado.pontoExaustaoPrincipal != null)
                pontoExaustaoPropulsorOpcional = FogueteConectado.pontoExaustaoPrincipal;

            if (pontoCameraExternaOpcional == null && FogueteConectado.pontoCameraExterna != null)
                pontoCameraExternaOpcional = FogueteConectado.pontoCameraExterna;
        }

        private Vector3 CalcularVibracaoLocal(float fatorSubidaAtmosferica, float ritmoImpulso, float tremorExtra01)
        {
            float baseAscensao = Mathf.Clamp01(fatorSubidaAtmosferica + Mathf.Max(0f, ritmoImpulso) * 0.45f);
            float intensidade = amplitudeVibracaoSubida * Mathf.Max(baseAscensao, Mathf.Clamp01(tremorExtra01) * 1.4f);
            if (intensidade <= 0.0001f || !Application.isPlaying)
                return Vector3.zero;

            float tempo = Time.time;
            float dx = Mathf.Sin(tempo * 21f) * intensidade * 0.5f;
            float dy = Mathf.Cos(tempo * 27f) * intensidade * 0.5f;
            float dz = Mathf.Sin(tempo * 17f) * intensidade * 0.25f;
            return new Vector3(dx, dy, dz);
        }

        private void AtualizarLuzesPropulsores(float fatorSubidaAtmosferica, float ritmoImpulso)
        {
            if (luzesPropulsoresOpcionais == null || luzesPropulsoresOpcionais.Length == 0)
                return;

            float boostPositivo = Mathf.Max(0f, ritmoImpulso);
            float intensidade = intensidadeBaseLuzPropulsor * Mathf.Lerp(0.75f, 1.35f, fatorSubidaAtmosferica)
                                + boostPositivo * intensidadeExtraLuzImpulso;

            for (int i = 0; i < luzesPropulsoresOpcionais.Length; i++)
            {
                if (luzesPropulsoresOpcionais[i] != null)
                    luzesPropulsoresOpcionais[i].intensity = intensidade;
            }
        }
    }
}
