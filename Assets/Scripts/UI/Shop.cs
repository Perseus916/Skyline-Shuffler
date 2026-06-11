using UnityEngine;
using TMPro;
using UnityEngine.UI;
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
    [Header("Optional (runtime wiring)")]
    [Tooltip("If assigned, these are used to wire the OnClick events automatically on enable.")]
    [SerializeField] private Button buyUndo1Button;
    [SerializeField] private Button buyUndo2Button;
    [SerializeField] private Button buyUndo3Button;
    [SerializeField] private Button buyUndo5Button;
    [SerializeField] private Button buyHint1Button;
    [SerializeField] private Button buyHint2Button;
    [SerializeField] private Button buyHint5Button;
    [SerializeField] private Button backButton;
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
        if (shopPanel != null)
            shopPanel.SetActive(false);
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
    }
    // 1) PANEL NAVIGATION
    public void OpenShop()
    {
        if (shopPanel != null)
            shopPanel.SetActive(true);
        RefreshCoinDisplay();
    }
    public void CloseShop()
    {
        if (shopPanel != null)
            shopPanel.SetActive(false);
    }
    // Optional alternate name if your BACK button expects it.
    public void BackButton()
    {
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
        if (coins < coinCost) return;
        bool spent = SaveSystem.SpendCoins(coinCost);
        if (!spent) return;
        if (isUndo)
            SaveSystem.AddFreeUndos(amount);
        else
            SaveSystem.AddFreeHints(amount);
        RefreshCoinDisplay();
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
}