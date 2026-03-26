using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Spire.Analyzers.Utils.FlowAnalysis;

/// Applies IOperation transfer functions to update variable states within a basic block.
public static class TransferFunctions
{
    /// Processes a top-level operation in a basic block and returns updated states.
    /// Returns true if any state changed.
    public static bool Apply(
        IOperation operation,
        Dictionary<ISymbol, VariableState> states,
        TrackedSymbolSet symbols)
    {
        // CFG blocks wrap most statements in IExpressionStatementOperation — unwrap
        if (operation is IExpressionStatementOperation exprStmt)
            operation = exprStmt.Operation;

        return operation switch
        {
            ISimpleAssignmentOperation assignment => ApplyAssignment(assignment, states, symbols),
            ICompoundAssignmentOperation compound => ApplyFieldWrite(compound.Target, states, symbols),
            IIncrementOrDecrementOperation incDec => ApplyFieldWrite(incDec.Target, states, symbols),
            _ => false,
        };
    }

    private static bool ApplyAssignment(
        ISimpleAssignmentOperation assignment,
        Dictionary<ISymbol, VariableState> states,
        TrackedSymbolSet symbols)
    {
        var target = assignment.Target;
        var value = assignment.Value;

        // Field/property write: s.Field = val
        if (target is IFieldReferenceOperation fieldRef
            && fieldRef.Instance is ILocalReferenceOperation or IParameterReferenceOperation)
        {
            var variable = GetSymbol(fieldRef.Instance!);
            if (variable is null) return false;
            if (!states.TryGetValue(variable, out var state)) return false;
            if (fieldRef.Field.ContainingType is not INamedTypeSymbol containingType) return false;

            var ordinal = symbols.GetFieldOrdinal(containingType, fieldRef.Field);
            if (ordinal < 0) return false;

            var newState = state.WithFieldState(ordinal, InitState.Initialized);
            if (newState.Equals(state)) return false;
            states[variable] = newState;
            return true;
        }

        // Whole-variable assignment: s = expr
        if (target is ILocalReferenceOperation or IParameterReferenceOperation)
        {
            var variable = GetSymbol(target);
            if (variable is null) return false;
            if (!states.ContainsKey(variable)) return false;

            var newState = DetermineStateFromValue(value, states, symbols, variable);
            if (states.TryGetValue(variable, out var oldState) && newState.Equals(oldState))
                return false;
            states[variable] = newState;
            return true;
        }

        return false;
    }

    private static bool ApplyFieldWrite(
        IOperation target,
        Dictionary<ISymbol, VariableState> states,
        TrackedSymbolSet symbols)
    {
        if (target is not IFieldReferenceOperation fieldRef) return false;
        if (fieldRef.Instance is not (ILocalReferenceOperation or IParameterReferenceOperation))
            return false;

        var variable = GetSymbol(fieldRef.Instance!);
        if (variable is null) return false;
        if (!states.TryGetValue(variable, out var state)) return false;
        if (fieldRef.Field.ContainingType is not INamedTypeSymbol containingType) return false;

        var ordinal = symbols.GetFieldOrdinal(containingType, fieldRef.Field);
        if (ordinal < 0) return false;

        var newState = state.WithFieldState(ordinal, InitState.Initialized);
        if (newState.Equals(state)) return false;
        states[variable] = newState;
        return true;
    }

    /// Determines the VariableState that results from assigning `value` to a tracked variable.
    private static VariableState DetermineStateFromValue(
        IOperation value,
        Dictionary<ISymbol, VariableState> states,
        TrackedSymbolSet symbols,
        ISymbol targetVariable)
    {
        // Unwrap conversions
        while (value is IConversionOperation conv)
            value = conv.Operand;

        var currentState = states.TryGetValue(targetVariable, out var s)
            ? s : default;

        // default(T) or default literal
        if (value is IDefaultValueOperation)
            return currentState.WithAllFields(InitState.Default).WithNullState(NullState.Null);

        // new T(...) with arguments → Initialized
        if (value is IObjectCreationOperation objCreate)
        {
            if (objCreate.Arguments.Length > 0 || objCreate.Initializer is not null)
                return currentState.WithAllFields(InitState.Initialized).WithNullState(NullState.NotNull);

            // new T() with no args on [EnforceInitialization] — same as default
            return currentState.WithAllFields(InitState.Default).WithNullState(NullState.NotNull);
        }

        // null literal
        if (value.ConstantValue is { HasValue: true, Value: null })
            return currentState.WithNullState(NullState.Null);

        // Method call returning [EnforceInitialization] type → assume Initialized
        if (value is IInvocationOperation invocation && symbols.EnforceInitializationType is not null)
        {
            var returnType = invocation.TargetMethod.ReturnType;
            if (returnType is INamedTypeSymbol named
                && EnforceInitializationChecks.HasEnforceInitializationAttribute(named, symbols.EnforceInitializationType))
                return currentState.WithAllFields(InitState.Initialized).WithNullState(NullState.NotNull);
        }

        // Unknown source → conservative
        return currentState.WithAllFields(InitState.MaybeDefault).WithNullState(NullState.MaybeNull);
    }

    private static ISymbol? GetSymbol(IOperation operation)
    {
        return operation switch
        {
            ILocalReferenceOperation local => local.Local,
            IParameterReferenceOperation param => param.Parameter,
            _ => null,
        };
    }
}
