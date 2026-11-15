using UnityEngine;
using UnityEngine.InputSystem;

namespace AstroLab.Nicholas
{
    /// <summary>Controle FPS substituto, usado SOMENTE se a cena não tiver o controle do colega.</summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class NicholasFirstPersonController : MonoBehaviour
    {
        [SerializeField] private Transform view;
        [SerializeField] private float speed = 3.2f;
        [SerializeField] private float runSpeed = 5.5f;
        [SerializeField] private float mouseSensitivity = .12f;
        [SerializeField] private float gravity = 1.62f;
        [SerializeField] private float jumpHeight = 1.0f;
        private CharacterController controller;
        private float pitch;
        private float verticalSpeed;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (!view) view = GetComponentInChildren<Camera>()?.transform;
        }
        private void OnEnable()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard == null || mouse == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame)
            { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            if (mouse.leftButton.wasPressedThisFrame && Cursor.lockState == CursorLockMode.None)
            { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            if (Cursor.lockState == CursorLockMode.Locked && view)
            {
                Vector2 look = mouse.delta.ReadValue() * mouseSensitivity;
                transform.Rotate(0, look.x, 0);
                pitch = Mathf.Clamp(pitch - look.y, -85, 85);
                view.localEulerAngles = new Vector3(pitch, 0, 0);
            }
            Vector2 move = Vector2.zero;
            if (keyboard.wKey.isPressed) move.y += 1;
            if (keyboard.sKey.isPressed) move.y -= 1;
            if (keyboard.aKey.isPressed) move.x -= 1;
            if (keyboard.dKey.isPressed) move.x += 1;
            float velocity = keyboard.leftShiftKey.isPressed ? runSpeed : speed;
            Vector3 displacement = (transform.forward * move.y + transform.right * move.x).normalized * velocity;
            if (controller.isGrounded)
            {
                verticalSpeed = -0.5f;
                if (keyboard.spaceKey.wasPressedThisFrame) verticalSpeed = Mathf.Sqrt(2 * gravity * jumpHeight);
            }
            verticalSpeed -= gravity * Time.deltaTime;
            displacement.y = verticalSpeed;
            controller.Move(displacement * Time.deltaTime);
        }
    }
}
