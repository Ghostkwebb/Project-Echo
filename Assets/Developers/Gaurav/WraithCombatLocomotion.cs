using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController), typeof(Animator))]
public class WraithCombatLocomotion : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 4.2f;
    [SerializeField] private float aimTurnSpeed = 15f;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float animDampTime = 0.15f;

    [Header("Diagonal Animation Tuning")]
    [Tooltip("How much faster the animation plays when moving diagonally (1.2 = 20% faster)")]
    [SerializeField] private float diagonalAnimSpeedBoost = 1.25f;

    private CharacterController controller;
    private Animator animator;
    private Transform mainCamera;

    private Vector2 inputVector;
    private Vector3 verticalVelocity;

    private readonly int moveXHash = Animator.StringToHash("MoveX");
    private readonly int moveZHash = Animator.StringToHash("MoveZ");
    private readonly int animSpeedMultHash = Animator.StringToHash("AnimSpeedMultiplier");

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();

        if (Camera.main != null)
            mainCamera = Camera.main.transform;
    }

    public void OnMove(InputValue value)
    {
        // Clamp so diagonal physical speed remains identical to cardinal speed
        inputVector = Vector2.ClampMagnitude(value.Get<Vector2>(), 1f);
    }

    void Update()
    {
        HandleRotation();
        HandleMovement();
        UpdateAnimator();
    }

    private void HandleRotation()
    {
        if (mainCamera == null) return;

        Vector3 cameraForward = mainCamera.forward;
        cameraForward.y = 0f;

        if (cameraForward.sqrMagnitude > 0.001f)
        {
            cameraForward.Normalize();
            Quaternion targetRotation = Quaternion.LookRotation(cameraForward) * Quaternion.Euler(0f, 180f, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, aimTurnSpeed * Time.deltaTime);
        }
    }

    private void HandleMovement()
    {
        if (mainCamera == null) return;

        Vector3 camForward = mainCamera.forward;
        Vector3 camRight = mainCamera.right;
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        Vector3 moveDirection = (camForward * inputVector.y) + (camRight * inputVector.x);

        if (controller.isGrounded && verticalVelocity.y < 0)
        {
            verticalVelocity.y = -2f;
        }
        else
        {
            verticalVelocity.y += gravity * Time.deltaTime;
        }

        Vector3 finalMotion = (moveDirection * moveSpeed + verticalVelocity) * Time.deltaTime;
        controller.Move(finalMotion);
    }

    private void UpdateAnimator()
    {
        animator.SetFloat(moveXHash, inputVector.x, animDampTime, Time.deltaTime);
        animator.SetFloat(moveZHash, inputVector.y, animDampTime, Time.deltaTime);

        // --- DYNAMIC DIAGONAL ANIMATION SPEED ---
        // On pure cardinal axes (W, S, A, D), min(|X|, |Z|) is 0 -> 1.0x speed
        // On pure 45-degree diagonals, min(|X|, |Z|) reaches 0.707 -> boosts to diagonalAnimSpeedBoost
        float diagonalFactor = Mathf.Min(Mathf.Abs(inputVector.x), Mathf.Abs(inputVector.y));
        float diagonalRatio = Mathf.InverseLerp(0f, 0.707f, diagonalFactor);
        float currentAnimSpeed = Mathf.Lerp(1.0f, diagonalAnimSpeedBoost, diagonalRatio);

        animator.SetFloat(animSpeedMultHash, currentAnimSpeed);
    }
}