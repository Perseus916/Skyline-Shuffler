using System.Collections.Generic;
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

        for (int i = 0; i < toSpawn; i++)
        {
            Waypoint spawnPoint = spawnWaypoints[Random.Range(0, spawnWaypoints.Count)];

            // Pick a random prefab variant
            NPCController prefab = npcPrefabs[Random.Range(0, npcPrefabs.Count)];

            NPCController npc = Instantiate(prefab, spawnPoint.transform.position, Quaternion.identity);

            npc.Initialize(spawnPoint);
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