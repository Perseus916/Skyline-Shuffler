using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
/// <summary>
/// Shop panel controller + purchase logic for Undo/Hint packs.
/// Integrates with SaveSystem economy.
///
/// Wire your shop buttons via Inspector OnClick:
/// - Home -> ShopManager.OpenShop()
/// - Shop BACK -> ShopManager.CloseShop() OR ShopManager.BackButton()
/// - Each purchase button -> corresponding BuyUndo*/BuyHint* method
///
/// Also supports optional runtime wiring via serialized Button references.
/// If you assign the button references, they will be wired in OnEnable.
/// </summary>
public class ShopManager : MonoBehaviour
{
    [Header("Panel")]
    [Tooltip("Root GameObject for the shop panel. Link this to the shop UI root.")]
    public GameObject shopPanel;
    [Header("UI")]
    [Tooltip("TextMeshPro UI element that displays current coin balance.")]
    [SerializeField] private TextMeshProUGUI coinDisplayText;

    [Header("Purchase Notification Panel (achive)")]
    [Tooltip("The 'achive' panel GameObject.")]
    [SerializeField] private GameObject achivePanel;
    [Tooltip("The TextMeshProUGUI inside the 'achive' panel.")]
    [SerializeField] private TextMeshProUGUI achiveText;
    [SerializeField] private float notificationDuration = 2.0f;

    private Coroutine notificationCoroutine;

    [Header("Buttons (optional runtime wiring)")]
    [Tooltip("If assigned, these are used to wire the OnClick events automatically on enable.")]
    [SerializeField] private Button buyUndo1Button;
    [SerializeField] private Button buyUndo2Button;
    [SerializeField] private Button buyUndo3Button;
    [SerializeField] private Button buyUndo5Button;
    [SerializeField] private Button buyHint1Button;
    [SerializeField] private Button buyHint2Button;
    [SerializeField] private Button buyHint5Button;
    [SerializeField] private Button backButton;

    [Header("Shop options")]
    [Tooltip("If true, undo/hint grants can only happen from shop purchases (not via other systems). Currently enabled by removing other grant logic elsewhere.")]
    [SerializeField] private bool shopOnlyConsumables = true;

    [Header("Fail Feedback Visuals")]
    [Tooltip("Optional: image for fullscreen red flash. If left unassigned, a temporary RedFlash image will be created at runtime.")]
    [SerializeField] private Image redFlashImage;
    [Tooltip("Optional: RectTransform to shake (e.g. the Shop Panel window). If unassigned, falls back to shopPanel's transform.")]
    [SerializeField] private RectTransform shakeTarget;

    private Coroutine shakeCoroutine;
    private Coroutine flashCoroutine;
    private Vector3 originalShakePos;
    private bool hasStoredOriginalPos = false;

    private const int UNDO1_COST = 100;
    private const int UNDO1_AMOUNT = 1;
    private const int UNDO2_COST = 200;
    private const int UNDO2_AMOUNT = 2;
    private const int UNDO3_COST = 250;
    private const int UNDO3_AMOUNT = 3;
    private const int UNDO5_COST = 400;
    private const int UNDO5_AMOUNT = 5;
    private const int HINT1_COST = 150;
    private const int HINT1_AMOUNT = 1;
    private const int HINT2_COST = 250;
    private const int HINT2_AMOUNT = 2;
    private const int HINT5_COST = 400;
    private const int HINT5_AMOUNT = 5;
    private void Awake()
    {
        if (Time.frameCount == 0 && shopPanel != null)
            shopPanel.SetActive(false);
        
        // Ensure the notification panel starts hidden and scaled to 0
        if (achivePanel != null)
        {
            achivePanel.transform.localScale = Vector3.zero;
            achivePanel.SetActive(false);
        }

        RefreshCoinDisplay();
    }
    private void OnEnable()
    {
        WireButtonsIfAssigned();
        RefreshCoinDisplay();
    }
    private void OnDisable()
    {
        UnwireButtonsIfAssigned();

        // Ensure positions and overlays are reset when panel is closed/disabled
        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
            shakeCoroutine = null;
        }
        if (hasStoredOriginalPos)
        {
            Transform target = shakeTarget != null ? shakeTarget : (shopPanel != null ? shopPanel.transform : transform);
            if (target != null)
            {
                target.localPosition = originalShakePos;
            }
            hasStoredOriginalPos = false;
        }

        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
            flashCoroutine = null;
            
            Image flashImage = redFlashImage;
            if (flashImage == null)
            {
                Transform existingFlash = transform.Find("RedFlash") ?? (shopPanel != null ? shopPanel.transform.Find("RedFlash") : null);
                if (existingFlash != null)
                {
                    flashImage = existingFlash.GetComponent<Image>();
                }
            }
            if (flashImage != null)
            {
                flashImage.color = new Color(1f, 0f, 0f, 0f);
            }
        }
    }
    // 1) PANEL NAVIGATION
    public void OpenShop()
    {
        if (shopPanel != null)
        {
            shopPanel.SetActive(true);
        }
        RefreshCoinDisplay();
    }
    public void CloseShop()
    {
        if (shopPanel != null)
        {
            shopPanel.SetActive(false);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
    // Optional alternate name if your BACK button expects it.
    public void BackButton()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayButtonClick();
            
        CloseShop();
    }
    // 2) PURCHASE LOGIC FOR BUTTONS
    public void BuyUndo1() => TryBuy(isUndo: true, coinCost: UNDO1_COST, amount: UNDO1_AMOUNT);
    public void BuyUndo2() => TryBuy(isUndo: true, coinCost: UNDO2_COST, amount: UNDO2_AMOUNT);
    public void BuyUndo3() => TryBuy(isUndo: true, coinCost: UNDO3_COST, amount: UNDO3_AMOUNT);
    public void BuyUndo5() => TryBuy(isUndo: true, coinCost: UNDO5_COST, amount: UNDO5_AMOUNT);
    public void BuyHint1() => TryBuy(isUndo: false, coinCost: HINT1_COST, amount: HINT1_AMOUNT);
    public void BuyHint2() => TryBuy(isUndo: false, coinCost: HINT2_COST, amount: HINT2_AMOUNT);
    public void BuyHint5() => TryBuy(isUndo: false, coinCost: HINT5_COST, amount: HINT5_AMOUNT);
    // Generic template for Hint Pack 4 just in case you add it later.
    // You can expose a public wrapper later if needed.
    private void BuyHint4Template(int coinCost, int hintsToGrant)
    {
        TryBuy(isUndo: false, coinCost: coinCost, amount: hintsToGrant);
    }
    private void TryBuy(bool isUndo, int coinCost, int amount)
    {
        if (amount <= 0) return;
        int coins = SaveSystem.GetCoins();
        if (coins < coinCost)
        {
            TriggerFailFeedback();
            return;
        }

        // Spend coins atomically.
        bool spent = SaveSystem.SpendCoins(coinCost);
        if (!spent) return;

        // Play coin spend sound & trigger haptics
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayCoinSpend();
        }

        // Grant consumables (shop-only policy).
        if (isUndo)
            SaveSystem.AddFreeUndos(amount);
        else
            SaveSystem.AddFreeHints(amount);

        RefreshCoinDisplay();

        // Show purchase success notification!
        string itemName = isUndo ? (amount == 1 ? "Undo" : "Undos") : (amount == 1 ? "Hint" : "Hints");
        ShowPurchaseNotification($"Purchased {amount} {itemName}!");
    }

    private void RefreshCoinDisplay()
    {
        if (coinDisplayText == null) return;
        coinDisplayText.text = $"{SaveSystem.GetCoins()}";
    }
    // Optional runtime wiring support (no debug logs).
    private void WireButtonsIfAssigned()
    {
        if (buyUndo1Button) buyUndo1Button.onClick.AddListener(BuyUndo1);
        if (buyUndo2Button) buyUndo2Button.onClick.AddListener(BuyUndo2);
        if (buyUndo3Button) buyUndo3Button.onClick.AddListener(BuyUndo3);
        if (buyUndo5Button) buyUndo5Button.onClick.AddListener(BuyUndo5);
        if (buyHint1Button) buyHint1Button.onClick.AddListener(BuyHint1);
        if (buyHint2Button) buyHint2Button.onClick.AddListener(BuyHint2);
        if (buyHint5Button) buyHint5Button.onClick.AddListener(BuyHint5);
        if (backButton) backButton.onClick.AddListener(BackButton);
    }
    private void UnwireButtonsIfAssigned()
    {
        if (buyUndo1Button) buyUndo1Button.onClick.RemoveListener(BuyUndo1);
        if (buyUndo2Button) buyUndo2Button.onClick.RemoveListener(BuyUndo2);
        if (buyUndo3Button) buyUndo3Button.onClick.RemoveListener(BuyUndo3);
        if (buyUndo5Button) buyUndo5Button.onClick.RemoveListener(BuyUndo5);
        if (buyHint1Button) buyHint1Button.onClick.RemoveListener(BuyHint1);
        if (buyHint2Button) buyHint2Button.onClick.RemoveListener(BuyHint2);
        if (buyHint5Button) buyHint5Button.onClick.RemoveListener(BuyHint5);
        if (backButton) backButton.onClick.RemoveListener(BackButton);
    }

    private void ShowPurchaseNotification(string message)
    {
        if (notificationCoroutine != null)
        {
            StopCoroutine(notificationCoroutine);
        }
        notificationCoroutine = StartCoroutine(AnimateNotification(message));
    }

    private IEnumerator AnimateNotification(string message)
    {
        if (achivePanel == null) yield break;

        // Set the text
        if (achiveText != null)
        {
            achiveText.text = message;
        }

        // Set active
        achivePanel.SetActive(true);

        // Animation timing configuration
        float elapsed = 0f;
        float popDuration = 0.4f;

        // Easing colors: starts as an attractive vibrant bright gold and fades into clean white
        Color startColor = new Color(1f, 0.88f, 0.2f, 0f); // Bright Gold, transparent at first
        Color targetColor = Color.white; // Settle on white

        // Spacing animation configuration
        float startCharSpacing = 20f;  // widely spaced
        float targetCharSpacing = 0f;  // normal

        float startWordSpacing = 30f;  // widely spaced
        float targetWordSpacing = 0f;  // normal

        // 1. Elastic Pop Up + Text Easing In (color, character/word spacing, scale)
        while (elapsed < popDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / popDuration;
            
            // Back Out Easing curve: overshoots 1.0 slightly (to ~1.15) and bounces back smoothly
            float c1 = 1.70158f;
            float c3 = c1 + 1f;
            float scale = 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
            achivePanel.transform.localScale = new Vector3(scale, scale, scale);

            // Animate text elements
            if (achiveText != null)
            {
                achiveText.color = Color.Lerp(startColor, targetColor, t);
                achiveText.characterSpacing = Mathf.Lerp(startCharSpacing, targetCharSpacing, t);
                achiveText.wordSpacing = Mathf.Lerp(startWordSpacing, targetWordSpacing, t);
            }
            
            yield return null;
        }

        // Lock values at final state
        achivePanel.transform.localScale = Vector3.one;
        if (achiveText != null)
        {
            achiveText.color = targetColor;
            achiveText.characterSpacing = targetCharSpacing;
            achiveText.wordSpacing = targetWordSpacing;
        }

        // 2. Wait for display duration
        yield return new WaitForSecondsRealtime(notificationDuration);

        // 3. Smooth Scale Down + Text Fade Out & Disperse
        elapsed = 0f;
        float shrinkDuration = 0.2f;
        Color fadeOutColor = new Color(targetColor.r, targetColor.g, targetColor.b, 0f); // fade to transparent

        while (elapsed < shrinkDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / shrinkDuration;
            
            // Ease out quad
            float scale = 1f - (t * t);
            achivePanel.transform.localScale = new Vector3(scale, scale, scale);

            // Animate text fading out and dispersing characters slightly
            if (achiveText != null)
            {
                achiveText.color = Color.Lerp(targetColor, fadeOutColor, t);
                achiveText.characterSpacing = Mathf.Lerp(targetCharSpacing, 12f, t);
                achiveText.wordSpacing = Mathf.Lerp(targetWordSpacing, 18f, t);
            }
            
            yield return null;
        }

        achivePanel.transform.localScale = Vector3.zero;
        achivePanel.SetActive(false);
    }

    // ──────────────────────────────────────────────────────────────────────
    // FAIL FEEDBACK (INSUFFICIENT COINS)
    // ──────────────────────────────────────────────────────────────────────

    private void TriggerFailFeedback()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayMoveFailed();
        }

        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
            Transform target = shakeTarget != null ? shakeTarget : (shopPanel != null ? shopPanel.transform : transform);
            if (target != null && hasStoredOriginalPos)
            {
                target.localPosition = originalShakePos;
            }
        }
        
        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
        }

        shakeCoroutine = StartCoroutine(ShakeScreenRoutine());
        flashCoroutine = StartCoroutine(RedFlashRoutine());
    }

    private IEnumerator ShakeScreenRoutine()
    {
        Transform target = shakeTarget != null ? shakeTarget : (shopPanel != null ? shopPanel.transform : transform);
        if (target == null) yield break;

        if (hasStoredOriginalPos)
        {
            target.localPosition = originalShakePos;
        }
        else
        {
            originalShakePos = target.localPosition;
            hasStoredOriginalPos = true;
        }

        float elapsed = 0f;
        float duration = 0.35f;
        float magnitude = 12f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float percent = elapsed / duration;
            float currentMagnitude = Mathf.Lerp(magnitude, 0f, percent);

            float offsetX = Random.Range(-1f, 1f) * currentMagnitude;
            float offsetY = Random.Range(-1f, 1f) * currentMagnitude;

            target.localPosition = new Vector3(originalShakePos.x + offsetX, originalShakePos.y + offsetY, originalShakePos.z);
            yield return null;
        }

        target.localPosition = originalShakePos;
        hasStoredOriginalPos = false;
    }

    private IEnumerator RedFlashRoutine()
    {
        Image flashImage = redFlashImage;
        
        if (flashImage == null)
        {
            Transform existingFlash = transform.Find("RedFlash") ?? (shopPanel != null ? shopPanel.transform.Find("RedFlash") : null);
            if (existingFlash != null)
            {
                flashImage = existingFlash.GetComponent<Image>();
            }
            
            if (flashImage == null)
            {
                GameObject flashObj = new GameObject("RedFlash", typeof(RectTransform), typeof(Image));
                flashObj.transform.SetParent(shopPanel != null ? shopPanel.transform : transform, false);
                
                RectTransform rect = flashObj.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                
                flashImage = flashObj.GetComponent<Image>();
                flashImage.color = new Color(1f, 0f, 0f, 0f);
                flashImage.raycastTarget = false;
            }
        }

        if (flashImage == null) yield break;

        flashImage.gameObject.SetActive(true);

        float elapsed = 0f;
        float duration = 0.4f;
        float maxAlpha = 0.35f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float percent = elapsed / duration;

            float alpha;
            if (percent < 0.25f)
            {
                alpha = Mathf.Lerp(0f, maxAlpha, percent / 0.25f);
            }
            else
            {
                alpha = Mathf.Lerp(maxAlpha, 0f, (percent - 0.25f) / 0.75f);
            }

            flashImage.color = new Color(1f, 0f, 0f, alpha);
            yield return null;
        }

        flashImage.color = new Color(1f, 0f, 0f, 0f);
    }
}