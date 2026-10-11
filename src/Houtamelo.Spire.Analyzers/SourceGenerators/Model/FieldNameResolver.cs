using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Model;

/// Resolves the public property name and type for each struct-union field.
/// Factory and payload metadata remain on FieldInfo.Name and TypeFullName.
internal static class FieldNameResolver
{
    private static readonly SymbolDisplayFormat TypeDisplayFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
            SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions |
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    public static EquatableArray<VariantInfo> Resolve(
        EquatableArray<VariantInfo> variants,
        INamedTypeSymbol unionType,
        Compilation compilation)
    {
        var variantList = variants.ToList();
        var methods = unionType.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(method => method.IsStatic && HasVariantAttribute(method))
            .GroupBy(method => method.Name, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToDictionary(method => method.Name, StringComparer.Ordinal);

        var entries = variantList
            .SelectMany(variant =>
            {
                methods.TryGetValue(variant.Name, out var method);
                return variant.Fields.Select((field, index) => new FieldEntry(
                    variant,
                    index,
                    field,
                    method is not null && index < method.Parameters.Length
                        ? method.Parameters[index].Type
                        : null,
                    method is not null && index < method.Parameters.Length
                        ? Canonicalize(method.Parameters[index].Type, compilation)
                        : null));
            })
            .ToList();

        var resolved = new Dictionary<(string Variant, int Index), (string Name, string Type)>();
        foreach (var group in entries.GroupBy(entry => entry.Field.Name, StringComparer.Ordinal))
        {
            var groupEntries = group.ToList();
            var typeGroups = groupEntries
                .GroupBy(entry => entry.CanonicalType, SymbolEqualityComparer.Default)
                .ToList();
            var incompatible = typeGroups.Count > 1;
            var mergedType = !incompatible && groupEntries.Count > 1
                ? MergeType(
                    groupEntries.Select(entry => entry.Type)
                        .FirstOrDefault(type => type is not null),
                    groupEntries.Select(entry => entry.Type)
                        .Where(type => type is not null)
                        .Cast<ITypeSymbol>(),
                    compilation)
                : null;
            var mergedTypeName = mergedType?.ToDisplayString(TypeDisplayFormat);

            foreach (var entry in groupEntries)
            {
                var propertyName = incompatible
                    ? CamelCase(entry.Variant.Name) + "_" + entry.Field.Name
                    : entry.Field.Name;
                var propertyType = incompatible
                    ? entry.Field.TypeFullName
                    : mergedTypeName ?? entry.Field.TypeFullName;
                resolved[(entry.Variant.Name, entry.Index)] = (propertyName, propertyType);
            }
        }

        var result = variantList
            .Select(variant => variant with
            {
                Fields = new EquatableArray<FieldInfo>(variant.Fields
                    .Select((field, index) =>
                    {
                        if (!resolved.TryGetValue((variant.Name, index), out var value))
                            return field;

                        return field with
                        {
                            PropertyName = value.Name == field.Name ? null : value.Name,
                            PropertyTypeFullName = value.Type == field.TypeFullName ? null : value.Type,
                        };
                    })
                    .ToImmutableArray())
            })
            .ToImmutableArray();

        return new EquatableArray<VariantInfo>(result);
    }

    private static bool HasVariantAttribute(IMethodSymbol method)
        => method.GetAttributes().Any(attribute =>
            attribute.AttributeClass?.Name == "VariantAttribute");

    /// Produces a semantic grouping symbol while dropping tuple element names
    /// and reference-nullability annotations. Assembly identity and value-type
    /// Nullable<T> structure remain part of the symbol.
    private static ITypeSymbol Canonicalize(
        ITypeSymbol type,
        Compilation compilation)
    {
        ITypeSymbol canonical = type;
        if (type is IArrayTypeSymbol array)
        {
            canonical = compilation.CreateArrayTypeSymbol(
                Canonicalize(array.ElementType, compilation),
                array.Rank);
        }
        else if (type is IPointerTypeSymbol pointer)
        {
            canonical = compilation.CreatePointerTypeSymbol(
                Canonicalize(pointer.PointedAtType, compilation));
        }
        else if (type is INamedTypeSymbol named)
        {
            if (named.IsTupleType)
            {
                canonical = compilation.CreateTupleTypeSymbol(
                    named.TupleElements
                        .Select(element => Canonicalize(element.Type, compilation))
                        .ToImmutableArray());
            }
            else if (named.TypeArguments.Length > 0)
            {
                canonical = named.ConstructedFrom.Construct(
                    named.TypeArguments
                        .Select(argument => Canonicalize(argument, compilation))
                        .ToArray());
            }
        }

        return canonical.IsReferenceType
            ? canonical.WithNullableAnnotation(NullableAnnotation.NotAnnotated)
            : canonical;
    }

    private static ITypeSymbol? MergeType(
        ITypeSymbol? first,
        IEnumerable<ITypeSymbol> types,
        Compilation compilation)
    {
        if (first is null)
            return null;

        var result = first;
        foreach (var type in types.Skip(1))
            result = MergeTwo(result, type, compilation);
        return result;
    }

    /// Merges reference nullability recursively while retaining the semantic
    /// type identity selected by SymbolEqualityComparer.Default.
    private static ITypeSymbol MergeTwo(
        ITypeSymbol left,
        ITypeSymbol right,
        Compilation compilation)
    {
        var mergedAnnotation = MergeAnnotation(left.NullableAnnotation, right.NullableAnnotation);
        ITypeSymbol merged = left;

        if (left is IArrayTypeSymbol leftArray && right is IArrayTypeSymbol rightArray &&
            leftArray.Rank == rightArray.Rank)
        {
            var element = MergeTwo(leftArray.ElementType, rightArray.ElementType, compilation);
            merged = compilation.CreateArrayTypeSymbol(element, leftArray.Rank);
        }
        else if (left is IPointerTypeSymbol leftPointer && right is IPointerTypeSymbol rightPointer)
        {
            merged = compilation.CreatePointerTypeSymbol(
                MergeTwo(leftPointer.PointedAtType, rightPointer.PointedAtType, compilation));
        }
        else if (left is INamedTypeSymbol leftNamed && right is INamedTypeSymbol rightNamed &&
                 SymbolEqualityComparer.Default.Equals(
                     leftNamed.OriginalDefinition,
                     rightNamed.OriginalDefinition) &&
                 leftNamed.TypeArguments.Length == rightNamed.TypeArguments.Length)
        {
            var arguments = leftNamed.TypeArguments
                .Zip(rightNamed.TypeArguments, (a, b) => MergeTwo(a, b, compilation))
                .ToImmutableArray();

            if (leftNamed.IsTupleType && rightNamed.IsTupleType)
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

    private sealed record FieldEntry(
        VariantInfo Variant,
        int Index,
        FieldInfo Field,
        ITypeSymbol? Type,
        ITypeSymbol? CanonicalType);
}
