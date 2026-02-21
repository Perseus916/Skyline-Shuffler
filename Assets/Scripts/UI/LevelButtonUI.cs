using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Individual level button in the level select grid.
/// Shows level number, stars, and locked state.
/// </summary>
public class LevelButtonUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button button;
    [SerializeField] private TextMeshProUGUI levelNumberText;
    [SerializeField] private GameObject[] starIcons; // 3 stars
    [SerializeField] private GameObject lockOverlay;
    [SerializeField] private Image backgroundImage;
    
    [Header("Colors")]
    [SerializeField] private Color unlockedColor = new Color(0.3f, 0.7f, 0.3f);
    [SerializeField] private Color completedColor = new Color(0.2f, 0.5f, 0.8f);
    [SerializeField] private Color lockedColor = new Color(0.3f, 0.3f, 0.3f);
    
    private int levelNumber;
    
    /// <summary>
    /// Initialize button with level data
    /// </summary>
    public void Setup(int level)
    {
        levelNumber = level;
        
        if (levelNumberText != null)
            levelNumberText.text = level.ToString();
        
        bool isUnlocked = SaveSystem.IsLevelUnlocked(level);
        LevelProgress progress = SaveSystem.GetLevelProgress(level);
        
        // Lock overlay
        if (lockOverlay != null)
            lockOverlay.SetActive(!isUnlocked);
        
        // Button interactable
        if (button != null)
        {
            button.interactable = isUnlocked;
            button.onClick.AddListener(OnClick);
        }
        
        // Stars
        if (starIcons != null)
        {
            for (int i = 0; i < starIcons.Length; i++)
            {
                if (starIcons[i] != null)
                    starIcons[i].SetActive(i < progress.stars);
            }
        }
        
        // Background color
        if (backgroundImage != null)
        {
            if (!isUnlocked)
                backgroundImage.color = lockedColor;
            else if (progress.completed)
                backgroundImage.color = completedColor;
            else
                backgroundImage.color = unlockedColor;
        }
    }
    
    private void OnClick()
    {
        // Find parent LevelSelectUI and notify
        LevelSelectUI selectUI = GetComponentInParent<LevelSelectUI>();
        if (selectUI != null)
        {
            selectUI.OnLevelSelected(levelNumber);
        }
        else if (GameManager.Instance != null)
        {
            GameManager.Instance.PlayLevel(levelNumber);
        }
    }
}
