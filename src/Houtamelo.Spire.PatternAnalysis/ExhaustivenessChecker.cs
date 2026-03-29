using System.Collections.Generic;
using System.Collections.Immutable;
using Houtamelo.Spire.PatternAnalysis.Algorithm;
using Houtamelo.Spire.PatternAnalysis.Resolution;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Houtamelo.Spire.PatternAnalysis;

/// Public entry point that wires DomainResolver, PatternMatrix, and DecisionTreeBuilder together.
internal static class ExhaustivenessChecker
{
    public static ExhaustivenessResult Check(
        Compilation compilation,
        ISwitchExpressionOperation switchExpr)
    {
        var resolver = new DomainResolver(compilation, new TypeHierarchyResolver());
        var matrix = PatternMatrix.Build(switchExpr, resolver);
        return DecisionTreeBuilder.Check(matrix);
    }

    public static ExhaustivenessResult Check(
        Compilation compilation,
        ISwitchOperation switchStmt)
    {
        var resolver = new DomainResolver(compilation, new TypeHierarchyResolver());
        var matrix = PatternMatrix.Build(switchStmt, resolver);
        return DecisionTreeBuilder.Check(matrix);
    }

    /// Extracts variant names from a pattern on a discriminated union.
    /// Used by FieldAccessSafetyAnalyzer for guard detection.
    /// Returns true if the pattern is a wildcard (matches all variants).
    internal static bool CollectVariants(
        IPatternOperation pattern,
        INamedTypeSymbol? kindEnumType,
        ImmutableArray<string> variantNames,
        ImmutableArray<INamedTypeSymbol> variantTypes,
        bool isStructUnion,
        List<string> variants)
    {
        bool isRecordUnion = !isStructUnion;

        switch (pattern)
        {
            case IDiscardPatternOperation:
                return true;

            case IDeclarationPatternOperation declPattern:
                if (declPattern.Syntax is Microsoft.CodeAnalysis.CSharp.Syntax.VarPatternSyntax)
                    return true;
                if (declPattern.MatchesNull)
                    return false;
                if (declPattern.InputType is not null &&
                    SymbolEqualityComparer.Default.Equals(
                        declPattern.InputType, declPattern.MatchedType))
                    return true;
                if (isRecordUnion && declPattern.MatchedType is INamedTypeSymbol declMatchedType)
                {
                    var name = TryGetRecordVariant(declMatchedType, variantTypes, variantNames);
                    if (name is not null)
                    {
                        variants.Add(name);
                        return false;
                    }
                }
                return false;

            case IRecursivePatternOperation recursive:
                return CollectVariantsRecursive(
                    recursive, kindEnumType, variantNames, variantTypes,
                    isStructUnion, isRecordUnion, variants);

            case IBinaryPatternOperation binary:
                return CollectVariantsBinary(
                    binary, kindEnumType, variantNames, variantTypes,
                    isStructUnion, variants);

            case INegatedPatternOperation:
                return false;

            case ITypePatternOperation typePattern:
                if (isRecordUnion && typePattern.MatchedType is INamedTypeSymbol typeMatchedType)
                {
                    var name = TryGetRecordVariant(typeMatchedType, variantTypes, variantNames);
                    if (name is not null)
                    {
                        variants.Add(name);
                        return false;
                    }
                }
                return false;

            case IConstantPatternOperation constant:
                return CollectVariantsKindConstant(constant, kindEnumType, variantNames, variants);

            default:
                return false;
        }
    }

    private static bool CollectVariantsRecursive(
        IRecursivePatternOperation recursive,
        INamedTypeSymbol? kindEnumType,
        ImmutableArray<string> variantNames,
        ImmutableArray<INamedTypeSymbol> variantTypes,
        bool isStructUnion,
        bool isRecordUnion,
        List<string> variants)
    {
        if (recursive.Syntax is Microsoft.CodeAnalysis.CSharp.Syntax.VarPatternSyntax
            or Microsoft.CodeAnalysis.CSharp.Syntax.DiscardPatternSyntax)
            return true;

        if (isRecordUnion && recursive.MatchedType is INamedTypeSymbol matchedType)
        {
            var name = TryGetRecordVariant(matchedType, variantTypes, variantNames);
            if (name is not null)
            {
                variants.Add(name);
                return false;
            }
        }

        // Struct union: Deconstruction pattern — first subpattern is Kind
        if (!recursive.DeconstructionSubpatterns.IsEmpty)
        {
            return CollectVariants(
                recursive.DeconstructionSubpatterns[0],
                kindEnumType, variantNames, variantTypes, isStructUnion, variants);
        }

        // Struct union: Property pattern — find kind member
        if (!recursive.PropertySubpatterns.IsEmpty)
        {
            foreach (var propSub in recursive.PropertySubpatterns)
            {
                if (IsKindMember(propSub))
                {
                    return CollectVariants(
                        propSub.Pattern,
                        kindEnumType, variantNames, variantTypes, isStructUnion, variants);
                }
            }
        }

        return false;
    }

    private static bool CollectVariantsBinary(
        IBinaryPatternOperation binary,
        INamedTypeSymbol? kindEnumType,
        ImmutableArray<string> variantNames,
        ImmutableArray<INamedTypeSymbol> variantTypes,
        bool isStructUnion,
        List<string> variants)
    {
        if (binary.OperatorKind == BinaryOperatorKind.Or)
        {
            bool leftWild = CollectVariants(
                binary.LeftPattern, kindEnumType, variantNames, variantTypes, isStructUnion, variants);
            bool rightWild = CollectVariants(
                binary.RightPattern, kindEnumType, variantNames, variantTypes, isStructUnion, variants);
            return leftWild || rightWild;
        }

        if (binary.OperatorKind == BinaryOperatorKind.And)
        {
            var leftVariants = new List<string>();
            bool leftWild = CollectVariants(
                binary.LeftPattern, kindEnumType, variantNames, variantTypes, isStructUnion, leftVariants);

            var rightVariants = new List<string>();
            bool rightWild = CollectVariants(
                binary.RightPattern, kindEnumType, variantNames, variantTypes, isStructUnion, rightVariants);

            if (leftVariants.Count > 0)
                variants.AddRange(leftVariants);
            else if (rightVariants.Count > 0)
                variants.AddRange(rightVariants);

            return leftWild && rightWild;
        }

        return false;
    }

    private static bool CollectVariantsKindConstant(
        IConstantPatternOperation constant,
        INamedTypeSymbol? kindEnumType,
        ImmutableArray<string> variantNames,
        List<string> variants)
    {
        if (kindEnumType is null)
            return false;

        var valueOp = constant.Value;
        if (valueOp?.Type is null)
            return false;

        if (!SymbolEqualityComparer.Default.Equals(valueOp.Type, kindEnumType))
            return false;

        if (!valueOp.ConstantValue.HasValue)
            return false;

        var value = valueOp.ConstantValue.Value;
        int ordinal;
        switch (value)
        {
            case byte b: ordinal = b; break;
            case ushort u: ordinal = u; break;
            case int i: ordinal = i; break;
            case uint ui: ordinal = (int)ui; break;
            default: return false;
        }

        if (ordinal >= 0 && ordinal < variantNames.Length)
        {
            variants.Add(variantNames[ordinal]);
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

    /// For record unions, resolves a matched type to a variant name.
    /// Compares using OriginalDefinition so generic instantiations are handled correctly.
    private static string? TryGetRecordVariant(
        INamedTypeSymbol matchedType,
        ImmutableArray<INamedTypeSymbol> variantTypes,
        ImmutableArray<string> variantNames)
    {
        var originalMatched = matchedType.OriginalDefinition;
        for (int i = 0; i < variantTypes.Length; i++)
        {
            if (SymbolEqualityComparer.Default.Equals(
                    originalMatched, variantTypes[i].OriginalDefinition))
                return variantNames[i];
        }
        return null;
    }
}
