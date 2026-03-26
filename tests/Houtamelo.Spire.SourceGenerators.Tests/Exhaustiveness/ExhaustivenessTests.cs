using System.Collections.Immutable;
using Houtamelo.Spire.Analyzers.SourceGenerators.Analyzers;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Houtamelo.Spire.SourceGenerators.Tests.Exhaustiveness;

public class ExhaustivenessTests : GeneratorAnalyzerTestBase
{
    protected override string Category => "Exhaustiveness";

    protected override ImmutableArray<DiagnosticAnalyzer> GetAnalyzers()
        => ImmutableArray.Create<DiagnosticAnalyzer>(
            new ExhaustivenessAnalyzer(),
            new CS8509Suppressor());

    protected override bool IsRelevantDiagnostic(RoslynDiagnostic d)
        => d.Id == "SPIRE009";
}
