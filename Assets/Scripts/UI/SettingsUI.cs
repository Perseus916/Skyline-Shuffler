using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple settings panel for basic options.
/// </summary>
public class SettingsUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject panel;
    
    [Header("Audio")]
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;
    
    [Header("Other")]
    [SerializeField] private Toggle vibrationToggle;
    [SerializeField] private Button resetProgressButton;
    [SerializeField] private Button closeButton;
    
    [Header("Confirmation")]
    [SerializeField] private GameObject resetConfirmPanel;
    [SerializeField] private Button confirmResetButton;
    [SerializeField] private Button cancelResetButton;
    
    private void Start()
    {
        SetupUI();
        LoadSettings();
    }
    
    private void SetupUI()
    {
        musicSlider?.onValueChanged.AddListener(OnMusicChanged);
        sfxSlider?.onValueChanged.AddListener(OnSFXChanged);
        vibrationToggle?.onValueChanged.AddListener(OnVibrationChanged);
        
        resetProgressButton?.onClick.AddListener(ShowResetConfirm);
        closeButton?.onClick.AddListener(Close);
        
        confirmResetButton?.onClick.AddListener(ConfirmReset);
        cancelResetButton?.onClick.AddListener(HideResetConfirm);
        
        if (resetConfirmPanel != null)
            resetConfirmPanel.SetActive(false);
    }
    
    private void LoadSettings()
    {
        if (GameManager.Instance != null)
        {
            if (musicSlider != null)
                musicSlider.value = GameManager.Instance.MusicVolume;
            
            if (sfxSlider != null)
                sfxSlider.value = GameManager.Instance.SFXVolume;
            
            if (vibrationToggle != null)
                vibrationToggle.isOn = GameManager.Instance.VibrationEnabled;
        }
    }
    
    public void Open()
    {
        if (panel != null)
            panel.SetActive(true);
        LoadSettings();
    }
    
    public void Close()
    {
        if (panel != null)
            panel.SetActive(false);
    }
    
    private void OnMusicChanged(float value)
    {
        if (GameManager.Instance != null)
            GameManager.Instance.MusicVolume = value;
    }
    
    private void OnSFXChanged(float value)
    {
        if (GameManager.Instance != null)
            GameManager.Instance.SFXVolume = value;
    }
    
    private void OnVibrationChanged(bool value)
    {
        if (GameManager.Instance != null)
            GameManager.Instance.VibrationEnabled = value;
    }
    
    private void ShowResetConfirm()
    {
        if (resetConfirmPanel != null)
            resetConfirmPanel.SetActive(true);
    }
    
    private void HideResetConfirm()
    {
        if (resetConfirmPanel != null)
            resetConfirmPanel.SetActive(false);
    }
    
    private void ConfirmReset()
    {
        SaveSystem.ResetAllProgress();
        HideResetConfirm();
        Debug.Log("<color=yellow>All progress has been reset!</color>");
        
        // Reload current scene to refresh UI
        if (GameManager.Instance != null)
            GameManager.Instance.LoadHomeScene();
    }
}
