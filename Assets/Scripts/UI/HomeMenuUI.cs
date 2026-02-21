using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Home screen main menu UI.
/// </summary>
public class HomeMenuUI : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Button levelsButton;
    [SerializeField] private Button settingsButton;
    
    [Header("Continue Button State")]
    [SerializeField] private GameObject continueActiveState;
    [SerializeField] private GameObject continueDisabledState;
    [SerializeField] private TextMeshProUGUI continueLevelText;
    
    [Header("Stats Display")]
    [SerializeField] private TextMeshProUGUI totalStarsText;
    [SerializeField] private TextMeshProUGUI levelsCompletedText;
    
    [Header("Panels")]
    [SerializeField] private GameObject settingsPanel;
    
    private void Start()
    {
        SetupButtons();
        UpdateUI();
    }
    
    private void SetupButtons()
    {
        continueButton?.onClick.AddListener(OnContinueClicked);
        levelsButton?.onClick.AddListener(OnLevelsClicked);
        settingsButton?.onClick.AddListener(OnSettingsClicked);
    }
    
    private void UpdateUI()
    {
        // Check if there's a game to continue
        bool hasProgress = SaveSystem.Data.currentLevel > 1 || SaveSystem.Data.hasInProgressGame;
        
        if (continueActiveState != null)
            continueActiveState.SetActive(hasProgress);
        if (continueDisabledState != null)
            continueDisabledState.SetActive(!hasProgress);
        
        continueButton.interactable = hasProgress;
        
        // Show what level they'll continue to
        if (continueLevelText != null && hasProgress)
        {
            if (SaveSystem.Data.hasInProgressGame)
                continueLevelText.text = $"Resume Level {SaveSystem.Data.inProgressLevel}";
            else
                continueLevelText.text = $"Level {SaveSystem.Data.currentLevel}";
        }
        
        // Stats
        if (totalStarsText != null)
            totalStarsText.text = SaveSystem.GetTotalStars().ToString();
        
        if (levelsCompletedText != null)
            levelsCompletedText.text = $"{SaveSystem.Data.levelProgress.Count} Levels";
    }
    
    private void OnContinueClicked()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.ContinueGame();
    }
    
    private void OnLevelsClicked()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.LoadLevelSelectScene();
    }
    
    private void OnSettingsClicked()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(true);
    }
}
