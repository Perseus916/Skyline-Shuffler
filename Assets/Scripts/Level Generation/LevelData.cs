using UnityEngine;
using System.Collections.Generic;

public enum DifficultyTier 
{ 
    Tutorial,   // Levels 1-5: Instant solve
    Easy,       // Levels 6-30: Obvious moves
    Medium,     // Levels 31-100: Starting to think
    Hard,       // Levels 101-300: Comfortable challenge
    Expert,     // Levels 301-700: Strategic thinking
    Master      // Levels 700+: Mastery puzzles
}

[CreateAssetMenu(fileName = "Level_", menuName = "Skyline Architect/Level Data")]
public class LevelDataSO : ScriptableObject
{
    public int levelNumber;
    public int gridDimension;
    public Vector2Int craneGridPos;

    [Header("Initial Setup")]
    public List<SlotData> slots = new List<SlotData>();

    [Header("Optimal Solution")]
    public List<MoveStep> solvingSteps = new List<MoveStep>();

    [Header("Player Constraints")]
    public int optimalMoves;        // Minimum moves to solve
    public int playerMoveLimit;     // Generous limit for multiple solutions

    [Header("Difficulty Metrics")]
    public DifficultyTier tier;
    public float cognitiveComplexity;   // 0-100: how much thinking required
    public int maxBlockingDepth;        // Deepest color burial (0 = none)
    public float contaminationRatio;    // 0-1: how mixed the puzzle is

    [Header("Engagement")]
    public int perfectClearBonus;       // Reward for solving in optimal moves
    public bool isBreatherLevel;        // Intentionally easier level
    public bool isMilestone;            // Every 50th level

    [Header("Generation Debug")]
    public int shuffleDepth;            // How many shuffles were performed
    public int emptySlotCount;          // Number of empty slots
    public int buildingStyleCount;      // Number of different building styles
}

[System.Serializable]
public struct MoveStep
{
    public Vector2Int fromGridPos;
    public Vector2Int toGridPos;
}

[System.Serializable]
public class SlotData
{
    public Vector2Int gridPos;
    public bool isLocked;
    public bool isEmpty;
    public BuildingStyleSO buildingStyle; // The "Goal" style for this slot
    public List<BuildingStyleSO> floorStyles = new List<BuildingStyleSO>(); // Current floor stack
}