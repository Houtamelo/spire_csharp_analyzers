using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Analyzers;

internal enum NullableKind
{
    ValueType,
    NullableValueType,
    RefType,
    NullableRefType,
}

/// Detects whether a type symbol is a discriminated union and extracts variant info.
internal sealed class UnionTypeInfo
{
    public bool IsStructUnion { get; }
    public bool IsRecordUnion => !IsStructUnion;
    public NullableKind Nullability { get; }
    public ImmutableArray<string> VariantNames { get; }
    /// For record unions: the variant type symbols (for type pattern matching).
    public ImmutableArray<INamedTypeSymbol> VariantTypes { get; }
    /// For struct unions: the nested Kind enum type (for constant pattern matching).
    public INamedTypeSymbol? KindEnumType { get; }

    private UnionTypeInfo(
        bool isStruct,
        NullableKind nullability,
        ImmutableArray<string> names,
        ImmutableArray<INamedTypeSymbol> types,
        INamedTypeSymbol? kindEnumType = null)
    {
        IsStructUnion = isStruct;
        Nullability = nullability;
        VariantNames = names;
        VariantTypes = types;
        KindEnumType = kindEnumType;
    }

    /// Returns a UnionTypeInfo if the type has [DiscriminatedUnion] and valid variant structure, null otherwise.
    /// Handles Nullable&lt;T&gt; unwrapping for struct unions internally.
    public static UnionTypeInfo? TryCreate(ITypeSymbol type, INamedTypeSymbol duAttributeType)
    {
        // Unwrap Nullable<T> for value types
        bool isNullableValueType = type is INamedTypeSymbol named
            && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
            && named.TypeArguments.Length == 1;

        if (isNullableValueType)
            type = ((INamedTypeSymbol)type).TypeArguments[0];

        if (!HasAttribute(type, duAttributeType))
            return null;

        NullableKind nullability;
        if (type.IsValueType)
            nullability = isNullableValueType ? NullableKind.NullableValueType : NullableKind.ValueType;
        else
            nullability = type.NullableAnnotation != NullableAnnotation.NotAnnotated
                ? NullableKind.NullableRefType
                : NullableKind.RefType;

        if (type.IsValueType)
            return TryCreateStruct(type, nullability);
        else
            return TryCreateRecord(type, nullability);
    }

    private static UnionTypeInfo? TryCreateStruct(ITypeSymbol type, NullableKind nullability)
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

        return new UnionTypeInfo(true, nullability, names, ImmutableArray<INamedTypeSymbol>.Empty, kindEnum);
    }

    private static UnionTypeInfo? TryCreateRecord(ITypeSymbol type, NullableKind nullability)
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
        return new UnionTypeInfo(false, nullability, names, variants);
    }

    private static bool HasAttribute(ITypeSymbol type, INamedTypeSymbol attributeType)
    {
        return type.GetAttributes().Any(a =>
            SymbolEqualityComparer.Default.Equals(a.AttributeClass, attributeType));
    }
}
