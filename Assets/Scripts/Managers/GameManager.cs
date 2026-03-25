using UnityEngine;
using UnityEngine.Events;

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
    [SerializeField] private int totalLevelsAvailable = 100;
    
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
    
    [Header("Events")]
    public UnityEvent<int> OnLevelLoaded;
    
    // Runtime state
    public int SelectedLevel { get; private set; } = 1;
    public int TotalLevels => totalLevelsAvailable;
    
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
        if (AudioManager.Instance != null) AudioManager.Instance.PlayPopupOpen();
    }
    
    public void ShowShop()
    {
        if (shopUI != null) shopUI.Show();
        if (AudioManager.Instance != null) AudioManager.Instance.PlayPopupOpen();
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
        string path = $"{levelResourcePath}{levelNumber}";
        LevelDataSO data = Resources.Load<LevelDataSO>(path);
        
        if (data == null)
            Debug.LogError($"Level not found at Resources/{path}!");
        
        return data;
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
    public void OnLevelComplete(int levelNumber, int movesTaken, int optimalMoves)
    {
        int stars = CalculateStars(movesTaken, optimalMoves);
        
        // Check if this is a new best for star bonus
        var previousProgress = SaveSystem.GetLevelProgress(levelNumber);
        bool isNewThreeStar = stars == 3 && previousProgress.stars < 3;
        
        SaveSystem.SetLevelProgress(levelNumber, stars, movesTaken);
        
        // Award coins: 10 per star + bonus for first 3-star
        int coinsEarned = stars * 10;
        if (isNewThreeStar) coinsEarned += 20; // First 3-star bonus
        SaveSystem.AddCoins(coinsEarned);
        
        // Replenish 1 free undo on level complete
        SaveSystem.AddFreeUndos(1);
        
        // Show level complete UI
        if (levelCompleteUI != null)
        {
            levelCompleteUI.Show(levelNumber, movesTaken, optimalMoves, stars, coinsEarned);
        }
        
        Debug.Log($"<color=green>Level {levelNumber} complete! ⭐{stars} | +{coinsEarned} coins | Moves: {movesTaken}/{optimalMoves}</color>");
    }
    
    public int CalculateStars(int movesTaken, int optimalMoves)
    {
        if (optimalMoves <= 0) return 1;
        
        if (movesTaken <= optimalMoves)
            return 3;
        else if (movesTaken <= Mathf.CeilToInt(optimalMoves * 1.5f))
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
