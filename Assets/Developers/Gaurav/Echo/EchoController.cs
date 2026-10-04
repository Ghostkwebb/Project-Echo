using UnityEngine;

[DisallowMultipleComponent]
public class EchoController : MonoBehaviour
{
    [Header("Echo Identity")]
    [SerializeField] private int echoIndex = 1;
    [SerializeField] private float maxHealth = 60f; // Can be destroyed by boss attacks (GDD 20)

    [Header("Weapon Spawning")]
    [Tooltip("Drag WraithBullet prefab here.")]
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
    private static readonly int FireHash = Animator.StringToHash("Fire");

    private void Awake()
    {
        if (ghostAnimator == null)
            ghostAnimator = GetComponentInChildren<Animator>();

        if (muzzlePoint == null)
            muzzlePoint = transform;

        if (hitLayers.value == 0)
        {
            hitLayers = LayerMask.GetMask("BossPart", "BossCore", "Default", "Floor");
        }
    }

    public void Initialize(EchoData data, int index)
    {
        Data = data;
        echoIndex = index;
        PlaybackTime = 0f;
        CurrentHealth = maxHealth;
        IsAlive = true;
        nextFireAllowedTime = 0f;

        gameObject.name = $"Echo_Ghost_{index:00}";
        gameObject.SetActive(true);

        // Snap to initial recorded spawn frame
        if (data.Snapshots.Count > 0)
        {
            transform.position = data.Snapshots[0].Position;
            transform.rotation = data.Snapshots[0].Rotation;
        }
    }

    private void Update()
    {
        if (!IsAlive || Data == null) return;

        PlaybackTime += Time.deltaTime;

        // 1. Check Lifespan Expiration (GDD Section 6)
        if (PlaybackTime >= Data.TotalLifespan)
        {
            CompleteLifespan();
            return;
        }

        // 2. Sample 50Hz Buffer with O(1) Smooth Interpolation
        if (Data.Sample(PlaybackTime, out Vector3 targetPos, out Quaternion targetRot, out Vector3 aimPoint, out EchoActionFlags actions))
        {
            transform.position = targetPos;
            transform.rotation = targetRot;

            // 3. Replay Actions
            HandleReplayActions(actions, aimPoint);
        }
        else
        {
            CompleteLifespan();
        }
    }

    private void HandleReplayActions(EchoActionFlags actions, Vector3 aimPoint)
    {
        // Replay Weapon Fire
        if ((actions & EchoActionFlags.Fire) != 0 && Time.time >= nextFireAllowedTime)
        {
            FireEchoBullet(aimPoint);
            nextFireAllowedTime = Time.time + 0.15f; // Fire-rate safety guard
        }
    }

    private void FireEchoBullet(Vector3 targetAimPoint)
    {
        if (bulletPrefab == null) return;

        Vector3 spawnPos = muzzlePoint != null ? muzzlePoint.position : transform.position + Vector3.up * 1.2f;
        Vector3 fireDir = (targetAimPoint - spawnPos).normalized;

        if (fireDir.sqrMagnitude < 0.001f) fireDir = transform.forward;

        GameObject bulletObj = Instantiate(bulletPrefab, spawnPos, Quaternion.LookRotation(fireDir));

        // Initialize bullet: Deals Echo-tagged damage! (Shield only, 0 Health during shield phase)
        if (bulletObj.TryGetComponent<WraithBullet>(out var bullet))
        {
            bullet.Initialize(fireDir, hitLayers, bulletSpeed, bulletDamage);
        }

        // Also notify Threat Monitor of Echo attack
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

    /// <summary>
    /// Called when hit by boss attacks (Stomp, Laser, Missiles).
    /// GDD Section 20: Destruction is temporary for current attempt only.
    /// </summary>
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
        // Temporary deactivation for this life only (returns next life!)
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

        if (Data != null && Data.Snapshots.Count > 0)
        {
            transform.position = Data.Snapshots[0].Position;
            transform.rotation = Data.Snapshots[0].Rotation;
            gameObject.SetActive(true);
        }
    }
}