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
    [SerializeField] private TextMeshProUGUI totalStarsText;
    [SerializeField] private string moveFormat = "{0} / {1}";
    [Tooltip("If true, shows remaining moves (limit - current). If false, shows moves used (current).")]
    [SerializeField] private bool showRemainingMoves = true;
    
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
    [SerializeField] private Button unlockButton;  // Unlocks one locked block
    
    [Header("Undo/Hint Labels")]
    [SerializeField] private Text undoCountText;
    [SerializeField] private Text hintCountText;
    
    [Header("Unlock Button")]
    [SerializeField] private Text unlockCountText;       // Shows remaining locked blocks
    [SerializeField] private GameObject unlockAdIcon;    // Shown when player has no coins but ad ready
    [SerializeField] private GameObject unlockButtonRoot; // Parent to hide when no locked blocks
    
    [Header("Ad Indicators")]
    [SerializeField] private GameObject undoAdIcon;  // Small video icon shown when free = 0
    [SerializeField] private GameObject hintAdIcon;
    
    // Costs (must match GameplayManager)
    private const int UNDO_COIN_COST = 75;
    private const int HINT_COIN_COST = 150;
    private const int UNLOCK_COIN_COST = 200;
    
    private void OnEnable()
    {
        if (gameplayManager != null)
        {
            gameplayManager.OnMoveCountChanged.AddListener(UpdateMoveCounter);
            gameplayManager.OnUndoCountChanged.AddListener(UpdateUndoCount);
            gameplayManager.OnHintCountChanged.AddListener(UpdateHintCount);
            gameplayManager.OnCoinsChanged.AddListener(UpdateCoins);
            gameplayManager.OnLockedBlockCountChanged.AddListener(UpdateUnlockButton);
        }
        
        pauseButton?.onClick.RemoveAllListeners();
        homeButton?.onClick.RemoveAllListeners();
        restartButton?.onClick.RemoveAllListeners();
        undoButton?.onClick.RemoveAllListeners();
        hintButton?.onClick.RemoveAllListeners();
        unlockButton?.onClick.RemoveAllListeners();
        
        pauseButton?.onClick.AddListener(OnPauseClicked);
        homeButton?.onClick.AddListener(OnHomeClicked);
        restartButton?.onClick.AddListener(OnRestartClicked);
        undoButton?.onClick.AddListener(OnUndoClicked);
        hintButton?.onClick.AddListener(OnHintClicked);
        unlockButton?.onClick.AddListener(OnUnlockClicked);
        
        // Show level number
        if (levelNumberText != null && GameManager.Instance != null)
        {
            levelNumberText.text = $"Level {GameManager.Instance.SelectedLevel}";
        }
        
        // Show initial values
        UpdateCoins(SaveSystem.GetCoins());
        UpdateUndoCount(SaveSystem.GetFreeUndos());
        UpdateHintCount(SaveSystem.GetFreeHints());
        UpdateTotalStars();
        // Unlock button: start with 0 locked (will be refreshed via event when level loads)
        UpdateUnlockButton(0);
        
        if (gameplayManager != null)
        {
            UpdateMoveCounter(gameplayManager.GetMoveCount(), gameplayManager.GetMoveLimit());
        }
    }
    
    private void OnDisable()
    {
        if (gameplayManager != null)
        {
            gameplayManager.OnMoveCountChanged.RemoveListener(UpdateMoveCounter);
            gameplayManager.OnUndoCountChanged.RemoveListener(UpdateUndoCount);
            gameplayManager.OnHintCountChanged.RemoveListener(UpdateHintCount);
            gameplayManager.OnCoinsChanged.RemoveListener(UpdateCoins);
            gameplayManager.OnLockedBlockCountChanged.RemoveListener(UpdateUnlockButton);
        }
    }
    
    private void UpdateMoveCounter(int current, int limit)
    {
        if (moveCountText != null)
        {
            int displayMoves = showRemainingMoves ? Mathf.Max(0, limit - current) : current;
            moveCountText.text = string.Format(moveFormat, displayMoves, limit);
            
            float ratio = limit > 0 ? (float)current / limit : 0f;
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
        
        string displayStr = freeCount.ToString();
            
        SetButtonText(undoButton, undoCountText, displayStr);
        
        // Show/hide ad icon (only if it is a separate GameObject from the button itself)
        if (undoAdIcon != null && (undoButton == null || undoAdIcon != undoButton.gameObject))
            undoAdIcon.SetActive(!hasFree && !hasCoins && hasAd);
    }
    
    private void UpdateHintCount(int freeCount)
    {
        bool hasFree = freeCount > 0;
        bool hasCoins = SaveSystem.GetCoins() >= HINT_COIN_COST;
        bool hasAd = AdManager.Instance != null && AdManager.Instance.IsRewardedAdReady();
        
        string displayStr = freeCount.ToString();
            
        SetButtonText(hintButton, hintCountText, displayStr);
        
        // Show/hide ad icon (only if it is a separate GameObject from the button itself)
        if (hintAdIcon != null && (hintButton == null || hintAdIcon != hintButton.gameObject))
            hintAdIcon.SetActive(!hasFree && !hasCoins && hasAd);
    }

    private void SetButtonText(Button button, Text textComp, string text)
    {
        if (textComp != null)
        {
            textComp.text = text;
        }
        else if (button != null)
        {
            // Fallback: try to find TextMeshProUGUI in children
            var tmp = button.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.text = text;
                return;
            }
            
            // Fallback: try to find legacy Text in children
            var txt = button.GetComponentInChildren<Text>();
            if (txt != null)
            {
                txt.text = text;
            }
        }
    }
    
    private void UpdateCoins(int total)
    {
        if (coinText != null)
            coinText.text = total.ToString();
        
        // Refresh undo/hint labels since coin count affects what's shown
        UpdateUndoCount(SaveSystem.GetFreeUndos());
        UpdateHintCount(SaveSystem.GetFreeHints());
        UpdateTotalStars();
        
        // Refresh unlock button ad icon state when coins change
        bool hasCoinsForUnlock = total >= UNLOCK_COIN_COST;
        bool hasAdForUnlock = AdManager.Instance != null && AdManager.Instance.IsRewardedAdReady();
        if (unlockAdIcon != null && (unlockButton == null || unlockAdIcon != unlockButton.gameObject))
        {
            // Only show ad icon if there are still locked blocks (button is visible and interactable)
            bool unlockVisible = unlockButton != null && unlockButton.gameObject.activeSelf;
            unlockAdIcon.SetActive(unlockVisible && !hasCoinsForUnlock && hasAdForUnlock);
        }
    }

    private void UpdateTotalStars()
    {
        if (totalStarsText != null)
            totalStarsText.text = $"{SaveSystem.GetTotalStars()}";
    }
    
    /// <summary>
    /// Update the Unlock button's label and visibility based on how many locked blocks remain.
    /// </summary>
    private void UpdateUnlockButton(int lockedCount)
    {
        // Hide the entire unlock button root if there are no locked blocks
        if (unlockButtonRoot != null)
        {
            unlockButtonRoot.SetActive(lockedCount > 0);
        }
        else if (unlockButton != null)
        {
            unlockButton.gameObject.SetActive(lockedCount > 0);
        }
        
        // Update the count label
        if (unlockCountText != null)
            unlockCountText.text = lockedCount.ToString();
        else if (unlockButton != null)
        {
            var tmp = unlockButton.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null) tmp.text = lockedCount.ToString();
        }
        
        // Show/hide ad icon
        bool hasCoins = SaveSystem.GetCoins() >= UNLOCK_COIN_COST;
        bool hasAd = AdManager.Instance != null && AdManager.Instance.IsRewardedAdReady();
        if (unlockAdIcon != null && (unlockButton == null || unlockAdIcon != unlockButton.gameObject))
            unlockAdIcon.SetActive(lockedCount > 0 && !hasCoins && hasAd);
        
        // Disable button if nothing to unlock
        if (unlockButton != null)
            unlockButton.interactable = lockedCount > 0;
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
    
    private void OnUnlockClicked()
    {
        if (gameplayManager != null)
            gameplayManager.TryUnlockBlock();
    }
}
