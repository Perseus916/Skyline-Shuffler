using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Level selection screen with scrollable grid of level buttons.
/// </summary>
public class LevelSelectUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform levelButtonContainer;
    [SerializeField] private GameObject levelButtonPrefab;
    [SerializeField] private Button backButton;
    
    [Header("Display")]
    [SerializeField] private TextMeshProUGUI totalStarsText;
    [SerializeField] private int levelsToShow = 100;
    
    private void Start()
    {
        backButton?.onClick.AddListener(OnBackClicked);
        PopulateLevelButtons();
        UpdateStarsDisplay();
    }
    
    private void PopulateLevelButtons()
    {
        if (levelButtonPrefab == null || levelButtonContainer == null)
        {
            Debug.LogError("LevelSelectUI: Missing prefab or container reference!");
            return;
        }
        
        // Clear existing
        foreach (Transform child in levelButtonContainer)
        {
            Destroy(child.gameObject);
        }
        
        // Create buttons
        int totalLevels = GameManager.Instance != null 
            ? GameManager.Instance.totalLevelsAvailable 
            : levelsToShow;
            
        for (int i = 1; i <= Mathf.Min(totalLevels, levelsToShow); i++)
        {
            GameObject buttonObj = Instantiate(levelButtonPrefab, levelButtonContainer);
            LevelButtonUI buttonUI = buttonObj.GetComponent<LevelButtonUI>();
            
            if (buttonUI != null)
            {
                buttonUI.Setup(i);
            }
        }
    }
    
    private void UpdateStarsDisplay()
    {
        if (totalStarsText != null)
        {
            totalStarsText.text = $"⭐ {SaveSystem.GetTotalStars()}";
        }
    }
    
    private void OnBackClicked()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.LoadHomeScene();
    }
    
    /// <summary>
    /// Called by level buttons
    /// </summary>
    public void OnLevelSelected(int levelNumber)
    {
        if (GameManager.Instance != null)
            GameManager.Instance.PlayLevel(levelNumber);
    }
}
