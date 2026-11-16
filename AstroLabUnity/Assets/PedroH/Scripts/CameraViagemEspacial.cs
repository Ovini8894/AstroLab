using UnityEngine;

namespace AstroLab.PedroH
{
    /// <summary>
    /// Câmera de acompanhamento espacial para a viagem da nave:
    /// mantém a nave exatamente centralizada no início da fase e transmite
    /// sensação de aceleração, inércia e curvas suaves durante o voo.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public class CameraViagemEspacial : MonoBehaviour
    {
        [Header("Referências")]
        public TrajetoriaViagemLunar trajetoria;
        public ControleTrajetoriaJogador controleJogador;
        public MinigameSeparacaoEstagios minigameEstagios;

        [Header("Enquadramento Base (Centralizado no Início)")]
        public Vector3 posicaoLocalBase = new Vector3(0f, 4.0f, -14.5f);
        public Vector3 alvoLocalBase = new Vector3(0f, 0f, 0.515f);
        [Range(40f, 80f)] public float fovBase = 60f;

        [Header("Sensação Dinâmica de Inércia e Aceleração")]
        [Tooltip("Atraso lateral da câmera ao curvar, destacando a inércia lateral da nave.")]
        [Range(0f, 3f)] public float deslocamentoInerciaLateral = 1.15f;

        [Tooltip("Atraso vertical da câmera ao subir/descer no corredor.")]
        [Range(0f, 2.5f)] public float deslocamentoInerciaVertical = 0.75f;

        [Tooltip("Recuo extra de câmera ao usar o impulso espacial.")]
        [Range(0f, 3f)] public float recuoZImpulso = 1.35f;

        [Tooltip("Abertura máxima de FOV durante o impulso espacial ou bônus de estágio.")]
        [Range(0f, 15f)] public float incrementoFovImpulso = 6.5f;

        [Tooltip("Inclinação leve do horizonte da câmera nas curvas laterais.")]
        [Range(0f, 10f)] public float rollMaximoCamera = 4.5f;

        [Range(1f, 20f)] public float velocidadeSuavizacaoPosicao = 6.5f;
        [Range(1f, 20f)] public float velocidadeSuavizacaoFov = 4.5f;

        private Camera cameraComponente;
        private Vector3 offsetAtual;
        private float rollAtual;
        private float fovExtraAtual;
        private float intensidadeTremorImpacto;

        private void Awake()
        {
            cameraComponente = GetComponent<Camera>();
            AutoVincular();
            ResetarImediatamente();
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying || cameraComponente == null)
                return;

            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            AtualizarAcompanhamento(dt);
        }

        /// <summary>
        /// Retorna a câmera exatamente para a posição centralizada inicial (usado em terra e reset).
        /// </summary>
        public void ResetarImediatamente()
        {
            offsetAtual = Vector3.zero;
            rollAtual = 0f;
            fovExtraAtual = 0f;
            intensidadeTremorImpacto = 0f;

            if (cameraComponente == null)
                cameraComponente = GetComponent<Camera>();

            transform.localPosition = posicaoLocalBase;
            transform.localRotation = CalcularRotacaoOlharBase(0f);

            if (cameraComponente != null)
                cameraComponente.fieldOfView = fovBase;
        }

        /// <summary>
        /// Dispara um tremor rápido na câmera (ex.: leve esbarrão em asteroide).
        /// </summary>
        public void AcionarTremorImpacto(float intensidade01)
        {
            intensidadeTremorImpacto = Mathf.Max(intensidadeTremorImpacto, Mathf.Clamp01(intensidade01));
        }

        /// <summary>
        /// Atualiza posição, inclinação e FOV suaves conforme velocidade e impulso reais da trajetória.
        /// </summary>
        public void AtualizarAcompanhamento(float deltaTime)
        {
            AutoVincular();

            if (cameraComponente == null || deltaTime <= 0f)
                return;

            Vector2 velNormalizada = Vector2.zero;
            float impulsoAtual01 = 0f;

            if (trajetoria != null && trajetoria.EmViagem)
            {
                float maxLat = Mathf.Max(1f, trajetoria.velocidadeMaximaLateral);
                float maxVert = Mathf.Max(1f, trajetoria.velocidadeMaximaVertical);
                velNormalizada = new Vector2(
                    Mathf.Clamp(trajetoria.VelocidadeCorredorMetrosPorSeg.x / maxLat, -1f, 1f),
                    Mathf.Clamp(trajetoria.VelocidadeCorredorMetrosPorSeg.y / maxVert, -1f, 1f));

                impulsoAtual01 = trajetoria.FatorImpulsoAtual;
            }
            else if (controleJogador != null)
            {
                velNormalizada = controleJogador.DirecaoSuave;
                impulsoAtual01 = controleJogador.RitmoImpulsoSuave;
            }

            float bonusEstagio = minigameEstagios != null ? minigameEstagios.ImpulsoBonusAtual01 : 0f;
            float energiaImpulso = Mathf.Clamp01(Mathf.Max(impulsoAtual01, bonusEstagio * 0.75f));

            Vector3 offsetAlvo = new Vector3(
                -velNormalizada.x * deslocamentoInerciaLateral,
                -velNormalizada.y * deslocamentoInerciaVertical,
                -energiaImpulso * recuoZImpulso);

            float fatorPos = 1f - Mathf.Exp(-velocidadeSuavizacaoPosicao * deltaTime);
            offsetAtual = Vector3.Lerp(offsetAtual, offsetAlvo, fatorPos);

            float rollAlvo = -velNormalizada.x * rollMaximoCamera;
            rollAtual = Mathf.Lerp(rollAtual, rollAlvo, fatorPos);

            float fovAlvo = incrementoFovImpulso * energiaImpulso;
            float fatorFov = 1f - Mathf.Exp(-velocidadeSuavizacaoFov * deltaTime);
            fovExtraAtual = Mathf.Lerp(fovExtraAtual, fovAlvo, fatorFov);

            Vector3 tremorOffset = Vector3.zero;
            if (intensidadeTremorImpacto > 0.005f)
            {
                float t = Time.time;
                float amp = intensidadeTremorImpacto * 0.28f;
                tremorOffset = new Vector3(
                    Mathf.Sin(t * 43f) * amp,
                    Mathf.Cos(t * 37f) * amp,
                    0f);
                intensidadeTremorImpacto = Mathf.MoveTowards(intensidadeTremorImpacto, 0f, deltaTime * 2.2f);
            }

            transform.localPosition = posicaoLocalBase + offsetAtual + tremorOffset;
            transform.localRotation = CalcularRotacaoOlharBase(rollAtual);
            cameraComponente.fieldOfView = fovBase + fovExtraAtual;
        }

        private Quaternion CalcularRotacaoOlharBase(float rollGraus)
        {
            Vector3 direcao = (alvoLocalBase - posicaoLocalBase - offsetAtual * 0.35f).normalized;
            if (direcao.sqrMagnitude < 0.0001f)
                direcao = Vector3.forward;

            return Quaternion.LookRotation(direcao, Vector3.up) * Quaternion.AngleAxis(rollGraus, Vector3.forward);
        }

        private void AutoVincular()
        {
            if (trajetoria == null)
                trajetoria = GetComponentInParent<TrajetoriaViagemLunar>();

            if (controleJogador == null)
                controleJogador = GetComponentInParent<ControleTrajetoriaJogador>();

            if (minigameEstagios == null)
                minigameEstagios = GetComponentInParent<MinigameSeparacaoEstagios>();
        }
    }
}
