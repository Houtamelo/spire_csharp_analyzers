using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Spire.Analyzers.Utils;

namespace Spire.Analyzers.Rules;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SPIRE007UnsafeSkipInitOfMustBeInitStructAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Descriptors.SPIRE007_UnsafeSkipInitOfMustBeInitStruct);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var unsafeType = compilationContext.Compilation
                .GetTypeByMetadataName("System.Runtime.CompilerServices.Unsafe");

            var mustBeInitType = compilationContext.Compilation
                .GetTypeByMetadataName("Spire.MustBeInitAttribute");

            if (unsafeType is null || mustBeInitType is null)
                return;

            compilationContext.RegisterOperationAction(
                operationContext => AnalyzeInvocation(operationContext, unsafeType, mustBeInitType),
                OperationKind.Invocation);
        });
    }

    private static void AnalyzeInvocation(
        OperationAnalysisContext context,
        INamedTypeSymbol unsafeType,
        INamedTypeSymbol mustBeInitType)
    {
        var operation = (IInvocationOperation)context.Operation;
        var method = operation.TargetMethod;

        if (method.Name != "SkipInit")
            return;

        if (!SymbolEqualityComparer.Default.Equals(method.ContainingType, unsafeType))
            return;

        if (method.TypeArguments.Length != 1)
            return;

        var targetType = method.TypeArguments[0];

        if (targetType is not INamedTypeSymbol namedTarget)
            return;

        if (namedTarget.TypeKind != TypeKind.Struct && namedTarget.TypeKind != TypeKind.Enum)
            return;

        if (!MustBeInitChecks.HasMustBeInitAttribute(namedTarget, mustBeInitType))
            return;

        // For structs: require instance fields. For enums: always flag (garbage data).
        if (namedTarget.TypeKind == TypeKind.Struct && !MustBeInitChecks.HasInstanceFields(namedTarget))
            return;

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptors.SPIRE007_UnsafeSkipInitOfMustBeInitStruct,
                operation.Syntax.GetLocation(),
                namedTarget.Name));
    }
}
