using Spire.Analyzers.Rules;

namespace Spire.Analyzers.Tests.SPIRE004;

public class SPIRE004Tests : AnalyzerTestBase<SPIRE004NewOfEnforceInitializationStructWithoutCtorAnalyzer>
{
    protected override string RuleId => "SPIRE004";
}
