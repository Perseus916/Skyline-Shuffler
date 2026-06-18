using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Creates sparkle, glow, and floating reward VFX for level completion celebrations.
/// All effects are generated via code — no prefab assets required.
/// </summary>
public class CelebrationVFX : MonoBehaviour
{
    private static Material _sparkleMaterial;
    public static Material SparkleMaterial
    {
        get
        {
            if (_sparkleMaterial == null)
            {
                Shader s = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (s == null) s = Shader.Find("Particles/Standard Unlit");
                if (s == null) s = Shader.Find("Sprites/Default");
                _sparkleMaterial = new Material(s);
                _sparkleMaterial.color = Color.white;
            }
            return _sparkleMaterial;
        }
        set { _sparkleMaterial = value; }
    }

    private static Material _starburstMaterial;
    public static Material StarburstMaterial
    {
        get
        {
            if (_starburstMaterial == null)
            {
                Shader s = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (s == null) s = Shader.Find("Particles/Standard Unlit");
                if (s == null) s = Shader.Find("Sprites/Default");
                _starburstMaterial = new Material(s);
                _starburstMaterial.color = Color.white;
            }
            return _starburstMaterial;
        }
        set { _starburstMaterial = value; }
    }

    /// <summary>
    /// Spawn golden sparkle particles rising from a world position.
    /// Great for placing above completed building stacks.
    /// </summary>
    public static ParticleSystem SpawnSparkles(Vector3 position, float duration = 3f, int count = 40)
    {
        GameObject go = new GameObject("Sparkles");
        go.transform.position = position;

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = duration;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
        main.gravityModifier = -0.3f; // Float upward
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = count + 20;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.85f, 0.2f, 1f),  // Gold
            new Color(1f, 1f, 0.6f, 1f)       // Bright yellow
        );

        // Emission — spread over duration
        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = count / duration;

        // Shape — small sphere around the position
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 1.5f;

        // Size over lifetime — pulse and shrink
        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0.5f);
        sizeCurve.AddKey(0.2f, 1f);
        sizeCurve.AddKey(0.5f, 0.8f);
        sizeCurve.AddKey(1f, 0f);
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // Color over lifetime — fade out
        var colorOverLife = ps.colorOverLifetime;
        colorOverLife.enabled = true;
        Gradient fadeGrad = new Gradient();
        fadeGrad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(1f, 0.9f, 0.3f), 0f),
                new GradientColorKey(new Color(1f, 1f, 0.8f), 0.5f),
                new GradientColorKey(new Color(1f, 0.7f, 0.2f), 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.15f),
                new GradientAlphaKey(1f, 0.6f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLife.color = new ParticleSystem.MinMaxGradient(fadeGrad);

        // Noise for twinkle/shimmer
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.8f;
        noise.frequency = 4f;
        noise.scrollSpeed = 1f;

        // Renderer
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            if (SparkleMaterial == _sparkleMaterial)
            {
                if (_sparkleMaterial.shader.name.Contains("Particles/Standard"))
                {
                    _sparkleMaterial.SetFloat("_Mode", 1); // Additive for glow
                }
            }
            renderer.material = SparkleMaterial;
        }

        ps.Play();
        Destroy(go, duration + 2f);

        return ps;
    }

    /// <summary>
    /// Spawn a starburst/flash effect — a quick radial particle explosion.
    /// Perfect for the moment of level completion.
    /// </summary>
    public static ParticleSystem SpawnStarburst(Vector3 position, float scale = 1f)
    {
        GameObject go = new GameObject("Starburst");
        go.transform.position = position;

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 0.1f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(8f * scale, 16f * scale);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f * scale, 0.3f * scale);
        main.gravityModifier = 0.5f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 100;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.95f, 0.5f, 1f),   // Warm gold
            new Color(1f, 1f, 1f, 1f)           // White
        );

        // Emission
        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[]
        {
            new ParticleSystem.Burst(0f, 60)
        });

        // Shape — sphere outward burst
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.3f;

        // Size over lifetime — shrink
        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 1f);
        sizeCurve.AddKey(1f, 0f);
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // Color over lifetime — bright to fade
        var colorOverLife = ps.colorOverLifetime;
        colorOverLife.enabled = true;
        Gradient fadeGrad = new Gradient();
        fadeGrad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(new Color(1f, 0.8f, 0.3f), 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLife.color = new ParticleSystem.MinMaxGradient(fadeGrad);

        // Renderer
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = StarburstMaterial;
        }

        ps.Play();
        Destroy(go, 2f);

        return ps;
    }

    /// <summary>
    /// Animate a building stack with a "celebration bounce" — scale pulse effect.
    /// </summary>
    public static IEnumerator AnimateBuildingBounce(Transform building, float delay = 0f)
    {
        if (building == null) yield break;

        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        Vector3 originalScale = building.localScale;
        float duration = 0.4f;
        float bounceHeight = 1.15f; // Scale up to 115%

        // Scale up
        float elapsed = 0f;
        float halfDuration = duration * 0.4f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            t = t * t * (3f - 2f * t); // Smoothstep
            building.localScale = Vector3.Lerp(originalScale, originalScale * bounceHeight, t);
            yield return null;
        }

        // Scale back with overshoot
        elapsed = 0f;
        float secondHalf = duration * 0.6f;
        Vector3 peakScale = originalScale * bounceHeight;
        while (elapsed < secondHalf)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / secondHalf;
            // Elastic ease-out for satisfying bounce
            float elastic = 1f - Mathf.Pow(2f, -10f * t) * Mathf.Cos(t * Mathf.PI * 2f);
            building.localScale = Vector3.Lerp(peakScale, originalScale, elastic);
            yield return null;
        }

        building.localScale = originalScale;
    }

    /// <summary>
    /// Creates a golden light flash that fades away — like a "reward glow" around buildings.
    /// Uses a point light created via code.
    /// </summary>
    public static void SpawnRewardGlow(Vector3 position, float duration = 2f, float intensity = 3f)
    {
        GameObject go = new GameObject("RewardGlow");
        go.transform.position = position + Vector3.up * 2f;

        Light light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.85f, 0.3f); // Warm gold
        light.intensity = intensity;
        light.range = 10f;

        // Fade coroutine via a helper MonoBehaviour
        var fader = go.AddComponent<LightFader>();
        fader.StartFade(light, duration);

        Destroy(go, duration + 0.5f);
    }
}

/// <summary>
/// Helper to fade a light intensity over time. Attached at runtime.
/// </summary>
public class LightFader : MonoBehaviour
{
    private Light targetLight;
    private float fadeDuration;
    private float startIntensity;

    public void StartFade(Light light, float duration)
    {
        targetLight = light;
        fadeDuration = duration;
        startIntensity = light.intensity;
        StartCoroutine(FadeRoutine());
    }

    private IEnumerator FadeRoutine()
    {
        float elapsed = 0f;

        // Quick ramp up
        float rampTime = 0.2f;
        while (elapsed < rampTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / rampTime;
            targetLight.intensity = Mathf.Lerp(0f, startIntensity, t * t);
            yield return null;
        }

        // Hold briefly
        yield return new WaitForSeconds(0.3f);

        // Fade out
        elapsed = 0f;
        float fadeTime = fadeDuration - rampTime - 0.3f;
        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeTime;
            targetLight.intensity = Mathf.Lerp(startIntensity, 0f, t);
            yield return null;
        }

        targetLight.intensity = 0f;
    }
}
