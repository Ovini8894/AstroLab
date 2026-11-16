using UnityEngine;

namespace AstroLab.PedroH
{
    public enum FaseViagemEspacial
    {
        ProntaNaTerra = 0,
        SubindoDaTerra = 1,
        CruzeiroEspacial = 2,
        AproximandoDaLua = 3,
        ChegouNaLua = 4
    }

    /// <summary>
    /// Calcula e controla a trajetória da nave partindo da Terra até a Lua com duração
    /// aproximada de 2 minutos (120s), permitindo que crianças conduzam a rota com
    /// segurança dentro de um corredor espacial guiado.
    /// </summary>
    [DisallowMultipleComponent]
    public class TrajetoriaViagemLunar : MonoBehaviour
    {
        [Header("Pontos de Referência da Rota (Terra -> Lua)")]
        [Tooltip("Transform opcional representando a Terra ou ponto de lançamento na Terra.")]
        public Transform pontoPartidaTerra;

        [Tooltip("Posição padrão da Terra usada caso pontoPartidaTerra ainda não esteja conectado.")]
        public Vector3 posicaoPadraoTerra = Vector3.zero;

        [Tooltip("Deslocamento local a partir de pontoPartidaTerra (ex.: altura da superfície da Terra).")]
        public Vector3 offsetPartidaTerra = Vector3.zero;

        [Tooltip("Transform opcional representando a Lua ou ponto de chegada lunar.")]
        public Transform pontoChegadaLua;

        [Tooltip("Posição padrão da Lua usada caso pontoChegadaLua ainda não esteja conectado.")]
        public Vector3 posicaoPadraoLua = new Vector3(0f, 900f, 2400f);

        [Tooltip("Deslocamento até o ponto de chegada ou órbita da Lua.")]
        public Vector3 offsetChegadaLua = Vector3.zero;

        [Header("Subida Inicial a Partir da Terra")]
        [Tooltip("Transform opcional cujo eixo Y (up) define a vertical da decolagem na Terra.")]
        public Transform referenciaVerticalSubida;

        [Tooltip("Direção inicial da subida a partir da Terra quando referenciaVerticalSubida estiver vazia.")]
        public Vector3 direcaoPadraoSubidaTerra = Vector3.up;

        [Tooltip("Fração da rota dedicada à subida vertical inicial saindo da Terra.")]
        [Range(0.10f, 0.45f)] public float proporcaoAlturaSubidaInicial = 0.30f;

        [Tooltip("Fração da rota dedicada à curvatura suave de aproximação à Lua.")]
        [Range(0.10f, 0.40f)] public float proporcaoCurvaAproximacaoLua = 0.25f;

        [Header("Duração da Viagem (~2 Minutos)")]
        [Tooltip("Duração alvo completa da viagem Terra -> Lua em segundos.")]
        [Min(30f)] public float duracaoAlvoSegundos = 105f;

        [Tooltip("Limita variações de impulso para manter uma duração previsível perto do alvo.")]
        public bool manterDuracaoAproximadaDoisMinutos = false;

        [Range(0.6f, 1.0f)] public float multiplicadorMinimoRitmo = 0.85f;
        [Range(1.0f, 1.8f)] public float multiplicadorMaximoRitmo = 1.55f;

        [Tooltip("Velocidade de rampa de aceleração longitudinal ao pressionar impulso.")]
        [Range(0.5f, 8f)] public float velocidadeAceleracaoImpulso = 2.6f;

        [Tooltip("Velocidade de desaceleração longitudinal por inércia ao soltar o impulso.")]
        [Range(0.5f, 8f)] public float velocidadeDesaceleracaoImpulso = 1.3f;

        [Header("Controle de Trajetória com Aceleração e Inércia")]
        [Tooltip("Amplitude lateral máxima (esquerda/direita) em metros no corredor de voo.")]
        [Min(1f)] public float raioCorredorLateral = 26f;

        [Tooltip("Amplitude vertical máxima (subir/descer na rota) em metros no corredor de voo.")]
        [Min(1f)] public float raioCorredorVertical = 14f;

        [Tooltip("Aceleração lateral da nave (m/s²) ao acionar esquerda/direita.")]
        [Min(5f)] public float aceleracaoLateral = 54f;

        [Tooltip("Aceleração vertical da nave (m/s²) ao acionar subir/descer.")]
        [Min(5f)] public float aceleracaoVertical = 38f;

        [Tooltip("Velocidade lateral máxima (m/s) dentro do corredor.")]
        [Min(2f)] public float velocidadeMaximaLateral = 28f;

        [Tooltip("Velocidade vertical máxima (m/s) dentro do corredor.")]
        [Min(2f)] public float velocidadeMaximaVertical = 18f;

        [Tooltip("Amortecimento por inércia espacial quando o jogador solta os comandos (menor = desliza mais).")]
        [Range(0.5f, 8f)] public float amortecimentoSemEntrada = 2.4f;

        [Tooltip("Amortecimento leve enquanto manobra, ajudando em curvas suaves e responsivas.")]
        [Range(0.2f, 6f)] public float amortecimentoComEntrada = 1.5f;

        [Tooltip("Retorna continuamente ao centro da rota mesmo sem entrada (desligado preserva o desvio feito pelo jogador).")]
        public bool autoCentralizarSemEntrada = false;

        [Range(0f, 5f)] public float velocidadeRetornoAoCentro = 0.8f;

        [Tooltip("Progresso normalizado em que o controle direcional começa a atuar após deixar a Terra.")]
        [Range(0.01f, 0.15f)] public float inicioControleAposDecolagem = 0.03f;

        [Tooltip("Progresso normalizado em que a nave começa a se alinhar suavemente para chegar à Lua.")]
        [Range(0.75f, 0.96f)] public float inicioAlinhamentoLunar = 0.85f;

        [Header("Telemetria Real Terra-Lua (Somente Leitura)")]
        public float distanciaTerraLuaKm = 384400f;

        public float Progresso01 { get; private set; }
        public float TempoDecorridoSegundos { get; private set; }
        public float TempoRestanteEstimadoSegundos => Mathf.Max(0f, duracaoAlvoSegundos - TempoDecorridoSegundos);
        public float VelocidadeEscalarAtual { get; private set; }
        public float MultiplicadorRitmoAtual { get; private set; } = 1f;
        public float FatorImpulsoAtual { get; private set; }
        public Vector2 DesvioCorredorMetros { get; private set; }
        public Vector2 VelocidadeCorredorMetrosPorSeg { get; private set; }
        public Vector3 PosicaoAtualMundo { get; private set; }
        public Quaternion OrientacaoBaseMundo { get; private set; } = Quaternion.identity;
        public FaseViagemEspacial FaseAtual { get; private set; } = FaseViagemEspacial.ProntaNaTerra;
        public bool EmViagem { get; private set; }

        public float DistanciaPercorridaKm => distanciaTerraLuaKm * Progresso01;
        public float DistanciaRestanteKm => distanciaTerraLuaKm * (1f - Progresso01);

        /// <summary>
        /// Fator 1 na decolagem na Terra e diminuindo suavemente conforme sai da atmosfera.
        /// </summary>
        public float FatorSubidaAtmosferica => 1f - Mathf.SmoothStep(0f, 0.28f, Progresso01);

        private Vector3 pontoP0;
        private Vector3 pontoP1;
        private Vector3 pontoP2;
        private Vector3 pontoP3;
        private Vector3 eixoDireitaReferencia = Vector3.right;

        /// <summary>
        /// Configura dinamicamente os transforms de partida (Terra), chegada (Lua) e referência vertical.
        /// </summary>
        public void ConfigurarPontosRota(Transform terra, Transform lua, Transform verticalSubida = null)
        {
            pontoPartidaTerra = terra;
            pontoChegadaLua = lua;
            if (verticalSubida != null)
                referenciaVerticalSubida = verticalSubida;

            PrepararInicioNaTerra();
        }

        /// <summary>
        /// Posiciona a lógica no ponto inicial na Terra (0%), pronta para iniciar a subida.
        /// </summary>
        public void PrepararInicioNaTerra()
        {
            EmViagem = false;
            Progresso01 = 0f;
            TempoDecorridoSegundos = 0f;
            VelocidadeEscalarAtual = 0f;
            MultiplicadorRitmoAtual = 1f;
            FatorImpulsoAtual = 0f;
            DesvioCorredorMetros = Vector2.zero;
            VelocidadeCorredorMetrosPorSeg = Vector2.zero;
            FaseAtual = FaseViagemEspacial.ProntaNaTerra;

            AtualizarPontosControleCurva();
            PosicaoAtualMundo = pontoP0;
            OrientacaoBaseMundo = CalcularOrientacaoEstavelNaTrajetoria(0f, Vector2.zero);
        }

        /// <summary>
        /// Aplica um impulso externo na velocidade lateral/vertical da nave (ex.: leve recuo ao encostar em asteroide).
        /// </summary>
        public void AplicarImpulsoExternoCorredor(Vector2 impulsoVel)
        {
            VelocidadeCorredorMetrosPorSeg += impulsoVel;
            VelocidadeCorredorMetrosPorSeg = new Vector2(
                Mathf.Clamp(VelocidadeCorredorMetrosPorSeg.x, -velocidadeMaximaLateral * 1.35f, velocidadeMaximaLateral * 1.35f),
                Mathf.Clamp(VelocidadeCorredorMetrosPorSeg.y, -velocidadeMaximaVertical * 1.35f, velocidadeMaximaVertical * 1.35f));
        }

        /// <summary>
        /// Inicia a subida da nave partindo da Terra rumo à Lua.
        /// </summary>
        public void IniciarSubida()
        {
            PrepararInicioNaTerra();
            EmViagem = true;
            FaseAtual = FaseViagemEspacial.SubindoDaTerra;
        }

        /// <summary>
        /// Atualiza o avanço temporal (~2 minutos), a curva de subida a partir da Terra e
        /// o controle de trajetória da nave conduzido pelo jogador.
        /// </summary>
        public FaseViagemEspacial AvancarTrajetoria(Vector2 entradaDirecao, float ajusteRitmo, float deltaTime)
        {
            if (!EmViagem || deltaTime <= 0f)
                return FaseAtual;

            Vector3 posicaoAnterior = PosicaoAtualMundo;

            AtualizarPontosControleCurva();

            TempoDecorridoSegundos += deltaTime;
            float passo = CalcularPassoProgresso(ajusteRitmo, deltaTime);
            Progresso01 = Mathf.Clamp01(Progresso01 + passo);

            AtualizarDesvioNaTrajetoria(entradaDirecao, Progresso01, deltaTime);

            PosicaoAtualMundo = AvaliarPosicaoNaCurvaComDesvio(Progresso01, DesvioCorredorMetros);
            VelocidadeEscalarAtual = (PosicaoAtualMundo - posicaoAnterior).magnitude / deltaTime;
            OrientacaoBaseMundo = CalcularOrientacaoEstavelNaTrajetoria(Progresso01, DesvioCorredorMetros);

            FaseAtual = AvaliarFasePorProgresso(Progresso01);

            if (Progresso01 >= 1f)
            {
                Progresso01 = 1f;
                DesvioCorredorMetros = Vector2.zero;
                VelocidadeCorredorMetrosPorSeg = Vector2.zero;
                FatorImpulsoAtual = 0f;
                PosicaoAtualMundo = pontoP3;
                EmViagem = false;
                FaseAtual = FaseViagemEspacial.ChegouNaLua;
            }

            return FaseAtual;
        }

        /// <summary>
        /// Retorna a posição de partida na Terra considerando Transform ou fallback configurado.
        /// </summary>
        public Vector3 ObterPosicaoPartidaTerra()
        {
            if (pontoPartidaTerra != null)
                return pontoPartidaTerra.position + pontoPartidaTerra.TransformDirection(offsetPartidaTerra);

            return posicaoPadraoTerra + offsetPartidaTerra;
        }

        /// <summary>
        /// Retorna a posição de chegada na Lua considerando Transform ou fallback configurado.
        /// </summary>
        public Vector3 ObterPosicaoChegadaLua()
        {
            if (pontoChegadaLua != null)
                return pontoChegadaLua.position + offsetChegadaLua;

            return posicaoPadraoLua + offsetChegadaLua;
        }

        /// <summary>
        /// Calcula o envelope [0..1] do corredor de manobra: zero na saída da Terra e na chegada à Lua,
        /// liberado rapidamente após a decolagem para resposta clara ao jogador.
        /// </summary>
        public float CalcularEnvelopeCorredor(float t)
        {
            float inicio = Mathf.Clamp(inicioControleAposDecolagem, 0.01f, 0.20f);
            float fimInicio = Mathf.Min(inicio + 0.06f, 0.30f);
            float entrada = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(inicio, fimInicio, t));

            float alinhamento = Mathf.Clamp(inicioAlinhamentoLunar, 0.55f, 0.98f);
            float saida = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(alinhamento, 1f, t));

            return Mathf.Clamp01(entrada * saida);
        }

        /// <summary>
        /// Avalia a posição no espaço para o progresso t somada ao deslocamento lateral/vertical controlado.
        /// </summary>
        public Vector3 AvaliarPosicaoNaCurvaComDesvio(float t, Vector2 desvioMetros)
        {
            t = Mathf.Clamp01(t);
            Vector3 pontoCentral = AvaliarBezierCubica(pontoP0, pontoP1, pontoP2, pontoP3, t);
            Vector3 frenteCentral = AvaliarTangenteBezierCubica(pontoP0, pontoP1, pontoP2, pontoP3, t);

            CalcularBaseOrtonormal(frenteCentral, out Vector3 direita, out Vector3 cima);
            float envelope = CalcularEnvelopeCorredor(t);

            return pontoCentral
                   + direita * (desvioMetros.x * envelope)
                   + cima * (desvioMetros.y * envelope);
        }

        /// <summary>
        /// Calcula uma rotação contínua e sem travamentos de eixo (gimbal) acompanhando tanto a
        /// curvatura da rota quanto a direção real do vetor de velocidade lateral/vertical.
        /// </summary>
        public Quaternion CalcularOrientacaoEstavelNaTrajetoria(float t, Vector2 desvioMetros)
        {
            t = Mathf.Clamp01(t);
            Vector3 direcaoBase;

            if (t < 0.975f)
            {
                float tFrente = Mathf.Min(1f, t + 0.025f);
                Vector3 posAtual = AvaliarPosicaoNaCurvaComDesvio(t, desvioMetros);
                Vector3 posFrente = AvaliarPosicaoNaCurvaComDesvio(tFrente, desvioMetros);
                Vector3 delta = posFrente - posAtual;

                direcaoBase = delta.sqrMagnitude > 0.0001f
                    ? delta.normalized
                    : AvaliarTangenteBezierCubica(pontoP0, pontoP1, pontoP2, pontoP3, t);
            }
            else
            {
                direcaoBase = AvaliarTangenteBezierCubica(pontoP0, pontoP1, pontoP2, pontoP3, t);
            }

            if (direcaoBase.sqrMagnitude < 0.0001f)
                direcaoBase = ObterDirecaoSubidaInicial();

            CalcularBaseOrtonormal(direcaoBase, out Vector3 direitaEstavel, out Vector3 cimaEstavel);

            // Soma a velocidade real do corredor espacial para apontar o nariz na direção efetiva do voo
            float velFrenteRef = Mathf.Max(14f, VelocidadeEscalarAtual * 2.2f);
            float envelope = CalcularEnvelopeCorredor(t);
            Vector3 vetorVelocidadeReal = direcaoBase.normalized * velFrenteRef
                                        + direitaEstavel * (VelocidadeCorredorMetrosPorSeg.x * envelope * 0.42f)
                                        + cimaEstavel * (VelocidadeCorredorMetrosPorSeg.y * envelope * 0.42f);

            Vector3 direcaoFinal = vetorVelocidadeReal.sqrMagnitude > 0.001f
                ? vetorVelocidadeReal.normalized
                : direcaoBase.normalized;

            CalcularBaseOrtonormal(direcaoFinal, out _, out Vector3 cimaFinal);
            return Quaternion.LookRotation(direcaoFinal, cimaFinal);
        }

        private float CalcularPassoProgresso(float ajusteRitmo, float deltaTime)
        {
            float duracaoSegura = Mathf.Max(30f, duracaoAlvoSegundos);
            float taxaBase = 1f / duracaoSegura;

            float alvoImpulso = Mathf.Clamp(ajusteRitmo, -1f, 1f);
            float taxaRampa = alvoImpulso > FatorImpulsoAtual
                ? velocidadeAceleracaoImpulso
                : velocidadeDesaceleracaoImpulso;
            FatorImpulsoAtual = Mathf.MoveTowards(FatorImpulsoAtual, alvoImpulso, taxaRampa * deltaTime);

            float mult = 1f;
            if (FatorImpulsoAtual > 0.005f)
                mult = Mathf.Lerp(1f, multiplicadorMaximoRitmo, Mathf.Clamp01(FatorImpulsoAtual));
            else if (FatorImpulsoAtual < -0.005f)
                mult = Mathf.Lerp(1f, multiplicadorMinimoRitmo, Mathf.Clamp01(-FatorImpulsoAtual));

            // Regulador gentil opcional na reta final
            if (manterDuracaoAproximadaDoisMinutos && Progresso01 > 0.85f && Progresso01 < 1f)
            {
                float restanteAlvo = duracaoSegura - (TempoDecorridoSegundos - deltaTime);
                if (restanteAlvo > 0.5f)
                {
                    float taxaRegulada = (1f - Progresso01) / restanteAlvo;
                    float multRegulado = Mathf.Clamp(taxaRegulada / taxaBase, 0.82f, 1.35f);
                    float pesoConvergencia = Mathf.InverseLerp(0.85f, 0.96f, Progresso01);
                    mult = Mathf.Lerp(mult, multRegulado, pesoConvergencia * 0.65f);
                }
            }

            MultiplicadorRitmoAtual = mult;
            return taxaBase * mult * deltaTime;
        }

        private void AtualizarPontosControleCurva()
        {
            pontoP0 = ObterPosicaoPartidaTerra();
            pontoP3 = ObterPosicaoChegadaLua();

            Vector3 vetorTerraLua = pontoP3 - pontoP0;
            if (vetorTerraLua.sqrMagnitude < 25f)
            {
                Vector3 dirFallback = ObterDirecaoSubidaInicial();
                pontoP3 = pontoP0 + dirFallback * 200f;
                vetorTerraLua = pontoP3 - pontoP0;
            }

            float distancia = vetorTerraLua.magnitude;
            Vector3 dirRota = vetorTerraLua / distancia;
            Vector3 dirSubida = ObterDirecaoSubidaInicial();

            // P1 garante que a subida comece verticalmente a partir da Terra antes de inclinar para o espaço
            pontoP1 = pontoP0 + dirSubida * (distancia * Mathf.Clamp(proporcaoAlturaSubidaInicial, 0.10f, 0.45f));

            // P2 desenha um arco orbital suave de chegada à Lua sem quedas bruscas
            pontoP2 = pontoP3 - dirRota * (distancia * Mathf.Clamp(proporcaoCurvaAproximacaoLua, 0.10f, 0.40f))
                              + dirSubida * (distancia * 0.06f);

            // Eixo lateral constante ao plano Terra -> Subida -> Lua evita inversões da câmera/nave ao sair da vertical
            Vector3 eixoPlano = Vector3.Cross(dirSubida, dirRota);
            if (eixoPlano.sqrMagnitude > 0.001f)
            {
                eixoDireitaReferencia = eixoPlano.normalized;
            }
            else
            {
                Vector3 referenciaAux = Mathf.Abs(Vector3.Dot(dirSubida, Vector3.forward)) < 0.9f
                    ? Vector3.forward
                    : Vector3.right;
                eixoDireitaReferencia = Vector3.Cross(dirSubida, referenciaAux).normalized;
            }
        }

        private Vector3 ObterDirecaoSubidaInicial()
        {
            if (referenciaVerticalSubida != null && referenciaVerticalSubida.up.sqrMagnitude > 0.001f)
                return referenciaVerticalSubida.up.normalized;

            if (pontoPartidaTerra != null && offsetPartidaTerra.sqrMagnitude > 0.01f)
                return pontoPartidaTerra.TransformDirection(offsetPartidaTerra).normalized;

            return direcaoPadraoSubidaTerra.sqrMagnitude > 0.001f
                ? direcaoPadraoSubidaTerra.normalized
                : Vector3.up;
        }

        private void CalcularBaseOrtonormal(Vector3 frente, out Vector3 direita, out Vector3 cima)
        {
            Vector3 f = frente.sqrMagnitude > 0.0001f ? frente.normalized : Vector3.up;
            Vector3 cimaBruto = Vector3.Cross(f, eixoDireitaReferencia);

            if (cimaBruto.sqrMagnitude < 0.0001f)
            {
                Vector3 aux = Mathf.Abs(Vector3.Dot(f, Vector3.right)) < 0.9f ? Vector3.right : Vector3.forward;
                cimaBruto = Vector3.Cross(f, aux);
            }

            cima = cimaBruto.normalized;
            direita = Vector3.Cross(cima, f).normalized;
        }

        private void AtualizarDesvioNaTrajetoria(Vector2 entrada, float t, float deltaTime)
        {
            float envelope = CalcularEnvelopeCorredor(t);
            if (envelope <= 0.001f)
            {
                VelocidadeCorredorMetrosPorSeg = Vector2.Lerp(VelocidadeCorredorMetrosPorSeg, Vector2.zero, Mathf.Min(1f, deltaTime * 4f));
                DesvioCorredorMetros = Vector2.MoveTowards(DesvioCorredorMetros, Vector2.zero, velocidadeMaximaLateral * deltaTime);
                return;
            }

            Vector2 vel = VelocidadeCorredorMetrosPorSeg;

            // Aceleração nos eixos X (lateral) e Y (vertical) baseada em massa/empuxo RCS
            vel.x += entrada.x * aceleracaoLateral * deltaTime;
            vel.y += entrada.y * aceleracaoVertical * deltaTime;

            // Amortecimento espacial diferenciado: suave ao soltar para preservar inércia, levemente mais firme ao manobrar
            float taxaArrastoX = Mathf.Abs(entrada.x) > 0.05f ? amortecimentoComEntrada : amortecimentoSemEntrada;
            float taxaArrastoY = Mathf.Abs(entrada.y) > 0.05f ? amortecimentoComEntrada : amortecimentoSemEntrada;

            vel.x *= Mathf.Exp(-taxaArrastoX * deltaTime);
            vel.y *= Mathf.Exp(-taxaArrastoY * deltaTime);

            if (autoCentralizarSemEntrada)
            {
                if (Mathf.Abs(entrada.x) < 0.05f)
                    vel.x -= DesvioCorredorMetros.x * velocidadeRetornoAoCentro * 0.35f * deltaTime;
                if (Mathf.Abs(entrada.y) < 0.05f)
                    vel.y -= DesvioCorredorMetros.y * velocidadeRetornoAoCentro * 0.35f * deltaTime;
            }

            vel.x = Mathf.Clamp(vel.x, -velocidadeMaximaLateral, velocidadeMaximaLateral);
            vel.y = Mathf.Clamp(vel.y, -velocidadeMaximaVertical, velocidadeMaximaVertical);

            if (Mathf.Abs(entrada.x) < 0.02f && Mathf.Abs(vel.x) < 0.05f) vel.x = 0f;
            if (Mathf.Abs(entrada.y) < 0.02f && Mathf.Abs(vel.y) < 0.05f) vel.y = 0f;

            Vector2 atualizado = DesvioCorredorMetros + vel * deltaTime;

            // Limita dentro da elipse do corredor zerando suavemente a componente radial de saída (sem rebote brusco)
            float invLat = 1f / Mathf.Max(0.1f, raioCorredorLateral);
            float invVert = 1f / Mathf.Max(0.1f, raioCorredorVertical);
            Vector2 normalizado = new Vector2(atualizado.x * invLat, atualizado.y * invVert);

            if (normalizado.sqrMagnitude > 1f)
            {
                normalizado.Normalize();
                atualizado.x = normalizado.x * raioCorredorLateral;
                atualizado.y = normalizado.y * raioCorredorVertical;

                Vector2 normEspaco = new Vector2(normalizado.x * invLat, normalizado.y * invVert).normalized;
                float projFora = Vector2.Dot(vel, normEspaco);
                if (projFora > 0f)
                    vel -= normEspaco * projFora;
            }

            VelocidadeCorredorMetrosPorSeg = vel;
            DesvioCorredorMetros = atualizado;
        }

        private FaseViagemEspacial AvaliarFasePorProgresso(float t)
        {
            if (t <= 0f) return FaseViagemEspacial.ProntaNaTerra;
            if (t < 0.20f) return FaseViagemEspacial.SubindoDaTerra;
            if (t < inicioAlinhamentoLunar) return FaseViagemEspacial.CruzeiroEspacial;
            if (t < 1f) return FaseViagemEspacial.AproximandoDaLua;
            return FaseViagemEspacial.ChegouNaLua;
        }

        private static Vector3 AvaliarBezierCubica(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float u = 1f - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;

            return (uuu * p0)
                 + (3f * uu * t * p1)
                 + (3f * u * tt * p2)
                 + (ttt * p3);
        }

        private static Vector3 AvaliarTangenteBezierCubica(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float u = 1f - t;
            Vector3 deriv = (3f * u * u * (p1 - p0))
                          + (6f * u * t * (p2 - p1))
                          + (3f * t * t * (p3 - p2));

            return deriv.sqrMagnitude > 0.0001f ? deriv.normalized : (p3 - p0).normalized;
        }
    }
}
