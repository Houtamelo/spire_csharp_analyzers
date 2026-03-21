using Spire.Analyzers.Rules;

namespace Spire.Analyzers.Tests.SPIRE015;

public class SPIRE015Tests : AnalyzerTestBase<SPIRE015ExhaustiveEnumSwitchAnalyzer>
{
    protected override string RuleId => "SPIRE015";
}
