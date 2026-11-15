using TMPro;
using UnityEngine;

namespace AstroLab.Menu
{
    /// <summary>Fontes, sprites e cores da interface do menu (asset em Assets/MenuInicial/UI/TemaMenu.asset).</summary>
    [CreateAssetMenu(menuName = "AstroLab/Menu/Tema do Menu", fileName = "TemaMenu")]
    public sealed class TemaMenu : ScriptableObject
    {
        [Header("Fontes")]
        public TMP_FontAsset fonteTitulo;
        public TMP_FontAsset fonteTexto;

        [Header("Sprites (9-slice)")]
        public Sprite arredondado;     // preenchimento com cantos arredondados
        public Sprite borda;           // só o contorno arredondado
        public Sprite brilho;          // halo suave em volta
        public Sprite seta;            // triângulo apontando para a direita
        public Sprite faixaBrilho;     // faixa de luz que atravessa o botão no hover

        [Header("Cores — azul translúcido com bordas destacadas")]
        public Color fundo = new Color(0.10f, 0.36f, 0.78f, 0.24f);
        public Color fundoHover = new Color(0.16f, 0.50f, 0.95f, 0.46f);
        public Color fundoPressionado = new Color(0.30f, 0.70f, 1.00f, 0.62f);
        public Color bordaCor = new Color(0.50f, 0.80f, 1.00f, 0.75f);
        public Color bordaHover = new Color(0.55f, 0.95f, 1.00f, 1.00f);
        public Color brilhoHover = new Color(0.28f, 0.85f, 1.00f, 0.55f);
        public Color texto = new Color(0.92f, 0.97f, 1.00f, 0.95f);
        public Color textoHover = Color.white;
        public Color textoSecundario = new Color(0.68f, 0.82f, 0.98f, 0.85f);
        public Color destaque = new Color(0.28f, 0.90f, 1.00f, 1f);        // ciano das telas (#47E6FF)
        public Color painel = new Color(0.03f, 0.08f, 0.20f, 0.72f);
        public Color painelBorda = new Color(0.45f, 0.75f, 1.00f, 0.55f);
        public Color sombra = new Color(0.07f, 0.06f, 0.20f, 0.85f);        // índigo do contorno, nunca preto
    }
}
