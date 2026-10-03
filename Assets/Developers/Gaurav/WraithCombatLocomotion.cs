using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController), typeof(Animator))]
public class WraithCombatLocomotion : MonoBehaviour
{
    [Header("Camera & Aiming")]
    [SerializeField] private Transform cameraTarget;
    [Tooltip("Drag CinemachineCamera_ADS GameObject here")]
    [SerializeField] private GameObject adsCameraObject;
    [Tooltip("Drag CinemachineCamera_Sprint GameObject here")]
    [SerializeField] private GameObject sprintCameraObject;
    [SerializeField] private float mouseSensitivity = 1.2f;
    [SerializeField] private float adsSensitivityMultiplier = 0.6f;
    [SerializeField] private float minPitch = -40f;
    [SerializeField] private float maxPitch = 65f;

    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 4.2f;
    [SerializeField] private float sprintSpeed = 7.2f;
    [SerializeField] private float sprintTurnSpeed = 12f;
    [SerializeField] private float combatTurnSpeed = 20f;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float animDampTime = 0.15f;

    [Header("Diagonal Animation Tuning")]
    [SerializeField] private float diagonalAnimSpeedBoost = 1.25f;

    private CharacterController controller;
    private Animator animator;

    private Vector2 inputVector;
    private Vector2 lookInput;
    private float currentYaw = 0f;
    private float currentPitch = 8f;
    private Vector3 verticalVelocity;
    private bool isAiming = false;
    private bool isSprinting = false;

    private readonly int moveXHash = Animator.StringToHash("MoveX");
    private readonly int moveZHash = Animator.StringToHash("MoveZ");
    private readonly int isSprintingHash = Animator.StringToHash("IsSprinting");
    private readonly int animSpeedMultHash = Animator.StringToHash("AnimSpeedMultiplier");

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (adsCameraObject != null) adsCameraObject.SetActive(false);
        if (sprintCameraObject != null) sprintCameraObject.SetActive(false);

        // Initialize camera yaw to match starting character orientation
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

    void Update()
    {
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

        // 2. Sprinting (Left Shift)
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

        // Update camera look angles
        currentYaw += mouseX;
        currentPitch = Mathf.Clamp(currentPitch - mouseY, minPitch, maxPitch);

        // Keep CameraTarget oriented in world space (camera stays completely smooth and steady)
        if (cameraTarget != null)
        {
            cameraTarget.rotation = Quaternion.Euler(currentPitch, currentYaw + 180f, 0f);
        }
    }

    private void HandleMovement()
    {
        if (cameraTarget == null) return;

        // Calculate planar camera direction
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

        // --- BODY ROTATION ---
        if (isSprinting && moveDirection.sqrMagnitude > 0.01f)
        {
            // SPRINTING: Rotate body to face the actual diagonal direction of travel
            Quaternion targetSprintRot = Quaternion.LookRotation(moveDirection) * Quaternion.Euler(0f, 180f, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetSprintRot, sprintTurnSpeed * Time.deltaTime);
        }
        else
        {
            // COMBAT STRAFE: Body stays locked facing camera forward (for strafing)
            Quaternion targetCombatRot = Quaternion.Euler(0f, currentYaw, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetCombatRot, combatTurnSpeed * Time.deltaTime);
        }

        // Grounding & Gravity
        if (controller.isGrounded && verticalVelocity.y < 0)
        {
            verticalVelocity.y = -2f;
        }
        else
        {
            verticalVelocity.y += gravity * Time.deltaTime;
        }

        float currentSpeed = isSprinting ? sprintSpeed : walkSpeed;
        Vector3 finalMotion = (moveDirection * currentSpeed + verticalVelocity) * Time.deltaTime;
        controller.Move(finalMotion);
    }

    private void UpdateAnimator()
    {
        animator.SetFloat(moveXHash, inputVector.x, animDampTime, Time.deltaTime);
        animator.SetFloat(moveZHash, inputVector.y, animDampTime, Time.deltaTime);
        animator.SetBool(isSprintingHash, isSprinting);

        float diagonalFactor = Mathf.Min(Mathf.Abs(inputVector.x), Mathf.Abs(inputVector.y));
        float diagonalRatio = Mathf.InverseLerp(0f, 0.707f, diagonalFactor);
        float currentAnimSpeed = Mathf.Lerp(1.0f, diagonalAnimSpeedBoost, diagonalRatio);

        animator.SetFloat(animSpeedMultHash, currentAnimSpeed);
    }
}