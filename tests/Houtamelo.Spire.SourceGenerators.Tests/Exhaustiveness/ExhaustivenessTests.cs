using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Spire.SourceGenerators.Analyzers;

namespace Spire.SourceGenerators.Tests;

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
