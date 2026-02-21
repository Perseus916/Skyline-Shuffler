using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BuildingLibrary", menuName = "Skyline Architect/Building Library")]
public class BuildingLibrarySO : ScriptableObject
{
    public List<BuildingStyleSO> allStyles;

    public BuildingStyleSO GetRandomStyle()
    {
        if (allStyles.Count == 0) return null;
        return allStyles[Random.Range(0, allStyles.Count)];
    }
}