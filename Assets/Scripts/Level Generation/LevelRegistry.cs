using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Registry of all available levels in the game.
/// Attach to a GameObject in the scene or make it a ScriptableObject.
/// </summary>
[CreateAssetMenu(fileName = "LevelRegistry", menuName = "City Sort/Level Registry")]
public class LevelRegistrySO : ScriptableObject
{
    [Tooltip("All levels in order. Index 0 = Level 1, etc.")]
    public List<LevelDataSO> levels = new();
    
    /// <summary>
    /// Get level by number (1-indexed)
    /// </summary>
    public LevelDataSO GetLevel(int levelNumber)
    {
        int index = levelNumber - 1;
        if (index >= 0 && index < levels.Count)
        {
            return levels[index];
        }
        
        Debug.LogWarning($"Level {levelNumber} not found in registry! Available: 1-{levels.Count}");
        return null;
    }
    
    /// <summary>
    /// Total number of levels available
    /// </summary>
    public int TotalLevels => levels.Count;
}
