using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Analyzers;

/// Analyzes switch expression/statement patterns to determine which
/// discriminated union variants are covered.
internal sealed class SwitchCoverage
{
    public HashSet<string> CoveredVariants { get; } = new HashSet<string>();
    public HashSet<string> GuardedVariants { get; } = new HashSet<string>();
    public bool HasWildcard { get; set; }
    public bool CoversNull { get; set; }

    public ImmutableArray<string> GetMissingVariants(ImmutableArray<string> allVariants)
    {
        return allVariants.Where(v => !CoveredVariants.Contains(v)).ToImmutableArray();
    }
}

/// Extracts variant coverage from switch patterns on discriminated unions (struct, record, and class).
internal static class PatternAnalyzer
{
    public static SwitchCoverage AnalyzeExpression(
        ISwitchExpressionOperation switchOp, UnionTypeInfo info)
    {
        var coverage = new SwitchCoverage();

        foreach (var arm in switchOp.Arms)
        {
            var variants = new List<string>();
            bool isWildcard = CollectVariants(arm.Pattern, info, variants);

            if (isWildcard)
            {
                coverage.HasWildcard = true;
                continue;
            }

            bool hasGuard = arm.Guard is not null;

            if (!hasGuard && IsNullPattern(arm.Pattern))
                coverage.CoversNull = true;

            foreach (var v in variants)
            {
                if (hasGuard)
                    coverage.GuardedVariants.Add(v);
                else
                    coverage.CoveredVariants.Add(v);
            }
        }

        return coverage;
    }

    public static SwitchCoverage AnalyzeStatement(
        ISwitchOperation switchOp, UnionTypeInfo info)
    {
        var coverage = new SwitchCoverage();

        foreach (var caseOp in switchOp.Cases)
        {
            foreach (var clause in caseOp.Clauses)
            {
                if (clause is IDefaultCaseClauseOperation)
                {
                    coverage.HasWildcard = true;
                    coverage.CoversNull = true;
                    continue;
                }

                if (clause is IPatternCaseClauseOperation patternClause)
                {
                    var variants = new List<string>();
                    bool isWildcard = CollectVariants(patternClause.Pattern, info, variants);

                    if (isWildcard)
                    {
                        coverage.HasWildcard = true;
                        continue;
                    }

                    bool hasGuard = patternClause.Guard is not null;

                    if (!hasGuard && IsNullPattern(patternClause.Pattern))
                        coverage.CoversNull = true;

                    foreach (var v in variants)
                    {
                        if (hasGuard)
                            coverage.GuardedVariants.Add(v);
                        else
                            coverage.CoveredVariants.Add(v);
                    }
                }
            }
        }

        return coverage;
    }

    /// Checks whether a pattern covers the null value.
    internal static bool IsNullPattern(IPatternOperation pattern)
    {
        switch (pattern)
        {
            case IDiscardPatternOperation:
                return true;
            case IDeclarationPatternOperation decl:
                if (decl.Syntax is Microsoft.CodeAnalysis.CSharp.Syntax.VarPatternSyntax)
                    return true;
                return decl.MatchesNull;
            case IConstantPatternOperation c:
                return c.Value.ConstantValue is { HasValue: true, Value: null };
            case IBinaryPatternOperation { OperatorKind: BinaryOperatorKind.Or } binary:
                return IsNullPattern(binary.LeftPattern) || IsNullPattern(binary.RightPattern);
            default:
                return false;
        }
    }

    /// Collects variant names from a pattern. Returns true if the pattern is a wildcard.
    internal static bool CollectVariants(
        IPatternOperation pattern, UnionTypeInfo info, List<string> variants)
    {
        switch (pattern)
        {
            case IDiscardPatternOperation:
                return true;

            case IDeclarationPatternOperation declPattern:
                // `var x` is always a wildcard (MatchesNull=true, MatchedType=null).
                // Must check BEFORE MatchesNull guard.
                if (declPattern.Syntax is Microsoft.CodeAnalysis.CSharp.Syntax.VarPatternSyntax)
                    return true;
                // `null` pattern check — not a variant match
                if (declPattern.MatchesNull)
                    return false;
                if (declPattern.InputType is not null &&
                    SymbolEqualityComparer.Default.Equals(
                        declPattern.InputType, declPattern.MatchedType))
                    return true;
                // Record union: `Option<int>.Some some` — type narrows to a variant
                if (info.IsRecordUnion && declPattern.MatchedType is INamedTypeSymbol declMatchedType)
                {
                    var variantName = TryGetRecordVariant(declMatchedType, info);
                    if (variantName is not null)
                    {
                        variants.Add(variantName);
                        return false;
                    }
                }
                return false;

            case IRecursivePatternOperation recursive:
                return HandleRecursivePattern(recursive, info, variants);

            case IBinaryPatternOperation binary:
                return HandleBinaryPattern(binary, info, variants);

            case INegatedPatternOperation:
                // `not X` does not positively cover a variant
                return false;

            case ITypePatternOperation typePattern:
                // Record union: bare type pattern `Option<int>.None =>` (no binding, no properties)
                if (info.IsRecordUnion && typePattern.MatchedType is INamedTypeSymbol typeMatchedType)
                {
                    var variantName = TryGetRecordVariant(typeMatchedType, info);
                    if (variantName is not null)
                    {
                        variants.Add(variantName);
                        return false;
                    }
                }
                return false;

            case IConstantPatternOperation constant:
                // A bare constant could match a Kind value — unlikely in practice for structs
                return TryMatchKindConstant(constant, info, variants);

            default:
                return false;
        }
    }

    private static bool HandleRecursivePattern(
        IRecursivePatternOperation recursive, UnionTypeInfo info, List<string> variants)
    {
        // `var x` on a type with Deconstruct may produce a recursive pattern.
        if (recursive.Syntax is Microsoft.CodeAnalysis.CSharp.Syntax.VarPatternSyntax
            or Microsoft.CodeAnalysis.CSharp.Syntax.DiscardPatternSyntax)
            return true;

        // Record union: `Option<int>.Some { Value: var v }` or just `Option<int>.None`
        // The MatchedType tells us which variant is being matched.
        if (info.IsRecordUnion && recursive.MatchedType is INamedTypeSymbol matchedType)
        {
            var variantName = TryGetRecordVariant(matchedType, info);
            if (variantName is not null)
            {
                variants.Add(variantName);
                return false;
            }
        }

        // Struct union: Deconstruction pattern: (Shape.Kind.Circle, double r)
        // The first subpattern identifies the variant (Kind enum value).
        // If the first subpattern is a wildcard (discard/var), the entire pattern is a wildcard
        // (e.g., `var x` on a struct with Deconstruct becomes `(var _, var _)`).
        if (!recursive.DeconstructionSubpatterns.IsEmpty)
        {
            var firstSub = recursive.DeconstructionSubpatterns[0];
            return CollectVariants(firstSub, info, variants);
        }

        // Struct union: Property pattern: { kind: Shape.Kind.Circle }
        // Find the kind member and delegate its pattern to CollectVariants.
        if (!recursive.PropertySubpatterns.IsEmpty)
        {
            foreach (var propSub in recursive.PropertySubpatterns)
            {
                if (IsKindMember(propSub))
                    return CollectVariants(propSub.Pattern, info, variants);
            }
        }

        return false;
    }

    private static bool IsKindMember(IPropertySubpatternOperation propSub)
    {
        if (propSub.Member is IPropertyReferenceOperation propRef)
            return propRef.Property.Name == "kind";
        if (propSub.Member is IFieldReferenceOperation fieldRef)
            return fieldRef.Field.Name == "kind";
        return false;
    }

    private static bool HandleBinaryPattern(
        IBinaryPatternOperation binary, UnionTypeInfo info, List<string> variants)
    {
        if (binary.OperatorKind == BinaryOperatorKind.Or)
        {
            // `or` pattern: both branches contribute variants
            bool leftWild = CollectVariants(binary.LeftPattern, info, variants);
            bool rightWild = CollectVariants(binary.RightPattern, info, variants);
            return leftWild || rightWild;
        }

        if (binary.OperatorKind == BinaryOperatorKind.And)
        {
            // `and` pattern: take the variant-determining branch
            var leftVariants = new List<string>();
            bool leftWild = CollectVariants(binary.LeftPattern, info, leftVariants);

            var rightVariants = new List<string>();
            bool rightWild = CollectVariants(binary.RightPattern, info, rightVariants);

            if (leftVariants.Count > 0)
                variants.AddRange(leftVariants);
            else if (rightVariants.Count > 0)
                variants.AddRange(rightVariants);

            return leftWild && rightWild;
        }

        return false;
    }

    private static bool TryMatchKindConstant(
        IConstantPatternOperation constant, UnionTypeInfo info, List<string> variants)
    {
        string? variant = ResolveKindConstant(constant, info);
        if (variant is not null)
        {
            variants.Add(variant);
            return false;
        }
        return false;
    }

    /// Resolves a constant pattern to a variant name by checking if it matches a Kind enum value.
    private static string? ResolveKindConstant(IConstantPatternOperation constant, UnionTypeInfo info)
    {
        var valueOp = constant.Value;
        if (valueOp is null || valueOp.Type is null)
            return null;

        // Check if the constant's type is the Kind enum
        var kindEnum = GetKindEnumType(info);
        if (kindEnum is null)
            return null;

        if (!SymbolEqualityComparer.Default.Equals(valueOp.Type, kindEnum))
            return null;

        // Try to get the constant value (an int ordinal)
        if (!valueOp.ConstantValue.HasValue)
            return null;

        // The constant value's type matches the Kind enum's underlying type
        // (byte, ushort, or uint depending on variant count).
        var value = valueOp.ConstantValue.Value;
        int ordinal;
        switch (value)
        {
            case byte b: ordinal = b; break;
            case ushort u: ordinal = u; break;
            case int i: ordinal = i; break;
            case uint ui: ordinal = (int)ui; break;
            default: return null;
        }

        if (ordinal >= 0 && ordinal < info.VariantNames.Length)
            return info.VariantNames[ordinal];

        return null;
    }

    /// Gets the Kind enum type from the union's variant names by finding the nested Kind enum.
    private static INamedTypeSymbol? GetKindEnumType(UnionTypeInfo info)
    {
        return info.KindEnumType;
    }

    /// For record unions, resolves a matched type to a variant name.
    /// Compares using OriginalDefinition so generic instantiations are handled correctly.
    private static string? TryGetRecordVariant(INamedTypeSymbol matchedType, UnionTypeInfo info)
    {
        var originalMatched = matchedType.OriginalDefinition;
        for (int i = 0; i < info.VariantTypes.Length; i++)
        {
            if (SymbolEqualityComparer.Default.Equals(
                    originalMatched, info.VariantTypes[i].OriginalDefinition))
                return info.VariantNames[i];
        }
        return null;
    }
}
