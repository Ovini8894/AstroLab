using UnityEngine;
using UnityEngine.InputSystem;

// Controle em primeira pessoa para a Base Lunar (jogador de 1,80 m).
// WASD anda, mouse olha, Shift corre (apenas no solo), Espaço pula. Gravidade da Lua: 1,62 m/s².
[RequireComponent(typeof(CharacterController))]
public class JogadorFPS : MonoBehaviour
{
    [Header("Movimento no Solo")]
    public float velocidade = 3.0f;
    public float velocidadeCorrida = 5.5f;
    public float aceleracaoSolo = 10.0f;
    public float desaceleracaoSolo = 8.0f;

    [Header("Câmera")]
    public float sensibilidadeMouse = 0.12f;

    [Header("Pulo e Gravidade Lunar")]
    public float gravidade = 1.62f;
    public float alturaPulo = 1.2f;
    public float multiplicadorAlturaPulo = 2.35f;
    public float velocidadeMaximaQueda = 3.4f;
    public float coyoteTime = 0.15f;
    public float jumpBufferTime = 0.12f;

    [Header("Controle Aéreo (Sem Corrida no Ar)")]
    public float controleAereo = 2.8f;
    public float velocidadeMinimaAjusteAereo = 1.6f;

    const float AderenciaSolo = -0.5f;
    const float LimiarInput = 0.001f;

    CharacterController cc;
    Transform cam;
    float pitch;
    float velY;
    Vector3 velHorizontal;
    bool estavaNoChao;
    float velocidadeTetoAereo;
    float tempoCoyoteRestante;
    float tempoBufferPuloRestante;

    void Start()
    {
        cc = GetComponent<CharacterController>();
        Camera childCam = GetComponentInChildren<Camera>();
        cam = childCam != null ? childCam.transform : null;
        velocidadeTetoAereo = velocidadeMinimaAjusteAereo;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void OnDisable()
    {
        velHorizontal = Vector3.zero;
        velY = 0f;
        estavaNoChao = false;
        tempoCoyoteRestante = 0f;
        tempoBufferPuloRestante = 0f;
        velocidadeTetoAereo = velocidadeMinimaAjusteAereo;
    }

    void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null || mouse == null) return;

        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        if (dt <= 0f) return;

        AtualizarCursorECamera(kb, mouse);

        bool noChao = cc.isGrounded;
        AtualizarTemporizadoresPulo(kb, noChao, dt);

        if (noChao && velY <= 0f) velY = AderenciaSolo;

        Vector3 direcaoDesejada = LerDirecaoMovimento(kb);
        bool correndoNoSolo = noChao && kb.leftShiftKey.isPressed && direcaoDesejada.sqrMagnitude > LimiarInput;
        AtualizarMovimentoHorizontal(direcaoDesejada, noChao, correndoNoSolo, dt);

        // Ao perder o chão caminhando para fora de uma borda, zera a aderência vertical e trava o teto de velocidade aérea.
        if (!noChao && estavaNoChao && velY <= 0f)
        {
            velY = 0f;
            RegistrarTetoAereo();
        }

        if (tempoBufferPuloRestante > 0f && tempoCoyoteRestante > 0f)
        {
            ExecutarPulo();
            noChao = false;
        }

        velY = Mathf.Max(velY - gravidade * dt, -velocidadeMaximaQueda);

        Vector3 movimento = velHorizontal;
        movimento.y = velY;
        CollisionFlags colisoes = cc.Move(movimento * dt);

        if ((colisoes & CollisionFlags.Above) != 0 && velY > 0f) velY = 0f;

        estavaNoChao = noChao;
    }

    void AtualizarCursorECamera(Keyboard kb, Mouse mouse)
    {
        if (kb.escapeKey.wasPressedThisFrame) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        if (mouse.leftButton.wasPressedThisFrame) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }

        if (Cursor.lockState != CursorLockMode.Locked || cam == null) return;

        Vector2 d = mouse.delta.ReadValue() * sensibilidadeMouse;
        transform.Rotate(0f, d.x, 0f);
        pitch = Mathf.Clamp(pitch - d.y, -85f, 85f);
        cam.localEulerAngles = new Vector3(pitch, 0f, 0f);
    }

    void AtualizarTemporizadoresPulo(Keyboard kb, bool noChao, float dt)
    {
        if (noChao && velY <= 0f) tempoCoyoteRestante = coyoteTime;
        else tempoCoyoteRestante = Mathf.Max(0f, tempoCoyoteRestante - dt);

        if (kb.spaceKey.wasPressedThisFrame) tempoBufferPuloRestante = jumpBufferTime;
        else tempoBufferPuloRestante = Mathf.Max(0f, tempoBufferPuloRestante - dt);
    }

    Vector3 LerDirecaoMovimento(Keyboard kb)
    {
        Vector2 input = Vector2.zero;
        if (kb.wKey.isPressed) input.y += 1f;
        if (kb.sKey.isPressed) input.y -= 1f;
        if (kb.dKey.isPressed) input.x += 1f;
        if (kb.aKey.isPressed) input.x -= 1f;

        Vector3 direcao = transform.forward * input.y + transform.right * input.x;
        return direcao.sqrMagnitude > LimiarInput ? direcao.normalized : Vector3.zero;
    }

    void AtualizarMovimentoHorizontal(Vector3 direcaoDesejada, bool noChao, bool correndoNoSolo, float dt)
    {
        bool temInput = direcaoDesejada.sqrMagnitude > LimiarInput;

        if (noChao)
        {
            float velAlvo = correndoNoSolo ? velocidadeCorrida : velocidade;
            Vector3 destino = temInput ? direcaoDesejada * velAlvo : Vector3.zero;
            float taxa = temInput ? aceleracaoSolo : desaceleracaoSolo;
            float fator = 1f - Mathf.Exp(-taxa * dt);
            velHorizontal = Vector3.Lerp(velHorizontal, destino, fator);
            if (!temInput && velHorizontal.sqrMagnitude < 0.0001f) velHorizontal = Vector3.zero;
            return;
        }

        // Sem entrada no ar, conserva o momento horizontal da decolagem.
        if (!temInput) return;

        // No ar, ignora Shift e permite só ajuste direcional suave dentro do teto da decolagem.
        Vector3 alvoAereo = direcaoDesejada * velocidadeTetoAereo;
        float fatorAereo = 1f - Mathf.Exp(-controleAereo * dt);
        velHorizontal = Vector3.Lerp(velHorizontal, alvoAereo, fatorAereo);
        velHorizontal = Vector3.ClampMagnitude(velHorizontal, velocidadeTetoAereo);
    }

    void RegistrarTetoAereo()
    {
        float momentoAtual = new Vector3(velHorizontal.x, 0f, velHorizontal.z).magnitude;
        velocidadeTetoAereo = Mathf.Clamp(momentoAtual, velocidadeMinimaAjusteAereo, velocidadeCorrida);
    }

    void ExecutarPulo()
    {
        RegistrarTetoAereo();
        float alturaEfetiva = Mathf.Max(0.1f, alturaPulo * multiplicadorAlturaPulo);
        velY = Mathf.Sqrt(2f * gravidade * alturaEfetiva);
        tempoCoyoteRestante = 0f;
        tempoBufferPuloRestante = 0f;
    }
}
