using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Progress settings panel.
/// Shows stats and allows reset/clear.
/// </summary>
public class ProgressSettingsUI : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private TextMeshProUGUI levelsCompletedText;
    [SerializeField] private TextMeshProUGUI totalStarsText;
    [SerializeField] private TextMeshProUGUI coinsText;

    [Header("Buttons")]
    [SerializeField] private Button resetProgressButton;
    [SerializeField] private Button clearAllDataButton;
    [SerializeField] private Button closeButton;

    [Header("Panels")]
    [SerializeField] private GameObject settingsPanel;

    [Header("Confirm Dialog")]
    [SerializeField] private GameObject resetConfirmPanel;
    [SerializeField] private TextMeshProUGUI resetConfirmText;
    [SerializeField] private Button confirmResetButton;
    [SerializeField] private Button cancelResetButton;

    private bool clearAllRequested;

    private void OnEnable()
    {
        RefreshDisplay();
        SetupListeners();

        if (resetConfirmPanel != null)
            resetConfirmPanel.SetActive(false);
    }

    private void OnDisable()
    {
        RemoveListeners();
    }

    private void SetupListeners()
    {
        RemoveListeners();

        if (resetProgressButton != null)
            resetProgressButton.onClick.AddListener(OnResetClicked);

        if (clearAllDataButton != null)
            clearAllDataButton.onClick.AddListener(OnClearAllClicked);

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        if (confirmResetButton != null)
            confirmResetButton.onClick.AddListener(ConfirmReset);

        if (cancelResetButton != null)
            cancelResetButton.onClick.AddListener(CancelReset);
    }

    private void RemoveListeners()
    {
        if (resetProgressButton != null)
            resetProgressButton.onClick.RemoveListener(OnResetClicked);

        if (clearAllDataButton != null)
            clearAllDataButton.onClick.RemoveListener(OnClearAllClicked);

        if (closeButton != null)
            closeButton.onClick.RemoveListener(Close);

        if (confirmResetButton != null)
            confirmResetButton.onClick.RemoveListener(ConfirmReset);

        if (cancelResetButton != null)
            cancelResetButton.onClick.RemoveListener(CancelReset);
    }

    private void RefreshDisplay()
    {
        int completed = SaveSystem.Data.levelProgress.Count;
        int totalStars = SaveSystem.GetTotalStars();
        int coins = SaveSystem.Data.coins;

        if (levelsCompletedText != null)
            levelsCompletedText.text = $"Levels Completed: {completed}";

        if (totalStarsText != null)
            totalStarsText.text = $"Total Stars: {totalStars}";

        if (coinsText != null)
            coinsText.text = $"Coins: 💰 {coins}";
    }

    private void OnResetClicked()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayButtonClick();

        clearAllRequested = false;

        if (resetConfirmPanel != null)
        {
            resetConfirmPanel.SetActive(true);
            if (resetConfirmText != null)
                resetConfirmText.text = "Reset all level progress?\nYou will keep your coins.";
        }
    }

    private void OnClearAllClicked()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayButtonClick();

        clearAllRequested = true;

        if (resetConfirmPanel != null)
        {
            resetConfirmPanel.SetActive(true);
            if (resetConfirmText != null)
                resetConfirmText.text = "Clear ALL data?\nCoins, progress, everything will be deleted.\nThis cannot be undone.";
        }
    }

    private void ConfirmReset()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayButtonClick();

        if (clearAllRequested)
        {
            // Clear everything
            SaveSystem.ResetAllProgress();
            SaveSystem.Data.coins = 0;
            SaveSystem.Data.freeUndos = 2;
            SaveSystem.Data.freeHints = 1;
            SaveSystem.Save();
            Debug.Log("<color=yellow>All data cleared!</color>");
        }
        else
        {
            // Reset progress only, keep coins/settings
            SaveSystem.ResetLevelProgressOnly();
            Debug.Log("<color=yellow>Progress reset!</color>");
        }

        if (resetConfirmPanel != null)
            resetConfirmPanel.SetActive(false);

        RefreshDisplay();

        // Return to home
        if (GameManager.Instance != null)
            GameManager.Instance.ShowHomeScreen();
    }

    private void CancelReset()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayButtonClick();

        if (resetConfirmPanel != null)
            resetConfirmPanel.SetActive(false);
    }

    public void Close()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayButtonClick();

        gameObject.SetActive(false);

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
            SettingsUI settingsUI = settingsPanel.GetComponent<SettingsUI>();
            if (settingsUI != null)
            {
                settingsUI.ShowSettingsMenu();
            }
        }
    }
}
