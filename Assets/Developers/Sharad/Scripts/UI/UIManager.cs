using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Cinemachine;

[DisallowMultipleComponent]
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Panels")]
    [SerializeField] private GameObject panelMainMenu;
    [SerializeField] private GameObject panelSettings;
    [SerializeField] private GameObject panelHUD;
    [SerializeField] private GameObject panelVictory;

    [Header("Cinemachine Menu Camera")]
    [Tooltip("Drag CinemachineCamera_Menu here.")]
    [SerializeField] private CinemachineCamera menuCamera;
    [SerializeField] private float cameraBlendDuration = 1.5f;

    [Header("Player & Input Lock")]
    [SerializeField] private MonoBehaviour playerLocomotion;
    [SerializeField] private MonoBehaviour playerShooter;

    [Header("HUD - Boss Sliders & Texts (GDD 29)")]
    [SerializeField] private Slider bossShieldSlider;
    [SerializeField] private Slider bossHealthSlider;
    [SerializeField] private TextMeshProUGUI bossStatusText;
    [SerializeField] private TextMeshProUGUI threatWarningText;

    [Header("HUD - Player & Echoes")]
    [SerializeField] private Slider playerHealthSlider;
    [SerializeField] private TextMeshProUGUI playerHealthText;
    [SerializeField] private TextMeshProUGUI attemptCountText;
    [SerializeField] private TextMeshProUGUI echoCountText;

    [Header("Victory Screen")]
    [SerializeField] private TextMeshProUGUI victoryStatsText;

    [Header("Settings UI Controls")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private Slider sensitivitySlider;
    [SerializeField] private TMP_Dropdown qualityDropdown;
    [SerializeField] private Toggle fullscreenToggle;

    private BossHealth bossHealth;
    private PlayerHealth playerHealth;
    private float combatTimer;
    private bool isInCombat;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        bossHealth = FindAnyObjectByType<BossHealth>();
        playerHealth = FindAnyObjectByType<PlayerHealth>();

        ShowMainMenuInstant();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        // Hook Boss Events
        if (bossHealth != null)
        {
            bossHealth.OnHealthChanged += UpdateBossHealth;
            bossHealth.OnShieldChanged += UpdateBossShield;
            bossHealth.OnStaggerStarted += HandleBossStaggered;
            bossHealth.OnStaggerEnded += HandleBossRecovered;
        }

        // Hook Threat Monitor
        BossThreatMonitor threat = FindAnyObjectByType<BossThreatMonitor>();
        if (threat != null)
        {
            threat.OnPrimaryThreatSelected += (id, t) => ShowThreatWarning($"⚠ PRIMARY THREAT: ECHO {id:00}");
            threat.OnPlayerReEngaged += HideThreatWarning;
        }

        // Hook Encounter Manager
        if (EncounterManager.Instance != null)
        {
            EncounterManager.Instance.OnAttemptStarted += HandleAttemptStarted;
            EncounterManager.Instance.OnPlayerDied += HandlePlayerDied;
            EncounterManager.Instance.OnVictory += HandleVictory;
        }

        InitializeSettingsUI();
    }

    private void Update()
    {
        if (isInCombat)
        {
            combatTimer += Time.deltaTime;
        }

        // Press Escape to toggle Settings in-game
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (panelSettings != null && panelSettings.activeSelf)
            {
                CloseSettings();
            }
            else if (isInCombat)
            {
                OpenSettings();
            }
        }
    }

    // ==========================================
    // MENU ➔ PLAY ZOOM-IN FLOW
    // ==========================================
    public void OnPlayButtonClicked()
    {
        StartCoroutine(PlayTransitionRoutine());
    }

    private IEnumerator PlayTransitionRoutine()
    {
        // 1. Hide Menu
        if (panelMainMenu != null) panelMainMenu.SetActive(false);

        // 2. Drop Menu Camera Priority -> Cinemachine glides down to Wraith!
        if (menuCamera != null)
        {
            menuCamera.Priority = 0;
        }

        // Wait for smooth zoom-in blend
        yield return new WaitForSeconds(cameraBlendDuration);

        // 3. Lock Cursor & Enable Player Controls
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (playerLocomotion != null) playerLocomotion.enabled = true;
        if (playerShooter != null) playerShooter.enabled = true;

        // 4. Reveal Combat HUD
        if (panelHUD != null) panelHUD.SetActive(true);

        isInCombat = true;
        combatTimer = 0f;
    }

    private void ShowMainMenuInstant()
    {
        isInCombat = false;

        if (panelMainMenu != null) panelMainMenu.SetActive(true);
        if (panelSettings != null) panelSettings.SetActive(false);
        if (panelHUD != null) panelHUD.SetActive(false);
        if (panelVictory != null) panelVictory.SetActive(false);

        if (menuCamera != null) menuCamera.Priority = 20;

        // Unlock mouse for menu clicking
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Disable player input during menu
        if (playerLocomotion != null) playerLocomotion.enabled = false;
        if (playerShooter != null) playerShooter.enabled = false;
    }

    // ==========================================
    // SETTINGS PANEL
    // ==========================================
    public void OpenSettings()
    {
        if (panelSettings != null) panelSettings.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void CloseSettings()
    {
        if (panelSettings != null) panelSettings.SetActive(false);

        if (isInCombat)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void InitializeSettingsUI()
    {
        if (SettingsManager.Instance == null) return;

        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.value = SettingsManager.Instance.MasterVolume;
            masterVolumeSlider.onValueChanged.AddListener(v => SettingsManager.Instance.SetMasterVolume(v));
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.value = SettingsManager.Instance.SFXVolume;
            sfxVolumeSlider.onValueChanged.AddListener(v => SettingsManager.Instance.SetSFXVolume(v));
        }

        if (sensitivitySlider != null)
        {
            sensitivitySlider.value = SettingsManager.Instance.MouseSensitivity;
            sensitivitySlider.onValueChanged.AddListener(v => SettingsManager.Instance.SetMouseSensitivity(v));
        }

        if (qualityDropdown != null)
        {
            qualityDropdown.value = SettingsManager.Instance.QualityIndex;
            qualityDropdown.onValueChanged.AddListener(idx => SettingsManager.Instance.SetQuality(idx));
        }

        if (fullscreenToggle != null)
        {
            fullscreenToggle.isOn = SettingsManager.Instance.IsFullscreen;
            fullscreenToggle.onValueChanged.AddListener(isOn => SettingsManager.Instance.SetFullscreen(isOn));
        }
    }

    // ==========================================
    // HUD UPDATES
    // ==========================================
    private void UpdateBossHealth(float current, float max)
    {
        if (bossHealthSlider != null) bossHealthSlider.value = current / max;
    }

    private void UpdateBossShield(float current, float max)
    {
        if (bossShieldSlider != null) bossShieldSlider.value = current / max;
    }

    private void HandleBossStaggered()
    {
        if (bossStatusText != null)
        {
            bossStatusText.text = "⚠ CORE VULNERABLE // 2X CRITICAL ⚠";
            bossStatusText.color = Color.yellow;
        }
    }

    private void HandleBossRecovered()
    {
        if (bossStatusText != null)
        {
            bossStatusText.text = "HOWITZER // HEAVY UNIT-04";
            bossStatusText.color = Color.white;
        }
    }

    public void UpdatePlayerHP(float current, float max)
    {
        if (playerHealthSlider != null) playerHealthSlider.value = current / max;
        if (playerHealthText != null) playerHealthText.text = $"{Mathf.CeilToInt(current)} / {max}";
    }

    private void ShowThreatWarning(string message)
    {
        if (threatWarningText != null)
        {
            threatWarningText.text = message;
            threatWarningText.gameObject.SetActive(true);
        }
    }

    private void HideThreatWarning()
    {
        if (threatWarningText != null) threatWarningText.gameObject.SetActive(false);
    }

    private void HandleAttemptStarted(int attempt)
    {
        if (attemptCountText != null) attemptCountText.text = $"ATTEMPT: {attempt:00}";
        if (echoCountText != null && EchoManager.Instance != null)
        {
            echoCountText.text = $"ECHO SQUAD: {EchoManager.Instance.ActiveEchoCount} / {EchoManager.Instance.MaxCapacity}";
        }

        UpdateBossHealth(1500f, 1500f);
        UpdateBossShield(1000f, 1000f);
        HideThreatWarning();
    }

    private void HandlePlayerDied(int attempt)
    {
        // Brief death feedback
    }

    private void HandleVictory()
    {
        isInCombat = false;
        if (panelHUD != null) panelHUD.SetActive(false);
        if (panelVictory != null) panelVictory.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (victoryStatsText != null)
        {
            int attempts = EncounterManager.Instance != null ? EncounterManager.Instance.CurrentAttempt : 1;
            victoryStatsText.text = $"THREAT NEUTRALIZED\n\nATTEMPTS: {attempts}\nTIME: {Mathf.FloorToInt(combatTimer)}s";
        }
    }

    public void OnReplayButtonClicked()
    {
        if (EncounterManager.Instance != null)
        {
            EncounterManager.Instance.ResetEncounter();
        }
        if (EchoManager.Instance != null)
        {
            EchoManager.Instance.ClearAllEchoHistory();
        }
        ShowMainMenuInstant();
    }

    public void OnQuitButtonClicked()
    {
        Application.Quit();
    }
}