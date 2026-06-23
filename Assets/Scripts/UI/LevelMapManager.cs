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

    [Header("Path Settings")]
    [Tooltip("Prefab for the path points/dots between buttons. Should contain a RectTransform and Image component.")]
    [SerializeField] private GameObject pathDotPrefab;

    [Tooltip("Distance (in pixels) between each path point/dot.")]
    [SerializeField] private float dotSpacing = 30f;

    [Tooltip("Color of the path dots leading to an unlocked level.")]
    [SerializeField] private Color unlockedPathColor = Color.white;

    [Tooltip("Color of the path dots leading to a locked level.")]
    [SerializeField] private Color lockedPathColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

    [Tooltip("Rotation offset (in degrees) for the path dots.")]
    [SerializeField] private float dotRotationOffset = 0f;

    [Tooltip("Pattern of scales for consecutive dots to create a rhythm (e.g. 1.2, 0.8, 0.8). Leave empty for constant scale.")]
    [SerializeField] private float[] dotScalePattern = new float[] { 1.2f, 0.8f, 0.8f };

    [Header("Path Variety & Scatter")]
    [Tooltip("If true, rotates the path dots to align with the winding curve's direction.")]
    [SerializeField] private bool rotateToPathDirection = true;

    [Tooltip("If true, gives each path dot a random rotation for a scattered, natural look.")]
    [SerializeField] private bool useRandomRotation = false;

    [Tooltip("Minimum random rotation angle (in degrees).")]
    [SerializeField] private float minRandomRotation = -15f;

    [Tooltip("Maximum random rotation angle (in degrees).")]
    [SerializeField] private float maxRandomRotation = 15f;

    [Tooltip("If true, randomly flips the X and Y axes of the dots to prevent repetitive texture patterns.")]
    [SerializeField] private bool useRandomFlip = false;

    [Tooltip("Adds a small random offset (in pixels) to the position of each dot to make the path look organic.")]
    [SerializeField] private float scatterAmount = 0f;

    [Header("Path Animations")]
    [Tooltip("If true, path dots leading to/on unlocked levels will animate with a flowing wave effect.")]
    [SerializeField] private bool enableWaveAnimation = true;

    [Tooltip("Speed of the wave animation.")]
    [SerializeField] private float waveSpeed = 4f;

    [Tooltip("Amount of scaling applied by the wave (e.g. 0.12 for 12% scale change).")]
    [SerializeField] private float waveAmount = 0.12f;

    [Tooltip("Delay between adjacent dots to create the flowing wave propagation.")]
    [SerializeField] private float waveSpacingDelay = 0.25f;

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

    [Header("Candy Crush Path")]
    [Tooltip("Horizontal sweep amplitude (how far left/right the road/nodes travel).")]
    [SerializeField] private float horizontalAmplitude = 250f;

    [Tooltip("Custom X pattern for level map column alignments (0 = center, -1 = left, 1 = right). Repeating sequence.")]
    [SerializeField] private float[] customXPattern = new float[] { 0f, -1f, 1f, -1f, 1f };

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
        // Defer generation until Unity finishes initializing the UI layout/viewport.
        StartCoroutine(GenerateNextFrame());
        // Auto-scroll kept for later debugging (currently may be commented out by design).
        StartCoroutine(ScrollToCurrentLevelRoutine());
    }

    private IEnumerator GenerateNextFrame()
    {
        yield return new WaitForEndOfFrame();
        Regenerate();
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

        // Step 2: Instantiate path dots along the winding curve
        if (pathDotPrefab != null)
        {
            GameObject pathContainer = new GameObject("PathContainer", typeof(RectTransform));
            RectTransform pathContainerRect = pathContainer.GetComponent<RectTransform>();
            pathContainerRect.SetParent(content, false);
            pathContainerRect.anchorMin = Vector2.zero;
            pathContainerRect.anchorMax = Vector2.one;
            pathContainerRect.sizeDelta = Vector2.zero;
            pathContainerRect.anchoredPosition = Vector2.zero;
            pathContainerRect.localScale = Vector3.one;
            pathContainerRect.SetAsFirstSibling(); // Draw in the background

            float t = 0f;
            Vector2 currentPos = GetPositionOnCurve(t);
            int dotCount = 0;

            while (t < totalLevels - 1)
            {
                // Calculate the speed along the curve to step by a constant distance: ds/dt = speed
                float dt_epsilon = 0.01f;
                float x1 = GetPositionOnCurve(t).x;
                float x2 = GetPositionOnCurve(t + dt_epsilon).x;
                float dx = (x2 - x1) / dt_epsilon;
                float dy = verticalSpacing;
                float speed = Mathf.Sqrt(dx * dx + dy * dy);

                // If speed is zero (should not happen), fallback to spacing
                float dt = speed > 0.001f ? (dotSpacing / speed) : 0.1f;
                t += dt;

                if (t > totalLevels - 1) break;

                Vector2 nextPos = GetPositionOnCurve(t);
                Vector2 dir = (nextPos - currentPos).normalized;
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

                // Apply scatter offset if configured
                Vector2 finalPos = nextPos;
                if (scatterAmount > 0f)
                {
                    float scatterX = Random.Range(-scatterAmount, scatterAmount);
                    float scatterY = Random.Range(-scatterAmount, scatterAmount);
                    finalPos += new Vector2(scatterX, scatterY);
                }

                // Instantiate dot
                GameObject dotObj = Instantiate(pathDotPrefab, pathContainerRect);
                dotObj.name = $"PathDot_{dotCount}";

                RectTransform dotRect = dotObj.GetComponent<RectTransform>();
                if (dotRect != null)
                {
                    dotRect.anchorMin = new Vector2(0.5f, 1f);
                    dotRect.anchorMax = new Vector2(0.5f, 1f);
                    dotRect.pivot = new Vector2(0.5f, 0.5f);
                    dotRect.anchoredPosition = finalPos;

                    // Determine rotation
                    float finalAngle = rotateToPathDirection ? (angle + dotRotationOffset) : dotRotationOffset;
                    if (useRandomRotation)
                    {
                        finalAngle += Random.Range(minRandomRotation, maxRandomRotation);
                    }
                    dotRect.localRotation = Quaternion.Euler(0, 0, finalAngle);
                }

                // Apply size/scale patterns if configured
                float baseScaleMultiplier = 1f;
                if (dotScalePattern != null && dotScalePattern.Length > 0)
                {
                    baseScaleMultiplier = dotScalePattern[dotCount % dotScalePattern.Length];
                }
                Vector3 baseScale = Vector3.one * baseScaleMultiplier;

                // Apply random flip if configured
                if (useRandomFlip)
                {
                    float flipX = Random.value > 0.5f ? 1f : -1f;
                    float flipY = Random.value > 0.5f ? 1f : -1f;
                    baseScale.x *= flipX;
                    baseScale.y *= flipY;
                }

                if (dotRect != null)
                {
                    dotRect.localScale = baseScale;
                }

                // Determine if this path point is unlocked
                int leadingLevel = Mathf.CeilToInt(t + 1);
                bool isUnlocked = leadingLevel <= currentLevel;

                Image dotImage = dotObj.GetComponent<Image>();
                if (dotImage != null)
                {
                    dotImage.color = isUnlocked ? unlockedPathColor : lockedPathColor;
                }

                // Set up wave animation
                if (enableWaveAnimation)
                {
                    PathDotUI anim = dotObj.GetComponent<PathDotUI>();
                    if (anim == null)
                    {
                        anim = dotObj.AddComponent<PathDotUI>();
                    }
                    anim.SetupAnimation(isUnlocked, waveSpeed, waveAmount, dotCount * waveSpacingDelay, baseScale);
                }

                currentPos = nextPos;
                dotCount++;
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
            buttonRect.anchorMin = new Vector2(0.5f, 1f);
            buttonRect.anchorMax = new Vector2(0.5f, 1f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);

            // Place using anchoredPosition.
            buttonRect.anchoredPosition = buttonPos;

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
    /// Computes the exact position along the winding path at a continuous parameter t.
    /// </summary>
    public Vector2 GetPositionOnCurve(float t)
    {
        float xMultiplier = GetInterpolatedX(t);
        float x = xMultiplier * horizontalAmplitude;
        float y = startY - ((totalLevels - 1 - t) * verticalSpacing);
        return new Vector2(x, y);
    }

    private float GetPatternValue(int index)
    {
        if (customXPattern == null || customXPattern.Length == 0)
        {
            float[] fallback = new float[] { 0f, -1f, 1f, -1f, 1f };
            int len = fallback.Length;
            int mod = ((index % len) + len) % len;
            return fallback[mod];
        }
        else
        {
            int len = customXPattern.Length;
            int mod = ((index % len) + len) % len;
            return customXPattern[mod];
        }
    }

    private float GetInterpolatedX(float t)
    {
        int i = Mathf.FloorToInt(t);
        float fraction = t - i;

        float p0 = GetPatternValue(i - 1);
        float p1 = GetPatternValue(i);
        float p2 = GetPatternValue(i + 1);
        float p3 = GetPatternValue(i + 2);

        return CatmullRom(p0, p1, p2, p3, fraction);
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

