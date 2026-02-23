using UnityEngine;
using System.Collections.Generic;

public class LevelLoader : MonoBehaviour
{
    [Header("Level Data")]
    public LevelDataSO currentLevelData;

    [Header("Prefabs")]
    public GameObject cranePrefab;
    public GameObject foundationPrefab;

    [Header("Settings")]
    public float gridSpacing = 3.0f;
    public float floorHeight = 1.0f;
    
    [Header("Gameplay")]
    [SerializeField] private GameplayManager gameplayManager;

    [Header("Hierarchy")]
    public Transform gridContainer; // All foundations + crane spawn here

    // Internal tracking
    private List<BuildingStack> activeStacks = new List<BuildingStack>();
    private int currentStackHeight;
    
    /// <summary>
    /// Load a level from a specific LevelDataSO (called by GameManager)
    /// </summary>
    public void LoadLevel(LevelDataSO levelData, int levelNumber)
    {
        currentLevelData = levelData;
        LoadLevel(levelNumber);
    }

    /// <summary>
    /// Build the level from the current ScriptableObject.
    /// levelNumber is passed explicitly so GameplayManager knows which level this is.
    /// </summary>
    public void LoadLevel(int levelNumber = -1)
    {
        if (currentLevelData == null)
        {
            Debug.LogError("No Level Data assigned to the Loader!");
            return;
        }

        ClearCurrentLevel();

        float offset = (currentLevelData.gridDimension - 1) * gridSpacing * 0.5f;
        
        currentStackHeight = GetStackHeightFromLevel(currentLevelData);

        // 1. Spawn Crane
        SpawnCrane(currentLevelData.craneGridPos, offset);

        // 2. Build the city
        foreach (SlotData slot in currentLevelData.slots)
        {
            SpawnSlot(slot, offset);
        }

        // Use explicit level number, fallback to data or GameManager
        int resolvedLevel = levelNumber > 0 
            ? levelNumber 
            : (currentLevelData.levelNumber > 0 
                ? currentLevelData.levelNumber 
                : (GameManager.Instance != null ? GameManager.Instance.SelectedLevel : 1));

        Debug.Log($"Level {resolvedLevel} Loaded. Stacks: {activeStacks.Count}");
        
        // 3. Initialize gameplay
        if (gameplayManager != null)
        {
            gameplayManager.InitializeLevel(currentLevelData, activeStacks, currentStackHeight, resolvedLevel);
        }
    }
    
    /// <summary>
    /// Load level layout then restore floor positions from saved state.
    /// Called by GameManager when resuming an in-progress game.
    /// </summary>
    public void LoadLevelWithRestore(LevelDataSO levelData, int levelNumber, LevelStateData savedState)
    {
        currentLevelData = levelData;
        
        ClearCurrentLevel();

        float offset = (currentLevelData.gridDimension - 1) * gridSpacing * 0.5f;
        
        currentStackHeight = GetStackHeightFromLevel(currentLevelData);

        // 1. Spawn Crane
        SpawnCrane(currentLevelData.craneGridPos, offset);

        // 2. Build the city (default layout — will be rearranged)
        foreach (SlotData slot in currentLevelData.slots)
        {
            SpawnSlot(slot, offset);
        }
        
        // 3. Restore from saved state (rearranges floors)
        if (gameplayManager != null)
        {
            gameplayManager.RestoreFromSave(currentLevelData, activeStacks, currentStackHeight, levelNumber, savedState);
        }
        
        Debug.Log($"<color=green>Level {levelNumber} loaded with saved state restore</color>");
    }
    
    private int GetStackHeightFromLevel(LevelDataSO level)
    {
        // Total capacity = ground floor + movable floors
        int maxHeight = 3;
        foreach (var slot in level.slots)
        {
            if (!slot.isLocked && slot.floorStyles != null)
            {
                int totalFloors = slot.floorStyles.Count;
                if (slot.buildingStyle != null) totalFloors++; // ground floor counts now
                maxHeight = Mathf.Max(maxHeight, totalFloors);
            }
        }
        return maxHeight;
    }

    private void SpawnCrane(Vector2Int gridPos, float offset)
    {
        // Crane is 2 units wide, so we shift it by half a grid space to center it
        Vector3 worldPos = new(
            (gridPos.x + 0.5f) * gridSpacing - offset,
            0,
            gridPos.y * gridSpacing - offset
        );

        Instantiate(cranePrefab, worldPos, Quaternion.identity, gridContainer);
    }

    private void SpawnSlot(SlotData data, float offset)
    {
        Vector3 worldPos = new(
            data.gridPos.x * gridSpacing - offset,
            0,
            data.gridPos.y * gridSpacing - offset
        );

        // Always spawn a foundation mesh
        GameObject foundation = Instantiate(foundationPrefab, worldPos, Quaternion.identity, gridContainer);
        foundation.name = $"Slot_{data.gridPos.x}_{data.gridPos.y}";

        if (data.isLocked)
        {
            ApplyLockedVisuals(foundation);
        }
        else
        {
            // All playable slots (with or without floors) need BuildingStack component
            BuildingStack stack = foundation.AddComponent<BuildingStack>();
            stack.SetMaxStackHeight(currentStackHeight);
            
            if (data.floorStyles != null && data.floorStyles.Count > 0)
            {
                // Has floors to display
                stack.InitializeFromData(data, floorHeight);
            }
            else
            {
                // Empty slot - initialize with no floors but same style target
                stack.InitializeFromData(data, floorHeight);
                ApplyEmptySlotVisuals(foundation);
            }
            
            // Set tag for raycast detection
            foundation.tag = "Floor";
            
            activeStacks.Add(stack);
        }
    }

    private void ApplyEmptySlotVisuals(GameObject obj)
    {
        obj.name += "_Empty";
        // Visual indicator for empty playable slots
        Renderer rend = obj.GetComponent<Renderer>();
        if (rend != null)
        {
            // Slightly highlighted to show it's playable
            rend.material.color = new Color(0.6f, 0.6f, 0.6f, 1f);
        }
    }

    private void ApplyLockedVisuals(GameObject obj)
    {
        obj.name += "_Locked";
        obj.tag = "Untagged"; // Raycast ignores locked slots

        // Visual feedback for locked slots
        Renderer rend = obj.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material.color = new Color(0.1f, 0.1f, 0.1f, 0.6f);
        }
    }

    public void ClearCurrentLevel()
    {
        activeStacks.Clear();

        if (gridContainer != null)
        {
            foreach (Transform child in gridContainer) Destroy(child.gameObject);
        }
    }
    
    // ========================================
    // PUBLIC API FOR GAMEPLAY
    // ========================================
    
    public List<BuildingStack> GetActiveStacks() => activeStacks;
    public int GetStackHeight() => currentStackHeight;
    public LevelDataSO GetCurrentLevelData() => currentLevelData;
}
