using Microsoft.CodeAnalysis;

namespace Houtamelo.Spire.Analyzers.Utils;

public static class EnforceInitializationChecks
{
    public static bool HasEnforceInitializationAttribute(ITypeSymbol type, INamedTypeSymbol enforceInitializationType)
    {
        foreach (var attr in type.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, enforceInitializationType))
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
    public static bool IsEnforceInitializationWithFields(ITypeSymbol type, INamedTypeSymbol enforceInitializationType)
    {
        return HasEnforceInitializationAttribute(type, enforceInitializationType) && HasInstanceFields(type);
    }

    /// Returns true if default(T) is invalid for this [EnforceInitialization] type.
    /// For structs/classes: requires instance fields (same as IsEnforceInitializationWithFields).
    /// For enums: requires no zero-valued named member (default = 0 is unnamed).
    public static bool IsDefaultValueInvalid(ITypeSymbol type, INamedTypeSymbol enforceInitializationType)
    {
        if (!HasEnforceInitializationAttribute(type, enforceInitializationType))
            return false;

        if (type.TypeKind == TypeKind.Enum)
            return type is INamedTypeSymbol named && !HasZeroValuedMember(named);

        return HasInstanceFields(type);
    }

    /// Returns true if any named member of this enum has an underlying value equal to 0.
    public static bool HasZeroValuedMember(INamedTypeSymbol enumType)
    {
        foreach (var member in enumType.GetMembers())
        {
            if (member is not IFieldSymbol { IsConst: true } field)
                continue;

            var value = field.ConstantValue;
            if (value is null)
                continue;

            if (IsZeroValue(value))
                return true;
        }

        return false;
    }

    private static bool IsZeroValue(object value)
    {
        return value switch
        {
            int v => v == 0,
            uint v => v == 0,
            long v => v == 0,
            ulong v => v == 0,
            short v => v == 0,
            ushort v => v == 0,
            byte v => v == 0,
            sbyte v => v == 0,
            _ => false,
        };
    }

    /// Returns true if the type is a nullable-annotated reference type (T?).
    /// When true, null is explicitly allowed — callers should skip the diagnostic.
    public static bool IsNullableAnnotatedReference(ITypeSymbol? type)
    {
        return type is { IsReferenceType: true, NullableAnnotation: NullableAnnotation.Annotated };
    }
}
