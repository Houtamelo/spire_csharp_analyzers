using System.Collections.Immutable;
using Houtamelo.Spire.Analyzers.SourceGenerators.Analyzers;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Houtamelo.Spire.SourceGenerators.Tests.TypeSafety;

public class TypeSafetyTests : GeneratorAnalyzerTestBase
{
    protected override string Category => "TypeSafety";

    protected override ImmutableArray<DiagnosticAnalyzer> GetAnalyzers()
        => ImmutableArray.Create<DiagnosticAnalyzer>(new TypeSafetyAnalyzer());

    protected override bool IsRelevantDiagnostic(RoslynDiagnostic d)
        => d.Id == "SPIRE011" || d.Id == "SPIRE012";
}
