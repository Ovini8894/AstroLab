using UnityEngine;

namespace AstroLab.Menu
{
    /// <summary>Oscila o objeto devagar (±graus) em volta de um eixo local — a Terra "respira" sem tirar o Brasil de vista.</summary>
    public sealed class OscilacaoLenta : MonoBehaviour
    {
        public Vector3 eixoLocal = Vector3.up;
        public float amplitudeGraus = 6f;
        public float periodo = 48f;

        Quaternion rotBase;

        void Awake() => rotBase = transform.localRotation;

        void Update()
        {
            float a = amplitudeGraus * Mathf.Sin(Time.unscaledTime * 2f * Mathf.PI / Mathf.Max(1f, periodo));
            transform.localRotation = rotBase * Quaternion.AngleAxis(a, eixoLocal);
        }
    }
}
