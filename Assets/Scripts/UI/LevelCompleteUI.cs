using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Level complete popup with achievement-style animation.
///
/// Hierarchy:
///   Level Complete Popup  ← this script + CanvasGroup (rootCanvasGroup)
///     └── bg              ← background panel image; assign bgRect
///         ├── Level Number
///         ├── congratulation text
///         ├── GameObject (1)
///         │   ├── star
///         │   ├── star
///         │   └── star
///         ├── coin background  ← coinsRect
///         │   └── coin text    ← coinsEarnedText
///         └── GameObject       ← buttonsRect
///             ├── home button  ← homeButton
///             └── next level button ← nextLevelButton
///
/// IMPORTANT: Keep 'Level Complete Popup' ALWAYS ACTIVE in the scene.
/// Visibility is controlled entirely through CanvasGroup alpha + blocksRaycasts.
/// </summary>
public class LevelCompleteUI : MonoBehaviour
{
    // ─── References ───────────────────────────────────────────────────────
    [Header("Root Canvas Group (on Level Complete Popup)")]
    [Tooltip("CanvasGroup on THIS GameObject — controls visibility of entire popup")]
    [SerializeField] private CanvasGroup rootCanvasGroup;

    [Header("HUD to hide while popup is open")]
    [Tooltip("Drag your in-game HUD root GameObject here — it will be hidden when popup opens")]
    [SerializeField] private GameObject hudPanel;

    [Header("bg — the bouncing card")]
    [Tooltip("'bg' RectTransform — this is the card that bounces in / squishes out")]
    [SerializeField] private RectTransform bgRect;

    [Header("Text")]
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI coinsEarnedText;

    [Header("Stars")]
    [SerializeField] private GameObject[] starObjects; // 3 star GameObjects

    [Header("Slide-Up Elements")]
    [SerializeField] private RectTransform coinsRect;   // 'coin background'
    [SerializeField] private RectTransform buttonsRect; // buttons 'GameObject'

    [Header("Buttons")]
    [SerializeField] private Button homeButton;
    [SerializeField] private Button nextLevelButton;

    [Header("Animation Timing")]
    [SerializeField] private float bgFadeDuration  = 0.22f;
    [SerializeField] private float cardPopDuration  = 0.48f;
    [SerializeField] private float starPopDuration  = 0.26f;
    [SerializeField] private float starInterval     = 0.18f;
    [SerializeField] private float slideDelay       = 0.10f;
    [SerializeField] private float slideDuration    = 0.36f;

    [Header("Coin Flip Animation")]
    [Tooltip("Assign the Coin Image GameObject (with Image component) here")] 
    [SerializeField] private Image coinImage; // The coin image to flip
    [SerializeField] private float coinFlipDuration = 0.6f;
    private CanvasGroup coinCanvasGroup;

    private int earnedStars;
    private bool isAnimating;
    private bool wasVisible; // tracks if popup was actually shown to user

    // ──────────────────────────────────────────────────────────────────────
    // LIFECYCLE
    // ──────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Wire buttons
        if (homeButton == null)
            Debug.LogWarning("[LevelCompleteUI] Home Button not assigned in Inspector!");
        else
            homeButton.onClick.AddListener(OnHome);

        if (nextLevelButton == null)
            Debug.LogWarning("[LevelCompleteUI] Next Level Button not assigned in Inspector!");
        else
            nextLevelButton.onClick.AddListener(OnNextLevel);

        if (coinImage != null)
        {
            coinCanvasGroup = coinImage.GetComponent<CanvasGroup>();
            if (coinCanvasGroup == null)
                coinCanvasGroup = coinImage.gameObject.AddComponent<CanvasGroup>();
            coinCanvasGroup.alpha = 1f;
            coinImage.transform.localRotation = Quaternion.identity;
        }
    }

    private void Start()
    {
        // Hide visually on start — but keep GameObject ACTIVE
        SetHiddenState();
    }

    // ──────────────────────────────────────────────────────────────────────
    // PUBLIC API
    // ──────────────────────────────────────────────────────────────────────

    public void Show(int levelNumber, int movesTaken, int optimalMoves, int stars, int coins)
    {
        Debug.Log($"[LevelCompleteUI] Show — Level {levelNumber}, Stars {stars}, Coins {coins}");
        wasVisible = true; // mark that popup has been shown

        if (isAnimating)
        {
            StopAllCoroutines();
            isAnimating = false;
        }

        // ✅ Always re-enable buttons at the start of every Show()
        SetButtonsInteractable(true);

        earnedStars = Mathf.Clamp(stars, 0, starObjects != null ? starObjects.Length : 3);

        // Set text content
        if (levelText != null)       levelText.text       = $"Level {levelNumber}";
        if (coinsEarnedText != null) coinsEarnedText.text = $"+{coins}";

        // Reset all stars to hidden / scale 0
        if (starObjects != null)
            foreach (var s in starObjects)
                if (s != null) { s.SetActive(false); s.transform.localScale = Vector3.zero; }

        // Reset slide elements invisible
        SetAlpha(coinsRect,   0f);
        SetAlpha(buttonsRect, 0f);

        // Reset bg card scale to zero
        if (bgRect != null) bgRect.localScale = Vector3.zero;

        // Make root invisible + block rays (ready to animate)
        if (rootCanvasGroup != null)
        {
            rootCanvasGroup.alpha          = 0f;
            rootCanvasGroup.interactable   = false;
            rootCanvasGroup.blocksRaycasts = true;
        }

        StartCoroutine(AnimateOpen(coins));
    }

    // Backward compatible overload
    public void Show(int levelNumber, int movesTaken, int optimalMoves, int stars)
        => Show(levelNumber, movesTaken, optimalMoves, stars, 0);

    /// <summary>Instant hide — for external callers (GameManager etc.).</summary>
    public void Hide()
    {
        Debug.Log("[LevelCompleteUI] Hide called.");
        StopAllCoroutines();
        isAnimating = false;
        SetHiddenState();
    }

    // ──────────────────────────────────────────────────────────────────────
    // OPEN ANIMATION
    // ──────────────────────────────────────────────────────────────────────

    private IEnumerator AnimateOpen(int coins)
    {
        isAnimating = true;

        // Phase 0 ── Instantly hide the HUD
        if (hudPanel != null) hudPanel.SetActive(false);
        else Debug.LogWarning("[LevelCompleteUI] HUD Panel not assigned! Assign it in the Inspector.");

        // Phase 1 ── Fade in dark background overlay
        yield return StartCoroutine(FadeAlpha(rootCanvasGroup, 0f, 1f, bgFadeDuration));

        // Phase 2 ── bg card bounces in with elastic pop
        if (bgRect != null)
            yield return StartCoroutine(ScalePop(bgRect, Vector3.zero, Vector3.one, cardPopDuration, false));

        // Enable interactions now that card is visible
        if (rootCanvasGroup != null) rootCanvasGroup.interactable = true;

        // Phase 3 ── Stars pop in one by one
        if (starObjects != null)
        {
            for (int i = 0; i < starObjects.Length; i++)
            {
                if (starObjects[i] == null) continue;

                bool earned = i < earnedStars;

                if (earned)
                {
                    starObjects[i].SetActive(true);
                    starObjects[i].transform.localScale = Vector3.zero;

                    if (AudioManager.Instance != null)
                    {
                        AudioManager.Instance.PlayStarEarn();
                    }

                    yield return StartCoroutine(
                        ScalePop(starObjects[i].transform, Vector3.zero, Vector3.one, starPopDuration, false));
                    yield return new WaitForSeconds(starInterval);
                }
                else
                {
                    starObjects[i].SetActive(false);
                }
            }
        }

        yield return new WaitForSeconds(slideDelay);

        // Phase 4 ── Coins row slides up + fades in
        if (coins > 0 && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayCoinEarn();
        }
        StartCoroutine(SlideUp(coinsRect, slideDuration));
        // --- COIN FLIP ---
        if (coinImage != null)
        {
            coinImage.transform.localRotation = Quaternion.identity;
            if (coinCanvasGroup != null) coinCanvasGroup.alpha = 1f;
            StartCoroutine(FlipAndFadeCoin());
        }
        yield return new WaitForSeconds(0.10f);

        // Phase 5 ── Buttons row slides up + fades in (slight stagger)
        StartCoroutine(SlideUp(buttonsRect, slideDuration));

        isAnimating = false;
    }

    private IEnumerator FlipAndFadeCoin()
    {
        // Flip
        float elapsed = 0f;
        while (elapsed < coinFlipDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / coinFlipDuration);
            float yRot = Mathf.Lerp(0f, 360f, t);
            coinImage.transform.localRotation = Quaternion.Euler(0f, yRot, 0f);
            yield return null;
        }
        coinImage.transform.localRotation = Quaternion.identity;
        
      
    }

    // ──────────────────────────────────────────────────────────────────────
    // CLOSE ANIMATION
    // ──────────────────────────────────────────────────────────────────────

    private void HideWithAnimation(System.Action onComplete = null)
    {
        StopAllCoroutines();
        StartCoroutine(AnimateClose(onComplete));
    }

    private IEnumerator AnimateClose(System.Action onComplete)
    {
        isAnimating = true;

        // Disable interactions immediately
        SetButtonsInteractable(false);
        if (rootCanvasGroup != null) rootCanvasGroup.interactable = false;

        // Phase 1 ── bg card squishes out (ease-in back)
        if (bgRect != null)
            yield return StartCoroutine(
                ScalePop(bgRect, Vector3.one, Vector3.zero, 0.26f, reverse: true));

        // Phase 2 ── Fade out popup overlay
        yield return StartCoroutine(FadeAlpha(rootCanvasGroup, 1f, 0f, 0.18f));

        // Phase 3 ── Restore HUD, fully hidden, fire callback
        if (hudPanel != null) hudPanel.SetActive(true);

        // Phase 3 ── Fully hidden, fire callback
        SetHiddenState();
        isAnimating = false;

        onComplete?.Invoke();
    }

    // ──────────────────────────────────────────────────────────────────────
    // STATE HELPERS
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>Makes the popup completely invisible and non-interactive without deactivating its GameObject.</summary>
    private void SetHiddenState()
    {
        if (rootCanvasGroup != null)
        {
            rootCanvasGroup.alpha          = 0f;
            rootCanvasGroup.interactable   = false;
            rootCanvasGroup.blocksRaycasts = false;
        }
        // Only restore HUD if popup was actually shown — not on initial Start()
        if (wasVisible && hudPanel != null)
            hudPanel.SetActive(true);

        wasVisible = false;
        if (bgRect != null) bgRect.localScale = Vector3.one; // reset card scale
    }

    // ──────────────────────────────────────────────────────────────────────
    // ANIMATION HELPERS
    // ──────────────────────────────────────────────────────────────────────

    private IEnumerator FadeAlpha(CanvasGroup cg, float from, float to, float duration)
    {
        if (cg == null) yield break;
        cg.alpha = from;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        cg.alpha = to;
    }

    private IEnumerator ScalePop(Transform target, Vector3 from, Vector3 to, float duration, bool reverse)
    {
        target.localScale = from;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t     = Mathf.Clamp01(elapsed / duration);
            float eased = reverse ? EaseInBack(t) : ElasticOut(t);
            target.localScale = Vector3.LerpUnclamped(from, to, eased);
            yield return null;
        }
        target.localScale = to;
    }

    private IEnumerator ScaleTo(Transform target, Vector3 to, float duration)
    {
        Vector3 from  = target.localScale;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            target.localScale = Vector3.Lerp(from, to, t);
            yield return null;
        }
        target.localScale = to;
    }

    private IEnumerator SlideUp(RectTransform rect, float duration)
    {
        if (rect == null) yield break;

        CanvasGroup cg = rect.GetComponent<CanvasGroup>();
        if (cg == null) cg = rect.gameObject.AddComponent<CanvasGroup>();

        Vector2 endPos   = rect.anchoredPosition;
        Vector2 startPos = endPos - new Vector2(0f, 35f);

        cg.alpha              = 0f;
        rect.anchoredPosition = startPos;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            cg.alpha              = t;
            rect.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            yield return null;
        }
        cg.alpha              = 1f;
        rect.anchoredPosition = endPos;
    }

    private void SetAlpha(RectTransform rect, float alpha)
    {
        if (rect == null) return;
        CanvasGroup cg = rect.GetComponent<CanvasGroup>();
        if (cg == null) cg = rect.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = alpha;
    }

    // ──────────────────────────────────────────────────────────────────────
    // EASING
    // ──────────────────────────────────────────────────────────────────────

    private float ElasticOut(float t)
    {
        if (t <= 0f) return 0f;
        if (t >= 1f) return 1f;
        float c4 = (2f * Mathf.PI) / 3f;
        return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
    }

    private float EaseInBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return c3 * t * t * t - c1 * t * t;
    }

    // ──────────────────────────────────────────────────────────────────────
    // BUTTONS
    // ──────────────────────────────────────────────────────────────────────

    private void OnHome()
    {
        Debug.Log("[LevelCompleteUI] Home clicked.");
        if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
        SetButtonsInteractable(false);
        HideWithAnimation(() =>
        {
            if (GameManager.Instance != null) GameManager.Instance.ShowHomeScreen();
        });
    }

    private void OnNextLevel()
    {
        Debug.Log("[LevelCompleteUI] Next Level clicked.");
        if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
        SetButtonsInteractable(false);
        HideWithAnimation(() =>
        {
            if (GameManager.Instance != null) GameManager.Instance.LoadNextLevel();
        });
    }

    private void SetButtonsInteractable(bool value)
    {
        if (homeButton != null)      homeButton.interactable      = value;
        if (nextLevelButton != null) nextLevelButton.interactable = value;
    }
}
