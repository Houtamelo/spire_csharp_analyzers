using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Spire.Analyzers.Utils;

namespace Spire.Analyzers.Rules;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SPIRE007UnsafeSkipInitOfEnforceInitializationStructAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Descriptors.SPIRE007_UnsafeSkipInitOfEnforceInitializationStruct);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var unsafeType = compilationContext.Compilation
                .GetTypeByMetadataName("System.Runtime.CompilerServices.Unsafe");

            var enforceInitializationType = compilationContext.Compilation
                .GetTypeByMetadataName("Houtamelo.Spire.EnforceInitializationAttribute");

            if (unsafeType is null || enforceInitializationType is null)
                return;

            compilationContext.RegisterOperationAction(
                operationContext => AnalyzeInvocation(operationContext, unsafeType, enforceInitializationType),
                OperationKind.Invocation);
        });
    }

    private static void AnalyzeInvocation(
        OperationAnalysisContext context,
        INamedTypeSymbol unsafeType,
        INamedTypeSymbol enforceInitializationType)
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

        if (!EnforceInitializationChecks.HasEnforceInitializationAttribute(namedTarget, enforceInitializationType))
            return;

        // For structs: require instance fields. For enums: always flag (garbage data).
        if (namedTarget.TypeKind == TypeKind.Struct && !EnforceInitializationChecks.HasInstanceFields(namedTarget))
            return;

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptors.SPIRE007_UnsafeSkipInitOfEnforceInitializationStruct,
                operation.Syntax.GetLocation(),
                namedTarget.Name));
    }
}
