using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class BossAttack_HeavyLaser : BossAttackBase
{
    [Header("Laser Tuning (Designer PDF 3 & 4)")]
    [SerializeField] private float chargeDuration = 1.5f;
    [SerializeField] private float beamDuration = 6.0f;
    [SerializeField] private float damagePerTick = 12.0f;
    [SerializeField] private float damageInterval = 0.25f;

    [Header("Lava Trail Generation")]
    [SerializeField] private float lavaSpacing = 1.2f;
    [SerializeField] private float lavaRadius = 1.6f;

    [Header("Shoulder Cannon Kinematics")]
    [SerializeField] private Transform cannonArm1;
    [SerializeField] private Transform cannonArm2;
    [SerializeField] private Transform cannonBarrel;
    [SerializeField] private Transform laserMuzzle;

    [Header("Visuals")]
    [SerializeField] private LineRenderer laserBeam;

    [Header("Floor Detection")]
    [SerializeField] private LayerMask floorLayer;

    [SerializeField] private float laserTrackingLag = 0.55f; // 0.55s lag so player can outrun it
    private Vector3 sweepVelocity;


    private Coroutine laserRoutine;
    private Vector3 currentGroundTarget;
    private Vector3 lastLavaSpawnPos;
    private float nextDamageTime;
    private bool isFiringBeam;

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

        // Kinematic bone aiming
        if (cannonBarrel != null && currentGroundTarget != Vector3.zero)
        {
            ApplyKinematicShoulderAim();
        }

        // Draw laser
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
        // Auto-find Floor layer if empty
        if (floorLayer.value == 0)
        {
            floorLayer = LayerMask.GetMask("Floor", "Default");
        }

        // 1. Initial ground target point at real floor height
        currentGroundTarget = GetGroundPointUnderTarget(target != null ? target.position : transform.position + transform.forward * 10f);
        lastLavaSpawnPos = currentGroundTarget;

        // ==========================================
        // 1. CHARGE TELEGRAPH (1.5s Thin Red Laser)
        // ==========================================
        laserBeam.enabled = true;
        laserBeam.startWidth = 0.04f;
        laserBeam.endWidth = 0.04f;
        laserBeam.material.color = new Color(1f, 0.1f, 0.1f, 0.9f);

        float chargeTimer = 0f;
        while (chargeTimer < chargeDuration)
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

        // ==========================================
        // 2. CONTINUOUS HEAVY BEAM + LAVA (6.0s)
        // ==========================================
        isFiringBeam = true;
        laserBeam.startWidth = 0.45f;
        laserBeam.endWidth = 0.45f;
        laserBeam.material.color = new Color(1f, 0.4f, 0.05f, 1f);

        float beamTimer = 0f;
        while (beamTimer < beamDuration)
        {
            beamTimer += Time.deltaTime;

            // Sweeps along the real floor towards moving player
            if (target != null)
            {
                Vector3 targetFloor = GetGroundPointUnderTarget(target.position);
                // SmoothDamp lag: laser chases behind sprinting player!
                currentGroundTarget = Vector3.SmoothDamp(currentGroundTarget, targetFloor, ref sweepVelocity, laserTrackingLag);
            }

            yield return null;
        }

        // ==========================================
        // 3. SHUTDOWN & RECOVERY
        // ==========================================
        isFiringBeam = false;
        laserBeam.enabled = false;
        ResetBonesToZero();

        yield return new WaitForSeconds(1.2f);

        laserRoutine = null;
        FinishAttack();
    }

    private void UpdateActiveLaserBeam()
    {
        Vector3 origin = GetMuzzlePosition();
        Vector3 groundHitPoint = currentGroundTarget;

        laserBeam.SetPosition(0, origin);
        laserBeam.SetPosition(1, groundHitPoint);

        // 1. Damage check along the beam
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

        // 2. Drop Flat Lava Patch ON REAL FLOOR
        if (Vector3.Distance(groundHitPoint, lastLavaSpawnPos) >= lavaSpacing)
        {
            LavaHazardPatch.Spawn(groundHitPoint, lavaRadius);
            lastLavaSpawnPos = groundHitPoint;
        }
    }

    private Vector3 GetGroundPointUnderTarget(Vector3 worldPos)
    {
        // Casts straight down to find real floor surface
        Ray ray = new Ray(new Vector3(worldPos.x, worldPos.y + 10f, worldPos.z), Vector3.down);
        if (Physics.Raycast(ray, out RaycastHit hit, 30f, floorLayer))
        {
            return new Vector3(hit.point.x, hit.point.y + 0.02f, hit.point.z);
        }

        return new Vector3(worldPos.x, 0.52f, worldPos.z);
    }

    private Vector3 GetMuzzlePosition()
    {
        if (laserMuzzle != null) return laserMuzzle.position;
        if (cannonBarrel != null) return cannonBarrel.position;
        return transform.position + Vector3.up * 4.5f;
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

        // Kinematics: Arm1 & Arm2 rotate on Y only
        if (cannonArm1 != null) cannonArm1.localRotation = Quaternion.Euler(0f, euler.y * 0.35f, 0f);
        if (cannonArm2 != null) cannonArm2.localRotation = Quaternion.Euler(0f, euler.y * 0.35f, 0f);

        // Barrel: Y and Z only. X is strictly 0 (LOCKED)
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