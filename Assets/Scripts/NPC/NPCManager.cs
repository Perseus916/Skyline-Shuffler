using System.Collections.Generic;
using UnityEngine;

public class NPCManager : MonoBehaviour
{
    [SerializeField] NPCController npcPrefab;

    [SerializeField]
    List<Waypoint> spawnWaypoints =
        new List<Waypoint>();

    [SerializeField]
    int npcCount = 20;

    void Start()
    {
        SpawnNPCs();
    }

    void SpawnNPCs()
    {
        for (int i = 0; i < npcCount; i++)
        {
            Waypoint spawnPoint =
                spawnWaypoints[
                    Random.Range(0,
                    spawnWaypoints.Count)];

            NPCController npc =
                Instantiate(
                    npcPrefab,
                    spawnPoint.transform.position,
                    Quaternion.identity);

            npc.Initialize(spawnPoint);
        }
    }
}