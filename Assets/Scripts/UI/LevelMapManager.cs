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

    [Tooltip("Button to focus/scroll back to the current active level when the user has scrolled away.")]
    [SerializeField] private Button currentLevelTargetButton;

    [Header("Levels")]
    [Tooltip("Total amount of levels to generate (e.g., 200).")]
    [Min(1)]
    [SerializeField] private int totalLevels = 200;



    [Header("Current Level Animation")]
    [Tooltip("If true, applies a zoom in/zoom out pulse animation to the player's current active level button.")]
    [SerializeField] private bool animateCurrentLevel = true;

    [Tooltip("Speed of the pulse animation for the current level button.")]
    [SerializeField] private float currentLevelPulseSpeed = 3f;

    [Tooltip("Amount of scaling applied to the current level button (e.g. 0.12 for 12%).")]
    [SerializeField] private float currentLevelPulseAmount = 0.12f;

    [Header("Current Level Glow Ring")]
    [Tooltip("Prefab for the glow/pulse ring around the current level button. Should contain a RectTransform and Image component.")]
    [SerializeField] private GameObject glowRingPrefab;

    [Tooltip("Speed of the glow ring pulse ripple.")]
    [SerializeField] private float glowRingSpeed = 1.5f;

    [Tooltip("Maximum scale size the glow ring expands to.")]
    [SerializeField] private float glowRingMaxScale = 1.8f;

    [Tooltip("Initial alpha transparency of the glow ring.")]
    [Range(0f, 1f)]
    [SerializeField] private float glowRingStartAlpha = 0.8f;

    [Header("Pattern Coordinates")]
    [Tooltip("Exact positions for the first 21 levels.")]
    [SerializeField] private Vector2[] fixedPositions = new Vector2[]
    {
        new Vector2(353f, -49732f),  // Level 1
        new Vector2(3f, -49561f),    // Level 2
        new Vector2(-318f, -49545f), // Level 3
        new Vector2(38f, -49342f),   // Level 4
        new Vector2(-334f, -49175f), // Level 5
        new Vector2(330f, -49038f),  // Level 6
        new Vector2(-323f, -48865f), // Level 7
        new Vector2(350f, -48698f),  // Level 8
        new Vector2(3f, -48548f),    // Level 9
        new Vector2(-318f, -48413f), // Level 10
        new Vector2(3f, -48225f),    // Level 11
        new Vector2(-334f, -48075f), // Level 12
        new Vector2(330f, -47905f),  // Level 13
        new Vector2(-323f, -47734f), // Level 14
        new Vector2(350f, -47575f),  // Level 15
        new Vector2(3f, -47408f),    // Level 16
        new Vector2(-318f, -47250f), // Level 17
        new Vector2(3f, -47085f),    // Level 18
        new Vector2(-334f, -46930f), // Level 19
        new Vector2(330f, -46750f),  // Level 20
        new Vector2(-323f, -46588f)  // Level 21
    };

    [Tooltip("Height of one repeating cycle (background tile height).")]
    [SerializeField] private float cycleHeight = 1146f;

    [Tooltip("Padding added to the bottom of the ScrollView Content height.")]
    [SerializeField] private float bottomPadding = 268f;

    [Tooltip("Padding added to the top of the ScrollView Content height.")]
    [SerializeField] private float topPadding = 300f;

    private bool isGeneratingMore = false;
    private Coroutine scrollCoroutine;

    private RectTransform[] backgroundTiles;
    private float backgroundTileHeight = 1000f;
    private RectTransform backgroundContainer;


    [Header("Behavior")]
    [Tooltip("If true, clears existing children under Content before generating new buttons.")]
    [SerializeField] private bool clearExistingChildren = true;

    [Header("Background Tiling")]
    [Tooltip("Optional background prefab to tile vertically behind the path. Should be a UI prefab with a RectTransform.")]
    [SerializeField] private GameObject backgroundPrefab;

    [Tooltip("Vertical spacing between background tiles. If 0 or less the prefab RectTransform height will be used.")]
    [SerializeField] private float backgroundTileSpacing = 0f;

    private void OnEnable()
    {
        int currentLevel = 1;
        if (SaveSystem.Data != null)
        {
            currentLevel = SaveSystem.Data.currentLevel;
        }
        totalLevels = Mathf.Min(1000, Mathf.Max(200, currentLevel + 200));

        if (currentLevelTargetButton != null)
        {
            currentLevelTargetButton.onClick.RemoveAllListeners();
            currentLevelTargetButton.onClick.AddListener(ScrollToCurrentLevel);
            currentLevelTargetButton.gameObject.SetActive(false);
        }

        ScrollRect scrollRect = content != null ? content.GetComponentInParent<ScrollRect>() : null;
        if (scrollRect != null)
        {
            scrollRect.onValueChanged.RemoveListener(OnScrollValueChanged);
            scrollRect.onValueChanged.AddListener(OnScrollValueChanged);
        }

        // Defer generation until Unity finishes initializing the UI layout/viewport.
        StartCoroutine(GenerateNextFrame());
        // Auto-scroll kept for later debugging (currently may be commented out by design).
        StartCoroutine(ScrollToCurrentLevelRoutine());
    }

    private void OnDisable()
    {
        ScrollRect scrollRect = content != null ? content.GetComponentInParent<ScrollRect>() : null;
        if (scrollRect != null)
        {
            scrollRect.onValueChanged.RemoveListener(OnScrollValueChanged);
        }

        if (backgroundContainer != null)
        {
            Destroy(backgroundContainer.gameObject);
            backgroundContainer = null;
        }
        backgroundTiles = null;
    }

    private void LateUpdate()
    {
        UpdateBackgroundPosition();
    }

    private void UpdateBackgroundPosition()
    {
        if (backgroundTiles == null || backgroundTiles.Length == 0 || content == null) return;

        float scrolledDistance = -content.anchoredPosition.y;
        float spacing = backgroundTileSpacing > 0f ? backgroundTileSpacing : backgroundTileHeight;
        float offset = LoopModulo(scrolledDistance, spacing);

        for (int i = 0; i < backgroundTiles.Length; i++)
        {
            if (backgroundTiles[i] != null)
            {
                backgroundTiles[i].anchoredPosition = new Vector2(0f, i * spacing - offset);
            }
        }
    }

    private float LoopModulo(float val, float m)
    {
        float r = val % m;
        return r < 0 ? r + m : r;
    }

    private void OnScrollValueChanged(Vector2 scrollPosition)
    {
        ScrollRect scrollRect = content != null ? content.GetComponentInParent<ScrollRect>() : null;
        if (scrollRect == null) return;

        if (totalLevels >= 1000) return;

        // If the user scrolls near the top (e.g. verticalNormalizedPosition > 0.85f), generate more levels
        if (scrollRect.verticalNormalizedPosition > 0.85f)
        {
            if (isGeneratingMore) return;
            StartCoroutine(GenerateMoreLevelsRoutine());
        }

        UpdateTargetButtonVisibility();
    }

    private IEnumerator GenerateMoreLevelsRoutine()
    {
        if (totalLevels >= 1000)
        {
            isGeneratingMore = false;
            yield break;
        }
        isGeneratingMore = true;

        // Save scroll position relative to bottom of content
        Vector2 savedAnchoredPosition = content.anchoredPosition;

        // Append 200 levels
        totalLevels = Mathf.Min(1000, totalLevels + 200);

        // Regenerate level map
        Regenerate();

        // Wait for end of frame to ensure all UI elements are layouted and dimensions updated
        yield return new WaitForEndOfFrame();

        // Restore content position so there is no visual jumping or jittering
        content.anchoredPosition = savedAnchoredPosition;

        isGeneratingMore = false;

        UpdateTargetButtonVisibility();
    }

    private IEnumerator GenerateNextFrame()
    {
        yield return new WaitForEndOfFrame();
        Regenerate();
        UpdateTargetButtonVisibility();
    }


    private IEnumerator ScrollToCurrentLevelRoutine()
    {
        // Wait for end of frame so Unity can compute layout and viewport rect sizes
        yield return new WaitForEndOfFrame();
        ScrollToCurrentLevel(false); // Instant scroll on startup
    }

    /// <summary>
    /// Scroll the scroll view content to center on the player's current unlocked level.
    /// Default overload that uses animation.
    /// </summary>
    public void ScrollToCurrentLevel()
    {
        ScrollToCurrentLevel(true);
    }

    /// <summary>
    /// Scroll the scroll view content to center on the player's current unlocked level.
    /// </summary>
    public void ScrollToCurrentLevel(bool animate)
    {
        int currentLevel = 1;
        if (SaveSystem.Data != null)
        {
            currentLevel = SaveSystem.Data.currentLevel;
        }

        // Clamp to valid range just in case
        currentLevel = Mathf.Clamp(currentLevel, 1, totalLevels);

        int levelIndex = currentLevel - 1;
        float yAnchored = GetLevelPosition(levelIndex).y - GetLevelPosition(0).y + bottomPadding;

        ScrollRect scrollRect = content != null ? content.GetComponentInParent<ScrollRect>() : null;
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
            float contentHeight = GetLevelPosition(totalLevels - 1).y - GetLevelPosition(0).y + bottomPadding + topPadding;

            // Target scroll Y position to center the level button in the viewport
            float targetY = (viewportHeight * 0.5f) - yAnchored;

            // Clamp between top (-maxScroll) and bottom (0)
            float maxScroll = contentHeight - viewportHeight;
            if (maxScroll < 0) maxScroll = 0;
            targetY = Mathf.Clamp(targetY, -maxScroll, 0f);

            Vector2 targetPos = new Vector2(content.anchoredPosition.x, targetY);

            if (animate && gameObject.activeInHierarchy)
            {
                if (scrollCoroutine != null)
                {
                    StopCoroutine(scrollCoroutine);
                }
                scrollCoroutine = StartCoroutine(SmoothScrollRoutine(targetPos));
            }
            else
            {
                content.anchoredPosition = targetPos;
                UpdateTargetButtonVisibility();
            }
        }
    }

    private IEnumerator SmoothScrollRoutine(Vector2 targetPosition)
    {
        float duration = 0.5f; // Smooth scroll duration in seconds
        float elapsed = 0f;
        Vector2 startPosition = content.anchoredPosition;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float percent = Mathf.Clamp01(elapsed / duration);

            // Use smooth step interpolation (ease in / ease out)
            float t = percent * percent * (3f - 2f * percent);

            content.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, t);
            UpdateTargetButtonVisibility();
            yield return null;
        }

        content.anchoredPosition = targetPosition;
        UpdateTargetButtonVisibility();
        scrollCoroutine = null;
    }

    /// <summary>
    /// Checks scroll position and shows/hides the focus button based on distance to current level.
    /// </summary>
    private void UpdateTargetButtonVisibility()
    {
        if (currentLevelTargetButton == null) return;

        ScrollRect scrollRect = content != null ? content.GetComponentInParent<ScrollRect>() : null;
        if (scrollRect == null)
        {
            currentLevelTargetButton.gameObject.SetActive(false);
            return;
        }

        int currentLevel = 1;
        if (SaveSystem.Data != null)
        {
            currentLevel = SaveSystem.Data.currentLevel;
        }
        currentLevel = Mathf.Clamp(currentLevel, 1, totalLevels);
        int levelIndex = currentLevel - 1;
        float yAnchored = GetLevelPosition(levelIndex).y - GetLevelPosition(0).y + bottomPadding;

        RectTransform viewport = scrollRect.viewport;
        if (viewport == null)
        {
            viewport = scrollRect.GetComponent<RectTransform>();
        }
        float viewportHeight = viewport != null ? viewport.rect.height : 800f;
        float contentHeight = GetLevelPosition(totalLevels - 1).y - GetLevelPosition(0).y + bottomPadding + topPadding;

        float targetY = (viewportHeight * 0.5f) - yAnchored;
        float maxScroll = contentHeight - viewportHeight;
        if (maxScroll < 0) maxScroll = 0;
        targetY = Mathf.Clamp(targetY, -maxScroll, 0f);

        // If the scroll position is far from targetY, show the button
        float distance = Mathf.Abs(content.anchoredPosition.y - targetY);

        // Show if more than 60% of viewport height away from centering the current level button
        bool shouldShow = distance > (viewportHeight * 0.6f);
        currentLevelTargetButton.gameObject.SetActive(shouldShow);
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

        if (totalLevels > 1000)
        {
            totalLevels = 1000;
        }

        if (totalLevels < 1)
        {
            Debug.LogError("LevelMapManager: totalLevels must be >= 1.");
            return;
        }

        // Programmatically configure content anchors and pivot to bottom-center
        content.anchorMin = new Vector2(0.5f, 0f);
        content.anchorMax = new Vector2(0.5f, 0f);
        content.pivot = new Vector2(0.5f, 0f);

        if (clearExistingChildren)
        {
            // Clear any pre-existing children under Content to avoid duplicates.
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                Destroy(content.GetChild(i).gameObject);
            }
        }

        // Content height should cover the whole vertical span.
        Debug.Log($"[LevelMapManager] Before Resize Height: {content.sizeDelta.y}");

        float contentHeight = GetLevelPosition(totalLevels - 1).y - GetLevelPosition(0).y + bottomPadding + topPadding;
        Vector2 size = new Vector2(content.sizeDelta.x, contentHeight);
        content.sizeDelta = size;

        Debug.Log($"[LevelMapManager] After Resize Height: {content.sizeDelta.y}");

        // Step 1: Pre-calculate all button positions and determine the player's current level
        int currentLevel = 1;
        if (SaveSystem.Data != null)
        {
            currentLevel = SaveSystem.Data.currentLevel;
        }

        Vector2[] buttonPositions = new Vector2[totalLevels];
        for (int levelIndex = 0; levelIndex < totalLevels; levelIndex++)
        {
            buttonPositions[levelIndex] = GetPositionOnCurve(levelIndex);
        }

        // Step 1.5: Optional Background tiling behind everything
        if (backgroundPrefab != null)
        {
            ScrollRect scrollRect = content != null ? content.GetComponentInParent<ScrollRect>() : null;
            Transform parentForBg = null;
            if (scrollRect != null)
            {
                parentForBg = scrollRect.viewport;
                if (parentForBg == null)
                {
                    parentForBg = scrollRect.transform;
                }
            }
            else if (content != null)
            {
                parentForBg = content.parent;
            }

            if (parentForBg != null)
            {
                // Clean up any existing background container under the parent
                Transform existingBg = parentForBg.Find("BackgroundContainer");
                if (existingBg != null)
                {
                    DestroyImmediate(existingBg.gameObject);
                }

                GameObject bgContainer = new GameObject("BackgroundContainer", typeof(RectTransform));
                backgroundContainer = bgContainer.GetComponent<RectTransform>();
                backgroundContainer.SetParent(parentForBg, false);
                backgroundContainer.anchorMin = Vector2.zero;
                backgroundContainer.anchorMax = Vector2.one;
                backgroundContainer.sizeDelta = Vector2.zero;
                backgroundContainer.anchoredPosition = Vector2.zero;
                backgroundContainer.localScale = Vector3.one;
                backgroundContainer.SetAsFirstSibling(); // ensure background is behind other UI

                RectTransform prefabRect = backgroundPrefab.GetComponent<RectTransform>();
                backgroundTileHeight = (prefabRect != null && prefabRect.rect.height > 0f) ? prefabRect.rect.height : 1000f;
                float spacing = backgroundTileSpacing > 0f ? backgroundTileSpacing : backgroundTileHeight;

                // Determine viewport height to know how many tiles to spawn
                RectTransform parentRect = parentForBg.GetComponent<RectTransform>();
                float viewportHeight = parentRect != null ? parentRect.rect.height : 2000f;
                if (viewportHeight <= 0f) viewportHeight = 2000f; // safe fallback

                int tilesNeeded = Mathf.CeilToInt(viewportHeight / spacing) + 1;
                tilesNeeded = Mathf.Max(3, tilesNeeded); // Ensure at least 3 tiles for safety

                backgroundTiles = new RectTransform[tilesNeeded];

                for (int i = 0; i < tilesNeeded; i++)
                {
                    GameObject bg = Instantiate(backgroundPrefab, backgroundContainer);
                    bg.name = $"Background_{i}";

                    RectTransform r = bg.GetComponent<RectTransform>();
                    if (r != null)
                    {
                        r.anchorMin = new Vector2(0.5f, 0f);
                        r.anchorMax = new Vector2(0.5f, 0f);
                        r.pivot = new Vector2(0.5f, 0f);
                        r.sizeDelta = new Vector2(prefabRect != null ? prefabRect.sizeDelta.x : r.sizeDelta.x, spacing);
                        r.localScale = Vector3.one;
                        backgroundTiles[i] = r;
                    }
                }

                UpdateBackgroundPosition();
            }
        }



        // Step 3: Instantiate level buttons
        for (int levelIndex = 0; levelIndex < totalLevels; levelIndex++)
        {
            int levelNumber = levelIndex + 1;
            Vector2 buttonPos = buttonPositions[levelIndex];

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
            buttonRect.anchorMin = new Vector2(0.5f, 0f);
            buttonRect.anchorMax = new Vector2(0.5f, 0f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);

            // Place using anchoredPosition.
            buttonRect.anchoredPosition = GetAnchoredPosition(buttonPos);

            if (levelNumber <= 5)
            {
                Debug.Log($"Level {levelNumber}: {buttonRect.anchoredPosition}");
            }

            // Ensure its anchors/pivot don't fight anchoredPosition.
            buttonRect.localScale = Vector3.one;

            // Preserve existing lock/unlock, stars, and click wiring.
            LevelButtonUI buttonUI = buttonObj.GetComponent<LevelButtonUI>();
            if (buttonUI == null)
            {
                Debug.LogError($"LevelMapManager: Instantiated '{buttonObj.name}' is missing LevelButtonUI component.");
                continue;
            }

            buttonUI.Setup(levelNumber);

            // Highlight current active level with a zoom in / zoom out pulse animation & glow ring
            if (levelNumber == currentLevel)
            {
                if (animateCurrentLevel)
                {
                    CurrentLevelButtonAnimation anim = buttonObj.GetComponent<CurrentLevelButtonAnimation>();
                    if (anim == null)
                    {
                        anim = buttonObj.AddComponent<CurrentLevelButtonAnimation>();
                    }
                    anim.pulseSpeed = currentLevelPulseSpeed;
                    anim.pulseAmount = currentLevelPulseAmount;
                }

                if (glowRingPrefab != null)
                {
                    GameObject ringObj = Instantiate(glowRingPrefab, buttonObj.transform);
                    ringObj.name = "GlowRing";
                    ringObj.transform.SetAsFirstSibling(); // Render behind button graphics

                    RectTransform ringRect = ringObj.GetComponent<RectTransform>();
                    if (ringRect != null)
                    {
                        ringRect.anchorMin = new Vector2(0.5f, 0.5f);
                        ringRect.anchorMax = new Vector2(0.5f, 0.5f);
                        ringRect.pivot = new Vector2(0.5f, 0.5f);
                        ringRect.anchoredPosition = Vector2.zero;
                        ringRect.localScale = Vector3.one;
                    }

                    GlowRingAnimation ringAnim = ringObj.GetComponent<GlowRingAnimation>();
                    if (ringAnim == null)
                    {
                        ringAnim = ringObj.AddComponent<GlowRingAnimation>();
                    }
                    ringAnim.pulseSpeed = glowRingSpeed;
                    ringAnim.maxScaleMultiplier = glowRingMaxScale;
                    ringAnim.startAlpha = glowRingStartAlpha;
                }
            }
        }
    }

    /// <summary>
    /// Gets the exact position of a level button based on the custom pattern.
    /// </summary>
    public Vector2 GetLevelPosition(int levelIndex)
    {
        levelIndex = Mathf.Clamp(levelIndex, 0, totalLevels - 1);

        if (fixedPositions != null && levelIndex < fixedPositions.Length)
        {
            return fixedPositions[levelIndex];
        }

        // For levels beyond the fixed positions, repeat the cycle (levels 15 to 21, indices 14 to 20)
        int steps = levelIndex - 14;
        int cycle = steps / 7;
        int rem = steps % 7;

        int sourceIndex = 14 + rem;
        Vector2 sourcePos = (fixedPositions != null && sourceIndex < fixedPositions.Length) 
            ? fixedPositions[sourceIndex] 
            : Vector2.zero;

        float x = sourcePos.x;
        float y = sourcePos.y + cycle * cycleHeight;

        return new Vector2(x, y);
    }

    /// <summary>
    /// Converts a negative absolute position to a bottom-anchored position.
    /// </summary>
    public Vector2 GetAnchoredPosition(Vector2 absolutePos)
    {
        float x = absolutePos.x;
        float y = absolutePos.y - GetLevelPosition(0).y + bottomPadding;
        return new Vector2(x, y);
    }

    /// <summary>
    /// Computes the exact position along the winding path at a continuous parameter t.
    /// </summary>
    public Vector2 GetPositionOnCurve(float t)
    {
        int i = Mathf.FloorToInt(t);
        float fraction = t - i;

        Vector2 p0 = GetLevelPosition(i - 1);
        Vector2 p1 = GetLevelPosition(i);
        Vector2 p2 = GetLevelPosition(i + 1);
        Vector2 p3 = GetLevelPosition(i + 2);

        float x = CatmullRom(p0.x, p1.x, p2.x, p3.x, fraction);
        float y = Mathf.Lerp(p1.y, p2.y, fraction);

        return new Vector2(x, y);
    }

    private float CatmullRom(float p0, float p1, float p2, float p3, float t)
    {
        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t
        );
    }
}

