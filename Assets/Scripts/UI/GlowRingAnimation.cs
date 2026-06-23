using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// GlowRingAnimation
/// Handles the expanding ripple/sonar effect on a UI Image that fades out and repeats.
/// </summary>
[RequireComponent(typeof(Image))]
public class GlowRingAnimation : MonoBehaviour
{
    [Tooltip("Speed of the pulse ripple animation.")]
    public float pulseSpeed = 1.5f;

    [Tooltip("Maximum scale multiplier the ring reaches before fading out.")]
    public float maxScaleMultiplier = 1.8f;

    [Tooltip("Starting alpha transparency of the glow ring.")]
    [Range(0f, 1f)]
    public float startAlpha = 0.8f;

    private Image ringImage;
    private Vector3 originalScale;
    private Color baseColor;

    private void Start()
    {
        ringImage = GetComponent<Image>();
        baseColor = ringImage.color;
        originalScale = transform.localScale;
    }

    private void Update()
    {
        if (ringImage == null) return;

        // Ripple phase goes from 0.0 to 1.0 continuously over time
        float t = (Time.time * pulseSpeed) % 1f;

        // Scale goes from originalScale to originalScale * maxScaleMultiplier
        float currentScaleMultiplier = Mathf.Lerp(1.0f, maxScaleMultiplier, t);
        transform.localScale = originalScale * currentScaleMultiplier;

        // Alpha fades out linearly as scale increases
        float currentAlpha = Mathf.Lerp(startAlpha, 0f, t);
        ringImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, currentAlpha);
    }
}
