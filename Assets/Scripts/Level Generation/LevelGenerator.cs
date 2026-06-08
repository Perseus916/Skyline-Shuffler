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
/// - Same-type rule enforced: every generated solution is guaranteed playable
/// </summary>
public class LevelGeneratorEditor : EditorWindow
{
    [Header("Configuration")]
    private DifficultyConfigSO config;
    private BuildingLibrarySO library;

    [Header("Paths")]
    private string savePath = "Assets/Resources/Levels/";

    [Header("Range")]
    private int startLevel = 1;
    private int endLevel = 100;

    // Breather tracking
    private int levelsSinceBreather = 0;
    private int nextBreatherAt = 0;
    
    // Generation stats
    private int regenerationCount = 0;

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
        regenerationCount = 0;

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
        
        Debug.Log($"<color=green>✓ Generated {endLevel - startLevel + 1} levels! ({regenerationCount} regenerations needed)</color>");
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
        
        // All grid slots are now playable/usable (no locked slots)
        int totalPlayableSlots = level.gridDimension * level.gridDimension;
        int emptySlotCount = totalPlayableSlots - buildingCount;
        
        level.emptySlotCount = emptySlotCount;
        level.buildingStyleCount = Mathf.Min(buildingCount, library.allStyles.Count);

        // ═══════════════════════════════════════════════════════════════
        // 4. GENERATE WITH RETRY (ensures solvability)
        // ═══════════════════════════════════════════════════════════════
        
        const int maxRetries = 10;
        List<MoveStep> solution = null;
        List<SlotData> playableSlots = null;
        
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            // Create fresh solved state each attempt
            List<BuildingStyleSO> shuffledStyles = library.allStyles
                .OrderBy(x => Random.value)
                .Take(buildingCount)
                .ToList();
            
            playableSlots = new List<SlotData>();
            level.slots = new List<SlotData>();
            
            int filledCount = 0;
            int createdPlayable = 0;
            
            // Iterate grid positions
            for (int z = level.gridDimension - 1; z >= 0; z--)
            {
                for (int x = 0; x < level.gridDimension; x++)
                {
                    // Crane positions (0,2) and (1,2) are now playable empty slots
                    // No positions are skipped — all 9 grid slots are used

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

            // ═══════════════════════════════════════════════════════════
            // 5. COGNITIVE COMPLEXITY SHUFFLE (with same-type rule)
            // ═══════════════════════════════════════════════════════════
            
            solution = PerformCognitiveShuffles(
                playableSlots, 
                targetOptimalMoves, 
                targetCognitive,
                stackHeight,
                lvlNum
            );
            
            // Reverse to get forward solution
            solution.Reverse();
            
            // Verify solution is actually playable
            if (VerifySolution(playableSlots, solution, stackHeight))
            {
                // Find optimal blue→green solution
                var solver = new PuzzleSolver(stackHeight);
                var optimalSolution = solver.FindShortestSolution(playableSlots, targetOptimalMoves + 10);
                
                if (optimalSolution.Count > 0)
                {
                    solution = optimalSolution;
                    Debug.Log($"Level {lvlNum}: Found optimal blue→green solution with {solution.Count} moves");
                }
                else
                {
                    Debug.LogWarning($"Level {lvlNum}: No blue→green solution found, using shuffled solution");
                }
                
                break; // Valid level!
            }
            else
            {
                regenerationCount++;
                Debug.LogWarning($"Level {lvlNum} attempt {attempt + 1}: solution invalid, retrying...");
                solution = null;
            }
        }
        
        if (solution == null)
        {
            Debug.LogError($"Level {lvlNum}: Failed to generate valid level after {maxRetries} attempts!");
            // Generate a minimal fallback
            solution = new List<MoveStep>();
        }

        // ═══════════════════════════════════════════════════════════════
        // 6. CALCULATE FINAL METRICS
        // ═══════════════════════════════════════════════════════════════
        
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

    // ═══════════════════════════════════════════════════════════════════
    // SHUFFLE ENGINE
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Perform shuffles with cognitive complexity control.
    /// Every shuffle move is validated so the reverse (solution) follows same-type rules.
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
        int maxAttempts = targetMoves * 5; // More headroom with stricter validation
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
                // Find stacks that are already contaminated (mixed floor types)
                var dirtySlots = filledSlots.Where(s => 
                    s.floorStyles.Count >= 2 && 
                    s.floorStyles.Distinct().Count() > 1).ToList();
                
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
            
            if (source.floorStyles.Count == 0) continue;

            // ═══════════════════════════════════════════════════════════
            // TARGET SELECTION (Where to put the floor)
            // Must be valid for BOTH:
            //   - Forward shuffle: target has capacity
            //   - Reverse solution: source's new top matches moved floor
            // ═══════════════════════════════════════════════════════════
            
            BuildingStyleSO movingFloor = source.floorStyles.Last();
            
            var validTargets = playableSlots.Where(slot =>
                slot != source &&
                slot.floorStyles.Count < stackHeight &&
                IsReverseMoveValid(source, movingFloor)).ToList();

            if (validTargets.Count == 0) continue;

            SlotData target;
            
            // Higher cognitive = prefer targets that create blocking
            if (targetCognitive > 50 && Random.value < blockingProbability)
            {
                // Prefer putting a floor on top of a DIFFERENT color (creates blocking)
                var blockingTargets = validTargets.Where(t =>
                    t.floorStyles.Count > 0 &&
                    t.floorStyles.Last() != movingFloor).ToList();
                
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
            // REVERSE MOVE VALIDATION
            // The solution (reverse) takes from target and places on source.
            // After this shuffle move:
            //   - target gains movingFloor on top
            //   - source loses movingFloor
            // For reverse to work:
            //   - target top = movingFloor (player takes it)
            //   - source's new top must match movingFloor or source is empty
            // ═══════════════════════════════════════════════════════════
            
            // Also validate: in the reverse move, the floor lands on source.
            // Source's new top (after removing movingFloor) must match movingFloor,
            // OR source will be empty.
            // Additionally, the reverse move takes from target — after adding movingFloor,
            // target's top IS movingFloor, but the player also needs to be able to
            // place it somewhere with matching top. We already validated source.
            
            // Anti-undo: Don't immediately reverse the last move
            if (source.gridPos == lastTo && target.gridPos == lastFrom) continue;
            
            // For tutorial/early levels, avoid creating blocking
            if (targetCognitive < 20)
            {
                bool wouldBlock = target.floorStyles.Count > 0 && 
                                  target.floorStyles.Last() != movingFloor;
                if (wouldBlock && Random.value > 0.3f) continue; // 70% reject blocking
            }

            // ═══════════════════════════════════════════════════════════
            // EXECUTE SHUFFLE MOVE
            // ═══════════════════════════════════════════════════════════
            
            source.floorStyles.RemoveAt(source.floorStyles.Count - 1);
            target.floorStyles.Add(movingFloor);

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
        
        if (successfulMoves < targetMoves)
        {
            Debug.LogWarning($"Level {level}: Only managed {successfulMoves}/{targetMoves} valid shuffles");
        }

        return forwardSolution;
    }
    
    /// <summary>
    /// Check if the reverse (solution) move is valid under same-type rules.
    /// The reverse move takes movingFloor from the target and places it on source.
    /// After the shuffle, source has lost movingFloor, so we check source's new top.
    /// </summary>
    private bool IsReverseMoveValid(SlotData source, BuildingStyleSO movingFloor)
    {
        // After shuffle: source loses its top (movingFloor).
        // In the reverse (solution): player picks up movingFloor from target
        // and places it on source. Source's new top must match movingFloor.
        
        // If source will become empty after shuffle, any floor can go on it
        if (source.floorStyles.Count <= 1)
            return true;
        
        // Source's new top after removing movingFloor
        BuildingStyleSO sourceNewTop = source.floorStyles[source.floorStyles.Count - 2];
        
        // The reverse move puts movingFloor on top of sourceNewTop — must match
        return sourceNewTop == movingFloor;
    }

    // ═══════════════════════════════════════════════════════════════════
    // SOLUTION VERIFICATION
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Simulate the solution forward on the shuffled state.
    /// Verifies every move is legal under game rules:
    /// - Source has movable floors
    /// - Target has capacity  
    /// - Target top matches moved floor (or target is empty/ground-only)
    /// </summary>
    private bool VerifySolution(List<SlotData> originalSlots, List<MoveStep> solution, int stackHeight)
    {
        // Deep clone the slot data so we don't mutate the original
        var slots = DeepCloneSlots(originalSlots);
        
        foreach (var step in solution)
        {
            // Find source and target slots by grid position
            SlotData from = slots.FirstOrDefault(s => s.gridPos == step.fromGridPos);
            SlotData to = slots.FirstOrDefault(s => s.gridPos == step.toGridPos);
            
            if (from == null || to == null)
            {
                Debug.LogError($"Verification failed: can't find slot at {step.fromGridPos} or {step.toGridPos}");
                return false;
            }
            
            // Check 1: Source has floors to move
            if (from.floorStyles.Count == 0)
            {
                Debug.LogError($"Verification failed: source {step.fromGridPos} is empty");
                return false;
            }
            
            BuildingStyleSO movingFloor = from.floorStyles.Last();
            
            // Check 2: Target has capacity
            // Total capacity in gameplay = stackHeight (movable) + 1 (ground if present)
            int targetCapacity = stackHeight;
            if (to.buildingStyle != null) targetCapacity++; // has ground floor
            
            int targetCurrentFloors = to.floorStyles.Count;
            if (to.buildingStyle != null) targetCurrentFloors++; // ground
            
            if (targetCurrentFloors >= targetCapacity)
            {
                Debug.LogError($"Verification failed: target {step.toGridPos} is full ({targetCurrentFloors}/{targetCapacity})");
                return false;
            }
            
            // Check 3: Same-type rule — top of target must match moving floor
            // If target has floors, check top. If target is empty, any type is OK.
            if (to.floorStyles.Count > 0)
            {
                BuildingStyleSO targetTop = to.floorStyles.Last();
                if (targetTop != movingFloor)
                {
                    Debug.LogError($"Verification failed: type mismatch at {step.toGridPos}. Top={targetTop.buildingName}, Moving={movingFloor.buildingName}");
                    return false;
                }
            }
            else if (to.buildingStyle != null)
            {
                // Target has only ground floor — ground style must match
                if (to.buildingStyle != movingFloor)
                {
                    Debug.LogError($"Verification failed: ground mismatch at {step.toGridPos}. Ground={to.buildingStyle.buildingName}, Moving={movingFloor.buildingName}");
                    return false;
                }
            }
            // else: target is completely empty (no ground, no floors) — any type OK
            
            // Execute move
            from.floorStyles.RemoveAt(from.floorStyles.Count - 1);
            to.floorStyles.Add(movingFloor);
        }
        
        return true;
    }
    
    /// <summary>
    /// Deep clone slot data for verification (don't mutate originals)
    /// </summary>
    private List<SlotData> DeepCloneSlots(List<SlotData> originals)
    {
        var clones = new List<SlotData>();
        foreach (var original in originals)
        {
            var clone = new SlotData
            {
                gridPos = original.gridPos,
                isLocked = original.isLocked,
                isEmpty = original.isEmpty,
                buildingStyle = original.buildingStyle,
                floorStyles = new List<BuildingStyleSO>(original.floorStyles)
            };
            clones.Add(clone);
        }
        return clones;
    }

    // ═══════════════════════════════════════════════════════════════════
    // METRICS
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Calculate actual cognitive complexity based on puzzle state.
    /// Does NOT depend on slot.buildingStyle — uses floor diversity instead.
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
    /// Calculate maximum blocking depth (different types buried under each other).
    /// Counts the number of "style transitions" in each stack — more transitions = deeper blocking.
    /// </summary>
    private int CalculateBlockingDepth(List<SlotData> slots)
    {
        int maxDepth = 0;
        
        foreach (var slot in slots.Where(s => !s.isEmpty && !s.isLocked))
        {
            if (slot.floorStyles.Count < 2) continue;
            
            // Count style transitions (each transition = a block)
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

    /// <summary>
    /// Calculate contamination ratio (how mixed the puzzle is).
    /// A "pure" stack has all floors of one type. Contaminated = multiple types present.
    /// </summary>
    private float CalculateContamination(List<SlotData> slots)
    {
        int totalStacks = 0;
        int contaminatedStacks = 0;
        
        foreach (var slot in slots.Where(s => !s.isEmpty && !s.isLocked && s.floorStyles.Count > 0))
        {
            totalStacks++;
            
            // A stack is contaminated if it has more than 1 distinct style
            int distinctStyles = slot.floorStyles.Distinct().Count();
            if (distinctStyles > 1)
                contaminatedStacks++;
        }
        
        if (totalStacks == 0) return 0;
        return (float)contaminatedStacks / totalStacks;
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