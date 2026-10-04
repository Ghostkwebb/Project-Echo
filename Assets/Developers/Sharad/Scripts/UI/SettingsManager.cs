using System;
using UnityEngine;

[DisallowMultipleComponent]
public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }

    public static event Action<float> OnSFXVolumeChanged;

    private const string MasterVolKey = "Settings_MasterVol";
    private const string SFXVolKey = "Settings_SFXVol";
    private const string QualityKey = "Settings_Quality";
    private const string FullscreenKey = "Settings_Fullscreen";
    private const string MouseSensKey = "Settings_MouseSens";
    public float MouseSensitivity { get; private set; } = 1.2f;
    public static event Action<float> OnMouseSensitivityChanged;
    public static float GlobalSFXVolume => Instance != null ? Instance.SFXVolume : 1.0f;

    public float MasterVolume { get; private set; } = 1.0f;
    public float SFXVolume { get; private set; } = 1.0f;
    public int QualityIndex { get; private set; } = 2;
    public bool IsFullscreen { get; private set; } = true;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        LoadAndApplySettings();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void LoadAndApplySettings()
    {
        // 1. Audio
        MasterVolume = PlayerPrefs.GetFloat(MasterVolKey, 1.0f);
        SFXVolume = PlayerPrefs.GetFloat(SFXVolKey, 1.0f);
        AudioListener.volume = MasterVolume;

        // 2. Graphics Quality
        QualityIndex = PlayerPrefs.GetInt(QualityKey, QualitySettings.GetQualityLevel());
        QualitySettings.SetQualityLevel(QualityIndex, true);

        // 3. Fullscreen (PC & WebGL)
        IsFullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
        Screen.fullScreen = IsFullscreen;

        // 4. Mouse Sensitivity
        MouseSensitivity = PlayerPrefs.GetFloat(MouseSensKey, 1.2f);
        OnMouseSensitivityChanged?.Invoke(MouseSensitivity);
    }

    public void SetMasterVolume(float volume)
    {
        MasterVolume = Mathf.Clamp01(volume);
        AudioListener.volume = MasterVolume; // Master scales Unity audio engine globally!
        PlayerPrefs.SetFloat(MasterVolKey, MasterVolume);
        PlayerPrefs.Save();
    }

    public void SetSFXVolume(float volume)
    {
        SFXVolume = Mathf.Clamp01(volume);
        OnSFXVolumeChanged?.Invoke(SFXVolume);
        PlayerPrefs.SetFloat(SFXVolKey, SFXVolume);
        PlayerPrefs.Save();
    }

    public void SetQuality(int index)
    {
        QualityIndex = Mathf.Clamp(index, 0, QualitySettings.names.Length - 1);
        QualitySettings.SetQualityLevel(QualityIndex, true);
        PlayerPrefs.SetInt(QualityKey, QualityIndex);
        PlayerPrefs.Save();
    }

    public void SetFullscreen(bool fullscreen)
    {
        IsFullscreen = fullscreen;
        Screen.fullScreen = fullscreen;
        PlayerPrefs.SetInt(FullscreenKey, fullscreen ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void SetMouseSensitivity(float value)
    {
        MouseSensitivity = Mathf.Clamp(value, 0.2f, 4.0f);
        OnMouseSensitivityChanged?.Invoke(MouseSensitivity);
        PlayerPrefs.SetFloat(MouseSensKey, MouseSensitivity);
        PlayerPrefs.Save();
    }
}