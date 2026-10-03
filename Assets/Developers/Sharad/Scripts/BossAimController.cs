using UnityEngine;

[DisallowMultipleComponent]
public class BossAimController : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform currentTarget;
    [SerializeField] private string playerTag = "Player";

    [Header("Mech Rotation Tracking")]
    [SerializeField] private float turnSpeed = 75f; // degrees per second
    [SerializeField] private float yawOffset = 180f; // Flips Unreal FBX backward axis

    [Header("Aim Lockout")]
    [SerializeField] private bool isAimLocked = false;

    private BossHealth bossHealth;
    private Animator bossAnimator;

    public Transform CurrentTarget => currentTarget;

    private void Awake()
    {
        bossHealth = GetComponent<BossHealth>();
        bossAnimator = GetComponentInChildren<Animator>();
        FindTargetIfNull();
    }

    private void Update()
    {
        if (ShouldFreezeAim()) return;

        FindTargetIfNull();
        if (currentTarget == null) return;

        RotateMechTowardsTarget();
    }

    private bool ShouldFreezeAim()
    {
        if (isAimLocked) return true;
        if (bossHealth != null && (bossHealth.IsDead || bossHealth.IsStaggered)) return true;

        // Freeze rotation during hit flinch so animation plays clean
        if (bossAnimator != null)
        {
            AnimatorStateInfo state = bossAnimator.GetCurrentAnimatorStateInfo(0);
            if (state.IsName("Hit_React_Fwd")) return true;
        }

        return false;
    }

    private void RotateMechTowardsTarget()
    {
        Vector3 direction = currentTarget.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) return;

        float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + yawOffset;
        Quaternion targetRotation = Quaternion.Euler(0f, targetAngle, 0f);

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            turnSpeed * Time.deltaTime
        );
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