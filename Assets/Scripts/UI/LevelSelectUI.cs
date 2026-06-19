using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Level selection grid. Calls GameManager for navigation (panel toggling).
/// </summary>
public class LevelSelectUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform levelButtonContainer;
    [SerializeField] private GameObject levelButtonPrefab;
    [SerializeField] private Button backButton;
    
    [Header("Display")]
    [SerializeField] private TextMeshProUGUI totalStarsText;
    [SerializeField] private TextMeshProUGUI totalCoinsText;
    
    private void OnEnable()
    {
        backButton?.onClick.RemoveAllListeners();
        backButton?.onClick.AddListener(OnBackClicked);
        
        PopulateLevelButtons();
        UpdateStarsDisplay();
        UpdateCoinsDisplay();
    }

    private void Update()
    {
        UpdateCoinsDisplay();
    }
    
    private void PopulateLevelButtons()
    {
        if (levelButtonPrefab == null || levelButtonContainer == null)
        {
            Debug.LogError("LevelSelectUI: Missing prefab or container!");
            return;
        }
        
        // Clear existing buttons
        foreach (Transform child in levelButtonContainer)
            Destroy(child.gameObject);
        
        int totalLevels = GameManager.Instance != null 
            ? GameManager.Instance.TotalLevels 
            : 0;
            
        for (int i = 1; i <= totalLevels; i++)
        {
            GameObject buttonObj = Instantiate(levelButtonPrefab, levelButtonContainer);
            LevelButtonUI buttonUI = buttonObj.GetComponent<LevelButtonUI>();
            if (buttonUI != null)
                buttonUI.Setup(i);
        }
    }
    
    private void UpdateStarsDisplay()
    {
        if (totalStarsText != null)
            totalStarsText.text = $"{SaveSystem.GetTotalStars()}";
    }
    
    private void UpdateCoinsDisplay()
    {
        if (totalCoinsText != null)
            totalCoinsText.text = $"{SaveSystem.GetCoins()}";
    }
    
    private void OnBackClicked()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.ShowHomeScreen();
    }
    
    /// <summary>
    /// Called by LevelButtonUI when a level is selected
    /// </summary>
    public void OnLevelSelected(int levelNumber)
    {
        if (GameManager.Instance != null)
            GameManager.Instance.PlayLevel(levelNumber);
    }
}
