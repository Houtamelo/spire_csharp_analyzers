using Spire.Analyzers.Rules;

namespace Spire.Analyzers.Tests.SPIRE008;

public class SPIRE008Tests : AnalyzerTestBase<SPIRE008GetUninitializedObjectOfMustBeInitStructAnalyzer>
{
    protected override string RuleId => "SPIRE008";
}
