using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Simple gameplay UI for move counter and level complete display.
/// Connect to GameplayManager events.
/// </summary>
public class GameplayUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameplayManager gameplayManager;
    
    [Header("Move Counter")]
    [SerializeField] private TextMeshProUGUI moveCountText;
    [SerializeField] private string moveFormat = "{0} / {1}";
    
    [Header("Level Complete Panel")]
    [SerializeField] private GameObject levelCompletePanel;
    [SerializeField] private TextMeshProUGUI completeMoveCountText;
    [SerializeField] private GameObject perfectClearBadge;
    
    private void Start()
    {
        // Subscribe to events
        if (gameplayManager != null)
        {
            gameplayManager.OnMoveCountChanged.AddListener(UpdateMoveCounter);
            gameplayManager.OnLevelComplete.AddListener(ShowLevelComplete);
        }
        
        // Hide complete panel on start
        if (levelCompletePanel != null)
            levelCompletePanel.SetActive(false);
    }
    
    private void OnDestroy()
    {
        if (gameplayManager != null)
        {
            gameplayManager.OnMoveCountChanged.RemoveListener(UpdateMoveCounter);
            gameplayManager.OnLevelComplete.RemoveListener(ShowLevelComplete);
        }
    }
    
    private void UpdateMoveCounter(int current, int limit)
    {
        if (moveCountText != null)
        {
            moveCountText.text = string.Format(moveFormat, current, limit);
            
            // Change color when approaching limit
            float ratio = (float)current / limit;
            if (ratio > 0.9f)
                moveCountText.color = Color.red;
            else if (ratio > 0.7f)
                moveCountText.color = new Color(1f, 0.6f, 0f); // Orange
            else
                moveCountText.color = Color.white;
        }
    }
    
    private void ShowLevelComplete()
    {
        if (levelCompletePanel != null)
        {
            levelCompletePanel.SetActive(true);
            
            if (completeMoveCountText != null)
            {
                completeMoveCountText.text = $"Completed in {gameplayManager.GetMoveCount()} moves!";
            }
            
            // Show perfect clear badge if applicable
            if (perfectClearBadge != null)
            {
                // We'd need access to optimalMoves, could be added later
                perfectClearBadge.SetActive(false);
            }
        }
    }
    
    /// <summary>
    /// Call from Next Level button
    /// </summary>
    public void OnNextLevelClicked()
    {
        // TODO: Load next level
        Debug.Log("Next Level clicked - implement level progression");
    }
    
    /// <summary>
    /// Call from Retry button
    /// </summary>
    public void OnRetryClicked()
    {
        // Reload current level
        LevelLoader loader = FindObjectOfType<LevelLoader>();
        if (loader != null)
        {
            loader.LoadLevel();
            if (levelCompletePanel != null)
                levelCompletePanel.SetActive(false);
        }
    }
}
