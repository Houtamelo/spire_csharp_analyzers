using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Spire.Analyzers.Utils;

namespace Spire.Analyzers.Rules;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SPIRE015ExhaustiveEnumSwitchAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Descriptors.SPIRE015_ExhaustiveEnumSwitch);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(compilationCtx =>
        {
            var enforceType = compilationCtx.Compilation.GetTypeByMetadataName(
                "Spire.Analyzers.EnforceExhaustivenessAttribute");

            if (enforceType is null)
                return;

            compilationCtx.RegisterOperationAction(
                ctx => AnalyzeSwitch(ctx, enforceType),
                OperationKind.Switch, OperationKind.SwitchExpression);
        });
    }

    private static void AnalyzeSwitch(OperationAnalysisContext context, INamedTypeSymbol enforceType)
    {
        INamedTypeSymbol? enumType;
        Location switchKeywordLocation;

        if (context.Operation is ISwitchOperation switchOp)
        {
            enumType = GetEnumType(switchOp.Value.Type);
            if (switchOp.Syntax is not SwitchStatementSyntax switchStmtSyntax) return;
            switchKeywordLocation = switchStmtSyntax.SwitchKeyword.GetLocation();
        }
        else if (context.Operation is ISwitchExpressionOperation switchExprOp)
        {
            enumType = GetEnumType(switchExprOp.Value.Type);
            if (switchExprOp.Syntax is not SwitchExpressionSyntax switchExprSyntax) return;
            switchKeywordLocation = switchExprSyntax.SwitchKeyword.GetLocation();
        }
        else
        {
            return;
        }

        if (enumType is null || enumType.TypeKind != TypeKind.Enum)
            return;

        if (!AttributeHelper.HasOrInheritsAttribute(enumType, enforceType))
            return;

        var allMembers = GetEnumMembers(enumType);
        if (allMembers.Count == 0)
            return;

        var isFlags = HasFlagsAttribute(enumType);
        var coveredValues = new HashSet<object>(ObjectEqualityComparer.Instance);

        if (context.Operation is ISwitchOperation switchStmt)
            CollectCoverageFromSwitchStatement(switchStmt, allMembers, coveredValues);
        else if (context.Operation is ISwitchExpressionOperation switchExpr)
            CollectCoverageFromSwitchExpression(switchExpr, allMembers, coveredValues);

        if (isFlags)
            ExpandFlagsCoverage(allMembers, coveredValues);
        var missingGroups = new List<string>();
        var reportedValues = new HashSet<object>(ObjectEqualityComparer.Instance);

        foreach (var member in allMembers)
        {
            var val = member.ConstantValue!;
            if (reportedValues.Contains(val))
                continue; // Already handled this group (alias)

            reportedValues.Add(val);

            if (!coveredValues.Contains(val))
                missingGroups.Add(member.Name);
        }

        if (missingGroups.Count == 0)
            return;

        var missingStr = string.Join(", ", missingGroups);

        var properties = ImmutableDictionary<string, string?>.Empty
            .Add("MissingMembers", missingStr);

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptors.SPIRE015_ExhaustiveEnumSwitch,
                switchKeywordLocation,
                properties,
                enumType.Name,
                missingStr));
    }

    /// Unwraps Nullable<T> and returns the named type, or null if not applicable.
    private static INamedTypeSymbol? GetEnumType(ITypeSymbol? type)
    {
        if (type is not INamedTypeSymbol named)
            return null;

        // Unwrap Nullable<T>
        if (named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
            && named.TypeArguments.Length == 1)
        {
            return named.TypeArguments[0] as INamedTypeSymbol;
        }

        return named;
    }

    private static bool HasFlagsAttribute(INamedTypeSymbol enumType)
    {
        foreach (var attr in enumType.GetAttributes())
        {
            var attrClass = attr.AttributeClass;
            if (attrClass is null) continue;

            if (attrClass.Name == "FlagsAttribute"
                && attrClass.ContainingNamespace?.ToDisplayString() == "System")
                return true;
        }

        return false;
    }

    /// Returns all named enum constant members in declaration order.
    private static List<IFieldSymbol> GetEnumMembers(INamedTypeSymbol enumType)
    {
        var result = new List<IFieldSymbol>();
        foreach (var member in enumType.GetMembers())
        {
            if (member is IFieldSymbol { HasConstantValue: true, IsConst: true } field)
                result.Add(field);
        }

        return result;
    }

    private static void CollectCoverageFromSwitchStatement(
        ISwitchOperation switchOp,
        List<IFieldSymbol> allMembers,
        HashSet<object> coveredValues)
    {
        foreach (var switchCase in switchOp.Cases)
        {
            // Check if this case section has a when guard
            var hasGuard = HasWhenGuard(switchCase);

            foreach (var clause in switchCase.Clauses)
            {
                if (clause is IDefaultCaseClauseOperation)
                    continue; // default: does not provide member coverage

                if (hasGuard)
                    continue; // when guard disqualifies all clauses in this case section

                if (clause is ISingleValueCaseClauseOperation singleValue)
                {
                    ExtractValueFromOperation(singleValue.Value, coveredValues);
                }
                else if (clause is IPatternCaseClauseOperation patternClause)
                {
                    ExtractCoverageFromPattern(patternClause.Pattern, coveredValues, allMembers);
                }
            }
        }
    }

    private static bool HasWhenGuard(ISwitchCaseOperation switchCase)
    {
        // The when guard appears as IPatternCaseClauseOperation.Guard on one of the clauses.
        // For ISingleValueCaseClauseOperation with when, Roslyn actually represents it as
        // IPatternCaseClauseOperation with a Guard. So we only need to check pattern clauses.
        foreach (var clause in switchCase.Clauses)
        {
            if (clause is IPatternCaseClauseOperation { Guard: not null })
                return true;
        }

        return false;
    }

    private static void CollectCoverageFromSwitchExpression(
        ISwitchExpressionOperation switchExprOp,
        List<IFieldSymbol> allMembers,
        HashSet<object> coveredValues)
    {
        foreach (var arm in switchExprOp.Arms)
        {
            // Skip arms with when guard
            if (arm.Guard != null)
                continue;

            ExtractCoverageFromPattern(arm.Pattern, coveredValues, allMembers);
        }
    }

    private static void ExtractValueFromOperation(IOperation valueOp, HashSet<object> coveredValues)
    {
        // Only count enum-typed constants — not raw int literals
        if (valueOp.ConstantValue.HasValue
            && valueOp.ConstantValue.Value != null
            && valueOp.Type?.TypeKind == TypeKind.Enum)
        {
            coveredValues.Add(valueOp.ConstantValue.Value);
        }
    }

    private static void ExtractCoverageFromPattern(
        IPatternOperation pattern,
        HashSet<object> coveredValues,
        List<IFieldSymbol> allMembers)
    {
        switch (pattern)
        {
            case IConstantPatternOperation constant:
                ExtractValueFromOperation(constant.Value, coveredValues);
                break;

            case IBinaryPatternOperation { OperatorKind: BinaryOperatorKind.Or } orPattern:
                ExtractCoverageFromPattern(orPattern.LeftPattern, coveredValues, allMembers);
                ExtractCoverageFromPattern(orPattern.RightPattern, coveredValues, allMembers);
                break;

            case IBinaryPatternOperation { OperatorKind: BinaryOperatorKind.And } andPattern:
            {
                // Intersect: only values covered by BOTH sides
                var leftSet = new HashSet<object>(ObjectEqualityComparer.Instance);
                var rightSet = new HashSet<object>(ObjectEqualityComparer.Instance);
                ExtractCoverageFromPattern(andPattern.LeftPattern, leftSet, allMembers);
                ExtractCoverageFromPattern(andPattern.RightPattern, rightSet, allMembers);
                foreach (var val in leftSet)
                {
                    if (rightSet.Contains(val))
                        coveredValues.Add(val);
                }

                break;
            }

            case INegatedPatternOperation negated:
            {
                // Covers all member values EXCEPT those matched by the inner pattern
                var innerSet = new HashSet<object>(ObjectEqualityComparer.Instance);
                ExtractCoverageFromPattern(negated.Pattern, innerSet, allMembers);

                var seenValues = new HashSet<object>(ObjectEqualityComparer.Instance);
                foreach (var member in allMembers)
                {
                    var val = member.ConstantValue!;
                    if (seenValues.Contains(val))
                        continue;
                    seenValues.Add(val);

                    if (!innerSet.Contains(val))
                        coveredValues.Add(val);
                }

                break;
            }

            // IDiscardPatternOperation, IDeclarationPatternOperation,
            // IRelationalPatternOperation, null-check patterns — no member coverage
        }
    }

    private static void ExpandFlagsCoverage(
        List<IFieldSymbol> allMembers,
        HashSet<object> coveredValues)
    {
        // For [Flags] enums: when a case handles value V,
        // all named members M where (M & V) == M are also covered.
        // Run a single pass — the initial coveredValues are all case-covered values.
        var toAdd = new List<object>();

        foreach (var coveredVal in coveredValues)
        {
            var coveredLong = ToLong(coveredVal);

            foreach (var member in allMembers)
            {
                var memberVal = member.ConstantValue!;
                var memberLong = ToLong(memberVal);

                // memberLong is a bitwise subset of coveredLong
                if ((memberLong & coveredLong) == memberLong)
                    toAdd.Add(memberVal);
            }
        }

        foreach (var val in toAdd)
            coveredValues.Add(val);
    }

    private static long ToLong(object value)
    {
        return value switch
        {
            byte b => b,
            sbyte sb => sb,
            short s => s,
            ushort us => us,
            int i => i,
            uint ui => (long)ui,
            long l => l,
            ulong ul => (long)ul,
            _ => 0,
        };
    }

    private sealed class ObjectEqualityComparer : IEqualityComparer<object>
    {
        public static readonly ObjectEqualityComparer Instance = new();

        private ObjectEqualityComparer() { }

        bool IEqualityComparer<object>.Equals(object? x, object? y)
        {
            if (x is null && y is null) return true;
            if (x is null || y is null) return false;
            return x.Equals(y);
        }

        public int GetHashCode(object obj) => obj.GetHashCode();
    }
}
