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

        // Spawn evenly across spawn points
        SpawnEvenly(toSpawn);
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

                // Spawn the batch evenly across waypoints
                SpawnEvenly(batch);
            }

            yield return new WaitForSeconds(Mathf.Max(0.01f, spawnInterval));
        }
    }

    /// <summary>
    /// Spawn 'toSpawn' NPCs evenly across configured spawn waypoints.
    /// If the count does not divide evenly, the first 'remainder' waypoints receive one extra NPC.
    /// </summary>
    private void SpawnEvenly(int toSpawn)
    {
        if (toSpawn <= 0) return;
        if (spawnWaypoints == null || spawnWaypoints.Count == 0) return;

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
                NPCController npc = Instantiate(prefab, spawnPoint.transform.position, Quaternion.identity);
                npc.Initialize(spawnPoint);
            }
        }
    }

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

        int sent = 0;
        for (int i = 0; i < candidates.Count && sent < count; i++)
        {
            var npc = candidates[i];
            if (npc == null) continue;
            npc.StartEnterBuilding(entryPos);
            sent++;
        }
    }
}