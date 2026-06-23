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

    [Header("UI Respawn / Wrap")]
    [Tooltip("When true, the UI object will wrap around to the opposite side of its parent when it goes off-screen.")]
    public bool respawnUIObject = true;
    [Tooltip("When true, randomizes the perpendicular position on respawn (e.g., Y position for horizontal wrapping) for variety.")]
    public bool randomizeOffsetOnRespawn = true;
    [Header("Custom Spawn Settings")]
    [Tooltip("If true, uses the custom spawn coordinates below instead of parent Rect bounds.")]
    public bool useCustomSpawnBounds = true;
    [Tooltip("The X coordinate to wrap/spawn at.")]
    public float customSpawnX = 1626f;
    [Tooltip("The minimum Y coordinate for randomizing spawning position.")]
    public float minSpawnY = -965f;
    [Tooltip("The maximum Y coordinate for randomizing spawning position.")]
    public float maxSpawnY = -63f;

    [Header("Speed Randomization")]
    [Tooltip("If true, randomizes the UI movement speed within the min/max values below.")]
    public bool randomizeSpeed = true;
    public float minMoveSpeed = 10f;
    public float maxMoveSpeed = 80f;

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

        // Randomize speed on startup if enabled
        if (randomizeSpeed)
        {
            uiMoveSpeed = Random.Range(minMoveSpeed, maxMoveSpeed);
        }

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

        if (respawnUIObject)
        {
            float parentWidth = 0f;
            float parentHeight = 0f;
            float selfWidth = rectTransform.rect.width;
            float selfHeight = rectTransform.rect.height;

            RectTransform parentRect = rectTransform.parent as RectTransform;
            if (parentRect != null)
            {
                parentWidth = parentRect.rect.width;
                parentHeight = parentRect.rect.height;
            }

            // Decide the horizontal wrapping threshold
            float xLimit = useCustomSpawnBounds ? customSpawnX : (parentWidth + selfWidth) * 0.5f;
            float yLimit = (parentHeight + selfHeight) * 0.5f;

            Vector2 pos = rectTransform.anchoredPosition;
            bool didRespawn = false;

            // Wrap X (Horizontal Movement)
            if (uiMoveDirection.x > 0f && pos.x > xLimit)
            {
                pos.x = -xLimit;
                didRespawn = true;
            }
            else if (uiMoveDirection.x < 0f && pos.x < -xLimit)
            {
                pos.x = xLimit;
                didRespawn = true;
            }

            // Wrap Y (Vertical Movement)
            if (uiMoveDirection.y > 0f && pos.y > yLimit)
            {
                pos.y = -yLimit;
                didRespawn = true;
            }
            else if (uiMoveDirection.y < 0f && pos.y < -yLimit)
            {
                pos.y = yLimit;
                didRespawn = true;
            }

            if (didRespawn)
            {
                // Handle perpendicular positioning upon wrap
                if (useCustomSpawnBounds)
                {
                    if (uiMoveDirection.x != 0f)
                    {
                        // Randomize Y in the custom range provided (-63 to -965)
                        pos.y = Random.Range(minSpawnY, maxSpawnY);
                    }
                    else if (uiMoveDirection.y != 0f)
                    {
                        // Randomize X in the custom range (-1626 to 1626)
                        pos.x = Random.Range(-customSpawnX, customSpawnX);
                    }
                }
                else if (randomizeOffsetOnRespawn)
                {
                    if (uiMoveDirection.x != 0f)
                    {
                        pos.y = Random.Range(-parentHeight * 0.35f, parentHeight * 0.35f);
                    }
                    else if (uiMoveDirection.y != 0f)
                    {
                        pos.x = Random.Range(-parentWidth * 0.35f, parentWidth * 0.35f);
                    }
                }

                // Randomize speed on respawn for dynamic variation
                if (randomizeSpeed)
                {
                    uiMoveSpeed = Random.Range(minMoveSpeed, maxMoveSpeed);
                }
            }

            rectTransform.anchoredPosition = pos;
        }
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
