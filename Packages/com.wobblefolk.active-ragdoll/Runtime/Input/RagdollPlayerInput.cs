using UnityEngine;
using UnityEngine.InputSystem;

namespace Wobblefolk.Ragdoll
{
    /// <summary>
    /// Input System driver for a player-controlled ragdoll. Reads move / look / jump / sprint, converts movement to
    /// camera-relative world directions and feeds the <see cref="LocomotionController"/> and
    /// <see cref="RagdollFollowCamera"/>. Actions are defined inline with keyboard-mouse and gamepad defaults and can
    /// be rebound or replaced with references to an Input Actions asset in the Inspector.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public sealed class RagdollPlayerInput : MonoBehaviour
    {
        [SerializeField] LocomotionController locomotion;
        [SerializeField] RagdollFollowCamera followCamera;

        [Header("Actions")]
        [SerializeField] InputActionProperty move = new InputActionProperty(CreateMoveAction());
        [SerializeField] InputActionProperty look = new InputActionProperty(CreateLookAction());
        [SerializeField] InputActionProperty jump = new InputActionProperty(CreateButton("Jump", "<Keyboard>/space", "<Gamepad>/buttonSouth"));
        [SerializeField] InputActionProperty sprint = new InputActionProperty(CreateButton("Sprint", "<Keyboard>/leftShift", "<Gamepad>/leftStickPress"));

        [Header("Look")]
        [Tooltip("Gamepad stick look is scaled by this and by delta time to match mouse sensitivity.")]
        [Min(0f)] public float gamepadLookScale = 300f;
        public bool lockCursor = true;

        public RagdollFollowCamera FollowCamera
        {
            get => followCamera;
            set => followCamera = value;
        }

        internal void Configure(LocomotionController locomotionController, RagdollFollowCamera camera)
        {
            locomotion = locomotionController;
            followCamera = camera;
        }

        void Reset()
        {
            locomotion = GetComponent<LocomotionController>();
            followCamera = FindAnyObjectByType<RagdollFollowCamera>();
        }

        void Awake()
        {
            if (locomotion == null) locomotion = GetComponent<LocomotionController>();
        }

        void OnEnable()
        {
            move.action?.Enable();
            look.action?.Enable();
            jump.action?.Enable();
            sprint.action?.Enable();
            if (lockCursor)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        void OnDisable()
        {
            move.action?.Disable();
            look.action?.Disable();
            jump.action?.Disable();
            sprint.action?.Disable();
            if (lockCursor)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        void Update()
        {
            if (followCamera != null && look.action != null)
            {
                Vector2 delta = look.action.ReadValue<Vector2>();
                bool fromGamepad = look.action.activeControl?.device is Gamepad;
                followCamera.AddLookInput(fromGamepad ? delta * (gamepadLookScale * Time.deltaTime) : delta);
            }

            if (locomotion == null)
            {
                return;
            }

            Vector2 input = move.action != null ? Vector2.ClampMagnitude(move.action.ReadValue<Vector2>(), 1f) : Vector2.zero;
            locomotion.MoveInput = followCamera != null
                ? followCamera.ToWorldDirection(input)
                : new Vector3(input.x, 0f, input.y);
            locomotion.Sprint = sprint.action != null && sprint.action.IsPressed();

            if (jump.action != null && jump.action.WasPressedThisFrame())
            {
                locomotion.RequestJump();
            }
        }

        static InputAction CreateMoveAction()
        {
            var action = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            action.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            action.AddBinding("<Gamepad>/leftStick");
            return action;
        }

        static InputAction CreateLookAction()
        {
            var action = new InputAction("Look", InputActionType.Value, expectedControlType: "Vector2");
            action.AddBinding("<Mouse>/delta");
            action.AddBinding("<Gamepad>/rightStick");
            return action;
        }

        static InputAction CreateButton(string name, string keyboard, string gamepad)
        {
            var action = new InputAction(name, InputActionType.Button);
            action.AddBinding(keyboard);
            action.AddBinding(gamepad);
            return action;
        }
    }
}
