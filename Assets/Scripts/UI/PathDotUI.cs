using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PathDotUI
/// Attached to path dot instances to handle wave animations and effects.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class PathDotUI : MonoBehaviour
{
    private bool enableAnimation = false;
    private float waveSpeed = 4f;
    private float waveAmount = 0.12f;
    private float timeDelay = 0f;
    private Vector3 baseScale = Vector3.one;

    /// <summary>
    /// Configures the animation settings for this path dot.
    /// </summary>
    public void SetupAnimation(bool enable, float speed, float amount, float delay, Vector3 scale)
    {
        enableAnimation = enable;
        waveSpeed = speed;
        waveAmount = amount;
        timeDelay = delay;
        baseScale = scale;
        transform.localScale = baseScale;
    }

    private void Update()
    {
        if (!enableAnimation) return;

        // Apply a smooth cosine wave scaling offset based on time and position delay
        float scaleMultiplier = 1f + Mathf.Cos(Time.time * waveSpeed - timeDelay) * waveAmount;
        transform.localScale = baseScale * scaleMultiplier;
    }
}
