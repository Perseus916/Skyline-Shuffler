using UnityEngine;
using System.Collections.Generic;

public class LevelLoader : MonoBehaviour
{
    [Header("Level Data")]
    public LevelDataSO currentLevelData;
    [SerializeField] private LevelRegistrySO levelRegistry;
    [SerializeField] private bool autoLoadOnStart = true;

    [Header("Prefabs")]
    public GameObject cranePrefab;
    public GameObject foundationPrefab;

    [Header("Settings")]
    public float gridSpacing = 3.0f;
    public float floorHeight = 1.0f;
    
    [Header("Gameplay")]
    [SerializeField] private GameplayManager gameplayManager;

    [Header("Hierarchy Containers")]
    public Transform gridContainer; // Keep foundations here
    public Transform cityContainer; // Keep buildings here to avoid scale issues

    // Internal tracking for gameplay logic
    private List<BuildingStack> activeStacks = new List<BuildingStack>();
    private int currentStackHeight;
    
    private void Start()
    {
        if (autoLoadOnStart)
        {
            // Get level from GameManager if available
            if (GameManager.Instance != null && levelRegistry != null)
            {
                int levelNumber = GameManager.Instance.SelectedLevel;
                LoadLevelByNumber(levelNumber);
            }
            else if (currentLevelData != null)
            {
                // Fallback to assigned level
                LoadLevel();
            }
        }
    }
    
    /// <summary>
    /// Load a specific level by number
    /// </summary>
    public void LoadLevelByNumber(int levelNumber)
    {
        if (levelRegistry == null)
        {
            Debug.LogError("No LevelRegistry assigned!");
            return;
        }
        
        LevelDataSO levelData = levelRegistry.GetLevel(levelNumber);
        if (levelData != null)
        {
            currentLevelData = levelData;
            LoadLevel();
        }
    }

    /// <summary>
    /// Call this to build the level from the ScriptableObject
    /// </summary>
    public void LoadLevel()
    {
        if (currentLevelData == null)
        {
            Debug.LogError("No Level Data assigned to the Loader!");
            return;
        }

        ClearCurrentLevel();

        float offset = (currentLevelData.gridDimension - 1) * gridSpacing * 0.5f;
        
        // Determine stack height from level data or config
        currentStackHeight = GetStackHeightFromLevel(currentLevelData);

        // 1. Spawn Crane at stored grid position
        SpawnCrane(currentLevelData.craneGridPos, offset);

        // 2. Iterate through slot data to build the city
        foreach (SlotData slot in currentLevelData.slots)
        {
            SpawnSlot(slot, offset);
        }

        Debug.Log($"Level {currentLevelData.levelNumber} Loaded Successfully. Stacks: {activeStacks.Count}");
        
        // 3. Initialize gameplay manager if present
        if (gameplayManager != null)
        {
            gameplayManager.InitializeLevel(currentLevelData, activeStacks, currentStackHeight);
        }
    }
    
    private int GetStackHeightFromLevel(LevelDataSO level)
    {
        // Find the tallest stack in the level data
        int maxHeight = 3; // Default minimum
        foreach (var slot in level.slots)
        {
            if (!slot.isLocked && slot.floorStyles != null)
            {
                int totalFloors = slot.floorStyles.Count;
                if (slot.buildingStyle != null) totalFloors++; // Ground floor
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
                stack.InitializeFromData(data, cityContainer, floorHeight);
            }
            else
            {
                // Empty slot - initialize with no floors but same style target
                stack.InitializeFromData(data, cityContainer, floorHeight);
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

        // Destroy children in grid container
        if (gridContainer != null)
        {
            foreach (Transform child in gridContainer) Destroy(child.gameObject);
        }

        // Destroy children in city container
        if (cityContainer != null)
        {
            foreach (Transform child in cityContainer) Destroy(child.gameObject);
        }
    }
    
    // ========================================
    // PUBLIC API FOR GAMEPLAY
    // ========================================
    
    public List<BuildingStack> GetActiveStacks() => activeStacks;
    public int GetStackHeight() => currentStackHeight;
    public LevelDataSO GetCurrentLevelData() => currentLevelData;
}
