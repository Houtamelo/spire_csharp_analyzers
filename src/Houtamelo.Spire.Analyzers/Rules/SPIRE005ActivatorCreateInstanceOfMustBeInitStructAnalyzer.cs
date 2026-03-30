using System.Collections.Immutable;
using Houtamelo.Spire.Analyzers.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Houtamelo.Spire.Analyzers.Rules;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SPIRE005ActivatorCreateInstanceOfEnforceInitializationStructAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Descriptors.SPIRE005_ActivatorCreateInstanceOfEnforceInitializationStruct);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var activatorType = compilationContext.Compilation
                .GetTypeByMetadataName("System.Activator");

            var enforceInitializationType = compilationContext.Compilation
                .GetTypeByMetadataName("Houtamelo.Spire.EnforceInitializationAttribute");

            var arrayType = compilationContext.Compilation
                .GetTypeByMetadataName("System.Array");

            if (activatorType is null || enforceInitializationType is null)
                return;

            var enforceOnAllEnums = GlobalConfigHelper.ReadEnforceExhaustivenessOnAllEnumTypes(compilationContext.Options);

            compilationContext.RegisterOperationAction(
                operationContext => AnalyzeInvocation(operationContext, activatorType, enforceInitializationType, enforceOnAllEnums, arrayType),
                OperationKind.Invocation);
        });
    }

    private static void AnalyzeInvocation(
        OperationAnalysisContext context,
        INamedTypeSymbol activatorType,
        INamedTypeSymbol enforceInitializationType,
        bool enforceOnAllEnums,
        INamedTypeSymbol? arrayType)
    {
        var operation = (IInvocationOperation)context.Operation;

        var method = operation.TargetMethod;

        if (!SymbolEqualityComparer.Default.Equals(method.ContainingType, activatorType))
            return;

        if (method.Name != "CreateInstance")
            return;

        ITypeSymbol? targetType;
        int? argsParamIndex;

        if (method.IsGenericMethod && method.TypeArguments.Length == 1 && method.Parameters.Length == 0)
        {
            // CreateInstance<T>() — generic, no params
            targetType = method.TypeArguments[0];
            argsParamIndex = null; // no args to check — always flag
        }
        else
        {
            // Skip string-based overloads
            if (method.Parameters.Length == 0)
                return;

            var firstParam = method.Parameters[0];
            if (firstParam.Type.SpecialType == SpecialType.System_String)
                return;

            if (operation.Arguments.Length == 0)
                return;

            var firstArg = operation.Arguments[0].Value;
            while (firstArg is IConversionOperation { IsImplicit: true } conv)
                firstArg = conv.Operand;

            if (firstArg is not ITypeOfOperation typeOfOp)
                return;

            targetType = typeOfOp.TypeOperand;

            argsParamIndex = GetArgsParamIndex(method);
        }

        if (targetType is not INamedTypeSymbol namedTarget)
            return;

        if (namedTarget.TypeKind != TypeKind.Struct && namedTarget.TypeKind != TypeKind.Enum)
            return;

        if (!EnforceInitializationChecks.IsDefaultValueInvalid(namedTarget, enforceInitializationType, enforceOnAllEnums))
            return;
        if (argsParamIndex.HasValue)
        {
            if (argsParamIndex.Value < 0 || argsParamIndex.Value >= operation.Arguments.Length)
                return;

            var argsArg = operation.Arguments[argsParamIndex.Value].Value;
            while (argsArg is IConversionOperation { IsImplicit: true } conv)
                argsArg = conv.Operand;

            if (!IsNullOrEmptyArray(argsArg, arrayType))
                return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptors.SPIRE005_ActivatorCreateInstanceOfEnforceInitializationStruct,
                operation.Syntax.GetLocation(),
                namedTarget.Name));
    }

    /// Returns the index of the constructor args (object[]) parameter, or null if the overload
    /// always produces a default instance regardless of args (1-param and 2-param bool overloads).
    private static int? GetArgsParamIndex(IMethodSymbol method)
    {
        var paramCount = method.Parameters.Length;

        switch (paramCount)
        {
            case 1:
                // CreateInstance(Type) — always produces default
                return null;

            case 2:
                // CreateInstance(Type, bool nonPublic) — always produces default
                // CreateInstance(Type, params object[] args) — check args at index 1
                if (method.Parameters[1].Type.SpecialType == SpecialType.System_Boolean)
                    return null;
                return 1;

            case 3:
                // CreateInstance(Type, object[] args, object[] activationAttributes) — check args at index 1
                return 1;

            case 5:
                // CreateInstance(Type, BindingFlags, Binder, object[], CultureInfo) — check args at index 3
                return 3;

            case 6:
                // CreateInstance(Type, BindingFlags, Binder, object[], CultureInfo, object[]) — check args at index 3
                return 3;

            default:
                // Unknown overload — skip
                return -1; // signal "skip"
        }
    }

    private static bool IsNullOrEmptyArray(IOperation operation, INamedTypeSymbol? arrayType)
    {
        switch (operation)
        {
            case ILiteralOperation literal:
                return literal.ConstantValue.HasValue && literal.ConstantValue.Value is null;

            case IDefaultValueOperation:
                return true;

            case IConversionOperation conversion:
                return IsNullOrEmptyArray(conversion.Operand, arrayType);

            case IArrayCreationOperation arrayCreation:
                if (arrayCreation.Initializer is { ElementValues.Length: 0 })
                    return true;

                if (arrayCreation.DimensionSizes.Length == 1)
                {
                    var dimOp = arrayCreation.DimensionSizes[0];
                    if (dimOp.ConstantValue.HasValue && dimOp.ConstantValue.Value is int size && size == 0)
                        return true;
                }

                return false;

            case IInvocationOperation invocation:
                if (arrayType is null)
                    return false;

                return invocation.TargetMethod.Name == "Empty"
                    && SymbolEqualityComparer.Default.Equals(invocation.TargetMethod.ContainingType, arrayType);

            default:
                return false;
        }
    }

}
