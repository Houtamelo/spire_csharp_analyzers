using System.Collections.Immutable;
using Houtamelo.Spire.Analyzers.SourceGenerators.Analyzers.GuardFlow;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FieldAccessSafetyAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            AnalyzerDescriptors.SPIRE013_WrongVariantFieldAccess,
            AnalyzerDescriptors.SPIRE014_UnguardedFieldAccess);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var discriminatedUnionAttribute = compilationContext.Compilation
                .GetTypeByMetadataName("Houtamelo.Spire.DiscriminatedUnionAttribute");
            if (discriminatedUnionAttribute is null)
                return;

            compilationContext.RegisterOperationBlockAction(operationBlockContext =>
                GuardFlowAnalyzer.Analyze(operationBlockContext, discriminatedUnionAttribute));
        });
    }
}
