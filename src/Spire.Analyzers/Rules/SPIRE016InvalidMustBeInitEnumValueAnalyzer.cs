using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Spire.Analyzers.Utils;

namespace Spire.Analyzers.Rules;

/// Flags integer-to-enum casts where the value may not correspond to a named member.
/// Other enum-related checks (default, array, clear, SkipInit, etc.) are handled by SPIRE001-008.
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SPIRE016InvalidMustBeInitEnumValueAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Descriptors.SPIRE016_InvalidMustBeInitEnumValue);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var mustBeInitType = compilationContext.Compilation
                .GetTypeByMetadataName("Spire.MustBeInitAttribute");

            if (mustBeInitType is null)
                return;

            compilationContext.RegisterOperationAction(
                operationContext => AnalyzeConversion(operationContext, mustBeInitType),
                OperationKind.Conversion);
        });
    }

    private static void AnalyzeConversion(
        OperationAnalysisContext context,
        INamedTypeSymbol mustBeInitType)
    {
        var operation = (IConversionOperation)context.Operation;

        if (operation.IsImplicit)
            return;

        var targetType = operation.Type as INamedTypeSymbol;
        if (targetType is null)
            return;

        if (targetType.TypeKind != TypeKind.Enum)
            return;

        if (!MustBeInitChecks.HasMustBeInitAttribute(targetType, mustBeInitType))
            return;

        var sourceType = operation.Operand.Type;
        if (sourceType is null)
            return;

        if (!IsIntegerType(sourceType))
            return;

        var constantValue = operation.Operand.ConstantValue;
        if (constantValue.HasValue && IsNamedMemberValue(targetType, constantValue.Value))
            return;

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptors.SPIRE016_InvalidMustBeInitEnumValue,
                operation.Syntax.GetLocation(),
                "Integer cast",
                targetType.Name));
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
