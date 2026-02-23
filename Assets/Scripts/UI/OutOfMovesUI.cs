using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Popup shown when player runs out of moves.
/// Offers: Watch Ad (+5 moves), Spend Coins (+5 moves), or Restart.
/// Designed to feel like a helpful second chance, not a dead end.
/// </summary>
public class OutOfMovesUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject panel;
    [SerializeField] private CanvasGroup canvasGroup;
    
    [Header("Display")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI subtitleText;
    
    [Header("Buttons")]
    [SerializeField] private Button watchAdButton;
    [SerializeField] private Button spendCoinsButton;
    [SerializeField] private Button restartButton;
    
    [Header("Button Labels")]
    [SerializeField] private TextMeshProUGUI adButtonText;
    [SerializeField] private TextMeshProUGUI coinButtonText;
    
    [Header("Settings")]
    [SerializeField] private int extraMovesFromAd = 5;
    [SerializeField] private int extraMovesFromCoins = 5;
    [SerializeField] private int coinCostForMoves = 75;
    
    [Header("Animation")]
    [SerializeField] private float fadeInDuration = 0.3f;
    
    private GameplayManager gameplayManager;
    
    private void Awake()
    {
        if (panel != null)
            panel.SetActive(false);
        
        watchAdButton?.onClick.AddListener(OnWatchAd);
        spendCoinsButton?.onClick.AddListener(OnSpendCoins);
        restartButton?.onClick.AddListener(OnRestart);
    }
    
    public void Show(GameplayManager manager)
    {
        gameplayManager = manager;
        
        if (titleText != null)
            titleText.text = "Out of Moves!";
        
        if (subtitleText != null)
            subtitleText.text = "Don't give up — you're almost there!";
        
        if (adButtonText != null)
            adButtonText.text = $"📺 Watch Ad\n+{extraMovesFromAd} Moves";
        
        if (coinButtonText != null)
            coinButtonText.text = $"🪙 {coinCostForMoves} Coins\n+{extraMovesFromCoins} Moves";
        
        // Disable ad button if not available
        if (watchAdButton != null)
        {
            bool adReady = AdManager.Instance != null && AdManager.Instance.IsRewardedAdReady();
            watchAdButton.interactable = adReady;
        }
        
        // Disable coin button if insufficient
        if (spendCoinsButton != null)
        {
            spendCoinsButton.interactable = SaveSystem.GetCoins() >= coinCostForMoves;
        }
        
        if (panel != null)
            panel.SetActive(true);
        
        StartCoroutine(AnimateFadeIn());
    }
    
    private IEnumerator AnimateFadeIn()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0;
            float elapsed = 0;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = elapsed / fadeInDuration;
                yield return null;
            }
            canvasGroup.alpha = 1;
        }
    }
    
    public void Hide()
    {
        StopAllCoroutines();
        if (panel != null)
            panel.SetActive(false);
    }
    
    private void OnWatchAd()
    {
        if (AdManager.Instance == null) return;
        
        AdManager.Instance.ShowExtraMovesAd(() =>
        {
            // Grant extra moves
            if (gameplayManager != null)
                gameplayManager.AddExtraMoves(extraMovesFromAd);
            
            Hide();
            Debug.Log($"<color=green>Ad watched! +{extraMovesFromAd} extra moves</color>");
        });
    }
    
    private void OnSpendCoins()
    {
        if (!SaveSystem.SpendCoins(coinCostForMoves))
        {
            Debug.Log("<color=red>Not enough coins!</color>");
            return;
        }
        
        if (gameplayManager != null)
            gameplayManager.AddExtraMoves(extraMovesFromCoins);
        
        Hide();
        Debug.Log($"<color=cyan>Spent {coinCostForMoves} coins for +{extraMovesFromCoins} moves</color>");
    }
    
    private void OnRestart()
    {
        Hide();
        if (GameManager.Instance != null)
            GameManager.Instance.ReplayLevel();
    }
}
