using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Houtamelo.Spire.PatternAnalysis;

/// Recognizes the generated boolean Is{Variant} projections of struct unions.
/// The generator does not attach a marker attribute, so the owning union and
/// canonical variant metadata are part of the identity check.
internal static class GeneratedVariantPredicate
{
    public static bool TryGetConditionVariants(
        IOperation condition,
        INamedTypeSymbol unionType,
        ImmutableArray<string> variantNames,
        out ImmutableArray<string> variants)
    {
        if (TryGetPredicate(condition, unionType, variantNames, out var predicate))
        {
            variants = ImmutableArray.Create(predicate.VariantName);
            return true;
        }

        if (condition is IUnaryOperation { OperatorKind: UnaryOperatorKind.Not } unary &&
            TryGetConditionVariants(unary.Operand, unionType, variantNames, out var negated))
        {
            variants = Complement(variantNames, negated);
            return true;
        }

        if (condition is IBinaryOperation binary &&
            binary.OperatorKind is BinaryOperatorKind.Equals or BinaryOperatorKind.NotEquals)
        {
            if (TryGetPredicate(binary.LeftOperand, unionType, variantNames, out predicate) &&
                TryGetBooleanConstant(binary.RightOperand, out var rightValue))
            {
                variants = ResolveComparison(predicate, rightValue, binary.OperatorKind, variantNames);
                return true;
            }

            if (TryGetPredicate(binary.RightOperand, unionType, variantNames, out predicate) &&
                TryGetBooleanConstant(binary.LeftOperand, out var leftValue))
            {
                variants = ResolveComparison(predicate, leftValue, binary.OperatorKind, variantNames);
                return true;
            }
        }

        variants = ImmutableArray<string>.Empty;
        return false;
    }

    public static bool TryGetPropertyPatternVariants(
        IPropertySubpatternOperation propertySubpattern,
        INamedTypeSymbol unionType,
        ImmutableArray<string> variantNames,
        out ImmutableArray<string> variants)
    {
        variants = ImmutableArray<string>.Empty;
        if (propertySubpattern.Member is not IPropertyReferenceOperation propertyReference ||
            propertyReference.Instance is not IInstanceReferenceOperation ||
            !TryGetGeneratedProperty(propertyReference.Property, unionType, variantNames, out var variantName))
        {
            return false;
        }

        if (!TryGetPatternBoolean(propertySubpattern.Pattern, out var positive))
            return false;

        variants = positive
        ? ImmutableArray.Create(variantName)
        : Except(variantNames, variantName);
        return true;
    }

    public static bool TryGetGeneratedProperty(
        IPropertyReferenceOperation operation,
        INamedTypeSymbol unionType,
        ImmutableArray<string> variantNames,
        out string variantName)
        => TryGetGeneratedProperty(operation.Property, unionType, variantNames, out variantName);

    public static bool TryGetGeneratedProperty(
        IPropertySymbol property,
        INamedTypeSymbol unionType,
        ImmutableArray<string> variantNames,
        out string variantName)
    {
        variantName = string.Empty;
        if (property.IsStatic || property.Parameters.Length != 0 ||
            property.GetMethod is null ||
            property.Type.SpecialType != SpecialType.System_Boolean ||
            property.ContainingType is null ||
            !SameType(property.ContainingType, unionType))
        {
            return false;
        }

        foreach (var candidate in variantNames)
        {
            if (string.Equals(property.Name, "Is" + candidate, StringComparison.Ordinal))
            {
                variantName = candidate;
                return true;
            }
        }

        return false;
    }

    public static bool ReferencesGeneratedProperty(
        IOperation operation,
        INamedTypeSymbol unionType,
        ImmutableArray<string> variantNames,
        IOperation targetInstance,
        Func<IOperation, IOperation, bool> sameInstance)
    {
        operation = StripConversions(operation);
        if (operation is IPropertyReferenceOperation propertyReference &&
            TryGetGeneratedProperty(propertyReference, unionType, variantNames, out _))
        {
            return propertyReference.Instance is not null &&
                   sameInstance(propertyReference.Instance, targetInstance);
        }

        if (operation is IUnaryOperation unary && unary.OperatorKind == UnaryOperatorKind.Not)
            return ReferencesGeneratedProperty(unary.Operand, unionType, variantNames, targetInstance, sameInstance);

        if (operation is IBinaryOperation binary &&
            binary.OperatorKind is BinaryOperatorKind.Equals or BinaryOperatorKind.NotEquals)
        {
            return ReferencesGeneratedProperty(binary.LeftOperand, unionType, variantNames, targetInstance, sameInstance) ||
                   ReferencesGeneratedProperty(binary.RightOperand, unionType, variantNames, targetInstance, sameInstance);
        }

        return false;
    }

    private static bool TryGetPredicate(
        IOperation operation,
        INamedTypeSymbol unionType,
        ImmutableArray<string> variantNames,
        out Predicate predicate)
    {
        operation = StripConversions(operation);
        if (operation is IPropertyReferenceOperation propertyReference &&
            TryGetGeneratedProperty(propertyReference, unionType, variantNames, out var variantName))
        {
            predicate = new Predicate(variantName);
            return true;
        }

        predicate = default;
        return false;
    }

    private static bool TryGetPatternBoolean(IPatternOperation pattern, out bool value)
    {
        if (pattern is IConstantPatternOperation constant &&
            TryGetBooleanConstant(constant.Value, out value))
        {
            return true;
        }

        if (pattern is INegatedPatternOperation negated &&
            TryGetPatternBoolean(negated.Pattern, out value))
        {
            value = !value;
            return true;
        }

        value = false;
        return false;
    }

    private static bool TryGetBooleanConstant(IOperation? operation, out bool value)
    {
        if (operation is not null)
        {
            operation = StripConversions(operation);
            if (operation.ConstantValue is { HasValue: true, Value: bool constant })
            {
                value = constant;
                return true;
            }
        }

        value = false;
        return false;
    }

    private static ImmutableArray<string> ResolveComparison(
        Predicate predicate,
        bool constant,
        BinaryOperatorKind operatorKind,
        ImmutableArray<string> variantNames)
    {
        var positive = operatorKind == BinaryOperatorKind.Equals
            ? constant
            : !constant;
        return positive
            ? ImmutableArray.Create(predicate.VariantName)
            : Except(variantNames, predicate.VariantName);
    }

    private static ImmutableArray<string> Except(
        ImmutableArray<string> variantNames,
        string excluded)
    {
        var builder = ImmutableArray.CreateBuilder<string>();
        foreach (var variant in variantNames)
        {
            if (!string.Equals(variant, excluded, StringComparison.Ordinal))
                builder.Add(variant);
        }
        return builder.ToImmutable();
    }

    private static ImmutableArray<string> Complement(
        ImmutableArray<string> variantNames,
        ImmutableArray<string> included)
    {
        var includedSet = new HashSet<string>(included, StringComparer.Ordinal);
        var builder = ImmutableArray.CreateBuilder<string>();
        foreach (var variant in variantNames)
        {
            if (!includedSet.Contains(variant))
                builder.Add(variant);
        }
        return builder.ToImmutable();
    }

    private static IOperation StripConversions(IOperation operation)
    {
        while (operation is IConversionOperation conversion)
            operation = conversion.Operand;
        return operation;
    }

    private static bool SameType(ITypeSymbol left, ITypeSymbol right)
        => SymbolEqualityComparer.Default.Equals(left, right);

    private readonly struct Predicate
    {
        public string VariantName { get; }
        public Predicate(string variantName)
        {
            VariantName = variantName;
        }
    }
}
