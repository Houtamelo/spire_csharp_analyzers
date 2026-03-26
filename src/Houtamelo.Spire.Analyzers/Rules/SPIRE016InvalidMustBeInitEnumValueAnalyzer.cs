using System.Collections.Immutable;
using Houtamelo.Spire.Analyzers.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Houtamelo.Spire.Analyzers.Rules;

/// Flags integer-to-enum casts where the value may not correspond to a named member.
/// Other enum-related checks (default, array, clear, SkipInit, etc.) are handled by SPIRE001-008.
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SPIRE016InvalidEnforceInitializationEnumValueAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Descriptors.SPIRE016_InvalidEnforceInitializationEnumValue);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var enforceInitializationType = compilationContext.Compilation
                .GetTypeByMetadataName("Houtamelo.Spire.Core.EnforceInitializationAttribute");

            if (enforceInitializationType is null)
                return;

            var flagsAttributeType = compilationContext.Compilation
                .GetTypeByMetadataName("System.FlagsAttribute");

            compilationContext.RegisterOperationAction(
                operationContext => AnalyzeConversion(operationContext, enforceInitializationType, flagsAttributeType),
                OperationKind.Conversion);
        });
    }

    private static void AnalyzeConversion(
        OperationAnalysisContext context,
        INamedTypeSymbol enforceInitializationType,
        INamedTypeSymbol? flagsAttributeType)
    {
        var operation = (IConversionOperation)context.Operation;

        if (operation.IsImplicit)
            return;

        var targetType = operation.Type as INamedTypeSymbol;
        if (targetType is null)
            return;

        if (targetType.TypeKind != TypeKind.Enum)
            return;

        if (!EnforceInitializationChecks.HasEnforceInitializationAttribute(targetType, enforceInitializationType))
            return;

        var sourceType = operation.Operand.Type;
        if (sourceType is null)
            return;

        if (!IsIntegerType(sourceType) && sourceType.TypeKind != TypeKind.Enum)
            return;

        var constantValue = operation.Operand.ConstantValue;
        if (constantValue.HasValue)
        {
            bool hasFlagsAttribute = flagsAttributeType is not null
                && HasAttribute(targetType, flagsAttributeType);

            bool isValid = hasFlagsAttribute
                ? IsValidFlagsCombination(targetType, constantValue.Value)
                : IsNamedMemberValue(targetType, constantValue.Value);

            if (isValid)
                return;
        }

        string castLabel = sourceType.TypeKind == TypeKind.Enum ? "Enum cast" : "Integer cast";

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptors.SPIRE016_InvalidEnforceInitializationEnumValue,
                operation.Syntax.GetLocation(),
                castLabel,
                targetType.Name));
    }

    private static bool HasAttribute(INamedTypeSymbol type, INamedTypeSymbol attributeType)
    {
        foreach (var attr in type.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attributeType))
                return true;
        }

        return false;
    }

    /// For [Flags] enums: value 0 is valid only if a zero-valued member exists.
    /// Non-zero non-negative value is valid if all its bits are covered by named members.
    /// Negative values fall back to exact named-member check.
    private static bool IsValidFlagsCombination(INamedTypeSymbol enumType, object? constantValue)
    {
        if (constantValue is null)
            return false;

        // Try to get the value as ulong for bit manipulation.
        // For signed types, convert only when non-negative.
        if (!TryToLong(constantValue, out long longValue))
        {
            // ulong case
            if (constantValue is ulong ulongValue)
                return IsValidFlagsCombinationUlong(enumType, ulongValue);

            return IsNamedMemberValue(enumType, constantValue);
        }

        if (longValue < 0)
        {
            // Negative values can't be valid flag combinations — fall back to exact match.
            return IsNamedMemberValue(enumType, constantValue);
        }

        return IsValidFlagsCombinationUlong(enumType, (ulong)longValue);
    }

    private static bool IsValidFlagsCombinationUlong(INamedTypeSymbol enumType, ulong value)
    {
        ulong allNamedBits = 0;
        bool hasZeroMember = false;

        foreach (var member in enumType.GetMembers())
        {
            if (member is not IFieldSymbol { IsConst: true } field)
                continue;

            if (!TryToUlong(field.ConstantValue, out ulong memberValue))
                continue;

            if (memberValue == 0)
                hasZeroMember = true;
            else
                allNamedBits |= memberValue;
        }

        if (value == 0)
            return hasZeroMember;

        return (value & ~allNamedBits) == 0;
    }

    private static bool TryToUlong(object? value, out ulong result)
    {
        if (value is null)
        {
            result = 0;
            return false;
        }

        if (TryToLong(value, out long longVal) && longVal >= 0)
        {
            result = (ulong)longVal;
            return true;
        }

        if (value is ulong ul)
        {
            result = ul;
            return true;
        }

        result = 0;
        return false;
    }

    private static bool IsNamedMemberValue(INamedTypeSymbol enumType, object? constantValue)
    {
        if (constantValue is null)
            return false;

        foreach (var member in enumType.GetMembers())
        {
            if (member is not IFieldSymbol { IsConst: true } field)
                continue;

            if (AreEqualValues(field.ConstantValue, constantValue))
                return true;
        }

        return false;
    }

    private static bool AreEqualValues(object? a, object? b)
    {
        if (a is null || b is null)
            return false;

        if (TryToLong(a, out long la) && TryToLong(b, out long lb))
            return la == lb;

        if (a is ulong ua && b is ulong ub)
            return ua == ub;

        return a.Equals(b);
    }

    private static bool TryToLong(object value, out long result)
    {
        switch (value)
        {
            case int v: result = v; return true;
            case long v: result = v; return true;
            case short v: result = v; return true;
            case sbyte v: result = v; return true;
            case uint v: result = v; return true;
            case ushort v: result = v; return true;
            case byte v: result = v; return true;
            default: result = 0; return false;
        }
    }

    private static bool IsIntegerType(ITypeSymbol type)
    {
        return type.SpecialType switch
        {
            SpecialType.System_Int32 => true,
            SpecialType.System_Int64 => true,
            SpecialType.System_Int16 => true,
            SpecialType.System_SByte => true,
            SpecialType.System_UInt32 => true,
            SpecialType.System_UInt64 => true,
            SpecialType.System_UInt16 => true,
            SpecialType.System_Byte => true,
            _ => false,
        };
    }
}
