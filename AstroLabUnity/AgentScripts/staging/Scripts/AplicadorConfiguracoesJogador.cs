using AstroLab.Settings;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AstroLab.Centro
{
    // Copia as opções do menu (AstroLab.Settings.GameSettings) para o JogadorFPS e a câmera, sem editar o JogadorFPS.
    // - sensibilidade: 0.12 × multiplicador do menu
    // - FOV: direto na câmera
    // - inverter Y: o JogadorFPS não tem a opção; aqui a câmera mantém o próprio pitch (invertido) e o reaplica no
    //   LateUpdate, depois do JogadorFPS (ordem de execução alta).
    [DefaultExecutionOrder(200)]
    [RequireComponent(typeof(JogadorFPS))]
    public class AplicadorConfiguracoesJogador : MonoBehaviour
    {
        public const float SensibilidadeBase = 0.12f;

        JogadorFPS jogador;
        Camera cam;
        bool inverterY;
        float pitch;
        bool travado;                       // quando o jogador está sentado/em diálogo, outro script controla a câmera

        void Awake()
        {
            jogador = GetComponent<JogadorFPS>();
            cam = GetComponentInChildren<Camera>();
        }

        void OnEnable()
        {
            GameSettings.OnChanged += Aplicar;
            Aplicar();
        }

        void OnDisable() => GameSettings.OnChanged -= Aplicar;
        void OnDestroy() => GameSettings.OnChanged -= Aplicar;

        public void Aplicar()
        {
            if (jogador) jogador.sensibilidadeMouse = SensibilidadeBase * GameSettings.MouseSensitivity;
            if (cam) cam.fieldOfView = GameSettings.FieldOfView;
            bool novo = GameSettings.InvertY;
            if (novo && !inverterY && cam) pitch = NormalizarPitch(cam.transform.localEulerAngles.x);
            inverterY = novo;
        }

        public void TravarCamera(bool valor) => travado = valor;

        static float NormalizarPitch(float x) => x > 180f ? x - 360f : x;

        void LateUpdate()
        {
            if (!inverterY || travado || cam == null || jogador == null || !jogador.enabled) return;
            if (Cursor.lockState != CursorLockMode.Locked) return;
            var mouse = Mouse.current;
            if (mouse == null) return;
            float dy = mouse.delta.ReadValue().y * jogador.sensibilidadeMouse;
            pitch = Mathf.Clamp(pitch + dy, -85f, 85f);
            cam.transform.localEulerAngles = new Vector3(pitch, 0f, 0f);
        }
    }
}
