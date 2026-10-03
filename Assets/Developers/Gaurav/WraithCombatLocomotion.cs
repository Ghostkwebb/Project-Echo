using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController), typeof(Animator))]
public class WraithCombatLocomotion : MonoBehaviour
{
    [Header("Camera & Aiming")]
    [Tooltip("The CameraTarget child transform located at eye/chest level")]
    [SerializeField] private Transform cameraTarget;
    [Tooltip("Drag the CinemachineCamera_ADS GameObject here")]
    [SerializeField] private GameObject adsCameraObject;
    [Tooltip("Drag the CinemachineCamera_Sprint GameObject here")]
    [SerializeField] private GameObject sprintCameraObject;
    [SerializeField] private float mouseSensitivity = 1.2f;
    [Tooltip("Sensitivity scale when holding RMB to aim down sights")]
    [Range(0.1f, 1f)]
    [SerializeField] private float adsSensitivityMultiplier = 0.6f;
    [SerializeField] private float minPitch = -40f;
    [SerializeField] private float maxPitch = 65f;

    [Header("Movement & Speeds")]
    [Tooltip("Base combat strafe speed (m/s)")]
    [SerializeField] private float walkSpeed = 4.2f;
    [Tooltip("High-speed sprint speed (m/s)")]
    [SerializeField] private float sprintSpeed = 7.2f;
    [Tooltip("Time in seconds to smoothly ramp up to speed (prevents sudden velocity pops on landing)")]
    [Range(0.05f, 0.5f)]
    [SerializeField] private float accelerationTime = 0.2f;
    [Tooltip("How fast Wraith turns to face diagonal sprint directions")]
    [SerializeField] private float sprintTurnSpeed = 12f;
    [Tooltip("How fast Wraith snaps back to crosshair aim in combat")]
    [SerializeField] private float combatTurnSpeed = 20f;
    [Tooltip("Damping time for blend tree parameters to prevent animation popping")]
    [SerializeField] private float animDampTime = 0.15f;

    [Header("Jump & Heavy Gravity Physics")]
    [Tooltip("Apex height for normal combat jog jumps (meters)")]
    [SerializeField] private float combatJumpHeight = 1.25f;
    [Tooltip("Apex height for high-momentum sprint jumps (meters)")]
    [SerializeField] private float sprintJumpHeight = 1.6f;
    [Tooltip("Base gravity (negative value). Higher absolute values = faster, punchier jump arc")]
    [SerializeField] private float gravity = -30f;
    [Tooltip("Extra gravity applied when falling downward (1.5 = 50% heavier fall). Eliminates floatiness")]
    [Range(1f, 3f)]
    [SerializeField] private float fallMultiplier = 1.5f;

    [Header("Animation Tuning")]
    [Tooltip("Playback speed multiplier when moving diagonally (prevents foot-sliding)")]
    [Range(1f, 2f)]
    [SerializeField] private float diagonalAnimSpeedBoost = 1.25f;

    private CharacterController controller;
    private Animator animator;

    private Vector2 inputVector;
    private Vector2 lookInput;
    private float currentYaw = 0f;
    private float currentPitch = 8f;
    private float currentSpeed = 0f;
    private Vector3 verticalVelocity;
    private bool isAiming = false;
    private bool isSprinting = false;
    private bool jumpRequested = false;

    private readonly int moveXHash = Animator.StringToHash("MoveX");
    private readonly int moveZHash = Animator.StringToHash("MoveZ");
    private readonly int isSprintingHash = Animator.StringToHash("IsSprinting");
    private readonly int isGroundedHash = Animator.StringToHash("IsGrounded");
    private readonly int jumpHash = Animator.StringToHash("Jump");
    private readonly int animSpeedMultHash = Animator.StringToHash("AnimSpeedMultiplier");

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (adsCameraObject != null) adsCameraObject.SetActive(false);
        if (sprintCameraObject != null) sprintCameraObject.SetActive(false);

        currentYaw = transform.eulerAngles.y;

        if (cameraTarget != null)
        {
            // 180° Y-offset keeps camera forward aligned with Unreal's -Z mesh orientation
            cameraTarget.rotation = Quaternion.Euler(currentPitch, currentYaw + 180f, 0f);
        }
    }

    public void OnMove(InputValue value)
    {
        inputVector = Vector2.ClampMagnitude(value.Get<Vector2>(), 1f);
    }

    public void OnLook(InputValue value)
    {
        lookInput = value.Get<Vector2>();
    }

    public void OnJump(InputValue value)
    {
        if (value.isPressed && controller.isGrounded)
        {
            jumpRequested = true;
        }
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame && controller.isGrounded)
        {
            jumpRequested = true;
        }

        HandleAimingAndSprinting();
        HandleCameraAndRotation();
        HandleMovement();
        UpdateAnimator();
    }

    private void HandleAimingAndSprinting()
    {
        // 1. Aiming (RMB)
        if (Mouse.current != null)
        {
            bool wasAiming = isAiming;
            isAiming = Mouse.current.rightButton.isPressed;

            if (isAiming != wasAiming && adsCameraObject != null)
            {
                adsCameraObject.SetActive(isAiming);
            }
        }

        // 2. Sprinting (Left Shift) - forward only, cancelled by ADS
        bool shiftHeld = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
        bool wasSprinting = isSprinting;
        isSprinting = shiftHeld && inputVector.y > 0.1f && !isAiming;

        if (isSprinting != wasSprinting && sprintCameraObject != null)
        {
            sprintCameraObject.SetActive(isSprinting);
        }
    }

    private void HandleCameraAndRotation()
    {
        float sensitivity = isAiming ? (mouseSensitivity * adsSensitivityMultiplier) : mouseSensitivity;

        float mouseX = lookInput.x * sensitivity * 0.1f;
        float mouseY = lookInput.y * sensitivity * 0.1f;

        currentYaw += mouseX;
        currentPitch = Mathf.Clamp(currentPitch - mouseY, minPitch, maxPitch);

        if (cameraTarget != null)
        {
            cameraTarget.rotation = Quaternion.Euler(currentPitch, currentYaw + 180f, 0f);
        }
    }

    private void HandleMovement()
    {
        if (cameraTarget == null) return;

        Vector3 aimForward = cameraTarget.forward;
        Vector3 aimRight = cameraTarget.right;
        aimForward.y = 0f;
        aimRight.y = 0f;
        aimForward.Normalize();
        aimRight.Normalize();

        Vector3 moveDirection = (aimForward * inputVector.y) + (aimRight * inputVector.x);

        if (moveDirection.sqrMagnitude > 1f)
        {
            moveDirection.Normalize();
        }

        // Body Rotation
        if (isSprinting && moveDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetSprintRot = Quaternion.LookRotation(moveDirection) * Quaternion.Euler(0f, 180f, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetSprintRot, sprintTurnSpeed * Time.deltaTime);
        }
        else
        {
            Quaternion targetCombatRot = Quaternion.Euler(0f, currentYaw, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetCombatRot, combatTurnSpeed * Time.deltaTime);
        }

        // Grounding & Heavy Jump Physics
        if (controller.isGrounded)
        {
            if (verticalVelocity.y < 0)
            {
                verticalVelocity.y = -2f;
            }

            if (jumpRequested)
            {
                float targetJumpHeight = isSprinting ? sprintJumpHeight : combatJumpHeight;
                verticalVelocity.y = Mathf.Sqrt(targetJumpHeight * -2f * gravity);
                animator.SetTrigger(jumpHash);
                jumpRequested = false;
            }
        }
        else
        {
            float activeGravity = (verticalVelocity.y < 0f) ? (gravity * fallMultiplier) : gravity;
            verticalVelocity.y += activeGravity * Time.deltaTime;
        }

        // --- SMOOTH SPEED ACCELERATION ---
        // Determines target speed based on input; smoothly ramps velocity to prevent abrupt bursts
        float targetSpeed = (inputVector.sqrMagnitude > 0.01f) ? (isSprinting ? sprintSpeed : walkSpeed) : 0f;
        float accelRate = (sprintSpeed / Mathf.Max(0.01f, accelerationTime));
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, accelRate * Time.deltaTime);

        Vector3 finalMotion = (moveDirection * currentSpeed + verticalVelocity) * Time.deltaTime;
        controller.Move(finalMotion);
    }

    private void UpdateAnimator()
    {
        animator.SetFloat(moveXHash, inputVector.x, animDampTime, Time.deltaTime);
        animator.SetFloat(moveZHash, inputVector.y, animDampTime, Time.deltaTime);
        animator.SetBool(isSprintingHash, isSprinting);
        animator.SetBool(isGroundedHash, controller.isGrounded);

        float diagonalFactor = Mathf.Min(Mathf.Abs(inputVector.x), Mathf.Abs(inputVector.y));
        float diagonalRatio = Mathf.InverseLerp(0f, 0.707f, diagonalFactor);
        float currentAnimSpeed = Mathf.Lerp(1.0f, diagonalAnimSpeedBoost, diagonalRatio);

        animator.SetFloat(animSpeedMultHash, currentAnimSpeed);
    }
}