using UnityEngine;

namespace AstroLab.Centro
{
    // Perfis de movimento do JogadorFPS. O JogadorFPS vem configurado para a Lua; na Terra o pulo e a queda
    // precisam de outros números (senão o jogador pula quase 3 m com gravidade 9,81).
    [System.Serializable]
    public struct PerfilGravidade
    {
        public float gravidade, alturaPulo, multiplicadorAlturaPulo, velocidadeMaximaQueda, controleAereo;

        public static readonly PerfilGravidade Terra = new PerfilGravidade
        { gravidade = 9.81f, alturaPulo = 0.45f, multiplicadorAlturaPulo = 1f, velocidadeMaximaQueda = 25f, controleAereo = 4f };

        public static readonly PerfilGravidade Lua = new PerfilGravidade
        { gravidade = 1.62f, alturaPulo = 1.2f, multiplicadorAlturaPulo = 2.35f, velocidadeMaximaQueda = 3.4f, controleAereo = 2.8f };

        public void AplicarEm(JogadorFPS j)
        {
            if (j == null) return;
            j.gravidade = gravidade;
            j.alturaPulo = alturaPulo;
            j.multiplicadorAlturaPulo = multiplicadorAlturaPulo;
            j.velocidadeMaximaQueda = velocidadeMaximaQueda;
            j.controleAereo = controleAereo;
        }
    }
}
