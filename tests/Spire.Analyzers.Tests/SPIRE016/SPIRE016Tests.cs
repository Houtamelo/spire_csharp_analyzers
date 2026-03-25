using Spire.Analyzers.Rules;

namespace Spire.Analyzers.Tests.SPIRE016;

public class SPIRE016Tests : AnalyzerTestBase<SPIRE016InvalidEnforceInitializationEnumValueAnalyzer>
{
    protected override string RuleId => "SPIRE016";
}
