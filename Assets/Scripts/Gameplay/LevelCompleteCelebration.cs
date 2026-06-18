using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Orchestrates the level-complete celebration sequence:
/// 1. Freeze input + initial flash
/// 2. Confetti burst + building bounce VFX
/// 3. 360° cinematic camera orbit with zoom
/// 4. Sparkle particles from buildings during orbit
/// 5. Starburst flash at orbit end
/// 6. Show Level Complete popup
///
/// Attach to a persistent GameObject in the scene.
/// Wire up references in the Inspector.
/// </summary>
public class LevelCompleteCelebration : MonoBehaviour
{
    [Header("Camera References")]
    [Tooltip("The pivot transform that the camera orbits around")]
    [SerializeField] private Transform cameraPivot;
    [Tooltip("The main camera (for zoom effects)")]
    [SerializeField] private Camera mainCamera;
    [Tooltip("The swipe controller to disable during celebration")]
    [SerializeField] private CameraSwipeRotateController swipeController;

    [Header("Orbit Settings")]
    [Tooltip("Duration of the full 360° camera orbit in seconds")]
    [SerializeField] private float orbitDuration = 3f;
    [Tooltip("How much to zoom in during the orbit (subtracted from ortho size)")]
    [SerializeField] private float zoomAmount = 1.5f;
    [Tooltip("Pause before starting the orbit (lets the player see they won)")]
    [SerializeField] private float preOrbitDelay = 0.4f;
    [Tooltip("Pause after orbit before showing popup")]
    [SerializeField] private float postOrbitDelay = 0.5f;

    [Header("Confetti Settings")]
    [Tooltip("Number of confetti particles in the burst")]
    [SerializeField] private int confettiCount = 300;
    [Tooltip("Height offset above the city center for confetti spawn")]
    [SerializeField] private float confettiHeightOffset = 12f;

    [Header("VFX Settings")]
    [Tooltip("Number of sparkle particles per building stack")]
    [SerializeField] private int sparklesPerStack = 30;
    [Tooltip("Delay between building bounces (staggered animation)")]
    [SerializeField] private float bounceCascadeDelay = 0.15f;

    [Header("Celebration Materials")]
    [Tooltip("Material to use for sparkle particles. If left blank, it will attempt to find a default particle shader.")]
    [SerializeField] private Material sparkleMaterial;
    [Tooltip("Material to use for starburst particles. If left blank, it will attempt to find a default particle shader.")]
    [SerializeField] private Material starburstMaterial;
    [Tooltip("Material to use for confetti particles. If left blank, it will attempt to find a default particle shader.")]
    [SerializeField] private Material confettiMaterial;

    // Internal state
    private bool isCelebrating;
    private float originalOrthoSize;
    private Quaternion originalPivotRotation;

    // Callback data stored for popup
    private int cachedLevelNumber;
    private int cachedMovesTaken;
    private int cachedOptimalMoves;
    private int cachedStars;
    private int cachedCoinsEarned;
    private LevelCompleteUI cachedLevelCompleteUI;

    private void Awake()
    {
        if (sparkleMaterial != null) CelebrationVFX.SparkleMaterial = sparkleMaterial;
        if (starburstMaterial != null) CelebrationVFX.StarburstMaterial = starburstMaterial;
        if (confettiMaterial != null) ConfettiController.ConfettiMaterial = confettiMaterial;
    }

    /// <summary>
    /// Start the celebration sequence. Called by GameManager instead of showing popup directly.
    /// </summary>
    /// <param name="levelNumber">Completed level number</param>
    /// <param name="movesTaken">Moves used by the player</param>
    /// <param name="optimalMoves">Optimal/par moves for the level</param>
    /// <param name="stars">Stars earned (1-3)</param>
    /// <param name="coinsEarned">Coins awarded</param>
    /// <param name="levelCompleteUI">The UI to show after celebration</param>
    /// <param name="buildingStacks">All building stacks in the level (for VFX positioning)</param>
    public void PlayCelebration(int levelNumber, int movesTaken, int optimalMoves, int stars,
                                  int coinsEarned, LevelCompleteUI levelCompleteUI,
                                  List<BuildingStack> buildingStacks = null)
    {
        Debug.Log($"[LevelCompleteCelebration] PlayCelebration called. Level: {levelNumber}, UI: {(levelCompleteUI != null ? "Assigned" : "Null")}");
        if (isCelebrating) 
        {
            Debug.LogWarning("[LevelCompleteCelebration] Already celebrating. Ignoring call.");
            return;
        }

        // Cache for later popup
        cachedLevelNumber = levelNumber;
        cachedMovesTaken = movesTaken;
        cachedOptimalMoves = optimalMoves;
        cachedStars = stars;
        cachedCoinsEarned = coinsEarned;
        cachedLevelCompleteUI = levelCompleteUI;

        StartCoroutine(CelebrationSequence(buildingStacks));
    }

    private IEnumerator CelebrationSequence(List<BuildingStack> buildingStacks)
    {
        isCelebrating = true;

        // ── Phase 0: Setup ──
        // Disable camera swipe during celebration
        if (swipeController != null)
            swipeController.CelebrationMode = true;

        // Store original camera state
        if (mainCamera != null)
            originalOrthoSize = mainCamera.orthographicSize;
        if (cameraPivot != null)
            originalPivotRotation = cameraPivot.rotation;

        // Calculate city center for effects
        Vector3 cityCenter = CalculateCityCenter(buildingStacks);

        // ── Phase 1: Initial Flash + Confetti ──
        yield return new WaitForSeconds(preOrbitDelay);

        // Starburst flash at the center
        CelebrationVFX.SpawnStarburst(cityCenter + Vector3.up * 5f, 1.5f);

        // Big confetti burst from above
        ConfettiController.SpawnBurst(
            cityCenter + Vector3.up * confettiHeightOffset,
            confettiCount
        );

        // Second smaller confetti burst slightly delayed
        yield return new WaitForSeconds(0.2f);
        ConfettiController.SpawnBurst(
            cityCenter + Vector3.up * (confettiHeightOffset + 3f),
            confettiCount / 2
        );

        // Reward glow at center
        CelebrationVFX.SpawnRewardGlow(cityCenter, orbitDuration + 1f, 4f);

        // ── Phase 2: Building Bounce Cascade + Sparkles ──
        if (buildingStacks != null)
        {
            for (int i = 0; i < buildingStacks.Count; i++)
            {
                if (buildingStacks[i] == null) continue;

                // Staggered bounce animation
                StartCoroutine(CelebrationVFX.AnimateBuildingBounce(
                    buildingStacks[i].transform,
                    i * bounceCascadeDelay
                ));

                // Sparkles rising from each building
                Vector3 stackTop = buildingStacks[i].transform.position + Vector3.up * 4f;
                CelebrationVFX.SpawnSparkles(stackTop, orbitDuration, sparklesPerStack);
            }
        }

        // Brief pause to let initial effects register
        yield return new WaitForSeconds(0.3f);

        // ── Phase 3: 360° Camera Orbit with Zoom ──
        yield return StartCoroutine(CinematicOrbit());

        // ── Phase 4: Final Flash + Show Popup ──
        // One more starburst at the end
        CelebrationVFX.SpawnStarburst(cityCenter + Vector3.up * 5f, 1f);

        // Small pause before popup
        yield return new WaitForSeconds(postOrbitDelay);

        // Show the Level Complete popup
        if (cachedLevelCompleteUI != null)
        {
            Debug.Log("[LevelCompleteCelebration] Showing LevelCompleteUI via cached reference...");
            cachedLevelCompleteUI.Show(
                cachedLevelNumber,
                cachedMovesTaken,
                cachedOptimalMoves,
                cachedStars,
                cachedCoinsEarned
            );
        }
        else
        {
            Debug.LogError("[LevelCompleteCelebration] ERROR: cachedLevelCompleteUI is NULL! The popup will not open. Check if GameManager has LevelCompleteUI assigned in inspector.");
        }

        // Re-enable camera swipe
        if (swipeController != null)
            swipeController.CelebrationMode = false;

        isCelebrating = false;

        Debug.Log("<color=magenta>🎉 Celebration sequence complete!</color>");
    }

    /// <summary>
    /// Smooth 360° camera orbit around the city with zoom-in and zoom-out.
    /// Uses an ease-in-out curve for cinematic feel.
    /// </summary>
    private IEnumerator CinematicOrbit()
    {
        if (cameraPivot == null)
        {
            yield return new WaitForSeconds(orbitDuration);
            yield break;
        }

        float startY = cameraPivot.eulerAngles.y;
        float targetY = startY + 360f;

        float startOrtho = originalOrthoSize;
        float zoomedOrtho = Mathf.Max(startOrtho - zoomAmount, 2f); // Don't zoom too close

        float elapsed = 0f;

        while (elapsed < orbitDuration)
        {
            elapsed += Time.deltaTime;
            float rawT = Mathf.Clamp01(elapsed / orbitDuration);

            // Smooth ease-in-out using sine curve (very cinematic)
            float orbitT = EaseInOutSine(rawT);

            // Rotation — full 360°
            float currentY = Mathf.Lerp(startY, targetY, orbitT);
            cameraPivot.rotation = Quaternion.Euler(
                cameraPivot.eulerAngles.x,
                currentY,
                cameraPivot.eulerAngles.z
            );

            // Zoom — zoom in during middle of orbit, zoom back out at end
            // Bell curve: peaks at t=0.5
            float zoomT = ZoomBellCurve(rawT);
            if (mainCamera != null && mainCamera.orthographic)
            {
                mainCamera.orthographicSize = Mathf.Lerp(startOrtho, zoomedOrtho, zoomT);
            }

            yield return null;
        }

        // Ensure we land exactly where we started (full 360°)
        cameraPivot.rotation = Quaternion.Euler(
            cameraPivot.eulerAngles.x,
            startY,
            cameraPivot.eulerAngles.z
        );

        // Restore original zoom
        if (mainCamera != null && mainCamera.orthographic)
        {
            mainCamera.orthographicSize = startOrtho;
        }
    }

    /// <summary>
    /// Calculate the geometric center of all building stacks.
    /// </summary>
    private Vector3 CalculateCityCenter(List<BuildingStack> stacks)
    {
        if (stacks == null || stacks.Count == 0)
        {
            return cameraPivot != null ? cameraPivot.position : Vector3.zero;
        }

        Vector3 center = Vector3.zero;
        int count = 0;
        foreach (var stack in stacks)
        {
            if (stack != null)
            {
                center += stack.transform.position;
                count++;
            }
        }

        return count > 0 ? center / count : Vector3.zero;
    }

    // ========================================
    // EASING FUNCTIONS
    // ========================================

    /// <summary>
    /// Sine ease-in-out for smooth, cinematic rotation.
    /// </summary>
    private float EaseInOutSine(float t)
    {
        return -(Mathf.Cos(Mathf.PI * t) - 1f) / 2f;
    }

    /// <summary>
    /// Bell curve that peaks at t=0.5, used for zoom effect.
    /// Returns 0 at t=0, 1 at t=0.5, 0 at t=1.
    /// </summary>
    private float ZoomBellCurve(float t)
    {
        // Sine bell: sin(π * t)
        return Mathf.Sin(Mathf.PI * t);
    }

    /// <summary>
    /// Whether a celebration is currently playing.
    /// </summary>
    public bool IsCelebrating => isCelebrating;
}
