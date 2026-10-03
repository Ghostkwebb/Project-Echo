using System;
using System.Collections.Generic;
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
    [Tooltip("Time in seconds before repair completes when debris flies back to socket.")]
    [SerializeField] private float recallDuration = 2.5f;

    [Header("Hitbox Colliders To Disable On Break")]
    [SerializeField] private Collider[] partColliders;

    [Header("Physical Debris On Break")]
    [SerializeField] private GameObject[] debrisPrefabs;
    [SerializeField] private float explosionForce = 7.5f;
    [SerializeField] private float upwardModifier = 1.5f;
    [SerializeField] private Material debrisMaterialOverride;

    [Header("Debris Scale & Volume")]
    [SerializeField] private int totalDebrisPieces = 14; // Spawns 14 chunks
    [SerializeField] private float debrisScale = 2.2f;    // Matches 2.5x Howitzer size

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
    private bool isRecalling;
    private readonly List<GameObject> activeDebris = new List<GameObject>();
    private readonly List<Vector3> debrisStartRecallPositions = new List<Vector3>();
    private static readonly int OnPartBreakHash = Animator.StringToHash("OnPartBreak");

    // Events
    public event Action<BossPart> OnPartBroken;
    public event Action<BossPart> OnPartRepaired;
    public event Action<float, float> OnProgressChanged;

    private void Awake()
    {
        bossHealth = GetComponentInParent<BossHealth>();
        bossAnimator = GetComponentInParent<Animator>();

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

        // Stage 1: Trigger Magnetic Recall (Last 2.5 seconds)
        if (repairTimer <= recallDuration && !isRecalling)
        {
            StartMagneticRecall();
        }

        // Stage 2: Pull debris toward socket
        if (isRecalling)
        {
            UpdateDebrisAttraction();
        }

        // Stage 3: Fully Repaired
        if (repairTimer <= 0f)
        {
            RepairPart();
        }
    }

    public void ApplyDamage(float amount, DamageSource source)
    {
        if (IsBroken) return;

        if (bossHealth != null)
        {
            bossHealth.TakeDamage(amount, source, HitboxType.Part);
        }

        CurrentBreakProgress += amount;
        OnProgressChanged?.Invoke(CurrentBreakProgress, breakThreshold);

        if (CurrentBreakProgress >= breakThreshold)
        {
            BreakPart();
        }
    }

    private void BreakPart()
    {
        IsBroken = true;
        isRecalling = false;
        repairTimer = repairDuration;

        if (bossAnimator != null)
        {
            bossAnimator.SetTrigger(OnPartBreakHash);
        }

        SetCollidersActive(false);
        transform.localScale = Vector3.zero;

        SpawnDebrisBurst();
        OnPartBroken?.Invoke(this);
    }

    private void StartMagneticRecall()
    {
        isRecalling = true;
        debrisStartRecallPositions.Clear();

        for (int i = 0; i < activeDebris.Count; i++)
        {
            if (activeDebris[i] != null)
            {
                // Disable physics so pieces can fly freely
                if (activeDebris[i].TryGetComponent<Rigidbody>(out var rb))
                {
                    rb.isKinematic = true;
                }
                if (activeDebris[i].TryGetComponent<Collider>(out var col))
                {
                    col.enabled = false;
                }

                debrisStartRecallPositions.Add(activeDebris[i].transform.position);
            }
            else
            {
                debrisStartRecallPositions.Add(transform.position);
            }
        }
    }

    private void UpdateDebrisAttraction()
    {
        float recallProgress = 1f - Mathf.Clamp01(repairTimer / recallDuration);
        // Exponential acceleration curve (starts floating, snaps fast into socket)
        float curve = recallProgress * recallProgress;

        Vector3 socketPos = transform.position;

        for (int i = 0; i < activeDebris.Count; i++)
        {
            if (activeDebris[i] != null && i < debrisStartRecallPositions.Count)
            {
                // Arc slightly upward then into socket
                Vector3 start = debrisStartRecallPositions[i];
                Vector3 current = Vector3.Lerp(start, socketPos, curve);
                current.y += Mathf.Sin(recallProgress * Mathf.PI) * 0.8f; // Floating arc

                activeDebris[i].transform.position = current;
                activeDebris[i].transform.Rotate(Vector3.up * (200f * Time.deltaTime));
            }
        }
    }

    private void RepairPart()
    {
        IsBroken = false;
        isRecalling = false;
        CurrentBreakProgress = 0f;
        repairTimer = 0f;

        transform.localScale = Vector3.one;
        SetCollidersActive(true);
        ClearActiveDebris();

        OnProgressChanged?.Invoke(CurrentBreakProgress, breakThreshold);
        OnPartRepaired?.Invoke(this);
    }

    private void SpawnDebrisBurst()
    {
        ClearActiveDebris();
        if (debrisPrefabs == null || debrisPrefabs.Length == 0) return;

        Vector3 spawnCenter = transform.position;

        for (int i = 0; i < totalDebrisPieces; i++)
        {
            // Cycle through the 6 prefab models
            GameObject prefab = debrisPrefabs[i % debrisPrefabs.Length];
            if (prefab == null) continue;

            Vector3 scatterOffset = UnityEngine.Random.insideUnitSphere * 0.6f;
            GameObject chunk = Instantiate(prefab, spawnCenter + scatterOffset, UnityEngine.Random.rotation);
            chunk.name = $"Debris_{partType}_{i}";

            // SCALE TO MATCH 2.5x MECH SIZE (with random size variation)
            float randomSize = UnityEngine.Random.Range(0.8f, 1.35f) * debrisScale;
            chunk.transform.localScale = Vector3.one * randomSize;

            if (debrisMaterialOverride != null)
            {
                Renderer rend = chunk.GetComponentInChildren<Renderer>();
                if (rend != null) rend.material = debrisMaterialOverride;
            }

            Collider col = chunk.GetComponent<Collider>();
            if (col == null)
            {
                MeshCollider mc = chunk.AddComponent<MeshCollider>();
                mc.convex = true;
            }

            Rigidbody rb = chunk.GetComponent<Rigidbody>();
            if (rb == null) rb = chunk.AddComponent<Rigidbody>();

            rb.mass = 4.0f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.linearDamping = 1.8f;   // Unity 6 API: Stops sliding across arena!
            rb.angularDamping = 2.0f;  // Stops endless rolling

            Vector3 explosionOrigin = spawnCenter - (transform.forward * 0.3f);
            rb.AddExplosionForce(explosionForce, explosionOrigin, 3.0f, upwardModifier, ForceMode.Impulse);
            rb.AddTorque(UnityEngine.Random.insideUnitSphere * 10f, ForceMode.Impulse);

            activeDebris.Add(chunk);
        }
    }

    private void ClearActiveDebris()
    {
        for (int i = 0; i < activeDebris.Count; i++)
        {
            if (activeDebris[i] != null) Destroy(activeDebris[i]);
        }
        activeDebris.Clear();
        debrisStartRecallPositions.Clear();
    }

    private void SetCollidersActive(bool active)
    {
        if (partColliders == null) return;
        for (int i = 0; i < partColliders.Length; i++)
        {
            if (partColliders[i] != null) partColliders[i].enabled = active;
        }
    }

    public void ResetPart()
    {
        IsBroken = false;
        isRecalling = false;
        CurrentBreakProgress = 0f;
        repairTimer = 0f;

        transform.localScale = Vector3.one;
        SetCollidersActive(true);
        ClearActiveDebris();

        OnProgressChanged?.Invoke(CurrentBreakProgress, breakThreshold);
    }

    [ContextMenu("TEST: Break This Part (Deal 300 Dmg)")]
    private void TestBreakPart()
    {
        ApplyDamage(breakThreshold, DamageSource.Player);
    }
}