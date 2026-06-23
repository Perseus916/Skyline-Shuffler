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
    [SerializeField] private Transform levelButtonContainer;
    [SerializeField] private GameObject levelButtonPrefab;
    [SerializeField] private Button backButton;
    
    [Header("Display")]
    [SerializeField] private TextMeshProUGUI totalStarsText;
    [SerializeField] private TextMeshProUGUI totalCoinsText;

    [Header("Locked Level Popup Panel")]
    [SerializeField] private GameObject lockedLevelPanel;
    [SerializeField] private TextMeshProUGUI lockedLevelText;
    
    private Coroutine openAnimationCoroutine;

    private void OnEnable()
    {
        backButton?.onClick.RemoveAllListeners();
        backButton?.onClick.AddListener(OnBackClicked);
        
        PopulateLevelButtons();
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
    
    private void PopulateLevelButtons()
    {
        if (levelButtonPrefab == null || levelButtonContainer == null)
        {
            Debug.LogError("LevelSelectUI: Missing prefab or container!");
            return;
        }
        
        // Clear existing buttons
        foreach (Transform child in levelButtonContainer)
            Destroy(child.gameObject);
        
        int totalLevels = GameManager.Instance != null 
            ? GameManager.Instance.TotalLevels 
            : 0;
            
        for (int i = 1; i <= totalLevels; i++)
        {
            GameObject buttonObj = Instantiate(levelButtonPrefab, levelButtonContainer);
            LevelButtonUI buttonUI = buttonObj.GetComponent<LevelButtonUI>();
            if (buttonUI != null)
                buttonUI.Setup(i);
        }
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
        float duration = 0.3f;
        float elapsed = 0f;
        Transform panelTransform = this.transform;
        panelTransform.localScale = Vector3.zero;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float percent = Mathf.Clamp01(elapsed / duration);

            // Overshoot bounce easing
            float s = 1.3f; 
            float p = percent - 1f;
            float t = p * p * ((s + 1f) * p + s) + 1f;

            panelTransform.localScale = Vector3.LerpUnclamped(Vector3.zero, Vector3.one, t);
            yield return null;
        }

        panelTransform.localScale = Vector3.one;
        openAnimationCoroutine = null;
    }

    private IEnumerator AnimateClose()
    {
        float duration = 0.2f;
        float elapsed = 0f;
        Transform panelTransform = this.transform;
        Vector3 startScale = panelTransform.localScale;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float percent = Mathf.Clamp01(elapsed / duration);

            // Ease in scale down
            float t = percent * percent;

            panelTransform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            yield return null;
        }

        panelTransform.localScale = Vector3.zero;
        
        if (GameManager.Instance != null)
            GameManager.Instance.ShowHomeScreen();
    }
    
    /// <summary>
    /// Called by LevelButtonUI when a level is selected. Plays a scale-down closing animation first.
    /// </summary>
    public void OnLevelSelected(int levelNumber)
    {
        if (gameObject.activeInHierarchy)
        {
            if (openAnimationCoroutine != null)
                StopCoroutine(openAnimationCoroutine);
            StartCoroutine(AnimateCloseAndPlay(levelNumber));
        }
        else
        {
            if (GameManager.Instance != null)
                GameManager.Instance.PlayLevel(levelNumber);
        }
    }

    private IEnumerator AnimateCloseAndPlay(int levelNumber)
    {
        float duration = 0.2f;
        float elapsed = 0f;
        Transform panelTransform = this.transform;
        Vector3 startScale = panelTransform.localScale;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float percent = Mathf.Clamp01(elapsed / duration);

            // Ease in scale down
            float t = percent * percent;

            panelTransform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            yield return null;
        }

        panelTransform.localScale = Vector3.zero;

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
