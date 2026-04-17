using Houtamelo.Spire.Analyzers.SourceGenerators.Performance.Analyzers;

namespace Houtamelo.Spire.Analyzers.Tests.SPIRE024;

public sealed class SPIRE024Tests : AnalyzerTestBase<InlinableUsageAnalyzer>
{
    protected override string RuleId => "SPIRE024";
}
