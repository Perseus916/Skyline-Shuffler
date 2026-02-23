using UnityEngine;

/// <summary>
/// Tunable difficulty configuration for level generation.
/// Allows designers to adjust difficulty curve without code changes.
/// </summary>
[CreateAssetMenu(fileName = "DifficultyConfig", menuName = "Skyline Architect/Difficulty Config")]
public class DifficultyConfigSO : ScriptableObject
{
    [Header("Exponential Curve")]
    [Tooltip("Power for difficulty curve. Lower = gentler curve. Default: 0.25")]
    [Range(0.1f, 0.5f)]
    public float exponent = 0.25f;
    
    [Tooltip("Base complexity before curve is applied")]
    public float baseComplexity = 1f;

    [Header("Move Targets (Reference Points)")]
    [Tooltip("Expected optimal moves at level 100")]
    public int level100OptimalMoves = 16;
    
    [Tooltip("Expected optimal moves at level 500")]
    public int level500OptimalMoves = 24;
    
    [Tooltip("Expected optimal moves at level 1000")]
    public int level1000OptimalMoves = 28;
    
    [Tooltip("Maximum optimal moves (soft cap for very late game)")]
    public int maxOptimalMoves = 35;

    [Header("Cognitive Complexity Targets")]
    [Tooltip("Cognitive score at level 100 (0-100)")]
    [Range(0, 100)]
    public float level100Cognitive = 30f;
    
    [Tooltip("Cognitive score at level 500 (0-100)")]
    [Range(0, 100)]
    public float level500Cognitive = 65f;

    [Header("Player Move Limit")]
    [Tooltip("Multiplier for player moves vs optimal. 1.8 = 80% extra moves allowed (tighter = more ad demand)")]
    public float playerLimitMultiplier = 1.8f;
    
    [Tooltip("Round player limit to nearest N")]
    public int playerLimitRounding = 5;

    [Header("Breather Levels")]
    [Tooltip("Minimum levels between breathers")]
    public int breatherMinGap = 5;
    
    [Tooltip("Maximum levels between breathers")]
    public int breatherMaxGap = 12;
    
    [Tooltip("How much easier breather levels are (0.7 = 30% easier)")]
    [Range(0.5f, 0.9f)]
    public float breatherDifficultyMultiplier = 0.7f;

    [Header("Perfect Clear Bonus")]
    [Tooltip("Base bonus for solving in optimal moves")]
    public int basePerfectBonus = 10;
    
    [Tooltip("Bonus multiplier based on level")]
    public float bonusPerLevel = 0.1f;
    
    [Tooltip("Max perfect clear bonus")]
    public int maxPerfectBonus = 100;

    [Header("Grid Settings")]
    [Tooltip("Level at which grid expands from 3x3 to 4x4")]
    public int gridExpansionLevel = 500;
    
    [Tooltip("Level at which grid expands from 4x4 to 5x5")]
    public int gridExpansionLevel2 = 1500;

    [Header("Stack Height")]
    [Tooltip("Base stack height for early levels")]
    public int baseStackHeight = 3;
    
    [Tooltip("Level at which stack height increases to 4")]
    public int stackHeightIncrease1 = 100;
    
    [Tooltip("Level at which stack height increases to 5")]
    public int stackHeightIncrease2 = 300;
    
    [Tooltip("Level at which stack height increases to 6")]
    public int stackHeightIncrease3 = 600;

    [Header("Tier Thresholds")]
    public int tutorialEnd = 5;
    public int easyEnd = 30;
    public int mediumEnd = 100;
    public int hardEnd = 300;
    public int expertEnd = 700;
    // Master = 700+

    /// <summary>
    /// Calculate optimal moves for a given level using smooth interpolation
    /// </summary>
    public int GetOptimalMoves(int level)
    {
        if (level <= 1) return 1;
        if (level <= tutorialEnd) return Mathf.Clamp(level, 1, 4);
        
        // Use logarithmic interpolation for very smooth curve
        // Maps level 1-1000 to 0-1, then interpolates move count
        float t = Mathf.Log10(level) / Mathf.Log10(1000);
        t = Mathf.Clamp01(t);
        
        // Cubic easing for even smoother progression
        t = t * t * (3f - 2f * t);
        
        int moves = Mathf.RoundToInt(Mathf.Lerp(4f, maxOptimalMoves, t));
        return Mathf.Max(moves, 2);
    }

    /// <summary>
    /// Calculate cognitive complexity for a given level
    /// </summary>
    public float GetCognitiveComplexity(int level)
    {
        if (level <= tutorialEnd) return level * 2f;
        
        // Smooth S-curve for cognitive complexity
        float t = Mathf.Log10(level) / Mathf.Log10(2000);
        t = Mathf.Clamp01(t);
        
        // Sigmoid-like curve
        t = t * t * (3f - 2f * t);
        
        return Mathf.Lerp(5f, 95f, t);
    }

    /// <summary>
    /// Get the appropriate difficulty tier for a level
    /// </summary>
    public DifficultyTier GetTier(int level)
    {
        if (level <= tutorialEnd) return DifficultyTier.Tutorial;
        if (level <= easyEnd) return DifficultyTier.Easy;
        if (level <= mediumEnd) return DifficultyTier.Medium;
        if (level <= hardEnd) return DifficultyTier.Hard;
        if (level <= expertEnd) return DifficultyTier.Expert;
        return DifficultyTier.Master;
    }

    /// <summary>
    /// Calculate player move limit (generous for multiple solutions)
    /// </summary>
    public int GetPlayerMoveLimit(int optimalMoves)
    {
        float raw = optimalMoves * playerLimitMultiplier;
        return Mathf.CeilToInt(raw / playerLimitRounding) * playerLimitRounding;
    }

    /// <summary>
    /// Calculate perfect clear bonus for a level
    /// </summary>
    public int GetPerfectClearBonus(int level)
    {
        int bonus = Mathf.RoundToInt(basePerfectBonus + (level * bonusPerLevel));
        return Mathf.Min(bonus, maxPerfectBonus);
    }

    /// <summary>
    /// Get stack height for a level
    /// </summary>
    public int GetStackHeight(int level)
    {
        if (level < stackHeightIncrease1) return baseStackHeight;
        if (level < stackHeightIncrease2) return 4;
        if (level < stackHeightIncrease3) return 5;
        return 6;
    }

    /// <summary>
    /// Get grid dimension for a level
    /// </summary>
    public int GetGridDimension(int level)
    {
        if (level < gridExpansionLevel) return 3;
        if (level < gridExpansionLevel2) return 4;
        return 5;
    }
}
