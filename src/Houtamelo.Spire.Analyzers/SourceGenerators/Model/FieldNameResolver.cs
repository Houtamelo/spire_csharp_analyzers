using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Model;

/// Groups CLR-equivalent storage types and merges nullable annotations.
/// Public payload fields keep their declared names and types.
internal static class FieldNameResolver
{
    private static readonly SymbolDisplayFormat TypeDisplayFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
            SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions |
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    /// Returns a string-only CLR identity for grouping generated constructor
    /// parameters. Reference nullability and tuple element names are removed;
    /// assembly identity and constructed containing type arguments are retained.
    public static string ConstructorTypeName(ITypeSymbol type)
        => ConstructorTypeNameCore(type);

    public static EquatableArray<VariantInfo> ResolveStorageTypes(
        EquatableArray<VariantInfo> variants,
        INamedTypeSymbol unionType,
        Compilation compilation)
    {
        var storageTypes = unionType.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(method => method.IsStatic && HasVariantAttribute(method))
            .SelectMany(method => method.Parameters)
            .GroupBy(parameter => ConstructorTypeName(parameter.Type), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => MergeType(group.First().Type, group.Select(parameter => parameter.Type),
                    compilation, constructorMerge: true)!.ToDisplayString(TypeDisplayFormat),
                StringComparer.Ordinal);

        return new EquatableArray<VariantInfo>(variants.Select(variant => variant with
        {
            Fields = new EquatableArray<FieldInfo>(variant.Fields.Select(field => field with
            {
                StorageTypeFullName = field.ConstructorTypeKey is { } key && storageTypes.TryGetValue(key, out var type)
                    ? type
                    : field.TypeFullName,
            }).ToImmutableArray()),
        }).ToImmutableArray());
    }

    private static bool HasVariantAttribute(IMethodSymbol method)
        => method.GetAttributes().Any(attribute =>
            attribute.AttributeClass?.Name == "VariantAttribute");

    /// Produces a constructor-only CLR identity while keeping type and
    /// assembly identity in nested generic arguments.
    private static string ConstructorTypeNameCore(ITypeSymbol type)
    {
        if (type.TypeKind == TypeKind.Dynamic ||
            type.SpecialType == SpecialType.System_Object)
            return "object";

        if (type is IArrayTypeSymbol array)
            return ConstructorTypeNameCore(array.ElementType) + ArraySuffix(array.Rank);

        if (type is IPointerTypeSymbol pointer)
            return ConstructorTypeNameCore(pointer.PointedAtType) + "*";

        if (type is INamedTypeSymbol named)
        {
            var constructorType = named.IsTupleType && named.TupleUnderlyingType is { } underlying
                ? underlying
                : named;
            var arguments = ConstructedTypeArguments(constructorType)
                .Select(ConstructorTypeNameCore)
                .ToArray();
            var name = NamedDefinitionName(constructorType.OriginalDefinition);
            var assembly = constructorType.ContainingAssembly?.Identity.ToString() ?? "";
            return assembly + "|" + name +
                (arguments.Length == 0
                    ? ""
                    : "<" + string.Join(",", arguments) + ">");
        }

        return type.ToDisplayString(TypeDisplayFormat);
    }

    private static string ArraySuffix(int rank)
        => rank == 1 ? "[]" : "[" + new string(',', rank - 1) + "]";

    private static IEnumerable<ITypeSymbol> ConstructedTypeArguments(INamedTypeSymbol type)
    {
        if (type.ContainingType is { } containing)
        {
            foreach (var argument in ConstructedTypeArguments(containing))
                yield return argument;
        }

        foreach (var argument in type.TypeArguments)
            yield return argument;
    }

    private static string NamedDefinitionName(INamedTypeSymbol type)
    {
        var name = type.Name + (type.Arity == 0 ? "" : "`" + type.Arity);
        if (type.ContainingType is { } containing)
            return NamedDefinitionName(containing) + "." + name;

        var ns = type.ContainingNamespace;
        return ns.IsGlobalNamespace
            ? "global::" + name
            : "global::" + ns.ToDisplayString() + "." + name;
    }

    private static ITypeSymbol? MergeType(
        ITypeSymbol? first,
        IEnumerable<ITypeSymbol> types,
        Compilation compilation,
        bool constructorMerge = false)
    {
        if (first is null)
            return null;

        var result = first;
        foreach (var type in types.Skip(1))
            result = MergeTwo(result, type, compilation, constructorMerge);
        return result;
    }

    /// Merges reference nullability recursively while retaining the semantic
    /// type identity selected by SymbolEqualityComparer.Default.
    private static ITypeSymbol MergeTwo(
        ITypeSymbol left,
        ITypeSymbol right,
        Compilation compilation,
        bool constructorMerge = false)
    {
        var mergedAnnotation = MergeAnnotation(left.NullableAnnotation, right.NullableAnnotation);
        ITypeSymbol merged = left;

        if (left is IArrayTypeSymbol leftArray && right is IArrayTypeSymbol rightArray &&
            leftArray.Rank == rightArray.Rank)
        {
            var element = MergeTwo(
                leftArray.ElementType,
                rightArray.ElementType,
                compilation,
                constructorMerge);
            merged = compilation.CreateArrayTypeSymbol(element, leftArray.Rank);
        }
        else if (left is IPointerTypeSymbol leftPointer && right is IPointerTypeSymbol rightPointer)
        {
            merged = compilation.CreatePointerTypeSymbol(
                MergeTwo(
                    leftPointer.PointedAtType,
                    rightPointer.PointedAtType,
                    compilation,
                    constructorMerge));
        }
        else if (left is INamedTypeSymbol leftNamed && right is INamedTypeSymbol rightNamed &&
                 SymbolEqualityComparer.Default.Equals(
                     leftNamed.OriginalDefinition,
                     rightNamed.OriginalDefinition) &&
                 leftNamed.TypeArguments.Length == rightNamed.TypeArguments.Length)
        {
            var useFlattenedTupleElements = constructorMerge &&
                leftNamed.IsTupleType &&
                rightNamed.IsTupleType &&
                leftNamed.TupleElements.Length >= 2 &&
                rightNamed.TupleElements.Length >= 2;
            var arguments = (useFlattenedTupleElements
                    ? leftNamed.TupleElements.Select(element => element.Type)
                    : leftNamed.TypeArguments)
                .Zip(
                    useFlattenedTupleElements
                        ? rightNamed.TupleElements.Select(element => element.Type)
                        : rightNamed.TypeArguments,
                    (a, b) => MergeTwo(a, b, compilation, constructorMerge))
                .ToImmutableArray();

            if (leftNamed.IsTupleType && rightNamed.IsTupleType &&
                (!constructorMerge ||
                 (leftNamed.TupleElements.Length >= 2 &&
                  rightNamed.TupleElements.Length >= 2)))
            {
                var names = leftNamed.TupleElements
                    .Select(element => (string?)element.Name)
                    .ToImmutableArray();
                merged = compilation.CreateTupleTypeSymbol(arguments, names);
            }
            else if (arguments.Length > 0)
            {
                merged = leftNamed.ConstructedFrom.Construct(arguments.ToArray());
            }
        }

        return merged.IsReferenceType
            ? merged.WithNullableAnnotation(mergedAnnotation)
            : merged;
    }

    private static NullableAnnotation MergeAnnotation(
        NullableAnnotation left,
        NullableAnnotation right)
        => left == NullableAnnotation.Annotated || right == NullableAnnotation.Annotated
            ? NullableAnnotation.Annotated
            : NullableAnnotation.NotAnnotated;

    /// Converts a type name to the agreed acronym-aware lower camel case.
    public static string CamelCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        var uppercasePrefixLength = 0;
        while (uppercasePrefixLength < name.Length &&
               char.IsUpper(name[uppercasePrefixLength]))
        {
            uppercasePrefixLength++;
        }

        if (uppercasePrefixLength == 0)
            return name;

        var lowercaseLength = uppercasePrefixLength;
        if (uppercasePrefixLength > 1 &&
            uppercasePrefixLength < name.Length &&
            char.IsLower(name[uppercasePrefixLength]))
        {
            lowercaseLength--;
        }

        return name.Substring(0, lowercaseLength).ToLowerInvariant() +
            name.Substring(lowercaseLength);
    }

}
