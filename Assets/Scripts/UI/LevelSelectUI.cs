using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Level selection grid. Calls GameManager for navigation (panel toggling).
/// </summary>
public class LevelSelectUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Button backButton;
    
    [Header("Display")]
    [SerializeField] private TextMeshProUGUI totalStarsText;
    [SerializeField] private TextMeshProUGUI totalCoinsText;

    [Header("Transitions")]
    [Tooltip("Duration of the fade-in transition when opening the level selection screen.")]
    [SerializeField] private float openTransitionDuration = 0.25f;

    [Tooltip("Duration of the fade-out transition when closing the level selection screen.")]
    [SerializeField] private float closeTransitionDuration = 0.2f;

    [Header("Locked Level Popup Panel")]
    [SerializeField] private GameObject lockedLevelPanel;
    [SerializeField] private TextMeshProUGUI lockedLevelText;
    
    private Coroutine openAnimationCoroutine;
    private CanvasGroup canvasGroup;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void OnEnable()
    {
        backButton?.onClick.RemoveAllListeners();
        backButton?.onClick.AddListener(OnBackClicked);
        
        UpdateStarsDisplay();
        UpdateCoinsDisplay();

        // Ensure the panel is hidden initially
        HideLockedLevelPanel();

        // Trigger opening animation
        if (openAnimationCoroutine != null)
            StopCoroutine(openAnimationCoroutine);
        openAnimationCoroutine = StartCoroutine(AnimateOpen());
    }

    private void Update()
    {
        UpdateCoinsDisplay();
    }
    

    
    private void UpdateStarsDisplay()
    {
        if (totalStarsText != null)
            totalStarsText.text = $"{SaveSystem.GetTotalStars()}";
    }
    
    private void UpdateCoinsDisplay()
    {
        if (totalCoinsText != null)
            totalCoinsText.text = $"{SaveSystem.GetCoins()}";
    }
    
    private void OnBackClicked()
    {
        if (gameObject.activeInHierarchy)
        {
            if (openAnimationCoroutine != null)
                StopCoroutine(openAnimationCoroutine);
            StartCoroutine(AnimateClose());
        }
        else
        {
            if (GameManager.Instance != null)
                GameManager.Instance.ShowHomeScreen();
        }
    }

    private IEnumerator AnimateOpen()
    {
        float duration = openTransitionDuration;
        float elapsed = 0f;

        // Reset scale so the panel remains standard size at all times
        this.transform.localScale = Vector3.one;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float percent = Mathf.Clamp01(elapsed / duration);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = percent;
            }
            yield return null;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
        }
        openAnimationCoroutine = null;
    }

    private IEnumerator AnimateClose()
    {
        float duration = closeTransitionDuration;
        float elapsed = 0f;

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
        }

        float startAlpha = canvasGroup != null ? canvasGroup.alpha : 1f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float percent = Mathf.Clamp01(elapsed / duration);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, percent);
            }
            yield return null;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }
        
        if (GameManager.Instance != null)
            GameManager.Instance.ShowHomeScreen();
    }
    
    /// <summary>
    /// Called by LevelButtonUI when a level is selected. Plays a closing fade animation first.
    /// </summary>
    public void OnLevelSelected(int levelNumber)
    {
        if (openAnimationCoroutine != null)
            StopCoroutine(openAnimationCoroutine);
        // Immediately play the level, skipping fade-out
        if (GameManager.Instance != null)
            GameManager.Instance.PlayLevel(levelNumber);
    }

    private IEnumerator AnimateCloseAndPlay(int levelNumber)
    {
        float duration = closeTransitionDuration;
        float elapsed = 0f;

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
        }

        float startAlpha = canvasGroup != null ? canvasGroup.alpha : 1f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float percent = Mathf.Clamp01(elapsed / duration);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, percent);
            }
            yield return null;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }

        if (GameManager.Instance != null)
            GameManager.Instance.PlayLevel(levelNumber);
    }

    private Coroutine panelAnimationCoroutine;

    /// <summary>
    /// Show the locked level panel and set the text with a scale-up opening animation
    /// </summary>
    public void ShowLockedLevelPanel(int levelNumber)
    {
        if (lockedLevelText != null)
            lockedLevelText.text = "Level " + levelNumber;

        if (lockedLevelPanel != null)
        {
            if (panelAnimationCoroutine != null)
                StopCoroutine(panelAnimationCoroutine);

            lockedLevelPanel.SetActive(true);
            panelAnimationCoroutine = StartCoroutine(AnimatePanelScale(lockedLevelPanel.transform, Vector3.zero, Vector3.one, 0.3f, true));
        }
    }

    /// <summary>
    /// Hide the locked level panel with a scale-down closing animation
    /// </summary>
    public void HideLockedLevelPanel()
    {
        if (lockedLevelPanel != null)
        {
            if (gameObject.activeInHierarchy)
            {
                if (panelAnimationCoroutine != null)
                    StopCoroutine(panelAnimationCoroutine);

                panelAnimationCoroutine = StartCoroutine(AnimatePanelScale(lockedLevelPanel.transform, lockedLevelPanel.transform.localScale, Vector3.zero, 0.2f, false));
            }
            else
            {
                lockedLevelPanel.SetActive(false);
            }
        }
    }

    private System.Collections.IEnumerator AnimatePanelScale(Transform panelTransform, Vector3 startScale, Vector3 targetScale, float duration, bool isOpening)
    {
        float elapsed = 0f;
        panelTransform.localScale = startScale;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float percent = Mathf.Clamp01(elapsed / duration);

            float t;
            if (isOpening)
            {
                // Premium overshoot bounce formula
                float s = 1.70158f;
                float p = percent - 1f;
                t = p * p * ((s + 1f) * p + s) + 1f;
            }
            else
            {
                t = percent * percent; // Ease in scale down
            }

            panelTransform.localScale = Vector3.LerpUnclamped(startScale, targetScale, t);
            yield return null;
        }

        panelTransform.localScale = targetScale;

        if (!isOpening)
        {
            lockedLevelPanel.SetActive(false);
        }

        panelAnimationCoroutine = null;
    }
}
