using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Home screen UI. Single "Play" button that always loads the right level.
/// </summary>
public class HomeMenuUI : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button levelsButton;
    [SerializeField] private Button settingsButton;

    [Header("Panels")]
    [SerializeField] private GameObject settingsPanel;

    [Header("Display")]
    [SerializeField] private TextMeshProUGUI playButtonText;
    [SerializeField] private TextMeshProUGUI totalStarsText;

    private void OnEnable()
    {
        SetupButtons();
        UpdateUI();
    }

    private void SetupButtons()
    {
        playButton?.onClick.RemoveAllListeners();
        levelsButton?.onClick.RemoveAllListeners();
        settingsButton?.onClick.RemoveAllListeners();

        playButton?.onClick.AddListener(OnPlayClicked);
        levelsButton?.onClick.AddListener(OnLevelsClicked);
        settingsButton?.onClick.AddListener(OnSettingsClicked);
    }

    private void UpdateUI()
    {
        // Show what level they'll play
        if (playButtonText != null)
        {
            int nextLevel = SaveSystem.Data.currentLevel;

            if (nextLevel <= 1)
                playButtonText.text = "Play";
            else
                playButtonText.text = $"Level {nextLevel}";
        }

        // Show total stars
        if (totalStarsText != null)
        {
            totalStarsText.text = $"⭐ {SaveSystem.GetTotalStars()}";
        }
    }

    // =========================
    // PLAY BUTTON
    // =========================
    private void OnPlayClicked()
    {
        StartCoroutine(PlayButtonAnimation());
    }

    private IEnumerator PlayButtonAnimation()
    {
        // Get Animator from Play Button
        Animator animator = playButton.GetComponent<Animator>();

        // Play animation
        if (animator != null)
        {
            animator.Play("Button", 0, 0f);
        }

        // Small delay so animation becomes visible
        yield return new WaitForSeconds(0.15f);

        // Load gameplay
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ContinueGame();
        }
    }

    // =========================
    // LEVELS BUTTON
    // =========================
    private void OnLevelsClicked()
    {
        StartCoroutine(LevelsButtonAnimation());
    }

    private IEnumerator LevelsButtonAnimation()
    {
        Animator animator = levelsButton.GetComponent<Animator>();

        if (animator != null)
        {
            animator.Play("Button", 0, 0f);
        }

        yield return new WaitForSeconds(0.15f);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.ShowLevelSelect();
        }
    }

    // =========================
    // SETTINGS BUTTON
    // =========================
    private void OnSettingsClicked()
    {
        StartCoroutine(SettingsButtonAnimation());
    }

    private IEnumerator SettingsButtonAnimation()
    {
        Animator animator = settingsButton.GetComponent<Animator>();

        if (animator != null)
        {
            animator.Play("Button", 0, 0f);
        }

        yield return new WaitForSeconds(0.15f);

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayPopupOpen();
            }

            yield break;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.ShowSettings();
        }
    }
}