using UnityEngine;
using System;

/// <summary>
/// Centralized ad manager using Unity LevelPlay (ironSource) SDK.
/// All rewarded ad logic flows through this singleton.
/// 
/// SETUP:
/// 1. Unity Dashboard → Services → Ads → Enable LevelPlay
/// 2. Install LevelPlay SDK via Package Manager or Services panel
/// 3. Set your App Key in the inspector (from Unity Dashboard → Monetization)
/// 
/// In editor/development: ads auto-succeed with a 1-second delay (stub mode).
/// </summary>
public class AdManager : MonoBehaviour
{
    private static AdManager instance;
    public static AdManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<AdManager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("AdManager");
                    instance = go.AddComponent<AdManager>();
                }
            }
            return instance;
        }
    }
    
    [Header("LevelPlay Settings")]
    [Tooltip("Your ironSource/LevelPlay App Key from Unity Dashboard")]
    [SerializeField] private string appKey = "YOUR_APP_KEY";
    
    [Header("Settings")]
    [SerializeField] private float adCooldownSeconds = 45f; // Min time between ads
    
    // State
    private bool isInitialized;
    private bool isRewardedAdLoaded;
    private float lastAdShownTime = -999f;
    private Action pendingRewardCallback;
    private string pendingPlacement;
    
    // Analytics
    public static int TotalAdsWatched { get; private set; }
    
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        
        InitializeAds();
    }
    
    private void Start()
    {
        InitializeAds();
    }
    
    // ========================================
    // INITIALIZATION
    // ========================================
    
    private void InitializeAds()
    {
        if (isInitialized) return;
#if !USE_IRONSOURCE || UNITY_EDITOR || (!UNITY_ANDROID && !UNITY_IOS)
        // In editor/standalone/stub mode, we stub everything
        isInitialized = true;
        isRewardedAdLoaded = true;
        Debug.Log("<color=yellow>[AdManager] Running in STUB mode (Editor/Standalone/Stub)</color>");
#else
        // LevelPlay / ironSource initialization
        IronSourceEvents.onSdkInitializationCompletedEvent += OnSdkInitialized;
        
        // Rewarded ad events
        IronSourceRewardedVideoEvents.onAdReadyEvent += OnRewardedAdReady;
        IronSourceRewardedVideoEvents.onAdUnavailableEvent += OnRewardedAdUnavailable;
        IronSourceRewardedVideoEvents.onAdOpenedEvent += OnRewardedAdOpened;
        IronSourceRewardedVideoEvents.onAdClosedEvent += OnRewardedAdClosed;
        IronSourceRewardedVideoEvents.onAdRewardedEvent += OnRewardedAdRewarded;
        IronSourceRewardedVideoEvents.onAdShowFailedEvent += OnRewardedAdShowFailed;
        
        // Initialize with rewarded video ad unit
        IronSource.Agent.init(appKey, IronSourceAdUnits.REWARDED_VIDEO);
        
        Debug.Log("[AdManager] LevelPlay SDK initializing...");
#endif
    }
    
#if USE_IRONSOURCE && !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
    private void OnSdkInitialized()
    {
        isInitialized = true;
        Debug.Log("[AdManager] LevelPlay SDK Initialized");
        
        // Validate integration in debug/dev builds
        #if DEVELOPMENT_BUILD
        IronSource.Agent.validateIntegration();
        #endif
    }
    
    private void OnRewardedAdReady(IronSourceAdInfo adInfo)
    {
        isRewardedAdLoaded = true;
        Debug.Log("[AdManager] Rewarded ad ready");
    }
    
    private void OnRewardedAdUnavailable()
    {
        isRewardedAdLoaded = false;
        Debug.Log("[AdManager] Rewarded ad unavailable");
    }
    
    private void OnRewardedAdOpened(IronSourceAdInfo adInfo)
    {
        Debug.Log($"[AdManager] Rewarded ad opened — placement: {pendingPlacement}");
    }
    
    private void OnRewardedAdClosed(IronSourceAdInfo adInfo)
    {
        // Ad closed — reward was already granted in OnRewardedAdRewarded if watched fully
        Debug.Log("[AdManager] Rewarded ad closed");
    }
    
    private void OnRewardedAdRewarded(IronSourcePlacement placement, IronSourceAdInfo adInfo)
    {
        Debug.Log($"[AdManager] Reward granted — placement: {pendingPlacement}, reward: {placement.getRewardName()} x{placement.getRewardAmount()}");
        TotalAdsWatched++;
        lastAdShownTime = Time.time;
        
        pendingRewardCallback?.Invoke();
        pendingRewardCallback = null;
        pendingPlacement = null;
    }
    
    private void OnRewardedAdShowFailed(IronSourceError error, IronSourceAdInfo adInfo)
    {
        Debug.LogWarning($"[AdManager] Rewarded ad show failed: {error.getDescription()}");
        pendingRewardCallback = null;
        pendingPlacement = null;
    }
    
    // LevelPlay requires this lifecycle call
    private void OnApplicationPause(bool isPaused)
    {
        IronSource.Agent.onApplicationPause(isPaused);
    }
#endif
    
    // ========================================
    // PUBLIC API
    // ========================================
    
    /// <summary>
    /// Check if a rewarded ad is ready to show.
    /// Also respects cooldown timer.
    /// </summary>
    public bool IsRewardedAdReady()
    {
        if (!isInitialized) return false;
        if (Time.time - lastAdShownTime < adCooldownSeconds) return false;
        
#if !USE_IRONSOURCE || UNITY_EDITOR || (!UNITY_ANDROID && !UNITY_IOS)
        return true; // Always available in editor/standalone/stub mode
#else
        return IronSource.Agent.isRewardedVideoAvailable();
#endif
    }
    
    /// <summary>
    /// Show a rewarded ad. The callback fires only if the user watches it fully.
    /// Placement is for analytics (e.g., "double_reward", "extra_moves", "free_undo").
    /// </summary>
    /// <param name="placement">Analytics placement name</param>
    /// <param name="onRewardGranted">Called when reward is earned</param>
    public void ShowRewardedAd(string placement, Action onRewardGranted)
    {
        if (!IsRewardedAdReady())
        {
            Debug.LogWarning($"[AdManager] Ad not ready for placement: {placement}");
            return;
        }
        
        pendingRewardCallback = onRewardGranted;
        pendingPlacement = placement;
        
#if !USE_IRONSOURCE || UNITY_EDITOR || (!UNITY_ANDROID && !UNITY_IOS)
        // Stub: simulate ad with delay
        Debug.Log($"<color=green>[AdManager] STUB — Showing ad for '{placement}'. Granting reward in 1s...</color>");
        Invoke(nameof(StubGrantReward), 1f);
#else
        IronSource.Agent.showRewardedVideo(placement);
#endif
    }
    
#if !USE_IRONSOURCE || UNITY_EDITOR || (!UNITY_ANDROID && !UNITY_IOS)
    private void StubGrantReward()
    {
        TotalAdsWatched++;
        lastAdShownTime = Time.time;
        
        Debug.Log($"<color=green>[AdManager] STUB — Reward granted for '{pendingPlacement}'</color>");
        pendingRewardCallback?.Invoke();
        pendingRewardCallback = null;
        pendingPlacement = null;
    }
#endif
    
    // ========================================
    // CONVENIENCE METHODS (for common placements)
    // ========================================
    
    /// <summary>Show ad for double reward on level complete</summary>
    public void ShowDoubleRewardAd(Action onDoubled)
    {
        ShowRewardedAd("double_reward", onDoubled);
    }
    
    /// <summary>Show ad for extra moves when out of moves</summary>
    public void ShowExtraMovesAd(Action onMovesGranted)
    {
        ShowRewardedAd("extra_moves", onMovesGranted);
    }
    
    /// <summary>Show ad for free undos</summary>
    public void ShowFreeUndosAd(Action onUndosGranted)
    {
        ShowRewardedAd("free_undo", onUndosGranted);
    }
    
    /// <summary>Show ad for free hint</summary>
    public void ShowFreeHintAd(Action onHintGranted)
    {
        ShowRewardedAd("free_hint", onHintGranted);
    }
    
    /// <summary>Show ad for daily reward doubling</summary>
    public void ShowDailyDoubleAd(Action onDoubled)
    {
        ShowRewardedAd("daily_double", onDoubled);
    }
    
    /// <summary>Show ad for pre-level boost</summary>
    public void ShowLevelBoostAd(Action onBoostGranted)
    {
        ShowRewardedAd("level_boost", onBoostGranted);
    }
    
    /// <summary>Show ad for free slot unlock</summary>
    public void ShowUnlockBlockAd(Action onUnlockGranted)
    {
        ShowRewardedAd("unlock_block", onUnlockGranted);
    }
}
