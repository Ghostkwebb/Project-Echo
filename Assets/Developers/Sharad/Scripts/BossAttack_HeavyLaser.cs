using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class BossAttack_HeavyLaser : BossAttackBase
{
    [Header("Laser Tuning (Designer PDF 3 & 4)")]
    [SerializeField] private float chargeDuration = 1.5f;
    [SerializeField] private float beamDuration = 6.0f;
    [SerializeField] private float laserTrackingLag = 0.55f; // Damped lag tracking
    [SerializeField] private float damagePerTick = 12.0f;
    [SerializeField] private float damageInterval = 0.25f;
    [SerializeField] private float headClearanceOffset = 1.6f;

    [Header("Continuous Magma Ribbon (TrailRenderer)")]
    [Tooltip("Drag M_LavaRibbon_Toon material here.")]
    [SerializeField] private Material lavaRibbonMaterial;
    [SerializeField] private float lavaRibbonWidth = 2.4f;
    [SerializeField] private float lavaDuration = 10.0f; // 8-12s in PDF
    [SerializeField] private LayerMask floorLayer;

    [Header("Shoulder Cannon Kinematics")]
    [SerializeField] private Transform cannonArm1;
    [SerializeField] private Transform cannonArm2;
    [SerializeField] private Transform cannonBarrel;
    [SerializeField] private Transform laserMuzzle;

    [Header("Visuals")]
    [SerializeField] private LineRenderer laserBeam;

    [Tooltip("Locks aim on ground for this many seconds before firing so player can dodge away.")]
    [SerializeField] private float preFireLockDuration = 0.45f;

    private Coroutine laserRoutine;
    private Vector3 currentGroundTarget;
    private Vector3 sweepVelocity;
    private Vector3 lastDamageNodePos;
    private float nextDamageTime;
    private bool isFiringBeam;
    private float floorY = 0.52f;

    private GameObject activeLavaRibbonObj;
    private TrailRenderer activeLavaTrail;

    protected override void Awake()
    {
        base.Awake();

        if (linkedPart == null)
            linkedPart = GetComponentInParent<BossPart>();

        AutoFindBonesIfNull();
        CreateBeamIfNull();
    }

    private void LateUpdate()
    {
        if (!IsExecuting) return;

        if (cannonBarrel != null && currentGroundTarget != Vector3.zero)
        {
            ApplyKinematicShoulderAim();
        }

        if (isFiringBeam)
        {
            UpdateActiveLaserBeam();
        }
    }

    protected override void OnStartAttack(Transform target)
    {
        laserRoutine = StartCoroutine(ExecuteHeavyLaserRoutine(target));
    }

    private IEnumerator ExecuteHeavyLaserRoutine(Transform target)
    {
        if (floorLayer.value == 0) floorLayer = LayerMask.GetMask("Floor", "Default");

        currentGroundTarget = GetGroundPointUnderTarget(target != null ? target.position : transform.position + transform.forward * 10f);

        // ==========================================
        // 1. CHARGE TELEGRAPH (1.6s Total)
        // ==========================================
        laserBeam.enabled = true;
        laserBeam.startWidth = 0.04f;
        laserBeam.endWidth = 0.04f;
        laserBeam.material.color = new Color(1f, 0.1f, 0.1f, 0.7f);

        float trackingPhaseTime = chargeDuration - preFireLockDuration; // ~1.15s tracking
        float chargeTimer = 0f;

        // Phase 1A: Tracking you (1.15s)
        while (chargeTimer < trackingPhaseTime)
        {
            chargeTimer += Time.deltaTime;

            if (target != null)
            {
                currentGroundTarget = GetGroundPointUnderTarget(target.position);
            }

            Vector3 origin = GetMuzzlePosition();
            laserBeam.SetPosition(0, origin);
            laserBeam.SetPosition(1, currentGroundTarget);

            yield return null;
        }

        // Phase 1B: AIM LOCKED! (0.45s Dodge Window)
        // Laser turns bright solid red and stops following you!
        laserBeam.material.color = new Color(1f, 0f, 0f, 1f);
        laserBeam.startWidth = 0.07f;
        laserBeam.endWidth = 0.07f;

        while (chargeTimer < chargeDuration)
        {
            chargeTimer += Time.deltaTime;

            // Target stays FROZEN where you were standing!
            Vector3 origin = GetMuzzlePosition();
            laserBeam.SetPosition(0, origin);
            laserBeam.SetPosition(1, currentGroundTarget);

            yield return null;
        }

        // ==========================================
        // 2. CONTINUOUS HEAVY BEAM + LAVA (6.0s)
        // ==========================================
        StartContinuousLavaRibbon();

        isFiringBeam = true;
        laserBeam.startWidth = 0.45f;
        laserBeam.endWidth = 0.45f;
        laserBeam.material.color = new Color(1f, 0.4f, 0.05f, 1f);

        float beamTimer = 0f;
        while (beamTimer < beamDuration)
        {
            beamTimer += Time.deltaTime;

            // Chases moving player with lag
            if (target != null)
            {
                Vector3 targetFloor = GetGroundPointUnderTarget(target.position);
                currentGroundTarget = Vector3.SmoothDamp(
                    currentGroundTarget,
                    targetFloor,
                    ref sweepVelocity,
                    laserTrackingLag
                );
            }

            yield return null;
        }

        // ==========================================
        // 3. SHUTDOWN & RECOVERY
        // ==========================================
        StopContinuousLavaRibbon();

        isFiringBeam = false;
        laserBeam.enabled = false;
        ResetBonesToZero();

        yield return new WaitForSeconds(1.2f);

        laserRoutine = null;
        FinishAttack();
    }

    private void StartContinuousLavaRibbon()
    {
        activeLavaRibbonObj = new GameObject("ActiveLavaRibbon");
        activeLavaRibbonObj.transform.position = currentGroundTarget;
        // -90 on X so TransformZ aligns the ribbon 100% flat on the floor!
        activeLavaRibbonObj.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

        activeLavaTrail = activeLavaRibbonObj.AddComponent<TrailRenderer>();
        activeLavaTrail.alignment = LineAlignment.TransformZ; // Ground flat plane
        activeLavaTrail.time = lavaDuration;                   // Lingers 10 seconds
        activeLavaTrail.startWidth = lavaRibbonWidth;
        activeLavaTrail.endWidth = lavaRibbonWidth * 0.7f;
        activeLavaTrail.minVertexDistance = 0.25f;            // Smooth curves
        activeLavaTrail.textureMode = LineTextureMode.Tile;  // Seamless tiling noise

        // Tapered curve: Rounded nose at front, rounded tail at back (Zero flat ruler cuts!)
        AnimationCurve widthCurve = new AnimationCurve();
        widthCurve.AddKey(0f, 0.1f);   // Tapers to point at tail
        widthCurve.AddKey(0.08f, 1.0f); // Reaches full width fast
        widthCurve.AddKey(0.92f, 1.0f); // Stays full width along body
        widthCurve.AddKey(1f, 0.1f);   // Tapers to point at laser head
        activeLavaTrail.widthCurve = widthCurve;

        if (lavaRibbonMaterial != null)
        {
            activeLavaTrail.material = lavaRibbonMaterial;
        }

        lastDamageNodePos = currentGroundTarget;
    }

    private void StopContinuousLavaRibbon()
    {
        if (activeLavaRibbonObj != null)
        {
            // Detach emitter so trail naturally dissolves over its 10s lifetime
            Destroy(activeLavaRibbonObj, lavaDuration + 0.5f);
            activeLavaRibbonObj = null;
            activeLavaTrail = null;
        }
    }

    private void UpdateActiveLaserBeam()
    {
        Vector3 origin = GetMuzzlePosition();
        Vector3 groundHitPoint = currentGroundTarget;

        laserBeam.SetPosition(0, origin);
        laserBeam.SetPosition(1, groundHitPoint);

        // Update ground ribbon position (draws the continuous fluid highway!)
        if (activeLavaRibbonObj != null)
        {
            activeLavaRibbonObj.transform.position = groundHitPoint;
        }

        // 1. Direct Laser Damage (48 DPS)
        Vector3 beamDir = (groundHitPoint - origin).normalized;
        float beamDist = Vector3.Distance(origin, groundHitPoint);

        if (Time.time >= nextDamageTime)
        {
            RaycastHit[] hits = Physics.SphereCastAll(origin, 0.5f, beamDir, beamDist);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider col = hits[i].collider;
                if (col.CompareTag("Player") || col.gameObject.layer == LayerMask.NameToLayer("Echo"))
                {
                    col.SendMessage("TakeDamage", damagePerTick, SendMessageOptions.DontRequireReceiver);
                }
            }
            nextDamageTime = Time.time + damageInterval;
        }

        // 2. Drop Invisible Trigger Node every 1.2m for contact damage
        if (Vector3.Distance(groundHitPoint, lastDamageNodePos) >= 1.2f)
        {
            LavaHazardPatch.SpawnDamageNode(groundHitPoint, lavaDuration);
            lastDamageNodePos = groundHitPoint;
        }
    }

    private Vector3 GetMuzzlePosition()
    {
        Vector3 rawMuzzle = laserMuzzle != null ? laserMuzzle.position : (cannonBarrel != null ? cannonBarrel.position : transform.position + Vector3.up * 4.5f);
        Vector3 beamDir = (currentGroundTarget - rawMuzzle).normalized;
        return rawMuzzle + (beamDir * headClearanceOffset);
    }

    private Vector3 GetGroundPointUnderTarget(Vector3 worldPos)
    {
        Ray ray = new Ray(new Vector3(worldPos.x, worldPos.y + 10f, worldPos.z), Vector3.down);
        if (Physics.Raycast(ray, out RaycastHit hit, 30f, floorLayer))
        {
            return new Vector3(hit.point.x, hit.point.y + 0.02f, hit.point.z);
        }
        return new Vector3(worldPos.x, floorY, worldPos.z);
    }

    private void ApplyKinematicShoulderAim()
    {
        Vector3 toGround = (currentGroundTarget - cannonBarrel.position).normalized;
        if (toGround.sqrMagnitude < 0.001f) return;

        Vector3 localDir = cannonBarrel.parent.InverseTransformDirection(toGround);
        Quaternion localLook = Quaternion.LookRotation(localDir);
        Vector3 euler = localLook.eulerAngles;

        if (euler.x > 180f) euler.x -= 360f;
        if (euler.y > 180f) euler.y -= 360f;
        if (euler.z > 180f) euler.z -= 360f;

        if (cannonArm1 != null) cannonArm1.localRotation = Quaternion.Euler(0f, euler.y * 0.35f, 0f);
        if (cannonArm2 != null) cannonArm2.localRotation = Quaternion.Euler(0f, euler.y * 0.35f, 0f);
        if (cannonBarrel != null) cannonBarrel.localRotation = Quaternion.Euler(0f, euler.y * 0.30f, euler.z);
    }

    private void ResetBonesToZero()
    {
        if (cannonArm1 != null) cannonArm1.localRotation = Quaternion.identity;
        if (cannonArm2 != null) cannonArm2.localRotation = Quaternion.identity;
        if (cannonBarrel != null) cannonBarrel.localRotation = Quaternion.identity;
    }

    protected override void OnInterrupt()
    {
        if (laserRoutine != null)
        {
            StopCoroutine(laserRoutine);
            laserRoutine = null;
        }

        StopContinuousLavaRibbon();
        isFiringBeam = false;
        if (laserBeam != null) laserBeam.enabled = false;
        ResetBonesToZero();
    }

    private void AutoFindBonesIfNull()
    {
        if (cannonBarrel == null)
        {
            Transform b = transform.Find("cannon_arm_1/cannon_arm_2/cannon");
            if (b != null) cannonBarrel = b;
            else cannonBarrel = transform;
        }

        if (cannonArm1 == null) cannonArm1 = transform.Find("cannon_arm_1");
        if (cannonArm2 == null) cannonArm2 = transform.Find("cannon_arm_1/cannon_arm_2");

        if (laserMuzzle == null && cannonBarrel != null)
        {
            Transform m = cannonBarrel.Find("mine");
            laserMuzzle = m != null ? m : cannonBarrel;
        }
    }

    private void CreateBeamIfNull()
    {
        if (laserBeam != null) return;

        GameObject beamObj = new GameObject("HeavyLaserBeam");
        beamObj.transform.SetParent(transform);
        beamObj.transform.localPosition = Vector3.zero;

        LineRenderer lr = beamObj.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.startWidth = 0.04f;
        lr.endWidth = 0.04f;
        lr.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        lr.material.color = new Color(1f, 0.4f, 0.05f);
        lr.enabled = false;

        laserBeam = lr;
    }
}