using System;
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
    public int wrongMoveCount;
    public List<StackStateData> stacks = new();
}

/// <summary>
/// Central gameplay controller for City Sort.
/// Handles stack selection, floor movement, win detection, and auto-save.
/// </summary>
public class GameplayManager : MonoBehaviour
{
    public static GameplayManager Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    [Header("References")]
    [SerializeField] private LevelLoader levelLoader;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private OutOfMovesUI outOfMovesUI;

    [Header("Hook")]
    [SerializeField] private hookfollow hookController; // controls visual hook
    
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
    private int wrongMoveCount;
    private int moveLimit;
    private int stackHeight;
    private bool isAnimating;
    private bool levelComplete;
    
    // Level info
    private int currentLevelNumber;
    private int optimalMoves;
    private LevelDataSO currentLevelData;
    
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
    private bool unlimitedHintsEnabled; // Debug mode for unlimited hints
    private Dictionary<Vector2Int, BuildingStack> gridToStackMap = new(); // Maps grid positions to stacks
    
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
        currentLevelData = levelData;
        allStacks = stacks;
        moveCount = 0;
        wrongMoveCount = 0;
        moveLimit = levelData.playerMoveLimit;
        stackHeight = ResolveMovableStackHeight(stacks, maxStackHeight);
        foreach (var stack in allStacks)
        {
            if (stack != null)
            {
                stack.SetMaxStackHeight(stackHeight);
                stack.CheckAndApplyInitialCompletionState();
            }
        }
        levelComplete = false;
        selectedStack = null;
        isPressing = false;
        
        currentLevelNumber = levelNumber;
        optimalMoves = levelData.optimalMoves;
        
        // Undo/Hint reset
        undoStack.Clear();
        solutionSteps = levelData.solvingSteps != null ? new List<MoveStep>(levelData.solvingSteps) : new();
        nextHintIndex = 0;
        
        // Build grid-to-stack mapping for hints
        BuildGridToStackMap();
        
        // Dynamically set free undos and hints based on level wise pattern (2, 3, 1 repeating)
        int dynamicCount = 1;
        int pattern = levelNumber % 3;
        if (pattern == 1) dynamicCount = 2;
        else if (pattern == 2) dynamicCount = 3;
        
        SaveSystem.SetFreeUndos(dynamicCount);
        SaveSystem.SetFreeHints(dynamicCount);
        
        OnMoveCountChanged?.Invoke(moveCount, moveLimit);
        OnUndoCountChanged?.Invoke(SaveSystem.GetFreeUndos());
        OnHintCountChanged?.Invoke(SaveSystem.GetFreeHints());
        OnCoinsChanged?.Invoke(SaveSystem.GetCoins());
        
        // Save initial state
        SaveInProgressState();
        
        Debug.Log($"<color=cyan>Level {currentLevelNumber} initialized. Optimal: {optimalMoves}, Limit: {moveLimit}, Capacity: {stackHeight}, Stacks: {allStacks.Count}</color>");

        if (hookController != null)
        {
            StartCoroutine(PositionHookOnStartFrame());
        }
    }

    /// <summary>
    /// Restore gameplay from a saved in-progress state.
    /// Called after LevelLoader spawns the default level layout.
    /// </summary>
    public void RestoreFromSave(LevelDataSO levelData, List<BuildingStack> stacks, int maxStackHeight, 
                                 int levelNumber, LevelStateData savedState)
    {
        currentLevelData = levelData;
        allStacks = stacks;
        moveCount = savedState.moveCount;
        wrongMoveCount = savedState.wrongMoveCount;
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
        
        // Undo/Hint reset (can't undo moves from before save)
        undoStack.Clear();
        solutionSteps = levelData.solvingSteps != null ? new List<MoveStep>(levelData.solvingSteps) : new();
        nextHintIndex = 0;
        
        // Build grid-to-stack mapping for hints
        BuildGridToStackMap();
        
        // Rearrange floors to match saved state
        RearrangeStacksFromState(savedState);
        
        // Ensure any completed buildings after restore have their lights and completion visuals set instantly
        foreach (var stack in allStacks)
        {
            if (stack != null)
            {
                stack.CheckAndApplyInitialCompletionState();
            }
        }
        
        OnMoveCountChanged?.Invoke(moveCount, moveLimit);
        OnUndoCountChanged?.Invoke(SaveSystem.GetFreeUndos());
        OnHintCountChanged?.Invoke(SaveSystem.GetFreeHints());
        OnCoinsChanged?.Invoke(SaveSystem.GetCoins());
        
        Debug.Log($"<color=green>Level {currentLevelNumber} RESTORED at move {moveCount}/{moveLimit} (Capacity: {stackHeight})</color>");

        if (hookController != null)
        {
            StartCoroutine(PositionHookOnStartFrame());
        }
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
        
        // Check for 'H' key to toggle unlimited hints
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.hKey.wasPressedThisFrame)
        {
            unlimitedHintsEnabled = true;
            Debug.Log("<color=yellow>H pressed: unlimited hints enabled and showing hint.</color>");
            if (!TryShowHint())
            {
                Debug.LogWarning("<color=red>H pressed but no hint could be shown.</color>");
            }
        }
        
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
        
        // Tapped empty space or non-stack - deselect smoothly
        if (selectedStack != null)
        {
            StartCoroutine(AnimateDeselectStackFlow());
        }
    }
    
    private void ProcessStackTap(BuildingStack tappedStack)
    {
        // Case 1: Nothing selected - try to select this stack smoothly
        if (selectedStack == null)
        {
            // Can't select empty or ground-only stacks
            if (tappedStack.MovableFloorCount > 0)
            {
                StartCoroutine(AnimateSelectStackFlow(tappedStack));
            }
            return;
        }
        
        // Case 2: Same stack tapped - deselect smoothly
        if (tappedStack == selectedStack)
        {
            StartCoroutine(AnimateDeselectStackFlow());
            return;
        }
        
        // Case 3: Different stack tapped - try to move
        TryMoveFloor(selectedStack, tappedStack);
    }
    
    private void SelectStack(BuildingStack stack)
    {
        selectedStack = stack;
        stack.SetSelected(true, selectedColor);
        // Make hook follow the selected stack's top floor
        if (hookController != null)
            hookController.FollowStack(stack);
        Debug.Log($"<color=yellow>Selected stack with {stack.FloorCount} floors</color>");
    }
    
    private void DeselectStack(bool animateHook = true)
    {
        if (selectedStack != null)
        {
            if (hookController != null)
                hookController.StopFollow();

            BuildingStack stackToDrop = selectedStack;

            // Instantly reset top floor elevation
            stackToDrop.SetTopFloorElevationInstant(false);
            selectedStack.SetSelected(false, Color.white);
            selectedStack = null;

            if (animateHook && hookController != null)
            {
                hookController.MoveHookToStack(stackToDrop, 0.15f);
            }
            
            Debug.Log("<color=gray>Deselected</color>");
        }
    }
    
    private void TryMoveFloor(BuildingStack from, BuildingStack to)
    {
        // Validation 1: Source has movable floors
        if (from.MovableFloorCount == 0)
        {
            ShowError(from);
            RegisterWrongMove();
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
                RegisterWrongMove();
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
            RegisterWrongMove();
            
            if (to.GetTopFloorStyle() != null && to.GetTopFloorStyle() != movingStyle)
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
        
        // If we have a hook controller, animate hook to source then perform the move in callback
        if (hookController != null)
        {
            isAnimating = true;

            hookController.MoveHookToStack(from, 0f, () =>
            {
                // Get floor data from source
                var floorData = from.RemoveTopFloor();
                if (floorData.floorObject == null)
                {
                    isAnimating = false;
                    return;
                }

                // Stop hook following during transit
                hookController.StopFollow();

                // Parent the floor to the hook so it moves horizontally with the crane
                GameObject floorObj = floorData.floorObject;
                floorObj.transform.SetParent(hookController.hook);
                
                // Position the floor slightly below the hook (compensate for hookOffset y=2)
                floorObj.transform.localPosition = new Vector3(0f, -hookController.hookOffset.y, 0f);

                // Move hook quickly to the elevated position above target stack
                Transform targetTop = FindTopFloorTransformOfStack(to);
                Vector3 targetBasePos = (targetTop != null ? targetTop.position : to.transform.position);
                Vector3 targetElevatedPos = targetBasePos + hookController.hookOffset + new Vector3(0f, 8f, 0f);

                // We smoothly move hook to this elevated position over 0.2s (fast horizontal transit!)
                StartCoroutine(MoveHookToPositionCoroutine(targetElevatedPos, 0.2f, () =>
                {
                    // Arrived high up above 'to' stack! Now perform the drop.
                    // First unparent from hook
                    floorObj.transform.SetParent(null);

                    // Add to target stack (which handles parenting and animates it down over animationDuration)
                    to.AddFloor(floorObj, floorData.style, animationDuration);
                    if (AudioManager.Instance != null) AudioManager.Instance.PlayFloorPlace();

                    // Update move counter
                    moveCount++;
                    OnMoveCountChanged?.Invoke(moveCount, moveLimit);

                    // Auto-save
                    SaveInProgressState();

                    // Smoothly move the hook down to follow the dropped block to its landing spot
                    Vector3 finalHookPos = to.GetTopFloorWorldPosition() + hookController.hookOffset;
                    StartCoroutine(MoveHookToPositionCoroutine(finalHookPos, animationDuration, () =>
                    {
                        // Now that the hook is down, clear selection and release animation block
                        DeselectStack(false);
                        isAnimating = false;
                    }));

                    // Check win condition
                    CheckWinCondition();
                }));
            });
        }
        else
        {
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
    }
    
    private void ShowError(BuildingStack stack)
    {
        stack.FlashColor(errorColor, 0.2f);
        if (AudioManager.Instance != null) AudioManager.Instance.PlayMoveFailed();
    }
    
    private void CheckWinCondition()
    {
        foreach (var stack in allStacks)
        {
            if (stack == null) continue;
            if (!stack.IsComplete()) return;
        }
        
        // All stacks complete!
        levelComplete = true;
        OnLevelComplete?.Invoke();
        if (AudioManager.Instance != null) AudioManager.Instance.PlayLevelComplete();
        
        Debug.Log($"<color=green>🎉 Level Complete in {moveCount} moves!</color>");
        
        // Notify GameManager (handles save + UI)
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnLevelComplete(currentLevelNumber, moveCount, optimalMoves, wrongMoveCount);
        }
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
            moveCount = moveCount,
            wrongMoveCount = wrongMoveCount
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

    private void RegisterWrongMove()
    {
        wrongMoveCount++;
        Debug.Log($"<color=yellow>Wrong move {wrongMoveCount}</color>");
        SaveInProgressState();
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
    /// Returns all building stacks in the current level.
    /// Used by celebration VFX to position particles on buildings.
    /// </summary>
    public List<BuildingStack> GetAllStacks() => allStacks;
    
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
    /// Undo the last move. Costs one free undo or 75 coins.
    /// </summary>
    public bool TryUndo()
    {
        if (levelComplete || isAnimating || undoStack.Count == 0) return false;
        
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
        
        // If we have a hook controller, animate hook to source (from) then perform the move in callback
        if (hookController != null)
        {
            isAnimating = true;

            // Move hook to the current stack (from)
            hookController.MoveHookToStack(from, 0f, () =>
            {
                // Get floor data from source (from)
                var floorData = from.RemoveTopFloor();
                if (floorData.floorObject == null)
                {
                    isAnimating = false;
                    return;
                }

                // Stop hook following during transit
                hookController.StopFollow();

                // Parent the floor to the hook so it moves horizontally with the crane
                GameObject floorObj = floorData.floorObject;
                floorObj.transform.SetParent(hookController.hook);
                
                // Position the floor slightly below the hook (compensate for hookOffset y=2)
                floorObj.transform.localPosition = new Vector3(0f, -hookController.hookOffset.y, 0f);

                // Move hook quickly to the elevated position above target stack (to)
                Transform targetTop = FindTopFloorTransformOfStack(to);
                Vector3 targetBasePos = (targetTop != null ? targetTop.position : to.transform.position);
                Vector3 targetElevatedPos = targetBasePos + hookController.hookOffset + new Vector3(0f, 8f, 0f);

                // We smoothly move hook to this elevated position over 0.2s (fast horizontal transit!)
                StartCoroutine(MoveHookToPositionCoroutine(targetElevatedPos, 0.2f, () =>
                {
                    // Arrived high up above 'to' stack! Now perform the drop.
                    // First unparent from hook
                    floorObj.transform.SetParent(null);

                    // Add to target stack (which handles parenting and animates it down over animationDuration)
                    to.AddFloor(floorObj, floorData.style, animationDuration);
                    if (AudioManager.Instance != null) AudioManager.Instance.PlayFloorPlace();

                    // Decrement move count
                    moveCount = Mathf.Max(0, moveCount - 1);
                    OnMoveCountChanged?.Invoke(moveCount, moveLimit);
                    OnUndoCountChanged?.Invoke(SaveSystem.GetFreeUndos());

                    // Auto-save
                    SaveInProgressState();

                    // Smoothly move the hook down to follow the dropped block to its landing spot
                    Vector3 finalHookPos = to.GetTopFloorWorldPosition() + hookController.hookOffset;
                    StartCoroutine(MoveHookToPositionCoroutine(finalHookPos, animationDuration, () =>
                    {
                        // Now that the hook is down, clear selection and release animation block
                        DeselectStack(false);
                        isAnimating = false;
                    }));
                }));
            });
        }
        else
        {
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
            
            DeselectStack(false);
            SaveInProgressState();
        }
        
        Debug.Log($"<color=yellow>Undo! Move count: {moveCount}/{moveLimit} | Undos left: {SaveSystem.GetFreeUndos()}</color>");
        return true;
    }
    
    // ========================================
    // HELPER METHODS
    // ========================================
    
    /// <summary>
    /// Build a mapping from grid positions to BuildingStack objects for hint system.
    /// </summary>
    private void BuildGridToStackMap()
    {
        gridToStackMap.Clear();
        foreach (var stack in allStacks)
        {
            if (stack != null)
            {
                gridToStackMap[stack.GridPosition] = stack;
            }
        }
        Debug.Log($"<color=cyan>Grid-to-stack map built with {gridToStackMap.Count} stacks</color>");
    }

    /// <summary>
    /// Snapshot the current gameplay stacks into SlotData list for the solver.
    /// Preserves original slot target styles from `currentLevelData` when available.
    /// </summary>
    private List<SlotData> BuildCurrentLevelState()
    {
        var slots = new List<SlotData>();

        foreach (var stack in allStacks)
        {
            if (stack == null) continue;

            var slot = new SlotData();
            slot.gridPos = stack.GridPosition;
            slot.isLocked = false;
            slot.isEmpty = stack.IsEmpty;
            slot.floorStyles = stack.GetFloorStyles() ?? new List<BuildingStyleSO>();

            // If we have original level data, copy the goal buildingStyle for this grid
            if (currentLevelData != null && currentLevelData.slots != null)
            {
                var orig = currentLevelData.slots.FirstOrDefault(s => s.gridPos == slot.gridPos);
                if (orig != null)
                {
                    slot.buildingStyle = orig.buildingStyle;
                    slot.isLocked = orig.isLocked;
                }
            }

            slots.Add(slot);
        }

        return slots;
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
        Debug.Log("<color=cyan>TryShowHint called</color>");

        if (levelComplete) return false;

        BuildingStack hintFrom = null;
        BuildingStack hintTo = null;

        // 1) Runtime solver-first: build current slots and ask PuzzleSolver for shortest move
        try
        {
            var currentSlots = BuildCurrentLevelState();
            var solver = new PuzzleSolver(stackHeight);
            var solution = solver.FindShortestSolution(currentSlots, 100);

            if (solution != null && solution.Count > 0)
            {
                MoveStep next = solution[0];
                gridToStackMap.TryGetValue(next.fromGridPos, out hintFrom);
                gridToStackMap.TryGetValue(next.toGridPos, out hintTo);
                if (hintFrom != null && hintTo != null)
                {
                    Debug.Log($"<color=cyan>Runtime solver hint: {next.fromGridPos} -> {next.toGridPos}</color>");
                }
                else
                {
                    Debug.Log("<color=yellow>Runtime solver produced step but mapping failed.</color>");
                }
            }
            else
            {
                Debug.Log("<color=yellow>Runtime solver found no move, falling back to precomputed/fallback.</color>");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error running runtime solver: {ex}");
        }

        // 2) If runtime solver didn't yield a valid mapping, try precomputed solution steps
        if (hintFrom == null || hintTo == null)
        {
            if (solutionSteps != null && solutionSteps.Count > 0)
            {
                for (int i = nextHintIndex; i < solutionSteps.Count; i++)
                {
                    MoveStep step = solutionSteps[i];
                    if (gridToStackMap.TryGetValue(step.fromGridPos, out BuildingStack fromStack) &&
                        gridToStackMap.TryGetValue(step.toGridPos, out BuildingStack toStack))
                    {
                        if (fromStack.MovableFloorCount > 0)
                        {
                            var topStyle = fromStack.GetTopFloorStyle();
                            if (topStyle != null && toStack.CanReceiveFloor(stackHeight, topStyle))
                            {
                                hintFrom = fromStack;
                                hintTo = toStack;
                                nextHintIndex = i + 1;
                                break;
                            }
                        }
                    }
                }
            }
        }

        if (hintFrom == null || hintTo == null)
        {
            Debug.Log("<color=red>No valid hint available!</color>");
            return false;
        }
        
        // Check consumable availability (skip if unlimited hints enabled)
        if (!unlimitedHintsEnabled)
        {
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
        }
        
        // Highlight the hint stacks
        hintFrom.FlashColor(new Color(0.3f, 0.7f, 1f, 1f), 1.5f); // Blue flash on source
        hintTo.FlashColor(new Color(0.3f, 1f, 0.3f, 1f), 1.5f);   // Green flash on target
        
        OnHintCountChanged?.Invoke(SaveSystem.GetFreeHints());
        
        Debug.Log($"<color=green>Hint: Move from stack {allStacks.IndexOf(hintFrom)} to {allStacks.IndexOf(hintTo)}</color>");
        return true;
    }

    // ========================================
    // HOOK STARTUP CONFIGURATION
    // ========================================

    /// <summary>
    /// Coroutine to position the crane hook on the center stack's top floor on the start frame.
    /// This ensures we override any default hook positioning from other components (e.g., CraneBuilder).
    /// </summary>
    private System.Collections.IEnumerator PositionHookOnStartFrame()
    {
        // Position it instantly now
        PositionHookOnCenterStack();

        // Also position it on the next frame after Start() has executed on all objects
        yield return null;
        PositionHookOnCenterStack();
    }

    /// <summary>
    /// Finds the building stack closest to the geometric center of all stacks,
    /// and positions the crane hook over its top floor.
    /// </summary>
    private void PositionHookOnCenterStack()
    {
        if (hookController == null || allStacks == null || allStacks.Count == 0) return;

        // Calculate the geometric center of all stacks
        Vector3 center = Vector3.zero;
        int validCount = 0;
        foreach (var stack in allStacks)
        {
            if (stack != null)
            {
                center += stack.transform.position;
                validCount++;
            }
        }

        if (validCount == 0) return;
        center /= validCount;

        // Find the stack closest to the center
        BuildingStack centerStack = null;
        float minDistance = float.MaxValue;
        foreach (var stack in allStacks)
        {
            if (stack == null) continue;
            float dist = Vector3.Distance(stack.transform.position, center);
            if (dist < minDistance)
            {
                minDistance = dist;
                centerStack = stack;
            }
        }

        if (centerStack != null)
        {
            // Position the hook instantly
            hookController.MoveHookToStack(centerStack, 0f);
        }
    }

    // ========================================
    // SMOOTH ELEVATION & TRANSIT ANIMATIONS
    // ========================================

    private Transform FindTopFloorTransformOfStack(BuildingStack stack)
    {
        if (stack == null) return null;
        Transform stackTransform = stack.transform;
        Transform top = null;
        float maxY = float.MinValue;

        for (int i = 0; i < stackTransform.childCount; i++)
        {
            Transform child = stackTransform.GetChild(i);
            if (!child.gameObject.activeInHierarchy) continue;

            float y = child.position.y;
            if (y > maxY)
            {
                maxY = y;
                top = child;
            }
        }

        return top;
    }

    private System.Collections.IEnumerator MoveHookToPositionCoroutine(Vector3 destination, float duration, System.Action onComplete)
    {
        if (hookController == null || hookController.hook == null)
        {
            onComplete?.Invoke();
            yield break;
        }

        Transform hook = hookController.hook;
        Vector3 start = hook.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t); // Smoothstep
            hook.position = Vector3.Lerp(start, destination, t);
            yield return null;
        }

        hook.position = destination;
        onComplete?.Invoke();
    }

    private System.Collections.IEnumerator AnimateSelectStackFlow(BuildingStack stack)
    {
        isAnimating = true;

        if (hookController != null)
        {
            Transform top = FindTopFloorTransformOfStack(stack);
            Vector3 targetPos = (top != null ? top.position : stack.transform.position) + hookController.hookOffset;

            bool done = false;
            StartCoroutine(MoveHookToPositionCoroutine(targetPos, 0.25f, () => done = true));
            while (!done) yield return null;
        }

        SelectStack(stack);

        bool elevateDone = false;
        stack.AnimateTopFloorElevation(true, 0.25f, () => elevateDone = true);
        while (!elevateDone) yield return null;

        isAnimating = false;
    }

    private System.Collections.IEnumerator AnimateDeselectStackFlow()
    {
        if (selectedStack == null) yield break;

        isAnimating = true;

        BuildingStack stackToDrop = selectedStack;

        bool lowerDone = false;
        stackToDrop.AnimateTopFloorElevation(false, 0.25f, () => lowerDone = true);
        while (!lowerDone) yield return null;

        DeselectStack(false);

        isAnimating = false;
    }

    /// <summary>
    /// Triggers a modern, satisfying camera screen shake during block placement snaps.
    /// </summary>
    public void TriggerCameraShake(float duration = 0.15f, float magnitude = 0.04f)
    {
        if (mainCamera != null)
        {
            StartCoroutine(CameraShakeCoroutine(duration, magnitude));
        }
    }

    private System.Collections.IEnumerator CameraShakeCoroutine(float duration, float magnitude)
    {
        Vector3 originalPos = mainCamera.transform.localPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float x = UnityEngine.Random.Range(-1f, 1f) * magnitude;
            float y = UnityEngine.Random.Range(-1f, 1f) * magnitude;

            mainCamera.transform.localPosition = new Vector3(originalPos.x + x, originalPos.y + y, originalPos.z);

            elapsed += Time.deltaTime;
            yield return null;
        }

        mainCamera.transform.localPosition = originalPos;
    }
}
