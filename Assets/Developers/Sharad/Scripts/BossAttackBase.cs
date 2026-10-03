using System;
using UnityEngine;

public interface IBossAttack
{
    string AttackName { get; }
    bool CanExecute(Transform target);
    void ExecuteAttack(Transform target, Action onComplete);
    void InterruptAttack();
    bool IsExecuting { get; }
}

[DisallowMultipleComponent]
public abstract class BossAttackBase : MonoBehaviour, IBossAttack
{
    [Header("Attack Configuration")]
    [SerializeField] private string attackName = "Attack";
    [SerializeField] private float baseCooldown = 5.0f;
    [SerializeField] private float selectionWeight = 1.0f;

    [Header("Part Dependency (Optional)")]
    [Tooltip("If linked part breaks, this attack is locked out.")]
    [SerializeField] protected BossPart linkedPart;

    // State
    public string AttackName => attackName;
    public float SelectionWeight => selectionWeight;
    public bool IsExecuting { get; protected set; }
    public BossPart LinkedPart => linkedPart;

    protected float cooldownTimer;
    protected Action onAttackCompleteCallback;
    protected Transform activeTarget;

    protected virtual void Awake()
    {
        if (linkedPart == null)
        {
            linkedPart = GetComponentInParent<BossPart>();
        }
    }

    protected virtual void Update()
    {
        if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.deltaTime;
        }
    }

    public virtual bool CanExecute(Transform target)
    {
        if (IsExecuting) return false;
        if (cooldownTimer > 0f) return false;

        // Disqualify attack if linked physical part is broken
        if (linkedPart != null && linkedPart.IsBroken)
        {
            return false;
        }

        return target != null;
    }

    public void ExecuteAttack(Transform target, Action onComplete)
    {
        IsExecuting = true;
        activeTarget = target;
        onAttackCompleteCallback = onComplete;

        OnStartAttack(target);
    }

    public void InterruptAttack()
    {
        if (!IsExecuting) return;

        IsExecuting = false;
        OnInterrupt();

        // Start cooldown on interrupt so boss doesn't insta-spam
        cooldownTimer = baseCooldown * 0.5f;

        Action callback = onAttackCompleteCallback;
        onAttackCompleteCallback = null;
        callback?.Invoke();
    }

    protected void FinishAttack()
    {
        if (!IsExecuting) return;

        IsExecuting = false;
        cooldownTimer = baseCooldown;

        OnFinish();

        Action callback = onAttackCompleteCallback;
        onAttackCompleteCallback = null;
        callback?.Invoke();
    }

    // Abstract methods: Designer overrides these in concrete attacks
    protected abstract void OnStartAttack(Transform target);
    protected virtual void OnInterrupt() { }
    protected virtual void OnFinish() { }
}