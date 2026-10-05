using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Spire.Analyzers.Rules;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class {{ANALYZER_NAME}}Analyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Descriptors.{{DESCRIPTOR_FIELD}});

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // TODO: Register analysis actions
        // Use CompilationStartAction for type resolution:
        // context.RegisterCompilationStartAction(compilationContext => { ... });
        //
        // Use OperationAction for IOperation-based analysis:
        // compilationContext.RegisterOperationAction(ctx => { ... }, OperationKind.Xxx);
    }
}
