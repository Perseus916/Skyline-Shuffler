using System.Collections.Generic;
using UnityEngine;

public class Waypoint : MonoBehaviour
{
    public List<Waypoint> connectedWaypoints = new List<Waypoint>();

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;

        foreach (Waypoint waypoint in connectedWaypoints)
        {
            if (waypoint != null)
            {
                Gizmos.DrawLine(transform.position, waypoint.transform.position);
            }
        }
    }
}