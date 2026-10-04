using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class BossProximityStomp : MonoBehaviour
{
    [Header("Proximity & Timing (GDD 4)")]
    [SerializeField] private float triggerDistance = 7.5f;
    [SerializeField] private float stompRadius = 8.5f;
    [SerializeField] private float cooldownDuration = 8.0f;

    [Header("Damage & Knockback")]
    [SerializeField] private float damage = 45f;
    [SerializeField] private float knockbackForce = 18f;
    [SerializeField] private LayerMask targetLayers;

    [Header("Jump Physics & Timing")]
    [SerializeField] private float jumpHeight = 4.5f;   // Higher apex for 6m boss
    [SerializeField] private float riseTime = 0.40f;    // Time to reach peak
    [SerializeField] private float hangTime = 0.40f;    // Real hover in sky
    [SerializeField] private float slamTime = 0.18f;    // Violent fast crash

    [Header("Telegraph Visual")]
    [Tooltip("Optional ground ring transform. If empty, creates one automatically.")]
    [SerializeField] private Transform telegraphRing;

    [Header("Impact Shockwave FX")]
    [Tooltip("Drag SM_Shockwave_Disc.fbx here.")]
    [SerializeField] private GameObject shockwaveMeshPrefab;
    [Tooltip("Drag M_Shockwave_Toon material here.")]
    [SerializeField] private Material shockwaveMaterial;

    [Tooltip("Drag M_Telegraph_Holo material asset here.")]
    [SerializeField] private Material telegraphMaterial;

    // References
    private BossHealth bossHealth;
    private Animator bossAnimator;
    private BossAttackDirector attackDirector;

    // State
    public bool IsStomping { get; private set; }
    private float cooldownTimer;
    private readonly Collider[] hitBuffer = new Collider[16];
    private static readonly int OnStompHash = Animator.StringToHash("OnStomp");

    private void Awake()
    {
        bossHealth = GetComponent<BossHealth>();
        bossAnimator = GetComponentInChildren<Animator>();
        attackDirector = GetComponent<BossAttackDirector>();

        // Default targets Player and Echo layers
        if (targetLayers.value == 0)
        {
            targetLayers = LayerMask.GetMask("Player", "Echo", "Default");
        }

        CreateTelegraphRingIfNull();
    }

    private void Update()
    {
        if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;

        if (IsStomping) return;
        if (cooldownTimer > 0f) return;
        if (bossHealth != null && (bossHealth.IsDead || bossHealth.IsStaggered)) return;

        if (attackDirector != null && attackDirector.IsExecutingAttack) return;

        CheckProximityTrigger();
    }
    private void CheckProximityTrigger()
    {
        // Non-alloc check for players or echoes in danger zone
        int hits = Physics.OverlapSphereNonAlloc(transform.position, triggerDistance, hitBuffer, targetLayers);
        for (int i = 0; i < hits; i++)
        {
            Transform t = hitBuffer[i].transform;
            if (t.CompareTag("Player") || t.gameObject.layer == LayerMask.NameToLayer("Echo"))
            {
                StartCoroutine(ExecuteStompRoutine());
                return;
            }
        }
    }

    private IEnumerator ExecuteStompRoutine()
    {
        IsStomping = true;
        cooldownTimer = cooldownDuration;
        float baseY = transform.position.y;
        float totalWarningTime = riseTime + hangTime;

        // Position telegraph ring on floor
        if (telegraphRing != null)
        {
            telegraphRing.gameObject.SetActive(true);
            telegraphRing.position = new Vector3(transform.position.x, baseY + 0.02f, transform.position.z);
            telegraphRing.localScale = Vector3.zero;
        }

        // 1. Play Jump Up
        if (bossAnimator != null)
        {
            bossAnimator.Play("jump_up", 0, 0f);
            BossAudio.Instance?.PlayStompJump();
        }

        // ==========================================
        // STAGE 1: ROCKET LAUNCH (0.35s)
        // ==========================================
        float t = 0f;
        while (t < riseTime)
        {
            if (CheckStaggerAbort(baseY)) yield break;

            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / riseTime);

            // Fast explosive launch
            float currentY = baseY + Mathf.Sin(p * Mathf.PI * 0.5f) * jumpHeight;
            transform.position = new Vector3(transform.position.x, currentY, transform.position.z);

            UpdateRingScale(p * (riseTime / totalWarningTime));
            yield return null;
        }

        // ==========================================
        // STAGE 2: MENACING HANG + WINDUP HITCH (0.40s)
        // ==========================================
        t = 0f;
        while (t < hangTime)
        {
            if (CheckStaggerAbort(baseY)) yield break;

            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / hangTime);

            // Last 25% of hang: pulls knees up extra +0.4m (anticipation hammer hitch)
            float hitch = p > 0.75f ? Mathf.Sin((p - 0.75f) / 0.25f * Mathf.PI * 0.5f) * 0.4f : 0f;
            transform.position = new Vector3(transform.position.x, baseY + jumpHeight + hitch, transform.position.z);

            float totalProgress = (riseTime + t) / totalWarningTime;
            UpdateRingScale(totalProgress);
            yield return null;
        }

        // ==========================================
        // STAGE 3: VIOLENT QUARTIC CRASH (p^4) (0.14s)
        // ==========================================
        t = 0f;
        float apexY = transform.position.y;
        while (t < slamTime)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / slamTime);

            // Quartic curve (p^4): Starts accelerating, slams down with massive weight
            float fallProgress = p * p * p * p;
            float currentY = Mathf.Lerp(apexY, baseY, fallProgress);
            transform.position = new Vector3(transform.position.x, currentY, transform.position.z);
            yield return null;
        }

        // ==========================================
        // STAGE 4: GROUND IMPACT (FRAME-EXACT BOOM)
        // ==========================================
        transform.position = new Vector3(transform.position.x, baseY, transform.position.z);

        // Force Jump_Land on exact impact frame!
        if (bossAnimator != null)
        {
            bossAnimator.Play("Jump_Land", 0, 0f);
        }

        ShockwaveFX.Spawn(new Vector3(transform.position.x, baseY + 0.03f, transform.position.z), shockwaveMeshPrefab, shockwaveMaterial);
        ApplyStompExplosion();
        BossAudio.Instance?.PlayStompImpact();

        if (telegraphRing != null)
        {
            telegraphRing.gameObject.SetActive(false);
        }

        yield return new WaitForSeconds(0.35f);
        IsStomping = false;
    }

    private void UpdateRingScale(float progress)
    {
        if (telegraphRing == null) return;
        float diameter = stompRadius * 2f * Mathf.Clamp01(progress);
        telegraphRing.localScale = new Vector3(diameter, 0.01f, diameter);
    }

    private bool CheckStaggerAbort(float baseY)
    {
        if (bossHealth != null && bossHealth.IsStaggered)
        {
            transform.position = new Vector3(transform.position.x, baseY, transform.position.z);
            CancelStomp();
            return true;
        }
        return false;
    }

    private void ApplyStompExplosion()
    {
        Vector3 center = transform.position;
        int hitCount = Physics.OverlapSphereNonAlloc(center, stompRadius, hitBuffer, targetLayers);

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = hitBuffer[i];
            if (col.transform == transform || col.transform.IsChildOf(transform)) continue;

            // Damage player / echo
            if (col.CompareTag("Player") || col.gameObject.layer == LayerMask.NameToLayer("Echo"))
            {
                // Knockback calculation (Radial outwards + slight upward lift)
                Vector3 knockDir = (col.transform.position - center);
                knockDir.y = 0f;

                if (knockDir.sqrMagnitude < 0.001f)
                    knockDir = -transform.forward;
                else
                    knockDir.Normalize();

                Vector3 impulseVector = (knockDir + Vector3.up * 0.35f).normalized * knockbackForce;

                // Push Rigidbody
                if (col.TryGetComponent<Rigidbody>(out var rb))
                {
                    rb.linearVelocity = Vector3.zero; // Unity 6 API
                    rb.AddForce(impulseVector, ForceMode.Impulse);
                }

                // Push CharacterController (via simple message or controller component)
                col.SendMessage("ApplyKnockback", impulseVector, SendMessageOptions.DontRequireReceiver);
                col.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
            }
        }
    }

    public void CancelStomp()
    {
        StopAllCoroutines();
        IsStomping = false;

        if (telegraphRing != null)
        {
            telegraphRing.gameObject.SetActive(false);
        }
    }

    private void CreateTelegraphRingIfNull()
    {
        if (telegraphRing != null) return;

        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "Stomp_Telegraph_Ring";
        ring.transform.SetParent(null);
        ring.transform.position = new Vector3(transform.position.x, transform.position.y + 0.02f, transform.position.z);
        ring.transform.localScale = Vector3.zero;

        Collider c = ring.GetComponent<Collider>();
        if (c != null) Destroy(c);

        Renderer rend = ring.GetComponent<Renderer>();
        if (rend != null)
        {
            if (telegraphMaterial != null)
            {
                rend.material = telegraphMaterial;
            }
            else
            {
                rend.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            }
        }

        telegraphRing = ring.transform;
        telegraphRing.gameObject.SetActive(false);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, triggerDistance);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, stompRadius);
    }
}