using Houtamelo.Spire.Analyzers.SourceGenerators.Performance.Analyzers;

namespace Houtamelo.Spire.Analyzers.Tests.SPIRE023;

public sealed class SPIRE023Tests : AnalyzerTestBase<InlinableUsageAnalyzer>
{
    protected override string RuleId => "SPIRE023";
}
