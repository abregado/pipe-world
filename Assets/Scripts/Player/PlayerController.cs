using UnityEngine;
using UnityEngine.InputSystem;

namespace PipeWorld.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public Transform cameraPivot;

        [Header("Movement")]
        public float walkSpeed = 5f;
        public float sprintSpeed = 9f;
        public float crouchSpeed = 2.5f;
        public float groundAcceleration = 40f;
        public float airAcceleration = 10f;
        public float jumpHeight = 1.4f;
        public float gravity = -25f;
        public float coyoteTime = 0.1f;

        [Header("Crouch")]
        public float standingHeight = 1.8f;
        public float crouchHeight = 1.0f;
        public float heightChangeSpeed = 6f;
        public float eyeOffsetFromTop = 0.2f;

        [Header("Look")]
        public float mouseDegreesPerPixel = 0.1f;
        public float stickDegreesPerSecond = 180f;
        public float maxPitch = 89f;

        public bool IsCrouching { get; private set; }

        readonly Collider[] headroomHits = new Collider[8];

        CharacterController body;
        InputAction move, lookMouse, lookStick, jump, sprint, crouch;
        Vector3 planarVelocity;
        float verticalVelocity;
        float lastGroundedTime;
        float yaw;
        float pitch;

        void Awake()
        {
            body = GetComponent<CharacterController>();

            move = new InputAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddBinding("<Gamepad>/leftStick");

            lookMouse = new InputAction("LookMouse", InputActionType.Value, "<Mouse>/delta");
            lookStick = new InputAction("LookStick", InputActionType.Value, "<Gamepad>/rightStick");

            jump = new InputAction("Jump", InputActionType.Button);
            jump.AddBinding("<Keyboard>/space");
            jump.AddBinding("<Gamepad>/buttonSouth");

            sprint = new InputAction("Sprint", InputActionType.Button);
            sprint.AddBinding("<Keyboard>/leftShift");
            sprint.AddBinding("<Gamepad>/leftStickPress");

            crouch = new InputAction("Crouch", InputActionType.Button);
            crouch.AddBinding("<Keyboard>/leftCtrl");
            crouch.AddBinding("<Keyboard>/c");
            crouch.AddBinding("<Gamepad>/buttonEast");

            yaw = transform.eulerAngles.y;
            ApplyHeight(standingHeight);
        }

        void OnEnable()
        {
            move.Enable();
            lookMouse.Enable();
            lookStick.Enable();
            jump.Enable();
            sprint.Enable();
            crouch.Enable();
            SetCursorLocked(true);
        }

        void OnDisable()
        {
            move.Disable();
            lookMouse.Disable();
            lookStick.Disable();
            jump.Disable();
            sprint.Disable();
            crouch.Disable();
            SetCursorLocked(false);
        }

        void Update()
        {
            UpdateCursorLock();
            if (Cursor.lockState == CursorLockMode.Locked)
                UpdateLook();
            UpdateCrouch();
            UpdateMovement();
        }

        public void Teleport(Vector3 position, float newYaw, float newPitch)
        {
            body.enabled = false;
            transform.position = position;
            body.enabled = true;
            planarVelocity = Vector3.zero;
            verticalVelocity = 0f;
            IsCrouching = false;
            ApplyHeight(standingHeight);
            yaw = newYaw;
            pitch = Mathf.Clamp(newPitch, -maxPitch, maxPitch);
            ApplyLook();
        }

        void UpdateCursorLock()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                SetCursorLocked(false);
            else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                SetCursorLocked(true);
        }

        static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void UpdateLook()
        {
            var mouse = lookMouse.ReadValue<Vector2>() * mouseDegreesPerPixel;
            var stick = lookStick.ReadValue<Vector2>() * (stickDegreesPerSecond * Time.deltaTime);
            var delta = mouse + stick;
            yaw += delta.x;
            pitch = Mathf.Clamp(pitch - delta.y, -maxPitch, maxPitch);
            ApplyLook();
        }

        void ApplyLook()
        {
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (cameraPivot != null)
                cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        void UpdateCrouch()
        {
            if (crouch.IsPressed())
                IsCrouching = true;
            else if (IsCrouching && HasStandingHeadroom())
                IsCrouching = false;

            var target = IsCrouching ? crouchHeight : standingHeight;
            ApplyHeight(Mathf.MoveTowards(body.height, target, heightChangeSpeed * Time.deltaTime));
        }

        void ApplyHeight(float height)
        {
            body.height = height;
            body.center = new Vector3(0f, height * 0.5f, 0f);
            if (cameraPivot != null)
                cameraPivot.localPosition = new Vector3(0f, height - eyeOffsetFromTop, 0f);
        }

        // Only the space above the crouched capsule is tested, so the ground under the feet never counts as an obstruction.
        bool HasStandingHeadroom()
        {
            var radius = body.radius - 0.05f;
            var bottom = transform.position + Vector3.up * crouchHeight;
            var top = transform.position + Vector3.up * (standingHeight - radius);
            var count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, headroomHits, ~0, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++)
                if (headroomHits[i] != body)
                    return false;
            return true;
        }

        void UpdateMovement()
        {
            var input = Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f);
            var speed = IsCrouching ? crouchSpeed : sprint.IsPressed() ? sprintSpeed : walkSpeed;
            var wish = (transform.right * input.x + transform.forward * input.y) * speed;

            bool grounded = body.isGrounded;
            if (grounded) lastGroundedTime = Time.time;

            float accel = grounded ? groundAcceleration : airAcceleration;
            planarVelocity = Vector3.MoveTowards(planarVelocity, wish, accel * Time.deltaTime);

            // CharacterController.isGrounded only holds after a downward Move, so keep a small downward bias while grounded.
            if (grounded && verticalVelocity < 0f)
                verticalVelocity = -2f;

            bool canJump = !IsCrouching && Time.time - lastGroundedTime <= coyoteTime;
            if (canJump && jump.WasPressedThisFrame())
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                lastGroundedTime = float.NegativeInfinity;
            }

            verticalVelocity += gravity * Time.deltaTime;
            body.Move((planarVelocity + Vector3.up * verticalVelocity) * Time.deltaTime);
        }
    }
}
