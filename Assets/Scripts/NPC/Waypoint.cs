using System.Collections.Generic;
using UnityEngine;

public class Waypoint : MonoBehaviour
{
    public List<Waypoint> connectedWaypoints = new List<Waypoint>();

    [Header("Activation")]
    [Tooltip("If true, this waypoint is only considered active when the assigned BuildingStack is completed.")]
    public bool activeWhenBuildingComplete = false;

    [Tooltip("Optional BuildingStack that controls activation when activeWhenBuildingComplete is true.")]
    public BuildingStack buildingOnWaypoint;

    /// <summary>
    /// Returns whether this waypoint is currently active (available for NPCs to move to).
    /// </summary>
    public bool IsActive()
    {
        if (!activeWhenBuildingComplete) return true;
        if (buildingOnWaypoint == null) return false;
        return buildingOnWaypoint.IsCompleted;
    }

    private void OnDrawGizmos()
    {
        foreach (Waypoint waypoint in connectedWaypoints)
        {
            if (waypoint == null) continue;

            // Color depends on whether this connection is currently active.
            Color col = Color.green;

            bool thisActive = IsActive();
            bool otherActive = waypoint.IsActive();

            if (thisActive && otherActive)
                col = Color.green;
            else if (thisActive && !otherActive)
                col = Color.yellow;
            else if (!thisActive && otherActive)
                col = Color.cyan;
            else
                col = Color.red;

            Gizmos.color = col;
            Gizmos.DrawLine(transform.position, waypoint.transform.position);
        }

        // Draw a small sphere showing activation for this waypoint
        Gizmos.color = IsActive() ? Color.green : Color.red;
        Gizmos.DrawSphere(transform.position, 0.1f);
    }
}