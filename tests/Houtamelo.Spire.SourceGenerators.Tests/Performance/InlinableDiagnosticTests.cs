using System.Collections.Immutable;
using Houtamelo.Spire.Analyzers.SourceGenerators.Performance.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Houtamelo.Spire.SourceGenerators.Tests.Performance;

public sealed class InlinableDiagnosticTests : GeneratorAnalyzerTestBase
{
    protected override string Category => "Performance/InlinableDiagnostics";

    protected override ImmutableArray<DiagnosticAnalyzer> GetAnalyzers()
        => ImmutableArray.Create<DiagnosticAnalyzer>(new InlinableUsageAnalyzer());

    protected override bool IsRelevantDiagnostic(Microsoft.CodeAnalysis.Diagnostic diagnostic)
        => diagnostic.Id == "SPIRE025";
}
