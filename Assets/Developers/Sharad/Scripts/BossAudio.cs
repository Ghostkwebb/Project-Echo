using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public class BossAudio : MonoBehaviour
{
    [Header("State SFX")]
    [SerializeField] private AudioClip shieldShatterClip;
    [SerializeField] private AudioClip shieldRebootClip;
    [SerializeField] private AudioClip coreExposedAlarmClip;
    [SerializeField] private AudioClip partBreakClip;
    [SerializeField] private AudioClip deathExplosionClip;

    [Header("Attack SFX")]
    [SerializeField] private AudioClip stompJumpClip;
    [SerializeField] private AudioClip stompImpactClip;
    [SerializeField] private AudioClip machineGunFireClip;
    [SerializeField] private AudioClip missileLaunchClip;
    [SerializeField] private AudioClip missileExplosionClip;
    [SerializeField] private AudioClip laserChargeClip;
    [SerializeField] private AudioClip laserLoopClip;

    public static BossAudio Instance { get; private set; }


    private AudioSource audioSource;
    private BossHealth bossHealth;

    private void Awake()
    {
        Instance = this;
        audioSource = GetComponent<AudioSource>();
        audioSource.spatialBlend = 1.0f; // 3D sound
        audioSource.playOnAwake = false;
        bossHealth = GetComponent<BossHealth>();
    }

    public void PlayStompJump() => PlayOneShot(stompJumpClip, 1.0f);
    public void PlayStompImpact() => PlayOneShot(stompImpactClip, 1.3f);
    public void PlayMGFire() => PlayOneShot(machineGunFireClip, 0.5f);
    public void PlayMissileLaunch() => PlayOneShot(missileLaunchClip, 0.8f);
    public void PlayMissileExplosion(Vector3 pos)
    {
        if (missileExplosionClip != null) AudioSource.PlayClipAtPoint(missileExplosionClip, pos, 0.9f);
    }
    public void PlayLaserCharge() => PlayOneShot(laserChargeClip, 1.0f);
    public void PlayLaserLoop() => PlayOneShot(laserLoopClip, 1.1f);

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnEnable()
    {
        if (bossHealth != null)
        {
            bossHealth.OnStaggerStarted += HandleStagger;
            bossHealth.OnStaggerEnded += HandleReboot;
            bossHealth.OnBossDefeated += HandleDeath;
        }

        BossPart[] parts = GetComponentsInChildren<BossPart>(true);
        for (int i = 0; i < parts.Length; i++)
        {
            parts[i].OnPartBroken += p => PlayOneShot(partBreakClip);
        }
    }

    private void OnDisable()
    {
        if (bossHealth != null)
        {
            bossHealth.OnStaggerStarted -= HandleStagger;
            bossHealth.OnStaggerEnded -= HandleReboot;
            bossHealth.OnBossDefeated -= HandleDeath;
        }
    }

    private void HandleStagger()
    {
        PlayOneShot(shieldShatterClip, 1.2f);
        PlayOneShot(coreExposedAlarmClip, 0.8f);
    }

    private void HandleReboot() => PlayOneShot(shieldRebootClip, 1.0f);
    private void HandleDeath() => PlayOneShot(deathExplosionClip, 1.5f);

    public void PlayOneShot(AudioClip clip, float vol = 1.0f)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip, vol);
        }
    }
}