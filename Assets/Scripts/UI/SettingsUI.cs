using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Settings panel UI. Works with GameManager for toggle visibility.
/// </summary>
public class SettingsUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject panel;
    [SerializeField] private GameObject progressPanel;
    
    [Header("Audio")]
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;
    
    [Header("Other")]
    [SerializeField] private Toggle vibrationToggle;
    [SerializeField] private Button progressButton;
    [SerializeField] private Button closeButton;
    
    private void OnEnable()
    {
        ShowSettingsMenu();
        SetupListeners();
        LoadValues();
    }

    public void ShowSettingsMenu()
    {
        if (progressPanel != null)
            progressPanel.SetActive(false);

        for (int i = 0; i < transform.childCount; i++)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if (child != progressPanel && child.name != "Reset panel")
            {
                child.SetActive(true);
            }
        }
    }
    
    private void SetupListeners()
    {
        musicSlider?.onValueChanged.RemoveAllListeners();
        sfxSlider?.onValueChanged.RemoveAllListeners();
        vibrationToggle?.onValueChanged.RemoveAllListeners();
        progressButton?.onClick.RemoveAllListeners();
        closeButton?.onClick.RemoveAllListeners();
        
        musicSlider?.onValueChanged.AddListener(v => { if (GameManager.Instance) GameManager.Instance.MusicVolume = v; });
        sfxSlider?.onValueChanged.AddListener(v => { if (GameManager.Instance) GameManager.Instance.SFXVolume = v; });
        vibrationToggle?.onValueChanged.AddListener(v => { if (GameManager.Instance) GameManager.Instance.VibrationEnabled = v; });
        
        progressButton?.onClick.AddListener(OpenProgressPanel);
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
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayButtonClick();

        if (panel != null)
            panel.SetActive(false);

        if (GameManager.Instance != null)
            GameManager.Instance.HideSettings();
    }
    
    private void OpenProgressPanel()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayButtonClick();

        if (progressPanel != null)
            progressPanel.SetActive(true);

        for (int i = 0; i < transform.childCount; i++)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if (child != progressPanel && child.name != "Reset panel")
            {
                child.SetActive(false);
            }
        }
    }
}
