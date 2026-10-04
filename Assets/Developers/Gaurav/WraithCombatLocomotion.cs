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
    [Tooltip("Aim-Down-Sights tactical walk speed (m/s)")]
    [SerializeField] private float adsSpeed = 3.5f;
    [Tooltip("High-speed sprint speed (m/s)")]
    [SerializeField] private float sprintSpeed = 7.2f;
    [Tooltip("Time in seconds to ramp up to speed from a dead stop")]
    [Range(0.01f, 0.5f)]
    [SerializeField] private float accelerationTime = 0.12f;
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
    [Tooltip("Gravity applied while ascending (negative value)")]
    [SerializeField] private float ascentGravity = -28f;
    [Tooltip("Gravity applied while falling (negative value)")]
    [SerializeField] private float descentGravity = -45f;
    [Tooltip("Boost multiplier applied to horizontal speed while airborne for longer leap distance")]
    [Range(1.0f, 1.6f)]
    [SerializeField] private float airSpeedMultiplier = 1.25f;

    [Header("Animation Tuning")]
    [Tooltip("Playback speed multiplier when moving diagonally (prevents foot-sliding)")]
    [Range(1f, 2f)]
    [SerializeField] private float diagonalAnimSpeedBoost = 1.25f;
    [Tooltip("Playback speed multiplier for ADS walk clips to eliminate foot-sliding at higher move speeds")]
    [Range(1f, 2f)]
    [SerializeField] private float adsAnimSpeedMultiplier = 1.35f;

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

    // Public accessors
    public bool IsSprinting => isSprinting;
    public bool IsAiming => isAiming;

    private readonly int moveXHash = Animator.StringToHash("MoveX");
    private readonly int moveZHash = Animator.StringToHash("MoveZ");
    private readonly int isSprintingHash = Animator.StringToHash("IsSprinting");
    private readonly int isAimingHash = Animator.StringToHash("IsAiming");
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
        if (Mouse.current != null)
        {
            bool wasAiming = isAiming;
            isAiming = Mouse.current.rightButton.isPressed;

            if (isAiming != wasAiming && adsCameraObject != null)
            {
                adsCameraObject.SetActive(isAiming);
            }
        }

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

        if (controller.isGrounded)
        {
            if (verticalVelocity.y < 0)
            {
                verticalVelocity.y = -2f;
            }

            if (jumpRequested)
            {
                float targetJumpHeight = isSprinting ? sprintJumpHeight : combatJumpHeight;
                verticalVelocity.y = Mathf.Sqrt(targetJumpHeight * -2f * ascentGravity);
                animator.SetTrigger(jumpHash);
                jumpRequested = false;
            }
        }
        else
        {
            float activeGravity = (verticalVelocity.y >= 0f) ? ascentGravity : descentGravity;
            verticalVelocity.y += activeGravity * Time.deltaTime;
        }

        float baseTargetSpeed = 0f;
        if (inputVector.sqrMagnitude > 0.01f)
        {
            if (isSprinting) baseTargetSpeed = sprintSpeed;
            else if (isAiming) baseTargetSpeed = adsSpeed;
            else baseTargetSpeed = walkSpeed;
        }

        float targetSpeed = controller.isGrounded ? baseTargetSpeed : (baseTargetSpeed * airSpeedMultiplier);

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
        animator.SetBool(isAimingHash, isAiming);
        animator.SetBool(isGroundedHash, controller.isGrounded);

        // Diagonal factor (0 to 0.707)
        float diagonalFactor = Mathf.Min(Mathf.Abs(inputVector.x), Mathf.Abs(inputVector.y));
        float diagonalRatio = Mathf.InverseLerp(0f, 0.707f, diagonalFactor);

        // When in ADS, scale base cadence by adsAnimSpeedMultiplier to match the faster ground travel
        float baseCadence = isAiming ? adsAnimSpeedMultiplier : 1.0f;
        float currentAnimSpeed = Mathf.Lerp(baseCadence, baseCadence * diagonalAnimSpeedBoost, diagonalRatio);

        animator.SetFloat(animSpeedMultHash, currentAnimSpeed);
    }
}