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
        // Ensure pool matches saved runs count
        while (spawnedGhosts.Count < savedRuns.Count)
        {
            if (echoGhostPrefab == null)
            {
                Debug.LogWarning("EchoManager: EchoGhostPrefab not assigned!");
                return;
            }

            GameObject ghostObj = Instantiate(echoGhostPrefab, Vector3.zero, Quaternion.identity, transform);
            EchoController controller = ghostObj.GetComponent<EchoController>();
            if (controller == null) controller = ghostObj.AddComponent<EchoController>();

            spawnedGhosts.Add(controller);
        }

        // Initialize and reset each ghost with its corresponding run data
        for (int i = 0; i < spawnedGhosts.Count; i++)
        {
            if (i < savedRuns.Count)
            {
                spawnedGhosts[i].Initialize(savedRuns[i], i + 1);
                spawnedGhosts[i].ResetForNewAttempt();
            }
            else
            {
                spawnedGhosts[i].gameObject.SetActive(false);
            }
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