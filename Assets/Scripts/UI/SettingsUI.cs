using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Settings panel UI. Works with GameManager for toggle visibility.
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
    
    [Header("Reset Confirmation")]
    [SerializeField] private GameObject resetConfirmPanel;
    [SerializeField] private Button confirmResetButton;
    [SerializeField] private Button cancelResetButton;
    
    private void OnEnable()
    {
        SetupListeners();
        LoadValues();
        
        if (resetConfirmPanel != null)
            resetConfirmPanel.SetActive(false);
    }
    
    private void SetupListeners()
    {
        musicSlider?.onValueChanged.RemoveAllListeners();
        sfxSlider?.onValueChanged.RemoveAllListeners();
        vibrationToggle?.onValueChanged.RemoveAllListeners();
        resetProgressButton?.onClick.RemoveAllListeners();
        closeButton?.onClick.RemoveAllListeners();
        confirmResetButton?.onClick.RemoveAllListeners();
        cancelResetButton?.onClick.RemoveAllListeners();
        
        musicSlider?.onValueChanged.AddListener(v => { if (GameManager.Instance) GameManager.Instance.MusicVolume = v; });
        sfxSlider?.onValueChanged.AddListener(v => { if (GameManager.Instance) GameManager.Instance.SFXVolume = v; });
        vibrationToggle?.onValueChanged.AddListener(v => { if (GameManager.Instance) GameManager.Instance.VibrationEnabled = v; });
        
        resetProgressButton?.onClick.AddListener(() => { if (resetConfirmPanel) resetConfirmPanel.SetActive(true); });
        cancelResetButton?.onClick.AddListener(() => { if (resetConfirmPanel) resetConfirmPanel.SetActive(false); });
        confirmResetButton?.onClick.AddListener(ConfirmReset);
        closeButton?.onClick.AddListener(Close);
    }
    
    private void LoadValues()
    {
        if (GameManager.Instance == null) return;
        
        if (musicSlider != null) musicSlider.SetValueWithoutNotify(GameManager.Instance.MusicVolume);
        if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(GameManager.Instance.SFXVolume);
        if (vibrationToggle != null) vibrationToggle.SetIsOnWithoutNotify(GameManager.Instance.VibrationEnabled);
    }
    
    public void Close()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.HideSettings();
    }
    
    private void ConfirmReset()
    {
        SaveSystem.ResetAllProgress();
        if (resetConfirmPanel != null) resetConfirmPanel.SetActive(false);
        Debug.Log("<color=yellow>Progress reset!</color>");
        
        // Go home
        if (GameManager.Instance != null)
            GameManager.Instance.ShowHomeScreen();
    }
}
