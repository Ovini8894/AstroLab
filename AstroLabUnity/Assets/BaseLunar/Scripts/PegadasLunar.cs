using System.Collections.Generic;
using UnityEngine;

namespace AstroLab.BaseLunar
{
    /// <summary>
    /// Gera pegadas na areia/regolito lunar conforme o jogador caminha.
    /// Mantém cerca de 10 passos aparentes na areia; os passos mais antigos
    /// esvanecem suavemente e somem, respeitando o limite.
    /// </summary>
    [DisallowMultipleComponent]
    public class PegadasLunar : MonoBehaviour
    {
        [Header("Configuração dos Passos")]
        [Tooltip("Número máximo de pegadas visíveis simultaneamente na areia lunar.")]
        [Range(4, 20)]
        public int maximoPassos = 10;

        [Tooltip("Distância percorrida para marcar cada novo passo (metros).")]
        [Range(0.4f, 2.0f)]
        public float distanciaPorPasso = 0.85f;

        [Tooltip("Afastamento lateral alternado de cada pé em relação ao centro (metros).")]
        [Range(0.05f, 0.4f)]
        public float espacamentoPes = 0.18f;

        [Header("Tamanho da Pegada")]
        public Vector2 tamanhoPegada = new Vector2(0.24f, 0.42f);

        [Header("Duração e Desvanecimento")]
        [Tooltip("Tempo em segundos antes de uma pegada começar a sumir por tempo, caso o jogador fique parado.")]
        public float tempoVidaSegundos = 30f;

        [Header("Material")]
        public Material materialPegada;

        [Header("Camadas de Colisão")]
        public LayerMask camadasSolo = ~0;

        private sealed class PegadaInstance
        {
            public GameObject gameObject;
            public MeshFilter meshFilter;
            public MeshRenderer renderer;
            public Mesh mesh;
            public MaterialPropertyBlock mpb;
            public Color corBase;
            public float tempoCriacao;
            public float opacidadeAtual = 1f;
        }

        private readonly List<PegadaInstance> pegadasAtivas = new List<PegadaInstance>();
        private CharacterController characterController;
        private Vector3 ultimaPosicaoPasso;
        private bool peDireito = true;
        private Transform containerPegadas;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            ultimaPosicaoPasso = transform.position;

            if (!materialPegada)
            {
                // Tenta carregar o material padrão criado para a pegada lunar
                materialPegada = Resources.Load<Material>("M_Pegada_Lunar");
                if (!materialPegada)
                {
                    Shader s = Shader.Find("AstroLab/PegadaLunar") ?? Shader.Find("Universal Render Pipeline/Unlit");
                    if (s) materialPegada = new Material(s);
                }
            }

            GameObject container = new GameObject("Container_PegadasLunares");
            containerPegadas = container.transform;
        }

        private void OnDestroy()
        {
            if (containerPegadas)
            {
                Destroy(containerPegadas.gameObject);
            }
        }

        private void Update()
        {
            AtualizarPassos();
            AtualizarOpacidade();
        }

        private void AtualizarPassos()
        {
            // Verifica se está em solo
            bool noChao = characterController ? characterController.isGrounded : true;
            if (!noChao) return;

            Vector3 delta = transform.position - ultimaPosicaoPasso;
            delta.y = 0f; // Apenas deslocamento no plano horizontal

            if (delta.magnitude >= distanciaPorPasso)
            {
                CriarPasso();
                ultimaPosicaoPasso = transform.position;
            }
        }

        private void CriarPasso()
        {
            // Alterna entre pé direito e esquerdo
            float offsetLateral = peDireito ? espacamentoPes : -espacamentoPes;
            peDireito = !peDireito;

            Vector3 origemRaio = transform.position + (transform.right * offsetLateral) + (Vector3.up * 0.5f);
            if (Physics.Raycast(origemRaio, Vector3.down, out RaycastHit hit, 2.0f, camadasSolo, QueryTriggerInteraction.Ignore))
            {
                // Não cria pegada sobre o próprio jogador ou se a inclinação for excessiva (ex: parede)
                if (hit.collider.transform.IsChildOf(transform)) return;
                if (Vector3.Angle(hit.normal, Vector3.up) > 45f) return;

                InstanciarPegada(hit.point, hit.normal, transform.forward);
            }
        }

        private void InstanciarPegada(Vector3 pontoImpacto, Vector3 normal, Vector3 direcaoFrente)
        {
            // Se já atingiu o limite de cerca de 10 passos, o mais antigo é removido
            while (pegadasAtivas.Count >= maximoPassos)
            {
                RemoverPegadaMaisAntiga();
            }

            GameObject go = new GameObject("PegadaLunar");
            go.transform.SetParent(containerPegadas);

            // Alinha a pegada com a superfície inclinada do solo
            Vector3 frenteProjetada = Vector3.ProjectOnPlane(direcaoFrente, normal).normalized;
            if (frenteProjetada.sqrMagnitude < 0.001f) frenteProjetada = transform.forward;
            Quaternion rotacao = Quaternion.LookRotation(frenteProjetada, normal);

            // Leve elevação (offset normal de 2mm) para evitar z-fighting com o mesh do terreno
            go.transform.position = pontoImpacto + (normal * 0.003f);
            go.transform.rotation = rotacao;

            MeshFilter mf = go.AddComponent<MeshFilter>();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();

            Mesh mesh = CriarMeshQuad(tamanhoPegada.x, tamanhoPegada.y);
            mf.sharedMesh = mesh;

            if (materialPegada)
            {
                mr.sharedMaterial = materialPegada;
            }
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            MaterialPropertyBlock mpb = new MaterialPropertyBlock();
            Color corPadrao = materialPegada && materialPegada.HasProperty("_BaseColor")
                ? materialPegada.GetColor("_BaseColor")
                : new Color(0.16f, 0.16f, 0.20f, 0.85f);

            mpb.SetColor("_BaseColor", corPadrao);
            mr.SetPropertyBlock(mpb);

            PegadaInstance instancia = new PegadaInstance
            {
                gameObject = go,
                meshFilter = mf,
                renderer = mr,
                mesh = mesh,
                mpb = mpb,
                corBase = corPadrao,
                tempoCriacao = Time.time,
                opacidadeAtual = 1f
            };

            pegadasAtivas.Add(instancia);
        }

        private Mesh CriarMeshQuad(float largura, float comprimento)
        {
            Mesh m = new Mesh { name = "MeshPegada" };
            float hx = largura * 0.5f;
            float hz = comprimento * 0.5f;

            m.vertices = new[]
            {
                new Vector3(-hx, 0f, -hz),
                new Vector3( hx, 0f, -hz),
                new Vector3( hx, 0f,  hz),
                new Vector3(-hx, 0f,  hz)
            };

            m.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f)
            };

            m.normals = new[]
            {
                Vector3.up,
                Vector3.up,
                Vector3.up,
                Vector3.up
            };

            m.colors = new[]
            {
                Color.white,
                Color.white,
                Color.white,
                Color.white
            };

            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            return m;
        }

        private void AtualizarOpacidade()
        {
            float agora = Time.time;
            for (int i = pegadasAtivas.Count - 1; i >= 0; i--)
            {
                PegadaInstance p = pegadasAtivas[i];
                if (!p.gameObject)
                {
                    pegadasAtivas.RemoveAt(i);
                    continue;
                }

                // Cálculo de degradação:
                // 1. Posição na fila (os últimos 2 a 3 passos do conjunto de 10 começam a esvanecer suavemente)
                float fatorPosicaoFila = 1f;
                if (pegadasAtivas.Count > 1)
                {
                    // i = 0 é a pegada mais antiga, i = count - 1 é a mais recente
                    float indiceNorm = (float)i / (maximoPassos - 1);
                    // Passos entre os 3 mais antigos diminuem progressivamente
                    if (indiceNorm < 0.35f)
                    {
                        fatorPosicaoFila = Mathf.Clamp01(indiceNorm / 0.35f + 0.15f);
                    }
                }

                // 2. Tempo de vida caso o jogador fique parado muito tempo
                float idade = agora - p.tempoCriacao;
                float fatorTempo = 1f;
                if (idade > tempoVidaSegundos * 0.7f)
                {
                    fatorTempo = Mathf.Clamp01(1f - (idade - tempoVidaSegundos * 0.7f) / (tempoVidaSegundos * 0.3f));
                }

                float opacidadeFinal = Mathf.Min(fatorPosicaoFila, fatorTempo);
                p.opacidadeAtual = Mathf.MoveTowards(p.opacidadeAtual, opacidadeFinal, Time.deltaTime * 1.5f);

                if (p.opacidadeAtual <= 0.01f)
                {
                    DestruirInstancia(p);
                    pegadasAtivas.RemoveAt(i);
                    continue;
                }

                Color c = p.corBase;
                c.a *= p.opacidadeAtual;
                p.mpb.SetColor("_BaseColor", c);
                p.renderer.SetPropertyBlock(p.mpb);
            }
        }

        private void RemoverPegadaMaisAntiga()
        {
            if (pegadasAtivas.Count == 0) return;
            PegadaInstance maisAntiga = pegadasAtivas[0];
            pegadasAtivas.RemoveAt(0);
            DestruirInstancia(maisAntiga);
        }

        private void DestruirInstancia(PegadaInstance p)
        {
            if (p.mesh) Destroy(p.mesh);
            if (p.gameObject) Destroy(p.gameObject);
        }
    }
}
