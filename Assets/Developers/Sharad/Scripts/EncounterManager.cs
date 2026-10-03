using System;
using System.Collections;
using UnityEngine;

public enum EncounterState
{
    InCombat,
    PlayerDied,
    BossDefeated
}

[DisallowMultipleComponent]
public class EncounterManager : MonoBehaviour
{
    public static EncounterManager Instance { get; private set; }

    [Header("Encounter Settings")]
    [SerializeField] private float respawnDelay = 1.5f;

    [Header("Player References")]
    [Tooltip("Drag the spawn point transform where player restarts.")]
    [SerializeField] private Transform playerSpawnPoint;
    [Tooltip("Drag the player or test cube.")]
    [SerializeField] private Transform playerTransform;

    [Header("Boss References (Auto-detected if empty)")]
    [SerializeField] private BossHealth bossHealth;
    [SerializeField] private BossAttackDirector attackDirector;
    [SerializeField] private BossThreatMonitor threatMonitor;
    [SerializeField] private BossPart[] bossParts;

    // State
    public int CurrentAttempt { get; private set; } = 1;
    public EncounterState State { get; private set; } = EncounterState.InCombat;

    // Events (UI & Echo Manager hook into these)
    public event Action<int> OnAttemptStarted; // int attemptNumber
    public event Action<int> OnPlayerDied;     // int attemptNumber
    public event Action OnVictory;

    private void Awake()
    {
        // Safe singleton pattern for Unity 6 Fast Enter Play Mode
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        AutoFindBossReferences();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        if (bossHealth != null)
        {
            bossHealth.OnBossDefeated += HandleBossDefeated;
        }

        StartNewAttempt();
    }

    /// <summary>
    /// Call when player HP hits 0.
    /// </summary>
    public void TriggerPlayerDeath()
    {
        if (State != EncounterState.InCombat) return;

        State = EncounterState.PlayerDied;
        OnPlayerDied?.Invoke(CurrentAttempt);

        StartCoroutine(RespawnSequenceRoutine());
    }

    private IEnumerator RespawnSequenceRoutine()
    {
        // Brief pause for death feedback / black fade
        yield return new WaitForSeconds(respawnDelay);

        CurrentAttempt++;
        ResetEncounter();
        StartNewAttempt();
    }

    public void ResetEncounter()
    {
        // 1. Reset Boss Health & Shield (GDD Section 2 rule 6)
        if (bossHealth != null)
        {
            bossHealth.ResetBoss();
        }

        // 2. Reset All 4 Boss Parts (Restores scale & re-enables colliders)
        if (bossParts != null)
        {
            for (int i = 0; i < bossParts.Length; i++)
            {
                if (bossParts[i] != null)
                {
                    bossParts[i].ResetPart();
                }
            }
        }

        // 3. Reset Director (Puts attacks on initial cooldown)
        if (attackDirector != null)
        {
            attackDirector.ResetDirector();
        }

        // 4. Reset Threat Monitor (Clears rolling DPS window)
        if (threatMonitor != null)
        {
            threatMonitor.ResetThreat();
        }

        // 5. Snap Player back to Spawn Point (GDD Section 2 rule 7)
        if (playerTransform != null && playerSpawnPoint != null)
        {
            playerTransform.position = playerSpawnPoint.position;
            playerTransform.rotation = playerSpawnPoint.rotation;

            // Reset linear velocity if Rigidbody is attached
            if (playerTransform.TryGetComponent<Rigidbody>(out var rb))
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }

    private void StartNewAttempt()
    {
        State = EncounterState.InCombat;
        OnAttemptStarted?.Invoke(CurrentAttempt);
    }

    private void HandleBossDefeated()
    {
        State = EncounterState.BossDefeated;
        OnVictory?.Invoke();
    }

    private void AutoFindBossReferences()
    {
        // Unity 6.6 API: FindAnyObjectByType & parameterless FindObjectsByType
        if (bossHealth == null) bossHealth = FindAnyObjectByType<BossHealth>();
        if (attackDirector == null) attackDirector = FindAnyObjectByType<BossAttackDirector>();
        if (threatMonitor == null) threatMonitor = FindAnyObjectByType<BossThreatMonitor>();
        if (bossParts == null || bossParts.Length == 0)
        {
            bossParts = FindObjectsByType<BossPart>();
        }

        if (playerTransform == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTransform = p.transform;
        }
    }

    // ==========================================
    // 1-CLICK TEST BUTTON FOR INSPECTOR
    // ==========================================
    [ContextMenu("TEST: Kill Player & Reset Encounter")]
    private void TestKillAndReset()
    {
        TriggerPlayerDeath();
    }
}