using System.Collections.Immutable;
using Houtamelo.Spire.Analyzers.SourceGenerators.Analyzers;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Houtamelo.Spire.SourceGenerators.Tests.FieldAccess;

public class FieldAccessTests : GeneratorAnalyzerTestBase
{
    protected override string Category => "FieldAccess";

    protected override ImmutableArray<DiagnosticAnalyzer> GetAnalyzers()
        => ImmutableArray.Create<DiagnosticAnalyzer>(new FieldAccessSafetyAnalyzer());

    protected override bool IsRelevantDiagnostic(RoslynDiagnostic d)
        => d.Id == "SPIRE013" || d.Id == "SPIRE014";
}
