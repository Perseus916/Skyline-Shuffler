using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class LevelLoader : MonoBehaviour
{
    [Header("Level Data")]
    public LevelDataSO currentLevelData;

    [Header("Prefabs")]
    public GameObject foundationPrefab;

    [Header("Settings")]
    public float gridSpacing = 3.0f;
    public float floorHeight = 1.0f;
    
    [Header("Gameplay")]
    [SerializeField] private GameplayManager gameplayManager;

    [Header("Hierarchy")]
    public Transform gridContainer; // All foundations spawn here

    // Internal tracking
    private List<BuildingStack> activeStacks = new List<BuildingStack>();
    private List<LockedSlotEntry> lockedSlots = new List<LockedSlotEntry>();
    private int currentStackHeight;

    /// <summary>Represents a locked slot that can be unlocked at runtime.</summary>
    public class LockedSlotEntry
    {
        public GameObject foundation;
        public Vector2Int gridPos;
        public SlotData originalData;
    }
    
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

        // 2. Build the complete city grid
        // Iterate every grid position and spawn a foundation. If the level provides
        // SlotData for that position, use it; otherwise spawn an empty playable slot.
        for (int x = 0; x < currentLevelData.gridDimension; x++)
        {
            for (int y = 0; y < currentLevelData.gridDimension; y++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                // Find slot data for this grid position if any
                SlotData slot = currentLevelData.slots != null
                    ? currentLevelData.slots.FirstOrDefault(s => s.gridPos == pos)
                    : null;

                SpawnSlotAtGridPos(pos, slot, offset);
            }
        }

        // Legacy levels may not define every grid coordinate.
        // Spawn missing coordinates as locked foundations so players see
        // non-playable bases consistently (black, same as other locked tiles).
        SpawnMissingLockedSlots(offset);

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

        // 2. Build the complete city grid (default layout — will be rearranged)
        for (int x = 0; x < currentLevelData.gridDimension; x++)
        {
            for (int y = 0; y < currentLevelData.gridDimension; y++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                SlotData slot = currentLevelData.slots != null
                    ? currentLevelData.slots.FirstOrDefault(s => s.gridPos == pos)
                    : null;

                SpawnSlotAtGridPos(pos, slot, offset);
            }
        }

        // Keep locked visual coverage consistent for levels that omit some slots.
        SpawnMissingLockedSlots(offset);
        
        // 3. Restore from saved state (rearranges floors)
        if (gameplayManager != null)
        {
            gameplayManager.RestoreFromSave(currentLevelData, activeStacks, currentStackHeight, levelNumber, savedState);
        }
        
        Debug.Log($"<color=green>Level {levelNumber} loaded with saved state restore</color>");
    }
    
    private int GetStackHeightFromLevel(LevelDataSO level)
    {
        // Runtime uses MOVABLE floor capacity.
        // We infer it robustly from the level content because shuffled states
        // can hide the original full stack size in any single slot.
        int maxMovableFromSlot = 1;
        Dictionary<BuildingStyleSO, int> stylePieceCounts = new();

        foreach (var slot in level.slots)
        {
            if (!slot.isLocked && slot.floorStyles != null)
            {
                maxMovableFromSlot = Mathf.Max(maxMovableFromSlot, slot.floorStyles.Count);

                // Count all pieces by style (ground + movable). In generated levels,
                // each style total is (movableCapacity + 1 ground), so this recovers
                // capacity even when no single slot is currently full.
                if (slot.buildingStyle != null)
                {
                    if (!stylePieceCounts.ContainsKey(slot.buildingStyle))
                        stylePieceCounts[slot.buildingStyle] = 0;
                    stylePieceCounts[slot.buildingStyle]++;
                }

                for (int i = 0; i < slot.floorStyles.Count; i++)
                {
                    var style = slot.floorStyles[i];
                    if (style == null) continue;
                    if (!stylePieceCounts.ContainsKey(style))
                        stylePieceCounts[style] = 0;
                    stylePieceCounts[style]++;
                }
            }
        }

        int inferredFromStyleTotals = 0;
        if (stylePieceCounts.Count > 0)
        {
            int maxPiecesOfAnyStyle = stylePieceCounts.Values.Max();
            inferredFromStyleTotals = Mathf.Max(0, maxPiecesOfAnyStyle - 1); // subtract ground
        }

        int resolved = Mathf.Max(maxMovableFromSlot, inferredFromStyleTotals);

        // Safe fallback for malformed/custom levels
        if (resolved <= 0) resolved = 3;

        return resolved;
    }

    private void SpawnSlotAtGridPos(Vector2Int gridPos, SlotData data, float offset)
    {
        Vector3 worldPos = new(
            gridPos.x * gridSpacing - offset,
            0,
            gridPos.y * gridSpacing - offset
        );

        // Always spawn a foundation mesh
        GameObject foundation = Instantiate(foundationPrefab, worldPos, Quaternion.identity, gridContainer);
        foundation.name = $"Slot_{gridPos.x}_{gridPos.y}";

        if (data != null && data.isLocked)
        {
            ApplyLockedVisuals(foundation);
            lockedSlots.Add(new LockedSlotEntry
            {
                foundation = foundation,
                gridPos = gridPos,
                originalData = data
            });
        }
        else if (data != null)
        {
            // Playable slot with data
            BuildingStack stack = foundation.AddComponent<BuildingStack>();
            stack.SetMaxStackHeight(currentStackHeight);
            stack.SetGridPosition(data.gridPos);
            
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
        else
        {
            // No slot data provided: spawn an empty playable slot
            BuildingStack stack = foundation.AddComponent<BuildingStack>();
            stack.SetMaxStackHeight(currentStackHeight);
            stack.SetGridPosition(gridPos);

            // Create a minimal SlotData to initialize as empty
            var emptyData = new SlotData { gridPos = gridPos };
            stack.InitializeFromData(emptyData, floorHeight);

            ApplyEmptySlotVisuals(foundation);

            foundation.tag = "Floor";
            activeStacks.Add(stack);
        }
    }

    private void SpawnMissingLockedSlots(float offset)
    {
        if (currentLevelData == null) return;

        HashSet<Vector2Int> existing = new HashSet<Vector2Int>();
        if (currentLevelData.slots != null)
        {
            foreach (var slot in currentLevelData.slots)
            {
                existing.Add(slot.gridPos);
            }
        }

        for (int z = 0; z < currentLevelData.gridDimension; z++)
        {
            for (int x = 0; x < currentLevelData.gridDimension; x++)
            {
                var pos = new Vector2Int(x, z);
                if (existing.Contains(pos)) continue;

                SpawnSlotAtGridPos(pos, new SlotData
                {
                    gridPos = pos,
                    isLocked = true,
                    isEmpty = false,
                    buildingStyle = null,
                    floorStyles = new List<BuildingStyleSO>()
                }, offset);
            }
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
        lockedSlots.Clear();

        if (gridContainer != null)
        {
            foreach (Transform child in gridContainer) Destroy(child.gameObject);
        }
    }

    /// <summary>
    /// Unlock one locked slot at runtime: convert it to a playable empty BuildingStack.
    /// Returns the newly created BuildingStack, or null if no locked slots remain.
    /// </summary>
    public BuildingStack UnlockOneLockedSlot()
    {
        if (lockedSlots == null || lockedSlots.Count == 0) return null;

        // Pick the first (or random) locked slot
        var entry = lockedSlots[0];
        lockedSlots.RemoveAt(0);

        if (entry.foundation == null) return null;

        // Remove locked visuals by resetting color
        Renderer rend = entry.foundation.GetComponent<Renderer>();
        if (rend != null)
            rend.material.color = new Color(0.6f, 0.6f, 0.6f, 1f);

        // Add BuildingStack component and make it playable
        BuildingStack stack = entry.foundation.AddComponent<BuildingStack>();
        stack.SetMaxStackHeight(currentStackHeight);
        stack.SetGridPosition(entry.gridPos);

        var emptyData = new SlotData { gridPos = entry.gridPos };
        stack.InitializeFromData(emptyData, floorHeight);

        entry.foundation.tag = "Floor";
        entry.foundation.name = entry.foundation.name.Replace("_Locked", "_Unlocked");

        activeStacks.Add(stack);

        Debug.Log($"<color=yellow>Unlocked slot at {entry.gridPos}. Remaining locked: {lockedSlots.Count}</color>");
        return stack;
    }
    
    // ========================================
    // PUBLIC API FOR GAMEPLAY
    // ========================================
    
    public List<BuildingStack> GetActiveStacks() => activeStacks;
    public int GetStackHeight() => currentStackHeight;
    public LevelDataSO GetCurrentLevelData() => currentLevelData;
    public int GetLockedSlotCount() => lockedSlots != null ? lockedSlots.Count : 0;
}
