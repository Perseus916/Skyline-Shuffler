using System.Collections.Generic;
using System.Collections;
using UnityEngine;

public class NPCManager : MonoBehaviour
{
    public static NPCManager Instance { get; private set; }

    [SerializeField]
    List<NPCController> npcPrefabs = new List<NPCController>();

    [SerializeField]
    List<Waypoint> spawnWaypoints =
        new List<Waypoint>();

    [SerializeField]
    int npcCount = 20;
    [SerializeField]
    int npcinbuilding = 5;

    // Public read-only accessor for other systems to use as "how many NPCs to send"
    public int NpcCount => npcinbuilding;

    // When true, NPCManager will auto-spawn in Start(). Set false to control spawning externally.
    [SerializeField] private bool spawnOnStart = false;

    // When true, spawn repeatedly at intervals instead of a single burst on start.
    [SerializeField] private bool spawnWithInterval = false;

    // How many seconds between spawn batches when spawnWithInterval is true.
    [SerializeField] private float spawnInterval = 5f;

    // How many NPCs to spawn at once each interval.
    [SerializeField] private int spawnBatchSize = 1;

    [Header("Staggered Spawning")]
    [Tooltip("Delay between each individual NPC spawn during a batch (avoids frame hitches).")]
    [SerializeField] private float staggerDelay = 0.05f;

    [Tooltip("Random XZ offset from spawn waypoint position so NPCs don't stack.")]
    [SerializeField] private float spawnPositionJitter = 0.5f;

    [Header("Celebration Broadcast")]
    [Tooltip("Radius within which nearby NPCs react to a completed building.")]
    [SerializeField] private float celebrationRadius = 15f;

    [Header("Enter Building Stagger")]
    [Tooltip("Delay between each NPC starting to enter a building.")]
    [SerializeField] private float enterStaggerDelay = 0.3f;

    private Coroutine spawnCoroutine;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple NPCManager instances detected. Destroying duplicate.");
            Destroy(this.gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        if (!spawnOnStart) return;

        if (spawnWithInterval)
            StartSpawning();
        else
            SpawnNPCs();
    }

    // Make spawning callable from other systems (LevelLoader will call this when the level is generated)
    public void SpawnNPCs()
    {
        if (npcPrefabs == null || npcPrefabs.Count == 0)
        {
            Debug.LogWarning("NPCManager.SpawnNPCs: No NPC prefabs assigned. Aborting spawn.");
            return;
        }

        if (spawnWaypoints == null || spawnWaypoints.Count == 0)
        {
            Debug.LogWarning("NPCManager.SpawnNPCs: No spawn waypoints assigned. Aborting spawn.");
            return;
        }

        // Count existing alive NPCs tracked by NPCController
        int existing = 0;
        var all = NPCController.AllNPCs;
        if (all != null)
        {
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null) existing++;
            }
        }

        int toSpawn = Mathf.Max(0, npcCount - existing);
        if (toSpawn == 0)
        {
            // Desired count already met or exceeded; do nothing.
            return;
        }

        // Spawn with stagger to avoid frame hitches
        StartCoroutine(SpawnEvenlyStaggered(toSpawn));
    }

    /// <summary>
    /// Start spawning NPCs repeatedly using the configured interval and batch size.
    /// If already spawning, this does nothing.
    /// </summary>
    public void StartSpawning()
    {
        if (spawnCoroutine != null) return;
        spawnCoroutine = StartCoroutine(SpawnIntervalCoroutine());
    }

    /// <summary>
    /// Stop the interval spawning if it is active.
    /// </summary>
    public void StopSpawning()
    {
        if (spawnCoroutine == null) return;
        StopCoroutine(spawnCoroutine);
        spawnCoroutine = null;
    }

    private IEnumerator SpawnIntervalCoroutine()
    {
        // Basic validation
        if (npcPrefabs == null || npcPrefabs.Count == 0 || spawnWaypoints == null || spawnWaypoints.Count == 0)
        {
            yield break;
        }

        while (true)
        {
            // Count existing alive NPCs
            int existing = 0;
            var all = NPCController.AllNPCs;
            if (all != null)
            {
                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i] != null) existing++;
                }
            }

            int remaining = Mathf.Max(0, npcCount - existing);
            if (remaining > 0)
            {
                int batch = Mathf.Clamp(spawnBatchSize, 1, remaining);

                // Spawn the batch with stagger
                yield return StartCoroutine(SpawnEvenlyStaggered(batch));
            }

            // Add slight randomization to interval for natural feel
            float jitteredInterval = Mathf.Max(0.01f, spawnInterval + Random.Range(-0.5f, 0.5f));
            yield return new WaitForSeconds(jitteredInterval);
        }
    }

    /// <summary>
    /// Spawn 'toSpawn' NPCs evenly across configured spawn waypoints, staggered over multiple frames.
    /// Each NPC is placed at a small random XZ offset from the waypoint so they don't stack on top of each other.
    /// </summary>
    private IEnumerator SpawnEvenlyStaggered(int toSpawn)
    {
        if (toSpawn <= 0) yield break;
        if (spawnWaypoints == null || spawnWaypoints.Count == 0) yield break;

        int pointCount = spawnWaypoints.Count;
        int basePerPoint = toSpawn / pointCount;
        int remainder = toSpawn % pointCount;

        for (int i = 0; i < pointCount; i++)
        {
            int countForThisPoint = basePerPoint + (i < remainder ? 1 : 0);
            if (countForThisPoint <= 0) continue;

            Waypoint spawnPoint = spawnWaypoints[i];
            if (spawnPoint == null) continue;

            for (int j = 0; j < countForThisPoint; j++)
            {
                NPCController prefab = npcPrefabs[Random.Range(0, npcPrefabs.Count)];

                // Random XZ offset from waypoint so NPCs don't stack at exact same spot
                Vector3 basePos = spawnPoint.transform.position;
                Vector3 jitter = new Vector3(
                    Random.Range(-spawnPositionJitter, spawnPositionJitter),
                    0f,
                    Random.Range(-spawnPositionJitter, spawnPositionJitter)
                );
                Vector3 spawnPos = basePos + jitter;

                NPCController npc = Instantiate(prefab, spawnPos, Quaternion.identity);
                npc.Initialize(spawnPoint);

                // Stagger: wait a bit before spawning next NPC
                if (staggerDelay > 0f)
                    yield return new WaitForSeconds(staggerDelay);
            }
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    // CELEBRATION BROADCAST
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Notify all NPCs within a given radius of a position to celebrate (e.g., stack completed).
    /// Called from BuildingStack or other game systems.
    /// </summary>
    public void NotifyCelebration(Vector3 position, float radius)
    {
        if (radius <= 0f) radius = celebrationRadius;

        float radiusSqr = radius * radius;
        var all = NPCController.AllNPCs;
        if (all == null || all.Count == 0) return;

        foreach (var npc in all)
        {
            if (npc == null) continue;
            if (npc.IsEnteringBuilding) continue;

            float distSqr = Vector3.SqrMagnitude(npc.transform.position - position);
            if (distSqr <= radiusSqr)
            {
                npc.Celebrate();
            }
        }
    }

    /// <summary>
    /// Overload using the default celebration radius.
    /// </summary>
    public void NotifyCelebration(Vector3 position)
    {
        NotifyCelebration(position, celebrationRadius);
    }

    // ──────────────────────────────────────────────────────────────────────
    // SEND NPCs TO BUILDING (with stagger)
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Send up to 'count' nearest available NPCs to the building to enter and disappear.
    /// </summary>
    public void SendNPCsToBuilding(BuildingStack stack, int count = 1)
    {
        SendNPCsToBuilding(stack, count, Mathf.Infinity);
    }

    /// <summary>
    /// Send up to 'count' nearest available NPCs to the building to enter and disappear,
    /// only considering NPCs within maxDistance world units.
    /// NPCs enter with staggered timing for a more natural look.
    /// </summary>
    public void SendNPCsToBuilding(BuildingStack stack, int count, float maxDistance)
    {
        if (stack == null) return;

        // Use top floor world position as target entry point
        Vector3 entryPos = stack.GetTopFloorWorldPosition();

        var all = NPCController.AllNPCs;
        if (all == null || all.Count == 0) return;

        float maxDistSqr = float.IsPositiveInfinity(maxDistance) ? float.PositiveInfinity : maxDistance * maxDistance;

        // Find nearest available NPCs (not already entering) and within maxDistance
        List<NPCController> candidates = new List<NPCController>();
        foreach (var npc in all)
        {
            if (npc == null) continue;
            if (npc.IsEnteringBuilding) continue;
            float d2 = Vector3.SqrMagnitude(npc.transform.position - entryPos);
            if (d2 <= maxDistSqr) candidates.Add(npc);
        }

        if (candidates.Count == 0) return;

        // Sort by distance
        candidates.Sort((a, b) =>
        {
            float da = Vector3.SqrMagnitude(a.transform.position - entryPos);
            float db = Vector3.SqrMagnitude(b.transform.position - entryPos);
            return da.CompareTo(db);
        });

        int toSend = Mathf.Min(candidates.Count, count);

        // Start staggered enter coroutine
        StartCoroutine(StaggeredEnterBuilding(candidates, toSend, entryPos));
    }

    /// <summary>
    /// Sends NPCs to the building entry one by one with a small delay between each.
    /// </summary>
    private IEnumerator StaggeredEnterBuilding(List<NPCController> candidates, int count, Vector3 entryPos)
    {
        for (int i = 0; i < count && i < candidates.Count; i++)
        {
            var npc = candidates[i];
            if (npc == null) continue;
            npc.StartEnterBuilding(entryPos);

            // Wait a beat before sending the next NPC
            if (enterStaggerDelay > 0f && i < count - 1)
                yield return new WaitForSeconds(enterStaggerDelay);
        }
    }
}