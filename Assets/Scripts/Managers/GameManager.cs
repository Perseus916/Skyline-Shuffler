using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Central game controller for single-scene architecture.
/// Loads levels from Resources/Levels/ by name at runtime.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    
    [Header("Core References")]
    [SerializeField] private LevelLoader levelLoader;
    
    [Header("Level Settings")]
    [Tooltip("Path inside Resources folder where levels are stored")]
    [SerializeField] private string levelResourcePath = "Levels/Level_";
    [SerializeField] private int totalLevelsAvailable = 200;
    [SerializeField] private int proceduralStartLevel = 101;
    [SerializeField] private int proceduralEndLevel = 200;
    [SerializeField] private BuildingLibrarySO runtimeBuildingLibrary;
    
    [Header("UI Panels")]
    [SerializeField] private GameObject homePanel;
    [SerializeField] private GameObject levelSelectPanel;
    [SerializeField] private GameObject gameplayPanel;
    [SerializeField] private GameObject settingsPanel;
    
    /// <summary>True when the gameplay panel is active (player is in a level)</summary>
    public bool IsGameplayActive => gameplayPanel != null && gameplayPanel.activeSelf;
    
    [Header("Gameplay UI")]
    [SerializeField] private LevelCompleteUI levelCompleteUI;
    [SerializeField] private DailyRewardUI dailyRewardUI;
    [SerializeField] private ShopUI shopUI;
    
    [Header("Celebration")]
    [Tooltip("Celebration sequence played before showing the level complete popup. If null, popup shows immediately.")]
    [SerializeField] private LevelCompleteCelebration celebration;
    
    [Header("Events")]
    public UnityEvent<int> OnLevelLoaded;
    
    // Runtime state
    public int SelectedLevel { get; private set; } = 1;
    public int TotalLevels => totalLevelsAvailable;
    private readonly Dictionary<int, LevelDataSO> proceduralLevelCache = new();
    private List<BuildingStyleSO> cachedProceduralStylePool;
    
    // ========================================
    // LIFECYCLE
    // ========================================
    
    private void Awake()
    {
        // Singleton (no DontDestroyOnLoad — single scene)
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (totalLevelsAvailable < proceduralEndLevel)
            totalLevelsAvailable = proceduralEndLevel;
        
        // Load save data
        SaveSystem.Load();
    }
    
    private void Start()
    {
        // Always start at home screen
        ShowHomeScreen();
        
        // Show daily reward if available
        if (dailyRewardUI != null && dailyRewardUI.ShouldShow())
        {
            dailyRewardUI.Show();
        }
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        // DEBUG (temporary): unlock all levels on N.
        // Commented out while we remove unintended free progression.
        /*
        if (keyboard.nKey.wasPressedThisFrame)
        {
            SaveSystem.UnlockAllLevels(totalLevelsAvailable);
            Debug.Log($"<color=yellow>Debug unlock enabled: all levels 1-{totalLevelsAvailable} unlocked.</color>");

            if (levelSelectPanel != null && levelSelectPanel.activeSelf)
            {
                ShowLevelSelect();
            }
        }
        */


        // Testing: press C to add 5000 coins
        if (keyboard.cKey.wasPressedThisFrame)
        {
            SaveSystem.AddCoins(5000);
            Debug.Log("<color=cyan>Debug: +5000 coins (C)</color>");

            // Optionally refresh coin display if any GameplayUI is active (GameplayUI listens to events already)
            // but we ensure OnCoinsChanged is triggered via SaveSystem.
            // SaveSystem.AddCoins already calls Save(), but does not invoke gameplay events.
            // Those events are only fired by gameplay UI update loops.
        }
    }

    
    // ========================================
    // PANEL MANAGEMENT
    // ========================================
    
    private void HideAllPanels()
    {
        if (homePanel != null) homePanel.SetActive(false);
        if (levelSelectPanel != null) levelSelectPanel.SetActive(false);
        if (gameplayPanel != null) gameplayPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (levelCompleteUI != null) levelCompleteUI.Hide();
    }
    
    public void ShowHomeScreen()
    {
        HideAllPanels();
        levelLoader.ClearCurrentLevel();
        if (homePanel != null) homePanel.SetActive(true);
        if (AudioManager.Instance != null) AudioManager.Instance.PlayMenuMusic();
    }
    
    public void ShowLevelSelect()
    {
        HideAllPanels();
        levelLoader.ClearCurrentLevel();
        if (levelSelectPanel != null) levelSelectPanel.SetActive(true);
    }
    
    public void ShowSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(true);
    }
    
    public void ShowShop()
    {
        if (shopUI != null) shopUI.Show();
    }
    
    public void HideSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }
    
    // ========================================
    // LEVEL LOADING (Resources.Load)
    // ========================================
    
    /// <summary>
    /// Load a LevelDataSO from Resources by level number.
    /// e.g. Level 5 → Resources.Load("Levels/Level_5")
    /// </summary>
    private LevelDataSO LoadLevelData(int levelNumber)
    {
        if (levelNumber <= 0) return null;

        string path = $"{levelResourcePath}{levelNumber}";
        LevelDataSO data = Resources.Load<LevelDataSO>(path);
        if (data != null)
            return data;

        if (levelNumber >= proceduralStartLevel && levelNumber <= proceduralEndLevel)
        {
            data = GetOrCreateProceduralLevel(levelNumber);
            if (data != null)
                return data;
        }

        Debug.LogError($"Level not found at Resources/{path}!");
        return data;
    }

    private LevelDataSO GetOrCreateProceduralLevel(int levelNumber)
    {
        if (proceduralLevelCache.TryGetValue(levelNumber, out var cached) && cached != null)
            return cached;

        var generated = GenerateProceduralLevel(levelNumber);
        if (generated != null)
            proceduralLevelCache[levelNumber] = generated;

        return generated;
    }

    private LevelDataSO GenerateProceduralLevel(int levelNumber)
    {
        const int gridDimension = 3;
        const int totalSlots = 9;
        const int maxAttempts = 12;

        var stylePool = GetProceduralStylePool();
        if (stylePool.Count < 2)
        {
            Debug.LogError("Not enough building styles found to generate procedural levels.");
            return null;
        }

        Random.State previousState = Random.state;
        Random.InitState(levelNumber * 73856093 ^ 19349663);

        try
        {
            float t = Mathf.InverseLerp(proceduralStartLevel, proceduralEndLevel, levelNumber);
            int stackHeight = GetRuntimeStackHeight(levelNumber);
            int desiredBuildings = 4 + Mathf.FloorToInt(t * 3f); // 4 → 7
            int maxBuildings = Mathf.Min(stylePool.Count, totalSlots - 1, 7);
            int buildingCount = Mathf.Clamp(desiredBuildings, 2, Mathf.Max(2, maxBuildings));

            int targetShuffleMoves = Mathf.RoundToInt(Mathf.Lerp(9f, 22f, t));
            float blockerBias = Mathf.Lerp(0.2f, 0.85f, t);

            List<SlotData> generatedSlots = null;
            List<MoveStep> generatedSolution = null;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                var selectedStyles = stylePool
                    .OrderBy(_ => Random.value)
                    .Take(buildingCount)
                    .ToList();

                var slots = CreateSolvedLayout(selectedStyles, gridDimension, stackHeight);
                var reverseSolution = ShuffleFromSolved(slots, targetShuffleMoves, stackHeight, blockerBias);
                reverseSolution.Reverse();

                if (reverseSolution.Count == 0)
                    continue;

                if (!VerifySolution(slots, reverseSolution, stackHeight))
                    continue;

                generatedSolution = reverseSolution;

                generatedSlots = slots;
                break;
            }

            if (generatedSlots == null || generatedSolution == null)
            {
                Debug.LogError($"Failed to generate procedural level {levelNumber} after {maxAttempts} attempts.");
                return null;
            }

            int optimalMoves = generatedSolution.Count;
            int playerMoveLimit = GetRuntimePlayerMoveLimit(optimalMoves);

            LevelDataSO level = ScriptableObject.CreateInstance<LevelDataSO>();
            level.name = $"Runtime_Level_{levelNumber}";
            level.levelNumber = levelNumber;
            level.gridDimension = gridDimension;
            level.craneGridPos = new Vector2Int(0, gridDimension - 1);
            level.slots = generatedSlots;
            level.solvingSteps = generatedSolution;
            level.optimalMoves = optimalMoves;
            level.playerMoveLimit = playerMoveLimit;
            level.tier = DifficultyTier.Hard;
            level.cognitiveComplexity = Mathf.Lerp(35f, 75f, t);
            level.maxBlockingDepth = CalculateBlockingDepth(generatedSlots);
            level.contaminationRatio = CalculateContamination(generatedSlots);
            level.perfectClearBonus = Mathf.Clamp(Mathf.RoundToInt(10f + levelNumber * 0.1f), 10, 100);
            level.isBreatherLevel = false;
            level.isMilestone = (levelNumber % 50 == 0);
            level.shuffleDepth = generatedSolution.Count;
            level.emptySlotCount = generatedSlots.Count(s => s.buildingStyle == null);
            level.buildingStyleCount = generatedSlots.Count(s => s.buildingStyle != null);

            return level;
        }
        finally
        {
            Random.state = previousState;
        }
    }

    private List<SlotData> CreateSolvedLayout(List<BuildingStyleSO> styles, int gridDimension, int stackHeight)
    {
        var slots = new List<SlotData>(gridDimension * gridDimension);
        int styleIndex = 0;

        for (int z = gridDimension - 1; z >= 0; z--)
        {
            for (int x = 0; x < gridDimension; x++)
            {
                SlotData slot = new SlotData
                {
                    gridPos = new Vector2Int(x, z),
                    isLocked = false,
                    floorStyles = new List<BuildingStyleSO>()
                };

                if (styleIndex < styles.Count)
                {
                    slot.isEmpty = false;
                    slot.buildingStyle = styles[styleIndex];
                    for (int i = 0; i < stackHeight; i++)
                        slot.floorStyles.Add(styles[styleIndex]);
                    styleIndex++;
                }
                else
                {
                    slot.isEmpty = true;
                    slot.buildingStyle = null;
                }

                slots.Add(slot);
            }
        }

        return slots;
    }

    private List<MoveStep> ShuffleFromSolved(List<SlotData> slots, int targetMoves, int stackHeight, float blockerBias)
    {
        List<MoveStep> reverseSolution = new();
        Vector2Int lastFrom = new Vector2Int(-999, -999);
        Vector2Int lastTo = new Vector2Int(-999, -999);

        int attempts = 0;
        int maxAttempts = targetMoves * 12;

        while (reverseSolution.Count < targetMoves && attempts < maxAttempts)
        {
            attempts++;

            var sources = slots.Where(s => s.floorStyles.Count > 0).ToList();
            if (sources.Count == 0) break;

            var source = sources[Random.Range(0, sources.Count)];
            var moving = source.floorStyles[source.floorStyles.Count - 1];

            var validTargets = slots.Where(target =>
                target != source &&
                CanReceiveForGeneration(target, moving, stackHeight) &&
                IsReverseMoveValid(source, moving) &&
                !(source.gridPos == lastTo && target.gridPos == lastFrom)).ToList();

            if (validTargets.Count == 0) continue;

            var blockingTargets = validTargets.Where(t =>
                t.floorStyles.Count == 0 &&
                t.buildingStyle != null &&
                t.buildingStyle != moving).ToList();

            SlotData target;
            if (blockingTargets.Count > 0 && Random.value < blockerBias)
                target = blockingTargets[Random.Range(0, blockingTargets.Count)];
            else
                target = validTargets[Random.Range(0, validTargets.Count)];

            source.floorStyles.RemoveAt(source.floorStyles.Count - 1);
            target.floorStyles.Add(moving);
            source.isEmpty = source.floorStyles.Count == 0 && source.buildingStyle == null;
            target.isEmpty = false;

            reverseSolution.Add(new MoveStep
            {
                fromGridPos = target.gridPos,
                toGridPos = source.gridPos
            });

            lastFrom = source.gridPos;
            lastTo = target.gridPos;
        }

        return reverseSolution;
    }

    private bool CanReceiveForGeneration(SlotData target, BuildingStyleSO movingStyle, int stackHeight)
    {
        int maxMovable = stackHeight + (target.buildingStyle == null ? 1 : 0);
        if (target.floorStyles.Count >= maxMovable)
            return false;

        if (target.floorStyles.Count > 0 && target.floorStyles[target.floorStyles.Count - 1] != movingStyle)
            return false;

        return true;
    }

    private bool IsReverseMoveValid(SlotData source, BuildingStyleSO movingStyle)
    {
        if (source.floorStyles.Count <= 1)
            return true;

        var sourceNewTop = source.floorStyles[source.floorStyles.Count - 2];
        return sourceNewTop == movingStyle;
    }

    private bool VerifySolution(List<SlotData> shuffledSlots, List<MoveStep> solution, int stackHeight)
    {
        var slots = DeepCloneSlots(shuffledSlots);

        foreach (var step in solution)
        {
            var from = slots.FirstOrDefault(s => s.gridPos == step.fromGridPos);
            var to = slots.FirstOrDefault(s => s.gridPos == step.toGridPos);
            if (from == null || to == null) return false;
            if (from.floorStyles.Count == 0) return false;

            var moving = from.floorStyles[from.floorStyles.Count - 1];
            int maxMovable = stackHeight + (to.buildingStyle == null ? 1 : 0);
            if (to.floorStyles.Count >= maxMovable) return false;

            if (to.floorStyles.Count > 0 && to.floorStyles[to.floorStyles.Count - 1] != moving)
                return false;

            from.floorStyles.RemoveAt(from.floorStyles.Count - 1);
            to.floorStyles.Add(moving);
        }

        return IsSolvedState(slots, stackHeight);
    }

    private bool IsSolvedState(List<SlotData> slots, int stackHeight)
    {
        foreach (var slot in slots)
        {
            if (slot.buildingStyle == null)
            {
                if (slot.floorStyles.Count != 0)
                    return false;
                continue;
            }

            if (slot.floorStyles.Count != stackHeight)
                return false;

            for (int i = 0; i < slot.floorStyles.Count; i++)
            {
                if (slot.floorStyles[i] != slot.buildingStyle)
                    return false;
            }
        }

        return true;
    }

    private List<SlotData> DeepCloneSlots(List<SlotData> originals)
    {
        List<SlotData> clones = new();
        foreach (var original in originals)
        {
            clones.Add(new SlotData
            {
                gridPos = original.gridPos,
                isLocked = original.isLocked,
                isEmpty = original.isEmpty,
                buildingStyle = original.buildingStyle,
                floorStyles = new List<BuildingStyleSO>(original.floorStyles)
            });
        }
        return clones;
    }

    private int GetRuntimePlayerMoveLimit(int optimalMoves)
    {
        float raw = optimalMoves * 1.8f;
        int rounded = Mathf.CeilToInt(raw / 5f) * 5;
        if (rounded < optimalMoves + 5)
            rounded += 5;
        return rounded;
    }

    private int GetRuntimeStackHeight(int level)
    {
        if (level < 100) return 3;
        if (level < 300) return 4;
        if (level < 600) return 5;
        return 6;
    }

    private int CalculateBlockingDepth(List<SlotData> slots)
    {
        int maxDepth = 0;
        foreach (var slot in slots)
        {
            if (slot.floorStyles == null || slot.floorStyles.Count < 2) continue;

            int transitions = 0;
            for (int i = 1; i < slot.floorStyles.Count; i++)
            {
                if (slot.floorStyles[i] != slot.floorStyles[i - 1])
                    transitions++;
            }
            maxDepth = Mathf.Max(maxDepth, transitions);
        }
        return maxDepth;
    }

    private float CalculateContamination(List<SlotData> slots)
    {
        int total = 0;
        int contaminated = 0;

        foreach (var slot in slots)
        {
            if (slot.floorStyles == null || slot.floorStyles.Count == 0) continue;
            total++;
            if (slot.floorStyles.Distinct().Count() > 1)
                contaminated++;
        }

        if (total == 0) return 0f;
        return (float)contaminated / total;
    }

    private List<BuildingStyleSO> GetProceduralStylePool()
    {
        if (cachedProceduralStylePool != null && cachedProceduralStylePool.Count > 0)
            return cachedProceduralStylePool;

        HashSet<BuildingStyleSO> styles = new();

        if (runtimeBuildingLibrary != null && runtimeBuildingLibrary.allStyles != null)
        {
            for (int i = 0; i < runtimeBuildingLibrary.allStyles.Count; i++)
            {
                if (runtimeBuildingLibrary.allStyles[i] != null)
                    styles.Add(runtimeBuildingLibrary.allStyles[i]);
            }
        }

        for (int i = 1; i <= 100; i++)
        {
            LevelDataSO data = Resources.Load<LevelDataSO>($"{levelResourcePath}{i}");
            if (data == null || data.slots == null) continue;

            foreach (var slot in data.slots)
            {
                if (slot == null) continue;

                if (slot.buildingStyle != null)
                    styles.Add(slot.buildingStyle);

                if (slot.floorStyles == null) continue;
                for (int f = 0; f < slot.floorStyles.Count; f++)
                {
                    if (slot.floorStyles[f] != null)
                        styles.Add(slot.floorStyles[f]);
                }
            }
        }

        cachedProceduralStylePool = styles
            .OrderBy(s => s.buildingName)
            .ToList();

        return cachedProceduralStylePool;
    }
    
    /// <summary>
    /// Load and play a specific level (fresh start, no resume)
    /// </summary>
    public void PlayLevel(int levelNumber)
    {
        LevelDataSO levelData = LoadLevelData(levelNumber);
        if (levelData == null) return;
        
        // Clear any old in-progress state (fresh start)
        SaveSystem.ClearInProgressGame();
        
        SelectedLevel = levelNumber;
        SaveSystem.Data.currentLevel = levelNumber;
        SaveSystem.Save();
        
        HideAllPanels();
        if (gameplayPanel != null) gameplayPanel.SetActive(true);
        if (AudioManager.Instance != null) AudioManager.Instance.PlayGameplayMusic();
        
        // Load fresh — no saved state
        levelLoader.LoadLevel(levelData, levelNumber);
        
        OnLevelLoaded?.Invoke(levelNumber);
        Debug.Log($"<color=cyan>Playing Level {levelNumber} (fresh)</color>");
    }
    
    /// <summary>
    /// Continue from last played / in-progress level.
    /// If there's a saved state, restores exact floor positions and move count.
    /// </summary>
    public void ContinueGame()
    {
        // Check for in-progress game first
        if (SaveSystem.Data.hasInProgressGame)
        {
            int level = SaveSystem.Data.inProgressLevel;
            LevelDataSO levelData = LoadLevelData(level);
            if (levelData == null) 
            {
                // Fallback: corrupted save, start fresh
                SaveSystem.ClearInProgressGame();
                PlayLevel(Mathf.Max(1, SaveSystem.Data.currentLevel));
                return;
            }
            
            // Parse saved state
            LevelStateData savedState = null;
            try
            {
                savedState = JsonUtility.FromJson<LevelStateData>(SaveSystem.Data.inProgressState);
            }
            catch
            {
                Debug.LogWarning("Failed to parse in-progress state, starting fresh");
            }
            
            if (savedState != null && ShouldResumeSavedState(savedState, level))
            {
                SelectedLevel = level;
                
                HideAllPanels();
                if (gameplayPanel != null) gameplayPanel.SetActive(true);
                if (AudioManager.Instance != null) AudioManager.Instance.PlayGameplayMusic();
                
                // Load level layout, then restore floor positions
                levelLoader.LoadLevelWithRestore(levelData, level, savedState);
                
                OnLevelLoaded?.Invoke(level);
                Debug.Log($"<color=green>Resuming Level {level} from saved state</color>");
                return;
            }

            // Saved state is stale/incompatible - start fresh on the same level
            SaveSystem.ClearInProgressGame();
            PlayLevel(level);
            return;
        }
        
        // No in-progress game — start next level
        int nextLevel = Mathf.Max(1, SaveSystem.Data.currentLevel);
        PlayLevel(nextLevel);
    }

    /// <summary>
    /// Guard against stale or incompatible in-progress snapshots.
    /// We intentionally skip resume for zero-move snapshots and malformed states.
    /// </summary>
    private bool ShouldResumeSavedState(LevelStateData savedState, int level)
    {
        if (savedState == null) return false;
        if (savedState.levelNumber != level) return false;
        if (savedState.moveCount <= 0) return false;
        if (savedState.stacks == null || savedState.stacks.Count == 0) return false;

        for (int i = 0; i < savedState.stacks.Count; i++)
        {
            var stack = savedState.stacks[i];
            if (stack == null) return false;
            if (stack.stackIndex < 0) return false;
            if (stack.floorStyleNames == null) return false;
        }

        return true;
    }
    
    /// <summary>
    /// Load next level after completion
    /// </summary>
    public void LoadNextLevel()
    {
        int nextLevel = SelectedLevel + 1;
        
        // Check if next level exists
        if (LoadLevelData(nextLevel) == null)
        {
            Debug.Log("<color=green>No more levels!</color>");
            ShowLevelSelect();
            return;
        }
        
        PlayLevel(nextLevel);
    }
    
    /// <summary>
    /// Replay current level
    /// </summary>
    public void ReplayLevel()
    {
        PlayLevel(SelectedLevel);
    }
    
    // ========================================
    // LEVEL COMPLETION
    // ========================================
    
    /// <summary>
    /// Called by GameplayManager when level is won
    /// </summary>
    public void OnLevelComplete(int levelNumber, int movesTaken, int optimalMoves, int wrongMoves)
    {
        int stars = CalculateStars(wrongMoves);
        
        // Check if this is a new best for star bonus
        var previousProgress = SaveSystem.GetLevelProgress(levelNumber);
        bool isNewThreeStar = stars == 3 && previousProgress.stars < 3;
        
        SaveSystem.SetLevelProgress(levelNumber, stars, movesTaken);
        
        // Award coins: 10 per star + bonus for first 3-star
        int coinsEarned = stars * 10;
        if (isNewThreeStar) coinsEarned += 20; // First 3-star bonus
        SaveSystem.AddCoins(coinsEarned);
        
        // Replenish 1 free undo on level complete, up to a maximum of 5
        if (SaveSystem.GetFreeUndos() < 5)
        {
            SaveSystem.AddFreeUndos(1);
        }
        
        // Show level complete UI if not the last level
        bool isLastLevel = (levelNumber >= totalLevelsAvailable) || (LoadLevelData(levelNumber + 1) == null);
        Debug.Log($"[GameManager] OnLevelComplete called. levelNumber: {levelNumber}, isLastLevel: {isLastLevel}, celebration: {(celebration != null ? "Assigned" : "Null")}, levelCompleteUI: {(levelCompleteUI != null ? "Assigned" : "Null")}");
        if (isLastLevel)
        {
            Debug.Log("<color=green>Last level complete! Returning to level select.</color>");
            SaveSystem.ClearInProgressGame();
            ShowLevelSelect();
        }
        else
        {
            // Try celebration sequence first, fallback to direct popup
            if (celebration != null)
            {
                Debug.Log("[GameManager] Triggering Celebration Sequence...");
                // Get building stacks from GameplayManager for VFX positioning
                var buildingStacks = GameplayManager.Instance != null 
                    ? GameplayManager.Instance.GetAllStacks() 
                    : null;
                
                celebration.PlayCelebration(
                    levelNumber, movesTaken, optimalMoves, stars, coinsEarned,
                    levelCompleteUI, buildingStacks
                );
            }
            else if (levelCompleteUI != null)
            {
                Debug.Log("[GameManager] No celebration component found. Showing LevelCompleteUI directly.");
                levelCompleteUI.Show(levelNumber, movesTaken, optimalMoves, stars, coinsEarned);
            }
            else
            {
                Debug.LogError("[GameManager] ERROR: Both 'celebration' and 'levelCompleteUI' references are NULL in the GameManager inspector! Cannot show popup.");
            }
        }
        
        Debug.Log($"<color=green>Level {levelNumber} complete! Stars: {stars} | +{coinsEarned} coins | Wrong moves: {wrongMoves} | Moves: {movesTaken}/{optimalMoves}</color>");
    }
    
    public int CalculateStars(int wrongMoves)
    {
        if (wrongMoves <= 0)
            return 3;
        else if (wrongMoves == 1)
            return 2;
        else
            return 1;
    }
    
    // ========================================
    // SETTINGS
    // ========================================
    
    public float MusicVolume
    {
        get => SaveSystem.Data.musicVolume;
        set { SaveSystem.Data.musicVolume = value; SaveSystem.Save(); }
    }
    
    public float SFXVolume
    {
        get => SaveSystem.Data.sfxVolume;
        set { SaveSystem.Data.sfxVolume = value; SaveSystem.Save(); }
    }
    
    public bool VibrationEnabled
    {
        get => SaveSystem.Data.vibrationEnabled;
        set { SaveSystem.Data.vibrationEnabled = value; SaveSystem.Save(); }
    }
}
