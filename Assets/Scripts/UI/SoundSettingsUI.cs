using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Mobile-friendly sound settings panel.
/// Controls master volume, music, SFX, and haptics.
/// </summary>
public class SoundSettingsUI : MonoBehaviour
{
    [Header("Sliders")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;

    [Header("Toggles")]
    [SerializeField] private Toggle hapticsToggle;

    [Header("Buttons")]
    [SerializeField] private Button closeButton;

    private void OnEnable()
    {
        SetupListeners();
        LoadValues();
    }

    private void OnDisable()
    {
        RemoveListeners();
    }

    private void SetupListeners()
    {
        RemoveListeners();

        if (masterSlider != null)
            masterSlider.onValueChanged.AddListener(HandleMasterVolumeChanged);

        if (musicSlider != null)
            musicSlider.onValueChanged.AddListener(HandleMusicVolumeChanged);

        if (sfxSlider != null)
            sfxSlider.onValueChanged.AddListener(HandleSfxVolumeChanged);

        if (hapticsToggle != null)
            hapticsToggle.onValueChanged.AddListener(HandleHapticsChanged);

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);
    }

    private void RemoveListeners()
    {
        if (masterSlider != null)
            masterSlider.onValueChanged.RemoveListener(HandleMasterVolumeChanged);

        if (musicSlider != null)
            musicSlider.onValueChanged.RemoveListener(HandleMusicVolumeChanged);

        if (sfxSlider != null)
            sfxSlider.onValueChanged.RemoveListener(HandleSfxVolumeChanged);

        if (hapticsToggle != null)
            hapticsToggle.onValueChanged.RemoveListener(HandleHapticsChanged);

        if (closeButton != null)
            closeButton.onClick.RemoveListener(Close);
    }

    private void LoadValues()
    {
        if (masterSlider != null)
            masterSlider.SetValueWithoutNotify(SaveSystem.Data.masterVolume);

        if (musicSlider != null)
            musicSlider.SetValueWithoutNotify(SaveSystem.Data.musicVolume);

        if (sfxSlider != null)
            sfxSlider.SetValueWithoutNotify(SaveSystem.Data.sfxVolume);

        if (hapticsToggle != null)
            hapticsToggle.SetIsOnWithoutNotify(SaveSystem.Data.vibrationEnabled);
    }

    private void HandleMasterVolumeChanged(float value)
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetMasterVolume(value);
        else
        {
            SaveSystem.Data.masterVolume = value;
            SaveSystem.Save();
        }
    }

    private void HandleMusicVolumeChanged(float value)
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetMusicVolume(value);
        else
        {
            SaveSystem.Data.musicVolume = value;
            SaveSystem.Save();
        }
    }

    private void HandleSfxVolumeChanged(float value)
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetSfxVolume(value);
        else
        {
            SaveSystem.Data.sfxVolume = value;
            SaveSystem.Save();
        }
    }

    private void HandleHapticsChanged(bool enabled)
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetVibration(enabled);
        else
        {
            SaveSystem.Data.vibrationEnabled = enabled;
            SaveSystem.Save();
        }
    }

    public void Close()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayButtonClick();
            
        gameObject.SetActive(false);
    }
}
