using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages a building stack on a foundation.
/// Handles floor spawning, selection visuals, and floor movement.
/// </summary>
public class BuildingStack : MonoBehaviour
{
    public BuildingStyleSO myStyle; // Goal style for completion
    
    // Floor tracking
    private readonly List<GameObject> floors = new();
    private readonly List<BuildingStyleSO> floorStyleData = new();
    private GameObject groundFloor;
    
    // Positioning constants
    private const float FIRST_FLOOR_LOCAL_Y = 2.5f;
    private float localFloorSpacing;
    private Vector3 compensatedScale;
    private int maxStackHeight;
    
    // Selection state
    private bool isSelected;
    private Renderer foundationRenderer;
    private Color originalColor;
    
    // Animation
    private Coroutine currentAnimation;
    
    /// <summary>
    /// Initialize stack from level data
    /// </summary>
    public void InitializeFromData(SlotData data, Transform worldParent, float floorHeight)
    {
        myStyle = data.buildingStyle;
        
        Debug.Log($"<color=cyan>Initializing stack at {data.gridPos}: style={myStyle?.buildingName}, floorStyles.Count={data.floorStyles?.Count ?? 0}</color>");
        
        // Cache renderer for selection visuals
        foundationRenderer = GetComponent<Renderer>();
        if (foundationRenderer != null)
            originalColor = foundationRenderer.material.color;
        
        // Get parent (foundation) scale to calculate floor spacing and compensate scale
        Vector3 parentScale = transform.localScale;
        compensatedScale = new Vector3(
            1f / parentScale.x,
            1f / parentScale.y,
            1f / parentScale.z
        );
        
        localFloorSpacing = floorHeight / parentScale.y;
        
        int floorIndex = 0;
        
        // 1. Spawn Ground Floor if we have a style assigned (immovable first floor)
        if (myStyle != null && myStyle.groundPrefab != null)
        {
            float localY = FIRST_FLOOR_LOCAL_Y + (floorIndex * localFloorSpacing);
            
            groundFloor = Instantiate(myStyle.groundPrefab, Vector3.zero, Quaternion.identity, this.transform);
            groundFloor.transform.localPosition = new Vector3(0, localY, 0);
            groundFloor.transform.localScale = compensatedScale;
            groundFloor.name = "GroundFloor";
            
            floorIndex++;
        }

        // 2. Spawn the movable floors stored in the Level Data
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
        
        Debug.Log($"<color=cyan>Stack initialized with {floors.Count} movable floors</color>");
    }
    
    /// <summary>
    /// Set the max stack height (needed for validation)
    /// </summary>
    public void SetMaxStackHeight(int height)
    {
        maxStackHeight = height;
    }
    
    // ========================================
    // GAMEPLAY API
    // ========================================
    
    /// <summary>
    /// Get the current movable floor count
    /// </summary>
    public int FloorCount => floors.Count;
    
    /// <summary>
    /// Check if stack can receive another floor
    /// </summary>
    public bool CanReceiveFloor(int maxHeight)
    {
        int totalFloors = floors.Count;
        if (groundFloor != null) totalFloors++;
        return totalFloors < maxHeight;
    }
    
    /// <summary>
    /// Get the style of the top floor
    /// </summary>
    public BuildingStyleSO GetTopFloorStyle()
    {
        if (floorStyleData.Count == 0) return null;
        return floorStyleData[floorStyleData.Count - 1];
    }
    
    /// <summary>
    /// Remove and return the top floor
    /// </summary>
    public (GameObject floorObject, BuildingStyleSO style) RemoveTopFloor()
    {
        if (floors.Count == 0)
            return (null, null);
        
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
        // Calculate target position
        int floorIndex = floors.Count;
        if (groundFloor != null) floorIndex++;
        
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
            // Animate to position
            if (currentAnimation != null) StopCoroutine(currentAnimation);
            currentAnimation = StartCoroutine(AnimateFloorToPosition(floor, targetLocalPos, animDuration));
        }
        else
        {
            floor.transform.localPosition = targetLocalPos;
        }
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
        
        // Elevate or lower the top floor
        if (floors.Count > 0)
        {
            GameObject topFloor = floors[floors.Count - 1];
            if (topFloor != null)
            {
                Vector3 pos = topFloor.transform.localPosition;
                
                if (selected)
                {
                    // Move up by 8 local units (= 2 world units since parent Y scale is 0.25)
                    pos.y += 8f;
                }
                else
                {
                    // Calculate correct position
                    int floorIndex = floors.Count - 1;
                    if (groundFloor != null) floorIndex++;
                    pos.y = FIRST_FLOOR_LOCAL_Y + (floorIndex * localFloorSpacing);
                }
                
                topFloor.transform.localPosition = pos;
            }
        }
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
    // WIN CONDITION
    // ========================================
    
    /// <summary>
    /// Check if this stack is complete (all floors match goal style)
    /// </summary>
    public bool IsComplete()
    {
        // Empty stacks with no goal are considered complete
        if (myStyle == null)
            return floors.Count == 0;
        
        // Must have floors to be complete
        if (floors.Count == 0)
            return false;
        
        // All movable floors must match goal style
        foreach (var style in floorStyleData)
        {
            if (style != myStyle)
                return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Check if this stack is empty (no movable floors)
    /// </summary>
    public bool IsEmpty => floors.Count == 0;
}
