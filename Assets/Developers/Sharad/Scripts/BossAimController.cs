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
    [SerializeField] private float turnSpeed = 70f;
    [SerializeField] private float yawOffset = 180f;

    [Header("Cannon Forearm Aim Alignment")]
    [Tooltip("Drag bone lowerarm_r here.")]
    [SerializeField] private Transform rightForearmBone;
    [Tooltip("Drag MuzzlePoint here.")]
    [SerializeField] private Transform muzzlePoint;
    [SerializeField] private float maxForearmAdjustment = 45f;

    [Header("Aim Lockout")]
    [SerializeField] private bool isAimLocked = false;

    private BossHealth bossHealth;
    private Animator bossAnimator;
    private float currentTurretYaw;

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

        // 1. Update tracking angle if not locked or flinching
        bool freezeTracking = isAimLocked || IsFlinching() || (bossHealth != null && bossHealth.IsStaggered);

        if (!freezeTracking && currentTarget != null)
        {
            UpdateTargetAngles();
        }

        // 2. Hold waist bearing orientation
        if (waistBearing != null)
        {
            waistBearing.rotation = Quaternion.Euler(waistBearing.eulerAngles.x, currentTurretYaw, waistBearing.eulerAngles.z);
        }

        // 3. Option B: Align right arm shoulder ball-joint dead at target
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
        currentTurretYaw = Mathf.MoveTowardsAngle(currentTurretYaw, targetAngle, turnSpeed * Time.deltaTime);
    }

    private void AlignForearmToTarget()
    {
        if (rightForearmBone == null || muzzlePoint == null || currentTarget == null) return;

        // Physical centerline from elbow to muzzle tip in 3D world space
        Vector3 barrelDir = (muzzlePoint.position - rightForearmBone.position).normalized;
        Vector3 targetDir = (currentTarget.position - rightForearmBone.position).normalized;

        if (barrelDir.sqrMagnitude < 0.001f || targetDir.sqrMagnitude < 0.001f) return;

        // Rotation needed to point barrel line dead at target
        Quaternion delta = Quaternion.FromToRotation(barrelDir, targetDir);

        // Clamp to prevent elbow hyperextension
        delta.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f) angle -= 360f;
        angle = Mathf.Clamp(angle, -maxForearmAdjustment, maxForearmAdjustment);

        rightForearmBone.rotation = Quaternion.AngleAxis(angle, axis) * rightForearmBone.rotation;
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