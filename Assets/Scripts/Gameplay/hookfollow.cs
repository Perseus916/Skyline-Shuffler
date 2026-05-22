using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Controls a crane hook so it can follow the top floor of a given BuildingStack
/// and be moved smoothly to a stack when requested.
/// Usage:
/// - Assign the hook Transform in the inspector.
/// - Call FollowStack(stack) to make the hook continuously track the stack's top floor.
/// - Call StopFollow() to stop tracking.
/// - Call MoveHookToStack(stack, duration, onComplete) to move the hook to the stack once.
/// Note: This script does not automatically hook into GameplayManager; call the public
/// methods from your game controller when selection/move events occur.
/// </summary>
public class hookfollow : MonoBehaviour
{
    [Tooltip("Transform representing the visible hook object that will be moved.")]
    public Transform hook;

    [Tooltip("How quickly the hook follows the target (larger = snappier).")]
    public float followSpeed = 8f;

    [Tooltip("Offset from the top floor world position where the hook should hover.")]
    public Vector3 hookOffset = new Vector3(0f, 2f, 0f);

    private BuildingStack targetStack;
    private Transform currentTopFloor;
    private Coroutine moveCoroutine;

    /// <summary>Begin continuously following the given stack's top floor.</summary>
    public void FollowStack(BuildingStack stack)
    {
        if (stack == null) return;
        targetStack = stack;
        currentTopFloor = FindTopFloorTransform(stack.transform);
    }

    /// <summary>Stop following any stack.</summary>
    public void StopFollow()
    {
        targetStack = null;
        currentTopFloor = null;
    }

    /// <summary>
    /// Smoothly move the hook to the target stack's top floor position (or stack origin if no floors).
    /// If duration is 0 the hook will teleport instantly.
    /// Optional onComplete is invoked when motion finishes.
    /// </summary>
    public void MoveHookToStack(BuildingStack stack, float duration = 0.3f, Action onComplete = null)
    {
        if (hook == null || stack == null)
        {
            onComplete?.Invoke();
            return;
        }

        Transform top = FindTopFloorTransform(stack.transform);
        Vector3 destination = (top != null ? top.position : stack.transform.position) + hookOffset;

        if (moveCoroutine != null)
            StopCoroutine(moveCoroutine);

        moveCoroutine = StartCoroutine(MoveHookCoroutine(destination, duration, onComplete));
    }

    private IEnumerator MoveHookCoroutine(Vector3 destination, float duration, Action onComplete)
    {
        if (hook == null)
        {
            onComplete?.Invoke();
            yield break;
        }

        if (duration <= 0f)
        {
            hook.position = destination;
            onComplete?.Invoke();
            yield break;
        }

        Vector3 start = hook.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // smoothstep interpolation
            t = t * t * (3f - 2f * t);
            hook.position = Vector3.Lerp(start, destination, t);
            yield return null;
        }

        hook.position = destination;
        moveCoroutine = null;
        onComplete?.Invoke();
    }

    private void Update()
    {
        // If following a stack, keep the hook hovering above its current top floor
        if (targetStack == null || hook == null) return;

        // Refresh top floor reference if needed
        if (currentTopFloor == null)
            currentTopFloor = FindTopFloorTransform(targetStack.transform);

        Vector3 desired = (currentTopFloor != null ? currentTopFloor.position : targetStack.transform.position) + hookOffset;

        if (moveCoroutine == null)
        {
            // Only perform follow smoothing when not in the middle of an explicit MoveHook coroutine
            hook.position = Vector3.Lerp(hook.position, desired, Time.deltaTime * followSpeed);
        }
    }

    /// <summary>
    /// Find the child transform of the stack that is highest in world Y — treated as the top floor.
    /// This is robust to BuildingStack's internal private lists because floors are parented under the stack.
    /// </summary>
    private Transform FindTopFloorTransform(Transform stackTransform)
    {
        Transform top = null;
        float maxY = float.MinValue;

        for (int i = 0; i < stackTransform.childCount; i++)
        {
            Transform child = stackTransform.GetChild(i);
            if (!child.gameObject.activeInHierarchy) continue;

            float y = child.position.y;
            if (y > maxY)
            {
                maxY = y;
                top = child;
            }
        }

        return top;
    }
}
