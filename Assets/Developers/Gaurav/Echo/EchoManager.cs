using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class EchoManager : MonoBehaviour
{
    public static EchoManager Instance { get; private set; }

    [Header("Echo Squad Capacity (GDD 21)")]
    [Tooltip("Maximum active Echoes in the arena simultaneously.")]
    [Range(1, 10)]
    [SerializeField] private int maxActiveEchoes = 5;

    [Header("Ghost Prefab")]
    [Tooltip("Prefab containing EchoController + ghost material.")]
    [SerializeField] private GameObject echoGhostPrefab;

    // Run history & active ghost instances
    private readonly List<EchoData> savedRuns = new List<EchoData>();
    private readonly List<EchoController> spawnedGhosts = new List<EchoController>();

    public int ActiveEchoCount => savedRuns.Count;
    public int MaxCapacity => maxActiveEchoes;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        if (EncounterManager.Instance != null)
        {
            EncounterManager.Instance.OnAttemptStarted += HandleAttemptStarted;
            EncounterManager.Instance.OnPlayerDied += HandlePlayerDied;
            EncounterManager.Instance.OnVictory += HandleVictory;
        }
    }

    /// <summary>
    /// Called by RunRecorder when live player dies.
    /// Stores life into FIFO history (GDD Section 21).
    /// </summary>
    public void RegisterCompletedRun(EchoData runData)
    {
        if (runData == null || runData.Snapshots.Count < 10) return; // Discard sub-second empty runs

        // Enforce FIFO Cap: If exceeds max (5-6), remove oldest
        if (savedRuns.Count >= maxActiveEchoes)
        {
            savedRuns.RemoveAt(0);
        }

        savedRuns.Add(runData);
    }

    private void HandleAttemptStarted(int attemptNumber)
    {
        // Spawns and synchronizes all past lives at T = 0 (GDD Section 2)
        SyncAndSpawnEchoSquad();
    }

    private void HandlePlayerDied(int attemptNumber)
    {
        // Deactivate all active ghosts for the reset wipe
        DeactivateAllGhosts();
    }

    private void HandleVictory()
    {
        // GDD Section 26: Echoes stop and disappear on boss defeat
        DeactivateAllGhosts();
    }

    private void SyncAndSpawnEchoSquad()
    {
        while (spawnedGhosts.Count < savedRuns.Count)
        {
            if (echoGhostPrefab == null) return;

            GameObject ghostObj = Instantiate(echoGhostPrefab, Vector3.zero, Quaternion.identity, transform);
            EchoController controller = ghostObj.GetComponent<EchoController>();
            if (controller == null) controller = ghostObj.AddComponent<EchoController>();

            spawnedGhosts.Add(controller);
        }

        Transform spawnPoint = EncounterManager.Instance != null ? EncounterManager.Instance.transform : transform;

        for (int i = 0; i < spawnedGhosts.Count; i++)
        {
            if (i < savedRuns.Count)
            {
                // Calculate Tactical V-Formation Offset (rotated to match spawn facing)
                Vector3 localOffset = GetSquadFormationOffset(i + 1);
                Vector3 worldFormationOffset = spawnPoint.rotation * localOffset;

                spawnedGhosts[i].Initialize(savedRuns[i], i + 1, worldFormationOffset);
                spawnedGhosts[i].ResetForNewAttempt();
            }
            else
            {
                spawnedGhosts[i].gameObject.SetActive(false);
            }
        }
    }

    private Vector3 GetSquadFormationOffset(int squadIndex)
    {
        switch (squadIndex)
        {
            case 1: return new Vector3(-2.2f, 0f, -0.8f); // Left Flank Wing
            case 2: return new Vector3(2.2f, 0f, -0.8f); // Right Flank Wing
            case 3: return new Vector3(-3.8f, 0f, -2.0f); // Far Left Rear
            case 4: return new Vector3(3.8f, 0f, -2.0f); // Far Right Rear
            case 5: return new Vector3(0.0f, 0f, -3.2f); // Rear Anchor
            default: return new Vector3(Random.Range(-2f, 2f), 0f, -2f);
        }
    }

    private void DeactivateAllGhosts()
    {
        for (int i = 0; i < spawnedGhosts.Count; i++)
        {
            if (spawnedGhosts[i] != null)
            {
                spawnedGhosts[i].gameObject.SetActive(false);
            }
        }
    }

    public void ClearAllEchoHistory()
    {
        savedRuns.Clear();
        DeactivateAllGhosts();
    }
}