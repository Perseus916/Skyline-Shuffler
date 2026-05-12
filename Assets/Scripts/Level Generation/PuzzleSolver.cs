using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// BFS-based puzzle solver that finds the shortest solution path.
/// Uses the same stack-capacity and completion rules as gameplay.
/// </summary>
public class PuzzleSolver
{
    private int stackHeight;
    
    /// <summary>
    /// Represents a single puzzle state for BFS.
    /// </summary>
    private struct StateSnapshot
    {
        public List<List<BuildingStyleSO>> stacks; // Current state of each stack
        public List<MoveStep> movesMade; // Path taken to reach this state
        
        public StateSnapshot(List<List<BuildingStyleSO>> stacks, List<MoveStep> movesMade)
        {
            this.stacks = stacks;
            this.movesMade = movesMade;
        }
    }
    
    public PuzzleSolver(int stackHeight)
    {
        this.stackHeight = stackHeight;
    }
    
    /// <summary>
    /// Find the shortest solution using BFS.
    /// Prefers blue→green moves first, then falls back to any valid direction
    /// so runtime hints still work on states that need a reverse move.
    /// </summary>
    public List<MoveStep> FindShortestSolution(List<SlotData> slots, int maxMoves = 100)
    {
        var forwardOnly = FindShortestSolutionInternal(slots, maxMoves, true);
        if (forwardOnly.Count > 0)
            return forwardOnly;

        return FindShortestSolutionInternal(slots, maxMoves, false);
    }

    private List<MoveStep> FindShortestSolutionInternal(List<SlotData> slots, int maxMoves, bool forwardOnly)
    {
        int totalCapacity = stackHeight + 1;

        // Create initial state
        var initialState = new StateSnapshot(
            CreateStateSnapshot(slots),
            new List<MoveStep>()
        );
        
        // Check if already solved
        if (IsSolved(initialState.stacks, slots))
        {
            return new List<MoveStep>();
        }
        
        // BFS
        Queue<StateSnapshot> queue = new Queue<StateSnapshot>();
        HashSet<string> visited = new HashSet<string>();
        
        queue.Enqueue(initialState);
        visited.Add(StateToHash(initialState.stacks));
        
        while (queue.Count > 0)
        {
            StateSnapshot current = queue.Dequeue();
            
            // Stop if solution is too long
            if (current.movesMade.Count >= maxMoves)
                continue;
            
            // Try all valid moves
            for (int fromIdx = 0; fromIdx < slots.Count; fromIdx++)
            {
                if (current.stacks[fromIdx].Count == 0) continue; // Empty source
                
                var movingFloor = current.stacks[fromIdx].Last();
                
                int startToIdx = forwardOnly ? fromIdx + 1 : 0;
                int endToIdx = slots.Count;

                for (int toIdx = startToIdx; toIdx < endToIdx; toIdx++)
                {
                    if (toIdx == fromIdx) continue;

                    // In forward-only mode, keep the original blue→green preference.
                    if (forwardOnly && toIdx < fromIdx)
                        continue;

                    // Check if move is valid
                    if (current.stacks[toIdx].Count >= totalCapacity) continue; // Full target
                    
                    // Same-type rule: if target has floors, top must match
                    if (current.stacks[toIdx].Count > 0)
                    {
                        if (current.stacks[toIdx].Last() != movingFloor)
                            continue; // Type mismatch
                    }
                    
                    // Execute move
                    var newState = CreateStateSnapshot(current.stacks);
                    var movedFloor = newState[fromIdx].Last();
                    newState[fromIdx].RemoveAt(newState[fromIdx].Count - 1);
                    newState[toIdx].Add(movedFloor);
                    
                    // Check if solved
                    if (IsSolved(newState, slots))
                    {
                        var solution = new List<MoveStep>(current.movesMade);
                        solution.Add(new MoveStep 
                        { 
                            fromGridPos = slots[fromIdx].gridPos, 
                            toGridPos = slots[toIdx].gridPos 
                        });
                        return solution;
                    }
                    
                    // Add to queue if not visited
                    string hash = StateToHash(newState);
                    if (!visited.Contains(hash))
                    {
                        visited.Add(hash);
                        var newMoves = new List<MoveStep>(current.movesMade);
                        newMoves.Add(new MoveStep 
                        { 
                            fromGridPos = slots[fromIdx].gridPos, 
                            toGridPos = slots[toIdx].gridPos 
                        });
                        queue.Enqueue(new StateSnapshot(newState, newMoves));
                    }
                }
            }
        }
        
        if (forwardOnly)
            Debug.LogWarning("No blue→green solution found; trying any direction.");
        else
            Debug.LogWarning("No solution found! Level may need redesign.");
        return new List<MoveStep>();
    }
    
    /// <summary>
    /// Check if current state is the solved state.
    /// Solved = each stack is either empty or all same type.
    /// </summary>
    private bool IsSolved(List<List<BuildingStyleSO>> stacks, List<SlotData> originalSlots)
    {
        int totalCapacity = stackHeight + 1;

        for (int i = 0; i < stacks.Count; i++)
        {
            // Empty target slots must end empty.
            if (originalSlots[i].buildingStyle == null)
            {
                if (stacks[i].Count != 0)
                    return false;
                continue;
            }

            if (stacks[i].Count != totalCapacity)
                return false;
            
            // All floors must be the same type
            var firstStyle = stacks[i][0];
            for (int j = 1; j < stacks[i].Count; j++)
            {
                if (stacks[i][j] != firstStyle)
                    return false; // Mixed types
            }
            
            // All floors must match the slot's target style
            if (originalSlots[i].buildingStyle != firstStyle)
                return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Create a deep copy of the current game state.
    /// </summary>
    private List<List<BuildingStyleSO>> CreateStateSnapshot(List<SlotData> slots)
    {
        var snapshot = new List<List<BuildingStyleSO>>();
        foreach (var slot in slots)
        {
            snapshot.Add(new List<BuildingStyleSO>(slot.floorStyles));
        }
        return snapshot;
    }
    
    private List<List<BuildingStyleSO>> CreateStateSnapshot(List<List<BuildingStyleSO>> stacks)
    {
        var snapshot = new List<List<BuildingStyleSO>>();
        foreach (var stack in stacks)
        {
            snapshot.Add(new List<BuildingStyleSO>(stack));
        }
        return snapshot;
    }
    
    /// <summary>
    /// Hash a state for visited tracking.
    /// </summary>
    private string StateToHash(List<List<BuildingStyleSO>> stacks)
    {
        var parts = new List<string>();
        foreach (var stack in stacks)
        {
            var stackHash = string.Join(",", stack.Select(s => s != null ? s.buildingName : "empty"));
            parts.Add($"[{stackHash}]");
        }
        return string.Join("|", parts);
    }
}
