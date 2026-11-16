using UnityEngine;
using UnityEngine.InputSystem;

namespace AstroLab.PedroH
{
    /// <summary>
    /// Lê e suaviza os controles de voo para que a navegação espacial seja clara,
    /// responsiva e intuitiva (WASD/Setas para conduzir, Shift/Espaço para impulso,
    /// E/F para separação de estágios). Sem lógica de frenagem nesta cena.
    /// </summary>
    [DisallowMultipleComponent]
    public class ControleTrajetoriaJogador : MonoBehaviour
    {
        public const string TextoResumoControles = "WASD / Setas: Navegar   •   Shift / Espaço: Impulso   •   E: Desacoplar estágio";

        [Header("Entrada do Projeto (Opcional)")]
        [Tooltip("InputActionAsset opcional (ex.: Assets/InputSystem_Actions.inputactions). Se vazio, lê teclado e controle diretamente.")]
        public InputActionAsset acoesInput;

        [Header("Responsividade e Suavização")]
        [Tooltip("Quão rápido o comando responde ao pressionar as teclas/analógico.")]
        [Range(2f, 25f)] public float suavizacaoEntrada = 11f;

        [Tooltip("Quão rápido a leitura de comando retorna a zero ao soltar (a inércia física fica na trajetória).")]
        [Range(2f, 25f)] public float suavizacaoRetornoEntrada = 8.5f;

        [Tooltip("Zona morta para evitar desvios acidentais em controles analógicos.")]
        [Range(0f, 0.4f)] public float zonaMorta = 0.08f;

        [Tooltip("Se verdadeiro, W / Seta para cima sobe a nave.")]
        public bool direcaoVerticalIntuitiva = true;

        [Tooltip("Permite acelerar o voo com Shift ou Espaço.")]
        public bool permitirImpulsoSuave = true;

        public Vector2 DirecaoBruta { get; private set; }
        public Vector2 DirecaoSuave { get; private set; }
        public float RitmoImpulsoSuave { get; private set; }
        public bool ImpulsoAtivo { get; private set; }
        public bool TemEntradaDirecional { get; private set; }
        public bool AcaoSeparacaoPressionada { get; private set; }

        private InputAction acaoMover;
        private InputAction acaoImpulso;
        private InputAction acaoInteragir;
        private InputAction acaoPular;
        private Vector2 direcaoExterna;
        private bool separacaoExternaSolicitada;
        private bool impulsoExternoSolicitado;

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
            acaoPular?.Enable();
        }

        private void OnDisable()
        {
            acaoMover?.Disable();
            acaoImpulso?.Disable();
            acaoInteragir?.Disable();
            acaoPular?.Disable();
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
            acaoPular = acoesInput.FindAction("Player/Jump", false);
        }

        /// <summary>
        /// Permite direcionar a nave via botões virtuais ou UI futura mantendo a mesma suavização.
        /// </summary>
        public void DefinirDirecaoExterna(Vector2 direcao)
        {
            direcaoExterna = Vector2.ClampMagnitude(direcao, 1f);
        }

        /// <summary>
        /// Permite acionar o impulso espacial por UI ou script externo.
        /// </summary>
        public void DefinirImpulsoExterno(bool ativo)
        {
            impulsoExternoSolicitado = ativo;
        }

        /// <summary>
        /// Solicita separação de estágio por botão de UI ou comando externo no próximo ciclo.
        /// </summary>
        public void SolicitarSeparacaoExterna()
        {
            separacaoExternaSolicitada = true;
        }

        /// <summary>
        /// Atualiza leitura e suavização dos comandos direcionais, impulso e separação de estágios.
        /// </summary>
        public void AtualizarLeituraControles(float deltaTime, bool controleHabilitado)
        {
            if (deltaTime <= 0f)
                return;

            if (!controleHabilitado)
            {
                AcaoSeparacaoPressionada = false;
                ImpulsoAtivo = false;
                separacaoExternaSolicitada = false;
                impulsoExternoSolicitado = false;
                direcaoExterna = Vector2.zero;
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

            float taxaSuavizacao = TemEntradaDirecional ? suavizacaoEntrada : suavizacaoRetornoEntrada;
            float fator = 1f - Mathf.Exp(-taxaSuavizacao * deltaTime);

            DirecaoSuave = Vector2.Lerp(DirecaoSuave, entrada, fator);
            if (!TemEntradaDirecional && DirecaoSuave.sqrMagnitude < 0.0001f)
                DirecaoSuave = Vector2.zero;

            float impulsoAlvo = LerImpulsoBruto();
            ImpulsoAtivo = impulsoAlvo > 0.5f;
            float taxaImpulso = ImpulsoAtivo ? suavizacaoEntrada * 0.65f : suavizacaoRetornoEntrada * 0.5f;
            float fatorImpulso = 1f - Mathf.Exp(-taxaImpulso * deltaTime);

            RitmoImpulsoSuave = Mathf.Lerp(RitmoImpulsoSuave, impulsoAlvo, fatorImpulso);
            if (!ImpulsoAtivo && Mathf.Abs(RitmoImpulsoSuave) < 0.005f)
                RitmoImpulsoSuave = 0f;

            AcaoSeparacaoPressionada = LerAcaoSeparacaoNoFrame();
            separacaoExternaSolicitada = false;
        }

        /// <summary>
        /// Zera imediatamente todos os eixos, impulso e comandos de estágio.
        /// </summary>
        public void ZerarControles()
        {
            DirecaoBruta = Vector2.zero;
            DirecaoSuave = Vector2.zero;
            RitmoImpulsoSuave = 0f;
            ImpulsoAtivo = false;
            TemEntradaDirecional = false;
            AcaoSeparacaoPressionada = false;
            direcaoExterna = Vector2.zero;
            separacaoExternaSolicitada = false;
            impulsoExternoSolicitado = false;
        }

        private void SuavizarParaZero(float deltaTime)
        {
            DirecaoBruta = Vector2.zero;
            TemEntradaDirecional = false;

            float fator = 1f - Mathf.Exp(-suavizacaoRetornoEntrada * deltaTime);
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

        private float LerImpulsoBruto()
        {
            if (!permitirImpulsoSuave)
                return 0f;

            if (impulsoExternoSolicitado)
                return 1f;

            if ((acaoImpulso != null && acaoImpulso.IsPressed()) ||
                (acaoPular != null && acaoPular.IsPressed()))
            {
                return 1f;
            }

            var kb = Keyboard.current;
            if (kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed || kb.spaceKey.isPressed))
                return 1f;

            var gamepad = Gamepad.current;
            if (gamepad != null && (gamepad.rightTrigger.isPressed || gamepad.buttonSouth.isPressed))
                return 1f;

            return 0f;
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
    }
}
