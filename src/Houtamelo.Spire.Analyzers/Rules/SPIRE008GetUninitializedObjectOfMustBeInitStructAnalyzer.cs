using System.Collections.Immutable;
using Houtamelo.Spire.Analyzers.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Houtamelo.Spire.Analyzers.Rules;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SPIRE008GetUninitializedObjectOfEnforceInitializationStructAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Descriptors.SPIRE008_GetUninitializedObjectOfEnforceInitializationStruct);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var runtimeHelpersType = compilationContext.Compilation
                .GetTypeByMetadataName("System.Runtime.CompilerServices.RuntimeHelpers");

            var enforceInitializationType = compilationContext.Compilation
                .GetTypeByMetadataName("Houtamelo.Spire.EnforceInitializationAttribute");

            if (runtimeHelpersType is null || enforceInitializationType is null)
                return;

            var enforceOnAllEnums = GlobalConfigHelper.ReadEnforceExhaustivenessOnAllEnumTypes(compilationContext.Options);

            compilationContext.RegisterOperationAction(
                operationContext => AnalyzeInvocation(operationContext, runtimeHelpersType, enforceInitializationType, enforceOnAllEnums),
                OperationKind.Invocation);
        });
    }

    private static void AnalyzeInvocation(
        OperationAnalysisContext context,
        INamedTypeSymbol runtimeHelpersType,
        INamedTypeSymbol enforceInitializationType,
        bool enforceOnAllEnums)
    {
        var operation = (IInvocationOperation)context.Operation;
        var method = operation.TargetMethod;

        if (method.Name != "GetUninitializedObject")
            return;

        if (!SymbolEqualityComparer.Default.Equals(method.ContainingType, runtimeHelpersType))
            return;

        if (operation.Arguments.Length != 1)
            return;

        var arg = operation.Arguments[0].Value;
        while (arg is IConversionOperation { IsImplicit: true } conv)
            arg = conv.Operand;

        if (arg is not ITypeOfOperation typeOfOp)
            return;

        var targetType = typeOfOp.TypeOperand;

        if (targetType is not INamedTypeSymbol namedTarget)
            return;

        if (namedTarget.TypeKind != TypeKind.Struct && namedTarget.TypeKind != TypeKind.Enum)
            return;

        if (!EnforceInitializationChecks.IsDefaultValueInvalid(namedTarget, enforceInitializationType, enforceOnAllEnums))
            return;

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptors.SPIRE008_GetUninitializedObjectOfEnforceInitializationStruct,
                operation.Syntax.GetLocation(),
                namedTarget.Name));
    }
}
