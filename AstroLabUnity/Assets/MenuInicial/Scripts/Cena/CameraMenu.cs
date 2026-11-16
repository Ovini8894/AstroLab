using UnityEngine;
using UnityEngine.InputSystem;

namespace AstroLab.Menu
{
    /// <summary>Câmera do menu: leve paralaxe com o mouse, balanço lento e tremor curto no lançamento.</summary>
    public sealed class CameraMenu : MonoBehaviour
    {
        [Tooltip("Giro máximo (graus) seguindo o mouse: x = horizontal, y = vertical.")]
        public Vector2 paralaxe = new Vector2(1.2f, 0.7f);
        public float balanco = 0.25f;
        public float suavidade = 2.5f;

        Quaternion rotBase;
        Vector3 posBase;
        Vector2 atual;
        float tremor, tremorDur, tremorT;

        public float Influencia { get; set; } = 1f;

        void Awake() { rotBase = transform.localRotation; posBase = transform.localPosition; }

        public void Tremer(float forca, float duracao) { tremor = forca; tremorDur = duracao; tremorT = 0f; }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            Vector2 alvo = Vector2.zero;
            var m = Mouse.current;
            if (m != null && Screen.width > 0)
            {
                var p = m.position.ReadValue();
                alvo = new Vector2(Mathf.Clamp(p.x / Screen.width * 2f - 1f, -1f, 1f), Mathf.Clamp(p.y / Screen.height * 2f - 1f, -1f, 1f));
            }
            atual = Vector2.Lerp(atual, alvo * Influencia, 1f - Mathf.Exp(-suavidade * dt));
            float t = Time.unscaledTime;
            float yaw = atual.x * paralaxe.x + Mathf.Sin(t * 0.11f) * balanco;
            float pitch = -atual.y * paralaxe.y + Mathf.Sin(t * 0.07f + 1.3f) * balanco * 0.6f;

            Vector3 sacudida = Vector3.zero;
            if (tremorT < tremorDur)
            {
                tremorT += dt;
                float k = tremor * (1f - tremorT / tremorDur);
                sacudida = new Vector3(Mathf.PerlinNoise(t * 31f, 0f) - 0.5f, Mathf.PerlinNoise(0f, t * 29f) - 0.5f, 0f) * k;
            }
            transform.localRotation = rotBase * Quaternion.Euler(pitch + sacudida.y * 2f, yaw + sacudida.x * 2f, 0f);
            transform.localPosition = posBase;
        }
    }
}
