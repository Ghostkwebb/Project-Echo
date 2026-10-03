using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class BossHitbox : MonoBehaviour
{
    [Header("Hitbox Configuration")]
    [SerializeField] private HitboxType hitboxType = HitboxType.Part;

    [Header("Explicit References (Auto-cached if empty)")]
    [SerializeField] private BossPart linkedPart;
    [SerializeField] private BossHealth bossHealth;

    public HitboxType Type => hitboxType;
    public BossPart LinkedPart => linkedPart;

    private void Awake()
    {
        // Auto-find references up hierarchy if not manually dragged in Inspector
        if (bossHealth == null)
        {
            bossHealth = GetComponentInParent<BossHealth>();
        }

        if (hitboxType == HitboxType.Part && linkedPart == null)
        {
            linkedPart = GetComponentInParent<BossPart>();
        }
    }

    /// <summary>
    /// Universal entry point for weapon hitscan and projectiles.
    /// </summary>
    public void TakeHit(float damage, DamageSource source)
    {
        switch (hitboxType)
        {
            case HitboxType.Part:
                if (linkedPart != null)
                {
                    linkedPart.ApplyDamage(damage, source);
                }
                else if (bossHealth != null)
                {
                    // Fallback to body damage if no part attached
                    bossHealth.TakeDamage(damage, source, HitboxType.Body);
                }
                break;

            case HitboxType.Core:
                if (bossHealth != null)
                {
                    bossHealth.TakeDamage(damage, source, HitboxType.Core);
                }
                break;

            case HitboxType.Body:
                if (bossHealth != null)
                {
                    bossHealth.TakeDamage(damage, source, HitboxType.Body);
                }
                break;
        }
    }

    // Context menu helper for editor setup
    private void Reset()
    {
        bossHealth = GetComponentInParent<BossHealth>();
        linkedPart = GetComponentInParent<BossPart>();
    }
}