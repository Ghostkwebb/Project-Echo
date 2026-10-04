using System;
using UnityEngine;
using System.Collections;

public enum DamageSource
{
    Player,
    Echo
}

public enum HitboxType
{
    Body,
    Part,
    Core
}

[DisallowMultipleComponent]
public class BossHealth : MonoBehaviour
{
    [Header("Health & Shield Pools")]
    [SerializeField] private float maxHealth = 1500f;
    [SerializeField] private float maxShield = 1000f;

    [Header("Stagger / Vulnerability Window")]
    [SerializeField] private float staggerDuration = 5.0f;
    [SerializeField] private float coreCriticalMultiplier = 2.0f;

    [Header("Shield Active Multipliers")]
    [SerializeField] private float playerShieldMult = 1.0f;
    [SerializeField] private float playerHealthChipMult = 0.1f;
    [SerializeField] private float echoShieldMult = 0.6f;

    [Header("Stagger Phase Multipliers")]
    [SerializeField] private float playerStaggerHealthMult = 1.0f;
    [SerializeField] private float echoStaggerHealthMult = 0.45f;

    [Header("References")]
    [SerializeField] private Animator bossAnimator;

    [Header("Hit Flash Feedback")]
    [SerializeField] private SkinnedMeshRenderer bossMeshRenderer;
    private Color[] originalBaseColors;
    private Coroutine flashRoutine;
    private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");

    // State
    public float CurrentHealth { get; private set; }
    public float CurrentShield { get; private set; }
    public bool IsStaggered { get; private set; }
    public bool IsDead { get; private set; }

    private float staggerTimer;

    // Animator Hashes
    private static readonly int IsStaggeredHash = Animator.StringToHash("IsStaggered");
    private static readonly int OnDeathHash = Animator.StringToHash("OnDeath");

    // Events for UI & Audio
    public event Action<float, float> OnHealthChanged;
    public event Action<float, float> OnShieldChanged;
    public event Action OnStaggerStarted;
    public event Action OnStaggerEnded;
    public event Action OnBossDefeated;

    private void Awake()
    {
        if (bossAnimator == null)
            bossAnimator = GetComponentInChildren<Animator>();

        if (bossMeshRenderer == null)
            bossMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();

        // Cache original colors for clean restoration
        if (bossMeshRenderer != null)
        {
            Material[] mats = bossMeshRenderer.materials;
            originalBaseColors = new Color[mats.Length];
            for (int i = 0; i < mats.Length; i++)
            {
                originalBaseColors[i] = mats[i].GetColor(BaseColorID);
            }
        }

        ResetBoss();
    }

    private void Update()
    {
        if (IsDead || !IsStaggered) return;

        staggerTimer -= Time.deltaTime;
        if (staggerTimer <= 0f)
        {
            EndStagger();
        }
    }

    public void TakeDamage(float rawDamage, DamageSource source, HitboxType hitboxType)
    {
        if (IsDead) return;

        // Visual Hit Flash on EVERY hit (Red for body, Gold for Rabbit Core!)
        Color flashCol = (hitboxType == HitboxType.Core) ? new Color(1f, 0.9f, 0.2f) : new Color(1f, 0.15f, 0.15f);
        TriggerHitFlash(flashCol);

        if (IsStaggered)
        {
            ApplyStaggerDamage(rawDamage, source, hitboxType);
        }
        else
        {
            ApplyShieldedDamage(rawDamage, source);
        }
    }

    private void ApplyShieldedDamage(float rawDamage, DamageSource source)
    {
        if (source == DamageSource.Player)
        {
            float shieldDmg = rawDamage * playerShieldMult;
            float chipDmg = rawDamage * playerHealthChipMult;

            ReduceShield(shieldDmg);
            ReduceHealth(chipDmg, DamageSource.Player);
        }
        else // Echo
        {
            float shieldDmg = rawDamage * echoShieldMult;
            ReduceShield(shieldDmg);
            // Echo deals ZERO health damage while shield is active (GDD Section 8)
        }
    }

    private void ApplyStaggerDamage(float rawDamage, DamageSource source, HitboxType hitboxType)
    {
        float damage = rawDamage;

        // Flash Red on body hit, Flash Gold on Rabbit Core critical hit!
        Color flashCol = (hitboxType == HitboxType.Core) ? new Color(2.5f, 2.0f, 0.5f) : new Color(2.0f, 0.1f, 0.1f);
        TriggerHitFlash(flashCol);

        // Core weakpoint bonus
        if (hitboxType == HitboxType.Core)
        {
            damage *= coreCriticalMultiplier;
        }

        if (source == DamageSource.Player)
        {
            damage *= playerStaggerHealthMult;
        }
        else // Echo
        {
            damage *= echoStaggerHealthMult;
        }

        ReduceHealth(damage, source);
    }

    private void ReduceShield(float amount)
    {
        if (CurrentShield <= 0f) return;

        CurrentShield = Mathf.Max(0f, CurrentShield - amount);
        OnShieldChanged?.Invoke(CurrentShield, maxShield);

        // Discard spillover: hitting 0 shield triggers stagger gate
        if (CurrentShield <= 0f)
        {
            StartStagger();
        }
    }

    private void ReduceHealth(float amount, DamageSource source)
    {
        if (IsDead) return;

        float targetHealth = CurrentHealth - amount;

        // GDD Section 26: Final blow MUST come from live player. Echo locks at 1 HP.
        if (source == DamageSource.Echo && targetHealth <= 1f)
        {
            CurrentHealth = Mathf.Max(1f, CurrentHealth);
        }
        else
        {
            CurrentHealth = Mathf.Max(0f, targetHealth);
        }

        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);

        if (CurrentHealth <= 0f && source == DamageSource.Player)
        {
            Die();
        }
    }

    private void StartStagger()
    {
        IsStaggered = true;
        staggerTimer = staggerDuration;

        if (bossAnimator != null)
        {
            bossAnimator.SetBool(IsStaggeredHash, true);
        }

        OnStaggerStarted?.Invoke();
    }

    private void EndStagger()
    {
        IsStaggered = false;
        CurrentShield = maxShield;

        if (bossAnimator != null)
        {
            bossAnimator.SetBool(IsStaggeredHash, false);
        }

        OnShieldChanged?.Invoke(CurrentShield, maxShield);
        OnStaggerEnded?.Invoke();
    }

    private void Die()
    {
        IsDead = true;
        IsStaggered = false;

        if (bossAnimator != null)
        {
            bossAnimator.SetBool(IsStaggeredHash, false);
            bossAnimator.SetTrigger(OnDeathHash);
        }

        OnBossDefeated?.Invoke();
    }

    public void ResetBoss()
    {
        IsDead = false;
        IsStaggered = false;
        staggerTimer = 0f;

        CurrentHealth = maxHealth;
        CurrentShield = maxShield;

        if (bossAnimator != null)
        {
            bossAnimator.SetBool(IsStaggeredHash, false);
            bossAnimator.Play("Idle", 0, 0f);
        }

        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        OnShieldChanged?.Invoke(CurrentShield, maxShield);
    }

    private void TriggerHitFlash(Color color)
    {
        if (bossMeshRenderer == null || originalBaseColors == null) return;
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(HitFlashRoutine(color));
    }

    private IEnumerator HitFlashRoutine(Color color)
    {
        Material[] mats = bossMeshRenderer.materials;
        for (int i = 0; i < mats.Length; i++)
        {
            mats[i].SetColor(BaseColorID, color);
        }

        yield return new WaitForSeconds(0.06f);

        // Restore exact original colors
        for (int i = 0; i < mats.Length; i++)
        {
            if (i < originalBaseColors.Length)
            {
                mats[i].SetColor(BaseColorID, originalBaseColors[i]);
            }
        }
        flashRoutine = null;
    }
}