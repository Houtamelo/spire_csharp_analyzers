using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Spire.SourceGenerators.Analyzers;

namespace Spire.SourceGenerators.Tests;

public class FieldAccessTests : GeneratorAnalyzerTestBase
{
    protected override string Category => "FieldAccess";

    protected override ImmutableArray<DiagnosticAnalyzer> GetAnalyzers()
        => ImmutableArray.Create<DiagnosticAnalyzer>(new FieldAccessSafetyAnalyzer());

    protected override bool IsRelevantDiagnostic(Diagnostic d)
        => d.Id == "SPIRE013" || d.Id == "SPIRE014";
}
