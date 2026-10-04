using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class BossAttack_MachineGun : BossAttackBase
{
    [Header("Machine Gun Tuning (Designer PDF 1)")]
    [SerializeField] private float damagePerBullet = 4f;
    [SerializeField] private float fireRate = 12f; // 12 rounds per sec
    [SerializeField] private float burstDuration = 3.5f;
    [SerializeField] private float telegraphDuration = 1.0f;
    [SerializeField] private float trackingLag = 0.4f; // 0.3-0.5s lag
    [SerializeField] private float bulletSpeed = 45f;
    [SerializeField] private float spreadAngle = 1.0f;

    [Header("Muzzle & Visuals")]
    [SerializeField] private Transform muzzlePoint;
    [SerializeField] private LineRenderer telegraphLaser;

    [Header("Muzzle Flash FX")]
    [Tooltip("Drag MuzzleFlash_FX child object here.")]
    [SerializeField] private GameObject muzzleFlashObject;
    [SerializeField] private float muzzleFlashBaseScale = 1.5f;

    private Animator bossAnimator;
    private Coroutine attackRoutine;
    private Vector3 currentAimPoint;
    private static readonly int IsFiringMGHash = Animator.StringToHash("IsFiringMG");

    protected override void Awake()
    {
        base.Awake();

        bossAnimator = GetComponentInParent<Animator>();

        if (linkedPart == null)
            linkedPart = GetComponentInParent<BossPart>();

        if (muzzlePoint == null)
            muzzlePoint = transform;

        CreateLaserIfNull();
    }

    protected override void OnStartAttack(Transform target)
    {
        attackRoutine = StartCoroutine(MachineGunRoutine(target));
    }

    private IEnumerator MachineGunRoutine(Transform target)
    {
        currentAimPoint = target != null ? target.position : muzzlePoint.position + muzzlePoint.forward * 10f;

        // 1. TELEGRAPH (1.0s Red Laser Line)
        if (telegraphLaser != null) telegraphLaser.enabled = true;

        float chargeTimer = 0f;
        while (chargeTimer < telegraphDuration)
        {
            chargeTimer += Time.deltaTime;

            if (target != null)
            {
                currentAimPoint = target.position + Vector3.up * 1.1f;
                if (telegraphLaser != null)
                {
                    telegraphLaser.SetPosition(0, muzzlePoint.position);
                    telegraphLaser.SetPosition(1, target.position);
                }
            }
            yield return null;
        }

        if (telegraphLaser != null) telegraphLaser.enabled = false;

        // 2. CONTINUOUS FIRING (3.5s Rapid Fire at 2.5x - 3x anim speed)
        if (bossAnimator != null)
        {
            bossAnimator.SetBool(IsFiringMGHash, true);
        }

        Vector3 aimVelocity = Vector3.zero;
        float burstTimer = 0f;
        float fireInterval = 1f / fireRate;
        float nextShotTime = 0f;

        while (burstTimer < burstDuration)
        {
            burstTimer += Time.deltaTime;

            // 0.4s Damped Tracking Lag (Player sprints to dodge out of stream)
            if (target != null)
            {
                // Aims at human chest height (1.1m above feet)
                Vector3 chestTarget = target.position + Vector3.up * 1.1f;
                currentAimPoint = Vector3.SmoothDamp(currentAimPoint, chestTarget, ref aimVelocity, trackingLag);
            }

            if (burstTimer >= nextShotTime)
            {
                FireMachineGunBullet(currentAimPoint);
                nextShotTime = burstTimer + fireInterval;
            }

            yield return null;
        }

        if (muzzleFlashObject != null) muzzleFlashObject.SetActive(false);

        // Stop firing loop
        if (bossAnimator != null)
        {
            bossAnimator.SetBool(IsFiringMGHash, false);
        }

        // 3. RECOVERY
        yield return new WaitForSeconds(1.0f);

        attackRoutine = null;
        FinishAttack();
    }

    private void FireMachineGunBullet(Vector3 targetPos)
    {
        if (muzzleFlashObject != null)
        {
            StopCoroutine(nameof(MuzzleFlashStrobeRoutine));
            StartCoroutine(nameof(MuzzleFlashStrobeRoutine));
        }

        Vector3 spawnPos = muzzlePoint.position;
        Vector3 baseDir = (targetPos - spawnPos).normalized;

        Vector3 spreadDir = Quaternion.Euler(
            Random.Range(-spreadAngle, spreadAngle),
            Random.Range(-spreadAngle, spreadAngle),
            0f
        ) * baseDir;

        // 1. Create Stretched Capsule Tracer (Overwatch / Borderlands style)
        GameObject proj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        proj.name = "Boss_MG_Tracer";
        proj.transform.position = spawnPos;

        // Stretched thin needle (Length 0.65m, Thickness 0.08m)
        proj.transform.localScale = new Vector3(0.08f, 0.65f, 0.08f);

        // Align long axis dead-straight along travel trajectory
        proj.transform.rotation = Quaternion.LookRotation(spreadDir) * Quaternion.Euler(90f, 0f, 0f);

        // Remove solid physics collider (trigger only)
        Collider c = proj.GetComponent<Collider>();
        if (c != null) c.isTrigger = true;

        // Blinding High-Emission Plasma Yellow/Orange
        Renderer rend = proj.GetComponent<Renderer>();
        if (rend != null)
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.color = new Color(1f, 0.75f, 0.1f); // Laser yellow core
            rend.material = mat;
        }

        // 2. Micro High-Speed Trail Ribbon behind bullet
        TrailRenderer tr = proj.AddComponent<TrailRenderer>();
        tr.time = 0.08f; // Super short micro-tail
        tr.startWidth = 0.10f;
        tr.endWidth = 0.01f;
        tr.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        tr.material.SetFloat("_Surface", 1);
        tr.material.color = new Color(1f, 0.45f, 0.05f, 0.75f); // Neon orange tail

        BossMGBullet bullet = proj.AddComponent<BossMGBullet>();
        bullet.Initialize(spreadDir, bulletSpeed, damagePerBullet);
    }

    private IEnumerator MuzzleFlashStrobeRoutine()
    {
        muzzleFlashObject.SetActive(true);

        // Random spin and jitter on every bullet
        muzzleFlashObject.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        float jitter = Random.Range(0.85f, 1.25f) * muzzleFlashBaseScale;
        muzzleFlashObject.transform.localScale = Vector3.one * jitter;

        yield return new WaitForSeconds(0.04f); // 40ms flash duration

        muzzleFlashObject.SetActive(false);
    }

    protected override void OnInterrupt()
    {
        if (muzzleFlashObject != null) muzzleFlashObject.SetActive(false);

        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        if (bossAnimator != null)
        {
            bossAnimator.SetBool(IsFiringMGHash, false);
        }

        if (telegraphLaser != null)
        {
            telegraphLaser.enabled = false;
        }
    }

    private void CreateLaserIfNull()
    {
        if (telegraphLaser != null) return;

        GameObject laserObj = new GameObject("TelegraphLaser");
        laserObj.transform.SetParent(muzzlePoint);
        laserObj.transform.localPosition = Vector3.zero;

        LineRenderer lr = laserObj.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.startWidth = 0.035f;
        lr.endWidth = 0.035f;
        lr.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        lr.material.color = new Color(1f, 0.1f, 0.1f, 0.85f);
        lr.enabled = false;

        telegraphLaser = lr;
    }
}

public class BossMGBullet : MonoBehaviour
{
    private Vector3 moveDir;
    private float speed;
    private float damage;
    private LayerMask hitMask;

    public void Initialize(Vector3 dir, float spd, float dmg)
    {
        moveDir = dir;
        speed = spd;
        damage = dmg;
        hitMask = LayerMask.GetMask("Player", "Default", "Floor");
        Destroy(gameObject, 3.5f);
    }

    private void Update()
    {
        float step = speed * Time.deltaTime;

        // Continuous raycast reliably hits CharacterController (Zero Rigidbody bugs)
        if (Physics.Raycast(transform.position, moveDir, out RaycastHit hit, step, hitMask, QueryTriggerInteraction.Ignore))
        {
            // Ignore boss parts
            if (hit.collider.gameObject.layer != LayerMask.NameToLayer("BossPart") &&
                hit.collider.gameObject.layer != LayerMask.NameToLayer("BossCore"))
            {
                if (hit.collider.CompareTag("Player") || hit.collider.gameObject.layer == LayerMask.NameToLayer("Player"))
                {
                    hit.collider.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
                }
                Destroy(gameObject);
                return;
            }
        }

        transform.position += moveDir * step;
    }
}