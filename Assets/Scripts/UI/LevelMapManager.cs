using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// LevelMapManager
/// Generates a Candy-Crush-style zigzag path of level buttons inside a ScrollView Content.
///
/// Key points:
/// - Uses your existing level button prefab.
/// - Instantiates UI buttons as children of the ScrollView Content.
/// - Positions buttons via RectTransform.anchoredPosition.
/// - Calls LevelButtonUI.Setup(levelNumber) to preserve all click/lock/unlock/stars logic.
/// - Automatically resizes Content height so all levels can be scrolled.
///
/// Attach this script to a GameObject named LevelMapManager (or any GameObject in the scene).
/// </summary>
public class LevelMapManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("ScrollView Content RectTransform (the object with a RectTransform used by the Scroll View).")]
    [SerializeField] private RectTransform content;

    [Tooltip("Your existing level button prefab. Must include LevelButtonUI component.")]
    [SerializeField] private GameObject levelButtonPrefab;

    [Header("Levels")]
    [Tooltip("Total amount of levels to generate (e.g., 200).")]
    [Min(1)]
    [SerializeField] private int totalLevels = 200;



    [Header("Candy Crush Path")]
    [Tooltip("Horizontal sweep amplitude (how far left/right the road/nodes travel).")]
    [SerializeField] private float horizontalAmplitude = 250f;

    [Tooltip("Horizontal sweep frequency for the S-curve (higher = more wiggles).")]
    [SerializeField] private float horizontalFrequency = 0.25f;

    [Header("Spacing (Y)")]
    [Tooltip("Y distance between each level button along the winding path.")]
    [SerializeField] private float verticalSpacing = 110f;


    [Tooltip("Starting Y position for levelIndex = 0 (Level 1). Final Y is: startY - (levelIndex * verticalSpacing).")]
    [SerializeField] private float startY = 0f;


    [Header("Behavior")]
    [Tooltip("If true, clears existing children under Content before generating new buttons.")]
    [SerializeField] private bool clearExistingChildren = true;

    private void OnEnable()
    {
        Regenerate();
        StartCoroutine(ScrollToCurrentLevelRoutine());
    }

    private IEnumerator ScrollToCurrentLevelRoutine()
    {
        // Wait for end of frame so Unity can compute layout and viewport rect sizes
        yield return new WaitForEndOfFrame();
        ScrollToCurrentLevel();
    }

    /// <summary>
    /// Scroll the scroll view content to center on the player's current unlocked level.
    /// </summary>
    public void ScrollToCurrentLevel()
    {
        int currentLevel = 1;
        if (SaveSystem.Data != null)
        {
            currentLevel = SaveSystem.Data.currentLevel;
        }

        // Clamp to valid range just in case
        currentLevel = Mathf.Clamp(currentLevel, 1, totalLevels);

        int levelIndex = currentLevel - 1;
        float y = startY - ((totalLevels - 1 - levelIndex) * verticalSpacing);

        ScrollRect scrollRect = content.GetComponentInParent<ScrollRect>();
        if (scrollRect != null)
        {
            // Force Canvas update to ensure viewport rect sizes are computed correctly
            Canvas.ForceUpdateCanvases();

            RectTransform viewport = scrollRect.viewport;
            if (viewport == null)
            {
                viewport = scrollRect.GetComponent<RectTransform>();
            }

            float viewportHeight = viewport != null ? viewport.rect.height : 800f;
            float contentHeight = totalLevels * verticalSpacing;

            // Target scroll Y position to center the level button in the viewport
            float targetY = -y - (viewportHeight * 0.5f);

            // Clamp between top (0) and bottom (contentHeight - viewportHeight)
            float maxScroll = contentHeight - viewportHeight;
            if (maxScroll < 0) maxScroll = 0;
            targetY = Mathf.Clamp(targetY, 0, maxScroll);

            content.anchoredPosition = new Vector2(content.anchoredPosition.x, targetY);
        }
    }

    /// <summary>
    /// Public so you can call it again from the Inspector during iteration.
    /// </summary>
    [ContextMenu("Regenerate Levels")]
    public void Regenerate()
    {
        if (content == null)
        {
            Debug.LogError("LevelMapManager: 'content' RectTransform is not assigned.");
            return;
        }

        if (levelButtonPrefab == null)
        {
            Debug.LogError("LevelMapManager: 'levelButtonPrefab' is not assigned.");
            return;
        }

        if (totalLevels < 1)
        {
            Debug.LogError("LevelMapManager: totalLevels must be >= 1.");
            return;
        }

        if (clearExistingChildren)
        {
            // Clear any pre-existing buttons under Content to avoid duplicates.
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                Destroy(content.GetChild(i).gameObject);
            }

        }

        // Content height should cover the whole vertical span.
        // You requested: totalLevels * verticalSpacing.
        Debug.Log($"[LevelMapManager] Before Resize Height: {content.sizeDelta.y}");

        Vector2 size = new Vector2(content.sizeDelta.x, totalLevels * verticalSpacing);
        content.sizeDelta = size;


        Debug.Log($"[LevelMapManager] After Resize Height: {content.sizeDelta.y}");


        // Center horizontally inside Content.
        // Content pivot impacts this; we treat map center as the Content's rect center in local space.
        float mapCenterX = content.rect.width * 0.5f;

        for (int levelIndex = 0; levelIndex < totalLevels; levelIndex++)
        {
            int levelNumber = levelIndex + 1;


            // Smooth winding route (sine wave) instead of fixed columns.
            // Candy Crush-like wider sweeping horizontal motion.
            // phase is based on levelIndex so the curve is continuous.
            float x = Mathf.Sin(levelIndex * horizontalFrequency) * horizontalAmplitude;


            // Candy-Crush ordering requirement:
            // Level 1 at bottom, level increases upward.
            // levelIndex is 0-based, so Level 200 (index 199) ends near the top.
            float y = startY - ((totalLevels - 1 - levelIndex) * verticalSpacing);






            // Instantiate.
            GameObject buttonObj = Instantiate(levelButtonPrefab, content);
            buttonObj.name = $"LevelButton_{levelNumber}";

            RectTransform buttonRect = buttonObj.GetComponent<RectTransform>();
            if (buttonRect == null)
            {
                Debug.LogError($"LevelMapManager: levelButtonPrefab '{levelButtonPrefab.name}' has no RectTransform.");
                continue;
            }

            // Force a predictable UI anchoring so anchoredPosition behaves consistently.
            buttonRect.anchorMin = new Vector2(0.5f, 1f);
            buttonRect.anchorMax = new Vector2(0.5f, 1f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);

            // Place using anchoredPosition.
            buttonRect.anchoredPosition = new Vector2(x, y);

            if(levelNumber <= 5)
{
    Debug.Log($"Level {levelNumber}: {buttonRect.anchoredPosition}");
}






            // Ensure its anchors/pivot don't fight anchoredPosition.
            // (We don't overwrite anchors, but we can keep it at a sane scale.)
            buttonRect.localScale = Vector3.one;

            // Preserve existing lock/unlock, stars, and click wiring.
            LevelButtonUI buttonUI = buttonObj.GetComponent<LevelButtonUI>();
            if (buttonUI == null)
            {
                Debug.LogError($"LevelMapManager: Instantiated '{buttonObj.name}' is missing LevelButtonUI component.");
                continue;
            }

            buttonUI.Setup(levelNumber);
        }
    }
}

