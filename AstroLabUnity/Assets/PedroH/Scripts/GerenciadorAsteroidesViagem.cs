using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace AstroLab.PedroH
{
    /// <summary>
    /// Gerencia os asteroides reais ao longo da trajetória Terra -> Lua,
    /// emitindo alertas antecipados quando um asteroide está diretamente à frente
    /// da rota atual da nave e tratando desvios e impactos leves de forma justa.
    /// </summary>
    [DisallowMultipleComponent]
    public class GerenciadorAsteroidesViagem : MonoBehaviour
    {
        [Header("Referências da Viagem")]
        public TrajetoriaViagemLunar trajetoria;
        public Transform raizAsteroidesRota;
        public Transform naveReferencia;
        public CameraViagemEspacial cameraViagem;

        [Header("Parâmetros Justos de Alerta e Colisão")]
        [Tooltip("Raio aproximado da nave usada para detectar esbarrões justos com asteroides (calibrado para o Ônibus Espacial).")]
        [Min(0.5f)] public float raioNaveMetros = 2.2f;

        [Tooltip("Distância à frente em que o asteroide passa a emitir alerta se estiver na rota atual.")]
        [Range(30f, 160f)] public float distanciaAntecedenciaAlerta = 95f;

        [Tooltip("Margem extra sobre o raio de colisão para considerar o asteroide 'na rota de colisão'.")]
        [Range(1f, 8f)] public float margemLateralAlertaMetros = 3.2f;

        [Tooltip("Força do impulso lateral automático de segurança ao encostar num asteroide.")]
        [Range(4f, 35f)] public float impulsoDesvioPosImpacto = 16f;

        [Header("Eventos para HUD e Feedback")]
        public UnityEvent<AsteroideObstaculo, Vector2> aoDetectarAsteroideNaRota = new UnityEvent<AsteroideObstaculo, Vector2>();
        public UnityEvent aoLimparAlertaAsteroide = new UnityEvent();
        public UnityEvent<AsteroideObstaculo> aoColidirComAsteroide = new UnityEvent<AsteroideObstaculo>();

        public AsteroideObstaculo AsteroideAlertaAtual { get; private set; }
        public Vector2 DirecaoSugeridaDesvio { get; private set; }
        public int TotalColisoesNaViagem { get; private set; }

        private readonly List<AsteroideObstaculo> asteroides = new List<AsteroideObstaculo>();
        private AsteroideObstaculo ultimoAlertaNotificado;

        private void Awake()
        {
            AutoVincular();
            AtualizarListaAsteroides();
        }

        /// <summary>
        /// Recarrega a lista de asteroides filhos do contêiner da rota.
        /// </summary>
        public void AtualizarListaAsteroides()
        {
            asteroides.Clear();
            AutoVincular();

            if (raizAsteroidesRota != null)
            {
                raizAsteroidesRota.GetComponentsInChildren(true, asteroides);
            }
            else
            {
                var encontrados = FindObjectsByType<AsteroideObstaculo>(FindObjectsInactive.Exclude);
                if (encontrados != null && encontrados.Length > 0)
                    asteroides.AddRange(encontrados);
            }
        }

        /// <summary>
        /// Restaura todos os asteroides e limpa alertas ao iniciar/reiniciar na Terra.
        /// </summary>
        public void ReiniciarAsteroides()
        {
            AtualizarListaAsteroides();
            TotalColisoesNaViagem = 0;
            AsteroideAlertaAtual = null;
            ultimoAlertaNotificado = null;
            DirecaoSugeridaDesvio = Vector2.zero;

            for (int i = 0; i < asteroides.Count; i++)
            {
                if (asteroides[i] != null)
                    asteroides[i].ReiniciarEstado();
            }
        }

        /// <summary>
        /// Atualiza rotação dos asteroides, alerta antecipado de rota e detecção justa de impacto.
        /// </summary>
        public void AtualizarAsteroides(float progresso01, Vector3 posicaoNaveMundo, Quaternion orientacaoNave, float deltaTime)
        {
            if (asteroides.Count == 0)
                AtualizarListaAsteroides();

            Vector3 frenteNave = (orientacaoNave * Vector3.forward).normalized;
            Vector3 direitaNave = (orientacaoNave * Vector3.right).normalized;
            Vector3 cimaNave = (orientacaoNave * Vector3.up).normalized;

            AsteroideObstaculo melhorCandidatoAlerta = null;
            float menorDistanciaAlerta = float.MaxValue;
            Vector2 melhorDirecaoDesvio = Vector2.zero;

            for (int i = 0; i < asteroides.Count; i++)
            {
                AsteroideObstaculo ast = asteroides[i];
                if (ast == null || !ast.gameObject.activeInHierarchy)
                    continue;

                ast.AtualizarAsteroide(deltaTime);

                Vector3 deltaParaAst = ast.transform.position - posicaoNaveMundo;
                float distanciaTotal = deltaParaAst.magnitude;
                float raioSoma = raioNaveMetros + ast.raioColisaoMetros;

                // 1. Checagem justa de colisão esférica com cooldown
                if (!ast.EmCooldownImpacto && distanciaTotal <= raioSoma)
                {
                    TratarImpacto(ast, deltaParaAst, direitaNave, cimaNave);
                    continue;
                }

                // 2. Detecção antecipada acompanhando a curvatura real da trajetória (sem falsos positivos da Bézier)
                float projFrente = Vector3.Dot(deltaParaAst, frenteNave);
                bool aFrenteNaRota = ast.progressoNaRota > progresso01 && projFrente > raioSoma && projFrente <= distanciaAntecedenciaAlerta;
                if (aFrenteNaRota && progresso01 < 0.88f)
                {
                    float distLateral;
                    Vector2 projCorredor;

                    if (trajetoria != null && !ast.JaImpactouNaViagem)
                    {
                        Vector3 pontoNaveNaSecao = trajetoria.AvaliarPosicaoNaCurvaComDesvio(ast.progressoNaRota, trajetoria.DesvioCorredorMetros);
                        Quaternion orientSecao = trajetoria.CalcularOrientacaoEstavelNaTrajetoria(ast.progressoNaRota, trajetoria.DesvioCorredorMetros);
                        Vector3 deltaNaSecao = ast.transform.position - pontoNaveNaSecao;
                        distLateral = deltaNaSecao.magnitude;
                        projCorredor = new Vector2(
                            Vector3.Dot(deltaNaSecao, orientSecao * Vector3.right),
                            Vector3.Dot(deltaNaSecao, orientSecao * Vector3.up));
                    }
                    else
                    {
                        Vector3 offsetPlano = deltaParaAst - frenteNave * projFrente;
                        distLateral = offsetPlano.magnitude;
                        projCorredor = new Vector2(
                            Vector3.Dot(offsetPlano, direitaNave),
                            Vector3.Dot(offsetPlano, cimaNave));
                    }

                    float limiteAlerta = raioSoma + margemLateralAlertaMetros;
                    if (distLateral <= limiteAlerta && projFrente < menorDistanciaAlerta)
                    {
                        menorDistanciaAlerta = projFrente;
                        melhorCandidatoAlerta = ast;
                        melhorDirecaoDesvio = CalcularDirecaoSeguraDesvio(projCorredor, ast.coordenadaCorredor);
                    }
                }
            }

            AsteroideAlertaAtual = melhorCandidatoAlerta;
            DirecaoSugeridaDesvio = melhorDirecaoDesvio;

            if (melhorCandidatoAlerta != null)
            {
                if (ultimoAlertaNotificado != melhorCandidatoAlerta)
                {
                    ultimoAlertaNotificado = melhorCandidatoAlerta;
                    aoDetectarAsteroideNaRota?.Invoke(melhorCandidatoAlerta, melhorDirecaoDesvio);
                }
            }
            else if (ultimoAlertaNotificado != null)
            {
                ultimoAlertaNotificado = null;
                aoLimparAlertaAsteroide?.Invoke();
            }
        }

        private void TratarImpacto(AsteroideObstaculo ast, Vector3 deltaParaAst, Vector3 direitaNave, Vector3 cimaNave)
        {
            TotalColisoesNaViagem++;

            Vector3 dirParaNave = (-deltaParaAst);
            Vector2 desvioCorredor = new Vector2(
                Vector3.Dot(dirParaNave, direitaNave),
                Vector3.Dot(dirParaNave, cimaNave));

            if (desvioCorredor.sqrMagnitude < 0.01f)
                desvioCorredor = CalcularDirecaoSeguraDesvio(Vector2.zero, ast.coordenadaCorredor);
            else
                desvioCorredor.Normalize();

            Vector3 dirEmpurraoAst = -(direitaNave * desvioCorredor.x + cimaNave * desvioCorredor.y);
            if (dirEmpurraoAst.sqrMagnitude > 0.001f)
                dirEmpurraoAst = (dirEmpurraoAst.normalized + (deltaParaAst.sqrMagnitude > 0.001f ? deltaParaAst.normalized * 0.25f : Vector3.zero)).normalized;
            else
                dirEmpurraoAst = -direitaNave;

            ast.RegistrarImpacto(dirEmpurraoAst);

            if (trajetoria != null)
                trajetoria.AplicarImpulsoExternoCorredor(desvioCorredor * impulsoDesvioPosImpacto);

            if (cameraViagem != null)
                cameraViagem.AcionarTremorImpacto(0.8f);

            if (ultimoAlertaNotificado == ast)
            {
                ultimoAlertaNotificado = null;
                aoLimparAlertaAsteroide?.Invoke();
            }

            aoColidirComAsteroide?.Invoke(ast);
        }

        private Vector2 CalcularDirecaoSeguraDesvio(Vector2 offsetNaveParaAst, Vector2 coordAsteroide)
        {
            // Sugere desviar na direção contrária ao centro do asteroide; se alinhado em cheio, foge rumo ao lado mais aberto
            if (offsetNaveParaAst.sqrMagnitude > 0.25f)
                return (-offsetNaveParaAst).normalized;

            Vector2 paraCentro = -coordAsteroide;
            if (paraCentro.sqrMagnitude > 0.25f)
                return paraCentro.normalized;

            return Vector2.right;
        }

        private void AutoVincular()
        {
            if (trajetoria == null)
                trajetoria = GetComponent<TrajetoriaViagemLunar>();

            if (naveReferencia == null && trajetoria != null)
                naveReferencia = trajetoria.transform;

            if (cameraViagem == null)
                cameraViagem = GetComponentInChildren<CameraViagemEspacial>(true);

            if (raizAsteroidesRota == null)
            {
                var obj = GameObject.Find("Ambiente_Espacial/Asteroides_Rota");
                if (obj != null)
                    raizAsteroidesRota = obj.transform;
            }
        }
    }
}
