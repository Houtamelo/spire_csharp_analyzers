using Microsoft.CodeAnalysis;

namespace Spire.Analyzers.Utils;

public static class MustBeInitChecks
{
    public static bool HasMustBeInitAttribute(ITypeSymbol type, INamedTypeSymbol mustBeInitType)
    {
        foreach (var attr in type.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, mustBeInitType))
                return true;
        }

        return false;
    }

    public static bool HasInstanceFields(ITypeSymbol type)
    {
        foreach (var member in type.GetMembers())
        {
            if (member is IFieldSymbol { IsStatic: false })
                return true;
        }

        return false;
    }

    /// Combines attribute + field checks. Does NOT check TypeKind — callers handle that.
    public static bool IsMustBeInitWithFields(ITypeSymbol type, INamedTypeSymbol mustBeInitType)
    {
        return HasMustBeInitAttribute(type, mustBeInitType) && HasInstanceFields(type);
    }

    /// Returns true if the type is a nullable-annotated reference type (T?).
    /// When true, null is explicitly allowed — callers should skip the diagnostic.
    public static bool IsNullableAnnotatedReference(ITypeSymbol? type)
    {
        return type is { IsReferenceType: true, NullableAnnotation: NullableAnnotation.Annotated };
    }
}
