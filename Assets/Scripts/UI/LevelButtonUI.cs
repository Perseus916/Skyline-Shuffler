using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Level Select Button
/// Handles:
/// - Locked / Unlocked state
/// - Stars (show only earned stars)
/// - Level Number
/// - Button click
/// </summary>
public class LevelButtonUI : MonoBehaviour
{
    [Header("Main Button")]
    [SerializeField] private Button button;

    [Header("UNLOCK SECTION")]
    [SerializeField] private GameObject unlockRoot;
    [SerializeField] private TextMeshProUGUI levelNumberText;

    [Header("Stars")]
    // NOTE: Assign the Star (1), Star (2), Star (3) GameObjects here.
    // These must be GameObjects, not Image components.
    [SerializeField] private GameObject[] stars;

    [SerializeField] private Image unlockBackground;

    [Header("LOCK SECTION")]
    [SerializeField] private GameObject lockRoot;

    [Header("Colors")]
    [SerializeField] private Color normalLevelColor = Color.white;
    [SerializeField] private Color completedLevelColor = new Color(1f, 0.85f, 0.2f);

    private int levelNumber;

    //========================================================
    // SETUP
    //========================================================
    public void Setup(int level)
    {
        levelNumber = level;

        bool isUnlocked = SaveSystem.IsLevelUnlocked(level);
        LevelProgress progress = SaveSystem.GetLevelProgress(level);

        //------------------------------------
        // STEP 1: Hide ALL stars immediately.
        // Must run before unlockRoot is activated.
        //------------------------------------
        HideAllStars();

        //------------------------------------
        // STEP 2: Show/Hide lock & unlock panels
        //------------------------------------
        if (unlockRoot != null)
            unlockRoot.SetActive(isUnlocked);

        if (lockRoot != null)
            lockRoot.SetActive(!isUnlocked);

        //------------------------------------
        // STEP 3: Button interactable
        // Always interactable so locked buttons can be pressed to show the panel
        //------------------------------------
        if (button != null)
        {
            button.interactable = true;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClick);
        }

        //------------------------------------
        // STEP 4: Level number
        //------------------------------------
        if (levelNumberText != null)
            levelNumberText.text = level.ToString();

        //------------------------------------
        // STEP 5: Stars
        // Only show stars that were actually earned.
        // 0-star levels (newly unlocked, not yet played) show nothing.
        //------------------------------------
        if (isUnlocked && progress.stars > 0)
            ShowEarnedStars(progress.stars);

        //------------------------------------
        // STEP 6: Background color
        //------------------------------------
        if (unlockBackground != null)
        {
            unlockBackground.color = progress.completed
                ? completedLevelColor
                : normalLevelColor;
        }
    }

    //========================================================
    // STARS
    //========================================================

    /// <summary>
    /// Hides the Stars container AND every individual star.
    /// Called at the very top of Setup() so no star is ever
    /// visible before we explicitly decide to show it.
    /// </summary>
    private void HideAllStars()
    {
        if (stars == null || stars.Length == 0) return;

        // Hide the Stars container parent (hides all stars at once)
        if (stars[0] != null)
            stars[0].transform.parent.gameObject.SetActive(false);

        // Also deactivate each star individually as a safety net
        foreach (var star in stars)
        {
            if (star != null)
                star.SetActive(false);
        }
    }

    /// <summary>
    /// Activates the Stars container and shows only [starCount] stars.
    /// Stars beyond starCount remain hidden.
    /// </summary>
    private void ShowEarnedStars(int starCount)
    {
        if (stars == null || stars.Length == 0)
        {
            Debug.LogWarning($"[LevelButtonUI] Stars array is null/empty on {gameObject.name}");
            return;
        }

        // Activate the Stars container so children can be visible
        if (stars[0] != null)
            stars[0].transform.parent.gameObject.SetActive(true);

        // Show only the earned stars; hide the rest
        for (int i = 0; i < stars.Length; i++)
        {
            if (stars[i] != null)
                stars[i].SetActive(i < starCount);
        }
    }

    //========================================================
    // BUTTON CLICK
    //========================================================
    private void OnClick()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayButtonClick();

        bool isUnlocked = SaveSystem.IsLevelUnlocked(levelNumber);
        LevelSelectUI levelSelectUI = GetComponentInParent<LevelSelectUI>();

        if (isUnlocked)
        {
            if (levelSelectUI != null)
                levelSelectUI.OnLevelSelected(levelNumber);
            else if (GameManager.Instance != null)
                GameManager.Instance.PlayLevel(levelNumber);
        }
        else
        {
            if (levelSelectUI != null)
                levelSelectUI.ShowLockedLevelPanel(levelNumber);
        }
    }
}