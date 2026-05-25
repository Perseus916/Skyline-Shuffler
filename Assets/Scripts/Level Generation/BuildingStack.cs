using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages a building stack (foundation + floors).
/// Ground and normal floors are all in one list.
/// Ground floor cannot be lifted. Everything else follows sorting rules.
/// Completion = FULL + all floors same type.
/// </summary>
public class BuildingStack : MonoBehaviour
{
    // All floors in one unified list (ground at index 0 if present)
    private readonly List<GameObject> floors = new();
    private readonly List<BuildingStyleSO> floorStyleData = new();
    
    // Ground floor tracking (ground is in floors list but can't be removed)
    private int groundFloorCount = 0; // 0 or 1
    
    // Positioning constants
    private const float FIRST_FLOOR_LOCAL_Y = 2.5f;
    private float localFloorSpacing;
    private Vector3 compensatedScale;
    private int maxStackHeight;
    
    // Grid position tracking for hints
    private Vector2Int gridPosition;
    public Vector2Int GridPosition => gridPosition;
    
    // Completion state
    private bool isCompleted;
    public bool IsCompleted => isCompleted;
    
    // Selection state
    private bool isSelected;
    private Renderer foundationRenderer;
    private Color originalColor;
    
    // Animation
    private Coroutine currentAnimation;
    
    // ========================================
    // INITIALIZATION
    // ========================================
    
    /// <summary>
    /// Initialize stack from level data.
    /// Ground floor goes into floors[0], movable floors after it.
    /// </summary>
    public void InitializeFromData(SlotData data, float floorHeight)
    {
        // Cache renderer for selection visuals
        foundationRenderer = GetComponent<Renderer>();
        if (foundationRenderer != null)
            originalColor = foundationRenderer.material.color;
        
        // Calculate scale compensation
        Vector3 parentScale = transform.localScale;
        compensatedScale = new Vector3(
            1f / parentScale.x,
            1f / parentScale.y,
            1f / parentScale.z
        );
        
        localFloorSpacing = floorHeight / parentScale.y;
        
        int floorIndex = 0;
        
        // 1. Ground floor (goes into floors list at index 0, but can't be removed)
        if (data.buildingStyle != null && data.buildingStyle.groundPrefab != null)
        {
            float localY = FIRST_FLOOR_LOCAL_Y + (floorIndex * localFloorSpacing);
            
            GameObject ground = Instantiate(data.buildingStyle.groundPrefab, Vector3.zero, Quaternion.identity, this.transform);
            ground.transform.localPosition = new Vector3(0, localY, 0);
            ground.transform.localScale = compensatedScale;
            ground.name = "GroundFloor";
            
            floors.Add(ground);
            floorStyleData.Add(data.buildingStyle);
            groundFloorCount = 1;
            
            floorIndex++;
        }

        // 2. Movable floors
        if (data.floorStyles != null)
        {
            for (int i = 0; i < data.floorStyles.Count; i++)
            {
                if (data.floorStyles[i] == null || data.floorStyles[i].floorPrefab == null) continue;
                
                float localY = FIRST_FLOOR_LOCAL_Y + (floorIndex * localFloorSpacing);

                GameObject floor = Instantiate(data.floorStyles[i].floorPrefab, Vector3.zero, Quaternion.identity, this.transform);
                floor.transform.localPosition = new Vector3(0, localY, 0);
                floor.transform.localScale = compensatedScale;
                floor.name = $"Floor_{floors.Count}";

                floors.Add(floor);
                floorStyleData.Add(data.floorStyles[i]);
                floorIndex++;
            }
        }

        // Start-of-level baseline: stacks should not be visually/logic-locked as completed.
        // Completion is earned during gameplay moves.
        isCompleted = false;
        SetCompletionVisuals(false);
        
        Debug.Log($"<color=cyan>Stack initialized: {floors.Count} total floors ({groundFloorCount} ground, {MovableFloorCount} movable)</color>");
    }
    
    /// <summary>
    /// Set max MOVABLE floor capacity for this stack.
    /// Ground floor (if present) is separate and does not consume this limit.
    /// </summary>
    public void SetMaxStackHeight(int height)
    {
        maxStackHeight = height;
    }
    
    /// <summary>
    /// Set this stack's grid position for hint system tracking.
    /// </summary>
    public void SetGridPosition(Vector2Int pos)
    {
        gridPosition = pos;
    }
    
    // ========================================
    // GAMEPLAY API
    // ========================================
    
    /// <summary>Total floor count (ground + movable)</summary>
    public int FloorCount => floors.Count;
    
    /// <summary>Only movable floors (excludes ground)</summary>
    public int MovableFloorCount => floors.Count - groundFloorCount;
    
    /// <summary>Number of ground floors (0 or 1)</summary>
    public int GroundFloorCount => groundFloorCount;
    
    /// <summary>
    /// Get the buildingName of each floor for save state serialization.
    /// Returns all floors bottom-to-top (ground first if present).
    /// </summary>
    public List<string> GetFloorStyleNames()
    {
        List<string> names = new();
        foreach (var style in floorStyleData)
        {
            names.Add(style != null ? style.buildingName : "");
        }
        return names;
    }

    /// <summary>
    /// Return the actual BuildingStyleSO objects for each floor (bottom-to-top).
    /// Used by the runtime hint/solver to snapshot the current state.
    /// </summary>
    public List<BuildingStyleSO> GetFloorStyles()
    {
        return new List<BuildingStyleSO>(floorStyleData);
    }
    
    /// <summary>
    /// Check if stack can receive a floor of the given style.
    /// Rules: not full (movable capacity), not completed, and top must match OR stack has no movable floors.
    /// </summary>
    public bool CanReceiveFloor(int maxHeight, BuildingStyleSO incomingStyle = null)
    {
        // Completed stacks are locked
        if (isCompleted) return false;
        
        // Check movable capacity (ground does NOT count).
        // Temporary no-ground stacks can hold one extra movable floor so their
        // total visible height can match grounded stacks.
        int effectiveMaxHeight = maxHeight + (groundFloorCount == 0 ? 1 : 0);
        if (MovableFloorCount >= effectiveMaxHeight) return false;
        
        // Same-type rule applies only when destination already has movable floors.
        // If stack is empty or has only ground floor, any style may be placed.
        if (incomingStyle != null && MovableFloorCount > 0)
        {
            BuildingStyleSO topStyle = floorStyleData[floorStyleData.Count - 1];
            if (topStyle != incomingStyle)
                return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Get the style of the top floor (ground or movable, whichever is on top)
    /// </summary>
    public BuildingStyleSO GetTopFloorStyle()
    {
        if (floorStyleData.Count == 0) return null;
        return floorStyleData[floorStyleData.Count - 1];
    }
    
    /// <summary>
    /// Remove and return the top floor.
    /// Cannot remove ground floor (it's permanent).
    /// </summary>
    public (GameObject floorObject, BuildingStyleSO style) RemoveTopFloor()
    {
        // Can't remove if empty or only ground remains
        if (floors.Count <= groundFloorCount)
            return (null, null);
        
        // If was completed, revert visuals before removing
        if (isCompleted)
        {
            isCompleted = false;
            SetCompletionVisuals(false);
        }
        
        int lastIndex = floors.Count - 1;
        GameObject floor = floors[lastIndex];
        BuildingStyleSO style = floorStyleData[lastIndex];
        
        floors.RemoveAt(lastIndex);
        floorStyleData.RemoveAt(lastIndex);
        
        // Unparent from this stack
        floor.transform.SetParent(null);
        
        return (floor, style);
    }
    
    /// <summary>
    /// Add a floor on top of this stack
    /// </summary>
    public void AddFloor(GameObject floor, BuildingStyleSO style, float animDuration = 0f)
    {
        // Calculate target position (index in full list)
        int floorIndex = floors.Count;
        
        float localY = FIRST_FLOOR_LOCAL_Y + (floorIndex * localFloorSpacing);
        Vector3 targetLocalPos = new Vector3(0, localY, 0);
        
        // Parent to this stack
        floor.transform.SetParent(this.transform);
        floor.transform.localScale = compensatedScale;
        floor.name = $"Floor_{floors.Count}";
        
        floors.Add(floor);
        floorStyleData.Add(style);
        
        if (animDuration > 0 && gameObject.activeInHierarchy)
        {
            if (currentAnimation != null) StopCoroutine(currentAnimation);
            currentAnimation = StartCoroutine(AnimateFloorToPosition(floor, targetLocalPos, animDuration));
        }
        else
        {
            floor.transform.localPosition = targetLocalPos;
        }
        
        // Check if stack just became complete
        CheckCompletion();
    }
    
    private IEnumerator AnimateFloorToPosition(GameObject floor, Vector3 targetLocalPos, float duration)
    {
        Vector3 startPos = floor.transform.localPosition;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            t = t * t * (3f - 2f * t); // Smoothstep
            
            floor.transform.localPosition = Vector3.Lerp(startPos, targetLocalPos, t);
            yield return null;
        }
        
        floor.transform.localPosition = targetLocalPos;
        currentAnimation = null;
    }
    
    // ========================================
    // SELECTION & VISUALS
    // ========================================
    
    /// <summary>
    /// Set selection state with visual highlight and floor elevation
    /// </summary>
    public void SetSelected(bool selected, Color highlightColor)
    {
        isSelected = selected;
        
        // Change foundation color
        if (foundationRenderer != null)
        {
            foundationRenderer.material.color = selected ? highlightColor : originalColor;
        }
    }

    private Coroutine elevationAnimation;

    /// <summary>
    /// Smoothly animates the Y elevation of the top floor for crane pickup/drop animations.
    /// </summary>
    public void AnimateTopFloorElevation(bool elevate, float duration, System.Action onComplete = null)
    {
        if (MovableFloorCount == 0)
        {
            onComplete?.Invoke();
            return;
        }

        GameObject topFloor = floors[floors.Count - 1];
        if (topFloor == null)
        {
            onComplete?.Invoke();
            return;
        }

        if (elevationAnimation != null)
            StopCoroutine(elevationAnimation);

        float targetY;
        int floorIndex = floors.Count - 1;
        float normalY = FIRST_FLOOR_LOCAL_Y + (floorIndex * localFloorSpacing);

        if (elevate)
            targetY = normalY + 8f;
        else
            targetY = normalY;

        elevationAnimation = StartCoroutine(AnimateTopFloorLocalY(topFloor, targetY, duration, onComplete));
    }

    private System.Collections.IEnumerator AnimateTopFloorLocalY(GameObject floor, float targetY, float duration, System.Action onComplete)
    {
        Vector3 pos = floor.transform.localPosition;
        float startY = pos.y;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t); // Smoothstep
            pos.y = Mathf.Lerp(startY, targetY, t);
            floor.transform.localPosition = pos;
            yield return null;
        }

        pos.y = targetY;
        floor.transform.localPosition = pos;
        elevationAnimation = null;
        onComplete?.Invoke();
    }

    /// <summary>
    /// Instantly sets the Y elevation of the top floor (used for quick resets/restoration).
    /// </summary>
    public void SetTopFloorElevationInstant(bool elevate)
    {
        if (MovableFloorCount == 0) return;

        GameObject topFloor = floors[floors.Count - 1];
        if (topFloor != null)
        {
            Vector3 pos = topFloor.transform.localPosition;
            int floorIndex = floors.Count - 1;
            float normalY = FIRST_FLOOR_LOCAL_Y + (floorIndex * localFloorSpacing);

            pos.y = elevate ? (normalY + 8f) : normalY;
            topFloor.transform.localPosition = pos;
        }
    }

    /// <summary>
    /// Returns the final world position of the top floor in its normal (non-elevated) state.
    /// </summary>
    public Vector3 GetTopFloorWorldPosition()
    {
        if (floors.Count == 0) return transform.position;

        int floorIndex = floors.Count - 1;
        float localY = FIRST_FLOOR_LOCAL_Y + (floorIndex * localFloorSpacing);
        Vector3 localPos = new Vector3(0, localY, 0);
        return transform.TransformPoint(localPos);
    }
    
    /// <summary>
    /// Brief color flash for feedback
    /// </summary>
    public void FlashColor(Color flashColor, float duration)
    {
        if (gameObject.activeInHierarchy)
            StartCoroutine(FlashColorCoroutine(flashColor, duration));
    }
    
    private IEnumerator FlashColorCoroutine(Color flashColor, float duration)
    {
        if (foundationRenderer == null) yield break;
        
        Color startColor = foundationRenderer.material.color;
        foundationRenderer.material.color = flashColor;
        
        yield return new WaitForSeconds(duration);
        
        foundationRenderer.material.color = isSelected ? startColor : originalColor;
    }
    
    // ========================================
    // COMPLETION SYSTEM
    // ========================================
    
    /// <summary>
    /// Complete = FULL (at max capacity) AND all floors are the same type.
    /// Only grounded stacks can be truly complete/locked.
    /// No "goal style" needed — just checks uniformity.
    /// Empty stacks (no floors at all) count as satisfied for win condition.
    /// Stacks with only ground floor count as satisfied (building is "vacant").
    /// </summary>
    public bool IsComplete()
    {
        // Temporary staging stacks (no ground) should never lock as complete.
        // They are only "satisfied" when empty.
        if (groundFloorCount == 0)
            return MovableFloorCount == 0;

        // Empty = satisfied for win condition
        if (floors.Count == 0) return true;
        
        // Only ground floor = vacant building, satisfied
        if (floors.Count <= groundFloorCount) return true;
        
        // Must be at movable capacity to be truly "complete"
        if (MovableFloorCount < maxStackHeight) return false;
        
        // All floors (including ground) must be the same type
        BuildingStyleSO firstStyle = floorStyleData[0];
        for (int i = 1; i < floorStyleData.Count; i++)
        {
            if (floorStyleData[i] != firstStyle)
                return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Check completion and trigger visuals if newly complete.
    /// Only locks stacks that are truly full and uniform.
    /// </summary>
    private void CheckCompletion()
    {
        // No-ground stacks are temporary holding slots and must stay movable.
        if (groundFloorCount == 0)
        {
            if (isCompleted)
            {
                isCompleted = false;
                SetCompletionVisuals(false);
            }
            return;
        }

        // Don't lock empty or ground-only stacks
        if (floors.Count <= groundFloorCount) return;
        
        bool nowComplete = IsComplete();
        
        if (nowComplete && !isCompleted)
        {
            isCompleted = true;
            SetCompletionVisuals(true);
            Debug.Log($"<color=green>🏙️ Stack complete! All {floors.Count} floors matched.</color>");
        }
        else if (!nowComplete && isCompleted)
        {
            isCompleted = false;
            SetCompletionVisuals(false);
        }
    }

    /// <summary>
    /// Force this stack into non-completed state (used at level start/restore).
    /// </summary>
    public void ForceIncompleteState()
    {
        isCompleted = false;
        SetCompletionVisuals(false);
    }
    
    /// <summary>
    /// Toggle CompleteBuilding/IncompleteBuilding children on ALL floors.
    /// Floor prefab expected structure:
    ///   FloorPrefab
    ///     ├── IncompleteBuilding  (active by default)
    ///     └── CompleteBuilding    (inactive by default)
    /// </summary>
    private void SetCompletionVisuals(bool complete)
    {
        foreach (var floor in floors)
        {
            if (floor != null)
                ToggleFloorVisuals(floor, complete);
        }
    }
    
    private void ToggleFloorVisuals(GameObject floor, bool complete)
    {
        Transform incomplete = floor.transform.Find("IncompleteBuilding");
        Transform completed = floor.transform.Find("CompleteBuilding");
        
        if (incomplete != null) incomplete.gameObject.SetActive(!complete);
        if (completed != null) completed.gameObject.SetActive(complete);
    }
    
    /// <summary>
    /// Check if this stack has no movable floors
    /// </summary>
    public bool IsEmpty => MovableFloorCount == 0;
}
