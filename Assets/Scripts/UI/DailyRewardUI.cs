using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;

/// <summary>
/// Daily reward popup. Shows once per day on game start.
/// Base reward is always claimed. Ad doubles it.
/// </summary>
public class DailyRewardUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject panel;
    [SerializeField] private CanvasGroup canvasGroup;
    
    [Header("Display")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI rewardText;
    [SerializeField] private TextMeshProUGUI streakText;
    
    [Header("Buttons")]
    [SerializeField] private Button claimButton;
    [SerializeField] private Button doubleButton; // Watch ad to double
    
    [Header("Settings")]
    [SerializeField] private int baseRewardCoins = 25;
    [SerializeField] private int streakBonusPerDay = 5; // +5 per consecutive day
    [SerializeField] private int maxStreakBonus = 50;    // Cap at +50
    
    [Header("Animation")]
    [SerializeField] private float fadeInDuration = 0.3f;
    
    private int currentReward;
    private bool hasDoubled;
    
    private void Awake()
    {
        if (panel != null)
            panel.SetActive(false);
        
        claimButton?.onClick.AddListener(OnClaim);
        doubleButton?.onClick.AddListener(OnWatchAdToDouble);
    }
    
    /// <summary>
    /// Check if daily reward should be shown. Call from GameManager on startup.
    /// </summary>
    public bool ShouldShow()
    {
        string lastDate = PlayerPrefs.GetString("DailyRewardDate", "");
        string today = DateTime.Now.ToString("yyyy-MM-dd");
        return lastDate != today;
    }
    
    public void Show()
    {
        hasDoubled = false;
        
        // Calculate streak
        int streak = PlayerPrefs.GetInt("DailyStreak", 0);
        string lastDate = PlayerPrefs.GetString("DailyRewardDate", "");
        string yesterday = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd");
        
        if (lastDate == yesterday)
        {
            streak++; // Consecutive day!
        }
        else if (lastDate != DateTime.Now.ToString("yyyy-MM-dd"))
        {
            streak = 1; // Reset streak
        }
        
        PlayerPrefs.SetInt("DailyStreak", streak);
        PlayerPrefs.SetString("DailyRewardDate", DateTime.Now.ToString("yyyy-MM-dd"));
        PlayerPrefs.Save();
        
        // Calculate reward
        int streakBonus = Mathf.Min((streak - 1) * streakBonusPerDay, maxStreakBonus);
        currentReward = baseRewardCoins + streakBonus;
        
        // Update UI
        if (titleText != null)
            titleText.text = "Daily Reward!";
        
        if (rewardText != null)
            rewardText.text = $"🪙 {currentReward}";
        
        if (streakText != null)
        {
            if (streak > 1)
                streakText.text = $"🔥 {streak} day streak! +{streakBonus} bonus";
            else
                streakText.text = "Come back tomorrow for a streak bonus!";
        }
        
        // Enable double button only if ad available
        if (doubleButton != null)
        {
            bool adReady = AdManager.Instance != null && AdManager.Instance.IsRewardedAdReady();
            doubleButton.interactable = adReady;
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
    
    private void OnClaim()
    {
        int finalReward = hasDoubled ? currentReward * 2 : currentReward;
        SaveSystem.AddCoins(finalReward);

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayButtonClick();
            AudioManager.Instance.PlayCoinEarn();
        }
        
        Debug.Log($"<color=green>Daily reward claimed: +{finalReward} coins{(hasDoubled ? " (DOUBLED!)" : "")}</color>");
        Hide();
    }
    
    private void OnWatchAdToDouble()
    {
        if (AdManager.Instance == null) return;
        
        AdManager.Instance.ShowDailyDoubleAd(() =>
        {
            hasDoubled = true;
            
            // Update display to show doubled amount
            if (rewardText != null)
                rewardText.text = $"🪙 {currentReward * 2} (2×!)";
            
            // Disable the double button (already used)
            if (doubleButton != null)
                doubleButton.interactable = false;
            
            Debug.Log($"<color=green>Daily reward DOUBLED! {currentReward} → {currentReward * 2}</color>");
        });
    }
}
