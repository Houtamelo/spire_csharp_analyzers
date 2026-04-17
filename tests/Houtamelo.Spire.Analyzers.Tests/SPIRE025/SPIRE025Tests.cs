using Houtamelo.Spire.Analyzers.SourceGenerators.Performance.Analyzers;

namespace Houtamelo.Spire.Analyzers.Tests.SPIRE025;

public sealed class SPIRE025Tests : AnalyzerTestBase<InlinableUsageAnalyzer>
{
    protected override string RuleId => "SPIRE025";
}
