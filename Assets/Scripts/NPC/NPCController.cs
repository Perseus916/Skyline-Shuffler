using System.Collections.Generic;
using UnityEngine;

public class NPCController : MonoBehaviour
{
    [SerializeField] float moveSpeed = 2f;
    [SerializeField] float rotationSpeed = 5f;

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

    // Enter-building settings
    [SerializeField] float enterApproachSpeedMultiplier = 1.5f;
    [SerializeField] float enterReachDistance = 0.35f;
    [SerializeField] float vanishDelay = 0.6f; // time to scale down before destroy

    private Waypoint currentWaypoint;
    private Waypoint targetWaypoint;
    private Waypoint previousWaypoint;
    private Animator animator;

    // Track all NPC instances for simple local avoidance
    private static readonly List<NPCController> allNPCs = new List<NPCController>();
    public static List<NPCController> AllNPCs => allNPCs;

    // Collider for penetration checks
    private Collider myCollider;

    // Stuck tracking state
    private Vector3 lastPosition;
    private float stuckTimer = 0f;
    private float reverseCooldownTimer = 0f;

    // Stuck recovery candidates: when stuck, try closest connected waypoints in order
    // NPC should NOT go back to previous waypoint when stuck; instead try closest then next closest.
    private List<Waypoint> stuckCandidates = null;
    private int stuckCandidateIndex = 0;
    private Waypoint stuckCandidateOrigin = null; // currentWaypoint when candidates were built
    private int stuckCandidateStuckCount = 0; // how many stuck detections while targeting current candidate

    // Enter-building state
    private bool isEnteringBuilding = false;
    public bool IsEnteringBuilding => isEnteringBuilding;
    private Vector3 enterTarget;

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
        currentWaypoint = startWaypoint;
        // Snap to ground at start
        Vector3 pos = startWaypoint.transform.position;
        float groundY = SampleGroundHeight(pos);
        transform.position = new Vector3(pos.x, groundY, pos.z);

        lastPosition = transform.position;
        stuckTimer = 0f;
        reverseCooldownTimer = 0f;

        // reset stuck candidate state
        stuckCandidates = null;
        stuckCandidateIndex = 0;
        stuckCandidateOrigin = null;
        stuckCandidateStuckCount = 0;

        ChooseNextWaypoint();
    }

    void Update()
    {
        if (isEnteringBuilding)
        {
            MoveToEnterTarget();
            return;
        }

        // If target is inactive for any reason, pick another
        if (targetWaypoint != null && !targetWaypoint.IsActive())
        {
            ChooseNextWaypoint();
        }

        if (targetWaypoint == null)
            return;

        // Ensure the target waypoint is actually directly connected to the current waypoint.
        // If it's not, pick the nearest connected waypoint instead. This prevents "skipping"
        // to farther waypoints that may be connected in the graph but not intended as the next step.
        if (currentWaypoint != null && targetWaypoint != null && !currentWaypoint.connectedWaypoints.Contains(targetWaypoint))
        {
            Waypoint nearest = null;
            float bestDist = float.MaxValue;
            foreach (var w in currentWaypoint.connectedWaypoints)
            {
                if (w == null) continue;
                float d = Vector3.SqrMagnitude(w.transform.position - transform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    nearest = w;
                }
            }

            if (nearest != null)
            {
                targetWaypoint = nearest;
            }
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
            HandleStuckReverse();
            stuckTimer = 0f;
            reverseCooldownTimer = stuckReverseCooldown;
        }

        lastPosition = transform.position;
    }

    /// <summary>
    /// Called by NPCManager (or other game systems) to send this NPC to a building entry point and disappear.
    /// </summary>
    public void StartEnterBuilding(Vector3 entryPosition)
    {
        isEnteringBuilding = true;
        enterTarget = entryPosition;

        // Stop regular waypoint movement
        targetWaypoint = null;

        // Optionally trigger enter animation
        if (animator != null)
        {
            animator.SetFloat("Speed", 0f);
            if (animator.HasState(0, Animator.StringToHash("Enter")))
            {
                animator.SetTrigger("Enter");
            }
        }

        // Disable collider so NPC won't block others while entering
        if (myCollider != null)
        {
            myCollider.enabled = false;
        }
    }

    void MoveToEnterTarget()
    {
        // Move directly toward enterTarget on XZ plane
        Vector3 flatTarget = new Vector3(enterTarget.x, 0f, enterTarget.z);
        Vector3 flatPos = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 dir = (flatTarget - flatPos);
        float dist = dir.magnitude;
        if (dist <= enterReachDistance)
        {
            // Reached entry; vanish
            StartCoroutine(VanishAndDestroy());
            return;
        }

        Vector3 moveDir = dir.normalized;
        float speed = moveSpeed * enterApproachSpeedMultiplier;

        Vector3 tentativeXZ = flatPos + moveDir * speed * Time.deltaTime;
        Vector3 tentativeWorld = new Vector3(tentativeXZ.x, transform.position.y, tentativeXZ.z);

        // Keep on ground if possible
        float groundY = SampleGroundHeight(tentativeWorld);
        transform.position = new Vector3(tentativeXZ.x, groundY, tentativeXZ.z);

        if (moveDir != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(new Vector3(moveDir.x, 0f, moveDir.z));
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }

    System.Collections.IEnumerator VanishAndDestroy()
    {
        // Small delay to allow enter animation or effects
        float elapsed = 0f;
        Vector3 startScale = transform.localScale;
        while (elapsed < vanishDelay)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / vanishDelay);
            float s = Mathf.Lerp(1f, 0f, t);
            transform.localScale = startScale * s;
            yield return null;
        }

        // Finally destroy the NPC
        Destroy(this.gameObject);
        yield break;
    }

    void HandleStuckReverse()
    {
        // New behavior per request:
        // - Do NOT go back to previousWaypoint when stuck.
        // - Instead build a list of connected waypoints sorted by XZ distance from the NPC's current position.
        // - On first stuck choose the closest. If stuck again while still trying to reach that candidate,
        //   advance to the next-closest candidate. Continue until a waypoint is reached.

        if (currentWaypoint == null) return;

        // If origin changed or we don't have candidates yet, build them
        if (stuckCandidateOrigin != currentWaypoint || stuckCandidates == null || stuckCandidates.Count == 0)
        {
            stuckCandidates = new List<Waypoint>(currentWaypoint.connectedWaypoints);
            stuckCandidates.RemoveAll(w => w == null);

            // Sort by XZ distance to this NPC
            Vector3 pos = transform.position;
            stuckCandidates.Sort((a, b) =>
            {
                float da = (new Vector3(a.transform.position.x, 0f, a.transform.position.z) - new Vector3(pos.x, 0f, pos.z)).sqrMagnitude;
                float db = (new Vector3(b.transform.position.x, 0f, b.transform.position.z) - new Vector3(pos.x, 0f, pos.z)).sqrMagnitude;
                return da.CompareTo(db);
            });

            stuckCandidateIndex = 0;
            stuckCandidateStuckCount = 1; // first stuck attempt for this candidate
            stuckCandidateOrigin = currentWaypoint;
        }
        else
        {
            // Still targeting candidates from same origin: increment stuck count and advance candidate after 2 attempts
            stuckCandidateStuckCount++;
            if (stuckCandidateStuckCount >= 2)
            {
                // advance to next candidate if available
                stuckCandidateIndex = Mathf.Min(stuckCandidateIndex + 1, stuckCandidates.Count - 1);
                stuckCandidateStuckCount = 1;
            }
        }

        if (stuckCandidates == null || stuckCandidates.Count == 0) return;

        Waypoint chosen = stuckCandidates[stuckCandidateIndex];
        if (chosen != null)
        {
            // Set the chosen waypoint as the new target. Do not choose previousWaypoint.
            previousWaypoint = currentWaypoint; // keep history but do not use it as target
            targetWaypoint = chosen;
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
        Vector3 targetPos = targetWaypoint.transform.position;

        // Compute desired direction only in XZ plane to avoid moving vertically
        Vector3 flatTarget = new Vector3(targetPos.x, 0f, targetPos.z);
        Vector3 flatPos = new Vector3(transform.position.x, 0f, transform.position.z);

        Vector3 desiredDir = (flatTarget - flatPos).normalized;

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

        if (hasGround)
        {
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
        }
        else
        {
            // No ground ahead: don't move forward. Try to pick another waypoint to avoid falling
            ChooseNextWaypoint();
        }

        float distance = Vector3.Distance(new Vector3(transform.position.x, 0f, transform.position.z), new Vector3(targetPos.x, 0f, targetPos.z));

        if (distance < 0.2f)
        {
            currentWaypoint = targetWaypoint;

            // Reached a waypoint: reset stuck candidate tracking
            stuckCandidates = null;
            stuckCandidateIndex = 0;
            stuckCandidateOrigin = null;
            stuckCandidateStuckCount = 0;

            ChooseNextWaypoint();
        }

        if (animator != null)
        {
            // Pass actual speed to animator (magnitude of velocity-ish)
            animator.SetFloat("Speed", moveSpeed);
        }
    }

    void ChooseNextWaypoint()
    {
        // Reset stuck candidate tracking whenever a regular next waypoint is chosen
        stuckCandidates = null;
        stuckCandidateIndex = 0;
        stuckCandidateOrigin = null;
        stuckCandidateStuckCount = 0;

        List<Waypoint> options =
            new List<Waypoint>(currentWaypoint.connectedWaypoints);

        // Filter to only active waypoints
        List<Waypoint> activeOptions = options.FindAll(w => w != null && w.IsActive());

        // Remove previous from active options if present
        if (previousWaypoint != null)
        {
            activeOptions.Remove(previousWaypoint);
        }

        // Allow a random chance to go back to the previous waypoint
        if (previousWaypoint != null && Random.value < reverseChance && previousWaypoint.IsActive())
        {
            targetWaypoint = previousWaypoint;
            previousWaypoint = currentWaypoint;
            return;
        }

        // If no active options, fall back to any connected waypoint (including inactive)
        if (activeOptions.Count == 0)
        {
            options = new List<Waypoint>(currentWaypoint.connectedWaypoints);

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
            return;
        }

        previousWaypoint = currentWaypoint;

        targetWaypoint =
            activeOptions[Random.Range(0, activeOptions.Count)];
    }
}