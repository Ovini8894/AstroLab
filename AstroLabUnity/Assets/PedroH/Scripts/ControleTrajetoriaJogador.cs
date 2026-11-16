using UnityEngine;
using UnityEngine.InputSystem;

namespace AstroLab.PedroH
{
    /// <summary>
    /// Lê e suaviza os controles de voo para que crianças conduzam a trajetória da nave
    /// com conforto (teclado WASD/Setas, controle ou InputActionAsset do projeto).
    /// </summary>
    [DisallowMultipleComponent]
    public class ControleTrajetoriaJogador : MonoBehaviour
    {
        [Header("Entrada do Projeto (Opcional)")]
        [Tooltip("InputActionAsset opcional (ex.: Assets/InputSystem_Actions.inputactions). Se vazio, lê teclado e controle diretamente.")]
        public InputActionAsset acoesInput;

        [Header("Acessibilidade e Suavização Infantil")]
        [Tooltip("Suaviza movimentos bruscos das teclas/analógico para curvas agradáveis.")]
        [Range(1f, 20f)] public float suavizacaoEntrada = 6.5f;

        [Tooltip("Zona morta para evitar desvios acidentais em controles analógicos.")]
        [Range(0f, 0.4f)] public float zonaMorta = 0.08f;

        [Tooltip("Se true, W / Seta para cima sobe a nave (mais natural para o público infantil).")]
        public bool direcaoVerticalIntuitiva = true;

        [Tooltip("Permite acelerar levemente com Espaço/Shift dentro do limite de duração de ~2 minutos.")]
        public bool permitirImpulsoSuave = true;

        public Vector2 DirecaoBruta { get; private set; }
        public Vector2 DirecaoSuave { get; private set; }
        public float RitmoImpulsoSuave { get; private set; }
        public bool TemEntradaDirecional { get; private set; }
        public bool AcaoSeparacaoPressionada { get; private set; }
        public bool RetroFreioPressionado { get; private set; }

        private InputAction acaoMover;
        private InputAction acaoImpulso;
        private InputAction acaoInteragir;
        private Vector2 direcaoExterna;
        private bool separacaoExternaSolicitada;
        private bool retroFreioExternoPressionado;

        private void Awake()
        {
            ResolverAcoesInput();
        }

        private void OnEnable()
        {
            ResolverAcoesInput();
            acaoMover?.Enable();
            acaoImpulso?.Enable();
            acaoInteragir?.Enable();
        }

        private void OnDisable()
        {
            acaoMover?.Disable();
            acaoImpulso?.Disable();
            acaoInteragir?.Disable();
            ZerarControles();
        }

        /// <summary>
        /// Resolve ações do Input System caso um InputActionAsset ou PlayerInput esteja configurado.
        /// </summary>
        public void ResolverAcoesInput()
        {
            if (acoesInput == null)
            {
                var playerInput = GetComponent<PlayerInput>();
                if (playerInput != null)
                    acoesInput = playerInput.actions;
            }

            if (acoesInput == null)
                return;

            acaoMover = acoesInput.FindAction("Player/Move", false);
            acaoImpulso = acoesInput.FindAction("Player/Sprint", false);
            acaoInteragir = acoesInput.FindAction("Player/Interact", false);
        }

        /// <summary>
        /// Permite direcionar a nave via botões virtuais ou UI futura mantendo a mesma suavização infantil.
        /// </summary>
        public void DefinirDirecaoExterna(Vector2 direcao)
        {
            direcaoExterna = Vector2.ClampMagnitude(direcao, 1f);
        }

        /// <summary>
        /// Solicita separação de estágio por botão de UI ou comando externo no próximo ciclo.
        /// </summary>
        public void SolicitarSeparacaoExterna()
        {
            separacaoExternaSolicitada = true;
        }

        /// <summary>
        /// Controla o retro-freio lunar via botão segurável de UI ou comando externo.
        /// </summary>
        public void DefinirRetroFreioExterno(bool ativo)
        {
            retroFreioExternoPressionado = ativo;
        }

        /// <summary>
        /// Atualiza leitura e suavização dos comandos direcionais, impulso, separação (Ideia 1) e retro-freio (Ideia 5).
        /// </summary>
        public void AtualizarLeituraControles(float deltaTime, bool controleHabilitado)
        {
            if (deltaTime <= 0f)
                return;

            if (!controleHabilitado)
            {
                AcaoSeparacaoPressionada = false;
                RetroFreioPressionado = false;
                separacaoExternaSolicitada = false;
                SuavizarParaZero(deltaTime);
                return;
            }

            Vector2 entrada = LerDirecaoBruta();
            if (!direcaoVerticalIntuitiva)
                entrada.y = -entrada.y;

            if (entrada.magnitude < zonaMorta)
                entrada = Vector2.zero;

            DirecaoBruta = entrada;
            TemEntradaDirecional = entrada.sqrMagnitude > 0.0001f;

            float impulsoAlvo = LerAjusteRitmoBruto();
            float fator = 1f - Mathf.Exp(-suavizacaoEntrada * deltaTime);

            DirecaoSuave = Vector2.Lerp(DirecaoSuave, entrada, fator);
            if (!TemEntradaDirecional && DirecaoSuave.sqrMagnitude < 0.0001f)
                DirecaoSuave = Vector2.zero;

            RitmoImpulsoSuave = Mathf.Lerp(RitmoImpulsoSuave, impulsoAlvo, fator);
            if (Mathf.Approximately(impulsoAlvo, 0f) && Mathf.Abs(RitmoImpulsoSuave) < 0.01f)
                RitmoImpulsoSuave = 0f;

            AcaoSeparacaoPressionada = LerAcaoSeparacaoNoFrame();
            separacaoExternaSolicitada = false;
            RetroFreioPressionado = LerRetroFreioSegurado();
        }

        /// <summary>
        /// Zera imediatamente todos os eixos, impulsos e comandos de minigames.
        /// </summary>
        public void ZerarControles()
        {
            DirecaoBruta = Vector2.zero;
            DirecaoSuave = Vector2.zero;
            RitmoImpulsoSuave = 0f;
            TemEntradaDirecional = false;
            AcaoSeparacaoPressionada = false;
            RetroFreioPressionado = false;
            direcaoExterna = Vector2.zero;
            separacaoExternaSolicitada = false;
            retroFreioExternoPressionado = false;
        }

        private void SuavizarParaZero(float deltaTime)
        {
            DirecaoBruta = Vector2.zero;
            TemEntradaDirecional = false;

            float fator = 1f - Mathf.Exp(-suavizacaoEntrada * deltaTime);
            DirecaoSuave = Vector2.Lerp(DirecaoSuave, Vector2.zero, fator);
            RitmoImpulsoSuave = Mathf.Lerp(RitmoImpulsoSuave, 0f, fator);
        }

        private Vector2 LerDirecaoBruta()
        {
            Vector2 leitura = direcaoExterna;

            if (acaoMover != null && acaoMover.enabled)
            {
                Vector2 mov = acaoMover.ReadValue<Vector2>();
                if (mov.sqrMagnitude > leitura.sqrMagnitude)
                    leitura = mov;
            }

            var kb = Keyboard.current;
            if (kb != null)
            {
                Vector2 teclado = Vector2.zero;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) teclado.x += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) teclado.x -= 1f;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) teclado.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) teclado.y -= 1f;

                if (teclado.sqrMagnitude > leitura.sqrMagnitude)
                    leitura = teclado;
            }

            var gamepad = Gamepad.current;
            if (gamepad != null)
            {
                Vector2 stick = gamepad.leftStick.ReadValue();
                Vector2 dpad = gamepad.dpad.ReadValue();
                Vector2 controle = dpad.sqrMagnitude > stick.sqrMagnitude ? dpad : stick;

                if (controle.sqrMagnitude > leitura.sqrMagnitude)
                    leitura = controle;
            }

            return Vector2.ClampMagnitude(leitura, 1f);
        }

        private float LerAjusteRitmoBruto()
        {
            if (!permitirImpulsoSuave)
                return 0f;

            bool acelerar = acaoImpulso != null && acaoImpulso.IsPressed();

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.leftShiftKey.isPressed)
                    acelerar = true;

                if (!acelerar && (kb.leftCtrlKey.isPressed || kb.cKey.isPressed))
                    return -1f;
            }

            var gamepad = Gamepad.current;
            if (gamepad != null && gamepad.rightTrigger.isPressed)
                acelerar = true;

            return acelerar ? 1f : 0f;
        }

        private bool LerAcaoSeparacaoNoFrame()
        {
            if (separacaoExternaSolicitada)
                return true;

            if (acaoInteragir != null && acaoInteragir.WasPressedThisFrame())
                return true;

            var kb = Keyboard.current;
            if (kb != null && (kb.eKey.wasPressedThisFrame || kb.fKey.wasPressedThisFrame))
                return true;

            var gamepad = Gamepad.current;
            if (gamepad != null && gamepad.buttonNorth.wasPressedThisFrame)
                return true;

            return false;
        }

        private bool LerRetroFreioSegurado()
        {
            if (retroFreioExternoPressionado)
                return true;

            var kb = Keyboard.current;
            if (kb != null && (kb.spaceKey.isPressed || kb.leftCtrlKey.isPressed || kb.cKey.isPressed))
                return true;

            var gamepad = Gamepad.current;
            if (gamepad != null && (gamepad.leftTrigger.isPressed || gamepad.buttonSouth.isPressed))
                return true;

            return false;
        }
    }
}
