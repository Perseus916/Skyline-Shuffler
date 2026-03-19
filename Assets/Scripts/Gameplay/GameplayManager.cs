using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Linq;

// ========================================
// SAVE STATE DATA CLASSES
// ========================================

/// <summary>
/// Serializable snapshot of a single stack's floors
/// </summary>
[System.Serializable]
public class StackStateData
{
    public int stackIndex;
    public List<string> floorStyleNames = new(); // buildingName of each floor, bottom to top
}

/// <summary>
/// Serializable snapshot of the entire level's current state
/// </summary>
[System.Serializable]
public class LevelStateData
{
    public int levelNumber;
    public int moveCount;
    public List<StackStateData> stacks = new();
}

/// <summary>
/// Central gameplay controller for City Sort.
/// Handles stack selection, floor movement, win detection, and auto-save.
/// </summary>
public class GameplayManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LevelLoader levelLoader;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private OutOfMovesUI outOfMovesUI;
    
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
    public UnityEvent<int> OnUndoCountChanged;  // remaining undos
    public UnityEvent<int> OnHintCountChanged;  // remaining hints
    public UnityEvent<int> OnCoinsChanged;      // total coins
    
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
    
    // Undo stack
    private struct UndoRecord
    {
        public int fromIndex; // index into allStacks
        public int toIndex;
    }
    private Stack<UndoRecord> undoStack = new();
    
    // Hint system
    private List<MoveStep> solutionSteps;
    private int nextHintIndex;

    // Win-condition requirements derived from level data
    // styleName -> required movable floors that must end up on a single grounded stack
    private Dictionary<string, int> requiredMovableByStyle = new();
    
    private void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
    }
    
    /// <summary>
    /// Initialize gameplay for a level (fresh start).
    /// Called by LevelLoader after spawning all objects.
    /// </summary>
    public void InitializeLevel(LevelDataSO levelData, List<BuildingStack> stacks, int maxStackHeight, int levelNumber)
    {
        allStacks = stacks;
        moveCount = 0;
        moveLimit = levelData.playerMoveLimit;
        stackHeight = ResolveMovableStackHeight(stacks, maxStackHeight);
        foreach (var stack in allStacks)
        {
            if (stack != null)
            {
                stack.SetMaxStackHeight(stackHeight);
                stack.ForceIncompleteState();
            }
        }
        levelComplete = false;
        selectedStack = null;
        isPressing = false;
        
        currentLevelNumber = levelNumber;
        optimalMoves = levelData.optimalMoves;

        BuildRequiredStyleCounts(levelData);
        
        // Undo/Hint reset
        undoStack.Clear();
        solutionSteps = levelData.solvingSteps != null ? new List<MoveStep>(levelData.solvingSteps) : new();
        nextHintIndex = 0;
        
        OnMoveCountChanged?.Invoke(moveCount, moveLimit);
        OnUndoCountChanged?.Invoke(SaveSystem.GetFreeUndos());
        OnHintCountChanged?.Invoke(SaveSystem.GetFreeHints());
        OnCoinsChanged?.Invoke(SaveSystem.GetCoins());
        
        // Save initial state
        SaveInProgressState();
        
        Debug.Log($"<color=cyan>Level {currentLevelNumber} initialized. Optimal: {optimalMoves}, Limit: {moveLimit}, Capacity: {stackHeight}, Stacks: {allStacks.Count}</color>");
    }
    
    /// <summary>
    /// Restore gameplay from a saved in-progress state.
    /// Called after LevelLoader spawns the default level layout.
    /// </summary>
    public void RestoreFromSave(LevelDataSO levelData, List<BuildingStack> stacks, int maxStackHeight, 
                                 int levelNumber, LevelStateData savedState)
    {
        allStacks = stacks;
        moveCount = savedState.moveCount;
        moveLimit = levelData.playerMoveLimit;
        stackHeight = ResolveMovableStackHeight(stacks, maxStackHeight);
        foreach (var stack in allStacks)
        {
            if (stack != null)
            {
                stack.SetMaxStackHeight(stackHeight);
                stack.ForceIncompleteState();
            }
        }
        levelComplete = false;
        selectedStack = null;
        isPressing = false;
        
        currentLevelNumber = levelNumber;
        optimalMoves = levelData.optimalMoves;

        BuildRequiredStyleCounts(levelData);
        
        // Undo/Hint reset (can't undo moves from before save)
        undoStack.Clear();
        solutionSteps = levelData.solvingSteps != null ? new List<MoveStep>(levelData.solvingSteps) : new();
        nextHintIndex = 0;
        
        // Rearrange floors to match saved state
        RearrangeStacksFromState(savedState);
        
        OnMoveCountChanged?.Invoke(moveCount, moveLimit);
        OnUndoCountChanged?.Invoke(SaveSystem.GetFreeUndos());
        OnHintCountChanged?.Invoke(SaveSystem.GetFreeHints());
        OnCoinsChanged?.Invoke(SaveSystem.GetCoins());
        
        Debug.Log($"<color=green>Level {currentLevelNumber} RESTORED at move {moveCount}/{moveLimit} (Capacity: {stackHeight})</color>");
    }

    /// <summary>
    /// Resolve movable-floor capacity for this level.
    /// Uses a safe max of loader-provided value and style-frequency inference from spawned stacks.
    /// </summary>
    private int ResolveMovableStackHeight(List<BuildingStack> stacks, int fallback)
    {
        int inferred = 0;
        Dictionary<string, int> movableStyleCounts = new();

        if (stacks != null)
        {
            foreach (var stack in stacks)
            {
                if (stack == null) continue;

                var names = stack.GetFloorStyleNames();
                int start = Mathf.Clamp(stack.GroundFloorCount, 0, names.Count);

                for (int i = start; i < names.Count; i++)
                {
                    string styleName = names[i];
                    if (string.IsNullOrEmpty(styleName)) continue;

                    if (!movableStyleCounts.ContainsKey(styleName))
                        movableStyleCounts[styleName] = 0;

                    movableStyleCounts[styleName]++;
                }
            }
        }

        foreach (var kv in movableStyleCounts)
            inferred = Mathf.Max(inferred, kv.Value);

        int resolved = Mathf.Max(fallback, inferred);
        return resolved > 0 ? resolved : 3;
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
            // Can't select empty stacks, ground-only stacks, or completed stacks
            if (tappedStack.MovableFloorCount > 0 && !tappedStack.IsCompleted)
            {
                SelectStack(tappedStack);
            }
            else if (tappedStack.IsCompleted)
            {
                Debug.Log("<color=gray>Stack is complete — can't take floors</color>");
            }
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
        // Validation 1: Source has movable floors
        if (from.MovableFloorCount == 0)
        {
            ShowError(from);
            return;
        }
        
        // Validation 2: Move limit not exceeded
        if (moveCount >= moveLimit)
        {
            DeselectStack();
            
            // Show out-of-moves popup instead of just erroring
            if (outOfMovesUI != null)
            {
                outOfMovesUI.Show(this);
            }
            else
            {
                ShowError(from);
                OnMoveFailed?.Invoke();
            }
            Debug.Log("<color=red>Move limit reached! Showing recovery options.</color>");
            return;
        }
        
        // Validation 3: Target can receive this floor type
        BuildingStyleSO movingStyle = from.GetTopFloorStyle();
        if (!to.CanReceiveFloor(stackHeight, movingStyle))
        {
            ShowError(to);
            OnMoveFailed?.Invoke();
            
            if (to.IsCompleted)
                Debug.Log("<color=red>Can't place on completed stack!</color>");
            else if (to.GetTopFloorStyle() != null && to.GetTopFloorStyle() != movingStyle)
                Debug.Log($"<color=red>Wrong type! Top is {to.GetTopFloorStyle().buildingName}, placing {movingStyle.buildingName}</color>");
            else
                Debug.Log($"<color=red>Stack full! {to.FloorCount}/{stackHeight}</color>");
            
            return;
        }
        
        // Execute move
        ExecuteMove(from, to);
    }
    
    private void ExecuteMove(BuildingStack from, BuildingStack to)
    {
        // Record undo
        int fromIdx = allStacks.IndexOf(from);
        int toIdx = allStacks.IndexOf(to);
        undoStack.Push(new UndoRecord { fromIndex = fromIdx, toIndex = toIdx });
        
        // Get floor data from source
        var floorData = from.RemoveTopFloor();
        if (floorData.floorObject == null) return;
        
        // Deselect before animation
        DeselectStack();
        
        // Add to target (handles positioning)
        to.AddFloor(floorData.floorObject, floorData.style, animationDuration);
        if (AudioManager.Instance != null) AudioManager.Instance.PlayFloorPlace();
        
        // Update move counter
        moveCount++;
        OnMoveCountChanged?.Invoke(moveCount, moveLimit);
        
        Debug.Log($"<color=cyan>Move {moveCount}/{moveLimit}</color>");
        
        // Auto-save after every move
        SaveInProgressState();
        
        // Check win condition
        CheckWinCondition();
    }
    
    private void ShowError(BuildingStack stack)
    {
        stack.FlashColor(errorColor, 0.2f);
        if (AudioManager.Instance != null) AudioManager.Instance.PlayMoveFailed();
    }
    
    private void CheckWinCondition()
    {
        if (!IsLevelSolvedStrict())
            return;
        
        // All stacks complete!
        levelComplete = true;
        OnLevelComplete?.Invoke();
        if (AudioManager.Instance != null) AudioManager.Instance.PlayLevelComplete();
        
        Debug.Log($"<color=green>🎉 Level Complete in {moveCount} moves!</color>");
        
        // Notify GameManager (handles save + UI)
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnLevelComplete(currentLevelNumber, moveCount, optimalMoves);
        }
    }

    /// <summary>
    /// Build strict win requirements from authored level data.
    /// For each style, count how many movable pieces exist in the puzzle.
    /// </summary>
    private void BuildRequiredStyleCounts(LevelDataSO levelData)
    {
        requiredMovableByStyle.Clear();

        if (levelData == null || levelData.slots == null) return;

        foreach (var slot in levelData.slots)
        {
            if (slot == null || slot.isLocked || slot.floorStyles == null) continue;

            for (int i = 0; i < slot.floorStyles.Count; i++)
            {
                var style = slot.floorStyles[i];
                if (style == null || string.IsNullOrEmpty(style.buildingName)) continue;

                if (!requiredMovableByStyle.ContainsKey(style.buildingName))
                    requiredMovableByStyle[style.buildingName] = 0;

                requiredMovableByStyle[style.buildingName]++;
            }
        }
    }

    /// <summary>
    /// Strict solved-state check:
    /// 1) For each style, exactly one GROUNDED stack contains all movable floors for that style.
    /// 2) Any other stack must have zero movable floors.
    /// This prevents premature completion when a block is still left to place.
    /// </summary>
    private bool IsLevelSolvedStrict()
    {
        // Fallback for malformed/custom levels with no style data
        if (requiredMovableByStyle == null || requiredMovableByStyle.Count == 0)
        {
            foreach (var stack in allStacks)
            {
                if (!stack.IsComplete())
                    return false;
            }
            return true;
        }

        HashSet<BuildingStack> matchedSolvedStacks = new();

        foreach (var kv in requiredMovableByStyle)
        {
            string styleName = kv.Key;
            int requiredMovable = kv.Value;

            List<BuildingStack> matchingStacks = new();

            foreach (var stack in allStacks)
            {
                if (stack == null) continue;
                if (stack.GroundFloorCount <= 0) continue; // must be a grounded building

                var names = stack.GetFloorStyleNames();
                if (names == null || names.Count == 0) continue;

                // Must have correct total count (ground + all movable of that style)
                if (stack.MovableFloorCount != requiredMovable) continue;
                if (names.Count != stack.GroundFloorCount + requiredMovable) continue;

                // Ground and all movables must be this style
                bool allSame = true;
                for (int i = 0; i < names.Count; i++)
                {
                    if (names[i] != styleName)
                    {
                        allSame = false;
                        break;
                    }
                }

                if (allSame)
                    matchingStacks.Add(stack);
            }

            // Must be exactly one final stack per style
            if (matchingStacks.Count != 1)
                return false;

            matchedSolvedStacks.Add(matchingStacks[0]);
        }

        // Any stack not used as a final style stack must be empty of movable floors
        foreach (var stack in allStacks)
        {
            if (stack == null) continue;
            if (matchedSolvedStacks.Contains(stack)) continue;

            if (stack.MovableFloorCount > 0)
                return false;
        }

        return true;
    }
    
    // ========================================
    // SAVE / RESTORE STATE
    // ========================================
    
    /// <summary>
    /// Capture current stack state and save to PlayerPrefs.
    /// Called after every move for seamless resume.
    /// </summary>
    private void SaveInProgressState()
    {
        LevelStateData state = new LevelStateData
        {
            levelNumber = currentLevelNumber,
            moveCount = moveCount
        };
        
        for (int i = 0; i < allStacks.Count; i++)
        {
            StackStateData stackState = new StackStateData
            {
                stackIndex = i,
                floorStyleNames = allStacks[i].GetFloorStyleNames()
            };
            state.stacks.Add(stackState);
        }
        
        string json = JsonUtility.ToJson(state);
        SaveSystem.SaveInProgressGame(currentLevelNumber, json, moveCount);
    }
    
    /// <summary>
    /// Rearrange floor GameObjects to match a saved state.
    /// Steps: remove all movable floors from stacks, then redistribute.
    /// </summary>
    private void RearrangeStacksFromState(LevelStateData savedState)
    {
        // 1. Collect all movable floors from all stacks
        List<(GameObject obj, BuildingStyleSO style)> allFloors = new();
        
        foreach (var stack in allStacks)
        {
            while (stack.MovableFloorCount > 0)
            {
                var floor = stack.RemoveTopFloor();
                if (floor.floorObject != null)
                    allFloors.Add(floor);
            }
        }
        
        // 2. Build a lookup: styleName → list of floor objects
        Dictionary<string, Queue<(GameObject obj, BuildingStyleSO style)>> floorPool = new();
        foreach (var floor in allFloors)
        {
            string key = floor.style.buildingName;
            if (!floorPool.ContainsKey(key))
                floorPool[key] = new Queue<(GameObject, BuildingStyleSO)>();
            floorPool[key].Enqueue(floor);
        }
        
        // 3. Redistribute floors according to saved state
        for (int i = 0; i < savedState.stacks.Count && i < allStacks.Count; i++)
        {
            var stackState = savedState.stacks[i];
            var stack = allStacks[stackState.stackIndex < allStacks.Count ? stackState.stackIndex : i];
            
            // Skip ground floor entries (they're already in place)
            int groundCount = stack.GroundFloorCount;
            
            for (int f = groundCount; f < stackState.floorStyleNames.Count; f++)
            {
                string styleName = stackState.floorStyleNames[f];
                
                if (floorPool.ContainsKey(styleName) && floorPool[styleName].Count > 0)
                {
                    var floor = floorPool[styleName].Dequeue();
                    stack.AddFloor(floor.obj, floor.style, 0f); // instant, no animation
                }
                else
                {
                    Debug.LogWarning($"Missing floor for style '{styleName}' during restore!");
                }
            }
        }
        
        Debug.Log($"<color=green>Restored {allFloors.Count} floors from saved state</color>");
    }
    
    // ========================================
    // PUBLIC API
    // ========================================
    
    public int GetMoveCount() => moveCount;
    public int GetMoveLimit() => moveLimit;
    public bool IsLevelComplete() => levelComplete;
    
    /// <summary>
    /// Add extra moves (from ad reward or coin purchase).
    /// Called by OutOfMovesUI.
    /// </summary>
    public void AddExtraMoves(int count)
    {
        moveLimit += count;
        OnMoveCountChanged?.Invoke(moveCount, moveLimit);
        SaveInProgressState();
        Debug.Log($"<color=green>+{count} extra moves! New limit: {moveLimit}</color>");
    }
    
    /// <summary>
    /// Check if player earned perfect clear (solved in optimal moves + bonus range)
    /// </summary>
    public bool IsPerfectClear(int optimalMoves)
    {
        return moveCount <= optimalMoves + 2;
    }
    
    // ========================================
    // UNDO SYSTEM
    // ========================================
    
    /// <summary>
    /// Undo the last move. Costs one free undo or 50 coins.
    /// </summary>
    public bool TryUndo()
    {
        if (levelComplete || undoStack.Count == 0) return false;
        
        // Check consumable availability
        bool hasFree = SaveSystem.UseFreeUndo();
        if (!hasFree)
        {
            // Try spending coins
            if (!SaveSystem.SpendCoins(75))
            {
                // Offer ad as last resort
                if (AdManager.Instance != null && AdManager.Instance.IsRewardedAdReady())
                {
                    AdManager.Instance.ShowFreeUndosAd(() =>
                    {
                        SaveSystem.AddFreeUndos(3);
                        OnUndoCountChanged?.Invoke(SaveSystem.GetFreeUndos());
                        // Now execute the undo since player earned it
                        TryUndo();
                    });
                }
                else
                {
                    Debug.Log("<color=red>No undos available! No free undos, coins, or ads.</color>");
                }
                return false;
            }
            OnCoinsChanged?.Invoke(SaveSystem.GetCoins());
        }
        
        // Pop last move
        var record = undoStack.Pop();
        BuildingStack from = allStacks[record.toIndex];  // The floor is now here
        BuildingStack to = allStacks[record.fromIndex];    // Move it back here
        
        // Execute reverse move (instant, no animation delay)
        var floorData = from.RemoveTopFloor();
        if (floorData.floorObject != null)
        {
            to.AddFloor(floorData.floorObject, floorData.style, 0.15f);
        }
        
        // Decrement move count
        moveCount = Mathf.Max(0, moveCount - 1);
        OnMoveCountChanged?.Invoke(moveCount, moveLimit);
        OnUndoCountChanged?.Invoke(SaveSystem.GetFreeUndos());
        
        DeselectStack();
        SaveInProgressState();
        
        Debug.Log($"<color=yellow>Undo! Move count: {moveCount}/{moveLimit} | Undos left: {SaveSystem.GetFreeUndos()}</color>");
        return true;
    }
    
    // ========================================
    // HINT SYSTEM
    // ========================================
    
    /// <summary>
    /// Show a hint by highlighting the source and target stacks.
    /// Costs one free hint or 100 coins.
    /// </summary>
    public bool TryShowHint()
    {
        if (levelComplete) return false;
        
        // Find a valid hint from current state
        BuildingStack hintFrom = null;
        BuildingStack hintTo = null;
        
        foreach (var from in allStacks)
        {
            if (from.MovableFloorCount == 0) continue;
            
            var topStyle = from.GetTopFloorStyle();
            if (topStyle == null) continue;
            
            foreach (var to in allStacks)
            {
                if (to == from) continue;
                if (!to.CanReceiveFloor(stackHeight, topStyle)) continue;
                
                hintFrom = from;
                hintTo = to;
                
                // Prefer targets that already have matching floors
                if (to.MovableFloorCount > 0)
                    goto FoundHint;
            }
        }
        
        FoundHint:
        if (hintFrom == null || hintTo == null)
        {
            Debug.Log("<color=red>No valid hint available!</color>");
            return false;
        }
        
        // Check consumable availability
        bool hasFree = SaveSystem.UseFreeHint();
        if (!hasFree)
        {
            if (!SaveSystem.SpendCoins(150))
            {
                // Offer ad as last resort
                if (AdManager.Instance != null && AdManager.Instance.IsRewardedAdReady())
                {
                    AdManager.Instance.ShowFreeHintAd(() =>
                    {
                        SaveSystem.AddFreeHints(1);
                        OnHintCountChanged?.Invoke(SaveSystem.GetFreeHints());
                        TryShowHint();
                    });
                }
                else
                {
                    Debug.Log("<color=red>No hints available! No free hints, coins, or ads.</color>");
                }
                return false;
            }
            OnCoinsChanged?.Invoke(SaveSystem.GetCoins());
        }
        
        // Highlight the hint stacks
        hintFrom.FlashColor(new Color(0.3f, 1f, 0.3f, 1f), 1.0f); // Green flash on source
        hintTo.FlashColor(new Color(0.3f, 0.7f, 1f, 1f), 1.0f);   // Blue flash on target
        
        OnHintCountChanged?.Invoke(SaveSystem.GetFreeHints());
        
        Debug.Log($"<color=green>Hint: Move from stack {allStacks.IndexOf(hintFrom)} to {allStacks.IndexOf(hintTo)}</color>");
        return true;
    }
}
