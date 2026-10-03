using UnityEngine;

[DisallowMultipleComponent]
public class BossCoreExposeController : MonoBehaviour
{
    [Header("Cockpit Canopy Bone")]
    [Tooltip("Drag bone dome here. The windshield hatch.")]
    [SerializeField] private Transform domeHatchBone;
    [Tooltip("Hinges canopy open backward. Adjust X/Y/Z to match hinge angle.")]
    [SerializeField] private Vector3 openRotationOffset = new Vector3(-65f, 0f, 0f);
    [SerializeField] private float hatchMoveSpeed = 6.0f;

    [Header("Exposed Core Indicator (Optional)")]
    [Tooltip("Drag Hitbox_Core here.")]
    [SerializeField] private Transform coreWeakpoint;

    // References
    private BossHealth bossHealth;

    // State
    private Quaternion closedLocalRotation;
    private Quaternion openLocalRotation;
    private Quaternion targetHatchRotation;
    private bool isHatchOpen;

    private void Awake()
    {
        bossHealth = GetComponentInParent<BossHealth>();

        if (domeHatchBone == null)
        {
            // Auto-find dome bone in hierarchy
            Transform d = transform.Find("root/pelvis/spine_01/spine_02/spine_03/dome");
            domeHatchBone = d != null ? d : transform;
        }

        closedLocalRotation = domeHatchBone.localRotation;
        openLocalRotation = closedLocalRotation * Quaternion.Euler(openRotationOffset);
        targetHatchRotation = closedLocalRotation;
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

    private void LateUpdate()
    {
        if (domeHatchBone == null) return;

        // Animate canopy hatch open/close in LateUpdate (after Animator)
        domeHatchBone.localRotation = Quaternion.Slerp(
            domeHatchBone.localRotation,
            targetHatchRotation,
            hatchMoveSpeed * Time.deltaTime
        );
    }

    private void HandleStaggerStarted()
    {
        OpenCanopy();
    }

    private void HandleStaggerEnded()
    {
        CloseCanopy();
    }

    private void HandleBossDefeated()
    {
        // Keep cockpit blown open on defeat
        OpenCanopy();
    }

    public void OpenCanopy()
    {
        isHatchOpen = true;
        targetHatchRotation = openLocalRotation;
    }

    public void CloseCanopy()
    {
        isHatchOpen = false;
        targetHatchRotation = closedLocalRotation;
    }

    public void ResetHatch()
    {
        isHatchOpen = false;
        targetHatchRotation = closedLocalRotation;
        if (domeHatchBone != null)
        {
            domeHatchBone.localRotation = closedLocalRotation;
        }
    }

    // 1-Click Inspector test button
    [ContextMenu("TEST: Toggle Canopy Open / Close")]
    private void TestToggleCanopy()
    {
        if (isHatchOpen) CloseCanopy();
        else OpenCanopy();
    }
}