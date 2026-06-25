using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Save data structure for level progress and game state.
/// Serialized to JSON and stored in PlayerPrefs.
/// </summary>
[System.Serializable]
public class LevelProgress
{
    public bool completed;
    public int stars;       // 0-3
    public int bestMoves;
}

[System.Serializable]
public class GameSaveData
{
    public int currentLevel = 1;
    public int highestUnlockedLevel = 1;
    public List<LevelProgressEntry> levelProgress = new();
    
    // In-progress game state (for resume)
    public bool hasInProgressGame;
    public int inProgressLevel;
    public string inProgressState; // JSON of floor positions
    public int inProgressMoves;
    
    // Economy
    public int coins = 0;
    public int freeUndos = 1;    // Start with 1 undo on first download/play
    public int freeHints = 1;    // Start with 1 hint on first download/play

    
    // Settings
    public float masterVolume = 1f;
    public float musicVolume = 1f;
    public float sfxVolume = 1f;
    public bool vibrationEnabled = true;
}

[System.Serializable]
public class LevelProgressEntry
{
    public int levelNumber;
    public LevelProgress progress;
}

/// <summary>
/// Static class for saving/loading game data using PlayerPrefs.
/// </summary>
public static class SaveSystem
{
    private const string SAVE_KEY = "CitySortSaveData";
    
    private static GameSaveData _cachedData;
    
    /// <summary>
    /// Get the current save data (cached for performance)
    /// </summary>
    public static GameSaveData Data
    {
        get
        {
            if (_cachedData == null)
                Load();
            return _cachedData;
        }
    }
    
    /// <summary>
    /// Load save data from PlayerPrefs
    /// </summary>
    public static void Load()
    {
        string json = PlayerPrefs.GetString(SAVE_KEY, "");
        if (string.IsNullOrEmpty(json))
        {
            _cachedData = new GameSaveData();
        }
        else
        {
            try
            {
                _cachedData = JsonUtility.FromJson<GameSaveData>(json);
            }
            catch
            {
                Debug.LogWarning("Failed to parse save data, creating new");
                _cachedData = new GameSaveData();
            }
        }

        // Ensure starting consumables once per fresh install.
        // This prevents negative/incorrect values if old saves exist.
        EnsureInitialConsumables();
    }

    
    /// <summary>
    /// Save current data to PlayerPrefs
    /// </summary>
    public static void Save()
    {
        if (_cachedData == null) return;
        
        string json = JsonUtility.ToJson(_cachedData);
        PlayerPrefs.SetString(SAVE_KEY, json);
        PlayerPrefs.Save();
    }
    
    /// <summary>
    /// Get progress for a specific level
    /// </summary>
    public static LevelProgress GetLevelProgress(int levelNumber)
    {
        var entry = Data.levelProgress.Find(e => e.levelNumber == levelNumber);
        return entry?.progress ?? new LevelProgress();
    }
    
    /// <summary>
    /// Save progress for a level
    /// </summary>
    public static void SetLevelProgress(int levelNumber, int stars, int moves)
    {
        if (levelNumber <= 0) return; // Safety guard

        var existing = Data.levelProgress.Find(e => e.levelNumber == levelNumber);
        
        if (existing != null)
        {
            // Keep the BEST (highest) star count, never downgrade
            existing.progress.stars = Mathf.Max(existing.progress.stars, stars);
            if (!existing.progress.completed || moves < existing.progress.bestMoves)
                existing.progress.bestMoves = moves;
            existing.progress.completed = true;
        }
        else
        {
            Data.levelProgress.Add(new LevelProgressEntry
            {
                levelNumber = levelNumber,
                progress = new LevelProgress
                {
                    completed = true,
                    stars = stars,
                    bestMoves = moves
                }
            });
        }
        
        // Unlock next level only (do NOT create a progress entry for it)
        if (levelNumber >= Data.highestUnlockedLevel)
        {
            Data.highestUnlockedLevel = Mathf.Min(1000, levelNumber + 1);
        }
        
        // Update current level pointer
        Data.currentLevel = Mathf.Min(1000, levelNumber + 1);
        
        // Clear in-progress since level is complete
        ClearInProgressGame();
        
        Save();
    }
    
    /// <summary>
    /// Save current game state for resume
    /// </summary>
    public static void SaveInProgressGame(int levelNumber, string stateJson, int moveCount)
    {
        Data.hasInProgressGame = true;
        Data.inProgressLevel = levelNumber;
        Data.inProgressState = stateJson;
        Data.inProgressMoves = moveCount;
        Save();
    }
    
    /// <summary>
    /// Clear in-progress game
    /// </summary>
    public static void ClearInProgressGame()
    {
        Data.hasInProgressGame = false;
        Data.inProgressLevel = 0;
        Data.inProgressState = "";
        Data.inProgressMoves = 0;
        Save();
    }
    
    /// <summary>
    /// Check if a level is unlocked
    /// </summary>
    public static bool IsLevelUnlocked(int levelNumber)
    {
        if (levelNumber > 1000) return false;
        return levelNumber <= Data.highestUnlockedLevel;
    }
    
    /// <summary>
    /// Get total stars earned
    /// </summary>
    public static int GetTotalStars()
    {
        int total = 0;
        foreach (var entry in Data.levelProgress)
        {
            total += entry.progress.stars;
        }
        return total;
    }
    
    // ========================================
    // COINS
    // ========================================

    public static int GetCoins() => Data.coins;

    private static void EnsureInitialConsumables()
    {
        // If save is brand new, start with exactly 1 hint + 1 undo.
        // (Also safe if other systems have accidentally overwritten via old patterns.)
        if (Data.freeHints == 0 && Data.freeUndos == 0)
        {
            Data.freeUndos = 1;
            Data.freeHints = 1;
            Save();
        }
    }

    
    /// <summary>Add coins and save</summary>
    public static void AddCoins(int amount)
    {
        Data.coins += amount;
        Save();
    }
    
    /// <summary>Try to spend coins, returns false if insufficient</summary>
    public static bool SpendCoins(int amount)
    {
        if (Data.coins < amount) return false;
        Data.coins -= amount;
        Save();
        return true;
    }
    
    // ========================================
    // CONSUMABLES (Undo / Hint)
    // ========================================
    
    public static int GetFreeUndos() => Data.freeUndos;
    public static int GetFreeHints() => Data.freeHints;
    
    public static void SetFreeUndos(int count)
    {
        Data.freeUndos = count;
        Save();
    }
    
    public static void SetFreeHints(int count)
    {
        Data.freeHints = count;
        Save();
    }
    
    /// <summary>Use a free undo. Returns true if available.</summary>
    public static bool UseFreeUndo()
    {
        if (Data.freeUndos <= 0) return false;
        Data.freeUndos--;
        Save();
        return true;
    }
    
    /// <summary>Use a free hint. Returns true if available.</summary>
    public static bool UseFreeHint()
    {
        if (Data.freeHints <= 0) return false;
        Data.freeHints--;
        Save();
        return true;
    }
    
    /// <summary>Add free undos (from ads, shop, etc.)</summary>
    public static void AddFreeUndos(int count)
    {
        Data.freeUndos += count;
        Save();
    }
    
    /// <summary>Add free hints (from ads, shop, etc.)</summary>
    public static void AddFreeHints(int count)
    {
        Data.freeHints += count;
        Save();
    }
    
    /// <summary>
    /// Reset all progress (for settings)
    /// </summary>
    public static void ResetAllProgress()
    {
        _cachedData = new GameSaveData();
        Save();
    }

    /// <summary>
    /// Reset only level progress, keeping coins, hints, undos, and settings.
    /// </summary>
    public static void ResetLevelProgressOnly()
    {
        Data.currentLevel = 1;
        Data.highestUnlockedLevel = 1;
        Data.levelProgress.Clear();
        Data.hasInProgressGame = false;
        Data.inProgressLevel = 0;
        Data.inProgressState = "";
        Data.inProgressMoves = 0;
        Save();
    }

    /// <summary>
    /// Debug helper: unlock all levels up to the provided maximum.
    /// </summary>
    public static void UnlockAllLevels(int maxLevel)
    {
        Data.highestUnlockedLevel = Mathf.Clamp(maxLevel, 1, 1000);
        Data.currentLevel = Mathf.Clamp(Data.currentLevel, 1, 1000);
        Save();
    }
}
