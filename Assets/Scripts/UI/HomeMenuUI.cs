using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Home screen UI. Single "Play" button that always loads the right level.
/// </summary>
public class HomeMenuUI : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button levelsButton;
    [SerializeField] private Button settingsButton;
    
    [Header("Display")]
    [SerializeField] private TextMeshProUGUI playButtonText;
    [SerializeField] private TextMeshProUGUI totalStarsText;
    
    private void OnEnable()
    {
        SetupButtons();
        UpdateUI();
    }
    
    private void SetupButtons()
    {
        playButton?.onClick.RemoveAllListeners();
        levelsButton?.onClick.RemoveAllListeners();
        settingsButton?.onClick.RemoveAllListeners();
        
        playButton?.onClick.AddListener(OnPlayClicked);
        levelsButton?.onClick.AddListener(OnLevelsClicked);
        settingsButton?.onClick.AddListener(OnSettingsClicked);
    }
    
    private void UpdateUI()
    {
        // Show what level they'll play
        if (playButtonText != null)
        {
            int nextLevel = SaveSystem.Data.currentLevel;
            if (nextLevel <= 1)
                playButtonText.text = "Play";
            else
                playButtonText.text = $"Level {nextLevel}";
        }
        
        if (totalStarsText != null)
            totalStarsText.text = $"⭐ {SaveSystem.GetTotalStars()}";
    }
    
    private void OnPlayClicked()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.ContinueGame();
    }
    
    private void OnLevelsClicked()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.ShowLevelSelect();
    }
    
    private void OnSettingsClicked()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.ShowSettings();
    }
}
