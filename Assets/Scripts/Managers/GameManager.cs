using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Persistent singleton that manages game state and scene transitions.
/// Survives scene loads using DontDestroyOnLoad.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    
    [Header("Scene Names")]
    public string homeSceneName = "HomeScene";
    public string levelSelectSceneName = "LevelSelectScene";
    public string gameSceneName = "MainGame";
    
    [Header("Level Settings")]
    public int totalLevelsAvailable = 100; // Can be expanded
    
    // Runtime state
    public int SelectedLevel { get; private set; } = 1;
    public bool IsResumingGame { get; private set; } = false;
    
    private void Awake()
    {
        // Singleton pattern with persistence
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        // Load save data on startup
        SaveSystem.Load();
    }
    
    // ========================================
    // LEVEL SELECTION
    // ========================================
    
    /// <summary>
    /// Select and load a specific level
    /// </summary>
    public void PlayLevel(int levelNumber)
    {
        SelectedLevel = levelNumber;
        IsResumingGame = false;
        SaveSystem.Data.currentLevel = levelNumber;
        SaveSystem.Save();
        
        LoadGameScene();
    }
    
    /// <summary>
    /// Continue from last played level or in-progress game
    /// </summary>
    public void ContinueGame()
    {
        if (SaveSystem.Data.hasInProgressGame)
        {
            SelectedLevel = SaveSystem.Data.inProgressLevel;
            IsResumingGame = true;
        }
        else
        {
            SelectedLevel = SaveSystem.Data.currentLevel;
            IsResumingGame = false;
        }
        
        LoadGameScene();
    }
    
    /// <summary>
    /// Replay completed level
    /// </summary>
    public void ReplayLevel(int levelNumber)
    {
        SelectedLevel = levelNumber;
        IsResumingGame = false;
        LoadGameScene();
    }
    
    /// <summary>
    /// Load next level after completion
    /// </summary>
    public void LoadNextLevel()
    {
        SelectedLevel++;
        IsResumingGame = false;
        
        if (SelectedLevel > totalLevelsAvailable)
        {
            // All levels complete! Go back to level select
            LoadLevelSelectScene();
        }
        else
        {
            LoadGameScene();
        }
    }
    
    // ========================================
    // SCENE TRANSITIONS
    // ========================================
    
    public void LoadHomeScene()
    {
        SceneManager.LoadScene(homeSceneName);
    }
    
    public void LoadLevelSelectScene()
    {
        SceneManager.LoadScene(levelSelectSceneName);
    }
    
    public void LoadGameScene()
    {
        SceneManager.LoadScene(gameSceneName);
    }
    
    // ========================================
    // LEVEL COMPLETION
    // ========================================
    
    /// <summary>
    /// Called when level is completed
    /// </summary>
    public void OnLevelComplete(int levelNumber, int movesTaken, int optimalMoves)
    {
        int stars = CalculateStars(movesTaken, optimalMoves);
        SaveSystem.SetLevelProgress(levelNumber, stars, movesTaken);
        
        Debug.Log($"<color=green>Level {levelNumber} complete! Stars: {stars}, Moves: {movesTaken}/{optimalMoves}</color>");
    }
    
    /// <summary>
    /// Calculate stars based on moves
    /// </summary>
    public int CalculateStars(int movesTaken, int optimalMoves)
    {
        if (movesTaken <= optimalMoves)
            return 3; // Perfect!
        else if (movesTaken <= optimalMoves * 1.5f)
            return 2; // Good
        else
            return 1; // Completed
    }
    
    // ========================================
    // IN-PROGRESS GAME
    // ========================================
    
    /// <summary>
    /// Save current game state for resume later
    /// </summary>
    public void SaveGameState(int levelNumber, string stateJson, int moveCount)
    {
        SaveSystem.SaveInProgressGame(levelNumber, stateJson, moveCount);
    }
    
    /// <summary>
    /// Get saved game state for resume
    /// </summary>
    public (string stateJson, int moveCount) GetSavedGameState()
    {
        if (SaveSystem.Data.hasInProgressGame)
        {
            return (SaveSystem.Data.inProgressState, SaveSystem.Data.inProgressMoves);
        }
        return (null, 0);
    }
    
    // ========================================
    // SETTINGS
    // ========================================
    
    public float MusicVolume
    {
        get => SaveSystem.Data.musicVolume;
        set
        {
            SaveSystem.Data.musicVolume = value;
            SaveSystem.Save();
            // TODO: Apply to audio mixer
        }
    }
    
    public float SFXVolume
    {
        get => SaveSystem.Data.sfxVolume;
        set
        {
            SaveSystem.Data.sfxVolume = value;
            SaveSystem.Save();
            // TODO: Apply to audio mixer
        }
    }
    
    public bool VibrationEnabled
    {
        get => SaveSystem.Data.vibrationEnabled;
        set
        {
            SaveSystem.Data.vibrationEnabled = value;
            SaveSystem.Save();
        }
    }
}
