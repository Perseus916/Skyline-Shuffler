using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

/// <summary>
/// Swipeable tab switcher for the Shop panel.
/// Switches between Undo and Hint sub-panels with animated sliding
/// and tab-button scale transitions.
///
/// Setup:
///   1. Place Undo & Hint content panels as children of a clipping container.
///   2. Attach this script to the shop panel (or a manager object).
///   3. Wire the four RectTransform references in Inspector.
///   4. Wire tab button OnClick → OnUndoTabClicked / OnHintTabClicked.
///   5. Add an invisible Image (Raycast Target) over the content area and
///      attach ShopTabSwipeCatcher (see bottom of file) to relay drag events here.
/// </summary>
public class ShopTabSwitcher : MonoBehaviour
{
    // ───────────────────── Inspector ─────────────────────

    [Header("Content Panels")]
    [Tooltip("RectTransform of the Undo shop content panel.")]
    [SerializeField] private RectTransform undoPanel;
    [Tooltip("RectTransform of the Hint shop content panel.")]
    [SerializeField] private RectTransform hintPanel;

    [Header("Tab Buttons")]
    [Tooltip("RectTransform of the Undo tab button at bottom.")]
    [SerializeField] private RectTransform undoTabButton;
    [Tooltip("RectTransform of the Hint tab button at bottom.")]
    [SerializeField] private RectTransform hintTabButton;

    [Header("Swipe Settings")]
    [Tooltip("Minimum horizontal drag (pixels) to trigger a tab switch.")]
    [SerializeField] private float swipeThreshold = 50f;

    [Header("Animation Settings")]
    [Tooltip("Duration of the panel slide animation.")]
    [SerializeField] private float slideDuration = 0.3f;
    [Tooltip("Scale applied to the active tab button.")]
    [SerializeField] private float activeScale = 1.2f;
    [Tooltip("Scale applied to the inactive tab button.")]
    [SerializeField] private float inactiveScale = 0.85f;
    [Tooltip("Enable a subtle idle pulse on the active tab button.")]
    [SerializeField] private bool enableIdlePulse = true;
    [Tooltip("Speed of the idle pulse oscillation.")]
    [SerializeField] private float pulseSpeed = 2.5f;
    [Tooltip("Amplitude of the idle pulse (±).")]
    [SerializeField] private float pulseAmplitude = 0.03f;

    // ───────────────────── Runtime state ─────────────────────

    public enum ShopTab { Undo, Hint }
    private ShopTab currentTab = ShopTab.Undo;

    private float panelWidth;           // cached width for slide offsets
    private Coroutine slideCoroutine;
    private Coroutine undoScaleCoroutine;
    private Coroutine hintScaleCoroutine;
    private Coroutine pulseCoroutine;

    // Drag tracking (fed by ShopTabSwipeCatcher)
    private bool isDragging;
    private float dragStartX;
    private float lastDragX;
    private float dragVelocity;
    private const float flickVelocityThreshold = 800f; // pixels/second

    // ───────────────────── Lifecycle ─────────────────────

    private void OnEnable()
    {
        // Cache the width of the content area (use undoPanel's parent or itself)
        if (undoPanel != null)
        {
            RectTransform parent = undoPanel.parent as RectTransform;
            panelWidth = parent != null ? parent.rect.width : undoPanel.rect.width;
        }

        // Snap to Undo tab immediately (no animation)
        SetTabImmediate(ShopTab.Undo);
    }

    private void OnDisable()
    {
        StopAllAnimations();
    }

    // ───────────────────── Public API (Button OnClick) ─────────────────────

    /// <summary>Wire to the Undo tab button's OnClick event.</summary>
    public void OnUndoTabClicked()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayButtonClick();

        SwitchToTab(ShopTab.Undo);
    }

    /// <summary>Wire to the Hint tab button's OnClick event.</summary>
    public void OnHintTabClicked()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayButtonClick();

        SwitchToTab(ShopTab.Hint);
    }

    // ───────────────────── Drag / Swipe (called by SwipeCatcher) ─────────────────────

    public void OnSwipeBegin(float screenX)
    {
        isDragging = true;
        dragStartX = screenX;
        lastDragX = screenX;
        dragVelocity = 0f;

        // Stop any running slide so user takes control
        if (slideCoroutine != null)
        {
            StopCoroutine(slideCoroutine);
            slideCoroutine = null;
        }
    }

    /// <summary>Called during the drag for real-time panel following.</summary>
    public void OnSwipeDrag(float screenX)
    {
        if (!isDragging) return;
        if (undoPanel == null || hintPanel == null) return;

        // Track velocity for flick detection
        float dt = Time.unscaledDeltaTime;
        if (dt > 0f)
            dragVelocity = (screenX - lastDragX) / dt;
        lastDragX = screenX;

        float delta = screenX - dragStartX;

        // Apply elastic resistance at the edges (can't swipe past first/last tab)
        if ((currentTab == ShopTab.Undo && delta > 0f) ||
            (currentTab == ShopTab.Hint && delta < 0f))
        {
            delta *= 0.3f; // rubber-band effect
        }

        // Move panels with finger
        if (currentTab == ShopTab.Undo)
        {
            undoPanel.anchoredPosition = new Vector2(delta, 0f);
            hintPanel.anchoredPosition = new Vector2(panelWidth + delta, 0f);
        }
        else
        {
            undoPanel.anchoredPosition = new Vector2(-panelWidth + delta, 0f);
            hintPanel.anchoredPosition = new Vector2(delta, 0f);
        }
    }

    public void OnSwipeEnd(float screenX)
    {
        if (!isDragging) return;
        isDragging = false;

        float delta = screenX - dragStartX;

        // Switch tab if swipe distance OR flick velocity exceeds threshold
        bool shouldSwitch = false;
        if (delta < -swipeThreshold && currentTab == ShopTab.Undo)
            shouldSwitch = true;
        else if (delta > swipeThreshold && currentTab == ShopTab.Hint)
            shouldSwitch = true;
        // Flick detection: fast swipe even if short distance
        else if (dragVelocity < -flickVelocityThreshold && currentTab == ShopTab.Undo)
            shouldSwitch = true;
        else if (dragVelocity > flickVelocityThreshold && currentTab == ShopTab.Hint)
            shouldSwitch = true;

        if (shouldSwitch)
        {
            ShopTab newTab = (currentTab == ShopTab.Undo) ? ShopTab.Hint : ShopTab.Undo;
            currentTab = newTab;

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayButtonClick();
        }

        // Animate to final position (either new tab or snap back)
        AnimateToTab(currentTab);
    }

    // ───────────────────── Core switching ─────────────────────

    public void SwitchToTab(ShopTab tab)
    {
        if (tab == currentTab) return;
        currentTab = tab;
        AnimateToTab(tab);
    }

    /// <summary>Immediately places panels & buttons without animation (used on enable).</summary>
    private void SetTabImmediate(ShopTab tab)
    {
        currentTab = tab;
        StopAllAnimations();

        if (undoPanel == null || hintPanel == null) return;

        // Ensure both panels are active so content stays ready
        undoPanel.gameObject.SetActive(true);
        hintPanel.gameObject.SetActive(true);

        if (tab == ShopTab.Undo)
        {
            undoPanel.anchoredPosition = Vector2.zero;
            hintPanel.anchoredPosition = new Vector2(panelWidth, 0f);
        }
        else
        {
            undoPanel.anchoredPosition = new Vector2(-panelWidth, 0f);
            hintPanel.anchoredPosition = Vector2.zero;
        }

        // Button scales
        if (undoTabButton != null)
            undoTabButton.localScale = (tab == ShopTab.Undo) ? Vector3.one * activeScale : Vector3.one * inactiveScale;
        if (hintTabButton != null)
            hintTabButton.localScale = (tab == ShopTab.Hint) ? Vector3.one * activeScale : Vector3.one * inactiveScale;

        // Start pulse on active button
        if (enableIdlePulse)
            StartPulse(tab == ShopTab.Undo ? undoTabButton : hintTabButton);
    }

    // ───────────────────── Animations ─────────────────────

    private void AnimateToTab(ShopTab tab)
    {
        StopAllAnimations();

        // Ensure both panels active
        if (undoPanel != null) undoPanel.gameObject.SetActive(true);
        if (hintPanel != null) hintPanel.gameObject.SetActive(true);

        slideCoroutine = StartCoroutine(SlideRoutine(tab));
        undoScaleCoroutine = StartCoroutine(ScaleButtonRoutine(undoTabButton,
            tab == ShopTab.Undo ? activeScale : inactiveScale, tab == ShopTab.Undo));
        hintScaleCoroutine = StartCoroutine(ScaleButtonRoutine(hintTabButton,
            tab == ShopTab.Hint ? activeScale : inactiveScale, tab == ShopTab.Hint));
    }

    /// <summary>Slides both panels to their target positions with easing.</summary>
    private IEnumerator SlideRoutine(ShopTab tab)
    {
        if (undoPanel == null || hintPanel == null) yield break;

        Vector2 undoStart = undoPanel.anchoredPosition;
        Vector2 hintStart = hintPanel.anchoredPosition;

        Vector2 undoEnd, hintEnd;
        if (tab == ShopTab.Undo)
        {
            undoEnd = Vector2.zero;
            hintEnd = new Vector2(panelWidth, 0f);
        }
        else
        {
            undoEnd = new Vector2(-panelWidth, 0f);
            hintEnd = Vector2.zero;
        }

        float elapsed = 0f;
        while (elapsed < slideDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / slideDuration);
            float ease = EaseOutCubic(t);

            undoPanel.anchoredPosition = Vector2.LerpUnclamped(undoStart, undoEnd, ease);
            hintPanel.anchoredPosition = Vector2.LerpUnclamped(hintStart, hintEnd, ease);
            yield return null;
        }

        undoPanel.anchoredPosition = undoEnd;
        hintPanel.anchoredPosition = hintEnd;
        slideCoroutine = null;
    }

    /// <summary>Animates a tab button's scale. Active buttons get EaseOutBack overshoot.</summary>
    private IEnumerator ScaleButtonRoutine(RectTransform button, float targetScale, bool isBecomingActive)
    {
        if (button == null) yield break;

        Vector3 startScale = button.localScale;
        Vector3 endScale = Vector3.one * targetScale;
        float duration = isBecomingActive ? slideDuration * 1.1f : slideDuration * 0.8f;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float ease = isBecomingActive ? EaseOutBack(t) : EaseOutCubic(t);

            button.localScale = Vector3.LerpUnclamped(startScale, endScale, ease);
            yield return null;
        }

        button.localScale = endScale;

        // Start idle pulse on the newly active button
        if (isBecomingActive && enableIdlePulse)
            StartPulse(button);
    }

    /// <summary>Subtle idle scale pulse on the active tab button.</summary>
    private void StartPulse(RectTransform button)
    {
        if (pulseCoroutine != null)
            StopCoroutine(pulseCoroutine);

        if (button != null)
            pulseCoroutine = StartCoroutine(PulseRoutine(button));
    }

    private IEnumerator PulseRoutine(RectTransform button)
    {
        while (true)
        {
            float scale = activeScale + Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmplitude;
            button.localScale = Vector3.one * scale;
            yield return null;
        }
    }

    // ───────────────────── Easing Functions ─────────────────────

    private static float EaseOutCubic(float t)
    {
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    // ───────────────────── Cleanup ─────────────────────

    private void StopAllAnimations()
    {
        if (slideCoroutine != null) { StopCoroutine(slideCoroutine); slideCoroutine = null; }
        if (undoScaleCoroutine != null) { StopCoroutine(undoScaleCoroutine); undoScaleCoroutine = null; }
        if (hintScaleCoroutine != null) { StopCoroutine(hintScaleCoroutine); hintScaleCoroutine = null; }
        if (pulseCoroutine != null) { StopCoroutine(pulseCoroutine); pulseCoroutine = null; }
    }
}
