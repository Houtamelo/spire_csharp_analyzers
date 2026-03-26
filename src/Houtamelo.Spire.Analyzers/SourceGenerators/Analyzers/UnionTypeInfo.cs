using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Analyzers;

/// Detects whether a type symbol is a discriminated union and extracts variant info.
internal sealed class UnionTypeInfo
{
    public bool IsStructUnion { get; }
    public bool IsRecordUnion => !IsStructUnion;
    public ImmutableArray<string> VariantNames { get; }
    /// For record unions: the variant type symbols (for type pattern matching).
    public ImmutableArray<INamedTypeSymbol> VariantTypes { get; }
    /// For struct unions: the nested Kind enum type (for constant pattern matching).
    public INamedTypeSymbol? KindEnumType { get; }

    private UnionTypeInfo(
        bool isStruct,
        ImmutableArray<string> names,
        ImmutableArray<INamedTypeSymbol> types,
        INamedTypeSymbol? kindEnumType = null)
    {
        IsStructUnion = isStruct;
        VariantNames = names;
        VariantTypes = types;
        KindEnumType = kindEnumType;
    }

    /// Returns a UnionTypeInfo if the type has [DiscriminatedUnion] and valid variant structure, null otherwise.
    public static UnionTypeInfo? TryCreate(ITypeSymbol type, INamedTypeSymbol duAttributeType)
    {
        if (!HasAttribute(type, duAttributeType))
            return null;

        if (type.IsValueType)
            return TryCreateStruct(type);
        else
            return TryCreateRecord(type);
    }

    /// Unwraps Nullable<T> and checks reference type nullability.
    /// Returns the union info (or null) and whether the type is nullable.
    public static UnionTypeInfo? TryCreateWithNullableUnwrap(
        ITypeSymbol type, INamedTypeSymbol duAttr, out bool isNullable)
    {
        isNullable = false;

        // Unwrap Nullable<T> for value types
        if (type is INamedTypeSymbol named
            && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
            && named.TypeArguments.Length == 1)
        {
            type = named.TypeArguments[0];
            isNullable = true;
        }

        var info = TryCreate(type, duAttr);
        if (info is null)
            return null;

        // Reference types: nullable unless explicitly non-nullable in #nullable enable context
        if (!isNullable && !type.IsValueType)
        {
            isNullable = type.NullableAnnotation != NullableAnnotation.NotAnnotated;
        }

        return info;
    }

    private static UnionTypeInfo? TryCreateStruct(ITypeSymbol type)
    {
        // Struct unions have a nested "Kind" enum with one field per variant
        var kindEnum = type.GetTypeMembers("Kind")
            .FirstOrDefault(t => t.TypeKind == TypeKind.Enum);

        if (kindEnum is null)
            return null;

        var names = kindEnum.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(f => f.HasConstantValue)
            .Select(f => f.Name)
            .ToImmutableArray();

        return new UnionTypeInfo(true, names, ImmutableArray<INamedTypeSymbol>.Empty, kindEnum);
    }

    private static UnionTypeInfo? TryCreateRecord(ITypeSymbol type)
    {
        // Record unions have sealed nested types that inherit from the union
        var variants = type.GetTypeMembers()
            .Where(nested => nested.IsSealed &&
                SymbolEqualityComparer.Default.Equals(
                    nested.BaseType?.OriginalDefinition,
                    type.OriginalDefinition))
            .ToImmutableArray();

        if (variants.Length == 0)
            return null;

        var names = variants.Select(v => v.Name).ToImmutableArray();
        return new UnionTypeInfo(false, names, variants);
    }

    private static bool HasAttribute(ITypeSymbol type, INamedTypeSymbol attributeType)
    {
        return type.GetAttributes().Any(a =>
            SymbolEqualityComparer.Default.Equals(a.AttributeClass, attributeType));
    }
}
