using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Level complete popup shown when player finishes a level.
/// Displays stars with animation and navigation buttons.
/// </summary>
public class LevelCompleteUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject panel;
    [SerializeField] private CanvasGroup canvasGroup;
    
    [Header("Display")]
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI movesText;
    [SerializeField] private GameObject[] starObjects; // 3 stars
    
    [Header("Buttons")]
    [SerializeField] private Button nextLevelButton;
    [SerializeField] private Button replayButton;
    [SerializeField] private Button menuButton;
    
    [Header("Animation")]
    [SerializeField] private float starDelay = 0.3f;
    [SerializeField] private float fadeInDuration = 0.3f;
    
    private int earnedStars;
    
    private void Awake()
    {
        // Hide on start
        if (panel != null)
            panel.SetActive(false);
        
        // Setup buttons
        nextLevelButton?.onClick.AddListener(OnNextLevel);
        replayButton?.onClick.AddListener(OnReplay);
        menuButton?.onClick.AddListener(OnMenu);
    }
    
    /// <summary>
    /// Show the level complete popup
    /// </summary>
    public void Show(int levelNumber, int movesTaken, int optimalMoves, int stars)
    {
        earnedStars = stars;
        
        if (levelText != null)
            levelText.text = $"Level {levelNumber}";
        
        if (movesText != null)
            movesText.text = $"Completed in {movesTaken} moves";
        
        // Hide all stars initially
        if (starObjects != null)
        {
            foreach (var star in starObjects)
            {
                if (star != null)
                    star.SetActive(false);
            }
        }
        
        // Show panel
        if (panel != null)
            panel.SetActive(true);
        
        // Animate
        StartCoroutine(AnimateShow());
    }
    
    private IEnumerator AnimateShow()
    {
        // Fade in
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0;
            float elapsed = 0;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = elapsed / fadeInDuration;
                yield return null;
            }
            canvasGroup.alpha = 1;
        }
        
        // Animate stars one by one
        if (starObjects != null)
        {
            for (int i = 0; i < earnedStars && i < starObjects.Length; i++)
            {
                yield return new WaitForSeconds(starDelay);
                if (starObjects[i] != null)
                {
                    starObjects[i].SetActive(true);
                    // Could add scale punch animation here
                }
            }
        }
    }
    
    public void Hide()
    {
        if (panel != null)
            panel.SetActive(false);
    }
    
    private void OnNextLevel()
    {
        Hide();
        if (GameManager.Instance != null)
            GameManager.Instance.LoadNextLevel();
    }
    
    private void OnReplay()
    {
        Hide();
        if (GameManager.Instance != null)
        {
            int currentLevel = GameManager.Instance.SelectedLevel;
            GameManager.Instance.ReplayLevel(currentLevel);
        }
    }
    
    private void OnMenu()
    {
        Hide();
        if (GameManager.Instance != null)
            GameManager.Instance.LoadLevelSelectScene();
    }
}
