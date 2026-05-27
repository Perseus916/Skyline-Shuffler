using UnityEngine;

/// <summary>
/// Creates and manages a confetti particle burst entirely via code.
/// No prefab assets required — configures ParticleSystem at runtime.
/// Attach to any GameObject or call ConfettiController.SpawnBurst() statically.
/// </summary>
public class ConfettiController : MonoBehaviour
{
    [Header("Burst Settings")]
    [SerializeField] private int burstCount = 250;
    [SerializeField] private float lifetime = 4f;
    [SerializeField] private float spreadRadius = 8f;
    [SerializeField] private float launchSpeed = 12f;
    [SerializeField] private float gravity = 3f;

    [Header("Visuals")]
    [SerializeField] private float minSize = 0.15f;
    [SerializeField] private float maxSize = 0.35f;

    private ParticleSystem ps;

    /// <summary>
    /// Creates a confetti burst at the given world position and returns the controller.
    /// Auto-destroys after particles finish.
    /// </summary>
    public static ConfettiController SpawnBurst(Vector3 position, int count = 250)
    {
        GameObject go = new GameObject("ConfettiBurst");
        go.transform.position = position;
        var ctrl = go.AddComponent<ConfettiController>();
        ctrl.burstCount = count;
        ctrl.Initialize();
        ctrl.Fire();
        return ctrl;
    }

    public void Initialize()
    {
        if (ps != null) return;

        ps = gameObject.AddComponent<ParticleSystem>();

        // Stop the auto-play so we can configure first
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 0.1f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.6f, lifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(launchSpeed * 0.4f, launchSpeed);
        main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = burstCount + 50;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

        // Multi-color gradient for start color
        main.startColor = new ParticleSystem.MinMaxGradient(
            BuildConfettiGradient()
        );

        // Emission — single burst
        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[]
        {
            new ParticleSystem.Burst(0f, burstCount)
        });

        // Shape — hemisphere shooting upwards
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 45f;
        shape.radius = spreadRadius * 0.3f;
        shape.radiusThickness = 1f;

        // Rotation over lifetime (spinning confetti)
        var rotOverLife = ps.rotationOverLifetime;
        rotOverLife.enabled = true;
        rotOverLife.z = new ParticleSystem.MinMaxCurve(-300f * Mathf.Deg2Rad, 300f * Mathf.Deg2Rad);

        // Size over lifetime — slight shrink at end
        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 1f);
        sizeCurve.AddKey(0.7f, 1f);
        sizeCurve.AddKey(1f, 0.3f);
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // Color over lifetime — fade out at end
        var colorOverLife = ps.colorOverLifetime;
        colorOverLife.enabled = true;
        Gradient fadeGrad = new Gradient();
        fadeGrad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 0.8f),
                new GradientColorKey(Color.white, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 0.75f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLife.color = new ParticleSystem.MinMaxGradient(fadeGrad);

        // Velocity over lifetime — slight horizontal drift (wind)
        var velOverLife = ps.velocityOverLifetime;
        velOverLife.enabled = true;
        velOverLife.x = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
        velOverLife.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);

        // Noise for natural flutter
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 1.5f;
        noise.frequency = 2f;
        noise.scrollSpeed = 0.5f;
        noise.octaveCount = 2;

        // Use default particle material (white sprite)
        var renderer = GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            // Use the built-in default particle shader
            renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
            renderer.material.SetFloat("_Mode", 0); // Additive-ish
            renderer.material.color = Color.white;
        }
    }

    public void Fire()
    {
        if (ps == null) Initialize();
        ps.Play();

        // Auto-destroy after particles finish
        Destroy(gameObject, lifetime + 1f);
    }

    /// <summary>
    /// Build a vibrant multi-color gradient for confetti.
    /// </summary>
    private Gradient BuildConfettiGradient()
    {
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(1f, 0.2f, 0.3f), 0f),     // Red/Pink
                new GradientColorKey(new Color(1f, 0.85f, 0.1f), 0.2f),  // Gold
                new GradientColorKey(new Color(0.2f, 0.9f, 0.4f), 0.4f), // Green
                new GradientColorKey(new Color(0.3f, 0.6f, 1f), 0.6f),   // Blue
                new GradientColorKey(new Color(0.8f, 0.3f, 1f), 0.8f),   // Purple
                new GradientColorKey(new Color(1f, 0.5f, 0.2f), 1f)      // Orange
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            }
        );
        return grad;
    }
}
