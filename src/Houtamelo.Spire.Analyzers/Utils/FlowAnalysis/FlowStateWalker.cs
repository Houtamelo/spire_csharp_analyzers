using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Houtamelo.Spire.Analyzers.Utils.FlowAnalysis;

/// Worklist-based fixed-point flow analysis over a ControlFlowGraph.
public static class FlowStateWalker
{
    public static FlowAnalysisResult Analyze(ControlFlowGraph cfg, TrackedSymbolSet symbols, ISymbol owningSymbol)
    {
        var blocks = cfg.Blocks;
        var blockCount = blocks.Length;

        // Per-block input state: variable -> state
        var blockInputs = new Dictionary<ISymbol, VariableState>[blockCount];
        for (int i = 0; i < blockCount; i++)
            blockInputs[i] = new Dictionary<ISymbol, VariableState>(SymbolEqualityComparer.Default);

        // Initialize tracked locals/parameters
        InitializeTrackedVariables(cfg, symbols, blockInputs[0], owningSymbol);

        // Per-operation state snapshots (top-level operations only)
        var operationStates = new Dictionary<IOperation, Dictionary<ISymbol, VariableState>>();
        var variableTimelines = new Dictionary<ISymbol, List<(IOperation, ReadOrWrite, VariableState)>>(
            SymbolEqualityComparer.Default);
        var nestedToTopLevel = new Dictionary<IOperation, IOperation>();

        // Worklist
        var worklist = new Queue<int>();
        var inWorklist = new bool[blockCount];
        worklist.Enqueue(0);
        inWorklist[0] = true;

        while (worklist.Count > 0)
        {
            var blockOrdinal = worklist.Dequeue();
            inWorklist[blockOrdinal] = false;

            var block = blocks[blockOrdinal];
            if (block.Kind == BasicBlockKind.Entry || block.Kind == BasicBlockKind.Exit)
            {
                PropagateToSuccessors(block, blockInputs[blockOrdinal], blockInputs,
                    worklist, inWorklist, blocks);
                continue;
            }

            // Copy input state for this block
            var currentStates = new Dictionary<ISymbol, VariableState>(
                blockInputs[blockOrdinal], SymbolEqualityComparer.Default);

            // Apply transfer functions for each operation in the block
            foreach (var operation in block.Operations)
            {
                // Build nested -> top-level index
                IndexNestedOperations(operation, operation, nestedToTopLevel);

                // Snapshot state BEFORE this operation
                operationStates[operation] = new Dictionary<ISymbol, VariableState>(
                    currentStates, SymbolEqualityComparer.Default);

                // Apply transfer function
                TransferFunctions.Apply(operation, currentStates, symbols);
            }

            // Handle branch narrowing
            if (block.ConditionalSuccessor is not null && block.BranchValue is not null)
            {
                var narrowedTrue = new Dictionary<ISymbol, VariableState>(
                    currentStates, SymbolEqualityComparer.Default);
                var narrowedFalse = new Dictionary<ISymbol, VariableState>(
                    currentStates, SymbolEqualityComparer.Default);

                foreach (var kvp in currentStates)
                {
                    var (trueState, falseState) = BranchAnalyzer.AnalyzeBranch(
                        block.BranchValue, block.ConditionKind, kvp.Value);
                    narrowedTrue[kvp.Key] = trueState;
                    narrowedFalse[kvp.Key] = falseState;
                }

                if (block.ConditionalSuccessor.Destination is not null)
                    MergeAndEnqueue(block.ConditionalSuccessor.Destination, narrowedTrue,
                        blockInputs, worklist, inWorklist, blocks);

                if (block.FallThroughSuccessor?.Destination is not null)
                    MergeAndEnqueue(block.FallThroughSuccessor.Destination, narrowedFalse,
                        blockInputs, worklist, inWorklist, blocks);
            }
            else
            {
                PropagateToSuccessors(block, currentStates, blockInputs,
                    worklist, inWorklist, blocks);
            }
        }

        return new FlowAnalysisResult(operationStates, variableTimelines, nestedToTopLevel);
    }

    private static void InitializeTrackedVariables(
        ControlFlowGraph cfg, TrackedSymbolSet symbols,
        Dictionary<ISymbol, VariableState> entryState,
        ISymbol owningSymbol)
    {
        // Parameters come from the owning method, NOT from region.Locals
        // (ControlFlowRegion.Locals only contains ILocalSymbol, never IParameterSymbol)
        if (owningSymbol is IMethodSymbol method)
        {
            foreach (var param in method.Parameters)
            {
                if (param.Type is INamedTypeSymbol paramType
                    && symbols.TryGetFieldOrdinals(paramType, out _))
                {
                    entryState[param] = symbols.CreateInitialState(
                        paramType, InitState.Initialized, NullState.NotNull);
                }
            }
        }

        // Local variables come from CFG regions
        CollectLocals(cfg.Root, symbols, entryState);
    }

    private static void CollectLocals(
        ControlFlowRegion region, TrackedSymbolSet symbols,
        Dictionary<ISymbol, VariableState> entryState)
    {
        foreach (var local in region.Locals)
        {
            if (local.Type is INamedTypeSymbol namedType
                && symbols.TryGetFieldOrdinals(namedType, out _))
            {
                entryState[local] = symbols.CreateInitialState(
                    namedType, InitState.Default, NullState.NotNull);
            }
        }

        foreach (var nested in region.NestedRegions)
            CollectLocals(nested, symbols, entryState);
    }

    private static void PropagateToSuccessors(
        BasicBlock block,
        Dictionary<ISymbol, VariableState> currentStates,
        Dictionary<ISymbol, VariableState>[] blockInputs,
        Queue<int> worklist, bool[] inWorklist,
        ImmutableArray<BasicBlock> blocks)
    {
        if (block.FallThroughSuccessor?.Destination is { } fallThrough)
            MergeAndEnqueue(fallThrough, currentStates, blockInputs, worklist, inWorklist, blocks);

        if (block.ConditionalSuccessor?.Destination is { } conditional)
            MergeAndEnqueue(conditional, currentStates, blockInputs, worklist, inWorklist, blocks);
    }

    private static void MergeAndEnqueue(
        BasicBlock destination,
        Dictionary<ISymbol, VariableState> incomingStates,
        Dictionary<ISymbol, VariableState>[] blockInputs,
        Queue<int> worklist, bool[] inWorklist,
        ImmutableArray<BasicBlock> blocks)
    {
        var destOrdinal = destination.Ordinal;
        var destInputs = blockInputs[destOrdinal];
        var changed = false;

        foreach (var kvp in incomingStates)
        {
            if (destInputs.TryGetValue(kvp.Key, out var existing))
            {
                var merged = VariableState.Merge(existing, kvp.Value);
                if (!merged.Equals(existing))
                {
                    destInputs[kvp.Key] = merged;
                    changed = true;
                }
            }
            else
            {
                destInputs[kvp.Key] = kvp.Value;
                changed = true;
            }
        }

        if (changed && !inWorklist[destOrdinal])
        {
            worklist.Enqueue(destOrdinal);
            inWorklist[destOrdinal] = true;
        }
    }

    private static void IndexNestedOperations(
        IOperation current, IOperation topLevel,
        Dictionary<IOperation, IOperation> index)
    {
        foreach (var child in current.ChildOperations)
        {
            index[child] = topLevel;
            IndexNestedOperations(child, topLevel, index);
        }
    }
}
