using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Level Select Button
/// Handles:
/// - Locked / Unlocked state
/// - Stars
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
    [SerializeField] private Image[] stars;

    [SerializeField] private Color activeStarColor = Color.white;

    [SerializeField] private Color inactiveStarColor = Color.black;

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
        // SHOW / HIDE LOCK & UNLOCK SECTION
        //------------------------------------
        if (unlockRoot != null)
            unlockRoot.SetActive(isUnlocked);

        if (lockRoot != null)
            lockRoot.SetActive(!isUnlocked);

        //------------------------------------
        // BUTTON INTERACTABLE
        //------------------------------------
        if (button != null)
        {
            button.interactable = isUnlocked;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClick);
        }

        //------------------------------------
        // LEVEL NUMBER
        //------------------------------------
        if (levelNumberText != null)
            levelNumberText.text = level.ToString();

        //------------------------------------
        // STARS
        //------------------------------------
        SetupStars(progress.stars);

        //------------------------------------
        // COMPLETED COLOR
        //------------------------------------
        if (unlockBackground != null)
        {
            if (progress.completed)
                unlockBackground.color = completedLevelColor;
            else
                unlockBackground.color = normalLevelColor;
        }
    }

    //========================================================
// STARS
//========================================================
private void SetupStars(int starCount)
{
    if (stars == null || stars.Length == 0)
    {
        Debug.LogWarning($"[LevelButtonUI] Stars array is null or empty on {gameObject.name}");
        return;
    }

    Debug.Log($"[LevelButtonUI] Setting {starCount} stars out of {stars.Length} on Level {levelNumber}");

    for (int i = 0; i < stars.Length; i++)
    {
        if (stars[i] != null)
            stars[i].color = inactiveStarColor;
        else
            Debug.LogWarning($"[LevelButtonUI] stars[{i}] is NULL on {gameObject.name}");
    }

    for (int i = 0; i < starCount && i < stars.Length; i++)
    {
        if (stars[i] != null)
            stars[i].color = activeStarColor;
    }
}
    //========================================================
    // BUTTON CLICK
    //========================================================
    private void OnClick()
    {
        LevelSelectUI levelSelectUI = GetComponentInParent<LevelSelectUI>();

        if (levelSelectUI != null)
        {
            levelSelectUI.OnLevelSelected(levelNumber);
        }
        else if (GameManager.Instance != null)
        {
            GameManager.Instance.PlayLevel(levelNumber);
        }
    }
}