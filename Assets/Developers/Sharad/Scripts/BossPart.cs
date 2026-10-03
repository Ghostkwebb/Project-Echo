using System;
using UnityEngine;

public enum BossPartType
{
    RightArmCannon,
    LeftArmLauncher,
    ShoulderCannon,
    BackRockets
}

[DisallowMultipleComponent]
public class BossPart : MonoBehaviour
{
    [Header("Part Identity")]
    [SerializeField] private BossPartType partType;
    [SerializeField] private string partDisplayName = "Boss Part";

    [Header("Break & Repair Tuning")]
    [SerializeField] private float breakThreshold = 300f;
    [SerializeField] private float repairDuration = 15f;

    [Header("Hitbox Colliders To Disable On Break")]
    [Tooltip("Drag child collider objects here. Disabled when broken so empty socket takes no damage.")]
    [SerializeField] private Collider[] partColliders;

    [Header("Optional Visuals / Debris")]
    [Tooltip("Mesh or object to hide/detach when broken.")]
    [SerializeField] private GameObject visualMesh;

    // References
    private BossHealth bossHealth;
    private Animator bossAnimator;

    // State
    public float CurrentBreakProgress { get; private set; }
    public float BreakThreshold => breakThreshold;
    public bool IsBroken { get; private set; }
    public BossPartType PartType => partType;
    public string PartDisplayName => partDisplayName;

    private float repairTimer;
    private static readonly int OnPartBreakHash = Animator.StringToHash("OnPartBreak");

    // Events
    public event Action<BossPart> OnPartBroken;
    public event Action<BossPart> OnPartRepaired;
    public event Action<float, float> OnProgressChanged; // current, max

    private void Awake()
    {
        bossHealth = GetComponentInParent<BossHealth>();
        bossAnimator = GetComponentInParent<Animator>();

        // Auto-cache child colliders if not assigned manually
        if (partColliders == null || partColliders.Length == 0)
        {
            partColliders = GetComponentsInChildren<Collider>(true);
        }

        ResetPart();
    }

    private void Update()
    {
        if (!IsBroken) return;

        repairTimer -= Time.deltaTime;
        if (repairTimer <= 0f)
        {
            RepairPart();
        }
    }

    public void ApplyDamage(float amount, DamageSource source)
    {
        // Designer rule: broken/detached part takes zero damage
        if (IsBroken) return;

        // 1. Forward damage to global Boss Health / Shield
        if (bossHealth != null)
        {
            bossHealth.TakeDamage(amount, source, HitboxType.Part);
        }

        // 2. Accumulate break progress
        CurrentBreakProgress += amount;
        OnProgressChanged?.Invoke(CurrentBreakProgress, breakThreshold);

        // 3. Check break threshold
        if (CurrentBreakProgress >= breakThreshold)
        {
            BreakPart();
        }
    }

    private void BreakPart()
    {
        IsBroken = true;
        repairTimer = repairDuration;

        transform.localScale = Vector3.zero;

        // Flinch boss
        if (bossAnimator != null)
        {
            bossAnimator.SetTrigger(OnPartBreakHash);
        }

        // Disable colliders so bullets pass through empty socket
        SetCollidersActive(false);

        // Hide mesh if assigned
        if (visualMesh != null)
        {
            visualMesh.SetActive(false);
        }

        OnPartBroken?.Invoke(this);
    }

    private void RepairPart()
    {
        IsBroken = false;
        CurrentBreakProgress = 0f;
        repairTimer = 0f;

        transform.localScale = Vector3.one;

        // Re-enable colliders
        SetCollidersActive(true);

        // Restore mesh
        if (visualMesh != null)
        {
            visualMesh.SetActive(true);
        }

        OnProgressChanged?.Invoke(CurrentBreakProgress, breakThreshold);
        OnPartRepaired?.Invoke(this);
    }

    private void SetCollidersActive(bool active)
    {
        if (partColliders == null) return;

        for (int i = 0; i < partColliders.Length; i++)
        {
            if (partColliders[i] != null)
            {
                partColliders[i].enabled = active;
            }
        }
    }

    public void ResetPart()
    {
        IsBroken = false;
        CurrentBreakProgress = 0f;
        repairTimer = 0f;

        SetCollidersActive(true);

        if (visualMesh != null)
        {
            visualMesh.SetActive(true);
        }

        OnProgressChanged?.Invoke(CurrentBreakProgress, breakThreshold);
    }

    [ContextMenu("TEST: Break This Part (Deal 300 Dmg)")]
    private void TestBreakPart()
    {
        ApplyDamage(breakThreshold, DamageSource.Player);
    }
}