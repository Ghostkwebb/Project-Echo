using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class BossShieldBubble : MonoBehaviour
{
    [Header("Shield Bubble Tuning")]
    [SerializeField] private float baseScale = 5.6f;
    [SerializeField] private float hitPulseScale = 5.85f;
    [SerializeField] private float pulseDuration = 0.22f; // Fast shimmer

    [Header("Colors (HDR Glow)")]
    [ColorUsage(true, true)]
    [SerializeField] private Color normalColor = new Color(0f, 0.9f, 1f, 1.8f); // HDR Cyan
    [ColorUsage(true, true)]
    [SerializeField] private Color hitColor = new Color(1f, 1f, 1f, 2.5f);     // Blinding white flash

    [Header("Static Shimmer Tuning")]
    [SerializeField] private float shimmerDuration = 0.18f; // Fast crisp flash

    private BossHealth bossHealth;
    private Renderer bubbleRenderer;
    private Material matInstance;
    private Coroutine pulseRoutine;
    private float lastShieldAmount;
    private bool isShieldBroken;
    private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");

    private void Awake()
    {
        bossHealth = GetComponentInParent<BossHealth>();
        bubbleRenderer = GetComponent<Renderer>();

        if (bubbleRenderer != null)
        {
            matInstance = bubbleRenderer.material;
            SetMaterialColor(normalColor);
            // INVISIBLE BY DEFAULT (Only shows on damage impact!)
            bubbleRenderer.enabled = false;
        }

        transform.localScale = Vector3.one * baseScale;

        // Subscribe in Awake so event NEVER unhooks prematurely
        if (bossHealth != null)
        {
            bossHealth.OnShieldChanged += HandleShieldChanged;
            bossHealth.OnStaggerStarted += HandleShieldShatter;
            bossHealth.OnStaggerEnded += HandleShieldReboot;
            lastShieldAmount = bossHealth.CurrentShield;
        }
    }

    private void OnDestroy()
    {
        if (bossHealth != null)
        {
            bossHealth.OnShieldChanged -= HandleShieldChanged;
            bossHealth.OnStaggerStarted -= HandleShieldShatter;
            bossHealth.OnStaggerEnded -= HandleShieldReboot;
        }

        if (matInstance != null) Destroy(matInstance);
    }

    private void Update()
    {
        // New Input System Hotkeys for Testing
        if (Application.isPlaying && bossHealth != null)
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.hKey.wasPressedThisFrame)
                {
                    bossHealth.TakeDamage(250f, DamageSource.Player, HitboxType.Part);
                }

                if (keyboard.jKey.wasPressedThisFrame)
                {
                    bossHealth.TakeDamage(1000f, DamageSource.Player, HitboxType.Part);
                }
            }
        }
    }

    private void HandleShieldChanged(float current, float max)
    {
        if (isShieldBroken) return;

        // Bullet hit: Pure opacity shimmer, ZERO scale change
        if (current < lastShieldAmount && current > 0f)
        {
            if (pulseRoutine != null) StopCoroutine(pulseRoutine);
            pulseRoutine = StartCoroutine(StaticHitShimmerRoutine());
        }

        lastShieldAmount = current;
    }

    private IEnumerator StaticHitShimmerRoutine()
    {
        if (bubbleRenderer == null) yield break;

        // Lock scale exact - never stretches
        transform.localScale = Vector3.one * baseScale;
        bubbleRenderer.enabled = true;

        float t = 0f;
        while (t < shimmerDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / shimmerDuration);

            // Fades from bright hit flash down to transparent
            Color c = Color.Lerp(hitColor, Color.clear, p * p);
            SetMaterialColor(c);

            yield return null;
        }

        bubbleRenderer.enabled = false;
        pulseRoutine = null;
    }

    private IEnumerator HitPulseRoutine()
    {
        if (bubbleRenderer == null) yield break;

        // Make visible and pulse
        bubbleRenderer.enabled = true;
        SetMaterialColor(hitColor);
        transform.localScale = Vector3.one * hitPulseScale;

        float t = 0f;
        while (t < pulseDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / pulseDuration);

            SetMaterialColor(Color.Lerp(hitColor, normalColor, p));
            transform.localScale = Vector3.Lerp(Vector3.one * hitPulseScale, Vector3.one * baseScale, p);
            yield return null;
        }

        // Fades back to invisible
        bubbleRenderer.enabled = false;
        transform.localScale = Vector3.one * baseScale;
        pulseRoutine = null;
    }

    private void HandleShieldShatter()
    {
        isShieldBroken = true;
        if (pulseRoutine != null) StopCoroutine(pulseRoutine);
        StartCoroutine(ShatterRoutine());
    }

    private IEnumerator ShatterRoutine()
    {
        if (bubbleRenderer == null) yield break;

        bubbleRenderer.enabled = true;
        SetMaterialColor(hitColor);

        float t = 0f;
        float popDuration = 0.15f;

        while (t < popDuration)
        {
            t += Time.deltaTime;
            float p = t / popDuration;
            transform.localScale = Vector3.Lerp(Vector3.one * baseScale, Vector3.one * (baseScale + 1.2f), p);
            yield return null;
        }

        // Turn OFF renderer during Stagger (never disable GameObject!)
        bubbleRenderer.enabled = false;
    }

    private void HandleShieldReboot()
    {
        StartCoroutine(RebootRoutine());
    }

    private IEnumerator RebootRoutine()
    {
        if (bubbleRenderer == null) yield break;

        isShieldBroken = false;
        bubbleRenderer.enabled = true;
        SetMaterialColor(normalColor);

        // Contract from large ring back to Howitzer (Energy reboot pulse)
        float t = 0f;
        float rebootDuration = 0.40f;

        while (t < rebootDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / rebootDuration);
            // Snap inwards
            transform.localScale = Vector3.Lerp(Vector3.one * (baseScale + 1.5f), Vector3.one * baseScale, p);
            yield return null;
        }

        transform.localScale = Vector3.one * baseScale;

        // Flash once to confirm reboot, then go back to dormant
        yield return new WaitForSeconds(0.2f);
        bubbleRenderer.enabled = false;
    }

    private void SetMaterialColor(Color c)
    {
        if (matInstance != null) matInstance.SetColor(BaseColorID, c);
    }
}