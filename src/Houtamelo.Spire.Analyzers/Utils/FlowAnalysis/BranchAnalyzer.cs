using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Houtamelo.Spire.Analyzers.Utils.FlowAnalysis;

public static class BranchAnalyzer
{
    /// Analyzes a branch condition and returns narrowed states for the two successors.
    /// trueState = state when condition matches ConditionKind.
    /// falseState = state when condition does NOT match.
    public static (VariableState trueState, VariableState falseState) AnalyzeBranch(
        IOperation branchValue,
        ControlFlowConditionKind conditionKind,
        VariableState currentState)
    {
        // IIsNullOperation: produced by CFG lowering for null checks
        if (branchValue is IIsNullOperation)
        {
            var nullState = currentState.WithNullState(NullState.Null);
            var notNullState = currentState.WithNullState(NullState.NotNull);

            // WhenTrue means "branch when condition is true"
            // IIsNullOperation is true when operand IS null
            return conditionKind == ControlFlowConditionKind.WhenTrue
                ? (nullState, notNullState)
                : (notNullState, nullState);
        }

        // IBinaryOperation: kind == EnumConstant checks
        if (branchValue is IBinaryOperation binary
            && (binary.OperatorKind == BinaryOperatorKind.Equals
             || binary.OperatorKind == BinaryOperatorKind.NotEquals))
        {
            var enumConstant = ExtractEnumConstantName(binary);
            if (enumConstant is not null)
            {
                var narrowed = currentState.WithKindState(
                    KindState.Known(ImmutableHashSet.Create(enumConstant)));

                var isEquals = binary.OperatorKind == BinaryOperatorKind.Equals;

                if (conditionKind == ControlFlowConditionKind.WhenTrue)
                    return isEquals
                        ? (narrowed, currentState)
                        : (currentState, narrowed);
                else
                    return isEquals
                        ? (currentState, narrowed)
                        : (narrowed, currentState);
            }
        }

        // No narrowing possible
        return (currentState, currentState);
    }

    private static string? ExtractEnumConstantName(IBinaryOperation binary)
    {
        if (TryGetFieldConstantName(binary.RightOperand, out var name))
            return name;
        if (TryGetFieldConstantName(binary.LeftOperand, out name))
            return name;
        return null;
    }

    private static bool TryGetFieldConstantName(IOperation operand, out string? name)
    {
        while (operand is IConversionOperation conv)
            operand = conv.Operand;

        if (operand is IFieldReferenceOperation fieldRef
            && fieldRef.Field.IsStatic
            && fieldRef.Field.ContainingType?.TypeKind == TypeKind.Enum)
        {
            name = fieldRef.Field.Name;
            return true;
        }

        name = null;
        return false;
    }
}
