using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class CameraSwipeRotateController : MonoBehaviour
{
    [SerializeField] private Transform cameraPivot;
    [SerializeField] private float swipeThreshold = 50f;
    [SerializeField] private float rotationDuration = 0.3f;

    private InputSystem_Actions controls;
    private Vector2 startPosition;
    private bool isRotating = false;
    private float targetYRotation = 0f;
    private bool swipeStartedOnUI = false;

    /// <summary>
    /// When true, all swipe input is blocked (used during level-complete celebration orbit).
    /// </summary>
    public bool CelebrationMode { get; set; }

    private void Awake()
    {
        controls = new InputSystem_Actions();
        if (cameraPivot != null)
        {
            targetYRotation = cameraPivot.eulerAngles.y;
        }
    }

    private void OnEnable()
    {
        controls.Enable();
        controls.Player.PrimaryContact.started += OnSwipeStarted;
        controls.Player.PrimaryContact.canceled += OnSwipeEnded;
    }

    private void OnDisable() => controls.Disable();

    /// <summary>
    /// Sync the internal target rotation after an external rotation (e.g., celebration orbit).
    /// Call this after the celebration orbit completes so manual swiping resumes from the correct angle.
    /// </summary>
    public void SyncTargetRotation()
    {
        if (cameraPivot != null)
        {
            targetYRotation = cameraPivot.eulerAngles.y;
        }
    }

    /// <summary>
    /// Check if the pointer is currently over any UI element.
    /// </summary>
    private bool IsPointerOverUI()
    {
        // Touch
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            int touchId = Touchscreen.current.primaryTouch.touchId.ReadValue();
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touchId);
        }
        
        // Mouse fallback
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private void OnSwipeStarted(InputAction.CallbackContext context)
    {
        // Block all input during celebration
        if (CelebrationMode) return;

        // Block if game is paused
        if (Time.timeScale == 0f)
        {
            swipeStartedOnUI = true;
            return;
        }

        // Block if touching UI
        swipeStartedOnUI = IsPointerOverUI();
        if (swipeStartedOnUI) return;
        
        // Block if gameplay panel isn't active (home screen, settings, etc.)
        if (GameManager.Instance != null && !GameManager.Instance.IsGameplayActive)
        {
            swipeStartedOnUI = true; // reuse flag to block the end handler
            return;
        }

        if (Touchscreen.current != null)
        {
            startPosition = Touchscreen.current.primaryTouch.position.ReadValue();
        }
        else if (Mouse.current != null)
        {
            startPosition = Mouse.current.position.ReadValue();
        }
    }

    private void OnSwipeEnded(InputAction.CallbackContext context)
    {
        // Block all input during celebration
        if (CelebrationMode) return;

        // If swipe started on UI or not during gameplay, ignore
        if (swipeStartedOnUI) return;
        if (isRotating) return;

        Vector2 endPosition = controls.Player.PrimaryPosition.ReadValue<Vector2>();
        float diffX = endPosition.x - startPosition.x;

        if (Mathf.Abs(diffX) > swipeThreshold)
        {
            float step = diffX < 0 ? -90f : 90f;
            targetYRotation += step;
            StartCoroutine(RotatePivotSmoothly());
        }
    }

    private IEnumerator RotatePivotSmoothly()
    {
        isRotating = true;
        Quaternion startRot = cameraPivot.rotation;
        Quaternion endRot = Quaternion.Euler(0, targetYRotation, 0);

        float elapsed = 0;
        while (elapsed < rotationDuration)
        {
            float t = elapsed / rotationDuration;
            t = t * t * (3f - 2f * t);

            cameraPivot.rotation = Quaternion.Slerp(startRot, endRot, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        cameraPivot.rotation = endRot;
        isRotating = false;
    }
}