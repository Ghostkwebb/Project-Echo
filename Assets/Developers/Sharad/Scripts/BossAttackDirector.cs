using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BossAttackDirector : MonoBehaviour
{
    private enum DirectorState
    {
        Resting,
        ExecutingAttack,
        Staggered,
        Defeated
    }

    [Header("Rhythm Tuning")]
    [Tooltip("Pause duration between attacks (safe window for player).")]
    [SerializeField] private float minRestDuration = 1.5f;
    [SerializeField] private float maxRestDuration = 2.5f;

    [Header("Attack Slots (Auto-detected if empty)")]
    [SerializeField] private List<BossAttackBase> attackSlots = new List<BossAttackBase>();

    [Header("Behavior Tuning")]
    [Tooltip("Prevents boss from executing the same attack twice in a row.")]
    [SerializeField] private bool preventConsecutiveRepeats = true;

    // References
    private BossHealth bossHealth;
    private BossAimController aimController;

    // State
    private DirectorState currentState = DirectorState.Resting;
    private float restTimer;
    private BossAttackBase activeAttack;
    private BossAttackBase lastExecutedAttack;
    public bool IsExecutingAttack => currentState == DirectorState.ExecutingAttack;

    private void Awake()
    {
        bossHealth = GetComponent<BossHealth>();
        aimController = GetComponent<BossAimController>();

        // Auto-find all attack components on Howitzer hierarchy if empty
        if (attackSlots.Count == 0)
        {
            attackSlots.AddRange(GetComponentsInChildren<BossAttackBase>(true));
        }
    }

    private void OnEnable()
    {
        if (bossHealth != null)
        {
            bossHealth.OnStaggerStarted += HandleStaggerStarted;
            bossHealth.OnStaggerEnded += HandleStaggerEnded;
            bossHealth.OnBossDefeated += HandleBossDefeated;
        }
    }

    private void OnDisable()
    {
        if (bossHealth != null)
        {
            bossHealth.OnStaggerStarted -= HandleStaggerStarted;
            bossHealth.OnStaggerEnded -= HandleStaggerEnded;
            bossHealth.OnBossDefeated -= HandleBossDefeated;
        }
    }

    private void Start()
    {
        ResetDirector();
    }

    private void Update()
    {
        if (currentState != DirectorState.Resting) return;

        restTimer -= Time.deltaTime;
        if (restTimer <= 0f)
        {
            TryExecuteNextAttack();
        }
    }

    private void TryExecuteNextAttack()
    {
        Transform target = aimController != null ? aimController.CurrentTarget : null;
        if (target == null)
        {
            restTimer = 0.5f; // Wait and search target again
            return;
        }

        // 1. Query available attacks (filters out broken parts & cooldowns)
        List<BossAttackBase> validAttacks = GetAvailableAttacks(target);

        if (validAttacks.Count == 0)
        {
            // All attacks on cooldown or all parts broken → rest briefly
            restTimer = 0.8f;
            return;
        }

        // 2. Filter consecutive repeat if possible
        if (preventConsecutiveRepeats && validAttacks.Count > 1 && lastExecutedAttack != null)
        {
            validAttacks.Remove(lastExecutedAttack);
        }

        // 3. Weighted selection
        BossAttackBase chosenAttack = PickWeightedAttack(validAttacks);
        if (chosenAttack == null) return;

        // 4. Execute attack
        ExecuteAttack(chosenAttack, target);
    }

    private void ExecuteAttack(BossAttackBase attack, Transform target)
    {
        currentState = DirectorState.ExecutingAttack;
        activeAttack = attack;
        lastExecutedAttack = attack;

        attack.ExecuteAttack(target, HandleAttackCompleted);
    }

    private void HandleAttackCompleted()
    {
        if (currentState == DirectorState.Staggered || currentState == DirectorState.Defeated)
        {
            return;
        }

        activeAttack = null;
        currentState = DirectorState.Resting;
        restTimer = Random.Range(minRestDuration, maxRestDuration);

        // Ensure aim unlocks when attack finishes
        if (aimController != null)
        {
            aimController.SetAimLocked(false);
        }
    }

    private void HandleStaggerStarted()
    {
        currentState = DirectorState.Staggered;

        // Interrupt active attack immediately (GDD Section 14)
        if (activeAttack != null)
        {
            activeAttack.InterruptAttack();
            activeAttack = null;
        }

        if (aimController != null)
        {
            aimController.SetAimLocked(false);
        }
    }

    private void HandleStaggerEnded()
    {
        currentState = DirectorState.Resting;
        restTimer = 0.5f; // Brief delay before boss starts attacking again
    }

    private void HandleBossDefeated()
    {
        currentState = DirectorState.Defeated;

        if (activeAttack != null)
        {
            activeAttack.InterruptAttack();
            activeAttack = null;
        }

        enabled = false;
    }

    private List<BossAttackBase> GetAvailableAttacks(Transform target)
    {
        List<BossAttackBase> valid = new List<BossAttackBase>();
        for (int i = 0; i < attackSlots.Count; i++)
        {
            if (attackSlots[i] != null && attackSlots[i].CanExecute(target))
            {
                valid.Add(attackSlots[i]);
            }
        }
        return valid;
    }

    private BossAttackBase PickWeightedAttack(List<BossAttackBase> attacks)
    {
        float totalWeight = 0f;
        for (int i = 0; i < attacks.Count; i++)
        {
            totalWeight += attacks[i].SelectionWeight;
        }

        if (totalWeight <= 0f) return attacks[0];

        float roll = Random.Range(0f, totalWeight);
        float cumulative = 0f;

        for (int i = 0; i < attacks.Count; i++)
        {
            cumulative += attacks[i].SelectionWeight;
            if (roll <= cumulative)
            {
                return attacks[i];
            }
        }

        return attacks[0];
    }

    public void RegisterAttackSlot(BossAttackBase attack)
    {
        if (attack != null && !attackSlots.Contains(attack))
        {
            attackSlots.Add(attack);
        }
    }

    public void ResetDirector()
    {
        currentState = DirectorState.Resting;
        activeAttack = null;
        lastExecutedAttack = null;
        restTimer = Random.Range(minRestDuration, maxRestDuration);
    }
}