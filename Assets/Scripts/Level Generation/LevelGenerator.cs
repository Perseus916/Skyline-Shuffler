using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// Advanced Level Generator with Cognitive Complexity System
/// 
/// Philosophy: Move Count ≠ Difficulty
/// - Early levels: Few moves, obvious solutions
/// - Late levels: Similar move count, but requires strategic thinking
/// 
/// Key Features:
/// - Ultra-gentle exponential difficulty curve
/// - Blocking depth control (colors buried under other colors)
/// - Contamination spread (how mixed stacks are)
/// - Breather levels for engagement
/// - Perfect clear bonus tracking
/// </summary>
public class LevelGeneratorEditor : EditorWindow
{
    [Header("Configuration")]
    private DifficultyConfigSO config;
    private BuildingLibrarySO library;

    [Header("Paths")]
    private string savePath = "Assets/Levels/";

    [Header("Range")]
    private int startLevel = 1;
    private int endLevel = 100;

    // Breather tracking
    private int levelsSinceBreather = 0;
    private int nextBreatherAt = 0;

    [MenuItem("Tools/Skyline Architect/Batch Level Generator")]
    public static void ShowWindow() => GetWindow<LevelGeneratorEditor>("Level Generator");

    private void OnEnable()
    {
        // Try to auto-find config
        string[] guids = AssetDatabase.FindAssets("t:DifficultyConfigSO");
        if (guids.Length > 0)
        {
            config = AssetDatabase.LoadAssetAtPath<DifficultyConfigSO>(
                AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }

    private void OnGUI()
    {
        GUILayout.Label("Advanced Level Generator", EditorStyles.boldLabel);
        
        EditorGUILayout.Space();
        config = (DifficultyConfigSO)EditorGUILayout.ObjectField(
            "Difficulty Config", config, typeof(DifficultyConfigSO), false);
        library = (BuildingLibrarySO)EditorGUILayout.ObjectField(
            "Building Library", library, typeof(BuildingLibrarySO), false);

        EditorGUILayout.Space();
        GUILayout.Label("Level Range", EditorStyles.miniBoldLabel);
        startLevel = EditorGUILayout.IntField("Start Level", startLevel);
        endLevel = EditorGUILayout.IntField("End Level", endLevel);

        EditorGUILayout.Space();
        savePath = EditorGUILayout.TextField("Save Path", savePath);

        // Preview stats
        if (config != null)
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                $"Level 10: {config.GetOptimalMoves(10)} moves | Cognitive: {config.GetCognitiveComplexity(10):F0}\n" +
                $"Level 100: {config.GetOptimalMoves(100)} moves | Cognitive: {config.GetCognitiveComplexity(100):F0}\n" +
                $"Level 500: {config.GetOptimalMoves(500)} moves | Cognitive: {config.GetCognitiveComplexity(500):F0}",
                MessageType.Info);
        }

        EditorGUILayout.Space();
        GUI.enabled = config != null && library != null;
        if (GUILayout.Button("Generate Batch Levels", GUILayout.Height(30)))
        {
            GenerateBatch();
        }
        GUI.enabled = true;

        if (config == null)
            EditorGUILayout.HelpBox("Create a Difficulty Config asset first!\nRight-click > Create > Skyline Architect > Difficulty Config", MessageType.Warning);
        if (library == null)
            EditorGUILayout.HelpBox("Assign a Building Library!", MessageType.Warning);
    }

    private void GenerateBatch()
    {
        if (!Directory.Exists(savePath)) 
            Directory.CreateDirectory(savePath);

        // Reset breather tracking
        levelsSinceBreather = 0;
        nextBreatherAt = Random.Range(config.breatherMinGap, config.breatherMaxGap + 1);

        int progressId = Progress.Start("Generating Levels", null, Progress.Options.Managed);
        
        try
        {
            for (int i = startLevel; i <= endLevel; i++)
            {
                Progress.Report(progressId, (float)(i - startLevel) / (endLevel - startLevel + 1), 
                    $"Level {i}");
                CreateLevelAsset(i);
            }
        }
        finally
        {
            Progress.Remove(progressId);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        Debug.Log($"<color=green>✓ Generated {endLevel - startLevel + 1} levels!</color>");
    }

    private void CreateLevelAsset(int lvlNum)
    {
        LevelDataSO level = CreateInstance<LevelDataSO>();
        level.levelNumber = lvlNum;

        // ═══════════════════════════════════════════════════════════════
        // 1. DETERMINE LEVEL PARAMETERS FROM CONFIG
        // ═══════════════════════════════════════════════════════════════
        
        level.tier = config.GetTier(lvlNum);
        level.gridDimension = config.GetGridDimension(lvlNum);
        level.craneGridPos = new Vector2Int(0, level.gridDimension - 1);
        
        int stackHeight = config.GetStackHeight(lvlNum);
        float targetCognitive = config.GetCognitiveComplexity(lvlNum);
        int targetOptimalMoves = config.GetOptimalMoves(lvlNum);

        // ═══════════════════════════════════════════════════════════════
        // 2. BREATHER LEVEL CHECK
        // ═══════════════════════════════════════════════════════════════
        
        levelsSinceBreather++;
        level.isBreatherLevel = false;
        
        if (lvlNum > config.tutorialEnd && levelsSinceBreather >= nextBreatherAt)
        {
            level.isBreatherLevel = true;
            levelsSinceBreather = 0;
            nextBreatherAt = Random.Range(config.breatherMinGap, config.breatherMaxGap + 1);
            
            // Reduce difficulty for breather
            targetOptimalMoves = Mathf.Max(3, Mathf.RoundToInt(targetOptimalMoves * config.breatherDifficultyMultiplier));
            targetCognitive *= config.breatherDifficultyMultiplier;
        }

        // Milestone check
        level.isMilestone = (lvlNum % 50 == 0);

        // ═══════════════════════════════════════════════════════════════
        // 3. CALCULATE SLOT CONFIGURATION
        // ═══════════════════════════════════════════════════════════════
        
        // Building count scales with difficulty tier
        int buildingCount = GetBuildingCount(level.tier, lvlNum);
        
        // Empty slots: More = easier. Tutorial/Easy get 2, later get 1
        int emptySlotCount = (lvlNum <= config.easyEnd) ? 2 : 1;
        if (level.isBreatherLevel) emptySlotCount = 2; // Breathers are easier
        
        int totalPlayableSlots = buildingCount + emptySlotCount;
        
        level.emptySlotCount = emptySlotCount;
        level.buildingStyleCount = Mathf.Min(buildingCount, library.allStyles.Count);

        // ═══════════════════════════════════════════════════════════════
        // 4. CREATE SOLVED STATE (Perfect stacks)
        // ═══════════════════════════════════════════════════════════════
        
        List<BuildingStyleSO> shuffledStyles = library.allStyles
            .OrderBy(x => Random.value)
            .Take(buildingCount)
            .ToList();
        
        List<SlotData> playableSlots = new List<SlotData>();
        level.slots = new List<SlotData>();

        int filledCount = 0;
        int createdPlayable = 0;

        // Iterate grid positions
        for (int z = level.gridDimension - 1; z >= 0; z--)
        {
            for (int x = 0; x < level.gridDimension; x++)
            {
                // Skip crane positions (2 units wide at top-left)
                if (z == level.gridDimension - 1 && (x == 0 || x == 1)) continue;

                SlotData slot = new SlotData { gridPos = new Vector2Int(x, z) };

                if (createdPlayable < totalPlayableSlots)
                {
                    slot.isLocked = false;

                    if (filledCount < buildingCount)
                    {
                        slot.buildingStyle = shuffledStyles[filledCount % shuffledStyles.Count];
                        // Fill with same style = SOLVED state
                        for (int f = 0; f < stackHeight; f++)
                            slot.floorStyles.Add(slot.buildingStyle);
                        filledCount++;
                    }
                    else
                    {
                        slot.isEmpty = true;
                        slot.buildingStyle = null;
                    }

                    playableSlots.Add(slot);
                    createdPlayable++;
                }
                else
                {
                    slot.isLocked = true;
                }
                level.slots.Add(slot);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // 5. COGNITIVE COMPLEXITY SHUFFLE
        // ═══════════════════════════════════════════════════════════════
        
        List<MoveStep> solution = PerformCognitiveShuffles(
            playableSlots, 
            targetOptimalMoves, 
            targetCognitive,
            stackHeight,
            lvlNum
        );

        // ═══════════════════════════════════════════════════════════════
        // 6. CALCULATE FINAL METRICS
        // ═══════════════════════════════════════════════════════════════
        
        solution.Reverse(); // Convert backward moves to forward solution
        level.solvingSteps = solution;
        level.optimalMoves = solution.Count;
        level.shuffleDepth = solution.Count;
        
        // Player limit = optimal × multiplier, rounded
        level.playerMoveLimit = config.GetPlayerMoveLimit(level.optimalMoves);
        
        // Ensure minimum cushion
        if (level.playerMoveLimit < level.optimalMoves + 5)
            level.playerMoveLimit += config.playerLimitRounding;

        // Calculate actual cognitive complexity
        level.cognitiveComplexity = CalculateActualCognitiveScore(playableSlots, level.tier);
        level.maxBlockingDepth = CalculateBlockingDepth(playableSlots);
        level.contaminationRatio = CalculateContamination(playableSlots);
        
        // Perfect clear bonus
        level.perfectClearBonus = config.GetPerfectClearBonus(lvlNum);

        // ═══════════════════════════════════════════════════════════════
        // 7. SAVE ASSET
        // ═══════════════════════════════════════════════════════════════
        
        string assetPath = $"{savePath}Level_{lvlNum}.asset";
        AssetDatabase.CreateAsset(level, assetPath);
    }

    /// <summary>
    /// Perform shuffles with cognitive complexity control.
    /// Early levels: Surface contamination only.
    /// Late levels: Deep blocking, buried colors.
    /// </summary>
    private List<MoveStep> PerformCognitiveShuffles(
        List<SlotData> playableSlots,
        int targetMoves,
        float targetCognitive,
        int stackHeight,
        int level)
    {
        List<MoveStep> forwardSolution = new List<MoveStep>();
        
        // Get filled slots only for source selection
        var getFilledSlots = new System.Func<List<SlotData>>(() => 
            playableSlots.Where(s => s.floorStyles.Count > 0).ToList());
        
        Vector2Int lastFrom = new Vector2Int(-1, -1);
        Vector2Int lastTo = new Vector2Int(-1, -1);

        // Calculate how "strategic" our shuffles should be
        float blockingProbability = Mathf.Clamp01(targetCognitive / 100f);
        int maxAttempts = targetMoves * 3; // Prevent infinite loops
        int attempts = 0;
        int successfulMoves = 0;

        while (successfulMoves < targetMoves && attempts < maxAttempts)
        {
            attempts++;
            
            var filledSlots = getFilledSlots();
            if (filledSlots.Count == 0) break;

            // ═══════════════════════════════════════════════════════════
            // SOURCE SELECTION (Where to take a floor from)
            // ═══════════════════════════════════════════════════════════
            SlotData source;
            
            // Higher cognitive = prefer "dirty" stacks (create deeper blocking)
            if (targetCognitive > 30 && Random.value < blockingProbability)
            {
                // Find stacks that are already contaminated
                var dirtySlots = filledSlots.Where(s => 
                    s.buildingStyle != null && 
                    s.floorStyles.Any(f => f != s.buildingStyle)).ToList();
                
                if (dirtySlots.Count > 0 && Random.value < 0.7f)
                    source = dirtySlots[Random.Range(0, dirtySlots.Count)];
                else
                    source = filledSlots[Random.Range(0, filledSlots.Count)];
            }
            else
            {
                // Early levels: Random selection
                source = filledSlots[Random.Range(0, filledSlots.Count)];
            }

            // ═══════════════════════════════════════════════════════════
            // TARGET SELECTION (Where to put the floor)
            // ═══════════════════════════════════════════════════════════
            var validTargets = playableSlots.Where(slot =>
                slot != source &&
                slot.floorStyles.Count < stackHeight).ToList();

            if (validTargets.Count == 0) continue;

            SlotData target;
            
            // Higher cognitive = prefer targets that create blocking
            if (targetCognitive > 50 && Random.value < blockingProbability)
            {
                // Prefer putting a floor on top of a DIFFERENT color (creates blocking)
                var blockingTargets = validTargets.Where(t =>
                    t.floorStyles.Count > 0 &&
                    t.floorStyles.Last() != source.floorStyles.Last()).ToList();
                
                if (blockingTargets.Count > 0)
                    target = blockingTargets[Random.Range(0, blockingTargets.Count)];
                else
                    target = validTargets[Random.Range(0, validTargets.Count)];
            }
            else
            {
                target = validTargets[Random.Range(0, validTargets.Count)];
            }

            // ═══════════════════════════════════════════════════════════
            // VALIDATION
            // ═══════════════════════════════════════════════════════════
            
            // Anti-undo: Don't immediately reverse the last move
            if (source.gridPos == lastTo && target.gridPos == lastFrom) continue;
            
            // For tutorial/early levels, avoid creating blocking
            if (targetCognitive < 20)
            {
                bool wouldBlock = target.floorStyles.Count > 0 && 
                                  target.floorStyles.Last() != source.floorStyles.Last();
                if (wouldBlock && Random.value > 0.3f) continue; // 70% reject blocking
            }

            // ═══════════════════════════════════════════════════════════
            // EXECUTE MOVE
            // ═══════════════════════════════════════════════════════════
            
            BuildingStyleSO floor = source.floorStyles.Last();
            source.floorStyles.RemoveAt(source.floorStyles.Count - 1);
            target.floorStyles.Add(floor);

            if (target.isEmpty) target.isEmpty = false;

            // Record forward solution (reverse of what we did)
            forwardSolution.Add(new MoveStep 
            { 
                fromGridPos = target.gridPos, 
                toGridPos = source.gridPos 
            });

            lastFrom = source.gridPos;
            lastTo = target.gridPos;
            successfulMoves++;
        }

        return forwardSolution;
    }

    /// <summary>
    /// Calculate actual cognitive complexity based on puzzle state
    /// </summary>
    private float CalculateActualCognitiveScore(List<SlotData> slots, DifficultyTier tier)
    {
        float score = 0;
        
        // Base score from tier
        score += (int)tier * 10f;
        
        // Add blocking depth contribution
        int blocking = CalculateBlockingDepth(slots);
        score += blocking * 8f;
        
        // Add contamination contribution
        float contamination = CalculateContamination(slots);
        score += contamination * 30f;
        
        return Mathf.Clamp(score, 0f, 100f);
    }

    /// <summary>
    /// Calculate maximum blocking depth (colors buried under other colors)
    /// </summary>
    private int CalculateBlockingDepth(List<SlotData> slots)
    {
        int maxDepth = 0;
        
        foreach (var slot in slots.Where(s => !s.isEmpty && !s.isLocked))
        {
            if (slot.floorStyles.Count < 2) continue;
            
            int currentDepth = 0;
            BuildingStyleSO topColor = slot.floorStyles.Last();
            
            // Count how many floors of different color are on top of matching floors
            for (int i = slot.floorStyles.Count - 2; i >= 0; i--)
            {
                if (slot.floorStyles[i] != topColor && slot.floorStyles[i] == slot.buildingStyle)
                    currentDepth++;
            }
            
            maxDepth = Mathf.Max(maxDepth, currentDepth);
        }
        
        return maxDepth;
    }

    /// <summary>
    /// Calculate contamination ratio (how mixed the puzzle is)
    /// </summary>
    private float CalculateContamination(List<SlotData> slots)
    {
        int totalFloors = 0;
        int contaminatedFloors = 0;
        
        foreach (var slot in slots.Where(s => !s.isEmpty && !s.isLocked && s.buildingStyle != null))
        {
            foreach (var floor in slot.floorStyles)
            {
                totalFloors++;
                if (floor != slot.buildingStyle)
                    contaminatedFloors++;
            }
        }
        
        if (totalFloors == 0) return 0;
        return (float)contaminatedFloors / totalFloors;
    }

    /// <summary>
    /// Get building count based on difficulty tier
    /// </summary>
    private int GetBuildingCount(DifficultyTier tier, int level)
    {
        return tier switch
        {
            DifficultyTier.Tutorial => 2,
            DifficultyTier.Easy => Mathf.Min(3, library.allStyles.Count),
            DifficultyTier.Medium => Mathf.Min(3, library.allStyles.Count),
            DifficultyTier.Hard => Mathf.Min(4, library.allStyles.Count),
            DifficultyTier.Expert => Mathf.Min(5, library.allStyles.Count),
            DifficultyTier.Master => Mathf.Min(6, library.allStyles.Count),
            _ => 3
        };
    }
}