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
    [SerializeField] private Button shopButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private ShopManager shopManager;

    [Header("Panels")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject shopPanel;


    [Header("Display")]
    [SerializeField] private TextMeshProUGUI playButtonText;
    [SerializeField] private TextMeshProUGUI totalStarsText;
    [SerializeField] private TextMeshProUGUI totalCoinsText;

    private void OnEnable()
    {
        // Restore scale, rotation, and alpha in case we were previously animated out
        this.transform.localScale = Vector3.one;
        this.transform.localRotation = Quaternion.identity;
        CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }

        // Restore button interactability
        if (playButton != null) playButton.interactable = true;
        if (levelsButton != null) levelsButton.interactable = true;
        if (settingsButton != null) settingsButton.interactable = true;
        if (shopButton != null) shopButton.interactable = true;
        if (quitButton != null) quitButton.interactable = true;

        SetupButtons();
        UpdateUI();
    }

    private void Update()
    {
        // Keep coins display updated if it changes (e.g., after closing shop)
        if (totalCoinsText != null)
        {
            totalCoinsText.text = $"{SaveSystem.GetCoins()}";
        }
    }

    private void SetupButtons()
    {
        playButton?.onClick.RemoveAllListeners();
        levelsButton?.onClick.RemoveAllListeners();
        settingsButton?.onClick.RemoveAllListeners();
        shopButton?.onClick.RemoveAllListeners();
        quitButton?.onClick.RemoveAllListeners();

        playButton?.onClick.AddListener(OnPlayClicked);
        levelsButton?.onClick.AddListener(OnLevelsClicked);
        settingsButton?.onClick.AddListener(OnSettingsClicked);
        shopButton?.onClick.AddListener(OnShopClicked);
        quitButton?.onClick.AddListener(OnQuitClicked);
    }

    private void UpdateUI()
    {
        // Show what level they'll play
        if (playButtonText != null)
        {
            int nextLevel = SaveSystem.Data.currentLevel;
            if (nextLevel > 1000)
            {
                nextLevel = 1000;
            }

            if (nextLevel <= 1)
                playButtonText.text = "Play";
            else
                playButtonText.text = $"Level {nextLevel}";
        }

        // Show total stars
        if (totalStarsText != null)
        {
            totalStarsText.text = $"{SaveSystem.GetTotalStars()}";
        }

        // Show total coins
        if (totalCoinsText != null)
        {
            totalCoinsText.text = $"{SaveSystem.GetCoins()}";
        }
    }

    // =========================
    // PLAY BUTTON
    // =========================
    private void OnPlayClicked()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayButtonClick();
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
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayButtonClick();
        StartCoroutine(LevelsButtonAnimation());
    }

    private void OnShopClicked()
    {
        if (shopButton != null)
        {
            StartCoroutine(DisableButtonTemporarily(shopButton, 0.5f));
        }

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayButtonClick();

        // Preferred path: use ShopManager if assigned.
        if (shopManager != null)
        {
            shopManager.OpenShop();
            return;
        }

        // Fallback path: directly show the serialized shop panel.
        if (shopPanel != null)
        {
            shopPanel.SetActive(true);
            return;
        }

        // Last fallback: delegate to GameManager if it exists.
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ShowShop();
        }
    }

    private IEnumerator DisableButtonTemporarily(Button button, float duration)
    {
        button.interactable = false;
        yield return new WaitForSecondsRealtime(duration);
        button.interactable = true;
    }

    /// <summary>
    /// No-op kept for API compatibility.
    /// </summary>
    public void RestoreShopButton() { }


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
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayButtonClick();
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

            yield break;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.ShowSettings();
        }
    }

    // =========================
    // QUIT BUTTON
    // =========================
    private void OnQuitClicked()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayButtonClick();
        StartCoroutine(QuitGameAnimationRoutine());
    }

    private IEnumerator QuitGameAnimationRoutine()
    {
        // Prevent multiple clicks
        if (quitButton != null) quitButton.interactable = false;
        if (playButton != null) playButton.interactable = false;
        if (levelsButton != null) levelsButton.interactable = false;
        if (settingsButton != null) settingsButton.interactable = false;
        if (shopButton != null) shopButton.interactable = false;

        // 1. Shake & Scale-up the quit button (0.5s build-up)
        Vector3 origButtonScale = quitButton != null ? quitButton.transform.localScale : Vector3.one;
        Vector3 origButtonPos = quitButton != null ? quitButton.transform.localPosition : Vector3.zero;
        
        float shakeDuration = 0.5f;
        float elapsed = 0f;
        while (elapsed < shakeDuration)
        {
            elapsed += Time.deltaTime;
            float percent = elapsed / shakeDuration;
            
            if (quitButton != null)
            {
                // Scale up slightly as it shakes
                quitButton.transform.localScale = origButtonScale * (1f + percent * 0.25f);
                // Frantic shake
                float shakeOffset = Mathf.Sin(elapsed * 50f) * 15f * percent;
                quitButton.transform.localPosition = origButtonPos + new Vector3(shakeOffset, 0f, 0f);
            }
            yield return null;
        }

        // Restore button scale and position in case of editor interruption or resume
        if (quitButton != null)
        {
            quitButton.transform.localScale = origButtonScale;
            quitButton.transform.localPosition = origButtonPos;
        }

        // 2. Spin & Shrink the entire Home Panel (0.6s collapse)
        Transform targetPanel = this.transform;
        Vector3 origPanelScale = targetPanel.localScale;
        
        CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        float collapseDuration = 0.6f;
        elapsed = 0f;
        while (elapsed < collapseDuration)
        {
            elapsed += Time.deltaTime;
            float percent = elapsed / collapseDuration;
            
            // Easing: start slow, accelerate fast (t^3)
            float t = percent * percent * percent;
            
            // Spin rotation (rapidly spins 1080 degrees)
            float angle = Mathf.Lerp(0f, -1080f, t);
            targetPanel.localRotation = Quaternion.Euler(0f, 0f, angle);
            
            // Scale down to 0
            targetPanel.localScale = Vector3.Lerp(origPanelScale, Vector3.zero, t);
            
            // Fade out alpha
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f - t;
            }
            
            yield return null;
        }
        
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        targetPanel.localScale = Vector3.zero;

        // Delay a tiny bit
        yield return new WaitForSeconds(0.1f);

        Debug.Log("Closing the game!");
        
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }
}