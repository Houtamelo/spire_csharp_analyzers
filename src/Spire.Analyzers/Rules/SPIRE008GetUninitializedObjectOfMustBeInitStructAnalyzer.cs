using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Spire.Analyzers.Utils;

namespace Spire.Analyzers.Rules;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SPIRE008GetUninitializedObjectOfMustBeInitStructAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Descriptors.SPIRE008_GetUninitializedObjectOfMustBeInitStruct);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var runtimeHelpersType = compilationContext.Compilation
                .GetTypeByMetadataName("System.Runtime.CompilerServices.RuntimeHelpers");

            var mustBeInitType = compilationContext.Compilation
                .GetTypeByMetadataName("Spire.MustBeInitAttribute");

            if (runtimeHelpersType is null || mustBeInitType is null)
                return;

            compilationContext.RegisterOperationAction(
                operationContext => AnalyzeInvocation(operationContext, runtimeHelpersType, mustBeInitType),
                OperationKind.Invocation);
        });
    }

    private static void AnalyzeInvocation(
        OperationAnalysisContext context,
        INamedTypeSymbol runtimeHelpersType,
        INamedTypeSymbol mustBeInitType)
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

        if (!MustBeInitChecks.IsDefaultValueInvalid(namedTarget, mustBeInitType))
            return;

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptors.SPIRE008_GetUninitializedObjectOfMustBeInitStruct,
                operation.Syntax.GetLocation(),
                namedTarget.Name));
    }
}
