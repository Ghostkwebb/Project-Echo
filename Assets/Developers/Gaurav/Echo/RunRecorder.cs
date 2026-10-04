using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class RunRecorder : MonoBehaviour
{
    [Header("Component References (Auto-detected if empty)")]
    [SerializeField] private WraithCombatLocomotion locomotion;
    [SerializeField] private WraithShooter shooter;
    [SerializeField] private PlayerHealth playerHealth;

    // State
    public bool IsRecording { get; private set; }
    private EchoData currentRunData;
    private float runTimer;

    // Input latches (Prevents dropped clicks between 50Hz fixed ticks)
    private bool latchedFire;
    private bool latchedJump;

    // Target preference tallies (GDD Section 7)
    private readonly Dictionary<BossPartType, float> partDamageTally = new Dictionary<BossPartType, float>();
    private float totalDamageThisRun;

    private void Awake()
    {
        if (locomotion == null) locomotion = GetComponent<WraithCombatLocomotion>();
        if (shooter == null) shooter = GetComponent<WraithShooter>();
        if (playerHealth == null) playerHealth = GetComponent<PlayerHealth>();
    }

    private void Start()
    {
        if (EncounterManager.Instance != null)
        {
            EncounterManager.Instance.OnAttemptStarted += StartNewRecording;
            EncounterManager.Instance.OnPlayerDied += StopAndFinalizeRecording;
        }

        // Start recording initial run
        StartNewRecording(1);
    }

    private void OnDestroy()
    {
        if (EncounterManager.Instance != null)
        {
            EncounterManager.Instance.OnAttemptStarted -= StartNewRecording;
            EncounterManager.Instance.OnPlayerDied -= StopAndFinalizeRecording;
        }
    }

    private void Update()
    {
        if (!IsRecording) return;

        // Latch discrete actions in Update() so 50Hz FixedUpdate never misses a click
        if (Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.leftButton.isPressed))
        {
            latchedFire = true;
        }

        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            latchedJump = true;
        }
    }

    private void FixedUpdate()
    {
        if (!IsRecording || currentRunData == null) return;

        runTimer += Time.fixedDeltaTime;

        // 1. Pack Action Bitmask
        EchoActionFlags actions = EchoActionFlags.None;
        if (latchedFire) actions |= EchoActionFlags.Fire;
        if (latchedJump) actions |= EchoActionFlags.Jump;
        if (locomotion != null && locomotion.IsSprinting) actions |= EchoActionFlags.Sprint;
        if (locomotion != null && locomotion.IsAiming) actions |= EchoActionFlags.ADS;

        // Clear latches for next tick
        latchedFire = false;
        latchedJump = false;

        // 2. Sample World Vectors
        Vector3 pos = transform.position;
        Quaternion rot = transform.rotation;
        Vector3 aimPoint = (shooter != null) ? shooter.CurrentTargetPoint : (pos + transform.forward * 50f);

        // 3. Write Snapshot into Buffer
        currentRunData.AddSnapshot(runTimer, pos, rot, aimPoint, actions);
    }

    public void StartNewRecording(int attemptNumber)
    {
        runTimer = 0f;
        latchedFire = false;
        latchedJump = false;
        totalDamageThisRun = 0f;
        partDamageTally.Clear();

        currentRunData = new EchoData
        {
            AttemptNumber = attemptNumber,
            PreferredTargetPart = BossPartType.RightArmCannon
        };

        IsRecording = true;
    }

    public void StopAndFinalizeRecording(int attemptNumber)
    {
        if (!IsRecording || currentRunData == null) return;

        IsRecording = false;
        currentRunData.TotalLifespan = runTimer;
        currentRunData.TotalDamageDealt = totalDamageThisRun;
        currentRunData.PreferredTargetPart = CalculatePreferredPart();

        // Ship finished run to central EchoManager
        if (EchoManager.Instance != null)
        {
            EchoManager.Instance.RegisterCompletedRun(currentRunData);
        }

        currentRunData = null;
    }

    /// <summary>
    /// Call from weapon when bullet hits a boss part to track target preference.
    /// </summary>
    public void RecordDamageDealt(BossPartType partHit, float amount)
    {
        if (!IsRecording) return;

        totalDamageThisRun += amount;

        if (!partDamageTally.ContainsKey(partHit))
            partDamageTally[partHit] = 0f;

        partDamageTally[partHit] += amount;
    }

    private BossPartType CalculatePreferredPart()
    {
        BossPartType topPart = BossPartType.RightArmCannon;
        float maxDmg = -1f;

        foreach (var pair in partDamageTally)
        {
            if (pair.Value > maxDmg)
            {
                maxDmg = pair.Value;
                topPart = pair.Key;
            }
        }

        return topPart;
    }
}