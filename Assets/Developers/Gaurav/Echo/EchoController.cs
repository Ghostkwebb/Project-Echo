using UnityEngine;

[DisallowMultipleComponent]
public class EchoController : MonoBehaviour
{
    [Header("Echo Identity")]
    [SerializeField] private int echoIndex = 1;
    [SerializeField] private float maxHealth = 60f;

    [Header("Weapon Spawning")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform muzzlePoint;
    [SerializeField] private float bulletSpeed = 85f;
    [SerializeField] private float bulletDamage = 25f;
    [SerializeField] private LayerMask hitLayers;

    [Header("References")]
    [SerializeField] private Animator ghostAnimator;

    // State
    public bool IsAlive { get; private set; }
    public EchoData Data { get; private set; }
    public int EchoIndex => echoIndex;
    public float PlaybackTime { get; private set; }
    public float CurrentHealth { get; private set; }

    private float nextFireAllowedTime;
    private Vector3 lastPosition;
    private float spawnMaterializeTimer;
    private Vector3 formationOffset;


    // Animator Hashes
    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveZHash = Animator.StringToHash("MoveZ");
    private static readonly int IsSprintingHash = Animator.StringToHash("IsSprinting");
    private static readonly int IsAimingHash = Animator.StringToHash("IsAiming");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int FireHash = Animator.StringToHash("Fire");

    private void Awake()
    {
        if (ghostAnimator == null) ghostAnimator = GetComponentInChildren<Animator>();
        if (muzzlePoint == null) muzzlePoint = transform;

        if (hitLayers.value == 0)
        {
            hitLayers = LayerMask.GetMask("BossPart", "BossCore", "Default", "Floor");
        }
    }

    public void Initialize(EchoData data, int index, Vector3 squadOffset)
    {
        Data = data;
        echoIndex = index;
        formationOffset = squadOffset; // Tactical offset applied
        PlaybackTime = 0f;
        CurrentHealth = maxHealth;
        IsAlive = true;
        nextFireAllowedTime = 0f;

        gameObject.name = $"Echo_Ghost_{index:00}";
        gameObject.SetActive(true);

        if (data.Snapshots.Count > 0)
        {
            transform.position = data.Snapshots[0].Position + formationOffset;
            transform.rotation = data.Snapshots[0].Rotation;
            lastPosition = transform.position;
        }
    }

    private void Update()
    {
        if (!IsAlive || Data == null) return;

        PlaybackTime += Time.deltaTime;

        // Smooth materialization at T=0 so ghosts don't clip inside player
        if (spawnMaterializeTimer < 0.35f)
        {
            spawnMaterializeTimer += Time.deltaTime;
            float scaleP = Mathf.Clamp01(spawnMaterializeTimer / 0.35f);
            transform.localScale = Vector3.one * scaleP;
        }
        else
        {
            transform.localScale = Vector3.one;
        }

        // 1. Check Lifespan Expiration
        if (PlaybackTime >= Data.TotalLifespan)
        {
            CompleteLifespan();
            return;
        }

        // 2. Sample 50Hz Buffer with Smooth Lerp
        if (Data.Sample(PlaybackTime, out Vector3 targetPos, out Quaternion targetRot, out Vector3 aimPoint, out EchoActionFlags actions))
        {
            transform.position = targetPos + formationOffset;
            transform.rotation = targetRot;

            // 3. Drive Ghost Animations from Velocity!
            UpdateGhostLocomotion(targetPos, actions);

            // 4. Replay Weapon Actions
            HandleReplayActions(actions, aimPoint);

            lastPosition = targetPos;
        }
        else
        {
            CompleteLifespan();
        }
    }

    private void UpdateGhostLocomotion(Vector3 currentPos, EchoActionFlags actions)
    {
        if (ghostAnimator == null) return;

        // Calculate velocity vector in local space
        Vector3 worldVel = (currentPos - lastPosition) / Mathf.Max(0.001f, Time.deltaTime);
        Vector3 localVel = transform.InverseTransformDirection(worldVel);

        // Normalize to walk speed (~4.5 m/s)
        float moveX = Mathf.Clamp(localVel.x / 4.5f, -1f, 1f);
        float moveZ = Mathf.Clamp(localVel.z / 4.5f, -1f, 1f);

        ghostAnimator.SetFloat(MoveXHash, moveX, 0.1f, Time.deltaTime);
        ghostAnimator.SetFloat(MoveZHash, moveZ, 0.1f, Time.deltaTime);
        ghostAnimator.SetBool(IsSprintingHash, (actions & EchoActionFlags.Sprint) != 0);
        ghostAnimator.SetBool(IsAimingHash, (actions & EchoActionFlags.ADS) != 0);
        ghostAnimator.SetBool(IsGroundedHash, true);
    }

    private void HandleReplayActions(EchoActionFlags actions, Vector3 aimPoint)
    {
        if ((actions & EchoActionFlags.Fire) != 0 && Time.time >= nextFireAllowedTime)
        {
            FireEchoBullet(aimPoint);
            nextFireAllowedTime = Time.time + 0.15f;
        }
    }

    private void FireEchoBullet(Vector3 targetAimPoint)
    {
        if (bulletPrefab == null) return;

        Vector3 spawnPos = muzzlePoint != null ? muzzlePoint.position : transform.position + Vector3.up * 1.2f;
        Vector3 fireDir = (targetAimPoint - spawnPos).normalized;

        if (fireDir.sqrMagnitude < 0.001f) fireDir = transform.forward;

        GameObject bulletObj = Instantiate(bulletPrefab, spawnPos, Quaternion.LookRotation(fireDir));

        if (bulletObj.TryGetComponent<WraithBullet>(out var bullet))
        {
            bullet.Initialize(fireDir, hitLayers, bulletSpeed, bulletDamage);
        }

        BossThreatMonitor threat = FindAnyObjectByType<BossThreatMonitor>();
        if (threat != null)
        {
            threat.RecordDamage(echoIndex, transform, bulletDamage);
        }

        if (ghostAnimator != null)
        {
            ghostAnimator.ResetTrigger(FireHash);
            ghostAnimator.SetTrigger(FireHash);
        }
    }

    public void TakeDamage(float amount)
    {
        if (!IsAlive) return;

        CurrentHealth -= amount;
        if (CurrentHealth <= 0f)
        {
            DestroyForCurrentRun();
        }
    }

    private void DestroyForCurrentRun()
    {
        IsAlive = false;
        gameObject.SetActive(false);
    }

    private void CompleteLifespan()
    {
        IsAlive = false;
        gameObject.SetActive(false);
    }

    public void ResetForNewAttempt()
    {
        PlaybackTime = 0f;
        CurrentHealth = maxHealth;
        IsAlive = true;
        spawnMaterializeTimer = 0f;

        if (Data != null && Data.Snapshots.Count > 0)
        {
            transform.position = Data.Snapshots[0].Position + formationOffset;
            lastPosition = transform.position;
            transform.rotation = Data.Snapshots[0].Rotation;
            lastPosition = transform.position;
            transform.localScale = Vector3.zero;
            gameObject.SetActive(true);
        }
    }
}