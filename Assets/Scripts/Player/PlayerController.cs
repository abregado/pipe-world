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
        public bool IsGrounded => body != null && body.isGrounded;
        public bool MenuOpen => menuLevel > 0;
        public bool Flying { get; private set; }
        public float flySpeed = 9f;

        // While set, this runs every frame instead of the normal look and movement: scripted camera moves use it to own the view.
        public System.Action<float> frameDriver;
        public Vector3 EyePosition => cameraPivot != null ? cameraPivot.position : transform.position;
        public Quaternion EyeRotation => cameraPivot != null ? cameraPivot.rotation : transform.rotation;
        public float Pitch => pitch;
        public bool IsWalking => planarVelocity.sqrMagnitude > 0.25f;

        int menuLevel;

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
            SetCursorLocked(menuLevel == 0);
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
            if (frameDriver != null)
            {
                frameDriver(Time.deltaTime);
                return;
            }

            if (menuLevel == 0 && Cursor.lockState == CursorLockMode.Locked)
                UpdateLook();
            if (Flying)
            {
                UpdateFly();
                return;
            }
            UpdateCrouch();
            UpdateMovement();
        }

        // Free flight ignores collision and gravity; leaving it restores the character controller.
        public void SetFlying(bool flying)
        {
            if (Flying == flying)
                return;

            Flying = flying;
            planarVelocity = Vector3.zero;
            verticalVelocity = 0f;
            body.enabled = !flying;
            if (!flying)
                ApplyHeight(standingHeight);
        }

        void UpdateFly()
        {
            if (menuLevel > 0)
                return;

            var input = Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f);
            Transform view = cameraPivot != null ? cameraPivot : transform;
            var vertical = (jump.IsPressed() ? 1f : 0f) - (crouch.IsPressed() ? 1f : 0f);
            var speed = sprint.IsPressed() ? flySpeed * 2f : flySpeed;
            transform.position += (view.forward * input.y + view.right * input.x + Vector3.up * vertical) * (speed * Time.deltaTime);
        }

        // Puts the eyes at a world pose by moving the whole (flying) player, so look and flight carry on from there.
        public void SetEyePose(Vector3 eye, Quaternion rotation)
        {
            Vector3 euler = rotation.eulerAngles;
            yaw = euler.y;
            pitch = Mathf.Clamp(euler.x > 180f ? euler.x - 360f : euler.x, -maxPitch, maxPitch);
            ApplyLook();
            float eyeHeight = cameraPivot != null ? cameraPivot.localPosition.y : 0f;
            transform.position = eye - Vector3.up * eyeHeight;
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

        // Reference counted so a menu opened inside another menu does not relock the cursor when it closes first.
        public void SetMenuState(bool open)
        {
            menuLevel = Mathf.Max(0, menuLevel + (open ? 1 : -1));
            if (enabled)
                SetCursorLocked(menuLevel == 0);
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
            if (menuLevel == 0 && crouch.IsPressed())
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
            var input = menuLevel > 0 ? Vector2.zero : Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f);
            var speed = IsCrouching ? crouchSpeed : sprint.IsPressed() && menuLevel == 0 ? sprintSpeed : walkSpeed;
            var wish = (transform.right * input.x + transform.forward * input.y) * speed;

            bool grounded = body.isGrounded;
            if (grounded) lastGroundedTime = Time.time;

            float accel = grounded ? groundAcceleration : airAcceleration;
            planarVelocity = Vector3.MoveTowards(planarVelocity, wish, accel * Time.deltaTime);

            // CharacterController.isGrounded only holds after a downward Move, so keep a small downward bias while grounded.
            if (grounded && verticalVelocity < 0f)
                verticalVelocity = -2f;

            bool canJump = !IsCrouching && Time.time - lastGroundedTime <= coyoteTime;
            if (canJump && menuLevel == 0 && jump.WasPressedThisFrame())
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                lastGroundedTime = float.NegativeInfinity;
            }

            verticalVelocity += gravity * Time.deltaTime;
            body.Move((planarVelocity + Vector3.up * verticalVelocity) * Time.deltaTime);
        }
    }
}
