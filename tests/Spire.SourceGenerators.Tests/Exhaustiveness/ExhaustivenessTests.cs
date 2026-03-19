using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Spire.SourceGenerators.Analyzers;

namespace Spire.SourceGenerators.Tests;

public class ExhaustivenessTests : GeneratorAnalyzerTestBase
{
    protected override string Category => "Exhaustiveness";

    protected override ImmutableArray<DiagnosticAnalyzer> GetAnalyzers()
        => ImmutableArray.Create<DiagnosticAnalyzer>(new ExhaustivenessAnalyzer());

    protected override bool IsRelevantDiagnostic(Diagnostic d)
        => d.Id == "SPIRE009" || d.Id == "SPIRE010";
}
