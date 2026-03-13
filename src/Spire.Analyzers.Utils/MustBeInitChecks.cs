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
}
