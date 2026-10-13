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

    /// Returns a string-only CLR identity for grouping generated constructor
    /// parameters. Reference nullability and tuple element names are removed;
    /// assembly identity and constructed containing type arguments are retained.
    public static string ConstructorTypeName(ITypeSymbol type)
        => ConstructorTypeNameCore(type);

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

        var fieldEntries = entries.ToDictionary(
            entry => (entry.Variant.Name, entry.Index));
        var constructorTypes = new Dictionary<(string Variant, int Index), string>();
        foreach (var variantGroup in variantList.GroupBy(variant => string.Join(
            "\u001f",
            variant.Fields.Select(field => field.ConstructorTypeKey ?? field.TypeFullName))))
        {
            var firstVariant = variantGroup.First();
            for (var index = 0; index < firstVariant.Fields.Length; index++)
            {
                var types = variantGroup
                    .Select(variant => fieldEntries[(variant.Name, index)].Type)
                    .Where(type => type is not null)
                    .Cast<ITypeSymbol>()
                    .ToList();
                if (types.Count == 0)
                    continue;

                var firstType = types[0];
                var commonType = types.Any(type =>
                        type.TypeKind == TypeKind.Dynamic ||
                        type.SpecialType == SpecialType.System_Object)
                    ? "object"
                    : MergeType(firstType, types, compilation, constructorMerge: true)
                        ?.ToDisplayString(TypeDisplayFormat)
                        ?? firstVariant.Fields[index].TypeFullName;
                foreach (var variant in variantGroup)
                    constructorTypes[(variant.Name, index)] = commonType;
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
                            ConstructorTypeFullName = constructorTypes.TryGetValue(
                                (variant.Name, index),
                                out var constructorType)
                                ? constructorType
                                : null,
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

    private sealed record FieldEntry(
        VariantInfo Variant,
        int Index,
        FieldInfo Field,
        ITypeSymbol? Type,
        ITypeSymbol? CanonicalType);
}
