using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Style_New", menuName = "Skyline Architect/Single Style")]
public class BuildingStyleSO : ScriptableObject
{
    public string buildingName;
    public GameObject groundPrefab;
    public GameObject floorPrefab;
    public GameObject completedPrefab; // For that "lights-on" rewarded state
}