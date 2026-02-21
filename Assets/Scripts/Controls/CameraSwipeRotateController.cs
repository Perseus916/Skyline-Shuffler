using System.Collections;
using UnityEngine;
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

    private void Awake()
    {
        controls = new InputSystem_Actions();
        if (cameraPivot != null)
        {
            // Initial sync to current rotation to prevent first-time jump
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

    private void OnSwipeStarted(InputAction.CallbackContext context)
    {
        // FIXED: Using Touchscreen.current bypasses the action buffer delay
        // ensuring startPosition is never (0,0) on frame one
        if (Touchscreen.current != null)
        {
            startPosition = Touchscreen.current.primaryTouch.position.ReadValue();
            Debug.Log($"<color=cyan>Swipe Started At:</color> {startPosition}");
        }
    }

    private void OnSwipeEnded(InputAction.CallbackContext context)
    {
        if (isRotating) return;

        Vector2 endPosition = controls.Player.PrimaryPosition.ReadValue<Vector2>();
        float diffX = endPosition.x - startPosition.x;

        Debug.Log($"<color=white>DiffX:</color> {diffX} | <color=yellow>EndPos:</color> {endPosition}");

        if (Mathf.Abs(diffX) > swipeThreshold)
        {
            // INVERTED LOGIC:
            // diffX < 0 (Swipe Left)  -> Rotate -90 (Counter-Clockwise)
            // diffX > 0 (Swipe Right) -> Rotate +90 (Clockwise)
            float step = diffX < 0 ? -90f : 90f;

            Debug.Log($"<color=green>Rotating Step:</color> {step}");

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
            // SmoothStep curve for better "weight" feel
            t = t * t * (3f - 2f * t);

            cameraPivot.rotation = Quaternion.Slerp(startRot, endRot, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        cameraPivot.rotation = endRot;
        isRotating = false;
    }
}