using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Spire.Analyzers.Utils.FlowAnalysis;

public enum ReadOrWrite : byte { Read, Write }

/// Query interface for flow analysis results within a single method body.
public sealed class FlowAnalysisResult
{
    /// Per-operation state snapshots. Key is a top-level BasicBlock operation.
    /// Value maps tracked variable symbol → state AFTER that operation.
    private readonly Dictionary<IOperation, Dictionary<ISymbol, VariableState>> _operationStates;

    /// Per-variable operation timeline — ordered list of operations affecting each variable.
    private readonly Dictionary<ISymbol, List<(IOperation Operation, ReadOrWrite Kind, VariableState StateAfter)>> _variableTimelines;

    /// Reverse index: maps any IOperation (including nested) to its enclosing top-level block operation.
    private readonly Dictionary<IOperation, IOperation> _nestedToTopLevel;

    public FlowAnalysisResult(
        Dictionary<IOperation, Dictionary<ISymbol, VariableState>> operationStates,
        Dictionary<ISymbol, List<(IOperation, ReadOrWrite, VariableState)>> variableTimelines,
        Dictionary<IOperation, IOperation> nestedToTopLevel)
    {
        _operationStates = operationStates;
        _variableTimelines = variableTimelines;
        _nestedToTopLevel = nestedToTopLevel;
    }

    /// Returns the state of a variable at the program point of the given operation.
    /// For nested operations, walks up to the enclosing top-level block operation.
    /// Returns null if the variable is not tracked at that point.
    public VariableState? GetStateAt(IOperation operation, ISymbol variable)
    {
        var topLevel = ResolveTopLevel(operation);
        if (topLevel is null)
            return null;

        if (_operationStates.TryGetValue(topLevel, out var states)
            && states.TryGetValue(variable, out var state))
            return state;

        return null;
    }

    /// Returns all operations affecting a variable, in execution order.
    public IReadOnlyList<(IOperation Operation, ReadOrWrite Kind, VariableState StateAfter)>?
        GetOperationsFor(ISymbol variable)
    {
        return _variableTimelines.TryGetValue(variable, out var timeline)
            ? timeline
            : null;
    }

    private IOperation? ResolveTopLevel(IOperation operation)
    {
        if (_operationStates.ContainsKey(operation))
            return operation;

        return _nestedToTopLevel.TryGetValue(operation, out var topLevel)
            ? topLevel
            : null;
    }
}
