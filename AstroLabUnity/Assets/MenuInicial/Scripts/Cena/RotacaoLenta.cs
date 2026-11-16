using UnityEngine;

namespace AstroLab.Menu
{
    /// <summary>Gira o objeto devagar em volta do próprio eixo (Terra e nuvens).</summary>
    public sealed class RotacaoLenta : MonoBehaviour
    {
        public Vector3 eixoLocal = Vector3.up;
        public float grausPorSegundo = 1.5f;

        void Update() => transform.Rotate(eixoLocal, grausPorSegundo * Time.unscaledDeltaTime, Space.Self);
    }
}
