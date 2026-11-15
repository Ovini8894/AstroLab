using UnityEngine;
using UnityEngine.InputSystem;

// Controle simples em primeira pessoa para testar a escala da base (jogador de 1,80 m).
// WASD anda, mouse olha, Shift corre, Espaço pula. Gravidade da Lua: 1,62 m/s².
[RequireComponent(typeof(CharacterController))]
public class JogadorFPS : MonoBehaviour
{
    public float velocidade = 3.0f;
    public float velocidadeCorrida = 5.5f;
    public float sensibilidadeMouse = 0.12f;
    public float gravidade = 1.62f;
    public float alturaPulo = 1.2f;

    CharacterController cc;
    Transform cam;
    float pitch;
    float velY;

    void Start()
    {
        cc = GetComponent<CharacterController>();
        cam = GetComponentInChildren<Camera>().transform;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null || mouse == null) return;

        if (kb.escapeKey.wasPressedThisFrame) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        if (mouse.leftButton.wasPressedThisFrame) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }

        if (Cursor.lockState == CursorLockMode.Locked)
        {
            Vector2 d = mouse.delta.ReadValue() * sensibilidadeMouse;
            transform.Rotate(0f, d.x, 0f);
            pitch = Mathf.Clamp(pitch - d.y, -85f, 85f);
            cam.localEulerAngles = new Vector3(pitch, 0f, 0f);
        }

        Vector2 input = Vector2.zero;
        if (kb.wKey.isPressed) input.y += 1;
        if (kb.sKey.isPressed) input.y -= 1;
        if (kb.dKey.isPressed) input.x += 1;
        if (kb.aKey.isPressed) input.x -= 1;
        float vel = kb.leftShiftKey.isPressed ? velocidadeCorrida : velocidade;
        Vector3 move = (transform.forward * input.y + transform.right * input.x).normalized * vel;

        if (cc.isGrounded)
        {
            velY = -0.5f;
            if (kb.spaceKey.wasPressedThisFrame) velY = Mathf.Sqrt(2f * gravidade * alturaPulo);
        }
        velY -= gravidade * Time.deltaTime;
        move.y = velY;
        cc.Move(move * Time.deltaTime);
    }
}
