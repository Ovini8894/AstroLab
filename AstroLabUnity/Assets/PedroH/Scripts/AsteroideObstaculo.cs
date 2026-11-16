using UnityEngine;

namespace AstroLab.PedroH
{
    /// <summary>
    /// Representa um asteroide individual posicionado ao longo do corredor da rota Terra -> Lua.
    /// Gira continuamente no espaço, armazena sua posição/escala de corredor para previsão justa
    /// e reage com leve empurrão ao ser tocado, sem travar a viagem.
    /// </summary>
    [DisallowMultipleComponent]
    public class AsteroideObstaculo : MonoBehaviour
    {
        [Header("Coordenadas na Trajetória Terra -> Lua")]
        [Range(0.15f, 0.88f)] public float progressoNaRota = 0.35f;
        public Vector2 coordenadaCorredor = Vector2.zero;

        [Header("Colisão Justa e Dinâmica Espacial")]
        [Tooltip("Raio físico justo de colisão em metros (mantido ligeiramente menor que a silhueta visual).")]
        [Min(0.5f)] public float raioColisaoMetros = 3.2f;

        [Tooltip("Velocidade angular constante do asteroide no espaço.")]
        public Vector3 velocidadeRotacaoDegPorSeg = new Vector3(12f, 20f, 8f);

        public bool EmCooldownImpacto => tempoRestanteCooldown > 0f;
        public bool JaImpactouNaViagem { get; private set; }

        private Vector3 posicaoBaseMundo;
        private Vector3 velocidadeDerivaImpacto;
        private Vector3 velocidadeAngularAtual;
        private float tempoRestanteCooldown;
        private bool baseCapturada;

        private void Awake()
        {
            CapturarBaseSeNecessario();
            velocidadeAngularAtual = velocidadeRotacaoDegPorSeg;
        }

        /// <summary>
        /// Define a posição original calibrada do asteroide ao longo da rota.
        /// </summary>
        public void ConfigurarPosicaoBase(Vector3 posicaoMundo, float progresso, Vector2 coordCorredor, float raioJusto)
        {
            progressoNaRota = progresso;
            coordenadaCorredor = coordCorredor;
            raioColisaoMetros = Mathf.Max(0.5f, raioJusto);
            posicaoBaseMundo = posicaoMundo;
            baseCapturada = true;
            transform.position = posicaoMundo;
        }

        /// <summary>
        /// Restaura o asteroide para sua posição e rotação iniciais (ex.: reinício na Terra).
        /// </summary>
        public void ReiniciarEstado()
        {
            CapturarBaseSeNecessario();
            transform.position = posicaoBaseMundo;
            velocidadeDerivaImpacto = Vector3.zero;
            velocidadeAngularAtual = velocidadeRotacaoDegPorSeg;
            tempoRestanteCooldown = 0f;
            JaImpactouNaViagem = false;
        }

        /// <summary>
        /// Atualiza giro contínuo e eventual deriva pós-impacto.
        /// </summary>
        public void AtualizarAsteroide(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            if (tempoRestanteCooldown > 0f)
                tempoRestanteCooldown = Mathf.Max(0f, tempoRestanteCooldown - deltaTime);

            transform.Rotate(velocidadeAngularAtual * deltaTime, Space.Self);

            if (velocidadeDerivaImpacto.sqrMagnitude > 0.0001f)
            {
                transform.position += velocidadeDerivaImpacto * deltaTime;
                velocidadeDerivaImpacto = Vector3.Lerp(velocidadeDerivaImpacto, Vector3.zero, deltaTime * 0.65f);
            }
        }

        /// <summary>
        /// Aplica recuo visual ao asteroide após contato leve com a nave.
        /// </summary>
        public void RegistrarImpacto(Vector3 direcaoEmpurraoMundo, float cooldownSegundos = 2.2f)
        {
            CapturarBaseSeNecessario();
            JaImpactouNaViagem = true;
            tempoRestanteCooldown = Mathf.Max(0.2f, cooldownSegundos);

            Vector3 dir = direcaoEmpurraoMundo.sqrMagnitude > 0.001f
                ? direcaoEmpurraoMundo.normalized
                : Vector3.up;

            velocidadeDerivaImpacto = dir * 7.5f;
            velocidadeAngularAtual = velocidadeRotacaoDegPorSeg * 2.5f + new Vector3(28f, -34f, 19f);
        }

        private void CapturarBaseSeNecessario()
        {
            if (!baseCapturada)
            {
                posicaoBaseMundo = transform.position;
                baseCapturada = true;
            }
        }
    }
}
