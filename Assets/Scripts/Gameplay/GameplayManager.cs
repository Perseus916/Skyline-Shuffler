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
    public int gridX = -1; // Default to -1 so we can detect old saves
    public int gridY = -1;
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
    public UnityEvent<int> OnLockedBlockCountChanged; // remaining locked blocks
    
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

        // IMPORTANT: do NOT dynamically modify hints/undos here.
        // Hints/undos are only granted by the Shop (and the initial save values).

        OnMoveCountChanged?.Invoke(moveCount, moveLimit);
        OnUndoCountChanged?.Invoke(SaveSystem.GetFreeUndos());
        OnHintCountChanged?.Invoke(SaveSystem.GetFreeHints());

        OnCoinsChanged?.Invoke(SaveSystem.GetCoins());
        OnLockedBlockCountChanged?.Invoke(levelLoader != null ? levelLoader.GetLockedSlotCount() : 0);
        
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

        // IMPORTANT: do NOT dynamically modify hints/undos here.
        // Hints/undos are only granted by the Shop (and the initial save values).

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
        OnLockedBlockCountChanged?.Invoke(levelLoader != null ? levelLoader.GetLockedSlotCount() : 0);
        
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
        if (levelComplete || isAnimating || Time.timeScale == 0f || (GameManager.Instance != null && GameManager.Instance.IsTransitioning)) return;
        
        // Hints/undos are shop-only. (Debug unlimited hints removed)
        // Intentionally no keyboard handling here.


        
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
                if (selectedStack != null && hit.collider.gameObject.name.Contains("_Locked"))
                {
                    TriggerGroundBuildingShake();
                    if (AudioManager.Instance != null) AudioManager.Instance.PlayMoveFailed();
                    
                    Renderer rend = hit.collider.GetComponent<Renderer>();
                    if (rend != null)
                    {
                        StartCoroutine(FlashLockedFoundationColor(rend));
                    }
                }
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
            // Can't select empty or ground-only stacks — trigger ground thud shake!
            if (tappedStack.MovableFloorCount > 0)
            {
                StartCoroutine(AnimateSelectStackFlow(tappedStack));
            }
            else if (tappedStack.GroundFloorCount > 0)
            {
                // Player tapped an immovable ground/base building — red flash + crazy thud shake!
                tappedStack.FlashColor(errorColor, 0.35f);
                TriggerGroundBuildingShake();
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
        // If target is a completed/locked stack, also shake on rejection
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

                // Parent the block to the hook so it travels with the crane
                GameObject floorObj = floorData.floorObject;
                floorObj.transform.SetParent(hookController.hook);
                floorObj.transform.localPosition = new Vector3(0f, -hookController.hookOffset.y, 0f);

                // Compute the elevated position directly above the TARGET stack
                Transform targetTop = FindTopFloorTransformOfStack(to);
                Vector3 targetBasePos = (targetTop != null ? targetTop.position : to.transform.position);
                Vector3 targetElevatedPos = targetBasePos + hookController.hookOffset + new Vector3(0f, 8f, 0f);

                // --- 2-phase crane movement: lift straight up, then travel horizontally ---
                StartCoroutine(CraneLiftAndTransitCoroutine(targetElevatedPos, () =>
                {
                    // Arrived high up above 'to' stack — release the block
                    floorObj.transform.SetParent(null);

                    // Drop block onto target stack with smooth deceleration
                    to.AddFloor(floorObj, floorData.style, animationDuration);
                    if (AudioManager.Instance != null) AudioManager.Instance.PlayFloorPlace();

                    // Update move counter
                    moveCount++;
                    OnMoveCountChanged?.Invoke(moveCount, moveLimit);

                    // Auto-save
                    SaveInProgressState();

                    // Hook follows the block down using EaseOutCubic (matches block landing)
                    Vector3 finalHookPos = to.GetTopFloorWorldPosition() + hookController.hookOffset;
                    StartCoroutine(MoveHookToPositionCoroutine(finalHookPos, animationDuration, HookEasing.EaseOutCubic, () =>
                    {
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
                gridX = allStacks[i].GridPosition.x,
                gridY = allStacks[i].GridPosition.y,
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
        foreach (var stackState in savedState.stacks)
        {
            BuildingStack stack = null;
            if (stackState.gridX >= 0 && stackState.gridY >= 0)
            {
                Vector2Int pos = new Vector2Int(stackState.gridX, stackState.gridY);
                stack = allStacks.Find(s => s.GridPosition == pos);
            }
            
            if (stack == null)
            {
                int idx = stackState.stackIndex;
                if (idx >= 0 && idx < allStacks.Count)
                    stack = allStacks[idx];
            }
            
            if (stack == null) continue;
            
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
    /// Returns the current number of locked slots in the loaded level (from LevelLoader).
    /// Used by UI to read initial locked block count.
    /// </summary>
    public int GetLockedBlockCount()
    {
        return levelLoader != null ? levelLoader.GetLockedSlotCount() : 0;
    }
    
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
    // UNLOCK BLOCK SYSTEM
    // ========================================
    
    /// <summary>
    /// Get the cost to unlock a block, which scales for early levels.
    /// </summary>
    public int GetUnlockCoinCost()
    {
        if (currentLevelNumber == 1) return 20;
        if (currentLevelNumber == 2) return 40;
        return 200;
    }

    /// <summary>
    /// Unlock one locked block. Costs coins (scaled level-wise), or a rewarded ad as fallback.
    /// </summary>
    public bool TryUnlockBlock()
    {
        if (levelComplete || isAnimating) return false;
        
        if (levelLoader == null || levelLoader.GetLockedSlotCount() == 0)
        {
            Debug.Log("<color=orange>No locked blocks to unlock!</color>");
            return false;
        }
        
        int unlockCoinCost = GetUnlockCoinCost();

        bool spent = SaveSystem.SpendCoins(unlockCoinCost);

        if (!spent)
        {
            // Try rewarded ad
            if (AdManager.Instance != null && AdManager.Instance.IsRewardedAdReady("unlock_block"))
            {
                AdManager.Instance.ShowUnlockBlockAd(() =>
                {
                    // On ad complete, unlock for free
                    PerformUnlockBlock();
                });
                return true;
            }
            else
            {
                Debug.Log("<color=red>Not enough coins to unlock! Need " + unlockCoinCost + " coins.</color>");
            }
            return false;
        }
        
        OnCoinsChanged?.Invoke(SaveSystem.GetCoins());
        PerformUnlockBlock();
        return true;
    }
    
    private void PerformUnlockBlock()
    {
        BuildingStack newStack = levelLoader.UnlockOneLockedSlot();
        if (newStack != null)
        {
            // Register the new stack in the game
            allStacks.Add(newStack);
            newStack.SetMaxStackHeight(stackHeight);
            
            // Add to grid map for hints
            gridToStackMap[newStack.GridPosition] = newStack;
            
            // Flash unlock visual on the new stack
            newStack.FlashColor(new Color(1f, 0.9f, 0.2f, 1f), 0.6f);
            
            OnLockedBlockCountChanged?.Invoke(levelLoader.GetLockedSlotCount());
            
            // Save state immediately so the unlocked block is remembered
            SaveInProgressState();
            
            Debug.Log($"<color=green>Block unlocked! Remaining locked: {levelLoader.GetLockedSlotCount()}</color>");
        }
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
        // Only allow undo if player has at least 1 free undo.
        // When none are available, open the shop instead of spending coins/ads.
        if (SaveSystem.GetFreeUndos() <= 0)
        {
            var shop = FindFirstObjectByType<ShopManager>();
            if (shop != null)
                shop.OpenShop();
            else if (GameManager.Instance != null)
                GameManager.Instance.ShowShop();
            return false;
        }

        // Consume one free undo.
        bool hasFree = SaveSystem.UseFreeUndo();
        if (!hasFree)
        {
            // Shouldn't happen due to GetFreeUndos check, but keep behavior safe.
            var shop = FindFirstObjectByType<ShopManager>();
            if (shop != null)
                shop.OpenShop();
            return false;
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

                // Parent the block to the hook so it travels with the crane
                GameObject floorObj = floorData.floorObject;
                floorObj.transform.SetParent(hookController.hook);
                floorObj.transform.localPosition = new Vector3(0f, -hookController.hookOffset.y, 0f);

                // Compute elevated position above target stack (to)
                Transform targetTop = FindTopFloorTransformOfStack(to);
                Vector3 targetBasePos = (targetTop != null ? targetTop.position : to.transform.position);
                Vector3 targetElevatedPos = targetBasePos + hookController.hookOffset + new Vector3(0f, 8f, 0f);

                // --- 2-phase crane movement: lift straight up, then travel horizontally ---
                StartCoroutine(CraneLiftAndTransitCoroutine(targetElevatedPos, () =>
                {
                    // Arrived above 'to' stack — release the block
                    floorObj.transform.SetParent(null);

                    // Drop block onto target stack
                    to.AddFloor(floorObj, floorData.style, animationDuration);
                    if (AudioManager.Instance != null) AudioManager.Instance.PlayFloorPlace();

                    // Decrement move count
                    moveCount = Mathf.Max(0, moveCount - 1);
                    OnMoveCountChanged?.Invoke(moveCount, moveLimit);
                    OnUndoCountChanged?.Invoke(SaveSystem.GetFreeUndos());

                    // Auto-save
                    SaveInProgressState();

                    // Hook follows the block down using EaseOutCubic
                    Vector3 finalHookPos = to.GetTopFloorWorldPosition() + hookController.hookOffset;
                    StartCoroutine(MoveHookToPositionCoroutine(finalHookPos, animationDuration, HookEasing.EaseOutCubic, () =>
                    {
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
    /// Costs one free hint.
    /// Checks consumable availability FIRST to avoid running the expensive solver
    /// when the player has no hints left.
    /// </summary>
    public bool TryShowHint()
    {
        Debug.Log("<color=cyan>TryShowHint called</color>");

        if (levelComplete) return false;

        // ── 1) Check consumable availability BEFORE running the solver ──
        if (!unlimitedHintsEnabled)
        {
            if (SaveSystem.GetFreeHints() <= 0)
            {
                if (GameManager.Instance != null)
                    GameManager.Instance.ShowShop();
                return false;
            }
        }

        BuildingStack hintFrom = null;
        BuildingStack hintTo = null;

        // ── 2) Runtime solver: build current state and find next best move ──
        // Uses a capped BFS to avoid main-thread freezes.
        try
        {
            var currentSlots = BuildCurrentLevelState();
            var solver = new PuzzleSolver(stackHeight);
            // maxMoves=30 + maxStates=5000 keeps BFS fast (< a few ms)
            var solution = solver.FindShortestSolution(currentSlots, 30);

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

        // ── 3) Fallback: if runtime solver didn't yield valid stacks, try precomputed solution ──
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

        // ── 4) Consume the hint ──
        if (!unlimitedHintsEnabled)
        {
            bool hasFree = SaveSystem.UseFreeHint();
            if (!hasFree)
            {
                // Shouldn't happen due to GetFreeHints check, but keep behavior safe.
                var shop = FindFirstObjectByType<ShopManager>();
                if (shop != null)
                    shop.OpenShop();
                else if (GameManager.Instance != null)
                    GameManager.Instance.ShowShop();
                return false;
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

    // Easing modes for hook movement
    private enum HookEasing { Smoothstep, EaseOutCubic, EaseInOutCubic }

    /// <summary>
    /// Move the hook to a world position using the specified easing curve.
    /// </summary>
    private System.Collections.IEnumerator MoveHookToPositionCoroutine(
        Vector3 destination, float duration, System.Action onComplete)
        => MoveHookToPositionCoroutine(destination, duration, HookEasing.Smoothstep, onComplete);

    private System.Collections.IEnumerator MoveHookToPositionCoroutine(
        Vector3 destination, float duration, HookEasing easing, System.Action onComplete)
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
            float raw = Mathf.Clamp01(elapsed / duration);
            float t = easing switch
            {
                HookEasing.EaseOutCubic   => 1f - Mathf.Pow(1f - raw, 3f),
                HookEasing.EaseInOutCubic => raw < 0.5f
                                            ? 4f * raw * raw * raw
                                            : 1f - Mathf.Pow(-2f * raw + 2f, 3f) / 2f,
                _                         => raw * raw * (3f - 2f * raw), // Smoothstep default
            };
            hook.position = Vector3.Lerp(start, destination, t);
            yield return null;
        }

        hook.position = destination;
        onComplete?.Invoke();
    }

    /// <summary>
    /// 2-phase crane transit: first lifts straight up to the carry height,
    /// then glides horizontally to position above the target stack.
    /// This creates the clean L-shaped crane movement instead of a diagonal arc,
    /// and makes the corner transition smooth and natural.
    /// </summary>
    private System.Collections.IEnumerator CraneLiftAndTransitCoroutine(Vector3 targetElevatedPos, System.Action onComplete)
    {
        if (hookController == null || hookController.hook == null)
        {
            onComplete?.Invoke();
            yield break;
        }

        Transform hook = hookController.hook;
        Vector3 currentPos = hook.position;

        // Phase 1 — Lift straight up to the carry height (EaseOutCubic: fast lift, smooth top)
        Vector3 liftTarget = new Vector3(currentPos.x, targetElevatedPos.y, currentPos.z);
        float liftDist    = Mathf.Abs(targetElevatedPos.y - currentPos.y);
        float liftDur     = Mathf.Clamp(liftDist * 0.022f, 0.10f, 0.20f);

        float elapsed = 0f;
        Vector3 liftStart = hook.position;
        while (elapsed < liftDur)
        {
            elapsed += Time.deltaTime;
            float raw = Mathf.Clamp01(elapsed / liftDur);
            float t   = 1f - Mathf.Pow(1f - raw, 3f); // EaseOutCubic
            hook.position = Vector3.Lerp(liftStart, liftTarget, t);
            yield return null;
        }
        hook.position = liftTarget;

        // Phase 2 — Horizontal glide to above the target (EaseInOutCubic: smooth departure and arrival)
        float travelDist = Vector3.Distance(hook.position, targetElevatedPos);
        float travelDur  = Mathf.Clamp(travelDist * 0.018f, 0.12f, 0.30f);

        elapsed = 0f;
        Vector3 travelStart = hook.position;
        while (elapsed < travelDur)
        {
            elapsed += Time.deltaTime;
            float raw = Mathf.Clamp01(elapsed / travelDur);
            // EaseInOutCubic for buttery smooth entry and exit at corners
            float t = raw < 0.5f
                      ? 4f * raw * raw * raw
                      : 1f - Mathf.Pow(-2f * raw + 2f, 3f) / 2f;
            hook.position = Vector3.Lerp(travelStart, targetElevatedPos, t);
            yield return null;
        }
        hook.position = targetElevatedPos;

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

    /// <summary>
    /// Triggers a strong "ground thud" screen shake when the player taps an immovable
    /// base/ground-floor building. Gives a crazy, satisfying wall-hit impact feel.
    /// </summary>
    public void TriggerGroundBuildingShake()
    {
        if (mainCamera != null)
        {
            StartCoroutine(GroundBuildingShakeCoroutine());
        }
    }

    private System.Collections.IEnumerator GroundBuildingShakeCoroutine()
    {
        Vector3 originalPos = mainCamera.transform.localPosition;

        // Phase 1: Sudden big impact jolt (very fast, sharp)
        float impactDuration = 0.07f;
        float impactMagnitude = 0.12f;
        float elapsed = 0f;
        while (elapsed < impactDuration)
        {
            float decay = 1f - (elapsed / impactDuration); // starts big, shrinks
            float x = UnityEngine.Random.Range(-1f, 1f) * impactMagnitude * decay;
            float y = UnityEngine.Random.Range(-1f, 1f) * impactMagnitude * decay;
            mainCamera.transform.localPosition = new Vector3(originalPos.x + x, originalPos.y + y, originalPos.z);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Phase 2: Gentle rolling aftershock decay
        float aftershockDuration = 0.18f;
        float aftershockMagnitude = 0.045f;
        elapsed = 0f;
        while (elapsed < aftershockDuration)
        {
            float decay = 1f - (elapsed / aftershockDuration);
            float x = UnityEngine.Random.Range(-1f, 1f) * aftershockMagnitude * decay;
            float y = UnityEngine.Random.Range(-1f, 1f) * aftershockMagnitude * decay;
            mainCamera.transform.localPosition = new Vector3(originalPos.x + x, originalPos.y + y, originalPos.z);
            elapsed += Time.deltaTime;
            yield return null;
        }

        mainCamera.transform.localPosition = originalPos;
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

    private System.Collections.IEnumerator FlashLockedFoundationColor(Renderer rend)
    {
        Color originalColor = rend.material.color;
        rend.material.color = errorColor;
        yield return new WaitForSeconds(0.25f);
        if (rend != null)
        {
            rend.material.color = originalColor;
        }
    }
}