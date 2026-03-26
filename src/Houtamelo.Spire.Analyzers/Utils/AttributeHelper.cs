using Microsoft.CodeAnalysis;

namespace Houtamelo.Spire.Analyzers.Utils;

/// <summary>
/// Utilities for attribute class hierarchy checks.
/// </summary>
public static class AttributeHelper
{
    /// <summary>
    /// Returns true if <paramref name="type"/> has an attribute whose class is
    /// or inherits from <paramref name="attributeType"/>.
    /// </summary>
    public static bool HasOrInheritsAttribute(ITypeSymbol type, INamedTypeSymbol attributeType)
    {
        foreach (var attr in type.GetAttributes())
        {
            var attrClass = attr.AttributeClass;
            while (attrClass != null)
            {
                if (SymbolEqualityComparer.Default.Equals(attrClass, attributeType))
                    return true;
                attrClass = attrClass.BaseType;
            }
        }

        return false;
    }
}
