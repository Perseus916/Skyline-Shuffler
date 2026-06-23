using UnityEngine;

/// <summary>
/// CurrentLevelButtonAnimation
/// Attached to the current level button to make it stand out with a pulse (zoom in / zoom out) animation.
/// </summary>
public class CurrentLevelButtonAnimation : MonoBehaviour
{
    [Header("Pulse Settings")]
    public float pulseSpeed = 3f;
    public float pulseAmount = 0.12f;

    private Vector3 originalScale;

    private void Start()
    {
        originalScale = transform.localScale;
    }

    private void Update()
    {
        // Gentle scale pulse (zoom in / zoom out)
        float scaleOffset = Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
        transform.localScale = originalScale * (1f + scaleOffset);
    }

    private void OnDisable()
    {
        // Reset scale to original when disabled/regenerated
        transform.localScale = originalScale;
    }
}
