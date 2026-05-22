using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// In-game HUD: move counter, level number, coins, undo/hint buttons with ad fallback.
/// Shows free count → coin cost → ad icon as player exhausts options.
/// </summary>
public class GameplayUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameplayManager gameplayManager;
    
    [Header("HUD")]
    [SerializeField] private TextMeshProUGUI moveCountText;
    [SerializeField] private TextMeshProUGUI levelNumberText;
    [SerializeField] private TextMeshProUGUI coinText;
    [SerializeField] private string moveFormat = "{0} / {1}";
    
    [Header("Progress Bar")]
    public RectTransform fill;
    public float maxWidth = 320f;
    [Tooltip("If true, the bar drains from full to empty as moves are used. If false, it fills from empty to full.")]
    [SerializeField] private bool drainProgressBar = true;
    
    [Header("Buttons")]
    [SerializeField] private Button pauseButton;
    [SerializeField] private Button homeButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button undoButton;
    [SerializeField] private Button hintButton;
    
    [Header("Undo/Hint Labels")]
    [SerializeField] private TextMeshProUGUI undoCountText;
    [SerializeField] private TextMeshProUGUI hintCountText;
    
    [Header("Ad Indicators")]
    [SerializeField] private GameObject undoAdIcon;  // Small video icon shown when free = 0
    [SerializeField] private GameObject hintAdIcon;
    
    // Costs (must match GameplayManager)
    private const int UNDO_COIN_COST = 75;
    private const int HINT_COIN_COST = 150;
    
    private void OnEnable()
    {
        if (gameplayManager != null)
        {
            gameplayManager.OnMoveCountChanged.AddListener(UpdateMoveCounter);
            gameplayManager.OnUndoCountChanged.AddListener(UpdateUndoCount);
            gameplayManager.OnHintCountChanged.AddListener(UpdateHintCount);
            gameplayManager.OnCoinsChanged.AddListener(UpdateCoins);
        }
        
        pauseButton?.onClick.RemoveAllListeners();
        homeButton?.onClick.RemoveAllListeners();
        restartButton?.onClick.RemoveAllListeners();
        undoButton?.onClick.RemoveAllListeners();
        hintButton?.onClick.RemoveAllListeners();
        
        pauseButton?.onClick.AddListener(OnPauseClicked);
        homeButton?.onClick.AddListener(OnHomeClicked);
        restartButton?.onClick.AddListener(OnRestartClicked);
        undoButton?.onClick.AddListener(OnUndoClicked);
        hintButton?.onClick.AddListener(OnHintClicked);
        
        // Show level number
        if (levelNumberText != null && GameManager.Instance != null)
        {
            levelNumberText.text = $"Level {GameManager.Instance.SelectedLevel}";
        }
        
        // Show initial values
        UpdateCoins(SaveSystem.GetCoins());
        UpdateUndoCount(SaveSystem.GetFreeUndos());
        UpdateHintCount(SaveSystem.GetFreeHints());
    }
    
    private void OnDisable()
    {
        if (gameplayManager != null)
        {
            gameplayManager.OnMoveCountChanged.RemoveListener(UpdateMoveCounter);
            gameplayManager.OnUndoCountChanged.RemoveListener(UpdateUndoCount);
            gameplayManager.OnHintCountChanged.RemoveListener(UpdateHintCount);
            gameplayManager.OnCoinsChanged.RemoveListener(UpdateCoins);
        }
    }
    
    private void UpdateMoveCounter(int current, int limit)
    {
        if (moveCountText != null)
        {
            moveCountText.text = string.Format(moveFormat, current, limit);
            
            float ratio = (float)current / limit;
            if (ratio > 0.9f)
                moveCountText.color = Color.red;
            else if (ratio > 0.7f)
                moveCountText.color = new Color(1f, 0.6f, 0f); // Orange warning
            else
                moveCountText.color = Color.white;
        }
        
        if (fill != null)
        {
            float percent = limit > 0 ? (float)current / limit : 0f;
            if (drainProgressBar)
            {
                percent = 1f - percent;
            }
            SetProgress(Mathf.Clamp01(percent));
        }
    }
    
    public void SetProgress(float percent)
    {
        if (fill != null)
        {
            fill.sizeDelta = new Vector2(maxWidth * percent, 12f);
        }
    }
    
    private void UpdateUndoCount(int freeCount)
    {
        bool hasFree = freeCount > 0;
        bool hasCoins = SaveSystem.GetCoins() >= UNDO_COIN_COST;
        bool hasAd = AdManager.Instance != null && AdManager.Instance.IsRewardedAdReady();
        
        if (undoCountText != null)
        {
            if (hasFree)
                undoCountText.text = freeCount.ToString();
            else if (hasCoins)
                undoCountText.text = $"{UNDO_COIN_COST}";
            else if (hasAd)
                undoCountText.text = "📺";
            else
                undoCountText.text = "—";
        }
        
        // Show/hide ad icon
        if (undoAdIcon != null)
            undoAdIcon.SetActive(!hasFree && !hasCoins && hasAd);
    }
    
    private void UpdateHintCount(int freeCount)
    {
        bool hasFree = freeCount > 0;
        bool hasCoins = SaveSystem.GetCoins() >= HINT_COIN_COST;
        bool hasAd = AdManager.Instance != null && AdManager.Instance.IsRewardedAdReady();
        
        if (hintCountText != null)
        {
            if (hasFree)
                hintCountText.text = freeCount.ToString();
            else if (hasCoins)
                hintCountText.text = $"{HINT_COIN_COST}";
            else if (hasAd)
                hintCountText.text = "📺";
            else
                hintCountText.text = "—";
        }
        
        // Show/hide ad icon
        if (hintAdIcon != null)
            hintAdIcon.SetActive(!hasFree && !hasCoins && hasAd);
    }
    
    private void UpdateCoins(int total)
    {
        if (coinText != null)
            coinText.text = total.ToString();
        
        // Refresh undo/hint labels since coin count affects what's shown
        UpdateUndoCount(SaveSystem.GetFreeUndos());
        UpdateHintCount(SaveSystem.GetFreeHints());
    }
    
    private void OnPauseClicked()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.ShowHomeScreen();
    }
    
    private void OnHomeClicked()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.ShowHomeScreen();
    }

    private void OnRestartClicked()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.ReplayLevel();
    }
    
    private void OnUndoClicked()
    {
        if (gameplayManager != null)
            gameplayManager.TryUndo();
    }
    
    private void OnHintClicked()
    {
        if (gameplayManager != null)
            gameplayManager.TryShowHint();
    }
}
