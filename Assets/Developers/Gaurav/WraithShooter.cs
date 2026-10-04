using UnityEngine;
using UnityEngine.InputSystem;

public class WraithShooter : MonoBehaviour
{
    [Header("Component References")]
    [Tooltip("Auto-detected or drag Wraith_ODGreen_Ready here")]
    [SerializeField] private WraithCombatLocomotion locomotion;

    [Header("Muzzle & Aim Bone")]
    [Tooltip("Transform representing the gun barrel tip")]
    [SerializeField] private Transform muzzlePoint;
    [Tooltip("Drag spine_02 or spine_03 here")]
    [SerializeField] private Transform aimBone;

    [Header("Weapon & Bullet Config")]
    [Tooltip("Drag your WraithBullet prefab here")]
    [SerializeField] private GameObject bulletPrefab;
    [Tooltip("Shots fired per second")]
    [Range(0.5f, 20f)]
    [SerializeField] private float fireRate = 5f;
    [Tooltip("Toggle between single-shot (semi-auto) and holding to spray (full-auto)")]
    [SerializeField] private bool isAutomatic = false;
    [Tooltip("Bullet flight speed in m/s")]
    [SerializeField] private float bulletSpeed = 90f;
    [Tooltip("Damage dealt per shot")]
    [SerializeField] private float bulletDamage = 25f;

    [Header("Aim Settings")]
    [Tooltip("Maximum range for the aim raycast")]
    [SerializeField] private float maxAimDistance = 150f;
    [Tooltip("Layers the bullet and aim ray can hit (exclude Player layer)")]
    [SerializeField] private LayerMask aimLayerMask = ~0;
    [Tooltip("Smoothing speed for upper body aiming")]
    [SerializeField] private float aimTurnSpeed = 20f;

    [Header("Aim Pitch Limits")]
    [SerializeField] private float minPitch = -35f;
    [SerializeField] private float maxPitch = 50f;


    [SerializeField] private AudioClip gunshotSound;

    [Header("Debug")]
    [SerializeField] private bool showDebugRays = true;

    private Camera mainCamera;
    private Animator animator;
    private Vector3 currentTargetPoint;
    private float smoothedPitch = 0f;
    private float smoothedYaw = 0f;
    private float nextFireTime = 0f;
    private bool fireInputHeld = false;
    private float fireClipDuration = 0.5f;

    private readonly int fireHash = Animator.StringToHash("Fire");
    private readonly int fireAnimSpeedHash = Animator.StringToHash("FireAnimSpeed");

    public Vector3 CurrentTargetPoint => currentTargetPoint;
    public Transform MuzzlePoint => muzzlePoint;

    void Awake()
    {
        mainCamera = Camera.main;

        if (locomotion == null)
        {
            locomotion = GetComponent<WraithCombatLocomotion>() ?? GetComponentInParent<WraithCombatLocomotion>();
        }

        animator = GetComponent<Animator>() ?? GetComponentInParent<Animator>();

        // Cache the exact length of the fire clip from the Animator Controller
        CacheFireClipDuration();

        // Exclude Player layer from raycasts
        aimLayerMask &= ~(1 << gameObject.layer);

        // Calculate initial animation playback speed
        UpdateFireAnimationSpeed();
    }

    private void CacheFireClipDuration()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;

        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip.name.Contains("Fire_A_Fast_V1") || clip.name.Contains("Fire"))
            {
                fireClipDuration = clip.length;
                break;
            }
        }
    }

    public void UpdateFireAnimationSpeed()
    {
        if (animator == null) return;

        // Scale playback speed so the animation completes within the window between shots
        // Clamped at 1.0f so low fire rates don't result in slow-motion recoil
        float calculatedSpeed = Mathf.Max(1.0f, fireClipDuration * fireRate);
        animator.SetFloat(fireAnimSpeedHash, calculatedSpeed);
    }

    void OnValidate()
    {
        // Live updates when tweaking fireRate in the Inspector
        if (Application.isPlaying && animator != null)
        {
            UpdateFireAnimationSpeed();
        }
    }

    public void OnAttack(InputValue value)
    {
        fireInputHeld = value.isPressed;
        if (value.isPressed && !isAutomatic)
        {
            TryShoot();
        }
    }

    public void OnFire(InputValue value)
    {
        OnAttack(value);
    }

    void Update()
    {
        UpdateAimTarget();
        HandleFiring();
    }

    private void HandleFiring()
    {
        if (IsCurrentlySprinting()) return;

        if (Mouse.current != null)
        {
            if (isAutomatic)
            {
                if (Mouse.current.leftButton.isPressed) TryShoot();
            }
            else
            {
                if (Mouse.current.leftButton.wasPressedThisFrame) TryShoot();
            }
        }
        else if (isAutomatic && fireInputHeld)
        {
            TryShoot();
        }
    }

    private bool IsCurrentlySprinting()
    {
        return locomotion != null && locomotion.IsSprinting;
    }

    private void TryShoot()
    {
        if (IsCurrentlySprinting()) return;

        if (Time.time < nextFireTime || bulletPrefab == null || muzzlePoint == null) return;

        nextFireTime = Time.time + (1f / Mathf.Max(0.1f, fireRate));
        SpawnBullet();
    }

    private void SpawnBullet()
    {
        if (gunshotSound != null)
        {
            AudioSource.PlayClipAtPoint(gunshotSound, muzzlePoint.position, 0.8f);
        }

        Vector3 fireDirection = (currentTargetPoint - muzzlePoint.position).normalized;

        GameObject bulletObj = Instantiate(bulletPrefab, muzzlePoint.position, Quaternion.LookRotation(fireDirection));

        WraithBullet bullet = bulletObj.GetComponent<WraithBullet>();
        if (bullet != null)
        {
            bullet.Initialize(fireDirection, aimLayerMask, bulletSpeed, bulletDamage);
        }

        if (animator != null)
        {
            // Reset and trigger to allow immediate re-triggering during rapid fire
            animator.ResetTrigger(fireHash);
            animator.SetTrigger(fireHash);
        }
    }

    private void UpdateAimTarget()
    {
        if (mainCamera == null) return;

        Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (Physics.Raycast(ray, out RaycastHit hit, maxAimDistance, aimLayerMask))
        {
            currentTargetPoint = hit.point;
        }
        else
        {
            currentTargetPoint = ray.GetPoint(maxAimDistance);
        }

        if (showDebugRays && muzzlePoint != null)
        {
            Debug.DrawLine(ray.origin, currentTargetPoint, Color.red);
            Debug.DrawLine(muzzlePoint.position, currentTargetPoint, Color.green);
        }
    }

    void LateUpdate()
    {
        ApplySpineAiming();
    }

    private void ApplySpineAiming()
    {
        if (IsCurrentlySprinting()) return;

        if (aimBone == null || mainCamera == null) return;

        Vector3 aimDirection = (currentTargetPoint - aimBone.position).normalized;

        float targetPitch = Mathf.Asin(Mathf.Clamp(aimDirection.y, -1f, 1f)) * Mathf.Rad2Deg;
        targetPitch = Mathf.Clamp(targetPitch, minPitch, maxPitch);

        Vector3 flatAimDir = new Vector3(aimDirection.x, 0f, aimDirection.z).normalized;
        Vector3 flatFacing = -transform.forward;
        float targetYaw = Vector3.SignedAngle(flatFacing, flatAimDir, Vector3.up);
        targetYaw = Mathf.Clamp(targetYaw, -35f, 35f);

        smoothedPitch = Mathf.Lerp(smoothedPitch, targetPitch, aimTurnSpeed * Time.deltaTime);
        smoothedYaw = Mathf.Lerp(smoothedYaw, targetYaw, aimTurnSpeed * Time.deltaTime);

        Quaternion pitchDelta = Quaternion.AngleAxis(-smoothedPitch, mainCamera.transform.right);
        Quaternion yawDelta = Quaternion.AngleAxis(smoothedYaw, Vector3.up);

        aimBone.rotation = yawDelta * pitchDelta * aimBone.rotation;
    }
}