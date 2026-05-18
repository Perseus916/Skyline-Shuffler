using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class Clouds : MonoBehaviour
{
    [Header("Texture Movement")]
    public bool animateClouds = true;
    public Vector2 moveDirection = Vector2.right;
    public float moveSpeed = 1f;
    public Vector2 textureScale = Vector2.one;

    [Header("Rotation")]
    public bool rotateClouds = false;
    public float rotationSpeed = 10f;

    [Header("Opacity")]
    [Tooltip("Static alpha used when opacity animation is disabled")] 
    public float alpha = 1f;

    [Tooltip("Enable animated opacity between two values")]
    public bool animateOpacity = false;

    [Tooltip("Start alpha for the animation")]
    public float opacityFrom = 1f;

    [Tooltip("End alpha for the animation")]
    public float opacityTo = 0f;

    [Tooltip("Duration of one animation cycle in seconds")] 
    public float opacityDuration = 2f;

    [Tooltip("If true, the animation will loop (repeat). If false, it will play once.)")] 
    public bool opacityLoop = true;

    [Tooltip("When true and looping, the animation will ping-pong between from and to")]
    public bool opacityPingPong = false;

    [Tooltip("Easing curve applied to the animation progress (0..1)")]
    public AnimationCurve opacityCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("Time Base")]
    [Tooltip("When true, animations use unscaled time (ignores timeScale). When false, uses scaled Time.deltaTime.")]
    public bool useUnscaledTime = true;

    [Header("Pulse")]
    public bool pulseScale = false;
    public float pulseSpeed = 1f;
    public float pulseAmount = 0.1f;

    [Header("UI Movement")]
    public bool moveUIObject = false;
    public Vector2 uiMoveDirection = Vector2.zero;
    public float uiMoveSpeed = 1f;

    private RawImage rawImage;
    private RectTransform rectTransform;

    private Vector2 currentOffset;
    private Quaternion startRotation;
    private Vector3 startScale;

    // Opacity animation state
    private float opacityTimer = 0f;
    private bool opacityFinished = false;

    void Awake()
    {
        rawImage = GetComponent<RawImage>();
        rectTransform = GetComponent<RectTransform>();

        startRotation = transform.rotation;
        startScale = transform.localScale;

        rawImage.uvRect = new Rect(0, 0, textureScale.x, textureScale.y);

        // Ensure initial alpha is applied
        ApplyAlphaImmediate(alpha);
    }

    void Update()
    {
        HandleTextureMovement();
        HandleRotation();
        HandleOpacity();
        HandlePulse();
        HandleUIMovement();
    }

    void HandleTextureMovement()
    {
        if (!animateClouds)
            return;

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        currentOffset += moveDirection.normalized * moveSpeed * dt;

        rawImage.uvRect = new Rect(
            currentOffset.x,
            currentOffset.y,
            textureScale.x,
            textureScale.y
        );
    }

    void HandleRotation()
    {
        if (!rotateClouds)
            return;

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        transform.Rotate(Vector3.forward * rotationSpeed * dt);
    }

    void HandleOpacity()
    {
        if (!animateOpacity)
        {
            // static alpha
            Color c0 = rawImage.color;
            c0.a = alpha;
            rawImage.color = c0;
            return;
        }

        if (opacityDuration <= 0f)
        {
            // immediate set
            Color cImmediate = rawImage.color;
            cImmediate.a = opacityTo;
            rawImage.color = cImmediate;
            return;
        }

        if (opacityFinished && !opacityLoop)
            return;

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        opacityTimer += dt;

        float t;
        if (opacityLoop)
        {
            if (opacityPingPong)
            {
                // PingPong between 0 and duration, normalized
                float ping = Mathf.PingPong(opacityTimer, opacityDuration) / opacityDuration;
                t = Mathf.Clamp01(ping);
            }
            else
            {
                t = (opacityTimer % opacityDuration) / opacityDuration;
            }
        }
        else
        {
            t = Mathf.Clamp01(opacityTimer / opacityDuration);
            if (opacityTimer >= opacityDuration)
                opacityFinished = true;
        }

        float curveVal = opacityCurve.Evaluate(t);
        float a = Mathf.Lerp(opacityFrom, opacityTo, curveVal);

        Color c = rawImage.color;
        c.a = a;
        rawImage.color = c;
    }

    void HandlePulse()
    {
        if (!pulseScale)
            return;

        float time = useUnscaledTime ? Time.unscaledTime : Time.time;
        float scale = 1f + Mathf.Sin(time * pulseSpeed) * pulseAmount;
        transform.localScale = startScale * scale;
    }

    void HandleUIMovement()
    {
        if (!moveUIObject)
            return;

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        rectTransform.anchoredPosition += uiMoveDirection.normalized * uiMoveSpeed * dt;
    }

    // Public helper to (re)start the opacity animation from the beginning
    public void StartOpacityAnimation()
    {
        opacityTimer = 0f;
        opacityFinished = false;
    }

    // Immediately apply an alpha value to the RawImage color
    public void ApplyAlphaImmediate(float a)
    {
        Color c = rawImage.color;
        c.a = Mathf.Clamp01(a);
        rawImage.color = c;
    }
}
