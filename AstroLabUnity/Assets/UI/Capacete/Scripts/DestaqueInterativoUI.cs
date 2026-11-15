using UnityEngine;
using UnityEngine.UI;

namespace AstroLab.Capacete
{
    /// <summary>
    /// Efeito visual de destaque sutil para elementos interativos da interface (uGUI).
    /// A cada 2 segundos, produz um brilho suave em branco que percorre o elemento
    /// da esquerda para a direita (efeito sweep/sheen), mantendo total consistência
    /// com a identidade visual e sem prejudicar a leitura nem a interação.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    [DisallowMultipleComponent]
    public class DestaqueInterativoUI : BaseMeshEffect
    {
        [Tooltip("Intervalo entre cada ciclo de animação (em segundos).")]
        public float intervaloCiclo = 2.0f;

        [Tooltip("Duração do feixe de brilho percorrendo o elemento (em segundos).")]
        public float duracaoVarredura = 0.55f;

        [Tooltip("Largura relativa do feixe de brilho.")]
        [Range(0.05f, 0.5f)]
        public float larguraBrilho = 0.22f;

        [Tooltip("Intensidade máxima do clareamento/flash em branco.")]
        [Range(0.05f, 0.6f)]
        public float intensidadeMaxima = 0.32f;

        private Graphic elementoGrafico;

        protected override void Awake()
        {
            base.Awake();
            elementoGrafico = GetComponent<Graphic>();
        }

        private void Update()
        {
            if (elementoGrafico && elementoGrafico.canvasRenderer && elementoGrafico.isActiveAndEnabled)
            {
                // Força reconstrução da malha dos vértices durante o ciclo de animação
                elementoGrafico.SetVerticesDirty();
            }
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;

            // Ciclo de repetição a cada 2 segundos
            float t = (Time.unscaledTime % Mathf.Max(0.1f, intervaloCiclo));
            if (t > duracaoVarredura)
            {
                // Fora do período da varredura, não altera os vértices
                return;
            }

            // Progresso normalizado da varredura (0 = extrema esquerda, 1 = extrema direita)
            float progresso = t / duracaoVarredura;

            // Descobre os limites X do elemento no espaço local
            UIVertex vert = new UIVertex();
            float minX = float.MaxValue;
            float maxX = float.MinValue;

            int count = vh.currentVertCount;
            for (int i = 0; i < count; i++)
            {
                vh.PopulateUIVertex(ref vert, i);
                if (vert.position.x < minX) minX = vert.position.x;
                if (vert.position.x > maxX) maxX = vert.position.x;
            }

            float larguraTotal = Mathf.Max(1f, maxX - minX);

            // Centro do brilho percorrendo de (minX - margem) até (maxX + margem)
            float margem = larguraTotal * larguraBrilho;
            float centroX = Mathf.Lerp(minX - margem, maxX + margem, progresso);
            float raioX = margem;

            // Aplica o realce de cor branca nos vértices de acordo com a proximidade ao centro do brilho
            for (int i = 0; i < count; i++)
            {
                vh.PopulateUIVertex(ref vert, i);

                float dist = Mathf.Abs(vert.position.x - centroX);
                if (dist < raioX)
                {
                    // Curva suave de sino
                    float fator = Mathf.Cos((dist / raioX) * (Mathf.PI * 0.5f));
                    float brilho = fator * intensidadeMaxima;

                    Color32 c = vert.color;
                    // Interpola suavemente em direção ao branco puro mantendo o alfa original
                    c.r = (byte)Mathf.Clamp(c.r + (255 - c.r) * brilho, 0, 255);
                    c.g = (byte)Mathf.Clamp(c.g + (255 - c.g) * brilho, 0, 255);
                    c.b = (byte)Mathf.Clamp(c.b + (255 - c.b) * brilho, 0, 255);

                    vert.color = c;
                    vh.SetUIVertex(vert, i);
                }
            }
        }
    }
}
