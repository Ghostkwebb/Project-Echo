using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BossAttack_MissileBarrage : BossAttackBase
{
    [Header("Barrage Tuning (Designer PDF 2 & 3)")]
    [SerializeField] private float damagePerMissile = 35f;
    [SerializeField] private float explosionRadius = 2.5f;
    [SerializeField] private float rocketFlightTime = 1.35f;
    [SerializeField] private float arcApexHeight = 16.0f;
    [SerializeField] private float predictionLeadTime = 1.2f;
    [SerializeField] private float randomOffsetRadius = 5.0f;
    [SerializeField] private LayerMask floorLayer;

    [Header("Hardpoints (Back Rocket Pods)")]
    [Tooltip("Drag bone rocket_root_l here.")]
    [SerializeField] private Transform leftRocketPod;
    [Tooltip("Drag bone rocket_root_r here.")]
    [SerializeField] private Transform rightRocketPod;
    [SerializeField] private LayerMask damageLayers;

    [Header("Animation Sync")]
    [Tooltip("Time for back pod hatches to slide fully open before firing.")]
    [SerializeField] private float podOpenDuration = 1.1f; // Increased from 0.45s

    [Header("3D Rocket Mesh & Trail")]
    [SerializeField] private GameObject rocketMeshPrefab;
    [SerializeField] private Material rocketMaterialOverride;
    [Tooltip("Drag M_Smoke_FX or a transparent material here.")]
    [SerializeField] private Material smokeTrailMaterial;
    [SerializeField] private float rocketScale = 1.0f;
    [Tooltip("Adjust if 3D model nose points up or sideways. (0,0,0) for default forward.")]
    [SerializeField] private Vector3 rocketModelRotationOffset = Vector3.zero;

    [Tooltip("Drag M_Explosion_Toon material here.")]
    [SerializeField] private Material explosionMaterial;

    [Tooltip("Drag M_Telegraph_Holo here to make ground circles translucent.")]
    [SerializeField] private Material telegraphMaterial;

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
        BossAudio.Instance?.PlayMissileLaunch();
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

        // 2 on player
        points.Add(GetFloorPoint(playerPos));
        points.Add(GetFloorPoint(playerPos + new Vector3(Random.Range(-0.8f, 0.8f), 0f, Random.Range(-0.8f, 0.8f))));

        // 2 predicted ahead
        Vector3 lead = Vector3.ClampMagnitude(estimatedPlayerVelocity * predictionLeadTime, 7.0f);
        points.Add(GetFloorPoint(playerPos + lead));
        points.Add(GetFloorPoint(playerPos + lead + new Vector3(Random.Range(-1.0f, 1.0f), 0f, Random.Range(-1.0f, 1.0f))));

        // 1 random nearby
        Vector2 rnd = Random.insideUnitCircle * randomOffsetRadius;
        points.Add(GetFloorPoint(playerPos + new Vector3(rnd.x, 0f, rnd.y)));

        return points;
    }

    private Vector3 GetFloorPoint(Vector3 worldPos)
    {
        if (floorLayer.value == 0) floorLayer = LayerMask.GetMask("Floor", "Default");

        Ray ray = new Ray(new Vector3(worldPos.x, worldPos.y + 10f, worldPos.z), Vector3.down);
        if (Physics.Raycast(ray, out RaycastHit hit, 30f, floorLayer))
        {
            return new Vector3(hit.point.x, hit.point.y + 0.02f, hit.point.z);
        }
        return new Vector3(worldPos.x, 0.52f, worldPos.z);
    }

    private void SpawnBallisticRocket(Vector3 startPodPos, Vector3 targetGroundPos, int index)
    {
        GameObject rocket;

        if (rocketMeshPrefab != null)
        {
            rocket = Instantiate(rocketMeshPrefab, startPodPos, Quaternion.identity);
        }
        else
        {
            rocket = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Collider c = rocket.GetComponent<Collider>();
            if (c != null) Destroy(c);
        }

        rocket.name = $"Ballistic_Rocket_{index}";
        rocket.transform.localScale = Vector3.one * rocketScale;

        if (rocketMaterialOverride != null)
        {
            Renderer[] rends = rocket.GetComponentsInChildren<Renderer>();
            for (int r = 0; r < rends.Length; r++) rends[r].material = rocketMaterialOverride;
        }

        // Create 3D Volumetric World-Space Smoke Trail
        GameObject smokeObj = new GameObject("Rocket_SmokeTrail");
        smokeObj.transform.SetParent(rocket.transform);
        smokeObj.transform.localPosition = Vector3.zero;

        ParticleSystem ps = smokeObj.AddComponent<ParticleSystem>();

        // 1. Main Module
        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World; // Leaves clouds behind in sky!
        main.startLifetime = 0.6f;
        main.startSpeed = 0f; // Stationary in air where rocket was
        main.startSize = 0.4f;
        main.startColor = new Color(0.95f, 0.95f, 1f, 0.5f); // Soft white/grey vapor

        // 2. Emission Module (Spawns clouds along distance traveled)
        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.rateOverDistance = 22; // Drops puff every few centimeters

        // 3. Size Over Lifetime (Smoke billows and expands)
        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(0f, 0.3f);
        curve.AddKey(1f, 1.4f); // Expands to 1.4m cloud
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

        // 4. Color Over Lifetime (Smooth alpha fade)
        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0.6f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        colorOverLifetime.color = grad;

        // 5. Renderer Material
        var rend = smokeObj.GetComponent<ParticleSystemRenderer>();
        if (smokeTrailMaterial != null)
        {
            rend.material = smokeTrailMaterial;
        }

        BossBallisticRocket flight = rocket.AddComponent<BossBallisticRocket>();
        flight.Initialize(startPodPos, targetGroundPos, rocketFlightTime, arcApexHeight, damagePerMissile, explosionRadius, damageLayers, rocketModelRotationOffset, explosionMaterial);
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
                if (telegraphMaterial != null)
                {
                    rend.material = telegraphMaterial;
                }
                else
                {
                    Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                    mat.SetFloat("_Surface", 1);
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.color = new Color(1f, 0.2f, 0.1f, 0.35f);
                    rend.material = mat;
                }
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
    private Vector3 rotationOffset;
    private Material explosionMatOverride;


    public void Initialize(Vector3 start, Vector3 target, float flightTime, float apexHeight, float dmg, float radius, LayerMask layers, Vector3 modelRotOffset, Material exploMat)
    {
        startPos = start;
        targetPos = target;
        totalFlightTime = flightTime;
        damage = dmg;
        splashRadius = radius;
        hitLayers = layers;
        rotationOffset = modelRotOffset;
        explosionMatOverride = exploMat; // Cached

        apexControlPoint = ((startPos + targetPos) * 0.5f) + Vector3.up * apexHeight;
        lastPos = start;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float p = Mathf.Clamp01(elapsed / totalFlightTime);

        Vector3 m1 = Vector3.Lerp(startPos, apexControlPoint, p);
        Vector3 m2 = Vector3.Lerp(apexControlPoint, targetPos, p);
        Vector3 currentPos = Vector3.Lerp(m1, m2, p);

        transform.position = currentPos;

        // Nose tracks flight trajectory directly (Zero 90° sideways snap)
        Vector3 travelDir = currentPos - lastPos;
        if (travelDir.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(travelDir) * Quaternion.Euler(rotationOffset);
        }
        lastPos = currentPos;

        if (p >= 1.0f)
        {
            Detonate();
        }
    }

    private void Detonate()
    {
        BossAudio.Instance?.PlayMissileExplosion(targetPos);
        // 1. Splash Damage (Wipes standing Echoes)
        Collider[] hits = Physics.OverlapSphere(targetPos, splashRadius, hitLayers);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i];
            if (col.CompareTag("Player") || col.gameObject.layer == LayerMask.NameToLayer("Echo"))
            {
                col.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
            }
        }

        // 2. Spawn Real Fireball & Shrapnel Blast (Zero primitive spheres!)
        SpawnExplosionVFX(targetPos);

        Destroy(gameObject);
    }

    private void SpawnExplosionVFX(Vector3 pos)
    {
        GameObject vfxObj = new GameObject("Missile_Explosion_FX");
        vfxObj.transform.position = pos;

        // 1. FIREBALL BURST
        ParticleSystem ps = vfxObj.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); // FIXES DURATION WARNING!

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.startLifetime = 0.25f;
        main.startSpeed = 3.5f;
        main.startSize = 4.0f; // Big fire bloom
        main.startColor = new Color(1f, 0.65f, 0.15f, 1f);

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 6) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.4f;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.4f, 1f, 1.2f));

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(new Color(1f, 0.9f, 0.3f), 0f), new GradientColorKey(new Color(1f, 0.2f, 0f), 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        colorOverLifetime.color = grad;

        var rend = vfxObj.GetComponent<ParticleSystemRenderer>();
        Material fireMat = explosionMatOverride;
        if (fireMat == null)
        {
            fireMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            fireMat.SetFloat("_Surface", 1);
            fireMat.color = new Color(1f, 0.45f, 0.05f);
        }
        rend.material = fireMat;

        ps.Play(); // Play cleanly after configuration

        // 2. FLYING SHRAPNEL SPARKS
        GameObject sparksObj = new GameObject("Explosion_Sparks");
        sparksObj.transform.SetParent(vfxObj.transform);
        sparksObj.transform.localPosition = Vector3.zero;

        ParticleSystem sp = sparksObj.AddComponent<ParticleSystem>();
        sp.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); // FIXES WARNING!

        var sm = sp.main;
        sm.playOnAwake = false;
        sm.loop = false;
        sm.startLifetime = 0.45f;
        sm.startSpeed = 16f;
        sm.startSize = 0.05f;
        sm.gravityModifier = 2.5f;
        sm.startColor = new Color(1f, 0.95f, 0.4f, 1f);

        var se = sp.emission;
        se.rateOverTime = 0;
        se.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 25) });

        var ss = sp.shape;
        ss.shapeType = ParticleSystemShapeType.Hemisphere;
        ss.radius = 0.2f;

        var sRend = sparksObj.GetComponent<ParticleSystemRenderer>();
        sRend.renderMode = ParticleSystemRenderMode.Stretch;
        sRend.velocityScale = 0.04f;
        sRend.lengthScale = 2.0f;
        sRend.material = fireMat;

        sp.Play();

        Destroy(vfxObj, 1.0f);
    }
}