using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BossAttack_MissileBarrage : BossAttackBase
{
    [Header("Barrage Tuning (Designer PDF 2 & 3)")]
    [SerializeField] private int missileCount = 5;
    [SerializeField] private float damagePerMissile = 35f;
    [SerializeField] private float explosionRadius = 2.5f;
    [SerializeField] private float rocketFlightTime = 1.35f;
    [SerializeField] private float arcApexHeight = 16.0f;
    [SerializeField] private float predictionLeadTime = 1.2f;
    [SerializeField] private float randomOffsetRadius = 5.0f;

    [Header("Hardpoints (Back Rocket Pods)")]
    [Tooltip("Drag bone rocket_root_l here.")]
    [SerializeField] private Transform leftRocketPod;
    [Tooltip("Drag bone rocket_root_r here.")]
    [SerializeField] private Transform rightRocketPod;
    [SerializeField] private LayerMask damageLayers;

    [Header("Animation Sync")]
    [Tooltip("Time for back pod hatches to slide fully open before firing.")]
    [SerializeField] private float podOpenDuration = 1.1f; // Increased from 0.45s

    private Animator bossAnimator;
    private Coroutine barrageRoutine;
    private Vector3 lastPlayerPos;
    private Vector3 estimatedPlayerVelocity;

    private readonly List<GameObject> activeWarningRings = new List<GameObject>();
    private static readonly int OnAttackMissilesHash = Animator.StringToHash("OnAttack_Missiles");
    private static readonly int OnFireMissilesHash = Animator.StringToHash("OnFireMissiles");

    protected override void Awake()
    {
        base.Awake();

        bossAnimator = GetComponentInParent<Animator>();

        if (linkedPart == null)
            linkedPart = GetComponentInParent<BossPart>();

        if (damageLayers.value == 0)
        {
            damageLayers = LayerMask.GetMask("Player", "Echo", "Default");
        }

        // Auto-find rocket bones if empty
        if (leftRocketPod == null)
        {
            Transform l = transform.Find("rocket_root_l");
            if (l != null) leftRocketPod = l;
            else leftRocketPod = transform;
        }

        if (rightRocketPod == null)
        {
            Transform r = transform.Find("rocket_root_r");
            if (r != null) rightRocketPod = r;
            else rightRocketPod = transform;
        }
    }

    protected override void Update()
    {
        base.Update();

        if (activeTarget != null)
        {
            estimatedPlayerVelocity = (activeTarget.position - lastPlayerPos) / Time.deltaTime;
            lastPlayerPos = activeTarget.position;
        }
    }

    protected override void OnStartAttack(Transform target)
    {
        barrageRoutine = StartCoroutine(ExecuteBarrageRoutine(target));
    }

    private IEnumerator ExecuteBarrageRoutine(Transform target)
    {
        // 1. OPEN PODS (Swarm_Start)
        if (bossAnimator != null)
        {
            bossAnimator.SetTrigger(OnAttackMissilesHash);
        }

        yield return new WaitForSeconds(podOpenDuration);

        // 2. SPAWN 5 WARNING CIRCLES ON FLOOR
        List<Vector3> targetPoints = CalculateTargetPoints(target);
        SpawnWarningCircles(targetPoints);

        // 3. LAUNCH SALVO OUT OF BACK PODS (Swarm_Fire)
        if (bossAnimator != null)
        {
            bossAnimator.SetTrigger(OnFireMissilesHash);
        }

        yield return new WaitForSeconds(0.15f);

        // Launch 5 rockets sequentially from Left and Right pods
        for (int i = 0; i < targetPoints.Count; i++)
        {
            Transform pod = (i % 2 == 0) ? leftRocketPod : rightRocketPod;
            SpawnBallisticRocket(pod.position, targetPoints[i], i);
            yield return new WaitForSeconds(0.08f); // Salvo ripple fire
        }

        // 4. CIRCLES PULSE WHILE ROCKETS ARE IN FLIGHT
        float flightTimer = 0f;
        while (flightTimer < rocketFlightTime)
        {
            flightTimer += Time.deltaTime;
            float progress = Mathf.Clamp01(flightTimer / rocketFlightTime);
            UpdateWarningCircles(progress);
            yield return null;
        }

        // 5. CLEANUP & RECOVERY (Swarm_End auto-returns to Idle)
        yield return new WaitForSeconds(0.6f);
        ClearWarningRings();

        yield return new WaitForSeconds(0.4f);
        barrageRoutine = null;
        FinishAttack();
    }

    private List<Vector3> CalculateTargetPoints(Transform target)
    {
        List<Vector3> points = new List<Vector3>();
        Vector3 playerPos = target != null ? target.position : transform.position + transform.forward * 10f;
        float groundY = playerPos.y + 0.02f;

        // 2 on player
        points.Add(new Vector3(playerPos.x, groundY, playerPos.z));
        points.Add(new Vector3(playerPos.x + Random.Range(-0.8f, 0.8f), groundY, playerPos.z + Random.Range(-0.8f, 0.8f)));

        // 2 predicted ahead of player run direction
        Vector3 predictedLead = estimatedPlayerVelocity * predictionLeadTime;
        predictedLead = Vector3.ClampMagnitude(predictedLead, 7.0f);
        Vector3 leadPos = playerPos + predictedLead;
        points.Add(new Vector3(leadPos.x, groundY, leadPos.z));
        points.Add(new Vector3(leadPos.x + Random.Range(-1.0f, 1.0f), groundY, leadPos.z + Random.Range(-1.0f, 1.0f)));

        // 1 random nearby location
        Vector2 randomCircle = Random.insideUnitCircle * randomOffsetRadius;
        points.Add(new Vector3(playerPos.x + randomCircle.x, groundY, playerPos.z + randomCircle.y));

        return points;
    }

    private void SpawnBallisticRocket(Vector3 startPodPos, Vector3 targetGroundPos, int index)
    {
        GameObject rocket = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        rocket.name = $"Ballistic_Rocket_{index}";
        rocket.transform.position = startPodPos;
        rocket.transform.localScale = new Vector3(0.3f, 0.7f, 0.3f);

        Renderer rend = rocket.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            rend.material.color = new Color(1f, 0.45f, 0.05f); // Orange warhead
        }

        // Add ballistic flight controller
        BossBallisticRocket flight = rocket.AddComponent<BossBallisticRocket>();
        flight.Initialize(startPodPos, targetGroundPos, rocketFlightTime, arcApexHeight, damagePerMissile, explosionRadius, damageLayers);
    }

    private void SpawnWarningCircles(List<Vector3> points)
    {
        ClearWarningRings();

        for (int i = 0; i < points.Count; i++)
        {
            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = $"Missile_Target_Circle_{i}";
            ring.transform.position = points[i];
            ring.transform.localScale = Vector3.zero;

            Collider c = ring.GetComponent<Collider>();
            if (c != null) Destroy(c);

            Renderer rend = ring.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                rend.material.color = new Color(1f, 0.12f, 0.12f, 0.6f);
            }

            activeWarningRings.Add(ring);
        }
    }

    private void UpdateWarningCircles(float progress)
    {
        float targetDiameter = explosionRadius * 2f;
        float pulse = Mathf.Sin(progress * Mathf.PI * 8f) * 0.15f;
        float currentDiameter = (targetDiameter * progress) + pulse;

        for (int i = 0; i < activeWarningRings.Count; i++)
        {
            if (activeWarningRings[i] != null)
            {
                activeWarningRings[i].transform.localScale = new Vector3(currentDiameter, 0.01f, currentDiameter);
            }
        }
    }

    private void ClearWarningRings()
    {
        for (int i = 0; i < activeWarningRings.Count; i++)
        {
            if (activeWarningRings[i] != null)
            {
                Destroy(activeWarningRings[i]);
            }
        }
        activeWarningRings.Clear();
    }

    protected override void OnInterrupt()
    {
        if (barrageRoutine != null)
        {
            StopCoroutine(barrageRoutine);
            barrageRoutine = null;
        }

        ClearWarningRings();
    }
}

/// <summary>
/// Flies in a physical ballistic parabolic curve out of the pod and down into target.
/// </summary>
public class BossBallisticRocket : MonoBehaviour
{
    private Vector3 startPos;
    private Vector3 targetPos;
    private Vector3 apexControlPoint;
    private float totalFlightTime;
    private float damage;
    private float splashRadius;
    private LayerMask hitLayers;
    private float elapsed;
    private Vector3 lastPos;

    public void Initialize(Vector3 start, Vector3 target, float flightTime, float apexHeight, float dmg, float radius, LayerMask layers)
    {
        startPos = start;
        targetPos = target;
        totalFlightTime = flightTime;
        damage = dmg;
        splashRadius = radius;
        hitLayers = layers;

        // Apex point: High mid-air curve above the battlefield
        apexControlPoint = ((startPos + targetPos) * 0.5f) + Vector3.up * apexHeight;
        lastPos = start;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float p = Mathf.Clamp01(elapsed / totalFlightTime);

        // Quadratic Bezier Curve: Start -> Sky Apex -> Ground Target
        Vector3 m1 = Vector3.Lerp(startPos, apexControlPoint, p);
        Vector3 m2 = Vector3.Lerp(apexControlPoint, targetPos, p);
        Vector3 currentPos = Vector3.Lerp(m1, m2, p);

        transform.position = currentPos;

        // Face trajectory direction (noses up on launch, noses down on descent)
        Vector3 travelDir = currentPos - lastPos;
        if (travelDir.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(travelDir) * Quaternion.Euler(90f, 0f, 0f);
        }
        lastPos = currentPos;

        // Detonate on reaching ground
        if (p >= 1.0f)
        {
            Detonate();
        }
    }

    private void Detonate()
    {
        Collider[] hits = Physics.OverlapSphere(targetPos, splashRadius, hitLayers);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i];
            if (col.CompareTag("Player") || col.gameObject.layer == LayerMask.NameToLayer("Echo"))
            {
                col.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
            }
        }

        // Visual blast
        GameObject blast = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        blast.transform.position = targetPos;
        blast.transform.localScale = Vector3.one * (splashRadius * 2f);
        Collider bc = blast.GetComponent<Collider>();
        if (bc != null) Destroy(bc);

        Renderer rend = blast.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            rend.material.color = new Color(1f, 0.25f, 0.05f, 0.85f);
        }

        Destroy(blast, 0.3f);
        Destroy(gameObject);
    }
}