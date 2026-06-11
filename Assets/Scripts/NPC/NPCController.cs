using System.Collections.Generic;
using UnityEngine;

public class NPCController : MonoBehaviour
{
    [SerializeField] float moveSpeed = 2f;
    [SerializeField] float rotationSpeed = 5f;

    // Debugging
    [SerializeField] bool debugLogs = true;

    // Separation settings
    [SerializeField] float separationRadius = 1f;
    [SerializeField] float separationStrength = 2f;
    [SerializeField] float minSeparationDistance = 0.5f;

    // Chance to reverse to previous waypoint when choosing next
    [Range(0f, 1f)]
    [SerializeField] float reverseChance = 0.15f;

    // Ground checking
    [SerializeField] LayerMask groundMask = ~0; // default: everything
    [SerializeField] float raycastHeight = 2f;
    [SerializeField] float raycastDistance = 5f;

    // Stuck detection
    [SerializeField] float stuckTimeThreshold = 2f; // seconds before considered stuck
    [SerializeField] float stuckDistanceThreshold = 0.05f; // movement threshold per check
    [SerializeField] float stuckReverseCooldown = 3f; // seconds to wait after reversing

    private Waypoint currentWaypoint;
    private Waypoint targetWaypoint;
    private Waypoint previousWaypoint;
    private Animator animator;

    // Track all NPC instances for simple local avoidance
    private static readonly List<NPCController> allNPCs = new List<NPCController>();

    // Collider for penetration checks
    private Collider myCollider;

    // Stuck tracking state
    private Vector3 lastPosition;
    private float stuckTimer = 0f;
    private float reverseCooldownTimer = 0f;

    // One-time warnings to avoid log spam
    private bool warnedNoTarget = false;
    private bool warnedNoGround = false;
    private bool warnedNoWaypoint = false;
    private bool warnedAnimatorParam = false;

    void Awake()
    {
        allNPCs.Add(this);
        myCollider = GetComponent<Collider>();
        lastPosition = transform.position;
    }

    void OnDestroy()
    {
        allNPCs.Remove(this);
    }

    void Start()
    {
        animator = GetComponent<Animator>();
    }

    public void Initialize(Waypoint startWaypoint)
    {
        if (startWaypoint == null)
        {
            if (debugLogs) Debug.LogError("NPC.Initialize called with null startWaypoint on " + name);
            return;
        }

        currentWaypoint = startWaypoint;
        // Snap to ground at start
        Vector3 pos = startWaypoint.transform.position;
        float groundY = SampleGroundHeight(pos);
        transform.position = new Vector3(pos.x, groundY, pos.z);

        lastPosition = transform.position;
        stuckTimer = 0f;
        reverseCooldownTimer = 0f;

        if (debugLogs) Debug.Log(name + " initialized at waypoint " + startWaypoint.name + " pos=" + transform.position);

        ChooseNextWaypoint();

        if (targetWaypoint == null && debugLogs)
        {
            Debug.LogWarning(name + " has no targetWaypoint after Initialize");
        }
    }

    void Update()
    {
        if (targetWaypoint == null)
        {
            if (debugLogs && !warnedNoTarget)
            {
                Debug.LogWarning(name + " targetWaypoint is null. NPC will not move.");
                warnedNoTarget = true;
            }
            return;
        }

        // Update cooldown timer
        if (reverseCooldownTimer > 0f)
            reverseCooldownTimer -= Time.deltaTime;

        MoveToWaypoint();

        // Stuck detection: check movement since last frame
        float moved = Vector3.Distance(new Vector3(transform.position.x, 0f, transform.position.z), new Vector3(lastPosition.x, 0f, lastPosition.z));
        if (moved < stuckDistanceThreshold)
        {
            stuckTimer += Time.deltaTime;
        }
        else
        {
            stuckTimer = 0f;
        }

        // If stuck longer than threshold and cooldown allows, reverse direction
        if (stuckTimer >= stuckTimeThreshold && reverseCooldownTimer <= 0f)
        {
            if (debugLogs) Debug.Log(name + " appears stuck. Attempting reverse.");
            HandleStuckReverse();
            stuckTimer = 0f;
            reverseCooldownTimer = stuckReverseCooldown;
        }

        lastPosition = transform.position;
    }

    void HandleStuckReverse()
    {
        // Try to reverse to previous waypoint if available
        if (previousWaypoint != null)
        {
            // swap current and target so NPC goes back
            Waypoint oldTarget = targetWaypoint;
            targetWaypoint = previousWaypoint;
            previousWaypoint = currentWaypoint;
            currentWaypoint = oldTarget; // set current to old target so path logic stays consistent

            if (debugLogs) Debug.Log(name + " reversed to previous waypoint: " + targetWaypoint.name);
        }
        else
        {
            // Fallback: pick a random different connected waypoint
            if (currentWaypoint == null)
            {
                if (debugLogs && !warnedNoWaypoint) Debug.LogWarning(name + " cannot reverse because currentWaypoint is null");
                warnedNoWaypoint = true;
                return;
            }

            List<Waypoint> options = new List<Waypoint>(currentWaypoint.connectedWaypoints);
            if (options.Count > 1)
            {
                options.Remove(targetWaypoint);
                targetWaypoint = options[Random.Range(0, options.Count)];
                if (debugLogs) Debug.Log(name + " chose fallback waypoint: " + targetWaypoint.name);
            }
            else if (debugLogs && !warnedNoWaypoint)
            {
                Debug.LogWarning(name + " has no alternate connected waypoints to reverse to.");
                warnedNoWaypoint = true;
            }
        }
    }

    float SampleGroundHeight(Vector3 samplePosition)
    {
        Vector3 origin = samplePosition + Vector3.up * raycastHeight;
        RaycastHit hit;
        if (Physics.Raycast(origin, Vector3.down, out hit, raycastDistance + raycastHeight, groundMask))
        {
            return hit.point.y;
        }

        // If no ground found, keep current y
        return transform.position.y;
    }

    void MoveToWaypoint()
    {
        if (targetWaypoint == null)
        {
            if (debugLogs && !warnedNoTarget) Debug.LogWarning(name + " MoveToWaypoint called but targetWaypoint is null");
            warnedNoTarget = true;
            return;
        }

        Vector3 targetPos = targetWaypoint.transform.position;

        // Compute desired direction only in XZ plane to avoid moving vertically
        Vector3 flatTarget = new Vector3(targetPos.x, 0f, targetPos.z);
        Vector3 flatPos = new Vector3(transform.position.x, 0f, transform.position.z);

        Vector3 desiredDir = (flatTarget - flatPos).normalized;

        if (desiredDir.sqrMagnitude < 0.0001f)
        {
            if (debugLogs) Debug.Log(name + " desiredDir is zero - already at target XZ or degenerate direction");
            // Still call ChooseNextWaypoint to avoid stalling on same target
            ChooseNextWaypoint();
            return;
        }

        // Compute simple separation from other NPCs (XZ only)
        Vector3 separation = Vector3.zero;
        foreach (var other in allNPCs)
        {
            if (other == this) continue;

            Vector3 toThis = transform.position - other.transform.position;
            Vector3 toThisFlat = new Vector3(toThis.x, 0f, toThis.z);
            float dist = toThisFlat.magnitude;
            if (dist > 0f && dist < separationRadius)
            {
                separation += toThisFlat.normalized * (separationRadius - dist) / separationRadius;

                // If overlapping too much, push out immediately (XZ only)
                if (dist < minSeparationDistance)
                {
                    float push = (minSeparationDistance - dist);
                    Vector3 correction = toThisFlat.normalized * push * 0.5f; // small immediate correction
                    Vector3 tentativePos = transform.position + correction;

                    // Ensure correction keeps NPC on ground
                    float groundY = SampleGroundHeight(tentativePos);
                    transform.position = new Vector3(tentativePos.x, groundY, tentativePos.z);
                }
            }
        }

        Vector3 moveDir = (desiredDir + separation * separationStrength).normalized;

        if (moveDir.sqrMagnitude < 0.0001f)
        {
            if (debugLogs) Debug.Log(name + " moveDir is zero after separation adjustments - not moving this frame");
            return;
        }

        // Compute tentative new position (XZ) and sample ground there
        Vector3 tentativeXZ = new Vector3(transform.position.x, 0f, transform.position.z) + moveDir * moveSpeed * Time.deltaTime;
        Vector3 tentativeWorld = new Vector3(tentativeXZ.x, transform.position.y, tentativeXZ.z);

        // Check if ground exists under tentative position
        float groundAtTentative = SampleGroundHeight(tentativeWorld);
        bool hasGround = false;
        Vector3 origin = tentativeWorld + Vector3.up * raycastHeight;
        RaycastHit tmpHit;
        if (Physics.Raycast(origin, Vector3.down, out tmpHit, raycastDistance + raycastHeight, groundMask))
        {
            hasGround = true;
            groundAtTentative = tmpHit.point.y;
        }

        if (!hasGround)
        {
            if (debugLogs && !warnedNoGround)
            {
                Debug.LogWarning(name + " no ground detected ahead at tentative position " + tentativeWorld + ". Choosing new waypoint to avoid falling.");
                warnedNoGround = true;
            }
            ChooseNextWaypoint();
            return;
        }

        // Apply position with ground Y
        transform.position = new Vector3(tentativeXZ.x, groundAtTentative, tentativeXZ.z);

        // After moving, resolve any penetrations into other colliders
        if (myCollider != null)
        {
            // Use a conservative overlap radius based on collider bounds
            float overlapRadius = myCollider.bounds.extents.magnitude;
            Collider[] overlaps = Physics.OverlapSphere(transform.position, overlapRadius, ~0, QueryTriggerInteraction.Ignore);
            foreach (var other in overlaps)
            {
                if (other == myCollider) continue;

                Vector3 direction;
                float penetrationDistance;
                // Compute minimal translation to separate this collider and the other
                if (Physics.ComputePenetration(myCollider, transform.position, transform.rotation,
                                               other, other.transform.position, other.transform.rotation,
                                               out direction, out penetrationDistance))
                {
                    // Move out along the separation direction
                    transform.position += direction * penetrationDistance;

                    // Resample ground after correction and clamp to it
                    float newGroundY = SampleGroundHeight(transform.position);
                    transform.position = new Vector3(transform.position.x, newGroundY, transform.position.z);
                }
            }
        }

        if (moveDir != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(new Vector3(moveDir.x, 0f, moveDir.z));

            transform.rotation =
                Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    rotationSpeed * Time.deltaTime);
        }

        float distance = Vector3.Distance(new Vector3(transform.position.x, 0f, transform.position.z), new Vector3(targetPos.x, 0f, targetPos.z));

        if (distance < 0.2f)
        {
            currentWaypoint = targetWaypoint;
            ChooseNextWaypoint();
        }

        if (animator != null)
        {
            // compute approximate forward speed (actual displacement per second)
            float animSpeed = moveDir.magnitude * moveSpeed;
            if (AnimatorHasParameter("Speed"))
                animator.SetFloat("Speed", animSpeed);
            else if (debugLogs && !warnedAnimatorParam)
            {
                Debug.LogWarning(name + " Animator does not have 'Speed' parameter.");
                warnedAnimatorParam = true;
            }
        }
    }

    // Utility to check if the Animator has a parameter with the given name.
    bool AnimatorHasParameter(string name)
    {
        if (animator == null) return false;
        foreach (var p in animator.parameters)
            if (p.name == name) return true;
        return false;
    }

    void ChooseNextWaypoint()
    {
        if (currentWaypoint == null)
        {
            if (debugLogs && !warnedNoWaypoint) Debug.LogWarning(name + " currentWaypoint is null in ChooseNextWaypoint");
            warnedNoWaypoint = true;
            return;
        }

        List<Waypoint> options =
            new List<Waypoint>(currentWaypoint.connectedWaypoints);

        // Allow a random chance to go back to the previous waypoint
        if (previousWaypoint != null && Random.value < reverseChance)
        {
            targetWaypoint = previousWaypoint;
            previousWaypoint = currentWaypoint;
            return;
        }

        if (previousWaypoint != null)
        {
            options.Remove(previousWaypoint);
        }

        if (options.Count == 0)
        {
            options = new List<Waypoint>(
                currentWaypoint.connectedWaypoints);
        }

        previousWaypoint = currentWaypoint;

        targetWaypoint =
            options[Random.Range(0, options.Count)];

        if (debugLogs) Debug.Log(name + " chose next waypoint: " + targetWaypoint.name);
    }
}