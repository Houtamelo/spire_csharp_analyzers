using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Spire.SourceGenerators.Analyzers;

namespace Spire.SourceGenerators.Tests;

public class TypeSafetyTests : GeneratorAnalyzerTestBase
{
    protected override string Category => "TypeSafety";

    protected override ImmutableArray<DiagnosticAnalyzer> GetAnalyzers()
        => ImmutableArray.Create<DiagnosticAnalyzer>(new TypeSafetyAnalyzer());

    protected override bool IsRelevantDiagnostic(RoslynDiagnostic d)
        => d.Id == "SPIRE011" || d.Id == "SPIRE012";
}
