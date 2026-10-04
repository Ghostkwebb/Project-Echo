using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BossThreatMonitor : MonoBehaviour
{
    private struct DamageRecord
    {
        public float Timestamp;
        public int AttackerId; // 0 = Player, 1..6 = Echoes
        public Transform AttackerTransform;
        public float Amount;
    }

    [Header("Anti-Passive Thresholds (GDD 18)")]
    [Tooltip("Time window in seconds to calculate contribution ratio.")]
    [SerializeField] private float rollingWindow = 8.0f;

    [Tooltip("If player deals less than this % of total damage, flag as passive.")]
    [Range(0.05f, 0.5f)]
    [SerializeField] private float passiveRatioThreshold = 0.20f; // 20%

    [Tooltip("Minimum damage Echoes must deal before boss cares about passive player.")]
    [SerializeField] private float minEchoDamageRequired = 100f;

    [Header("References")]
    [SerializeField] private BossAimController aimController;
    [SerializeField] private Transform defaultPlayerTransform;

    // State
    public bool IsPlayerPassive { get; private set; }
    public int PrimaryThreatEchoId { get; private set; } = -1;
    public Transform CurrentThreatTarget { get; private set; }
    public float CurrentPlayerContribution { get; private set; } = 1.0f;

    private readonly List<DamageRecord> damageHistory = new List<DamageRecord>();
    private readonly Dictionary<int, float> echoWindowTotals = new Dictionary<int, float>();
    private readonly Dictionary<int, Transform> echoTransformMap = new Dictionary<int, Transform>();

    // Events for UI banners (e.g. "PRIMARY THREAT: ECHO 02")
    public event Action<int, Transform> OnPrimaryThreatSelected;
    public event Action OnPlayerReEngaged;

    private void Awake()
    {
        if (aimController == null)
            aimController = GetComponent<BossAimController>();

        FindPlayerIfNull();
    }

    private void Update()
    {
        // If current targeted Echo died or despawned -> SNAP BACK TO PLAYER IMMEDIATELY!
        if (IsPlayerPassive && (CurrentThreatTarget == null || !CurrentThreatTarget.gameObject.activeInHierarchy))
        {
            ClearAntiPassive();
        }

        PurgeOldRecords();
        EvaluateThreat();
    }

    /// <summary>
    /// Call this from weapon hit or BossHealth whenever damage is dealt.
    /// attackerId: 0 for Player, 1+ for Echoes.
    /// </summary>
    public void RecordDamage(int attackerId, Transform attackerTransform, float amount)
    {
        damageHistory.Add(new DamageRecord
        {
            Timestamp = Time.time,
            AttackerId = attackerId,
            AttackerTransform = attackerTransform,
            Amount = amount
        });

        if (attackerId > 0 && attackerTransform != null)
        {
            echoTransformMap[attackerId] = attackerTransform;
        }

        // Cache player transform
        if (attackerId == 0 && attackerTransform != null)
        {
            defaultPlayerTransform = attackerTransform;
        }
    }

    private void PurgeOldRecords()
    {
        float cutoff = Time.time - rollingWindow;
        for (int i = damageHistory.Count - 1; i >= 0; i--)
        {
            if (damageHistory[i].Timestamp < cutoff)
            {
                damageHistory.RemoveAt(i);
            }
        }
    }

    private void EvaluateThreat()
    {
        float playerDmg = 0f;
        float echoTotalDmg = 0f;
        echoWindowTotals.Clear();

        for (int i = 0; i < damageHistory.Count; i++)
        {
            var record = damageHistory[i];
            if (record.AttackerId == 0)
            {
                playerDmg += record.Amount;
            }
            else
            {
                echoTotalDmg += record.Amount;
                if (!echoWindowTotals.ContainsKey(record.AttackerId))
                    echoWindowTotals[record.AttackerId] = 0f;

                echoWindowTotals[record.AttackerId] += record.Amount;
            }
        }

        float totalDmg = playerDmg + echoTotalDmg;
        CurrentPlayerContribution = totalDmg > 0.001f ? (playerDmg / totalDmg) : 1.0f;

        // Condition 1: Player slacking (< 20% DPS) AND Echoes are doing real work
        if (totalDmg > 0f && echoTotalDmg >= minEchoDamageRequired && CurrentPlayerContribution < passiveRatioThreshold)
        {
            TriggerAntiPassive();
        }
        // Condition 2: Player re-engaged and fighting
        else if (IsPlayerPassive && (CurrentPlayerContribution >= passiveRatioThreshold || echoTotalDmg < minEchoDamageRequired))
        {
            ClearAntiPassive();
        }
    }

    private void TriggerAntiPassive()
    {
        // Find highest-DPS Echo in the window
        int topEchoId = -1;
        float highestDmg = -1f;

        foreach (var pair in echoWindowTotals)
        {
            if (pair.Value > highestDmg)
            {
                highestDmg = pair.Value;
                topEchoId = pair.Key;
            }
        }

        if (topEchoId == -1) return;

        // Update threat state
        IsPlayerPassive = true;
        PrimaryThreatEchoId = topEchoId;
        echoTransformMap.TryGetValue(topEchoId, out Transform echoTransform);
        CurrentThreatTarget = echoTransform;

        // Redirect boss aim to the dangerous Echo!
        if (aimController != null && CurrentThreatTarget != null)
        {
            aimController.SetTarget(CurrentThreatTarget);
        }

        OnPrimaryThreatSelected?.Invoke(PrimaryThreatEchoId, CurrentThreatTarget);
    }

    private void ClearAntiPassive()
    {
        IsPlayerPassive = false;
        PrimaryThreatEchoId = -1;
        CurrentThreatTarget = defaultPlayerTransform;

        // Boss returns focus to live player
        if (aimController != null && defaultPlayerTransform != null)
        {
            aimController.SetTarget(defaultPlayerTransform);
        }

        OnPlayerReEngaged?.Invoke();
    }

    public void ResetThreat()
    {
        damageHistory.Clear();
        echoWindowTotals.Clear();
        echoTransformMap.Clear();
        IsPlayerPassive = false;
        PrimaryThreatEchoId = -1;
        CurrentThreatTarget = defaultPlayerTransform;
        CurrentPlayerContribution = 1.0f;

        if (aimController != null && defaultPlayerTransform != null)
        {
            aimController.SetTarget(defaultPlayerTransform);
        }
    }

    private void FindPlayerIfNull()
    {
        if (defaultPlayerTransform == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) defaultPlayerTransform = p.transform;
        }
    }
}