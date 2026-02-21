using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// Central gameplay controller for City Sort.
/// Handles stack selection, floor movement, and win detection.
/// Differentiates taps from swipes to avoid conflict with camera rotation.
/// </summary>
public class GameplayManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LevelLoader levelLoader;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private LevelCompleteUI levelCompleteUI;
    
    [Header("Settings")]
    [SerializeField] private LayerMask stackLayerMask;
    [SerializeField] private float animationDuration = 0.3f;
    
    [Header("Input Settings")]
    [Tooltip("Max movement in pixels to still count as a tap (not a swipe)")]
    [SerializeField] private float tapThreshold = 30f;
    [Tooltip("Max time in seconds for a press to count as a tap")]
    [SerializeField] private float tapMaxDuration = 0.5f;
    
    [Header("Selection Visuals")]
    [SerializeField] private Color selectedColor = new Color(1f, 1f, 0.5f, 1f);
    [SerializeField] private Color errorColor = new Color(1f, 0.3f, 0.3f, 1f);
    
    [Header("Events")]
    public UnityEvent<int, int> OnMoveCountChanged; // (current, limit)
    public UnityEvent OnLevelComplete;
    public UnityEvent OnMoveFailed;
    
    // State
    private BuildingStack selectedStack;
    private int moveCount;
    private int moveLimit;
    private int stackHeight;
    private bool isAnimating;
    private bool levelComplete;
    
    // Level info
    private int currentLevelNumber;
    private int optimalMoves;
    
    // Input tracking
    private bool isPressing;
    private Vector2 pressStartPosition;
    private float pressStartTime;
    
    // Cached
    private List<BuildingStack> allStacks = new();
    
    private void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
    }
    
    /// <summary>
    /// Initialize gameplay for a level. Call after LevelLoader.LoadLevel()
    /// </summary>
    public void InitializeLevel(LevelDataSO levelData, List<BuildingStack> stacks, int maxStackHeight)
    {
        allStacks = stacks;
        moveCount = 0;
        moveLimit = levelData.playerMoveLimit;
        stackHeight = maxStackHeight;
        levelComplete = false;
        selectedStack = null;
        isPressing = false;
        
        // Store level info for completion
        currentLevelNumber = ExtractLevelNumber(levelData.name);
        optimalMoves = levelData.optimalMoves;
        
        OnMoveCountChanged?.Invoke(moveCount, moveLimit);
        
        Debug.Log($"<color=cyan>Level {currentLevelNumber} initialized. Optimal: {optimalMoves}, Limit: {moveLimit}</color>");
    }
    
    private int ExtractLevelNumber(string levelName)
    {
        // Try to extract number from "Level_001" format
        if (levelName.Contains("_"))
        {
            string[] parts = levelName.Split('_');
            if (parts.Length >= 2 && int.TryParse(parts[1], out int num))
                return num;
        }
        return GameManager.Instance != null ? GameManager.Instance.SelectedLevel : 1;
    }
    
    private void Update()
    {
        if (levelComplete || isAnimating) return;
        
        // Get current pointer (works for mouse and touch)
        var pointer = Pointer.current;
        if (pointer == null) return;
        
        bool isPressed = pointer.press.isPressed;
        Vector2 currentPos = pointer.position.ReadValue();
        
        // Press started
        if (isPressed && !isPressing)
        {
            isPressing = true;
            pressStartPosition = currentPos;
            pressStartTime = Time.time;
        }
        
        // Press ended - check if it was a tap
        if (!isPressed && isPressing)
        {
            isPressing = false;
            
            float pressDuration = Time.time - pressStartTime;
            float moveDistance = Vector2.Distance(pressStartPosition, currentPos);
            
            // Only register as tap if:
            // 1. Movement was small (not a swipe)
            // 2. Duration was short (not a long press)
            if (moveDistance < tapThreshold && pressDuration < tapMaxDuration)
            {
                HandleTap(pressStartPosition);
            }
        }
    }
    
    private void HandleTap(Vector2 screenPos)
    {
        Ray ray = mainCamera.ScreenPointToRay(screenPos);
        
        Debug.Log($"<color=white>Tap at {screenPos}, casting ray...</color>");
        
        // First try with layer mask
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, stackLayerMask))
        {
            Debug.Log($"<color=green>Hit: {hit.collider.gameObject.name} (parent: {hit.collider.transform.parent?.name})</color>");
            
            // Try to get BuildingStack from hit object
            BuildingStack tappedStack = hit.collider.GetComponent<BuildingStack>();
            
            // Try parent
            if (tappedStack == null)
                tappedStack = hit.collider.GetComponentInParent<BuildingStack>();
            
            // Manual parent search as fallback
            if (tappedStack == null)
            {
                Transform current = hit.collider.transform.parent;
                while (current != null && tappedStack == null)
                {
                    tappedStack = current.GetComponent<BuildingStack>();
                    Debug.Log($"<color=yellow>Checking parent: {current.name}, has stack: {tappedStack != null}</color>");
                    current = current.parent;
                }
            }
            
            if (tappedStack != null)
            {
                Debug.Log($"<color=lime>Found BuildingStack! Floors: {tappedStack.FloorCount}</color>");
                ProcessStackTap(tappedStack);
                return;
            }
            else
            {
                Debug.Log("<color=orange>Hit object has no BuildingStack in hierarchy</color>");
            }
        }
        else
        {
            // Debug: Try raycast without layer mask to see if anything is there
            if (Physics.Raycast(ray, out RaycastHit debugHit, 100f))
            {
                Debug.Log($"<color=orange>LayerMask blocked hit on: {debugHit.collider.gameObject.name} (layer: {LayerMask.LayerToName(debugHit.collider.gameObject.layer)})</color>");
            }
            else
            {
                Debug.Log("<color=gray>No hit at all</color>");
            }
        }
        
        // Tapped empty space or non-stack - deselect
        if (selectedStack != null)
        {
            DeselectStack();
        }
    }
    
    private void ProcessStackTap(BuildingStack tappedStack)
    {
        // Case 1: Nothing selected - try to select this stack
        if (selectedStack == null)
        {
            if (tappedStack.FloorCount > 0)
            {
                SelectStack(tappedStack);
            }
            // Can't select empty stack (no floors to move)
            return;
        }
        
        // Case 2: Same stack tapped - deselect
        if (tappedStack == selectedStack)
        {
            DeselectStack();
            return;
        }
        
        // Case 3: Different stack tapped - try to move
        TryMoveFloor(selectedStack, tappedStack);
    }
    
    private void SelectStack(BuildingStack stack)
    {
        selectedStack = stack;
        stack.SetSelected(true, selectedColor);
        Debug.Log($"<color=yellow>Selected stack with {stack.FloorCount} floors</color>");
    }
    
    private void DeselectStack()
    {
        if (selectedStack != null)
        {
            selectedStack.SetSelected(false, Color.white);
            selectedStack = null;
            Debug.Log("<color=gray>Deselected</color>");
        }
    }
    
    private void TryMoveFloor(BuildingStack from, BuildingStack to)
    {
        // Validation 1: Source has floors
        if (from.FloorCount == 0)
        {
            ShowError(from);
            return;
        }
        
        // Validation 2: Target has room
        if (!to.CanReceiveFloor(stackHeight))
        {
            ShowError(to);
            OnMoveFailed?.Invoke();
            Debug.Log("<color=red>Target stack full!</color>");
            return;
        }
        
        // Validation 3: Move limit not exceeded
        if (moveCount >= moveLimit)
        {
            ShowError(from);
            OnMoveFailed?.Invoke();
            Debug.Log("<color=red>Move limit reached!</color>");
            return;
        }
        
        // Execute move
        ExecuteMove(from, to);
    }
    
    private void ExecuteMove(BuildingStack from, BuildingStack to)
    {
        // Get floor data from source
        var floorData = from.RemoveTopFloor();
        if (floorData.floorObject == null) return;
        
        // Deselect before animation
        DeselectStack();
        
        // Add to target (handles positioning)
        to.AddFloor(floorData.floorObject, floorData.style, animationDuration);
        
        // Update move counter
        moveCount++;
        OnMoveCountChanged?.Invoke(moveCount, moveLimit);
        
        Debug.Log($"<color=cyan>Move {moveCount}/{moveLimit}</color>");
        
        // Check win condition
        CheckWinCondition();
    }
    
    private void ShowError(BuildingStack stack)
    {
        // Brief red flash
        stack.FlashColor(errorColor, 0.2f);
    }
    
    private void CheckWinCondition()
    {
        foreach (var stack in allStacks)
        {
            if (!stack.IsComplete())
                return;
        }
        
        // All stacks complete!
        levelComplete = true;
        OnLevelComplete?.Invoke();
        
        Debug.Log($"<color=green>🎉 Level Complete in {moveCount} moves!</color>");
        
        // Save progress to GameManager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnLevelComplete(currentLevelNumber, moveCount, optimalMoves);
        }
        
        // Show UI
        if (levelCompleteUI != null)
        {
            int stars = GameManager.Instance != null 
                ? GameManager.Instance.CalculateStars(moveCount, optimalMoves)
                : (moveCount <= optimalMoves ? 3 : moveCount <= optimalMoves * 1.5f ? 2 : 1);
            levelCompleteUI.Show(currentLevelNumber, moveCount, optimalMoves, stars);
        }
    }
    
    // ========================================
    // PUBLIC API
    // ========================================
    
    public int GetMoveCount() => moveCount;
    public int GetMoveLimit() => moveLimit;
    public bool IsLevelComplete() => levelComplete;
    
    /// <summary>
    /// Check if player earned perfect clear (solved in optimal moves + bonus range)
    /// </summary>
    public bool IsPerfectClear(int optimalMoves)
    {
        return moveCount <= optimalMoves + 2;
    }
}

