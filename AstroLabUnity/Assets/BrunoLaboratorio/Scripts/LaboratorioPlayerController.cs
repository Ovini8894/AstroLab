using UnityEngine;
using UnityEngine.InputSystem;

namespace AstroLab.BrunoLaboratorio
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInput))]
    public sealed class LaboratorioPlayerController : MonoBehaviour
    {
        [Header("Movimento")]
        [SerializeField] private float walkSpeed = 3.6f;
        [SerializeField] private float runSpeed = 6.2f;
        [SerializeField] private float acceleration = 14f;
        [SerializeField] private float rotationSharpness = 14f;
        [SerializeField] private float gravity = -24f;
        [SerializeField] private float jumpHeight = 1.25f;

        [Header("Camera")]
        [SerializeField] private Transform cameraTarget;
        [SerializeField] private float mouseSensitivity = 0.12f;
        [SerializeField] private float gamepadLookSpeed = 145f;
        [SerializeField] private float minimumPitch = -35f;
        [SerializeField] private float maximumPitch = 68f;

        private CharacterController characterController;
        private PlayerInput playerInput;
        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction sprintAction;
        private InputAction jumpAction;
        private Vector3 horizontalVelocity;
        private float verticalVelocity;
        private float yaw;
        private float pitch = 12f;

        public float CurrentSpeed { get; private set; }
        public float MaximumSpeed => runSpeed;

        public void Configure(Transform target)
        {
            cameraTarget = target;
        }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            playerInput = GetComponent<PlayerInput>();
            yaw = transform.eulerAngles.y;
            ResolveActions();
        }

        private void OnEnable()
        {
            ResolveActions();
            moveAction?.Enable();
            lookAction?.Enable();
            sprintAction?.Enable();
            jumpAction?.Enable();
            LockCursor();
        }

        private void OnDisable()
        {
            moveAction?.Disable();
            lookAction?.Disable();
            sprintAction?.Disable();
            jumpAction?.Disable();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Update()
        {
            HandleCursor();
            UpdateLook();
            UpdateMovement();
        }

        private void ResolveActions()
        {
            if (playerInput == null || playerInput.actions == null)
                return;

            moveAction = playerInput.actions.FindAction("Player/Move", false);
            lookAction = playerInput.actions.FindAction("Player/Look", false);
            sprintAction = playerInput.actions.FindAction("Player/Sprint", false);
            jumpAction = playerInput.actions.FindAction("Player/Jump", false);
        }

        private void UpdateLook()
        {
            if (cameraTarget == null || lookAction == null || Cursor.lockState != CursorLockMode.Locked)
                return;

            Vector2 look = lookAction.ReadValue<Vector2>();
            bool usingMouse = lookAction.activeControl?.device is Mouse;
            float scale = usingMouse ? mouseSensitivity : gamepadLookSpeed * Time.deltaTime;

            yaw += look.x * scale;
            pitch = Mathf.Clamp(pitch - look.y * scale, minimumPitch, maximumPitch);
            cameraTarget.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        private void UpdateMovement()
        {
            Vector2 input = moveAction?.ReadValue<Vector2>() ?? Vector2.zero;
            input = Vector2.ClampMagnitude(input, 1f);

            Vector3 cameraForward = cameraTarget != null ? cameraTarget.forward : transform.forward;
            Vector3 cameraRight = cameraTarget != null ? cameraTarget.right : transform.right;
            cameraForward.y = 0f;
            cameraRight.y = 0f;
            cameraForward.Normalize();
            cameraRight.Normalize();

            Vector3 direction = cameraForward * input.y + cameraRight * input.x;
            if (direction.sqrMagnitude > 1f)
                direction.Normalize();

            bool sprinting = sprintAction?.IsPressed() == true && input.sqrMagnitude > 0.01f;
            float targetSpeed = sprinting ? runSpeed : walkSpeed;
            Vector3 targetVelocity = direction * targetSpeed;
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, acceleration * Time.deltaTime);
            CurrentSpeed = horizontalVelocity.magnitude;

            if (direction.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
                float blend = 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, blend);
            }

            float activeGravity = LunarGravityZone.TryGetGravity(transform.position, out float zoneGravity)
                ? zoneGravity
                : gravity;

            if (characterController.isGrounded)
            {
                if (verticalVelocity < 0f)
                    verticalVelocity = Mathf.Max(activeGravity * 0.12f, -2f);

                if (jumpAction?.WasPressedThisFrame() == true)
                    verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * activeGravity);
            }
            else
            {
                verticalVelocity += activeGravity * Time.deltaTime;
            }

            Vector3 motion = horizontalVelocity + Vector3.up * verticalVelocity;
            characterController.Move(motion * Time.deltaTime);
        }

        private static void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private static void HandleCursor()
        {
            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (Mouse.current?.leftButton.wasPressedThisFrame == true)
            {
                LockCursor();
            }
        }
    }
}
