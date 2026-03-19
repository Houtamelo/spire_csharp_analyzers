using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Spire.SourceGenerators.Analyzers;

/// Detects whether a type symbol is a discriminated union and extracts variant info.
internal sealed class UnionTypeInfo
{
    public bool IsStructUnion { get; }
    public bool IsRecordOrClassUnion => !IsStructUnion;
    public ImmutableArray<string> VariantNames { get; }
    /// For record/class unions: the variant type symbols (for type pattern matching).
    public ImmutableArray<INamedTypeSymbol> VariantTypes { get; }

    private UnionTypeInfo(bool isStruct, ImmutableArray<string> names, ImmutableArray<INamedTypeSymbol> types)
    {
        IsStructUnion = isStruct;
        VariantNames = names;
        VariantTypes = types;
    }

    /// Returns a UnionTypeInfo if the type has [DiscriminatedUnion] and valid variant structure, null otherwise.
    public static UnionTypeInfo? TryCreate(ITypeSymbol type, INamedTypeSymbol duAttributeType)
    {
        if (!HasAttribute(type, duAttributeType))
            return null;

        if (type.IsValueType)
            return TryCreateStruct(type);
        else
            return TryCreateRecordOrClass(type);
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

        return new UnionTypeInfo(true, names, ImmutableArray<INamedTypeSymbol>.Empty);
    }

    private static UnionTypeInfo? TryCreateRecordOrClass(ITypeSymbol type)
    {
        // Record/class unions have sealed nested types that inherit from the union
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
