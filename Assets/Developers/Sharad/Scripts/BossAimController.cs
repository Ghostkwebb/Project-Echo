using UnityEngine;

[DisallowMultipleComponent]
public class BossAimController : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform currentTarget;
    [SerializeField] private string playerTag = "Player";

    [Header("360 Turret Swivel (Mech Waist)")]
    [Tooltip("Drag bone spine_01 here.")]
    [SerializeField] private Transform waistBearing;
    [SerializeField] private float maxTurnSpeed = 75f;
    [Tooltip("Damping delay in seconds. Higher = heavier, lazier mech tracking.")]
    [SerializeField] private float trackingDelay = 0.35f;
    [SerializeField] private float yawOffset = 155f;

    [Header("Cannon Forearm Aim Alignment")]
    [SerializeField] private Transform rightForearmBone;
    [SerializeField] private Transform muzzlePoint;
    [SerializeField] private float maxForearmAdjustment = 45f;
    [SerializeField] private float forearmSmoothSpeed = 8f; // Smooths arm aiming

    [Header("Aim Lockout")]
    [SerializeField] private bool isAimLocked = false;

    private BossHealth bossHealth;
    private Animator bossAnimator;
    private float currentTurretYaw;
    private float yawVelocity;
    private Quaternion currentForearmCorrection = Quaternion.identity;

    public Transform CurrentTarget => currentTarget;

    private void Awake()
    {
        bossHealth = GetComponent<BossHealth>();
        bossAnimator = GetComponentInChildren<Animator>();
        FindTargetIfNull();

        if (waistBearing != null)
        {
            currentTurretYaw = waistBearing.eulerAngles.y;
        }
    }

    private void LateUpdate()
    {
        if (bossHealth != null && bossHealth.IsDead) return;

        FindTargetIfNull();

        // 1. Update tracking angle with hydraulic inertia
        bool freezeTracking = isAimLocked || IsFlinching() || (bossHealth != null && bossHealth.IsStaggered);

        if (!freezeTracking && currentTarget != null)
        {
            UpdateTargetAngles();
        }

        // 2. Apply smooth damped yaw to waist bearing
        if (waistBearing != null)
        {
            waistBearing.rotation = Quaternion.Euler(waistBearing.eulerAngles.x, currentTurretYaw, waistBearing.eulerAngles.z);
        }

        // 3. Smooth forearm aim
        if (currentTarget != null && !IsFlinching())
        {
            AlignForearmToTarget();
        }
    }

    private void UpdateTargetAngles()
    {
        if (waistBearing == null) return;

        Vector3 targetDir = currentTarget.position - waistBearing.position;
        targetDir.y = 0f;
        if (targetDir.sqrMagnitude < 0.001f) return;

        float targetAngle = Mathf.Atan2(targetDir.x, targetDir.z) * Mathf.Rad2Deg + yawOffset;

        // SmoothDampAngle adds mechanical mass & lazy delay curve
        currentTurretYaw = Mathf.SmoothDampAngle(
            currentTurretYaw,
            targetAngle,
            ref yawVelocity,
            trackingDelay,
            maxTurnSpeed,
            Time.deltaTime
        );
    }

    private void AlignForearmToTarget()
    {
        if (rightForearmBone == null || muzzlePoint == null || currentTarget == null) return;

        Vector3 barrelDir = (muzzlePoint.position - rightForearmBone.position).normalized;
        Vector3 targetDir = (currentTarget.position - rightForearmBone.position).normalized;

        if (barrelDir.sqrMagnitude < 0.001f || targetDir.sqrMagnitude < 0.001f) return;

        Quaternion targetDelta = Quaternion.FromToRotation(barrelDir, targetDir);

        targetDelta.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f) angle -= 360f;
        angle = Mathf.Clamp(angle, -maxForearmAdjustment, maxForearmAdjustment);

        Quaternion targetCorrection = Quaternion.AngleAxis(angle, axis);

        // Smoothly slerp arm correction so hand never snaps instantly
        currentForearmCorrection = Quaternion.Slerp(
            currentForearmCorrection,
            targetCorrection,
            forearmSmoothSpeed * Time.deltaTime
        );

        rightForearmBone.rotation = currentForearmCorrection * rightForearmBone.rotation;
    }

    private bool IsFlinching()
    {
        if (bossAnimator == null) return false;
        return bossAnimator.GetCurrentAnimatorStateInfo(0).IsName("Hit_React_Fwd");
    }

    public void SetAimLocked(bool locked) => isAimLocked = locked;
    public void SetTarget(Transform target) => currentTarget = target;

    private void FindTargetIfNull()
    {
        if (currentTarget == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag(playerTag);
            if (p != null) currentTarget = p.transform;
        }
    }
}