using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class DummyBarrageAttack : BossAttackBase
{
    [Header("Barrage Tuning")]
    [SerializeField] private float damage = 20f;
    [SerializeField] private float projectileSpeed = 22f;
    [SerializeField] private float telegraphDuration = 0.8f;
    [SerializeField] private float fireDelayAfterAnim = 0.25f;

    [Header("Spawn Hardpoint")]
    [Tooltip("Transform where bullets spawn. Drag right cannon barrel socket or bone here.")]
    [SerializeField] private Transform muzzlePoint;

    [Header("References")]
    [SerializeField] private Animator bossAnimator;
    [SerializeField] private BossAimController aimController;

    private Coroutine attackRoutine;
    private static readonly int OnAttackBarrageHash = Animator.StringToHash("OnAttack_Barrage");

    protected override void Awake()
    {
        base.Awake();

        if (bossAnimator == null)
            bossAnimator = GetComponentInParent<Animator>();

        if (aimController == null)
            aimController = GetComponentInParent<BossAimController>();

        if (linkedPart == null)
            linkedPart = GetComponentInParent<BossPart>();

        if (muzzlePoint == null)
            muzzlePoint = transform;
    }

    protected override void OnStartAttack(Transform target)
    {
        attackRoutine = StartCoroutine(ExecuteBarrageRoutine(target));
    }

    private IEnumerator ExecuteBarrageRoutine(Transform target)
    {
        // 1. Telegraph Phase (Boss tracks target, charging sound/glint)
        yield return new WaitForSeconds(telegraphDuration);

        // Lock aim 0.2s before trigger pull so player can dodge sideways
        if (aimController != null)
        {
            aimController.SetAimLocked(true);
        }

        yield return new WaitForSeconds(0.2f);

        // 2. Fire Animation
        if (bossAnimator != null)
        {
            bossAnimator.SetTrigger(OnAttackBarrageHash);
        }

        yield return new WaitForSeconds(fireDelayAfterAnim);

        // 3. Spawn Test Projectile
        Vector3 targetPoint = target != null ? target.position + Vector3.up * 1.0f : muzzlePoint.position + muzzlePoint.forward * 10f;
        SpawnTestProjectile(targetPoint);

        // 4. Recovery Window
        yield return new WaitForSeconds(0.4f);

        if (aimController != null)
        {
            aimController.SetAimLocked(false);
        }

        attackRoutine = null;
        FinishAttack();
    }

    protected override void OnInterrupt()
    {
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        if (aimController != null)
        {
            aimController.SetAimLocked(false);
        }
    }

    private void SpawnTestProjectile(Vector3 targetPoint)
    {
        Vector3 spawnPos = muzzlePoint != null ? muzzlePoint.position : transform.position;
        Vector3 fireDirection = (targetPoint - spawnPos).normalized;

        // Create standalone red test sphere (Zero prefab required for prototype)
        GameObject proj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        proj.name = "Boss_Test_Bullet";
        proj.transform.position = spawnPos;
        proj.transform.localScale = Vector3.one * 0.45f;

        // Unlit red color
        Renderer rend = proj.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material.color = Color.red;
        }

        // Add projectile flight behavior
        BossTestBullet bullet = proj.AddComponent<BossTestBullet>();
        bullet.Initialize(fireDirection, projectileSpeed, damage);
    }
}

/// <summary>
/// Simple self-contained flying bullet. Destroys on impact or after 5 seconds.
/// </summary>
public class BossTestBullet : MonoBehaviour
{
    private Vector3 moveDirection;
    private float moveSpeed;
    private float bulletDamage;

    public void Initialize(Vector3 dir, float speed, float dmg)
    {
        moveDirection = dir;
        moveSpeed = speed;
        bulletDamage = dmg;

        // Auto-destroy after 5 seconds if flies out of arena
        Destroy(gameObject, 5f);
    }

    private void Update()
    {
        transform.position += moveDirection * (moveSpeed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        // Ignore boss colliders
        if (other.gameObject.layer == LayerMask.NameToLayer("BossPart") ||
            other.gameObject.layer == LayerMask.NameToLayer("BossCore"))
        {
            return;
        }

        // Destroy bullet on hitting arena floor/cover or player
        Destroy(gameObject);
    }
}