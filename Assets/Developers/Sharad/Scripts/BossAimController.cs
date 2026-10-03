using UnityEngine;

[DisallowMultipleComponent]
public class BossAimController : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform currentTarget;
    [SerializeField] private string playerTag = "Player";

    [Header("360 Turret Swivel (Mech Waist Bearing)")]
    [Tooltip("Drag bone spine_01 here. Rotates 360 like a tank turret.")]
    [SerializeField] private Transform waistBearing;
    [SerializeField] private float turnSpeed = 70f;
    [SerializeField] private float yawOffset = 180f; // Aligns cannon with target

    [Header("Shoulder Cannon Pitch (Vertical)")]
    [SerializeField] private Transform cannonBone;
    [SerializeField] private float cannonPitchSpeed = 90f;
    [SerializeField] private float minPitch = -20f;
    [SerializeField] private float maxPitch = 45f;

    [Header("Aim Lockout")]
    [SerializeField] private bool isAimLocked = false;

    private BossHealth bossHealth;
    private Animator bossAnimator;
    private float currentTurretYaw;
    private float currentCannonPitch;

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
        // Only completely stop on death
        if (bossHealth != null && bossHealth.IsDead) return;

        FindTargetIfNull();

        // 1. Freeze tracking during aim-lock OR hit flinch (so boss doesn't spin while getting hit)
        bool freezeTracking = isAimLocked || IsFlinching() || (bossHealth != null && bossHealth.IsStaggered);

        if (!freezeTracking && currentTarget != null)
        {
            UpdateTargetAngles();
        }

        // 2. ALWAYS hold the facing angle on waist bearing (prevents snapping sideways!)
        ApplyBoneRotations();
    }

    private void UpdateTargetAngles()
    {
        Vector3 direction = currentTarget.position - waistBearing.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) return;

        float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + yawOffset;
        currentTurretYaw = Mathf.MoveTowardsAngle(currentTurretYaw, targetAngle, turnSpeed * Time.deltaTime);
    }

    private void ApplyBoneRotations()
    {
        if (waistBearing != null)
        {
            // Holds the turret locked at target throughout attack
            waistBearing.rotation = Quaternion.Euler(waistBearing.eulerAngles.x, currentTurretYaw, waistBearing.eulerAngles.z);
        }

        if (cannonBone != null && currentTarget != null)
        {
            Vector3 toTarget = currentTarget.position - cannonBone.position;
            if (toTarget.sqrMagnitude >= 0.001f)
            {
                Quaternion lookRot = Quaternion.LookRotation(toTarget);
                float targetPitch = lookRot.eulerAngles.x;
                if (targetPitch > 180f) targetPitch -= 360f;
                targetPitch = Mathf.Clamp(targetPitch, minPitch, maxPitch);

                // Uses cannonPitchSpeed to tilt cannon smoothly
                currentCannonPitch = Mathf.MoveTowardsAngle(currentCannonPitch, targetPitch, cannonPitchSpeed * Time.deltaTime);
                cannonBone.rotation = Quaternion.Euler(currentCannonPitch, currentTurretYaw, 0f);
            }
        }
    }

    private bool IsFlinching()
    {
        if (bossAnimator == null) return false;
        return bossAnimator.GetCurrentAnimatorStateInfo(0).IsName("Hit_React_Fwd");
    }

    private bool ShouldFreezeAim()
    {
        if (isAimLocked) return true;
        if (bossHealth != null && (bossHealth.IsDead || bossHealth.IsStaggered)) return true;

        if (bossAnimator != null)
        {
            AnimatorStateInfo state = bossAnimator.GetCurrentAnimatorStateInfo(0);
            if (state.IsName("Hit_React_Fwd")) return true;
        }

        return false;
    }

    private void SwivelTurretYaw()
    {
        if (waistBearing == null) return;

        Vector3 direction = currentTarget.position - waistBearing.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) return;

        float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + yawOffset;
        currentTurretYaw = Mathf.MoveTowardsAngle(currentTurretYaw, targetAngle, turnSpeed * Time.deltaTime);

        // Continuous 360 swivel on Y-axis
        waistBearing.rotation = Quaternion.Euler(waistBearing.eulerAngles.x, currentTurretYaw, waistBearing.eulerAngles.z);
    }

    private void AimCannonPitch()
    {
        if (cannonBone == null) return;

        Vector3 toTarget = currentTarget.position - cannonBone.position;
        if (toTarget.sqrMagnitude < 0.001f) return;

        Quaternion lookRot = Quaternion.LookRotation(toTarget);
        float pitch = lookRot.eulerAngles.x;
        if (pitch > 180f) pitch -= 360f;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        cannonBone.rotation = Quaternion.Euler(pitch, currentTurretYaw, 0f);
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