using Houtamelo.Spire.Analyzers.Rules;

namespace Houtamelo.Spire.Analyzers.Tests.SPIRE004;

public class SPIRE004Tests : AnalyzerTestBase<SPIRE004NewOfEnforceInitializationStructWithoutCtorAnalyzer>
{
    protected override string RuleId => "SPIRE004";
}
