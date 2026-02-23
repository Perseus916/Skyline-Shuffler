using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Level complete popup. Shows stars, coins earned, double reward ad, and navigation.
/// </summary>
public class LevelCompleteUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject panel;
    [SerializeField] private CanvasGroup canvasGroup;
    
    [Header("Display")]
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI movesText;
    [SerializeField] private TextMeshProUGUI coinsEarnedText;
    [SerializeField] private GameObject[] starObjects; // 3 stars
    
    [Header("Buttons")]
    [SerializeField] private Button nextLevelButton;
    [SerializeField] private Button replayButton;
    [SerializeField] private Button menuButton;
    [SerializeField] private Button doubleRewardButton; // Watch ad to 2× coins
    
    [Header("Double Reward")]
    [SerializeField] private TextMeshProUGUI doubleButtonText;
    [SerializeField] private GameObject doubleRewardBadge; // "2×" badge visual
    
    [Header("Animation")]
    [SerializeField] private float starDelay = 0.3f;
    [SerializeField] private float fadeInDuration = 0.3f;
    
    private int earnedStars;
    private int coinsEarned;
    private bool hasDoubled;
    
    private void Awake()
    {
        if (panel != null)
            panel.SetActive(false);
        
        nextLevelButton?.onClick.AddListener(OnNextLevel);
        replayButton?.onClick.AddListener(OnReplay);
        menuButton?.onClick.AddListener(OnMenu);
        doubleRewardButton?.onClick.AddListener(OnDoubleReward);
    }
    
    public void Show(int levelNumber, int movesTaken, int optimalMoves, int stars, int coins)
    {
        earnedStars = stars;
        coinsEarned = coins;
        hasDoubled = false;
        
        if (levelText != null)
            levelText.text = $"Level {levelNumber}";
        
        if (movesText != null)
            movesText.text = $"Completed in {movesTaken} moves";
        
        if (coinsEarnedText != null)
            coinsEarnedText.text = $"+{coins} 🪙";
        
        // Hide all stars initially
        if (starObjects != null)
        {
            foreach (var star in starObjects)
                if (star != null) star.SetActive(false);
        }
        
        // Show double reward button only if ad is available
        if (doubleRewardButton != null)
        {
            bool adReady = AdManager.Instance != null && AdManager.Instance.IsRewardedAdReady();
            doubleRewardButton.gameObject.SetActive(adReady);
            doubleRewardButton.interactable = adReady;
        }
        
        if (doubleButtonText != null)
            doubleButtonText.text = $"📺 Double → +{coins * 2} 🪙";
        
        if (doubleRewardBadge != null)
            doubleRewardBadge.SetActive(true);
        
        if (panel != null)
            panel.SetActive(true);
        
        StartCoroutine(AnimateShow());
    }
    
    // Backward compatible overload
    public void Show(int levelNumber, int movesTaken, int optimalMoves, int stars)
    {
        Show(levelNumber, movesTaken, optimalMoves, stars, 0);
    }
    
    private IEnumerator AnimateShow()
    {
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
        
        if (starObjects != null)
        {
            for (int i = 0; i < earnedStars && i < starObjects.Length; i++)
            {
                yield return new WaitForSeconds(starDelay);
                if (starObjects[i] != null)
                    starObjects[i].SetActive(true);
            }
        }
    }
    
    public void Hide()
    {
        StopAllCoroutines();
        if (panel != null)
            panel.SetActive(false);
    }
    
    private void OnDoubleReward()
    {
        if (hasDoubled || AdManager.Instance == null) return;
        
        AdManager.Instance.ShowDoubleRewardAd(() =>
        {
            hasDoubled = true;
            
            // Grant bonus coins (same amount again = total 2×)
            SaveSystem.AddCoins(coinsEarned);
            
            // Update display
            if (coinsEarnedText != null)
                coinsEarnedText.text = $"+{coinsEarned * 2} 🪙 (2×!)";
            
            // Disable the button
            if (doubleRewardButton != null)
                doubleRewardButton.interactable = false;
            
            if (doubleRewardBadge != null)
                doubleRewardBadge.SetActive(false);
            
            Debug.Log($"<color=green>Double reward! +{coinsEarned} bonus coins (total: {coinsEarned * 2})</color>");
        });
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
            GameManager.Instance.ReplayLevel();
    }
    
    private void OnMenu()
    {
        Hide();
        if (GameManager.Instance != null)
            GameManager.Instance.ShowLevelSelect();
    }
}
