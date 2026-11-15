using UnityEngine;

namespace AstroLab.Menu
{
    /// <summary>
    /// Mantém o objeto (a Terra) num ponto fixo da TELA, a uma distância fixa da câmera, em qualquer proporção
    /// (16:9, 16:10, 21:9...). Recalcula só quando o tamanho da tela muda (a paralaxe leve da câmera continua).
    /// </summary>

    public sealed class AncoraViewport : MonoBehaviour
    {
        public Camera cameraMenu;
        [Tooltip("Posição na tela (0..1). Ex.: (0.82, 0.62) = terço superior direito.")]
        public Vector2 viewport = new Vector2(0.82f, 0.62f);
        public float distancia = 650f;

        Vector2Int ultimaTela;
        Quaternion rotBase;
        bool temBase;

        void OnEnable() { temBase = false; ultimaTela = default; }

        void LateUpdate()
        {
            if (!cameraMenu) return;
            if (!temBase) { rotBase = cameraMenu.transform.rotation; temBase = true; }
            var tela = new Vector2Int(cameraMenu.pixelWidth, cameraMenu.pixelHeight);
            if (tela == ultimaTela) return;
            ultimaTela = tela;
            Posicionar();
        }

        public void Posicionar()
        {
            if (!cameraMenu) return;
            var t = cameraMenu.transform;
            var rotAtual = t.rotation;
            if (temBase) t.rotation = rotBase;                     // ignora a paralaxe do momento
            var raio = cameraMenu.ViewportPointToRay(new Vector3(viewport.x, viewport.y, 0f));
            t.rotation = rotAtual;
            transform.position = raio.origin + raio.direction * distancia;
        }
    }
}
