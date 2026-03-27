using System.Collections.Immutable;
using Houtamelo.Spire.PatternAnalysis.Domains;
using Houtamelo.Spire.PatternAnalysis.Resolution;
using Microsoft.CodeAnalysis;

namespace Houtamelo.Spire.PatternAnalysis;

/// Maps an ITypeSymbol to the appropriate IValueDomain.
internal sealed class DomainResolver
{
    private readonly Compilation _compilation;
    private readonly INamedTypeSymbol? _enforceExhaustivenessAttr;
    private readonly INamedTypeSymbol? _discriminatedUnionAttr;
    private readonly TypeHierarchyResolver _hierarchyResolver;

    public DomainResolver(Compilation compilation, TypeHierarchyResolver hierarchyResolver)
    {
        _compilation = compilation;
        _hierarchyResolver = hierarchyResolver;
        _enforceExhaustivenessAttr = compilation.GetTypeByMetadataName("Houtamelo.Spire.Core.EnforceExhaustivenessAttribute");
        _discriminatedUnionAttr = compilation.GetTypeByMetadataName("Houtamelo.Spire.DiscriminatedUnionAttribute");
    }

    public IValueDomain Resolve(ITypeSymbol type)
    {
        // 1. Nullable check — value type T? (Nullable<T>)
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } namedNullable)
        {
            var innerType = namedNullable.TypeArguments[0];
            var innerDomain = ResolveInner(innerType);
            return NullableDomain.Universe(type, innerDomain);
        }

        // 1. Nullable check — reference types
        if (!type.IsValueType)
        {
            // Annotated (string?) or None/oblivious => nullable
            if (type.NullableAnnotation != NullableAnnotation.NotAnnotated)
            {
                var innerDomain = ResolveInner(type);
                return NullableDomain.Universe(type, innerDomain);
            }
        }

        // Not nullable — resolve directly
        return ResolveInner(type);
    }

    /// Resolves the inner (non-nullable) type to a domain.
    private IValueDomain ResolveInner(ITypeSymbol type)
    {
        // Bool
        if (type.SpecialType == SpecialType.System_Boolean)
            return BoolDomain.Universe(type);

        // Enum
        if (type.TypeKind == TypeKind.Enum)
            return EnumDomain.Universe((INamedTypeSymbol)type);

        // Numeric types
        if (IsNumericType(type.SpecialType))
            return NumericDomain.Universe(type);

        // [DiscriminatedUnion] attribute — basic fallback for now
        if (_discriminatedUnionAttr != null && HasAttribute(type, _discriminatedUnionAttr))
            return new PropertyPatternDomain(type, ImmutableArray<(SlotIdentifier, IValueDomain)>.Empty, hasWildcard: false);

        // [EnforceExhaustiveness] attribute
        if (_enforceExhaustivenessAttr != null && type is INamedTypeSymbol namedType && HasAttribute(type, _enforceExhaustivenessAttr))
            return EnforceExhaustiveDomain.Create(namedType, _hierarchyResolver, _compilation);

        // Fallback — structural domain with no slots
        return new PropertyPatternDomain(type, ImmutableArray<(SlotIdentifier, IValueDomain)>.Empty, hasWildcard: false);
    }

    private static bool IsNumericType(SpecialType specialType)
    {
        switch (specialType)
        {
            case SpecialType.System_Byte:
            case SpecialType.System_SByte:
            case SpecialType.System_Int16:
            case SpecialType.System_UInt16:
            case SpecialType.System_Int32:
            case SpecialType.System_UInt32:
            case SpecialType.System_Int64:
            case SpecialType.System_UInt64:
            case SpecialType.System_Single:
            case SpecialType.System_Double:
            case SpecialType.System_Decimal:
                return true;
            default:
                return false;
        }
    }

    private static bool HasAttribute(ITypeSymbol type, INamedTypeSymbol attributeType)
    {
        foreach (var attr in type.GetAttributes())
        {
            if (attr.AttributeClass != null &&
                SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attributeType))
            {
                return true;
            }
        }

        return false;
    }
}
