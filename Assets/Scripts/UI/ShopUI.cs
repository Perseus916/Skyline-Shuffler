using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Shop panel where players buy undos, hints, and coins.
/// Each item can be purchased with coins OR by watching an ad.
/// Accessible from the home screen or pause menu.
/// </summary>
public class ShopUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject panel;
    [SerializeField] private CanvasGroup canvasGroup;
    
    [Header("Currency Display")]
    [SerializeField] private TextMeshProUGUI totalCoinsText;
    [SerializeField] private TextMeshProUGUI totalUndosText;
    [SerializeField] private TextMeshProUGUI totalHintsText;
    
    [Header("Undo Pack Buttons")]
    [SerializeField] private Button buyUndo3Button;   // 3 undos for coins
    [SerializeField] private Button buyUndo10Button;  // 10 undos for coins
    [SerializeField] private Button adUndoButton;     // Watch ad for 3 undos
    
    [Header("Hint Pack Buttons")]
    [SerializeField] private Button buyHint1Button;   // 1 hint for coins
    [SerializeField] private Button buyHint5Button;   // 5 hints for coins
    [SerializeField] private Button adHintButton;     // Watch ad for 1 hint
    
    [Header("Free Coins")]
    [SerializeField] private Button adCoinsButton;    // Watch ad for coins
    
    [Header("Button Labels")]
    [SerializeField] private TextMeshProUGUI undo3Label;
    [SerializeField] private TextMeshProUGUI undo10Label;
    [SerializeField] private TextMeshProUGUI hint1Label;
    [SerializeField] private TextMeshProUGUI hint5Label;
    [SerializeField] private TextMeshProUGUI adCoinsLabel;
    
    [Header("Navigation")]
    [SerializeField] private Button closeButton;
    
    [Header("Pricing")]
    [SerializeField] private int undo3Price = 150;
    [SerializeField] private int undo10Price = 400;
    [SerializeField] private int hint1Price = 200;
    [SerializeField] private int hint5Price = 800;
    [SerializeField] private int adCoinReward = 50;
    
    [Header("Ad Rewards")]
    [SerializeField] private int adUndoReward = 3;
    [SerializeField] private int adHintReward = 1;
    
    [Header("Animation")]
    [SerializeField] private float fadeInDuration = 0.25f;
    
    private void Awake()
    {
        if (panel != null) panel.SetActive(false);
        
        buyUndo3Button?.onClick.AddListener(() => BuyWithCoins("undo3", undo3Price, () => SaveSystem.AddFreeUndos(3)));
        buyUndo10Button?.onClick.AddListener(() => BuyWithCoins("undo10", undo10Price, () => SaveSystem.AddFreeUndos(10)));
        buyHint1Button?.onClick.AddListener(() => BuyWithCoins("hint1", hint1Price, () => SaveSystem.AddFreeHints(1)));
        buyHint5Button?.onClick.AddListener(() => BuyWithCoins("hint5", hint5Price, () => SaveSystem.AddFreeHints(5)));
        
        adUndoButton?.onClick.AddListener(OnAdForUndos);
        adHintButton?.onClick.AddListener(OnAdForHint);
        adCoinsButton?.onClick.AddListener(OnAdForCoins);
        
        closeButton?.onClick.AddListener(Hide);
    }
    
    public void Show()
    {
        RefreshUI();
        
        if (panel != null) panel.SetActive(true);
        StartCoroutine(AnimateFadeIn());
    }
    
    public void Hide()
    {
        StopAllCoroutines();
        if (panel != null) panel.SetActive(false);
    }
    
    private void RefreshUI()
    {
        int coins = SaveSystem.GetCoins();
        int undos = SaveSystem.GetFreeUndos();
        int hints = SaveSystem.GetFreeHints();
        bool adReady = AdManager.Instance != null && AdManager.Instance.IsRewardedAdReady();
        
        // Currency display
        if (totalCoinsText != null) totalCoinsText.text = coins.ToString();
        if (totalUndosText != null) totalUndosText.text = undos.ToString();
        if (totalHintsText != null) totalHintsText.text = hints.ToString();
        
        // Labels
        if (undo3Label != null) undo3Label.text = $"3 Undos\n🪙 {undo3Price}";
        if (undo10Label != null) undo10Label.text = $"10 Undos\n🪙 {undo10Price}";
        if (hint1Label != null) hint1Label.text = $"1 Hint\n🪙 {hint1Price}";
        if (hint5Label != null) hint5Label.text = $"5 Hints\n🪙 {hint5Price}";
        if (adCoinsLabel != null) adCoinsLabel.text = $"📺 Watch Ad\n+{adCoinReward} 🪙";
        
        // Enable/disable based on affordability
        if (buyUndo3Button != null) buyUndo3Button.interactable = coins >= undo3Price;
        if (buyUndo10Button != null) buyUndo10Button.interactable = coins >= undo10Price;
        if (buyHint1Button != null) buyHint1Button.interactable = coins >= hint1Price;
        if (buyHint5Button != null) buyHint5Button.interactable = coins >= hint5Price;
        
        // Ad buttons
        if (adUndoButton != null) adUndoButton.interactable = adReady;
        if (adHintButton != null) adHintButton.interactable = adReady;
        if (adCoinsButton != null) adCoinsButton.interactable = adReady;
    }
    
    private void BuyWithCoins(string itemId, int price, System.Action grantItem)
    {
        if (!SaveSystem.SpendCoins(price))
        {
            Debug.Log($"<color=red>Not enough coins for {itemId}!</color>");
            return;
        }
        
        grantItem.Invoke();
        RefreshUI();
        Debug.Log($"<color=green>Purchased {itemId} for {price} coins</color>");
    }
    
    private void OnAdForUndos()
    {
        if (AdManager.Instance == null) return;
        
        AdManager.Instance.ShowFreeUndosAd(() =>
        {
            SaveSystem.AddFreeUndos(adUndoReward);
            RefreshUI();
            Debug.Log($"<color=green>Ad reward: +{adUndoReward} undos</color>");
        });
    }
    
    private void OnAdForHint()
    {
        if (AdManager.Instance == null) return;
        
        AdManager.Instance.ShowFreeHintAd(() =>
        {
            SaveSystem.AddFreeHints(adHintReward);
            RefreshUI();
            Debug.Log($"<color=green>Ad reward: +{adHintReward} hint</color>");
        });
    }
    
    private void OnAdForCoins()
    {
        if (AdManager.Instance == null) return;
        
        AdManager.Instance.ShowRewardedAd("shop_coins", () =>
        {
            SaveSystem.AddCoins(adCoinReward);
            RefreshUI();
            Debug.Log($"<color=green>Ad reward: +{adCoinReward} coins</color>");
        });
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
}
